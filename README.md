# Ender's Extras 
[Source code](https://github.com/EnderallyGolem/Enders-Extras)
[Gamebanana page](---)

# Features



### Individual Entities / Triggers
	

###### Utility
- Connectable Outline
	- Visual outline.
	- Can be attached, and can be toggled by flag.
	
- Tile Entity
	- Foreground / Background tile entity that allows customising: 
		- Entity depth
		- Connections to the same/different tile entities or the edge of the room, 
		- Whether/what direction it renders off-screen in (Note: done via an invisible tileset)
		- If the seed depends on the relative position in the room
		- If it is collidable
	- Option to be breakable
	
- Flag Killbox
	- Flag dependent and adjustable height dependent killbox.
	
- Flag Invisible Barrier
	- Flag and player position dependent activation
	
- Conditional Bird Tutorial
	- Tutorial bird which flies in when certain conditions are met:
		- Certain time in room / part of room (total or at once)
		- Certain number of deaths in room / part of room
		- Flag enabled (either as a seperate condition, or required for the above 2 conditions to increment)
		- If on screen	

- Mod Settings NPC
	- An NPC that opens the mod setting menu, with only the specified options being displayed.
	- An additional header can be added for each setting.
	
- Incremental Flag Trigger
	- Increments a counter when you reach the trigger with a specific value, which then sets a flag <flagname><num> and removes the previous number.
	
- Camera Spline Target Trigger
	- Causes the camera to target the closest point on a spline made using nodes.
	- Can be set to scroll in one direction and/or avoid skipping parts of the spline.
	- Can be set to kill the player on leaving the screen horizontally or/and downwards.
	- Lerping can be set to be based on the distance from the nearest part of the spline.
	
	
###### Gameplay
	
- Temple Gate (Death Count)
	- Opens and closes the gate depending on the death count in the map or room.
	- With Death Handler, has extra options regarding full resets and manual retrys.

- Dream Droplet
	- An elliptical bubble that the player can stand inside, and can dream-dash by dashing into
	- Can be dashed out of, dream jumped, wall bounced, hypered or supered out from the droplet when dream dashing (configurable)
	- Can be steered while dream-dashing like a superdash (configurable)
	
### Gimmicks


###### Room-Swap
- Create a grid of rooms that can swap positions with each other. (This is done by copying template rooms into the playable swap rooms.)
- You can check out how they work in my [Crossroads Contest map](https://www.youtube.com/watch?v=xB6RLAKZC0g).
	- Setup a grid with Room-Swap Controller (ensure it is loaded before entering the grid)
	- Create template rooms (with names matching the controller) and actual rooms of the same size. Actual rooms are empty, template rooms have the actual room.
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
Respawn Bypass and Full Resets require Ender's Blender as a dependency. They affect respawns in various ways.
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
- Sound Ripple Bell creates a sound ripple that gives nearby entities an outline that can be seen even in darkness.
- The effect lasts for about a second, and only shows the entity at the time when it is hit.
- 3 sizes of the Sound Ripple Bell (with different sound ranges)
- Sound Ripple Seeker can be used to integrate together with the bells. They're regular seekers that can't detect the
player if they're within a Tile Entity, and they are not detected by a Sound Ripple Bell.
- Sound Ripple Watchtower can be used to view rooms in the dark (normal watchtower that constantly creates sound ripples)




### Misc Features

####### Chapter Select Overrides
- On the map select screen, the map name can be overridden, and the chapter number can be removed, centering the map name.
	- The map name shown is overridden with the dialog endersextras_mapname_(map dialog id).
	- The chapter number can be removed with the dialog endersextras_removechapternum_(map dialog id). This can be set to anything.
	- This lets you change (or remove) the map name without affecting how it appears in other places, such as the file select.
	- Example:
		EndersExtras_1=Ender's Extras
		EndersExtras_1_EndersExtrasTestMap=Test Rack
		poem_EndersExtras_1_EndersExtrasTestMap_A=Insert Poem Here
		endersextras_mapname_EndersExtras_1_EndersExtrasTestMap=Basemen- i mean Test Rack
		endersextras_removechapternum_EndersExtras_1_EndersExtrasTestMap=This removes the chapter number and centers the text. Any text can go here!
		
		
		

Changelog:

### 1.1.0:
- Utilities:
	- New entities: Mod Settings NPC, Flag Invisible Barrier, Dream Droplet
	- Tile Entity: 
		- Added Disable Flag option. Toggles whether if the tile entity is visible, collidable, and occludes light.
	- Camera Spline Trigger:
		- The camera now properly with extended camera dynamics
		- With assist mode and Kill Offscreen Vertical, the player now bounces off the bottom of the screen

- Death Handler:
	- Fixed crash with newer blender versions
	- Attempted to fixed bug that prevents some entities from working the first time you load a room with entities, 
	  after leaving and reentering the map
	- Respawn Point, Throwable Respawn Point, Change Respawn Region and Respawn Marker can be used without Ender's Blender as a dependency, as long as Full Reset isn't used.
	- Modified Throwable Respawn Point's hitbox to match Theo Crystals in width. So uhh it doesn't fall through the ground when throwing it next to a wall.
	
- Room-Swap:
	- Room-Swap Respawn Force Same Room Triggers have been removed - spawnpoints are automatically updated when using Room-Swap.
	- Foreground and Background Tiles are now copied during swaps.
	- Collectables/Locks/Stuff dependent on EntityID now works properly with swaps. Probably. I hope.
	- Readded HUD Layer toggle for Room-Swap Maps. I don't know why it disappeared.
	- Fixed the Requested texture that does not exist warning appearing in logs, even when there are no missing textures.
	- Fixed wrong depth for Room-Swap maps and map upgrades in loenn

### 1.0.1:
- Sound Ripples: Fixed seeker bell detection of player persisting past screen transitions