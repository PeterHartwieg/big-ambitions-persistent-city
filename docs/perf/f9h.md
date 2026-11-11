# F9h performance: `full` wall time on the PC and the Mac

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

## Same-head re-record at `b3e5a910`

Run `20261110T201123Z-b3e5a910`, 10 Nov 2026 20:11–20:34 UTC, same PC and SDK. Tool cost only; F8b-g's mod changes are not timed here (no frame was timed).

| Stage | Wall | Child process | Waiting | Runner overhead | First record (wall) |
| --- | --- | --- | --- | --- | --- |
| provenance | 0.0 s | – | – | – | 0.0 s |
| build | 4.9 s | 4.3 s | 0.5 s | 0.01 s | 5.9 s |
| unit (4,128 tests, Debug) | 391.8 s | 391.2 s | 0.5 s | 0.06 s | 436.3 s |
| population (from `unit` TRX) | 0.1 s | – | – | – | 0.1 s |
| economy | 446.2 s | 445.7 s | 0.5 s | 0.01 s | 475.9 s |
| saves (from `unit` TRX) | 0.1 s | – | – | – | 0.1 s |
| movement | 467.8 s | 467.2 s | 0.6 s | 0.01 s | 617.7 s |
| city-s | 10.0 s | 8.7 s | 1.2 s | 0.01 s | 8.9 s |
| city-m | 43.8 s | 43.2 s | 0.6 s | 0.01 s | 41.4 s |
| **Total** | **22 min 45 s** (1,364.5 s stage sum) | 1,360.2 s | 4.0 s | 0.11 s | 26 min 27 s |

**Load from other runs.** The wait script found no other test run before any stage (about 0.5 s per stage is the script's start-up). Other agent sessions were active on the PC, so the times are a single run on a shared machine, not a benchmark. The 150 s drop in `movement` is mostly the first record's 135 s wait; the rest of the differences are within run-to-run noise.

## Mac record at `b5f2a1de` (F9h-mac)

Run `20261110T215417Z-b5f2a1de`, 10 Nov 2026 21:54:17 to 22:21:06 UTC, on the Mac (Apple M1 Pro, 8 cores, macOS 26.5.2, .NET SDK 10.0.400, Python 3.15.0). Tool cost only; no mod code changed and no frame was timed, so the mod-work gate is not affected. The record at this commit keeps wall seconds per stage only. Child and wait seconds are not in it, so they are not reported.

| Stage | Mac wall | PC wall at `b3e5a910` |
| --- | --- | --- |
| provenance | 0.0 s | 0.0 s |
| build | 20.9 s | 4.9 s |
| unit (4,128 tests, Debug) | 584.2 s | 391.8 s |
| population | 0.1 s | 0.1 s |
| economy | 496.5 s | 446.2 s |
| saves | 0.1 s | 0.1 s |
| movement | 450.2 s | 467.8 s |
| city-s | 8.3 s | 10.0 s |
| city-m | 48.2 s | 43.8 s |
| **Total** | **26 min 49 s** (1,608.5 s stage sum; 1,609 s by the clock) | 22 min 45 s |

**Load from other runs.** No `dotnet` or `testhost` process was running when the run started. Load average was 4.4 at the start and 5.6 at the end on 8 cores, so other work shared the Mac for the whole run. `unit` is the stage that lost most against the PC (about 190 s), which fits a busier, smaller machine. These are single runs on shared machines, not a benchmark.

`evidence.py record` and `compare` on the Mac: under 1 s each.

## Evidence writer

`python tools/acceptance/evidence.py record` and `compare` on this run: under 1 s each (Python start, two JSON reads, three small writes). `rig-manifest`: under 1 s.

## Cheap wins

None taken: F9h adds no runner stage and changes no existing tool. Not taken: running `movement` or `economy` in parallel with `unit` (the runner runs one .NET process at a time by design, Decision 1).
