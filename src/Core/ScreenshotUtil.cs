using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoPrimitives
{
    /// <summary>Saves the current back buffer to an image file.</summary>
    /// <remarks>Call after <c>End()</c>/before <c>Present()</c>, so the buffer still holds this frame's finished image.</remarks>
    public static class ScreenshotUtil
    {
        // MonoGame's own Texture2D.SaveAsPng/SaveAsJpeg only work on a Texture2D you already own;
        // getting the actual on-screen back buffer into one is left entirely to the caller.
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
