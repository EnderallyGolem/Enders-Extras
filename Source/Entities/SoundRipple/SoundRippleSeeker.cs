using System;
using System.Collections.Generic;
using Celeste.Mod.EndersExtras.Entities.Misc;
using Celeste.Mod.EndersExtras.Utils;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;



namespace Celeste.Mod.EndersExtras.Entities.SoundRipple;
[Tracked(true)]
[CustomEntity("EndersExtras/SoundRippleSeeker")]
public class SoundRippleSeeker : Seeker
{
    private bool hasSpotlight;
    private bool cannotDetectTileEntity;
    public SoundRippleSeeker(EntityData data, Vector2 offset) : base(data.Position + offset, data.NodesOffset(offset))
    {
        hasSpotlight = data.Bool("hasSpotlight", false);
        cannotDetectTileEntity = data.Bool("cannotDetectTileEntity", true);
        if (!hasSpotlight) Light.Color *= 0;
    }

    public override void Update()
    {
        base.Update();
        this.canSeePlayer = false;
        if (!hasSpotlight) Light.Color *= 0;
    }

    internal bool CanSeePlayerHook(Player? player, bool returnVal)
    {
        if (!returnVal) return false;

        if (cannotDetectTileEntity && player is not null && player.Components.Get<SoundRippleBell.SoundRippleDetected>() is null)
        {
            Level level = SceneAs<Level>();
            List<Entity> entity = level.Tracker.GetEntities<TileEntity>();
            foreach (var entity1 in entity)
            {
                // Two checks: Player in direct collision with TileEntity, and no line of sight.
                var tileEntity = (TileEntity)entity1;

                // Direct collision
                if (tileEntity.CollideCheck(player)) return false;

                // Line of sight check
                bool origCollidable = tileEntity.Collidable;
                tileEntity.Collidable = true;
                bool viewBlocked = tileEntity.CollideLine(this.Center, player.Center);
                tileEntity.Collidable = origCollidable;
                if (viewBlocked) return false;
            }
        }

        return returnVal;
    }
}