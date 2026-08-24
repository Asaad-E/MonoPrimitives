using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives.Primitives3D;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Regression coverage for the reported bug: <c>DrawLineStrip3D</c>/<c>Trail3D.Draw</c> used to
    /// draw each segment as its own independent camera-facing quad, so two segments meeting at a
    /// sharp bend computed different offset directions for what should be one shared corner —
    /// visible as a background-colored gap right at the joint. Renders a real sharp zigzag to an
    /// offscreen target and checks each interior bend vertex's own screen projection is solidly
    /// covered, not punched through — the only way to actually prove the fix, since the bug only
    /// shows up once the geometry reaches the GPU.
    /// </summary>
    internal static class LineStrip3DJoinTests
    {
        public static void Run(GraphicsDevice device, TestResults results)
        {
            results.Check("DrawLineStrip3D: sharp bends have no background gap at the shared joint", () =>
            {
                const int size = 400;
                using var rt = new RenderTarget2D(device, size, size);
                using var batch = new Primitive3DBatch(device) { SmoothLines = false };
                var camera = new Camera3D(position: new Vector3(0, 0, 20), target: Vector3.Zero, up: Vector3.Up, fovy: 40f);
                var viewport = new Viewport(0, 0, size, size);

                Vector3[] zigzag =
                {
                    new Vector3(-6, -2, 0),
                    new Vector3(-3, 2, 0),
                    new Vector3(0, -2, 0),
                    new Vector3(3, 2, 0),
                    new Vector3(6, -2, 0),
                };

                device.SetRenderTarget(rt);
                device.Clear(Color.Black);
                batch.Begin(camera);
                batch.DrawLineStrip3D(zigzag, Color.Lime, 1.2f);
                batch.End();
                device.SetRenderTarget(null);

                var pixels = new Color[size * size];
                rt.GetData(pixels);

                static bool IsStripColor(Color c) => c.G > 100 && c.R < 100 && c.B < 100;

                // The 3 interior bend vertices (excluding the two open endpoints, which have no
                // joint to close) -- a small disc centered on each one's screen projection should
                // be solidly strip-colored.
                for (int vi = 1; vi <= 3; vi++)
                {
                    Vector3 v = zigzag[vi];
                    Vector2 screen = camera.WorldToScreen(v, viewport);
                    int cx = (int)screen.X, cy = (int)screen.Y;

                    int total = 0, hit = 0;
                    for (int dy = -3; dy <= 3; dy++)
                    {
                        for (int dx = -3; dx <= 3; dx++)
                        {
                            int x = cx + dx, y = cy + dy;
                            if (x < 0 || x >= size || y < 0 || y >= size) continue;
                            total++;
                            if (IsStripColor(pixels[y * size + x])) hit++;
                        }
                    }

                    if (total == 0) return $"bend vertex {v} projected off-screen ({cx},{cy})";
                    double coverage = hit / (double)total;
                    if (coverage < 0.9)
                        return $"gap detected at bend vertex {v} (screen {cx},{cy}): only {coverage:P0} of the sampled disc was strip-colored";
                }

                return null;
            });

            results.Check("Trail3D.Draw: sharp bends have no background gap either (per-segment-color overload)", () =>
            {
                const int size = 400;
                using var rt = new RenderTarget2D(device, size, size);
                using var batch = new Primitive3DBatch(device) { SmoothLines = false };
                var camera = new Camera3D(position: new Vector3(0, 0, 20), target: Vector3.Zero, up: Vector3.Up, fovy: 40f);
                var viewport = new Viewport(0, 0, size, size);

                Vector3[] zigzag =
                {
                    new Vector3(-6, -2, 0),
                    new Vector3(-3, 2, 0),
                    new Vector3(0, -2, 0),
                    new Vector3(3, 2, 0),
                    new Vector3(6, -2, 0),
                };

                var trail = new Trail3D(zigzag.Length);
                foreach (var p in zigzag) trail.Add(p);

                device.SetRenderTarget(rt);
                device.Clear(Color.Black);
                batch.Begin(camera);
                trail.Draw(batch, Color.Cyan, thickness: 1.2f, fadeToAlpha: 0.3f); // fadeToAlpha > 0 so even the oldest segment stays visible/sampleable
                batch.End();
                device.SetRenderTarget(null);

                var pixels = new Color[size * size];
                rt.GetData(pixels);

                static bool IsStripColor(Color c) => c.B > 60 && c.G > 60 && c.R < 60; // cyan-ish at any brightness

                for (int vi = 1; vi <= 3; vi++)
                {
                    Vector3 v = zigzag[vi];
                    Vector2 screen = camera.WorldToScreen(v, viewport);
                    int cx = (int)screen.X, cy = (int)screen.Y;

                    int total = 0, hit = 0;
                    for (int dy = -3; dy <= 3; dy++)
                    {
                        for (int dx = -3; dx <= 3; dx++)
                        {
                            int x = cx + dx, y = cy + dy;
                            if (x < 0 || x >= size || y < 0 || y >= size) continue;
                            total++;
                            if (IsStripColor(pixels[y * size + x])) hit++;
                        }
                    }

                    if (total == 0) return $"bend vertex {v} projected off-screen ({cx},{cy})";
                    double coverage = hit / (double)total;
                    if (coverage < 0.9)
                        return $"gap detected at bend vertex {v} (screen {cx},{cy}): only {coverage:P0} of the sampled disc was strip-colored";
                }

                return null;
            });
        }
    }
}
