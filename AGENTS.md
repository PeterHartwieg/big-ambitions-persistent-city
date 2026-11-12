# Persistent City

A Big Ambitions mod that keeps a persistent city of residents, journeys and traffic, and connects them to the player's food businesses. It requires Real Life Overhaul (RLO) and is not released.

## Map

- `Core/`: pure simulation, netstandard2.1, no Unity. `CitySimulator`, `DailyRoutine`, `TrafficKernel`, `FoodCommerce`, `HouseholdFinance`, `PersonIdentity`.
- `Persistence/`: save codecs and stores. `SaveCodec` (one writer; `CityState.CurrentSchema` in `Core/Model.cs` is held at 1), `SaveEpoch`, `FoodSaleReceipts`.
- `Runtime/`: Unity and game calls. `PersistentCityMod`, the `PopulationRuntime` partials, `PhysicalPopulation`, `NativeFoodMarket`.
- `Compatibility/`: the only RLO access. `RloBridge`, `RloIdentityAdapter`.
- `tests/`: xUnit, `PersistentCity.Tests.csproj`. `tools/`: build, harnesses, `docs-links`.
- Docs: [README](README.md), [roadmap](docs/roadmap.md), [behaviour](docs/behaviour.md), [glossary](docs/glossary.md), [rig checklist](docs/rig-checklist.md), `docs/plans/` (open plans only), `docs/decisions/`. Nothing else is kept in the tree; older records are in the tag `docs-history-2026-11-10` and are not required reading.

## Start a slice

Read this file, your plan row and the sections your task lists. Then read the code. Other docs only if the task names them. Plans and large source files are long: find the section with `grep -n` and read that range, not the whole file.

## Rules

- Write plainly: "implemented" or "verified by rig". One qualification per claim; link evidence instead of restating caveats. Status words: planned, implemented, partial, stopped, verified by rig.
- Perf gate: mod-work p95 < 5 ms; flag every mod-work spike >= 30 ms; whole-frame timing is reported separately. Take cheap, safe wins and give their before/after numbers in the PR description. In unit tests, wall-clock limits go through `PerfGate.Under` (checked only with `PC_PERF_ASSERTS=1`), never a plain `Assert`.
- Local tests while working and in fix rounds: build, `python tools/test-focused.py` (the classes in test files you changed) and the slice's own new checks. Do not run the full suite: the harness runs it once before merge. There is no dotnet lock; never wait for other test runs. The `tools/*-regression` suites are not run before release.
- In-game checks are batched for the next rig run and do not gate merge. List them in the PR description under "In-game checks", each numbered with setup, steps and expected result. The coordinator copies them into `docs/rig-checklist.md` after merge.
- The save schema is held at 1 until release. Never bump it, add a per-feature schema constant, or plan a writer number; change the format in place. This overrides older plan text. A test enforces it. [decision](docs/decisions/2026-11-09-schema-one.md)
- Slices write no docs unless the task names the file. Evidence, perf numbers and in-game checks go in the PR description. The coordinator updates the roadmap and the rig checklist after merge. Plans are frozen and leave the tree once their slices have merged; later owner decisions go in `docs/decisions/`. [decision](docs/decisions/2026-11-10-lean-docs.md)
- No code comments. Fix the problem or open an issue instead of leaving an excuse in the code. The only exceptions are tooling directives and the lines in `comment-excuses.txt`; `CommentBanTests` enforces this.
- No new markdown files unless Peter explicitly asks for one (a plan or a review); put status in the PR description or the issue. `tools/md-ban.py` (`MarkdownBanTests`) fails on new `.md`/`.mdx` paths; `--pr <n>` passes when Peter has applied the `docs-approved` label, and `PC_MD_BAN_PR=<n>` makes the test use it.
- Use "Refs #N", not "Fixes #N".
- Do not change RLO or publish anything upstream. Do not ask about release or Workshop.
- Implementers do not use the game, the rig, ModsLocal or the bridge.

Details: [delivery rules, 6 Nov 2026](docs/decisions/2026-11-06-delivery-rules.md); [agent speed, 9 Nov 2026](docs/decisions/2026-11-09-agent-speed.md) (tests, reading); [schema held at 1, 9 Nov 2026](docs/decisions/2026-11-09-schema-one.md); [city direction, 10 Nov 2026](docs/decisions/2026-11-10-city-direction.md).

## Safety rules

- Core is pure C# on netstandard2.1. Unity and game calls stay in Runtime. RLO access stays behind Compatibility: validate its contract, refuse to start on incompatible data, never silently make replacement identities.
- Own state uses a versioned `persistentcity` namespace. Reject unreadable data and any schema number other than 1 without overwriting it.
- Reuse stable IDs and persist assignments. No identity change on pool reuse, reload, camera movement or a different update cadence.
- No game DLLs, decompiled code, raw save files or credentials in Git. Reference local assemblies in place.
- Tests check invariants and interrupted journeys, not repeated formulas. Report offline checks separately from game evidence.
- The detached food shadow stays hypothetical and never books native sales. The population has no economic effect unless the saved city owns a food scope.
- Eligible player food businesses are owned automatically; ownership transfers only at native accounting boundaries. Unsupported businesses stay native-owned. Never write rival finances. Never deploy to Peter's ordinary save.
- Diagnostic helpers create fixtures only in their named disposable FT cities on the owned rig, and restore preexisting rig files and preferences before releasing it. Fixture evidence is not release approval.
- Perf runs disable autosave in the isolated test profile before timing. Record the original setting, check it is off before each measured window (also after reload), and restore it at cleanup. A save inside a window invalidates the sample. Save profiling needs a separate, explicitly requested run.

The October authorizations and the old scope statement are in [docs/decisions/2026-10-22-24-authorizations.md](docs/decisions/2026-10-22-24-authorizations.md).
