# Working on MonoPrimitives

**Before doing anything**, read **[`Design/README.md`](Design/README.md)** — it links to 5 short docs (project brief, architecture map, code style, decisions, roadmap) covering current state. Together under 2,000 words; read all five before starting real work, not just the one that seems most relevant.

**Before ending your turn**, if you made a real change (new file, new public API, a fixed bug worth remembering, a decision someone might otherwise re-litigate): update the relevant Design/ doc in the same turn — ARCHITECTURE.md for what now exists, DECISIONS.md for why, ROADMAP.md if it closes or opens a gap. Prefer editing an existing line over appending a new one — these docs should stay roughly constant-size as the project grows, not accumulate. Skip this for trivial changes (typo fixes, pure refactors with no behavior change) — don't pad the docs for its own sake.

Don't read the historical logs under `Design/2D/`/`Design/3D/` unless specifically digging into why a past bug fix happened — they're large and superseded by the docs above for anything about *current* state.
