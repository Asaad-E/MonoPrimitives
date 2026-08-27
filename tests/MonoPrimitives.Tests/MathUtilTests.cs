using System;
using Microsoft.Xna.Framework;
using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-math checks for <see cref="MathUtil"/> — no GraphicsDevice needed.</summary>
    internal static class MathUtilTests
    {
        private static bool Close(float a, float b, float tolerance = 1e-4f) => MathF.Abs(a - b) < tolerance;

        public static void Run(TestResults results)
        {
            results.Check("MathUtil.Remap: basic and extrapolated ranges", () =>
            {
                if (!Close(MathUtil.Remap(5f, 0f, 10f, 0f, 100f), 50f)) return "expected 5 in [0,10]->[0,100] to be 50";
                if (!Close(MathUtil.Remap(0f, 0f, 10f, 20f, 30f), 20f)) return "expected the source min to map to the target min";
                if (!Close(MathUtil.Remap(10f, 0f, 10f, 20f, 30f), 30f)) return "expected the source max to map to the target max";
                if (!Close(MathUtil.Remap(15f, 0f, 10f, 0f, 100f), 150f)) return "expected out-of-range input to extrapolate, not clamp";
                if (!Close(MathUtil.Remap(5f, 0f, 10f, 10f, 0f), 5f)) return "expected a reversed target range to still map linearly";
                return null;
            });

            results.Check("MathUtil.DeltaAngle: shortest signed path, including wraparound", () =>
            {
                if (!Close(MathUtil.DeltaAngle(0f, 0f), 0f)) return "same angle should have zero delta";
                if (!Close(MathUtil.DeltaAngle(0f, MathHelper.PiOver2), MathHelper.PiOver2)) return "expected +90deg";
                if (!Close(MathUtil.DeltaAngle(MathHelper.PiOver2, 0f), -MathHelper.PiOver2)) return "expected -90deg";

                // 170deg -> -170deg is a 20deg step the short way around, not 340deg the long way.
                float a = MathHelper.ToRadians(170f);
                float b = MathHelper.ToRadians(-170f);
                float delta = MathUtil.DeltaAngle(a, b);
                if (!Close(delta, MathHelper.ToRadians(20f), 1e-3f)) return $"expected a short ~20deg wraparound step, got {MathHelper.ToDegrees(delta)}deg";
                return null;
            });

            results.Check("MathUtil.LerpAngle: interpolates the short way, including across the wrap boundary", () =>
            {
                if (!Close(MathUtil.LerpAngle(0f, MathHelper.PiOver2, 0f), 0f)) return "t=0 should return a";
                if (!Close(MathUtil.LerpAngle(0f, MathHelper.PiOver2, 0.5f), MathHelper.PiOver4)) return "t=0.5 should be halfway";
                if (!Close(MathUtil.LerpAngle(0f, MathHelper.PiOver2, 1f), MathHelper.PiOver2)) return "t=1 should return an angle equivalent to b";

                float a = MathHelper.ToRadians(170f);
                float b = MathHelper.ToRadians(-170f);
                float result = MathUtil.LerpAngle(a, b, 1f);
                float residual = MathHelper.WrapAngle(result - b);
                if (!Close(residual, 0f, 1e-3f)) return $"expected t=1 to land on an angle equivalent to b, residual {MathHelper.ToDegrees(residual)}deg";
                return null;
            });

            results.Check("MathUtil.PingPong: bounces between 0 and length, including for negative input", () =>
            {
                if (!Close(MathUtil.PingPong(0f, 5f), 0f)) return "t=0 should be 0";
                if (!Close(MathUtil.PingPong(2.5f, 5f), 2.5f)) return "t=length/2 should be length/2 (still climbing)";
                if (!Close(MathUtil.PingPong(5f, 5f), 5f)) return "t=length should be length (the turning point)";
                if (!Close(MathUtil.PingPong(7.5f, 5f), 2.5f)) return "t=1.5*length should be bouncing back down";
                if (!Close(MathUtil.PingPong(10f, 5f), 0f)) return "t=2*length should be back to 0 (one full cycle)";
                if (!Close(MathUtil.PingPong(-2.5f, 5f), 2.5f)) return "negative t should still bounce within [0,length]";
                if (MathUtil.PingPong(3f, 0f) != 0f) return "a non-positive length should return 0 rather than divide by zero";
                return null;
            });
        }
    }
}
