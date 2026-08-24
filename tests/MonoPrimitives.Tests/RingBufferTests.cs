using System;
using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-logic checks for <see cref="RingBuffer{T}"/> — no GraphicsDevice needed.</summary>
    internal static class RingBufferTests
    {
        public static void Run(TestResults results)
        {
            results.Check("RingBuffer: Count grows to Capacity then stays there, indexer reads oldest..newest in order", () =>
            {
                var buf = new RingBuffer<int>(3);
                if (buf.Count != 0 || buf.Capacity != 3) return "wrong initial Count/Capacity";

                buf.Add(1);
                if (buf.Count != 1 || buf[0] != 1) return "wrong state after one Add";

                buf.Add(2);
                buf.Add(3);
                if (buf.Count != 3) return "Count should be 3 (== Capacity) after 3 Adds";
                if (buf[0] != 1 || buf[1] != 2 || buf[2] != 3) return "indexer order wrong before wraparound";

                buf.Add(4); // evicts the oldest (1)
                if (buf.Count != 3) return "Count shouldn't exceed Capacity";
                if (buf[0] != 2 || buf[1] != 3 || buf[2] != 4) return "oldest element wasn't evicted correctly";
                return null;
            });

            results.Check("RingBuffer: Newest/Oldest match the indexer's own ends; throw on an empty buffer", () =>
            {
                var buf = new RingBuffer<int>(3);
                try { _ = buf.Newest; return "expected InvalidOperationException on empty Newest"; }
                catch (InvalidOperationException) { }
                try { _ = buf.Oldest; return "expected InvalidOperationException on empty Oldest"; }
                catch (InvalidOperationException) { }

                buf.Add(10);
                buf.Add(20);
                buf.Add(30);
                buf.Add(40); // wraps once, oldest recorded is now 20
                if (buf.Newest != 40) return $"expected Newest==40, got {buf.Newest}";
                if (buf.Oldest != 20) return $"expected Oldest==20, got {buf.Oldest}";
                return null;
            });

            results.Check("RingBuffer: this[] throws out of [0, Count)", () =>
            {
                var buf = new RingBuffer<int>(3);
                buf.Add(1);
                try { _ = buf[-1]; return "expected ArgumentOutOfRangeException for a negative index"; }
                catch (ArgumentOutOfRangeException) { }
                try { _ = buf[1]; return "expected ArgumentOutOfRangeException for index == Count"; }
                catch (ArgumentOutOfRangeException) { }
                return null;
            });

            results.Check("RingBuffer: foreach enumerates oldest-first to newest-last, matching the indexer", () =>
            {
                var buf = new RingBuffer<int>(4);
                buf.Add(1);
                buf.Add(2);
                buf.Add(3);
                buf.Add(4);
                buf.Add(5); // wraps once: recorded values are now 2,3,4,5

                int[] expected = { 2, 3, 4, 5 };
                int i = 0;
                foreach (int value in buf)
                {
                    if (i >= expected.Length) return "enumerated more elements than expected";
                    if (value != expected[i]) return $"enumeration order wrong at index {i}: expected {expected[i]}, got {value}";
                    i++;
                }
                if (i != expected.Length) return $"enumerated {i} elements, expected {expected.Length}";
                return null;
            });

            results.Check("RingBuffer: Clear() resets Count to 0 and a subsequent Add starts a fresh buffer", () =>
            {
                var buf = new RingBuffer<int>(3);
                buf.Add(1);
                buf.Add(2);
                buf.Clear();
                if (buf.Count != 0) return "Clear() should reset Count to 0";
                buf.Add(99);
                if (buf.Count != 1 || buf[0] != 99) return "buffer didn't behave correctly after Clear()";
                return null;
            });

            results.Check("RingBuffer: constructor rejects a non-positive capacity", () =>
            {
                try { _ = new RingBuffer<int>(0); return "expected ArgumentOutOfRangeException for capacity=0"; }
                catch (ArgumentOutOfRangeException) { }
                try { _ = new RingBuffer<int>(-1); return "expected ArgumentOutOfRangeException for capacity=-1"; }
                catch (ArgumentOutOfRangeException) { }
                return null;
            });
        }
    }
}
