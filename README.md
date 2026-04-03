# Ender's Extras 
[Source code](---)
[Gamebanana page](---)

# Features



### Death Handler

These require Ender's Blender as a dependency. They affect respawns in various ways.
It's recommended to use this with Seemless Respawns using Blender's Gameplay Tweaks Override Trigger.

###### ...



### Misc Entities
	
###### Tile Entity
- Foreground / Background tile entity that allows customising: 
    - Entity depth
    - Connections to the same/different tile entities or the edge of the room, 
    - Whether/what direction it renders off-screen in (Note: done via an invisible tileset)
    - If the seed depends on the relative position in the room
	- If it is collidable
- Option to be breakable

###### Conditional Bird Tutorial
- Tutorial bird which flies in when certain conditions are met:
	- Certain time in room / part of room (total or at once)
	- Certain number of deaths in room / part of room
	- Flag enabled (either as a seperate condition, or required for the above 2 conditions to increment)
	- If on screen

###### Misc
- Connectable Outline
	- Can be attached, and can be toggled by flag.
	
- Incremental Flag Trigger
	- Increments a counter when you reach the trigger with a specific value, which then sets a flag
	
- Flag Killbox

- Temple Gate (Death Count)
	- Opens and closes the gate depending on the death count in the map or room.
	- With Death Handler, has extra options regarding full resets and manual retrys.