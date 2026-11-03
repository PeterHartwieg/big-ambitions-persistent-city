# City-audit F1 measurements — 3 November 2026

Initial offline evidence below: base `bd99eb3`, implementation `98e2afd`. R1 corrections and current limits follow under [R1 review corrections](#r1-review-corrections). Run:
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
the pre-review 120 FPS policy used eight starts/frame, reducing the old capacity from
1,920 to 960 starts/s and 240 to 120 soft ms/s. R1 restores the legacy 16-start / 2 ms floor
and its 1,920 starts/s / 240 soft ms/s capacity. This removes the artificial low-FPS cap
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

## R1 review corrections

3 November 2026, code `6df891d` / `8e5c8a9`, offline only. The initial rows above are retained historical
evidence, superseded for the corrected fence by the [R1 raw output](f1-accounting-backlog-r1.txt).
`ClockBaseline` compares a full zero-work logical day with identical native float addition,
configured requested rate and FPS: policy 0 = uncapped reference, 2 = frozen pre-review minute
fence, 1 = corrected production limiter. The only work here is clock arithmetic; these are
simulated wall durations/tick counts, never native frame costs.

| FPS | game min/s | Uncapped ticks | Pre-review ticks | Corrected ticks | Remaining extra frames/game minute | Remaining slowdown vs uncapped |
| --- | --- | --- | --- | --- | --- | --- |
| 30 | 1/3 | 129,597 | 131,472 | 130,033 | 0.303 | 0.336% |
| 30 | 2 (6x) | 21,601 | 23,856 | 22,632 | 0.716 | 4.773% |
| 60 | 1/3 | 259,225 | 261,528 | 260,089 | 0.600 | 0.333% |
| 60 | 2 (6x) | 43,201 | 45,864 | 44,448 | 0.866 | 2.887% |
| 120 | 1/3 | 518,489 | 521,016 | 519,576 | 0.755 | 0.210% |
| 120 | 2 (6x) | 86,402 | 89,184 | 87,744 | 0.932 | 1.553% |

One-frame-only crossing waste is removed. Remaining clipping depends on request phase and
native float accumulation: measured **0.303–0.932 extra frames/game minute**, rather than
assuming exactly half a frame. At one-third pace the added simulated wall duration for a
whole logical day is **14.53/14.40/9.06 seconds** at 30/60/120 FPS. At the 6x request it is
**34.37/20.78/11.18 seconds**. Calendar factor is unchanged; boundary clips/withheld minutes
are counted and now trigger the player-facing pacing notice as well as the panel.

**Skip minimum cost:** for eight logical hours, 120 requested minutes/tick and zero work,
the historical hour limiter uses **16 ticks / 0.267 simulated seconds at 60 FPS**; the
pre-review minute limiter uses **960 / 16.000 s**; the corrected limiter uses **481 / 8.017 s**.
The remaining minute-epoch floor is approximately **one tick/game minute** plus endpoint
alignment: still **30.06x** the old hour-limiter minimum, and approximately half the pre-review
minimum. This deliberate cost preserves minute-stamped input publication; empty-epoch jumps
were not added without a proof of all due inputs. It is explicitly reported, not a skip
latency pass. The next rig must measure it together with actual work and time-machine behavior.

**120 FPS regression removed:** retain the pre-F1 16 starts / 128 units / 2 soft ms per frame,
so capacity remains **1,920 starts/s / 240 ms/s**. At 60 FPS it remains 960 / 120; at 30 FPS the
existing win remains 480 -> 960 starts/s and 60 -> 120 soft ms/s. The 4 ms allowance at 30 FPS
leaves only 1 ms under the complete mod-work p95 gate and cannot certify that gate. No parked
hotspot is rewritten. Cached visible text still measures 499,200 -> 16,640 B / 1,200 -> 40 builds.

R1 supported/stress throughput and complete **detached** frame costs are retained in the raw
output; native mod-work and whole-frame costs remain separate pending proof. Native timing
must flag every >=30 ms mod-work sample and keep whole-frame stalls separate.

## R1 final foreground matrix repeat

The corrected HEAD also passed both required nested wrappers and the standalone F1 tool.
[Nested repeats](f1-accounting-backlog-r1-repeat.txt) and
[final standalone raw output](f1-accounting-backlog-r1-final.txt) retain the repeated measurements.
All measured arms exclude save/read and native/Unity work, with the same inputs and economic
assertions as above. Complete native mod-work and separate whole-frame costs remain unverified.

| Final detached arm | Frames / held ticks | Max lag, game min | Stall and recovery, s | Frame p50 / p95 / max, ms | >=30 ms |
| --- | --- | --- | --- | --- | --- |
| 30 FPS / 1 unit | 29,537 / 28,096 | 0.999999999999773 | 2 | 0.0056 / 0.0118 / 4.0703 | 0 |
| 60 FPS / 16 units | 4,436 / 1,555 | 0.500003814697038 | 2 | 0.0049 / 0.0839 / 2.6435 | 0 |
| 120 FPS / 128 units | 6,016 / 255 | 0.250003814697038 | 2 | 0.0005 / 0.2280 / 2.5934 | 0 |
| skip 30 FPS / 1 unit | 29,537 / 28,096 | 0.999999999999773 | 2 | 0.0039 / 0.0169 / 3.9963 | 0 |
| stress 2,000 / 30 FPS | 58,053 / 56,612 | 0.999999999999773 | 10 | 0.0028 / 0.0092 / 5.8933 | 0 |

Final F1 >=30 ms samples: **0**. The corrected precheck and first nested repeat also recorded zero. The first nested stress maximum was 14.8795 ms. Native frame gates remain pending regardless of these detached results.
Across the six R1 runs (30 clean arm windows), >=30 ms samples total **0**, maximum **16.5706 ms**. All are detached, save-free windows.
The zero-work tick counts and remaining baseline/skip costs above are unchanged in the repeats.

The required standalone 957x2 saturated-history bulk arm also flags **289.8408/583.4523/340.8855 ms** write/read/activation maxima, each **3/3 samples >=30 ms**, and **1171.2615 ms** migration. These are bulk serialization/startup costs, separate from F1 gameplay frame windows; the parked serialization hotspot remains unchanged. They are not a native frame-gate pass.

## R2 notice/recovery corrections - 3 November 2026 (offline)

Code `a8c6cfc`: the minute limiter, service budgets, configured calendar factor and native
accounting rules are unchanged. Zero-work fences now increment their existing diagnostics
without extending the three-second workload-notice tail. A constant 22-pixel-high single-line
status replaces the 760x82 card for baseline quantization. No percentage/window calculation,
per-frame scan, reflection or new diagnostic allocation is introduced. Actual workload holds
and refusal retain the prominent notice; startup preparation still explains the clock hold.

**Taken low-risk win:** 1,200 warm baseline text requests over ten simulated seconds now use
**0 B / 0 text builds**, versus **16,640 B / 40 formatter builds** for the former cached status.
[Raw corrected-source output](f1-accounting-backlog-r2-focused.txt) includes the measurement,
180 empty ordinary minutes each at 1/3, 1 and 2 game minutes/second, then an actual workload
hold. Native GUI/Unity cost is excluded; removal of the background box is not a measured
native CPU claim. Complete native mod-work and separate whole-frame costs remain pending.
The four parked hotspots remain unchanged.

The focused corrected run also reconciles the same 957-resident economics and held/midnight
reloads, with <1 minute lag and 2 s injected supported / 10 s stress recovery. Its ordinary
30/60/120 p95 is **0.0333/0.1404/0.2172 ms**, maxima **15.8939/4.5423/2.9441 ms**; skip
**0.0040/1.5204 ms**, stress **0.0051/8.3109 ms** (p95/max). Zero >=30 ms F1 samples in that
clean detached run. Full required-matrix repeats will be recorded after completion. Native
rig proof remains the Sol-r2-1 merge blocker; no game or rig was used by this fixer.

**R2 required-matrix spike flag:** the first nested repeat records **66.3905 ms** at
120 simulated FPS / 128 units / 2 soft ms, **one >=30 ms sample**, p95 **0.4610 ms**.
[Full raw repeat](f1-accounting-backlog-r2-first-repeat.txt) is retained. It is a save-free
complete detached frame, not a native frame and not invalidated or discarded as a warmup.
The sample is unattributed; a low p95 does not excuse it. R2 changes no Core frame work,
and this does not authorize a rewrite of the parked hotspots or establish a native cause.
The detached spike condition is not passed for this run; native gates remain unverified.

## R2 final foreground matrix repeat

All required foreground commands and nested suites passed their functional assertions.
[All retained R2 repeats](f1-accounting-backlog-r2-repeats.txt) and
[final standalone output](f1-accounting-backlog-r2-final.txt) preserve measurements, source
notice/restart assertions and the unchanged zero-work clock baselines. These are save-free
complete detached frame costs; native/Unity/GUI work and whole-game frames remain excluded.

| Final detached arm | Frames / held ticks | Max lag, game min | Injected stall + recovery, s | Frame p50 / p95 / max, ms | >=30 ms |
| --- | --- | --- | --- | --- | --- |
| 30 FPS / 1 unit | 29,537 / 28,096 | 0.999999999999773 | 2 | 0.0066 / 0.0188 / 4.7865 | 0 |
| 60 FPS / 16 units | 4,438 / 1,557 | 0.500003814697038 | 2 | 0.0055 / 0.1338 / 4.6588 | 0 |
| 120 FPS / 128 units | 6,017 / 256 | 0.250003814697038 | 2 | 0.0009 / 0.2736 / 4.2513 | 0 |
| skip 30 FPS / 1 unit | 29,537 / 28,096 | 0.999999999999773 | 2 | 0.0068 / 0.0173 / 3.1596 | 0 |
| stress 2,000 / 30 FPS | 58,053 / 56,612 | 0.999999999999773 | 10 | 0.0047 / 0.0158 / 14.7591 | 0 |

Across six R2 runs (30 measured arm windows), **one sample >=30 ms** is flagged,
maximum **66.3905 ms**. The retained 66.3905 ms save-free sample is not discarded; the
detached spike condition is **not passed**, irrespective of other low p95s. No native
mod-work/whole-frame gate is established. Calendar policy, economic results and writer 25
remain unchanged.
The unchanged standalone saturated-history bulk arm also flags **310.6081/609.7626/255.2312 ms**
write/read/activation maxima (3/3 >=30 ms each), plus **1077.9129 ms** migration. These are separate
serialization/startup costs. The parked hotspots remain parked.
