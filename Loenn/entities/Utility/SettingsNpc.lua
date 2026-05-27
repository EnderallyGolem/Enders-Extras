local miscFuncs = require('mods').requireFromPlugin("libraries.miscFuncs")
local SettingsNpc = {}

SettingsNpc.name = "EndersExtras/SettingsNpc"
SettingsNpc.depth = 100
SettingsNpc.justification = {0.5, 1.0}
SettingsNpc.placements = {
    name = "normal",
    data = {
        sprite = "EndersExtras/SettingsNpc/settings_npc",
        spriteRate = 3,
        dialogId = "",
        onlyOnce = false,
        endLevel = false,
        flipX = false,
        flipY = false,
        approachWhenTalking = false,
        approachDistance = 16,
        indicatorOffsetX = 0,
        indicatorOffsetY = -14,
        talkRegionScaleX = 0.2,
        talkRegionScaleY = 0.2,

        modSettings = "",
        headerDialog = "EndersExtras_ModSettingsDefault",
        logSettings = false,
    }
}

local function validatorFunc(string)
    local _, colonCount = string:gsub("::","")
    if colonCount == 1 or colonCount == 2 then
        return true
    end
    return false
end

SettingsNpc.fieldInformation = {
    spriteRate = { fieldType = "integer", },
    approachDistance = { fieldType = "integer", },
    indicatorOffsetX = { fieldType = "integer", },
    indicatorOffsetY = { fieldType = "integer", },
    modSettings = { 
        fieldType = "list", elementSeparator = "||",
        elementOptions = { fieldType = "string", validator = validatorFunc }
    },
}
SettingsNpc.fieldOrder = {
    "x", "y", 
    "sprite", "spriteRate",
    "dialogId", "modSettings", "headerDialog",
    "indicatorOffsetX", "indicatorOffsetY", "talkRegionScaleX", "talkRegionScaleY",
    "approachDistance", "approachWhenTalking", "flipX", "flipY",
    "onlyOnce",
    "logSettings",
}
SettingsNpc.ignoredFields = {
    "_name", "_id", "originX", "originY", "endLevel"
}

function SettingsNpc.scale(room, entity)
    local scaleX = entity.flipX and -1 or 1
    local scaleY = entity.flipY and -1 or 1

    return scaleX, scaleY
end

function SettingsNpc.onFlip(room, entity, horizontal, vertical)
    if horizontal then
        entity.flipX = not entity.flipX
    end
    if vertical then
        entity.flipY = not entity.flipY
    end
end

function SettingsNpc.onRotate(room, entity, direction)
    entity.flipX = not entity.flipX
end


function SettingsNpc.texture(room, entity)
    local texture = string.format("characters/%s00", entity.sprite or "")
    return texture
end

return SettingsNpc