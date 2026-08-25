#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoPrimitives.Primitives2D
{
    /// <summary>Maps a fixed "virtual" resolution onto the actual window; <see cref="GetScaleMatrix"/> maps virtual coordinates onto it.</summary>
    /// <remarks><see cref="Camera2D"/> folds this in automatically when constructed with an adapter — <c>camera.GetTransformMatrix()</c> alone is enough to pass into <c>Primitive2DBatch.Begin</c>.</remarks>
    public abstract class ViewportAdapter2D
    {
        /// <summary>Device this adapter reads the actual window/viewport size from.</summary>
        protected readonly GraphicsDevice Device;

        /// <summary>For subclasses: stores <paramref name="device"/> as <see cref="Device"/>.</summary>
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
                return new Rectangle((int)MathF.Round(offset.X), (int)MathF.Round(offset.Y), (int)MathF.Round(VirtualWidth * scale.X), (int)MathF.Round(VirtualHeight * scale.Y));
            }
        }

        /// <summary>Matrix mapping virtual coordinates to actual window pixels.</summary>
        public virtual Matrix GetScaleMatrix() => Matrix.CreateScale(Scale.X, Scale.Y, 1f) * Matrix.CreateTranslation(Offset.X, Offset.Y, 0f);

        /// <summary>Converts an actual-window position (e.g. raw mouse coordinates) into virtual/game coordinates.</summary>
        public Vector2 PointToVirtual(Vector2 windowPosition) => Vector2.Transform(windowPosition, Matrix.Invert(GetScaleMatrix()));

        /// <summary>Converts a virtual/game position into actual-window coordinates.</summary>
        public Vector2 VirtualToPoint(Vector2 virtualPosition) => Vector2.Transform(virtualPosition, GetScaleMatrix());

        /// <summary>Sets the <see cref="GraphicsDevice"/>'s active viewport to <see cref="BoundingRectangle"/>, so hardware clears/clips stop at its edge instead of covering the full window.</summary>
        /// <remarks>Don't also bake <see cref="Offset"/> into a draw's transform matrix after calling this — the narrowed viewport already applies it, so using both double-applies it. And <see cref="GraphicsDevice.Clear(Color)"/> ignores a narrowed viewport and clears the whole render target regardless, so a Clear() after Apply() wipes the bars too, not just the inside.</remarks>
        public virtual void Apply() => Device.Viewport = new Viewport(BoundingRectangle);

        /// <summary>Restores the <see cref="GraphicsDevice"/>'s viewport to the full window.</summary>
        public void Reset() => Device.Viewport = new Viewport(0, 0, Device.PresentationParameters.BackBufferWidth, Device.PresentationParameters.BackBufferHeight);
    }
}
