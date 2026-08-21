using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives;
using MonoPrimitives.Primitives3D;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Checks for 3D's own <c>DebugFont5x7.cs</c> (<see cref="Primitive3DBatch.DrawString3D"/>/
    /// <see cref="Primitive3DBatch.MeasureText3D"/>/<see cref="Primitive3DBatch.GetBillboardAxes"/>)
    /// — the shared glyph bitmap data itself is covered by <see cref="FontGlyphs5x7Tests"/>. Needs a
    /// real <see cref="GraphicsDevice"/> (billboarding reads it through <see cref="Primitive3DBatch.Begin(Camera3D,BlendState,DepthStencilState,RasterizerState,Matrix?)"/>),
    /// which <see cref="TestRunnerGame"/> already provides.
    /// </summary>
    internal static class DebugFont3DTests
    {
        public static void Run(Primitive3DBatch batch, Camera3D camera, GraphicsDevice device, TestResults results)
        {
            results.Check("GetBillboardAxes returns an orthonormal basis facing the camera, matching its screen-right convention, from any side", () =>
            {
                foreach (Vector3 camPos in new[]
                {
                    new Vector3(0, 0, 10), new Vector3(10, 0, 0), new Vector3(-5, 5, -5), new Vector3(0, 0, -10),
                })
                {
                    var cam = new Camera3D(camPos, Vector3.Zero, Vector3.Up);
                    batch.Begin(cam);
                    batch.GetBillboardAxes(Vector3.Zero, out Vector3 right, out Vector3 up);
                    batch.End();

                    if (MathF.Abs(right.Length() - 1f) > 1e-3f || MathF.Abs(up.Length() - 1f) > 1e-3f)
                        return $"non-unit billboard axis for camera at {camPos}: |right|={right.Length()} |up|={up.Length()}";
                    if (MathF.Abs(Vector3.Dot(right, up)) > 1e-3f)
                        return $"billboard right/up not orthogonal for camera at {camPos}";

                    // The billboard faces the glyph position at the camera, looking at the same target
                    // (the origin) that it's labeling -- so its right/up must match the camera's own
                    // rendered view-space X/Y axes exactly (read straight off GetViewMatrix, the ground
                    // truth -- NOT cam.UpNormalized, which is only Up normalized, not orthogonalized
                    // against Forward, so it only coincides with the true render-up when the camera
                    // happens to sit level with its target; same subtlety Camera3DTests' own basis
                    // check ran into).
                    Matrix view = cam.GetViewMatrix();
                    Vector3 viewRight = new(view.M11, view.M21, view.M31);
                    Vector3 viewUp = new(view.M12, view.M22, view.M32);
                    if (Vector3.Distance(right, viewRight) > 1e-3f || Vector3.Distance(up, viewUp) > 1e-3f)
                        return $"billboard axes don't match the view matrix's own X/Y axes for camera at {camPos}: billboard=({right},{up}) view=({viewRight},{viewUp})";
                }
                return null;
            });

            results.Check("GetBillboardAxes falls back to a full camera-facing basis when looking straight down world +Y (no well-defined cylindrical up)", () =>
            {
                var cam = new Camera3D(new Vector3(0, 10, 0), Vector3.Zero, Vector3.Forward);
                batch.Begin(cam);
                batch.GetBillboardAxes(Vector3.Zero, out Vector3 right, out Vector3 up);
                batch.End();

                if (MathF.Abs(right.Length() - 1f) > 1e-3f || MathF.Abs(up.Length() - 1f) > 1e-3f)
                    return $"non-unit fallback axis: |right|={right.Length()} |up|={up.Length()}";
                if (MathF.Abs(Vector3.Dot(right, up)) > 1e-3f)
                    return "fallback right/up not orthogonal";
                Vector3 forward = Vector3.Normalize(cam.Position - Vector3.Zero);
                if (MathF.Abs(Vector3.Dot(right, forward)) > 1e-3f || MathF.Abs(Vector3.Dot(up, forward)) > 1e-3f)
                    return "fallback axes not perpendicular to the camera-facing direction";
                return null;
            });

            results.Check("DrawString3D emits geometry only for non-space glyphs, and MeasureText3D matches FontGlyphs5x7.MeasureText", () =>
            {
                batch.Begin(camera);
                batch.DrawString3D("  ", Vector3.Zero, 1f, Color.White); // spaces only
                batch.End();
                int trianglesForSpaces = batch.TrianglesSubmitted;
                if (trianglesForSpaces != 0) return $"space-only text emitted {trianglesForSpaces} triangles, expected 0";

                batch.Begin(camera);
                batch.DrawString3D("A", Vector3.Zero, 1f, Color.White);
                batch.End();
                int trianglesForA = batch.TrianglesSubmitted;
                if (trianglesForA <= 0) return "\"A\" emitted no geometry";

                batch.Begin(camera);
                batch.DrawString3D("AA", Vector3.Zero, 1f, Color.White);
                batch.End();
                int trianglesForAA = batch.TrianglesSubmitted;
                if (trianglesForAA != trianglesForA * 2) return $"\"AA\" emitted {trianglesForAA} triangles, expected exactly {trianglesForA * 2} (two identical glyphs)";

                Vector2 measured = Primitive3DBatch.MeasureText3D("AB\nA", pixelSize: 2f, glyphSpacing: 1f, lineSpacing: 2f);
                (float w, float h) = FontGlyphs5x7.MeasureText("AB\nA", pixelSize: 2f, glyphSpacing: 1f, lineSpacing: 2f);
                if (measured.X != w || measured.Y != h) return $"MeasureText3D={measured} != FontGlyphs5x7.MeasureText=({w},{h})";
                return null;
            });

            results.Check("DrawString3D throws if not begun, and no-ops (no exception) for null/empty text or non-positive pixelSize", () =>
            {
                var freshBatch = new Primitive3DBatch(device);
                try
                {
                    freshBatch.DrawString3D("A", Vector3.Zero, 1f, Color.White);
                    return "DrawString3D didn't throw when called before Begin";
                }
                catch (InvalidOperationException) { /* expected */ }

                batch.Begin(camera);
                batch.DrawString3D(null!, Vector3.Zero, 1f, Color.White);
                batch.DrawString3D("", Vector3.Zero, 1f, Color.White);
                batch.DrawString3D("A", Vector3.Zero, 0f, Color.White);
                batch.End();
                if (batch.TrianglesSubmitted != 0) return $"null/empty/zero-size text emitted {batch.TrianglesSubmitted} triangles, expected 0";
                return null;
            });
        }
    }
}
