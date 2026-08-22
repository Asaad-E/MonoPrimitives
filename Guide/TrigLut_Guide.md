# TrigLut — Guide

`TrigLut` (namespace `MonoPrimitives.Primitives3D`, file [`src/3D/TrigLut.cs`](../src/3D/TrigLut.cs)) is a precomputed sin/cos lookup table — fast, allocation-free trig for building your own curved 3D geometry (a custom ring of vertices, a procedural mesh) without redoing this table yourself. `Primitive3DBatchShapes.cs` uses it internally for every curved shape it builds (circles, spheres, cylinders, torus, capsules); it's exposed publicly for the same reason 2D's `UnitCircleLut` is.

Unlike `UnitCircleLut` (which returns a `Vector2`), every `TrigLut` method returns `sin`/`cos` as separate `out float` parameters — it's meant for tight vertex-building loops that already work in raw floats, not `Vector2` pairs.

## Quick start

```csharp
using MonoPrimitives.Primitives3D;

// Building a ring of `segments` points, the same way FillCircle3D does internally:
TrigLut.SinCosStep(0, segments, out float s0, out float c0);
Vector3 prev = center + axisX * c0 + axisY * s0;
for (int i = 1; i <= segments; i++)
{
    TrigLut.SinCosStep(i, segments, out float s, out float c);
    Vector3 cur = center + axisX * c + axisY * s;
    // ... use prev/cur as one segment of the ring ...
    prev = cur;
}
```

## API

| Member | What it does |
|---|---|
| `Resolution` | `1024` — the number of samples covering a full turn. |
| `Mask` | `Resolution - 1` — the bitmask `SinIndex`/`CosIndex` wrap indices with; exposed in case you're building your own wraparound indexing on top of the same table. |
| `SinIndex(index)` / `CosIndex(index)` | Raw table lookup at a table index (`index * 2*PI/Resolution` radians) — exact, no interpolation. `index` wraps (including negative/out-of-range) via a bitmask, no modulo needed. |
| `SinCosStep(step, steps, out sin, out cos)` | Sine/cosine for step `step` out of `steps` equal divisions of a full circle — the ring/slice-building entry point every curved 3D shape uses. Exact table hit when `steps` divides `Resolution` evenly; linearly interpolates between the two nearest entries otherwise. |
| `Sample(t01, out sin, out cos)` | Sine/cosine at a normalized angle `t01` in turns (`0`=`(0,1)` sin/cos order, `0.25`=`(1,0)`, ...) — the continuous counterpart to `SinCosStep`, for an angle that isn't naturally a division of a circle (an animated phase held as its own float). The 3D equivalent of `UnitCircleLut.Sample(t01)`. |
| `SampleRadians(radians, out sin, out cos)` / `SampleDegrees(degrees, out sin, out cos)` | Same as `Sample`, for an angle already in radians/degrees — a multiply by `TurnsPerRadian`/`TurnsPerDegree`, not a divide, before the lookup. |
| `TurnsPerRadian` / `TurnsPerDegree` | `1/(2*PI)` / `1/360` — the precomputed constants `SampleRadians`/`SampleDegrees` multiply by, exposed in case you're converting a batch of angles yourself. |

All of `SinCosStep`/`Sample`/`SampleRadians`/`SampleDegrees` wrap correctly for negative or out-of-range input — `SinCosStep(-1, steps, ...)` and `Sample(-0.1f, ...)` land on the same table entries as their positive-angle equivalent, to float precision.

## Why 1024, not 512 like `UnitCircleLut`

Same error-vs-radius tradeoff as 2D's table, just tuned for 3D shapes that can be viewed from any distance (a sphere the camera flies close to shows more of its own curvature error than a 2D circle drawn flat to the screen ever does). At `Resolution=1024` the worst-case interpolation error is ~1/Resolution² of a turn — negligible in practice; see `Design/DECISIONS.md` for the numeric verification. Not a knob to retune without a concrete case where it's visibly not enough.

## Testing

[`tests/MonoPrimitives.Tests/TrigLutTests.cs`](../tests/MonoPrimitives.Tests/TrigLutTests.cs) checks `SinIndex`/`CosIndex`'s cardinal points and negative/out-of-range wrapping, `SinCosStep`'s exact-division fast path against real trig across several divisor counts, its interpolated generic path against real trig including negative `step` (a regression test for a real, if small, imprecision this had — see `Design/DECISIONS.md`), `Sample`/`SampleRadians`/`SampleDegrees`'s mutual agreement and agreement with `Math.Sin`/`Cos` across a wide range of angles including negative ones, and continuity across the `t01=0/1` wrap boundary. Run with:

```bash
dotnet run --project tests/MonoPrimitives.Tests/MonoPrimitives.Tests.csproj
```

## See also

- [`Design/DECISIONS.md`](../Design/DECISIONS.md) — the negative-`step` imprecision found and fixed, and the `Sample`/`SampleRadians`/`SampleDegrees` additions.
- [`Guide/UnitCircleLut_Guide.md`](UnitCircleLut_Guide.md) — the 2D counterpart, `Vector2`-shaped instead of `out float` pairs.
