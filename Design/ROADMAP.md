# Roadmap

Known gaps and queued ideas. Check before proposing something "obviously missing" — it may be a deliberate deferral, not an oversight.

## Deferred on purpose (don't build without asking)

- **Named input-action mapping** (register `"Jump"` once, rebindable). `PrimitiveInput` deliberately stays low-level (`IsKeyDown`/`GetAxis`/`GetVector2` + raw device queries) — a full action-map layer is real infrastructure beyond what's been asked for.
- **Shader-based screen-space antialiasing.** Built twice (search-based FXAA, then a simpler edge-aware blend), both verified rendering correctly, both shelved anyway — see DECISIONS.md. Don't rebuild this without a concrete reason MSAA isn't enough.

## Real gaps, not yet built

- **NuGet publish finalization**: `src/MonoPrimitives.csproj` needs `Authors`, `PackageLicenseExpression`, `RepositoryUrl`, and a real starting version before an actual publish — a deliberate decision, not something to fill in speculatively.
- **No automated tests.** All verification so far is manual (standalone render checks + `dotnet build`). A `tests/` project covering the pure-math pieces (`Collision2D`/`3D`, `ClampCornerRadiusToFit`, `Noise`, `ColorUtil`'s HSV round-trip) would be a real improvement, not yet requested.
- **No CI** — no GitHub remote yet either.
