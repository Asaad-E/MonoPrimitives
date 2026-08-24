using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>Renders a known color to the real back buffer, captures it, and reads the saved file back to confirm it round-trips.</summary>
    internal static class ScreenshotUtilTests
    {
        public static void Run(GraphicsDevice device, TestResults results)
        {
            results.Check("ScreenshotUtil.Capture: PNG round-trips the actual back buffer content", () =>
            {
                string path = Path.Combine(Path.GetTempPath(), $"monoprimitives_screenshot_test_{Guid.NewGuid():N}.png");
                try
                {
                    device.SetRenderTarget(null); // capture the real back buffer, not a leftover render target from another test
                    device.Clear(Color.MediumPurple);

                    ScreenshotUtil.Capture(device, path);

                    if (!File.Exists(path)) return $"expected a file at {path}, none was written";

                    using var stream = File.OpenRead(path);
                    using var loaded = Texture2D.FromStream(device, stream);

                    if (loaded.Width != device.PresentationParameters.BackBufferWidth || loaded.Height != device.PresentationParameters.BackBufferHeight)
                        return $"saved image is {loaded.Width}x{loaded.Height}, expected {device.PresentationParameters.BackBufferWidth}x{device.PresentationParameters.BackBufferHeight}";

                    var pixels = new Color[loaded.Width * loaded.Height];
                    loaded.GetData(pixels);
                    Color center = pixels[pixels.Length / 2];

                    const int tolerance = 5; // PNG is lossless, but SaveAsPng/FromStream can still round through sRGB byte conversions
                    bool close = Math.Abs(center.R - Color.MediumPurple.R) <= tolerance
                              && Math.Abs(center.G - Color.MediumPurple.G) <= tolerance
                              && Math.Abs(center.B - Color.MediumPurple.B) <= tolerance;
                    return close ? null : $"expected ~{Color.MediumPurple} at the center of the saved image, got {center}";
                }
                finally
                {
                    if (File.Exists(path)) File.Delete(path);
                }
            });

            results.Check("ScreenshotUtil.Capture: .jpg/.jpeg extensions are also accepted and produce a loadable file", () =>
            {
                foreach (string ext in new[] { ".jpg", ".jpeg" })
                {
                    string path = Path.Combine(Path.GetTempPath(), $"monoprimitives_screenshot_test_{Guid.NewGuid():N}{ext}");
                    try
                    {
                        device.SetRenderTarget(null);
                        device.Clear(Color.SeaGreen);
                        ScreenshotUtil.Capture(device, path);

                        if (!File.Exists(path)) return $"expected a file at {path}, none was written";
                        using var stream = File.OpenRead(path);
                        using var loaded = Texture2D.FromStream(device, stream); // JPEG is lossy -- just confirm it loads and is the right size, not exact pixels
                        if (loaded.Width != device.PresentationParameters.BackBufferWidth || loaded.Height != device.PresentationParameters.BackBufferHeight)
                            return $"{ext}: saved image is {loaded.Width}x{loaded.Height}, expected {device.PresentationParameters.BackBufferWidth}x{device.PresentationParameters.BackBufferHeight}";
                    }
                    finally
                    {
                        if (File.Exists(path)) File.Delete(path);
                    }
                }
                return null;
            });

            results.Check("ScreenshotUtil.Capture: an unrecognized extension throws instead of silently guessing a format", () =>
            {
                string path = Path.Combine(Path.GetTempPath(), $"monoprimitives_screenshot_test_{Guid.NewGuid():N}.bmp");
                try
                {
                    ScreenshotUtil.Capture(device, path);
                    return "expected an ArgumentException for a .bmp path";
                }
                catch (ArgumentException)
                {
                    return null;
                }
                finally
                {
                    if (File.Exists(path)) File.Delete(path);
                }
            });

            results.Check("ScreenshotUtil.Capture: null device / null-or-empty path throw", () =>
            {
                try
                {
                    ScreenshotUtil.Capture(null, "x.png");
                    return "expected an exception for a null device";
                }
                catch (ArgumentNullException) { }

                try
                {
                    ScreenshotUtil.Capture(device, "");
                    return "expected an exception for an empty path";
                }
                catch (ArgumentException) { }

                return null;
            });
        }
    }
}
