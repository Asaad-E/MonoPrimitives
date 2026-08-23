#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace TextReadabilityTest;

/// <summary>
/// Visual test for <c>DebugFont5x7</c>'s legibility: every printable glyph the font supports,
/// plus a short real sentence (a pangram, not lorem ipsum -- every letter appears, but it's
/// still readable English) at a normal reading size. Camera2D (WASD pan, wheel zoom, left-drag
/// pan) lets you zoom in to inspect individual glyphs or back out to judge it at reading size.
/// </summary>
public class Game1 : Game
{
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;

    private GraphicsDeviceManager _graphics;
    private Primitive2DBatch _batch2d = null!;
    private PrimitiveInput _input = null!;
    private Camera2D _camera2d = null!;

    private const string Sentence = "The quick brown fox jumps over the lazy dog.";

    public Game1()
    {
_graphics = new GraphicsDeviceManager(this) { 
            PreferredBackBufferWidth = WindowWidth,
            PreferredBackBufferHeight = WindowHeight,
            PreferMultiSampling = false,
            GraphicsProfile= GraphicsProfile.HiDef };

        _graphics.PreparingDeviceSettings += (sender, e) =>
        {
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 0;
        };        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _batch2d = new Primitive2DBatch(GraphicsDevice);
        _input = new PrimitiveInput();
        _camera2d = new Camera2D(target: Vector2.Zero, offset: new Vector2(20f, 40f));
        base.Initialize();
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _input.Update(dt);
        _camera2d.UpdateWithInput(_input, dt);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.Background);

        _batch2d.Begin(_camera2d.GetTransformMatrix());

        float y = 0f;
        y = DrawRow("ABCDEFGHIJKLMNOPQRSTUVWXYZ", y, 4f, Palette.Clouds);
        y = DrawRow("abcdefghijklmnopqrstuvwxyz", y, 4f, Palette.Clouds);
        y = DrawRow("0123456789", y, 4f, Palette.Sunflower);
        y = DrawRow(" !\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~", y, 4f, Palette.PeterRiver);

        y += 24f;
        _batch2d.DrawString("Every letter, so a font issue shows up somewhere above:", new Vector2(0, y), 2f, Palette.Silver);
        y += DebugFont5x7Height(2f) + 10f;
        DrawRow(Sentence, y, 2f, Color.White);

        _batch2d.End();

        DrawHud();
        base.Draw(gameTime);
    }

    private float DrawRow(string text, float y, float pixelSize, Color color)
    {
        _batch2d.DrawString(text, new Vector2(0, y), pixelSize, color);
        return y + DebugFont5x7Height(pixelSize) + 6f;
    }

    private static float DebugFont5x7Height(float pixelSize) => 7f * pixelSize;

    private void DrawHud()
    {
        _batch2d.Begin();
        _batch2d.DrawString("WASD/drag: pan   wheel: zoom", new Vector2(16, WindowHeight - 32), 1.5f, Palette.Silver);
        _batch2d.End();
    }
}
