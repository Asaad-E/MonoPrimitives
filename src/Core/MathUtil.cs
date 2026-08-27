using System;
using Microsoft.Xna.Framework;

namespace MonoPrimitives
{
    /// <summary>Scalar math helpers confirmed missing from MonoGame's own <see cref="MathHelper"/>.</summary>
    public static class MathUtil
    {
        /// <summary>Remaps <paramref name="value"/> from the <paramref name="fromMin"/>-<paramref name="fromMax"/> range onto <paramref name="toMin"/>-<paramref name="toMax"/>, linearly. Not clamped -- a value outside the source range extrapolates past the target range.</summary>
        public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax) =>
            toMin + (value - fromMin) / (fromMax - fromMin) * (toMax - toMin);

        /// <summary>Signed shortest-path difference from <paramref name="current"/> to <paramref name="target"/>, both in radians. Result is in (-pi, pi].</summary>
        public static float DeltaAngle(float current, float target) => MathHelper.WrapAngle(target - current);

        /// <summary>Interpolates from angle <paramref name="a"/> to <paramref name="b"/> (radians) by <paramref name="t"/>, taking the shorter direction around the circle instead of <see cref="MathHelper.Lerp"/>'s straight numeric path.</summary>
        /// <remarks>Not clamped to <paramref name="a"/>/<paramref name="b"/> -- <paramref name="t"/> outside [0,1] extrapolates past <paramref name="b"/> in the same rotational direction.</remarks>
        public static float LerpAngle(float a, float b, float t) => a + DeltaAngle(a, b) * t;

        /// <summary>Bounces <paramref name="t"/> back and forth between 0 and <paramref name="length"/> as it increases, instead of wrapping like <see cref="MathHelper.WrapAngle"/> does for angles.</summary>
        /// <remarks>Matches Unity's <c>Mathf.PingPong</c> -- useful for a value that should oscillate (a light's brightness, a back-and-forth patrol offset) driven by an ever-increasing time/distance input.</remarks>
        public static float PingPong(float t, float length)
        {
            if (length <= 0f) return 0f;
            float doubled = length * 2f;
            float wrapped = t - doubled * MathF.Floor(t / doubled); // Repeat(t, doubled), correct for negative t
            return length - MathF.Abs(wrapped - length);
        }
    }
}
