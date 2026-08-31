using System;
using Microsoft.Xna.Framework;

using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace MonogameLibs;

/// <summary>
/// A curated, presentation-quality shape showcase: a handful of shapes in bordered cards with a
/// centered label -- not Gallery2D's exhaustive per-row Fill/Border/Draw/LineJoin matrix. Not
/// part of the library; exists purely to produce README/guide screenshots that read clearly at a
/// glance instead of the dev gallery's dense grid. Content grows left-to-right/top-to-bottom from
/// the origin.
/// </summary>
internal static class Showcase2D
{
    private const float CardSize = 260f;
    private const float Gap = 36f;
    private const float LabelPixelSize = 3f;
    private const int Columns = 4;

    private readonly record struct Item(string Label, Color Accent, Action<Primitive2DBatch, Vector2> Draw);

    private static readonly Item[] Items =
    {
        new("TRIANGLE", Palette.Alizarin, (b, c) => b.FillTriangleRounded(c + new Vector2(-65, 55), c + new Vector2(65, 55), c + new Vector2(0, -65), 14f, Palette.Alizarin)),
        new("RECTANGLE", Palette.PeterRiver, (b, c) => b.FillRectangleRounded(c - new Vector2(75, 55), new Vector2(150, 110), 22f, Palette.PeterRiver)),
        new("CIRCLE", Palette.Sunflower, (b, c) => b.FillCircleGradient(c, 70f, Palette.Sunflower, Palette.Carrot)),
        new("ELLIPSE", Palette.Amethyst, (b, c) => b.FillEllipse(c, 80f, 52f, Palette.Amethyst)),
        new("CAPSULE", Palette.Emerald, (b, c) => b.FillCapsule(c, 130f, 40f, Palette.Emerald, MathF.PI / 2f)),
        new("HEXAGON", Palette.Turquoise, (b, c) => b.FillPoly(c, 6, 72f, Palette.Turquoise)),
        new("STAR", Palette.Carrot, (b, c) => b.FillPolygon(StarPoints(c, 72f, 30f, 5), Palette.Carrot)),
        new("RING", Palette.Wisteria, (b, c) => b.FillRing(c, 42f, 70f, Palette.Wisteria)),
    };

    /// <summary>Total content size, without drawing anything -- for framing a camera before the first <see cref="Draw"/> call.</summary>
    public static Vector2 GetContentSize()
    {
        int rows = (Items.Length + Columns - 1) / Columns;
        return new Vector2(Columns * CardSize + (Columns - 1) * Gap, rows * CardSize + (rows - 1) * Gap);
    }

    /// <summary>Draws the curated card grid starting at the origin and returns its total content size (for camera framing).</summary>
    public static Vector2 Draw(Primitive2DBatch batch)
    {
        for (int i = 0; i < Items.Length; i++)
        {
            int col = i % Columns, row = i / Columns;
            Vector2 cardTopLeft = new(col * (CardSize + Gap), row * (CardSize + Gap));
            Vector2 shapeCenter = cardTopLeft + new Vector2(CardSize * 0.5f, CardSize * 0.42f);

            batch.BorderRectangleRounded(cardTopLeft, new Vector2(CardSize, CardSize), 24f, Items[i].Accent, thickness: 3f);
            Items[i].Draw(batch, shapeCenter);

            Vector2 labelSize = DebugFont5x7.MeasureText(Items[i].Label, LabelPixelSize);
            Vector2 labelPos = cardTopLeft + new Vector2((CardSize - labelSize.X) * 0.5f, CardSize - labelSize.Y - 26f);
            batch.DrawString(Items[i].Label, labelPos, LabelPixelSize, Items[i].Accent);
        }

        return GetContentSize();
    }

    private static Vector2[] StarPoints(Vector2 center, float outerRadius, float innerRadius, int points)
    {
        var result = new Vector2[points * 2];
        for (int i = 0; i < result.Length; i++)
        {
            float radius = (i % 2 == 0) ? outerRadius : innerRadius;
            float angle = -MathF.PI / 2f + i * MathF.PI / points;
            result[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }
        return result;
    }
}
