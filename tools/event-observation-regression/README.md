# Event diagnostic regression

Run `dotnet run --project tools/event-observation-regression -c Release`.
Run `python tools/event-observation-regression/compare.py --baseline <pre-change sources> --output <ignored output directory>` for a two-source comparison.

Compiles the actual optional `EventMeasurements.cs` Harmony patches against actual Core/Persistence and controlled Runtime/Unity doubles. A 36-resident city runs two days through suspended route requests, with capture off and on, then repeats with a planner exception. Activity counts and exact serialized city bytes must agree. The harness checks that event, plan, walking, road and exception hooks really fired and that `ContinuePlanning` without a pending event is harmless. The comparison script builds against both source trees and requires identical outputs. No Unity or game is loaded. Native hook installation, native overhead, food/traffic attribution and Unity/Mono timing are not established by this fixture.

`tests/EventObservationTests.cs` separately checks resumed-slice accumulation, distinct same-minute food calls, bounded overflow, retained value isolation, failures, signed heap shrinkage and cumulative-counter resets. The frame-observation harness covers the expanded fixed component storage.
