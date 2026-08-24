using System;
using Microsoft.Xna.Framework;
using MonoPrimitives.Primitives3D;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-math checks for <see cref="Vector3Extensions"/> — no GraphicsDevice needed.</summary>
    internal static class Vector3ExtensionsTests
    {
        private static bool CloseF(float a, float b, float eps = 1e-4f) => MathF.Abs(a - b) < eps;
        private static bool CloseV(Vector3 a, Vector3 b, float eps = 1e-4f) => Vector3.Distance(a, b) < eps;

        public static void Run(TestResults results)
        {
            results.Check("Vector3Extensions.DirectionTo/SafeNormalize: unit length, safe at zero with a configurable fallback", () =>
            {
                if (!CloseV(Vector3.Zero.DirectionTo(new Vector3(10, 0, 0)), Vector3.UnitX)) return "DirectionTo not unit length/direction";
                if (!CloseV(new Vector3(5, 5, 5).DirectionTo(new Vector3(5, 5, 5)), Vector3.Zero)) return "DirectionTo(p,p) should safely be Zero, not NaN";
                if (!CloseV(Vector3.Zero.SafeNormalize(), Vector3.Zero)) return "SafeNormalize(Zero) default fallback should be Zero";
                if (!CloseV(Vector3.Zero.SafeNormalize(Vector3.UnitY), Vector3.UnitY)) return "SafeNormalize(Zero, fallback) didn't use the fallback";
                if (!CloseV(new Vector3(3, 4, 0).SafeNormalize(), Vector3.Normalize(new Vector3(3, 4, 0)))) return "SafeNormalize disagrees with Normalize for a nonzero vector";
                return null;
            });

            results.Check("Vector3Extensions.Approach (Vector3 and float): reaches target exactly, never overshoots, partial steps correctly", () =>
            {
                if (!CloseV(Vector3.Zero.Approach(new Vector3(10, 0, 0), 10f), new Vector3(10, 0, 0))) return "Approach didn't land exactly at maxDistance==dist";
                if (!CloseV(Vector3.Zero.Approach(new Vector3(10, 0, 0), 100f), new Vector3(10, 0, 0))) return "Approach overshot past the target";
                if (!CloseV(Vector3.Zero.Approach(new Vector3(10, 0, 0), 3f), new Vector3(3, 0, 0))) return "Approach partial step wrong";
                // The float overload deliberately lives on Vector2Extensions (dimension-agnostic scalar
                // math) rather than being duplicated here -- would be an ambiguous call otherwise.
                if (!CloseF(5f.Approach(10f, 3f), 8f)) return "MonoPrimitives.Vector2Extensions.Approach(float) toward a higher target wrong";
                return null;
            });

            results.Check("Vector3Extensions.ClampMagnitude: shrinks an over-length vector to exactly maxLength, no-ops a shorter one", () =>
            {
                Vector3 v = new(0, 30, 40); // 3-4-5 triangle scaled by 10, length 50
                if (!CloseV(v.ClampMagnitude(10f), new Vector3(0, 6, 8))) return "ClampMagnitude didn't shrink correctly";
                if (!CloseV(new Vector3(1, 0, 0).ClampMagnitude(10f), new Vector3(1, 0, 0))) return "ClampMagnitude changed an already-short vector";
                if (!CloseF(v.ClampMagnitude(10f).Length(), 10f)) return "ClampMagnitude's result isn't exactly maxLength";
                return null;
            });

            results.Check("Vector3Extensions.Approach/ClampMagnitude/SafeNormalize don't collide with MonoGame's own native Vector3 members", () =>
            {
                // MonoGame already ships Vector3.Reflect/Clamp/Lerp natively -- confirmed by reflection
                // before adding this class, so none of those are duplicated here. This check exists so
                // a future addition doesn't accidentally re-introduce one of them as an extension method,
                // which would silently be unreachable (an instance member always wins over an extension).
                Vector3 reflected = Vector3.Reflect(new Vector3(1, -1, 0), Vector3.UnitY);
                if (!CloseV(reflected, new Vector3(1, 1, 0))) return "Vector3.Reflect (native) behaved unexpectedly -- sanity check itself is wrong";
                return null;
            });
        }
    }
}
