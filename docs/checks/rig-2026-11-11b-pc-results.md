# Rig results — 11 November 2026 b PC (pcsave/pcstate/modwork re-runs of #209 rows)

Role `rig_test`, HomePCHartwieg, agent1 background. Run in progress; this commit preserves the first completed group.

[Combined checklist](rig-2026-11-06-checklist.md). [Previous PC report (#209)](rig-2026-11-11-pc-results.md).

## Build and rig

| Item | Evidence |
|---|---|
| Candidate | Fetched `113a5e32d2f2a9a54f242186ab1632197e9674c6`, newer than requested `a089de2c`. Detached build worktree. |
| Build | Requested build command succeeded; dotnet PATH prefixed; wait-other-dotnet ran first. No offline test suite. |
| DLL SHA-256 | `4c500997601b098e18665feca8d2b6a3130e7a0180249f515944ce09d6436850` |
| Rig source | `cfcc5fde3f652356a5aa3585250f4a92604557fb`; read only; no overlay update. |
| Installed Ops SHA-256 | `e9ae1a640fad85d93e30c2afb1383221c012d90a7771691d68bd4567b30ba961`; live capabilities pending verification. |
| Backup | Current post-update state, 301 protected entries in `<stage>/backup-manifest.json`; not the rollback revision. |
| Stage | `C:/Users/Peter/HART-Builds/persistent-city-rig-2026-11-11b-pc` |

## Runs

| Group | Result |
|---|---|
| Native fresh fixture | Producer PASS, city running, ordinary Ready checkpoint written; normal stop, lifecycle/modset restore and lease release completed. `<stage>/runs/creation/report.json`. |

## Fixtures

`FT E5b PC 11 Nov b 113a5e32 Ready`, isolated `SaveGames/Big Ambitions/HzeC2ec54UuZDSJce9cxuA==/FT E5b PC 11 Nov b 113a5e32 Ready.hsg`, SHA-256 `cc473dd9d7b4127227c2d9e21f6b67fadfd83d2a6731debff4df4a3be93224ce`.

## Findings

Fresh native creation and Ready save succeeded. Payload, digest and timing readbacks are next. All #209 passes remain carried evidence; no pending capability is assumed present.

Completed group: payload and success epoch. Raw evidence: `<stage>/runs/positive`; progress and request/reply files retained.
