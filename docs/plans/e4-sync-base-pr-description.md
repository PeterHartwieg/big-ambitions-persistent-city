# Sync approved E4 with E1/F1 integration base

Target: `codex/pc-e4-wider-menu`. Merge this PR with a **merge commit** so the real integration merge ancestry is retained; PR #56 can then be squash-merged into `codex/persistent-city`.

Merge integration head `5877453` (E1) and ancestor `2f12ae9` (F1) into the approved E4 head `506e434`, using merge base `51ce709`. No rebase, squash, cherry-pick, new feature, game access or deployment. E4 retains schema 23, the eight-offer saved envelope, effective live limit four, reserved product identities and the approved missing-price fix. E1 rain capture/settings/choice inputs and F1's NO-GO stop report remain intact.

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

## Minimal integration edits

Relative to the E4 head, this merge imports E1's approved Core/Runtime weather code; relative to the integration head, it imports E4's approved menu code. There are no additional **Core or Runtime code changes** beyond composing those branches. The only Core conflict selects schema 23 and retains both owners' fields. No E1/E4 policy, product permission, route bound, price, checkout, native ownership or other slice behavior was redesigned.

Persistence changes make `MenuPredecessorSchema=20`, expose E1's existing next-day activation as the isolated `MigrateWeather20` step, and admit schema 20 in early capability probing. E4's migration still only sets `MenuPolicy` and schema 23. Schema-20 activation day, resident commitment, consumer weather and held decision weather/multiplier are preserved exactly. D2=21 and C2b=22 remain reserved and refused; they can later insert exact resolvers/migrations below E4 without changing its migration body. Unknown/newer/corrupt data remains atomically preserved.

Tests and tools use exact 18/19/20 source fixtures, retain old four-offer bounds, and add an exact schema-20 projection comparison with nonneutral weather captures. Save-contract accounting independently reconciles menu-header and weather deltas against schema 19. Awareness tooling similarly removes both additions only for its legacy projection.

The additional regression sweep exposed a stale partial native double in `tools/food-candidate-regression/Program.cs`: E4's approved `NativeFoodCandidates` calls `SupportedMenu`, but the fixture still defined `Supported`. Rename that fixture method to match the production API, preserving its exact eligibility expression and all assertions. The initial compile failure and rerun are recorded in E4's validation evidence; no production code changes are involved. The C4 save comparison also deliberately pins schema 23 and compares the merged Core/Persistence with the same source excluding C4 files.

## Validation and measurement provenance

Current results will be recorded in [E4 checks](../checks/e4-wider-menu.md#base-sync-with-e1-and-f1). All original E4 tables/JSON/hash/byte comparisons remain historical measurements of the `51ce709`-based stack **before E1**; neither those results nor E1's archived measurements are relabelled as integrated-tree evidence. The merge adds no per-frame work or claimed performance win; legacy loading now validates the composed migration steps and the saved wire includes both owners' fields.

No per-PR rig under merge rule v2. The existing numbered E4 [6 Nov checklist](../checks/e4-wider-menu.md#in-game-checks-for-6-nov-rig), E1 checklist and F1 handoff remain pending for the combined locked head; pointer/input checks wait for Peter's playtest. This integration provides no release, upload or ordinary-save clearance.
