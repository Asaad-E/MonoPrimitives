using System;
using System.Diagnostics;

namespace MonoPrimitives
{
    /// <summary>
    /// Prints how long a <c>using</c> block took to <see cref="Console"/>, on <see cref="Dispose"/> —
    /// a quick "why is this slow" check, not a profiler.
    /// </summary>
    public readonly struct DebugTimer : IDisposable
    {
        private const string SeparatorLine = "------------------------------";

        private readonly string _label;
        private readonly long _startTimestamp;
        private readonly bool _separator;

        /// <summary>
        /// Starts timing, printed as <paramref name="label"/> once <see cref="Dispose"/> runs.
        /// <paramref name="separator"/> prints a divider line first, for marking the start of a new
        /// group of timers (e.g. once per frame).
        /// </summary>
        public DebugTimer(string label, bool separator = false)
        {
            _label = label;
            _startTimestamp = Stopwatch.GetTimestamp();
            _separator = separator;
        }

        /// <summary>Prints <c>[label] X.XX ms</c> for the time elapsed since construction.</summary>
        public void Dispose()
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
            if (_separator) Console.WriteLine(SeparatorLine);
            Console.WriteLine($"[{_label}] {elapsed.TotalMilliseconds:F2} ms");
        }
    }
}
