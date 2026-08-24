using System;
using Microsoft.Xna.Framework;

namespace MonoPrimitives
{
    /// <summary>
    /// Float-precision counterpart to MonoGame's own <see cref="Rectangle"/> (integer-only) — for
    /// anything that needs sub-pixel positions/sizes (a zoomed camera's visible bounds, a smoothly
    /// scaling UI panel, a hitbox that shouldn't snap to whole pixels) without truncating them.
    /// Mirrors <see cref="Rectangle"/>'s own member shape (<see cref="Left"/>/<see cref="Right"/>/
    /// <see cref="Top"/>/<see cref="Bottom"/>/<see cref="Contains(Vector2)"/>/<see cref="Intersects"/>/
    /// <see cref="Inflate"/>/<see cref="Union"/>/<see cref="Intersect"/>) so it behaves exactly like
    /// the type you already know, just without the integer rounding.
    /// </summary>
    public struct RectangleF : IEquatable<RectangleF>
    {
        /// <summary>X position of the rectangle's left edge.</summary>
        public float X;

        /// <summary>Y position of the rectangle's top edge.</summary>
        public float Y;

        /// <summary>Width. Can be negative (an "inverted" rectangle) — nothing here guards against it, same as <see cref="Rectangle"/>.</summary>
        public float Width;

        /// <summary>Height. Can be negative, same caveat as <see cref="Width"/>.</summary>
        public float Height;

        /// <summary>A zero-sized rectangle at the origin.</summary>
        public static readonly RectangleF Empty = new(0f, 0f, 0f, 0f);

        public RectangleF(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>Builds a rectangle of <paramref name="size"/> centered on <paramref name="center"/>.</summary>
        public static RectangleF FromCenter(Vector2 center, Vector2 size)
            => new(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f, size.X, size.Y);

        public readonly float Left => X;
        public readonly float Right => X + Width;
        public readonly float Top => Y;
        public readonly float Bottom => Y + Height;

        public Vector2 Position
        {
            readonly get => new(X, Y);
            set { X = value.X; Y = value.Y; }
        }

        public Vector2 Size
        {
            readonly get => new(Width, Height);
            set { Width = value.X; Height = value.Y; }
        }

        /// <summary>Center point — the average of the opposite corners, so it stays correct even for a negative <see cref="Width"/>/<see cref="Height"/>.</summary>
        public readonly Vector2 Center => new(X + Width * 0.5f, Y + Height * 0.5f);

        public readonly bool IsEmpty => Width <= 0f || Height <= 0f;

        public readonly bool Contains(float x, float y) => x >= X && x < Right && y >= Y && y < Bottom;
        public readonly bool Contains(Vector2 point) => Contains(point.X, point.Y);

        /// <summary>True if <paramref name="other"/> lies entirely within this rectangle.</summary>
        public readonly bool Contains(RectangleF other) => other.X >= X && other.Right <= Right && other.Y >= Y && other.Bottom <= Bottom;

        public readonly bool Intersects(RectangleF other) => X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;

        /// <summary>Grows (or shrinks, for a negative amount) the rectangle by <paramref name="horizontalAmount"/>/<paramref name="verticalAmount"/> on each side, keeping the same center — matches <see cref="Rectangle.Inflate(int,int)"/>'s own convention (doubles the total size change), returned rather than mutated in place.</summary>
        public readonly RectangleF Inflate(float horizontalAmount, float verticalAmount)
            => new(X - horizontalAmount, Y - verticalAmount, Width + horizontalAmount * 2f, Height + verticalAmount * 2f);

        /// <summary>The overlapping region of <paramref name="a"/> and <paramref name="b"/>, or <see cref="Empty"/> if they don't intersect.</summary>
        public static RectangleF Intersect(RectangleF a, RectangleF b)
        {
            float left = MathF.Max(a.X, b.X);
            float top = MathF.Max(a.Y, b.Y);
            float right = MathF.Min(a.Right, b.Right);
            float bottom = MathF.Min(a.Bottom, b.Bottom);
            return right > left && bottom > top ? new RectangleF(left, top, right - left, bottom - top) : Empty;
        }

        /// <summary>The smallest rectangle containing both <paramref name="a"/> and <paramref name="b"/>.</summary>
        public static RectangleF Union(RectangleF a, RectangleF b)
        {
            float left = MathF.Min(a.X, b.X);
            float top = MathF.Min(a.Y, b.Y);
            float right = MathF.Max(a.Right, b.Right);
            float bottom = MathF.Max(a.Bottom, b.Bottom);
            return new RectangleF(left, top, right - left, bottom - top);
        }

        /// <summary>Rounds to the nearest integer <see cref="Rectangle"/> — the inverse of the implicit <c>Rectangle</c>-to-<see cref="RectangleF"/> conversion, which is exact.</summary>
        public readonly Rectangle ToRectangle()
            => new((int)MathF.Round(X), (int)MathF.Round(Y), (int)MathF.Round(Width), (int)MathF.Round(Height));

        /// <summary>A <see cref="Rectangle"/>'s integer values convert to <see cref="RectangleF"/> for free — no data loss going this direction, unlike <see cref="ToRectangle"/>.</summary>
        public static implicit operator RectangleF(Rectangle r) => new(r.X, r.Y, r.Width, r.Height);

        public readonly bool Equals(RectangleF other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
        public override readonly bool Equals(object obj) => obj is RectangleF other && Equals(other);
        public override readonly int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
        public static bool operator ==(RectangleF a, RectangleF b) => a.Equals(b);
        public static bool operator !=(RectangleF a, RectangleF b) => !a.Equals(b);

        public override readonly string ToString() => $"{{X:{X} Y:{Y} Width:{Width} Height:{Height}}}";
    }
}
