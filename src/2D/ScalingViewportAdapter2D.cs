#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoPrimitives.Primitives2D
{
    /// <summary>
    /// Stretches a fixed virtual resolution to exactly fill the window — independent X/Y scale
    /// factors, no letterbox/pillarbox bars, but distorts the aspect ratio if the window's
    /// proportions don't match the virtual resolution's.
    /// </summary>
    /// <remarks>Use <see cref="BoxingViewportAdapter2D"/> instead when preserving aspect ratio matters more than filling the whole window.</remarks>
    public sealed class ScalingViewportAdapter2D : ViewportAdapter2D
    {
        /// <inheritdoc/>
        public override int VirtualWidth { get; }
        /// <inheritdoc/>
        public override int VirtualHeight { get; }

        /// <summary>Wraps <paramref name="device"/>, stretching a <paramref name="virtualWidth"/>×<paramref name="virtualHeight"/> virtual resolution to exactly fill it.</summary>
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
                // Stable backbuffer size, not Device.Viewport directly — see
                // BoxingViewportAdapter2D.Scale's doc comment for why (same fix, same reason).
                // Doesn't currently misrender for this adapter specifically (Apply() narrows to
                // exactly the full backbuffer size here, since Offset is always 0 and Scale
                // already fills it exactly, so re-reading after Apply() happens to still agree)
                // but reading the stable reference either way removes the fragility instead of
                // relying on that coincidence.
                PresentationParameters pp = Device.PresentationParameters;
                return new Vector2((float)pp.BackBufferWidth / VirtualWidth, (float)pp.BackBufferHeight / VirtualHeight);
            }
        }

        /// <summary>Always zero — the virtual resolution fills the window exactly, no bars to offset around.</summary>
        public override Vector2 Offset => Vector2.Zero;
    }
}
