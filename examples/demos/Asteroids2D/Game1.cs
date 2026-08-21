#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace Asteroids2D;

/// <summary>
/// Small Asteroids demo: no menus, no pause, no music -- a thrusting/rotating ship, splitting
/// asteroids, screen wrap. Movement reads <see cref="PrimitiveInput"/> directly, never the
/// camera's own controller. The camera itself is hand-driven from ship state -- <see cref="Camera2D.FollowTarget"/>
/// gives the "camera lags a little behind" feel, and <see cref="Camera2D.SmoothDamp(float,float,ref float,float,float)"/>
/// eases <c>Zoom</c> out while thrusting -- both are just building blocks a game calls itself,
/// not the "give me a whole controller" convenience this project deliberately keeps separate.
/// Colorful on purpose (bright palette per asteroid/ship/bullet) rather than the original
/// arcade's monochrome vector look.
/// </summary>
public class Game1 : Game
{
    private const int VirtualWidth = 960;
    private const int VirtualHeight = 720;
    private const int WindowWidth = 1280;
    private const int WindowHeight = 960;

    private GraphicsDeviceManager _graphics;
    private PrimitiveBatch _batch2d = null!;
    private PrimitiveInput _input = null!;
    private BoxingViewportAdapter2D _adapter = null!;
    private Camera2D _camera2d = null!;
    private float _zoomVelocity;

    private const float ShipTurnSpeed = 3.4f;
    private const float ShipThrust = 260f;
    private const float ShipDrag = 0.35f;
    private const float ShipMaxSpeed = 420f;
    private const float ShipRadius = 12f;

    private Vector2 _shipPos;
    private float _shipAngle;
    private Vector2 _shipVelocity;
    private int _lives;
    private bool _gameOver;
    private float _fireCooldown;

    private sealed class Asteroid
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Radius;
        public Color Color;
        public float Spin;
        public float Rotation;
    }

    private sealed class Bullet
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float TimeToLive;
    }

    private readonly List<Asteroid> _asteroids = new();
    private readonly List<Bullet> _bullets = new();
    private readonly Random _rng = new();
    private int _wave;
    private int _score;

    private static readonly Color[] AsteroidColors = { Palette.Carrot, Palette.Amethyst, Palette.Turquoise, Palette.Alizarin, Palette.PeterRiver, Palette.Emerald };

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = WindowWidth, PreferredBackBufferHeight = WindowHeight };
        IsMouseVisible = true;
        _graphics.HardwareModeSwitch = false;
    }

    protected override void Initialize()
    {
        _batch2d = new PrimitiveBatch(GraphicsDevice);
        _input = new PrimitiveInput();
        _adapter = new BoxingViewportAdapter2D(GraphicsDevice, VirtualWidth, VirtualHeight);
        _camera2d = new Camera2D(_adapter, target: new Vector2(VirtualWidth * 0.5f, VirtualHeight * 0.5f)) { FollowSmoothTime = 0.22f };
        ResetGame();
        base.Initialize();
    }

    private void ResetGame()
    {
        _shipPos = new Vector2(VirtualWidth * 0.5f, VirtualHeight * 0.5f);
        _shipAngle = -MathHelper.PiOver2;
        _shipVelocity = Vector2.Zero;
        _lives = 3;
        _gameOver = false;
        _score = 0;
        _wave = 0;
        _bullets.Clear();
        SpawnWave();
    }

    private void SpawnWave()
    {
        _wave++;
        _asteroids.Clear();
        int largeCount = 3 + _wave;
        for (int i = 0; i < largeCount; i++)
            SpawnAsteroid(RandomEdgePosition(), 40f);
    }

    private Vector2 RandomEdgePosition()
    {
        // Spawn away from the ship so a new wave doesn't insta-kill it.
        Vector2 pos;
        do
        {
            pos = new Vector2((float)_rng.NextDouble() * VirtualWidth, (float)_rng.NextDouble() * VirtualHeight);
        } while (Vector2.Distance(pos, _shipPos) < 180f);
        return pos;
    }

    private void SpawnAsteroid(Vector2 position, float radius)
    {
        float angle = (float)(_rng.NextDouble() * MathHelper.TwoPi);
        float speed = 30f + (float)_rng.NextDouble() * 50f;
        _asteroids.Add(new Asteroid
        {
            Position = position,
            Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed,
            Radius = radius,
            Color = AsteroidColors[_rng.Next(AsteroidColors.Length)],
            Spin = ((float)_rng.NextDouble() - 0.5f) * 2f,
        });
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = MathF.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1f / 30f);
        _input.Update(dt);

        if (_input.IsKeyPressed(Keys.R) && _gameOver)
            ResetGame();

        if (!_gameOver)
            UpdatePlaying(dt);

        UpdateCamera(dt);
        base.Update(gameTime);
    }

    private void UpdatePlaying(float dt)
    {
        if (_input.IsKeyDown(Keys.Left) || _input.IsKeyDown(Keys.A)) _shipAngle -= ShipTurnSpeed * dt;
        if (_input.IsKeyDown(Keys.Right) || _input.IsKeyDown(Keys.D)) _shipAngle += ShipTurnSpeed * dt;

        bool thrusting = _input.IsKeyDown(Keys.Up) || _input.IsKeyDown(Keys.W);
        if (thrusting)
        {
            Vector2 forward = new(MathF.Cos(_shipAngle), MathF.Sin(_shipAngle));
            _shipVelocity += forward * ShipThrust * dt;
            float speed = _shipVelocity.Length();
            if (speed > ShipMaxSpeed) _shipVelocity *= ShipMaxSpeed / speed;
        }
        _shipVelocity *= MathF.Max(0f, 1f - ShipDrag * dt);
        _shipPos = Wrap(_shipPos + _shipVelocity * dt);

        _fireCooldown -= dt;
        if ((_input.IsKeyDown(Keys.Space) || _input.IsKeyDown(Keys.RightControl)) && _fireCooldown <= 0f)
        {
            _fireCooldown = 0.22f;
            Vector2 dir = new(MathF.Cos(_shipAngle), MathF.Sin(_shipAngle));
            _bullets.Add(new Bullet { Position = _shipPos + dir * ShipRadius, Velocity = dir * 520f, TimeToLive = 1.1f });
        }

        UpdateBullets(dt);
        UpdateAsteroids(dt);
        CheckShipCollision();

        if (_asteroids.Count == 0)
            SpawnWave();
    }

    private void UpdateBullets(float dt)
    {
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            Bullet b = _bullets[i];
            b.Position = Wrap(b.Position + b.Velocity * dt);
            b.TimeToLive -= dt;
            if (b.TimeToLive <= 0f) { _bullets.RemoveAt(i); continue; }

            for (int j = _asteroids.Count - 1; j >= 0; j--)
            {
                Asteroid a = _asteroids[j];
                if (!Collision2D.CheckCollisionCircles(b.Position, 2.5f, a.Position, a.Radius)) continue;

                _bullets.RemoveAt(i);
                SplitAsteroid(a);
                _asteroids.RemoveAt(j);
                _camera2d.AddTrauma(0.15f);
                break;
            }
        }
    }

    private void SplitAsteroid(Asteroid a)
    {
        _score += a.Radius switch { >= 36f => 20, >= 20f => 50, _ => 100 };
        if (a.Radius < 20f) return; // smallest size: destroyed for good

        float childRadius = a.Radius * 0.6f;
        for (int i = 0; i < 2; i++)
            SpawnAsteroid(a.Position, childRadius);
    }

    private void UpdateAsteroids(float dt)
    {
        foreach (Asteroid a in _asteroids)
        {
            a.Position = Wrap(a.Position + a.Velocity * dt);
            a.Rotation += a.Spin * dt;
        }
    }

    private void CheckShipCollision()
    {
        foreach (Asteroid a in _asteroids)
        {
            if (!Collision2D.CheckCollisionCircles(_shipPos, ShipRadius, a.Position, a.Radius)) continue;

            _lives--;
            _camera2d.AddTrauma(0.6f);
            if (_lives <= 0) { _gameOver = true; return; }

            _shipPos = new Vector2(VirtualWidth * 0.5f, VirtualHeight * 0.5f);
            _shipVelocity = Vector2.Zero;
            return;
        }
    }

    private void UpdateCamera(float dt)
    {
        // Zoom out the faster the ship goes -- reads as "the world rushing past" while thrusting.
        float speedFraction = Math.Clamp(_shipVelocity.Length() / ShipMaxSpeed, 0f, 1f);
        float desiredZoom = MathHelper.Lerp(1f, 0.75f, speedFraction);
        _camera2d.Zoom = Camera2D.SmoothDamp(_camera2d.Zoom, desiredZoom, ref _zoomVelocity, 0.35f, dt);

        // A little lag behind the ship instead of locking on exactly -- reinforces the sense of speed.
        _camera2d.FollowTarget(_shipPos, dt);
        _camera2d.Update(dt); // decays screen-shake trauma
    }

    private static Vector2 Wrap(Vector2 p) => new(
        ((p.X % VirtualWidth) + VirtualWidth) % VirtualWidth,
        ((p.Y % VirtualHeight) + VirtualHeight) % VirtualHeight);

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.Background);

        Matrix transform = _camera2d.GetTransformMatrix() * _adapter.GetScaleMatrix();
        _batch2d.Begin(transform);

        _batch2d.BorderRectangle(0, 0, VirtualWidth, VirtualHeight, Palette.WetAsphalt, 2f);

        foreach (Asteroid a in _asteroids)
            DrawAsteroid(a);

        foreach (Bullet b in _bullets)
            _batch2d.FillCircle(b.Position, 3f, Palette.Sunflower);

        if (!_gameOver)
            DrawShip();

        _batch2d.End();

        _batch2d.Begin();
        _batch2d.DrawString($"Score: {_score}   Lives: {_lives}   Wave: {_wave}", new Vector2(16, 16), 1.8f, Color.White);
        _batch2d.DrawString("A/D: rotate   W: thrust   Space: fire", new Vector2(16, 42), 1.3f, Palette.Silver);
        if (_gameOver)
            _batch2d.DrawString("GAME OVER -- press R", new Vector2(WindowWidth * 0.5f - 140, WindowHeight * 0.5f), 2.2f, Palette.Alizarin);
        _batch2d.End();

        base.Draw(gameTime);
    }

    private void DrawShip()
    {
        Vector2 forward = new(MathF.Cos(_shipAngle), MathF.Sin(_shipAngle));
        Vector2 right = new(-forward.Y, forward.X);
        Vector2 nose = _shipPos + forward * ShipRadius * 1.6f;
        Vector2 left = _shipPos - forward * ShipRadius + right * ShipRadius * 0.9f;
        Vector2 rightBack = _shipPos - forward * ShipRadius - right * ShipRadius * 0.9f;
        _batch2d.FillTriangle(nose, left, rightBack, Palette.Clouds);
        _batch2d.BorderTriangle(nose, left, rightBack, Palette.PeterRiver, 2f);
    }

    private void DrawAsteroid(Asteroid a)
    {
        const int sides = 10;
        Span<Vector2> points = stackalloc Vector2[sides];
        for (int i = 0; i < sides; i++)
        {
            float t = i / (float)sides * MathHelper.TwoPi + a.Rotation;
            float wobble = 0.85f + 0.15f * MathF.Sin(t * 3f + a.Position.X);
            points[i] = a.Position + new Vector2(MathF.Cos(t), MathF.Sin(t)) * a.Radius * wobble;
        }
        _batch2d.FillPolygon(points, a.Color);
        for (int i = 0; i < sides; i++)
            _batch2d.DrawLine(points[i], points[(i + 1) % sides], 2f, Palette.WetAsphalt);
    }
}
