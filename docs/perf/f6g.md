Status: implemented (offline); not verified by rig

Refs #93. Measurements are foreground headless .NET 10 checks on this Mac. Native game/rig work, autosave settings and whole-frame timing were not exercised. Save timing is reported separately from mod-work. The four parked hotspots remain parked.

The reproducible F6g measurements live in `ChangeJournalTests.MeasurementAndHiddenPanel`. Warmup precedes each measured loop; per-call allocation uses the current thread counter, and samples include stopwatch overhead. Observations use an existing input image, so these measure the **added Core consumer**, excluding the preexisting native supply read. Cases cover 1, 64 and 1,024 owned shops, one and eight offers, unchanged supply/hours, changed supply/hours, and the visible panel projection. No regression matrix is run.

Native-read contract: the same 50-read fixture produces 50 reads before and after attachment. `NativeFoodMarket` retains exactly the base's five `FoodSupplyReader.ReadMenu` call sites. The journal block consumes `b.Offers` and the existing supply dictionary; the hours hook consumes the refreshed `Place.WeeklyHours`. It has no native reader or writer. A closed/unverified supply result does not seed stock or staff. Native counter equality and shop-state readback remain in batched rig check 8; this offline evidence is not native proof.

## Cheap wins measured during implementation

| Case | Before | After |
| --- | --- | --- |
| Unchanged one-offer observation, 1 shop | 24 B/call; p95 0.000084 ms | 0 B/call; p95 0.000084 ms |
| Unchanged hours, 1,024 shops, per shop | p95 0.005125 ms, 0 B | Cached per-day counts compare only present windows: p95 0.000334 ms, 0 B |
| Changed one-offer supply, 1 shop | Re-hash the whole hours image: p95 0.021416 ms, 59,850 B/call | Reuse the derived hours hash: p95 0.001083 ms, 1,040 B/call |

Changed hours at 64 shops/eight offers fell from p95 0.030250 ms and 66,458 B/call with decimal-string hashing to 0.005834 ms and 9,008 B/call with binary hashing.

The first win removes an allocating closure before the dictionary lookup's early return. Hours counts and their hash are transient caches reconstructed from the saved exact image. Exact comparison remains authoritative; hash collisions cannot suppress changes. Changed hours hash a bounded binary image, avoiding decimal-string churn. The panel is bounded to 16 shops per page, with Previous/Next controls; city totals and history indexing retain their existing scope. The final panel fixture includes 14 history rows and eight changes per shop and measures allocation of its completed text.

## Final observations

All times below are milliseconds. Supply/hours rows are per shop observation; panel rows include full history indexing, the F6c/F6g text for at most 16 visible shops, and its final string. Each shop has 14 daily rows and eight journal entries in that panel fixture. The native account reader, Unity layout and whole-frame cost are excluded. Changed-observation averages include some already-current images while cycling the larger shop sets.

| Work / shops | Samples | p50 | p95 | Max | B/call | ≥30 ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| supply 1 | 2000 | 0.000083 | 0.000084 | 0.000125 | 0.0 | 0 |
| hours 1 | 2000 | 0.000209 | 0.000250 | 0.003833 | 0.0 | 0 |
| changed supply 1 | 2000 | 0.000875 | 0.001083 | 0.014083 | 1040.0 | 0 |
| supply8 1 | 2000 | 0.000334 | 0.000375 | 0.003250 | 0.0 | 0 |
| changed hours 1 | 100 | 0.005333 | 0.005584 | 0.007125 | 9008.0 | 0 |
| panel 1 | 100 | 0.018250 | 0.019583 | 0.075958 | 22080.0 | 0 |
| supply 64 | 2000 | 0.000083 | 0.000125 | 0.000167 | 0.0 | 0 |
| hours 64 | 2000 | 0.000250 | 0.000291 | 0.012958 | 0.0 | 0 |
| changed supply 64 | 2000 | 0.000917 | 0.001125 | 0.004125 | 1053.6 | 0 |
| supply8 64 | 2000 | 0.000334 | 0.000375 | 0.000500 | 0.0 | 0 |
| changed hours 64 | 128 | 0.005458 | 0.005834 | 0.008250 | 9008.0 | 0 |
| panel 64 | 100 | 0.417875 | 0.886792 | 3.419292 | 335136.2 | 0 |
| supply 1024 | 2000 | 0.000083 | 0.000125 | 0.000167 | 0.0 | 0 |
| hours 1024 | 2000 | 0.000250 | 0.000334 | 0.020375 | 0.0 | 0 |
| changed supply 1024 | 4096 | 0.000916 | 0.001250 | 1.268042 | 851.4 | 0 |
| supply8 1024 | 2000 | 0.000333 | 0.000375 | 0.000500 | 0.0 | 0 |
| changed hours 1024 | 2048 | 0.005417 | 0.008333 | 2.452667 | 9163.5 | 0 |
| panel 1024 | 100 | 1.847000 | 2.557291 | 5.214542 | 618541.6 | 0 |

Hidden panel: **0 projection calls, 0 B**, checked with the incremental request counter and the existing open-panel/three-second call-site guard. The final non-save p95 values are below 5 ms; the panel's 5.214542 ms maximum is retained above. No final sample reached 30 ms. Whole-frame timing and the full native mod-work union remain separate batched rig evidence.

The pre-pagination prototype built all 1,024 journals, with empty history and about four changes per shop: p95 3.074542 ms and 3,493,697 B/call. The final, heavier history fixture above indexes all 1,024 shops but renders only 16, with eight changes each: p95 2.557291 ms and 618,542 B/call. These are different fixture shapes; the page bound is the performance decision. The panel also reuses the journal's existing dictionary and compares journal entry indices without boxing value types. Page changes retain the existing refresh cadence and add no native account refresh.

## Save timing (separate)

Fresh writer-33 cities only; no game save or migration. The small fixture has one resident. The observed fixture has 75 residents and 150 visitors, advanced through its first day, with price/stock/staff/hours journal entries and nonzero meal-period counters.

| Work | Samples | p50 | p95 | Max | B/call | ≥30 ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| save write fresh | 50 | 0.387416 | 0.460958 | 0.479542 | 44120.0 | 0 |
| save read fresh | 50 | 0.644917 | 0.820250 | 0.935041 | 79992.0 | 0 |
| save write observed fixture | 20 | 3.713292 | 9.648750 | 9.648750 | 2490637.6 | 0 |
| save read observed fixture | 20 | 13.599083 | 14.644625 | 14.644625 | 4833642.0 | 0 |

The observed seed-5150 fixture is 201,191 characters and 858 records by both the streaming and semantic counters. Encoding the same fixture with the frozen attendance writer is 198,376 characters: **+2,815** for the new exact image and observed stock-out marker. Seed 9217 is 201,021 characters. Both current-writer hashes are pinned in [ChangeJournalTests](../../tests/ChangeJournalTests.cs); the F6d/F6e predecessor pins remain unchanged.

The added exact-image extension is bounded to 2,916 ASCII characters per observed shop at eight offers (20 state characters, eight 22-character product/price/stock cells, 896 three-character hours edges, quotes and separators), or 2,985,984 at 1,024 shops. The new `StockoutObserved` property adds at most 25 characters per daily row, or 358,400 over 14 × 1,024 rows. This is a component bound, not a remeasurement of the saturated CF32 composition. The existing input/record caps and refusal behavior remain in force.

## Validation

- Required build with the supplied managed-assembly shim: passed.
- Required foreground `dotnet test tests/PersistentCity.Tests.csproj`: **3,821 passed**, 0 failed, 0 skipped (3 min 41 s).
- Foreground F6g checks and final timing batch: **19 passed**. No other testhost was active when launched.
- `git diff --check`: passed. No game, rig, ModsLocal, bridge or regression matrix run.
