# Native frame attribution, 24 October 2026

The ordinary-play performance gate remains **open**. The K1 car capture below has combined mod-work p95 **5.0086 ms** and **two frames above 50 ms**. The later corrected K2 capture also misses the target: **8.4622 ms p95**, with two frames above 50 ms; see the addendum for its comparison limits. Do not round either into a pass or claim clearance of repeated stalls. No production optimization follows from this review alone.

Evidence is retained locally under `build/ownership-k1-native-20261024-r5/`:

- `component-car-start.json`, `component-car-initial.json`, `component-car-midpoint.json`, `component-car-stop.json`.
- `probe-output/frames-mod-work-frames-stop-36bcbbb6.csv`: per-Unity-frame component correlation.
- `Player-agent.log`: surrounding runtime and native performance summaries, especially lines 2455–2522.

Candidate K1 SHA256: `a0aa250cd86524d6de3a813e977b85021444890f8e70697d9d9c52982151f566`. Instrumented helper SHA256: `5f8904883fdd304afdc3af9363dbd72eaf88b4ba2acdd04dc516f0ee8bde2548`. These are native measurements, separate from source review and compile checks. No raw saves or decompiled game code are included in this report.

## Capture and attribution

The 64.56-second capture spans 19.12 native game minutes and 1,098 completed Unity frames. A car and pedestrians were observed initially and at midpoint; the car had left by the final sample. Population lag ended at zero. All seven component hooks ran; all timing exception and cross-frame counters were zero. The earlier `component-frame-stop.json` is a late no-body exploratory capture, not the commute comparison.

Combined Update/authority work: mean 1.946 ms, p95 5.0086 ms, p99 19.8342 ms, maximum 87.4306 ms. Fifty-five frames exceeded 5 ms. Whole-frame p95 was 168.317 ms and maximum 1,237.174 ms; those larger intervals include rendering, other mods, diagnostics and scheduling, and cannot be assigned to these mod scopes.

| Inclusive component | Per-frame p95, ms | Maximum, ms |
| --- | ---: | ---: |
| PopulationRuntime.AdvancePopulation | 2.1122 | 67.0034 |
| PhysicalPopulation.Update | 1.5711 | 28.8302 |
| RouteObjectDiscovery.Step | 1.4476 | 9.3974 |
| MultimodalRoutes.Prepare | 1.0288 | 2.5745 |
| CitySimulator.RefreshTrafficClosures | 0.1080 | 3.1422 |
| PhysicalVehicles.Update | 0.0446 | 0.6101 |
| PhysicalPopulation.BeforeAdvance | 0.0172 | 0.1850 |

Do not sum component percentiles or nested timings. PhysicalVehicles.Update is already inside PhysicalPopulation.Update. The original Update/authority union counts overlapping scopes once; component hooks do not extend that union. Their timing and array allocations add diagnostic overhead. Calls outside the primary scopes were zero.

The two >50 ms frames are specifically advancement dominated:

| Unity frame | Combined, ms | Advance, ms | Physical population, ms | Discovery, ms |
| --- | ---: | ---: | ---: | ---: |
| 14975 | 87.4306 | 61.8969 | 20.2826 | 0.1719 |
| 15975 | 67.5334 | 67.0034 | 0.0787 | 0.0389 |

Among the 55 >5 ms frames, the largest measured component was physical population in 26, advancement in 22, discovery in five, preparation in one and closure refresh in one. Authority work totaled only 1.367 ms across the entire capture, including one observed hour transition. Discovery cadence and vehicle-manager updates are therefore not justified targets for fixing the >50 ms stalls.

One collection was reported in each GC generation before midpoint; no later increase occurred. The allocation recorder produced no samples. This does not attribute the collection to the mod or prove zero allocations.

## Source review and remaining uncertainty

`AdvancePopulation` processes one event per `AdvanceFrameSlice` call and checks a soft budget after that call. Reducing its event limit cannot bound a single slow event. The final `SynchronizeSnapshot` updates live physical/traffic journey progress; it does not serialize the city or deep-copy the traffic save. Full traffic capture is on the save path. Surrounding native summaries report no save serialization during the capture intervals and no road snapshot rebuilds; preparation timings exclude a graph rebuild as the measured large stall.

Citizen lookup uses a dictionary. Meal and budget work use ordered due sets; budget posting handles one resident at midnight rather than scanning every household each frame. The car capture is around noon. Food decisions allocate bounded intent, quote and lookup collections per decision, and physical selection scans/sorts potential nearby residents on its existing quarter-second cadence. Those are possible costs, not measured explanations for either >50 ms advancement call. No obvious redundant whole-city serialization or financial scan was found to remove safely.

The available component CSV does not identify the event kind, route search or snapshot substep within the two slow advancement calls. Existing walking metrics are cumulative rather than tied to these frames, so route queries **cannot** be excluded. Whole-game collection/scheduling effects also remain possible. Preserve the finding without a speculative patch or broader profiler framework; continue economic validation separately and leave the performance gate open.

## Corrected K2 capture: still above target

`build/ownership-k2-native-20261024-r2/k2-car-frame-stop.json` records 78.640 seconds, 24.578 game minutes and 1,567 completed Unity frames. One physical car was observed during the interval; it had left by the final sample. All seven hooks ran, with zero timing exceptions, zero cross-frame calls and zero final population lag. Active food decision auditing and component instrumentation were enabled.

The named CSV was copied read-only from the helper to `build/ownership-k2-native-20261024-r2/frame-evidence/frames-mod-work-frames-stop-07119a3b.csv`, SHA256 `0bd82c5a86412a61c7dcd44da0946bd46658ad527e62780401d4fdb765e407cf`.

Combined mod-work mean is 2.314 ms, p95 **8.4622 ms**, p99 19.0055 ms and maximum **65.334 ms**. **142/1,567 frames (9.06%) exceed 5 ms; two exceed 50 ms.** The absolute 5 ms target is missed. Whole-frame p95 is 114.3876 ms, maximum 769.3006 ms; these are not mod-only measures.

| Inclusive component | Per-frame p95, ms | Maximum, ms |
| --- | ---: | ---: |
| PopulationRuntime.AdvancePopulation | 3.2090 | 52.5137 |
| RouteObjectDiscovery.Step | 1.7932 | 10.8819 |
| PhysicalPopulation.Update | 1.3514 | 15.4460 |
| MultimodalRoutes.Prepare | 1.0239 | 2.8588 |
| CitySimulator.RefreshTrafficClosures | 0.0843 | 1.4605 |
| PhysicalVehicles.Update | 0.0301 | 0.1534 |
| PhysicalPopulation.BeforeAdvance | 0.0184 | 0.1828 |

Of the >5 ms frames, advancement is the largest measured component in 89, discovery in 26, physical population in 25 and preparation in two. This classification does not assign unmeasured Update time to the largest known component.

- Frame 5331: combined **55.7122 ms**, advancement **52.5137 ms**. This repeats the earlier finding of occasional large advancement calls. Nearby frames 4417 and 5174 also spend 41.6727 and 40.7471 ms in advancement.
- Frame 5612: combined **65.334 ms**, but measured nonnested top-level components plus authority total only **0.2485 ms**. Approximately **65.0855 ms remains outside those component scopes**, within the overall measurement. It is not an advancement spike. Frame 5267 similarly has 38.3561 ms outside those scopes.

The remaining Update code includes market discovery, periodic RLO/access checks, routine-hour refresh and summary logging; instrumentation/scheduling/collection can also contribute to the remainder. The remainder is not an exclusive timing measurement of any one such operation, so none is assigned responsibility here. Authority hooks total only 1.480 ms over the entire capture. Vehicle-manager Update remains small; neither scope justifies a speculative optimization. One collection in each generation was observed for the whole game, without frame-level attribution.

This is a longer window with a slightly later start than the K1 64.56-second capture, active decision auditing, different prior economic history, and the corrected whole-path quote validator. It is **not a controlled estimate of the cost of doubling consumers**. New quote acceptance and native sales establish the functional fix separately; they do not waive the performance miss. The concrete unresolved work remains large advancement calls and substantial time inside Update but outside the measured components. No production change is made from these data.
