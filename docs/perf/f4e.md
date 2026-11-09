# F4e performance: offline pre-gate expectations

Refs [#91](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/91). Checks: [docs/checks/f4e.md](../checks/f4e.md). Plan: [F4 plan](../plans/f4-perf-gate.md), F4e row, Decisions 3, 4 and 8.

**Offline only.** Windows 11 (10.0.26200) development PC, Intel64 Family 6 Model 183, 20 logical processors, .NET 10.0.13, Release, 9 November 2026, base `3655662` (writer 35). Other sessions' dotnet processes shared the machine between runs; each run was one foreground process. All numbers are .NET Stopwatch time of Core advancement and the F2a presentation driver double. They are not the PC union, not Mono, not presented frames, and nothing here ran in the game or on the rig. The report is `perf-gate-report/v1` with `source=offline` and is never mixed with native windows.

## Mod work of this slice

F4e adds a tool only: no Core, Persistence or Runtime file changes, so in-game mod-work p95, ≥ 30 ms spikes and whole-frame timing of the product are unchanged. No cheap mod-work win applies to a tool slice. The four parked hotspots stay parked. Frame budgets, caps, radius and the clock factor are unchanged (Decision 9).

## Summary of the expectations

| Row | Offline expectation | Worst value |
| --- | --- | --- |
| M1 mod-work p95 < 5 ms | pass | 0.0629 ms (G9a speed 3); 0.0294 ms among declared windows (W-morning, 30 FPS) |
| M2 frames ≥ 30 ms | 0 flags in every window | largest frame 5.20 ms (W-evening, 60 FPS) |
| M3 p99 ≤ 10 ms | pass | 0.128 ms (W-evening, 30 FPS) |
| M4 mean ≤ 1.5 ms | pass | 0.0243 ms (G9a speed 3) |
| S1 `historicalClockMisses` = 0 | pass | 0 |
| S2 lag ≤ 1 game minute | pass | 1.0 in the time-machine skip (the minute fence); ≤ 0.091 elsewhere |
| S3 held ≤ 1 % and longest hold ≤ 3 s, speeds 1–3 | **fail** on held share | 12.2 % at factor 1 speed 3 (one hour); 1.33–1.57 % at W-evening at the declared pace (30/60 FPS); longest hold 0.167 s |
| S4 recovery ≤ 5 s | pass | 0.033 s |
| S5 fastest traveller ≤ 2.0× per hour, ≤ 1.25× per day | pass | 1.170× (18:00 hour), 1.035× for the day |
| P1–P4, L1–L4, R1 | not established | no presented frames, reference or native start-up offline |

**S3 is the offline finding.** Every hold comes from the frame before it ending on Runtime's frame budget, not on time: frames cost hundredths of a millisecond. `PopulationFrameBudget` allows 16 citizen events per frame at 60 and 120 FPS (its floor) and 32 at 30 FPS. At the declared pace that is about 3,200 citizen events per game minute at 30 or 60 FPS and 6,300 at 120 FPS; at factor 1 and speed 3 it is about 350, so a burst of departures in one minute holds the clock for a few frames. In the evening rush, 272 of 284 held frames at 60 FPS follow a frame that stopped at 16 citizen events, the other 12 at the 16-path admission or the 2 ms limit. At factor 1 and speed 3 the same cap leaves the calendar held for 12 % of the 08:00 hour. The holds are short (at most 0.167 s), the city keeps pace within S5, and no minute is missed. The rig decides the verdict (checks file, check 2); changing the budget to pass the gate is not allowed (Decision 9).

## Method

```
dotnet run --project tools/perf-gate-offline -c Release -- --out build/perf-gate-offline/report-offline.json
```

FT-F4-Declared's shape: the F5h reference city (seed 2026, 957 residents, 1,018 commuters, 576 pool members, 2,551 persons, three player food shops, household finances and wages) aged to 06:00 on day 3, with the F6 change journal and ordinary visits attached. Fixture hash `f769b3a3c9be440f29b954d0b8babe2ab328753d9ad9db125ef7ff152e6bc550`. Each window loads its checkpoint, collects garbage, then runs Runtime's frame shape under the F1 accounting clock with the F2a driver at 48/24 around a kerb point 3 m from the first shop (S-stationary) or along kerb → city centroid → farthest home (S-path, 1.4 m/s). Declared pace: factor 1/3 at speed 1, 0.3043 game minutes per real second; fastest traveller: factor 1, speeds assumed linear. Bodies are commanded at the F2a runner's fixed real-time walking speed (2 m/s, `GridPlanner.ClockRate` game minutes per real second), also on held frames and at every clock factor. The first window is run once and discarded (JIT), as Decision 3 discards each process's first window. Percentiles are nearest rank over one window's frames; W-accounting pools raw frames. Every window's state hash equals an uninterrupted advance over the same span.

Two full runs after review round 1; the tables show run 1. Between the runs p95 values moved by at most 0.023 ms, maxima by up to 1.9 ms, frame counts by one frame (the 2 ms soft limit is wall time); held shares within 0.01 percentage points, pace ratios within 0.001, and state hashes were identical.

## Per window (run 1)

Mod-work = Core frame + driver, ms. Held % is held frames over frames; pace ratio is simulated real time over the unheld clock's.

| Window | Frames | Core p50 | Core p95 | Core p99 | Core max | Mod p95 | Mod p99 | Mod max | Mod mean | ≥ 30 ms | GC 0/1/2 | Max lag | Held % | Pace ratio | Median bodies |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: | ---: | ---: |
| W-morning 07:00–08:30, 30 FPS | 8,994 | 0.0001 | 0.0171 | 0.1021 | 3.055 | 0.0294 | 0.1155 | 3.994 | 0.0112 | 0 | 2/0/0 | 0.010 | 0.89 | 1.014 | 9 |
| W-morning, 60 FPS | 17,993 | 0.0001 | 0.0053 | 0.0503 | 2.680 | 0.0221 | 0.0710 | 2.705 | 0.0092 | 0 | 2/0/0 | 0.005 | 0.92 | 1.014 | 9 |
| W-morning, 120 FPS | 35,722 | 0.0001 | 0.0002 | 0.0186 | 3.239 | 0.0044 | 0.0220 | 3.251 | 0.0026 | 0 | 2/0/0 | 0.003 | 0.46 | 1.006 | 9 |
| W-lunch 11:45–13:15, 30 FPS | 8,911 | 0.0001 | 0.0031 | 0.0843 | 2.826 | 0.0041 | 0.1003 | 2.834 | 0.0039 | 0 | 2/0/0 | 0.010 | 0.00 | 1.004 | 4 |
| W-lunch, 60 FPS | 17,868 | 0.0001 | 0.0002 | 0.0081 | 2.232 | 0.0018 | 0.0096 | 2.236 | 0.0022 | 0 | 2/0/0 | 0.005 | 0.26 | 1.007 | 4 |
| W-lunch, 120 FPS | 35,598 | 0.0001 | 0.0002 | 0.0046 | 2.641 | 0.0016 | 0.0059 | 2.646 | 0.0016 | 0 | 2/0/0 | 0.003 | 0.13 | 1.003 | 4 |
| W-evening 17:45–19:15, 30 FPS | 9,033 | 0.0001 | 0.0124 | 0.1209 | 3.901 | 0.0256 | 0.1279 | 3.915 | 0.0109 | 0 | 2/0/0 | 0.010 | 1.33 | 1.018 | 39 |
| W-evening, 60 FPS | 18,109 | 0.0001 | 0.0041 | 0.0572 | 5.181 | 0.0140 | 0.0650 | 5.197 | 0.0076 | 0 | 3/0/0 | 0.005 | 1.57 | 1.020 | 40 |
| W-evening, 120 FPS | 35,839 | 0.0001 | 0.0019 | 0.0353 | 3.816 | 0.0144 | 0.0471 | 4.012 | 0.0069 | 0 | 3/0/0 | 0.003 | 0.79 | 1.010 | 39 |
| W-evening S-path, 60 FPS | 18,110 | 0.0001 | 0.0040 | 0.0545 | 3.020 | 0.0141 | 0.0593 | 3.032 | 0.0061 | 0 | 2/0/0 | 0.005 | 1.57 | 1.020 | 16 |
| W-midnight 23:30–01:00, 30 FPS | 8,913 | 0.0001 | 0.0001 | 0.0024 | 0.450 | 0.0002 | 0.0029 | 0.450 | 0.0003 | 0 | 0/0/0 | 0.010 | 0.02 | 1.004 | 0 |
| W-midnight, 60 FPS | 17,826 | 0.0001 | 0.0001 | 0.0002 | 0.434 | 0.0002 | 0.0003 | 0.434 | 0.0003 | 0 | 0/0/0 | 0.005 | 0.03 | 1.004 | 0 |
| W-midnight, 120 FPS | 35,556 | 0.0001 | 0.0001 | 0.0001 | 0.470 | 0.0002 | 0.0002 | 0.470 | 0.0002 | 0 | 0/0/0 | 0.003 | 0.01 | 1.002 | 0 |
| W-idle day 4 03:00–04:30, 30 FPS | 8,911 | 0.0001 | 0.0001 | 0.0002 | 0.072 | 0.0002 | 0.0004 | 0.073 | 0.0002 | 0 | 0/0/0 | 0.010 | 0.00 | 1.004 | 0 |
| W-idle, 60 FPS | 17,821 | 0.0001 | 0.0001 | 0.0002 | 0.080 | 0.0002 | 0.0003 | 0.080 | 0.0002 | 0 | 0/0/0 | 0.005 | 0.00 | 1.004 | 0 |
| W-idle, 120 FPS | 35,551 | 0.0001 | 0.0001 | 0.0002 | 0.081 | 0.0002 | 0.0003 | 0.142 | 0.0002 | 0 | 0/0/0 | 0.003 | 0.00 | 1.002 | 0 |
| W-accounting (pooled, 60 FPS) | 1,600 | 0.0001 | 0.0002 | 0.0452 | 0.213 | 0.0069 | 0.0496 | 0.214 | 0.0036 | 0 | – | – | – | – | – |
| G9a speed 1, 06:00–07:00 | 3,963 | 0.0001 | 0.0019 | 0.0353 | 0.145 | 0.0035 | 0.0382 | 0.177 | 0.0022 | 0 | 0/0/0 | 0.015 | 0.05 | 1.005 | 6 |
| G9a speed 2, 07:00–08:00 | 2,065 | 0.0001 | 0.0333 | 0.0960 | 2.051 | 0.0366 | 0.0971 | 2.056 | 0.0093 | 0 | 1/0/0 | 0.030 | 3.97 | 1.047 | 16 |
| G9a speed 3, 08:00–09:00 | 1,505 | 0.0102 | 0.0586 | 0.0931 | 3.725 | 0.0629 | 0.0969 | 3.735 | 0.0243 | 0 | 2/0/0 | 0.046 | 12.2 | 1.145 | 19 |
| G9a speed 6 hidden, 09:00–13:00 | 2,699 | 0.0001 | 0.0606 | 0.1765 | 2.293 | 0.0607 | 0.1766 | 2.294 | 0.0115 | 0 | 3/0/0 | 0.091 | 2.15 | 1.026 | released |
| G9a skip 120 min, 13:00–15:00 | 121 | 0.0074 | 0.1361 | 0.1971 | 0.248 | 0.1362 | 0.1972 | 0.249 | 0.0357 | 0 | 0/0/0 | 1.000 | 0.00 | – | released |
| G9b day at speed 3, 06:00–06:00 | 32,639 | 0.0001 | 0.0240 | 0.0848 | 4.693 | 0.0290 | 0.0903 | 4.707 | 0.0073 | 0 | 13/1/0 | 0.046 | 2.94 | 1.034 | 4 |

The slowest single units are citizen and food-continuation units of 2–3.8 ms in the morning, evening and G9 windows, and one food unit of 5.14 ms in W-evening at 60 FPS (run 1; 3.3 ms in run 2). Car bodies are 0 everywhere: the F5h planner routes walking legs only.

### Pace and holds per game hour, G9b (factor 1, speed 3, 60 FPS)

| Hour | Pace ratio | Held s | Max lag |
| --- | ---: | ---: | ---: |
| 06:00 | 1.006 | 0.05 | 0.046 |
| 07:00 | 1.066 | 1.37 | 0.046 |
| 08:00 | 1.145 | 3.08 | 0.046 |
| 09:00–15:00 | 1.004–1.035 | ≤ 0.67 | 0.046 |
| 16:00 | 1.073 | 1.50 | 0.046 |
| 17:00 | 1.097 | 2.03 | 0.046 |
| 18:00 | 1.170 | 3.63 | 0.046 |
| 19:00 | 1.078 | 1.62 | 0.046 |
| 20:00 | 1.073 | 1.52 | 0.046 |
| 21:00–05:00 | 1.004–1.008 | ≤ 0.08 | 0.046 |

An hour without a single hold still has a ratio of about 1.004: `AccountingClock.Limit` clips a tick at the minute fence and the rest of that tick's allowance is lost (a boundary clip, not a hold). The whole day is 1.035× the unheld clock.

## Tool cost

| Run | Total | Fixture and 9 checkpoints | Discarded warm-up window | Allocated |
| --- | ---: | ---: | ---: | ---: |
| Full matrix, run 1 | 20.0 s | 4.9 s | 0.8 s | 8.9 GB |
| Full matrix, run 2 | 19.0 s | 4.7 s | 0.8 s | 8.9 GB |
| `--self-test` | 15.0 s | three fixtures | – | – |

Each window, with its uninterrupted replay, takes 0.5–1.3 s. Most of the allocation is the per-window save, read and hash of a 2,551-person city.

## What only the rig can show

Presented frames and P1–P4, the complete union (F4b probe), Mono GC and JIT, native seams (route search, checkout, `AfterNativeTick`, hourly customers), dense car traffic, native speed rates at 2, 3 and 6, start-up and reload latency, and the soak. Those are the checks in [docs/checks/f4e.md](../checks/f4e.md) and section 7 of the plan.
