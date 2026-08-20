#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Primitives2D
{
    /// <summary>
    /// No virtual resolution at all — 1:1 with the device's actual viewport, tracked live (no
    /// fixed <see cref="VirtualWidth"/>/<see cref="VirtualHeight"/> to set up). Scale is always
    /// (1,1) and offset always zero; exists so code written against
    /// <see cref="ViewportAdapter2D"/> works unchanged whether or not the caller actually wants
    /// resolution independence — swap in a <see cref="BoxingViewportAdapter2D"/> later without
    /// touching anything downstream.
    /// </summary>
    public sealed class DefaultViewportAdapter2D : ViewportAdapter2D
    {
        public DefaultViewportAdapter2D(GraphicsDevice device) : base(device) { }

        public override int VirtualWidth => Device.Viewport.Width;
        public override int VirtualHeight => Device.Viewport.Height;
        public override Vector2 Scale => Vector2.One;
        public override Vector2 Offset => Vector2.Zero;
    }
}
