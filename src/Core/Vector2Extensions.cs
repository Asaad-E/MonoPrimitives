using System;
using Microsoft.Xna.Framework;

namespace MonoPrimitives
{
    /// <summary>
    /// Extension methods on MonoGame's own <see cref="Vector2"/> — everyday 2D vector math XNA's
    /// <see cref="Vector2"/>/<see cref="MathHelper"/> don't provide themselves.
    /// </summary>
    /// <remarks>Part of <c>MonoPrimitives</c> — these show up on any <see cref="Vector2"/> once this namespace is in scope, but they aren't native MonoGame members.</remarks>
    public static class Vector2Extensions
    {
        /// <summary>
        /// The vector's own heading: the angle in <c>(-PI, PI]</c> (counter-clockwise from +X) that
        /// <c>new Vector2(MathF.Cos(a), MathF.Sin(a))</c> would reproduce. <see cref="Vector2.Zero"/>
        /// has no defined heading and returns <c>0</c>.
        /// </summary>
        /// <remarks>
        /// Exactly on the negative X axis, the result can be either <c>+PI</c> or <c>-PI</c>
        /// depending on the sign of a near-zero Y component (both represent the same angle) —
        /// standard <c>atan2</c> branch-cut behavior, not specific to this method.
        /// </remarks>
        public static float Angle(this Vector2 v) => MathF.Atan2(v.Y, v.X);

        /// <summary>
        /// Unsigned angle (radians, always in <c>[0, PI]</c>) between <paramref name="from"/> and
        /// <paramref name="to"/> — how far apart the two directions are, with no sense of which way
        /// to turn. Use <see cref="AngleToSigned"/> instead when the turning direction matters.
        /// </summary>
        public static float AngleTo(this Vector2 from, Vector2 to)
        {
            float lenProduct = MathF.Sqrt(from.LengthSquared() * to.LengthSquared());
            if (lenProduct < 1e-12f) return 0f;
            float cos = Math.Clamp(Vector2.Dot(from, to) / lenProduct, -1f, 1f);
            return MathF.Acos(cos);
        }

        /// <summary>
        /// Signed angle (radians, in <c>[-PI, PI]</c>) to rotate <paramref name="from"/> by to face
        /// <paramref name="to"/> — positive is counter-clockwise, matching <see cref="Rotated(Vector2,float)"/>'s own
        /// sign convention. Unlike <see cref="AngleTo"/>, this tells you which way to turn.
        /// </summary>
        public static float AngleToSigned(this Vector2 from, Vector2 to)
        {
            float cross = from.X * to.Y - from.Y * to.X;
            float dot = Vector2.Dot(from, to);
            return MathF.Atan2(cross, dot);
        }

        /// <summary>
        /// Returns <paramref name="v"/> rotated by <paramref name="radians"/> (counter-clockwise for
        /// a positive angle, matching <see cref="AngleToSigned"/>'s sign convention) as a new vector,
        /// without modifying <paramref name="v"/> itself.
        /// </summary>
        /// <remarks><see cref="Vector2.Rotate(float)"/>/<see cref="Vector2.RotateAround(Vector2,float)"/> mutate in place instead (return <c>void</c>) — named <c>Rotated</c> to avoid colliding with those.</remarks>
        public static Vector2 Rotated(this Vector2 v, float radians)
        {
            float cos = MathF.Cos(radians), sin = MathF.Sin(radians);
            return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
        }

        /// <summary>90°-clockwise rotation of <paramref name="v"/> — exact and trig-free (a swap and a negate), unlike <see cref="Rotated"/>.</summary>
        public static Vector2 PerpendicularClockwise(this Vector2 v) => new(v.Y, -v.X);

        /// <summary>90°-counter-clockwise rotation of <paramref name="v"/> — exact and trig-free, unlike <see cref="Rotated"/>.</summary>
        public static Vector2 PerpendicularCounterClockwise(this Vector2 v) => new(-v.Y, v.X);

        /// <summary>Normalized direction from <paramref name="from"/> to <paramref name="to"/> — shorthand for <c>(to - from).SafeNormalize()</c>. Returns <see cref="Vector2.Zero"/> if the two points coincide.</summary>
        public static Vector2 DirectionTo(this Vector2 from, Vector2 to) => (to - from).SafeNormalize();

        /// <summary>
        /// Normalizes <paramref name="v"/>, or returns <paramref name="fallback"/> (default
        /// <see cref="Vector2.Zero"/>) instead of <c>NaN</c> when <paramref name="v"/> is at or near
        /// the zero vector — <see cref="Vector2.Normalize()"/> itself produces <c>NaN</c> there.
        /// </summary>
        public static Vector2 SafeNormalize(this Vector2 v, Vector2 fallback = default)
        {
            float lenSq = v.LengthSquared();
            return lenSq < 1e-12f ? fallback : v * (1f / MathF.Sqrt(lenSq));
        }

        /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="maxDistance"/>, landing exactly on <paramref name="target"/> instead of overshooting past it.</summary>
        /// <remarks><paramref name="maxDistance"/> can be negative to move away from <paramref name="target"/> instead.</remarks>
        public static Vector2 Approach(this Vector2 current, Vector2 target, float maxDistance)
        {
            Vector2 toTarget = target - current;
            float dist = toTarget.Length();
            if (dist <= maxDistance || dist < 1e-12f) return target;
            return current + toTarget * (maxDistance / dist);
        }

        /// <inheritdoc cref="Approach(Vector2,Vector2,float)"/>
        public static float Approach(this float current, float target, float maxDistance)
        {
            if (current < target) return MathF.Min(current + maxDistance, target);
            return MathF.Max(current - maxDistance, target);
        }

        /// <summary>
        /// Clamps <paramref name="v"/>'s own length to at most <paramref name="maxLength"/>,
        /// preserving its direction. A no-op if <paramref name="v"/> is already shorter.
        /// </summary>
        public static Vector2 ClampMagnitude(this Vector2 v, float maxLength)
        {
            float lenSq = v.LengthSquared();
            if (lenSq <= maxLength * maxLength) return v;
            return v * (maxLength / MathF.Sqrt(lenSq));
        }

        /// <summary>Removes the component of <paramref name="v"/> along <paramref name="normal"/>, keeping only the tangential part — the direction to keep moving along a wall/floor instead of stopping dead against it.</summary>
        /// <remarks><paramref name="normal"/> must already be unit length (not renormalized here, same convention as <see cref="Vector2.Reflect(Vector2,Vector2)"/>). Different from <see cref="Vector2.Reflect(Vector2,Vector2)"/>: <c>Reflect</c> flips the normal component (a bounce), <c>Slide</c> drops it entirely (a slide).</remarks>
        public static Vector2 Slide(this Vector2 v, Vector2 normal) => v - normal * Vector2.Dot(v, normal);
    }

    /// <summary>Extension methods on MonoGame's own <see cref="GameTime"/>. Part of <c>MonoPrimitives</c>, not a native MonoGame member.</summary>
    public static class GameTimeExtensions
    {
        /// <summary>Shorthand for <c>(float)gameTime.ElapsedGameTime.TotalSeconds</c> — the delta-time value almost every <c>Update(GameTime)</c> ends up computing by hand.</summary>
        public static float GetElapsedTimeSeconds(this GameTime gameTime) => (float)gameTime.ElapsedGameTime.TotalSeconds;
    }
}
