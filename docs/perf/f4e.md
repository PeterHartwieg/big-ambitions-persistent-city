# F4e performance: offline pre-gate expectations

Refs [#91](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/91). Checks: [docs/checks/f4e.md](../checks/f4e.md). Plan: [F4 plan](../plans/f4-perf-gate.md), F4e row, Decisions 3, 4 and 8.

**Offline only.** Windows 11 (10.0.26200) development PC, Intel64 Family 6 Model 183, 20 logical processors, .NET 10.0.13, Release, 9 November 2026, base `3655662` (writer 35). Other sessions' dotnet processes shared the machine between runs; each run was one foreground process. All numbers are .NET Stopwatch time of Core advancement and the F2a presentation driver double. They are not the PC union, not Mono, not presented frames, and nothing here ran in the game or on the rig. The report is `perf-gate-report/v1` with `source=offline` and is never mixed with native windows.

## Mod work of this slice

F4e adds a tool only: no Core, Persistence or Runtime file changes, so in-game mod-work p95, ≥ 30 ms spikes and whole-frame timing of the product are unchanged. No cheap mod-work win applies to a tool slice. The four parked hotspots stay parked. Frame budgets, caps, radius and the clock factor are unchanged (Decision 9).

## Summary of the expectations

| Row | Offline expectation | Worst value |
| --- | --- | --- |
| M1 mod-work p95 < 5 ms | pass | 0.0238 ms (W-morning, 30 FPS) |
| M2 frames ≥ 30 ms | 0 flags in every window | largest frame 4.74 ms (G9b day, speed 3) |
| M3 p99 ≤ 10 ms | pass | 0.118 ms (W-evening, 30 FPS) |
| M4 mean ≤ 1.5 ms | pass | 0.0101 ms (W-morning, 30 FPS) |
| S1 `historicalClockMisses` = 0 | pass | 0 |
| S2 lag ≤ 1 game minute | pass | 1.0 in the time-machine skip (the minute fence); ≤ 0.091 elsewhere |
| S3 held ≤ 1 % and longest hold ≤ 3 s, speeds 1–3 | **fail** on held share | 12.2 % at factor 1 speed 3 (one hour); 1.33–1.57 % at W-evening at the declared pace (30/60 FPS); longest hold 0.167 s |
| S4 recovery ≤ 5 s | pass | 0.033 s |
| S5 fastest traveller ≤ 2.0× per hour, ≤ 1.25× per day | pass | 1.170× (18:00 hour), 1.035× for the day |
| P1–P4, L1–L4, R1 | not established | no presented frames, reference or native start-up offline |

**S3 is the offline finding.** Every hold comes from the frame before it ending on Runtime's frame budget, not on time: frames cost hundredths of a millisecond. `PopulationFrameBudget` allows 16 citizen events per frame at 60 and 120 FPS (its floor) and 32 at 30 FPS, At the declared pace that is about 3,200 citizen events per game minute at 30 or 60 FPS and 6,300 at 120 FPS; at factor 1 and speed 3 it is about 350, so a burst of departures in one minute holds the clock for a few frames. In the evening rush, 272 of 285 held frames at 60 FPS follow a frame that stopped at 16 citizen events, the other 13 at the 16-path admission or the 2 ms limit. At factor 1 and speed 3 the same cap leaves the calendar held for 12 % of the 08:00 hour. The holds are short (at most 0.167 s), the city keeps pace within S5, and no minute is missed. The rig decides the verdict (checks file, check 2); changing the budget to pass the gate is not allowed (Decision 9).

## Method

```
dotnet run --project tools/perf-gate-offline -c Release -- --out build/perf-gate-offline/report-offline.json
```

FT-F4-Declared's shape: the F5h reference city (seed 2026, 957 residents, 1,018 commuters, 576 pool members, 2,551 persons, three player food shops, household finances and wages) aged to 06:00 on day 3, with the F6 change journal and ordinary visits attached. Fixture hash `f769b3a3c9be440f29b954d0b8babe2ab328753d9ad9db125ef7ff152e6bc550`. Each window loads its checkpoint, collects garbage, then runs Runtime's frame shape under the F1 accounting clock with the F2a driver at 48/24 around a kerb point 3 m from the first shop (S-stationary) or along kerb → city centroid → farthest home (S-path, 1.4 m/s). Declared pace: factor 1/3 at speed 1, 0.3043 game minutes per real second; fastest traveller: factor 1, speeds assumed linear. The first window is run once and discarded (JIT), as Decision 3 discards each process's first window. Percentiles are nearest rank over one window's frames; W-accounting pools raw frames. Every window's state hash equals an uninterrupted advance over the same span.

Two full runs, both shown below as run 1 with run 2 where they differ in a reported figure: p95 values moved by at most 0.006 ms, maxima by up to 1.2 ms, frame counts by one frame (the 2 ms soft limit is wall time); state hashes were identical.

## Per window (run 1)

Mod-work = Core frame + driver, ms. Held % is held frames over frames; pace ratio is simulated real time over the unheld clock's.

| Window | Frames | Core p50 | Core p95 | Core p99 | Core max | Mod p95 | Mod p99 | Mod max | Mod mean | ≥ 30 ms | GC 0/1/2 | Max lag | Held % | Pace ratio | Median bodies |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: | ---: | ---: | ---: |
| W-morning 07:00–08:30, 30 FPS | 8,994 | 0.0001 | 0.0140 | 0.0660 | 2.844 | 0.0238 | 0.0772 | 2.865 | 0.0101 | 0 | 2/0/0 | 0.010 | 0.89 | 1.014 | 9 |
| W-morning, 60 FPS | 17,993 | 0.0001 | 0.0036 | 0.0334 | 2.805 | 0.0167 | 0.0468 | 2.821 | 0.0081 | 0 | 2/0/0 | 0.005 | 0.92 | 1.014 | 9 |
| W-morning, 120 FPS | 35,721 | 0.0001 | 0.0002 | 0.0162 | 3.047 | 0.0036 | 0.0185 | 3.052 | 0.0023 | 0 | 2/0/0 | 0.003 | 0.46 | 1.006 | 9 |
| W-lunch 11:45–13:15, 30 FPS | 8,911 | 0.0001 | 0.0022 | 0.0776 | 2.083 | 0.0031 | 0.0789 | 2.086 | 0.0032 | 0 | 2/0/0 | 0.010 | 0.00 | 1.004 | 4 |
| W-lunch, 60 FPS | 17,868 | 0.0001 | 0.0002 | 0.0061 | 2.001 | 0.0014 | 0.0072 | 2.004 | 0.0020 | 0 | 2/0/0 | 0.005 | 0.26 | 1.007 | 4 |
| W-lunch, 120 FPS | 35,598 | 0.0001 | 0.0001 | 0.0035 | 2.817 | 0.0012 | 0.0044 | 2.820 | 0.0014 | 0 | 2/0/0 | 0.003 | 0.13 | 1.003 | 4 |
| W-evening 17:45–19:15, 30 FPS | 9,033 | 0.0001 | 0.0080 | 0.1116 | 3.316 | 0.0164 | 0.1184 | 3.330 | 0.0095 | 0 | 2/0/0 | 0.010 | 1.33 | 1.018 | 41 |
| W-evening, 60 FPS | 18,110 | 0.0001 | 0.0035 | 0.0523 | 2.804 | 0.0118 | 0.0596 | 2.816 | 0.0072 | 0 | 3/0/0 | 0.005 | 1.57 | 1.020 | 40 |
| W-evening, 120 FPS | 35,840 | 0.0001 | 0.0016 | 0.0271 | 2.619 | 0.0089 | 0.0355 | 2.632 | 0.0059 | 0 | 3/0/0 | 0.003 | 0.80 | 1.010 | 40 |
| W-evening S-path, 60 FPS | 18,110 | 0.0001 | 0.0036 | 0.0475 | 2.933 | 0.0115 | 0.0536 | 2.938 | 0.0057 | 0 | 2/0/0 | 0.005 | 1.56 | 1.020 | 17 |
| W-midnight 23:30–01:00, 30 FPS | 8,913 | 0.0001 | 0.0001 | 0.0022 | 0.492 | 0.0002 | 0.0029 | 0.492 | 0.0003 | 0 | 0/0/0 | 0.010 | 0.02 | 1.004 | 0 |
| W-midnight, 60 FPS | 17,826 | 0.0001 | 0.0002 | 0.0002 | 0.560 | 0.0003 | 0.0003 | 0.561 | 0.0003 | 0 | 0/0/0 | 0.005 | 0.03 | 1.004 | 0 |
| W-midnight, 120 FPS | 35,556 | 0.0001 | 0.0001 | 0.0001 | 0.308 | 0.0002 | 0.0002 | 0.309 | 0.0002 | 0 | 0/0/0 | 0.003 | 0.01 | 1.002 | 0 |
| W-idle day 4 03:00–04:30, 30 FPS | 8,911 | 0.0001 | 0.0001 | 0.0002 | 0.065 | 0.0002 | 0.0003 | 0.065 | 0.0002 | 0 | 0/0/0 | 0.010 | 0.00 | 1.004 | 0 |
| W-idle, 60 FPS | 17,821 | 0.0001 | 0.0001 | 0.0002 | 0.071 | 0.0002 | 0.0003 | 0.071 | 0.0002 | 0 | 0/0/0 | 0.005 | 0.00 | 1.004 | 0 |
| W-idle, 120 FPS | 35,551 | 0.0001 | 0.0001 | 0.0001 | 0.085 | 0.0002 | 0.0002 | 0.101 | 0.0002 | 0 | 0/0/0 | 0.003 | 0.00 | 1.002 | 0 |
| W-accounting (pooled, 60 FPS) | 1,600 | 0.0001 | 0.0002 | 0.0389 | 0.178 | 0.0069 | 0.0423 | 0.178 | 0.0035 | 0 | – | – | – | – | – |
| G9a speed 1, 06:00–07:00 | 3,963 | 0.0001 | 0.0015 | 0.0272 | 0.144 | 0.0025 | 0.0294 | 0.146 | 0.0016 | 0 | 0/0/0 | 0.015 | 0.05 | 1.005 | 4 |
| G9a speed 2, 07:00–08:00 | 2,065 | 0.0001 | 0.0331 | 0.0919 | 2.199 | 0.0349 | 0.0949 | 2.201 | 0.0080 | 0 | 1/0/0 | 0.030 | 3.97 | 1.047 | 9 |
| G9a speed 3, 08:00–09:00 | 1,505 | 0.0082 | 0.0489 | 0.0750 | 2.680 | 0.0514 | 0.0784 | 2.685 | 0.0198 | 0 | 2/0/0 | 0.046 | 12.2 | 1.144 | 11 |
| G9a speed 6 hidden, 09:00–13:00 | 2,699 | 0.0001 | 0.0591 | 0.1611 | 2.031 | 0.0591 | 0.1611 | 2.031 | 0.0104 | 0 | 3/0/0 | 0.091 | 2.15 | 1.027 | released |
| G9a skip 120 min, 13:00–15:00 | 121 | 0.0058 | 0.1392 | 0.2085 | 0.261 | 0.1392 | 0.2086 | 0.261 | 0.0354 | 0 | 0/0/0 | 1.000 | 0 | – | released |
| G9b day at speed 3, 06:00–06:00 | 32,639 | 0.0001 | 0.0227 | 0.0809 | 4.736 | 0.0254 | 0.0832 | 4.739 | 0.0058 | 0 | 13/1/0 | 0.046 | 2.94 | 1.035 | 1 |

The slowest single units are food-continuation units of 2–3.3 ms in the morning and evening windows; none reaches 5 ms. Car bodies are 0 everywhere: the F5h planner routes walking legs only.

### Pace and holds per game hour, G9b (factor 1, speed 3, 60 FPS)

| Hour | Pace ratio | Held s | Max lag |
| --- | ---: | ---: | ---: |
| 06:00 | 1.006 | 0.05 | 0.046 |
| 07:00 | 1.066 | 1.35 | 0.046 |
| 08:00 | 1.144 | 3.07 | 0.046 |
| 09:00–15:00 | 1.004–1.035 | ≤ 0.68 | 0.046 |
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
| Full matrix, run 1 | 18.3 s | 4.6 s | 0.7 s | 8.9 GB |
| Full matrix, run 2 | 18.6 s | 4.3 s | 0.6 s | 8.9 GB |
| `--self-test` | 15.0 s | three fixtures | – | – |

Each window, with its uninterrupted replay, takes 0.5–1.3 s. Most of the allocation is the per-window save, read and hash of a 2,551-person city.

## What only the rig can show

Presented frames and P1–P4, the complete union (F4b probe), Mono GC and JIT, native seams (route search, checkout, `AfterNativeTick`, hourly customers), dense car traffic, native speed rates at 2, 3 and 6, start-up and reload latency, and the soak. Those are the checks in [docs/checks/f4e.md](../checks/f4e.md) and section 7 of the plan.
