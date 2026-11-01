# E1 base sync: conflict resolution and integration

Merge approved E1 `72f1cbd` with development `1f8f4b4` (including final C2a `692b8b5`, B1/B2, C3/C4, E2b/E2c and shared donut/cupcake feasibility), then refresh to `51ce709` including read-only E3 salad. This is a real two-parent merge on `codex/pc-e1-sync-base`; no rebase, squash or cherry-pick. The actual common ancestor is `7a6153c`; both conflicted-file diffs against it were inspected, with `9b13698..72f1cbd` isolating the approved E1 delta from shared squash ancestry.

## Latest base refresh

The final fetch found development had moved from `1f8f4b4` to `51ce709` while
validation ran. Second two-parent merge `d146f90` retains that complete update:
salad's pinned descriptor/facade, cases, source/differential tools, plans/checks
and both index lines. It merged cleanly; there are no new conflicted files.
The landed shared reader and salad tools match the base exactly, and that refresh
changes no Core/Persistence source. Initial merge/repair evidence remains below;
the latest full gate and preservation audit are recorded separately.

## Runtime/Core scope

**Runtime/Core code changes only compose existing approved behavior.** Shared food ranking now accepts both the merged B1 memory callback/contributions and E1 weather distance coefficient. Route rebuild retains B1 settings, E2b timing and E1 source/settings. Queue planning keeps E2b valid physical-binding precedence while forwarding the logical E1 Q capture and rain lead; capture still belongs to WaitingForOpening. No new feature, draw key, source slot, route/context allowance, native writer, menu support or C4 caller is introduced. The failure-only C2a memory fix, inactive C4 engine, B1/B2, E2b timings and native checkout remain intact.

E1's `Core/WeatherPolicy.cs` also changes its queue API to accept/forward the
optional existing dwell. This is the minimal adapter for E2b's preserved physical
commitment argument; its weather policy, keys, capture boundary and settings do
not change. No Runtime/Core fix was added in response to a test failure: all such
repairs were schema literals or synthetic old-format fixtures/tools.

## Save contract

Merged schema 19 has the same wire shape as E1's original `9b13698`; the difference is B1 semantic support for active revision 1 and finite nonzero held contributions. Preserve that support, with exact strict 18/19 shape readers and additive 19->20 neutral initialization/next-day weather activation/preflight. W remains 22/24 bytes per resident, consumer weather 2/8 extra bytes and zero additional consumer records; fixed caps/reserves are unchanged.

Integration-only test/tool repairs: C4 literal save-neutrality assertions now pin schema 20; its byte comparator exports the current schema with C4 files omitted and requires exact byte equality. C2a's populated-memory migration fixture now creates genuine schemas 18/19/20 and retains B1 memory/contributions (rainy current-schema case included). E2b's extracted queue fixture now models logical saved Q separately from physical binding commitment, preserving its thinning/lead/validity cases and adding capture continuity. Initial full-suite run: 2 fixture failures, 2,372 passes; no production workaround. The separately run awareness benchmark also needed its synthetic schema-18 input to project out only E1 weather; `5089748` preserves C2a's byte comparison and reports weather bytes separately (tool-only fix). All final results are recorded in the E1 slice evidence and command manifest.

## Every conflicted file

| File | Resolution |
| --- | --- |
| `CHECKS.md` | Keep every development index entry; move both complete E1 evidence sections and LOCKED-OK / NEEDS-INPUT checklist to docs/checks/e1-rain.md; add one E1 line. |
| `Core/CitySimulator.cs` | Keep B1 activation/market settings and E2b timing on route rebuild; carry E1 source/settings, W copy/commit and event-owned queue capture. |
| `Core/DwellPlanner.cs` | Keep E2b physical commitment argument/precedence; append E1 captured rain-lead argument and bounded lead calculation. |
| `Core/FoodAwareness.cs` | Keep renamed awareness alternative type and B1 contribution callback; append weather multiplier to distance preference. B1 failure-only source fix remains unchanged. |
| `Core/FoodChoicePolicy.cs` | Keep B1 ApplyMemory and offer contribution; thread E1 captured distance multiplier through shortlist/final choice without changing keys. |
| `Core/FoodCommerce.cs` | Keep development B1 constructors, memory hooks, failure attribution, cached hours and checkout behavior unchanged. |
| `Core/FoodToday.cs` | Keep merged held contribution contract and add E1 weather/multiplier scalars. |
| `Core/FoodWeightedCommerce.cs` | Compose captured weather distance with B1 contribution arrays/callbacks for both shortlist paths and diagnostics; preserve experience hooks and route/context caps. |
| `Core/Model.cs` | Keep merged model; write schema 20 and root WeatherStartsDay plus nullable resident W. |
| `Core/ResidentInsight.cs` | Keep development address formatting and B2 memory words unchanged. |
| `Core/ShopMemory.cs` | Keep active B1 revision-1 support and separate B2 projection; add only ConsumerMemory.Weather. |
| `Persistence/PopulationWire18.cs` | Keep merged B1 capability/memory validation; add schema-aware E1 suffix encoding/validation and current record-count shape. |
| `Persistence/SaveCodec.cs` | Keep B1 bounded nonzero contributions; retain E1 strict 18/19 resolvers, 19->20 neutral next-day migration, preflight and weather record validation. |
| `README.md` | Keep development concise layout; relocate the complete E1 dated notice into its checks file. |
| `Runtime/NativeFoodMarket.cs` | Keep development IFoodFailureMarket and native failure evidence unchanged. |
| `Runtime/PersistentCityMod.cs` | Keep all newer startup guards/settings; share OptionalStopEnvironment with lazy frame weather source while preserving E2b StopTiming. |
| `Runtime/PhysicalPopulation.cs` | Keep current selection/visibility/timing and valid binding dwell; QueueDwell delegates to saved E1 weather instead of live rain. Both queue entry paths use that helper. |
| `Runtime/PrototypeSettings.cs` | Keep all B1/C4/E2b settings/converters and their JsonIgnore annotations; add all four E1 flat settings/converters. |
| `Runtime/ResidentInspector.cs` | Keep development visibility-gated refresh, formatted-address callback and B2 projections unchanged. |
| `config/persistent-city.json` | Keep every merged setting and all four E1 keys. |
| `docs/plans/step-a-inspector-traits-choice.md` | Keep complete development plan, decisions and migrated checks links. |
| `docs/population-schema18.md` | Keep complete merged B1/B2 contract and append E1 budget consumption with slice evidence link. |
| `docs/population-schema19.md` | Keep final merged C2a contract including populated B1 source semantics/failure-only fix and combined-head native boundary. |
| `docs/reviews/2026-10-31-a3-pr-description.md` | Keep development description with split-layout evidence links. |
| `docs/roadmap.md` | Keep all merged statuses/gates and every perf index; integrate E1 status and move all three original E1 win rows to docs/perf/e1-rain.md with one index line. |
| `tests/FoodAwarenessTests.cs` | Keep complete merged tests; construct a genuine schema-18 image by removing only E1 additions before awareness fields and expect current schema after migration. |
| `tests/HeldFoodTuningTests.cs` | Keep merged B1 tests: bounded nonzero contributions execute; malformed/nonfinite/out-of-range contributions still refuse. |
| `tests/OptionalStopSchedulingTests.cs` | Keep every E2b timing/rain/day-night case plus E1 enabled/disabled native-safety/work/paired-continuation tests. |
| `tests/ResidentInsightTests.cs` | Keep all development address and B2 evidence tests unchanged. |
| `tests/Schema17Tests.cs` | Keep all tests; shift unsupported future-schema examples beyond 20. |
| `tests/Schema18ChoiceReplayTests.cs` | Keep full merged economy/reload comparisons; align only the newly introduced activation header. |
| `tests/Schema18ReviewTests.cs` | Keep merged active-B support; update literal writer/future schemas to 20/21. |
| `tests/Schema18Tests.cs` | Keep merged active-B semantics; adapt only literal noncanonical schema and escaped-memory corruption tokens to schema 20. |
| `tools/build.py` | Keep development assembly references, SDK fallback and empty-SDK error unchanged. |
| `tools/food-ownership-regression/Program.cs` | Keep merged active-B bootstrap success and unsupported revision/code/future refusal tests unchanged. |
| `tools/native-command-regression/run.py` | Keep development verified Windows SDK fallback and all existing checks unchanged. |
| `tools/optional-stop-regression/run.py` | Keep complete development extraction, stationary scope and performance/findings harness unchanged; update its fixture separately for saved E1 queue terms. |
| `tools/physical-selection-regression/run.py` | Keep development SDK fallback, interior wiring and selection checks unchanged. |
| `tools/save-contract-regression/AwarenessContracts.cs` | Keep all awareness byte/record checks; project out only schema-20 weather when comparing schema 18. |
| `tools/save-contract-regression/CapacityBench.cs` | Keep merged full-memory reserves; populate real E1 captures and subtract weather bytes/records exactly once. |
| `tools/save-contract-regression/HeldTuningContracts.cs` | Keep merged B1 bounded nonzero contribution acceptance and fixed scalar budget/record assertions unchanged. |
| `tools/save-contract-regression/Program.cs` | Keep development F0 save-neutral checks and Runtime imports; include E1 weather contract checks and exact schema-19 projection digest/length. |

## Validation and evidence boundary

**2,375/2,375 tests and 57/57 final foreground gate commands pass** (58 attempts including the preserved awareness fixture failure/rerun). The game-target build, every regression, demos, exact schema-19 projection/economy digests, C4 raw schema-20 zero-delta comparison, CF32/CF64 ledger and three fresh weather benchmarks pass. Weather has zero >=30 ms sweeps across 5,100 samples, maximum 3.6095 ms. Offline aggregate >=30 ms costs (including D1 build 36.7610 ms, awareness whole build 33.1729 ms and whole-save timings) are flagged separately from native mod-work/whole-frame results in the measurements. Evidence: [E1 sync evidence](../checks/e1-rain.md#base-sync--1-november-2026-offline-only) and [command/hash manifest](2026-11-01-e1-sync-base-results.json). No game, rig, bridge lease, Steam/Workshop or normal profile access. Merge rule v2 defers in-game proof to the batched combined locked rig on development head; pointer/input-only checks wait for Peter's 6 Nov playtest. Original E1 LOCKED-OK / NEEDS-INPUT items are preserved. No release/upload or ordinary-save clearance.
