local mp = require 'mp'
local utils = require 'mp.utils'

local config_dir = mp.command_native({"expand-path", "~~/"})
local geom_file = utils.join_path(config_dir, "last_geometry.txt")

local function restore_geom()
    local f = io.open(geom_file, "r")
    if f then
        local geom = f:read("*all")
        f:close()
        if geom and geom ~= "" then
            mp.set_property("geometry", geom)
        end
    end
end

local function save_geom()
    local w = mp.get_property("osd-width")
    local h = mp.get_property("osd-height")
    if w and h and w ~= "0" and h ~= "0" then
        local geom = w .. "x" .. h
        local f = io.open(geom_file, "w")
        if f then
            f:write(geom)
            f:close()
        end
    end
end

mp.register_event("file-loaded", restore_geom)
mp.register_event("shutdown", save_geom)
