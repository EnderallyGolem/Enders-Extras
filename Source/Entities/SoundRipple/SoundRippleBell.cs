using System;
using System.Collections;
using Celeste.Mod.EndersExtras.Utils;
using Celeste.Mod.Entities;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;



namespace Celeste.Mod.EndersExtras.Entities.SoundRipple;
[Tracked(true)]
[CustomEntity("EndersExtras/SoundRippleBell")]
public class SoundRippleBell : Entity
{
    private readonly MTexture bellTexture;
    private readonly Image image;
    private readonly SoundSource bellSFX;
    private readonly float revealRadius;
    private readonly float pitchScale;
    private readonly float pitchVariation;

    private readonly SineWave rotSineWave;
    private float rotAmplitude;

    private readonly bool onlyPlayerRing;
    private readonly float cooldown;

    private float cooldownTimer;
    private readonly String soundEvent;

    private const int height = 28;
    private const int width = 26;

    private const int RangeSmall = 6;
    private const int RangeMedium = 14;
    private const int RangeLarge = 24;

    public SoundRippleBell(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        // Enable shader
        SoundEcho.enableShader = true;

        // Clear shader on entity load
        SoundEcho.nextClearShader = true;

        Depth = data.Int("depth", 10000);

        var type1 = data.String("type", "medium");
        onlyPlayerRing = data.Bool("onlyPlayerRing", false);
        cooldown = data.Float("cooldown", 0.3f);
        revealRadius = data.Float("radius", 0f) * 8;
        pitchScale = data.Float("pitchScale", 1);
        pitchVariation = data.Float("pitchVariation", 0f);

        switch (type1)
        {
            case "small":
                bellTexture = GFX.Game["objects/EndersExtras/SoundRipple/soundripplebell_bronze"];
                soundEvent = "event:/Custom/EndersExtras/bell_small";
                if (revealRadius <= 0) revealRadius = 8 * RangeSmall;
                break;
            case "medium":
                bellTexture = GFX.Game["objects/EndersExtras/SoundRipple/soundripplebell_silver"];
                soundEvent = "event:/Custom/EndersExtras/bell_medium";
                if (revealRadius <= 0) revealRadius = 8 * RangeMedium;
                break;
            case "large":
                bellTexture = GFX.Game["objects/EndersExtras/SoundRipple/soundripplebell_gold"];
                soundEvent = "event:/Custom/EndersExtras/bell_large";
                if (revealRadius <= 0) revealRadius = 8 * RangeLarge;
                break;
        }

        Add(image = new Image(bellTexture));
        image.CenterOrigin();

        Add( rotSineWave = new SineWave(frequency: 0.8f, MathF.PI / 2));

        Collider = new Hitbox(width, height, -width / 2, -height / 2);

        cooldownTimer = 2 / 60f;
    }

    private bool beginRemoveSelf = false;
    public override void Update()
    {
        Level level = SceneAs<Level>();
        //Logger.Log(LogLevel.Info, "EndersExtras/SoundRippleBell", $"sinewave amount {rotSineWave.Value}");

        base.Update();

        // For negative cooldown, immediately OnCollide if cooldownTimer = 0
        if (cooldown < 0 && cooldownTimer <= 0)
        {
            Random dirRng = new Random();
            OnCollide(dirRng.NextSingle() > 0.5, null);
        }


        // Check for collisions
        if (cooldownTimer > 0)
        {
            cooldownTimer -= 1 / 60f;
        }

        // ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
        foreach (Actor actor in level.Tracker.GetEntities<Actor>())
        {
            if (Collider.Collide(actor))
            {
                if (onlyPlayerRing && actor is not Player) continue;

                //Logger.Log(LogLevel.Info, "EndersExtras/SoundRippleBell", $"collided with {actor}");

                OnCollide(actor.Center.X < this.CenterX, actor);
                break;
            }
        }


        if (rotAmplitude > 0)
        {
            rotAmplitude -= (float)(Math.PI / (60 * 1.5 * 2));
            image.Rotation = (90*image.Rotation + 10*rotSineWave.Value * rotAmplitude) * 0.01f;
        }
        else image.Rotation = 0;

        if (beginRemoveSelf)
        {
            image.Color *= 0.98f;
            if (image.Color.A < 0.01f) this.RemoveSelf();
        }
    }

    private void OnCollide(bool swingRight, Actor? collidingActor)
    {
        if (cooldownTimer <= 0)
        {
            // Ring bell
            CollideEffects(swingRight);
        }
        else if (cooldownTimer <= 2/60f)
        {
            // If it's a player, only prevent retriggering if they're not moving
            if (collidingActor is Player player && player.PreviousPosition != player.Position)
            {
                return;
            }

            // Prevent constant retriggering
            // Set it slightly above cooldown so the entity doesn't retrigger if in the bell
            cooldownTimer = 2 / 60f;
        }
    }

    private void CollideEffects(bool swingRight)
    {
        SoundEcho.enableShader = true; // Not enabled properly when loading state, lonn refresh, etc

        // Swing bell
        rotAmplitude = (float)Math.PI/2 * 5/4;
        if (!swingRight) rotSineWave.StartUp(); else rotSineWave.StartDown();

        // Ask the shader to add an echo
        SoundEcho.AddEchoSource(this.Center, revealRadius);
        //Logger.Log(LogLevel.Info, "EndersExtras/SoundRippleBell", $"Create echo at ({this.Center})");

        EventInstance bell = Audio.Play(soundEvent, Position);
        float camXPos = 0.5f * (SceneAs<Level>().Camera.Right + SceneAs<Level>().Camera.Left);
        float camYPos = 0.5f * (SceneAs<Level>().Camera.Top + SceneAs<Level>().Camera.Bottom);
        float cameraBellDistance = Vector2.Distance(Position, new Vector2(camXPos, camYPos));

        float volumeScale = (float)Math.Clamp(2 - cameraBellDistance * 0.125 * 1 / 22, 0, 1);

        Random rng = new Random();
        float pitchScaleFinal = 1 - ((2*rng.NextFloat()-1) * pitchVariation);

        bell.setVolume(volumeScale);
        bell.setPitch(pitchScaleFinal * pitchScale);

        SceneAs<Level>().Flash(Color.White*0.02f*volumeScale, false);
        SceneAs<Level>().Displacement.AddBurst(this.Center, 0.4f, 12f, 8*RangeSmall, 0.4f);
        if (revealRadius >= 8*RangeMedium) SceneAs<Level>().Displacement.AddBurst(this.Center, 0.5f, 12f, 8*RangeMedium, 0.5f);
        if (revealRadius >= 8*RangeLarge) SceneAs<Level>().Displacement.AddBurst(this.Center, 0.6f, 12f, 8*RangeLarge, 0.6f);

        // Reset cooldown
        if (cooldown != 0)
        {
            // +ve / -ve cooldown: Reset as normal
            cooldownTimer = Math.Abs(cooldown);
        }
        else
        {
            // 0 cooldown: Remove the bell
            cooldownTimer = 99999;
            beginRemoveSelf = true;
        }

        // If player within detection radius, add component
        if (SceneAs<Level>().Tracker.GetEntity<Player>() is { } player)
        {
            if (Vector2.Distance(player.Center, this.Center) <= revealRadius)
            {
                if (player.Components.Get<SoundRippleDetected>() is { } soundRippleComponent)
                    soundRippleComponent.ResetCountdown();
                else player.Add(new SoundRippleDetected());
            }
        }
    }


    public class SoundRippleDetected() : Component (true, true)
    {
        private const int CountdownVal = 30;

        private int detectCountdown = CountdownVal;

        public override void Update()
        {
            base.Update();

            detectCountdown--;
            if (detectCountdown <= 0) RemoveSelf();
        }

        internal void ResetCountdown()
        {
            detectCountdown = CountdownVal;
        }
    }
}