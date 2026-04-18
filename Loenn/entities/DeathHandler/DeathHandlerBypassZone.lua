local drawableNinePatch = require("structs.drawable_nine_patch")
local drawableSprite = require("structs.drawable_sprite")
local utils = require("utils")
local miscFuncs = require('mods').requireFromPlugin("libraries.miscFuncs")

local defaultTexture_Activate = "Graphics/Atlases/Gameplay/objects/EndersExtras/DeathHandlerBypassZone/stainedglass_activate"
local defaultTexture_Deactivate = "Graphics/Atlases/Gameplay/objects/EndersExtras/DeathHandlerBypassZone/stainedglass_deactivate"
local defaultTexture_Toggle = "Graphics/Atlases/Gameplay/objects/EndersExtras/DeathHandlerBypassZone/stainedglass_toggle"
local defaultTexture_None = "Graphics/Atlases/Gameplay/objects/EndersExtras/DeathHandlerBypassZone/stainedglass_none"

local defaultBorderTexture_Player = "Graphics/Atlases/Gameplay/objects/EndersExtras/DeathHandlerBypassZone/stainedglass_border_player"
local defaultBorderTexture_Entity = "Graphics/Atlases/Gameplay/objects/EndersExtras/DeathHandlerBypassZone/stainedglass_border_entity"

local DeathHandlerBypassZone = {}

DeathHandlerBypassZone.name = "EndersExtras/DeathHandlerBypassZone"
DeathHandlerBypassZone.depth = 9500
DeathHandlerBypassZone.canResize = {true, true}
DeathHandlerBypassZone.justification = {0, 0}
DeathHandlerBypassZone.minimumSize = {24, 24}
DeathHandlerBypassZone.placements = {
    associatedMods = {"EndersExtras", "EndersBlender"},
    name = "normal",
    data = {
        width = 24,
        height = 24,

        glassActivateTexture = defaultTexture_Activate,
        glassDeactivateTexture = defaultTexture_Deactivate,
        glassToggleTexture = defaultTexture_Toggle,
        glassNoneTexture = defaultTexture_None,

        borderPlayerTexture = defaultBorderTexture_Player,
        borderEntityTexture = defaultBorderTexture_Entity,

        effect = "Activate",
        altEffect = "Deactivate",
        altFlag = "",
        bypassFlag = "",

        attachable = true,
        affectPlayer = true,
        visible = true,

    }
}

DeathHandlerBypassZone.fieldInformation = {
    glassActivateTexture = {fieldType = "path", allowFolders = false, allowFiles = true},
    glassDeactivateTexture = {fieldType = "path", allowFolders = false, allowFiles = true},
    glassToggleTexture = {fieldType = "path", allowFolders = false, allowFiles = true},
    glassNoneTexture = {fieldType = "path", allowFolders = false, allowFiles = true},
    borderPlayerTexture = {fieldType = "path", allowFolders = false, allowFiles = true},
    borderEntityTexture = {fieldType = "path", allowFolders = false, allowFiles = true},
    effect = { fieldType = "string", editable = false,
        options = {
            {"Activate", "Activate"},
            {"Deactivate", "Deactivate"},
            {"Toggle", "Toggle"},
            {"None", "None"},
        }
    },
    altEffect = { fieldType = "string", editable = false,
        options = {
            {"Activate", "Activate"},
            {"Deactivate", "Deactivate"},
            {"Toggle", "Toggle"},
            {"None", "None"},
        }
    }
}

DeathHandlerBypassZone.fieldOrder = {
    "x", "y", "editorLayer",
    "glassActivateTexture", "glassDeactivateTexture", "glassToggleTexture", "glassNoneTexture", "borderPlayerTexture", "borderEntityTexture",
    "effect", "altEffect", "altFlag", "bypassFlag",
    "attachable", "affectPlayer", "visible"
}
DeathHandlerBypassZone.ignoredFields = {"_name", "_id", "originX", "originY", "height", "width"}

local ninePatchOptions = {
    mode = "fill",
    borderMode = "repeat",
    fillMode = "repeat"
}

-- Helper to get the correct glass texture based on effect
local function getGlassTexture(entity)
    local effect = entity.effect or "None"
    
    if effect == "Activate" then
        return entity.glassActivateTexture or defaultTexture_Activate
    elseif effect == "Deactivate" then
        return entity.glassDeactivateTexture or defaultTexture_Deactivate
    elseif effect == "Toggle" then
        return entity.glassToggleTexture or defaultTexture_Toggle
    else
        return entity.glassNoneTexture or defaultTexture_None
    end
end

-- Helper to get the correct border texture
local function getBorderTexture(entity)
    if entity.affectPlayer then
        return entity.borderPlayerTexture or defaultBorderTexture_Player
    else
        return entity.borderEntityTexture or defaultBorderTexture_Entity
    end
end

-- Helper to tile a sprite and clip the edges
local function getTessellatedSprites(texturePath, x, y, width, height)
    local sprites = {}
    texturePath = miscFuncs.trimPath(texturePath)
    
    -- We need a temp sprite to get the texture's native dimensions from metadata
    local tempSprite = drawableSprite.fromTexture(texturePath, {x = 0, y = 0})
    if not tempSprite or not tempSprite.meta then return sprites end

    local texWidth = tempSprite.meta.width
    local texHeight = tempSprite.meta.height

    for ty = 0, height - 1, texHeight do
        for tx = 0, width - 1, texWidth do
            -- Calculate the size of the piece we want to draw
            -- If the remaining zone space is smaller than the texture, we crop
            local drawWidth = math.min(texWidth, width - tx)
            local drawHeight = math.min(texHeight, height - ty)

            local tile = drawableSprite.fromTexture(texturePath, {
                x = x + tx,
                y = y + ty
            })

            tile:setJustification(0, 0)
            
            -- This is the function used in drawable_nine_patch.lua
            -- It crops the texture source to the specified rectangle (x, y, w, h)
            -- relative to the texture's top-left corner.
            tile:useRelativeQuad(0, 0, drawWidth, drawHeight)

            table.insert(sprites, tile)
        end
    end

    return sprites
end

function DeathHandlerBypassZone.sprite(room, entity)
    local x, y = entity.x or 0, entity.y or 0
    local width, height = entity.width or 24, entity.height or 24
    local sprites = {}

    -- 1. Get Tiled Glass (Bottom Layer)
    local glassPath = miscFuncs.trimPath(getGlassTexture(entity))
    local glassSprites = getTessellatedSprites(glassPath, x, y, width, height)
    
    for _, s in ipairs(glassSprites) do
        table.insert(sprites, s)
    end

    -- 2. Get Border (Top Layer)
    -- Borders generally work better as 9-slices because they need to stretch, 
    -- but we'll use the entity pathing you established.
    local borderPath = miscFuncs.trimPath(getBorderTexture(entity))
    local borderNinePatch = drawableNinePatch.fromTexture(borderPath, ninePatchOptions, x, y, width, height)

    table.insert(sprites, borderNinePatch)

    return sprites
end

function DeathHandlerBypassZone.onRotate(room, entity, direction)
    local oldWidth = entity.width
    entity.width = entity.height
    entity.height = oldWidth
end

return DeathHandlerBypassZone