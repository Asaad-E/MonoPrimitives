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

Both are frame-pacing mechanisms in their own right, and leaving either on just means two systems fighting over the same job — `IsFixedTimeStep` calling `Update` on its own cadence regardless of what `FrameLimiter` wants, vsync capping the framerate at the monitor's refresh rate no matter what `TargetFps` says. So `FrameLimiter` turns both off at construction and takes over.

## A real limitation, not a bug

`EndFrame()` waits out most of the frame with `Thread.Sleep`, and on Windows a single `Sleep` call has a small but real chance (measured around 1-5%) of running nearly a full extra frame — OS scheduler jitter, outside this class's control. A pure busy-spin the whole frame would dodge it, but pins a full CPU core the entire time, which isn't the right tradeoff for a general prototyping library. If you need frame timing with zero jitter at all, that's a bigger problem than this class is trying to solve. See [`Design/DECISIONS.md`](../Design/DECISIONS.md) for the numbers.
