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

Before the window fills up (fewer than `SampleCount` calls to `Update` so far), `AverageFps` is computed only from the samples actually recorded — it isn't diluted by empty slots.

## See also

- [`FrameLimiter_Guide.md`](FrameLimiter_Guide.md) — a related but different job: `FrameLimiter` *paces* the loop to a target FPS, `FpsCounter` just *measures* whatever FPS you're actually getting. Use one, the other, both, or neither independently.
