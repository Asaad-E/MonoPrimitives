#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace Platformer2D;

/// <summary>
/// Small platformer demo: no menus, no pause, no music -- run/jump across platforms, stomp or
/// avoid enemies, reach the flag. Exists mainly to exercise 2D collision resolution, camera
/// follow, screen shake and bounds clamping together. Movement reads <see cref="PrimitiveInput"/>
/// directly; the camera is driven by <see cref="Camera2D.FollowTarget"/> called every frame by
/// this demo, never <c>UpdateWithInput</c>. Enemies and platforms are plain geometric shapes
/// (circles and rectangles) -- no sprites, matching this library's whole approach.
/// </summary>
public class Game1 : Game
{
    private const int VirtualWidth = 480;
    private const int VirtualHeight = 270;
    private const int WindowWidth = 960;
    private const int WindowHeight = 540;
    private const int LevelWidth = 2400;
    private const int LevelHeight = 270;

    private GraphicsDeviceManager _graphics;
    private PrimitiveBatch _batch2d = null!;
    private PrimitiveInput _input = null!;
    private BoxingViewportAdapter2D _adapter = null!;
    private Camera2D _camera2d = null!;

    private const float Gravity = 900f;
    private const float MoveSpeed = 220f;
    private const float JumpVelocity = -380f;
    private const float PlayerHalfWidth = 12f;
    private const float PlayerHalfHeight = 16f;

    private Vector2 _playerPos;
    private Vector2 _velocity;
    private bool _grounded;
    private int _lives;
    private int _score;
    private bool _won;
    private bool _dead;

    private readonly List<Rectangle> _platforms = new();

    private sealed class Enemy
    {
        public Vector2 Position;
        public float Radius;
        public float MinX, MaxX;
        public float Speed;
        public bool Alive = true;
    }

    private readonly List<Enemy> _enemies = new();
    private Rectangle _goal;
    private static readonly Vector2 SpawnPoint = new(40, LevelHeight - 60);

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = WindowWidth, PreferredBackBufferHeight = WindowHeight };
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _batch2d = new PrimitiveBatch(GraphicsDevice);
        _input = new PrimitiveInput();
        _adapter = new BoxingViewportAdapter2D(GraphicsDevice, VirtualWidth, VirtualHeight);
        _camera2d = new Camera2D(_adapter, target: SpawnPoint) { FollowSmoothTime = 0.15f, TargetBounds = LevelCameraBounds() };
        BuildLevel();
        ResetGame();
        base.Initialize();
    }

    private static Rectangle LevelCameraBounds()
        // Clamp so the camera never shows past the level edges -- half a viewport of margin
        // on each side lets Target reach the true edges without the view poking outside.
        => new(VirtualWidth / 2, VirtualHeight / 2, LevelWidth - VirtualWidth, LevelHeight - VirtualHeight);

    private void BuildLevel()
    {
        _platforms.Clear();
        _enemies.Clear();

        _platforms.Add(new Rectangle(0, LevelHeight - 20, 300, 20));
        _platforms.Add(new Rectangle(360, LevelHeight - 20, 260, 20));
        _platforms.Add(new Rectangle(680, LevelHeight - 70, 160, 20));
        _platforms.Add(new Rectangle(900, LevelHeight - 20, 300, 20));
        _platforms.Add(new Rectangle(1260, LevelHeight - 110, 140, 20));
        _platforms.Add(new Rectangle(1460, LevelHeight - 20, 220, 20));
        _platforms.Add(new Rectangle(1740, LevelHeight - 70, 160, 20));
        _platforms.Add(new Rectangle(1960, LevelHeight - 20, 400, 20));

        AddEnemy(420, LevelHeight - 20, 360, 600, 40f);
        AddEnemy(950, LevelHeight - 20, 900, 1180, 55f);
        AddEnemy(1520, LevelHeight - 20, 1460, 1660, 45f);
        AddEnemy(2050, LevelHeight - 20, 1960, 2300, 60f);

        _goal = new Rectangle(2320, LevelHeight - 60, 30, 40);
    }

    private void AddEnemy(float x, float platformTop, float minX, float maxX, float speed)
    {
        const float radius = 10f;
        _enemies.Add(new Enemy { Position = new Vector2(x, platformTop - radius), Radius = radius, MinX = minX, MaxX = maxX, Speed = speed });
    }

    private void ResetGame()
    {
        _playerPos = SpawnPoint;
        _velocity = Vector2.Zero;
        _lives = 3;
        _score = 0;
        _won = false;
        _dead = false;
        foreach (Enemy e in _enemies) e.Alive = true;
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = MathF.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1f / 30f);
        _input.Update(dt);

        if (_input.IsKeyPressed(Keys.R) && (_won || _dead))
            ResetGame();

        if (!_won && !_dead)
            UpdatePlaying(dt);

        _camera2d.FollowTarget(_playerPos, dt);
        _camera2d.Update(dt); // decays screen-shake trauma
        base.Update(gameTime);
    }

    private void UpdatePlaying(float dt)
    {
        float axis = _input.GetAxis(Keys.A, Keys.D) + _input.GetAxis(Keys.Left, Keys.Right);
        _velocity.X = axis * MoveSpeed;

        if (_grounded && (_input.IsKeyPressed(Keys.Space) || _input.IsKeyPressed(Keys.W) || _input.IsKeyPressed(Keys.Up)))
            _velocity.Y = JumpVelocity;

        _velocity.Y += Gravity * dt;
        MoveAndCollide(dt);
        UpdateEnemies(dt);
        CheckEnemyCollisions();

        if (_playerPos.Y > LevelHeight + 100f)
            LoseLife();

        if (Collision2D.CheckCollisionRecs(PlayerRect(), _goal))
            _won = true;
    }

    private Rectangle PlayerRect() => new((int)(_playerPos.X - PlayerHalfWidth), (int)(_playerPos.Y - PlayerHalfHeight), (int)(PlayerHalfWidth * 2), (int)(PlayerHalfHeight * 2));

    // Separate-axis AABB resolution: move+resolve X, then move+resolve Y -- standard platformer
    // approach, avoids the corner-catching a combined-axis resolve can produce.
    //
    // Uses a float overlap check (PlayerOverlaps), not Collision2D.CheckCollisionRecs(PlayerRect(), ...)
    // -- PlayerRect() truncates _playerPos to an int Rectangle, and since PlayerHalfHeight*2 has
    // no fractional part, that truncated rect stays byte-for-byte identical across almost a full
    // pixel of vertical movement. Combined with Rectangle.Intersects' strict (non-inclusive)
    // comparison, resting exactly on a platform after landing needed nearly a full pixel of
    // gravity-driven interpenetration to register as touching again -- confirmed by simulation:
    // _grounded cycled false/false/true while standing still, not true every frame. Space only
    // triggers a jump on the single-frame IsKeyPressed edge, so two out of every three presses
    // landed on a "not grounded" frame and were silently dropped -- reported as "jump randomly
    // doesn't work". Float precision removes the dead zone: any nonzero interpenetration (even
    // the ~0.25px one frame of gravity introduces) now registers immediately.
    private void MoveAndCollide(float dt)
    {
        _playerPos.X += _velocity.X * dt;
        foreach (Rectangle plat in _platforms)
        {
            if (!PlayerOverlaps(plat)) continue;
            if (_velocity.X > 0f) _playerPos.X = plat.Left - PlayerHalfWidth;
            else if (_velocity.X < 0f) _playerPos.X = plat.Right + PlayerHalfWidth;
            _velocity.X = 0f;
        }

        _playerPos.Y += _velocity.Y * dt;
        _grounded = false;
        foreach (Rectangle plat in _platforms)
        {
            if (!PlayerOverlaps(plat)) continue;
            if (_velocity.Y > 0f) { _playerPos.Y = plat.Top - PlayerHalfHeight; _grounded = true; }
            else if (_velocity.Y < 0f) { _playerPos.Y = plat.Bottom + PlayerHalfHeight; }
            _velocity.Y = 0f;
        }
    }

    private bool PlayerOverlaps(Rectangle plat)
        => _playerPos.X - PlayerHalfWidth < plat.Right && _playerPos.X + PlayerHalfWidth > plat.Left
        && _playerPos.Y - PlayerHalfHeight < plat.Bottom && _playerPos.Y + PlayerHalfHeight > plat.Top;

    private void UpdateEnemies(float dt)
    {
        foreach (Enemy e in _enemies)
        {
            if (!e.Alive) continue;
            e.Position.X += e.Speed * dt;
            if (e.Position.X < e.MinX || e.Position.X > e.MaxX) e.Speed = -e.Speed;
        }
    }

    private void CheckEnemyCollisions()
    {
        Rectangle playerRect = PlayerRect();
        foreach (Enemy e in _enemies)
        {
            if (!e.Alive) continue;
            if (!Collision2D.CheckCollisionCircleRec(e.Position, e.Radius, playerRect)) continue;

            bool stomping = _velocity.Y > 0f && (playerRect.Bottom - e.Position.Y) < e.Radius;
            if (stomping)
            {
                e.Alive = false;
                _velocity.Y = JumpVelocity * 0.6f;
                _score += 100;
                _camera2d.AddTrauma(0.2f);
            }
            else
            {
                LoseLife();
                return;
            }
        }
    }

    private void LoseLife()
    {
        _lives--;
        _camera2d.AddTrauma(0.5f);
        if (_lives <= 0) { _dead = true; return; }

        _playerPos = SpawnPoint;
        _velocity = Vector2.Zero;
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.MidnightBlue);

        _batch2d.Begin(_camera2d.GetTransformMatrix());

        foreach (Rectangle plat in _platforms)
            _batch2d.FillRectangleRounded(new Vector2(plat.X, plat.Y), new Vector2(plat.Width, plat.Height), 3f, Palette.Nephritis);

        _batch2d.FillRectangle(_goal.X, _goal.Y, _goal.Width, _goal.Height, Palette.Sunflower);

        foreach (Enemy e in _enemies)
            if (e.Alive)
                _batch2d.FillCircle(e.Position, e.Radius, Palette.Alizarin);

        if (!_dead)
        {
            Rectangle p = PlayerRect();
            _batch2d.FillRectangleRounded(new Vector2(p.X, p.Y), new Vector2(p.Width, p.Height), 4f, Palette.PeterRiver);
        }

        _batch2d.End();

        // Fixed-to-screen HUD: the adapter's scale only (no camera world-transform), so text
        // stays put in virtual pixel coordinates regardless of where the camera has scrolled.
        _batch2d.Begin(_adapter.GetScaleMatrix());
        _batch2d.DrawString($"Score: {_score}   Lives: {_lives}", new Vector2(10, 8), 1.4f, Color.White);
        _batch2d.DrawString("A/D: move   Space: jump   land on enemies to stomp them", new Vector2(10, 26), 1f, Palette.Silver);
        if (_won)
            CenteredMessage("YOU WIN -- press R", Palette.Emerald);
        if (_dead)
            CenteredMessage("GAME OVER -- press R", Palette.Alizarin);
        _batch2d.End();

        base.Draw(gameTime);
    }

    private void CenteredMessage(string text, Color color)
    {
        Vector2 size = new(text.Length * 6f * 1.6f, 7f * 1.6f);
        _batch2d.DrawString(text, new Vector2((VirtualWidth - size.X) * 0.5f, VirtualHeight * 0.5f - size.Y * 0.5f), 1.6f, color);
    }
}
