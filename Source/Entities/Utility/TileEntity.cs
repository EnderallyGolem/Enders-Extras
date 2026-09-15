using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Celeste.Mod.EndersExtras.Utils;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace Celeste.Mod.EndersExtras.Entities.Utility;
[Tracked]
[CustomEntity("EndersExtras/TileEntity")]

// This is largely PLAGIARIZED from VivHelper because i really needed a good tile entity =[
public class TileEntity : Solid
{
    private EntityID id;
    private TileGrid? tiles;
    private AnimatedTiles? animTiles;

    private readonly char tileType;
    private readonly char tiletypeOffscreen;
    private readonly bool backgroundTiles;
    private bool tileTypeMix = false;
    private readonly bool allowMergeDifferentType;
    private readonly bool allowMerge;
    private readonly bool extendOffscreen;
    private readonly bool noEdges;
    private readonly Color colour;

    private readonly bool locationSeeded;

    private readonly List<bool> offDirecBoolList;

    private List<TileEntity>? Group;

    private Point GroupBoundsMin;
    private Point GroupBoundsMax;

    private readonly bool dashBlock;
    private readonly bool dashBlockPermament;
    private readonly string dashBlockBreakSound;

    private readonly bool fallingBlock;
    private readonly bool fallingBlockClimbFall; //todo setting


    private List<LightOcclude>? lightOccludeComponents;
    private readonly bool occludeLightSetting;
    private readonly bool collidableSetting;
    private readonly string disableFlag;

    private readonly float widthSingle = 0;
    private readonly float heightSingle = 0;

    private Collider[] collidersList; // For master - list of all hitboxes
    private Vector2 masterPosOffset = Vector2.Zero; // Pos offset from master

    public bool HasGroup
    {
        get;
        private set;
    }

    public bool isMasterOfGroup
    {
        get;
        private set;
    }

    private TileEntity? getMasterOfGroup;

    public TileEntity(Vector2 position, float width, float height, EntityID id, char tileType, char tiletypeOffscreen, int depth, bool backgroundTiles, bool collidable, bool occludeLight, string colourStr, bool allowMergeDifferentType = false, bool allowMerge = true,
        bool extendOffscreen = false, bool noEdges = false, List<bool>? offDirecBoolList = null, bool locationSeeded = false,
        bool dashBlock = false, bool dashBlockPermament = false, String dashBlockBreakSound = "", bool fallingBlock = false, bool fallingBlockClimbFall = true, String disableFlag = "")
    : base(position, width, height, safe: true)
    {
        
        this.tileType = tileType;
        this.tiletypeOffscreen = tiletypeOffscreen;
        this.backgroundTiles = backgroundTiles;
        Depth = Calc.Clamp(depth, -300000, 20000);
        this.allowMergeDifferentType = allowMergeDifferentType;
        this.allowMerge = allowMerge;
        this.extendOffscreen = extendOffscreen;
        this.noEdges = noEdges;
        this.offDirecBoolList = offDirecBoolList ?? new List<bool>([true, true, true, true, true, true, true, true]); //Start at top, go CW
        this.locationSeeded = locationSeeded;

        this.dashBlock = dashBlock;
        this.dashBlockPermament = dashBlockPermament;
        this.dashBlockBreakSound = dashBlockBreakSound;

        this.fallingBlock = fallingBlock;
        this.fallingBlockClimbFall = fallingBlockClimbFall;

        this.colour = Calc.HexToColorWithAlpha(colourStr);

        this.occludeLightSetting = occludeLight;
        this.collidableSetting = collidable;
        this.disableFlag = disableFlag;

        this.widthSingle = width;
        this.heightSingle = height;

        // Temporary settings, for entities to look at and do stuff...
        this.AllowStaticMovers = collidableSetting;
        this.Collidable = collidableSetting;

        this.id = id;

        // All collidable/visible/light occulude/etc settings to be done after grouping!
        SurfaceSoundIndex = SurfaceIndex.TileToIndex.GetValueOrDefault(tileType, SurfaceIndex.Brick);
    }

    private void ChangeCollidable(bool? collidable)
    {
        if (!isMasterOfGroup) return;
        if (collidable is null) collidable = collidableSetting;

        //AllowStaticMovers = collidable.Value;
        Collidable = collidable.Value;
        if (collidable == true) EnableStaticMovers();
        else DisableStaticMovers();
    }
    // private void ChangeVisiblity(bool? visiblity)
    // {
    //     if (!isMasterOfGroup) return;
    //     if (visiblity is null) visiblity = true;
    //
    //     foreach (TileEntity tileEntity in Group!)
    //     {
    //         tileEntity.Visible = visiblity.Value;
    //     }
    // }
    private void ChangeLightOcclude(bool? blockLight)
    {
        if (!isMasterOfGroup) return;
        if (blockLight is null) blockLight = occludeLightSetting;

        // Remove
        if (blockLight == false && lightOccludeComponents is not null)
        {
            for (int i = 0; i < lightOccludeComponents!.Count; i++)
            {
                lightOccludeComponents[i].RemoveSelf();
            }
            lightOccludeComponents = null;
        }

        // Add
        if (blockLight == true && lightOccludeComponents is null)
        {
            lightOccludeComponents = [];
            foreach (Collider colliderSeg in collidersList)
            {
                LightOcclude newLightOcc;
                Rectangle colliderRect = colliderSeg.Bounds;
                colliderRect.X -= (int)Position.X;
                colliderRect.Y -= (int)Position.Y;

                Add(newLightOcc = new LightOcclude(colliderRect, 1f));
                lightOccludeComponents.Add(newLightOcc);
            }
        }
    }

    public override void Update()
    {
        base.Update();
        if (isMasterOfGroup)
        {
            FlagDisableStuff(); // Force disable if disableFlag is set.
        }
    }
    private void UpdatePos()
    {
        // Pos isn't used much apart from falling block particles
        if (!isMasterOfGroup) return;

        foreach (TileEntity tileEntity in Group!)
        {
            tileEntity.Position = this.Position + tileEntity.masterPosOffset;
        }
    }

    float opacity = 1f;
    bool? previousForceDisable = false; // Null is transitionary state. Keep checking!!!

    // This only runs for the master of the group!
    private void FlagDisableStuff()
    {
        if (disableFlag == "") return;

        // Flag controlled appearing/disappearing (ignore if no disableFlag)
        bool deactivate = Utils_General.AreFlagsEnabled(SceneAs<Level>().Session, disableFlag, false);
        if (deactivate && previousForceDisable is false or null)
        {
            if (opacity > 0) opacity += -0.1f;

            if (opacity <= 0)
            {
                ChangeCollidable(false);
                ChangeLightOcclude(false);
                previousForceDisable = true; // Done disabling
            }
        }
        else if (!deactivate && previousForceDisable is true or null)
        {
            previousForceDisable = null;

            float opacityCap = 1f;
            bool tileInGroupCollidePlayer = CollideCheck<Player>();

            if (collidableSetting && tileInGroupCollidePlayer) opacityCap = 0.5f; // Want to turn solid but player is inside
            if (opacity < opacityCap) opacity += 0.1f;

            // Set the stuff
            bool collidableToSet = collidableSetting && !tileInGroupCollidePlayer; // True only if setting is true and not colliding player
            ChangeCollidable(collidableToSet);
            ChangeLightOcclude(occludeLightSetting);

            if (opacity >= 1) previousForceDisable = false; // Done enabling
        }
        if (tiles is not null) tiles.Color = colour * opacity;
        if (animTiles is not null) animTiles.Color = colour * opacity;
    }

    private Vector2 relativePos;

    public TileEntity(EntityData data, Vector2 offset, EntityID id)
        : this(data.Position + offset, data.Width, data.Height, id, data.Char("tiletype", '3'), data.Char("tiletypeOffscreen", '◯'), data.Int("Depth", -9000), data.Bool("backgroundTiles", false), data.Bool("collidable", true), data.Bool("occludeLight", true), data.Attr("colour", "ffffffff"), data.Bool("allowMergeDifferentType", false), data.Bool("allowMerge", true), data.Bool("extendOffscreen", true), data.Bool("noEdges", false),
              [data.Bool("offU", true), data.Bool("offUR", true), data.Bool("offR", true), data.Bool("offDR", true), data.Bool("offD", true), data.Bool("offDL", true), data.Bool("offL", true), data.Bool("offUL", true)], data.Bool("locationSeeded", false),
              data.Bool("dashBlock", false), data.Bool("dashBlockPermament", true), data.Attr("dashBlockBreakSound", ""),  data.Bool("fallingBlock", false), data.Bool("fallingBlockClimbFall", true), data.Attr("disableFlag", "")
        )
    {
        relativePos = data.Position;

        int surfaceSoundIndexSet = data.Int("surfaceSoundIndex", -1);
        if (surfaceSoundIndexSet >= 0) { SurfaceSoundIndex = surfaceSoundIndexSet; }
    }

    // Fixes issues with rounding for non-grid-aligned tile entities.
    // I was going to add more to this but bundling the int together already fixes it
    private static int SafeDiv8(float num) { return (int)(num / 8); }

    public override void Awake(Scene scene)
    {
        if (!HasGroup)
        {
            isMasterOfGroup = true;
            Group = [];
            GroupBoundsMin = new Point((int)X, (int)Y);
            GroupBoundsMax = new Point((int)Right, (int)Bottom);
            AddToGroupAndFindChildren(this);
            _ = Scene;

            Rectangle rectangle = new Rectangle(SafeDiv8(GroupBoundsMin.X) - 1, SafeDiv8(GroupBoundsMin.Y) - 1, SafeDiv8(GroupBoundsMax.X - GroupBoundsMin.X) + 3, SafeDiv8(GroupBoundsMax.Y - GroupBoundsMin.Y) + 3);
            VirtualMap<char> virtualMap = new VirtualMap<char>(rectangle.Width, rectangle.Height, '0');


            Level level = SceneAs<Level>();
            Rectangle roomRect = level.Bounds;

            bool noEdgesAny = noEdges;

            foreach (TileEntity item in Group)
            {
                if (item.noEdges)
                {
                    noEdgesAny = true;
                }

                int num = (int)(SafeDiv8(item.X) - rectangle.X);
                int num2 = (int)(SafeDiv8(item.Y) - rectangle.Y);
                int num3 = SafeDiv8(item.Width);
                int num4 = SafeDiv8(item.Height);

                //If group size reaches the screen edge and extendOffscreen is enabled, increase width/height by 1 or decrease starting x/y by 1
                if (item.extendOffscreen)
                {
                    if (num + rectangle.X == SafeDiv8(roomRect.Left))
                    {
                        num--;
                        num3++;
                    }
                    if (num + num3 + rectangle.X == SafeDiv8(roomRect.Right))
                    {
                        num3++;
                    }
                    if (num2 + rectangle.Y == SafeDiv8(roomRect.Top))
                    {
                        num2--;
                        num4++;
                    }
                    if (num2 + num4 + rectangle.Y == SafeDiv8(roomRect.Bottom))
                    {
                        num4++;
                    }
                }

                for (int i = num; i < num + num3; i++)
                {
                    for (int j = num2; j < num2 + num4; j++)
                    {
                        virtualMap[i, j] = item.tileType;
                        Vector2 tilePos = new Vector2(i + rectangle.X, j + rectangle.Y) * 8;
                        //Logger.Log(LogLevel.Info, "EndersExtras/Misc/TileEntity", $"{tilePos.X}/{roomRect.Left}/{roomRect.Right} {tilePos.Y}/{roomRect.Top}/{roomRect.Bottom}");

                        Vector2 offDirection = new Vector2(0, 0);
                        if (tilePos.X < roomRect.Left) { offDirection.X = -1; }
                        if (tilePos.X >= roomRect.Right) { offDirection.X = 1; }
                        if (tilePos.Y < roomRect.Top) { offDirection.Y = -1; }
                        if (tilePos.Y >= roomRect.Bottom) { offDirection.Y = 1; }

                        //Logger.Log(LogLevel.Info, "EndersExtras/Misc/TileEntity", $"Exceed screen in direction {offDirection.X} {offDirection.Y}");

                        if (offDirection != Vector2.Zero && (
                             item.offDirecBoolList[0] && offDirection == new Vector2(0, -1) ||  //U
                             item.offDirecBoolList[1] && offDirection == new Vector2(1, -1) ||  //UR
                             item.offDirecBoolList[2] && offDirection == new Vector2(1, 0) ||  //R
                             item.offDirecBoolList[3] && offDirection == new Vector2(1, 1) ||  //DR
                             item.offDirecBoolList[4] && offDirection == new Vector2(0, 1) ||  //D
                             item.offDirecBoolList[5] && offDirection == new Vector2(-1, 1) ||  //DL
                             item.offDirecBoolList[6] && offDirection == new Vector2(-1, 0) ||  //L
                             item.offDirecBoolList[7] && offDirection == new Vector2(-1, -1)     //UL;
                           ))
                        {
                            //Logger.Log(LogLevel.Info, "EndersExtras/Misc/TileEntity", $"offscreen stuff");
                            virtualMap[i, j] = item.tiletypeOffscreen;
                        }
                    }
                }
            }
            //Logger.Log(LogLevel.Info, "EndersExtras/Misc/TileEntity", $"{virtualMap}");
            if (locationSeeded) { Calc.PushRandom((int)(relativePos.X * relativePos.Y + Width + Height)); }
            Autotiler tiler = backgroundTiles ? GFX.BGAutotiler : GFX.FGAutotiler;
            Autotiler.Generated map = tiler.GenerateMap(virtualMap, new Autotiler.Behaviour
            {
                EdgesExtend = false,
                EdgesIgnoreOutOfLevel = noEdgesAny,
                PaddingIgnoreOutOfLevel = false,
            });
            tiles = map.TileGrid; animTiles = map.SpriteOverlay;
            animTiles.Position = tiles.Position = new Vector2(GroupBoundsMin.X - X - 8, GroupBoundsMin.Y - Y - 8);
            animTiles.Color = tiles.Color = colour;
            tiles.VisualExtend = 32;
            Add(tiles); Add(animTiles);
            if (locationSeeded) { Calc.PopRandom(); }

            collidersList = new Collider[Group!.Count]; int county = 0;
            foreach (TileEntity tileEntity in Group!)
            {
                // Add all colliders to this list
                collidersList[county] = tileEntity.Collider.Clone();
                collidersList[county].Position = tileEntity.Position - this.Position;
                county++;

                if (tileEntity != this)
                {
                    tileEntity.Collidable = false;
                    tileEntity.Collider = null;
                }
            }
            Collider colliderAll = new ColliderList(collidersList);
            this.Collider = colliderAll;

            foreach (TileEntity tileEntity in Group!)
            {
                tileEntity.Collidable = false; tileEntity.AllowStaticMovers = false;
                tileEntity.masterPosOffset = tileEntity.Position - Position;
            }
        }

        // SETUP
        if (isMasterOfGroup)
        {
            ChangeLightOcclude(occludeLightSetting);
            ChangeCollidable(collidableSetting);

            OnDashCollide = OnDashed;

            if (fallingBlock) Add(new Coroutine(Sequence()));
            FlagDisableStuff(); // Force disable if disableFlag is set.
        }

        AllowStaticMovers = collidableSetting;
        base.Awake(scene);
    }

    private void AddToGroupAndFindChildren(TileEntity from, List<Entity>? entities = null)
    {
        // This function is repeatedly ran by the master until all the nearby blocks are found!

        from.getMasterOfGroup = this;
        //Logger.Log(LogLevel.Info, "EndersExtras/Misc/TileEntity", $"{id}: set {getMasterOfGroup.id} as master");

        if (from.X < GroupBoundsMin.X)
        {
            GroupBoundsMin.X = (int)from.X;
        }
        if (from.Y < GroupBoundsMin.Y)
        {
            GroupBoundsMin.Y = (int)from.Y;
        }
        if (from.Right > GroupBoundsMax.X)
        {
            GroupBoundsMax.X = (int)from.Right;
        }
        if (from.Bottom > GroupBoundsMax.Y)
        {
            GroupBoundsMax.Y = (int)from.Bottom;
        }
        from.HasGroup = true;
        Group!.Add(from);
        if (from != this)
        {
        }
        // Implement variable entities so that it doesn't pull from hash per tileentity in the chain
        if (entities == null && !Scene.Tracker.TryGetEntities<TileEntity>(out entities))
        {
            return;
        }
        foreach (TileEntity entity in entities!)
        {
            bool disallowMerge = !allowMerge || !entity.allowMerge || entity.HasGroup
                || entity.dashBlock != dashBlock || (entity.dashBlock && entity.dashBlockPermament != dashBlockPermament)
                || entity.fallingBlock != fallingBlock || (entity.fallingBlock && entity.fallingBlockClimbFall != fallingBlockClimbFall)
                || entity.colour != colour || entity.backgroundTiles != backgroundTiles
                || entity.collidableSetting != collidableSetting || entity.occludeLightSetting != occludeLightSetting
                || entity.disableFlag != disableFlag;

            if (!disallowMerge &&
                // Check if tile entity is next to other tile entity (horizontal +-1 or vertical +-1)
                (Scene.CollideCheckForce(new Rectangle((int)from.X - 1, (int)from.Y, (int)from.Width + 2, (int)from.Height), entity)
                 || Scene.CollideCheckForce(new Rectangle((int)from.X, (int)from.Y - 1, (int)from.Width, (int)from.Height + 2), entity)))
            {
                if (allowMergeDifferentType && entity.allowMergeDifferentType)
                {
                    tileTypeMix = true;
                    AddToGroupAndFindChildren(entity, entities);
                }
                else if (entity.tileType == tileType && !tileTypeMix)
                {
                    AddToGroupAndFindChildren(entity, entities);
                }
            }
        }
    }

    private void DashBlockBreak(Vector2 from, Vector2 direction, bool playSound = true)
    {
        if (playSound && dashBlockBreakSound != "") Audio.Play(dashBlockBreakSound, Position);

        // Breaking is ran by master of group! If not master, get the master.
        if (isMasterOfGroup)
        {
            // Run for every tile inside
            foreach (TileEntity tileEntity in Group!)
            {
                for (int i = 0; i < tileEntity.widthSingle / 8f; i++)
                {
                    for (int j = 0; j < tileEntity.heightSingle / 8f; j++)
                    {
                        Scene.Add(Engine.Pooler.Create<Debris>().Init(tileEntity.Position + new Vector2(4 + i * 8, 4 + j * 8), tileEntity.tileType, true).BlastFrom(from));
                    }
                }

                if (tileEntity.dashBlockPermament)
                {
                    tileEntity.RemoveAndFlagAsGone();
                }
                else
                {
                    tileEntity.DestroyStaticMovers();
                    tileEntity.RemoveSelf();
                }
            }
        }
        else if (getMasterOfGroup is not null)
        {
            getMasterOfGroup.DashBlockBreak(from, direction, false);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RemoveAndFlagAsGone()
    {
        foreach (StaticMover staticMover in staticMovers)
        {
            SceneAs<Level>().Session.DoNotLoad.Add(staticMover.Entity.SourceId);
            Logger.Log(LogLevel.Info, "EndersExtras/Misc/TileEntity", $"uhh don't load {staticMover.Entity.SourceId}");
        }
        Logger.Log(LogLevel.Info, "EndersExtras/Misc/TileEntity", $"destroy static movers. {staticMovers.Count}");
        DestroyStaticMovers();

        SceneAs<Level>().Session.DoNotLoad.Add(id);
        RemoveSelf();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private DashCollisionResults OnDashed(Player player, Vector2 direction)
    {
        if (!dashBlock)
        {
            if (player.StateMachine.State == 5) //Get out of the booster
            {
                player.StateMachine.State = 0;
            }
            return DashCollisionResults.NormalCollision;
        }

        DashBlockBreak(player.Center, direction);
        return DashCollisionResults.Rebound;
    }

    #region fallBlock

    // This is only ran by the master of the group!
    private bool triggered;

    private IEnumerator Sequence()
    {
        UpdatePos();
         while (!this.triggered && !this.PlayerFallCheck())
           yield return null;
        label_6:
         this.ShakeSfx();
         this.StartShaking();
         Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
         yield return 0.2f;
         float timer = 0.4f;
         for (; timer > 0.0 && this.PlayerWaitCheck(); timer -= Engine.DeltaTime)
           yield return null;
         this.StopShaking();

        FallParticles();

        float speed = 0.0f;
        float maxSpeed = 160f;
        Level? level;
        while (true)
        {
            UpdatePos();
            level = SceneAs<Level>();
            speed = Calc.Approach(speed, maxSpeed, 500f * Engine.DeltaTime);

            //Collidable = false;
            bool moveSuccess = MoveVCollideSolids(speed * Engine.DeltaTime, true);

            //Collidable = true;
            if (!moveSuccess)
            {
                // Null checks:
                // 1. If highest point is <= level.bounds.bottom+16
                // 2. Any block collides with a solid below it (important to exclude colliding with itself!)

                float topPos = float.PositiveInfinity;
                foreach (TileEntity tileEntity in Group!)
                {
                    topPos = tileEntity.Top < topPos ? tileEntity.Top : topPos;
                }

                bool collidedWithSomething = CollideCheck<Solid>(Position + new Vector2(0.0f, 1.0f));

                if (topPos <= (double)(level.Bounds.Bottom + 16 /*0x10*/) && (topPos <= (double)(level.Bounds.Bottom - 1) || !collidedWithSomething))
                {
                    yield return null;
                    level = null;
                }
                else
                    goto label_23;
            }
            else
                break;
        }

        ImpactSfx();
        Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
        SceneAs<Level>().DirectionalShake(Vector2.UnitY);
        StartShaking();
        LandParticles();
        yield return 0.2f;
        StopShaking();
        if (CollideCheck<SolidTiles>(Position + new Vector2(0.0f, 1f)))
        {
            Safe = true;
            yield break;
        }

        while (CollideCheck<Platform>(Position + new Vector2(0.0f, 1f)))
            yield return 0.1f;
        goto label_6;
        label_23:
        Collidable = Visible = false;
        yield return 0.2f;
        if (level.Session.MapData.CanTransitionTo(level, new Vector2(Center.X, Bottom + 12f)))
        {
            yield return 0.2f;
            SceneAs<Level>().Shake();
            Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
        }

        RemoveSelf();
        DestroyStaticMovers();
    }

    private void FallParticles()
    {
        foreach (TileEntity tileEntity in Group!)
        {
            for (int x = 2; x < (double)tileEntity.widthSingle; x += 4)
            {
                if (Scene.CollideCheck<Solid>(tileEntity.Position + new Vector2(x, -2f)))
                    SceneAs<Level>().Particles.Emit(FallingBlock.P_FallDustA, 2, new Vector2(tileEntity.X + x, tileEntity.Y), Vector2.One * 4f, 1.5707964f);
                SceneAs<Level>().Particles.Emit(FallingBlock.P_FallDustB, 2, new Vector2(tileEntity.X + x, tileEntity.Y), Vector2.One * 4f);
            }
        }
    }

    private void LandParticles()
    {
        bool oldCollidable = Collidable; Collidable = false;
        foreach (TileEntity tileEntity in Group!)
        {
            //Logger.Log(LogLevel.Info, "EndersExtras/Misc/TileEntity", $"{tileEntity.Position} {tileEntity.widthSingle} {tileEntity.heightSingle} {tileEntity.Height}");

            for (int x = 2; x <= (double)tileEntity.widthSingle; x += 4)
            {
                float bottomY = tileEntity.Position.Y + tileEntity.heightSingle;
                if (tileEntity.Scene.CollideCheck<Solid>(tileEntity.Position + Vector2.UnitY*tileEntity.heightSingle + new Vector2(x, 3f)))
                {
                    SceneAs<Level>().ParticlesFG.Emit(FallingBlock.P_FallDustA, 1, new Vector2(tileEntity.X + x, bottomY), Vector2.One * 4f, -1.5707964f);
                    float direction = x >= tileEntity.Width / 2.0 ? 0.0f : 3.1415927f;
                    SceneAs<Level>().ParticlesFG.Emit(FallingBlock.P_LandDust, 1, new Vector2(tileEntity.X + x, bottomY), Vector2.One * 4f, direction);
                }
            }
        }
        Collidable = oldCollidable;
    }

    private void ShakeSfx()
    {
        if (this.tileType == '3')
            Audio.Play("event:/game/01_forsaken_city/fallblock_ice_shake", this.Center);
        else if (this.tileType == '9')
            Audio.Play("event:/game/03_resort/fallblock_wood_shake", this.Center);
        else if (this.tileType == 'g')
            Audio.Play("event:/game/06_reflection/fallblock_boss_shake", this.Center);
        else
            Audio.Play("event:/game/general/fallblock_shake", this.Center);
    }
    private void ImpactSfx()
    {
        if (this.tileType == '3')
            Audio.Play("event:/game/01_forsaken_city/fallblock_ice_impact", this.BottomCenter);
        else if (this.tileType == '9')
            Audio.Play("event:/game/03_resort/fallblock_wood_impact", this.BottomCenter);
        else if (this.tileType == 'g')
            Audio.Play("event:/game/06_reflection/fallblock_boss_impact", this.BottomCenter);
        else
            Audio.Play("event:/game/general/fallblock_impact", this.BottomCenter);
    }
    public override void OnStaticMoverTrigger(StaticMover sm)
    {
        this.triggered = true;
        base.OnStaticMoverTrigger(sm);
    }
    private bool PlayerFallCheck() => this.fallingBlockClimbFall ? this.HasPlayerRider() : this.HasPlayerOnTop();
    private bool PlayerWaitCheck()
    {
        if (triggered || PlayerFallCheck()) return true;
        if (!fallingBlockClimbFall) return false;
        return CollideCheck<Player>(Position - Vector2.UnitX) || CollideCheck<Player>(Position + Vector2.UnitX);
    }
    public override void OnShake(Vector2 amount)
    {
        base.OnShake(amount);
        if (isMasterOfGroup)
        {
            if (tiles is not null) tiles.Position += amount;
            if (animTiles is not null) animTiles.Position += amount;
        }
    }

    #endregion
}

public static class Extensions
{
    public static bool TryGetEntities<T>(this Tracker self, out List<Entity>? entities)
    {
        return self.TryGetEntities(typeof(T), out entities);
    }
    public static bool TryGetEntities(this Tracker self, Type type, out List<Entity>? entities)
    {
        entities = null;
        if (self.Entities.TryGetValue(type, out entities))
            return true;
        return false;
    }
}