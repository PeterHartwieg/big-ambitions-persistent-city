# F5e: workplace capacity and employment (writer 28), performance, 7 November 2026

Refs #92. These are offline .NET 10.0.400 measurements on an Apple M1 Pro (macOS, arm64), using Release builds run in the foreground, one dotnet process at a time. They are synthetic, not game, Mono or rig evidence. Save and load timing is a separate criterion from ordinary-play mod-work. Whole-frame timing is not measured offline and is not claimed. The four parked hotspots (physical population, closure refresh, optional-stop refresh, save serialization) were not touched.

Harness: `tests/F5eMeasure.cs`, opt-in. Run it with `PC_F5E_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F5eMeasure`. `PC_F5E_FIXTURES` and `PC_F5E_SAMPLES` narrow a run. The fixture uses the F7a representative replay's people, homes, shops and portals (`EconomyScenario`). Its two workplaces are replaced by a business catalogue shaped like the reference city: 383 work roles per 957 residents, every 30th a school, areas spread over all bands, 1 in 20 with unknown area, 1 in 50 player-owned, capped at 19,000 businesses so the stress city stays inside `MaxPlaces`. The `bands` arm seats them by `WorkplaceCapacityPolicy`. The `base-16` arm gives every business the old 16. The `tight` arms keep 20% of the businesses, so most visitors find no vacancy.

## Gate summary

- **Mod-work proxy stays far under p95 5 ms.** One natural day at 1,914 and 10,000 persons has per-slice p95 ≤ 0.61 ms in every arm, with or without bands, ample or tight capacity. No slice reaches 30 ms; the largest is 17.4 ms (base-16, 1,914 persons, first fixture of the run).
- **Job-finding roll at 10,000 persons:** p50 0.18 ms, p95 0.22 ms, 573 B after the occupancy index (0.53 ms, 0.72 ms and 102,768 B before).
- **Flagged, before the win only:** at the 100,000-person stress size the recount path had 2 of 400 find rolls ≥ 30 ms (max 66.6 ms). The index path has none (max 2.0 ms).
- **Save/load samples are ≥ 30 ms** (save criterion, flagged as in F5c-lite, F5d and F7a). They are not ordinary-play work.

## Win taken: workplace occupancy index

Before, each successful finding roll called `Population.Assignments`, which walked every resident and visitor and built a fresh dictionary of home and workplace counts. That is O(persons) time and allocation per roll. Now the simulator keeps `WorkplaceOccupancy`, a derived count of persons per Work/Study place. It is rebuilt when a simulator is built or visitors are scheduled, and moved only in `CompleteEvent` when a committed `DayPlaceId` changes. The person on the finding branch holds no workplace, so both paths give the same vacancies. The harness compares the shortlists of every timed roll (0 differ), `OccupancyIndexGivesTheSameVacanciesAsAFullRecount` compares two years of churn, and every pre-existing golden pin is unchanged. Calls without a simulator (tests, tools) keep the recount.

One successful roll (bookkeeping plus the two-probe shortlist; the two route probes run later through F3 admission), 224–400 rolls per fixture:

| Persons (places) | Recount p50 / p95 / max ms | Recount bytes | Index p50 / p95 / max ms | Index bytes | Index rebuild |
| --- | --- | ---: | --- | ---: | --- |
| 1,914 (403) | 0.193 / 0.238 / 4.53 | 22,856 | 0.083 / 0.103 / 0.14 | 562 | 0.15 ms, 22,328 B |
| 10,000 (2,075) | 0.531 / 0.725 / 4.12 | 102,768 | 0.184 / 0.224 / 3.76 | 573 | 0.55 ms, 102,232 B |
| 100,000 (19,641) | 10.93 / 12.06 / 66.6 (2 ≥ 30 ms) | 942,548 | 1.61 / 1.83 / 1.99 | 561 | 15.3 ms, 942,024 B |

What remains per roll is `DestinationChoice.Shortlist` over the place catalogue, which is O(places). The rebuild runs at simulator construction and visitor scheduling (load and city creation), never on a per-frame path. At 100,000 persons it is 15.3 ms, under the 30 ms flag.

## Day gate (one natural day, frame slices of 64 units)

| Persons | Arm | Events | Slice p50 / p95 / max ms | Work capacity | Seated residents / visitors | Carried visitors | Purchases |
| --- | --- | ---: | --- | ---: | --- | ---: | ---: |
| 1,914 | base-16 | 48,793 | 0.229 / 0.605 / 17.4 | 6,128 | 837 / 862 | 0 | 4,230 |
| 1,914 | bands | 48,785 | 0.214 / 0.462 / 3.89 | 3,920 | 837 / 862 | 0 | 4,229 |
| 1,914 | base-16 tight | 43,269 | 0.105 / 0.215 / 9.61 | 1,232 | 749 / 515 | 331 | 3,665 |
| 1,914 | bands tight | 37,783 | 0.111 / 0.239 / 2.80 | 850 | 773 / 133 | 713 | 3,054 |
| 10,000 | base-16 | 255,558 | 0.090 / 0.255 / 10.6 | 32,016 | 4,375 / 4,463 | 0 | 22,273 |
| 10,000 | bands | 255,488 | 0.090 / 0.262 / 10.2 | 19,262 | 4,375 / 4,463 | 0 | 22,263 |
| 10,000 | base-16 tight | 226,895 | 0.086 / 0.195 / 10.1 | 6,400 | 3,958 / 2,650 | 1,704 | 19,342 |
| 10,000 | bands tight | 191,995 | 0.084 / 0.186 / 8.15 | 4,062 | 3,998 / 312 | 4,042 | 15,453 |

At the reference shape the bands still leave more capacity than workers, so every worker is seated and the day matches the base within a handful of events and purchases. When capacity is tight, fewer visitors are seated, keep their carried outside employer (F5d) and make fewer commute trips and meals. That is a behaviour result of the rule, not a cost. No job changed inside the one-day window: the next daily check falls after the window starts, and the loss chance is 0.1% per day.

## Fresh-city save cost after one natural day

Bands against base-16 on the same fixture. Writer 28 has writer 27's shape, so only capacity values and assignments differ.

| Fresh city | Arm | Characters | Cap fraction | Records (streaming / semantic) | Write p50 / max ms | Read p50 / max ms |
| --- | --- | ---: | ---: | --- | --- | --- |
| 957 × 2 | base-16 | 2,429,134 | 0.9% | 13,232 / 13,229 | 26.2 / 31.7 | 86.4 / 92.5 |
| 957 × 2 | bands | 2,428,972 | 0.9% | 13,232 / 13,229 | 26.7 / 35.0 | 83.4 / 92.2 |
| 5,000 × 2 | base-16 | 12,696,098 | 4.73% | 69,297 / 69,294 | 154.7 / 194.2 | 469.7 / 502.5 |
| 5,000 × 2 | bands | 12,694,643 | 4.73% | 69,295 / 69,292 | 145.8 / 151.9 | 468.3 / 492.1 |
| 50,000 × 3 | base-16 | 146,373,436 | 54.5% | 797,318 / 797,315 | 2,020 / 2,159 | 5,906 / 5,920 |
| 50,000 × 3 | bands | 145,972,430 | 54.4% | 797,039 / 797,036 | 1,899 / 1,904 | 5,998 / 6,034 |

The 957 × 2 rows come from the second pass of a run that measured that fixture twice. The first pass included JIT warm-up (base-16 write 50 ms, read 218 ms). Every fresh city is inside section 5's budgets: the 150,000-person city uses 54.4% of the 268,435,456-character cap and 20% of the 4,000,000-record cap. Both counters agree. Bytes change by less than 0.3%, and latency is within run noise. Every save and load sample is ≥ 30 ms and flagged under the save criterion.

## Not measured

- Native `NavMesh.CalculatePath` cost of the two workplace probes. The probes keep F3 admission (≤ 16 paths per frame), unchanged by F5e.
- Whole-frame timing and in-game mod-work. These are listed for the batched rig in [docs/checks/f5e.md](../checks/f5e.md).
