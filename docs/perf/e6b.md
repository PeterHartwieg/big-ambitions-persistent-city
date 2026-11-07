# E6b: vendor bridge and Core vendor rules cost (7 November 2026, offline)

Plan: [E6 section 4, row E6b](../plans/e6-carts-food-trucks.md#4-slice-breakdown) and [section 6](../plans/e6-carts-food-trucks.md#6-performance). Checks: [docs/checks/e6b.md](../checks/e6b.md). E6b adds no Runtime wiring: nothing in the game build calls the bridge or the vendor coordinator yet (E6d does), so E6b adds no mod-work in game today. These are the costs E6d will add once it calls `VendorCoordinator.Poll` once per logical minute and `Continue` in continuation units.

## Method

Test `E6bVendorMeasure.RevisionPollImageCopyStandPublishReferencesAndAwarenessRebuild`, run alone with `PC_E6B_MEASURE=1` and `--filter FullyQualifiedName~E6bVendorMeasure --logger "console;verbosity=detailed"` (without the variable it returns at once, like the F5c-lite harness; its timing bounds are not reliable inside the full parallel suite). Debug build, net10.0, Windows 11, one foreground dotnet process. `Stopwatch` timing, allocation from `GC.GetAllocatedBytesForCurrentThread`, warm-up samples dropped. Street Food is the contract-shaped stand-in in `tests/StreetFoodDoubles.cs`, bound through the real `StreetFoodBridge`, so the reflection calls and the copy are the production path. The stand-in allocates one `VendorApiVehicle` per vehicle on every `Read`, as a detached view must; that allocation is in the copy rows.

## Results

| Work | When it runs | p50 | p95 | max | Allocation |
| --- | --- | ---: | ---: | ---: | ---: |
| Revision poll, unchanged revision, 64 live rows (100,000 polls) | once per logical minute | 0.0008 ms | 0.0010 ms | 0.0029 ms | 0 B |
| Poll with lease renewal | once per 15 native minutes while PC wants or owns a vehicle | 0.0003 ms | 0.0004 ms | 0.0024 ms | 237 B |
| Image copy, 8 vehicles, whole copy (Read + 1 unit) | on a changed revision | 0.0129 ms | 0.0168 ms | 0.0275 ms | 5,579 B |
| Image copy, 8 vehicles, per unit | | 0.0077 ms | 0.0125 ms | 0.0214 ms | — |
| Image copy, 64 vehicles, whole copy (Read + 4 units of 16) | on a changed revision | 0.0681 ms | 0.0941 ms | 6.5182 ms | 43,267 B |
| Image copy, 64 vehicles, per unit | | 0.0076 ms | 0.0438 ms | 6.4478 ms | — |
| Apply unit, all 64 vehicles at a new stand (64 grants, stand plans, place and scope publishes) | on a changed revision | 0.0948 ms | 0.1309 ms | 0.1783 ms | 78,652 B per copy + apply |
| `PublishVendorPlace`, one stand | per stand change | 0.0001 ms | 0.0003 ms | 0.0010 ms | not measured |
| `CollectVendorReferences`, 20,000 meals | at most once per apply, only when a row waits on residents (deferred stand, release), at most once per 5 native minutes; and at compaction | 0.2820 ms | 0.4849 ms | 1.4087 ms | not measured |
| Awareness rebuild after 1 move, 1,064 scopes, 452 units, whole | after a stand change, through the existing staged builder | 1.6309 ms | 2.3658 ms | 3.0536 ms | not measured |
| Awareness rebuild after 1 move, per unit | | 0.0031 ms | 0.0082 ms | 0.8087 ms | not measured |
| Awareness rebuild after 64 moves, whole | | 0.8262 ms | 1.1348 ms | 1.4508 ms | not measured |
| Awareness rebuild after 64 moves, per unit | | 0.0017 ms | 0.0037 ms | 0.5762 ms | not measured |

The awareness rebuild is the same work after one move and after 64 moves: any stand change invalidates the shared image and the existing builder rebuilds it whole, at most 64 operations per unit (`FoodAwarenessCatalogue.Builder.MaximumWorkPerUnit`). The total runs spread over 452 continuation units; mod-work per frame is the per-unit figure.

Gate: every per-unit p95 is below 0.2 ms, far under the 5 ms mod-work gate. Spikes: none of 30 ms or more. The largest single sample is a 6.4 ms copy unit (one in 1,500, with a 0.04 ms p95, so a GC or JIT outlier rather than copy work); the largest awareness unit is 0.81 ms. The 64-move rebuild measuring faster than the 1-move one is warm-up order (it runs second), not less work. The four known hotspots stay parked; E6b does not touch them. Whole-frame timing is not measured offline; the batched rig measures it separately (check 7 in the checks file).

## Perf decisions

- **Index loops instead of `foreach` over `IReadOnlyList` in the per-minute poll.** `NeedsLease` and `Waiting` run on every poll. With `foreach` over the table's `IReadOnlyList<VendorRow>` the boxed enumerator cost **80 B per poll** (8,000,000 B over 100,000 polls, same test); with index loops it is **0 B**. Time was unchanged (p50 0.0008 ms before and after). Taken.
- **The revision is read through a typed delegate** (`Func<long>` over the property getter, built once at bind time), so the poll neither boxes nor allocates. Taken in the first version; no before number.
- **Two images, allocated once and swapped.** The copy reuses 64 preallocated vehicle slots with 8-item arrays. The remaining copy allocation is Street Food's own detached DTOs and the boxing of reflected field values (about 0.7 KB per vehicle), only on a changed revision. A compiled-accessor copy would remove the boxing but is not worth it at 0.12 ms p95 for 64 vehicles on change only. Not changed.
- **One reference scan per apply, shared by all rows.** Stand-move and release guards collect the vendor places named by meals, excursions, held decisions and routine stops in one pass (0.42 ms p95 at 20,000 meals) instead of one scan per row. It runs only while something waits. Taken in the first version.
