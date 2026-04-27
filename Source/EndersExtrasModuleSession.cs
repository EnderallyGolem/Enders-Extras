using System.Collections.Generic;

namespace Celeste.Mod.EndersExtras;

public class EndersExtrasModuleSession : EverestModuleSession {

    #region Room Swap

    public Dictionary<string, bool> allowTriggerEffect { get; set; } = new() { };
    public Dictionary<string, int> roomSwapRow { get; set; } = new() { };
    public Dictionary<string, int> roomSwapColumn { get; set; } = new() { };
    public Dictionary<string, string> roomSwapPrefix { get; set; } = new() { };
    public Dictionary<string, string> roomTemplatePrefix { get; set; } = new() { };
    public Dictionary<string, float> roomTransitionTime { get; set; } = new() { };
    public Dictionary<string, string> activateSoundEvent1 { get; set; } = new() { };
    public Dictionary<string, string> activateSoundEvent2 { get; set; } = new() { };
    public Dictionary<string, int> roomMapLevel { get; set; } = new() { };

    public bool enableRoomSwapFuncs;

    // 2D list containing template room names. The index matches up with the swap room locations.
    public Dictionary<string, List<List<string>>> roomSwapOrderList { get; set; } = new() { };

    #endregion
}