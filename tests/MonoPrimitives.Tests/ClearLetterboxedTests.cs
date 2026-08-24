using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives.Primitives2D;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Regression coverage for the bug shipped in 0.5.6, fixed in 0.5.7 (see DECISIONS.md):
    /// <c>ClearLetterboxed</c> left the device viewport narrowed (<c>Apply()</c>'d) when it
    /// returned, so a following <c>Begin(camera.GetTransformMatrix())</c> double-applied the
    /// letterboxing adapter's offset — content rendered cut off and off-center instead of centered
    /// in the boxed area. Renders the exact sequence a real caller uses (letterbox clear, then a
    /// camera-transformed draw) to an offscreen target sized like a real window, and checks a shape
    /// drawn at the virtual center actually lands at the boxed area's true center on screen.
    /// </summary>
    internal static class ClearLetterboxedTests
    {
        public static void Run(GraphicsDevice device, TestResults results)
        {
            results.Check("ClearLetterboxed leaves the viewport correct for a following Begin(camera.GetTransformMatrix())", () =>
            {
                const int windowSize = 800;
                const int virtualWidth = 1920;
                const int virtualHeight = 1080;

                using var rt = new RenderTarget2D(device, windowSize, windowSize);
                using var batch = new Primitive2DBatch(device);

                device.SetRenderTarget(rt);
                var adapter = new BoxingViewportAdapter2D(device, virtualWidth, virtualHeight);
                var camera = new Camera2D(adapter) { Offset = Vector2.Zero };

                batch.ClearLetterboxed(adapter, Color.Black, Color.CornflowerBlue);
                batch.Begin(camera.GetTransformMatrix());
                batch.FillCircleGradient(new Vector2(virtualWidth / 2f, virtualHeight / 2f), 150f, Color.Yellow, Color.Red);
                batch.End();
                device.SetRenderTarget(null);

                var pixels = new Color[windowSize * windowSize];
                rt.GetData(pixels);
                Color At(int x, int y) => pixels[Math.Clamp(y, 0, windowSize - 1) * windowSize + Math.Clamp(x, 0, windowSize - 1)];

                Rectangle boxed = adapter.BoundingRectangle;
                int expectedCx = boxed.X + boxed.Width / 2;
                int expectedCy = boxed.Y + boxed.Height / 2;

                // Locate the actual centroid of the drawn circle (anything reddish/yellowish, not
                // the bars or the boxed background) instead of trusting a single sample point --
                // more robust, and it directly measures "where did the shape actually end up."
                long sumX = 0, sumY = 0, count = 0;
                for (int y = 0; y < windowSize; y += 2)
                {
                    for (int x = 0; x < windowSize; x += 2)
                    {
                        Color c = At(x, y);
                        bool isCircleish = c.R > 150 && c.G < 220 && c != Color.Black && c != Color.CornflowerBlue;
                        if (isCircleish) { sumX += x; sumY += y; count++; }
                    }
                }

                if (count == 0) return "no circle-colored pixels found at all -- did FillCircleGradient draw anything?";

                int actualCx = (int)(sumX / count);
                int actualCy = (int)(sumY / count);
                int dx = Math.Abs(actualCx - expectedCx);
                int dy = Math.Abs(actualCy - expectedCy);

                if (dx >= 15 || dy >= 15)
                    return $"circle centroid ({actualCx},{actualCy}) is {dx}px/{dy}px off the boxed area's true center ({expectedCx},{expectedCy}) -- the viewport offset is being double-applied again";

                return null;
            });
        }
    }
}
