# Easing — Guide

`Easing` (namespace `MonoPrimitives`, file [`src/Core/Easing.cs`](../src/Core/Easing.cs)) is a static class of classic 0→1 tweening curves for one-shot animations with a known duration — a menu sliding in, an object scaling up, a color fading out. Every formula matches the canonical easings.net/Penner reference set.

## Quick start

```csharp
using MonoPrimitives;

float t = Math.Clamp(elapsed / duration, 0f, 1f);
float eased = Easing.CubicOut(t);
Vector2 position = Vector2.Lerp(start, end, eased);
```

Every function takes `t` in `[0,1]` and returns a (usually, see below) `[0,1]`-ish value — clamp your own progress fraction before passing it in, then feed the result into a plain `Lerp` (`Vector2.Lerp`, `ColorUtil.Lerp`, `MathHelper.Lerp`, whatever you're tweening).

`Easing` complements `Camera2D`/`Camera3D`'s own `SmoothDamp` rather than replacing it: `SmoothDamp` is a physical spring for open-ended following/zoom (no fixed end time, reacts continuously to a moving target); `Easing` is for a one-shot animation with a known start and duration.

## The families

10 shaped families plus `Linear`, each with `In` (slow start), `Out` (slow finish), and `InOut` (slow at both ends) — 31 functions total.

| Family | Character | Curve |
|---|---|---|
| `Linear` | No easing — passes `t` through unchanged. The baseline every other curve bends away from. | — |
| `Quad` | Gentle, everyday default — `QuadOut` is the go-to for "settle into place." | `t²`-based |
| `Cubic` | A more pronounced version of `Quad`. | `t³`-based |
| `Quart` | More pronounced still. | `t⁴`-based |
| `Quint` | The strongest polynomial curve here — an even more pronounced slow start/finish than `Quart`. | `t⁵`-based |
| `Expo` | The most dramatic slow-start/fast-finish (or reverse) curve — barely moves, then accelerates sharply. | exponential |
| `Sine` | The gentlest, smoothest curve here — good as a default when easing should be felt, not noticed. | `sin`/`cos`-based |
| `Circ` | A quarter-circle arc — distinctly rounder than the polynomial curves, a different shape rather than just a stronger/weaker version of them. | `sqrt`-based |
| `Back` | A slight pull backward before moving (`In`), or an overshoot past the target before settling (`Out`) — a "wind-up" or a "pop." | polynomial + overshoot constant |
| `Bounce` | Settles (`Out`) or starts (`In`) with a few decaying bounces — good for something landing or launching. | piecewise |
| `Elastic` | Overshoots and oscillates like a spring pulled taut — the most "characterful" curve here. | exponential × `sin` |

`Back`/`Bounce`/`Elastic` are the three families that deliberately leave `[0,1]` at some point along the curve (an overshoot past the target, or a dip below the start) — that's their entire defining character, not a bug. Every other family stays monotonically within `[0,1]`.

## Picking a curve as data

Every curve above is also a value of the `EasingType` enum, dispatched through `Easing.Evaluate(EasingType, t)`:

```csharp
EasingType curve = LoadCurveChoiceFromLevelFile(); // or a debug-UI dropdown
float eased = Easing.Evaluate(curve, t);
```

Use this when the curve itself is data — chosen from a config/level file, exposed as a dropdown in a debug panel, stored per-object instead of hardcoded. Call the named function directly (`Easing.CubicOut(t)`) instead when the curve is already known at compile time — `Evaluate` adds a switch dispatch on top for no benefit in that case.

## Picking one

- **Default choice**: `QuadOut` or `CubicOut` — reads as "settling into place," unremarkable in the best way.
- **Want it to barely register as easing at all**: `SineInOut`.
- **Want a "pop" when something appears**: `BackOut`.
- **Something landing**: `BounceOut`. **Something launching off**: `BounceIn` or `ElasticIn`.
- **A distinctly rounder feel than the polynomial curves**: `Circ*`.
- **Need it to feel more dramatic than `Quart` allows**: `Quint*`.

## Two properties worth knowing (both are permanent regression tests, not just claims)

- **Every `InOut` variant passes through exactly `f(0.5) = 0.5`.** The `In` and `Out` halves are constructed to meet exactly at the midpoint — true even for `Back`/`Bounce`/`Elastic`'s `InOut` variants.
- **Every family's `Out` is `1 - In(1 - t)`.** This is how an ease-out is always derived from its ease-in — reverse time, flip the result. True universally, even though only `BounceIn` is literally implemented that way in the source (`BounceIn(t) => 1 - BounceOut(1 - t)`); the rest just happen to satisfy the same identity by construction.

## Testing

Every function has a permanent regression check in [`tests/MonoPrimitives.Tests/EasingTests.cs`](../tests/MonoPrimitives.Tests/EasingTests.cs): the two properties above, boundary conditions (`f(0)=0`/`f(1)=1`) across all 31 functions, monotonicity for the 7 smooth families, ease-in-lags/ease-out-leads-linear-pace at `t=0.25`, the defining overshoot/oscillation behavior for `Back`/`Elastic`/`Bounce`, and that `Evaluate(EasingType, t)` matches its named function exactly for every curve. Run with:

```bash
dotnet run --project tests/MonoPrimitives.Tests/MonoPrimitives.Tests.csproj
```

## See also

- [`Design/DECISIONS.md`](../Design/DECISIONS.md) — the audit that added `Quint`/`Circ` (confirmed against raylib's `reasings.h`, Godot's `Tween.TransitionType`, and DOTween's `Ease` enum) and built out the test suite from nothing.
- [`Guide/Camera2D_Guide.md`](Camera2D_Guide.md) — `SmoothDamp`, the spring-based alternative for open-ended following/zoom instead of a fixed-duration tween.
