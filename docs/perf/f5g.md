# F5g performance: population fill and visitor transport

Refs [#92](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/92). Checks: [docs/checks/f5g.md](../checks/f5g.md).

Offline only, on this Mac (Apple M1 Pro, .NET 10.0.400, Release), 8 November 2026, at the head after the PR review round-2 fixes: staging in units of 64, one publication of at most 1,024 persons per boundary, and no fill work in the save. Nothing here ran in the game or on the rig. Whole-frame and native timings are not measured here and are batched for the rig (checks file, check 8).

Harnesses, all opt-in. Each fact stays under the 10-minute foreground limit:

```
PC_F5G_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter "FullyQualifiedName~F5gMeasure.MeasureReference|FullyQualifiedName~F5gMeasure.MeasureStress"
PC_F5G_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter "FullyQualifiedName~F5gMeasure.MeasureCapacity|FullyQualifiedName~F5gAccountingTests.Measure"
```

`F5gMeasure` uses fresh factor-1 cities of the F5g test fixture with the synchronous `CachedPortalPlanner` double. That double models Runtime's `WalkingRoutes` geometry cache: endpoint key, 1,024 entries, FIFO, negative entries kept, a copy per request.

Each city crosses day boundaries until one publishes fewer than 1,024 persons, which means it reached its targets. The day before each boundary is advanced in one call; the boundary itself goes slice by slice, so every staging unit and publication is timed. The measured day then runs from that boundary to the next one at the full persisted population.

`F5gAccountingTests` runs the F1 accounting scenario with the fill attached (section 4).

Persons are persisted persons: residents, commuters and the whole day-visitor pool, active or dormant. The stress row is 10,017 persisted persons, not 10,000 residents.

| Arm | Residents | Commuters | Pool | Persisted persons | Boundaries to fill | Staging units | Skipped numbers |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| reference | 957 | 999 | 576 | 2,532 | 2 | 25 | 369 |
| stress-10k | 3,339 | 4,674 | 2,004 | 10,017 | 7 | 105 | 1,700 |
| capacity, one boundary | 49,996 | 49,996 seeded + 1,024 | 0 | 101,016 after it | 49 to the 99,992 capacity | 16 | 361 |

- **Reference target.** The commuter target is 999, not 1,000, because every band capacity is even and 80% of it cannot reach every integer.
- **Skipped numbers.** All fill-created commuters have working personas (owner decision). Skipped numbers are numbers whose identity is a student or retiree.
- **Capacity arm.** The city is seeded at factor 2, and catalogue-only businesses keep commuter demand above the capacity. Only its first fill boundary is measured: 16 staging units, then one publication merging 1,024 commuters into about 50,000 visitors. The remaining 48,972 visitors need 48 more boundaries.

## 1. Staging units and the publication step (budget 1, one step per slice)

| Arm | Staging unit ms p50 / p95 / max | Unit allocated bytes p50 / p95 / max | Publications | Publication ms p50 / max | Persons per publication (max) |
| --- | --- | --- | ---: | --- | ---: |
| reference | 0.08 / 0.10 / 0.14 | 125,392 / 136,584 / 144,776 | 2 | 4.42 / 4.42 | 1,024 |
| stress-10k | 0.12 / 1.32 / 5.26 | 128,592 / 157,544 / 182,672 | 7 | 2.61 / 23.21 | 1,024 |
| capacity, one boundary | 0.09 / 22.86 / 22.86 | 129,096 / 585,744 / 585,744 | 1 | 5.48 | 1,024 |

- **No staging unit or publication takes 30 ms or more.**
- **Flagged ≥ 5 ms** (single steps, not the windowed p95):
  - the capacity arm's first staging unit (22.9 ms), which latches the plan (one scan of about 100,000 persons) and builds the candidate lists;
  - the stress publications (max 23.2 ms) and the capacity publication (5.5 ms).

  A publication links up to 1,024 prebuilt persons in one step: the ordinal merge, the queues, the occupancy index, and one meal record and wallet each. It allocates 3.2 MB at the capacity arm.
- **Staging allocation.** About 2 KB per staged person (identity, routine, traits). Wallets and meal records are created at publication.
- **Held clock.** It stands before the boundary while staging runs, then crosses it at publication: 17 steps per boundary at most (16 units of 64 plus the publication). The first fill of a city at the resident bound takes 49 boundaries of at most 1,024 persons each instead of one long hold. The round-1 design held the clock for 1,563 units at that size.

## 2. Full-day throughput at the full population, budgets and skip

The measured day runs from the boundary that finished the fill (4,321 for reference, 11,521 for stress) to the next boundary.

| Arm | Budget | Slices | Events | Slice ms p50 / p95 / max | Slices ≥ 30 ms | State hash |
| --- | --- | ---: | ---: | --- | ---: | --- |
| reference | 1 | 38,494 | 38,494 | 0.001 / 0.005 / 4.90 | 0 | `33579c84c2046d08` |
| reference | 16 | 2,406 | 38,494 | 0.027 / 0.044 / 4.13 | 0 | `33579c84c2046d08` |
| reference | 128 | 301 | 38,494 | 0.214 / 2.114 / 4.45 | 0 | `33579c84c2046d08` |
| reference | skip | 1 | n/a | 119.31 (one call) | 1 | `33579c84c2046d08` |
| stress-10k | 1 | 92,568 | 92,568 | 0.002 / 0.005 / 8.91 | 0 | `d3be4c28ca458947` |
| stress-10k | 16 | 5,786 | 92,568 | 0.036 / 0.074 / 9.89 | 0 | `d3be4c28ca458947` |
| stress-10k | 128 | 724 | 92,568 | 0.304 / 2.938 / 9.47 | 0 | `d3be4c28ca458947` |
| stress-10k | skip | 1 | n/a | 429.89 (one call) | 1 | `d3be4c28ca458947` |

- **Mod-work proxy.** The slice time, which includes citizen and meal events, postings and any fill step. p95 stays under 5 ms in every budget arm (max p95 2.94 ms).
- **Flagged ≥ 30 ms.** One sample per skip arm. Condition: a hidden skip that advances the whole measured day in a single `AdvanceTo` call. Component: the whole simulation for that skip, not a frame. No frame slice reached 30 ms.
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

## 4. Accounting clock, admission and saves (reviews Astra-r1-1, Sol-r2-2)

`F5gAccountingTests` runs the F1 scenario's frame loop, as in `tools/accounting-backlog-regression`, on the F5g fixture with the fill attached. It runs from 06:00 on day 1 to 06:00 on day 2, across the midnight fill:

- **Native clock.** It advances only by `AccountingClock.Limit` and is published by `BeginAccountingObservations`.
- **Market.** Every checkout must fall inside the current native hour.
- **Routes.** They go through a resumable planner double that admits at most 16 native paths per frame.
- **Frames.** Each frame runs `AdvanceFrameSlice(target)` steps with a time check, as Runtime does.
- **Saves.** In the save arms there is one save while midnight's persons are staged (when staging spans a frame) and one right after the midnight publication. A save does no fill work. Save time is excluded from frame cost.

| City | FPS / budget / ms | Saves | Frames | Held | Held while staged | Staging units | Published | Max lag (min) | Jobs / paths | Deferred continues | Max paths per frame | Frame p95 / max ms | State hash |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: | --- | --- |
| small (12 homes) | 30 / 1 / 0.01 | 0 | 3,772 | 2,331 | 2 | 2 | 102 | 1.00 | 582 / 942 | 0 | 2 | 0.019 / 12.34 | `00569E8259DBEE66` |
| small | 30 / 1 / 0.01 | 2 | 3,773 | 2,332 | 2 | 3 | 102 | 1.00 | 582 / 942 | 0 | 2 | 0.018 / 6.72 | `00569E8259DBEE66` |
| small | 120 / 128 / 2 | 1 | 5,768 | 7 | 0 | 2 | 102 | 0.25 | 582 / 942 | 5 | 16 | 0.011 / 1.62 | `00569E8259DBEE66` |
| reference (152 homes) | 30 / 1 / 0.01 | 0 | 20,255 | 18,796 | 16 | 16 | 1,024 | 1.00 | 3,631 / 5,409 | 0 | 2 | 0.021 / 10.76 | `1FB6F7069530C34A` |
| reference | 30 / 1 / 0.01 | 2 | 20,256 | 18,797 | 16 | 17 | 1,024 | 1.00 | 3,631 / 5,409 | 0 | 2 | 0.019 / 6.47 | `1FB6F7069530C34A` |
| reference | 60 / 16 / 0.5 | 0 | 3,574 | 692 | 4 | 16 | 1,024 | 0.50 | 3,631 / 5,409 | 0 | 16 | 0.075 / 7.74 | `1FB6F7069530C34A` |
| reference | 60 / 16 / 0.5 | 2 | 3,568 | 686 | 2 | 22 | 1,024 | 0.50 | 3,631 / 5,409 | 0 | 16 | 0.037 / 28.07 | `1FB6F7069530C34A` |
| reference | 120 / 128 / 2 | 0 | 5,960 | 199 | 0 | 16 | 1,024 | 0.25 | 3,631 / 5,409 | 174 | 16 | 0.026 / 10.43 | `1FB6F7069530C34A` |
| reference | 120 / 128 / 2 | 2 | 5,962 | 201 | 0 | 22 | 1,024 | 0.25 | 3,631 / 5,409 | 175 | 16 | 0.029 / 24.27 | `1FB6F7069530C34A` |
| reference | 60 / 1 / 0.01, skip | 2 | 20,256 | 18,797 | 16 | 17 | 1,024 | 1.00 | 3,631 / 5,409 | 0 | 2 | 0.005 / 15.17 | `1FB6F7069530C34A` |

The small city's 60/16 arms and the remaining skip arms give the same hashes and are omitted.

- **Identical results.** Every arm of a city has the same save and order list. `HistoricalClockMisses` is 0, and no checkout fell outside its native hour.
- **Lag.** Maximum lag stays inside `AccountingClock.MaximumLagMinutes`.
- **Admission.** Paths never exceed 16 per frame. At 120 FPS with 128 events per frame, about 175 Continues found the frame's admission spent, and each of those jobs waited one frame.
- **Restaging.** A save while persons are staged makes the reload stage them again, which is why the save arms show more staging units (17 or 22 instead of 16) and the same published total.
- **Frame cost.** No frame reached 30 ms.
- **Near the flag.** The frames at 24–28 ms in the reference save arms are the frames that ran the 1,024-person publication together with other events in their time budget.
- **Cap in this run.** The reference accounting city publishes its first 1,024 persons at this midnight; the rest follows at the next.

## Changes with a cost and their numbers

- **`BudgetQueue` ties.** Ties now compare the `ResidentId` strings instead of the rank they had at build. It is the same order, and it lets a person enrolled later take its place without renumbering. It costs a string comparison on equal posting minutes. The slice numbers include it.
- **Visitor slot check.** `CurrentCitizenSlot` is now a binary search by ID instead of an index read, used only on meal-outing, optional-visit and lease checks.
- **Skipped commuter numbers.** About a quarter of the numbers the fill reaches for a commuter are students or retirees and are skipped. Each skip costs one identity draw and builds one `pv:<n>` string; the read rule does the same for every skipped number.
- **Scans and allocation.** There is no new per-frame scan or allocation. The plan scans residents and visitors once per boundary. A publication costs O(visitors) moves for the ordered merge, plus the persons it links.

## Not measured here

- Whole-frame timings and the native `NavMesh.CalculatePath` cost of portal routes (rig check 8).
- The native walking-cache hit rate for portal legs (rig check 8).
- The capacity arm's later boundaries. Each is bounded like the first (16 units and one 1,024-person publication), with the merge growing linearly up to 100,000 visitors.
