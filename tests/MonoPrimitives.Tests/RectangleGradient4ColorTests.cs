using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives.Primitives2D;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Renders <c>Primitive2DBatch.FillRectangle</c>'s 4-independent-corner-color overload to an
    /// offscreen target and samples near each corner, confirming each one is dominated by its own
    /// assigned color (not, say, all 4 vertices accidentally wired to the same 2 triangles' shared
    /// color, or the corners shuffled) -- geometry correctness that only shows up once the colors
    /// actually reach the GPU, same reasoning as this file's sibling render-based tests. Also covers
    /// the rotation support added alongside the pre-existing (rotation-less) 4-corner overload.
    /// </summary>
    internal static class RectangleGradient4ColorTests
    {
        public static void Run(GraphicsDevice device, TestResults results)
        {
            results.Check("FillRectangle (4-corner): each corner is dominated by its own assigned color", () =>
            {
                const int size = 100;
                using var rt = new RenderTarget2D(device, size, size);
                using var batch = new Primitive2DBatch(device);

                device.SetRenderTarget(rt);
                device.Clear(Color.Black);
                batch.Begin();
                batch.FillRectangle(0f, 0f, size, size, Color.Red, Color.Lime, Color.Blue, Color.Yellow);
                batch.End();
                device.SetRenderTarget(null);

                var pixels = new Color[size * size];
                rt.GetData(pixels);
                Color At(int x, int y) => pixels[y * size + x];

                const int inset = 5;
                Color topLeft = At(inset, inset);
                Color topRight = At(size - 1 - inset, inset);
                Color bottomRight = At(size - 1 - inset, size - 1 - inset);
                Color bottomLeft = At(inset, size - 1 - inset);

                if (!(topLeft.R > 180 && topLeft.G < 80 && topLeft.B < 80))
                    return $"top-left should read red-dominant, got {topLeft}";
                if (!(topRight.G > 180 && topRight.R < 80 && topRight.B < 80))
                    return $"top-right should read green-dominant, got {topRight}";
                if (!(bottomRight.B > 180 && bottomRight.R < 80 && bottomRight.G < 80))
                    return $"bottom-right should read blue-dominant, got {bottomRight}";
                if (!(bottomLeft.R > 180 && bottomLeft.G > 180 && bottomLeft.B < 80))
                    return $"bottom-left should read yellow-dominant (red+green), got {bottomLeft}";

                // The quad is 2 triangles sharing the top-left -> bottom-right diagonal (standard
                // GPU-quad practice, same as a texture-mapped quad), so the exact center sits ON
                // that diagonal -- its color is a pure blend of ONLY those 2 corners (Red, Blue),
                // not an average of all 4. This is a real, expected property of the technique (not
                // true per-pixel bilinear across all 4 corners), confirmed here instead of assumed:
                // an "average of all 4" expectation would fail this exact check (G would read ~0,
                // not ~128, since Lime/Yellow's green never reaches the diagonal at all).
                Color center = At(size / 2, size / 2);
                float expectedR = (Color.Red.R + Color.Blue.R) / 2f;
                float expectedG = (Color.Red.G + Color.Blue.G) / 2f;
                float expectedB = (Color.Red.B + Color.Blue.B) / 2f;
                if (System.MathF.Abs(center.R - expectedR) > 15 || System.MathF.Abs(center.G - expectedG) > 15 || System.MathF.Abs(center.B - expectedB) > 15)
                    return $"center {center} should be a pure Red/Blue blend ({expectedR:F0},{expectedG:F0},{expectedB:F0}) -- it sits on the triangulation's diagonal";

                return null;
            });

            results.Check("FillRectangle (4-corner): a 180-degree rotation swaps opposite corners", () =>
            {
                const int size = 100;
                using var rt = new RenderTarget2D(device, size, size);
                using var batch = new Primitive2DBatch(device);

                device.SetRenderTarget(rt);
                device.Clear(Color.Black);
                batch.Begin();
                batch.FillRectangle(0f, 0f, size, size, Color.Red, Color.Lime, Color.Blue, Color.Yellow, rotation: MathF.PI);
                batch.End();
                device.SetRenderTarget(null);

                var pixels = new Color[size * size];
                rt.GetData(pixels);
                Color At(int x, int y) => pixels[y * size + x];

                const int inset = 5;
                // Rotated 180 degrees about the rect's own center: the color originally assigned to
                // topLeft now renders at the screen's bottom-right corner, and so on for each pair.
                Color screenTopLeft = At(inset, inset);
                Color screenBottomRight = At(size - 1 - inset, size - 1 - inset);

                if (!(screenTopLeft.B > 180 && screenTopLeft.R < 80 && screenTopLeft.G < 80))
                    return $"expected the (originally bottom-right) blue corner at screen top-left after a 180-degree spin, got {screenTopLeft}";
                if (!(screenBottomRight.R > 180 && screenBottomRight.G < 80 && screenBottomRight.B < 80))
                    return $"expected the (originally top-left) red corner at screen bottom-right after a 180-degree spin, got {screenBottomRight}";

                return null;
            });
        }
    }
}
