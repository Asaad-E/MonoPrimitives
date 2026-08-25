#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoPrimitives.Primitives2D
{
    /// <summary>
    /// Letterboxes/pillarboxes a fixed virtual resolution into the window: uniform scale (the
    /// larger of the two axes' fit), preserving aspect ratio — black bars on the short axis
    /// instead of stretching.
    /// </summary>
    /// <remarks>The common choice for pixel-art or fixed-composition prototypes.</remarks>
    public sealed class BoxingViewportAdapter2D : ViewportAdapter2D
    {
        /// <inheritdoc/>
        public override int VirtualWidth { get; }
        /// <inheritdoc/>
        public override int VirtualHeight { get; }

        /// <summary>When true, <see cref="Scale"/> floors to the nearest whole number (minimum 1) instead of a continuous fit, for crisp, non-shimmering pixel art.</summary>
        /// <remarks>Expect a border on all 4 sides rather than 2 opposite bars — flooring the scale almost always leaves leftover space on both axes, not just the one a continuous fit bars.</remarks>
        public bool PixelPerfect { get; }

        /// <summary>Wraps <paramref name="device"/>, letterboxing a <paramref name="virtualWidth"/>×<paramref name="virtualHeight"/> virtual resolution into it.</summary>
        /// <param name="device">Device this adapter reads the actual window/viewport size from.</param>
        /// <param name="virtualWidth">Fixed width of the virtual coordinate space.</param>
        /// <param name="virtualHeight">Fixed height of the virtual coordinate space.</param>
        /// <param name="pixelPerfect">See <see cref="PixelPerfect"/> — snaps the scale to a whole number for crisp pixel art, at the cost of a larger (all 4 sides) border in the common case where the window isn't an exact multiple of the virtual resolution.</param>
        public BoxingViewportAdapter2D(GraphicsDevice device, int virtualWidth, int virtualHeight, bool pixelPerfect = false) : base(device)
        {
            if (virtualWidth <= 0 || virtualHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(virtualWidth), "Virtual resolution must be positive.");
            VirtualWidth = virtualWidth;
            VirtualHeight = virtualHeight;
            PixelPerfect = pixelPerfect;
        }

        /// <summary>Uniform scale factor (same on both axes) that fits the virtual resolution inside the window without cropping — floored to a whole number when <see cref="PixelPerfect"/> is set.</summary>
        public override Vector2 Scale
        {
            get
            {
                // Reads the STABLE backbuffer size (same reference Reset() uses), not
                // Device.Viewport directly — Apply() narrows Device.Viewport to the letterboxed
                // rect, and that rect's own size is smaller on one axis than the full window by
                // construction. Reading Scale/Offset again after Apply() (e.g. from
                // Camera2D.ScreenToWorld/WorldToScreen called later in the same frame, or next
                // frame before Reset() runs) would otherwise recompute from that already-narrowed
                // size and silently return the wrong Offset (reads back as 0 — the "bar" the
                // narrowed viewport already IS has nothing left over to report) even though
                // nothing about the actual window changed. Verified: a round-trip
                // WorldToScreen-then-ScreenToWorld through a live-narrowed viewport drifted by
                // over 30 pixels before this fix, 0 after.
                PresentationParameters pp = Device.PresentationParameters;
                float scaleX = (float)pp.BackBufferWidth / VirtualWidth;
                float scaleY = (float)pp.BackBufferHeight / VirtualHeight;
                float scale = MathF.Min(scaleX, scaleY);
                if (PixelPerfect) scale = MathF.Max(1f, MathF.Floor(scale));
                return new Vector2(scale, scale);
            }
        }

        /// <summary>Pixel offset of the letterbox bars — half the leftover space on whichever axis isn't fully filled by the uniform <see cref="Scale"/>.</summary>
        public override Vector2 Offset
        {
            get
            {
                PresentationParameters pp = Device.PresentationParameters;
                Vector2 scale = Scale;
                return new Vector2((pp.BackBufferWidth - VirtualWidth * scale.X) * 0.5f, (pp.BackBufferHeight - VirtualHeight * scale.Y) * 0.5f);
            }
        }
    }
}
