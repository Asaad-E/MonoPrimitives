using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives3D;

namespace MonoPrimitives.Tests
{
    /// <summary>Generates every 3D shape once and checks it actually emitted geometry without throwing.</summary>
    internal static class ShapeTests3D
    {
        public static void Run(Primitive3DBatch batch, Camera3D camera, GraphicsDevice device, TestResults results)
        {
            Check(results, "FillCube", batch, camera, () =>
                batch.FillCube(Vector3.Zero, 2f, 2f, 2f, Color.Red), expectTriangles: true);

            Check(results, "BorderCube", batch, camera, () =>
                batch.BorderCube(Vector3.Zero, 2f, 2f, 2f, Color.Black), expectTriangles: true);

            Check(results, "DrawCubeV (fill+border)", batch, camera, () =>
                batch.DrawCubeV(Vector3.Zero, Vector3.One * 2f, Color.Red, Color.Black), expectTriangles: true);

            Check(results, "FillSphereEx", batch, camera, () =>
                batch.FillSphereEx(Vector3.Zero, 3f, 16, 16, Color.Red), expectTriangles: true);

            Check(results, "BorderSphereEx", batch, camera, () =>
                batch.BorderSphereEx(Vector3.Zero, 3f, 16, 16, Color.Black), expectTriangles: true);

            Check(results, "FillCylinderEx", batch, camera, () =>
                batch.FillCylinderEx(new Vector3(0, -2, 0), new Vector3(0, 2, 0), 1.5f, 1.5f, 20, Color.Blue), expectTriangles: true);

            Check(results, "BorderCylinderEx", batch, camera, () =>
                batch.BorderCylinderEx(new Vector3(0, -2, 0), new Vector3(0, 2, 0), 1.5f, 1.5f, 20, Color.Black), expectTriangles: true);

            Check(results, "FillCapsule", batch, camera, () =>
                batch.FillCapsule(new Vector3(0, -2, 0), new Vector3(0, 2, 0), 1f, 16, 8, Color.Green), expectTriangles: true);

            Check(results, "FillCapsule (degenerate axis -> sphere fallback)", batch, camera, () =>
                batch.FillCapsule(Vector3.Zero, Vector3.Zero, 1f, 16, 8, Color.Green), expectTriangles: true);

            Check(results, "BorderCapsule", batch, camera, () =>
                batch.BorderCapsule(new Vector3(0, -2, 0), new Vector3(0, 2, 0), 1f, 16, 8, Color.Black), expectTriangles: true);

            Check(results, "FillTorus", batch, camera, () =>
                batch.FillTorus(Vector3.Zero, 3f, 1f, 16, 24, Color.Purple), expectTriangles: true);

            Check(results, "BorderTorus", batch, camera, () =>
                batch.BorderTorus(Vector3.Zero, 3f, 1f, 16, 24, Color.Black), expectTriangles: true);

            Check(results, "FillPlane", batch, camera, () =>
                batch.FillPlane(Vector3.Zero, new Vector2(4f, 4f), Color.Gray), expectTriangles: true);

            Check(results, "BorderPlane", batch, camera, () =>
                batch.BorderPlane(Vector3.Zero, new Vector2(4f, 4f), Color.Black), expectTriangles: true);

            Check(results, "FillCircle3D", batch, camera, () =>
                batch.FillCircle3D(Vector3.Zero, 2f, Vector3.UnitX, 0f, Color.Orange), expectTriangles: true);

            Check(results, "BorderCircle3D", batch, camera, () =>
                batch.BorderCircle3D(Vector3.Zero, 2f, Vector3.UnitX, 0f, Color.Black), expectTriangles: true);

            Check(results, "FillTriangle3D", batch, camera, () =>
                batch.FillTriangle3D(new Vector3(-1, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 2, 0), Color.Yellow), expectTriangles: true);

            Check(results, "FillBoundingBox", batch, camera, () =>
                batch.FillBoundingBox(new BoundingBox(-Vector3.One, Vector3.One), Color.Red), expectTriangles: true);

            Check(results, "FillHeightmap", batch, camera, () =>
                batch.FillHeightmap(MakeHeights(), Vector3.Zero, new Vector2(1f, 1f), Color.SaddleBrown), expectTriangles: true);

            Check(results, "FillCubes (batch positions)", batch, camera, () =>
                batch.FillCubes(new[] { Vector3.Zero, Vector3.One * 3f }, Vector3.One, Color.Red), expectTriangles: true);

            Check(results, "FillSpheres (batch positions)", batch, camera, () =>
                batch.FillSpheres(new[] { Vector3.Zero, Vector3.One * 3f }, 1f, Color.Red), expectTriangles: true);

            Check(results, "DrawLine3D", batch, camera, () =>
                batch.DrawLine3D(Vector3.Zero, Vector3.One * 5f, Color.Black), expectTriangles: true);

            Check(results, "DrawLine3DFast", batch, camera, () =>
                batch.DrawLine3DFast(Vector3.Zero, Vector3.One * 5f, Color.Black), expectLines: true);

            Check(results, "DrawPoint3D", batch, camera, () =>
                batch.DrawPoint3D(Vector3.Zero, Color.Black), expectTriangles: true);

            Check(results, "DrawArrow3D", batch, camera, () =>
                batch.DrawArrow3D(Vector3.Zero, new Vector3(0, 5, 0), 0.3f, 8, Color.Black), expectTriangles: true);

            Check(results, "DrawSplineCatmullRom3D", batch, camera, () =>
                batch.DrawSplineCatmullRom3D(
                    new[] { new Vector3(-2, 0, 0), new Vector3(-1, 1, 0), new Vector3(1, -1, 0), new Vector3(2, 0, 0) },
                    Color.Black), expectTriangles: true);

            Check(results, "DrawSplineBezierCubic3D", batch, camera, () =>
                batch.DrawSplineBezierCubic3D(
                    new[] { new Vector3(-2, 0, 0), new Vector3(-1, 2, 0), new Vector3(1, -2, 0), new Vector3(2, 0, 0) },
                    Color.Black), expectTriangles: true);

            Check(results, "DrawGridXZ (minor+major lines)", batch, camera, () =>
                batch.DrawGridXZ(25, 1f), expectLines: true, expectTriangles: true);

            Check(results, "DrawGridXY (minor+major lines)", batch, camera, () =>
                batch.DrawGridXY(25, 1f), expectLines: true, expectTriangles: true);

            Check(results, "DrawGridYZ (minor+major lines)", batch, camera, () =>
                batch.DrawGridYZ(25, 1f), expectLines: true, expectTriangles: true);

            Check(results, "DrawAxis (grid-style triad, no colored gizmo)", batch, camera, () =>
                batch.DrawAxis(10f), expectLines: true);

            Check(results, "DrawAxes (RGB gizmo, unaffected by DrawAxis split)", batch, camera, () =>
                batch.DrawAxes(Vector3.Zero, 5f), expectTriangles: true);

            LightingRegressionTests.Run(device, results);
        }

        private static float[,] MakeHeights()
        {
            var heights = new float[4, 4];
            for (int x = 0; x < 4; x++)
                for (int z = 0; z < 4; z++)
                    heights[x, z] = (x + z) * 0.5f;
            return heights;
        }

        private static void Check(
            TestResults results, string name, Primitive3DBatch batch, Camera3D camera, Action draw,
            bool expectTriangles = false, bool expectLines = false)
            => results.Check(name, () =>
            {
                batch.Begin(camera);
                draw();
                batch.End();

                if (expectTriangles && batch.TrianglesSubmitted == 0)
                    return "expected triangles but TrianglesSubmitted was 0";
                if (expectLines && batch.LinesSubmitted == 0)
                    return "expected lines but LinesSubmitted was 0";
                return null;
            });
    }
}
