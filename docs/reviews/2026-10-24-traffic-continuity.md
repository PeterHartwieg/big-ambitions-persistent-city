# Traffic continuity evidence — 24 October 2026

The isolated Mac FT sessions establish that a resident’s moving car retains its identity and appearance across observer departure/return and an in-flight save/reload. A separate bounded diagnostic establishes subway travel, retained parking and subsequent pickup/completion with the same owned car in the background traffic simulation. These are native observations of the frozen 0.9 traffic baseline plus the narrow admission correction described below; they do not validate all current food/lifecycle changes or deployment to an ordinary save.

## Admission defect and correction

Sessions r2–r3 exposed a nearby committed car journey that never acquired a physical body. The r4 tracked-only diagnostic (`build/continuity-baseline-20261024-r4/boundary-detail.json`) identified the exact refusal: resident `c106`, road progress 424.159830 m, ordinary native `Ferdinand112` length 4.352026 m, clearance margin 1 m. Its native route began with an explicit junction-0 exit at distance 0, followed by five correctly paired junction entries/exits. The previous predicate rejected the initial unpaired exit forever, although the vehicle was hundreds of metres beyond it.

`Core/PhysicalRoadAdmission.cs` now accepts one leading exit as evidence that the route started inside that junction, while blocking admission until the whole vehicle plus margin clears that exit. All later boundaries must pair normally. Empty/unknown/malformed topology, an inner unmatched exit, and a final unmatched entry still refuse admission. Local entry clearance and traffic permissions remain enforced. The diagnostic trace uses the same predicate. All 24 focused admission tests passed, including the exact native boundary sequence and its later junction clearances (`build/execution-20261024/persistence/leading-exit-admission.trx`).

The r5 observer teleport was refused on nonwalkable ground; that attempt is not continuity proof. The r6 source provenance records the same r5 source, with an acceptable observer location used for the repeat.

## Native moving-car continuity

The r6 snapshots below consistently identify Emil Ellis (`c106`), car `c106:car`, model `Ferdinand112`, paint seed `1401826894`, rendered paint White. Home/work assignments and the committed work journey survive the observations.

| Observation | Native minute | Physical evidence |
| --- | ---: | --- |
| `fixed-car-body.json` | 6412.462 | Valid body generation 1, lease generation 8; active Car sample, route leg 96. |
| `car-away.json` | 6412.508 | Logical resident/car and active journey persist; physical body released while observer is away. |
| `car-returned.json` | 6413.932 | Same car/model/paint; valid body generation 2, lease generation 21; route progressed to leg 100. |
| `car-inflight-restored.json` | 6414.311 | Save reload restores the same identities, parked position and active Car journey at leg 103; no body yet. |
| `reload-driving-body.json` | 6417.377 | Same car/model/paint rendered again with a valid lease; route advanced to leg 112. |

Body generation restarts at 1 after reload because native body leases are runtime resources. Stable resident/car identity and appearance are the persisted contract. Changed event times include ordinary traffic retiming; exact wall-clock progress was not asserted.

The rendered white Ferdinand screenshot was copied out of the private test profile before restoration to `build/continuity-baseline-20261024-r6/pc-c106-rendered.png`; SHA-256 `4ec8433c678007e0fb36a502544205cb7ed2696e91d5dbe16400662b34f2633b`. The capture used diagnostic camera framing and building reveal, so it is visual evidence of the native car rather than an untouched-camera performance sample. The copy did not change rig files.

## Subway, retained parking and later pickup

The 71 r6 `subway-car-*.json` captures contain 56 distinct simulation times and 125 distinct car owners whose **current sampled mode** is Subway, rather than merely a route containing a subway leg. Their stored parking remains unchanged after their first sampled subway leg. Twenty-three distinct owners are sampled driving during this window, with no observed owner crossing from the subway group to the driving group. This absence was a coverage gap, not evidence of a pickup defect. Moving logical cars peak at 23; physical bodies peak at six. Physical availability and proximity are separate from citywide mode choice, and this sample does not justify transport recalibration.

Session r10 closes the bounded logical pickup gate for Orla Sutton (`c215`) and her existing `c215:car`. The r6 samples `subway-car-0025.json` through `0030.json` show nonstationary Subway progress from fraction 0.0817 to 0.9288 while her car stays parked at `(103.37721252441406, 0.090000152587890625, -13.0537109375)`. The embedded first observation, `0024.json`, is a stationary Subway boarding/wait leg; the later moving samples independently establish travel. She subsequently reaches home, completing journey 10.

The later test uses the disposable `FT Food Routines Continuity Pickup 20261024` branch. A bounded native preflight checks three observed subway owners against up to eight actual open destinations each, one query per frame. It excludes player-owned shops, keeps each person's actual preferences and existing car, and queries only a detached person copy. Query 0 provides a native car route from Orla's actual home to the leisure entrance at 5 Sixth Street. The diagnostic explicitly selects this itinerary; it does not claim that an ordinary routine or a changed mode preference chose the destination.

Before mutation, the helper saves the complete original city JSON. Its change receipt shows only seven itinerary fields changing: phase, destination/activity, journey, departure, next event and activity deadline. Identity, assignments, preferences, completed count and every car field are unchanged. The same resident and scheduled event are reused; the helper neither creates nor moves a car or inserts traffic directly. Commit revalidates a fresh native route to the same destination, including complete car composition, pickup position, current traffic mapping and destination opening. The committed route contains 67 legs and 15.326026 nominal minutes, starting with a 0.729100-minute walk to the exact saved curb. Normal traffic ownership begins at its scheduled first road section.

| Evidence | Simulation minute | Result |
| --- | ---: | --- |
| r6 `subway-car-0025.json` / `0030.json` | 6737 / 6795 | Moving Subway samples; same car ID and parking. |
| r10 `pickup-commit.json` | 7119.488022 | Existing idle resident, journey count 10; selected itinerary committed without changing the car. |
| `pickup-first-walk.json` | 7119.576912 | Walk sample progresses toward the saved curb; parking remains unchanged. |
| `pickup-driving.json` | 7126.087730 | Nonstationary Car sample, real traffic permission for `c215`, physical control false; stored parking unchanged. |
| `pickup-visible-road.json` | 7131.489235 | Further Car progress on the same journey and car; physical control remains false despite the evidence filename. |
| `pickup-arrived.json` | 7139.569187 | AtPlace/Leisure at 5 Sixth Street, journey count 11, no remaining traffic ownership; parking commits to `(-36.64376449584961, 0.05500030517578125, 113.64629364013672)`, the route's final car endpoint. |

The destination visit is recorded at minute 7139.189678. Model `Limo` and paint seed `1563653881` are assigned for the first time during physical-admission consideration and remain stable through driving and arrival. They were blank before this first consideration; this is not evidence of an earlier rendered Limo.

**No physical car body was observed for this pickup.** The native road sample ends with a junction entry that has no matching exit in the committed road block. The existing conservative admission policy therefore refuses a physical body while the logical car continues through the normal traffic kernel. Both driving captures have `PhysicalControl=false`, no `c215` body and the explicit final-entry refusal trace. This validates background pickup and parking continuity, not a rendered pickup or parked vehicle mesh. The separately rendered c106 look-away/reload evidence above remains the physical continuity proof. No production policy was relaxed to force this diagnostic car to appear.

The preparation attempts are retained: r7 ended at controller input EOF before a pickup command; r8 returned a successful preflight with an empty top-level `error` field that collided with the controller's reserved error-key convention; r9 completed 24 queries but refused whole-route JSON equality before commit. These attempts contain no itinerary-change receipt. The r10 evidence identifies the equality issue precisely: the preflight's JSON route copy lost the transient `EstimatedMinutes` value, reducing it from 15.926026 to its 15.326026 nominal duration. The original native preflight quote and fresh native commit quote actually agree, including all geometry and timings. The corrected helper validates the fresh quote and records both representations; this is a helper-copy artifact, not observed native route instability.

Read-only collation passes 17 assertions across the captured sequence, with input hashes in `build/execution-20261024/subway-pickup-native-verification.json`. The r10 production DLL is `51c12709680f89703b8e254f1f0cbc55d6c2e62f4ab4bb0921d599ae2b2e69e2`; the diagnostic helper is `47db35ca417cbd7b0fdcf6f63a7293e48d7ed0230ac4e94160b0209339ab687d`. The native game saves `FT Food Routines Continuity Pickup Complete 20261024`; a reload of that completed checkpoint is not claimed here. Cleanup verifies 3,889 preexisting rig files, restored preferences and manifest, removed helper, stopped game and released ownership (`held=false`, `pending=0`). The normal profile was untouched.

## Exploratory frame sample, not a benchmark gate

`normal-reference-start.json` / `normal-reference-stop.json` recorded 114.144 seconds, 3,096 frames, and 37.178 game minutes with 957 residents, normal calendar factor 1/3 and time scale 1. The Mac ran 1920×1080 Low quality on Apple M1 Pro/Metal with a 30 FPS target, focused and rendering. Sampled physical peaks were 12 pedestrians and 2 cars; population lag ended at zero.

Whole-frame p50/p95/p99 were 33.503 / 40.336 / 107.649 ms; maximum 1,346.002 ms. Population-update p50/p95/p99 were 1.265 / 4.041 / 9.821 ms. GPU and allocation counters were unavailable. These are exploratory measurements of the whole running game and installed mods, not an attributable Persistent City overhead result.

The requested quiet window was approximately 90 seconds, while this capture continued for 114 seconds. Our focused test command/build began after 18:41:38 UTC; its TRX records execution **18:41:52.1192460–18:41:53.2407790 UTC** (20:41:52–20:41:53 Europe/Berlin). This overlaps or potentially overlaps the tail of the sample and invalidates it as a clean baseline/comparison gate. A controlled matched baseline/candidate sample remains required before any performance conclusion.

All native JSON, screenshots and detailed offline test evidence referenced here remain under ignored `build/` directories. No raw saves or decompiled game code are included in this report.
