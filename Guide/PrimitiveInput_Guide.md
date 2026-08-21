# PrimitiveInput — Guide

`PrimitiveInput` (namespace `MonoPrimitives`, file [`src/Core/PrimitiveInput.cs`](../src/Core/PrimitiveInput.cs)) polls keyboard, mouse, and gamepad state once per frame and exposes down/pressed/released queries, plus a handful of composite helpers that turn several individual key bindings into one call. It's the input layer `Camera2D`/`Camera3D`'s `UpdateWithInput` are themselves built on.

## Quick start

```csharp
using MonoPrimitives;

private PrimitiveInput _input;

protected override void Initialize()
{
    _input = new PrimitiveInput(); // or new PrimitiveInput(Window) -- see "Typed text" below
    base.Initialize();
}

protected override void Update(GameTime gameTime)
{
    _input.Update(gameTime); // first line -- every query below reads this frame's snapshot
    if (_input.IsKeyPressed(Keys.Space)) Jump();
    base.Update(gameTime);
}
```

Call `Update` exactly once per frame, before reading anything else — every `Is*Down`/`Is*Pressed`/`Is*Released` query compares this frame's polled state against the previous frame's, so `Update` is what actually advances "previous."

## Keyboard

| Method | What it does |
|---|---|
| `IsKeyDown(key)` / `IsKeyUp(key)` | Held / not held. |
| `IsKeyPressed(key)` / `IsKeyReleased(key)` | True for exactly one frame — the up→down or down→up transition. |
| `IsAnyKeyPressed()` | True the frame any key at all transitions to down — a "press any key to continue" prompt in one call. |
| `CapsLock` / `NumLock` | Whether the lock is currently toggled **on** — the lock state itself, not whether the physical key is held. Useful for a custom on-screen keyboard, or a "Caps Lock is on" warning next to a password field. |

## Mouse

| Member | What it does |
|---|---|
| `MousePosition` | Cursor position in screen pixels, origin top-left. |
| `MouseDelta` | Movement since the last `Update`, in pixels. |
| `MouseScrollDelta` / `MouseHorizontalScrollDelta` | Wheel movement since the last `Update` (120 units per notch). |
| `IsMouseButtonDown/Up/Pressed/Released(button)` | Same shape as the keyboard, for `MouseButton.Left/Right/Middle/XButton1/XButton2`. |
| `IsAnyMouseButtonPressed()` | Same idea as `IsAnyKeyPressed`, for the mouse. |
| `IsMouseButtonDoubleClicked(button)` | True the frame a button is pressed again within `DoubleClickTime` seconds **and** `DoubleClickDistance` pixels of its previous press — both configurable properties, so two clicks in unrelated corners of the screen don't count. |
| `DragDelta(button)` | Total movement since `button` was last pressed. Live-tracks `MousePosition` while held; once released, keeps reporting the drag's final distance (not a stale zero) for the rest of that frame and after, until the next press starts a new drag — so checking it from inside `if (IsMouseButtonReleased(button))` (a swipe/flick gesture) sees the real distance. |
| `IsDragging(button, threshold = 4f)` | True while held **and** moved more than `threshold` px from the press point — lets a click handler and a drag handler share one button without the click firing on every tiny press-time jitter. Unlike `DragDelta`, always false once released. |
| `IsMouseOver(rect)` | Point-in-rectangle test against `MousePosition` — hit-testing a panel/button without a UI library. |
| `SetMousePosition(x, y)` | Moves the OS cursor — e.g. re-centering every frame for an FPS-style mouse-look that never hits the window edge (see "What this can't do" below). Pair with `ResetMouseDelta()` or accept one frame of a large jump-delta. |
| `SetCursor(cursor)` | Sets the OS cursor's shape — one of `MouseCursor`'s built-ins (`Arrow`, `IBeam`, `Hand`, `Crosshair`, the resize arrows, `SizeAll`, `No`, `WaitArrow`, `Wait`) or a fully custom one via `MouseCursor.FromTexture2D(texture, originX, originY)`. |
| `ResetMouseDelta()` | Zeroes delta tracking for the next `Update` — call after teleporting the cursor or regaining window focus, so `MouseDelta` doesn't report a one-frame snap. |

## Gamepad (player index 0-3)

| Member | What it does |
|---|---|
| `IsConnected(player = 0)` | Whether a controller is plugged into that slot. |
| `IsButtonDown/Up/Pressed/Released(button, player = 0)` | Same shape as keyboard/mouse, for MonoGame's `Buttons` enum (face buttons, shoulders, stick clicks, D-pad, Start/Back, BigButton). |
| `IsAnyButtonPressed(player = 0)` | True the frame any **digital** button transitions to down — face/shoulder/stick-click/D-pad/Start/Back only. Deliberately excludes the analog stick/trigger "as digital button" flags MonoGame also reports under `Buttons`, so idle stick drift or a light trigger rest can't misfire a "press any button to join" screen. |
| `LeftStick(player = 0)` / `RightStick(player = 0)` | Raw thumbstick, MonoGame's convention (X right-positive, Y up-positive), no deadzone. |
| `LeftTrigger(player = 0)` / `RightTrigger(player = 0)` | Trigger pull, `[0,1]`. |
| `LeftStickDeadzoned` / `RightStickDeadzoned` (`deadzone = 0.15f`) | Stick with a circular deadzone — snaps to zero within `deadzone` of center instead of reporting idle drift as movement. |
| `LeftTriggerDeadzoned` / `RightTriggerDeadzoned` (`deadzone = 0.05f`) | Same idea for triggers, since some controllers rest slightly above `0`. |
| `SetVibration(leftMotor, rightMotor, player = 0)` | Sets rumble motor speeds, `[0,1]` each. Returns `false` if the slot isn't connected or the platform/controller doesn't support it. |
| `SetVibration(leftMotor, rightMotor, leftTrigger, rightTrigger, player = 0)` | Same, plus the two trigger-impulse motors some controllers (Xbox One/Series) have separately from the main motors. |

**`SetVibration` is a raw, stateless passthrough — there's no `duration` parameter.** Call `SetVibration(0, 0, player)` to stop. Deciding *how long* to rumble (a fixed pulse, decaying with impact distance, etc.) is a per-game decision, the same reasoning that keeps every other "system" out of this library — see `examples/test/InputPanelTest`, which owns its own short rumble timer around the raw call.

## Typed text (`GetCharPressed`)

Everything above is `Keys`/`Buttons` **polling** — and polling fundamentally cannot produce correct typed text. `Keys` is physical key identity, not the character a keyboard layout/shift/dead-key combination actually produces (`Keys.Q` is `'A'` on AZERTY; this library's own `DebugFont5x7` supports Spanish accents like `'á'`, which are typically composed from a dead-key sequence only the OS can resolve), and OS key-repeat timing can't be reconstructed by guessing at settings the OS already knows. Even raylib's own `GetCharPressed` — despite its "call it in a loop" polling *feel* — is backed by a real OS/GLFW character-composition event under the hood, not raw key-state polling.

So `PrimitiveInput` has a second constructor for this:

```csharp
_input = new PrimitiveInput(Window); // subscribes to Window.TextInput

// each frame:
char c;
while ((c = _input.GetCharPressed()) != '\0')
    typedText += c;
if (_input.IsKeyPressed(Keys.Back) && typedText.Length > 0)
    typedText = typedText[..^1]; // Backspace/Enter are editing controls, poll them separately
```

- The **parameterless constructor** skips this entirely — `GetCharPressed()` then always returns `'\0'`, never throws, since there's simply nothing subscribed to feed it.
- The queue is a fixed 64-character ring buffer, not a growable list — if you never drain it, new characters are dropped once full rather than growing memory unboundedly.
- `PrimitiveInput` implements `IDisposable` solely to unsubscribe the `TextInput` handler when you're done with a `GameWindow`-backed instance; skipping `Dispose()` costs nothing for the common case of one `PrimitiveInput` living the whole game.
- See `examples/test/InputPanelTest` for a working "type here" box, including Spanish accents.

## What this can't do (and why)

Two things worth knowing before you go looking for them:

- **A true "relative/captured" mouse mode** (like love2d's `setRelativeMode`, Godot's `MOUSE_MODE_CAPTURED`, Unity's `Cursor.lockState`) — MonoGame's own `Mouse` class doesn't expose this at all (checked: it only has `GetState`/`SetPosition`/`SetCursor`/`WindowHandle`). The manual re-center-every-frame trick `SetMousePosition`/`ResetMouseDelta` already support is the actual ceiling here, not a missing convenience wrapper — there's no lower-level toggle in MonoGame's public API to wrap.
- **Correct typed text from key polling alone** — see `GetCharPressed` above; this needs the `GameWindow` constructor, there's no way around it.

Deliberately **not** in scope, regardless of what other engines offer: an input *action*/binding-map system (Unity's Input System package, Godot's named actions). This library hands you the poll, not a rebindable-action layer on top of it — building that yourself with `GetAxis`/`GetVector2` as the last mile is the point of reaching for a toolkit instead of a framework.

## See also

- [`Design/DECISIONS.md`](../Design/DECISIONS.md) — the audit trail behind each addition here (why `SetVibration` has no duration, why `IsAnyButtonPressed` excludes analog-as-digital flags, the `GetCharPressed`/`TextInput` verification).
- [`Guide/Camera2D_Guide.md`](Camera2D_Guide.md) — `Camera2D.UpdateWithInput`, the built-in WASD/drag/wheel controller built on top of this class.
