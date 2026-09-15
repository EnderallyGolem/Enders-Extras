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

        private readonly bool disablePlayerInside = true;
        private readonly bool deathBarrier = false;

        public FlagInvisibleBarrier(EntityData data, Vector2 offset) : base(data, offset)
        {
            requireFlag = data.Attr("requireFlag", "");
            disableLeft = data.Bool("disableLeft", false);
            disableRight = data.Bool("disableRight", false);
            disableAbove = data.Bool("disableAbove", false);
            disableBelow = data.Bool("disableBelow", false);
            disablePermanently = data.Bool("disablePermanently", false);
            disablePlayerInside = data.Bool("disablePlayerInside", true);
            enablePermanently = data.Bool("enablePermanently", false);
            deathBarrier = data.Bool("deathBarrier", false);
        }

        public override void Update()
        {
            Active = true;
            bool? oldEnableBarrier = enableBarrier;

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

            // Disable if player inside and disablePlayerInside (do not affect lock state for this one)
            bool tempEnable = enableBarrier ?? true;
            if (tempEnable)
            {
                Collidable = true; // For collide check to work lol

                // disablePlayerInside - disable if player inside
                if (!deathBarrier && CollideCheck<Player>() && disablePlayerInside) tempEnable = false;

                // deathBarrier - disable if player inside, AND previous enableBarrier is false
                if (deathBarrier && CollideCheck<Player>() && oldEnableBarrier==false) tempEnable = false;
                //Logger.Log(LogLevel.Info, "EndersExtras/FlagInvisibleBarrier", $"regain dash {deathBarrier} {CollideCheck<Player>()} {oldEnableBarrier==false}");
            }

            // Update collider
            enableBarrier = Collidable = tempEnable;

            // If deathBarrier, kill the player if they collide and otherwise shut the collision off
            if (deathBarrier && Collidable)
            {
                Player? player = CollideFirst<Player>();
                if (player is not null)
                {
                    player.Die(-player.Speed.SafeNormalize());
                }
                Collidable = false;
            }
        }

        public override void DebugRender(Camera camera)
        {
            base.DebugRender(camera);
            if (deathBarrier)
            {
                this.Collider.Render(camera, enableBarrier==true ? Color.Red : Color.DarkRed);
            }
        }
    }
}