#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoPrimitives.Primitives2D
{
    /// <summary>
    /// No virtual resolution at all — 1:1 with the device's actual viewport, tracked live (no
    /// fixed <see cref="VirtualWidth"/>/<see cref="VirtualHeight"/> to set up). Scale is always
    /// (1,1) and offset always zero.
    /// </summary>
    /// <remarks>
    /// Exists so code written against <see cref="ViewportAdapter2D"/> works unchanged whether or
    /// not the caller actually wants resolution independence — swap in a
    /// <see cref="BoxingViewportAdapter2D"/> later without touching anything downstream.
    /// </remarks>
    public sealed class DefaultViewportAdapter2D : ViewportAdapter2D
    {
        /// <summary>Wraps <paramref name="device"/> — no virtual resolution to configure, since this adapter always tracks the device's own viewport.</summary>
        public DefaultViewportAdapter2D(GraphicsDevice device) : base(device) { }

        /// <inheritdoc/>
        public override int VirtualWidth => Device.Viewport.Width;
        /// <inheritdoc/>
        public override int VirtualHeight => Device.Viewport.Height;
        /// <inheritdoc/>
        public override Vector2 Scale => Vector2.One;
        /// <inheritdoc/>
        public override Vector2 Offset => Vector2.Zero;
    }
}
