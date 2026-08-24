using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace MonoPrimitives.Template
{
    public class Game1 : Game
    {
        private const int VirtualWidth = 1920;
        private const int VirtualHeight = 1080;
        private const int TargetFps = 60;

        private readonly GraphicsDeviceManager _graphics;
        private ViewportAdapter2D _viewportAdapter;
        private Camera2D _camera;
        private PrimitiveInput _input;
        private RenderContext _render;

        public Game1()
        {
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

            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / TargetFps);
        }

        protected override void Initialize()
        {

            _viewportAdapter = new BoxingViewportAdapter2D(GraphicsDevice, VirtualWidth, VirtualHeight);
            _camera = new Camera2D(_viewportAdapter) { Offset = Vector2.Zero };
            _input = new PrimitiveInput();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _render = new RenderContext(
                GraphicsDevice,
                _graphics,
                new SpriteBatch(GraphicsDevice),
                new Primitive2DBatch(GraphicsDevice));
        }

        protected override void Update(GameTime gameTime)
        {
            _input.Update(gameTime);

            if (_input.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
                Exit();

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            _render.Batch2D.ClearLetterboxed(_viewportAdapter, backgroundColor: Palette.Background);

            _render.Batch2D.Begin(_camera.GetTransformMatrix());

            var (inner, outer) = Palette.GradientPairs[0];
            _render.Batch2D.FillCircleGradient(new Vector2(VirtualWidth / 2f, VirtualHeight / 2f), 150f, inner, outer);

            _render.Batch2D.End();

            base.Draw(gameTime);
        }
    }
}
