using Celeste.Mod.EndersExtras.Integration;
using Celeste.Mod.EndersExtras.Triggers.DeathHandler;
using Celeste.Mod.EndersExtras.Utils;
using Celeste.Mod.EndHelper.Utils;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Celeste.Mod.EndersExtras.Entities.DeathHandler;

[CustomEntity("EndersExtras/DeathHandlerBypassZone")]
public class DeathHandlerBypassZone : Entity
{
    float altVisibility = 0;

    internal readonly MTexture glassImage;
    internal readonly MTexture glassAltImage;
    internal readonly MTexture borderTexture;
    internal readonly EntityID entityID;

    internal enum BypassEffect { Activate, Deactivate, Toggle, None }
    private readonly BypassEffect mainEffect;
    private readonly BypassEffect altEffect;
    internal readonly string altFlag;
    internal readonly bool attachable;
    internal readonly string bypassFlag;
    internal readonly bool affectPlayer;

    internal readonly float width;
    internal readonly float height;

    internal BypassEffect currentEffect = BypassEffect.None;
    internal List<Vector2> particles = new List<Vector2>();
    public float[] particleSpeeds = new float[3] { 6f, 12f, 20f };

    List<Entity> entitiesInsideZone = new List<Entity>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public DeathHandlerBypassZone(EntityData data, Vector2 offset, EntityID id)
        : base(data.Position + offset)
    {
        Utils_DeathHandlerEntities.EnableDeathHandler();

        Depth = 9500;
        entityID = id;

        mainEffect = ConvertToBypassEffect(data.Attr("effect"));
        altEffect = ConvertToBypassEffect(data.Attr("altEffect"));
        altFlag = data.Attr("altFlag", "");

        attachable = data.Bool("attachable", true);
        bypassFlag = data.Attr("bypassFlag", "");
        affectPlayer = data.Bool("affectPlayer", true);

        String glassActivateTexture = Utils_General.TrimPath
            (data.Attr("glassActivateTexture"), "Graphics/Atlases/Gameplay/objects/EndersExtras/DeathHandlerBypassZone/stainedglass_activate");
        String glassDeactivateTexture = Utils_General.TrimPath
            (data.Attr("glassActivateTexture"), "Graphics/Atlases/Gameplay/objects/EndersExtras/DeathHandlerBypassZone/stainedglass_deactivate");
        String glassToggleTexture = Utils_General.TrimPath
            (data.Attr("glassToggleTexture"), "Graphics/Atlases/Gameplay/objects/EndersExtras/DeathHandlerBypassZone/stainedglass_toggle");
        String glassNoneTexture = Utils_General.TrimPath
            (data.Attr("glassNoneTexture"), "Graphics/Atlases/Gameplay/objects/EndersExtras/DeathHandlerBypassZone/stainedglass_none");

        switch (mainEffect)
        {
            case BypassEffect.Activate:
                glassImage = GFX.Game[glassActivateTexture];
                break;
            case BypassEffect.Deactivate:
                glassImage = GFX.Game[glassDeactivateTexture];
                break;
            case BypassEffect.Toggle:
                glassImage = GFX.Game[glassToggleTexture];
                break;
            default:
                glassImage = GFX.Game[glassNoneTexture];
                break;
        }
        switch (altEffect)
        {
            case BypassEffect.Activate:
                glassAltImage = GFX.Game[glassActivateTexture];
                break;
            case BypassEffect.Deactivate:
                glassAltImage = GFX.Game[glassDeactivateTexture];
                break;
            case BypassEffect.Toggle:
                glassAltImage = GFX.Game[glassToggleTexture];
                break;
            default:
                glassAltImage = GFX.Game[glassNoneTexture];
                break;
        }

        String borderPlayerTexture = Utils_General.TrimPath
            (data.Attr("borderPlayerTexture"), "Graphics/Atlases/Gameplay/objects/EndersExtras/DeathHandlerBypassZone/stainedglass_border_player");


        String borderEntityTexture = Utils_General.TrimPath(
            data.Attr("borderEntityTexture"), "Graphics/Atlases/Gameplay/objects/EndersExtras/DeathHandlerBypassZone/stainedglass_border_entity"
        );
        borderTexture = affectPlayer ? GFX.Game[borderPlayerTexture] : GFX.Game[borderEntityTexture];

        Visible = data.Bool("visible", true);

        width = data.Width;
        height = data.Height;
        base.Collider = new Hitbox(x: 1, y: 1, width: width - 2, height: height - 2);

        // Static Mover stuff
        if (attachable)
        {
            Add(new StaticMover
            {
                OnShake = OnShake,
                SolidChecker = IsRidingSolid
            });
        }

        GoldenRipple.enableShader = true;
    }

    private void OnShake(Vector2 shakePos)
    {
        Position += shakePos;
    }

    private bool IsRidingSolid(Solid solid)
    {
        Collider origCollider = base.Collider;
        base.Collider = new Hitbox(Width + 4, Height + 4, -1, -1);
        bool collideCheck = CollideCheck(solid);
        base.Collider = origCollider;
        return collideCheck;
    }

    private static BypassEffect ConvertToBypassEffect(String effectString)
    {
        switch (effectString)
        {
            case "Activate": return BypassEffect.Activate;
            case "Deactivate": return BypassEffect.Deactivate;
            case "Toggle": return BypassEffect.Toggle;
            case "None": return BypassEffect.None;
            default: return BypassEffect.None;
        }
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
    }
    public override void Awake(Scene scene)
    {
        UpdateCurrentEffect();

        bool useAlt = Utils_General.AreFlagsEnabled(SceneAs<Level>().Session, altFlag, false);
        if (useAlt) altVisibility = 1f;

        base.Awake(scene);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override void Update()
    {
        UpdateCurrentEffect();
        UpdateDeathBypass();
        base.Update();
    }

    private void UpdateCurrentEffect()
    {
        Level level = SceneAs<Level>();
        bool useAlt = Utils_General.AreFlagsEnabled(level.Session, altFlag, false);
        currentEffect = useAlt ? altEffect : mainEffect;

        if (useAlt && altVisibility < 1) altVisibility += 0.05f;
        if (!useAlt && altVisibility > 0) altVisibility -= 0.05f;
    }

    private void UpdateDeathBypass()
    {
        Level level = SceneAs<Level>();
        foreach (Entity entity in level.Entities)
        {
            if (DeathHandlerDeathBypassTrigger.FilterEntity(entity, affectPlayer, false) && entity is not DeathHandlerBypassZone)
            {
                bool isCurrentlyInside = CollideCheck(entity);
                if (entity is Booster booster && booster.BoostingPlayer)
                {
                    Player player = base.Scene.Tracker.GetEntity<Player>();
                    isCurrentlyInside = CollideCheck(player);
                }

                bool wasInside = entitiesInsideZone.Contains(entity);

                if (isCurrentlyInside && !wasInside) UpdateZoneOnEnter(entity);
                if (!isCurrentlyInside && wasInside) UpdateZoneOnExit(entity);
            }
        }
    }

    private void UpdateDeathBypassEntity(Entity entity)
    {
        // Check for existing deathbypass
        if (entity.Components.Get<DeathBypass>() is null)
        {
            // No deathbypass. Add one.
            entity.Add(new DeathBypass(bypassFlag, true, initialAllowBypass: false));
        }
    }

    private void UpdateZoneOnEnter(Entity entity)
    {
        //Logger.Log(LogLevel.Info, "EndersExtras/DeathHandlerBypassZone", $"Entity entered zone: {entity} {entity.SourceId}");
        UpdateDeathBypassEntity(entity); // Set death bypass if not already set

        if (entity.Components.Get<DeathBypass>() is DeathBypass deathbypass)
        {
            entitiesInsideZone.Add(entity);
            switch (currentEffect)
            {
                case BypassEffect.Activate:
                    deathbypass.ToggleAllowBypass(entity, true, newRequireFlag: bypassFlag);
                    break;
                case BypassEffect.Deactivate:
                    deathbypass.ToggleAllowBypass(entity, false, newRequireFlag: bypassFlag);
                    break;
                case BypassEffect.Toggle:
                    deathbypass.ToggleAllowBypass(entity, null, newRequireFlag: bypassFlag);
                    break;
                default:
                    break;
            }
        }
    }
    private void UpdateZoneOnExit(Entity entity)
    {
        //Logger.Log(LogLevel.Info, "EndersExtras/DeathHandlerBypassZone", $"Entity exited zone: {entity} {entity.SourceID}");
        entitiesInsideZone.Remove(entity);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override void Render()
    {
        base.Render();

        Level level = SceneAs<Level>();
        if (level.Camera.GetRect().Intersects(this.HitRect()))
        {
            // Glass
            glassImage.RenderTessellate(width, height, Position);
            glassAltImage.RenderTessellate(width, height, Position, Color.White * altVisibility);

            // Border
            borderTexture.Render9Slice(width, height, Position);
        }
    }
}