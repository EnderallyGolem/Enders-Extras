using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using System;
using System.Runtime.CompilerServices;
using Monocle;
using static MonoMod.InlineRT.MonoModRule;
using System.Threading.Tasks;
using Celeste.Mod.EndersExtras.Utils;

namespace Celeste.Mod.EndersExtras.Triggers.RoomSwap;

[CustomEntity("EndersExtras/RoomSwapChangeRespawnTrigger")]
public class RoomSwapChangeRespawnTrigger : Trigger
{
    private Vector2 Target;
    private Vector2 dataOffset;
    private EntityData entityData;

    private bool checkSolid = true;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public RoomSwapChangeRespawnTrigger(EntityData data, Vector2 offset)
        : base(data, offset)
    {
        checkSolid = data.Bool("checkSolid", true);
        Collider = new Hitbox(data.Width, data.Height);
        entityData = data;
        dataOffset = offset;
        Visible = Active = false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override void Added(Scene scene)
    {
        base.Added(scene);
    }

    public override void Awake(Scene scene)
    {
        base.Awake(scene);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override void OnEnter(Player player)
    {
        Level level = SceneAs<Level>();

        //Move target to closest spawn point
        if (entityData.Nodes != null && entityData.Nodes.Length != 0)
        {
            Target = entityData.Nodes[0] + dataOffset;
        }
        else
        {
            Target = Center;
        }

        UpdateRespawnPos(Target, level, checkSolid);
    }

    /// Moves the respawn position to the point closest to targetPos. Outputs true if successful change.
    private static bool UpdateRespawnPos(Vector2 targetPos, Level level, bool checkSolid)
    {
        bool changedRespawn = false;

        targetPos = level.GetSpawnPoint(targetPos);

        Session session = level.Session;
        if (NoInvalidCheck(level, targetPos, checkSolid) && (!session.RespawnPoint.HasValue || session.RespawnPoint.Value != targetPos))
        {
            session.HitCheckpoint = true;
            if (session.RespawnPoint != targetPos)
            {
                changedRespawn = true;
            }
            session.RespawnPoint = targetPos;
            session.UpdateLevelStartDashes();
        }

        //Logger.Log(LogLevel.Info, "EndHelper/Utils_DeathHandler", $"Tried updating respawn point to {targetPos}. Success: {changedRespawn}");
        return changedRespawn;
    }

    private static bool NoInvalidCheck(Level level, Vector2 targetPos, bool checkInvalid = true)
    {
        if (!checkInvalid)
        {
            return true; // Avoid any checks for solid. Always return true (no solids), since they are already invalid by default.
        }

        Vector2 point = targetPos + Vector2.UnitY * -4f;

        if (level.CollideCheck<CrystalStaticSpinner>(point) || level.CollideCheck<DustStaticSpinner>(point) || level.CollideCheck<Spikes>(point))
        {
            return false;
        }
        if (level.CollideCheck<Solid>(point))
        {
            return level.CollideCheck<FloatySpaceBlock>(point);
        }

        return true;
    }
}