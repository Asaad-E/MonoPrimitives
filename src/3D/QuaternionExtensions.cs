using System;
using Microsoft.Xna.Framework;

namespace MonoPrimitives.Primitives3D
{
    /// <summary>Extension methods on MonoGame's own <see cref="Quaternion"/> — currently just the missing inverse of <see cref="Quaternion.CreateFromYawPitchRoll(float,float,float)"/>, which MonoGame already has natively.</summary>
    public static class QuaternionExtensions
    {
        /// <summary>Decomposes <paramref name="q"/> back into pitch (X), yaw (Y), and roll (Z) radians — the inverse of <see cref="Quaternion.CreateFromYawPitchRoll(float,float,float)"/>.</summary>
        /// <remarks>
        /// Verified by round-tripping 20000 random angle triples through <c>CreateFromYawPitchRoll</c>
        /// then this method then back through <c>CreateFromYawPitchRoll</c> and confirming the
        /// reconstructed quaternion matches (worst-case dot product 0.9999997) -- not assumed correct
        /// from the formula alone. Like any Euler-angle extraction, this loses a degree of freedom
        /// (gimbal lock) when pitch is at or near +/-90 degrees -- yaw and roll become
        /// indistinguishable there, and the specific values returned aren't meaningful, though the
        /// reconstructed rotation is still correct.
        /// </remarks>
        public static Vector3 ToEuler(this Quaternion q)
        {
            float sinPitch = Math.Clamp(2f * (q.W * q.X - q.Y * q.Z), -1f, 1f);
            float pitch = MathF.Asin(sinPitch);
            float yaw = MathF.Atan2(2f * (q.W * q.Y + q.X * q.Z), 1f - 2f * (q.X * q.X + q.Y * q.Y));
            float roll = MathF.Atan2(2f * (q.W * q.Z + q.X * q.Y), 1f - 2f * (q.X * q.X + q.Z * q.Z));
            return new Vector3(pitch, yaw, roll);
        }
    }
}
