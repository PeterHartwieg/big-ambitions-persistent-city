# City-audit F1 measurements — 3 November 2026

Offline only, base `bd99eb3`, final implementation `98e2afd`. Run:
`dotnet run --project tools/accounting-backlog-regression/AccountingBacklogRegression.csproj --configuration Release`.
The foreground run uses .NET 10 on this Windows host, after the shared dotnet gate. Raw output
is retained in [f1-accounting-backlog.txt](f1-accounting-backlog.txt).

## Throughput, lag and recovery

The fixture executes the real Core scheduler, current held-context food policy, household
ledger and writer 25. Native clock/stock/order APIs and routes are doubles. One logical day
starts at minute 1440 and ends after draining minute 2880. Price inputs change at integer
timestamps, finite stock decrements on successful orders and every 31st order loses its
acknowledgement. The synthetic native request is 30 game minutes per wall second, independent
of the chosen FPS; it is an accelerated scheduler fixture, **not a different configured native
calendar factor or a normal-play throughput claim**. The skip arm requests 120 minutes/tick.
Both use the same production minute fence and readiness contract.

| Detached arm | FPS / units / soft ms | Frames / units | Held ticks | Max lag, min | Injected stall + recovery, simulated s | Frame p50 / p95 / max, ms | >=30 ms |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 957 residents / 1,914 consumers | 30 / 1 / 0.01 | 30,543 / 28,564 | 27,662 | <1 | 2 | 0.0075 / 0.0274 / 2.8245 | 0 |
| same city | 60 / 16 / 0.5 | 5,766 / 28,564 | 1,446 | 0.5 | 2 | 0.0009 / 0.2508 / 14.8037 | 0 |
| same city | 120 / 128 / 2 | 7,456 / 28,564 | 256 | 0.25 | 2 | 0.0004 / 0.1894 / 4.4055 | 0 |
| same city, skip | 30 / 1 / 0.01 | 30,543 / 28,564 | 27,662 | <1 | 2 | 0.0025 / 0.0084 / 15.0445 | 0 |
| stress: 2,000 / 4,000 | 30 / 1 / 0.01 | 59,007 / 56,879 | 56,126 | <1 | 10 | 0.0023 / 0.0090 / 14.0765 | 0 |

Reported recovery includes the deliberately withheld 60/120/240 frames (or 300 stress
frames); none required an additional simulated frame after that stall to drain the admitted
target. CPU elapsed seconds for the four supported timing arms: 0.3432 / 0.3671 / 0.2682 /
0.1515; unit rates 83,235 / 77,804 / 106,503 / 188,486 per harness CPU second. Stress elapsed
0.2591 s. These CPU rates include harness overhead and do not predict Unity/Mono capacity.

The supported ordinary arms take **1,018.1 / 96.100 / 62.133 simulated wall seconds** for the
logical day (skip 1,018.1; stress 1,966.9). An unclipped ordinary request at the accelerated
30-minute/second fixture pace would take 48 s. This deliberately exposes the strong pacing
cost of tiny work budgets and minute fences; bounded lag is **not** evidence of adequate
throughput at that artificial pace. These arms establish equal outcomes under backpressure,
not a performance/normal-calendar acceptance pass. Native baseline fence cost and workload
hold duration must be measured separately by the rig.

Every supported arm has **969 confirmed purchases, 971 distinct native-double orders, 164
recorded missed meals**, prepared spending **28,523,951,554,560 exact Money units**, aggregate
balance **87,799,646,683,922,432 units** and held funds **64,424,509,440 units**. Two original
orders still await confirmation; they are not replayed. Complete writer-25 bytes (including
outcomes, wallets, holds and histories) and ordered purchase IDs/timestamps/amounts equal the
reference. Historical-clock missed demand is zero. In-flight and midnight reload equivalence
is separately checked outside these timing arms.

These are **complete detached harness frame costs** (clock guard, simulator batch, market
doubles, snapshot publication), not feature-only timers. They exclude construction, save/read
and final serialization. No save occurs in a timing arm. They are neither complete mod-work
nor whole-game/presented-frame timings: Unity routing, native work, physical population,
rendering, optional-stop/closure refresh, native campaigns and GUI are absent. The native
mod-work p95 <5 ms gate and separate whole-frame/stall watch remain **pending**. Do not use
these p95s as a native gate pass.

Four preliminary windows accidentally included midnight save/read: maxima **654.2654,
559.9204, 398.6659 and 142.5552 ms** (one >=30 ms sample in each). They are flagged and invalid
as clean gameplay samples; the final arms above contain no saves. They do not justify a
serialization rewrite. The four parked hotspots remain parked.

The required matrix also flags inherited bulk/cold timings outside these F1 frame windows:
D1's 20,000-entry cold build **43.9751 ms**; the schema-25 957x2 saturated-history write/read/
activation maxima **257.8943/591.7442/235.029 ms**, each with **3/3 samples >=30 ms** in that
standalone arm. These remain bulk/startup evidence, not F1 gameplay frames or attributed new
regressions. Their existing flags do not become a native frame-gate pass or authorize a
parked serialization rewrite.

## Taken low-risk win

Scale the existing frame caps by unscaled elapsed time instead of giving every rendered
frame sixteen citizen starts. The matched real-Core 960-resident same-minute queue at
30 simulated FPS drains in **60 -> 30 frames**, **2 -> 1 simulated seconds**, with byte-identical
final state. Citizen-start capacity is **480 -> 960/s** at 30 FPS; 60 FPS retains 960/s and
120 FPS uses eight starts/frame for the same rate. This removes the artificial low-FPS cap
when the CPU time budget permits it; it is not a whole-frame CPU saving or a promise to fit
arbitrary work into 4 ms. Long-stall caps prevent an unlimited next-frame burst.

The new pure clock limiter performs **100,000 calls in 0.6375 ms, 0 allocated bytes** in the
warm component probe. Its float predecessor uses a value union rather than temporary arrays.
There are no new normal-tick strings, reflection, scans, polling loops or diagnostic object
construction. Native input/read/hotspot rewrites remain with their existing owners.

The visible overload formatter also has a measured low-risk cache win. Compiling the actual
`AccountingStatus.cs` against the clock/UI doubles, 1,200 requests (two GUI requests/frame at
60 simulated FPS for ten seconds) cost **499,200 -> 16,640 B** and **1,200 -> 40 text builds**
when changing from a rebuild on every request to the 4 Hz cache. The before arm explicitly
invalidates the cache each request to reproduce the original formatter. Refusal text is a
constant and appears immediately; normal closed UI does not call the formatter. This is a
formatter allocation comparison, not native GUI or complete mod-work timing.
No further cheap safe win was identified in the touched accounting boundary.
