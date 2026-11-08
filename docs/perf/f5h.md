# F5h performance: integrated F5 population at the declared and stress loads

Refs [#92](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/92). Checks: [docs/checks/f5h.md](../checks/f5h.md). Plan: [F5 section 6](../plans/f5-population.md#6-performance).

Offline only. Apple M1 Pro, .NET 10.0.400, Release, one dotnet process at a time, 8 November 2026. The machine was shared (load average 3.5 to 4.4 on 10 cores during these runs). All numbers are synthetic. Nothing here ran in the game, on Mono or on the rig. Whole-frame cost is not measured offline; it belongs to rig check R8 in the checks file.

These are the numbers after PR review round 1. That round found three harness faults, all fixed before this run:

- the cities' saved places lacked the census's workplaces, so no commuter held a seat (Astra-r1-1);
- the frame loop did not follow Runtime's planning rules (Sol-r1-1);
- events finished through route planning were not counted (Astra-r1-3).

The round-0 numbers are kept only in the comparison table at the end.

## Summary

- **Mod-work p95 stays under 5 ms at every size.** Per-frame p95 is 0.39 ms at the declared load (2,552 persons) and at most 0.39 ms at every size up to 99,996 persons, in Runtime's 60 FPS frame shape.
- **Spikes of 30 ms or more** (all listed in sections 1 and 3):
  - 8 frames at 99,996 persons, the largest 45.1 ms. Each is a citizen unit with a gen-1 .NET GC inside it, plus one 32.0 ms planning call.
  - 1 frame at 50,000 persons (30.3 ms, a citizen unit with a GC).
  - 1 frame at 10,017 persons (42.2 ms, a citizen unit with a gen-2 GC).
  - 1 frame in the 10,000-person matrix (32.9 ms, a citizen unit with a gen-2 GC).
  - None at the declared load or at 1,914 persons.
- **Fill units near the flag line at the stress sizes.** The units on the 50,000- and 100,000-person cities' first boundary take up to 25.2 and 28.2 ms with no GC inside them. That is fill work: those cities now have about 2,000 and 4,000 workplaces to seat commuters in. At the declared load a fill unit takes at most 0.44 ms without saves.
- **Throughput.** At 60 FPS a game day takes 1.53 times the frames of an unheld clock at the declared load and 3.46 times at 10,017 persons. 50,000 and 100,000 persons cannot keep pace (14.1 and 26.0 times). That is the refusal envelope the plan asks to record. Lag stays at most 0.5 game minutes and no hour is missed, because the F1 clock holds the calendar instead of skipping work.
- **Due backlog.** After a frame, at most 129 events and meals are due but not yet run at the declared load (575 on a fill boundary in the matrix), and a backlog drains within 9 frames (43 at 60 / 16 / 2 across the boundary). At 100,000 persons the peak is 4,035 and a backlog lasts up to 307 frames.
- **No perf change in F5h.** The four parked hotspots stay parked.

## Method

Harness: `tests/F5hMeasure.cs`, opt-in.

```
PC_F5H_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter "FullyQualifiedName~F5hMeasure.MeasureSizes"
PC_F5H_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter "FullyQualifiedName~F5hMeasure.MeasureReferenceMatrix|FullyQualifiedName~F5hMeasure.MeasureStressMatrix"
```

`PC_F5H_SIZES` picks sizes; `PC_F5H_SAMPLES` sets save samples (default 3). The sizes run took 3 min 39 s and the two matrices 37 s. The 50,000 and 100,000 rows below come from a second sizes run (3 min 28 s), which also lists every unit of 5 ms or more without a GC. Its state hashes equal the first run's.

Every city is a fresh F5g fixture city in writer 34 with the fill attached: seed 2026, homes of seven beds at 90% fill, two portals, three player food shops.

**One business list per city.** F5g's base businesses plus a number of large (900 m², 24-seat) businesses are both the saved places and the census catalogue, so every census workplace is one a commuter can be seated in (`F5hIntegratedAcceptanceTests.Shape`). The number of large businesses is the fewest for which the city's own commuter target reaches 1,000 at the declared load, or the whole capacity left after the pool at 10,000 persons. The factor-2 sizes scale the declared load's businesses by homes.

| Size | Homes | Added large businesses | Seeding | Window | Persons at start → end | Residents / commuters / pool at end |
| --- | ---: | ---: | --- | --- | --- | --- |
| reference (declared load) | 152 | the declared load's | factor 1, filled for two boundaries first | day 3 06:00 → day 4 06:00 | 2,551 → 2,552 | 957 / 1,019 / 576 |
| 1,914 | 152 | the declared load's | factor 2 (957 seeded commuters) | day 1 06:00 → day 2 06:00 | 1,914 → 2,551 | 957 / 1,018 / 576 |
| 10,000 | 530 | for the full capacity | factor 1, filled for seven boundaries first | day 8 06:00 → day 9 06:00 | 10,017 → 10,017 | 3,339 / 4,687 / 1,991 |
| 50,000 | 3,887 | scaled by homes | factor 2 | day 1 06:00 → day 2 06:00 | 48,976 → 50,000 | 24,488 / 24,609 / 903 |
| 100,000 | 7,855 | scaled by homes | factor 2 | day 1 06:00 → day 2 06:00 | 98,972 → 99,996 | 49,486 / 49,633 / 877 |

- **Why factor 2 at 50,000 and 100,000.** Filling a factor-1 city that large from empty takes 33 to 66 boundaries of 1,024 persons. Seeding R commuters through F5d's fresh roster and letting the window's first boundary create 1,024 more persons in 16 units gives the size in one day. The pool stays under 1,000 there; it would keep growing at later boundaries.
- **The window** is one game day of the F1 frame loop (`F5hIntegratedAcceptanceTests.Accounting`):
  - the native clock advances only as `AccountingClock.Limit` allows;
  - the market refuses a checkout outside the current native hour;
  - route jobs go through a resumable planner double that admits at most 16 native paths per frame and models Runtime's 1,024-entry walking-route cache (a cached endpoint pair needs no native path).
- **Frame loop** (review Sol-r1-1). It follows `PersistentCityMod`:
  - the first unit always runs;
  - a planning call gets the frame's remaining time;
  - an unfinished plan ends the frame only when the time or the 16-path admission is spent, otherwise planning continues with the event's next request;
  - a finished plan counts as one citizen event and is followed by the time check;
  - a slice that begins planning skips the citizen-limit check.

  The size runs use Runtime's 60 FPS caps from `PopulationFrameBudget`: 128 units, 16 citizen events and 2 ms per frame, with only citizen events counted against the 16. The matrix arms are the plan's F1 arms, where every unit counts against the event budget; that is stricter than Runtime.
- **Event counts** (review Astra-r1-3). A unit is counted once: a slice's own units, plus one for each event finished through route planning. The ordinary test checks this against a synchronous replay of the same day: the frame loop's unit total equals the replay's, and both cities end equal (`DeclaredLoadF1MatrixEqualsTheReferenceAcrossBudgetsCadencesSkipAndReload`). Hourly means are over all 24 hours.
- **Components.** Each slice runs one unit, so each unit is timed and classed by what it did:
  - a fill unit (at most 64 persons);
  - a posting unit (at most 32 wallets);
  - a citizen event;
  - a food decision continuation;
  - route planning (`ContinuePlanning`).

  Per-frame component time is the sum of that component's units in the frame.
- **Queue occupancy and due backlog** (review Sol-r1-3). Occupancy is the whole queue, future events included: every person keeps one routine event and one meal record queued, so it equals the persons. The due backlog is the person events and meals due at or before the frame's target that have not run when the frame ends. Two measurement-only internal counters give it: `CitySimulator.DuePersonEventsAtOrBefore` and `DueMealEventsAtOrBefore`. They walk each ordered queue from its minimum, have no Runtime caller and are not saved. The drain is the longest run of consecutive frames that end with a backlog.
- **Spikes.** Every slice of 5 ms or more is recorded with its component, game minute and whether a .NET GC (and which generation) ran inside it. Planning calls are not tagged with GC counts.

## 1. Mod-work per frame and by component (60 FPS, Runtime caps)

Frame ms p50 / p95 / max. Component columns are over the frames where that component ran.

| Size | Frames | Frame | Frames ≥ 5 / ≥ 30 ms | Fill | Postings | Planning | Events (citizen and food) |
| --- | ---: | --- | --- | --- | --- | --- | --- |
| reference | 4,401 | 0.040 / 0.388 / 7.05 | 2 / 0 | 0.31 (1 frame) | 0.049 / 0.099 / 1.05 | 0.011 / 0.056 / 3.62 | 0.040 / 0.406 / 5.25 |
| 1,914 | 4,315 | 0.022 / 0.143 / 6.11 | 2 / 0 | 2.06 (2 frames) | 0.020 / 0.044 / 0.26 | 0.007 / 0.052 / 3.14 | 0.025 / 0.133 / 5.89 |
| 10,000 | 9,958 | 0.035 / 0.174 / **42.24** | 47 / **1** | none in window | 0.124 / 0.254 / 0.91 | 0.014 / 0.032 / 8.66 | 0.027 / 0.166 / 42.24 |
| 50,000 | 40,615 | 0.053 / 0.161 / **30.27** | 226 / **1** | 8.45 / 25.20 / 25.20 (6 frames) | 0.512 / 0.855 / 6.86 | 0.022 / 0.068 / 24.44 | 0.038 / 0.130 / 29.95 |
| 100,000 | 74,973 | 0.052 / 0.195 / **45.10** | 407 / **8** | 7.46 / 28.22 / 28.22 (5 frames) | 0.571 / 0.947 / 1.34 | 0.024 / 0.073 / **32.01** | 0.038 / 0.142 / 45.10 |

Single units, ms p50 / p95 / max:

| Size | Posting unit | Citizen event | Food continuation | Fill unit (count) | Fill unit allocated bytes p50 / max |
| --- | --- | --- | --- | --- | --- |
| reference | 0.047 / 0.064 / 0.10 | 0.003 / 0.017 / 4.16 | 0.004 / 0.013 / 4.92 | 0.31 (1, a single job-churn commuter) | 33,824 |
| 1,914 | 0.012 / 0.029 / 0.04 | 0.002 / 0.015 / 5.84 | 0.002 / 0.030 / 0.03 | 0.22 / 1.41 / 1.41 (10) | 302,736 / 696,224 |
| 10,000 | 0.020 / 0.032 / 0.53 | 0.002 / 0.006 / 42.21 | 0.001 / 0.004 / 13.24 | none in window | none |
| 50,000 | 0.040 / 0.069 / 6.65 | 0.003 / 0.009 / 29.86 | 0.002 / 0.079 / 3.06 | 0.41 / 25.20 / 25.20 (16) | 305,208 / 854,400 |
| 100,000 | 0.040 / 0.064 / 0.20 | 0.002 / 0.009 / 45.10 | 0.002 / 0.007 / 0.07 | 0.29 / 28.22 / 28.22 (16) | 305,208 / 1,410,672 |

Flagged slices of 5 ms or more:

| Size | Slices ≥ 5 ms | With a GC inside | Gen 2 | Components | Largest | Without a GC |
| --- | ---: | ---: | ---: | --- | --- | --- |
| reference | 0 | 0 | 0 | none | none | none |
| 1,914 | 1 | 1 | 0 | citizen 1 | 5.8 ms citizen, minute 2,585 (19:05), gen 1 | none |
| 10,000 | 43 | 43 | 1 | citizen 22, food 21 | **42.2 ms citizen, minute 12,654 (18:54, day 8), gen 2** | none |
| 50,000 | 195 | 189 | 3 | citizen 191, posting 1, fill 3 | 29.9 ms citizen, minute 1,802 (06:02), gen 1 | fill 25.2 and 12.0 ms at 00:00; citizen 12.7, 9.7, 5.3 ms; posting 6.7 ms |
| 100,000 | 341 | 336 | 3 | citizen 338, fill 3 | **45.1 ms citizen, minute 1,818 (06:18), gen 1** | fill 28.2, 24.1 and 7.5 ms at 00:00; citizen 7.2 and 6.0 ms |

- **GC-bound units.** At 50,000 and 100,000 the large citizen units cluster in the morning commute (06:00–06:30) and the evening (17:00–19:30), when the most citizen events allocate. The time is the .NET collector walking a heap of 104–333 MB of retained city, not the event's own work: the citizen unit p95 is 0.009 ms at both sizes. Mono's collector behaves differently, so R8 must measure it.
- **Fill units without a GC.** The first boundary's units at 50,000 and 100,000 persons take 12–28 ms with no GC. The plan-latching first unit scans residents, visitors and wallets, and every unit seats commuters against about 2,000 or 4,000 workplaces and merges into 25,000 or 50,000 visitors. At the declared load the same units take at most 0.44 ms without saves (section 3). Under 30 ms, but within a few milliseconds of it at 100,000 persons. Making the fill cheaper at that size would be a perf slice of its own, not F5h's.
- **Planning max.** The planning maxima (8.7 ms at 10,000, 24.4 ms at 50,000, **32.0 ms at 100,000**) are single `ContinuePlanning` calls. The harness does not tag them with GC counts. The double's path work per call is constant, so a GC is the likely cause, but that is not measured.

## 2. Throughput, events, queues, planning and portal routes

| Size | Frames for the day | Frames ÷ unheld | Held frames | Max lag (game min) | Units in the day | Units per game hour max / mean | Events finished through planning | Queue occupancy persons / meals |
| --- | ---: | ---: | ---: | ---: | ---: | --- | ---: | --- |
| reference | 4,401 | 1.53 | 1,520 | 0.5 | 64,271 | 12,596 / 2,678 | 11,715 | 2,552 / 2,552 |
| 1,914 | 4,315 | 1.50 | 1,434 | 0.5 | 34,084 | 5,707 / 1,420 | 10,831 | 2,551 / 2,551 |
| 10,000 | 9,958 | 3.46 | 7,077 | 0.5 | 216,838 | 31,105 / 9,035 | 44,611 | 10,017 / 10,017 |
| 50,000 | 40,615 | 14.10 | 37,733 | 0.5 | 565,241 | 79,842 / 23,552 | 185,459 | 50,000 / 50,000 |
| 100,000 | 74,973 | 26.03 | 72,089 | 0.5 | 1,043,109 | 160,144 / 43,463 | 324,813 | 99,996 / 99,996 |

Due backlog after each frame:

| Size | Peak due backlog (events + meals) | Frames ending with a backlog | Longest drain (frames) | Wallet postings |
| --- | ---: | ---: | ---: | ---: |
| reference | 129 | 1,516 | 9 | 5,999 |
| 1,914 | 641 | 1,432 | 47 | 5,345 |
| 10,000 | 475 | 7,062 | 33 | 24,281 |
| 50,000 | 1,997 | 37,669 | 178 | 122,614 |
| 100,000 | 4,035 | 72,024 | 307 | 247,028 |

- **Frames ÷ unheld** is the day's frames against 2,880, the frames of a native clock that is never held at 60 FPS. The 16-citizen-event cap binds in the busiest hours, and the clock holds there. `HistoricalClockMisses` is 0 at every size.
- **Backlog and holds.** A frame that ends with due work holds the native clock (`AccountingClock.Limit`), so the held frames and the frames with a backlog track each other. The 1,914 row's peak of 641 is its fill boundary: the 637 persons created there have their first events due on the boundary minute.
- **Refusal envelope.** At 50,000 and 100,000 the day takes 14 and 26 times as many frames, and the backlog lasts up to 178 and 307 frames. The plan names these sizes as throughput probes. They do not keep pace under Runtime's 60 FPS caps, and the F1 clock slows the calendar instead of dropping work.
- **Postings.** The posting units posted 5,999 wallets in the day at the declared load, about 2.4 per person. I did not break that down by posting kind.

Planning admissions and portal routes:

| Size | Route jobs | Native paths | Deferred continues | Max paths per frame | Max frames a job waited | Longest run of frames with planning pending | Portal hits / misses |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| reference | 11,718 | 5,753 | 103 | 16 | 1 | 3 | 5,294 / 772 |
| 1,914 | 10,832 | 5,707 | 103 | 16 | 1 | 4 | 3,880 / 711 |
| 10,000 | 44,624 | 28,952 | 700 | 16 | 1 | 10 | 20,476 / 7,096 |
| 50,000 | 199,130 | 218,805 | 9,915 | 16 | 1 | 90 | 46,500 / 48,266 |
| 100,000 | 360,170 | 426,543 | 19,398 | 16 | 1 | 193 | 68,123 / 80,953 |

- Admission never exceeds 16 native paths per frame, and no job waits more than one frame for admission.
- Portal hits and misses come from the cache model, keyed by place pair. With commuters going to their own workplaces there are far more distinct pairs than the cache's 1,024 entries at 50,000 and 100,000, so misses exceed hits there. Runtime keys by exact endpoint positions and also caches station connectors; the native rate is R8's.

## 3. The F1 matrix (event budgets, cadences, hidden skip, reload)

The plan's arms, every unit counted against the event budget. Each arm ran without saves and with saves: one as the clock reaches midnight, and one between two fill units when a frame ended inside the fill. The "Saves" column shows which arms got one save and which got two. All arms of a size end with the same save and order list (state hash), with `HistoricalClockMisses` 0 and at most 16 paths per frame. The ordinary test `DeclaredLoadF1MatrixEqualsTheReferenceAcrossBudgetsCadencesSkipAndReload` asserts the same equality at the declared load on every test run, and the reconciliation against the synchronous replay.

Declared load: day 2 06:00 → day 3 06:00, 1,981 → 2,551 persons, the second boundary creating the last 570 (9 units). State hash `7ab226085da235f3` in every arm. 63,260 units in the day without saves. The runs with saves count 63,283: 23 more, the same with one save or two. I did not trace which events those are; the state is equal either way.

| Arm (FPS / units / ms) | Saves | Frames | Frames ÷ unheld | Held (on the boundary) | Max lag | Frame p50 / p95 / max ms | ≥ 30 ms | Fill unit p50 / max ms | Peak backlog / longest drain |
| --- | ---: | ---: | ---: | --- | ---: | --- | ---: | --- | --- |
| 60 / 128 / 2 (reference) | 0 | 3,201 | 1.11 | 320 (0) | 0.5 | 0.006 / 0.185 / 5.40 | 0 | 0.15 / 0.33 | 575 / 5 |
| 30 / 32 / 4 | 0 | 3,342 | 2.32 | 1,901 (0) | 1.0 | 0.033 / 0.069 / 4.93 | 0 | 0.17 / 0.42 | 575 / 20 |
| 30 / 32 / 4 | 2 | 3,345 | 2.32 | 1,904 (1) | 1.0 | 0.034 / 0.070 / 7.99 | 0 | 0.15 / 6.93 | 575 / 22 |
| 60 / 16 / 2 | 0 | 6,755 | 2.35 | 3,874 (0) | 0.5 | 0.016 / 0.036 / 4.67 | 0 | 0.17 / 0.36 | 575 / 42 |
| 60 / 16 / 2 | 1 | 6,756 | 2.35 | 3,875 (0) | 0.5 | 0.016 / 0.037 / 4.34 | 0 | 0.15 / 0.31 | 575 / 43 |
| 120 / 128 / 2 | 0 | 6,048 | 1.05 | 287 (0) | 0.25 | 0.002 / 0.121 / 5.67 | 0 | 0.17 / 0.44 | 575 / 6 |
| 120 / 128 / 2 | 2 | 6,048 | 1.05 | 287 (1) | 0.25 | 0.001 / 0.122 / 10.83 | 0 | 0.17 / 10.44 | 575 / 7 |
| hidden skip, 32 / 4 | 0 | 3,341 | n/a | 1,900 (0) | 1.0 | 0.033 / 0.070 / 5.76 | 0 | 0.16 / 0.43 | 575 / 20 |
| hidden skip, 32 / 4 | 2 | 3,345 | n/a | 1,904 (1) | 1.0 | 0.034 / 0.070 / 10.63 | 0 | 0.17 / 3.84 | 575 / 23 |

10,000 persons (opt-in): day 7 06:00 → day 8 06:00, 9,483 → 10,017 persons, the seventh boundary creating the last 534 (9 units). State hash `41d61cf55ec2f281` in every arm. 90,047 units without saves, 90,070 with.

| Arm (FPS / units / ms) | Saves | Frames | Frames ÷ unheld | Held (on the boundary) | Max lag | Frame p50 / p95 / max ms | ≥ 30 ms | Fill unit p50 / max ms | Peak backlog / longest drain |
| --- | ---: | ---: | ---: | --- | ---: | --- | ---: | --- | --- |
| 60 / 128 / 2 (reference) | 0 | 3,508 | 1.22 | 627 (1) | 0.5 | 0.019 / 0.303 / 10.25 | 0 | 0.21 / 5.08 | 537 / 10 |
| 30 / 32 / 4 | 0 | 4,307 | 2.99 | 2,866 (0) | 1.0 | 0.054 / 0.088 / 10.25 | 0 | 0.17 / 0.78 | 537 / 26 |
| 30 / 32 / 4 | 2 | 4,309 | 2.99 | 2,868 (1) | 1.0 | 0.054 / 0.086 / 9.70 | 0 | 0.16 / 5.54 | 537 / 28 |
| 60 / 16 / 2 | 0 | 8,757 | 3.04 | 5,876 (1) | 0.5 | 0.026 / 0.042 / 10.07 | 0 | 0.18 / 0.96 | 537 / 54 |
| 60 / 16 / 2 | 2 | 8,768 | 3.04 | 5,887 (1) | 0.5 | 0.027 / 0.045 / **32.95** | **1** | 0.15 / 5.25 | 537 / 56 |
| 120 / 128 / 2 | 0 | 6,338 | 1.10 | 577 (1) | 0.25 | 0.003 / 0.215 / 9.17 | 0 | 0.17 / 0.93 | 537 / 10 |
| 120 / 128 / 2 | 2 | 6,333 | 1.10 | 572 (1) | 0.25 | 0.003 / 0.214 / 9.23 | 0 | 0.19 / 0.93 | 537 / 10 |
| hidden skip, 32 / 4 | 0 | 4,308 | n/a | 2,867 (1) | 1.0 | 0.053 / 0.084 / 9.02 | 0 | 0.17 / 4.89 | 537 / 27 |
| hidden skip, 32 / 4 | 1 | 4,308 | n/a | 2,867 (0) | 1.0 | 0.055 / 0.084 / 10.66 | 0 | 0.18 / 2.35 | 537 / 27 |

- **Flagged ≥ 30 ms.** One frame of 32.9 ms at 10,017 persons, 60 / 16 / 2 with saves: a citizen unit with a gen-2 GC inside it, minute 10,920 (14:00 on day 7). The same arm without saves had none. At the declared load the largest frame was 10.8 ms, a fill unit on the boundary minute right after a reload, with a gen-2 GC inside it.
- **Fill unit cost.** Every arm of a city runs the same nine units. The p50 is 0.15–0.21 ms. The larger maxima (3.8–10.4 ms) are on the boundary minute, mostly right after a reload, where the reloaded city's first unit latches the plan again. Several of them had a GC inside.
- **Peak backlog** is the boundary minute in every arm: the 570 (or 534) persons the fill creates have their first events due there.
- **Hidden skip.** The skip arm requests 120 game minutes per frame, so "frames ÷ unheld" has no meaning there. It runs the same units under the 32-unit cap and ends equal.

## 4. Saved state at the end of each window

Writer 34 (compact). Write is `SaveCodec.Write`, read is `SaveCodec.Read`; p50 of 3 samples. These are save and load latencies and never count as ordinary-play mod-work.

| Size | Persons | Characters (UTF-8 bytes) | Of the 256 Mi-character cap | Records (streaming = semantic) | Retained after read | Write ms p50 | Read ms p50 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| reference | 2,552 | 1,408,244 | 0.52% | 26,523 | 11.4 MB | 151 | 182 |
| 1,914 → 2,551 | 2,551 | 1,086,887 | 0.40% | 16,474 | 10.2 MB | 70 | 131 |
| 10,000 | 10,017 | 6,202,123 | 2.31% | 138,340 | 47.2 MB | 374 | 607 |
| 50,000 | 50,000 | 19,599,841 | 7.30% | 279,584 | 103.5 MB | 1,931 | 3,033 |
| 100,000 | 99,996 | 38,401,347 | 14.31% | 544,771 | 332.8 MB | 4,254 | 6,281 |

Records stay far below the 4,000,000 cap. The 100,000-person city uses 13.6% of it.

## Before and after

Against the earlier F5 slices and against this slice's round 0 (before the review fixes):

| Measure | Earlier slice | F5h round 0 | F5h now |
| --- | --- | --- | --- |
| Declared-load city | F5g measure: 957 / 999 / 576 with catalogue-only businesses | 957 / 999 / 576; no commuter seated (68 saved seats, review Astra-r1-1) | 957 / 1,019 / 576; 1,018 commuters seated at city workplaces |
| Units in a declared-load day | not measured | 32,325 (planned events not counted) | 64,271 (11,715 of them finished through planning) |
| Frames ÷ unheld, declared load, Runtime shape | not measured | 1.29 | 1.53 |
| Frame p95, declared load, Runtime shape | F5g: ≤ 2.14 ms per 128-unit slice | 0.437 ms | 0.388 ms |
| Frame p95, 10,000, Runtime shape | F5g: ≤ 2.41 ms per 128-unit slice | 0.144 ms | 0.174 ms |
| Fill unit at the declared load, ms | F5g: p95 0.27, max 5.20 (budget 1) | p50 0.15–0.17, max 0.23–3.42 | p50 0.15–0.17, max 0.31–10.44 (the 10.44 after a reload, with a gen-2 GC) |
| Fill unit near 50,000 visitors, ms max | F5g capacity arm: 6.92 | 2.81 (50,000) / 7.78 (99,996) | 25.20 (50,000) / 28.22 (99,996), with about 2,000 and 4,000 workplaces |
| Posting unit p95 at 1,914 / 10,000 / 100,000 | F3: 0.0208 / 0.0229 / 0.0111 ms | 0.042 / 0.020 / 0.025 ms | 0.029 / 0.032 / 0.064 ms |
| Save size, declared load | F7d: 849,572 chars (957×2), 1,152,633 (957×3) | 1,083,570 | 1,408,244 |
| Save write p50, declared load | F7d: 131 ms (957×2) on the Windows PC | 137 ms | 151 ms on this Mac |
| Physical selection | F5f: p95 0.0133 / 0.0126 / 0.0123 ms at 1,914 / 10,000 / 50,000 persons with 100 active journeys | not rerun | not rerun: F5h changes no selection code, and visitors stay out of selection until F5i |

The F5g day-slice p95 measured 128 units per slice with no frame time cap. The F5h frames stop at the time cap and the citizen cap, so those columns measure different things.

## Changes with a cost

None in product code. F5h changes words in the panel and the inspector (fixed strings, no new scan) and adds two measurement-only internal counters (`DuePersonEventsAtOrBefore`, `DueMealEventsAtOrBefore`) with no Runtime caller.

## Parked hotspots

Physical selection (F5f took the scan part), closure refresh, optional-stop refresh and save serialization stay parked. F5h took no perf-only rewrite. Two candidates for later perf slices came out of the stress sizes:

- allocation per citizen event, behind the GC-bound units at 50,000 persons and more;
- the fill unit's cost at 50,000 and 100,000 persons with thousands of workplaces.

## Not measured offline

- Whole-frame time, render cost and stalls (R8).
- Mono's collector, which decides whether the GC-bound units above appear in the game (R8).
- Native `NavMesh.CalculatePath` cost and the native walking-cache hit rate for portal legs (R8).
- In-game save pauses at these sizes (a separate, explicitly requested save-profiling run).
- A factor-1 city filled from empty to 50,000 or 100,000 persons. The harness can do it, but each needs 33 to 66 game days of fill. F5g's capacity arm measured the boundary cost at the resident bound, and the factor-2 rows above measure a full day at the size.
