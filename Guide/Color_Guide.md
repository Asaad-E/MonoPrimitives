# Palette & ColorUtil — Guide

`Palette` (a curated set of ready-to-use `Color`s) and `ColorUtil` (conversions and adjustments on any `Color`) both live in namespace `MonoPrimitives`, files [`src/Core/Palette.cs`](../src/Core/Palette.cs) and [`src/Core/ColorUtil.cs`](../src/Core/ColorUtil.cs). Covered together here since they're normally used together: pick a starting color from `Palette`, then adjust it with `ColorUtil`.

## Quick start

```csharp
using MonoPrimitives;

batch.FillCircle(pos, 20f, Palette.Emerald);
batch.BorderCircle(pos, 20f, Palette.Nephritis, thickness: 3f); // Emerald's own darker "shadow" twin

Color hovered = ColorUtil.Lighten(Palette.PeterRiver, 0.2f);
Color pressed = ColorUtil.Darken(Palette.PeterRiver, 0.2f);
```

## Palette

21 flat colors, each paired with a slightly darker twin for a border/shadow/pressed-state without hand-picking a second color (`Emerald`/`Nephritis`, `PeterRiver`/`BelizeHole`, `Amethyst`/`Wisteria`, `Alizarin`/`Pomegranate`, `Turquoise`/`GreenSea`, `WetAsphalt`/`MidnightBlue`), plus warm accents (`Sunflower`, `Orange`, `Carrot`, `Pumpkin`), three grays (`Clouds`, `Silver`, `Concrete`/`Asbestos`), and `Background` (a near-black dashboard backdrop, not a content color).

| Member | What it's for |
|---|---|
| `Palette.<Name>` (e.g. `Palette.Emerald`) | One curated color, by name. |
| `Palette.All` | Every one of the 21, `Background` included — for a swatch viewer or "cycle through everything" debug tool. **Not** for picking a random foreground color: a random pick can silently return `Background`, rendering as invisible against the very background it's meant for. |
| `Palette.Primary` | The 10 primary hues only (each pair's brighter half, never `Background`) — always safe as a foreground color, visually distinct even in a short sequence. |
| `Palette.Cycle(index)` | A color from `Primary`, cycling by index — deterministic (same index always gives the same color) and wraps correctly for any `index`, negative included — e.g. one color per simulation category (`Palette.Cycle(agent.Category)`). |
| `Palette.GradientPairs` | `(Color Inner, Color Outer)[]` — a curated set of inner(highlight)/outer(edge) pairs for a glossy radial-gradient "juicy ball" look, feed straight into `FillCircleGradient`/`FillCircleGradientLinear`. A different flavor from the flat colors above, not exhaustive. |

`Palette`'s 21 colors are a deliberately small, game-appropriate curated set — MonoGame's own `Color` struct already ships the *entire* X11/CSS named-color list (`Color.AliceBlue`, `Color.Chartreuse`, dozens more) if you want an exhaustive palette instead; reach for that when `Palette` doesn't have the exact shade you want.

## ColorUtil

### Hex

| Method | What it does |
|---|---|
| `FromHex(hex)` | Parses `"#RRGGBB"`, `"RRGGBB"`, `"#RRGGBBAA"`, or `"RRGGBBAA"` — the leading `#` is always optional, case-insensitive, missing alpha defaults to fully opaque. Throws `ArgumentException` on null/empty/wrong-length input, `FormatException` on non-hex digits. |
| `ToHex(color, includeAlpha = false)` | Formats back to `"#RRGGBB"` or `"#RRGGBBAA"`. |

### HSV

| Method | What it does |
|---|---|
| `FromHSV(h, s, v, alpha = 255)` | Builds a color from hue/saturation/value. **Hue is a turn in `[0,1)`** (0 = red, 1/3 = green, 2/3 = blue) — this project's own convention for angle-like values elsewhere (`DrawCircleSector`'s `startAngle`/`endAngle`), not degrees or radians. `h` wraps automatically outside `[0,1)`. |
| `ToHSV(color, out h, out s, out v)` | The inverse — decomposes a color into hue/saturation/value. |

### Single-color adjustments

All of these preserve alpha and take an existing `Color` in, returning a new one — none mutate the input (`Color` is a value type anyway, but worth saying).

| Method | What it does |
|---|---|
| `Lighten(color, amount)` | Moves value toward 1 (white) by `amount` (`0` = unchanged, `1` = white), hue/saturation preserved. |
| `Darken(color, amount)` | Moves value toward 0 (black), same shape. |
| `Saturate(color, amount)` | Moves saturation toward 1 (fully saturated), hue/value preserved. |
| `Desaturate(color, amount)` | Moves saturation toward 0 (grayscale), same shape. |
| `Complementary(color)` | The opposite hue (half a turn around the wheel) — a color that reads as clearly distinct at a glance. |
| `Invert(color)` | Photographic negative: `255 - channel` per RGB channel. Its own inverse (`Invert(Invert(x)) == x`) — a hit-flash or negative-filter effect. |
| `Contrast(color, amount)` | Pulls every RGB channel toward (`amount > 0`) or away from (`amount < 0`) the 127.5 midpoint, `amount` in `[-1,1]`; `-1` flattens to mid-gray. Unlike the HSV-based adjustments above, this works directly on RGB, so it can shift apparent hue slightly — expected, not a bug. |

### Lerp

| Method | What it does |
|---|---|
| `Lerp(a, b, t)` | Straight per-channel RGB lerp — a thin, discoverable pass-through to `Color.Lerp`. |
| `LerpHSV(a, b, t, longWay = false)` | Interpolates through HSV space instead — a straight RGB lerp from saturated red to saturated blue muddies through gray at `t=0.5`; this sweeps the hue wheel instead, staying vivid the whole way. Takes the **short way** around the wheel by default (red→violet goes backward through magenta, not forward through the whole spectrum); pass `longWay: true` to force the long way instead. |

### Blend modes

Pure `Color x Color -> Color` functions computing a blended color *value* to draw normally afterward — **not** a GPU blend-state operation (`Primitive2DBatch` already uses one `NonPremultiplied` state throughout). Use these for tinting/layering colors in code — procedural palette mixing, not a per-pixel GPU effect. **Alpha always comes from `a` unchanged** in every mode — these blend color, not transparency.

| Method | What it does |
|---|---|
| `Multiply(a, b)` | Darkens — never lighter than either input. `Multiply(white, x) == x` exactly. Like stacking two semi-transparent filters. |
| `Screen(a, b)` | Lightens — the inverse of `Multiply`, never darker than either input. `Screen(black, x) == x` exactly. |
| `Overlay(a, b)` | `Multiply` where `a` is dark, `Screen` where `a` is light — boosts contrast instead of uniformly shifting brightness. `a` is the base, `b` is overlaid on it — the two aren't interchangeable, unlike `Multiply`/`Screen`. |
| `Additive(a, b)` | Straight per-channel sum, clamped at 255 — the standard "glow"/particle-additive look. |

## What's deliberately not here

- **A "readable text color" / auto-contrast helper** (pick black or white text based on a background's luminance) — a genuinely common UI utility elsewhere, but not something raylib/love2d/Godot/Unity ship as a core color function either, so it wasn't added speculatively.
- **Proper alpha-compositing** (raylib's `ColorAlphaBlend`, a Porter-Duff "over" operator accounting for both colors' own alpha) — lower value here specifically, since `Primitive2DBatch` already composites layered draws via its GPU blend state; a CPU-side compositor would mostly duplicate that.
- **Per-color alpha replacement, normalized/packed-int conversion** — already covered directly by MonoGame's own `Color` struct (`new Color(color, alpha)`, `.ToVector3()`/`.ToVector4()`, `.PackedValue`, `new Color(uint)`), so wrapping them here would be pure, redundant sugar.

## Testing

Every method above has a permanent regression check in [`tests/MonoPrimitives.Tests/ColorUtilTests.cs`](../tests/MonoPrimitives.Tests/ColorUtilTests.cs) — round-trips (`Color`↔HSV, hex↔`Color`), each blend mode's defining identity, direction checks for the HSV adjustments, and `Palette`'s wraparound/opacity invariants. Run with:

```bash
dotnet run --project tests/MonoPrimitives.Tests/MonoPrimitives.Tests.csproj
```

## See also

- [`Design/DECISIONS.md`](../Design/DECISIONS.md) — the audit that added `Invert`/`Contrast` and the missing test coverage, including exactly what was checked and ruled out against raylib/love2d/Godot/Unity.
- [`Guide/Primitive2DBatch_Guide.md`](Primitive2DBatch_Guide.md) — where these colors actually get drawn.
