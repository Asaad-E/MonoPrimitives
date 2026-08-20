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

    // 2D shape gallery — separate camera/input from the 3D one above, toggled with Tab.
    private Camera2D _camera2d;
    private PrimitiveInput _input2d;
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
        _input2d = new PrimitiveInput();
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

    // WASD pans, mouse wheel zooms, left-click-drag pans — panning is clamped to
    // [0,0]..gallery size so you can't scroll past the content (Camera2D.Offset stays at
    // zero, so Target itself is the world point shown at the screen's top-left corner).
    private void UpdateGallery2DCamera(float deltaTime)
    {
        _input2d.Update(deltaTime);

        float panSpeed = 700f * deltaTime / MathF.Max(_camera2d.Zoom, 0.05f);
        Vector2 move = _input2d.GetVector2(Keys.A, Keys.D, Keys.W, Keys.S) * panSpeed;
        _camera2d.Target += move;

        if (_input2d.MouseScrollDelta != 0)
            _camera2d.Zoom += _input2d.MouseScrollDelta * 0.03f * deltaTime;

        if (_input2d.IsMouseButtonDown(MouseButton.Left))
            _camera2d.Target -= _input2d.MouseDelta / MathF.Max(_camera2d.Zoom, 0.05f);

        _camera2d.TargetBounds = new Rectangle(0, 0, (int)MathF.Max(_gallery2DSize.X, 1f), (int)MathF.Max(_gallery2DSize.Y, 1f));
        _camera2d.ClampToBounds();
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

            int slices = 40;
            float spacing = 1f;
            _primitive3DBatch.DrawGridXZ(slices, spacing);
            _primitive3DBatch.DrawAxis(slices * spacing * 0.5f);

            DrawShapeGallery();

            _primitive3DBatch.End();
        }

        base.Draw(gameTime);
    }

    // One of every shape in a grid on top of the ground plane, for visual regression
    // checking after a library change — run the sample and eyeball each cell.
    private static readonly float[,] _heightmapPatch =
    {
        { 0f, 0.4f, 0.2f },
        { 0.3f, 0.9f, 0.1f },
        { 0f, 0.5f, 0f }
    };

    private void DrawShapeGallery()
    {
        const float cellSize = 6f;
        const int columns = 6;

        (string Name, Action<Vector3> Draw)[] shapes =
        {
            ("Cube", p => _primitive3DBatch.DrawCube(p + Vector3.UnitY * 1f, Vector3.One * 2f, Color.Red, Color.Black)),
            ("Sphere", p => _primitive3DBatch.DrawSphere(p + Vector3.UnitY * 1.5f, 1.5f, 24, 24, Color.Orange, Color.Black)),
            ("Cylinder", p => _primitive3DBatch.DrawCylinder(p, 1.2f, 1.2f, 3f, 24, Color.Blue, Color.Black)),
            ("Cone", p => _primitive3DBatch.DrawCylinder(p, 0f, 1.2f, 3f, 24, Color.Purple, Color.Black)),
            ("Capsule", p => _primitive3DBatch.DrawCapsule(p + Vector3.UnitY * 1f, p + Vector3.UnitY * 3f, 1f, 24, 12, Color.Green, Color.Black)),
            ("Torus", p => _primitive3DBatch.DrawTorus(p + Vector3.UnitY * 1.5f, 1.3f, 0.5f, 24, 24, Color.MediumPurple, Color.Black)),
            ("BoundingBox", p => _primitive3DBatch.DrawBoundingBox(new BoundingBox(p, p + new Vector3(2f, 2f, 2f)), Color.Yellow, Color.Black)),
            ("Plane", p => _primitive3DBatch.DrawPlane(p + Vector3.UnitY * 1.5f, new Vector2(2.5f, 2.5f), Color.Gray, Color.Black)),
            ("Circle3D", p => _primitive3DBatch.DrawCircle3D(p + Vector3.UnitY * 1.5f, 1.5f, Vector3.UnitX, 90f, 32, Color.Teal, Color.Black)),
            ("Triangle3D", p => _primitive3DBatch.DrawTriangle3D(p, p + new Vector3(2f, 0, 0), p + new Vector3(1f, 2.5f, 0), Color.Gold, Color.Black)),
            ("Heightmap", p => _primitive3DBatch.DrawHeightmap(_heightmapPatch, p, new Vector2(1f, 1f), Color.SaddleBrown, Color.Black)),
            ("Arrow3D", p => _primitive3DBatch.DrawArrow3D(p, p + Vector3.UnitY * 3f, 0.35f, 12, Color.Crimson)),
        };

        int columnsUsed = Math.Min(columns, shapes.Length);
        int rows = (shapes.Length + columnsUsed - 1) / columnsUsed;
        float originX = -(columnsUsed - 1) * cellSize * 0.5f;
        float originZ = -(rows - 1) * cellSize * 0.5f;

        for (int i = 0; i < shapes.Length; i++)
        {
            int col = i % columnsUsed;
            int row = i / columnsUsed;
            Vector3 cellPos = new(originX + col * cellSize, 0f, originZ + row * cellSize);
            shapes[i].Draw(cellPos);
        }
    }
}
