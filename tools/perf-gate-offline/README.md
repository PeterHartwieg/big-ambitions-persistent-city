# F4e offline pre-gate

Offline expectations for the F4 gate's M and S rows, written as `perf-gate-report/v1` with `source=offline` ([F4 plan](../../docs/plans/f4-perf-gate.md), F4e row and Decision 8). It runs before the rig batch and never replaces it.

```
dotnet run --project tools/perf-gate-offline -c Release -- --out build/perf-gate-offline/report-offline.json
dotnet run --project tools/perf-gate-offline -c Release -- --self-test
```

Options: `--seed N` (default 2026, the F5h seed), `--only <window id prefix>`, for example `--only W-evening`. The full run takes about 20 s; the self-test about 15 s. Run one dotnet process at a time.

## What it runs

- **Fixture (FT-F4-Declared's shape).** The F5h reference city from `tests/F5hIntegratedAcceptanceTests.cs` (`ReferenceShape`, the `Prefilled` steps with the seed as a parameter): 152 homes, 957 residents, the declared-load workplaces, three player croissant shops, F6 household finances, aged to 06:00 on day 3 so the F5g fill has reached its targets (2,551 persons). At load it attaches the F5g fill, the food market, F6 ordinary visits for every other shop and leisure place, and the F6 change journal (fed once per native hour). Attendance wages come with the writer-35 city.
- **Checkpoints.** One uninterrupted pass from the aged city saves a checkpoint at every window start. Each window loads its checkpoint, as the rig loads FT checkpoints.
- **Frame loop.** Runtime's shape: `PopulationFrameBudget(1/fps)` units, citizen events and soft milliseconds; the first unit always runs; route planning gets the frame's remaining time through F5h's admitted planner (16 native paths per frame, 1,024-entry cache). Native time advances under `AccountingClock.Limit`, so it is held until the epoch drains, and checkouts stay in the native hour. The F2a presentation driver (`tools/movement-equivalence/PresentationDriver.cs`, 48 pedestrians, 24 vehicles, 120 m) moves bodies before the advance at a fixed real-time walking speed (2 m/s, as the F2a runner), also on held frames, and selects around the focus after it.
- **Matrix.** Decision 3's windows at the declared pace (factor 1/3, speed 1) at 30, 60 and 120 FPS with S-stationary, plus W-evening with S-path at 60 FPS. W-accounting pools the 60 FPS lunch and evening frames within one game minute of a native hour rollover. G9's fastest-traveller sequence at factor 1 and 60 FPS: one hour each at speeds 1, 2 and 3 with bodies, four hours at speed 6 with bodies released, a 120-minute time-machine skip (hidden-skip budget), and a whole day at speed 3 with bodies.
- **Pace.** Speed 1 at factor 1/3 is the #128 replay's 98.6 frames per game minute at 30 FPS (Decision 3's 3.3 s per game minute). Speeds 2, 3 and 6 are assumed to scale it linearly; rig check G9 measures the real rates.
- **Simulated real time** is frames ÷ fps: each frame stands for one steady frame at the window's rate, whatever the offline frame cost.

## What it measures

Per window: frame cost of Core advancement (`coreFrameMs`), of the driver (`driverMs`) and their sum (`modWorkMs`, the offline proxy for M1–M4), p50/p95/p99/max/mean by nearest rank; every frame of 30 ms or more with its component and GC generation (M2); GC collections; the slowest single unit; held frames, holds, the longest hold and the longest recovery to lag ≤ 0.1 (S3, S4), with held frames grouped by why the frame before them stopped (`citizen-events`, `path-admission`, `milliseconds`); lag (S2); `historicalClockMisses` (S1); per game hour: frames, simulated real seconds, pace ratio against the unheld clock, held seconds and lag (S5); body counts. Verdict rows cover M1–R1: P, L and R rows are `not established` offline, and the overall verdict is always `not established`.

Every window also replays its game span uninterrupted from the same checkpoint and requires the same state hash (saved city plus native orders); the run fails if any window differs.

## Self-test

1. Same seed: two fixtures built from nothing are byte-identical, their window checkpoints are identical, and the same window run on each ends with the same state hash. A different seed builds a different fixture.
2. Sliced = uninterrupted: 30, 60 and 120 FPS with bodies, speed 6 with bodies released, a time-machine skip, and a window starting on an hour rollover each end in the state of the uninterrupted advance.
3. Report: the built report has every Decision 8 field and `source=offline`; removing any header, machine, window, receipt or verdict field, setting `source=native`, an overall `pass`, or an empty commit is refused (71 mutations).
4. Bodies get the same movement per frame on held frames and at every clock factor (Runtime's fixed real-time walking speed).
5. Verdicts: a slow fastest-traveller speed-3 window or slow W-accounting frames fail M1; with no speed 1-3 window, M1, M3, M4, S3, S4 and S5 are `not established`.

## Schema validation

F4a's `tools/perf-gate/contract-v1.json` is not merged at this base. `Report.Validate` checks the plan's documented field list instead. When F4a merges, validate the offline report with F4a's analyzer and retire `Report.Validate`. Fields that only a native run has carry `n/a (offline)` instead of being left out, so a missing field is still a refusal.

## Limits

- .NET 10 Stopwatch time on the development PC, not Mono and not the game. Native seams are doubles: the route planner, the checkout, the ordinary-visit ownership and the bodies. No union probe, no presented frames, no dependency-only reference.
- The F5h fixture routes walking legs only and has no rival scope: car bodies stay at 0, and rival service limits are not exercised.
- W-accounting has no `AfterNativeTick`, no native hourly customers and no ownership transfer offline.
- The soft frame limit is wall-clock, so frame and held-frame counts can differ by a frame or two between runs. State hashes do not.

## Build

`PerfGateOffline.csproj` compiles Core, Persistence and Compatibility by path, as `tools/rush-spike-replay` does, and every test source with the test project's linked files, so the fixture is the code the unit tests run. When `tests/PersistentCity.Tests.csproj` links a new file, add the same line here. Nullable warnings that differ between netstandard2.1 and net10.0 annotations of the shared sources are not errors in this tool.
