using Celeste.Mod.EndersExtras.Entities.DeathHandler;
using Celeste.Mod.EndersExtras.Integration;
using Celeste.Mod.EndHelper;
using Celeste.Mod.EndHelper.Utils;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.EndersExtras.Utils
{
    internal static class Utils_DeathHandlerEntities
    {
        internal static bool EnabledDeathHandler { get; private set; } = false;
        internal static void EnableDeathHandler()
        {
            if (!EnabledDeathHandler)
            {
                EnabledDeathHandler = true;
                LoadHooks();
            }
        }
        internal static void DisableHooks()
        {
            if (EnabledDeathHandler) UnloadHooks();
        }


        private static ILHook Loadhook_Level_OrigTransitionRoutine;
        private static void LoadHooks()
        {
            Everest.Events.Player.OnSpawn += OnPlayerSpawnFunc;
            On.Celeste.Session.GetSpawnPoint += Hook_SessionGetSpawnPoint;
            On.Celeste.Level.TransitionRoutine += Hook_TransitionRoutine;

            MethodInfo ILTransitionCoroutine = typeof(Level).GetMethod("orig_TransitionRoutine", BindingFlags.NonPublic | BindingFlags.Instance).GetStateMachineTarget();
            Loadhook_Level_OrigTransitionRoutine = new ILHook(ILTransitionCoroutine, Hook_IL_OrigTransitionRoutine);
        }
        private static void UnloadHooks()
        {
            Everest.Events.Player.OnSpawn -= OnPlayerSpawnFunc;
            On.Celeste.Session.GetSpawnPoint -= Hook_SessionGetSpawnPoint;
            On.Celeste.Level.TransitionRoutine -= Hook_TransitionRoutine;

            Loadhook_Level_OrigTransitionRoutine?.Dispose(); Loadhook_Level_OrigTransitionRoutine = null;
        }

        // Hooks!!!Q1q!Q1q1
        private static void OnPlayerSpawnFunc(Player player)
        {
            // Spawn facing same direction as DeathHandlerRespawnPoint or DeathHandlerRespawnMarker.
            // Marker is last in priority, doesn't work that well when full resetting
            DeathHandlerRespawnPoint respawnPoint = player.CollideFirst<DeathHandlerRespawnPoint>();
            DeathHandlerThrowableRespawnPoint throwableRespawnPoint = player.CollideFirst<DeathHandlerThrowableRespawnPoint>();
            DeathHandlerRespawnMarker respawnMarker = player.CollideFirst<DeathHandlerRespawnMarker>();

            Facings? updateFacings = null;
            if (respawnPoint != null) updateFacings = respawnPoint.faceLeft ? Facings.Left : Facings.Right;
            else if (throwableRespawnPoint != null) updateFacings = throwableRespawnPoint.faceLeft ? Facings.Left : Facings.Right;
            else if (respawnMarker != null) updateFacings = respawnMarker.faceLeft ? Facings.Left : Facings.Right;

            if (updateFacings != null) player.Facing = updateFacings.Value;
        }

        private static Vector2 Hook_SessionGetSpawnPoint(On.Celeste.Session.orig_GetSpawnPoint orig, global::Celeste.Session self, Vector2 from)
        {
            if (Engine.Scene is Level level &&
                (level.Tracker.GetEntity<DeathHandlerRespawnPoint>() is not null || level.Tracker.GetEntity<DeathHandlerThrowableRespawnPoint>() is not null))
            {
                // In case other mods use this function, don't just replace orig directly
                // So do this roundabout thing where we add extra points, check, then remove the extra points
                List<Vector2> deathHandlerSpawnPoints = [];
                foreach (DeathHandlerRespawnPoint deathHandlerSpawnPointEntity in level.Tracker.GetEntities<DeathHandlerRespawnPoint>())
                {
                    if (deathHandlerSpawnPointEntity.disabled == false && deathHandlerSpawnPointEntity.Active)
                    {
                        Vector2 deathHandlerSpawnPointPos = deathHandlerSpawnPointEntity.entityPosSpawnPoint;
                        deathHandlerSpawnPoints.Add(deathHandlerSpawnPointPos);
                    }
                }
                foreach (DeathHandlerThrowableRespawnPoint deathHandlerThrowableSpawnPointEntity in level.Tracker.GetEntities<DeathHandlerThrowableRespawnPoint>())
                {
                    if (deathHandlerThrowableSpawnPointEntity.disabled == false && deathHandlerThrowableSpawnPointEntity.Active)
                    {
                        Vector2 deathHandlerThrowableSpawnPointPos = deathHandlerThrowableSpawnPointEntity.entityPosSpawnPoint;
                        deathHandlerSpawnPoints.Add(deathHandlerThrowableSpawnPointPos);
                    }
                }

                self.LevelData.Spawns.AddRange(deathHandlerSpawnPoints);                    // Add everything in deathHandlerSpawnPoints to LevelData.Spawns
                Vector2 closestSpawnPos = orig(self, from);                                 // Do the usual checks
                foreach (Vector2 ourSpawnPointPos in deathHandlerSpawnPoints)
                {
                    self.LevelData.Spawns.Remove(ourSpawnPointPos);                         // Now remove the stuff we added
                }
                return closestSpawnPos;
            }
            else
            {
                return orig(self, from);
            }
        }

        private static IEnumerator Hook_TransitionRoutine(
            On.Celeste.Level.orig_TransitionRoutine orig, global::Celeste.Level self, global::Celeste.LevelData next, Vector2 direction
        )
        {
            Utils_General.framesSinceEnteredRoom = 0;
            yield return new SwapImmediately(orig(self, next, direction));

            if (EndHelperModule.Session.AllowDeathHandlerEntityChecks) ResetFullResetAndBypassBetweenRooms(self); // AFTER room change
        }

        private static void Hook_IL_OrigTransitionRoutine(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            // --- Replace Session.LevelData.Spawns.ClosestTo(to) with Session.GetSpawnPoint(from) ---
            // by replace i mean just add a line immediately after it
            if (cursor.TryGotoNext(MoveType.After,
                instr => instr.MatchStfld<Session>("RespawnPoint")
            ))
            {
                cursor.EmitDelegate(ReplaceTransitionRoutineGetSpawnpointWithTheActualFunction);
            }
        }










        // Other functions
        public static Vector2? GetFullResetSpawnPoint(this Level level)
        {
            Player player = level.Tracker.GetEntity<Player>();
            if (player is null) return null;

            // Look at every single death handler respawn point. Get the one closest to the player.
            if (level.Tracker.GetEntity<DeathHandlerRespawnPoint>() is not null || level.Tracker.GetEntity<DeathHandlerThrowableRespawnPoint>() is not null)
            {
                List<Vector2> deathHandlerFullResetSpawnPoints = [];
                foreach (DeathHandlerRespawnPoint deathHandlerSpawnPointEntity in level.Tracker.GetEntities<DeathHandlerRespawnPoint>())
                {
                    if (deathHandlerSpawnPointEntity.disabled == false && deathHandlerSpawnPointEntity.fullReset)
                    {
                        Vector2 deathHandlerSpawnPointPos = deathHandlerSpawnPointEntity.entityPosSpawnPoint;
                        deathHandlerFullResetSpawnPoints.Add(deathHandlerSpawnPointPos);
                    }
                }
                foreach (DeathHandlerThrowableRespawnPoint deathHandlerThrowableSpawnPointEntity in level.Tracker.GetEntities<DeathHandlerThrowableRespawnPoint>())
                {
                    if (deathHandlerThrowableSpawnPointEntity.disabled == false && deathHandlerThrowableSpawnPointEntity.fullReset)
                    {
                        Vector2 deathHandlerThrowableSpawnPointPos = deathHandlerThrowableSpawnPointEntity.entityPosSpawnPoint;
                        deathHandlerFullResetSpawnPoints.Add(deathHandlerThrowableSpawnPointPos);
                    }
                }

                // Search for closest spot to player
                Vector2 closestSpawnPos = Calc.ClosestTo(deathHandlerFullResetSpawnPoints, player.BottomCenter);
                return closestSpawnPos;
            }
            return null;
        }

        public static void ResetFullResetAndBypassBetweenRooms(Level level)
        {
            DeathBypass.ClearDeathBypassID(level);
            Vector2? firstFullResetRespawnPoint = GetFullResetSpawnPoint(level);
            EndHelperModule.Session.nextRespawnFullReset = false;

            //Logger.Log(LogLevel.Info, "EndersExtras/Utils_DeathHandler", $"Full Reset and bypass between rooms. firstFullResetRespawnPoint: {firstFullResetRespawnPoint}");

            if (firstFullResetRespawnPoint is not null)
            {
                EndHelperModule.Session.firstFullResetPos = firstFullResetRespawnPoint.Value;
                EndHelperModule.Session.lastFullResetPos = firstFullResetRespawnPoint.Value;
            }
            else
            {
                EndHelperModule.Session.firstFullResetPos = null;
                EndHelperModule.Session.lastFullResetPos = null;
            }
            //Logger.Log(LogLevel.Info, "EndersExtras/Utils_DeathHandler", $"room transitiionnn. firstfullresetpos is {firstFullResetRespawnPoint}. null? : {firstFullResetRespawnPoint is null}");
        }

        internal static void ReplaceTransitionRoutineGetSpawnpointWithTheActualFunction()
        {
            // Returning the actual function should always be better
            // but since this is a little jank, just in case, we'll add a DeathHanderRespawnPoint check
            if (Engine.Scene is Level level &&
                (level.Tracker.GetEntity<DeathHandlerRespawnPoint>() is not null || level.Tracker.GetEntity<DeathHandlerThrowableRespawnPoint>() is not null))
            {
                Player player = level.Tracker.GetEntity<Player>();
                Vector2 to = player.CollideFirst<RespawnTargetTrigger>()?.Target ?? player.Position;
                level.Session.RespawnPoint = level.Session.GetSpawnPoint(to);
            }
        }
    }
}
