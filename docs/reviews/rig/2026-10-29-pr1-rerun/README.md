# PC rig proof rerun (agent1) - PR #1

**Verdict: PASS for the specified PR #1 gates.** PC / `rig_test`. Financial before/after equality, native-error attribution and the interleaved descriptive noise check pass. No production source/tests were edited. No merge, release, ordinary-save deployment or Workshop publish was performed.

The requested evidence directory uses the **29 October campaign label**. Actual execution timestamps are **28 October 2026 UTC**. Lease: `2026-10-28T15:53:47.4435235Z` to `2026-10-28T16:30:44.7241792Z`. The earlier blocked report remains historical; this rerun resolves its PR #1-specific equality, target-error attribution and interleaving questions under the supplied gate decisions.

| Check | Before | After | Gate |
| --- | --- | --- | --- |
| Frozen source | `76e903aa38c258c6b78c574c310ce07000434fc2` | `3df8232384d293f6c7fb20522577a9ef3dc5901c` | Origin checked; starting evidence-only head `50d5fb9f19e5c06d09c03ba063d73ac000d8e724` |
| Build/unit suite | Pass; 1,260 passed, 0 failed/skipped | Pass; 1,267 passed, 0 failed/skipped | PASS offline |
| Seven README commands/differentials | All pass | All pass; 32,414 traffic observations, 38,924 activity events and seven save outputs identical | PASS offline; [outputs](offline-output.txt), [exits](offline-results.json) |
| Same FT fixture activation | 957 citizens, 1,914 consumers | Same | PASS native |
| Financial equality | 110 purchases; exact values below | Identical measured money/quantities/receipt identities | PASS |
| Controlled native errors | Three matched sequences | Three matched sequences | PASS; neither target reproduced |
| Interleaved timing | Three clean nominal 60-second windows | Three clean nominal 60-second windows | PASS descriptive spread comparison; noisy tails retained |
| Schema-13 save/reload | Three round trips; identities/assignments and stored payload retained | Same | PASS; [round trips](roundtrip-summary.json) |
| Restore/stop/lease | Original backup verified before installation | 4,828 files, 107 references, preferences and root files restored/hash-verified; stopped; lease released/pending 0 | PASS |

## Financial equality

**Supplied gate: PR #1 must preserve before/after money behavior.** The shared native-versus-resident conservation defect is a separate issue. Both arms load the same disposable FT morning checkpoint and use native skips of 17 and 24 hours, Day 5 07:45 to Day 7 00:45. Test-player needs suppression is restored after each replay.

The observer captures native IEEE bits and resident decimal values. Comparison uses exact integer rationals, no tolerances or comparison rounding. All compared amounts, quantities, purchase/receipt identities, native prices/history/accounts/cash and measured wallet totals match. Native transaction sub-minute timestamps are excluded from the money comparison.

| Observation (dollars unless count) | Before | After | Equality |
| --- | ---: | ---: | --- |
| Confirmed purchases | 110 | 110 | Exact |
| Resident payments | 1127.5 | 1127.5 | Exact |
| Native order total | 1127.8079986572265625 | 1127.8079986572265625 | Exact |
| Native revenue transactions | 1127.8079833984375 | 1127.8079833984375 | Exact |
| Native cash change, including costs | 695.3291015625 | 695.3291015625 | Exact |
| Croissants / bags sold | 110 / 110 | 110 / 110 | Exact |
| Ledger failures / final held cents | 0 / 0 | 0 / 0 | Exact |

| Day | Purchases before / after | Payments before / after | Native history/account/cash revenue before / after |
| --- | ---: | ---: | ---: |
| 5 | 57 / 57 | 584.25 / 584.25 | 584.40960693359375 / 584.40960693359375 |
| 6 | 53 / 53 | 543.25 / 543.25 | 543.39837646484375 / 543.39837646484375 |


There are **zero differences and zero observer faults**. All 1,914 consumer IDs remain. Native orders exceed resident payments by **$0.3079986572265625 on both builds**. Native accumulated history/account/cash totals also match across builds. Consequently the retained [before](before-financial-diagnostic.json) and [after](after-financial-diagnostic.json) conservation diagnostics say FAIL, while the PR #1 [before/after equality gate](financial-equality.json) says PASS. [Compared values](financial-compared-values.json), [comparison](compare_finances.py), [native-value verifier](verify_financial.py).

## Error attribution

**Supplied rule:** if either StreetLamp.ChangeLightSetting NullReferenceExceptions or Food Trucks "being saved while the mod isn't attached" ERROR reproduces only on after and never before, verdict FAIL and stop further testing. Reproduction on both, or neither, passes this attribution gate.

Order: **A1 B1 A2 B2 A3 B3**, A=before, B=after. Each fresh process loads the same copied fixture, plays a ten-second warmup and 60-second normal-speed window, pauses, manually saves to a unique disposable FT name, invokes native load (leaves/unloads the city and reloads that save), verifies autosave off, plays ten seconds, pauses and quits. A3/B3 additionally run the same separate financial replay before quitting. Two runs per build completed before continuation to the third pair. Complete logs, including shutdown, were also checked.

| Run | Build | StreetLamp NRE sequence / full | Food Trucks ERROR sequence / full | Other full-log exceptions |
| --- | --- | ---: | ---: | --- |
| A1 | before | 0 / 0 | 0 / 0 | 1 helper IO; 0 native SaveCurrentPosition NRE |
| B1 | after | 0 / 0 | 0 / 0 | 2 helper IO; 0 native SaveCurrentPosition NRE |
| A2 | before | 0 / 0 | 0 / 0 | 2 helper IO; 0 native SaveCurrentPosition NRE |
| B2 | after | 0 / 0 | 0 / 0 | 2 helper IO; 0 native SaveCurrentPosition NRE |
| A3 | before | 0 / 0 | 0 / 0 | 3 helper IO; 1 native SaveCurrentPosition NRE |
| B3 | after | 0 / 0 | 0 / 0 | 3 helper IO; 1 native SaveCurrentPosition NRE |


Neither target signature appears anywhere in the six complete logs. This passes the rule's non-reproduction branch; the earlier occurrences remain recorded. No PersistentCity exception stack or mod ERROR line was found. Every run has the same **132 active patch lines and four inactive City Performance capabilities**: pedestrian refill, midnight cleanup deferral, tree detail and cinematic coverage.

Other failures remain visible: reused-helper `File.Move`/existing-file IOExceptions on reload, and one native `PlayerHelper.SaveCurrentPosition`/pending-midnight-autosave NRE after **each** financial arm, when guard teardown restores the original autosave flag. These occur on both builds and outside measured windows. This is not an error-free modpack claim. [Attribution/raw-log hashes](error-attribution.json), [native checks](native-log-checks.json), [full exceptions](native-full-exception-excerpts.txt), [controlled exceptions](native-error-excerpts.txt).

## Interleaved timing

Milliseconds below, except seconds/native minutes. Same player position, camera approximately `(215,20.17771,28.67032)`/yaw 180 degrees; only tiny camera float differences. Same RTX 4070/i5-13600KF, Direct3D11, Low, 1920x1080, vSync off, target 90, `timeScale=1`, one-third calendar factor. No movement commands. Every window ends at zero population lag and has zero timed/component exceptions and cross-frame timing calls.

| Run | Build | Seconds | Native min | Mod p50 | Mod p95 | Mod max | Frame p50 | Frame p95 | Frame max |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| A1 | before | 60.201 | 20.090 | 0.1695 | 0.2618 | 9.7224 | 11.1473 | 14.6339 | 115.8827 |
| B1 | after | 60.301 | 20.123 | 0.1665 | 0.2787 | 12.3257 | 11.1160 | 14.3918 | 113.0126 |
| A2 | before | 60.140 | 20.072 | 0.1675 | 0.2561 | 9.3489 | 11.1523 | 14.7379 | 142.8313 |
| B2 | after | 60.320 | 20.111 | 0.1674 | 0.2642 | 12.4337 | 11.1969 | 15.2984 | 413.3835 |
| A3 | before | 60.271 | 20.182 | 0.1754 | 0.2804 | 26.8698 | 12.2809 | 29.7023 | 420.5261 |
| B3 | after | 60.409 | 19.953 | 0.1778 | 0.3181 | 37.2948 | 14.4767 | 49.4436 | 491.7174 |


The descriptive rule was recorded before testing: compare **after median minus before median** against the **larger full within-build range** for each metric. No metric exceeds that spread, and all ranges overlap. Thus **no worsening beyond observed within-build spread is established**.

| Metric (ms) | Before median | After median | Change | Before range width | After range width | Beyond larger spread? |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| mod p50 | 0.1695 | 0.1674 | -0.0021 | 0.0079 | 0.0113 | No |
| mod p95 | 0.2618 | 0.2787 | +0.0169 | 0.0243 | 0.0539 | No |
| mod maximum | 9.7224 | 12.4337 | +2.7113 | 17.5209 | 24.9691 | No |
| wholeFrame p50 | 11.1523 | 11.1969 | +0.0446 | 1.1336 | 3.3607 | No |
| wholeFrame p95 | 14.7379 | 15.2984 | +0.5605 | 15.0684 | 35.0518 | No |
| wholeFrame maximum | 142.8313 | 413.3835 | +270.5522 | 304.6434 | 378.7048 | No |


After tails are higher in this sample: 37.29-ms mod maximum, 491.72-ms whole-frame maximum. A3/B3 are substantially slower than earlier windows. Three windows per build and this drift cannot establish causal speedup or statistical equivalence. This is a bounded fixture comparison, not interactive FPS, large-city, allocation or every-hook proof. Mod time is the reused helper's Update/authority union; instrumentation overhead and unrelated whole-frame work remain. Optional new PR event tracing was off. [Window summaries](performance-summary.json), [noise calculation](performance-noise.json), [pre-run method](methodology.json), [private CSV/log hashes](private-evidence-hashes.json).

## Autosave and restoration

Original setting: `preventAutoSave=false`, five minutes between saves, three retained. Autosave was disabled and verified before/after every window and after reload. All accepted windows have unchanged save-file sizes/mtimes and zero actual save-start/completion markers. Broad "saving" mentions include RLO's "not saving" messages. Manual saves, skips, teardown and the paired pending-autosave exceptions are outside measured windows.

| Run | Autosave off before / after timing | Off after reload / resumed play | Save files unchanged; actual save markers |
| --- | --- | --- | --- |
| A1 | Yes / Yes | Yes / Yes | Yes; 0 |
| B1 | Yes / Yes | Yes / Yes | Yes; 0 |
| A2 | Yes / Yes | Yes / Yes | Yes; 0 |
| B2 | Yes / Yes | Yes / Yes | Yes; 0 |
| A3 | Yes / Yes | Yes / Yes | Yes; 0 |
| B3 | Yes / Yes | Yes / Yes | Yes; 0 |


Original preferences restored byte-for-byte: `66fccbcc5f63bc492e36ba7f342d9d2480adb2b87404a650a8607094d207381d`. Exact inventory/SHA-256 match all **4828** original profile files (including ModsLocal), **107** receipt references, and original receipt/run/log root files. The tested profile is retained privately; test-created helpers/files are absent from the restored profile. Rig stopped; lease unheld, pending 0. [Cleanup](cleanup.json), [lease](lease-summary.json), [release](lock-released.json).

| Artifact | SHA-256 |
| --- | --- |
| Before DLL | `bcca8f86c094d997a96506d0f7d1113cc40c1a0de786315ad23fe700803efec5` |
| After DLL | `222008b91283e2765747e1dbdf393efa6754b62a93fa1a50b613b87ceb2191ff` |
| Identical timing helper v3 | `dc939f272b1b451a7eb15dd7c6665e3a74d4a389e3ebfe6bf9d35d15d18696aa` |
| Identical autosave/capture/financial guard | `17336e1e36c39efaa1c98479d4b11a8d61a7bc88b1df740c29f7daa31efd4401` |
| Copied FT morning checkpoint | `bb8ac25721dcea825cdf40e462a606e6244fe93668c8940b9688f4ca482d9273` |

The guard adapts the retained PR #2 observer solely to this named disposable agent1 FT fixture family. It does not change prices, purchasing policy or native accounting. Game assemblies were referenced in place. Full logs/CSVs, raw snapshots, fixture copies and restoration inventories remain private. No game DLL, decompiled source, raw save, preference export or credential is committed. Only agent1 was installed/launched. No normal profile/save/ModsLocal, Steam/Workshop setting, RLO implementation, WSL distro, merge or force push was used. [Public manifest](file-manifest.json).
