using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace MonoPrimitives.Tests
{
    /// <summary>Checks for <see cref="Camera2D"/>'s bounds/visibility/follow/fit helpers, using the headless <see cref="GraphicsDevice"/> the test runner already has (no <see cref="ViewportAdapter2D"/> needed — these methods accept a plain device directly).</summary>
    internal static class Camera2DTests
    {
        private static bool Close(float a, float b, float eps = 1e-3f) => MathF.Abs(a - b) < eps;
        private static bool Close(Vector2 a, Vector2 b, float eps = 1e-2f) => Vector2.Distance(a, b) < eps;

        public static void Run(GraphicsDevice device, TestResults results)
        {
            results.Check("GetVisibleWorldBoundsF/IsVisible: a point at Target is visible, a point far outside the viewport isn't, and bounds match viewport size at zoom 1", () =>
            {
                var cam = new Camera2D(target: Vector2.Zero, offset: new Vector2(device.Viewport.Width * 0.5f, device.Viewport.Height * 0.5f));
                RectangleF bounds = cam.GetVisibleWorldBoundsF(device);
                if (!Close(bounds.Width, device.Viewport.Width, 0.5f) || !Close(bounds.Height, device.Viewport.Height, 0.5f))
                    return $"visible bounds {bounds.Width}x{bounds.Height} != viewport {device.Viewport.Width}x{device.Viewport.Height} at zoom 1";

                if (!cam.IsVisible(Vector2.Zero, device)) return "Target itself was reported not visible";
                if (cam.IsVisible(new Vector2(1_000_000f, 0f), device)) return "a point far outside the viewport was reported visible";

                var overlapping = new RectangleF(-10f, -10f, 20f, 20f);
                if (!cam.IsVisible(overlapping, device)) return "a rect straddling the origin was reported not visible";
                var farAway = new RectangleF(1_000_000f, 1_000_000f, 10f, 10f);
                if (cam.IsVisible(farAway, device)) return "a rect far outside the viewport was reported visible";
                return null;
            });

            results.Check("FollowTarget(deadZoneHalfSize) converges toward the desired target and holds still inside the deadzone", () =>
            {
                var cam = new Camera2D(target: Vector2.Zero, offset: Vector2.Zero) { FollowSmoothTime = 0.1f };
                var desired = new Vector2(500f, 0f);
                for (int i = 0; i < 300; i++) cam.FollowTarget(desired, 1f / 60f, new Vector2(20f, 20f));
                if (!Close(cam.Target, new Vector2(480f, 0f), 1f)) return $"FollowTarget settled at {cam.Target}, expected ~(480,0) (desired minus the 20-unit deadzone half-size)";

                var cam2 = new Camera2D(target: Vector2.Zero, offset: Vector2.Zero);
                cam2.FollowTarget(new Vector2(5f, 0f), 1f / 60f, new Vector2(20f, 20f)); // within the deadzone
                if (!Close(cam2.Target, Vector2.Zero)) return $"FollowTarget moved inside its own deadzone: {cam2.Target}";
                return null;
            });

            results.Check("FitBounds sets Zoom/Target so the requested world rect exactly fits the viewport (limited by the tighter axis)", () =>
            {
                var cam = new Camera2D(target: Vector2.Zero, offset: Vector2.Zero);
                var worldBounds = new RectangleF(-50f, -25f, 100f, 50f); // center (0,0), 100x50
                cam.FitBounds(worldBounds, padding: 0f, device);

                float expectedZoom = MathF.Min(device.Viewport.Width / 100f, device.Viewport.Height / 50f);
                if (!Close(cam.Zoom, expectedZoom, 0.01f)) return $"FitBounds set Zoom={cam.Zoom}, expected ~{expectedZoom}";
                if (!Close(cam.Target, Vector2.Zero)) return $"FitBounds set Target={cam.Target}, expected the bounds' center (0,0)";
                return null;
            });
        }
    }
}
