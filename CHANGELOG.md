# Changelog

All notable changes to this project are documented in this file.

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning follows [SemVer](https://semver.org/), with the pre-1.0 caveat that the API can still change between minor versions — see [ROADMAP.md](Design/ROADMAP.md).

## [0.8.4] - 2026-08-26

### Added
- `WindowUtil` — minimize/maximize/restore, window opacity, window icon, multi-monitor enumeration, clipboard text (all resolved directly from SDL2, DesktopGL only), plus `DisableCursor`/`EnableCursor`/`GetCursorDelta` for FPS-style mouse-look capture (works on every backend).
- `MathUtil` — `Remap`, `DeltaAngle`, `LerpAngle`, `PingPong`: scalar helpers confirmed missing from `MathHelper`.
- `Primitive3DBatch.FillBillboard`/`BorderBillboard`/`DrawBillboard` — a camera-facing quad built on the existing `GetBillboardAxes`.
- `TextureUtil` — procedural texture generation (`CreateSolid`/`CreateGradientLinear`/`CreateGradientRadial`/`CreateCheckerboard`/`CreateFromNoise`) plus `Crop`/`FlipHorizontal`/`FlipVertical`/`Tint`/`Resize`/`Combine`/`ToTexture2D` transforms.

## [0.8.3] - 2026-08-25

### Added
- SourceLink + a `.snupkg` symbol package, so "Go to Definition" into the published package resolves to this repo's real source instead of decompiling the DLL.

### Changed
- `FrameLimiter.EndFrame()` no longer recomputes `1000/TargetFps` every call — `TargetFps`'s setter now caches it, since `TargetFps` only actually changes when a caller sets it.

## [0.8.2] - 2026-08-25

### Added
- `FrameLimiter.MaxFrameTime` (constructor param + live property, `0` = disabled) and `FrameTime` — `BeginFrame()` now returns real seconds since the previous call, clamped to `MaxFrameTime`.
- `FrameLimiter` now composes an internal `FpsCounter` (constructor's `fpsSampleCount`, default 60): `AverageFps`/`CurrentFps`/`AverageFrameTimeMs`/`CurrentFrameTimeMs`/`FpsSampleCount`, fed the raw unclamped frame time every `BeginFrame()` — pacing your loop with `FrameLimiter` now gets you FPS readouts for free.
- `DebugTimer` (struct, `IDisposable`) — times a `using` block and prints `[label] X.XX ms` to `Console` on dispose, with an optional divider line (`separator: true`) for marking a new group.
- `Vector2Extensions`/`Vector3Extensions`: `Dot` (fluent wrapper — MonoGame only exposes it as a static call), `Project` (parallel-component projection, complementing the existing `Slide`), and 2D-only `Cross` (scalar cross product).

### Changed
- `SmoothDamp` moved from being duplicated on `Camera2D`/`Camera3D` to a single `Vector2Extensions.SmoothDamp` (`float`/`Vector2`) plus `Vector3Extensions.SmoothDamp` (`Vector3`) — `Camera2D`/`Camera3D` now call it as an extension instead of owning their own copy.

## [0.8.0] - 2026-08-25

### Added
- `EasingType` enum + `Easing.Evaluate(type, t)`, for picking a curve as data instead of a function reference.
- `ColorUtil.FromTemperature(kelvin)` — blackbody Kelvin→Color approximation for fire/plasma/heatmap coloring.
- `RectangleF.Lerp`, `Vector2Extensions.Slide`/`Vector3Extensions.Slide` (tangential-only projection against a normal, alongside the existing `Reflect`).
- `Camera2D`/`Camera3D`: `IsVisible` frustum/rect culling, box-deadzone `FollowTarget` overload, `FitBounds`.
- `RandomUtil.NextItem<T>`, `NextGaussianVector2`, `NextGaussianVector3`.
- `Vector3Extensions.AngleTo`/`AngleToSigned(other, axis)`/`Rotated(axis, radians)`.
- `Primitive2DBatch`/`Primitive3DBatch.Effect` and `FrameLimiter.Elapsed` read-only escape hatches.

### Changed
- Cut XML doc-comment content across `src/` down to the IDE-tooltip-only standard in `CODESTYLE.md`.

## [0.7.5] - 2026-08-24

### Added
- `ObjectPool<T>`, `RingBuffer<T>`, `Cooldown` (struct), `Vector3Extensions` (initial `Approach`/`ClampMagnitude`/`Slide`-adjacent surface).
- `PrimitiveInput` raw keyboard/mouse/gamepad state accessors (`Current`/`PreviousKeyboardState`, etc.).
- `FpsCounter.AverageFrameTimeMs`/`CurrentFrameTimeMs`.

## [0.7.0] - 2026-08-24

### Added
- CI workflow: build + run the full test suite on every push/PR (`xvfb` for a real `GraphicsDevice` on Ubuntu).

### Changed
- `dotnet new` template rewritten to file-scoped namespaces, inline `RenderContext`, `PrimitiveInput(Window)`.

### Fixed
- `Primitive2DBatch` was missing the disposed-guard `Primitive3DBatch` already had.
- All 329 pre-existing XML doc warnings (broken `cref`s, missing `<param>` tags) — 0 warnings from here on.

## [0.6.0] - 2026-08-24

### Added
- `RectangleF` — float-precision counterpart to MonoGame's integer-only `Rectangle`.
- `FpsCounter`, `ScreenshotUtil`.
- `pixelPerfect` option on `BoxingViewportAdapter2D` for crisp pixel-art scaling.
- Word-wrap on `DebugFont5x7` (`FontGlyphs5x7.WrapText` + a `maxWidth` parameter on `DrawString`/`DrawString3D`).
- Rotation support on `FillRectangle`'s 4-corner-color overload.

### Changed
- `Nullable` and `GenerateDocumentationFile` enabled project-wide.

### Fixed
- `PrimitiveInput`'s analog deadzone now rescales past the cutoff instead of just clamping below it.

## [0.5.7] - 2026-08-24

### Changed
- `dotnet new` template renamed from `MonoPrimitives.Template` to `PrimitiveBase`.

### Fixed
- `ClearLetterboxed` left the viewport narrowed on return, double-applying the offset on the caller's next `Begin()`.

## [0.5.6] - 2026-08-24

### Added
- `Primitive2DBatch.ClearLetterboxed(adapter, barColor?, backgroundColor?)` — letterbox bars plus a separate "inside" clear color in one call.

## [0.5.5] - 2026-08-24

### Changed
- **Breaking:** retargeted from `net10.0` to `net8.0` to match where most MonoGame consumers actually are.

## [0.5.0] - 2026-08-24

### Added
- `FillTriangle`/`BorderTriangle`/`DrawTriangle(center, radius, rotation)` equilateral overloads.

### Changed
- **Breaking:** every `Draw<Shape>`'s single-color and `fillColor, borderColor` overload pairs merged into one signature (`Color? borderColor = null`).
- Minor batcher perf cleanup (redundant `sqrt`/`Atan2` removed from rounded-corner joints).

### Fixed
- `InsetConvexPolygon`/`OutsetConvexPolygon` reflex-vertex direction bug.
- `DrawLineStrip3D`/`Trail3D.Draw` left a real gap at shared bends — now one properly mitered joined strip.

## [0.4.0] - 2026-08-23

### Added
- `Vector2Extensions` (`Angle`/`AngleTo`/`AngleToSigned`/`Rotated`/`Perpendicular*`/`DirectionTo`/`SafeNormalize`/`Approach`/`ClampMagnitude`) and `GameTimeExtensions.GetElapsedTimeSeconds()`.
- `FastTexture` — raw `glTexSubImage2D` upload, ~2.5–2.7x faster than `SetData` per real frame, with a safe fallback.
- `FrameLimiter` — sleep+spin frame pacing more precise than `IsFixedTimeStep` alone.
- `PrimitiveInput.GetWASD`/`GetArrowKeys`/`GetInputDirection`.
- `dotnet new` scaffolding template (`PrimitiveBase`).
- Round-cap shaft and rounded-head-corner options on 2D `DrawArrow`.

### Changed
- **Breaking:** `PrimitiveBatch` renamed to `Primitive2DBatch`.
- **Breaking:** `CircleSector`/`Ring` fill/border methods renamed to the standard `Fill`/`Border`, with the missing combined `Draw` overloads added.
- `Sphere`/`Circle3D`'s simple overloads now use automatic LOD instead of a fixed segment count.

### Fixed
- `Camera2D.SmoothZoom`'s settled-epsilon is now relative to the target, fixing a stall on small zoom ranges.

## [0.3.0] - 2026-08-21

### Added
- 2D `Capsule` shape family (`Fill`/`Border`/`Draw`/`Gradient`, endpoint and center+length+rotation overloads).
- `Collision2D`: full Capsule-vs-Circle/Rectangle/Triangle/Polygon/Capsule coverage, plus general polygon and mixed-shape checks.
- `Collision3D`: `GetRayCollisionTriangle`/`Quad` (Möller–Trumbore, raylib parity), `CheckCollisionCapsuleBox`.
- Shadow variants for Triangle/Ellipse/Poly/Polygon/Capsule, the full Poly rounded family, `DrawPolygonGradientRounded`, `DrawPolyGradientRounded`, `DrawCircleSectorGradient`/`DrawRingGradient`.
- `ColorUtil.Invert`/`Contrast` and `Multiply`/`Screen`/`Overlay`/`Additive` blend modes.
- 3D spline family completed to match 2D (`DrawSplineBasis3D`, `DrawSplineBezierQuadratic3D`, all `GetSplinePoint*3D`).
- `Easing.QuintIn/Out/InOut` and `CircIn/Out/InOut`, completing the canonical easing set.
- `Noise.RidgeNoise2D/3D` and `Turbulence2D/3D`; `octaves`/`lacunarity`/`gain` moved to instance properties.
- `RandomUtil.NextWeightedIndex`, `UnderlyingRandom` escape hatch.
- `PrimitiveInput.SetCursor`, `CapsLock`/`NumLock`, `SetVibration`, trigger deadzone helpers, `IsAnyKeyPressed`/`IsAnyMouseButtonPressed`/`IsAnyButtonPressed`, `GetCharPressed()` via a new `PrimitiveInput(GameWindow)` constructor.
- `UnitCircleLut.SampleRadians`/`SampleDegrees`; `TrigLut.Sample`/`SampleRadians`/`SampleDegrees` for parity.

### Changed
- **Breaking:** `Rectangle`'s `*RoundedGradient`/`*ChamferGradient` renamed to `*GradientRounded`/`*GradientChamfer`; `GetSplinePointBezierQuad` renamed to `GetSplinePointBezierQuadratic`.

### Removed
- `GridRenderer2D` — a ready-made texture-backed system, out of the building-blocks scope.

### Fixed
- `DrawPolygonGradient` now sources vertex colors from the original points, not the inset boundary.
- `DrawRingShadow`'s partial-wedge case.
- `Camera2D`'s mouse-drag pan now compensates for `Rotation`; added `Camera2D.Reset()`; `ViewportAdapter2D.BoundingRectangle` rounding normalized.
- `Camera2D.Offset` now live-tracks its `ViewportAdapter`'s virtual center instead of snapshotting it once.
- `FontGlyphs5x7`: `a`/`e`/`g`/`u` (and accented `á`/`é`) x-height corrected; lowercase `h`'s crossbar widened to connect to the right leg.
- `UnitCircleLut.Sample`/`TrigLut.SinCosStep` now floor before casting, fixing negative/out-of-range wraparound.
- `Camera3D.Reset()` now clears `Trauma`, matching `Camera2D.Reset()`.

## [0.2.5] - 2026-08-21

### Added
- `RandomUtil` — seedable float-based distribution sampling (Gaussian, Poisson, Binomial, uniform disc/sphere, and more).
- `DrawSplineBasis`, `DrawSplineBezierQuadratic` (2D).

## [0.2.0] - 2026-08-21

### Added
- Configurable line thickness for `DrawGrid`/`DrawAxis`.

### Fixed
- Camera/viewport double-offset bug; `Asteroids3D`'s inverted yaw axis.
- `README.md` now bundled into the published NuGet package.

## [0.1.0] - 2026-08-21

Initial functional release. Core 2D/3D primitive drawing, camera, input, and math foundation:

### Added
- `Primitive2DBatch`/`Primitive3DBatch` — Fill/Border/Draw shape drawing, unified under the `MonoPrimitives`/`MonoPrimitives.Primitives2D`/`MonoPrimitives.Primitives3D` namespaces.
- `Camera2D`/`Camera3D` with `ViewportAdapter2D`-aware construction, split `Update`/`UpdateWithInput` tiers, and trauma-based screen shake.
- `PrimitiveInput`, `Easing` (full In/Out/InOut coverage), `Noise`, `Palette`/`ColorUtil`, `Collision2D`/`Collision3D`, `Trail2D`/`Trail3D`, `UnitCircleLut`, `DebugFont5x7`.
- Drop-shadow shape primitives (`FillCircleShadow`, `FillRectangleShadow`/`FillRectangleChamferShadow`).
- `tests/MonoPrimitives.Tests`, the headless-`Game` regression suite.
- `samples/MonoPrimitives.Sample` (2D/3D shape gallery), 5 `examples/test/` projects, and 7 `examples/demos/` games/simulations (Breakout, Tetris, Asteroids2D/3D, Platformer2D, Snake, Boids).

### Changed
- `CameraInput`/`CameraInput2D` structs removed — `UpdateWithInput` reads a caller-owned `PrimitiveInput` directly instead.

### Fixed
- `Platformer2D`'s grounded-state flicker, `Asteroids3D`'s yaw/pitch drift, missing DPI-awareness manifests across `examples/`.
- Viewport adapter offset bugs (stale read, `Apply()`+`GetScaleMatrix()` double-offset).
- `Collision3D`'s ray-vs-box normal at large scale; `Noise.Sample1D`'s gradient degeneracy; `PrimitiveInput`'s drag-delta-on-release and double-click distance check.

[Unreleased]: https://github.com/Asaad-E/MonoPrimitives/compare/v0.8.3...HEAD
