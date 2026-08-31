using Microsoft.Xna.Framework;

using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace MonogameLibs;

/// <summary>
/// A labeled swatch grid of every <see cref="Palette"/> color -- not part of the library; exists
/// purely to produce the <c>Color_Guide.md</c> screenshot. Content grows left-to-right/top-to-bottom
/// from the origin, same convention as Showcase2D/Gallery2D.
/// </summary>
internal static class ShowcasePalette2D
{
    private const float SwatchSize = 150f;
    private const float Gap = 46f;
    private const float LabelPixelSize = 2f;
    private const int Columns = 7;

    // Palette itself doesn't expose names as strings (only static Color fields), so this list is
    // kept in step with Palette.All's own declared order by hand -- see src/Core/Palette.cs.
    private static readonly string[] Names =
    {
        "TURQUOISE", "GREEN SEA", "EMERALD", "NEPHRITIS", "PETER RIVER", "BELIZE HOLE",
        "AMETHYST", "WISTERIA", "WET ASPHALT", "MIDNIGHT BLUE", "SUNFLOWER", "ORANGE",
        "CARROT", "PUMPKIN", "ALIZARIN", "POMEGRANATE", "CLOUDS", "SILVER", "CONCRETE",
        "ASBESTOS", "BACKGROUND",
    };

    /// <summary>Total content size, without drawing anything -- for framing a camera before the first <see cref="Draw"/> call.</summary>
    public static Vector2 GetContentSize()
    {
        int rows = (Palette.All.Length + Columns - 1) / Columns;
        return new Vector2(Columns * SwatchSize + (Columns - 1) * Gap, rows * SwatchSize + (rows - 1) * Gap);
    }

    /// <summary>Draws the swatch grid starting at the origin and returns its total content size (for camera framing).</summary>
    public static Vector2 Draw(Primitive2DBatch batch)
    {
        for (int i = 0; i < Palette.All.Length; i++)
        {
            int col = i % Columns, row = i / Columns;
            Vector2 topLeft = new(col * (SwatchSize + Gap), row * (SwatchSize + Gap));

            // Every swatch gets the same light border, not just a colored fill -- Background
            // (and Palette.All's own doc comment warns about this) would otherwise render as an
            // invisible hole against this class's own dark Palette.Background canvas.
            batch.FillRectangleRounded(topLeft, new Vector2(SwatchSize, SwatchSize), 16f, Palette.All[i]);
            batch.BorderRectangleRounded(topLeft, new Vector2(SwatchSize, SwatchSize), 16f, Palette.Silver, thickness: 2f);

            Vector2 labelSize = DebugFont5x7.MeasureText(Names[i], LabelPixelSize);
            Vector2 labelPos = topLeft + new Vector2((SwatchSize - labelSize.X) * 0.5f, SwatchSize + 12f);
            batch.DrawString(Names[i], labelPos, LabelPixelSize, Palette.Clouds);
        }

        return GetContentSize();
    }
}
