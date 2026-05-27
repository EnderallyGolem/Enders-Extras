using System;
using Celeste.Mod;
using Celeste.Mod.EndersExtras.Utils;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.EndersExtras.Entities.Misc
{
    [CustomEntity("EndersExtras/DreamDroplet")]
    [Tracked]
    public class DreamDroplet : Solid
    {
        private readonly MTexture[] particleTextures;
        private DreamBlock.DreamParticle[] particles;
        public Vector2 shake;
        private float whiteFill = 1f;
        private float whiteHeight = 1f;
        private float animTimer;

        private Color colour;

        private float respawnTime;
        private bool regainDash;
        private bool retainSpeed;

        private enum DashEffect {none, redirect_diffdir, redirect_allowsamedir};
        private enum JumpEffect {none, jump, super, hyper, wallbounce, default_effect};

        private JumpEffect defaultEffect;
        private JumpEffect defaultUpEffect;
        private JumpEffect defaultUpDiagonalEffect;
        private JumpEffect upKeyEffect;
        private JumpEffect downKeyEffect;
        private DashEffect dashEffect;

        private int dashSpeed;
        private float horizontalVelocityScale;
        private float verticalVelocityScale;
        private float normalisationScale;

        private float nodeMoveTime;
        private float nodeMoveOffset;

        private Ellipse ellipse;
        private bool playerInside = false;

        private JumpEffect StringToJumpEffect(String str)
        {
            switch (str)
            {
                case "none": return JumpEffect.none;
                case "jump": return JumpEffect.jump;
                case "super": return JumpEffect.super;
                case "hyper": return JumpEffect.hyper;
                case "wallbounce": return JumpEffect.wallbounce;
                default: return JumpEffect.default_effect;
            }
        }
        private DashEffect StringToDashEffect(String str)
        {
            switch (str)
            {
                case "redirect_diffdir": return DashEffect.redirect_diffdir;
                case "redirect_allowsamedir": return DashEffect.redirect_allowsamedir;
                default: return DashEffect.none;
            }
        }


        public DreamDroplet(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Width, data.Height, safe: true)
        {
            Depth = data.Int("Depth");
            colour = data.HexColor("colour");

            float semimajorDistance = data.Float("semimajorDistance", 5f) * 8;
            bool flipFocals = data.Bool("flipFocals", false);
            respawnTime = data.Float("respawnTime", 3);
            regainDash = data.Bool("regainDash", false);
            retainSpeed = data.Bool("retainSpeed", false);

            defaultEffect = StringToJumpEffect(data.Attr("defaultEffect", "jump"));
            defaultUpEffect = StringToJumpEffect(data.Attr("defaultUpEffect", "wallbounce"));
            defaultUpDiagonalEffect = StringToJumpEffect(data.Attr("defaultUpDiagonalEffect", "default_effect"));
            upKeyEffect = StringToJumpEffect(data.Attr("upKeyEffect", "wallbounce"));
            downKeyEffect = StringToJumpEffect(data.Attr("downKeyEffect", "hyper"));
            dashEffect = StringToDashEffect(data.Attr("dashEffect", "redirect_diffdir"));

            dashSpeed = data.Int("dashSpeed", 240);
            horizontalVelocityScale = data.Float("horizontalVelocityScale", 1.0f);
            verticalVelocityScale = data.Float("verticalVelocityScale", 1.0f);
            normalisationScale = data.Float("normalisationScale", 0f);

            nodeMoveTime = data.Float("nodeMoveTime", 3f);
            nodeMoveOffset = data.Float("nodeMoveOffset", 3f);


            particleTextures =
            [
                GFX.Game["objects/dreamblock/particles"].GetSubtexture(14, 0, 7, 7),
                GFX.Game["objects/dreamblock/particles"].GetSubtexture(7, 0, 7, 7),
                GFX.Game["objects/dreamblock/particles"].GetSubtexture(0, 0, 7, 7),
                GFX.Game["objects/dreamblock/particles"].GetSubtexture(7, 0, 7, 7)
            ];

            this.Collidable = false; // Check manually. Otherwise unwanted checks + crash due to other mods.

            // Elliptical collider.
            // Focals: TopLeft, BottomRight or (w/ flipFocals) TopRight, BottomLeft of entity rect
            ellipse = !flipFocals ?
                new Ellipse(semimajorDistance, new Vector2(4, 4), new Vector2(data.Width-4, data.Height-4))
              : new Ellipse(semimajorDistance, new Vector2(data.Width-4, 4), new Vector2(4, data.Height-4));
            Collider = ellipse;

            //Logger.Log(LogLevel.Info, "EndersExtras/DreamDroplet", $"Collider width {ellipse.Width}  height {ellipse.Height} | left {ellipse.Left} {ellipse.AbsoluteLeft} top {ellipse.Top} {ellipse.AbsoluteTop} | entity pos {Position} ");
        }

        public override void Awake(Scene scene)
        {
            base.Awake(scene);
        }

        public override void Added(Scene scene)
        {
            base.Added(scene);
            Setup();
        }


        public override void Update()
        {
            // Run player collision stuffs
            if (CheckPlayerCollision())
            {
                if (!playerInside)
                {
                    playerInside = true;
                    OnPlayerCollisionStart();
                }
                else
                {
                    OnPlayerCollisionUpdate();
                }
            }
            else
            {
                if (playerInside)
                {
                    playerInside = false;
                    OnPlayerCollisionEnd();
                }
            }

            base.Update();
            // animTimer += 6f * Engine.DeltaTime;
            // Level level = SceneAs<Level>();
        }

        public bool CheckPlayerCollision()
        {
            Level level = SceneAs<Level>();
            if (level.Tracker.GetEntity<Player>() is {} player)
            {
                Rectangle rect = new Rectangle((int)player.Collider.AbsoluteLeft, (int)player.Collider.AbsoluteTop, (int)player.Collider.Width, (int)player.Collider.Height);
                return ellipse.Collide(rect);
            }
            return false;
        }

        public void OnPlayerCollisionStart()
        {
            Logger.Log(LogLevel.Info, "EndersExtras/DreamDroplet", $"START collision");
        }
        public void OnPlayerCollisionUpdate()
        {
            //Logger.Log(LogLevel.Info, "EndersExtras/DreamDroplet", $"UPDATE collision");
        }
        public void OnPlayerCollisionEnd()
        {
            Logger.Log(LogLevel.Info, "EndersExtras/DreamDroplet", $"END collision");
        }


        public void Setup()
        {
            //Dream particles
            particles = new DreamBlock.DreamParticle[(int) (Width / 8.0 * (Height / 8.0) * 0.7)];
            for (int index = 0; index < particles.Length; ++index)
            {
                particles[index].Position = new Vector2(Calc.Random.NextFloat(Width), Calc.Random.NextFloat(Height));
                particles[index].Layer = Calc.Random.Choose(0, 1, 1, 2, 2, 2);
                particles[index].TimeOffset = Calc.Random.NextFloat();
                particles[index].Color = Color.LightGray * (float) (0.5 + particles[index].Layer / 2.0 * 0.5);
                switch (particles[index].Layer)
                {
                    case 0:
                        particles[index].Color = Calc.Random.Choose(Calc.HexToColor("FFEF11"), Calc.HexToColor("FF00D0"), Calc.HexToColor("08a310"));
                        continue;
                    case 1:
                        particles[index].Color = Calc.Random.Choose(Calc.HexToColor("5fcde4"), Calc.HexToColor("7fb25e"), Calc.HexToColor("E0564C"));
                        continue;
                    case 2:
                        particles[index].Color = Calc.Random.Choose(Calc.HexToColor("5b6ee1"), Calc.HexToColor("CC3B3B"), Calc.HexToColor("7daa64"));
                        continue;
                    default:
                        continue;
                }
            }
        }
        public override void Render()
        {
            Camera camera = SceneAs<Level>().Camera;
            if (Right < (double) camera.Left || Left > (double) camera.Right || Bottom < (double) camera.Top || Top > (double) camera.Bottom)
              return;

            // TODO. Render elliptical bubble... rough...

            Draw.Rect(shake.X + ellipse.AbsoluteLeft, shake.Y + ellipse.AbsoluteTop, Width, Height, DreamBlock.activeBackColor * 0.01f);

            Vector2 camPos = SceneAs<Level>().Camera.Position;
            for (int index = 0; index < particles.Length; ++index)
            {
                int layer = particles[index].Layer;
                Vector2 particlePos = PutInside(particles[index].Position + camPos * (float) (0.3 + 0.25 * layer));
                Color color = particles[index].Color;
                MTexture particleTexture;
                switch (layer)
                {
                case 0:
                  particleTexture = particleTextures[3 - (int) ((particles[index].TimeOffset * 4.0 + animTimer) % 4.0)];
                  break;
                case 1:
                  particleTexture = particleTextures[1 + (int) ((particles[index].TimeOffset * 2.0 + animTimer) % 2.0)];
                  break;
                default:
                  particleTexture = particleTextures[2];
                  break;
              }
              if (ellipse.IsInside(particlePos, buffer: -3)) particleTexture.DrawCentered(particlePos + shake, color);
            }

            //TODO - This is placeholder for hitbox testing!
            if (playerInside)
            {
                ellipse.DrawShape(Color.Green, 0, 0);
            }
            else
            {
                ellipse.DrawShape(Color.MediumPurple, 0, 0);
            }
        }

        private Vector2 PutInside(Vector2 pos)
        {
            if (pos.X > (double) Right)
                pos.X -= (float) Math.Ceiling((pos.X - (double) Right) / Width) * Width;
            else if (pos.X < (double) Left)
                pos.X += (float) Math.Ceiling((Left - (double) pos.X) / Width) * Width;
            if (pos.Y > (double) Bottom)
                pos.Y -= (float) Math.Ceiling((pos.Y - (double) Bottom) / Height) * Height;
            else if (pos.Y < (double) Top)
                pos.Y += (float) Math.Ceiling((Top - (double) pos.Y) / Height) * Height;
            return pos;
        }
    }
}

public class Ellipse : Collider
{
    // Offsets are from center
    public float semiMajorDist;
    public Vector2 focal1Pos;
    public Vector2 focal2Pos;
    public Vector2 centerPos => 0.5f * (focal1Pos + focal2Pos);

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
        Vector2 searchPos = circle.Center + Vector2.UnitX * circle.Radius;

        while (searchAngleRange * circle.Radius >= 0.5f && searchAngleRange > 0) // While search arc length >= 0.5
        {

            searchPos = circle.Center + new Vector2( MathF.Cos(searchAngle), MathF.Sin(searchAngle) ) * circle.Radius;
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

    public override bool Collide(Hitbox hitbox) => hitbox.Collide(this);
    public override bool Collide(Grid grid) => grid.Collide(this);
    public override bool Collide(ColliderList list) => list.Collide(this);

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
        Vector2 origPen1 = new Vector2(pen.X, pen.Y);

        Vector2 searchDir = -Vector2.UnitY;
        // Search searchDir, then searchDir perp. If neither, rotate both by 90 deg
        for (int i = 0; i < 4 * (Width + Height); i++)
        {
            if (pen == origPen1 && i>1) break; // Reach back orig pos

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