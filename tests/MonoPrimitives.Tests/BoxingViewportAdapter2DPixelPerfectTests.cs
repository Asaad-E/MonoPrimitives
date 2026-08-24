using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives.Primitives2D;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Coverage for <c>BoxingViewportAdapter2D</c>'s <c>pixelPerfect</c> option: floors the
    /// continuous fit-scale to a whole number (minimum 1) so pixel art scales by an identical
    /// integer factor everywhere instead of some source pixels landing 2 screen pixels wide and
    /// others 3. Uses the real (whatever it is) test-runner window size and picks virtual
    /// resolutions relative to it, rather than assuming a fixed window size.
    /// </summary>
    internal static class BoxingViewportAdapter2DPixelPerfectTests
    {
        public static void Run(GraphicsDevice device, TestResults results)
        {
            int bw = device.PresentationParameters.BackBufferWidth;
            int bh = device.PresentationParameters.BackBufferHeight;

            results.Check("BoxingViewportAdapter2D (default): Scale is unaffected by the new parameter's default", () =>
            {
                var adapter = new BoxingViewportAdapter2D(device, bw / 2, bh / 2);
                float expected = MathF.Min((float)bw / (bw / 2), (float)bh / (bh / 2));
                if (MathF.Abs(adapter.Scale.X - expected) > 1e-4f) return $"expected continuous scale ~{expected}, got {adapter.Scale.X}";
                if (adapter.PixelPerfect) return "PixelPerfect should default to false";
                return null;
            });

            results.Check("BoxingViewportAdapter2D (pixelPerfect): an already-exact-integer fit is unchanged", () =>
            {
                // A virtual resolution that divides the real backbuffer size exactly (scale = 2.0 on
                // the tighter axis) should behave identically whether pixelPerfect is on or off --
                // nothing to floor away.
                int virtualWidth = bw / 2;
                int virtualHeight = bh / 2;
                var continuous = new BoxingViewportAdapter2D(device, virtualWidth, virtualHeight, pixelPerfect: false);
                var perfect = new BoxingViewportAdapter2D(device, virtualWidth, virtualHeight, pixelPerfect: true);

                if (MathF.Abs(continuous.Scale.X - perfect.Scale.X) > 1e-4f)
                    return $"expected matching scale for an exact-integer fit, got continuous={continuous.Scale.X}, pixelPerfect={perfect.Scale.X}";

                Rectangle rect = perfect.BoundingRectangle;
                bool matchesOneAxisExactly = rect.Width == bw || rect.Height == bh;
                if (!matchesOneAxisExactly) return $"expected the exact-fit case to still fill one axis exactly, got {rect} in a {bw}x{bh} window";
                return null;
            });

            results.Check("BoxingViewportAdapter2D (pixelPerfect): a fractional fit floors to a whole number and borders BOTH axes", () =>
            {
                // Choose a virtual size deliberately NOT a clean divisor, so the continuous scale
                // has a fractional part -- e.g. slightly smaller than an exact half, so the
                // continuous scale is a bit above 2.0 and floors down to exactly 2.
                int virtualWidth = (int)(bw / 2.3f);
                int virtualHeight = (int)(bh / 2.3f);
                var continuous = new BoxingViewportAdapter2D(device, virtualWidth, virtualHeight, pixelPerfect: false);
                var perfect = new BoxingViewportAdapter2D(device, virtualWidth, virtualHeight, pixelPerfect: true);

                if (continuous.Scale.X == MathF.Floor(continuous.Scale.X))
                    return $"test setup bug: expected a fractional continuous scale, got exactly {continuous.Scale.X}";

                float expectedFloor = MathF.Floor(continuous.Scale.X);
                if (perfect.Scale.X != expectedFloor) return $"expected pixelPerfect scale floored to {expectedFloor}, got {perfect.Scale.X}";
                if (perfect.Scale.X >= continuous.Scale.X) return $"pixelPerfect scale ({perfect.Scale.X}) should be strictly less than the continuous fit ({continuous.Scale.X})";

                Rectangle rect = perfect.BoundingRectangle;
                // The whole point: BOTH axes now have leftover space, not just the one a continuous
                // fit would have left bars on.
                if (rect.Width >= bw) return $"expected a gap on the width axis too, got {rect} in a {bw}x{bh} window";
                if (rect.Height >= bh) return $"expected a gap on the height axis too, got {rect} in a {bw}x{bh} window";
                return null;
            });

            results.Check("BoxingViewportAdapter2D (pixelPerfect): never floors below 1x, even for a virtual resolution larger than the window", () =>
            {
                var adapter = new BoxingViewportAdapter2D(device, bw * 3, bh * 3, pixelPerfect: true);
                if (adapter.Scale.X < 1f) return $"expected a minimum scale of 1, got {adapter.Scale.X}";
                if (adapter.Scale.X != 1f) return $"expected exactly 1 for a virtual resolution this much larger than the window, got {adapter.Scale.X}";
                return null;
            });

            results.Check("BoxingViewportAdapter2D (pixelPerfect): Offset still centers the (possibly all-4-sides-bordered) content", () =>
            {
                int virtualWidth = (int)(bw / 2.3f);
                int virtualHeight = (int)(bh / 2.3f);
                var adapter = new BoxingViewportAdapter2D(device, virtualWidth, virtualHeight, pixelPerfect: true);
                Rectangle rect = adapter.BoundingRectangle;

                int leftGap = rect.X;
                int rightGap = bw - rect.Right;
                int topGap = rect.Y;
                int bottomGap = bh - rect.Bottom;

                if (Math.Abs(leftGap - rightGap) > 1) return $"not horizontally centered: left={leftGap}, right={rightGap}";
                if (Math.Abs(topGap - bottomGap) > 1) return $"not vertically centered: top={topGap}, bottom={bottomGap}";
                return null;
            });

            results.Check("BoxingViewportAdapter2D (pixelPerfect): a camera-drawn shape still lands at the boxed area's true center", () =>
            {
                int virtualWidth = (int)(bw / 2.3f);
                int virtualHeight = (int)(bh / 2.3f);

                using var rt = new RenderTarget2D(device, bw, bh);
                using var batch = new Primitive2DBatch(device);

                device.SetRenderTarget(rt);
                var adapter = new BoxingViewportAdapter2D(device, virtualWidth, virtualHeight, pixelPerfect: true);
                var camera = new Camera2D(adapter) { Offset = Vector2.Zero };

                batch.ClearLetterboxed(adapter, Color.Black, Color.CornflowerBlue);
                batch.Begin(camera.GetTransformMatrix());
                batch.FillCircle(new Vector2(virtualWidth / 2f, virtualHeight / 2f), MathF.Min(virtualWidth, virtualHeight) * 0.3f, Color.Red);
                batch.End();
                device.SetRenderTarget(null);

                var pixels = new Color[bw * bh];
                rt.GetData(pixels);
                Color At(int x, int y) => pixels[Math.Clamp(y, 0, bh - 1) * bw + Math.Clamp(x, 0, bw - 1)];

                Rectangle boxed = adapter.BoundingRectangle;
                int expectedCx = boxed.X + boxed.Width / 2;
                int expectedCy = boxed.Y + boxed.Height / 2;

                long sumX = 0, sumY = 0, count = 0;
                for (int y = 0; y < bh; y += 2)
                {
                    for (int x = 0; x < bw; x += 2)
                    {
                        Color c = At(x, y);
                        if (c.R > 150 && c.G < 80 && c.B < 80) { sumX += x; sumY += y; count++; }
                    }
                }
                if (count == 0) return "no red pixels found -- did FillCircle draw anything?";

                int actualCx = (int)(sumX / count);
                int actualCy = (int)(sumY / count);
                int dx = Math.Abs(actualCx - expectedCx);
                int dy = Math.Abs(actualCy - expectedCy);
                if (dx >= 5 || dy >= 5) return $"circle centroid ({actualCx},{actualCy}) is off the boxed area's center ({expectedCx},{expectedCy}) by ({dx},{dy})";
                return null;
            });
        }
    }
}
