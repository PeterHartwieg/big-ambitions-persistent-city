# Road-search ceiling regression

```sh
dotnet run --project tools/road-ceiling-regression/RoadCeilingRegression.csproj --configuration Release
```

Compiles the actual Core, `RoadRoutes`, `TrafficRoadNetwork`, `NativeRoadSpeeds` and `MultimodalRoutes` against deterministic native/Unity and walking-route fixtures. It checks winning cars, expensive queues, walking/subway alternatives, exact ties and one-ULP improvements with unequal nonzero source/destination connectors, a better second curb, missing egress, closures, reopening, unreachable roads and no feasible alternative at clock rates 0.01, 1, 2 and 1,000. Access and egress times, curb connectors and car preparation/parking are included. Complete route fingerprints include geometry, service IDs, nominal durations and ETA.

Compare a retained pre-change source tree against the current implementation:

```sh
python3 tools/road-ceiling-regression/compare.py \
  --baseline build/road-search-ceiling-20261021/baseline \
  --output build/road-search-ceiling-20261021/comparison --benchmark
```

The runner compiles the same harness against both trees, compares every regression case and the exact three timed fixtures before timing, then runs three alternating process pairs with tiered compilation disabled. Each timing is the median of five warmed batches of 200 identical full multimodal queries; the report takes the median of the three process results. Run without competing builds or benchmarks. `StagingHarness.Guard.Reads` counts accesses to instrumented synthetic native fields, not A* expansions or all native API calls. Allocations are .NET 10 thread allocations including returned routes and scratch data, not Unity/Mono measurements. These timings do not establish frame-time or FPS gains in game.

Since the resumable planner (27 October), every case also runs through `MultimodalRoutes.Begin` with `Continue(0)`, one unit of work per call, and its complete route fingerprint must equal the one-shot route. Four further cases suspend a job mid road search and run a one-shot plan meanwhile (the road search starts over and still answers the one-shot route), replace the road snapshot between slices (since 28 October `Continue` must throw `RoutePlanningRestartRequired` and a fresh job on the new snapshot must answer the one-shot route), replace it after the job allocated a home car at a curb the new snapshot no longer offers (a fresh job on a fresh resident must answer the one-shot route and park where the one-shot plan parks), and abandon a job. These checks are counted separately as `slicedChecks` and listed under `sliced`, so `compare.py` can still compare a pre-slicing baseline tree, which it builds with `-p:SlicedPlanner=false`.

Core tests in `RoadSearchCeilingTests.cs` separately compare 6,300 bounded queries on 30 deterministic directed graphs against unbounded searches, including gate/disabled-node exclusions, nonnegative traffic costs and slowdown-only traversal. They also cover cutoff cleanup, malformed ceilings and floating-point boundaries. Existing search-parity and traversal tests retain the old overload/method-group contracts and unbounded behavior.
