#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoPrimitives;

namespace MonoPrimitives.Primitives2D
{
    /// <summary>A 2D camera — pan/rotate/zoom, bounds, smooth-follow, smooth-zoom, screen shake, and an optional input-driven update. Pass <see cref="GetTransformMatrix"/> into <see cref="Primitive2DBatch.Begin(Matrix?,BlendState?,DepthStencilState?,RasterizerState?,Effect?)"/>'s <c>transformMatrix</c> each frame.</summary>
    public sealed class Camera2D
    {
        /// <summary>World point the camera centers on.</summary>
        public Vector2 Target;

        private Vector2 _offset;
        private bool _offsetOverridden;

        /// <summary>Screen-space point <see cref="Target"/> is drawn at — typically the viewport center.</summary>
        /// <remarks>With a <see cref="ViewportAdapter"/> and no direct assignment yet, tracks its virtual center live; assigning <see cref="Offset"/> (even to the same value) pins it, until <see cref="Reset"/> un-pins it again. See DECISIONS.md for why.</remarks>
        public Vector2 Offset
        {
            get => !_offsetOverridden && ViewportAdapter is not null
                ? new Vector2(ViewportAdapter.VirtualWidth * 0.5f, ViewportAdapter.VirtualHeight * 0.5f)
                : _offset;
            set
            {
                _offset = value;
                _offsetOverridden = true;
            }
        }

        /// <summary>Rotation in radians.</summary>
        public float Rotation;

        /// <summary>Scale factor: 1 = no zoom, &gt;1 = zoomed in, &lt;1 = zoomed out.</summary>
        public float Zoom = 1f;

        /// <summary>
        /// Viewport adapter this camera was constructed with, if any — set once at construction,
        /// then every viewport-dependent method (<see cref="GetTransformMatrix"/>,
        /// <see cref="ScreenToWorld"/>, <see cref="WorldToScreen"/>, <see cref="GetVisibleWorldBounds(GraphicsDevice?)"/>,
        /// <see cref="UpdateWithInput(PrimitiveInput, float)"/>'s mouse-drag pan) uses it
        /// automatically instead of requiring it passed in again at every call site.
        /// </summary>
        /// <remarks>
        /// <c>null</c> for the raw-screen-space constructors — those
        /// methods then fall back to assuming <see cref="Offset"/> is already in the same pixel
        /// space as raw device/mouse coordinates.
        /// </remarks>
        public ViewportAdapter2D? ViewportAdapter { get; }

        private readonly Vector2 _initialTarget;
        private readonly Vector2 _initialOffset;
        private readonly float _initialRotation;
        private readonly float _initialZoom;

        /// <summary>Creates a camera with no <see cref="ViewportAdapter"/> — <paramref name="offset"/> is assumed to already be in the same raw pixel space as screen/mouse coordinates.</summary>
        public Camera2D(Vector2 target, Vector2 offset, float rotation = 0f, float zoom = 1f)
        {
            Target = target;
            Offset = offset;
            Rotation = rotation;
            Zoom = zoom;

            _initialTarget = target;
            _initialOffset = offset;
            _initialRotation = rotation;
            _initialZoom = zoom;
        }

        /// <summary>
        /// Creates a camera bound to <paramref name="viewportAdapter"/> — <see cref="Offset"/>
        /// defaults to the adapter's virtual center, and <see cref="ViewportAdapter"/> is stored
        /// so screen↔world conversions and mouse-drag panning account for letterbox bars/scaling
        /// automatically.
        /// </summary>
        /// <remarks>
        /// Prefer this over the raw <see cref="Camera2D(Vector2,Vector2,float,float)"/>
        /// constructor whenever a <see cref="ViewportAdapter2D"/> is in play.
        /// </remarks>
        public Camera2D(ViewportAdapter2D viewportAdapter, Vector2 target = default, float rotation = 0f, float zoom = 1f)
        {
            ViewportAdapter = viewportAdapter ?? throw new ArgumentNullException(nameof(viewportAdapter));
            Target = target;
            Rotation = rotation;
            Zoom = zoom;
            // Offset deliberately left un-pinned (_offsetOverridden stays false) so it live-tracks
            // the adapter's virtual center from here on — see Offset's own doc comment.

            _initialTarget = Target;
            _initialOffset = Offset; // snapshot of the live value right now, for Reset()'s bookkeeping only
            _initialRotation = rotation;
            _initialZoom = zoom;
        }

        /// <summary>Creates a camera centered on the device's current viewport, looking at <paramref name="target"/> (world origin by default). No <see cref="ViewportAdapter"/> — prefer <see cref="Camera2D(ViewportAdapter2D,Vector2,float,float)"/> if you have one.</summary>
        public static Camera2D CreateCentered(GraphicsDevice device, Vector2 target = default)
            => new(target, new Vector2(device.Viewport.Width * 0.5f, device.Viewport.Height * 0.5f));

        /// <summary>Creates a camera with every field at its identity default (no pan, no zoom, world origin at the screen's top-left corner) — a placeholder to construct with before a real target/viewport is known.</summary>
        /// <remarks>Matches <see cref="Primitives3D.Camera3D.CreateDefault"/>'s role; deliberately no bare parameterless constructor so every <see cref="Camera2D"/> is either explicit about its setup or explicit that it's a placeholder.</remarks>
        public static Camera2D CreateDefault() => new(Vector2.Zero, Vector2.Zero);

        /// <summary>
        /// Restores <see cref="Target"/>/<see cref="Offset"/>/<see cref="Rotation"/>/<see cref="Zoom"/>
        /// to the values passed at construction, and clears smooth-zoom/smooth-follow/shake state
        /// so there's no lingering velocity or trauma to swoop/shake through afterward. Bound to
        /// <c>R</c> by default in <see cref="UpdateWithInput(PrimitiveInput, float)"/> — call
        /// directly if you're not using that.
        /// </summary>
        /// <remarks>
        /// Matches <see cref="Primitives3D.Camera3D.Reset"/>. Also un-pins <see cref="Offset"/> if
        /// it had been assigned directly since construction, restoring construction-time behavior
        /// exactly — live tracking again, when constructed with a <see cref="ViewportAdapter"/>.
        /// </remarks>
        public void Reset()
        {
            Target = _initialTarget;
            _offset = _initialOffset;
            _offsetOverridden = false;
            Rotation = _initialRotation;
            Zoom = _initialZoom;

            _pendingZoomTarget = float.NaN;
            _zoomVelocity = 0f;
            ResetFollowVelocity();
            ResetTrauma();
        }

        /// <summary>
        /// Builds the matrix to pass into <c>Primitive2DBatch.Begin</c>'s <c>transformMatrix</c>:
        /// translate by <c>-Target</c>, rotate (<see cref="Rotation"/> plus any screen-shake
        /// rotation — see <see cref="AddTrauma"/>), scale by <see cref="Zoom"/>, then translate
        /// to <see cref="Offset"/> plus any screen-shake offset — the standard 2D camera
        /// composition.
        /// </summary>
        /// <remarks>
        /// When <see cref="ViewportAdapter"/> is set, its own <c>GetScaleMatrix()</c> (virtual →
        /// window pixels) is folded in automatically — don't multiply by it again at the call site.
        /// </remarks>
        public Matrix GetTransformMatrix()
        {
            Matrix cameraMatrix = GetCameraMatrix();
            return ViewportAdapter is not null ? cameraMatrix * ViewportAdapter.GetScaleMatrix() : cameraMatrix;
        }

        /// <summary>The camera's own world↔virtual transform, without <see cref="ViewportAdapter"/>'s virtual↔window mapping — used internally by conversions that need to compose with the adapter differently than <see cref="GetTransformMatrix"/> does.</summary>
        private Matrix GetCameraMatrix()
        {
            (Vector2 shakeOffset, float shakeRotation) = GetShakeOffset();
            return Matrix.CreateTranslation(-Target.X, -Target.Y, 0f)
                 * Matrix.CreateRotationZ(Rotation + shakeRotation)
                 * Matrix.CreateScale(Zoom, Zoom, 1f)
                 * Matrix.CreateTranslation(Offset.X + shakeOffset.X, Offset.Y + shakeOffset.Y, 0f);
        }

        /// <summary>Converts a screen-space position (e.g. mouse coordinates) to world space.</summary>
        /// <remarks>
        /// When <see cref="ViewportAdapter"/> is set, <paramref name="screenPosition"/> is real
        /// window pixels — mapped through the adapter into its virtual space first. Without one,
        /// it's assumed to already be in the same pixel space as <see cref="Offset"/>.
        /// </remarks>
        public Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            Vector2 pos = ViewportAdapter?.PointToVirtual(screenPosition) ?? screenPosition;
            return Vector2.Transform(pos, Matrix.Invert(GetCameraMatrix()));
        }

        /// <summary>Converts a world-space position to screen space — the inverse of <see cref="ScreenToWorld"/>, including the same <see cref="ViewportAdapter"/> mapping back to real window pixels when one is set.</summary>
        public Vector2 WorldToScreen(Vector2 worldPosition)
        {
            Vector2 pos = Vector2.Transform(worldPosition, GetCameraMatrix());
            return ViewportAdapter?.VirtualToPoint(pos) ?? pos;
        }

        /// <summary>
        /// The world-space rectangle currently visible on screen, as its (min, max) corners —
        /// ignoring <see cref="Rotation"/> (an axis-aligned bound around the rotated view).
        /// </summary>
        /// <remarks>
        /// Useful for culling before drawing many objects (boids, cellular-automata cells, plot
        /// points) that are off-screen. Returns corners rather than a framework <c>Rectangle</c>
        /// (whose fields are integers) since world space is floating-point.
        /// </remarks>
        /// <param name="device">
        /// Only used as a fallback when this camera has no <see cref="ViewportAdapter"/> — pass
        /// <c>null</c> (or omit) when constructed with one, since its virtual resolution is used
        /// instead. Required (throws if both are missing) when there's no adapter.
        /// </param>
        public (Vector2 Min, Vector2 Max) GetVisibleWorldBounds(GraphicsDevice? device = null)
        {
            int width = ViewportAdapter?.VirtualWidth ?? device?.Viewport.Width
                ?? throw new ArgumentNullException(nameof(device), $"{nameof(GetVisibleWorldBounds)} needs either a {nameof(ViewportAdapter)} (set at construction) or a {nameof(GraphicsDevice)} argument.");
            int height = ViewportAdapter?.VirtualHeight ?? device!.Viewport.Height;
            return GetVisibleWorldBounds(width, height);
        }

        /// <summary>Same as <see cref="GetVisibleWorldBounds(GraphicsDevice?)"/>, as a <see cref="RectangleF"/> instead of a corner tuple — pairs directly with <see cref="IsVisible(Vector2,GraphicsDevice?)"/>/<see cref="IsVisible(RectangleF,GraphicsDevice?)"/>.</summary>
        /// <param name="device">Same fallback rule as <see cref="GetVisibleWorldBounds(GraphicsDevice?)"/>.</param>
        public RectangleF GetVisibleWorldBoundsF(GraphicsDevice? device = null)
        {
            (Vector2 min, Vector2 max) = GetVisibleWorldBounds(device);
            return new RectangleF(min.X, min.Y, max.X - min.X, max.Y - min.Y);
        }

        /// <summary>True if <paramref name="worldPoint"/> is inside the currently visible world bounds — a cheap "should I bother drawing this" check before drawing many objects.</summary>
        /// <param name="worldPoint">World-space point to test.</param>
        /// <param name="device">Same fallback rule as <see cref="GetVisibleWorldBounds(GraphicsDevice?)"/>.</param>
        public bool IsVisible(Vector2 worldPoint, GraphicsDevice? device = null) => GetVisibleWorldBoundsF(device).Contains(worldPoint);

        /// <summary>True if <paramref name="worldBounds"/> overlaps the currently visible world bounds at all — for culling an object with real extent, not just a point.</summary>
        /// <param name="worldBounds">World-space bounds to test.</param>
        /// <param name="device">Same fallback rule as <see cref="GetVisibleWorldBounds(GraphicsDevice?)"/>.</param>
        public bool IsVisible(RectangleF worldBounds, GraphicsDevice? device = null) => GetVisibleWorldBoundsF(device).Intersects(worldBounds);

        private (Vector2 Min, Vector2 Max) GetVisibleWorldBounds(int width, int height)
        {
            if (Rotation != 0f)
            {
                // Axis-aligned bound around all 4 rotated screen corners' world positions.
                Matrix inv = Matrix.Invert(GetCameraMatrix());
                Vector2 tl = Vector2.Transform(Vector2.Zero, inv);
                Vector2 tr = Vector2.Transform(new Vector2(width, 0), inv);
                Vector2 bl = Vector2.Transform(new Vector2(0, height), inv);
                Vector2 br = Vector2.Transform(new Vector2(width, height), inv);
                float minX = MathF.Min(MathF.Min(tl.X, tr.X), MathF.Min(bl.X, br.X));
                float maxX = MathF.Max(MathF.Max(tl.X, tr.X), MathF.Max(bl.X, br.X));
                float minY = MathF.Min(MathF.Min(tl.Y, tr.Y), MathF.Min(bl.Y, br.Y));
                float maxY = MathF.Max(MathF.Max(tl.Y, tr.Y), MathF.Max(bl.Y, br.Y));
                return (new Vector2(minX, minY), new Vector2(maxX, maxY));
            }

            // Direct formula (not the symmetric "Target ± half" shortcut) since Offset need not
            // be the viewport center — e.g. a camera whose Target maps to the screen's top-left
            // corner instead of its middle. Matches the rotated path's full-inverse result.
            float safeZoom = MathF.Max(Zoom, 1e-6f);
            Vector2 topLeftWorld = -Offset / safeZoom + Target;
            Vector2 bottomRightWorld = (new Vector2(width, height) - Offset) / safeZoom + Target;
            return (Vector2.Min(topLeftWorld, bottomRightWorld), Vector2.Max(topLeftWorld, bottomRightWorld));
        }

        // ---------------------------------------------------------------------
        // Bounds / padding / limits
        // ---------------------------------------------------------------------

        /// <summary>Optional world-space bounds <see cref="Target"/> is kept inside. <c>null</c> (default) disables clamping.</summary>
        public Rectangle? TargetBounds { get; set; }

        /// <summary>Inward margin subtracted from <see cref="TargetBounds"/> before clamping.</summary>
        public float BoundsPadding { get; set; } = 0f;

        /// <summary>Clamps <see cref="Target"/> into <see cref="TargetBounds"/> (shrunk by <see cref="BoundsPadding"/>) if set. Called automatically by <see cref="FollowTarget(Vector2,float)"/>; call it yourself after setting <see cref="Target"/> directly if you want the same clamp.</summary>
        public void ClampToBounds()
        {
            if (!TargetBounds.HasValue)
                return;

            Rectangle b = TargetBounds.Value;
            float minX = b.Left + BoundsPadding, maxX = b.Right - BoundsPadding;
            float minY = b.Top + BoundsPadding, maxY = b.Bottom - BoundsPadding;
            // Padding wider than the box on an axis collapses to that axis's center,
            // instead of an inverted (min > max) clamp range.
            if (minX > maxX) { float c = (b.Left + b.Right) * 0.5f; minX = maxX = c; }
            if (minY > maxY) { float c = (b.Top + b.Bottom) * 0.5f; minY = maxY = c; }

            Target = new Vector2(Math.Clamp(Target.X, minX, maxX), Math.Clamp(Target.Y, minY, maxY));
        }

        // ---------------------------------------------------------------------
        // Smooth follow ("follow with delay")
        // ---------------------------------------------------------------------

        /// <summary>Time (seconds) for <see cref="FollowTarget(Vector2,float)"/> to close ~95% of the remaining distance.</summary>
        public float FollowSmoothTime { get; set; } = 0.2f;

        /// <summary>Deadzone radius (world units): <see cref="FollowTarget(Vector2,float)"/> doesn't move the camera until the desired position is at least this far away.</summary>
        public float FollowPadding { get; set; } = 0f;

        private Vector2 _followVelocity;

        /// <summary>Smoothly moves <see cref="Target"/> toward <paramref name="desiredTarget"/> instead of snapping.</summary>
        /// <remarks>
        /// E.g. follow a player character with a bit of lag instead of the camera being rigidly
        /// locked to it. Within <see cref="FollowPadding"/> world units of the goal, the camera
        /// holds still (a deadzone) rather than chasing tiny jitter.
        /// </remarks>
        public void FollowTarget(Vector2 desiredTarget, float deltaSeconds)
        {
            if (Vector2.Distance(Target, desiredTarget) <= FollowPadding)
            {
                ClampToBounds();
                return;
            }

            Target = SmoothDamp(Target, desiredTarget, ref _followVelocity, FollowSmoothTime, deltaSeconds);
            ClampToBounds();
        }

        /// <summary>Same as <see cref="FollowTarget(Vector2,float)"/>, but with an axis-independent rectangular deadzone instead of <see cref="FollowPadding"/>'s circular one — the "camera box" technique (e.g. looser horizontal than vertical, common in platformers).</summary>
        /// <param name="desiredTarget">World point the camera should end up looking at once outside the box.</param>
        /// <param name="deltaSeconds">Elapsed time this frame.</param>
        /// <param name="deadZoneHalfSize">Half-width/height (world units) of the box around <see cref="Target"/> that <paramref name="desiredTarget"/> can move within before the camera reacts, per axis independently.</param>
        /// <remarks>Eases toward whichever point keeps <paramref name="desiredTarget"/> exactly at the box's edge on the axis (or axes) it left — not snapped, and not toward <paramref name="desiredTarget"/> itself while still inside the box on that axis. Shares <see cref="FollowTarget(Vector2,float)"/>'s own smoothing state; don't alternate between the two per frame on the same camera.</remarks>
        public void FollowTarget(Vector2 desiredTarget, float deltaSeconds, Vector2 deadZoneHalfSize)
        {
            Vector2 delta = desiredTarget - Target;
            Vector2 boxDesired = Target;
            if (MathF.Abs(delta.X) > deadZoneHalfSize.X) boxDesired.X = desiredTarget.X - MathF.Sign(delta.X) * deadZoneHalfSize.X;
            if (MathF.Abs(delta.Y) > deadZoneHalfSize.Y) boxDesired.Y = desiredTarget.Y - MathF.Sign(delta.Y) * deadZoneHalfSize.Y;

            Target = SmoothDamp(Target, boxDesired, ref _followVelocity, FollowSmoothTime, deltaSeconds);
            ClampToBounds();
        }

        /// <summary>Resets <see cref="FollowTarget(Vector2,float)"/>'s internal smoothing velocity — call after teleporting the camera or its subject to avoid a lingering swoop.</summary>
        public void ResetFollowVelocity() => _followVelocity = Vector2.Zero;

        /// <summary>Sets <see cref="Target"/> and <see cref="Zoom"/> so that <paramref name="worldBounds"/> (plus <paramref name="padding"/> world units on every side) fits entirely inside the current viewport — a multi-target/RTS-style "keep everything of interest visible" camera. Sets state directly, no easing; wrap the call in your own smoothing if you want it eased.</summary>
        /// <param name="worldBounds">World-space region that must end up fully visible.</param>
        /// <param name="padding">Extra world-space margin added around <paramref name="worldBounds"/> on every side before fitting.</param>
        /// <param name="device">Same fallback rule as <see cref="GetVisibleWorldBounds(GraphicsDevice?)"/>.</param>
        /// <remarks>Doesn't clamp to <see cref="MinZoom"/>/<see cref="MaxZoom"/> — those are <see cref="SmoothZoom"/>'s own constraint, and clamping here could silently break the "fits" contract this method promises. Clamp the result yourself afterward if you need both.</remarks>
        public void FitBounds(RectangleF worldBounds, float padding = 0f, GraphicsDevice? device = null)
        {
            int width = ViewportAdapter?.VirtualWidth ?? device?.Viewport.Width
                ?? throw new ArgumentNullException(nameof(device), $"{nameof(FitBounds)} needs either a {nameof(ViewportAdapter)} (set at construction) or a {nameof(GraphicsDevice)} argument.");
            int height = ViewportAdapter?.VirtualHeight ?? device!.Viewport.Height;

            float requiredWidth = worldBounds.Width + padding * 2f;
            float requiredHeight = worldBounds.Height + padding * 2f;
            if (requiredWidth <= 0f || requiredHeight <= 0f) return;

            Zoom = MathF.Min(width / requiredWidth, height / requiredHeight);
            Target = worldBounds.Center;
        }

        // ---------------------------------------------------------------------
        // Smooth zoom
        // ---------------------------------------------------------------------

        /// <summary>Minimum allowed <see cref="Zoom"/> for <see cref="SmoothZoom"/>.</summary>
        public float MinZoom { get; set; } = 0.1f;

        /// <summary>Maximum allowed <see cref="Zoom"/> for <see cref="SmoothZoom"/>.</summary>
        public float MaxZoom { get; set; } = 10f;

        /// <summary>Time (seconds) for <see cref="SmoothZoom"/> to close ~95% of the distance to its target — 0 disables smoothing (instant).</summary>
        public float ZoomSmoothTime { get; set; } = 0.12f;

        private float _zoomVelocity;
        private float _pendingZoomTarget = float.NaN;

        /// <summary>
        /// Adds <paramref name="delta"/> to <see cref="Zoom"/>'s target, eased over
        /// <see cref="ZoomSmoothTime"/> seconds and clamped to [<see cref="MinZoom"/>,
        /// <see cref="MaxZoom"/>], instead of changing it instantly.
        /// </summary>
        /// <remarks>
        /// For discrete input (a mouse wheel tick) where <paramref name="delta"/> is naturally 0
        /// most frames: call every frame, most calls no-op. For continuous input (a key held to
        /// zoom), don't call this every frame with a small nonzero <paramref name="delta"/> — each
        /// call adds onto the target immediately, so a delta repeated every frame races the
        /// target ahead rather than climbing smoothly. Adjust <see cref="Zoom"/> directly by
        /// <c>rate * deltaSeconds</c> for that case instead.
        /// </remarks>
        public void SmoothZoom(float delta, float deltaSeconds)
        {
            if (delta == 0f && float.IsNaN(_pendingZoomTarget))
                return;

            float target = float.IsNaN(_pendingZoomTarget) ? Zoom : _pendingZoomTarget;
            target = Math.Clamp(target + delta, MinZoom, MaxZoom);
            _pendingZoomTarget = target;

            Zoom = ZoomSmoothTime <= 0f ? target : SmoothDamp(Zoom, target, ref _zoomVelocity, ZoomSmoothTime, deltaSeconds);

            // Relative to target, capped at the plain 0.0005f used for a normal (~0.1-10) zoom
            // range -- a fixed absolute epsilon alone declares "settled" while still meaningfully
            // (percentage-wise) short of target for a caller-chosen MinZoom/MaxZoom range much
            // smaller than that, permanently stalling short of it (SmoothZoom no-ops once settled).
            // Verified: a 0.001-0.01 zoom range settled 7% short of target after 1 frame with a
            // fixed epsilon; this closes to within 0.01% over a normal ~20-30 frame ease instead.
            float epsilon = MathF.Min(0.0005f, MathF.Abs(target) * 0.001f);
            if (MathF.Abs(Zoom - target) < epsilon)
                _pendingZoomTarget = float.NaN;
        }

        // ---------------------------------------------------------------------
        // Input-driven update
        // ---------------------------------------------------------------------
        // UpdateWithInput reads directly from a PrimitiveInput the caller owns and updates
        // itself (typically once per frame elsewhere in Update) — the camera never advances or
        // owns input state of its own, so the same session's input can drive UI, gameplay and
        // the camera without the three stepping on each other's delta tracking.

        /// <summary><see cref="MoveSpeed"/>'s default value.</summary>
        public const float DefaultMoveSpeed = 700f;

        /// <summary><see cref="MouseWheelZoomSensitivity"/>'s default value.</summary>
        public const float DefaultMouseWheelZoomSensitivity = 0.06f;

        /// <summary>Keyboard pan speed in world units per second (before the 1/<see cref="Zoom"/> scaling <see cref="UpdateWithInput(PrimitiveInput, float)"/> applies, so panning feels the same screen-space speed at any zoom level). Editable; defaults to <see cref="DefaultMoveSpeed"/>.</summary>
        public float MoveSpeed { get; set; } = DefaultMoveSpeed;

        /// <summary>Mouse-wheel-to-zoom scale, used by <see cref="UpdateWithInput(PrimitiveInput, float)"/>. Editable; defaults to <see cref="DefaultMouseWheelZoomSensitivity"/>.</summary>
        public float MouseWheelZoomSensitivity { get; set; } = DefaultMouseWheelZoomSensitivity;

        /// <summary>
        /// Advances internal state only — screen-shake trauma decay (see <see cref="AddTrauma"/>)
        /// and any in-flight <see cref="SmoothZoom"/> easing — with no movement and no input
        /// reading.
        /// </summary>
        /// <remarks>
        /// Call this every frame when you're driving <see cref="Target"/>/<see cref="Zoom"/>/
        /// <see cref="Rotation"/> yourself (a fixed prototype camera, a cutscene) and just want
        /// shake/easing to keep working. For a built-in WASD-pan/drag-pan/wheel-zoom controller
        /// driven by your own <see cref="PrimitiveInput"/>, use
        /// <see cref="UpdateWithInput(PrimitiveInput, float)"/> instead.
        /// </remarks>
        public void Update(float deltaSeconds) => UpdateShake(deltaSeconds);

        /// <inheritdoc cref="Update(float)"/>
        public void Update(GameTime gameTime) => Update((float)gameTime.ElapsedGameTime.TotalSeconds);

        /// <summary>
        /// Advances the camera one frame using W/A/S/D pan, left-mouse-drag pan and wheel zoom
        /// read from <paramref name="input"/> — the simplest way to get a working camera with no
        /// input code of your own. <c>R</c> calls <see cref="Reset"/> directly and skips movement
        /// for that frame, so a same-frame WASD/mouse delta doesn't immediately fight the reset.
        /// </summary>
        /// <remarks>
        /// This is a prototyping convenience, not something every game wants baked into its
        /// camera; use the plain <see cref="Update(float)"/> if you're driving the camera from
        /// your own logic, or query <paramref name="input"/> yourself and set
        /// <see cref="Target"/>/<see cref="Zoom"/> directly for custom bindings. Doesn't call
        /// <see cref="PrimitiveInput.Update(GameTime)"/> itself — <paramref name="input"/> is
        /// expected to already be current for this frame (the caller's own <c>Update</c> updates
        /// it once, then hands the same instance to everything that reads it, this camera
        /// included).
        ///
        /// Pan speed is divided by <see cref="Zoom"/> so keyboard panning covers the same amount
        /// of *screen* space per second regardless of zoom level, matching how the mouse-drag pan
        /// already behaves (a drag always tracks the cursor 1:1 on screen). When
        /// <see cref="ViewportAdapter"/> is set, mouse-drag pan is also divided by its
        /// <see cref="ViewportAdapter2D.Scale"/> — <see cref="PrimitiveInput.MouseDelta"/> is
        /// always real window pixels, so without this a drag would pan the wrong amount whenever
        /// the adapter's scale isn't 1:1 (letterbox scaling, a virtual resolution that doesn't
        /// match the window). Keyboard pan doesn't need this — its speed is defined directly in
        /// world units, not screen pixels.
        /// </remarks>
        public void UpdateWithInput(PrimitiveInput input, float deltaSeconds)
        {
            if (input.IsKeyPressed(Keys.R))
            {
                Reset();
                return;
            }

            float safeZoom = MathF.Max(Zoom, 1e-4f);
            float speed = MoveSpeed * deltaSeconds / safeZoom;

            Vector2 pan = input.GetVector2(Keys.A, Keys.D, Keys.W, Keys.S) * speed;

            if (input.IsMouseButtonDown(MouseButton.Left))
            {
                Vector2 mouseDelta = input.MouseDelta;
                if (ViewportAdapter is not null)
                    mouseDelta /= ViewportAdapter.Scale;
                // Undo the camera's own rotation before scaling into world space, matching
                // GetCameraMatrix's ROT * Zoom composition — without this, a rotated camera's
                // drag pans along screen axes instead of tracking the cursor (verified: a 90°
                // rotation turned a pure-X drag into pan drift on the Y axis instead).
                if (Rotation != 0f)
                {
                    float cos = MathF.Cos(-Rotation), sin = MathF.Sin(-Rotation);
                    mouseDelta = new Vector2(mouseDelta.X * cos - mouseDelta.Y * sin, mouseDelta.X * sin + mouseDelta.Y * cos);
                }
                pan -= mouseDelta / safeZoom;
            }

            float zoom = input.MouseScrollDelta * (MouseWheelZoomSensitivity / 120f);

            Target += pan;
            // Unconditional, matching SmoothZoom's own "call every frame, most calls no-op"
            // contract: a wheel tick only makes zoom nonzero for one frame, but the easing
            // toward that tick's target has to keep advancing on every frame after it too — a
            // guard here would let it take one small step and then freeze.
            SmoothZoom(zoom, deltaSeconds);
            UpdateShake(deltaSeconds);
            ClampToBounds();
        }

        /// <inheritdoc cref="UpdateWithInput(PrimitiveInput, float)"/>
        public void UpdateWithInput(PrimitiveInput input, GameTime gameTime) => UpdateWithInput(input, (float)gameTime.ElapsedGameTime.TotalSeconds);

        // ---------------------------------------------------------------------
        // Screen shake (trauma-based)
        // ---------------------------------------------------------------------
        // The technique most engines converged on (popularized by Squirrel Eiserloh's GDC talk
        // "Math for Game Programmers: Juicing Your Cameras With Math", used by Celeste and
        // plenty else): a "trauma" value in [0,1] that jumps up on impact and decays back to 0
        // over time, with shake magnitude scaled by trauma^2 (small bumps barely shake, big
        // hits shake hard, and the falloff itself feels natural rather than linear). The offset
        // is sampled from Perlin noise (this library's own Noise, one channel per axis plus one
        // for rotation) rather than raw random — smooth frame-to-frame motion instead of jitter,
        // for the same reason a hand-held camera looks like a hand-held camera and not static.
        // Purely a rendering effect: baked into GetTransformMatrix(), never touches Target/
        // Rotation/Zoom themselves, so nothing that reads camera state sees the shake.

        private readonly Noise _shakeNoiseX = new(unchecked((int)0x53484B58)); // "SHKX"
        private readonly Noise _shakeNoiseY = new(unchecked((int)0x53484B59)); // "SHKY"
        private readonly Noise _shakeNoiseRot = new(unchecked((int)0x53484B52)); // "SHKR"
        // Starts away from 0 on purpose: Perlin noise is exactly 0 at every integer lattice
        // point by construction, and 0 itself is one — starting there would make the very
        // first shake read (AddTrauma then GetShakeOffset/GetTransformMatrix before any Update
        // tick has advanced this) silently zero.
        private float _shakeTime = 0.5f;

        /// <summary>Current shake intensity, [0,1] — 0 is calm. Decays toward 0 at <see cref="TraumaDecayPerSecond"/> every time any <c>Update</c> overload runs. Set directly, or use <see cref="AddTrauma"/> to bump it without risking pushing it out of range.</summary>
        public float Trauma { get; set; }

        /// <summary>How fast <see cref="Trauma"/> decays back to 0, in units of trauma per second.</summary>
        public float TraumaDecayPerSecond { get; set; } = 1.5f;

        /// <summary>Shake displacement (world units) at maximum trauma (1.0) — actual displacement scales with trauma².</summary>
        public float MaxShakeOffset { get; set; } = 30f;

        /// <summary>Shake rotation (radians) at maximum trauma (1.0) — actual rotation scales with trauma².</summary>
        public float MaxShakeRotation { get; set; } = 0.12f;

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

        /// <summary>The current frame's shake offset/rotation — already folded into <see cref="GetTransformMatrix"/>; exposed separately in case you want to apply it to something else (e.g. a UI element that should shake in sync).</summary>
        public (Vector2 Offset, float Rotation) GetShakeOffset()
        {
            if (Trauma <= 0f) return (Vector2.Zero, 0f);
            float shake = Trauma * Trauma;
            float ox = _shakeNoiseX.Sample1D(_shakeTime) * shake * MaxShakeOffset;
            float oy = _shakeNoiseY.Sample1D(_shakeTime) * shake * MaxShakeOffset;
            float rot = _shakeNoiseRot.Sample1D(_shakeTime) * shake * MaxShakeRotation;
            return (new Vector2(ox, oy), rot);
        }

        // ---------------------------------------------------------------------
        // Easing
        // ---------------------------------------------------------------------

        /// <summary>
        /// Critically-damped spring smoothing — eases <paramref name="current"/> toward <paramref name="target"/> over roughly
        /// <paramref name="smoothTime"/> seconds without overshoot across varying frame rates.
        /// <paramref name="velocity"/> is state you own and pass back in every call (start at 0).
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

        /// <summary>Component-wise <see cref="SmoothDamp(float,float,ref float,float,float)"/> for a <see cref="Vector2"/>.</summary>
        public static Vector2 SmoothDamp(Vector2 current, Vector2 target, ref Vector2 velocity, float smoothTime, float deltaTime)
        {
            float vx = velocity.X, vy = velocity.Y;
            Vector2 result = new(
                SmoothDamp(current.X, target.X, ref vx, smoothTime, deltaTime),
                SmoothDamp(current.Y, target.Y, ref vy, smoothTime, deltaTime));
            velocity = new Vector2(vx, vy);
            return result;
        }
    }
}
