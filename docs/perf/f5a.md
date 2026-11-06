# City audit F5a — world census performance, 4 November 2026, offline

## Roadmap perf-log entry (moved 6 Nov 2026)

- City-audit F5a — [Pre-sized census dictionaries](f5a.md): census allocation 126,408 -> 104,000 B/call (reference shape) and 2,288,064 -> 1,817,576 B/call (20,000 places / 50,000 residents), timing within noise; on-request diagnostic, offline only.

Refs #92. Measurement named by the F5 plan's F5a row and section 6: census build time and allocation at the reference place count and at 20,000 places; it must stay off ordinary frames.

## Where the cost lands

- **Ordinary frames: zero new work.** The census runs only when the debug-console line `pc population census` is executed. There is no `Update` hook, polling, reflection, timer or cache refresh.
- **Request frame: capture only.** The main thread copies three fields per resident and the discovered station IDs (DLC-filtered on request). The reduction, sorting and text building run on a detached worker; the next request publishes the finished result for the same save.
- **Load:** `TryStart` also keeps a field copy (ID, kind, capacity, area) of the fresh place catalogue it already reads, about 1,059 small objects at the reference size.
- **Load time:** the existing `GamePlaces.Read` loop also records one small `CensusWorkplace` per established business (one dictionary lookup in the already-built catalogue area map, three field reads). This is measured offline only as part of `tools/food-hours-regression`; no in-game load timing exists yet.
- **Parked hotspots:** physical population, closure refresh, optional-stop refresh and save serialization are untouched.

## Offline measurement

`dotnet run --project tools/population-census-regression/PopulationCensusRegression.csproj --configuration Release`, Release build, Windows desktop, 20 warm-up calls, then 200 timed `Compute(...).Summary()` calls per shape (`Stopwatch`, `GC.GetAllocatedBytesForCurrentThread`). Synthetic catalogues: "reference shape" uses the rig city's recorded 1,059 places / 335 homes / 383 work roles / 957 residents with 20 stations; "places-20000" uses 20,000 places / 6,330 homes / 7,240 work roles / 50,000 residents / 256 stations. Two consecutive runs of the initial implementation:

| Shape | p50 ms | p95 ms | max ms | Allocated B/call (median / max) |
| --- | ---: | ---: | ---: | ---: |
| reference shape, run 1 | 0.229 | 0.267 | 1.219 | 104,000 / 106,072 |
| reference shape, run 2 | 0.234 | 0.369 | 1.231 | 104,000 / 106,072 |
| 20,000 places / 50,000 residents, run 1 | 6.473 | 8.828 | 18.614 | 1,817,576 / 1,825,320 |
| 20,000 places / 50,000 residents, run 2 | 6.374 | 8.368 | 18.710 | 1,817,576 / 1,825,736 |

No call reached 30 ms. These rows are the pre-review measurement of the reduction, which then ran on the Unity thread; after the review fix it runs on a detached worker (numbers below). The rig's per-request `capture_ms=` and `detached_compute_ms=` readings (check 5) are the native counterparts.

### Main-thread capture (review fix, same run)

`PopulationCensusInput.CaptureResidents` over the fixture's `CitizenState` list, 20 warm-up + 200 timed calls:

| Shape | p50 ms | p95 ms | max ms | Allocated B/call |
| --- | ---: | ---: | ---: | ---: |
| reference shape (957 residents), runs 1/2 | 0.021 / 0.021 | 0.023 / 0.023 | 0.031 / 0.031 | 22,992 |
| 50,000 residents, runs 1/2 | 0.562 / 0.739 | 0.990 / 1.382 | 2.482 / 2.338 | 1,200,024 |

The detached reduction after this change: reference p95 0.268 / 0.278 ms, max 1.340 / 1.345 ms, 104,232 B; stress p95 7.173 / 7.644 ms, max 18.444 / 17.871 ms, 1,819,928 B. That cost is now off the Unity thread; only the capture row above lands in the request frame.

## Win taken in this slice

**Pre-sized census dictionaries.** Occupancy and assignment dictionaries are sized from the home and day-place counts found in the first pass, instead of growing from empty. Matched fixture, same code otherwise:

| Shape | Before (median B/call) | After (median B/call) | p50 ms before → after |
| --- | ---: | ---: | --- |
| reference shape | 126,408 | 104,000 | 0.248 → 0.229–0.234 |
| 20,000 places / 50,000 residents | 2,288,064 | 1,817,576 | 6.740 → 6.374–6.473 |

The timing change is within run-to-run noise; the allocation change is deterministic. An intermediate variant sized both dictionaries by `min(places, residents)` was rejected: it allocated more (143,816 / 2,562,768 B).

## Limits

Offline synthetic catalogues on a desktop CPU, not game execution. Per-business areas, stations and assignments are synthetic. In-game mod-work p95, spike flags and whole-frame metrics come from the next batched rig run ([checks](../checks/f5a.md#in-game-checks-for-the-next-batched-rig)).
