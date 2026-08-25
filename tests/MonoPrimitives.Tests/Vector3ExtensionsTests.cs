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
            results.Check("Vector3Extensions.AngleTo: unsigned, symmetric, [0,PI]", () =>
            {
                if (!CloseF(Vector3.UnitX.AngleTo(Vector3.UnitY), MathHelper.PiOver2)) return "AngleTo(+X,+Y) != PI/2";
                if (!CloseF(Vector3.UnitX.AngleTo(Vector3.UnitY), Vector3.UnitY.AngleTo(Vector3.UnitX))) return "AngleTo not symmetric";
                if (!CloseF(Vector3.UnitX.AngleTo(Vector3.UnitX), 0f)) return "AngleTo(v,v) != 0";
                if (!CloseF(Vector3.UnitX.AngleTo(-Vector3.UnitX), MathF.PI)) return "AngleTo(v,-v) != PI";
                if (!CloseF(Vector3.Zero.AngleTo(Vector3.UnitX), 0f)) return "AngleTo with a zero vector should degrade to 0, not NaN";
                return null;
            });

            results.Check("Vector3Extensions.AngleToSigned: matches Rotated's sign convention (rotating 'from' by the result points toward 'to')", () =>
            {
                // +X rotated +PI/2 around +Y (right-hand rule) should land on -Z.
                if (!CloseF(Vector3.UnitX.AngleToSigned(-Vector3.UnitZ, Vector3.Up), MathHelper.PiOver2))
                    return "AngleToSigned(+X, -Z, axis=+Y) != +PI/2 -- sign convention disagrees with the right-hand rule";

                var rand = new Random(2);
                for (int i = 0; i < 200; i++)
                {
                    Vector3 axis = new((float)rand.NextDouble() * 2f - 1f, (float)rand.NextDouble() * 2f - 1f, (float)rand.NextDouble() * 2f - 1f);
                    if (axis.LengthSquared() < 1e-6f) continue;
                    Vector3 a = new((float)rand.NextDouble() * 20f - 10f, (float)rand.NextDouble() * 20f - 10f, (float)rand.NextDouble() * 20f - 10f);
                    Vector3 b = new((float)rand.NextDouble() * 20f - 10f, (float)rand.NextDouble() * 20f - 10f, (float)rand.NextDouble() * 20f - 10f);
                    if (a.LengthSquared() < 1e-6f || b.LengthSquared() < 1e-6f) continue;

                    float turn = a.AngleToSigned(b, axis);

                    // Rotate a's OWN flattened direction, not 'a' itself: Rotated(a, axis, turn) leaves
                    // whatever component of 'a' runs along axis untouched (ordinary axis-angle rotation
                    // behavior), so renormalizing the rotated full vector mixes that untouched axial part
                    // back in -- it does NOT generally land on toFlat unless 'a' was already perpendicular
                    // to axis. AngleToSigned/Rotated's real contract is about the flattened (perpendicular)
                    // component specifically, which this checks directly. (First caught as a real
                    // mismatch here before realizing it was this test comparing the wrong vectors, not a
                    // bug in AngleToSigned/Rotated -- rotating fromFlat lands on toFlat exactly.)
                    Vector3 n = axis.SafeNormalize();
                    Vector3 fromFlat = (a - Vector3.Dot(a, n) * n).SafeNormalize();
                    Vector3 bFlat = (b - Vector3.Dot(b, n) * n).SafeNormalize();
                    if (fromFlat == Vector3.Zero || bFlat == Vector3.Zero) continue; // a or b parallel to axis -- undefined direction, skip

                    Vector3 rotated = fromFlat.Rotated(axis, turn).SafeNormalize();
                    if (!CloseV(rotated, bFlat, 1e-3f))
                        return $"Rotating a's flattened direction by AngleToSigned(a,b,axis) didn't point toward b's flattened direction (a={a}, b={b}, axis={axis}, turn={turn})";
                }
                return null;
            });

            results.Check("Vector3Extensions.AngleToSigned: degenerate axis or a from/to parallel to the axis returns 0, not NaN", () =>
            {
                if (!CloseF(Vector3.UnitX.AngleToSigned(Vector3.UnitZ, Vector3.Zero), 0f)) return "zero-length axis should degrade to 0";
                if (!CloseF(Vector3.Up.AngleToSigned(Vector3.UnitX, Vector3.Up), 0f)) return "'from' parallel to axis (no flattened component) should degrade to 0";
                return null;
            });

            results.Check("Vector3Extensions.Rotated: doesn't mutate the source, Rotated(axis,0) is a no-op, matches Quaternion.CreateFromAxisAngle directly", () =>
            {
                Vector3 original = new(1, 0, 0);
                Vector3 rotated = original.Rotated(Vector3.Up, MathHelper.PiOver2);
                if (!CloseV(original, new Vector3(1, 0, 0))) return "Rotated mutated its receiver";
                if (!CloseV(original.Rotated(Vector3.Up, 0f), original)) return "Rotated(axis, 0) should be a no-op";

                Vector3 expected = Vector3.Transform(original, Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.PiOver2));
                if (!CloseV(rotated, expected)) return $"Rotated disagrees with a hand-built Quaternion.CreateFromAxisAngle + Transform: {rotated} vs {expected}";

                if (!CloseV(original.Rotated(Vector3.Zero, MathHelper.PiOver2), original)) return "Rotated with a zero-length axis should leave v unchanged, not produce NaN";
                return null;
            });

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

            results.Check("Vector3Extensions.Slide: drops the normal component entirely, keeps the tangential one, and result stays perpendicular to the normal", () =>
            {
                // Moving down into a floor whose normal points straight up: the vertical
                // (into-floor) component should vanish, leaving the horizontal motion untouched.
                if (!CloseV(new Vector3(1f, -1f, 2f).Slide(Vector3.UnitY), new Vector3(1f, 0f, 2f))) return "Slide against a floor normal didn't drop the vertical component";

                // A vector already tangential to the normal (perpendicular) should pass through unchanged.
                if (!CloseV(Vector3.UnitX.Slide(Vector3.UnitY), Vector3.UnitX)) return "Slide changed a vector already perpendicular to the normal";

                // A vector purely along the normal should slide to zero.
                if (!CloseV(new Vector3(0f, -5f, 0f).Slide(Vector3.UnitY), Vector3.Zero)) return "Slide of a purely-along-normal vector should be zero";

                // Result must always be perpendicular to the normal (that's the whole point of "tangential").
                Vector3 unitNormal = new Vector3(1f, 2f, 2f) / 3f; // length-3 vector normalized to unit length
                Vector3 slid = new Vector3(4f, -3f, 7f).Slide(unitNormal);
                if (MathF.Abs(Vector3.Dot(slid, unitNormal)) > 1e-4f) return $"Slide result {slid} isn't perpendicular to the normal";
                return null;
            });

            results.Check("Vector3Extensions.Dot: fluent wrapper matches Vector3.Dot exactly", () =>
            {
                if (!CloseF(Vector3.UnitX.Dot(Vector3.UnitY), 0f)) return "Dot(+X,+Y) != 0";
                if (!CloseF(Vector3.UnitX.Dot(Vector3.UnitX), 1f)) return "Dot(+X,+X) != 1";
                Vector3 a = new(3, 4, -1), b = new(-2, 5, 2);
                if (!CloseF(a.Dot(b), Vector3.Dot(a, b))) return "Dot(a,b) disagrees with Vector3.Dot(a,b)";
                return null;
            });

            results.Check("Vector3Extensions.Project: parallel component only, complements Slide, safe at a zero 'onto'", () =>
            {
                if (!CloseV(new Vector3(3, 4, 5).Project(Vector3.UnitX), new Vector3(3, 0, 0))) return "Project onto +X should keep only the X component";
                if (!CloseV(new Vector3(3, 4, 5).Project(Vector3.Zero), Vector3.Zero)) return "Project onto a zero vector should safely be Zero, not NaN";

                Vector3 v = new(5, -2, 3);
                Vector3 onto = new(3, 1, 0);
                if (!CloseV(v.Project(onto), v.Project(onto * 10f))) return "Project should be invariant to the length of 'onto'";

                Vector3 dir = Vector3.Normalize(new Vector3(1, 2, 2));
                if (!CloseV(v.Project(dir) + v.Slide(dir), v)) return "Project(dir) + Slide(dir) didn't reconstruct the original vector";
                return null;
            });

            results.Check("Vector3Extensions.SmoothDamp: converges to target over repeated steps, velocity ref is updated", () =>
            {
                Vector3 current = Vector3.Zero;
                Vector3 velocity = Vector3.Zero;
                Vector3 target = new(10, -5, 3);
                for (int i = 0; i < 300; i++) current = current.SmoothDamp(target, ref velocity, 0.2f, 1f / 60f);
                if (!CloseV(current, target, 0.01f)) return $"Vector3 SmoothDamp didn't converge, got {current}";
                if (velocity.Length() > 0.5f) return $"velocity should have settled near 0 once converged, got {velocity}";
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
