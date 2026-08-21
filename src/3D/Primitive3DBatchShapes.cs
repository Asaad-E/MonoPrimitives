using System;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace MonoPrimitives.Primitives3D
{
    public sealed partial class Primitive3DBatch
    {
        public const float DefaultPointSize = 0.1f;

        // =====================================================================
        // LINES / POINTS (stroke primitives — no Fill/Border split, same
        // convention as the 2D library's Line/Spline family)
        // =====================================================================

        /// <summary>Draws a line in 3D world space using <see cref="DefaultLineThickness"/>.</summary>
        public void DrawLine3D(Vector3 startPos, Vector3 endPos, Color color)
            => DrawLine3D(startPos, endPos, DefaultLineThickness, color);

        /// <summary>Draws a line in 3D world space as a camera-facing quad (two triangles).</summary>
        /// <param name="thickness">Thickness in pixels when <see cref="SmoothLines"/> is enabled, otherwise in world units.</param>
        public void DrawLine3D(Vector3 startPos, Vector3 endPos, float thickness, Color color)
        {
            ThrowIfNotBegun();

            Vector3 dir = endPos - startPos;
            float lenSq = dir.LengthSquared();
            if (lenSq < 1e-12f)
                return;

            // Orient the quad so its normal faces the camera; using the segment
            // midpoint keeps the whole line at a consistent thickness.
            Vector3 mid = (startPos + endPos) * 0.5f;
            Vector3 toCamera = _cameraPosition - mid;

            Vector3 side = Vector3.Cross(dir, toCamera);
            float sideLenSq = side.LengthSquared();

            if (sideLenSq < 1e-12f)
                BuildBasis(dir / MathF.Sqrt(lenSq), out side, out _); // line points straight at the camera
            else
                side *= 1f / MathF.Sqrt(sideLenSq);

            float halfWidth = SmoothLines
                ? thickness * 0.5f * MathF.Max(toCamera.Length(), 1e-4f) * _pixelScale
                : thickness * 0.5f;

            Vector3 offset = side * halfWidth;
            PushQuad(startPos - offset, endPos - offset, endPos + offset, startPos + offset, color);
        }

        /// <summary>Draws a line as a raw GPU line-list segment (cheaper than <see cref="DrawLine3D"/>, always 1px, no camera facing).</summary>
        public void DrawLine3DFast(Vector3 startPos, Vector3 endPos, Color color)
        {
            ReserveLine(2);
            PushLine(startPos, endPos, color);
        }

        /// <summary>Draws a polyline through the supplied points.</summary>
        public void DrawLineStrip3D(ReadOnlySpan<Vector3> points, Color color)
        {
            ThrowIfNotBegun();
            for (int i = 1; i < points.Length; i++)
                DrawLine3D(points[i - 1], points[i], DefaultLineThickness, color);
        }

        /// <summary>Draws a point in 3D space as a short line.</summary>
        public void DrawPoint3D(Vector3 position, Color color)
            => DrawPoint3D(position, DefaultLineThickness, color);

        /// <summary>Draws a point in 3D space as a short line of length <paramref name="size"/> along local X.</summary>
        public void DrawPoint3D(Vector3 position, float size, Color color)
            => DrawLine3DFast(position, new Vector3(position.X + size, position.Y, position.Z), color);

        /// <summary>Draws a point as a 3-axis cross, for readable debug visualization.</summary>
        public void DrawPoint3DCross(Vector3 position, Color color)
            => DrawPoint3DCross(position, DefaultLineThickness, color);

        /// <summary>Draws a point as a 3-axis cross of total length <paramref name="size"/>.</summary>
        public void DrawPoint3DCross(Vector3 position, float size, Color color)
        {
            float h = size * 0.5f;
            DrawLine3DFast(new Vector3(position.X - h, position.Y, position.Z), new Vector3(position.X + h, position.Y, position.Z), color);
            DrawLine3DFast(new Vector3(position.X, position.Y - h, position.Z), new Vector3(position.X, position.Y + h, position.Z), color);
            DrawLine3DFast(new Vector3(position.X, position.Y, position.Z - h), new Vector3(position.X, position.Y, position.Z + h), color);
        }

        /// <summary>Draws a ray as a 1000-unit-long line.</summary>
        public void DrawRay(Ray ray, Color color) => DrawRay(ray, 1000f, color);

        /// <summary>Draws a ray with an explicit length.</summary>
        public void DrawRay(Ray ray, float length, Color color)
        {
            Vector3 dir = ray.Direction;
            if (dir.LengthSquared() > 1e-12f)
                dir.Normalize();
            DrawLine3D(ray.Position, ray.Position + dir * length, color);
        }

        // =====================================================================
        // ARROWS (a shaft plus a cone head — velocity/force gizmos, vector fields)
        // =====================================================================

        /// <summary>Draws an arrow from <paramref name="start"/> to <paramref name="end"/> — a line shaft plus a cone head, sized automatically from the arrow's own length and <see cref="DefaultLineThickness"/>.</summary>
        public void DrawArrow(Vector3 start, Vector3 end, Color color)
            => DrawArrow(start, end, DefaultLineThickness, color);

        /// <summary>
        /// Draws an arrow with an explicit shaft thickness. The cone head's length and radius
        /// scale with both the arrow's own length and <paramref name="thickness"/>, capped so a
        /// short arrow doesn't grow a head bigger than the arrow itself; pass
        /// <paramref name="headLength"/>/<paramref name="headRadius"/> to size it exactly instead.
        /// </summary>
        public void DrawArrow(Vector3 start, Vector3 end, float thickness, Color color, float? headLength = null, float? headRadius = null, int sides = 12)
        {
            ThrowIfNotBegun();
            Vector3 delta = end - start;
            float len = delta.Length();
            if (len < 1e-6f)
                return;
            Vector3 dir = delta / len;

            // Min, not Max: the head should scale with the shaft's own thickness, only shrinking
            // further (never growing) when the arrow is too short for that to fit — same fix as
            // the 2D DrawArrow, found by actually rendering a long thin arrow and seeing an
            // oversized head (see the 2D changelog entry for the visual before/after).
            float hl = MathF.Min(headLength ?? MathF.Min(thickness * 6f, len * 0.25f), len);
            float hr = MathF.Min(headRadius ?? MathF.Max(thickness * 2.5f, hl * 0.4f), len * 0.5f);

            Vector3 headBase = end - dir * hl;
            if (hl < len)
                DrawLine3D(start, headBase, thickness, color);
            FillCylinder(headBase, end, hr, 0f, sides, color);
        }

        // =====================================================================
        // CIRCLES (flat disc in an arbitrary 3D plane)
        // =====================================================================

        /// <summary>Draws a filled disc, oriented like <see cref="BorderCircle3D(Vector3,float,Vector3,float,Color,float)"/>.</summary>
        public void FillCircle3D(Vector3 center, float radius, Vector3 rotationAxis, float rotationAngle, Color color)
            => FillCircle3D(center, radius, rotationAxis, rotationAngle, DefaultCircleSegments, color);

        /// <summary>Draws a filled disc with an explicit segment count (-1 for automatic LOD).</summary>
        public void FillCircle3D(Vector3 center, float radius, Vector3 rotationAxis, float rotationAngle, int segments, Color color)
        {
            ThrowIfNotBegun();
            int seg = ResolveSegments(segments, radius, center, DefaultCircleSegments);
            GetCircleBasis(rotationAxis, rotationAngle, radius, out Vector3 ax, out Vector3 ay);

            TrigLut.SinCosStep(0, seg, out float s0, out float c0);
            Vector3 prev = center + ax * c0 + ay * s0;

            for (int i = 1; i <= seg; i++)
            {
                TrigLut.SinCosStep(i, seg, out float s, out float c);
                Vector3 cur = center + ax * c + ay * s;
                PushTriangleLit(center, prev, cur, color);
                prev = cur;
            }
        }

        /// <summary>Draws a wireframe circle, rotated by <paramref name="rotationAngle"/> (degrees) around <paramref name="rotationAxis"/>.</summary>
        /// <param name="thickness">Line thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void BorderCircle3D(Vector3 center, float radius, Vector3 rotationAxis, float rotationAngle, Color color, float thickness = -1f)
            => BorderCircle3D(center, radius, rotationAxis, rotationAngle, DefaultCircleSegments, color, thickness);

        /// <summary>Draws a wireframe circle with an explicit segment count (-1 for automatic LOD).</summary>
        /// <param name="thickness">Line thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void BorderCircle3D(Vector3 center, float radius, Vector3 rotationAxis, float rotationAngle, int segments, Color color, float thickness = -1f)
        {
            ThrowIfNotBegun();
            float w = thickness > 0f ? thickness : DefaultLineThickness;
            int seg = ResolveSegments(segments, radius, center, DefaultCircleSegments);
            GetCircleBasis(rotationAxis, rotationAngle, radius, out Vector3 ax, out Vector3 ay);

            TrigLut.SinCosStep(0, seg, out float s0, out float c0);
            Vector3 prev = center + ax * c0 + ay * s0;

            for (int i = 1; i <= seg; i++)
            {
                TrigLut.SinCosStep(i, seg, out float s, out float c);
                Vector3 cur = center + ax * c + ay * s;
                DrawLine3D(prev, cur, w, color);
                prev = cur;
            }
        }

        /// <summary>Draws a disc with independently colored fill and wireframe border.</summary>
        /// <param name="thickness">Border thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void DrawCircle3D(Vector3 center, float radius, Vector3 rotationAxis, float rotationAngle, Color fillColor, Color borderColor, float thickness = -1f)
        {
            FillCircle3D(center, radius, rotationAxis, rotationAngle, fillColor);
            BorderCircle3D(center, radius, rotationAxis, rotationAngle, borderColor, thickness);
        }

        /// <summary>Draws a disc with independently colored fill and wireframe border, with an explicit segment count.</summary>
        /// <param name="thickness">Border thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void DrawCircle3D(Vector3 center, float radius, Vector3 rotationAxis, float rotationAngle, int segments, Color fillColor, Color borderColor, float thickness = -1f)
        {
            FillCircle3D(center, radius, rotationAxis, rotationAngle, segments, fillColor);
            BorderCircle3D(center, radius, rotationAxis, rotationAngle, segments, borderColor, thickness);
        }

        /// <summary>Shared radial basis for both circle methods — an XY-plane circle rotated by <paramref name="rotationAngle"/> degrees around <paramref name="rotationAxis"/>.</summary>
        private static void GetCircleBasis(in Vector3 rotationAxis, float rotationAngle, float radius, out Vector3 ax, out Vector3 ay)
        {
            Matrix rot = Matrix.CreateFromAxisAngle(SafeNormalize(rotationAxis, Vector3.UnitZ), MathHelper.ToRadians(rotationAngle));
            ax = new Vector3(rot.M11, rot.M12, rot.M13) * radius;
            ay = new Vector3(rot.M21, rot.M22, rot.M23) * radius;
        }

        // =====================================================================
        // TRIANGLES
        // =====================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 RotateAroundPivot(in Vector3 p, in Vector3 pivot, in Quaternion rotation)
            => pivot + Vector3.Transform(p - pivot, rotation);

        /// <summary>Draws a filled triangle (vertices in counter-clockwise order when viewed from the front).</summary>
        public void FillTriangle3D(Vector3 v1, Vector3 v2, Vector3 v3, Color color, Quaternion rotation = default, Vector3? origin = null)
        {
            ThrowIfNotBegun();
            if (rotation != default && rotation != Quaternion.Identity)
            {
                Vector3 pivot = origin ?? (v1 + v2 + v3) / 3f;
                v1 = RotateAroundPivot(v1, pivot, rotation);
                v2 = RotateAroundPivot(v2, pivot, rotation);
                v3 = RotateAroundPivot(v3, pivot, rotation);
            }
            PushTriangleLit(v1, v2, v3, color);
        }

        /// <summary>Draws a triangle's three edges only.</summary>
        /// <param name="thickness">Line thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void BorderTriangle3D(Vector3 v1, Vector3 v2, Vector3 v3, Color color, Quaternion rotation = default, Vector3? origin = null, float thickness = -1f)
        {
            if (rotation != default && rotation != Quaternion.Identity)
            {
                Vector3 pivot = origin ?? (v1 + v2 + v3) / 3f;
                v1 = RotateAroundPivot(v1, pivot, rotation);
                v2 = RotateAroundPivot(v2, pivot, rotation);
                v3 = RotateAroundPivot(v3, pivot, rotation);
            }
            float w = thickness > 0f ? thickness : DefaultLineThickness;
            DrawLine3D(v1, v2, w, color);
            DrawLine3D(v2, v3, w, color);
            DrawLine3D(v3, v1, w, color);
        }

        /// <summary>Draws a triangle with independently colored fill and edge outline.</summary>
        /// <param name="thickness">Border thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void DrawTriangle3D(Vector3 v1, Vector3 v2, Vector3 v3, Color fillColor, Color borderColor, Quaternion rotation = default, Vector3? origin = null, float thickness = -1f)
        {
            FillTriangle3D(v1, v2, v3, fillColor, rotation, origin);
            BorderTriangle3D(v1, v2, v3, borderColor, rotation, origin, thickness);
        }

        /// <summary>Draws a triangle strip as a raw mesh (no Fill/Border split — like <see cref="FillPolygon"/>'s 2D sibling, this submits geometry as given).</summary>
        public void DrawTriangleStrip3D(Vector3[] points, Color color) => DrawTriangleStrip3D(points.AsSpan(), color);

        /// <summary>Draws a triangle strip from a span of points, alternating winding to keep a consistent facing direction.</summary>
        public void DrawTriangleStrip3D(ReadOnlySpan<Vector3> points, Color color)
        {
            ThrowIfNotBegun();
            if (points.Length < 3)
                return;

            for (int i = 2; i < points.Length; i++)
            {
                if ((i & 1) == 0)
                    PushTriangle(points[i], points[i - 1], points[i - 2], color);
                else
                    PushTriangle(points[i - 2], points[i - 1], points[i], color);
            }
        }

        // =====================================================================
        // CUBES
        // =====================================================================

        /// <summary>Draws a filled cube centered at <paramref name="position"/>, optionally rotated about its own center.</summary>
        public void FillCube(Vector3 position, float width, float height, float length, Color color, Quaternion rotation = default)
            => FillCube(position, new Vector3(width, height, length), color, rotation);

        /// <summary>Draws a filled cube (vector size version).</summary>
        public void FillCube(Vector3 position, Vector3 size, Color color, Quaternion rotation = default)
        {
            ThrowIfNotBegun();
            GetCubeCorners(position, size, rotation, out Vector3 p000, out Vector3 p100, out Vector3 p110, out Vector3 p010,
                           out Vector3 p001, out Vector3 p101, out Vector3 p111, out Vector3 p011);

            Reserve(36);
            PushQuadLit(p001, p101, p111, p011, color); // +Z front
            PushQuadLit(p100, p000, p010, p110, color); // -Z back
            PushQuadLit(p011, p111, p110, p010, color); // +Y top
            PushQuadLit(p000, p100, p101, p001, color); // -Y bottom
            PushQuadLit(p101, p100, p110, p111, color); // +X right
            PushQuadLit(p000, p001, p011, p010, color); // -X left
        }

        /// <summary>Draws the wireframe of a cube.</summary>
        /// <param name="thickness">Line thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void BorderCube(Vector3 position, float width, float height, float length, Color color, Quaternion rotation = default, float thickness = -1f)
            => BorderCube(position, new Vector3(width, height, length), color, rotation, thickness);

        /// <summary>Draws the wireframe of a cube (vector size version).</summary>
        /// <param name="thickness">Line thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void BorderCube(Vector3 position, Vector3 size, Color color, Quaternion rotation = default, float thickness = -1f)
        {
            ThrowIfNotBegun();
            float w = thickness > 0f ? thickness : DefaultLineThickness;
            GetCubeCorners(position, size, rotation, out Vector3 p000, out Vector3 p100, out Vector3 p110, out Vector3 p010,
                           out Vector3 p001, out Vector3 p101, out Vector3 p111, out Vector3 p011);

            DrawLine3D(p001, p101, w, color); DrawLine3D(p101, p111, w, color);
            DrawLine3D(p111, p011, w, color); DrawLine3D(p011, p001, w, color);
            DrawLine3D(p000, p100, w, color); DrawLine3D(p100, p110, w, color);
            DrawLine3D(p110, p010, w, color); DrawLine3D(p010, p000, w, color);
            DrawLine3D(p000, p001, w, color); DrawLine3D(p100, p101, w, color);
            DrawLine3D(p110, p111, w, color); DrawLine3D(p010, p011, w, color);
        }

        /// <summary>Draws a cube with independently colored fill and wireframe border.</summary>
        /// <param name="thickness">Border thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void DrawCube(Vector3 position, float width, float height, float length, Color fillColor, Color borderColor, Quaternion rotation = default, float thickness = -1f)
            => DrawCube(position, new Vector3(width, height, length), fillColor, borderColor, rotation, thickness);

        /// <summary>Draws a cube (vector size version) with independently colored fill and wireframe border.</summary>
        /// <param name="thickness">Border thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void DrawCube(Vector3 position, Vector3 size, Color fillColor, Color borderColor, Quaternion rotation = default, float thickness = -1f)
        {
            FillCube(position, size, fillColor, rotation);
            BorderCube(position, size, borderColor, rotation, thickness);
        }

        private static void GetCubeCorners(in Vector3 position, in Vector3 size, in Quaternion rotation,
            out Vector3 p000, out Vector3 p100, out Vector3 p110, out Vector3 p010,
            out Vector3 p001, out Vector3 p101, out Vector3 p111, out Vector3 p011)
        {
            float x = size.X * 0.5f, y = size.Y * 0.5f, z = size.Z * 0.5f;
            bool hasRotation = rotation != default && rotation != Quaternion.Identity;
            Vector3 pos = position;
            Quaternion rot = rotation;

            Vector3 L(float sx, float sy, float sz)
            {
                Vector3 local = new(sx * x, sy * y, sz * z);
                return pos + (hasRotation ? Vector3.Transform(local, rot) : local);
            }

            p000 = L(-1, -1, -1); p100 = L(1, -1, -1); p110 = L(1, 1, -1); p010 = L(-1, 1, -1);
            p001 = L(-1, -1, 1); p101 = L(1, -1, 1); p111 = L(1, 1, 1); p011 = L(-1, 1, 1);
        }

        /// <summary>Draws a filled bounding box (axis-aligned, no rotation — that's what a <see cref="BoundingBox"/> is).</summary>
        public void FillBoundingBox(BoundingBox box, Color color)
        {
            Vector3 size = box.Max - box.Min;
            FillCube(box.Min + size * 0.5f, size, color);
        }

        /// <summary>Draws the wireframe of a bounding box.</summary>
        /// <param name="thickness">Line thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void BorderBoundingBox(BoundingBox box, Color color, float thickness = -1f)
        {
            Vector3 size = box.Max - box.Min;
            BorderCube(box.Min + size * 0.5f, size, color, default, thickness);
        }

        /// <summary>Draws a bounding box with independently colored fill and wireframe border.</summary>
        /// <param name="thickness">Border thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void DrawBoundingBox(BoundingBox box, Color fillColor, Color borderColor, float thickness = -1f)
        {
            FillBoundingBox(box, fillColor);
            BorderBoundingBox(box, borderColor, thickness);
        }

        // =====================================================================
        // SPHERES
        // =====================================================================
        // No rotation parameter: a solid-color UV sphere is rotationally
        // symmetric, so rotating it has no visible effect — same reasoning the
        // 2D library documents for FillCircleGradient's own lack of rotation.

        /// <summary>Draws a filled sphere using default tessellation.</summary>
        public void FillSphere(Vector3 centerPos, float radius, Color color)
            => FillSphere(centerPos, radius, DefaultSphereRings, DefaultSphereSlices, color);

        /// <summary>Draws a filled sphere with an explicit ring/slice count (-1 for automatic LOD).</summary>
        public void FillSphere(Vector3 centerPos, float radius, int rings, int slices, Color color)
        {
            ThrowIfNotBegun();
            int r = ResolveSegments(rings, radius, centerPos, DefaultSphereRings);
            int s = ResolveSegments(slices, radius, centerPos, DefaultSphereSlices);
            if (r < 2) r = 2;
            if (s < 3) s = 3;

            for (int i = 0; i < r; i++)
            {
                SinCosLatitude(i, r, out float sinLat0, out float cosLat0);
                SinCosLatitude(i + 1, r, out float sinLat1, out float cosLat1);

                float y0 = cosLat0 * radius, ry0 = sinLat0 * radius;
                float y1 = cosLat1 * radius, ry1 = sinLat1 * radius;

                TrigLut.SinCosStep(0, s, out float sinLon0, out float cosLon0);

                for (int j = 1; j <= s; j++)
                {
                    TrigLut.SinCosStep(j, s, out float sinLon1, out float cosLon1);

                    Vector3 a = new(centerPos.X + ry0 * cosLon0, centerPos.Y + y0, centerPos.Z + ry0 * sinLon0);
                    Vector3 b = new(centerPos.X + ry1 * cosLon0, centerPos.Y + y1, centerPos.Z + ry1 * sinLon0);
                    Vector3 c = new(centerPos.X + ry1 * cosLon1, centerPos.Y + y1, centerPos.Z + ry1 * sinLon1);
                    Vector3 d = new(centerPos.X + ry0 * cosLon1, centerPos.Y + y0, centerPos.Z + ry0 * sinLon1);

                    // b/d swapped from the (a,b,c,d) construction order above: this walks
                    // latitude-then-longitude, the opposite traversal from the cylinder/capsule
                    // quads elsewhere in this file (which walk angle-then-height) — passed in
                    // construction order the face normal points inward instead of outward.
                    //
                    // Normal is the exact outward sphere normal (point - center), not a
                    // cross-product of the quad's own edges: at the pole rings two of the four
                    // corners coincide (ry0 or ry1 is zero), which makes the edge-cross-product
                    // normal a zero vector and NaN after Normalize — that NaN silently blackens
                    // the pole quad once Shade() casts it to a byte. The exact normal has no
                    // such degenerate case and is cheaper than a robust cross-product fallback.
                    Vector3 quadNormal = Vector3.Normalize(a + b + c + d - 4f * centerPos);
                    PushQuadLit(a, d, c, b, quadNormal, color); // degenerate triangles at the poles are cheaper to emit than to branch on

                    sinLon0 = sinLon1;
                    cosLon0 = cosLon1;
                }
            }
        }

        /// <summary>Draws a filled sphere from a <see cref="BoundingSphere"/>.</summary>
        public void FillSphere(BoundingSphere sphere, Color color) => FillSphere(sphere.Center, sphere.Radius, color);

        /// <summary>Draws a sphere wireframe using default tessellation.</summary>
        /// <param name="thickness">Line thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void BorderSphere(Vector3 centerPos, float radius, Color color, float thickness = -1f)
            => BorderSphere(centerPos, radius, DefaultSphereRings, DefaultSphereSlices, color, thickness);

        /// <summary>Draws a sphere wireframe (latitude and longitude lines) with an explicit ring/slice count.</summary>
        /// <param name="thickness">Line thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void BorderSphere(Vector3 centerPos, float radius, int rings, int slices, Color color, float thickness = -1f)
        {
            ThrowIfNotBegun();
            float w = thickness > 0f ? thickness : DefaultLineThickness;
            int r = ResolveSegments(rings, radius, centerPos, DefaultSphereRings);
            int s = ResolveSegments(slices, radius, centerPos, DefaultSphereSlices);
            if (r < 2) r = 2;
            if (s < 3) s = 3;

            for (int i = 1; i < r; i++) // latitude rings; poles degenerate to a point, skip them
            {
                SinCosLatitude(i, r, out float sinLat, out float cosLat);
                float y = centerPos.Y + cosLat * radius;
                float rr = sinLat * radius;

                TrigLut.SinCosStep(0, s, out float s0, out float c0);
                Vector3 prev = new(centerPos.X + rr * c0, y, centerPos.Z + rr * s0);

                for (int j = 1; j <= s; j++)
                {
                    TrigLut.SinCosStep(j, s, out float sn, out float cs);
                    Vector3 cur = new(centerPos.X + rr * cs, y, centerPos.Z + rr * sn);
                    DrawLine3D(prev, cur, w, color);
                    prev = cur;
                }
            }

            for (int j = 0; j < s; j++) // longitude meridians
            {
                TrigLut.SinCosStep(j, s, out float sinLon, out float cosLon);
                Vector3 prev = new(centerPos.X, centerPos.Y + radius, centerPos.Z);
                for (int i = 1; i <= r; i++)
                {
                    SinCosLatitude(i, r, out float sinLat, out float cosLat);
                    Vector3 cur = new(centerPos.X + sinLat * radius * cosLon, centerPos.Y + cosLat * radius, centerPos.Z + sinLat * radius * sinLon);
                    DrawLine3D(prev, cur, w, color);
                    prev = cur;
                }
            }
        }

        /// <summary>Draws a sphere with independently colored fill and wireframe border.</summary>
        /// <param name="thickness">Border thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void DrawSphere(Vector3 centerPos, float radius, Color fillColor, Color borderColor, float thickness = -1f)
        {
            FillSphere(centerPos, radius, fillColor);
            BorderSphere(centerPos, radius, borderColor, thickness);
        }

        /// <summary>Draws a sphere with independently colored fill and wireframe border, with an explicit ring/slice count.</summary>
        /// <param name="thickness">Border thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void DrawSphere(Vector3 centerPos, float radius, int rings, int slices, Color fillColor, Color borderColor, float thickness = -1f)
        {
            FillSphere(centerPos, radius, rings, slices, fillColor);
            BorderSphere(centerPos, radius, rings, slices, borderColor, thickness);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SinCosLatitude(int ring, int rings, out float sin, out float cos)
            => TrigLut.SinCosStep(ring, rings * 2, out sin, out cos); // latitude covers half a turn -> half the LUT

        // =====================================================================
        // CYLINDERS / CONES
        // =====================================================================
        // The two-endpoint overloads are already fully orientable via their
        // start/end points; only the "standing on position, extends +Y" simple
        // overloads get a rotation parameter, since those otherwise have no way
        // to tilt.

        /// <summary>Draws a filled cylinder or cone standing on <paramref name="position"/>, extending along +Y unless <paramref name="rotation"/> tilts it.</summary>
        public void FillCylinder(Vector3 position, float radiusTop, float radiusBottom, float height, int slices, Color color, Quaternion rotation = default)
        {
            Vector3 axis = RotateAxis(Vector3.Up, rotation) * height;
            FillCylinder(position, position + axis, radiusBottom, radiusTop, slices, color);
        }

        /// <summary>Draws a filled cylinder between two arbitrary points.</summary>
        /// <param name="sides">Side count, or -1 for automatic level of detail.</param>
        public void FillCylinder(Vector3 startPos, Vector3 endPos, float startRadius, float endRadius, int sides, Color color)
        {
            ThrowIfNotBegun();
            Vector3 axis = endPos - startPos;
            if (axis.LengthSquared() < 1e-12f)
                return;

            Vector3 n = Vector3.Normalize(axis);
            BuildBasis(n, out Vector3 t, out Vector3 b);

            float maxRadius = MathF.Max(MathF.Abs(startRadius), MathF.Abs(endRadius));
            int seg = ResolveSegments(sides, maxRadius, (startPos + endPos) * 0.5f, DefaultCircleSegments);
            if (seg < 3) seg = 3;

            TrigLut.SinCosStep(0, seg, out float s0, out float c0);
            Vector3 dir0 = t * c0 + b * s0;
            Vector3 bottom0 = startPos + dir0 * startRadius;
            Vector3 top0 = endPos + dir0 * endRadius;

            for (int i = 1; i <= seg; i++)
            {
                TrigLut.SinCosStep(i, seg, out float s1, out float c1);
                Vector3 dir1 = t * c1 + b * s1;
                Vector3 bottom1 = startPos + dir1 * startRadius;
                Vector3 top1 = endPos + dir1 * endRadius;

                Reserve(12);
                PushQuadLit(bottom0, bottom1, top1, top0, color); // side (degenerates to a triangle when a radius is zero)
                if (startRadius > 0f) PushTriangleLit(startPos, bottom1, bottom0, color);
                if (endRadius > 0f) PushTriangleLit(endPos, top0, top1, color);

                bottom0 = bottom1;
                top0 = top1;
            }
        }

        /// <summary>Draws a cylinder/cone wireframe standing on <paramref name="position"/>, extending along +Y unless <paramref name="rotation"/> tilts it.</summary>
        /// <param name="thickness">Line thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void BorderCylinder(Vector3 position, float radiusTop, float radiusBottom, float height, int slices, Color color, Quaternion rotation = default, float thickness = -1f)
        {
            Vector3 axis = RotateAxis(Vector3.Up, rotation) * height;
            BorderCylinder(position, position + axis, radiusBottom, radiusTop, slices, color, thickness);
        }

        /// <summary>Draws a cylinder wireframe between two arbitrary points.</summary>
        /// <param name="thickness">Line thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void BorderCylinder(Vector3 startPos, Vector3 endPos, float startRadius, float endRadius, int sides, Color color, float thickness = -1f)
        {
            ThrowIfNotBegun();
            Vector3 axis = endPos - startPos;
            if (axis.LengthSquared() < 1e-12f)
                return;

            float w = thickness > 0f ? thickness : DefaultLineThickness;
            Vector3 n = Vector3.Normalize(axis);
            BuildBasis(n, out Vector3 t, out Vector3 b);

            float maxRadius = MathF.Max(MathF.Abs(startRadius), MathF.Abs(endRadius));
            int seg = ResolveSegments(sides, maxRadius, (startPos + endPos) * 0.5f, DefaultCircleSegments);
            if (seg < 3) seg = 3;

            TrigLut.SinCosStep(0, seg, out float s0, out float c0);
            Vector3 dir0 = t * c0 + b * s0;
            Vector3 bottom0 = startPos + dir0 * startRadius;
            Vector3 top0 = endPos + dir0 * endRadius;

            for (int i = 1; i <= seg; i++)
            {
                TrigLut.SinCosStep(i, seg, out float s1, out float c1);
                Vector3 dir1 = t * c1 + b * s1;
                Vector3 bottom1 = startPos + dir1 * startRadius;
                Vector3 top1 = endPos + dir1 * endRadius;

                DrawLine3D(bottom0, bottom1, w, color);
                DrawLine3D(top0, top1, w, color);
                DrawLine3D(bottom0, top0, w, color);

                bottom0 = bottom1;
                top0 = top1;
            }
        }

        /// <summary>Draws a cylinder/cone with independently colored fill and wireframe border, standing on <paramref name="position"/>.</summary>
        /// <param name="thickness">Border thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void DrawCylinder(Vector3 position, float radiusTop, float radiusBottom, float height, int slices, Color fillColor, Color borderColor, Quaternion rotation = default, float thickness = -1f)
        {
            FillCylinder(position, radiusTop, radiusBottom, height, slices, fillColor, rotation);
            BorderCylinder(position, radiusTop, radiusBottom, height, slices, borderColor, rotation, thickness);
        }

        /// <summary>Draws a cylinder between two arbitrary points with independently colored fill and wireframe border.</summary>
        /// <param name="thickness">Border thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void DrawCylinder(Vector3 startPos, Vector3 endPos, float startRadius, float endRadius, int sides, Color fillColor, Color borderColor, float thickness = -1f)
        {
            FillCylinder(startPos, endPos, startRadius, endRadius, sides, fillColor);
            BorderCylinder(startPos, endPos, startRadius, endRadius, sides, borderColor, thickness);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 RotateAxis(in Vector3 axis, in Quaternion rotation)
            => (rotation == default || rotation == Quaternion.Identity) ? axis : Vector3.Transform(axis, rotation);

        // =====================================================================
        // CAPSULES
        // =====================================================================
        // Already fully orientable via the two endpoints — no separate rotation
        // parameter needed (same reasoning as the two-endpoint cylinder overload).

        /// <summary>Draws a filled capsule: a cylinder between the two points with hemispherical caps of <paramref name="radius"/>.</summary>
        /// <param name="slices">Radial subdivisions, or -1 for automatic LOD.</param>
        /// <param name="rings">Cap subdivisions, or -1 for automatic LOD.</param>
        public void FillCapsule(Vector3 startPos, Vector3 endPos, float radius, int slices, int rings, Color color)
        {
            ThrowIfNotBegun();
            Vector3 axis = endPos - startPos;
            float axisLenSq = axis.LengthSquared();

            if (axisLenSq < 1e-12f) // degenerate capsule: fall back to a sphere
            {
                FillSphere(startPos, radius, rings <= 0 ? DefaultCapsuleRings * 2 : rings * 2, slices, color);
                return;
            }

            Vector3 center = (startPos + endPos) * 0.5f;
            int s = ResolveSegments(slices, radius, center, DefaultCapsuleSlices);
            int r = ResolveSegments(rings, radius, center, DefaultCapsuleRings);
            if (s < 3) s = 3;
            if (r < 1) r = 1;

            Vector3 n = axis * (1f / MathF.Sqrt(axisLenSq));
            BuildBasis(n, out Vector3 t, out Vector3 b);

            TrigLut.SinCosStep(0, s, out float sin0, out float cos0);
            Vector3 dir0 = t * cos0 + b * sin0;

            for (int i = 1; i <= s; i++)
            {
                TrigLut.SinCosStep(i, s, out float sin1, out float cos1);
                Vector3 dir1 = t * cos1 + b * sin1;
                PushQuadLit(startPos + dir0 * radius, startPos + dir1 * radius, endPos + dir1 * radius, endPos + dir0 * radius, color);
                dir0 = dir1;
            }

            FillHemisphere(endPos, radius, n, t, b, s, r, color, flipWinding: false);
            // The start cap bulges the opposite way (-n) but reuses the SAME t/b basis as the
            // end cap and the shaft body, so its equatorial ring lines up angle-for-angle with
            // the shaft's own start ring (a freshly-rebuilt basis for -n wouldn't necessarily
            // agree on where angle 0 points, tearing the seam). Reusing t/b with up flipped
            // makes (t, b, up) left-handed instead of right-handed, so the quad winding needs
            // flipping to still face outward — flipWinding does that without touching the seam.
            FillHemisphere(startPos, radius, -n, t, b, s, r, color, flipWinding: true);
        }

        /// <summary>Emits a filled hemisphere bulging along <paramref name="up"/>. Used for capsule caps.</summary>
        private void FillHemisphere(in Vector3 center, float radius, in Vector3 up, in Vector3 t, in Vector3 b, int slices, int rings, in Color color, bool flipWinding)
        {
            for (int i = 0; i < rings; i++)
            {
                SinCosQuarter(i, rings, out float sinLat0, out float cosLat0); // 0 at the equator, PI/2 at the pole
                SinCosQuarter(i + 1, rings, out float sinLat1, out float cosLat1);

                float ringRadius0 = cosLat0 * radius, height0 = sinLat0 * radius;
                float ringRadius1 = cosLat1 * radius, height1 = sinLat1 * radius;

                Vector3 rowCenter0 = center + up * height0;
                Vector3 rowCenter1 = center + up * height1;

                TrigLut.SinCosStep(0, slices, out float sin0, out float cos0);
                Vector3 dir0 = t * cos0 + b * sin0;

                for (int j = 1; j <= slices; j++)
                {
                    TrigLut.SinCosStep(j, slices, out float sin1, out float cos1);
                    Vector3 dir1 = t * cos1 + b * sin1;

                    Vector3 qa = rowCenter0 + dir0 * ringRadius0;
                    Vector3 qb = rowCenter0 + dir1 * ringRadius0;
                    Vector3 qc = rowCenter1 + dir1 * ringRadius1;
                    Vector3 qd = rowCenter1 + dir0 * ringRadius1;
                    // Exact outward sphere normal, not an edge cross-product — the tip ring
                    // (ringRadius1 == 0) makes qc == qd, degenerating the cross-product normal
                    // to NaN (see FillSphere). Same fix, same reason.
                    Vector3 quadNormal = Vector3.Normalize(qa + qb + qc + qd - 4f * center);
                    if (flipWinding) PushQuadLit(qa, qd, qc, qb, quadNormal, color);
                    else PushQuadLit(qa, qb, qc, qd, quadNormal, color);

                    dir0 = dir1;
                }
            }
        }

        /// <summary>Draws a capsule wireframe (body rails plus cap meridians and rings).</summary>
        /// <param name="thickness">Line thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void BorderCapsule(Vector3 startPos, Vector3 endPos, float radius, int slices, int rings, Color color, float thickness = -1f)
        {
            ThrowIfNotBegun();
            Vector3 axis = endPos - startPos;
            float axisLenSq = axis.LengthSquared();

            if (axisLenSq < 1e-12f)
            {
                BorderSphere(startPos, radius, rings <= 0 ? DefaultCapsuleRings * 2 : rings * 2, slices, color, thickness);
                return;
            }

            float w = thickness > 0f ? thickness : DefaultLineThickness;
            Vector3 center = (startPos + endPos) * 0.5f;
            int s = ResolveSegments(slices, radius, center, DefaultCapsuleSlices);
            int r = ResolveSegments(rings, radius, center, DefaultCapsuleRings);
            if (s < 3) s = 3;
            if (r < 1) r = 1;

            Vector3 n = axis * (1f / MathF.Sqrt(axisLenSq));
            BuildBasis(n, out Vector3 t, out Vector3 b);

            TrigLut.SinCosStep(0, s, out float sin0, out float cos0);
            Vector3 dir0 = t * cos0 + b * sin0;

            for (int i = 1; i <= s; i++)
            {
                TrigLut.SinCosStep(i, s, out float sin1, out float cos1);
                Vector3 dir1 = t * cos1 + b * sin1;

                DrawLine3D(startPos + dir0 * radius, endPos + dir0 * radius, w, color);   // rail
                DrawLine3D(startPos + dir0 * radius, startPos + dir1 * radius, w, color); // bottom ring
                DrawLine3D(endPos + dir0 * radius, endPos + dir1 * radius, w, color);     // top ring

                dir0 = dir1;
            }

            BorderHemisphere(endPos, radius, n, t, b, s, r, color, w);
            BorderHemisphere(startPos, radius, -n, t, b, s, r, color, w);
        }

        /// <summary>Emits the wireframe of a hemispherical capsule cap.</summary>
        private void BorderHemisphere(in Vector3 center, float radius, in Vector3 up, in Vector3 t, in Vector3 b, int slices, int rings, in Color color, float thickness)
        {
            for (int i = 1; i < rings; i++) // latitude rings; equator already drawn by the body
            {
                SinCosQuarter(i, rings, out float sinLat, out float cosLat);
                Vector3 rowCenter = center + up * (sinLat * radius);
                float ringRadius = cosLat * radius;

                TrigLut.SinCosStep(0, slices, out float sin0, out float cos0);
                Vector3 prev = rowCenter + (t * cos0 + b * sin0) * ringRadius;

                for (int j = 1; j <= slices; j++)
                {
                    TrigLut.SinCosStep(j, slices, out float sin1, out float cos1);
                    Vector3 cur = rowCenter + (t * cos1 + b * sin1) * ringRadius;
                    DrawLine3D(prev, cur, thickness, color);
                    prev = cur;
                }
            }

            Vector3 pole = center + up * radius;
            for (int j = 0; j < slices; j++) // meridians from the equator to the pole
            {
                TrigLut.SinCosStep(j, slices, out float sinLon, out float cosLon);
                Vector3 dir = t * cosLon + b * sinLon;

                Vector3 prev = center + dir * radius;
                for (int i = 1; i <= rings; i++)
                {
                    SinCosQuarter(i, rings, out float sinLat, out float cosLat);
                    Vector3 cur = i == rings ? pole : center + up * (sinLat * radius) + dir * (cosLat * radius);
                    DrawLine3D(prev, cur, thickness, color);
                    prev = cur;
                }
            }
        }

        /// <summary>Draws a capsule with independently colored fill and wireframe border.</summary>
        /// <param name="thickness">Border thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void DrawCapsule(Vector3 startPos, Vector3 endPos, float radius, int slices, int rings, Color fillColor, Color borderColor, float thickness = -1f)
        {
            FillCapsule(startPos, endPos, radius, slices, rings, fillColor);
            BorderCapsule(startPos, endPos, radius, slices, rings, borderColor, thickness);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SinCosQuarter(int step, int steps, out float sin, out float cos)
            => TrigLut.SinCosStep(step, steps * 4, out sin, out cos);

        // =====================================================================
        // TORUS (a common shape not otherwise covered)
        // =====================================================================

        /// <summary>Draws a filled torus (donut) centered at <paramref name="center"/>, lying flat on XZ (hole facing +Y) unless <paramref name="rotation"/> tilts it.</summary>
        /// <param name="radius">Distance from the center to the middle of the tube.</param>
        /// <param name="tubeRadius">Radius of the tube itself.</param>
        /// <param name="sides">Tube cross-section subdivisions, or -1 for automatic LOD.</param>
        /// <param name="rings">Ring subdivisions around the main radius, or -1 for automatic LOD.</param>
        public void FillTorus(Vector3 center, float radius, float tubeRadius, int sides, int rings, Color color, Quaternion rotation = default)
        {
            ThrowIfNotBegun();
            int s = ResolveSegments(sides, tubeRadius, center, DefaultCircleSegments / 2);
            int r = ResolveSegments(rings, radius, center, DefaultCircleSegments);
            if (s < 3) s = 3;
            if (r < 3) r = 3;

            bool hasRotation = rotation != default && rotation != Quaternion.Identity;

            Vector3 Point(int ringStep, int sideStep)
            {
                TrigLut.SinCosStep(ringStep, r, out float sinRing, out float cosRing);
                TrigLut.SinCosStep(sideStep, s, out float sinSide, out float cosSide);
                float ringRadius = radius + tubeRadius * cosSide;
                Vector3 local = new(ringRadius * cosRing, tubeRadius * sinSide, ringRadius * sinRing);
                return center + (hasRotation ? Vector3.Transform(local, rotation) : local);
            }

            for (int i = 0; i < r; i++)
            {
                for (int j = 0; j < s; j++)
                {
                    Vector3 a = Point(i, j), b = Point(i + 1, j), c = Point(i + 1, j + 1), d = Point(i, j + 1);
                    // b/d swapped (ring-then-side construction order gives an inward normal —
                    // same fix, same reason, as FillSphere above).
                    PushQuadLit(a, d, c, b, color);
                }
            }
        }

        /// <summary>Draws a torus wireframe (ring loops around the tube, plus meridian loops around the main radius).</summary>
        /// <param name="thickness">Line thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void BorderTorus(Vector3 center, float radius, float tubeRadius, int sides, int rings, Color color, Quaternion rotation = default, float thickness = -1f)
        {
            ThrowIfNotBegun();
            float w = thickness > 0f ? thickness : DefaultLineThickness;
            int s = ResolveSegments(sides, tubeRadius, center, DefaultCircleSegments / 2);
            int r = ResolveSegments(rings, radius, center, DefaultCircleSegments);
            if (s < 3) s = 3;
            if (r < 3) r = 3;

            bool hasRotation = rotation != default && rotation != Quaternion.Identity;

            Vector3 Point(int ringStep, int sideStep)
            {
                TrigLut.SinCosStep(ringStep, r, out float sinRing, out float cosRing);
                TrigLut.SinCosStep(sideStep, s, out float sinSide, out float cosSide);
                float ringRadius = radius + tubeRadius * cosSide;
                Vector3 local = new(ringRadius * cosRing, tubeRadius * sinSide, ringRadius * sinRing);
                return center + (hasRotation ? Vector3.Transform(local, rotation) : local);
            }

            for (int i = 0; i < r; i++) // tube cross-section loops
            {
                Vector3 prev = Point(i, 0);
                for (int j = 1; j <= s; j++)
                {
                    Vector3 cur = Point(i, j);
                    DrawLine3D(prev, cur, w, color);
                    prev = cur;
                }
            }
            for (int j = 0; j < s; j++) // meridian loops around the main radius
            {
                Vector3 prev = Point(0, j);
                for (int i = 1; i <= r; i++)
                {
                    Vector3 cur = Point(i, j);
                    DrawLine3D(prev, cur, w, color);
                    prev = cur;
                }
            }
        }

        /// <summary>Draws a torus with independently colored fill and wireframe border.</summary>
        /// <param name="thickness">Border thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void DrawTorus(Vector3 center, float radius, float tubeRadius, int sides, int rings, Color fillColor, Color borderColor, Quaternion rotation = default, float thickness = -1f)
        {
            FillTorus(center, radius, tubeRadius, sides, rings, fillColor, rotation);
            BorderTorus(center, radius, tubeRadius, sides, rings, borderColor, rotation, thickness);
        }

        // =====================================================================
        // PLANE
        // =====================================================================

        /// <summary>Draws a filled plane on the XZ axes, centered at <paramref name="centerPos"/>.</summary>
        public void FillPlane(Vector3 centerPos, Vector2 size, Color color)
        {
            ThrowIfNotBegun();
            GetPlaneCorners(centerPos, size, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d);
            PushQuadLit(a, b, c, d, color);
        }

        /// <summary>Draws a filled plane tilted so its face normal is <paramref name="normal"/> (the in-plane twist is arbitrary).</summary>
        public void FillPlane(Vector3 centerPos, Vector2 size, Vector3 normal, Color color)
        {
            ThrowIfNotBegun();
            Vector3 n = SafeNormalize(normal, Vector3.Up);
            BuildBasis(n, out Vector3 t, out Vector3 b);
            PushQuadLit(centerPos - t * (size.X * 0.5f) - b * (size.Y * 0.5f), centerPos - t * (size.X * 0.5f) + b * (size.Y * 0.5f),
                     centerPos + t * (size.X * 0.5f) + b * (size.Y * 0.5f), centerPos + t * (size.X * 0.5f) - b * (size.Y * 0.5f), color);
        }

        /// <summary>Draws a filled plane with a fully explicit orientation (tilt and twist).</summary>
        public void FillPlane(Vector3 centerPos, Vector2 size, Quaternion rotation, Color color)
        {
            ThrowIfNotBegun();
            GetPlaneCorners(centerPos, size, rotation, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d);
            PushQuadLit(a, b, c, d, color);
        }

        /// <summary>Draws the wireframe (4 edges) of a plane on the XZ axes.</summary>
        /// <param name="thickness">Line thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void BorderPlane(Vector3 centerPos, Vector2 size, Color color, float thickness = -1f)
        {
            ThrowIfNotBegun();
            float w = thickness > 0f ? thickness : DefaultLineThickness;
            GetPlaneCorners(centerPos, size, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d);
            DrawLine3D(a, b, w, color); DrawLine3D(b, c, w, color); DrawLine3D(c, d, w, color); DrawLine3D(d, a, w, color);
        }

        /// <summary>Draws the wireframe of a plane with a fully explicit orientation.</summary>
        /// <param name="thickness">Line thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void BorderPlane(Vector3 centerPos, Vector2 size, Quaternion rotation, Color color, float thickness = -1f)
        {
            ThrowIfNotBegun();
            float w = thickness > 0f ? thickness : DefaultLineThickness;
            GetPlaneCorners(centerPos, size, rotation, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d);
            DrawLine3D(a, b, w, color); DrawLine3D(b, c, w, color); DrawLine3D(c, d, w, color); DrawLine3D(d, a, w, color);
        }

        /// <summary>Draws a plane with independently colored fill and wireframe border.</summary>
        /// <param name="thickness">Border thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void DrawPlane(Vector3 centerPos, Vector2 size, Color fillColor, Color borderColor, float thickness = -1f)
        {
            FillPlane(centerPos, size, fillColor);
            BorderPlane(centerPos, size, borderColor, thickness);
        }

        /// <summary>Draws a plane with a fully explicit orientation and independently colored fill and wireframe border.</summary>
        /// <param name="thickness">Border thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void DrawPlane(Vector3 centerPos, Vector2 size, Quaternion rotation, Color fillColor, Color borderColor, float thickness = -1f)
        {
            FillPlane(centerPos, size, rotation, fillColor);
            BorderPlane(centerPos, size, rotation, borderColor, thickness);
        }

        private static void GetPlaneCorners(in Vector3 centerPos, in Vector2 size, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d)
        {
            float hx = size.X * 0.5f, hz = size.Y * 0.5f;
            a = new Vector3(centerPos.X - hx, centerPos.Y, centerPos.Z - hz);
            b = new Vector3(centerPos.X - hx, centerPos.Y, centerPos.Z + hz);
            c = new Vector3(centerPos.X + hx, centerPos.Y, centerPos.Z + hz);
            d = new Vector3(centerPos.X + hx, centerPos.Y, centerPos.Z - hz);
        }

        private static void GetPlaneCorners(in Vector3 centerPos, in Vector2 size, in Quaternion rotation,
            out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d)
        {
            bool hasRotation = rotation != default && rotation != Quaternion.Identity;
            float hx = size.X * 0.5f, hz = size.Y * 0.5f;
            Vector3 center = centerPos;
            Quaternion rot = rotation;

            Vector3 L(float sx, float sz)
            {
                Vector3 local = new(sx * hx, 0f, sz * hz);
                return center + (hasRotation ? Vector3.Transform(local, rot) : local);
            }

            a = L(-1, -1); b = L(-1, 1); c = L(1, 1); d = L(1, -1);
        }

        // =====================================================================
        // GRID
        // =====================================================================
        // Three explicitly-named methods (one per axis pair) instead of a single
        // method with a plane-selector parameter, so there's no runtime branch
        // on the hot path — each just plugs its own two constant basis vectors
        // into the shared line-emitting loop. Grid lines never include the axes
        // themselves — use DrawAxis for those, drawn separately so callers can
        // combine/omit/style them independently of the grid.
        //
        // Every 5th division is drawn as a "major" line: a darker color and one
        // pixel wider, matching common CAD/DCC grid conventions so large grids
        // stay readable. Minor lines use the cheap GPU line-list path
        // (DrawLine3DFast, fixed 1px, no camera facing) since they're the bulk
        // of the geometry; major lines are sparse enough (1 in 5) that the
        // pricier camera-facing DrawLine3D (which can vary width) doesn't matter.

        private const int MajorGridLineInterval = 5;

        /// <summary>Draws a grid on the XZ plane (the ground), centered at the origin.</summary>
        public void DrawGrid(int slices, float spacing) => DrawGridXZ(slices, spacing);

        /// <summary>Draws a grid on the XZ plane with an explicit origin and colors.</summary>
        public void DrawGrid(int slices, float spacing, Vector3 origin, Color lineColor, Color majorLineColor, bool showMajorLines = true)
            => DrawGridXZ(slices, spacing, origin, lineColor, majorLineColor, showMajorLines);

        /// <summary>Draws a grid on the XZ plane (the ground), centered at the origin.</summary>
        public void DrawGridXZ(int slices, float spacing)
            => DrawGridXZ(slices, spacing, Vector3.Zero, DefaultGridLineColor, DefaultGridMajorLineColor);

        /// <summary>Draws a grid on the XZ plane with an explicit origin and colors.</summary>
        public void DrawGridXZ(int slices, float spacing, Vector3 origin, Color lineColor, Color majorLineColor, bool showMajorLines = true)
            => DrawGridPlane(slices, spacing, origin, Vector3.UnitX, Vector3.UnitZ, lineColor, majorLineColor, showMajorLines);

        /// <summary>Draws a grid on the XY plane (a wall facing +Z/-Z), centered at the origin.</summary>
        public void DrawGridXY(int slices, float spacing)
            => DrawGridXY(slices, spacing, Vector3.Zero, DefaultGridLineColor, DefaultGridMajorLineColor);

        /// <summary>Draws a grid on the XY plane with an explicit origin and colors.</summary>
        public void DrawGridXY(int slices, float spacing, Vector3 origin, Color lineColor, Color majorLineColor, bool showMajorLines = true)
            => DrawGridPlane(slices, spacing, origin, Vector3.UnitX, Vector3.UnitY, lineColor, majorLineColor, showMajorLines);

        /// <summary>Draws a grid on the YZ plane (a wall facing +X/-X), centered at the origin.</summary>
        public void DrawGridYZ(int slices, float spacing)
            => DrawGridYZ(slices, spacing, Vector3.Zero, DefaultGridLineColor, DefaultGridMajorLineColor);

        /// <summary>Draws a grid on the YZ plane with an explicit origin and colors.</summary>
        public void DrawGridYZ(int slices, float spacing, Vector3 origin, Color lineColor, Color majorLineColor, bool showMajorLines = true)
            => DrawGridPlane(slices, spacing, origin, Vector3.UnitY, Vector3.UnitZ, lineColor, majorLineColor, showMajorLines);

        // Low alpha by default so a reference grid reads as a subtle backdrop for a moving
        // camera/simulation rather than competing with whatever's being drawn on top of it.
        // Major lines (every MajorGridLineInterval divisions) are darker and one pixel wider
        // so large grids keep a sense of scale; pass explicit colors to override either.
        private static readonly Color DefaultGridLineColor = new(0.75f, 0.75f, 0.75f, 0.35f);
        private static readonly Color DefaultGridMajorLineColor = new(0.45f, 0.45f, 0.45f, 0.4f);

        /// <param name="showMajorLines">When true (default), every <see cref="MajorGridLineInterval"/>th division uses <paramref name="majorLineColor"/> and a wider stroke. When false, every line is drawn uniformly with <paramref name="lineColor"/>.</param>
        private void DrawGridPlane(int slices, float spacing, in Vector3 origin, in Vector3 axisA, in Vector3 axisB, Color lineColor, Color majorLineColor, bool showMajorLines = true)
        {
            ThrowIfNotBegun();
            int halfSlices = slices / 2;
            float extent = halfSlices * spacing;

            for (int i = -halfSlices; i <= halfSlices; i++)
            {
                bool major = showMajorLines && i % MajorGridLineInterval == 0;
                Vector3 offsetA = axisA * (i * spacing);
                Vector3 extentB = axisB * extent;
                Vector3 offsetB = axisB * (i * spacing);
                Vector3 extentA = axisA * extent;

                if (major)
                {
                    DrawLine3D(origin + offsetA - extentB, origin + offsetA + extentB, DefaultLineThickness + 1f, majorLineColor);
                    DrawLine3D(origin - extentA + offsetB, origin + extentA + offsetB, DefaultLineThickness + 1f, majorLineColor);
                }
                else
                {
                    DrawLine3DFast(origin + offsetA - extentB, origin + offsetA + extentB, lineColor);
                    DrawLine3DFast(origin - extentA + offsetB, origin + extentA + offsetB, lineColor);
                }
            }
        }

        // =====================================================================
        // AXES
        // =====================================================================
        // Split out from the grid methods on purpose: an axis triad is a
        // different concern (orientation reference, not a measuring surface) and
        // callers frequently want one without the other.

        // Similar low-alpha, dark-gray styling to the grid's own lines so the
        // two read as one family when drawn together.
        private static readonly Color DefaultAxisColor = new(0.15f, 0.15f, 0.15f, 0.65f);

        /// <summary>Draws an X/Y/Z axis triad of length <paramref name="size"/> (in each direction) through the origin, using a dark, grid-like default color.</summary>
        public void DrawAxis(float size) => DrawAxis(Vector3.Zero, size, DefaultAxisColor);

        /// <summary>Draws an X/Y/Z axis triad of length <paramref name="size"/> (in each direction) through the origin.</summary>
        public void DrawAxis(float size, Color color) => DrawAxis(Vector3.Zero, size, color);

        /// <summary>Draws an X/Y/Z axis triad of length <paramref name="size"/> (in each direction) through <paramref name="origin"/>, all one color — for the classic red/green/blue gizmo instead, use <see cref="DrawAxes"/>.</summary>
        public void DrawAxis(Vector3 origin, float size, Color color)
        {
            ThrowIfNotBegun();
            DrawLine3DFast(origin - Vector3.UnitX * size, origin + Vector3.UnitX * size, color);
            DrawLine3DFast(origin - Vector3.UnitY * size, origin + Vector3.UnitY * size, color);
            DrawLine3DFast(origin - Vector3.UnitZ * size, origin + Vector3.UnitZ * size, color);
        }

        // =====================================================================
        // DASHED LINE (2D library parity)
        // =====================================================================

        /// <summary>Draws a dashed line using <see cref="DefaultLineThickness"/> — useful for predicted paths, ranges, or any "not solid" debug indicator.</summary>
        public void DrawLine3DDashed(Vector3 start, Vector3 end, float dashLength, float gapLength, Color color)
            => DrawLine3DDashed(start, end, dashLength, gapLength, DefaultLineThickness, color);

        /// <summary>Draws a dashed line with an explicit thickness.</summary>
        public void DrawLine3DDashed(Vector3 start, Vector3 end, float dashLength, float gapLength, float thickness, Color color)
        {
            if (dashLength <= 0f || gapLength < 0f)
                return;

            Vector3 delta = end - start;
            float total = delta.Length();
            if (total < 1e-6f)
                return;

            Vector3 dir = delta / total;
            float period = dashLength + gapLength;
            float travelled = 0f;

            while (travelled < total)
            {
                float segEnd = MathF.Min(travelled + dashLength, total);
                DrawLine3D(start + dir * travelled, start + dir * segEnd, thickness, color);
                travelled += period;
            }
        }

        // =====================================================================
        // SPLINES (2D library parity — smooth paths for camera rigs, agent
        // trajectories, rivers/roads following terrain, prediction curves)
        // =====================================================================

        /// <summary>Draws a smooth curve that passes through every point in <paramref name="points"/> (Catmull-Rom). Needs at least 4 points.</summary>
        public void DrawSplineCatmullRom3D(ReadOnlySpan<Vector3> points, Color color, int segmentsPerPiece = 16)
            => DrawSplineCatmullRom3D(points, DefaultLineThickness, color, segmentsPerPiece);

        /// <summary>Draws a smooth Catmull-Rom curve with an explicit line thickness.</summary>
        public void DrawSplineCatmullRom3D(ReadOnlySpan<Vector3> points, float thickness, Color color, int segmentsPerPiece = 16)
        {
            ThrowIfNotBegun();
            if (points.Length < 4 || segmentsPerPiece < 1)
                return;

            Vector3 prev = points[1];
            for (int i = 1; i < points.Length - 2; i++)
            {
                for (int s = 1; s <= segmentsPerPiece; s++)
                {
                    float t = s / (float)segmentsPerPiece;
                    Vector3 cur = Vector3.CatmullRom(points[i - 1], points[i], points[i + 1], points[i + 2], t);
                    DrawLine3D(prev, cur, thickness, color);
                    prev = cur;
                }
            }
        }

        /// <summary>Draws a cubic Bezier spline: <c>[p1, c2, c3, p4, c5, c6, p7, ...]</c> — needs <c>3n + 1</c> points for <c>n</c> segments.</summary>
        public void DrawSplineBezierCubic3D(ReadOnlySpan<Vector3> points, Color color, int segmentsPerPiece = 16)
            => DrawSplineBezierCubic3D(points, DefaultLineThickness, color, segmentsPerPiece);

        /// <summary>Draws a cubic Bezier spline with an explicit line thickness.</summary>
        public void DrawSplineBezierCubic3D(ReadOnlySpan<Vector3> points, float thickness, Color color, int segmentsPerPiece = 16)
        {
            ThrowIfNotBegun();
            if (points.Length < 4 || (points.Length - 1) % 3 != 0 || segmentsPerPiece < 1)
                return;

            int segmentCount = (points.Length - 1) / 3;
            Vector3 prev = points[0];
            for (int seg = 0; seg < segmentCount; seg++)
            {
                int b = seg * 3;
                for (int s = 1; s <= segmentsPerPiece; s++)
                {
                    float t = s / (float)segmentsPerPiece;
                    Vector3 cur = CubicBezier(points[b], points[b + 1], points[b + 2], points[b + 3], t);
                    DrawLine3D(prev, cur, thickness, color);
                    prev = cur;
                }
            }
        }

        private static Vector3 CubicBezier(in Vector3 p0, in Vector3 c1, in Vector3 c2, in Vector3 p3, float t)
        {
            float u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * p3;
        }

        // =====================================================================
        // HEIGHTMAP / TERRAIN (for terrain-generator prototyping: a grid of
        // heights becomes a triangulated ground mesh)
        // =====================================================================

        /// <summary>
        /// Draws a filled terrain mesh from a heightmap. <paramref name="heights"/>[x, z] is
        /// the Y coordinate of that grid vertex; world position is
        /// <c>origin + (x * cellSize.X, heights[x,z], z * cellSize.Y)</c>.
        /// </summary>
        public void FillHeightmap(float[,] heights, Vector3 origin, Vector2 cellSize, Color color)
        {
            ThrowIfNotBegun();
            int width = heights.GetLength(0), depth = heights.GetLength(1);
            if (width < 2 || depth < 2)
                return;

            for (int z = 0; z < depth - 1; z++)
            {
                for (int x = 0; x < width - 1; x++)
                {
                    Vector3 p00 = HeightmapVertex(heights, origin, cellSize, x, z);
                    Vector3 p10 = HeightmapVertex(heights, origin, cellSize, x + 1, z);
                    Vector3 p11 = HeightmapVertex(heights, origin, cellSize, x + 1, z + 1);
                    Vector3 p01 = HeightmapVertex(heights, origin, cellSize, x, z + 1);
                    // Winding matches FillPlane's convention (p00->p01->p11->p10, +Z then +X)
                    // so the computed face normal points +Y (up) for a flat/gently-sloped
                    // heightmap, not -Y — matters once LightingEnabled is on, and for anyone
                    // who supplies a culling rasterizer state to Begin() (CullNone is only
                    // the default, not the only option).
                    PushQuadLit(p00, p01, p11, p10, color);
                }
            }
        }

        /// <summary>
        /// Draws a filled terrain mesh with an independent color per grid vertex — e.g. a
        /// biome/temperature/elevation-band map from a simulation. Not a gradient (no
        /// interpolation is added beyond the GPU's own vertex-color blending inside each
        /// triangle, same as <see cref="FillTriangle3D(Vector3,Vector3,Vector3,Color,Quaternion,Vector3?)"/>'s
        /// implicit per-vertex color blending) — <paramref name="colors"/> must be the same
        /// size as <paramref name="heights"/>.
        /// </summary>
        public void FillHeightmap(float[,] heights, Color[,] colors, Vector3 origin, Vector2 cellSize)
        {
            ThrowIfNotBegun();
            int width = heights.GetLength(0), depth = heights.GetLength(1);
            if (width < 2 || depth < 2)
                return;

            for (int z = 0; z < depth - 1; z++)
            {
                for (int x = 0; x < width - 1; x++)
                {
                    Vector3 p00 = HeightmapVertex(heights, origin, cellSize, x, z);
                    Vector3 p10 = HeightmapVertex(heights, origin, cellSize, x + 1, z);
                    Vector3 p11 = HeightmapVertex(heights, origin, cellSize, x + 1, z + 1);
                    Vector3 p01 = HeightmapVertex(heights, origin, cellSize, x, z + 1);
                    // Same winding fix as the solid-color overload above (+Y-facing, matches FillPlane).
                    PushTriangle(p00, p01, p11, colors[x, z]);
                    PushTriangle(p00, p11, p10, colors[x, z]);
                }
            }
        }

        /// <summary>Draws a heightmap's wireframe (each grid cell's 4 edges) — useful for seeing the underlying mesh resolution while prototyping.</summary>
        /// <param name="thickness">Line thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void BorderHeightmap(float[,] heights, Vector3 origin, Vector2 cellSize, Color color, float thickness = -1f)
        {
            ThrowIfNotBegun();
            int width = heights.GetLength(0), depth = heights.GetLength(1);
            if (width < 2 || depth < 2)
                return;

            float w = thickness > 0f ? thickness : DefaultLineThickness;

            for (int z = 0; z < depth; z++)
                for (int x = 0; x < width - 1; x++)
                    DrawLine3D(HeightmapVertex(heights, origin, cellSize, x, z), HeightmapVertex(heights, origin, cellSize, x + 1, z), w, color);

            for (int x = 0; x < width; x++)
                for (int z = 0; z < depth - 1; z++)
                    DrawLine3D(HeightmapVertex(heights, origin, cellSize, x, z), HeightmapVertex(heights, origin, cellSize, x, z + 1), w, color);
        }

        /// <summary>Draws a heightmap with independently colored fill and wireframe.</summary>
        /// <param name="thickness">Border thickness; &lt;= 0 (the default) uses <see cref="DefaultLineThickness"/>.</param>
        public void DrawHeightmap(float[,] heights, Vector3 origin, Vector2 cellSize, Color fillColor, Color borderColor, float thickness = -1f)
        {
            FillHeightmap(heights, origin, cellSize, fillColor);
            BorderHeightmap(heights, origin, cellSize, borderColor, thickness);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 HeightmapVertex(float[,] heights, in Vector3 origin, in Vector2 cellSize, int x, int z)
            => new(origin.X + x * cellSize.X, origin.Y + heights[x, z], origin.Z + z * cellSize.Y);

        // =====================================================================
        // BULK HELPERS (prototyping sugar for cellular automata / boids /
        // particle-style scenes: many identical shapes, one call per frame)
        // =====================================================================

        /// <summary>Draws a filled cube of the same size/color/rotation at every position in <paramref name="positions"/>.</summary>
        public void FillCubes(ReadOnlySpan<Vector3> positions, Vector3 size, Color color, Quaternion rotation = default)
        {
            for (int i = 0; i < positions.Length; i++)
                FillCube(positions[i], size, color, rotation);
        }

        /// <summary>Draws a filled sphere of the same radius/color at every position in <paramref name="positions"/>.</summary>
        public void FillSpheres(ReadOnlySpan<Vector3> positions, float radius, Color color)
        {
            for (int i = 0; i < positions.Length; i++)
                FillSphere(positions[i], radius, color);
        }

        // =====================================================================
        // EXTRA HELPERS (debug gizmos — no Fill/Border split, unlike the shapes above)
        // =====================================================================

        /// <summary>Draws a red/green/blue X/Y/Z gizmo at <paramref name="origin"/> — for a single-color axis triad instead (matching a grid's own color), use <see cref="DrawAxis(Vector3,float,Color)"/>.</summary>
        public void DrawAxes(Vector3 origin, float length)
        {
            DrawLine3D(origin, origin + Vector3.UnitX * length, Color.Red);
            DrawLine3D(origin, origin + Vector3.UnitY * length, Color.Lime);
            DrawLine3D(origin, origin + Vector3.UnitZ * length, Color.Blue);
        }

        /// <summary>
        /// Draws an arrow from <paramref name="start"/> to <paramref name="end"/> with a fixed
        /// shaft width (<see cref="DefaultLineThickness"/>, via <see cref="DrawLine3D(Vector3,Vector3,Color)"/>)
        /// and a head sized purely from <paramref name="headRadius"/> — a simpler, fewer-argument
        /// alternative to <see cref="DrawArrow(Vector3,Vector3,Color)"/>'s overload family for
        /// when you don't need control over the shaft width or head length independently.
        /// </summary>
        public void DrawArrow3D(Vector3 start, Vector3 end, float headRadius, int sides, Color color)
        {
            Vector3 dir = end - start;
            float length = dir.Length();
            if (length < 1e-6f)
                return;

            dir *= 1f / length;
            float headLength = MathF.Min(headRadius * 3f, length * 0.5f);
            Vector3 headBase = end - dir * headLength;

            DrawLine3D(start, headBase, color);
            FillCylinder(headBase, end, headRadius, 0f, sides, color);
        }

        /// <summary>Draws the wireframe of a view frustum, useful for debugging cameras.</summary>
        public void DrawFrustumWires(BoundingFrustum frustum, Color color)
        {
            Vector3[] corners = new Vector3[8]; // corner order documented by MonoGame: near 0..3 then far 4..7
            frustum.GetCorners(corners);

            for (int i = 0; i < 4; i++)
            {
                DrawLine3D(corners[i], corners[(i + 1) & 3], color);
                DrawLine3D(corners[i + 4], corners[((i + 1) & 3) + 4], color);
                DrawLine3D(corners[i], corners[i + 4], color);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 SafeNormalize(in Vector3 v, in Vector3 fallback)
        {
            float lenSq = v.LengthSquared();
            return lenSq < 1e-12f ? fallback : v * (1f / MathF.Sqrt(lenSq));
        }
    }
}
