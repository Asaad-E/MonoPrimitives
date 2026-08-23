# Design/ — start here

For a fresh session (AI or human) to pick up this project without reading the whole codebase. Read in order:

1. **[PROJECT.md](PROJECT.md)** — what this is, what it isn't.
2. **[ARCHITECTURE.md](ARCHITECTURE.md)** — file-by-file map + non-obvious machinery.
3. **[CODESTYLE.md](CODESTYLE.md)** — established conventions.
4. **[DECISIONS.md](DECISIONS.md)** — the *why* behind non-obvious choices.
5. **[ROADMAP.md](ROADMAP.md)** — known gaps, deliberate deferrals.

These five are kept short on purpose — current state only, no narrative. Deeper reference (pull into context only when actually working in that area):

- **[../Guide/](../Guide/)** — the current, actively-maintained per-topic guides, one file per topic, being built up incrementally (not all topics are migrated here yet — see below). Start here for anything it already covers.
  - **[RandomUtil_Guide.md](../Guide/RandomUtil_Guide.md)** — `RandomUtil`: every method, what it computes and when to reach for it, the algorithms behind Gaussian/Poisson/Binomial sampling, and single-threaded vs. multi-threaded usage.
  - **[Primitive2DBatch_Guide.md](../Guide/Primitive2DBatch_Guide.md)** — `Primitive2DBatch`: every 2D shape method grouped by family, the Fill/Border/Draw/Gradient/Shadow conventions, and how shadows/gradients/segment counts actually work.
  - **[Primitive3DBatch_Guide.md](../Guide/Primitive3DBatch_Guide.md)** — `Primitive3DBatch`: every 3D shape grouped by family, opt-in flat-shading lighting, the now-complete spline family, and the confirmed raylib `rmodels.h` superset comparison.
  - **[Camera2D_Guide.md](../Guide/Camera2D_Guide.md)** — `Camera2D` (pan/rotate/zoom, bounds, follow, shake, input controller) and the 4 `ViewportAdapter2D` types, when to use each, and how 2D/3D scenes share one for letterbox-aware projection.
  - **[Camera3D_Guide.md](../Guide/Camera3D_Guide.md)** — `Camera3D`: `rcamera.h`-parity basis/movement/rotation, the 5 `CameraMode`s and `UpdateWithInput`'s bindings, and the bounds/follow/zoom/shake surface shared with `Camera2D`.
  - **[Trail2D_Guide.md](../Guide/Trail2D_Guide.md)** — `Trail2D`/`Trail3D`: the ring-buffer API and its per-segment fade cost/tradeoff.
  - **[PrimitiveInput_Guide.md](../Guide/PrimitiveInput_Guide.md)** — `PrimitiveInput`: keyboard/mouse/gamepad polling, vibration, `GetCharPressed`'s `GameWindow` requirement, and what it deliberately can't/won't do.
  - **[Color_Guide.md](../Guide/Color_Guide.md)** — `Palette` (curated colors, `Cycle`, `GradientPairs`) and `ColorUtil` (hex/HSV conversions, adjustments, blend modes) together, plus what's deliberately not there.
  - **[Noise_Guide.md](../Guide/Noise_Guide.md)** — `Noise`: `Sample1D`/`2D`/`3D`, the `Fbm`/`RidgeNoise`/`Turbulence` families and their shared `Octaves`/`Lacunarity`/`Gain` properties, why `Sample1D` is special, and what's deliberately not there.
  - **[Easing_Guide.md](../Guide/Easing_Guide.md)** — `Easing`: all 11 families (including `Quint`/`Circ`), which curve to reach for, and the two universal properties (`InOut`'s midpoint, `Out = 1 - In(1-t)`) every function satisfies.
  - **[UnitCircleLut_Guide.md](../Guide/UnitCircleLut_Guide.md)** — `UnitCircleLut`: the trig-free `Sample(t01)`/`SampleRadians`/`SampleDegrees` table `Primitive2DBatch` itself is built on.
  - **[TrigLut_Guide.md](../Guide/TrigLut_Guide.md)** — `TrigLut`: the 3D counterpart (`out float` sin/cos pairs), `SinCosStep`'s ring/slice-building shape, and `Sample`/`SampleRadians`/`SampleDegrees` for a continuous angle.
  - **[DebugFont5x7_Guide.md](../Guide/DebugFont5x7_Guide.md)** — `DebugFont5x7`/`FontGlyphs5x7` (2D and 3D): `DrawString`/`MeasureText`, 3D's billboarded `DrawString3D`/`GetBillboardAxes`, the row-span convention every glyph follows, and the glyph bugs found and fixed this session.
  - **[Collision2D_Guide.md](../Guide/Collision2D_Guide.md)** — `Collision2D`: every overlap/ray check by shape, and which ones need convex input (SAT) vs. work on any simple polygon.
  - **[Collision3D_Guide.md](../Guide/Collision3D_Guide.md)** — `Collision3D`: sphere/box/capsule/plane/triangle/quad overlap and raycasts, and the `RayCollision3D` result struct every raycast returns.
  - **[Vector2Extensions_Guide.md](../Guide/Vector2Extensions_Guide.md)** — `Vector2Extensions`: `Angle`/`AngleTo`/`AngleToSigned`/`Rotated`/`Approach`/`ClampMagnitude`/etc., and why `Rotated` isn't named `Rotate`.
  - **[FrameLimiter_Guide.md](../Guide/FrameLimiter_Guide.md)** — `FrameLimiter`: sleep+spin frame pacing, why it disables `IsFixedTimeStep`/vsync, and the measured Sleep-jitter limitation.
  - **[FastTexture_Guide.md](../Guide/FastTexture_Guide.md)** — `FastTexture`: raw-GL texture upload with a safe `SetData` fallback, mip/RenderTarget2D caveats, and the texture-slot cache gotcha.
- **[2D/Primitives2D_Audit_Report.md](2D/Primitives2D_Audit_Report.md)**, **[2D/Overnight_Changes_2026-08-19.md](2D/Overnight_Changes_2026-08-19.md)**, **[3D/Primitive3D_Changes.md](3D/Primitive3D_Changes.md)** — historical session logs. Archaeology only ("why does this bug fix exist") — the five docs above already capture current state. Long; don't load by default.

## Repo layout

```
MonogameLibs/
├── src/
│   ├── Core/        — shared (namespace MonoPrimitives)
│   ├── 2D/          — namespace MonoPrimitives.Primitives2D
│   ├── 3D/          — namespace MonoPrimitives.Primitives3D
│   └── MonoPrimitives.csproj   — one project → one MonoPrimitives.dll
├── samples/MonoPrimitives.Sample/
├── Design/          — you are here
├── Guide/           — actively-maintained per-topic user guides (see "Deeper reference" above)
└── MonoPrimitives.slnx
```

`dotnet build MonoPrimitives.slnx` builds everything.

## Keeping this useful

Update DECISIONS.md/ARCHITECTURE.md *when a change happens*, not "eventually" — stale docs cost more tokens to work around than short docs cost to maintain. Prefer editing an existing line over appending a new one; these files should stay roughly the same size as the project grows, not accumulate.
