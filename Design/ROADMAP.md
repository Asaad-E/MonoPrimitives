# Roadmap

Known gaps and queued ideas. Check before proposing something "obviously missing" — it may be a deliberate deferral, not an oversight.

## Deferred on purpose (don't build without asking)

- **Named input-action mapping** (register `"Jump"` once, rebindable). `PrimitiveInput` deliberately stays low-level (`IsKeyDown`/`GetAxis`/`GetVector2` + raw device queries) — a full action-map layer is real infrastructure beyond what's been asked for.
- **Shader-based screen-space antialiasing.** Built twice (search-based FXAA, then a simpler edge-aware blend), both verified rendering correctly, both shelved anyway — see DECISIONS.md. Don't rebuild this without a concrete reason MSAA isn't enough.

## Real gaps, not yet built

- **NuGet: not yet published.** `src/MonoPrimitives.csproj` metadata (`Authors`, `PackageLicenseExpression: MIT`, `RepositoryUrl`/`PackageProjectUrl`, root `LICENSE`) is filled in and `MonoPrimitives` is confirmed free on nuget.org as of this check. `.github/workflows/publish.yml` auto-publishes on every GitHub Release via NuGet Trusted Publishing (OIDC, no stored API key) — the repo owner still needs to (1) register the matching Trusted Publishing policy on nuget.org (repo `Asaad-E/MonoPrimitives`, workflow file `publish.yml`) and (2) add a `NUGET_USER` repo secret (nuget.org profile name, not email) before the first Release publish will actually succeed.
- **CI is publish-only** (`publish.yml`, triggered by GitHub Release) — no build/test-on-PR workflow yet.
