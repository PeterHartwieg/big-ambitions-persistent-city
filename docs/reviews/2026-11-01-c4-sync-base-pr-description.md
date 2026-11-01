# C4 sync with integrated development (1 November 2026)

PR #30 conflicts because its approved inactive engine was based on B0b before the newer slices landed. Merge development `692b8b5778b5b202eebcc9d8f73b6d6dccceef93` into C4 `b7b3b04` using a real two-parent merge, preserving merge base `7a6153cfa9bb88e28dea905b285b4273c3e268c7`. This integration PR targets `codex/pc-c4-research-engine` and must land with a merge commit; PR #30 can then squash into development.

The incoming Core/Runtime/Persistence implementations, tests and harnesses are preserved. The four C4 Core files are byte-identical to approved C4. No additional C4 or other slice Core/runtime behavior changes were made beyond importing the base: the only differences from incoming Runtime are the already approved C4 settings/converters and once-per-load Clamp call. No payment, UI, bridge command, scheduler or Runtime input reader was introduced. `NoPaymentPath` always denies. B1 is now present as a possible future research input, but C4 does not consume it; loyalty distributions and rival share still return unknown.

Population schema remains **19**. Every Persistence file and Core/Model.cs matches incoming development exactly. The C4 unit assertion now pins schema 19 while retaining exact before/after payload equality; the offline save comparison now exports the integrated source without C4 (rather than historical B0b), requires schema 19 in both fixtures and compares literal UTF-8 bytes with no exclusions. Historical B0b measurements remain preserved as historical evidence. No save contract was loosened.

Read both merge-base diffs for conflict intent and compared approved C4 against B0b to separate C4 additions from obsolete stacked-source copies. The ignored `build/c4-sync/diffs/` retains both per-file diffs; source/indices/checklist preservation is independently audited. All auto-merged production files also match their owning parent; the incoming tests project and C4 fixture include coexist.

## Every conflict and resolution

| Conflicted file | Resolution |
| --- | --- |
| `CHECKS.md` | Keep every incoming index line; move the complete C4 checks and locked/input checklist into docs/checks/c4-market-research.md and add one index line. |
| `Core/CitySimulator.cs` | Keep newer slice implementation exactly. C4 never changed this file; no C4-specific integration modification. |
| `Core/FoodChoicePolicy.cs` | Keep newer slice implementation exactly. C4 never changed this file; no C4-specific integration modification. |
| `Core/FoodCommerce.cs` | Keep newer slice implementation exactly. C4 never changed this file; no C4-specific integration modification. |
| `Core/FoodToday.cs` | Keep newer slice implementation exactly. C4 never changed this file; no C4-specific integration modification. |
| `Core/FoodWeightedCommerce.cs` | Keep newer slice implementation exactly. C4 never changed this file; no C4-specific integration modification. |
| `Core/Model.cs` | Keep current schema-19 base exactly. C4 never changed this file; no C4 payload/schema delta. |
| `Core/ResidentInsight.cs` | Keep newer slice implementation exactly. C4 never changed this file; no C4-specific integration modification. |
| `Core/ShopMemory.cs` | Keep newer slice implementation exactly. C4 never changed this file; no C4-specific integration modification. |
| `Persistence/PopulationWire18.cs` | Keep current schema-19 base exactly. C4 never changed this file; no C4 payload/schema delta. |
| `Persistence/SaveCodec.cs` | Keep current schema-19 base exactly. C4 never changed this file; no C4 payload/schema delta. |
| `README.md` | Keep incoming short README exactly; move the complete C4 dated notice to its checks file. |
| `Runtime/NativeFoodCandidates.cs` | Keep newer slice implementation exactly. C4 never changed this file; no C4-specific integration modification. |
| `Runtime/NativeFoodMarket.cs` | Keep newer slice implementation exactly. C4 never changed this file; no C4-specific integration modification. |
| `Runtime/PersistentCityMod.cs` | Keep all newer slice wiring; retain C4 once-per-load settings Clamp call. No research caller. |
| `Runtime/PrototypeSettings.cs` | Keep all newer flat settings/converters; preserve both C4 flat properties, nested settings object and converters literally. |
| `Runtime/ResidentInspector.cs` | Keep newer slice implementation exactly. C4 never changed this file; no C4-specific integration modification. |
| `config/persistent-city.json` | Keep every incoming default; retain C4 catchment 500 and consumers/unit 256. |
| `docs/plans/step-a-inspector-traits-choice.md` | Keep newer source contract, plan, evidence and split-layout links exactly. C4 never changed this file. |
| `docs/population-schema18.md` | Keep newer source contract, plan, evidence and split-layout links exactly. C4 never changed this file. |
| `docs/reviews/2026-10-31-a3-pr-description.md` | Keep newer source contract, plan, evidence and split-layout links exactly. C4 never changed this file. |
| `docs/roadmap.md` | Keep all incoming status, performance gate and index lines; append C4 inactive status and one perf index line; preserve the original C4 win row in docs/perf/c4-market-research.md. |
| `tests/HeldFoodTuningTests.cs` | Keep newer migration, replay, held tuning and inspector assertions exactly. C4 never changed this file; retain all C4 tests separately. |
| `tests/LegacyWire.cs` | Keep newer migration, replay, held tuning and inspector assertions exactly. C4 never changed this file; retain all C4 tests separately. |
| `tests/ResidentInsightTests.cs` | Keep newer migration, replay, held tuning and inspector assertions exactly. C4 never changed this file; retain all C4 tests separately. |
| `tests/Schema17Tests.cs` | Keep newer migration, replay, held tuning and inspector assertions exactly. C4 never changed this file; retain all C4 tests separately. |
| `tests/Schema18ChoiceReplayTests.cs` | Keep newer migration, replay, held tuning and inspector assertions exactly. C4 never changed this file; retain all C4 tests separately. |
| `tests/Schema18ReviewTests.cs` | Keep newer migration, replay, held tuning and inspector assertions exactly. C4 never changed this file; retain all C4 tests separately. |
| `tests/Schema18Tests.cs` | Keep newer migration, replay, held tuning and inspector assertions exactly. C4 never changed this file; retain all C4 tests separately. |
| `tools/build.py` | Keep newer regression assertions, native reference/build support and harness wiring exactly. C4 never changed this file. |
| `tools/food-candidate-regression/Program.cs` | Keep newer regression assertions, native reference/build support and harness wiring exactly. C4 never changed this file. |
| `tools/food-ownership-regression/Program.cs` | Keep newer regression assertions, native reference/build support and harness wiring exactly. C4 never changed this file. |
| `tools/native-command-regression/run.py` | Keep newer regression assertions, native reference/build support and harness wiring exactly. C4 never changed this file. |
| `tools/optional-stop-regression/run.py` | Keep newer regression assertions, native reference/build support and harness wiring exactly. C4 never changed this file. |
| `tools/physical-selection-regression/run.py` | Keep newer regression assertions, native reference/build support and harness wiring exactly. C4 never changed this file. |
| `tools/save-contract-regression/B0Contracts.cs` | Keep newer regression assertions, native reference/build support and harness wiring exactly. C4 never changed this file. |
| `tools/save-contract-regression/CapacityBench.cs` | Keep newer regression assertions, native reference/build support and harness wiring exactly. C4 never changed this file. |
| `tools/save-contract-regression/EconomyScenario.cs` | Keep newer regression assertions, native reference/build support and harness wiring exactly. C4 never changed this file. |
| `tools/save-contract-regression/HeldTuningContracts.cs` | Keep newer regression assertions, native reference/build support and harness wiring exactly. C4 never changed this file. |
| `tools/save-contract-regression/Program.cs` | Keep newer regression assertions, native reference/build support and harness wiring exactly. C4 never changed this file. |

There was no conflict in `docs/market-research.md` or `docs/plans/steps-b-e.md`: retain the incoming C3 fee-path findings and planning content, and clarify the C4 contract's integrated baseline without adding B1 wiring. C4 checks, original notice, in-game checklist and perf win move into the new per-slice layout; all C2a/E3/E2b and other incoming index lines survive.

## Validation

**Validation:** **48/48 executable checks pass** (49 command entries including the explicit guard): Steam-reference build, **2,337 unit tests / 2,337 discovered**, all **33 top-level regression projects** including C4, four Python harnesses, three demos, exact C4 save comparison, pickup regression, optional-stop performance/findings and donut isolation/differential checks. C4 retains all **44** cases. Every dotnet test/run was guarded in the foreground with no imposed execution deadline and server reuse disabled. All invoked processes completed. [Command exits, log hashes/tails, production hashes and preservation audit](2026-11-01-c4-sync-base-results.json); ignored raw logs: `build/c4/gates/` and `build/c4-sync/extras/`.

Literal schema-19 population bytes **41,406 -> 41,406**, food bytes **65,719 -> 65,719**, both zero delta. Population SHA-256 `084fe4d9b3c532b5f52efcbd797412978624e6b2de1e724442259a43550e9d16`; food `f94a187df5f0dcff519fe6a606a277da6cb3a301519c2969651f0e75a2b39ed9`. Save-contract fixture remains 119,083 characters at schema 19, digest `D7FF8294F56C3B3D5AF736540274E68E9848319B34658859FC15E0D4CDB1F3A2`; economy remains 849 orders, 797 purchases and two uncertain. Game DLL SHA-256 `da0b617f4b76249d8fef71107a57e18810ab3a202f5e3724cb1e94cbe2710833`. No build/test integration fix was necessary; only C4's current-schema assertion, baseline comparison and combined validation runner required integration updates.

C4 records **13,200 continuation samples**, largest unit p95 **0.0649 ms**, maximum **0.7624 ms**, zero >=30 ms unit spikes, zero nonfinal allocation, 2,664 B final publication and zero idle reads/allocation/native/route requests. D1's 20k-place aggregate build **34.1477 ms across units** is flagged >=30 ms; C1 cold p95 **5.9863 ms**, max **9.0152 ms**; food-routine batch max **8.7842 ms**. All are synthetic offline costs, not native combined mod-work or whole-frame proof. No new perf change or new win is claimed.

Final fetch still resolves development to `692b8b5`; it is the incoming merge parent. `git diff --check` against that base passes. Older imported evidence files retain their preexisting whitespace; no unrelated cleanup.

No game or rig was launched; no test profile, lease, Steam/Workshop, ordinary save or WSL access.

Merge rule v2 applies: batched combined locked rig on the development head later, with pointer/input-only checks waiting for Peter's 6 Nov playtest. This sync supplies offline proof only and no release/deployment authorization. No new perf-affecting change or new performance win; the original C4 direct-validation win remains in its indexed perf file. Native mod-work p95 <5 ms, every >=30 ms spike flagged, separate whole-frame tracking and the four parked hotspots remain unchanged.
