# Primitive3DBatch — Guide

`Primitive3DBatch` (namespace `MonoPrimitives.Primitives3D`, files [`src/3D/Primitive3DBatch.cs`](../src/3D/Primitive3DBatch.cs) + [`Primitive3DBatchShapes.cs`](../src/3D/Primitive3DBatchShapes.cs)) is the 3D counterpart to [`PrimitiveBatch`](PrimitiveBatch_Guide.md) — immediate-mode shape drawing, one call per shape, no retained scene graph. Confirmed against raylib's `rmodels.h`: every 3D shape function raylib has, this already covers as a strict superset (unified into `Fill`/`Border`/`Draw` instead of raylib's own `Ex`/`V`/`Wires`-suffixed sprawl) — see `Design/DECISIONS.md`.

## Quick start

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
    _batch.Begin(_camera); // aspect ratio from the device viewport, or camera.ViewportAdapter if set
    _batch.FillSphere(Vector3.Zero, 1f, Color.Red);
    _batch.BorderCube(new Vector3(3, 0, 0), 2f, 2f, 2f, Color.Black);
    _batch.End();
    base.Draw(gameTime);
}
```

## Core conventions (same shape as 2D, read `PrimitiveBatch_Guide.md`'s "Core conventions" first if you haven't)

- **`Fill<Shape>`/`Border<Shape>`/`Draw<Shape>`** per closed shape — `Draw` is a `Fill` + `Border` in one call, always as a `fillColor, borderColor` overload (no single-color `Draw` here, since a filled 3D shape without light needs two colors to actually distinguish edge from face). `Border` thickness defaults to `DefaultLineThickness` via the `<= 0` sentinel convention (`thickness: float = -1f`).
- **No `Ex`/`V`-suffixed overloads** — a two-endpoint cylinder (`FillCylinder(startPos, endPos, startRadius, endRadius, sides, color)`) and a "standing upright" one (`FillCylinder(position, radiusTop, radiusBottom, height, slices, color, rotation)`) are both just `FillCylinder`, not `FillCylinderEx`/`FillCylinderV`. Same for `Cube`(vector-size vs. width/height/length) and `Capsule`/`Torus`.
- **Segment/ring/slice counts are always explicit parameters, never omitted** for anything but `Sphere` (see "Segment counts" below) — matches raylib's own `DrawCylinder`/`DrawCapsule` shape exactly, not an inconsistency.
- **`-1` means automatic level-of-detail** wherever a segment count parameter exists — `ResolveSegments` estimates on-screen size from distance-to-camera and picks a count so each edge is ~6px. `0` means "use this shape's own compiled-in default" instead.

## Lines, points, rays, arrows

| Member | What it does |
|---|---|
| `DrawLine3D(start, end, color)` / `(start, end, thickness, color)` | A camera-facing quad (two triangles), so it can have real thickness and stays visible edge-on. `thickness` is in *pixels* when `SmoothLines` (default `true`) is on, world units otherwise. |
| `DrawLine3DFast(start, end, color)` | A raw GPU line-list segment — always 1px, no camera facing, cheapest option. The 3D equivalent of 2D's `DrawPixelFast` vs `DrawPixel` naming split. |
| `DrawLineStrip3D(points, color)` | A connected polyline, each segment independently drawn via `DrawLine3D`. **Known limitation**: at non-trivial thickness, a sharp bend shows a visible gap/overlap, since each segment computes its own camera-facing offset independently — see `Design/ROADMAP.md`. Fine for thin/near-1px debug lines; don't rely on it for a thick, sharp-cornered ribbon. |
| `DrawPoint3D` / `DrawPoint3DCross` | A point as a short line along local X, or as a 3-axis cross (more readable when the camera angle is unknown) — both sized from `DefaultPointSize`. |
| `DrawRay(ray, color)` / `(ray, length, color)` | A ray drawn as a line; defaults to 1000 units. |
| `DrawArrow(start, end, color)` | Shaft + cone head, sized automatically from length and `DefaultLineThickness`. `DrawArrow(start, end, thickness, color, headLength?, headRadius?, sides = 12)` for full control; `DrawArrow(start, end, headRadius, sides, color)` for a simpler fixed-shaft-width overload. |
| `DrawLine3DDashed` | Dashed line — predicted paths, ranges, "not solid" debug indicators. |

## Circles, triangles

`FillCircle3D`/`BorderCircle3D`/`DrawCircle3D(center, radius, rotationAxis, rotationAngle, ...)` draw a flat disc in an arbitrary plane (rotation in **degrees** around an axis, unlike everywhere else in this library which uses radians — matches raylib's own `DrawCircle3D` parameter shape exactly). `FillTriangle3D`/`BorderTriangle3D`/`DrawTriangle3D` take 3 vertices plus an optional `Quaternion rotation`/`Vector3? origin` for rotating in place. `DrawTriangleStrip3D(points, color)` submits a raw triangle strip mesh (like 2D's own `DrawTriangleStrip`, no triangulation) — no `Fill`/`Border` split, since a strip has no single "inside."

## Solid shapes: Cube, Sphere, Cylinder, Capsule, Torus, Plane

All follow `Fill`/`Border`/`Draw`. Notes specific to 3D:

- **Cube**: `FillCube(position, width, height, length, color, rotation)` or the vector-size overload; `FillBoundingBox`/`BorderBoundingBox`/`DrawBoundingBox` are thin wrappers for a `BoundingBox` (axis-aligned, no rotation parameter — that's what a `BoundingBox` is).
- **Sphere**: no `rotation` parameter — a solid-color UV sphere is rotationally symmetric, rotating it has no visible effect (same reasoning 2D documents for `FillCircleGradient`). `FillSphere(center, radius, color)` uses default tessellation; `FillSphere(center, radius, rings, slices, color)` is explicit. A `BoundingSphere` overload exists too.
- **Cylinder/cone**: two overload families, deliberately different parameter names — `radiusTop`/`radiusBottom`/`slices` for the "standing on `position`, extends +Y, `rotation` tilts it" form (top/bottom is a real direction there), `startRadius`/`endRadius`/`sides` for the "between two arbitrary points" form (top/bottom would be misleading once it can point anywhere). A zero radius on either end gives a cone.
- **Capsule**: fully orientable via its two endpoints alone, no separate rotation parameter needed. Degenerates gracefully to a sphere when `startPos == endPos`.
- **Torus**: not in raylib — this library's own addition ("a common shape not otherwise covered"). Lies flat on XZ (hole facing +Y) unless `rotation` tilts it; `radius` is to the tube's centerline, `tubeRadius` is the tube's own thickness.
- **Plane**: three overloads for orientation — XZ-aligned (`FillPlane(centerPos, size, color)`), tilted by a `normal` (in-plane twist arbitrary), or a fully explicit `Quaternion rotation`. A superset of raylib's single XZ-only `DrawPlane`.

## Lighting (flat shading, opt-in)

```csharp
batch.LightingEnabled = true;             // off by default -- zero behavior change otherwise
batch.LightDirection = new Vector3(-0.5f, -1f, -0.35f); // direction the light travels, not toward it
batch.AmbientLight = 0.35f;               // brightness floor for faces away from the light
```

Each filled triangle/quad's own face normal (from its own points via cross product — no stored per-vertex normals, vertex format untouched) is dotted against `LightDirection` and used to darken the shape's color toward `AmbientLight`. Lines, points, and the grid are never shaded (a camera-facing line quad has no meaningful surface normal). This has no 2D equivalent — a flat screen-space shape has no notion of a face normal to light.

## Splines

Full parity with 2D's spline family (`DrawSplineCatmullRom3D`/`BezierCubic3D`/`BezierQuadratic3D`/`Basis3D`, plus static `GetSplinePointCatmullRom3D`/`BezierCubic3D`/`BezierQuadratic3D`/`Basis3D` sampling functions) — added this session to close a gap where 3D only had half the family. `CatmullRom`/`BezierQuadratic`/`Basis` interpolate/approximate through a 4-or-fewer-point sliding window the same way as 2D; `Basis` (uniform cubic B-spline) is the one curve that does NOT pass through its own control points — an approximating curve, traded for extra smoothness. Each is drawn as a loop of independent `DrawLine3D` calls (not a shared-vertex strip the way 2D's `BuildOutlineGeometry` builds splines) — fine at the thin-to-moderate thickness splines are typically drawn at; see the `DrawLineStrip3D` limitation above for why a very thick spline could show sub-segment seams.

## Grid, axis, heightmap, bulk helpers

- **`DrawGridXZ`/`XY`/`YZ`** — three explicit methods (no plane-selector parameter, so there's no runtime branch on the hot path), each drawing a reference grid on its named plane with every-5th-line emphasis (`showMajorLines`). `DrawGrid` is an alias for `DrawGridXZ` (the ground plane). Grid lines never include the axes themselves — use `DrawAxis` separately.
- **`DrawAxis`** — a single-color X/Y/Z triad; **`DrawAxes`** — the classic red/green/blue gizmo. Split into two methods on purpose: an orientation reference is a different concern from a measuring grid, and callers frequently want one without the other.
- **`FillHeightmap`/`BorderHeightmap`/`DrawHeightmap`** — turns a `float[,]` grid of heights into a triangulated ground mesh; a `Color[,]` overload gives each vertex its own color (e.g. a biome/elevation map) instead of one flat fill color.
- **`FillCubes`/`FillSpheres`** — the same shape/color/size at every position in a span, for particle-style scenes (boids, cellular automata) that want one call instead of a loop at the call site.
- **`BorderFrustum(frustum, color)`** — wireframe of a `BoundingFrustum`, for visualizing another camera's view volume. Wireframe-only (a frustum has no sensible "fill"), named `Border*` like every other outline-only closed shape (renamed from `DrawFrustumWires` this session for that consistency — see `Design/DECISIONS.md`).

## Segment counts: chosen for you, overridable

Only `Sphere` gets a no-tessellation-arguments convenience overload (`FillSphere(center, radius, color)`) — `Cylinder`/`Capsule`/`Torus` always take their segment counts explicitly. This isn't an inconsistency: it matches raylib's own `DrawSphere`/`DrawSphereEx` pair exactly, while raylib's `DrawCylinder`/`DrawCapsule` are *also* always-explicit with no simpler sibling. Pass `-1` for automatic LOD (`Primitive3DBatch.ResolveSegments`, distance-and-radius based) instead of a fixed number if you don't want to hand-pick one.

## Testing

[`tests/MonoPrimitives.Tests/ShapeTests3D.cs`](../tests/MonoPrimitives.Tests/ShapeTests3D.cs) generates every public 3D shape method once against a real `GraphicsDevice` and asserts it actually emitted triangles/lines without throwing (`Primitive3DBatch.TrianglesSubmitted`/`LinesSubmitted`) — a regression net against a method silently emitting nothing. [`tests/MonoPrimitives.Tests/LightingRegressionTests.cs`](../tests/MonoPrimitives.Tests/LightingRegressionTests.cs) renders an offscreen lit sphere and reads pixels back, guarding the pole-quad NaN-blackening fix specifically. Run with:

```bash
dotnet run --project tests/MonoPrimitives.Tests/MonoPrimitives.Tests.csproj
```

## See also

- [`Guide/PrimitiveBatch_Guide.md`](PrimitiveBatch_Guide.md) — the 2D sibling; most conventions here were carried over directly from it.
- [`Guide/Camera3D_Guide.md`](Camera3D_Guide.md) / [`Guide/TrigLut_Guide.md`](TrigLut_Guide.md) — the camera `Begin(camera)` takes, and the trig table every curved shape here samples from.
- [`Design/DECISIONS.md`](../Design/DECISIONS.md) — the raylib `rmodels.h` comparison, the spline-family gap closed this session, the `DrawArrow3D`/`DrawFrustumWires` naming fixes, and the doc-precision fixes.
- [`Design/ROADMAP.md`](../Design/ROADMAP.md) — the `DrawLineStrip3D`/`Trail3D` joint-gap limitation, confirmed but deliberately not fixed this pass.
- `samples/MonoPrimitives.Sample/Gallery3D.cs` — every shape family, one row each, Fill/Border/Draw per cell, the same gallery structure as 2D's own.
