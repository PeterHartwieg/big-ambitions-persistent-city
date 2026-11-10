# F9h performance: `full` wall time on the PC

Refs #96. F9h runs the acceptance tooling and adds an offline evidence writer. It changes no mod code (no Core, Persistence, Runtime or Compatibility file), so mod work is unchanged and no frame was timed. The standing gate (mod-work p95 < 5 ms, every mod-work spike ≥ 30 ms flagged, whole-frame timing reported separately, #128 a separate investigation) is not affected; the four parked hotspots stay parked. Every number below is tool cost on the PC (Windows 11 x64, 20 logical processors, .NET SDK 10.0.402, Python 3.12.10), run `20261110T131314Z-d23bb925` at `d23bb925`, 10 Nov 2026 13:13–13:39 UTC. None of it is mod work.

## Wall time per stage

Seconds from the run's `acceptance.json` (`seconds`, `childSeconds`, `waitSeconds`; [evidence](../../tools/acceptance/evidence/windows-x64/summary.json)).

| Stage | Wall | Child process | Waiting for other test runs | Runner overhead |
| --- | --- | --- | --- | --- |
| provenance | 0.0 s | – | – | – |
| build | 5.9 s | 5.3 s | 0.6 s | 0.01 s |
| unit (4,003 tests, Debug) | 436.3 s | 435.1 s | 1.1 s | 0.06 s |
| population (from the `unit` TRX) | 0.1 s | – | – | – |
| economy (85-run F6h matrix + `acceptance.py`) | 475.9 s | 474.8 s | 1.1 s | 0.02 s |
| saves (from the `unit` TRX) | 0.1 s | – | – | – |
| movement (F2 harness, 76 arms) | 617.7 s | 482.2 s | 135.4 s | 0.01 s |
| city-s | 8.9 s | 8.4 s | 0.5 s | 0.01 s |
| city-m | 41.4 s | 40.8 s | 0.6 s | 0.01 s |
| **Total** | **26 min 27 s** (1,586.5 s, `time`) | 1,446.6 s | 139.2 s | 0.11 s |

The `wait` column includes the `PC_DOTNET_WAIT` script's own start-up (about 0.5 s per .NET stage).

**Estimate vs measured.** The plan estimated about 25–35 min for `full`; the PC measured 26 min 27 s, or 24 min 7 s without the 135 s spent waiting. The three long stages are `movement` (482 s of harness time), `economy` (475 s) and `unit` (435 s); together they are 96% of the child time.

**Load from other runs.** At the start no `dotnet` or `testhost` process was running and CPU load was 15%. Before `movement` another session's test run on this PC (`dotnet` ×2 and `testhost`) was active; the runner waited 135 s for it, then ran. That run may have shared the CPU with later stages after its test host exited; the stage times are a single run on a shared machine, not a benchmark.

## Evidence writer

`python tools/acceptance/evidence.py record` and `compare` on this run: under 1 s each (Python start, two JSON reads, three small writes). `rig-manifest`: under 1 s.

## Cheap wins

None taken: F9h adds no runner stage and changes no existing tool. Not taken: running `movement` or `economy` in parallel with `unit` (the runner runs one .NET process at a time by design, Decision 1).
