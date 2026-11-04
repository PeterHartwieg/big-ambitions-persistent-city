# City audit F6a performance: intervention harness tool cost, 4 November 2026, offline

Refs #93. Base `dc59049` (writer 25). [Checks, baseline and the numbered rig list](../checks/f6a-intervention-harness.md).

## Scope of this measurement

F6a adds only `tools/economy-interventions` and tests. It changes no Runtime or Core code and adds no frame participant, so it adds **no mod work** and has no mod-work p95 or ≥30 ms spike to report. The plan's section 4 row asks for harness runtime and allocation per simulated week at 1,914/4,000 consumers. That is tool cost only, with no gate. The section 6 table has no F6a row; its rows belong to F6b–F6g. Whole-frame cost is not involved. The four parked hotspots (physical population, closure refresh, optional-stop refresh, save serialization) are untouched.

## Method

`dotnet run --project tools/economy-interventions/EconomyInterventions.csproj -c Release -- build/f6a-baseline` on Windows 11, .NET SDK 10.0.402, Release, single thread, sequential. The perf stage runs three control seeds for one simulated week each (7 days plus the 03:00 outcome sample, 10,260 one-minute epochs; the runner asserts this count). Wall time is a `Stopwatch` around the whole run, including fixture construction and the daily ledger checks. Allocation is `GC.GetTotalAllocatedBytes(precise: true)` for the process across the run. Retained output: [perf.json](assets/f6a/perf.json). Host noise and the JIT warm state from the preceding baseline runs limit comparisons. These numbers do not predict Mono or in-game cost.

| Residents / consumers | Seconds per simulated week, median / max of 3 | Allocated bytes per simulated week, median / max |
| ---: | ---: | ---: |
| 957 / 1,914 | 1.3236 / 1.4853 | 1,795,304,264 / 1,797,003,536 |
| 2,000 / 4,000 | 3.2717 / 3.2835 | 3,741,885,424 / 3,751,238,816 |

A full six-week arm at 957 residents takes a median **9.50 s** and allocates **10,967,495,912 B** (control arm, 5 seeds). The complete baseline (85 six-week runs, the full-size reload check, the curve and the perf stage) took about 17 minutes. Allocation is dominated by the existing Core decision path, the double's per-read `FoodBusiness`/observation objects and the harness's decision-trace subscriber (Core builds a `FoodDecisionTrace` only while it is observed). It is detached tool cost, not a mod-work claim.

Superseded first measurement (review round 1, Sol-r1-3): the original runner simulated one extra unmeasured day, so its 2.01 s / 2,002,038,248 B (1,914 consumers) and 4.91 s / 4,169,955,040 B (4,000) covered 11,700 epochs, not one week. The rows above replace them.

## Perf wins

None taken. F6a touches no product code; the harness is an offline tool with no frame, UI or save path. The roadmap index line records this with no before/after numbers.
