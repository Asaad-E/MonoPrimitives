using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives.Primitives2D;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Coverage for <c>CoverViewportAdapter2D</c>: the inverse of <c>BoxingViewportAdapter2D</c>
    /// (fills the window completely instead of showing bars, cropping overflow instead). Uses the
    /// real test-runner window size and picks virtual resolutions relative to it, same convention
    /// as <see cref="BoxingViewportAdapter2DPixelPerfectTests"/>.
    /// </summary>
    internal static class CoverViewportAdapter2DTests
    {
        public static void Run(GraphicsDevice device, TestResults results)
        {
            int bw = device.PresentationParameters.BackBufferWidth;
            int bh = device.PresentationParameters.BackBufferHeight;

            results.Check("CoverViewportAdapter2D: Scale is the LARGER of the two continuous fits (opposite of Boxing's smaller)", () =>
            {
                // A virtual resolution deliberately not proportional to the window, so scaleX != scaleY.
                int vw = bw / 2;
                int vh = bh / 3;
                var cover = new CoverViewportAdapter2D(device, vw, vh);
                var boxing = new BoxingViewportAdapter2D(device, vw, vh);

                float scaleX = (float)bw / vw, scaleY = (float)bh / vh;
                float expectedCover = MathF.Max(scaleX, scaleY);
                float expectedBoxing = MathF.Min(scaleX, scaleY);

                if (MathF.Abs(cover.Scale.X - expectedCover) > 1e-4f) return $"expected Cover's scale ~{expectedCover}, got {cover.Scale.X}";
                if (MathF.Abs(boxing.Scale.X - expectedBoxing) > 1e-4f) return "test setup bug: Boxing's own scale didn't match the expected smaller fit";
                if (cover.Scale.X <= boxing.Scale.X) return "Cover's fill scale should always be >= Boxing's fit scale for the same virtual size";
                return null;
            });

            results.Check("CoverViewportAdapter2D: BoundingRectangle completely covers the window (width/height >= backbuffer, offset <= 0 on the cropped axis)", () =>
            {
                int vw = bw / 2;
                int vh = bh / 3; // deliberately disproportionate, so one axis crops
                var cover = new CoverViewportAdapter2D(device, vw, vh);
                Rectangle rect = cover.BoundingRectangle;

                if (rect.Width < bw) return $"expected BoundingRectangle.Width ({rect.Width}) >= backbuffer width ({bw}) -- Cover must never leave a horizontal gap";
                if (rect.Height < bh) return $"expected BoundingRectangle.Height ({rect.Height}) >= backbuffer height ({bh}) -- Cover must never leave a vertical gap";
                if (rect.X > 0) return $"expected a non-positive X (content starts at or before the window's own left edge), got {rect.X}";
                if (rect.Y > 0) return $"expected a non-positive Y (content starts at or before the window's own top edge), got {rect.Y}";
                return null;
            });

            results.Check("CoverViewportAdapter2D: Offset centers the crop symmetrically", () =>
            {
                int vw = bw / 2;
                int vh = bh / 3;
                var cover = new CoverViewportAdapter2D(device, vw, vh);
                Rectangle rect = cover.BoundingRectangle;

                int leftOverflow = -rect.X;
                int rightOverflow = rect.Right - bw;
                int topOverflow = -rect.Y;
                int bottomOverflow = rect.Bottom - bh;

                if (Math.Abs(leftOverflow - rightOverflow) > 1) return $"not horizontally centered: left overflow={leftOverflow}, right overflow={rightOverflow}";
                if (Math.Abs(topOverflow - bottomOverflow) > 1) return $"not vertically centered: top overflow={topOverflow}, bottom overflow={bottomOverflow}";
                return null;
            });

            results.Check("CoverViewportAdapter2D: an exact-aspect-ratio virtual size needs no cropping, matches Boxing exactly", () =>
            {
                int vw = bw / 2;
                int vh = bh / 2; // same aspect ratio as the window itself -- nothing to crop or bar
                var cover = new CoverViewportAdapter2D(device, vw, vh);
                var boxing = new BoxingViewportAdapter2D(device, vw, vh);

                if (MathF.Abs(cover.Scale.X - boxing.Scale.X) > 1e-4f) return $"expected matching scale when aspect ratios agree, got cover={cover.Scale.X} boxing={boxing.Scale.X}";
                if (cover.BoundingRectangle != boxing.BoundingRectangle) return $"expected matching bounds when aspect ratios agree, got cover={cover.BoundingRectangle} boxing={boxing.BoundingRectangle}";
                return null;
            });

            results.Check("CoverViewportAdapter2D: Apply() doesn't throw even with a negative-offset, overflowing rectangle", () =>
            {
                int vw = bw / 2;
                int vh = bh / 3;
                var cover = new CoverViewportAdapter2D(device, vw, vh);
                cover.Apply();
                cover.Reset();
                return null;
            });

            results.Check("CoverViewportAdapter2D: a rendered fill covers every pixel of the window, no background showing through", () =>
            {
                int vw = bw / 2;
                int vh = bh / 3;
                var cover = new CoverViewportAdapter2D(device, vw, vh);
                var camera = new Camera2D(cover, target: new Vector2(vw / 2f, vh / 2f), zoom: 1f);

                using var rt = new RenderTarget2D(device, bw, bh);
                using var batch = new Primitive2DBatch(device);

                device.SetRenderTarget(rt);
                device.Clear(Color.Black); // background -- must be fully overdrawn if Cover really fills the window
                batch.Begin(camera.GetTransformMatrix());
                batch.FillRectangle(0, 0, vw, vh, Color.CornflowerBlue);
                batch.End();
                device.SetRenderTarget(null);

                var pixels = new Color[bw * bh];
                rt.GetData(pixels);
                foreach (Color p in pixels)
                    if (p == Color.Black) return "found a black (background) pixel -- Cover left a gap somewhere instead of filling the whole window";
                return null;
            });

            results.Check("CoverViewportAdapter2D: non-positive virtual size throws", () =>
            {
                try { new CoverViewportAdapter2D(device, 0, 100); return "expected ArgumentOutOfRangeException for width=0"; }
                catch (ArgumentOutOfRangeException) { return null; }
            });
        }
    }
}
