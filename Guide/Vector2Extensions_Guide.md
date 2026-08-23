# Vector2Extensions — Guide

`Vector2Extensions` (namespace `MonoPrimitives`, file [`src/Core/Vector2Extensions.cs`](../src/Core/Vector2Extensions.cs)) is a set of extension methods on MonoGame's own `Vector2` — everyday 2D vector math it doesn't provide itself. They show up on any `Vector2` once `using MonoPrimitives;` is in scope; none of this is a native MonoGame member.

## Quick start

```csharp
using MonoPrimitives;

Vector2 toTarget = target - position;
float heading = toTarget.Angle();               // radians, (-PI, PI]
Vector2 aimed = Vector2.UnitX.Rotated(heading);  // a rotated COPY, not in-place
position = position.Approach(target, speed * dt); // move toward target, landing exactly on it
```

## API

| Member | What it does |
|---|---|
| `Angle()` | The vector's own heading in `(-PI, PI]`, counter-clockwise from +X. `Vector2.Zero` returns `0`. |
| `AngleTo(other)` | Unsigned angle in `[0, PI]` between two vectors — how far apart they are, no turning direction. |
| `AngleToSigned(other)` | Signed angle in `[-PI, PI]` to rotate `this` by to face `other` — positive is counter-clockwise, matching `Rotated`'s convention. |
| `Rotated(radians)` | Returns a rotated **copy** — see "Rotated, not Rotate" below for why this isn't just called `Rotate`. |
| `PerpendicularClockwise()` / `PerpendicularCounterClockwise()` | Exact, trig-free 90° turns (a swap and a negate) — cheaper than `Rotated` when you only need a quarter turn. |
| `DirectionTo(other)` | Normalized direction from `this` to `other`. Returns `Vector2.Zero` if the two points coincide, instead of `NaN`. |
| `SafeNormalize(fallback = default)` | Like `Vector2.Normalize()`, but returns `fallback` (default `Vector2.Zero`) instead of `NaN` for a zero-length vector. |
| `Approach(target, maxDistance)` (`Vector2` and `float` overloads) | Moves toward `target` by at most `maxDistance`, landing exactly on it instead of overshooting — Godot's `move_toward`/Unity's `MoveTowards`. Negative `maxDistance` moves away from `target` instead. |
| `ClampMagnitude(maxLength)` | Shrinks a vector to at most `maxLength`, preserving direction — a no-op if it's already shorter. |
| `GameTimeExtensions.GetElapsedTimeSeconds()` | Shorthand for `(float)gameTime.ElapsedGameTime.TotalSeconds`. |

## Rotated, not Rotate

MonoGame's own `Vector2` already has instance methods `Rotate(float)`/`RotateAround(Vector2, float)` — both **mutating** (`void`, in place). An extension method named `Rotate` would be silently unreachable: a same-named instance method always wins over an extension method in C#, so `v.Rotate(angle)` would just call MonoGame's own mutating version, not this one — no compile error, no warning, just the wrong behavior. Named `Rotated` instead (Godot's own convention for the same "give me a rotated copy" shape) to sidestep the collision entirely.

## See also

- [`Design/DECISIONS.md`](../Design/DECISIONS.md) — how the `Rotate`/`Rotated` naming collision was found (a numeric test caught a `void` where a `Vector2` was expected) and the `atan2` branch-cut behavior at `Angle(-X)`.
- [`Guide/Camera2D_Guide.md`](Camera2D_Guide.md) / [`Guide/Primitive2DBatch_Guide.md`](Primitive2DBatch_Guide.md) — `rotation` parameters elsewhere in the library use the same radians/counter-clockwise convention as `Rotated`/`AngleToSigned`.
