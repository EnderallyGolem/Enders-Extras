using System;
using Celeste.Mod.EndersExtras.Utils;
using Celeste.Mod.Entities;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;

namespace Celeste.Mod.EndersExtras.Entities.Misc;

[CustomEntity("EndersExtras/DreamDroplet")]
[Tracked]
public class DreamDroplet : Solid
{
    #region init

    private readonly ParticleType waterParticle = new()
    {
        Source = GFX.Game["particles/bubble"],
        SourceChooser = null,
        Color = new Color(121, 129, 243, 255),
        Color2 = new Color(255, 255, 255, 0),
        ColorMode = ParticleType.ColorModes.Fade,
        FadeMode = ParticleType.FadeModes.Linear,
        SpeedMin = 20f,
        SpeedMax = 50f,
        SpeedMultiplier = 1f,
        Friction = 30f,
        Direction = 1.5707964f,
        DirectionRange = 1.3f,
        LifeMin = 0.5f,
        LifeMax = 1.3f,
        Size = 1.3f,
        SizeRange = 0.4f,
        RotationMode = ParticleType.RotationModes.Random,
        SpinMin = 0f,
        SpinMax = 0f,
        SpinFlippedChance = true,
        ScaleOut = false,
        UseActualDeltaTime = true,
    };
    private readonly ParticleType waterParticleSmall = new()
    {
        Source = GFX.Game["particles/bubble"],
        SourceChooser = null,
        Color = new Color(121, 129, 243, 255),
        Color2 = new Color(255, 255, 255, 0),
        ColorMode = ParticleType.ColorModes.Fade,
        FadeMode = ParticleType.FadeModes.Linear,
        SpeedMin = 5f,
        SpeedMax = 10f,
        SpeedMultiplier = 1f,
        Friction = 30f,
        Direction = 0,
        DirectionRange = 3.141592f,
        LifeMin = 0.5f,
        LifeMax = 1.3f,
        Size = 0.8f,
        SizeRange = 0.1f,
        RotationMode = ParticleType.RotationModes.Random,
        SpinMin = 0f,
        SpinMax = 0f,
        SpinFlippedChance = true,
        ScaleOut = false,
        UseActualDeltaTime = true,
    };

    private readonly MTexture[] particleTextures;
    private DreamBlock.DreamParticle[] dreamParticles;
    private float animTimer;

    private readonly Color colour;
    private readonly float rainbowIntensity;

    private readonly float respawnTime;
    private readonly DashCondition regainDash;
    private readonly DashCondition retainSpeed;
    private readonly bool burstOnExit;
    private readonly float semimajorDistance;

    private enum DashEffect {None, DashBurst, DashRedirect};
    private enum JumpEffect {None, Jump, Super, Hyper, Wallbounce, DefaultEffect};
    private enum DashCondition {Always, NotDash, Never};

    private readonly JumpEffect defaultEffect;
    private readonly JumpEffect defaultUpEffect;
    private readonly JumpEffect defaultUpDiagonalEffect;
    private readonly JumpEffect upKeyEffect;
    private readonly JumpEffect downKeyEffect;
    private readonly DashEffect dashEffect;
    private readonly float directionRedirectIntensity;

    private readonly float dashSpeed;
    private readonly float horizontalVelocityScale;
    private readonly float verticalVelocityScale;
    private readonly float wallbounceVelocityScale;

    //private readonly float nodeMoveTime;
    //private readonly float nodeMoveOffset;
    //private readonly Ease.Easer nodeEase;
    private readonly bool nodeMoveOneWay;
    private readonly Vector2[] nodes;
    private readonly Vector2 origPos;
    private readonly Tween? nodeTween;
    private readonly Tween? wobbleTween;
    private readonly bool gainDashInside;

    private readonly Ellipse ellipse;
    private bool playerInside = false;
    private float respawnTimeCurrent = 0;
    private bool dropletFormed = true;
    private float burstPerc = 0;

    private readonly string flagWhenDashingInside = "";

    private JumpEffect StringToJumpEffect(String str)
    {
        switch (str)
        {
            case "none": return JumpEffect.None;
            case "jump": return JumpEffect.Jump;
            case "super": return JumpEffect.Super;
            case "hyper": return JumpEffect.Hyper;
            case "wallbounce": return JumpEffect.Wallbounce;
            default: return JumpEffect.DefaultEffect;
        }
    }
    private DashEffect StringToDashEffect(String str)
    {
        switch (str)
        {
            case "dash_burst": return DashEffect.DashBurst;
            case "dash_redirect": return DashEffect.DashRedirect;
            default: return DashEffect.None;
        }
    }
    private DashCondition StringToDashCondition(String str)
    {
        switch (str)
        {
            case "always": return DashCondition.Always;
            case "not_dash": return DashCondition.NotDash;
            default: return DashCondition.Never;
        }
    }

    #endregion

    #region hooks

    internal static bool EnabledHooks { get; private set; } = false;
    internal static void EnableHooks()
    {
        if (EnabledHooks) return;
        EnabledHooks = true;
        On.Celeste.Player.DreamDashBegin += Hook_DreamDashBegin;
        On.Celeste.Player.DreamDashEnd += Hook_DreamDashEnd;
        On.Celeste.Player.DreamDashCheck += Hook_DreamDashCheck;
        On.Celeste.Player.DreamDashUpdate += Hook_DreamDashUpdate;
        IL.Celeste.Player.DreamDashUpdate += ILHook_DreamDashUpdate;
        On.Celeste.Player.Jump += Hook_Jump;
        On.Celeste.Spikes.OnCollide += Hook_SpikesOnCollide;
    }
    internal static void DisableHooks()
    {
        if (!EnabledHooks) return;
        EnabledHooks = false;
        On.Celeste.Player.DreamDashBegin -= Hook_DreamDashBegin;
        On.Celeste.Player.DreamDashEnd -= Hook_DreamDashEnd;
        On.Celeste.Player.DreamDashCheck -= Hook_DreamDashCheck;
        On.Celeste.Player.DreamDashUpdate -= Hook_DreamDashUpdate;
        IL.Celeste.Player.DreamDashUpdate -= ILHook_DreamDashUpdate;
        On.Celeste.Player.Jump -= Hook_Jump;
        On.Celeste.Spikes.OnCollide -= Hook_SpikesOnCollide;
    }

    private static DreamDroplet? _lastDropletDashed;
    private static int _playerDropletDreamDashInsideCooldown = 0;
    private static bool _overrideAllowDreamJump = false;

    private static bool _exitDreamBlockDashIsDash;
    private static void Hook_DreamDashBegin(On.Celeste.Player.orig_DreamDashBegin orig, Player player)
    {
        Vector2 oldSpeed = player.Speed;
        orig(player);
        Level level = player.SceneAs<Level>();
        bool playerInDroplet = PlayerInsideAnyDroplet(level);
        _lastDropletDashed = null;
        _overrideAllowDreamJump = false;
        _exitDreamBlockDashIsDash = false;

        if (playerInDroplet)
        {
            player.dreamDashCanEndTimer = 0;
            DreamDroplet? closestDroplet = PlayerClosestDroplet(level);
            if (closestDroplet is not null) _lastDropletDashed = closestDroplet;

            if (closestDroplet?.retainSpeed is DashCondition.Always or DashCondition.NotDash)
            {
                if (MathF.Abs(oldSpeed.X) > MathF.Abs(player.Speed.X))
                { player.Speed.X = oldSpeed.X; }
                if (MathF.Abs(oldSpeed.Y) > MathF.Abs(player.Speed.Y))
                { player.Speed.Y = oldSpeed.Y; }
            }

            player.Speed = GetDreamDashTargetSpeed(closestDroplet, player);
        }
    }

    private static Vector2 GetDreamDashTargetSpeed(DreamDroplet? droplet, Player player, float speedLerp = 1)
    {
        float targetSpeed;
        float currentSpeed = player.Speed.Length();
        if (droplet is null)
        {
            targetSpeed = 240f;
        }
        else
        {
            float minSpeed = droplet.dashSpeed;
            targetSpeed = droplet.retainSpeed != DashCondition.Never ? Math.Max(currentSpeed, minSpeed) : minSpeed;
        }

        float endSpeed = MathHelper.Lerp(currentSpeed, targetSpeed, speedLerp);
        return player.DashDir * endSpeed;
    }

    private static void Hook_DreamDashEnd(On.Celeste.Player.orig_DreamDashEnd orig, Player player)
    {
        if (_playerDropletDreamDashInsideCooldown > 0)
        {
            DreamDroplet? droplet = _lastDropletDashed;

            if (!_overrideAllowDreamJump)
            {
                player.AutoJump = false;
                player.AutoJumpTimer = 0.0f;
                player.jumpGraceTimer = 0.0f;
            }
            player.Depth = 0;
            _playerDropletDreamDashInsideCooldown = 0;
            _overrideAllowDreamJump = false;


            //Logger.Log(LogLevel.Info, "EndersExtras/DreamDroplet", $"regain dash {droplet?.regainDash.ToString()}");
            if (
                droplet?.regainDash == DashCondition.Always // Always
                || (droplet?.regainDash == DashCondition.NotDash && _exitDreamBlockDashIsDash == false) // Not dash
                )
            {
                player.RefillDash();
                player.RefillStamina();
            }

            player.TreatNaive = false;
            player.Stop(player.dreamSfxLoop);
            player.Play("event:/char/madeline/dreamblock_exit");
            Input.Rumble(RumbleStrength.Medium, RumbleLength.Short);
            return;
        }
        _lastDropletDashed = null;
        orig(player);
    }

    private static bool Hook_DreamDashCheck(On.Celeste.Player.orig_DreamDashCheck orig, Player player, Vector2 dir)
    {
        bool returnVal = orig(player, dir);
        bool playerInDroplet = PlayerInsideAnyDroplet(player.SceneAs<Level>());
        if (playerInDroplet && player.DashAttacking &&
            (dir.X == Math.Sign(player.DashDir.X) || dir.Y == Math.Sign(player.DashDir.Y)))
        { returnVal = true; }
        return returnVal;
    }

    private static int Hook_DreamDashUpdate(On.Celeste.Player.orig_DreamDashUpdate orig, Player player)
    {
        Level level = player.SceneAs<Level>();

        bool playerInDroplet = PlayerInsideAnyDroplet(level);
        if (playerInDroplet)
        {
            //Logger.Log(LogLevel.Info, "EndersExtras/DreamDroplet", $"droplet... water... <3 <3 <3 i love waterrrr");
            // Orig of all time (disabled by ilhook, only exists for other mods to use)
            orig(player);

            Input.Rumble(RumbleStrength.Light, RumbleLength.Medium);
            player.NaiveMove(player.Speed * Engine.DeltaTime);
            if (player.dreamDashCanEndTimer > 0.0) player.dreamDashCanEndTimer -= Engine.DeltaTime;
            if (level.OnInterval(0.1f)) player.CreateTrail();
            if (level.OnInterval(0.05f)) level.Displacement.AddBurst(player.Center, 0.3f, 0.0f, 30f);

            player.dreamBlock = null;
            if (player.dreamDashCanEndTimer > 0.0)
            { _playerDropletDreamDashInsideCooldown = (int)(player.dreamDashCanEndTimer / Engine.DeltaTime) + 4; }
            else _playerDropletDreamDashInsideCooldown = 3;

            // Settings & Effects

            // Update closest droplet. Burst old one if needed.
            DreamDroplet? closestDroplet = PlayerClosestDroplet(level);

            if (_lastDropletDashed is { playerInside: false, burstOnExit: true } prevBlock
                && prevBlock != closestDroplet && closestDroplet != null)
            {
                _lastDropletDashed.BurstDroplet(); // If this happens the lastDropletDashed *WILL* get replaced
            }

            if (closestDroplet is not null) _lastDropletDashed = closestDroplet;
            player.Speed = GetDreamDashTargetSpeed(closestDroplet, player, 0.1f);

            //Logger.Log(LogLevel.Info, "EndersExtras/DreamDroplet", $"what {closestDroplet} {closestDroplet is not null} {closestDroplet?.dashEffect.ToString()}");
            if (closestDroplet is not null && player.dreamDashCanEndTimer <= 0)
            {
                if (Input.Jump.Pressed) // Jump dash tech. Priority before redirect!
                {
                    int playerStateNext = closestDroplet.PerformJumpEffect(player);
                    if (playerStateNext != -1) return playerStateNext;
                }
                if (Input.Dash.Pressed || Input.CrouchDash.Pressed) // Redirect
                {
                    int playerStateNext = closestDroplet.PerformDashEffect(player);
                    if (playerStateNext != -1) return playerStateNext;
                }
                if (Input.Aim.Value != Vector2.Zero) // Superdash swimming
                {
                    closestDroplet.PerformSuperdashRedirect(player);
                }
            }

            //Logger.Log(LogLevel.Info, "EndersExtras/DreamDroplet", $"dash dir {player.DashDir}");
            return 9;
        }
        if (_playerDropletDreamDashInsideCooldown > 0) _playerDropletDreamDashInsideCooldown--;
        if (_lastDropletDashed is { playerInside: false, burstOnExit: true } )
        {
            _lastDropletDashed.BurstDroplet(); // Burst on normal reach-the-end exit
            // Do not set null here yet, as dream dash might only end next frame and DreamDashEnd needs to look at last droplet
        }
        if (_lastDropletDashed != null && _playerDropletDreamDashInsideCooldown == 0)
        {
            // Not inside a dream droplet anymore.
            // Set lastDropletDashed to null to prevent rebursts or whatever bugs.
            _lastDropletDashed = null;
        }
        return orig(player);
    }

    private static void ILHook_DreamDashUpdate(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);

        // Skip Death Wipe. And by skip I mean delete self (and return to be safe).
        ILLabel resume = cursor.DefineLabel();
        cursor.EmitDelegate<Func<bool>>(SkipDreamDashUpdate);
        cursor.Emit(OpCodes.Brfalse_S, resume);
        cursor.Emit(OpCodes.Ldc_I4, 9); // Return value of 9 (dream)
        cursor.Emit(OpCodes.Ret); // IL hook that just kills the original function, truly the il of all time
        cursor.MarkLabel(resume); // Continue as per normal

        static bool SkipDreamDashUpdate()
        {
            return PlayerInsideAnyDroplet();
        }
    }

    private static void Hook_Jump(On.Celeste.Player.orig_Jump orig, Player player, bool particles, bool playSfx)
    {
        if (_playerDropletDreamDashInsideCooldown > 0 && !_overrideAllowDreamJump) return;
        orig(player, particles, playSfx);
    }

    private static void Hook_SpikesOnCollide(On.Celeste.Spikes.orig_OnCollide orig, Spikes spikes, Player player)
    {
        bool playerInDroplet = PlayerInsideAnyDroplet(player.level);

        if (playerInDroplet && _playerDropletDreamDashInsideCooldown > 0)
        {
            // If dream dashing in droplet, do a >= check instead of > (moving perpendicularly does not kill you)
            switch (spikes.Direction)
            {
                case Spikes.Directions.Up:
                    if ((double) player.Speed.Y <= 0.0)
                        break;
                    player.Die(new Vector2(0.0f, -1f));
                    break;
                case Spikes.Directions.Down:
                    if ((double) player.Speed.Y >= 0.0)
                        break;
                    player.Die(new Vector2(0.0f, 1f));
                    break;
                case Spikes.Directions.Left:
                    if ((double) player.Speed.X <= 0.0)
                        break;
                    player.Die(new Vector2(-1f, 0.0f));
                    break;
                case Spikes.Directions.Right:
                    if ((double) player.Speed.X >= 0.0)
                        break;
                    player.Die(new Vector2(1f, 0.0f));
                    break;
            }
        }
        else
        {
            orig(spikes, player);
        }
    }


    #endregion

    #region Effect logic

    private void PerformSuperdashRedirect(Player player)
    {
        if (directionRedirectIntensity == 0) return;

        Vector2 aimNewDir = Input.Aim.Value.SafeNormalize();
        if (aimNewDir == Vector2.Zero) return;

        player.Speed = player.Speed.RotateTowards(aimNewDir.Angle(), 4.1887903f * Engine.DeltaTime * directionRedirectIntensity);
        player.DashDir = player.Speed.SafeNormalize();
        player.DashDir = player.CorrectDashPrecision(player.DashDir);
    }

    // Return value is new state value! -1 means keep.
    private int PerformJumpEffect(Player player)
    {
        if (player.DreamDashedIntoSolid()) return -1; // no lol you'll die dumb idiot

        Vector2 dashDir = player.DashDir;                           // In Increasing priority:
        JumpEffect effect = defaultEffect;                          // Default
        if (dashDir.Y < -0.38268) effect = defaultUpDiagonalEffect; // UpDiag
        if (dashDir.Y < -0.92398) effect = defaultUpEffect;         // Up
        if (Input.Aim.Value.Y > 0 && downKeyEffect != JumpEffect.DefaultEffect) effect = downKeyEffect;          // DownKey
        if (Input.Aim.Value.Y < 0 && upKeyEffect != JumpEffect.DefaultEffect) effect = upKeyEffect;            // UpKey

        if (effect == JumpEffect.DefaultEffect) effect = defaultEffect;
        Vector2 oldSpeed = player.Speed;
        switch (effect)
        {
            case JumpEffect.Jump:
                _overrideAllowDreamJump = true;
                player.dreamJump = true;
                player.Jump();
                player.Speed = AddVelocityBonus(player.Speed, player);
                BurstDroplet();
                return 0;
            case JumpEffect.Super:
                player.dashCooldownTimer = 0.2f;
                player.Ducking = false;
                player.SuperJump();
                player.Play(player.Facing == Facings.Left ?
                    "event:/char/madeline/dash_red_left" : "event:/char/madeline/dash_red_right");
                player.Speed = AddVelocityBonus(player.Speed, player);
                if (retainSpeed == DashCondition.Always) RetainFasterOldSpeedBonus(oldSpeed, player);

                BurstDroplet();
                _exitDreamBlockDashIsDash = true;
                return 0;
            case JumpEffect.Hyper:
                player.dashCooldownTimer = 0.2f;
                player.Ducking = true;
                player.SuperJump();
                player.Play(player.Facing == Facings.Left ?
                    "event:/char/madeline/dash_red_left" : "event:/char/madeline/dash_red_right");
                player.Speed = AddVelocityBonus(player.Speed, player);
                if (retainSpeed == DashCondition.Always) RetainFasterOldSpeedBonus(oldSpeed, player);
                BurstDroplet();
                _exitDreamBlockDashIsDash = true;
                return 0;
            case JumpEffect.Wallbounce:
                player.dashCooldownTimer = 0.2f;
                player.SuperWallJump(player.Facing == Facings.Right ? 1 : -1);
                // player.Play(player.Facing == Facings.Left ?
                //     "event:/char/madeline/dash_red_left" : "event:/char/madeline/dash_red_right");
                player.Speed = AddVelocityBonus(player.Speed, player);
                player.Speed.Y *= wallbounceVelocityScale;
                if (retainSpeed == DashCondition.Always) RetainFasterOldSpeedBonus(oldSpeed, player);
                BurstDroplet();
                _exitDreamBlockDashIsDash = true;
                return 0;
            default:
                return -1;
        }
    }

    private int PerformDashEffect(Player player)
    {
        DashEffect dashEffectApply = dashEffect;
        if (dashEffectApply == DashEffect.None) return -1;

        Vector2 currDashDir = player.DashDir;
        float currDashSpeed = player.Speed.Length();
        Vector2 aimNewDir = player.CorrectDashPrecision(Input.Aim.Value).SafeNormalize();
        if (aimNewDir == Vector2.Zero) aimNewDir = Vector2.UnitX * (int)player.Facing;

        if (dashEffectApply == DashEffect.DashRedirect)
        {
            if (aimNewDir == currDashDir || aimNewDir == Vector2.Zero) return -1;
            Input.Dash.ConsumeBuffer();
            player.Speed = currDashSpeed * aimNewDir;
            player.DashDir = aimNewDir;
            //Logger.Log(LogLevel.Info, "EndersExtras/DreamDroplet", $"...");
        }
        if (dashEffectApply == DashEffect.DashBurst)
        {
            // Prevent exiting in wall
            if (player.DreamDashedIntoSolid() && PlayerInsideDropletCount() <= 1) return -1;
            Input.Dash.ConsumeBuffer();
            BurstDroplet();

            if (!PlayerInsideAnyDroplet())
            {
                // Not in (another) droplet - Dash and Burst droplet
                Celeste.Freeze(0.05f);
                _exitDreamBlockDashIsDash = true;
                player.StateMachine.State = 2;
                player.Speed = AddVelocityBonus(currDashSpeed * aimNewDir, player);
                player.beforeDashSpeed = player.Speed;
                player.DashDir = aimNewDir;
                return 2;
            }
            // Still in (different) droplet - Redirect
            player.Speed = currDashSpeed * aimNewDir;
            player.DashDir = aimNewDir;
            return -1;
        }
        return -1;
    }

    private Vector2 AddVelocityBonus(Vector2 speed, Player player)
    {
        Vector2 multiplierBonus = Vector2.One;
        multiplierBonus.X *= horizontalVelocityScale;
        multiplierBonus.Y *= verticalVelocityScale;
        speed *= multiplierBonus;
        return speed;
    }
    private void RetainFasterOldSpeedBonus(Vector2 oldSpeed, Player player)
    {
        if ( (player.Speed.X > 0 && oldSpeed.X > player.Speed.X)
             || (player.Speed.X < 0 && oldSpeed.X < player.Speed.X)
           )
        { player.Speed.X = oldSpeed.X; }
        if ( (player.Speed.Y > 0 && oldSpeed.Y > player.Speed.Y)
             || (player.Speed.Y < 0 && oldSpeed.Y < player.Speed.Y)
           )
        { player.Speed.Y = oldSpeed.Y; }
    }

    #endregion

    #region entitylogic

    public DreamDroplet(EntityData data, Vector2 offset)
        : base(data.Position + offset, data.Width, data.Height, safe: true)
    {
        Depth = data.Int("Depth");
        colour = Calc.HexToColorWithAlpha(data.Attr("colour"));
        colour *= colour.A / 256f;
        rainbowIntensity = data.Float("rainbowIntensity", 1f);

        semimajorDistance = data.Float("semimajorDistance", 5f) * 8;
        bool flipFocals = data.Bool("flipFocals", false);
        burstOnExit = data.Bool("burstOnExit", true);
        respawnTime = data.Float("respawnTime", 3);
        regainDash = StringToDashCondition(data.Attr("regainDash", "always"));
        retainSpeed = StringToDashCondition(data.Attr("retainSpeed", "not_speed"));

        defaultEffect = StringToJumpEffect(data.Attr("defaultEffect", "jump"));
        defaultUpEffect = StringToJumpEffect(data.Attr("defaultUpEffect", "wallbounce"));
        defaultUpDiagonalEffect = StringToJumpEffect(data.Attr("defaultUpDiagonalEffect", "default_effect"));
        upKeyEffect = StringToJumpEffect(data.Attr("upKeyEffect", "wallbounce"));
        downKeyEffect = StringToJumpEffect(data.Attr("downKeyEffect", "hyper"));
        dashEffect = StringToDashEffect(data.Attr("dashEffect", "dash_burst"));
        directionRedirectIntensity = data.Float("directionRedirectIntensity", 0f);

        dashSpeed = data.Int("dashSpeed", 240);
        horizontalVelocityScale = data.Float("horizontalVelocityScale", 1.0f);
        verticalVelocityScale = data.Float("verticalVelocityScale", 1.0f);
        wallbounceVelocityScale = data.Float("wallbounceVelocityScale", 2f);

        float nodeMoveTime = data.Float("nodeMoveTime", 3f);
        float nodeMoveOffset = data.Float("nodeMoveOffset", 0f);
        Ease.Easer nodeEase = Utils_General.easeTypes[data.Attr("nodeEase", "SineInOut")];
        nodeMoveOneWay = data.Bool("nodeMoveOneWay", false);
        bool haveWobbleTween = data.Bool("wobble", true);

        flagWhenDashingInside = data.String("flagWhenDashingInside", "");

        gainDashInside = data.Bool("gainDashInside", true);
        nodes = data.NodesOffset(offset);

        origPos = new Vector2(Position.X, Position.Y);

        if (nodes.Length == 1)
        {
            nodeTween = Tween.Create(nodeMoveOneWay ? Tween.TweenMode.Looping : Tween.TweenMode.YoyoLooping, nodeEase, nodeMoveTime, start: true);

            float effectiveNodeMoveOffset = nodeTween.Mode == Tween.TweenMode.YoyoLooping ? nodeMoveOffset*2 : nodeMoveOffset;
            if (effectiveNodeMoveOffset >= 1)
            {
                effectiveNodeMoveOffset -= 1;
                nodeTween.Reverse = true;
            }
            nodeTween.TimeLeft = nodeTween.Duration - effectiveNodeMoveOffset * nodeMoveTime;

            Add(nodeTween);

            Vector2 endPos = nodes[0];
            nodeTween.Update();
            Position = Vector2.Lerp(origPos, endPos, nodeTween.Eased);
        }
        if (haveWobbleTween)
        {
            Calc.PushRandom((int)(data.Position.X * data.Position.Y + Width + Height + semimajorDistance));
            wobbleTween = Tween.Create(Tween.TweenMode.YoyoLooping, Ease.SineInOut, Random.Shared.NextFloat(3)+2, start: true);

            Calc.PushRandom((int)(data.Position.X * data.Position.Y + Width + Height - semimajorDistance));
            wobbleTween.TimeLeft = wobbleTween.Duration * Random.Shared.NextFloat(1);

            Calc.PushRandom((int)(data.Position.X * data.Position.Y + Width + Height - semimajorDistance));
            wobbleTween.Reverse = Random.Shared.Chance(0.5f);

            wobbleTween.Update();
            Add(wobbleTween);
            Position += Vector2.Lerp(new Vector2(0, 3), new Vector2(0, -3), wobbleTween.Eased);
        }

        particleTextures =
        [
            GFX.Game["objects/dreamblock/particles"].GetSubtexture(14, 0, 7, 7),
            GFX.Game["objects/dreamblock/particles"].GetSubtexture(7, 0, 7, 7),
            GFX.Game["objects/dreamblock/particles"].GetSubtexture(0, 0, 7, 7),
            GFX.Game["objects/dreamblock/particles"].GetSubtexture(7, 0, 7, 7)
        ];

        this.Collidable = false; // Check manually. Otherwise unwanted checks + crash due to other mods.
        this.AllowStaticMovers = false; // No.

        // Elliptical collider.
        // Focals: TopLeft, BottomRight or (w/ flipFocals) TopRight, BottomLeft of entity rect
        ellipse = !flipFocals ?
            new Ellipse(semimajorDistance, new Vector2(4, 4), new Vector2(data.Width-4, data.Height-4))
            : new Ellipse(semimajorDistance, new Vector2(data.Width-4, 4), new Vector2(4, data.Height-4));
        Collider = ellipse;

        Add(new DisplacementRenderHook(RenderDisplacement));

        waterParticle.Color = colour * 0.25f;
        waterParticleSmall.Color = colour * 0.2f;

        EnableHooks(); _playerDropletDreamDashInsideCooldown = 0;
        //Logger.Log(LogLevel.Info, "EndersExtras/DreamDroplet", $"...");
    }

    public void RenderDisplacement()
    {
        if (!dropletFormed) return;

        Level level = SceneAs<Level>();
        EllipseMask.BeginEntityRender(level, ellipse.AbsoluteFocal1Pos, ellipse.AbsoluteFocal2Pos, ellipse.semiMajorDist);
        Color displacementColor = new Color(0.5f, 0.5f, 0.3f, 1f);
        Draw.Rect(ellipse.AbsoluteLeft, ellipse.AbsoluteTop, ellipse.Width, ellipse.Height, displacementColor);
        EllipseMask.EndEntityRender(level);
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        Setup();
    }


    private bool nodeRespawnDropletNext = false;

    private void UpdateFlag()
    {
        // Check if in any droplet with this flag if dream dashing
        Level level = SceneAs<Level>();
        bool setFlagBool = false;

        if (SceneAs<Level>().Tracker.GetEntity<Player>() is {} player && player.StateMachine == 9 )
        {
            setFlagBool = PlayerInsideAnyDroplet(level, flagWhenDashingInside);
        }
        level.Session.SetFlag(flagWhenDashingInside, setFlagBool);
    }

    public override void Update()
    {
        if (flagWhenDashingInside != "") UpdateFlag(); // Update flag

        // Droplet Formation
        if (dropletFormed)
        {
            // Run player collision stuffs
            if (CheckPlayerCollision())
            {
                if (!playerInside) OnPlayerCollisionStart(); // playerInside -> true
                else OnPlayerCollisionUpdate();
            }
            else
            {
                if (playerInside) OnPlayerCollisionEnd(); // playerInside -> false
            }
        }
        else
        {
            // Respawn Droplet
            if (respawnTime > 0) // -ve means never respawn, 0 means never burst
            {
                respawnTimeCurrent += 1f / 60f;
                if (respawnTimeCurrent >= respawnTime)
                {
                    ReformDroplet();
                }
            }
        }

        if (dropletFormed && burstPerc > 0) burstPerc -= 0.1f;
        if (!dropletFormed && burstPerc < 1) burstPerc += 0.1f;
        animTimer += 4f * Engine.DeltaTime;

        // Funny node movements
        if (nodes.Length == 1 && nodeTween is not null)
        {
            Vector2 endPos = nodes[0];
            Position = Vector2.Lerp(origPos, endPos, nodeTween.Eased);

            if (nodeMoveOneWay)
            {
                if (nodeTween.TimeLeft > nodeTween.Duration - 0.7f)
                {
                    ellipse.semiMajorDist = MathHelper.Lerp(ellipse.semiMajorDist, semimajorDistance, 0.1f);
                }
                else if (nodeTween.TimeLeft > nodeTween.Duration - 0.8f)
                {
                    ellipse.semiMajorDist = semimajorDistance;
                }

                if (nodeTween.TimeLeft < 0.167f)
                {
                    BurstDroplet(forceBurst: true);
                    respawnTimeCurrent = -9999;
                    nodeRespawnDropletNext = true;
                }
                else if (nodeRespawnDropletNext)
                {
                    ReformDroplet();
                    nodeRespawnDropletNext = false;
                    ellipse.semiMajorDist = Vector2.Distance(ellipse.focal1Pos, ellipse.focal2Pos)/2 + 0.1f;
                }
            }
        }
        else if (wobbleTween is not null) { Position = origPos; }

        // Less funny wobble movements
        if (wobbleTween is not null)
        {
            Position += Vector2.Lerp(new Vector2(0, 3), new Vector2(0, -3), wobbleTween.Eased);
        }

        base.Update();
    }

    public bool CheckPlayerCollision()
    {
        Level level = SceneAs<Level>();
        if (level?.Tracker.GetEntity<Player>() is {} player) return ellipse.Collide(player.Collider);
        return false;
    }

    public void OnPlayerCollisionStart()
    {
        Level level = SceneAs<Level>();
        if (level?.Tracker.GetEntity<Player>() is {} player)
        {
            if (!PlayerInsideAnyDroplet(level))
            {
                if (player.DashAttacking)
                {
                    Audio.Play("event:/char/madeline/water_dash_in", player.Center, "deep", 0f);
                    waterParticle.SpeedMultiplier = 0.6f;
                }
                else
                {
                    Audio.Play("event:/char/madeline/water_in", player.Center, "deep", 0f);
                    waterParticle.SpeedMultiplier = 1f;
                }
                waterParticle.Direction = player.Speed.Angle() + MathF.PI;
                waterParticle.SpeedMax = Math.Min(50, player.Speed.Length());
                for (int i = 0; i < 10; i++)
                {
                    level.ParticlesBG.Emit(waterParticle, player.Center + Calc.Random.Range(Vector2.One * -8f, Vector2.One * 8f));
                }
            }
        }
        playerInside = true;
    }
    public void OnPlayerCollisionUpdate()
    {
        if (SceneAs<Level>().Tracker.GetEntity<Player>() is {} player)
        {
            if (player.DashAttacking && player.DashDir != Vector2.Zero)
            {
                StartDreamDash();
            }
            if (player.StateMachine == 9) // 9 is dream dash
            {
                waterParticle.SpeedMultiplier = 1f;
                waterParticle.Direction = player.Speed.Angle() + MathF.PI;
                SceneAs<Level>().ParticlesBG.Emit(waterParticle, player.Center + Calc.Random.Range(Vector2.One * -3f, Vector2.One * 3f));
            }
            else if (gainDashInside && dropletFormed)
            {
                // If not dream dashing and gainDashInside, refill when inside (formed) droplet
                player.RefillDash();
                player.RefillStamina();
            }
        }
    }
    public void OnPlayerCollisionEnd()
    {
        playerInside = false;
        Level level = SceneAs<Level>();
        if (level?.Tracker.GetEntity<Player>() is {} player)
        {
            if (!PlayerInsideAnyDroplet(level))
            {
                if (player.DashAttacking)
                {
                    Audio.Play("event:/char/madeline/water_dash_out", player.Center, "deep", 0f);
                    waterParticle.SpeedMultiplier = 0.6f;
                }
                else
                {
                    Audio.Play("event:/char/madeline/water_out", player.Center, "deep", 0f);
                    waterParticle.SpeedMultiplier = 1f;
                }
                waterParticle.Direction = player.Speed.Angle();
                waterParticle.SpeedMax = Math.Min(50, player.Speed.Length());
                for (int i = 0; i < 10; i++)
                {
                    level.ParticlesBG.Emit(waterParticle, player.Center + Calc.Random.Range(Vector2.One * -8f, Vector2.One * 8f));
                }
            }
        }
    }

    private void StartDreamDash()
    {
        //Level level = SceneAs<Level>();
        if (SceneAs<Level>().Tracker.GetEntity<Player>() is {} player)
        {
            player.StateMachine.State = 9;
        }
    }

    private void BurstDroplet(bool forceBurst = false)
    {
        if (respawnTime == 0 && !forceBurst) return; // No bursting!
        if (!dropletFormed) return; // Already burst.

        if (wobbleTween is not null) wobbleTween.Active = false; // Pause Oscillation

        respawnTimeCurrent = 0;
        dropletFormed = false;
        if (playerInside) OnPlayerCollisionEnd();

        Level level = SceneAs<Level>();
        if (level is null) return;
        Vector2 camPos = level.Camera.Position;
        foreach (DreamBlock.DreamParticle dpart in dreamParticles)
        {
            if (dpart.Layer <= 1) continue;
            Vector2 particlePos = PutInside(dpart.Position + camPos * (float) (0.3 + 0.25 * dpart.Layer), 10);
            if (ellipse.IsInside(particlePos, buffer: 10))
            {
                level.ParticlesBG.Emit(waterParticleSmall, particlePos);
            }
        }
        EventInstance dropletBoosterBreakSfx = Audio.Play("event:/game/04_cliffside/greenbooster_end", ellipse.AbsoluteCenterPos);
        dropletBoosterBreakSfx.setPitch(1.5f);
        EventInstance dropletBoosterBreakSfx2 = Audio.Play("event:/game/general/assist_nonsolid_out", ellipse.AbsoluteCenterPos);
        dropletBoosterBreakSfx2.setPitch(0.8f + Random.Shared.NextFloat(0.3f));
    }
    private void ReformDroplet()
    {
        if (dropletFormed) return; // Already formed.
        dropletFormed = true;

        if (wobbleTween is not null) wobbleTween.Active = true; // Resume Oscillation

        Level level = SceneAs<Level>();
        Vector2 camPos = level.Camera.Position;
        foreach (DreamBlock.DreamParticle dpart in dreamParticles)
        {
            if (dpart.Layer >= 2) continue;
            Vector2 particlePos = PutInside(dpart.Position + camPos * (float) (0.3 + 0.25 * dpart.Layer), 16);
            if (ellipse.IsInside(particlePos, buffer: 16))
            {
                level.ParticlesBG.Emit(waterParticleSmall, particlePos);
            }
        }

        Audio.Play("event:/char/madeline/water_in", ellipse.AbsoluteCenterPos, "deep", 0f);
        EventInstance dropletBoosterRespawnSfx = Audio.Play("event:/game/05_mirror_temple/redbooster_reappear", ellipse.AbsoluteCenterPos);
        dropletBoosterRespawnSfx.setPitch(0.7f);
    }

    public static bool PlayerInsideAnyDroplet(Level? level = null, String? flagRequired = null)
    {
        level ??= Engine.Scene as Level;
        if (level?.Tracker.GetEntity<Player>() is null) return false;
        foreach (var dreamDropletVar in level.Tracker.GetEntities<DreamDroplet>())
        {
            DreamDroplet droplet = (DreamDroplet) dreamDropletVar;

            if (droplet.playerInside && droplet.dropletFormed)
            {
                if (flagRequired is null || flagRequired == droplet.flagWhenDashingInside) return true;
            }
        }
        return false;
    }
    public static int PlayerInsideDropletCount(Level? level = null)
    {
        level ??= Engine.Scene as Level;
        if (level?.Tracker.GetEntity<Player>() is null) return 0;
        int returnCount = 0;
        foreach (var dreamDropletVar in level.Tracker.GetEntities<DreamDroplet>())
        {
            DreamDroplet droplet = (DreamDroplet) dreamDropletVar;
            if (droplet.playerInside && droplet.dropletFormed) returnCount++;
        }
        return returnCount;
    }

    /// <summary>
    /// Returns the droplet closest to the player.
    /// "Closest" is calculated by a mix of focal sum distance (subtracted by minimum val) and distance from center.
    /// </summary>
    /// <param name="level"></param>
    /// <returns></returns>
    public static DreamDroplet? PlayerClosestDroplet(Level? level = null)
    {
        level ??= Engine.Scene as Level;

        // Priority: Previous Inside -> Nearest Droplet Inside -> Previous -> Random -> Null
        Player? player = level?.Tracker.GetEntity<Player>();
        DreamDroplet? closestDroplet = _lastDropletDashed; // Priority 3: Previous

        // Priority 1: Player is still inside the previous droplet
        if (_lastDropletDashed is not null && _lastDropletDashed.playerInside)
        {
            return closestDroplet;
        }

        closestDroplet ??= level?.Tracker.GetEntity<DreamDroplet>(); // Priority 4: Random

        if (player is null || level is null || closestDroplet is null) return closestDroplet;

        // Priority 2: Search for closest droplet
        float closestDropletDistance = 999999f;
        foreach (var dreamDropletVar in level.Tracker.GetEntities<DreamDroplet>())
        {
            DreamDroplet droplet = (DreamDroplet) dreamDropletVar;
            if (!droplet.playerInside) continue;

            float dropletDistance = DistanceCalculator(droplet.ellipse, player.Center);
            if (dropletDistance < closestDropletDistance)
            {
                closestDroplet = droplet;
            }
        }
        return closestDroplet;

        float DistanceCalculator(Ellipse ellipseCalc, Vector2 playerPos)
        {
            float distFocal = ellipseCalc.FocalDistanceSum(playerPos);
            float distCircle = Vector2.Distance(ellipseCalc.AbsoluteCenterPos, playerPos);
            float distFocalMin = Vector2.Distance(ellipseCalc.focal1Pos, ellipseCalc.focal2Pos); // Min possible focal sum * 2
            return distFocal + distCircle - distFocalMin;
        }
    }


    public void Setup()
    {
        //Dream particles
        dreamParticles = new DreamBlock.DreamParticle[(int) (Width / 8.0 * (Height / 8.0) * 0.7)];
        for (int index = 0; index < dreamParticles.Length; ++index)
        {
            dreamParticles[index].Position = new Vector2(Calc.Random.NextFloat(Width), Calc.Random.NextFloat(Height));
            dreamParticles[index].Layer = Calc.Random.Choose(0, 1, 1, 2, 2, 2);
            dreamParticles[index].TimeOffset = Calc.Random.NextFloat();
            dreamParticles[index].Color = Color.LightGray * (float) (0.5 + dreamParticles[index].Layer / 2.0 * 0.5);
            switch (dreamParticles[index].Layer)
            {
                case 0:
                    dreamParticles[index].Color = Calc.Random.Choose(Calc.HexToColor("FFEF11"), Calc.HexToColor("FF00D0"), Calc.HexToColor("08a310"));
                    continue;
                case 1:
                    dreamParticles[index].Color = Calc.Random.Choose(Calc.HexToColor("5fcde4"), Calc.HexToColor("7fb25e"), Calc.HexToColor("E0564C"));
                    continue;
                case 2:
                    dreamParticles[index].Color = Calc.Random.Choose(Calc.HexToColor("5b6ee1"), Calc.HexToColor("CC3B3B"), Calc.HexToColor("7daa64"));
                    continue;
                default:
                    continue;
            }
        }
    }
    public override void Render()
    {
        if (respawnTime < 0 && burstPerc >= 1) return;

        Level level = SceneAs<Level>();

        RenderParticles(1-burstPerc); // Before the shader, since the shader umm makes the inside super transparent

        // This rectangle is the ellipse. The shader takes care of filtering the outside and the visuals!
        DreamDropletBubble.BeginEntityRender(level, ellipse.AbsoluteFocal1Pos, ellipse.AbsoluteFocal2Pos, ellipse.semiMajorDist, rainbowIntensity, burstPerc);
        Draw.Rect(ellipse.AbsoluteLeft-3, ellipse.AbsoluteTop-3, ellipse.Width+3, ellipse.Height+3, colour);
        DreamDropletBubble.EndEntityRender(level);
    }

    private void RenderParticles(float opacity)
    {
        Level level = SceneAs<Level>();
        Camera camera = level.Camera;
        if (Right < (double) camera.Left || Left > (double) camera.Right || Bottom < (double) camera.Top || Top > (double) camera.Bottom)
            return;

        Vector2 camPos = level.Camera.Position;
        for (int index = 0; index < dreamParticles.Length; ++index)
        {
            int layer = dreamParticles[index].Layer;
            Vector2 particlePos = PutInside(dreamParticles[index].Position + camPos * (float) (0.3 + 0.25 * layer));
            Color color = dreamParticles[index].Color * opacity;
            MTexture? particleTexture;
            switch (layer)
            {
                case 0:
                    particleTexture = particleTextures[3 - (int) ((dreamParticles[index].TimeOffset * 4.0 + animTimer) % 4.0)];
                    break;
                case 1:
                    particleTexture = particleTextures[1 + (int) ((dreamParticles[index].TimeOffset * 2.0 + animTimer) % 2.0)];
                    break;
                default:
                    particleTexture = particleTextures[2];
                    break;
            }
            if (ellipse.IsInside(particlePos, buffer: -3)) particleTexture.DrawCentered(particlePos, color*0.7f);
        }
    }

    private Vector2 PutInside(Vector2 pos, float buffer = 0)
    {
        if (pos.X > (double) Right + buffer)
            pos.X -= (float) Math.Ceiling((pos.X - (double) Right) / Width) * Width;
        else if (pos.X < (double) Left - buffer)
            pos.X += (float) Math.Ceiling((Left - (double) pos.X) / Width) * Width;
        if (pos.Y > (double) Bottom + buffer)
            pos.Y -= (float) Math.Ceiling((pos.Y - (double) Bottom) / Height) * Height;
        else if (pos.Y < (double) Top - buffer)
            pos.Y += (float) Math.Ceiling((Top - (double) pos.Y) / Height) * Height;
        return pos;
    }
    #endregion
}

#region ellipse

/// <summary>
/// Custom collider. Actual position doesn't really matter, as long as focal1Pos and focal2Pos is relative to actual position.
///
/// WARNING: Any collide with ellipse is NOT implemented (including ellipse with ellipse).
/// Probably not that hard to implement with a On.Monocle.Collider.Collide_Collider hook and iteration for ellipse on ellipse.
/// But not necessary right now.
/// </summary>
public class Ellipse : Collider
{
    // Offsets are from center
    public float semiMajorDist;
    public readonly Vector2 focal1Pos;
    public readonly Vector2 focal2Pos;
    public Vector2 centerPos => 0.5f * (focal1Pos + focal2Pos);


    public Vector2 AbsoluteFocal1Pos => focal1Pos + Entity.Position;
    public Vector2 AbsoluteFocal2Pos => focal2Pos + Entity.Position;
    public Vector2 AbsoluteCenterPos => centerPos + Entity.Position;

    private readonly float halfWidth;
    private readonly float halfHeight;

    public Ellipse(float semiMajorDist, Vector2 focal1Pos, Vector2 focal2Pos, Vector2? offset = null)
    {
        this.semiMajorDist = semiMajorDist;
        if (offset is null) offset = Vector2.Zero;
        this.Position = offset.Value;
        this.focal1Pos = focal1Pos;
        this.focal2Pos = focal2Pos;

        // Ensure reasonable semi major dist
        if (semiMajorDist < Vector2.Distance(focal1Pos, focal2Pos)/2 + 0.1f)
        {
            this.semiMajorDist = Vector2.Distance(focal1Pos, focal2Pos)/2 + 0.1f;
        }

        // Calculate half width / half height by just freaking iterating
        // Go direction of towards one focal, prioritise X/Y differently
        Vector2 xSearch = centerPos; Vector2 ySearch = centerPos;
        Vector2 xSearchDir = new Vector2(focal1Pos.X - centerPos.X, 0).SafeNormalize(Vector2.UnitX);
        Vector2 ySearchDir = new Vector2(0, focal1Pos.Y - centerPos.Y).SafeNormalize(Vector2.UnitY);

        while (true)
        {
            if (IsInside(xSearch + xSearchDir, true)) { xSearch += xSearchDir; continue; }
            if (IsInside(xSearch + ySearchDir, true)) { xSearch += ySearchDir; continue; }
            halfWidth = MathF.Abs(xSearch.X - centerPos.X) + 0.5f; break;
        }
        while (true)
        {
            if (IsInside(ySearch + ySearchDir, true)) { ySearch += ySearchDir; continue; }
            if (IsInside(ySearch + xSearchDir, true)) { ySearch += xSearchDir; continue; }
            halfHeight = MathF.Abs(ySearch.Y  - centerPos.Y) + 0.5f; break;
        }
    }


    public override bool Collide(Vector2 point)
    {
        return IsInside(point, false);
    }

    public override bool Collide(Vector2 from, Vector2 to)
    {
        // Iterate from -> to
        while (Vector2.DistanceSquared(from, to) >= 0.5f)
        {
            float fromSum = FocalDistanceSum(from);
            float toSum = FocalDistanceSum(to);

            if (fromSum < 2*semiMajorDist || toSum < 2*semiMajorDist) return true; // Inside!

            bool fromCloser = fromSum < toSum;
            Vector2 midpoint = 0.5f * (from + to);
            if (fromCloser) to = midpoint;
            else from = midpoint;
        }
        return false;
    }

    public override bool Collide(Rectangle rect)
    {
        // Fast exit
        if (rect.Right < this.AbsoluteLeft) return false;
        if (rect.Left > this.AbsoluteRight) return false;
        if (rect.Bottom < this.AbsoluteTop) return false;
        if (rect.Top > this.AbsoluteBottom) return false;

        // 1. Rect Center inside
        if (Collide(Utils_General.ToVector2(rect.Center))) return true;
        if (rect.Contains(centerPos.ToPoint())) return true; // Ellipse is inside rectangle

        // 2. Rect Corners inside
        Vector2 topLeft = new Vector2(rect.Left, rect.Top);
        Vector2 topRight = new Vector2(rect.Right, rect.Top);
        Vector2 bottomLeft = new Vector2(rect.Left, rect.Bottom);
        Vector2 bottomRight = new Vector2(rect.Right, rect.Bottom);

        if (Collide(topLeft)) return true;
        if (Collide(topRight)) return true;
        if (Collide(bottomLeft)) return true;
        if (Collide(bottomRight)) return true;

        // 3. Ellipse's major axis intersects Rectangle

        // 4. Rect's closest 2 sides intersects Ellipse
        if (rect.Left < AbsoluteLeft)
        {
            // Right side closer
            if (Collide(topRight, bottomRight)) return true;
        }
        else
        {
            if (Collide(topLeft, bottomLeft)) return true;
        }

        if (rect.Top < AbsoluteTop)
        {
            // Bottom side closer
            if (Collide(bottomLeft, bottomRight)) return true;
        } else
        {
            if (Collide(topLeft, topRight)) return true;
        }

        return false;
    }


    public override bool Collide(Circle circle)
    {
        // Fast exit
        if (Vector2.Distance(circle.Center, centerPos) > 2 * semiMajorDist + circle.Radius) return false;

        if (Collide(circle.Center)) return true;
        if (circle.Collide(this.Center)) return true; // If circle is inside ellipse

        // Iterate angle
        float searchAngle = 0;
        float searchAngleRange = MathF.PI * 2;

        while (searchAngleRange * circle.Radius >= 0.5f && searchAngleRange > 0) // While search arc length >= 0.5
        {

            Vector2 searchPos = circle.Center + new Vector2( MathF.Cos(searchAngle), MathF.Sin(searchAngle) ) * circle.Radius;
            if (IsInside(searchPos)) return true;

            searchAngleRange /= 1.8f;
            float firstSearchAngle = searchAngle + searchAngleRange;
            float secondSearchAngle = searchAngle - searchAngleRange;
            Vector2 searchPos1 = circle.Center + new Vector2( MathF.Cos(firstSearchAngle), MathF.Sin(firstSearchAngle) );
            Vector2 searchPos2 = circle.Center + new Vector2( MathF.Cos(secondSearchAngle), MathF.Sin(secondSearchAngle) );

            float searchPos1Sum = FocalDistanceSum(searchPos1);
            float searchPos2Sum = FocalDistanceSum(searchPos2);
            //Logger.Log(LogLevel.Info, "EndersExtras/DreamDroplet", $"search angle {searchAngle} -- {searchAngleRange} --- {searchPos} | dist {searchPos1}: {searchPos1Sum} {searchPos2}: {searchPos2Sum} | vs 2*semimajor {2*semiMajorDist}");
            if (searchPos1Sum < 2*semiMajorDist || searchPos2Sum < 2*semiMajorDist) return true; // Inside!

            searchAngle = searchPos1Sum < searchPos2Sum ? firstSearchAngle : secondSearchAngle;
        }

        return false;
    }

    public override bool Collide(Hitbox hitbox) => this.Collide(hitbox.Bounds);
    public override bool Collide(Grid grid) => this.Collide(grid.Bounds);
    public override bool Collide(ColliderList list)
    {
        foreach (Collider collider in list.colliders)
        {
            bool collided = this.Collide(collider);
            if (collided) return true;
        }
        return false;
    }

    public override Collider Clone()
    {
        return new Ellipse( semiMajorDist, focal1Pos, focal2Pos, Position);
    }

    public override void Render(Camera camera, Color color)
    {
        DrawShape(color, 0f);
    }

    /// <summary>
    /// Draws an ellipse. Thickness is done by overlapping circles so transparency won't work!
    /// </summary>
    /// <param name="color">Colour of ellipse</param>
    /// <param name="thickness">Thickness of circles used to make the ellipse. Set to 0 for pixel width</param>
    /// <param name="buffer">Modification to the size of the ellipse. Negative = smaller, Positive = larger</param>
    public void DrawShape(Color color, float thickness = 0, float buffer = 0)
    {
        // Locate point on semicircle directly above/below midpoint
        Vector2 pen = centerPos;
        while (IsInside(pen, true, buffer: buffer)) pen -= Vector2.UnitY;
        pen += Vector2.UnitY; // Should match top of rectangle
        Vector2 origPen = new Vector2(pen.X, pen.Y);

        Vector2 searchDir = -Vector2.UnitY;
        // Search searchDir, then searchDir perp. If neither, rotate both by 90 deg
        for (int i = 0; i < 4 * (Width + Height); i++)
        {
            if (pen == origPen && i>1) break; // Reach back orig pos

            Vector2 searchDir2 = searchDir.Perpendicular();
            if (thickness == 0)
            {
                Draw.Point(pen + AbsolutePosition, color);
            }
            else
            {
                Draw.Circle(pen + AbsolutePosition, thickness, color, (int)thickness/4+1);
            }

            if (IsInside(pen + searchDir, true, buffer: buffer))
            {
                pen += searchDir;
            }
            else if (IsInside(pen + searchDir2, true, buffer: buffer))
            {
                pen += searchDir2;
            }
            else
            {
                searchDir = searchDir2;
            }
        }
    }

    public override float Width
    {
        get => halfWidth * 2;
        set => throw new NotImplementedException();
    }

    public override float Height
    {
        get => halfHeight * 2;
        set => throw new NotImplementedException();
    }
    public override float Top
    {
        get => centerPos.Y - halfHeight + this.Position.Y + 0.5f;
        set => throw new NotImplementedException();
    }
    public override float Bottom
    {
        get => centerPos.Y + halfHeight + this.Position.Y + 0.5f;
        set => throw new NotImplementedException();
    }
    public override float Left
    {
        get => centerPos.X - halfWidth + this.Position.X + 0.5f;
        set => throw new NotImplementedException();
    }
    public override float Right
    {
        get => centerPos.X + halfWidth + this.Position.X + 0.5f;
        set => throw new NotImplementedException();
    }


    /// <summary>
    /// Find the sum of distances of a point to the 2 focal points.
    /// </summary>
    public float FocalDistanceSum(Vector2 pos, bool relative = false)
    {
        float focal1Dist, focal2Dist;
        if (relative)
        {
            focal1Dist = Vector2.Distance(focal1Pos, pos);
            focal2Dist = Vector2.Distance(focal2Pos, pos);
        }
        else
        {
            focal1Dist = Vector2.Distance(focal1Pos + AbsolutePosition, pos);
            focal2Dist = Vector2.Distance(focal2Pos + AbsolutePosition, pos);
        }
        return focal1Dist + focal2Dist;
    }
    public float FocalDistanceSum(Point pos, bool relative = false)
    {
        return FocalDistanceSum(new Vector2(pos.X, pos.Y), relative);
    }
    /// <summary>
    /// If a Vector2 point is within an ellipse.
    /// </summary>
    /// <param name="point">Point to check</param>
    /// <param name="relative">If using coordinates relative to entity position (instead of world position)</param>
    /// <param name="buffer">Buffer pixels to count as inside. Positive = Larger region, Negative = Smaller region.</param>
    /// <returns></returns>
    public bool IsInside(Vector2 point, bool relative = false, float buffer = 0)
    {
        return FocalDistanceSum(point, relative) - buffer <= 2*semiMajorDist;
    }
}
#endregion