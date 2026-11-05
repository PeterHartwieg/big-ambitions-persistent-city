# City-audit F5f — active-journey candidate index: performance

5 November 2026, offline. Refs [#92](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/92). Measurement named in the [F5 plan](../plans/f5-population.md) section 4 F5f row and section 6: selection p95/max and allocation at 1,914 / 10,000 / 50,000 persons with 0 / 100 / 1,000 active journeys, before/after against the full scan. Historical comparison point: parked hotspot #1 (physical population) p95 0.64 ms / max 21.7 ms in game (30 October review), which covers the whole `PhysicalPopulation.Update`, not only the candidate scan.

## Scope and limits

- Numbers come from `python tools/physical-selection-regression/run.py` (`SelectionPerf.F5f`): the **production** selection block extracted from `Runtime/PhysicalPopulation.cs`, compiled with the real Core `ActiveJourneyIndex`, against the harness's native doubles (positions, samples, reservations). Release build, .NET 10, default tiered compilation with 100 warm-up calls, then 300 timed single calls; this PC, shared with other worktrees. These are component timings, not Unity/Mono timings, and not mod-work p95.
- **BEFORE** is the same production loop over every citizen (the `journeyIndexFallback` path, which is the pre-F5f loop plus a visitor filter that never matches here). **AFTER** iterates the index. Both arms run on identical fixtures; the harness asserts identical selections first.
- Fixture: open pools (48 pedestrians / 24 cars, so the full-pool early exit never fires), N persons of which A walk within the 100 m radius and the rest are indoors at work. Each call clears the 2 s cooldown dictionary so it attempts up to four candidates again; this clear runs in both arms.
- **Allocation:** the 32 / 152 B per call in both arms is the harness's own result list (and its growth to four entries), which production does not have. The selection and index iteration themselves allocate nothing per call.
- Not measured here: queue planning (`PlanQueues`) now also iterates the index (same saving, not timed separately), and the Runtime half of the shadow check (`StatefulCandidatesIndexed`, one dictionary lookup per citizen, at the same 30 s cadence) has no harness; its cost is expected to be of the same order as the Core shadow comparison below, unverified.

## Selection (one call, ms)

| Persons | Active journeys | BEFORE p50 / p95 / max | AFTER p50 / p95 / max |
| ---: | ---: | --- | --- |
| 1,914 | 0 | 0.0685 / 0.0942 / 0.2139 | 0.0001 / 0.0002 / 0.0026 |
| 1,914 | 100 | 0.0987 / 0.1494 / 0.2945 | 0.0396 / 0.0470 / 0.0556 |
| 1,914 | 1,000 | 0.1625 / 0.4779 / 5.4924 | 0.1263 / 0.1386 / 0.2297 |
| 10,000 | 0 | 0.0562 / 0.0603 / 0.1741 | 0.0000 / 0.0001 / 0.0004 |
| 10,000 | 100 | 0.0819 / 0.1357 / 0.1634 | 0.0099 / 0.0124 / 0.0165 |
| 10,000 | 1,000 | 0.1990 / 0.3308 / 0.3571 | 0.1279 / 0.1513 / 0.3226 |
| 50,000 | 0 | 0.4336 / 0.6965 / 0.8972 | 0.0000 / 0.0001 / 0.0003 |
| 50,000 | 100 | 0.5051 / 0.9725 / 1.5149 | 0.0098 / 0.0121 / 0.0211 |
| 50,000 | 1,000 | 0.7064 / 1.1065 / 1.6581 | 0.1274 / 0.1965 / 0.2906 |

With the index the cost follows active journeys (about 0.01 ms at 100 and 0.13 ms at 1,000) and no longer the population: at 50,000 persons and 100 active journeys p95 drops 0.9725 → 0.0121 ms. The remaining cost at 1,000 active journeys is the candidate sort and per-candidate position sampling, which both arms share. The 5.49 ms BEFORE maximum at 1,914 / 1,000 was a single outlier in that run (earlier run of the same build: 5.33 ms; the other 299 calls were below 0.48 ms p95); it is not attributed to F5f. No sample ≥ 30 ms in either arm.

The selection runs at most every 0.25 s (unchanged), so these are not per-frame costs.

## Index maintenance and shadow comparison (ms)

| Persons | Membership change (one phase change in and out) | Rebuild (construction / load / route rebuild) | Shadow comparison (`MatchesScan`, first selection and every 30 s) at 0 / 100 / 1,000 active |
| ---: | --- | --- | --- |
| 1,914 | 0.0002 p95, 0 B | 0.0871 p50 / 0.1080 p95 / 0.2256 max, 0 B warm | p95 0.0376 / 0.0699 / 0.1245 |
| 10,000 | 0.0001 p95, 0 B | 0.0898 / 0.1359 / 0.3123, 0 B warm | p95 0.0925 / 0.1526 / 0.0886 |
| 50,000 | 0.0001 p95, 0 B | 0.5702 / 1.0542 / 1.4016, 0 B warm | p95 0.3991 / 0.7945 / 0.9641 (max 1.5950) |

Filing runs once per committed citizen event (inside `FileOptionalStop`) and at the two visit starts: one membership test and at most one dictionary insert/remove with a swap-removal. Warm rebuilds reuse list and dictionary capacity; the first build at construction allocates the index once (not measured separately). The shadow comparison allocates nothing.

## Gate reading

Offline only. F5f adds no per-frame scan, reflection or allocation: the selection's population scan becomes an index walk, the index is filed on simulation events, and the full-scan shadow check runs once at the first selection and then every 30 s (about 1 ms at 50,000 persons, well under the 30 ms single-spike flag). In-game mod-work p95, single-spike counts and whole-frame timing are measured by the next batched rig run (check 4 in [the checks file](../checks/f5f.md#in-game-checks-for-the-next-batched-rig)); whole-frame figures stay separate from mod-work.
