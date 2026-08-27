-- =================================================================
-- 🧲 Magnetic Screen Snap & 8-Directional Window Resizing for MPV Player
-- Supports: 4 Corners, 4 Edges, Native Frame & Desktop Magnetic Snap
-- Ensures 100% compatibility with OSC buttons (Close, Min, Pin, Pause)
-- =================================================================

local ffi = require("ffi")
local mp = require("mp")

pcall(ffi.cdef, [[
    typedef void* HWND;
    typedef int BOOL;
    typedef unsigned int UINT;
    typedef unsigned long DWORD;
    typedef struct tagRECT {
        long left;
        long top;
        long right;
        long bottom;
    } RECT;

    HWND FindWindowA(const char* lpClassName, const char* lpWindowName);
    HWND GetForegroundWindow();
    BOOL GetWindowRect(HWND hWnd, RECT* lpRect);
    BOOL SetWindowPos(HWND hWnd, HWND hWndInsertAfter, int X, int Y, int cx, int cy, UINT uFlags);
    BOOL SystemParametersInfoA(UINT uiAction, UINT uiParam, void* pvParam, UINT fWinIni);
    BOOL IsZoomed(HWND hWnd);
    BOOL IsIconic(HWND hWnd);
    long GetWindowLongA(HWND hWnd, int nIndex);
    long SetWindowLongA(HWND hWnd, int nIndex, long dwNewLong);
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

local SNAP_THRESHOLD = 35 -- distance in pixels for magnetic snap to screen edges and corners

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

-- Magnetic Snap logic: snaps window to screen edges and corners automatically
local last_snapped_x = nil
local last_snapped_y = nil

local function apply_magnetic_snap()
    if not frame_initialized then init_window_frame() end
    if mp.get_property_bool("fullscreen", false) then return end

    local hwnd = get_mpv_hwnd()
    if hwnd == nil or hwnd == ffi.null then return end

    if user32.IsIconic(hwnd) ~= 0 or user32.IsZoomed(hwnd) ~= 0 then return end

    local rect = ffi.new("RECT")
    if user32.GetWindowRect(hwnd, rect) == 0 then return end

    local workArea = ffi.new("RECT")
    if user32.SystemParametersInfoA(SPI_GETWORKAREA, 0, workArea, 0) == 0 then return end

    local width = rect.right - rect.left
    local height = rect.bottom - rect.top
    local cur_x = rect.left
    local cur_y = rect.top

    local new_x = cur_x
    local new_y = cur_y
    local snapped = false

    -- 1. Horizontal snap
    if math.abs(cur_x - workArea.left) <= SNAP_THRESHOLD then
        new_x = workArea.left
        snapped = true
    elseif math.abs((cur_x + width) - workArea.right) <= SNAP_THRESHOLD then
        new_x = workArea.right - width
        snapped = true
    end

    -- 2. Vertical snap
    if math.abs(cur_y - workArea.top) <= SNAP_THRESHOLD then
        new_y = workArea.top
        snapped = true
    elseif math.abs((cur_y + height) - workArea.bottom) <= SNAP_THRESHOLD then
        new_y = workArea.bottom - height
        snapped = true
    end

    -- 3. Corner Magnetic Snapping
    if math.abs(cur_x - workArea.left) <= SNAP_THRESHOLD and math.abs(cur_y - workArea.top) <= SNAP_THRESHOLD then
        new_x = workArea.left
        new_y = workArea.top
        snapped = true
    elseif math.abs((cur_x + width) - workArea.right) <= SNAP_THRESHOLD and math.abs(cur_y - workArea.top) <= SNAP_THRESHOLD then
        new_x = workArea.right - width
        new_y = workArea.top
        snapped = true
    elseif math.abs(cur_x - workArea.left) <= SNAP_THRESHOLD and math.abs((cur_y + height) - workArea.bottom) <= SNAP_THRESHOLD then
        new_x = workArea.left
        new_y = workArea.bottom - height
        snapped = true
    elseif math.abs((cur_x + width) - workArea.right) <= SNAP_THRESHOLD and math.abs((cur_y + height) - workArea.bottom) <= SNAP_THRESHOLD then
        new_x = workArea.right - width
        new_y = workArea.bottom - height
        snapped = true
    end

    if snapped and (new_x ~= cur_x or new_y ~= cur_y) then
        if new_x ~= last_snapped_x or new_y ~= last_snapped_y then
            last_snapped_x = new_x
            last_snapped_y = new_y
            user32.SetWindowPos(hwnd, nil, new_x, new_y, width, height, bit.bor(SWP_NOZORDER, SWP_NOACTIVATE))
        end
    else
        last_snapped_x = nil
        last_snapped_y = nil
    end
end

-- Initialize on load
mp.register_event("file-loaded", init_window_frame)

-- Periodic check for magnetic snap after moving window
mp.add_periodic_timer(0.25, apply_magnetic_snap)
