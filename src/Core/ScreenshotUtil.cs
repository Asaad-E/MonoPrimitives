using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoPrimitives
{
    /// <summary>Captures the current back buffer to a <see cref="Texture2D"/> or an image file.</summary>
    /// <remarks>Call after <c>End()</c>/before <c>Present()</c>, so the buffer still holds this frame's finished image.</remarks>
    public static class ScreenshotUtil
    {
        // MonoGame's own Texture2D.SaveAsPng/SaveAsJpeg only work on a Texture2D you already own;
        // getting the actual on-screen back buffer into one is left entirely to the caller.
        /// <summary>Captures the current back buffer and saves it to <paramref name="filePath"/>, in the format its extension names.</summary>
        /// <param name="device">Device to capture the back buffer from.</param>
        /// <param name="filePath">Destination path; format is inferred from its extension (<c>.png</c>, <c>.jpg</c>, or <c>.jpeg</c>).</param>
        /// <exception cref="ArgumentException"><paramref name="filePath"/> is null/empty, or its extension isn't <c>.png</c>/<c>.jpg</c>/<c>.jpeg</c>.</exception>
        public static void Capture(GraphicsDevice device, string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("filePath must not be null or empty.", nameof(filePath));

            string extension = Path.GetExtension(filePath);
            bool isPng = extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
            bool isJpeg = extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
            if (!isPng && !isJpeg)
                throw new ArgumentException($"Unrecognized image extension '{extension}' -- use .png, .jpg, or .jpeg.", nameof(filePath));

            using Texture2D texture = Capture(device);

            string? directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            using var stream = File.Create(filePath);
            if (isPng) texture.SaveAsPng(stream, texture.Width, texture.Height);
            else texture.SaveAsJpeg(stream, texture.Width, texture.Height);
        }

        /// <summary>Captures the current back buffer into a new <see cref="Texture2D"/>.</summary>
        /// <param name="device">Device to capture the back buffer from.</param>
        /// <returns>A texture the caller owns and must dispose.</returns>
        public static Texture2D Capture(GraphicsDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);

            int width = device.PresentationParameters.BackBufferWidth;
            int height = device.PresentationParameters.BackBufferHeight;

            var pixels = new Color[width * height];
            device.GetBackBufferData(pixels);

            var texture = new Texture2D(device, width, height);
            using var fast = new FastTexture(device, texture);
            fast.Update(pixels);
            return texture;
        }
    }
}
