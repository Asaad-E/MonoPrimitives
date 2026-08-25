#nullable enable

using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace MonoPrimitives.Primitives3D
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
        /// <summary>No automatic update; the caller drives the camera (or calls <see cref="Camera3D.FollowTarget(Vector3,float,Vector3?)"/>/<see cref="Camera3D.SmoothZoom"/> directly).</summary>
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
    /// A 3D camera with its own update/input logic folded into one class — a single object
    /// that owns both its state and its behaviour.
    /// </summary>
    /// <remarks>
    /// Reference type (not a struct) so it can hold its own smoothing state (follow velocity,
    /// zoom velocity, mouse tracking) without every caller needing to pass it by <c>ref</c>.
    /// </remarks>
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

        /// <summary><see cref="NearPlane"/>'s default value.</summary>
        public const float DefaultNear = 0.1f;

        /// <summary><see cref="FarPlane"/>'s default value.</summary>
        public const float DefaultFar = 1000f;

        /// <summary>
        /// Viewport adapter this camera was constructed with, if any — set once, then
        /// <see cref="Primitive3DBatch.Begin(Camera3D,BlendState,DepthStencilState,RasterizerState,Matrix?)"/>
        /// and <see cref="WorldToScreen(Vector3,Viewport?)"/>/<see cref="ScreenToWorld"/>/
        /// <see cref="GetScreenToWorldRay"/> all use it automatically instead of requiring it
        /// passed in again at every call site.
        /// </summary>
        /// <remarks>
        /// Same shape of dependency <see cref="Primitives2D.Camera2D"/> takes.
        /// <c>null</c> for the plain constructor — those methods then need an explicit
        /// <see cref="Viewport"/> argument, or fall back to the full device viewport in
        /// <c>Begin</c>'s case.
        /// </remarks>
        public ViewportAdapter2D? ViewportAdapter { get; }

        // Captured at construction so Reset() has something to restore to.
        private readonly Vector3 _initialPosition;
        private readonly Vector3 _initialTarget;
        private readonly Vector3 _initialUp;
        private readonly float _initialFovy;
        private readonly CameraProjection _initialProjection;
        private readonly float _initialNearPlane;
        private readonly float _initialFarPlane;

        /// <summary>Creates a camera with no <see cref="ViewportAdapter"/> — <see cref="ScreenToWorld"/>/<see cref="WorldToScreen(Vector3,Viewport?)"/>/<see cref="GetScreenToWorldRay"/> then need an explicit <see cref="Viewport"/> argument.</summary>
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

            _initialPosition = position;
            _initialTarget = target;
            _initialUp = up;
            _initialFovy = fovy;
            _initialProjection = projection;
            _initialNearPlane = nearPlane;
            _initialFarPlane = farPlane;
        }

        /// <summary>Same as the plain constructor, but also stores <paramref name="viewportAdapter"/> on <see cref="ViewportAdapter"/> — see its doc comment.</summary>
        public Camera3D(ViewportAdapter2D viewportAdapter, Vector3 position, Vector3 target, Vector3 up, float fovy = 45f,
                        CameraProjection projection = CameraProjection.Perspective,
                        float nearPlane = DefaultNear, float farPlane = DefaultFar)
            : this(position, target, up, fovy, projection, nearPlane, farPlane)
        {
            ViewportAdapter = viewportAdapter ?? throw new ArgumentNullException(nameof(viewportAdapter));
        }

        /// <summary>Creates a camera with sensible defaults looking at the origin.</summary>
        public static Camera3D CreateDefault() => new(new Vector3(10f, 10f, 10f), Vector3.Zero, Vector3.Up, 45f);

        /// <summary>
        /// Restores <see cref="Position"/>/<see cref="Target"/>/<see cref="Up"/>/<see cref="Fovy"/>/
        /// <see cref="Projection"/>/<see cref="NearPlane"/>/<see cref="FarPlane"/> to the values
        /// passed at construction, and clears smooth-zoom/smooth-follow/head-bobbing/screen-shake
        /// state so there's no lingering velocity (or trauma) to swoop through afterward. Bound to
        /// <c>R</c> by default in <see cref="UpdateWithInput(PrimitiveInput, float)"/>; call
        /// directly if you're not using that.
        /// </summary>
        /// <remarks>Deliberately leaves <see cref="Mode"/> alone — that's a control-scheme choice, not part of the camera's pose.</remarks>
        public void Reset()
        {
            Position = _initialPosition;
            Target = _initialTarget;
            Up = _initialUp;
            Fovy = _initialFovy;
            Projection = _initialProjection;
            NearPlane = _initialNearPlane;
            FarPlane = _initialFarPlane;

            _pendingZoomTarget = float.NaN;
            _zoomVelocity = 0f;
            ResetFollowVelocity();
            ResetHeadBobbing();
            ResetTrauma();
        }

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

        /// <summary>Builds the view matrix, including any screen-shake offset/roll (see <see cref="AddTrauma"/>) on top of <see cref="Position"/>/<see cref="Target"/>/<see cref="Up"/> — those themselves are never modified by shake.</summary>
        public Matrix GetViewMatrix()
        {
            (Vector3 shakeOffset, float shakeRoll) = GetShakeOffset();
            if (shakeOffset == Vector3.Zero && shakeRoll == 0f)
                return Matrix.CreateLookAt(Position, Target, UpNormalized);

            Vector3 up = shakeRoll == 0f ? UpNormalized : Vector3.Transform(UpNormalized, Matrix.CreateFromAxisAngle(Forward, shakeRoll));
            return Matrix.CreateLookAt(Position + shakeOffset, Target + shakeOffset, up);
        }

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

        /// <summary>
        /// Resolves the <see cref="Viewport"/> to project/unproject with: the explicit
        /// <paramref name="viewport"/> if given, otherwise <see cref="ViewportAdapter"/>'s
        /// <see cref="ViewportAdapter2D.BoundingRectangle"/>. Throws if neither is available.
        /// </summary>
        private Viewport ResolveViewport(Viewport? viewport)
            => viewport
            ?? (ViewportAdapter is not null ? new Viewport(ViewportAdapter.BoundingRectangle) : (Viewport?)null)
            ?? throw new InvalidOperationException($"Needs either a {nameof(ViewportAdapter)} (set at construction) or an explicit {nameof(Viewport)} argument.");

        /// <summary>Projects a world position to screen coordinates (pixels, origin top-left).</summary>
        /// <param name="position">World-space position to project.</param>
        /// <param name="viewport">Explicit viewport to project with; omit to use <see cref="ViewportAdapter"/> instead (throws if neither is available).</param>
        public Vector2 WorldToScreen(Vector3 position, Viewport? viewport = null)
        {
            Viewport vp = ResolveViewport(viewport);
            Vector3 projected = vp.Project(position, GetProjectionMatrix(vp.AspectRatio), GetViewMatrix(), Matrix.Identity);
            return new Vector2(projected.X, projected.Y);
        }

        /// <summary>Projects a world position to screen coordinates, also returning normalized depth (0 near, 1 far; outside [0,1] means outside the frustum).</summary>
        /// <param name="position">World-space position to project.</param>
        /// <param name="depth">Normalized depth of the projected position (0 near, 1 far).</param>
        /// <param name="viewport">Explicit viewport to project with; omit to use <see cref="ViewportAdapter"/> instead (throws if neither is available).</param>
        public Vector2 WorldToScreen(Vector3 position, out float depth, Viewport? viewport = null)
        {
            Viewport vp = ResolveViewport(viewport);
            Vector3 projected = vp.Project(position, GetProjectionMatrix(vp.AspectRatio), GetViewMatrix(), Matrix.Identity);
            depth = projected.Z;
            return new Vector2(projected.X, projected.Y);
        }

        /// <summary>Unprojects a screen position at the given depth back into world space.</summary>
        /// <param name="screenPosition">Screen-space position (pixels, origin top-left).</param>
        /// <param name="depth">Normalized depth to unproject at (0 near, 1 far).</param>
        /// <param name="viewport">Explicit viewport to unproject with; omit to use <see cref="ViewportAdapter"/> instead (throws if neither is available).</param>
        public Vector3 ScreenToWorld(Vector2 screenPosition, float depth, Viewport? viewport = null)
        {
            Viewport vp = ResolveViewport(viewport);
            return vp.Unproject(new Vector3(screenPosition, depth), GetProjectionMatrix(vp.AspectRatio), GetViewMatrix(), Matrix.Identity);
        }

        /// <summary>Builds a picking ray from a screen position (e.g. the mouse cursor).</summary>
        /// <param name="screenPosition">Screen-space position (pixels, origin top-left).</param>
        /// <param name="viewport">Explicit viewport to unproject with; omit to use <see cref="ViewportAdapter"/> instead (throws if neither is available).</param>
        public Ray GetScreenToWorldRay(Vector2 screenPosition, Viewport? viewport = null)
        {
            Viewport vp = ResolveViewport(viewport);
            Matrix proj = GetProjectionMatrix(vp.AspectRatio);
            Matrix view = GetViewMatrix();
            Vector3 nearPoint = vp.Unproject(new Vector3(screenPosition, 0f), proj, view, Matrix.Identity);
            Vector3 farPoint = vp.Unproject(new Vector3(screenPosition, 1f), proj, view, Matrix.Identity);

            Vector3 direction = farPoint - nearPoint;
            float lenSq = direction.LengthSquared();
            if (lenSq > 1e-12f) direction *= 1f / MathF.Sqrt(lenSq);
            return new Ray(nearPoint, direction);
        }

        /// <summary>Builds the view frustum, useful for culling before submitting primitives.</summary>
        public BoundingFrustum GetFrustum(float aspectRatio) => new(GetViewProjectionMatrix(aspectRatio));

        /// <summary>True if <paramref name="sphere"/> is at least partially inside the view frustum — a cheap "should I bother drawing this" check before drawing many objects.</summary>
        /// <param name="sphere">World-space bounding sphere to test.</param>
        /// <param name="viewport">Explicit viewport to derive the aspect ratio from; omit to use <see cref="ViewportAdapter"/> instead (throws if neither is available).</param>
        public bool IsVisible(BoundingSphere sphere, Viewport? viewport = null) => GetFrustum(ResolveViewport(viewport).AspectRatio).Intersects(sphere);

        /// <summary>True if <paramref name="box"/> is at least partially inside the view frustum.</summary>
        /// <param name="box">World-space bounding box to test.</param>
        /// <param name="viewport">Explicit viewport to derive the aspect ratio from; omit to use <see cref="ViewportAdapter"/> instead (throws if neither is available).</param>
        public bool IsVisible(BoundingBox box, Viewport? viewport = null) => GetFrustum(ResolveViewport(viewport).AspectRatio).Intersects(box);

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

        /// <summary>Current behaviour mode.</summary>
        public CameraMode Mode { get; set; } = CameraMode.Free;

        /// <summary>Unitless multiplier applied on top of <see cref="MoveSpeed"/> (which is the one in world units per second).</summary>
        public float MoveSpeedScale { get; set; } = 1f;

        /// <summary>Mouse look sensitivity multiplier.</summary>
        public float LookSensitivity { get; set; } = 1f;

        /// <summary>Base keyboard movement speed in world units per second, used by <see cref="UpdateWithInput(PrimitiveInput, float)"/> (further scaled by <see cref="MoveSpeedScale"/>). Editable; defaults to <see cref="DefaultMoveSpeed"/>.</summary>
        public float MoveSpeed { get; set; } = DefaultMoveSpeed;

        /// <summary>Keyboard-driven rotation speed in radians/frame, used by <see cref="UpdateWithInput(PrimitiveInput, float)"/> (Q/E yaw, Z/X roll, arrow-key look). Editable; defaults to <see cref="DefaultRotationSpeed"/>.</summary>
        public float RotationSpeed { get; set; } = DefaultRotationSpeed;

        /// <summary>Raw mouse-delta-to-radians scale, used by <see cref="UpdateWithInput(PrimitiveInput, float)"/> (further scaled by <see cref="LookSensitivity"/>). Editable; defaults to <see cref="DefaultMouseMoveSensitivity"/>.</summary>
        public float MouseMoveSensitivity { get; set; } = DefaultMouseMoveSensitivity;

        /// <summary>Mouse-wheel-to-zoom scale, used by <see cref="UpdateWithInput(PrimitiveInput, float)"/>. Editable; defaults to <see cref="DefaultMouseWheelZoomSensitivity"/>.</summary>
        public float MouseWheelZoomSensitivity { get; set; } = DefaultMouseWheelZoomSensitivity;

        /// <summary>Angular speed (radians/second) of the automatic orbit in <see cref="CameraMode.Orbital"/>. Editable; defaults to <see cref="DefaultOrbitalSpeed"/>.</summary>
        public float OrbitalSpeed { get; set; } = DefaultOrbitalSpeed;

        /// <summary>Enables head bobbing in <see cref="CameraMode.FirstPerson"/>.</summary>
        public bool HeadBobbing { get; set; } = true;

        /// <summary>
        /// Suggested eye height for <see cref="CameraMode.FirstPerson"/>, in world units above
        /// ground level — a reference default, not applied automatically.
        /// </summary>
        /// <remarks>
        /// Only the game knows ground/terrain height, so <see cref="UpdateWithInput(PrimitiveInput,float)"/>'s
        /// head-bob only ever nudges the existing <c>Position.Y</c>/<c>Target.Y</c> by a small
        /// delta; it's on the caller to set <c>Position.Y</c> (and <c>Target.Y</c>) to
        /// <c>groundY + EyeHeight</c> themselves each frame the way a platformer already tracks
        /// its own ground contact — an auto-applying version would need to track ground height
        /// itself, which would make this a physics/collision system rather than a camera.
        /// </remarks>
        public float EyeHeight { get; set; } = FirstPersonEyeHeight;

        /// <summary>
        /// Advances internal state only — screen-shake trauma decay (see <see cref="AddTrauma"/>)
        /// and any in-flight <see cref="SmoothZoom"/> easing — with no movement and no input
        /// reading.
        /// </summary>
        /// <remarks>
        /// Call this every frame when you're driving <see cref="Position"/>/<see cref="Target"/>
        /// yourself (a fixed prototype camera, a cutscene) and just want shake/easing to keep
        /// working. For a built-in WASD/mouse-look controller driven by your own
        /// <see cref="PrimitiveInput"/>, use <see cref="UpdateWithInput(PrimitiveInput, float)"/>
        /// instead.
        /// </remarks>
        public void Update(float deltaSeconds) => UpdateShake(deltaSeconds);

        /// <inheritdoc cref="Update(float)"/>
        public void Update(GameTime gameTime) => Update((float)gameTime.ElapsedGameTime.TotalSeconds);

        /// <summary>
        /// Advances the camera one frame using W/A/S/D + Space/Ctrl movement, Q/E yaw, Z/X roll,
        /// right-mouse-drag look and wheel zoom read from <paramref name="input"/> — the simplest
        /// way to get a working camera with no input code of your own. <c>R</c> calls
        /// <see cref="Reset"/> directly and skips movement for that frame, so a same-frame
        /// WASD/mouse delta doesn't immediately fight the reset.
        /// </summary>
        /// <remarks>
        /// This is a prototyping convenience, not something every game wants baked into its
        /// camera; use the plain <see cref="Update(float)"/> if you're driving the camera from
        /// your own logic, or query <paramref name="input"/> yourself and call <see cref="Yaw"/>/
        /// <see cref="Pitch"/>/<see cref="MoveForward"/>/etc. directly for custom bindings.
        /// Doesn't call <see cref="PrimitiveInput.Update(GameTime)"/> itself — <paramref name="input"/>
        /// is expected to already be current for this frame (the caller's own <c>Update</c>
        /// updates it once, then hands the same instance to everything that reads it, this
        /// camera included).
        /// </remarks>
        public void UpdateWithInput(PrimitiveInput input, float deltaSeconds)
        {
            UpdateShake(deltaSeconds); // before the Mode switch below, since Custom/Orbital return early

            if (input.IsKeyPressed(Keys.R))
            {
                Reset();
                return;
            }

            bool moveInWorldPlane = Mode == CameraMode.FirstPerson || Mode == CameraMode.ThirdPerson;
            bool rotateAroundTarget = Mode == CameraMode.ThirdPerson || Mode == CameraMode.Orbital;
            bool lockView = Mode != CameraMode.Free;
            bool rotateUp = false;

            float speed = MoveSpeed * MoveSpeedScale * deltaSeconds;
            float sensitivity = MouseMoveSensitivity * LookSensitivity;

            // normalize: false to keep each axis independently adding speed — a diagonal like
            // W+D moves at sqrt(2)*speed rather than being capped to 1x.
            Vector2 moveXZ = input.GetVector2(Keys.A, Keys.D, Keys.S, Keys.W, normalize: false) * speed;
            float moveX = moveXZ.X, moveZ = moveXZ.Y, moveY = 0f;
            if (input.IsKeyDown(Keys.Space)) moveY += speed;
            if (input.IsKeyDown(Keys.LeftControl)) moveY -= speed;

            float yaw = 0f, pitch = 0f, roll = 0f;

            // Q/E turn the camera's body left/right (yaw) — the old roll binding moved to Z/X.
            if (input.IsKeyDown(Keys.Q)) yaw -= RotationSpeed;
            if (input.IsKeyDown(Keys.E)) yaw += RotationSpeed;
            if (input.IsKeyDown(Keys.Z)) roll -= RotationSpeed;
            if (input.IsKeyDown(Keys.X)) roll += RotationSpeed;

            // Mouse look only while the right button is held, so the mouse can still drive
            // UI/other systems the rest of the time. Subtracted (not added): a "grab and drag"
            // feel — dragging right brings what was on the right into view, the same convention
            // Camera2D's own left-drag pan already uses — rather than an FPS-style "steer the
            // direction you push the mouse" feel.
            if (input.IsMouseButtonDown(MouseButton.Right))
            {
                Vector2 mouseDelta = input.MouseDelta;
                yaw -= mouseDelta.X * sensitivity;
                pitch -= mouseDelta.Y * sensitivity;
            }

            if (input.IsKeyDown(Keys.Up)) pitch += RotationSpeed;
            if (input.IsKeyDown(Keys.Down)) pitch -= RotationSpeed;
            if (input.IsKeyDown(Keys.Right)) yaw += RotationSpeed;
            if (input.IsKeyDown(Keys.Left)) yaw -= RotationSpeed;

            float zoom = -input.MouseScrollDelta * (MouseWheelZoomSensitivity / 120f);

            switch (Mode)
            {
                case CameraMode.Custom:
                    return;

                case CameraMode.Orbital:
                    {
                        Matrix rotation = Matrix.CreateFromAxisAngle(UpNormalized, OrbitalSpeed * deltaSeconds);
                        Vector3 view = Position - Target;
                        Position = Target + Vector3.Transform(view, rotation);
                        SmoothZoom(zoom, deltaSeconds);
                        ClampToBounds();
                        return;
                    }

                case CameraMode.Free:
                    rotateUp = true;
                    break;
            }

            if (yaw != 0f) Yaw(-yaw, rotateAroundTarget);
            if (pitch != 0f) Pitch(-pitch, lockView, rotateAroundTarget, rotateUp);
            if (roll != 0f && Mode == CameraMode.Free) Roll(roll);

            if (moveZ != 0f) MoveForward(moveZ, moveInWorldPlane);
            if (moveX != 0f) MoveRight(moveX, moveInWorldPlane);
            if (moveY != 0f) MoveUp(moveY);

            if (Mode == CameraMode.FirstPerson)
            {
                if (HeadBobbing)
                {
                    float horizontalMovement = MathF.Abs(moveX) + MathF.Abs(moveZ);
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
                SmoothZoom(zoom, deltaSeconds);
            }

            ClampToBounds();
        }

        /// <inheritdoc cref="UpdateWithInput(PrimitiveInput, float)"/>
        public void UpdateWithInput(PrimitiveInput input, GameTime gameTime) => UpdateWithInput(input, (float)gameTime.ElapsedGameTime.TotalSeconds);

        /// <summary>Resets the head-bobbing phase, useful when respawning the player.</summary>
        public void ResetHeadBobbing() => _stepPhase = 0f;

        // =====================================================================
        // Screen shake (trauma-based) — same technique and API shape as Primitives2D.Camera2D's
        // own (see its doc comment for the full rationale): a "trauma" value in [0,1] that jumps
        // up on impact and decays over time, shake magnitude scaled by trauma^2, sampled from
        // Perlin noise (one channel per axis plus one for roll) instead of raw random so it
        // reads as a smooth camera bump rather than jitter. Purely a rendering effect — baked
        // into GetViewMatrix(), never touches Position/Target/Up themselves, so nothing that
        // reads camera state sees the shake. The offset is applied along the camera's OWN
        // right/up axes (not world axes), so it reads as "camera shake" regardless of which way
        // the camera happens to be facing.
        // =====================================================================

        private readonly Noise _shakeNoiseX = new(unchecked((int)0x53484B58)); // "SHKX"
        private readonly Noise _shakeNoiseY = new(unchecked((int)0x53484B59)); // "SHKY"
        private readonly Noise _shakeNoiseRoll = new(unchecked((int)0x53484B52)); // "SHKR"
        // Starts away from 0 on purpose: Perlin noise is exactly 0 at every integer lattice
        // point by construction, and 0 itself is one — starting there would make the very
        // first shake read (AddTrauma then GetShakeOffset/GetViewMatrix before any Update
        // tick has advanced this) silently zero.
        private float _shakeTime = 0.5f;

        /// <summary>Current shake intensity, [0,1] — 0 is calm. Decays toward 0 at <see cref="TraumaDecayPerSecond"/> every time any <c>Update</c> overload runs. Set directly, or use <see cref="AddTrauma"/> to bump it without risking pushing it out of range.</summary>
        public float Trauma { get; set; }

        /// <summary>How fast <see cref="Trauma"/> decays back to 0, in units of trauma per second.</summary>
        public float TraumaDecayPerSecond { get; set; } = 1.5f;

        /// <summary>Shake displacement (world units) at maximum trauma (1.0), along the camera's own right/up axes — actual displacement scales with trauma².</summary>
        public float MaxShakeOffset { get; set; } = 0.6f;

        /// <summary>Shake roll (radians, around the camera's own forward axis) at maximum trauma (1.0) — actual roll scales with trauma².</summary>
        public float MaxShakeRoll { get; set; } = 0.08f;

        /// <summary>How fast the underlying Perlin noise is sampled while shaking is active — higher reads as a faster, more frantic shake; lower as a slower sway.</summary>
        public float ShakeNoiseSpeed { get; set; } = 20f;

        /// <summary>Bumps <see cref="Trauma"/> up by <paramref name="amount"/>, clamped to [0,1] — call this on a hit/impact/explosion instead of setting <see cref="Trauma"/> directly, so stacking several hits in one frame can't overshoot.</summary>
        public void AddTrauma(float amount) => Trauma = Math.Clamp(Trauma + MathF.Max(0f, amount), 0f, 1f);

        /// <summary>Stops any shake immediately.</summary>
        public void ResetTrauma() => Trauma = 0f;

        private void UpdateShake(float deltaSeconds)
        {
            if (Trauma <= 0f) return;
            Trauma = MathF.Max(0f, Trauma - TraumaDecayPerSecond * deltaSeconds);
            _shakeTime += deltaSeconds * ShakeNoiseSpeed;
        }

        /// <summary>The current frame's shake offset (along the camera's own right/up axes) and roll (around its own forward axis) — already folded into <see cref="GetViewMatrix"/>; exposed separately in case you want to apply it to something else.</summary>
        public (Vector3 Offset, float Roll) GetShakeOffset()
        {
            if (Trauma <= 0f) return (Vector3.Zero, 0f);
            float shake = Trauma * Trauma;
            float ox = _shakeNoiseX.Sample1D(_shakeTime) * shake * MaxShakeOffset;
            float oy = _shakeNoiseY.Sample1D(_shakeTime) * shake * MaxShakeOffset;
            float roll = _shakeNoiseRoll.Sample1D(_shakeTime) * shake * MaxShakeRoll;
            return (Right * ox + UpNormalized * oy, roll);
        }

        // =====================================================================
        // Robust extras: bounds/padding/limits, smooth follow, smooth zoom, easing
        // =====================================================================

        /// <summary>
        /// Optional world-space bounds the camera <see cref="Position"/> is kept
        /// inside (checked after every <see cref="Update(float)"/>/<see cref="UpdateWithInput(PrimitiveInput,float)"/>
        /// call, and after <see cref="FollowTarget(Vector3,float,Vector3?)"/>). <c>null</c> (default) disables clamping.
        /// </summary>
        public BoundingBox? PositionBounds { get; set; }

        /// <summary>
        /// Inward margin subtracted from <see cref="PositionBounds"/> before clamping, so the
        /// camera stops short of the hard edge instead of hugging it exactly.
        /// </summary>
        public float BoundsPadding { get; set; } = 0f;

        /// <summary>Minimum allowed <see cref="TargetDistance"/> for <see cref="SmoothZoom"/> and the Orbital/ThirdPerson/Free auto-zoom in <see cref="UpdateWithInput(PrimitiveInput,float)"/>.</summary>
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
        /// and clamped to [<see cref="MinDistance"/>, <see cref="MaxDistance"/>].
        /// </summary>
        /// <remarks>
        /// For discrete input (a mouse wheel tick) where <paramref name="delta"/> is naturally 0
        /// most frames: call every frame, most calls no-op. For continuous input (a key held to
        /// zoom), don't call this every frame with a small nonzero <paramref name="delta"/> —
        /// each call adds onto the target immediately, so a delta repeated every frame races
        /// the target ahead rather than climbing smoothly. Adjust the distance directly by
        /// <c>rate * deltaSeconds</c> via <see cref="MoveToTarget"/> for that case instead.
        /// </remarks>
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

            // Relative to target, capped at the plain 0.0005f used for a normal-scale distance --
            // see Camera2D.SmoothZoom's identical fix for the measured failure mode (a small
            // MinDistance/MaxDistance range stalling short of target once a fixed absolute epsilon
            // declares "settled" too early).
            float epsilon = MathF.Min(0.0005f, MathF.Abs(target) * 0.001f);
            if (MathF.Abs(newDistance - target) < epsilon)
                _pendingZoomTarget = float.NaN; // settled; let the next call start fresh
        }

        private Vector3 _followVelocity;
        private Vector3 _followTargetVelocity;

        /// <summary>Smoothly moves <see cref="Position"/> (and <see cref="Target"/> by the same delta, preserving the current look offset) toward <paramref name="desiredPosition"/> — "follow with delay": the camera eases toward the subject instead of snapping to it every frame.</summary>
        /// <remarks>Within <see cref="FollowPadding"/> world units of the goal, the camera doesn't move at all (a deadzone), which reads as "the subject can wander a little before the camera reacts" rather than a constant low-amplitude jitter.</remarks>
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

        /// <summary>Same as <see cref="FollowTarget(Vector3,float,Vector3?)"/>, with an axis-independent box deadzone instead of <see cref="FollowPadding"/>'s spherical one.</summary>
        /// <param name="desiredPosition">Where the camera should end up once outside the box.</param>
        /// <param name="deltaSeconds">Frame time in seconds.</param>
        /// <param name="deadZoneHalfSize">Half-extents (world units) of the box around <see cref="Position"/> that <paramref name="desiredPosition"/> can move within before the camera reacts, per axis independently.</param>
        /// <param name="desiredTarget">Same as <see cref="FollowTarget(Vector3,float,Vector3?)"/>'s own parameter.</param>
        /// <remarks>Eases toward whichever point keeps <paramref name="desiredPosition"/> exactly at the box's edge on the axis (or axes) it left. Shares <see cref="FollowTarget(Vector3,float,Vector3?)"/>'s own smoothing state; don't alternate between the two per frame on the same camera.</remarks>
        public void FollowTarget(Vector3 desiredPosition, float deltaSeconds, Vector3 deadZoneHalfSize, Vector3? desiredTarget = null)
        {
            Vector3 delta = desiredPosition - Position;
            Vector3 boxDesired = Position;
            if (MathF.Abs(delta.X) > deadZoneHalfSize.X) boxDesired.X = desiredPosition.X - MathF.Sign(delta.X) * deadZoneHalfSize.X;
            if (MathF.Abs(delta.Y) > deadZoneHalfSize.Y) boxDesired.Y = desiredPosition.Y - MathF.Sign(delta.Y) * deadZoneHalfSize.Y;
            if (MathF.Abs(delta.Z) > deadZoneHalfSize.Z) boxDesired.Z = desiredPosition.Z - MathF.Sign(delta.Z) * deadZoneHalfSize.Z;

            Vector3 newPosition = SmoothDamp(Position, boxDesired, ref _followVelocity, FollowSmoothTime, deltaSeconds);
            Vector3 moveDelta = newPosition - Position;
            Position = newPosition;
            Target = desiredTarget.HasValue
                ? SmoothDamp(Target, desiredTarget.Value, ref _followTargetVelocity, FollowSmoothTime, deltaSeconds)
                : Target + moveDelta;

            ClampToBounds();
        }

        /// <summary>Time (seconds) for <see cref="FollowTarget(Vector3,float,Vector3?)"/> to close ~95% of the remaining distance.</summary>
        public float FollowSmoothTime { get; set; } = 0.2f;

        /// <summary>Deadzone radius (world units): <see cref="FollowTarget(Vector3,float,Vector3?)"/> doesn't move the camera until the desired position is at least this far away.</summary>
        public float FollowPadding { get; set; } = 0f;

        /// <summary>Resets <see cref="FollowTarget(Vector3,float,Vector3?)"/>'s internal smoothing velocity — call after teleporting the camera or its subject to avoid a lingering swoop.</summary>
        public void ResetFollowVelocity() { _followVelocity = Vector3.Zero; _followTargetVelocity = Vector3.Zero; }

        /// <summary>Sets <see cref="Target"/> and <see cref="Position"/> (keeping the camera's current viewing direction) so that <paramref name="worldBounds"/> (plus <paramref name="padding"/> world units) fits entirely inside the view frustum — a multi-target/"keep everything of interest visible" camera.</summary>
        /// <param name="worldBounds">World-space region that must end up fully visible.</param>
        /// <param name="padding">Extra world-space margin added around <paramref name="worldBounds"/>'s bounding sphere.</param>
        /// <param name="viewport">Explicit viewport to derive the aspect ratio from; omit to use <see cref="ViewportAdapter"/> instead (throws if neither is available).</param>
        /// <remarks>Sets state directly, no easing. In <see cref="CameraProjection.Perspective"/>, moves <see cref="Position"/> to the distance a sphere of the bounds' size needs to fully fit within <see cref="Fovy"/> (both axes, via the current aspect ratio); in <see cref="CameraProjection.Orthographic"/>, grows <see cref="Fovy"/> (the projection's own world-height) to fit instead, keeping the existing distance.</remarks>
        public void FitBounds(BoundingBox worldBounds, float padding = 0f, Viewport? viewport = null)
        {
            Vector3 center = (worldBounds.Min + worldBounds.Max) * 0.5f;
            float radius = Vector3.Distance(worldBounds.Min, worldBounds.Max) * 0.5f + padding;
            if (radius <= 0f) return;

            Vector3 viewDirection = Normalize(Position - Target, -Vector3.Forward); // preserve current viewing angle

            float aspectRatio = ResolveViewport(viewport).AspectRatio;

            if (Projection == CameraProjection.Orthographic)
            {
                // Fovy IS the vertical world height for orthographic (see GetProjectionMatrix); the
                // horizontal half-extent is Fovy*aspectRatio/2, so satisfy whichever axis is tighter.
                float currentDistance = TargetDistance;
                Fovy = MathF.Max(radius * 2f, radius * 2f / aspectRatio);
                Target = center;
                Position = center + viewDirection * MathF.Max(currentDistance, radius + NearPlane);
            }
            else
            {
                // Distance for a sphere of `radius` to exactly fit within a half-angle `theta` cone
                // from the camera: sin(theta) = radius / distance, i.e. distance = radius / sin(theta).
                // Computed for both the vertical half-FOV and the derived horizontal one (via the
                // same tan(halfFovX) = tan(halfFovY) * aspectRatio relationship GetProjectionMatrix's
                // own orthographic branch uses), and the larger (more constraining) distance wins.
                float halfFovyRad = MathHelper.ToRadians(Fovy) * 0.5f;
                float halfFovxRad = MathF.Atan(MathF.Tan(halfFovyRad) * aspectRatio);
                float distance = MathF.Max(radius / MathF.Sin(halfFovyRad), radius / MathF.Sin(halfFovxRad));
                Target = center;
                Position = center + viewDirection * distance;
            }
        }

        /// <summary>Clamps <see cref="Position"/> into <see cref="PositionBounds"/> (shrunk by <see cref="BoundsPadding"/>) if set, keeping <see cref="Target"/>'s relative offset. Called automatically by <see cref="UpdateWithInput(PrimitiveInput,float)"/> and <see cref="FollowTarget(Vector3,float,Vector3?)"/>; call it yourself after setting <see cref="Position"/> directly if you want the same clamp.</summary>
        public void ClampToBounds()
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

        /// <summary>Critically-damped spring smoothing — eases <paramref name="current"/> toward <paramref name="target"/> over roughly <paramref name="smoothTime"/> seconds.</summary>
        /// <remarks>No overshoot/oscillation under varying frame rates, unlike a naive exponential lerp. <paramref name="velocity"/> is state you own and pass back in every call (start it at 0).</remarks>
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
