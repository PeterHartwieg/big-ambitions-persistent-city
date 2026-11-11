Status: implemented (offline); not verified by rig

# City acceptance F9h, PC record: the first integrated acceptance run, 10 November 2026

Refs [#96](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/96). Task `pc-f9h-first-acceptance-record`, branch `codex/pc-f9h-first-acceptance-record` from `codex/persistent-city` at `d23bb925` (F9b+F9d+F9f #197 merged). Plan row [F9h](../plans/f9-city-acceptance.md#4-slice-breakdown), Decisions 2, 5, 6 and 7, section 3.10. Numbers: [performance](../perf/f9h.md). This is the PC record. The Mac record and the PC-vs-Mac digest comparison follow in a separate F9h-mac run at the head where this merges. No product, schema or save change. `tools/economy-interventions/acceptance.py` is not edited.

## Same-head re-record at `b3e5a910`, 10 November 2026

Task `pc-f9h-pc-record-at-merged-head-2`. The first record below was taken at `d23bb925`, before F8b-g #194 (`297f32ea`) changed Core, Runtime, Compatibility and tests, so a Mac run at the merged F9h head (`b3e5a910`) would compare `different-source` with it. This run is the same-head comparison baseline: `full` again on the PC at `b3e5a910`, clean tree, `PC_DOTNET_WAIT` set, default price weight, in the foreground. The committed `windows-x64/` files, `comparison.json` and `rig-manifest.json` are from this run. The first record stays below as the original record. The earlier attempt `20261110T200733Z-b3e5a910` was incomplete and is not used.

| Field | Value |
| --- | --- |
| runId | `20261110T201123Z-b3e5a910` (20:11:23–20:34:09 UTC) |
| Source | `b3e5a9101fe065662c0baf3aedab01caa123773e`, clean (`dirty: false`, 0 untracked) |
| Host | Windows AMD64, .NET SDK 10.0.402, Python 3.12.10 (unchanged) |
| Game fingerprint set | `steam-1331550-build-25482473` (unchanged) |
| `PersistentCity.dll` SHA-256 | `0f34e6e22e5157f7aed25b41fe8cb2af14727275cdea99fbf64a5bd5be476c2d` (changed: F8b-g edits the mod) |
| Compiled defaults, config SHA-256 | unchanged (1.2 / 1, `2c14a2bc…d34691`); no declared override |
| `acceptance.json` SHA-256 | `fc04d55943349e3389b1535471f1b3ebe8c71c34f2cb59cd399968bd2e36cde5` |
| Exit | 0 (summary `incomplete`, the same 11 native items; failed `[]`, deviations `[]`) |

| Stage | Verdict | Result | Wall (s) | First record |
| --- | --- | --- | --- | --- |
| provenance | pass | recorded | 0.0 | 0.0 |
| build | pass | exit 0 | 4.9 | 5.9 |
| unit | pass | 4,128 passed, 0 failed (was 4,003) | 391.8 | 436.3 |
| population | pass | 8 passed | 0.1 | 0.1 |
| economy | pass | 85 runs, complete matrix, `failedCriteria` empty | 446.2 | 475.9 |
| saves | pass | 84 passed (was 82) | 0.1 | 0.1 |
| movement | pass | 76 arms equal their reference | 467.8 | 617.7 |
| city-s | pass | 27 passed | 10.0 | 8.9 |
| city-m | pass | 18 passed | 43.8 | 41.4 |

Changes against the first record:

- Stage verdicts: none; every semantic stage passed again.
- Digests: every simulation digest equals the first record's (the F6h `acceptance.json`, the 85 run files and the movement save hashes). Only the build digest `PersistentCity.dll` differs, which is a machine input and is not compared.
- Household bands at 1.2: unchanged (0.0049, 6.1, 655.0; all five seeds inside the current thresholds). Not adopted.
- Test counts: `unit` +125 and `saves` +2, from the tests added by F8b-g and F9h.
- Native manifest: one more run at a head without native proof, so each due row's debt rose by one; `rlo-donor-contract` reached debt 3, so `f9-3` is now due and sorts before `f9-9`. Still 17 rows unproven, 0 fresh, `native-wholesale-costs` unscheduled. Native stays incomplete until the combined batch.
- Wall time: 1,364.5 s against 1,586.5 s, mostly because this run waited only about 4 s in total for other test runs (see [performance](../perf/f9h.md)).

The sections below describe the first record at `d23bb925` unless they say otherwise.

## The run (first record, `d23bb925`)

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
| PC (`windows-x64`) | recorded at `b3e5a910` (committed); first record at `d23bb925` superseded as the comparison baseline | `20261110T201123Z-b3e5a910` | pass | 174 recorded, equal to the first record |
| Mac (`macos-arm64`) | recorded at `b5f2a1de` (F9h-mac, below) | `20261110T215417Z-b5f2a1de` | pass | 174 recorded; 173 equal to the PC, `economy/acceptance.json` differs by line endings only |
| Linux box | not run: the Linux box does not build or test (team rule) | – | – | – |

Comparison verdict: `platform-divergent`, first differing digest `economy/acceptance.json`. See the F9h-mac section below. The semantic class passes on both machines.

## F9h-mac: Mac record and PC-vs-Mac comparison, 10 November 2026

Task `pc-f9h-mac-acceptance-compare`, branch `codex/pc-f9h-mac-acceptance-compare` from `codex/persistent-city` at `18008ef3` (#202).

**Where the Mac run ran.** The committed PC record is at `b3e5a910` and merged in `b5f2a1de` (#201), which changed no simulation input. The branch tip `18008ef3` (#202) changed `Persistence/SaveEpoch.cs`, `Runtime/PersistentCityMod.cs`, `Runtime/SaveEpochRuntime.cs` and `tests/SaveEpochTests.cs`, all simulation inputs, so a Mac `full` at the tip would compare `different-source`. I ran the Mac `full` from a second, detached worktree at exactly `b5f2a1de`, in the foreground, recorded that run's `acceptance.json` into `macos-arm64/` on this branch and ran `compare` here. Both records are then at commits with no simulation-input diff. The second worktree was removed after recording. This is the same-source Mac record the plan asks for, not a substitute for a PC re-record.

| Field | Value |
| --- | --- |
| runId | `20261110T215417Z-b5f2a1de` (21:54:17 to 22:21:06 UTC) |
| Source | `b5f2a1de0d132a476c0e1e5c695c5be9f1b6f386`, clean (`dirty: false`, 0 untracked), detached |
| Host | Darwin arm64 (`macos-arm64`), .NET SDK 10.0.400, Python 3.15.0 |
| Game fingerprint set | `unknown`: the Mac dll-import-shim's assemblies differ from every declared set (all 27 listed in `differing`), so `game-fingerprint` is a required native item `incomplete` |
| `PersistentCity.dll` SHA-256 | `0f34e6e22e5157f7aed25b41fe8cb2af14727275cdea99fbf64a5bd5be476c2d`, the same as the PC build at `b3e5a910` |
| Compiled defaults | 1.2 / 1, event budgets 10000 / 1; no declared override |
| `acceptance.json` SHA-256 | `126056c73bdb9ce55071062bdf412cfcf42b7a0b5c3aa722f16f1060d6c89cd9` |
| Exit | 0 (summary `incomplete`; failed `[]`; incomplete: `f9-2` to `f9-9`, `f6b-2`, `game-fingerprint`, `native-wholesale-costs`, `native-staffed-throughput`) |

| Stage | Verdict | Result | Mac wall (s) | PC wall at `b3e5a910` (s) |
| --- | --- | --- | --- | --- |
| provenance | pass | recorded | 0.0 | 0.0 |
| build | pass | exit 0 | 20.9 | 4.9 |
| unit | pass | 4,128 passed | 584.2 | 391.8 |
| population | pass | 8 passed | 0.1 | 0.1 |
| economy | pass | functional criteria pass; unmeasured criteria routed | 496.5 | 446.2 |
| saves | pass | 84 passed | 0.1 | 0.1 |
| movement | pass | 76 arms equal their reference | 450.2 | 467.8 |
| city-s | pass | 27 passed | 8.3 | 10.0 |
| city-m | pass | 18 passed | 48.2 | 43.8 |

The Mac has one more incomplete native item than the PC (`game-fingerprint`), because the shim is not a declared Steam set. That is a property of the Mac's reference assemblies, not of the simulation.

### Comparison

[`comparison.json`](../../tools/acceptance/evidence/comparison.json): verdict `platform-divergent`, first differing digest `economy/acceptance.json`. The other 173 digests are equal: all 85 F6h run files and all 88 movement save hashes. The semantic class passes on both machines; the divergence does not fail it.

The one difference is line endings. The PC wrote F6h's `acceptance.json` with CRLF. Converting the Mac file's LF to CRLF gives `9397589d…aeec8b8d`, the PC digest exactly. The JSON content is the same on both machines, so the simulation itself agrees. The runner hashes the file bytes, though, and `compare` reports what it was given. Hashing that file in a newline-independent way would need a change to `run.py` or `economy_stage.py`, which are simulation inputs. That change would make this pair of records `different-source`, so it is left for a later slice.

### Runner fix: `configSha256` in the settings check

The first `compare` returned `different-source` ("recorded machines ran different settings"). The only differing setting was `configSha256`: `2c14a2bc…d34691` on the PC, `f0940fcb…1a0d85` on the Mac. The committed `config/persistent-city.json` hashes to `f0940fcb…` and its CRLF checkout to `2c14a2bc…`, so the PC's value is the Windows `core.autocrlf` checkout of the same file. `config/` is a simulation input and already compared by the git diff between the record commits. `evidence.py` now leaves `configSha256` out of the settings comparison, and compiled defaults and overrides still have to match. Two new tests in `test_evidence.py` cover it: a config hash that differs only by checkout compares, and different compiled defaults are `different-source`. `evidence.py` is not a simulation input.

### Household bands

[`macos-arm64/f6h-household-bands.json`](../../tools/acceptance/evidence/macos-arm64/f6h-household-bands.json), derived by `evidence.py record` with `acceptance.derive_household_thresholds`. Every derived value, band and per-seed flag equals the PC file. Only `runId`, `commit` and `machine` differ. Not adopted: `acceptance.py`, `FoodChoicePriceWeight` and `FoodChoiceHouseholdWeight` are unchanged.

The Linux box stays not run: it does not build or test (team rule).

## Reference run under `tools/acceptance/evidence/`

| File | Bytes | Content |
| --- | --- | --- |
| `machines.json` | 489 | declared machines and the reason for each one not recorded |
| `windows-x64/summary.json` | 43,537 (re-record) | provenance, classes, summary, ledger state, every stage's verdict, seconds, metrics and digests, the F6h run digests, `acceptanceSha256` |
| `windows-x64/f6h-household-bands.json` | 1,896 | the 1.2 seed bands above |
| `rig-manifest.json` | 17,064 (re-record) | the batch manifest and ledger state at `b3e5a910` |
| `comparison.json` | 39,938 | the digest comparison |

No TRX, log, binary or harness dump; no absolute path, user name or machine name (checked with grep). Raw evidence stayed under `build/acceptance/20261110T131314Z-d23bb925/` and `build/acceptance/20261110T201123Z-b3e5a910/` and is not committed.

## Tooling added

`tools/acceptance/evidence.py` (`record`, `compare`) and `tools/acceptance/test_evidence.py` (10 tests: per-machine layout, no path, bands equal F6's function on a synthetic run, PC-only is pending with the Mac pending and the Linux box not run by rule, equal digests are cross-machine deterministic, one difference is platform-divergent with its name, commits that git cannot compare are not compared, a later head with only docs changes is compared, a simulation input change between the commits or a dirty record is `different-source`). Nothing in `run.py`, `stages.json` or the economy adapter changed.

## Offline validation

- The `full` run above (it runs the build and the whole dotnet suite).
- Required build, `dotnet test tests/PersistentCity.Tests.csproj` and `python -m unittest discover -s tools/acceptance -p test_*.py`: results in the PR description.
- No game, rig, ModsLocal or bridge was used; the installed game's DLLs were only read and hashed.

## In-game checks

From the plan's [section 7](../plans/f9-city-acceptance.md#7-in-game-checks-for-the-next-batched-rig): the checks this record feeds, for the next combined native/perf batch. All run on the headless agent rig against the development head, in the disposable **FT-F9** city with one player food business, never in Peter's ordinary save. The rig records the autosave setting, turns autosave off in the isolated profile, checks it is off before each measured window (also after reload) and restores it at cleanup; functional steps that save run outside F4's timing windows. None is left to Peter, a playtest or a by-hand check. Setup of the batch: execution order is fixture prerequisites first, then the manifest's priority. Run `f9-1`, then `f9-2`, which creates and verifies the shared checkpoint `f9-city-ready` (with its copy for `f9-4` and `f9-9`); stop the checkpoint-dependent checks if `f9-2` fails. Then run the remaining checks in the manifest's priority from `tools/acceptance/evidence/rig-manifest.json` (table above): `f9-8` (its own older-schema city, no checkpoint), `f9-4`, `f9-7`, `f9-6`, `f6b-2`, `f9-5`, `f9-9`, `f9-3`, and `f9-10` last. Use an offline `full` `acceptance.json` at the same head as the rig build (the re-record's run if the head is still `b3e5a910`, otherwise a new `full` run at the batch head). Each result is a `persistentcity-acceptance-1` stage record of class `native` with the check ID (pending capability **RIG-ACCEPT**). **Locked-PC**: runs on the locked rig PC through FT console and rig commands. **Input-only**: also needs injected UI input.

1. **`f9-1` Build provenance at the rig head.** Locked-PC.
   - Setup: head build installed by RIG-MODSET.
   - Steps: on the rig, `python tools/build.py --game-managed <rig Managed> --provenance <out>/build.json` (read-only hashing of the installed game's assemblies); hash the installed `PersistentCity.dll`.
   - Expected: `set` is a declared set (`steam-1331550-build-25482473` unless the game updated); the DLL hash equals the offline `full` run's `provenance.artifact` at the same commit (`0f34e6…be476c2d` at `b3e5a910`; `f72508…16e85e` at `d23bb925`). An `unknown` set leaves `native` incomplete and marks every ledger row stale.
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
