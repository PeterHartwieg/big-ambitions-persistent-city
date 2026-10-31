# VERDICT: FAIL — check 6, 100% breakfast mod-work p95 5.683 ms exceeds 5 ms

Role: `rig_test`. Locked Windows merge-gating proof for [PR #13](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/13), E2a existing street-life proof and read-only venue discovery. **No merge clearance from this session.** The observed performance breach is the failure; unavailable opportunities and documented E2b reds are not labelled defects.

Candidate **`abd9bc1ac793a4ccb66a5f36415f75de708a4391`**, unchanged from the requested head after `git fetch origin` / `git rev-parse origin/codex/pc-e2a-street-life`. Baseline **`6d43276198eba5d0dd016b51bc7fc670bc651762`**. Evidence branch starts at fetched `origin/codex/persistent-city` **`707145d`**. No A1–A3/B0 source is inherited into the tested candidate.

Candidate built against agent1 Managed assemblies with the required per-user dotnet PATH prefix. Candidate, retained baseline and claimed DLL all have SHA-256 **`f7f0c36e42dcd2b66067245602f27622a2020c2df5670ecf636e9b9525ca471c`**. `git diff --stat 6d43276 abd9bc1 -- Core Compatibility Persistence Runtime config tools/build.py` is empty. Loaded assembly provenance confirms that hash. Therefore there are no base/candidate interleaved windows and **no claim that E2a introduced the measured cost**. [Identity](identity.json), [loaded DLL](cand100-provenance.json), [unchanged persistence sources/readbacks](save-contract.json).

| Check | Result | Evidence / boundary |
| --- | --- | --- |
| 0. DLL/source identity | PASS | Exact baseline/claimed/candidate hash; production diff empty; loaded hash verified. [Identity](identity.json). |
| 1. State/accounting matrix | PARTIAL | Breaks (future/active/cancelled/expired), paired chats, window shopping and park resting captured; all comparable chance deltas reconcile; 0% non-vacuous control passes. **NOT-RUN: opening queues and arrival audiences, no natural opportunity captured.** Optional binding/return flags captured; approach vs standing not distinguished. [State index](state-index.json), [full requested state reads](state-samples.json.xz), [chance audit](chance-audit.json), [caps](population-and-caps.json), [physical bindings](physical-binding-proof.json). |
| 2. Mid-stop reload and weather | PARTIAL | Eight save/reload comparisons preserve existing stop IDs, geometry, start/end, membership and original activity/event deadlines, including future/active breaks and both chat partners. Rain cancellation survives reload and clearing; no replay found. **NOT-RUN: queue/audience reload, absent opportunity.** [Reload proof](reload-proof.json), [rain follow-up](red-evidence.json). |
| 3. Food priority / interruption / payment | NOT-RUN (no matching work-break meal case) | Meal outings were observed from Home, with no matched optional stop; no accepted meal interruption of a scheduled work break was captured. Native NoStaff and low paper-bag warnings also occurred. No journey/receipt/payment or mid-interruption reload claim. Optional records never intersected saved food-shop places. [Coverage](food-priority-coverage.json). |
| 4. E2b red evidence | PARTIAL | R1 red reproduced; R2 source/double-only; R3 overlap not reproduced/established in sampled native objects; R4 source/double-only, no waiting-customer queue. These are evidence gaps/expected reds, not E2a failures. [Details](red-evidence.json), [native objects](native-venues.json). |
| 5. Read-only candidate venue facts | PARTIAL | Loaded flags/pools, 101 registrations, hours, closure, controller/address and doors read; captured production facade caches reported below. Warehouse has a catalog asset but no registration. Global entrance uniqueness/unknown geometry remain unavailable; no venue enabled. [Native rows](native-venues.json), [summary](venue-summary.json), [facades](facade-summary.json). |
| 6. Performance | **FAIL** | 100% breakfast **p95 5.683 ms**, clean ≥60 s window. Other three p95s pass. No mod-work spike ≥30 ms. Whole-frame stalls separate. [Every window/load/raw mapping](performance-summary.json). |
| 7. Load, ordinary save/reload, PC errors | PASS, scoped to PC | Eight schema-16 readbacks load and round-trip byte-identically; production contract/DLL unchanged. No Persistent City error/exception in four retained arm logs. Native outdoor music/hangout-controller exceptions are disclosed separately; no PC stack frames in captured contexts. Initial helper-only restart log was not retained as an arm log. [Contract](save-contract.json), [log review](log-review.json). |
| PR item 2: visible matrix | DEFERRED-INPUT | Peter's visual playtest, including facing, body correlation, blocked doors, duplicate standers and camera/pool continuity. Native captures below are supporting evidence only. |
| PR item 6: E2b visual reds/native respawn | DEFERRED-INPUT | No pointer/focus or visual-playtest assumption. |
| PR items 7/8: frontage and remaining UI input | DEFERRED-INPUT | Walkable off-flow frontage, safe facing, entrance/actor overlap, and pointer-driven UI actions. Phone collapse was exercised through a semantic bridge click only. |

## Native state evidence

Only copied, named **FT Food Routines Locked E2a** fixture cities were used in agent1; no ordinary save/profile was launched or changed. Starting morning copy is day 5, 07:45, seed `-1903163055`, schema 16, 957 residents / 1,059 places. The 25% arm advances through day/night. The 100% arm loads its saved night continuation and uses a labelled native `skiptime 48h true` seam to Monday. A native hospitalization following that long skip prevented a planned window from starting; Continue and native `tpa 23 ba:street_thirdstreet` returned the player outside. The exterior weekday checkpoint was saved before timing and reused for the 0% control. No failed/preparation attempt is counted as a performance window. Phone UI is expanded in the 25% windows and collapsed by bridge in the 100% arm. A read-only WTS query confirms session 11 remained locked at cleanup. [Session](session-final.json), [seam clocks](cand100-after-weekday-seam.json).

All state reads run on Unity's main thread through stage-only observers. No production source/DLL is instrumented. Native weather is changed only with labelled `ToggleRainInstant` console seams; view relocation uses a labelled player/view seam. No registration/spawn/pool getter or accounting mutation is called by the venue reader. [Observer source](E2Rig.cs.txt).

506 captured state snapshots retain one identity digest (`20e917890faad9447cbe170b83ed9e709921bd5cde325ef88e19eb4e319b511c`) and **957 residents**. Largest logical stop-record stock is **452**; it includes future/expired/cancelled records and is not a visible-body count. Captured pedestrian bindings reach **48/48**, cars **6**, optional bindings **4**. The unchanged maximum-256 clamp is source-backed, not saturation-tested in Unity. No new population or synthetic performer trip was injected; native audience-trip evidence remains unavailable.

The final 0% control has **573 eligible visits, 0 draws, 0 realised and 0 new stop records**. It preserves 23 accepted records inherited from the 100% checkpoint; these are not new admissions. The earlier 0% morning attempt had no eligible visit and is retained as a vacuous attempt. Neither control captured an opening queue, so queue independence is not claimed natively. All tested within-attach accounting intervals satisfy `delta draws = delta realised + delta pending + delta drops`; reload resets are explicitly excluded from cross-epoch subtraction. [Zero control](zero-control.json), [audit](chance-audit.json).

The binding audit captures `c174` bound to its break before/after reload and later `OptionalReturning=true`; window-shopping return and other break bindings also appear. The observer's raw `Step` is the queue enum, **not an optional-stop approach/standing classifier**. It is retained only as context. No standing proof is inferred from it. [Binding observations](physical-binding-proof.json), [supporting native view](cand100-day-physical.jpg), [night capture](night-supporting.jpg).

## Performance and load

All values are ms. Combined mod work is the same-frame Update/authority union **plus whole PopulationRuntime.OnGUI**. Instrumentation overhead remains. Nested components are not added; other hook bodies outside those scopes remain outside attribution. The existing observer records native query/event/component details in raw CSVs. There are no state snapshots, saves or status polling inside timed brackets. Initial/current partial mod frames are excluded; initial partial wall interval retained; percentiles use `floor(n*q)`. Normal speed, one-third calendar factor, Low/1920×1080, 90 FPS target and vSync 0. The two rates/clock/locations are different workloads, not an optimization comparison.

Before every window, dotnet/testhost presence was checked and waited on up to 15 minutes; state preparation can occur inside that waiting period, outside measurement. Load that appeared during a window is recorded. CPU/process samples are before/after snapshots, not a continuous utilization trace; top per-process CPU deltas and RAM are retained. Other processes were never controlled.

| Window | Seconds / native minutes | Mod p50/p95/max | Whole frame p50/p95/max | Whole stalls >50/>100/>250 | CPU before/after % | Load* |
| --- | --- | --- | --- | --- | --- | --- |
| cand25-breakfast | 60.114 / 20.105 | **0.299/0.381/14.495** | 11.114/12.686/61.289 | 1/0/0 | 32.4/28.8 | yes |
| cand25-night-idle | 60.090 / 19.999 | **0.701/0.950/4.331** | 11.415/13.746/619.634 | 3/1/1 | 28.1/38.5 | no |
| cand100-breakfast | 60.415 / 20.207 | **2.537/5.683/23.995** | 20.730/27.932/60.222 | 3/0/0 | 39.5/79.2 | yes |
| cand100-afternoon-idle | 60.267 / 20.124 | **1.500/2.259/17.328** | 12.343/15.270/39.131 | 0/0/0 | 34.1/46.9 | yes |

*Load means dotnet/testhost remained at the final wait check or appeared in a before/after snapshot. The failed breakfast sample begins with no dotnet/testhost and ends with 2 dotnet / 1 testhost; CPU rises 39.5→79.2%. This does not waive the specified gate or establish causality. 10–12 agent CLI processes coexist. RAM, all counts and top process deltas are in the window evidence.

**Every ≥30 ms mod-work spike: none across all four windows.** Largest mod-work frame is 23.9954 ms, 100% breakfast, frame 61364, arrival; inclusive advancement 13.2273 ms and physical update 9.3755 ms. These timings are observations, not a root-cause diagnosis. All timing/cross-frame/event/drop/active-report error counters are zero. No autosave or manual-save activity occurred in any measured window; save inventories are unchanged and autosave is verified off before/after, including after reload. Original autosave was enabled, five minutes/three slots, restored exactly. [Validity/counters/raw frames](performance-summary.json).

The 619.634 ms whole-frame stall in the 25% night window is informational and is not a mod-work spike. Query/catch-up/runtime counters and per-frame native walking/road/event fields are retained. Production occupancy-loop counters are unavailable; cached facade counts are separate. Mono allocated-byte measurement is not established: recorder availability/positive-observation flags, GC and heap values are disclosed rather than claiming zero allocations. No short soak or extra performance rerun was taken; the combined requested work used four clean windows. No offline dotnet test suite was run.

## E2b before-red evidence

R1: **red reproduced**. Native rain cancels 346 future and 3 active breaks. All 349 retain start/end and original deadlines through clearing/reload; all are observed again at/after planned start without replay. This is the documented baseline rule, not an E2a regression. R2: **source/double-only**, no ambiguous native candidate address/controller fixture was found. R3: **not reproduced / overlap not established**: nightclub's asset enables outside pedestrians with a named pool; six loaded hangout zones and six corresponding spawners are inactive, and no active anonymous frontage-body overlap was established. Native LOD music/hangout-controller null-reference stacks also occur; causation is not established and these are not fabricated red results. R4: **source/double-only**; no waiting-customer queue was captured, so changed-weather lead and its reload reconstruction were not run natively. [Red records](red-evidence.json), [objects/flags](native-venues.json), [native-stack disclosure](log-review.json).

## Candidate venue facts

Identifiers below have prefix `ba:businesstype_`. Registered rows are established, `temporarilyClosed=false`, with one door and one controller per sampled address. This does not establish global entrance uniqueness against every non-candidate controller. Facades below are production cache successes joined to saved entrances, not new sampling by the helper and not visual clearance proof. Every uncaptured/missing/ambiguous fact remains Unknown/Unavailable; E2a enables no candidate.

| Type | Registrations / saved place / captured facade | Outside flag / pool | Cached spot counts | Example Monday hours |
| --- | --- | --- | --- | --- |
| `coffeeshop` | 28 / 28 / 25 | false / none | 8 spots ×22, 5 spots ×1, 6 spots ×1, 7 spots ×1 | 5–20 |
| `fastfoodrestaurant` | 24 / 24 / 18 | false / none | 8 spots ×17, 2 spots ×1 | 0–2, 10–24 |
| `nightclub` | 15 / 14 / 6 | true / NightclubOutsidePedestrianPool | 5 spots ×1, 8 spots ×5 | 21–24 |
| `gym` | 11 / 10 / 9 | false / none | 8 spots ×8, 7 spots ×1 | 5–24 |
| `cinema` | 4 / 4 / 4 | false / none | 8 spots ×4 | 0–2, 11–24 |
| `theater` | 4 / 4 / 3 | false / none | 8 spots ×2, 1 spots ×1 | closed |
| `lawfirm` | 14 / 14 / 9 | false / none | 5 spots ×1, 8 spots ×7, 2 spots ×1 | 8–17 |
| `school` | 1 / 1 / 1 | false / none | 8 spots ×1 | 6–20 |
| `warehouse` | 0 / 0 / 0 | false / none | unavailable | unavailable |

Warehouse has a loaded asset but no registration/work frontage in this fixture. Nightclub and gym each have one registration without a matched saved place. Named generic “bar/café/restaurant” mappings remain unavailable without their actual type IDs. Hours shown are one representative registration; all native weekly schedules, actual door instances/positions, closure and address counts are retained in [native rows](native-venues.json). [Facade/cache joins](facade-summary.json). Walking clearance, blocked doors, facing and actual actor overlap are **DEFERRED-INPUT**.

## Cleanup and evidence custody

**PASS: restored, hash-verified, lease released.** Five owned game processes quit normally; no kill was used. All **4,828/4,828 profile files and 107/107 install references** verify against backup. Original ModsLocal, config, preferences/autosave, installation receipt, run metadata and Player log are restored; preference export and root-file hashes match. No owned game/coordinator/launcher/build process remains. Lease **held=false, pending=0** at release. [Backup](backup-receipt.json), [root manifest hashes](backup-root-manifest.json), [restore](restore-receipt.json), [lease](lock-released.json), [cleanup](cleanup-summary.json).

Private backups, raw saves, full logs, payloads and lease token remain in the authorized stage; none is committed. An observer Vector3 JSON-loop error was corrected before timing; the initial process quit normally. Time-skip pause refusal and hospitalization blocked preparation only; no partial timing window was claimed or replaced. Capture/observer limitations are explicit above. No Windows/session setting, Steam/Workshop, RLO source, other worktree/process or normal game profile was changed. No merge, PR comment/approval, issue or release action was performed. Only this evidence folder is committed and pushed to `codex/pc-rig-locked-e2a`.

## Deferred to Peter

- PR items 2 and 6: visible kind matrix, facing, blocked entrances, duplicates, performer/homeless respawn, pool/camera identity continuity and before-red visual effects.
- PR item 7 frontage/overlap and PR item 8 pointer/UI actions unavailable through semantic bridge controls.
- Native opportunity gaps remain separately NOT-RUN: opening queue/audience matrix and reload, changed-weather queue lead, accepted work-break meal interruption/payment/reload. The 256-body saturation case is source-only.

`python verify_evidence.py` independently recalculates all four timing distributions/stalls/spike flags and checks accounting/reload/zero-control/cleanup receipts. It expects and verifies the recorded failed gate; verifier success is evidence integrity, not a passing rig verdict.
