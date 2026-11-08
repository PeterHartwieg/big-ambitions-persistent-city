# F2c perf — walkers follow logical progress, 8 November 2026

Offline component timings on the development PC (20 logical cores), .NET 10 Release test build, measured on the review-round-2 tree (walker destination bounded by the logical pose). These are not in-game mod-work numbers; in-game mod-work p95, ≥ 30 ms spikes and whole-frame timing come from the batched rig (check 5 in [the F2c checks](../checks/f2c.md)).

## Follower cost per body per frame

`PhysicalWalkerFollowTests.MeasureFollowerArithmeticPerBody` (opt-in, `PC_F2C_MEASURE=<file>`): one walker's per-frame arithmetic (`SampleJourney`, `WalkerFollow.Advance`, `RouteMetres`, `Sample`, `PaceFactor`) on a two-leg route, 200,000 calls.

| Run | Mean per call |
| --- | ---: |
| 1 | 0.797 µs |
| 2 | 0.733 µs |
| 3 | 0.758 µs |

At 48 walkers that is about 0.04 ms per frame. It replaces the per-body `ObservePhysicalJourney` rebase and lease observation for walk legs; the native calls are `Pace` per frame, `SetTarget` once per earlier leg and every 0.5 m on the logical pose's leg, and `OnScreen` only for a body being corrected. Each re-aim is one synchronous NavMesh path request, preceded by a `SamplePosition` projection for a pose between corners. Their rate scales with game speed: at native walking pace about four per second per walker at speed 1, and proportionally more at higher speeds (about six times as many at 6×, so roughly 1,150 per second at 48 walkers). `pc movement status` counts them (`path_requests`); rig check 5 records the count and the game speed per window.

## `PhysicalPopulation.Update` at 48 walkers, before and after

`PhysicalWalkerPerfTests.MeasureUpdateWithFortyEightWalkers` (opt-in): the production `PhysicalPopulation` on the native scene doubles with 48 residents walking six-leg routes inside the radius, a native-movement double that steps each walking body toward its target at native pace (a third at 0.85×), the city advanced 1/180 game minute per 1/60 s frame; 120 warm-up frames, then 2,400 timed `Update` calls. The same file was run on the base (`ec186f9`, walk legs leased) in a temporary worktree and on this branch.

| Build | Run | Bound | p50 | p95 | max | ≥ 30 ms |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| base `ec186f9` (walk leases) | 1 | 48 | 0.0568 ms | 0.0671 ms | 2.5968 ms | 0 |
| base `ec186f9` (walk leases) | 2 | 48 | 0.0510 ms | 0.0620 ms | 0.3160 ms | 0 |
| F2c | 1 | 48 | 0.0671 ms | 0.0789 ms | 0.2750 ms | 0 |
| F2c | 2 | 48 | 0.0595 ms | 0.0709 ms | 2.5761 ms | 0 |
| F2c | 3 | 48 | 0.0586 ms | 0.0714 ms | 2.6898 ms | 0 |

Result: about 0.005–0.01 ms higher at p50 and p95 (well under the 5 ms gate). The scene double's `SetTarget` is a field write, so the re-aim's native path requests are not timed here; rig check 5 measures them. The single-frame maxima of 2.6–2.7 ms appear in both builds (JIT/GC), not in every run. No ≥ 30 ms sample. The plan expected lower or equal mod-work; the double makes `ObservePhysicalJourney` cheap, so the removed Core writes do not show here. Their larger in-game effect (no traffic `Reconsider` from walkers) is F2d's.

No cheap wins were taken: nothing in the follower path showed up as a hotspot.

## Lag distribution and corrections in the harness

`dotnet run --project tools/movement-equivalence -c Release -- --arms walking/,focus/stationary --no-self-checks` (957 residents, two simulated days plus the 03:00 sample, seed 0). The double has no camera, so every correction is an off-screen move. Its walker destination is bounded by the logical pose, as in Runtime. Native lag model: seeded walker pace 0.8–1.0× and a 3 s block every 20 s of real time.

| Arm | E1 vs bodies-off | Walker frames | Lag p50 | Lag p95 | Lag max | Moves | Lag releases | Handoffs | Tool s |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| walking/stationary | equal | 8,734,271 | 2.05 m | 6.85 m | 8.03 m | 1,239 | 0 | 0 | 5.0 |
| walking/sweep | equal | 6,571,958 | 2.05 m | 6.80 m | 8.03 m | 948 | 0 | 0 | 3.7 |
| walking/away-return | equal | 6,388,184 | 2.05 m | 6.85 m | 8.03 m | 979 | 0 | 0 | 4.2 |
| walking/caps-1 | equal | 273,028 | 1.95 m | 6.70 m | 8.03 m | 36 | 0 | 0 | 1.9 |
| walking/caps-256 | equal | 31,743,046 | 2.10 m | 6.95 m | 8.03 m | 5,132 | 0 | 0 | 13.5 |
| walking/lag-ideal | equal | 8,621,516 | 0.10 m | 0.15 m | 0.15 m | 0 | 0 | 0 | 5.0 |
| walking/failures-50pct | equal | 8,599,121 | 2.10 m | 6.90 m | 8.03 m | 1,344 | 0 | 0 | 5.1 |
| walking/cadence-30fps | equal (vs plain-30) | 4,369,686 | 2.10 m | 6.90 m | 8.07 m | 1,246 | 0 | 0 | 3.3 |
| walking/reload-mid-journey | equal | 8,739,937 | 2.05 m | 6.90 m | 8.03 m | 1,243 | 0 | 0 | 5.5 |
| walking/hidden-travel | equal (vs plain-hidden) | 8,425,294 | 2.05 m | 6.85 m | 8.03 m | 1,188 | 0 | 0 | 5.0 |
| focus/stationary (mixed city, cars still leased) | differs (expected until F2d) | 8,938,537 | 2.00 m | 6.60 m | 8.03 m | 732 | 0 | 204 | 10.7 |

Corrections per hour: the default walking arm makes 1,239 moves in about 51 simulated hours, about 24 per game hour at 48 walkers (0.5 per walker-hour). Lag releases: 0 in every arm.

Against the E3 walker target (p95 ≤ 2 m): met with ideal bodies (0.15 m), not met under the harness's Native lag model (p95 6.85 m). That model blocks every walker for 3 s of every 20 s (6 m at 2 m/s), which catch-up at up to 1.5× repays in about 12 s, so the walker spends much of each cycle several metres behind. This is the harness's pessimistic stand-in for native crossings and crowds, not a native measurement. Native lag, and whether 1.5 m / 8 m / 1.5× need tuning, come from rig checks 1 and 5.
