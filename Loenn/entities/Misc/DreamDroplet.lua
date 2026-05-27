local drawableSprite = require("structs.drawable_sprite")
local celesteEnums = require("consts.celeste_enums")
local utils = require("utils")

local DreamDroplet = {}

DreamDroplet.name = "EndersExtras/DreamDroplet"
DreamDroplet.depth = function(room,entity) return entity.Depth or -12000 end
DreamDroplet.borderColor = {0.6, 0.6, 1.0, 0.8}
DreamDroplet.nodeLimits = {0, 1}

function DreamDroplet.fillColor(room, entity)
    return entity.colour
end

DreamDroplet.placements = {
    name = "normal",
    placementType = "point",
    data = {
        width = 32,
        height = 32,
        Depth = -12000,
        colour = "01e8fff4",
        semimajorDistance = 5,
        flipFocals = false,

        respawnTime = 3,
        regainDash = false,
        retainSpeed = false,

        defaultEffect = "jump",
        defaultUpEffect = "wallbounce",
        defaultUpDiagonalEffect = "default_effect",
        upKeyEffect = "wallbounce",
        downKeyEffect = "hyper",
        dashEffect = "redirect_diffdir",

        dashSpeed = 240,
        horizontalVelocityScale = 1.0,
        verticalVelocityScale = 1.0,
        normalisationScale = 0.0,

        nodeMoveTime = 3,
        nodeMoveOffset = 0
    }
}

DreamDroplet.fieldOrder = {
    "x", "y", "width", "height", "Depth", "colour",
    "semimajorDistance", "flipFocals",
    
    "respawnTime",

    "defaultEffect", "defaultUpEffect", "defaultUpDiagonalEffect",
    "upKeyEffect", "downKeyEffect", "dashEffect",

    "dashSpeed", "regainDash", "retainSpeed",
    "horizontalVelocityScale", "verticalVelocityScale", "normalisationScale",

    "nodeMoveTime", "nodeMoveOffset"
}

DreamDroplet.fieldInformation = {
    Depth = { fieldType = "integer"},
    semimajorDistance = { fieldType = "number", minimumValue = 0 },
    respawnTime = { fieldType = "number", minimumValue = -1 },
    nodeMoveTime = { fieldType = "number", minimumValue = 0 },
    nodeMoveOffset = { fieldType = "number", minimumValue = 0, maximumValue = 1 },
    colour = { fieldType = "color", useAlpha = true },
    dashSpeed = { fieldType = "integer"},
    dashEffect = { fieldType = "string", editable = false,
        options = {
            {"None", "none"},
            {"Redirect (Different Direction)", "redirect_diffdir"},
            {"Redirect (Allow Same Direction)", "redirect_allowsamedir"},
        }
    },
    upKeyEffect = { fieldType = "string", editable = false,
        options = {
            {"None", "none"},
            {"Dream Jump", "jump"},
            {"Super", "super"},
            {"Hyper", "hyper"},
            {"Wall Bounce", "wallbounce"},
            {"Default Effect", "default_effect"},
        }
    },
    downKeyEffect = { fieldType = "string", editable = false,
        options = {
            {"None", "none"},
            {"Dream Jump", "jump"},
            {"Super", "super"},
            {"Hyper", "hyper"},
            {"Wall Bounce", "wallbounce"},
            {"Default Effect", "default_effect"},
        }
    },
    defaultUpEffect = { fieldType = "string", editable = false,
        options = {
            {"None", "none"},
            {"Dream Jump", "jump"},
            {"Super", "super"},
            {"Hyper", "hyper"},
            {"Wall Bounce", "wallbounce"},
            {"Default Effect", "default_effect"},
        }
    },
    defaultUpDiagonalEffect = { fieldType = "string", editable = false,
        options = {
            {"None", "none"},
            {"Dream Jump", "jump"},
            {"Super", "super"},
            {"Hyper", "hyper"},
            {"Wall Bounce", "wallbounce"},
            {"Default Effect", "default_effect"},
        }
    },
    defaultEffect = { fieldType = "string", editable = false,
        options = {
            {"None", "none"},
            {"Dream Jump", "jump"},
            {"Super", "super"},
            {"Hyper", "hyper"},
            {"Wall Bounce", "wallbounce"},
        }
    },
}

function DreamDroplet.onRotate(room, entity, direction)
    local oldWidth = entity.width
    entity.width = entity.height
    entity.height = oldWidth
end

function DreamDroplet.onFlip(room, entity, horizontal, vertical)
    entity.flipFocals = not entity.flipFocals
end

return DreamDroplet