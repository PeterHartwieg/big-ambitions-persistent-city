# PC rig proof (agent1) — PR #1, 28 October 2026

**Verdict: BLOCKED.** This is evidence for PR #1's gates, not approval to merge, release, deploy to ordinary saves or publish to Workshop. Offline equivalence, activation and schema-13 save compatibility passed. Six save-free native timing windows were collected. The original strict financial verifier was unavailable, and broad native-log/noise acceptance remains incomplete. No incomplete check is counted as a pass.

PC / `rig_test` role. Compared frozen source `76e903aa38c258c6b78c574c310ce07000434fc2` (before) and `3df8232384d293f6c7fb20522577a9ef3dc5901c` (after). No production source or tests were edited. Both packages were compiled on Windows against installed Steam Managed assemblies referenced in place.

| Check / expected | Before 🔴 observed | After 🟢 observed | Gate / evidence |
| --- | --- | --- | --- |
| Build; unit tests green | Build passes; 1,260 passed / 0 failed / 0 skipped | Build passes; 1,267 passed / 0 failed / 0 skipped | PASS; [offline output](offline-output.txt), [command exits](offline-results.json) |
| All README harnesses green | All seven commands pass | All seven commands pass | PASS; five road/gate/traffic harnesses, native-command regression and 28-day simulator, separate from unit tests |
| Differential traffic/save/city fixtures identical | Same harnesses built against before sources | 32,414 traffic observations identical; 38,924 activity events and seven saves identical; save bytes and refusal outcomes identical | PASS; [offline output](offline-output.txt) |
| Same disposable fixture activates | 957 residents, 1,059 places, schema 13, 1,914 consumers | Same counts and schema | PASS; [before image](before-loaded-city.png), [after image](after-loaded-city.png), [startup log excerpts](native-log-excerpts.txt) |
| Native QA: no new errors/inactive patches | 132 active patch lines; four inactive City Performance patches; 0 mod-error lines / 0 PersistentCity exception stacks in main before log | 132 active; same four inactive patches; 1 Food Trucks mod-error line / 0 PersistentCity exception stacks | **INCOMPLETE broad error gate**; native/helper failures retained below; [checker](native-log-checks.json), [exceptions](exception-summary.json) |
| Manual after-save reloads with both builds; state valid; schema stays 13 | Before loads after-save; stored payload hash identical; IDs/assignments retained | Manual save succeeds, after reload succeeds; same stored payload bytes | PASS for Persistent City payload; [round-trip checks](roundtrip-summary.json). Whole-modpack error-free saving is not established |
| Three equal nominal 60-second normal-speed windows, no save | 3 valid windows | 3 valid windows | PASS collection; [timings](performance-summary.json), [autosave boundaries](autosave-evidence.json) |
| No performance regression beyond noise | p95 2.7416 / 2.9104 / 3.4181 ms | p95 1.7516 / 1.7743 / 1.2024 ms; first-window max worse | **INCOMPLETE stronger noise/causality gate**. No observed p95 regression in this block; maxima mixed, blocked run order and native/system-load variation remain |
| Existing strict native accounting check rerun on both | Original 27 October verifier script unavailable | Same blocker; retained capture/operations helper refuses new PR1 fixture | **BLOCKED**; supplemental reconciliation below is not a substitute or a 1,936/2 rerun |
| Restore originals; stop; release lease | Original backup verified before install | 4,828 original profile files, exact inventory and all SHA-256; 107 receipt references; preferences and root files restored | PASS; [cleanup](cleanup.json), [lease release](lock-released.json) |

## Package and fixture identities

- Before DLL SHA-256: `bcca8f86c094d997a96506d0f7d1113cc40c1a0de786315ad23fe700803efec5`.
- After DLL SHA-256: `222008b91283e2765747e1dbdf393efa6754b62a93fa1a50b613b87ceb2191ff`.
- Identical timing helper in both arms, retained v3: `dc939f272b1b451a7eb15dd7c6665e3a74d4a389e3ebfe6bf9d35d15d18696aa`.
- Autosave/capture guard DLL: `bd191ead8c7e231bfae77c5b2d1928dd9d24cfc724401e3d88115cf5d7082cbf`; it records the native autosave flag and validates/writes diagnostic snapshots outside timing windows. It does not change resident decisions, prices, native sales or accounting.
- Copied FT morning fixture SHA-256: `bb8ac25721dcea825cdf40e462a606e6244fe93668c8940b9688f4ca482d9273`. New disposable name: `FT Food Routines PR1 20261028`; the original fixture was not overwritten. Manual-save name: `FT Food Routines PR1 After Roundtrip 20261028`.
- Stored schema-13 payload after manual save and both reloads: `6214ad1a3c37c1dbf86454352ff511d990d1614f010a604cfac714074cd75cfb`. All 957 citizen identities, homes, day places, leisure/shop assignments and preferences match the payload; all 1,914 consumer IDs remain. Live snapshots pass `SaveCodec.Write` validation. Live geometry/timing after rebind is not asserted byte-identical.

## Native timing and autosave

Both runs use the same copied checkpoint, unchanged player position (no movement commands), fixed camera approximately `(215,20.17771,28.67032)` / yaw 180°, normal native speed (`timeScale=1`) and one-third calendar factor. Tiny camera float differences remain. RTX 4070, i5-13600KF, Direct3D11, Low quality, 1920×1080, vSync off and target 90. Same helper instrumentation; optional new PR event tracing was not enabled. These are measured Update/authority union scopes, not every possible mod hook or an interactive FPS benchmark. Instrumentation overhead remains; native allocation recording was unavailable.

| Arm/window | Seconds | Native minutes | Mod p50 ms | Mod p95 ms | Mod max ms | Whole-frame p95 ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| before 1 | 60.336 | 20.130 | 1.5013 | 2.7416 | 13.4226 | 34.6289 |
| before 2 | 60.345 | 20.143 | 1.5762 | 2.9104 | 10.6838 | 38.0277 |
| before 3 | 60.465 | 20.250 | 1.7921 | 3.4181 | 24.5119 | 55.9626 |
| after 1 | 60.288 | 20.124 | 1.0579 | 1.7516 | 19.8351 | 23.6534 |
| after 2 | 60.285 | 20.108 | 1.0323 | 1.7743 | 8.8696 | 23.2619 |
| after 3 | 60.413 | 20.151 | 0.7718 | 1.2024 | 20.2877 | 18.8388 |

All six accepted windows have zero timed/component exceptions and zero cross-frame timing calls; final population lag is zero. This does not mean the complete native logs contain no exceptions. Native bodies, rendering work and other-program CPU load vary; all before windows precede all after windows. Lower p95 is observed, but a causal speedup and the stronger noise acceptance are not established. Window 1 max rises 13.4226→19.8351 ms; other paired maxima fall. No FPS or native allocation improvement is claimed.

The original autosave state was `preventAutoSave=false`, 5 minutes between saves, 3 retained autosaves. The isolated guard calls native `GameManager.SetPreventAutoSave(true)` and verifies it before/after every accepted window, including after reload. `SavingGameInProgress=false`, save-file sizes/mtimes unchanged and no actual save-start/completion marker appears in any accepted window. The guard's broad `saving` mention counter also counts RLO's **“not saving”** reports; those counts are explicitly named broad mentions, not saves. Manual saves and native skips occur outside the windows. A native pending-midnight-autosave exception occurred outside the windows during baseline teardown; the flag is not claimed to suppress every unload/skip path. Original preferences were restored byte-for-byte.

## Financial observations and incomplete strict gate

The exact verifier producing the earlier 1,936 pass / 2 fail accounting reports was not present in the worktrees or retained Windows campaign files. Its location was requested during the run and remained unavailable. The retained helper's `capture-state` and `operations-status` operations reject the newly named PR1 copy with their original exact-fixture guards. Those refusals were retained; guards, prices, tolerances and RLO were not changed to force a pass.

Separate paused population/status captures and native account rows support the following **supplemental** two-day replay, using a native 16-hour skip after the timed morning and then a 24-hour skip. Temporary test-player needs suppression is restored after each skip. Both builds produce identical aggregates:

| Day | Purchases before/after | Resident payments before/after | Native revenue before/after | Residual before/after |
| --- | ---: | ---: | ---: | ---: |
| 5 | 57 / 57 | $584.25 / $584.25 | $584.4096 / $584.4096 | 15.96 / 15.96 cents |
| 6 | 53 / 53 | $543.25 / $543.25 | $543.3984 / $543.3984 | 14.84 / 14.84 cents |
| Total | 110 / 110 | $1,127.50 / $1,127.50 | $1,127.808 / $1,127.808 | 30.80 / 30.80 cents |

Both consume exactly 110 croissants and bags, preserve the 1,914 consumer IDs and have zero wallet-ledger invariant failures or held purchase cents at the final capture. Native revenue conservation at the original 0.1-cent boundary **fails in both**. The unit discrepancy is unchanged: native $10.2528 versus 1,025 resident cents. The old campaign's 109 sales / 30.52-cent gap is not this replay: this run has one more Day-5 sale. It must not be presented as an exact replay of that old total. [Captured-state reconciliation](financial-supplement.json). Exact-shop bank cash and every assertion of the missing strict verifier were not independently rerun; no full financial-gate pass follows.

## Native QA and retained failures

Log excerpts sanitize paths and normalize line endings/trailing whitespace; raw-log SHA-256 values identify the retained originals. Native ModAPI logs are `Player-agent.log`; no BepInEx/MelonLoader run applies. Both main runs show the same four inactive City Performance capabilities: pedestrian refill, midnight cleanup deferral, tree detail and cinematic coverage. No additional inactive capability or PersistentCity exception stack was found by the retained checker. This is not a blanket clean-log result.

- Before main log: three old-helper `File.Move` IOExceptions and one native `PlayerHelper.SaveCurrentPosition` / pending midnight-autosave NullReferenceException.
- After main log: two old-helper `File.Move` IOExceptions and nine native `StreetLamp.ChangeLightSetting` NullReferenceExceptions during city unload/reload. One Food Trucks ERROR says a save is being made while its city mod scope is detached; it appears after the successful manual-save completion, while leaving the city.
- Additional before save/reload control: two helper IOExceptions, no Food Trucks ERROR or native exception header. It does **not** establish that the after-only errors are harmless or preexisting. No PersistentCity stack appears, but PR causality is unresolved.
- NavMesh warnings: 35 before / 22 after / 20 in the additional before control. [Raw-log hashes, counts and excerpts](native-log-excerpts.txt).

The no-new-error gate is incomplete pending attribution or a clean controlled rerun. No production repair was attempted. The successful Persistent City payload round-trip does not certify the unrelated Food Trucks save block.

Earlier attempts are excluded from the six-window comparison: initial too-early status read before RLO/PC readiness; hidden launch with invalid/null game-view texture; a 35-second sample contaminated by UI enumeration; a 60-second sample rejected by the overbroad “saving” counter. Original failures remain in private staging. Windowed native game-view screenshots succeeded. Computer-use window activation failed (`failed to activate captured window`); no physical-input or unlocked-desktop presentation claim follows.

## Restoration, lease and boundaries

Lease acquired `2026-10-28T13:57:58.2702606Z`. Only agent1 at `C:\BigAmbitions-AgentTests` was launched/controlled, through identity-checked port 18330. Installation changes stayed in its private profile. Unrelated receipt references were checked at each package switch; RLO implementation/settings were not deliberately changed. Steam and Workshop writes are disabled by the rig runtime. No ordinary profile/save/ModsLocal, Steam setting, WSL distro, merge, force-push or Workshop upload was used.

Restored at `2026-10-28T14:34:59.8716786Z`: exact inventory and bytes for all 4,828 original profile files, all 107 receipt references, original registry preferences, install receipt, run record and original Player-agent.log. Test-created files were moved out with the tested profile; they do not remain in the restored profile. Original install-receipt SHA-256 `9785d1de5c7360875b2a2a47f511815660dd98a38d6f64ab4ecff8c20627b949`; preference export `66fccbcc5f63bc492e36ba7f342d9d2480adb2b87404a650a8607094d207381d`. Helpers removed by full profile restoration. Game stopped. Lease released `2026-10-28T14:34:59.9531025Z`, unheld, zero pending operations.

Open gates: missing exact strict accounting verifier; native money-conservation defect retained for PR #2; after unload/save-scope error attribution; stronger performance/noise acceptance. This evidence does not clear PR #1 for all requested gates. The local full logs, original failed attempts, raw snapshots and restoration inventories are retained privately; no game DLL, decompiled source, raw save or credential is included here. [Evidence hashes](file-manifest.json).
