local SoundRippleSeeker = {}

SoundRippleSeeker.name = "EndersExtras/SoundRippleSeeker"
SoundRippleSeeker.depth = -199
SoundRippleSeeker.nodeLineRenderType = "line"
SoundRippleSeeker.texture = "characters/monsters/predator73"
SoundRippleSeeker.nodeLimits = {1, -1}
SoundRippleSeeker.placements = {
    name = "normal",
    placementType = "point",
    data = {
        hasSpotlight = false,
        cannotDetectTileEntity = true
    }
}

return SoundRippleSeeker