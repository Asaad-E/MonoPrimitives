# MonoPrimitives — project brief

## What this is

Fast-prototype helpers for MonoGame in 2D and 3D — primitive drawing plus camera, input, easing, color, and noise, so you don't need several external libraries for those. **One package, one assembly** (`MonoPrimitives.dll`, raylib/MonoGame.Extended-style). Internally organized into three namespaces under one root (`MonoPrimitives`/`MonoPrimitives.Primitives2D`/`MonoPrimitives.Primitives3D`, matching `Core/`/`2D/`/`3D/` source folders) so nothing is duplicated, but there's only ever one thing to install.

Target use: simulations (boids, cellular automata, predator-prey, pandemic models, terrain) and small retro-style game demos. Not a game engine, not aimed at a finished commercial game, not a charting library (use ScottPlot or similar for real data plots — a `Plot2D` was built and removed here for exactly that reason, see DECISIONS.md).

Visual/use-case inspiration: Pezzza's Work, Sebastian Lague, Primer, 3Blue1Brown. Never named in code comments or docs — see CODESTYLE.md.

## What's where

Shared (`Core/`, namespace `MonoPrimitives`):

| | |
|---|---|
| Input | `PrimitiveInput` |
| Scalar math | `MathUtil` |
| Easing | `Easing` |
| Color | `Palette`, `ColorUtil` |
| Noise | `Noise` |
| Random | `RandomUtil` (+ static `RandomUtil.Shared`) |
| Bitmap font | `FontGlyphs5x7` (2D/3D each draw it separately, see ARCHITECTURE.md) |
| Vector2 helpers | `Vector2Extensions` |
| Float rectangle | `RectangleF` |
| Frame pacing | `FrameLimiter` |
| Fast texture upload | `FastTexture` |
| Texture generation & transforms | `TextureUtil` |
| FPS measurement | `FpsCounter` |
| Screenshot capture | `ScreenshotUtil` |
| Object pooling | `ObjectPool<T>` |
| Fixed-capacity history | `RingBuffer<T>` |
| Countdown timer | `Cooldown` (struct) |
| Debug timing | `DebugTimer` (struct) |
| Window/monitor/clipboard/cursor-capture | `WindowUtil` |

Per-namespace (not shared — genuinely different code):

| | 2D (`MonoPrimitives.Primitives2D`) | 3D (`MonoPrimitives.Primitives3D`) |
|---|---|---|
| Shape drawing | `Primitive2DBatch` | `Primitive3DBatch` |
| Camera + viewport | `Camera2D`, `ViewportAdapter2D` family | `Camera3D` (reuses `ViewportAdapter2D`) |
| Collision & raycasts | `Collision2D` | `Collision3D` |
| Trail | `Trail2D` | `Trail3D` |
| Debug text rendering | `DebugFont5x7` | `DebugFont5x7` (billboarded) |
| Fast-trig LUT | `UnitCircleLut` | `TrigLut` |
| Vector helpers | (`Vector2Extensions` is shared, see above) | `Vector3Extensions` |

See DECISIONS.md for the Core-vs-duplicated rule.

## What this is *not*

- **No physics resolution** — `Collision2D`/`Collision3D` detect, never resolve.
- **No texture/model loading, no window creation/ownership** — MonoGame's own `Game`/content pipeline handles that. `WindowUtil` is the one exception to "no window management," and a narrow one: it only *operates on* a `GameWindow`/`Game` the caller already owns (minimize/maximize, opacity, icon, monitor info, clipboard) — it never creates, owns, or replaces one, same boundary `ScreenshotUtil` draws around `GraphicsDevice`.

## Design philosophy

Immediate-mode (no retained scene graph), `Fill`/`Border`/`Draw` per shape (border grows inward), rotation in radians, no per-frame heap allocation on hot paths, and — since this codebase is built mostly by an AI — a house rule of **rendering genuinely new geometry before trusting it**, not just reasoning about it. See CODESTYLE.md.
