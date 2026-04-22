using Celeste.Mod.EndersExtras.Integration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Celeste.Mod.UI;

namespace Celeste.Mod.EndersExtras.Utils
{
    internal static class Utils_Shaders
    {
        private static bool _loadedShaders = false;
        internal static Effect? FxGoldenRipple, FxGoldenRippleDisable;
        internal static Effect? FxRespawnRipple;
        internal static Effect? FxSoundEcho;
        internal static Effect? FxTintColor; // Helper shader lol
        internal static RenderTarget2D? tempRender;
        internal static RenderTarget2D? tempRenderB;
        internal static RenderTarget2D? tempRenderC;
        internal static RenderTarget2D? echoRender; // Used by SoundEcho for effect to fade away

        internal static void LoadCustomShaders(bool forceReload = false)
        {
            // Disable first
            DisableShaders();

            if (!_loadedShaders || forceReload)
            {
                tempRender = new RenderTarget2D(
                    Engine.Graphics.GraphicsDevice,
                    width: 320,
                    height: 180,
                    mipMap: false,
                    preferredFormat: SurfaceFormat.Color,
                    preferredDepthFormat: DepthFormat.Depth24Stencil8,
                    preferredMultiSampleCount: 0,
                    usage: RenderTargetUsage.DiscardContents
                );
                tempRenderB = new RenderTarget2D(
                    Engine.Graphics.GraphicsDevice,
                    width: 320,
                    height: 180,
                    mipMap: false,
                    preferredFormat: SurfaceFormat.Color,
                    preferredDepthFormat: DepthFormat.Depth24Stencil8,
                    preferredMultiSampleCount: 0,
                    usage: RenderTargetUsage.DiscardContents
                );
                tempRenderC = new RenderTarget2D(
                    Engine.Graphics.GraphicsDevice,
                    width: 320,
                    height: 180,
                    mipMap: false,
                    preferredFormat: SurfaceFormat.Color,
                    preferredDepthFormat: DepthFormat.Depth24Stencil8,
                    preferredMultiSampleCount: 0,
                    usage: RenderTargetUsage.DiscardContents
                );
                echoRender = new RenderTarget2D(
                    Engine.Graphics.GraphicsDevice,
                    width: 320,
                    height: 180,
                    mipMap: false,
                    preferredFormat: SurfaceFormat.Color,
                    preferredDepthFormat: DepthFormat.Depth24Stencil8,
                    preferredMultiSampleCount: 0,
                    usage: RenderTargetUsage.DiscardContents
                );
                //Logger.Log(LogLevel.Info, "EndersExtras/Utils_Shaders", $"Loading custom shaders.");
                FxGoldenRipple = LoadFxEndersExtras("goldenRipple"); GoldenRipple.ResetRipples();
                FxGoldenRippleDisable = LoadFxEndersExtras("goldenRippleDisable");
                FxRespawnRipple = LoadFxEndersExtras("respawnRipple");
                FxSoundEcho = LoadFxEndersExtras("soundEcho");
                FxTintColor = LoadFxEndersExtras("tintColor");
            }
            _loadedShaders = true;
        }

        private static Effect LoadFxEndersExtras(string shaderName)
        {
            ModAsset shaderAsset = Everest.Content.Get($"Effects/EndersExtras/{shaderName}.cso");
            Effect effect = new Effect(Engine.Graphics.GraphicsDevice, shaderAsset.Data);
            return effect;
        }

        public static void ApplyShaders(Level level)
        {
            // Utils_Shaders kept having the tendancy to reset upon rebuild... so this is the nice simple safe solution!
            if (!_loadedShaders) { LoadCustomShaders(); }

            // These apply each time Glitch is updated. Which is to say, every frame.
            if (GoldenRipple.enableShader) GoldenRipple.Apply(GameplayBuffers.Level, level);
            if (SoundEcho.enableShader) SoundEcho.Apply(GameplayBuffers.Level, level);
        }

        private static void DisableShaders()
        {
            _loadedShaders = false;

            tempRender?.Dispose();
            echoRender?.Dispose();
            GoldenRipple.enableShader = false;
            SoundEcho.enableShader = false;
        }
    }

    internal static class GoldenRipple
    {
        internal static bool enableShader = false; // Enabled by DeathBypass component

        // Note: This renders on top of the existing texture, so transparency doesn't work
        private struct Ripple
        {
            public Vector2 originPosition;
            public float startTime;
        }

        private static readonly List<Ripple> RippleList = new();
        //static float timeSinceLastRipple = 0f;
        private static readonly Random Rnd = new Random();

        private const float WaveSpeed = 0.6f;
        private const float WaveStrength = 0.7f;
        private const float FadeOutTime = 3f;
        private const int MaxRippleCount = 15; // For shader badly made garbage reasons: This should be MAX 20
        private const int InverseFractionSpawnChance = 15; // Eg: 10 means 1/10 chance of spawning a ripple per frame

        internal static void ResetRipples()
        {
            RippleList.Clear();
            //timeSinceLastRipple = 0;
            //Logger.Log(LogLevel.Info, "EndersExtras/Utils_Shaders", $"GoldenRipple - Resetted Ripples!");
        }

        private static void UpdateRipples(Level level)
        {
            //timeSinceLastRipple += Engine.DeltaTime;
            Camera camera = level.Camera;

            // Remove ripples that reach fadeOutTime
            for (int i = 0; i < RippleList.Count; i++)
            {
                if (RippleList[i].startTime + FadeOutTime < Engine.Scene.TimeActive)
                {
                    //Logger.Log(LogLevel.Info, "EndersExtras/Utils_Shaders", $"Removed ripple with start time {rippleList[i].StartTime}. Active time is {Engine.Scene.TimeActive}");
                    RippleList.RemoveAt(i);
                    i--;
                }
            }

            // Roll for a new ripple
            if (RippleList.Count < MaxRippleCount)
            {
                // Roll for a chance to spawn another ripple
                if (Rnd.Range(0, InverseFractionSpawnChance) >= InverseFractionSpawnChance - 1)
                {
                    // Spawn a new ripple
                    Vector2 rippleSpawnPos = new Vector2(Rnd.Range(camera.Left - 200, camera.Right + 200), Rnd.Range(camera.Top - 200, camera.Bottom + 200));
                    float rippleStartTime = Engine.Scene.TimeActive;
                    RippleList.Add(
                        new Ripple
                        {
                            originPosition = rippleSpawnPos,
                            startTime = rippleStartTime
                        }
                    );
                    //timeSinceLastRipple = 0;
                    //Logger.Log(LogLevel.Info, "EndersExtras/Utils_Shaders", $"GoldenRipple - Spawned a ripple at {rippleSpawnPos} with StartTime {rippleStartTime}. {rippleList.Count} / {maxRippleCount}");
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Apply(VirtualRenderTarget sourceTarget, Level level)
        {
            UpdateRipples(level);
            Effect effect = Utils_Shaders.FxGoldenRipple!;
            Effect effectDisable = Utils_Shaders.FxGoldenRippleDisable!;

            // Generic Parameters
            effect.Parameters["Time"]?.SetValue(Engine.Scene.TimeActive);
            effect.Parameters["Dimensions"]?.SetValue(new Vector2(GameplayBuffers.Gameplay.Width, GameplayBuffers.Gameplay.Height));
            effect.Parameters["CamPos"]?.SetValue(level.Camera.Position);
            Viewport vp = Engine.Graphics.GraphicsDevice.Viewport;
            effect.Parameters["TransformMatrix"]?.SetValue(Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0, 1));
            effect.Parameters["ViewMatrix"]?.SetValue(Matrix.Identity);

            // Special Parameters
            effect.Parameters["TimeTransitionRoom"]?.SetValue(Utils_General.framesSinceEnteredRoom / 60);
            effect.Parameters["WaveSpeed"]?.SetValue(WaveSpeed);
            effect.Parameters["WaveStrength"]?.SetValue(WaveStrength);
            effect.Parameters["FadeOutTime"]?.SetValue(FadeOutTime);
            Vector3[] rippleData = new Vector3[RippleList.Count];
            for (int i = 0; i < RippleList.Count; i++)
            {
                rippleData[i] = new Vector3(RippleList[i].originPosition.X, RippleList[i].originPosition.Y, RippleList[i].startTime);
            }
            effect.Parameters["RippleData"]?.SetValue(rippleData);
            effect.Parameters["RippleNumber"]?.SetValue(rippleData.Length);

            // Temporarily render on tempRender
            Engine.Instance.GraphicsDevice.SetRenderTarget(Utils_Shaders.tempRender);
            Engine.Instance.GraphicsDevice.Clear(Color.Transparent);
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, level.Camera.Matrix);
            // Instead of drawing everything (Draw.SpriteBatch.Draw((RenderTarget2D)source, Vector2.Zero, Color.White);)
            foreach (Entity entity in level.Entities)
            {
                if (EndersBlenderIntegration.ModInstalled && EndersBlenderIntegration.CheckShowBypassEffects(entity) && !entity.TagCheck(Tags.HUD) && !entity.TagCheck(TagsExt.SubHUD))
                {
                    if (entity.Visible)
                    {
                        entity.Render();
                    }
                }
            }
            Draw.SpriteBatch.End();

            // Blend that tempRender onto sourceTarget (GameplayBuffers.Level)
            Engine.Instance.GraphicsDevice.SetRenderTarget(sourceTarget);
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, effect);
            Draw.SpriteBatch.Draw(Utils_Shaders.tempRender, Vector2.Zero, Color.White);
            Draw.SpriteBatch.End();




            //Vector4 rippleColourFloat4Normalisation2 = new Vector4(0.9f, 0.9f, 1.9f, 0);
            // Generic Parameters
            effectDisable.Parameters["Time"]?.SetValue(Engine.Scene.TimeActive);
            effectDisable.Parameters["Dimensions"]?.SetValue(new Vector2(GameplayBuffers.Gameplay.Width, GameplayBuffers.Gameplay.Height));
            effectDisable.Parameters["CamPos"]?.SetValue(level.Camera.Position);
            effectDisable.Parameters["TransformMatrix"]?.SetValue(Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0, 1));
            effectDisable.Parameters["ViewMatrix"]?.SetValue(Matrix.Identity);

            // Special Parameters
            effectDisable.Parameters["TimeTransitionRoom"]?.SetValue(Utils_General.framesSinceEnteredRoom / 60);
            effectDisable.Parameters["WaveSpeed"]?.SetValue(WaveSpeed);
            effectDisable.Parameters["WaveStrength"]?.SetValue(WaveStrength);
            effectDisable.Parameters["FadeOutTime"]?.SetValue(FadeOutTime);
            effectDisable.Parameters["RippleData"]?.SetValue(rippleData);
            effectDisable.Parameters["RippleNumber"]?.SetValue(rippleData.Length);

            Engine.Instance.GraphicsDevice.SetRenderTarget(Utils_Shaders.tempRender);
            Engine.Instance.GraphicsDevice.Clear(Color.Transparent);
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, level.Camera.Matrix);

            foreach (Entity entity in level.Entities)
            {
                if (EndersBlenderIntegration.ModInstalled && EndersBlenderIntegration.CheckShowDisableBypassEffects(entity) && !entity.TagCheck(Tags.HUD) && !entity.TagCheck(TagsExt.SubHUD))
                {
                    if (entity.Visible)
                    {
                        entity.Render();
                    }
                }
            }
            Draw.SpriteBatch.End();

            // Blend that tempRender onto sourceTarget (GameplayBuffers.Level)
            Engine.Instance.GraphicsDevice.SetRenderTarget(sourceTarget);
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, effectDisable);
            Draw.SpriteBatch.Draw(Utils_Shaders.tempRender, Vector2.Zero, Color.White);
            Draw.SpriteBatch.End();
        }
    }

    internal static class RespawnRipple
    {
        internal static bool enableShader = false; // Enabled when BeginEntityRender is called

        private const float WaveSpeed = 0.16f;
        private const float WaveStrength = 0.7f;
        private const float FadeOutTime = 2.3f;
        private const float RippleCycleTime = 2.4f;
        private const int DistanceBetweenRipple = 25;

        private static float _rippleCycleCurrentTime = 0;
        private static RenderTargetBinding[] _renderTargets = null!;
        private static Color _rippleColour;
        private static Color _outlineColour;

        // Run BeginEntityRender and EndEntityRender at the start and end of the entity render
        internal static void BeginEntityRender(Level level, Color setRippleColour, Color setOutlineColour)
        {
            enableShader = true;
            _rippleColour = setRippleColour;
            _outlineColour = setOutlineColour;

            Draw.SpriteBatch.End();
            _renderTargets = Engine.Instance.GraphicsDevice.GetRenderTargets();

            // Temporarily render on tempRender
            Engine.Instance.GraphicsDevice.SetRenderTarget(Utils_Shaders.tempRender);
            Engine.Instance.GraphicsDevice.Clear(Color.Transparent);

            Apply(level);
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, level.Camera.matrix);
        }

        internal static void EndEntityRender(Level level)
        {
            Draw.SpriteBatch.End();

            Engine.Instance.GraphicsDevice.SetRenderTargets(_renderTargets);
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, Utils_Shaders.FxRespawnRipple);
            Draw.SpriteBatch.Draw(Utils_Shaders.tempRender, Vector2.Zero, Color.White);
            Draw.SpriteBatch.End();

            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, level.Camera.Matrix);
        }

        // Called by Hook_LevelUpdate
        internal static void UpdateRipples(Level level)
        {
            if (!level.FrozenOrPaused)
            {
                _rippleCycleCurrentTime += Engine.DeltaTime;
                if (_rippleCycleCurrentTime > RippleCycleTime)
                {
                    _rippleCycleCurrentTime -= RippleCycleTime;
                }
                //Logger.Log(LogLevel.Info, "EndersExtras/Utils_Shaders", $"RespawnRipple: Cycle time is {rippleCycleCurrentTime} / {rippleCycleTime}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Apply(Level level)
        {
            Effect effect = Utils_Shaders.FxRespawnRipple!;

            // Generic Parameters
            effect.Parameters["Time"]?.SetValue(Engine.Scene.TimeActive);
            effect.Parameters["Dimensions"]?.SetValue(new Vector2(GameplayBuffers.Gameplay.Width, GameplayBuffers.Gameplay.Height));
            effect.Parameters["CamPos"]?.SetValue(level.Camera.Position);
            Viewport vp = Engine.Graphics.GraphicsDevice.Viewport;
            effect.Parameters["TransformMatrix"]?.SetValue(Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0, 1));
            effect.Parameters["ViewMatrix"]?.SetValue(Matrix.Identity);

            // Special Parameters
            Vector4 rippleColourFloat4Normalisation = new Vector4((float)_rippleColour.R / 256, (float)_rippleColour.G / 256, (float)_rippleColour.B / 256, (float)(1 - _rippleColour.A) / 256);
            Vector4 outlineColourFloat4Normalisation = new Vector4((float)_outlineColour.R / 256, (float)_outlineColour.G / 256, (float)_outlineColour.B / 256, (float)(1 - _outlineColour.A) / 256);
            //Logger.Log(LogLevel.Info, "EndersExtras/Utils_Shaders", $"RespawnRipple: Normalised Ripple float4: {rippleColourFloat4Normalisation}, Normalised outline float4: {outlineColourFloat4Normalisation}");

            //effect.Parameters["TimeTransitionRoom"]?.SetValue(Utils_General.timeSinceEnteredRoom / 60);
            effect.Parameters["WaveSpeed"]?.SetValue(WaveSpeed);
            effect.Parameters["WaveStrength"]?.SetValue(WaveStrength);
            effect.Parameters["FadeOutTime"]?.SetValue(FadeOutTime);
            effect.Parameters["RippleTime"]?.SetValue(_rippleCycleCurrentTime);
            effect.Parameters["RippleTimeMax"]?.SetValue(RippleCycleTime);
            effect.Parameters["DistanceBetweenRipple"]?.SetValue(DistanceBetweenRipple);
            effect.Parameters["RippleColour"]?.SetValue(rippleColourFloat4Normalisation);
            effect.Parameters["OutlineColour"]?.SetValue(outlineColourFloat4Normalisation);
        }
    }


    internal static class SoundEcho
    {
        internal static bool enableShader = false;
        internal static bool nextClearShader = false;
        private static Vector2 lastCameraPos = Vector2.Zero;

        // For *creating* an echo
        private static readonly List<Echo> EchoSources = new List<Echo>();
        private struct Echo
        {
            internal Vector2 originPosition;
            internal float radius;
        }

        internal static void AddEchoSource(Vector2 sourcePos, float radius)
        {
            Echo newEcho = new Echo(){originPosition = sourcePos, radius = radius};
            EchoSources.Add(newEcho);
        }

        // Note: This renders on top of the existing texture
        private const float EchoStrength = 0.6f;
        private const float ContrastThreshold = 0.7f;
        private const float FadeOutMultiplier = 0.99f;

        private static void Clear(VirtualRenderTarget sourceTarget, Level level)
        {
            if (nextClearShader)
            {
                Engine.Instance.GraphicsDevice.SetRenderTarget(Utils_Shaders.echoRender);
                Engine.Instance.GraphicsDevice.Clear(Color.Transparent);
                Engine.Instance.GraphicsDevice.SetRenderTarget(sourceTarget);
                nextClearShader = false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Apply(VirtualRenderTarget sourceTarget, Level level)
        {
            Clear(sourceTarget, level);

            Effect effect = Utils_Shaders.FxSoundEcho!;
            Effect effectTint = Utils_Shaders.FxTintColor!;
            Viewport vp = Engine.Graphics.GraphicsDevice.Viewport;
            effectTint.Parameters["Tint"]?.SetValue(new Vector4(1f, 1f, 1f, FadeOutMultiplier));
            effectTint.Parameters["TransformMatrix"]?.SetValue(Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0, 1));
            effectTint.Parameters["ViewMatrix"]?.SetValue(Matrix.Identity);

            // Generic Parameters
            effect.Parameters["Time"]?.SetValue(Engine.Scene.TimeActive);
            effect.Parameters["Dimensions"]?.SetValue(new Vector2(GameplayBuffers.Gameplay.Width, GameplayBuffers.Gameplay.Height));
            effect.Parameters["CamPos"]?.SetValue(level.Camera.Position);
            effect.Parameters["TransformMatrix"]?.SetValue(Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0, 1));
            effect.Parameters["ViewMatrix"]?.SetValue(Matrix.Identity);

            // Special Parameters
            effect.Parameters["EchoStrength"]?.SetValue(EchoStrength);
            effect.Parameters["ContrastThreshold"]?.SetValue(ContrastThreshold);

            // For simplicity, we allow only one source to take effect at the same time
            Vector3 echoData = new Vector3(0, 0, -1);

            if (EchoSources.Count > 0)
                echoData = new Vector3(SoundEcho.EchoSources[0].originPosition.X, SoundEcho.EchoSources[0].originPosition.Y, SoundEcho.EchoSources[0].radius);
            effect.Parameters["EchoData"]?.SetValue(echoData);

            // Echo render pass 1 - For everything except actors
            Engine.Instance.GraphicsDevice.SetRenderTarget(Utils_Shaders.tempRender);
            Engine.Instance.GraphicsDevice.Clear(Color.Transparent);
            if (SoundEcho.EchoSources.Count > 0)
            {   // Drawing only necessary if there are echo sources
                Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, level.Camera.Matrix);
                {
                    foreach (Entity entity in level.Entities)
                    {
                        if (entity.Visible && entity is not BackgroundTiles && entity is not Actor && !entity.TagCheck(Tags.HUD) && !entity.TagCheck(TagsExt.SubHUD) &&
                            (entity.Collidable || entity is CrystalStaticSpinner ||
                             (FrostHelperIntegration.ModInstalled && FrostHelperIntegration.CheckIfCustomSpinner(entity) )
                            ))
                        {
                            entity.Render();
                        }
                    }
                }
                Draw.SpriteBatch.End();
            }

            // Echo render pass 2 - For actors
            Engine.Instance.GraphicsDevice.SetRenderTarget(Utils_Shaders.tempRenderC);
            Engine.Instance.GraphicsDevice.Clear(Color.Transparent);
            if (SoundEcho.EchoSources.Count > 0)
            {   // Drawing only necessary if there are echo sources
                Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, level.Camera.Matrix);
                {
                    foreach (Entity entity in level.Entities)
                    {
                        if (entity.Visible && entity is Actor && !entity.TagCheck(Tags.HUD) && !entity.TagCheck(TagsExt.SubHUD) && entity.Collidable)
                        {
                            entity.Render();
                        }
                    }
                    EchoSources.RemoveAt(0); // Remove the one used
                }
                Draw.SpriteBatch.End();
            }

            Vector2 cameraDiff = lastCameraPos - level.Camera.Position;
            lastCameraPos = level.Camera.Position;

            // Blend the previous echoRender onto tempRenderB
            Engine.Instance.GraphicsDevice.SetRenderTarget(Utils_Shaders.tempRenderB);
            Engine.Instance.GraphicsDevice.Clear(Color.Transparent);
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, effectTint);
            Draw.SpriteBatch.Draw(Utils_Shaders.echoRender, cameraDiff, Color.White);
            Draw.SpriteBatch.End();

            // Blend both onto echoRender. This is stored for the future.
            Engine.Instance.GraphicsDevice.SetRenderTarget(Utils_Shaders.echoRender);
            Engine.Instance.GraphicsDevice.Clear(Color.Transparent);

            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null);
            Draw.SpriteBatch.Draw(Utils_Shaders.tempRenderB, Vector2.Zero, Color.White); // Previous echoRender
            Draw.SpriteBatch.End();
            effect.Parameters["Color"]?.SetValue(new Vector3(1, 1, 1));
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, effect);
            Draw.SpriteBatch.Draw(Utils_Shaders.tempRender, Vector2.Zero, Color.White); // New echo effects - non-actors
            Draw.SpriteBatch.End();
            effect.Parameters["Color"]?.SetValue(new Vector3(1.5f, 0.5f, 0.5f));
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, effect);
            Draw.SpriteBatch.Draw(Utils_Shaders.tempRenderC, Vector2.Zero, Color.White); // New echo effects - actors
            Draw.SpriteBatch.End();


            // Blend that echoRender onto sourceTarget (GameplayBuffers.Level)
            Engine.Instance.GraphicsDevice.SetRenderTarget(sourceTarget);
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null);
            Draw.SpriteBatch.Draw(Utils_Shaders.echoRender, Vector2.Zero, Color.White);
            Draw.SpriteBatch.End();
        }
    }
}
