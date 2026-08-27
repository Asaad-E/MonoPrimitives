#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace WindowUtilTest;

/// <summary>
/// Visual + interactive test for <see cref="WindowUtil"/>. Runs a scripted verification pass on
/// startup (logs PASS/FAIL for each check to Console), then stays open for manual poking:
/// 1=Minimize, 2=Maximize, 3=Restore, 4=toggle 50% opacity, 5=set a solid-color icon,
/// 6=clipboard round-trip, 7=toggle cursor capture (move mouse to see the delta readout),
/// 8=re-print monitor info.
/// </summary>
public class Game1 : Game
{
    private const int WindowWidth = 1000;
    private const int WindowHeight = 500;

    private GraphicsDeviceManager _graphics;
    private Primitive2DBatch _batch2d = null!;
    private PrimitiveInput _input = null!;
    private Texture2D _icon = null!;

    private bool _cursorCaptured;
    private Point _lastCursorDelta;
    private string _lastClipboardRoundTrip = "(not tested yet)";
    private string _status = "starting...";

    // Auto-verification sequence: one step per ~0.8s, entirely Console-driven so it can be
    // checked headlessly (no one needs to be watching the window for this part to be useful).
    private float _autoTimer;
    private int _autoStep;
    private bool _autoDone;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = WindowWidth, PreferredBackBufferHeight = WindowHeight };
        IsMouseVisible = true;
        Window.Title = "WindowUtil test";
    }

    protected override void Initialize()
    {
        Window.AllowUserResizing = true; // MaximizeWindow is a no-op on Windows without this -- see its doc comment
        _batch2d = new Primitive2DBatch(GraphicsDevice);
        _input = new PrimitiveInput(Window);

        // Small solid-color icon for SetWindowIcon -- doesn't need to be pretty, just SurfaceFormat.Color.
        _icon = new Texture2D(GraphicsDevice, 32, 32, false, SurfaceFormat.Color);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Palette.Alizarin;
        _icon.SetData(pixels);

        Console.WriteLine("WindowUtil.IsAvailable = " + WindowUtil.IsAvailable);
        Console.WriteLine("WindowUtil.Diagnostics = " + WindowUtil.Diagnostics);

        base.Initialize();
    }

    protected override void Update(GameTime gameTime)
    {
        _input.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        RunAutoVerification(dt);

        if (_input.IsKeyPressed(Keys.D1)) { WindowUtil.MinimizeWindow(Window); _status = "MinimizeWindow() called"; }
        if (_input.IsKeyPressed(Keys.D2)) { WindowUtil.MaximizeWindow(Window); _status = "MaximizeWindow() called"; }
        if (_input.IsKeyPressed(Keys.D3)) { WindowUtil.RestoreWindow(Window); _status = "RestoreWindow() called"; }
        if (_input.IsKeyPressed(Keys.D4))
        {
            float current = WindowUtil.GetWindowOpacity(Window);
            WindowUtil.SetWindowOpacity(Window, current > 0.75f ? 0.5f : 1f);
            _status = "opacity toggled -> " + WindowUtil.GetWindowOpacity(Window).ToString("F2");
        }
        if (_input.IsKeyPressed(Keys.D5))
        {
            WindowUtil.SetWindowIcon(Window, _icon);
            _status = "SetWindowIcon() called (check the title bar/taskbar icon)";
        }
        if (_input.IsKeyPressed(Keys.D6))
        {
            string sent = "MonoPrimitives-" + Environment.TickCount;
            WindowUtil.SetClipboardText(sent);
            string received = WindowUtil.GetClipboardText();
            _lastClipboardRoundTrip = sent == received ? "OK (" + sent + ")" : "MISMATCH sent='" + sent + "' got='" + received + "'";
            _status = "clipboard round-trip: " + _lastClipboardRoundTrip;
        }
        if (_input.IsKeyPressed(Keys.D7))
        {
            _cursorCaptured = !_cursorCaptured;
            if (_cursorCaptured) WindowUtil.DisableCursor(this);
            else WindowUtil.EnableCursor(this);
            _status = _cursorCaptured ? "cursor captured -- move the mouse" : "cursor released";
        }
        if (_cursorCaptured)
            _lastCursorDelta = WindowUtil.GetCursorDelta(Window);

        if (_input.IsKeyPressed(Keys.D8))
            _status = DescribeMonitors();

        base.Update(gameTime);
    }

    private void RunAutoVerification(float dt)
    {
        if (_autoDone) return;
        _autoTimer += dt;
        if (_autoTimer < 0.8f) return;
        _autoTimer = 0f;

        switch (_autoStep)
        {
            case 0:
                Console.WriteLine("[auto] " + DescribeMonitors());
                break;
            case 1:
                Console.WriteLine("[auto] GetCurrentMonitorIndex = " + Safe(() => WindowUtil.GetCurrentMonitorIndex(Window).ToString()));
                break;
            case 2:
            {
                WindowUtil.SetWindowOpacity(Window, 0.5f);
                float readBack = WindowUtil.GetWindowOpacity(Window);
                bool ok = Math.Abs(readBack - 0.5f) < 0.05f;
                Console.WriteLine("[auto] opacity round-trip: set 0.50, read " + readBack.ToString("F2") + " -> " + (ok ? "PASS" : "FAIL"));
                break;
            }
            case 3:
                WindowUtil.SetWindowOpacity(Window, 1f);
                Console.WriteLine("[auto] opacity restored to 1.0, read back " + WindowUtil.GetWindowOpacity(Window).ToString("F2"));
                break;
            case 4:
            {
                string sent = "MonoPrimitives-autoverify-" + Environment.TickCount;
                WindowUtil.SetClipboardText(sent);
                string received = Safe(WindowUtil.GetClipboardText);
                bool ok = sent == received;
                Console.WriteLine("[auto] clipboard round-trip -> " + (ok ? "PASS" : "FAIL (" + received + ")"));
                break;
            }
            case 5:
                Console.WriteLine("[auto] IsWindowMinimized=" + WindowUtil.IsWindowMinimized(Window) + " IsWindowMaximized=" + WindowUtil.IsWindowMaximized(Window) + " (expected both false at startup)");
                break;
            case 6:
                WindowUtil.SetWindowIcon(Window, _icon);
                Console.WriteLine("[auto] SetWindowIcon() called with a solid-color 32x32 texture -- no exception thrown");
                break;
            case 7:
            {
                Rectangle bounds = Window.ClientBounds;
                Mouse.SetPosition(bounds.Width / 2, bounds.Height / 2);
                Point delta = WindowUtil.GetCursorDelta(Window);
                bool ok = delta == Point.Zero;
                Console.WriteLine("[auto] GetCursorDelta immediately after centering -> " + delta + " -> " + (ok ? "PASS" : "FAIL"));
                break;
            }
            case 8:
                WindowUtil.MaximizeWindow(Window);
                Console.WriteLine("[auto] MaximizeWindow() called");
                break;
            case 9:
                Console.WriteLine("[auto] IsWindowMaximized after MaximizeWindow -> " + WindowUtil.IsWindowMaximized(Window) + " (needs AllowUserResizing on Windows and a real window manager elsewhere -- expect false under a bare Xvfb CI display)");
                break;
            case 10:
                WindowUtil.RestoreWindow(Window);
                Console.WriteLine("[auto] RestoreWindow() called");
                break;
            case 11:
                Console.WriteLine("[auto] IsWindowMaximized after RestoreWindow -> " + WindowUtil.IsWindowMaximized(Window) + " (expect false)");
                break;
            case 12:
                WindowUtil.MinimizeWindow(Window);
                Console.WriteLine("[auto] MinimizeWindow() called");
                break;
            case 13:
                Console.WriteLine("[auto] IsWindowMinimized after MinimizeWindow -> " + WindowUtil.IsWindowMinimized(Window) + " (depends on a real window manager -- expect false under bare Xvfb)");
                break;
            case 14:
                WindowUtil.RestoreWindow(Window);
                Console.WriteLine("[auto] RestoreWindow() called -- IsWindowMinimized now " + WindowUtil.IsWindowMinimized(Window));
                break;
            case 15:
                Console.WriteLine("[auto] VERIFICATION SEQUENCE COMPLETE. Manual keys still active: 1=Min 2=Max 3=Restore 4=Opacity 5=Icon 6=Clipboard 7=CursorCapture 8=Monitors");
                _autoDone = true;
                break;
        }
        _autoStep++;
    }

    private static string Safe(Func<string> f)
    {
        try { return f(); }
        catch (Exception ex) { return "threw " + ex.GetType().Name + ": " + ex.Message; }
    }

    private string DescribeMonitors()
    {
        if (!WindowUtil.IsAvailable)
            return "WindowUtil unavailable: " + WindowUtil.Diagnostics;

        int count = WindowUtil.GetMonitorCount();
        string result = "Monitors: " + count;
        for (int i = 0; i < count; i++)
        {
            MonitorInfo m = WindowUtil.GetMonitorInfo(i);
            result += " | #" + m.Index + " '" + m.Name + "' " + m.Bounds + " @" + m.RefreshRate + "Hz";
        }
        return result;
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.Background);
        _batch2d.Begin();

        _batch2d.DrawString("WINDOWUTIL TEST", new Vector2(16, 12), 2f, Color.White);
        _batch2d.DrawString("1=Minimize 2=Maximize 3=Restore 4=Opacity 5=Icon 6=Clipboard 7=CursorCapture 8=Monitors", new Vector2(16, 40), 1.3f, Palette.Silver);

        _batch2d.DrawString("IsAvailable: " + WindowUtil.IsAvailable, new Vector2(16, 70), 1.5f, WindowUtil.IsAvailable ? Palette.Nephritis : Palette.Alizarin);
        _batch2d.DrawString("Diagnostics: " + WindowUtil.Diagnostics, new Vector2(16, 92), 1.2f, Palette.Silver);

        _batch2d.DrawString("Minimized: " + Safe(() => WindowUtil.IsWindowMinimized(Window).ToString()), new Vector2(16, 120), 1.3f, Palette.Silver);
        _batch2d.DrawString("Maximized: " + Safe(() => WindowUtil.IsWindowMaximized(Window).ToString()), new Vector2(16, 140), 1.3f, Palette.Silver);
        _batch2d.DrawString("Opacity: " + Safe(() => WindowUtil.GetWindowOpacity(Window).ToString("F2")), new Vector2(16, 160), 1.3f, Palette.Silver);
        _batch2d.DrawString("Clipboard round-trip: " + _lastClipboardRoundTrip, new Vector2(16, 180), 1.3f, Palette.Silver);
        _batch2d.DrawString("Cursor captured: " + _cursorCaptured + "  delta: " + _lastCursorDelta, new Vector2(16, 200), 1.3f, Palette.Silver);

        _batch2d.DrawString(_status, new Vector2(16, 240), 1.4f, Palette.Sunflower);

        _batch2d.End();
        base.Draw(gameTime);
    }
}
