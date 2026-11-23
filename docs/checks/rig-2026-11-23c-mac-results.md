# Rig results — 23 November 2026 c, Mac (rig 17d): #350 gate-bounds checks at ce8c514d

**0 PASS / 1 PARTIAL / 1 FAIL / 1 NOT RUN** across [PR #350's three checks](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/350). Pointer, hover and visual feel are **NOT RUN (for Peter)**, outside those counts.

| Check | Result | Evidence |
|---|---|---|
| 1. Fallback tally | **FAIL** | The requested warning appeared zero times at load, during 60 wall seconds from day 2 15:00 to 15:16 at 1x, or through shutdown. Two periodic dependency checks fell in the tally log, and the subsequent population summary recorded 1.593 ms maximum for the door check. The expected construction line was missing, so the fallback count and door names are unknown. |
| 2. Hamptons private gate | **PARTIAL** | The live map listed 12 Hamptons residential properties for sale. The building read for 1 East Hamptons Way reported private-fence index 0. The survey showed its closed driveway gate and the driveway leading to a junction with another barrier. Seven screenshots over 193 wall seconds (day 2 15:44–16:39) showed no gate crossing or moving traffic at the junction. Citywide counts varied, but the rig could not locate those vehicles on this road or confirm the segment's public classification. The open-gate arm was **NOT RUN** because the bridge exposed no gate interaction. |
| 3. Footprint gates | **NOT RUN** | The live building op exposed residential collider bounds. Neither it nor the mod inspector exposed each gate's chosen source, its expanded box, or public-road segment positions. The footprint-gate inventory, intersections and local stops or detours remain unknown. |

In `PublicRoadAccess`, the constructor compares the new fallback-name array with an empty array when no previous snapshot exists. An empty tally therefore produces no initial warning. This explains how the missing line can occur. The live rig could not independently confirm a zero fallback-gate count or exercise the unchanged, nonempty warning path.

The bridge's `use {"op":"list","radius":12}` returned `"things":[]` at the gate approach `(-2808, 0.06, -1463)`. The 40 m query from the driveway also returned no targets. The installed rig operation catalog supplied no gate toggle. `pc road status`, `pc gate status` and `pc traffic status` each returned `Warning: [Persistent City] Unknown command. Use: pc population census [workplaces] | pc population person <id> | pc population manifest`. The bridge rejected the read `crew_drive_probe {"op":"waypoints","ids":[0]}` with `Unknown command 'crew_drive_probe'.` These are readout and interaction limits.

## Build and isolation

| Item | Evidence |
|---|---|
| Tested tip | `ce8c514d313476ada28bd99ef1476bf480c18976` after `git fetch origin` at setup and post-run. |
| Player | Isolated `agentmac`, build 3682, Unity 2022.3.62f2, RLO 1.0.6. Background graphics for the gate screenshots. |
| Fixture | `FT GR5 13f Timing v1` loaded in 54.3 s. Persistent City reported 1,000 residents, 2,000 visitors and 1,683 places. The first traffic summary reported 13,965 road sections. |
| Staging | `python3 scripts/install-mac.py --stage-mod PersistentCity ~/HART-Builds/persistent-city-rig-2026-11-23c-mac/package` (the command used the absolute package path). Staging refreshed 28 entries. Receipt backup `~/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261123T123305Z`. |
| Live DLL | `[AgentRigOps] staged_mod name=PersistentCity version=absent informational=0.10.0-persistent-city-candidate sha256=79024c3bb370d27fce1696e4c1b85411fa85f507bb8b18c91d2e741366f80c74` matched the build and staging output. |
| Agent | agent-rig `9cee682`. AgentRigOps DLL SHA-256 `a775ba0c17be67fa2f355a5e4240b31dd074955bafa833664d8e5884a246297c` before and after. |
| Autosave | Original native preference was 5 minutes, 3 saves, `prevent_autosave=false`. The `--rig-test` guard read `suppressed=true` at menu, after load and before and after the tally and Hamptons observations. The final guard read counted 3,674 refused and zero allowed saves, with no save or compression in progress. SaveCostDiagnostics was off. |
| Restore | Closed with `mac-agent stop`. Restored the receipt and original mod directory from the staging backup, restored the isolated preferences, and verified all 34 manifest paths, 53 RLO files, four sealed fixture files and the complete installation receipt. The `FT RIGTMP-GR5-15c` and `FT RIGTMP-GR5-23c-mac` reads both returned `exists=false`. Released the lease after verification and confirmed the game was stopped and the lease free. |
| Host load | Start 13:32 CET, `3.12 3.10 3.10`. End 13:41 CET, `5.06 3.96 3.48`. Shared Mac. |

Raw evidence is in `~/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-23c-mac/`, including the rollback command in `stage.log`, `Player-tally.log`, seven `hamptons-closed-*.png` captures, and the restoration readbacks. Final `Player.log` SHA-256 is `f5ec5d35aaaac98e69bdb33d7de3e235e15818c46ce18c385b6a1f5649d2f4ad`.

## Incidental timings

**PROVISIONAL (shared Mac).** No GR-5 windows, no reference sessions and no probe installation. The mod printed no mod-work p95. Whole-frame timings were not measured.

| Player.log | Mod work at or above 30 ms |
|---|---|
| line 1787 | Startup journey retime, frame 3338, 131.4 ms. |
| lines 1789 and 1798 | Startup routing preparation, frame 3394, 129.935 ms. The planner reported a 120.491 ms road-scene search. |
| line 1804 | Physical population setup, 138.0 ms after 1.6 ms earlier in the update. |
| line 2010 | Maximum advancement call 143.025 ms. The same summary reported a 139.929 ms optional-stop performer scan. |

Refs #350, Refs #331
