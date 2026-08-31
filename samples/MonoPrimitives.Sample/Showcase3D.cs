using System;
using Microsoft.Xna.Framework;

using MonoPrimitives;
using MonoPrimitives.Primitives3D;

namespace MonogameLibs;

/// <summary>
/// A curated, presentation-quality shape showcase: one representative shape each, evenly spaced
/// with a soft ground glow and a centered, color-matched label -- not Gallery3D's exhaustive
/// per-row Fill/Border/Draw matrix. Not part of the library; exists purely to produce README/guide
/// screenshots that read clearly at a glance instead of the dev gallery's dense grid. Content
/// grows along +X from the origin, all in a single row (see the class remarks below for why).
/// </summary>
internal static class Showcase3D
{
    private const float ColumnSpacing = 6.5f;
    private const float LabelPixelSize = 0.1f;

    // A soft light halo instead of a dark contact shadow -- this showcase clears to the same dark
    // Palette.Background the 2D showcase's cards sit on (see Game1.ToggleShowcaseMode), where a
    // black shadow would be invisible.
    private static readonly Color GlowColor = new(Palette.Clouds, 35);

    private readonly record struct Item(string Label, Color Color, Action<Primitive3DBatch, Vector3, Color> Draw);

    // Seven iconic primitives in a single row, not Gallery3D's full 14-row-deep surface. A single
    // row keeps every shape at the same camera distance -- two rows at different depths made the
    // far row noticeably smaller and pushed the framing math around fighting that mismatch,
    // exactly the kind of "show less but show it well" tradeoff this class exists for. Base
    // position is ground level (Y=0) for every shape, elevated internally by each draw call as
    // needed -- same convention FillCylinder/FillCube's own "position" parameter uses.
    private static readonly Item[] Items =
    {
        new("CUBE", Palette.PeterRiver, (b, c, col) => b.FillCube(c + Vector3.UnitY * 1.2f, Vector3.One * 2.4f, col)),
        new("SPHERE", Palette.Alizarin, (b, c, col) => b.FillSphere(c + Vector3.UnitY * 1.3f, 1.3f, col)),
        new("CYLINDER", Palette.Emerald, (b, c, col) => b.FillCylinder(c, 1.1f, 1.1f, 2.4f, 24, col)),
        new("CONE", Palette.Sunflower, (b, c, col) => b.FillCylinder(c, 0f, 1.2f, 2.6f, 24, col)),
        new("CAPSULE", Palette.Amethyst, (b, c, col) => b.FillCapsule(c + Vector3.UnitY * 0.7f, c + Vector3.UnitY * 1.7f, 0.7f, 24, 12, col)),
        // A pyramid is just a cone with 4 sides instead of a smooth 24 -- same method as CONE.
        new("PYRAMID", Palette.Pomegranate, (b, c, col) => b.FillCylinder(c, 0f, 1.5f, 2.6f, 4, col)),
        new("TORUS", Palette.Turquoise, (b, c, col) => b.FillTorus(c + Vector3.UnitY * 0.45f, 1.2f, 0.45f, 24, 24, col)),
    };

    /// <summary>World bounds of the curated grid, without drawing anything -- for framing a camera before the first <see cref="Draw"/> call.</summary>
    public static BoundingBox GetContentBounds()
    {
        Vector3 min = new(-1.6f, 0f, -1.6f);
        Vector3 max = new((Items.Length - 1) * ColumnSpacing + 1.6f, 2.6f, 1.6f);
        return new BoundingBox(min, max);
    }

    /// <summary>Center of <see cref="GetContentBounds"/>, for pointing a curated camera at the grid.</summary>
    public static Vector3 GetContentCenter()
    {
        BoundingBox b = GetContentBounds();
        return (b.Min + b.Max) * 0.5f;
    }

    /// <summary>Draws the curated grid starting at the origin and returns its world bounds (for camera framing).</summary>
    public static BoundingBox Draw(Primitive3DBatch batch)
    {
        for (int i = 0; i < Items.Length; i++)
        {
            Vector3 baseCenter = new(i * ColumnSpacing, 0f, 0f);

            batch.FillCircle3D(baseCenter + Vector3.UnitY * 0.01f, 1.5f, Vector3.UnitX, 90f, GlowColor);
            Items[i].Draw(batch, baseCenter, Items[i].Color);
            // Offset toward the curated camera (see Game1.ToggleShowcaseMode, positioned on the
            // +Z side) instead of sitting directly under the shape, so the shape itself doesn't
            // occlude its own label. Colored to match its shape, same as the 2D showcase's
            // accent-colored card labels, instead of one flat text color for all of them.
            DrawCenteredCaption(batch, Items[i].Label, baseCenter + Vector3.UnitZ * 2f, Items[i].Color);
        }

        return GetContentBounds();
    }

    // Same centering trick Gallery3D's own caption uses: measure the billboarded text and offset
    // by half its width along the billboard's own right axis, so it centers correctly from any
    // viewing angle instead of only from straight ahead.
    private static void DrawCenteredCaption(Primitive3DBatch batch, string text, Vector3 anchor, Color color)
    {
        batch.GetBillboardAxes(anchor, out Vector3 right, out _);
        Vector2 size = Primitive3DBatch.MeasureText3D(text, LabelPixelSize);
        batch.DrawString3D(text, anchor - right * (size.X * 0.5f), LabelPixelSize, color);
    }
}
