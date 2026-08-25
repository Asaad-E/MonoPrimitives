using System;

namespace MonoPrimitives
{
    /// <summary>Names every curve <see cref="Easing"/> exposes, for picking one as data (a level/config file, a debug UI dropdown) instead of a function reference.</summary>
    public enum EasingType
    {
        /// <summary>See <see cref="Easing.Linear"/>.</summary>
        Linear,
        /// <summary>See <see cref="Easing.QuadIn"/>.</summary>
        QuadIn,
        /// <summary>See <see cref="Easing.QuadOut"/>.</summary>
        QuadOut,
        /// <summary>See <see cref="Easing.QuadInOut"/>.</summary>
        QuadInOut,
        /// <summary>See <see cref="Easing.CubicIn"/>.</summary>
        CubicIn,
        /// <summary>See <see cref="Easing.CubicOut"/>.</summary>
        CubicOut,
        /// <summary>See <see cref="Easing.CubicInOut"/>.</summary>
        CubicInOut,
        /// <summary>See <see cref="Easing.QuartIn"/>.</summary>
        QuartIn,
        /// <summary>See <see cref="Easing.QuartOut"/>.</summary>
        QuartOut,
        /// <summary>See <see cref="Easing.QuartInOut"/>.</summary>
        QuartInOut,
        /// <summary>See <see cref="Easing.QuintIn"/>.</summary>
        QuintIn,
        /// <summary>See <see cref="Easing.QuintOut"/>.</summary>
        QuintOut,
        /// <summary>See <see cref="Easing.QuintInOut"/>.</summary>
        QuintInOut,
        /// <summary>See <see cref="Easing.CircIn"/>.</summary>
        CircIn,
        /// <summary>See <see cref="Easing.CircOut"/>.</summary>
        CircOut,
        /// <summary>See <see cref="Easing.CircInOut"/>.</summary>
        CircInOut,
        /// <summary>See <see cref="Easing.ExpoIn"/>.</summary>
        ExpoIn,
        /// <summary>See <see cref="Easing.ExpoOut"/>.</summary>
        ExpoOut,
        /// <summary>See <see cref="Easing.ExpoInOut"/>.</summary>
        ExpoInOut,
        /// <summary>See <see cref="Easing.SineIn"/>.</summary>
        SineIn,
        /// <summary>See <see cref="Easing.SineOut"/>.</summary>
        SineOut,
        /// <summary>See <see cref="Easing.SineInOut"/>.</summary>
        SineInOut,
        /// <summary>See <see cref="Easing.BackIn"/>.</summary>
        BackIn,
        /// <summary>See <see cref="Easing.BackOut"/>.</summary>
        BackOut,
        /// <summary>See <see cref="Easing.BackInOut"/>.</summary>
        BackInOut,
        /// <summary>See <see cref="Easing.BounceIn"/>.</summary>
        BounceIn,
        /// <summary>See <see cref="Easing.BounceOut"/>.</summary>
        BounceOut,
        /// <summary>See <see cref="Easing.BounceInOut"/>.</summary>
        BounceInOut,
        /// <summary>See <see cref="Easing.ElasticIn"/>.</summary>
        ElasticIn,
        /// <summary>See <see cref="Easing.ElasticOut"/>.</summary>
        ElasticOut,
        /// <summary>See <see cref="Easing.ElasticInOut"/>.</summary>
        ElasticInOut,
    }

    /// <summary>
    /// Classic 0→1 tweening curves for one-shot animations with a known duration — a menu
    /// sliding in, an object scaling up, a color fading out.
    /// </summary>
    /// <remarks>
    /// Complements <c>Camera2D</c>/<c>Camera3D</c>'s own <c>SmoothDamp</c> (a physical spring,
    /// good for open-ended following/zoom instead). Typical use:
    /// <c>float eased = Easing.CubicOut(Math.Clamp(elapsed / duration, 0f, 1f));</c>
    /// </remarks>
    public static class Easing
    {
        /// <summary>Evaluates the curve named by <paramref name="type"/> at <paramref name="t"/> — dispatches to the matching method below.</summary>
        /// <remarks>For picking a curve as data (see <see cref="EasingType"/>) rather than calling it directly. Prefer calling the named method (<see cref="CubicOut"/>, etc.) instead when the curve is already known at compile time — this adds a switch dispatch on top.</remarks>
        public static float Evaluate(EasingType type, float t) => type switch
        {
            EasingType.Linear => Linear(t),
            EasingType.QuadIn => QuadIn(t),
            EasingType.QuadOut => QuadOut(t),
            EasingType.QuadInOut => QuadInOut(t),
            EasingType.CubicIn => CubicIn(t),
            EasingType.CubicOut => CubicOut(t),
            EasingType.CubicInOut => CubicInOut(t),
            EasingType.QuartIn => QuartIn(t),
            EasingType.QuartOut => QuartOut(t),
            EasingType.QuartInOut => QuartInOut(t),
            EasingType.QuintIn => QuintIn(t),
            EasingType.QuintOut => QuintOut(t),
            EasingType.QuintInOut => QuintInOut(t),
            EasingType.CircIn => CircIn(t),
            EasingType.CircOut => CircOut(t),
            EasingType.CircInOut => CircInOut(t),
            EasingType.ExpoIn => ExpoIn(t),
            EasingType.ExpoOut => ExpoOut(t),
            EasingType.ExpoInOut => ExpoInOut(t),
            EasingType.SineIn => SineIn(t),
            EasingType.SineOut => SineOut(t),
            EasingType.SineInOut => SineInOut(t),
            EasingType.BackIn => BackIn(t),
            EasingType.BackOut => BackOut(t),
            EasingType.BackInOut => BackInOut(t),
            EasingType.BounceIn => BounceIn(t),
            EasingType.BounceOut => BounceOut(t),
            EasingType.BounceInOut => BounceInOut(t),
            EasingType.ElasticIn => ElasticIn(t),
            EasingType.ElasticOut => ElasticOut(t),
            EasingType.ElasticInOut => ElasticInOut(t),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };

        /// <summary>No easing — passes <paramref name="t"/> through unchanged. The baseline every other curve bends away from.</summary>
        public static float Linear(float t) => t;

        /// <summary>Quadratic ease-in: slow start, accelerating toward the end.</summary>
        public static float QuadIn(float t) => t * t;
        /// <summary>Quadratic ease-out: fast start, decelerating into the end — the everyday default for "settle into place."</summary>
        public static float QuadOut(float t) => t * (2f - t);
        /// <summary>Quadratic ease-in-out: slow start and end, faster through the middle.</summary>
        public static float QuadInOut(float t) => t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;

        /// <summary>Cubic ease-in — like <see cref="QuadIn"/> but with a more pronounced slow start.</summary>
        public static float CubicIn(float t) => t * t * t;
        /// <summary>Cubic ease-out — like <see cref="QuadOut"/> but with a more pronounced slow finish.</summary>
        public static float CubicOut(float t) { float f = t - 1f; return f * f * f + 1f; }
        /// <summary>Cubic ease-in-out — like <see cref="QuadInOut"/> but with a stronger curve at both ends.</summary>
        public static float CubicInOut(float t) => t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) * 0.5f;

        /// <summary>Quartic ease-in — even more pronounced slow start than <see cref="CubicIn"/>.</summary>
        public static float QuartIn(float t) => t * t * t * t;
        /// <summary>Quartic ease-out — even more pronounced slow finish than <see cref="CubicOut"/>.</summary>
        public static float QuartOut(float t) { float f = t - 1f; return 1f - f * f * f * f; }
        /// <summary>Quartic ease-in-out — a stronger curve at both ends than <see cref="CubicInOut"/>.</summary>
        public static float QuartInOut(float t) { float f = t - 1f; return t < 0.5f ? 8f * t * t * t * t : 1f - 8f * f * f * f * f; }

        /// <summary>Quintic ease-in — an even more pronounced slow start than <see cref="QuartIn"/>, the strongest polynomial curve here.</summary>
        public static float QuintIn(float t) => t * t * t * t * t;
        /// <summary>Quintic ease-out — an even more pronounced slow finish than <see cref="QuartOut"/>.</summary>
        public static float QuintOut(float t) { float f = t - 1f; return 1f + f * f * f * f * f; }
        /// <summary>Quintic ease-in-out — a stronger curve at both ends than <see cref="QuartInOut"/>.</summary>
        public static float QuintInOut(float t) => t < 0.5f ? 16f * t * t * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 5f) * 0.5f;

        /// <summary>Circular ease-in: a quarter-circle arc — starts slower than <see cref="QuadIn"/> but with a distinctly rounder, less "mathematical" feel than the polynomial curves.</summary>
        public static float CircIn(float t) => 1f - MathF.Sqrt(1f - t * t);
        /// <summary>Circular ease-out: the mirror quarter-circle arc — a rounder alternative to <see cref="QuadOut"/>.</summary>
        public static float CircOut(float t) { float f = t - 1f; return MathF.Sqrt(1f - f * f); }
        /// <summary>Circular ease-in-out — <see cref="CircIn"/> and <see cref="CircOut"/> combined into one motion.</summary>
        public static float CircInOut(float t)
        {
            if (t < 0.5f) { float f = 2f * t; return (1f - MathF.Sqrt(1f - f * f)) * 0.5f; }
            else { float f = -2f * t + 2f; return (MathF.Sqrt(1f - f * f) + 1f) * 0.5f; }
        }

        /// <summary>Exponential ease-in: barely moves at first, then accelerates sharply right at the end — the most dramatic slow-start curve here.</summary>
        public static float ExpoIn(float t) => t <= 0f ? 0f : MathF.Pow(2f, 10f * (t - 1f));
        /// <summary>Exponential ease-out: a sharp initial burst that quickly decelerates and coasts to the end — the most dramatic fast-start curve here.</summary>
        public static float ExpoOut(float t) => t >= 1f ? 1f : 1f - MathF.Pow(2f, -10f * t);
        /// <summary>Exponential ease-in-out — <see cref="ExpoIn"/> and <see cref="ExpoOut"/> combined into one motion.</summary>
        public static float ExpoInOut(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t < 0.5f ? MathF.Pow(2f, 20f * t - 10f) * 0.5f : (2f - MathF.Pow(2f, -20f * t + 10f)) * 0.5f;
        }

        /// <summary>Sine ease-in: a gentle, smooth slow start — softer than <see cref="QuadIn"/>.</summary>
        public static float SineIn(float t) => 1f - MathF.Cos(t * MathF.PI * 0.5f);
        /// <summary>Sine ease-out: a gentle, smooth slow finish — softer than <see cref="QuadOut"/>.</summary>
        public static float SineOut(float t) => MathF.Sin(t * MathF.PI * 0.5f);
        /// <summary>Sine ease-in-out: the gentlest, smoothest curve here — good as a default when you want easing to be felt rather than noticed.</summary>
        public static float SineInOut(float t) => -(MathF.Cos(MathF.PI * t) - 1f) * 0.5f;

        /// <summary>Eases in with a slight pull backward first — a small "wind-up" before moving.</summary>
        public static float BackIn(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }

        /// <summary>Eases out with a slight overshoot past the target before settling — a common "pop" for something appearing.</summary>
        public static float BackOut(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float f = t - 1f;
            return 1f + c3 * f * f * f + c1 * f * f;
        }

        /// <summary>Wind-up before moving, then a slight overshoot past the target before settling — <see cref="BackIn"/> and <see cref="BackOut"/> combined into one motion.</summary>
        public static float BackInOut(float t)
        {
            const float c1 = 1.70158f, c2 = c1 * 1.525f;
            float t2 = t * 2f;
            if (t < 0.5f) return t2 * t2 * ((c2 + 1f) * t2 - c2) * 0.5f;
            float f = t2 - 2f;
            return (f * f * ((c2 + 1f) * f + c2) + 2f) * 0.5f;
        }

        /// <summary>Starts with a few decaying bounces before committing to the motion — the mirror image of <see cref="BounceOut"/>, good for something launching off.</summary>
        public static float BounceIn(float t) => 1f - BounceOut(1f - t);

        /// <summary>Settles with a few decaying bounces — good for something landing.</summary>
        public static float BounceOut(float t)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }

        /// <summary>Bounces in, then bounces out — <see cref="BounceIn"/> and <see cref="BounceOut"/> combined into one motion.</summary>
        public static float BounceInOut(float t)
            => t < 0.5f ? (1f - BounceOut(1f - 2f * t)) * 0.5f : (1f + BounceOut(2f * t - 1f)) * 0.5f;

        /// <summary>Overshoots and oscillates before committing to the motion, like a spring pulled taut — good for something launching off with a bit of character.</summary>
        public static float ElasticIn(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c4 = 2f * MathF.PI / 3f;
            return -MathF.Pow(2f, 10f * t - 10f) * MathF.Sin((t * 10f - 10.75f) * c4);
        }

        /// <summary>Overshoots and oscillates before settling, like a spring — good for something snapping into place with a bit of character.</summary>
        public static float ElasticOut(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c4 = 2f * MathF.PI / 3f;
            return MathF.Pow(2f, -10f * t) * MathF.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        /// <summary>Oscillates going in, then again settling out — <see cref="ElasticIn"/> and <see cref="ElasticOut"/> combined into one motion.</summary>
        public static float ElasticInOut(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c5 = 2f * MathF.PI / 4.5f;
            return t < 0.5f
                ? -(MathF.Pow(2f, 20f * t - 10f) * MathF.Sin((20f * t - 11.125f) * c5)) * 0.5f
                : MathF.Pow(2f, -20f * t + 10f) * MathF.Sin((20f * t - 11.125f) * c5) * 0.5f + 1f;
        }
    }
}
