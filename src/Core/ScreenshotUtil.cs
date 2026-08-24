using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoPrimitives
{
    /// <summary>
    /// Saves the current back buffer to an image file — MonoGame has no built-in equivalent
    /// (<see cref="Texture2D.SaveAsPng"/>/<see cref="Texture2D.SaveAsJpeg"/> exist, but only for a
    /// <see cref="Texture2D"/> you already own; getting the actual on-screen frame into one is left
    /// entirely to you). Call after <c>End()</c>/before <c>Present()</c> — typically the last thing
    /// in <c>Draw</c> — so the buffer actually holds this frame's finished image.
    /// </summary>
    public static class ScreenshotUtil
    {
        /// <summary>
        /// Captures the current back buffer and saves it to <paramref name="filePath"/>. The image
        /// format is inferred from the file extension (<c>.png</c>, or <c>.jpg</c>/<c>.jpeg</c>);
        /// anything else throws <see cref="ArgumentException"/> rather than silently guessing.
        /// </summary>
        public static void Capture(GraphicsDevice device, string filePath)
        {
            ArgumentNullException.ThrowIfNull(device);
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("filePath must not be null or empty.", nameof(filePath));

            string extension = Path.GetExtension(filePath);
            bool isPng = extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
            bool isJpeg = extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
            if (!isPng && !isJpeg)
                throw new ArgumentException($"Unrecognized image extension '{extension}' -- use .png, .jpg, or .jpeg.", nameof(filePath));

            int width = device.PresentationParameters.BackBufferWidth;
            int height = device.PresentationParameters.BackBufferHeight;

            var pixels = new Color[width * height];
            device.GetBackBufferData(pixels);

            using var texture = new Texture2D(device, width, height);
            texture.SetData(pixels);

            string? directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            using var stream = File.Create(filePath);
            if (isPng) texture.SaveAsPng(stream, width, height);
            else texture.SaveAsJpeg(stream, width, height);
        }
    }
}
