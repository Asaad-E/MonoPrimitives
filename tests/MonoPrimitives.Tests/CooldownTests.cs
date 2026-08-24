using System;
using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-logic checks for <see cref="Cooldown"/> — no GraphicsDevice needed.</summary>
    internal static class CooldownTests
    {
        private static bool CloseF(float a, float b, float eps = 1e-4f) => MathF.Abs(a - b) < eps;

        public static void Run(TestResults results)
        {
            results.Check("Cooldown: starts already IsReady, even with a nonzero Duration", () =>
            {
                var cd = new Cooldown(5f);
                if (!cd.IsReady) return "a fresh Cooldown should start ready";
                if (cd.Remaining != 0f) return "Remaining should be 0 before first use";
                if (!CloseF(cd.Progress, 1f)) return "Progress should read 1 (ready) at construction";
                return null;
            });

            results.Check("Cooldown: TryUse succeeds once, then fails until it counts back down", () =>
            {
                var cd = new Cooldown(2f);
                if (!cd.TryUse()) return "first TryUse should succeed (starts ready)";
                if (cd.IsReady) return "IsReady should be false immediately after TryUse";
                if (cd.TryUse()) return "second TryUse should fail while still counting down";

                cd.Update(1f);
                if (cd.IsReady) return "should not be ready yet after only half the duration";
                if (!CloseF(cd.Remaining, 1f)) return $"expected Remaining ~1, got {cd.Remaining}";

                cd.Update(1f);
                if (!cd.IsReady) return "should be ready after the full duration has elapsed";
                if (!cd.TryUse()) return "TryUse should succeed again once ready";
                return null;
            });

            results.Check("Cooldown.Update never drives Remaining negative, even if overshot past zero", () =>
            {
                var cd = new Cooldown(1f);
                cd.TryUse();
                cd.Update(5f); // way more than Duration
                if (cd.Remaining != 0f) return $"expected Remaining clamped at 0, got {cd.Remaining}";
                if (!cd.IsReady) return "should be ready after overshooting past the duration";
                return null;
            });

            results.Check("Cooldown.Progress moves from 0 (just used) to 1 (ready)", () =>
            {
                var cd = new Cooldown(4f);
                cd.TryUse();
                if (!CloseF(cd.Progress, 0f)) return $"expected Progress ~0 right after TryUse, got {cd.Progress}";
                cd.Update(2f);
                if (!CloseF(cd.Progress, 0.5f)) return $"expected Progress ~0.5 halfway through, got {cd.Progress}";
                cd.Update(2f);
                if (!CloseF(cd.Progress, 1f)) return $"expected Progress ~1 once ready, got {cd.Progress}";
                return null;
            });

            results.Check("Cooldown: Reset()/ResetReady() force not-ready/ready respectively", () =>
            {
                var cd = new Cooldown(3f);
                cd.Reset();
                if (cd.IsReady) return "Reset() should start the full countdown, not leave it ready";
                if (!CloseF(cd.Remaining, 3f)) return $"expected Remaining == Duration right after Reset(), got {cd.Remaining}";

                cd.ResetReady();
                if (!cd.IsReady) return "ResetReady() should force IsReady immediately";
                return null;
            });

            results.Check("Cooldown: Duration of 0 or less is always ready and reports Progress == 1", () =>
            {
                var cd = new Cooldown(0f);
                if (!cd.IsReady) return "zero-duration cooldown should always be ready";
                if (!CloseF(cd.Progress, 1f)) return "zero-duration Progress should read 1, not divide by zero";
                cd.TryUse();
                if (!cd.IsReady) return "TryUse on a zero-duration cooldown should still leave it ready immediately";
                return null;
            });
        }
    }
}
