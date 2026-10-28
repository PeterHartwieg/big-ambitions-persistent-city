# Traffic queue reference regression

Run `python tools/traffic-queue-regression/compare.py --baseline <pre-change Core sources> --output <ignored directory>`.
The same program builds against each tree; complete output must match. Run `dotnet run --project tools/traffic-queue-regression -c Release` for one tree.

32,414 serialized observations cover permissions, completion callbacks, forecast values and exact kernel captures at event budgets 1, 17 and 10,000. Fixtures use sparse link IDs, 60 vehicles, closures/reopening, physical progress/release, mid-queue curb exits, cancellation and reuse of the same external ID, interrupted restoration preserving physical control, plus a 400-vehicle queue. Every forecast checks that the live capture is unchanged; every restore checks exact capture bytes. No game is loaded and no native performance claim follows.

Also run `tests/TrafficQueueReferenceTests.cs`, the complete test suite, and the existing `tools/core-identity-regression/compare.py` against the same baseline to compare full city activity, traffic and save outputs.
