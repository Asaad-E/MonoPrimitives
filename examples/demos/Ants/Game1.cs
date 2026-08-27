using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace Ants;

/// <summary>
/// An ant colony finding food purely through stigmergy -- pheromone trails on a shared grid, not
/// direct ant-to-ant awareness (see <see cref="AntColony"/>). Watch a scattered, undirected search
/// give way to a small number of well-worn paths between the nest and each food source as weaker
/// trails decay faster than they're reinforced. Up/Down ramps the ant count live so you can watch
/// <see cref="FrameLimiter.AverageFps"/> and find this machine's real ceiling.
/// </summary>
public class Game1 : Game
{
    private const string Title = "Ants";
    private const int VirtualWidth = 1920;
    private const int VirtualHeight = 1080;
    private const int TargetFps = 60;
    private const float TrailCellSize = 12f;

    private const int InitialAntCount = 2000;
    private const int MinAntCount = 10;
    private const int MaxAntCount = 100_000;
    private const float CountChangeRatePerSecond = 4000f;
    private const float AntRadius = 3f;
    private const float TrailDrawThreshold = 0.05f; // skip drawing near-decayed-away cells

    private readonly GraphicsDeviceManager _graphics;
    private ViewportAdapter2D _viewportAdapter = null!;
    private Camera2D _camera = null!;
    private PrimitiveInput _input = null!;
    private Primitive2DBatch _batch2d = null!;
    private FrameLimiter _limiter;

    private AntColony _colony = null!;
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

        var food = new[]
        {
            new AntColony.FoodSource(new Vector2(VirtualWidth * 0.15f, VirtualHeight * 0.2f), 40f),
            new AntColony.FoodSource(new Vector2(VirtualWidth * 0.85f, VirtualHeight * 0.2f), 40f),
            new AntColony.FoodSource(new Vector2(VirtualWidth * 0.5f, VirtualHeight * 0.85f), 40f),
        };
        _colony = new AntColony(VirtualWidth, VirtualHeight, InitialAntCount, TrailCellSize, food);

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
        HandleCountChange(dt);

        _colony.Update(dt);

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
        int newCount = Math.Clamp(_colony.Count + step, MinAntCount, MaxAntCount);
        if (newCount != _colony.Count) _colony.SetCount(newCount);
    }

    protected override void Draw(GameTime gameTime)
    {
        _batch2d.ClearLetterboxed(_viewportAdapter, backgroundColor: Palette.Background);

        _batch2d.Begin(_camera.GetTransformMatrix(), BlendState.Additive);
        DrawTrails();
        _batch2d.End();

        _batch2d.Begin(_camera.GetTransformMatrix());
        DrawLandmarks();
        DrawAnts();
        DrawStats();
        _batch2d.End();

        _limiter.EndFrame();
        base.Draw(gameTime);
    }

    private void DrawTrails()
    {
        float[] home = _colony.HomeTrail, food = _colony.FoodTrail;
        int cols = _colony.GridCols, rows = _colony.GridRows;
        float cell = _colony.CellSize;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                int i = y * cols + x;
                float homeStrength = home[i], foodStrength = food[i];
                if (homeStrength < TrailDrawThreshold && foodStrength < TrailDrawThreshold) continue;

                Vector2 topLeft = new(x * cell, y * cell);
                if (homeStrength >= TrailDrawThreshold)
                {
                    byte a = (byte)MathHelper.Clamp(homeStrength * 40f, 0f, 160f);
                    _batch2d.FillRectangle(topLeft, new Vector2(cell, cell), Palette.BelizeHole * (a / 255f));
                }
                if (foodStrength >= TrailDrawThreshold)
                {
                    byte a = (byte)MathHelper.Clamp(foodStrength * 40f, 0f, 160f);
                    _batch2d.FillRectangle(topLeft, new Vector2(cell, cell), Palette.Nephritis * (a / 255f));
                }
            }
        }
    }

    private void DrawLandmarks()
    {
        _batch2d.BorderCircle(_colony.NestPosition, 30f, Palette.Clouds, thickness: 3f);
        foreach (AntColony.FoodSource food in _colony.FoodSources)
            _batch2d.BorderCircle(food.Position, food.Radius, Palette.Sunflower, thickness: 3f);
    }

    private void DrawAnts()
    {
        foreach (AntColony.Ant ant in _colony.Ants)
        {
            Color color = ant.State == AntColony.AntState.Returning ? Palette.Carrot : Palette.Emerald;
            _batch2d.FillTriangle(ant.Position, AntRadius, color, rotation: ant.Heading);
        }
    }

    private void DrawStats()
    {
        string stats = string.Format(CultureInfo.InvariantCulture,
            "Ants: {0:N0}   FPS: {1:F0} avg / {2:F0} cur   Frame: {3:F2}ms avg",
            _colony.Count, _limiter.AverageFps, _limiter.CurrentFps, _limiter.AverageFrameTimeMs);
        _batch2d.DrawString(stats, new Vector2(16, 16), 2f, Color.White);
        _batch2d.DrawString("Up/Down: change count   Esc: quit", new Vector2(16, 44), 1.6f, Color.White);
    }
}
