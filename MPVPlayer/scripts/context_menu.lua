-- =================================================================
-- 🎬 Native Windows Context Menu Helper for MovieManager MPV Player
-- =================================================================

local utils = require("mp.utils")
local msg = require("mp.msg")

local callbacks = {}
local next_cmd_id = 100

local function add_menu_item(menu, text, action, is_checked)
    local cmd_id = tostring(next_cmd_id)
    next_cmd_id = next_cmd_id + 1
    callbacks[cmd_id] = action
    table.insert(menu, {type = "item", id = cmd_id, text = text, checked = is_checked})
end

local function add_submenu(parent_menu, title, child_menu)
    table.insert(parent_menu, {type = "submenu", text = title, items = child_menu})
end

local function add_separator(menu)
    table.insert(menu, {type = "separator"})
end

local function open_sync_dialog()
    local pipe_name = mp.get_property("input-ipc-server") or "mpvsocket"
    local s_del = tostring(mp.get_property_number("sub-delay", 0.0))
    local a_del = tostring(mp.get_property_number("audio-delay", 0.0))
    mp.command_native_async({
        name = "subprocess",
        playback_only = false,
        args = {"MpvMenuHelper.exe", "sync", pipe_name, s_del, a_del}
    })
end

local function open_style_dialog()
    local pipe_name = mp.get_property("input-ipc-server") or "mpvsocket"
    mp.command_native_async({
        name = "subprocess",
        playback_only = false,
        args = {"MpvMenuHelper.exe", "style", pipe_name}
    })
end

local function open_translate_dialog()
    local pipe_name = mp.get_property("input-ipc-server") or "mpvsocket"
    local sub_path = mp.get_property("current-tracks/sub/external-filename") or ""
    local video_path = mp.get_property("path") or ""
    local sid = mp.get_property("sid") or ""
    mp.command_native_async({
        name = "subprocess",
        playback_only = false,
        args = {"MpvMenuHelper.exe", "translate", pipe_name, sub_path, video_path, sid}
    })
end

local function open_pick_subtitle()
    local pipe_name = mp.get_property("input-ipc-server") or "mpvsocket"
    local video_path = mp.get_property("path") or ""
    local video_dir = ""
    if video_path ~= "" then
        local dir, _ = utils.split_path(video_path)
        video_dir = dir
    end

    mp.command_native_async({
        name = "subprocess",
        playback_only = false,
        capture_stdout = true,
        args = {"MpvMenuHelper.exe", "picksub", pipe_name, video_dir}
    }, function(success, result)
        if success and result and result.stdout then
            local selected_path = result.stdout:match("^%s*(.-)%s*$")
            if selected_path and selected_path ~= "" then
                mp.commandv("sub-add", selected_path, "select")
                mp.set_property_bool("sub-visibility", true)
                local _, sub_name = utils.split_path(selected_path)
                mp.osd_message("زیرنویس فعال شد: " .. (sub_name or selected_path), 3)
            end
        end
    end)
end

function show_native_menu()
    callbacks = {}
    next_cmd_id = 100
    local root = {}

    -- ──────────────────────────────────────────
    -- 1. ابزارهای شناور و ثابت (Floating Tools)
    -- ──────────────────────────────────────────
    add_menu_item(root, "⏱ همگام‌سازی صدا و زیرنویس...", open_sync_dialog)
    add_menu_item(root, "🎨 شخصی‌سازی و استایل زیرنویس...", open_style_dialog)
    add_menu_item(root, "🌐 ترجمه هوشمند زیرنویس به فارسی...", open_translate_dialog)
    add_separator(root)

    -- ──────────────────────────────────────────
    -- 2. زیرنویس (Subtitles Menu)
    -- ──────────────────────────────────────────
    local subMenu = {}
    
    local tracks = mp.get_property_native("track-list") or {}
    local current_sid = mp.get_property("sid")
    local sub_count = 0
    for _, track in ipairs(tracks) do
        if track.type == "sub" then
            sub_count = sub_count + 1
            local lang = track.lang or "نامشخص"
            local title = track.title or ("ترک زیرنویس " .. sub_count)
            local label = string.format("[%s] %s", lang:upper(), title)
            local track_id = tostring(track.id)
            local is_active = (current_sid == track_id)
            add_menu_item(subMenu, label, function()
                mp.set_property("sid", track_id)
            end, is_active)
        end
    end
    if sub_count > 0 then
        add_separator(subMenu)
    end

    local sub_vis = mp.get_property_bool("sub-visibility", true)
    add_menu_item(subMenu, "نمایش زیرنویس", function()
        mp.command("cycle sub-visibility")
    end, sub_vis)

    add_menu_item(subMenu, "بارگذاری فایل زیرنویس...", open_pick_subtitle)

    add_separator(subMenu)

    add_menu_item(subMenu, "🎨 استایل و شخصی‌سازی زیرنویس...", open_style_dialog)
    add_menu_item(subMenu, "🌐 ترجمه به فارسی با هوش مصنوعی...", open_translate_dialog)
    add_menu_item(subMenu, "⏱ همگام‌سازی زیرنویس و صدا...", open_sync_dialog)

    add_submenu(root, "زیرنویس", subMenu)

    -- ──────────────────────────────────────────
    -- 3. صدا (Audio Menu)
    -- ──────────────────────────────────────────
    local audioMenu = {}
    local current_aid = mp.get_property("aid")
    local audio_count = 0
    for _, track in ipairs(tracks) do
        if track.type == "audio" then
            audio_count = audio_count + 1
            local lang = track.lang or "نامشخص"
            local title = track.title or ("ترک صدا " .. audio_count)
            local label = string.format("[%s] %s", lang:upper(), title)
            local track_id = tostring(track.id)
            local is_active = (current_aid == track_id)
            add_menu_item(audioMenu, label, function()
                mp.set_property("aid", track_id)
            end, is_active)
        end
    end
    if audio_count > 0 then
        add_separator(audioMenu)
    end

    local is_mute = mp.get_property_bool("mute", false)
    add_menu_item(audioMenu, "حالت بی‌صدا (Mute)", function()
        mp.command("cycle mute")
    end, is_mute)

    add_menu_item(audioMenu, "⏱ تنظیم تاخیر و همگام‌سازی صدا...", open_sync_dialog)

    add_submenu(root, "صدا و دوبله", audioMenu)

    -- ──────────────────────────────────────────
    -- 4. پخش و سرعت (Playback Menu)
    -- ──────────────────────────────────────────
    local playMenu = {}
    local is_paused = mp.get_property_bool("pause", false)
    if is_paused then
        add_menu_item(playMenu, "▶ ادامه پخش", function() mp.command("cycle pause") end)
    else
        add_menu_item(playMenu, "⏸ توقف (Pause)", function() mp.command("cycle pause") end)
    end
    
    add_separator(playMenu)
    
    add_menu_item(playMenu, "پرش ۵ ثانیه جلو", function() mp.command("seek 5 exact") end)
    add_menu_item(playMenu, "پرش ۵ ثانیه عقب", function() mp.command("seek -5 exact") end)
    add_menu_item(playMenu, "پرش ۳۰ ثانیه جلو", function() mp.command("seek 30 exact") end)
    add_menu_item(playMenu, "پرش ۳۰ ثانیه عقب", function() mp.command("seek -30 exact") end)
    
    add_separator(playMenu)
    
    local speedMenu = {}
    local current_speed = mp.get_property_number("speed", 1.0)
    local speeds = {
        { label = "۰.۵x (بسیار آهسته)", val = 0.5 },
        { label = "۰.۷۵x (آهسته)", val = 0.75 },
        { label = "۱.۰x (سرعت عادی)", val = 1.0 },
        { label = "۱.۲۵x", val = 1.25 },
        { label = "۱.۵x", val = 1.5 },
        { label = "۲.۰x (دو برابر)", val = 2.0 },
    }
    for _, s in ipairs(speeds) do
        local is_s_active = math.abs(current_speed - s.val) < 0.05
        add_menu_item(speedMenu, s.label, function()
            mp.set_property("speed", s.val)
        end, is_s_active)
    end
    add_submenu(playMenu, "سرعت پخش", speedMenu)

    add_separator(playMenu)
    add_menu_item(playMenu, "⏭ قسمت بعدی (PageDown)", function() mp.command("playlist-next") end)
    add_menu_item(playMenu, "⏮ قسمت قبلی (PageUp)", function() mp.command("playlist-prev") end)

    add_submenu(root, "کنترل پخش", playMenu)

    -- ──────────────────────────────────────────
    -- 5. تصویر و پنجره (Video Menu)
    -- ──────────────────────────────────────────
    local videoMenu = {}
    local is_fs = mp.get_property_bool("fullscreen", false)
    add_menu_item(videoMenu, "حالت تمام‌صفحه (Enter)", function()
        mp.command("cycle fullscreen")
    end, is_fs)

    local is_ontop = mp.get_property_bool("ontop", false)
    add_menu_item(videoMenu, "همیشه روی سایر پنجره‌ها (On Top)", function()
        mp.command("cycle ontop")
    end, is_ontop)

    local aspectMenu = {}
    local aspect = mp.get_property("video-aspect-override") or "no"
    add_menu_item(aspectMenu, "پیش‌فرض / خودکار", function() mp.set_property("video-aspect-override", "no") end, aspect == "no" or aspect == "-1.000")
    add_menu_item(aspectMenu, "۱۶:۹ (عریض)", function() mp.set_property("video-aspect-override", "16:9") end, aspect == "16:9" or aspect == "1.778")
    add_menu_item(aspectMenu, "۴:۳ (کلاسیک)", function() mp.set_property("video-aspect-override", "4:3") end, aspect == "4:3" or aspect == "1.333")
    add_submenu(videoMenu, "نسبت تصویر (Aspect Ratio)", aspectMenu)

    add_submenu(root, "تصویر و پنجره", videoMenu)

    -- ──────────────────────────────────────────
    -- 6. خروج
    -- ──────────────────────────────────────────
    add_separator(root)
    add_menu_item(root, "❌ بستن پلیر (Esc)", function()
        mp.command("quit")
    end)

    -- ──────────────────────────────────────────
    -- اجرای برنامه کمکی و نمایش منو
    -- ──────────────────────────────────────────
    local json_str = utils.format_json({items = root})
    
    local temp_dir = os.getenv("TEMP") or "/tmp"
    local temp_file = temp_dir .. "/mpv_menu_" .. tostring(mp.get_time()) .. ".json"
    
    local f = io.open(temp_file, "w")
    if not f then
        msg.error("Failed to write menu json")
        return
    end
    
    f:write(json_str)
    f:close()
    
    local helper_exe = "MpvMenuHelper.exe"
    
    mp.command_native_async({
        name = "subprocess",
        playback_only = false,
        args = {helper_exe, temp_file},
        capture_stdout = true,
    }, function(success, result)
        os.remove(temp_file)
        if success and result and result.stdout then
            local selected_id = result.stdout:match("^%s*(.-)%s*$")
            if callbacks[selected_id] then
                callbacks[selected_id]()
            end
        end
    end)
end

mp.register_script_message("show_native_context_menu", show_native_menu)
mp.add_forced_key_binding("MBTN_RIGHT", "open_context_menu", show_native_menu)
mp.add_key_binding("MBTN_RIGHT", "open_context_menu", show_native_menu)
