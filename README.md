# Ender's Extras 
[Source code](https://github.com/EnderallyGolem/Enders-Extras)
[Gamebanana page](---)

# Features



### Individual Entities
	
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
	
	
	
	
### Gimmicks


###### Room-Swap
- Create a grid of rooms that can swap positions with each other (baring some limitations: no collectables and FG/BG tiles).
- You can check out how they work in my [Crossroads Contest map](https://www.youtube.com/watch?v=xB6RLAKZC0g).
	- Setup a grid with Room-Swap Controller (ensure it is loaded before entering the grid)
	- Create template rooms (with names matching the controller) and actual rooms of the same size. Actual rooms are empty, template rooms have the actual room.
	- Add Room-Swap Respawn Force Same Room Triggers in each template room.
	- Use Updating Change Respawn Triggers instead of the regular trigger.
	- Change room order using Room-Swap Breaker Box or Room-Swap Modify Room Trigger.
	- Create a map with Room-Swap Map. Implement map upgrades with Room-Swap Map Upgrade.

###### Cassette Entity/Triggers
- Cassette Beat Gates
    - Blocks that move along nodes in accordance to cassette beats
    - Can be dependent on bar progress or full track progress, as well as flags
    - Option to move entities/triggers/decals within it instead
- Cassette Manager Trigger
    - Set varying tempo speeds (speed multiplies at predefined beats, either set or multiply existing)
    - Change the current beat
    - Can be different depending on entering/exiting trigger, flags, or if within beat range
- Both of these have support for Quantum Mechanic's wonky cassettes.

###### Death Handler

These require Ender's Blender as a dependency. They affect respawns in various ways.
It's recommended to use this with Seemless Respawns using Blender's Gameplay Tweaks Override Trigger.

**NOTE: THESE ARE EXPERIMENTAL, AND FUNCTIONALITY MAY CHANGE IN THE FUTURE!**

- Allows for respawn points to change, as well as add a death-bypass component that makes an entity not reset upon respawn.
- Includes "Full Resets", which reset positions, even entities with death-bypass. Falling off-screen counts as a full reset.
- Manual Resets (clicking retry), which are full-resets that further reset the player's state
- The death-bypass component can be given to the player, which
	- makes "dying" not result in a respawn
	- gives coyote from dying if you were dashing (or in a booster), in a feather, or were bird-flung when dying
	- Full-resets do not remove the player's death-bypass. The player instantly respawns at a full-reset spawnpoint with all momentum.
	
Entities included:
- Respawn Point
	- An entity-version and visible respawn point, allowing for regular and full-reset spawn points.
	- Can be attached and moved
- Throwable Respawn Point
	- Respawn Point but a theo
- Respawn Marker
	- Marks the currently activated respawn point. Can show an arrow towards it if it is offscreen.
	
- Change Respawn Region
	- A visible change respawn trigger which constantly updates the respawn point it selects.
	- Can be set to select only Full-Reset spawnpoints
	- Can kill you upon entering (useful to combine with changing the Full-Reset spawnpoint, to trigger a Full-Reset)
- Bypass Zone
	- Region that changes the Death-Bypass state of entities that pass through it.
	- Can be set to affect all entities, or all entities except players.
- Reload Bypass Trigger
	- Allows giving entities inside Death-Bypass upon loading the room


####### Sound Ripples
- Sound Ripple Bell creates a sound wave that gives nearby entities an outline that can be seen even in darkness.
- The effect lasts for a second, but only shows the entity at the time when it is hit
- 3 sizes of the Sound Ripple Bell (with different sound ranges)
- Sound Ripple Seeker can be used to integrate together with the bells. They're regular seekers that can't detect the
player if they're within a Tile Entity, and they are not detected by a Sound Ripple Bell.
- Sound Ripple Watchtower can be used to view rooms in the dark (normal watchtower that constantly creates sound waves)