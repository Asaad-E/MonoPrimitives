using System;
using System.Globalization;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace BoidsSwarm;

/// <summary>
/// A flocking simulation built for scale rather than a fixed boid count: thousands of boids
/// double-buffered and stepped in parallel across every CPU core, neighbor queries answered by a
/// counting-sort spatial hash instead of a brute-force O(boids^2) scan, and a Perlin-noise flow
/// field the whole flock drifts along. Every behavior parameter is retunable live via the ImGui
/// panel (drag the sliders and watch the flock/FPS react), and Up/Down ramps the boid count so
/// you can watch <see cref="FrameLimiter.AverageFps"/> and find this machine's real ceiling.
/// </summary>
public class Game1 : Game
{
    private const string Title = "Boids Swarm";
    private const int VirtualWidth = 1920;
    private const int VirtualHeight = 1080;
    private const int TargetFps = 60;

    private const int InitialBoidCount = 5000;
    private const int MinBoidCount = 100;
    private const int MaxBoidCount = 100_000;
    private const float CountChangeRatePerSecond = 8000f;
    private const float BoidRadius = 3.5f;

    private readonly GraphicsDeviceManager _graphics;
    private ViewportAdapter2D _viewportAdapter = null!;
    private Camera2D _camera = null!;
    private PrimitiveInput _input = null!;
    private Primitive2DBatch _batch2d = null!;
    private FrameLimiter _limiter;
    private ImGuiRenderer _imGui = null!;

    private BoidSwarm _swarm = null!;
    private float _countChangeAccumulator;
    private bool _showFlowField;

    public Game1()
    {
        // Deliberately smaller than the virtual resolution, and no pixelPerfect on the viewport
        // adapter below: a window sized to exactly match VirtualWidth/Height sounds crisper, but
        // depends on the OS actually granting that size -- a title bar and taskbar can force a
        // smaller real window than requested, and pixelPerfect never scales *below* 1x, only up,
        // so the excess gets silently cropped instead of shrunk to fit (caught here: a settled
        // fluid pool sitting near the bottom edge was invisible on a screen where the window
        // couldn't actually reach the full 1080px tall). Continuous scaling has no such failure
        // mode -- worth a little softness over content that can silently vanish.
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

        // A generous cap: protects the simulation's own dt from a huge step after a real stall,
        // without hiding the true frame cost from the FPS readouts below -- those are fed the raw,
        // unclamped time every BeginFrame(), which is exactly what "how far can this go" wants.
        _limiter = new FrameLimiter(this, targetFps: TargetFps, maxFrameTime: 1f / 15f);
    }

    protected override void Initialize()
    {
        _viewportAdapter = new BoxingViewportAdapter2D(GraphicsDevice, VirtualWidth, VirtualHeight);
        _camera = new Camera2D(_viewportAdapter) { Offset = Vector2.Zero };
        _input = new PrimitiveInput(Window);
        _swarm = new BoidSwarm(VirtualWidth, VirtualHeight, InitialBoidCount);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _batch2d = new Primitive2DBatch(GraphicsDevice);
        _imGui = new ImGuiRenderer(this);
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = _limiter.BeginFrame();
        _input.Update(dt);

        bool imguiWantsKeyboard = ImGui.GetIO().WantCaptureKeyboard;
        if (!imguiWantsKeyboard)
        {
            if (_input.IsKeyDown(Keys.Escape)) Exit();
            if (_input.IsKeyPressed(Keys.R)) _swarm.Reseed();
            if (_input.IsKeyPressed(Keys.F)) _showFlowField = !_showFlowField;
            HandleCountChange(dt);
        }

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
        int newCount = Math.Clamp(_swarm.Count + step, MinBoidCount, MaxBoidCount);
        if (newCount != _swarm.Count) _swarm.SetCount(newCount);
    }

    protected override void Draw(GameTime gameTime)
    {
        _batch2d.ClearLetterboxed(_viewportAdapter, backgroundColor: Palette.Background);

        _batch2d.Begin(_camera.GetTransformMatrix());
        if (_showFlowField) DrawFlowField();
        if (_swarm.ProfilingEnabled)
        {
            using (new DebugTimer($"Draw boids ({_swarm.Count:N0})")) DrawBoids();
        }
        else
        {
            DrawBoids();
        }
        DrawStats();
        _batch2d.End();

        if (_swarm.ProfilingEnabled)
        {
            using (new DebugTimer("ImGui render"))
            {
                _imGui.BeforeLayout(gameTime);
                BuildImGuiPanel();
                _imGui.AfterLayout();
            }
        }
        else
        {
            _imGui.BeforeLayout(gameTime);
            BuildImGuiPanel();
            _imGui.AfterLayout();
        }

        _limiter.EndFrame();
        base.Draw(gameTime);
    }

    private void DrawBoids()
    {
        foreach (BoidSwarm.Boid b in _swarm.Boids)
        {
            float heading = b.Velocity.LengthSquared() > 1e-4f ? b.Velocity.Angle() : 0f;
            _batch2d.DrawTriangle(b.Position, BoidRadius, b.Color, Color.Black, rotation: heading);
        }
    }

    private void DrawFlowField()
    {
        ReadOnlySpan<Vector2> field = _swarm.FlowField;
        int cols = _swarm.FlowFieldCols, rows = _swarm.FlowFieldRows;
        float cellW = (float)VirtualWidth / cols, cellH = (float)VirtualHeight / rows;
        Color lineColor = new(255, 255, 255, 60);
        float len = MathF.Min(cellW, cellH) * 0.4f;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                Vector2 center = new((x + 0.5f) * cellW, (y + 0.5f) * cellH);
                _batch2d.DrawLine(center, center + field[y * cols + x] * len, lineColor);
            }
        }
    }

    private void DrawStats()
    {
        string stats = string.Format(CultureInfo.InvariantCulture,
            "Boids: {0:N0}   FPS: {1:F0} avg / {2:F0} cur   Frame: {3:F2}ms avg",
            _swarm.Count, _limiter.AverageFps, _limiter.CurrentFps, _limiter.AverageFrameTimeMs);
        _batch2d.DrawString(stats, new Vector2(16, 16), 2f, Color.White);
        _batch2d.DrawString("Up/Down: change count   R: reseed   F: flow field   Esc: quit", new Vector2(16, 44), 1.6f, Color.White);
    }

    private void BuildImGuiPanel()
    {
        ImGui.Begin("Boids Swarm");

        ImGui.Text($"Boids: {_swarm.Count:N0}");
        ImGui.Text($"FPS: {_limiter.AverageFps:F0} avg / {_limiter.CurrentFps:F0} cur");
        ImGui.Text($"Frame time: {_limiter.AverageFrameTimeMs:F2} ms avg");
        ImGui.Separator();

        // SetCount reallocates 3 arrays sized by the new count, so a fast drag across a huge
        // range does briefly stutter -- applying only "IsItemDeactivatedAfterEdit" (release) would
        // avoid that, but didn't reliably fire in this ImGui.NET version; applying on every actual
        // change (SliderInt returning true) is the plain, unambiguous pattern instead.
        int count = _swarm.Count;
        if (ImGui.SliderInt("Boid Count", ref count, MinBoidCount, MaxBoidCount))
            _swarm.SetCount(count);

        float perception = _swarm.PerceptionRadius;
        if (ImGui.SliderFloat("Perception Radius", ref perception, 4f, 80f)) _swarm.PerceptionRadius = perception;

        float separationRadius = _swarm.SeparationRadius;
        if (ImGui.SliderFloat("Separation Radius", ref separationRadius, 1f, 40f)) _swarm.SeparationRadius = separationRadius;

        float maxSpeed = _swarm.MaxSpeed;
        if (ImGui.SliderFloat("Max Speed", ref maxSpeed, 20f, 400f)) _swarm.MaxSpeed = maxSpeed;

        float maxForce = _swarm.MaxForce;
        if (ImGui.SliderFloat("Max Force", ref maxForce, 20f, 600f)) _swarm.MaxForce = maxForce;

        float sepWeight = _swarm.SeparationWeight;
        if (ImGui.SliderFloat("Separation Weight", ref sepWeight, 0f, 4f)) _swarm.SeparationWeight = sepWeight;

        float aliWeight = _swarm.AlignmentWeight;
        if (ImGui.SliderFloat("Alignment Weight", ref aliWeight, 0f, 4f)) _swarm.AlignmentWeight = aliWeight;

        float cohWeight = _swarm.CohesionWeight;
        if (ImGui.SliderFloat("Cohesion Weight", ref cohWeight, 0f, 4f)) _swarm.CohesionWeight = cohWeight;

        float flowWeight = _swarm.FlowFieldWeight;
        if (ImGui.SliderFloat("Flow Field Weight", ref flowWeight, 0f, 3f)) _swarm.FlowFieldWeight = flowWeight;

        ImGui.Separator();
        ImGui.Checkbox("Show Flow Field", ref _showFlowField);
        if (ImGui.Button("Reseed")) _swarm.Reseed();

        ImGui.Separator();
        bool profiling = _swarm.ProfilingEnabled;
        if (ImGui.Checkbox("Profile to Console (DebugTimer)", ref profiling)) _swarm.ProfilingEnabled = profiling;
        if (profiling)
            ImGui.TextColored(new System.Numerics.Vector4(1f, 0.7f, 0.3f, 1f), "Printing every frame -- expect lower FPS while this is on.");

        ImGui.End();
    }
}
