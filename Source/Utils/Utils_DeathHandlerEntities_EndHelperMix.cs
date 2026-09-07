using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Celeste.Mod.EndHelper;
using Celeste.Mod.EndHelper.Utils;
using Microsoft.Xna.Framework;
using Monocle;
using static Celeste.Mod.EndersExtras.Utils.Utils_DeathHandlerEntities;

namespace Celeste.Mod.EndersExtras.Utils
{
    internal static class Utils_DeathHandlerEntities_EndHelperMix
    {
        #region EndHelper Function Caller
        // Slap a Utils_DeathHandlerEntities_EndHelperMix instead of Utils_DeathHandler

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void EnableDeathHandlerEntityChecks()
        {
            Utils_DeathHandler.EnableDeathHandlerEntityChecks();
            EnableDeathHandler(true); // Just in case
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static DeathBypassModifier NewDeathBypassModifier(List<Entity>? subEntityList = null, Action? beforeDeathBypassAction = null, Action? onDeathBypassAction = null)
        {
            return new DeathBypassModifier(subEntityList, beforeDeathBypassAction, onDeathBypassAction);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static DeathBypass NewDeathBypass(String requireFlag = "", bool showVisuals = true, EntityID? id = null, bool isAttached = false, bool initialAllowBypass = true, bool preventChange = false)
        {
            return new DeathBypass(requireFlag, showVisuals, id, isAttached, initialAllowBypass, preventChange);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static DeathBypass? GetDeathBypassComponent(Entity entity)
        {
            return entity.Components.Get<DeathBypass>();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void SetFullResetPos(Vector2? pos, bool overrideFirstPos = false) { Utils_DeathHandler.SetFullResetPos(pos, overrideFirstPos); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Vector2? getLastFullResetPos() { return Utils_DeathHandler.getLastFullResetPos(); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool getNextRespawnFullReset() { return Utils_DeathHandler.getNextRespawnFullReset(); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static float getDeathCooldownFrames() { return Utils_DeathHandler.getDeathCooldownFrames(); }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ForceShortDeathCooldown() { Utils_DeathHandler.ForceShortDeathCooldown(); }


        #endregion

        #region Utils Extension

        internal static void Hook_TransitionRoutine_Ext(global::Celeste.Level self)
        {

            if (EndHelperModule.Session.AllowDeathHandlerEntityChecks) ResetFullResetAndBypassBetweenRooms(self); // AFTER room change
        }

        public static void ResetFullResetAndBypassBetweenRooms_Ext(Level level)
        {
            DeathBypass.ClearDeathBypassID(level);
        }

        internal static void ResetFullReset_Ext(Level level, bool onlyIfNull = false)
        {
            if (onlyIfNull && EndHelperModule.Session.firstFullResetPos is not null && EndHelperModule.Session.lastFullResetPos is not null) return;

            Vector2? firstFullResetRespawnPoint = level.GetFullResetSpawnPoint();
            EndHelperModule.Session.nextRespawnFullReset = false;

            // Note: Possible for firstFullResetRespawnPoint to be null!
            EndHelperModule.Session.firstFullResetPos = firstFullResetRespawnPoint;
            EndHelperModule.Session.lastFullResetPos = firstFullResetRespawnPoint;
            //Logger.Log(LogLevel.Info, "EndersExtras/Utils_DeathHandler", $"room transitiionnn. firstfullresetpos is {firstFullResetRespawnPoint}. null? : {firstFullResetRespawnPoint is null}");
        }



        public static bool NoInvalidCheck_Ext(Level level, Vector2 targetPos, bool checkInvalid = true)
        {
            return Utils_DeathHandler.NoInvalidCheck(level, targetPos, checkInvalid);
        }
        public static bool NoInvalidCheck_Ext(Level level, Rectangle targetRect, bool checkInvalid = true, int inflate = 0)
        {
            return Utils_DeathHandler.NoInvalidCheck(level, targetRect, checkInvalid, inflate);
        }

        public static bool UpdateRespawnPos_Ext(Vector2 targetPos, Level level, bool checkSolid, bool fullResetOnly)
        {
            // Logger.Log(LogLevel.Info, "EndersExtras/Utils_DeathHandlerEntities_EndHelperMix", $"UpdateRespawnPos - full reset only {fullResetOnly}");
            return Utils_DeathHandler.UpdateRespawnPos(targetPos, level, checkSolid, fullResetOnly);
        }

        #endregion
    }
}
