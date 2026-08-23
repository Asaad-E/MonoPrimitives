#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using MonoPrimitives;
using MonoPrimitives.Primitives2D;
using MonoPrimitives.Primitives3D;

namespace ViewportTest;

/// <summary>
/// Visual test for every <see cref="ViewportAdapter2D"/> configuration, 2D and 3D. One global
/// evaluation, applied identically to all 5 modes (no adapter, Default, Window, Boxing,
/// Scaling): draw the virtual canvas bounds, a marker at its center and at (0,0), then prove
/// screen&lt;-&gt;world conversion both ways — a crosshair follows the mouse via ScreenToWorld,
/// and a fixed world marker's WorldToScreen position is drawn as a HUD dot, so any drift
/// between the two is immediately visible. Same content for 3D via a raycast against the
/// ground plane instead of a 2D transform. Keys 1-5 switch adapter mode, Tab switches 2D/3D.
/// </summary>
public class Game1 : Game
{
    private const int WindowWidth = 1000;
    private const int WindowHeight = 700;
    private const int VirtualWidth = 480;
    private const int VirtualHeight = 270;

    private GraphicsDeviceManager _graphics;
    private Primitive2DBatch _batch2d = null!;
    private Primitive3DBatch _batch3d = null!;
    private PrimitiveInput _input = null!;

    private enum Mode { None, Default, Window, Boxing, Scaling }
    private Mode _mode = Mode.Boxing;
    private bool _show3D;
    private bool _tabWasDown;

    private ViewportAdapter2D? _adapter;
    private Camera2D _camera2d = Camera2D.CreateDefault();
    private Camera3D _camera3d = new(new Vector3(300, 260, 300), Vector3.Zero, Vector3.Up, 55f);

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = WindowWidth, PreferredBackBufferHeight = WindowHeight };
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
    }

    protected override void Initialize()
    {
        _batch2d = new Primitive2DBatch(GraphicsDevice);
        _batch3d = new Primitive3DBatch(GraphicsDevice) { LightingEnabled = true, AmbientLight = 0.55f, LightDirection = new Vector3(-0.4f, -1f, -0.3f) };
        _input = new PrimitiveInput();
        RebuildForMode();
        base.Initialize();
    }

    private void RebuildForMode()
    {
        _adapter = _mode switch
        {
            Mode.None => null,
            Mode.Default => new DefaultViewportAdapter2D(GraphicsDevice),
            Mode.Window => new WindowViewportAdapter2D(GraphicsDevice, Window),
            Mode.Boxing => new BoxingViewportAdapter2D(GraphicsDevice, VirtualWidth, VirtualHeight),
            Mode.Scaling => new ScalingViewportAdapter2D(GraphicsDevice, VirtualWidth, VirtualHeight),
            _ => null,
        };

        _camera2d = _adapter is not null
            ? new Camera2D(_adapter, target: new Vector2(VirtualWidth * 0.5f, VirtualHeight * 0.5f), zoom: 1f)
            : new Camera2D(target: Vector2.Zero, offset: new Vector2(WindowWidth * 0.5f, WindowHeight * 0.5f), zoom: 1f);

        _camera3d = _adapter is not null
            ? new Camera3D(_adapter, new Vector3(300, 260, 300), Vector3.Zero, Vector3.Up, 55f)
            : new Camera3D(new Vector3(300, 260, 300), Vector3.Zero, Vector3.Up, 55f);
    }

    protected override void Update(GameTime gameTime)
    {
        var kb = Keyboard.GetState();
        if (kb.IsKeyDown(Keys.D1) && _mode != Mode.None) { _mode = Mode.None; RebuildForMode(); }
        if (kb.IsKeyDown(Keys.D2) && _mode != Mode.Default) { _mode = Mode.Default; RebuildForMode(); }
        if (kb.IsKeyDown(Keys.D3) && _mode != Mode.Window) { _mode = Mode.Window; RebuildForMode(); }
        if (kb.IsKeyDown(Keys.D4) && _mode != Mode.Boxing) { _mode = Mode.Boxing; RebuildForMode(); }
        if (kb.IsKeyDown(Keys.D5) && _mode != Mode.Scaling) { _mode = Mode.Scaling; RebuildForMode(); }

        bool tabDown = kb.IsKeyDown(Keys.Tab);
        if (tabDown && !_tabWasDown) _show3D = !_show3D;
        _tabWasDown = tabDown;

        _input.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _adapter?.Reset(); // start from the full window; the scene draw narrows it again if boxed
        GraphicsDevice.Clear(Palette.Background);

        Vector2 rawMouse = _input.MousePosition;

        if (_show3D) Draw3DScene(rawMouse);
        else Draw2DScene(rawMouse);

        _adapter?.Reset(); // HUD always covers the full window, regardless of letterboxing
        DrawHud(rawMouse);

        base.Draw(gameTime);
    }

    private void Draw2DScene(Vector2 rawMouse)
    {
        // No Apply() here: Camera2D.GetTransformMatrix() already folds in the adapter's
        // GetScaleMatrix() (scale + offset) when one is set, so narrowing the device viewport
        // too would double-apply the offset (only visible for adapters with a nonzero Offset,
        // i.e. Boxing). See ViewportAdapter2D.Apply's doc comment.
        _batch2d.Begin(_camera2d.GetTransformMatrix());

        _batch2d.DrawGrid(24, 20f, new Vector2(VirtualWidth * 0.5f, VirtualHeight * 0.5f), new Color(0.6f, 0.6f, 0.65f, 0.4f), new Color(0.85f, 0.85f, 0.9f, 0.6f));
        _batch2d.BorderRectangle(0, 0, VirtualWidth, VirtualHeight, Color.White, 3f);

        _batch2d.FillCircle(new Vector2(VirtualWidth * 0.5f, VirtualHeight * 0.5f), 10f, Palette.Sunflower);
        _batch2d.FillCircle(Vector2.Zero, 8f, Palette.Emerald);
        _batch2d.DrawString($"(0,0)", new Vector2(12, 8), 3f, Palette.Emerald);
        _batch2d.FillCircle(new Vector2(VirtualWidth, VirtualHeight), 8f, Palette.Alizarin);

        Vector2 mouseWorld = _camera2d.ScreenToWorld(rawMouse);
        _batch2d.DrawLine(mouseWorld + new Vector2(-10, 0), mouseWorld + new Vector2(10, 0), 2f, Palette.PeterRiver);
        _batch2d.DrawLine(mouseWorld + new Vector2(0, -10), mouseWorld + new Vector2(0, 10), 2f, Palette.PeterRiver);

        // Round-trip proof: this fixed virtual-space point's WorldToScreen result is drawn as a
        // HUD dot below -- it should always land exactly on this same visual spot on screen.
        _batch2d.FillCircle(new Vector2(VirtualWidth * 0.25f, VirtualHeight * 0.75f), 6f, Palette.Amethyst);

        _batch2d.End();
    }

    private void Draw3DScene(Vector2 rawMouse)
    {
        // Begin(camera) applies the adapter internally (camera.ViewportAdapter?.Apply()) --
        // no separate call needed here.
        _batch3d.Begin(_camera3d);

        _batch3d.DrawGridXZ(30, 20f);
        _batch3d.DrawAxis(200f);
        _batch3d.FillCube(Vector3.Zero, Vector3.One * 24f, Palette.Sunflower);

        Ray ray = _camera3d.GetScreenToWorldRay(rawMouse, GraphicsDevice.Viewport);
        RayCollision3D hit = Collision3D.GetRayCollisionPlane(ray, Vector3.Zero, Vector3.Up);
        if (hit.Hit)
            _batch3d.FillSphere(hit.Point, 8f, Palette.PeterRiver);

        _batch3d.FillSphere(new Vector3(100, 0, -100), 10f, Palette.Amethyst);

        _batch3d.End();
    }

    private static readonly (string Name, string Desc)[] ModeInfo =
    {
        ("1: NONE", "no adapter -- raw device viewport"),
        ("2: DEFAULT", "1:1 with device viewport"),
        ("3: WINDOW", "1:1 with GameWindow.ClientBounds"),
        ("4: BOXING", "480x270 virtual, letterboxed"),
        ("5: SCALING", "480x270 virtual, stretched"),
    };

    private void DrawHud(Vector2 rawMouse)
    {
        _batch2d.Begin();

        int modeIndex = (int)_mode;
        _batch2d.DrawString($"MODE {ModeInfo[modeIndex].Name} ({ModeInfo[modeIndex].Desc})", new Vector2(16, 16), 2f, Color.White);
        _batch2d.DrawString("1-5: switch adapter   Tab: switch 2D/3D scene", new Vector2(16, 44), 1.5f, Palette.Silver);
        _batch2d.DrawString($"Scene: {(_show3D ? "3D" : "2D")}", new Vector2(16, 66), 1.5f, Palette.Silver);
        _batch2d.DrawString($"Raw mouse (screen px): {rawMouse.X:F0}, {rawMouse.Y:F0}", new Vector2(16, 90), 1.5f, Palette.Concrete);

        if (!_show3D)
        {
            Vector2 mouseWorld = _camera2d.ScreenToWorld(rawMouse);
            _batch2d.DrawString($"Mouse -> world (virtual): {mouseWorld.X:F1}, {mouseWorld.Y:F1}", new Vector2(16, 112), 1.5f, Palette.PeterRiver);

            Vector2 fixedWorld = new(VirtualWidth * 0.25f, VirtualHeight * 0.75f);
            Vector2 backToScreen = _camera2d.WorldToScreen(fixedWorld);
            _batch2d.FillCircle(backToScreen, 5f, Palette.Amethyst);
            _batch2d.DrawString($"Fixed world (120,202.5) -> screen: {backToScreen.X:F0}, {backToScreen.Y:F0}", new Vector2(16, 134), 1.5f, Palette.Amethyst);
            _batch2d.DrawString("(the purple dot here should sit exactly on the purple dot in the scene)", new Vector2(16, 152), 1.2f, Palette.Silver);
        }
        else
        {
            Ray ray = _camera3d.GetScreenToWorldRay(rawMouse, GraphicsDevice.Viewport);
            RayCollision3D hit = Collision3D.GetRayCollisionPlane(ray, Vector3.Zero, Vector3.Up);
            string hitText = hit.Hit ? $"{hit.Point.X:F1}, {hit.Point.Z:F1}" : "(no hit)";
            _batch2d.DrawString($"Mouse -> ground-plane world (X,Z): {hitText}", new Vector2(16, 112), 1.5f, Palette.PeterRiver);

            Vector2 fixedScreen = _camera3d.WorldToScreen(new Vector3(100, 0, -100), GraphicsDevice.Viewport);
            _batch2d.FillCircle(fixedScreen, 5f, Palette.Amethyst);
            _batch2d.DrawString($"Fixed world (100,0,-100) -> screen: {fixedScreen.X:F0}, {fixedScreen.Y:F0}", new Vector2(16, 134), 1.5f, Palette.Amethyst);
            _batch2d.DrawString("(the purple dot here should sit exactly on the purple sphere in the scene)", new Vector2(16, 152), 1.2f, Palette.Silver);
        }

        _batch2d.End();
    }
}
