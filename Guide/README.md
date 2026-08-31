# MonoPrimitives — Guide

Reference documentation for every public class in **MonoPrimitives**, a NuGet package that adds immediate-mode 2D/3D primitive drawing to MonoGame, plus the small set of helpers a fast prototype usually needs — camera, input, easing, color, noise, and collision — so you don't need several separate libraries for those.

Each guide below is self-contained: what the class is for, a runnable quick-start snippet, a full method reference, and the non-obvious gotchas worth knowing before you hit them yourself. You don't need to read them in order — jump straight to whichever one covers what you're building.

## Before you start

These guides assume you already have a MonoGame project with a working `Game` class (`Initialize`/`LoadContent`/`Update`/`Draw`) — they teach MonoPrimitives, not MonoGame itself. If you're setting up a project from scratch:

```bash
dotnet add package MonoPrimitives
```

or scaffold a full starter (letterboxed virtual resolution, MSAA, input, all wired together) with the included template:

```bash
dotnet new install ./templates/PrimitiveBase
dotnet new primitivebase -n YourGame
```

See the root [`README.md`](../README.md) for the two-minute quick-start snippet (2D and 3D) if you just want to see the shape of the API before diving into a specific guide.

## If you're brand new here

Almost everything else in this library exists to support drawing, so start there:

- Making a 2D game or prototype → [`Primitive2DBatch`](Primitive2DBatch_Guide.md)
- Making a 3D game or prototype → [`Primitive3DBatch`](Primitive3DBatch_Guide.md)

Then, whichever you picked, add a camera ([`Camera2D`](Camera2D_Guide.md) / [`Camera3D`](Camera3D_Guide.md)) and input ([`PrimitiveInput`](PrimitiveInput_Guide.md)) — those three cover the large majority of what a small game or simulation actually needs. Everything below is there for when you need it.

## Every guide, by topic

**Drawing**
- [`Primitive2DBatch`](Primitive2DBatch_Guide.md) — every 2D shape (rectangles, circles, ellipses, capsules, polygons, sectors/rings, splines), gradients, shadows, rounded/chamfered corners.
- [`Primitive3DBatch`](Primitive3DBatch_Guide.md) — every 3D shape (cubes, spheres, cylinders, capsules, torus, planes, heightmaps), flat-shading lighting, splines.
- [`DebugFont5x7`](DebugFont5x7_Guide.md) — a built-in bitmap debug font, 2D and 3D (billboarded), including word-wrap.

**Camera & viewport**
- [`Camera2D & Viewport`](Camera2D_Guide.md) — pan/rotate/zoom, bounds, follow, shake, and the `ViewportAdapter2D` family (letterboxing/scaling for resolution independence — read this even if you're only using 3D, since 3D shares the same adapters).
- [`Camera3D`](Camera3D_Guide.md) — the 3D counterpart: 5 behavior modes, free-fly/orbit/first-/third-person controllers.

**Input**
- [`PrimitiveInput`](PrimitiveInput_Guide.md) — keyboard/mouse/gamepad polling, vibration, typed text, and what's deliberately not here (no action-mapping layer).

**Collision**
- [`Collision2D`](Collision2D_Guide.md) — every 2D overlap/ray check by shape, and which ones need convex input.
- [`PolygonUtil`](PolygonUtil_Guide.md) — `IsConvex` (does my polygon qualify for SAT?) and `Triangulate` (ear clipping, for your own mesh/collision/nav data).
- [`Collision3D`](Collision3D_Guide.md) — sphere/box/capsule/plane/triangle/quad overlap and raycasts.

**Math & utilities**
- [`MathUtil`](MathUtil_Guide.md) — `Remap`/`DeltaAngle`/`LerpAngle`/`PingPong`, the scalar helpers `MathHelper` doesn't have.
- [`Easing`](Easing_Guide.md) — 31 tweening curves for one-shot animations with a known duration.
- [`Noise`](Noise_Guide.md) — seedable Perlin noise, fBm, ridge, and turbulence, for terrain/organic variation.
- [`RandomUtil`](RandomUtil_Guide.md) — seedable distribution sampling (Gaussian, Poisson, Binomial, uniform disc/sphere, weighted picks) for simulations.
- [`Color`](Color_Guide.md) — a curated color palette plus hex/HSV conversion, adjustment, and blend modes.
- [`Trail`](Trail2D_Guide.md) — a fading position-history trail, 2D and 3D.
- [`VectorExtensions`](Vector2Extensions_Guide.md) — angle/rotation/approach/clamp helpers on MonoGame's own `Vector2`, plus `Vector3Extensions` (its 3D counterpart) in the same guide.
- [`RectangleF`](RectangleF_Guide.md) — a float-precision counterpart to MonoGame's integer-only `Rectangle`.
- [`UnitCircleLut`](UnitCircleLut_Guide.md) / [`TrigLut`](TrigLut_Guide.md) — the trig-free lookup tables the shape batches are themselves built on, for your own curved geometry.
- [`RingBuffer`](RingBuffer_Guide.md) — a generic fixed-capacity ring buffer, the same building block `Trail2D`/`Trail3D`/`FpsCounter` each already use privately, exposed for your own history/log/sample-window need.
- [`ObjectPool`](ObjectPool_Guide.md) — a generic object pool, for anything spawned/discarded often enough (bullets, particles, simulation agents) to want reuse over reallocation.

**App helpers**
- [`FrameLimiter`](FrameLimiter_Guide.md) — sleep+spin frame pacing, more precise than `IsFixedTimeStep` alone.
- [`FastTexture`](FastTexture_Guide.md) — raw-GL texture upload, 2.5-2.7x faster than `SetData` for frequent updates.
- [`FpsCounter`](FpsCounter_Guide.md) — rolling-average FPS measurement.
- [`ScreenshotUtil`](ScreenshotUtil_Guide.md) — one-call back-buffer capture to `.png`/`.jpg`.
- [`TextureUtil`](TextureUtil_Guide.md) — procedural texture generation (solid/gradient/checkerboard/from `Noise`) plus resize/crop/flip/tint/combine transforms.
- [`Cooldown`](Cooldown_Guide.md) — a simple countdown struct for attack cooldowns, spawn timers, input debouncing.
- [`WindowUtil`](WindowUtil_Guide.md) — minimize/maximize/restore, opacity, window icon, multi-monitor info, clipboard text, and captured-cursor mouse-look.

## Something not working the way a guide says it should?

Each guide's "See also" section links to [`Design/DECISIONS.md`](../Design/DECISIONS.md) where relevant — that's where the *why* behind a non-obvious behavior lives (a clamp, a naming choice, a bug that was found and fixed). [`Design/ROADMAP.md`](../Design/ROADMAP.md) lists known gaps and things deliberately left out of scope, in case what you're looking for was considered and skipped on purpose rather than missed.
