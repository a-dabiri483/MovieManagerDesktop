-- =================================================================
-- 🎬 Watch Progress & Watched State Sync for MovieManager MPV
-- Tracks playback progress and persists to sync file even if main app is closed
-- =================================================================

local utils = require("mp.utils")
local msg = require("mp.msg")

local current_file = nil
local last_time_pos = 0
local last_duration = 0
local last_save_time = 0

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

local function save_progress(force_watched)
    local path = mp.get_property("path")
    if not path or path == "" then return end

    -- Skip web streams (http/https)
    if path:find("^https?://") then return end

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

    local is_watched = false
    if force_watched or percent >= 85.0 or (duration > 0 and time_pos >= (duration - 15)) then
        is_watched = true
        if force_watched then
            percent = 100.0
        end
        time_pos = 0
    end

    local sync_paths = get_sync_paths()
    local sync_data = {}

    -- Load existing records
    for _, sp in ipairs(sync_paths) do
        local d = read_json_file(sp)
        if d and type(d) == "table" then
            for k, v in pairs(d) do
                sync_data[k] = v
            end
            break
        end
    end

    -- Update entry for this file
    local now_iso = os.date("!%Y-%m-%dT%H:%M:%SZ")
    sync_data[path] = {
        filePath = path,
        timePos = math.floor(time_pos),
        duration = math.floor(duration),
        percent = math.floor(percent * 10) / 10,
        isWatched = is_watched,
        updatedAt = now_iso
    }

    -- Save to all available locations
    for _, sp in ipairs(sync_paths) do
        write_json_file(sp, sync_data)
    end
end

local function on_tick()
    local time_pos = mp.get_property_number("time-pos", 0)
    local duration = mp.get_property_number("duration", 0)
    if time_pos > 0 then
        last_time_pos = time_pos
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
    current_file = mp.get_property("path")
    last_time_pos = 0
    last_duration = 0
    last_save_time = mp.get_time()
end

local function on_end_file(event)
    if event and (event.reason == "eof" or event.reason == "stop") then
        local duration = mp.get_property_number("duration", 0)
        local time_pos = mp.get_property_number("time-pos", 0)
        if event.reason == "eof" or (duration > 0 and time_pos >= duration * 0.85) then
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
