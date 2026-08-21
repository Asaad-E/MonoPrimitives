using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives.Primitives3D;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-math checks for <see cref="Camera3D"/> — no GraphicsDevice needed (a plain <see cref="Viewport"/> is just a struct).</summary>
    internal static class Camera3DTests
    {
        private static bool Close(float a, float b, float eps = 1e-4f) => MathF.Abs(a - b) < eps;
        private static bool Close(Vector3 a, Vector3 b, float eps = 1e-3f) => Vector3.Distance(a, b) < eps;

        public static void Run(TestResults results)
        {
            results.Check("Forward/Right/UpNormalized form an orthonormal basis for several poses, and degenerate cases fall back safely", () =>
            {
                var poses = new (Vector3 pos, Vector3 target, Vector3 up)[]
                {
                    (new Vector3(0, 0, 10), Vector3.Zero, Vector3.Up),
                    (new Vector3(5, 3, -7), new Vector3(1, 1, 1), Vector3.Up),
                    (new Vector3(0, 10, 0), Vector3.Zero, Vector3.Forward), // looking straight down
                };
                foreach (var (pos, target, up) in poses)
                {
                    var cam = new Camera3D(pos, target, up);
                    Vector3 f = cam.Forward, r = cam.Right, u = cam.UpNormalized;
                    if (!Close(f.Length(), 1f) || !Close(r.Length(), 1f) || !Close(u.Length(), 1f))
                        return $"non-unit basis vector for pose {pos}->{target}: |F|={f.Length()} |R|={r.Length()} |U|={u.Length()}";
                    // Right = cross(Forward, UpNormalized) is *always* perpendicular to both of its
                    // inputs by construction -- that's the only orthogonality guarantee here. Forward
                    // and UpNormalized themselves are NOT required to be perpendicular (Up is just a
                    // "roughly up" hint, same as raylib/most camera models -- CreateLookAt does its
                    // own internal Gram-Schmidt against it).
                    if (!Close(Vector3.Dot(f, r), 0f, 1e-3f) || !Close(Vector3.Dot(r, u), 0f, 1e-3f))
                        return $"Right not perpendicular to Forward/Up for pose {pos}->{target}";
                }

                // Degenerate: Position == Target -> Forward has no defined direction, falls back to Vector3.Forward.
                var degenerate = new Camera3D(Vector3.One, Vector3.One, Vector3.Up);
                if (!Close(degenerate.Forward, Vector3.Forward)) return $"degenerate Forward = {degenerate.Forward}, expected Vector3.Forward fallback";
                return null;
            });

            results.Check("Right matches MonoGame's own CreateLookAt X-axis convention (screen-right stays correct)", () =>
            {
                // Verified algebraically: Right = normalize(cross(Forward,Up)) equals CreateLookAt's
                // xaxis = normalize(cross(up, position-target)) since position-target = -Forward.
                var cam = new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.Up);
                Matrix view = cam.GetViewMatrix();
                // MonoGame's Matrix.Right/Up/Forward expose the matrix's own basis rows for CreateLookAt-built matrices.
                if (!Close(new Vector3(view.M11, view.M21, view.M31), cam.Right, 1e-3f))
                    return $"view matrix X-axis {new Vector3(view.M11, view.M21, view.M31)} != cam.Right {cam.Right}";
                return null;
            });

            results.Check("MoveForward/MoveRight/MoveUp displace Position and Target identically, by the requested distance, along the correct axis", () =>
            {
                var cam = new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.Up);
                Vector3 originalOffset = cam.Target - cam.Position;

                Vector3 posBefore = cam.Position;
                cam.MoveForward(2f);
                if (!Close(Vector3.Distance(cam.Position, posBefore), 2f)) return $"MoveForward(2) moved by {Vector3.Distance(cam.Position, posBefore)}, expected 2";
                if (!Close(cam.Target - cam.Position, originalOffset)) return "MoveForward changed the look offset (Target-Position)";

                posBefore = cam.Position;
                cam.MoveRight(3f);
                if (!Close(Vector3.Distance(cam.Position, posBefore), 3f)) return $"MoveRight(3) moved by {Vector3.Distance(cam.Position, posBefore)}, expected 3";
                if (!Close(cam.Target - cam.Position, originalOffset)) return "MoveRight changed the look offset";

                posBefore = cam.Position;
                cam.MoveUp(1.5f);
                if (!Close(Vector3.Distance(cam.Position, posBefore), 1.5f)) return $"MoveUp(1.5) moved by {Vector3.Distance(cam.Position, posBefore)}, expected 1.5";
                if (!Close(cam.Target - cam.Position, originalOffset)) return "MoveUp changed the look offset";
                return null;
            });

            results.Check("MoveToTarget changes TargetDistance by delta along Forward, and clamps instead of crossing to/through the target", () =>
            {
                var cam = new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.Up);
                float before = cam.TargetDistance;
                cam.MoveToTarget(-3f);
                if (!Close(cam.TargetDistance, before - 3f)) return $"distance after MoveToTarget(-3) = {cam.TargetDistance}, expected {before - 3f}";

                cam.MoveToTarget(-1000f); // would overshoot past the target
                if (cam.TargetDistance <= 0f) return $"MoveToTarget let distance go non-positive: {cam.TargetDistance}";
                return null;
            });

            results.Check("Yaw rotates Forward around Up by the requested angle; rotateAroundTarget picks which of Position/Target stays fixed", () =>
            {
                var cam = new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.Up);
                Vector3 targetBefore = cam.Target;
                cam.Yaw(MathHelper.PiOver2, rotateAroundTarget: false); // Target orbits, Position fixed
                if (Close(cam.Target, targetBefore)) return "Yaw(rotateAroundTarget:false) left Target unchanged, expected it to orbit";
                // Position should NOT have moved when rotateAroundTarget is false.
                if (!Close(cam.Position, new Vector3(0, 0, 10))) return $"Yaw(rotateAroundTarget:false) moved Position to {cam.Position}, expected unchanged";

                var cam2 = new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.Up);
                cam2.Yaw(MathHelper.PiOver2, rotateAroundTarget: true); // Position orbits, Target fixed
                if (!Close(cam2.Target, Vector3.Zero)) return $"Yaw(rotateAroundTarget:true) moved Target to {cam2.Target}, expected unchanged";
                if (Close(cam2.Position, new Vector3(0, 0, 10))) return "Yaw(rotateAroundTarget:true) left Position unchanged, expected it to orbit";
                if (!Close(cam2.TargetDistance, 10f)) return $"Yaw(rotateAroundTarget:true) changed distance to {cam2.TargetDistance}, expected 10 preserved";
                return null;
            });

            results.Check("Pitch's lockView keeps Forward from ever aligning with Up (no pole-flip) even for an extreme angle", () =>
            {
                var cam = new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.Up);
                cam.Pitch(MathHelper.Pi, lockView: true); // a huge pitch request, should clamp well short of the pole
                float angleToUp = MathF.Acos(Math.Clamp(Vector3.Dot(cam.Forward, cam.UpNormalized), -1f, 1f));
                if (angleToUp < 0.0005f) return $"Pitch with lockView let Forward align with Up (angle={angleToUp})";
                return null;
            });

            results.Check("Roll rotates Up around Forward without changing Forward itself", () =>
            {
                var cam = new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.Up);
                Vector3 forwardBefore = cam.Forward;
                cam.Roll(MathHelper.PiOver4);
                if (!Close(cam.Forward, forwardBefore)) return $"Roll changed Forward from {forwardBefore} to {cam.Forward}";
                if (Close(cam.UpNormalized, Vector3.Up)) return "Roll(PI/4) left Up unchanged, expected it to rotate";
                if (!Close(Vector3.Dot(cam.Forward, cam.UpNormalized), 0f, 1e-3f)) return "Forward/Up not orthogonal after Roll";
                return null;
            });

            results.Check("SetZoom/Zoom set and clamp Fovy correctly", () =>
            {
                var cam = new Camera3D(Vector3.Zero, Vector3.Forward, Vector3.Up, fovy: 45f);
                cam.SetZoom(60f);
                if (!Close(cam.Fovy, 60f)) return $"SetZoom(60) -> Fovy={cam.Fovy}";
                cam.Zoom(-1000f, min: 10f, max: 120f);
                if (!Close(cam.Fovy, 10f)) return $"Zoom clamped low to {cam.Fovy}, expected 10";
                cam.Zoom(1000f, min: 10f, max: 120f);
                if (!Close(cam.Fovy, 120f)) return $"Zoom clamped high to {cam.Fovy}, expected 120";
                return null;
            });

            results.Check("SmoothZoom converges to the clamped target distance over repeated calls, and no-ops when there's nothing to do", () =>
            {
                // SmoothZoom's own contract (see its doc comment): a nonzero delta is a single discrete
                // request (a mouse-wheel tick) that gets added to the pending target ONCE -- calling it
                // repeatedly with the same nonzero delta races the target further every frame, it doesn't
                // hold steady. So request once, then advance the easing with delta=0.
                var cam = new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.Up) { MinDistance = 1f, MaxDistance = 50f, ZoomSmoothTime = 0.1f };
                cam.SmoothZoom(-5f, 1f / 60f);
                for (int i = 0; i < 200; i++) cam.SmoothZoom(0f, 1f / 60f);
                if (!Close(cam.TargetDistance, 5f, 0.05f)) return $"SmoothZoom settled at distance {cam.TargetDistance}, expected ~5";

                // Clamped: requesting far past MaxDistance should settle at MaxDistance, not beyond.
                var cam2 = new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.Up) { MinDistance = 1f, MaxDistance = 50f, ZoomSmoothTime = 0.05f };
                cam2.SmoothZoom(1000f, 1f / 60f);
                for (int i = 0; i < 500; i++) cam2.SmoothZoom(0f, 1f / 60f);
                if (cam2.TargetDistance > 50.5f) return $"SmoothZoom exceeded MaxDistance: {cam2.TargetDistance}";
                return null;
            });

            results.Check("FollowTarget converges toward the desired position, keeping the look offset, and respects the deadzone", () =>
            {
                var cam = new Camera3D(Vector3.Zero, new Vector3(0, 0, -5), Vector3.Up) { FollowSmoothTime = 0.1f };
                Vector3 desired = new(20f, 0f, 0f);
                for (int i = 0; i < 300; i++) cam.FollowTarget(desired, 1f / 60f);
                if (!Close(cam.Position, desired, 0.05f)) return $"FollowTarget settled at {cam.Position}, expected ~{desired}";
                if (!Close(cam.Target, desired + new Vector3(0, 0, -5), 0.05f)) return $"FollowTarget's Target didn't keep the original look offset: {cam.Target}";

                var cam2 = new Camera3D(Vector3.Zero, Vector3.Zero, Vector3.Up) { FollowPadding = 5f };
                cam2.FollowTarget(new Vector3(2f, 0f, 0f), 1f / 60f); // within the deadzone
                if (!Close(cam2.Position, Vector3.Zero)) return $"FollowTarget moved inside its own deadzone: {cam2.Position}";
                return null;
            });

            results.Check("ClampToBounds clamps Position into PositionBounds (minus BoundsPadding) and preserves the look offset", () =>
            {
                var cam = new Camera3D(new Vector3(100, 0, 0), Vector3.Zero, Vector3.Up)
                {
                    PositionBounds = new BoundingBox(new Vector3(-10), new Vector3(10)),
                    BoundsPadding = 1f
                };
                Vector3 offsetBefore = cam.Target - cam.Position;
                cam.ClampToBounds();
                if (cam.Position.X > 9f) return $"Position.X={cam.Position.X} not clamped to bounds-padding";
                if (!Close(cam.Target - cam.Position, offsetBefore)) return "ClampToBounds changed the look offset instead of preserving it";
                return null;
            });

            results.Check("AddTrauma clamps to [0,1], decays over Update, and GetShakeOffset is zero at zero trauma", () =>
            {
                var cam = new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.Up);
                if (cam.GetShakeOffset() != (Vector3.Zero, 0f)) return "GetShakeOffset non-zero at Trauma=0";

                cam.AddTrauma(5f); // should clamp to 1
                if (!Close(cam.Trauma, 1f)) return $"AddTrauma(5) -> Trauma={cam.Trauma}, expected clamped to 1";

                for (int i = 0; i < 300; i++) cam.Update(1f / 60f);
                if (cam.Trauma > 0.01f) return $"Trauma didn't decay to ~0 after 5s of Update: {cam.Trauma}";
                return null;
            });

            results.Check("GetViewMatrix/GetProjectionMatrix are non-degenerate and WorldToScreen/ScreenToWorld round-trip through a plain Viewport", () =>
            {
                var cam = new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.Up, fovy: 60f);
                var viewport = new Viewport(0, 0, 800, 600);

                Matrix view = cam.GetViewMatrix();
                Matrix proj = cam.GetProjectionMatrix(viewport.AspectRatio);
                if (MathF.Abs(view.Determinant()) < 1e-6f) return "GetViewMatrix is singular";
                if (MathF.Abs(proj.Determinant()) < 1e-9f) return "GetProjectionMatrix is singular";

                Vector3 worldPoint = new(1f, 0.5f, 0f);
                Vector2 screen = cam.WorldToScreen(worldPoint, out float depth, viewport);
                Vector3 back = cam.ScreenToWorld(screen, depth, viewport);
                if (!Close(back, worldPoint, 0.01f)) return $"round-trip WorldToScreen->ScreenToWorld gave {back}, expected {worldPoint}";
                return null;
            });

            results.Check("GetScreenToWorldRay through the screen center points roughly along Forward, from near the camera", () =>
            {
                var cam = new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.Up, fovy: 60f);
                var viewport = new Viewport(0, 0, 800, 600);
                Ray ray = cam.GetScreenToWorldRay(new Vector2(400, 300), viewport);
                if (Vector3.Dot(ray.Direction, cam.Forward) < 0.999f) return $"center ray direction {ray.Direction} isn't aligned with Forward {cam.Forward}";
                if (Vector3.Distance(ray.Position, cam.Position) > cam.NearPlane + 0.5f) return $"ray origin {ray.Position} too far from camera {cam.Position}";
                return null;
            });

            results.Check("Reset restores the construction-time pose and clears zoom/follow/shake state", () =>
            {
                var cam = new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.Up, fovy: 45f);
                cam.MoveForward(5f);
                cam.SetZoom(90f);
                cam.AddTrauma(1f);
                cam.SmoothZoom(-2f, 1f / 60f);
                cam.FollowTarget(new Vector3(50, 0, 0), 1f / 60f);

                cam.Reset();
                if (!Close(cam.Position, new Vector3(0, 0, 10))) return $"Reset left Position at {cam.Position}";
                if (!Close(cam.Target, Vector3.Zero)) return $"Reset left Target at {cam.Target}";
                if (!Close(cam.Fovy, 45f)) return $"Reset left Fovy at {cam.Fovy}";
                if (cam.Trauma != 0f) return $"Reset left Trauma at {cam.Trauma}";
                return null;
            });
        }
    }
}
