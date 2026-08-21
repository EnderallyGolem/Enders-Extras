using Celeste.Mod.EndersExtras.Utils;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.EndersExtras.Entities.Utility
{
    [CustomEntity("EndersExtras/FlagInvisibleBarrier")]
    [Tracked(false)]
    public class FlagInvisibleBarrier : InvisibleBarrier
    {
        private readonly string requireFlag;
        private readonly bool disableLeft;
        private readonly bool disableRight;
        private readonly bool disableAbove;
        private readonly bool disableBelow;
        private readonly bool disablePermanently;
        private readonly bool enablePermanently;

        private bool lockState = false;
        private bool? enableBarrier = null;

        public FlagInvisibleBarrier(EntityData data, Vector2 offset) : base(data, offset)
        {
            requireFlag = data.Attr("requireFlag", "");
            disableLeft = data.Bool("disableLeft", false);
            disableRight = data.Bool("disableRight", false);
            disableAbove = data.Bool("disableAbove", false);
            disableBelow = data.Bool("disableBelow", false);
            disablePermanently = data.Bool("disablePermanently", false);
            enablePermanently = data.Bool("enablePermanently", false);
        }

        public override void Update()
        {
            Active = true;

            // Lock State Check
            if (!lockState || enableBarrier is null)
            {
                Level level = SceneAs<Level>();

                // Flag Check
                enableBarrier = Utils_General.AreFlagsEnabled(level.Session, requireFlag, true);

                // Direction Disabling Checks
                if (level.Tracker.GetEntity<Player>() is {} player)
                {
                    if (disableLeft && player.Left < Collider.Bounds.Center.X) enableBarrier = false;
                    if (disableRight && player.Right > Collider.Bounds.Center.X) enableBarrier = false;
                    if (disableAbove && player.Top < Collider.Bounds.Center.Y) enableBarrier = false;
                    if (disableBelow && player.Bottom > Collider.Bounds.Center.Y) enableBarrier = false;
                }

                // Lock State Set
                if (disablePermanently && enableBarrier == false) lockState = true;
                if (enablePermanently && enableBarrier == true) lockState = true;
            }

            // Disable if player inside (do not affect lock state for this one)
            bool tempEnable = enableBarrier ?? true;
            if (tempEnable)
            {
                Collidable = true; // For collide check to work lol
                if (CollideCheck<Player>()) tempEnable = false;
            }

            // Update collider
            Collidable = tempEnable;
        }
    }
}