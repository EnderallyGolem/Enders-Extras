using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.EndersExtras.Utils
{
    internal static class Utils_RoomSwap
    {
        // Hooks
        #region Hooks

        private static bool EnabledRoomSwapHooks { get; set; } = false;

        internal static void UpdateEnablingRoomSwapHooks(bool enable)
        {
            if (enable && !EnabledRoomSwapHooks)
            {
                EnabledRoomSwapHooks = true;
                LoadHooks();
            }
            if (!enable && EnabledRoomSwapHooks)
            {
                EnabledRoomSwapHooks = false;
                UnloadHooks();
            }
        }

        private static ILHook? Loadhook_Level_OrigLoadLevel;
        private static void LoadHooks()
        {
            Logger.Log(LogLevel.Info, "EndersExtras/Utils_RoomSwap", $"Enabling Room-Swap Hooks.");

            Everest.Events.Level.OnLoadLevel += Event_LoadLevel;
            On.Celeste.Player.IntroRespawnBegin += Hook_OnPlayerRespawn;
            On.Celeste.Level.CreateEntityId += Hook_OnCreateEntityID;
            MethodInfo ILOrigLoadLevel = typeof(Level).GetMethod("orig_LoadLevel", BindingFlags.Public | BindingFlags.Instance)!;
            Loadhook_Level_OrigLoadLevel = new ILHook(ILOrigLoadLevel, Hook_ILOrigLoadLevel);
        }
        private static void UnloadHooks()
        {
            Logger.Log(LogLevel.Info, "EndersExtras/Utils_RoomSwap", $"Disabling Room-Swap Hooks.");

            Everest.Events.Level.OnLoadLevel -= Event_LoadLevel;
            On.Celeste.Player.IntroRespawnBegin -= Hook_OnPlayerRespawn;
            On.Celeste.Level.CreateEntityId -= Hook_OnCreateEntityID;
            Loadhook_Level_OrigLoadLevel?.Dispose(); Loadhook_Level_OrigLoadLevel = null;
        }

        private static void Event_LoadLevel(Level level, Player.IntroTypes introType, bool isFromLoader)
        {
            DoTheRespawnPositionChangeThingy(level);
        }

        private static void DoTheRespawnPositionChangeThingy(Level level)
        {
            Vector2 respawnOffset = Vector2.Zero;

            //Get random spawnpoint
            Vector2 randomSpawnPos = level.Session.LevelData.Spawns[0];

            //Check all rooms one by one for location of this room's spawnpoint.
            //The spawnpoint pos is shuffled at this point and hence is likely in some random swap room.
            foreach (LevelData levelData in level.Session.MapData.Levels)
            {
                //Logger.Log(LogLevel.Info, "EndersExtras/RoomSwap/TransitionChangeRespawnTrigger", $"Checking if spawnpoint at {randomSpawnPos.X} {randomSpawnPos.Y} are from {levelData.Name}");
                //Logger.Log(LogLevel.Info, "EndersExtras/RoomSwap/TransitionChangeRespawnTrigger", $"level pos: {levelData.Position.X} {levelData.Position.Y}. Size: {levelData.Bounds.Width} {levelData.Bounds.Height}");
                if (levelData.Position.X < randomSpawnPos.X && levelData.Position.Y < randomSpawnPos.Y &&
                    levelData.Position.X + levelData.Bounds.Width > randomSpawnPos.X &&
                    levelData.Position.Y + levelData.Bounds.Height > randomSpawnPos.Y)
                {
                    respawnOffset = level.LevelOffset - levelData.Position;
                    //Logger.Log(LogLevel.Info, "EndersExtras/RoomSwap/TransitionChangeRespawnTrigger", $"Yes! Updating respawnOffset to {respawnOffset.Value.X} {respawnOffset.Value.Y}");
                    break;
                }
            }

            // Change spawnpoint location
            for (int i = 0; i < level.Session.LevelData.Spawns.Count; i++)
            {
                level.Session.LevelData.Spawns[i] += respawnOffset;
            }

            // Same for DefaultSpawn, if it exists.
            Vector2? defaultSpawnPos = level.Session.LevelData.DefaultSpawn;
            if (defaultSpawnPos.HasValue) level.Session.LevelData.DefaultSpawn += respawnOffset;
        }

        private static void Hook_OnPlayerRespawn(On.Celeste.Player.orig_IntroRespawnBegin orig, global::Celeste.Player self)
        {
            //Update the room-swap rooms. This is kind of here as a failsafe,
            //and also otherwise warping with debug mode permanently empty the swap rooms.
            ReupdateAllRoomsBasic();
            orig(self);
        }

        private static EntityID Hook_OnCreateEntityID(On.Celeste.Level.orig_CreateEntityId orig, Level level, LevelData levelData, EntityData entityData)
        {
            EntityID entityId = orig(level, levelData, entityData);
            entityId.Level = GetTemplateRoomFromSwapRoom(entityId.Level);
            return entityId;
        }

        public static void Hook_ILOrigLoadLevel(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            // At EntityID entityId = new EntityID(levelData.Name, id);
            // Replace levelData.Name for swap rooms with the template rooms
            while (cursor.TryGotoNext(MoveType.After,
                    instr => instr.MatchLdloca(out _),
                    instr => instr.MatchLdloc3(),
                    instr => instr.MatchLdfld(typeof(global::Celeste.LevelData), "Name")
            ))
            {
                cursor.EmitDelegate<Func<string, string>>(GetTemplateRoomFromSwapRoom);
            }
        }

        private static String GetTemplateRoomFromSwapRoom(string roomName)
        {
            List<int> roomPos = GetPosFromRoomName(roomName);
            if (roomPos == new List<int>([1, 1])) return roomName; // Null roomPos

            // Search all rooms for a template room which corresponds to this swap room
            foreach (String gridID in EndersExtrasModule.Session.roomSwapOrderList.Keys)
            {
                String roomSwapPrefix = EndersExtrasModule.Session.roomSwapPrefix[gridID];
                int row = roomPos[0]; int col = roomPos[1];

                // Check if {roomSwapPrefix}{row}{column} matches current room name
                String swapRoomName = $"{roomSwapPrefix}{row}{col}";
                if (swapRoomName != roomName) continue;

                // Found match! Return templateRoomName
                String templateRoomName = EndersExtrasModule.Session.roomSwapOrderList[gridID][row - 1][col - 1];
                return templateRoomName;
            }
            return roomName;
        }

        private static void ChangeAllEntityIDsToTemplate(Level level)
        {
            String roomName = level.Session.LevelData.Name;
            List<int> roomPos = GetPosFromRoomName(roomName);

            foreach (String gridID in EndersExtrasModule.Session.roomSwapOrderList.Keys)
            {
                String roomSwapPrefix = EndersExtrasModule.Session.roomSwapPrefix[gridID];

                int row = roomPos[0]; int col = roomPos[1];

                // Check if {roomSwapPrefix}{row}{column} matches current room name
                String swapRoomName = $"{roomSwapPrefix}{row}{col}";
                if (swapRoomName != roomName) continue;

                // Found match! Replace that part with templateRoomName
                String templateRoomName = EndersExtrasModule.Session.roomSwapOrderList[gridID][row - 1][col - 1];

                // Replace for all (non UI, non global) entities in the room
                foreach (Entity entity in level.Entities)
                {
                    if (entity.TagCheck(Tags.Global) || entity.TagCheck(Tags.HUD) || entity.TagCheck(TagsExt.SubHUD)) continue;

                    if (entity is Decal or SolidTiles or BackgroundTiles or Player) continue;

                    entity.SourceId = entity.SourceId with { Level = templateRoomName };
                }

                return;
            }
        }


        #endregion

        // Event Listener for when room modification occurs
        internal static event EventHandler<RoomModificationEventArgs> RoomModificationEvent;
        internal class RoomModificationEventArgs : EventArgs
        {
            public string gridID { get; set; }
            public RoomModificationEventArgs(string gridID)
            {
                this.gridID = gridID;
            }
        }

        // Umm like literally everything else

        internal static void RoomModificationEventTrigger(string gridID)
        {
            RoomModificationEvent?.Invoke(null, new RoomModificationEventArgs(gridID));
        }

        internal static void ReupdateAllRoomsBasic()
        {
            if (Engine.Scene is not Level level)
            {
                return;
            }
            else
            {
                ReupdateAllRooms(level, 0);
            }
        }

        internal static void ReupdateAllRooms(global::Celeste.Level level, int teleportDelayMilisecond)
        {
            foreach (String gridID in EndersExtrasModule.Session.roomSwapOrderList.Keys)
            {
                int roomSwapTotalRow = EndersExtrasModule.Session.roomSwapRow[gridID];
                int roomSwapTotalColumn = EndersExtrasModule.Session.roomSwapColumn[gridID];
                String roomSwapPrefix = EndersExtrasModule.Session.roomSwapPrefix[gridID];
                //String roomTemplatePrefix = EndersExtrasModule.Session.roomTemplatePrefix[gridID];

                for (int row = 1; row <= roomSwapTotalRow; row++)
                {
                    for (int column = 1; column <= roomSwapTotalColumn; column++)
                    {
                        ReplaceRoomAfterReloadEnd(gridID, roomSwapPrefix, row, column, level, teleportDelayMilisecond);
                    }
                }
                // Update current room IDs. Sometimes needed, although usually the orig_LoadRoom handles it.
                ChangeAllEntityIDsToTemplate(level);
                RoomModificationEventTrigger(gridID);
            }
        }

        internal static async void ReplaceRoomAfterReloadEnd(string gridID, String roomSwapPrefix, int row, int column, global::Celeste.Level level, int teleportDelayMilisecond)
        {
            while (EndersExtrasModule.reloadComplete != true)
            {
                await Task.Delay(20);
            }

            //Logger.Log(LogLevel.Info, "EndersExtras/Utils_RoomSwap", $"Replace {EndersExtrasModule.Session.roomSwapOrderList[gridID][row - 1][column - 1]} >> {roomSwapPrefix}{row}{column}");
            ReplaceRoom($"{roomSwapPrefix}{row}{column}", EndersExtrasModule.Session.roomSwapOrderList[gridID][row - 1][column - 1], level, teleportDelayMilisecond);
        }

        static LevelData GetRoomDataFromName(string roomName, Level level)
        {
            foreach (LevelData levelData in level.Session.MapData.Levels)
            {
                if (levelData.Name == roomName) { return levelData; }
            }
            Logger.Log(LogLevel.Warn, "EndersExtras/main/getRoomDataFromName", $"Unable to find room {roomName} - returning current room leveldata instead.");
            return level.Session.LevelData; //returns current room if can't find (this should not happen)
        }

        static void ReplaceRoom(String replaceSwapRoomName, String replaceTemplateRoomName, global::Celeste.Level level, int teleportDelayMilisecond)
        {
            LevelData replaceSwapRoomData = GetRoomDataFromName(replaceSwapRoomName, level);
            LevelData replaceTemplateRoomData = GetRoomDataFromName(replaceTemplateRoomName, level);

            //Logger.Log(LogLevel.Info, "EndersExtras/Utils_RoomSwap", $"Replacing room {replaceSwapRoomName} with the template {replaceTemplateRoomName}");

            // Avoid changing name, position
            replaceSwapRoomData.Entities = replaceTemplateRoomData.Entities;
            replaceSwapRoomData.Dummy = replaceTemplateRoomData.Dummy;
            //replaceSwapRoomData.Space = replaceTemplateRoomData.Space;
            replaceSwapRoomData.Bg = replaceTemplateRoomData.Bg;
            replaceSwapRoomData.BgDecals = replaceTemplateRoomData.BgDecals;

            // Spawns don't have their position set properly, but that's what TransitionRespawnForceSameRoomTrigger is for
            replaceSwapRoomData.Spawns = replaceTemplateRoomData.Spawns;
            replaceSwapRoomData.DefaultSpawn = replaceTemplateRoomData.DefaultSpawn;

            // Tiles can't be copy pasted like this
            // replaceSwapRoomData.BgTiles = replaceTemplateRoomData.BgTiles;
            // replaceSwapRoomData.FgTiles = replaceTemplateRoomData.FgTiles;
            replaceSwapRoomData.ObjTiles = replaceTemplateRoomData.ObjTiles;

            replaceSwapRoomData.Solids = replaceTemplateRoomData.Solids;

            replaceSwapRoomData.FgDecals = replaceTemplateRoomData.FgDecals;
            replaceSwapRoomData.Music = replaceTemplateRoomData.Music;
            replaceSwapRoomData.Strawberries = replaceTemplateRoomData.Strawberries;
            replaceSwapRoomData.Triggers = replaceTemplateRoomData.Triggers;
            replaceSwapRoomData.MusicLayers = replaceTemplateRoomData.MusicLayers;
            replaceSwapRoomData.Music = replaceTemplateRoomData.Music;
            replaceSwapRoomData.MusicProgress = replaceTemplateRoomData.MusicProgress;
            replaceSwapRoomData.MusicWhispers = replaceTemplateRoomData.MusicWhispers;
            replaceSwapRoomData.DelayAltMusic = replaceTemplateRoomData.DelayAltMusic;
            replaceSwapRoomData.AltMusic = replaceTemplateRoomData.AltMusic;
            replaceSwapRoomData.Ambience = replaceTemplateRoomData.Ambience;
            replaceSwapRoomData.AmbienceProgress = replaceTemplateRoomData.AmbienceProgress;
            replaceSwapRoomData.Dark = replaceTemplateRoomData.Dark;
            replaceSwapRoomData.EnforceDashNumber = replaceTemplateRoomData.EnforceDashNumber;
            replaceSwapRoomData.Underwater = replaceTemplateRoomData.Underwater;
            replaceSwapRoomData.WindPattern = replaceTemplateRoomData.WindPattern;
            replaceSwapRoomData.HasGem = replaceTemplateRoomData.HasGem;
            replaceSwapRoomData.HasHeartGem = replaceTemplateRoomData.HasHeartGem;
            replaceSwapRoomData.HasCheckpoint = replaceTemplateRoomData.HasCheckpoint;

            CloneTiles(replaceSwapRoomData, replaceTemplateRoomData, level, teleportDelayMilisecond);
        }

        private static async void CloneTiles(LevelData replaceSwapRoomData, LevelData replaceTemplateRoomData, Level level, int teleportDelayMilisecond)
        {
            level.PauseLock = true;
            // Logger.Log(LogLevel.Info, "EndersExtras/Utils_RoomSwap", $"Template {replaceTemplateRoomData.Name} >> Swap {replaceSwapRoomData.Name}");

            Rectangle swapRoomBounds = replaceSwapRoomData.TileBounds;
            swapRoomBounds.Location -= level.Session.MapData.TileBounds.Location;
            Rectangle templateRoomBounds = replaceTemplateRoomData.TileBounds;
            templateRoomBounds.Location -= level.Session.MapData.TileBounds.Location;

            if (swapRoomBounds.Width != templateRoomBounds.Width || swapRoomBounds.Height != templateRoomBounds.Height)
            {
                Logger.Log(LogLevel.Warn, "EndersExtras/Utils_RoomSwap", $"Template Room {replaceTemplateRoomData.Name} and Swap Room {replaceSwapRoomData.Name} have differing sizes!\nTemplate: {templateRoomBounds.Width} x {templateRoomBounds.Height} tiles, Swap:  {swapRoomBounds.Width} x {swapRoomBounds.Height} tiles.");
            }

            for (int x = 0; x < templateRoomBounds.Width; x++) for (int y = 0; y < templateRoomBounds.Height; y++)
            {
                Point templateRoomPoint = new Point(templateRoomBounds.X + x, templateRoomBounds.Y + y);
                Point swapRoomPoint = new Point(swapRoomBounds.X + x, swapRoomBounds.Y + y);

                level.SolidsData[swapRoomPoint.X, swapRoomPoint.Y] = level.SolidsData[templateRoomPoint.X, templateRoomPoint.Y];
                level.BgData[swapRoomPoint.X, swapRoomPoint.Y] = level.BgData[templateRoomPoint.X, templateRoomPoint.Y];
            }

            Autotiler.Generated genned = GFX.FGAutotiler.Generate(level.SolidsData, swapRoomBounds.X, swapRoomBounds.Y, templateRoomBounds.Width, templateRoomBounds.Height, forceSolid: false, '0', new Autotiler.Behaviour {
                EdgesExtend = true,
                EdgesIgnoreOutOfLevel = false,
                PaddingIgnoreOutOfLevel = false
            });
            Autotiler.Generated gennedBG = GFX.BGAutotiler.Generate(level.BgData, swapRoomBounds.X, swapRoomBounds.Y, templateRoomBounds.Width, templateRoomBounds.Height, forceSolid: false, '0', new Autotiler.Behaviour {
                EdgesExtend = true,
                EdgesIgnoreOutOfLevel = false,
                PaddingIgnoreOutOfLevel = false
            });
            int swapDelayMilisecond = teleportDelayMilisecond == 0 ? 0 : teleportDelayMilisecond + 150;
            await Task.Delay(swapDelayMilisecond); // So you don't see the swap before you teleport
            for (int x = 0; x < templateRoomBounds.Width; x++) for (int y = 0; y < templateRoomBounds.Height; y++)
            {
                Point swapRoomPoint = new Point(swapRoomBounds.X + x, swapRoomBounds.Y + y);
                Point templateRoomPoint = new Point(templateRoomBounds.X + x, templateRoomBounds.Y + y);

                level.SolidTiles.Tiles.Tiles[swapRoomPoint.X, swapRoomPoint.Y] = genned.TileGrid.Tiles[x, y];
                level.BgTiles.Tiles.Tiles[swapRoomPoint.X, swapRoomPoint.Y] = gennedBG.TileGrid.Tiles[x, y];

                level.SolidTiles.AnimatedTiles.tiles[swapRoomPoint.X, swapRoomPoint.Y] = level.SolidTiles.AnimatedTiles.tiles[templateRoomPoint.X, templateRoomPoint.Y];
                level.SolidTiles.Grid[swapRoomPoint.X, swapRoomPoint.Y] = level.SolidTiles.Grid[templateRoomPoint.X, templateRoomPoint.Y];
            }
            level.PauseLock = false;
        }

        internal static async void TemporarilyDisableTrigger(int millisecondDelay, string gridID)
        {
            EndersExtrasModule.Session.allowTriggerEffect[gridID] = false;
            await Task.Delay(millisecondDelay);
            EndersExtrasModule.Session.allowTriggerEffect[gridID] = true;
        }

        internal static bool ModifyRooms(String modifyType, bool isSilent, Player? player, Level level, String gridID, int teleportDelayMilisecond = 0, int teleportDisableMilisecond = 200, bool flashEffect = false)
        {
            bool succeedModify = false;

            //player is NULLable! player should only be checked inside the not-silent box
            LevelData currentRoomData = level.Session.LevelData;
            String currentRoomName = currentRoomData.Name;

            int roomSwapTotalRow = EndersExtrasModule.Session.roomSwapRow[gridID];
            int roomSwapTotalColumn = EndersExtrasModule.Session.roomSwapColumn[gridID];
            String roomSwapPrefix = EndersExtrasModule.Session.roomSwapPrefix[gridID];
            String roomTemplatePrefix = EndersExtrasModule.Session.roomTemplatePrefix[gridID];

            string? currentTemplateRoomName = GetTemplateRoomFromSwapRoom(currentRoomName);

            if (EndersExtrasModule.Session.allowTriggerEffect[gridID])
            {
                Logger.Log(LogLevel.Info, "EndersExtras/Utils_RoomSwap", $"Modifying Room! Type: {modifyType}. Triggered from {currentRoomName}. ({roomSwapTotalRow}x{roomSwapTotalColumn})");
                TemporarilyDisableTrigger(teleportDisableMilisecond + (int)(EndersExtrasModule.Session.roomTransitionTime[gridID] * 1000 + teleportDelayMilisecond), gridID);

                if (EndersExtrasModule.Session.roomSwapOrderList.ContainsKey(gridID)) //Don't run this if first load
                {
                    level.Session.SetFlag(GetTransitionFlagName(), false); //Remove flag
                }


                switch (modifyType)
                {
                    case "Reset":
                        {
                            List<List<string>>? initial = null;
                            if (EndersExtrasModule.Session.roomSwapOrderList.TryGetValue(gridID, out List<List<string>> value))
                            {
                                initial = new List<List<string>>(Utils_General.DeepCopyJson(value));
                            }


                            EndersExtrasModule.Session.roomSwapOrderList[gridID] = [];
                            for (int row = 1; row <= roomSwapTotalRow; row++)
                            {
                                List<string> roomRow = [];
                                for (int column = 1; column <= roomSwapTotalColumn; column++)
                                {
                                    roomRow.Add($"{roomTemplatePrefix}{row}{column}");
                                }
                                EndersExtrasModule.Session.roomSwapOrderList[gridID].Add(roomRow);
                            }

                            if (!Utils_General.Are2LayerListsEqual(initial, EndersExtrasModule.Session.roomSwapOrderList[gridID]))
                            {
                                UpdateRooms();
                            }

                            //If reset is triggered while in the swap zone, do le warp
                            if (currentRoomName.StartsWith(roomSwapPrefix))
                            {
                                TeleportToRoom(getSwapRoomFromTemplateRoom(currentTemplateRoomName), player, level);
                            }
                        }
                        break;

                    case "CurrentRowLeft":
                        {
                            int roomRow = GetPosFromRoomName(currentRoomName)[0];

                            if (roomRow < 0)
                            {
                                // Room is not in grid. Why is the box here...
                                Logger.Log(LogLevel.Warn, "EndersExtras/Utils_RoomSwap", $"Modification of CurrentRowLeft ran, but current room {currentRoomName} is not in grid!");
                                break;
                            }

                            EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 1].Add(EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 1][0]);
                            EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 1].RemoveAt(0);
                            UpdateRooms();
                            TeleportToRoom(getSwapRoomFromTemplateRoom(currentTemplateRoomName), player, level);
                        }
                        break;

                    case "CurrentRowLeft_PreventWarp":
                        {
                            int roomCol = GetPosFromRoomName(currentRoomName)[1];
                            int roomRow = GetPosFromRoomName(currentRoomName)[0];

                            if (roomCol < 0 || roomRow < 0)
                            {
                                // Room is not in grid. Why is the box here...
                                Logger.Log(LogLevel.Warn, "EndersExtras/Utils_RoomSwap", $"Modification of CurrentRowLeft_PreventWarp ran, but current room {currentRoomName} is not in grid!");
                                break;
                            }

                            //Only continue if roomCol is not leftmost room
                            if (roomCol != 1)
                            {
                                EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 1].Add(EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 1][0]);
                                EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 1].RemoveAt(0);
                                UpdateRooms();
                                TeleportToRoom(getSwapRoomFromTemplateRoom(currentTemplateRoomName), player, level);
                            }
                        }
                        break;

                    case "CurrentRowRight":
                        {
                            int roomRow = GetPosFromRoomName(currentRoomName)[0];

                            if (roomRow < 0)
                            {
                                // Room is not in grid. Why is the box here...
                                Logger.Log(LogLevel.Warn, "EndersExtras/Utils_RoomSwap", $"Modification of CurrentRowRight ran, but current room {currentRoomName} is not in grid!");
                                break;
                            }

                            EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 1].Insert(0, EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 1][roomSwapTotalColumn - 1]);
                            EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 1].RemoveAt(roomSwapTotalColumn);
                            UpdateRooms();
                            TeleportToRoom(getSwapRoomFromTemplateRoom(currentTemplateRoomName), player, level);
                        }
                        break;

                    case "CurrentRowRight_PreventWarp":
                        {
                            int roomCol = GetPosFromRoomName(currentRoomName)[1];
                            int roomRow = GetPosFromRoomName(currentRoomName)[0];

                            if (roomCol < 0 || roomRow < 0)
                            {
                                // Room is not in grid. Why is the box here...
                                Logger.Log(LogLevel.Warn, "EndersExtras/Utils_RoomSwap", $"Modification of CurrentRowRight_PreventWarp ran, but current room {currentRoomName} is not in grid!");
                                break;
                            }

                            //Only continue if roomCol is not rightmost room
                            if (roomCol != roomSwapTotalColumn)
                            {
                                EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 1].Insert(0, EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 1][roomSwapTotalColumn - 1]);
                                EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 1].RemoveAt(roomSwapTotalColumn);
                                UpdateRooms();
                                TeleportToRoom(getSwapRoomFromTemplateRoom(currentTemplateRoomName), player, level);
                            }
                        }
                        break;

                    case "CurrentColumnUp":
                        {
                            int roomCol = GetPosFromRoomName(currentRoomName)[1];
                            int roomRow = GetPosFromRoomName(currentRoomName)[0];

                            if (roomCol < 0 || roomRow < 0)
                            {
                                // Room is not in grid. Why is the box here...
                                Logger.Log(LogLevel.Warn, "EndersExtras/Utils_RoomSwap", $"Modification of CurrentColumnUp ran, but current room {currentRoomName} is not in grid!");
                                break;
                            }

                            String topRoomName = EndersExtrasModule.Session.roomSwapOrderList[gridID][0][roomCol - 1];
                            for (int row = 1; row <= roomSwapTotalRow - 1; row++)
                            {
                                //Move each room up
                                EndersExtrasModule.Session.roomSwapOrderList[gridID][row - 1][roomCol - 1] = EndersExtrasModule.Session.roomSwapOrderList[gridID][row][roomCol - 1];
                            }
                            //Copy over top room to the bottom
                            EndersExtrasModule.Session.roomSwapOrderList[gridID][roomSwapTotalRow - 1][roomCol - 1] = topRoomName;
                            UpdateRooms();
                            TeleportToRoom(getSwapRoomFromTemplateRoom(currentTemplateRoomName), player, level);
                        }
                        break;

                    case "CurrentColumnUp_PreventWarp":
                        {
                            int roomCol = GetPosFromRoomName(currentRoomName)[1];
                            int roomRow = GetPosFromRoomName(currentRoomName)[0];

                            if (roomCol < 0 || roomRow < 0)
                            {
                                // Room is not in grid. Why is the box here...
                                Logger.Log(LogLevel.Warn, "EndersExtras/Utils_RoomSwap", $"Modification of CurrentColumnUp_PreventWarp ran, but current room {currentRoomName} is not in grid!");
                                break;
                            }

                            //Only continue if roomRow is not topmost room
                            if (roomRow != 1)
                            {
                                String topRoomName = EndersExtrasModule.Session.roomSwapOrderList[gridID][0][roomCol - 1];
                                for (int row = 1; row <= roomSwapTotalRow - 1; row++)
                                {
                                    //Move each room up
                                    EndersExtrasModule.Session.roomSwapOrderList[gridID][row - 1][roomCol - 1] = EndersExtrasModule.Session.roomSwapOrderList[gridID][row][roomCol - 1];
                                }
                                //Copy over top room to the bottom
                                EndersExtrasModule.Session.roomSwapOrderList[gridID][roomSwapTotalRow - 1][roomCol - 1] = topRoomName;
                                UpdateRooms();
                                TeleportToRoom(getSwapRoomFromTemplateRoom(currentTemplateRoomName), player, level);
                            }
                        }
                        break;

                    case "CurrentColumnDown":
                        {
                            int roomCol = GetPosFromRoomName(currentRoomName)[1];
                            int roomRow = GetPosFromRoomName(currentRoomName)[0];

                            if (roomCol < 0 || roomRow < 0)
                            {
                                // Room is not in grid. Why is the box here...
                                Logger.Log(LogLevel.Warn, "EndersExtras/Utils_RoomSwap", $"Modification of CurrentColumnDown ran, but current room {currentRoomName} is not in grid!");
                                break;
                            }

                            String bottomRoomName = EndersExtrasModule.Session.roomSwapOrderList[gridID][roomSwapTotalRow - 1][roomCol - 1];
                            for (int row = roomSwapTotalRow; row > 1; row--)
                            {
                                //Move each room down
                                EndersExtrasModule.Session.roomSwapOrderList[gridID][row - 1][roomCol - 1] = EndersExtrasModule.Session.roomSwapOrderList[gridID][row - 2][roomCol - 1];
                            }
                            //Copy over bottom room to the top
                            EndersExtrasModule.Session.roomSwapOrderList[gridID][0][roomCol - 1] = bottomRoomName;
                            UpdateRooms();
                            TeleportToRoom(getSwapRoomFromTemplateRoom(currentTemplateRoomName), player, level);
                        }
                        break;

                    case "CurrentColumnDown_PreventWarp":
                        {
                            int roomCol = GetPosFromRoomName(currentRoomName)[1];
                            int roomRow = GetPosFromRoomName(currentRoomName)[0];

                            if (roomCol < 0 || roomRow < 0)
                            {
                                // Room is not in grid. Why is the box here...
                                Logger.Log(LogLevel.Warn, "EndersExtras/Utils_RoomSwap", $"Modification of CurrentColumnDown_PreventWarp ran, but current room {currentRoomName} is not in grid!");
                                break;
                            }

                            //Only continue if roomRow is not bottommost room
                            if (roomRow != roomSwapTotalRow)
                            {
                                String bottomRoomName = EndersExtrasModule.Session.roomSwapOrderList[gridID][roomSwapTotalRow - 1][roomCol - 1];
                                for (int row = roomSwapTotalRow; row > 1; row--)
                                {
                                    //Move each room down
                                    EndersExtrasModule.Session.roomSwapOrderList[gridID][row - 1][roomCol - 1] = EndersExtrasModule.Session.roomSwapOrderList[gridID][row - 2][roomCol - 1];
                                }
                                //Copy over bottom room to the top
                                EndersExtrasModule.Session.roomSwapOrderList[gridID][0][roomCol - 1] = bottomRoomName;
                                UpdateRooms();
                                TeleportToRoom(getSwapRoomFromTemplateRoom(currentTemplateRoomName), player, level);
                            }
                        }
                        break;

                    case "SwapLeftRight":
                        {
                            int roomCol = GetPosFromRoomName(currentRoomName)[1];
                            int roomRow = GetPosFromRoomName(currentRoomName)[0];

                            if (roomCol < 0 || roomRow < 0) 
                            {
                                // Room is not in grid. Why is the box here...
                                Logger.Log(LogLevel.Warn, "EndersExtras/Utils_RoomSwap", $"Modification of SwapLeftRight ran, but current room {currentRoomName} is not in grid!");
                                break;
                            }

                            //Only continue if not leftmost or rightmost
                            if (roomCol != roomSwapTotalColumn && roomCol != 1)
                            {
                                (EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 1][roomCol - 2], EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 1][roomCol]) = (EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 1][roomCol], EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 1][roomCol - 2]);
                                UpdateRooms();
                                //teleportToRoom(getSwapRoomFromTemplateRoom(currentTemplateRoomName), player, level);
                            }
                        }
                        break;

                    case "SwapUpDown":
                        {
                            int roomCol = GetPosFromRoomName(currentRoomName)[1];
                            int roomRow = GetPosFromRoomName(currentRoomName)[0];

                            if (roomCol < 0 || roomRow < 0)
                            {
                                // Room is not in grid. Why is the box here...
                                Logger.Log(LogLevel.Warn, "EndersExtras/Utils_RoomSwap", $"Modification of SwapUpDown ran, but current room {currentRoomName} is not in grid!");
                                break;
                            }

                            //Only continue if not topmost or bottommost
                            if (roomRow != roomSwapTotalRow && roomRow != 1)
                            {
                                (EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 2][roomCol - 1], EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow][roomCol - 1]) = (EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow][roomCol - 1], EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 2][roomCol - 1]);
                                UpdateRooms();
                                //teleportToRoom(getSwapRoomFromTemplateRoom(currentTemplateRoomName), player, level);
                            }
                        }
                        break;

                    //set_11_12_21_22
                    case { } s when s.StartsWith("Set_"):
                        {
                            string oldTemplateRoomAtThisPos = "";
                            int roomCol = 0; int roomRow = 0;
                            if (currentTemplateRoomName != null)
                            {
                                roomCol = GetPosFromRoomName(currentRoomName)[1];
                                roomRow = GetPosFromRoomName(currentRoomName)[0];
                                // Logger.Log(LogLevel.Info, "EndersExtras/Utils_RoomSwap", $"------------- room name {currentRoomName}. col row {roomCol} {roomRow} and gridID {gridID}");
                                oldTemplateRoomAtThisPos = EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 1][roomCol - 1];
                            }

                            string[] splittedArr = s.Split("_");
                            int row = 1;
                            int column = 1;

                            List<List<string>> initial = null;
                            if (EndersExtrasModule.Session.roomSwapOrderList.TryGetValue(gridID, out List<List<string>> value))
                            {
                                initial = new List<List<string>>(Utils_General.DeepCopyJson(value));
                            }

                            for (int i = 1; i < splittedArr.Length; i++)
                            {

                                List<int> roomPos = GetPosFromRoomName(splittedArr[i]);

                                // Set i-th item in splittedArr to row/col
                                if (roomPos[0] == -1 || roomPos[1] == -1)
                                {
                                    // Same as the previous one
                                    EndersExtrasModule.Session.roomSwapOrderList[gridID][row - 1][column - 1] = initial[row - 1][column - 1];
                                }
                                else
                                {
                                    // Dependent on setted roomPos
                                    EndersExtrasModule.Session.roomSwapOrderList[gridID][row - 1][column - 1] = $"{roomTemplatePrefix}{roomPos[0]}{roomPos[1]}";
                                }

                                // Change row/column index.
                                // If overshot/undershot, it still shouldn't break
                                column++;
                                if (column > roomSwapTotalColumn)
                                {
                                    column = 1;
                                    row++;
                                }
                                if (row > roomSwapTotalRow) { break; }
                            }
                            if (!Utils_General.Are2LayerListsEqual(initial, EndersExtrasModule.Session.roomSwapOrderList[gridID]))
                            {

                                UpdateRooms();

                                // Teleport the player if it is triggered in a room that gets changed from setting
                                // First, check if the player is even in the grid
                                if (oldTemplateRoomAtThisPos == "")
                                {
                                    // Player is not in a room in the grid. Stop checking.
                                }
                                else
                                {
                                    // There can be multiple of the same template room. First check if the current room is the same
                                    string newTemplateRoomAtThisPos = EndersExtrasModule.Session.roomSwapOrderList[gridID][roomRow - 1][roomCol - 1];

                                    //Logger.Log(LogLevel.Info, "EndersExtras/Utils_RoomSwap", $"current {oldTemplateRoomAtThisPos} new {newTemplateRoomAtThisPos}");
                                    if (oldTemplateRoomAtThisPos != newTemplateRoomAtThisPos)
                                    {
                                        // Only teleport if the template room at the same position is different
                                        TeleportToRoom(getSwapRoomFromTemplateRoom(currentTemplateRoomName), player, level);
                                    }
                                }
                            }
                        }
                        break;

                    case "None":
                        UpdateRooms();
                        break;
                }
                level.Session.SetFlag(GetTransitionFlagName(), true); //Set flag
            }

            void UpdateRooms()
            {
                for (int row = 1; row <= roomSwapTotalRow; row++)
                {
                    for (int column = 1; column <= roomSwapTotalColumn; column++)
                    {
                        ReplaceRoom($"{roomSwapPrefix}{row}{column}", EndersExtrasModule.Session.roomSwapOrderList[gridID][row - 1][column - 1], level, teleportDelayMilisecond);
                    }
                }
                //Logger.Log(LogLevel.Info, "EndersExtras/Utils_RoomSwap", "Updating rooms...");
                SwapEffects();
            }

            async void TeleportToRoom(String teleportToRoomName, Player player, Level level)
            {
                LevelData currentRoomData = level.Session.LevelData;
                Vector2 currentRoomPos = currentRoomData.Position;

                if (currentRoomData.Name == teleportToRoomName)
                {
                    //If same room, do nothing
                    EndersExtrasModule.Session.allowTriggerEffect[gridID] = true; //Well actually we can set this to true immediately
                }
                else
                {
                    LevelData toRoomData = GetRoomDataFromName(teleportToRoomName, level);
                    Vector2 toRoomPos = toRoomData.Position;

                    await Task.Delay(teleportDelayMilisecond);

                    Vector2 playerOriginalPos = new(player.Position.X, player.Position.Y);

                    level.NextTransitionDuration = EndersExtrasModule.Session.roomTransitionTime[gridID];
                    Vector2 transitionOffset = toRoomPos - currentRoomPos;
                    Vector2 transitionDirection = transitionOffset.SafeNormalize();

                    player.Position += transitionOffset;
                    level.TransitionTo(toRoomData, transitionDirection);

                    //Occasionally the transition is jank and undoes the position change.
                    //This is here to unjank the jank
                    if (Math.Abs(playerOriginalPos.X - player.Position.X) <= 5 && Math.Abs(playerOriginalPos.Y - player.Position.Y) <= 5)
                    {
                        //If player coordinates barely change, teleport again...
                        player.Position += transitionOffset;
                    }

                    player.ResetSpriteNextFrame(default); // Hopefully fixes a bug where the player sometimes turns invisible after warp

                    // Move followers along with player
                    for (int index = 0; index < player.Leader.PastPoints.Count; index++)
                    {
                        player.Leader.PastPoints[index] += transitionOffset;
                    }
                    foreach (var follower in player.Leader.Followers)
                    {
                        if (follower != null)
                        {
                            follower.Entity.Position += transitionOffset;
                        }
                    }
                    Logger.Log(LogLevel.Info, "EndersExtras/Utils_RoomSwap", $"Teleporting from {currentRoomData.Name} >> {teleportToRoomName}. Pos change: ({playerOriginalPos.X} {playerOriginalPos.Y} => {player.Position.X} {player.Position.Y}) - change by ({(toRoomPos - currentRoomPos).X} {(toRoomPos - currentRoomPos).Y}), Transition direction: ({transitionDirection.X} {transitionDirection.Y})");
                }
            }

            String getSwapRoomFromTemplateRoom(String templateRoomName)
            {
                List<List<String>> roomList = EndersExtrasModule.Session.roomSwapOrderList[gridID];
                for (int row = 1; row <= roomSwapTotalRow; row++)
                {
                    for (int column = 1; column <= roomSwapTotalColumn; column++)
                    {
                        if (templateRoomName == roomList[row - 1][column - 1])
                        {
                            //Found match at {row}{colu} - the swap room is {prefix}{row}{col}
                            return $"{roomSwapPrefix}{row}{column}";
                        }
                    }
                }
                Logger.Log(LogLevel.Info, "EndersExtras/Utils_RoomSwap", $"getSwapRoomFromTemplateRoom - Unable to find {templateRoomName} - returning current room leveldata instead.");
                return currentRoomName; //This shouldn't happen...
            }

            String? GetTemplateRoomFromSwapRoom(String swapRoomName)
            {
                if (swapRoomName.StartsWith(roomSwapPrefix))
                {
                    List<List<String>> roomList = EndersExtrasModule.Session.roomSwapOrderList[gridID];
                    int len = swapRoomName.Length;
                    int rowIndex = swapRoomName[len - 2] - '0';
                    rowIndex += -1;
                    int colIndex = swapRoomName[len - 1] - '0';
                    colIndex += -1;
                    String templateRoomName = roomList[rowIndex][colIndex];
                    return templateRoomName;
                }
                //This means the current room is not a swap room
                //If the template room can't be found it means modifyRoom was triggered from outside the swap grid
                //If the swap effect doesn't depend on the current room this will run with no issues! (Null check for Set)
                return null;
            }

            void SwapEffects()
            {
                RoomModificationEventTrigger(gridID);

                if (flashEffect)
                {
                    level.Flash(Color.White, drawPlayerOver: true);
                }
                if (!isSilent && player is not null)
                {
                    level.Shake();

                    if (EndersExtrasModule.Session.activateSoundEvent1[gridID] != "")
                    {
                        Audio.Play(EndersExtrasModule.Session.activateSoundEvent1[gridID], player.Position);
                    }
                    if (EndersExtrasModule.Session.activateSoundEvent2[gridID] != "")
                    {
                        Audio.Play(EndersExtrasModule.Session.activateSoundEvent2[gridID], player.Position);
                    }
                }
                succeedModify = true;
            }

            String GetTransitionFlagName()
            {
                String flagName = roomSwapPrefix;
                for (int row = 1; row <= roomSwapTotalRow; row++)
                {
                    for (int column = 1; column <= roomSwapTotalColumn; column++)
                    {
                        String roomNameAtPos = EndersExtrasModule.Session.roomSwapOrderList[gridID][row - 1][column - 1];
                        List<int> roomPos = GetPosFromRoomName(roomNameAtPos);
                        flagName += $"_{roomPos[0]}{roomPos[1]}";
                    }
                }
                //Logger.Log(LogLevel.Info, "EndersExtras/Utils_RoomSwap", $"getTransitionFlagName - Obtained flag name for {roomSwapPrefix} to be {flagName}");
                return flagName;
            }
            return succeedModify;
        }

        /// <summary>
        /// Returns the last 2 digits of the room name... or any string lol. Returns [-1, -1] if last 2 characters aren't digits.
        /// </summary>
        /// <param name="roomName"></param>
        /// <returns></returns>
        internal static List<int> GetPosFromRoomName(String roomName)
        {
            int len = roomName.Length;
            int row;

            if (char.IsDigit(roomName[len - 1]) && char.IsDigit(roomName[len - 2]))
            {
                row = roomName[len - 2] - '0';
                var col = roomName[len - 1] - '0';
                return [row, col];
            }
            else
            {
                return [-1, -1];
            }
        }
    }
}
