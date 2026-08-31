#nullable enable

using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace MonoPrimitives.Primitives2D
{
    /// <summary>Polygon geometry helpers <see cref="Primitive2DBatch"/> already used internally to fill arbitrary polygons, exposed for building your own mesh/collision/nav data.</summary>
    public static class PolygonUtil
    {
        // Same budget/rationale as Primitive2DBatch's own stackalloc-vs-heap threshold.
        private const int MaxStackAllocElements = 4096;

        /// <summary>True if walking <paramref name="points"/> in order always turns the same rotational way (allowing near-collinear runs).</summary>
        /// <remarks>This is exactly the condition <see cref="MonoPrimitives.Primitives2D.Collision2D"/>'s SAT-based checks (<c>Polys</c>/<c>RecPoly</c>/<c>RecTriangle</c>/<c>Triangles</c>) require of their input -- check this first if you're not sure a polygon qualifies.</remarks>
        public static bool IsConvex(ReadOnlySpan<Vector2> points)
        {
            int n = points.Length;
            if (n < 4) return true;

            float sign = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % n];
                Vector2 c = points[(i + 2) % n];
                float cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                if (MathF.Abs(cross) < 1e-7f) continue; // near-collinear: doesn't determine turn direction
                if (sign == 0f) sign = MathF.Sign(cross);
                else if (MathF.Sign(cross) != sign) return false;
            }
            return true;
        }

        /// <summary>Ear-clipping triangulation for an arbitrary simple polygon (concave allowed; must not self-intersect).</summary>
        /// <remarks>
        /// Writes up to <c>(points.Length - 2) * 3</c> LOCAL indices (0-based into
        /// <paramref name="points"/>) into <paramref name="outIndices"/> and returns how many were
        /// written, or 0 if triangulation got stuck (degenerate or self-intersecting input -- fall
        /// back to a plain fan from <c>points[0]</c> in that case, same as <see cref="Primitive2DBatch.FillPolygon"/> does).
        /// </remarks>
        public static int Triangulate(ReadOnlySpan<Vector2> points, Span<int> outIndices)
        {
            int n = points.Length;
            if (n < 3) return 0;
            if (n == 3) { outIndices[0] = 0; outIndices[1] = 1; outIndices[2] = 2; return 3; }

            // Overall winding decides which triangle orientation counts as a clippable "ear"
            // versus a reflex (concave) vertex while clipping.
            float area2 = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % n];
                area2 += a.X * b.Y - b.X * a.Y;
            }
            bool ccw = area2 > 0f;

            Span<int> prevIdx = n <= MaxStackAllocElements ? stackalloc int[n] : new int[n];
            Span<int> nextIdx = n <= MaxStackAllocElements ? stackalloc int[n] : new int[n];
            for (int i = 0; i < n; i++)
            {
                prevIdx[i] = (i - 1 + n) % n;
                nextIdx[i] = (i + 1) % n;
            }

            int remaining = n;
            int outCount = 0;
            int current = 0;
            int guard = 0;
            int maxGuard = n * n + n; // generous bound for legitimate simple polygons; only degenerate input exhausts it

            while (remaining > 3 && guard < maxGuard)
            {
                guard++;
                int ip = prevIdx[current], inx = nextIdx[current];
                Vector2 a = points[ip], b = points[current], c = points[inx];

                float cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                bool isConvexVertex = ccw ? cross > 0f : cross < 0f;

                if (isConvexVertex && !AnyRemainingPointInTriangle(points, nextIdx, inx, ip, a, b, c))
                {
                    outIndices[outCount++] = ip;
                    outIndices[outCount++] = current;
                    outIndices[outCount++] = inx;

                    nextIdx[ip] = inx;
                    prevIdx[inx] = ip;
                    remaining--;
                    current = ip; // re-check the vertex before the one just clipped
                }
                else
                {
                    current = inx;
                }
            }

            if (remaining != 3)
                return 0; // stuck -- degenerate/self-intersecting input

            outIndices[outCount++] = prevIdx[current];
            outIndices[outCount++] = current;
            outIndices[outCount++] = nextIdx[current];
            return outCount;
        }

        // Any still-remaining vertex (other than the candidate ear's own 3 corners) strictly
        // inside triangle (a,b,c)? Used to reject a topologically-convex ear that another vertex
        // still pokes into.
        private static bool AnyRemainingPointInTriangle(ReadOnlySpan<Vector2> points, Span<int> nextIdx, int inx, int ip, in Vector2 a, in Vector2 b, in Vector2 c)
        {
            for (int i = nextIdx[inx]; i != ip; i = nextIdx[i])
            {
                if (PointInTriangle(points[i], a, b, c)) return true;
            }
            return false;
        }

        private static bool PointInTriangle(in Vector2 p, in Vector2 a, in Vector2 b, in Vector2 c)
        {
            float d1 = EdgeCross(p, a, b);
            float d2 = EdgeCross(p, b, c);
            float d3 = EdgeCross(p, c, a);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EdgeCross(in Vector2 p, in Vector2 a, in Vector2 b) => (a.X - p.X) * (b.Y - p.Y) - (a.Y - p.Y) * (b.X - p.X);
    }
}
