#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Primitives2D
{
    /// <summary>
    /// Stretches a fixed virtual resolution to exactly fill the window — independent X/Y scale
    /// factors, no letterbox/pillarbox bars, but distorts the aspect ratio if the window's
    /// proportions don't match the virtual resolution's. Use <see cref="BoxingViewportAdapter2D"/>
    /// instead when preserving aspect ratio matters more than filling the whole window.
    /// </summary>
    public sealed class ScalingViewportAdapter2D : ViewportAdapter2D
    {
        public override int VirtualWidth { get; }
        public override int VirtualHeight { get; }

        public ScalingViewportAdapter2D(GraphicsDevice device, int virtualWidth, int virtualHeight) : base(device)
        {
            if (virtualWidth <= 0 || virtualHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(virtualWidth), "Virtual resolution must be positive.");
            VirtualWidth = virtualWidth;
            VirtualHeight = virtualHeight;
        }

        /// <summary>Independent per-axis scale — always fills the window exactly, so the two axes generally differ.</summary>
        public override Vector2 Scale
        {
            get
            {
                Viewport vp = Device.Viewport;
                return new Vector2((float)vp.Width / VirtualWidth, (float)vp.Height / VirtualHeight);
            }
        }

        /// <summary>Always zero — the virtual resolution fills the window exactly, no bars to offset around.</summary>
        public override Vector2 Offset => Vector2.Zero;
    }
}
