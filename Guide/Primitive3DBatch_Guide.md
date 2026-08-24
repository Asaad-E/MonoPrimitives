# Primitive3DBatch — Guide

`Primitive3DBatch` (namespace `MonoPrimitives.Primitives3D`, files [`src/3D/Primitive3DBatch.cs`](../src/3D/Primitive3DBatch.cs) + [`Primitive3DBatchShapes.cs`](../src/3D/Primitive3DBatchShapes.cs)) is the 3D counterpart to [`Primitive2DBatch`](Primitive2DBatch_Guide.md) — immediate-mode shape drawing for MonoGame, one call per shape, no retained scene graph or model loading.

This guide covers every public method, grouped by shape family. For per-parameter detail beyond what's here, the XML doc comments in `Primitive3DBatchShapes.cs` go deeper.

## Quick start

```csharp
using MonoPrimitives.Primitives3D;

private Primitive3DBatch _batch;
private Camera3D _camera;

protected override void LoadContent()
{
    _batch = new Primitive3DBatch(GraphicsDevice); // one instance, reused every frame
    _camera = new Camera3D(position: new Vector3(6, 6, 6), target: Vector3.Zero, up: Vector3.Up, fovy: 50f);
}

protected override void Draw(GameTime gameTime)
{
    _batch.Begin(_camera); // or Begin(view, projection) for your own camera representation
    _batch.FillSphere(Vector3.Zero, 1f, Color.Red);
    _batch.BorderCube(new Vector3(3, 0, 0), 2f, 2f, 2f, Color.Black);
    _batch.End();
    base.Draw(gameTime);
}
```

Construct one `Primitive3DBatch` per `GraphicsDevice` and keep it — its internal buffers are allocated once and reused every frame. Wrap every frame's drawing in `Begin`/`End`.

| Method | What it does |
|---|---|
| `Primitive3DBatch(graphicsDevice, maxVertices = DefaultMaxVertices)` | Constructor. `maxVertices` is the vertex capacity before an automatic mid-batch flush — `DefaultMaxVertices` (`49152`) is generous for typical scenes; raise it if you're submitting many large meshes (heightmaps, many spheres) per frame and want fewer draw calls. |
| `Begin(camera, blendState, depthStencilState, rasterizerState, transform)` | Starts a batch using a `Camera3D` — applies its `ViewportAdapter` automatically if it has one. |
| `Begin(view, projection, ...)` | Starts a batch with explicit matrices, for interop with your own camera representation. |
| `End()` | Submits any buffered geometry and restores device state. |
| `Flush()` / `FlushLine()` | Submits buffered triangle/line geometry immediately without ending the batch. |
| `Dispose()` | Releases the internal effect. |
| `PendingVertices` / `Capacity` | Vertices currently buffered and not yet flushed / total vertex capacity before an automatic flush. |
| `DrawCalls` / `TrianglesSubmitted` / `LinesSubmitted` | Reset on every `Begin` — running totals since then, for profiling how many draw calls or how much geometry a frame actually costs. |

## Core conventions

**Fill / Border / Draw**, same as 2D: `Fill<Shape>` is solid color, `Border<Shape>` is outline only (defaults to `DefaultLineThickness` via a `thickness <= 0` sentinel), `Draw<Shape>` is both together — `fillColor` required, `borderColor = null` optional (defaults to `fillColor`, so a single-color call needs no repeated argument).

**Segment/ring/slice counts are explicit parameters** on every curved shape except `Sphere`/`Circle3D`, whose simple overloads pick automatically (see "Segment counts" below). Pass `-1` for automatic level-of-detail (picked from distance-to-camera and radius so each edge stays roughly constant-size on screen), or `0` to use the shape's own default.

**Rotation** on the "standing upright"/centered shapes (`Cube`, `Cylinder`, `Torus`) is a `Quaternion`, not radians — the natural rotation type in 3D. `Circle3D` is the one exception: it takes `Vector3 rotationAxis, float rotationAngle` (angle in **degrees**), an axis-angle pair instead of a full quaternion. Shapes fully described by two endpoints (`Capsule`, the two-point `Cylinder` overload) take no separate rotation parameter — they're already oriented by their own points. `Sphere` takes no rotation at all — a solid-color sphere looks identical from any angle.

## Lines, points, rays, arrows

| Method | What it does |
|---|---|
| `DrawLine3D(start, end, color)` / `(start, end, thickness, color)` | A camera-facing quad, so it reads as a real 3D line with thickness from any angle. `thickness` is in pixels when `SmoothLines` (default `true`) is on, world units otherwise. |
| `DrawLine3DFast(start, end, color)` | A raw GPU line-list segment — always 1px, no camera facing, the cheapest option for large numbers of lines. |
| `DrawLineStrip3D(points, color, thickness?)` | A connected polyline through any number of points, drawn as one joined camera-facing strip — adjacent segments share a miter-joined offset at each interior vertex, so a sharp bend doesn't gap or overlap even at non-trivial thickness. |
| `DrawLineStrip3D(points, segmentColors, thickness?)` | Same, with an independent flat color per segment (`segmentColors.Length == points.Length - 1`) — e.g. a fade along the strip's length. Shared corner positions mean two differently-colored segments still meet cleanly. `Trail3D.Draw` is built on this. |
| `DrawLine3DDashed(start, end, dashLength, gapLength, color)` / `(..., thickness, color)` | A dashed line. |
| `DrawPoint3D(position, color)` / `(position, size, color)` | A point drawn as a short line along local X. `size` defaults to `DefaultPointSize`. |
| `DrawPoint3DCross(position, color)` / `(position, size, color)` | A point drawn as a 3-axis cross — more readable when the camera angle is unknown. `size` defaults to `DefaultPointSize`. |
| `DrawRay(ray, color)` / `DrawRay(ray, length, color)` | A ray drawn as a line; defaults to a length of 1000 units. |
| `DrawArrow(start, end, color)` | Shaft + cone head, sized automatically from length and `DefaultLineThickness`. |
| `DrawArrow(start, end, thickness, color, headLength?, headRadius?, sides = 12)` | Same, with full control over shaft thickness and head size. |
| `DrawArrow(start, end, headRadius, sides, color)` | A simpler overload: fixed shaft width, head sized purely from `headRadius`. |

## Circles and triangles

| Method | What it does |
|---|---|
| `FillCircle3D(center, radius, rotationAxis, rotationAngle, [segments,] color)` | A filled disc in an arbitrary plane. |
| `BorderCircle3D(center, radius, rotationAxis, rotationAngle, [segments,] color, thickness)` | Outline only. |
| `DrawCircle3D(center, radius, rotationAxis, rotationAngle, [segments,] fillColor, borderColor = null, thickness)` | Fill + border. |
| `FillTriangle3D(v1, v2, v3, color, rotation, origin)` | A filled triangle; vertices counter-clockwise when viewed from the front. `rotation`/`origin` let you spin it around a pivot without recomputing the three points. |
| `BorderTriangle3D(v1, v2, v3, color, rotation, origin, thickness)` | Outline (the three edges) only. |
| `DrawTriangle3D(v1, v2, v3, fillColor, borderColor = null, rotation, origin, thickness)` | Fill + border. |
| `DrawTriangleStrip3D(points, color)` | A raw triangle-strip mesh, points submitted as given — no Fill/Border split, since a strip has no single "inside." |

## Cube and bounding box

| Method | What it does |
|---|---|
| `FillCube(position, width, height, length, color, rotation)` / `FillCube(position, size, color, rotation)` | A filled cube (or box), centered at `position`. |
| `BorderCube(..., color, rotation, thickness)` | Wireframe only. |
| `DrawCube(..., fillColor, borderColor = null, rotation, thickness)` | Fill + border. |
| `FillBoundingBox(box, color)` / `BorderBoundingBox(box, color, thickness)` / `DrawBoundingBox(box, fillColor, borderColor = null, thickness)` | Same, from a `BoundingBox` directly — always axis-aligned, no rotation parameter. |

## Sphere

No `rotation` parameter anywhere — a solid-color sphere is rotationally symmetric.

| Method | What it does |
|---|---|
| `FillSphere(center, radius, color)` | A filled sphere, ring/slice count picked automatically (see "Segment counts" below). |
| `FillSphere(center, radius, rings, slices, color)` | Same, with an explicit ring/slice count. |
| `FillSphere(BoundingSphere, color)` | Same, from a `BoundingSphere` directly. |
| `BorderSphere(center, radius, color, thickness)` / `(center, radius, rings, slices, color, thickness)` | Wireframe (latitude + longitude lines) only. |
| `DrawSphere(center, radius, fillColor, borderColor = null, thickness)` / `(..., rings, slices, ...)` | Fill + border. |

## Cylinder and cone

Two overload families with deliberately different parameter names: `radiusTop`/`radiusBottom`/`slices` for a cylinder standing on `position` and extending along `+Y` (`rotation` tilts it — "top"/"bottom" is a meaningful direction there); `startRadius`/`endRadius`/`sides` for a cylinder between two arbitrary points (already fully oriented by its own endpoints). Either radius can be `0` for a cone.

| Method | What it does |
|---|---|
| `FillCylinder(position, radiusTop, radiusBottom, height, slices, color, rotation)` | Standing form. |
| `FillCylinder(startPos, endPos, startRadius, endRadius, sides, color)` | Two-point form. |
| `BorderCylinder(...)` (both forms, `+ thickness`) | Wireframe only. |
| `DrawCylinder(...)` (both forms, `+ fillColor, borderColor = null, thickness`) | Fill + border. |

## Capsule

A cylinder between two points with hemispherical caps of `radius` — already fully oriented by its own two endpoints, no separate rotation parameter. Degenerates cleanly to a sphere when `startPos == endPos`.

| Method | What it does |
|---|---|
| `FillCapsule(startPos, endPos, radius, slices, rings, color)` | Filled capsule. |
| `BorderCapsule(startPos, endPos, radius, slices, rings, color, thickness)` | Wireframe only. |
| `DrawCapsule(startPos, endPos, radius, slices, rings, fillColor, borderColor = null, thickness)` | Fill + border. |

## Torus

A donut shape, lying flat on XZ (hole facing `+Y`) unless `rotation` tilts it. `radius` is to the tube's centerline, `tubeRadius` is the tube's own thickness.

| Method | What it does |
|---|---|
| `FillTorus(center, radius, tubeRadius, sides, rings, color, rotation)` | Filled torus. |
| `BorderTorus(..., color, rotation, thickness)` | Wireframe only. |
| `DrawTorus(..., fillColor, borderColor = null, rotation, thickness)` | Fill + border. |

## Plane

| Method | What it does |
|---|---|
| `FillPlane(centerPos, size, color)` | A filled plane on the XZ axes. |
| `FillPlane(centerPos, size, normal, color)` | Tilted so its face normal is `normal`; the in-plane twist is arbitrary. |
| `FillPlane(centerPos, size, rotation, color)` | Fully explicit orientation (tilt and twist) via a `Quaternion`. |
| `BorderPlane(centerPos, size, color, thickness)` / `(centerPos, size, rotation, color, thickness)` | The plane's 4 edges only — the default-XZ and `Quaternion` forms (no separate `normal` overload). |
| `DrawPlane(centerPos, size, fillColor, borderColor = null, thickness)` / `(centerPos, size, rotation, fillColor, borderColor = null, thickness)` | Fill + border, same two forms. |

## Lighting (flat shading, opt-in)

```csharp
batch.LightingEnabled = true;             // off by default -- no behavior change otherwise
batch.LightDirection = new Vector3(-0.5f, -1f, -0.35f); // direction the light travels, not toward it
batch.AmbientLight = 0.35f;               // brightness floor for faces pointing away from the light
```

Each filled triangle/quad is shaded by its own face normal — a cheap, per-face flat look, not smooth per-vertex lighting. Lines, points, and the grid are never shaded.

## Splines

Same four spline types as 2D (see [`Primitive2DBatch_Guide.md`](Primitive2DBatch_Guide.md#splines) for the shape of each curve), each drawn as a loop of `DrawLine3D` calls rather than a single joined strip.

| Method | What it does |
|---|---|
| `DrawSplineCatmullRom3D(points, [thickness,] color, segmentsPerPiece)` | A smooth curve that passes through every point. Needs at least 4 points. |
| `DrawSplineBasis3D(points, [thickness,] color, segmentsPerPiece)` | A uniform cubic B-spline — does **not** pass through its own control points, only stays inside their shape. |
| `DrawSplineBezierCubic3D(points, [thickness,] color, segmentsPerPiece)` | A cubic Bézier spline: `[p1, c2, c3, p4, c5, c6, p7, ...]` — needs `3n + 1` points for `n` segments. |
| `DrawSplineBezierQuadratic3D(points, [thickness,] color, segmentsPerPiece)` | A quadratic Bézier spline: `[p1, c2, p3, c4, p5, ...]` — needs `2n + 1` points for `n` segments. |
| `GetSplinePointCatmullRom3D` / `BezierCubic3D` / `BezierQuadratic3D` / `Basis3D` (static) | The raw math behind each `DrawSpline*3D` above, if you need a point on the curve without drawing it. |

## Grid, axis, heightmap, bulk helpers

| Method | What it does |
|---|---|
| `DrawGridXZ` / `DrawGridXY` / `DrawGridYZ` (`slices, spacing`) | A reference grid on the named plane, centered at the origin, using subtle default colors. `DrawGrid` is an alias for `DrawGridXZ`. |
| `DrawGridXZ` / `XY` / `YZ` `(slices, spacing, origin, lineColor, majorLineColor, showMajorLines, lineThickness)` | Same, fully customized. Every 5th line draws wider in `majorLineColor` when `showMajorLines` is true. |
| `DrawAxis(size)` / `DrawAxis(size, color)` / `DrawAxis(origin, size, color, thickness)` | A single-color X/Y/Z axis triad. |
| `DrawAxes(origin, length)` | The classic red/green/blue axis gizmo. |
| `FillHeightmap(heights, origin, cellSize, color)` | A triangulated ground mesh from a `float[,]` grid of heights. |
| `FillHeightmap(heights, colors, origin, cellSize)` | Same, with an independent color per grid vertex. |
| `BorderHeightmap(heights, origin, cellSize, color, thickness)` / `DrawHeightmap(...)` | Wireframe / fill + wireframe. |
| `FillCubes(positions, size, color, rotation)` / `FillSpheres(positions, radius, color)` | The same shape at every position in a span — for particle-style scenes (boids, cellular automata) that want one call instead of a loop. |
| `BorderFrustum(frustum, color)` | Wireframe of a `BoundingFrustum` — for visualizing another camera's view volume. |

## Segment counts: chosen for you, overridable

Only `Sphere` and `Circle3D` have a no-tessellation-arguments overload, and both pick automatic level-of-detail by default rather than a fixed segment count — `Cylinder`/`Capsule`/`Torus` always take their segment counts explicitly. Pass `-1` for any of them to get the same automatic level-of-detail instead of hand-picking a number — `ResolveSegments(requested, radius, [center,] fallback)` is the public method behind that choice (distance-to-camera and radius in, a segment count out, clamped to `[AutoSegmentsMin, AutoSegmentsMax]` = `[8, 96]`), exposed in case you want the same auto-LOD sizing for your own curved geometry.

## Testing

[`tests/MonoPrimitives.Tests/ShapeTests3D.cs`](../tests/MonoPrimitives.Tests/ShapeTests3D.cs) generates every public 3D shape once against a real `GraphicsDevice` and checks it actually emitted geometry. Run with:

```bash
dotnet run --project tests/MonoPrimitives.Tests/MonoPrimitives.Tests.csproj
```

## See also

- [`Guide/Primitive2DBatch_Guide.md`](Primitive2DBatch_Guide.md) — the 2D sibling; most conventions here carry over directly.
- [`Guide/Camera3D_Guide.md`](Camera3D_Guide.md) — the camera `Begin(camera)` takes.
- [`Guide/TrigLut_Guide.md`](TrigLut_Guide.md) — the trig table every curved shape here samples from.
- [`Guide/Collision3D_Guide.md`](Collision3D_Guide.md) — hit-testing the shapes this guide draws.
- `samples/MonoPrimitives.Sample/Gallery3D.cs` — every shape family, one row each, Fill/Border/Draw per cell.
