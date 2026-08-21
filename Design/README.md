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
  - **[PrimitiveBatch_Guide.md](../Guide/PrimitiveBatch_Guide.md)** — `PrimitiveBatch`: every 2D shape method grouped by family, the Fill/Border/Draw/Gradient/Shadow conventions, and how shadows/gradients/segment counts actually work.
  - **[Camera2D_Guide.md](../Guide/Camera2D_Guide.md)** — `Camera2D` (pan/rotate/zoom, bounds, follow, shake, input controller) and the 4 `ViewportAdapter2D` types, when to use each, and how 2D/3D scenes share one for letterbox-aware projection.
  - **[Trail2D_Guide.md](../Guide/Trail2D_Guide.md)** — `Trail2D`: the ring-buffer API and its per-segment fade cost/tradeoff.
  - **[PrimitiveInput_Guide.md](../Guide/PrimitiveInput_Guide.md)** — `PrimitiveInput`: keyboard/mouse/gamepad polling, vibration, `GetCharPressed`'s `GameWindow` requirement, and what it deliberately can't/won't do.
  - **[Color_Guide.md](../Guide/Color_Guide.md)** — `Palette` (curated colors, `Cycle`, `GradientPairs`) and `ColorUtil` (hex/HSV conversions, adjustments, blend modes) together, plus what's deliberately not there.
  - **[Noise_Guide.md](../Guide/Noise_Guide.md)** — `Noise`: `Sample1D`/`2D`/`3D`, the `Fbm`/`RidgeNoise`/`Turbulence` families and their shared `Octaves`/`Lacunarity`/`Gain` properties, why `Sample1D` is special, and what's deliberately not there.
  - **[Easing_Guide.md](../Guide/Easing_Guide.md)** — `Easing`: all 11 families (including `Quint`/`Circ`), which curve to reach for, and the two universal properties (`InOut`'s midpoint, `Out = 1 - In(1-t)`) every function satisfies.
  - **[UnitCircleLut_Guide.md](../Guide/UnitCircleLut_Guide.md)** — `UnitCircleLut`: the trig-free `Sample(t01)`/`SampleRadians`/`SampleDegrees` table `PrimitiveBatch` itself is built on.
  - **[TrigLut_Guide.md](../Guide/TrigLut_Guide.md)** — `TrigLut`: the 3D counterpart (`out float` sin/cos pairs), `SinCosStep`'s ring/slice-building shape, and `Sample`/`SampleRadians`/`SampleDegrees` for a continuous angle.
  - **[DebugFont5x7_Guide.md](../Guide/DebugFont5x7_Guide.md)** — `DebugFont5x7`/`FontGlyphs5x7`: `DrawString`/`MeasureText`, the row-span convention every glyph follows, and the two glyph bugs found and fixed this session.
  - **[Collision2D_Guide.md](../Guide/Collision2D_Guide.md)** — `Collision2D`: every overlap/ray check by shape, and which ones need convex input (SAT) vs. work on any simple polygon.
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
