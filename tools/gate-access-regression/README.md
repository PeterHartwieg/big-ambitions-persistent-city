# Offline public-gate regression and benchmark

```sh
dotnet run --project tools/gate-access-regression/GateAccessRegression.csproj --configuration Release
DOTNET_TieredCompilation=0 dotnet run --project tools/gate-access-regression/GateAccessRegression.csproj --configuration Release -- --benchmark
```

The C# 9 harness compiles the actual Runtime `PublicRoadAccess` with the existing synthetic Unity/native surfaces. It reads each captured gate box once by reflection and compares the spatial index against the original full-list intersection scan. The oracle does not use index buckets or cell arithmetic. No game is launched and no save is read or written.

Checks cover seeded short and city-spanning queries; empty and small gate sets; exact faces, edges and corners; reversed and zero-length segments; positive and negative cell boundaries; vertical clearance; oversized, nonfinite and out-of-range geometry; missing-collider fallback boxes; moving leaves crossing cells; retained leaf envelopes; gate movement, addition, removal and reordered discovery; unchanged prior snapshots; and both blocked and clear mid-scan candidate-budget fallbacks. Eighty synthetic road queries compare route feasibility, exact cost and selected node sequences, including unreachable goals and expansion-limited searches, with an assertion that the searches actually encounter blocked edges. A separate fixture forces a known detour, closes both paths, then reopens the detour through gate refreshes; it also proves the gated route differs from the ungated result.

The optional benchmark uses identical queries for a harness implementation of the original scan and the current implementation; it does not time the baseline mod binary. Before timing, it checks every request's answer individually, so equal aggregate counts cannot hide cancelling mistakes. It warms both methods, alternates their measurement order across five pairs and prints median times for 200,000 queries per run. It also asserts equal aggregate results and zero hot-path allocations on .NET 10. Layouts include spread-out gates, a dense cluster, and deliberately overlapping boxes spanning many cells. Disable tiered compilation as above to keep background JIT optimization from changing the measured code during these short runs. Run without competing builds or benchmarks.

These are .NET synthetic measurements, not Unity/Mono frame times, observed native gate distributions, or an FPS result. Tiny gate sets and fully overlapping geometry can still have slight dispatch/index overhead; the fallback bounds repeated candidate tests to less than twice a full scan.
