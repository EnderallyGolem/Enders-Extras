local utils = require("utils")

local FlagInvisibleBarrier = {
    name = "EndersExtras/FlagInvisibleBarrier",
    fillColor = {0.4, 0.4, 0.4, 0.8},
    borderColor = {0.0, 0.0, 0.0, 0.0},
    placements = {
        {
            name = "normal",
            data = {
                width = 8,
                height = 8,
                requireFlag = "",
                disableLeft = false,
                disableRight = false,
                disableAbove = false,
                disableBelow = false,
                disablePlayerInside = true,
                disablePermanently = false,
                enablePermanently = false,
            }
        }
    }
}

FlagInvisibleBarrier.fieldOrder = {
    "x", "y", "width", "height",
    "requireFlag", "disableLeft", "disableRight", "disableAbove", "disableBelow",
    "disablePlayerInside", "disablePermanently", "enablePermanently"
}


return FlagInvisibleBarrier