# F5g performance: population fill and visitor transport

Refs [#92](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/92). Checks: [docs/checks/f5g.md](../checks/f5g.md).

Offline only, on this Mac (Apple M1 Pro, .NET 10.0.400, Release), 8 November 2026, at the head after the PR review round-3 fixes. The fill runs in units of at most 64 persons at the boundary minute; each unit creates, merges, queues, seats and enrols its persons. One boundary creates at most 1,024 persons, and the save does no fill work. Nothing here ran in the game or on the rig. Whole-frame and native timings are not measured here and are batched for the rig (checks file, check 8).

Harnesses, all opt-in. Each fact stays under the 10-minute foreground limit:

```
PC_F5G_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter "FullyQualifiedName~F5gMeasure.MeasureReference|FullyQualifiedName~F5gMeasure.MeasureStress|FullyQualifiedName~F5gAccountingTests.Measure"
PC_F5G_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter "FullyQualifiedName~F5gMeasure.MeasureCapacity"
```

`F5gMeasure` uses fresh factor-1 cities of the F5g test fixture with the synchronous `CachedPortalPlanner` double. That double models Runtime's `WalkingRoutes` geometry cache: endpoint key, 1,024 entries, FIFO, negative entries kept, a copy per request.

Each city crosses day boundaries until one creates fewer than 1,024 persons, which means it reached its targets. The day before each boundary is advanced in one call; the boundary itself goes slice by slice, so every fill unit is timed. The measured day then runs from that boundary to the next one at the full persisted population.

`F5gAccountingTests` runs the F1 accounting scenario with the fill attached (section 4).

Run conditions:

- **Load.** The machine was shared, with a load average of about 7 on 10 cores.
- **Reruns.** The stress and accounting numbers below are from a second run. In the first run, the same cities, with identical state hashes, had these isolated spikes, which did not repeat:
  - one stress unit of 56.3 ms;
  - four accounting frames of 30–74 ms in the no-save reference arms;
  - one in the no-save small arm.

  They are flagged, and their condition is CPU contention.

Persons are persisted persons: residents, commuters and the whole day-visitor pool, active or dormant. The stress row is 10,017 persisted persons, not 10,000 residents.

| Arm | Residents | Commuters | Pool | Persisted persons | Boundaries to fill | Fill units | Skipped numbers |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| reference | 957 | 999 | 576 | 2,532 | 2 | 25 | 369 |
| stress-10k | 3,339 | 4,674 | 2,004 | 10,017 | 7 | 105 | 1,700 |
| capacity, one boundary | 49,996 | 49,996 seeded + 1,024 | 0 | 101,016 after it | 49 to the 99,992 capacity | 16 | 361 |

- **Reference target.** The commuter target is 999, not 1,000, because every band capacity is even and 80% of it cannot reach every integer.
- **Skipped numbers.** All fill-created commuters have working personas (owner decision). Skipped numbers are numbers whose identity is a student or retiree.
- **Capacity arm.** The city is seeded at factor 2, and catalogue-only businesses keep commuter demand above the capacity. Only its first fill boundary is measured: 16 units, each merging its 64 commuters into about 50,000 visitors. The remaining 48,972 visitors need 48 more boundaries.

## 1. Fill units (budget 1, one unit per slice)

Each unit creates, merges, queues, seats and enrols at most 64 persons (review Sol-r3-2).

| Arm | Unit ms p50 / p95 / max | Unit allocated bytes p50 / p95 / max |
| --- | --- | --- |
| reference | 0.18 / 0.27 / 5.20 | 326,552 / 438,808 / 544,832 |
| stress-10k | 0.21 / 4.39 / 6.67 | 327,128 / 510,224 / 1,283,816 |
| capacity, one boundary | 0.47 / 6.92 / 6.92 | 329,976 / 784,704 / 784,704 |

- **No unit takes 30 ms or more** in these runs (see run conditions above for the flagged first-run spike).
- **Flagged ≥ 5 ms** (single units, not the windowed p95): each arm's boundary-first unit, which latches the plan with one scan of residents, visitors and wallets and builds the candidate lists. Those are 5.2 ms, 6.7 ms and 6.9 ms.
- **Allocation.** About 5 KB per created person: identity, routine, traits, meal record and wallet, queue entries and the merge list.
- **Held clock.** It stands on a boundary for at most 16 units, because a boundary creates at most 1,024 persons. The first fill of a city at the resident bound therefore takes 49 boundaries instead of the 1,563 back-to-back units of the round-1 design.
- **Saves.** The save path does no fill work, so a save during the fill costs nothing extra.

## 2. Full-day throughput at the full population, budgets and skip

The measured day runs from the boundary that finished the fill (4,321 for reference, 11,521 for stress) to the next boundary.

| Arm | Budget | Slices | Events | Slice ms p50 / p95 / max | Slices ≥ 30 ms | State hash |
| --- | --- | ---: | ---: | --- | ---: | --- |
| reference | 1 | 38,493 | 38,493 | 0.001 / 0.005 / 7.07 | 0 | `f48f48e269ada1bf` |
| reference | 16 | 2,406 | 38,493 | 0.029 / 0.044 / 22.39 | 0 | `f48f48e269ada1bf` |
| reference | 128 | 301 | 38,493 | 0.226 / 2.136 / 6.11 | 0 | `f48f48e269ada1bf` |
| reference | skip | 1 | n/a | 142.23 (one call) | 1 | `f48f48e269ada1bf` |
| stress-10k | 1 | 92,567 | 92,567 | 0.002 / 0.005 / 9.89 | 0 | `0075526aa2b9808b` |
| stress-10k | 16 | 5,786 | 92,567 | 0.034 / 0.070 / 9.74 | 0 | `0075526aa2b9808b` |
| stress-10k | 128 | 724 | 92,567 | 0.269 / 2.410 / 9.15 | 0 | `0075526aa2b9808b` |
| stress-10k | skip | 1 | n/a | 422.43 (one call) | 1 | `0075526aa2b9808b` |

- **Mod-work proxy.** The slice time, which includes citizen and meal events, postings and any fill unit. p95 stays under 5 ms in every budget arm (max p95 2.41 ms).
- **Flagged ≥ 30 ms.** One sample per skip arm. Condition: a hidden skip that advances the whole measured day in a single `AdvanceTo` call. Component: the whole simulation for that skip, not a frame.
- **Budget and cadence equality.** Every arm of a shape ends with the same save and order list (state hash column), the fill days included. `HistoricalClockMisses` is 0 in every arm.
- **Queues.** Events per day are equal across budgets. The peak person and meal queues equal the persisted persons.

## 3. Portal routes (model)

| Arm | Route requests | Portal requests | Portal hits | Portal misses | Peak requests in one game minute | Frames at 16 paths per frame |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| reference | 56,120 | 5,590 | 5,558 | 32 | 188 | 12 |
| stress-10k | 121,385 | 32,706 | 32,538 | 168 | 239 | 15 |

- **What is counted.** The counts cover the whole run (fill days and the measured day) of the budget-1 arms. "Portal" means a request that starts or ends at a portal place.
- **Upper bound.** The double keys geometry by place pair, so its hit rate is an upper bound. Runtime keys by exact endpoint positions and also caches the station connectors. The fixture has two portals.
- **Admissions.** Section 4 measures them.

## 4. Accounting clock, admission and saves (reviews Astra-r1-1, Sol-r2-2, Sol-r3-1)

`F5gAccountingTests` runs the F1 scenario's frame loop, as in `tools/accounting-backlog-regression`, on the F5g fixture with the fill attached. It runs from 06:00 on day 1 to 06:00 on day 2, across the midnight fill:

- **Native clock.** It advances only by `AccountingClock.Limit`. `BeginAccountingObservations` publishes each epoch, and at midnight it moves the clock onto the boundary, where the fill then runs.
- **Market.** Every checkout must fall inside the current native hour.
- **Routes.** They go through a resumable planner double that admits at most 16 native paths per frame.
- **Frames.** Each frame runs `AdvanceFrameSlice(target)` steps with a time check, as Runtime does.
- **Saves.** In the save arms there is one save as the clock reaches midnight and one between two of the fill's units. Save time is excluded from frame cost.

| City | FPS / budget / ms | Saves | Frames | Held | Held on the boundary with fill work | Fill units | Created | Max lag (min) | Jobs / paths | Deferred continues | Max paths per frame | Frame p95 / max ms | State hash |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: | --- | --- |
| small (12 homes) | 30 / 1 / 0.01 | 0 | 3,771 | 2,330 | 1 | 2 | 102 | 1.00 | 582 / 942 | 0 | 2 | 0.017 / 24.63 | `205C490759E44025` |
| small | 30 / 1 / 0.01 | 2 | 3,771 | 2,330 | 1 | 2 | 102 | 1.00 | 582 / 942 | 0 | 2 | 0.017 / 4.63 | `205C490759E44025` |
| small | 120 / 128 / 2 | 1 | 5,771 | 10 | 0 | 2 | 102 | 0.25 | 582 / 942 | 5 | 16 | 0.012 / 2.06 | `205C490759E44025` |
| reference (152 homes) | 30 / 1 / 0.01 | 0 | 20,254 | 18,795 | 15 | 16 | 1,024 | 1.00 | 3,631 / 5,409 | 0 | 2 | 0.022 / 3.10 | `6A29C7B50B9A7A42` |
| reference | 30 / 1 / 0.01 | 2 | 20,254 | 18,795 | 15 | 16 | 1,024 | 1.00 | 3,631 / 5,409 | 0 | 2 | 0.020 / 10.11 | `6A29C7B50B9A7A42` |
| reference | 60 / 16 / 0.5 | 0 | 3,598 | 716 | 6 | 16 | 1,024 | 0.50 | 3,631 / 5,409 | 0 | 16 | 0.124 / 2.81 | `6A29C7B50B9A7A42` |
| reference | 60 / 16 / 0.5 | 2 | 3,582 | 700 | 6 | 16 | 1,024 | 0.50 | 3,631 / 5,409 | 0 | 16 | 0.102 / 4.48 | `6A29C7B50B9A7A42` |
| reference | 120 / 128 / 2 | 0 | 5,970 | 209 | 2 | 16 | 1,024 | 0.25 | 3,631 / 5,409 | 174 | 16 | 0.052 / 7.05 | `6A29C7B50B9A7A42` |
| reference | 120 / 128 / 2 | 2 | 5,962 | 201 | 2 | 16 | 1,024 | 0.25 | 3,631 / 5,409 | 175 | 16 | 0.033 / 9.36 | `6A29C7B50B9A7A42` |
| reference | 60 / 1 / 0.01, skip | 2 | 20,254 | 18,795 | 15 | 16 | 1,024 | 1.00 | 3,631 / 5,409 | 0 | 2 | 0.005 / 6.62 | `6A29C7B50B9A7A42` |

The held, frame, unit and created counts are from the first run; frame p95 and max are from the second. The small city's 60/16 arms and the remaining skip arms give the same hashes and are omitted.

- **Identical results.** Every arm of a city has the same save and order list. `HistoricalClockMisses` is 0, and no checkout fell outside its native hour.
- **Lag.** Maximum lag stays inside `AccountingClock.MaximumLagMinutes`.
- **Admission.** Paths never exceed 16 per frame. At 120 FPS with 128 events per frame, about 175 Continues found the frame's admission spent, and each of those jobs waited one frame.
- **Saves.** A save between units makes the reload continue with the remaining units, and the unit and created counts equal the unsaved arms. No frame reached 30 ms in the second run.
- **Cap in this run.** The reference accounting city creates 1,024 persons at this midnight; the rest follows at the next.

## Changes with a cost and their numbers

- **`BudgetQueue` ties.** Ties now compare the `ResidentId` strings instead of the rank they had at build. It is the same order, and it lets a person enrolled later take its place without renumbering. It costs a string comparison on equal posting minutes. The slice numbers include it.
- **Visitor slot check.** `CurrentCitizenSlot` is now a binary search by ID instead of an index read, used only on meal-outing, optional-visit and lease checks.
- **Skipped commuter numbers.** About a quarter of the numbers the fill reaches for a commuter are students or retirees and are skipped. Each skip costs one identity draw and builds one `pv:<n>` string; the read rule does the same for every skipped number.
- **Scans and allocation.** There is no new per-frame scan or allocation. The plan scans residents, visitors and wallets once per boundary. A fill unit costs O(visitors) moves for the ordered merge, plus the persons it creates.

## Not measured here

- Whole-frame timings and the native `NavMesh.CalculatePath` cost of portal routes (rig check 8).
- The native walking-cache hit rate for portal legs (rig check 8).
- The capacity arm's later boundaries. Each is bounded like the first (at most 16 units of 64), with the merge growing linearly up to 100,000 visitors.
