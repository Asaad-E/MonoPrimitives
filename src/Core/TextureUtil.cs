using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoPrimitives
{
    /// <summary>
    /// Procedural <see cref="Texture2D"/> generation (solid/gradient/checkerboard/from <see cref="Noise"/>)
    /// plus transform utilities (resize/crop/flip/tint/combine) that MonoGame's own <see cref="Texture2D"/>
    /// doesn't provide -- the CPU-pixel-buffer half of what raylib's <c>Image*</c> functions cover.
    /// </summary>
    /// <remarks>
    /// Generation and the CPU-side transforms (<see cref="Crop"/>/<see cref="FlipHorizontal"/>/
    /// <see cref="FlipVertical"/>/<see cref="Tint"/>) build a <c>Color[]</c> buffer and upload it once --
    /// none of this is meant for a per-frame hot path. <see cref="Resize"/>/<see cref="Combine"/> render
    /// through a temporary <see cref="RenderTarget2D"/> instead (resampling and alpha blending are what
    /// the GPU already does correctly); both save and restore the device's current render target, so
    /// calling one mid-frame doesn't clobber whatever the caller was already rendering to.
    /// </remarks>
    public static class TextureUtil
    {
        // ==================================================================
        // Generation
        // ==================================================================

        /// <summary>Creates a texture filled with a single solid color.</summary>
        public static Texture2D CreateSolid(GraphicsDevice device, int width, int height, Color color)
        {
            ValidateSize(width, height);
            var pixels = new Color[width * height];
            Array.Fill(pixels, color);
            return Upload(device, width, height, pixels);
        }

        /// <summary>Creates a texture that linearly blends from <paramref name="colorA"/> to <paramref name="colorB"/> along <paramref name="angle"/> (radians; 0 = left-to-right, increasing rotates counter-clockwise).</summary>
        public static Texture2D CreateGradientLinear(GraphicsDevice device, int width, int height, Color colorA, Color colorB, float angle = 0f)
        {
            ValidateSize(width, height);
            var pixels = new Color[width * height];

            Vector2 direction = new(MathF.Cos(angle), MathF.Sin(angle));
            Vector2 center = new(width * 0.5f, height * 0.5f);
            // Half-projections of every corner onto `direction` -- the true span of the gradient
            // axis across the rectangle, not just half the diagonal (which overshoots for a
            // near-axis-aligned direction and undershoots for a diagonal one).
            float half = MathF.Abs(direction.X) * width * 0.5f + MathF.Abs(direction.Y) * height * 0.5f;
            if (half < 1e-6f) half = 1e-6f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 offset = new(x + 0.5f - center.X, y + 0.5f - center.Y);
                    float t = MathHelper.Clamp((Vector2.Dot(offset, direction) + half) / (2f * half), 0f, 1f);
                    pixels[y * width + x] = ColorUtil.Lerp(colorA, colorB, t);
                }
            }
            return Upload(device, width, height, pixels);
        }

        /// <summary>Creates a texture that radially blends from <paramref name="innerColor"/> at the center to <paramref name="outerColor"/> at the corners.</summary>
        public static Texture2D CreateGradientRadial(GraphicsDevice device, int width, int height, Color innerColor, Color outerColor)
        {
            ValidateSize(width, height);
            var pixels = new Color[width * height];

            Vector2 center = new(width * 0.5f, height * 0.5f);
            float maxDist = center.Length();
            if (maxDist < 1e-6f) maxDist = 1e-6f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float t = MathHelper.Clamp(dist / maxDist, 0f, 1f);
                    pixels[y * width + x] = ColorUtil.Lerp(innerColor, outerColor, t);
                }
            }
            return Upload(device, width, height, pixels);
        }

        /// <summary>Creates a two-color checkerboard texture with <paramref name="cellSize"/>-pixel square cells.</summary>
        public static Texture2D CreateCheckerboard(GraphicsDevice device, int width, int height, int cellSize, Color colorA, Color colorB)
        {
            ValidateSize(width, height);
            if (cellSize < 1) throw new ArgumentOutOfRangeException(nameof(cellSize), "cellSize must be at least 1.");

            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                bool rowEven = (y / cellSize) % 2 == 0;
                for (int x = 0; x < width; x++)
                {
                    bool colEven = (x / cellSize) % 2 == 0;
                    pixels[y * width + x] = (rowEven == colEven) ? colorA : colorB;
                }
            }
            return Upload(device, width, height, pixels);
        }

        /// <summary>
        /// Creates a texture by sampling <paramref name="noise"/> once per pixel at <c>(x,y) * scale</c>.
        /// </summary>
        /// <param name="device">Device the texture is created on.</param>
        /// <param name="width">Texture width in pixels.</param>
        /// <param name="height">Texture height in pixels.</param>
        /// <param name="noise">Sampled once per pixel via <see cref="Noise.Sample2D"/>.</param>
        /// <param name="scale">Multiplies the pixel coordinate before sampling -- smaller values zoom in on lower-frequency detail.</param>
        /// <param name="colorMap">Maps a sample (remapped from <see cref="Noise"/>'s own roughly [-1,1] output to [0,1]) to a color. Grayscale (<c>new Color(v,v,v)</c>) if omitted.</param>
        public static Texture2D CreateFromNoise(GraphicsDevice device, int width, int height, Noise noise, float scale = 0.05f, Func<float, Color>? colorMap = null)
        {
            ValidateSize(width, height);
            ArgumentNullException.ThrowIfNull(noise);
            colorMap ??= v => new Color(v, v, v);

            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float raw = noise.Sample2D(x * scale, y * scale);
                    float v = MathHelper.Clamp(MathUtil.Remap(raw, -1f, 1f, 0f, 1f), 0f, 1f);
                    pixels[y * width + x] = colorMap(v);
                }
            }
            return Upload(device, width, height, pixels);
        }

        // ==================================================================
        // CPU-side transforms
        // ==================================================================

        /// <summary>Extracts a sub-rectangle of <paramref name="source"/> into a new texture, without touching the GPU.</summary>
        public static Texture2D Crop(GraphicsDevice device, Texture2D source, Rectangle sourceRectangle)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (sourceRectangle.X < 0 || sourceRectangle.Y < 0 ||
                sourceRectangle.Right > source.Width || sourceRectangle.Bottom > source.Height ||
                sourceRectangle.Width <= 0 || sourceRectangle.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceRectangle), "sourceRectangle must lie within the source texture and have a positive size.");

            var pixels = new Color[sourceRectangle.Width * sourceRectangle.Height];
            source.GetData(0, sourceRectangle, pixels, 0, pixels.Length);
            return Upload(device, sourceRectangle.Width, sourceRectangle.Height, pixels);
        }

        /// <summary>Flips <paramref name="source"/> left-to-right into a new texture.</summary>
        public static Texture2D FlipHorizontal(GraphicsDevice device, Texture2D source)
        {
            GetPixels(source, out Color[] src, out int width, out int height);
            var dst = new Color[src.Length];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    dst[y * width + (width - 1 - x)] = src[y * width + x];
            return Upload(device, width, height, dst);
        }

        /// <summary>Flips <paramref name="source"/> top-to-bottom into a new texture.</summary>
        public static Texture2D FlipVertical(GraphicsDevice device, Texture2D source)
        {
            GetPixels(source, out Color[] src, out int width, out int height);
            var dst = new Color[src.Length];
            for (int y = 0; y < height; y++)
                Array.Copy(src, y * width, dst, (height - 1 - y) * width, width);
            return Upload(device, width, height, dst);
        }

        /// <summary>Multiplies every pixel of <paramref name="source"/> by <paramref name="tintColor"/> (<see cref="ColorUtil.Multiply"/>) into a new texture.</summary>
        public static Texture2D Tint(GraphicsDevice device, Texture2D source, Color tintColor)
        {
            GetPixels(source, out Color[] src, out int width, out int height);
            var dst = new Color[src.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = ColorUtil.Multiply(src[i], tintColor);
            return Upload(device, width, height, dst);
        }

        /// <summary>Rotates <paramref name="source"/> by a quarter turn (90 degrees), swapping width and height, into a new texture.</summary>
        /// <remarks>Exact -- every source pixel maps to exactly one destination pixel, no resampling. For an arbitrary angle, see <see cref="Rotate"/>.</remarks>
        public static Texture2D Rotate90(GraphicsDevice device, Texture2D source, bool clockwise = true)
        {
            GetPixels(source, out Color[] src, out int width, out int height);
            int newWidth = height, newHeight = width;
            var dst = new Color[src.Length];

            if (clockwise)
            {
                for (int dy = 0; dy < newHeight; dy++)
                    for (int dx = 0; dx < newWidth; dx++)
                        dst[dy * newWidth + dx] = src[(height - 1 - dx) * width + dy];
            }
            else
            {
                for (int dy = 0; dy < newHeight; dy++)
                    for (int dx = 0; dx < newWidth; dx++)
                        dst[dy * newWidth + dx] = src[dx * width + (width - 1 - dy)];
            }
            return Upload(device, newWidth, newHeight, dst);
        }

        /// <summary>Applies <paramref name="map"/> to every pixel of <paramref name="source"/> into a new texture.</summary>
        /// <remarks>One general per-pixel transform covering grayscale/invert/brightness/contrast/color-remap all at once -- compose with <see cref="ColorUtil"/>'s existing functions, e.g. <c>TextureUtil.Map(device, src, ColorUtil.Invert)</c>.</remarks>
        public static Texture2D Map(GraphicsDevice device, Texture2D source, Func<Color, Color> map)
        {
            ArgumentNullException.ThrowIfNull(map);
            GetPixels(source, out Color[] src, out int width, out int height);
            var dst = new Color[src.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = map(src[i]);
            return Upload(device, width, height, dst);
        }

        /// <summary>Applies a separable Gaussian blur with the given pixel <paramref name="radius"/> into a new texture.</summary>
        /// <remarks>Blurs in premultiplied-alpha space and un-premultiplies the result, so a blurred semi-transparent edge doesn't pick up color bleed from fully-transparent neighboring pixels. Cost is roughly <c>O(width * height * radius)</c> -- a large radius on a large texture is real work, not something to do every frame.</remarks>
        public static Texture2D Blur(GraphicsDevice device, Texture2D source, int radius)
        {
            if (radius < 1) throw new ArgumentOutOfRangeException(nameof(radius), "must be at least 1.");
            GetPixels(source, out Color[] src, out int width, out int height);

            float sigma = MathF.Max(radius / 2f, 0.6f);
            float[] kernel = BuildGaussianKernel(radius, sigma);

            var pr = new float[src.Length];
            var pg = new float[src.Length];
            var pb = new float[src.Length];
            var pa = new float[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                float a = src[i].A / 255f;
                pr[i] = src[i].R / 255f * a;
                pg[i] = src[i].G / 255f * a;
                pb[i] = src[i].B / 255f * a;
                pa[i] = a;
            }

            var hr = new float[src.Length];
            var hg = new float[src.Length];
            var hb = new float[src.Length];
            var ha = new float[src.Length];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int sx = Math.Clamp(x + k, 0, width - 1);
                        float w = kernel[k + radius];
                        int idx = y * width + sx;
                        r += pr[idx] * w; g += pg[idx] * w; b += pb[idx] * w; a += pa[idx] * w;
                    }
                    int di = y * width + x;
                    hr[di] = r; hg[di] = g; hb[di] = b; ha[di] = a;
                }
            }

            var dst = new Color[src.Length];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int sy = Math.Clamp(y + k, 0, height - 1);
                        float w = kernel[k + radius];
                        int idx = sy * width + x;
                        r += hr[idx] * w; g += hg[idx] * w; b += hb[idx] * w; a += ha[idx] * w;
                    }
                    int di = y * width + x;
                    float outA = MathHelper.Clamp(a, 0f, 1f);
                    float outR = outA > 1e-5f ? MathHelper.Clamp(r / outA, 0f, 1f) : 0f;
                    float outG = outA > 1e-5f ? MathHelper.Clamp(g / outA, 0f, 1f) : 0f;
                    float outB = outA > 1e-5f ? MathHelper.Clamp(b / outA, 0f, 1f) : 0f;
                    dst[di] = new Color(outR, outG, outB, outA);
                }
            }

            return Upload(device, width, height, dst);
        }

        private static float[] BuildGaussianKernel(int radius, float sigma)
        {
            var kernel = new float[radius * 2 + 1];
            float sum = 0f;
            float twoSigmaSq = 2f * sigma * sigma;
            for (int i = -radius; i <= radius; i++)
            {
                float v = MathF.Exp(-(i * i) / twoSigmaSq);
                kernel[i + radius] = v;
                sum += v;
            }
            for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;
            return kernel;
        }

        // ==================================================================
        // GPU-side transforms (temporary render target, saved/restored)
        // ==================================================================

        /// <summary>Resamples <paramref name="source"/> to <paramref name="newWidth"/>x<paramref name="newHeight"/> into a new texture.</summary>
        /// <param name="device">Device the new texture is created on.</param>
        /// <param name="source">Texture to resample.</param>
        /// <param name="newWidth">Target width in pixels.</param>
        /// <param name="newHeight">Target height in pixels.</param>
        /// <param name="smooth">True (default) for bilinear filtering; false for nearest-neighbor (pixel art).</param>
        public static Texture2D Resize(GraphicsDevice device, Texture2D source, int newWidth, int newHeight, bool smooth = true)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(source);
            ValidateSize(newWidth, newHeight);

            return RenderToTexture(device, newWidth, newHeight, spriteBatch =>
            {
                spriteBatch.Begin(samplerState: smooth ? SamplerState.LinearClamp : SamplerState.PointClamp);
                spriteBatch.Draw(source, new Rectangle(0, 0, newWidth, newHeight), Color.White);
                spriteBatch.End();
            });
        }

        /// <summary>Rotates <paramref name="source"/> by an arbitrary <paramref name="radians"/> into a new, larger-as-needed texture that fits the whole rotated image (no cropping).</summary>
        /// <param name="device">Device the new texture is created on.</param>
        /// <param name="source">Texture to rotate.</param>
        /// <param name="radians">Rotation angle, clockwise.</param>
        /// <param name="smooth">True (default) for bilinear filtering; false for nearest-neighbor.</param>
        /// <param name="backgroundColor">Fills the corners the rotated image doesn't cover. Transparent if omitted.</param>
        /// <remarks>For an exact quarter turn with no resampling, use <see cref="Rotate90"/> instead.</remarks>
        public static Texture2D Rotate(GraphicsDevice device, Texture2D source, float radians, bool smooth = true, Color? backgroundColor = null)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(source);

            float cos = MathF.Abs(MathF.Cos(radians));
            float sin = MathF.Abs(MathF.Sin(radians));
            int newWidth = Math.Max(1, (int)MathF.Ceiling(source.Width * cos + source.Height * sin));
            int newHeight = Math.Max(1, (int)MathF.Ceiling(source.Width * sin + source.Height * cos));

            return RenderToTexture(device, newWidth, newHeight, spriteBatch =>
            {
                spriteBatch.Begin(samplerState: smooth ? SamplerState.LinearClamp : SamplerState.PointClamp);
                Vector2 origin = new(source.Width * 0.5f, source.Height * 0.5f);
                Vector2 center = new(newWidth * 0.5f, newHeight * 0.5f);
                spriteBatch.Draw(source, center, null, Color.White, radians, origin, 1f, SpriteEffects.None, 0f);
                spriteBatch.End();
            }, backgroundColor);
        }

        /// <summary>Draws <paramref name="overlay"/> onto a copy of <paramref name="background"/> at <paramref name="offset"/>, alpha-blended, into a new texture the size of <paramref name="background"/>.</summary>
        public static Texture2D Combine(GraphicsDevice device, Texture2D background, Texture2D overlay, Point offset)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(background);
            ArgumentNullException.ThrowIfNull(overlay);

            return RenderToTexture(device, background.Width, background.Height, spriteBatch =>
            {
                // NonPremultiplied matches this library's own batches (Primitive2DBatch/Primitive3DBatch)
                // -- straight (non-premultiplied) alpha is what a plain Color[]-uploaded texture holds.
                spriteBatch.Begin(blendState: BlendState.Opaque);
                spriteBatch.Draw(background, Vector2.Zero, Color.White);
                spriteBatch.End();

                spriteBatch.Begin(blendState: BlendState.NonPremultiplied);
                spriteBatch.Draw(overlay, offset.ToVector2(), Color.White);
                spriteBatch.End();
            });
        }

        /// <summary>Copies a <see cref="RenderTarget2D"/>'s current contents into a plain, independent <see cref="Texture2D"/> -- a snapshot that survives the render target being cleared, resized, or disposed.</summary>
        public static Texture2D ToTexture2D(GraphicsDevice device, RenderTarget2D renderTarget)
        {
            ArgumentNullException.ThrowIfNull(renderTarget);
            var pixels = new Color[renderTarget.Width * renderTarget.Height];
            renderTarget.GetData(pixels);
            return Upload(device, renderTarget.Width, renderTarget.Height, pixels);
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        private static void ValidateSize(int width, int height)
        {
            if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(width < 1 ? nameof(width) : nameof(height), "must be at least 1.");
        }

        private static Texture2D Upload(GraphicsDevice device, int width, int height, Color[] pixels)
        {
            ArgumentNullException.ThrowIfNull(device);
            var texture = new Texture2D(device, width, height, false, SurfaceFormat.Color);
            texture.SetData(pixels);
            return texture;
        }

        private static void GetPixels(Texture2D source, out Color[] pixels, out int width, out int height)
        {
            ArgumentNullException.ThrowIfNull(source);
            width = source.Width;
            height = source.Height;
            pixels = new Color[width * height];
            source.GetData(pixels);
        }

        // Renders into a same-sized-as-requested RenderTarget2D via a throwaway SpriteBatch, reads
        // it back into a plain Texture2D, and restores whatever the device was already rendering to
        // -- so this is safe to call mid-frame without disturbing the caller's own render target.
        private static Texture2D RenderToTexture(GraphicsDevice device, int width, int height, Action<SpriteBatch> draw, Color? clearColor = null)
        {
            RenderTargetBinding[] previous = device.GetRenderTargets();
            try
            {
                using var rt = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.None);
                using var spriteBatch = new SpriteBatch(device);

                device.SetRenderTarget(rt);
                device.Clear(clearColor ?? Color.Transparent);
                draw(spriteBatch);

                return ToTexture2D(device, rt);
            }
            finally
            {
                if (previous.Length > 0) device.SetRenderTargets(previous);
                else device.SetRenderTarget(null);
            }
        }
    }
}
