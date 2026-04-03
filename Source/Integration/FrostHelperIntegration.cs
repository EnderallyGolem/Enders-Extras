using Celeste.Mod.EndersExtras.Utils;
using FrostHelper;
using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace Celeste.Mod.EndersExtras.Integration
{
    public static class FrostHelperIntegration
    {
        public static bool ModInstalled = false;
        internal static void Load()
        {
            EverestModuleMetadata FrostHelperMetaData = new()
            {
                Name = "FrostHelper",
                Version = new Version(1, 70, 1)
            };
            if (Everest.Loader.DependencyLoaded(FrostHelperMetaData))
            {
                ModInstalled = true;
            }
        }

        internal static void Unload()
        {

        }

        internal static bool CheckCollisionWithCustomSpinners(Level level, Rectangle targetRect)
        {
            return level.CollideCheck<CustomSpinner>(targetRect);
        }
        internal static bool CheckCollisionWithCustomSpinners(Level level, Vector2 point)
        {
            return level.CollideCheck<CustomSpinner>(point);
        }
        internal static bool CheckIfCustomSpinner(Entity entity)
        {
            return entity is CustomSpinner;
        }
    }
}
