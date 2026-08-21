# Camera3D — Guide

`Camera3D` (namespace `MonoPrimitives.Primitives3D`, file [`src/3D/Camera3D.cs`](../src/3D/Camera3D.cs)) is a 3D camera — view/projection matrices, 5 behaviour modes, bounds clamping, smooth-follow, smooth-zoom, trauma-based screen shake, and an optional WASD/mouse-look/wheel controller — merged into one class that owns both its pose and its update logic. Same overall shape as [`Camera2D`](Camera2D_Guide.md); this guide covers what's different in 3D.

## Quick start

```csharp
using MonoPrimitives.Primitives3D;

private Primitive3DBatch _batch;
private Camera3D _camera;

protected override void LoadContent()
{
    _batch = new Primitive3DBatch(GraphicsDevice);
    _camera = new Camera3D(position: new Vector3(10, 10, 10), target: Vector3.Zero, up: Vector3.Up, fovy: 45f);
}

protected override void Update(GameTime gameTime)
{
    _camera.UpdateWithInput(_input, gameTime); // or Update(gameTime) if you drive the camera yourself
}

protected override void Draw(GameTime gameTime)
{
    _batch.Begin(_camera); // aspect ratio comes from the current device viewport (or the camera's own ViewportAdapter)
    _batch.FillSphere(Vector3.Zero, 1f, Color.Red);
    _batch.End();
    base.Draw(gameTime);
}
```

`Position`/`Target`/`Up` are plain public fields — set them directly for a hand-driven camera (a fixed cutscene angle, a custom rig); nothing stops you from ignoring `Mode`/`UpdateWithInput` entirely and only using `GetViewMatrix()`/`GetProjectionMatrix()`.

## Basis vectors and matrices

| Member | What it does |
|---|---|
| `Forward` / `Right` / `UpNormalized` | Normalized basis, recomputed from `Position`/`Target`/`Up` on every access. `Up` is only a "roughly up" hint — it doesn't need to be exactly perpendicular to `Forward`; `UpNormalized` is just `Up` normalized. Each degenerates to a sane fallback (`Vector3.Forward`/`Up`/`Right`) if the input is near-zero-length (e.g. `Position == Target`). |
| `TargetDistance` | `Vector3.Distance(Position, Target)`. |
| `GetViewMatrix()` | `Matrix.CreateLookAt(Position, Target, UpNormalized)`, with any screen-shake offset/roll (see below) folded in — `Position`/`Target`/`Up` themselves are never modified by shake. |
| `GetProjectionMatrix(aspectRatio)` | Perspective (`Fovy` in degrees) or orthographic (`Fovy` as world height) depending on `Projection`. |
| `GetViewProjectionMatrix(aspectRatio)` / `GetFrustum(aspectRatio)` | Combined matrix / `BoundingFrustum` built from it, for culling before submitting primitives. |
| `GetPixelScale(viewportHeight)` | Approximate world units per screen pixel at unit distance — what `Primitive3DBatch` uses internally to size pixel-width lines. |
| `WorldToScreen`/`ScreenToWorld`/`GetScreenToWorldRay` | Project/unproject/picking-ray, resolving `ViewportAdapter` automatically when set (pass an explicit `Viewport` to override) — same shape as `Camera2D`'s screen↔world conversions. See `Guide/Camera2D_Guide.md`'s "Using an adapter with 3D content" for the full adapter story. |

## Movement helpers

Call these directly for custom bindings instead of `UpdateWithInput`:

| Member | What it does |
|---|---|
| `MoveForward(distance, moveInWorldPlane)` / `MoveRight` / `MoveUp` | Moves `Position` and `Target` together by the same delta, so the look direction doesn't change. `moveInWorldPlane` (Forward/Right only) flattens the Y component first — the usual choice for on-foot movement so looking up/down doesn't slow/speed horizontal walking. |
| `MoveToTarget(delta)` | Moves `Position` along `Forward` to change `TargetDistance` by `delta` (negative = closer) — an instant zoom for orbital-style cameras; clamps to a small positive distance instead of crossing through `Target`. |
| `Yaw(angle, rotateAroundTarget)` / `Pitch(angle, lockView, rotateAroundTarget, rotateUp)` | Rotate around `UpNormalized`/`Right` respectively. `rotateAroundTarget: false` (default) rotates `Target` around `Position` (mouse-look style); `true` rotates `Position` around `Target` (orbit-camera style) instead — `TargetDistance` is preserved either way. `Pitch`'s `lockView` clamps so the view can't flip past straight-up/down; `rotateUp` (Free mode only) also rotates `Up` itself, for full unconstrained rotation. |
| `Roll(angle)` | Rotates `Up` around `Forward` (barrel roll) — `Forward` itself is unaffected. |
| `SetZoom(fovy)` / `Zoom(delta, min, max)` | Set `Fovy` directly, or nudge it by `delta` clamped to `[min, max]` (defaults `1`/`179`). |

## Modes and `UpdateWithInput`

```csharp
camera.Mode = CameraMode.Free; // Custom | Free | Orbital | FirstPerson | ThirdPerson
camera.UpdateWithInput(primitiveInput, deltaSeconds);
```

`UpdateWithInput` reads a `PrimitiveInput` instance *you* own and update (it never calls `PrimitiveInput.Update` itself):

| Input | Effect |
|---|---|
| `W`/`A`/`S`/`D` + `Space`/`Ctrl` | Move at `MoveSpeed * MoveSpeedScale` world units/second; flattened to the horizontal plane in `FirstPerson`/`ThirdPerson`. |
| `Q`/`E` | Yaw the camera body left/right. |
| `Z`/`X` | Roll (`Free` mode only). |
| Right-mouse drag | Mouse-look (yaw/pitch) at `MouseMoveSensitivity * LookSensitivity` — subtracted, "grab and drag" feel, same convention as `Camera2D`'s left-drag pan. |
| Arrow keys | Keyboard-driven look, alternative to the mouse. |
| Mouse wheel | `SmoothZoom`'d by `MouseWheelZoomSensitivity` (`Free`/`ThirdPerson`/`Orbital`). |
| `R` | Calls `Reset()` and skips movement for that frame. |

`Custom` mode does nothing (you drive the camera entirely yourself); `Orbital` auto-rotates around `Target` at `OrbitalSpeed` radians/second; `FirstPerson` adds head-bobbing (`HeadBobbing`, `EyeHeight` as a suggested reference height — only the game knows ground height, so bobbing only ever nudges the existing `Position.Y`/`Target.Y`, it never sets them); `ThirdPerson` orbits `Position` around `Target` on look input. `MoveSpeed`/`RotationSpeed`/`MouseMoveSensitivity`/`MouseWheelZoomSensitivity`/`OrbitalSpeed` are editable properties (`Default*` constants name their defaults).

## Bounds, smooth follow, smooth zoom, screen shake

Identical shape and API to `Camera2D`'s (see its guide for the full rationale) — `PositionBounds`/`BoundsPadding`/`ClampToBounds()`, `FollowTarget(desiredPosition, deltaSeconds, desiredTarget?)`/`FollowSmoothTime`/`FollowPadding`, `SmoothZoom(delta, deltaSeconds)`/`MinDistance`/`MaxDistance`/`ZoomSmoothTime`, `AddTrauma`/`ResetTrauma`/`Trauma`/`GetShakeOffset()`. Two 3D-specific notes:

- `SmoothZoom` moves `Position` along `Forward` to change `TargetDistance` (not a `Zoom` scalar like 2D, since 3D zoom is dolly-in/out distance) — call it once per discrete request (a wheel tick), not every frame with the same nonzero `delta`, or the target races ahead instead of easing.
- Shake is applied along the camera's own `Right`/`UpNormalized` axes (not world axes) plus a roll around `Forward`, so it reads as camera shake regardless of which way the camera faces.

## Reset()

```csharp
camera.Reset(); // or press R via UpdateWithInput
```

Restores `Position`/`Target`/`Up`/`Fovy`/`Projection`/`NearPlane`/`FarPlane` to construction-time values, and clears smooth-zoom/smooth-follow/head-bobbing/screen-shake state (including `Trauma`) so there's no lingering velocity or shake to swoop through afterward — matches `Camera2D.Reset()`'s exact contract. Deliberately leaves `Mode` alone — that's a control-scheme choice, not part of the camera's pose.

## Testing

[`tests/MonoPrimitives.Tests/Camera3DTests.cs`](../tests/MonoPrimitives.Tests/Camera3DTests.cs) covers the basis vectors, movement/rotation (including `Pitch`'s pole-lock), zoom/follow/bounds clamping, screen shake, matrix/projection correctness (a `WorldToScreen`↔`ScreenToWorld` round-trip, `GetScreenToWorldRay`'s direction), and `Reset()`'s full state restoration. Run with:

```bash
dotnet run --project tests/MonoPrimitives.Tests/MonoPrimitives.Tests.csproj
```

## See also

- [`Guide/Camera2D_Guide.md`](Camera2D_Guide.md) — the 2D counterpart; bounds/follow/zoom/shake are the same shape, and it covers `ViewportAdapter2D` usage shared by both.
- `samples/MonoPrimitives.Sample/Gallery3D.cs` — a free-fly camera driving the 3D shape gallery.
- `examples/demos/Asteroids3D/` — `Camera3D.FollowTarget` driving a lag-behind chase cam, with yaw/pitch flight kept relative to world `Up` for predictable turning.
