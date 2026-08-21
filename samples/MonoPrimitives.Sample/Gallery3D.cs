using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

using MonoPrimitives;
using MonoPrimitives.Primitives3D;

namespace MonogameLibs;

/// <summary>
/// Visual regression gallery for every 3D shape method: one row per shape family (headed with a
/// billboarded <see cref="Primitive3DBatch.DrawString3D"/> label), one cell per Fill/Border/Draw
/// variant — the same row/cell/caption structure as the 2D library's own <see cref="Gallery2D"/>.
/// Not part of the library; a sample-only tool for eyeballing a change across the whole 3D
/// surface at once, the same way Gallery2D does for 2D. Content grows along +X (cells) and +Z
/// (rows) starting near the origin — fly the free camera to see it all.
/// </summary>
internal static class Gallery3D
{
    private const float CellSize = 6f;
    private const float RowGap = 3f;
    private const float CaptionPixelSize = 0.05f;
    private const float LabelPixelSize = 0.12f;

    private static readonly Color FillColor = Palette.PeterRiver;
    private static readonly Color BorderColor = Palette.Amethyst;
    private static readonly Color AltFillColor = Palette.Alizarin;

    private readonly record struct Cell(string Label, Action<Primitive3DBatch, Vector3> Draw);

    /// <summary>Draws the whole gallery starting at the origin.</summary>
    public static void Draw(Primitive3DBatch batch)
    {
        float z = 0f;

        DrawRow(batch, "CUBE", CubeCells(), ref z);
        DrawRow(batch, "SPHERE", SphereCells(), ref z);
        DrawRow(batch, "CYLINDER", CylinderCells(), ref z);
        DrawRow(batch, "CONE", ConeCells(), ref z);
        DrawRow(batch, "CAPSULE", CapsuleCells(), ref z);
        DrawRow(batch, "TORUS", TorusCells(), ref z);
        DrawRow(batch, "BOUNDING BOX", BoundingBoxCells(), ref z);
        DrawRow(batch, "PLANE", PlaneCells(), ref z);
        DrawRow(batch, "CIRCLE3D", Circle3DCells(), ref z);
        DrawRow(batch, "TRIANGLE3D", Triangle3DCells(), ref z);
        DrawRow(batch, "HEIGHTMAP", HeightmapCells(), ref z);
        DrawRow(batch, "LINES / POINTS / ARROW", LineCells(), ref z);
        DrawRow(batch, "SPLINES", SplineCells(), ref z);
        DrawRow(batch, "TEXT: BILLBOARDED VS FIXED", TextCells(), ref z);
    }

    // Dark text on purpose: the 3D sample clears to white (unlike the 2D gallery's dark
    // Palette.Background), so the light Palette.Clouds/Silver captions Gallery2D uses would be
    // unreadable here.
    private static readonly Color LabelTextColor = Palette.MidnightBlue;
    private static readonly Color CaptionTextColor = Palette.WetAsphalt;

    private static void DrawRow(Primitive3DBatch batch, string title, IReadOnlyList<Cell> cells, ref float z)
    {
        batch.DrawString3D(title, new Vector3(0f, 4.5f, z), LabelPixelSize, LabelTextColor);

        float x = 0f;
        foreach (Cell cell in cells)
        {
            Vector3 center = new(x, 0f, z);
            cell.Draw(batch, center);
            DrawCenteredCaption(batch, cell.Label, center + new Vector3(0f, -0.7f, 0f));
            x += CellSize;
        }

        z += CellSize + RowGap;
    }

    // Captions are billboarded, so their own "right" axis rotates with the camera — center them
    // by measuring the text and offsetting by half its width along that same right axis, instead
    // of a fixed world-space X offset that would only look centered from one viewing angle.
    private static void DrawCenteredCaption(Primitive3DBatch batch, string text, Vector3 anchor)
    {
        batch.GetBillboardAxes(anchor, out Vector3 right, out _);
        Vector2 size = Primitive3DBatch.MeasureText3D(text, CaptionPixelSize);
        batch.DrawString3D(text, anchor - right * (size.X * 0.5f), CaptionPixelSize, CaptionTextColor);
    }

    // ==================================================================
    // CUBE
    // ==================================================================

    private static List<Cell> CubeCells() => new()
    {
        new("Fill", (b, p) => b.FillCube(p + Vector3.UnitY, Vector3.One * 2f, FillColor)),
        new("Border", (b, p) => b.BorderCube(p + Vector3.UnitY, Vector3.One * 2f, BorderColor)),
        new("Draw", (b, p) => b.DrawCube(p + Vector3.UnitY, Vector3.One * 2f, FillColor, BorderColor)),
    };

    // ==================================================================
    // SPHERE
    // ==================================================================

    private static List<Cell> SphereCells() => new()
    {
        new("Fill", (b, p) => b.FillSphere(p + Vector3.UnitY * 1.5f, 1.5f, FillColor)),
        new("Border", (b, p) => b.BorderSphere(p + Vector3.UnitY * 1.5f, 1.5f, BorderColor)),
        new("Draw", (b, p) => b.DrawSphere(p + Vector3.UnitY * 1.5f, 1.5f, FillColor, BorderColor)),
    };

    // ==================================================================
    // CYLINDER
    // ==================================================================

    private static List<Cell> CylinderCells() => new()
    {
        new("Fill", (b, p) => b.FillCylinder(p, 1.2f, 1.2f, 3f, 24, FillColor)),
        new("Border", (b, p) => b.BorderCylinder(p, 1.2f, 1.2f, 3f, 24, BorderColor)),
        new("Draw", (b, p) => b.DrawCylinder(p, 1.2f, 1.2f, 3f, 24, FillColor, BorderColor)),
    };

    // ==================================================================
    // CONE (a cylinder with radiusTop = 0 — same shape, no separate method)
    // ==================================================================

    private static List<Cell> ConeCells() => new()
    {
        new("Fill", (b, p) => b.FillCylinder(p, 0f, 1.2f, 3f, 24, FillColor)),
        new("Border", (b, p) => b.BorderCylinder(p, 0f, 1.2f, 3f, 24, BorderColor)),
        new("Draw", (b, p) => b.DrawCylinder(p, 0f, 1.2f, 3f, 24, FillColor, BorderColor)),
    };

    // ==================================================================
    // CAPSULE
    // ==================================================================

    private static List<Cell> CapsuleCells() => new()
    {
        new("Fill", (b, p) => b.FillCapsule(p + Vector3.UnitY, p + Vector3.UnitY * 3f, 1f, 24, 12, FillColor)),
        new("Border", (b, p) => b.BorderCapsule(p + Vector3.UnitY, p + Vector3.UnitY * 3f, 1f, 24, 12, BorderColor)),
        new("Draw", (b, p) => b.DrawCapsule(p + Vector3.UnitY, p + Vector3.UnitY * 3f, 1f, 24, 12, FillColor, BorderColor)),
    };

    // ==================================================================
    // TORUS
    // ==================================================================

    private static List<Cell> TorusCells() => new()
    {
        new("Fill", (b, p) => b.FillTorus(p + Vector3.UnitY * 1.5f, 1.3f, 0.5f, 24, 24, FillColor)),
        new("Border", (b, p) => b.BorderTorus(p + Vector3.UnitY * 1.5f, 1.3f, 0.5f, 24, 24, BorderColor)),
        new("Draw", (b, p) => b.DrawTorus(p + Vector3.UnitY * 1.5f, 1.3f, 0.5f, 24, 24, FillColor, BorderColor)),
    };

    // ==================================================================
    // BOUNDING BOX
    // ==================================================================

    private static List<Cell> BoundingBoxCells() => new()
    {
        new("Fill", (b, p) => b.FillBoundingBox(new BoundingBox(p, p + new Vector3(2f, 2f, 2f)), FillColor)),
        new("Border", (b, p) => b.BorderBoundingBox(new BoundingBox(p, p + new Vector3(2f, 2f, 2f)), BorderColor)),
        new("Draw", (b, p) => b.DrawBoundingBox(new BoundingBox(p, p + new Vector3(2f, 2f, 2f)), FillColor, BorderColor)),
    };

    // ==================================================================
    // PLANE
    // ==================================================================

    private static List<Cell> PlaneCells() => new()
    {
        new("Fill", (b, p) => b.FillPlane(p + Vector3.UnitY * 1.5f, new Vector2(2.5f, 2.5f), FillColor)),
        new("Border", (b, p) => b.BorderPlane(p + Vector3.UnitY * 1.5f, new Vector2(2.5f, 2.5f), BorderColor)),
        new("Draw", (b, p) => b.DrawPlane(p + Vector3.UnitY * 1.5f, new Vector2(2.5f, 2.5f), FillColor, BorderColor)),
    };

    // ==================================================================
    // CIRCLE3D
    // ==================================================================

    private static List<Cell> Circle3DCells() => new()
    {
        new("Fill", (b, p) => b.FillCircle3D(p + Vector3.UnitY * 1.5f, 1.5f, Vector3.UnitX, 90f, FillColor)),
        new("Border", (b, p) => b.BorderCircle3D(p + Vector3.UnitY * 1.5f, 1.5f, Vector3.UnitX, 90f, BorderColor)),
        new("Draw", (b, p) => b.DrawCircle3D(p + Vector3.UnitY * 1.5f, 1.5f, Vector3.UnitX, 90f, FillColor, BorderColor)),
    };

    // ==================================================================
    // TRIANGLE3D
    // ==================================================================

    private static List<Cell> Triangle3DCells() => new()
    {
        new("Fill", (b, p) => b.FillTriangle3D(p, p + new Vector3(2f, 0, 0), p + new Vector3(1f, 2.5f, 0), FillColor)),
        new("Border", (b, p) => b.BorderTriangle3D(p, p + new Vector3(2f, 0, 0), p + new Vector3(1f, 2.5f, 0), BorderColor)),
        new("Draw", (b, p) => b.DrawTriangle3D(p, p + new Vector3(2f, 0, 0), p + new Vector3(1f, 2.5f, 0), FillColor, BorderColor)),
    };

    // ==================================================================
    // HEIGHTMAP
    // ==================================================================

    private static readonly float[,] HeightmapPatch =
    {
        { 0f, 0.4f, 0.2f },
        { 0.3f, 0.9f, 0.1f },
        { 0f, 0.5f, 0f },
    };

    private static List<Cell> HeightmapCells() => new()
    {
        new("Fill", (b, p) => b.FillHeightmap(HeightmapPatch, p, new Vector2(1f, 1f), FillColor)),
        new("Border", (b, p) => b.BorderHeightmap(HeightmapPatch, p, new Vector2(1f, 1f), BorderColor)),
        new("Draw", (b, p) => b.DrawHeightmap(HeightmapPatch, p, new Vector2(1f, 1f), FillColor, BorderColor)),
    };

    // ==================================================================
    // LINES / POINTS / ARROW
    // ==================================================================
    // No Fill/Border split here — same as Gallery2D's own "LINES / POINTS" row, since a stroke
    // primitive has no interior to fill.

    private static List<Cell> LineCells() => new()
    {
        new("Line thin", (b, p) => b.DrawLine3D(p + new Vector3(-1.5f, 0.5f, 0), p + new Vector3(1.5f, 3f, 0), FillColor)),
        new("Line thick", (b, p) => b.DrawLine3D(p + new Vector3(-1.5f, 0.5f, 0), p + new Vector3(1.5f, 3f, 0), 6f, FillColor)),
        new("Line dashed", (b, p) => b.DrawLine3DDashed(p + new Vector3(-1.5f, 1.5f, 0), p + new Vector3(1.5f, 1.5f, 0), 0.4f, 0.2f, FillColor)),
        new("Point", (b, p) => b.DrawPoint3D(p + Vector3.UnitY * 1.5f, 0.3f, Color.Black)),
        new("Point cross", (b, p) => b.DrawPoint3DCross(p + Vector3.UnitY * 1.5f, 0.3f, Color.Black)),
        new("Arrow", (b, p) => b.DrawArrow(p, p + Vector3.UnitY * 3f, 0.3f, 8, Color.Black)),
    };

    // ==================================================================
    // SPLINES
    // ==================================================================

    private static List<Cell> SplineCells() => new()
    {
        new("Catmull-Rom", (b, p) => b.DrawSplineCatmullRom3D(
            new[] { p + new Vector3(-2, 0.5f, 0), p + new Vector3(-1, 2f, 0), p + new Vector3(1, -1f, 0), p + new Vector3(2, 0.5f, 0) },
            1f, FillColor)),
        new("Bezier cubic", (b, p) => b.DrawSplineBezierCubic3D(
            new[] { p + new Vector3(-2, 0.5f, 0), p + new Vector3(-1, 3f, 0), p + new Vector3(1, -2f, 0), p + new Vector3(2, 0.5f, 0) },
            1f, FillColor)),
        new("Bezier quadratic", (b, p) => b.DrawSplineBezierQuadratic3D(
            new[] { p + new Vector3(-2, 0.5f, 0), p + new Vector3(-1, 3f, 0), p + new Vector3(0, 0.5f, 0), p + new Vector3(1, -2f, 0), p + new Vector3(2, 0.5f, 0) },
            1f, FillColor)),
        new("Basis (B-spline)", (b, p) => b.DrawSplineBasis3D(
            new[] { p + new Vector3(-2, 0.5f, 0), p + new Vector3(-1, 3f, 0), p + new Vector3(1, -2f, 0), p + new Vector3(2, 0.5f, 0) },
            1f, FillColor)),
    };

    // ==================================================================
    // TEXT: BILLBOARDED VS FIXED
    // ==================================================================
    // DrawString3D(text, position, pixelSize, color) always faces the camera (billboarded) —
    // that's what every row label/caption in this gallery uses. The overload that also takes an
    // explicit right/up basis opts out: the text holds that orientation regardless of where the
    // camera moves, the same way any other quad in the scene would. Orbit the free camera around
    // this row to see the difference — "Billboarded" stays readable from every angle, the two
    // "Fixed" cells rotate out of view / foreshorten like a sign painted onto a surface would.

    private static List<Cell> TextCells() => new()
    {
        new("Billboarded", (b, p) => b.DrawString3D("HELLO", p + Vector3.UnitY * 1.5f, 0.18f, FillColor)),
        new("Fixed: facing +Z", (b, p) => b.DrawString3D("HELLO", p + Vector3.UnitY * 1.5f, Vector3.UnitX, Vector3.UnitY, 0.18f, AltFillColor)),
        new("Fixed: lying flat", (b, p) => b.DrawString3D("HELLO", p + Vector3.UnitY * 0.05f, Vector3.UnitX, Vector3.UnitZ, 0.18f, Palette.Nephritis)),
    };
}
