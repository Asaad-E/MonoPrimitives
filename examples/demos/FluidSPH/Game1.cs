using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace FluidSPH;

/// <summary>
/// A 2D Smoothed Particle Hydrodynamics fluid (see <see cref="FluidSim"/>) falling into a
/// container under gravity -- the classic SPH "dam break" demo. Up/Down ramps the particle count
/// live so you can watch <see cref="FrameLimiter.AverageFps"/> and find this machine's real
/// ceiling before the fluid stops looking like a fluid.
/// </summary>
public class Game1 : Game
{
    private const string Title = "Fluid SPH";
    private const int VirtualWidth = 1920;
    private const int VirtualHeight = 1080;
    private const int TargetFps = 60;

    private const int InitialParticleCount = 2000;
    private const int MinParticleCount = 100;
    private const int MaxParticleCount = 20_000;
    private const float CountChangeRatePerSecond = 1500f;
    private const float ParticleRadius = 5f;

    private readonly GraphicsDeviceManager _graphics;
    private ViewportAdapter2D _viewportAdapter = null!;
    private Camera2D _camera = null!;
    private PrimitiveInput _input = null!;
    private Primitive2DBatch _batch2d = null!;
    private FrameLimiter _limiter;

    private FluidSim _fluid = null!;
    private float _countChangeAccumulator;

    private static readonly Color SlowColor = Palette.BelizeHole;
    private static readonly Color FastColor = Palette.Clouds;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = VirtualWidth,
            PreferredBackBufferHeight = VirtualHeight,
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
        _viewportAdapter = new BoxingViewportAdapter2D(GraphicsDevice, VirtualWidth, VirtualHeight, pixelPerfect: true);
        _camera = new Camera2D(_viewportAdapter) { Offset = Vector2.Zero };
        _input = new PrimitiveInput(Window);
        _fluid = new FluidSim(VirtualWidth, VirtualHeight, InitialParticleCount);

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
        if (_input.IsKeyPressed(Keys.R)) _fluid.Reseed();
        HandleCountChange(dt);

        _fluid.Update(dt);

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
        int newCount = Math.Clamp(_fluid.Count + step, MinParticleCount, MaxParticleCount);
        if (newCount != _fluid.Count) _fluid.SetCount(newCount);
    }

    protected override void Draw(GameTime gameTime)
    {
        _batch2d.ClearLetterboxed(_viewportAdapter, backgroundColor: Palette.MidnightBlue);

        _batch2d.Begin(_camera.GetTransformMatrix());
        DrawFluid();
        DrawStats();
        _batch2d.End();

        _limiter.EndFrame();
        base.Draw(gameTime);
    }

    private void DrawFluid()
    {
        foreach (FluidSim.Particle p in _fluid.Particles)
        {
            // Faster-moving water reads as foam/froth (lighter), still water reads as deep blue --
            // a cheap, purely cosmetic stand-in for a real foam simulation.
            float speedFraction = MathHelper.Clamp(p.Velocity.Length() / 600f, 0f, 1f);
            Color color = ColorUtil.Lerp(SlowColor, FastColor, speedFraction);
            _batch2d.FillCircle(p.Position, ParticleRadius, color);
        }
    }

    private void DrawStats()
    {
        string stats = string.Format(CultureInfo.InvariantCulture,
            "Particles: {0:N0}   FPS: {1:F0} avg / {2:F0} cur   Frame: {3:F2}ms avg",
            _fluid.Count, _limiter.AverageFps, _limiter.CurrentFps, _limiter.AverageFrameTimeMs);
        _batch2d.DrawString(stats, new Vector2(16, 16), 2f, Color.White);
        _batch2d.DrawString("Up/Down: change count   R: reseed   Esc: quit", new Vector2(16, 44), 1.6f, Color.White);
    }
}
