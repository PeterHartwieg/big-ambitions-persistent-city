# F9e perf: read-only city views, 9 November 2026

Offline component cost on a Mac (Apple M1 Pro), .NET 10 Debug test build. These are not in-game mod-work numbers. Mod-work p95 < 5 ms, every mod-work spike of 30 ms or more and whole-frame timing come from the batched rig ([checks](../checks/f9e.md#in-game-checks), check 6). #128 stays a separate investigation. The four parked hotspots stay parked; F9e touches none of them.

## Method

The F5h reference city (957 residents, 255 places, 3 shops) at day 2 13:00. One scratch harness, run on an export of base `9cb1490` (the base code's `State` reads, as the base Runtime files make them) and on this head (`View` reads, as the F9e Runtime files make them). The panel and console commands themselves need Unity, so the harness times the reads and Core calls each one makes. Per measurement: three warm-up calls, then seven batches; median and minimum per call, and bytes allocated per call on the test thread. Two runs of each build, alternated base, head, base, head in separate processes. The harness is not committed.

| Work | What it times |
| --- | --- |
| Panel reads | One `BuildEconomyText` refresh's city reads: headline counts, shop-to-place lookup, history index, recording days, money rescan and the city economy lines. Shop text formatting, which F9e does not change, is left out. |
| Person command | `pc population person <id>` for every resident (957 calls). |
| Census capture | The main-thread resident capture of `pc population census`. |
| Inspector | `CitySimulator.Inspect(id).Text()` for every resident (957 popup builds). |

## Before and after

Median per call, two runs each (run 1 / run 2):

| Work | Base `9cb1490` | This head | Allocated per call (both builds) |
| --- | ---: | ---: | ---: |
| Panel reads | 0.274 / 0.274 ms | 0.275 / 0.276 ms | 2,848 B |
| Person command × 957 | 9.52 / 9.49 ms | 9.06 / 9.24 ms | 2,669,825 B |
| Census capture | 0.019 / 0.018 ms | 0.019 / 0.018 ms | 22,992 B |
| Inspector × 957 | 12.13 / 11.85 ms | 13.31 / 13.46 ms | 5,762,669 B |

- **Panel, person command, census:** neutral. Allocation is identical byte for byte.
- **Inspector:** about 1.4 ms more over 957 builds, so about 1.4 µs per popup build (12.5 µs to 13.9 µs). The household count now loops over `ResidentList` through the covariant interface, which costs one variant interface dispatch per resident. The popup builds on click and refreshes once per second while open, so the in-game cost is about 1.4 µs per second with a popup open. I left it rather than add a cast-back fast path in Core, which the view is meant to avoid.
- **Allocation per panel refresh:** the view reads themselves allocate nothing. `PanelRefreshReadsThroughTheViewAllocateNothing` measures 0 bytes over 1,000 refreshes of the panel's view reads. The panel walks the view's lists with index loops, because a `foreach` over an `IReadOnlyList` would box an enumerator per refresh. The 2,848 B above are the history index and money rescan, the same in both builds.
- **`CitySimulator.View`** allocates the view and its two inner views once per simulator, on first use.

## Review round 1: detached copies

The observer paths that used to receive the city's objects now get copies. Same Mac, one run, median per call:

| Work | F9c city-s (3 orders, 7 history rows) | F5h (0 orders, 6 history rows) |
| --- | ---: | ---: |
| Research ledger copy (`ResearchSnapshot`) | 0.0003 ms, 1,016 B | 0.0001 ms, 608 B |
| Economy overview, base (`FoodEconomyInsight.Capture`) | 0.168 ms, 97 KB | 1.35 ms, 867 KB |
| Economy overview, detached (`EconomyOverview`) | 0.258 ms, 126 KB | 1.38 ms, 894 KB |
| Vendor-place meal copies (`VendorMeals`, 0 meals in both) | 0.0015 ms | 0.012 ms |
| Rejected option: decode the published payload for save-cost counts | 29.5 ms, 10 MB | 221 ms, 72 MB |

- The research window takes a new ledger copy at most every 0.25 s while it is open and nothing while it is closed. Console commands take one per command.
- The overview and vendor sales are on-demand bridge and console diagnostics, not frame work.
- The save-cost decode would have been a main-thread spike of 221 ms in an explicitly requested save-profiling run. The save owner passes the count function instead, so the counted path and its timing are the base code's.
- The panel's history now goes through `FoodHistoryReport`: the same index and the same text, built inside Core.

## Cheap wins

None taken. No measured cost moved beyond noise except the inspector's 1.4 µs per build, which I judged not worth a type check against the view's purpose.

## Test time

`OwnershipBoundaryTests`: 7 facts (8 test cases), about 3 s together, mostly the F9c city-s reference run (about 1 s) and the F5h city build. Whole suite: see the [checks file](../checks/f9e.md#validation-run).
