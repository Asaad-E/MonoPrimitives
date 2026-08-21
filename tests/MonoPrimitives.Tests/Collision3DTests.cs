using System;
using Microsoft.Xna.Framework;
using MonoPrimitives.Primitives3D;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-math checks for every <see cref="Collision3D"/> overlap/ray test — no GraphicsDevice needed.</summary>
    internal static class Collision3DTests
    {
        public static void Run(TestResults results)
        {
            results.Check("CheckCollisionSpheres: overlapping", () =>
                Collision3D.CheckCollisionSpheres(Vector3.Zero, 5f, new Vector3(8f, 0f, 0f), 5f) ? null : "expected true");

            results.Check("CheckCollisionSpheres: far apart", () =>
                !Collision3D.CheckCollisionSpheres(Vector3.Zero, 5f, new Vector3(20f, 0f, 0f), 5f) ? null : "expected false");

            results.Check("CheckCollisionBoxes: overlapping and far apart", () =>
            {
                var a = new BoundingBox(-Vector3.One, Vector3.One);
                var overlapping = new BoundingBox(Vector3.One * 0.5f, Vector3.One * 2f);
                var farAway = new BoundingBox(Vector3.One * 100f, Vector3.One * 101f);
                if (!Collision3D.CheckCollisionBoxes(a, overlapping)) return "expected overlapping boxes to collide";
                if (Collision3D.CheckCollisionBoxes(a, farAway)) return "expected far-apart boxes not to collide";
                return null;
            });

            results.Check("CheckCollisionBoxSphere: sphere inside box", () =>
                Collision3D.CheckCollisionBoxSphere(new BoundingBox(-Vector3.One * 5f, Vector3.One * 5f), Vector3.Zero, 1f) ? null : "expected true");

            results.Check("CheckCollisionBoxSphere: sphere far from box", () =>
                !Collision3D.CheckCollisionBoxSphere(new BoundingBox(-Vector3.One, Vector3.One), new Vector3(100, 100, 100), 1f) ? null : "expected false");

            results.Check("CheckCollisionCapsules: overlapping", () =>
                Collision3D.CheckCollisionCapsules(new Vector3(0, -5, 0), new Vector3(0, 5, 0), 1f, new Vector3(0.5f, -5, 0), new Vector3(0.5f, 5, 0), 1f) ? null : "expected true");

            results.Check("CheckCollisionCapsules: far apart", () =>
                !Collision3D.CheckCollisionCapsules(new Vector3(0, -5, 0), new Vector3(0, 5, 0), 1f, new Vector3(100, -5, 0), new Vector3(100, 5, 0), 1f) ? null : "expected false");

            results.Check("CheckCollisionCapsules: degenerate (zero-length) capsule still collides like a sphere", () =>
                Collision3D.CheckCollisionCapsules(Vector3.Zero, Vector3.Zero, 1f, new Vector3(1.5f, 0, 0), new Vector3(1.5f, 0, 0), 1f) ? null : "expected true");

            results.Check("CheckCollisionCapsuleSphere: overlapping, far apart, and degenerate capsule", () =>
            {
                if (!Collision3D.CheckCollisionCapsuleSphere(new Vector3(0, -5, 0), new Vector3(0, 5, 0), 1f, new Vector3(0.5f, 0, 0), 1f))
                    return "expected overlapping capsule/sphere to collide";
                if (Collision3D.CheckCollisionCapsuleSphere(new Vector3(0, -5, 0), new Vector3(0, 5, 0), 1f, new Vector3(100, 0, 0), 1f))
                    return "expected far-apart capsule/sphere not to collide";
                if (!Collision3D.CheckCollisionCapsuleSphere(Vector3.Zero, Vector3.Zero, 1f, new Vector3(1.5f, 0, 0), 1f))
                    return "expected a degenerate (zero-length) capsule to behave like a sphere";
                return null;
            });

            results.Check("CheckCollisionCapsuleBox: inside, far apart, radius reaching in, passing through, and exact-touching boundary", () =>
            {
                var box = new BoundingBox(-Vector3.One, Vector3.One);
                if (!Collision3D.CheckCollisionCapsuleBox(new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0), 0.2f, box))
                    return "expected a capsule fully inside the box to collide";
                if (Collision3D.CheckCollisionCapsuleBox(new Vector3(100, 0, 0), new Vector3(101, 0, 0), 0.2f, box))
                    return "expected a far-away capsule not to collide";
                if (!Collision3D.CheckCollisionCapsuleBox(new Vector3(1.5f, 0, 0), new Vector3(1.5f, 5, 0), 1f, box))
                    return "expected a capsule outside the box, but within reach via radius, to collide";
                if (Collision3D.CheckCollisionCapsuleBox(new Vector3(1.5f, 0, 0), new Vector3(1.5f, 5, 0), 0.3f, box))
                    return "expected a capsule outside the box, radius too small to reach, not to collide";
                if (!Collision3D.CheckCollisionCapsuleBox(new Vector3(-5, 0, 0), new Vector3(5, 0, 0), 0.2f, box))
                    return "expected a capsule passing straight through the box (both endpoints outside) to collide";
                if (!Collision3D.CheckCollisionCapsuleBox(new Vector3(2f, 0, 0), new Vector3(2f, 5, 0), 1f, box))
                    return "expected exactly-touching (distance == radius) to count as a collision";
                if (Collision3D.CheckCollisionCapsuleBox(new Vector3(2.02f, 0, 0), new Vector3(2.02f, 5, 0), 1f, box))
                    return "expected just-short-of-touching not to collide";
                return null;
            });

            results.Check("GetRayCollisionSphere: ray hits", () =>
            {
                var ray = new Ray(new Vector3(-20, 0, 0), Vector3.UnitX);
                RayCollision3D hit = Collision3D.GetRayCollisionSphere(ray, Vector3.Zero, 5f);
                if (!hit.Hit) return "expected a hit";
                return MathF.Abs(hit.Distance - 15f) > 0.01f ? $"expected distance near 15, got {hit.Distance}" : null;
            });

            results.Check("GetRayCollisionSphere: ray misses", () =>
            {
                var ray = new Ray(new Vector3(-20, 20, 0), Vector3.UnitX);
                return !Collision3D.GetRayCollisionSphere(ray, Vector3.Zero, 5f).Hit ? null : "expected no hit";
            });

            results.Check("GetRayCollisionBox: ray hits a face straight-on with the correct normal", () =>
            {
                var box = new BoundingBox(-Vector3.One, Vector3.One);
                var ray = new Ray(new Vector3(-10, 0, 0), Vector3.UnitX);
                RayCollision3D hit = Collision3D.GetRayCollisionBox(ray, box);
                if (!hit.Hit) return "expected a hit";
                if (MathF.Abs(hit.Distance - 9f) > 0.01f) return $"expected distance ~9, got {hit.Distance}";
                return Vector3.Distance(hit.Normal, -Vector3.UnitX) < 0.01f ? null : $"expected normal -X, got {hit.Normal}";
            });

            results.Check("GetRayCollisionBox: ray misses", () =>
                !Collision3D.GetRayCollisionBox(new Ray(new Vector3(-10, 10, 0), Vector3.UnitX), new BoundingBox(-Vector3.One, Vector3.One)).Hit
                    ? null : "expected no hit");

            // Regression test: GetRayCollisionBox's face-normal detection used to rely on a
            // fixed absolute epsilon, which broke down at large scale (float precision at the
            // hit point routinely exceeds a tiny fixed epsilon well before the box is actually
            // huge) -- fixed by picking the nearest face instead. This checks the fix holds at
            // a scale where the old bug reproduced (see DECISIONS.md).
            results.Check("GetRayCollisionBox: normal is scale-invariant at large box sizes", () =>
            {
                var box = new BoundingBox(new Vector3(-1_000_000f), new Vector3(1_000_000f));
                var ray = new Ray(new Vector3(0, 0, -2_000_000f), Vector3.UnitZ);
                RayCollision3D hit = Collision3D.GetRayCollisionBox(ray, box);
                if (!hit.Hit) return "expected a hit";
                Vector3 expectedNormal = -Vector3.UnitZ;
                return Vector3.Distance(hit.Normal, expectedNormal) < 0.01f
                    ? null
                    : $"expected normal {expectedNormal}, got {hit.Normal}";
            });

            results.Check("GetRayCollisionPlane: ray hits", () =>
            {
                var ray = new Ray(new Vector3(0, 10, 0), new Vector3(0, -1, 0));
                RayCollision3D hit = Collision3D.GetRayCollisionPlane(ray, Vector3.Zero, Vector3.Up);
                if (!hit.Hit) return "expected a hit";
                return Vector3.Distance(hit.Point, Vector3.Zero) < 0.01f ? null : $"expected hit point near origin, got {hit.Point}";
            });

            results.Check("GetRayCollisionPlane: ray parallel to plane misses", () =>
            {
                var ray = new Ray(new Vector3(0, 10, 0), Vector3.UnitX);
                return !Collision3D.GetRayCollisionPlane(ray, Vector3.Zero, Vector3.Up).Hit ? null : "expected no hit";
            });

            results.Check("GetRayCollisionTriangle: hits through the centroid with a ray-facing normal, misses outside/parallel/behind", () =>
            {
                Vector3 p1 = new(-1, -1, 5), p2 = new(1, -1, 5), p3 = new(0, 1, 5);
                var rayHit = new Ray(new Vector3(0, -0.33f, 0), Vector3.UnitZ);
                RayCollision3D hit = Collision3D.GetRayCollisionTriangle(rayHit, p1, p2, p3);
                if (!hit.Hit) return "expected a hit through the centroid";
                if (MathF.Abs(hit.Distance - 5f) > 0.01f) return $"expected distance ~5, got {hit.Distance}";
                if (Vector3.Dot(hit.Normal, rayHit.Direction) >= 0f) return "expected the normal to face back toward the ray origin";

                if (Collision3D.GetRayCollisionTriangle(new Ray(new Vector3(10, 10, 0), Vector3.UnitZ), p1, p2, p3).Hit)
                    return "expected a ray outside the triangle to miss";
                if (Collision3D.GetRayCollisionTriangle(new Ray(Vector3.Zero, Vector3.UnitX), p1, p2, p3).Hit)
                    return "expected a ray parallel to the triangle's plane to miss";
                if (Collision3D.GetRayCollisionTriangle(new Ray(new Vector3(0, -0.33f, 10), Vector3.UnitZ), p1, p2, p3).Hit)
                    return "expected a hit behind the ray origin to miss";

                // Reversed winding still hits at the same distance, normal still facing the ray.
                RayCollision3D flipped = Collision3D.GetRayCollisionTriangle(rayHit, p1, p3, p2);
                if (!flipped.Hit || MathF.Abs(flipped.Distance - hit.Distance) > 1e-4f) return "expected reversed winding to hit at the same distance";
                if (Vector3.Dot(flipped.Normal, rayHit.Direction) >= 0f) return "expected reversed winding's normal to also face the ray";
                return null;
            });

            results.Check("GetRayCollisionQuad: hits either triangle half, misses outside", () =>
            {
                Vector3 q1 = new(-1, -1, 5), q2 = new(1, -1, 5), q3 = new(1, 1, 5), q4 = new(-1, 1, 5);
                RayCollision3D center = Collision3D.GetRayCollisionQuad(new Ray(Vector3.Zero, Vector3.UnitZ), q1, q2, q3, q4);
                if (!center.Hit || MathF.Abs(center.Distance - 5f) > 0.01f) return "expected a hit through the quad's center";

                RayCollision3D firstHalf = Collision3D.GetRayCollisionQuad(new Ray(new Vector3(0.5f, -0.9f, 0), Vector3.UnitZ), q1, q2, q3, q4);
                if (!firstHalf.Hit) return "expected a hit in the (p1,p2,p3) triangle half";

                return Collision3D.GetRayCollisionQuad(new Ray(new Vector3(10, 10, 0), Vector3.UnitZ), q1, q2, q3, q4).Hit
                    ? "expected a ray outside the quad to miss" : null;
            });

            results.Check("GetRayCollisionCapsule: hits the cylindrical body, hits an end cap, misses, and degenerates to a sphere", () =>
            {
                Vector3 start = new(0, -5, 0), end = new(0, 5, 0);
                const float radius = 1f;

                // Body hit: ray travelling along X at the capsule's vertical midpoint.
                RayCollision3D body = Collision3D.GetRayCollisionCapsule(new Ray(new Vector3(-10, 0, 0), Vector3.UnitX), start, end, radius);
                if (!body.Hit || MathF.Abs(body.Distance - 9f) > 0.01f) return $"expected a body hit at distance ~9, got hit={body.Hit} dist={body.Distance}";

                // Cap hit: ray coming straight down onto the top hemisphere, above the cylindrical span.
                RayCollision3D cap = Collision3D.GetRayCollisionCapsule(new Ray(new Vector3(0, 20, 0), -Vector3.UnitY), start, end, radius);
                if (!cap.Hit || MathF.Abs(cap.Distance - 14f) > 0.01f) return $"expected a cap hit at distance ~14, got hit={cap.Hit} dist={cap.Distance}";

                // Miss: ray well outside the capsule's radius.
                if (Collision3D.GetRayCollisionCapsule(new Ray(new Vector3(-10, 100, 0), Vector3.UnitX), start, end, radius).Hit)
                    return "expected a far-away ray to miss";

                // Degenerate (zero-length) capsule behaves like a sphere.
                RayCollision3D degenerate = Collision3D.GetRayCollisionCapsule(new Ray(new Vector3(-10, 0, 0), Vector3.UnitX), Vector3.Zero, Vector3.Zero, radius);
                if (!degenerate.Hit || MathF.Abs(degenerate.Distance - 9f) > 0.01f) return "expected a degenerate capsule to behave like a sphere";
                return null;
            });
        }
    }
}
