#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoPrimitives;

namespace MonoPrimitives.Primitives2D
{
    /// <summary>
    /// Per-frame pan/zoom request for <see cref="Camera2D.Update(in CameraInput2D, float)"/>.
    /// Filling this yourself (instead of always going through
    /// <see cref="Camera2D.ReadDefaultInput(float)"/>) keeps the camera logic input-agnostic,
    /// which matters for replays, network play and tests. Named with a "2D" suffix (unlike
    /// <c>Camera2D</c> itself) because <c>Primitives3D.CameraInput</c> is commonly imported
    /// alongside this namespace and an unsuffixed <c>CameraInput</c> here would collide.
    /// </summary>
    public struct CameraInput2D
    {
        /// <summary>World-space pan request, already scaled by speed and delta time.</summary>
        public Vector2 Pan;

        /// <summary>Zoom delta (mouse wheel), fed into <see cref="Camera2D.SmoothZoom"/>.</summary>
        public float Zoom;
    }

    /// <summary>
    /// A 2D camera: <see cref="Target"/>/<see cref="Offset"/>/<see cref="Rotation"/>/
    /// <see cref="Zoom"/>, plus the same bounds/padding/smooth-follow/smooth-zoom/easing/
    /// input-driven-update <c>Camera3D</c> has in the companion 3D library — a 2D prototype (a
    /// platformer, an asteroids clone, a pan-and-zoom plot) starts from a camera that already
    /// does the common things. Pass <see cref="GetTransformMatrix"/> into
    /// <see cref="PrimitiveBatch.Begin(Matrix?,BlendState?,DepthStencilState?,RasterizerState?,Effect?)"/>'s
    /// <c>transformMatrix</c> each frame.
    /// </summary>
    public sealed class Camera2D
    {
        /// <summary>World point the camera centers on.</summary>
        public Vector2 Target;

        /// <summary>Screen-space point <see cref="Target"/> is drawn at — typically the viewport center.</summary>
        public Vector2 Offset;

        /// <summary>Rotation in radians.</summary>
        public float Rotation;

        /// <summary>Scale factor: 1 = no zoom, &gt;1 = zoomed in, &lt;1 = zoomed out.</summary>
        public float Zoom = 1f;

        /// <summary>
        /// Viewport adapter this camera was constructed with, if any — MonoGame.Extended-style:
        /// set once at construction, then every viewport-dependent method (<see cref="ScreenToWorld"/>,
        /// <see cref="WorldToScreen"/>, <see cref="GetVisibleWorldBounds"/>,
        /// <see cref="ReadDefaultInput(float)"/>'s mouse-drag pan) uses it automatically instead
        /// of requiring it passed in again at every call site. <c>null</c> for the raw-screen-space
        /// constructors — those methods then fall back to assuming <see cref="Offset"/> is
        /// already in the same pixel space as raw device/mouse coordinates.
        /// </summary>
        public ViewportAdapter2D? ViewportAdapter { get; }

        public Camera2D() { }

        public Camera2D(Vector2 target, Vector2 offset, float rotation = 0f, float zoom = 1f)
        {
            Target = target;
            Offset = offset;
            Rotation = rotation;
            Zoom = zoom;
        }

        /// <summary>
        /// MonoGame.Extended's own <c>OrthographicCamera(ViewportAdapter)</c> constructor shape:
        /// <see cref="Offset"/> defaults to the adapter's virtual center, and <see cref="ViewportAdapter"/>
        /// is stored so screen↔world conversions and mouse-drag panning account for letterbox
        /// bars/scaling automatically. Prefer this over the raw <see cref="Camera2D(Vector2,Vector2,float,float)"/>
        /// constructor whenever a <see cref="ViewportAdapter2D"/> is in play.
        /// </summary>
        public Camera2D(ViewportAdapter2D viewportAdapter, Vector2 target = default, float rotation = 0f, float zoom = 1f)
        {
            ViewportAdapter = viewportAdapter ?? throw new ArgumentNullException(nameof(viewportAdapter));
            Target = target;
            Offset = new Vector2(viewportAdapter.VirtualWidth * 0.5f, viewportAdapter.VirtualHeight * 0.5f);
            Rotation = rotation;
            Zoom = zoom;
        }

        /// <summary>Creates a camera centered on the device's current viewport, looking at <paramref name="target"/> (world origin by default). No <see cref="ViewportAdapter"/> — prefer <see cref="Camera2D(ViewportAdapter2D,Vector2,float,float)"/> if you have one.</summary>
        public static Camera2D CreateCentered(GraphicsDevice device, Vector2 target = default)
            => new(target, new Vector2(device.Viewport.Width * 0.5f, device.Viewport.Height * 0.5f));

        /// <summary>
        /// Builds the matrix to pass into <c>PrimitiveBatch.Begin</c>'s <c>transformMatrix</c>:
        /// translate by <c>-Target</c>, rotate, scale by <see cref="Zoom"/>, then translate to
        /// <see cref="Offset"/> — the standard 2D camera composition. Does NOT include
        /// <see cref="ViewportAdapter"/>'s own scale/offset — compose with
        /// <c>ViewportAdapter.GetScaleMatrix()</c> yourself (per <c>Design/2D/ViewportAdapter_Guide.md</c>)
        /// the same way you would with any other <see cref="ViewportAdapter2D"/> user.
        /// </summary>
        public Matrix GetTransformMatrix()
            => Matrix.CreateTranslation(-Target.X, -Target.Y, 0f)
             * Matrix.CreateRotationZ(Rotation)
             * Matrix.CreateScale(Zoom, Zoom, 1f)
             * Matrix.CreateTranslation(Offset.X, Offset.Y, 0f);

        /// <summary>
        /// Converts a screen-space position (e.g. mouse coordinates) to world space. When
        /// <see cref="ViewportAdapter"/> is set, <paramref name="screenPosition"/> is real window
        /// pixels — mapped through the adapter into its virtual space first. Without one, it's
        /// assumed to already be in the same pixel space as <see cref="Offset"/>.
        /// </summary>
        public Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            Vector2 pos = ViewportAdapter?.PointToVirtual(screenPosition) ?? screenPosition;
            return Vector2.Transform(pos, Matrix.Invert(GetTransformMatrix()));
        }

        /// <summary>Converts a world-space position to screen space — the inverse of <see cref="ScreenToWorld"/>, including the same <see cref="ViewportAdapter"/> mapping back to real window pixels when one is set.</summary>
        public Vector2 WorldToScreen(Vector2 worldPosition)
        {
            Vector2 pos = Vector2.Transform(worldPosition, GetTransformMatrix());
            return ViewportAdapter?.VirtualToPoint(pos) ?? pos;
        }

        /// <summary>
        /// The world-space rectangle currently visible on screen, as its (min, max) corners —
        /// ignoring <see cref="Rotation"/> (an axis-aligned bound around the rotated view) —
        /// useful for culling before drawing many objects (boids, cellular-automata cells, plot
        /// points) that are off-screen. Returns corners rather than a framework <c>Rectangle</c>
        /// (whose fields are integers) since world space is floating-point.
        /// </summary>
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

        private (Vector2 Min, Vector2 Max) GetVisibleWorldBounds(int width, int height)
        {
            if (Rotation != 0f)
            {
                // Axis-aligned bound around all 4 rotated screen corners' world positions.
                Matrix inv = Matrix.Invert(GetTransformMatrix());
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

        /// <summary>Clamps <see cref="Target"/> into <see cref="TargetBounds"/> (shrunk by <see cref="BoundsPadding"/>) if set. Called automatically by <see cref="FollowTarget"/>; call it yourself after setting <see cref="Target"/> directly if you want the same clamp.</summary>
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

        /// <summary>Time (seconds) for <see cref="FollowTarget"/> to close ~95% of the remaining distance.</summary>
        public float FollowSmoothTime { get; set; } = 0.2f;

        /// <summary>Deadzone radius (world units): <see cref="FollowTarget"/> doesn't move the camera until the desired position is at least this far away.</summary>
        public float FollowPadding { get; set; } = 0f;

        private Vector2 _followVelocity;

        /// <summary>
        /// Smoothly moves <see cref="Target"/> toward <paramref name="desiredTarget"/> instead
        /// of snapping — e.g. follow a player character with a bit of lag instead of the
        /// camera being rigidly locked to it. Within <see cref="FollowPadding"/> world units of
        /// the goal, the camera holds still (a deadzone) rather than chasing tiny jitter.
        /// </summary>
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

        /// <summary>Resets <see cref="FollowTarget"/>'s internal smoothing velocity — call after teleporting the camera or its subject to avoid a lingering swoop.</summary>
        public void ResetFollowVelocity() => _followVelocity = Vector2.Zero;

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
        /// <see cref="MaxZoom"/>], instead of changing it instantly. For discrete input
        /// (a mouse wheel tick) where <paramref name="delta"/> is naturally 0 most frames:
        /// call every frame, most calls no-op. For continuous input (a key held to zoom),
        /// don't call this every frame with a small nonzero <paramref name="delta"/> — each
        /// call adds onto the target immediately, so a delta repeated every frame races the
        /// target ahead rather than climbing smoothly. Adjust <see cref="Zoom"/> directly by
        /// <c>rate * deltaSeconds</c> for that case instead.
        /// </summary>
        public void SmoothZoom(float delta, float deltaSeconds)
        {
            if (delta == 0f && float.IsNaN(_pendingZoomTarget))
                return;

            float target = float.IsNaN(_pendingZoomTarget) ? Zoom : _pendingZoomTarget;
            target = Math.Clamp(target + delta, MinZoom, MaxZoom);
            _pendingZoomTarget = target;

            Zoom = ZoomSmoothTime <= 0f ? target : SmoothDamp(Zoom, target, ref _zoomVelocity, ZoomSmoothTime, deltaSeconds);

            if (MathF.Abs(Zoom - target) < 0.0005f)
                _pendingZoomTarget = float.NaN;
        }

        // ---------------------------------------------------------------------
        // Input-driven update
        // ---------------------------------------------------------------------
        // Same shape as Camera3D's ReadDefaultInput/Update split: build a CameraInput2D from
        // whatever source you like (the built-in keyboard/mouse reader below, a gamepad, a
        // network packet, a replay file) and feed it to Update — the camera itself never reads
        // input directly outside of ReadDefaultInput.

        private const float DefaultMoveSpeed = 700f;
        private const float DefaultMouseWheelZoomSensitivity = 0.06f;

        /// <summary>Keyboard pan speed in world units per second (before the 1/<see cref="Zoom"/> scaling <see cref="ReadDefaultInput(float)"/> applies, so panning feels the same screen-space speed at any zoom level). Editable; defaults to <see cref="DefaultMoveSpeed"/>.</summary>
        public float MoveSpeed { get; set; } = DefaultMoveSpeed;

        /// <summary>Mouse-wheel-to-zoom scale, used by <see cref="ReadDefaultInput(float)"/>. Editable; defaults to <see cref="DefaultMouseWheelZoomSensitivity"/>.</summary>
        public float MouseWheelZoomSensitivity { get; set; } = DefaultMouseWheelZoomSensitivity;

        private readonly PrimitiveInput _input = new();

        /// <summary>Advances the camera one frame using <see cref="ReadDefaultInput(float)"/> — the simplest way to get a working pan/zoom camera with no input code of your own.</summary>
        public void Update(float deltaSeconds) => Update(ReadDefaultInput(deltaSeconds), deltaSeconds);

        /// <inheritdoc cref="Update(float)"/>
        public void Update(GameTime gameTime) => Update((float)gameTime.ElapsedGameTime.TotalSeconds);

        /// <summary>Advances the camera one frame using an explicit <see cref="CameraInput2D"/>, taking the frame delta from a MonoGame <see cref="GameTime"/> instead of a raw float.</summary>
        public void Update(GameTime gameTime, in CameraInput2D input) => Update(input, (float)gameTime.ElapsedGameTime.TotalSeconds);

        /// <summary>Applies <paramref name="input"/>'s pan to <see cref="Target"/> and zoom to <see cref="SmoothZoom"/>, then re-clamps to <see cref="TargetBounds"/> if set.</summary>
        public void Update(in CameraInput2D input, float deltaSeconds)
        {
            Target += input.Pan;
            // Unconditional, matching SmoothZoom's own "call every frame, most calls no-op"
            // contract: a wheel tick only makes input.Zoom nonzero for one frame, but the easing
            // toward that tick's target has to keep advancing on every frame after it too — a
            // guard here would let it take one small step and then freeze.
            SmoothZoom(input.Zoom, deltaSeconds);
            ClampToBounds();
        }

        /// <summary>Builds a <see cref="CameraInput2D"/> from the current keyboard and mouse state, taking the frame delta straight from a MonoGame <see cref="GameTime"/> instead of a raw float.</summary>
        public CameraInput2D ReadDefaultInput(GameTime gameTime) => ReadDefaultInput((float)gameTime.ElapsedGameTime.TotalSeconds);

        /// <summary>
        /// Builds a <see cref="CameraInput2D"/> from the current keyboard and mouse state
        /// (W/A/S/D pan, left-mouse-drag pan, wheel zoom) via the shared <see cref="PrimitiveInput"/>.
        /// Pan speed is divided by <see cref="Zoom"/> so keyboard panning covers the same amount
        /// of *screen* space per second regardless of zoom level, matching how the mouse-drag
        /// pan already behaves (a drag always tracks the cursor 1:1 on screen). When
        /// <see cref="ViewportAdapter"/> is set, mouse-drag pan is also divided by its
        /// <see cref="ViewportAdapter2D.Scale"/> — <see cref="PrimitiveInput.MouseDelta"/> is
        /// always real window pixels, so without this a drag would pan the wrong amount whenever
        /// the adapter's scale isn't 1:1 (letterbox scaling, a virtual resolution that doesn't
        /// match the window). Keyboard pan doesn't need this — its speed is defined directly in
        /// world units, not screen pixels.
        /// </summary>
        public CameraInput2D ReadDefaultInput(float deltaSeconds)
        {
            _input.Update(deltaSeconds);

            float safeZoom = MathF.Max(Zoom, 1e-4f);
            float speed = MoveSpeed * deltaSeconds / safeZoom;

            CameraInput2D input = default;
            input.Pan = _input.GetVector2(Keys.A, Keys.D, Keys.W, Keys.S) * speed;

            if (_input.IsMouseButtonDown(MouseButton.Left))
            {
                Vector2 mouseDelta = _input.MouseDelta;
                if (ViewportAdapter is not null)
                    mouseDelta /= ViewportAdapter.Scale;
                input.Pan -= mouseDelta / safeZoom;
            }

            input.Zoom = _input.MouseScrollDelta * (MouseWheelZoomSensitivity / 120f);

            return input;
        }

        /// <summary>Resets <see cref="ReadDefaultInput(float)"/>'s internal mouse-delta tracking — call after teleporting the camera or repositioning the mouse to avoid a one-frame pan jump.</summary>
        public void ResetMouseTracking() => _input.ResetMouseDelta();

        // ---------------------------------------------------------------------
        // Easing
        // ---------------------------------------------------------------------

        /// <summary>
        /// Critically-damped spring smoothing (the same algorithm as Unity's <c>Mathf.SmoothDamp</c>)
        /// — eases <paramref name="current"/> toward <paramref name="target"/> over roughly
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
