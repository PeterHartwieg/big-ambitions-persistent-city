# F5h performance: integrated F5 population at the declared and stress loads

Refs [#92](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/92). Checks: [docs/checks/f5h.md](../checks/f5h.md). Plan: [F5 section 6](../plans/f5-population.md#6-performance).

Offline only. Apple M1 Pro, .NET 10.0.400, Release, one dotnet process at a time, 8 November 2026. The machine was shared (load average 3.4 to 6.8 on 10 cores during the runs). All numbers are synthetic. Nothing here ran in the game, on Mono or on the rig. Whole-frame cost is not measured offline; it belongs to rig check R8 in the checks file.

## Summary

- **Mod-work p95 stays under 5 ms at every size.** Per-frame p95 is 0.44 ms at the declared load (2,532 persons) and at most 0.44 ms at every size up to 99,996 persons, in Runtime's 60 FPS frame shape.
- **Spikes of 30 ms or more:** two in all the runs below. One is a 35.9 ms citizen unit at 99,996 persons (06:20, a gen-1 .NET GC inside the unit). The other is a 33.8 ms citizen unit at 10,017 persons in the 30 / 32 / 4 matrix arm (a gen-2 GC inside the unit). Every unit of 5 ms or more in every run had a .NET GC inside it. The test process's GC is not Mono's, so the game number needs R8.
- **Throughput.** The declared load keeps a game day at 1.29 times the frames of an unheld clock at 60 FPS, and 10,017 persons at 2.32 times. 50,000 and 100,000 persons cannot keep pace: 13.2 and 26.8 times. That is the refusal envelope the plan asks to record. Lag stays at most 0.5 game minutes and no hour is missed, because the F1 clock holds the calendar instead of skipping work.
- **No perf change in F5h.** The integrated numbers needed no cheap win. The four parked hotspots stay parked.

## Method

Harness: `tests/F5hMeasure.cs`, opt-in.

```
PC_F5H_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter "FullyQualifiedName~F5hMeasure.MeasureSizes"
PC_F5H_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter "FullyQualifiedName~F5hMeasure.MeasureReferenceMatrix|FullyQualifiedName~F5hMeasure.MeasureStressMatrix"
```

`PC_F5H_SIZES` picks sizes; `PC_F5H_SAMPLES` sets save samples (default 3). The sizes run took 2 min 31 s and the two matrices 27 s.

Every city is a fresh F5g fixture city in writer 34 with the fill attached (`F5gPopulationFillTests.City`, seed 2026, homes of seven beds, 90% fill, two portals, three player food shops). Persons are persisted persons: residents, commuters and the whole day-visitor pool, active or dormant.

| Size | Homes | Seeding | Window | Persons at start → end | Residents / commuters / pool at end |
| --- | ---: | --- | --- | --- | --- |
| reference (declared load) | 152 | factor 1, filled for two boundaries first | day 3 06:00 → day 4 06:00 | 2,532 → 2,532 | 957 / 999 / 576 |
| 1,914 | 152 | factor 2 (957 seeded commuters) | day 1 06:00 → day 2 06:00 | 1,914 → 2,490 | 957 / 957 / 576 |
| 10,000 | 530 | factor 1, filled for seven boundaries first | day 8 06:00 → day 9 06:00 | 10,017 → 10,017 | 3,339 / 4,674 / 2,004 |
| 50,000 | 3,887 | factor 2 | day 1 06:00 → day 2 06:00 | 48,976 → 50,000 | 24,488 / 24,488 / 1,024 |
| 100,000 | 7,855 | factor 2 | day 1 06:00 → day 2 06:00 | 98,972 → 99,996 | 49,486 / 49,486 / 1,024 |

- **Why factor 2 at 50,000 and 100,000.** Filling a factor-1 city to those sizes from empty takes 33 to 66 boundaries of 1,024 persons. Seeding R commuters through F5d's fresh roster and letting the window's first boundary create 1,024 pool members gives the size in one day and still runs 16 fill units inside the window. The pool stays at 1,024 there; it would keep growing at later boundaries.
- **The window** is one game day of the F1 frame loop (`F5hIntegratedAcceptanceTests.Accounting`). The native clock advances only as `AccountingClock.Limit` allows. The market refuses a checkout outside the current native hour. Route jobs go through a resumable planner double that admits at most 16 native paths per frame and models Runtime's 1,024-entry walking-route cache (a cached endpoint pair needs no native path).
- **Frame shape.** The size runs use Runtime's 60 FPS caps from `PopulationFrameBudget`: 128 units, 16 citizen events and 2 ms per frame, with only citizen events counted against the 16, as `PersistentCityMod` does. The matrix arms are the plan's F1 arms, where every unit counts against the event budget; that is stricter than Runtime.
- **Components.** Each slice runs one unit, so each unit is timed and classed by what it did: a fill unit (at most 64 persons), a posting unit (at most 32 wallets), a citizen event, a food decision continuation, or route planning (`ContinuePlanning`). Per-frame component time is the sum of that component's units in the frame.
- **Spikes.** Every unit of 5 ms or more is recorded with its component, game minute and whether a .NET GC (and which generation) ran inside it.

## 1. Mod-work per frame and by component (60 FPS, Runtime caps)

Frame ms p50 / p95 / max. Component columns are over the frames where that component ran.

| Size | Frames | Frame | Frames ≥ 5 / ≥ 30 ms | Fill | Postings | Planning | Events (citizen and food) |
| --- | ---: | --- | --- | --- | --- | --- | --- |
| reference | 3,722 | 0.017 / 0.437 / 7.13 | 2 / 0 | none in window | 0.004 / 0.741 / 1.24 | 0.008 / 0.061 / 0.34 | 0.032 / 0.489 / 6.76 |
| 1,914 | 3,706 | 0.012 / 0.151 / 4.41 | 0 / 0 | 2.43 / 4.40 / 4.40 (3 frames) | 0.003 / 0.545 / 0.64 | 0.005 / 0.048 / 0.10 | 0.022 / 0.149 / 4.18 |
| 10,000 | 6,692 | 0.037 / 0.144 / 9.86 | 23 / 0 | none in window | 0.003 / 0.232 / 0.75 | 0.013 / 0.031 / 6.92 | 0.034 / 0.148 / 8.60 |
| 50,000 | 37,896 | 0.031 / 0.143 / 22.98 | 154 / 0 | 2.20 / 2.81 / 2.81 (3 frames) | 0.171 / 0.327 / 0.79 | 0.003 / 0.027 / 14.72 | 0.036 / 0.171 / 22.97 |
| 100,000 | 77,186 | 0.034 / 0.220 / 35.94 | 329 / 1 | 4.29 / 9.49 / 9.49 (3 frames) | 0.185 / 0.373 / 0.79 | 0.002 / 0.026 / 19.78 | 0.041 / 0.342 / 35.94 |

Single units, ms p50 / p95 / max:

| Size | Posting unit | Citizen event | Food continuation | Fill unit (count) | Fill unit allocated bytes p50 / max |
| --- | --- | --- | --- | --- | --- |
| reference | 0.041 / 0.059 / 0.39 | 0.005 / 0.026 / 3.88 | 0.004 / 0.016 / 6.42 | see section 3 | see section 3 |
| 1,914 | 0.031 / 0.042 / 0.07 | 0.003 / 0.018 / 4.13 | 0.003 / 0.025 / 0.03 | 0.38 / 3.91 / 3.91 (9) | 302,800 / 655,392 |
| 10,000 | 0.010 / 0.020 / 0.39 | 0.002 / 0.007 / 6.77 | 0.001 / 0.004 / 8.57 | see section 3 | see section 3 |
| 50,000 | 0.013 / 0.024 / 0.10 | 0.003 / 0.006 / 22.96 | 0.001 / 0.004 / 0.05 | 0.27 / 2.81 / 2.81 (16) | 305,208 / 524,136 |
| 100,000 | 0.014 / 0.025 / 0.09 | 0.003 / 0.007 / 35.89 | 0.001 / 0.002 / 0.13 | 0.26 / 7.78 / 7.78 (16) | 305,208 / 759,808 |

Flagged units of 5 ms or more:

| Size | Units ≥ 5 ms | With a GC inside | Gen 2 | Components | Largest |
| --- | ---: | ---: | ---: | --- | --- |
| reference | 1 | 1 | 0 | food 1 | 6.4 ms food continuation, minute 4,747 (07:07, day 3), gen 1 |
| 1,914 | 0 | 0 | 0 | none | none |
| 10,000 | 21 | 21 | 0 | citizen 15, food 6 | 9.8 ms citizen, minute 12,579 (17:39), gen 1 |
| 50,000 | 147 | 147 | 3 | citizen 147 | 23.0 ms citizen, minute 1,813 (06:13), gen 1 |
| 100,000 | 302 | 302 | 0 | citizen 301, fill 1 | **35.9 ms citizen, minute 1,820 (06:20), gen 1** |

- **Condition.** At 50,000 and 100,000 the large units cluster in the morning commute (06:10–06:35) and the evening (17:20–19:20), when the most citizen events allocate. The time is the .NET collector walking a heap of 120–186 MB of retained city, not the event's own work: the citizen unit p95 is 0.007 ms at both sizes.
- **Planning max.** The planning maxima (6.9 ms at 10,000, 14.7 ms at 50,000, 19.8 ms at 100,000) are single `ContinuePlanning` calls. The harness does not tag planning calls with GC counts. The double's path work per call is constant, so a GC is the likely cause, but that is not measured.
- **What this means for the game.** Mono's collector behaves differently, so R8 must measure it. Allocation per citizen event is not reduced here; that would be a perf-only change outside F5h.

## 2. Throughput, events, queues, planning and portal routes

| Size | Frames for the day | Frames ÷ unheld | Held frames | Max lag (game min) | Units in the day | Units per game hour max / mean | Peak due queue persons / meals | Posting units' wallets |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- | ---: |
| reference | 3,722 | 1.29 | 841 | 0.5 | 32,325 | 6,264 / 1,469 | 2,532 / 2,532 | 2,668 |
| 1,914 | 3,706 | 1.29 | 825 | 0.5 | 15,812 | 3,310 / 719 | 2,490 / 2,490 | 2,626 |
| 10,000 | 6,692 | 2.32 | 3,811 | 0.5 | 94,294 | 23,437 / 4,286 | 10,017 / 10,017 | 10,153 |
| 50,000 | 37,896 | 13.16 | 35,014 | 0.5 | 266,993 | 73,639 / 12,136 | 50,000 / 50,000 | 50,136 |
| 100,000 | 77,186 | 26.80 | 74,302 | 0.5 | 526,191 | 147,814 / 22,878 | 99,996 / 99,996 | 100,132 |

- **Frames ÷ unheld** is the day's frames against 2,880, the frames of a native clock that is never held at 60 FPS. The 16-citizen-event cap binds in the busiest hours (06:00–09:00 and the evening), and the clock holds there. `HistoricalClockMisses` is 0 at every size.
- **Refusal envelope.** At 50,000 and 100,000 the day takes 13 and 27 times as many frames. The plan names these sizes as throughput probes. They do not keep pace under Runtime's 60 FPS caps, and the F1 clock slows the calendar instead of dropping work. The rig's R8 run is where a stress city at this size would show the player-visible effect.
- **Due queue.** Every person keeps exactly one queued routine event and one meal record, so the peak queue equals the persisted persons. There is no backlog beyond that: the planning backlog is the next row.
- **Postings.** The posting units posted about one wallet per person in the day (2,668 for 2,532 persons at the declared load). I did not break the excess down.

Planning admissions and portal routes:

| Size | Route jobs | Native paths | Deferred continues | Max paths per frame | Max frames a job waited | Longest run of frames with planning pending | Portal hits / misses |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| reference | 6,304 | 1,099 | 5 | 16 | 1 | 2 | 2,942 / 16 |
| 1,914 | 6,076 | 1,086 | 3 | 16 | 1 | 2 | 2,444 / 16 |
| 10,000 | 23,075 | 6,217 | 76 | 16 | 1 | 7 | 13,484 / 28 |
| 50,000 | 113,965 | 63,744 | 526 | 16 | 1 | 491 | 62,656 / 134 |
| 100,000 | 226,023 | 135,355 | 1,632 | 16 | 1 | 1,101 | 126,490 / 240 |

- Admission never exceeds 16 native paths per frame, and no job waits more than one frame for admission.
- The longest pending run at 50,000 and 100,000 is a run of consecutive frames in which some job was planning. With the clock held most frames, those runs are long, but each job waited at most one frame.
- Portal hits and misses come from the cache model, keyed by place pair. That is an upper bound on Runtime's hit rate: Runtime keys by exact endpoint positions and also caches station connectors. The native rate is R8's.

## 3. The F1 matrix (event budgets, cadences, hidden skip, reload)

The plan's arms, every unit counted against the event budget. Each arm ran with and without a save and reload (one save as the clock reaches midnight and one between two fill units). All arms of a size end with the same save and order list (state hash column), with `HistoricalClockMisses` 0 and at most 16 paths per frame. The ordinary test `DeclaredLoadF1MatrixEqualsTheReferenceAcrossBudgetsCadencesSkipAndReload` asserts the same equality at the declared load on every test run.

Declared load: day 2 06:00 → day 3 06:00, 1,981 → 2,532 persons, the second boundary creating the last 551 (9 units). State hash `2a5286427e059f0b` in every arm.

| Arm (FPS / units / ms) | Saves | Frames | Frames ÷ unheld | Held (on the boundary) | Max lag | Frame p50 / p95 / max ms | ≥ 30 ms | Fill unit p50 / max ms |
| --- | ---: | ---: | ---: | --- | ---: | --- | ---: | --- |
| 60 / 128 / 2 (reference) | 0 | 2,989 | 1.04 | 108 (1) | 0.5 | 0.001 / 0.124 / 4.63 | 0 | 0.15 / 3.42 |
| 30 / 32 / 4 | 0 | 2,477 | 1.72 | 1,036 (0) | 1.0 | 0.024 / 0.066 / 13.37 | 0 | 0.17 / 0.30 |
| 30 / 32 / 4 | 2 | 2,479 | 1.72 | 1,038 (1) | 1.0 | 0.023 / 0.066 / 4.37 | 0 | 0.15 / 3.06 |
| 60 / 16 / 2 | 0 | 5,099 | 1.77 | 2,218 (0) | 0.5 | 0.010 / 0.033 / 4.40 | 0 | 0.17 / 0.34 |
| 60 / 16 / 2 | 1 | 5,108 | 1.77 | 2,227 (0) | 0.5 | 0.011 / 0.034 / 4.33 | 0 | 0.15 / 0.23 |
| 120 / 128 / 2 | 0 | 5,861 | 1.02 | 100 (0) | 0.25 | 0.000 / 0.088 / 4.19 | 0 | 0.16 / 0.27 |
| 120 / 128 / 2 | 1 | 5,862 | 1.02 | 101 (0) | 0.25 | 0.000 / 0.086 / 4.16 | 0 | 0.15 / 0.24 |
| hidden skip, 32 / 4 | 0 | 2,476 | n/a | 1,035 (0) | 1.0 | 0.023 / 0.062 / 4.63 | 0 | 0.15 / 0.25 |
| hidden skip, 32 / 4 | 1 | 2,477 | n/a | 1,036 (0) | 1.0 | 0.023 / 0.066 / 4.05 | 0 | 0.15 / 0.24 |

10,000 persons (opt-in): day 7 06:00 → day 8 06:00, 9,483 → 10,017 persons, the seventh boundary creating the last 534 (9 units). State hash `6a3b63e2785913ee` in every arm.

| Arm (FPS / units / ms) | Saves | Frames | Frames ÷ unheld | Held (on the boundary) | Max lag | Frame p50 / p95 / max ms | ≥ 30 ms | Fill unit p50 / max ms |
| --- | ---: | ---: | ---: | --- | ---: | --- | ---: | --- |
| 60 / 128 / 2 (reference) | 0 | 3,461 | 1.20 | 580 (3) | 0.5 | 0.019 / 0.289 / 8.92 | 0 | 0.18 / 4.66 |
| 30 / 32 / 4 | 0 | 4,181 | 2.90 | 2,740 (1) | 1.0 | 0.054 / 0.093 / **33.78** | **1** | 0.16 / 3.45 |
| 30 / 32 / 4 | 1 | 4,182 | 2.90 | 2,741 (0) | 1.0 | 0.052 / 0.084 / 8.84 | 0 | 0.17 / 0.47 |
| 60 / 16 / 2 | 0 | 8,496 | 2.95 | 5,615 (1) | 0.5 | 0.025 / 0.043 / 8.42 | 0 | 0.19 / 2.83 |
| 60 / 16 / 2 | 1 | 8,500 | 2.95 | 5,619 (0) | 0.5 | 0.026 / 0.043 / 8.58 | 0 | 0.15 / 0.47 |
| 120 / 128 / 2 | 0 | 6,293 | 1.09 | 532 (1) | 0.25 | 0.002 / 0.194 / 8.98 | 0 | 0.18 / 3.06 |
| 120 / 128 / 2 | 2 | 6,295 | 1.09 | 534 (2) | 0.25 | 0.002 / 0.196 / 8.43 | 0 | 0.18 / 3.10 |
| hidden skip, 32 / 4 | 0 | 4,181 | n/a | 2,740 (1) | 1.0 | 0.053 / 0.081 / 8.06 | 0 | 0.16 / 3.24 |
| hidden skip, 32 / 4 | 1 | 4,187 | n/a | 2,746 (0) | 1.0 | 0.052 / 0.082 / 8.14 | 0 | 0.15 / 0.51 |

- **Flagged ≥ 30 ms.** One frame of 33.8 ms at 10,017 persons, 30 / 32 / 4 without saves: a citizen unit with a gen-2 GC inside, minute 10,922 (14:02 on day 7). The same arm with saves had none. At the declared load the largest frame was 13.4 ms, also a citizen unit with a gen-2 GC.
- **Fill unit cost.** Every arm of a city runs the same nine units. The p50 is 0.15–0.19 ms, and the max varies from 0.23 to 4.66 ms between arms doing identical work, so the max is machine noise or a GC rather than fill work. F5g found that the first unit of a boundary, which latches the plan with one scan of residents, visitors and wallets, is the expensive one.
- **Holds at the boundary.** At most three frames in any arm hold the clock while the boundary's fill has units left.
- **Hidden skip.** The skip arm requests 120 game minutes per frame, so "frames ÷ unheld" has no meaning there. It runs the same units under the 32-unit cap and ends equal.

## 4. Saved state at the end of each window

Writer 34 (compact). Write is `SaveCodec.Write`, read is `SaveCodec.Read`; p50 of 3 samples. These are save and load latencies and never count as ordinary-play mod-work.

| Size | Persons | Characters (UTF-8 bytes) | Of the 256 Mi-character cap | Records (streaming = semantic) | Retained after read | Write ms p50 | Read ms p50 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| reference | 2,532 | 1,083,570 | 0.40% | 21,454 | 10.3 MB | 137 | 299 |
| 1,914 → 2,490 | 2,490 | 851,770 | 0.32% | 14,083 | 9.4 MB | 74 | 122 |
| 10,000 | 10,017 | 5,039,928 | 1.88% | 122,132 | 43.5 MB | 334 | 527 |
| 50,000 | 50,000 | 13,157,413 | 4.90% | 221,324 | 119.7 MB | 1,593 | 2,560 |
| 100,000 | 99,996 | 26,156,631 | 9.74% | 426,674 | 185.8 MB | 3,375 | 5,347 |

- Records stay far below the 4,000,000 cap. The 100,000-person city uses 10.7% of it.
- The factor-2 cities at 50,000 and 100,000 cost about half the characters per person of the filled 10,000-person city (263 against 503). I did not break that difference down.

## Before and after, against the earlier F5 slices

| Measure | Earlier slice | F5h integrated |
| --- | --- | --- |
| Fill unit at the declared load, ms | F5g: p95 0.27, max 5.20 (2,532 persons, budget 1) | p50 0.15–0.17, max 0.23–3.42 across the arms (section 3) |
| Fill unit at 10,000 persons, ms | F5g: p95 4.39, max 6.67 | p50 0.15–0.19, max 0.47–4.66 across the arms |
| Fill unit near 50,000 visitors, ms max | F5g capacity arm: 6.92 (merging into 50,000 visitors) | 2.81 at 50,000 persons and 7.78 at 99,996 (merging into 25,512 and 50,510 visitors) |
| Day slice / frame p95, declared load | F5g: ≤ 2.14 ms per 128-unit slice | 0.124 ms per frame at 60 / 128 / 2; 0.437 ms in Runtime's frame shape |
| Day slice / frame p95, 10,000 | F5g: ≤ 2.41 ms per 128-unit slice | 0.289 ms at 60 / 128 / 2; 0.144 ms in Runtime's frame shape |
| Posting units p95 at 1,914 / 10,000 / 100,000 | F3: 0.0208 / 0.0229 / 0.0111 ms | 0.042 / 0.020 / 0.025 ms (posting unit, 32 wallets) |
| Portal route model hits / misses | F5g: 5,558 / 32 (declared load, fill days included) | 2,942 / 16 over one full-population day |
| Save size, declared load | F7d: 849,572 chars (representative 957×2), 1,152,633 (957×3) | 1,083,570 chars for 957 residents, 999 commuters and 576 pool members |
| Save write p50, declared load | F7d: 131 ms (957×2), 141 ms (957×3) on the Windows PC | 137 ms on this Mac |
| Physical selection | F5f: p95 0.0133 / 0.0126 / 0.0123 ms at 1,914 / 10,000 / 50,000 persons with 100 active journeys | not rerun: F5h changes no selection code, and visitors stay out of selection until F5i |

The F5g day-slice p95 measured 128 units per slice with no frame time cap. The F5h frames stop at the time cap and the citizen cap, so the two columns measure different things. The comparison shows the frame loop keeps every per-frame p95 well under 5 ms.

## Changes with a cost

None. F5h changes words in the panel and the inspector (fixed strings, no new scan) and adds tests. The panel line it replaces already printed the same three counts.

## Parked hotspots

Physical selection (F5f took the scan part), closure refresh, optional-stop refresh and save serialization stay parked. F5h took no perf-only rewrite. The GC-bound spikes in section 1 point at allocation per citizen event at 50,000 persons and more. Reducing it is a perf slice of its own, not F5h's.

## Not measured offline

- Whole-frame time, render cost and stalls (R8).
- Mono's collector, which decides whether the GC-bound units above appear in the game (R8).
- Native `NavMesh.CalculatePath` cost and the native walking-cache hit rate for portal legs (R8).
- In-game save pauses at these sizes (a separate, explicitly requested save-profiling run).
- A factor-1 city filled from empty to 50,000 or 100,000 persons. The harness can do it, but each needs 33 to 66 game days of fill. F5g's capacity arm measured the boundary cost at the resident bound, and the factor-2 rows above measure a full day at the size.
