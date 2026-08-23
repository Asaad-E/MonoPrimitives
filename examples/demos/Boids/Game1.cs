#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace Boids;

/// <summary>
/// Small flocking simulation (Craig Reynolds' boids: separation/alignment/cohesion) -- the
/// "simulations" half of this library's scope, not a game, so there's no score/lives/win state,
/// just something to watch and nudge. Left-click attracts the flock toward the cursor, right-click
/// scatters it -- read directly from <see cref="PrimitiveInput"/>, no camera involved at all
/// (screen space *is* world space here). No menus, no pause, no music.
/// </summary>
public class Game1 : Game
{
    private const int WindowWidth = 1280;
    private const int WindowHeight = 800;
    private const int BoidCount = 120;

    private GraphicsDeviceManager _graphics;
    private Primitive2DBatch _batch2d = null!;
    private PrimitiveInput _input = null!;

    private struct Boid
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Color Color;
    }

    private Boid[] _boids = new Boid[BoidCount];

    private const float PerceptionRadius = 55f;
    private const float SeparationRadius = 22f;
    private const float MaxSpeed = 180f;
    private const float MaxForce = 220f;
    private const float SeparationWeight = 1.6f;
    private const float AlignmentWeight = 1.0f;
    private const float CohesionWeight = 0.9f;
    private const float MouseForce = 260f;

    private static readonly Color[] FlockColors = { Palette.Turquoise, Palette.PeterRiver, Palette.Amethyst, Palette.Emerald };

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = WindowWidth, PreferredBackBufferHeight = WindowHeight };
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _batch2d = new Primitive2DBatch(GraphicsDevice);
        _input = new PrimitiveInput();

        var rng = new Random(7);
        for (int i = 0; i < _boids.Length; i++)
        {
            float angle = (float)(rng.NextDouble() * MathHelper.TwoPi);
            _boids[i] = new Boid
            {
                Position = new Vector2((float)rng.NextDouble() * WindowWidth, (float)rng.NextDouble() * WindowHeight),
                Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (MaxSpeed * 0.5f),
                Color = FlockColors[rng.Next(FlockColors.Length)],
            };
        }

        base.Initialize();
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = MathF.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1f / 30f);
        _input.Update(dt);

        Vector2? attractTo = _input.IsMouseButtonDown(MouseButton.Left) ? _input.MousePosition : null;
        Vector2? repelFrom = _input.IsMouseButtonDown(MouseButton.Right) ? _input.MousePosition : null;

        for (int i = 0; i < _boids.Length; i++)
            StepBoid(i, dt, attractTo, repelFrom);

        base.Update(gameTime);
    }

    private void StepBoid(int index, float dt, Vector2? attractTo, Vector2? repelFrom)
    {
        Boid self = _boids[index];
        Vector2 separation = Vector2.Zero, alignment = Vector2.Zero, cohesion = Vector2.Zero;
        int neighbors = 0;

        for (int j = 0; j < _boids.Length; j++)
        {
            if (j == index) continue;
            // Wrap-aware: the shortest vector from self to the other boid, accounting for the
            // screen wrap. A plain unwrapped delta treats a boid just past the right edge and
            // one just past the left edge as far apart even though they're visually adjacent
            // after wrapping -- boids would misjudge their neighbors near the seam and pile up
            // at the wrap corners instead of flocking naturally (confirmed by rendering: two
            // dense, near-stationary clumps sitting exactly on the corners before this fix).
            Vector2 toOther = ToroidalDelta(self.Position, _boids[j].Position);
            float distSq = toOther.LengthSquared();
            if (distSq > PerceptionRadius * PerceptionRadius || distSq < 1e-6f) continue;

            float dist = MathF.Sqrt(distSq);
            if (dist < SeparationRadius)
                separation -= toOther / dist * (SeparationRadius - dist);

            alignment += _boids[j].Velocity;
            cohesion += toOther;
            neighbors++;
        }

        Vector2 steer = separation * SeparationWeight;
        if (neighbors > 0)
        {
            alignment /= neighbors;
            steer += Limit(alignment - self.Velocity, MaxForce) * AlignmentWeight;

            cohesion /= neighbors;
            steer += Limit(cohesion, MaxForce) * CohesionWeight;
        }

        if (attractTo.HasValue)
            steer += Limit(attractTo.Value - self.Position, MaxForce) * 1.4f;
        if (repelFrom.HasValue)
        {
            Vector2 away = self.Position - repelFrom.Value;
            float distSq = away.LengthSquared();
            if (distSq < 220f * 220f && distSq > 1e-6f)
                steer += Vector2.Normalize(away) * MouseForce;
        }

        self.Velocity = LimitVelocity(self.Velocity + steer * dt);
        self.Position = Wrap(self.Position + self.Velocity * dt);
        _boids[index] = self;
    }

    // Shortest vector from `from` to `to` on the wrapped screen -- e.g. from x=1270 to x=5 is
    // +15 (through the seam), not -1265 (the long way around, which a plain subtraction gives).
    private static Vector2 ToroidalDelta(Vector2 from, Vector2 to)
    {
        float dx = WrapDelta(to.X - from.X, WindowWidth);
        float dy = WrapDelta(to.Y - from.Y, WindowHeight);
        return new Vector2(dx, dy);
    }

    private static float WrapDelta(float delta, float span)
    {
        if (delta > span * 0.5f) delta -= span;
        else if (delta < -span * 0.5f) delta += span;
        return delta;
    }

    private static Vector2 Limit(Vector2 v, float max)
    {
        float lenSq = v.LengthSquared();
        return lenSq > max * max ? v * (max / MathF.Sqrt(lenSq)) : v;
    }

    private static Vector2 LimitVelocity(Vector2 v) => Limit(v, MaxSpeed);

    private static Vector2 Wrap(Vector2 p) => new(
        ((p.X % WindowWidth) + WindowWidth) % WindowWidth,
        ((p.Y % WindowHeight) + WindowHeight) % WindowHeight);

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.Background);

        _batch2d.Begin();
        foreach (Boid b in _boids)
            DrawBoid(b);

        _batch2d.DrawString("Left-click: attract   Right-click: scatter", new Vector2(16, 16), 1.6f, Color.White);
        _batch2d.End();

        base.Draw(gameTime);
    }

    private void DrawBoid(Boid b)
    {
        Vector2 forward = b.Velocity.LengthSquared() > 1e-4f ? Vector2.Normalize(b.Velocity) : Vector2.UnitX;
        Vector2 right = new(-forward.Y, forward.X);
        const float length = 10f, width = 6f;
        Vector2 nose = b.Position + forward * length;
        Vector2 left = b.Position - forward * length * 0.6f + right * width;
        Vector2 tailRight = b.Position - forward * length * 0.6f - right * width;
        _batch2d.FillTriangle(nose, left, tailRight, b.Color);
    }
}
