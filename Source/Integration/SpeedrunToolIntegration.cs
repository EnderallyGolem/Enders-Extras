using System;
using System.Collections.Generic;
using static Celeste.Mod.EndersExtras.EndersExtrasModule;
using MonoMod.ModInterop;

namespace Celeste.Mod.EndersExtras.Integration
{
    [ModImportName("SpeedrunTool.SaveLoad")]
    public static class SpeedrunToolImport
    {
        public static Func<Action<Dictionary<Type, Dictionary<string, object>>, Level>, Action<Dictionary<Type, Dictionary<string, object>>, Level>, Action, Action<Level>, Action<Level>, Action, object> RegisterSaveLoadAction;
        public static Action<Monocle.Entity, bool> IgnoreSaveState;
        public static Action<object> Unregister;
    }

    public static class SpeedrunToolIntegration
    {
        public static bool SpeedrunToolInstalled;
        private static object action;
        internal static void Load()
        {
            typeof(SpeedrunToolImport).ModInterop();
            SpeedrunToolInstalled = SpeedrunToolImport.IgnoreSaveState is not null;
            AddSaveLoadAction();
            //Logger.Log(LogLevel.Info, "EndersExtras/SpeedrunToolIntegration", $"initialise stuff perhaps. {SpeedrunToolInstalled}");
        }

        internal static void Unload()
        {
            RemoveSaveLoadAction();
        }

        private static void AddSaveLoadAction()
        {
            if (!SpeedrunToolInstalled)
            {
                return;
            }

            action = SpeedrunToolImport.RegisterSaveLoadAction(
                // Save State - Action<Dictionary<Type, Dictionary<string, object>>
                (_, level) => {

                },

                // Load State - Action<Dictionary<Type, Dictionary<string, object>>, Level>
                (_, level) => {

                },

                // Clear State - Action
                null,

                // Level before Save State - Action<Level>
                null,

                // Level before Load State - Action<Level>
                (level) =>
                {
                    OnLoadState(level);
                },

                // preCloneEntities - Action
                null
            );
        }

        private static void RemoveSaveLoadAction()
        {
            if (SpeedrunToolInstalled)
            {
                SpeedrunToolImport.Unregister(action);
            }
        }

#pragma warning disable IDE0051  // Private method is unused


        private static void OnLoadState(Level preloadLevel)
        {
            EndersExtrasModule.timeSinceSessionReset = 0; // Call for reset
            lastSessionResetCause = SessionResetCause.LoadState;
        }
    }
}