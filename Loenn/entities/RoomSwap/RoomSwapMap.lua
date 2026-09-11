local miscFuncs = require('mods').requireFromPlugin("libraries.miscFuncs")
local defaultTexture = "objects/EndersExtras/roomSwapMap/lonnicon"

local roomSwapMap = {
    name = "EndersExtras/RoomSwapMap",
    depth = 20,
    -- offset = {0, 0},
    --texture = "objects/EndersExtras/roomSwapMap/lonnicon",
    placements = {
        {
            name = "normal",
            data = {
                gridId = "1",
                
                folderPath = "",
                scale = 1.0,
                mapBackgroundFileName = "background",
                mapCurrentPosFileName = "current",
                mapIconFilePrefix = "icon_",
                floatAmplitude = 0.1,
                animationSpeedMultiplier = 0.1,

                hudLayer = false
            },
        },
    },
    fieldOrder = {
        "x", "y", "editorLayer",
        "gridId",

        "folderPath", "scale", "mapBackgroundFileName", "mapCurrentPosFileName", "mapIconFilePrefix", 
        "floatAmplitude", "animationSpeedMultiplier",

        "hudLayer"
    },
    fieldInformation = {
        folderPath = {fieldType = "path", allowFolders = true, allowFiles = false}
    }
}

function roomSwapMap.depth(room, entity)
    if entity.hudLayer then
        return -999999
    else
        return 20
    end
end

function roomSwapMap.scale(room, entity)
    local scale = entity.scale

    return {scale, scale}
end

function roomSwapMap.texture(room, entity)

    if entity.folderPath == "" 
    then 
        return defaultTexture 
    end

    local iconPath = entity.folderPath .. "/" .. entity.mapBackgroundFileName

    return miscFuncs.trimPath(iconPath, defaultTexture)
end

return roomSwapMap