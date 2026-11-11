# Rig results — 11 November 2026 (b), Mac (F8h conflict refusals + autosave re-runs)

Role `rig_test`, where `mac`, isolated instance `agentmac`. Candidate `2f1e8136fb0c4b351eb8a837bcaa1411ee6b5270`; fetch and remote-tip verification matched the requested head before build. Schema held at 1. Testing only; no production change, release or merge.

**Priority result committed first: 0 passed / 1 partial / 0 failed / 0 not run.** This initial commit covers F8h check 2; subsequent checks and cleanup will be recorded in this report. Other rows retain their results in [the preceding Mac report](rig-2026-11-11-mac-results.md) and are not recounted.

`<stage>` is `~/HART-Builds/persistent-city-rig-2026-11-11b-mac`. Raw logs, replies, backups and tooling stay there.

## Build and rig

| Item | Value |
|---|---|
| Candidate | `2f1e8136fb0c4b351eb8a837bcaa1411ee6b5270`, detached build worktree `rig-2026-11-11b-mac`. Main tree unchanged. |
| Build | `python3 tools/build.py --game-managed <private-app>/Contents/Resources/Data/Managed --provenance <stage>/build-provenance.json`; same private Mac references as the preceding run. Homebrew PATH and DOTNET_ROOT set. Build succeeded. |
| DLL SHA-256 | `4c500997601b098e18665feca8d2b6a3130e7a0180249f515944ce09d6436850`; installed hash matched. |
| Dependencies | Downloaded Harmony archive failed the pinned checksum. Reused the preceding build's DLL and license after verifying DLL hash `7b9e756306fa3d7620e02a857c8927a6ab04973f9bd8a77d3866700a6deac55c`; build verifies that pin again. |
| Modpack | All 29 DLL paths match [the Mac owner record](f8h-check1-modpack-owners-mac.json); all hashes match except the candidate PC DLL. `<stage>/modpack-comparison.json`. |
| Launcher / lease | Official read-only Mac launcher through the preceding run's stage-only LocalControl-path wrapper. Owner `pc-mac-rig-2026-11-11b`; acquired only after status showed free/stopped. |
| Mode / resources | Headless; immediately before start: 93.5 GiB disk, 76% reported free memory. |
| Backup | 260 files copied and SHA-256 recorded before install, including isolated ModsLocal, preferences, PC configuration and installation receipt. |
| Autosave | Original preference keys absent; native Options reads 5 minutes / 3 saves, ranges 1–60 / 1–10. No Off value. No documented Mac suppression option. Preferences not edited to attempt suppression. |

## Runs

| Run | Fixture / purpose | Result |
|---|---|---|
| creation | Fresh native `new_game`, candidate enabled | City running; F8h diagnostics captured. |
| rental | Native retail building rental at 36 3rd Street | Refused: `Not enough money`; $551 available, $13,875 initial payment. No money injection. |

## F8h

**Check 2 PARTIAL.** `food.checkout.shop optional active`; header ends `0 refusing food.checkout.shop`, 49/49 target methods patched. Shared `RunMainGameTick` lists cityperformance.mod prefix (may-skip) and finalizer (swallows), drivenrides.mod prefix (may-skip), foodtrucks.mod prefix (may-skip), postfix (observer) and finalizer (swallows). None refuses checkout. The activation line has no `food.checkout.shop off` and reports `scan 11.024 ms`. No post-load `Food checkout waits for the city load to finish` refusal observed through this capture.

The first-sale assertion is not run: native rental was refused as above and the documented `shop_fixture` addon is Windows-only. No owned food shop was established. Autosave-off setup is also unavailable. `<stage>/f8h-result.json`, `creation-status.json`, `creation-hooks.json`, `player-creation.log`, `shop-rent-after.json` retain the evidence.

## Performance

Activation scan: **11.024 ms**, once during fresh-city activation. Provisional: concurrent harness activity was not excluded. This is a startup observation, not a gameplay window or a mod-work p95 verdict. Autosave was not suppressed; no measured gameplay window or save profiling was run.

## Findings

The full modpack keeps checkout active at this head. First-sale proof remains missing because the fresh fixture cannot afford native rental and Mac shop-fixture support is unavailable.

## NOT RUN grouped

Other catalogue rows retain the [preceding report](rig-2026-11-11-mac-results.md), including Peter's pointer/hover/focus/click playtest rows. They are not recounted here.

## Fixtures

Fresh disposable native `new_game`: `FT-PC-Mac-20261111b-2f1e8136`, isolated profile only. Checkpoint hashes and final cleanup follow in the completion commit; fixture saves remain in place.

## Per-check results

| Check | Result | Evidence / reason |
|---|---|---|
| `f8h-2` | PARTIAL | Status/hooks/activation assertions pass; first sale missing after native rental refusal; autosave-off unavailable. |
