# Rig results — 11 November 2026 (b), Mac (F8h conflict refusals + autosave re-runs)

Role `rig_test`, where `mac`, isolated instance `agentmac`. Candidate `2f1e8136fb0c4b351eb8a837bcaa1411ee6b5270`; fetch and remote-tip verification matched the requested head before build. Schema held at 1. Testing only; no production change, release or merge.

**Summary: 0 passed / 3 partial / 0 failed / 1 not run.** Counts cover only F8h checks 1–2, fix-reload-accounting-epoch check 1 and X3 check 1. Other rows retain their results in [the preceding Mac report](rig-2026-11-11-mac-results.md) and are not recounted. The priority F8h check 2 result was committed first in `437f7996ab0a16d5edc9990085e857ca5369bd9d`.

`<stage>` is `~/HART-Builds/persistent-city-rig-2026-11-11b-mac`. Raw logs, replies, backups and tooling stay there.

## Build and rig

| Item | Value |
|---|---|
| Candidate | `2f1e8136fb0c4b351eb8a837bcaa1411ee6b5270`, detached build worktree `rig-2026-11-11b-mac`. Main tree unchanged. |
| Host / player | Native Apple Silicon M1 Pro Mac, 16 GB; loopback 28330, no tunnel; same private Mac player as preceding run. Runtime 6 observed in health. |
| Build | `python3 tools/build.py --game-managed <private-app>/Contents/Resources/Data/Managed --provenance <stage>/build-provenance.json`; same private Mac references as the preceding run. Homebrew PATH and DOTNET_ROOT set. Build succeeded. |
| DLL SHA-256 | `4c500997601b098e18665feca8d2b6a3130e7a0180249f515944ce09d6436850`; installed hash matched. |
| Dependencies | Downloaded Harmony archive failed the pinned checksum. Reused the preceding build's DLL and license after verifying DLL hash `7b9e756306fa3d7620e02a857c8927a6ab04973f9bd8a77d3866700a6deac55c`; build verifies that pin again. |
| Provenance | Mac reference fingerprint `unknown`, 27 differing assemblies. `<stage>/build-provenance.json`. No Windows compatibility or same-head offline full-artifact comparison is claimed. |
| Modpack | All 29 DLL paths match [the Mac owner record](f8h-check1-modpack-owners-mac.json); all hashes match except the candidate PC DLL. `<stage>/modpack-comparison.json`. |
| Launcher / lease | Official read-only Mac launcher through the preceding run's stage-only LocalControl-path wrapper. Owner `pc-mac-rig-2026-11-11b`; acquired only after status showed free/stopped. |
| Official rig checkout | `cfcc5fde3f652356a5aa3585250f4a92604557fb`, unchanged and clean. Default LocalControl DLL absent; reused preceding run's stage-built controller. No install/update script or Windows launcher used. |
| Mode / resources | Headless; immediately before start: 93.5 GiB disk, 76% reported free memory. |
| Backup | 260 files copied and SHA-256 recorded before install, including isolated ModsLocal, preferences, PC configuration and installation receipt. |
| PC settings | Enabled and PhysicalEnabled true, 48 pedestrians / 24 cars / radius 120 m; other compiled defaults. Only isolated PC DLL, English locale, PC config and receipt installed. |
| Autosave | Original preference keys absent; native Options reads 5 minutes / 3 saves, ranges 1–60 / 1–10, at creation and after both reloads. No Off value. No documented Mac suppression option. Preferences not edited to attempt suppression; original bytes restored after stop. |
| Offline checks | Candidate build and stage-only auxiliary reader build succeeded. No source changes, full suite or regression suite. Native results and auxiliary reader results are recorded separately. |

## Runs

| Run | Fixture / purpose | Result |
|---|---|---|
| creation | Fresh native `new_game`, candidate enabled | City running; F8h diagnostics captured. |
| rental | Native retail building rental at 36 3rd Street | Refused: `Not enough money`; $551 available, $13,875 initial payment. No money injection. |
| S1 reload | Ordinary save at 08:00; bridge menu/load | City running at 08:00, 49/49 targets, checkout active / 0 refusing. PARTIAL for autosave setup. |
| hour / H1 reload | Normal speed-6 advance 08:00 → 09:00; save, menu/load, save H1-Reload | City running at 09:00, 49/49; 957 residents / 957 visitors and equal resident identity digests. X3 remains PARTIAL. |
| cleanup | Normal stop, restore, hash check and release | 260 files match; zero mismatches; stopped, free, pending 0. |

## F8h

**Check 2 PARTIAL.** `food.checkout.shop optional active`; header ends `0 refusing food.checkout.shop`, 49/49 target methods patched. Shared `RunMainGameTick` lists cityperformance.mod prefix (may-skip) and finalizer (swallows), drivenrides.mod prefix (may-skip), foodtrucks.mod prefix (may-skip), postfix (observer) and finalizer (swallows). None refuses checkout. The activation line has no `food.checkout.shop off` and reports `scan 11.024 ms`. No post-load `Food checkout waits for the city load to finish` refusal observed through this capture.

The same diagnostics pass after ordinary S1 and H1 menu/load, with activation scans 5.125 ms and 5.278 ms. All three captures have 49/49 targets and 0 refusing checkout. The final log contains zero `Food checkout waits for the city load to finish` refusals.

The first-sale assertion is not run: native rental was refused as above and the documented `shop_fixture` addon is Windows-only. No owned food shop was established. Autosave-off setup is also unavailable. `<stage>/f8h-result.json`, `creation-status.json`, `creation-hooks.json`, `player-final.log`, `shop-rent-after.json`, `s1-hooks.json`, `h1-reload-hooks.json` retain the evidence. Street Food's vendor API is absent in this pinned modpack, as in the preceding run; the modpack was not replaced.

**Check 1 NOT RUN (needs RIG-FOREIGNPATCH, Coding Projects).** Read the official rig README, `docs/MAC.md`, `docs/rig-ops.md`, `docs/rig-flows.md` and `docs/rig-modset.md`; searched official Markdown documentation for foreign-patch support. No documented Mac capability installs and removes a named test Harmony prefix. The current Mac launcher exposes neither that operation nor the Windows-only rig-ops addon. No injector was written, no test owner installed, and neither refusal nor remove-and-reload proof is claimed.

## fix-reload-accounting-epoch

**Check 1 PARTIAL.** Fresh enabled native `new_game` completes its creation save, then ordinary S1 is saved at epoch minute `1920` (08:00) and immediately menu/loaded. PC reaches city running at 08:00 with 49/49 targets. The final positive-run log has zero `Accounting input epoch was lost`, `start refused (clock.city)`, `startup is still waiting` or `City clock already active` messages. The expected reload assertions pass; autosave-off setup cannot be established through the documented Mac path. Evidence: `save-s1.json`, `s1-health.json`, `s1-status.json`, `s1-hooks.json`, `s1-reload-native-autosave.json`, `player-s1-reload.log`.

## X3

**Check 1 PARTIAL.** The fresh city advances normally from 08:00 to 09:00, saves schema-1 H1 at epoch minute `1980.0099994596094`, and menu/loads at 09:00 with PC running. H1 and H1-Reload each contain 957 residents / 957 visitors; resident identity SHA-256 is equal: `7e3e975e93a00f8dcc3c469fc6338744391f6a8d82c4f202ed3a3d946c8a18e9`. No `start a new city` message appears.

Autosave remains 5 minutes / 3 saves after H1 reload. The native log does not emit the expected literal `Loaded` / schema-1 population-read assertion. Schema and identity evidence comes from payload extraction and a stage-only reader built against this candidate. That auxiliary .NET reader reports `Invalid visitor next event` for both H1 payloads despite native Mono loading H1 and running; S1 auxiliary reading reports `Invalid commute duration`. This is not a native-corruption verdict or full-payload validity claim. Evidence: `hour-health.json`, `save-h1.json`, `h1-reload-health.json`, `h1-reload-native-autosave.json`, `fixtures.json`, `identity-before.jsonl`, `identity-comparison.jsonl`, `player-final.log`.

## Performance

| F8h activation | Scan |
|---|---:|
| Fresh city | 11.024 ms |
| Ordinary S1 reload | 5.125 ms |
| H1 reload | 5.278 ms |

Provisional: concurrent harness activity was not excluded. These are startup observations, not gameplay windows or a mod-work p95 verdict. Autosave was not suppressed; no measured gameplay window or save profiling was run. No comparison isolates F8h's incremental cost. Whole-frame/RLO timing is separate.

All emitted PC routing component samples ≥30 ms in `<stage>/player-final.log` are flagged below; no additional measurement was run:

| Log line / frame | Component | ms |
|---|---|---:|
| 1385 / 3173 | preparation | 120.938 |
| 2251 / 5616 | preparation | 128.749 |
| 3262 / 8996 | preparation | 114.955 |
| 3264 / 9062 | closure refresh | 99.672 |

Aggregate diagnostic maxima ≥30 ms: scene search 126.254 ms, preparation 128.749 ms, closure refresh 99.672 ms. These summarize component observations and are not extra enumerated samples. Unlogged samples cannot be enumerated. `<stage>/observed-spikes.json` retains the extraction.

## Findings

1. Full-modpack diagnostic assertions pass across creation, S1 reload and H1 reload. First-sale proof remains missing after native rental refusal.
2. Immediate reload again avoids the accounting-epoch refusal and activates at the saved minute. Its autosave setup remains unverified.
3. X3's native load and identity equality pass. Autosave suppression and the literal native Loaded/schema assertion remain missing; auxiliary validation differs from native loading as recorded above.
4. RIG-FOREIGNPATCH has no documented Mac route in this checkout. No rig upgrade or custom patch injector was attempted.

Existing headless shader and mod asset/API limitations remain in raw logs. They are not counted as new failures of these four checks.

## NOT RUN grouped

**RIG-FOREIGNPATCH, Coding Projects — 1 row:** `f8h-1`, including its remove-and-reload half.

**RIG-FOODSHOP / native rental — unrun assertion within `f8h-2`, not an extra row:** first PC sale after load. Native rental refused; Mac shop-fixture addon unavailable.

**Autosave-off — missing setup within the three PARTIAL rows:** native sliders have no Off value; installed runtime 6 / documented Mac launcher has no autosave guard option. Windows `-rig-test` flow/rig-ops suppression is not a Mac capability and was not invoked. No performance window was taken with autosave on.

Other NOT RUN/PARTIAL catalogue rows retain the [preceding report](rig-2026-11-11-mac-results.md), grouped there by capability. The current Mac documentation provides no new route for those prerequisites. They are not recounted here. Pointer/hover/focus/click checks remain Peter's playtest; bridge UI operations here are setup/readbacks, not pointer-input proof.

## Fixtures

One fresh disposable FT character created by native `new_game` at the candidate in the isolated profile. No preexisting fixture or real save loaded. No shop, campaign, rival-finance mutation or money injection. The four disposable checkpoints remain in the isolated profile; raw saves are not committed. Hashes were re-read after stop.

| Checkpoint | SHA-256 | Bytes |
|---|---|---:|
| `FT-PC-Mac-20261111b-2f1e8136` | `bb5e00e5835f200e9a1c7d2c3e0f331189867c60c3f33d60971d8477919c0d07` | 682809 |
| `FT-PC-Mac-20261111b-2f1e8136-S1` | `0d4efc6a1c41735ead51823477a905096bc58f682f1a1638b98100e9af636aa9` | 1714601 |
| `FT-PC-Mac-20261111b-2f1e8136-H1` | `60704959c071ded75e933d366fd86e65f5654d23e0fe21e242fe5190ecb7d1a3` | 2775412 |
| `FT-PC-Mac-20261111b-2f1e8136-H1-Reload` | `4dd0fb70f764b4f918a36ed6ceb72987c8eecdb6644f678e44c4bd31b1edd2c6` | 2849670 |

All payloads declare schema 1; save epochs use `v1` and schema 1, sequences 1–4. `<stage>/fixtures.json` retains lengths, payload hashes and epochs. No recovery save was found for this character at the final extraction.

## Cleanup

Normal `mac-agent stop` succeeded; no force stop. Restored and SHA-256 verified all **260** backed-up files, with zero mismatches, including isolated ModsLocal, persistent-city.json, installation receipt, profile preferences and native preference plist. New mod files were removed. Original autosave preference-key absence and plist bytes restored. `<stage>/restore.json` records `autosave_restored=true`.

Released the lease after stop/restore. `<stage>/status-final.json`: `running=false`, lease `held=false`, pending 0. Owned game PID 41734 and its caffeinate assertion are absent (`process-check.json`); foreground tooling commands completed. Main mod tree and official rig checkout remain clean. No Windows/agent1, Steam/normal profile, OS settings or unrelated process was controlled. No WRAP_UP_NOW marker appeared at the checked boundaries. Evidence worktree is detached after PR creation so the branch is available for review.

## Per-check results

| Check | Result | Evidence / reason |
|---|---|---|
| `f8h-2` | PARTIAL | Status/hooks/activation assertions pass; first sale missing after native rental refusal; autosave-off unavailable. |
| `f8h-1` | NOT RUN | Needs RIG-FOREIGNPATCH, Coding Projects; no documented Mac patch-install/remove capability. |
| `fix-reload-accounting-epoch-1` | PARTIAL | Saved-minute 08:00 reload, city running, 49/49 targets, zero epoch/clock/startup refusal messages; autosave-off unavailable. |
| `x3-1` | PARTIAL | One-hour run and schema-1 H1 native reload, resident identity equality; autosave-off and literal native Loaded/schema assertion missing. Auxiliary validation differs from native loading. |
