using System;
using Microsoft.Xna.Framework;
using MonoPrimitives.Primitives2D;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-logic checks for <see cref="Trail2D"/>'s ring-buffer behavior — no GraphicsDevice needed.</summary>
    internal static class Trail2DTests
    {
        public static void Run(TestResults results)
        {
            results.Check("Trail2D: constructor rejects capacity < 2", () =>
            {
                try { _ = new Trail2D(1); return "expected ArgumentOutOfRangeException for capacity=1"; }
                catch (ArgumentOutOfRangeException) { }
                try { _ = new Trail2D(0); return "expected ArgumentOutOfRangeException for capacity=0"; }
                catch (ArgumentOutOfRangeException) { }
                return null;
            });

            results.Check("Trail2D: Count grows to Capacity then stays there, evicting the oldest point", () =>
            {
                var trail = new Trail2D(3);
                if (trail.Count != 0 || trail.Capacity != 3) return "wrong initial Count/Capacity";

                trail.Add(new Vector2(1, 0));
                trail.Add(new Vector2(2, 0));
                trail.Add(new Vector2(3, 0));
                if (trail.Count != 3) return "Count should be 3 (== Capacity) after 3 Adds";
                if (trail[0] != new Vector2(1, 0) || trail[2] != new Vector2(3, 0)) return "indexer order wrong before wraparound";

                trail.Add(new Vector2(4, 0)); // evicts the oldest point, (1,0)
                if (trail.Count != 3) return "Count shouldn't exceed Capacity";
                if (trail[0] != new Vector2(2, 0)) return "oldest point wasn't evicted correctly";
                if (trail[2] != new Vector2(4, 0)) return "newest point should be at index Count-1";
                return null;
            });

            results.Check("Trail2D: this[] throws out of [0, Count)", () =>
            {
                var trail = new Trail2D(3);
                trail.Add(Vector2.Zero);
                try { _ = trail[-1]; return "expected ArgumentOutOfRangeException for a negative index"; }
                catch (ArgumentOutOfRangeException) { }
                try { _ = trail[1]; return "expected ArgumentOutOfRangeException for index == Count"; }
                catch (ArgumentOutOfRangeException) { }
                return null;
            });

            results.Check("Trail2D: Clear() resets Count to 0 and a subsequent Add starts a fresh trail", () =>
            {
                var trail = new Trail2D(3);
                trail.Add(new Vector2(1, 1));
                trail.Add(new Vector2(2, 2));
                trail.Clear();
                if (trail.Count != 0) return "Clear() should reset Count to 0";

                trail.Add(new Vector2(9, 9));
                if (trail.Count != 1 || trail[0] != new Vector2(9, 9)) return "trail didn't behave correctly after Clear()";
                return null;
            });

            results.Check("Trail2D: Draw() no-ops instead of throwing when Count < 2", () =>
            {
                var trail = new Trail2D(3);
                trail.Draw(null!, Color.White); // Count == 0: must return before touching the (null) batch
                trail.Add(Vector2.Zero);
                trail.Draw(null!, Color.White); // Count == 1: same
                return null;
            });
        }
    }
}
