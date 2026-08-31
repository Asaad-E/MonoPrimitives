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
    private Primitive2DBatch _primitiveBatch;
    private Primitive3DBatch _primitive3DBatch;
    private Camera3D _camera3d;


    private ViewportAdapter _viewport;

    private OrthographicCamera _camera;

    // 2D shape gallery — separate camera from the 3D one above, toggled with Tab. Camera2D is
    // constructed with its own ViewportAdapter2D (MonoGame.Extended-style: the adapter is a
    // constructor dependency, not a parameter threaded through every call) so screen<->world
    // conversions and mouse-drag panning stay correct if the window is resized/letterboxed.
    private ViewportAdapter2D _viewportAdapter2d;
    private Camera2D _camera2d;
    private readonly PrimitiveInput _input = new();
    private bool _show2DGallery = true;
    private Vector2 _gallery2DSize;
    private bool _tabWasDown;

    // A second, curated view alongside each dev gallery -- Showcase2D/Showcase3D, toggled with S.
    // Framed once on toggle-in via FitBounds (not every frame, so free-fly/pan still works
    // afterward), not part of the library: exists purely to produce clean README/guide
    // screenshots instead of the dev galleries' dense every-variant grids.
    private bool _showcaseMode;
    private bool _sWasDown;

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
        _primitiveBatch = new Primitive2DBatch(GraphicsDevice);
        _primitive3DBatch = new Primitive3DBatch(GraphicsDevice);
        _primitive3DBatch.LightingEnabled = true;
        _primitive3DBatch.AmbientLight = 0.5f;
        _primitive3DBatch.LightDirection = new Vector3(0, -1, 0);


        _viewport = new BoxingViewportAdapter(Window, GraphicsDevice, 800, 720);
        _camera = new OrthographicCamera(_viewport);

        // Shared between both cameras below: same window, same boxed rect either way, so there's
        // no reason for the 2D and 3D galleries to each carry their own adapter instance.
        _viewportAdapter2d = new BoxingViewportAdapter2D(GraphicsDevice, ScreenSizeX, SCreenSizeY);

        _camera3d = new Camera3D(
            _viewportAdapter2d,
            position: new Vector3(0, 22, 38),
            target: Vector3.Zero,
            up: Vector3.Up,
            fovy: 50
        );

        // Offset overridden back to zero after construction: the ctor defaults it to the
        // adapter's virtual center (Extended's own convention), but this gallery's pan/clamp
        // design (see UpdateGallery2DCamera) wants Target itself to be the world point drawn at
        // the screen's top-left corner instead.
        _camera2d = new Camera2D(_viewportAdapter2d, target: Vector2.Zero, zoom: 1f) { Offset = Vector2.Zero };
        // TODO: use this.Content to load your game content here
    }

    public Vector2 Pos;

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            _camera.Zoom += 0.1f;
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _input.Update(deltaTime);

        bool tabDown = Keyboard.GetState().IsKeyDown(Keys.Tab);
        if (tabDown && !_tabWasDown) _show2DGallery = !_show2DGallery;
        _tabWasDown = tabDown;

        bool sDown = Keyboard.GetState().IsKeyDown(Keys.S);
        if (sDown && !_sWasDown) ToggleShowcaseMode();
        _sWasDown = sDown;

        if (_show2DGallery)
            UpdateGallery2DCamera(deltaTime);
        else
            _camera3d.UpdateWithInput(_input, deltaTime);

        base.Update(gameTime);
    }

    // WASD pans, mouse wheel zooms, left-click-drag pans, all via Camera2D's own built-in
    // input reader — panning is clamped to [0,0]..gallery size so you can't scroll past the
    // content (Camera2D.Offset stays at zero, so Target itself is the world point shown at
    // the screen's top-left corner).
    private void UpdateGallery2DCamera(float deltaTime)
    {
        _camera2d.TargetBounds = new Rectangle(0, 0, (int)MathF.Max(_gallery2DSize.X, 1f), (int)MathF.Max(_gallery2DSize.Y, 1f));
        _camera2d.UpdateWithInput(_input, deltaTime);
    }

    // FitBounds runs once here (not every Draw) so the showcase starts framed correctly but the
    // usual pan/zoom (2D) or free-fly (3D) controls still work normally afterward.
    private void ToggleShowcaseMode()
    {
        _showcaseMode = !_showcaseMode;

        if (_show2DGallery)
        {
            // FitBounds assumes the default Offset=viewport-center convention; the gallery's own
            // pan/clamp design (UpdateGallery2DCamera) instead wants Offset=Zero so Target is the
            // world point drawn at the screen's top-left corner -- swap conventions with the mode.
            if (_showcaseMode)
            {
                Vector2 size = Showcase2D.GetContentSize();
                _camera2d.Offset = new Vector2(ScreenSizeX / 2f, SCreenSizeY / 2f);
                _camera2d.FitBounds(new MonoPrimitives.RectangleF(0, 0, size.X, size.Y), 40f, GraphicsDevice);
            }
            else
            {
                _camera2d.Offset = Vector2.Zero;
            }
        }
        else if (_showcaseMode)
        {
            // Not FitBounds: its sphere-fit is conservative for a flat, wide layout like this row
            // (bounded mostly by a large horizontal diagonal, tiny vertical extent), so it leaves
            // far more margin than a manually placed camera needs to. A narrower fovy than the
            // dev gallery's default 50 keeps the mostly-empty sky above the row out of frame.
            Vector3 center = Showcase3D.GetContentCenter();
            _camera3d.Fovy = 30f;
            _camera3d.Target = center;
            _camera3d.Position = center + new Vector3(0f, 12f, 32f);
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        // TODO: Add your drawing code here
        Pos = Mouse.GetState().Position.ToVector2();

        if (_show2DGallery)
        {
            // No Apply() here: Camera2D.GetTransformMatrix() already folds in the adapter's
            // GetScaleMatrix() (scale AND offset), so narrowing the device viewport too would
            // double-apply the letterbox offset. Clear the whole window so the letterbox bars
            // show this color too.
            GraphicsDevice.Clear(Palette.Background);

            _primitiveBatch.Begin(_camera2d.GetTransformMatrix());
            if (_showcaseMode)
            {
                _gallery2DSize = Showcase2D.Draw(_primitiveBatch);
            }
            else
            {
                _primitiveBatch.DrawGrid(80*4, 40f);
                _primitiveBatch.DrawAxis(2000f);
                _gallery2DSize = Gallery2D.Draw(_primitiveBatch);
            }
            _primitiveBatch.End();
        }
        else
        {
            // No manual Apply()/Reset() needed here: _camera3d was constructed with the same
            // _viewportAdapter2d instance the 2D branch uses, so Begin(_camera3d) re-applies the
            // same boxed rect automatically (Camera3D.ViewportAdapter).
            GraphicsDevice.Clear(Color.White);

            _primitive3DBatch.Begin(_camera3d);

            if (_showcaseMode)
            {
                Showcase3D.Draw(_primitive3DBatch);
            }
            else
            {
                // Large enough to cover Gallery3D's full extent (13 rows deep, up to 6 cells wide).
                int slices = 140;
                float spacing = 2f;
                _primitive3DBatch.DrawGridXZ(slices, spacing);
                _primitive3DBatch.DrawAxis(slices * spacing * 0.5f);

                Gallery3D.Draw(_primitive3DBatch);
            }

            _primitive3DBatch.End();
        }

        base.Draw(gameTime);
    }
}
