-- =================================================================
-- 🎬 Watch Progress & Watched State Sync for MovieManager MPV
-- Tracks playback progress, playlist navigation & persists to sync file
-- =================================================================

local utils = require("mp.utils")
local msg = require("mp.msg")

local current_file = nil
local last_time_pos = 0
local last_duration = 0
local last_save_time = 0

-- In-memory cache of file positions during this player session
local session_file_positions = {} -- path -> { timePos = N, duration = N, isWatched = bool }
local last_file_path = nil
local last_playlist_idx = -1

local function get_sync_paths()
    local paths = {}
    local localAppData = os.getenv("LOCALAPPDATA")
    if localAppData then
        table.insert(paths, localAppData .. "\\MovieManager\\playback_sync.json")
        table.insert(paths, localAppData .. "\\MovieManagerDesktop\\playback_sync.json")
    end
    local mpvDir = mp.command_native({"expand-path", "~~/playback_sync.json"})
    if mpvDir then
        table.insert(paths, mpvDir)
    end
    return paths
end

local function read_json_file(path)
    if not path then return nil end
    local f = io.open(path, "r")
    if not f then return nil end
    local content = f:read("*a")
    f:close()
    if not content or content == "" then return nil end
    local res = utils.parse_json(content)
    return res
end

local function write_json_file(path, data)
    if not path or not data then return false end
    local json_str, err = utils.format_json(data)
    if not json_str then return false end

    local f = io.open(path, "w")
    if not f then return false end
    f:write(json_str)
    f:close()
    return true
end

local function load_all_sync_data()
    local sync_paths = get_sync_paths()
    local sync_data = {}
    for _, sp in ipairs(sync_paths) do
        local d = read_json_file(sp)
        if d and type(d) == "table" then
            for k, v in pairs(d) do
                sync_data[k] = v
            end
            break
        end
    end
    return sync_data
end

local function write_all_sync_data(sync_data)
    local sync_paths = get_sync_paths()
    for _, sp in ipairs(sync_paths) do
        write_json_file(sp, sync_data)
    end
end

local function save_custom_entry(path, time_pos, duration, is_watched)
    if not path or path == "" or path:find("^https?://") then return end

    local percent = 0
    if duration > 0 then
        percent = math.min(100.0, math.max(0.0, (time_pos / duration) * 100.0))
    end
    if is_watched then
        percent = 100.0
    end

    local sync_data = load_all_sync_data()
    local now_iso = os.date("!%Y-%m-%dT%H:%M:%SZ")
    sync_data[path] = {
        filePath = path,
        timePos = math.floor(time_pos),
        duration = math.floor(duration),
        percent = math.floor(percent * 10) / 10,
        isWatched = is_watched,
        updatedAt = now_iso
    }
    write_all_sync_data(sync_data)
end

local function save_progress(force_watched)
    local path = mp.get_property("path")
    if not path or path == "" or path:find("^https?://") then return end

    local time_pos = mp.get_property_number("time-pos", 0)
    local duration = mp.get_property_number("duration", 0)

    if time_pos == 0 and last_time_pos > 0 then
        time_pos = last_time_pos
    end
    if duration == 0 and last_duration > 0 then
        duration = last_duration
    end

    if time_pos <= 2 and not force_watched then
        return
    end

    local percent = 0
    if duration > 0 then
        percent = math.min(100.0, math.max(0.0, (time_pos / duration) * 100.0))
    end

    local is_near_end = (duration > 60 and time_pos >= (duration - 60)) or (duration <= 60 and duration > 0 and time_pos >= (duration - 5))
    local is_watched = false
    if force_watched or is_near_end then
        is_watched = true
        -- If watched to the end, preserve resume point at 20 seconds before end
        if duration > 30 and (time_pos >= (duration - 25) or force_watched) then
            time_pos = math.max(0, math.floor(duration - 20))
        end
    end

    save_custom_entry(path, time_pos, duration, is_watched)
    session_file_positions[path] = {
        timePos = math.floor(time_pos),
        duration = math.floor(duration),
        isWatched = is_watched
    }
end

local function on_tick()
    local time_pos = mp.get_property_number("time-pos", 0)
    local duration = mp.get_property_number("duration", 0)
    local path = mp.get_property("path")

    if time_pos > 0 then
        last_time_pos = time_pos
        if path then
            if not session_file_positions[path] then
                session_file_positions[path] = {}
            end
            session_file_positions[path].timePos = math.floor(time_pos)
            if duration > 0 then
                session_file_positions[path].duration = math.floor(duration)
            end
        end
    end
    if duration > 0 then
        last_duration = duration
    end

    local now = mp.get_time()
    if now - last_save_time >= 5 then
        last_save_time = now
        save_progress(false)
    end
end

local function on_file_loaded()
    local current_path = mp.get_property("path")
    if not current_path or current_path:find("^https?://") then return end

    local current_playlist_idx = mp.get_property_number("playlist-pos", 0)
    local current_duration = mp.get_property_number("duration", 0)

    -- Detect playlist navigation between episodes!
    if last_file_path and last_file_path ~= current_path and last_playlist_idx >= 0 then
        local prev_pos = last_time_pos
        local prev_dur = last_duration

        if current_playlist_idx > last_playlist_idx then
            -- =================================================================
            -- ⏩ MOVING FORWARD (Next Episode: PageDown / > / OSC button)
            -- 1. Mark previous episode as WATCHED!
            --    If watched to end, save 20s before end; else save exact position!
            -- =================================================================
            local resume_prev = math.floor(prev_pos)
            if (prev_dur > 60 and prev_pos >= prev_dur - 60) or (prev_dur <= 60 and prev_dur > 0 and prev_pos >= prev_dur - 5) then
                resume_prev = math.max(0, math.floor(prev_dur - 20))
            end

            save_custom_entry(last_file_path, resume_prev, prev_dur, true)
            session_file_positions[last_file_path] = {
                timePos = resume_prev,
                duration = prev_dur,
                isWatched = true
            }

            -- 2. Resume current episode if user was already watching it earlier!
            local saved_info = session_file_positions[current_path]
            if not saved_info then
                local all_sync = load_all_sync_data()
                if all_sync and all_sync[current_path] then
                    saved_info = all_sync[current_path]
                end
            end

            if saved_info and saved_info.timePos and saved_info.timePos > 2 then
                -- Only resume if not finished or if it was partially watched
                if not saved_info.isWatched or saved_info.timePos < (current_duration - 25) then
                    mp.commandv("seek", saved_info.timePos, "absolute", "exact")
                end
            end

        elseif current_playlist_idx < last_playlist_idx then
            -- =================================================================
            -- ⏪ MOVING BACKWARD (Previous Episode: PageUp / < / OSC button)
            -- 1. Save current position of the episode we are leaving (e.g. at 12:00)
            --    so when user advances forward again, it resumes from here!
            -- =================================================================
            if prev_pos > 2 then
                save_custom_entry(last_file_path, math.floor(prev_pos), prev_dur, false)
                session_file_positions[last_file_path] = {
                    timePos = math.floor(prev_pos),
                    duration = prev_dur,
                    isWatched = false
                }
            end

            -- 2. For the episode we are returning to:
            --    REMOVE watched tick (isWatched = false)!
            --    Seek to where user previously pressed Next, or 20s before end!
            local target_seek = 0
            local saved_prev = session_file_positions[current_path]
            if not saved_prev then
                local all_sync = load_all_sync_data()
                if all_sync and all_sync[current_path] then
                    saved_prev = all_sync[current_path]
                end
            end

            if saved_prev and saved_prev.timePos and saved_prev.timePos > 0 then
                target_seek = saved_prev.timePos
            end

            if (target_seek <= 0 or (current_duration > 30 and target_seek >= current_duration - 25)) and current_duration > 30 then
                target_seek = math.max(0, math.floor(current_duration - 20))
            end

            save_custom_entry(current_path, target_seek, current_duration, false)
            session_file_positions[current_path] = {
                timePos = target_seek,
                duration = current_duration,
                isWatched = false
            }

            if target_seek > 0 then
                mp.commandv("seek", target_seek, "absolute", "exact")
            end
        end
    end

    last_file_path = current_path
    last_playlist_idx = current_playlist_idx
    last_time_pos = 0
    last_duration = current_duration
    last_save_time = mp.get_time()
end

local function on_end_file(event)
    if event and (event.reason == "eof" or event.reason == "stop") then
        local duration = mp.get_property_number("duration", 0)
        local time_pos = mp.get_property_number("time-pos", 0)
        local is_near_end = (duration > 60 and time_pos >= duration - 60) or (duration <= 60 and duration > 0 and time_pos >= duration - 5)
        if event.reason == "eof" or is_near_end then
            save_progress(true)
        else
            save_progress(false)
        end
    else
        save_progress(false)
    end
end

local function on_shutdown()
    save_progress(false)
end

mp.register_event("file-loaded", on_file_loaded)
mp.register_event("end-file", on_end_file)
mp.register_event("shutdown", on_shutdown)
mp.observe_property("pause", "bool", function(_, paused)
    if paused then
        save_progress(false)
    end
end)

mp.add_periodic_timer(2.5, on_tick)
