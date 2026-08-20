#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace MonoPrimitives.Primitives2D
{
    /// <summary>How consecutive segments of a thick outline meet at a vertex.</summary>
    public enum LineJoin
    {
        /// <summary>Segments are extended to a sharp point (clamped on very sharp turns). Cheapest, current default.</summary>
        Miter,
        /// <summary>The gap on the outer side of the turn is filled with a circular arc.</summary>
        Round,
        /// <summary>The gap on the outer side of the turn is filled with a single flat-cut triangle.</summary>
        Bevel
    }

    /// <summary>How the free end of an open (non-closed) thick outline is finished.</summary>
    public enum LineCap
    {
        /// <summary>Flat cut exactly at the endpoint. Current default.</summary>
        Butt,
        /// <summary>A half-circle bulging outward past the endpoint.</summary>
        Round
    }

    /// <summary>
    /// A value assigned per rectangle corner (top-left, top-right, bottom-right,
    /// bottom-left going clockwise), used for both rounded-rectangle corner radius and
    /// chamfered-rectangle corner cut size — same shape of value, meaning depends on
    /// which method it's passed to. Implicitly convertible from a single <see cref="float"/>
    /// to apply the same value to all four corners, so the common case stays simple:
    /// <c>FillRectangleRounded(rect, 12f, color)</c> rounds every corner by 12px.
    /// </summary>
    public struct RectCorners
    {
        public float TopLeft;
        public float TopRight;
        public float BottomRight;
        public float BottomLeft;

        public RectCorners(float all)
        {
            TopLeft = TopRight = BottomRight = BottomLeft = all;
        }

        public RectCorners(float topLeft, float topRight, float bottomRight, float bottomLeft)
        {
            TopLeft = topLeft;
            TopRight = topRight;
            BottomRight = bottomRight;
            BottomLeft = bottomLeft;
        }

        public static implicit operator RectCorners(float uniform) => new RectCorners(uniform);
    }

    /// <summary>
    /// Immediate-mode 2D primitive batcher with a SpriteBatch-like API.
    ///
    /// All primitives are triangulated and accumulated into a single vertex/index
    /// buffer, then submitted in as few draw calls as possible on <see cref="End"/>.
    ///
    /// Design notes:
    ///  - Zero per-frame heap allocations. Buffers are allocated once in the constructor.
    ///  - No trigonometry at draw time: a unit-circle lookup table is built once.
    ///  - Device state is fully saved on Begin and restored on End.
    /// </summary>
    public sealed class PrimitiveBatch : IDisposable
    {
        // ------------------------------------------------------------------
        // Tunables
        // ------------------------------------------------------------------

        /// <summary>Resolution of the precomputed unit-circle table. Higher = smoother max quality.</summary>
        private const int UnitCircleResolution = 512;

        /// <summary>Minimum segments used for any arc/circle, regardless of radius.</summary>
        private const int MinCircleSegments = 48;

        /// <summary>Maximum segments used for a full circle.</summary>
        public const int MaxCircleSegments = 128 * 4;

        /// <summary>
        /// Largest element count any single call stack-allocates a point/joint buffer for.
        /// Point-list methods (Poly/Polygon borders and Round-joined fills, line-strip round
        /// joins, splines) size their buffer off a caller-controlled count (sides, points.Length,
        /// segmentsPerPiece) with no upper bound of their own — above this threshold they
        /// allocate on the heap instead so a pathological input degrades to a GC allocation
        /// rather than an uncatchable <see cref="StackOverflowException"/>. 4096 elements is
        /// ~32KB per <see cref="Vector2"/> buffer, comfortably inside a default 1MB thread stack
        /// even with several such calls nested.
        /// </summary>
        private const int MaxStackAllocElements = 4096;

        /// <summary>
        /// Passed as <see cref="BuildRoundedCornerBoundary"/>'s <c>innerOffsetForClamp</c> by the
        /// standalone <c>Fill*Rounded</c> methods (no accompanying border, so there's no border
        /// thickness to pass instead). Deliberately large rather than 0: <see cref="ComputeJoint"/>
        /// derives its radius clamp from <c>innerOffset * cosHalfAngle</c>, so passing the true
        /// "no border" value (0) would clamp every rounded corner down to a hard 0 regardless of
        /// how much room the triangle/polygon actually has — the opposite of what's wanted. A
        /// large value instead makes that term always non-binding, leaving <see cref="ComputeJoint"/>'s
        /// *other* clamp (radius can't trim more than 90% of either adjacent edge) as the only
        /// real constraint, which is exactly the right one for a fill with no border at all.
        /// </summary>
        private const float NoBorderClampBudget = 1e6f;

        /// <summary>
        /// Shrinks <paramref name="requestedRadius"/> so no two ADJACENT corners' fillets can
        /// ever claim more than their shared edge actually has. <see cref="ComputeJoint"/>
        /// already clamps each corner's own trim distance to ≤90% of its own view of an edge —
        /// correct in isolation, but that's a per-corner budget, not a per-edge one: two
        /// neighboring corners each independently claiming up to 90% of the SAME shared edge can
        /// together claim up to 180% of it, so their tangent points cross past each other and the
        /// fillet self-intersects. Verified by rendering exactly this: a triangle with ~60°
        /// angles and ~70px edges, given a 25px corner radius, produced visible self-intersecting
        /// spikes at every corner — each corner's own 90%-of-edge clamp was satisfied, but the
        /// two corners on each edge together wanted ~137% of it. This never showed up on the
        /// existing <c>Border*</c>/<c>Draw*</c>'s Round-joined corners because those couple the
        /// radius to a border <c>thickness</c> that's typically small next to the shape itself,
        /// so <see cref="ComputeJoint"/>'s OTHER clamp (against that thickness) was already the
        /// binding one before this per-edge conflict could ever occur — the standalone
        /// <c>Fill*Rounded</c>/<c>*Rounded</c> methods (no border thickness to lean on, and often
        /// a deliberately generous radius) are the first case this codebase has where it can. Uses
        /// the plain isoceles fillet formula (tangent distance = radius / tan(halfAngle)) per
        /// vertex rather than duplicating <see cref="ComputeJoint"/>'s own exact bisector-based
        /// trim slope — a close, slightly conservative match for a regular convex vertex, which
        /// errs toward a smaller-than-strictly-necessary safe radius rather than an unsafe one.
        /// </summary>
        private static float ClampCornerRadiusToFit(ReadOnlySpan<Vector2> points, float requestedRadius)
        {
            int n = points.Length;
            if (requestedRadius <= 0.01f || n < 3) return requestedRadius;

            Span<float> tangentPerUnitRadius = n <= 256 ? stackalloc float[n] : new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = points[(i - 1 + n) % n];
                Vector2 curr = points[i];
                Vector2 next = points[(i + 1) % n];
                Vector2 v1 = SafeNormalize(prev - curr);
                Vector2 v2 = SafeNormalize(next - curr);
                if (v1 == Vector2.Zero || v2 == Vector2.Zero) { tangentPerUnitRadius[i] = 0f; continue; }

                float cosAngle = Math.Clamp(Vector2.Dot(v1, v2), -1f, 1f);
                float halfAngle = MathF.Max(MathF.Acos(cosAngle) * 0.5f, 0.01f);
                float tanHalf = MathF.Tan(halfAngle);
                tangentPerUnitRadius[i] = tanHalf > 1e-4f ? 1f / tanHalf : 0f;
            }

            float maxRadius = requestedRadius;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float combined = tangentPerUnitRadius[i] + tangentPerUnitRadius[j];
                if (combined <= 1e-6f) continue;
                float edgeSafeRadius = Vector2.Distance(points[i], points[j]) / combined;
                if (edgeSafeRadius < maxRadius) maxRadius = edgeSafeRadius;
            }
            return MathF.Max(0f, maxRadius);
        }

        /// <summary>Same purpose as <see cref="MaxStackAllocElements"/>, sized for the larger <see cref="JointInfo"/> struct instead of <see cref="Vector2"/> (~30KB budget either way).</summary>
        private const int MaxStackAllocJoints = 512;

        // ------------------------------------------------------------------
        // Precomputed unit circle (static: shared by every instance)
        // ------------------------------------------------------------------

        private static readonly Vector2[] UnitCircle = BuildUnitCircle();

        private static Vector2[] BuildUnitCircle()
        {
            Debug.Assert((UnitCircleResolution & (UnitCircleResolution - 1)) == 0,
                "UnitCircleResolution must be a power of two — SampleUnitCircle's bitwise-AND wrap depends on it.");

            var table = new Vector2[UnitCircleResolution];
            const double step = Math.PI * 2.0 / UnitCircleResolution;
            for (int i = 0; i < UnitCircleResolution; i++)
            {
                table[i] = new Vector2((float)Math.Cos(i * step), (float)Math.Sin(i * step));
            }
            return table;
        }

        /// <summary>
        /// Samples the unit circle table at a normalized angle [0..1) with linear
        /// interpolation. Trig-free; accurate to well under a pixel at any sane radius.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector2 SampleUnitCircle(float t01)
        {
            float f = t01 * UnitCircleResolution;
            int i0 = (int)f;
            float frac = f - i0;

            i0 &= UnitCircleResolution - 1;               // resolution is a power of two
            int i1 = (i0 + 1) & (UnitCircleResolution - 1);

            Vector2 a = UnitCircle[i0];
            Vector2 b = UnitCircle[i1];
            return new Vector2(a.X + (b.X - a.X) * frac,
                               a.Y + (b.Y - a.Y) * frac);
        }

        /// <summary>
        /// Chooses a segment count appropriate for the given radius and sweep.
        /// Small shapes get fewer segments; the chord error stays below ~0.25 px only
        /// up to the radius where the formula would need more than <see cref="MaxCircleSegments"/>
        /// (≈60px for a full circle). Beyond that radius the segment count stays capped at
        /// <see cref="MaxCircleSegments"/> and the error grows roughly linearly with radius
        /// instead (≈1px at r=200, ≈5px at r=1000) — very large circles/arcs will look
        /// visibly faceted. Raise <see cref="MaxCircleSegments"/> if smoother large circles
        /// are needed; this trades off against per-shape triangle count.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SegmentsForArc(float radius, float sweepRadians)
        {
            if (radius <= 0f) return MinCircleSegments;

            // Chord-error bound: for max deviation e, segments >= sweep / (2*acos(1 - e/r)).
            // Approximated cheaply as proportional to sqrt(radius) * sweep.
            float fullCircleSegments = 4f + 2f * MathF.Sqrt(radius) * 4f;
            if (fullCircleSegments < MinCircleSegments) fullCircleSegments = MinCircleSegments;
            float scaled = fullCircleSegments * (Math.Abs(sweepRadians) / MathHelper.TwoPi);

            int segments = (int)(scaled + 0.5f);
            if (segments < MinCircleSegments) segments = MinCircleSegments;
            if (segments > MaxCircleSegments) segments = MaxCircleSegments;
            return segments;
        }

        // ------------------------------------------------------------------
        // Batch storage
        // ------------------------------------------------------------------

        private readonly GraphicsDevice _device;
        private readonly BasicEffect _effect;

        private VertexPositionColor[] _vertices;
        private int[] _indices;
        private int _vertexCount;
        private int _indexCount;

        private VertexPositionColor[] _verticesPoint;
        private int _vertexCountPoint;

        /// <summary>Sequential 0,1,2,3... index buffer for point primitives, built once and reused.</summary>
        private int[] _pointIndices;

        // ------------------------------------------------------------------
        // Begin/End state
        // ------------------------------------------------------------------

        private bool _isStarted;

        // Saved device state, restored on End(). Null until the first Begin() call.
        private BlendState? _savedBlend;
        private DepthStencilState? _savedDepth;
        private RasterizerState? _savedRasterizer;
        private SamplerState? _savedSampler;

        private BlendState? _activeBlend;
        private DepthStencilState? _activeDepth;
        private RasterizerState? _activeRasterizer;
        private Effect? _customEffect;

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        /// <summary>
        /// Creates a primitive batcher.
        /// </summary>
        /// <param name="device">Graphics device to render with.</param>
        /// <param name="initialVertexCapacity">
        /// Fixed vertex/index buffer capacity. This does NOT grow automatically: once
        /// full, the batch flushes what it already has, but a single shape that alone
        /// needs more vertices/indices than this capacity will still fail. Raise this
        /// value if you draw very large single shapes (e.g. big polygons or splines).
        /// </param>
        public PrimitiveBatch(GraphicsDevice device, int initialVertexCapacity = 32768)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));

            _vertices = new VertexPositionColor[Math.Max(64, initialVertexCapacity)];
            _verticesPoint = new VertexPositionColor[Math.Max(64, initialVertexCapacity)];
            _indices = new int[Math.Max(96, initialVertexCapacity * 3)];

            // Point indices are always sequential (0,1,2,...), so this is built once
            // and reused for every FlushPoint() call instead of allocating per flush.
            _pointIndices = new int[_verticesPoint.Length];
            for (int i = 0; i < _pointIndices.Length; i++)
                _pointIndices[i] = i;

            _effect = new BasicEffect(device)
            {
                VertexColorEnabled = true,
                TextureEnabled = false,
                LightingEnabled = false,
                World = Matrix.Identity
            };
        }

        // ------------------------------------------------------------------
        // Begin / End
        // ------------------------------------------------------------------

        /// <summary>
        /// Begins a primitive batch using a pixel-space projection derived from the
        /// current viewport (origin top-left, Y down), matching SpriteBatch conventions.
        /// </summary>
        public void Begin(
            Matrix? transformMatrix = null,
            BlendState? blendState = null,
            DepthStencilState? depthStencilState = null,
            RasterizerState? rasterizerState = null,
            Effect? effect = null)
        {
            Viewport vp = _device.Viewport;

            Matrix projection = Matrix.CreateOrthographicOffCenter(
                0f, vp.Width, vp.Height, 0f, 0f, 1f);

            Begin(transformMatrix ?? Matrix.Identity, projection,
                  blendState, depthStencilState, rasterizerState, effect);
        }

        /// <summary>
        /// Begins a primitive batch with an explicit view and projection matrix.
        /// Use this to render through a custom camera.
        /// </summary>
        /// <param name="view">View (camera) matrix.</param>
        /// <param name="projection">Projection matrix.</param>
        /// <param name="blendState">Defaults to <see cref="BlendState.NonPremultiplied"/> — every color in this batch is a straight (non-premultiplied) RGBA value, so this is the blend state that actually matches them.</param>
        /// <param name="depthStencilState">Defaults to <see cref="DepthStencilState.None"/>.</param>
        /// <param name="rasterizerState">Defaults to <see cref="RasterizerState.CullNone"/>.</param>
        /// <param name="effect">Optional replacement effect. Must accept VertexPositionColor.</param>
        public void Begin(
            Matrix view,
            Matrix projection,
            BlendState? blendState = null,
            DepthStencilState? depthStencilState = null,
            RasterizerState? rasterizerState = null,
            Effect? effect = null)
        {
            if (_isStarted)
                throw new InvalidOperationException("Begin cannot be called again until End has been called.");

            // Save everything we are about to touch.
            _savedBlend = _device.BlendState;
            _savedDepth = _device.DepthStencilState;
            _savedRasterizer = _device.RasterizerState;
            _savedSampler = _device.SamplerStates[0];

            // NonPremultiplied, not AlphaBlend: every color here (user-supplied or internal) is
            // constructed as straight RGBA (e.g. `new Color(r, g, b, a)`), never premultiplied
            // before reaching the vertex buffer. AlphaBlend's blend factors assume the opposite
            // (source RGB already scaled by its own alpha) — fed a straight color instead, a
            // translucent draw comes out far more opaque than its alpha implies (verified: a
            // 15%-alpha color over a dark background blended to ~82% instead of ~18%). Fully
            // opaque colors (alpha=255, the common case) are unaffected either way.
            _activeBlend = blendState ?? BlendState.NonPremultiplied;
            _activeDepth = depthStencilState ?? DepthStencilState.None;
            _activeRasterizer = rasterizerState ?? RasterizerState.CullNone;
            _customEffect = effect;

            _effect.View = view;
            _effect.Projection = projection;

            _vertexCount = 0;
            _indexCount = 0;
            _vertexCountPoint = 0;
            _isStarted = true;
        }

        /// <summary>
        /// Flushes all batched geometry and restores the device state captured by Begin.
        /// </summary>
        public void End()
        {
            EnsureStarted();
            Flush();
            FlushPoint();

            _device.BlendState = _savedBlend;
            _device.DepthStencilState = _savedDepth;
            _device.RasterizerState = _savedRasterizer;
            _device.SamplerStates[0] = _savedSampler;

            _isStarted = false;
        }

        /// <summary>
        /// Submits everything accumulated so far without ending the batch.
        /// Called automatically when buffers fill up.
        /// </summary>
        public void Flush()
        {
            if (_indexCount == 0)
            {
                _vertexCount = 0;
                return;
            }

            _device.BlendState = _activeBlend;
            _device.DepthStencilState = _activeDepth;
            _device.RasterizerState = _activeRasterizer;

            Effect effect = _customEffect ?? _effect;
            effect.CurrentTechnique.Passes[0].Apply();

            _device.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                _vertices, 0, _vertexCount,
                _indices, 0, _indexCount / 3);


            _vertexCount = 0;
            _indexCount = 0;
        }

        void FlushPoint()
        {
            if (_vertexCountPoint == 0)
                return;

            // reset device
            _device.BlendState = _activeBlend;
            _device.DepthStencilState = _activeDepth;
            _device.RasterizerState = _activeRasterizer;
            Effect effect = _customEffect ?? _effect;
            effect.CurrentTechnique.Passes[0].Apply();

            // NOTE: PrimitiveType.PointListEXT is NOT part of stock MonoGame (DesktopGL /
            // WindowsDX NuGet packages) — it requires a MonoGame build/fork with GL_POINTS
            // support added to PrimitiveType. If this project ever moves to stock MonoGame,
            // this line will fail to compile; DrawPixelFast/FlushPoint would need to fall
            // back to something else (e.g. 1x1 quads via DrawPixel) in that case.
            _device.DrawUserIndexedPrimitives(
                    PrimitiveType.PointList,
                    _verticesPoint,
                    0, _vertexCountPoint,
                    _pointIndices,
                    0, _vertexCountPoint);

            _vertexCountPoint = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureStarted()
        {
            if (!_isStarted)
                throw new InvalidOperationException("Begin must be called before drawing.");
        }

        // ------------------------------------------------------------------
        // Low-level geometry submission
        // ------------------------------------------------------------------

        /// <summary>
        /// Reserves room for the given vertex/index counts, flushing the batch first if
        /// either buffer would overflow. Buffer capacity is fixed (set at construction)
        /// and is never grown; a single reservation larger than that capacity will still
        /// overflow after the flush. Also flushes any pending <see cref="DrawPixelFast"/>
        /// points first, so interleaved calls keep their draw order (see <see cref="PushVertexPoint"/>).
        /// Returns the base vertex index that reserved vertices start at.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Reserve(int vertexCount, int indexCount)
        {
            // Preserve call order: any points queued before this shape must render first.
            if (_vertexCountPoint > 0)
                FlushPoint();

            if (_vertexCount + vertexCount > _vertices.Length ||
                _indexCount + indexCount > _indices.Length)
                Flush();

            return _vertexCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushVertex(float x, float y, Color color)
        {
            _vertices[_vertexCount].Position.X = x;
            _vertices[_vertexCount].Position.Y = y;
            _vertices[_vertexCount].Position.Z = 0f;
            _vertices[_vertexCount].Color = color;
            _vertexCount++;
        }

        /// <summary>
        /// Pushes a point into the separate point batch. To preserve draw order relative
        /// to triangle-based shapes, any pending triangle geometry queued before this
        /// point is flushed first (mirrors the symmetric check in <see cref="Reserve"/>).
        /// </summary>
        private void PushVertexPoint(Vector2 position, Color color)
        {
            // Preserve call order: any triangles queued before this point must render first.
            if (_indexCount > 0)
                Flush();

            if (_vertexCountPoint + 1 > _verticesPoint.Length)
            {
                FlushPoint();
            }

            _verticesPoint[_vertexCountPoint].Position.X = position.X;
            _verticesPoint[_vertexCountPoint].Position.Y = position.Y;
            _verticesPoint[_vertexCountPoint].Position.Z = 0;
            _verticesPoint[_vertexCountPoint].Color = color;

            _vertexCountPoint++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushVertex(Vector2 p, Color color) => PushVertex(p.X, p.Y, color);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushTriangleIndices(int a, int b, int c)
        {
            _indices[_indexCount++] = a;
            _indices[_indexCount++] = b;
            _indices[_indexCount++] = c;
        }

        /// <summary>Emits a quad from four corner vertices already pushed, in order.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushQuadIndices(int baseIndex)
        {
            PushTriangleIndices(baseIndex + 0, baseIndex + 1, baseIndex + 2);
            PushTriangleIndices(baseIndex + 0, baseIndex + 2, baseIndex + 3);
        }

        /// <summary>
        /// Normalizes a vector, returning <see cref="Vector2.Zero"/> instead of NaN when
        /// the input is (near) zero-length. Used by the polygon/polyline outline builders
        /// so duplicate consecutive points degrade gracefully instead of poisoning the
        /// whole batch with NaN vertices.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector2 SafeNormalize(Vector2 v)
        {
            float lengthSq = v.LengthSquared();
            if (lengthSq < 1e-12f) return Vector2.Zero;
            return v / MathF.Sqrt(lengthSq);
        }

        // ==================================================================
        // POINTS
        // ==================================================================

        /// <summary>Draws a single pixel as a 1x1 quad. Prefer batching many via other shapes.</summary>
        public void DrawPixel(Vector2 position, Color color)
            => FillRectangle(position.X, position.Y, 1f, 1f, color);

        /// <inheritdoc cref="DrawPixel(Vector2,Color)"/>
        public void DrawPixel(float x, float y, Color color)
            => FillRectangle(x, y, 1f, 1f, color);

        /// <summary>
        /// Draws a single native GPU point primitive, batched separately from triangle-based
        /// shapes for efficiency. Draw order relative to other primitives is preserved:
        /// pushing a point flushes any pending triangle geometry first, and vice versa.
        /// </summary>
        public void DrawPixelFast(Vector2 position, Color color)
        {
            EnsureStarted();
            PushVertexPoint(position, color);
        }

        /// <summary>Draws a square point of the given size, centered on <paramref name="position"/>.</summary>
        public void DrawPoint(Vector2 position, float size, Color color)
        {
            float h = size * 0.5f;
            FillRectangle(position.X - h, position.Y - h, size, size, color);
        }


        // ==================================================================
        // LINES
        // ==================================================================

        /// <summary>Draws a 1px-wide line.</summary>
        public void DrawLine(Vector2 start, Vector2 end, Color color)
            => DrawLine(start, end, 1f, color);

        /// <inheritdoc cref="DrawLine(Vector2,Vector2,Color)"/>
        public void DrawLine(float x1, float y1, float x2, float y2, Color color)
            => DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), 1f, color);

        /// <inheritdoc cref="DrawLine(Vector2,Vector2,float,Color)"/>
        public void DrawLine(float x1, float y1, float x2, float y2, float thickness, Color color)
            => DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), thickness, color);

        /// <summary>Draws a line of the given thickness as a quad.</summary>
        public void DrawLine(Vector2 start, Vector2 end, float thickness, Color color)
            => DrawLine(start, end, thickness, color, LineCap.Butt);

        /// <inheritdoc cref="DrawLine(Vector2,Vector2,float,Color)"/>
        /// <param name="cap">
        /// <see cref="LineCap.Round"/> adds a half-circle bulging past each endpoint,
        /// instead of the default flat (<see cref="LineCap.Butt"/>) cut.
        /// </param>
        public void DrawLine(Vector2 start, Vector2 end, float thickness, Color color, LineCap cap)
        {
            EnsureStarted();

            float dx = end.X - start.X;
            float dy = end.Y - start.Y;
            if (dx * dx + dy * dy < 1e-12f) return;

            float half = thickness * 0.5f;
            EmitLineSegment(start, end, half, color);

            if (cap == LineCap.Round)
            {
                Vector2 dir = SafeNormalize(end - start);
                EmitRoundCap(start, dir * -1f, half, color);
                EmitRoundCap(end, dir, half, color);
            }
        }

        /// <summary>
        /// Emits a single straight, flat-capped (butt) thick-line quad between two points.
        /// Shared by <see cref="DrawLine(Vector2,Vector2,float,Color,LineCap)"/> and the
        /// per-segment bodies of <see cref="BuildRoundedOutlineGeometry"/>. Caller must have
        /// already validated <paramref name="start"/>/<paramref name="end"/> aren't coincident.
        /// </summary>
        private void EmitLineSegment(Vector2 start, Vector2 end, float half, Color color)
        {
            float dx = end.X - start.X;
            float dy = end.Y - start.Y;
            float invLength = 1f / MathF.Sqrt(dx * dx + dy * dy);
            // Perpendicular unit vector.
            float nx = -dy * invLength;
            float ny = dx * invLength;

            int b = Reserve(4, 6);
            PushVertex(start.X + nx * half, start.Y + ny * half, color);
            PushVertex(end.X + nx * half, end.Y + ny * half, color);
            PushVertex(end.X - nx * half, end.Y - ny * half, color);
            PushVertex(start.X - nx * half, start.Y - ny * half, color);
            PushQuadIndices(b);
        }

        /// <summary>Draws a connected sequence of line segments.</summary>
        public void DrawLineStrip(ReadOnlySpan<Vector2> points, Color color)
            => DrawLineStrip(points, 1f, color);

        /// <inheritdoc cref="DrawLineStrip(ReadOnlySpan{Vector2},Color)"/>
        /// <param name="join">How interior vertices are joined. Ignored for a 2-point strip.</param>
        /// <param name="cap">How the two free ends are finished.</param>
        /// <param name="jointRadius">
        /// Radius of <see cref="LineJoin.Round"/>/<see cref="LineJoin.Bevel"/> joins. Null
        /// (default) matches <paramref name="thickness"/>/2, flush with the line. Ignored for
        /// <see cref="LineJoin.Miter"/>.
        /// </param>
        public void DrawLineStrip(ReadOnlySpan<Vector2> points, float thickness, Color color,
            LineJoin join = LineJoin.Miter, LineCap cap = LineCap.Butt, float? jointRadius = null)
        {
            EnsureStarted();
            if (points.Length < 2) return;
            BuildOutlineGeometry(points, thickness, color, closed: false, join, cap, jointRadius);
        }

        /// <summary>Draws a dashed line.</summary>
        public void DrawLineDashed(Vector2 start, Vector2 end, float dashLength, float gapLength, Color color)
            => DrawLineDashed(start, end, dashLength, gapLength, 1f, color);

        /// <inheritdoc cref="DrawLineDashed(Vector2,Vector2,float,float,Color)"/>
        public void DrawLineDashed(Vector2 start, Vector2 end, float dashLength, float gapLength, float thickness, Color color)
        {
            if (dashLength <= 0f || gapLength < 0f) return;

            Vector2 delta = end - start;
            float total = delta.Length();
            if (total < 1e-6f) return;

            Vector2 dir = delta / total;
            float period = dashLength + gapLength;
            float travelled = 0f;

            while (travelled < total)
            {
                float segEnd = Math.Min(travelled + dashLength, total);
                DrawLine(start + dir * travelled, start + dir * segEnd, thickness, color);
                travelled += period;
            }
        }

        /// <summary>Draws an arrow from <paramref name="start"/> to <paramref name="end"/> — a line shaft plus a triangular head, sized automatically from the arrow's own length and <paramref name="thickness"/>.</summary>
        public void DrawArrow(Vector2 start, Vector2 end, Color color, float thickness = 2f)
            => DrawArrow(start, end, thickness, color, null, null);

        /// <summary>
        /// Draws an arrow with an explicit head size. The head's length/width scale with both
        /// the arrow's own length and <paramref name="thickness"/> by default, capped so a short
        /// arrow doesn't grow a head bigger than the arrow itself; pass <paramref name="headLength"/>/
        /// <paramref name="headWidth"/> to size it exactly instead.
        /// </summary>
        public void DrawArrow(Vector2 start, Vector2 end, float thickness, Color color, float? headLength, float? headWidth)
        {
            EnsureStarted();
            Vector2 delta = end - start;
            float len = delta.Length();
            if (len < 1e-6f)
                return;
            Vector2 dir = delta / len;
            Vector2 normal = new(-dir.Y, dir.X);

            // Min, not Max: the head should scale with the shaft's own thickness, only shrinking
            // further (never growing) when the arrow is too short for that to fit — a long thin
            // arrow must not grow a disproportionately huge head just because it's long.
            float hl = MathF.Min(headLength ?? MathF.Min(thickness * 4f, len * 0.3f), len);
            float hw = MathF.Min(headWidth ?? MathF.Max(thickness * 2.5f, hl * 0.7f), len);

            Vector2 headBase = end - dir * hl;
            if (hl < len)
                DrawLine(start, headBase, thickness, color);
            FillTriangle(end, headBase + normal * hw * 0.5f, headBase - normal * hw * 0.5f, color);
        }

        // ==================================================================
        // TRIANGLES
        // ==================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector2 RotateAroundPivot(Vector2 p, Vector2 pivot, float cos, float sin)
        {
            Vector2 d = p - pivot;
            return pivot + new Vector2(d.X * cos - d.Y * sin, d.X * sin + d.Y * cos);
        }

        /// <summary>
        /// Rotates the three vertices in place about <paramref name="origin"/> (the triangle's
        /// own centroid if null) by <paramref name="rotation"/> radians — a no-op when 0. Called
        /// once at the top of every public Triangle method that takes explicit vertices, so
        /// <paramref name="rotation"/> is a trailing optional parameter instead of a second
        /// overload; internal calls between Triangle methods always pass the result forward
        /// already rotated, never re-rotating.
        /// </summary>
        private static void RotateTriangle(ref Vector2 v1, ref Vector2 v2, ref Vector2 v3, float rotation, Vector2? origin)
        {
            if (rotation == 0f) return;
            Vector2 pivot = origin ?? (v1 + v2 + v3) / 3f;
            float cos = MathF.Cos(rotation), sin = MathF.Sin(rotation);
            v1 = RotateAroundPivot(v1, pivot, cos, sin);
            v2 = RotateAroundPivot(v2, pivot, cos, sin);
            v3 = RotateAroundPivot(v3, pivot, cos, sin);
        }

        /// <summary>Draws a filled triangle, no border. Winding does not matter (culling is disabled).</summary>
        public void FillTriangle(Vector2 v1, Vector2 v2, Vector2 v3, Color color, float rotation = 0f, Vector2? origin = null)
        {
            RotateTriangle(ref v1, ref v2, ref v3, rotation, origin);
            EnsureStarted();
            int b = Reserve(3, 3);
            PushVertex(v1, color);
            PushVertex(v2, color);
            PushVertex(v3, color);
            PushTriangleIndices(b, b + 1, b + 2);
        }

        /// <summary>Draws a filled triangle with per-vertex colors.</summary>
        public void FillTriangle(Vector2 v1, Color c1, Vector2 v2, Color c2, Vector2 v3, Color c3, float rotation = 0f, Vector2? origin = null)
        {
            RotateTriangle(ref v1, ref v2, ref v3, rotation, origin);
            EnsureStarted();
            int b = Reserve(3, 3);
            PushVertex(v1, c1);
            PushVertex(v2, c2);
            PushVertex(v3, c3);
            PushTriangleIndices(b, b + 1, b + 2);
        }

        /// <summary>
        /// Draws a triangle's border only, growing inward from its own edges — the border never
        /// extends outside the triangle. For <see cref="LineJoin.Miter"/> (default), pre-insets
        /// via <see cref="InsetConvexPolygon"/> then strokes symmetrically (cheap, exact, since
        /// Miter's offset is linear and self-cancels). For Round/Bevel, strokes the true vertices
        /// directly with an inward-only stroke instead — see <see cref="BuildRoundedOutlineGeometry"/>'s
        /// <c>inwardOnly</c> doc for why pre-insetting first would be wrong there, and see
        /// <see cref="DrawTriangle(Vector2,Vector2,Vector2,Color,Color,float,float,Vector2?,LineJoin,float?)"/>
        /// for how the fill is kept from overflowing this border's rounded corners.
        /// </summary>
        public void BorderTriangle(Vector2 v1, Vector2 v2, Vector2 v3, Color color, float thickness = 1f, float rotation = 0f, Vector2? origin = null, LineJoin join = LineJoin.Miter, float? jointRadius = null)
        {
            if (thickness <= 0f) return;
            RotateTriangle(ref v1, ref v2, ref v3, rotation, origin);
            EnsureStarted();
            Span<Vector2> pts = [v1, v2, v3];

            if (join == LineJoin.Miter)
            {
                Span<Vector2> inset = stackalloc Vector2[3];
                InsetConvexPolygon(pts, thickness * 0.5f, inset, closed: true);
                BuildOutlineGeometry(inset, thickness, color, closed: true, join);
                return;
            }

            BuildOutlineGeometry(pts, thickness, color, closed: true, join, LineCap.Butt, jointRadius, inwardOnly: true);
        }

        /// <summary>Draws a triangle with both fill and border (same color), border growing inward.</summary>
        public void DrawTriangle(Vector2 v1, Vector2 v2, Vector2 v3, Color color, float thickness = 1f, float rotation = 0f, Vector2? origin = null, LineJoin join = LineJoin.Miter, float? jointRadius = null)
            => DrawTriangle(v1, v2, v3, color, color, thickness, rotation, origin, join, jointRadius);

        /// <summary>
        /// Draws a triangle with an independently colored fill and border, border growing inward.
        /// For Round/Bevel <paramref name="join"/> with <paramref name="thickness"/> &gt; 0, the
        /// fill is rounded off the same way <see cref="BorderTriangle"/>'s inward-only stroke
        /// rounds its own outer edge (same <see cref="ComputeJoint"/> call, same clamp against
        /// <paramref name="thickness"/> via <see cref="BuildRoundedCornerBoundary"/>) so the two
        /// always agree exactly — no sliver of sharp-cornered fill left showing past a rounded
        /// border corner, and no mismatch even when the requested radius has to be clamped down.
        /// </summary>
        public void DrawTriangle(Vector2 v1, Vector2 v2, Vector2 v3, Color fillColor, Color borderColor, float thickness = 1f, float rotation = 0f, Vector2? origin = null, LineJoin join = LineJoin.Miter, float? jointRadius = null)
        {
            RotateTriangle(ref v1, ref v2, ref v3, rotation, origin);
            if (join == LineJoin.Miter || !jointRadius.HasValue || jointRadius.Value <= 0f || thickness <= 0f)
            {
                FillTriangle(v1, v2, v3, fillColor);
            }
            else
            {
                EnsureStarted();
                Span<Vector2> pts = [v1, v2, v3];
                Span<Vector2> boundary = stackalloc Vector2[3 * (MaxCircleSegments + 1)];
                int count = BuildRoundedCornerBoundary(pts, jointRadius.Value, join, thickness, boundary);
                FillPolygon(boundary[..count], fillColor);
            }
            if (thickness > 0f) BorderTriangle(v1, v2, v3, borderColor, thickness, join: join, jointRadius: jointRadius);
        }

        /// <summary>
        /// Draws a filled triangle with each corner rounded by <paramref name="cornerRadius"/>
        /// world units, no border pass at all — the standalone counterpart to calling
        /// <see cref="DrawTriangle(Vector2,Vector2,Vector2,Color,float,float,Vector2?,LineJoin,float?)"/>
        /// with a matching fill/border color just to get a rounded fill: same rounded geometry,
        /// without the redundant border stroke. Each corner's radius is independently clamped so
        /// it can't reach past the midpoint of either adjacent edge (no self-intersection on a
        /// small or sharp triangle) — same clamp <see cref="BuildRoundedCornerBoundary"/> already
        /// uses for a Draw* call's rounded fill, just without a border thickness also competing
        /// for that same clamp budget.
        /// </summary>
        public void FillTriangleRounded(Vector2 v1, Vector2 v2, Vector2 v3, float cornerRadius, Color color, float rotation = 0f, Vector2? origin = null)
        {
            if (cornerRadius <= 0.01f) { FillTriangle(v1, v2, v3, color, rotation, origin); return; }
            RotateTriangle(ref v1, ref v2, ref v3, rotation, origin);
            EnsureStarted();
            Span<Vector2> pts = [v1, v2, v3];
            float safeRadius = ClampCornerRadiusToFit(pts, cornerRadius);
            Span<Vector2> boundary = stackalloc Vector2[3 * (MaxCircleSegments + 1)];
            int count = BuildRoundedCornerBoundary(pts, safeRadius, LineJoin.Round, NoBorderClampBudget, boundary);
            FillPolygon(boundary[..count], color);
        }

        /// <summary>Equivalent to <see cref="BorderTriangle"/> with <c>join: LineJoin.Round, jointRadius: cornerRadius</c> (pre-clamped so neighboring corners can't overlap, see <see cref="ClampCornerRadiusToFit"/>) — named to match <see cref="FillTriangleRounded"/> for discoverability.</summary>
        public void BorderTriangleRounded(Vector2 v1, Vector2 v2, Vector2 v3, float cornerRadius, Color color, float thickness = 1f, float rotation = 0f, Vector2? origin = null)
        {
            RotateTriangle(ref v1, ref v2, ref v3, rotation, origin);
            float safeRadius = ClampCornerRadiusToFit([v1, v2, v3], cornerRadius);
            BorderTriangle(v1, v2, v3, color, thickness, 0f, null, LineJoin.Round, safeRadius);
        }

        /// <summary>Draws a rounded-corner triangle with both fill and border (same color), border growing inward.</summary>
        public void DrawTriangleRounded(Vector2 v1, Vector2 v2, Vector2 v3, float cornerRadius, Color color, float thickness = 1f, float rotation = 0f, Vector2? origin = null)
            => DrawTriangleRounded(v1, v2, v3, cornerRadius, color, color, thickness, rotation, origin);

        /// <summary>Draws a rounded-corner triangle with an independently colored fill and border, border growing inward. Radius pre-clamped so neighboring corners can't overlap (see <see cref="ClampCornerRadiusToFit"/>).</summary>
        public void DrawTriangleRounded(Vector2 v1, Vector2 v2, Vector2 v3, float cornerRadius, Color fillColor, Color borderColor, float thickness = 1f, float rotation = 0f, Vector2? origin = null)
        {
            RotateTriangle(ref v1, ref v2, ref v3, rotation, origin);
            float safeRadius = ClampCornerRadiusToFit([v1, v2, v3], cornerRadius);
            DrawTriangle(v1, v2, v3, fillColor, borderColor, thickness, 0f, null, LineJoin.Round, safeRadius);
        }

        /// <summary>
        /// Draws a filled triangle fading from <paramref name="from"/> at <paramref name="v1"/>
        /// to <paramref name="to"/> at both <paramref name="v2"/> and <paramref name="v3"/>, no
        /// border. Thin wrapper over the existing per-vertex-color <see cref="FillTriangle(Vector2,Color,Vector2,Color,Vector2,Color,float,Vector2?)"/>.
        /// </summary>
        public void FillTriangleGradient(Vector2 v1, Vector2 v2, Vector2 v3, Color from, Color to, float rotation = 0f, Vector2? origin = null)
            => FillTriangle(v1, from, v2, to, v3, to, rotation, origin);

        /// <summary>
        /// Draws a filled triangle with each corner rounded by <paramref name="cornerRadius"/>,
        /// fading from <paramref name="from"/> at <paramref name="v1"/> to <paramref name="to"/>
        /// at both <paramref name="v2"/> and <paramref name="v3"/> — the standalone (no border)
        /// rounded-corner counterpart to <see cref="FillTriangleGradient(Vector2,Vector2,Vector2,Color,Color,float,Vector2?)"/>.
        /// Every boundary point (not just the 3 original corners) is colored by its exact
        /// barycentric weight against the ORIGINAL sharp triangle, so the gradient inside the
        /// rounded outline is pixel-for-pixel the same continuous blend the sharp-corner version
        /// has — unlike a flat per-corner fan color (which would fan out as visible radial
        /// creases converging on whichever point the fan happens to start from).
        /// </summary>
        public void FillTriangleGradientRounded(Vector2 v1, Vector2 v2, Vector2 v3, float cornerRadius, Color from, Color to, float rotation = 0f, Vector2? origin = null)
        {
            if (cornerRadius <= 0.01f) { FillTriangleGradient(v1, v2, v3, from, to, rotation, origin); return; }
            RotateTriangle(ref v1, ref v2, ref v3, rotation, origin);
            EnsureStarted();
            Span<Vector2> pts = [v1, v2, v3];
            float safeRadius = ClampCornerRadiusToFit(pts, cornerRadius);
            Span<Vector2> boundary = stackalloc Vector2[3 * (MaxCircleSegments + 1)];
            int count = BuildRoundedCornerBoundary(pts, safeRadius, LineJoin.Round, NoBorderClampBudget, boundary);
            FillTriangleGradientBoundary(boundary[..count], v1, v2, v3, from, to, to);
        }

        /// <summary>
        /// Fan-fills a rounded triangle's boundary (many points), coloring each point by its
        /// exact barycentric weight against the true (un-rounded) <paramref name="v1"/>/
        /// <paramref name="v2"/>/<paramref name="v3"/> triangle. This reproduces a smooth,
        /// continuous gradient — the same one <see cref="FillTriangle(Vector2,Color,Vector2,Color,Vector2,Color,float,Vector2?)"/>
        /// draws via pure GPU interpolation over one triangle — even though the actual geometry
        /// here is many thin fan triangles: since every point's color already IS its correct
        /// final blend, each thin triangle's own GPU interpolation between three
        /// already-correct colors stays consistent with its neighbors, so no facet/seam shows.
        /// </summary>
        private void FillTriangleGradientBoundary(ReadOnlySpan<Vector2> boundary, Vector2 v1, Vector2 v2, Vector2 v3, Color c1, Color c2, Color c3)
        {
            int n = boundary.Length;
            if (n < 3) return;

            int b = Reserve(n, (n - 2) * 3);
            for (int i = 0; i < n; i++)
                PushVertex(boundary[i], BarycentricColor(boundary[i], v1, v2, v3, c1, c2, c3));
            for (int i = 1; i < n - 1; i++)
                PushTriangleIndices(b, b + i, b + i + 1);
        }

        /// <summary>
        /// Blends <paramref name="c1"/>/<paramref name="c2"/>/<paramref name="c3"/> at
        /// <paramref name="p"/>'s barycentric weight against triangle <paramref name="v1"/>/
        /// <paramref name="v2"/>/<paramref name="v3"/> — the same interpolation the GPU performs
        /// natively across one triangle's own 3 vertex colors, computed explicitly here so it can
        /// be baked into extra points (e.g. a rounded boundary) instead.
        /// </summary>
        private static Color BarycentricColor(Vector2 p, Vector2 v1, Vector2 v2, Vector2 v3, Color c1, Color c2, Color c3)
        {
            Vector2 e1 = v2 - v1, e2 = v3 - v1, e3 = p - v1;
            float d00 = Vector2.Dot(e1, e1), d01 = Vector2.Dot(e1, e2), d11 = Vector2.Dot(e2, e2);
            float d20 = Vector2.Dot(e3, e1), d21 = Vector2.Dot(e3, e2);
            float denom = d00 * d11 - d01 * d01;
            if (MathF.Abs(denom) < 1e-8f) return c1;

            float wv = (d11 * d20 - d01 * d21) / denom;
            float wwt = (d00 * d21 - d01 * d20) / denom;
            float wu = 1f - wv - wwt;

            float r = c1.R * wu + c2.R * wv + c3.R * wwt;
            float g = c1.G * wu + c2.G * wv + c3.G * wwt;
            float bC = c1.B * wu + c2.B * wv + c3.B * wwt;
            float a = c1.A * wu + c2.A * wv + c3.A * wwt;
            return new Color(
                (byte)Math.Clamp(r + 0.5f, 0f, 255f),
                (byte)Math.Clamp(g + 0.5f, 0f, 255f),
                (byte)Math.Clamp(bC + 0.5f, 0f, 255f),
                (byte)Math.Clamp(a + 0.5f, 0f, 255f));
        }

        /// <summary>
        /// Fan-fills a rounded polygon boundary (many points) using a color per ORIGINAL vertex
        /// (few points) — each boundary point takes the color of whichever original vertex it's
        /// nearest to. Correct by construction: <see cref="ClampCornerRadiusToFit"/> already
        /// guarantees no corner's fillet reaches past the midpoint of an adjacent edge, so every
        /// boundary point is provably at least as close to the vertex it replaced as to either
        /// neighboring one. Used instead of trying to track "which vertex produced this point"
        /// through <see cref="BuildRoundedCornerBoundary"/>'s own loop, which would mean either
        /// changing that widely-shared method's signature or duplicating its logic.
        /// </summary>
        private void FillPolygonGradientByNearestVertex(ReadOnlySpan<Vector2> boundary, ReadOnlySpan<Vector2> originalVertices, ReadOnlySpan<Color> vertexColors)
        {
            EnsureStarted();
            int n = boundary.Length;
            if (n < 3) return;

            int b = Reserve(n, (n - 2) * 3);
            for (int i = 0; i < n; i++)
            {
                Vector2 p = boundary[i];
                int nearest = 0;
                float bestDistSq = Vector2.DistanceSquared(p, originalVertices[0]);
                for (int v = 1; v < originalVertices.Length; v++)
                {
                    float d = Vector2.DistanceSquared(p, originalVertices[v]);
                    if (d < bestDistSq) { bestDistSq = d; nearest = v; }
                }
                PushVertex(p, vertexColors[nearest]);
            }

            // Same convex-fast-path/ear-clipping split as FillPolygon: a rounded boundary
            // built from a concave polygon (a rounded star, say) is still concave, so the
            // plain fan-from-boundary[0] this used unconditionally can produce the same
            // wrong/overlapping triangles FillPolygon's own fan did before that fix.
            if (n == 3 || IsConvex(boundary))
            {
                for (int i = 1; i < n - 1; i++)
                    PushTriangleIndices(b, b + i, b + i + 1);
                return;
            }

            int triIndexCount = (n - 2) * 3;
            Span<int> localIndices = triIndexCount <= MaxStackAllocElements ? stackalloc int[triIndexCount] : new int[triIndexCount];
            int written = TriangulateEarClipping(boundary, localIndices);
            if (written > 0)
            {
                for (int i = 0; i < written; i += 3)
                    PushTriangleIndices(b + localIndices[i], b + localIndices[i + 1], b + localIndices[i + 2]);
            }
            else
            {
                for (int i = 1; i < n - 1; i++)
                    PushTriangleIndices(b, b + i, b + i + 1);
            }
        }

        /// <summary>Equivalent to <see cref="DrawTriangleGradient(Vector2,Vector2,Vector2,Color,Color,Color,float,float,Vector2?,LineJoin,float?)"/> with <c>join: LineJoin.Round, jointRadius: cornerRadius</c> (pre-clamped so neighboring corners can't overlap) — named to match <see cref="FillTriangleGradientRounded"/> for discoverability.</summary>
        public void DrawTriangleGradientRounded(Vector2 v1, Vector2 v2, Vector2 v3, float cornerRadius, Color from, Color to, Color borderColor, float thickness = 1f, float rotation = 0f, Vector2? origin = null)
        {
            RotateTriangle(ref v1, ref v2, ref v3, rotation, origin);
            float safeRadius = ClampCornerRadiusToFit([v1, v2, v3], cornerRadius);
            DrawTriangleGradient(v1, v2, v3, from, to, borderColor, thickness, 0f, null, LineJoin.Round, safeRadius);
        }

        /// <summary>
        /// Draws a triangle with a <paramref name="from"/>→<paramref name="to"/> gradient fill
        /// (v1→from, v2/v3→to) and a solid border. The gradient's own triangle is inset by
        /// <paramref name="thickness"/> via <see cref="InsetConvexPolygon"/> — it stops
        /// exactly where the border begins, same rule as the other shapes' gradients.
        /// </summary>
        public void DrawTriangleGradient(Vector2 v1, Vector2 v2, Vector2 v3, Color from, Color to, Color borderColor, float thickness = 1f, float rotation = 0f, Vector2? origin = null, LineJoin join = LineJoin.Miter, float? jointRadius = null)
        {
            RotateTriangle(ref v1, ref v2, ref v3, rotation, origin);
            EnsureStarted();
            if (thickness > 0f)
            {
                Span<Vector2> pts = [v1, v2, v3];
                if (join == LineJoin.Miter || !jointRadius.HasValue || jointRadius.Value <= 0f)
                {
                    Span<Vector2> inset = stackalloc Vector2[3];
                    InsetConvexPolygon(pts, thickness, inset, closed: true);
                    FillTriangleGradient(inset[0], inset[1], inset[2], from, to);
                }
                else
                {
                    // Same rounded-boundary-shared-by-fill-and-border fix as DrawTriangle: round
                    // the true corners first (see BuildRoundedCornerBoundary), then inset that
                    // boundary so the gradient stops near where the border begins. The inset
                    // distance is capped at the rounding radius itself — insetting a rounded arc
                    // by more than its own radius crosses through the arc's center and inverts
                    // it (self-intersecting garbage) — since BorderTriangle is drawn on top right
                    // after, any resulting sub-thickness gap here is simply covered by the
                    // border, not a visible seam.
                    Span<Vector2> boundary = stackalloc Vector2[3 * (MaxCircleSegments + 1)];
                    int count = BuildRoundedCornerBoundary(pts, jointRadius.Value, join, thickness, boundary);
                    Span<Vector2> inset = stackalloc Vector2[count];
                    InsetConvexPolygon(boundary[..count], MathF.Min(thickness, jointRadius.Value), inset, closed: true);
                    // Barycentric against the TRUE (un-inset) triangle, same as FillTriangleGradientRounded
                    // — a flat per-corner fan color here would show the same radial-seam artifact.
                    FillTriangleGradientBoundary(inset, v1, v2, v3, from, to, to);
                }
                BorderTriangle(v1, v2, v3, borderColor, thickness, join: join, jointRadius: jointRadius);
            }
            else
            {
                FillTriangleGradient(v1, v2, v3, from, to);
            }
        }

        /// <summary>Draws a triangle fan. The first point is the shared center vertex.</summary>
        public void DrawTriangleFan(ReadOnlySpan<Vector2> points, Color color)
        {
            EnsureStarted();
            if (points.Length < 3) return;

            int b = Reserve(points.Length, (points.Length - 2) * 3);
            for (int i = 0; i < points.Length; i++)
                PushVertex(points[i], color);

            for (int i = 1; i < points.Length - 1; i++)
                PushTriangleIndices(b, b + i, b + i + 1);
        }

        /// <summary>Draws a triangle strip.</summary>
        public void DrawTriangleStrip(ReadOnlySpan<Vector2> points, Color color)
        {
            EnsureStarted();
            if (points.Length < 3) return;

            int b = Reserve(points.Length, (points.Length - 2) * 3);
            for (int i = 0; i < points.Length; i++)
                PushVertex(points[i], color);

            for (int i = 0; i < points.Length - 2; i++)
                PushTriangleIndices(b + i, b + i + 1, b + i + 2);
        }

        // ==================================================================
        // RECTANGLES
        // ==================================================================

        /// <summary>
        /// Draws a filled rectangle, no border. <paramref name="rotation"/> (radians)
        /// pivots about the rectangle's own center unless <paramref name="origin"/> is
        /// given, expressed relative to the rectangle's top-left corner.
        /// </summary>
        public void FillRectangle(float x, float y, float width, float height, Color color, float rotation = 0f, Vector2? origin = null)
        {
            EnsureStarted();

            if (rotation == 0f)
            {
                int b = Reserve(4, 6);
                PushVertex(x, y, color);
                PushVertex(x + width, y, color);
                PushVertex(x + width, y + height, color);
                PushVertex(x, y + height, color);
                PushQuadIndices(b);
                return;
            }

            Vector2 pivot = origin ?? new Vector2(width * 0.5f, height * 0.5f);
            float cos = MathF.Cos(rotation);
            float sin = MathF.Sin(rotation);
            float ox = x + pivot.X;
            float oy = y + pivot.Y;

            // Corner offsets relative to the pivot.
            float left = -pivot.X;
            float top = -pivot.Y;
            float right = left + width;
            float bottom = top + height;

            int bi = Reserve(4, 6);
            PushVertex(ox + left * cos - top * sin, oy + left * sin + top * cos, color);
            PushVertex(ox + right * cos - top * sin, oy + right * sin + top * cos, color);
            PushVertex(ox + right * cos - bottom * sin, oy + right * sin + bottom * cos, color);
            PushVertex(ox + left * cos - bottom * sin, oy + left * sin + bottom * cos, color);
            PushQuadIndices(bi);
        }

        /// <inheritdoc cref="FillRectangle(float,float,float,float,Color,float,Vector2?)"/>
        public void FillRectangle(Vector2 position, Vector2 size, Color color, float rotation = 0f, Vector2? origin = null)
            => FillRectangle(position.X, position.Y, size.X, size.Y, color, rotation, origin);

        /// <inheritdoc cref="FillRectangle(float,float,float,float,Color,float,Vector2?)"/>
        public void FillRectangle(Rectangle rect, Color color, float rotation = 0f, Vector2? origin = null)
            => FillRectangle(rect.X, rect.Y, rect.Width, rect.Height, color, rotation, origin);

        /// <summary>Draws a filled rectangle with four independently colored corners (no rotation).</summary>
        public void FillRectangle(Rectangle rect, Color topLeft, Color topRight, Color bottomRight, Color bottomLeft)
        {
            EnsureStarted();
            int b = Reserve(4, 6);
            PushVertex(rect.X, rect.Y, topLeft);
            PushVertex(rect.X + rect.Width, rect.Y, topRight);
            PushVertex(rect.X + rect.Width, rect.Y + rect.Height, bottomRight);
            PushVertex(rect.X, rect.Y + rect.Height, bottomLeft);
            PushQuadIndices(b);
        }

        /// <summary>
        /// Draws a rectangle's border only, growing inward from the rectangle's own edge —
        /// the border never extends past <paramref name="width"/>/<paramref name="height"/>,
        /// clamped so a thick border on a small rect degenerates gracefully to a fill.
        /// </summary>
        /// <param name="join">
        /// <see cref="LineJoin.Miter"/> (default), with no rotation, keeps the original fast
        /// path — four overlapping filled rectangles, sharp corners. Any rotation, or
        /// Round/Bevel, routes through the shared outline builder on the rectangle's corners
        /// pre-shrunk by <see cref="InsetConvexPolygon"/> (this is what guarantees "inward,
        /// never exceeds the shape" for those cases too — the old Round/Bevel path here
        /// didn't shrink first and could bulge outward; fixed as part of this redesign).
        /// </param>
        public void BorderRectangle(float x, float y, float width, float height, Color color, float thickness = 1f, float rotation = 0f, Vector2? origin = null, LineJoin join = LineJoin.Miter, float? jointRadius = null)
        {
            if (thickness <= 0f) return;
            float t = Math.Min(thickness, Math.Min(width, height) * 0.5f);

            if (rotation == 0f && join == LineJoin.Miter)
            {
                FillRectangle(x, y, width, t, color);                          // top
                FillRectangle(x, y + height - t, width, t, color);             // bottom
                FillRectangle(x, y + t, t, height - t * 2f, color);            // left
                FillRectangle(x + width - t, y + t, t, height - t * 2f, color);// right
                return;
            }

            EnsureStarted();
            Vector2 pivot = origin ?? new Vector2(width * 0.5f, height * 0.5f);
            Span<Vector2> pts = stackalloc Vector2[4];
            if (rotation == 0f)
            {
                pts[0] = new Vector2(x, y);
                pts[1] = new Vector2(x + width, y);
                pts[2] = new Vector2(x + width, y + height);
                pts[3] = new Vector2(x, y + height);
            }
            else
            {
                float cos = MathF.Cos(rotation), sin = MathF.Sin(rotation);
                float ox = x + pivot.X, oy = y + pivot.Y;
                float left = -pivot.X, top = -pivot.Y, right = left + width, bottom = top + height;
                pts[0] = new Vector2(ox + left * cos - top * sin, oy + left * sin + top * cos);
                pts[1] = new Vector2(ox + right * cos - top * sin, oy + right * sin + top * cos);
                pts[2] = new Vector2(ox + right * cos - bottom * sin, oy + right * sin + bottom * cos);
                pts[3] = new Vector2(ox + left * cos - bottom * sin, oy + left * sin + bottom * cos);
            }

            if (join == LineJoin.Miter)
            {
                Span<Vector2> inset = stackalloc Vector2[4];
                InsetConvexPolygon(pts, t * 0.5f, inset, closed: true);
                BuildOutlineGeometry(inset, t, color, closed: true, join);
                return;
            }

            // Round/Bevel: stroke the true (rotated but un-inset) corners directly with an
            // inward-only stroke — pre-insetting via InsetConvexPolygon first (as the Miter
            // branch above does) is wrong for Round/Bevel; see BuildRoundedOutlineGeometry's
            // inwardOnly doc for why.
            BuildOutlineGeometry(pts, t, color, closed: true, join, LineCap.Butt, jointRadius, inwardOnly: true);
        }

        /// <inheritdoc cref="BorderRectangle(float,float,float,float,Color,float,float,Vector2?,LineJoin,float?)"/>
        public void BorderRectangle(Rectangle rect, Color color, float thickness = 1f, float rotation = 0f, Vector2? origin = null, LineJoin join = LineJoin.Miter, float? jointRadius = null)
            => BorderRectangle(rect.X, rect.Y, rect.Width, rect.Height, color, thickness, rotation, origin, join, jointRadius);

        /// <inheritdoc cref="BorderRectangle(float,float,float,float,Color,float,float,Vector2?,LineJoin,float?)"/>
        public void BorderRectangle(Vector2 position, Vector2 size, Color color, float thickness = 1f, float rotation = 0f, Vector2? origin = null, LineJoin join = LineJoin.Miter, float? jointRadius = null)
            => BorderRectangle(position.X, position.Y, size.X, size.Y, color, thickness, rotation, origin, join, jointRadius);

        /// <summary>Draws a rectangle with both fill and border (same color), border growing inward.</summary>
        public void DrawRectangle(float x, float y, float width, float height, Color color, float thickness = 1f, float rotation = 0f, Vector2? origin = null, LineJoin join = LineJoin.Miter, float? jointRadius = null)
            => DrawRectangle(x, y, width, height, color, color, thickness, rotation, origin, join, jointRadius);

        /// <inheritdoc cref="DrawRectangle(float,float,float,float,Color,float,float,Vector2?,LineJoin,float?)"/>
        public void DrawRectangle(Rectangle rect, Color color, float thickness = 1f, float rotation = 0f, Vector2? origin = null, LineJoin join = LineJoin.Miter, float? jointRadius = null)
            => DrawRectangle(rect.X, rect.Y, rect.Width, rect.Height, color, thickness, rotation, origin, join, jointRadius);

        /// <inheritdoc cref="DrawRectangle(float,float,float,float,Color,float,float,Vector2?,LineJoin,float?)"/>
        public void DrawRectangle(Vector2 position, Vector2 size, Color color, float thickness = 1f, float rotation = 0f, Vector2? origin = null, LineJoin join = LineJoin.Miter, float? jointRadius = null)
            => DrawRectangle(position.X, position.Y, size.X, size.Y, color, thickness, rotation, origin, join, jointRadius);

        /// <summary>
        /// Draws a rectangle with an independently colored fill and border, border growing inward.
        /// For Round/Bevel <paramref name="join"/>, the fill is rounded off the same way the
        /// border is (see <see cref="BuildRoundedCornerBoundary"/>) so the two share one boundary
        /// instead of a sharp-cornered fill showing past a rounded border corner.
        /// </summary>
        public void DrawRectangle(float x, float y, float width, float height, Color fillColor, Color borderColor, float thickness = 1f, float rotation = 0f, Vector2? origin = null, LineJoin join = LineJoin.Miter, float? jointRadius = null)
        {
            if (join == LineJoin.Miter || !jointRadius.HasValue || jointRadius.Value <= 0f || thickness <= 0f)
            {
                FillRectangle(x, y, width, height, fillColor, rotation, origin);
            }
            else
            {
                EnsureStarted();
                Vector2 pivot = origin ?? new Vector2(width * 0.5f, height * 0.5f);
                Span<Vector2> pts = stackalloc Vector2[4];
                if (rotation == 0f)
                {
                    pts[0] = new Vector2(x, y);
                    pts[1] = new Vector2(x + width, y);
                    pts[2] = new Vector2(x + width, y + height);
                    pts[3] = new Vector2(x, y + height);
                }
                else
                {
                    float cos = MathF.Cos(rotation), sin = MathF.Sin(rotation);
                    float ox = x + pivot.X, oy = y + pivot.Y;
                    float left = -pivot.X, top = -pivot.Y, right = left + width, bottom = top + height;
                    pts[0] = new Vector2(ox + left * cos - top * sin, oy + left * sin + top * cos);
                    pts[1] = new Vector2(ox + right * cos - top * sin, oy + right * sin + top * cos);
                    pts[2] = new Vector2(ox + right * cos - bottom * sin, oy + right * sin + bottom * cos);
                    pts[3] = new Vector2(ox + left * cos - bottom * sin, oy + left * sin + bottom * cos);
                }
                // Same clamp BorderRectangle applies to its own stroke thickness, so the fill's
                // rounding stays in lockstep with it (see BuildRoundedCornerBoundary's doc).
                float t = Math.Min(thickness, Math.Min(width, height) * 0.5f);
                Span<Vector2> boundary = stackalloc Vector2[4 * (MaxCircleSegments + 1)];
                int count = BuildRoundedCornerBoundary(pts, jointRadius.Value, join, t, boundary);
                FillPolygon(boundary[..count], fillColor);
            }
            if (thickness > 0f) BorderRectangle(x, y, width, height, borderColor, thickness, rotation, origin, join, jointRadius);
        }

        /// <inheritdoc cref="DrawRectangle(float,float,float,float,Color,Color,float,float,Vector2?,LineJoin,float?)"/>
        public void DrawRectangle(Rectangle rect, Color fillColor, Color borderColor, float thickness = 1f, float rotation = 0f, Vector2? origin = null, LineJoin join = LineJoin.Miter, float? jointRadius = null)
            => DrawRectangle(rect.X, rect.Y, rect.Width, rect.Height, fillColor, borderColor, thickness, rotation, origin, join, jointRadius);

        /// <inheritdoc cref="DrawRectangle(float,float,float,float,Color,Color,float,float,Vector2?,LineJoin,float?)"/>
        public void DrawRectangle(Vector2 position, Vector2 size, Color fillColor, Color borderColor, float thickness = 1f, float rotation = 0f, Vector2? origin = null, LineJoin join = LineJoin.Miter, float? jointRadius = null)
            => DrawRectangle(position.X, position.Y, size.X, size.Y, fillColor, borderColor, thickness, rotation, origin, join, jointRadius);

        /// <summary>
        /// Draws a rectangle filled with a linear gradient from <paramref name="from"/> to
        /// <paramref name="to"/>, no border. <paramref name="horizontal"/> picks the fade
        /// axis (true = left→right, false = top→bottom). <paramref name="rotation"/> (radians)
        /// turns that axis about <paramref name="origin"/> (rect center if null) — same
        /// convention as <see cref="FillRectangleRoundedGradient"/>. <paramref name="innerOffset"/>
        /// holds solid <paramref name="from"/> color for that many pixels along the fade axis
        /// before the gradient starts; <paramref name="outerOffset"/> holds solid
        /// <paramref name="to"/> color for that many pixels at the far end. If they'd
        /// overlap, both are scaled down proportionally to fit (same "clamp gracefully"
        /// precedent as elsewhere).
        /// </summary>
        public void FillRectangleGradient(float x, float y, float width, float height, Color from, Color to, bool horizontal, float rotation = 0f, Vector2? origin = null, float innerOffset = 0f, float outerOffset = 0f)
        {
            EnsureStarted();
            if (width <= 0f || height <= 0f) return;

            float axisLength = horizontal ? width : height;
            innerOffset = MathF.Max(0f, innerOffset);
            outerOffset = MathF.Max(0f, outerOffset);
            float total = innerOffset + outerOffset;
            if (total > axisLength && total > 0f)
            {
                float scale = axisLength / total;
                innerOffset *= scale;
                outerOffset *= scale;
            }

            // Stops along the fade axis: 0 (from) → [innerOffset (from)] → gradientEnd (to) → [axisLength (to)].
            Span<float> stops = stackalloc float[4];
            Span<Color> stopColors = stackalloc Color[4];
            int stopCount = 0;
            stops[stopCount] = 0f; stopColors[stopCount] = from; stopCount++;
            if (innerOffset > 0f) { stops[stopCount] = innerOffset; stopColors[stopCount] = from; stopCount++; }
            stops[stopCount] = axisLength - outerOffset; stopColors[stopCount] = to; stopCount++;
            if (outerOffset > 0f) { stops[stopCount] = axisLength; stopColors[stopCount] = to; stopCount++; }

            Vector2 pivot = origin.HasValue ? new Vector2(x + origin.Value.X, y + origin.Value.Y) : new Vector2(x + width * 0.5f, y + height * 0.5f);
            float cos = MathF.Cos(rotation), sin = MathF.Sin(rotation);
            Vector2 RotatePoint(float px, float py)
            {
                if (rotation == 0f) return new Vector2(px, py);
                float dx = px - pivot.X, dy = py - pivot.Y;
                return new Vector2(pivot.X + dx * cos - dy * sin, pivot.Y + dx * sin + dy * cos);
            }

            int b = Reserve(stopCount * 2, (stopCount - 1) * 6);
            for (int i = 0; i < stopCount; i++)
            {
                Color c = stopColors[i];
                if (horizontal)
                {
                    float sx = x + stops[i];
                    PushVertex(RotatePoint(sx, y), c);
                    PushVertex(RotatePoint(sx, y + height), c);
                }
                else
                {
                    float sy = y + stops[i];
                    PushVertex(RotatePoint(x, sy), c);
                    PushVertex(RotatePoint(x + width, sy), c);
                }
            }
            for (int i = 0; i < stopCount - 1; i++)
            {
                int i0 = b + i * 2, i1 = b + (i + 1) * 2;
                PushTriangleIndices(i0, i0 + 1, i1 + 1);
                PushTriangleIndices(i0, i1 + 1, i1);
            }
        }

        /// <inheritdoc cref="FillRectangleGradient(float,float,float,float,Color,Color,bool,float,Vector2?,float,float)"/>
        public void FillRectangleGradient(Rectangle rect, Color from, Color to, bool horizontal, float rotation = 0f, Vector2? origin = null, float innerOffset = 0f, float outerOffset = 0f)
            => FillRectangleGradient(rect.X, rect.Y, rect.Width, rect.Height, from, to, horizontal, rotation, origin, innerOffset, outerOffset);

        /// <inheritdoc cref="FillRectangleGradient(float,float,float,float,Color,Color,bool,float,Vector2?,float,float)"/>
        public void FillRectangleGradient(Vector2 position, Vector2 size, Color from, Color to, bool horizontal, float rotation = 0f, Vector2? origin = null, float innerOffset = 0f, float outerOffset = 0f)
            => FillRectangleGradient(position.X, position.Y, size.X, size.Y, from, to, horizontal, rotation, origin, innerOffset, outerOffset);

        /// <summary>
        /// Draws a rectangle with a linear gradient fill and a solid border. The gradient's
        /// own area is the rect inset by <paramref name="thickness"/> on every side — it
        /// stops exactly where the border begins — and <paramref name="innerOffset"/>/
        /// <paramref name="outerOffset"/> are then measured from that inset area's own edges,
        /// same "stops at the border" rule as <see cref="DrawCircleGradient"/>. <paramref name="rotation"/>
        /// turns both the fill and the border together, same convention as <see cref="DrawRectangleRoundedGradient"/>.
        /// </summary>
        public void DrawRectangleGradient(float x, float y, float width, float height, Color from, Color to, bool horizontal, Color borderColor, float thickness = 1f, float rotation = 0f, Vector2? origin = null, float innerOffset = 0f, float outerOffset = 0f)
        {
            float t = MathF.Max(0f, thickness);
            // origin is relative to this call's own (x,y); shift it by -t so the inset fill's
            // pivot (measured from its own, shrunk (x+t,y+t)) lands on the same absolute point
            // as the border's pivot below — otherwise the two would rotate about different points.
            Vector2? fillOrigin = origin.HasValue ? origin.Value - new Vector2(t, t) : null;
            FillRectangleGradient(x + t, y + t, MathF.Max(0f, width - 2f * t), MathF.Max(0f, height - 2f * t), from, to, horizontal, rotation, fillOrigin, innerOffset, outerOffset);
            if (thickness > 0f) BorderRectangle(x, y, width, height, borderColor, thickness, rotation, origin);
        }

        /// <inheritdoc cref="DrawRectangleGradient(float,float,float,float,Color,Color,bool,Color,float,float,Vector2?,float,float)"/>
        public void DrawRectangleGradient(Rectangle rect, Color from, Color to, bool horizontal, Color borderColor, float thickness = 1f, float rotation = 0f, Vector2? origin = null, float innerOffset = 0f, float outerOffset = 0f)
            => DrawRectangleGradient(rect.X, rect.Y, rect.Width, rect.Height, from, to, horizontal, borderColor, thickness, rotation, origin, innerOffset, outerOffset);

        /// <inheritdoc cref="DrawRectangleGradient(float,float,float,float,Color,Color,bool,Color,float,float,Vector2?,float,float)"/>
        public void DrawRectangleGradient(Vector2 position, Vector2 size, Color from, Color to, bool horizontal, Color borderColor, float thickness = 1f, float rotation = 0f, Vector2? origin = null, float innerOffset = 0f, float outerOffset = 0f)
            => DrawRectangleGradient(position.X, position.Y, size.X, size.Y, from, to, horizontal, borderColor, thickness, rotation, origin, innerOffset, outerOffset);

        // ==================================================================
        // ROUNDED RECTANGLES
        // ==================================================================

        /// <summary>
        /// Fills a convex boundary (a rounded/chamfered rectangle's or a circle's boundary
        /// points) with a linear gradient along the shape's own local H/V axis — shared by
        /// every boundary-based gradient, since "compute a gradient color per boundary vertex
        /// and fan-fill" is identical regardless of which boundary generator produced the
        /// points. Each point is un-rotated back to the shape's local frame first (inverse of
        /// <paramref name="rotation"/> about <paramref name="origin"/>) before computing its
        /// axis position, so the gradient rotates *with* the shape rather than staying
        /// screen-aligned. <paramref name="innerOffset"/>/<paramref name="outerOffset"/> work
        /// exactly as in <see cref="FillRectangleGradient(float,float,float,float,Color,Color,bool,float,float)"/>.
        /// </summary>
        private void FillAxisGradientBoundary(ReadOnlySpan<Vector2> boundary, float boxX, float boxY, float boxWidth, float boxHeight, float rotation, Vector2? origin, Color from, Color to, bool horizontal, float innerOffset, float outerOffset)
        {
            int count = boundary.Length;
            if (count < 3) return;

            float axisLength = horizontal ? boxWidth : boxHeight;
            if (axisLength <= 0f)
            {
                int b0 = Reserve(count, (count - 2) * 3);
                for (int i = 0; i < count; i++) PushVertex(boundary[i], from);
                for (int i = 1; i < count - 1; i++) PushTriangleIndices(b0, b0 + i, b0 + i + 1);
                return;
            }

            innerOffset = MathF.Max(0f, innerOffset);
            outerOffset = MathF.Max(0f, outerOffset);
            float total = innerOffset + outerOffset;
            if (total > axisLength && total > 0f)
            {
                float scale = axisLength / total;
                innerOffset *= scale;
                outerOffset *= scale;
            }
            float axisMin = horizontal ? boxX : boxY;
            float innerT = innerOffset / axisLength;
            float outerT = 1f - outerOffset / axisLength;

            Vector2 pivot = origin.HasValue ? new Vector2(boxX + origin.Value.X, boxY + origin.Value.Y) : new Vector2(boxX + boxWidth * 0.5f, boxY + boxHeight * 0.5f);
            float cos = MathF.Cos(-rotation), sin = MathF.Sin(-rotation);

            int b = Reserve(count, (count - 2) * 3);
            for (int i = 0; i < count; i++)
            {
                Vector2 p = boundary[i];
                Vector2 d = p - pivot;
                Vector2 local = rotation == 0f ? p : pivot + new Vector2(d.X * cos - d.Y * sin, d.X * sin + d.Y * cos);
                float pos = horizontal ? local.X : local.Y;
                float t = (pos - axisMin) / axisLength;

                Color c;
                if (t <= innerT) c = from;
                else if (t >= outerT) c = to;
                else
                {
                    float span = outerT - innerT;
                    c = Color.Lerp(from, to, span > 1e-6f ? (t - innerT) / span : 0f);
                }
                PushVertex(p, c);
            }
            for (int i = 1; i < count - 1; i++)
                PushTriangleIndices(b, b + i, b + i + 1);
        }

        /// <summary>
        /// Samples a rounded rectangle's boundary (per-corner arcs plus the straight edges
        /// between them, in order: TR arc → right edge → BR arc → bottom edge → BL arc →
        /// left edge → TL arc → top edge, closing back to TR) into <paramref name="buffer"/>,
        /// returning how many points were written. Each corner's radius is independently
        /// clamped to half the shorter side — same simple per-corner clamp as the original
        /// single-radius rounded rectangle; doesn't check pairwise sums, so two large adjacent
        /// radii can still touch/overlap slightly (matches this project's existing
        /// "clamp per-dimension, don't solve joint constraints" precedent elsewhere).
        /// A zero-radius corner degenerates to its single sharp point, no arc sampling.
        /// Rotation (radians) is applied about <paramref name="origin"/> (rect center if
        /// null) after the unrotated boundary is built. <paramref name="buffer"/> must be at
        /// least <c>4 * (MaxCircleSegments + 1)</c> long to fit the worst case (every corner
        /// fully rounded at the segment cap).
        /// </summary>
        private static int GenerateRoundedRectBoundary(Rectangle rect, RectCorners radius, float rotation, Vector2? origin, Span<Vector2> buffer)
        {
            float x0 = rect.X, y0 = rect.Y, x1 = rect.X + rect.Width, y1 = rect.Y + rect.Height;
            float maxR = MathF.Min(MathF.Abs(rect.Width), MathF.Abs(rect.Height)) * 0.5f;
            float rTL = Math.Clamp(radius.TopLeft, 0f, maxR);
            float rTR = Math.Clamp(radius.TopRight, 0f, maxR);
            float rBR = Math.Clamp(radius.BottomRight, 0f, maxR);
            float rBL = Math.Clamp(radius.BottomLeft, 0f, maxR);

            int count = 0;
            AppendRoundedCorner(buffer, ref count, new Vector2(x1 - rTR, y0 + rTR), rTR, 0.75f, 1.00f); // TR
            AppendRoundedCorner(buffer, ref count, new Vector2(x1 - rBR, y1 - rBR), rBR, 0.00f, 0.25f); // BR
            AppendRoundedCorner(buffer, ref count, new Vector2(x0 + rBL, y1 - rBL), rBL, 0.25f, 0.50f); // BL
            AppendRoundedCorner(buffer, ref count, new Vector2(x0 + rTL, y0 + rTL), rTL, 0.50f, 0.75f); // TL

            if (rotation != 0f)
            {
                Vector2 pivot = origin.HasValue ? new Vector2(rect.X + origin.Value.X, rect.Y + origin.Value.Y) : new Vector2(rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.5f);
                float cos = MathF.Cos(rotation), sin = MathF.Sin(rotation);
                for (int i = 0; i < count; i++)
                {
                    Vector2 d = buffer[i] - pivot;
                    buffer[i] = pivot + new Vector2(d.X * cos - d.Y * sin, d.X * sin + d.Y * cos);
                }
            }

            return count;
        }

        private static void AppendRoundedCorner(Span<Vector2> buffer, ref int count, Vector2 center, float r, float fromTurn, float toTurn)
        {
            if (r <= 0.01f)
            {
                buffer[count++] = center;
                return;
            }
            int segs = SegmentsForArc(r, MathHelper.PiOver2);
            for (int k = 0; k <= segs; k++)
            {
                float t = fromTurn + (toTurn - fromTurn) * (k / (float)segs);
                buffer[count++] = center + SampleUnitCircle(t) * r;
            }
        }

        /// <summary>Draws a filled rounded rectangle, no border.</summary>
        /// <param name="radius">
        /// Corner radius, per corner (implicitly convertible from a single float for all
        /// four). Each corner is independently clamped to half the shorter side.
        /// </param>
        /// <param name="rotation">Rotation in radians, about the rect's own center unless <paramref name="origin"/> is given.</param>
        public void FillRectangleRounded(Rectangle rect, RectCorners radius, Color color, float rotation = 0f, Vector2? origin = null)
        {
            EnsureStarted();
            Span<Vector2> buffer = stackalloc Vector2[4 * (MaxCircleSegments + 1)];
            int count = GenerateRoundedRectBoundary(rect, radius, rotation, origin, buffer);
            FillPolygon(buffer[..count], color);
        }

        /// <inheritdoc cref="FillRectangleRounded(Rectangle,RectCorners,Color,float,Vector2?)"/>
        /// <remarks><paramref name="position"/>/<paramref name="size"/> are truncated to <see cref="Rectangle"/>'s integer fields.</remarks>
        public void FillRectangleRounded(Vector2 position, Vector2 size, RectCorners radius, Color color, float rotation = 0f, Vector2? origin = null)
            => FillRectangleRounded(new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), radius, color, rotation, origin);

        /// <summary>
        /// Draws a rounded rectangle's border only, growing inward from its own edges (both
        /// the straight sides and the corner radii) via <see cref="InsetConvexPolygon"/> —
        /// the border never extends past the rectangle. The corner arcs are pre-sampled into
        /// points and stroked with a plain <see cref="LineJoin.Miter"/> pass (consecutive
        /// samples are nearly colinear, so miter looks smooth) rather than the more expensive
        /// tangent-fillet <see cref="LineJoin.Round"/> machinery built for arbitrary polylines
        /// — cheaper, and unnecessary here since the roundness already comes from the samples.
        /// </summary>
        public void BorderRectangleRounded(Rectangle rect, RectCorners radius, Color color, float thickness = 1f, float rotation = 0f, Vector2? origin = null)
        {
            if (thickness <= 0f) return;
            EnsureStarted();
            Span<Vector2> buffer = stackalloc Vector2[4 * (MaxCircleSegments + 1)];
            int count = GenerateRoundedRectBoundary(rect, radius, rotation, origin, buffer);
            ReadOnlySpan<Vector2> boundary = buffer[..count];

            Span<Vector2> inset = stackalloc Vector2[count];
            InsetConvexPolygon(boundary, thickness * 0.5f, inset, closed: true);
            BuildOutlineGeometry(inset, thickness, color, closed: true, LineJoin.Miter);
        }

        /// <inheritdoc cref="BorderRectangleRounded(Rectangle,RectCorners,Color,float,float,Vector2?)"/>
        /// <remarks><paramref name="position"/>/<paramref name="size"/> are truncated to <see cref="Rectangle"/>'s integer fields.</remarks>
        public void BorderRectangleRounded(Vector2 position, Vector2 size, RectCorners radius, Color color, float thickness = 1f, float rotation = 0f, Vector2? origin = null)
            => BorderRectangleRounded(new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), radius, color, thickness, rotation, origin);

        /// <summary>Draws a rounded rectangle with both fill and border (same color), border growing inward.</summary>
        public void DrawRectangleRounded(Rectangle rect, RectCorners radius, Color color, float thickness = 1f, float rotation = 0f, Vector2? origin = null)
            => DrawRectangleRounded(rect, radius, color, color, thickness, rotation, origin);

        /// <inheritdoc cref="DrawRectangleRounded(Rectangle,RectCorners,Color,float,float,Vector2?)"/>
        /// <remarks><paramref name="position"/>/<paramref name="size"/> are truncated to <see cref="Rectangle"/>'s integer fields.</remarks>
        public void DrawRectangleRounded(Vector2 position, Vector2 size, RectCorners radius, Color color, float thickness = 1f, float rotation = 0f, Vector2? origin = null)
            => DrawRectangleRounded(new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), radius, color, thickness, rotation, origin);

        /// <summary>Draws a rounded rectangle with an independently colored fill and border, border growing inward.</summary>
        public void DrawRectangleRounded(Rectangle rect, RectCorners radius, Color fillColor, Color borderColor, float thickness = 1f, float rotation = 0f, Vector2? origin = null)
        {
            FillRectangleRounded(rect, radius, fillColor, rotation, origin);
            if (thickness > 0f) BorderRectangleRounded(rect, radius, borderColor, thickness, rotation, origin);
        }

        /// <inheritdoc cref="DrawRectangleRounded(Rectangle,RectCorners,Color,Color,float,float,Vector2?)"/>
        /// <remarks><paramref name="position"/>/<paramref name="size"/> are truncated to <see cref="Rectangle"/>'s integer fields.</remarks>
        public void DrawRectangleRounded(Vector2 position, Vector2 size, RectCorners radius, Color fillColor, Color borderColor, float thickness = 1f, float rotation = 0f, Vector2? origin = null)
            => DrawRectangleRounded(new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), radius, fillColor, borderColor, thickness, rotation, origin);

        /// <summary>
        /// Draws a rounded rectangle filled with a linear gradient along its own H/V axis
        /// (rotates with the shape), no border. <paramref name="innerOffset"/>/
        /// <paramref name="outerOffset"/> work exactly as in <see cref="FillRectangleGradient(float,float,float,float,Color,Color,bool,float,float)"/>.
        /// </summary>
        public void FillRectangleRoundedGradient(Rectangle rect, RectCorners radius, Color from, Color to, bool horizontal, float rotation = 0f, Vector2? origin = null, float innerOffset = 0f, float outerOffset = 0f)
        {
            EnsureStarted();
            Span<Vector2> buffer = stackalloc Vector2[4 * (MaxCircleSegments + 1)];
            int count = GenerateRoundedRectBoundary(rect, radius, rotation, origin, buffer);
            FillAxisGradientBoundary(buffer[..count], rect.X, rect.Y, rect.Width, rect.Height, rotation, origin, from, to, horizontal, innerOffset, outerOffset);
        }

        /// <inheritdoc cref="FillRectangleRoundedGradient(Rectangle,RectCorners,Color,Color,bool,float,Vector2?,float,float)"/>
        /// <remarks><paramref name="position"/>/<paramref name="size"/> are truncated to <see cref="Rectangle"/>'s integer fields.</remarks>
        public void FillRectangleRoundedGradient(Vector2 position, Vector2 size, RectCorners radius, Color from, Color to, bool horizontal, float rotation = 0f, Vector2? origin = null, float innerOffset = 0f, float outerOffset = 0f)
            => FillRectangleRoundedGradient(new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), radius, from, to, horizontal, rotation, origin, innerOffset, outerOffset);

        /// <summary>
        /// Draws a rounded rectangle with a linear gradient fill and a solid border. The
        /// gradient's own boundary is the rounded rectangle's boundary inset by
        /// <paramref name="thickness"/> via <see cref="InsetConvexPolygon"/> (shrinks the
        /// straight edges and the corner arcs together, concentrically) — stops exactly
        /// where the border begins, same rule as the other shapes' gradients.
        /// </summary>
        public void DrawRectangleRoundedGradient(Rectangle rect, RectCorners radius, Color from, Color to, bool horizontal, Color borderColor, float thickness = 1f, float rotation = 0f, Vector2? origin = null, float innerOffset = 0f, float outerOffset = 0f)
        {
            EnsureStarted();
            Span<Vector2> buffer = stackalloc Vector2[4 * (MaxCircleSegments + 1)];
            int count = GenerateRoundedRectBoundary(rect, radius, rotation, origin, buffer);
            ReadOnlySpan<Vector2> boundary = buffer[..count];

            if (thickness > 0f)
            {
                Span<Vector2> inset = stackalloc Vector2[count];
                InsetConvexPolygon(boundary, thickness, inset, closed: true);
                FillAxisGradientBoundary(inset, rect.X, rect.Y, rect.Width, rect.Height, rotation, origin, from, to, horizontal, innerOffset, outerOffset);
                BorderRectangleRounded(rect, radius, borderColor, thickness, rotation, origin);
            }
            else
            {
                FillAxisGradientBoundary(boundary, rect.X, rect.Y, rect.Width, rect.Height, rotation, origin, from, to, horizontal, innerOffset, outerOffset);
            }
        }

        /// <inheritdoc cref="DrawRectangleRoundedGradient(Rectangle,RectCorners,Color,Color,bool,Color,float,float,Vector2?,float,float)"/>
        /// <remarks><paramref name="position"/>/<paramref name="size"/> are truncated to <see cref="Rectangle"/>'s integer fields.</remarks>
        public void DrawRectangleRoundedGradient(Vector2 position, Vector2 size, RectCorners radius, Color from, Color to, bool horizontal, Color borderColor, float thickness = 1f, float rotation = 0f, Vector2? origin = null, float innerOffset = 0f, float outerOffset = 0f)
            => DrawRectangleRoundedGradient(new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), radius, from, to, horizontal, borderColor, thickness, rotation, origin, innerOffset, outerOffset);

        // ==================================================================
        // CHAMFERED RECTANGLES
        // ==================================================================

        /// <summary>
        /// Samples a chamfered rectangle's boundary (2 points per corner — where the cut
        /// starts and ends — plus the straight edges between them, same corner order as
        /// <see cref="GenerateRoundedRectBoundary"/>: TR → BR → BL → TL) into
        /// <paramref name="buffer"/>, returning how many points were written (at most 8).
        /// Each corner's chamfer size is independently clamped to half the shorter side,
        /// same simple per-corner clamp as the rounded rectangle (doesn't check pairwise
        /// sums). A zero-chamfer corner degenerates to its single sharp point. Rotation
        /// (radians) is applied about <paramref name="origin"/> (rect center if null) after
        /// the unrotated boundary is built.
        /// </summary>
        private static int GenerateChamferRectBoundary(Rectangle rect, RectCorners chamfer, float rotation, Vector2? origin, Span<Vector2> buffer)
        {
            float x0 = rect.X, y0 = rect.Y, x1 = rect.X + rect.Width, y1 = rect.Y + rect.Height;
            float maxC = MathF.Min(MathF.Abs(rect.Width), MathF.Abs(rect.Height)) * 0.5f;
            float cTL = Math.Clamp(chamfer.TopLeft, 0f, maxC);
            float cTR = Math.Clamp(chamfer.TopRight, 0f, maxC);
            float cBR = Math.Clamp(chamfer.BottomRight, 0f, maxC);
            float cBL = Math.Clamp(chamfer.BottomLeft, 0f, maxC);

            int count = 0;
            AppendChamferCorner(buffer, ref count, cTR, new Vector2(x1 - cTR, y0), new Vector2(x1, y0 + cTR));
            AppendChamferCorner(buffer, ref count, cBR, new Vector2(x1, y1 - cBR), new Vector2(x1 - cBR, y1));
            AppendChamferCorner(buffer, ref count, cBL, new Vector2(x0 + cBL, y1), new Vector2(x0, y1 - cBL));
            AppendChamferCorner(buffer, ref count, cTL, new Vector2(x0, y0 + cTL), new Vector2(x0 + cTL, y0));

            if (rotation != 0f)
            {
                Vector2 pivot = origin.HasValue ? new Vector2(rect.X + origin.Value.X, rect.Y + origin.Value.Y) : new Vector2(rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.5f);
                float cos = MathF.Cos(rotation), sin = MathF.Sin(rotation);
                for (int i = 0; i < count; i++)
                {
                    Vector2 d = buffer[i] - pivot;
                    buffer[i] = pivot + new Vector2(d.X * cos - d.Y * sin, d.X * sin + d.Y * cos);
                }
            }

            return count;
        }

        private static void AppendChamferCorner(Span<Vector2> buffer, ref int count, float c, Vector2 entry, Vector2 exit)
        {
            if (c <= 0.01f) { buffer[count++] = entry; return; } // entry == exit == the sharp corner when c is ~0
            buffer[count++] = entry;
            buffer[count++] = exit;
        }

        /// <summary>Draws a filled chamfered rectangle, no border — a rectangle whose corners are cut straight across instead of rounded.</summary>
        /// <param name="chamfer">
        /// How far each corner's cut reaches back along its two edges, per corner
        /// (implicitly convertible from a single float for all four). Each corner is
        /// independently clamped to half the shorter side.
        /// </param>
        /// <param name="rotation">Rotation in radians, about the rect's own center unless <paramref name="origin"/> is given.</param>
        public void FillRectangleChamfer(Rectangle rect, RectCorners chamfer, Color color, float rotation = 0f, Vector2? origin = null)
        {
            EnsureStarted();
            Span<Vector2> buffer = stackalloc Vector2[8];
            int count = GenerateChamferRectBoundary(rect, chamfer, rotation, origin, buffer);
            FillPolygon(buffer[..count], color);
        }

        /// <inheritdoc cref="FillRectangleChamfer(Rectangle,RectCorners,Color,float,Vector2?)"/>
        /// <remarks><paramref name="position"/>/<paramref name="size"/> are truncated to <see cref="Rectangle"/>'s integer fields.</remarks>
        public void FillRectangleChamfer(Vector2 position, Vector2 size, RectCorners chamfer, Color color, float rotation = 0f, Vector2? origin = null)
            => FillRectangleChamfer(new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), chamfer, color, rotation, origin);

        /// <summary>
        /// Draws a chamfered rectangle's border only, growing inward from its own edges (both
        /// the straight sides and the corner cuts) via <see cref="InsetConvexPolygon"/> — the
        /// border never extends past the rectangle. Stroked with a plain
        /// <see cref="LineJoin.Miter"/> pass, which is exactly correct here since chamfer
        /// corners are flat cuts to begin with — a miter join between two already-straight
        /// cut edges reproduces the cut exactly, no approximation needed.
        /// </summary>
        public void BorderRectangleChamfer(Rectangle rect, RectCorners chamfer, Color color, float thickness = 1f, float rotation = 0f, Vector2? origin = null)
        {
            if (thickness <= 0f) return;
            EnsureStarted();
            Span<Vector2> buffer = stackalloc Vector2[8];
            int count = GenerateChamferRectBoundary(rect, chamfer, rotation, origin, buffer);
            ReadOnlySpan<Vector2> boundary = buffer[..count];

            Span<Vector2> inset = stackalloc Vector2[count];
            InsetConvexPolygon(boundary, thickness * 0.5f, inset, closed: true);
            BuildOutlineGeometry(inset, thickness, color, closed: true, LineJoin.Miter);
        }

        /// <inheritdoc cref="BorderRectangleChamfer(Rectangle,RectCorners,Color,float,float,Vector2?)"/>
        /// <remarks><paramref name="position"/>/<paramref name="size"/> are truncated to <see cref="Rectangle"/>'s integer fields.</remarks>
        public void BorderRectangleChamfer(Vector2 position, Vector2 size, RectCorners chamfer, Color color, float thickness = 1f, float rotation = 0f, Vector2? origin = null)
            => BorderRectangleChamfer(new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), chamfer, color, thickness, rotation, origin);

        /// <summary>Draws a chamfered rectangle with both fill and border (same color), border growing inward.</summary>
        public void DrawRectangleChamfer(Rectangle rect, RectCorners chamfer, Color color, float thickness = 1f, float rotation = 0f, Vector2? origin = null)
            => DrawRectangleChamfer(rect, chamfer, color, color, thickness, rotation, origin);

        /// <inheritdoc cref="DrawRectangleChamfer(Rectangle,RectCorners,Color,float,float,Vector2?)"/>
        /// <remarks><paramref name="position"/>/<paramref name="size"/> are truncated to <see cref="Rectangle"/>'s integer fields.</remarks>
        public void DrawRectangleChamfer(Vector2 position, Vector2 size, RectCorners chamfer, Color color, float thickness = 1f, float rotation = 0f, Vector2? origin = null)
            => DrawRectangleChamfer(new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), chamfer, color, thickness, rotation, origin);

        /// <summary>Draws a chamfered rectangle with an independently colored fill and border, border growing inward.</summary>
        public void DrawRectangleChamfer(Rectangle rect, RectCorners chamfer, Color fillColor, Color borderColor, float thickness = 1f, float rotation = 0f, Vector2? origin = null)
        {
            FillRectangleChamfer(rect, chamfer, fillColor, rotation, origin);
            if (thickness > 0f) BorderRectangleChamfer(rect, chamfer, borderColor, thickness, rotation, origin);
        }

        /// <inheritdoc cref="DrawRectangleChamfer(Rectangle,RectCorners,Color,Color,float,float,Vector2?)"/>
        /// <remarks><paramref name="position"/>/<paramref name="size"/> are truncated to <see cref="Rectangle"/>'s integer fields.</remarks>
        public void DrawRectangleChamfer(Vector2 position, Vector2 size, RectCorners chamfer, Color fillColor, Color borderColor, float thickness = 1f, float rotation = 0f, Vector2? origin = null)
            => DrawRectangleChamfer(new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), chamfer, fillColor, borderColor, thickness, rotation, origin);

        /// <summary>
        /// Draws a chamfered rectangle filled with a linear gradient along its own H/V axis
        /// (rotates with the shape), no border. <paramref name="innerOffset"/>/
        /// <paramref name="outerOffset"/> work exactly as in <see cref="FillRectangleGradient(float,float,float,float,Color,Color,bool,float,float)"/>.
        /// </summary>
        public void FillRectangleChamferGradient(Rectangle rect, RectCorners chamfer, Color from, Color to, bool horizontal, float rotation = 0f, Vector2? origin = null, float innerOffset = 0f, float outerOffset = 0f)
        {
            EnsureStarted();
            Span<Vector2> buffer = stackalloc Vector2[8];
            int count = GenerateChamferRectBoundary(rect, chamfer, rotation, origin, buffer);
            FillAxisGradientBoundary(buffer[..count], rect.X, rect.Y, rect.Width, rect.Height, rotation, origin, from, to, horizontal, innerOffset, outerOffset);
        }

        /// <inheritdoc cref="FillRectangleChamferGradient(Rectangle,RectCorners,Color,Color,bool,float,Vector2?,float,float)"/>
        /// <remarks><paramref name="position"/>/<paramref name="size"/> are truncated to <see cref="Rectangle"/>'s integer fields.</remarks>
        public void FillRectangleChamferGradient(Vector2 position, Vector2 size, RectCorners chamfer, Color from, Color to, bool horizontal, float rotation = 0f, Vector2? origin = null, float innerOffset = 0f, float outerOffset = 0f)
            => FillRectangleChamferGradient(new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), chamfer, from, to, horizontal, rotation, origin, innerOffset, outerOffset);

        /// <summary>
        /// Draws a chamfered rectangle with a linear gradient fill and a solid border. The
        /// gradient's own boundary is the chamfered rectangle's boundary inset by
        /// <paramref name="thickness"/> via <see cref="InsetConvexPolygon"/> — stops exactly
        /// where the border begins, same rule as the other shapes' gradients.
        /// </summary>
        public void DrawRectangleChamferGradient(Rectangle rect, RectCorners chamfer, Color from, Color to, bool horizontal, Color borderColor, float thickness = 1f, float rotation = 0f, Vector2? origin = null, float innerOffset = 0f, float outerOffset = 0f)
        {
            EnsureStarted();
            Span<Vector2> buffer = stackalloc Vector2[8];
            int count = GenerateChamferRectBoundary(rect, chamfer, rotation, origin, buffer);
            ReadOnlySpan<Vector2> boundary = buffer[..count];

            if (thickness > 0f)
            {
                Span<Vector2> inset = stackalloc Vector2[count];
                InsetConvexPolygon(boundary, thickness, inset, closed: true);
                FillAxisGradientBoundary(inset, rect.X, rect.Y, rect.Width, rect.Height, rotation, origin, from, to, horizontal, innerOffset, outerOffset);
                BorderRectangleChamfer(rect, chamfer, borderColor, thickness, rotation, origin);
            }
            else
            {
                FillAxisGradientBoundary(boundary, rect.X, rect.Y, rect.Width, rect.Height, rotation, origin, from, to, horizontal, innerOffset, outerOffset);
            }
        }

        /// <inheritdoc cref="DrawRectangleChamferGradient(Rectangle,RectCorners,Color,Color,bool,Color,float,float,Vector2?,float,float)"/>
        /// <remarks><paramref name="position"/>/<paramref name="size"/> are truncated to <see cref="Rectangle"/>'s integer fields.</remarks>
        public void DrawRectangleChamferGradient(Vector2 position, Vector2 size, RectCorners chamfer, Color from, Color to, bool horizontal, Color borderColor, float thickness = 1f, float rotation = 0f, Vector2? origin = null, float innerOffset = 0f, float outerOffset = 0f)
            => DrawRectangleChamferGradient(new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y), chamfer, from, to, horizontal, borderColor, thickness, rotation, origin, innerOffset, outerOffset);

        // ==================================================================
        // CIRCLES & ELLIPSES
        // ==================================================================

        /// <summary>Draws a filled circle, no border, with an automatically chosen segment count.</summary>
        public void FillCircle(Vector2 center, float radius, Color color)
            => FillEllipse(center, radius, radius, SegmentsForArc(radius, MathHelper.TwoPi), color, color);

        /// <inheritdoc cref="FillCircle(Vector2,float,Color)"/>
        public void FillCircle(float x, float y, float radius, Color color)
            => FillCircle(new Vector2(x, y), radius, color);

        /// <summary>Draws a filled circle with an explicit segment count.</summary>
        public void FillCircle(Vector2 center, float radius, int segments, Color color)
            => FillEllipse(center, radius, radius, segments, color, color);

        /// <summary>
        /// Draws a circle's border only, growing inward from <paramref name="radius"/> — the
        /// border never extends past it. Clamped so a thick border on a small circle
        /// degenerates gracefully to a full fill.
        /// </summary>
        public void BorderCircle(Vector2 center, float radius, Color color, float thickness = 1f)
        {
            if (thickness <= 0f || radius <= 0f) return;
            float t = Math.Min(thickness, radius);
            DrawRing(center, radius - t, radius, 0f, 1f, SegmentsForArc(radius, MathHelper.TwoPi), color);
        }

        /// <summary>Draws a circle with both fill and border (same color), border growing inward.</summary>
        public void DrawCircle(Vector2 center, float radius, Color color, float thickness = 1f)
            => DrawCircle(center, radius, color, color, thickness);

        /// <summary>Draws a circle with an independently colored fill and border, border growing inward.</summary>
        public void DrawCircle(Vector2 center, float radius, Color fillColor, Color borderColor, float thickness = 1f)
        {
            FillCircle(center, radius, fillColor);
            if (thickness > 0f) BorderCircle(center, radius, borderColor, thickness);
        }

        /// <summary>Draws a filled ellipse, no border.</summary>
        public void FillEllipse(Vector2 center, float radiusH, float radiusV, Color color, float rotation = 0f)
            => FillEllipse(center, radiusH, radiusV,
                           SegmentsForArc(Math.Max(radiusH, radiusV), MathHelper.TwoPi), color, color, rotation);

        /// <summary>
        /// Draws a filled ellipse as a triangle fan, optionally gradient-filled.
        /// <paramref name="rotation"/> (radians) turns the ellipse's own H/V axes about
        /// <paramref name="center"/> — the only way to tilt an ellipse at all, since
        /// <paramref name="radiusH"/>/<paramref name="radiusV"/> alone are always screen-aligned.
        /// </summary>
        /// <param name="segments">Number of segments around the perimeter.</param>
        /// <param name="inner">Color at the center.</param>
        /// <param name="outer">Color at the rim.</param>
        public void FillEllipse(Vector2 center, float radiusH, float radiusV, int segments, Color inner, Color outer, float rotation = 0f)
        {
            EnsureStarted();
            if (segments < 3 || radiusH <= 0f || radiusV <= 0f) return;

            int b = Reserve(segments + 2, segments * 3);

            float step = 1f / segments;
            float cos = MathF.Cos(rotation), sin = MathF.Sin(rotation);

            PushVertex(center, inner);
            for (int i = 0; i <= segments; i++)
            {
                Vector2 u = SampleUnitCircle(i * step);
                float lx = u.X * radiusH, ly = u.Y * radiusV;
                PushVertex(center.X + lx * cos - ly * sin, center.Y + lx * sin + ly * cos, outer);
            }
            for (int i = 0; i < segments; i++)
                PushTriangleIndices(b, b + 1 + i, b + 2 + i);
        }

        /// <summary>
        /// Draws an ellipse's border only, growing inward from <paramref name="radiusH"/>/
        /// <paramref name="radiusV"/> — the border never extends past them. Clamped so a
        /// thick border on a small ellipse degenerates gracefully to a full fill.
        /// <paramref name="rotation"/> works the same as <see cref="FillEllipse(Vector2,float,float,int,Color,Color,float)"/>'s.
        /// </summary>
        public void BorderEllipse(Vector2 center, float radiusH, float radiusV, Color color, float thickness = 1f, float rotation = 0f)
        {
            EnsureStarted();
            if (thickness <= 0f || radiusH <= 0f || radiusV <= 0f) return;

            int segments = SegmentsForArc(Math.Max(radiusH, radiusV), MathHelper.TwoPi);
            float t = Math.Min(thickness, Math.Min(radiusH, radiusV));

            int pointCount = segments + 1; // close the loop
            int b = Reserve(pointCount * 2, segments * 2 * 3);

            float step = 1f / segments;
            float cos = MathF.Cos(rotation), sin = MathF.Sin(rotation);

            for (int i = 0; i <= segments; i++)
            {
                Vector2 u = SampleUnitCircle(i * step);
                float lxOuter = u.X * radiusH, lyOuter = u.Y * radiusV;
                float lxInner = u.X * (radiusH - t), lyInner = u.Y * (radiusV - t);
                PushVertex(center.X + lxOuter * cos - lyOuter * sin, center.Y + lxOuter * sin + lyOuter * cos, color); // exterior = the true radius
                PushVertex(center.X + lxInner * cos - lyInner * sin, center.Y + lxInner * sin + lyInner * cos, color); // interior, pulled inward by t
            }

            for (int i = 0; i < segments; i++)
            {
                int i0 = b + i * 2;
                int i1 = b + (i + 1) * 2;
                PushTriangleIndices(i0, i0 + 1, i1 + 1);
                PushTriangleIndices(i0, i1 + 1, i1);
            }
        }

        /// <summary>Draws an ellipse with both fill and border (same color), border growing inward.</summary>
        public void DrawEllipse(Vector2 center, float radiusH, float radiusV, Color color, float thickness = 1f, float rotation = 0f)
            => DrawEllipse(center, radiusH, radiusV, color, color, thickness, rotation);

        /// <summary>Draws an ellipse with an independently colored fill and border, border growing inward.</summary>
        public void DrawEllipse(Vector2 center, float radiusH, float radiusV, Color fillColor, Color borderColor, float thickness = 1f, float rotation = 0f)
        {
            FillEllipse(center, radiusH, radiusV, fillColor, rotation);
            if (thickness > 0f) BorderEllipse(center, radiusH, radiusV, borderColor, thickness, rotation);
        }

        /// <summary>
        /// Draws an ellipse filled with a radial gradient from <paramref name="inner"/>
        /// (center) to <paramref name="outer"/> (rim), no border. <paramref name="innerOffset"/>
        /// holds solid <paramref name="inner"/> color from the center out to that distance
        /// before the gradient starts; <paramref name="outerOffset"/> holds solid
        /// <paramref name="outer"/> color from the rim inward by that distance. Both together:
        /// the gradient only runs in the band between them. If they'd overlap, both are
        /// scaled down proportionally to fit (same "clamp gracefully" precedent as elsewhere,
        /// e.g. the ellipse-thickness clamp, rather than throwing). With both left at 0 this
        /// is the same single continuous fan gradient <see cref="FillEllipse(Vector2,float,float,int,Color,Color,float)"/> already draws.
        /// <paramref name="rotation"/> turns the ellipse's own H/V axes, same as <see cref="FillEllipse(Vector2,float,float,int,Color,Color,float)"/>'s.
        /// </summary>
        public void FillEllipseGradient(Vector2 center, float radiusH, float radiusV, Color inner, Color outer, float rotation = 0f, float innerOffset = 0f, float outerOffset = 0f)
        {
            EnsureStarted();
            if (radiusH <= 0f || radiusV <= 0f) return;

            innerOffset = MathF.Max(0f, innerOffset);
            outerOffset = MathF.Max(0f, outerOffset);
            float maxRadius = MathF.Min(radiusH, radiusV);
            float total = innerOffset + outerOffset;
            if (total > maxRadius && total > 0f)
            {
                float scale = maxRadius / total;
                innerOffset *= scale;
                outerOffset *= scale;
            }
            float gradientOuterScale = 1f - outerOffset / maxRadius;

            int segments = SegmentsForArc(MathF.Max(radiusH, radiusV), MathHelper.TwoPi);
            float step = 1f / segments;

            // Ring list from the fan's rim outward: 1 ring if both offsets are 0 (plain
            // gradient, matches FillEllipse's own fan exactly), up to 3 if both are set.
            Span<float> ringScale = stackalloc float[3];
            Span<Color> ringColors = stackalloc Color[3];
            int ringCount = 0;
            if (innerOffset > 0f) { ringScale[ringCount] = innerOffset / maxRadius; ringColors[ringCount] = inner; ringCount++; }
            ringScale[ringCount] = gradientOuterScale; ringColors[ringCount] = outer; ringCount++;
            if (outerOffset > 0f) { ringScale[ringCount] = 1f; ringColors[ringCount] = outer; ringCount++; }

            int ringVerts = segments + 1;
            int totalVerts = 1 + ringVerts * ringCount;
            int totalIndices = segments * 3 + segments * 6 * (ringCount - 1);
            int b = Reserve(totalVerts, totalIndices);

            Span<Vector2> rim = stackalloc Vector2[ringVerts];
            for (int i = 0; i <= segments; i++) rim[i] = SampleUnitCircle(i * step);
            float cos = MathF.Cos(rotation), sin = MathF.Sin(rotation);

            PushVertex(center, inner);
            for (int i = 0; i <= segments; i++)
            {
                Vector2 u = rim[i];
                float lx = u.X * radiusH * ringScale[0], ly = u.Y * radiusV * ringScale[0];
                PushVertex(center.X + lx * cos - ly * sin, center.Y + lx * sin + ly * cos, ringColors[0]);
            }
            for (int i = 0; i < segments; i++)
                PushTriangleIndices(b, b + 1 + i, b + 2 + i);

            int prevBase = b + 1;
            for (int r = 1; r < ringCount; r++)
            {
                int currBase = prevBase + ringVerts;
                for (int i = 0; i <= segments; i++)
                {
                    Vector2 u = rim[i];
                    float lx = u.X * radiusH * ringScale[r], ly = u.Y * radiusV * ringScale[r];
                    PushVertex(center.X + lx * cos - ly * sin, center.Y + lx * sin + ly * cos, ringColors[r]);
                }
                for (int i = 0; i < segments; i++)
                {
                    int i0 = prevBase + i, i1 = currBase + i;
                    PushTriangleIndices(i0, i1, i1 + 1);
                    PushTriangleIndices(i0, i1 + 1, i0 + 1);
                }
                prevBase = currBase;
            }
        }

        /// <inheritdoc cref="FillEllipseGradient(Vector2,float,float,Color,Color,float,float,float)"/>
        /// <remarks>No <c>rotation</c> param — a radial gradient on a true circle is rotationally symmetric, so it wouldn't do anything. Use <see cref="FillCircleGradientLinear"/> if you want a rotatable (non-radial) gradient.</remarks>
        public void FillCircleGradient(Vector2 center, float radius, Color inner, Color outer, float innerOffset = 0f, float outerOffset = 0f)
            => FillEllipseGradient(center, radius, radius, inner, outer, 0f, innerOffset, outerOffset);

        /// <summary>
        /// Draws a circle with a radial gradient fill and a solid border. The gradient's own
        /// <paramref name="radius"/> is <c>radius - thickness</c> — it stops exactly where the
        /// border begins — and <paramref name="innerOffset"/>/<paramref name="outerOffset"/>
        /// are then measured from that inner boundary, not the full radius: e.g. radius 100,
        /// thickness 10, outerOffset 20 → the gradient's outer solid margin starts at 70.
        /// </summary>
        public void DrawCircleGradient(Vector2 center, float radius, Color innerFill, Color outerFill, Color borderColor, float thickness = 1f, float innerOffset = 0f, float outerOffset = 0f)
        {
            float fillRadius = MathF.Max(0f, radius - MathF.Max(0f, thickness));
            FillCircleGradient(center, fillRadius, innerFill, outerFill, innerOffset, outerOffset);
            if (thickness > 0f) BorderCircle(center, radius, borderColor, thickness);
        }

        /// <summary>
        /// Draws a circle filled with a straight (non-radial) linear gradient from
        /// <paramref name="from"/> to <paramref name="to"/> along an axis through the center,
        /// no border — unlike <see cref="FillCircleGradient"/>'s rings, the color varies
        /// across the shape in one direction, like <see cref="FillRectangleGradient(float,float,float,float,Color,Color,bool,float,float)"/>.
        /// <paramref name="horizontal"/> picks the default axis (true = left→right, false =
        /// top→bottom); <paramref name="rotation"/> (radians) turns that axis, so top-to-bottom
        /// is just <c>horizontal: false</c> and any other direction is one rotation away — no
        /// separate method per direction. <paramref name="innerOffset"/>/<paramref name="outerOffset"/>
        /// hold solid color at each end of the axis before the fade starts, same rule as
        /// <see cref="FillRectangleGradient(float,float,float,float,Color,Color,bool,float,float)"/>.
        /// </summary>
        public void FillCircleGradientLinear(Vector2 center, float radius, Color from, Color to, bool horizontal = true, float rotation = 0f, float innerOffset = 0f, float outerOffset = 0f)
        {
            EnsureStarted();
            if (radius <= 0f) return;

            int segments = SegmentsForArc(radius, MathHelper.TwoPi);
            Span<Vector2> boundary = stackalloc Vector2[segments];
            float step = 1f / segments;
            for (int i = 0; i < segments; i++)
                boundary[i] = center + SampleUnitCircle(i * step) * radius;

            FillAxisGradientBoundary(boundary, center.X - radius, center.Y - radius, radius * 2f, radius * 2f, rotation, null, from, to, horizontal, innerOffset, outerOffset);
        }

        /// <summary>
        /// Draws a circle with a straight linear gradient fill (see <see cref="FillCircleGradientLinear"/>)
        /// and a solid border. The gradient's own radius is <c>radius - thickness</c> — stops
        /// exactly where the border begins, same rule as <see cref="DrawCircleGradient"/>.
        /// </summary>
        public void DrawCircleGradientLinear(Vector2 center, float radius, Color from, Color to, Color borderColor, bool horizontal = true, float thickness = 1f, float rotation = 0f, float innerOffset = 0f, float outerOffset = 0f)
        {
            float fillRadius = MathF.Max(0f, radius - MathF.Max(0f, thickness));
            FillCircleGradientLinear(center, fillRadius, from, to, horizontal, rotation, innerOffset, outerOffset);
            if (thickness > 0f) BorderCircle(center, radius, borderColor, thickness);
        }

        /// <inheritdoc cref="DrawCircleGradient(Vector2,float,Color,Color,Color,float,float,float)"/>
        public void DrawEllipseGradient(Vector2 center, float radiusH, float radiusV, Color innerFill, Color outerFill, Color borderColor, float thickness = 1f, float rotation = 0f, float innerOffset = 0f, float outerOffset = 0f)
        {
            float t = MathF.Max(0f, thickness);
            FillEllipseGradient(center, MathF.Max(0f, radiusH - t), MathF.Max(0f, radiusV - t), innerFill, outerFill, rotation, innerOffset, outerOffset);
            if (thickness > 0f) BorderEllipse(center, radiusH, radiusV, borderColor, thickness, rotation);
        }

        // ==================================================================
        // SECTORS & RINGS
        // ==================================================================

        /// <summary>
        /// Draws a filled circle sector (pie slice).
        /// </summary>
        /// <param name="startAngle">Start of the sweep, normalized to [0..1] where 1 is a full turn.</param>
        /// <param name="endAngle">End of the sweep, same units.</param>
        public void DrawCircleSector(Vector2 center, float radius, float startAngle, float endAngle, Color color)
            => DrawCircleSector(center, radius, startAngle, endAngle,
                   SegmentsForArc(radius, (endAngle - startAngle) * MathHelper.TwoPi), color);

        /// <inheritdoc cref="DrawCircleSector(Vector2,float,float,float,Color)"/>
        public void DrawCircleSector(Vector2 center, float radius, float startAngle, float endAngle, int segments, Color color)
        {
            EnsureStarted();
            if (segments < 1 || radius <= 0f) return;

            int b = Reserve(segments + 2, segments * 3);

            PushVertex(center, color);

            float step = (endAngle - startAngle) / segments;
            for (int i = 0; i <= segments; i++)
            {
                Vector2 u = SampleUnitCircle(startAngle + i * step);
                PushVertex(center.X + u.X * radius, center.Y + u.Y * radius, color);
            }

            for (int i = 0; i < segments; i++)
                PushTriangleIndices(b, b + 1 + i, b + 2 + i);
        }

        /// <summary>Draws the outline of a circle sector, including the two radii.</summary>
        public void DrawCircleSectorLines(Vector2 center, float radius, float startAngle, float endAngle, float thickness, Color color)
        {
            int segments = SegmentsForArc(radius, (endAngle - startAngle) * MathHelper.TwoPi);
            DrawRing(center, radius - thickness, radius, startAngle, endAngle, segments, color);

            Vector2 a = SampleUnitCircle(startAngle) * radius + center;
            Vector2 c = SampleUnitCircle(endAngle) * radius + center;
            // One strip (a -> center -> c) with a real joint at center, not two independent
            // DrawLine calls: each of those gets its own flat end cap oriented to its own
            // direction, and since the two directions differ, the flat cuts leave a small
            // uncovered wedge right at the shared point — visible as a notch/gap at the apex.
            DrawLineStrip([a, center, c], thickness, color, LineJoin.Round);
        }

        /// <summary>
        /// Draws a filled ring (annulus) or a partial arc band.
        /// </summary>
        /// <param name="innerRadius">Inner edge radius. Values <= 0 produce a sector.</param>
        /// <param name="startAngle">Normalized start angle, 1 = full turn.</param>
        /// <param name="endAngle">Normalized end angle.</param>
        public void DrawRing(Vector2 center, float innerRadius, float outerRadius,
                     float startAngle, float endAngle, int segments, Color color)
        {
            EnsureStarted();
            if (segments < 1 || outerRadius <= 0f) return;
            if (innerRadius < 0f) innerRadius = 0f;

            int b = Reserve((segments + 1) * 2, segments * 2 * 3);

            float step = (endAngle - startAngle) / segments;

            for (int i = 0; i <= segments; i++)
            {
                Vector2 u = SampleUnitCircle(startAngle + i * step);
                PushVertex(center.X + u.X * outerRadius, center.Y + u.Y * outerRadius, color);
                PushVertex(center.X + u.X * innerRadius, center.Y + u.Y * innerRadius, color);
            }

            for (int i = 0; i < segments; i++)
            {
                int i0 = b + i * 2;
                int i1 = b + (i + 1) * 2;
                PushTriangleIndices(i0, i0 + 1, i1 + 1);
                PushTriangleIndices(i0, i1 + 1, i1);
            }
        }

        /// <inheritdoc cref="DrawRing(Vector2,float,float,float,float,int,Color)"/>
        public void DrawRing(Vector2 center, float innerRadius, float outerRadius, Color color)
            => DrawRing(center, innerRadius, outerRadius, 0f, 1f,
                        SegmentsForArc(outerRadius, MathHelper.TwoPi), color);

        /// <summary>
        /// Draws a ring's outline: the outer and inner arcs, plus (for a partial ring) the
        /// two straight radial lines closing the wedge at <paramref name="startAngle"/>/
        /// <paramref name="endAngle"/>. Unlike this library's other <c>Border*</c> methods, a
        /// ring's own "inward" direction is ambiguous (its inner edge could grow toward the
        /// hole or the filled band), so <paramref name="thickness"/> is applied uniformly —
        /// centered on each arc/edge line — rather than growing inward. Degenerates to a
        /// pie-slice outline (<see cref="DrawCircleSectorLines"/>) when
        /// <paramref name="innerRadius"/> &lt;= 0.
        /// </summary>
        /// <param name="startAngle">Normalized start angle, 1 = full turn.</param>
        /// <param name="endAngle">Normalized end angle.</param>
        /// <param name="thickness">Line thickness; defaults to 1px.</param>
        public void BorderRing(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, int segments, Color color, float thickness = 1f)
        {
            EnsureStarted();
            if (segments < 1 || outerRadius <= 0f) return;
            if (innerRadius <= 0f) { DrawCircleSectorLines(center, outerRadius, startAngle, endAngle, thickness, color); return; }

            // Each arc is drawn as ONE joined strip (LineJoin.Round), not one DrawLine per
            // segment: independent segments each get their own flat, perpendicular end caps,
            // which don't line up on a curve and show as a ring of small sawtooth notches once
            // thickness is more than a pixel or two — worse the more segments (finer the arc).
            int pointCount = segments + 1;
            Span<Vector2> outerPts = pointCount <= MaxStackAllocElements ? stackalloc Vector2[pointCount] : new Vector2[pointCount];
            Span<Vector2> innerPts = pointCount <= MaxStackAllocElements ? stackalloc Vector2[pointCount] : new Vector2[pointCount];

            float step = (endAngle - startAngle) / segments;
            for (int i = 0; i <= segments; i++)
            {
                Vector2 dir = SampleUnitCircle(startAngle + i * step);
                outerPts[i] = center + dir * outerRadius;
                innerPts[i] = center + dir * innerRadius;
            }

            bool isFullTurn = MathF.Abs(MathF.Abs(endAngle - startAngle) - 1f) < 1e-4f;
            if (isFullTurn)
            {
                // The last point duplicates the first (angle wrapped a full turn) — drop it
                // and close the loop properly instead of using two nearly-coincident
                // LineCap.Round end caps, which left a tiny seam/pixel gap right at the wrap.
                BuildOutlineGeometry(outerPts[..segments], thickness, color, closed: true, LineJoin.Round);
                BuildOutlineGeometry(innerPts[..segments], thickness, color, closed: true, LineJoin.Round);
            }
            else
            {
                // One CLOSED loop around the whole wedge outline (inner arc, radial edge, outer
                // arc reversed, radial edge back to start) instead of 2 open arc strips plus 2
                // independent DrawLine radial edges: the outline traced this way shares actual
                // vertices at all 4 corners, so BuildOutlineGeometry puts a real (round) joint
                // there. Stitching 4 separately-rasterized primitives together at those corners
                // left a hairline gap — confirmed by scanning full-circle transitions pixel by
                // pixel — because two independent draws that merely touch at a shared edge
                // don't reliably rasterize with zero gap, even when the underlying math agrees
                // exactly.
                int loopCount = pointCount * 2;
                Span<Vector2> loop = loopCount <= MaxStackAllocElements ? stackalloc Vector2[loopCount] : new Vector2[loopCount];
                for (int i = 0; i < pointCount; i++) loop[i] = innerPts[i];
                for (int i = 0; i < pointCount; i++) loop[pointCount + i] = outerPts[pointCount - 1 - i];
                // Miter, not Round: the wedge's 4 corners are ~90° cuts (radial edge meeting
                // the arc's tangent), where a sharp square corner is the expected look for a
                // ring slice, not a rounded one.
                BuildOutlineGeometry(loop, thickness, color, closed: true, LineJoin.Miter);
            }
        }

        /// <inheritdoc cref="BorderRing(Vector2,float,float,float,float,int,Color,float)"/>
        public void BorderRing(Vector2 center, float innerRadius, float outerRadius, Color color, float thickness = 1f)
            => BorderRing(center, innerRadius, outerRadius, 0f, 1f,
                          SegmentsForArc(outerRadius, MathHelper.TwoPi), color, thickness);

        // ==================================================================
        // POLYGONS
        // ==================================================================

        /// <summary>Draws a filled regular polygon, no border.</summary>
        /// <param name="rotation">Rotation in radians.</param>
        public void FillPoly(Vector2 center, int sides, float radius, Color color, float rotation = 0f)
        {
            EnsureStarted();
            if (sides < 3) return;

            int b = Reserve(sides + 2, sides * 3);
            float rotationTurns = rotation / MathHelper.TwoPi;

            PushVertex(center, color);

            float step = 1f / sides;
            for (int i = 0; i <= sides; i++)
            {
                Vector2 u = SampleUnitCircle(rotationTurns + i * step);
                PushVertex(center.X + u.X * radius, center.Y + u.Y * radius, color);
            }

            for (int i = 0; i < sides; i++)
                PushTriangleIndices(b, b + 1 + i, b + 2 + i);
        }

        /// <summary>
        /// Draws a regular polygon's border only, growing inward from <paramref name="radius"/> —
        /// the border never extends past it. Unlike the old per-edge <c>DrawLine</c> fast path
        /// this replaces (which left small gaps at each corner, since independent line segments
        /// don't join), every join style here shares the same proper-corner outline builder.
        /// For <see cref="LineJoin.Miter"/> (default), pre-insets via <see cref="InsetConvexPolygon"/>
        /// then strokes symmetrically. For Round/Bevel, strokes the true vertices directly with
        /// an inward-only stroke instead — see <see cref="BuildRoundedOutlineGeometry"/>'s
        /// <c>inwardOnly</c> doc for why pre-insetting first would be wrong there.
        /// </summary>
        public void BorderPoly(Vector2 center, int sides, float radius, Color color, float thickness = 1f, float rotation = 0f, LineJoin join = LineJoin.Miter, float? jointRadius = null)
        {
            if (thickness <= 0f || sides < 3) return;
            EnsureStarted();

            float rotationTurns = rotation / MathHelper.TwoPi;
            float step = 1f / sides;
            Span<Vector2> pts = sides <= MaxStackAllocElements ? stackalloc Vector2[sides] : new Vector2[sides];
            for (int i = 0; i < sides; i++)
                pts[i] = SampleUnitCircle(rotationTurns + i * step) * radius + center;

            if (join == LineJoin.Miter)
            {
                Span<Vector2> inset = sides <= MaxStackAllocElements ? stackalloc Vector2[sides] : new Vector2[sides];
                InsetConvexPolygon(pts, thickness * 0.5f, inset, closed: true);
                BuildOutlineGeometry(inset, thickness, color, closed: true, join);
                return;
            }

            BuildOutlineGeometry(pts, thickness, color, closed: true, join, LineCap.Butt, jointRadius, inwardOnly: true);
        }

        /// <summary>Draws a regular polygon with both fill and border (same color), border growing inward.</summary>
        public void DrawPoly(Vector2 center, int sides, float radius, Color color, float thickness = 1f, float rotation = 0f, LineJoin join = LineJoin.Miter, float? jointRadius = null)
            => DrawPoly(center, sides, radius, color, color, thickness, rotation, join, jointRadius);

        /// <summary>
        /// Draws a regular polygon with an independently colored fill and border, border growing
        /// inward. For Round/Bevel <paramref name="join"/>, the fill is rounded off the same way
        /// <see cref="BorderPoly"/>'s inward-only stroke rounds its own outer edge (same clamp
        /// against <paramref name="thickness"/>, see <see cref="BuildRoundedCornerBoundary"/>) so
        /// the two always agree, even once the requested radius has to be clamped down.
        /// </summary>
        public void DrawPoly(Vector2 center, int sides, float radius, Color fillColor, Color borderColor, float thickness = 1f, float rotation = 0f, LineJoin join = LineJoin.Miter, float? jointRadius = null)
        {
            if (join == LineJoin.Miter || !jointRadius.HasValue || jointRadius.Value <= 0f || sides < 3 || thickness <= 0f)
            {
                FillPoly(center, sides, radius, fillColor, rotation);
            }
            else
            {
                EnsureStarted();
                float rotationTurns = rotation / MathHelper.TwoPi;
                float step = 1f / sides;
                Span<Vector2> pts = sides <= MaxStackAllocElements ? stackalloc Vector2[sides] : new Vector2[sides];
                for (int i = 0; i < sides; i++)
                    pts[i] = SampleUnitCircle(rotationTurns + i * step) * radius + center;
                // Guard the multiply itself against int overflow for absurd `sides` — compare
                // against the divisor rather than the product, so the stackalloc branch's own
                // size expression never has a chance to overflow.
                Span<Vector2> boundary = sides <= MaxStackAllocElements / (MaxCircleSegments + 1)
                    ? stackalloc Vector2[sides * (MaxCircleSegments + 1)]
                    : new Vector2[sides * (MaxCircleSegments + 1)];
                int count = BuildRoundedCornerBoundary(pts, jointRadius.Value, join, thickness, boundary);
                FillPolygon(boundary[..count], fillColor);
            }
            if (thickness > 0f) BorderPoly(center, sides, radius, borderColor, thickness, rotation, join, jointRadius);
        }

        /// <summary>
        /// Draws a regular polygon filled with a radial gradient from <paramref name="inner"/>
        /// (center) to <paramref name="outer"/> (rim), no border. <paramref name="innerOffset"/>/
        /// <paramref name="outerOffset"/> work exactly as in <see cref="FillCircleGradient"/> —
        /// solid margins before/after the fade, scaled down proportionally if they'd overlap.
        /// </summary>
        public void FillPolyGradient(Vector2 center, int sides, float radius, Color inner, Color outer, float rotation = 0f, float innerOffset = 0f, float outerOffset = 0f)
        {
            EnsureStarted();
            if (sides < 3 || radius <= 0f) return;

            innerOffset = MathF.Max(0f, innerOffset);
            outerOffset = MathF.Max(0f, outerOffset);
            float total = innerOffset + outerOffset;
            if (total > radius && total > 0f)
            {
                float scale = radius / total;
                innerOffset *= scale;
                outerOffset *= scale;
            }
            float gradientOuterRadius = radius - outerOffset;

            float rotationTurns = rotation / MathHelper.TwoPi;
            float step = 1f / sides;

            Span<float> ringRadii = stackalloc float[3];
            Span<Color> ringColors = stackalloc Color[3];
            int ringCount = 0;
            if (innerOffset > 0f) { ringRadii[ringCount] = innerOffset; ringColors[ringCount] = inner; ringCount++; }
            ringRadii[ringCount] = gradientOuterRadius; ringColors[ringCount] = outer; ringCount++;
            if (outerOffset > 0f) { ringRadii[ringCount] = radius; ringColors[ringCount] = outer; ringCount++; }

            int ringVerts = sides + 1;
            int totalVerts = 1 + ringVerts * ringCount;
            int totalIndices = sides * 3 + sides * 6 * (ringCount - 1);
            int b = Reserve(totalVerts, totalIndices);

            Span<Vector2> rim = ringVerts <= MaxStackAllocElements ? stackalloc Vector2[ringVerts] : new Vector2[ringVerts];
            for (int i = 0; i <= sides; i++) rim[i] = SampleUnitCircle(rotationTurns + i * step);

            PushVertex(center, inner);
            for (int i = 0; i <= sides; i++)
            {
                Vector2 u = rim[i];
                PushVertex(center.X + u.X * ringRadii[0], center.Y + u.Y * ringRadii[0], ringColors[0]);
            }
            for (int i = 0; i < sides; i++)
                PushTriangleIndices(b, b + 1 + i, b + 2 + i);

            int prevBase = b + 1;
            for (int r = 1; r < ringCount; r++)
            {
                int currBase = prevBase + ringVerts;
                for (int i = 0; i <= sides; i++)
                {
                    Vector2 u = rim[i];
                    PushVertex(center.X + u.X * ringRadii[r], center.Y + u.Y * ringRadii[r], ringColors[r]);
                }
                for (int i = 0; i < sides; i++)
                {
                    int i0 = prevBase + i, i1 = currBase + i;
                    PushTriangleIndices(i0, i1, i1 + 1);
                    PushTriangleIndices(i0, i1 + 1, i0 + 1);
                }
                prevBase = currBase;
            }
        }

        /// <summary>
        /// Draws a regular polygon with a radial gradient fill and a solid border. The
        /// gradient's own radius is <c>radius - thickness</c> (stops exactly where the
        /// border begins), same rule as <see cref="DrawCircleGradient"/>.
        /// </summary>
        public void DrawPolyGradient(Vector2 center, int sides, float radius, Color innerFill, Color outerFill, Color borderColor, float thickness = 1f, float rotation = 0f, float innerOffset = 0f, float outerOffset = 0f)
        {
            float fillRadius = MathF.Max(0f, radius - MathF.Max(0f, thickness));
            FillPolyGradient(center, sides, fillRadius, innerFill, outerFill, rotation, innerOffset, outerOffset);
            if (thickness > 0f) BorderPoly(center, sides, radius, borderColor, thickness, rotation);
        }

        /// <summary>
        /// Draws a filled arbitrary polygon, no border. Convex input (or a triangle) uses a
        /// cheap triangle fan from the first point; anything else — a concave/reflex shape like
        /// a 5-point star — goes through ear-clipping triangulation instead, since a plain fan
        /// produces wrong-looking triangles there (missing/overlapping wedges, not just a
        /// jagged edge — see DECISIONS.md). <paramref name="points"/> must still describe a
        /// *simple* polygon (edges don't cross themselves); self-intersecting input falls back
        /// to the fan rather than drawing nothing, but isn't guaranteed to look right.
        /// </summary>
        public void FillPolygon(ReadOnlySpan<Vector2> points, Color color)
        {
            EnsureStarted();
            int n = points.Length;
            if (n < 3) return;

            if (n == 3 || IsConvex(points))
            {
                int b = Reserve(n, (n - 2) * 3);
                for (int i = 0; i < n; i++)
                    PushVertex(points[i], color);
                for (int i = 1; i < n - 1; i++)
                    PushTriangleIndices(b, b + i, b + i + 1);
                return;
            }

            int triIndexCount = (n - 2) * 3;
            Span<int> localIndices = triIndexCount <= MaxStackAllocElements ? stackalloc int[triIndexCount] : new int[triIndexCount];
            int written = TriangulateEarClipping(points, localIndices);

            int baseIdx = Reserve(n, (n - 2) * 3);
            for (int i = 0; i < n; i++)
                PushVertex(points[i], color);

            if (written > 0)
            {
                for (int i = 0; i < written; i += 3)
                    PushTriangleIndices(baseIdx + localIndices[i], baseIdx + localIndices[i + 1], baseIdx + localIndices[i + 2]);
            }
            else
            {
                // Ear-clipping got stuck (degenerate/self-intersecting input) — fall back to
                // the old fan rather than drawing nothing.
                for (int i = 1; i < n - 1; i++)
                    PushTriangleIndices(baseIdx, baseIdx + i, baseIdx + i + 1);
            }
        }

        /// <summary>
        /// True if walking <paramref name="points"/> in order always turns the same rotational
        /// way (allowing near-collinear runs) — the condition under which a plain triangle fan
        /// from <c>points[0]</c> is always correct, convex or not.
        /// </summary>
        private static bool IsConvex(ReadOnlySpan<Vector2> points)
        {
            int n = points.Length;
            if (n < 4) return true;

            float sign = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % n];
                Vector2 c = points[(i + 2) % n];
                float cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                if (MathF.Abs(cross) < 1e-7f) continue; // near-collinear: doesn't determine turn direction
                if (sign == 0f) sign = MathF.Sign(cross);
                else if (MathF.Sign(cross) != sign) return false;
            }
            return true;
        }

        /// <summary>
        /// Ear-clipping triangulation for an arbitrary simple polygon (concave allowed; must
        /// not self-intersect) — <see cref="FillPolygon"/>'s fallback once <see cref="IsConvex"/>
        /// says the fast fan isn't safe. Writes up to <c>(points.Length - 2) * 3</c> LOCAL
        /// indices (0-based into <paramref name="points"/>) into <paramref name="outIndices"/>
        /// and returns how many were written, or 0 if triangulation got stuck (degenerate or
        /// self-intersecting input — the caller falls back to the plain fan in that case).
        /// </summary>
        private static int TriangulateEarClipping(ReadOnlySpan<Vector2> points, Span<int> outIndices)
        {
            int n = points.Length;
            if (n < 3) return 0;
            if (n == 3) { outIndices[0] = 0; outIndices[1] = 1; outIndices[2] = 2; return 3; }

            // Overall winding decides which triangle orientation counts as a clippable "ear"
            // versus a reflex (concave) vertex while clipping.
            float area2 = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % n];
                area2 += a.X * b.Y - b.X * a.Y;
            }
            bool ccw = area2 > 0f;

            Span<int> prevIdx = n <= MaxStackAllocElements ? stackalloc int[n] : new int[n];
            Span<int> nextIdx = n <= MaxStackAllocElements ? stackalloc int[n] : new int[n];
            for (int i = 0; i < n; i++)
            {
                prevIdx[i] = (i - 1 + n) % n;
                nextIdx[i] = (i + 1) % n;
            }

            int remaining = n;
            int outCount = 0;
            int current = 0;
            int guard = 0;
            int maxGuard = n * n + n; // generous bound for legitimate simple polygons; only degenerate input exhausts it

            while (remaining > 3 && guard < maxGuard)
            {
                guard++;
                int ip = prevIdx[current], inx = nextIdx[current];
                Vector2 a = points[ip], b = points[current], c = points[inx];

                float cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                bool isConvexVertex = ccw ? cross > 0f : cross < 0f;

                if (isConvexVertex && !AnyRemainingPointInTriangle(points, nextIdx, inx, ip, a, b, c))
                {
                    outIndices[outCount++] = ip;
                    outIndices[outCount++] = current;
                    outIndices[outCount++] = inx;

                    nextIdx[ip] = inx;
                    prevIdx[inx] = ip;
                    remaining--;
                    current = ip; // re-check the vertex before the one just clipped
                }
                else
                {
                    current = inx;
                }
            }

            if (remaining != 3)
                return 0; // stuck — degenerate/self-intersecting input

            outIndices[outCount++] = prevIdx[current];
            outIndices[outCount++] = current;
            outIndices[outCount++] = nextIdx[current];
            return outCount;
        }

        /// <summary>Any still-remaining vertex (other than the candidate ear's own 3 corners) strictly inside triangle (a,b,c)? Used to reject a topologically-convex ear that another vertex still pokes into.</summary>
        private static bool AnyRemainingPointInTriangle(ReadOnlySpan<Vector2> points, Span<int> nextIdx, int inx, int ip, in Vector2 a, in Vector2 b, in Vector2 c)
        {
            for (int i = nextIdx[inx]; i != ip; i = nextIdx[i])
            {
                if (PointInTriangle(points[i], a, b, c)) return true;
            }
            return false;
        }

        private static bool PointInTriangle(in Vector2 p, in Vector2 a, in Vector2 b, in Vector2 c)
        {
            float d1 = EdgeCross(p, a, b);
            float d2 = EdgeCross(p, b, c);
            float d3 = EdgeCross(p, c, a);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EdgeCross(in Vector2 p, in Vector2 a, in Vector2 b) => (a.X - p.X) * (b.Y - p.Y) - (a.Y - p.Y) * (b.X - p.X);

        /// <summary>
        /// Draws an arbitrary closed polygon's border only, growing inward from its own
        /// edges via <see cref="InsetConvexPolygon"/> — correct for a convex input; a
        /// non-convex (reflex-vertex) input's inward direction isn't guaranteed to resolve
        /// exactly right at every corner (same documented scope limitation as elsewhere).
        /// For <see cref="LineJoin.Round"/>/<see cref="LineJoin.Bevel"/>, strokes the true
        /// vertices directly with an inward-only stroke instead — see
        /// <see cref="BuildRoundedOutlineGeometry"/>'s <c>inwardOnly</c> doc for why
        /// pre-insetting first would be wrong there.
        /// </summary>
        public void BorderPolygon(ReadOnlySpan<Vector2> points, Color color, float thickness = 1f, LineJoin join = LineJoin.Miter, float? jointRadius = null)
        {
            if (thickness <= 0f || points.Length < 2) return;
            EnsureStarted();

            if (join == LineJoin.Miter)
            {
                Span<Vector2> inset = points.Length <= MaxStackAllocElements ? stackalloc Vector2[points.Length] : new Vector2[points.Length];
                InsetConvexPolygon(points, thickness * 0.5f, inset, closed: true);
                BuildOutlineGeometry(inset, thickness, color, closed: true, join);
                return;
            }

            BuildOutlineGeometry(points, thickness, color, closed: true, join, LineCap.Butt, jointRadius, inwardOnly: true);
        }

        /// <summary>Draws an arbitrary polygon with both fill and border (same color), border growing inward.</summary>
        public void DrawPolygon(ReadOnlySpan<Vector2> points, Color color, float thickness = 1f, LineJoin join = LineJoin.Miter, float? jointRadius = null)
            => DrawPolygon(points, color, color, thickness, join, jointRadius);

        /// <summary>
        /// Draws an arbitrary polygon with an independently colored fill and border, border
        /// growing inward. For Round/Bevel <paramref name="join"/>, the fill is rounded off the
        /// same way <see cref="BorderPolygon"/>'s inward-only stroke rounds its own outer edge
        /// (same clamp against <paramref name="thickness"/>, see
        /// <see cref="BuildRoundedCornerBoundary"/>) so the two always agree.
        /// </summary>
        public void DrawPolygon(ReadOnlySpan<Vector2> points, Color fillColor, Color borderColor, float thickness = 1f, LineJoin join = LineJoin.Miter, float? jointRadius = null)
        {
            if (join == LineJoin.Miter || !jointRadius.HasValue || jointRadius.Value <= 0f || thickness <= 0f)
            {
                FillPolygon(points, fillColor);
            }
            else
            {
                EnsureStarted();
                Span<Vector2> boundary = points.Length <= MaxStackAllocElements / (MaxCircleSegments + 1)
                    ? stackalloc Vector2[points.Length * (MaxCircleSegments + 1)]
                    : new Vector2[points.Length * (MaxCircleSegments + 1)];
                int count = BuildRoundedCornerBoundary(points, jointRadius.Value, join, thickness, boundary);
                FillPolygon(boundary[..count], fillColor);
            }
            if (thickness > 0f) BorderPolygon(points, borderColor, thickness, join, jointRadius);
        }

        /// <summary>
        /// Draws a filled arbitrary polygon with each corner rounded by
        /// <paramref name="cornerRadius"/> world units, no border pass — the standalone
        /// counterpart to <see cref="DrawPolygon(ReadOnlySpan{Vector2},Color,Color,float,LineJoin,float?)"/>
        /// with <c>join: LineJoin.Round</c> and a matching fill/border color, without the
        /// redundant border stroke. Same convex/star-shaped-from-first-vertex assumption as
        /// <see cref="FillPolygon"/>; each corner's radius is independently clamped so it can't
        /// reach past the midpoint of either adjacent edge.
        /// </summary>
        public void FillPolygonRounded(ReadOnlySpan<Vector2> points, float cornerRadius, Color color)
        {
            if (cornerRadius <= 0.01f) { FillPolygon(points, color); return; }
            EnsureStarted();
            if (points.Length < 3) return;
            float safeRadius = ClampCornerRadiusToFit(points, cornerRadius);
            int maxOut = points.Length * (MaxCircleSegments + 1);
            Span<Vector2> boundary = maxOut <= MaxStackAllocElements ? stackalloc Vector2[maxOut] : new Vector2[maxOut];
            int count = BuildRoundedCornerBoundary(points, safeRadius, LineJoin.Round, NoBorderClampBudget, boundary);
            FillPolygon(boundary[..count], color);
        }

        /// <summary>Equivalent to <see cref="BorderPolygon"/> with <c>join: LineJoin.Round, jointRadius: cornerRadius</c> (pre-clamped so neighboring corners can't overlap, see <see cref="ClampCornerRadiusToFit"/>) — named to match <see cref="FillPolygonRounded"/> for discoverability.</summary>
        public void BorderPolygonRounded(ReadOnlySpan<Vector2> points, float cornerRadius, Color color, float thickness = 1f)
            => BorderPolygon(points, color, thickness, LineJoin.Round, ClampCornerRadiusToFit(points, cornerRadius));

        /// <summary>Draws a rounded-corner polygon with both fill and border (same color), border growing inward.</summary>
        public void DrawPolygonRounded(ReadOnlySpan<Vector2> points, float cornerRadius, Color color, float thickness = 1f)
            => DrawPolygonRounded(points, cornerRadius, color, color, thickness);

        /// <summary>Draws a rounded-corner polygon with an independently colored fill and border, border growing inward. Radius pre-clamped so neighboring corners can't overlap (see <see cref="ClampCornerRadiusToFit"/>).</summary>
        public void DrawPolygonRounded(ReadOnlySpan<Vector2> points, float cornerRadius, Color fillColor, Color borderColor, float thickness = 1f)
            => DrawPolygon(points, fillColor, borderColor, thickness, LineJoin.Round, ClampCornerRadiusToFit(points, cornerRadius));

        /// <summary>
        /// Draws a filled arbitrary polygon fading from <paramref name="from"/> at
        /// <paramref name="points"/>[0] to <paramref name="to"/> at the farthest point, with
        /// every other vertex colored by how far along that span it sits — the fan-fill
        /// generalization of <see cref="FillTriangleGradient"/> to any point count. Uses the
        /// same convex-fast-path/ear-clipping split as <see cref="FillPolygon"/> for correct
        /// triangulation on a concave shape (a star, an arrow, etc.).
        /// </summary>
        public void FillPolygonGradient(ReadOnlySpan<Vector2> points, Color from, Color to)
        {
            EnsureStarted();
            int n = points.Length;
            if (n < 3) return;

            Span<Color> vertexColors = n <= MaxStackAllocElements ? stackalloc Color[n] : new Color[n];
            ComputeDistanceGradientColors(points, from, to, vertexColors);

            int b = Reserve(n, (n - 2) * 3);
            for (int i = 0; i < n; i++)
                PushVertex(points[i], vertexColors[i]);

            if (n == 3 || IsConvex(points))
            {
                for (int i = 1; i < n - 1; i++)
                    PushTriangleIndices(b, b + i, b + i + 1);
                return;
            }

            int triIndexCount = (n - 2) * 3;
            Span<int> localIndices = triIndexCount <= MaxStackAllocElements ? stackalloc int[triIndexCount] : new int[triIndexCount];
            int written = TriangulateEarClipping(points, localIndices);
            if (written > 0)
            {
                for (int i = 0; i < written; i += 3)
                    PushTriangleIndices(b + localIndices[i], b + localIndices[i + 1], b + localIndices[i + 2]);
            }
            else
            {
                for (int i = 1; i < n - 1; i++)
                    PushTriangleIndices(b, b + i, b + i + 1);
            }
        }

        /// <summary>
        /// Fills <paramref name="outColors"/> with a continuous blend from <paramref name="from"/>
        /// (at <paramref name="points"/>[0]) to <paramref name="to"/> (at whichever point is
        /// farthest from it), each vertex weighted by its own distance from points[0] relative
        /// to that farthest one. Continuous rather than the old binary "points[0] gets
        /// <paramref name="from"/>, everything else gets <paramref name="to"/>" scheme: on a
        /// many-sided fan (a star, a rounded polygon's boundary) the binary version made every
        /// wedge except the ones touching points[0] flat-<paramref name="to"/>-colored, which
        /// shows as visible facet seams where adjacent wedges' independent GPU interpolation
        /// disagrees — confirmed by rendering a 5-point star gradient (visible creases) and
        /// fixed by giving every vertex its own, smoothly-varying color instead.
        /// </summary>
        private static void ComputeDistanceGradientColors(ReadOnlySpan<Vector2> points, Color from, Color to, Span<Color> outColors)
        {
            Vector2 origin = points[0];
            float maxDistSq = 0f;
            for (int i = 1; i < points.Length; i++)
            {
                float d = Vector2.DistanceSquared(points[i], origin);
                if (d > maxDistSq) maxDistSq = d;
            }
            float maxDist = MathF.Sqrt(maxDistSq);

            outColors[0] = from;
            for (int i = 1; i < points.Length; i++)
            {
                float t = maxDist > 1e-6f ? Vector2.Distance(points[i], origin) / maxDist : 0f;
                outColors[i] = Color.Lerp(from, to, t);
            }
        }

        /// <summary>
        /// Draws a filled arbitrary polygon with each corner rounded by
        /// <paramref name="cornerRadius"/>, same <paramref name="from"/>/<paramref name="to"/>
        /// distance-based blend as <see cref="FillPolygonGradient"/> — the rounded-corner
        /// counterpart. Each rounded corner's own arc is colored by whichever original vertex
        /// it replaced (see <see cref="FillPolygonGradientByNearestVertex"/>) — that vertex's
        /// smoothly-blended color now, not a flat binary one, so the seams a flat color caused
        /// there are fixed the same way as the sharp-corner version.
        /// </summary>
        public void FillPolygonGradientRounded(ReadOnlySpan<Vector2> points, float cornerRadius, Color from, Color to)
        {
            if (cornerRadius <= 0.01f) { FillPolygonGradient(points, from, to); return; }
            EnsureStarted();
            if (points.Length < 3) return;

            float safeRadius = ClampCornerRadiusToFit(points, cornerRadius);
            int maxOut = points.Length * (MaxCircleSegments + 1);
            Span<Vector2> boundary = maxOut <= MaxStackAllocElements ? stackalloc Vector2[maxOut] : new Vector2[maxOut];
            int count = BuildRoundedCornerBoundary(points, safeRadius, LineJoin.Round, NoBorderClampBudget, boundary);

            Span<Color> vertexColors = points.Length <= MaxStackAllocElements ? stackalloc Color[points.Length] : new Color[points.Length];
            ComputeDistanceGradientColors(points, from, to, vertexColors);
            FillPolygonGradientByNearestVertex(boundary[..count], points, vertexColors);
        }

        /// <summary>
        /// Draws a filled ribbon between two aligned point lists — <paramref name="bottomPoints"/>[i]
        /// connects to <paramref name="topPoints"/>[i] for each i — fading from
        /// <paramref name="bottomColor"/> at the bottom list to <paramref name="topColor"/> at the
        /// top list, no border. Useful for a gradient that follows an arbitrary profile (e.g. a
        /// terrain silhouette) instead of a straight axis like <see cref="FillRectangleGradient(float,float,float,float,Color,Color,bool)"/>.
        /// If the two lists differ in length, only the shorter length is used.
        /// </summary>
        public void FillPolygonGradientTopBottom(ReadOnlySpan<Vector2> bottomPoints, ReadOnlySpan<Vector2> topPoints, Color bottomColor, Color topColor)
        {
            EnsureStarted();
            int n = Math.Min(bottomPoints.Length, topPoints.Length);
            if (n < 2) return;

            int b = Reserve(n * 2, (n - 1) * 6);
            for (int i = 0; i < n; i++)
            {
                PushVertex(bottomPoints[i], bottomColor);
                PushVertex(topPoints[i], topColor);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int i0 = b + i * 2, i1 = b + (i + 1) * 2;
                PushTriangleIndices(i0, i0 + 1, i1 + 1);
                PushTriangleIndices(i0, i1 + 1, i1);
            }
        }

        /// <summary>
        /// Draws an arbitrary polygon with a <paramref name="from"/>→<paramref name="to"/>
        /// gradient fill (first point→from, rest→to) and a solid border. The gradient's own
        /// polygon is inset by <paramref name="thickness"/> via <see cref="InsetConvexPolygon"/> —
        /// stops exactly where the border begins, same rule as the other shapes' gradients.
        /// </summary>
        public void DrawPolygonGradient(ReadOnlySpan<Vector2> points, Color from, Color to, Color borderColor, float thickness = 1f, LineJoin join = LineJoin.Miter, float? jointRadius = null)
        {
            EnsureStarted();
            if (thickness > 0f)
            {
                if (join == LineJoin.Miter || !jointRadius.HasValue || jointRadius.Value <= 0f)
                {
                    Span<Vector2> inset = points.Length <= MaxStackAllocElements ? stackalloc Vector2[points.Length] : new Vector2[points.Length];
                    InsetConvexPolygon(points, thickness, inset, closed: true);
                    FillPolygonGradient(inset, from, to);
                }
                else
                {
                    // Inset distance capped at the rounding radius itself — see DrawTriangleGradient's
                    // matching comment for why (insetting an arc past its own radius inverts it);
                    // BorderPolygon draws on top right after, so any resulting sub-thickness gap
                    // here is simply covered, not a visible seam.
                    Span<Vector2> boundary = points.Length <= MaxStackAllocElements / (MaxCircleSegments + 1)
                        ? stackalloc Vector2[points.Length * (MaxCircleSegments + 1)]
                        : new Vector2[points.Length * (MaxCircleSegments + 1)];
                    int count = BuildRoundedCornerBoundary(points, jointRadius.Value, join, thickness, boundary);
                    Span<Vector2> inset = count <= MaxStackAllocElements ? stackalloc Vector2[count] : new Vector2[count];
                    InsetConvexPolygon(boundary[..count], MathF.Min(thickness, jointRadius.Value), inset, closed: true);
                    FillPolygonGradient(inset, from, to);
                }
                BorderPolygon(points, borderColor, thickness, join, jointRadius);
            }
            else
            {
                FillPolygonGradient(points, from, to);
            }
        }

        /// <summary>
        /// Builds a shared-vertex thick outline for either a closed polygon loop
        /// (<paramref name="closed"/> = true, used directly by <see cref="BorderPolygon"/>/
        /// <see cref="BorderPoly"/>, and via <see cref="InsetConvexPolygon"/> by
        /// <see cref="BorderTriangle"/>/<see cref="BorderRectangle(float,float,float,float,Color,float,float,Vector2?,LineJoin)"/>) or an open
        /// polyline (<paramref name="closed"/> = false, used by
        /// <see cref="DrawLineStrip(ReadOnlySpan{Vector2},float,Color,LineJoin,LineCap,float?)"/>
        /// and the splines). Dispatches to the cheap shared-vertex miter path (default,
        /// unchanged from before <see cref="LineJoin"/> existed) or to
        /// <see cref="BuildRoundedOutlineGeometry"/> for <see cref="LineJoin.Round"/>/
        /// <see cref="LineJoin.Bevel"/>, which also honors <paramref name="cap"/>.
        /// </summary>
        /// <param name="inwardOnly">
        /// When true (only meaningful for <see cref="LineJoin.Round"/>/<see cref="LineJoin.Bevel"/>
        /// — ignored for Miter), <paramref name="points"/> is the TRUE boundary and the stroke
        /// sits entirely inward from it (outer edge flush with the points, inner edge a full
        /// <paramref name="thickness"/> in) instead of the normal symmetric ±thickness/2 straddle.
        /// This is what <c>Border*</c> uses for Round/Bevel: pre-insetting via
        /// <see cref="InsetConvexPolygon"/> first (which IS correct for Miter, since Miter's
        /// offset is linear and self-cancels) is wrong here, because a fillet's tangent-circle
        /// construction is a nonlinear function of the radius and doesn't self-cancel the same
        /// way — see <see cref="BuildRoundedOutlineGeometry"/> for the full story, including why
        /// this alone isn't enough to keep an accompanying <c>Fill*</c> from overflowing it.
        /// </param>
        private void BuildOutlineGeometry(ReadOnlySpan<Vector2> points, float thickness, Color color, bool closed,
            LineJoin join = LineJoin.Miter, LineCap cap = LineCap.Butt, float? jointRadius = null, bool inwardOnly = false)
        {
            if (join == LineJoin.Miter)
            {
                BuildMiterOutlineGeometry(points, thickness, color, closed);
                return;
            }

            BuildRoundedOutlineGeometry(points, thickness, color, closed, join, cap, jointRadius, inwardOnly);
        }

        /// <summary>
        /// Computes the miter offset for a vertex where two segments meet, given their
        /// incoming/outgoing unit directions: the direction/length that keeps a stroke of
        /// the given half-thickness at constant width through the joint, clamped to avoid
        /// spikes on very sharp turns. <c>curr + offset</c> and <c>curr - offset</c> are the
        /// two candidate corner points; which one is "outer" vs "inner" depends on the
        /// polygon's winding and isn't resolved here — see <see cref="InsetConvexPolygon"/>
        /// for the caller that picks between them. Shared by <see cref="BuildMiterOutlineGeometry"/>
        /// (the plain stroke path) and <see cref="InsetConvexPolygon"/> (shrinking a polygon
        /// inward, the basis for every v2 <c>Border*</c>'s "inward-only" guarantee).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector2 ComputeMiterOffset(Vector2 dirIn, Vector2 dirOut, float half)
        {
            Vector2 normalIn = new Vector2(-dirIn.Y, dirIn.X);
            Vector2 normalOut = new Vector2(-dirOut.Y, dirOut.X);

            Vector2 miter = normalIn + normalOut;
            float miterLenSq = miter.LengthSquared();

            if (miterLenSq < 1e-6f)
                return normalIn * half;

            miter = Vector2.Normalize(miter);
            float cosHalfAngle = Vector2.Dot(miter, normalIn);
            float miterLength = half / MathF.Max(cosHalfAngle, 0.2f);
            return miter * miterLength;
        }

        /// <summary>
        /// Shrinks a convex (or star-shaped-around-its-own-centroid) polygon inward by
        /// <paramref name="distance"/>, writing one inset point per input point into
        /// <paramref name="result"/> (same length as <paramref name="points"/>). At each
        /// vertex, <see cref="ComputeMiterOffset"/> gives two candidate points
        /// (<c>curr ± offset</c>); this picks whichever one is closer to the polygon's
        /// centroid — robust regardless of winding direction (CW/CCW), so callers don't
        /// need to reason about or guarantee a particular point order.
        /// </summary>
        /// <remarks>
        /// This is the building block behind every v2 <c>Border*</c>'s "grows inward, never
        /// exceeds the shape" contract: stroke the result of <c>InsetConvexPolygon(points,
        /// thickness/2, ...)</c> with that same <c>thickness</c> via the existing symmetric
        /// outline builder, and the stroke's outer edge lands exactly on the original
        /// boundary. Does not clamp <paramref name="distance"/> against the shape's own
        /// size — callers are responsible for clamping <c>thickness</c> first (matching the
        /// existing precedent in e.g. <c>DrawRectangleLines</c>/<c>DrawRectangleRounded</c>),
        /// same as this project's established "clamp gracefully" pattern elsewhere.
        /// </remarks>
        private static void InsetConvexPolygon(ReadOnlySpan<Vector2> points, float distance, Span<Vector2> result, bool closed)
        {
            int n = points.Length;
            if (n == 0) return;

            Vector2 centroid = Vector2.Zero;
            for (int i = 0; i < n; i++) centroid += points[i];
            centroid /= n;

            for (int i = 0; i < n; i++)
            {
                Vector2 curr = points[i];
                Vector2 dirIn, dirOut;

                if (closed)
                {
                    Vector2 prev = points[(i - 1 + n) % n];
                    Vector2 next = points[(i + 1) % n];
                    dirIn = SafeNormalize(curr - prev);
                    dirOut = SafeNormalize(next - curr);
                }
                else
                {
                    dirOut = (i < n - 1) ? SafeNormalize(points[i + 1] - curr) : Vector2.Zero;
                    dirIn = (i > 0) ? SafeNormalize(curr - points[i - 1]) : Vector2.Zero;
                    if (i == 0) dirIn = dirOut;
                    if (i == n - 1) dirOut = dirIn;
                }

                if (dirIn == Vector2.Zero && dirOut == Vector2.Zero) { result[i] = curr; continue; }

                Vector2 offset = ComputeMiterOffset(dirIn, dirOut, distance);
                Vector2 plus = curr + offset;
                Vector2 minus = curr - offset;
                result[i] = Vector2.DistanceSquared(plus, centroid) < Vector2.DistanceSquared(minus, centroid) ? plus : minus;
            }
        }

        /// <summary>
        /// Builds a shared-vertex thick outline with proper miter joins. Both endpoints of
        /// an open polyline get a flat (non-mitered) cut. See <see cref="BuildOutlineGeometry"/>
        /// for the callers this serves.
        /// </summary>
        private void BuildMiterOutlineGeometry(ReadOnlySpan<Vector2> points, float thickness, Color color, bool closed)
        {
            int n = points.Length;
            if (n < 2 || thickness <= 0f) return;

            float half = thickness * 0.5f;

            int vertCount = n * 2;
            int segmentCount = closed ? n : n - 1;
            int indexCount = segmentCount * 2 * 3;

            int baseVert = Reserve(vertCount, indexCount);

            for (int i = 0; i < n; i++)
            {
                Vector2 curr = points[i];
                Vector2 dirIn, dirOut;

                if (closed)
                {
                    Vector2 prev = points[(i - 1 + n) % n];
                    Vector2 next = points[(i + 1) % n];
                    dirIn = SafeNormalize(curr - prev);
                    dirOut = SafeNormalize(next - curr);
                }
                else
                {
                    dirOut = (i < n - 1) ? SafeNormalize(points[i + 1] - curr) : Vector2.Zero;
                    dirIn = (i > 0) ? SafeNormalize(curr - points[i - 1]) : Vector2.Zero;
                    if (i == 0) dirIn = dirOut;        // flat cap at the open start
                    if (i == n - 1) dirOut = dirIn;    // flat cap at the open end
                }

                Vector2 offset = ComputeMiterOffset(dirIn, dirOut, half);
                PushVertex(curr + offset, color);
                PushVertex(curr - offset, color);
            }

            for (int i = 0; i < segmentCount; i++)
            {
                int i0 = baseVert + i * 2;
                int i1 = baseVert + ((i + 1) % n) * 2;
                PushTriangleIndices(i0 + 0, i0 + 1, i1 + 1);
                PushTriangleIndices(i0 + 0, i1 + 1, i1 + 0);
            }
        }

        /// <summary>
        /// Per-vertex result of <see cref="ComputeJoint"/>: either a proper tangent-trimmed
        /// fillet (<see cref="HasArc"/> true) or nothing (degenerate/near-straight/near-180,
        /// in which case the adjoining segments just meet at the plain vertex, matching the
        /// old flush-corner behavior).
        /// </summary>
        private struct JointInfo
        {
            public bool HasArc;
            public Vector2 Ctr;
            public Vector2 TangentIn, TangentOut;
            public float FromAngle, ToAngle;
            public int Segments;
            public float Side;
            public float ClampedRadius;
            /// <summary>
            /// The properly mitered inner corner at this vertex (only valid when <see cref="HasArc"/>)
            /// — see the assignment site in <see cref="ComputeJoint"/> for why the adjoining
            /// segment quads use this instead of each independently offsetting along their own
            /// normal.
            /// </summary>
            public Vector2 InnerCorner;
        }

        /// <summary>
        /// Computes a proper tangent-based rounded/beveled corner fillet at <paramref name="curr"/>:
        /// a circle of (clamped) <paramref name="radius"/> tangent to the outer edges of both
        /// adjoining segments, on the outer/convex side of the turn only. Unlike a naive "fan
        /// planted at the vertex", this keeps the join seamless with the straight segments for
        /// any radius, at the cost of also needing to trim the segments back to the tangent
        /// points (done by the caller, using <see cref="JointInfo.TangentIn"/>/<see cref="JointInfo.TangentOut"/>
        /// as the new outer corners instead of the plain vertex).
        /// </summary>
        /// <param name="outerOffset">
        /// How far the segments' own OUTER edge sits from <paramref name="curr"/> — <c>half</c>
        /// the stroke thickness for a normal symmetric stroke, or <c>0</c> when this is rounding
        /// the true shape boundary itself (see <see cref="BuildRoundedCornerBoundary"/>), since
        /// <paramref name="curr"/> then IS the true, un-inset vertex. This is the value the
        /// fillet is built relative to (see <c>k</c> below) — NOT necessarily half the stroke
        /// width, despite the name used at most call sites.
        /// </param>
        /// <param name="innerOffset">
        /// How far the segments' own INNER edge sits from <paramref name="curr"/> (the "opposite
        /// wall" the self-intersection guard below protects against) — equal to
        /// <paramref name="outerOffset"/> for a symmetric stroke, or the full stroke thickness
        /// for an inward-only <c>Border*</c> stroke.
        /// </param>
        /// <param name="lenIn">Length of the incoming segment (prev→curr), for the trim clamp.</param>
        /// <param name="lenOut">Length of the outgoing segment (curr→next), for the trim clamp.</param>
        private JointInfo ComputeJoint(Vector2 curr, Vector2 dirIn, Vector2 dirOut, float outerOffset, float innerOffset, float radius, LineJoin join, float lenIn, float lenOut)
        {
            var info = new JointInfo();
            Vector2 normalIn = new Vector2(-dirIn.Y, dirIn.X);
            Vector2 normalOut = new Vector2(-dirOut.Y, dirOut.X);

            // Which side of the joint is the outer (convex, gapped) one: verified against a
            // concrete example (dirIn=(1,0) turning left to dirOut=(0,1), cross>0) where the
            // gap lands on the negative-normal side — so a positive cross picks -1, negative picks +1.
            float cross = dirIn.X * dirOut.Y - dirIn.Y * dirOut.X;
            float side = cross >= 0f ? -1f : 1f;
            info.Side = side;

            float fromAngle = MathF.Atan2(normalIn.Y * side, normalIn.X * side);
            float toAngle = MathF.Atan2(normalOut.Y * side, normalOut.X * side);
            float delta = toAngle - fromAngle;
            while (delta > MathF.PI) delta -= MathHelper.TwoPi;
            while (delta < -MathF.PI) delta += MathHelper.TwoPi;
            if (MathF.Abs(delta) < 1e-4f) return info; // straight enough, nothing to bridge
            toAngle = fromAngle + delta;

            // The fillet center sits on the bisector of the two outer normals (same bisector the
            // original miter join uses). Both "how far the center sits from curr" and "how far
            // each segment must be trimmed back for the arc to stay exactly tangent" fall out of
            // that same bisector geometry — derived and numerically verified against a worked
            // 90°/135° example (see the audit report's join-radius follow-up) rather than copied
            // from a reference, so the guard clamp below is deliberately conservative.
            Vector2 miterSum = normalIn + normalOut;
            float miterLenSq = miterSum.LengthSquared();
            if (miterLenSq < 1e-6f) return info; // near-180 U-turn, degrade to a plain vertex

            Vector2 miter = miterSum / MathF.Sqrt(miterLenSq);
            float cosHalfAngle = Vector2.Dot(miter, normalIn);
            if (MathF.Abs(cosHalfAngle) < 1e-3f) return info; // guard div-by-~0 on a near-flat bisector

            // Trim distances are linear in (radius-outerOffset): u = (radius-outerOffset)*A for
            // the incoming segment, v = (radius-outerOffset)*B for the outgoing one. Clamp
            // |radius-outerOffset| so neither trim exceeds 90% of its own segment's length, AND
            // so the fillet center never drifts more than one inner-offset-width from curr — the
            // first guard alone isn't enough on a sharp corner (small cosHalfAngle amplifies the
            // center's drift), where it can still cross the "opposite wall" at innerOffset and
            // self-intersect it. Both are the same "clamp gracefully rather than break"
            // trade-off as the existing ellipse-thickness clamp (see the audit report's M2).
            float trimSlopeIn = side * Vector2.Dot(miter, dirIn) / cosHalfAngle;
            float trimSlopeOut = -side * Vector2.Dot(miter, dirOut) / cosHalfAngle;
            float maxDelta = innerOffset * MathF.Abs(cosHalfAngle);
            if (MathF.Abs(trimSlopeIn) > 1e-6f) maxDelta = MathF.Min(maxDelta, 0.9f * lenIn / MathF.Abs(trimSlopeIn));
            if (MathF.Abs(trimSlopeOut) > 1e-6f) maxDelta = MathF.Min(maxDelta, 0.9f * lenOut / MathF.Abs(trimSlopeOut));
            float clampedRadius = outerOffset + Math.Clamp(radius - outerOffset, -maxDelta, maxDelta);

            float k = (outerOffset - clampedRadius) * side / cosHalfAngle;
            Vector2 ctr = curr + miter * k;

            info.HasArc = true;
            info.Ctr = ctr;
            info.TangentIn = ctr + normalIn * side * clampedRadius;
            info.TangentOut = ctr + normalOut * side * clampedRadius;
            info.FromAngle = fromAngle;
            info.ToAngle = toAngle;
            info.Segments = join == LineJoin.Round ? Math.Max(1, SegmentsForArc(clampedRadius, MathF.Abs(delta))) : 1;
            info.ClampedRadius = clampedRadius;

            // The inner/concave corner on the SAME bisector, at innerOffset instead of the
            // fillet's own radius — same formula (and same 0.2 floor) as ComputeMiterOffset uses
            // for a plain Miter join, since on this side of curr there's no arc, just a sharp
            // mitered corner. This is NOT the same as naively offsetting each adjoining segment
            // along its own normal (what the caller used to do): on an acute corner, the two
            // edges' normals point in different-enough directions that each one's own inward
            // offset can land past the shape's OTHER edge entirely — this single shared point,
            // like InsetConvexPolygon's own vertex, can't do that by construction.
            info.InnerCorner = curr - miter * (innerOffset / MathF.Max(cosHalfAngle, 0.2f)) * side;
            return info;
        }

        /// <summary>
        /// Rounds every corner of <paramref name="points"/> by <paramref name="radius"/> (Round:
        /// an arc fillet: Bevel: a single straight chord) and writes the resulting boundary —
        /// original straight edges plus the new corner geometry — into <paramref name="buffer"/>.
        /// This is the fix for <c>Draw*</c>'s Round/Bevel corners: earlier, <c>Border*</c> alone
        /// rounded its own outer edge away from the true vertex (via <see cref="ComputeJoint"/>
        /// at outer offset 0) while the accompanying <c>Fill*</c> still went all the way to the
        /// sharp vertex — since a real fillet's trim distance grows sharply on an acute corner
        /// (proportional to <c>1/cos(halfAngle)</c>), that mismatch left a visible sliver of
        /// unrounded fill poking past the rounded border, worse the sharper the corner.
        /// </summary>
        /// <param name="innerOffsetForClamp">
        /// Passed straight through to <see cref="ComputeJoint"/> as its innerOffset — the same
        /// self-intersection clamp a Border's own inward-only stroke uses. <c>Draw*</c> passes
        /// its border <c>thickness</c> here so the fill's rounding is clamped in EXACT lockstep
        /// with <see cref="BuildRoundedOutlineGeometry"/>'s own <c>inwardOnly</c> stroke (both
        /// call <see cref="ComputeJoint"/> with outerOffset 0 and this same value) — otherwise,
        /// when the requested radius can't fit (e.g. a thick border on a tight corner), fill and
        /// border would clamp to two different radii and drift apart again.
        /// </param>
        private int BuildRoundedCornerBoundary(ReadOnlySpan<Vector2> points, float radius, LineJoin join, float innerOffsetForClamp, Span<Vector2> buffer)
        {
            int n = points.Length;
            int count = 0;
            if (radius <= 0.01f)
            {
                for (int i = 0; i < n; i++) buffer[count++] = points[i];
                return count;
            }

            float invTwoPi = 1f / MathHelper.TwoPi;
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = points[(i - 1 + n) % n];
                Vector2 curr = points[i];
                Vector2 next = points[(i + 1) % n];
                Vector2 dirIn = SafeNormalize(curr - prev);
                Vector2 dirOut = SafeNormalize(next - curr);
                if (dirIn == Vector2.Zero || dirOut == Vector2.Zero) { buffer[count++] = curr; continue; }

                JointInfo joint = ComputeJoint(curr, dirIn, dirOut, 0f, innerOffsetForClamp, radius, join, Vector2.Distance(curr, prev), Vector2.Distance(curr, next));
                if (!joint.HasArc) { buffer[count++] = curr; continue; }

                buffer[count++] = joint.TangentIn;
                if (join == LineJoin.Round)
                {
                    for (int k = 1; k < joint.Segments; k++)
                    {
                        float t = joint.FromAngle + (joint.ToAngle - joint.FromAngle) * (k / (float)joint.Segments);
                        buffer[count++] = joint.Ctr + SampleUnitCircle(t * invTwoPi) * joint.ClampedRadius;
                    }
                }
                buffer[count++] = joint.TangentOut;
            }
            return count;
        }

        /// <summary>
        /// Builds a thick outline for <see cref="LineJoin.Round"/>/<see cref="LineJoin.Bevel"/>.
        /// Each joint is precomputed via <see cref="ComputeJoint"/>; segments are then emitted as
        /// quads whose outer corners use the joint's tangent points (<see cref="EmitTrimmedSegmentQuadRaw"/>)
        /// instead of the plain vertex wherever a joint has a fillet, so the straight run and the
        /// arc always meet exactly — no gap, no floating "bead" (see the audit report's join-radius
        /// follow-up for why a naive vertex-planted fan doesn't work once the radius differs from
        /// half-thickness). The inner/concave corner uses <see cref="JointInfo.InnerCorner"/> — a
        /// proper mitered point shared by both adjoining quads — rather than each quad
        /// independently offsetting along its own edge normal: on an acute corner with a large
        /// <paramref name="thickness"/> (exactly <c>Border*</c>'s inward-only case), that naive
        /// per-edge offset could land past the shape's OTHER edge entirely, which is a real
        /// overflow, not the harmless same-color overlap this once assumed (see the audit
        /// report's Border-inset follow-up). Open-polyline endpoints
        /// get <see cref="EmitRoundCap"/> when <paramref name="cap"/> is <see cref="LineCap.Round"/>,
        /// nothing extra otherwise (the segment's own flat cut already gives a butt cap).
        /// </summary>
        /// <param name="jointRadius">
        /// Radius of the join fillet at each vertex. Null (default) matches the outer offset
        /// (half the stroke thickness for a normal symmetric stroke; 0 — i.e. a sharp corner —
        /// for <paramref name="inwardOnly"/>, since there's no "natural" flush radius when the
        /// outer edge doesn't move by default) — flush with the segments, seamless. Any other
        /// value reshapes the corner like a rounded-rectangle corner radius: bigger rounds off
        /// more of the corner (trimming the straight segments back further), smaller keeps more
        /// of the corner sharp before the fillet kicks in. Silently clamped per corner (see
        /// <see cref="ComputeJoint"/>) so an excessive radius on a short or sharp corner degrades
        /// gracefully instead of self-intersecting. Only affects joins, not
        /// <see cref="LineCap.Round"/> end caps (those always use half-thickness).
        /// </param>
        /// <param name="inwardOnly">
        /// When true, <paramref name="points"/> are treated as the TRUE (un-inset) boundary —
        /// the stroke's outer edge sits exactly on them (outer offset 0) and the inner edge sits
        /// a full <paramref name="thickness"/> inward, instead of the normal symmetric
        /// ±<c>thickness/2</c> straddle. Critically, <see cref="ComputeJoint"/> still receives
        /// the real <paramref name="thickness"/> as its innerOffset here, so the fillet radius
        /// is clamped exactly the same way a symmetric stroke's would be —
        /// gracefully shrinking the arc, never inverting it, when <paramref name="jointRadius"/>
        /// is smaller than the border needs (a fillet's own inward reach can't exceed its own
        /// radius: insetting an arc by more than its radius crosses through the arc's center and
        /// produces a self-intersecting mess if not caught, which is exactly what this clamp
        /// prevents). <c>Draw*</c> keeps its accompanying <c>Fill*</c> in agreement with this by
        /// rounding the true vertices with the SAME clamp (passing <paramref name="thickness"/>,
        /// not a sentinel, into <see cref="BuildRoundedCornerBoundary"/>) — see that method's
        /// doc for why the fill needs rounding here at all.
        /// </param>
        private void BuildRoundedOutlineGeometry(ReadOnlySpan<Vector2> points, float thickness, Color color, bool closed,
            LineJoin join, LineCap cap, float? jointRadius, bool inwardOnly = false)
        {
            int n = points.Length;
            if (n < 2 || thickness <= 0f) return;

            float outerOffset = inwardOnly ? 0f : thickness * 0.5f;
            float innerOffset = inwardOnly ? thickness : thickness * 0.5f;
            float radius = jointRadius.HasValue && jointRadius.Value > 0f ? jointRadius.Value : outerOffset;

            Span<JointInfo> joints = n <= MaxStackAllocJoints ? stackalloc JointInfo[n] : new JointInfo[n];
            int jointStart = closed ? 0 : 1;
            int jointEnd = closed ? n : n - 1; // exclusive
            for (int i = jointStart; i < jointEnd; i++)
            {
                Vector2 prev = points[(i - 1 + n) % n];
                Vector2 curr = points[i];
                Vector2 next = points[(i + 1) % n];
                Vector2 dirIn = SafeNormalize(curr - prev);
                Vector2 dirOut = SafeNormalize(next - curr);
                if (dirIn == Vector2.Zero || dirOut == Vector2.Zero) continue; // degenerate segment either side, leave HasArc=false

                joints[i] = ComputeJoint(curr, dirIn, dirOut, outerOffset, innerOffset, radius, join, Vector2.Distance(curr, prev), Vector2.Distance(curr, next));
            }

            bool startCap = !closed && cap == LineCap.Round && n >= 2 && SafeNormalize(points[1] - points[0]) != Vector2.Zero;
            bool endCap = !closed && cap == LineCap.Round && n >= 2 && SafeNormalize(points[n - 1] - points[n - 2]) != Vector2.Zero;
            int capSegments = Math.Max(1, SegmentsForArc(thickness * 0.5f, MathF.PI));

            // Pass 1: sum the exact vertex/index counts so the whole shape can be reserved
            // in one call, instead of one Reserve() per segment/joint/cap (today's approach
            // for this join style — up to ~3N separate calls for an N-vertex shape, versus
            // the cheap shared-vertex Miter path's single call for the whole thing).
            int segmentCount = closed ? n : n - 1;
            int totalVerts = 0, totalIndices = 0;
            for (int i = 0; i < segmentCount; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % n];
                if ((b - a).LengthSquared() < 1e-12f) continue; // duplicate consecutive points, nothing to draw
                totalVerts += 4;
                totalIndices += 6;
            }
            for (int i = jointStart; i < jointEnd; i++)
            {
                if (!joints[i].HasArc) continue;
                JoinFanCounts(joints[i].Segments, out int v, out int idx);
                totalVerts += v; totalIndices += idx;
                totalVerts += 4; totalIndices += 6; // EmitJointGapRaw's kite (see its doc)
            }
            if (startCap) { JoinFanCounts(capSegments, out int v, out int idx); totalVerts += v; totalIndices += idx; }
            if (endCap) { JoinFanCounts(capSegments, out int v, out int idx); totalVerts += v; totalIndices += idx; }

            if (totalVerts == 0) return;
            Reserve(totalVerts, totalIndices);

            // Pass 2: emit. Everything below writes directly (no nested Reserve calls) into
            // the block just reserved above.
            for (int i = 0; i < segmentCount; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % n];
                if ((b - a).LengthSquared() < 1e-12f) continue;

                float dx = b.X - a.X, dy = b.Y - a.Y;
                float invLen = 1f / MathF.Sqrt(dx * dx + dy * dy);
                Vector2 normal = new Vector2(-dy * invLen, dx * invLen);

                bool startIsJoint = i >= jointStart && i < jointEnd;
                int endIndex = (i + 1) % n;
                bool endIsJoint = endIndex >= jointStart && endIndex < jointEnd;

                float sideA = startIsJoint ? joints[i].Side : 1f;
                float sideB = endIsJoint ? joints[endIndex].Side : 1f;

                // joints[i].TangentOut/TangentIn sit on whichever physical side (+normal or
                // -normal) that JOINT's own turn direction put the fillet on (its Side, which
                // flips per-vertex with the turn — convex one way at a "peak", the opposite way
                // at a "valley"), not necessarily the same +normal side this loop's non-joint
                // fallback (sideA/sideB = 1) always assumes. Using Tangent*/InnerCorner without
                // checking that gave a self-intersecting (bowtie) quad whenever a joint's Side
                // was -1: TangentOut/TangentIn landed on the SAME side as the OTHER end's inner
                // corner, crossing the segment's centerline twice — a real gap/crack cutting
                // across the stroke, confirmed by rendering an open zigzag polyline and by the
                // quad's own shoelace area coming out to exactly 0 (the two halves' signed areas
                // cancel). Swap which of Tangent*/InnerCorner plays outer vs inner whenever
                // Side is negative so both ends of the quad agree on which side is "outer".
                bool startFlip = startIsJoint && joints[i].Side < 0f;
                bool endFlip = endIsJoint && joints[endIndex].Side < 0f;

                Vector2 outerA = (startIsJoint && joints[i].HasArc)
                    ? (startFlip ? joints[i].InnerCorner : joints[i].TangentOut)
                    : a + normal * outerOffset * sideA;
                Vector2 innerA = (startIsJoint && joints[i].HasArc)
                    ? (startFlip ? joints[i].TangentOut : joints[i].InnerCorner)
                    : a - normal * innerOffset * sideA;
                Vector2 outerB = (endIsJoint && joints[endIndex].HasArc)
                    ? (endFlip ? joints[endIndex].InnerCorner : joints[endIndex].TangentIn)
                    : b + normal * outerOffset * sideB;
                Vector2 innerB = (endIsJoint && joints[endIndex].HasArc)
                    ? (endFlip ? joints[endIndex].TangentIn : joints[endIndex].InnerCorner)
                    : b - normal * innerOffset * sideB;

                EmitTrimmedSegmentQuadRaw(outerA, outerB, innerB, innerA, color);
            }

            for (int i = jointStart; i < jointEnd; i++)
            {
                if (!joints[i].HasArc) continue;
                EmitJoinFanRaw(joints[i].Ctr, joints[i].ClampedRadius, joints[i].FromAngle, joints[i].ToAngle, joints[i].Segments, color);
                EmitJointGapRaw(joints[i].TangentIn, joints[i].TangentOut, joints[i].InnerCorner, joints[i].Ctr, color);
            }

            if (startCap)
            {
                float capHalf = thickness * 0.5f;
                (float fromAngle, float toAngle, _) = ComputeCapSweep(-SafeNormalize(points[1] - points[0]), capHalf);
                EmitJoinFanRaw(points[0], capHalf, fromAngle, toAngle, capSegments, color);
            }
            if (endCap)
            {
                float capHalf = thickness * 0.5f;
                (float fromAngle, float toAngle, _) = ComputeCapSweep(SafeNormalize(points[n - 1] - points[n - 2]), capHalf);
                EmitJoinFanRaw(points[n - 1], capHalf, fromAngle, toAngle, capSegments, color);
            }
        }

        /// <summary>Exact vertex/index count a fan of <paramref name="segments"/> slices needs — see <see cref="EmitJoinFanRaw"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void JoinFanCounts(int segments, out int vertCount, out int indexCount)
        {
            vertCount = 2 + segments;
            indexCount = segments * 3;
        }

        /// <summary>
        /// Closes the "kite" gap between a joint's fan (which only covers <c>Ctr</c>-to-arc) and
        /// its two adjoining segment quads (whose near edge runs Tangent-to-<see cref="JointInfo.InnerCorner"/>):
        /// since <c>Ctr</c> and <c>InnerCorner</c> are two different points on the same bisector
        /// (not the same point, and not on either quad's own edge), the triangles
        /// (<paramref name="tangentIn"/>, <paramref name="innerCorner"/>, <paramref name="ctr"/>) and
        /// (<paramref name="tangentOut"/>, <paramref name="innerCorner"/>, <paramref name="ctr"/>)
        /// are covered by neither the fan nor the quads on their own — confirmed by rendering the
        /// quads and fan in different colors with nothing else drawn: a real background-colored
        /// wedge was visible at every rounded corner, which is what let the fill underneath show
        /// through even after the fill/border boundary-rounding fix above.
        /// "Raw" — caller must have reserved 4 vertices / 6 indices via one upfront Reserve.
        /// </summary>
        private void EmitJointGapRaw(Vector2 tangentIn, Vector2 tangentOut, Vector2 innerCorner, Vector2 ctr, Color color)
        {
            int b = _vertexCount;
            PushVertex(tangentIn, color);
            PushVertex(innerCorner, color);
            PushVertex(ctr, color);
            PushVertex(tangentOut, color);
            PushTriangleIndices(b + 0, b + 1, b + 2);
            PushTriangleIndices(b + 3, b + 1, b + 2);
        }

        /// <summary>
        /// Emits one segment's quad, with independently-positioned outer corners (which may be
        /// the plain offset vertex, or a joint's tangent point when that end has a fillet — see
        /// <see cref="BuildRoundedOutlineGeometry"/>). "Raw" — assumes the caller already
        /// reserved exactly 4 vertices and 6 indices via one upfront <see cref="Reserve"/> call;
        /// writes directly at the batch's current position instead of calling Reserve itself.
        /// </summary>
        private void EmitTrimmedSegmentQuadRaw(Vector2 outerA, Vector2 outerB, Vector2 innerB, Vector2 innerA, Color color)
        {
            int b = _vertexCount;
            PushVertex(outerA, color);
            PushVertex(outerB, color);
            PushVertex(innerB, color);
            PushVertex(innerA, color);
            PushQuadIndices(b);
        }

        /// <summary>
        /// Emits a half-circle bulging outward from <paramref name="point"/> in
        /// <paramref name="outwardDir"/> (a unit vector), for a <see cref="LineCap.Round"/>
        /// end cap. <paramref name="outwardDir"/> is the direction the bulge should point:
        /// away from the line at the start of an open polyline, or continuing past the end.
        /// Standalone entry point (used directly by <see cref="DrawLine(Vector2,Vector2,float,Color,LineCap)"/>) —
        /// reserves its own space, unlike <see cref="BuildRoundedOutlineGeometry"/>'s caps
        /// which share one upfront Reserve for the whole shape.
        /// </summary>
        private void EmitRoundCap(Vector2 point, Vector2 outwardDir, float half, Color color)
        {
            (float fromAngle, float toAngle, int segments) = ComputeCapSweep(outwardDir, half);
            JoinFanCounts(segments, out int vertCount, out int indexCount);
            Reserve(vertCount, indexCount);
            EmitJoinFanRaw(point, half, fromAngle, toAngle, segments, color);
        }

        /// <summary>
        /// Pure angle computation for <see cref="EmitRoundCap"/>: which of the two candidate
        /// ±π semicircles (starting from the normal of <paramref name="outwardDir"/>) actually
        /// bulges towards <paramref name="outwardDir"/>, picked via a dot-product test against
        /// the sweep's midpoint direction. Split out from emission so <see cref="BuildRoundedOutlineGeometry"/>
        /// can compute cap segment counts without touching the vertex buffer.
        /// </summary>
        private static (float fromAngle, float toAngle, int segments) ComputeCapSweep(Vector2 outwardDir, float half)
        {
            Vector2 normal = new Vector2(-outwardDir.Y, outwardDir.X);
            float fromAngle = MathF.Atan2(normal.Y, normal.X);

            float sweep = MathF.PI;
            float midAngle = fromAngle + sweep * 0.5f;
            Vector2 midDir = SampleUnitCircle(midAngle / MathHelper.TwoPi);
            if (Vector2.Dot(midDir, outwardDir) < 0f) sweep = -MathF.PI;

            int segments = Math.Max(1, SegmentsForArc(half, MathF.PI));
            return (fromAngle, fromAngle + sweep, segments);
        }

        /// <summary>
        /// Emits a triangle fan of <paramref name="segments"/> slices around
        /// <paramref name="center"/>, sweeping from <paramref name="fromAngle"/> to
        /// <paramref name="toAngle"/> (radians) at radius <paramref name="radius"/>.
        /// <paramref name="segments"/> = 1 degenerates to a single flat-cut triangle (Bevel).
        /// Samples angles via <see cref="SampleUnitCircle"/> (turns = angle / 2π) rather than
        /// raw <c>MathF.Cos</c>/<c>MathF.Sin</c>, matching the class's "no trigonometry at draw
        /// time" design tenet used by every other curved shape. "Raw" — assumes the caller
        /// already reserved exactly the counts <see cref="JoinFanCounts"/> reports for
        /// <paramref name="segments"/> via one upfront <see cref="Reserve"/> call; writes
        /// directly at the batch's current position.
        /// </summary>
        private void EmitJoinFanRaw(Vector2 center, float radius, float fromAngle, float toAngle, int segments, Color color)
        {
            float step = (toAngle - fromAngle) / segments;
            float invTwoPi = 1f / MathHelper.TwoPi;

            int b = _vertexCount;
            PushVertex(center, color);
            for (int i = 0; i <= segments; i++)
            {
                Vector2 u = SampleUnitCircle((fromAngle + step * i) * invTwoPi);
                PushVertex(center.X + u.X * radius, center.Y + u.Y * radius, color);
            }
            for (int i = 0; i < segments; i++)
                PushTriangleIndices(b, b + 1 + i, b + 2 + i);
        }
        // ==================================================================
        // SPLINES
        // ==================================================================

        /// <summary>
        /// Draws a linear spline (i.e. a connected polyline) through the given points, using
        /// <see cref="LineJoin.Miter"/> joints — a thin alias for <see cref="DrawLineStrip(ReadOnlySpan{Vector2},float,Color,LineJoin,LineCap,float?)"/>
        /// so every spline method in this section shares one name family. Draws one shared-vertex
        /// strip, not independent per-segment lines: independent lines leave a gap/notch at
        /// every bend once <paramref name="thickness"/> is non-trivial, since neither segment's
        /// flat-cut end covers the wedge the OTHER segment's flat cut leaves open.
        /// </summary>
        public void DrawSplineLinear(ReadOnlySpan<Vector2> points, float thickness, Color color)
            => DrawSplineLinear(points, thickness, color, LineJoin.Miter);

        /// <inheritdoc cref="DrawSplineLinear(ReadOnlySpan{Vector2},float,Color)"/>
        /// <param name="join">How interior vertices are joined. Ignored for a 2-point spline.</param>
        /// <param name="cap">How the two free ends are finished.</param>
        /// <param name="jointRadius">Radius of <see cref="LineJoin.Round"/>/<see cref="LineJoin.Bevel"/> joins; null matches <paramref name="thickness"/>/2. Ignored for <see cref="LineJoin.Miter"/>.</param>
        public void DrawSplineLinear(ReadOnlySpan<Vector2> points, float thickness, Color color,
            LineJoin join, LineCap cap = LineCap.Butt, float? jointRadius = null)
        {
            EnsureStarted();
            if (points.Length < 2) return;
            BuildOutlineGeometry(points, thickness, color, closed: false, join, cap, jointRadius);
        }

        /// <summary>Evaluates a point on a Catmull-Rom spline segment.</summary>
        public static Vector2 GetSplinePointCatmullRom(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float t)
            => Vector2.CatmullRom(p1, p2, p3, p4, t);

        /// <summary>Evaluates a point on a cubic Bezier spline segment.</summary>
        public static Vector2 GetSplinePointBezierCubic(Vector2 p1, Vector2 c2, Vector2 c3, Vector2 p4, float t)
        {
            float u = 1f - t;
            return u * u * u * p1 + 3f * u * u * t * c2 + 3f * u * t * t * c3 + t * t * t * p4;
        }

        /// <summary>Evaluates a point on a quadratic Bezier spline segment.</summary>
        public static Vector2 GetSplinePointBezierQuad(Vector2 p1, Vector2 c2, Vector2 p3, float t)
        {
            float u = 1f - t;
            return u * u * p1 + 2f * u * t * c2 + t * t * p3;
        }

        /// <summary>
        /// Draws a Catmull-Rom spline through the given control points. Requires at
        /// least 4 points; the curve passes through points[1..length-2]. Samples the
        /// curve once into a single shared-vertex strip (proper miter joins, no seams
        /// between sub-segments), instead of drawing each sampled sub-segment as an
        /// independent line.
        /// </summary>
        public void DrawSplineCatmullRom(ReadOnlySpan<Vector2> points, float thickness, Color color, int segmentsPerPiece = 16)
        {
            EnsureStarted();
            if (points.Length < 4 || segmentsPerPiece < 1) return;

            int pieceCount = points.Length - 3;
            int sampleCount = pieceCount * segmentsPerPiece + 1;

            Span<Vector2> samples = sampleCount <= MaxStackAllocElements ? stackalloc Vector2[sampleCount] : new Vector2[sampleCount];
            samples[0] = points[1];

            int idx = 1;
            for (int i = 1; i < points.Length - 2; i++)
            {
                for (int s = 1; s <= segmentsPerPiece; s++)
                {
                    float t = s / (float)segmentsPerPiece;
                    samples[idx++] = Vector2.CatmullRom(points[i - 1], points[i], points[i + 1], points[i + 2], t);
                }
            }

            BuildOutlineGeometry(samples, thickness, color, closed: false);
        }

        /// <summary>
        /// Draws a cubic Bezier spline: [p1, c2, c3, p4, c5, c6, p7...].
        /// Requires 3n+1 points for n cubic segments. Samples the curve once into a
        /// single shared-vertex strip (proper miter joins, no seams between
        /// sub-segments), instead of drawing each sampled sub-segment as an
        /// independent line.
        /// </summary>
        public void DrawSplineBezierCubic(ReadOnlySpan<Vector2> points, float thickness, Color color, int segmentsPerPiece = 16)
        {
            EnsureStarted();
            if (points.Length < 4 || (points.Length - 1) % 3 != 0 || segmentsPerPiece < 1) return;

            int segmentCount = (points.Length - 1) / 3;
            int sampleCount = segmentCount * segmentsPerPiece + 1;

            Span<Vector2> samples = sampleCount <= MaxStackAllocElements ? stackalloc Vector2[sampleCount] : new Vector2[sampleCount];
            samples[0] = points[0];

            int idx = 1;
            for (int i = 0; i + 3 < points.Length; i += 3)
            {
                for (int s = 1; s <= segmentsPerPiece; s++)
                {
                    float t = s / (float)segmentsPerPiece;
                    samples[idx++] = GetSplinePointBezierCubic(points[i], points[i + 1], points[i + 2], points[i + 3], t);
                }
            }

            BuildOutlineGeometry(samples, thickness, color, closed: false);
        }

        // ==================================================================
        // GRID / AXES
        // ==================================================================
        // 2D counterpart to Primitive3DBatchShapes' DrawGridXY/XZ/YZ + DrawAxis: a
        // reference grid and an X/Y axis cross, kept as separate methods so callers
        // can combine/omit/style them independently. There's only one plane in 2D,
        // so (unlike the 3D per-axis-pair split) a single DrawGrid covers it.
        //
        // Every 5th division is a "major" line: a darker color and one pixel wider,
        // matching common CAD/DCC grid conventions so large grids stay readable.

        private const int MajorGridLineInterval = 5;

        // Low alpha by default so a reference grid reads as a subtle backdrop
        // rather than competing with whatever's drawn on top of it.
        private static readonly Color DefaultGridLineColor = new(0.75f, 0.75f, 0.75f, 0.35f);
        private static readonly Color DefaultGridMajorLineColor = new(0.45f, 0.45f, 0.45f, 0.4f);
        private static readonly Color DefaultAxisColor = new(0.15f, 0.15f, 0.15f, 0.65f);

        /// <summary>Draws a grid centered at the origin, using the default subtle grid colors.</summary>
        public void DrawGrid(int slices, float spacing)
            => DrawGrid(slices, spacing, Vector2.Zero, DefaultGridLineColor, DefaultGridMajorLineColor);

        /// <summary>Draws a grid with an explicit origin and colors (every 5th line drawn as a darker, wider "major" line).</summary>
        public void DrawGrid(int slices, float spacing, Vector2 origin, Color lineColor, Color majorLineColor)
        {
            EnsureStarted();
            int halfSlices = slices / 2;
            float extent = halfSlices * spacing;

            for (int i = -halfSlices; i <= halfSlices; i++)
            {
                bool major = i % MajorGridLineInterval == 0;
                float offset = i * spacing;
                float thickness = major ? 2f : 1f;
                Color color = major ? majorLineColor : lineColor;

                DrawLine(origin + new Vector2(offset, -extent), origin + new Vector2(offset, extent), thickness, color);
                DrawLine(origin + new Vector2(-extent, offset), origin + new Vector2(extent, offset), thickness, color);
            }
        }

        /// <summary>Draws an X/Y axis cross of length <paramref name="size"/> (in each direction) through the origin, using a dark, grid-like default color.</summary>
        public void DrawAxis(float size) => DrawAxis(Vector2.Zero, size, DefaultAxisColor);

        /// <summary>Draws an X/Y axis cross of length <paramref name="size"/> (in each direction) through the origin.</summary>
        public void DrawAxis(float size, Color color) => DrawAxis(Vector2.Zero, size, color);

        /// <summary>Draws an X/Y axis cross of length <paramref name="size"/> (in each direction) through <paramref name="origin"/>.</summary>
        public void DrawAxis(Vector2 origin, float size, Color color)
        {
            EnsureStarted();
            DrawLine(origin - Vector2.UnitX * size, origin + Vector2.UnitX * size, color);
            DrawLine(origin - Vector2.UnitY * size, origin + Vector2.UnitY * size, color);
        }

        // ------------------------------------------------------------------
        // Disposal
        // ------------------------------------------------------------------

        public void Dispose() => _effect?.Dispose();
    }
}