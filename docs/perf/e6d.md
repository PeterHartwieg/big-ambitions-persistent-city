Status: implemented (offline); not verified by rig

# E6d performance

Base `b2a0ab70`, writer 30. [Scope and checks](../checks/e6d.md). The four known hotspots stay parked.

## Method

`tests/E6dVehicleMarketMeasure.cs` is an opt-in headless measurement of the production Runtime vehicle/composite market linked into the xUnit project. It uses the real reflection bridge against E6a's contract-shaped test double, a mesh-query double, and a native-shop seller double. The double's `Read` constructs detached DTOs for all 64 vehicles, so those allocations and calls are included in checkout numbers. This does not measure the actual Street Food implementation or native scene/routing/rendering.

Command, in the foreground after confirming no other testhost or dotnet test run is active:

```sh
env PATH="/opt/homebrew/bin:$PATH" DOTNET_ROOT="$(/opt/homebrew/bin/brew --prefix dotnet)/libexec" PC_E6D_MEASURE=1 dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~E6dVehicleMarketMeasure --logger 'console;verbosity=detailed'
```

Read and capture use one validated snapshot. Checkout performs two fresh API reads and verifies the selected vehicle identity at its saved slot; if Street Food changes the array order, it falls back to the bounded identity search. Same-minute confirmed checkout updates only the copied supply/price evidence, so reads/captures do not add API reads or revision polls. The regular E6d cases assert the unchanged API-read count, the two-copy checkout path and safe fallback after reordering.

Warm-up: 30 operations; 300 measured operations per isolated case. The 64-vehicle lunch rush warms up 20 bursts and records 160 bursts, split into eight groups of eight checkouts. Each burst advances one native minute, so its first group includes the changed 64-vehicle snapshot copy/apply; each measured group includes vendor preparation, merged nearest-eight capture and eight checkouts. The whole burst is reported separately from a bounded group; a burst is not a Unity frame. Allocations use `GC.GetAllocatedBytesForCurrentThread`; timing uses `Stopwatch`. Assertions and the test-double costs are included. Samples at least 30 ms are all printed with the component; p95 is checked against 5 ms for isolated operations and combined bounded market work.

Autosave: absent in this headless process; no saves inside measured windows. Native windows must record, disable, verify and restore the original setting as specified in check 8. Whole-frame cost is unmeasured offline and is a separate batched-rig result.

## Measurements

Measured 7 November 2026, macOS 26.5.2 arm64, Release, .NET SDK 10.0.400 / runtime 10.0.11. One foreground dotnet test process; other checkout runs were allowed to finish before launching. The final lunch samples advance one minute per burst and include snapshot refresh.

| Operation | Samples | p50 ms | p95 ms | Max ms | Allocated B/op | ≥30 ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Snapshot Read, 64 vehicles | 300 | 0.349875 | 0.390542 | 3.240333 | 259,336.0 | 0 |
| One capture handle and 8 vehicle captures | 300 | 0.014834 | 0.015459 | 0.500333 | 11,544.0 | 0 |
| Baseline: materialize 64, rank 8 | 300 | 0.354875 | 0.429792 | 0.925708 | 261,672.0 | 0 |
| Current: rank, materialize 8 | 300 | 0.322250 | 0.367000 | 0.906917 | 248,344.0 | 0 |
| Checkout, vehicle 64 of 64 | 300 | 0.038917 | 0.045666 | 0.567791 | 43,167.6 | 0 |
| Combined preparation + merged capture + 8 checkouts | 1,280 | 0.236250 | **0.916833** | 5.889666 | 601,752.3 | 0 |
| Whole 64-sale burst, across 8 groups | 160 | 2.370375 | 6.957500 | 11.097417 | 4,814,018.7 | 0 |

The isolated operations and combined bounded market group pass p95 < 5 ms. Every measured ≥30 ms spike: **0**. The whole 64-sale burst exceeds 5 ms at p95 across eight groups; it is not a single frame sample. Whole-frame timing remains unmeasured offline.

## Cheap, safe wins

The baseline materializes 64 vehicle offer sets before sorting/selecting eight; the implementation ranks saved positions before materializing eight. The paired measurement above reduces p95 from 0.429792 to 0.367000 ms and allocations from 261,672 to 248,344 B/op for the same selected count. The composite applies its shared bound once, so eight nearer shops exclude a vehicle and seven nearer shops include it. No unbounded supply reads are added to a decision.

A second local change removed duplicate eligibility checks in full Read and resolved Runtime row lookup through stable saved scope/place strings. Core's full authority check still runs. With the same isolated 64-vehicle probe:

| Snapshot Read | p50 ms | p95 ms | Max ms | Allocated B/op |
| --- | ---: | ---: | ---: | ---: |
| Before local lookup/read change | 0.756667 | 1.286500 | 6.510583 | 1,046,496.3 |
| After | 0.349875 | 0.390542 | 3.240333 | 259,336.0 |

Allocation fell 75.2%. These are the first and final measurement runs (the final run also includes whole-menu exact-money-range refusal); the lunch fixture's minute advancement changed between runs, so combined-group before/after timings are not compared. No parked Core hotspot was edited.

The saved-slot checkout path verifies the vehicle id before reusing the slot. A normal before/after pair copies two vehicles instead of scanning up to 128 copies for the 64th vehicle. Reordered snapshots are covered by a functional test and fall back to identity search.

## Review-fix verification

Repeated the same Release probe on 7 November 2026 after the closed-visit, local-read, stockout and shutdown fixes. The completed foreground run had no other testhost active. A prior overlapping functional-suite launch was cancelled and is excluded from reported checks. Autosave is absent in this process, and there were no saves in the measurement windows. These are functional repairs; no additional optimization or parked-hotspot work was undertaken.

| Operation | Samples | p50 ms | p95 ms | Max ms | Allocated B/op | ≥30 ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Snapshot Read, 64 vehicles | 300 | 0.344625 | 0.359125 | 3.301167 | 259,336.0 | 0 |
| One capture handle and 8 vehicle captures | 300 | 0.014250 | 0.014500 | 0.433875 | 11,544.0 | 0 |
| Baseline: materialize 64, rank 8 | 300 | 0.349208 | 0.359375 | 0.831042 | 261,672.0 | 0 |
| Current: rank, materialize 8 | 300 | 0.310417 | 0.317875 | 0.724625 | 248,344.0 | 0 |
| Checkout, vehicle 64 of 64 | 300 | 0.036875 | 0.038250 | 0.489500 | 43,167.6 | 0 |
| Combined preparation + merged capture + 8 checkouts | 1,280 | 0.225041 | **0.854875** | 6.059375 | 601,214.7 | 0 |
| Whole 64-sale burst, across 8 groups | 160 | 2.305083 | 6.458458 | 11.521917 | 4,809,718.0 | 0 |

Combined bounded market work remains below 5 ms at p95 (previous 0.916833 ms; repeat 0.854875 ms). Every measured ≥30 ms spike: **0**. The whole burst spans eight groups and is not a frame; whole-frame cost remains unmeasured offline. The probe exercises normal sales; the review regressions separately exercise closed arrivals and the optional stockout evidence read after a definite refusal.

## Native performance still to measure

The [automated rig checklist](../checks/e6d.md#in-game-checks-for-the-next-batched-rig), check 8, owns combined full PC mod-work p95, all ≥ 30 ms spikes and separate whole-frame timing with actual Street Food. Offline combined market numbers include neither the four parked hotspots nor the rest of the game's work. They are not native frame evidence. Runtime vendor status exposes maximum observed unit time and the ≥ 30 ms count, and each such unit logs its component and duration without a first-N suppression.
