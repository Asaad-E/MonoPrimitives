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
        private readonly int _precision;

        /// <summary>
        /// Starts timing, printed as <paramref name="label"/> once <see cref="Dispose"/> runs.
        /// <paramref name="separator"/> prints a divider line first, for marking the start of a new
        /// group of timers (e.g. once per frame). <paramref name="precision"/> is the number of
        /// decimal places printed (default <c>2</c>) — raise it for a timer short enough that
        /// two decimal places round to <c>0.00</c>.
        /// </summary>
        public DebugTimer(string label, bool separator = false, int precision = 2)
        {
            if (precision < 0) throw new ArgumentOutOfRangeException(nameof(precision), "must be at least 0.");
            _label = label;
            _startTimestamp = Stopwatch.GetTimestamp();
            _separator = separator;
            _precision = precision;
        }

        /// <summary>Prints <c>[label] X.XX ms</c> (decimal places per the constructor's <c>precision</c>) for the time elapsed since construction.</summary>
        public void Dispose()
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
            if (_separator) Console.WriteLine(SeparatorLine);
            Console.WriteLine($"[{_label}] {elapsed.TotalMilliseconds.ToString("F" + _precision)} ms");
        }
    }
}
