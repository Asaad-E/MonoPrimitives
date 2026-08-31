using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives.Primitives2D;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Coverage for the three simplest <c>ViewportAdapter2D</c> variants -- <c>Default</c>,
    /// <c>Scaling</c>, and <c>Window</c> -- which <c>BoxingViewportAdapter2D</c>/
    /// <c>CoverViewportAdapter2D</c> already have their own dedicated (and more involved) test
    /// files for.
    /// </summary>
    internal static class ViewportAdapter2DVariantsTests
    {
        public static void Run(Game game, GraphicsDevice device, TestResults results)
        {
            int bw = device.PresentationParameters.BackBufferWidth;
            int bh = device.PresentationParameters.BackBufferHeight;

            results.Check("DefaultViewportAdapter2D: tracks the device viewport live, Scale is always (1,1), Offset always zero", () =>
            {
                var adapter = new DefaultViewportAdapter2D(device);
                if (adapter.VirtualWidth != device.Viewport.Width) return $"expected VirtualWidth == Viewport.Width ({device.Viewport.Width}), got {adapter.VirtualWidth}";
                if (adapter.VirtualHeight != device.Viewport.Height) return $"expected VirtualHeight == Viewport.Height ({device.Viewport.Height}), got {adapter.VirtualHeight}";
                if (adapter.Scale != Vector2.One) return $"expected Scale == (1,1), got {adapter.Scale}";
                if (adapter.Offset != Vector2.Zero) return $"expected Offset == (0,0), got {adapter.Offset}";
                return null;
            });

            results.Check("DefaultViewportAdapter2D: BoundingRectangle matches the device viewport exactly (1:1, no offset)", () =>
            {
                var adapter = new DefaultViewportAdapter2D(device);
                Rectangle expected = new(0, 0, device.Viewport.Width, device.Viewport.Height);
                if (adapter.BoundingRectangle != expected) return $"expected {expected}, got {adapter.BoundingRectangle}";
                return null;
            });

            results.Check("ScalingViewportAdapter2D: Scale is independent per axis, always exactly fills the window", () =>
            {
                // Deliberately not proportional to the window, so scaleX != scaleY -- Scaling is
                // the one adapter that's fine with that (it stretches, no bars, no cropping).
                int vw = bw / 2;
                int vh = bh / 3;
                var adapter = new ScalingViewportAdapter2D(device, vw, vh);

                float expectedX = (float)bw / vw, expectedY = (float)bh / vh;
                if (MathF.Abs(adapter.Scale.X - expectedX) > 1e-4f) return $"expected Scale.X ~{expectedX}, got {adapter.Scale.X}";
                if (MathF.Abs(adapter.Scale.Y - expectedY) > 1e-4f) return $"expected Scale.Y ~{expectedY}, got {adapter.Scale.Y}";
                if (adapter.Offset != Vector2.Zero) return $"expected Offset == (0,0) -- Scaling never shows bars, got {adapter.Offset}";
                return null;
            });

            results.Check("ScalingViewportAdapter2D: BoundingRectangle always equals the full backbuffer exactly", () =>
            {
                int vw = bw / 2;
                int vh = bh / 3;
                var adapter = new ScalingViewportAdapter2D(device, vw, vh);
                Rectangle expected = new(0, 0, bw, bh);
                if (adapter.BoundingRectangle != expected) return $"expected {expected} (the full backbuffer), got {adapter.BoundingRectangle}";
                return null;
            });

            results.Check("ScalingViewportAdapter2D: GetScaleMatrix maps virtual (0,0) and (vw,vh) onto the window's own corners", () =>
            {
                int vw = bw / 2;
                int vh = bh / 3;
                var adapter = new ScalingViewportAdapter2D(device, vw, vh);
                Matrix m = adapter.GetScaleMatrix();

                Vector2 topLeft = Vector2.Transform(Vector2.Zero, m);
                Vector2 bottomRight = Vector2.Transform(new Vector2(vw, vh), m);
                if (Vector2.DistanceSquared(topLeft, Vector2.Zero) > 1e-2f) return $"expected virtual (0,0) to map to window (0,0), got {topLeft}";
                if (MathF.Abs(bottomRight.X - bw) > 0.5f || MathF.Abs(bottomRight.Y - bh) > 0.5f) return $"expected virtual ({vw},{vh}) to map to window ({bw},{bh}), got {bottomRight}";
                return null;
            });

            results.Check("ScalingViewportAdapter2D: non-positive virtual size throws", () =>
            {
                try { new ScalingViewportAdapter2D(device, 0, 100); return "expected ArgumentOutOfRangeException for width=0"; }
                catch (ArgumentOutOfRangeException) { return null; }
            });

            results.Check("WindowViewportAdapter2D: tracks GameWindow.ClientBounds live, Scale is always (1,1), Offset always zero", () =>
            {
                var adapter = new WindowViewportAdapter2D(device, game.Window);
                if (adapter.VirtualWidth != game.Window.ClientBounds.Width) return $"expected VirtualWidth == ClientBounds.Width ({game.Window.ClientBounds.Width}), got {adapter.VirtualWidth}";
                if (adapter.VirtualHeight != game.Window.ClientBounds.Height) return $"expected VirtualHeight == ClientBounds.Height ({game.Window.ClientBounds.Height}), got {adapter.VirtualHeight}";
                if (adapter.Scale != Vector2.One) return $"expected Scale == (1,1), got {adapter.Scale}";
                if (adapter.Offset != Vector2.Zero) return $"expected Offset == (0,0), got {adapter.Offset}";
                return null;
            });

            results.Check("WindowViewportAdapter2D: a null window throws instead of deferring the NullReferenceException to first use", () =>
            {
                try { new WindowViewportAdapter2D(device, null!); return "expected ArgumentNullException for a null window"; }
                catch (ArgumentNullException) { return null; }
            });
        }
    }
}
