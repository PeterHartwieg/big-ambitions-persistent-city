# Native walking quote regression

Run `dotnet run --project tools/walking-quote-regression/WalkingQuoteRegression.csproj`.

This compiles the actual Core, Runtime/WalkingRoutes.cs and route metrics against small controlled Unity/NavMesh doubles. It does not run the game or prove native demand throughput.

The regression models a complete native corner path whose independently sampled segment raycast refuses. Whole-path revalidation must accept the unchanged complete route while still refusing changed corridors, incomplete or failed native queries, closed or changing public gates, endpoint displacement outside5cm, incorrect quote timing, broken continuity, excessive walking distance and more than512 legs. A changed cached corridor must be cleared so a subsequent plan can return the current geometry. Stationary walks retain endpoint validation.

The startup-clock candidate failed the complete-corner-path assertion; the revised source passes28checks. Captured local output is under `build/execution-20261024/walking-quote-regression/`. The observed native failure was132 quote checks and132 rejections despite otherwise planned paths. The exact rejected native corner was not logged, so polygon-boundary resampling/raycast disagreement remains the source-supported mechanism, not a captured hit coordinate. A native rerun is still required.

Validation now performs one bounded native CalculatePath through the existing dry filter, sampling original endpoints only, and compares its full geometry/timing with the quote. Both quoted and fresh paths retain live public-access checks. The1mm geometry tolerance matches the existing quote continuity bound; timing retains1e-6minute tolerance. It does not accept a different corridor or reuse geometry blindly.

Metric interpretation: WalkingCalculations counts planner cache misses. WalkingQuoteChecks counts quote validation attempts. NavMeshPathQueries and NavMeshPathMilliseconds include both planning and whole-path revalidation; quote duration separately includes validation work. A walking-cache hit does not imply validation avoided native work. NavMeshRaycastQueries remains available for older evidence but this validation path no longer performs segment raycasts.

## Shared-quote requests

`Operation.cs` covers `IWalkingQuoteOperation`. Core opens one scope per K>1 shared-quote request: the shared quote's check, then at most one replacement plan and its check. Inside it, WalkingRoutes may answer a repeat of the request's latest calculation, for identical endpoints and filter, from that calculation. A request's first calculation is always live, public access is read at every check, and nothing survives End. Standalone validation outside a scope is unchanged and still queries every time (the 28 checks above).

The checks count native queries for fresh plans (two to one), stale-quote replacement (three to one) and warm quotes (one, unchanged). They change navigation between requests without a frame boundary, close gates at every access-read position during fresh and stale requests, and cover nesting, Clear inside a scope, different endpoints and filters, and post-End standalone queries. One check documents the assumption itself: if navigation changed inside a request, the reused calculation would not see it, whereas a second live query would refuse.

An integrated run drives actual Core FoodCommerce at K=2 for three days through actual WalkingRoutes, with corridor edits and sub-millimetre corner jitter between requests. Scoped and unscoped planners must produce byte-identical saved cities and purchases, identical quote checks and rejections, and every reuse must replace exactly one live query. These are offline doubles; they do not show that native NavMesh data is stable within a request or measure frame time.
