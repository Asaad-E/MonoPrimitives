using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives.Primitives2D;

namespace MonoPrimitives.Template
{
    /// <summary>Everything a draw call typically needs, bundled so it's one parameter instead of four.</summary>
    public readonly record struct RenderContext(
        GraphicsDevice GraphicsDevice,
        GraphicsDeviceManager GraphicsDeviceManager,
        SpriteBatch SpriteBatch,
        Primitive2DBatch Primitive2DBatch);
}
