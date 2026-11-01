# E3 cupcake: merge the current development base into PR #33

PR #33 contains pre-squash donut history and conflicts with development after donut #23 and the intervening slices landed. Merge incoming `origin/codex/persistent-city` at `193c81d` (including E2b #28) into approved cupcake `682aa77`, using common ancestor `00d1321`, with a real two-parent merge. This integration targets `codex/pc-e3-cupcake` and must land with a **merge commit**, so PR #33 can subsequently be squash-merged into `codex/persistent-city`. The harness handles push and PR creation.

Conflict resolution and integration only. **E3 cupcake, donut facade and shared-reader C# are unchanged from approved cupcake head `682aa77`.** The shared donut read body/refusals and every original donut test/project match the contract landed in `3f7b885` after explicit mechanical substitutions. No candidate substitution, product activation, gameplay caller, native write or performance-affecting production change.

## Conflict resolutions

Both parents' diffs against `git merge-base` were read for all ten conflicted files. No configuration or persistence conflict occurred.

| Conflicted file | Resolution |
| --- | --- |
| `CHECKS.md` | Keep the complete incoming index. Move cupcake's entire checks section and former README notice into `docs/checks/e3-cupcake.md`, with one new index line. Preserve all six numbered in-game checks, LOCKED-OK / NEEDS-INPUT markers, input-only prerequisites, historical test counts, timing flags and evidence links. All incoming checks files, including donut's sync/review history and five native checks, are unchanged. |
| `README.md` | Keep incoming split-layout README and its checks-index pointer. Cupcake's dated notice follows the new convention in its owning checks file; every preexisting notice is retained in the incoming owning files. |
| `Runtime/DonutFeasibilityReader.cs` | Keep the already approved cupcake facade over the shared reader. Validate the shared implementation against the exact landed donut body/refusals and pinned API/harness, rather than restoring the old duplicate implementation. |
| `docs/plans/e3-donut.md` | Keep incoming squash-safe guard explanation and moved checks links; retain cupcake's candidate-row link. Explain the shared-reader pins and mechanical donut comparison. No native evidence is promoted. |
| `docs/plans/steps-b-e.md` | Keep incoming C3, E2a/E2c and all roadmap decisions; use cupcake's combined donut/cupcake E3 status. |
| `docs/roadmap.md` | Keep incoming B0 through B2 status, B1 exception, E2c and complete per-slice performance index. Retain combined donut/cupcake status. Donut's before/after table already lives unchanged in `docs/perf/e3-donut.md`; cupcake's existing timing/allocation evidence is in `docs/perf/e3-cupcake.md` with one index line. No new measured win. |
| `tools/donut-feasibility-regression/DonutFeasibilityRegression.csproj` | Keep the approved shared-reader include; removing exactly that include reproduces the landed donut project hash. |
| `tools/donut-feasibility-regression/Program.cs` | Keep the approved compile-time reader alias and every cupcake case. Removing cupcake-only blocks and the alias mechanically reproduces the landed donut harness hash; no cases dropped. |
| `tools/donut-feasibility-regression/source_checks.py` | Combine cupcake's all-reader isolation/assignment/caller guards with donut's history-independent approved pins. Replace stale original-base preservation with an explicit `--audit-base` per-sync whole-tree audit, preserving the landed distinction between standing isolation and per-sync preservation. Pin the unchanged approved shared reader/facades/harness and exact landed donut fixture. |
| `tools/donut-feasibility-regression/validate.py` | Keep cupcake, donut and all required gates; discover all additional top-level regression projects from the combined tree, plus nested pickup and optional-stop performance/findings. Retain sequential foreground execution, prescribed other-dotnet guard, disabled compiler/build-server reuse and no command deadline. Include original/shared donut differential and original baseline checks. |

## Minimal integration fixes and preservation

The old cupcake whole-tree guard compares against `00d1321` and would reject approved A/B/C/E source and migrations. Its mechanical comparisons also read `462a99d`, which need not survive PR #33's squash merge. The standing guard now uses SHA-256 pins with CRLF normalization and a hash-pinned **own-source** donut fixture copied from `3f7b885`; the differential and original-baseline tools read the same fixture. This is project-owned diagnostic source, not native/decompiled game code. The fixture and normalized original test/project hashes equal the approved donut pins. Whole-tree auditing is explicitly invoked against the actual incoming development head and does not freeze unrelated future slices.

The preservation audit confirms **499 incoming source/test/tool/config blobs** unchanged, apart from the separately pinned/mechanically checked donut composition. All **30 incoming checks/performance files** are unchanged. All **250 original CHECKS paragraphs** and **25 original README notices** survive, after only moved-link normalization. Cupcake's entire checks section survives after relative-link relocation. The standing guard passes in a Git-free exported tree; deliberate shared candidate substitution, facade crossover, original-case drift and gameplay caller probes all fail as required.

**Save schema:** inherit development's existing schema **18** and all migrations/refusals unchanged. All incoming `Persistence/`, Core, tests and configuration blobs match; cupcake adds no schema, saved field or persistence change. No schema conflict occurred.

## Validation

All **46/46 foreground gates pass**, covering every requested command: Steam-reference production build, **2,184 unit tests / 2,184 discovered cases** (zero failed/skipped), all **31 top-level Release regression projects**, nested pickup (**25 checks**), four Python harnesses, three demos with requested arguments, E3 source isolation, original/shared donut differential, original donut baseline and optional-stop `--performance --findings`. Donut/cupcake pass **2,082 / 2,098 assertions**; differential snapshots are byte-identical on every original functional/timed read. The **15 independent guard probes** and explicit whole-tree audit also pass. [Commands, exits, log hashes, preservation, probes and measurements](2026-11-01-e3-cupcake-sync-base-results.json).

The save harness retains schema **18**, **119,083 characters**, digest `018FC26676937FEEF88B7D06AE32B522CCE93A1CC265BCDD87A08C7DD91FBB56`, **849 orders / 797 purchases / two unconfirmed**, and all twelve F0 off/on/save/reload transitions. This is merged-tree validation, not a separately executed incoming-base comparison. All incoming persistence/migration/refusal tests and regression sources are unchanged. DLL SHA-256: `0388fe14109124d0fafa33b605c160c07863965183e312316c84ae70ae42f57d`.

**Offline timing flags:** D1's 20,000-place cold aggregate **37.5525 ms** across bounded units; C1 1,024-business cold p95 **6.4013 ms**. Optional-stop's existing **untuned** 957-resident/100% comparison arm has p95 **20.8439 ms** and three >=30 ms samples: **32.7387 / 31.4527 / 33.3107 ms** (samples 0/1/2). Untuned 25% p95 is **8.5125 ms**; current tuned 25/100% p95 is **4.5296 / 15.9663 ms**. All other optional-stop timing arms report zero >=30 ms samples; the 1,000-venue model-1 batch p95 is **5.5578 / 7.4663 ms** for reuse/rebind. These are existing synthetic stress/comparison conditions, not E3 or native mod-work frames. E3 shared donut/cupcake 2,000-entry warm p50/p95/max is **0.0813/0.1016/1.3626 ms** and **0.0856/0.1116/1.3355 ms**, both **49,104 B/read**, with zero >=30 ms single-reader samples. No new performance win or native clearance is claimed. Native mod-work p95 <5 ms, every >=30 ms spike flagged, separate whole-frame evidence and all four parked hotspots remain required.

Every workload completed sequentially in the foreground after the prescribed other-dotnet guard; the final extra diagnostic waited for another worktree's workload. Compiler/build-server reuse is disabled; no execution deadline was set. Logs stay ignored under `build/e3-cupcake-sync-base/`. `git diff --check` against incoming development passes; the first-parent diff preserves incoming whitespace in Core/tests and historical red logs. A final fetch still resolves development to **193c81d** (E2b #28); C2a has not landed.

## Native evidence and scope

Under merge rule v2, in-game checks follow in the batched combined locked `rig_test` session on `codex/persistent-city`; pointer/input-only checks wait for Peter's 6 Nov playtest. Cupcake's six checks and donut's five checks remain pending with their original markers. No release or ordinary-save deployment clearance is claimed.

No game launch, rig/profile/bridge lease access, Steam/Workshop writes, WSL, campaign purchase, in-game money movement, RLO edit, push or PR publication. The production build reads the supplied Steam assemblies in place.
