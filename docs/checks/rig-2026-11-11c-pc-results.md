# Rig results — 11 November 2026 c PC (FOREIGNPATCH + RLOTOGGLE + shop-rental retry)

Role `rig_test`, HomePCHartwieg, Windows background agent1. Candidate `10b2d7bfab033db9c97cf811a81b940d7a22f9d1`, fetched and verified before build. Schema held at 1. Testing only; no production changes, release, Workshop, version/changelog, issues or merge.

[Combined checklist](rig-2026-11-06-checklist.md). [Combined batch #211](rig-2026-11-11-batch-results.md). [Previous PC #210](rig-2026-11-11b-pc-results.md).

`<stage>` = `C:/Users/Peter/HART-Builds/persistent-city-rig-2026-11-11c-pc`. Raw replies, logs, binaries, saves and backup bytes remain outside Git. Earlier stages and the rig checkout are read only.

## Build and rig

| Item | Value |
|---|---|
| Candidate | `10b2d7bfab033db9c97cf811a81b940d7a22f9d1`; requested tip unchanged. Detached build worktree `rig-2026-11-11c-pc`. |
| Build | Requested `tools/build.py` succeeded; PATH prefixed with `%LOCALAPPDATA%/Microsoft/dotnet`; wait-other-dotnet script ran first (no-op). No offline dotnet suite. |
| DLL SHA-256 | `4c500997601b098e18665feca8d2b6a3130e7a0180249f515944ce09d6436850`. |
| Rig checkout | `19595b849f83c4afe9a328014a8566ca9af482ae`, clean, read only. |
| Installed overlay | Ops ProductVersion `1.0.0+19595b849f83c4afe9a328014a8566ca9af482ae`; Ops SHA-256 `576789cf42a17cb65f9258a8ad5e56367c2f367aadc9fc90a890500d1e440702`; runtime `f35e5848fd8fcec41af32ab7ef455be1c24b7ebe5fa41f5e17a21795b978043e`; Input `6739396f4a782f955826a629ffbe3672c7e6f5348d5229e1077e5357bcc0276b`. No overlay installation/build/update. |
| Backup | Current post-install 301-entry SHA-256 verified backup plus exact isolated HKCU key; `<stage>/backup-manifest.json`, SHA-256 `d94da116039a5dfbda1eab970fd5595a8a7a316d2fab0e7dd4f63739b4f5fe31`. Rollback revision `20261111T084212647` untouched. |
| Isolation | Fixed agent1 background player, port 18330; stable owner `pc-rig-2026-11-11c`. No foreground, real input, normal profile, normal ModsLocal or normal bridge access. Full receipt-hashed 30-DLL modpack. |

## Runs

Setup and build implemented. Native producer currently creating this run's disposable FT city; results will be appended after the segment finishes.
