# Camera2D & ViewportAdapter2D — Guide

`Camera2D` (namespace `MonoPrimitives.Primitives2D`, file [`src/2D/Camera2D.cs`](../src/2D/Camera2D.cs)) is a 2D camera — pan/rotate/zoom, bounds clamping, smooth-follow, smooth-zoom, trauma-based screen shake, and an optional WASD/mouse-drag/wheel controller — that hands `Primitive2DBatch.Begin` a single transform matrix. `ViewportAdapter2D` (and its four concrete adapters, same file's siblings) maps a fixed "virtual" resolution onto the actual window, so game logic and drawing work in one resolution regardless of what size the window actually is. The two are covered together here because a `Camera2D` is normally constructed *with* an adapter and leans on it for every screen↔world conversion.

## Quick start

```csharp
using MonoPrimitives.Primitives2D;

private Primitive2DBatch _batch;
private Camera2D _camera;

protected override void LoadContent()
{
    _batch = new Primitive2DBatch(GraphicsDevice);
    _camera = new Camera2D(target: Vector2.Zero, offset: new Vector2(400, 300)); // no adapter
}

protected override void Update(GameTime gameTime)
{
    _camera.Update(gameTime); // or UpdateWithInput for a built-in WASD/drag/wheel controller
}

protected override void Draw(GameTime gameTime)
{
    _batch.Begin(_camera.GetTransformMatrix());
    _batch.FillCircle(Vector2.Zero, 20f, Color.Red); // world-space drawing
    _batch.End();
    base.Draw(gameTime);
}
```

`Target` is the world point the camera looks at; `Offset` is the screen point `Target` is drawn at (typically the viewport center). `GetTransformMatrix()` is the one thing `Primitive2DBatch.Begin` needs — pass it as `transformMatrix` and every subsequent draw call in that batch is automatically panned/rotated/zoomed.

## Core state

| Member | What it does |
|---|---|
| `Target` (`Vector2`) | World point the camera centers on. |
| `Offset` (`Vector2`) | Screen-space point `Target` is drawn at. See "Offset and ViewportAdapter" below — behaves differently depending on whether this camera has an adapter. |
| `Rotation` (`float`, radians) | Camera rotation. |
| `Zoom` (`float`, default `1`) | Scale factor: `>1` zoomed in, `<1` zoomed out. |
| `GetTransformMatrix()` | Builds the matrix for `Primitive2DBatch.Begin`: translate by `-Target`, rotate, scale by `Zoom`, translate to `Offset` (screen shake folded in — see below), then — if constructed with a `ViewportAdapter` — the adapter's own `GetScaleMatrix()` on top. Composition order matches MonoGame.Extended's `OrthographicCamera.GetViewMatrix()`; don't multiply by the adapter's matrix again yourself. |
| `ScreenToWorld(Vector2)` / `WorldToScreen(Vector2)` | Convert between screen pixels and world space, inverse of each other. With an adapter, `screenPosition`/the result are real window pixels — the adapter's virtual↔window mapping is applied automatically. Without one, screen space is assumed to already share the same pixel space as `Offset` (raw device/mouse coordinates). |
| `GetVisibleWorldBounds(GraphicsDevice? device = null)` | The world-space rectangle currently visible on screen, as `(Vector2 Min, Vector2 Max)` corners (axis-aligned even under rotation) — for culling before drawing many world objects. `device` is only a fallback when there's no adapter; omit it when one is set. |

## Constructing a camera

Two constructors, no bare parameterless one — every `Camera2D` is explicit about its setup:

```csharp
// Raw screen-space: offset is assumed to already be in the same pixel space as screen/mouse input.
var camera = new Camera2D(target: Vector2.Zero, offset: new Vector2(400, 300), rotation: 0f, zoom: 1f);

// With a ViewportAdapter2D — MonoGame.Extended's own OrthographicCamera(ViewportAdapter) shape.
// Offset defaults to (and live-tracks) the adapter's virtual center; every screen<->world method
// and UpdateWithInput's mouse-drag pan account for the adapter automatically.
var camera = new Camera2D(adapter, target: Vector2.Zero, rotation: 0f, zoom: 1f);
```

Prefer the adapter constructor whenever a `ViewportAdapter2D` is in play. `Camera2D.CreateCentered(GraphicsDevice, target)` is a shortcut for the no-adapter form, centered on the device's current viewport. `Camera2D.CreateDefault()` gives every field its identity value (no pan, no zoom, world origin at the screen's top-left) — a placeholder to construct with before a real target/viewport is known, not a "sensible default camera."

`ViewportAdapter` itself (`get`-only) is fixed at construction — there's no way to attach or swap one afterward; construct a new `Camera2D` if you need a different adapter.

### Offset and ViewportAdapter: live vs. pinned

This is the one genuinely non-obvious behavior in the whole class. When a camera is constructed with a `ViewportAdapter2D` and `Offset` has never been assigned directly, `Offset` **live-tracks** the adapter's virtual center — recomputed as `(VirtualWidth/2, VirtualHeight/2)` on every read, not frozen at whatever it was when the camera was built:

- For `BoxingViewportAdapter2D`/`ScalingViewportAdapter2D`, virtual size never changes for the adapter's lifetime, so this is indistinguishable from a one-time snapshot.
- For `DefaultViewportAdapter2D`/`WindowViewportAdapter2D`, virtual size tracks the live device/window size — so `Offset` keeps re-centering across a window resize instead of quietly drifting off-center.

Assigning `Offset` directly — even to the value it already holds — **pins** it from that point on, exactly like a plain field: `camera.Offset = Vector2.Zero;` (so `Target` draws at the top-left corner instead of centered) stays exactly `(0,0)` forever after, through any number of resizes. `camera.Reset()` un-pins it again, restoring the construction-time behavior (which, for the adapter constructor, *was* live-tracking).

This goes one step further than MonoGame.Extended's own `OrthographicCamera.Origin`, which is a plain construction-time snapshot for the camera's entire lifetime — Extended's actual reactive mechanism (`GameWindow.ClientSizeChanged`) exists to keep `GraphicsDevice.Viewport` itself in sync instead, a different problem this library already solves differently (see the adapter section below). Making `Offset` live is simply the same "recompute live, no cache to invalidate" philosophy this library's adapters already use for `Scale`/`Offset`, applied consistently to the camera too.

## Bounds, padding, and clamping

```csharp
camera.TargetBounds = new Rectangle(0, 0, worldWidth, worldHeight);
camera.BoundsPadding = 32f; // keep the camera's look-at point at least 32 world units from the edge
```

`TargetBounds` (nullable `Rectangle`, default `null` = disabled) clamps `Target` into itself, shrunk by `BoundsPadding`. `ClampToBounds()` applies the clamp on demand — called automatically by `FollowTarget` and by `UpdateWithInput`; call it yourself after setting `Target` directly if you want the same clamp without going through either of those. Padding wider than the bounds on an axis collapses to that axis's center rather than producing an inverted clamp range.

## Smooth follow

```csharp
camera.FollowSmoothTime = 0.2f; // seconds to close ~95% of the remaining distance
camera.FollowPadding = 8f;      // deadzone radius in world units

// once per frame:
camera.FollowTarget(player.Position, deltaSeconds);
```

Eases `Target` toward `desiredTarget` via critically-damped spring smoothing (`SmoothDamp`, the same algorithm as Unity's `Mathf.SmoothDamp` — no overshoot across varying frame rates) instead of snapping. Within `FollowPadding` world units of the goal, the camera holds still — a deadzone, not constant low-amplitude jitter. `ResetFollowVelocity()` clears the internal smoothing velocity; call it after teleporting the camera or its subject to avoid a lingering swoop.

`Camera2D.SmoothDamp(float current, float target, ref float velocity, float smoothTime, float deltaTime)` and its `Vector2` overload are public static methods — reach for them directly on any float/`Vector2` value you want to ease the same way (a health bar filling in, a UI panel sliding into place), not just for the camera's own `Target`/`Zoom`.

## Smooth zoom

```csharp
camera.MinZoom = 0.1f;
camera.MaxZoom = 10f;
camera.ZoomSmoothTime = 0.12f;

camera.SmoothZoom(delta, deltaSeconds);
```

Adds `delta` to `Zoom`'s *target*, eased over `ZoomSmoothTime` seconds (`0` disables smoothing — instant) and clamped to `[MinZoom, MaxZoom]`. For discrete input (a mouse wheel tick, naturally `0` most frames): call every frame, most calls no-op. For continuous input (a key held to zoom), **don't** call this every frame with a small nonzero `delta` — each call adds onto the target immediately, so a delta repeated every frame races the target ahead of the easing rather than climbing smoothly through it. Adjust `Zoom` directly by `rate * deltaSeconds` for that case instead.

## Input-driven update

```csharp
camera.Update(deltaSeconds);                 // no input: just decays shake, settles zoom easing
camera.UpdateWithInput(primitiveInput, deltaSeconds); // + built-in WASD/drag/wheel/reset controller
```

`Update` is the passive tick — call it every frame when you're driving `Target`/`Zoom`/`Rotation` yourself (a fixed prototype camera, a cutscene) and just want shake/easing to keep working.

`UpdateWithInput` is a prototyping convenience, not something every game wants baked in — it reads a `PrimitiveInput` instance *you* own and update (it never calls `PrimitiveInput.Update` itself):

| Input | Effect |
|---|---|
| `W`/`A`/`S`/`D` | Pans `Target` in world axes at `MoveSpeed` world units/second, divided by `Zoom` so it covers the same amount of *screen* space per second at any zoom level. |
| Left-mouse drag | Pans the camera so the point under the cursor tracks the drag exactly — rotation-compensated (a rotated camera's drag still tracks the cursor correctly, not just at `Rotation == 0`) and divided by the adapter's `Scale` when one is set, since `PrimitiveInput.MouseDelta` is always real window pixels. |
| Mouse wheel | `SmoothZoom`'d by `MouseWheelZoomSensitivity`. |
| `R` | Calls `Reset()` directly and skips movement/panning for that frame, so a same-frame WASD/mouse delta doesn't immediately fight the reset. |

`MoveSpeed`/`MouseWheelZoomSensitivity` are editable properties (`DefaultMoveSpeed`/`DefaultMouseWheelZoomSensitivity` name their defaults) for tuning feel without subclassing.

## Reset()

```csharp
camera.Reset(); // or press R via UpdateWithInput
```

Restores `Target`/`Offset`/`Rotation`/`Zoom` to the values passed at construction, un-pins `Offset` if it had been assigned directly since (see above), and clears smooth-zoom/smooth-follow/shake state so there's no lingering velocity or trauma to swoop/shake through afterward. Matches `Camera3D.Reset()`'s exact contract and default `R` binding.

## Screen shake (trauma-based)

```csharp
camera.AddTrauma(0.4f); // on a hit/impact/explosion
```

The technique most engines converge on (Squirrel Eiserloh's "Math for Game Programmers: Juicing Your Cameras With Math", used by Celeste and plenty else): `Trauma` is a `[0,1]` value that jumps up on impact and decays back to `0` over time (`TraumaDecayPerSecond`), with shake magnitude scaled by `trauma²` — small bumps barely shake, big hits shake hard, and the falloff itself feels natural instead of linear. The offset is sampled from this library's own `Noise` (one channel per axis, one for rotation) rather than raw random, so it reads as a smooth camera bump instead of jitter.

| Member | What it does |
|---|---|
| `AddTrauma(amount)` | Bumps `Trauma` up, clamped to `[0,1]` — use this instead of setting `Trauma` directly so stacking several hits in one frame can't overshoot. |
| `ResetTrauma()` | Stops any shake immediately. |
| `MaxShakeOffset` / `MaxShakeRotation` | Displacement (world units) / rotation (radians) at maximum trauma (`1.0`) — actual values scale with `trauma²`. |
| `ShakeNoiseSpeed` | How fast the underlying noise is sampled — higher reads as faster/more frantic, lower as a slower sway. |
| `GetShakeOffset()` | The current frame's shake `(Offset, Rotation)` — already folded into `GetTransformMatrix()`; exposed separately in case you want to apply the same shake to something else (a UI element). |

Purely a rendering effect: baked into `GetTransformMatrix()`, never touches `Target`/`Rotation`/`Zoom` themselves, so nothing that reads camera state sees the shake.

---

## ViewportAdapter2D: the four adapters

All four share one surface (`VirtualWidth`/`VirtualHeight`/`Scale`/`Offset`/`BoundingRectangle`/`GetScaleMatrix()`/`PointToVirtual`/`VirtualToPoint`/`Apply()`/`Reset()`), so code written against the base `ViewportAdapter2D` type works unchanged no matter which one is plugged in.

| Adapter | Behavior | Bars? | Use when |
|---|---|---|---|
| `DefaultViewportAdapter2D` | 1:1 with `Device.Viewport`, no fixed virtual resolution — tracked live | No | You don't want resolution independence but still want to code against `ViewportAdapter2D`, so you can swap in a real one later without touching call sites. |
| `WindowViewportAdapter2D` | 1:1 with `GameWindow.ClientBounds` | No | Same as Default, but you specifically need window-client-area size rather than device-viewport size (they can briefly disagree right after a resize on some backends). |
| `BoxingViewportAdapter2D` | Fixed virtual resolution, uniform scale (fits inside the window), preserves aspect ratio | Yes — letterbox/pillarbox | Pixel-art or fixed-composition prototypes where black bars are less objectionable than distortion. |
| `ScalingViewportAdapter2D` | Fixed virtual resolution, independent per-axis scale, fills the window exactly | No | Filling the window matters more than preserving the virtual resolution's aspect ratio. |

```csharp
var adapter = new BoxingViewportAdapter2D(GraphicsDevice, virtualWidth: 480, virtualHeight: 270);
```

Unlike MonoGame.Extended's adapters, none of these subscribe to `GameWindow.ClientSizeChanged` — `Scale`/`Offset`/`BoundingRectangle` read `Device.Viewport` (or `PresentationParameters`/`GameWindow.ClientBounds`) live on every access instead of caching a value that needs an event to invalidate. Simpler, and correct by construction; the cost is a few extra property reads per frame, irrelevant next to actual draw calls.

## Using an adapter with 2D content

Pass the adapter to `Camera2D`'s constructor rather than threading it through every call site:

```csharp
var camera2d = new Camera2D(adapter, target: Vector2.Zero, zoom: 1f);

// once per frame, before drawing -- do NOT call adapter.Apply() here: GetTransformMatrix()
// already folds in adapter.GetScaleMatrix() (scale + offset); narrowing the device viewport
// too would double-apply the offset (see "Apply() vs GetScaleMatrix()" below).
primitiveBatch.Begin(camera2d.GetTransformMatrix());
// ... draw in virtual (480x270) coordinates ...
primitiveBatch.End();

// mouse input: ScreenToWorld/WorldToScreen take raw window pixels and handle the adapter
// mapping internally -- no manual PointToVirtual call needed once the camera owns the adapter.
Vector2 mouseWorld = camera2d.ScreenToWorld(rawMousePosition);
```

## Using an adapter with 3D content

A 3D scene doesn't have a "virtual resolution" the way 2D content does — what it needs from a boxed viewport is just the aspect ratio and the on-screen sub-rectangle, so the same adapter instance a 2D layer uses for letterboxing also works for 3D. `Camera3D` takes it the same way `Camera2D` does, at construction:

```csharp
var camera3d = new Camera3D(adapter, position: ..., target: ..., up: Vector3.Up, fovy: 50f);
primitive3DBatch.Begin(camera3d); // applies the adapter automatically, no separate parameter
```

`Begin(camera)` calls `camera.ViewportAdapter?.Apply()` first (narrowing `Device.Viewport` to `BoundingRectangle`) before deriving its projection aspect ratio from `Device.Viewport.AspectRatio`. Without an adapter, a 3D scene always projects using the full backbuffer's aspect ratio — if the window is letterboxed for 2D content, an adapter-less 3D scene stretches into the bars instead of matching them. `WorldToScreen`/`ScreenToWorld`/`GetScreenToWorldRay` (picking, mouse rays) resolve the stored adapter automatically too — pass an explicit `Viewport` argument only to override it for one call.

## Apply() vs GetScaleMatrix(): use one, never both

Two different ways of applying the same adapter to the same content — mixing them double-applies the offset (only visible for adapters with a nonzero `Offset`, e.g. `BoxingViewportAdapter2D`):

- **2D's path**: fold `GetScaleMatrix()` into the draw transform (`Camera2D.GetTransformMatrix()` already does this) and *don't* call `Apply()` first.
- **3D's path**: `Primitive3DBatch.Begin(camera)` calls `Apply()` internally, narrowing the hardware viewport; the projection matrix itself needs no offset, since the GPU maps NDC to whatever `Device.Viewport` currently is.

If a frame draws both layers, call `adapter.Reset()` before each layer that needs the full window (2D's draw, and the HUD), and let 3D's `Begin(camera)` apply the boxed viewport itself right before its own draw calls — see `examples/test/ViewportTest`'s `Draw()` for the exact pattern:

```csharp
protected override void Draw(GameTime gameTime)
{
    adapter?.Reset();      // full window
    Draw3DScene();         // Begin(camera3d) applies the adapter itself, internally

    adapter?.Reset();      // HUD always covers the full window
    DrawHud();
}
```

### Giving the boxed area a different color than the bars

`GraphicsDevice.Clear()` ignores a narrowed viewport — it always clears the *entire* render target. Clearing once for the bars, then narrowing via `Apply()` and calling `Clear()` again for a different "inside" color wipes the bars too. Draw a rectangle sized to the current viewport instead — an actual draw call *is* confined to `Viewport`, unlike `Clear()`:

```csharp
adapter.Apply(); // narrow the viewport once
Viewport vp = GraphicsDevice.Viewport;
batch2d.Begin(); // Begin() derives its own projection from the CURRENT (already-narrowed) viewport
batch2d.FillRectangle(0, 0, vp.Width, vp.Height, insideColor); // background, drawn before the scene
batch2d.End();

batch3d.Begin(camera3d); // calls Apply() again internally -- same rect, harmless
// ... draw the 3D scene on top ...
batch3d.End();
```

## See also

- [`Guide/Primitive2DBatch_Guide.md`](Primitive2DBatch_Guide.md) — everything `Camera2D.GetTransformMatrix()` feeds into.
- [`Guide/PrimitiveInput_Guide.md`](PrimitiveInput_Guide.md) — the `PrimitiveInput` instance `UpdateWithInput` reads from.
- [`Guide/Easing_Guide.md`](Easing_Guide.md) — fixed-duration tweens, for when `SmoothDamp`'s open-ended spring isn't the right shape.
- [`Design/DECISIONS.md`](../Design/DECISIONS.md) — the mouse-drag rotation fix, `Reset()` parity with `Camera3D`, the `Offset` live-tracking decision (including the direct comparison against MonoGame.Extended's actual `OrthographicCamera` source), and `BoundingRectangle`'s rounding fix.
- `examples/test/ViewportTest` — every adapter mode, 2D and 3D, side by side.
