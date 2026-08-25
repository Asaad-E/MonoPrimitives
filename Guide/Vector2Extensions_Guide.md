# Vector2Extensions / Vector3Extensions — Guide

`Vector2Extensions` (namespace `MonoPrimitives`, file [`src/Core/Vector2Extensions.cs`](../src/Core/Vector2Extensions.cs)) is a set of extension methods on MonoGame's own `Vector2` — everyday 2D vector math it doesn't provide itself. They show up on any `Vector2` once `using MonoPrimitives;` is in scope; none of this is a native MonoGame member. Its 3D counterpart, `Vector3Extensions`, is covered in its own section near the bottom of this guide.

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
| `Slide(normal)` | Drops the component of `this` along `normal` (which must already be unit length), keeping only the tangential part — the classic "keep moving along the wall/floor you just hit" instead of stopping dead. Different from `Vector2.Reflect`: `Reflect` bounces (flips the normal component), `Slide` slides (drops it entirely). |
| `GameTimeExtensions.GetElapsedTimeSeconds()` | Shorthand for `(float)gameTime.ElapsedGameTime.TotalSeconds`. |

## Rotated, not Rotate

MonoGame's own `Vector2` already has instance methods `Rotate(float)`/`RotateAround(Vector2, float)` — both **mutating** (`void`, in place). An extension method named `Rotate` would be silently unreachable: a same-named instance method always wins over an extension method in C#, so `v.Rotate(angle)` would just call MonoGame's own mutating version, not this one — no compile error, no warning, just the wrong behavior. Named `Rotated` instead (Godot's own convention for the same "give me a rotated copy" shape) to sidestep the collision entirely.

---

## 3D: Vector3Extensions

`Vector3Extensions` (namespace `MonoPrimitives.Primitives3D`, file [`src/3D/Vector3Extensions.cs`](../src/3D/Vector3Extensions.cs)) is the 3D counterpart — everyday `Vector3` math XNA doesn't provide itself. Lives in `MonoPrimitives.Primitives3D` rather than alongside `Vector2Extensions` in `Core/`, since — unlike `Vector2`, which both halves of this library use for screen-space positions — nothing in this library's 2D half ever touches a `Vector3`.

```csharp
using MonoPrimitives.Primitives3D;

float turn = shipForward.AngleToSigned(toTarget, Vector3.Up); // yaw needed, right-hand rule around +Up
Vector3 aimed = shipForward.Rotated(Vector3.Up, turn * dt * turnSpeed); // a rotated COPY, not in-place
Vector3 direction = position.DirectionTo(target);       // normalized, safe if the points coincide
position = position.Approach(target, speed * dt);        // move toward target, landing exactly on it
Vector3 clamped = velocity.ClampMagnitude(maxSpeed);      // cap length, keep direction
```

| Member | What it does |
|---|---|
| `AngleTo(other)` | Unsigned angle in `[0, PI]` between two vectors — how far apart they are, no turning direction. |
| `AngleToSigned(other, axis)` | Signed angle in `[-PI, PI]` to rotate `this` by **around `axis`** to face `other` — positive is counter-clockwise looking down `axis` toward the origin (the right-hand rule), matching `Rotated`'s convention and Unity's `Vector3.SignedAngle`. `axis` needs naming explicitly — see "Why `AngleToSigned`/`Rotated` need an axis" below. |
| `Rotated(axis, radians)` | Returns a **copy** of `this` rotated around `axis` by `radians` (right-hand rule) — a thin wrapper over `Quaternion.CreateFromAxisAngle` + `Vector3.Transform`, so you don't build the quaternion by hand for a one-off rotation. |
| `DirectionTo(other)` | Normalized direction from `this` to `other`. Returns `Vector3.Zero` if the two points coincide, instead of `NaN`. |
| `SafeNormalize(fallback = default)` | Like `Vector3.Normalize()`, but returns `fallback` (default `Vector3.Zero`) instead of `NaN` for a zero-length vector. |
| `Approach(target, maxDistance)` | Moves toward `target` by at most `maxDistance`, landing exactly on it instead of overshooting — Godot's `move_toward`/Unity's `Vector3.MoveTowards`. Negative `maxDistance` moves away from `target` instead. |
| `ClampMagnitude(maxLength)` | Shrinks a vector to at most `maxLength`, preserving direction — a no-op if it's already shorter. |
| `Slide(normal)` | Drops the component of `this` along `normal` (unit length), keeping only the tangential part — sliding along a wall/floor/slope instead of stopping dead against it. Same `Reflect` vs. `Slide` distinction as the 2D version. |

**No `float` overload of `Approach` here** — reuse `MonoPrimitives.Vector2Extensions`'s own `Approach(float, float, float)` directly (`using MonoPrimitives;`); it's already dimension-agnostic (plain 1D scalar math, nothing 2D-specific about it), and duplicating it in this namespace too would make any call site with both namespaces in scope ambiguous (`CS0121`) instead of picking one.

**No `Reflect`** — MonoGame's own `Vector3.Reflect(vector, normal)` already exists natively (confirmed by inspecting the referenced assembly, not assumed), so it isn't repeated here. Same for `Clamp`/`Lerp`/`SmoothStep` and friends.

### Why `AngleToSigned`/`Rotated` need an axis

2D's `AngleToSigned(other)` has no axis parameter because a 2D plane only has one way to rotate — "positive" unambiguously means counter-clockwise. A 3D vector has no such single default: "positive" rotation only means something once you've picked which axis you're turning around (turning `+PI/2` around `+Y` sends `+X` to `-Z`; around `-Y` it would send `+X` to `+Z` instead — genuinely different results, not a sign-flip quirk). So both methods ask for `axis` explicitly rather than picking one for you. `AngleToSigned` measures `from`/`to` as their projection onto the plane perpendicular to `axis` first — a component either vector has running *along* `axis` doesn't skew the result, so "how much yaw to face that point" (`axis = Vector3.Up`) stays correct whether the point is above or below eye level.

**Still no `Angle()`** (a bare heading with no reference) — a 2D vector has one canonical "heading" (its angle from +X); a 3D vector genuinely doesn't without naming a plane, which is exactly what `AngleToSigned`'s `axis` parameter is for.

## See also

- [`Design/DECISIONS.md`](../Design/DECISIONS.md) — how the `Rotate`/`Rotated` naming collision was found (a numeric test caught a `void` where a `Vector2` was expected), the `atan2` branch-cut behavior at `Angle(-X)`, and why `Vector3Extensions` has no bare `Angle()`/`PerpendicularClockwise`-style members despite otherwise matching `Vector2Extensions` closely.
- [`Guide/Camera2D_Guide.md`](Camera2D_Guide.md) / [`Guide/Primitive2DBatch_Guide.md`](Primitive2DBatch_Guide.md) — `rotation` parameters elsewhere in the library use the same radians/counter-clockwise convention as `Rotated`/`AngleToSigned`.
- [`Guide/Camera3D_Guide.md`](Camera3D_Guide.md) / [`Guide/Primitive3DBatch_Guide.md`](Primitive3DBatch_Guide.md) — where a `Vector3` built with these helpers usually ends up.
