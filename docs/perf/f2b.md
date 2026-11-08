# City audit F2b — read-only presentation boundary: performance

8 November 2026, offline. Refs [#89](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/89). Measurement named in the [F2 plan](../plans/f2-movement.md#6-performance): `PhysicalPopulation.Update` component p50/p95/max before/after on the native doubles; expected neutral.

## Scope and limits

- Harness: `PresentationBoundaryTests.MeasureUpdateWithOptionalStopBodies`, opt-in through `PC_F2B_MEASURE=<output file>`. It runs the production `Runtime/PhysicalPopulation.cs` against the test-suite native doubles (`tests/VendorPhysicalRuntimeDouble.cs`: pedestrian pool, NavMesh, time). Release build, .NET 10, 100 warm-up frames, then 2,000 (at spots) or 1,500 (walking) timed `Update` calls at 16 ms frames. Each `Update` includes the 0.25 s selection cadence (`PlanQueues`, candidate scan) as in game.
- Fixture: one leisure venue with a saved two-person conversation and 6 or 46 solo window-shopping stops, so 8 or 48 optional-stop bodies (the default pedestrian cap is 48).
- Arms: **at spots** (every body standing at its stop; the same work before and after) and **walking without progress** (bodies walk toward their spots and never arrive, which covers the 20 s bound inside the 24 s window).
- Before = base `3f734cb` `Runtime/PhysicalPopulation.cs` and `Core/WeatherPolicy.cs`, measured with the same harness. Two runs each; the tables give the first run with the p95 of the second in brackets. The PC is shared with other worktrees.
- The selection block extracted by `tools/physical-selection-regression` is unchanged by F2b, so the F5f selection numbers ([F5f perf](f5f.md)) still apply. That harness was not rerun.
- These are component timings on doubles, not Unity/Mono timings and not in-game mod-work.

## `PhysicalPopulation.Update` (one call, ms: p50 / p95 / max)

| Arm | Bodies | Before | After |
| --- | ---: | --- | --- |
| at spots | 8 | 0.0009 / 0.0021 / 0.0379 (0.0018) | 0.0010 / 0.0021 / 0.0091 (0.0019) |
| at spots | 48 | 0.0047 / 0.0089 / 0.1330 (0.0085) | 0.0047 / 0.0088 / 0.0479 (0.0123) |
| walking without progress | 8 | 0.0008 / 0.0018 / 0.3396 (0.0024) | 0.0009 / 0.0021 / 0.0086 (0.0018) |
| walking without progress | 48 | 0.0043 / 0.0076 / 0.0888 (0.0078) | 0.0046 / 0.0092 / 0.0826 (0.0085) |

No sample ≥ 30 ms in any arm. The at-spots arm is neutral within run-to-run noise. In the walking arm the base build cancels every stop at 20 s and releases the bodies (0 bodies at the end of the window), while F2b keeps all 8 or 48 bodies standing short of their spots. The after-arm therefore does more presentation work for the last 4 s and is still within noise of the base. The base's larger single maxima (0.34 ms, 0.60 ms in the second run) fall in the frames where it releases bodies.

## Gate reading

Offline only. F2b adds no per-frame scan, allocation or path query: standing short replaces a cancellation and a release, and the read-only `QueueAtOpening` drops a weather read on waits without a capture. In-game mod-work p95, ≥ 30 ms spikes and whole-frame timing come from the batched rig run (plan check 10); whole-frame figures stay separate from mod-work.
