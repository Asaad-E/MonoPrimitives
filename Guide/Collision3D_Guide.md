# Collision3D — Guide

`Collision3D` (namespace `MonoPrimitives.Primitives3D`, file [`src/3D/Collision3D.cs`](../src/3D/Collision3D.cs)) is a static class of pure geometry overlap tests and raycasts — sphere, box, capsule, plane, triangle, quad. Detection only: it tells you *whether* (and where) two things overlap, not how to separate them afterward — that's a per-game decision.

Sphere/box overlap and ray-vs-sphere/box are thin wrappers over MonoGame's own `BoundingSphere`/`BoundingBox`/`Ray` methods, kept here for a consistent name alongside everything else and for `RayCollision3D`'s richer result (point + normal, not just a distance). Capsule and triangle/quad raycasts are the genuinely new part — shapes MonoGame has no collision support for at all.

## Quick start

```csharp
using MonoPrimitives.Primitives3D;

if (Collision3D.CheckCollisionSpheres(playerPos, playerRadius, enemyPos, enemyRadius))
{
    // your own resolution logic goes here
}

RayCollision3D hit = Collision3D.GetRayCollisionBox(pickRay, targetBox);
if (hit.Hit)
{
    // hit.Point, hit.Normal, hit.Distance
}
```

## `RayCollision3D`

Every raycast in this class returns this struct instead of MonoGame's own mix of `bool`/`float?` returns. You normally only ever receive one back from a raycast method — the public `RayCollision3D(bool hit, float distance, Vector3 point, Vector3 normal)` constructor exists mainly for building your own `default`-like "no hit" or synthetic result if you're writing code that mixes hits from several sources.

| Field | What it holds |
|---|---|
| `Hit` | Whether the ray actually hit anything — always check this first; the other three fields are meaningless when it's `false`. |
| `Distance` | Distance from the ray's origin to `Point`. |
| `Point` | World-space point where the ray hit. |
| `Normal` | Surface normal at `Point`, pointing back toward the ray's origin. |

## Overlap tests

| Method | What it does |
|---|---|
| `CheckCollisionSpheres(center1, radius1, center2, radius2)` | Sphere vs sphere. |
| `CheckCollisionBoxes(box1, box2)` | Box vs box (axis-aligned). |
| `CheckCollisionBoxSphere(box, center, radius)` | Box vs sphere. |
| `CheckCollisionCapsules(start1, end1, radius1, start2, end2, radius2)` | Capsule vs capsule — distance between the two axis *segments* (not points), via the standard closest-point-between-segments algorithm, correctly handling every degenerate case (either/both collapsed to a point, parallel segments) without branching on them. |
| `CheckCollisionCapsuleSphere(capStart, capEnd, capRadius, sphereCenter, sphereRadius)` | Capsule vs sphere. |
| `CheckCollisionCapsuleBox(start, end, radius, box)` | Capsule vs axis-aligned box — shortest distance between the capsule's own segment and the box, within `radius`. |

A capsule with `start == end` behaves exactly like a sphere in every one of the above — no special-casing needed at the call site.

## Raycasts

| Method | What it does |
|---|---|
| `GetRayCollisionSphere(ray, center, radius)` | Ray vs sphere. |
| `GetRayCollisionBox(ray, box)` | Ray vs axis-aligned box; `Normal` is whichever of the box's 6 faces was actually hit. |
| `GetRayCollisionPlane(ray, planePoint, planeNormal)` | Ray vs an infinite plane — ground planes, mirrors, cut planes. |
| `GetRayCollisionTriangle(ray, p1, p2, p3)` | Ray vs a single triangle — mesh/terrain picking against triangles you already have on hand (e.g. one cell of a heightmap). |
| `GetRayCollisionQuad(ray, p1, p2, p3, p4)` | Ray vs a planar quad, given its 4 corners in order (same winding `FillPlane` uses) — tested as two triangles internally. |
| `GetRayCollisionCapsule(ray, start, end, radius)` | Ray vs capsule — the cylindrical body plus both hemispherical end caps, whichever is nearest. A degenerate (`start == end`) capsule falls back to `GetRayCollisionSphere`. |

## Testing

[`tests/MonoPrimitives.Tests/Collision3DTests.cs`](../tests/MonoPrimitives.Tests/Collision3DTests.cs) covers every method above: each overlap test's overlapping/far-apart cases (plus degenerate-capsule and exact-touching-boundary edge cases for `CheckCollisionCapsuleBox`), and each raycast's hit/miss cases (including a ray behind its own origin, a ray parallel to a plane/triangle, and `GetRayCollisionCapsule`'s body-hit vs. cap-hit vs. degenerate-to-sphere cases). Run with:

```bash
dotnet run --project tests/MonoPrimitives.Tests/MonoPrimitives.Tests.csproj
```

## See also

- [`Guide/Primitive3DBatch_Guide.md`](Primitive3DBatch_Guide.md) — drawing the shapes this class tests.
- [`Guide/Collision2D_Guide.md`](Collision2D_Guide.md) — the 2D counterpart; returns `bool` + `out` params instead of a `RayCollision3D` struct, since a 2D raycast is almost always an immediate yes/no gate rather than something you compare across several candidate hits.
