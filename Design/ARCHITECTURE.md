# Architecture — current-state map

Read PROJECT.md first. This is *what exists now*; DECISIONS.md is *why*.

One project/assembly (`src/MonoPrimitives.csproj` → `MonoPrimitives.dll`). `Core/`, `2D/`, `3D/` under `src/` are organization only — what matters is the namespace (`MonoPrimitives`/`MonoPrimitives.Primitives2D`/`MonoPrimitives.Primitives3D`, all nested under the one root namespace).

## `src/Core/` — namespace `MonoPrimitives`

Shared foundation used by both 2D and 3D — nothing here is 2D- or 3D-specific.

| File | Purpose |
|---|---|
| `Easing.cs` | 0→1 tween curves — every family (Quad/Cubic/Quart/Expo/Sine/Back/Bounce/Elastic) has In/Out/InOut. |
| `Palette.cs` | 21 curated flat colors + `Background`, `All`/`Primary` arrays, `Cycle(index)`. Plus `GradientPairs`: inner/outer color pairs for a glossy radial-gradient "juicy ball" look via `FillCircleGradient` — a different flavor from the flat colors, not exhaustive, add more pairs as needed. |
| `ColorUtil.cs` | Hex ↔ `Color`, HSV ↔ `Color`, `Lighten`/`Darken`/`Saturate`/`Desaturate`/`Complementary`, `Lerp`, `LerpHSV` (hue-wheel-aware). Hue is a turn in [0,1), not degrees. |
| `Noise.cs` | Seedable Perlin noise: `Sample1D`/`2D`/`3D` + `Fbm1D`/`2D`/`3D`. `Sample2D`/`3D` share one implementation (2D is a z=0 slice); `Sample1D` has its own dedicated gradient (a naive y=0,z=0 slice would degenerate — see DECISIONS.md). |
| `PrimitiveInput.cs` | Keyboard/mouse/gamepad polling, `GetAxis`/`GetVector2`, mouse drag/double-click/hit-test. `Update(GameTime)` once per frame. `DragDelta`/`IsDragging` still read correctly on the exact frame a drag ends (not reset to zero); `IsMouseButtonDoubleClicked` checks both `DoubleClickTime` and `DoubleClickDistance`. |
| `FontGlyphs5x7.cs` | Raw 5×7 glyph bitmap data + layout math for the debug font. No rendering (2D and 3D each draw it differently). |

## `src/2D/` — namespace `MonoPrimitives.Primitives2D`

| File | Purpose |
|---|---|
| `Primitives2D.cs` | `PrimitiveBatch` — all shape drawing (Fill/Border/Draw per shape, rounded-corner and gradient variants), outline/fillet engine, points/lines/splines/`DrawArrow`/`DrawGrid` (`showMajorLines: bool = true`)/`DrawAxis`/`Fill*Shadow` (soft drop shadows, no shader — see DECISIONS.md). Large; grep, don't read linearly. |
| `Camera2D.cs` | Transform matrix, screen↔world, bounds/padding, smooth-follow/zoom. Two-tier update: `Update` (no input, just decays shake/settles easing — for a camera you drive yourself) and `UpdateWithInput(PrimitiveInput, float/GameTime)` (W/A/S/D pan, left-drag pan, wheel zoom, read from a `PrimitiveInput` instance the caller owns and updates — a prototyping convenience, not baked into `Update`; doesn't call `PrimitiveInput.Update` itself). Same shape as `Camera3D`. No parameterless constructor — use `CreateDefault()` for a placeholder. Trauma-based screen shake (`AddTrauma`, baked into `GetTransformMatrix`) — see DECISIONS.md. Optionally takes a `ViewportAdapter2D` at construction (MonoGame.Extended's `OrthographicCamera(ViewportAdapter)` shape) — when set, `ScreenToWorld`/`WorldToScreen`/`GetVisibleWorldBounds`/`UpdateWithInput`'s mouse-drag pan all account for it automatically instead of assuming raw device pixels. |
| `ViewportAdapter2D.cs` (+ `Boxing`/`Scaling`/`Default`/`Window` variants) | MonoGame.Extended-parity viewport adapter family: `BoxingViewportAdapter2D` (letterbox/pillarbox, uniform scale), `ScalingViewportAdapter2D` (stretch to fill, non-uniform scale), `DefaultViewportAdapter2D` (1:1, tracks device viewport), `WindowViewportAdapter2D` (1:1, tracks `GameWindow.ClientBounds`). All expose the same `GetScaleMatrix()`/`PointToVirtual`/`VirtualToPoint` surface — compose with `Camera2D` the same way regardless of which one's in use. Also usable by 3D via `Primitive3DBatch.Begin(camera, viewportAdapter)`. See `Design/2D/ViewportAdapter_Guide.md`. |
| `Collision2D.cs` | Overlap tests + 3 raycasts. Detection only. |
| `DebugFont5x7.cs` | `DrawString`/`MeasureText` on `PrimitiveBatch`, via `FillRectangle`. |
| `Trail2D.cs` | Fixed-capacity fading position history. `fadeToAlpha` is clamped to [0,1]. |
| `UnitCircleLut.cs` | Public precomputed unit-circle table — the 2D counterpart to the 3D library's `TrigLut`, for your own curved geometry. `PrimitiveBatch` uses it internally instead of keeping a private duplicate. |

## `src/3D/` — namespace `MonoPrimitives.Primitives3D`

| File | Purpose |
|---|---|
| `Primitive3DBatch.cs` | Core batch: `Begin`/`End`/`Flush`, opt-in flat shading, `BuildBasis` (orthonormal basis), `ResolveSegments` (auto-LOD). `Begin(camera)` applies `camera.ViewportAdapter` automatically (letterboxes the 3D projection into its boxed rectangle) when the camera was constructed with one — no separate viewport-taking overload. |
| `Primitive3DBatchShapes.cs` | Cube/Sphere/Cylinder/Capsule/Torus/Heightmap/Plane/Grid/`DrawAxis`/splines/`DrawArrow`. Every shape is `Fill`/`Border`/`Draw` overloads of one name (no `Ex`/`V`-suffixed siblings — a two-endpoint cylinder, a vector-size cube, etc. are just another overload). `DrawGridXY/XZ/YZ` draw the grid only (`showMajorLines: bool = true` toggles the every-5th-line emphasis); `DrawAxis` is separate. Large; grep, don't read linearly. |
| `Camera3D.cs` | View/projection, multiple modes, bounds/padding/follow/zoom. Same two-tier update as `Camera2D`: `Update` (no input — shake decay/easing only) and `UpdateWithInput(PrimitiveInput, float/GameTime)` (W/A/S/D + Space/Ctrl move, Q/E yaw, Z/X roll, right-drag look, wheel zoom, read from a caller-owned `PrimitiveInput`). Camera + controller merged into one class. Movement/rotation/sensitivity speeds are editable properties (`MoveSpeed`, `RotationSpeed`, etc.), not constants. Trauma-based screen shake (`AddTrauma`, baked into `GetViewMatrix`, offset along the camera's own right/up axes) — see DECISIONS.md. Optionally takes a `ViewportAdapter2D` at construction, same as `Camera2D` — `GetWorldToScreen`/`GetScreenToWorld`/`GetScreenToWorldRay` then resolve it automatically instead of requiring an explicit `Viewport` argument. `Reset()` restores the construction-time pose (Position/Target/Up/Fovy/Projection/near/far) and clears zoom/follow/head-bob smoothing state; bound to `R` by default in `UpdateWithInput`. |
| `Collision3D.cs` | Wraps `BoundingSphere`/`BoundingBox`/`Ray`, plus capsule support and plane raycasts. |
| `TrigLut.cs` | Precomputed sin/cos table for per-vertex trig — public, the 3D counterpart to 2D's `UnitCircleLut`. |
| `Trail3D.cs` | 3D counterpart to `Trail2D`. `width` defaults to `Primitive3DBatch.DefaultLineWidth` (sentinel ≤0), `fadeToAlpha` is clamped to [0,1]. |
| `DebugFont5x7.cs` | `DrawString3D`/`MeasureText3D`/`GetBillboardAxes` — billboarded text (cylindrical facing) by default; an overload taking an explicit `right`/`up` basis opts out of billboarding for text that should hold a fixed orientation. Never lit. |

## `samples/MonoPrimitives.Sample/`

Minimal runnable MonoGame game referencing `MonoPrimitives`, plus `MonoGame.Extended` (sample-only). Proves the package works end-to-end; not a real game. Tab toggles between two visual-regression galleries — `Gallery2D.cs` (2D, camera-controlled) and `Gallery3D.cs` (3D, free-fly camera) — both the same row-per-shape-family/cell-per-Fill-Border-Draw-variant/text-caption structure, so a change to either library can be eyeballed the same way.

## `examples/`

Small standalone MonoGame apps (each its own `.csproj` referencing `MonoPrimitives.csproj` directly, not the sample) demonstrating library usage beyond the gallery. `examples/test/` holds one visual test per non-primitive-drawing component (`ViewportTest/` — every `ViewportAdapter2D` mode, 2D and 3D, verified via a `ScreenToWorld(WorldToScreen(x))` round-trip plus a rendered-marker-position check; `NoiseTest/` — `Noise`'s 1D/2D/3D samples, keys 1-3: an Fbm1D terrain silhouette, an Fbm2D heightmap terrain via `FillHeightmap`, and a live `Sample3D` field with Z as time; `ParticleTrailTest/` — several `Trail2D`s with different capacity/thickness/fade styles, dragged behind particles that bounce off the window edges and each other (`Collision2D` detects the overlap, the test itself resolves it — the library stops at detection by design); `TextReadabilityTest/` — every printable glyph `DebugFont5x7` supports plus a pangram at reading size, pannable/zoomable via `Camera2D`; more test folders land here as they're built). `examples/demos/` holds small non-menu/non-pause playable demos built directly on `PrimitiveInput` (not a camera's `UpdateWithInput` convenience) to prove the library at "real game" scale.

## Machinery worth knowing before you touch nearby code

- `ComputeJoint`/`BuildRoundedCornerBoundary` (2D) — shared fillet engine behind every rounded corner. See DECISIONS.md for its per-corner clamp caveat.
- `FillPolygonGradientByNearestVertex` (2D) — colors a rounded boundary's many points by nearest original vertex (a rounded corner's arc has no 1:1 vertex mapping).
- Blend state is `NonPremultiplied` in both batches — see DECISIONS.md.
- `PushQuadLit`/`PushTriangleLit` (3D) — face normal comes from vertex winding order; get it backwards and lighting/culling breaks silently. See DECISIONS.md.
- `UnitCircleLut` (2D) / `TrigLut` (3D) — precomputed trig tables, both public. Use these for new curved geometry, not raw `MathF.Sin`/`Cos`.
