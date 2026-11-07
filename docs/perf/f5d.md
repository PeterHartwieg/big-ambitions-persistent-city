# F5d — independent schedules (writer 27): performance, 7 November 2026

Refs #92. Offline .NET 10 measurements on this Windows workstation, Release builds, foreground, one dotnet process at a time after the shared other-dotnet wait. They are synthetic, not game, Mono or rig evidence. Save and load timing is its own criterion, separate from ordinary-play mod-work. Whole-frame timing is not measured offline and is not claimed. The four parked hotspots (physical population, closure refresh, optional-stop refresh, save serialization) were not touched.

## Gate summary

- **Mod-work proxies stay under p95 5 ms at the reference load.** The F1 persons variant, the integrated scheduler replay and posting units all have p95 ≤ 0.34 ms at 1,914–10,000 persons.
- **Flagged: ≥ 30 ms intervals at the 100,000-person stress size** in the integrated replay: 132 intervals, max 77.3 ms. The F3 base already had 39–144 at that size (max 54–180 ms). It is a stress probe (50,000 residents × 2), not the declared load, and has no native evidence. No other measured mod-work interval reaches 30 ms.
- **Save/load samples are all ≥ 30 ms** (save criterion, flagged as in F5c-lite/F7a). They are not ordinary-play work.

## Events per game hour and peak queue length (`tests/F5dMeasure.cs`)

One natural day of the F7a representative fixture (`EconomyScenario`, household finances, two portals), at factor K and at K = 1 (residents only), frame slices of 64 units. Peak queue lengths are sampled after every slice.

| Fresh city | Persons | Day events (K → K=1) | Max events / game hour (K → K=1) | Mean / hour | Peak queued person events | Peak queued meal events |
| --- | ---: | --- | --- | ---: | ---: | ---: |
| 957 × 2 | 1,914 | 48,814 → 25,456 | 7,936 → 3,328 | 2,441 | 1,914 | 1,914 |
| 957 × 3 | 2,871 | 64,992 → 25,456 | 10,240 → 3,328 | 3,250 | 2,871 | 2,871 |
| 5,000 × 2 | 10,000 | 255,823 → 133,428 | 43,456 → 17,728 | 12,791 | 10,000 | 10,000 |
| 5,000 × 3 | 15,000 | 341,213 → 133,428 | 55,744 → 17,728 | 17,061 | 15,000 | 15,000 |
| 50,000 × 2 | 100,000 | 2,038,158 → 1,202,935 | 301,120 → 152,256 | 101,908 | 100,000 | 100,000 |
| 50,000 × 3 | 150,000 | 2,623,090 → 1,202,935 | 362,112 → 152,256 | 131,155 | 150,000 | 150,000 |

Each person has exactly one queued routine event and one queued meal event, so the queues grow linearly with persons and never above them. Visitors roughly double the event count at K = 2; the per-hour peak (morning commute) grows 2.4×, because visitors also travel to and from their portal.

## Route admissions and backlog (`tools/road-ceiling-regression`, peak-planning throughput, persons variant)

957 commute requests (residents) or 1,914 (residents + one visitor each, visitors without a car), released at once, ≤ 16 path queries per frame, synthetic 0.1 ms per native path query, 2 ms soft frame budget:

| Variant | FPS | Frames | Drain seconds | Plans/s | Max queries/frame | Max queries/plan | Final backlog |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| residents | 30 | 488 | 16.3 | 58.8 | 16 | 8 | 0 |
| residents | 60 | 480 | 8.0 | 119.6 | 16 | 8 | 0 |
| persons | 30 | 780 | 26.0 | 73.6 | 16 | 8 | 0 |
| persons | 60 | 779 | 13.0 | 147.4 | 16 | 8 | 0 |

The doubled peak drains within the 90-second window that a 30-game-minute commute peak allows (the tool asserts it), and admission never exceeds 16 queries per frame. In game, visitor commutes also reuse previous durations (8–14-hour preparation lead), as residents' do.

## Posting units (`tools/bounded-bulk-regression`, persons)

Seven days after a discarded warm day, ≤ 32 postings per unit, one wallet per person (residents and visitors, each with its own income source):

| Persons | Calls | Call ms median / p95 / max | Work per call | Allocation per call | Synthetic frame ms median / p95 / max | Day work ms |
| ---: | ---: | --- | --- | --- | --- | ---: |
| 1,914 | 420 | 0.0246 / 0.0406 / 0.0565 | 26–32 | 0 B | 0.396 / 0.602 / 1.202 | 1.72 |
| 10,000 | 2,191 | 0.0276 / 0.0501 / 0.1038 | 16–32 | 0 B | 0.466 / 0.758 / 0.909 | 10.18 |
| 100,000 | 21,875 | 0.0091 / 0.0193 / 0.1844 | 32 | 0 B | 0.150 / 0.278 / 0.577 | 32.83 |

F3's bound holds per person. No posting call reaches 30 ms.

## Integrated scheduler replay (`tools/bounded-bulk-regression --integrated`)

Actual Core scheduler, 05:00 through the meal peaks to the next midnight, ≤ 16 units / 2 ms per synthetic frame, cold, fixed backlog drain. These figures are after the perf wins below; the "before" run is in the wins section.

| Persons | Frames | Events | Frame advance ms median / p95 / max | ≥ 30 ms | Allocated total | F3 base p95 / max (1,914 consumers etc.) |
| ---: | ---: | ---: | --- | ---: | ---: | --- |
| 1,914 | 3,062 | 48,814 | 0.0551 / 0.1431 / 19.69 | 0 | 91.2 MB | 0.160 / 20.84 |
| 10,000 | 16,009 | 255,823 | 0.0513 / 0.2323 / 24.26 | 0 | 472.7 MB | 0.225 / 12.16 |
| 100,000 | 127,531 | 2,038,158 | 0.0465 / 0.1331 / **77.30** | **132** | 4,086 MB | 0.156 / 54.29 (49 ≥ 30 ms) |

**Flagged:** 132 intervals ≥ 30 ms (max 77.3 ms) at 100,000 persons. This is a stress size and a cold replay with 4 GB of allocation, and the base already had such intervals at this size. The declared reference load (1,914–2,871 persons) has none. The 10,000 maximum (24.3 ms) is higher than F3's run (12.2 ms) but below 30 ms; single maxima vary by run on this workstation (F3 recorded 34.2 ms at 10,000 in an exploratory run).

## F1 accounting-backlog harness, persons variant (`tools/accounting-backlog-regression`)

957 residents + 957 visitors through one logical day. Every arm gives the same save bytes and native orders as the 60 FPS / 128-unit reference and conserves cash. Detached frame cost (no Unity):

| FPS | Units | ms budget | Skip | Frames | Max lag (game min) | Purchases (visitors) | Frame p50 / p95 / max ms | ≥ 30 ms |
| ---: | ---: | ---: | --- | ---: | ---: | --- | --- | ---: |
| 30 | 1 | 0.01 | no | 36,142 | 1.000 | 881 (327) | 0.0039 / 0.0148 / 21.49 | 0 |
| 60 | 16 | 0.5 | no | 4,853 | 0.500 | 881 (327) | 0.0211 / 0.1297 / 15.41 | 0 |
| 120 | 128 | 2 | no | 6,058 | 0.250 | 881 (327) | 0.0002 / 0.2010 / 20.55 | 0 |
| 30 | 1 | 0.01 | yes | 36,142 | 1.000 | 881 (327) | 0.0033 / 0.0121 / 15.32 | 0 |

The residents-only arms of the same tool are unchanged (647 purchases, 649 orders). The reload arm (an in-flight held or travelling meal, then midnight) also matched. `HistoricalClockMisses` is 0 in every arm (asserted). The 24 + 24 version of this variant is a unit test (`Schema27Tests.AccountingHarnessPersonsVariantIsEqualAcrossArms`).

## Saved state: bytes, records, memory and latency (`tests/F5dMeasure.cs`)

The same fixture at 00:30 on day 3, when every visitor is off-map. Writer-26 figures are F5c-lite's re-measured same-fixture numbers (represented consumers on their host's day). Behaviour differs between the two, so they are a reference, not an identical-state comparison.

| Fresh city | Persons | Writer 27 chars | % of cap | Writer 26 chars (F5c-lite) | Residents-only (K=1) chars | Records (semantic) | Retained MiB | Retained per visitor vs K=1 | vs writer 26 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 957 × 2 | 1,914 | 2,311,264 | 0.86 | 2,373,033 | 1,666,048 | 13,225 | 4.61 | 2,195 B | +0.37 MiB |
| 957 × 3 | 2,871 | 2,900,460 | 1.08 | 2,972,009 | 1,666,048 | 16,096 | 6.41 | 2,082 B | +0.62 MiB |
| 5,000 × 2 | 10,000 | 12,077,279 | 4.50 | 12,400,121 | 8,706,215 | 69,278 | 24.17 | 2,180 B | +1.75 MiB |
| 5,000 × 3 | 15,000 | 15,157,064 | 5.65 | 15,525,641 | 8,706,215 | 84,278 | 33.54 | 2,073 B | +3.25 MiB |
| 50,000 × 2 | 100,000 | 109,878,640 | 40.93 | 112,094,080 | 78,875,799 | 648,932 | 218.28 | 2,010 B | +20.8 MiB |
| 50,000 × 3 | 150,000 | 139,194,956 | 51.85 | 139,504,162 | 78,875,799 | 794,835 | 307.94 | 1,945 B | +42.5 MiB |

Against plan section 5:

- **Visitor ≤ 256 characters off-map or idle, arrival tuple included:** a visitor's tuple plus its routine object is at most 141–145 characters while dormant (mean 136–139). Active visitors at three daytime instants (08:20, 12:00, 17:40) on the 957 × 2 city: an active visitor without a journey is at most 204 characters (mean 159, 1,352 samples), inside 256. A travelling one is at most 350 (mean 291, 290 samples), because its route legs (charged as records, like a resident's journey) are included. Dormant samples in the same run: at most 141. A travelling visitor adds its route legs, charged like a resident's.
- **Records:** one per person tuple and per station, plus the route legs of travelling visitors. The streaming count is semantic + 3 at every size. Those three are the fixture's scope menu strings, a streaming-only charge that writer 26 already had; `Schema27Tests.StreamingAndSemanticRecordCountersAgree` checks the counters are equal on fixtures without menus. Every size is far below 4,000,000.
- **Cap:** the 100,000-person city uses 40.9 % and the 150,000-person city 51.9 % of the 256 Mi-character cap, inside the 85 % target. Writer 27 is smaller than writer 26 on the same fixture (−0.2 % to −2.6 %), because writer 26 stored represented routine stops and multiplied host-day state.
- **Retained memory ≤ 1 KiB per visitor:** each visitor now owns a full scheduled routine. Compared with writer 26's represented visitor, that adds about 0.4–0.45 KiB per visitor (+0.37 MiB per 957 visitors; +20.8 MiB per 50,000; +42.5 MiB per 100,000), inside the budget. The whole visitor, including its roster meal, wallet and memory that writer 26 already held, is about 1.9–2.2 KiB.

Write and read p50 / p95 / max in ms (5 samples after a warm-up):

| Fresh city | Write 27 | Read 27 | Write 26 (F5c-lite) | Read 26 (F5c-lite) |
| --- | --- | --- | --- | --- |
| 957 × 2 | 165.8 / 228.7 / 228.7 | 461.2 / 555.3 / 555.3 | 51.6 / 74.3 / 74.3 | 151.1 / 198.0 / 198.0 |
| 957 × 3 | 64.9 / 86.7 / 86.7 | 178.1 / 217.9 / 217.9 | 70.3 / 106.9 / 106.9 | 165.4 / 182.2 / 182.2 |
| 5,000 × 2 | 184.3 / 234.4 / 234.4 | 574.6 / 717.1 / 717.1 | 303.7 / 321.3 / 321.3 | 758.7 / 922.9 / 922.9 |
| 5,000 × 3 | 266.0 / 463.0 / 463.0 | 748.4 / 928.0 / 928.0 | 425.5 / 547.6 / 547.6 | 1,033.1 / 1,148.3 / 1,148.3 |
| 50,000 × 2 | 1,785 / 1,981 / 1,981 | 4,233 / 5,032 / 5,032 | 2,369 / 2,533 / 2,533 | 5,695 / 6,263 / 6,263 |
| 50,000 × 3 | 1,850 / 2,712 / 2,712 | 4,995 / 5,736 / 5,736 | 2,905 / 4,235 / 4,235 | 8,005 / 8,886 / 8,886 |

The 957 × 2 row is the first fixture of the process and includes its first-use JIT and cache cost. 957 × 3, run next on a larger city, is faster. From 5,000 residents up, writer 27 writes and reads faster than F5c-lite's writer 26 on this fixture. **Flagged:** every save and load sample is ≥ 30 ms (save criterion, not mod-work; saves run in the native save callback, reads off the main thread).

## Ordinary-play cost added by F5d

- One routine event and one meal event per visitor, in the same bounded units and F1 epoch as residents (above).
- `VisitorSchedules.Sync` after each visitor event: a handful of field stores. It allocates only when Employment changes.
- One pass over visitors in `PrepareForSave` (save path only).
- No per-frame scan, no reflection and no new native call on the frame path. The station read happens once, when a city's first roster is created (initialization, not ordinary play).

## Initialization: fresh-roster visitor creation (review Opus-r1-1)

A fresh city creates its visitors once, when its first roster is enrolled: `VisitorSchedules.Create`, `CitySimulator.ScheduleVisitors` and `Enrol`, inside `AttachFoodMarket`. Runtime calls that from `CompleteInitialization` on the main thread. This is initialization cost, not ordinary play, but it is a main-thread stall, so it is measured here. `F5dMeasure.VisitorCreationCostAtInitialization` uses the representative fixture padded with 2,000 zero-capacity homes (2,013 places, a game-sized catalogue). Each figure is the median of 3 attaches at factor K minus 3 at K = 1 (the same residents without visitors).

| Fresh city | Visitors | Before ms | After ms | Attach total after (K=1) ms |
| --- | ---: | ---: | ---: | --- |
| 957 × 2 | 957 | 60.4 | 11.4 | 13.8 (2.4) |
| 957 × 3 | 1,914 | 104.0 | 29.6 | 32.2 (2.6) |
| 5,000 × 2 | 5,000 | 273.8 | 48.1 | 64.5 (16.4) |
| 5,000 × 3 | 10,000 | 176.1 | 110.8 | 134.1 (23.3) |
| 50,000 × 2 | 50,000 | 858.5 | 299.2 | 452.0 (152.8) |
| 50,000 × 3 | 100,000 | 1,739.1 | 629.4 | 785.7 (156.3) |

**Win taken:** `Create` builds the Work, Study, Shop and Leisure candidate lists once per call, in catalogue order, instead of scanning every place three times per visitor. `DestinationChoice` applies the same kind-and-capacity filter, so every choice is unchanged; the writer-27 golden pins hold. The 5,000 × 2 "before" figure (above 5,000 × 3) is a single-run outlier on this workstation.

**Flagged:** the one-time attach stall is still ≥ 30 ms from 957 × 3 (32 ms total) and reaches 0.79 s at 50,000 × 3. It happens once per new city, during initialization while the native clock is held, and never in ordinary play or on a later load (a saved city creates no visitors). The reference city (957 × 2) is 13.8 ms. Moving creation off the main thread, next to the resident creation in `Task.Run`, would remove it. That needs the station read before the worker starts, which is a Runtime ordering change outside this fix; it is left for a later slice.

## Perf wins taken in code this slice touches

1. **Shared arrival record.** A visitor's `LastArrival` reuses the arrival record its routine already holds, instead of copying it per arrival.
2. **No Employment allocation per visitor posting.** Income selection reads the visitor's synced Employment instead of building one per daily posting.

Measured together with `--integrated` before and after, with identical save SHA-256 at every size (behaviour unchanged):

| Persons | Allocated per day before → after | Saved |
| ---: | --- | ---: |
| 1,914 | 91,479,008 → 91,225,104 B | −253,904 B (−0.28 %) |
| 10,000 | 473,717,704 → 472,663,008 B | −1,054,696 B (−0.22 %) |
| 100,000 | 4,097,354,792 → 4,086,257,120 B | −11,097,672 B (−0.27 %) |

Frame-advance timing differences between the two runs are within run-to-run noise and are not claimed as a gain.

## Commands

- `PC_F5D_MEASURE=<absolute file> PC_F5D_SAMPLES=5 dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F5dMeasure`
- `dotnet run --project tools/bounded-bulk-regression/BoundedBulkRegression.csproj -c Release [-- --integrated]`
- `dotnet run --project tools/road-ceiling-regression/RoadCeilingRegression.csproj -c Release`
- `dotnet run --project tools/accounting-backlog-regression/AccountingBacklogRegression.csproj -c Release`
