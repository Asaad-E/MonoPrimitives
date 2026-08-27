using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>Real-pixel checks for <see cref="TextureUtil"/> -- every generator/transform is verified by reading back actual uploaded/rendered pixels, not just "didn't throw."</summary>
    internal static class TextureUtilTests
    {
        private static Color[] Read(Texture2D t)
        {
            var pixels = new Color[t.Width * t.Height];
            t.GetData(pixels);
            return pixels;
        }

        private static bool CloseColor(Color a, Color b, int tolerance = 4) =>
            Math.Abs(a.R - b.R) <= tolerance && Math.Abs(a.G - b.G) <= tolerance && Math.Abs(a.B - b.B) <= tolerance;

        public static void Run(GraphicsDevice device, TestResults results)
        {
            results.Check("TextureUtil.CreateSolid: every pixel is the given color", () =>
            {
                using var t = TextureUtil.CreateSolid(device, 8, 6, Color.CornflowerBlue);
                foreach (Color p in Read(t))
                    if (p != Color.CornflowerBlue) return $"expected every pixel CornflowerBlue, found {p}";
                return null;
            });

            results.Check("TextureUtil.CreateGradientLinear: endpoints match colorA/colorB, direction actually rotates", () =>
            {
                const int size = 64;
                using var horizontal = TextureUtil.CreateGradientLinear(device, size, size, Color.Black, Color.White, angle: 0f);
                Color[] h = Read(horizontal);
                if (!CloseColor(h[size / 2 * size + 0], Color.Black, 10)) return $"left edge should be near black, got {h[size / 2 * size + 0]}";
                if (!CloseColor(h[size / 2 * size + (size - 1)], Color.White, 10)) return $"right edge should be near white, got {h[size / 2 * size + (size - 1)]}";

                using var vertical = TextureUtil.CreateGradientLinear(device, size, size, Color.Black, Color.White, angle: MathHelper.PiOver2);
                Color[] v = Read(vertical);
                if (!CloseColor(v[0 * size + size / 2], Color.Black, 10)) return $"top edge (angle=90deg) should be near black, got {v[0 * size + size / 2]}";
                if (!CloseColor(v[(size - 1) * size + size / 2], Color.White, 10)) return $"bottom edge (angle=90deg) should be near white, got {v[(size - 1) * size + size / 2]}";
                return null;
            });

            results.Check("TextureUtil.CreateGradientRadial: center is innerColor, corners are outerColor", () =>
            {
                const int size = 65; // odd so there's a true single center pixel
                using var t = TextureUtil.CreateGradientRadial(device, size, size, Color.White, Color.Black);
                Color[] p = Read(t);
                Color center = p[(size / 2) * size + size / 2];
                Color corner = p[0];
                if (!CloseColor(center, Color.White, 15)) return $"center should be near white, got {center}";
                if (!CloseColor(corner, Color.Black, 15)) return $"corner should be near black, got {corner}";
                return null;
            });

            results.Check("TextureUtil.CreateCheckerboard: alternates by cell, not by pixel", () =>
            {
                using var t = TextureUtil.CreateCheckerboard(device, 8, 8, cellSize: 2, Color.Red, Color.Lime);
                Color[] p = Read(t);
                Color At(int x, int y) => p[y * 8 + x];

                if (At(0, 0) != Color.Red) return "cell (0,0) should be colorA";
                if (At(1, 0) != Color.Red) return "cell (0,0) is 2px wide -- (1,0) should still be colorA";
                if (At(2, 0) != Color.Lime) return "cell (1,0) should be colorB";
                if (At(0, 2) != Color.Lime) return "cell (0,1) should be colorB";
                if (At(2, 2) != Color.Red) return "cell (1,1) should be colorA again (checkerboard identity)";
                return null;
            });

            results.Check("TextureUtil.CreateFromNoise: deterministic for the same Noise seed, not a flat color", () =>
            {
                var noiseA = new Noise(seed: 42);
                var noiseB = new Noise(seed: 42);
                using var a = TextureUtil.CreateFromNoise(device, 32, 32, noiseA);
                using var b = TextureUtil.CreateFromNoise(device, 32, 32, noiseB);
                Color[] pa = Read(a), pb = Read(b);

                for (int i = 0; i < pa.Length; i++)
                    if (pa[i] != pb[i]) return $"same seed should reproduce identical pixels, differed at index {i}: {pa[i]} vs {pb[i]}";

                bool allSame = true;
                for (int i = 1; i < pa.Length; i++) if (pa[i] != pa[0]) { allSame = false; break; }
                if (allSame) return "expected real variation across the texture, got a flat color";
                return null;
            });

            results.Check("TextureUtil.Crop: extracts the exact requested sub-rectangle", () =>
            {
                var pixels = new Color[4 * 4];
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++)
                        pixels[y * 4 + x] = (x < 2 && y < 2) ? Color.Red : (x >= 2 && y < 2) ? Color.Lime : (x < 2) ? Color.Blue : Color.Yellow;

                using var source = new Texture2D(device, 4, 4, false, SurfaceFormat.Color);
                source.SetData(pixels);

                using var cropped = TextureUtil.Crop(device, source, new Rectangle(2, 0, 2, 2)); // top-right quadrant (Lime)
                foreach (Color p in Read(cropped))
                    if (p != Color.Lime) return $"expected the cropped top-right quadrant to be pure Lime, got {p}";

                bool threw = false;
                try { TextureUtil.Crop(device, source, new Rectangle(3, 3, 2, 2)); }
                catch (ArgumentOutOfRangeException) { threw = true; }
                if (!threw) return "expected an out-of-bounds crop rectangle to throw ArgumentOutOfRangeException";
                return null;
            });

            results.Check("TextureUtil.FlipHorizontal/FlipVertical: corners swap to the correct side", () =>
            {
                var pixels = new[] { Color.Red, Color.Lime, Color.Blue, Color.Yellow }; // TL, TR, BL, BR in a 2x2
                using var source = new Texture2D(device, 2, 2, false, SurfaceFormat.Color);
                source.SetData(pixels);

                using var flippedH = TextureUtil.FlipHorizontal(device, source);
                Color[] h = Read(flippedH);
                if (h[0] != Color.Lime || h[1] != Color.Red) return $"expected TL/TR swapped after horizontal flip, got {h[0]},{h[1]}";

                using var flippedV = TextureUtil.FlipVertical(device, source);
                Color[] v = Read(flippedV);
                if (v[0] != Color.Blue || v[2] != Color.Red) return $"expected TL/BL swapped after vertical flip, got {v[0]},{v[2]}";
                return null;
            });

            results.Check("TextureUtil.Tint: multiplies a white source exactly to the tint color", () =>
            {
                using var white = TextureUtil.CreateSolid(device, 4, 4, Color.White);
                using var tinted = TextureUtil.Tint(device, white, new Color(200, 100, 50));
                foreach (Color p in Read(tinted))
                    if (p.R != 200 || p.G != 100 || p.B != 50) return $"expected exact tint color on a white source, got {p}";
                return null;
            });

            results.Check("TextureUtil.Resize: nearest-neighbor upscale keeps quadrant colors crisp; render target is saved/restored", () =>
            {
                var pixels = new[] { Color.Red, Color.Lime, Color.Blue, Color.Yellow };
                using var source = new Texture2D(device, 2, 2, false, SurfaceFormat.Color);
                source.SetData(pixels);

                using var callerRt = new RenderTarget2D(device, 16, 16);
                device.SetRenderTarget(callerRt);

                using var resized = TextureUtil.Resize(device, source, 8, 8, smooth: false);

                if (device.GetRenderTargets().Length == 0 || device.GetRenderTargets()[0].RenderTarget != callerRt)
                    return "expected the caller's own render target to still be active after Resize returns";
                device.SetRenderTarget(null);

                Color[] r = Read(resized);
                Color At(int x, int y) => r[y * 8 + x];
                if (At(0, 0) != Color.Red) return $"top-left quadrant should stay Red after a crisp nearest-neighbor upscale, got {At(0, 0)}";
                if (At(7, 0) != Color.Lime) return $"top-right quadrant should stay Lime, got {At(7, 0)}";
                if (At(0, 7) != Color.Blue) return $"bottom-left quadrant should stay Blue, got {At(0, 7)}";
                if (At(7, 7) != Color.Yellow) return $"bottom-right quadrant should stay Yellow, got {At(7, 7)}";
                return null;
            });

            results.Check("TextureUtil.Combine: overlay lands at the given offset, background shows through elsewhere", () =>
            {
                using var background = TextureUtil.CreateSolid(device, 8, 8, Color.Black);
                using var overlay = TextureUtil.CreateSolid(device, 4, 4, Color.Red);
                using var combined = TextureUtil.Combine(device, background, overlay, new Point(2, 2));

                Color[] p = Read(combined);
                Color At(int x, int y) => p[y * 8 + x];
                if (At(0, 0) != Color.Black) return $"outside the overlay, background should show through, got {At(0, 0)}";
                if (At(3, 3) != Color.Red) return $"inside the overlay's placed area, expected Red, got {At(3, 3)}";
                if (At(7, 7) != Color.Black) return $"far corner, outside the overlay, should still be background, got {At(7, 7)}";
                return null;
            });

            results.Check("TextureUtil.ToTexture2D: snapshot matches the render target's actual contents", () =>
            {
                using var rt = new RenderTarget2D(device, 4, 4);
                device.SetRenderTarget(rt);
                device.Clear(Color.Orange);
                device.SetRenderTarget(null);

                using var snapshot = TextureUtil.ToTexture2D(device, rt);
                foreach (Color p in Read(snapshot))
                    if (p != Color.Orange) return $"expected the snapshot to match the cleared render target, got {p}";
                return null;
            });

            results.Check("TextureUtil: invalid sizes and null arguments throw", () =>
            {
                try { TextureUtil.CreateSolid(device, 0, 4, Color.White); return "expected ArgumentOutOfRangeException for width=0"; }
                catch (ArgumentOutOfRangeException) { }

                try { TextureUtil.CreateSolid(null!, 4, 4, Color.White); return "expected ArgumentNullException for a null device"; }
                catch (ArgumentNullException) { }

                try { TextureUtil.Crop(device, null!, new Rectangle(0, 0, 1, 1)); return "expected ArgumentNullException for a null source"; }
                catch (ArgumentNullException) { }

                return null;
            });
        }
    }
}
