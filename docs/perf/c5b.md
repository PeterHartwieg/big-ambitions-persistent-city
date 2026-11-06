# C5b: survey runner and hold costs (6 November 2026, offline)

Plan: [C5 section 4, row C5b](../plans/c5-agency-research.md#c5b--survey-runner-and-time-hold) and section 6. Checks: [docs/checks/c5b.md](../checks/c5b.md). C5b has no caller in the game build, so it adds no mod-work per frame in game today. These are offline unit and frame costs for C5e and the batched rig.

## Method

Test `ResearchSurveyTests.Perf`, run with `PC_C5B_PERF=1 dotnet test tests/PersistentCity.Tests.csproj --filter "FullyQualifiedName~ResearchSurveyTests.Perf" --logger "console;verbosity=detailed"`. Debug build, net10.0, Windows 11, one foreground process after the shared other-dotnet wait. Timing with `Stopwatch`, allocation with `GC.GetAllocatedBytesForCurrentThread`.

- **Index alone:** a synthetic roster of scrambled anchor and represented consumer IDs, 5 complete bounded builds per size. The single synchronous sort is timed 20 times per size in two variants: with a copied key array (`Array.Sort(keys, order, StringComparer.Ordinal)`) and reading `ResidentId` from the roster in the comparer.
- **Survey in a city:** `EconomyScenario` city with representation factor 2 (957, 5,000 and 50,000 residents for 1,914, 10,000 and 100,000 consumers), household finances on, unit size 256. Two surveys in a row on the same runner: `detailed-city`, then `detailed-local` (500 m).
- **Hold frames:** each frame is driven as `PersistentCityMod.AdvancePopulation` drives it at 60 FPS: `PopulationFrameBudget(1/60)` gives 128 units and 2.0 ms. One-unit `AdvanceFrameSlice` calls run until the unit cap, the 2 ms allowance, the citizen-event limit (research units do not spend it) or catch-up. Frame work is the whole loop for that frame; unit timings are reported separately.
- **Idle:** 20,000 `IsCaughtUpTo` calls with the runner attached and no open order.

## Results

Per-unit costs (ms):

| Consumers | Index unit, synthetic (p50 / p95 / max) | Index unit, in city | Engine unit (256) in city |
| ---: | --- | --- | --- |
| 1,914 | 0.070 / 0.086 / 0.389 | 0.030 / 0.927 / 0.927 | 0.230 / 3.230 / 3.230 |
| 10,000 | 0.082 / 0.102 / 0.177 | 0.055 / 0.079 / 0.095 | 0.104 / 0.118 / 0.204 |
| 100,000 | 0.190 / 0.351 / 0.690 | 0.088 / 0.322 / 0.457 | 0.174 / 0.320 / 0.459 |

The 1,914 in-city maxima (0.93 ms index, 3.23 ms engine) are the first units the test process ran through those code paths (JIT). The same paths at 10,000 and 100,000, run later in the process, stay under 0.5 ms. No unit is near 30 ms.

Hold at 60 FPS (`PopulationFrameBudget` 128 units, 2.0 ms):

| Consumers | Survey | Units | Frames held | Frame work p50 / p95 / max (ms) | Allocation |
| ---: | --- | ---: | ---: | --- | ---: |
| 1,914 | detailed-city | 14 | 2 | 2.164 / 4.468 / 4.468 (both surveys, n = 4) | 36 KiB |
| 1,914 | detailed-local | 14 | 2 | | 5 KiB |
| 10,000 | detailed-city | 77 | 4 | 2.046 / 2.101 / 2.101 (n = 8) | 163 KiB |
| 10,000 | detailed-local | 77 | 4 | | 7 KiB |
| 100,000 | detailed-city | 831 | 41 | 2.065 / 2.268 / 2.396 (n = 120) | 1,585 KiB |
| 100,000 | detailed-local | 831 | 79 | | 22 KiB |

Frames stop at the 2 ms allowance, so frame work sits just above 2.0 ms: the last unit of a frame finishes after the allowance is reached. The 1,914 frame maximum of 4.47 ms is the JIT unit above. At 100,000 consumers the hold lasts 41 to 79 frames, about 0.7 to 1.3 s at 60 FPS. The catchment survey takes more frames for the same unit count because each catchment row also computes two distances; frames are bounded by time, not units. All frame work is under the 5 ms gate and no frame is near 30 ms.

Idle: `IsCaughtUpTo` with the runner attached and no order is p50 0.000 / p95 0.000 / max 0.051 ms at every size (the max is a timer outlier). With no order the extra work is one sequence compare and one null check.

Single sort compared with the bounded index:

| Consumers | Single sort, key array (p50 / p95 / max ms) | Single sort, roster reads |
| ---: | --- | --- |
| 1,914 | 0.232 / 0.257 / 0.398 | 0.340 / 0.427 / 0.434 |
| 10,000 | 1.608 / 1.973 / 2.472 | 2.474 / 3.664 / 3.668 |
| 100,000 | 31.810 / 46.557 / **60.794** | 61.414 / 95.462 / **108.453** |

**Spike flags (comparison code only, not shipped).** At 100,000 consumers the single synchronous sort is a single nonpreemptible call of 31.8 ms p50 and 60.8 ms max with keys copied, and 61.4 ms p50 and 108.5 ms max reading the roster. Both are mod-work spikes of 30 ms or more in that variant. A reviewer's run of the same comparison saw a 68.3 ms maximum. The single sort's p95 at 100,000 (46.6 ms) is far above the plan's 1 ms rule, so the bounded index is kept. Its units stay under 0.7 ms at every size.

## Perf wins taken

1. **Key array for the ordinal comparisons.** The index copies each `ResidentId` into a `string[]` during its bounded fill units and compares keys there, instead of reading `roster[i].ResidentId` through the list in every comparison. Measured on the same sort shape at 100,000 consumers: p50 61.4 → 31.8 ms, p95 95.5 → 46.6 ms (about 2× less comparison work). At 10,000: p50 2.47 → 1.61 ms.
2. **Index buffers reused between surveys.** The runner keeps its two `int[]` and one `string[]` buffers while they are large enough. Allocation for the second survey on the same runner: 1,585 → 22 KiB at 100,000 consumers, 163 → 7 KiB at 10,000, 36 → 5 KiB at 1,914 (first survey → second survey).
3. **Bulk copy of a finished run.** When one run of a merge is exhausted, the rest of the other run is moved with one bounded `Array.Copy` instead of element by element. This was written before the first measurement, so there is no separate before number.

The four parked hotspots (physical population, closure refresh, optional-stop refresh, save serialization) are not touched.
