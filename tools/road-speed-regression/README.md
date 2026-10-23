# Native road speed regression

Run from the repository root:

```sh
dotnet run --project tools/road-speed-regression/RoadSpeedRegression.csproj
```

Compiles the actual C#9 Runtime `RoadRoutes`, `TrafficRoadNetwork`, `NativeRoadSpeeds`, Core and Persistence sources against the existing offline Unity/native stubs. Checks canonical topology and storage across a verified pothole slowdown, staged preflight, first load with an active fault, future section-uniform route timing, partial-section mapping, existing queue immutability, save/rebind after restoration, geometric FIFO spacing and fail-closed unexplained changes.

The injected tuples stand for already-validated Compatibility snapshots; `RloRoadSpeedOverridesTests` independently covers that reflection contract. The harness does not run RLO, Unity physics or the native rig and does not establish attribution for any native campaign failure.
