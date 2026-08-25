using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace PrimitiveBase;

public readonly record struct RenderContext(
        GraphicsDevice GraphicsDevice,
        GraphicsDeviceManager Graphics,
        SpriteBatch SpriteBatch,
        Primitive2DBatch Batch2D);

public class Game1 : Game
{
    // Screen
    private const string Title = "Base Template";
    private const int VirtualWidth = 1920;
    private const int VirtualHeight = 1080;
    private const int TargetFps = 60;

    // Render context
    private readonly GraphicsDeviceManager _graphics;
    private ViewportAdapter2D _viewportAdapter;
    private Camera2D _camera;
    private PrimitiveInput _input;
    private RenderContext _render;

    private FrameLimiter _limiter;

    public Game1()
    {
        // windows init
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

        // Content
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        // FPS
        _limiter = new(
            game: this,
            targetFps: TargetFps,
            maxFrameTime: 1f / 30f
        );
    }

    protected override void Initialize()
    {
        // viewport and input handler
        _viewportAdapter = new BoxingViewportAdapter2D(GraphicsDevice, VirtualWidth, VirtualHeight);
        _camera = new Camera2D(_viewportAdapter) { Offset = Vector2.Zero };
        _input = new PrimitiveInput(Window);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        // Load render classes
        _render = new RenderContext(
            GraphicsDevice,
            _graphics,
            new SpriteBatch(GraphicsDevice),
            new Primitive2DBatch(GraphicsDevice));

        // load assets

    }

    protected override void Update(GameTime gameTime)
    {
        // start new frame
        float deltaTime = _limiter.BeginFrame();
        _input.Update(deltaTime);

        // close
        if (_input.IsKeyDown(Keys.Escape))
            Exit();

        // Logic

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        // Black bars and clear background
        _render.Batch2D.ClearLetterboxed(_viewportAdapter, backgroundColor: Palette.Background);

        _render.Batch2D.Begin(_camera.GetTransformMatrix());


        var (inner, outer) = Palette.GradientPairs[0];
        _render.Batch2D.FillCircleGradient(new Vector2(VirtualWidth / 2f, VirtualHeight / 2f), 150f, inner, outer);


        _render.Batch2D.End();

        // End Frame
        _limiter.EndFrame();
        base.Draw(gameTime);
    }
}
