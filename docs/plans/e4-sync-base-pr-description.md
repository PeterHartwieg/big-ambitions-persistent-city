# Sync approved E4 with the latest E1/F1/A5 integration base

Target: `codex/pc-e4-wider-menu`. Merge this PR with a **merge commit** so the real integration merge ancestry is retained; PR #56 can then be squash-merged into `codex/persistent-city`.

Merge `5877453` (E1) and ancestor `2f12ae9` (F1) into the approved E4 head `506e434`, using merge base `51ce709`. When integration advanced to `0ffb819` (A5), merge it again using merge base `5877453`. No rebase, squash, cherry-pick, new feature, game access or deployment. E4 retains schema 23, the eight-offer saved envelope, effective live limit four, reserved product identities and the approved missing-price fix. E1 rain capture/settings/choice inputs, F1's NO-GO stop report and A5's inspector/acceptance changes remain intact.

## Conflict resolutions

Both sides' diffs against `51ce709` were read for each conflicted file:

| Conflicted file | Resolution |
| --- | --- |
| `CHECKS.md` | Keep every incoming index entry, including E1/F1, and exactly one E4 entry. Qualify E4's original figures as pre-E1 measurements. |
| `Core/Model.cs` | Current schema stays 23; retain E4 `MenuPolicy` and E1 `WeatherStartsDay`/resident `W`. |
| `Persistence/SaveCodec.cs` | Combine strict shape/semantic validation and converters; retain exact resolvers for 18/19 and add exact 20. Older imports pass through 19→20 before isolated 20→23. Remove the duplicate independently added schema-19 resolver. |
| `README.md` | Keep E4's menu description and F1's NO-GO notice; update supported migration range through 20. No validation evidence appended here. |
| `config/persistent-city.json` | Keep all three E4 menu settings and all four E1 weather settings with their approved defaults. |
| `tests/FoodAwarenessMemoryIntegrationTests.cs` | Retain E1's full 18/19/20 matrix, rain/memory checks and changed-input replay; add current schema 23 and strip only fields absent from each old format. |
| `tests/FoodAwarenessTests.cs` | Use E1's weather-free legacy fixture plus E4-free legacy header; assert migration reaches 23. |
| `tests/MarketResearchTests.cs` | Keep unchanged-save assertion and pin current schema 23. |
| `tests/Schema17Tests.cs` | Pin genuinely future 24/25/1000; reserved 21/22 refusals remain explicitly tested by E4. |
| `tests/Schema18ReviewTests.cs` | Expect canonical schema-23 header; retain E1's reserved-schema/invalid bootstrap cases. |
| `tests/Schema18Tests.cs` | Use schema-23 hexadecimal corruption and E1's weather-suffixed escaped-memory corruption; retain exact refusal/source preservation. |
| `tools/save-contract-regression/AwarenessContracts.cs` | Preserve E1's exact old-format projection and awareness byte equality; project E4's menu header out separately rather than weakening the equality. |

Automatic merges retain both roadmap status/perf-log entries and all existing slice checks/plans. Food choice combines captured rain multipliers with the approved E4 offer limit and derived taste inputs. Runtime retains both startup/settings paths, supply/catalogue behavior and the missing-price fix.

The second real merge, against A5 `0ffb819`, conflicts only in `CHECKS.md`, `README.md` and `docs/roadmap.md`. Both sides' diffs against its merge base `5877453` were inspected. Keep both A5/E4 checks entries, README descriptions and perf-log entries, while the current README schema remains 23 with migration through 20. All A5 checks, historical measurement qualifications, plans, reviews and tests are retained. `tests/A5IntegratedAcceptanceTests.cs` changes only its current-schema assertion from 20 to 23; its four old-source migration cases and all inspector/weather/continuation assertions remain. A5's `Core/ResidentInsight.cs` is imported byte-for-byte; there is no integration-specific change to its behavior.

## Minimal integration edits

Relative to the E4 head, these merges import E1's approved Core/Runtime weather code and A5's approved Core inspector code; relative to the integration head, they import E4's approved menu code. There are no additional **Core or Runtime code changes** beyond composing those branches. The only Core conflict selects schema 23 and retains both owners' fields. No E1/E4 policy, product permission, route bound, price, checkout, native ownership or other slice behavior was redesigned.

Persistence changes make `MenuPredecessorSchema=20`, expose E1's existing next-day activation as the isolated `MigrateWeather20` step, and admit schema 20 in early capability probing. E4's migration still only sets `MenuPolicy` and schema 23. Schema-20 activation day, resident commitment, consumer weather and held decision weather/multiplier are preserved exactly. D2=21 and C2b=22 remain reserved and refused; they can later insert exact resolvers/migrations below E4 without changing its migration body. Unknown/newer/corrupt data remains atomically preserved.

Tests and tools use exact 18/19/20 source fixtures, retain old four-offer bounds, and add an exact schema-20 projection comparison with nonneutral weather captures. Save-contract accounting independently reconciles menu-header and weather deltas against schema 19. Awareness tooling similarly removes both additions only for its legacy projection.

The additional regression sweep exposed a stale partial native double in `tools/food-candidate-regression/Program.cs`: E4's approved `NativeFoodCandidates` calls `SupportedMenu`, but the fixture still defined `Supported`. Rename that fixture method to match the production API, preserving its exact eligibility expression and all assertions. The initial compile failure and rerun are recorded in E4's validation evidence; no production code changes are involved. The C4 save comparison also deliberately pins schema 23 and compares the merged Core/Persistence with the same source excluding C4 files.

## Validation and measurement provenance

Final head `caa28f8` passes the production build, **2,407/2,407 unit tests** (2,407 listed, zero failed/skipped), all requested regressions/demos and the additional standalone programs, including A5: **50/50 top-level checks**. E4 reports **25,165 assertions**; exact schema-20 replay, old four-offer refusals, reserved/newer refusal, weather/menu byte ledgers and default legacy/weighted economy comparisons pass. C4-excluded/included schema-23 fixtures remain byte-identical. Runs were sequential/foreground with the prescribed guard and no execution deadlines; cleanup found zero task-path dotnet/testhost processes. Final fetch confirmed latest integration `0ffb819` is retained as an ancestor.

[E4 checks](../checks/e4-wider-menu.md#base-sync-with-e1-and-f1) distinguish the completed initial pre-A5 run from [final command results, log hashes, benchmarks and preservation proof](../checks/e4-sync-base-results.json). All 25 incoming checks entries remain, with one E4 line. Initial and final results preserve the fixture compile failure/fix rather than hiding it. All original E4 tables/JSON/hash/byte comparisons remain historical measurements of the `51ce709`-based stack **before E1**; neither those results nor E1/A5 archived measurements are relabelled as integrated-tree evidence. The merge adds no per-frame work or claimed performance win; legacy loading now validates the composed migration steps and the saved wire includes both owners' fields.

Current population fixture: 120,257 characters versus its exact schema-19 projection of 119,083. Current four/eight-product history saves: 8,649,017 / 10,135,865 ASCII bytes, 32 bytes above original E4 because of E1 weather fields. Record counts stay exact. All **42/42 bulk serialization samples >=30 ms** are flagged, max **234.3412 / 233.2704 ms**; these are offline full saves. No E4 selection/supply/update sample reaches 30 ms. Shared-PC, JIT/tiering and sequential field-double limits prevent causal/native timing claims; command wall times can include nested guard waits. The four parked hotspots remain parked.

No per-PR rig under merge rule v2. The existing numbered E4 [6 Nov checklist](../checks/e4-wider-menu.md#in-game-checks-for-6-nov-rig), E1 checklist and F1 handoff remain pending for the combined locked head; pointer/input checks wait for Peter's playtest. This integration provides no release, upload or ordinary-save clearance.
