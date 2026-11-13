# F4e offline pre-gate

Offline expectations for the F4 gate's M and S rows ([F4 plan](../../docs/plans/f4-perf-gate.md), Decisions 3 and 8). It writes `perf-gate-export/v1` with `source=offline` and runs `tools/perf-gate/analyze.py` against `contract-v1.json`. It runs before the rig batch and never replaces it.

```
dotnet run --project tools/perf-gate-offline -c Release -- --out build/perf-gate-offline [--seed N] [--only W-evening]
dotnet run --project tools/perf-gate-offline -c Release -- --self-test
```

Output: `offline.export.json` (every frame), `offline.report.json` and `offline.report.md`. Python 3 must be on the path (`$PYTHON`, else `python3`, or `python` on Windows).

The fixture is the F5h reference city from the unit tests, aged to day 3, with three player food shops. Frames follow Runtime's `PopulationFrameBudget` at 30, 60 and 120 FPS with the F2a presentation driver; the matrix covers Decision 3's windows and G9's fastest-traveller sequence. Each window must replay to the same state hash when run uninterrupted.

**Offline windows can never pass the native gate.** The rig receipts are `n/a` offline, so every aggregated row reads `not established` and the overall verdict is never `pass`. Each window still carries its own M1, M3, M4 and S1–S4 values as expectations. `whole_frame_ms` equals `mod_work_ms`, because there is no game frame. The `offline` block holds extra detail the analyzer ignores.

Limits: .NET Stopwatch time, not Mono or the game. Native seams are doubles. No union probe, presented frames or dependency-only reference. Car bodies stay at 0. W-accounting has no native hourly customers or ownership transfer.
