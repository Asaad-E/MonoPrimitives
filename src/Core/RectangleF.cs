using System;
using Microsoft.Xna.Framework;

namespace MonoPrimitives
{
    /// <summary>
    /// Float-precision counterpart to MonoGame's own <see cref="Rectangle"/> (integer-only) — for
    /// anything that needs sub-pixel positions/sizes (a zoomed camera's visible bounds, a smoothly
    /// scaling UI panel, a hitbox that shouldn't snap to whole pixels) without truncating them.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="Rectangle"/>'s own member shape (<see cref="Left"/>/<see cref="Right"/>/
    /// <see cref="Top"/>/<see cref="Bottom"/>/<see cref="Contains(Vector2)"/>/<see cref="Intersects"/>/
    /// <see cref="Inflate"/>/<see cref="Union"/>/<see cref="Intersect"/>) so it behaves exactly like
    /// the type you already know, just without the integer rounding.
    /// </remarks>
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

        /// <summary>Constructs a rectangle directly from its edges' position and size.</summary>
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

        /// <summary>X position of the left edge — same value as <see cref="X"/>.</summary>
        public readonly float Left => X;

        /// <summary>X position of the right edge (<see cref="X"/> + <see cref="Width"/>).</summary>
        public readonly float Right => X + Width;

        /// <summary>Y position of the top edge — same value as <see cref="Y"/>.</summary>
        public readonly float Top => Y;

        /// <summary>Y position of the bottom edge (<see cref="Y"/> + <see cref="Height"/>).</summary>
        public readonly float Bottom => Y + Height;

        /// <summary><see cref="X"/>/<see cref="Y"/> as a <see cref="Vector2"/>.</summary>
        public Vector2 Position
        {
            readonly get => new(X, Y);
            set { X = value.X; Y = value.Y; }
        }

        /// <summary><see cref="Width"/>/<see cref="Height"/> as a <see cref="Vector2"/>.</summary>
        public Vector2 Size
        {
            readonly get => new(Width, Height);
            set { Width = value.X; Height = value.Y; }
        }

        /// <summary>Center point — the average of the opposite corners, so it stays correct even for a negative <see cref="Width"/>/<see cref="Height"/>.</summary>
        public readonly Vector2 Center => new(X + Width * 0.5f, Y + Height * 0.5f);

        /// <summary>True when <see cref="Width"/> or <see cref="Height"/> is zero or negative.</summary>
        public readonly bool IsEmpty => Width <= 0f || Height <= 0f;

        /// <summary>True if the point <c>(<paramref name="x"/>, <paramref name="y"/>)</c> lies inside this rectangle.</summary>
        public readonly bool Contains(float x, float y) => x >= X && x < Right && y >= Y && y < Bottom;

        /// <summary>True if <paramref name="point"/> lies inside this rectangle.</summary>
        public readonly bool Contains(Vector2 point) => Contains(point.X, point.Y);

        /// <summary>True if <paramref name="other"/> lies entirely within this rectangle.</summary>
        public readonly bool Contains(RectangleF other) => other.X >= X && other.Right <= Right && other.Y >= Y && other.Bottom <= Bottom;

        /// <summary>True if this rectangle and <paramref name="other"/> overlap — edges merely touching does not count.</summary>
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

        /// <summary>Linearly interpolates each of <see cref="X"/>/<see cref="Y"/>/<see cref="Width"/>/<see cref="Height"/> independently between <paramref name="a"/> and <paramref name="b"/>.</summary>
        /// <remarks>Useful for easing a rect-based transition — e.g. animating toward the rect <see cref="Primitives2D.Camera2D.FitBounds"/>/<see cref="Primitives3D.Camera3D.FitBounds"/> would set instantly, or a UI panel resizing into place. <paramref name="t"/> isn't clamped — values outside <c>[0,1]</c> extrapolate.</remarks>
        public static RectangleF Lerp(RectangleF a, RectangleF b, float t)
            => new(MathHelper.Lerp(a.X, b.X, t), MathHelper.Lerp(a.Y, b.Y, t), MathHelper.Lerp(a.Width, b.Width, t), MathHelper.Lerp(a.Height, b.Height, t));

        /// <summary>Rounds to the nearest integer <see cref="Rectangle"/> — the inverse of the implicit <c>Rectangle</c>-to-<see cref="RectangleF"/> conversion, which is exact.</summary>
        public readonly Rectangle ToRectangle()
            => new((int)MathF.Round(X), (int)MathF.Round(Y), (int)MathF.Round(Width), (int)MathF.Round(Height));

        /// <summary>A <see cref="Rectangle"/>'s integer values convert to <see cref="RectangleF"/> for free — no data loss going this direction, unlike <see cref="ToRectangle"/>.</summary>
        public static implicit operator RectangleF(Rectangle r) => new(r.X, r.Y, r.Width, r.Height);

        /// <summary>True if every field matches <paramref name="other"/> exactly (no epsilon tolerance).</summary>
        public readonly bool Equals(RectangleF other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

        /// <inheritdoc cref="Equals(RectangleF)"/>
        public override readonly bool Equals(object? obj) => obj is RectangleF other && Equals(other);

        /// <summary>Hash combining all 4 fields, consistent with <see cref="Equals(RectangleF)"/>.</summary>
        public override readonly int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

        /// <inheritdoc cref="Equals(RectangleF)"/>
        public static bool operator ==(RectangleF a, RectangleF b) => a.Equals(b);

        /// <summary>The negation of <c>==</c>.</summary>
        public static bool operator !=(RectangleF a, RectangleF b) => !a.Equals(b);

        /// <summary>Debug-friendly string, matching <see cref="Rectangle"/>'s own <c>ToString</c> format.</summary>
        public override readonly string ToString() => $"{{X:{X} Y:{Y} Width:{Width} Height:{Height}}}";
    }
}
