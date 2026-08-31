# Collision2D — Guide

`Collision2D` (namespace `MonoPrimitives.Primitives2D`, file [`src/2D/Collision2D.cs`](../src/2D/Collision2D.cs)) is a static class of pure geometry overlap/intersection tests — circle, rectangle, triangle, polygon, capsule, line — plus three raycasts. Detection only: it tells you *whether* (and where) two things overlap, not how to separate them afterward — that's a per-game decision, the same reasoning that keeps every "system" out of this library.

## Quick start

```csharp
using MonoPrimitives.Primitives2D;

if (Collision2D.CheckCollisionCircles(playerPos, playerRadius, enemyPos, enemyRadius))
{
    // your own resolution logic goes here
}
```

## Point tests

| Method | What it does |
|---|---|
| `CheckCollisionPointRec(point, rec)` | Point inside (or on the edge of) a rectangle. |
| `CheckCollisionPointCircle(point, center, radius)` | Point inside (or on the edge of) a circle. |
| `CheckCollisionPointTriangle(point, p1, p2, p3)` | Point inside a triangle, any winding order. |
| `CheckCollisionPointLine(point, p1, p2, threshold = 1f)` | Point within `threshold` world units of a line segment. |
| `CheckCollisionPointPoly(point, points)` | Point inside an arbitrary polygon — even-odd ray-casting (the standard PNPOLY algorithm). Correct for **any simple polygon, convex or not.** |

## Shape vs shape

| Method | What it does |
|---|---|
| `CheckCollisionRecs(rec1, rec2)` | Rectangle vs rectangle (a thin wrapper over `Rectangle.Intersects`, here for naming consistency). |
| `CheckCollisionCircles(center1, radius1, center2, radius2)` | Circle vs circle. |
| `CheckCollisionCircleRec(center, radius, rec)` | Circle vs rectangle. |
| `CheckCollisionCircleLine(center, radius, p1, p2)` | Circle vs line segment. |
| `CheckCollisionCircleCapsule(circleCenter, circleRadius, capsuleStart, capsuleEnd, capsuleRadius)` | Circle vs capsule. |
| `CheckCollisionLines(startPos1, endPos1, startPos2, endPos2, out collisionPoint)` | Segment vs segment, with the actual crossing point (not just the infinite lines' intersection). |
| `CheckCollisionCapsules(aStart, aEnd, aRadius, bStart, bEnd, bRadius)` | Capsule vs capsule — distance between the two axis *segments* (not points), via the standard closest-point-between-segments algorithm (Ericson, *Real-Time Collision Detection*), correctly handling every degenerate case (either/both collapsed to a point, parallel segments) without branching on them. |

## Polygon and mixed-shape overlaps — SAT vs. non-SAT, and why it matters

`CheckCollisionPolys`/`RecPoly`/`RecTriangle`/`CheckCollisionTriangles` all go through the **Separating Axis Theorem (SAT)**: try each polygon's own edge normals as a candidate separating axis; no gap on any of them means the shapes overlap. **This requires both shapes to be convex.** A rectangle and a triangle always are; an arbitrary `points` span is *not* checked for convexity — pass a convex one, or you'll get a wrong answer silently, not an exception.

| Method | Convexity required? |
|---|---|
| `CheckCollisionPolys(poly1, poly2)` | Yes (both) |
| `CheckCollisionTriangles(...)` | No — every triangle is convex |
| `CheckCollisionRecPoly(rec, points)` | Yes (`points`) |
| `CheckCollisionRecTriangle(rec, p1, p2, p3)` | No — every triangle is convex |
| `CheckCollisionCirclePoly(center, radius, points)` | **No — correct for any simple polygon** |
| `CheckCollisionCircleTriangle(center, radius, p1, p2, p3)` | No |
| `CheckCollisionCapsulePoly(capsuleStart, capsuleEnd, capsuleRadius, points)` | **No — correct for any simple polygon** |
| `CheckCollisionCapsuleRec(capsuleStart, capsuleEnd, capsuleRadius, rec)` | No |
| `CheckCollisionCapsuleTriangle(capsuleStart, capsuleEnd, capsuleRadius, p1, p2, p3)` | No |

The circle/capsule-vs-polygon checks skip SAT entirely (neither a circle nor a capsule's rounded ends have straight edges for SAT to use) and instead check: is either shape's reference point/segment *inside* the polygon (reusing `CheckCollisionPointPoly`), or does it come within radius of any polygon *edge*? That second check alone also catches a capsule passing all the way through solid material with both endpoints outside — entering a closed shape always crosses one of its edges.

**If you have a concave polygon, reach for the `Circle*`/`Capsule*` family, never `PolyPoly`/`RecPoly`/`RecTriangle`.** A worked example, from the test suite: an L-shaped polygon (a 10×10 square with its top-right quadrant notched out) has a point at `(7,7)` that sits inside the shape's *bounding box* but in the *removed* notch — outside the actual polygon. `CheckCollisionPointPoly`/`CirclePoly`/`CapsulePoly` all correctly report it as outside; a SAT-based check would not, since SAT assumes convexity it doesn't have here.

## Raycasts

Origin + direction (not a segment) — direction need not be pre-normalized, these normalize it internally so the returned distance is always in real world units. `t` is unclamped except `>= 0`.

| Method | What it does |
|---|---|
| `CheckCollisionRayCircle(origin, direction, center, radius, out hitPoint, out distance)` | `distance` is `0` if `origin` starts inside the circle. |
| `CheckCollisionRayRec(origin, direction, rec, out hitPoint, out distance)` | Slab method. `distance` is `0` if `origin` starts inside the rectangle. |
| `CheckCollisionRayLine(origin, direction, p1, p2, out hitPoint, out distance)` | Same parametric solve as `CheckCollisionLines`, except the ray's own parameter is only clamped `>= 0` (a ray has no far end) while the segment's stays clamped to `[0,1]`. |
| `CheckCollisionRayPoly(origin, direction, points, out hitPoint, out distance)` | Tests every edge, keeps the nearest crossing. Correct for any simple polygon, convex or not (unlike the SAT-based overlap checks above). |
| `CheckCollisionRayTriangle(origin, direction, p1, p2, p3, out hitPoint, out distance)` | `CheckCollisionRayPoly` on a 3-point span. |
| `CheckCollisionRayCapsule(origin, direction, capsuleStart, capsuleEnd, capsuleRadius, out hitPoint, out distance)` | Checked against the two end circles and the two straight sides; `distance` is `0` if `origin` starts inside. |

## Other

| Method | What it does |
|---|---|
| `GetCollisionRec(rec1, rec2)` | The overlapping rectangle of two rectangles (empty — `Width`/`Height` `0` — if they don't overlap). A thin wrapper over `Rectangle.Intersect`. |

## Testing

[`tests/MonoPrimitives.Tests/Collision2DTests.cs`](../tests/MonoPrimitives.Tests/Collision2DTests.cs) covers every method above, including the L-shaped concave-polygon fixture described above (exercised against `PointPoly`/`CirclePoly`/`CapsulePoly` specifically, including both the "capsule entirely inside the notch" and "capsule passing through solid material with both endpoints outside" cases), T-junction clamping for `CapsuleCapsule`, and a ray that must *not* hit a segment behind its own origin. Run with:

```bash
dotnet run --project tests/MonoPrimitives.Tests/MonoPrimitives.Tests.csproj
```

`examples/test/CollisionTest` is the interactive counterpart — every check above, one key each (`1`-`9`, `0`, `Q`, `E`, `R`, `T`, `Y`, `U`, `I`), two controllable points (mouse + WASD, swappable with Space), shapes turning red on overlap.

## See also

- [`Design/DECISIONS.md`](../Design/DECISIONS.md) — the audit that added the `Capsule*Poly` family and closed the test-coverage gap.
- [`Guide/Primitive2DBatch_Guide.md`](Primitive2DBatch_Guide.md) — drawing the shapes this class tests.
