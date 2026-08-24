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
        /// <summary>
        /// Unsigned angle (radians, always in <c>[0, PI]</c>) between <paramref name="from"/> and
        /// <paramref name="to"/> — how far apart the two directions are, with no sense of which way
        /// to turn. A 3D vector has no single canonical "which way is positive" the way a 2D one
        /// does, so there's no signed counterpart without also naming a reference axis — see
        /// <see cref="AngleToSigned"/> for that.
        /// </summary>
        public static float AngleTo(this Vector3 from, Vector3 to)
        {
            float lenProduct = MathF.Sqrt(from.LengthSquared() * to.LengthSquared());
            if (lenProduct < 1e-12f) return 0f;
            float cos = Math.Clamp(Vector3.Dot(from, to) / lenProduct, -1f, 1f);
            return MathF.Acos(cos);
        }

        /// <summary>
        /// Signed angle (radians, in <c>[-PI, PI]</c>) to rotate <paramref name="from"/> by around
        /// <paramref name="axis"/> to face <paramref name="to"/> — positive is counter-clockwise
        /// looking down <paramref name="axis"/> toward the origin (the right-hand rule, matching
        /// <see cref="Rotated"/>'s own sign convention), Unity's <c>Vector3.SignedAngle</c>.
        /// <paramref name="axis"/> need not be pre-normalized. Both <paramref name="from"/> and
        /// <paramref name="to"/> are measured as their projection onto the plane perpendicular to
        /// <paramref name="axis"/> first, so a component of either vector that runs along
        /// <paramref name="axis"/> itself doesn't skew the result — e.g. "how much yaw to face that
        /// point" with <paramref name="axis"/> straight up stays correct even if the point is above
        /// or below eye level.
        /// </summary>
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

        /// <summary>
        /// Returns <paramref name="v"/> rotated by <paramref name="radians"/> around
        /// <paramref name="axis"/> (right-hand rule — positive rotates counter-clockwise looking
        /// down <paramref name="axis"/> toward the origin, matching <see cref="AngleToSigned"/>'s
        /// sign convention) as a new vector. A thin wrapper over
        /// <see cref="Quaternion.CreateFromAxisAngle(Vector3,float)"/> + <see cref="Vector3.Transform(Vector3,Quaternion)"/>,
        /// sparing you from building the quaternion by hand for a one-off rotation.
        /// <paramref name="axis"/> need not be pre-normalized; a zero-length <paramref name="axis"/>
        /// leaves <paramref name="v"/> unchanged rather than producing <c>NaN</c>.
        /// </summary>
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
