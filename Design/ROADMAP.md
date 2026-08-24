# Roadmap

Known gaps and queued ideas. Check before proposing something "obviously missing" — it may be a deliberate deferral, not an oversight.

## Deferred on purpose (don't build without asking)

- **Named input-action mapping** (register `"Jump"` once, rebindable). `PrimitiveInput` deliberately stays low-level (`IsKeyDown`/`GetAxis`/`GetVector2` + raw device queries) — a full action-map layer is real infrastructure beyond what's been asked for.
- **Shader-based screen-space antialiasing.** Built twice (search-based FXAA, then a simpler edge-aware blend), both verified rendering correctly, both shelved anyway — see DECISIONS.md. Don't rebuild this without a concrete reason MSAA isn't enough. **Circle-specific edge feathering was evaluated separately (non-shader, a vertex-alpha gradient ring) and hits the same wall — see DECISIONS.md.**

## Real gaps, not yet built

- **NuGet: published.** `MonoPrimitives 0.5.0` is live on nuget.org (`dotnet add package MonoPrimitives`), via `.github/workflows/publish.yml` (GitHub Release → NuGet Trusted Publishing, OIDC, no stored API key). Covers this session's `Draw*` overload merges, the `InsetConvexPolygon`/`OutsetConvexPolygon` and `DrawLineStrip3D`/`Trail3D` joint-gap fixes, the equilateral-triangle overloads, and the batcher perf cleanup — `templates/MonoPrimitives` builds against it correctly.
- **CI is publish-only** (`publish.yml`, triggered by GitHub Release) — no build/test-on-PR workflow yet.
- **FNA/KNI: not supported, source-portable in principle only.** Evaluated on request, not built: the library only touches "core XNA4" surface (`BasicEffect`, `GraphicsDevice.DrawUser*Primitives`, `Texture2D.SetData`, standard math/input types) — no custom shaders (the #1 usual FNA/MonoGame incompatibility), no content pipeline, no P/Invoke, which is the best-case profile for portability. Two concrete blockers found anyway: (1) `PrimitiveType.PointList` (`Primitives2D.cs`'s `DrawPixelFast`/`FlushPoint`) is stock MonoGame's name for the primitive FNA calls `PointListEXT` — a one-line fix if ever needed; (2) KNI uses an entirely different root namespace (`nkast.Xna.Framework`, not `Microsoft.Xna.Framework`) — every `using` in every file would need rewriting, not a one-liner. Neither matters practically without a separate build target regardless: a prebuilt `MonoPrimitives.dll` referencing `MonoGame.Framework.DesktopGL` can't be passed an FNA/KNI project's own `Vector2`/`Color` values — same names, different CLR types, no binary compatibility across XNA-alike implementations exists. Real support would mean a second build (swapped `PackageReference`, separate `TargetFramework` or sibling project), not a shared DLL — don't build this speculatively.
