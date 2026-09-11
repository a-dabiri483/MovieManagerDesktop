-- =================================================================
-- 🧲 Magnetic Screen Snap & 8-Directional Window Resizing for MPV Player
-- Supports: 4 Corners, 4 Edges, Native Frame & Desktop Magnetic Snap
-- Ensures 100% compatibility with OSC buttons (Close, Min, Pin, Pause)
-- =================================================================

local ffi = require("ffi")
local mp = require("mp")

pcall(ffi.cdef, [[
    typedef void* HWND;
    typedef void* HMONITOR;
    typedef int BOOL;
    typedef unsigned int UINT;
    typedef unsigned long DWORD;
    typedef struct tagRECT {
        long left;
        long top;
        long right;
        long bottom;
    } RECT;
    typedef struct tagMONITORINFO {
        DWORD cbSize;
        RECT rcMonitor;
        RECT rcWork;
        DWORD dwFlags;
    } MONITORINFO;

    HWND FindWindowA(const char* lpClassName, const char* lpWindowName);
    HWND GetForegroundWindow();
    BOOL GetWindowRect(HWND hWnd, RECT* lpRect);
    BOOL SetWindowPos(HWND hWnd, HWND hWndInsertAfter, int X, int Y, int cx, int cy, UINT uFlags);
    BOOL SystemParametersInfoA(UINT uiAction, UINT uiParam, void* pvParam, UINT fWinIni);
    BOOL IsZoomed(HWND hWnd);
    BOOL IsIconic(HWND hWnd);
    long GetWindowLongA(HWND hWnd, int nIndex);
    long SetWindowLongA(HWND hWnd, int nIndex, long dwNewLong);
    short GetAsyncKeyState(int vKey);
    HMONITOR MonitorFromWindow(HWND hwnd, DWORD dwFlags);
    BOOL GetMonitorInfoA(HMONITOR hMonitor, MONITORINFO* lpmi);
]])

local user32 = ffi.load("user32")

-- Win32 Constants
local SPI_GETWORKAREA = 0x0030
local SWP_NOZORDER = 0x0004
local SWP_NOACTIVATE = 0x0010
local SWP_NOMOVE = 0x0002
local SWP_NOSIZE = 0x0001
local SWP_FRAMECHANGED = 0x0020
local GWL_STYLE = -16
local WS_THICKFRAME = 0x00040000
local VK_LBUTTON = 0x01
local MONITOR_DEFAULTTONEAREST = 2

-- Magnetic thresholds
local EDGE_SNAP_THRESHOLD = 35    -- Pixels for magnetic snap to top, bottom, left, right edges
local CORNER_SNAP_THRESHOLD = 50  -- Generous magnetic field for snapping into any of the 4 corners

local function get_mpv_hwnd()
    local hwnd = user32.FindWindowA("mpv", nil)
    if hwnd == nil or hwnd == ffi.null then
        hwnd = user32.GetForegroundWindow()
    end
    return hwnd
end

-- Ensure WS_THICKFRAME is active and aspect ratio lock is disabled for free edge resizing
local frame_initialized = false
local function init_window_frame()
    if frame_initialized then return end
    pcall(function()
        mp.set_property_bool("keepaspect-window", false)
    end)
    local hwnd = get_mpv_hwnd()
    if hwnd ~= nil and hwnd ~= ffi.null then
        local style = user32.GetWindowLongA(hwnd, GWL_STYLE)
        if style ~= 0 then
            user32.SetWindowLongA(hwnd, GWL_STYLE, bit.bor(style, WS_THICKFRAME))
            user32.SetWindowPos(hwnd, nil, 0, 0, 0, 0, bit.bor(SWP_NOMOVE, SWP_NOSIZE, SWP_NOZORDER, SWP_FRAMECHANGED, SWP_NOACTIVATE))
            frame_initialized = true
        end
    end
end

-- Get exact work area for the monitor where the window currently resides (handles multi-monitor & taskbars)
local function get_work_area(hwnd)
    local workArea = ffi.new("RECT")
    if user32.MonitorFromWindow ~= nil and user32.GetMonitorInfoA ~= nil then
        local hMon = user32.MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST)
        if hMon ~= nil and hMon ~= ffi.null then
            local mi = ffi.new("MONITORINFO")
            mi.cbSize = ffi.sizeof("MONITORINFO")
            if user32.GetMonitorInfoA(hMon, mi) ~= 0 then
                workArea.left = mi.rcWork.left
                workArea.top = mi.rcWork.top
                workArea.right = mi.rcWork.right
                workArea.bottom = mi.rcWork.bottom
                return workArea
            end
        end
    end
    -- Fallback to primary monitor work area
    user32.SystemParametersInfoA(SPI_GETWORKAREA, 0, workArea, 0)
    return workArea
end

-- Magnetic Snap State
local was_dragging = false
local last_x = nil
local last_y = nil

local function apply_magnetic_snap()
    if not frame_initialized then init_window_frame() end
    if mp.get_property_bool("fullscreen", false) then return end

    local hwnd = get_mpv_hwnd()
    if hwnd == nil or hwnd == ffi.null then return end

    if user32.IsIconic(hwnd) ~= 0 or user32.IsZoomed(hwnd) ~= 0 then return end

    local rect = ffi.new("RECT")
    if user32.GetWindowRect(hwnd, rect) == 0 then return end

    local cur_x = rect.left
    local cur_y = rect.top
    local width = rect.right - rect.left
    local height = rect.bottom - rect.top

    -- Check if left mouse button is pressed
    local is_lbutton_down = bit.band(user32.GetAsyncKeyState(VK_LBUTTON), 0x8000) ~= 0
    local is_foreground = (user32.GetForegroundWindow() == hwnd)

    if is_lbutton_down and is_foreground then
        -- User is actively dragging or resizing.
        -- DO NOT interrupt with SetWindowPos here, as Windows' internal SC_MOVE
        -- loop will fight SetWindowPos and cause the window to jerk and bounce back!
        if last_x ~= cur_x or last_y ~= cur_y then
            was_dragging = true
            last_x = cur_x
            last_y = cur_y
        end
        return
    end

    -- If the mouse is UP, and window hasn't moved since last check, nothing to do
    if not was_dragging and last_x == cur_x and last_y == cur_y then
        return
    end

    was_dragging = false
    last_x = cur_x
    last_y = cur_y

    local workArea = get_work_area(hwnd)
    local new_x = cur_x
    local new_y = cur_y
    local snapped = false

    local dist_left = math.abs(cur_x - workArea.left)
    local dist_right = math.abs((cur_x + width) - workArea.right)
    local dist_top = math.abs(cur_y - workArea.top)
    local dist_bottom = math.abs((cur_y + height) - workArea.bottom)

    -- 1. Corner Magnetic Snapping (Higher priority & larger magnetic zone)
    if dist_left <= CORNER_SNAP_THRESHOLD and dist_top <= CORNER_SNAP_THRESHOLD then
        -- Top-Left Corner
        new_x = workArea.left
        new_y = workArea.top
        snapped = true
    elseif dist_right <= CORNER_SNAP_THRESHOLD and dist_top <= CORNER_SNAP_THRESHOLD then
        -- Top-Right Corner
        new_x = workArea.right - width
        new_y = workArea.top
        snapped = true
    elseif dist_left <= CORNER_SNAP_THRESHOLD and dist_bottom <= CORNER_SNAP_THRESHOLD then
        -- Bottom-Left Corner
        new_x = workArea.left
        new_y = workArea.bottom - height
        snapped = true
    elseif dist_right <= CORNER_SNAP_THRESHOLD and dist_bottom <= CORNER_SNAP_THRESHOLD then
        -- Bottom-Right Corner
        new_x = workArea.right - width
        new_y = workArea.bottom - height
        snapped = true
    else
        -- 2. Single Edge Magnetic Snapping
        if dist_left <= EDGE_SNAP_THRESHOLD then
            new_x = workArea.left
            snapped = true
        elseif dist_right <= EDGE_SNAP_THRESHOLD then
            new_x = workArea.right - width
            snapped = true
        end

        if dist_top <= EDGE_SNAP_THRESHOLD then
            new_y = workArea.top
            snapped = true
        elseif dist_bottom <= EDGE_SNAP_THRESHOLD then
            new_y = workArea.bottom - height
            snapped = true
        end
    end

    if snapped and (new_x ~= cur_x or new_y ~= cur_y) then
        user32.SetWindowPos(hwnd, nil, new_x, new_y, 0, 0, bit.bor(SWP_NOZORDER, SWP_NOSIZE, SWP_NOACTIVATE))
        last_x = new_x
        last_y = new_y
    end
end

-- Initialize on load
mp.register_event("file-loaded", init_window_frame)

-- Periodic check for magnetic snap (50ms for instant, smooth lock without lag)
mp.add_periodic_timer(0.05, apply_magnetic_snap)
