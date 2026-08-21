#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace ParticleTrailTest;

/// <summary>
/// Visual test for <see cref="Trail2D"/>: a handful of particles bouncing off the window edges
/// and each other (elastic collision response written here, on top of <see cref="Collision2D"/>'s
/// detection-only checks — resolving overlaps is explicitly out of the library's scope), each
/// dragging its own differently-styled trail so a side-by-side comparison of trail looks is easy.
/// </summary>
public class Game1 : Game
{
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;

    private GraphicsDeviceManager _graphics;
    private PrimitiveBatch _batch2d = null!;
    private Particle[] _particles = null!;

    private sealed class Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Radius;
        public Color Color;
        public Trail2D Trail = null!;
        public float TrailThickness;
        public float TrailFadeToAlpha;
    }

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = WindowWidth, PreferredBackBufferHeight = WindowHeight };
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _batch2d = new PrimitiveBatch(GraphicsDevice);
        BuildParticles();
        base.Initialize();
    }

    // Each particle gets a visibly different trail style (length/thickness/fade/color) so the
    // range of looks Trail2D.Draw supports is easy to compare at a glance.
    private void BuildParticles()
    {
        var rng = new Random(12345);
        (int capacity, float thickness, float fadeToAlpha)[] styles =
        {
            (60, 2f, 0f),     // long, thin, fades fully to invisible
            (24, 6f, 0f),     // short, thick, fades fully
            (90, 3f, 0.25f),  // long, medium, never fully disappears (a soft "glow" tail)
            (14, 8f, 0f),     // very short, very thick -- reads almost like a comet head
            (45, 4f, 0.1f),   // medium all around
            (70, 2.5f, 0.4f), // long and thin but stays fairly visible throughout
        };

        _particles = new Particle[styles.Length];
        for (int i = 0; i < _particles.Length; i++)
        {
            float angle = (float)(rng.NextDouble() * MathHelper.TwoPi);
            float speed = 120f + (float)rng.NextDouble() * 80f;
            var (capacity, thickness, fadeToAlpha) = styles[i];
            _particles[i] = new Particle
            {
                Position = new Vector2(
                    100 + (float)rng.NextDouble() * (WindowWidth - 200),
                    100 + (float)rng.NextDouble() * (WindowHeight - 200)),
                Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed,
                Radius = 10f + i * 2f,
                Color = Palette.Cycle(i),
                Trail = new Trail2D(capacity),
                TrailThickness = thickness,
                TrailFadeToAlpha = fadeToAlpha,
            };
        }
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        foreach (Particle p in _particles)
        {
            p.Position += p.Velocity * dt;
            BounceOffWalls(p);
        }

        for (int i = 0; i < _particles.Length; i++)
            for (int j = i + 1; j < _particles.Length; j++)
                ResolveParticleCollision(_particles[i], _particles[j]);

        foreach (Particle p in _particles)
            p.Trail.Add(p.Position);

        base.Update(gameTime);
    }

    private static void BounceOffWalls(Particle p)
    {
        if (p.Position.X - p.Radius < 0f) { p.Position.X = p.Radius; p.Velocity.X = MathF.Abs(p.Velocity.X); }
        else if (p.Position.X + p.Radius > WindowWidth) { p.Position.X = WindowWidth - p.Radius; p.Velocity.X = -MathF.Abs(p.Velocity.X); }

        if (p.Position.Y - p.Radius < 0f) { p.Position.Y = p.Radius; p.Velocity.Y = MathF.Abs(p.Velocity.Y); }
        else if (p.Position.Y + p.Radius > WindowHeight) { p.Position.Y = WindowHeight - p.Radius; p.Velocity.Y = -MathF.Abs(p.Velocity.Y); }
    }

    // Collision2D only detects the overlap (by design -- this library never resolves physics);
    // the separation + equal-mass elastic velocity swap along the contact normal below is this
    // test's own simple response, not something Collision2D provides.
    private static void ResolveParticleCollision(Particle a, Particle b)
    {
        if (!Collision2D.CheckCollisionCircles(a.Position, a.Radius, b.Position, b.Radius))
            return;

        Vector2 delta = a.Position - b.Position;
        float dist = delta.Length();
        if (dist < 1e-4f)
        {
            delta = new Vector2(1f, 0f);
            dist = 1f;
        }
        Vector2 normal = delta / dist;

        float overlap = (a.Radius + b.Radius) - dist;
        a.Position += normal * (overlap * 0.5f);
        b.Position -= normal * (overlap * 0.5f);

        float aVelN = Vector2.Dot(a.Velocity, normal);
        float bVelN = Vector2.Dot(b.Velocity, normal);
        a.Velocity += (bVelN - aVelN) * normal;
        b.Velocity += (aVelN - bVelN) * normal;
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.Background);

        _batch2d.Begin();

        foreach (Particle p in _particles)
            p.Trail.Draw(_batch2d, p.Color, p.TrailThickness, p.TrailFadeToAlpha);

        foreach (Particle p in _particles)
        {
            _batch2d.FillCircle(p.Position, p.Radius, p.Color);
            _batch2d.BorderCircle(p.Position, p.Radius, Color.White, 1.5f);
        }

        _batch2d.DrawString("Particles bounce off walls and each other -- each drags a differently-styled Trail2D", new Vector2(16, 16), 1.5f, Color.White);

        _batch2d.End();
        base.Draw(gameTime);
    }
}
