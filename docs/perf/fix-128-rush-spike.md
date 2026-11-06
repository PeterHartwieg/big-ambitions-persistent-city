# Fix #128 rush spike — performance, 6 November 2026, offline

Refs #128. Base `f46b3dd`, writer 25. Status and rig checks: [checks file](../checks/fix-128-rush-spike.md).

## Replay

`tools/rush-spike-replay` (and `tests/RushSpikeReplayTests.cs`, which shares the fixture `tests/RushSpikeReplay.cs`) builds a matched city: 957 residents, 1,914 consumers (representation factor 2), 1,058 places (331 homes, 383 workplaces, 220 shops, 80 leisure venues, 20 `scene-park:` places, 24 schools), household finances, three food scopes, optional stops at the approved 25 %, and persistent car traffic on a 24×24 road grid (2,208 directed sections). It warms up without slicing to the window start, then advances frame by frame with the `AdvancePopulation` loop shape: `PopulationFrameBudget(1/30 s)` caps (256 units, 32 citizen events, 4 ms soft budget), `RefreshOptionalStops` first and `SynchronizeSnapshot` last. The clock moves 1/98.6 game minute per frame, the rig's measured pace (17,753 frames over 3 game hours). Every `AdvanceFrameSlice` call is timed as one unit.

The planner, market and stop environment are detached doubles. They count the calls each unit makes into the native seams but cannot reproduce native or Mono cost. Host: Windows 11, .NET SDK 10.0.402, Release, busy machine (other agents' dotnet loads ran between runs).

Run: `dotnet run -c Release --project tools/rush-spike-replay`. Before: the same command with `-p:SourceRoot=<extracted f46b3dd Core/Persistence>` (separate `BaseOutputPath`/`BaseIntermediateOutputPath`).

## Before / after (advancement interval per frame)

This slice changes no Core or Persistence code, so the two rows of each window run identical Core. The difference between them is run-to-run noise. They are kept as the matched baseline the task asked for.

| Window (day 3) | Build | Frames | p50 ms | p95 ms | p99 ms | Max frame ms | Frames ≥30 ms | Slowest unit ms (kind) | Save SHA-256 |
|---|---|---:|---:|---:|---:|---:|---:|---|---|
| Evening 17:30–20:30 | before `f46b3dd` | 17,749 | 0.0389 | 0.1205 | 0.5824 | 11.5101 | 0 | 2.0867 (citizen) | `029989F8…3615` |
| Evening 17:30–20:30 | after | 17,749 | 0.0397 | 0.1341 | 0.6486 | 12.2861 | 0 | 1.9270 (citizen) | `029989F8…3615` |
| Morning 07:00–09:30 | before `f46b3dd` | 14,790 | 0.0248 | 0.0663 | 0.3093 | 11.6568 | 0 | 2.4040 (food continuation) | `7C8FE1F8…D669` |
| Morning 07:00–09:30 | after | 14,790 | 0.0132 | 0.0457 | 0.2637 | 16.9405 | 0 | 4.3790 (citizen) | `7C8FE1F8…D669` |

Full hashes: evening `029989F8CA1E7AD37F632717F094ECFC07E35D1908AA232F506D254639E54076`, morning `7C8FE1F83163B843F7C8CF89C7DFFBB8C8B9DC12B6BC97FB40F108B5F52CD669`. In every run the sliced replay's save equals an uninterrupted `AdvanceTo` of the same city.

Evening workload (identical in all runs): 22,759 units, 8,933 citizen events, 9,119 traffic events, at most 83 units in one frame, peak 376 residents travelling and 108 cars, 2,709 purchases, 907 optional stops, 262,341 stop-environment calls (most from `RefreshOptionalStops`), at most 3 stop-environment calls and 2 route calls in one unit.

Reading:
- No Core unit comes near 30 ms. The slowest unit in any run is 4.38 ms, and three of the four runs stay at or below 2.40 ms.
- The 11–17 ms frame maxima are neither unit cost nor snapshot cost (snapshot ≤0.69 ms). They match one Windows scheduler quantum on a loaded host.
- One full-suite run with parallel tests produced a 60.58 ms frame whose units were all under 30 ms. That time sat outside every unit: in-process GC suspension or CPU contention from the other tests. The test now runs in a non-parallel xUnit collection and reports the GC count of its slowest frame.

So the rig's 134.96 ms advance is not reproduced by Core managed work at 957 residents. The remaining candidates are native or runtime work reached from a unit: native route-planning slices, optional-stop native reads (`OptionalStopEnvironment`), and a Mono garbage collection landing inside the call. The new summary fields attribute each of these.

## Taken low-risk win

- **`OptionalStopEnvironment.ParkEdge`: whole-scene `FindObjectsOfType<LocationHappinessTrigger>(true)` on every uncached call → one scan per change of the loaded-scene set.** Before, this scan ran inside an advancement unit whenever a leisure arrival at a `scene-park:` place tried a resting stop and the park had no cached spots. A failed lookup is not cached, so the scan repeated on every such arrival, which is the evening leisure peak. Matching requires a scene-authored, non-clone trigger, so the set can only change when a scene loads, finishes loading or unloads. The signature is each loaded scene's handle and `isLoaded` state. Per-call null, activity and live scene-key checks run unchanged and in the same order. Before/after cost: not measurable offline, because it needs Unity. Rig check 3 reads `park trigger scans` and optional-stop read time. No allocation is added per call; the signature list reallocates only on a scene change.

## Not taken

- Hoisting the arriving resident's own spot plan out of `TryConversation`'s partner loop was prototyped, then reverted. It is behaviour-identical, but the replay never reaches that multiplicity: total stop-environment calls were equal before and after. No evidence, no change.
- No change to the frame budget, unit caps or event order. The replay shows they already bound Core work, and changing them would change frame-cadence outcomes.

## Added diagnostic cost

Per advancement call: one `GC.CollectionCount(0)` read at start, one at end only for a new observation maximum, one extra subtraction per unit, and a `Stopwatch.GetTimestamp` pair per optional-stop read. No allocation per frame. Strings are built only in the existing 60-second summary.
