# F5g performance: population fill and visitor transport

Refs [#92](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/92). Checks: [docs/checks/f5g.md](../checks/f5g.md).

Offline only, on this Mac (Apple M1 Pro, .NET 10.0.400, Release), 8 November 2026, at the head after the PR review round-1 fixes: working-persona commuters, closed boundaries, and the save completing an open fill. Nothing here ran in the game or on the rig. Whole-frame and native timings are not measured here and are batched for the rig (checks file, check 8).

Harnesses, both opt-in:

```
PC_F5G_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter "FullyQualifiedName~F5gMeasure|FullyQualifiedName~F5gAccountingTests.Measure"
```

`tests/F5gMeasure.cs` runs fresh factor-1 cities of the F5g test fixture. Each fills at its first day boundary (minute 2,880) and then runs to 06:00 the next day (minute 4,680), with the synchronous `CachedPortalPlanner` double. That double models Runtime's `WalkingRoutes` geometry cache: endpoint key, 1,024 entries, FIFO, negative entries kept, a copy per request.

`tests/F5gAccountingTests.cs` runs the F1 accounting scenario with the fill attached (section 4).

Persons are persisted persons: residents, commuters and the whole day-visitor pool, active or dormant. The stress row is 10,017 persisted persons, not 10,000 residents.

| Arm | Residents | Commuters | Pool | Persisted persons | Fill units |
| --- | ---: | ---: | ---: | ---: | ---: |
| reference | 957 | 999 | 576 | 2,532 | 25 |
| stress-10k | 3,339 | 4,674 | 2,004 | 10,017 | 105 |
| capacity-100k | 49,996 | 99,992 | 0 | 149,988 | 1,563 |

- **Reference target.** The commuter target is 999, not 1,000, because every band capacity is even and 80% of it cannot reach every integer.
- **Stress and capacity.** Both cities fill their whole visitor capacity.
- **Skipped numbers.** All commuters the fill creates have working personas. The capacity city skipped 34,769 sequence numbers whose identities are students or retirees (about 26%).

## 1. Creation unit (budget 1, one unit per slice)

| Arm | Units | Unit ms p50 / p95 / max | Allocated bytes per unit p50 / p95 / max |
| --- | ---: | --- | --- |
| reference | 25 | 0.83 / 5.03 / 9.89 | 326,552 / 438,808 / 545,288 |
| stress-10k | 105 | 0.20 / 4.05 / 11.12 | 327,128 / 458,896 / 1,283,816 |
| capacity-100k | 1,563 | 0.41 / 5.32 / 18.70 | 329,976 / 336,712 / 9,090,336 |

- **No unit takes 30 ms or more.**
- **Flagged: unit p95 at or above 5 ms** at the reference load (5.03 ms) and at the capacity bound (5.32 ms). This is a per-unit cost, not the windowed mod-work p95 of section 2, which stays under 5 ms. The max is the boundary's first unit, which latches the plan (one scan of residents and visitors) and builds the candidate lists. At the capacity bound that first unit also allocates 9 MB.
- **Allocation.** About 5 KB per created person: identity, routine, traits, meal record and wallet, queue entries, and the 64-entry merge list.
- **Capacity bound (review Opus-r1-3).** The units take 2.95 s together (4.86 s wall time for the loop). The merge moves up to 100,000 references per unit. The clock stands on the boundary while the units run: 1,563 frames at one unit per frame (26 s at 60 FPS), or 98 frames at 16 units per frame. This happens once, at the first fill of a city at the resident bound, and is flagged for F5h's declared-load runs. A save during it runs the remaining units inside the save callback (checks decision 2).

## 2. Full-day throughput, budgets and skip

| Arm | Budget | Slices | Events | Slice ms p50 / p95 / max | Slices ≥ 30 ms | Fill slices p95 / max | State hash |
| --- | --- | ---: | ---: | --- | ---: | --- | --- |
| reference | 1 | 55,520 | 55,520 | 0.005 / 0.022 / 9.89 | 0 | 5.03 / 9.89 | `6e317065180ef869` |
| reference | 16 | 3,470 | 55,520 | 0.116 / 0.308 / 22.78 | 0 | 22.78 / 22.78 | `6e317065180ef869` |
| reference | 128 | 434 | 55,520 | 0.445 / 3.796 / 39.08 | 1 | 39.08 / 39.08 | `6e317065180ef869` |
| reference | skip | 1 | n/a | 285.62 (one call) | 1 | n/a | `6e317065180ef869` |
| stress-10k | 1 | 145,344 | 145,344 | 0.002 / 0.005 / 11.12 | 0 | 4.05 / 11.12 | `4923be4dbeec7df3` |
| stress-10k | 16 | 9,084 | 145,344 | 0.032 / 0.067 / 15.74 | 0 | 15.74 / 15.74 | `4923be4dbeec7df3` |
| stress-10k | 128 | 1,136 | 145,344 | 0.273 / 3.312 / 46.50 | 1 | 46.50 / 46.50 | `4923be4dbeec7df3` |
| stress-10k | skip | 1 | n/a | 632.48 (one call) | 1 | n/a | `4923be4dbeec7df3` |

- **Mod-work proxy.** The slice time includes fill units, citizen and meal events and postings. p95 stays under 5 ms in every budget arm (max p95 3.80 ms).
- **Flagged ≥ 30 ms, budget 128.** One slice per shape. Condition: one `AdvanceFrameSlice(target, 128)` call that ran the whole boundary's fill. The fill units now run back to back, so 25 or 105 units land in one call (39.1 ms and 46.5 ms). Component: population fill.
  - Runtime never makes such a call. It calls `AdvanceFrameSlice(target, 1)` per step and checks its frame time budget between steps (`PersistentCityMod`), so at most one unit runs per time check.
  - Section 4 drives Runtime's loop shape and shows a frame max of 6.8 ms.
- **Flagged ≥ 30 ms, skip.** One sample per skip arm. Condition: a hidden skip that advances 48 game hours in a single `AdvanceTo` call, the fill included. Component: the whole simulation for that skip, not a frame.
- **Budget and cadence equality.** Every arm of a shape ends with the same save and order list (state hash column). `HistoricalClockMisses` is 0 in every arm.
- **Queues.** Events per day are equal across budgets. The peak person and meal queues equal the persisted persons.

## 3. Portal routes (model)

| Arm | Route requests | Portal requests | Portal hits | Portal misses | Peak requests in one game minute | Frames at 16 paths per frame |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| reference | 38,115 | 2,976 | 2,960 | 16 | 171 | 11 |
| stress-10k | 41,910 | 13,478 | 13,450 | 28 | 249 | 16 |

- **What is counted.** The figures are from the budget-1 arms. "Portal" means a request that starts or ends at a portal place.
- **Upper bound.** The double keys geometry by place pair, so its hit rate is an upper bound. Runtime keys by exact endpoint positions and also caches the station connectors.
- **Fixture.** The two portals of the fixture explain the low miss counts.
- **Admissions.** Section 4 measures them; native admissions are batched for rig check 8.

## 4. Accounting clock, admission and saves (review Astra-r1-1)

`F5gAccountingTests` runs the F1 scenario's frame loop (as `tools/accounting-backlog-regression`), but on the F5g fixture with the fill attached. It runs from 06:00 on day 1 to 06:00 on day 2, across the midnight fill:

- **Native clock.** It advances only by `AccountingClock.Limit` and is published by `BeginAccountingObservations`.
- **Market.** Every checkout must fall inside the current native hour.
- **Routes.** They go through a resumable planner double that admits at most 16 native paths per frame. A job needs one or two paths, and a Continue that finds the frame's admission spent does no work.
- **Frames.** Each frame runs `AdvanceFrameSlice(target)` steps with a time check, as Runtime does.
- **Saves.** In the save arms a save lands as the clock reaches midnight, while the boundary's fill is open; the save runs the units. Save time is excluded from frame cost.

| City | FPS / budget / ms | Save | Frames | Held | Held at the boundary minute | Fill frames | Fill units | Created | Skipped | Max lag (min) | Route jobs / paths | Deferred continues | Max paths per frame | Max frames a job waited | Frame p95 / max ms | State hash |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: | ---: | --- | --- |
| small (12 homes) | 30 / 1 / 0.01 | no | 3,771 | 2,330 | 146 | 2 | 2 | 102 | 23 | 1.00 | 582 / 942 | 0 | 2 | 0 | 0.018 / 12.55 | `205C490759E44025` |
| small | 30 / 1 / 0.01 | yes | 3,769 | 2,328 | 0 | 0 | 2 | 102 | 23 | 1.00 | 582 / 942 | 0 | 2 | 0 | 0.017 / 5.31 | `205C490759E44025` |
| small | 60 / 16 / 0.5 | no | 2,901 | 20 | 10 | 2 | 2 | 102 | 23 | 0.50 | 582 / 942 | 0 | 14 | 0 | 0.023 / 1.31 | `205C490759E44025` |
| small | 120 / 128 / 2 | no | 5,768 | 7 | 1 | 1 | 2 | 102 | 23 | 0.25 | 582 / 942 | 5 | 16 | 1 | 0.014 / 5.51 | `205C490759E44025` |
| small | 60 / 1 / 0.01, skip | yes | 3,769 | 2,328 | 0 | 0 | 2 | 102 | 23 | 1.00 | 582 / 942 | 0 | 2 | 0 | 0.012 / 0.12 | `205C490759E44025` |
| reference (152 homes) | 30 / 1 / 0.01 | no | 20,680 | 19,221 | 1,546 | 23 | 23 | 1,426 | 310 | 1.00 | 3,631 / 5,409 | 0 | 2 | 0 | 0.014 / 4.50 | `D0A3113521FF883D` |
| reference | 30 / 1 / 0.01 | yes | 20,657 | 19,198 | 0 | 0 | 23 | 1,426 | 310 | 1.00 | 3,631 / 5,409 | 0 | 2 | 0 | 0.007 / 2.79 | `D0A3113521FF883D` |
| reference | 60 / 16 / 0.5 | no | 3,598 | 716 | 102 | 8 | 23 | 1,426 | 310 | 0.50 | 3,631 / 5,409 | 0 | 16 | 0 | 0.030 / 3.35 | `D0A3113521FF883D` |
| reference | 60 / 16 / 0.5 | yes | 3,590 | 708 | 0 | 0 | 23 | 1,426 | 310 | 0.50 | 3,631 / 5,409 | 0 | 16 | 0 | 0.031 / 2.70 | `D0A3113521FF883D` |
| reference | 120 / 128 / 2 | no | 5,959 | 198 | 14 | 3 | 23 | 1,426 | 310 | 0.25 | 3,631 / 5,409 | 175 | 16 | 1 | 0.024 / 3.16 | `D0A3113521FF883D` |
| reference | 120 / 128 / 2 | yes | 5,959 | 197 | 0 | 0 | 23 | 1,426 | 310 | 0.25 | 3,631 / 5,409 | 175 | 16 | 1 | 0.025 / 2.62 | `D0A3113521FF883D` |
| reference | 60 / 1 / 0.01, skip | no | 20,680 | 19,221 | 1,546 | 23 | 23 | 1,426 | 310 | 1.00 | 3,631 / 5,409 | 0 | 2 | 0 | 0.004 / 2.69 | `D0A3113521FF883D` |
| reference | 60 / 1 / 0.01, skip | yes | 20,657 | 19,198 | 0 | 0 | 23 | 1,426 | 310 | 1.00 | 3,631 / 5,409 | 0 | 2 | 0 | 0.004 / 2.81 | `D0A3113521FF883D` |

The small city's save arms at 60/16 and 120/128 and its no-save skip arm give the same hash and are omitted.

- **Identical results.** Every arm of a city has the same save and order list. `HistoricalClockMisses` is 0, and no checkout fell outside its native hour.
- **Lag.** Maximum lag stays inside `AccountingClock.MaximumLagMinutes` (1.00 minute at 30 FPS, 0.25 at 120 FPS).
- **Admission.** Paths never exceed 16 per frame. At 120 FPS with 128 events per frame, 175 Continues found the frame's admission spent, and each of those jobs waited one frame.
- **Held clock.** At the reference size the native clock was held 102 frames on the midnight minute at 60 FPS: 1.7 s of real time with the native clock standing still. That covers the 23 fill units and that minute's postings. At one event per frame, it is 1,546 frames.
- **Frame cost.** No frame reached 30 ms.
- **Saves.** In every save arm the save ran the fill's units, so the reloaded run advanced none itself (fill frames 0), and the result is unchanged.

## Changes with a cost and their numbers

- **`BudgetQueue` ties.** Ties now compare the `ResidentId` strings instead of the rank they had at build. It is the same order, and it lets a person enrolled later take its place without renumbering. It costs a string comparison on equal posting minutes. The slice numbers include it; a separate before/after for postings was not taken.
- **Visitor slot check.** `CurrentCitizenSlot` is now a binary search by ID instead of an index read. It runs only on meal-outing, optional-visit and lease checks, so it is O(log visitors) on those calls.
- **Skipped commuter numbers.** About a quarter of the numbers the fill reaches for a commuter are students or retirees and are skipped. Each skip costs one identity draw (no strings). On read, each skipped number costs one draw: 34,769 at the capacity bound.
- **Scans and allocation.** There is no new per-frame scan or allocation. The plan scans residents and visitors once per boundary. A fill unit costs O(visitors) moves for the ordered merge, plus the created persons.

## Not measured here

- Whole-frame timings and the native `NavMesh.CalculatePath` cost of portal routes (rig check 8).
- The native walking-cache hit rate for portal legs (rig check 8).
- The save-callback time when a save lands during a capacity-bound first fill. The units it runs are those of section 1, about 2.95 s in total at most.
