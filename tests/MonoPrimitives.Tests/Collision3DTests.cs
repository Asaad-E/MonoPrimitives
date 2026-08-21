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

            results.Check("CheckCollisionBoxSphere: sphere inside box", () =>
                Collision3D.CheckCollisionBoxSphere(new BoundingBox(-Vector3.One * 5f, Vector3.One * 5f), Vector3.Zero, 1f) ? null : "expected true");

            results.Check("CheckCollisionBoxSphere: sphere far from box", () =>
                !Collision3D.CheckCollisionBoxSphere(new BoundingBox(-Vector3.One, Vector3.One), new Vector3(100, 100, 100), 1f) ? null : "expected false");

            results.Check("CheckCollisionCapsules: overlapping", () =>
                Collision3D.CheckCollisionCapsules(new Vector3(0, -5, 0), new Vector3(0, 5, 0), 1f, new Vector3(0.5f, -5, 0), new Vector3(0.5f, 5, 0), 1f) ? null : "expected true");

            results.Check("CheckCollisionCapsules: far apart", () =>
                !Collision3D.CheckCollisionCapsules(new Vector3(0, -5, 0), new Vector3(0, 5, 0), 1f, new Vector3(100, -5, 0), new Vector3(100, 5, 0), 1f) ? null : "expected false");

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
        }
    }
}
