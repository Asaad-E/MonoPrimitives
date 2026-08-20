#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoPrimitives
{
    /// <summary>Mouse buttons, named rather than inferred from a raw <see cref="MouseState"/> field.</summary>
    public enum MouseButton
    {
        Left,
        Right,
        Middle,
        XButton1,
        XButton2
    }

    /// <summary>
    /// Polls keyboard, mouse, and gamepad state once per frame and exposes down/pressed/released
    /// queries plus a couple of composite helpers (<see cref="GetAxis"/>/<see cref="GetVector2"/>)
    /// that turn a handful of individual key bindings into one float or <see cref="Vector2"/> —
    /// the same shape as a game engine's "get movement direction" call, one line instead of six
    /// `if (IsKeyDown(...))` checks. Call <see cref="Update"/> once per frame (before reading
    /// anything else this frame) — typically the first line of your own <c>Game.Update</c>.
    /// </summary>
    public sealed class PrimitiveInput
    {
        private KeyboardState _keyboard;
        private KeyboardState _prevKeyboard;
        private MouseState _mouse;
        private MouseState _prevMouse;
        private readonly GamePadState[] _gamePads = new GamePadState[4];
        private readonly GamePadState[] _prevGamePads = new GamePadState[4];
        private bool _hasPrevMouse;

        // Per-button drag/double-click tracking (indexed by MouseButton, 5 values).
        private readonly Vector2?[] _dragStart = new Vector2?[5];
        private readonly float[] _timeSinceLastClick = { float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue };
        private readonly bool[] _doubleClickedThisFrame = new bool[5];

        /// <summary>Max seconds between two presses of the same button for <see cref="IsMouseButtonDoubleClicked"/> to report one.</summary>
        public float DoubleClickTime { get; set; } = 0.35f;

        /// <summary>Refreshes keyboard/mouse/gamepad state for this frame — the usual call from inside your own <c>Game.Update(GameTime gameTime)</c>, so <see cref="IsMouseButtonDoubleClicked"/>'s timing window uses the real elapsed time automatically.</summary>
        public void Update(GameTime gameTime) => Update((float)gameTime.ElapsedGameTime.TotalSeconds);

        /// <summary>
        /// Refreshes keyboard/mouse/gamepad state for this frame. Call once per frame before any
        /// query below. <paramref name="deltaSeconds"/> is only needed for
        /// <see cref="IsMouseButtonDoubleClicked"/>'s timing window — pass 0 (default) if you
        /// don't use it, or prefer <see cref="Update(GameTime)"/> if you have a <see cref="GameTime"/> on hand.
        /// </summary>
        public void Update(float deltaSeconds = 0f)
        {
            _prevKeyboard = _keyboard;
            _keyboard = Keyboard.GetState();

            _prevMouse = _mouse;
            _mouse = Mouse.GetState();
            if (!_hasPrevMouse) { _prevMouse = _mouse; _hasPrevMouse = true; }

            for (int i = 0; i < 4; i++)
            {
                _prevGamePads[i] = _gamePads[i];
                _gamePads[i] = GamePad.GetState((PlayerIndex)i);
            }

            for (int i = 0; i < 5; i++)
            {
                var button = (MouseButton)i;
                bool down = GetMouseButtonState(_mouse, button) == ButtonState.Pressed;
                bool wasDown = GetMouseButtonState(_prevMouse, button) == ButtonState.Pressed;
                bool doubleClicked = false;

                if (down && !wasDown)
                {
                    _dragStart[i] = MousePosition;
                    doubleClicked = _timeSinceLastClick[i] <= DoubleClickTime;
                    _timeSinceLastClick[i] = 0f;
                }
                else
                {
                    if (!down) _dragStart[i] = null;
                    if (_timeSinceLastClick[i] < float.MaxValue) _timeSinceLastClick[i] += deltaSeconds;
                }

                _doubleClickedThisFrame[i] = doubleClicked;
            }
        }

        /// <summary>Resets mouse-delta tracking to zero for the next <see cref="Update"/> — call after teleporting the cursor or regaining window focus, to avoid a one-frame snap in <see cref="MouseDelta"/>.</summary>
        public void ResetMouseDelta() => _hasPrevMouse = false;

        // ---------------------------------------------------------------------
        // Keyboard
        // ---------------------------------------------------------------------

        /// <summary>True while <paramref name="key"/> is held.</summary>
        public bool IsKeyDown(Keys key) => _keyboard.IsKeyDown(key);

        /// <summary>True while <paramref name="key"/> is not held.</summary>
        public bool IsKeyUp(Keys key) => _keyboard.IsKeyUp(key);

        /// <summary>True on the frame <paramref name="key"/> went from up to down.</summary>
        public bool IsKeyPressed(Keys key) => _keyboard.IsKeyDown(key) && _prevKeyboard.IsKeyUp(key);

        /// <summary>True on the frame <paramref name="key"/> went from down to up.</summary>
        public bool IsKeyReleased(Keys key) => _keyboard.IsKeyUp(key) && _prevKeyboard.IsKeyDown(key);

        // ---------------------------------------------------------------------
        // Mouse
        // ---------------------------------------------------------------------

        /// <summary>Cursor position in screen pixels, origin top-left.</summary>
        public Vector2 MousePosition => new(_mouse.X, _mouse.Y);

        /// <summary>Movement since the last <see cref="Update(float)"/>, in pixels.</summary>
        public Vector2 MouseDelta => new(_mouse.X - _prevMouse.X, _mouse.Y - _prevMouse.Y);

        /// <summary>Vertical scroll wheel movement since the last <see cref="Update(float)"/> (120 units per notch, matching <see cref="MouseState.ScrollWheelValue"/>).</summary>
        public int MouseScrollDelta => _mouse.ScrollWheelValue - _prevMouse.ScrollWheelValue;

        /// <summary>Horizontal scroll wheel movement since the last <see cref="Update(float)"/>, same units as <see cref="MouseScrollDelta"/>.</summary>
        public int MouseHorizontalScrollDelta => _mouse.HorizontalScrollWheelValue - _prevMouse.HorizontalScrollWheelValue;

        /// <summary>True while <paramref name="button"/> is held.</summary>
        public bool IsMouseButtonDown(MouseButton button) => GetMouseButtonState(_mouse, button) == ButtonState.Pressed;

        /// <summary>True while <paramref name="button"/> is not held.</summary>
        public bool IsMouseButtonUp(MouseButton button) => GetMouseButtonState(_mouse, button) == ButtonState.Released;

        /// <summary>True on the frame <paramref name="button"/> went from up to down.</summary>
        public bool IsMouseButtonPressed(MouseButton button)
            => GetMouseButtonState(_mouse, button) == ButtonState.Pressed && GetMouseButtonState(_prevMouse, button) == ButtonState.Released;

        /// <summary>True on the frame <paramref name="button"/> went from down to up.</summary>
        public bool IsMouseButtonReleased(MouseButton button)
            => GetMouseButtonState(_mouse, button) == ButtonState.Released && GetMouseButtonState(_prevMouse, button) == ButtonState.Pressed;

        /// <summary>True on the frame <paramref name="button"/> is pressed again within <see cref="DoubleClickTime"/> seconds of its previous press.</summary>
        public bool IsMouseButtonDoubleClicked(MouseButton button) => _doubleClickedThisFrame[(int)button];

        /// <summary>
        /// Total movement since <paramref name="button"/> was last pressed, or <see cref="Vector2.Zero"/>
        /// if it isn't currently held — a drag-to-pan camera or a box-select's live size both just
        /// need this each frame, no manual "remember where the button went down" bookkeeping.
        /// </summary>
        public Vector2 DragDelta(MouseButton button)
        {
            Vector2? start = _dragStart[(int)button];
            return start.HasValue ? MousePosition - start.Value : Vector2.Zero;
        }

        /// <summary>True while <paramref name="button"/> is held AND has moved more than <paramref name="threshold"/> pixels from where it was pressed — lets a click handler and a drag handler share the same button without the click firing on every tiny press-time jitter.</summary>
        public bool IsDragging(MouseButton button, float threshold = 4f) => DragDelta(button).LengthSquared() > threshold * threshold;

        /// <summary>Point-in-rectangle test against <see cref="MousePosition"/> — hit-testing a UI panel or button without pulling in a separate UI library.</summary>
        public bool IsMouseOver(Rectangle screenRect) => screenRect.Contains(new Point(_mouse.X, _mouse.Y));

        /// <summary>
        /// Moves the OS cursor to an exact screen position — e.g. re-centering it every frame for
        /// an FPS-style "infinite" mouse-look that never hits the window edge. Call
        /// <see cref="ResetMouseDelta"/> alongside this (or accept one frame of a large delta from
        /// the jump) since the cursor didn't actually travel there by user motion.
        /// </summary>
        public void SetMousePosition(int x, int y) => Mouse.SetPosition(x, y);

        private static ButtonState GetMouseButtonState(in MouseState state, MouseButton button) => button switch
        {
            MouseButton.Left => state.LeftButton,
            MouseButton.Right => state.RightButton,
            MouseButton.Middle => state.MiddleButton,
            MouseButton.XButton1 => state.XButton1,
            MouseButton.XButton2 => state.XButton2,
            _ => ButtonState.Released
        };

        // ---------------------------------------------------------------------
        // Gamepad (player index 0-3)
        // ---------------------------------------------------------------------

        /// <summary>True if a gamepad is plugged in at <paramref name="player"/>'s slot.</summary>
        public bool IsConnected(int player = 0) => _gamePads[player].IsConnected;

        /// <summary>True while <paramref name="button"/> is held on <paramref name="player"/>'s gamepad.</summary>
        public bool IsButtonDown(Buttons button, int player = 0) => _gamePads[player].IsButtonDown(button);

        /// <summary>True while <paramref name="button"/> is not held on <paramref name="player"/>'s gamepad.</summary>
        public bool IsButtonUp(Buttons button, int player = 0) => _gamePads[player].IsButtonUp(button);

        /// <summary>True on the frame <paramref name="button"/> went from up to down on <paramref name="player"/>'s gamepad.</summary>
        public bool IsButtonPressed(Buttons button, int player = 0) => _gamePads[player].IsButtonDown(button) && _prevGamePads[player].IsButtonUp(button);

        /// <summary>True on the frame <paramref name="button"/> went from down to up on <paramref name="player"/>'s gamepad.</summary>
        public bool IsButtonReleased(Buttons button, int player = 0) => _gamePads[player].IsButtonUp(button) && _prevGamePads[player].IsButtonDown(button);

        /// <summary>Raw left thumbstick, MonoGame's own convention (X right-positive, Y up-positive), no deadzone applied.</summary>
        public Vector2 LeftStick(int player = 0) => _gamePads[player].ThumbSticks.Left;

        /// <summary>Raw right thumbstick, same convention as <see cref="LeftStick"/>.</summary>
        public Vector2 RightStick(int player = 0) => _gamePads[player].ThumbSticks.Right;

        public float LeftTrigger(int player = 0) => _gamePads[player].Triggers.Left;
        public float RightTrigger(int player = 0) => _gamePads[player].Triggers.Right;

        /// <summary><see cref="LeftStick"/> with a circular deadzone applied — snaps to zero within <paramref name="deadzone"/> of center instead of reporting idle stick drift as movement.</summary>
        public Vector2 LeftStickDeadzoned(int player = 0, float deadzone = 0.15f) => ApplyDeadzone(LeftStick(player), deadzone);

        /// <summary><see cref="RightStick"/> with a circular deadzone applied, same as <see cref="LeftStickDeadzoned"/>.</summary>
        public Vector2 RightStickDeadzoned(int player = 0, float deadzone = 0.15f) => ApplyDeadzone(RightStick(player), deadzone);

        private static Vector2 ApplyDeadzone(Vector2 v, float deadzone)
            => v.LengthSquared() < deadzone * deadzone ? Vector2.Zero : v;

        // ---------------------------------------------------------------------
        // Composite helpers (Godot's Input.get_axis/get_vector shape)
        // ---------------------------------------------------------------------

        /// <summary>-1/0/1 axis from two keys: -1 if only <paramref name="negative"/> is held, +1 if only <paramref name="positive"/> is held, 0 if neither or both are (positive wins a tie).</summary>
        public float GetAxis(Keys negative, Keys positive)
        {
            float v = 0f;
            if (IsKeyDown(negative)) v -= 1f;
            if (IsKeyDown(positive)) v += 1f;
            return v;
        }

        /// <summary>
        /// A 2D direction from four keys in one call — WASD/arrow-style movement without writing
        /// out four separate <c>IsKeyDown</c> checks yourself. <paramref name="normalize"/>
        /// (default true) keeps diagonal movement the same speed as axis-aligned movement;
        /// pass false for the raw, un-normalized per-axis [-1,1] pair instead.
        /// </summary>
        public Vector2 GetVector2(Keys negativeX, Keys positiveX, Keys negativeY, Keys positiveY, bool normalize = true)
        {
            Vector2 v = new(GetAxis(negativeX, positiveX), GetAxis(negativeY, positiveY));
            if (normalize && v.LengthSquared() > 1f) v = Vector2.Normalize(v);
            return v;
        }
    }
}
