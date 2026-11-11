# Rig results ? 11 November 2026 PC (Windows-only rows, autosave-off re-runs, F8h first sale)

Role `rig_test`, HomePCHartwieg, background agent1. Candidate `11217a8f68c8dc3cbf7b2dd30041a860d2540e6c`. Testing only; no release or merge.

[Combined checklist](rig-2026-11-06-checklist.md).

Partial evidence, group 1: **1 passed / 0 partial / 0 failed**; remaining rows pending execution and capability classification.

## Build and rig

Candidate verified by `git fetch origin`, exact head `11217a8f68c8dc3cbf7b2dd30041a860d2540e6c`. Build succeeded after requested wait script, PATH prefixed with local dotnet. DLL SHA-256 `4c500997601b098e18665feca8d2b6a3130e7a0180249f515944ce09d6436850`. No offline tests. Read-only rig `cfcc5fde3f652356a5aa3585250f4a92604557fb`, installed runtime 7, unchanged overlay. Stage: `C:/Users/Peter/HART-Builds/persistent-city-rig-2026-11-11-pc`.

## Runs

| Run | Result |
|---|---|
| creation | Native new_game completed; PC city running; Ready checkpoint retained. All cleanup actions passed, lifecycle hash restore passed, modset restored, lease free/pending 0. |

## Per-check results grouped by slice

| Check | Result | Evidence |
|---|---|---|
| fix-creation-save-init-1 | PASS | Creation save completed, expected skipped-initialization line, no save-epoch refusal or clock.city refusal, city running with 49/49 targets. Native autosave guard enabled/off verified before new_game and after city load. |

## Fixtures

- `FT E5b PC 11 Nov 11217a8f`: `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\SaveGames\Big Ambitions\+yVpXTtLw0a80hg25pNjnw==\FT E5b PC 11 Nov 11217a8f.hsg`, SHA-256 `e73caffd08acef116ee407355898aaa36852985043793eb9b5d60fd38abd07c8`.
- `FT E5b PC 11 Nov 11217a8f Ready`: `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\SaveGames\Big Ambitions\+yVpXTtLw0a80hg25pNjnw==\FT E5b PC 11 Nov 11217a8f Ready.hsg`, SHA-256 `f1c5c41923e9057bb206fa917dd6b6081b37c82ade4f409af04b6108dbe683a3`.

## Group 2 ? guarded reload and shop attempt

Native S1 reloaded at 08:00; verified one-hour checkpoint `FT PC 11 Nov 0900 Verified` saved/reloaded at 09:00 with PC city running, 49/49 targets and autosave guard enabled/off. No lost accounting epoch, clock.city refusal or startup-wait warning observed. The separate main-menu confirmation was refused by the bridge guard, so the documented native `load` command was used; the menu step is qualified as PARTIAL. Full payload/epoch and identity readbacks need the unavailable installed RIG-PCSAVE op.

F8h-2 PARTIAL: checkout active, 0 refusing, shared-tick owners report-only, activation scan retained; shop_fixture refused `ops_shop_native_rental_refused` for `ba:street_fourthavenue#18`, no rented/owned shop and no sale. No money injection or retry of the refused rental. Installed ops lists no `save`, `pcsave`, `pcstate` or `modwork`; direct capability reads return `ops_unknown_op`.

E5b5-1 PASS: dynamicAcquisition true, productPolicy published and capabilitiesPassed true after warm-up across reloads. E5b3-1 PARTIAL: settled Active and passed cargo version-reader/self-test, but immediate Warming was not captured. F8a-2 and F8bg-1 PASS: required capabilities active, optional states/reasons and owners captured.

The sampled resident IDs/home/work assignments c1/c500/c957 match across the 09:00 reload. Two full console records match; c1's next_event changes from 1989.7508482813714 to 1981.6334733148396. No full state-equality claim. Earlier setup checkpoints labelled H1/Hour were actually saved at 08:00 after an interrupted advance; only the Verified checkpoints prove the hour.

Positive session cleanup: normal stop, mods and lifecycle hash restoration passed; lease free, pending 0. Raw evidence: `<stage>/runs/positive`.

## Group 3 ? Driven Rides absent

F8a-4 and F8bg-3 PASS. Candidate city runs with `clock.drivenrides required-when-present absent: Driven Rides is not loaded`; activation log records `City clock factor=0.33333333333333331; Driven Rides coordinated=False`. This matches the compiled default; no config override. Original autosave preference read back without editing: 5 minutes / 3 saves. Native autosave guard remains enabled/off; preference sliders are intentionally unchanged. Native Options readback: `<stage>/runs/dr-absent/results/016.json`. No foreground input.

Normal stop, modset restore and lifecycle file/registry hash checks passed; lease free, pending 0. `<stage>/runs/dr-absent`.
