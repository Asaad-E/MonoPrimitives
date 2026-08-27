using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace NBodies2D;

/// <summary>
/// A gravitational N-body "galaxy": every body pulls on every other body, approximated at scale
/// with a Barnes-Hut quadtree (see <see cref="NBodySwarm"/>) instead of the O(bodies^2) pairwise
/// sum that would otherwise cap this at a few thousand bodies. Up/Down ramps the body count live
/// so you can watch <see cref="FrameLimiter.AverageFps"/> and find this machine's real ceiling.
/// </summary>
public class Game1 : Game
{
    private const string Title = "N-Bodies 2D";
    private const int VirtualWidth = 1920;
    private const int VirtualHeight = 1080;
    private const int TargetFps = 60;

    private const int InitialBodyCount = 3000;
    private const int MinBodyCount = 2;
    private const int MaxBodyCount = 50_000;
    private const float CountChangeRatePerSecond = 4000f;

    private readonly GraphicsDeviceManager _graphics;
    private ViewportAdapter2D _viewportAdapter = null!;
    private Camera2D _camera = null!;
    private PrimitiveInput _input = null!;
    private Primitive2DBatch _batch2d = null!;
    private FrameLimiter _limiter;

    private NBodySwarm _swarm = null!;
    private float _countChangeAccumulator;

    public Game1()
    {
        // Deliberately smaller than the virtual resolution, with continuous (not pixel-perfect)
        // viewport scaling below -- see BoidsSwarm/Game1.cs's own constructor comment and
        // DECISIONS.md for why a window sized to match the virtual resolution exactly, combined
        // with pixel-perfect scaling, can silently crop content near the edges instead of
        // shrinking it to fit.
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
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
        _viewportAdapter = new BoxingViewportAdapter2D(GraphicsDevice, VirtualWidth, VirtualHeight);
        _camera = new Camera2D(_viewportAdapter) { Offset = Vector2.Zero };
        _input = new PrimitiveInput(Window);
        _swarm = new NBodySwarm(VirtualWidth, VirtualHeight, InitialBodyCount);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _batch2d = new Primitive2DBatch(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = _limiter.BeginFrame();
        _input.Update(dt);

        if (_input.IsKeyDown(Keys.Escape)) Exit();
        if (_input.IsKeyPressed(Keys.R)) _swarm.Reseed();
        HandleCountChange(dt);

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
        _batch2d.ClearLetterboxed(_viewportAdapter, backgroundColor: Palette.Background);

        _batch2d.Begin(_camera.GetTransformMatrix(), BlendState.Additive);
        DrawBodies();
        _batch2d.End();

        _batch2d.Begin(_camera.GetTransformMatrix());
        DrawStats();
        _batch2d.End();

        _limiter.EndFrame();
        base.Draw(gameTime);
    }

    private void DrawBodies()
    {
        foreach (NBodySwarm.Body b in _swarm.Bodies)
        {
            float radius = MathF.Min(MathF.Sqrt(b.Mass) * 1.1f, 24f);
            Color glow = b.Color * 0.6f;
            _batch2d.FillCircleGradient(b.Position, radius, b.Color, glow);
        }
    }

    private void DrawStats()
    {
        string stats = string.Format(CultureInfo.InvariantCulture,
            "Bodies: {0:N0}   FPS: {1:F0} avg / {2:F0} cur   Frame: {3:F2}ms avg",
            _swarm.Count, _limiter.AverageFps, _limiter.CurrentFps, _limiter.AverageFrameTimeMs);
        _batch2d.DrawString(stats, new Vector2(16, 16), 2f, Color.White);
        _batch2d.DrawString("Up/Down: change count   R: reseed   Esc: quit", new Vector2(16, 44), 1.6f, Color.White);
    }
}
