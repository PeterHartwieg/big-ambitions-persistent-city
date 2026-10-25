# Native walking quote regression

Run `dotnet run --project tools/walking-quote-regression/WalkingQuoteRegression.csproj`.

This compiles the actual Core, Runtime/WalkingRoutes.cs and route metrics against small controlled Unity/NavMesh doubles. It does not run the game or prove native demand throughput.

The regression models a complete native corner path whose independently sampled segment raycast refuses. Whole-path revalidation must accept the unchanged complete route while still refusing changed corridors, incomplete or failed native queries, closed or changing public gates, endpoint displacement outside5cm, incorrect quote timing, broken continuity, excessive walking distance and more than512 legs. A changed cached corridor must be cleared so a subsequent plan can return the current geometry. Stationary walks retain endpoint validation.

The startup-clock candidate failed the complete-corner-path assertion; the revised source passes28checks. Captured local output is under `build/execution-20261024/walking-quote-regression/`. The observed native failure was132 quote checks and132 rejections despite otherwise planned paths. The exact rejected native corner was not logged, so polygon-boundary resampling/raycast disagreement remains the source-supported mechanism, not a captured hit coordinate. A native rerun is still required.

Validation now performs one bounded native CalculatePath through the existing dry filter, sampling original endpoints only, and compares its full geometry/timing with the quote. Both quoted and fresh paths retain live public-access checks. The1mm geometry tolerance matches the existing quote continuity bound; timing retains1e-6minute tolerance. It does not accept a different corridor or reuse geometry blindly.

Metric interpretation: WalkingCalculations counts planner cache misses. WalkingQuoteChecks counts quote validation attempts. NavMeshPathQueries and NavMeshPathMilliseconds include both planning and whole-path revalidation; quote duration separately includes validation work. A walking-cache hit does not imply validation avoided native work. NavMeshRaycastQueries remains available for older evidence but this validation path no longer performs segment raycasts.
