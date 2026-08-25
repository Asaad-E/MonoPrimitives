using System;
using Microsoft.Xna.Framework;

namespace MonoPrimitives.Primitives3D
{
    /// <summary>Extension methods on MonoGame's own <see cref="Vector3"/> — the 3D counterpart to <see cref="MonoPrimitives.Vector2Extensions"/>, for the everyday vector math XNA's <see cref="Vector3"/> doesn't provide itself.</summary>
    /// <remarks>Part of <c>MonoPrimitives.Primitives3D</c> — these show up on any <see cref="Vector3"/> once this namespace is in scope, but they aren't native MonoGame members.</remarks>
    public static class Vector3Extensions
    {
        /// <summary>Unsigned angle (radians, always in <c>[0, PI]</c>) between <paramref name="from"/> and <paramref name="to"/> — how far apart the two directions are, with no sense of which way to turn.</summary>
        /// <remarks>A 3D vector has no single canonical "which way is positive" the way a 2D one does, so there's no signed counterpart without also naming a reference axis — see <see cref="AngleToSigned"/> for that.</remarks>
        public static float AngleTo(this Vector3 from, Vector3 to)
        {
            float lenProduct = MathF.Sqrt(from.LengthSquared() * to.LengthSquared());
            if (lenProduct < 1e-12f) return 0f;
            float cos = Math.Clamp(Vector3.Dot(from, to) / lenProduct, -1f, 1f);
            return MathF.Acos(cos);
        }

        /// <summary>Signed angle (radians, in <c>[-PI, PI]</c>) to rotate <paramref name="from"/> by around <paramref name="axis"/> to face <paramref name="to"/> — positive is counter-clockwise looking down <paramref name="axis"/> toward the origin (the right-hand rule, matching <see cref="Rotated"/>'s own sign convention).</summary>
        /// <remarks>
        /// <paramref name="axis"/> need not be pre-normalized. Both <paramref name="from"/> and
        /// <paramref name="to"/> are measured as their projection onto the plane perpendicular to
        /// <paramref name="axis"/> first, so a component of either vector that runs along
        /// <paramref name="axis"/> itself doesn't skew the result — e.g. "how much yaw to face that
        /// point" with <paramref name="axis"/> straight up stays correct even if the point is above
        /// or below eye level.
        /// </remarks>
        public static float AngleToSigned(this Vector3 from, Vector3 to, Vector3 axis)
        {
            Vector3 n = axis.SafeNormalize();
            if (n == Vector3.Zero) return 0f;
            Vector3 fromFlat = (from - Vector3.Dot(from, n) * n).SafeNormalize();
            Vector3 toFlat = (to - Vector3.Dot(to, n) * n).SafeNormalize();
            if (fromFlat == Vector3.Zero || toFlat == Vector3.Zero) return 0f;
            float cos = Math.Clamp(Vector3.Dot(fromFlat, toFlat), -1f, 1f);
            float sin = Vector3.Dot(Vector3.Cross(fromFlat, toFlat), n);
            return MathF.Atan2(sin, cos);
        }

        /// <summary>Returns <paramref name="v"/> rotated by <paramref name="radians"/> around <paramref name="axis"/> (right-hand rule — positive rotates counter-clockwise looking down <paramref name="axis"/> toward the origin, matching <see cref="AngleToSigned"/>'s sign convention) as a new vector.</summary>
        /// <remarks><paramref name="axis"/> need not be pre-normalized; a zero-length <paramref name="axis"/> leaves <paramref name="v"/> unchanged rather than producing <c>NaN</c>.</remarks>
        public static Vector3 Rotated(this Vector3 v, Vector3 axis, float radians)
        {
            Vector3 n = axis.SafeNormalize();
            return n == Vector3.Zero ? v : Vector3.Transform(v, Quaternion.CreateFromAxisAngle(n, radians));
        }

        /// <summary>Normalized direction from <paramref name="from"/> to <paramref name="to"/> — shorthand for <c>(to - from).SafeNormalize()</c>. Returns <see cref="Vector3.Zero"/> if the two points coincide.</summary>
        public static Vector3 DirectionTo(this Vector3 from, Vector3 to) => (to - from).SafeNormalize();

        /// <summary>
        /// Normalizes <paramref name="v"/>, or returns <paramref name="fallback"/> (default
        /// <see cref="Vector3.Zero"/>) instead of <c>NaN</c> when <paramref name="v"/> is at or near
        /// the zero vector — <see cref="Vector3.Normalize(Vector3)"/> itself produces <c>NaN</c> there.
        /// </summary>
        public static Vector3 SafeNormalize(this Vector3 v, Vector3 fallback = default)
        {
            float lenSq = v.LengthSquared();
            return lenSq < 1e-12f ? fallback : v * (1f / MathF.Sqrt(lenSq));
        }

        /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="maxDistance"/>, landing exactly on <paramref name="target"/> instead of overshooting past it. <paramref name="maxDistance"/> can be negative to move away from <paramref name="target"/> instead.</summary>
        /// <remarks>For a single <c>float</c> (not a <see cref="Vector3"/>), use <see cref="MonoPrimitives.Vector2Extensions.Approach(float,float,float)"/> directly — it's already dimension-agnostic, so it isn't duplicated here.</remarks>
        public static Vector3 Approach(this Vector3 current, Vector3 target, float maxDistance)
        {
            Vector3 toTarget = target - current;
            float dist = toTarget.Length();
            if (dist <= maxDistance || dist < 1e-12f) return target;
            return current + toTarget * (maxDistance / dist);
        }

        /// <summary>
        /// Clamps <paramref name="v"/>'s own length to at most <paramref name="maxLength"/>,
        /// preserving its direction. A no-op if
        /// <paramref name="v"/> is already shorter.
        /// </summary>
        public static Vector3 ClampMagnitude(this Vector3 v, float maxLength)
        {
            float lenSq = v.LengthSquared();
            if (lenSq <= maxLength * maxLength) return v;
            return v * (maxLength / MathF.Sqrt(lenSq));
        }

        /// <summary>Removes the component of <paramref name="v"/> along <paramref name="normal"/>, keeping only the tangential part — the direction to keep moving along a wall/floor/slope instead of stopping dead against it.</summary>
        /// <remarks><paramref name="normal"/> must already be unit length (not renormalized here, same convention as <see cref="Vector3.Reflect(Vector3,Vector3)"/>). Different from <see cref="Vector3.Reflect(Vector3,Vector3)"/>: <c>Reflect</c> flips the normal component (a bounce), <c>Slide</c> drops it entirely (a slide).</remarks>
        public static Vector3 Slide(this Vector3 v, Vector3 normal) => v - normal * Vector3.Dot(v, normal);
    }
}
