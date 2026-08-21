# Roadmap

Known gaps and queued ideas. Check before proposing something "obviously missing" — it may be a deliberate deferral, not an oversight.

## Deferred on purpose (don't build without asking)

- **Named input-action mapping** (register `"Jump"` once, rebindable). `PrimitiveInput` deliberately stays low-level (`IsKeyDown`/`GetAxis`/`GetVector2` + raw device queries) — a full action-map layer is real infrastructure beyond what's been asked for.
- **Shader-based screen-space antialiasing.** Built twice (search-based FXAA, then a simpler edge-aware blend), both verified rendering correctly, both shelved anyway — see DECISIONS.md. Don't rebuild this without a concrete reason MSAA isn't enough.

## Real gaps, not yet built

- **NuGet: not yet published.** `src/MonoPrimitives.csproj` metadata (`Authors`, `PackageLicenseExpression: MIT`, `RepositoryUrl`/`PackageProjectUrl`, root `LICENSE`) is filled in and `MonoPrimitives` is confirmed free on nuget.org as of this check — remaining steps (nuget.org account, API key, `dotnet pack` + `dotnet nuget push`) require the repo owner's own credentials, so they're not something to run unattended.
- **No CI**, though a GitHub remote now exists (`github.com/Asaad-E/MonoPrimitives`).
