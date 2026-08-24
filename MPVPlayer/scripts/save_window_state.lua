-- =================================================================
-- 💾 Save & Restore Window Size, Pin (On-Top) and Volume for MovieManager MPV
-- =================================================================

local ffi = require("ffi")
local utils = require("mp.utils")

-- Wrap in pcall to avoid redefinition errors if another script already defined these
pcall(ffi.cdef, [[
    typedef void* HWND;
    typedef int BOOL;
    typedef struct tagRECT {
        long left;
        long top;
        long right;
        long bottom;
    } RECT;

    HWND FindWindowA(const char* lpClassName, const char* lpWindowName);
    HWND GetForegroundWindow();
    BOOL GetWindowRect(HWND hWnd, RECT* lpRect);
    BOOL IsZoomed(HWND hWnd);
    BOOL IsIconic(HWND hWnd);
]])

local user32 = ffi.load("user32")

local function get_mpv_hwnd()
    local hwnd = user32.FindWindowA("mpv", nil)
    if hwnd == nil or hwnd == ffi.null then
        hwnd = user32.GetForegroundWindow()
    end
    return hwnd
end

local function write_file_safe(path, content)
    if not path then return end
    local f = io.open(path, "w")
    if f then
        f:write(content)
        f:close()
    end
end

local function save_window_state()
    local ok, err = pcall(function()
        local is_fullscreen = mp.get_property_bool("fullscreen", false)
        if is_fullscreen then
            return
        end

        local hwnd = get_mpv_hwnd()
        if hwnd == nil or hwnd == ffi.null then
            return
        end

        -- Don't save if minimized or maximized
        if user32.IsIconic(hwnd) ~= 0 or user32.IsZoomed(hwnd) ~= 0 then
            return
        end

        local rect = ffi.new("RECT")
        if user32.GetWindowRect(hwnd, rect) ~= 0 then
            local width = rect.right - rect.left
            local height = rect.bottom - rect.top
            local x = rect.left
            local y = rect.top

            if width > 200 and height > 150 and x >= -100 and y >= -100 then
                local volume = tostring(mp.get_property("volume") or "100")
                local ontop = mp.get_property_bool("ontop", false) and "yes" or "no"

                local content = string.format("geometry=%dx%d+%d+%d\nvolume=%s\nontop=%s\n", width, height, x, y, volume, ontop)

                -- 1. MPV Config Dir
                local conf_path = mp.command_native({"expand-path", "~~/window_state.conf"})
                write_file_safe(conf_path, content)

                -- 2. LocalAppData Permanent Storage (pure lua io.open without os.execute cmd popup)
                local localAppData = os.getenv("LOCALAPPDATA")
                if localAppData then
                    write_file_safe(localAppData .. "\\MovieManagerDesktop\\window_state.conf", content)
                end

                -- 3. Source Folder (so new builds won't overwrite with default)
                local srcDir = "c:\\Users\\ALI\\CascadeProjects\\MovieManagerDesktop\\MPVPlayer\\window_state.conf"
                write_file_safe(srcDir, content)
            end
        end
    end)
    if not ok then
        mp.msg.error("save_window_state error: " .. tostring(err))
    end
end

mp.register_event("shutdown", save_window_state)
mp.add_periodic_timer(5, save_window_state)
mp.observe_property("volume", "number", save_window_state)
mp.observe_property("ontop", "bool", save_window_state)
