# City-audit F5f — active-journey candidate index: performance

## Roadmap perf-log entry (moved 6 Nov 2026)

- City-audit F5f — [Active-journey candidate index for physical selection](f5f.md): selection p95 against the unchanged pre-F5f loop at 1,914/10,000/50,000 persons with 100 active journeys 0.0360/0.2623/1.0858 -> 0.0133/0.0126/0.0123 ms, with 1,000 active 0.2801/0.4230/1.9762 -> 0.1823/0.2289/0.1927 ms; 0 B per call from the selection itself; offline harness on native doubles (tiered compilation off), not in-game mod-work.

5 November 2026, offline. Refs [#92](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/92). Measurement named in the [F5 plan](../plans/f5-population.md) section 4 F5f row and section 6: selection p95/max and allocation at 1,914 / 10,000 / 50,000 persons with 0 / 100 / 1,000 active journeys, before/after against the full scan. Historical comparison point: parked hotspot #1 (physical population) p95 0.64 ms / max 21.7 ms in game (30 October review), which covers the whole `PhysicalPopulation.Update`, not only the candidate scan.

## Scope and limits

- Numbers come from `python tools/physical-selection-regression/run.py` (`SelectionPerf.F5f`): **production** selection text extracted from `Runtime/PhysicalPopulation.cs`, compiled with the real Core `ActiveJourneyIndex`, against the harness's native doubles (positions, samples, reservations). Release build, .NET 10, tiered compilation **off** (so short timing loops measure optimized code for every arm, as in the F5b record), 100 warm-up calls, then 300 timed single calls. This PC is shared with other worktrees' runs; two consecutive runs were taken and the tables give the second with the p95 range of both in brackets. These are component timings, not Unity/Mono timings, and not mod-work p95.
- Three arms on identical fixtures; the harness first asserts that all three return identical selections.
  - **BEFORE** (`BeforeF5f`): the unchanged pre-F5f loop over every citizen. `run.py` rebuilds it from the current selection text by putting back the base `foreach (var c in simulation.State.Citizens)` and removing the fallback-only visitor filter; the rebuilt block was checked byte-identical to `origin/codex/persistent-city`'s selection block. It does no ID classification.
  - **AFTER**: the F5f loop over the index.
  - **FALLBACK**: the F5f full scan used only after a shadow mismatch. It also classifies every ID (`ActiveJourneyIndex.ReceivesBody`) to withhold visitors, so it costs somewhat more than BEFORE; it is not the ordinary path.
- Fixture: open pools (48 pedestrians / 24 cars, so the full-pool early exit never fires), N persons of which A walk within the 100 m radius and the rest are indoors at work. Each call clears the 2 s cooldown dictionary so it attempts up to four candidates again; this clear runs in every arm.
- **Allocation:** 32 B per call at 0 active and 152 B at 100 or 1,000 active, identical in every arm. It is the harness's own result list (and its growth to four entries), which production does not have. The selection and index iteration themselves allocate nothing per call.
- Not measured here: queue planning (`PlanQueues`) now also iterates the index (same kind of saving, not timed separately), and the Runtime half of the shadow check (`StatefulCandidatesIndexed`, one dictionary lookup per citizen, at the same 30 s cadence) has no harness; its cost is expected to be of the same order as the Core shadow comparison below, unverified.
- Correction (review round 1): an earlier version of this record used the FALLBACK arm as BEFORE and ran with tiered compilation on. That inflated the claimed saving (FALLBACK includes ID classification that the base never did) and left the 1,914-person rows dominated by tier-0 code. The numbers below replace it.

## Selection (one call, ms: p50 / p95 / max, p95 range of two runs)

| Persons | Active journeys | BEFORE (pre-F5f loop) | AFTER (index) | FALLBACK (full scan after mismatch) |
| ---: | ---: | --- | --- | --- |
| 1,914 | 0 | 0.0153 / 0.0154 / 0.0252 (0.0154–0.0195) | 0.0000 / 0.0001 / 0.0037 (0.0001–0.0001) | 0.0229 / 0.0236 / 0.0277 (0.0236–0.0433) |
| 1,914 | 100 | 0.0314 / 0.0360 / 0.1140 (0.0360–0.0568) | 0.0126 / 0.0133 / 0.0173 (0.0133–0.0207) | 0.0391 / 0.0408 / 0.0493 (0.0408–0.0773) |
| 1,914 | 1,000 | 0.1894 / 0.2801 / 0.4839 (0.2691–0.2801) | 0.1726 / 0.1823 / 0.2705 (0.1823–0.2644) | 0.1932 / 0.2119 / 0.3393 (0.2119–0.2960) |
| 10,000 | 0 | 0.0926 / 0.1080 / 0.2323 (0.1064–0.1080) | 0.0000 / 0.0001 / 0.0021 (0.0001–0.0001) | 0.1202 / 0.1489 / 0.3072 (0.1295–0.1489) |
| 10,000 | 100 | 0.1176 / 0.2623 / 0.2961 (0.2166–0.2623) | 0.0125 / 0.0126 / 0.0157 (0.0126–0.0126) | 0.1570 / 0.2817 / 0.3134 (0.2561–0.2817) |
| 10,000 | 1,000 | 0.2720 / 0.4230 / 0.5289 (0.3166–0.4230) | 0.1800 / 0.2289 / 0.3170 (0.2289–0.2608) | 0.3103 / 0.3527 / 0.5586 (0.3303–0.3527) |
| 50,000 | 0 | 0.6008 / 1.2042 / 1.8217 (1.2042–1.2644) | 0.0000 / 0.0001 / 0.0030 (0.0001–0.0001) | 0.7134 / 1.2241 / 1.5444 (1.2241–1.2427) |
| 50,000 | 100 | 0.6015 / 1.0858 / 1.3235 (1.0858–1.2002) | 0.0122 / 0.0123 / 0.0178 (0.0123–0.0125) | 0.7973 / 1.0546 / 1.8093 (1.0546–2.7819) |
| 50,000 | 1,000 | 0.8951 / 1.9762 / 2.1892 (1.9762–2.7495) | 0.1727 / 0.1927 / 0.3634 (0.1927–0.2468) | 0.9578 / 1.4633 / 2.0626 (1.4633–2.7864) |

With the index the cost follows active journeys (about 0.012 ms p95 at 100 and 0.18–0.26 ms at 1,000) instead of the population: against the unchanged pre-F5f loop, p95 at 100 active journeys drops 0.0360 → 0.0133 ms (1,914 persons), 0.2623 → 0.0126 ms (10,000) and 1.0858 → 0.0123 ms (50,000). When about half the city is active (1,914 / 1,000) the index saves little, as expected: the remaining cost is the candidate sort and per-candidate position sampling, which every arm shares. No sample ≥ 30 ms in any arm. The 50,000-person rows vary most between runs (shared machine).

The selection runs at most every 0.25 s (unchanged), so these are not per-frame costs.

## Index maintenance and shadow comparison (ms, second run)

| Persons | Membership change (one phase change in and out) | Rebuild (construction / load / route rebuild), p50 / p95 / max | Shadow comparison (`MatchesScan`, first selection and every 30 s), p95 at 0 / 100 / 1,000 active |
| ---: | --- | --- | --- |
| 1,914 | 0.0001 p95, 0 B | 0.0386 / 0.0423 / 0.0616, 0 B warm | 0.0095 / 0.0277 / 0.0328 (max 0.0971) |
| 10,000 | 0.0001 p95, 0 B | 0.1701 / 0.1770 / 0.2825, 0 B warm | 0.0549 / 0.1642 / 0.1678 (max 0.3215) |
| 50,000 | 0.0001 p95, 0 B | 1.0621 / 2.2101 / 3.4914, 0 B warm | 0.5548 / 1.2883 / 1.8317 (max 3.1935) |

Filing runs once per committed citizen event (inside `FileOptionalStop`) and at the two visit starts: one membership test and at most one dictionary insert/remove with a swap-removal. Warm rebuilds reuse list and dictionary capacity; the first build at construction allocates the index once (not measured separately). The shadow comparison allocates nothing.

## Gate reading

Offline only. F5f adds no per-frame scan, reflection or allocation: the selection's population scan becomes an index walk, the index is filed on simulation events, and the full-scan shadow check runs once at the first selection and then every 30 s (≤ 3.2 ms at 50,000 persons in these runs, under the 30 ms single-spike flag). In-game mod-work p95, single-spike counts and whole-frame timing are measured by the next batched rig run (check 4 in [the checks file](../checks/f5f.md#in-game-checks-for-the-next-batched-rig)); whole-frame figures stay separate from mod-work.
