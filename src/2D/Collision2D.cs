#nullable enable

using System;
using Microsoft.Xna.Framework;

namespace MonoPrimitives.Primitives2D
{
    /// <summary>Pure geometry overlap/intersection tests (circle, rectangle, triangle, polygon, line).</summary>
    /// <remarks>Static utilities, not <see cref="Primitive2DBatch"/> methods — plain math you can call any time, not just mid-draw. Useful for hit detection, platformer collision, or neighbor/separation checks in a simulation.</remarks>
    public static class Collision2D
    {
        /// <summary>Rectangle vs rectangle overlap. Thin wrapper over <see cref="Rectangle.Intersects(Rectangle)"/>.</summary>
        public static bool CheckCollisionRecs(Rectangle rec1, Rectangle rec2) => rec1.Intersects(rec2);

        /// <summary>Circle vs circle overlap.</summary>
        public static bool CheckCollisionCircles(Vector2 center1, float radius1, Vector2 center2, float radius2)
        {
            float radiusSum = radius1 + radius2;
            return Vector2.DistanceSquared(center1, center2) <= radiusSum * radiusSum;
        }

        /// <summary>Circle vs rectangle overlap.</summary>
        public static bool CheckCollisionCircleRec(Vector2 center, float radius, Rectangle rec)
        {
            float closestX = Math.Clamp(center.X, rec.Left, rec.Right);
            float closestY = Math.Clamp(center.Y, rec.Top, rec.Bottom);
            float dx = center.X - closestX, dy = center.Y - closestY;
            return dx * dx + dy * dy <= radius * radius;
        }

        /// <summary>Circle vs line segment overlap.</summary>
        public static bool CheckCollisionCircleLine(Vector2 center, float radius, Vector2 p1, Vector2 p2)
            => CheckCollisionPointLine(center, p1, p2, radius);

        /// <summary>
        /// Circle vs capsule overlap — a capsule is just a thick line segment, so this is exactly
        /// <see cref="CheckCollisionCircleLine"/> with the two radii combined into one threshold
        /// instead of just the circle's own.
        /// </summary>
        public static bool CheckCollisionCircleCapsule(Vector2 circleCenter, float circleRadius, Vector2 capsuleStart, Vector2 capsuleEnd, float capsuleRadius)
            => CheckCollisionPointLine(circleCenter, capsuleStart, capsuleEnd, circleRadius + capsuleRadius);

        /// <summary>Point inside (or on the edge of) a rectangle. Thin wrapper over <see cref="Rectangle.Contains(Point)"/>.</summary>
        public static bool CheckCollisionPointRec(Vector2 point, Rectangle rec)
            => point.X >= rec.Left && point.X <= rec.Right && point.Y >= rec.Top && point.Y <= rec.Bottom;

        /// <summary>Point inside (or on the edge of) a circle.</summary>
        public static bool CheckCollisionPointCircle(Vector2 point, Vector2 center, float radius)
            => Vector2.DistanceSquared(point, center) <= radius * radius;

        /// <summary>Point inside a triangle (any winding order).</summary>
        public static bool CheckCollisionPointTriangle(Vector2 point, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float d1 = Cross(point, p1, p2);
            float d2 = Cross(point, p2, p3);
            float d3 = Cross(point, p3, p1);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static float Cross(in Vector2 p, in Vector2 a, in Vector2 b) => (p.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (p.Y - b.Y);

        /// <summary>Point within <paramref name="threshold"/> world units of a line segment.</summary>
        public static bool CheckCollisionPointLine(Vector2 point, Vector2 p1, Vector2 p2, float threshold = 1f)
        {
            Vector2 d = p2 - p1;
            float lenSq = d.LengthSquared();
            float distSq;
            if (lenSq < 1e-12f)
            {
                distSq = Vector2.DistanceSquared(point, p1);
            }
            else
            {
                float t = Math.Clamp(Vector2.Dot(point - p1, d) / lenSq, 0f, 1f);
                distSq = Vector2.DistanceSquared(point, p1 + d * t);
            }
            return distSq <= threshold * threshold;
        }

        /// <summary>Point inside an arbitrary (possibly non-convex) polygon. Correct for any simple polygon, not just the convex/star-shaped input this library's own <c>FillPolygon</c> assumes.</summary>
        public static bool CheckCollisionPointPoly(Vector2 point, ReadOnlySpan<Vector2> points)
        {
            bool inside = false;
            int n = points.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 pi = points[i], pj = points[j];
                if (pi.Y > point.Y != pj.Y > point.Y &&
                    point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>Line segment vs line segment intersection, returning the crossing point via <paramref name="collisionPoint"/> when they actually cross (not just their infinite extensions).</summary>
        public static bool CheckCollisionLines(Vector2 startPos1, Vector2 endPos1, Vector2 startPos2, Vector2 endPos2, out Vector2 collisionPoint)
        {
            collisionPoint = default;
            float d1x = endPos1.X - startPos1.X, d1y = endPos1.Y - startPos1.Y;
            float d2x = endPos2.X - startPos2.X, d2y = endPos2.Y - startPos2.Y;
            float denom = d1x * d2y - d1y * d2x;
            if (MathF.Abs(denom) < 1e-12f)
                return false; // parallel (or one segment has zero length)

            float dx = startPos2.X - startPos1.X, dy = startPos2.Y - startPos1.Y;
            float t = (dx * d2y - dy * d2x) / denom;
            float u = (dx * d1y - dy * d1x) / denom;
            if (t < 0f || t > 1f || u < 0f || u > 1f)
                return false;

            collisionPoint = new Vector2(startPos1.X + t * d1x, startPos1.Y + t * d1y);
            return true;
        }

        /// <summary>The overlapping rectangle of two rectangles (empty — <c>Width</c>/<c>Height</c> 0 — if they don't overlap). Thin wrapper over <see cref="Rectangle.Intersect(Rectangle,Rectangle)"/>.</summary>
        public static Rectangle GetCollisionRec(Rectangle rec1, Rectangle rec2) => Rectangle.Intersect(rec1, rec2);

        // =====================================================================
        // POLYGON / MIXED-SHAPE OVERLAPS
        //
        // CheckCollisionPolyPoly/RecPoly/RecTriangle/TriangleTriangle all go through the same
        // Separating Axis Theorem (SAT) core, which REQUIRES both shapes to be convex to give a
        // correct answer (a rectangle and a triangle always are; an arbitrary "poly" span here is
        // NOT checked for convexity — pass a convex one). CheckCollisionCirclePoly/CircleTriangle
        // and CheckCollisionCapsulePoly/CapsuleRec/CapsuleTriangle do NOT go through SAT (neither a
        // circle nor a capsule's rounded ends have straight edges for it to use) and work correctly
        // for ANY simple polygon, convex or not — same generality as CheckCollisionPointPoly above.
        // =====================================================================

        /// <summary>Convex polygon vs convex polygon overlap. Requires both polygons to be convex.</summary>
        public static bool CheckCollisionPolyPoly(ReadOnlySpan<Vector2> poly1, ReadOnlySpan<Vector2> poly2)
        {
            if (poly1.Length < 3 || poly2.Length < 3) return false;
            return !HasSeparatingAxis(poly1, poly2) && !HasSeparatingAxis(poly2, poly1);
        }

        /// <summary>Triangle vs triangle overlap (every triangle is convex, so this is exactly <see cref="CheckCollisionPolyPoly"/> on two 3-point spans).</summary>
        public static bool CheckCollisionTriangleTriangle(Vector2 a1, Vector2 a2, Vector2 a3, Vector2 b1, Vector2 b2, Vector2 b3)
            => CheckCollisionPolyPoly([a1, a2, a3], [b1, b2, b3]);

        /// <summary>Rectangle vs convex polygon overlap (SAT — see <see cref="CheckCollisionPolyPoly"/>).</summary>
        public static bool CheckCollisionRecPoly(Rectangle rec, ReadOnlySpan<Vector2> points)
        {
            Span<Vector2> rectPts = stackalloc Vector2[4] { new(rec.Left, rec.Top), new(rec.Right, rec.Top), new(rec.Right, rec.Bottom), new(rec.Left, rec.Bottom) };
            return CheckCollisionPolyPoly(rectPts, points);
        }

        /// <summary>Rectangle vs triangle overlap (SAT — see <see cref="CheckCollisionPolyPoly"/>).</summary>
        public static bool CheckCollisionRecTriangle(Rectangle rec, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            Span<Vector2> rectPts = stackalloc Vector2[4] { new(rec.Left, rec.Top), new(rec.Right, rec.Top), new(rec.Right, rec.Bottom), new(rec.Left, rec.Bottom) };
            return CheckCollisionPolyPoly(rectPts, [p1, p2, p3]);
        }

        // Separating Axis Theorem: tries each polygon's own edge normals in turn as a candidate
        // separating axis, projecting both polygons onto it and looking for a gap. No separating
        // axis on either polygon's edges means the polygons overlap.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static bool HasSeparatingAxis(ReadOnlySpan<Vector2> a, ReadOnlySpan<Vector2> b)
        {
            int n = a.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 edge = a[(i + 1) % n] - a[i];
                Vector2 axis = new(-edge.Y, edge.X); // perpendicular; doesn't need normalizing to compare projections

                ProjectPolygon(a, axis, out float minA, out float maxA);
                ProjectPolygon(b, axis, out float minB, out float maxB);
                if (maxA < minB || maxB < minA) return true; // a gap on this axis is enough to prove no overlap
            }
            return false;
        }

        private static void ProjectPolygon(ReadOnlySpan<Vector2> poly, Vector2 axis, out float min, out float max)
        {
            min = max = Vector2.Dot(poly[0], axis);
            for (int i = 1; i < poly.Length; i++)
            {
                float p = Vector2.Dot(poly[i], axis);
                if (p < min) min = p;
                if (p > max) max = p;
            }
        }

        /// <summary>Circle vs arbitrary (possibly non-convex) polygon overlap.</summary>
        /// <remarks>Correct for any simple polygon, unlike the SAT-based checks above. True if the circle's center is inside the polygon, or within <paramref name="radius"/> of any edge.</remarks>
        public static bool CheckCollisionCirclePoly(Vector2 center, float radius, ReadOnlySpan<Vector2> points)
        {
            if (points.Length < 2) return false;
            if (CheckCollisionPointPoly(center, points)) return true;

            int n = points.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
                if (CheckCollisionPointLine(center, points[j], points[i], radius))
                    return true;
            return false;
        }

        /// <summary>Circle vs triangle overlap (see <see cref="CheckCollisionCirclePoly"/>).</summary>
        public static bool CheckCollisionCircleTriangle(Vector2 center, float radius, Vector2 p1, Vector2 p2, Vector2 p3)
            => CheckCollisionCirclePoly(center, radius, [p1, p2, p3]);

        /// <summary>Capsule vs arbitrary (possibly non-convex) polygon overlap.</summary>
        /// <remarks>Correct for any simple polygon (not SAT-based, so convexity isn't required). True if either capsule endpoint is inside the polygon, or the capsule's axis segment comes within <paramref name="capsuleRadius"/> of any polygon edge.</remarks>
        public static bool CheckCollisionCapsulePoly(Vector2 capsuleStart, Vector2 capsuleEnd, float capsuleRadius, ReadOnlySpan<Vector2> points)
        {
            if (points.Length < 2) return false;
            if (CheckCollisionPointPoly(capsuleStart, points) || CheckCollisionPointPoly(capsuleEnd, points)) return true;

            int n = points.Length;
            float radiusSq = capsuleRadius * capsuleRadius;
            for (int i = 0, j = n - 1; i < n; j = i++)
                if (SegmentSegmentDistanceSquared(capsuleStart, capsuleEnd, points[j], points[i]) <= radiusSq)
                    return true;
            return false;
        }

        /// <summary>Capsule vs rectangle overlap (see <see cref="CheckCollisionCapsulePoly"/>).</summary>
        public static bool CheckCollisionCapsuleRec(Vector2 capsuleStart, Vector2 capsuleEnd, float capsuleRadius, Rectangle rec)
        {
            Span<Vector2> rectPts = stackalloc Vector2[4] { new(rec.Left, rec.Top), new(rec.Right, rec.Top), new(rec.Right, rec.Bottom), new(rec.Left, rec.Bottom) };
            return CheckCollisionCapsulePoly(capsuleStart, capsuleEnd, capsuleRadius, rectPts);
        }

        /// <summary>Capsule vs triangle overlap (see <see cref="CheckCollisionCapsulePoly"/>).</summary>
        public static bool CheckCollisionCapsuleTriangle(Vector2 capsuleStart, Vector2 capsuleEnd, float capsuleRadius, Vector2 p1, Vector2 p2, Vector2 p3)
            => CheckCollisionCapsulePoly(capsuleStart, capsuleEnd, capsuleRadius, [p1, p2, p3]);

        /// <summary>Capsule vs capsule overlap: distance between their two axis segments (not points), compared against the sum of both radii.</summary>
        public static bool CheckCollisionCapsuleCapsule(Vector2 aStart, Vector2 aEnd, float aRadius, Vector2 bStart, Vector2 bEnd, float bRadius)
        {
            float radiusSum = aRadius + bRadius;
            return SegmentSegmentDistanceSquared(aStart, aEnd, bStart, bEnd) <= radiusSum * radiusSum;
        }

        // Standard closest-point-between-segments algorithm (Ericson, "Real-Time Collision Detection"),
        // handling every degenerate case (either/both segments collapsed to a point, parallel
        // segments) without branching on them explicitly. s/t are each segment's parametric
        // closest-point position, clamped to [0,1] -- always on the segment, never the infinite line.
        private static float SegmentSegmentDistanceSquared(Vector2 p1, Vector2 q1, Vector2 p2, Vector2 q2)
        {
            Vector2 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector2.Dot(d1, d1), e = Vector2.Dot(d2, d2), f = Vector2.Dot(d2, r);

            float s, t;
            if (a <= 1e-12f && e <= 1e-12f)
            {
                return Vector2.DistanceSquared(p1, p2); // both segments are points
            }
            if (a <= 1e-12f)
            {
                s = 0f;
                t = Math.Clamp(f / e, 0f, 1f);
            }
            else
            {
                float c = Vector2.Dot(d1, r);
                if (e <= 1e-12f)
                {
                    t = 0f;
                    s = Math.Clamp(-c / a, 0f, 1f);
                }
                else
                {
                    float b = Vector2.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > 1e-12f ? Math.Clamp((b * f - c * e) / denom, 0f, 1f) : 0f;
                    t = (b * s + f) / e;

                    if (t < 0f) { t = 0f; s = Math.Clamp(-c / a, 0f, 1f); }
                    else if (t > 1f) { t = 1f; s = Math.Clamp((b - c) / a, 0f, 1f); }
                }
            }

            Vector2 closest1 = p1 + d1 * s;
            Vector2 closest2 = p2 + d2 * t;
            return Vector2.DistanceSquared(closest1, closest2);
        }

        // =====================================================================
        // RAYCASTS — the 2D counterpart to Camera3D's GetScreenToWorldRay and
        // MonoGame's own 3D Ray.Intersects: mouse-picking, line-of-sight,
        // projectile/sensor checks. origin+direction, not a segment — direction
        // need not be normalized (these normalize it internally so the returned
        // distance is always in real world units); t is unclamped except >= 0.
        // =====================================================================

        /// <summary>Ray vs circle. <paramref name="distance"/> is the distance to the nearest intersection in front of the ray (0 if <paramref name="origin"/> is already inside the circle).</summary>
        public static bool CheckCollisionRayCircle(Vector2 origin, Vector2 direction, Vector2 center, float radius, out Vector2 hitPoint, out float distance)
        {
            hitPoint = default; distance = 0f;
            float dirLenSq = direction.LengthSquared();
            if (dirLenSq < 1e-12f) return false;
            Vector2 dir = direction / MathF.Sqrt(dirLenSq);

            Vector2 oc = origin - center;
            float b = Vector2.Dot(oc, dir);
            float c = oc.LengthSquared() - radius * radius;
            float discriminant = b * b - c;
            if (discriminant < 0f) return false;

            float sqrtD = MathF.Sqrt(discriminant);
            float t = -b - sqrtD;
            if (t < 0f) t = -b + sqrtD; // origin is inside the circle: report the exit point's t (still >= 0 since c <= 0 there)
            if (t < 0f) return false;

            distance = t;
            hitPoint = origin + dir * t;
            return true;
        }

        /// <summary>Ray vs axis-aligned rectangle. <paramref name="distance"/> is 0 if <paramref name="origin"/> starts inside <paramref name="rec"/>.</summary>
        public static bool CheckCollisionRayRec(Vector2 origin, Vector2 direction, Rectangle rec, out Vector2 hitPoint, out float distance)
        {
            hitPoint = default; distance = 0f;
            float dirLenSq = direction.LengthSquared();
            if (dirLenSq < 1e-12f) return false;
            Vector2 dir = direction / MathF.Sqrt(dirLenSq);

            float tMin = 0f, tMax = float.MaxValue;

            if (MathF.Abs(dir.X) < 1e-12f)
            {
                if (origin.X < rec.Left || origin.X > rec.Right) return false;
            }
            else
            {
                float t1 = (rec.Left - origin.X) / dir.X;
                float t2 = (rec.Right - origin.X) / dir.X;
                if (t1 > t2) (t1, t2) = (t2, t1);
                tMin = MathF.Max(tMin, t1);
                tMax = MathF.Min(tMax, t2);
                if (tMin > tMax) return false;
            }

            if (MathF.Abs(dir.Y) < 1e-12f)
            {
                if (origin.Y < rec.Top || origin.Y > rec.Bottom) return false;
            }
            else
            {
                float t1 = (rec.Top - origin.Y) / dir.Y;
                float t2 = (rec.Bottom - origin.Y) / dir.Y;
                if (t1 > t2) (t1, t2) = (t2, t1);
                tMin = MathF.Max(tMin, t1);
                tMax = MathF.Min(tMax, t2);
                if (tMin > tMax) return false;
            }

            distance = tMin;
            hitPoint = origin + dir * tMin;
            return true;
        }

        /// <summary>
        /// Ray vs line segment — same parametric solve as <see cref="CheckCollisionLines"/>,
        /// with the ray's own parameter only clamped to <c>t &gt;= 0</c> (not <c>&lt;= 1</c>,
        /// since a ray has no far end) while the segment's parameter stays clamped to <c>[0,1]</c>.
        /// </summary>
        public static bool CheckCollisionRayLine(Vector2 origin, Vector2 direction, Vector2 p1, Vector2 p2, out Vector2 hitPoint, out float distance)
        {
            hitPoint = default; distance = 0f;
            float dirLenSq = direction.LengthSquared();
            if (dirLenSq < 1e-12f) return false;
            Vector2 dir = direction / MathF.Sqrt(dirLenSq);

            Vector2 segDir = p2 - p1;
            float denom = dir.X * segDir.Y - dir.Y * segDir.X;
            if (MathF.Abs(denom) < 1e-12f) return false; // parallel

            Vector2 diff = p1 - origin;
            float t = (diff.X * segDir.Y - diff.Y * segDir.X) / denom;
            float u = (diff.X * dir.Y - diff.Y * dir.X) / denom;
            if (t < 0f || u < 0f || u > 1f) return false;

            distance = t;
            hitPoint = origin + dir * t;
            return true;
        }
    }
}
