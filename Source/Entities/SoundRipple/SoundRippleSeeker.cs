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
    private readonly bool hasSpotlight;
    private readonly bool cannotDetectTileEntity;
    private readonly bool dieInBarrier;
    public SoundRippleSeeker(EntityData data, Vector2 offset) : base(data.Position + offset, data.NodesOffset(offset))
    {
        hasSpotlight = data.Bool("hasSpotlight", false);
        cannotDetectTileEntity = data.Bool("cannotDetectTileEntity", true);
        dieInBarrier = data.Bool("dieInBarrier", false);
        if (!hasSpotlight) Light.Color *= 0;
    }

    public override void Update()
    {
        base.Update();
        this.canSeePlayer = false;
        if (!hasSpotlight) Light.Color *= 0;
        if (dieInBarrier) KillIfInBarrier();
    }

    private void KillIfInBarrier()
    {
        Level level = SceneAs<Level>();
        List<Entity> seekerBarriers = level.Tracker.GetEntities<SeekerBarrier>();
        foreach (var e in seekerBarriers)
        {
            SeekerBarrier barrier = (SeekerBarrier)e;
            barrier.Collidable = true;
            if (barrier.CollideCheck(this)) Kys();
            barrier.Collidable = false;
        }
    }

    private void Kys()
    {
        Entity entity = new Entity(this.Position);
        entity.Add((Component) new DeathEffect(Color.HotPink, new Vector2?(this.Center - this.Position))
        {
            OnEnd = (System.Action) (entity.RemoveSelf)
        });
        entity.Depth = -1000000;
        this.Scene.Add(entity);
        Audio.Play("event:/game/05_mirror_temple/seeker_death", this.Position);
        this.RemoveSelf();
        this.dead = true;
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