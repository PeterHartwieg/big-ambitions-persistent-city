# PC rig proof (agent1), A1 inspector

**Verdict: BLOCKED.** Role: `rig_test`. Tested PR #4 head `ee9486b17db082aff1f764251568fb4670ffca2a`; requested BEFORE `73129e0fc93772e4d1460a71aea7d174186454e1`. Initial fetch confirmed the PR head was unchanged. This report does **not** clear the native merge gate.

Windows found the isolated game window, but activation failed (`failed to activate captured window`) and capture failed (`SetIsBorderRequired failed: Element not found. (0x80070490)`). Refresh/rebind and the alternate app surface failed too. LockApp was running; that alone does not establish a locked desktop. A desktop unlock was requested, with no response during the run. Bridge game-view capture and native observers worked. No real hover/click/wheel/drag gesture was injected or inferred from a method call. [Blocker](ui-blocker.json).

The FAIL entries below mean required evidence was **not established**, not a demonstrated production defect.

| Required item | PASS/FAIL | Native evidence / limitation |
| --- | --- | --- |
| 1. Walker hover; clear off; exclude HUD/dialogs/windows | FAIL | Pointer interaction blocked. BEFORE has no inspector patches; AFTER exposes the inspector and installs all three input patches. This is presence evidence, not hover red/green proof. |
| 2. Click popup and resident content, localized addresses, personality/story/destination/meals | FAIL | No popup could be opened with real input. Content and refresh unverified. |
| 3. Home/work links focus native map at correct building | FAIL | Real links were not exercised. |
| 4. Click-through, wheel, release/hold-walk, right-click, placement | FAIL | Real input blocked. Patch installation alone cannot certify gesture safety. |
| 5. No new PersistentCity exceptions or inactive patches | PASS | Startup plus three AFTER windows: no PersistentCity exception or inspector guard warning. Mouse, camera and GraphicRaycaster patches present at end; BEFORE has none. Both native outdoor culling exception stacks occur in BEFORE and AFTER. [Comparison](log-patch-summary.json). |
| 6. Inspector is read-only | FAIL | Native inspector-use comparison not exercised. Offline invariant tests pass separately; unchanged save files during timing do not prove live simulation/economy equality. |
| 7. Picking in closed/hover/open; mod work and whole frames | FAIL | Closed state measured in three clean windows per build and passes the measured 5 ms gate. Hover/open missing; whole-frame regression observed. Full acceptance remains unverified. |

Native game-view captures: [BEFORE](screenshots/before-closed.jpg), [AFTER](screenshots/after-closed.jpg). These show the loaded normal top-down fixture with no popup; they do not establish click red/green proof. The phone collapse animation is visible in these paused startup captures. No screenshot is claimed for an unperformed interaction.

## Build and offline checks

Both production builds succeeded against read-only agent1 Managed references. `BA_GAME_MANAGED` was absent from this shell; the isolated Managed directory was supplied. BEFORE additionally needed the existing per-user dotnet directory on PATH. No source was edited to build either version.

| Artifact | DLL SHA-256 |
| --- | --- |
| BEFORE | `f7f0c36e42dcd2b66067245602f27622a2020c2df5670ecf636e9b9525ca471c` |
| AFTER | `4cb1181f46ec5536ed9922799601c27379c296ddece39b278c4e4f84222b414c` |

AFTER `dotnet test tests/PersistentCity.Tests.csproj --disable-build-servers`: **1,567 passed, 0 failed, 0 skipped**, exit 0. Offline tests are separate from native evidence. All test/dotnet processes from this run exited. [Build/test summary](offline.json).

## Performance

All numbers are milliseconds. Each row is p50 / p95 / max. The aggregate pools individual frame samples from three windows; it does not average percentiles.

| Closed, cursor still | Combined mod work | Whole-frame interval |
| --- | --- | --- |
| BEFORE aggregate | 0.2719 / 0.3864 / 9.0795 | 11.1770 / 12.7843 / 214.6950 |
| AFTER aggregate | 0.3505 / 0.6780 / 12.9474 | 12.3470 / 17.5707 / 67.2598 |
| before-closed-1 | 0.2658 / 0.3523 / 8.1211 | 11.1615 / 12.7256 / 214.6950 |
| before-closed-2 | 0.2636 / 0.3664 / 9.0734 | 11.1699 / 12.6586 / 24.3379 |
| before-closed-3 | 0.2902 / 0.4257 / 9.0795 | 11.2096 / 12.9697 / 25.9942 |
| after-closed-1 | 0.3078 / 0.7155 / 12.9474 | 11.9299 / 17.6211 / 67.2598 |
| after-closed-2 | 0.3352 / 0.6127 / 10.7020 | 12.2846 / 17.2476 / 42.9218 |
| after-closed-3 | 0.4116 / 0.7004 / 11.8542 | 13.1034 / 17.8250 / 46.6657 |

**Measured closed-scope gate: PASS**, every AFTER window p95 < 5 ms. No combined mod spike >= 30 ms in either build; aggregate maxima 9.0795 / 12.9474 ms. BEFORE has one 214.695 ms whole-frame stall; AFTER maximum is 67.2598 ms. Whole-frame stalls are tracked independently of mod work. **The broader native-log brackets also report stalls up to 544 ms**, including capture setup/export edges. Their diagnostic records have no frame-ID/timestamp mapping into the narrow capture; they remain unaligned and unexplained, not silently discarded. The sampled maxima above do not describe all wider-session stalls. [Native bracket records](native-bracket-stalls.json).

**Whole-frame regression:** pooled p95 increased **37.4%**. Median-window p95 also worsens beyond either build's observed range, exceeding the plan's 10% reference target. System workloads were uncontrolled, input-hook overhead was not separately attributed, and this run does not establish the cause. This regression requires follow-up even though the measured mod-work gate passes. [Within-build comparison](performance-noise.json), [stall context](whole-frame-stalls.json).

| Inspector state | Per-frame picking work | Inspector.Update p50 / p95 / max | Native coverage |
| --- | --- | --- | --- |
| Closed + still | **0 pick passes** across all three AFTER timing brackets; no picking pass cost | **0.0048 / 0.0093 / 0.0668 ms**; mean **0.00543 ms/frame** | 180.813 s, 14,221 attributed mod frames |
| Hovering / moving over residents | Unmeasured | Unmeasured | Real pointer input unavailable |
| Popup open | Unmeasured | Unmeasured | Real pointer input unavailable |

The Update figure includes closed inspector checks and timing overhead; it is not a hover-picking measurement. Inspector cumulative timing brackets extend slightly beyond the narrow frame capture, but show zero picks/builds/hover/open calls throughout. Closed inspector draw is measured separately; the whole PopulationRuntime.OnGUI p95 is 0.0038 ms BEFORE and 0.0078 ms AFTER. No evidence-backed cheap inspector optimization was identified; separately profiling the input/raycast/camera guards is a useful follow-up, not an implemented change.

## Method and limits

- Same freshly copied disposable `FT Food Routines PR4 A1 20261030` city, retained `morning-base.hsg` FT fixture; day 5 at 07:45, 957 residents, 1,059 places. Normal native speed, one-third calendar factor, 15 s unpaused warm-up after runtime readiness; three >= 60 s windows per build. BEFORE ran first, AFTER second. No ordinary save was loaded.
- Agent1 only, 1920x1080, Low quality, D3D11, 90 FPS target. Arrangement follows the performance review: original RLO copied unchanged, Persistent City plus the bridge-only host and frame observer. Other original gameplay mods are excluded for both builds; this is not full-modpack certification.
- Original autosave state was enabled (`preventAutoSave=false`, five-minute interval, three slots). Disabled before timing and checked before/after every window, including fresh processes. All six windows verify no saving in progress, no save-file size/mtime change and no native save activity. Broad save-log counters increase because native frame diagnostics say "not saving"; every save mention in all six brackets was checked and is such a diagnostic. No manual save occurred during a measured window. [Window validity and timing](windows-summary.json).
- Mod work = **Update/authority union + whole PopulationRuntime.OnGUI on the same Unity frame**. Inspector.Update is nested in Update and is never added again. Existing raw frame method and floor(n*q) percentiles reused; first partial wall interval retained and current partial mod frame excluded. Input Harmony guard callbacks are outside these existing primary scopes and were not separately timed; therefore a full inspector-inclusive 5 ms certification is unavailable.
- Zero timing exceptions, cross-frame attribution errors, dropped mod frames or active frame reports in the six captures. Native system/GC/GPU work is not attributed solely to this mod. Raw timestamp/GC/component CSVs are compressed alongside [provenance](raw-provenance.json).
- Test tooling only: adapted prior rig scripts and performance observer; frame helper conditionally omits the absent BEFORE inspector seam, retaining OnGUI. A private observer snapshot serialization error on BEFORE was fixed in test tooling before AFTER; frame helper bytes are identical across arms. No production/RLO behavior was changed. Script templates contain sanitized private-path placeholders and require stage/profile configuration; other frame-helper sources come from the tested tree's tools/food-demand-audit.

Public evidence integrity: `python verify_evidence.py` recomputes all six per-window and pooled distributions, checks autosave and attribution counters, and verifies the file manifest. This command passes.

## Cleanup and scope

**PASS: restored and released.** Full original profile manifest: **4,828/4,828** hashes match, including ModsLocal and persistent-city.json. Original **107/107** install references match. Preferences/registry export hash matches, restoring autosave settings; original install receipt, run metadata and native log hashes also match. Root/preferences copies were made before installation; their additional root hash manifest was consolidated later from those originals. Agent1 is stopped; lease released with no pending operations; no testhost/dotnet process from this run remains. [Restore](restore-receipt.json), [lease release](lock-released.json), [backup](backup-receipt.json).

Private backups, lease token, raw saves and full native logs remain in the authorized private stage. They are excluded from this evidence. No production changes, normal-profile changes, Steam/Workshop operations, RLO changes, merge, approval, issue, force-push or protected-branch push was performed.
