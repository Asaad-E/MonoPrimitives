using System;
using System.Collections.Generic;

namespace MonoPrimitives.Tests
{
    /// <summary>Collects pass/fail outcomes for the shape-generation smoke tests.</summary>
    internal sealed class TestResults
    {
        private readonly List<(string Name, bool Passed, string Message)> _entries = new();

        /// <summary>Runs <paramref name="body"/>, recording a failure if it throws or returns false.</summary>
        public void Check(string name, Func<string> body)
        {
            try
            {
                string failureReason = body();
                _entries.Add((name, failureReason is null, failureReason ?? ""));
            }
            catch (Exception ex)
            {
                _entries.Add((name, false, $"threw {ex.GetType().Name}: {ex.Message}"));
            }
        }

        public int Failures { get; private set; }

        /// <summary>Prints every result and returns true if all passed.</summary>
        public bool PrintSummary()
        {
            int passed = 0;
            foreach (var (name, ok, message) in _entries)
            {
                Console.WriteLine(ok ? $"  PASS  {name}" : $"  FAIL  {name} — {message}");
                if (ok) passed++;
            }

            Failures = _entries.Count - passed;
            Console.WriteLine();
            Console.WriteLine($"{passed}/{_entries.Count} passed.");
            return Failures == 0;
        }
    }
}
