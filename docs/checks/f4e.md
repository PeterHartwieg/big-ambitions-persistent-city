Status: implemented (offline); not verified by rig

# F4e: offline perf pre-gate, 9 November 2026

Refs [#91](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/91). Branch `codex/pc-f4e-offline-pregate` from `codex/persistent-city` at `3655662` (F8 plan #167 on top of F2e #172; current writer 35). Scope: the F4e row of the [F4 plan](../plans/f4-perf-gate.md#4-slice-breakdown), with Decisions 2, 3, 4 and 8. Perf numbers: [docs/perf/f4e.md](../perf/f4e.md).

## What is implemented

`tools/perf-gate-offline/` (new project, [README](../../tools/perf-gate-offline/README.md)): `PerfGateOffline.csproj`, `Program.cs` (matrix and run), `Windows.cs` (fixture, checkpoints, frame loop), `Report.cs` (report writer and validator), `SelfTest.cs`.

- **Fixture.** FT-F4-Declared's shape: the F5h reference city (`tests/F5hIntegratedAcceptanceTests.cs` `ReferenceShape`, `Prefilled` with the seed as a parameter), 957 residents and 2,551 persons at 06:00 on day 3, three player food shops, F6 household finances and attendance wages, plus the F6 change journal and ordinary visits attached at load. Test sources are compiled by path, as `tools/rush-spike-replay` does.
- **Windows.** Decision 3's W-morning, W-lunch, W-evening, W-midnight and W-idle at the declared pace (factor 1/3, speed 1) at 30, 60 and 120 FPS with S-stationary; W-evening with S-path; W-accounting pooled from the lunch and evening rollovers; G9's fastest-traveller sequence (factor 1, speeds 1, 2, 3 with bodies, speed 6 with bodies released, a 120-minute time-machine skip) and a whole day at speed 3.
- **Frame loop.** Runtime's `PopulationFrameBudget` shape behind `AccountingClock.Limit`, F5h's admitted planner (16 native paths per frame), and the F2a presentation driver at 48 pedestrians / 24 vehicles.
- **Report.** `perf-gate-report/v1`, `source=offline`, contract `perf-gate/v1`: every Decision 8 header field (native-only ones as `n/a (offline)`), Decision 3 receipts per window, the per-window table (M1–M4, P1–P3, reference, P4, S1–S5, density, bodies, GC), a verdict with reason for every row M1–R1, the M2 spike list and the stall table. The overall verdict is always `not established`. F4a's `contract-v1.json` is not merged at this base; the report is validated against the plan's field list (`Report.Validate`), and validation against F4a's schema lands when F4a merges. No file under `tools/perf-gate/` is created or edited.
- **Offline expectations** (details in the perf file): M1–M4 pass over every window at speeds 1–3 with bodies and the pooled W-accounting frames, with the largest offline mod-work p95 at 0.063 ms (G9a speed 3) and no frame at or above 30 ms; S1, S2, S4 and S5 pass; **S3 is expected to fail on held share**: 1.3–1.6 % at W-evening at the declared pace (30/60 FPS) and 12 % in the speed-3 hour at factor 1, every hold following a frame that ended on the 16 citizen events (or the 16-path admission) of the frame budget. The longest hold is 0.167 s.

### Technical choices

- **One planner role everywhere.** The uninterrupted reference and the checkpoints use a synchronous planner with the commute-timing role (`ICommuteTimingPlanner`), like Runtime's and the window's admitted planner. The first version used F5g's cached planner without that role; the sliced-equals-uninterrupted check then failed in every window (commuters left at different minutes), which is how it was found.
- **Simulated real time** is frames ÷ fps, so pace and hold figures do not depend on the offline machine's speed.
- **The first window is discarded** (JIT), and garbage is collected before each window and the GC count is taken after it.
- Native speeds 2, 3 and 6 are assumed to be 2×, 3× and 6× speed 1; rig check 4 below measures them.
- No Core, Persistence or Runtime file changes; no change to the main test project or solution.
- **M rows** cover every window at native speeds 1–3 with bodies (declared windows and G9's speed 1–3 hours and day) and the pooled W-accounting frames; speed 6 and the skip are reported per window only. Each M2 spike keeps its gen-0/1/2 collection counts across the driver and Core parts of the frame.
- **Body movement** uses the F2a runner's fixed real-time conversion (`GridPlanner.ClockRate`, 2 m/s walkers), not the calendar delta, so holds and the clock factor do not change how far bodies move.

## Offline checks

| Check | Result |
| --- | --- |
| `python tools/build.py --game-managed "…\Big Ambitions_Data\Managed"` | passed |
| `dotnet test tests/PersistentCity.Tests.csproj` | round 1 head: 4,287 passed, 1 failed of 4,288: the timing assertion `FoodContractCoordinatorTests.RecheckBoundaryRebuildAndCommitGateCosts` failed under other sessions' load and passed when rerun alone; this branch changes no file under `Core/`, `Persistence/`, `Runtime/` or `tests/`. Review run at `b81488d`: 4,288 passed, 0 failed. |
| `dotnet run --project tools/perf-gate-offline -c Release -- --self-test` | passed, 21 checks |
| `dotnet run --project tools/perf-gate-offline -c Release -- --out build/perf-gate-offline/report-offline.json` | passed: 23 windows, every window's sliced state equal to its uninterrupted advance, report valid |

Self-test, the F4e row's three tests:

1. **Same seed gives identical state hashes.** Two fixtures built from nothing with seed 2026 are byte-identical (`f769b3a3…`), their 18:30 checkpoints are identical, and the same 18:30–19:05 window at 60 FPS with bodies ends with the same state hash on each. Seed 2027 builds a different fixture.
2. **Sliced runs equal an uninterrupted advance** (as the fix-128 replay): 30, 60 and 120 FPS with bodies, speed 6 with bodies released, a time-machine skip, and a window starting on the 19:00 rollover each end in the uninterrupted advance's state (saved city plus native orders), and 30/60/120 FPS end in the same state. The full run repeats this for all 23 windows.
3. **The report has every documented field and `source=offline`.** The built report validates; removing any of the 15 header, 7 machine, 23 window, 5 receipt or 18 verdict fields, `source=native`, an overall `pass` or an empty commit is refused (71 of 71).
4. **Bodies move at Runtime's fixed real-time walking speed** (review Astra-r1-1): the driver gets the same body movement per frame on held frames and at factor 1/3 speed 1 and factor 1 speed 3.
5. **Verdicts cover every applicable window** (review Sol-r1-1, Opus-r1-2): with synthetic windows, a slow fastest-traveller speed-3 window or slow W-accounting frames fail M1, and a run with no speed 1–3 window gives M1, M3, M4, S3, S4 and S5 `not established`. `--only` with no matching window is refused.

**Schema.** None. Writer 35 is unchanged; nothing under `Persistence/` or the saved model changes, so the pinned current-writer golden hash is untouched (`CurrentWriterGoldenHashIsUnchanged` in the unit tests). No migration.

## In-game checks for the next batched rig

All checks run on the headless agent rig in the named disposable FT city FT-F4-Declared (or a disposable copy of its checkpoints), never in Peter's ordinary save. Autosave is recorded, disabled in the isolated profile, verified off before every window and after every reload, and restored at cleanup; a save inside a window invalidates it. Preexisting rig files and preferences are restored on release. None is left to Peter, a playtest or a check by hand. Each compares the native result with this slice's offline report (`source=offline`), which F4a's analyzer keeps apart from the native verdict. "Locked-PC" checks need the exclusive, focused timing session of the F4 plan's G1; "input-only" checks need only scripted rig actions and state readback.

1. **Declared-load M rows against the offline expectation.** Locked-PC. Setup: FT-F4-Declared checkpoints at 07:00, 11:45, 17:45, 23:30 (day 3) and 03:00 (day 4), candidate build, G1 preflight passed. Steps: run G4's S-stationary windows at speed 1, factor 1/3, with the F4b probe armed; export the union and component times; put this slice's report beside the native report. Expected: native M1–M4 per window recorded; for each window the native union p95 and the offline proxy p95 are listed side by side; every native M2 flag is listed with component and GC; a native component time far above the offline Core time is attributed by component (route search, checkout, `PhysicalPopulation.Update`), not to the Core advancement the offline run measured. Rig-automated: existing RIG-MODSET, FT `load`/`time`/`teleport`; pending RIG-PERF-UNION, RIG-FOREGROUND, RIG-PRIORITY.
2. **Evening calendar holds (S3), the expected failure.** Locked-PC. Setup: the 17:45 checkpoint, speed 1, factor 1/3, 60 FPS cap. Steps: run W-evening with S-stationary for 300 real seconds of clean frames with the probe's per-tick hold postfix on `NativeCityClock.AccountingPrefix`; repeat once. Expected: held real time per window and the longest hold from the hold/resume events; the offline expectation is a held share of about 1.6 % with holds of at most 0.17 s, caused by the 16 citizen events per frame. The report states whether native matches (S3 fail on share, pass on length) or not; either result is recorded, and a matching fail is handed to a scheduler fix slice, not to a relaxed threshold. Rig-automated: existing FT `load`/`time`; pending RIG-PERF-UNION (hold postfix).
3. **Hour-rollover frames (W-accounting).** Locked-PC. Setup: FT-F4-Declared with the three owned shops staffed, stocked and open, checkpoint 11:45. Steps: run W-lunch and W-evening through the 12:00, 13:00, 18:00 and 19:00 native rollovers with the probe armed; pool the frames within one game minute of each rollover. Expected: native M1–M4 for the pooled frames beside the offline W-accounting row (offline p95 0.007 ms, no native accounting work); `AfterNativeTick` and posting-unit times listed as components; S1 = 0. Rig-automated: existing FT `load`/`time`; pending RIG-SHOPFIX, RIG-PERF-UNION.
4. **Native speed rates and fastest-traveller pace.** Locked-PC. Setup: FT-F4-Declared checkpoint 06:00, `GameClockFactor = 1`. Steps: G9 (a) and (b): one game hour each at native speeds 1, 2 and 3 with bodies, 4 game hours at speed 6, a 120-minute time-machine skip, then one whole day at speed 3 from a second copy of the checkpoint; read game minutes per real second per segment from the probe's clock reads. Expected: the measured rates at speeds 1, 2, 3 and 6 recorded, so the offline assumption (linear in the speed) is confirmed or replaced; per game hour pace ratio and held time beside the offline G9 rows (offline: at most 1.17× per hour, 1.035× for the day, 12 % held in the speed-3 hour); S1 and S2 in every segment. Rig-automated: existing FT `time`, RIG-WEEKS `time_machine`; pending RIG-PCCONFIG `GameClockFactor`, RIG-PERF-UNION.
5. **Body counts at the evening kerb.** Input-only. Setup: the 17:45 checkpoint, player at the kerb point 3 m from the first owned shop. Steps: run 5 real minutes at speed 1; read the physical diagnostics line every 60 s and RIG-VIEW `screen_pos list` every 10 s. Expected: median pedestrian and car bodies recorded beside the offline driver's median (40 walkers, 0 cars: the offline fixture has no car routes); a native car count above 0 marks the offline car-body cost as not represented. Rig-automated: existing RIG-VIEW `screen_pos`, FT `load`/`teleport`/`time`; pending none for the counts.

Each check maps to a G check of the plan's section 7: 1 to G4, 2 to G4's W-evening window, 3 to G8, 4 to G9, 5 to G5.

## Follow-up when F4a merges

Offline, not a rig check: validate this report with F4a's analyzer against `contract-v1.json` with `source=offline`, and confirm the analyzer refuses to mix it with native windows in one verdict.

Known drift risk: `tools/perf-gate-offline/PerfGateOffline.csproj` copies the linked Compile items of `tests/PersistentCity.Tests.csproj`. A file newly linked there breaks this tool's build until the same line is added here; no CI builds the tool, so run `dotnet build tools/perf-gate-offline -c Release` when the test project's links change.

## Not covered offline

Presented frames (P1–P4), the complete union, Mono, native seams, dense cars, rivals, start-up and reload latency (L1–L4) and the soak (R1). Those are the F4 plan's G1–G15.
