# F5g performance: population fill and visitor transport

Refs [#92](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/92). Checks: [docs/checks/f5g.md](../checks/f5g.md).

Offline only, on this Mac (Apple M1 Pro, .NET 10.0.400, Release), 8 November 2026, at the head after the review round-1 fixes. Nothing here ran in the game or on the rig. Whole-frame and native timings are not measured here and are batched for the rig (checks file, check 8).

Harness: `tests/F5gMeasure.cs`, opt-in:

```
PC_F5G_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F5gMeasure
```

Each arm is a fresh factor-1 city of the F5g test fixture. It fills at its first day boundary (minute 2,880) and then runs to 06:00 the next day (minute 4,680). The planner double is `CachedPortalPlanner` from the tests, a model of Runtime's `WalkingRoutes` geometry cache (endpoint key, 1,024 entries, FIFO, negative entries kept, a copy per request).

Persons are persisted persons: residents, commuters and the whole day-visitor pool, active or dormant. The stress row is 10,017 persisted persons, not 10,000 residents.

| Arm | Residents | Commuters | Pool | Persisted persons | Fill units |
| --- | ---: | ---: | ---: | ---: | ---: |
| reference | 957 | 999 | 576 | 2,532 | 25 |
| stress-10k | 3,339 | 4,674 | 2,004 | 10,017 | 105 |

The reference commuter target is 999, not 1,000. Every band capacity is even, so 80% of the capacity cannot reach every integer. The stress city fills its whole visitor capacity (2 × 3,339 = 6,678).

## Creation unit (budget 1, one unit per slice)

| Arm | Units | Persons per unit | Unit ms p50 / p95 / max | Allocated bytes per unit p50 / p95 / max |
| --- | ---: | ---: | --- | --- |
| reference | 25 | 64 (last unit 39) | 0.67 / 3.87 / 5.68 | 313,856 / 438,808 / 536,552 |
| stress-10k | 105 | 64 (last unit 22) | 0.20 / 1.46 / 7.52 | 314,744 / 446,080 / 1,283,816 |

- No unit takes 30 ms or more. The max is the first unit of the boundary, which also latches the plan (one census scan of residents and visitors) and builds the candidate lists.
- Allocation per unit is about 5 KB per created person: identity, routine, traits, meal record and wallet, the person's queue entries, and the 64-entry merge list. The merge into the ordinal visitor lists moves existing references in place and allocates no copy of them.
- Between units the clock stands on the boundary. At one unit per frame that is 25 frames at the reference load and 105 frames at the stress size. At 60 FPS that is 0.42 s and 1.75 s of held native time once per day. With the ordinary frame budget several units share a frame (next table).

## Full-day throughput, budgets and skip

| Arm | Budget | Slices | Events | Slice ms p50 / p95 / max | Slices ≥ 30 ms | Fill slices p95 / max | State hash |
| --- | --- | ---: | ---: | --- | ---: | --- | --- |
| reference | 1 | 55,088 | 55,088 | 0.004 / 0.021 / 13.18 | 0 | 3.87 / 5.68 | `c55106e7e3ed581c` |
| reference | 16 | 3,443 | 55,088 | 0.096 / 0.191 / 5.15 | 0 | 3.83 / 5.14 | `c55106e7e3ed581c` |
| reference | 128 | 431 | 55,088 | 0.480 / 2.298 / 7.87 | 0 | 4.69 / 4.69 | `c55106e7e3ed581c` |
| reference | skip | 1 | n/a | 201.07 (one call) | 1 | n/a | `c55106e7e3ed581c` |
| stress-10k | 1 | 144,752 | 144,752 | 0.002 / 0.005 / 9.22 | 0 | 1.46 / 7.52 | `e0137f9b855b0ac3` |
| stress-10k | 16 | 9,047 | 144,752 | 0.031 / 0.064 / 8.17 | 0 | 0.31 / 6.80 | `e0137f9b855b0ac3` |
| stress-10k | 128 | 1,131 | 144,752 | 0.264 / 2.870 / 16.41 | 0 | 4.91 / 8.02 | `e0137f9b855b0ac3` |
| stress-10k | skip | 1 | n/a | 651.11 (one call) | 1 | n/a | `e0137f9b855b0ac3` |

- Mod-work proxy: the slice time, which includes fill units, citizen and meal events and postings. p95 stays under 5 ms in every budget arm (max p95 2.87 ms).
- Flagged ≥ 30 ms: one sample per skip arm. Condition: a hidden skip that advances 48 game hours (minute 1,800 to 4,680) in a single `AdvanceTo` call, the fill included. Component: the whole simulation for that skip, not a frame. No frame-slice sample reached 30 ms.
- Budget and cadence equality: every arm of a shape ends with the same save and order list (state hash column). `HistoricalClockMisses` is 0 in every arm.
- Events per day are equal across budgets. The peak person queue and peak meal queue equal the persisted persons: one routine event and one meal event per person, dormant persons included.
- Backlog and lag: the fill adds no backlog after the boundary. It is held work at the boundary minute, drained before any later event (frames above). The F1 lag harness (`tools/accounting-backlog-regression`) was not run for this slice.

## Portal routes and admissions (model)

| Arm | Route requests | Portal requests | Portal hits | Portal misses | Peak requests in one game minute | Frames at 16 paths per frame |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| reference | 37,867 | 2,808 | 2,792 | 16 | 177 | 12 |
| stress-10k | 41,528 | 13,096 | 13,068 | 28 | 240 | 15 |

The figures are from the budget-1 arms. "Portal" means a request that starts or ends at a portal place.

The double keys geometry by place pair, so its hit rate is an upper bound. Runtime's cache keys by exact endpoint positions and also caches the station connectors. The frame column assumes one native path query per request. A multimodal request can need several (access, egress, comparison), so native admissions are batched for rig check 8. The two portals of the fixture explain the low miss counts. A real city has one portal per discovered station (up to 256).

## Changes with a cost and their numbers

- `BudgetQueue` ties now compare the `ResidentId` strings instead of the rank they had at build. This is the same order and lets a person enrolled later take its place without renumbering. It is not a win: it costs a string comparison on equal posting minutes. The slice numbers above include it. A separate before/after for postings was not taken.
- The visitor slot check (`CurrentCitizenSlot`) is now a binary search by ID instead of an index read. It runs only on meal-outing, optional-visit and lease checks, so it is O(log visitors) on those calls and costs nothing per frame.
- No new per-frame scan or allocation. The plan scans residents and visitors once per day boundary. A fill unit costs O(visitors) moves for the ordered merge, plus the created persons themselves.

## Not measured here

- Whole-frame timings and native `NavMesh.CalculatePath` cost of portal routes (rig check 8).
- The native walking-cache hit rate for portal legs (rig check 8).
- Fill at 50,000 residents (100,000 visitors). The unit cost is bounded by 64 persons. The O(visitors) merge per unit and the boundary plan scan grow linearly and were not timed at that size.
