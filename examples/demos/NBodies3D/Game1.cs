using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoPrimitives;
using MonoPrimitives.Primitives2D;
using MonoPrimitives.Primitives3D;

namespace NBodies3D;

/// <summary>
/// The 3D counterpart to examples/demos/NBodies2D's galaxy: same Barnes-Hut-approximated gravity
/// (an octree here, see <see cref="NBodySwarm"/>), but flown around with a free camera instead of
/// viewed flat, since a 3D cluster's actual shape only reads once you can orbit it. Up/Down ramps
/// the body count live so you can watch <see cref="FrameLimiter.AverageFps"/> and find this
/// machine's real ceiling.
/// </summary>
public class Game1 : Game
{
    private const string Title = "N-Bodies 3D";
    private const float WorldSize = 2000f;
    private const int TargetFps = 60;

    private const int InitialBodyCount = 2000;
    private const int MinBodyCount = 2;
    private const int MaxBodyCount = 30_000;
    private const float CountChangeRatePerSecond = 3000f;

    private readonly GraphicsDeviceManager _graphics;
    private Camera3D _camera = null!;
    private PrimitiveInput _input = null!;
    private Primitive3DBatch _batch3d = null!;
    private Primitive2DBatch _batch2d = null!;
    private FrameLimiter _limiter;

    private NBodySwarm _swarm = null!;
    private float _countChangeAccumulator;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1600,
            PreferredBackBufferHeight = 900,
            PreferMultiSampling = true,
        };
        _graphics.PreparingDeviceSettings += (sender, e) =>
        {
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 4;
        };
        Window.AllowUserResizing = true;
        Window.Title = Title;

        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        _limiter = new FrameLimiter(this, targetFps: TargetFps, maxFrameTime: 1f / 15f);
    }

    protected override void Initialize()
    {
        _camera = new Camera3D(new Vector3(0f, 250f, 900f), Vector3.Zero, Vector3.Up)
        {
            MoveSpeed = 400f,
        };
        _input = new PrimitiveInput(Window);
        _swarm = new NBodySwarm(WorldSize, InitialBodyCount);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _batch3d = new Primitive3DBatch(GraphicsDevice);
        _batch2d = new Primitive2DBatch(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = _limiter.BeginFrame();
        _input.Update(dt);

        if (_input.IsKeyDown(Keys.Escape)) Exit();
        if (_input.IsKeyPressed(Keys.R)) _swarm.Reseed();
        HandleCountChange(dt);

        _camera.UpdateWithInput(_input, dt);
        _swarm.Update(dt);

        base.Update(gameTime);
    }

    private void HandleCountChange(float dt)
    {
        int direction = 0;
        if (_input.IsKeyDown(Keys.Up)) direction += 1;
        if (_input.IsKeyDown(Keys.Down)) direction -= 1;
        if (direction == 0) { _countChangeAccumulator = 0f; return; }

        _countChangeAccumulator += CountChangeRatePerSecond * dt * direction;
        int step = (int)_countChangeAccumulator;
        if (step == 0) return;

        _countChangeAccumulator -= step;
        int newCount = Math.Clamp(_swarm.Count + step, MinBodyCount, MaxBodyCount);
        if (newCount != _swarm.Count) _swarm.SetCount(newCount);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.Background);

        _batch3d.Begin(_camera);
        DrawBodies();
        _batch3d.End();

        _batch2d.Begin();
        DrawStats();
        _batch2d.End();

        _limiter.EndFrame();
        base.Draw(gameTime);
    }

    private void DrawBodies()
    {
        foreach (NBodySwarm.Body b in _swarm.Bodies)
        {
            float radius = MathF.Min(MathF.Sqrt(b.Mass) * 2.2f, 40f);
            _batch3d.FillSphere(b.Position, radius, b.Color);
        }
    }

    private void DrawStats()
    {
        string stats = string.Format(CultureInfo.InvariantCulture,
            "Bodies: {0:N0}   FPS: {1:F0} avg / {2:F0} cur   Frame: {3:F2}ms avg",
            _swarm.Count, _limiter.AverageFps, _limiter.CurrentFps, _limiter.AverageFrameTimeMs);
        _batch2d.DrawString(stats, new Vector2(16, 16), 2f, Color.White);
        _batch2d.DrawString("WASD: move   Right-drag: look   Wheel: zoom   Up/Down: change count   R: reseed   Esc: quit", new Vector2(16, 44), 1.6f, Color.White);
    }
}
