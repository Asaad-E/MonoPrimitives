using System;
using System.Collections.Generic;
using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-math checks for <see cref="Easing"/> — no GraphicsDevice needed.</summary>
    internal static class EasingTests
    {
        // Every public easing function, by name -- so a boundary/range check can be written once
        // and applied to all of them, instead of 31 near-identical individual checks.
        private static readonly (string Name, Func<float, float> Fn)[] All =
        {
            ("Linear", Easing.Linear),
            ("QuadIn", Easing.QuadIn), ("QuadOut", Easing.QuadOut), ("QuadInOut", Easing.QuadInOut),
            ("CubicIn", Easing.CubicIn), ("CubicOut", Easing.CubicOut), ("CubicInOut", Easing.CubicInOut),
            ("QuartIn", Easing.QuartIn), ("QuartOut", Easing.QuartOut), ("QuartInOut", Easing.QuartInOut),
            ("QuintIn", Easing.QuintIn), ("QuintOut", Easing.QuintOut), ("QuintInOut", Easing.QuintInOut),
            ("ExpoIn", Easing.ExpoIn), ("ExpoOut", Easing.ExpoOut), ("ExpoInOut", Easing.ExpoInOut),
            ("SineIn", Easing.SineIn), ("SineOut", Easing.SineOut), ("SineInOut", Easing.SineInOut),
            ("CircIn", Easing.CircIn), ("CircOut", Easing.CircOut), ("CircInOut", Easing.CircInOut),
            ("BackIn", Easing.BackIn), ("BackOut", Easing.BackOut), ("BackInOut", Easing.BackInOut),
            ("BounceIn", Easing.BounceIn), ("BounceOut", Easing.BounceOut), ("BounceInOut", Easing.BounceInOut),
            ("ElasticIn", Easing.ElasticIn), ("ElasticOut", Easing.ElasticOut), ("ElasticInOut", Easing.ElasticInOut),
        };

        private static readonly (string Name, Func<float, float> Fn)[] InOut =
        {
            ("QuadInOut", Easing.QuadInOut), ("CubicInOut", Easing.CubicInOut), ("QuartInOut", Easing.QuartInOut),
            ("QuintInOut", Easing.QuintInOut), ("ExpoInOut", Easing.ExpoInOut), ("SineInOut", Easing.SineInOut),
            ("CircInOut", Easing.CircInOut), ("BackInOut", Easing.BackInOut), ("BounceInOut", Easing.BounceInOut),
            ("ElasticInOut", Easing.ElasticInOut),
        };

        // The families with a smooth, monotonic curve -- Back/Bounce/Elastic are deliberately
        // NOT here, since overshoot/oscillation is their entire defining character.
        private static readonly (string Name, Func<float, float> Fn)[] Monotonic =
        {
            ("Linear", Easing.Linear),
            ("QuadIn", Easing.QuadIn), ("QuadOut", Easing.QuadOut), ("QuadInOut", Easing.QuadInOut),
            ("CubicIn", Easing.CubicIn), ("CubicOut", Easing.CubicOut), ("CubicInOut", Easing.CubicInOut),
            ("QuartIn", Easing.QuartIn), ("QuartOut", Easing.QuartOut), ("QuartInOut", Easing.QuartInOut),
            ("QuintIn", Easing.QuintIn), ("QuintOut", Easing.QuintOut), ("QuintInOut", Easing.QuintInOut),
            ("ExpoIn", Easing.ExpoIn), ("ExpoOut", Easing.ExpoOut), ("ExpoInOut", Easing.ExpoInOut),
            ("SineIn", Easing.SineIn), ("SineOut", Easing.SineOut), ("SineInOut", Easing.SineInOut),
            ("CircIn", Easing.CircIn), ("CircOut", Easing.CircOut), ("CircInOut", Easing.CircInOut),
        };

        // The 7 smooth (non-Back/Bounce/Elastic) families' In/Out pairs -- an ease-in should lag
        // behind linear pace early on, an ease-out should lead it.
        private static readonly (string Family, Func<float, float> In, Func<float, float> Out)[] SmoothPairs =
        {
            ("Quad", Easing.QuadIn, Easing.QuadOut),
            ("Cubic", Easing.CubicIn, Easing.CubicOut),
            ("Quart", Easing.QuartIn, Easing.QuartOut),
            ("Quint", Easing.QuintIn, Easing.QuintOut),
            ("Expo", Easing.ExpoIn, Easing.ExpoOut),
            ("Sine", Easing.SineIn, Easing.SineOut),
            ("Circ", Easing.CircIn, Easing.CircOut),
        };

        public static void Run(TestResults results)
        {
            results.Check("Every easing function starts at f(0)=0 and ends at f(1)=1", () =>
            {
                var failures = new List<string>();
                foreach (var (name, fn) in All)
                {
                    float at0 = fn(0f), at1 = fn(1f);
                    if (MathF.Abs(at0) > 1e-4f) failures.Add($"{name}(0)={at0:F4}");
                    if (MathF.Abs(at1 - 1f) > 1e-4f) failures.Add($"{name}(1)={at1:F4}");
                }
                return failures.Count == 0 ? null : string.Join(", ", failures);
            });

            results.Check("Every InOut variant passes through exactly f(0.5)=0.5 (the In/Out halves meet at the midpoint)", () =>
            {
                var failures = new List<string>();
                foreach (var (name, fn) in InOut)
                {
                    float mid = fn(0.5f);
                    if (MathF.Abs(mid - 0.5f) > 1e-4f) failures.Add($"{name}(0.5)={mid:F4}");
                }
                return failures.Count == 0 ? null : string.Join(", ", failures);
            });

            results.Check("Smooth (non-Back/Bounce/Elastic) curves are monotonically non-decreasing over [0,1]", () =>
            {
                var failures = new List<string>();
                const int samples = 200;
                foreach (var (name, fn) in Monotonic)
                {
                    float prev = fn(0f);
                    for (int i = 1; i <= samples; i++)
                    {
                        float t = i / (float)samples;
                        float v = fn(t);
                        if (v < prev - 1e-4f) { failures.Add($"{name} decreased at t={t:F3} ({v:F4} < {prev:F4})"); break; }
                        prev = v;
                    }
                }
                return failures.Count == 0 ? null : string.Join(", ", failures);
            });

            results.Check("Ease-in lags behind linear pace, ease-out leads it, for every smooth family", () =>
            {
                var failures = new List<string>();
                foreach (var (family, easeIn, easeOut) in SmoothPairs)
                {
                    float atIn = easeIn(0.25f), atOut = easeOut(0.25f);
                    if (atIn >= 0.25f) failures.Add($"{family}In(0.25)={atIn:F4}, expected < 0.25 (behind linear pace)");
                    if (atOut <= 0.25f) failures.Add($"{family}Out(0.25)={atOut:F4}, expected > 0.25 (ahead of linear pace)");
                }
                return failures.Count == 0 ? null : string.Join(", ", failures);
            });

            results.Check("BackOut overshoots past 1, BackIn dips below 0 -- the defining 'wind-up'/'pop' character", () =>
            {
                float maxOut = float.MinValue, minIn = float.MaxValue;
                for (int i = 1; i < 100; i++)
                {
                    float t = i / 100f;
                    maxOut = MathF.Max(maxOut, Easing.BackOut(t));
                    minIn = MathF.Min(minIn, Easing.BackIn(t));
                }
                if (maxOut <= 1f) return $"BackOut never exceeded 1 (max was {maxOut:F4}), expected an overshoot";
                if (minIn >= 0f) return $"BackIn never went below 0 (min was {minIn:F4}), expected a wind-up dip";
                return null;
            });

            results.Check("ElasticOut/ElasticIn oscillate outside [0,1] -- the defining spring character", () =>
            {
                float maxOut = float.MinValue, minIn = float.MaxValue;
                for (int i = 1; i < 100; i++)
                {
                    float t = i / 100f;
                    maxOut = MathF.Max(maxOut, Easing.ElasticOut(t));
                    minIn = MathF.Min(minIn, Easing.ElasticIn(t));
                }
                if (maxOut <= 1f) return $"ElasticOut never exceeded 1 (max was {maxOut:F4}), expected oscillation past the target";
                if (minIn >= 0f) return $"ElasticIn never went below 0 (min was {minIn:F4}), expected oscillation before committing";
                return null;
            });

            results.Check("BounceOut/BounceIn stay close to [0,1] (bounces settle without wild overshoot)", () =>
            {
                float minOut = float.MaxValue, maxOut = float.MinValue, minIn = float.MaxValue, maxIn = float.MinValue;
                for (int i = 0; i <= 100; i++)
                {
                    float t = i / 100f;
                    float o = Easing.BounceOut(t), n = Easing.BounceIn(t);
                    minOut = MathF.Min(minOut, o); maxOut = MathF.Max(maxOut, o);
                    minIn = MathF.Min(minIn, n); maxIn = MathF.Max(maxIn, n);
                }
                bool ok = minOut >= -0.05f && maxOut <= 1.05f && minIn >= -0.05f && maxIn <= 1.05f;
                return ok ? null : $"BounceOut range [{minOut:F3},{maxOut:F3}], BounceIn range [{minIn:F3},{maxIn:F3}], expected roughly within [0,1]";
            });

            results.Check("Every family's In/Out pair satisfies In(t) == 1 - Out(1-t) (how an ease-out is always derived from its ease-in)", () =>
            {
                var failures = new List<string>();
                (string Name, Func<float, float> In, Func<float, float> Out)[] pairs =
                {
                    ("Quad", Easing.QuadIn, Easing.QuadOut),
                    ("Cubic", Easing.CubicIn, Easing.CubicOut),
                    ("Quart", Easing.QuartIn, Easing.QuartOut),
                    ("Quint", Easing.QuintIn, Easing.QuintOut),
                    ("Expo", Easing.ExpoIn, Easing.ExpoOut),
                    ("Sine", Easing.SineIn, Easing.SineOut),
                    ("Circ", Easing.CircIn, Easing.CircOut),
                    ("Back", Easing.BackIn, Easing.BackOut),
                    ("Bounce", Easing.BounceIn, Easing.BounceOut),
                    ("Elastic", Easing.ElasticIn, Easing.ElasticOut),
                };
                foreach (var (name, inFn, outFn) in pairs)
                {
                    for (int i = 0; i <= 10; i++)
                    {
                        float t = i / 10f;
                        float lhs = inFn(t), rhs = 1f - outFn(1f - t);
                        if (MathF.Abs(lhs - rhs) > 1e-3f)
                        {
                            failures.Add($"{name}In({t:F1})={lhs:F4} != 1-{name}Out({1f - t:F1})={rhs:F4}");
                            break;
                        }
                    }
                }
                return failures.Count == 0 ? null : string.Join(", ", failures);
            });
        }
    }
}
