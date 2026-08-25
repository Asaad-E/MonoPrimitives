using System;
using Microsoft.Xna.Framework;

namespace MonoPrimitives.Primitives3D
{
    /// <summary>Result of a ray-vs-shape test (<c>Hit</c>/<c>Distance</c>/<c>Point</c>/<c>Normal</c>), the consistent return type for every <see cref="Collision3D"/> raycast.</summary>
    public readonly struct RayCollision3D
    {
        /// <summary>Whether the ray actually hit anything. When <c>false</c>, the other three fields are meaningless (typically <c>default</c>) — always check this first.</summary>
        public readonly bool Hit;

        /// <summary>Distance from the ray's origin to <see cref="Point"/>, in the same units as the ray's direction (world units for a normalized direction).</summary>
        public readonly float Distance;

        /// <summary>World-space point where the ray hit.</summary>
        public readonly Vector3 Point;

        /// <summary>Surface normal at <see cref="Point"/>, pointing away from the shape.</summary>
        public readonly Vector3 Normal;

        /// <summary>Builds a result directly — normally returned by a <see cref="Collision3D"/> raycast method rather than constructed by hand.</summary>
        public RayCollision3D(bool hit, float distance, Vector3 point, Vector3 normal)
        {
            Hit = hit;
            Distance = distance;
            Point = point;
            Normal = normal;
        }
    }

    /// <summary>3D collision/raycast utilities.</summary>
    /// <remarks>Wraps MonoGame's own <see cref="BoundingSphere"/>/<see cref="BoundingBox"/>/<see cref="Ray"/> tests for a consistent name and the unified <see cref="RayCollision3D"/> result. Capsule collision is the genuinely new part — MonoGame has no bounding type for it.</remarks>
    public static class Collision3D
    {
        // ---------------------------------------------------------------------
        // Overlap tests
        // ---------------------------------------------------------------------

        /// <summary>Sphere vs sphere overlap. Thin wrapper over <see cref="BoundingSphere.Intersects(BoundingSphere)"/>.</summary>
        public static bool CheckCollisionSpheres(Vector3 center1, float radius1, Vector3 center2, float radius2)
            => new BoundingSphere(center1, radius1).Intersects(new BoundingSphere(center2, radius2));

        /// <summary>Box vs box overlap. Thin wrapper over <see cref="BoundingBox.Intersects(BoundingBox)"/>.</summary>
        public static bool CheckCollisionBoxes(BoundingBox box1, BoundingBox box2) => box1.Intersects(box2);

        /// <summary>Box vs sphere overlap. Thin wrapper over <see cref="BoundingBox.Intersects(BoundingSphere)"/>.</summary>
        public static bool CheckCollisionBoxSphere(BoundingBox box, Vector3 center, float radius)
            => box.Intersects(new BoundingSphere(center, radius));

        /// <summary>Capsule vs capsule overlap (new — no MonoGame equivalent): the distance between the two capsules' central segments, compared against the sum of both radii.</summary>
        public static bool CheckCollisionCapsules(Vector3 start1, Vector3 end1, float radius1, Vector3 start2, Vector3 end2, float radius2)
        {
            (Vector3 c1, Vector3 c2) = ClosestPointsBetweenSegments(start1, end1, start2, end2);
            float radiusSum = radius1 + radius2;
            return Vector3.DistanceSquared(c1, c2) <= radiusSum * radiusSum;
        }

        /// <summary>Capsule vs sphere overlap (new — no MonoGame equivalent).</summary>
        public static bool CheckCollisionCapsuleSphere(Vector3 capStart, Vector3 capEnd, float capRadius, Vector3 sphereCenter, float sphereRadius)
        {
            Vector3 closest = ClosestPointOnSegment(capStart, capEnd, sphereCenter);
            float radiusSum = capRadius + sphereRadius;
            return Vector3.DistanceSquared(closest, sphereCenter) <= radiusSum * radiusSum;
        }

        /// <summary>Capsule vs axis-aligned box overlap (new — no MonoGame equivalent). True if the shortest distance between the capsule's own central segment and <paramref name="box"/> is within <paramref name="radius"/>.</summary>
        // Segment-to-box distance has no simple closed form (unlike point-to-box, a single per-axis
        // clamp) -- minimized via ternary search over the segment's parameter t, valid because
        // distance from a fixed point on the segment to the box is convex in t (a sum of per-axis
        // clamped-linear terms, each convex), so the whole function has one minimum, no local traps.
        public static bool CheckCollisionCapsuleBox(Vector3 start, Vector3 end, float radius, BoundingBox box)
        {
            float radiusSq = radius * radius;
            Vector3 d = end - start;

            float DistSquaredAt(float t)
            {
                Vector3 p = start + d * t;
                Vector3 c = Vector3.Clamp(p, box.Min, box.Max);
                return Vector3.DistanceSquared(p, c);
            }

            float lo = 0f, hi = 1f;
            for (int i = 0; i < 32; i++) // shrinks the bracket by 2/3 each step -- (2/3)^32 is far below float precision
            {
                float m1 = lo + (hi - lo) / 3f;
                float m2 = hi - (hi - lo) / 3f;
                if (DistSquaredAt(m1) < DistSquaredAt(m2)) hi = m2; else lo = m1;
            }

            return DistSquaredAt((lo + hi) * 0.5f) <= radiusSq;
        }

        // ---------------------------------------------------------------------
        // Raycasts
        // ---------------------------------------------------------------------

        /// <summary>Ray vs sphere, wrapping <see cref="Ray.Intersects(BoundingSphere)"/> into a <see cref="RayCollision3D"/> (adds the hit point and surface normal).</summary>
        public static RayCollision3D GetRayCollisionSphere(Ray ray, Vector3 center, float radius)
        {
            float? d = ray.Intersects(new BoundingSphere(center, radius));
            if (!d.HasValue) return default;
            Vector3 point = ray.Position + ray.Direction * d.Value;
            return new RayCollision3D(true, d.Value, point, SafeNormalize(point - center, Vector3.Up));
        }

        /// <summary>Ray vs box, wrapping <see cref="Ray.Intersects(BoundingBox)"/> into a <see cref="RayCollision3D"/> (adds the hit point and which face's normal was hit).</summary>
        public static RayCollision3D GetRayCollisionBox(Ray ray, BoundingBox box)
        {
            float? d = ray.Intersects(box);
            if (!d.HasValue) return default;
            Vector3 point = ray.Position + ray.Direction * d.Value;
            return new RayCollision3D(true, d.Value, point, BoxFaceNormal(point, box));
        }

        /// <summary>Ray vs infinite plane (defined by a point on it and its normal) — ground planes, mirrors, cut planes.</summary>
        public static RayCollision3D GetRayCollisionPlane(Ray ray, Vector3 planePoint, Vector3 planeNormal)
        {
            Vector3 n = SafeNormalize(planeNormal, Vector3.Up);
            float denom = Vector3.Dot(ray.Direction, n);
            if (MathF.Abs(denom) < 1e-8f) return default; // parallel to the plane

            float t = Vector3.Dot(planePoint - ray.Position, n) / denom;
            if (t < 0f) return default;

            Vector3 point = ray.Position + ray.Direction * t;
            return new RayCollision3D(true, t, point, denom < 0f ? n : -n); // normal faces back toward the ray origin
        }

        /// <summary>Ray vs triangle. Useful for mesh/terrain picking against triangles you already have on hand (e.g. one cell of a heightmap).</summary>
        public static RayCollision3D GetRayCollisionTriangle(Ray ray, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            // Möller-Trumbore algorithm.
            const float epsilon = 1e-8f;
            Vector3 edge1 = p2 - p1;
            Vector3 edge2 = p3 - p1;
            Vector3 h = Vector3.Cross(ray.Direction, edge2);
            float a = Vector3.Dot(edge1, h);
            if (MathF.Abs(a) < epsilon) return default; // ray parallel to the triangle's plane

            float f = 1f / a;
            Vector3 s = ray.Position - p1;
            float u = f * Vector3.Dot(s, h);
            if (u < 0f || u > 1f) return default;

            Vector3 q = Vector3.Cross(s, edge1);
            float v = f * Vector3.Dot(ray.Direction, q);
            if (v < 0f || u + v > 1f) return default;

            float t = f * Vector3.Dot(edge2, q);
            if (t < 0f) return default;

            Vector3 point = ray.Position + ray.Direction * t;
            Vector3 normal = SafeNormalize(Vector3.Cross(edge1, edge2), Vector3.Up);
            if (Vector3.Dot(normal, ray.Direction) > 0f) normal = -normal; // face back toward the ray origin, same convention as GetRayCollisionPlane
            return new RayCollision3D(true, t, point, normal);
        }

        /// <summary>Ray vs a planar quad, given its 4 corners in order (<c>p1 -&gt; p2 -&gt; p3 -&gt; p4</c>, same winding <see cref="Primitive3DBatch.FillPlane(Vector3,Vector2,Color)"/> uses).</summary>
        /// <remarks>Tested as two triangles (<c>p1,p2,p3</c> and <c>p1,p3,p4</c>); returns whichever one the ray actually hits.</remarks>
        public static RayCollision3D GetRayCollisionQuad(Ray ray, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
        {
            RayCollision3D hit = GetRayCollisionTriangle(ray, p1, p2, p3);
            return hit.Hit ? hit : GetRayCollisionTriangle(ray, p1, p3, p4);
        }

        /// <summary>
        /// Ray vs capsule (new — no MonoGame equivalent). Tests the cylindrical body (an
        /// infinite-cylinder intersection, accepted only where it falls between the two caps)
        /// and both hemispherical end caps, returning whichever valid hit is nearest.
        /// </summary>
        public static RayCollision3D GetRayCollisionCapsule(Ray ray, Vector3 start, Vector3 end, float radius)
        {
            Vector3 axis = end - start;
            float axisLenSq = axis.LengthSquared();
            if (axisLenSq < 1e-12f)
                return GetRayCollisionSphere(ray, start, radius); // degenerate capsule = sphere

            float axisLen = MathF.Sqrt(axisLenSq);
            Vector3 axisDir = axis / axisLen;

            RayCollision3D best = default;
            float bestT = float.PositiveInfinity;

            void TryAcceptCylinderHit(float t)
            {
                if (t < 0f || t >= bestT) return;
                Vector3 p = ray.Position + ray.Direction * t;
                float axisPos = Vector3.Dot(p - start, axisDir);
                if (axisPos < 0f || axisPos > axisLen) return; // outside the cylindrical span; the caps handle this region
                bestT = t;
                Vector3 onAxis = start + axisDir * axisPos;
                best = new RayCollision3D(true, t, p, SafeNormalize(p - onAxis, Vector3.Up));
            }

            Vector3 rd = ray.Direction;
            Vector3 oc = ray.Position - start;
            Vector3 rdPerp = rd - axisDir * Vector3.Dot(rd, axisDir);
            Vector3 ocPerp = oc - axisDir * Vector3.Dot(oc, axisDir);
            float a = rdPerp.LengthSquared();
            if (a > 1e-12f)
            {
                float b = 2f * Vector3.Dot(rdPerp, ocPerp);
                float c = ocPerp.LengthSquared() - radius * radius;
                float disc = b * b - 4f * a * c;
                if (disc >= 0f)
                {
                    float sq = MathF.Sqrt(disc);
                    TryAcceptCylinderHit((-b - sq) / (2f * a));
                    TryAcceptCylinderHit((-b + sq) / (2f * a));
                }
            }

            RayCollision3D capStart = GetRayCollisionSphere(ray, start, radius);
            if (capStart.Hit && capStart.Distance < bestT) { bestT = capStart.Distance; best = capStart; }

            RayCollision3D capEnd = GetRayCollisionSphere(ray, end, radius);
            if (capEnd.Hit && capEnd.Distance < bestT) { best = capEnd; }

            return best;
        }

        // ---------------------------------------------------------------------
        // Internals
        // ---------------------------------------------------------------------

        private static Vector3 SafeNormalize(in Vector3 v, in Vector3 fallback)
        {
            float lenSq = v.LengthSquared();
            return lenSq < 1e-12f ? fallback : v * (1f / MathF.Sqrt(lenSq));
        }

        // Picks the nearest of the 6 face planes rather than a fixed absolute-epsilon threshold —
        // at large box scales, floating-point error in the hit point (from Ray.Intersects plus
        // the point-reconstruction multiply-add) routinely exceeds a small fixed epsilon even
        // for an unambiguous mid-face hit, which made every prior epsilon check fail and silently
        // fall through to a hardcoded wrong normal. Verified: a 1,000,000-unit box with oblique
        // rays previously returned the wrong face on ~7% of hits (always the same hardcoded
        // fallback), 0% after switching to nearest-face selection.
        private static Vector3 BoxFaceNormal(in Vector3 point, in BoundingBox box)
        {
            float dMinX = MathF.Abs(point.X - box.Min.X), dMaxX = MathF.Abs(point.X - box.Max.X);
            float dMinY = MathF.Abs(point.Y - box.Min.Y), dMaxY = MathF.Abs(point.Y - box.Max.Y);
            float dMinZ = MathF.Abs(point.Z - box.Min.Z), dMaxZ = MathF.Abs(point.Z - box.Max.Z);

            float min = dMinX;
            Vector3 normal = -Vector3.UnitX;
            if (dMaxX < min) { min = dMaxX; normal = Vector3.UnitX; }
            if (dMinY < min) { min = dMinY; normal = -Vector3.UnitY; }
            if (dMaxY < min) { min = dMaxY; normal = Vector3.UnitY; }
            if (dMinZ < min) { min = dMinZ; normal = -Vector3.UnitZ; }
            if (dMaxZ < min) { normal = Vector3.UnitZ; }
            return normal;
        }

        private static Vector3 ClosestPointOnSegment(in Vector3 a, in Vector3 b, in Vector3 p)
        {
            Vector3 ab = b - a;
            float lenSq = ab.LengthSquared();
            if (lenSq < 1e-12f) return a;
            float t = Math.Clamp(Vector3.Dot(p - a, ab) / lenSq, 0f, 1f);
            return a + ab * t;
        }

        // Closest points between two line segments (Ericson, Real-Time Collision Detection Sec 5.1.9).
        private static (Vector3, Vector3) ClosestPointsBetweenSegments(in Vector3 p1, in Vector3 q1, in Vector3 p2, in Vector3 q2)
        {
            const float epsilon = 1e-9f;
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);

            float s, t;
            if (a <= epsilon && e <= epsilon)
            {
                return (p1, p2);
            }
            if (a <= epsilon)
            {
                s = 0f;
                t = Math.Clamp(f / e, 0f, 1f);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= epsilon)
                {
                    t = 0f;
                    s = Math.Clamp(-c / a, 0f, 1f);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > epsilon ? Math.Clamp((b * f - c * e) / denom, 0f, 1f) : 0f;
                    t = (b * s + f) / e;

                    if (t < 0f) { t = 0f; s = Math.Clamp(-c / a, 0f, 1f); }
                    else if (t > 1f) { t = 1f; s = Math.Clamp((b - c) / a, 0f, 1f); }
                }
            }

            return (p1 + d1 * s, p2 + d2 * t);
        }
    }
}
