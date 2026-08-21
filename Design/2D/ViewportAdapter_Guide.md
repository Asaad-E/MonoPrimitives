# ViewportAdapter2D — usage guide

MonoGame.Extended-parity resolution independence, folded into this library so a prototype
doesn't need the Extended package just for this. Game logic and drawing work in a fixed
**virtual** resolution regardless of the actual window size; the adapter maps that virtual
space onto whatever window size actually exists.

## The four adapters

| Adapter | Behavior | Bars? | Use when |
|---|---|---|---|
| `DefaultViewportAdapter2D` | 1:1 with `Device.Viewport`, no virtual resolution | No | You don't want resolution independence but still want to code against `ViewportAdapter2D` (so you can swap in a real one later without touching call sites). |
| `WindowViewportAdapter2D` | 1:1 with `GameWindow.ClientBounds` | No | Same as Default, but you specifically need window-client-area size rather than device-viewport size (they can briefly disagree right after a resize on some backends). |
| `BoxingViewportAdapter2D` | Fixed virtual resolution, uniform scale (fit), preserves aspect ratio | Yes — letterbox/pillarbox | Pixel-art or fixed-composition prototypes where distortion is worse than bars. |
| `ScalingViewportAdapter2D` | Fixed virtual resolution, non-uniform scale, fills window exactly | No | Filling the window matters more than preserving the virtual resolution's aspect ratio. |

All four share one surface (`VirtualWidth`/`VirtualHeight`/`Scale`/`Offset`/`BoundingRectangle`/
`GetScaleMatrix()`/`PointToVirtual`/`VirtualToPoint`/`Apply()`/`Reset()`), so code written
against the base `ViewportAdapter2D` type works unchanged no matter which one is plugged in.

Unlike MonoGame.Extended's adapters, these don't subscribe to `GameWindow.ClientSizeChanged` —
`Scale`/`Offset`/`BoundingRectangle` read `Device.Viewport` (or `GameWindow.ClientBounds`) live
on every access instead of caching a value that needs an event to invalidate. Simpler, and
correct by construction; the cost is a few extra property reads per frame, which is irrelevant
next to actual draw calls.

## Using it with 2D content

Pass the adapter to `Camera2D`'s constructor (MonoGame.Extended's own `OrthographicCamera(ViewportAdapter)`
shape) rather than threading it through every call — once stored, `ScreenToWorld`/`WorldToScreen`/
`GetVisibleWorldBounds`/the mouse-drag part of `ReadDefaultInput` all use it automatically:

```csharp
var adapter = new BoxingViewportAdapter2D(GraphicsDevice, virtualWidth: 480, virtualHeight: 270);
var camera2d = new Camera2D(adapter, target: Vector2.Zero, zoom: 1f);
// camera2d.Offset now defaults to the adapter's virtual center (240, 135) — override it if you
// want a different anchor (e.g. Vector2.Zero so Target is the point drawn at the top-left corner).

// once per frame, before drawing:
adapter.Apply(); // narrows Device.Viewport to the boxed rect; clears/draws stop at its edge
primitiveBatch.Begin(camera2d.GetTransformMatrix() * adapter.GetScaleMatrix());
// ... draw in virtual (480x270) coordinates ...
primitiveBatch.End();

// mouse input: ScreenToWorld/WorldToScreen take raw window pixels and handle the adapter mapping
// internally — no manual PointToVirtual call needed once the camera owns the adapter.
Vector2 mouseWorld = camera2d.ScreenToWorld(rawMousePosition);
```

## Using it with 3D content

A 3D scene doesn't have a "virtual resolution" the way 2D content does — what it needs from
a boxed viewport is just the aspect ratio and the on-screen sub-rectangle, so the same
adapter instance a 2D layer uses for letterboxing also works for 3D. `Camera3D` takes it the
same way `Camera2D` does — at construction:

```csharp
var camera3d = new Camera3D(adapter, position: ..., target: ..., up: Vector3.Up, fovy: 50f);

primitive3DBatch.Begin(camera3d); // applies adapter automatically, no separate parameter
```

`Begin(camera)` calls `camera.ViewportAdapter?.Apply()` first (narrowing `Device.Viewport` to
`BoundingRectangle`) before deriving its projection aspect ratio from `Device.Viewport.AspectRatio`.
Without an adapter, a 3D scene always projects using the full backbuffer's aspect ratio, so if
the window is letterboxed for 2D content, an adapter-less 3D scene stretches into the bars
instead of matching them. `GetWorldToScreen`/`GetScreenToWorld`/`GetScreenToWorldRay` (picking,
mouse rays) also resolve the stored adapter automatically — pass an explicit `Viewport` argument
only to override it for one call. See DECISIONS.md.

## Coexisting 2D + 3D in one window

Draw order matters when both use the same adapter: call `adapter.Apply()` (directly, or via
the `Primitive3DBatch.Begin(camera, adapter)` overload) before *each* layer's draw calls —
it's idempotent and cheap, so there's no reason to try to call it only once per frame.
