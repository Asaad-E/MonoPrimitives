#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using MonoPrimitives;
using MonoPrimitives.Primitives2D;
using MonoPrimitives.Primitives3D;

namespace Asteroids3D;

/// <summary>
/// Small 3D Asteroids demo: no menus, no pause, no music -- a free-flying ship (yaw/pitch +
/// thrust), splitting asteroids drifting in a wrapped cube of space, a chase camera. Yaw/pitch
/// are always relative to world Up (not the ship's own drifting up vector) so turning stays
/// predictable no matter how the ship is currently oriented; Q/E roll is purely a cosmetic
/// camera bank and never feeds back into flight. Movement reads <see cref="PrimitiveInput"/>
/// directly; the camera is hand-driven via <see cref="Camera3D.FollowTarget"/> every frame
/// (called by this demo, not <c>UpdateWithInput</c>) for a lag-behind chase feel. Colorful
/// asteroids on purpose, matching the 2D version.
/// </summary>
public class Game1 : Game
{
    private const int WindowWidth = 1280;
    private const int WindowHeight = 800;
    private const float WorldExtent = 260f; // world wraps within [-WorldExtent, +WorldExtent] on each axis

    private GraphicsDeviceManager _graphics;
    private Primitive3DBatch _batch3d = null!;
    private PrimitiveBatch _batch2d = null!;
    private PrimitiveInput _input = null!;
    private Camera3D _camera3d = null!;

    private const float ShipYawSpeed = 1.6f;
    private const float ShipPitchSpeed = 1.6f;
    private const float ShipRollSpeed = 2.2f;
    private const float ShipThrust = 140f;
    private const float ShipDrag = 0.4f;
    private const float ShipMaxSpeed = 220f;
    private const float ShipRadius = 6f;

    private Vector3 _shipPos;
    private Vector3 _shipForward;
    private Vector3 _shipUp;
    private Vector3 _shipVelocity;
    private float _bankAngle; // cosmetic only -- see RotateShip
    private int _lives;
    private bool _gameOver;
    private float _fireCooldown;

    private sealed class Asteroid
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Radius;
        public Color Color;
    }

    private sealed class Bullet
    {
        public Vector3 Position;
        public Vector3 Velocity;
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
    }

    protected override void Initialize()
    {
        _batch3d = new Primitive3DBatch(GraphicsDevice) { LightingEnabled = true, AmbientLight = 0.55f, LightDirection = new Vector3(-0.3f, -1f, -0.5f) };
        _batch2d = new PrimitiveBatch(GraphicsDevice);
        _input = new PrimitiveInput();
        _camera3d = new Camera3D(new Vector3(0, 20, 60), Vector3.Zero, Vector3.Up, 55f) { Mode = CameraMode.Custom, FollowSmoothTime = 0.25f };
        ResetGame();
        base.Initialize();
    }

    private void ResetGame()
    {
        _shipPos = Vector3.Zero;
        _shipForward = new Vector3(0, 0, -1);
        _shipUp = Vector3.Up;
        _shipVelocity = Vector3.Zero;
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
        int count = 3 + _wave;
        for (int i = 0; i < count; i++)
            SpawnAsteroid(RandomFarPosition(), 22f);
    }

    private Vector3 RandomFarPosition()
    {
        Vector3 pos;
        do
        {
            pos = new Vector3(
                ((float)_rng.NextDouble() * 2f - 1f) * WorldExtent,
                ((float)_rng.NextDouble() * 2f - 1f) * WorldExtent,
                ((float)_rng.NextDouble() * 2f - 1f) * WorldExtent);
        } while (Vector3.Distance(pos, _shipPos) < 90f);
        return pos;
    }

    private void SpawnAsteroid(Vector3 position, float radius)
    {
        Vector3 dir = RandomDirection();
        float speed = 12f + (float)_rng.NextDouble() * 18f;
        _asteroids.Add(new Asteroid { Position = position, Velocity = dir * speed, Radius = radius, Color = AsteroidColors[_rng.Next(AsteroidColors.Length)] });
    }

    private Vector3 RandomDirection()
    {
        float z = (float)_rng.NextDouble() * 2f - 1f;
        float t = (float)_rng.NextDouble() * MathHelper.TwoPi;
        float r = MathF.Sqrt(1f - z * z);
        return new Vector3(r * MathF.Cos(t), r * MathF.Sin(t), z);
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
        float yaw = _input.GetAxis(Keys.Left, Keys.Right) * ShipYawSpeed * dt;
        float pitch = _input.GetAxis(Keys.Down, Keys.Up) * ShipPitchSpeed * dt;
        float roll = _input.GetAxis(Keys.Q, Keys.E) * ShipRollSpeed * dt;
        RotateShip(yaw, pitch, roll);

        if (_input.IsKeyDown(Keys.Space))
        {
            _shipVelocity += _shipForward * ShipThrust * dt;
            float speed = _shipVelocity.Length();
            if (speed > ShipMaxSpeed) _shipVelocity *= ShipMaxSpeed / speed;
        }
        _shipVelocity *= MathF.Max(0f, 1f - ShipDrag * dt);
        _shipPos = Wrap(_shipPos + _shipVelocity * dt);

        _fireCooldown -= dt;
        if (_input.IsKeyDown(Keys.RightControl) && _fireCooldown <= 0f)
        {
            _fireCooldown = 0.25f;
            _bullets.Add(new Bullet { Position = _shipPos + _shipForward * ShipRadius * 2f, Velocity = _shipForward * 380f, TimeToLive = 1.4f });
        }

        UpdateBullets(dt);
        foreach (Asteroid a in _asteroids)
            a.Position = Wrap(a.Position + a.Velocity * dt);

        CheckShipCollision();
        if (_asteroids.Count == 0) SpawnWave();
    }

    // Yaw and pitch are always relative to world Up, not the ship's own (drifting) up vector --
    // the earlier version rotated yaw around _shipUp and let pitch tilt _shipUp too, so after any
    // combined maneuver "turn left/right" stopped meaning the same thing and the ship spiraled
    // unpredictably (reported as "controls feel super weird", reproduced by combining pitch and
    // yaw and watching the turn axis drift). Pitch is clamped so the ship can't fly exactly
    // straight up/down, where Forward and world Up go parallel and Right degenerates to zero.
    // Roll (Q/E) no longer touches flight at all -- it's purely a cosmetic camera bank now, so it
    // can never feed back into which way "turn" turns.
    private const float MaxPitchFromWorldUp = 5f * (MathF.PI / 180f);

    private void RotateShip(float yaw, float pitch, float roll)
    {
        if (yaw != 0f)
            _shipForward = Vector3.Normalize(Vector3.Transform(_shipForward, Matrix.CreateFromAxisAngle(Vector3.Up, yaw)));

        if (pitch != 0f)
        {
            Vector3 rightForPitch = Vector3.Normalize(Vector3.Cross(_shipForward, Vector3.Up));
            Vector3 candidate = Vector3.Normalize(Vector3.Transform(_shipForward, Matrix.CreateFromAxisAngle(rightForPitch, pitch)));

            float angleFromUp = MathF.Acos(Math.Clamp(Vector3.Dot(candidate, Vector3.Up), -1f, 1f));
            if (angleFromUp > MaxPitchFromWorldUp && angleFromUp < MathF.PI - MaxPitchFromWorldUp)
                _shipForward = candidate;
        }

        Vector3 right = Vector3.Normalize(Vector3.Cross(_shipForward, Vector3.Up));
        _shipUp = Vector3.Normalize(Vector3.Cross(right, _shipForward));

        if (roll != 0f)
            _bankAngle += roll;
        else
            _bankAngle *= 0.9f; // auto-level back toward zero once Q/E is released
    }

    private void UpdateBullets(float dt)
    {
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            Bullet b = _bullets[i];
            b.Position += b.Velocity * dt;
            b.TimeToLive -= dt;
            if (b.TimeToLive <= 0f) { _bullets.RemoveAt(i); continue; }

            for (int j = _asteroids.Count - 1; j >= 0; j--)
            {
                Asteroid a = _asteroids[j];
                if (!Collision3D.CheckCollisionSpheres(b.Position, 1.5f, a.Position, a.Radius)) continue;

                _bullets.RemoveAt(i);
                SplitAsteroid(a);
                _asteroids.RemoveAt(j);
                _camera3d.AddTrauma(0.15f);
                break;
            }
        }
    }

    private void SplitAsteroid(Asteroid a)
    {
        _score += a.Radius switch { >= 20f => 20, >= 12f => 50, _ => 100 };
        if (a.Radius < 12f) return;

        float childRadius = a.Radius * 0.6f;
        for (int i = 0; i < 2; i++)
            SpawnAsteroid(a.Position, childRadius);
    }

    private void CheckShipCollision()
    {
        foreach (Asteroid a in _asteroids)
        {
            if (!Collision3D.CheckCollisionSpheres(_shipPos, ShipRadius, a.Position, a.Radius)) continue;

            _lives--;
            _camera3d.AddTrauma(0.6f);
            if (_lives <= 0) { _gameOver = true; return; }

            _shipPos = Vector3.Zero;
            _shipVelocity = Vector3.Zero;
            return;
        }
    }

    private void UpdateCamera(float dt)
    {
        // _bankAngle is cosmetic only (see RotateShip) -- tilting the chase offset around
        // Forward by it gives Q/E a visible "banking turn" lean without ever touching flight.
        Vector3 bankedUp = Vector3.Transform(_shipUp, Matrix.CreateFromAxisAngle(_shipForward, _bankAngle));
        Vector3 desiredCamPos = _shipPos - _shipForward * 70f + bankedUp * 22f;
        Vector3 desiredLookAt = _shipPos + _shipForward * 30f;
        _camera3d.FollowTarget(desiredCamPos, dt, desiredLookAt);
        _camera3d.Update(dt); // decays screen-shake trauma
    }

    private static Vector3 Wrap(Vector3 p) => new(WrapAxis(p.X), WrapAxis(p.Y), WrapAxis(p.Z));

    private static float WrapAxis(float v)
    {
        float span = WorldExtent * 2f;
        float wrapped = ((v + WorldExtent) % span + span) % span - WorldExtent;
        return wrapped;
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.Background);

        _batch3d.Begin(_camera3d);
        _batch3d.DrawGridXZ(20, WorldExtent * 2f / 20f);
        _batch3d.BorderCube(Vector3.Zero, new Vector3(WorldExtent * 2f), Palette.WetAsphalt);

        foreach (Asteroid a in _asteroids)
            _batch3d.FillSphere(a.Position, a.Radius, a.Color);

        foreach (Bullet b in _bullets)
            _batch3d.FillSphere(b.Position, 1.5f, Palette.Sunflower);

        if (!_gameOver)
            _batch3d.FillCylinder(_shipPos + _shipForward * ShipRadius * 2f, _shipPos - _shipForward * ShipRadius, 0f, ShipRadius, 10, Palette.Clouds);

        _batch3d.End();

        _batch2d.Begin();
        _batch2d.DrawString($"Score: {_score}   Lives: {_lives}   Wave: {_wave}", new Vector2(16, 16), 1.8f, Color.White);
        _batch2d.DrawString("Left/Right: yaw   Up/Down: pitch   Q/E: roll   Space: thrust   RCtrl: fire", new Vector2(16, 42), 1.3f, Palette.Silver);
        if (_gameOver)
            _batch2d.DrawString("GAME OVER -- press R", new Vector2(WindowWidth * 0.5f - 140, WindowHeight * 0.5f), 2.2f, Palette.Alizarin);
        _batch2d.End();

        base.Draw(gameTime);
    }
}
