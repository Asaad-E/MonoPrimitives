using Microsoft.Xna.Framework;
using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-math checks for <see cref="RectangleF"/> — no GraphicsDevice needed.</summary>
    internal static class RectangleFTests
    {
        public static void Run(TestResults results)
        {
            results.Check("RectangleF: Left/Right/Top/Bottom/Center match X/Y/Width/Height", () =>
            {
                var r = new RectangleF(10f, 20f, 30f, 40f);
                if (r.Left != 10f || r.Top != 20f) return $"Left/Top mismatch: {r.Left},{r.Top}";
                if (r.Right != 40f || r.Bottom != 60f) return $"Right/Bottom mismatch: {r.Right},{r.Bottom}";
                if (r.Center != new Vector2(25f, 40f)) return $"Center mismatch: {r.Center}";
                return null;
            });

            results.Check("RectangleF.FromCenter round-trips against Center", () =>
            {
                var center = new Vector2(100f, 50f);
                var size = new Vector2(20f, 10f);
                var r = RectangleF.FromCenter(center, size);
                if (Vector2.Distance(r.Center, center) > 1e-4f) return $"FromCenter's own Center drifted: {r.Center} vs {center}";
                if (r.Size != size) return $"Size mismatch: {r.Size} vs {size}";
                return null;
            });

            results.Check("RectangleF.Contains matches Rectangle.Contains at integer coordinates", () =>
            {
                var rect = new Rectangle(0, 0, 10, 10);
                var rectF = (RectangleF)rect;
                for (int x = -2; x <= 12; x++)
                {
                    for (int y = -2; y <= 12; y++)
                    {
                        bool expected = rect.Contains(x, y);
                        bool actual = rectF.Contains(x, y);
                        if (expected != actual) return $"({x},{y}): Rectangle.Contains={expected}, RectangleF.Contains={actual}";
                    }
                }
                return null;
            });

            results.Check("RectangleF.Intersects: overlapping, edge-touching, and disjoint cases", () =>
            {
                var a = new RectangleF(0f, 0f, 10f, 10f);
                var overlapping = new RectangleF(5f, 5f, 10f, 10f);
                var edgeTouching = new RectangleF(10f, 0f, 10f, 10f); // shares exactly the right edge -- not an overlap
                var disjoint = new RectangleF(20f, 20f, 5f, 5f);

                if (!a.Intersects(overlapping)) return "expected overlapping rects to intersect";
                if (a.Intersects(edgeTouching)) return "edge-touching rects should NOT count as intersecting (matches Rectangle.Intersects)";
                if (a.Intersects(disjoint)) return "expected disjoint rects to not intersect";
                return null;
            });

            results.Check("RectangleF.Inflate grows symmetrically around the same center", () =>
            {
                var r = new RectangleF(10f, 10f, 20f, 20f);
                Vector2 centerBefore = r.Center;
                var inflated = r.Inflate(5f, 3f);
                if (Vector2.Distance(inflated.Center, centerBefore) > 1e-4f) return $"center moved: {centerBefore} -> {inflated.Center}";
                if (inflated.Width != 30f || inflated.Height != 26f) return $"unexpected size: {inflated.Width}x{inflated.Height}";
                return null;
            });

            results.Check("RectangleF.Intersect/Union: correct overlap and bounding box, Empty for disjoint intersection", () =>
            {
                var a = new RectangleF(0f, 0f, 10f, 10f);
                var b = new RectangleF(5f, 5f, 10f, 10f);
                var intersection = RectangleF.Intersect(a, b);
                if (intersection.X != 5f || intersection.Y != 5f || intersection.Width != 5f || intersection.Height != 5f)
                    return $"unexpected intersection: {intersection}";

                var union = RectangleF.Union(a, b);
                if (union.X != 0f || union.Y != 0f || union.Width != 15f || union.Height != 15f)
                    return $"unexpected union: {union}";

                var disjointIntersection = RectangleF.Intersect(a, new RectangleF(100f, 100f, 5f, 5f));
                if (!disjointIntersection.IsEmpty) return $"expected Empty for disjoint rects, got {disjointIntersection}";
                return null;
            });

            results.Check("RectangleF <-> Rectangle conversions: implicit widen is exact, ToRectangle rounds", () =>
            {
                var rect = new Rectangle(3, 4, 5, 6);
                RectangleF widened = rect; // implicit
                if (widened.X != 3f || widened.Y != 4f || widened.Width != 5f || widened.Height != 6f)
                    return $"implicit conversion lost data: {widened}";

                var rounded = new RectangleF(3.6f, 4.4f, 5.5f, 6.5f).ToRectangle();
                if (rounded != new Rectangle(4, 4, 6, 6)) // MathF.Round defaults to MidpointRounding.ToEven: both .5 cases (5.5, 6.5) round to the even neighbor, 6
                    return $"unexpected rounding: {rounded}";
                return null;
            });

            results.Check("RectangleF equality and IsEmpty", () =>
            {
                var a = new RectangleF(1f, 2f, 3f, 4f);
                var b = new RectangleF(1f, 2f, 3f, 4f);
                var c = new RectangleF(1f, 2f, 3f, 5f);
                if (!(a == b) || a != b) return "equal rectangles compared unequal";
                if (a == c || !(a != c)) return "different rectangles compared equal";
                if (!RectangleF.Empty.IsEmpty) return "RectangleF.Empty.IsEmpty should be true";
                if (new RectangleF(0, 0, 5, 5).IsEmpty) return "a real rectangle should not be IsEmpty";
                return null;
            });

            results.Check("RectangleF.Lerp interpolates each field independently, exact at t=0/1, extrapolates outside [0,1]", () =>
            {
                var a = new RectangleF(0f, 0f, 10f, 20f);
                var b = new RectangleF(100f, 200f, 50f, 60f);

                if (RectangleF.Lerp(a, b, 0f) != a) return $"t=0 should equal a exactly, got {RectangleF.Lerp(a, b, 0f)}";
                if (RectangleF.Lerp(a, b, 1f) != b) return $"t=1 should equal b exactly, got {RectangleF.Lerp(a, b, 1f)}";

                var mid = RectangleF.Lerp(a, b, 0.5f);
                var expectedMid = new RectangleF(50f, 100f, 30f, 40f);
                if (mid != expectedMid) return $"t=0.5 expected {expectedMid}, got {mid}";

                var extrapolated = RectangleF.Lerp(a, b, 2f);
                var expectedExtrapolated = new RectangleF(200f, 400f, 90f, 100f);
                if (extrapolated != expectedExtrapolated) return $"t=2 (extrapolation) expected {expectedExtrapolated}, got {extrapolated}";
                return null;
            });
        }
    }
}
