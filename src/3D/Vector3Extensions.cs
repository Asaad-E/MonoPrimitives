using System;
using Microsoft.Xna.Framework;

namespace MonoPrimitives.Primitives3D
{
    /// <summary>
    /// Extension methods on MonoGame's own <see cref="Vector3"/> — the 3D counterpart to
    /// <see cref="MonoPrimitives.Vector2Extensions"/>, for the everyday vector math XNA's
    /// <see cref="Vector3"/> doesn't provide itself. Confirmed missing by direct inspection of the
    /// referenced MonoGame.Framework assembly rather than assumed — <see cref="Vector3.Reflect(Vector3,Vector3)"/>,
    /// <see cref="Vector3.Clamp(Vector3,Vector3,Vector3)"/>, and <see cref="Vector3.Lerp(Vector3,Vector3,float)"/>
    /// already exist natively and are deliberately not repeated here. Lives in this namespace rather
    /// than <c>Core/</c> since, unlike <see cref="Vector2"/> (used by both halves of this library for
    /// screen-space positions), nothing in this library's 2D half ever touches a <see cref="Vector3"/>.
    /// </summary>
    public static class Vector3Extensions
    {
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

        /// <summary>
        /// Moves <paramref name="current"/> toward <paramref name="target"/> by at most
        /// <paramref name="maxDistance"/>, landing exactly on <paramref name="target"/> instead of
        /// overshooting past it — Godot's <c>move_toward</c>/Unity's <c>Vector3.MoveTowards</c>.
        /// <paramref name="maxDistance"/> can be negative to move away from <paramref name="target"/>
        /// instead. For a single <c>float</c> value (not a <see cref="Vector3"/>), reuse
        /// <see cref="MonoPrimitives.Vector2Extensions.Approach(float,float,float)"/> directly — that
        /// overload is already dimension-agnostic (plain 1D scalar math, nothing 2D-specific about
        /// it), so it isn't duplicated here; a second identical <c>float</c> overload in this class
        /// would make any call site with both namespaces in scope ambiguous (<c>CS0121</c>) instead.
        /// </summary>
        public static Vector3 Approach(this Vector3 current, Vector3 target, float maxDistance)
        {
            Vector3 toTarget = target - current;
            float dist = toTarget.Length();
            if (dist <= maxDistance || dist < 1e-12f) return target;
            return current + toTarget * (maxDistance / dist);
        }

        /// <summary>
        /// Clamps <paramref name="v"/>'s own length to at most <paramref name="maxLength"/>,
        /// preserving its direction — Unity's <c>Vector3.ClampMagnitude</c>. A no-op if
        /// <paramref name="v"/> is already shorter.
        /// </summary>
        public static Vector3 ClampMagnitude(this Vector3 v, float maxLength)
        {
            float lenSq = v.LengthSquared();
            if (lenSq <= maxLength * maxLength) return v;
            return v * (maxLength / MathF.Sqrt(lenSq));
        }
    }
}
