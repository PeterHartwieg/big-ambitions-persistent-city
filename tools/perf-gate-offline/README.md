# F4e offline pre-gate

Offline expectations for the F4 gate's M and S rows ([F4 plan](../../docs/plans/f4-perf-gate.md), F4e row and Decision 8). The tool writes F4a's `perf-gate-export/v1` with `source=offline` and runs F4a's analyzer (`tools/perf-gate/analyze.py` against `contract-v1.json`) on it, which writes the `perf-gate-report/v1` JSON and Markdown. It runs before the rig batch and never replaces it.

```
dotnet run --project tools/perf-gate-offline -c Release -- --out build/perf-gate-offline
dotnet run --project tools/perf-gate-offline -c Release -- --self-test
```

Output in the `--out` directory: `offline.export.json` (about 65 MB, every frame), `offline.report.json` and `offline.report.md`. Python 3 must be on the path (`$PYTHON`, else `python3`, or `python` on Windows).

Options: `--seed N` (default 2026, the F5h seed), `--only <window id prefix>`, for example `--only W-evening`. The full run takes about 20 s; the self-test about 15 s. Run one dotnet process at a time.

## What it runs

- **Fixture (FT-F4-Declared's shape).** The F5h reference city from `tests/F5hIntegratedAcceptanceTests.cs` (`ReferenceShape`, the `Prefilled` steps with the seed as a parameter): 152 homes, 957 residents, the declared-load workplaces, three player croissant shops, F6 household finances, aged to 06:00 on day 3 so the F5g fill has reached its targets (2,551 persons). At load it attaches the F5g fill, the food market, F6 ordinary visits for every other shop and leisure place, and the F6 change journal (fed once per native hour). Attendance wages come with the writer-35 city.
- **Checkpoints.** One uninterrupted pass from the aged city saves a checkpoint at every window start. Each window loads its checkpoint, as the rig loads FT checkpoints.
- **Frame loop.** Runtime's shape: `PopulationFrameBudget(1/fps)` units, citizen events and soft milliseconds; the first unit always runs; route planning gets the frame's remaining time through F5h's admitted planner (16 native paths per frame, 1,024-entry cache). Native time advances under `AccountingClock.Limit`, so it is held until the epoch drains, and checkouts stay in the native hour. The F2a presentation driver (`tests/movement-equivalence/PresentationDriver.cs`, 48 pedestrians, 24 vehicles, 120 m) moves bodies before the advance at a fixed real-time walking speed (2 m/s, as the F2a runner), also on held frames, and selects around the focus after it.
- **Matrix.** Decision 3's windows at the declared pace (factor 1/3, speed 1) at 30, 60 and 120 FPS with S-stationary, plus W-evening with S-path at 60 FPS. W-accounting pools the 60 FPS lunch and evening frames within one game minute of a native hour rollover. G9's fastest-traveller sequence at factor 1 and 60 FPS: one hour each at speeds 1, 2 and 3 with bodies, four hours at speed 6 with bodies released, a 120-minute time-machine skip (hidden-skip budget), and a whole day at speed 3 with bodies.
- **Pace.** Speed 1 at factor 1/3 is the #128 replay's 98.6 frames per game minute at 30 FPS (Decision 3's 3.3 s per game minute). Speeds 2, 3 and 6 are assumed to scale it linearly; rig check G9 measures the real rates.
- **Simulated real time** is frames ÷ fps: each frame stands for one steady frame at the window's rate, whatever the offline frame cost.

## What it measures

Each export window has one frame row per frame: `mod_work_ms` is Core advancement plus the driver (the offline proxy for M1–M4), with components `AdvancePopulation` and `presentation-driver` and the frame's GC count. Offline has no game frame, so `whole_frame_ms` equals `mod_work_ms`. The `accounting` block has `historicalClockMisses` (S1), a lag sample after every frame (S2, S4) and each calendar hold as a start and end in simulated real seconds (S3, S4). `density` and `bodies` come from the driver. G9's hours and day go into `measurements.S5`. F4a's analyzer computes every row from these fields.

Each window's `offline` block keeps detail the contract has no field for, and the analyzer ignores it: pace ratio, held frames grouped by why the frame before them stopped (`citizen-events`, `path-admission`, `milliseconds`), the slowest single unit, the finer M2 component (`planning`, `fill`, `food` and so on), per game hour frames, real seconds, pace ratio, held seconds and lag, and the state hashes.

**Every offline window is invalid as a gate window.** The contract's rig receipts (`no_dotnet_activity`, `priority_high`, `focused`, `settings_read_back`, `probe_coverage`, `probe_flags`, `presentation_trace`) are `n/a` offline, and the contract does not allow `n/a` on a candidate window. Most windows are also shorter than 300 clean real seconds. The report therefore keeps the offline expectations as each window's own rows (M1, M3, M4, S1–S4), and every aggregated window row reads `not established` ("no valid candidate window"). S5 comes from the measurements block. The overall verdict is never `pass`.

Arms: Decision 3's declared windows and G9's speed 1–3 segments with bodies are `primary`. Speed 6 with bodies released and the time-machine skip are `report-only`. Each condition carries its frame rate (`W-evening@60fps`), so the analyzer never pools or pairs 30, 60 and 120 FPS frames.

Every window also replays its game span uninterrupted from the same checkpoint and requires the same state hash (saved city plus native orders); the run fails if any window differs.

## Self-test

1. Same seed: two fixtures built from nothing are byte-identical, their window checkpoints are identical, and the same window run on each ends with the same state hash. A different seed builds a different fixture.
2. Sliced = uninterrupted: 30, 60 and 120 FPS with bodies, speed 6 with bodies released, a time-machine skip, and a window starting on an hour rollover each end in the state of the uninterrupted advance.
3. F4a's analyzer accepts the export and writes `perf-gate-report/v1`, contract `perf-gate/v1`, `source=offline`, with every window invalid and an overall verdict other than `pass`. The same analyzer refuses the export with any contract header field removed, a machine or fixture field removed, a wrong contract version, one window marked `native`, or the whole export marked `native` with its offline `n/a` fields (20 mutations).
4. Bodies get the same movement per frame on held frames and at every clock factor (Runtime's fixed real-time walking speed).
5. Expectations through F4a's analyzer: a slow speed-3 window and slow W-accounting frames fail their window's M1, and a fast declared window passes. Without G9's day window S5 is `not established`, and speed 6 is `report-only`.

## Header

Contract field names. The offline-allowed fields (probe hash, scope list hash, game build, mod set, rig commit and overlay, settings read-back, rig capabilities) are `n/a`. `pc_dll_sha256` is the SHA-256 of the PerfGateOffline assembly, because Core and Persistence are compiled into it from source and there is no PersistentCity.dll. Machine fields that only a game has read `none (offline)`. The fixture's `provenance_hash` is the aged fixture's SHA-256.

## Limits

- .NET 10 Stopwatch time on the development PC, not Mono and not the game. Native seams are doubles: the route planner, the checkout, the ordinary-visit ownership and the bodies. No union probe, no presented frames, no dependency-only reference.
- The F5h fixture routes walking legs only and has no rival scope: car bodies stay at 0, and rival service limits are not exercised.
- W-accounting has no `AfterNativeTick`, no native hourly customers and no ownership transfer offline.
- The soft frame limit is wall-clock, so frame and held-frame counts can differ by a frame or two between runs. State hashes do not.

## Build

`PerfGateOffline.csproj` compiles Core, Persistence and Compatibility by path, as `tools/rush-spike-replay` does, and every test source (`tests/*.cs`, `tests/Acceptance/*.cs`, `tests/Support/*.cs`) with the test project's linked files, so the fixture is the code the unit tests run. When `tests/PersistentCity.Tests.csproj` links a new file or the tests gain a folder, add the same line here. Nullable warnings that differ between netstandard2.1 and net10.0 annotations of the shared sources are not errors in this tool.
