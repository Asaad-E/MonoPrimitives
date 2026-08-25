# FrameLimiter — Guide

`FrameLimiter` (namespace `MonoPrimitives`, file [`src/Core/FrameLimiter.cs`](../src/Core/FrameLimiter.cs)) paces a game loop to a target framerate more precisely than `Game.IsFixedTimeStep`'s own timer, by sleeping out most of a frame's remaining time and busy-spinning the last couple of milliseconds for precision.

## Quick start

```csharp
using MonoPrimitives;

private FrameLimiter _limiter;

public Game1()
{
    _graphics = new GraphicsDeviceManager(this); // construct this FIRST
    _limiter = new FrameLimiter(this, targetFps: 60f); // then this
}

protected override void Update(GameTime gameTime)
{
    float dt = _limiter.BeginFrame(); // real seconds since the previous BeginFrame, clamped to MaxFrameTime
    // ... your update logic ...
}

protected override void Draw(GameTime gameTime)
{
    // ... your draw logic ...
    _limiter.EndFrame();
}
```

Construct `FrameLimiter` **after** your `GraphicsDeviceManager` — the constructor disables vsync by reading the manager already registered as a service on `Game`, and silently does nothing if none is found yet.

## API

| Member | What it does |
|---|---|
| `new FrameLimiter(game, targetFps = 60f, maxFrameTime = 0f, fpsSampleCount = 60)` | Sets `game.IsFixedTimeStep = false` and disables vsync (`SynchronizeWithVerticalRetrace = false`) on the game's `GraphicsDeviceManager`, if one is already registered. Throws on a null `game`, non-positive `targetFps`, negative `maxFrameTime`, or non-positive `fpsSampleCount`. |
| `TargetFps` | Editable at any time — takes effect on the very next `EndFrame()`. |
| `MaxFrameTime` | Editable at any time. Upper bound (seconds) `BeginFrame()` clamps its returned/stored frame time to. `0` (default) disables clamping. |
| `FrameTime` | The value `BeginFrame()` most recently returned. `0` before the first call. |
| `BeginFrame()` | Marks the start of a frame — call once, before doing any of the frame's own work. Measures real time since the previous call (`0` the first time), feeds that raw value into the FPS readouts below, clamps it to `MaxFrameTime` if that's non-zero and exceeded, stores the clamped result in `FrameTime`, and returns it. |
| `EndFrame()` | Blocks until `TargetFps`'s worth of time has passed since `BeginFrame()`. Returns immediately if the frame's own work already ran long. |
| `Elapsed` | Time since the current frame's `BeginFrame()`, read live — for a debug overlay showing frame-budget usage mid-frame without waiting for `EndFrame()`. Read-only: the internal `Stopwatch` itself isn't exposed, so nothing outside this class can `Stop()`/`Reset()` it and break `EndFrame()`'s own pacing. |
| `AverageFps` / `CurrentFps` / `AverageFrameTimeMs` / `CurrentFrameTimeMs` | Same readouts as `FpsCounter` — `FrameLimiter` keeps one internally, fed automatically by `BeginFrame()`, so pacing your loop with `FrameLimiter` gets you an FPS counter with no separate object to construct or `Update` yourself. Based on the **raw, unclamped** frame time — a real stall still shows up here even when `MaxFrameTime` hides it from `FrameTime`. |
| `FpsSampleCount` | The rolling-average window size, set once via the constructor's `fpsSampleCount`. |

Call `BeginFrame()`/`EndFrame()` once per real frame — typically the first line of `Update` and the last line of `Draw`. `MaxFrameTime` guards against a huge simulated step after a real stall (breakpoint, GC pause, asset load) — e.g. `maxFrameTime: 0.25f` caps `dt` at a quarter-second no matter how long the actual gap was; the FPS readouts stay unaffected, so you can still see the stall on an on-screen counter.

## Why disable `IsFixedTimeStep` and vsync

Both are their own frame-pacing mechanisms. Leaving either on means two different systems trying to control the same frame timing at once — `IsFixedTimeStep` would keep calling `Update` at its own fixed cadence regardless of what `FrameLimiter` decides, and vsync would additionally block `Present()` until the next monitor refresh, capping the framerate at the display's own refresh rate no matter what `TargetFps` asks for. `FrameLimiter` takes over both jobs itself, so it disables the built-in ones at construction rather than fighting them every frame.

## A real limitation, not a bug

On Windows, any single call to `Thread.Sleep` — which `EndFrame()` uses for most of the wait — has a real (measured, roughly 1-5%) chance of running for nearly a full extra frame, regardless of how the remaining time is split between sleeping and a precise busy-spin tail. This is OS scheduler jitter, not something under this class's control; a pure busy-spin loop the whole frame would avoid it entirely, at the cost of pinning a full CPU core for the whole frame — the wrong tradeoff for a general-purpose prototyping library, so `FrameLimiter` accepts the rare jitter instead. If your game needs frame timing free of any such hiccup, that's a different (and much more involved) problem than this class solves. See [`Design/DECISIONS.md`](../Design/DECISIONS.md) for the measurements behind this.

## See also

- [`Design/DECISIONS.md`](../Design/DECISIONS.md) — why the busy-spin "tail" strategy (plain spin vs. `Thread.Yield`/`Thread.SpinWait`) turned out not to matter, and the Sleep-jitter measurements above.
