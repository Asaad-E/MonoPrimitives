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

`t01` doesn't need to stay within `[0,1)` — negative values and values `>= 1` wrap correctly (exactly, to float precision), so `Sample(-0.1)` and `Sample(0.9)` give the same result, and so does `Sample(5.9)`.

## Why "turns," not radians or degrees

Matches this project's own convention for angle-like values elsewhere (`DrawCircleSector`'s `startAngle`/`endAngle`, `ColorUtil`'s hue) — a full turn is `1.0`, a quarter turn is `0.25`, regardless of direction. If you have an angle in radians, divide by `MathHelper.TwoPi` first.

## Testing

[`tests/MonoPrimitives.Tests/UnitCircleLutTests.cs`](../tests/MonoPrimitives.Tests/UnitCircleLutTests.cs) checks the four cardinal points, that every sample stays on the unit circle (length `~1`, within the small chord-vs-arc error linear interpolation between two lattice points necessarily introduces), continuity across the `t01 = 0/1` wrap seam, and that negative/large `t01` wraps to *exactly* (float-precision) the same result as the mathematically-wrapped equivalent — a regression test for a real, if small, imprecision this class used to have (see `Design/DECISIONS.md`). Run with:

```bash
dotnet run --project tests/MonoPrimitives.Tests/MonoPrimitives.Tests.csproj
```

## See also

- [`Design/DECISIONS.md`](../Design/DECISIONS.md) — the negative-input imprecision found and fixed, including why it wasn't the index bug it first looked like.
- [`Guide/PrimitiveBatch_Guide.md`](PrimitiveBatch_Guide.md) — everything this table is built to serve.
