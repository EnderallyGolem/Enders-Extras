local CameraSplineTargetTrigger = {
    name = "EndersExtras/CameraSplineTargetTrigger",
    category = "camera",
    nodeLimits = {1, -1},
    placements = {
        {
            name = "normal",
            data = {
                innerRadius = 8.0,
                outerRadius = 24.0,
                innerLerp = 1,
                outerLerp = 1,
                catchupStrength = 0.7,
                requireFlag = "",
                dependOnlyOnX = false,
                dependOnlyOnY = false,
                coverScreen = false,
                oneWay = false,
                killOffscreenHorizontal = false,
                killOffscreenVertical = false,
                nodeSearchRange = 0.0,
                considerCameraOffset = true,
            }
        },
    },
    fieldInformation = {
        innerRadius = { fieldType = "number", minimumValue = 0 },
        outerRadius = { fieldType = "number", minimumValue = 0 },
        innerLerp = { fieldType = "number", minimumValue = 0, maximumValue = 1 },
        outerLerp = { fieldType = "number", minimumValue = 0, maximumValue = 1 },
        catchupStrength = { fieldType = "number", minimumValue = 0, maximumValue = 1 },
        restrictNodeSearch = { fieldType = "number", minimumValue = 0 },
    },
    fieldOrder = {
        "x", "y", "height", "width",
        "innerRadius", "innerLerp", "outerRadius", "outerLerp", "catchupStrength",
        "nodeSearchRange", "requireFlag",
        "dependOnlyOnX", "dependOnlyOnY", "killOffscreenHorizontal", "killOffscreenVertical",
        "considerCameraOffset", "coverScreen", "oneWay"
    },
}

return CameraSplineTargetTrigger