# F9g perf: presentation reads through the view, 10 November 2026

Offline component timing on the development PC (20 logical cores), .NET 10 Release test build. The machine was shared with other agent sessions' test runs, so single-frame maxima are noisy. These are not in-game mod-work numbers. Mod-work p95 < 5 ms, every mod-work spike of 30 ms or more and whole-frame timing come from the batched rig ([checks](../checks/f9g.md#in-game-checks), check 4). #128 stays a separate investigation. The four parked hotspots stay parked; F9g touches none of them.

## Method

One scratch harness, not committed, run on base `649467b` (a detached worktree) and on this branch. It builds the same fixtures as the existing opt-in perf tests:

- **48 walkers:** `PhysicalWalkerPerfTests.MeasureUpdateWithFortyEightWalkers`' city. 48 residents walk to work inside the radius, a native-movement double steps each body, and the city advances 1/180 game minute per 1/60 s frame. 120 warm-up frames, then 2,400 timed `Update` calls.
- **24 cars:** `PhysicalCarPerfTests.MeasureUpdateKernelWorkAndHarnessWithTwentyFourCars`' city, with the traffic kernel and the car-driving double. Warm-up until 24 cars are bound, then 1,800 timed `Update` calls.

Each call is timed with `Stopwatch` around `PhysicalPopulation.Update` only (the city advance is outside the timed span). Allocation is `GC.GetAllocatedBytesForCurrentThread()` around the same call. Each build ran three times in one process.

## Before and after

p50 / p95 per `Update` call, three runs:

| Arm | Base `649467b` p50 | Base p95 | F9g p50 | F9g p95 | ≥ 30 ms (both) |
| --- | --- | --- | --- | --- | ---: |
| 48 walkers | 0.071 / 0.088 / 0.090 ms | 0.122 / 0.121 / 0.125 ms | 0.065 / 0.060 / 0.065 ms | 0.111 / 0.109 / 0.115 ms | 0 |
| 24 cars | 0.082 / 0.088 / 0.070 ms | 0.153 / 0.155 / 0.134 ms | 0.078 / 0.087 / 0.037 ms | 0.136 / 0.156 / 0.068 ms | 0 |

Maxima: base 0.89–2.68 ms walkers, 0.90–13.3 ms cars; F9g 1.75–5.91 ms walkers, 1.77–5.48 ms cars. They move between runs on both builds (JIT, GC, other sessions' load) and are not a trend.

Allocated bytes per `Update` call, mean over the timed calls:

| Arm | Base `649467b` | F9g |
| --- | ---: | ---: |
| 48 walkers | 131.8 / 134.9 / 131.4 B | 131.7 / 134.8 / 131.4 B |
| 24 cars | 1,166.9 / 1,167.6 / 1,165.0 B | 1,167.3 / 1,168.0 / 1,165.4 B |

- **Time:** neutral within run-to-run noise at 48 walkers and 24 cars, well under the 5 ms gate. No sample at or above 30 ms on either build.
- **Allocation:** the same on both builds; the differences are under 1 B per call and change sign between runs. The view reads add no per-frame allocation:
  - `View.Minute` and `View.ResidentList` are interface reads of the simulator's one cached `CityView`.
  - `Query` fills the reused `List<IPersonView>` buffer.
  - The roster loops index the `IReadOnlyList` and do not use `foreach`, so no enumerator is boxed.
  - `Entity()` is a cast.
  - The remaining bytes are the base code's own (car admission, LINQ in the queue plan) and are unchanged.
- The retry-table prune (`ResidentList.Concat(VisitorRoutineList)`) allocates like the `PersonTable.Schedules` iterator it replaces. It runs only when the table outgrows the population by 128, not per frame.

No cheap wins were taken: nothing in the changed paths shows as a hotspot.
