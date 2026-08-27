using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives.Primitives3D;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Renders <c>Primitive3DBatch.FillBillboard</c> from two very different camera angles and
    /// confirms it covers roughly the same number of screen pixels both times -- the actual
    /// property "always faces the camera" promises, not just "emitted some triangles." A fixed,
    /// non-billboarded reference plane rendered from the same two cameras is used as a control:
    /// it must show a large coverage swing between the two angles, or the test setup itself
    /// wouldn't be exercising orientation-sensitivity at all.
    /// </summary>
    internal static class BillboardTests
    {
        public static void Run(GraphicsDevice device, TestResults results)
        {
            results.Check("FillBillboard: apparent size stays consistent across very different camera angles (fixed-plane control swings hard)", () =>
            {
                const int size = 200;
                using var rt = new RenderTarget2D(device, size, size);
                using var batch = new Primitive3DBatch(device);

                // Same distance from the origin, 90 degrees apart, both horizontal -- a fixed
                // +Z-facing plane looks face-on from camA and edge-on (near-invisible) from camB.
                var camA = new Camera3D(position: new Vector3(0, 0, 8), target: Vector3.Zero, up: Vector3.Up, fovy: 50f);
                var camB = new Camera3D(position: new Vector3(8, 0, 0), target: Vector3.Zero, up: Vector3.Up, fovy: 50f);

                int CountDominant(Camera3D camera, Action draw, Func<Color, bool> isMatch)
                {
                    device.SetRenderTarget(rt);
                    device.Clear(Color.Black);
                    batch.Begin(camera);
                    draw();
                    batch.End();
                    device.SetRenderTarget(null);

                    var pixels = new Color[size * size];
                    rt.GetData(pixels);
                    int count = 0;
                    foreach (Color p in pixels) if (isMatch(p)) count++;
                    return count;
                }

                bool isRed(Color c) => c.R > 150 && c.G < 80 && c.B < 80;
                bool isBlue(Color c) => c.B > 150 && c.R < 80 && c.G < 80;

                int billboardA = CountDominant(camA, () => batch.FillBillboard(Vector3.Zero, new Vector2(2f, 2f), Color.Red), isRed);
                int billboardB = CountDominant(camB, () => batch.FillBillboard(Vector3.Zero, new Vector2(2f, 2f), Color.Red), isRed);

                int planeA = CountDominant(camA, () => batch.FillPlane(Vector3.Zero, new Vector2(2f, 2f), Vector3.UnitZ, Color.Blue), isBlue);
                int planeB = CountDominant(camB, () => batch.FillPlane(Vector3.Zero, new Vector2(2f, 2f), Vector3.UnitZ, Color.Blue), isBlue);

                if (billboardA < 100) return $"billboard from camA covered only {billboardA} px -- did it even render?";
                if (billboardB < 100) return $"billboard from camB covered only {billboardB} px -- did it even render?";

                float billboardRatio = (float)Math.Max(billboardA, billboardB) / Math.Min(billboardA, billboardB);
                if (billboardRatio > 1.3f)
                    return $"expected the billboard's coverage to stay roughly constant across camera angles, got camA={billboardA}px camB={billboardB}px (ratio {billboardRatio:F2})";

                // Control: the fixed plane must actually swing hard between these two angles, or
                // this test isn't proving anything about orientation-sensitivity at all.
                if (planeA < 100) return $"control plane from camA covered only {planeA} px -- test setup itself is broken";
                if (planeB > planeA / 3)
                    return $"expected the fixed-orientation control plane to nearly disappear edge-on from camB, got planeA={planeA}px planeB={planeB}px";

                return null;
            });

            results.Check("BorderBillboard/DrawBillboard: both emit geometry without throwing", () =>
            {
                using var batch = new Primitive3DBatch(device);
                var camera = new Camera3D(position: new Vector3(0, 0, 8), target: Vector3.Zero, up: Vector3.Up, fovy: 50f);

                // BorderBillboard draws its 4 edges via DrawLine3D, which (like every other Border*
                // method in this batch) emits billboarded-ribbon triangles by default, not raw GPU
                // lines -- same reason ShapeTests3D's BorderPlane check also expects triangles.
                // TrianglesSubmitted only tallies on Flush/End, not per draw call -- check after End().
                batch.Begin(camera);
                batch.BorderBillboard(Vector3.Zero, new Vector2(2f, 2f), Color.Black);
                batch.End();
                if (batch.TrianglesSubmitted == 0) return "expected BorderBillboard to submit triangle geometry (its ribbon-line edges)";

                batch.Begin(camera);
                batch.DrawBillboard(new Vector3(1, 0, 0), new Vector2(1f, 1f), Color.Red, Color.Black);
                batch.End();
                if (batch.TrianglesSubmitted == 0) return "expected DrawBillboard to submit triangle geometry";

                return null;
            });
        }
    }
}
