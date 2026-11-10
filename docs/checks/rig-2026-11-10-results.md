# Batched rig results ? 10 November 2026 (F8h check 1 + post-9 Nov pending checks)

Intermediate background run authorised by Peter. Tested `b3e5a9101fe065662c0baf3aedab01caa123773e`; the fetched origin tip matched the requested candidate. No merge or release action.

**Summary.** Of 141 numbered rows in the requested slice catalogue, **2 passed, 0 partial, 0 failed, 139 not run**. The two passes are the same F8 check 1 execution, cross-listed as F8a-2 and F8b-g-1. No distinct check is counted twice within a slice. X1 and X2 list no in-game checks. The run was **stopped** by the local `WRAP_UP_NOW.md` handoff at 21:24 Europe/Berlin, recording Peter's t110u instruction that the Mac takes the remaining scope. The interrupted smoke flow executed no check steps. No product failure is inferred from that interruption.

`<stage>` is `%USERPROFILE%\HART-Builds\persistent-city-rig-2026-11-10`; `<agent1-profile>` is `%USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests`. Raw logs, request/reply evidence, backup manifests and the full check definitions stay in the stage.

## Build and rig

| Item | Value |
|---|---|
| Host / role | HomePCHartwieg / `rig_test`; Windows, isolated agent1, background mode. |
| Head / origin | `b3e5a9101fe065662c0baf3aedab01caa123773e`, verified by fetch before the detached-worktree build. Main and concurrent acceptance worktrees untouched. |
| Build command | `python tools/build.py --game-managed "<Steam>\Big Ambitions_Data\Managed"`; preceded by the requested `wait-other-dotnet.ps1 -Repo <build-worktree>` and PATH prefixed with `%LOCALAPPDATA%\Microsoft\dotnet`. The wait script is now a no-op and released immediately. No offline dotnet tests run. |
| DLL SHA-256 | `0f34e6e22e5157f7aed25b41fe8cb2af14727275cdea99fbf64a5bd5be476c2d` |
| Rig / installed overlay | Read-only checkout `cfcc5fde3f652356a5aa3585250f4a92604557fb`; runtime 7; installed Ops `4b16c613eb2d8bcb90da0f5665ea5f2d6704d21177a01bda35195316c69d2f24`, Input `c9d93537eea54abf8c7fe513cdfe708ded299301a6f652eea646c9a4c445d6c0`. No overlay update. |
| Install / mod set | `C:\BigAmbitions-AgentTests`; RIG-MODSET staged 30 DLLs, including RLO, Driven Rides and FoodTrucks 0.10.2. This is the prior isolated full modpack without its optional E5bNativeRigProof diagnostic DLL. Complete relative DLL paths and hashes are in the check-1 input. |
| PC config | Existing config retained: Enabled/PhysicalEnabled true; MaximumPedestrians 48, MaximumVehicles 24, PhysicalRadiusMetres 120. No developer override staged. |
| Autosave | Native preference bytes/key backed up unchanged; no separate numerical preference readback was taken. `rig_test.enabled=true`, `autosave_disabled=true` verified before new_game and after city load. No measured windows. Manual Ready save occurred outside timing. Exact lifecycle files and isolated HKCU export restored and hash-verified. |
| PC load | F9h acceptance testhost observed at preflight and left untouched. One later sample: CPU 27.3%, RAM 72.6%, dotnet 1, testhost/MSBuild 0. No timing window or performance gate claimed. |
| Read-only provenance | All referenced Steam assemblies match `steam-1331550-build-25482473`. Installed rig raw fingerprint is `unknown`: `BigAmbitions.dll`, `HGPlugins.dll` differ, as expected for the private patched player; not silently classified as the declared unpatched set. `<stage>/provenance.json` retains per-assembly hashes. F9 native provenance/acceptance remains NOT RUN. |
| Cleanup | 303/303 protected file/hash-or-absence entries verified; 301 existing files, two expected absences; no extra ModsLocal files. All three lifecycle restores verified, including isolated registry hash `03f3d95c25debe2647ea840aab60557b4599cbfcfed9691efd723f0cb59eacb0`. Lease free; no isolated player or owned worker remains. |

## Runs

| Run | Fixture / purpose | Result |
|---|---|---|
| `head-producer` | Native new_game at candidate | Setup stopped: installed Ops returned `ops_unknown_op` for `e5b_proof runtime`. Fresh initial save retained; no check result. Full cleanup verified. |
| `head-producer-b` | Second fresh native new_game; existing capability console used for readiness | PASS: city running, check-1 reports captured; explicit Ready save retained. Full cleanup verified. |
| `smoke` | Fresh candidate Ready checkpoint copied by existing flow | Interrupted during startup selfcheck (`KeyboardInterrupt`); zero check steps executed. Full cleanup verified. |

## F8h check 1

**Verified by rig.** [Small committed F8h input](f8h-check1-modpack-owners-pc.json): `city running`; five required capabilities active (including Driven Rides when present); twelve optional active; `vendor.streetfood` off because the pinned Street Food vendor API is missing, `interior.view` off by default setting.

The report lists **49/49 patched target methods, eight targets with foreign owners, five foreign owners**: `cityperformance.mod`, `drivenrides.mod`, `foodtrucks.mod`, `hardmodepatch`, `agent-tests.rig-input`. Patch classes are retained per target: may-skip, observer, swallows and rewrites. No foreign owner is reported on `ProcessAllCustomersFromThisHour`, `ProcessCustomer`, customer-entry helpers or `IsPriceAcceptable`. `RunMainGameTick` does have foreign prefixes/finalizers; consult the input rather than interpreting the customer-method observation as a city-wide absence of conflicts.

Activation excerpts retain the early `not checked` statuses; the subsequent settled status is active. The input includes the exact head, DLL hash, fresh fixture checkpoint, full DLL inventory and activation scan line. The 16.278 ms timing in that scan line is diagnostic only: it was recorded on the shared machine outside any measured window and is not a perf-gate sample. No conflict refusal was implemented or tested. F8h itself remains NOT RUN.

## Findings

- The full isolated modpack supports the required capability report at this head. The optional vendor API is absent in the pinned FoodTrucks build, so dynamic vehicle-sale checks have no positive fixture here.
- The old installed overlay lacks the E5b readiness op expected by the copied producer. The second producer used the existing console status; no new rig capability or product code was installed.
- A fresh candidate city started and was manually saved. X3's one-hour/menu/reload check did not run, so this report does not claim schema-one reload or resident-identity equality.
- The local handoff stopped further PC work. Existing runnable checks remain NOT RUN; no permission or capability is inferred from source alone. No shop rental was attempted, no campaign bought, no native rival fields written.

## NOT RUN grouped by missing capability or fixture

These groups describe outstanding prerequisites and may overlap; the per-slice table below contains the counts.

| Missing capability / fixture | Checks left outstanding |
|---|---|
| Stopped by PC-to-Mac handoff | X3-1; E5b3-1; E5b5-1; F8a-4/5 and F8b-g-3/4 (Driven Rides/RLO absence); further console/catalogue/save/load work. |
| FT-F4 fixtures; RIG-PRESENT, RIG-FOREGROUND, RIG-PRIORITY, RIG-F4-PRODUCER, RIG-SHOPFIX, suitable RIG-PERF-UNION overlay/counters | All F4a/b/c/d/e checks. No F4f window or F7e run attempted. Installed Ops was left unchanged. |
| FT-F9 / `f9-city-ready`; RIG-ACCEPT | All F9a/c/e/g/b+d+f/h catalogue rows. Read-only assembly hashes above are supplemental evidence, not a native acceptance record. |
| RIG-FOODSHOP: staffed/stocked shop with native sales, multi-shop and dynamic-menu variants | E5b3-2?6; E5b4-4?6; E5b5-2?6; F8 mechanism reports requiring shop/cart scopes. No positive or negative shop result claimed. |
| Current visitor/mover fixture, body-ID readback, RIG-PCFIX / RIG-LIVE; PhysicalEnabled/config/replay/drive support as specified by each check | F5i-1?6; F2e-1?2; F2g-1?11. Frame-cap/real-input work was not attempted. |
| Street Food vendor API and crewed dynamic cart/truck fixture; held-offer tracing / RIG-SFAPI / RIG-VLEDGER | E6f-1?7; F8 Street Food absent/return and mechanism arms. The live optional capability explicitly reports the API missing. |
| RIG-FAULT / RIG-PCSAVE / RIG-RLOTOGGLE | F8a-6?9 and F8b-g-5?8. Explicitly excluded from this intermediate run. |
| RIG-HARMONYOWNERS / RIG-FOREIGNPATCH and F8h scope | F8a-1 exact PC-owner inventory; F8a-11 conflict-refusal arm. Check 1 reports foreign owners but does not prove every PC-owner multiplicity assertion of F8a-1. |
| Older/future-writer fixtures or payload-write capability; superseded writer expectations | E5b4-1?3 still name writers 35?37. Schema is held at 1; no old positive fixture reused, no future-payload injection attempted. X3-1 is the current fresh-city check. |
| Clean union timing window | F8a-12 / F8b-g-10 and all other performance rows. No performance table: no window was measured. |

## Fixtures

All created through native new_game at this candidate in the isolated agent1 profile. No older fixture loaded. Disposable outputs remain in place; temporary flow copies were removed. Paths below expand only under the isolated profile.

| Name | Path | SHA-256 | Note |
|---|---|---|---|
| `FT E5b Rig 10 Nov 2026 b3e5a910` | `<agent1-profile>/SaveGames/Big Ambitions/Ug1T60owDEehEBaFUeyFA==/FT E5b Rig 10 Nov 2026 b3e5a910.hsg` | `80d1b25f117c6868a17ea5ec2e4321c999d59e0d3c95653ee86c25028a48005e` | Initial attempt |
| `FT E5b Rig 10 Nov 2026 b3e5a910 B` | `<agent1-profile>/SaveGames/Big Ambitions/4DRhATP5gkObRdQH6L+mbw==/FT E5b Rig 10 Nov 2026 b3e5a910 B.hsg` | `7712433058b6f7fb248bc283a63a9171a0b30717b4c5649687db0f16af369bab` | Successful producer |
| `FT E5b Rig 10 Nov 2026 b3e5a910 B Ready` | `<agent1-profile>/SaveGames/Big Ambitions/4DRhATP5gkObRdQH6L+mbw==/FT E5b Rig 10 Nov 2026 b3e5a910 B Ready.hsg` | `853fe02449277d9ab66ec1b33d9780b9e41e0267ff959b3b8326e30244954320` | Successful producer |


The successful producer's city contained 957 residents. It had no player food business or cart; check 1 does not require one. No fixture is offered as FT-F4 or FT-F9.

## Per-check results grouped by slice

Definitions were read from the current `docs/checks` where present, otherwise from `docs-history-2026-11-10`; the F8 plan's check 1 was cross-checked there. The stage retains `check-definitions.md` and `check-catalogue.json`. Ranges below refer to numbered checks in each slice's ?In-game checks? section; NOT RUN includes work stopped by the handoff, even when an existing capability could have run it.

| Slice | Passed | Partial | Failed | Not run | Per-check result |
|---|---:|---:|---:|---:|---|
| e5b3 | 0 | 0 | 0 | 6 | 1?6 NOT RUN. |
| f2e | 0 | 0 | 0 | 2 | 1?2 NOT RUN. |
| f9c | 0 | 0 | 0 | 6 | 1?6 NOT RUN. |
| e5b4 | 0 | 0 | 0 | 6 | 1?6 NOT RUN. |
| f5i | 0 | 0 | 0 | 6 | 1?6 NOT RUN. |
| f4a | 0 | 0 | 0 | 8 | 1?8 NOT RUN. |
| f9e | 0 | 0 | 0 | 6 | 1?6 NOT RUN. |
| x2 | 0 | 0 | 0 | 0 | No in-game checks listed. |
| x1 | 0 | 0 | 0 | 0 | No in-game checks listed. |
| e5b5 | 0 | 0 | 0 | 6 | 1?6 NOT RUN. |
| f4e | 0 | 0 | 0 | 5 | 1?5 NOT RUN. |
| f4b | 0 | 0 | 0 | 8 | 1?8 NOT RUN. |
| f8a | 1 | 0 | 0 | 11 | Check 2 PASS; 1 and 3?12 NOT RUN. |
| e6f | 0 | 0 | 0 | 7 | 1?7 NOT RUN. |
| f4c | 0 | 0 | 0 | 5 | 1?5 NOT RUN. |
| f4d | 0 | 0 | 0 | 15 | 1?15 NOT RUN. |
| x3 | 0 | 0 | 0 | 1 | 1 NOT RUN. |
| f2g | 0 | 0 | 0 | 11 | 1?11 NOT RUN. |
| f9a | 0 | 0 | 0 | 10 | 1?10 NOT RUN. |
| f9g | 0 | 0 | 0 | 4 | 1?4 NOT RUN. |
| f9bdf | 0 | 0 | 0 | 4 | 1?4 NOT RUN. |
| f8bg | 1 | 0 | 0 | 9 | Check 1 PASS; 2?10 NOT RUN. |
| f9h | 0 | 0 | 0 | 3 | 1?3 NOT RUN. |

Raw evidence and all large outputs remain outside Git. No issue was created or edited. The docs-only results PR is not to be merged by this run.
