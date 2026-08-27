# WindowUtil — Guide

`WindowUtil` (namespace `MonoPrimitives`, file [`src/Core/WindowUtil.cs`](../src/Core/WindowUtil.cs)) covers window/monitor/clipboard operations `GameWindow` and `GraphicsDeviceManager` don't expose at all: minimize/maximize/restore, opacity, a title-bar/taskbar icon, per-monitor position/size/refresh rate, and system clipboard text — plus a captured-cursor delta helper for mouse-look input.

It doesn't create or own a window — you already have one (`Game.Window`) and a `Game` instance; every method here just operates on those.

## Quick start

```csharp
using MonoPrimitives;

protected override void Initialize()
{
    Window.AllowUserResizing = true; // needed for MaximizeWindow to actually take effect -- see Notes
    // ... your usual setup ...
    base.Initialize();
}

protected override void Update(GameTime gameTime)
{
    if (_input.IsKeyPressed(Keys.F11))
    {
        if (WindowUtil.IsWindowMaximized(Window)) WindowUtil.RestoreWindow(Window);
        else WindowUtil.MaximizeWindow(Window);
    }

    base.Update(gameTime);
}
```

## API

| Member | What it does |
|---|---|
| `IsAvailable` | True once the SDL2 entry points this class needs were resolved. Check this (or just trust the graceful fallbacks below) once at startup — log `Diagnostics` if you want to know why. |
| `Diagnostics` | Human-readable explanation of whether SDL2 was found and, if not, why. |
| `MinimizeWindow(window)` / `MaximizeWindow(window)` / `RestoreWindow(window)` | Minimize, maximize, or restore. No-op if `IsAvailable` is false. |
| `IsWindowMinimized(window)` / `IsWindowMaximized(window)` | Current state. Always false if `IsAvailable` is false. |
| `SetWindowOpacity(window, opacity)` / `GetWindowOpacity(window)` | 0 (transparent) to 1 (opaque), clamped on set. `Get` returns 1 if unsupported. |
| `SetWindowIcon(window, icon)` | Sets the title-bar/taskbar icon from a texture's current pixels. `icon` must be `SurfaceFormat.Color` (throws `ArgumentException` otherwise) — the format images loaded via `Texture2D.FromStream` or the content pipeline already use. |
| `GetMonitorCount()` | Number of connected monitors. Throws `InvalidOperationException` if `IsAvailable` is false. |
| `GetMonitorInfo(index)` | Returns a `MonitorInfo` (`Index`, `Name`, `Bounds` — desktop position+size, `RefreshRate` in Hz, 0 if unknown) for one monitor. Throws `ArgumentOutOfRangeException` for a bad index, `InvalidOperationException` if unavailable. |
| `GetCurrentMonitorIndex(window)` | The monitor containing most of the window. |
| `SetClipboardText(text)` / `GetClipboardText()` | System clipboard round-trip. `Set` no-ops if unavailable; `Get` throws `InvalidOperationException`. |
| `DisableCursor(game)` / `EnableCursor(game)` / `GetCursorDelta(window)` | Mouse-look capture — see below. Works on every backend, not just Desktop GL. |

## Mouse-look capture

MonoGame has no real relative/captured mouse mode — `DisableCursor`/`GetCursorDelta`/`EnableCursor` package the standard hide-and-recenter-every-frame workaround into three calls:

```csharp
protected override void Update(GameTime gameTime)
{
    if (_input.IsKeyPressed(Keys.Tab))
        _looking = !_looking;

    if (_looking)
    {
        if (justEnabled) WindowUtil.DisableCursor(this);
        Point delta = WindowUtil.GetCursorDelta(Window); // call this once per frame while captured
        _camera.Yaw += delta.X * lookSpeed;
        _camera.Pitch += delta.Y * lookSpeed;
    }
    else if (justDisabled)
    {
        WindowUtil.EnableCursor(this);
    }

    base.Update(gameTime);
}
```

`GetCursorDelta` reads how far the cursor moved since the window's center, then recenters it — call it exactly once per frame between `DisableCursor` and `EnableCursor`, not on every frame regardless of capture state (outside capture, the cursor isn't being recentered, so the delta would be meaningless).

## Notes

- Everything except cursor capture is Desktop GL only, resolved directly from the SDL2 library MonoGame's DesktopGL backend already loads — the same below-MonoGame approach `FastTexture` uses for raw GL uploads, for the same reason (MonoGame's own public API doesn't reach this far).
- **`MaximizeWindow` silently does nothing on Windows unless `GameWindow.AllowUserResizing` is `true`.** The OS ignores a maximize request for a window that was never given a maximize/thick-frame style — the same reason such a window's title bar has no maximize button. `MinimizeWindow`/`RestoreWindow` don't have this restriction.
- Action methods (`Minimize`/`Maximize`/`Restore`/`SetWindowOpacity`/`SetWindowIcon`/`SetClipboardText`) no-op when `IsAvailable` is false, so you don't need to guard every call site. Query methods (`GetMonitorInfo`, `GetClipboardText`, `GetMonitorCount`) throw `InvalidOperationException` instead, since there's no honest default value to hand back.
- See `examples/test/WindowUtilTest/` for a runnable demo of every method (an automatic startup verification pass, plus number keys for manual poking).
