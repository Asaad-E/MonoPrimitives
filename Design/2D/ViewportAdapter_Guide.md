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

```csharp
var adapter = new BoxingViewportAdapter2D(GraphicsDevice, virtualWidth: 480, virtualHeight: 270);
var camera2d = new Camera2D(...);

// once per frame, before drawing:
adapter.Apply(); // narrows Device.Viewport to the boxed rect; clears/draws stop at its edge
primitiveBatch.Begin(camera2d.GetTransformMatrix() * adapter.GetScaleMatrix());
// ... draw in virtual (480x270) coordinates ...
primitiveBatch.End();

// mouse input: convert window pixels to virtual coordinates before hit-testing
Vector2 virtualMouse = adapter.PointToVirtual(rawMousePosition);
```

## Using it with 3D content

A 3D scene doesn't have a "virtual resolution" the way 2D content does — what it needs from
a boxed viewport is just the aspect ratio and the on-screen sub-rectangle, so the same
adapter instance a 2D layer uses for letterboxing also works for 3D:

```csharp
Primitive3DBatch.Begin(Camera3D camera, ViewportAdapter2D viewportAdapter, ...)
```

This calls `viewportAdapter.Apply()` first (narrowing `Device.Viewport` to
`BoundingRectangle`), then proceeds exactly like the plain `Begin(camera)` overload — which
already derives its projection aspect ratio from `Device.Viewport.AspectRatio`. Without this
overload (or an equivalent manual `Apply()` + `Begin(camera)`), a 3D scene always projects
using the full backbuffer's aspect ratio, so if the window is letterboxed for 2D content, the
3D scene stretches into the bars instead of matching them. See DECISIONS.md.

## Coexisting 2D + 3D in one window

Draw order matters when both use the same adapter: call `adapter.Apply()` (directly, or via
the `Primitive3DBatch.Begin(camera, adapter)` overload) before *each* layer's draw calls —
it's idempotent and cheap, so there's no reason to try to call it only once per frame.
