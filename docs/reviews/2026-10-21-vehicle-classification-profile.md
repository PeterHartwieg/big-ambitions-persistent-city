# Next performance opportunity: live vehicle classification

21 October 2026. **Finding and proposed scope; no new production implementation or PC installation.** Two native Mac profiles of the reviewed scene-discovery candidate `782e13b18df3026496150e478d537352cb3be9d28d73fb25c3ee5d8af89cdb23` identify repeated vehicle classification as the largest measured Persistent City cost. This supersedes the earlier hypotheses that pedestrian appearance creation or long-route sampling should be the next priority.

## Measured finding

In the detailed 180.031-second capture, `PhysicalVehicles.RandomAmbient` ran **975,233 times**: approximately **5,417 calls per real second**, accumulating **40.634 seconds of main-thread wall time**. That is **225.7 ms per second**, over ten times the next largest measured Persistent City method's exclusive cost. There were 8 owned cars at capture start and 11 at the end; these are endpoint counts, not a complete occupancy trace.

`PhysicalVehicleBody.OwnershipLossReason` ran 920,639 times. Of its 38.512 seconds inclusive time, 37.604 seconds—97.6%—was in classification. Classification is also called directly by pool eligibility/selection and retirement. These overlapping parent and child totals must not be added together.

Detailed capture, ranked examples; exclusive time removes measured nested methods:

| Work | Calls | Exclusive ms per real second | Largest individual call, ms |
| --- | ---: | ---: | ---: |
| Vehicle classification | 975,233 | **225.71** | 34.69 |
| Live road-closure refresh | 4,137 | 21.35 | 11.06 |
| Route-position sampling | 466,860 | 14.11 | 0.36 |
| Physical-population update, remaining exclusive work | 4,137 | 11.97 | — |
| Incremental scene discovery | 4,137 | 9.42 | 27.97 |
| Traffic-state publication, excluding queue snapshots | 4,137 | 8.65 | — |
| Road searches | 183 | 8.09 | 44.04 |
| Queue permission snapshots | 526,815 | 3.95 | 0.62 |
| Pedestrian acquisition/appearance | 105 | 3.50 | 16.19 |
| Core physical-lease validation | 1,244,345 | 2.06 | 0.36 |
| Walking path calculation | 995 | 2.06 | 6.13 |
| Rotation constraint calculation | 87,699 | 0.29 | 0.03 |

The first, less detailed capture put the largest costs inside car command limits, body observation and native traffic physics. The second separates their descendants and explains that result. Native traffic's remaining exclusive cost fell from 131.09 to 16.64 ms/s when the ownership methods were instrumented: much of the earlier apparent native-physics cost was our own nested work. The independent runs differ in workload and instrumentation; that is attribution evidence, **not an optimization or measured speedup**.

## Why it happens

In `Runtime/PhysicalVehicles.cs`, `RandomAmbient` performs three service-component searches and a fourth enumeration of all child `MonoBehaviour` components, including inactive children. The fourth call returns a new array; its predicate repeatedly obtains managed assembly names to decide whether a component belongs to the game, Unity, ExternalPlugins or another mod.

`OwnershipLossReason` calls that classifier on each validity check. `Position`, `TryPeekReached`, `Owned`, native force/rotation interception, final command application and body observation repeatedly reach it. A cheap-looking validity or position read therefore repeats component discovery and reflection. The relevant seven production files match the frozen reviewed candidate byte for byte.

This is a large recurring cost even with a small visible car population. It supports addressing the cost of each live check before increasing body counts or adding more behavior. It does not establish memory growth or hours-long degradation.

## Recommended first implementation

Keep the existing live ownership checks and their current call boundaries. Make classification cheaper:

1. Cache the immutable allowed/foreign decision for each managed component type, preserving the exact existing assembly-name rules. A newly encountered type is classified on demand.
2. Replace temporary component arrays/LINQ enumeration with reusable non-allocating buffers, clearing retained Unity references and handling reentrancy/lifetime explicitly.
3. Combine hierarchy searches only after verifying service-component inheritance and native query equivalence. Keep service and foreign components on inactive children visible to the check.

Do **not** retain a per-vehicle ownership verdict across frames or physics steps, or remove the final ownership check after a permission callback. A mod can add a service/foreign component, replace a path or rigidbody, reparent a marker or reuse a native slot. Those changes must still be detected before Persistent City issues an owned command. The proposed type cache is independent of a vehicle's mutable component set.

This is narrower than changing the simulation scheduler or route representation. The profile measures the existing classifier as a whole; it does not separate reflection, native searches, allocation, GC or scheduling pauses inside that method. The achievable saving must be measured with an actual candidate. Removing all 225.7 ms/s is not a justified forecast.

## Acceptance checks for that change

- Run the actual old/new classifier over identical native pool hierarchies and require identical decisions. Include inactive descendants, derived service types, missing/destroyed components and foreign components added or removed after an earlier successful check.
- Add regression tests for takeover immediately before final force application, changed prefab/index/rigidbody/path/front marker, pool reuse and release/reacquire. Preserve service vehicles, native random selection, reached-corner receipts and force/rotation constraints.
- Exercise buffer reuse, nested calls and disposal without retaining Unity bodies across cities. Verify stable steady-state allocation with an allocation profiler; the present capture counts collections but does not measure allocated bytes.
- Run independent review rounds, then matched native baseline/candidate captures with identical diagnostic hooks and settings. Compare classifier time/calls, total owned work, frame distributions and physical outcomes. Follow with pause/speed/reload checks and a longer soak.

Secondary priorities are live closure refresh and route-position sampling. Sampling repeatedly walks route legs and recomputes total duration, so it remains a scaling concern. Full route planning occasionally stalls (72.16 ms maximum in the detailed capture), but its total inclusive work was only 2.038 seconds, compared with classification's 40.634 seconds. Neither walking searches nor steering math is the next high-return target in this scenario.

## Method and limits

Isolated Mac rig, background Metal, 960 × 540 low settings, 30 FPS cap, two worker threads and low priority. The same disposable `FT Road Snapshot Comparison 20261021 Checkpoint` contained 957 residents and 1,011 places; physical limits were 48 pedestrians/24 cars with a 120 m radius. Both runs used the same local installed modpack snapshot and settings, including RLO 0.45. Only the diagnostic helper DLL differed. Existing Bakery and PolyModsHyperreal asset-bundle warnings remain in both logs; this is not feature-by-feature clearance of the modpack.

Each run reloaded the checkpoint, settled paused for 45 seconds, warmed at native 1× for 45 seconds and captured for 180 seconds. The probe recorded main-thread inclusive/exclusive method wall times and per-frame arrays using preallocated timing storage. All per-frame sums match the method totals; both captures had zero probe-stack errors, overflows or measured-method exceptions. Instrumentation has overhead, and exclusive time excludes only instrumented descendants. No heavyweight build ran during capture.

Both captures retained one road snapshot, had zero routing-wait frames, ended with zero simulation backlog and reported no runtime/physical failure or traffic overshoots. No save command was issued. These are bounded observations, not exhaustive driving safety validation.

Whole-game frame intervals still contain large unexplained stalls: detailed capture median 34.79 ms, p95 83.60 ms, p99 177.38 ms, maximum 702.51 ms. The largest frame contained only about 5.7 ms of classifier time, so fixing classification will not explain or remove every hitch. There were 127 frames with observed GC counter changes; the three Mono generation counters moved together and must not be summed into 381 independent collections. GC duration, allocation volume, GPU time, Windows performance, interactive FPS and a 10,000-resident workload remain unmeasured.

## Evidence and restoration

Ignored evidence root: `build/next-performance-20261021/`. It contains both raw captures, verified analyses, profiler source/build manifests (`Probe-r1` and `Probe`), installation receipts, source-match checks, player logs, lifecycle receipts and a SHA-256 evidence manifest. Raw saves and game assemblies remain local and are not committed.

The Mac rig is stopped and its reservation released. Its original DLL `e0756b4a74764c4b04e65cf0edd9a2043cff9ccb4e34dea2942a9063c0582124`, installation manifest and preferences were restored. All **2,463 pre-existing profile files, including 920 save files**, were hash-verified; the diagnostic mod was removed. Windows was not contacted. No new production build was made or installed.
