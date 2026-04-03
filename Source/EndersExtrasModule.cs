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

// Update: MMHook_Celeste, MonoMod.Cecil, MonoMod.Core

namespace Celeste.Mod.EndersExtras;

public class EndersExtrasModule : EverestModule {
    public static EndersExtrasModule Instance { get; private set; }

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
#endif
    }

    //Custom spritebank
    public static SpriteBank SpriteBank => Instance._CustomEntitySpriteBank;
    private SpriteBank _CustomEntitySpriteBank;
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
        Everest.Events.AssetReload.OnReloadLevel += AssetReloadLevelFunc;
        Everest.Events.Level.OnBeforeUpdate += OnBeforeLevelUpdate;
        On.Celeste.Level.TransitionRoutine += Hook_TransitionRoutine;
        On.Celeste.LevelLoader.StartLevel += Hook_StartMapFromBeginning;

        On.Celeste.Player.Die += Hook_OnPlayerDeath;
        MethodInfo ILOrigDie = typeof(Player).GetMethod("orig_Die", BindingFlags.Public | BindingFlags.Instance);
        Loadhook_Player_OrigDie = new ILHook(ILOrigDie, Hook_ILOrigDie);

        On.Celeste.Editor.MapEditor.Update += Hook_UsingMapEditor;

        On.Celeste.Glitch.Apply += Hook_GlitchEffectApply;

        EndersBlenderIntegration.Load();
        SpeedrunToolIntegration.Load();
    }

    public override void Unload()
    {
        Everest.Events.AssetReload.OnReloadLevel -= AssetReloadLevelFunc;
        Everest.Events.Level.OnBeforeUpdate -= OnBeforeLevelUpdate;
        On.Celeste.Level.TransitionRoutine -= Hook_TransitionRoutine;
        On.Celeste.LevelLoader.StartLevel -= Hook_StartMapFromBeginning;

        On.Celeste.Player.Die -= Hook_OnPlayerDeath;
        Loadhook_Player_OrigDie?.Dispose(); Loadhook_Player_OrigDie = null;

        On.Celeste.Editor.MapEditor.Update -= Hook_UsingMapEditor;

        On.Celeste.Glitch.Apply -= Hook_GlitchEffectApply;

        EndersBlenderIntegration.Unload();
        SpeedrunToolIntegration.Unload();

        Utils_DeathHandlerEntities.DisableHooks();
    }


    private static void SessionResetFuncs(Level level)
    {
        Utils_Shaders.LoadCustomShaders(forceReload: true);

        if (Utils_DeathHandlerEntities.EnabledDeathHandler && lastSessionResetCause == SessionResetCause.Debug)
        {
            Utils_DeathHandlerEntities.ResetFullResetAndBypassBetweenRooms(level);
        }


        if (timeSinceSessionReset <= 1)
        {
            timeSinceSessionReset = 2;
        }
    }
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

        if (origMethod is not null)
        {
            DeathCountGate.OnPlayerDeathStatic(level);
        }

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

    public static void Hook_GlitchEffectApply(On.Celeste.Glitch.orig_Apply orig, VirtualRenderTarget source, float timer, float seed, float amplitude)
    {
        // Does not work if applied at the start/end of level render
        orig(source, timer, seed, amplitude);
        if (Engine.Scene is Level level)
        {
            Utils_Shaders.ApplyShaders(level);
        }
    }
}