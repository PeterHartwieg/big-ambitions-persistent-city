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

## F8h check 1

**PARTIAL; install/refusal/remove/reload assertions verified by rig.** Also covers the same F8a-11 foreign-prefix row. Owned-shop hand-back, lunch service, sale suppression and reacquisition require the shop fixture and are not inferred from capability reports.

On this run's fresh Ready city, `foreign_patch install` added owner `agent-rig.foreign.pc1111c`, kind `prefix`, to `RetailBusinessSimulator.ProcessCustomer(CustomerEntry)`. Its native readback included `AgentRigOps.ForeignPatchWorld.ForeignPrefix`. Reloaded status:

```text
food.checkout.shop optional off: foreign prefix by agent-rig.foreign.pc1111c on RetailBusinessSimulator.ProcessCustomer(CustomerEntry)
```

The hooks entry ended `-> refuses food.checkout.shop`, with one refusing entry; the activation capability line and deciding hook scan named the same refusal (scan 13.002 ms). `foreign_patch remove` succeeded. After reload, status reported checkout active, hooks reported 49/49 targets and 0 refusing, and `ProcessCustomer` had no foreign owner. The final owner list matched the initial owner list. Live PC keys contained no food-sale receipt key. Raw evidence: `<stage>/runs/foreign2/results`, `capability-summary.json`, `foreign-refusal-Player.log`, `foreign-restored-Player.log`.

The first attempt (`<stage>/runs/foreign`) is retained: after a successful install, the stage loader accepted stale `load=done` state and attempted removal during the menu transition. Exact removal error: `ops_city_required`. Normal process exit removed the process-local owner; mods and lifecycle were restored. The stage loader was corrected to require a new native `Test bridge: load '…' done` log line. The second attempt ran the full cycle; no rig checkout or overlay change.
`foreign2` then advanced naturally from 08:00 to 09:00, paused at the boundary, with checkout active and no accounting refusal. Cleanup stopped only the owned game, restored mods/preferences, and released the lease (all cleanup actions succeeded).


## fix-reload-accounting-epoch checks 3-4

**Check 3 PASS.** `rlo_option` read Social Media live-on, stored absent, `original_saved=false`; set-off and readback confirmed live-off. Reloaded S1 with autosave suppression verified. During 20.203 seconds unpaused, one new warning said `Persistent City startup is still waiting after 15 seconds; native time is released.` Native time advanced 08:00→08:15; frames advanced 7242→7763, and the city continued answering commands. Capability status was `start waiting (rlo.donor transient)` with `transient (Off): Flare is not running`, as expected for this startup case. Evidence: `<stage>/runs/epoch/epoch3-wait20-clock.json`, `epoch3-loaded-Player.log`, `epoch3-complete-Player.log`, results 016–025.

Check 4 and the related mid-session switch are being executed in the same owned lifecycle. The first S2 checkpoint's saved marker is minute `1922.0277554988861`; it crossed into the next minute before pause, so it is not the requested late-minute setup. A subsequent check 4 run will require a verified saved fraction before a verdict is completed.

