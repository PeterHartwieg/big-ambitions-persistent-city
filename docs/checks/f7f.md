# City audit F7f — frozen reader corpus and packaging, 5 November 2026, offline

## README entry (moved 6 Nov 2026)

City-audit F7f implements a frozen reader corpus for every supported schema 13–25, with pinned hashes, per-reader acceptance/refusal tests and a one-folder-per-writer layout; no reader, writer or schema change (writer 25). Offline only; not verified by rig. [Checks and automated batched-rig list](f7f.md).

## Roadmap entry (moved 6 Nov 2026)

City-audit F7f (#94) implements the [frozen reader corpus](../plans/f7-save-snapshots.md#4-slice-breakdown) for schemas 13–25 offline, with pinned hashes and no reader/writer change; rig verification pending. [Checks and batched-rig list](f7f.md).

## CHECKS index entry (moved 6 Nov 2026)

- City-audit F7f — 5 November 2026, offline: frozen reader corpus for schemas 13–25: 26 synthetic fixtures from existing test builders, SHA-256 pinned with writer-25 migration pins; 85 corpus cases (own/newer readers accept, older report Future, exact migration and source projection, cross-schema relabels refused or meaning-identical, future/older/corrupt/visit+1 (also from populated histories)/input+1 refused with every key preserved). No reader, writer or schema change; writer 25. Zero runtime change, so no mod-work added; maintenance cost 9,187 reader lines, corpus suite median 3,164 ms. At `cdee629`: 2,972 tests passed and all required regression tools, source checks and batch scripts (`run-d3a-checks.py`, both `validate.py`) passed offline. [Checks, cross-schema matrix and In-game checks for the next batched rig](f7f.md#in-game-checks-for-the-next-batched-rig), [maintenance cost](../perf/f7f.md).

## Plan row status (moved 6 Nov 2026)

F7f row in [f7-save-snapshots.md](../plans/f7-save-snapshots.md): *Status (5 Nov 2026): implemented offline on `codex/pc-f7f-reader-corpus`; rig verification pending ([checks](f7f.md)).*

Refs [#94](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/94). Branch `codex/pc-f7f-reader-corpus` from `origin/codex/persistent-city` at `c64e3e2` (writer **25**; F1 #99, F3 #98, F5a #107 and the F5/F6/F7 plans merged). The scope is the F7f row of [the F7 plan](../plans/f7-save-snapshots.md#4-slice-breakdown). Section 3.9, the section 3.7 invariants and the F7f rows of sections 5 and 6 are the spec.

Persistent City is not live or released. This slice is **implemented** offline. It is **not verified by rig**: no game, rig, lease, profile, ModsLocal or bridge directory was touched. Every result below is offline/headless evidence, not native proof.

## Base re-verification

The plan's cited locations were re-read at `c64e3e2`:

- `Core/Model.cs:256`: `CurrentSchema = 25`.
- `Persistence/SaveCodec.cs:35–52`: caps unchanged (256 Mi characters and UTF-8 bytes, 50,000 citizens, 64 recent visits). `SaveCodec.Read` (`:127–164`) returns `Future` above 25 and delegates `<= 24` to `PopulationSchema24.Read`.
- The frozen chain is still `LegacyPopulationCodec` (13–17, explicit allow-list, `:86`), `PopulationSchema19` (18–19, `:119`), `PopulationSchema20` (20, `:117`), `PopulationSchema24` (21–24, `:123`) and `SaveCodec` (25).
- Older than 13 refuses with "Unsupported experimental state schema". Each reader returns `Future` above its own range before it inspects anything else.

## What changed

Only test code, test data, one tool script and docs. **No reader, writer, schema, Core, Persistence, Runtime or Compatibility file changed.** `git diff c64e3e2 -- Core Persistence Runtime Compatibility` is empty.

- `tests/ReaderCorpus/schema-13 … schema-25/`: each folder has `full.json`, `minimal.json` and a `corpus.json` manifest (SHA-256, characters, builder, recipe, writer commit, rig fixture reference). There is also `current-writer-25.json` (migrated pins) and a [README](../../tests/ReaderCorpus/README.md) with the packaging rules.
- `tests/ReaderCorpus.cs`: the reader registry (`ReaderCorpus.Readers`, one row per frozen reader with its own range), a loader that verifies the pinned hash of every file before use, and `ReaderCorpusRecipes.Project`, the existing builders' frozen-writer recipes.
- `tests/ReaderCorpusTests.cs`: 9 test methods, 85 cases (72 in the first commit; review r1 added the 13-case populated-visits theory).
- `tests/ReaderCorpusGenerator.cs`: a one-time generator, run only with `PC_READER_CORPUS_WRITE`. It never rewrites an existing folder. For an existing folder it loads (and hash-checks) **every** manifest entry and pins its migration, so a native file added later gets a next-writer pin; the builder list is used only for a new folder (review r1).
- `.gitattributes`: `tests/ReaderCorpus/** -text`, so line-ending conversion cannot change a pinned byte. Fixtures are single-line, with no BOM and no trailing newline.
- `tests/CurrentSchemaOnlyTests.cs`: `City()` changed from private to internal, so the minimal builder is shared rather than copied. `tests/PersistentCity.Tests.csproj` copies the corpus JSON to the output.
- `tools/reader-corpus/maintenance_cost.py`: lines, corpus bytes and test time per reader ([perf record](../perf/f7f.md)).

## Corpus and provenance

Every fixture is a **synthetic offline payload from existing test builders, not a native save**:

- `full.json` comes from `FoodCommerceTests.S17_Full`: 2 residents, factor 2, three meals, a selected decision with routes and two offers, and current-day outcomes.
- `minimal.json` comes from `CurrentSchemaOnlyTests.City`: 1 resident, 1 home, no food scope.

Each city is written through the frozen writer of its schema. 13–16 use `LegacyWire` and `LegacyMoneyWire`, the same steps as `S17_Old`. 17 uses `LegacyPopulationCodec.Write`. 18 uses `PopulationSchema19.Write` with the `E4_Old` strip. 19 and 20 use their own frozen writers, 21–24 use `PopulationSchema24.WriteForSchema` (with `FoodMenuPolicy.Wire` for 23–24, as in `E5d_Wire24`), and 25 uses `SaveCodec.Write`. A second generation into an empty directory produced byte-identical files. No fixture was hand-edited.

Writer commits are the first mainline commit whose `CurrentSchema` equals the schema. Schemas 15, 21 and 22 only existed on stacked branches and were first merged together with a later writer, so they have no single writer commit. The rig's native fixtures (`pc-schema-13..16@1`, `pc-schema-25-ref@1`, `pc-representative-957@1` in big-ambitions-agent-rig `491ffbf`) are still pending production, so none of them is in the corpus. The README describes how a sealed native payload is added as a further file.

| Schema | Reader | Writer commit | Fixture chars / SHA-256 → writer-25 SHA-256 (12-hex prefixes; full values in the manifests) |
| --- | --- | --- | --- |
| 13 | LegacyPopulationCodec | 63248e4 | full 5,237 / `6056210b7017` → `8bd8383c90c9` <br> minimal 1,375 / `e6879ac7449f` → `da2a118960e6` |
| 14 | LegacyPopulationCodec | 7357f78 | full 5,269 / `3e40e36cb9a6` → `8bd8383c90c9` <br> minimal 1,375 / `60e4ecfdfa0e` → `da2a118960e6` |
| 15 | LegacyPopulationCodec | — (stacked branch) | full 5,229 / `452aced74f33` → `8bd8383c90c9` <br> minimal 1,395 / `b12bbb54388c` → `da2a118960e6` |
| 16 | LegacyPopulationCodec | 21db30b | full 5,281 / `59e87b3b9a2b` → `8bd8383c90c9` <br> minimal 1,421 / `03d14bb6f877` → `da2a118960e6` |
| 17 | LegacyPopulationCodec | 8b05227 | full 7,199 / `60ae54cf8b74` → `b9e233542225` <br> minimal 1,465 / `9073eb76d892` → `da2a118960e6` |
| 18 | PopulationSchema19 | 3b8f7ec | full 5,583 / `0dfb0c63c799` → `b9e233542225` <br> minimal 1,521 / `3ee923784388` → `da2a118960e6` |
| 19 | PopulationSchema19 | 692b8b5 | full 5,623 / `0970933932eb` → `b9e233542225` <br> minimal 1,521 / `3b7a57e9fac7` → `da2a118960e6` |
| 20 | PopulationSchema20 | 5877453 | full 5,712 / `95cadc63dd82` → `eda9dfd006ca` <br> minimal 1,551 / `4825518729af` → `18d005511e25` |
| 21 | PopulationSchema24 | — (stacked branch) | full 5,815 / `3c9fc6a49e3b` → `eda9dfd006ca` <br> minimal 1,551 / `51d87ec47fbd` → `18d005511e25` |
| 22 | PopulationSchema24 | — (stacked branch) | full 5,830 / `b3cdb2262875` → `eda9dfd006ca` <br> minimal 1,551 / `3acee04ed48f` → `18d005511e25` |
| 23 | PopulationSchema24 | 2bd9006 | full 5,903 / `3d5ab61ce7aa` → `eda9dfd006ca` <br> minimal 1,624 / `6af17c125730` → `18d005511e25` |
| 24 | PopulationSchema24 | fb958ae | full 5,927 / `4dbaf0fbf2dd` → `eda9dfd006ca` <br> minimal 1,624 / `57a04520512e` → `18d005511e25` |
| 25 | SaveCodec | 4ee0c8d | full 6,083 / `2085efd217a9` → `2085efd217a9` <br> minimal 1,646 / `ca3965dd083a` → `ca3965dd083a` |

The migrated hashes form three groups (13–16, 17–19, 20–24). Within a group, migration synthesizes the same activation facts and discards the same older fields, so the cities converge on identical writer-25 bytes. A writer-25 fixture round-trips to its own bytes.

## Tests (section 3.7 / 3.9 invariants)

| Test | What it asserts |
| --- | --- |
| `ReaderCorpus_PackagingCoversEverySupportedSchemaWithOneFolderAndOneReader` | Exactly one folder per schema from 13 to `CurrentSchema`, and nothing else. The registered readers tile that range with no gap or overlap, and the newest is `SaveCodec`. The current-writer pin file covers exactly the corpus files. A new writer without a folder, reader row or pin file fails here. |
| `ReaderCorpus_AChangedFixtureByteFailsLoudly` | One digit changed in a copy of `schema-20/full.json` makes `Load` throw `schema-20/full.json changed: pinned sha256 …, found …`. Every test loads through the same hash check. |
| `…OwnAndNewerFrozenReadersAcceptOlderReadersReportFuture` (×13) | For every fixture and every reader: a reader whose own range reaches the schema returns `Loaded`, with `SourceSchema` = N and its own output schema. An older reader returns `Future` and no state. |
| `…FullChainMigratesToTheCurrentWriterExactlyAsPinned` (×13) | `SaveCodec.Read` returns `Loaded`, `SourceSchema` = N and schema 25. The SHA-256 of `SaveCodec.Write` equals the pin, so migration matches today's result exactly. The writer-25 bytes round-trip. A session loads and keeps the source string reference. |
| `…MigratedCityProjectsBackOntoItsSourceBytes` (×13) | Projecting the migrated city back through schema N's frozen writer reproduces the fixture byte for byte, for every schema 13–25 (the E5d check). |
| `…CrossSchemaVariantsAreRefusedOrMeaningIdenticalAndRefusalsPreserveEveryKey` (×13) | Each fixture is relabelled to all 12 other supported schemas, changing only the schema token. Every reader agrees with the full chain: `Future` above its range, otherwise the chain's status. A refused variant preserves everything (see below). An accepted variant must read to exactly the same writer-25 bytes as the true label. A `full` fixture may only be accepted for the 13↔14 pair. |
| `…FutureOlderCorruptAndOverboundVariantsAreRefusedAndPreserved` (×13) | The cases are: future (26, 99) → `Future` from every reader; older (12, 1, 0, −1) → `Invalid` "Unsupported experimental state schema" from every reader; truncated by one character, half, trailing content, unknown top-level field ("Unknown field: Corpus"), duplicate `Schema` and string `Schema`, and empty → `Invalid`; one recent visit over the cap (65, in each schema's own visit shape: named object for 13–17, compact token for 18+) → "Oversized collection". If the schema token still parses, an older reader returns `Future`. |
| `…VisitCapVariantAlsoBuildsFromPopulatedVisitHistories` (×13) | Review r1. Every resident of the `full` city gets three visits and the city is written through schema N's frozen writer (no `"RecentVisits":[]` left). The populated payload loads with three visits per citizen. The over-cap mutation, which replaces the first `RecentVisits` array whatever it holds (bracket matching that skips string contents), is refused by every reader at or above N with "Oversized collection" and preserved. The mutation therefore also works on an ordinary populated native checkpoint (rig check 1). |
| `…OneCharacterOverTheInputCapIsRefusedAndPreserved` (×5, one per reader) | A payload of exactly 268,435,457 characters (a valid fixture plus trailing spaces) is refused by every reader with "input size limit" and preserved. |

**Preserved** means the following. `modData` holds the variant plus `rlo:state`, a `persistentcity:food-sale-v2:` receipt, `persistentcity:food-authority-v1` and a foreign mod key. `SaveCodec.Read` and `SaveSession.Open` report the expected refusal with no state. `SaveSession.WriteTo` and `SaveStore.WriteTo` with a valid city of the same population key both fail. Afterwards every key is present and `Assert.Same` holds for every value, including the original payload string reference.

### Cross-schema matrix (recorded, not hand-tuned)

Of the 26 × 12 = 312 relabels, 299 are refused. The 13 that load all migrate to byte-identical writer-25 output, the same as the true label:

- `schema-13/full`, `schema-13/minimal` → 14; `schema-14/minimal` → 13. Whole-cent schema-13 money is a valid schema-14 decimal-cent value with the same amount. Schema-14 `full` relabelled 13 is refused, because 13 requires whole cents.
- `minimal` (no food scope) 18 ↔ 19, 20 ↔ 21 ↔ 22 (all six directions) and 23 ↔ 24. These schemas differ only in food fields, so a city without food has the same wire shape under both labels.

The plan's wording "refuses cross-schema variants" therefore holds wherever the wire shapes differ. Where they do not, the label carries no meaning, and the test pins that equivalence instead of claiming a refusal the readers do not perform. No reader was changed.

## Packaging (one reader + one folder per writer)

A later writer W+1 needs three changes. First, it freezes the schema-W codec as its own reader (E5d pattern): one new registry row, the live `SaveCodec` row moves to W+1, and one `Project` branch routes schema W to the frozen writer. Second, it runs the generator once, which writes only `schema-(W+1)/` and `current-writer-(W+1).json`. Third, it records maintenance cost with `tools/reader-corpus/maintenance_cost.py`. Older folders, fixtures and manifests stay untouched: `readerWhenFrozen` in a manifest is informational, and only the registry assigns readers. Only the pin file of the current writer is asserted, and older pin files stay as a record.

## Questions for Peter

F7 section 9 asks about a save notice, shop-history days and remembered visits. F7f touches none of them: it adds no runtime behaviour, retention or UI. No default or setting applies, so none was added.

## Performance

No runtime code changed, so there is **zero added ordinary-frame mod-work**: no reflection, polling, scans or allocations in any `Update` path. The mod-work gate (p95 < 5 ms, spikes ≥ 30 ms flagged) is unaffected by construction. No offline sample in this slice measures frame work, and whole-frame cost is not measured here and not claimed. The four parked hotspots (physical population, closure refresh, optional-stop refresh, save serialization) stay parked; save serialization belongs to F7c/F7d.

The section 4 measurement for F7f is test time and lines per reader. It is in [docs/perf/f7f.md](../perf/f7f.md): 9,187 reader lines across 5 readers, 26 fixtures (94,311 bytes), and a corpus suite median of 3,164 ms. One win was taken in the new test code: one over-cap payload per reader instead of per schema cut the over-cap test from 3,294.7 to 871.4 ms and the suite from 6,188.0 to 3,162.9 ms (sum of per-method medians over 3 runs). This is test time only and has no mod-work effect.

## Offline results

The F7f-specific results are listed here. The full required command list and its results are under [Required commands](#required-commands) below.

- `dotnet test --filter FullyQualifiedName~ReaderCorpusTests`: 72 passed, 0 failed (three timed runs, first commit). At head `cdee629`, the `ReaderCorpus` filter gives 86 passed, 0 failed (85 corpus cases plus the generator fact).
- Generator check (review r1): in a scratch copy of the corpus with one extra manifest entry in `schema-20` (a copy of `minimal.json` under a new name), the generator pinned the extra entry, left all 26 existing pins unchanged and gave the extra entry the same pin as `minimal.json`. The scratch copy was deleted.
- Corpus regeneration into an empty directory: byte-identical (`diff -r`).

### Required commands

Run on 5 November 2026 at head `cdee629` (the review r1 fix, including the populated-visits theory). Later commits change only docs. Each dotnet step was preceded by `C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1`, and every step ran on the development PC with no game, rig or bridge. Logs are under the ignored `build/f7f/req/`.

| Command | Result |
| --- | --- |
| `python tools/build.py --game-managed "…\Big Ambitions_Data\Managed"` | exit 0; package DLL SHA-256 `95e69590251fa6963eeca1b9d901bc8f70f841d27f46efe8381e50a2629a8cad` |
| `wait-other-dotnet.ps1 -Repo <worktree>` | exit 0 ("no other dotnet test load; continuing") |
| `dotnet test tests/PersistentCity.Tests.csproj` | **2,972 passed, 0 failed, 0 skipped** (2 m 7 s; base `c64e3e2` had 2,886) |
| `dotnet test … --no-build --list-tests` | exit 0 (2,963 listed lines) |
| `dotnet run --configuration Release` for each of the 41 listed projects: core-identity, diagnostic-response, event-observation, food-catalogue, food-checkout, food-hours, food-household-catalogue, food-ownership, food-supply, frame-observation, gate-access, road-ceiling, road-handoff, road-invalidation, road-speed, route-discovery, save-contract, startup-clock, traffic-closure, traffic-queue, walking-quote, campaign-reader, donut/cupcake/salad feasibility, rival-outflow, awareness, food-candidate, rival-memory, icecream/frenchfries/pizza feasibility, e4-menu, e3-remaining feasibility, e5a-eligibility, food-metadata, name-identity, e5b-complete-menu, food-metadata-api, accounting-backlog, bounded-bulk | all 41 exit 0 (1–19 s each). In a fresh worktree, e4-menu first needs its generated inputs from `python tools/e4-menu-regression/prepare.py`. Without them, the first attempt on this branch failed at compile (missing `build/e4-menu/*.cs`) and passed after `prepare.py`. That is an environment step, not a code failure. |
| `python tools/{native-command,physical-selection,vehicle-ownership,optional-stop}-regression/run.py` | all 4 exit 0 |
| `dotnet run --project tools/simulate/Simulate.csproj -- 28 build/harness-demo` | exit 0 |
| `dotnet run --project tools/food-routines/FoodRoutines.csproj --configuration Release -- build/harness-food-routines-demo 957` | exit 0 |
| `dotnet run --project tools/food-demand/FoodDemand.csproj -- build/harness-food-demand-demo` | exit 0 |
| `source_checks.py` for donut, salad, icecream, frenchfries, pizza, e3-remaining and e5a-eligibility | all 7 exit 0 |
| `python -m unittest discover -s tools/e5b-native-rig-proof -p "test_*.py"` | 16 tests, OK |
| `python tools/food-metadata-api-regression/source_checks.py` | exit 0 |
| `python tools/food-metadata-regression/validate.py`. It wraps `prepare.py`, then **`python tools/run-d3a-checks.py`**, then **`python tools/rival-memory-regression/validate.py`**, then its own build, test and process-proof steps, with `check=True` on every step. | Completed with no failing step: 138 `exit 0` step lines, no `Traceback`, empty stderr. **run-d3a-checks**: all 72 gates exit 0, including its own build, a 2,972-passed test run and list-tests. **rival-memory validate**: 66/66 steps exit 0. Own steps: build exit 0, 2,972 passed / 0 failed, "PASS: two fresh processes match the independent name-taste v1 reference", "PASS; command manifest and log hashes". |

Qualifications:

- `food-metadata-regression/validate.py` runs longer than the 10-minute foreground limit of the agent's shell tool, because its dotnet guard waits whenever other worktrees run dotnet. It was therefore started as a detached process with a recorded PID and polled from the foreground until that PID exited. Nothing was left running. Its numeric exit code was not captured; the evidence is the absence of any failing step under `check=True`, an empty stderr and the final PASS lines.
- That script rewrites two tracked E5c evidence files (`docs/checks/e5c-name-taste-process-proof.json`, `docs/checks/e5c-neutral-metadata-results.json`) with fresh timings. They are not F7f evidence and were restored with `git checkout` and not committed.
- All of this is offline/headless evidence. Nothing here is verified by rig.

### Sync with codex/persistent-city (5 November 2026)

Merge commit `25865bd` brings in F5b (#105, `448247c`) and F6c (#110, `0a926ef`) on top of F7f head `800e803`, as a two-parent merge. The only conflicts were in `CHECKS.md`, `README.md` and `docs/roadmap.md`. Each was a single line on both sides at the same insertion point, and all were resolved by keeping both lines:

- `CHECKS.md`: the F7f index line (5 November) goes above F6c (4 November), newest first. The F5a, F5b and older lines are unchanged.
- `README.md` and `docs/roadmap.md` status paragraphs: F5b goes after F5a, then F7f.
- `docs/roadmap.md` perf log: F5b's line goes after F5a's, then F7f's.

`docs/plans/f7-save-snapshots.md` and `tests/PersistentCity.Tests.csproj` merged without conflict. No code, test, fixture or schema change was needed: writer 25 is unchanged, and no `tests/ReaderCorpus/**` fixture changed (the hash-pin and migration-pin tests pass against the committed bytes, and `git status` stayed clean after every run). Neither F5a nor F7f code changed.

Offline results on the merged tree, development PC, each dotnet step guarded by `wait-other-dotnet.ps1`:

- `python tools/build.py`: exit 0. Package DLL SHA-256 `6ee8b14f234f2209ac0251de19621ed45d5e2cd61281cbc0ca161f454aab068d`; this differs from F7f's because the merged Core now includes F5b and F6c.
- `dotnet test tests/PersistentCity.Tests.csproj`: **3,063 passed, 0 failed, 0 skipped** (2 m 24 s); `--no-build --list-tests` exit 0.
- `ReaderCorpus` + `CurrentSchemaOnlyTests` filter, three runs with `--logger trx`: 100 passed each time. `maintenance_cost.py --trx` over these runs gives the same lines (9,187), corpus files (26) and bytes (94,311) as the [perf record](../perf/f7f.md). Medians in ms are 1,012.1 / 636.3 / 375.0 / 872.6 / 400.7 / 20.6. They include the 13 populated-visits cases that were not in the recorded timing, so they are within noise of it.
- All 42 listed `dotnet run --configuration Release` regression projects exit 0 (e4-menu after `prepare.py`), including `population-census-regression` (F5a). The 4 `run.py` tools, the 8 `source_checks.py`, the simulate/food-routines/food-demand demos and the e5b-native-rig-proof unittests also pass.
- F5b `identity-core-measure`: PASS; validator p50 0.6437 / 3.6705 / 47.6115 ms at 1,914 / 10,000 / 100,000 persons, 0 B warm allocation. This matches the recorded 0.6485 / 3.6938 / 49.2912 ms.
- F6c `feedback-panel-regression`: passed, no failures. Counters p95 is 0.0033 / 0.0031 / 0.0019 ms, with 0 spikes ≥ 30 ms, and the hidden panel does 0 work.
- `python tools/rival-memory-regression/validate.py`: PASS 67/67. A first attempt hit the agent shell's 30-minute background cap while it was waiting for other worktrees' dotnet load, and was stopped. The rerun completed with exit 0.
- `python tools/food-metadata-regression/validate.py` (wraps `run-d3a-checks.py` and rival-memory validate), run on 6 November against merge `25865bd`: exit 0. All 73 `run-d3a-checks.py` gates exit 0. Rival-memory validate reports `PASS 67/67 foreground commands`, every pinned `source_checks.py` prints PASS, and the bundled build gives the same DLL SHA-256 `6ee8b14f…ab068d`. Its test run gives 3,063 passed, 0 failed, 0 skipped. `verify_taste.py` reports PASS for the name-taste v1 reference (`937617912baf…3949e`) and for the command manifest. Earlier attempts hit the agent shell's 30-minute background cap while they waited for other worktrees' dotnet load, and were stopped. This run used a 2-hour limit and completed. As in the F7f record above, the two E5c evidence JSON files it rewrites were restored with `git checkout` and are not committed.

## In-game checks for the next batched rig

These come from F7 plan section 7, checks 5 and 9, which F7f makes testable. Each is executed by the headless agent rig ([PeterHartwieg/big-ambitions-agent-rig](https://github.com/PeterHartwieg/big-ambitions-agent-rig), read-only here). None is deferred to Peter, a playtest, an unlocked console or a by-hand check. Checks 1–3 belong to the save-specific campaign and run only when that campaign is requested. Check 4 runs in the ordinary batched rig.

1. **Native historical payloads read by the frozen corpus readers (save-specific; plan check 5).** For each sealed `pc-schema-13..16@1` fixture, and for `pc-representative-957@1` and `pc-schema-25-ref@1` once sealed, the rig extracts `persistentcity:state_v1` from the fixture save and records its SHA-256 against the fixture manifest's save hash. In a scratch checkout of this branch, it adds the payload as a new file in `tests/ReaderCorpus/schema-N/`, with a manifest entry and a current-writer pin computed from the same run, and runs `dotnet test tests/PersistentCity.Tests.csproj --filter FullyQualifiedName~ReaderCorpusTests` in the foreground. It asserts all of the following: the payload's top-level schema equals the fixture's `schema.persistent_city`; schema N's own reader and every newer reader return `Loaded` with `SourceSchema` = N, and every older reader returns `Future`; the full chain migrates to writer 25 and the migrated city projects back onto the extracted bytes; every refusal variant generated from it is refused with all keys preserved. Any failure, including a projection mismatch, is reported as a finding with the payload hash. The payload and the readers are never edited to make it pass. — rig-automated (existing capability: fixture library manifests and `pc-schema-N@1` provenance, RIG-PCFIX recipes; pending rig capability: **RIG-PCSAVE** payload extraction, plus production and sealing of those fixtures).
2. **Historical fixture loads under the current build (save-specific; plan check 5).** In a fresh process with the writer-25 DLL set from this branch, the rig loads a disposable copy of each sealed `pc-schema-13..16@1` fixture. Through `state` and Player.log it asserts that PC starts with no "Population save refused" line, and that the resident count and population key equal the fixture manifest. After one explicit save, it extracts the payload through RIG-PCSAVE and asserts schema 25 with the same resident IDs. Its writer-25 SHA-256 is compared with a migration of the extracted source under offline check 1, not with the synthetic pins. — rig-automated (existing capability: `rig flow run`, FT `load`/`save`/`state`, RIG-MODSET DLL set selection; pending rig capability: **RIG-PCSAVE**, plus production of the fixtures).
3. **Refused payloads stay unchanged through native play and native saves (save-specific; plan check 5).** In a disposable copy of a sealed fixture, the rig replaces `persistentcity:state_v1` with each of these variants, all built by the offline corpus test helpers: future (schema 26), older (schema 12), truncated by one character, and one recent visit over the cap. For each variant it loads the copy and asserts the Player.log line `Population save refused: …` with the matching reason ("newer schema", "Unsupported experimental state schema", "Invalid state", "Oversized collection") and that native play continues. It then makes one explicit native save, extracts every `modData` key and asserts that the PC payload is byte-identical to the variant and every other key is byte-identical to the copy before load. — rig-automated (existing capability: FT `load`/`save`/`state`, Player.log capture; pending rig capability: **RIG-PCSAVE** extraction plus **RIG-FAULT**-style replacement of one `modData` key in a disposable save copy).
4. **No runtime change and no save work in clean windows (ordinary batched rig; plan check 9).** The rig builds the DLL set from this branch and from base `c64e3e2` at the same build path and asserts identical SHA-256 for every `PersistentCity.*.dll` in the receipt; F7f changes no Runtime, Core, Persistence or Compatibility source. In every ordinary performance window, it records the original autosave preference, keeps autosave off, verifies it off before each window (including after reload), keeps manual saves outside windows and restores the preference in cleanup. It records mod-work p95 < 5 ms and flags every ≥ 30 ms spike, keeping whole-frame metrics separate. — rig-automated (existing capability: RIG-MODSET receipt hashes, `rig flow run` autosave guard and health `autosave_disabled`; pending rig capability: **RIG-PERF-UNION** for unified mod-work/whole-frame evidence).
