# MonoPrimitives

Immediate-mode 2D and 3D primitive drawing for MonoGame, plus the small set of helpers a fast prototype usually needs — camera, input, easing, color, noise, collision/raycast tests, and a live 2D chart — so you don't have to reach for a handful of separate external libraries for those.

Built for prototypes: simulations, small game demos, plots. Not a game engine, not aimed at shipping a full commercial game.

## One package

`MonoPrimitives` is one NuGet package, one assembly — install it and you get 2D and 3D both, no separate sub-packages to resolve (raylib/MonoGame.Extended-style, not a multi-package ecosystem). Internally still organized into three namespaces so nothing is duplicated:

- **`Primitives2D`** (`src/2D/`)
- **`MonoPrimitives3D`** (`src/3D/`)
- **`MonoPrimitives`** (`src/Core/`) — shared foundation (input, easing, color, noise) the other two use.

```bash
dotnet build MonoPrimitives.slnx
```

See [`samples/MonoPrimitives.Sample`](samples/MonoPrimitives.Sample) for a minimal runnable demo.

## Documentation

Start at **[`Design/README.md`](Design/README.md)** — project brief, architecture map, conventions, and the reasoning behind non-obvious choices. The full 2D API reference is at [`Guide/PrimitiveBatch_Guide.md`](Guide/PrimitiveBatch_Guide.md).

## Status

Pre-release, not yet published to NuGet. API may still change.
