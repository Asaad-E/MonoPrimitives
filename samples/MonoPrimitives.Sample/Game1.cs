using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.ViewportAdapters;

using MonoPrimitives;
using MonoPrimitives.Primitives3D;
using MonoPrimitives.Primitives2D;

namespace MonogameLibs;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private PrimitiveBatch _primitiveBatch;
    private Primitive3DBatch _primitive3DBatch;
    private Camera3D _camera3d;


    private ViewportAdapter _viweport;

    private RenderTarget2D _target;

    private OrthographicCamera _camera;

    // 2D shape gallery — separate camera from the 3D one above, toggled with Tab.
    private Camera2D _camera2d;
    private bool _show2DGallery = true;
    private Vector2 _gallery2DSize;
    private bool _tabWasDown;

    private const int ScreenSizeX = 1280;
    private const int SCreenSizeY = 720;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = ScreenSizeX,
            PreferredBackBufferHeight = SCreenSizeY,
            PreferMultiSampling = true,
            GraphicsProfile = GraphicsProfile.HiDef
        };

        _graphics.PreparingDeviceSettings += (sender, e) =>
        {
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 4;
        };


        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        // TODO: Add your initialization logic here


        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _primitiveBatch = new PrimitiveBatch(GraphicsDevice);
        _primitive3DBatch = new Primitive3DBatch(GraphicsDevice);
        _primitive3DBatch.LightingEnabled = true;
        _primitive3DBatch.AmbientLight = 0.5f;
        _primitive3DBatch.LightDirection = new Vector3(0, -1, 0);


        _viweport = new BoxingViewportAdapter(Window, GraphicsDevice, 800, 720);
        _camera = new OrthographicCamera(_viweport);
        _camera3d = new Camera3D(
            position: new Vector3(0, 22, 38),
            target: Vector3.Zero,
            up: Vector3.Up,
            fovy: 50
        );

        _camera2d = new Camera2D(target: Vector2.Zero, offset: Vector2.Zero, zoom: 1f);
        // TODO: use this.Content to load your game content here
    }

    public Vector2 Pos;

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            _camera.Zoom += 0.1f;
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        bool tabDown = Keyboard.GetState().IsKeyDown(Keys.Tab);
        if (tabDown && !_tabWasDown) _show2DGallery = !_show2DGallery;
        _tabWasDown = tabDown;

        if (_show2DGallery)
            UpdateGallery2DCamera(deltaTime);
        else
            _camera3d.Update(_camera3d.ReadDefaultInput(deltaTime), deltaTime);

        base.Update(gameTime);
    }

    // WASD pans, mouse wheel zooms, left-click-drag pans, all via Camera2D's own built-in
    // input reader — panning is clamped to [0,0]..gallery size so you can't scroll past the
    // content (Camera2D.Offset stays at zero, so Target itself is the world point shown at
    // the screen's top-left corner).
    private void UpdateGallery2DCamera(float deltaTime)
    {
        _camera2d.TargetBounds = new Rectangle(0, 0, (int)MathF.Max(_gallery2DSize.X, 1f), (int)MathF.Max(_gallery2DSize.Y, 1f));
        _camera2d.Update(deltaTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        // TODO: Add your drawing code here
        Pos = Mouse.GetState().Position.ToVector2();

        if (_show2DGallery)
        {
            GraphicsDevice.Clear(Palette.Background);

            _primitiveBatch.Begin(_camera2d.GetTransformMatrix());
            _primitiveBatch.DrawGrid(80, 40f);
            _primitiveBatch.DrawAxis(2000f);
            _gallery2DSize = Gallery2D.Draw(_primitiveBatch);
            _primitiveBatch.End();
        }
        else
        {
            GraphicsDevice.Clear(Color.White);

            _primitive3DBatch.Begin(_camera3d);

            // Large enough to cover Gallery3D's full extent (13 rows deep, up to 6 cells wide).
            int slices = 140;
            float spacing = 2f;
            _primitive3DBatch.DrawGridXZ(slices, spacing);
            _primitive3DBatch.DrawAxis(slices * spacing * 0.5f);

            Gallery3D.Draw(_primitive3DBatch);

            _primitive3DBatch.End();
        }

        base.Draw(gameTime);
    }
}
