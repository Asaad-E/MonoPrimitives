#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoPrimitives.Primitives2D
{
    /// <summary>
    /// Uniformly scales a fixed virtual resolution to completely fill the window — no
    /// letterbox/pillarbox bars, but whatever overflows the window on one axis is cropped instead
    /// of shown.
    /// </summary>
    /// <remarks>The inverse tradeoff from <see cref="BoxingViewportAdapter2D"/> (which fits everything inside the window, with bars on the short axis instead of cropping) — matches CSS's <c>object-fit: cover</c>. Use <see cref="ScalingViewportAdapter2D"/> instead if distorting the aspect ratio is preferable to cropping.</remarks>
    public sealed class CoverViewportAdapter2D : ViewportAdapter2D
    {
        /// <inheritdoc/>
        public override int VirtualWidth { get; }
        /// <inheritdoc/>
        public override int VirtualHeight { get; }

        /// <summary>Wraps <paramref name="device"/>, uniformly scaling a <paramref name="virtualWidth"/>×<paramref name="virtualHeight"/> virtual resolution to completely fill it, cropping any overflow.</summary>
        public CoverViewportAdapter2D(GraphicsDevice device, int virtualWidth, int virtualHeight) : base(device)
        {
            if (virtualWidth <= 0 || virtualHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(virtualWidth), "Virtual resolution must be positive.");
            VirtualWidth = virtualWidth;
            VirtualHeight = virtualHeight;
        }

        /// <summary>Uniform scale factor (same on both axes) that fills the window completely — the larger of the two axes' fit, the opposite of <see cref="BoxingViewportAdapter2D.Scale"/>'s smaller-axis fit.</summary>
        public override Vector2 Scale
        {
            get
            {
                // Stable backbuffer size, not Device.Viewport directly -- see
                // BoxingViewportAdapter2D.Scale's doc comment for why (same fix, same reason).
                PresentationParameters pp = Device.PresentationParameters;
                float scaleX = (float)pp.BackBufferWidth / VirtualWidth;
                float scaleY = (float)pp.BackBufferHeight / VirtualHeight;
                float scale = MathF.Max(scaleX, scaleY);
                return new Vector2(scale, scale);
            }
        }

        /// <summary>Pixel offset of the (overflowing) virtual content's top-left corner — negative on whichever axis overflows the window, since that content starts before the window's own edge.</summary>
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
