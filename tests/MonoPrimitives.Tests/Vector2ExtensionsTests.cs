using System;
using Microsoft.Xna.Framework;
using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>Pure-math checks for <see cref="Vector2Extensions"/>/<see cref="GameTimeExtensions"/> — no GraphicsDevice needed.</summary>
    internal static class Vector2ExtensionsTests
    {
        private static bool CloseF(float a, float b, float eps = 1e-4f) => MathF.Abs(a - b) < eps;
        private static bool CloseV(Vector2 a, Vector2 b, float eps = 1e-4f) => Vector2.Distance(a, b) < eps;

        public static void Run(TestResults results)
        {
            results.Check("Angle: cardinal directions, and reconstructs the original direction via Cos/Sin at the (-PI,PI] seam", () =>
            {
                if (!CloseF(Vector2.UnitX.Angle(), 0f)) return "Angle(+X) != 0";
                if (!CloseF(Vector2.UnitY.Angle(), MathHelper.PiOver2)) return "Angle(+Y) != PI/2";
                if (!CloseF((-Vector2.UnitY).Angle(), -MathHelper.PiOver2)) return "Angle(-Y) != -PI/2";
                if (!CloseF(Vector2.Zero.Angle(), 0f)) return "Angle(Zero) != 0";
                // -X is exactly the atan2 branch-cut seam -- +PI and -PI are equally valid, so check
                // reconstruction (cos/sin of the angle) instead of the raw radian value.
                float negXAngle = (-Vector2.UnitX).Angle();
                if (!CloseV(new Vector2(MathF.Cos(negXAngle), MathF.Sin(negXAngle)), -Vector2.UnitX)) return "Angle(-X) doesn't reconstruct -X via Cos/Sin";
                return null;
            });

            results.Check("AngleTo: unsigned, symmetric, [0,PI]", () =>
            {
                if (!CloseF(Vector2.UnitX.AngleTo(Vector2.UnitY), MathHelper.PiOver2)) return "AngleTo(+X,+Y) != PI/2";
                if (!CloseF(Vector2.UnitX.AngleTo(Vector2.UnitY), Vector2.UnitY.AngleTo(Vector2.UnitX))) return "AngleTo not symmetric";
                if (!CloseF(Vector2.UnitX.AngleTo(Vector2.UnitX), 0f)) return "AngleTo(v,v) != 0";
                if (!CloseF(Vector2.UnitX.AngleTo(-Vector2.UnitX), MathF.PI)) return "AngleTo(v,-v) != PI";
                if (!CloseF(Vector2.Zero.AngleTo(Vector2.UnitX), 0f)) return "AngleTo with a zero vector should degrade to 0, not NaN";
                return null;
            });

            results.Check("AngleToSigned: antisymmetric, matches Rotated's sign convention", () =>
            {
                if (!CloseF(Vector2.UnitX.AngleToSigned(Vector2.UnitY), MathHelper.PiOver2)) return "AngleToSigned(+X,+Y) != +PI/2";
                if (!CloseF(Vector2.UnitX.AngleToSigned(-Vector2.UnitY), -MathHelper.PiOver2)) return "AngleToSigned(+X,-Y) != -PI/2";
                if (!CloseF(Vector2.UnitX.AngleToSigned(Vector2.UnitY), -Vector2.UnitY.AngleToSigned(Vector2.UnitX))) return "AngleToSigned not antisymmetric";

                var rand = new Random(1);
                for (int i = 0; i < 200; i++)
                {
                    Vector2 a = new((float)rand.NextDouble() * 20f - 10f, (float)rand.NextDouble() * 20f - 10f);
                    Vector2 b = new((float)rand.NextDouble() * 20f - 10f, (float)rand.NextDouble() * 20f - 10f);
                    if (a.LengthSquared() < 1e-6f || b.LengthSquared() < 1e-6f) continue;
                    float turn = a.AngleToSigned(b);
                    Vector2 rotated = Vector2.Normalize(a.Rotated(turn));
                    if (!CloseV(rotated, Vector2.Normalize(b), 1e-3f))
                        return $"Rotating a by AngleToSigned(a,b) didn't point toward b (a={a}, b={b}, turn={turn})";
                }
                return null;
            });

            results.Check("Rotated: doesn't mutate the source, matches MonoGame's own mutating Rotate for the same angle", () =>
            {
                Vector2 original = new(3, 4);
                Vector2 rotated = original.Rotated(MathHelper.PiOver2);
                if (!CloseV(original, new Vector2(3, 4))) return "Rotated mutated its receiver";
                if (!CloseV(rotated.Rotated(0f), rotated)) return "Rotated(0) should be a no-op";

                Vector2 mutable = new(3, 4);
                mutable.Rotate(MathHelper.PiOver2); // MonoGame's own void, in-place Rotate
                if (!CloseV(rotated, mutable)) return $"Rotated(angle) disagrees with Vector2's own mutating Rotate(angle): {rotated} vs {mutable}";
                return null;
            });

            results.Check("PerpendicularClockwise/CounterClockwise: exact 90-degree turns, inverses of each other, match Rotated", () =>
            {
                if (!CloseV(Vector2.UnitX.PerpendicularClockwise(), -Vector2.UnitY)) return "PerpendicularClockwise(+X) != -Y";
                if (!CloseV(Vector2.UnitX.PerpendicularCounterClockwise(), Vector2.UnitY)) return "PerpendicularCounterClockwise(+X) != +Y";
                Vector2 v = new(3, 4);
                if (!CloseV(v.PerpendicularClockwise(), v.Rotated(-MathHelper.PiOver2))) return "PerpendicularClockwise != Rotated(-PI/2)";
                if (!CloseV(v.PerpendicularCounterClockwise(), v.Rotated(MathHelper.PiOver2))) return "PerpendicularCounterClockwise != Rotated(+PI/2)";
                if (!CloseV(v.PerpendicularClockwise().PerpendicularCounterClockwise(), v)) return "CW then CCW didn't return to the original";
                return null;
            });

            results.Check("DirectionTo/SafeNormalize: unit length, safe at zero with a configurable fallback", () =>
            {
                if (!CloseV(Vector2.Zero.DirectionTo(new Vector2(10, 0)), Vector2.UnitX)) return "DirectionTo not unit length/direction";
                if (!CloseV(new Vector2(5, 5).DirectionTo(new Vector2(5, 5)), Vector2.Zero)) return "DirectionTo(p,p) should safely be Zero, not NaN";
                if (!CloseV(Vector2.Zero.SafeNormalize(), Vector2.Zero)) return "SafeNormalize(Zero) default fallback should be Zero";
                if (!CloseV(Vector2.Zero.SafeNormalize(Vector2.UnitX), Vector2.UnitX)) return "SafeNormalize(Zero, fallback) didn't use the fallback";
                if (!CloseV(new Vector2(3, 4).SafeNormalize(), Vector2.Normalize(new Vector2(3, 4)))) return "SafeNormalize disagrees with Normalize for a nonzero vector";
                return null;
            });

            results.Check("Approach (Vector2 and float): reaches target exactly, never overshoots, partial steps correctly", () =>
            {
                if (!CloseV(Vector2.Zero.Approach(new Vector2(10, 0), 10f), new Vector2(10, 0))) return "Approach didn't land exactly at maxDistance==dist";
                if (!CloseV(Vector2.Zero.Approach(new Vector2(10, 0), 100f), new Vector2(10, 0))) return "Approach overshot past the target";
                if (!CloseV(Vector2.Zero.Approach(new Vector2(10, 0), 3f), new Vector2(3, 0))) return "Approach partial step wrong";
                if (!CloseF(5f.Approach(10f, 3f), 8f)) return "Approach(float) toward a higher target wrong";
                if (!CloseF(5f.Approach(0f, 3f), 2f)) return "Approach(float) toward a lower target wrong";
                if (!CloseF(5f.Approach(6f, 10f), 6f)) return "Approach(float) overshot past the target";
                return null;
            });

            results.Check("ClampMagnitude: shrinks an over-length vector to exactly maxLength, no-ops a shorter one", () =>
            {
                if (!CloseV(new Vector2(30, 40).ClampMagnitude(10f), new Vector2(6, 8))) return "ClampMagnitude didn't shrink correctly (30-40-50 triangle scaled to length 10)";
                if (!CloseV(new Vector2(1, 0).ClampMagnitude(10f), new Vector2(1, 0))) return "ClampMagnitude changed an already-short vector";
                if (!CloseF(new Vector2(30, 40).ClampMagnitude(10f).Length(), 10f)) return "ClampMagnitude's result isn't exactly maxLength";
                return null;
            });

            results.Check("Slide: drops the normal component entirely, keeps the tangential one, and is a no-op on a purely tangential vector", () =>
            {
                // Moving down-right into a floor whose normal points straight up: the vertical
                // (into-floor) component should vanish, leaving pure horizontal motion.
                if (!CloseV(new Vector2(1f, -1f).Slide(Vector2.UnitY), new Vector2(1f, 0f))) return "Slide against a floor normal didn't drop the vertical component";

                // A vector already tangential to the normal (perpendicular) should pass through unchanged.
                if (!CloseV(Vector2.UnitX.Slide(Vector2.UnitY), Vector2.UnitX)) return "Slide changed a vector already perpendicular to the normal";

                // A vector purely along the normal should slide to zero.
                if (!CloseV(new Vector2(0f, -5f).Slide(Vector2.UnitY), Vector2.Zero)) return "Slide of a purely-along-normal vector should be zero";

                // Result must always be perpendicular to the normal (that's the whole point of "tangential").
                Vector2 slid = new Vector2(3f, -7f).Slide(new Vector2(0.6f, 0.8f)); // (0.6,0.8) is already unit length
                if (MathF.Abs(Vector2.Dot(slid, new Vector2(0.6f, 0.8f))) > 1e-4f) return $"Slide result {slid} isn't perpendicular to the normal";
                return null;
            });

            results.Check("Dot: fluent wrapper matches Vector2.Dot exactly", () =>
            {
                if (!CloseF(Vector2.UnitX.Dot(Vector2.UnitY), 0f)) return "Dot(+X,+Y) != 0";
                if (!CloseF(Vector2.UnitX.Dot(Vector2.UnitX), 1f)) return "Dot(+X,+X) != 1";
                Vector2 a = new(3, 4), b = new(-2, 5);
                if (!CloseF(a.Dot(b), Vector2.Dot(a, b))) return "Dot(a,b) disagrees with Vector2.Dot(a,b)";
                return null;
            });

            results.Check("Cross: 2D scalar cross product -- sign gives turn direction, 0 when parallel", () =>
            {
                if (!CloseF(Vector2.UnitX.Cross(Vector2.UnitY), 1f)) return "Cross(+X,+Y) should be +1 (CCW)";
                if (!CloseF(Vector2.UnitY.Cross(Vector2.UnitX), -1f)) return "Cross(+Y,+X) should be -1 (CW)";
                if (!CloseF(Vector2.UnitX.Cross(Vector2.UnitX), 0f)) return "Cross(v,v) should be 0";
                if (!CloseF(new Vector2(2, 4).Cross(new Vector2(1, 2)), 0f)) return "Cross of parallel vectors should be 0";
                return null;
            });

            results.Check("Project: parallel component only, complements Slide, safe at a zero 'onto'", () =>
            {
                if (!CloseV(new Vector2(3, 4).Project(Vector2.UnitX), new Vector2(3, 0))) return "Project onto +X should keep only the X component";
                if (!CloseV(new Vector2(3, 4).Project(Vector2.Zero), Vector2.Zero)) return "Project onto a zero vector should safely be Zero, not NaN";

                // onto needn't be unit length -- a scaled-up onto should give the identical projection.
                Vector2 v = new(5, -2);
                Vector2 onto = new(3, 1);
                if (!CloseV(v.Project(onto), v.Project(onto * 10f))) return "Project should be invariant to the length of 'onto'";

                // Project + Slide against the same unit-length direction reconstructs the original vector.
                Vector2 dir = Vector2.Normalize(new Vector2(1, 2));
                if (!CloseV(v.Project(dir) + v.Slide(dir), v)) return "Project(dir) + Slide(dir) didn't reconstruct the original vector";
                return null;
            });

            results.Check("SmoothDamp (float and Vector2): converges to target over repeated steps, velocity ref is updated", () =>
            {
                float velocity = 0f;
                float current = 0f;
                for (int i = 0; i < 300; i++) current = current.SmoothDamp(10f, ref velocity, 0.2f, 1f / 60f);
                if (!CloseF(current, 10f, 0.01f)) return $"float SmoothDamp didn't converge, got {current}";
                if (MathF.Abs(velocity) > 0.5f) return $"velocity should have settled near 0 once converged, got {velocity}";

                Vector2 vCurrent = Vector2.Zero;
                Vector2 vVelocity = Vector2.Zero;
                for (int i = 0; i < 300; i++) vCurrent = vCurrent.SmoothDamp(new Vector2(10, -5), ref vVelocity, 0.2f, 1f / 60f);
                if (!CloseV(vCurrent, new Vector2(10, -5), 0.01f)) return $"Vector2 SmoothDamp didn't converge, got {vCurrent}";
                return null;
            });

            results.Check("GameTimeExtensions.GetElapsedTimeSeconds matches ElapsedGameTime.TotalSeconds", () =>
            {
                var gt = new GameTime(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(0.016));
                return CloseF(gt.GetElapsedTimeSeconds(), 0.016f, 1e-4f) ? null : $"GetElapsedTimeSeconds returned {gt.GetElapsedTimeSeconds()}, expected ~0.016";
            });
        }
    }
}
