using Celeste.Mod.EndersExtras.Entities.Misc;
using Celeste.Mod.EndersExtras.Integration;
using Celeste.Mod.EndersExtras.Utils;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections;
using System.Reflection;
using Celeste.Mod.EndersExtras.Entities.SoundRipple;
using Celeste.Mod.EndHelper.Utils;
using MonoMod.Utils;

// ReSharper disable PossibleInvalidCastExceptionInForeachLoop

// Update: MMHook_Celeste, MonoMod.Cecil, MonoMod.Core

namespace Celeste.Mod.EndersExtras;

public class EndersExtrasModule : EverestModule {
    public static EndersExtrasModule Instance { get; private set; } = null!;

    public override Type SettingsType => typeof(EndersExtrasModuleSettings);
    public static EndersExtrasModuleSettings Settings => (EndersExtrasModuleSettings) Instance._Settings;

    public override Type SessionType => typeof(EndersExtrasModuleSession);
    public static EndersExtrasModuleSession Session => (EndersExtrasModuleSession) Instance._Session;

    public override Type SaveDataType => typeof(EndersExtrasModuleSaveData);
    public static EndersExtrasModuleSaveData SaveData => (EndersExtrasModuleSaveData) Instance._SaveData;

    public EndersExtrasModule() {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(nameof(EndersExtrasModule), LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(nameof(EndersExtrasModule), LogLevel.Info);

        //Logger.Log(LogLevel.Info, "EndersExtras/Main", $"....");
#endif
    }

    //Custom spritebank
    public static SpriteBank SpriteBank => Instance._CustomEntitySpriteBank;
    private SpriteBank _CustomEntitySpriteBank = null!;
    public override void LoadContent(bool firstLoad)
    {
        base.LoadContent(firstLoad);
        _CustomEntitySpriteBank = new SpriteBank(GFX.Game, "Graphics/EndersExtras/Sprites.xml");
    }

    public enum SessionResetCause { None, LoadState, Debug, ReenterMap }
    public static SessionResetCause lastSessionResetCause = SessionResetCause.None; // Stores the previous cause of reset. Sometimes useful.
    public static int timeSinceSessionReset = 2;                                    // If == 1, correct for resets if needed. Starts from 2 so it does not cause a reset when loading!



    private static ILHook Loadhook_Player_OrigDie;
    public override void Load()
    {
        Everest.Events.Level.OnEnter += EnterMapFunc;
        Everest.Events.AssetReload.OnReloadLevel += AssetReloadLevelFunc;
        Everest.Events.AssetReload.OnBeforeReload += ReloadBeginFunc;
        Everest.Events.AssetReload.OnAfterReload += ReloadCompleteFunc;
        Everest.Events.Level.OnBeforeUpdate += OnBeforeLevelUpdate;
        On.Celeste.Level.TransitionRoutine += Hook_TransitionRoutine;
        On.Celeste.LevelLoader.StartLevel += Hook_StartMapFromBeginning;

        On.Celeste.Player.Die += Hook_OnPlayerDeath;
        MethodInfo ILOrigDie = typeof(Player).GetMethod("orig_Die", BindingFlags.Public | BindingFlags.Instance);
        Loadhook_Player_OrigDie = new ILHook(ILOrigDie!, Hook_ILOrigDie);
        On.Celeste.Player.IntroRespawnBegin += Hook_OnPlayerRespawn;
        //On.Celeste.OuiChapterPanel.
        On.Celeste.OuiChapterPanel.Render += Hook_OuiChapterPanelRender;
        IL.Celeste.OuiChapterPanel.Render += ILHook_OuiChapterPanelRender;
        On.Celeste.OuiChapterPanel._FixTitleLength += Hook_OuiChapterPanelFixTitleLength;

        On.Celeste.Seeker.CanSeePlayer += Hook_SeekerSeePlayer;
        On.Celeste.Editor.MapEditor.Update += Hook_UsingMapEditor;
        On.Celeste.Glitch.Apply += Hook_GlitchEffectApply;

        EndersBlenderIntegration.Load();
        SpeedrunToolIntegration.Load();
    }

    public override void Unload()
    {
        Everest.Events.Level.OnEnter -= EnterMapFunc;
        Everest.Events.AssetReload.OnReloadLevel -= AssetReloadLevelFunc;
        Everest.Events.AssetReload.OnBeforeReload -= ReloadBeginFunc;
        Everest.Events.AssetReload.OnAfterReload -= ReloadCompleteFunc;
        Everest.Events.Level.OnBeforeUpdate -= OnBeforeLevelUpdate;
        On.Celeste.Level.TransitionRoutine -= Hook_TransitionRoutine;
        On.Celeste.LevelLoader.StartLevel -= Hook_StartMapFromBeginning;

        On.Celeste.Player.Die -= Hook_OnPlayerDeath;
        Loadhook_Player_OrigDie?.Dispose(); Loadhook_Player_OrigDie = null;
        On.Celeste.Player.IntroRespawnBegin -= Hook_OnPlayerRespawn;
        On.Celeste.OuiChapterPanel.Render -= Hook_OuiChapterPanelRender;
        IL.Celeste.OuiChapterPanel.Render -= ILHook_OuiChapterPanelRender;
        On.Celeste.OuiChapterPanel._FixTitleLength -= Hook_OuiChapterPanelFixTitleLength;

        On.Celeste.Seeker.CanSeePlayer -= Hook_SeekerSeePlayer;
        On.Celeste.Editor.MapEditor.Update -= Hook_UsingMapEditor;
        On.Celeste.Glitch.Apply -= Hook_GlitchEffectApply;

        UnloadTempHooks();
    }

    private static void UnloadTempHooks()
    {
        Utils_CassetteManager.DisableHooks();
        Utils_DeathHandlerEntities.DisableHooks();
    }


    private static void SessionResetFuncs(Level level)
    {
        Utils_Shaders.LoadCustomShaders(forceReload: true);

        if (Utils_DeathHandlerEntities.EnabledDeathHandler && lastSessionResetCause == SessionResetCause.Debug)
        {
            Utils_DeathHandlerEntities.ResetFullResetAndBypassBetweenRooms(level);
        }

        if (EndersExtrasModule.Session.enableRoomSwapFuncs)
        {
            // This only exists so it updates when you respawn from debug. It umm still requires a transition/respawn to work lol
            // Also runs if SessionResetCause is ReenterMap
            Utils_RoomSwap.ReupdateAllRooms(level);

            if (lastSessionResetCause == SessionResetCause.Debug || lastSessionResetCause == SessionResetCause.ReenterMap)
            {
                // Check if require double reload - if room the player is in a grid
                String currentRoom = level.Session.LevelData.Name;
                foreach (String gridID in EndersExtrasModule.Session.roomSwapOrderList.Keys)
                {
                    String roomSwapPrefix = EndersExtrasModule.Session.roomSwapPrefix[gridID];
                    if (currentRoom.Contains(roomSwapPrefix))
                    {
                        // Is in one! Reload level again and break out of the loop.
                        level.Reload();
                    }
                }
            }
        }


        if (timeSinceSessionReset <= 1)
        {
            timeSinceSessionReset = 2;
        }
    }


    private static void EnterMapFunc(global::Celeste.Session session, bool fromSaveData)
    {
        // Disable level-dependent hooks if enabled
        UnloadTempHooks();
    }

    public static bool reloadComplete;
    public static void AssetReloadLevelFunc(global::Celeste.Level level)
    {
        // Yeah this exists solely so reloading a map midway through it doesn't break.
        // Solely this or solely EnterMapFunc doesn't work.
        // Also these are both in timeSinceSessionReset > 2 checks so they don't infinite loop off each other
        // Can you tell that the code is made with glue and duct tape
        if (timeSinceSessionReset > 2)
        {
            timeSinceSessionReset = 0;
            lastSessionResetCause = SessionResetCause.ReenterMap;
        }
    }
    private static void ReloadCompleteFunc(bool silent)
    {
        reloadComplete = true;
    }
    private static void ReloadBeginFunc(bool silent)
    {
        reloadComplete = false;
    }


    private static bool Hook_SeekerSeePlayer(On.Celeste.Seeker.orig_CanSeePlayer orig, Seeker self, Player player)
    {
        bool returnVal = orig(self, player);
        if (self is SoundRippleSeeker s) returnVal = s.CanSeePlayerHook(player, returnVal);
        return returnVal;
    }
    public static void Hook_UsingMapEditor(On.Celeste.Editor.MapEditor.orig_Update orig, global::Celeste.Editor.MapEditor self)
    {
        timeSinceSessionReset = 0;
        lastSessionResetCause = SessionResetCause.Debug;
        orig(self);
    }



    private static void OnBeforeLevelUpdate(global::Celeste.Level level)
    {
        if (!level.Transitioning && !level.FrozenOrPaused) Utils_General.framesSinceEnteredRoom++;

        // Session Reset Checker
        EndersExtrasModule.timeSinceSessionReset++;
        if (EndersExtrasModule.timeSinceSessionReset == 1)
        {
            SessionResetFuncs(level);
        }

        // Tick up Respawn Ripple Shader
        if (RespawnRipple.enableShader)
        {
            RespawnRipple.UpdateRipples(level);
        }
    }

    private static void Hook_StartMapFromBeginning(On.Celeste.LevelLoader.orig_StartLevel orig, global::Celeste.LevelLoader self)
    {
        Utils_Shaders.LoadCustomShaders(forceReload: true);
        orig(self);
    }

    private static IEnumerator Hook_TransitionRoutine(
        On.Celeste.Level.orig_TransitionRoutine orig, global::Celeste.Level self, global::Celeste.LevelData next, Vector2 direction
    )
    {
        Utils_General.framesSinceEnteredRoom = 0;
        yield return new SwapImmediately(orig(self, next, direction));
        DeathCountGate.OnTransitionStatic(self);
    }

    public static PlayerDeadBody Hook_OnPlayerDeath(On.Celeste.Player.orig_Die orig, global::Celeste.Player self, Vector2 direction, bool evenIfInvincible, bool registerDeathInStats)
    {
        Level level = self.SceneAs<Level>();
        PlayerDeadBody origMethod = orig(self, direction, evenIfInvincible, registerDeathInStats);

        // DeathCountGate - Track death count
        if (origMethod is not null) DeathCountGate.OnPlayerDeathStatic(level);

        return origMethod;
    }
    public static void Hook_ILOrigDie(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);

        // Match session.Deaths++, where I run stat menu add death
        if (cursor.TryGotoNext(MoveType.After,
            //instr => instr.MatchDup(),
            //instr => instr.MatchLdfld<Session>("Deaths"),
            //instr => instr.MatchLdcI4(out _),
            //instr => instr.MatchAdd(),
            instr => instr.MatchStfld(typeof(global::Celeste.Session), "Deaths")
        ))
        {
            cursor.EmitDelegate(ILRunOnPlayerDeath);
        }
    }

    public static void ILRunOnPlayerDeath()
    {
        if (Engine.Scene is Level level)
        {
            foreach (ConditionalBirdTutorial conditionalBirdTutorial in level.Tracker.GetEntities<ConditionalBirdTutorial>())
            {
                conditionalBirdTutorial.UpdateConditionTracking_Death();
            }
        }
    }

    public static void Hook_OnPlayerRespawn(On.Celeste.Player.orig_IntroRespawnBegin orig, global::Celeste.Player self)
    {
        //Update the room-swap rooms. This is kind of here as a failsafe,
        //and also otherwise warping with debug mode permamently empty the swap rooms.
        Utils_RoomSwap.ReupdateAllRooms();
        orig(self);
    }

    private static void Hook_OuiChapterPanelRender(On.Celeste.OuiChapterPanel.orig_Render orig, OuiChapterPanel self)
    {
        string dialogId = ("endersextras_removechapternum_" + self.Area.GetSID()).DialogKeyify();
        if (Dialog.Has(dialogId))
        {
            bool originalInterludeSafe = self.Data.Interlude_Safe; self.Data.Interlude_Safe = true;
            orig(self);
            self.Data.Interlude_Safe = originalInterludeSafe;
            return;
        }
        orig(self);
    }

    private static void ILHook_OuiChapterPanelRender(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);

        // Locate string text = Dialog.Clean(AreaData.Get(this.Area).Name);
        // This text will be replaced if possible
        // Also umm collabutils also hooks this same place so lemme just split my hook checks
        #pragma warning disable CL0006
        if (cursor.TryGotoNext(MoveType.After,
                instr => instr.MatchLdarg0(),
                instr => instr.MatchLdfld<OuiChapterPanel>("Area"),
                instr => instr.MatchCall<AreaData>("Get"),
                instr => instr.MatchLdfld<AreaData>("Name"),
                instr => true,
                instr => true
            )
         && (cursor.TryGotoNext(MoveType.After,
                instr => instr.MatchLdnull(),
                instr => instr.MatchCall(typeof(Dialog), "Clean")
            )))
        #pragma warning restore CL0006
        {
            cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<string, OuiChapterPanel, string>>(OuiChapterSelectReplacementMapName);
        }
    }

    private static String OuiChapterSelectReplacementMapName(String originalText, OuiChapterPanel panel)
    {
        // Override chapter name
        string dialogId = ("endersextras_mapname_" + panel.Area.GetSID()).DialogKeyify();
        if (Dialog.Has(dialogId)) {
            return Dialog.Clean(dialogId);
        }
        return originalText;
    }

    private static float Hook_OuiChapterPanelFixTitleLength(On.Celeste.OuiChapterPanel.orig__FixTitleLength orig, OuiChapterPanel panel, float vanillaValue)
    {
        float returnLength = orig(panel, vanillaValue);

        string dialogId = ("endersextras_mapname_" + panel.Area.GetSID()).DialogKeyify();
        if (Dialog.Has(dialogId))
        {
            float x = ActiveFont.Measure(Dialog.Clean(dialogId)).X;
            return vanillaValue - Math.Max(0.0f, (float) ((double) x + (double) vanillaValue - 490.0));
        }
        return returnLength;
    }

    public static void Hook_GlitchEffectApply(On.Celeste.Glitch.orig_Apply orig, VirtualRenderTarget source, float timer, float seed, float amplitude)
    {
        // Does not work if applied at the start/end of level render
        if (Engine.Scene is Level level) Utils_Shaders.ApplyShaders(level);
        orig(source, timer, seed, amplitude);
    }
}