# FpsCounter — Guide

`FpsCounter` (namespace `MonoPrimitives`, file [`src/Core/FpsCounter.cs`](../src/Core/FpsCounter.cs)) tracks a rolling average frames-per-second over the last `SampleCount` frames. It's a measurement only — it doesn't draw anything itself; pair it with your own `DrawString`/`DrawString3D` call to show the number.

## Quick start

```csharp
using MonoPrimitives;

private readonly FpsCounter _fps = new(sampleCount: 60);

protected override void Draw(GameTime gameTime)
{
    _fps.Update(gameTime);
    // ... your drawing ...
    _batch2d.Begin();
    _batch2d.DrawString($"{_fps.AverageFps:F0} FPS", new Vector2(8, 8), 2f, Color.White);
    _batch2d.End();
}
```

## API

| Member | What it does |
|---|---|
| `new FpsCounter(sampleCount = 60)` | `sampleCount` sets the averaging window — smaller reacts faster to real framerate changes, larger reads more stable. Throws on a non-positive value. |
| `SampleCount` | The window size passed at construction. |
| `Update(GameTime)` / `Update(float deltaSeconds)` | Records one frame's elapsed time. Call exactly once per frame. |
| `AverageFps` | Frames ÷ total time over the window — a *true* windowed average, not an average of each frame's own instantaneous FPS (which would over-weight a few unusually fast frames instead of reflecting how long the window actually took). `0` before the first `Update`. |
| `CurrentFps` | FPS implied by the single most recent frame alone — noisier than `AverageFps`, useful for spotting an isolated spike or stall. |
| `AverageFrameTimeMs` | Total time ÷ frames over the window, in milliseconds — the same average `AverageFps` is built on, read before the reciprocal. Fps compresses the low end and expands the high end of the scale (60→59 fps is 0.28 ms, 15→14 fps is 4.8 ms, same "1 fps"), so this is the more precise number for comparing against a fixed frame budget (e.g. 16.6 ms for 60 fps). `0` before the first `Update`. |
| `CurrentFrameTimeMs` | Milliseconds for the single most recent frame alone — noisier than `AverageFrameTimeMs`, useful for spotting an isolated spike or stall. |

Before the window fills up (fewer than `SampleCount` calls to `Update` so far), the averages are computed only from the samples actually recorded — they aren't diluted by empty slots.

## See also

- [`FrameLimiter_Guide.md`](FrameLimiter_Guide.md) — a related but different job: `FrameLimiter` *paces* the loop to a target FPS, `FpsCounter` just *measures* whatever FPS you're actually getting. Use one, the other, both, or neither independently.
