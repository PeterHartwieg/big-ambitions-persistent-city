# E4 offline performance

1 November 2026. Baseline `51ce709`; native field doubles, .NET 10 Release, seed 957. No native performance claim. Raw matrix/harness logs: ignored `build/e4-menu/validation`.

**Measurement provenance after base sync:** all tables and before/after numbers below were measured on the original E4 stack based on `51ce709`, before E1. They are retained historical evidence, not measurements of the merged E1/E4 tree. The sync adds current tool results and limits in [E4 checks](../checks/e4-wider-menu.md#base-sync-with-e1-and-f1). No new performance win is claimed by this merge.

Taken low-risk win: `FoodSupplyReader.ReadMenu` reuses an already captured ordered string array, removing the redundant Distinct set/enumerator/result-array pipeline. Duplicate input now refuses explicitly instead of being silently collapsed. Nonarray input stops at nine entries before allocation; the eight-offer contract is checked before native setup. Successful snapshot fields remain byte-identical at width four. Native setup/counter/producer call order and the checkout path are unchanged.

The harness measures the pinned baseline reader and current reader against the same fixture, 20 warmups and 1,001 samples; four/eight offers share one counter and bag pool. Choice measurements evaluate 32/64 offers from eight shops, with at most sixteen route slots. History measurements use 14 × 1,024 rows with four/eight product cells, both counting paths, churn, exact round trips, update and serialization. Fixture creation is outside measurements. Full serialization is a bulk offline operation, never ordinary-play mod-work p95. All ≥30 ms samples are reported, and whole-frame measurements are unavailable offline.

Final measured numbers follow. The physical population, closure refresh, optional-stop refresh and general save-serialization hotspots remain parked.

| Supply refresh | p50 / p95 / max ms | Allocated B/call |
| --- | ---: | ---: |
| 4 offers, baseline 51ce709 | 0.0010 / 0.0018 / 0.1375 | 1,960 |
| 4 offers, E4 | 0.0009 / 0.0015 / 0.0101 | 1,552 |
| 8 offers, E4 test-only envelope | 0.0018 / 0.0024 / 0.0099 | 2,368 |

**Taken win: 1,960 -> 1,552 B/refresh, saving 408 B (20.8%)**. p95 0.0018 -> 0.0015 ms in this sequential sample; no causal native CPU improvement is inferred. Zero measured reader samples >=30 ms. The preallocated menu array is reused; each refresh still performs one native setup and shares the same bag/counter pool across the menu. Duplicate/oversized input refuses before setup.

Default live-product selection has zero allocations before/after, p95 0.0124 / 0.0081 ms, and identical weights/choices across 1,000 seeds. Synthetic 4/8 offers x eight shops cost p95 0.0134 / 0.0257 ms, zero allocations, and sixteen quote slots in both arms. These observations establish no added allocation/native/route work, not literally zero extra CPU instructions.

History stress: 14,336 rows / 57,344 or 114,688 cells. Initial save sizes **8,648,985 / 10,135,833 ASCII characters and UTF-8 bytes**; timed saves after updates **8,864,025 / 10,350,873**. Update p95 **0.7869 / 2.5212 ms**, zero allocations; bulk serialization p95 **229.4066 / 245.1546 ms**, **80,172,755 / 92,156,970 B/call**. **All 42 measured serialization samples >=30 ms**, max **257.2624 / 248.9193 ms**, explicitly flagged as offline bulk saves, not ordinary-play mod-work. Eight-product bulk update max **6.6181 ms**; zero >=30 ms update samples.

[Full table, counting/size/churn proof and limits](../checks/e4-wider-menu.md#final-measured-results---1-november-2026-offline-only). [Machine-readable samples and command/log hashes](../checks/e4-wider-menu-results.json). Timing samples are warm, sequential field doubles on a shared development PC; fixture construction and warmups are excluded. No native total-gate or whole-frame claim. No additional measured performance win is claimed.
