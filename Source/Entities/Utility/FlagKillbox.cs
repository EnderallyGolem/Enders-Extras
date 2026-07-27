using System.Runtime.CompilerServices;
using Celeste.Mod.EndersExtras.Utils;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.EndersExtras.Entities.Utility
{
    [CustomEntity("EndersExtras/FlagKillbox")]
    [Tracked(false)]
    [TrackedAs(typeof(Killbox))]
    public class FlagKillbox : Killbox
    {
        private readonly float triggerDistance;
        private readonly string requireFlag;
        private readonly bool permanentActivate;
        private readonly bool immediateUpdate;

        private bool flagAllow = false;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public FlagKillbox(EntityData data, Vector2 offset)
            : base(data, offset)
        {
            triggerDistance = data.Float("triggerDistance", 4f);
            requireFlag = data.Attr("requireFlag", "");
            permanentActivate = data.Bool("permamentActivate", true);
            immediateUpdate = data.Bool("immediateUpdate", false);
        }

        public override void Awake(Scene scene)
        {
            base.Awake(scene);
            if (immediateUpdate) Update();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public override void Update()
        {
            bool collidableOverride = Collidable;
            base.Update();

            Level level = SceneAs<Level>();
            float triggerPixels = triggerDistance * 8f;
            if (permanentActivate && flagAllow) { } // Stay true if permament activate
            else
            {
                flagAllow = Utils_General.AreFlagsEnabled(level.Session, requireFlag, true);
            }

            if (!collidableOverride)
            {
                Player player = base.Scene.Tracker.GetEntity<Player>();
                if (player != null && player.Bottom < base.Top - triggerPixels && flagAllow)
                {
                    collidableOverride = true;
                }
            }
            else
            {
                Player entity2 = base.Scene.Tracker.GetEntity<Player>();
                if ((entity2 != null && entity2.Top > base.Bottom + 32f) || !flagAllow)
                {
                    collidableOverride = false;
                }
            }

            Collidable = collidableOverride;
        }
    }
}