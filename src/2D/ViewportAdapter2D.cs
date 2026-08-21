#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoPrimitives.Primitives2D
{
    /// <summary>
    /// Maps a fixed "virtual" resolution onto the actual window — the same family MonoGame.Extended
    /// offers (<c>DefaultViewportAdapter</c>/<c>BoxingViewportAdapter</c>/<c>ScalingViewportAdapter</c>/
    /// <c>WindowViewportAdapter</c>), folded into this library so a prototype doesn't need the
    /// Extended package just for resolution independence. Game logic and drawing work in
    /// <see cref="VirtualWidth"/>×<see cref="VirtualHeight"/> coordinates regardless of the
    /// actual window size; <see cref="GetScaleMatrix"/> maps that virtual space onto the window.
    /// Compose with a <see cref="Camera2D"/>'s own transform the same way across every subclass:
    /// pass <c>camera.GetTransformMatrix() * adapter.GetScaleMatrix()</c> into
    /// <c>PrimitiveBatch.Begin</c>.
    /// </summary>
    public abstract class ViewportAdapter2D
    {
        /// <summary>Device this adapter reads the actual window/viewport size from.</summary>
        protected readonly GraphicsDevice Device;

        protected ViewportAdapter2D(GraphicsDevice device) => Device = device ?? throw new ArgumentNullException(nameof(device));

        /// <summary>Fixed width of the virtual coordinate space.</summary>
        public abstract int VirtualWidth { get; }

        /// <summary>Fixed height of the virtual coordinate space.</summary>
        public abstract int VirtualHeight { get; }

        /// <summary>Per-axis scale factor mapping virtual coordinates onto the actual window. Non-uniform for subclasses that stretch (e.g. <see cref="ScalingViewportAdapter2D"/>).</summary>
        public abstract Vector2 Scale { get; }

        /// <summary>Pixel offset of the virtual viewport's top-left corner within the window.</summary>
        public abstract Vector2 Offset { get; }

        /// <summary>The actual on-screen rectangle the virtual resolution occupies once scaled and offset.</summary>
        public virtual Rectangle BoundingRectangle
        {
            get
            {
                Vector2 offset = Offset;
                Vector2 scale = Scale;
                return new Rectangle((int)offset.X, (int)offset.Y, (int)MathF.Round(VirtualWidth * scale.X), (int)MathF.Round(VirtualHeight * scale.Y));
            }
        }

        /// <summary>Matrix mapping virtual coordinates to actual window pixels.</summary>
        public virtual Matrix GetScaleMatrix() => Matrix.CreateScale(Scale.X, Scale.Y, 1f) * Matrix.CreateTranslation(Offset.X, Offset.Y, 0f);

        /// <summary>Converts an actual-window position (e.g. raw mouse coordinates) into virtual/game coordinates.</summary>
        public Vector2 PointToVirtual(Vector2 windowPosition) => Vector2.Transform(windowPosition, Matrix.Invert(GetScaleMatrix()));

        /// <summary>Converts a virtual/game position into actual-window coordinates.</summary>
        public Vector2 VirtualToPoint(Vector2 virtualPosition) => Vector2.Transform(virtualPosition, GetScaleMatrix());

        /// <summary>
        /// Sets the <see cref="GraphicsDevice"/>'s active viewport to <see cref="BoundingRectangle"/>,
        /// so hardware clears/clips stop at the virtual viewport's edge instead of covering the
        /// full window. Call once after resizing (or once per frame before drawing, if simplest).
        /// Don't combine this with <see cref="GetScaleMatrix"/> for 2D drawing — the matrix already
        /// bakes in <see cref="Offset"/>, so narrowing the device viewport too double-applies it
        /// (only visible for adapters with a nonzero Offset, e.g. <see cref="BoxingViewportAdapter2D"/>).
        /// Use one or the other: either skip Apply() and draw with the full <see cref="GetScaleMatrix"/>
        /// against the whole window, or call Apply() and drop Offset from the draw transform
        /// (<c>Matrix.CreateScale(Scale.X, Scale.Y, 1f)</c> only). <see cref="Primitive3DBatch"/>'s
        /// camera-driven 3D path uses Apply() this second way internally and is unaffected.
        /// </summary>
        public virtual void Apply() => Device.Viewport = new Viewport(BoundingRectangle);

        /// <summary>Restores the <see cref="GraphicsDevice"/>'s viewport to the full window.</summary>
        public void Reset() => Device.Viewport = new Viewport(0, 0, Device.PresentationParameters.BackBufferWidth, Device.PresentationParameters.BackBufferHeight);
    }
}
