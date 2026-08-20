# Architecture — current-state map

Read PROJECT.md first. This is *what exists now*; DECISIONS.md is *why*.

One project/assembly (`src/MonoPrimitives.csproj` → `MonoPrimitives.dll`). `Core/`, `2D/`, `3D/` under `src/` are organization only — what matters is the namespace (`MonoPrimitives`/`Primitives2D`/`MonoPrimitives3D`).

## `src/Core/` — namespace `MonoPrimitives`

Shared foundation used by both 2D and 3D — nothing here is 2D- or 3D-specific.

| File | Purpose |
|---|---|
| `Easing.cs` | 0→1 tween curves (Quad/Cubic/Quart/Expo/Sine/Back/Bounce/Elastic). |
| `Palette.cs` | 21 curated colors + `Background`, `All`/`Primary` arrays, `Cycle(index)`. |
| `ColorUtil.cs` | Hex ↔ `Color`, HSV ↔ `Color`, `Lighten`/`Darken`/`Saturate`/`Desaturate`/`Complementary`, `Lerp`, `LerpHSV` (hue-wheel-aware). Hue is a turn in [0,1), not degrees. |
| `Noise.cs` | Seedable Perlin noise: `Sample1D`/`2D`/`3D` (1D/2D are slices of the 3D implementation) + `Fbm1D`/`2D`/`3D`. |
| `PrimitiveInput.cs` | Keyboard/mouse/gamepad polling, `GetAxis`/`GetVector2`, mouse drag/double-click/hit-test. `Update(GameTime)` once per frame. |
| `FontGlyphs5x7.cs` | Raw 5×7 glyph bitmap data + layout math for the debug font. No rendering (2D and 3D each draw it differently). |

## `src/2D/` — namespace `Primitives2D`

| File | Purpose |
|---|---|
| `Primitives2D.cs` | `PrimitiveBatch` — all shape drawing (Fill/Border/Draw per shape, rounded-corner and gradient variants), outline/fillet engine, points/lines/splines/`DrawArrow`. Large; grep, don't read linearly. |
| `Camera2D.cs` | Transform matrix, screen↔world, bounds/padding, smooth-follow/zoom. |
| `ViewportAdapter2D.cs` (+ `Boxing`/`Scaling`/`Default`/`Window` variants) | MonoGame.Extended-parity viewport adapter family: `BoxingViewportAdapter2D` (letterbox/pillarbox, uniform scale), `ScalingViewportAdapter2D` (stretch to fill, non-uniform scale), `DefaultViewportAdapter2D` (1:1, tracks device viewport), `WindowViewportAdapter2D` (1:1, tracks `GameWindow.ClientBounds`). All expose the same `GetScaleMatrix()`/`PointToVirtual`/`VirtualToPoint` surface — compose with `Camera2D` the same way regardless of which one's in use. |
| `Collision2D.cs` | Overlap tests + 3 raycasts. Detection only. |
| `DebugFont5x7.cs` | `DrawString`/`MeasureText` on `PrimitiveBatch`, via `FillRectangle`. |
| `Trail2D.cs` | Fixed-capacity fading position history. |

## `src/3D/` — namespace `MonoPrimitives3D`

| File | Purpose |
|---|---|
| `Primitive3DBatch.cs` | Core batch: `Begin`/`End`/`Flush`, opt-in flat shading, `BuildBasis` (orthonormal basis), `ResolveSegments` (auto-LOD). |
| `Primitive3DBatchShapes.cs` | Cube/Sphere/Cylinder/Capsule/Torus/Heightmap/Plane/Grid/`DrawAxis`/splines/`DrawArrow`. Every shape is `Fill`/`Border`/`Draw` overloads of one name (no `Ex`/`V`-suffixed siblings — a two-endpoint cylinder, a vector-size cube, etc. are just another overload). `DrawGridXY/XZ/YZ` draw the grid only; `DrawAxis` is separate. Large; grep, don't read linearly. |
| `Camera3D.cs` | View/projection, multiple modes, bounds/padding/follow/zoom, `ReadDefaultInput`/`Update` (both have `GameTime` overloads) (uses `PrimitiveInput`). Camera + controller merged into one class. Movement/rotation/sensitivity speeds are editable properties (`MoveSpeed`, `RotationSpeed`, etc.), not constants. |
| `Collision3D.cs` | Wraps `BoundingSphere`/`BoundingBox`/`Ray`, plus capsule support and plane raycasts. |
| `TrigLut.cs` | Precomputed sin/cos table for per-vertex trig. |
| `Trail3D.cs` | 3D counterpart to `Trail2D`. |
| `DebugFont5x7.cs` | `DrawString3D`/`MeasureText3D`/`GetBillboardAxes` — billboarded text, cylindrical facing, never lit. |

## `samples/MonoPrimitives.Sample/`

Minimal runnable MonoGame game referencing `MonoPrimitives`, plus `MonoGame.Extended` (sample-only). Proves the package works end-to-end; not a real game.

## Machinery worth knowing before you touch nearby code

- `ComputeJoint`/`BuildRoundedCornerBoundary` (2D) — shared fillet engine behind every rounded corner. See DECISIONS.md for its per-corner clamp caveat.
- `FillPolygonGradientByNearestVertex` (2D) — colors a rounded boundary's many points by nearest original vertex (a rounded corner's arc has no 1:1 vertex mapping).
- Blend state is `NonPremultiplied` in both batches — see DECISIONS.md.
- `PushQuadLit`/`PushTriangleLit` (3D) — face normal comes from vertex winding order; get it backwards and lighting/culling breaks silently. See DECISIONS.md.
- `SampleUnitCircle` (2D) / `TrigLut` (3D) — precomputed trig tables. Use these for new curved geometry, not raw `MathF.Sin`/`Cos`.
