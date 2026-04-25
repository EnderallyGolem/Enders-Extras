local drawableNinePatch = require("structs.drawable_nine_patch")
local drawableSprite = require("structs.drawable_sprite")
local utils = require("utils")
local miscFuncs = require('mods').requireFromPlugin("libraries.miscFuncs")
local SoundRippleBell = {}

SoundRippleBell.name = "EndersExtras/SoundRippleBell"
SoundRippleBell.depth = -9000
SoundRippleBell.placements = {
    name = "normal",
    placementType = "point",
    data = {
        type = "medium",
        depth = -9000,
        onlyPlayerRing = false,
        cooldown = 0.8,
        radius = 0.0,
        volumeScale = 1.0,
        pitchScale = 1.0,
        pitchVariation = 0.0,
        flashIntensity = 1,
        distortIntensity = 1,
        clearPreviousRipples = false,
    }
}


SoundRippleBell.fieldInformation = {
    type = { fieldType = "string", editable = false,
        options = {
            {"Small", "small"},
            {"Medium", "medium"},
            {"Large", "large"},
        }
    },
    depth = { fieldType = "integer"},
    volumeScale = { fieldType = "number", minimumValue = 0 },
    pitchScale = { fieldType = "number", minimumValue = 0 },
    pitchVariation = { fieldType = "number", minimumValue = 0, maximumValue = 1 },
    flashIntensity = { fieldType = "number", minimumValue = 0 },
    distortIntensity = { fieldType = "number", minimumValue = 0 },
}

SoundRippleBell.fieldOrder = {
    "x", "y", "depth",
    "type", "cooldown", "radius", "pitchScale", "pitchVariation", "volumeScale",  "flashIntensity", "distortIntensity",
    "onlyPlayerRing", "clearPreviousRipples"
}

function SoundRippleBell.texture(room, entity)
    local textures = {
        small = "objects/EndersExtras/SoundRipple/soundripplebell_bronze",
        medium = "objects/EndersExtras/SoundRipple/soundripplebell_silver",
        large = "objects/EndersExtras/SoundRipple/soundripplebell_gold",
    }
    return textures[entity.options] or textures.medium
end

function SoundRippleBell.sprite(room, entity)
    local textures = {
        small = "objects/EndersExtras/SoundRipple/soundripplebell_bronze",
        medium = "objects/EndersExtras/SoundRipple/soundripplebell_silver",
        large = "objects/EndersExtras/SoundRipple/soundripplebell_gold",
    }
    local texture = textures[entity.type] or textures.medium
    local sprite = drawableSprite.fromTexture(texture, entity)
    return sprite
end

function SoundRippleBell.onFlip(room, entity, horizontal, vertical)
    ScrollOptions(entity)
end

function SoundRippleBell.onRotate(room, entity, direction)
    ScrollOptions(entity)
end

function ScrollOptions(entity)
    if entity.type == "small" then
        entity.type = "medium"
    elseif entity.type == "medium" then
        entity.type = "large"
    elseif entity.type == "large" then
        entity.type = "small"
    end
end


return SoundRippleBell