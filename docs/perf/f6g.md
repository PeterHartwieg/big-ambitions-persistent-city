Status: implemented (offline); not verified by rig

Refs #93. Measurements are foreground headless .NET 10 checks on this Mac. Native game/rig work, autosave settings and whole-frame timing were not exercised. Save timing is reported separately from mod-work. The four parked hotspots remain parked.

The reproducible F6g measurements live in `ChangeJournalTests.MeasurementAndHiddenPanel`. Warmup precedes each measured loop; per-call allocation uses the current thread counter, and samples include stopwatch overhead. Observations use an existing input image, so these measure the **added Core consumer**, excluding the preexisting native supply read. Cases cover 1, 64 and 1,024 owned shops, one and eight offers, unchanged supply/hours, changed supply/hours, and the visible panel projection. No regression matrix is run.

Native-read contract: a matched three-day CitySimulator fixture counts IFoodMarket reads with and without the journal while performing at least eight actual FoodCommerce checkouts; both read and sale counts match. The separate source-structure guard is explicitly labeled, rather than presented as a native counter experiment. `NativeFoodMarket` retains exactly the base's five `FoodSupplyReader.ReadMenu` call sites. The journal block consumes `b.Offers` and the existing supply dictionary; the hours hook consumes the refreshed `Place.WeeklyHours`. It has no native reader or writer. A closed/unverified supply result still observes validated prices and preserves stock/staff evidence. Same-minute live captures update the existing cache handle without native reads. Stock comparisons use availability, so ordinary sales above zero do not churn the journal. Native counter equality and shop-state readback remain in batched rig check 8; this offline evidence is not native proof.

## Cheap wins measured during implementation

| Case | Before | After |
| --- | --- | --- |
| Unchanged one-offer observation, 1 shop | 24 B/call; p95 0.000084 ms | 0 B/call; p95 0.000084 ms |
| Unchanged hours, 1,024 shops, per shop | p95 0.005125 ms, 0 B | Cached per-day counts compare only present windows: p95 0.000334 ms, 0 B |
| Changed one-offer supply, 1 shop | Re-hash the whole hours image: p95 0.021416 ms, 59,850 B/call | Reuse the derived hours hash: p95 0.001083 ms, 1,040 B/call |

Changed hours at 64 shops/eight offers fell from p95 0.030250 ms and 66,458 B/call with decimal-string hashing to 0.005834 ms and 9,008 B/call with binary hashing.

The first win removes an allocating closure before the dictionary lookup's early return. Hours counts and their hash are transient caches reconstructed from the saved exact image. Exact comparison remains authoritative; hash collisions cannot suppress changes. Changed hours hash a bounded binary image, avoiding decimal-string churn. The panel is bounded to 16 shops per page, with Previous/Next controls; city totals and history indexing retain their existing scope. The final panel fixture includes 14 history rows and eight changes per shop and measures allocation of its completed text.

## Review-fix observations

All times below are milliseconds. Supply/hours rows are per shop observation; panel rows include full history indexing, the F6c/F6g text for at most 16 visible shops, and its final string. Each shop has 14 daily rows and eight journal entries in that panel fixture. The native account reader, Unity layout and whole-frame cost are excluded. Changed-observation averages include some already-current images while cycling the larger shop sets.

| Work / shops | Samples | p50 | p95 | Max | B/call | ≥30 ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| supply 1 | 2000 | 0.000084 | 0.000125 | 0.051334 | 0.0 | 0 |
| hours 1 | 2000 | 0.000208 | 0.000209 | 0.014417 | 0.0 | 0 |
| changed supply 1 | 2000 | 0.000958 | 0.001166 | 0.838042 | 1040.1 | 0 |
| supply8 1 | 2000 | 0.000791 | 0.000875 | 0.001084 | 0.0 | 0 |
| changed hours 1 | 100 | 0.005708 | 0.005875 | 0.006084 | 8984.0 | 0 |
| panel 1 | 100 | 0.022000 | 0.033667 | 0.810584 | 22048.0 | 0 |
| supply 64 | 2000 | 0.000084 | 0.000125 | 0.000167 | 0.0 | 0 |
| hours 64 | 2000 | 0.000208 | 0.000250 | 0.017500 | 0.0 | 0 |
| changed supply 64 | 2000 | 0.001042 | 0.001333 | 0.032584 | 1053.6 | 0 |
| supply8 64 | 2000 | 0.000750 | 0.000792 | 0.017791 | 0.0 | 0 |
| changed hours 64 | 128 | 0.005875 | 0.006083 | 0.006250 | 8984.0 | 0 |
| panel 64 | 100 | 0.414416 | 0.706292 | 3.070208 | 334624.1 | 0 |
| supply 1024 | 2000 | 0.000084 | 0.000125 | 0.011833 | 0.0 | 0 |
| hours 1024 | 2000 | 0.000250 | 0.000292 | 0.012834 | 0.0 | 0 |
| changed supply 1024 | 4096 | 0.001042 | 0.001542 | 0.036792 | 851.4 | 0 |
| supply8 1024 | 2000 | 0.000791 | 0.000917 | 0.017542 | 0.0 | 0 |
| changed hours 1024 | 2048 | 0.005834 | 0.008916 | 2.251792 | 9139.5 | 0 |
| panel 1024 | 100 | 1.766208 | 2.357084 | 3.194833 | 618536.0 | 0 |
| cache live replacement | 2000 | 0.000000 | 0.000042 | 0.000042 | 0.0 | 0 |
| cache image creation | 2000 | 0.000041 | 0.000042 | 0.000625 | 24.0 | 0 |

Cache freshness adds one 24 B handle per decision-cache miss, independent of city size (p95 0.000042 ms); replacing its image after a live capture is 0 B (p95 0.000042 ms). These native-free seam measurements are separate from the Core consumer rows and include no native supply read. The handle creation allocation is not a steady-state comparison allocation.

Hidden panel: **0 projection calls, 0 B**, checked by executing the same refresh gate used by the live panel over 10,000 hidden frames, with unchanged BuildEconomyText calls, build count and FoodMoney.Requests. The final non-save p95 values are below 5 ms; the panel's 3.194833 ms maximum is retained above. No final sample reached 30 ms. Whole-frame timing and the full native mod-work union remain separate batched rig evidence.

The pre-pagination prototype built all 1,024 journals, with empty history and about four changes per shop: p95 3.074542 ms and 3,493,697 B/call. The final, heavier history fixture above indexes all 1,024 shops but renders only 16, with eight changes each: p95 2.357084 ms and 618,536 B/call. These are different fixture shapes; the page bound is the performance decision. The panel also reuses the journal's existing dictionary and compares journal entry indices without boxing value types. Page changes invalidate the three-second text gate and rebuild once immediately, using the existing native-account refresh policy.

## Save timing (separate)

Fresh writer-33 cities only; no game save or migration. The small fixture has one resident. The observed fixture has 75 residents and 150 visitors, advanced through its first day, with price/stock/staff/hours journal entries and nonzero meal-period counters.

| Work | Samples | p50 | p95 | Max | B/call | ≥30 ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| save write fresh | 50 | 0.070583 | 0.081000 | 0.083791 | 44160.0 | 0 |
| save read fresh | 50 | 0.369166 | 0.521125 | 0.987250 | 80089.1 | 0 |
| save write observed fixture | 20 | 3.759208 | 4.747792 | 4.747792 | 2407200.8 | 0 |
| save read observed fixture | 20 | 14.005375 | 14.840833 | 14.840833 | 4779293.2 | 0 |

The observed seed-5150 fixture is 198,523 characters and 858 records by both the streaming and semantic counters. Encoding the same fixture with the frozen attendance writer is 198,376 characters: **+147** for the new exact image and observed stock-out marker. Seed 9217 is 198,353 characters. Both current-writer hashes are pinned in [ChangeJournalTests](../../tests/ChangeJournalTests.cs); the F6d/F6e predecessor pins remain unchanged.

The hours image now saves seven two-character weekday counts followed by six characters per present window: **14 + 6W**, bounded at 64 total windows. Unobserved hours remain empty. A typical seven-window week is **56 characters**, down from the dense **2,688** (−97.9%); the 64-window bound is **398** (−85.2%). Decoding expands to the same canonical array and still refuses unordered windows, malformed counts and trailing cells.

The complete added exact-image extension (state, offer cells, sparse hours, quotes and separators; excluding F6d's existing header/journal) is **109 characters/shop** for one offer and seven windows, or **111,616 at 1,024 shops**. At eight offers and 64 windows it is **626/shop**, or **641,024/city**, down from **2,916/shop / 2,985,984/city**. These extensions alone remain above section 5's **32/shop / 32,768/city** last-observed-state budget: +77/shop (+78,848/city) for the typical case and +594/shop (+608,256/city) at the bound. Exact before/after evidence exceeds the hash-only budget; sparse encoding substantially reduces that overrun but does not erase it. Including the existing reference/fingerprint header, empty journal list and outer framing adds 30/shop: **139/shop / 142,336/city** typical and **656/shop / 671,744/city** at the bound, before journal entries (budget overruns of 109,568 and 638,976/city respectively). These are component bounds, not a remeasurement of the saturated CF32 composition or a claim that its headroom fits.

The new `StockoutObserved` property adds at most 25 characters per daily row, or 358,400 over 14 × 1,024 rows. The existing input/record caps and refusal behavior remain in force. Both current-writer pins were re-pinned for this review's sparse representation and availability baseline in the same unmerged F6g bump; no second accepted format or migration was added.

## Validation

- Required build with the supplied managed-assembly shim: passed.
- Required foreground `dotnet test tests/PersistentCity.Tests.csproj`: **3,828 passed**, 0 failed, 0 skipped (4 min 1 s).
- Foreground F6g checks and final timing batch: **40 passed**, including F6g and existing F6c/panel guards. No other testhost was active when launched.
- `git diff --check`: passed. No game, rig, ModsLocal, bridge or regression matrix run.

## Round-2 staffing review

Opus-r2-1 retains the exact staffed-counter contract. Boolean or daily-maximum aggregation would suppress a real lunch reduction from two counters to one. This review changes documentation only; runtime behavior, writer-33 golden pins, save sizes and measured costs above are unchanged. The existing staff-variation/reload and counted oldest-first eviction checks cover the contract. Native open-hour membership variation is explicitly included in batched rig check 4, rather than asserted from offline fixtures.

Round-2 validation: required build passed with the same DLL SHA256 (`2d552b68a214f9af905c5bd0b7e473ef6e553cc4545a3ca263fc27c8cbe1e20f`); required foreground unit suite passed **3,828 tests**, 0 failed, 0 skipped (3 min 51 s). The other checkout's testhost finished before this build/test sequence started. `git diff --check` passed. No game, rig or regression matrix run.
