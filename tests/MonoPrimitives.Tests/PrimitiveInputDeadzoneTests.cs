using System;
using Microsoft.Xna.Framework;
using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Pure-math checks for <see cref="PrimitiveInput.ApplyDeadzone(Vector2,float)"/>/<see cref="PrimitiveInput.ApplyDeadzone(float,float)"/>
    /// — no device needed (the previous version only clamped to zero below the deadzone radius; this
    /// verifies the fixed version also rescales the surviving range back onto a continuous [0,1],
    /// removing the jump the old clamp-only version had right at the cutoff).
    /// </summary>
    internal static class PrimitiveInputDeadzoneTests
    {
        private const float Tolerance = 1e-4f;

        public static void Run(TestResults results)
        {
            results.Check("ApplyDeadzone (Vector2): below the deadzone radius is exactly zero", () =>
            {
                Vector2 result = PrimitiveInput.ApplyDeadzone(new Vector2(0.1f, 0f), 0.15f);
                return result == Vector2.Zero ? null : $"expected Vector2.Zero, got {result}";
            });

            results.Check("ApplyDeadzone (Vector2): at full deflection (magnitude 1) rescales back to ~1, not less", () =>
            {
                Vector2 input = new(1f, 0f);
                Vector2 result = PrimitiveInput.ApplyDeadzone(input, 0.15f);
                float len = result.Length();
                return MathF.Abs(len - 1f) <= Tolerance ? null : $"expected magnitude ~1, got {len}";
            });

            results.Check("ApplyDeadzone (Vector2): a known midpoint matches the manual rescale formula", () =>
            {
                // deadzone=0.2, magnitude=0.6 -> (0.6-0.2)/(1-0.2) = 0.5
                Vector2 input = new(0.6f, 0f);
                Vector2 result = PrimitiveInput.ApplyDeadzone(input, 0.2f);
                return MathF.Abs(result.Length() - 0.5f) <= Tolerance ? null : $"expected magnitude 0.5, got {result.Length()} ({result})";
            });

            results.Check("ApplyDeadzone (Vector2): no jump right at the cutoff (the bug this fixes)", () =>
            {
                const float deadzone = 0.15f;
                Vector2 justBelow = PrimitiveInput.ApplyDeadzone(new Vector2(deadzone - 0.001f, 0f), deadzone);
                Vector2 justAbove = PrimitiveInput.ApplyDeadzone(new Vector2(deadzone + 0.001f, 0f), deadzone);

                if (justBelow != Vector2.Zero) return $"expected exactly zero just below the cutoff, got {justBelow}";
                // The old (clamp-only) behavior would have returned magnitude ~deadzone (~0.15) here --
                // a real, visible jump. The fixed version should be close to zero instead.
                if (justAbove.Length() > 0.02f) return $"expected a near-zero magnitude just above the cutoff (continuous ramp), got {justAbove.Length()} -- looks like the old jump-to-{deadzone} behavior";
                return null;
            });

            results.Check("ApplyDeadzone (Vector2): direction is preserved exactly, only magnitude changes", () =>
            {
                Vector2 input = new(3f, 4f); // magnitude 5, direction (0.6, 0.8) -- normalize down to a valid stick reading first
                Vector2 normalizedInput = Vector2.Normalize(input) * 0.9f;
                Vector2 result = PrimitiveInput.ApplyDeadzone(normalizedInput, 0.15f);
                Vector2 expectedDirection = Vector2.Normalize(normalizedInput);
                Vector2 actualDirection = Vector2.Normalize(result);
                return Vector2.Distance(expectedDirection, actualDirection) <= Tolerance
                    ? null
                    : $"expected direction {expectedDirection}, got {actualDirection}";
            });

            results.Check("ApplyDeadzone (Vector2): a magnitude slightly over 1 (raw hardware noise) doesn't produce an output over 1", () =>
            {
                Vector2 result = PrimitiveInput.ApplyDeadzone(new Vector2(1.05f, 0f), 0.15f);
                return result.Length() <= 1f + Tolerance ? null : $"expected magnitude <= 1, got {result.Length()}";
            });

            results.Check("ApplyDeadzone (Vector2): a degenerate deadzone >= 1 always returns zero, never divides by a bad range", () =>
            {
                Vector2 result = PrimitiveInput.ApplyDeadzone(new Vector2(0.99f, 0f), 1f);
                if (result != Vector2.Zero) return $"expected zero, got {result}";
                if (float.IsNaN(result.X) || float.IsNaN(result.Y)) return "got NaN";
                return null;
            });

            results.Check("ApplyDeadzone (float, triggers): same rescale behavior as the Vector2 overload", () =>
            {
                float belowDeadzone = PrimitiveInput.ApplyDeadzone(0.02f, 0.05f);
                float atMax = PrimitiveInput.ApplyDeadzone(1f, 0.05f);
                float midpoint = PrimitiveInput.ApplyDeadzone(0.55f, 0.05f); // (0.55-0.05)/(1-0.05) = 0.5263...

                if (belowDeadzone != 0f) return $"expected 0 below the deadzone, got {belowDeadzone}";
                if (MathF.Abs(atMax - 1f) > Tolerance) return $"expected ~1 at full pull, got {atMax}";
                float expectedMidpoint = (0.55f - 0.05f) / (1f - 0.05f);
                if (MathF.Abs(midpoint - expectedMidpoint) > Tolerance) return $"expected {expectedMidpoint}, got {midpoint}";
                return null;
            });
        }
    }
}
