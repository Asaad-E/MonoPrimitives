using System;
using Microsoft.Xna.Framework;

namespace MonoPrimitives.Primitives3D
{
    /// <summary>
    /// Result of a ray-vs-shape test (<c>Hit</c>/<c>Distance</c>/<c>Point</c>/<c>Normal</c>),
    /// used as a consistent return type across every <see cref="Collision3D"/> raycast
    /// instead of MonoGame's own mix of <c>bool</c> and <c>float?</c> returns with no normal.
    /// </summary>
    public readonly struct RayCollision3D
    {
        public readonly bool Hit;
        public readonly float Distance;
        public readonly Vector3 Point;
        public readonly Vector3 Normal;

        public RayCollision3D(bool hit, float distance, Vector3 point, Vector3 normal)
        {
            Hit = hit;
            Distance = distance;
            Point = point;
            Normal = normal;
        }
    }

    /// <summary>
    /// 3D collision/raycast utilities. Sphere/box/ray tests already exist natively on
    /// MonoGame's own <see cref="BoundingSphere"/>/<see cref="BoundingBox"/>/<see cref="Ray"/>
    /// (<c>.Intersects(...)</c>) — those are wrapped here only for a consistent name and
    /// the unified <see cref="RayCollision3D"/> result (point + normal, not just a distance).
    /// The genuinely new part: <b>capsule</b> collision — a shape this library draws
    /// (<c>FillCapsule</c>) that MonoGame has no bounding type for at all.
    /// </summary>
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

        /// <summary>
        /// Capsule vs capsule overlap (new — no MonoGame equivalent). Reduces to the distance
        /// between the two capsules' central segments via the standard closest-point-between-
        /// two-segments algorithm (Ericson, <i>Real-Time Collision Detection</i> §5.1.9).
        /// </summary>
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

        private static Vector3 BoxFaceNormal(in Vector3 point, in BoundingBox box)
        {
            const float eps = 1e-3f;
            if (MathF.Abs(point.X - box.Min.X) < eps) return -Vector3.UnitX;
            if (MathF.Abs(point.X - box.Max.X) < eps) return Vector3.UnitX;
            if (MathF.Abs(point.Y - box.Min.Y) < eps) return -Vector3.UnitY;
            if (MathF.Abs(point.Y - box.Max.Y) < eps) return Vector3.UnitY;
            if (MathF.Abs(point.Z - box.Min.Z) < eps) return -Vector3.UnitZ;
            return Vector3.UnitZ;
        }

        private static Vector3 ClosestPointOnSegment(in Vector3 a, in Vector3 b, in Vector3 p)
        {
            Vector3 ab = b - a;
            float lenSq = ab.LengthSquared();
            if (lenSq < 1e-12f) return a;
            float t = Math.Clamp(Vector3.Dot(p - a, ab) / lenSq, 0f, 1f);
            return a + ab * t;
        }

        /// <summary>Closest points between two line segments (Ericson, <i>Real-Time Collision Detection</i> §5.1.9).</summary>
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
