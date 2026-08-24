# Primitive3D changes log

Separate from the 2D library's `Overnight_Changes_2026-08-19.md` per request. Namespace `MonoPrimitives3D`. Base code (`Camera3D.cs`, `CameraController.cs`, `Primitive3DBatch.cs`, `Primitive3DBatchShapes.cs`, `TrigLut.cs`) already existed in a folder named `Primiteves3D` (typo) — renamed to `Primitive3D`, kept as the base per your instruction, given the same treatment as the 2D library rather than a rewrite from scratch.

**tl;dr:** real bug fixed (duplicate W-key check doubling forward speed), whole shape API renamed to the 2D library's Fill/Border/Draw convention (+ one naming inconsistency fixed: Circle3D now uses the same `Ex`-suffix pattern as Sphere/Cylinder), rotation added via `Quaternion` where it wasn't already possible, grids split into 3 axis-specific methods using the cheap line-list path, basic flat-shading lighting (opt-in, no vertex-format change), `Camera3D`+`CameraController` merged into one class with real bounds/padding/smooth-follow/smooth-zoom/easing support, and 4 new prototyping-oriented additions (Torus, 3D splines, Heightmap/Terrain, dashed lines, bulk shape helpers) picked by thinking through your named use cases (boids, cellular automata, physics, terrain, pandemic sims). Compiles clean against the real project. No gradients (not requested for 3D), no guide doc yet (per your "muy temprano" — too early).

## Bug found and fixed

**`CameraController.ReadDefaultInput`**: the `W` key check was duplicated on one line (`if (...) input.Movement.Z += speed; if (...) input.Movement.Z += speed;`), applying forward movement at double speed compared to every other direction. Fixed to a single check.

## API renamed to match the 2D library (Fill/Border/Draw)

The existing code used raylib's own `DrawX` (filled) / `DrawXWires` (wireframe) naming. Renamed to the 2D library's `Fill<Shape>`/`Border<Shape>`/`Draw<Shape>` (fill+border together) convention, and added the missing `Draw<Shape>` combined overload for every shape that didn't have one:

- Circle3D: `FillCircle3D` (was `DrawCircleFilled3D`), `BorderCircle3D` (was `DrawCircle3D`), new `DrawCircle3D` combined. Both now share the same `rotationAxis`/`rotationAngle` orientation parameters (the filled version used a plain `normal` vector before — unified for consistency).
- Triangle3D (single triangle): `FillTriangle3D` (was `DrawTriangle3D`), new `BorderTriangle3D`, new `DrawTriangle3D` combined. Got rotation too (see below) — `DrawTriangleStrip3D` (multi-point raw mesh submission) was deliberately left alone, matching the 2D library's own precedent of not splitting `DrawTriangleFan`/`DrawTriangleStrip` into Fill/Border either.
- Cube: `FillCube`/`FillCubeV` (was `DrawCube`/`DrawCubeV`), `BorderCube`/`BorderCubeV` (was `DrawCubeWires`/`DrawCubeWiresV`), new `DrawCube`/`DrawCubeV` combined.
- BoundingBox: `FillBoundingBox`/`BorderBoundingBox` (was `DrawBoundingBox`/`DrawBoundingBoxWires`), new `DrawBoundingBox` combined.
- Sphere: `FillSphere`/`FillSphereEx` (was `DrawSphere`/`DrawSphereEx`), `BorderSphere`/`BorderSphereEx` (was `DrawSphereWires`, which had no simple-overload sibling before — added one for symmetry), new `DrawSphere`/`DrawSphereEx` combined.
- Cylinder/Cone: `FillCylinder`/`FillCylinderEx` (was `DrawCylinder`/`DrawCylinderEx`), `BorderCylinder`/`BorderCylinderEx` (was `DrawCylinderWires`/`DrawCylinderWiresEx`), new `DrawCylinder`/`DrawCylinderEx` combined.
- Capsule: `FillCapsule` (was `DrawCapsule`), `BorderCapsule` (was `DrawCapsuleWires`), new `DrawCapsule` combined.
- Plane: `FillPlane`/`FillPlaneEx` (was `DrawPlane`/`DrawPlaneEx`), new `BorderPlane`/`BorderPlaneEx` (didn't exist — a plane never had a wireframe option), new `DrawPlane`/`DrawPlaneEx` combined.
- Torus (new shape): `FillTorus`/`BorderTorus`/`DrawTorus` from the start.

**Left unrenamed, matching the 2D library's own precedent for stroke/gizmo primitives with no fill-vs-border split:** `DrawLine3D`/`DrawLine3DFast`/`DrawLineStrip3D`, `DrawPoint3D`/`DrawPoint3DCross`, `DrawRay`, `DrawTriangleStrip3D`, `DrawGrid*` (a grid of lines has no "filled" version), `DrawAxes`, `DrawArrow3D`, `DrawFrustumWires`.

## Rotation added (Quaternion, optional, defaults to no-op)

- **Cube** (`FillCube(V)`/`BorderCube(V)`/`DrawCube(V)`): full rotation about the cube's own center — genuinely changes what's on screen, unlike a sphere.
- **Cylinder/Cone "standing" overloads** (`FillCylinder`/`BorderCylinder`/`DrawCylinder`): rotation tilts the +Y axis before computing the implicit end point; the `*Ex` two-endpoint overloads were already fully orientable and didn't need it. No extra "roll" parameter — a cylinder's cross-section is radially symmetric, so roll around its own axis is invisible for a solid color, same reasoning as skipping Sphere rotation entirely.
- **Triangle3D**: rotation about the triangle's own centroid by default (or an explicit `origin`), same pattern as the 2D library's Triangle rotation from earlier tonight.
- **Plane**: new `FillPlaneEx`/`BorderPlaneEx`/`DrawPlaneEx(Vector3, Vector2, Quaternion, Color...)` overloads — the existing `normal`-only overload could tilt a plane but never specify its twist around that normal (an arbitrary, unpredictable choice from `BuildBasis`); the new Quaternion overload gives full control. Purely additive, the normal-based overload is untouched.
- **Torus**: rotation from the start, same pattern as Cube.

**Deliberately not added:** Sphere (rotationally symmetric solid fill, no visible effect), Capsule (already fully oriented via its two endpoints, like Cylinder's `*Ex` form), BoundingBox (axis-aligned by definition — that's what makes it a *bounding* box).

## New shape

**Torus** (`FillTorus`/`BorderTorus`/`DrawTorus`) — not in raylib's `rmodels`, but a common primitive elsewhere (rings, wheels, donuts). Standard parametric torus (`(R + r·cos v)·(cos u, 0, sin u) + (0, r·sin v, 0)`), hole facing +Y by default, tilts via the same `Quaternion rotation` pattern as Cube.

## Grid: split into 3 axis-specific methods

Raylib's `DrawGrid` only ever draws the XZ ground plane. Added `DrawGridXY`/`DrawGridYZ` alongside the renamed `DrawGridXZ` (kept `DrawGrid` as an alias for `DrawGridXZ`, matching raylib's original name for the most common case). Deliberately 3 separate named methods instead of one method with a plane-selector enum/parameter, so there's no runtime branch on the hot path — each just plugs its own two constant basis vectors into a shared private `DrawGridPlane` helper. Per your explicit performance ask, grid lines use `DrawLine3DFast` (the raw GPU line-list path) instead of `DrawLine3D`'s camera-facing quad — a grid is a lot of thin reference lines where raw throughput matters more than per-line anti-aliased thickness, and this also matches raylib's own choice of plain thin lines for `DrawGrid`.

## Verification

Compiled the actual project (`dotnet build` in `MonogameLibs/`, not a scratch copy — `Primitive3D` is auto-included by the SDK-style project's default glob, same as `Primitives2D`) after every batch of changes; 0 errors, 1 pre-existing unrelated warning (`Game1._target` unused field, not touched). Not independently render-verified the way tonight's 2D geometry changes were (no 3D scratch-render harness exists yet, and building one would cost real tokens) — the renamed methods carry over already-working geometry unchanged, and the new rotation/Torus/Grid math was checked by hand against the standard formulas instead.

**Housekeeping note:** the folder rename briefly produced a stray duplicate copy at `Primitives2D/Primiteves3D/` (an artifact of the rename step in this sandboxed shell, not any of your data) — caught immediately via the resulting duplicate-definition compile errors and deleted; confirmed via diff it was just a stale pre-edit copy before removing it.

## Basic lighting (was backlog, now done)

Flat per-face shading without changing the vertex format (still `VertexPositionColor`, still `BasicEffect.LightingEnabled = false` — the "lighting" is baked into the vertex color before it's pushed, not done by the GPU). New on `Primitive3DBatch`: `LightingEnabled` (off by default — zero behavior change unless you opt in), `LightDirection`, `AmbientLight` (brightness floor for faces pointing away from the light). Two new internal helpers, `PushQuadLit`/`PushTriangleLit`, compute the face's own normal via cross product and darken the supplied color before handing off to the existing `PushQuad`/`PushTriangle` — every filled-surface shape (Cube, Sphere, Cylinder/Cone, Capsule, Torus, Plane, `FillTriangle3D`, `FillCircle3D`) now routes through these. Deliberately **not** applied to lines, points, the grid, or `DrawTriangleStrip3D` — a camera-facing line quad has no meaningful surface normal, and lighting it would flicker as the camera moves.

## Camera: merged into one class, with real follow/bounds/zoom/easing support

Per your request — merged `Camera3D` (struct) + `CameraController` (class) into a single `Camera3D` class (deleted `CameraController.cs`). Raylib splits these because C has no methods; C# doesn't need that split, and one object owning both its state and its own update logic is simpler to use (`camera.Update(deltaSeconds)` instead of `controller.Update(ref camera, deltaSeconds)`). `Camera3D` is now a reference type, so `Primitive3DBatch.Begin` takes it as a plain `Camera3D camera` parameter (was `in Camera3D`) — added a null check there for the same reason the constructor already null-checks `graphicsDevice`.

Everything from both original files carries over unchanged (`Position`/`Target`/`Up`/`Fovy`/projection math, `Yaw`/`Pitch`/`Roll`, `MoveForward`/`MoveRight`/`MoveUp`, `Mode`/`Update`/`ReadDefaultInput`, world↔screen projection, frustum) — this also carries the W-key bug fix forward, since it was fixed in-place before the merge. New, addressing "padding, limits, follow with delay, zoom, easing":

- **Limits**: `MinDistance`/`MaxDistance` clamp `SmoothZoom`'s (and the Orbital/ThirdPerson/Free auto-zoom's) target distance. `PositionBounds` (nullable `BoundingBox`) optionally confines `Position` — checked at the end of every `Update` and every `FollowTarget` call.
- **Padding**: `BoundsPadding` shrinks `PositionBounds` inward before clamping (so the camera stops short of the hard edge, not flush against it — collapses to the box's center on an axis if the padding would invert the range, rather than producing a nonsensical min>max clamp). `FollowPadding` is a separate deadzone radius for `FollowTarget`: the camera holds still until the subject drifts at least that far away, instead of chasing every small jitter.
- **Follow with delay**: `FollowTarget(desiredPosition, deltaSeconds, desiredTarget = null)` — eases `Position` toward the goal with the same `SmoothDamp` spring used by `SmoothZoom` (see below), moving `Target` by the same delta by default so the look direction doesn't snap, or toward its own independent goal if you pass `desiredTarget`. `FollowSmoothTime` controls the lag (seconds to close ~95% of the remaining distance); `ResetFollowVelocity()` clears the internal spring state after a teleport so the camera doesn't swoop in from where it used to be.
- **Zoom**: `SmoothZoom(delta, deltaSeconds)` eases the camera's distance from `Target` instead of `MoveToTarget`'s instant snap, clamped to `MinDistance`/`MaxDistance`; `ZoomSmoothTime` controls the lag. The original instant `Zoom(delta, min, max)` (FOV-based) and `MoveToTarget(delta)` (distance-based, instant) are both still there unchanged for anyone who wants the raylib-exact snap behavior.
- **Easing**: the underlying primitive, `Camera3D.SmoothDamp` (`float` and `Vector3` overloads, both `public static`) — the same critically-damped-spring algorithm as Unity's `Mathf.SmoothDamp`/`Vector3.SmoothDamp` (no overshoot/oscillation across varying frame rates, unlike a naive exponential lerp). Exposed publicly, not just used internally, so you can ease your own values (a FOV transition, a light color fade, anything) with the same tool the camera itself uses.

Not independently render-verified (no 3D scratch-render harness, and `SmoothDamp` is a faithful port of a well-known, widely-verified formula rather than new geometry math) — checked by hand-tracing the edge cases instead: zero-delta idle calls do no work, instant mode (`*SmoothTime <= 0`) resolves in one step with no lingering spring state, and bounds-padding that would invert a clamp axis collapses to that axis's center instead of producing a broken min>max range.

## API review: one naming inconsistency found and fixed

`Sphere`/`Cylinder` already followed an `Ex`-suffix pattern for "explicit segment count" (`FillSphere` vs `FillSphereEx`, matching raylib's own `DrawSphere`/`DrawSphereEx`), but `Circle3D` used two same-named overloads instead (`FillCircle3D(...,color)` and `FillCircle3D(...,segments,color)`), and `DrawCircle3D` had a bolted-on `segments = 0` default parameter that neither sibling had. Renamed to match: `FillCircle3DEx`/`BorderCircle3DEx`/`DrawCircle3DEx` now carry the explicit-segments overload, and the plain `DrawCircle3D` dropped its stray `segments` parameter — consistent with `Sphere`/`Cylinder` now.

## New prototyping features, thinking through your listed scenarios

You listed simulations (boids, cellular automata, predator-prey, physics engines, terrain generators, pandemic simulations) and games/plots (retro games, platformers, asteroids-style, line/circle/histogram plots) as the target use cases, and asked what 3D would need for that kind of fast, simple-but-pretty prototyping:

- **`DrawSplineCatmullRom3D` / `DrawSplineBezierCubic3D` (new)** — 2D already had smooth splines, 3D only had straight `DrawLineStrip3D`. Smooth curves matter for camera paths, agent trajectories (boid flock leader paths, predator pursuit curves), and rivers/roads following terrain — same math as the 2D versions (`Vector3.CatmullRom`, a hand-written cubic Bezier since MonoGame has no `Vector3` Bezier helper), same API shape.
- **`FillHeightmap` / `BorderHeightmap` / `DrawHeightmap` (new)** — directly for "terrain generator": turns a `float[,]` grid of heights into a triangulated ground mesh (`FillHeightmap`), its wireframe (useful while prototyping to see the actual mesh resolution), or both. A second `FillHeightmap` overload takes a `Color[,]` for per-cell coloring (a biome/temperature/elevation-band map from your own simulation data) — **not a gradient** (no interpolation is added beyond the ordinary per-vertex color blending every filled shape already has), so it doesn't cross the "no gradients for 3D" line.
- **`DrawLine3DDashed` (new)** — 2D parity; predicted paths, sensor ranges, "not solid" indicators are a common physics/simulation debug need.
- **`FillCubes` / `FillSpheres` (new, thin sugar)** — draws the same shape at every position in a point list, one call instead of a caller-side loop — for cellular automata (many voxel cells) and boid/particle-style scenes (many agents) where you're drawing the same shape hundreds of times a frame.

**Screen↔world conversion, already covered, not new:** you asked whether 3D has a screen-position↔world-position translation — `Camera3D.GetWorldToScreen`/`GetScreenToWorld`/`GetScreenToWorldRay` already did this (carried over from the original base code, unchanged). The 2D library was the one missing this — added as `Camera2D.ScreenToWorld`/`WorldToScreen` (see the 2D log).

All of the above compiled clean against the real project (`dotnet build` in `MonogameLibs/`).

## Gap analysis, round 1

- **`Easing.cs` (new)** — same class as the 2D one (a commonly-used subset of classic tween curves), duplicated here rather than shared since the two libraries stay independent of each other by design.
- Collision was initially skipped for 3D since sphere/box/ray tests already exist natively on MonoGame's own `BoundingSphere`/`BoundingBox`/`Ray` (`.Intersects(...)`) — revisited below once you asked for it explicitly.

## `Collision3D.cs` (new) — wraps MonoGame's native collision, adds what it's missing

You asked for 3D collision after all, specifically framed as wrapping MonoGame's own types for better integration rather than reimplementing them. `Collision3D.cs`:

- **Thin wrappers** — `CheckCollisionSpheres`/`CheckCollisionBoxes`/`CheckCollisionBoxSphere` over `BoundingSphere`/`BoundingBox`'s own `.Intersects(...)`, and `GetRayCollisionSphere`/`GetRayCollisionBox` over `Ray.Intersects(...)` — kept for a consistent name and a richer return type (see `RayCollision3D` below), not because the underlying math needed reimplementing.
- **`RayCollision3D` (new struct)** — a single `{ Hit, Distance, Point, Normal }` result used by every raycast here, instead of MonoGame's own mix of `bool` returns, `float?` returns, and no surface normal at all. Better integration in practice: one shape to consume regardless of which primitive you hit.
- **Genuinely new, not in MonoGame at all: capsule collision.** `FillCapsule`/`BorderCapsule` draw a shape MonoGame has zero bounding-volume support for. Added `CheckCollisionCapsules`, `CheckCollisionCapsuleSphere`, and `GetRayCollisionCapsule` (cylindrical body via an infinite-cylinder intersection clamped to the capsule's span, plus both hemispherical end caps, nearest valid hit wins). The capsule-capsule overlap test reduces to the closest distance between the two capsules' central segments (the standard closest-point-between-two-segments algorithm).
- **`GetRayCollisionPlane` (new)** — infinite plane defined by a point and normal; the natural complement to sphere/box that neither this library nor MonoGame had.

Verified with 28 standalone numeric checks (hit/miss/normal/point/distance for every function, including the two capsule algorithms specifically since they're the most intricate math here) — all pass.

## Bug found in a follow-up pass: `FillHeightmap`'s quad winding gave an inverted (downward) face normal

Both `FillHeightmap` overloads (solid color and per-cell `Color[,]`) built each cell's quad as `p00, p10, p11, p01` (+X then +Z from the corner). Compared against the already-correct `FillPlane` (`p00, p01, p11, p10` — +Z then +X), the axis order was swapped, which flips the cross product's sign: `Cross(+X,+Z) = -Y` instead of `Cross(+Z,+X) = +Y`. Practical effect: a flat/gently-sloped heightmap's computed lighting normal pointed straight down instead of up. Invisible today since `RasterizerState.CullNone` is the default and `LightingEnabled` defaults to off, but real for anyone who turns lighting on for terrain (the most likely thing to do with a *terrain* mesh) or who passes a culling rasterizer state to `Begin` (where it would have made the whole heightmap disappear, not just look wrong). Fixed both overloads to match `FillPlane`'s winding order; verified numerically (`Cross` of the fixed vertex order at the origin, confirmed exactly `(0,1,0)`) rather than just re-reading the code, since a sign flip is exactly the kind of thing that's easy to eyeball-confirm wrong.

## Library reorganized into `MonoPrimitives/3D/` (provisional name)

Moved out of the loose sibling folder `Primitive3D/` (directly under the game project) into `MonoPrimitives/3D/`, alongside the 2D library's own new `MonoPrimitives/2D/` — see that log for the full detail (namespace `MonoPrimitives3D` left unchanged, only the file location moved; verified file-for-file identical before deleting the old copies, then a full rebuild confirmed everything still resolves).

## Scope check: added, then removed, a `Verlet3D` physics helper

Looked into what your favorite channel ("pezzza's work") does — Verlet-integration particle simulation — and built `Integrate`/`ResolveSphereOverlap`/`SolveDistanceConstraint`/`ConstrainToSphere`. You caught this immediately: collision *resolution* and constraint *solving* is a physics engine's job, not this library's — it stays focused on drawing and standalone-usable helpers (detection like `Collision3D`, not resolution). Deleted before it saw further use.

## `SpatialHashGrid3D.cs` (new)

Same as the 2D library's own `SpatialHashGrid2D` (see that log for the full writeup and sources) — a uniform hash grid bucketing entities by cell for fast radius queries, the standard technique for flocking/crowd simulations at scale. `Clear()`/`Insert(item, position)`/`QueryRadius(center, radius, results)`, one extra dimension. Verified against a brute-force scan over 400 random points, 20/20 queries matched exactly.

## `Palette.cs` (new)

Same as the 2D library's own `Palette` (see that log for detail) — 20 colors from the well-known "Flat UI Colors" reference set, duplicated here for consistency between the two libraries.

## Documentation correction: no more naming raylib in doc comments

You flagged that naming raylib inside XML doc comments (which show up as the library's actual API reference) reads as unprofessional in what's meant to be user-facing documentation. Went through every `.cs` file touched this session and rewrote every raylib mention into a plain description of the behavior itself — the design *inspiration* doesn't change, just removed the name-dropping from anything a consumer of the library would actually read. Also trimmed several of the wordier doc comments down while doing this pass, per your "be concise" note. Historical `.md` change logs (this file, the 2D one) weren't scrubbed — those are session notes explaining *my* reasoning to you, not product documentation, so a raylib mention there is just me explaining where an idea came from, not something a library user would see.

## `Camera3D.SmoothZoom` usage trap, fixed via documentation (found while testing the 2D twin)

While load-testing `Camera2D.SmoothZoom` under a realistic multi-frame usage pattern, found the same accumulation logic exists here (both cameras share the identical `_pendingZoomTarget` NaN-sentinel design). Calling `SmoothZoom(delta, dt)` every frame with a small nonzero `delta` — the natural way to write "hold a key to zoom continuously" — races the target toward the clamp almost immediately instead of climbing smoothly, since each call adds onto the in-flight target rather than the current eased distance. Not a bug in the math — this is exactly correct for the actual call site in `Update` (`SmoothZoom(input.Zoom, deltaSeconds)`, where `input.Zoom` is naturally 0 on all but a handful of mouse-wheel-tick frames) — but the doc comment didn't warn against the continuous-input anti-pattern, so clarified it there and pointed that use case at `MoveToTarget` with a `rate * deltaSeconds` step instead. Recompiled clean.

## `DrawGrid`/`DrawGridXZ`/`DrawGridXY`/`DrawGridYZ` default colors were full-alpha, contradicting your own "low alpha, not intrusive" ask

You'd asked for the grid to be visible but not intrusive — "a color with a low alpha can be pretty but not intrusive to the view" — but `DefaultGridLineColor`/`DefaultGridAxisColor` were `new(0.75f, 0.75f, 0.75f, 1f)`/`new(0.5f, 0.5f, 0.5f, 1f)`: full alpha (1.0), not low. A prior log entry (2D log, since fixed there) had claimed low-alpha grids "already work" without actually checking — they don't, for two independent reasons: these defaults weren't low-alpha to begin with, and (see the blend-state fix below) alpha wasn't being applied correctly by the renderer either way. Lowered the defaults to 0.15 (grid lines) / 0.35 (the center axis lines, kept a bit more visible than the rest as an origin reference) — still overridable via the existing `lineColor`/`axisColor` parameters.

## Real bug found: `Primitive3DBatch`'s default `BlendState.AlphaBlend` doesn't match this library's colors

`BlendState.AlphaBlend` in XNA/MonoGame is the **premultiplied**-alpha preset — it expects the source color's RGB to already be scaled by its own alpha before reaching the GPU. `PushTriangleUnchecked`/`PushLine` write the `Color` straight through unmodified; every color anywhere in this library (including the grid defaults above) is a straight, non-premultiplied `new Color(r,g,b,a)`. Fed to a premultiplied blend state, any translucent draw renders far more opaque than its alpha implies — worked the blend equation by hand: a grid line at alpha 0.15 over a dark background should blend to ≈0.18, but came out ≈0.82 under the old setup. A fully opaque draw (alpha=255, the common case so far) is unaffected either way, which is why this stayed hidden. Fixed by defaulting `Begin`'s `blendState` to `BlendState.NonPremultiplied` instead — the preset that actually matches every color this library constructs. Zero call sites elsewhere needed to change. Full detail and the worked arithmetic are in the 2D log (same bug, same fix, both libraries share the class of issue since both push straight colors). `dotnet build` on the real project succeeds; not verified against a live `GraphicsDevice` (none available here), so this follows directly from documented MonoGame blend-state semantics rather than an on-screen check.

## Real bugs found by a focused review pass: inverted (inward) face normals on `FillSphereEx`, `FillTorus`, and one capsule cap

Delegated a careful review of `Primitive3DBatchShapes.cs` (the least-scrutinized file left) to a subagent, which verified geometry claims by reproducing the actual `PushQuadLit` normal formula (`Cross(b-a, d-a)`) standalone instead of eyeballing winding order. It found three real, related bugs, which I independently re-verified myself before fixing (same standalone-reproduction technique, fresh numeric checks, not just trusting the report):

- **`FillSphereEx`** (quad construction near the `PushQuadLit` call in the ring/slice loop): built each quad walking latitude-then-longitude (`a=(lat0,lon0), b=(lat1,lon0), c=(lat1,lon1), d=(lat0,lon1)`) — the opposite traversal order from the cylinder/capsule quads elsewhere in this file, which walk angle-then-height. Numeric check on an 8-ring/8-slice sphere: 56/56 non-degenerate quads had **inward**-facing normals (the 8 pole quads are degenerate either way). Fixed by passing the same 4 points to `PushQuadLit` in reversed order (`a, d, c, b` instead of `a, b, c, d`) — swapping two arguments flips the winding without changing which 4 points are used, so no geometry changes, only which way each quad faces. Affects `FillSphere`/`DrawSphere`/`DrawSphereEx`/`FillSpheres`.
- **`FillTorus`**: identical bug, identical fix (same "ring-then-side" traversal order as the sphere's "latitude-then-longitude"). 96/96 quads on a 12-ring/8-side torus were inward before the fix.
- **`FillCapsule`'s start-side hemisphere cap**: `FillHemisphere` is called twice — once for the end cap with `up=+n` (matching the shaft's own `t/b` basis), once for the start cap with `up=-n` but reusing the *same* `t/b` unchanged. That flips `(t, b, up)` from right-handed to left-handed without anything else compensating, so the start cap's quads came out inward (32/32) while the end cap's were correctly outward (32/32) — an asymmetric capsule, one dome correct and one inside-out. Fixed by adding a `flipWinding` parameter to the shared `FillHemisphere` helper (reverses the same two-argument swap as above) instead of rebuilding a fresh `t/b` basis for the start cap — rebuilding the basis independently would have "fixed" the normal but risked misaligning the cap's equatorial ring with the shaft's own seam ring, since a freshly-built basis for `-n` has no guarantee of agreeing with the shaft's `+n` basis on which direction is angle-zero.

Why this matters even though every shape was already visually validated once this session (via the standalone `System.Drawing`/`mgtest` PNG/console checks): none of those checks used `LightingEnabled` or a non-default `RasterizerState`, both real documented features of this batch. With `LightingEnabled = true`, these three shapes were being lit from the wrong side (dark where it should be lit and vice versa); with any back-face-culling rasterizer state — which this same file's own `FillHeightmap` doc comment explicitly anticipates callers using — a filled sphere or torus would disappear entirely and a capsule would have one hemisphere vanish, while every other shape in the file stayed visible. A purely solid-color, `LightingEnabled=false`, `CullNone` render (the default, and what every earlier check in this session used) can't surface a winding-direction bug at all — both winding directions produce an identical flat-colored silhouette. Independently re-verified all three fixes numerically (reproducing the real formulas in a standalone script): sphere 56/56 outward, torus 96/96 outward, capsule end cap 32/32 + start cap 32/32 both outward. `dotnet build` on the real project succeeds.

## `PrimitiveInput.cs` (new) + `Camera3D.ReadDefaultInput` refactored to use it

Same class as the 2D library's own `PrimitiveInput` (see that log for the full writeup: keyboard/mouse/gamepad wrapping, `GetAxis`/`GetVector2` composite helpers matching Godot's `Input.get_axis`/`get_vector`, mouse drag/double-click/hit-test helpers), namespace `MonoPrimitives3D` instead of `Primitives2D` — duplicated per this project's existing precedent for small shared-shape helpers between the two libraries.

`Camera3D.ReadDefaultInput` rewritten to use it, per your explicit ask ("Haz que los controles de la camara 3D usen esa libreria interna"): the old `_previousMouse`/`_hasPreviousMouse` fields and manual mouse-delta/wheel-delta math are gone, replaced by one `PrimitiveInput` instance and its `MouseDelta`/`MouseScrollDelta`/`IsMouseButtonDown`. The W/A/S/D movement read now goes through `GetVector2(Keys.A, Keys.D, Keys.S, Keys.W, normalize: false)` instead of four separate `IsKeyDown` checks — `normalize: false` deliberately, to keep this byte-for-byte the same speed profile as before (each axis independently adds `speed`, so a diagonal like W+D still moves at `sqrt(2)*speed`, exactly as it did before this refactor; `GetVector2`'s own default normalization was consciously opted out of here rather than silently changing existing camera feel). `ReadDefaultInput`'s public signature and `CameraInput` output are unchanged — this is an internal refactor, not a behavior change. `ResetMouseTracking()` now delegates to `PrimitiveInput.ResetMouseDelta()`. `dotnet build` succeeds.

## Needs your input

*(nothing blocking right now — flagging here only if something comes up)*
