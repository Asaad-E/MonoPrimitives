# UnitCircleLut — Guide

`UnitCircleLut` (namespace `MonoPrimitives.Primitives2D`, file [`src/2D/UnitCircleLut.cs`](../src/2D/UnitCircleLut.cs)) is a precomputed unit-circle lookup table — fast, allocation-free sin/cos for your own curved geometry (a custom particle ring, a procedural shape) without redoing this table yourself. `PrimitiveBatch` uses it internally for every curved shape it draws; it's exposed publicly for the same reason 3D's `TrigLut` is.

## Quick start

```csharp
using MonoPrimitives.Primitives2D;

Vector2 direction = UnitCircleLut.Sample(angleInTurns); // cos/sin at that angle, trig-free
Vector2 point = center + direction * radius;
```

## API

| Member | What it does |
|---|---|
| `Resolution` | `512` — the number of samples covering a full turn. |
| `Sample(t01)` | Samples the unit circle at a normalized angle `t01` — `0` = `(1,0)`, `0.25` = `(0,1)`, `0.5` = `(-1,0)`, `0.75` = `(0,-1)` (standard math convention, counter-clockwise). Linearly interpolates between the two nearest table entries — trig-free, accurate to well under a pixel at any sane radius. |
| `SampleRadians(radians)` | Same as `Sample`, for an angle already in radians — a multiply by `TurnsPerRadian`, not a divide, before the lookup. |
| `SampleDegrees(degrees)` | Same as `Sample`, for an angle already in degrees — a multiply by `TurnsPerDegree`, not a divide, before the lookup. |
| `TurnsPerRadian` / `TurnsPerDegree` | `1/(2*PI)` / `1/360` — the precomputed constants `SampleRadians`/`SampleDegrees` multiply by, exposed in case you're converting a batch of angles yourself and want to skip the per-call division too. |

`t01`/`radians`/`degrees` don't need to stay within their "natural" range — negative values and values past a full turn wrap correctly (exactly, to float precision), so `Sample(-0.1)`, `Sample(0.9)`, and `Sample(5.9)` all give the same result, and likewise for `SampleRadians`/`SampleDegrees`.

## Why "turns" is still the primary API

`Sample(t01)` stays the base case — it matches this project's own convention for angle-like values elsewhere (`DrawCircleSector`'s `startAngle`/`endAngle`, `ColorUtil`'s hue: a full turn is `1.0`, a quarter turn is `0.25`, regardless of direction) and every other method funnels into it. `SampleRadians`/`SampleDegrees` exist purely as convenience for callers already holding an angle in one of those units (e.g. from `Camera2D.Rotation` or a `MathF.Atan2` result) so they don't have to hand-write the turn conversion at every call site.

## Testing

[`tests/MonoPrimitives.Tests/UnitCircleLutTests.cs`](../tests/MonoPrimitives.Tests/UnitCircleLutTests.cs) checks the four cardinal points, that every sample stays on the unit circle (length `~1`, within the small chord-vs-arc error linear interpolation between two lattice points necessarily introduces), continuity across the `t01 = 0/1` wrap seam, that negative/large `t01` wraps to *exactly* (float-precision) the same result as the mathematically-wrapped equivalent — a regression test for a real, if small, imprecision this class used to have (see `Design/DECISIONS.md`) — and that `SampleRadians`/`SampleDegrees` agree with the equivalent `Sample(t01)` call and with `Math.Sin`/`Cos` (within the table's lattice-spacing error bound) across a wide range of angles, including negative ones. Run with:

```bash
dotnet run --project tests/MonoPrimitives.Tests/MonoPrimitives.Tests.csproj
```

## See also

- [`Design/DECISIONS.md`](../Design/DECISIONS.md) — the negative-input imprecision found and fixed, including why it wasn't the index bug it first looked like.
- [`Guide/PrimitiveBatch_Guide.md`](PrimitiveBatch_Guide.md) — everything this table is built to serve.
