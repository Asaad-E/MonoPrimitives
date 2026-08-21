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
                return new Vector2(scale, scale);
            }
        }

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
