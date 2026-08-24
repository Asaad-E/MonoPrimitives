using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives.Primitives2D;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Regression coverage for the fixed <c>InsetConvexPolygon</c>/<c>OutsetConvexPolygon</c>
    /// reflex-vertex bug (see DECISIONS.md): the old per-vertex "closer to centroid" heuristic only
    /// resolved correctly at a convex vertex, so a concave (reflex) corner could pick the wrong
    /// inset direction and leak the inward stroke out into empty space instead of staying inside the
    /// shape. Renders a real L-shape (one reflex vertex) via <c>BorderPolygon</c> with a
    /// <see cref="LineJoin.Miter"/> join — the exact code path that calls <c>InsetConvexPolygon</c>
    /// directly — and checks the shape's own "notch" (the region outside the L but inside its
    /// bounding box, right next to the reflex vertex) never gets painted.
    /// </summary>
    internal static class PolygonInsetRegressionTests
    {
        private const int Size = 200;

        // An L-shape: a horizontal bar (x:20-180, y:20-80) unioned with a vertical bar (x:20-80,
        // y:20-180). One reflex vertex, at (80,80). The "notch" -- outside the L but inside its
        // bounding box -- is roughly x:80-180, y:80-180.
        private static readonly Vector2[] LShape =
        {
            new(20, 20), new(180, 20), new(180, 80), new(80, 80), new(80, 180), new(20, 180),
        };

        private static Color[] RenderToPixels(GraphicsDevice device, Primitive2DBatch batch, System.Action draw)
        {
            using var rt = new RenderTarget2D(device, Size, Size);
            device.SetRenderTarget(rt);
            device.Clear(Color.Black);
            batch.Begin();
            draw();
            batch.End();
            device.SetRenderTarget(null);

            var pixels = new Color[Size * Size];
            rt.GetData(pixels);
            return pixels;
        }

        private static Color At(Color[] pixels, int x, int y) => pixels[y * Size + x];

        public static void Run(GraphicsDevice device, TestResults results)
        {
            using var batch = new Primitive2DBatch(device);

            results.Check("FillPolygon (L-shape): the notch outside the shape stays background", () =>
            {
                var pixels = RenderToPixels(device, batch, () => batch.FillPolygon(LShape, Color.Green));
                Color inside = At(pixels, 50, 50);
                Color notch = At(pixels, 130, 130);
                if (!IsGreenish(inside)) return $"expected the L's own body filled at (50,50), got {inside}";
                if (IsGreenish(notch)) return $"the notch outside the L (130,130) should stay background, got {notch}";
                return null;
            });

            results.Check("BorderPolygon (L-shape, Miter join): the inward stroke never leaks into the notch at the reflex vertex", () =>
            {
                var pixels = RenderToPixels(device, batch, () => batch.BorderPolygon(LShape, Color.Green, thickness: 10f, join: LineJoin.Miter));

                // The historical bug: a wrong inset direction at the reflex vertex (80,80) could
                // paint stroke geometry into the empty notch instead of staying inside the L.
                // Sample a small neighborhood right next to the reflex vertex, on the notch side.
                int leaked = 0, total = 0;
                for (int dx = 2; dx <= 20; dx += 2)
                {
                    for (int dy = 2; dy <= 20; dy += 2)
                    {
                        int x = 80 + dx, y = 80 + dy; // strictly inside the notch quadrant
                        if (x >= Size || y >= Size) continue;
                        total++;
                        if (IsGreenish(At(pixels, x, y))) leaked++;
                    }
                }
                if (total == 0) return "no sample points -- test setup bug";
                if (leaked > 0) return $"{leaked}/{total} sampled notch points near the reflex vertex were painted (expected 0) -- inset direction leaked outward";

                // And confirm the border actually drew something real elsewhere (not a false pass
                // from an empty/no-op draw) -- a point on the top edge's own inward stroke.
                Color onStroke = At(pixels, 100, 25);
                if (!IsGreenish(onStroke)) return $"expected the border stroke on the top edge, got {onStroke} at (100,25) -- did BorderPolygon draw anything at all?";

                return null;
            });
        }

        private static bool IsGreenish(Color c) => c.G > 100 && c.R < 100 && c.B < 100;
    }
}
