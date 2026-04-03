using Celeste.Mod.EndHelper.Utils;
using Monocle;
using MonoMod.ModInterop;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.EndersExtras.Integration
{
    [ModImportName("EndersBlender.DeathHandler")]
    public static class EndersBlenderImport
    {
        public static Func<bool> GetEnableEntityChecks;
        public static Func<bool> GetNextRespawnFullReset;
        public static Func<bool> GetManualReset;
    }

    public static class EndersBlenderIntegration
    {
        public static bool ModInstalled;
        internal static void Load()
        {
            typeof(EndersBlenderImport).ModInterop();
            ModInstalled = EndersBlenderImport.GetEnableEntityChecks is not null;
            Logger.Log(LogLevel.Info, "EndersExtras/EnderBlenderIntegration", $"initialise stuff perhaps. {ModInstalled}");
        }

        internal static void Unload()
        {
        }

        internal static bool CheckShowBypassEffects(Entity entity)
        {
            return entity.Components.Get<DeathBypass>() is DeathBypass deathBypassComponent && deathBypassComponent.bypass && deathBypassComponent.showVisuals;
        }
        internal static bool CheckShowDisableBypassEffects(Entity entity)
        {
            return entity.Components.Get<DeathBypass>() is DeathBypass deathBypassComponent && !deathBypassComponent.bypass && deathBypassComponent.allowBypass
                    && deathBypassComponent.showVisuals;
        }
    }
}