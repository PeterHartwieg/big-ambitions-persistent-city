# E3 salad: merge the current development base into PR #36

PR #36 contains pre-squash cupcake/donut history and conflicts with development after cupcake #33 landed. Merge incoming `origin/codex/persistent-city` at `3178f3e` into approved salad `4f90a38`, using common ancestor `00d1321`, with a real two-parent merge. This integration targets `codex/pc-e3-salad` and must land with a **merge commit**, so PR #36 can subsequently be squash-merged into `codex/persistent-city`. The harness handles push and PR creation.

Conflict resolution and integration only. **E3 salad, donut/cupcake facades and shared-reader C# are unchanged from approved salad head `4f90a38`.** Relative to incoming cupcake `3178f3e`, the shared reader retains only salad's already-approved pinned descriptor and documentation comment. Every read branch, refusal and original donut/cupcake case remains identical to the landed contracts. No cross-candidate substitution, product activation, gameplay caller, native write or performance-affecting production change.

## Conflict resolutions

Both parents' diffs against `git merge-base` were inspected for all thirteen conflicted files. No configuration or persistence conflict occurred.

| Conflicted file | Resolution |
| --- | --- |
| `CHECKS.md` | Keep the complete incoming index. Move salad's full original section and former README notice into `docs/checks/e3-salad.md`, with exactly one new index line. Preserve all seven numbered in-game checks, LOCKED-OK / NEEDS-INPUT markers, prerequisites, original test counts, timing flags and evidence links. Every incoming checks file stays unchanged. |
| `README.md` | Keep incoming split-layout README and checks-index pointer. Salad's dated notice moves to its owning checks file; all preexisting notices remain in their owning files. |
| `Runtime/DirectStockShowcaseReader.cs` | Keep approved salad bytes: the third pinned descriptor and general per-product comment extend the identical landed cupcake implementation. No read branch, refusal, snapshot or donor candidate changes. |
| `docs/plans/e3-cupcake-pr-description.md` | Keep incoming historical-handoff banner, sync-review pointer and split checks link; all original implementation evidence remains. |
| `docs/plans/e3-cupcake.md` | Keep incoming history-independent guard/baseline explanation and relocated checks links. No cupcake evidence or prerequisite changes. |
| `docs/plans/e3-donut.md` | Keep incoming squash-safe source-guard explanation and checks links, plus salad's candidate-row stop-record link. All candidate inventory entries and E5 prerequisites survive. |
| `docs/plans/steps-b-e.md` | Keep incoming C3, E2a/E2c and all product decisions; retain combined donut/cupcake status and salad's independent conditional-probe stop status. |
| `docs/roadmap.md` | Keep incoming B0-B2 status, B1 exception, E2c and complete per-slice performance index. Retain salad's roadmap status with corrected checks link; add one `docs/perf/e3-salad.md` index line carrying existing timing/allocation evidence. No new measured win. |
| `tools/cupcake-feasibility-regression/compare_donut.py` | Keep incoming hash-pinned landed donut fixture instead of the pre-squash Git lookup. Existing differential cases and byte-identical JSON comparison remain unchanged. |
| `tools/cupcake-feasibility-regression/measure_before.py` | Keep incoming fixture/hash-based baseline; mechanically strip the SALAD alias and SALAD-only block before the existing cupcake normalization. The resulting original donut harness still matches the landed hash. |
| `tools/donut-feasibility-regression/Program.cs` | Keep approved salad bytes, including every original donut/cupcake case and all salad-only cases. Removing only explicit salad additions reproduces the exact landed cupcake harness; removing cupcake additions reproduces the landed original donut harness. |
| `tools/donut-feasibility-regression/source_checks.py` | Keep incoming history-independent pins, isolation/writer/caller guards and explicit whole-tree audit. Extend isolation to salad; pin full approved salad additions and normalize only those additions for the existing cupcake pins. Audit all incoming production/test/tool/config files except five named, documented integration paths. |
| `tools/donut-feasibility-regression/validate.py` | Keep all incoming gates, dynamic regression discovery, nested pickup and optional-stop performance/findings; discover salad and add salad isolation, differential and before-baseline gates. Keep sequential foreground execution, required other-dotnet wait, disabled build/compiler server reuse and no deadlines. |

## Minimal integration fixes and preservation

The old salad guard would freeze unrelated slices at `00d1321`, rejecting already-approved source and migrations. Its source/baseline comparisons also depended on pre-squash cupcake `682aa77`. The standing guard now preserves incoming cupcake's history-independent SHA-256 pins and original donut fixture, adds full salad pins, and checks the exact cupcake shared reader/harness after removing only the explicit salad additions. `--audit-base <incoming-development-commit>` remains a separate whole-tree sync audit, so standing isolation accepts unrelated future slices.

Salad's baseline/differential tool reconstructs the **exact hash-verified landed cupcake source**, identical to approved `682aa77`, from normalized current source. It neither reads old feature history nor substitutes another candidate. Before timings still compile the two-descriptor baseline in fresh processes; differential checks compile separately named baseline types and compare serialized outputs for every approved donut/cupcake functional/timed case. Native doubles and assertions are unchanged. These are tool-only integration fixes; **no salad or shared-reader C# changed during this sync**.

The preservation audit confirms **505 incoming source/test/tool/config blobs** unchanged outside the five named integration paths. All **32 incoming checks/performance files**, **347 original CHECKS paragraphs** and **26 original README notices** survive after moved-link normalization. All seven original salad native checks remain intact. **21 Git-free, line-ending, future-source, mutation and exact-baseline probes pass**; candidate crossover, shared-output mutation, case drift and new gameplay reachability all refuse.

**Save schema:** inherit development's existing schema **18** and all migrations/refusals unchanged. Incoming Core, Persistence, Compatibility, tests and configuration match exactly. Salad adds no schema, saved field or persistence change; no schema conflict occurred. Historical schema-16/test/digest statements remain dated evidence of the original implementation, not current merged-tree claims.

## Validation

Merged-tree validation results will be recorded here after the foreground gate run completes.

## Native evidence and scope

Under merge rule v2, in-game checks follow in the batched combined locked `rig_test` session on `codex/persistent-city`; pointer/input-only checks wait for Peter's 6 Nov playtest. Salad's seven checks and all incoming native checklists retain their original qualifications. Native mod-work p95 <5 ms, every spike >=30 ms flagged, whole-frame evidence recorded separately, and all four parked hotspots remain required. No release or ordinary-save deployment clearance is claimed.

No game launch, rig/profile/bridge lease access, Steam/Workshop writes, WSL, campaign purchase, in-game money movement, RLO edit, push or PR publication. The production build reads the supplied Steam assemblies in place.
