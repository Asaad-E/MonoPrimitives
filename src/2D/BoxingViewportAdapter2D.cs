#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoPrimitives.Primitives2D
{
    /// <summary>
    /// Letterboxes/pillarboxes a fixed virtual resolution into the window: uniform scale (the
    /// larger of the two axes' fit), preserving aspect ratio — black bars on the short axis
    /// instead of stretching. The common choice for pixel-art or fixed-composition prototypes.
    /// </summary>
    public sealed class BoxingViewportAdapter2D : ViewportAdapter2D
    {
        public override int VirtualWidth { get; }
        public override int VirtualHeight { get; }

        public BoxingViewportAdapter2D(GraphicsDevice device, int virtualWidth, int virtualHeight) : base(device)
        {
            if (virtualWidth <= 0 || virtualHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(virtualWidth), "Virtual resolution must be positive.");
            VirtualWidth = virtualWidth;
            VirtualHeight = virtualHeight;
        }

        /// <summary>Uniform scale factor (same on both axes) that fits the virtual resolution inside the window without cropping.</summary>
        public override Vector2 Scale
        {
            get
            {
                Viewport vp = Device.Viewport;
                float scaleX = (float)vp.Width / VirtualWidth;
                float scaleY = (float)vp.Height / VirtualHeight;
                float scale = MathF.Min(scaleX, scaleY);
                return new Vector2(scale, scale);
            }
        }

        public override Vector2 Offset
        {
            get
            {
                Viewport vp = Device.Viewport;
                Vector2 scale = Scale;
                return new Vector2((vp.Width - VirtualWidth * scale.X) * 0.5f, (vp.Height - VirtualHeight * scale.Y) * 0.5f);
            }
        }
    }
}
