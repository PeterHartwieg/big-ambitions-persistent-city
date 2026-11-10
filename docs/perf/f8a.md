# F8a measurements

## 9 November 2026: offline components only

These numbers come from one foreground .NET 10 Release run on the Windows PC (`Stopwatch` at 10 MHz, so 0.1 µs per tick). They measure the game-free Core code in `Core/NativeCapabilities.cs`. F8a is not wired to Runtime, so it adds no per-frame work and no mod-work in game. No game, rig, autosave setting or lease was used.

Command: `PC_F8A_MEASURE=<file> dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~NativeCapabilityTests` (test `RegistryUpdateAndReportCost`). Each arm warms 1,000 calls, then times 20,000 single calls. Allocation is `GC.GetAllocatedBytesForCurrentThread` over the timed calls divided by the call count.

| Arm | p50 µs | p95 µs | max µs | B/call | >= 30 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Registry update (one re-check transition, then `Start` and `DisabledScopes`) | 0.600 | 0.900 | 10.400 | 0 | 0 |
| Registry `Fail` on an Off capability (sticky no-op) | 0.000 | 0.100 | 9.100 | 0 | 0 |
| Report `Status`, rebuilt (state changed since the last call) | 2.800 | 5.100 | 2500.900 | 2316 | 0 |
| Report `Status`, reused (no change) | 0.000 | 0.100 | 0.200 | 0 | 0 |
| Report `Show` (`food.dynamic`, 23 hook lines) | 7.300 | 11.300 | 5080.300 | 12120 | 0 |

The test asserts 0 B for the registry update, the sticky `Fail` and the reused report. The two report maxima (2.5 ms and 5.1 ms) are single samples in arms that allocate, with p95 at 5.1 µs and 11.3 µs. I read them as a GC pause or scheduler noise; none comes near the 30 ms spike line. Reports run only on request (F8b), never per frame.

## Gate

Standing gate: mod-work p95 < 5 ms, every mod-work spike >= 30 ms flagged, whole-frame timing reported separately. F8a adds no mod-work, so no in-game sample applies to this slice; the in-game window is rig check 12 in [the checks](../checks/f8a.md#in-game-checks-for-the-next-batched-rig), runnable after F8b. Whole-frame stalls stay with #128. The four parked hotspots stay parked.

## Cheap win

Reused status text: `CapabilityReport.Status` keeps its last string while the registry `Revision` and the game version string are unchanged. Before (rebuilt every call): p50 2.8 µs, p95 5.1 µs, 2,316 B per call. After (unchanged state): p50 0.0 µs, p95 0.1 µs, 0 B. The first call after any transition still rebuilds, which `DeterministicReport` checks.
