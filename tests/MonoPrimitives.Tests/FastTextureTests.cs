using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Correctness checks for <see cref="FastTexture"/> against a real <see cref="GraphicsDevice"/>.
    /// Not a performance benchmark: this test runner executes everything inside a single headless
    /// <c>Draw()</c> call with no real per-frame boundary, and a tight synchronous loop of raw GL
    /// calls with no intervening <c>Present()</c> forces the driver to fully serialize -- an
    /// artificial stall that doesn't happen in real per-frame usage and produces misleading numbers.
    /// The ~2.5-2.7x speedup documented on <see cref="FastTexture"/> was measured with a real
    /// per-frame MonoGame loop instead, once per real frame with normal GPU work between uploads.
    /// </summary>
    internal static class FastTextureTests
    {
        public static void Run(GraphicsDevice device, SpriteBatch spriteBatch, TestResults results)
        {
            results.Check("FastTexture: constructs, reports a non-empty Diagnostics string", () =>
            {
                using var tex = new FastTexture(device, 8, 8);
                return string.IsNullOrEmpty(tex.Diagnostics) ? "Diagnostics was empty" : null;
            });

            results.Check("FastTexture: full-texture Update round-trips exactly via GetData", () =>
            {
                using var tex = new FastTexture(device, 8, 8);
                var expected = new Color[64];
                for (int i = 0; i < expected.Length; i++)
                    expected[i] = new Color(i * 7 % 256, i * 13 % 256, i * 29 % 256, 255);

                tex.Update(expected);

                var actual = new Color[64];
                tex.Texture.GetData(actual);
                for (int i = 0; i < expected.Length; i++)
                    if (actual[i] != expected[i]) return $"pixel {i} mismatch: expected {expected[i]}, got {actual[i]}";
                return null;
            });

            results.Check("FastTexture: sub-rectangle Update only touches the given rectangle", () =>
            {
                using var tex = new FastTexture(device, 8, 8);
                var full = new Color[64];
                for (int i = 0; i < full.Length; i++) full[i] = Color.Black;
                tex.Update(full);

                var patch = new Color[4 * 4];
                for (int i = 0; i < patch.Length; i++) patch[i] = Color.White;
                tex.Update(new Rectangle(2, 2, 4, 4), (ReadOnlySpan<Color>)patch);

                var actual = new Color[64];
                tex.Texture.GetData(actual);
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        bool insidePatch = x >= 2 && x < 6 && y >= 2 && y < 6;
                        Color expected = insidePatch ? Color.White : Color.Black;
                        if (actual[y * 8 + x] != expected)
                            return $"pixel ({x},{y}) mismatch: expected {expected}, got {actual[y * 8 + x]}";
                    }
                }
                return null;
            });

            results.Check("FastTexture: wrong-length Update throws ArgumentException instead of corrupting memory", () =>
            {
                using var tex = new FastTexture(device, 8, 8);
                try
                {
                    tex.Update(new Color[10]);
                    return "expected an ArgumentException for a wrong-sized array, none was thrown";
                }
                catch (ArgumentException) { return null; }
            });

            results.Check("FastTexture: a slot-0 texture reused across an intervening raw upload elsewhere still draws correctly", () =>
            {
                var decoyPixels = new Color[64];
                for (int i = 0; i < decoyPixels.Length; i++) decoyPixels[i] = Color.Magenta;
                using var decoy = new Texture2D(device, 8, 8, false, SurfaceFormat.Color);
                decoy.SetData(decoyPixels);

                using var probe = new FastTexture(device, 8, 8);
                var probePixels = new Color[64];
                for (int i = 0; i < probePixels.Length; i++) probePixels[i] = Color.Lime;
                probe.Update(probePixels);

                using var rt = new RenderTarget2D(device, 8, 8);
                device.SetRenderTarget(rt);
                device.Clear(Color.Black);
                spriteBatch.Begin(samplerState: SamplerState.PointClamp);
                spriteBatch.Draw(decoy, Vector2.Zero, Color.White);
                spriteBatch.End();

                probe.Update(probePixels); // raw bind happens here, behind MonoGame's own texture-slot cache

                spriteBatch.Begin(samplerState: SamplerState.PointClamp);
                spriteBatch.Draw(decoy, Vector2.Zero, Color.White); // same reference as before -- must still show `decoy`, not `probe`
                spriteBatch.End();
                device.SetRenderTarget(null);

                var actual = new Color[64];
                rt.GetData(actual);
                for (int i = 0; i < actual.Length; i++)
                    if (actual[i] != Color.Magenta) return $"pixel {i} was {actual[i]}, expected Magenta (decoy) -- stale texture-slot cache leaked the FastTexture's raw bind";
                return null;
            });

            results.Check("FastTexture: Dispose(ownsTexture: true) disposes the wrapped texture, false leaves it alive", () =>
            {
                using var owned = new FastTexture(device, 4, 4);
                owned.Dispose();
                if (!owned.Texture.IsDisposed) return "ownsTexture defaults to true but the wrapped texture wasn't disposed";

                using var external = new Texture2D(device, 4, 4, false, SurfaceFormat.Color);
                var wrapper = new FastTexture(device, external, ownsTexture: false);
                wrapper.Dispose();
                if (external.IsDisposed) return "ownsTexture: false should not dispose the caller's own texture";
                return null;
            });
        }
    }
}
