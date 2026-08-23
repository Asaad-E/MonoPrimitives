using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Compares <see cref="Texture2D.SetData{T}(T[])"/> against a raw <c>glTexSubImage2D</c> P/Invoke
    /// path for large textures. Not part of the public library — this exists purely to produce real
    /// numbers for a from-scratch technique that started life as a scratch experiment (formerly
    /// <c>todo/FastTexture.cs</c>). Windows/Linux/macOS desktop only (DesktopGL's actual target
    /// matrix); throws <see cref="PlatformNotSupportedException"/> anywhere else the GL entry points
    /// can't be resolved.
    /// </summary>
    internal static class TextureUpdateBenchmark
    {
        /// <summary>
        /// Direct OpenGL texture upload, bypassing <see cref="Texture2D.SetData{T}(T[])"/>'s own
        /// validation/marshaling path. Reflects into <see cref="Texture2D"/>'s private "glTexture"
        /// field to get the driver handle MonoGame already created — an internal implementation
        /// detail, not part of MonoGame's public contract, so this throws a clear, specific
        /// exception instead of an obscure NullReferenceException if a future MonoGame version
        /// renames or removes it.
        /// </summary>
        private sealed class FastGLTextureUpdater
        {
            // Maps the literal "opengl32.dll" name every [DllImport] below uses to the actual
            // platform GL library at runtime -- a bare DllImport("opengl32.dll") only resolves on
            // Windows; Linux ships GL as libGL.so.1, macOS as the OpenGL framework. Registering this
            // resolver once (a static constructor runs before any P/Invoke call reaches the CLR's
            // default resolution) makes the same [DllImport] declarations work on all three, instead
            // of writing three sets of externs behind #if platform guards.
            static FastGLTextureUpdater()
            {
                NativeLibrary.SetDllImportResolver(typeof(FastGLTextureUpdater).Assembly, ResolveGL);
            }

            private static IntPtr ResolveGL(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
            {
                if (libraryName != "opengl32.dll") return IntPtr.Zero; // let the default resolver handle anything else

                if (OperatingSystem.IsWindows() && NativeLibrary.TryLoad("opengl32.dll", out IntPtr win)) return win;
                if (OperatingSystem.IsLinux() && NativeLibrary.TryLoad("libGL.so.1", out IntPtr lin)) return lin;
                if (OperatingSystem.IsMacOS() && NativeLibrary.TryLoad("/System/Library/Frameworks/OpenGL.framework/OpenGL", out IntPtr mac)) return mac;

                return IntPtr.Zero; // falls through to the default resolver, which will throw a normal DllNotFoundException
            }

            [DllImport("opengl32.dll")]
            private static extern void glBindTexture(uint target, uint texture);

            [DllImport("opengl32.dll")]
            private static extern unsafe void glTexSubImage2D(uint target, int level, int x, int y, int width, int height, uint format, uint type, void* pixels);

            private const uint GL_TEXTURE_2D = 0x0DE1;
            private const uint GL_RGBA = 0x1908;
            private const uint GL_UNSIGNED_BYTE = 0x1401;

            private readonly uint _glId;
            private readonly int _width, _height;

            public FastGLTextureUpdater(Texture2D texture)
            {
                if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
                    throw new PlatformNotSupportedException("FastGLTextureUpdater only supports desktop GL platforms (Windows/Linux/macOS).");

                _width = texture.Width;
                _height = texture.Height;

                FieldInfo field = typeof(Texture2D).GetField("glTexture", BindingFlags.NonPublic | BindingFlags.Instance);
                object value = field?.GetValue(texture);
                if (value is null)
                {
                    throw new InvalidOperationException(
                        "Texture2D's private 'glTexture' field wasn't found (or was null) on this MonoGame version. " +
                        "This technique relies on a MonoGame implementation detail, not its public API, so it can " +
                        "break on a MonoGame update -- that's the tradeoff for bypassing SetData's own overhead.");
                }
                _glId = Convert.ToUInt32(value);
            }

            public unsafe void Update(Color[] pixels)
            {
                if (pixels.Length < _width * _height)
                    throw new ArgumentException($"Expected at least {_width * _height} pixels for a {_width}x{_height} texture, got {pixels.Length}.", nameof(pixels));

                glBindTexture(GL_TEXTURE_2D, _glId);
                fixed (Color* p = pixels)
                    glTexSubImage2D(GL_TEXTURE_2D, 0, 0, 0, _width, _height, GL_RGBA, GL_UNSIGNED_BYTE, p);
            }
        }

        /// <summary>
        /// Runs both update paths against a <paramref name="size"/>x<paramref name="size"/> texture
        /// for <paramref name="iterations"/> full-texture updates each, after a few untimed warm-up
        /// calls, and prints the average time per update for both. Never fails the suite — a machine
        /// without a working GL context (or off desktop entirely) just skips the fast-path row.
        /// </summary>
        public static void Run(GraphicsDevice device, TestResults results)
        {
            const int size = 3000;
            const int iterations = 10;
            const int warmup = 2;

            var pixels = new Color[size * size];
            var rng = new Random(1);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256), (byte)255);

            using var texture = new Texture2D(device, size, size, false, SurfaceFormat.Color);

            for (int i = 0; i < warmup; i++) texture.SetData(pixels);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++) texture.SetData(pixels);
            sw.Stop();
            double setDataMs = sw.Elapsed.TotalMilliseconds / iterations;
            Console.WriteLine($"  [benchmark] Texture2D.SetData      {size}x{size}: {setDataMs:F3} ms/update (avg of {iterations})");

            try
            {
                var fast = new FastGLTextureUpdater(texture);
                for (int i = 0; i < warmup; i++) fast.Update(pixels);
                sw.Restart();
                for (int i = 0; i < iterations; i++) fast.Update(pixels);
                sw.Stop();
                double fastMs = sw.Elapsed.TotalMilliseconds / iterations;
                Console.WriteLine($"  [benchmark] Direct glTexSubImage2D {size}x{size}: {fastMs:F3} ms/update (avg of {iterations}) -- {setDataMs / fastMs:F2}x SetData's time");
            }
            catch (Exception ex) when (ex is PlatformNotSupportedException or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
            {
                Console.WriteLine($"  [benchmark] Direct glTexSubImage2D {size}x{size}: skipped ({ex.GetType().Name}: {ex.Message})");
            }

            results.Check("TextureUpdateBenchmark ran without throwing an unexpected exception", () => null);
        }
    }
}
