# MonoPrimitives

[![NuGet](https://img.shields.io/nuget/v/MonoPrimitives.svg)](https://www.nuget.org/packages/MonoPrimitives)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Immediate-mode 2D and 3D primitive drawing for MonoGame, plus the small set of helpers a fast prototype usually needs — camera, input, easing, color, noise, and collision/raycast tests — so you don't have to pull in a handful of separate external libraries for those.

Built for prototypes: simulations (boids, cellular automata, predator-prey, pandemic models, terrain), generative art, and small retro-style game demos. Not a game engine, and not aimed at shipping a full commercial game.

## Install

```bash
dotnet add package MonoPrimitives
```

One NuGet package, one assembly, one DLL — 2D and 3D both come with it, no separate sub-packages to resolve. Internally it's still organized into three namespaces so nothing is duplicated between them:

| Namespace | Source folder | What's there |
|---|---|---|
| `MonoPrimitives` | `src/Core/` | Shared: `PrimitiveInput`, `Easing`, `Palette`/`ColorUtil`, `Noise`, `RandomUtil`, `FontGlyphs5x7` |
| `MonoPrimitives.Primitives2D` | `src/2D/` | `PrimitiveBatch`, `Camera2D` + `ViewportAdapter2D`, `Collision2D`, `Trail2D`, `UnitCircleLut` |
| `MonoPrimitives.Primitives3D` | `src/3D/` | `Primitive3DBatch`, `Camera3D`, `Collision3D`, `Trail3D`, `TrigLut` |

## Quick start

```csharp
using MonoPrimitives.Primitives2D;

private PrimitiveBatch _batch;

protected override void LoadContent()
{
    _batch = new PrimitiveBatch(GraphicsDevice);
}

protected override void Draw(GameTime gameTime)
{
    _batch.Begin();
    _batch.FillCircle(new Vector2(400, 300), 50, Color.Red);
    _batch.DrawRectangle(100, 100, 200, 80, Color.White, Color.Black, thickness: 4);
    _batch.End();
    base.Draw(gameTime);
}
```

```csharp
using MonoPrimitives.Primitives3D;

private Primitive3DBatch _batch;
private Camera3D _camera;

protected override void LoadContent()
{
    _batch = new Primitive3DBatch(GraphicsDevice);
    _camera = new Camera3D(position: new Vector3(6, 6, 6), target: Vector3.Zero, up: Vector3.Up, fovy: 50f);
}

protected override void Draw(GameTime gameTime)
{
    _batch.Begin(_camera);
    _batch.FillSphere(Vector3.Zero, 1f, Color.Red);
    _batch.End();
    base.Draw(gameTime);
}
```

Every shape follows the same `Fill<Shape>` (solid) / `Border<Shape>` (outline, grows inward) / `Draw<Shape>` (both) pattern, in both 2D and 3D.

## Documentation

Each class has its own guide — start with whichever one covers what you're touching:

**Drawing**
- [`PrimitiveBatch_Guide.md`](Guide/PrimitiveBatch_Guide.md) — every 2D shape (rectangles, circles, ellipses, capsules, polygons, sectors/rings, splines), gradients, shadows, rounded/chamfered corners.
- [`Primitive3DBatch_Guide.md`](Guide/Primitive3DBatch_Guide.md) — every 3D shape (cubes, spheres, cylinders, capsules, torus, planes, heightmaps), flat-shading lighting, splines.
- [`DebugFont5x7_Guide.md`](Guide/DebugFont5x7_Guide.md) — a built-in bitmap debug font, 2D and 3D (billboarded).

**Camera & viewport**
- [`Camera2D_Guide.md`](Guide/Camera2D_Guide.md) — pan/rotate/zoom, bounds, follow, shake, and the `ViewportAdapter2D` family (letterboxing/scaling for resolution independence).
- [`Camera3D_Guide.md`](Guide/Camera3D_Guide.md) — the 3D counterpart: 5 behaviour modes, free-fly/orbit/first-/third-person controllers.

**Collision**
- [`Collision2D_Guide.md`](Guide/Collision2D_Guide.md) — every 2D overlap/ray check by shape.
- [`Collision3D_Guide.md`](Guide/Collision3D_Guide.md) — sphere/box/capsule/plane/triangle/quad overlap and raycasts.

**Input**
- [`PrimitiveInput_Guide.md`](Guide/PrimitiveInput_Guide.md) — keyboard/mouse/gamepad polling, vibration, typed text.

**Math & utilities**
- [`Easing_Guide.md`](Guide/Easing_Guide.md) — 31 tweening curves for one-shot animations with a known duration.
- [`Noise_Guide.md`](Guide/Noise_Guide.md) — seedable Perlin noise, fBm, ridge, and turbulence.
- [`RandomUtil_Guide.md`](Guide/RandomUtil_Guide.md) — seedable distribution sampling (Gaussian, Poisson, Binomial, uniform disc/sphere, weighted picks).
- [`Color_Guide.md`](Guide/Color_Guide.md) — a curated color palette plus hex/HSV conversion and adjustment.
- [`Trail2D_Guide.md`](Guide/Trail2D_Guide.md) — a fading position-history trail, 2D and 3D.
- [`UnitCircleLut_Guide.md`](Guide/UnitCircleLut_Guide.md) / [`TrigLut_Guide.md`](Guide/TrigLut_Guide.md) — the trig-free lookup tables the shape batches are themselves built on, for your own curved geometry.

For the project's own internals (architecture map, conventions, the reasoning behind non-obvious choices), see [`Design/README.md`](Design/README.md).

## Examples

- [`samples/MonoPrimitives.Sample`](samples/MonoPrimitives.Sample) — a visual-regression gallery of every 2D and 3D shape, camera-controlled.
- [`examples/test/`](examples/test) — one focused demo per non-drawing component: collision, input, viewport adapters, noise, particle trails, text.
- [`examples/demos/`](examples/demos) — small complete games built on the library (Breakout, Tetris, Asteroids in 2D and 3D, a platformer, Snake, a boids simulation).

## What this isn't

- **No physics resolution** — collision checks detect overlaps, they never resolve them.
- **No texture/model loading, no window management** — that's MonoGame's own `Game`/content pipeline.
- **No scene graph** — every draw call is immediate; nothing is retained between frames.

## Inspiration

This library borrows ideas from several places rather than inventing its own conventions from scratch:

- **[raylib](https://www.raylib.com/)** — the biggest influence on the API's shape: `Fill`/`Border`/`Draw` per shape mirrors raylib's own function-per-shape simplicity, `Camera3D`'s movement/rotation math is a direct port of `rcamera.h`, and `Collision3D`'s `GetRayCollision*` naming and result struct follow raylib's own collision module.
- **[raylib-cs](https://github.com/ChrisDill/Raylib-cs)** — a reference for translating raylib's C-shaped API (output parameters, `Ex`/`V`-suffixed overload families) into idiomatic C#: overloads instead of suffixes, MonoGame's own `Vector2`/`Vector3`/`Color` types instead of reinventing them.
- **[MonoGame.Extended](https://github.com/craftworkgames/MonoGame.Extended)** — `Camera2D`/`ViewportAdapter2D`'s design (letterbox/scaling viewport adapters composed with a camera) follows its `OrthographicCamera`/`ViewportAdapter` shape.
- **[Godot](https://godotengine.org/)** — several individual methods are confirmed against Godot's own equivalents where raylib has no answer: `PrimitiveInput.GetAxis`'s tie-to-zero behavior matches `Input.get_axis`, `DebugFont5x7`'s cylindrical text billboarding matches `Label3D`'s default billboard mode, `RandomUtil.NextWeightedIndex` matches `RandomNumberGenerator.rand_weighted_pick`.
- **[Processing](https://processing.org/) / [p5.js](https://p5js.org/)** — the underlying philosophy: draw a shape with one call, no setup ceremony, fast enough to iterate on an idea in a sketch rather than a project. `Noise`'s API shape (seedable, sample-anywhere) is the same idea as Processing's own `noise()`.

None of the above are dependencies — MonoPrimitives only depends on MonoGame itself. They're referenced here as the design lineage, not as libraries this one wraps or requires.

## Status

Published on NuGet, pre-1.0 — the API can still change between minor versions as gaps get closed. See [`Design/ROADMAP.md`](Design/ROADMAP.md) for known gaps and deliberate deferrals.

## License

MIT — see [`LICENSE`](LICENSE).
