#nullable enable

using System;
using Microsoft.Xna.Framework;

namespace Primitives2D
{
    /// <summary>
    /// Pure geometry overlap/intersection tests (circle, rectangle, triangle, polygon,
    /// line). Static utilities rather than <see cref="PrimitiveBatch"/> methods, since they
    /// don't touch a <see cref="Microsoft.Xna.Framework.Graphics.GraphicsDevice"/> at all —
    /// plain math you can call any time, not just mid-draw. MonoGame's own
    /// <see cref="Rectangle"/> only covers rect-rect/point-in-rect; everything else here
    /// fills a real gap. Useful for hit detection, platformer collision, or neighbor/
    /// separation checks in a simulation.
    /// </summary>
    public static class Collision2D
    {
        /// <summary>Rectangle vs rectangle overlap. Thin wrapper over <see cref="Rectangle.Intersects(Rectangle)"/>, included here for naming consistency with the rest of this class.</summary>
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

        /// <summary>Point inside (or on the edge of) a rectangle. Thin wrapper over <see cref="Rectangle.Contains(Point)"/>, included here for naming consistency with the rest of this class.</summary>
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

        /// <summary>
        /// Point inside an arbitrary (possibly non-convex) polygon, via the standard
        /// even-odd ray-casting rule. Correct for any simple polygon, not just the
        /// convex/star-shaped input this library's own <c>FillPolygon</c> assumes.
        /// </summary>
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

        /// <summary>The overlapping rectangle of two rectangles (empty — <c>Width</c>/<c>Height</c> 0 — if they don't overlap). Thin wrapper over <see cref="Rectangle.Intersect(Rectangle,Rectangle)"/>, included here for naming consistency with the rest of this class.</summary>
        public static Rectangle GetCollisionRec(Rectangle rec1, Rectangle rec2) => Rectangle.Intersect(rec1, rec2);

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

        /// <summary>Ray vs axis-aligned rectangle (slab method). <paramref name="distance"/> is 0 if <paramref name="origin"/> starts inside <paramref name="rec"/>.</summary>
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
