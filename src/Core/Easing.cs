using System;

namespace MonoPrimitives
{
    /// <summary>
    /// Classic 0→1 tweening curves for one-shot animations with a known duration — a menu
    /// sliding in, an object scaling up, a color fading out. Complements <c>Camera2D</c>/
    /// <c>Camera3D</c>'s own <c>SmoothDamp</c> (a physical spring, good for open-ended
    /// following/zoom instead). Typical use:
    /// <c>float eased = Easing.CubicOut(Math.Clamp(elapsed / duration, 0f, 1f));</c>
    /// </summary>
    public static class Easing
    {
        public static float Linear(float t) => t;

        public static float QuadIn(float t) => t * t;
        public static float QuadOut(float t) => t * (2f - t);
        public static float QuadInOut(float t) => t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;

        public static float CubicIn(float t) => t * t * t;
        public static float CubicOut(float t) { float f = t - 1f; return f * f * f + 1f; }
        public static float CubicInOut(float t) => t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) * 0.5f;

        public static float QuartIn(float t) => t * t * t * t;
        public static float QuartOut(float t) { float f = t - 1f; return 1f - f * f * f * f; }
        public static float QuartInOut(float t) { float f = t - 1f; return t < 0.5f ? 8f * t * t * t * t : 1f - 8f * f * f * f * f; }

        public static float ExpoIn(float t) => t <= 0f ? 0f : MathF.Pow(2f, 10f * (t - 1f));
        public static float ExpoOut(float t) => t >= 1f ? 1f : 1f - MathF.Pow(2f, -10f * t);
        public static float ExpoInOut(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t < 0.5f ? MathF.Pow(2f, 20f * t - 10f) * 0.5f : (2f - MathF.Pow(2f, -20f * t + 10f)) * 0.5f;
        }

        public static float SineIn(float t) => 1f - MathF.Cos(t * MathF.PI * 0.5f);
        public static float SineOut(float t) => MathF.Sin(t * MathF.PI * 0.5f);
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
