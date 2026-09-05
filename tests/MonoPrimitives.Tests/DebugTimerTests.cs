using System;
using System.IO;
using System.Threading;
using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Checks for <see cref="DebugTimer"/> by redirecting <see cref="Console.Out"/> and inspecting
    /// the captured text -- including a real <see cref="Thread.Sleep(int)"/> to confirm the printed
    /// duration reflects a real clock, not just "didn't throw".
    /// </summary>
    internal static class DebugTimerTests
    {
        private static string Capture(Action action)
        {
            TextWriter original = Console.Out;
            var writer = new StringWriter();
            Console.SetOut(writer);
            try { action(); }
            finally { Console.SetOut(original); }
            return writer.ToString();
        }

        public static void Run(TestResults results)
        {
            results.Check("DebugTimer: prints an explicit label and a real elapsed duration", () =>
            {
                string output = Capture(() =>
                {
                    using (new DebugTimer("MyLabel"))
                        Thread.Sleep(15);
                });

                if (!output.Contains("[MyLabel]")) return $"expected \"[MyLabel]\" in output, got: {output}";

                float ms = ParseMs(output, "[MyLabel]");
                if (ms < 10f) return $"expected roughly 15ms reported after a real 15ms sleep, got {ms:F2}ms";
                return null;
            });

            results.Check("DebugTimer: separator prints a divider line before the timing line", () =>
            {
                string withSeparator = Capture(() => { using (new DebugTimer("A", separator: true)) { } });
                string withoutSeparator = Capture(() => { using (new DebugTimer("B")) { } });

                string[] lines = withSeparator.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length != 2) return $"expected 2 lines (separator + timing) with separator:true, got {lines.Length}: {withSeparator}";
                if (!lines[0].Contains("---")) return $"expected the first line to be a divider, got: {lines[0]}";
                if (!lines[1].Contains("[A]")) return $"expected the second line to be the timing line, got: {lines[1]}";

                if (withoutSeparator.Contains("---")) return "expected no divider line when separator is false (the default)";
                return null;
            });

            results.Check("DebugTimer: does not throw when disposed without ever being used inside a using block explicitly", () =>
            {
                var timer = new DebugTimer("Unused");
                timer.Dispose();
                return null;
            });

            results.Check("DebugTimer: precision controls the printed decimal places, default 2", () =>
            {
                string defaultOutput = Capture(() => { using (new DebugTimer("C")) { } });
                int defaultDecimals = CountDecimals(defaultOutput, "[C]");
                if (defaultDecimals != 2) return $"expected 2 decimal places by default, got {defaultDecimals} in: {defaultOutput}";

                string zeroOutput = Capture(() => { using (new DebugTimer("D", precision: 0)) { } });
                if (CountDecimals(zeroOutput, "[D]") != 0) return $"expected 0 decimal places for precision:0, got: {zeroOutput}";

                string fiveOutput = Capture(() => { using (new DebugTimer("E", precision: 5)) { } });
                if (CountDecimals(fiveOutput, "[E]") != 5) return $"expected 5 decimal places for precision:5, got: {fiveOutput}";

                return null;
            });

            results.Check("DebugTimer: a negative precision throws", () =>
            {
                try { _ = new DebugTimer("F", precision: -1); return "expected ArgumentOutOfRangeException for precision:-1"; }
                catch (ArgumentOutOfRangeException) { return null; }
            });
        }

        private static int CountDecimals(string output, string label)
        {
            float _ = ParseMs(output, label); // reuse ParseMs's own label/"ms" bounds-finding, just re-slice for the decimal count
            int labelIndex = output.IndexOf(label, StringComparison.Ordinal);
            int msIndex = output.IndexOf("ms", labelIndex, StringComparison.Ordinal);
            string numberPart = output.Substring(labelIndex + label.Length, msIndex - (labelIndex + label.Length)).Trim();
            int dot = numberPart.IndexOf('.');
            return dot < 0 ? 0 : numberPart.Length - dot - 1;
        }

        private static float ParseMs(string output, string label)
        {
            int labelIndex = output.IndexOf(label, StringComparison.Ordinal);
            int msIndex = output.IndexOf("ms", labelIndex, StringComparison.Ordinal);
            string numberPart = output.Substring(labelIndex + label.Length, msIndex - (labelIndex + label.Length)).Trim();
            return float.Parse(numberPart, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
