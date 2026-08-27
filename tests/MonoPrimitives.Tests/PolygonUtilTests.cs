using System;
using Microsoft.Xna.Framework;
using MonoPrimitives.Primitives2D;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-math checks for <see cref="PolygonUtil"/> — no GraphicsDevice needed.</summary>
    internal static class PolygonUtilTests
    {
        // A concave "L-shape" (6 vertices, one reflex corner) -- the standard shape for proving a
        // triangulator actually handles concave input, not just convex.
        private static readonly Vector2[] LShape =
        {
            new(0, 0), new(4, 0), new(4, 2), new(2, 2), new(2, 4), new(0, 4),
        };

        // Shoelace formula -- ground truth for the polygon's own area, independent of how it gets triangulated.
        private static float PolygonArea(ReadOnlySpan<Vector2> pts)
        {
            float area2 = 0f;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
                area2 += a.X * b.Y - b.X * a.Y;
            }
            return MathF.Abs(area2) * 0.5f;
        }

        private static float TriangleArea(Vector2 a, Vector2 b, Vector2 c) =>
            MathF.Abs((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y)) * 0.5f;

        public static void Run(TestResults results)
        {
            results.Check("PolygonUtil.IsConvex: true for a square, false for a concave L-shape", () =>
            {
                Vector2[] square = { new(0, 0), new(4, 0), new(4, 4), new(0, 4) };
                if (!PolygonUtil.IsConvex(square)) return "a square should be convex";
                if (PolygonUtil.IsConvex(LShape)) return "an L-shape has a reflex corner -- should not be convex";
                return null;
            });

            results.Check("PolygonUtil.IsConvex: always true below 4 points (nothing to be concave about)", () =>
            {
                Vector2[] triangle = { new(0, 0), new(1, 0), new(0, 1) };
                if (!PolygonUtil.IsConvex(triangle)) return "a triangle is always convex";
                if (!PolygonUtil.IsConvex(new Vector2[] { new(0, 0), new(1, 1) })) return "2 points should short-circuit to true";
                return null;
            });

            results.Check("PolygonUtil.Triangulate: a square produces 2 triangles using only its own 4 vertex indices", () =>
            {
                Vector2[] square = { new(0, 0), new(4, 0), new(4, 4), new(0, 4) };
                Span<int> indices = stackalloc int[(square.Length - 2) * 3];
                int written = PolygonUtil.Triangulate(square, indices);

                if (written != 6) return $"expected 6 indices (2 triangles) for a 4-gon, got {written}";
                foreach (int idx in indices)
                    if (idx < 0 || idx >= square.Length) return $"index {idx} is out of range for a 4-vertex polygon";
                return null;
            });

            results.Check("PolygonUtil.Triangulate: a concave L-shape's triangles exactly cover its own shoelace area", () =>
            {
                Span<int> indices = stackalloc int[(LShape.Length - 2) * 3];
                int written = PolygonUtil.Triangulate(LShape, indices);
                int triangleCount = written / 3;

                if (written != (LShape.Length - 2) * 3) return $"expected {(LShape.Length - 2) * 3} indices for a {LShape.Length}-gon, got {written}";

                float summedArea = 0f;
                for (int i = 0; i < triangleCount; i++)
                {
                    Vector2 a = LShape[indices[i * 3]], b = LShape[indices[i * 3 + 1]], c = LShape[indices[i * 3 + 2]];
                    summedArea += TriangleArea(a, b, c);
                }

                float expected = PolygonArea(LShape);
                // Exact area match is the real test here: overlapping triangles would sum too high,
                // a missed sliver would sum too low -- only a correct, non-overlapping cover matches.
                if (MathF.Abs(summedArea - expected) > 1e-3f)
                    return $"triangulated area {summedArea} should exactly match the polygon's own shoelace area {expected}";
                return null;
            });

            results.Check("PolygonUtil.Triangulate: fewer than 3 points writes nothing", () =>
            {
                Span<int> indices = stackalloc int[3];
                int written = PolygonUtil.Triangulate(new Vector2[] { new(0, 0), new(1, 1) }, indices);
                return written == 0 ? null : $"expected 0 for a 2-point 'polygon', got {written}";
            });
        }
    }
}
