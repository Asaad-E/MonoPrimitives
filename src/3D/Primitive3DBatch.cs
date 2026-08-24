using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives.Primitives2D;

namespace MonoPrimitives.Primitives3D
{
    /// <summary>
    /// Immediate-mode 3D primitive batcher. Every primitive (including lines and points) is
    /// converted to triangles so that a single vertex buffer / draw path is used.
    /// <para>
    /// The batch is fully non-invasive: all graphics device state it modifies is
    /// captured on <see cref="Begin(Camera3D,BlendState?,DepthStencilState?,RasterizerState?,Matrix?)"/> and restored on <see cref="End"/>, so it can
    /// be interleaved with <see cref="SpriteBatch"/> or custom 3D rendering.
    /// </para>
    /// </summary>
    public sealed partial class Primitive3DBatch : IDisposable
    {
        // ---------------------------------------------------------------------
        // Constants
        // ---------------------------------------------------------------------

        /// <summary>Default vertex capacity (must be a multiple of 3).</summary>
        public const int DefaultMaxVertices = 24576 * 2; // 8192 triangles

        /// <summary>Minimum segment count used by the automatic LOD estimator.</summary>
        public const int AutoSegmentsMin = 8;

        /// <summary>Maximum segment count used by the automatic LOD estimator.</summary>
        public const int AutoSegmentsMax = 96;

        internal const int DefaultCircleSegments = 48;
        internal const int DefaultSphereRings = 24;
        internal const int DefaultSphereSlices = 24;
        internal const int DefaultCapsuleSlices = 24;
        internal const int DefaultCapsuleRings = 12;

        // ---------------------------------------------------------------------
        // Fields
        // ---------------------------------------------------------------------

        private readonly GraphicsDevice _device;
        private readonly VertexPositionColor[] _vertices;
        private readonly VertexPositionColor[] _verticesLines;

        private readonly int _maxVertices;
        private readonly BasicEffect _effect;

        private int _vertexCount;
        private int _vertexCountLine;

        private bool _began;
        private bool _disposed;

        // Captured device state (restored on End) -- always assigned in Begin() before use, not the
        // constructor, so the null-forgiving initializer just tells the analyzer what's already true
        // rather than forcing every read site to null-check a value that can't actually be null there.
        private BlendState _prevBlend = null!;
        private DepthStencilState _prevDepth = null!;
        private RasterizerState _prevRasterizer = null!;
        private SamplerState _prevSampler0 = null!;
        private VertexBuffer? _prevVertexBuffer; // MonoGame exposes no getter for the current vertex buffer -- genuinely null whenever none was bound, not just "not yet captured" like the fields above.
        private IndexBuffer _prevIndexBuffer = null!;

        // Current render settings for this Begin/End block.
        private Matrix _view;
        private Matrix _projection;
        private Matrix _transform;
        private bool _hasTransform;

        // Same "always set in Begin(), not the constructor" reasoning as the _prev* fields above.
        private BlendState _blendState = null!;
        private DepthStencilState _depthStencilState = null!;
        private RasterizerState _rasterizerState = null!;

        // ---------------------------------------------------------------------
        // Construction
        // ---------------------------------------------------------------------

        /// <summary>Creates a new batcher.</summary>
        /// <param name="graphicsDevice">Device used for rendering.</param>
        /// <param name="maxVertices">
        /// Vertex capacity before an automatic flush occurs. Rounded down to a
        /// multiple of 3.
        /// </param>
        public Primitive3DBatch(GraphicsDevice graphicsDevice, int maxVertices = DefaultMaxVertices)
        {
            _device = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));

            if (maxVertices < 3)
                throw new ArgumentOutOfRangeException(nameof(maxVertices), "At least 3 vertices are required.");

            _maxVertices = maxVertices - (maxVertices % 3);
            _vertices = new VertexPositionColor[_maxVertices];
            _verticesLines = new VertexPositionColor[_maxVertices];

            _effect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                TextureEnabled = false,
                LightingEnabled = false,
                FogEnabled = false,
                World = Matrix.Identity
            };
        }

        // ---------------------------------------------------------------------
        // Properties
        // ---------------------------------------------------------------------

        /// <summary>
        /// The <see cref="BasicEffect"/> this batch draws with — constructed internally, so this
        /// is the only way to reach it (the same "escape hatch to the thing this class wraps"
        /// shape as <see cref="RandomUtil.UnderlyingRandom"/>). Tweak parameters this batch's own
        /// API has no dedicated call for — <see cref="BasicEffect.Alpha"/> for a global fade, a
        /// custom <see cref="BasicEffect.FogEnabled"/> setup, etc. Don't change
        /// <see cref="BasicEffect.VertexColorEnabled"/>, <see cref="BasicEffect.TextureEnabled"/>,
        /// or <see cref="BasicEffect.World"/> — this batch depends on those staying exactly as
        /// constructed. <see cref="LightingEnabled"/> is this batch's own opt-in flat-shading
        /// switch, not the same thing as this effect's own (always-off) built-in lighting.
        /// </summary>
        public BasicEffect Effect => _effect;

        /// <summary>Vertices currently buffered and not yet flushed.</summary>
        public int PendingVertices => _vertexCount;

        /// <summary>Total vertex capacity before an automatic flush.</summary>
        public int Capacity => _maxVertices;

        /// <summary>Number of draw calls issued since the last <see cref="Begin(Camera3D,BlendState?,DepthStencilState?,RasterizerState?,Matrix?)"/>.</summary>
        public int DrawCalls { get; private set; }

        /// <summary>Total triangles submitted since the last <see cref="Begin(Camera3D,BlendState?,DepthStencilState?,RasterizerState?,Matrix?)"/>.</summary>
        public int TrianglesSubmitted { get; private set; }

        /// <summary>Total Lines submitted since the last <see cref="Begin(Camera3D,BlendState?,DepthStencilState?,RasterizerState?,Matrix?)"/>.</summary>
        public int LinesSubmitted { get; private set; }

        /// <summary>
        /// When <c>true</c>, line primitives are expanded into screen-facing quads
        /// (billboarded ribbons) which are noticeably smoother than 1px hardware
        /// lines and honour <see cref="DefaultLineThickness"/>. When <c>false</c>,
        /// lines are drawn as thin camera-facing quads of a fixed world width.
        /// </summary>
        public bool SmoothLines { get; set; } = true;

        /// <summary>
        /// Line thickness in *pixels* when <see cref="SmoothLines"/> is enabled,
        /// or in world units otherwise.
        /// </summary>
        public float DefaultLineThickness { get; set; } = 0.5f;

        /// <summary>Camera position used to orient line quads. Set by <see cref="Begin(Camera3D,BlendState?,DepthStencilState?,RasterizerState?,Matrix?)"/>.</summary>
        private Vector3 _cameraPosition;

        /// <summary>Approximate world-units-per-pixel scale factor at unit distance.</summary>
        private float _pixelScale = 0.002f;

        // ---------------------------------------------------------------------
        // Basic lighting (flat shading, no vertex format change)
        // ---------------------------------------------------------------------

        /// <summary>
        /// When enabled, filled surfaces (Cube/Sphere/Cylinder/Capsule/Torus/Plane/
        /// FillTriangle3D/FillCircle3D) are flat-shaded: each triangle/quad's own
        /// face normal (from its own points, via cross product — no stored per-vertex
        /// normals, so the vertex format stays untouched) is dotted against
        /// <see cref="LightDirection"/> and used to darken the face's color
        /// toward <see cref="AmbientLight"/>. Off by default — zero behavior change
        /// for existing callers. Lines, points and the grid are never shaded (a
        /// camera-facing line quad has no meaningful surface normal to light).
        /// </summary>
        public bool LightingEnabled { get; set; } = false;

        /// <summary>Direction the light travels (not the direction *to* the light). Normalized on set.</summary>
        public Vector3 LightDirection
        {
            get => _lightDirection;
            set => _lightDirection = value.LengthSquared() > 1e-12f ? Vector3.Normalize(value) : _lightDirection;
        }
        private Vector3 _lightDirection = Vector3.Normalize(new Vector3(-0.5f, -1f, -0.35f));

        /// <summary>
        /// Brightness floor for faces pointing away from the light, in [0, 1].
        /// 0 = fully black on the dark side; 1 = disables shading entirely (flat, unlit look).
        /// </summary>
        public float AmbientLight { get; set; } = 0.35f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Color Shade(in Vector3 normal, in Color color)
        {
            if (!LightingEnabled)
                return color;

            float nDotL = MathF.Max(0f, Vector3.Dot(normal, -_lightDirection));
            float lit = AmbientLight + (1f - AmbientLight) * nDotL;
            return new Color((byte)(color.R * lit + 0.5f), (byte)(color.G * lit + 0.5f), (byte)(color.B * lit + 0.5f), color.A);
        }

        /// <summary>Appends a lit quad: shades <paramref name="color"/> by the quad's own face normal (a·b × a·d) when <see cref="LightingEnabled"/> is on.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushQuadLit(in Vector3 a, in Vector3 b, in Vector3 c, in Vector3 d, in Color color)
        {
            if (!LightingEnabled) { PushQuad(a, b, c, d, color); return; }
            Vector3 normal = Vector3.Normalize(Vector3.Cross(b - a, d - a));
            PushQuad(a, b, c, d, Shade(normal, color));
        }

        /// <summary>Appends a lit triangle: shades <paramref name="color"/> by its own face normal when <see cref="LightingEnabled"/> is on.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushTriangleLit(in Vector3 a, in Vector3 b, in Vector3 c, in Color color)
        {
            if (!LightingEnabled) { PushTriangle(a, b, c, color); return; }
            Vector3 normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            PushTriangle(a, b, c, Shade(normal, color));
        }

        /// <summary>
        /// Appends a lit quad using an explicit, caller-supplied normal instead of deriving
        /// one from the quad's own edges. Needed wherever two of the four corners can
        /// coincide (a sphere/hemisphere pole ring) — the edge-cross-product normal goes
        /// zero-length there and <see cref="Vector3.Normalize(Vector3)"/> turns that into NaN, which
        /// then blackens the whole quad once it reaches <see cref="Shade"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushQuadLit(in Vector3 a, in Vector3 b, in Vector3 c, in Vector3 d, in Vector3 normal, in Color color)
        {
            if (!LightingEnabled) { PushQuad(a, b, c, d, color); return; }
            PushQuad(a, b, c, d, Shade(normal, color));
        }

        // ---------------------------------------------------------------------
        // Begin / End
        // ---------------------------------------------------------------------

        /// <summary>
        /// Starts a batch using a <see cref="Camera3D"/>. If <paramref name="camera"/> was
        /// constructed with a <see cref="ViewportAdapter2D"/> (<see cref="Camera3D.ViewportAdapter"/>),
        /// it's applied first — letterboxing/pillarboxing the 3D projection into
        /// <see cref="ViewportAdapter2D.BoundingRectangle"/> instead of the full backbuffer, the
        /// same way <see cref="Primitives2D.Camera2D"/> uses its own stored adapter. Without one,
        /// this always projects using the full window's aspect ratio — if the window is
        /// letterboxed (e.g. a fixed-aspect game inside a resizable window, sharing an adapter
        /// with 2D content), an adapter-less 3D scene would stretch into the bars instead of
        /// matching them.
        /// </summary>
        /// <param name="camera">Camera providing view and projection matrices.</param>
        /// <param name="blendState">Blend state, defaults to <see cref="BlendState.NonPremultiplied"/> — every color here is straight (non-premultiplied) RGBA, so this is the blend state that actually matches them.</param>
        /// <param name="depthStencilState">Depth state, defaults to <see cref="DepthStencilState.Default"/>.</param>
        /// <param name="rasterizerState">Rasterizer state, defaults to <see cref="RasterizerState.CullNone"/>.</param>
        /// <param name="transform">Optional additional world transform.</param>
        public void Begin(
            Camera3D camera,
            BlendState? blendState = null,
            DepthStencilState? depthStencilState = null,
            RasterizerState? rasterizerState = null,
            Matrix? transform = null)
        {
            if (camera is null) throw new ArgumentNullException(nameof(camera));

            camera.ViewportAdapter?.Apply();

            float aspect = _device.Viewport.AspectRatio;
            Matrix view = camera.GetViewMatrix();
            Matrix proj = camera.GetProjectionMatrix(aspect);

            BeginInternal(view, proj, camera.Position, camera.GetPixelScale(_device.Viewport.Height),
                          blendState, depthStencilState, rasterizerState, transform);
        }

        /// <summary>
        /// Starts a batch with explicit matrices, for interop with engines that
        /// already manage their own camera representation.
        /// </summary>
        public void Begin(
            in Matrix view,
            in Matrix projection,
            BlendState? blendState = null,
            DepthStencilState? depthStencilState = null,
            RasterizerState? rasterizerState = null,
            Matrix? transform = null)
        {
            // Extract camera position from the inverse view matrix.
            Matrix inv = Matrix.Invert(view);
            BeginInternal(view, projection, inv.Translation, 0.002f,
                          blendState, depthStencilState, rasterizerState, transform);
        }

        private void BeginInternal(
            in Matrix view, in Matrix projection, Vector3 cameraPosition, float pixelScale,
            BlendState? blendState, DepthStencilState? depthStencilState,
            RasterizerState? rasterizerState, Matrix? transform)
        {
            ThrowIfDisposed();
            if (_began)
                throw new InvalidOperationException("Begin cannot be called again until End has been called.");

            _began = true;
            _vertexCount = 0;
            _vertexCountLine = 0;
            DrawCalls = 0;
            TrianglesSubmitted = 0;

            _view = view;
            _projection = projection;
            _cameraPosition = cameraPosition;
            _pixelScale = pixelScale > 0f ? pixelScale : 0.002f;

            _hasTransform = transform.HasValue;
            _transform = transform ?? Matrix.Identity;

            // NonPremultiplied, not AlphaBlend: colors are pushed as straight RGBA everywhere in
            // this batch (see PushTriangleUnchecked/PushLine), never premultiplied first.
            // AlphaBlend expects the opposite and silently renders translucent draws far more
            // opaque than their alpha implies. Fully opaque colors are identical either way.
            _blendState = blendState ?? BlendState.NonPremultiplied;
            _depthStencilState = depthStencilState ?? DepthStencilState.Default;
            _rasterizerState = rasterizerState ?? RasterizerState.CullNone;

            // Capture existing state so it can be restored on End.
            _prevBlend = _device.BlendState;
            _prevDepth = _device.DepthStencilState;
            _prevRasterizer = _device.RasterizerState;
            _prevSampler0 = _device.SamplerStates[0];
            _prevVertexBuffer = null;
            _prevIndexBuffer = _device.Indices;

            ApplyDeviceState();
        }

        /// <summary>Flushes remaining geometry and restores the previous device state.</summary>
        public void End()
        {
            ThrowIfNotBegun();

            Flush();
            FlushLine();

            // Restore caller state.
            _device.BlendState = _prevBlend;
            _device.DepthStencilState = _prevDepth;
            _device.RasterizerState = _prevRasterizer;
            _device.SamplerStates[0] = _prevSampler0;
            _device.Indices = _prevIndexBuffer;
            _device.SetVertexBuffer(_prevVertexBuffer);

            _began = false;
        }

        /// <summary>
        /// Forces the currently buffered geometry to be submitted to the GPU.
        /// Called automatically when the buffer fills up and on <see cref="End"/>.
        /// </summary>
        public void Flush()
        {
            ThrowIfNotBegun();
            if (_vertexCount == 0)
                return;

            // Re-apply state in case the user changed the device between draws.
            ApplyDeviceState();

            _device.DrawUserPrimitives(PrimitiveType.TriangleList, _vertices, 0, _vertexCount / 3);

            DrawCalls++;
            TrianglesSubmitted += _vertexCount / 3;
            _vertexCount = 0;
        }

        /// <summary>
        /// Forces the currently buffered geometry to be submitted to the GPU.
        /// Called automatically when the buffer fills up and on <see cref="End"/>.
        /// </summary>
        public void FlushLine()
        {
            ThrowIfNotBegun();
            if (_vertexCountLine == 0)
                return;

            // Re-apply state in case the user changed the device between draws.
            ApplyDeviceState();

            _device.DrawUserPrimitives(PrimitiveType.LineList, _verticesLines, 0, _vertexCountLine / 2);

            DrawCalls++;
            LinesSubmitted += _vertexCountLine;
            _vertexCountLine = 0;
        }

        private void ApplyDeviceState()
        {
            _device.BlendState = _blendState;
            _device.DepthStencilState = _depthStencilState;
            _device.RasterizerState = _rasterizerState;

            _effect.View = _view;
            _effect.Projection = _projection;
            _effect.World = _hasTransform ? _transform : Matrix.Identity;
            _effect.CurrentTechnique.Passes[0].Apply();
        }

        // ---------------------------------------------------------------------
        // Vertex submission (hot path)
        // ---------------------------------------------------------------------

        /// <summary>
        /// Ensures room for <paramref name="requiredVertices"/> and flushes if needed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Reserve(int requiredVertices)
        {
            if (_vertexCount + requiredVertices > _maxVertices)
                Flush();
        }

        private void ReserveLine(int requiredVertices)
        {
            if (_vertexCountLine + requiredVertices > _maxVertices)
                FlushLine();
        }

        /// <summary>Appends a single triangle. Space must already be reserved.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushTriangleUnchecked(in Vector3 a, in Vector3 b, in Vector3 c, in Color color)
        {
            int i = _vertexCount;
            _vertices[i].Position = a; _vertices[i].Color = color;
            _vertices[i + 1].Position = b; _vertices[i + 1].Color = color;
            _vertices[i + 2].Position = c; _vertices[i + 2].Color = color;
            _vertexCount = i + 3;
        }

        /// <summary>Appends a triangle, flushing first if the buffer is full.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushTriangle(in Vector3 a, in Vector3 b, in Vector3 c, in Color color)
        {
            Reserve(3);
            PushTriangleUnchecked(a, b, c, color);
        }

        /// <summary>Appends a quad as two triangles (a-b-c-d in winding order).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushQuad(in Vector3 a, in Vector3 b, in Vector3 c, in Vector3 d, in Color color)
        {
            Reserve(6);
            PushTriangleUnchecked(a, b, c, color);
            PushTriangleUnchecked(a, c, d, color);
        }

        /// <summary>Appends a quad as two triangles (a-b-c-d in winding order).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushLine(in Vector3 a, in Vector3 b, in Color color)
        {
            _verticesLines[_vertexCountLine].Position = a;
            _verticesLines[_vertexCountLine].Color = color;
            _vertexCountLine++;

            _verticesLines[_vertexCountLine].Position = b;
            _verticesLines[_vertexCountLine].Color = color;
            _vertexCountLine++;
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        /// <summary>
        /// Resolves a user-supplied segment count. A value of <c>-1</c> triggers
        /// automatic level of detail based on <paramref name="radius"/> and the
        /// distance from the camera; the result is clamped to
        /// [<see cref="AutoSegmentsMin"/>, <see cref="AutoSegmentsMax"/>].
        /// </summary>
        /// <param name="requested">Requested segments, or -1 for automatic.</param>
        /// <param name="radius">Primitive radius in world units.</param>
        /// <param name="center">Primitive center, used for distance-based LOD.</param>
        /// <param name="fallback">Value used when <paramref name="requested"/> is 0.</param>
        public int ResolveSegments(int requested, float radius, in Vector3 center, int fallback)
        {
            if (requested > 0)
                return requested;
            if (requested == 0)
                return fallback;

            // Automatic: estimate the on-screen radius in pixels, then choose
            // enough segments so that each edge is roughly 6 px long.
            float distance = Vector3.Distance(center, _cameraPosition);
            if (distance < 0.0001f)
                distance = 0.0001f;

            float pixelRadius = Math.Abs(radius) / (distance * _pixelScale);
            int segments = (int)(MathF.Sqrt(pixelRadius) * 3f);

            return Math.Clamp(segments, AutoSegmentsMin, AutoSegmentsMax);
        }

        /// <summary>Same as above, but measures the automatic-LOD distance from the world origin instead of an explicit center — for a primitive whose exact center doesn't matter, or isn't known yet.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ResolveSegments(int requested, float radius, int fallback)
            => ResolveSegments(requested, radius, Vector3.Zero, fallback);

        /// <summary>
        /// Builds an orthonormal basis around <paramref name="normal"/>.
        /// Branch-free variant of Duff et al. to avoid degenerate cross products.
        /// </summary>
        internal static void BuildBasis(in Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
        {
            float sign = normal.Z >= 0f ? 1f : -1f;
            float a = -1f / (sign + normal.Z);
            float b = normal.X * normal.Y * a;

            tangent = new Vector3(1f + sign * normal.X * normal.X * a, sign * b, -sign * normal.X);
            bitangent = new Vector3(b, sign + normal.Y * normal.Y * a, -normal.Y);
        }

        private void ThrowIfNotBegun()
        {
            if (!_began)
                throw new InvalidOperationException("Begin must be called before drawing or ending the batch.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Primitive3DBatch));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
                return;
            _effect.Dispose();
            _disposed = true;
        }
    }
}