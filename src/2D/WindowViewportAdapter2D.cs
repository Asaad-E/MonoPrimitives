#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Primitives2D
{
    /// <summary>
    /// 1:1 with the game window's own client area (<see cref="GameWindow.ClientBounds"/>), not
    /// the device's viewport — the distinction matters right after a resize, on some backends,
    /// before the device has caught up to the new window size. Scale is always (1,1), offset
    /// always zero, same as <see cref="DefaultViewportAdapter2D"/>; use this one specifically
    /// when you need window-client-area size rather than device-viewport size.
    /// </summary>
    public sealed class WindowViewportAdapter2D : ViewportAdapter2D
    {
        private readonly GameWindow _window;

        public WindowViewportAdapter2D(GraphicsDevice device, GameWindow window) : base(device)
            => _window = window ?? throw new ArgumentNullException(nameof(window));

        public override int VirtualWidth => _window.ClientBounds.Width;
        public override int VirtualHeight => _window.ClientBounds.Height;
        public override Vector2 Scale => Vector2.One;
        public override Vector2 Offset => Vector2.Zero;
    }
}
