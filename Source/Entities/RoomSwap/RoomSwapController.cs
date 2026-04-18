using Celeste.Mod.Entities;
using Celeste.Mod.EndersExtras.Triggers;
using Celeste.Mod.EndersExtras.Utils;
using Microsoft.Xna.Framework;
using Monocle;



namespace Celeste.Mod.EndersExtras.Entities.RoomSwap;

[CustomEntity("EndersExtras/RoomSwapController")]
public class RoomSwapController : Entity
{
    private string gridID;

    public RoomSwapController(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        Collider = new Hitbox(16, 16, -8, -8);

        gridID = data.Attr("gridId", "1");

        // Swappy Setup
        EndersExtrasModule.Session.roomSwapRow[gridID] = data.Int("totalRows", 0);
        EndersExtrasModule.Session.roomSwapColumn[gridID] = data.Int("totalColumns", 0);
        EndersExtrasModule.Session.roomSwapPrefix[gridID] = data.Attr("swapRoomNamePrefix", "swap");
        EndersExtrasModule.Session.roomTemplatePrefix[gridID] = data.Attr("templateRoomNamePrefix", "template");

        EndersExtrasModule.Session.roomTransitionTime[gridID] = data.Float("roomTransitionTime", 0.3f);
        EndersExtrasModule.Session.activateSoundEvent1[gridID] = data.Attr("activateSoundEvent1", "");
        EndersExtrasModule.Session.activateSoundEvent2[gridID] = data.Attr("activateSoundEvent2", "");

        EndersExtrasModule.Session.enableRoomSwapFuncs = true;
    }
    public override void Added(Scene scene)
    {
        base.Added(scene);
        // Ensure reset only occurs ONCE. Reentering room with controller doesn't re-resets it.
        // also do not check player properties, player is null when the map *just* loads
        if (!EndersExtrasModule.Session.roomSwapOrderList.ContainsKey(gridID))
        {
            EndersExtrasModule.Session.allowTriggerEffect[gridID] = true;
            EndersExtrasModule.Session.roomMapLevel[gridID] = 0;

            Level level = SceneAs<Level>();
            Player player = level.Tracker.GetEntity<Player>();

            Utils_RoomSwap.ModifyRooms("Reset", true, player, level, gridID); //This loads the stuff in 
            Logger.Log(LogLevel.Info, "EndersExtras/RoomSwapController", $"Added a {EndersExtrasModule.Session.roomSwapRow[gridID]}x{EndersExtrasModule.Session.roomSwapColumn[gridID]} grid with id {gridID}");
        }
    }
}