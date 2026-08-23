using System;
using Microsoft.Xna.Framework;

namespace MonoPrimitives
{
    /// <summary>
    /// Extension methods on MonoGame's own <see cref="Vector2"/> — everyday 2D vector math XNA's
    /// <see cref="Vector2"/>/<see cref="MathHelper"/> don't provide themselves, confirmed missing by
    /// comparison against raylib's <c>raymath.h</c>, <see cref="System.Numerics"/>, Godot, Unity, and
    /// Processing/p5.js. Part of <c>MonoPrimitives</c> — these show up on any <see cref="Vector2"/>
    /// once this namespace is in scope, but they aren't native MonoGame members.
    /// </summary>
    public static class Vector2Extensions
    {
        /// <summary>
        /// The vector's own heading: the angle in <c>(-PI, PI]</c> (counter-clockwise from +X) that
        /// <c>new Vector2(MathF.Cos(a), MathF.Sin(a))</c> would reproduce. <see cref="Vector2.Zero"/>
        /// has no defined heading and returns <c>0</c>. Exactly on the negative X axis, the result
        /// can be either <c>+PI</c> or <c>-PI</c> depending on the sign of a near-zero Y component
        /// (both represent the same angle) — standard <c>atan2</c> branch-cut behavior, not specific
        /// to this method.
        /// </summary>
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
        /// <paramref name="to"/> — positive is counter-clockwise, matching <see cref="Rotate"/>'s own
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
        /// without modifying <paramref name="v"/> itself — <see cref="Vector2"/>'s own
        /// <see cref="Vector2.Rotate(float)"/>/<see cref="Vector2.RotateAround(Vector2,float)"/> instead
        /// mutate the vector in place (they return <c>void</c>), which only works on a variable, not
        /// an expression or a value you want to keep the original of. Named to match Godot's own
        /// <c>Vector2.rotated()</c> — the same "give me a rotated copy" shape.
        /// </summary>
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

        /// <summary>
        /// Moves <paramref name="current"/> toward <paramref name="target"/> by at most
        /// <paramref name="maxDistance"/>, landing exactly on <paramref name="target"/> instead of
        /// overshooting past it — Godot's <c>move_toward</c>/Unity's <c>Vector2.MoveTowards</c>.
        /// <paramref name="maxDistance"/> can be negative to move away from <paramref name="target"/> instead.
        /// </summary>
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
        /// preserving its direction — raylib's <c>Vector2ClampValue</c>, Godot's <c>limit_length</c>,
        /// Unity's <c>Vector2.ClampMagnitude</c>. A no-op if <paramref name="v"/> is already shorter.
        /// </summary>
        public static Vector2 ClampMagnitude(this Vector2 v, float maxLength)
        {
            float lenSq = v.LengthSquared();
            if (lenSq <= maxLength * maxLength) return v;
            return v * (maxLength / MathF.Sqrt(lenSq));
        }
    }

    /// <summary>Extension methods on MonoGame's own <see cref="GameTime"/>. Part of <c>MonoPrimitives</c>, not a native MonoGame member.</summary>
    public static class GameTimeExtensions
    {
        /// <summary>Shorthand for <c>(float)gameTime.ElapsedGameTime.TotalSeconds</c> — the delta-time value almost every <c>Update(GameTime)</c> ends up computing by hand.</summary>
        public static float GetElapsedTimeSeconds(this GameTime gameTime) => (float)gameTime.ElapsedGameTime.TotalSeconds;
    }
}
