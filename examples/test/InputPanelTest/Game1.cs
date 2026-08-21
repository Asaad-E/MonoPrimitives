#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace InputPanelTest;

/// <summary>
/// Visual test for <see cref="PrimitiveInput"/>: a keyboard layout that lights each key up while
/// held (and flashes on the exact press/release frame), a mouse panel showing position, buttons,
/// scroll and drag, a gamepad panel (sticks/triggers, rumble on A, any-button flash) for whatever's
/// connected, "any key"/"any mouse" indicators, and a live text box exercising
/// <see cref="PrimitiveInput.GetCharPressed"/> (try typing Spanish accents).
/// </summary>
public class Game1 : Game
{
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;

    private GraphicsDeviceManager _graphics;
    private PrimitiveBatch _batch2d = null!;
    private PrimitiveInput _input = null!;

    // Demo-owned rumble/flash timers -- PrimitiveInput.SetVibration is a raw, stateless
    // passthrough by design (see its doc comment), so "how long" is always the caller's call.
    private float _vibrationTimeRemaining;
    private float _anyButtonFlashTimeRemaining;
    private float _anyKeyFlashTimeRemaining;
    private float _anyMouseFlashTimeRemaining;

    // GetCharPressed demo: Backspace/Enter are editing controls, not typed "content" -- polled
    // separately via the regular Keys API, same split a real text box would make.
    private string _typedText = "";

    private readonly record struct KeySpec(Keys Key, string Label, float Width);
    private static readonly KeySpec[][] KeyboardRows =
    {
        new KeySpec[] { new(Keys.Escape, "Esc", 1.5f), new(Keys.D1, "1", 1f), new(Keys.D2, "2", 1f), new(Keys.D3, "3", 1f), new(Keys.D4, "4", 1f), new(Keys.D5, "5", 1f), new(Keys.D6, "6", 1f), new(Keys.D7, "7", 1f), new(Keys.D8, "8", 1f), new(Keys.D9, "9", 1f), new(Keys.D0, "0", 1f) },
        new KeySpec[] { new(Keys.Tab, "Tab", 1.5f), new(Keys.Q, "Q", 1f), new(Keys.W, "W", 1f), new(Keys.E, "E", 1f), new(Keys.R, "R", 1f), new(Keys.T, "T", 1f), new(Keys.Y, "Y", 1f), new(Keys.U, "U", 1f), new(Keys.I, "I", 1f), new(Keys.O, "O", 1f), new(Keys.P, "P", 1f) },
        new KeySpec[] { new(Keys.CapsLock, "Caps", 1.75f), new(Keys.A, "A", 1f), new(Keys.S, "S", 1f), new(Keys.D, "D", 1f), new(Keys.F, "F", 1f), new(Keys.G, "G", 1f), new(Keys.H, "H", 1f), new(Keys.J, "J", 1f), new(Keys.K, "K", 1f), new(Keys.L, "L", 1f), new(Keys.Enter, "Enter", 1.75f) },
        new KeySpec[] { new(Keys.LeftShift, "Shift", 2.25f), new(Keys.Z, "Z", 1f), new(Keys.X, "X", 1f), new(Keys.C, "C", 1f), new(Keys.V, "V", 1f), new(Keys.B, "B", 1f), new(Keys.N, "N", 1f), new(Keys.M, "M", 1f), new(Keys.RightShift, "Shift", 2.25f) },
        new KeySpec[] { new(Keys.LeftControl, "Ctrl", 1.5f), new(Keys.LeftAlt, "Alt", 1.5f), new(Keys.Space, "Space", 5f), new(Keys.RightAlt, "Alt", 1.5f), new(Keys.RightControl, "Ctrl", 1.5f) },
    };

    private static readonly (Keys key, string label)[] ArrowCluster =
    {
        (Keys.Up, "^"), (Keys.Left, "<"), (Keys.Down, "v"), (Keys.Right, ">"),
    };

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = WindowWidth, PreferredBackBufferHeight = WindowHeight };
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _batch2d = new PrimitiveBatch(GraphicsDevice);
        _input = new PrimitiveInput(Window); // Window ctor -- enables GetCharPressed
        base.Initialize();
    }

    protected override void Update(GameTime gameTime)
    {
        _input.Update(gameTime);

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_input.IsButtonPressed(Buttons.A))
            _vibrationTimeRemaining = 0.25f;
        if (_vibrationTimeRemaining > 0f)
        {
            _vibrationTimeRemaining -= dt;
            _input.SetVibration(1f, 1f);
            if (_vibrationTimeRemaining <= 0f) _input.SetVibration(0f, 0f);
        }

        if (_input.IsAnyButtonPressed())
            _anyButtonFlashTimeRemaining = 0.3f;
        if (_anyButtonFlashTimeRemaining > 0f)
            _anyButtonFlashTimeRemaining -= dt;

        if (_input.IsAnyKeyPressed())
            _anyKeyFlashTimeRemaining = 0.3f;
        if (_anyKeyFlashTimeRemaining > 0f)
            _anyKeyFlashTimeRemaining -= dt;

        if (_input.IsAnyMouseButtonPressed())
            _anyMouseFlashTimeRemaining = 0.3f;
        if (_anyMouseFlashTimeRemaining > 0f)
            _anyMouseFlashTimeRemaining -= dt;

        char c;
        while ((c = _input.GetCharPressed()) != '\0')
            _typedText += c;
        if (_input.IsKeyPressed(Keys.Back) && _typedText.Length > 0)
            _typedText = _typedText[..^1];
        if (_input.IsKeyPressed(Keys.Enter))
            _typedText = "";

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.Background);
        _batch2d.Begin();

        _batch2d.DrawString("INPUT PANEL -- keyboard, mouse, gamepad state, live", new Vector2(16, 12), 2f, Color.White);
        _batch2d.DrawString("any key:", new Vector2(700, 14), 1.3f, Palette.Silver);
        _batch2d.FillCircle(new Vector2(760, 20), 7f, _anyKeyFlashTimeRemaining > 0f ? Palette.Sunflower : Palette.WetAsphalt);
        _batch2d.DrawString("any mouse:", new Vector2(790, 14), 1.3f, Palette.Silver);
        _batch2d.FillCircle(new Vector2(870, 20), 7f, _anyMouseFlashTimeRemaining > 0f ? Palette.Sunflower : Palette.WetAsphalt);

        DrawKeyboard(new Vector2(16, 50));
        DrawArrowCluster(new Vector2(830, 210));
        DrawMousePanel(new Vector2(940, 50));
        DrawGamepadPanel(new Vector2(940, 320));
        DrawTypedTextPanel(new Vector2(16, 270));

        _batch2d.End();
        base.Draw(gameTime);
    }

    // ------------------------------------------------------------------
    // Keyboard
    // ------------------------------------------------------------------
    private const float KeyUnit = 44f;
    private const float KeyGap = 4f;
    private const float KeyRowHeight = 40f;

    private void DrawKeyboard(Vector2 origin)
    {
        float y = origin.Y;
        foreach (KeySpec[] row in KeyboardRows)
        {
            float x = origin.X;
            foreach (KeySpec spec in row)
            {
                float width = spec.Width * KeyUnit - KeyGap;
                DrawKey(new Vector2(x, y), new Vector2(width, KeyRowHeight - KeyGap), spec.Key, spec.Label);
                x += spec.Width * KeyUnit;
            }
            y += KeyRowHeight;
        }
    }

    private void DrawArrowCluster(Vector2 origin)
    {
        (float dx, float dy)[] offsets = { (1, 0), (0, 1), (1, 1), (2, 1) };
        for (int i = 0; i < ArrowCluster.Length; i++)
        {
            (Keys key, string label) = ArrowCluster[i];
            (float dx, float dy) = offsets[i];
            Vector2 pos = origin + new Vector2(dx * KeyUnit, dy * KeyRowHeight);
            DrawKey(pos, new Vector2(KeyUnit - KeyGap, KeyRowHeight - KeyGap), key, label);
        }
    }

    private void DrawKey(Vector2 position, Vector2 size, Keys key, string label)
    {
        bool down = _input.IsKeyDown(key);
        bool justChanged = _input.IsKeyPressed(key) || _input.IsKeyReleased(key);

        Color fill = down ? Palette.PeterRiver : Palette.WetAsphalt;
        Color border = justChanged ? Palette.Sunflower : (down ? Palette.BelizeHole : Palette.Concrete);

        _batch2d.FillRectangleRounded(position, size, 4f, fill);
        _batch2d.BorderRectangleRounded(position, size, 4f, border, justChanged ? 3f : 1.5f);

        Vector2 textSize = DebugFont5x7TextSize(label, 1.5f);
        Vector2 textPos = position + (size - textSize) * 0.5f;
        _batch2d.DrawString(label, textPos, 1.5f, down ? Color.White : Palette.Silver);
    }

    // ------------------------------------------------------------------
    // Typed text (GetCharPressed)
    // ------------------------------------------------------------------
    private void DrawTypedTextPanel(Vector2 origin)
    {
        _batch2d.DrawString("TYPE HERE (GetCharPressed -- try accents: áéíóúñ)  Backspace/Enter clear", origin, 1.5f, Color.White);

        Vector2 boxPos = origin + new Vector2(0, 22);
        Vector2 boxSize = new(900, 34);
        _batch2d.FillRectangleRounded(boxPos, boxSize, 4f, Palette.WetAsphalt);
        _batch2d.BorderRectangleRounded(boxPos, boxSize, 4f, Palette.Concrete, 1.5f);
        _batch2d.DrawString(_typedText, boxPos + new Vector2(8, 10), 1.6f, Color.White);
    }

    private static Vector2 DebugFont5x7TextSize(string text, float pixelSize) => new(text.Length * 6f * pixelSize, 7f * pixelSize);

    // ------------------------------------------------------------------
    // Mouse
    // ------------------------------------------------------------------
    private void DrawMousePanel(Vector2 origin)
    {
        _batch2d.DrawString("MOUSE", origin, 2f, Color.White);
        Vector2 pos = _input.MousePosition;
        _batch2d.DrawString($"Position: {pos.X:F0}, {pos.Y:F0}", origin + new Vector2(0, 26), 1.5f, Palette.Silver);
        _batch2d.DrawString($"Scroll: {_input.MouseScrollDelta}", origin + new Vector2(0, 46), 1.5f, Palette.Silver);

        (MouseButton button, string label, Vector2 offset)[] buttons =
        {
            (MouseButton.Left, "L", new Vector2(0, 0)),
            (MouseButton.Middle, "M", new Vector2(56, 0)),
            (MouseButton.Right, "R", new Vector2(112, 0)),
        };

        Vector2 buttonsOrigin = origin + new Vector2(0, 76);
        foreach (var (button, label, offset) in buttons)
        {
            Vector2 buttonPos = buttonsOrigin + offset;
            bool down = _input.IsMouseButtonDown(button);
            bool justChanged = _input.IsMouseButtonPressed(button) || _input.IsMouseButtonReleased(button);
            Color fill = down ? Palette.Amethyst : Palette.WetAsphalt;
            Color border = justChanged ? Palette.Sunflower : Palette.Concrete;

            _batch2d.FillRectangleRounded(buttonPos, new Vector2(48, 48), 6f, fill);
            _batch2d.BorderRectangleRounded(buttonPos, new Vector2(48, 48), 6f, border, justChanged ? 3f : 1.5f);
            _batch2d.DrawString(label, buttonPos + new Vector2(18, 16), 1.5f, Color.White);

            if (_input.IsMouseButtonDoubleClicked(button))
                _batch2d.DrawString("2x", buttonPos + new Vector2(4, 52), 1.2f, Palette.Sunflower);

            if (_input.IsDragging(button))
            {
                Vector2 drag = _input.DragDelta(button);
                _batch2d.DrawString($"drag {drag.X:F0},{drag.Y:F0}", buttonPos + new Vector2(-4, 68), 1.1f, Palette.PeterRiver);
            }
        }
    }

    // ------------------------------------------------------------------
    // Gamepad
    // ------------------------------------------------------------------
    private void DrawGamepadPanel(Vector2 origin)
    {
        _batch2d.DrawString("GAMEPAD (player 1)", origin, 2f, Color.White);
        bool connected = _input.IsConnected();
        _batch2d.DrawString(connected ? "connected" : "not connected", origin + new Vector2(0, 26), 1.5f, connected ? Palette.Nephritis : Palette.Concrete);

        if (!connected)
            return;

        Vector2 leftStick = _input.LeftStickDeadzoned();
        Vector2 rightStick = _input.RightStickDeadzoned();
        _batch2d.DrawString($"Left stick: {leftStick.X:F2}, {leftStick.Y:F2}", origin + new Vector2(0, 50), 1.5f, Palette.Silver);
        _batch2d.DrawString($"Right stick: {rightStick.X:F2}, {rightStick.Y:F2}", origin + new Vector2(0, 70), 1.5f, Palette.Silver);
        _batch2d.DrawString($"Triggers: L {_input.LeftTriggerDeadzoned():F2}  R {_input.RightTriggerDeadzoned():F2}", origin + new Vector2(0, 90), 1.5f, Palette.Silver);

        bool aDown = _input.IsButtonDown(Buttons.A);
        _batch2d.FillCircle(origin + new Vector2(30, 130), 14f, aDown ? Palette.Emerald : Palette.WetAsphalt);
        _batch2d.DrawString("A", origin + new Vector2(24, 122), 1.5f, Color.White);
        _batch2d.DrawString("(A: 0.25s rumble)", origin + new Vector2(56, 122), 1.2f, Palette.Silver);

        bool anyFlash = _anyButtonFlashTimeRemaining > 0f;
        _batch2d.DrawString("Any button pressed:", origin + new Vector2(0, 160), 1.5f, Palette.Silver);
        _batch2d.FillCircle(origin + new Vector2(160, 166), 8f, anyFlash ? Palette.Sunflower : Palette.WetAsphalt);
    }
}
