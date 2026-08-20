using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace MonogameLibs;

/// <summary>
/// Visual regression gallery for every 2D shape method: one row per shape family (headed with
/// a <see cref="DebugFont5x7"/> label), one cell per Fill/Border/Draw variant — including every
/// LineJoin/jointRadius combination and every gradient variant. Not part of the library; a
/// sample-only tool for eyeballing a change across the whole 2D surface at once. Content grows
/// left-to-right/top-to-bottom from (0,0); pan/zoom with the camera instead of trying to fit it
/// on screen.
/// </summary>
internal static class Gallery2D
{
    private const float CellSize = 170f;
    private const float CellGap = 26f;
    private const float RowHeaderHeight = 34f;
    private const float RowGap = 46f;
    private const float CaptionGap = 8f;
    private const float LabelPixelSize = 3f;
    private const float CaptionPixelSize = 1f;

    private static readonly Color FillColor = Palette.PeterRiver;
    private static readonly Color BorderColor = Palette.Clouds;
    private static readonly Color AltFillColor = Palette.Alizarin;
    private static readonly Color GradientFrom = Palette.Turquoise;
    private static readonly Color GradientTo = Palette.Wisteria;
    private static readonly Color GradientFrom2 = Palette.Sunflower;
    private static readonly Color GradientTo2 = Palette.Pomegranate;

    private readonly record struct Cell(string Label, Action<PrimitiveBatch, Vector2> Draw);

    /// <summary>Draws the whole gallery starting at the origin and returns its total content size (for camera bounds).</summary>
    public static Vector2 Draw(PrimitiveBatch batch)
    {
        float y = 20f;
        float maxRight = 0f;

        DrawRow(batch, "TRIANGLE", TriangleCells(), ref y, ref maxRight);
        DrawRow(batch, "RECTANGLE", RectangleCells(), ref y, ref maxRight);
        DrawRow(batch, "CIRCLE", CircleCells(), ref y, ref maxRight);
        DrawRow(batch, "ELLIPSE", EllipseCells(), ref y, ref maxRight);
        DrawRow(batch, "CIRCLE SECTOR / RING", SectorRingCells(), ref y, ref maxRight);
        DrawRow(batch, "REGULAR POLYGON (POLY)", PolyCells(), ref y, ref maxRight);
        DrawRow(batch, "ARBITRARY POLYGON", PolygonCells(), ref y, ref maxRight);
        DrawRow(batch, "LINES / POINTS", LineCells(), ref y, ref maxRight);
        DrawRow(batch, "SPLINES / ARROW / FAN / STRIP", SplineCells(), ref y, ref maxRight);

        return new Vector2(maxRight + 30f, y + 30f);
    }

    private static void DrawRow(PrimitiveBatch batch, string title, IReadOnlyList<Cell> cells, ref float y, ref float maxRight)
    {
        batch.DrawString(title, new Vector2(30f, y), LabelPixelSize, Palette.Clouds);
        y += RowHeaderHeight;

        float x = 30f;
        foreach (Cell cell in cells)
        {
            Vector2 center = new(x + CellSize * 0.5f, y + CellSize * 0.5f);
            cell.Draw(batch, center);

            Vector2 capSize = DebugFont5x7.MeasureText(cell.Label, CaptionPixelSize);
            float capX = x + MathF.Max(0f, (CellSize - capSize.X) * 0.5f);
            batch.DrawString(cell.Label, new Vector2(capX, y + CellSize + CaptionGap), CaptionPixelSize, Palette.Silver);

            x += CellSize + CellGap;
        }

        maxRight = MathF.Max(maxRight, x);
        y += CellSize + CaptionGap + DebugFont5x7.MeasureText("X", CaptionPixelSize).Y + RowGap;
    }

    // ==================================================================
    // TRIANGLE
    // ==================================================================

    private static List<Cell> TriangleCells()
    {
        var cells = new List<Cell>();
        static (Vector2 v1, Vector2 v2, Vector2 v3) Tri(Vector2 c) => (c + new Vector2(-55, 50), c + new Vector2(55, 50), c + new Vector2(0, -55));

        cells.Add(new Cell("Fill", (b, p) => { var (v1, v2, v3) = Tri(p); b.FillTriangle(v1, v2, v3, FillColor); }));
        cells.Add(new Cell("Fill rounded r=0", (b, p) => { var (v1, v2, v3) = Tri(p); b.FillTriangleRounded(v1, v2, v3, 0f, FillColor); }));
        cells.Add(new Cell("Fill rounded r=20", (b, p) => { var (v1, v2, v3) = Tri(p); b.FillTriangleRounded(v1, v2, v3, 20f, FillColor); }));

        foreach (var (suffix, join, radius) in JoinAxis())
            cells.Add(new Cell($"Border {suffix}", (b, p) => { var (v1, v2, v3) = Tri(p); b.BorderTriangle(v1, v2, v3, BorderColor, 4f, 0f, null, join, radius); }));

        cells.Add(new Cell("Border rounded r=0", (b, p) => { var (v1, v2, v3) = Tri(p); b.BorderTriangleRounded(v1, v2, v3, 0f, BorderColor, 4f); }));
        cells.Add(new Cell("Border rounded r=20", (b, p) => { var (v1, v2, v3) = Tri(p); b.BorderTriangleRounded(v1, v2, v3, 20f, BorderColor, 4f); }));

        cells.Add(new Cell("Draw", (b, p) => { var (v1, v2, v3) = Tri(p); b.DrawTriangle(v1, v2, v3, FillColor, BorderColor, 4f); }));
        cells.Add(new Cell("Draw rounded r=20", (b, p) => { var (v1, v2, v3) = Tri(p); b.DrawTriangleRounded(v1, v2, v3, 20f, FillColor, BorderColor, 4f); }));

        cells.Add(new Cell("Gradient", (b, p) => { var (v1, v2, v3) = Tri(p); b.FillTriangleGradient(v1, v2, v3, GradientFrom, GradientTo); }));
        cells.Add(new Cell("Gradient rounded r=20", (b, p) => { var (v1, v2, v3) = Tri(p); b.FillTriangleGradientRounded(v1, v2, v3, 20f, GradientFrom, GradientTo); }));
        cells.Add(new Cell("Draw gradient", (b, p) => { var (v1, v2, v3) = Tri(p); b.DrawTriangleGradient(v1, v2, v3, GradientFrom2, GradientTo2, BorderColor, 4f); }));
        cells.Add(new Cell("Draw gradient rounded", (b, p) => { var (v1, v2, v3) = Tri(p); b.DrawTriangleGradientRounded(v1, v2, v3, 20f, GradientFrom2, GradientTo2, BorderColor, 4f); }));

        return cells;
    }

    // ==================================================================
    // RECTANGLE
    // ==================================================================

    private static List<Cell> RectangleCells()
    {
        var cells = new List<Cell>();
        static Vector2 TopLeft(Vector2 c) => c - new Vector2(65, 45);
        Vector2 size = new(130, 90);

        cells.Add(new Cell("Fill", (b, p) => b.FillRectangle(TopLeft(p), size, FillColor)));
        cells.Add(new Cell("Fill rounded r=0", (b, p) => b.FillRectangleRounded(TopLeft(p), size, 0f, FillColor)));
        cells.Add(new Cell("Fill rounded r=20", (b, p) => b.FillRectangleRounded(TopLeft(p), size, 20f, FillColor)));
        cells.Add(new Cell("Fill rounded per-corner", (b, p) => b.FillRectangleRounded(TopLeft(p), size, new RectCorners(0f, 12f, 24f, 36f), FillColor)));
        cells.Add(new Cell("Fill chamfer r=20", (b, p) => b.FillRectangleChamfer(TopLeft(p), size, 20f, FillColor)));

        foreach (var (suffix, join, radius) in JoinAxis())
            cells.Add(new Cell($"Border {suffix}", (b, p) => b.BorderRectangle(TopLeft(p), size, BorderColor, 4f, 0f, null, join, radius)));

        cells.Add(new Cell("Border rounded r=20", (b, p) => b.BorderRectangleRounded(TopLeft(p), size, 20f, BorderColor, 4f)));
        cells.Add(new Cell("Border chamfer r=20", (b, p) => b.BorderRectangleChamfer(TopLeft(p), size, 20f, BorderColor, 4f)));

        cells.Add(new Cell("Draw", (b, p) => b.DrawRectangle(TopLeft(p), size, FillColor, BorderColor, 4f)));
        cells.Add(new Cell("Draw rounded r=20", (b, p) => b.DrawRectangleRounded(TopLeft(p), size, 20f, FillColor, BorderColor, 4f)));
        cells.Add(new Cell("Draw chamfer r=20", (b, p) => b.DrawRectangleChamfer(TopLeft(p), size, 20f, FillColor, BorderColor, 4f)));

        cells.Add(new Cell("Gradient horizontal", (b, p) => b.FillRectangleGradient(TopLeft(p), size, GradientFrom, GradientTo, horizontal: true)));
        cells.Add(new Cell("Gradient vertical", (b, p) => b.FillRectangleGradient(TopLeft(p), size, GradientFrom, GradientTo, horizontal: false)));
        cells.Add(new Cell("Gradient offset", (b, p) => b.FillRectangleGradient(TopLeft(p), size, GradientFrom, GradientTo, horizontal: true, rotation: 0f, origin: null, innerOffset: 0.2f, outerOffset: 0.2f)));
        cells.Add(new Cell("Gradient rounded", (b, p) => b.FillRectangleRoundedGradient(TopLeft(p), size, 20f, GradientFrom2, GradientTo2, horizontal: true)));
        cells.Add(new Cell("Gradient chamfer", (b, p) => b.FillRectangleChamferGradient(TopLeft(p), size, 20f, GradientFrom2, GradientTo2, horizontal: false)));
        cells.Add(new Cell("Draw gradient", (b, p) => b.DrawRectangleGradient(TopLeft(p), size, GradientFrom, GradientTo, horizontal: true, BorderColor, 4f)));
        cells.Add(new Cell("Draw gradient rounded", (b, p) => b.DrawRectangleRoundedGradient(TopLeft(p), size, 20f, GradientFrom2, GradientTo2, horizontal: true, BorderColor, 4f)));
        cells.Add(new Cell("Draw gradient chamfer", (b, p) => b.DrawRectangleChamferGradient(TopLeft(p), size, 20f, GradientFrom2, GradientTo2, horizontal: false, BorderColor, 4f)));

        return cells;
    }

    // ==================================================================
    // CIRCLE
    // ==================================================================

    private static List<Cell> CircleCells()
    {
        var cells = new List<Cell>
        {
            new("Fill", (b, p) => b.FillCircle(p, 60f, FillColor)),
            new("Fill segments=8", (b, p) => b.FillCircle(p, 60f, 8, FillColor)),
            new("Border thin", (b, p) => b.BorderCircle(p, 60f, BorderColor, 1f)),
            new("Border thick", (b, p) => b.BorderCircle(p, 60f, BorderColor, 6f)),
            new("Draw", (b, p) => b.DrawCircle(p, 60f, FillColor, BorderColor, 4f)),
            new("Gradient", (b, p) => b.FillCircleGradient(p, 60f, GradientFrom, GradientTo)),
            new("Gradient offset", (b, p) => b.FillCircleGradient(p, 60f, GradientFrom, GradientTo, innerOffset: 0.15f, outerOffset: 0.15f)),
            new("Gradient linear H", (b, p) => b.FillCircleGradientLinear(p, 60f, GradientFrom2, GradientTo2, horizontal: true)),
            new("Gradient linear V", (b, p) => b.FillCircleGradientLinear(p, 60f, GradientFrom2, GradientTo2, horizontal: false)),
            new("Draw gradient", (b, p) => b.DrawCircleGradient(p, 60f, GradientFrom, GradientTo, BorderColor, 4f)),
            new("Draw gradient linear", (b, p) => b.DrawCircleGradientLinear(p, 60f, GradientFrom2, GradientTo2, BorderColor, horizontal: true, thickness: 4f)),
        };
        return cells;
    }

    // ==================================================================
    // ELLIPSE
    // ==================================================================

    private static List<Cell> EllipseCells()
    {
        var cells = new List<Cell>
        {
            new("Fill", (b, p) => b.FillEllipse(p, 70f, 45f, FillColor)),
            new("Fill rotated", (b, p) => b.FillEllipse(p, 70f, 45f, FillColor, rotation: MathHelper.ToRadians(30f))),
            new("Border", (b, p) => b.BorderEllipse(p, 70f, 45f, BorderColor, 4f)),
            new("Draw", (b, p) => b.DrawEllipse(p, 70f, 45f, FillColor, BorderColor, 4f)),
            new("Gradient", (b, p) => b.FillEllipseGradient(p, 70f, 45f, GradientFrom, GradientTo)),
            new("Gradient offset", (b, p) => b.FillEllipseGradient(p, 70f, 45f, GradientFrom2, GradientTo2, innerOffset: 0.2f, outerOffset: 0.2f)),
            new("Draw gradient", (b, p) => b.DrawEllipseGradient(p, 70f, 45f, GradientFrom, GradientTo, BorderColor, 4f)),
        };
        return cells;
    }

    // ==================================================================
    // CIRCLE SECTOR / RING
    // ==================================================================

    private static List<Cell> SectorRingCells()
    {
        var cells = new List<Cell>
        {
            new("Sector 1/4", (b, p) => b.DrawCircleSector(p, 60f, 0f, 0.25f, FillColor)),
            new("Sector 3/4", (b, p) => b.DrawCircleSector(p, 60f, 0f, 0.75f, FillColor)),
            new("Sector lines", (b, p) => b.DrawCircleSectorLines(p, 60f, 0f, 0.6f, 4f, BorderColor)),
            new("Ring full", (b, p) => b.DrawRing(p, 30f, 60f, FillColor)),
            new("Ring partial 1/2", (b, p) => b.DrawRing(p, 30f, 60f, 0f, 0.5f, 32, FillColor)),
            new("Border ring thin", (b, p) => b.BorderRing(p, 30f, 60f, BorderColor, 1f)),
            new("Border ring thick", (b, p) => b.BorderRing(p, 30f, 60f, BorderColor, 6f)),
            new("Border ring partial", (b, p) => b.BorderRing(p, 30f, 60f, 0f, 0.5f, 32, BorderColor, 4f)),
        };
        return cells;
    }

    // ==================================================================
    // REGULAR POLYGON (POLY)
    // ==================================================================

    private static List<Cell> PolyCells()
    {
        var cells = new List<Cell>
        {
            new("Fill sides=3", (b, p) => b.FillPoly(p, 3, 60f, FillColor)),
            new("Fill sides=5", (b, p) => b.FillPoly(p, 5, 60f, FillColor)),
            new("Fill sides=8", (b, p) => b.FillPoly(p, 8, 60f, FillColor)),
        };

        foreach (var (suffix, join, radius) in JoinAxis())
            cells.Add(new Cell($"Border {suffix}", (b, p) => b.BorderPoly(p, 6, 60f, BorderColor, 4f, 0f, join, radius)));

        cells.Add(new Cell("Draw", (b, p) => b.DrawPoly(p, 6, 60f, FillColor, BorderColor, 4f)));
        cells.Add(new Cell("Gradient", (b, p) => b.FillPolyGradient(p, 6, 60f, GradientFrom, GradientTo)));
        cells.Add(new Cell("Gradient offset", (b, p) => b.FillPolyGradient(p, 6, 60f, GradientFrom2, GradientTo2, innerOffset: 0.2f, outerOffset: 0.2f)));
        cells.Add(new Cell("Draw gradient", (b, p) => b.DrawPolyGradient(p, 6, 60f, GradientFrom, GradientTo, BorderColor, 4f)));

        return cells;
    }

    // ==================================================================
    // ARBITRARY POLYGON
    // ==================================================================
    // A simple L-shape (one reflex vertex) — concave enough to exercise the rounded-corner/
    // inset code non-trivially. FillPolygon now ear-clips automatically for non-convex input
    // (see DECISIONS.md), so any vertex order works here; a 5-point star or any other simple
    // concave shape would render correctly too.
    private static Vector2[] LShape(Vector2 c) => new[]
    {
        c + new Vector2(-70, -70), // 0: opposite the notch — the fan/inset origin
        c + new Vector2(70, -70),
        c + new Vector2(70, 0),
        c + new Vector2(0, 0),     // reflex vertex (the notch's inner corner)
        c + new Vector2(0, 70),
        c + new Vector2(-70, 70),
    };

    private static List<Cell> PolygonCells()
    {
        var cells = new List<Cell>
        {
            new("Fill", (b, p) => b.FillPolygon(LShape(p), FillColor)),
        };

        // Thicker than the rest of this row's cells: BorderPolygon grows inward, so its
        // round/bevel jointRadius is clamped down to whatever fits without self-intersecting
        // (verified against ComputeJoint's own math) — a thin border on a sharp corner clamps
        // to just a couple of visible pixels, correctly but invisibly.
        foreach (var (suffix, join, radius) in JoinAxis())
            cells.Add(new Cell($"Border {suffix}", (b, p) => b.BorderPolygon(LShape(p), BorderColor, 10f, join, radius)));

        cells.Add(new Cell("Draw", (b, p) => b.DrawPolygon(LShape(p), FillColor, BorderColor, 4f)));
        cells.Add(new Cell("Fill rounded r=6", (b, p) => b.FillPolygonRounded(LShape(p), 6f, FillColor)));
        cells.Add(new Cell("Border rounded r=6", (b, p) => b.BorderPolygonRounded(LShape(p), 6f, BorderColor, 4f)));
        cells.Add(new Cell("Draw rounded r=6", (b, p) => b.DrawPolygonRounded(LShape(p), 6f, FillColor, BorderColor, 4f)));
        cells.Add(new Cell("Gradient", (b, p) => b.FillPolygonGradient(LShape(p), GradientFrom, GradientTo)));
        cells.Add(new Cell("Gradient rounded r=6", (b, p) => b.FillPolygonGradientRounded(LShape(p), 6f, GradientFrom2, GradientTo2)));
        cells.Add(new Cell("Gradient top/bottom", (b, p) =>
        {
            Vector2[] bottom = { p + new Vector2(-60, 50), p + new Vector2(60, 50) };
            Vector2[] top = { p + new Vector2(-60, -50), p + new Vector2(60, -50) };
            b.FillPolygonGradientTopBottom(bottom, top, GradientFrom, GradientTo);
        }));
        cells.Add(new Cell("Draw gradient", (b, p) => b.DrawPolygonGradient(LShape(p), GradientFrom, GradientTo, BorderColor, 4f)));

        // A 5-point star: alternating outer/inner vertices, so no single points[0] choice makes
        // the naive fan-from-points[0] correct (see FillPolygon's ear-clipping fallback in
        // DECISIONS.md) — this is the actual regression case that motivated it.
        cells.Add(new Cell("Fill star (ear-clipped)", (b, p) => b.FillPolygon(Star(p), FillColor)));
        cells.Add(new Cell("Draw star", (b, p) => b.DrawPolygon(Star(p), FillColor, BorderColor, 4f)));
        cells.Add(new Cell("Gradient star", (b, p) => b.FillPolygonGradient(Star(p), GradientFrom, GradientTo)));
        cells.Add(new Cell("Gradient star round", (b, p) => b.DrawPolygonRounded(Star(p), 10f, GradientTo, GradientFrom, 5)));

        return cells;
    }

    private static Vector2[] Star(Vector2 c)
    {
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float angle = MathHelper.PiOver2 + i * MathF.PI / 5f;
            float r = (i % 2 == 0) ? 65f : 28f;
            pts[i] = c + new Vector2(MathF.Cos(angle), -MathF.Sin(angle)) * r;
        }
        return pts;
    }

    // ==================================================================
    // LINES / POINTS
    // ==================================================================

    private static List<Cell> LineCells()
    {
        var cells = new List<Cell>
        {
            new("Line thin", (b, p) => b.DrawLine(p + new Vector2(-60, -50), p + new Vector2(60, 50), 1f, FillColor)),
            new("Line thick", (b, p) => b.DrawLine(p + new Vector2(-60, -50), p + new Vector2(60, 50), 10f, FillColor)),
            new("Line cap round", (b, p) => b.DrawLine(p + new Vector2(-60, -50), p + new Vector2(60, 50), 10f, FillColor, LineCap.Round)),
            new("Line dashed", (b, p) => b.DrawLineDashed(p + new Vector2(-60, 0), p + new Vector2(60, 0), 10f, 6f, 4f, FillColor)),
            // Thicker than the other Line cells on purpose: the miter/round/bevel difference
            // is a fixed number of pixels regardless of stroke width (bounded by geometry, not
            // thickness), so a thin stroke makes it hard to see at all — verified against
            // DrawLineStrip's own ComputeJoint math (16px here reads clearly; 8px was marginal).
            new("Line strip miter", (b, p) => b.DrawLineStrip(ZigZag(p), 16f, FillColor, LineJoin.Miter)),
            new("Line strip round", (b, p) => b.DrawLineStrip(ZigZag(p), 16f, FillColor, LineJoin.Round, LineCap.Round)),
            new("Line strip bevel r=20", (b, p) => b.DrawLineStrip(ZigZag(p), 16f, FillColor, LineJoin.Bevel, LineCap.Butt, 20f)),
            new("Pixel", (b, p) => b.DrawPixel(p, BorderColor)),
            new("Point", (b, p) => b.DrawPoint(p, 12f, BorderColor)),
        };
        return cells;
    }

    private static Vector2[] ZigZag(Vector2 c) => new[]
    {
        c + new Vector2(-65, 40), c + new Vector2(-25, -50), c + new Vector2(15, 40), c + new Vector2(65, -50)
    };

    // ==================================================================
    // SPLINES / ARROW / FAN / STRIP
    // ==================================================================

    private static List<Cell> SplineCells()
    {
        var cells = new List<Cell>
        {
            new("Spline linear", (b, p) => b.DrawSplineLinear(ZigZag(p), 5f, FillColor)),
            new("Spline Catmull-Rom", (b, p) => b.DrawSplineCatmullRom(ZigZag(p), 5f, FillColor)),
            new("Spline Bezier cubic", (b, p) => b.DrawSplineBezierCubic(FourPointBezier(p), 5f, FillColor)),
            new("Arrow default", (b, p) => b.DrawArrow(p + new Vector2(-60, 0), p + new Vector2(60, 0), FillColor)),
            new("Arrow thick head", (b, p) => b.DrawArrow(p + new Vector2(-60, 0), p + new Vector2(60, 0), 6f, FillColor, headLength: 40f, headWidth: 40f)),
            new("Triangle fan", (b, p) => b.DrawTriangleFan(Fan(p), FillColor)),
            new("Triangle strip", (b, p) => b.DrawTriangleStrip(Strip(p), FillColor)),
        };
        return cells;
    }

    private static Vector2[] FourPointBezier(Vector2 c) => new[]
    {
        c + new Vector2(-65, 40), c + new Vector2(-25, -60), c + new Vector2(25, 60), c + new Vector2(65, -40)
    };

    private static Vector2[] Fan(Vector2 c)
    {
        var pts = new Vector2[8];
        pts[0] = c;
        for (int i = 1; i < 8; i++)
        {
            float angle = (i - 1) / 6f * MathHelper.TwoPi;
            pts[i] = c + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 60f;
        }
        return pts;
    }

    private static Vector2[] Strip(Vector2 c) => new[]
    {
        c + new Vector2(-65, -50), c + new Vector2(-65, 50), c + new Vector2(-20, -50),
        c + new Vector2(-20, 50), c + new Vector2(25, -50), c + new Vector2(25, 50),
        c + new Vector2(65, -50), c + new Vector2(65, 50),
    };

    // Shared join/jointRadius axis for every Border*/Draw* overload that supports it.
    private static (string Suffix, LineJoin Join, float? Radius)[] JoinAxis() => new (string, LineJoin, float?)[]
    {
        ("miter", LineJoin.Miter, null),
        ("round r=0", LineJoin.Round, 0f),
        ("round r=20", LineJoin.Round, 20f),
        ("bevel r=0", LineJoin.Bevel, 0f),
        ("bevel r=20", LineJoin.Bevel, 20f),
    };
}
