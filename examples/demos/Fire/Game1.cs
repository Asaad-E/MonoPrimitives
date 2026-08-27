using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace Fire;

/// <summary>
/// A particle fire (see <see cref="FireSim"/>) rising from an emitter at the bottom of the
/// screen, cooling from white-hot to smoke as each particle ages. Up/Down ramps the maximum
/// particle count live so you can watch <see cref="FrameLimiter.AverageFps"/> and find this
/// machine's real ceiling.
/// </summary>
public class Game1 : Game
{
    private const string Title = "Fire";
    private const int VirtualWidth = 1920;
    private const int VirtualHeight = 1080;
    private const int TargetFps = 60;

    private const int InitialCapacity = 6000;
    private const int MinCapacity = 200;
    private const int MaxCapacity = 200_000;
    private const float CapacityChangeRatePerSecond = 8000f;

    private readonly GraphicsDeviceManager _graphics;
    private ViewportAdapter2D _viewportAdapter = null!;
    private Camera2D _camera = null!;
    private PrimitiveInput _input = null!;
    private Primitive2DBatch _batch2d = null!;
    private FrameLimiter _limiter;

    private FireSim _fire = null!;
    private float _capacityChangeAccumulator;

    private static readonly Color HotColor = ColorUtil.FromTemperature(9000f);
    private static readonly Color EmberColor = ColorUtil.FromTemperature(1300f);
    private static readonly Color SmokeColor = new(40, 40, 40);

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
        _fire = new FireSim(VirtualWidth, VirtualHeight, InitialCapacity);

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
        HandleCapacityChange(dt);

        _fire.EmitterX = VirtualWidth * 0.5f + MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 0.3f) * 300f;
        _fire.Update(dt);

        base.Update(gameTime);
    }

    private void HandleCapacityChange(float dt)
    {
        int direction = 0;
        if (_input.IsKeyDown(Keys.Up)) direction += 1;
        if (_input.IsKeyDown(Keys.Down)) direction -= 1;
        if (direction == 0) { _capacityChangeAccumulator = 0f; return; }

        _capacityChangeAccumulator += CapacityChangeRatePerSecond * dt * direction;
        int step = (int)_capacityChangeAccumulator;
        if (step == 0) return;

        _capacityChangeAccumulator -= step;
        int newCapacity = Math.Clamp(_fire.Capacity + step, MinCapacity, MaxCapacity);
        if (newCapacity != _fire.Capacity) _fire.SetCapacity(newCapacity);
    }

    protected override void Draw(GameTime gameTime)
    {
        _batch2d.ClearLetterboxed(_viewportAdapter, backgroundColor: Color.Black);

        _batch2d.Begin(_camera.GetTransformMatrix(), BlendState.Additive);
        DrawFire();
        _batch2d.End();

        _batch2d.Begin(_camera.GetTransformMatrix());
        DrawStats();
        _batch2d.End();

        _limiter.EndFrame();
        base.Draw(gameTime);
    }

    private void DrawFire()
    {
        foreach (FireSim.Particle p in _fire.Particles)
        {
            float ageFraction = FireSim.AgeFraction(p);

            // The first 70% of a particle's life cools from white-hot to ember orange/red; the
            // last 30% fades from ember toward smoke while shrinking away, instead of just
            // popping out of existence at full size.
            Color color;
            float alpha;
            if (ageFraction < 0.7f)
            {
                color = ColorUtil.Lerp(HotColor, EmberColor, ageFraction / 0.7f);
                alpha = 1f;
            }
            else
            {
                float smokeFraction = (ageFraction - 0.7f) / 0.3f;
                color = ColorUtil.Lerp(EmberColor, SmokeColor, smokeFraction);
                alpha = 1f - smokeFraction;
            }

            float radius = FireSim.BaseParticleRadius * MathHelper.Lerp(1f, 0.3f, ageFraction);
            Color glow = color * alpha;
            _batch2d.FillCircleGradient(p.Position, radius, glow, glow * 0.15f);
        }
    }

    private void DrawStats()
    {
        string stats = string.Format(CultureInfo.InvariantCulture,
            "Particles: {0:N0} / {1:N0} cap   FPS: {2:F0} avg / {3:F0} cur   Frame: {4:F2}ms avg",
            _fire.LiveCount, _fire.Capacity, _limiter.AverageFps, _limiter.CurrentFps, _limiter.AverageFrameTimeMs);
        _batch2d.DrawString(stats, new Vector2(16, 16), 2f, Color.White);
        _batch2d.DrawString("Up/Down: change max particles   Esc: quit", new Vector2(16, 44), 1.6f, Color.White);
    }
}
