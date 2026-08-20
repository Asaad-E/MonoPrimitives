using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives3D;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Regression coverage for the reported bug: a lit sphere's topmost latitude ring rendered
    /// pitch black instead of catching light from directly overhead. Renders a real lit sphere
    /// to an offscreen target and reads the pixels back — the only way to actually prove the
    /// fix, since the bug was a per-quad NaN that only shows up once colors reach the GPU.
    /// </summary>
    internal static class LightingRegressionTests
    {
        public static void Run(GraphicsDevice device, TestResults results)
        {
            results.Check("Sphere top is lit (not the NaN-blackened pole bug)", () =>
            {
                const int size = 256;
                using var rt = new RenderTarget2D(device, size, size, false, SurfaceFormat.Color, DepthFormat.Depth24);
                using var litBatch = new Primitive3DBatch(device)
                {
                    LightingEnabled = true,
                    AmbientLight = 0.35f,
                    LightDirection = new Vector3(0, -1, 0) // straight down, matches the reported repro
                };

                var camera = new Camera3D(position: new Vector3(0, 6, 12), target: Vector3.Zero, up: Vector3.Up, fovy: 50f);

                device.SetRenderTarget(rt);
                device.Clear(Color.Black);

                litBatch.Begin(camera);
                litBatch.FillSphereEx(Vector3.Zero, 3f, 16, 16, Color.Red);
                litBatch.End();

                device.SetRenderTarget(null);

                var pixels = new Color[size * size];
                rt.GetData(pixels);

                // Sphere pixels are pure-red-channel-scaled (G == B == 0 always, since the fill
                // color is Color.Red and Shade only scales magnitude), so any non-background
                // pixel with R > 0 and G == B == 0 is part of the sphere, not the black clear color.
                int topRow = -1;
                for (int y = 0; y < size && topRow < 0; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Color p = pixels[y * size + x];
                        if (p.R > 0 && p.G == 0 && p.B == 0) { topRow = y; break; }
                    }
                }

                if (topRow < 0)
                    return "sphere silhouette not found in the render (nothing rendered?)";

                // Sample a small band just below the topmost row — this is the pole ring that
                // the bug blackened. Straight-down light should make it very bright.
                long sum = 0;
                int count = 0;
                for (int y = topRow; y < Math.Min(topRow + 8, size); y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Color p = pixels[y * size + x];
                        if (p.R > 0 && p.G == 0 && p.B == 0) { sum += p.R; count++; }
                    }
                }

                if (count == 0)
                    return "no sphere pixels found near the top of the silhouette";

                double avgR = sum / (double)count;
                // Ambient-only (unlit) faces would floor at ~255*0.35 ≈ 89; the NaN bug rendered
                // this band as flat 0. A correctly lit top-of-sphere facing straight-down light
                // should be well above the ambient floor.
                if (avgR < 150)
                    return $"top of sphere too dark (avg R = {avgR:F1}, expected > 150 for a face lit head-on)";

                return null;
            });
        }
    }
}
