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
