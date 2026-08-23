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
    _limiter.BeginFrame();
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
| `new FrameLimiter(game, targetFps = 60f)` | Sets `game.IsFixedTimeStep = false` and disables vsync (`SynchronizeWithVerticalRetrace = false`) on the game's `GraphicsDeviceManager`, if one is already registered. Throws on a null `game` or non-positive `targetFps`. |
| `TargetFps` | Editable at any time — takes effect on the very next `EndFrame()`. |
| `BeginFrame()` | Marks the start of a frame — call once, before doing any of the frame's own work. |
| `EndFrame()` | Blocks until `TargetFps`'s worth of time has passed since `BeginFrame()`. Returns immediately if the frame's own work already ran long. |

Call `BeginFrame()`/`EndFrame()` once per real frame — typically the first line of `Update` and the last line of `Draw`.

## Why disable `IsFixedTimeStep` and vsync

Both are their own frame-pacing mechanisms. Leaving either on means two different systems trying to control the same frame timing at once — `IsFixedTimeStep` would keep calling `Update` at its own fixed cadence regardless of what `FrameLimiter` decides, and vsync would additionally block `Present()` until the next monitor refresh, capping the framerate at the display's own refresh rate no matter what `TargetFps` asks for. `FrameLimiter` takes over both jobs itself, so it disables the built-in ones at construction rather than fighting them every frame.

## A real limitation, not a bug

On Windows, any single call to `Thread.Sleep` — which `EndFrame()` uses for most of the wait — has a real (measured, roughly 1-5%) chance of running for nearly a full extra frame, regardless of how the remaining time is split between sleeping and a precise busy-spin tail. This is OS scheduler jitter, not something under this class's control; a pure busy-spin loop the whole frame would avoid it entirely, at the cost of pinning a full CPU core for the whole frame — the wrong tradeoff for a general-purpose prototyping library, so `FrameLimiter` accepts the rare jitter instead. If your game needs frame timing free of any such hiccup, that's a different (and much more involved) problem than this class solves. See [`Design/DECISIONS.md`](../Design/DECISIONS.md) for the measurements behind this.

## See also

- [`Design/DECISIONS.md`](../Design/DECISIONS.md) — why the busy-spin "tail" strategy (plain spin vs. `Thread.Yield`/`Thread.SpinWait`) turned out not to matter, and the Sleep-jitter measurements above.
