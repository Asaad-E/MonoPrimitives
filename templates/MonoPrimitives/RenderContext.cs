using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives.Primitives2D;

namespace MonoPrimitives.Template
{
    public readonly record struct RenderContext(
        GraphicsDevice GraphicsDevice,
        GraphicsDeviceManager Graphics,
        SpriteBatch SpriteBatch,
        Primitive2DBatch Batch2D);
}
