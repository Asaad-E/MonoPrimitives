# Documentation style (`///` XML doc comments)

Strict, not a suggestion — every `///` block written or touched gets checked against this file before the change is done. Based on standard .NET XML documentation conventions (the shape Microsoft's own BCL and Framework Design Guidelines use, and what MonoGame/MonoGame.Extended/Newtonsoft.Json follow), not invented ad hoc.

## Which tag holds what

- **`<summary>`** — one sentence (rarely two). Third person, present tense, declarative: `"Draws a filled circle."`, never `"This method draws..."` or a second-person `"Use this to draw..."`. States what the member does or returns — nothing else.
- **`<param name="x">`** — one short phrase describing what the parameter represents. Not a restatement of its name (`"the radius"` for a parameter called `radius` says nothing `radius` didn't already say — say what it controls instead).
- **`<returns>`** — what's returned, only when it isn't already obvious from the summary (skip on a trivial getter or an obvious `bool`).
- **`<exception cref="T">`** — one entry per exception type the member can throw, with the triggering condition. This is where a "throws X" fact belongs. **Never as prose inside `<summary>`** — `/// <summary>Saves...</summary> <exception cref="ArgumentException">An unrecognized file extension.</exception>` reads as reference documentation; `"...anything else throws ArgumentException rather than silently guessing"` reads as someone narrating their own design choice.
- **`<remarks>`** — optional, only for a genuine correctness gotcha: something that would break or surprise a caller if left unsaid (a hidden invariant, a thread-safety constraint, a non-obvious clamp). One or two sentences, not a paragraph.
- **`<see cref>` / `<paramref name>`** — use these instead of writing a type or parameter name as plain text.

## Voice

- Third person, present tense: `Returns`, `Draws`, `Clamps`, `Removes` — not `Will draw`, `Is used to draw`, `You can use this to`.
- No filler qualifiers: `simply`, `just`, `basically`, `essentially`.
- No rhetorical negation that restates the opposite of a fact already given, without adding a new one. `"throws ArgumentException"` is complete; appending `"rather than silently guessing"` adds no fact a caller needs, only narrates the design's own justification. Keep a contrast only when it states a genuinely different behavior the caller must not assume (`"clamps to the segment's own length, not the infinite line"` earns its keep — a caller could reasonably assume either).

## What never belongs in `///`

Rationale, benchmarks, comparisons to other classes/libraries, "why we built it this way," implementation mechanism (*how* something works internally). It goes in exactly one of these, never duplicated across more than one:

- A plain `//` comment near the relevant code, if it's genuinely useful to someone reading that implementation.
- `Design/DECISIONS.md`, if it's a design rationale worth recording.
- `Guide/*.md`, if it's usage guidance a caller would look up deliberately.

Grade **every clause independently**, not the block's overall length — a single short sentence can still smuggle in a comparison to a sibling class, an exhaustive member list, or a usage-example snippet, and reads as "fine" if you only check line count.

**A single illustrative example is not exempt.** `"(e.g. a debug overlay)"` and `"a loot table, a spawn point, a dialogue line"` are the same violation at different counts — the test is never "is this a list," it's "does this state a fact about behavior, or does it illustrate when/why you'd call this." A `(e.g. ...)` clause that names a scenario instead of a behavior always fails that test, whether it names one scenario or five.

- Never name "raylib" or another design inspiration (Unity, Godot, MonoGame.Extended, etc.) in a `///` comment or `Guide/*.md` file. A plain `//` implementation comment may, if genuinely useful. Exception: root `README.md`'s "Inspiration" section is deliberate and stays as-is.
- Don't compare one class/method to a sibling's implementation approach inside `///` ("mirroring `FastTexture`'s own below-MonoGame approach") — state this method's own behavior only.
- Don't narrate a verification/benchmark story ("verified by round-tripping 20000 random angle triples...", "observed once not to clamp... no reliable repro found") — state the resulting behavior/guarantee, full stop. The story goes in `DECISIONS.md` if worth keeping.
- Don't reference "the fix" or a specific past conversation — that belongs in a changelog entry, not in code that outlives the session that produced it.

## Calibration (real examples caught in this codebase)

| Bad (found, since fixed) | Why | Good |
|---|---|---|
| `...anything else throws ArgumentException rather than silently guessing.` | Rhetorical negation, no new fact | `<exception cref="ArgumentException">An unrecognized extension.</exception>` |
| `...the CPU-pixel-buffer half of what raylib's Image* functions cover.` | Names a design inspiration | Delete the clause |
| `Matches Unity's Mathf.PingPong.` | Names a design inspiration | Delete the sentence |
| `...mirroring FastTexture's own below-MonoGame approach for the same reason...` | Comparison to a sibling class | Delete; state only this class's own behavior |
| `Verified by round-tripping 20000 random angle triples... (worst-case dot product 0.9999997)` | Benchmark/verification narrative | Delete; keep only the resulting correctness fact (e.g. the gimbal-lock caveat) |
| `...instead of silently vanishing.` (after already stating what *does* happen) | Redundant negation | Delete the trailing clause |

Splitting a bloated `<summary>` into `<summary>` + `<remarks>` is **not** a fix — most IDEs render both concatenated in one tooltip, so total visible content is unchanged. The fix is deleting content, not relocating it.
