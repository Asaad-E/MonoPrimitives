# MathUtil — Guide

`MathUtil` (namespace `MonoPrimitives`, file [`src/Core/MathUtil.cs`](../src/Core/MathUtil.cs)) is four scalar helpers confirmed missing from MonoGame's own `MathHelper` — the kind of one-liner you'd otherwise write yourself in every project that needs it.

## API

| Member | What it does |
|---|---|
| `Remap(value, fromMin, fromMax, toMin, toMax)` | Linearly remaps `value` from one range onto another. Not clamped — a value outside `[fromMin, fromMax]` extrapolates past the target range instead of clamping to it. |
| `DeltaAngle(current, target)` | Signed shortest-path difference between two angles (radians), in `(-pi, pi]`. `DeltaAngle(170°, -170°)` is `20°`, not `-340°`. |
| `LerpAngle(a, b, t)` | Interpolates from angle `a` to `b` (radians) the short way around the circle, unlike `MathHelper.Lerp`'s straight numeric path (which would spin the long way around for the same 170°→-170° case). |
| `PingPong(t, length)` | Bounces `t` back and forth between `0` and `length` as `t` keeps increasing (or decreasing — negative `t` still bounces within range). `PingPong(7.5, 5)` is `2.5` (already climbed to 5 and come back down). Returns `0` for a non-positive `length` instead of dividing by zero. |

## Quick start

```csharp
using MonoPrimitives;

// Map a 0-100 health value onto a 0-1 UI bar fill amount.
float barFill = MathUtil.Remap(health, 0f, 100f, 0f, 1f);

// Turn a turret toward a target angle without ever spinning the long way around.
turretAngle += MathUtil.DeltaAngle(turretAngle, targetAngle) * turnSpeed * dt;

// A light that pulses between dim and bright as time passes.
float brightness = MathUtil.PingPong(elapsedTime, 1f);
```

## Notes

- `DeltaAngle`/`LerpAngle` both build directly on `MathHelper.WrapAngle` — if you need a raw angle wrapped into `[-pi, pi]` with nothing else, that's already there natively.
- None of these clamp their output — `Remap`/`LerpAngle` both extrapolate past their target range for out-of-range input, matching `MathHelper.Lerp`'s own unclamped behavior. Clamp yourself first if you need that.
