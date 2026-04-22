local SoundRippleSeeker = {}

SoundRippleSeeker.name = "EndersExtras/SoundRippleWatchtower"
SoundRippleSeeker.depth = -8500
SoundRippleSeeker.justification = {0.5, 1.0}
SoundRippleSeeker.nodeLineRenderType = "line"
SoundRippleSeeker.texture = "objects/lookout/lookout05"
SoundRippleSeeker.nodeLimits = {0, -1}
SoundRippleSeeker.placements = {
    name = "normal",
    alternativeName = {"Sound Ripple Tower Viewer", "Sound Ripple Lookout", "Sound Ripple Binoculars", "Sound Ripple Watchtower"},
    data = {
        onlyY = false
    }
}

return SoundRippleSeeker