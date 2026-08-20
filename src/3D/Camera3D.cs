using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoPrimitives;

namespace MonoPrimitives3D
{
    /// <summary>Camera projection type.</summary>
    public enum CameraProjection
    {
        /// <summary>Perspective projection driven by a vertical field of view.</summary>
        Perspective = 0,

        /// <summary>Orthographic projection where <c>Fovy</c> is the vertical world height.</summary>
        Orthographic = 1
    }

    /// <summary>Camera behaviour modes.</summary>
    public enum CameraMode
    {
        /// <summary>No automatic update; the caller drives the camera (or calls <see cref="Camera3D.FollowTarget"/>/<see cref="Camera3D.SmoothZoom"/> directly).</summary>
        Custom = 0,

        /// <summary>Free fly camera (WASD + mouse look, no gravity or collision).</summary>
        Free = 1,

        /// <summary>Orbital camera that rotates automatically around its target.</summary>
        Orbital = 2,

        /// <summary>First person camera with a fixed eye height and head bobbing.</summary>
        FirstPerson = 3,

        /// <summary>Third person camera offset behind the target.</summary>
        ThirdPerson = 4
    }

    /// <summary>
    /// Per-frame movement/rotation request. Filling this yourself keeps the camera
    /// logic input-agnostic, which matters for replays, network play and tests.
    /// </summary>
    public struct CameraInput
    {
        /// <summary>Movement request: X = right, Y = up, Z = forward. Already scaled by speed and delta time.</summary>
        public Vector3 Movement;

        /// <summary>Rotation request in radians: X = yaw, Y = pitch, Z = roll.</summary>
        public Vector3 Rotation;

        /// <summary>Zoom / target distance delta (mouse wheel).</summary>
        public float Zoom;
    }

    /// <summary>
    /// A 3D camera with its own update/input logic folded into one class — a single object
    /// that owns both its state and its behaviour. Reference type (not a struct) so it can
    /// hold its own smoothing state (follow velocity, zoom velocity, mouse tracking) without
    /// every caller needing to pass it by <c>ref</c>.
    /// </summary>
    public sealed class Camera3D
    {
        // =====================================================================
        // Core fields
        // =====================================================================

        /// <summary>Camera position in world space.</summary>
        public Vector3 Position;

        /// <summary>Point the camera looks at.</summary>
        public Vector3 Target;

        /// <summary>Camera up vector (rotation over its axis).</summary>
        public Vector3 Up;

        /// <summary>Vertical field of view in degrees (perspective) or world height (orthographic).</summary>
        public float Fovy;

        /// <summary>Projection type.</summary>
        public CameraProjection Projection;

        /// <summary>Near clip distance.</summary>
        public float NearPlane;

        /// <summary>Far clip distance.</summary>
        public float FarPlane;

        public const float DefaultNear = 0.1f;
        public const float DefaultFar = 1000f;

        public Camera3D(Vector3 position, Vector3 target, Vector3 up, float fovy = 45f,
                        CameraProjection projection = CameraProjection.Perspective,
                        float nearPlane = DefaultNear, float farPlane = DefaultFar)
        {
            Position = position;
            Target = target;
            Up = up;
            Fovy = fovy;
            Projection = projection;
            NearPlane = nearPlane;
            FarPlane = farPlane;
        }

        /// <summary>Creates a camera with sensible defaults looking at the origin.</summary>
        public static Camera3D CreateDefault() => new(new Vector3(10f, 10f, 10f), Vector3.Zero, Vector3.Up, 45f);

        // ---------------------------------------------------------------------
        // Basis vectors (rcamera.h parity)
        // ---------------------------------------------------------------------

        /// <summary>Normalized forward vector (position -> target).</summary>
        public Vector3 Forward
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Vector3 f = Target - Position;
                float lenSq = f.LengthSquared();
                return lenSq < 1e-12f ? Vector3.Forward : f * (1f / MathF.Sqrt(lenSq));
            }
        }

        /// <summary>Normalized up vector.</summary>
        public Vector3 UpNormalized
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                float lenSq = Up.LengthSquared();
                return lenSq < 1e-12f ? Vector3.Up : Up * (1f / MathF.Sqrt(lenSq));
            }
        }

        /// <summary>Normalized right vector (forward x up).</summary>
        public Vector3 Right
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Vector3 r = Vector3.Cross(Forward, UpNormalized);
                float lenSq = r.LengthSquared();
                return lenSq < 1e-12f ? Vector3.Right : r * (1f / MathF.Sqrt(lenSq));
            }
        }

        /// <summary>Distance from the camera to its target.</summary>
        public float TargetDistance => Vector3.Distance(Position, Target);

        // ---------------------------------------------------------------------
        // Matrices
        // ---------------------------------------------------------------------

        /// <summary>Builds the view matrix.</summary>
        public Matrix GetViewMatrix() => Matrix.CreateLookAt(Position, Target, UpNormalized);

        /// <summary>Builds the projection matrix for the supplied aspect ratio.</summary>
        public Matrix GetProjectionMatrix(float aspectRatio)
        {
            if (Projection == CameraProjection.Orthographic)
            {
                float top = Fovy * 0.5f;
                float right = top * aspectRatio;
                return Matrix.CreateOrthographicOffCenter(-right, right, -top, top, NearPlane, FarPlane);
            }

            return Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(Fovy), aspectRatio, NearPlane, FarPlane);
        }

        /// <summary>Combined view-projection matrix.</summary>
        public Matrix GetViewProjectionMatrix(float aspectRatio) => GetViewMatrix() * GetProjectionMatrix(aspectRatio);

        /// <summary>Approximate world units covered by one screen pixel at unit distance. Used by the batcher to size pixel-width lines.</summary>
        public float GetPixelScale(int viewportHeight)
        {
            if (viewportHeight <= 0)
                return 0.002f;
            if (Projection == CameraProjection.Orthographic)
                return Fovy / viewportHeight;
            return 2f * MathF.Tan(MathHelper.ToRadians(Fovy) * 0.5f) / viewportHeight;
        }

        // ---------------------------------------------------------------------
        // Movement helpers (rcamera.h)
        // ---------------------------------------------------------------------

        /// <summary>Moves the camera and its target forward.</summary>
        /// <param name="distance">Distance to travel; negative moves backwards.</param>
        /// <param name="moveInWorldPlane">When true, motion is projected onto the XZ plane.</param>
        public void MoveForward(float distance, bool moveInWorldPlane = false)
        {
            Vector3 forward = Forward;
            if (moveInWorldPlane)
            {
                forward.Y = 0f;
                forward = Normalize(forward, Vector3.Forward);
            }
            forward *= distance;
            Position += forward;
            Target += forward;
        }

        /// <summary>Moves the camera and its target along the up vector.</summary>
        public void MoveUp(float distance)
        {
            Vector3 up = UpNormalized * distance;
            Position += up;
            Target += up;
        }

        /// <summary>Moves the camera and its target right.</summary>
        public void MoveRight(float distance, bool moveInWorldPlane = false)
        {
            Vector3 right = Right;
            if (moveInWorldPlane)
            {
                right.Y = 0f;
                right = Normalize(right, Vector3.Right);
            }
            right *= distance;
            Position += right;
            Target += right;
        }

        /// <summary>Moves the camera towards or away from its target (instant zoom for orbital modes). The target is never overshot.</summary>
        /// <param name="delta">Negative values move closer, positive move away.</param>
        public void MoveToTarget(float delta)
        {
            float distance = TargetDistance + delta;
            if (distance <= 0f) distance = 0.001f;
            Position = Target - Forward * distance;
        }

        /// <summary>Rotates the camera around its up vector (left/right look).</summary>
        /// <param name="angle">Angle in radians; positive rotates left.</param>
        /// <param name="rotateAroundTarget">When true the camera orbits the target, otherwise the target orbits the camera.</param>
        public void Yaw(float angle, bool rotateAroundTarget = false)
        {
            Vector3 up = UpNormalized;
            Vector3 targetPosition = Vector3.Transform(Target - Position, Matrix.CreateFromAxisAngle(up, angle));
            if (rotateAroundTarget) Position = Target - targetPosition;
            else Target = Position + targetPosition;
        }

        /// <summary>Rotates the camera around its right vector (up/down look).</summary>
        /// <param name="angle">Angle in radians; positive looks up.</param>
        /// <param name="lockView">When true the pitch is clamped so the view cannot flip past the poles.</param>
        /// <param name="rotateAroundTarget">When true the camera orbits the target.</param>
        /// <param name="rotateUp">When true the up vector is rotated too, allowing full free rotation.</param>
        public void Pitch(float angle, bool lockView = true, bool rotateAroundTarget = false, bool rotateUp = false)
        {
            Vector3 up = UpNormalized;
            Vector3 targetPosition = Target - Position;

            if (lockView)
            {
                float maxAngleUp = AngleBetween(up, targetPosition) - 0.001f;
                float maxAngleDown = -(MathF.PI - maxAngleUp - 0.002f);
                if (angle > maxAngleUp) angle = maxAngleUp;
                if (angle < maxAngleDown) angle = maxAngleDown;
            }

            Vector3 right = Right;
            targetPosition = Vector3.Transform(targetPosition, Matrix.CreateFromAxisAngle(right, angle));

            if (rotateAroundTarget) Position = Target - targetPosition;
            else Target = Position + targetPosition;

            if (rotateUp) Up = Vector3.Transform(Up, Matrix.CreateFromAxisAngle(right, angle));
        }

        /// <summary>Rotates the camera around its forward vector (barrel roll), in radians.</summary>
        public void Roll(float angle) => Up = Vector3.Transform(Up, Matrix.CreateFromAxisAngle(Forward, angle));

        /// <summary>Sets the field of view / orthographic height directly.</summary>
        public void SetZoom(float fovy) => Fovy = fovy;

        /// <summary>Adds <paramref name="delta"/> to the field of view instantly, clamped to a usable range.</summary>
        public void Zoom(float delta, float min = 1f, float max = 179f) => Fovy = Math.Clamp(Fovy + delta, min, max);

        // ---------------------------------------------------------------------
        // Projection utilities
        // ---------------------------------------------------------------------

        /// <summary>Projects a world position to screen coordinates (pixels, origin top-left).</summary>
        public Vector2 GetWorldToScreen(Vector3 position, Viewport viewport)
        {
            Vector3 projected = viewport.Project(position, GetProjectionMatrix(viewport.AspectRatio), GetViewMatrix(), Matrix.Identity);
            return new Vector2(projected.X, projected.Y);
        }

        /// <summary>Projects a world position to screen coordinates, also returning normalized depth (0 near, 1 far; outside [0,1] means outside the frustum).</summary>
        public Vector2 GetWorldToScreen(Vector3 position, Viewport viewport, out float depth)
        {
            Vector3 projected = viewport.Project(position, GetProjectionMatrix(viewport.AspectRatio), GetViewMatrix(), Matrix.Identity);
            depth = projected.Z;
            return new Vector2(projected.X, projected.Y);
        }

        /// <summary>Unprojects a screen position at the given depth back into world space.</summary>
        public Vector3 GetScreenToWorld(Vector2 screenPosition, float depth, Viewport viewport)
            => viewport.Unproject(new Vector3(screenPosition, depth), GetProjectionMatrix(viewport.AspectRatio), GetViewMatrix(), Matrix.Identity);

        /// <summary>Builds a picking ray from a screen position (e.g. the mouse cursor).</summary>
        public Ray GetScreenToWorldRay(Vector2 screenPosition, Viewport viewport)
        {
            Matrix proj = GetProjectionMatrix(viewport.AspectRatio);
            Matrix view = GetViewMatrix();
            Vector3 nearPoint = viewport.Unproject(new Vector3(screenPosition, 0f), proj, view, Matrix.Identity);
            Vector3 farPoint = viewport.Unproject(new Vector3(screenPosition, 1f), proj, view, Matrix.Identity);

            Vector3 direction = farPoint - nearPoint;
            float lenSq = direction.LengthSquared();
            if (lenSq > 1e-12f) direction *= 1f / MathF.Sqrt(lenSq);
            return new Ray(nearPoint, direction);
        }

        /// <summary>Builds the view frustum, useful for culling before submitting primitives.</summary>
        public BoundingFrustum GetFrustum(float aspectRatio) => new(GetViewProjectionMatrix(aspectRatio));

        // =====================================================================
        // Controller: mode + per-frame update
        // =====================================================================

        /// <summary><see cref="MoveSpeed"/>'s default value.</summary>
        public const float DefaultMoveSpeed = 10.4f;

        /// <summary><see cref="RotationSpeed"/>'s default value.</summary>
        public const float DefaultRotationSpeed = 0.03f;

        /// <summary><see cref="MouseMoveSensitivity"/>'s default value.</summary>
        public const float DefaultMouseMoveSensitivity = 0.003f;

        /// <summary><see cref="MouseWheelZoomSensitivity"/>'s default value.</summary>
        public const float DefaultMouseWheelZoomSensitivity = 1.5f;

        /// <summary><see cref="OrbitalSpeed"/>'s default value.</summary>
        public const float DefaultOrbitalSpeed = 0.5f;

        private const float FirstPersonEyeHeight = 1.85f;
        private const float FirstPersonStepTrigonometricDivider = 5f;

        private float _stepPhase;
        private readonly PrimitiveInput _input = new();

        /// <summary>Current behaviour mode.</summary>
        public CameraMode Mode { get; set; } = CameraMode.Free;

        /// <summary>Movement speed multiplier in world units per second.</summary>
        public float MoveSpeedScale { get; set; } = 1f;

        /// <summary>Mouse look sensitivity multiplier.</summary>
        public float LookSensitivity { get; set; } = 1f;

        /// <summary>Base keyboard movement speed in world units per second, used by <see cref="ReadDefaultInput(float)"/> (further scaled by <see cref="MoveSpeedScale"/>). Editable; defaults to <see cref="DefaultMoveSpeed"/>.</summary>
        public float MoveSpeed { get; set; } = DefaultMoveSpeed;

        /// <summary>Keyboard-driven rotation speed in radians/frame, used by <see cref="ReadDefaultInput(float)"/> (Q/E roll, arrow-key look). Editable; defaults to <see cref="DefaultRotationSpeed"/>.</summary>
        public float RotationSpeed { get; set; } = DefaultRotationSpeed;

        /// <summary>Raw mouse-delta-to-radians scale, used by <see cref="ReadDefaultInput(float)"/> (further scaled by <see cref="LookSensitivity"/>). Editable; defaults to <see cref="DefaultMouseMoveSensitivity"/>.</summary>
        public float MouseMoveSensitivity { get; set; } = DefaultMouseMoveSensitivity;

        /// <summary>Mouse-wheel-to-zoom scale, used by <see cref="ReadDefaultInput(float)"/>. Editable; defaults to <see cref="DefaultMouseWheelZoomSensitivity"/>.</summary>
        public float MouseWheelZoomSensitivity { get; set; } = DefaultMouseWheelZoomSensitivity;

        /// <summary>Angular speed (radians/second) of the automatic orbit in <see cref="CameraMode.Orbital"/>. Editable; defaults to <see cref="DefaultOrbitalSpeed"/>.</summary>
        public float OrbitalSpeed { get; set; } = DefaultOrbitalSpeed;

        /// <summary>Enables head bobbing in <see cref="CameraMode.FirstPerson"/>.</summary>
        public bool HeadBobbing { get; set; } = true;

        /// <summary>Eye height applied in first person mode, measured from the camera target's Y coordinate.</summary>
        public float EyeHeight { get; set; } = FirstPersonEyeHeight;

        /// <summary>Updates the camera using keyboard and mouse input.</summary>
        public void Update(float deltaSeconds) => Update(ReadDefaultInput(deltaSeconds), deltaSeconds);

        /// <summary>Updates the camera using keyboard and mouse input, taking the frame delta straight from a MonoGame <see cref="GameTime"/> instead of a raw float.</summary>
        public void Update(GameTime gameTime) => Update((float)gameTime.ElapsedGameTime.TotalSeconds);

        /// <summary>Updates the camera from an explicit input request, taking the frame delta straight from a MonoGame <see cref="GameTime"/> instead of a raw float.</summary>
        public void Update(GameTime gameTime, in CameraInput input) => Update(input, (float)gameTime.ElapsedGameTime.TotalSeconds);

        /// <summary>Updates the camera from an explicit input request, allowing custom bindings, gamepads or recorded input.</summary>
        public void Update(in CameraInput input, float deltaSeconds)
        {
            bool moveInWorldPlane = Mode == CameraMode.FirstPerson || Mode == CameraMode.ThirdPerson;
            bool rotateAroundTarget = Mode == CameraMode.ThirdPerson || Mode == CameraMode.Orbital;
            bool lockView = Mode != CameraMode.Free;
            bool rotateUp = false;

            switch (Mode)
            {
                case CameraMode.Custom:
                    return;

                case CameraMode.Orbital:
                    {
                        Matrix rotation = Matrix.CreateFromAxisAngle(UpNormalized, OrbitalSpeed * deltaSeconds);
                        Vector3 view = Position - Target;
                        Position = Target + Vector3.Transform(view, rotation);
                        SmoothZoom(input.Zoom, deltaSeconds);
                        ClampToBounds();
                        return;
                    }

                case CameraMode.Free:
                    rotateUp = true;
                    break;
            }

            if (input.Rotation.X != 0f) Yaw(-input.Rotation.X, rotateAroundTarget);
            if (input.Rotation.Y != 0f) Pitch(-input.Rotation.Y, lockView, rotateAroundTarget, rotateUp);
            if (input.Rotation.Z != 0f && Mode == CameraMode.Free) Roll(input.Rotation.Z);

            if (input.Movement.Z != 0f) MoveForward(input.Movement.Z, moveInWorldPlane);
            if (input.Movement.X != 0f) MoveRight(input.Movement.X, moveInWorldPlane);
            if (input.Movement.Y != 0f) MoveUp(input.Movement.Y);

            if (Mode == CameraMode.FirstPerson)
            {
                if (HeadBobbing)
                {
                    float horizontalMovement = MathF.Abs(input.Movement.X) + MathF.Abs(input.Movement.Z);
                    if (horizontalMovement > 0f)
                    {
                        _stepPhase += horizontalMovement * FirstPersonStepTrigonometricDivider;
                        float bob = MathF.Sin(_stepPhase) * 0.03f;
                        Position = new Vector3(Position.X, Position.Y + bob, Position.Z);
                        Target = new Vector3(Target.X, Target.Y + bob, Target.Z);
                    }
                }
            }
            else if (Mode is CameraMode.ThirdPerson or CameraMode.Free)
            {
                SmoothZoom(input.Zoom, deltaSeconds);
            }

            ClampToBounds();
        }

        /// <summary>Builds a <see cref="CameraInput"/> from the current keyboard and mouse state, taking the frame delta straight from a MonoGame <see cref="GameTime"/> instead of a raw float.</summary>
        public CameraInput ReadDefaultInput(GameTime gameTime) => ReadDefaultInput((float)gameTime.ElapsedGameTime.TotalSeconds);

        /// <summary>Builds a <see cref="CameraInput"/> from the current keyboard and mouse state (W/A/S/D, Q/E roll, right-mouse-drag look, wheel zoom) via the shared <see cref="PrimitiveInput"/>.</summary>
        public CameraInput ReadDefaultInput(float deltaSeconds)
        {
            _input.Update(deltaSeconds);

            float speed = MoveSpeed * MoveSpeedScale * deltaSeconds;
            float sensitivity = MouseMoveSensitivity * LookSensitivity;

            CameraInput input = default;

            // normalize: false to keep this byte-for-byte the same speed as the four separate
            // IsKeyDown checks it replaces (each axis independently adds speed — a diagonal like
            // W+D moves at sqrt(2)*speed, same as before; GetVector2's own default normalization
            // is deliberately opted out of here, not forgotten).
            Vector2 moveXZ = _input.GetVector2(Keys.A, Keys.D, Keys.S, Keys.W, normalize: false) * speed;
            input.Movement.X = moveXZ.X;
            input.Movement.Z = moveXZ.Y;
            if (_input.IsKeyDown(Keys.Space)) input.Movement.Y += speed;
            if (_input.IsKeyDown(Keys.LeftControl)) input.Movement.Y -= speed;

            if (_input.IsKeyDown(Keys.Q)) input.Rotation.Z -= RotationSpeed;
            if (_input.IsKeyDown(Keys.E)) input.Rotation.Z += RotationSpeed;

            // Mouse look only while the right button is held, so the mouse can still
            // drive UI/other systems the rest of the time.
            if (_input.IsMouseButtonDown(MouseButton.Right))
            {
                Vector2 mouseDelta = _input.MouseDelta;
                input.Rotation.X += mouseDelta.X * sensitivity;
                input.Rotation.Y += mouseDelta.Y * sensitivity;
            }

            if (_input.IsKeyDown(Keys.Up)) input.Rotation.Y += RotationSpeed;
            if (_input.IsKeyDown(Keys.Down)) input.Rotation.Y -= RotationSpeed;
            if (_input.IsKeyDown(Keys.Right)) input.Rotation.X += RotationSpeed;
            if (_input.IsKeyDown(Keys.Left)) input.Rotation.X -= RotationSpeed;

            input.Zoom = -_input.MouseScrollDelta * (MouseWheelZoomSensitivity / 120f);
            return input;
        }

        /// <summary>Resets mouse-delta tracking. Call after teleporting the cursor or regaining window focus to avoid a one-frame snap.</summary>
        public void ResetMouseTracking() => _input.ResetMouseDelta();

        /// <summary>Resets the head-bobbing phase, useful when respawning the player.</summary>
        public void ResetHeadBobbing() => _stepPhase = 0f;

        // =====================================================================
        // Robust extras: bounds/padding/limits, smooth follow, smooth zoom, easing
        // =====================================================================

        /// <summary>
        /// Optional world-space bounds the camera <see cref="Position"/> is kept
        /// inside (checked after every <see cref="Update(float)"/>/<see cref="Update(in CameraInput,float)"/>
        /// call, and after <see cref="FollowTarget"/>). <c>null</c> (default) disables clamping.
        /// </summary>
        public BoundingBox? PositionBounds { get; set; }

        /// <summary>
        /// Inward margin subtracted from <see cref="PositionBounds"/> before clamping, so the
        /// camera stops short of the hard edge instead of hugging it exactly.
        /// </summary>
        public float BoundsPadding { get; set; } = 0f;

        /// <summary>Minimum allowed <see cref="TargetDistance"/> for <see cref="SmoothZoom"/> and the Orbital/ThirdPerson/Free auto-zoom in <see cref="Update(in CameraInput,float)"/>.</summary>
        public float MinDistance { get; set; } = 0.5f;

        /// <summary>Maximum allowed <see cref="TargetDistance"/> for <see cref="SmoothZoom"/>.</summary>
        public float MaxDistance { get; set; } = 200f;

        /// <summary>Time (seconds) for <see cref="SmoothZoom"/> to close ~95% of the distance to its target — 0 disables smoothing (instant, like <see cref="MoveToTarget"/>).</summary>
        public float ZoomSmoothTime { get; set; } = 0.12f;

        private float _zoomVelocity;
        private float _pendingZoomTarget = float.NaN; // NaN = not accumulating a smoothed zoom yet

        /// <summary>
        /// Zooms toward/away from <see cref="Target"/> by <paramref name="delta"/>, eased over
        /// <see cref="ZoomSmoothTime"/> seconds instead of snapping like <see cref="MoveToTarget"/>,
        /// and clamped to [<see cref="MinDistance"/>, <see cref="MaxDistance"/>]. For discrete
        /// input (a mouse wheel tick) where <paramref name="delta"/> is naturally 0 most
        /// frames: call every frame, most calls no-op. For continuous input (a key held to
        /// zoom), don't call this every frame with a small nonzero <paramref name="delta"/> —
        /// each call adds onto the target immediately, so a delta repeated every frame races
        /// the target ahead rather than climbing smoothly. Adjust the distance directly by
        /// <c>rate * deltaSeconds</c> via <see cref="MoveToTarget"/> for that case instead.
        /// </summary>
        public void SmoothZoom(float delta, float deltaSeconds)
        {
            if (delta == 0f && float.IsNaN(_pendingZoomTarget))
                return; // nothing requested and no in-flight smoothing to advance

            float current = TargetDistance;
            float target = float.IsNaN(_pendingZoomTarget) ? current : _pendingZoomTarget;
            target = Math.Clamp(target + delta, MinDistance, MaxDistance);
            _pendingZoomTarget = target;

            float newDistance = ZoomSmoothTime <= 0f
                ? target
                : SmoothDamp(current, target, ref _zoomVelocity, ZoomSmoothTime, deltaSeconds);

            Position = Target - Forward * MathF.Max(newDistance, 0.001f);

            if (MathF.Abs(newDistance - target) < 0.0005f)
                _pendingZoomTarget = float.NaN; // settled; let the next call start fresh
        }

        private Vector3 _followVelocity;
        private Vector3 _followTargetVelocity;

        /// <summary>
        /// Smoothly moves <see cref="Position"/> (and <see cref="Target"/> by the same delta,
        /// preserving the current look offset) toward <paramref name="desiredPosition"/> —
        /// "follow with delay": the camera eases toward the subject instead of snapping to it
        /// every frame. Within <see cref="FollowPadding"/> world units of the goal, the camera
        /// doesn't move at all (a deadzone), which reads as "the subject can wander a little
        /// before the camera reacts" rather than a constant low-amplitude jitter.
        /// </summary>
        /// <param name="desiredPosition">Where the camera should end up.</param>
        /// <param name="deltaSeconds">Frame time in seconds.</param>
        /// <param name="desiredTarget">
        /// Where <see cref="Target"/> should end up; if null, <see cref="Target"/> is moved by
        /// the same delta as <see cref="Position"/> so the camera keeps looking the same
        /// relative direction while it follows.
        /// </param>
        public void FollowTarget(Vector3 desiredPosition, float deltaSeconds, Vector3? desiredTarget = null)
        {
            Vector3 toDesired = desiredPosition - Position;
            if (toDesired.Length() <= FollowPadding)
            {
                ClampToBounds();
                return; // inside the deadzone: hold still
            }

            Vector3 newPosition = SmoothDamp(Position, desiredPosition, ref _followVelocity, FollowSmoothTime, deltaSeconds);
            Vector3 delta = newPosition - Position;
            Position = newPosition;
            Target = desiredTarget.HasValue
                ? SmoothDamp(Target, desiredTarget.Value, ref _followTargetVelocity, FollowSmoothTime, deltaSeconds)
                : Target + delta;

            ClampToBounds();
        }

        /// <summary>Time (seconds) for <see cref="FollowTarget"/> to close ~95% of the remaining distance.</summary>
        public float FollowSmoothTime { get; set; } = 0.2f;

        /// <summary>Deadzone radius (world units): <see cref="FollowTarget"/> doesn't move the camera until the desired position is at least this far away.</summary>
        public float FollowPadding { get; set; } = 0f;

        /// <summary>Resets <see cref="FollowTarget"/>'s internal smoothing velocity — call after teleporting the camera or its subject to avoid a lingering swoop.</summary>
        public void ResetFollowVelocity() { _followVelocity = Vector3.Zero; _followTargetVelocity = Vector3.Zero; }

        private void ClampToBounds()
        {
            if (!PositionBounds.HasValue)
                return;

            BoundingBox b = PositionBounds.Value;
            Vector3 min = b.Min + new Vector3(BoundsPadding);
            Vector3 max = b.Max - new Vector3(BoundsPadding);
            // If padding exceeds the box's own size on an axis, collapse to its center
            // rather than producing an inverted (min > max) clamp range.
            if (min.X > max.X) { float c = (b.Min.X + b.Max.X) * 0.5f; min.X = max.X = c; }
            if (min.Y > max.Y) { float c = (b.Min.Y + b.Max.Y) * 0.5f; min.Y = max.Y = c; }
            if (min.Z > max.Z) { float c = (b.Min.Z + b.Max.Z) * 0.5f; min.Z = max.Z = c; }

            Vector3 clamped = Vector3.Clamp(Position, min, max);
            if (clamped == Position)
                return;

            Vector3 offset = clamped - Position;
            Position = clamped;
            Target += offset; // keep looking the same relative direction, don't reorient on clamp
        }

        // =====================================================================
        // Easing (generic — usable for the camera's own smoothing above, or for
        // any other value you want to ease, e.g. a FOV transition)
        // =====================================================================

        /// <summary>
        /// Critically-damped spring smoothing (the same algorithm as Unity's
        /// <c>Mathf.SmoothDamp</c>) — eases <paramref name="current"/> toward
        /// <paramref name="target"/> over roughly <paramref name="smoothTime"/> seconds,
        /// without the overshoot/oscillation a naive exponential lerp can show under
        /// varying frame rates. <paramref name="velocity"/> is state you own and pass back
        /// in every call (start it at 0).
        /// </summary>
        public static float SmoothDamp(float current, float target, ref float velocity, float smoothTime, float deltaTime)
        {
            smoothTime = MathF.Max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega * deltaTime;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target;
            float temp = (velocity + omega * change) * deltaTime;
            velocity = (velocity - omega * temp) * exp;
            return target + (change + temp) * exp;
        }

        /// <summary>Component-wise <see cref="SmoothDamp(float,float,ref float,float,float)"/> for a <see cref="Vector3"/>.</summary>
        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 velocity, float smoothTime, float deltaTime)
        {
            float vx = velocity.X, vy = velocity.Y, vz = velocity.Z;
            Vector3 result = new(
                SmoothDamp(current.X, target.X, ref vx, smoothTime, deltaTime),
                SmoothDamp(current.Y, target.Y, ref vy, smoothTime, deltaTime),
                SmoothDamp(current.Z, target.Z, ref vz, smoothTime, deltaTime));
            velocity = new Vector3(vx, vy, vz);
            return result;
        }

        // ---------------------------------------------------------------------
        // Internals
        // ---------------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 Normalize(in Vector3 v, in Vector3 fallback)
        {
            float lenSq = v.LengthSquared();
            return lenSq < 1e-12f ? fallback : v * (1f / MathF.Sqrt(lenSq));
        }

        private static float AngleBetween(in Vector3 a, in Vector3 b)
        {
            float lenProduct = MathF.Sqrt(a.LengthSquared() * b.LengthSquared());
            if (lenProduct < 1e-12f) return 0f;
            float cos = Math.Clamp(Vector3.Dot(a, b) / lenProduct, -1f, 1f);
            return MathF.Acos(cos);
        }
    }
}
