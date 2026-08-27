using System;
using Microsoft.Xna.Framework;
using MonoPrimitives.Primitives3D;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-math checks for <see cref="QuaternionExtensions.ToEuler"/> — no GraphicsDevice needed.</summary>
    internal static class QuaternionExtensionsTests
    {
        private static bool QuaternionsMatch(Quaternion a, Quaternion b, float tolerance)
        {
            // q and -q represent the same rotation.
            float dot = MathF.Abs(a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W);
            return dot > 1f - tolerance;
        }

        public static void Run(TestResults results)
        {
            results.Check("QuaternionExtensions.ToEuler: identity quaternion is all zeros", () =>
            {
                Vector3 euler = Quaternion.Identity.ToEuler();
                bool ok = MathF.Abs(euler.X) < 1e-5f && MathF.Abs(euler.Y) < 1e-5f && MathF.Abs(euler.Z) < 1e-5f;
                return ok ? null : $"expected (0,0,0) for the identity quaternion, got {euler}";
            });

            results.Check("QuaternionExtensions.ToEuler: round-trips through CreateFromYawPitchRoll across 2000 random angles (excluding gimbal lock)", () =>
            {
                var rng = new Random(12345);
                for (int i = 0; i < 2000; i++)
                {
                    float yaw = (float)(rng.NextDouble() * 2 - 1) * MathHelper.Pi;
                    // Kept away from the +/-90deg gimbal-lock singularity -- an inherent property of
                    // Euler angles, not a bug (documented on ToEuler's own doc comment).
                    float pitch = (float)(rng.NextDouble() * 2 - 1) * (MathHelper.PiOver2 * 0.95f);
                    float roll = (float)(rng.NextDouble() * 2 - 1) * MathHelper.Pi;

                    Quaternion q1 = Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);
                    Vector3 euler = q1.ToEuler();
                    Quaternion q2 = Quaternion.CreateFromYawPitchRoll(euler.Y, euler.X, euler.Z);

                    if (!QuaternionsMatch(q1, q2, 1e-3f))
                        return $"round-trip failed at yaw={yaw:F4} pitch={pitch:F4} roll={roll:F4} -> got euler={euler}";
                }
                return null;
            });

            results.Check("QuaternionExtensions.ToEuler: a 90-degree yaw-only rotation decomposes to (0, 90deg, 0)", () =>
            {
                Quaternion q = Quaternion.CreateFromYawPitchRoll(MathHelper.PiOver2, 0f, 0f);
                Vector3 euler = q.ToEuler();
                bool ok = MathF.Abs(euler.X) < 1e-4f && MathF.Abs(euler.Y - MathHelper.PiOver2) < 1e-4f && MathF.Abs(euler.Z) < 1e-4f;
                return ok ? null : $"expected (0, 90deg, 0), got {euler}";
            });
        }
    }
}
