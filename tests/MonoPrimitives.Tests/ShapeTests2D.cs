using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives.Primitives2D;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Renders every major 2D shape family (Fill/Border/Draw) to an offscreen target and samples
    /// real pixels — the 2D counterpart to <see cref="ShapeTests3D"/>, but stricter: 3D only checks
    /// "did this throw and emit geometry" (a real <see cref="GraphicsDevice"/> lets it go further
    /// cheaply, since 2D already renders to a small target and reads pixels back elsewhere in this
    /// project). Every check confirms a known-interior point is the fill color, a known-exterior
    /// point is still background, and — for Border-only shapes — that the shape's own center is
    /// NOT filled (only the outline is). This is the coverage gap this project's own docs named
    /// directly: 2D had zero automated protection against a shape silently rendering in the wrong
    /// place, the wrong size, or not at all, unlike 3D's smoke tests and the couple of deep 2D/3D
    /// join-gap regression tests elsewhere in this file.
    /// </summary>
    internal static class ShapeTests2D
    {
        private const int Size = 200;
        private static readonly Vector2 Center = new(Size / 2f, Size / 2f);
        private static readonly Vector2 Corner = new(4f, 4f); // well outside every shape drawn below

        private static Color[] RenderToPixels(GraphicsDevice device, Primitive2DBatch batch, Action draw)
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

        private static Color At(Color[] pixels, Vector2 p) => pixels[(int)p.Y * Size + (int)p.X];

        private static bool CloseTo(Color a, Color b, int tolerance = 20)
            => Math.Abs(a.R - b.R) <= tolerance && Math.Abs(a.G - b.G) <= tolerance && Math.Abs(a.B - b.B) <= tolerance;

        /// <summary>Renders <paramref name="draw"/>, checks <paramref name="insidePoint"/> reads as <paramref name="fillColor"/> and <see cref="Corner"/> is still background.</summary>
        private static void CheckFilled(TestResults results, string name, GraphicsDevice device, Primitive2DBatch batch, Action draw, Vector2 insidePoint, Color fillColor)
        {
            results.Check(name, () =>
            {
                var pixels = RenderToPixels(device, batch, draw);
                Color inside = At(pixels, insidePoint);
                Color outside = At(pixels, Corner);
                if (!CloseTo(inside, fillColor)) return $"expected {fillColor} at {insidePoint}, got {inside}";
                if (!CloseTo(outside, Color.Black)) return $"expected background at the corner (nothing should reach there), got {outside}";
                return null;
            });
        }

        /// <summary>Renders <paramref name="draw"/> (a Border-only call), checks the shape's own center stays background (nothing filled) while <paramref name="onBorderPoint"/> reads as <paramref name="borderColor"/>.</summary>
        private static void CheckBorderOnly(TestResults results, string name, GraphicsDevice device, Primitive2DBatch batch, Action draw, Vector2 onBorderPoint, Color borderColor)
        {
            results.Check(name, () =>
            {
                var pixels = RenderToPixels(device, batch, draw);
                Color center = At(pixels, Center);
                Color border = At(pixels, onBorderPoint);
                if (!CloseTo(center, Color.Black)) return $"expected the center to stay background (Border draws no fill), got {center}";
                if (!CloseTo(border, borderColor)) return $"expected {borderColor} on the border stroke at {onBorderPoint}, got {border}";
                return null;
            });
        }

        public static void Run(GraphicsDevice device, TestResults results)
        {
            using var batch = new Primitive2DBatch(device);

            // ---- Triangle ----
            Vector2 t1 = Center + new Vector2(0, -60), t2 = Center + new Vector2(-60, 60), t3 = Center + new Vector2(60, 60);
            Vector2 triCentroid = (t1 + t2 + t3) / 3f;
            // A point a few px inward from the t1-t2 edge's own midpoint, toward the triangle's
            // centroid -- robust for any edge orientation, unlike a fixed axis-aligned offset
            // (which only happens to land on the stroke for an axis-aligned edge).
            Vector2 t1t2Inward = Vector2.Lerp((t1 + t2) / 2f, triCentroid, 0.08f);
            CheckFilled(results, "FillTriangle", device, batch, () => batch.FillTriangle(t1, t2, t3, Color.Red), Center, Color.Red);
            CheckBorderOnly(results, "BorderTriangle", device, batch, () => batch.BorderTriangle(t1, t2, t3, Color.Red, thickness: 4f), t1t2Inward, Color.Red);
            CheckFilled(results, "DrawTriangle", device, batch, () => batch.DrawTriangle(t1, t2, t3, Color.Red, Color.Yellow, thickness: 4f), Center, Color.Red);

            // ---- Triangle (equilateral, center+radius overload) ----
            CheckFilled(results, "FillTriangle (center, radius)", device, batch, () => batch.FillTriangle(Center, 50f, Color.Green), Center, Color.Green);

            // ---- Rectangle ----
            var rect = new Rectangle((int)Center.X - 50, (int)Center.Y - 30, 100, 60);
            CheckFilled(results, "FillRectangle", device, batch, () => batch.FillRectangle(rect, Color.Blue), Center, Color.Blue);
            CheckBorderOnly(results, "BorderRectangle", device, batch, () => batch.BorderRectangle(rect, Color.Blue, thickness: 4f), new Vector2(rect.X + 2, Center.Y), Color.Blue);
            CheckFilled(results, "DrawRectangle", device, batch, () => batch.DrawRectangle(rect, Color.Blue, Color.White, thickness: 4f), Center, Color.Blue);
            CheckFilled(results, "FillRectangleRounded", device, batch, () => batch.FillRectangleRounded(rect, 12f, Color.Blue), Center, Color.Blue);
            CheckFilled(results, "FillRectangle (4-independent-corner-color)", device, batch, () => batch.FillRectangle(rect, Color.Red, Color.Green, Color.Blue, Color.Yellow), Center, new Color((byte)((Color.Red.R + Color.Blue.R) / 2), (byte)((Color.Red.G + Color.Blue.G) / 2), (byte)((Color.Red.B + Color.Blue.B) / 2)));

            // ---- Circle ----
            CheckFilled(results, "FillCircle", device, batch, () => batch.FillCircle(Center, 50f, Color.Orange), Center, Color.Orange);
            CheckBorderOnly(results, "BorderCircle", device, batch, () => batch.BorderCircle(Center, 50f, Color.Orange, thickness: 4f), Center + new Vector2(0, -49f), Color.Orange);
            CheckFilled(results, "DrawCircle", device, batch, () => batch.DrawCircle(Center, 50f, Color.Orange, Color.White, thickness: 4f), Center, Color.Orange);

            // ---- Ellipse ----
            CheckFilled(results, "FillEllipse", device, batch, () => batch.FillEllipse(Center, 60f, 30f, Color.Purple), Center, Color.Purple);
            CheckBorderOnly(results, "BorderEllipse", device, batch, () => batch.BorderEllipse(Center, 60f, 30f, Color.Purple, thickness: 4f), Center + new Vector2(-59f, 0), Color.Purple);

            // ---- Poly (regular N-gon) ----
            CheckFilled(results, "FillPoly (hexagon)", device, batch, () => batch.FillPoly(Center, 6, 50f, Color.Cyan), Center, Color.Cyan);
            CheckBorderOnly(results, "BorderPoly (hexagon)", device, batch, () => batch.BorderPoly(Center, 6, 50f, Color.Cyan, thickness: 4f), Center + new Vector2(49f, 0), Color.Cyan);
            CheckFilled(results, "DrawPoly (hexagon)", device, batch, () => batch.DrawPoly(Center, 6, 50f, Color.Cyan, Color.White, thickness: 4f), Center, Color.Cyan);

            // ---- Polygon (arbitrary point list -- a plus/cross shape, deliberately non-convex to exercise ear-clipping) ----
            Vector2[] plus =
            {
                Center + new Vector2(-15, -50), Center + new Vector2(15, -50), Center + new Vector2(15, -15),
                Center + new Vector2(50, -15), Center + new Vector2(50, 15), Center + new Vector2(15, 15),
                Center + new Vector2(15, 50), Center + new Vector2(-15, 50), Center + new Vector2(-15, 15),
                Center + new Vector2(-50, 15), Center + new Vector2(-50, -15), Center + new Vector2(-15, -15),
            };
            CheckFilled(results, "FillPolygon (concave plus-shape, ear-clipping path)", device, batch, () => batch.FillPolygon(plus, Color.Magenta), Center, Color.Magenta);
            CheckBorderOnly(results, "BorderPolygon (concave plus-shape)", device, batch, () => batch.BorderPolygon(plus, Color.Magenta, thickness: 4f), Center + new Vector2(0, -49f), Color.Magenta);

            // ---- Capsule ----
            Vector2 capStart = Center + new Vector2(-40, 0), capEnd = Center + new Vector2(40, 0);
            CheckFilled(results, "FillCapsule", device, batch, () => batch.FillCapsule(capStart, capEnd, 25f, Color.Gray), Center, Color.Gray);
            CheckBorderOnly(results, "BorderCapsule", device, batch, () => batch.BorderCapsule(capStart, capEnd, 25f, Color.Gray, thickness: 4f), Center + new Vector2(0, -24f), Color.Gray);

            // ---- CircleSector (a quarter circle, 0 to 0.25 turns) ----
            // Turns follow SampleUnitCircle's (cos(2*pi*t), sin(2*pi*t)) convention in Y-down pixel
            // space: t=0 points +X (right), t=0.25 points +Y (down) -- so 0->0.25 sweeps the
            // bottom-right quadrant, not up-right.
            Vector2 sectorInside = Center + new Vector2(20, 20);
            CheckFilled(results, "FillCircleSector (quarter)", device, batch, () => batch.FillCircleSector(Center, 50f, 0f, 0.25f, Color.Lime), sectorInside, Color.Lime);

            // ---- Ring ----
            Vector2 ringInside = Center + new Vector2(40, 0); // between inner (30) and outer (50) radius
            CheckFilled(results, "FillRing", device, batch, () => batch.FillRing(Center, 30f, 50f, Color.Yellow), ringInside, Color.Yellow);
            results.Check("FillRing: the hole stays background", () =>
            {
                var pixels = RenderToPixels(device, batch, () => batch.FillRing(Center, 30f, 50f, Color.Yellow));
                Color hole = At(pixels, Center); // dead center, inside the inner radius -- should NOT be filled
                return CloseTo(hole, Color.Black) ? null : $"expected the ring's inner hole to stay background, got {hole}";
            });
        }
    }
}
