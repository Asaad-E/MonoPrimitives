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
        }
    }
}
