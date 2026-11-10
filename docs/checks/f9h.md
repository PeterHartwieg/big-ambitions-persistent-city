Status: implemented (offline); not verified by rig

# City acceptance F9h, PC record: the first integrated acceptance run, 10 November 2026

Refs [#96](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/96). Task `pc-f9h-first-acceptance-record`, branch `codex/pc-f9h-first-acceptance-record` from `codex/persistent-city` at `d23bb925` (F9b+F9d+F9f #197 merged). Plan row [F9h](../plans/f9-city-acceptance.md#4-slice-breakdown), Decisions 2, 5, 6 and 7, section 3.10. Numbers: [performance](../perf/f9h.md). This is the PC record. The Mac record and the PC-vs-Mac digest comparison follow in a separate F9h-mac run at the head where this merges. No product, schema or save change. `tools/economy-interventions/acceptance.py` is not edited.

## The run

`python tools/acceptance/run.py full --game-managed <Steam Managed>`, in the foreground, at the base head with a clean tree. `PC_DOTNET_WAIT` was set to the PC's wait script. Default price weight (no `--price-weight`).

| Field | Value |
| --- | --- |
| runId | `20261110T131314Z-d23bb925` |
| Source | `d23bb9256860833175568bb3481c8817bf07d716`, clean (`dirty: false`, 0 untracked) |
| Host | Windows AMD64 (`windows-x64`), .NET SDK 10.0.402, Python 3.12.10 |
| Game fingerprint set | `steam-1331550-build-25482473`, all 28 assemblies matched, `differing: []` |
| `PersistentCity.dll` SHA-256 | `f72508261b3ce3c5b83400610ab60ad5d5cb8ef56b4683e4a1bfd7c11616e85e` (the same as F9b+F9d+F9f's base build) |
| Compiled defaults | `FoodChoicePriceWeight` 1.2, `FoodChoiceHouseholdWeight` 1, event budgets 10000 / 1; no declared override |
| `config/persistent-city.json` SHA-256 | `2c14a2bc…d34691` |
| `acceptance.json` SHA-256 | `ce3e079173e3713e1e37dee895a0dd0285c6f66c73323fba3cdc9434155bbb77` |
| Exit | 0 (summary `incomplete`, no `--strict`) |

### Summary per class

| Class | Verdict |
| --- | --- |
| semantic | **pass** (every semantic stage passed) |
| native | **incomplete**: build fingerprint matched; nine rig checks not run (`f9-2` … `f9-9`, `f6b-2`) and the two routed economy rows `native-wholesale-costs` and `native-staffed-throughput` pending |
| performance | not-supplied (F4 supplies it; F9 does not time frames) |
| gameplay | not-assessed |
| **summary** | **incomplete**: failed `[]`, incomplete the 11 native items above, deviations `[]` |

### Stages

| Stage | Verdict | Result | Wall (s) |
| --- | --- | --- | --- |
| provenance | pass | recorded | 0.0 |
| build | pass | exit 0, fingerprint set above | 5.9 |
| unit | pass | 4,003 passed, 0 failed, 0 skipped | 436.3 |
| population | pass | 8 passed (from the `unit` TRX) | 0.1 |
| economy | pass | 85 runs, complete matrix, `failedCriteria` empty, 6 unmeasured criteria routed | 475.9 |
| saves | pass | 82 passed (from the `unit` TRX) | 0.1 |
| movement | pass | 76 arms equal their reference, self-checks and E2 rows pass, no expected-red arm | 617.7 (135.4 of it waiting for another test run) |
| city-s | pass | 27 passed | 8.9 |
| city-m | pass | 18 passed | 41.4 |

No stage failed, so there is no failure to record.

## The F6h matrix at weight 1.2

The first full F6h matrix at the 1.2 default (capacity-bound profile, 60 meals per hour, rival listed; writer 1; 85 runs; 6,832,980 daily conservation checks). `acceptance.py` status `partial`, exit 1, as on every complete sweep.

**Adapter verdict: pass.** `completeInterventionMatrix` true, `failedCriteria` empty (the household-threshold criterion did not miss: all five control seeds are within the weight-1 thresholds), recorded `priceWeight` 1.2 equals the compiled default. The six `unmeasuredCriteria` were routed:

| Criterion | Routed to |
| --- | --- |
| measured native wholesale costs | `native`, row `native-wholesale-costs` (incomplete; pending RIG-WHOLESALE) |
| native staffed-throughput calibration | `native`, row `native-staffed-throughput` (incomplete; F6b check 2) |
| every gain has a measured cost / no Pareto dominant arm | `superseded` ([price-weight decision](../decisions/2026-11-08-price-weight.md)) |
| F1 cadence byte identity | `population`, `city-s`, `city-m` (all pass) |
| F2 body-cap flagged differences | `movement` (pass) |
| full section 6 perf and 100000-consumer stress | `performance` (F4) |

Every intervention family has an effect outside the control band. The +50% whole-menu arm against control, mean of five seeds over weeks 3 and 4: items −5.6%, meals −1.7%, revenue +29.7%, arc elasticity −0.144; own capacity bound in the control. These are the capacity-bound profile's numbers, not a native measurement (that is rig check `f9-9`).

### Household seed bands at 1.2 (hand-off to the F6 owner)

Derived with the existing `derive_household_thresholds` (`tools/economy-interventions/acceptance.py:99-103`) on this run's five control seeds, week 6. File: [`tools/acceptance/evidence/windows-x64/f6h-household-bands.json`](../../tools/acceptance/evidence/windows-x64/f6h-household-bands.json) (also holds the week-6 bands and per-seed values). `acceptance.py` computed the same values in its own `derivedFromThisRun`.

| Threshold | Current (`HOUSEHOLD_THRESHOLDS`, measured at weight 1) | Derived at weight 1.2 |
| --- | --- | --- |
| `week6ArrearsShareMax` | 0.0047 | 0.0049 |
| `week6UnfedOwnerDaysMax` | 5.7 | 6.1 |
| `week6P10DollarsMin` | 658.0 | 655.0 |

Not adopted: `acceptance.py`, `FoodChoicePriceWeight` and `FoodChoiceHouseholdWeight` are unchanged, because the price-weight decision defers fine-tuning until closer to release. Adopting the bands is an F6 calibration change; after it, the economy adapter can drop its household-threshold exception. At this head the exception was not needed.

## Native-evidence ledger and the rig manifest

`rig-manifest` at this head (fingerprint `steam-1331550-build-25482473`), committed as [`tools/acceptance/evidence/rig-manifest.json`](../../tools/acceptance/evidence/rig-manifest.json): **native batch due** (11 rows at debt ≥ 3); 17 rows unproven, 0 stale, 0 fresh; one row unscheduled (`native-wholesale-costs`, needs RIG-WHOLESALE). Scheduling priority (the manifest's order: debt first, `f9-1` pinned first and `f9-10` last; the execution order is under *In-game checks*):

| # | Check | Debt | Mode | Rows |
| --- | --- | --- | --- | --- |
| 1 | `f9-1` Build provenance at the rig head | – | locked-PC | |
| 2 | `f9-8` Older-schema city refused and kept | 28 | locked-PC | native-save-contract |
| 3 | `f9-4` Seven days with daily save/reload | 19 | locked-PC | save-epoch, buylive-sale-authority |
| 4 | `f9-7` Research and a food truck in the same city | 7 | input-only | agency-billing, agency-dialog, streetfood-bridge |
| 5 | `f9-2` FT-F9 fixture with a player food business | 5 | locked-PC | subway-portals, world-census |
| 6 | `f9-6` Detail transitions and in-flight reload | 5 | locked-PC | physical-bodies, native-road-network |
| 7 | `f6b-2` Native staffed throughput | 5 | locked-PC | native-staffed-throughput |
| 8 | `f9-5` Timestamped native changes across cadences | 3 | locked-PC | hourly-settlement, native-clock-and-skip, native-shop-inputs |
| 9 | `f9-9` Price response on the real shop at weight 1.2 | 3 | locked-PC | price-weight-response |
| 10 | `f9-3` Active capabilities | 2 | locked-PC | rlo-donor-contract |
| 11 | `f9-10` Merged summary for the batch | – | locked-PC | |

`summarize --result <run>/acceptance.json` with no rig results: summary `incomplete`, the same 11 native items, `native` incomplete; no ledger row updated (the written `native-assumptions.json` has the same content as the committed ledger). Merged `acceptance.json` SHA-256 `e19493a3b5cabe999bf5218af90bd6dfc508f8e1fd405f1024ec23e7ec1ad22b`.

## Cross-machine determinism (section 3.10)

Evidence is kept per machine under `tools/acceptance/evidence/<os>-<arch>/`. `python tools/acceptance/evidence.py compare` reads every machine's `summary.json` and writes [`comparison.json`](../../tools/acceptance/evidence/comparison.json). It compares 174 simulation digests: the F6h `acceptance.json`, the 85 F6h run files and 88 movement harness save hashes. Build digests (the DLL and the game assemblies) are machine inputs and are not compared. A difference is `platform-divergent` with the first differing digest and does not fail the semantic class.

| Machine | Status | runId | Semantic | Digests |
| --- | --- | --- | --- | --- |
| PC (`windows-x64`) | recorded | `20261110T131314Z-d23bb925` | pass | 174 recorded |
| Mac (`macos-arm64`) | pending F9h-mac, at the head where F9h merges | – | – | – |
| Linux box | not run: the Linux box does not build or test (team rule) | – | – | – |

Comparison verdict: `pending` (one machine recorded). The F9h-mac run runs `full` on the Mac at the head where this PR merged, then `evidence.py record --result <its acceptance.json>` and `evidence.py compare`, and adds its column here. The PC record stays at `d23bb925`: `compare` accepts records from different commits when `git diff` between them touches no simulation input (`Core`, `Persistence`, `Runtime`, `Compatibility`, `config`, `tests`, `tools/economy-interventions`, `tools/movement-equivalence`, the stage catalogue, the movement expectations, `tools/acceptance/run.py` and `economy_stage.py`), the recorded settings are equal and no record comes from a dirty tree with another commit or diff. This PR changes none of them, so the merged head compares with the PC record. If a simulation input changes before the Mac run, the verdict is `different-source`, and F9h-mac runs `full` on the PC again at the Mac's head and records it before comparing.

## Reference run under `tools/acceptance/evidence/`

| File | Bytes | Content |
| --- | --- | --- |
| `machines.json` | 489 | declared machines and the reason for each one not recorded |
| `windows-x64/summary.json` | 43,459 | provenance, classes, summary, ledger state, every stage's verdict, seconds, metrics and digests, the F6h run digests, `acceptanceSha256` |
| `windows-x64/f6h-household-bands.json` | 1,896 | the 1.2 seed bands above |
| `rig-manifest.json` | 16,645 | the batch manifest and ledger state at this head |
| `comparison.json` | 39,938 | the digest comparison |

No TRX, log, binary or harness dump; no absolute path, user name or machine name (checked with grep). Raw evidence stayed under `build/acceptance/20261110T131314Z-d23bb925/` and is not committed.

## Tooling added

`tools/acceptance/evidence.py` (`record`, `compare`) and `tools/acceptance/test_evidence.py` (10 tests: per-machine layout, no path, bands equal F6's function on a synthetic run, PC-only is pending with the Mac pending and the Linux box not run by rule, equal digests are cross-machine deterministic, one difference is platform-divergent with its name, commits that git cannot compare are not compared, a later head with only docs changes is compared, a simulation input change between the commits or a dirty record is `different-source`). Nothing in `run.py`, `stages.json` or the economy adapter changed.

## Offline validation

- The `full` run above (it runs the build and the whole dotnet suite).
- Required build, `dotnet test tests/PersistentCity.Tests.csproj` and `python -m unittest discover -s tools/acceptance -p test_*.py`: results in the PR description.
- No game, rig, ModsLocal or bridge was used; the installed game's DLLs were only read and hashed.

## In-game checks

From the plan's [section 7](../plans/f9-city-acceptance.md#7-in-game-checks-for-the-next-batched-rig): the checks this record feeds, for the next combined native/perf batch. All run on the headless agent rig against the development head, in the disposable **FT-F9** city with one player food business, never in Peter's ordinary save. The rig records the autosave setting, turns autosave off in the isolated profile, checks it is off before each measured window (also after reload) and restores it at cleanup; functional steps that save run outside F4's timing windows. None is left to Peter, a playtest or a by-hand check. Setup of the batch: execution order is fixture prerequisites first, then the manifest's priority. Run `f9-1`, then `f9-2`, which creates and verifies the shared checkpoint `f9-city-ready` (with its copy for `f9-4` and `f9-9`); stop the checkpoint-dependent checks if `f9-2` fails. Then run the remaining checks in the manifest's priority from `tools/acceptance/evidence/rig-manifest.json` (table above): `f9-8` (its own older-schema city, no checkpoint), `f9-4`, `f9-7`, `f9-6`, `f6b-2`, `f9-5`, `f9-9`, `f9-3`, and `f9-10` last. Use an offline `full` `acceptance.json` at the same head as the rig build (this record's run if the head is still `d23bb925`, otherwise a new `full` run at the batch head). Each result is a `persistentcity-acceptance-1` stage record of class `native` with the check ID (pending capability **RIG-ACCEPT**). **Locked-PC**: runs on the locked rig PC through FT console and rig commands. **Input-only**: also needs injected UI input.

1. **`f9-1` Build provenance at the rig head.** Locked-PC.
   - Setup: head build installed by RIG-MODSET.
   - Steps: on the rig, `python tools/build.py --game-managed <rig Managed> --provenance <out>/build.json` (read-only hashing of the installed game's assemblies); hash the installed `PersistentCity.dll`.
   - Expected: `set` is a declared set (`steam-1331550-build-25482473` unless the game updated); the DLL hash equals the offline `full` run's `provenance.artifact` at the same commit (`f72508…16e85e` at `d23bb925`). An `unknown` set leaves `native` incomplete and marks every ledger row stale.
   - Rig automation: existing RIG-MODSET (hashed DLL set); pending RIG-ACCEPT.
2. **`f9-9` Price response on the real shop at weight 1.2.** Locked-PC.
   - Setup: `f9-city-ready` (check `f9-2`) and a copy; read the loaded `FoodChoicePriceWeight` from the PC config readback.
   - Steps: raise every price by 50% in one copy for two game days; leave the other unchanged.
   - Expected: weight 1.2 loaded; fewer meals in the raised copy and revenue rising by well under 50%, the direction of this record's offline F6h result at 1.2 (meals −1.7%, revenue +29.7% in the capacity-bound profile). Recorded as native evidence for row `price-weight-response`, not a calibration.
   - Rig automation: existing RIG-SHOPEDIT, RIG-WEEKS, FT `console`; pending RIG-LEDGER, RIG-PCCONFIG readback.
3. **`f9-10` Merged summary for the batch.** Locked-PC (no game input; offline merge on the rig PC).
   - Setup: the offline `full` `acceptance.json` at the batch head, the batch's check records (`f9-1` … `f9-9`, `f6b-2`) and F4's performance record.
   - Steps: `python tools/acceptance/run.py summarize --result <full>/acceptance.json --rig <checks.json> --performance <f4.json>`, then `python tools/acceptance/run.py rig-manifest --result <merged>/acceptance.json`.
   - Expected: one summary verdict over semantic and native; every `requiredChecks` entry satisfied by its record or listed `incomplete`; a failed check fails `native`; performance copied with F4's own verdict; gameplay `not-assessed`; `native-assumptions.json` beside the merged result records the head and fingerprint set for every row whose check passed, and the manifest from it lists those rows `fresh`. No absolute path or user name in the result.
   - Rig automation: pending RIG-ACCEPT.

The other checks in the manifest (`f9-2` … `f9-8`, `f6b-2`) are defined in section 7 and their own slices' records; this record only sets their order.
