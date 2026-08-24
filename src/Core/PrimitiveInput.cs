#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MonoPrimitives
{
    /// <summary>Mouse buttons, named rather than inferred from a raw <see cref="MouseState"/> field.</summary>
    public enum MouseButton
    {
        /// <summary>Primary (usually left) button.</summary>
        Left,

        /// <summary>Secondary (usually right) button.</summary>
        Right,

        /// <summary>Middle button, typically the scroll wheel click.</summary>
        Middle,

        /// <summary>First extra "thumb" button, if the mouse has one.</summary>
        XButton1,

        /// <summary>Second extra "thumb" button, if the mouse has one.</summary>
        XButton2
    }

    /// <summary>
    /// Polls keyboard, mouse, and gamepad state once per frame and exposes down/pressed/released
    /// queries plus a couple of composite helpers (<see cref="GetAxis"/>/<see cref="GetVector2"/>)
    /// that turn a handful of individual key bindings into one float or <see cref="Vector2"/> —
    /// the same shape as a game engine's "get movement direction" call, one line instead of six
    /// `if (IsKeyDown(...))` checks. Call <see cref="Update(GameTime)"/> once per frame (before reading
    /// anything else this frame) — typically the first line of your own <c>Game.Update</c>.
    /// </summary>
    public sealed class PrimitiveInput : IDisposable
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
        private readonly Vector2[] _dragEnd = new Vector2[5]; // valid only once _dragStart[i] is set and the button is currently up
        private readonly Vector2[] _lastClickPosition = new Vector2[5];
        private readonly float[] _timeSinceLastClick = { float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue };
        private readonly bool[] _doubleClickedThisFrame = new bool[5];

        private readonly GameWindow? _window;

        // Fixed-capacity ring buffer fed by GameWindow.TextInput -- bounded (not a growable
        // Queue<char>) so a caller that never drains it can't leak memory, same "no unbounded
        // per-frame growth" spirit as Trail2D's own ring buffer.
        private readonly char[] _textInputQueue = new char[64];
        private int _textInputHead;
        private int _textInputCount;

        /// <summary>Max seconds between two presses of the same button for <see cref="IsMouseButtonDoubleClicked"/> to report one.</summary>
        public float DoubleClickTime { get; set; } = 0.35f;

        /// <summary>Max pixels between two presses of the same button for <see cref="IsMouseButtonDoubleClicked"/> to report one — without this, two clicks in unrelated corners of the screen within <see cref="DoubleClickTime"/> would still count as a double-click.</summary>
        public float DoubleClickDistance { get; set; } = 6f;

        /// <summary>Creates an input poller with no typed-text support — <see cref="GetCharPressed"/> always returns <c>'\0'</c>, since there's no <see cref="GameWindow"/> to subscribe to. Use <see cref="PrimitiveInput(GameWindow)"/> instead if you need real typed text.</summary>
        public PrimitiveInput() { }

        /// <summary>Creates an input poller that also subscribes to <paramref name="window"/>'s <c>TextInput</c> event, enabling <see cref="GetCharPressed"/>. Call <see cref="Dispose"/> when done to unsubscribe.</summary>
        /// <remarks>
        /// The only correct way to get typed characters — keyboard-state polling (everything else
        /// in this class) can't produce them: <see cref="Keys"/> is physical key identity, not the
        /// character a layout/shift/dead-key combination actually produces (e.g. Spanish 'á' is
        /// typically composed from a dead-key sequence only the OS can resolve), and OS key-repeat
        /// timing can't be reconstructed by guessing at settings the OS already knows.
        /// </remarks>
        public PrimitiveInput(GameWindow window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _window.TextInput += OnTextInput;
        }

        /// <summary>Unsubscribes from <see cref="GameWindow"/>'s <c>TextInput</c> event, if this instance was constructed with one. Safe to call even if it wasn't.</summary>
        public void Dispose()
        {
            if (_window is not null) _window.TextInput -= OnTextInput;
        }

        private void OnTextInput(object? sender, TextInputEventArgs e)
        {
            if (_textInputCount >= _textInputQueue.Length) return; // full -- caller isn't draining; drop rather than grow unbounded
            int tail = (_textInputHead + _textInputCount) % _textInputQueue.Length;
            _textInputQueue[tail] = e.Character;
            _textInputCount++;
        }

        /// <summary>Dequeues the next typed character since the last call, or <c>'\0'</c> if none are queued.</summary>
        /// <remarks>Call in a loop (<c>while ((c = input.GetCharPressed()) != '\0') ...</c>) to drain everything typed since you last checked. Always <c>'\0'</c> if this instance was constructed without a <see cref="GameWindow"/> (the parameterless constructor).</remarks>
        public char GetCharPressed()
        {
            if (_textInputCount == 0) return '\0';
            char c = _textInputQueue[_textInputHead];
            _textInputHead = (_textInputHead + 1) % _textInputQueue.Length;
            _textInputCount--;
            return c;
        }

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
                    Vector2 pos = MousePosition;
                    doubleClicked = _timeSinceLastClick[i] <= DoubleClickTime
                        && Vector2.DistanceSquared(pos, _lastClickPosition[i]) <= DoubleClickDistance * DoubleClickDistance;
                    _timeSinceLastClick[i] = 0f;
                    _lastClickPosition[i] = pos;
                    _dragStart[i] = pos; // overwrites whatever the previous drag cycle left behind
                }
                else
                {
                    // Deliberately NOT nulling _dragStart[i] here on release: DragDelta/IsDragging
                    // need to still report the completed drag's distance on the exact frame
                    // IsMouseButtonReleased fires (the natural "did I just swipe far enough" check),
                    // not read as zero because the reset already happened this same frame. It only
                    // gets overwritten by the next press, above.
                    if (!down && wasDown) _dragEnd[i] = MousePosition; // capture once, right at release
                    if (_timeSinceLastClick[i] < float.MaxValue) _timeSinceLastClick[i] += deltaSeconds;
                }

                _doubleClickedThisFrame[i] = doubleClicked;
            }
        }

        /// <summary>Resets mouse-delta tracking to zero for the next <see cref="Update(GameTime)"/> — call after teleporting the cursor or regaining window focus, to avoid a one-frame snap in <see cref="MouseDelta"/>.</summary>
        public void ResetMouseDelta() => _hasPrevMouse = false;

        // ---------------------------------------------------------------------
        // Keyboard
        // ---------------------------------------------------------------------

        /// <summary>This frame's raw <see cref="KeyboardState"/>, exactly as captured by the last <see cref="Update(GameTime)"/> — for anything this class doesn't wrap itself (e.g. <see cref="KeyboardState.GetPressedKeys()"/>).</summary>
        /// <remarks>Reach here instead of calling <see cref="Keyboard.GetState()"/> yourself, which would return a second, independent snapshot that can desync from this frame's — same reasoning as <see cref="RandomUtil.UnderlyingRandom"/>.</remarks>
        public KeyboardState CurrentKeyboardState => _keyboard;

        /// <summary>The previous frame's raw <see cref="KeyboardState"/> — for building your own custom transition check (pressed/released/held-for-N-frames) beyond what <see cref="IsKeyPressed"/>/<see cref="IsKeyReleased"/> already cover.</summary>
        public KeyboardState PreviousKeyboardState => _prevKeyboard;

        /// <summary>True while <paramref name="key"/> is held.</summary>
        public bool IsKeyDown(Keys key) => _keyboard.IsKeyDown(key);

        /// <summary>True while <paramref name="key"/> is not held.</summary>
        public bool IsKeyUp(Keys key) => _keyboard.IsKeyUp(key);

        /// <summary>True on the frame <paramref name="key"/> went from up to down.</summary>
        public bool IsKeyPressed(Keys key) => _keyboard.IsKeyDown(key) && _prevKeyboard.IsKeyUp(key);

        /// <summary>True on the frame <paramref name="key"/> went from down to up.</summary>
        public bool IsKeyReleased(Keys key) => _keyboard.IsKeyUp(key) && _prevKeyboard.IsKeyDown(key);

        /// <summary>Whether Caps Lock is currently toggled on — the lock state itself, not whether the key is physically held. Useful for a custom on-screen keyboard, or warning next to a password field.</summary>
        public bool CapsLock => _keyboard.CapsLock;

        /// <summary>Whether Num Lock is currently toggled on, same idea as <see cref="CapsLock"/>.</summary>
        public bool NumLock => _keyboard.NumLock;

        private static readonly Keys[] AllKeys = (Keys[])Enum.GetValues(typeof(Keys));

        /// <summary>True on the frame any key went from up to down — for a "press any key to continue" prompt, one call instead of listing every key yourself.</summary>
        public bool IsAnyKeyPressed()
        {
            foreach (Keys key in AllKeys)
                if (IsKeyPressed(key)) return true;
            return false;
        }

        // ---------------------------------------------------------------------
        // Mouse
        // ---------------------------------------------------------------------

        /// <summary>This frame's raw <see cref="MouseState"/>, exactly as captured by the last <see cref="Update(GameTime)"/> — for anything this class doesn't wrap itself.</summary>
        /// <remarks>Reach here instead of calling <see cref="Mouse.GetState()"/> yourself, which would return a second, independent snapshot that can desync from this frame's.</remarks>
        public MouseState CurrentMouseState => _mouse;

        /// <summary>The previous frame's raw <see cref="MouseState"/> — for building your own custom transition/delta check beyond what this class already covers.</summary>
        public MouseState PreviousMouseState => _prevMouse;

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

        /// <summary>True on the frame <paramref name="button"/> is pressed again within <see cref="DoubleClickTime"/> seconds AND <see cref="DoubleClickDistance"/> pixels of its previous press.</summary>
        public bool IsMouseButtonDoubleClicked(MouseButton button) => _doubleClickedThisFrame[(int)button];

        private static readonly MouseButton[] AllMouseButtons = (MouseButton[])Enum.GetValues(typeof(MouseButton));

        /// <summary>True on the frame any mouse button went from up to down — same idea as <see cref="IsAnyKeyPressed"/>, for the mouse.</summary>
        public bool IsAnyMouseButtonPressed()
        {
            foreach (MouseButton button in AllMouseButtons)
                if (IsMouseButtonPressed(button)) return true;
            return false;
        }

        /// <summary>Total movement since <paramref name="button"/> was last pressed. <see cref="Vector2.Zero"/> if never pressed.</summary>
        /// <remarks>
        /// Live-tracks <see cref="MousePosition"/> while still held, or reports the drag's final
        /// distance for the rest of the frame it's released on (and after, until the next press
        /// starts a new drag) — so checking this from inside <c>if (IsMouseButtonReleased(button))</c>
        /// (a swipe/flick gesture) sees the real distance instead of a stale zero.
        /// </remarks>
        public Vector2 DragDelta(MouseButton button)
        {
            int i = (int)button;
            Vector2? start = _dragStart[i];
            if (!start.HasValue) return Vector2.Zero;
            Vector2 end = IsMouseButtonDown(button) ? MousePosition : _dragEnd[i];
            return end - start.Value;
        }

        /// <summary>True while <paramref name="button"/> is currently held AND has moved more than <paramref name="threshold"/> pixels from where it was pressed — lets a click handler and a drag handler share the same button without the click firing on every tiny press-time jitter. Unlike <see cref="DragDelta"/>, always false once the button is released.</summary>
        public bool IsDragging(MouseButton button, float threshold = 4f)
            => IsMouseButtonDown(button) && DragDelta(button).LengthSquared() > threshold * threshold;

        /// <summary>Point-in-rectangle test against <see cref="MousePosition"/> — hit-testing a UI panel or button without pulling in a separate UI library.</summary>
        public bool IsMouseOver(Rectangle screenRect) => screenRect.Contains(new Point(_mouse.X, _mouse.Y));

        /// <summary>
        /// Moves the OS cursor to an exact screen position — e.g. re-centering it every frame for
        /// an FPS-style "infinite" mouse-look that never hits the window edge. Call
        /// <see cref="ResetMouseDelta"/> alongside this (or accept one frame of a large delta from
        /// the jump) since the cursor didn't actually travel there by user motion.
        /// </summary>
        public void SetMousePosition(int x, int y) => Mouse.SetPosition(x, y);

        /// <summary>Sets the OS cursor's shape — one of <see cref="MouseCursor"/>'s built-in system shapes (<c>Arrow</c>, <c>IBeam</c>, <c>Hand</c>, <c>Crosshair</c>, the resize arrows, etc.) or a fully custom one via <see cref="MouseCursor.FromTexture2D(Microsoft.Xna.Framework.Graphics.Texture2D,int,int)"/>.</summary>
        /// <remarks>A thin passthrough to <see cref="Mouse.SetCursor(MouseCursor)"/>, kept here so mouse commands live alongside the mouse queries above.</remarks>
        public void SetCursor(MouseCursor cursor) => Mouse.SetCursor(cursor);

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

        /// <summary><paramref name="player"/>'s raw <see cref="GamePadState"/> for this frame — for anything this class doesn't wrap itself (e.g. <see cref="GamePadState.Buttons"/>'s packed flags).</summary>
        /// <remarks>Reach here instead of calling <see cref="GamePad.GetState(PlayerIndex)"/> yourself, which would return a second, independent snapshot that can desync from this frame's.</remarks>
        public GamePadState GetCurrentGamePadState(int player = 0) => _gamePads[player];

        /// <summary><paramref name="player"/>'s previous-frame raw <see cref="GamePadState"/> — for building your own custom transition check beyond what this class already covers.</summary>
        public GamePadState GetPreviousGamePadState(int player = 0) => _prevGamePads[player];

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

        /// <summary>Left trigger pull, [0,1] — 0 released, 1 fully pressed.</summary>
        public float LeftTrigger(int player = 0) => _gamePads[player].Triggers.Left;
        /// <summary>Right trigger pull, [0,1] — 0 released, 1 fully pressed.</summary>
        public float RightTrigger(int player = 0) => _gamePads[player].Triggers.Right;

        /// <summary><see cref="LeftStick"/> with a scaled radial deadzone applied — see <see cref="ApplyDeadzone(Vector2,float)"/> for the curve. Direction is preserved exactly; only magnitude is remapped.</summary>
        public Vector2 LeftStickDeadzoned(int player = 0, float deadzone = 0.15f) => ApplyDeadzone(LeftStick(player), deadzone);

        /// <summary><see cref="RightStick"/> with a scaled radial deadzone applied, same as <see cref="LeftStickDeadzoned"/>.</summary>
        public Vector2 RightStickDeadzoned(int player = 0, float deadzone = 0.15f) => ApplyDeadzone(RightStick(player), deadzone);

        /// <summary><see cref="LeftTrigger"/> with a deadzone applied — zero below <paramref name="deadzone"/> instead of reporting a controller's resting trigger noise as a pull, and the remaining range rescaled back to <c>[0, 1]</c> instead of jumping straight to <paramref name="deadzone"/>'s own value.</summary>
        public float LeftTriggerDeadzoned(int player = 0, float deadzone = 0.05f) => ApplyDeadzone(LeftTrigger(player), deadzone);

        /// <summary><see cref="RightTrigger"/> with a deadzone applied, same as <see cref="LeftTriggerDeadzoned"/>.</summary>
        public float RightTriggerDeadzoned(int player = 0, float deadzone = 0.05f) => ApplyDeadzone(RightTrigger(player), deadzone);

        /// <summary>Applies a scaled radial deadzone to an analog stick reading: zero within <paramref name="deadzone"/> of center, and the surviving <c>[deadzone, 1]</c> range rescaled back onto a full <c>[0, 1]</c> output. Direction/sign is untouched; only magnitude is remapped.</summary>
        /// <remarks>
        /// Rescaled, not just clamped — clamping alone leaves a discontinuity right at the cutoff
        /// (output jumps straight from 0 to ~<paramref name="deadzone"/> instead of a continuous
        /// ramp). A degenerate <paramref name="deadzone"/> &gt;= 1 returns zero rather than dividing
        /// by a zero-or-negative range. Public static because the same curve is useful for any other
        /// analog 2D input this class doesn't itself read (a custom joystick, a mouse-driven virtual
        /// stick) — <see cref="LeftStickDeadzoned"/>/<see cref="RightStickDeadzoned"/> are thin
        /// wrappers over this.
        /// </remarks>
        public static Vector2 ApplyDeadzone(Vector2 v, float deadzone)
        {
            float magnitude = v.Length();
            if (magnitude < deadzone) return Vector2.Zero;
            float range = 1f - deadzone;
            if (range <= 0f) return Vector2.Zero;
            float scaled = MathF.Min((magnitude - deadzone) / range, 1f);
            return v / magnitude * scaled;
        }

        /// <summary>1D counterpart of <see cref="ApplyDeadzone(Vector2,float)"/>, for a trigger or any other single-axis analog reading in <c>[0, 1]</c>.</summary>
        public static float ApplyDeadzone(float value, float deadzone)
        {
            if (value < deadzone) return 0f;
            float range = 1f - deadzone;
            return range > 0f ? MathF.Min((value - deadzone) / range, 1f) : 0f;
        }

        /// <summary>Sets <paramref name="player"/>'s gamepad rumble motor speeds, each <c>[0,1]</c> (0 stops, 1 is maximum). Returns <see langword="false"/> if the slot has no connected gamepad or the platform/controller doesn't support vibration.</summary>
        /// <remarks>Stateless — call <c>SetVibration(0, 0, player)</c> to stop. No <c>duration</c>/timer here: deciding when to stop (a fixed pulse, decaying with distance/trauma, etc.) is a per-game decision.</remarks>
        public bool SetVibration(float leftMotor, float rightMotor, int player = 0)
            => GamePad.SetVibration((PlayerIndex)player, leftMotor, rightMotor);

        /// <summary>Same as <see cref="SetVibration(float,float,int)"/>, plus the two trigger-impulse motors some controllers (Xbox One/Series) have separately from the main motors.</summary>
        public bool SetVibration(float leftMotor, float rightMotor, float leftTrigger, float rightTrigger, int player = 0)
            => GamePad.SetVibration((PlayerIndex)player, leftMotor, rightMotor, leftTrigger, rightTrigger);

        // Deliberately excludes the analog stick/trigger "as digital button" flags MonoGame also
        // reports under Buttons (LeftThumbstickLeft, RightTrigger, etc.) -- those fire from idle
        // drift or a light trigger rest, which would make IsAnyButtonPressed misfire during a
        // "press any button to join" lobby screen instead of only on a deliberate press.
        private static readonly Buttons[] DigitalButtons =
        {
            Buttons.A, Buttons.B, Buttons.X, Buttons.Y,
            Buttons.Start, Buttons.Back, Buttons.BigButton,
            Buttons.LeftShoulder, Buttons.RightShoulder,
            Buttons.LeftStick, Buttons.RightStick,
            Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight,
        };

        /// <summary>
        /// True on the frame any face/shoulder/stick-click/D-pad/Start/Back button on
        /// <paramref name="player"/>'s gamepad went from up to down — for a "press any button to
        /// join" lobby flow, one call instead of listing every button yourself. See
        /// <see cref="DigitalButtons"/>'s comment for why stick/trigger movement doesn't count.
        /// </summary>
        public bool IsAnyButtonPressed(int player = 0)
        {
            foreach (Buttons button in DigitalButtons)
                if (IsButtonPressed(button, player)) return true;
            return false;
        }

        // ---------------------------------------------------------------------
        // Composite helpers (Godot's Input.get_axis/get_vector shape)
        // ---------------------------------------------------------------------

        /// <summary>-1/0/1 axis from two keys: -1 if only <paramref name="negative"/> is held, +1 if only <paramref name="positive"/> is held, 0 if neither or both are held — matching Godot's own <c>Input.get_axis</c>, which cancels to 0 on a tie the same way (strength(positive) - strength(negative)), not a "positive wins" rule.</summary>
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

        /// <summary>WASD movement direction — shorthand for <c>GetVector2(Keys.A, Keys.D, Keys.W, Keys.S, normalize)</c>. W/S map to -Y/+Y, the same screen-space convention every camera and demo in this library already reads keyboard movement with.</summary>
        public Vector2 GetWASD(bool normalize = true) => GetVector2(Keys.A, Keys.D, Keys.W, Keys.S, normalize);

        /// <summary>Arrow-key movement direction — same shape and Y convention as <see cref="GetWASD"/>.</summary>
        public Vector2 GetArrowKeys(bool normalize = true) => GetVector2(Keys.Left, Keys.Right, Keys.Up, Keys.Down, normalize);

        /// <summary>Combined movement direction from every source at once — WASD, arrow keys, the left thumbstick (deadzoned), and the D-pad — so you don't have to pick one input method or merge them by hand.</summary>
        /// <remarks>Every source shares this library's Y-down convention; the thumbstick's own raw Y (hardware convention: up is positive) is flipped before summing to match. <paramref name="normalize"/> caps the combined magnitude at 1 without flattening a slower analog push into a full-speed digital one — same behavior as <see cref="GetVector2"/>.</remarks>
        public Vector2 GetInputDirection(int player = 0, bool normalize = true)
        {
            Vector2 v = GetVector2(Keys.A, Keys.D, Keys.W, Keys.S, normalize: false)
                      + GetVector2(Keys.Left, Keys.Right, Keys.Up, Keys.Down, normalize: false);

            Vector2 stick = LeftStickDeadzoned(player);
            v += new Vector2(stick.X, -stick.Y);

            if (IsButtonDown(Buttons.DPadLeft, player)) v.X -= 1f;
            if (IsButtonDown(Buttons.DPadRight, player)) v.X += 1f;
            if (IsButtonDown(Buttons.DPadUp, player)) v.Y -= 1f;
            if (IsButtonDown(Buttons.DPadDown, player)) v.Y += 1f;

            if (normalize && v.LengthSquared() > 1f) v = Vector2.Normalize(v);
            return v;
        }
    }
}
