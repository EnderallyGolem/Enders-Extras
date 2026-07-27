local drawableSprite = require("structs.drawable_sprite")
local celesteEnums = require("consts.celeste_enums")
local utils = require("utils")
local drawing = require("utils.drawing")

local DreamDroplet = {}

DreamDroplet.name = "EndersExtras/DreamDroplet"
DreamDroplet.depth = function(room,entity) return entity.Depth or -12000 end
--DreamDroplet.borderColor = {0.6, 0.6, 1.0, 0.8}
DreamDroplet.nodeLimits = {0, 1}
DreamDroplet.nodeLineRenderType = "line"

local function focalDistanceSum(posx, posy, focal1x, focal1y, focal2x, focal2y)
    local focal1dist = math.sqrt( (posx - focal1x)^2 + (posy - focal1y)^2 )
    local focal2dist = math.sqrt( (posx - focal2x)^2 + (posy - focal2y)^2 )
    return focal1dist + focal2dist
end

local function getFocalPos(entity) 
    local focal1x, focal1y, focal2x, focal2y
    if entity.flipFocals then
        focal1x, focal1y = entity.x+entity.width-4,  entity.y+4
        focal2x, focal2y = entity.x+4,               entity.y+entity.height-4
    else
        focal1x, focal1y = entity.x+4,               entity.y+4
        focal2x, focal2y = entity.x+entity.width-4,  entity.y+entity.height-4
    end
    return focal1x, focal1y, focal2x, focal2y
end

-- Reminder - Convert semimajorDistance to PIXELS and not tiles!!!
local function isInside(posx, posy, focal1x, focal1y, focal2x, focal2y, semimajorDistance)
    return focalDistanceSum(posx, posy, focal1x, focal1y, focal2x, focal2y) < semimajorDistance*2
end

local function getEllipseOutlinePos(entity, focal1x, focal1y, focal2x, focal2y)
    -- Locate point on semicircle directly above/below midpoint
    local penx, peny = 0.5*(focal1x+focal2x), 0.5*(focal1y+focal2y)

    local effectiveSemiMajorDist = entity.semimajorDistance*8 -- tiles > pos
    local focalDist = math.sqrt( (focal1x-focal2x)^2 + (focal1y-focal2y)^2 )/2
    if (effectiveSemiMajorDist < focalDist + 0.1) then
        effectiveSemiMajorDist = focalDist + 0.1;
    end

    while isInside(penx, peny, focal1x, focal1y, focal2x, focal2y, effectiveSemiMajorDist) do
        peny = peny - 1
    end
    peny = peny + 1 -- pen should be at the top of the rectangle

    local origPenX, origPenY = penx, peny

    local searchDirX, searchDirY = 0, -1

    local pointsList = {}

    -- Search searchDir, then searchDir perp. If neither, rotate both by 90 deg
    for i = 0, 8*effectiveSemiMajorDist, 1 do
        -- Reach back orig pos
        if (penx == origPenX and peny == origPenY and i > 1) then
            break
        end

        local searchDir2X, searchDir2Y = -searchDirY, searchDirX
        pointsList[#pointsList+1] = penx
        pointsList[#pointsList+1] = peny

        if isInside(penx + searchDirX, peny + searchDirY, focal1x, focal1y, focal2x, focal2y, effectiveSemiMajorDist) then
            penx = penx + searchDirX
            peny = peny + searchDirY
        elseif isInside(penx + searchDir2X, peny + searchDir2Y, focal1x, focal1y, focal2x, focal2y, effectiveSemiMajorDist) then
            penx = penx + searchDir2X
            peny = peny + searchDir2Y
        else
            searchDirX = searchDir2X
            searchDirY = searchDir2Y
        end
    end
    return pointsList
end


function DreamDroplet.draw(room, entity, viewport)

    local focal1x, focal1y, focal2x, focal2y = getFocalPos(entity)
    local pointsList = getEllipseOutlinePos(entity, focal1x, focal1y, focal2x, focal2y)

    drawing.callKeepOriginalColor(function()
        local x, y = entity.x or 0, entity.y or 0
        local width, height = entity.width, entity.height

        -- Semicircle
        love.graphics.setColor(utils.getColor(entity.colour))

        local oldSize = love.graphics.getPointSize()
        love.graphics.setPointSize(2)
        love.graphics.points(pointsList)
        love.graphics.setPointSize(oldSize)

        -- Rectangle
        love.graphics.rectangle("line", x, y, width, height)

        -- Focals
        if entity.flipFocals then
            love.graphics.rectangle("fill", x+width-8, y, 8, 8)
            love.graphics.rectangle("fill", x, y+height-8, 8, 8)
        else
            love.graphics.rectangle("fill", x, y, 8, 8)
            love.graphics.rectangle("fill", x+width-8, y+height-8, 8, 8)
        end
    end)
end


function DreamDroplet.fillColor(room, entity)
    return entity.colour
end

DreamDroplet.placements = {
    name = "normal",
    alternativeName = {"altname"},
    placementType = "point",
    data = {
        width = 8,
        height = 8,
        Depth = -12000,
        colour = "b9fafa80",
        rainbowIntensity = 1,
        semimajorDistance = 3.5,
        flipFocals = false,
        burstOnExit = true,

        respawnTime = 3,
        regainDash = "always",
        retainSpeed = "not_dash",

        defaultEffect = "jump",
        defaultUpEffect = "wallbounce",
        defaultUpDiagonalEffect = "default_effect",
        upKeyEffect = "default_effect",
        downKeyEffect = "hyper",
        dashEffect = "dash_burst",
        directionRedirectIntensity = 0.0,

        dashSpeed = 240.0,
        horizontalVelocityScale = 1.0,
        verticalVelocityScale = 1.0,
        wallbounceVelocityScale = 2.0,

        nodeMoveTime = 5,
        nodeMoveOffset = 0,
        nodeEase = "SineInOut",
        nodeMoveOneWay = false,

        wobble = true,
        gainDashInside = true,
    }
}

DreamDroplet.fieldOrder = {
    "x", "y", "width", "height", "Depth", "colour", "rainbowIntensity",
    "semimajorDistance", "flipFocals", "burstOnExit",
    
    "respawnTime",

    "defaultEffect", "defaultUpEffect", "defaultUpDiagonalEffect",
    "upKeyEffect", "downKeyEffect", "dashEffect", "directionRedirectIntensity",

    "dashSpeed", "regainDash", "retainSpeed",
    "horizontalVelocityScale", "verticalVelocityScale", "wallbounceVelocityScale",

    "nodeMoveTime", "nodeMoveOffset", "nodeEase", "nodeMoveOneWay", "wobble"
}

local easeTypes = {
    "Linear", "SineIn", "SineOut", "SineInOut", "QuadIn", "QuadOut", "QuadInOut", "CubeIn", "CubeOut", "CubeInOut", "QuintIn", "QuintOut", "QuintInOut", "BackIn", "BackOut", "BackInOut", "ExpoIn", "ExpoOut", "ExpoInOut", "BigBackIn", "BigBackOut", "BigBackInOut", "ElasticIn", "ElasticOut", "ElasticInOut", "BounceIn", "BounceOut", "BounceInOut"
}

DreamDroplet.fieldInformation = {
    Depth = { fieldType = "integer"},
    rainbowIntensity = { fieldType = "number", minimumValue = 0 },
    semimajorDistance = { fieldType = "number", minimumValue = 0 },
    respawnTime = { fieldType = "number", minimumValue = -1 },
    nodeMoveTime = { fieldType = "number", minimumValue = 0 },
    nodeMoveOffset = { fieldType = "number", minimumValue = 0, maximumValue = 1 },
    colour = { fieldType = "color", useAlpha = true },
    regainDash = { fieldType = "string", editable = false,
        options = {
            {"Always", "always"},
            {"If Not Dashed", "not_dash"},
            {"Never", "never"},
        }
    },
    retainSpeed = { fieldType = "string", editable = false,
        options = {
            {"Always", "always"},
            {"If Not Dash Tech", "not_dash"},
            {"Never", "never"},
        }
    },
    dashEffect = { fieldType = "string", editable = false,
        options = {
            {"None", "none"},
            {"Dash (Burst)", "dash_burst"},
            {"Redirect", "dash_redirect"},
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
    nodeEase = {
        options = easeTypes,
        editable = false
    }
}

local ceil_to_nearest = function (n, mul)
    return math.ceil(n / mul) * mul
end

function DreamDroplet.onRotate(room, entity, direction)
    if direction == 1 then
        -- Set to above 2a (rounded to nearest 0.5)
        local focal1x, focal1y, focal2x, focal2y = getFocalPos(entity)
        local focalDist = math.sqrt( (focal1x-focal2x)^2 + (focal1y-focal2y)^2 )/2
        if (entity.semimajorDistance*8 < focalDist + 0.1) then
            entity.semimajorDistance = (focalDist + 0.1)/8;
            entity.semimajorDistance = ceil_to_nearest(entity.semimajorDistance, 0.5)
        else
            -- Add 0.5
            entity.semimajorDistance = entity.semimajorDistance + 0.5
        end
    else
        -- Subtract 0.5
        entity.semimajorDistance = entity.semimajorDistance - 0.5
        -- Prevent negative
        if (entity.semimajorDistance < 0) then
            entity.semimajorDistance = 0
        end
    end
    -- local oldWidth = entity.width
    -- entity.width = entity.height
    -- entity.height = oldWidth
end

function DreamDroplet.onFlip(room, entity, horizontal, vertical)
    entity.flipFocals = not entity.flipFocals
end

return DreamDroplet