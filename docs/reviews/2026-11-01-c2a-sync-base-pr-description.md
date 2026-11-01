# Sync C2a with the landed Persistent City base

PR #24 conflicted after its B0/B0b ancestors landed as squashes and B1/B2 and other slices advanced the target. This branch merges `origin/codex/persistent-city` at `81472e6` into C2a `9b13698` with a real two-parent merge, preserving both histories. It targets `codex/pc-c2a-core-awareness`; land this integration with a merge commit so PR #24 inherits the ancestry. No push, PR creation, game launch or deployment was performed here. E1 is untouched.

## Integration changes

The target is still schema **18**, verified in `Core/Model.cs`. C2a keeps **19**, the frozen schema-18 resolver, next-day 18-to-19 activation, old migrations, held replay, exact money and fixed save/record budgets. Landed B1 nonzero memory payloads and captured contributions remain readable. No cap, tuple arity or per-consumer payload was enlarged.

**C2a and B1 Core integration code changed.** C2a's four memory slots now project the existing B1 saved generation references through O(1) consumer/reference indexes, with B1 expiry and activation checks. No memory writer or identity substitution was added. C2a's common shortlist rank accepts B1's existing bounded contribution; the selected scalar remains in each held shop for the existing B1 offer path. This stays within four memory source slots, 24 source identities, eight shortlisted shops and sixteen logical route requests. Catalogue construction still yields in 64-item phases. Campaigns remain unknown/unused, including with the landed C1 reader present.

**Runtime integration code changed only to combine existing attachment/settings/logging and C2a diagnostics with the landed runtime.** The actual native market/candidate delta from the target is C2a's already-approved code; B1 failure evidence/checkout and B2 feedback guards are retained. B2 Core/Runtime projection and inspector files are unchanged from the target. There is no new feature, economic writer, native authority path or E1 change.

The build exposed one additional combination-only collision: D1 and C2a both declared `FoodAlternativeKind` for different contracts. C2a's enum is renamed `FoodAwarenessAlternativeKind`; managed/external still encode 0/1, preserving hashes and wire behavior. D1's player/rival observation contract is unchanged.

C2a checks (including both complete historical native checklists and LOCKED-OK/NEEDS-INPUT markers) and README notices now live in `docs/checks/c2a-core-awareness.md`, with one `CHECKS.md` index line. All four C2a perf rows move to `docs/perf/c2a-core-awareness.md` with one roadmap index line. Existing slice files stay intact. The schema-19 contract and sync checklist amendments explain populated B1 memory and merge rule v2.

## Conflict resolutions

Both sides' diffs were inspected against real merge base `7a6153c`. Comparing each side again against the last pre-C2a B0b commit `c0af745` separated C2a's own changes from stale copies of squashed slices. Fourteen conflicted files exactly match the landed target; the rest retain C2a's delta atop it. The full inventory follows.

| Conflicted file | Resolution |
| --- | --- |
| `CHECKS.md` | Keep the short target index; add one C2a link. Move all C2a checks, dated README notices, review rounds and both numbered native checklists into docs/checks/c2a-core-awareness.md. |
| `Core/CitySimulator.cs` | Keep B1/B2 inspector and activation behavior; combine both attachment/settings APIs, retiming attachment and C2a yielded preparation. Activate B before preparing an event at that boundary. |
| `Core/FoodChoicePolicy.cs` | Keep landed B1 memory weighting exactly; discard the older pre-B1 copy. |
| `Core/FoodCommerce.cs` | Keep B1 store/settings/experience initialization; add C2a awareness settings, next-day header and the bounded saved-memory provider. |
| `Core/FoodToday.cs` | Keep target decision/tuning fields and add C2a held Awareness image. |
| `Core/FoodWeightedCommerce.cs` | Keep B1 capture/selection/experience paths; add C2a bounded index/source shortlist, propagate B1 rank and captured contributions, and retain legacy pre-activation shortlist behavior. |
| `Core/Model.cs` | Keep target model and C2a schema 19 (target is still 18; no collision). |
| `Core/ResidentInsight.cs` | Keep landed B1/B2 insight and address-format fixes exactly. |
| `Core/ShopMemory.cs` | Keep executable B1 capability contract exactly; regulars projection remains in its landed standalone file. |
| `Persistence/PopulationWire18.cs` | Keep target tuple/shape reader and add C2a 19-aware canonical header/probe bounds. |
| `Persistence/SaveCodec.cs` | Keep target B1 finite nonzero-contribution support and all synced B0/B0b validation; add C2a frozen schema-18 resolver/migration, awareness validation and record charging. |
| `README.md` | Keep target short notice/index convention exactly; relocate both C2a notices to its slice checks file. |
| `Runtime/NativeFoodCandidates.cs` | Keep target metadata/ownership behavior plus C2a direct generation indexes, geometry revisions and capture work bounds; eligibility changes still do not rebuild geometry. |
| `Runtime/NativeFoodMarket.cs` | Keep B1 failure evidence/checkout semantics plus C2a direct scope lookup and bounded capture paths. |
| `Runtime/PersistentCityMod.cs` | Keep every landed runtime slice and B1/B2 diagnostics/configuration; pass both memory and awareness settings at attachment and keep C2a awareness bridge dump/logging. |
| `Runtime/PrototypeSettings.cs` | Keep all target settings/converters, including B1/B2/F0/E2c; add C2a numeric settings/converter with separate JsonIgnore attributes. |
| `Runtime/ResidentInspector.cs` | Keep landed address, visibility and B2 memory-words behavior exactly. |
| `config/persistent-city.json` | Keep every target setting and add the three C2a defaults. |
| `docs/plans/step-a-inspector-traits-choice.md` | Keep target plan and repaired per-slice links exactly. |
| `docs/population-schema18.md` | Keep target B0/B0b/B1/B2 contract, ledger and protected-insertion exception exactly. |
| `docs/reviews/2026-10-31-a3-pr-description.md` | Keep target corrected evidence links exactly. |
| `docs/roadmap.md` | Keep target B1/B2 and all other slice status; add C2a status and one perf index link. Preserve all four C2a perf rows in docs/perf/c2a-core-awareness.md. |
| `tests/HeldFoodTuningTests.cs` | Keep target B1-compatible held-tuning regression exactly. |
| `tests/LegacyWire.cs` | Keep target legacy helper plus C2a schema >=18-to-17 fixture conversion. |
| `tests/ResidentInsightTests.cs` | Keep target address and B1/B2 inspector assertions exactly. |
| `tests/Schema17Tests.cs` | Keep all target migration cases; advance current-write assertions and future-version cases to C2a schema 19. |
| `tests/Schema18ChoiceReplayTests.cs` | Keep target replay coverage; expect the current schema after upgrade. |
| `tests/Schema18ReviewTests.cs` | Keep landed executable-B capability expectations; retain C2a header/future-schema updates. |
| `tests/Schema18Tests.cs` | Keep target B1 active-capability acceptance replacement and B2 unique-consumer fixture; retain all C2a migration/canonical-schema updates. |
| `tools/build.py` | Keep target game references (including InteriorDesigner) and Windows SDK discovery exactly. |
| `tools/food-candidate-regression/Program.cs` | Keep target harness and C2a geometry-revision assertions. |
| `tools/food-ownership-regression/Program.cs` | Keep B1 active bootstrap and B2 generation-read checks; use C2a current-schema future corruption. |
| `tools/native-command-regression/run.py` | Keep target SDK discovery and all native-command harness behavior exactly. |
| `tools/optional-stop-regression/run.py` | Keep target SDK discovery, source list and performance/stationary harness additions exactly. |
| `tools/physical-selection-regression/run.py` | Keep target SDK discovery, interior wiring and selection harness additions exactly. |
| `tools/save-contract-regression/B0Contracts.cs` | Keep target capacity/record-boundary coverage; subtract C2a awareness records from the fixed remaining record allowance. |
| `tools/save-contract-regression/CapacityBench.cs` | Keep target history/full-memory/table/CF32/CF64 fixtures; retain C2a awareness byte/record deductions from existing reserves. |
| `tools/save-contract-regression/EconomyScenario.cs` | Keep target economy digest/replay scenario; allow schema >=18 for frozen legacy comparison. |
| `tools/save-contract-regression/HeldTuningContracts.cs` | Keep landed B1 nonzero scalar acceptance/budget and refusal cases exactly. |
| `tools/save-contract-regression/Program.cs` | Keep F0 payload equality, all target contracts/scenarios/bench modes; add C2a held-image population and awareness contract invocation. |

`docs/population-schema19.md` was C2a-only, so it did not conflict; its contract is retained with a current integration explanation. Auto-merged files, the tests project and all per-slice checks/review records retain the target additions. No test method was lost beyond the target's already-landed B0-refusal-to-B1-acceptance replacement; the paired acceptance, unknown-revision refusal and preservation coverage remains.

## Validation

**Final gate: 45/45 commands passed; 2,197/2,197 tests and 2,197 discovered cases.** CF32/CF64 retain 82.204% / 96.509% of the cap and 2,111,867 / 3,711,867 budgeted records. All required build, regression and simulation commands pass, plus all additional regression tools and four populated-memory capacity fixtures. CF64 retains its existing >95% warning. Offline save/read latency >=30 ms and ten synchronous offline whole-index samples >=30 ms are explicitly flagged in the slice checks/perf notes; these are separate from the unmeasured native gate.

Full sequential foreground validation and capacity results: [command manifest](2026-11-01-c2a-sync-base-results.json). Final results are summarized in the C2a checks file. Every command waits for other dotnet load; no command has an execution timeout.

Three new integration cases verify populated memory for resident/represented consumers, expiry/inactive/replacement isolation, read-only diagnostics, populated schema-18 migration, schema-19 round trips and held contribution replay under changed memory/awareness settings. The original 2,194 tests passed before these additions; the three focused additions also passed.

Offline proof is separate from in-game proof. Under merge rule v2, native checks run in the later batched locked rig on the combined `codex/persistent-city` head; pointer/input-only checks await Peter's 6 November playtest. This sync performs no per-PR rig work and grants no release or ordinary-save deployment clearance. Mod-work p95 <5 ms, every >=30 ms spike, autosave discipline and separate whole-frame tracking remain applicable. The four parked performance hotspots are untouched; no native performance claim is made.
