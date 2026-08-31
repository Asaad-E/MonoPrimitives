using System;
using Microsoft.Xna.Framework;
using MonoPrimitives.Primitives2D;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-math checks for every <see cref="Collision2D"/> overlap/ray test — no GraphicsDevice needed.</summary>
    internal static class Collision2DTests
    {
        public static void Run(TestResults results)
        {
            results.Check("CheckCollisionCircles: overlapping", () =>
                Collision2D.CheckCollisionCircles(Vector2.Zero, 5f, new Vector2(8f, 0f), 5f) ? null : "expected true");

            results.Check("CheckCollisionCircles: far apart", () =>
                !Collision2D.CheckCollisionCircles(Vector2.Zero, 5f, new Vector2(20f, 0f), 5f) ? null : "expected false");

            results.Check("CheckCollisionCircleRec: circle inside rectangle", () =>
                Collision2D.CheckCollisionCircleRec(new Vector2(5f, 5f), 2f, new Rectangle(0, 0, 10, 10)) ? null : "expected true");

            results.Check("CheckCollisionCircleRec: circle far from rectangle", () =>
                !Collision2D.CheckCollisionCircleRec(new Vector2(100f, 100f), 2f, new Rectangle(0, 0, 10, 10)) ? null : "expected false");

            results.Check("CheckCollisionCircleLine: line through circle", () =>
                Collision2D.CheckCollisionCircleLine(Vector2.Zero, 3f, new Vector2(-10f, 0f), new Vector2(10f, 0f)) ? null : "expected true");

            results.Check("CheckCollisionCircleLine: line missing circle", () =>
                !Collision2D.CheckCollisionCircleLine(Vector2.Zero, 3f, new Vector2(-10f, 10f), new Vector2(10f, 10f)) ? null : "expected false");

            results.Check("CheckCollisionPointTriangle: point inside", () =>
                Collision2D.CheckCollisionPointTriangle(Vector2.Zero, new Vector2(0f, -10f), new Vector2(-10f, 10f), new Vector2(10f, 10f)) ? null : "expected true");

            results.Check("CheckCollisionPointTriangle: point outside", () =>
                !Collision2D.CheckCollisionPointTriangle(new Vector2(100f, 100f), new Vector2(0f, -10f), new Vector2(-10f, 10f), new Vector2(10f, 10f)) ? null : "expected false");

            results.Check("CheckCollisionPointPoly: point inside square", () =>
            {
                Span<Vector2> square = stackalloc Vector2[] { new(-5, -5), new(5, -5), new(5, 5), new(-5, 5) };
                return Collision2D.CheckCollisionPointPoly(Vector2.Zero, square) ? null : "expected true";
            });

            results.Check("CheckCollisionPointPoly: point outside square", () =>
            {
                Span<Vector2> square = stackalloc Vector2[] { new(-5, -5), new(5, -5), new(5, 5), new(-5, 5) };
                return !Collision2D.CheckCollisionPointPoly(new Vector2(100, 100), square) ? null : "expected false";
            });

            results.Check("CheckCollisionLines: intersecting segments", () =>
            {
                bool hit = Collision2D.CheckCollisionLines(new Vector2(-5, 0), new Vector2(5, 0), new Vector2(0, -5), new Vector2(0, 5), out Vector2 point);
                if (!hit) return "expected a hit";
                return Vector2.Distance(point, Vector2.Zero) < 0.01f ? null : $"expected intersection near origin, got {point}";
            });

            results.Check("CheckCollisionLines: parallel segments", () =>
                !Collision2D.CheckCollisionLines(new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 5), new Vector2(10, 5), out _) ? null : "expected false");

            results.Check("CheckCollisionRayCircle: ray hits circle", () =>
            {
                bool hit = Collision2D.CheckCollisionRayCircle(new Vector2(-20, 0), Vector2.UnitX, Vector2.Zero, 5f, out Vector2 hitPoint, out float distance);
                if (!hit) return "expected a hit";
                if (MathF.Abs(hitPoint.X - -5f) > 0.01f) return $"expected hitPoint.X near -5, got {hitPoint.X}";
                return MathF.Abs(distance - 15f) > 0.01f ? $"expected distance near 15, got {distance}" : null;
            });

            results.Check("CheckCollisionRayCircle: ray misses circle", () =>
                !Collision2D.CheckCollisionRayCircle(new Vector2(-20, 20), Vector2.UnitX, Vector2.Zero, 5f, out _, out _) ? null : "expected false");

            results.Check("CheckCollisionRayRec: ray hits rectangle", () =>
                Collision2D.CheckCollisionRayRec(new Vector2(-20, 5), Vector2.UnitX, new Rectangle(0, 0, 10, 10), out _, out _) ? null : "expected true");

            results.Check("CheckCollisionRayRec: ray misses rectangle", () =>
                !Collision2D.CheckCollisionRayRec(new Vector2(-20, 50), Vector2.UnitX, new Rectangle(0, 0, 10, 10), out _, out _) ? null : "expected false");

            results.Check("CheckCollisionRecs / CheckCollisionPointRec / CheckCollisionPointCircle / GetCollisionRec", () =>
            {
                if (!Collision2D.CheckCollisionRecs(new Rectangle(0, 0, 10, 10), new Rectangle(5, 5, 10, 10))) return "CheckCollisionRecs: expected overlapping rects to collide";
                if (Collision2D.CheckCollisionRecs(new Rectangle(0, 0, 10, 10), new Rectangle(100, 100, 10, 10))) return "CheckCollisionRecs: expected far-apart rects to not collide";
                if (!Collision2D.CheckCollisionPointRec(new Vector2(5, 5), new Rectangle(0, 0, 10, 10))) return "CheckCollisionPointRec: expected point inside";
                if (Collision2D.CheckCollisionPointRec(new Vector2(50, 50), new Rectangle(0, 0, 10, 10))) return "CheckCollisionPointRec: expected point outside";
                if (!Collision2D.CheckCollisionPointCircle(new Vector2(1, 0), Vector2.Zero, 5f)) return "CheckCollisionPointCircle: expected point inside";
                if (Collision2D.CheckCollisionPointCircle(new Vector2(50, 0), Vector2.Zero, 5f)) return "CheckCollisionPointCircle: expected point outside";
                Rectangle overlap = Collision2D.GetCollisionRec(new Rectangle(0, 0, 10, 10), new Rectangle(5, 5, 10, 10));
                if (overlap != new Rectangle(5, 5, 5, 5)) return $"GetCollisionRec: expected (5,5,5,5), got {overlap}";
                return null;
            });

            results.Check("CheckCollisionCircleCapsule: near and far", () =>
            {
                if (!Collision2D.CheckCollisionCircleCapsule(new Vector2(5, 2), 1f, new Vector2(0, 0), new Vector2(10, 0), 1f)) return "expected a hit (circle near the capsule's axis)";
                if (Collision2D.CheckCollisionCircleCapsule(new Vector2(5, 20), 1f, new Vector2(0, 0), new Vector2(10, 0), 1f)) return "expected no hit (circle far above)";
                return null;
            });

            results.Check("CheckCollisionCapsules: crossing, T-junction clamping, and far apart", () =>
            {
                // Two capsules crossing like a plus sign -- their axes intersect, so any positive radii overlap.
                if (!Collision2D.CheckCollisionCapsules(new Vector2(-5, 0), new Vector2(5, 0), 1f, new Vector2(0, -5), new Vector2(0, 5), 1f))
                    return "expected crossing capsules to collide";
                // T-junction: b's segment endpoint sits right next to a's segment, closest point must clamp correctly.
                if (!Collision2D.CheckCollisionCapsules(new Vector2(0, 0), new Vector2(10, 0), 1f, new Vector2(5, 1.5f), new Vector2(5, 10), 1f))
                    return "expected a T-junction within combined radius to collide";
                if (Collision2D.CheckCollisionCapsules(new Vector2(0, 0), new Vector2(10, 0), 1f, new Vector2(5, 50), new Vector2(5, 60), 1f))
                    return "expected far-apart capsules to not collide";
                return null;
            });

            results.Check("CheckCollisionRayLine: ray hits a crossing segment, but not one behind the ray's origin", () =>
            {
                bool hit = Collision2D.CheckCollisionRayLine(Vector2.Zero, Vector2.UnitX, new Vector2(5, -5), new Vector2(5, 5), out Vector2 point, out float dist);
                if (!hit) return "expected a hit";
                if (Vector2.Distance(point, new Vector2(5, 0)) > 0.01f) return $"expected hit point near (5,0), got {point}";
                if (MathF.Abs(dist - 5f) > 0.01f) return $"expected distance 5, got {dist}";
                if (Collision2D.CheckCollisionRayLine(Vector2.Zero, Vector2.UnitX, new Vector2(-5, -5), new Vector2(-5, 5), out _, out _))
                    return "expected no hit for a segment entirely behind the ray's origin";
                return null;
            });

            results.Check("CheckCollisionRayPoly: ray hits the near edge of a concave (star) polygon, not the far one", () =>
            {
                Span<Vector2> square = stackalloc Vector2[] { new(5, -5), new(15, -5), new(15, 5), new(5, 5) };
                bool hit = Collision2D.CheckCollisionRayPoly(Vector2.Zero, Vector2.UnitX, square, out Vector2 point, out float dist);
                if (!hit) return "expected a hit";
                if (Vector2.Distance(point, new Vector2(5, 0)) > 0.01f) return $"expected the nearest edge crossing near (5,0), got {point}";
                if (MathF.Abs(dist - 5f) > 0.01f) return $"expected distance 5, got {dist}";
                if (Collision2D.CheckCollisionRayPoly(new Vector2(-20, 20), Vector2.UnitX, square, out _, out _))
                    return "expected no hit for a ray passing above the polygon";
                return null;
            });

            results.Check("CheckCollisionRayTriangle: ray hits triangle (see CheckCollisionRayPoly)", () =>
            {
                if (!Collision2D.CheckCollisionRayTriangle(Vector2.Zero, Vector2.UnitX, new Vector2(5, -5), new Vector2(5, 5), new Vector2(15, 0), out _, out float dist))
                    return "expected a hit";
                if (MathF.Abs(dist - 5f) > 0.01f) return $"expected distance 5, got {dist}";
                return null;
            });

            results.Check("CheckCollisionRayCapsule: end caps, side, starts-inside, and a clean miss", () =>
            {
                Vector2 capStart = new(0, 0), capEnd = new(20, 0);
                const float radius = 3f;

                // Ray along the capsule's own axis should hit the near end cap, not the flat side.
                bool hitAxis = Collision2D.CheckCollisionRayCapsule(new Vector2(-20, 0), Vector2.UnitX, capStart, capEnd, radius, out Vector2 axisPoint, out float axisDist);
                if (!hitAxis) return "expected a hit along the capsule's axis";
                if (MathF.Abs(axisDist - 17f) > 0.01f) return $"expected distance 17 (hits the near end cap's front), got {axisDist}";

                // Perpendicular ray into the middle of the capsule's straight side.
                bool hitSide = Collision2D.CheckCollisionRayCapsule(new Vector2(10, -20), Vector2.UnitY, capStart, capEnd, radius, out Vector2 sidePoint, out float sideDist);
                if (!hitSide) return "expected a hit on the capsule's straight side";
                if (Vector2.Distance(sidePoint, new Vector2(10, -radius)) > 0.01f) return $"expected the side hit near (10,{-radius}), got {sidePoint}";
                if (MathF.Abs(sideDist - (20f - radius)) > 0.01f) return $"expected distance {20f - radius}, got {sideDist}";

                // Origin already inside the capsule: distance 0, hitPoint == origin.
                bool hitInside = Collision2D.CheckCollisionRayCapsule(new Vector2(10, 0), Vector2.UnitX, capStart, capEnd, radius, out Vector2 insidePoint, out float insideDist);
                if (!hitInside || insideDist != 0f || insidePoint != new Vector2(10, 0))
                    return $"expected an inside-start hit at distance 0, got hit={hitInside} dist={insideDist} point={insidePoint}";

                if (Collision2D.CheckCollisionRayCapsule(new Vector2(-20, 20), Vector2.UnitX, capStart, capEnd, radius, out _, out _))
                    return "expected no hit for a ray passing well above the capsule";
                return null;
            });

            results.Check("CheckCollisionPolys / Triangles / RecPoly / RecTriangle (SAT, convex shapes)", () =>
            {
                Span<Vector2> squareA = stackalloc Vector2[] { new(0, 0), new(10, 0), new(10, 10), new(0, 10) };
                Span<Vector2> squareB = stackalloc Vector2[] { new(5, 5), new(15, 5), new(15, 15), new(5, 15) };
                Span<Vector2> squareFar = stackalloc Vector2[] { new(100, 100), new(110, 100), new(110, 110), new(100, 110) };
                if (!Collision2D.CheckCollisionPolys(squareA, squareB)) return "CheckCollisionPolys: expected overlapping squares to collide";
                if (Collision2D.CheckCollisionPolys(squareA, squareFar)) return "CheckCollisionPolys: expected far squares to not collide";

                if (!Collision2D.CheckCollisionTriangles(new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 10), new Vector2(2, 2), new Vector2(12, 2), new Vector2(2, 12)))
                    return "CheckCollisionTriangles: expected overlapping triangles to collide";
                if (Collision2D.CheckCollisionTriangles(new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 10), new Vector2(100, 100), new Vector2(110, 100), new Vector2(100, 110)))
                    return "CheckCollisionTriangles: expected far triangles to not collide";

                if (!Collision2D.CheckCollisionRecPoly(new Rectangle(0, 0, 10, 10), squareB)) return "CheckCollisionRecPoly: expected overlap";
                if (Collision2D.CheckCollisionRecPoly(new Rectangle(0, 0, 10, 10), squareFar)) return "CheckCollisionRecPoly: expected no overlap";

                if (!Collision2D.CheckCollisionRecTriangle(new Rectangle(0, 0, 10, 10), new Vector2(5, 5), new Vector2(20, 5), new Vector2(5, 20))) return "CheckCollisionRecTriangle: expected overlap";
                if (Collision2D.CheckCollisionRecTriangle(new Rectangle(0, 0, 10, 10), new Vector2(100, 100), new Vector2(120, 100), new Vector2(100, 120))) return "CheckCollisionRecTriangle: expected no overlap";
                return null;
            });

            // L-shaped concave polygon: the union of [0,10]x[0,5] and [0,5]x[0,10] -- i.e. a 10x10
            // square with its top-right [5,10]x[5,10] quadrant removed. (7,7) sits exactly in that
            // removed notch: inside the shape's convex hull/bounding box, but outside the actual
            // polygon. A SAT-based (convex-only) check would get this wrong; the non-SAT
            // point/circle/capsule-vs-poly checks below must not.
            Span<Vector2> LShape() => new Vector2[] { new(0, 0), new(10, 0), new(10, 5), new(5, 5), new(5, 10), new(0, 10) };

            results.Check("CheckCollisionPointPoly is correct on a concave (L-shaped) polygon", () =>
            {
                if (Collision2D.CheckCollisionPointPoly(new Vector2(7, 7), LShape())) return "(7,7) is in the removed notch, expected outside";
                if (!Collision2D.CheckCollisionPointPoly(new Vector2(2, 8), LShape())) return "(2,8) is in the left bar, expected inside";
                if (!Collision2D.CheckCollisionPointPoly(new Vector2(8, 2), LShape())) return "(8,2) is in the bottom bar, expected inside";
                return null;
            });

            results.Check("CheckCollisionCirclePoly is correct on a concave polygon (a circle sitting in the notch doesn't collide)", () =>
            {
                if (Collision2D.CheckCollisionCirclePoly(new Vector2(7, 7), 1f, LShape())) return "a small circle centered in the notch should not collide";
                if (!Collision2D.CheckCollisionCirclePoly(new Vector2(2, 8), 1f, LShape())) return "a small circle centered inside the left bar should collide";
                return null;
            });

            results.Check("CheckCollisionCapsulePoly/CapsuleRec/CapsuleTriangle -- concave-correct, and a capsule passing fully through solid material", () =>
            {
                // Entirely within the notch (empty space) -- must not collide even though both
                // endpoints sit within the shape's overall bounding box.
                if (Collision2D.CheckCollisionCapsulePoly(new Vector2(7, 7), new Vector2(9, 9), 0.5f, LShape()))
                    return "a capsule entirely inside the concave notch should not collide";

                // Passes through the solid left bar (x=3 is inside the L for the full y range 0-10),
                // with both endpoints OUTSIDE the polygon (y=-5 and y=15) -- exercises the
                // "neither endpoint inside, but crosses an edge" path specifically.
                if (!Collision2D.CheckCollisionCapsulePoly(new Vector2(3, -5), new Vector2(3, 15), 0.5f, LShape()))
                    return "a capsule passing through solid material (endpoints outside on both ends) should collide";

                if (!Collision2D.CheckCollisionCapsuleRec(new Vector2(-5, 5), new Vector2(5, 5), 1f, new Rectangle(0, 0, 10, 10)))
                    return "CheckCollisionCapsuleRec: expected a capsule entering a rectangle to collide";
                if (Collision2D.CheckCollisionCapsuleRec(new Vector2(-50, 5), new Vector2(-40, 5), 1f, new Rectangle(0, 0, 10, 10)))
                    return "CheckCollisionCapsuleRec: expected a far capsule to not collide";

                if (!Collision2D.CheckCollisionCapsuleTriangle(new Vector2(-5, 2), new Vector2(5, 2), 1f, new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 10)))
                    return "CheckCollisionCapsuleTriangle: expected a capsule entering a triangle to collide";
                if (Collision2D.CheckCollisionCapsuleTriangle(new Vector2(-50, 2), new Vector2(-40, 2), 1f, new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 10)))
                    return "CheckCollisionCapsuleTriangle: expected a far capsule to not collide";

                return null;
            });
        }
    }
}
