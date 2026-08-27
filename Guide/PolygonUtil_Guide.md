# PolygonUtil — Guide

`PolygonUtil` (namespace `MonoPrimitives.Primitives2D`, file [`src/2D/PolygonUtil.cs`](../src/2D/PolygonUtil.cs)) is the polygon geometry `Primitive2DBatch.FillPolygon` already used internally to fill an arbitrary polygon, exposed for building your own mesh/collision/nav data from a polygon shape.

## API

| Member | What it does |
|---|---|
| `IsConvex(points)` | True if every interior angle is ≤ 180 degrees (no reflex vertices). This is exactly the condition `Collision2D`'s SAT-based checks (`CheckCollisionPolyPoly`/`RecPoly`/`RecTriangle`/`TriangleTriangle`) require of their input — check here first if you're not sure a polygon qualifies. |
| `Triangulate(points, outIndices)` | Ear-clipping triangulation for an arbitrary simple polygon (concave allowed; must not self-intersect). Writes up to `(points.Length - 2) * 3` local indices (0-based into `points`, 3 per triangle) into `outIndices` and returns how many were written — or `0` if triangulation got stuck on degenerate/self-intersecting input. |

## Quick start

```csharp
using MonoPrimitives.Primitives2D;

ReadOnlySpan<Vector2> polygon = myShape;
Span<int> indices = stackalloc int[(polygon.Length - 2) * 3];
int written = PolygonUtil.Triangulate(polygon, indices);

if (written > 0)
{
    // indices[0..written] is triangle data, 3 per triangle, indexing into `polygon` --
    // feed straight into a physics engine, a nav mesh, or your own custom mesh builder.
    for (int i = 0; i < written; i += 3)
        AddTriangle(polygon[indices[i]], polygon[indices[i + 1]], polygon[indices[i + 2]]);
}
```

## Notes

- `Triangulate` returns `0` rather than throwing on input it can't handle (degenerate or self-intersecting) — the same fallback contract `FillPolygon` itself relies on internally (it falls back to a plain fan from `points[0]` in that case).
- `IsConvex` returns `true` for fewer than 4 points — a triangle (or a 2-point/1-point degenerate span) has nothing to be concave about.
- This is pure geometry, not drawing — pair it with `Primitive2DBatch` if you also want to render the polygon, or with `Collision2D` if you're deciding which overlap check to use.
