# Car-search time ceiling — 21 October 2026

Implements finding 3, the next outstanding item in the [frame-cost review](2026-10-20-frame-cost-review.md). Periodic road-snapshot rebuilding and repeated all-gate scans were already addressed. Native profiling has not isolated the share of current frame time spent on losing car searches.

## Change

A feasible walking, subway or earlier car candidate gives `MultimodalRoutes` a known arrival-time estimate. The planner subtracts the required access walk, preparation, parking and egress, then passes the remaining time to `RoadRoutes`. That adapter subtracts the two curb connectors. `TransportRoadGraph` stops when the least estimated remaining path exceeds this ceiling. Its existing free-flow geometric heuristic remains a lower bound because traffic estimates are nonnegative and effective road durations can only slow traversal.

The query still uses the same directed graph, gate checks, native contracts, queue forecasts, expansion limit and final selected-path validation. It does not cache winners or change existing journeys, saves, native movement or the two-search candidate limit. No feasible alternative means an infinite ceiling and the existing full search. A new query gets fresh closure/delay information. The existing six/eight-argument `Plan` and nine-argument `PlanWithTraversal` method-group contracts remain available.

Search pruning has a conservative allowance of `max(1, incumbentMinutes) × 1e-9` at each deduction and at the graph boundary, covering floating-point accumulation and regrouping over the maximum 512 returned legs. This may retain a marginally slower candidate for the final comparison. It does not relax the strict arrival-time comparison: exact ties keep the first candidate and a one-ULP improvement can still win. Bounded graph queries require immutable positions/topology, as supplied by the Runtime snapshot. The default unbounded path preserves prior callback behavior; bounded queries intentionally stop callbacks after their cutoff. Consequently, an unrelated native change that an exhaustive losing search might have discovered can instead be noticed by a later search or the existing staged snapshot sweep. Selected paths still receive live validation. Invalid graph ceilings throw; the internal Runtime deduction helper conservatively disables pruning for invalid input.

## Offline validation

- **877 Core/Compatibility/Persistence tests pass**, including eight new test cases. Differential tests run **6,300 bounded queries** over 30 directed graphs with disabled nodes, excluded gate segments, slowdown-only traversal, queue delays and four clock rates. Competitive paths, exact estimates and leg durations match unbounded searches. Other cases check early exit on an unreachable region, workspace reuse, invalid ceilings, exact ties and one-ULP boundaries.
- The same harness compiles the actual `MultimodalRoutes`, `RoadRoutes`, traffic builder and Core against both the frozen previous source and current source. All **38 full-route results match exactly**, with **69 assertions per run**. Cases include a better second curb, unequal nonzero source/destination connectors at exact and one-ULP ties, blocked/reopened routes, congested walking/subway winners and expensive car trips with no alternative. A subway-winning fixture asserts that a car A* actually starts before the ceiling stops it. All three exact timed fixtures are compared across source versions before timing, and each timed result is checked against that preflight. Native objects and complete walking routes are synthetic fixtures; this is not a NavMesh or game test.
- Existing actual-Runtime gate checks (**213,884**), road-handoff/sweep/gate checks, and road-speed/persistence checks (**602**) pass.
- C# 9 builds with warnings as errors pass against local Mac game and retained Windows reference assemblies. Both produce DLL SHA-256 `164af7813e3b3ed0c8ca18c3605d7f8a1d38535407f815f401df1a7e6db19c1e`. Reference paths and hashes are retained with each build log.

## Synthetic full-query measurements

Tiered compilation is disabled. Three alternating baseline/current process pairs each measure five warmed batches of **200 full multimodal requests**; the table shows the median of those three batch medians. No competing build or benchmark ran. Every scenario's complete result is compared across source versions before timing. The retained baseline is compiled source, not a reimplementation of the original search. Reproduce with `tools/road-ceiling-regression/compare.py`; see the [harness instructions](../../tools/road-ceiling-regression/README.md).

| Scenario | Before | With ceiling | Speedup | Instrumented native-field reads |
| --- | ---: | ---: | ---: | ---: |
| Unreachable car destination; walking available | 71.403 ms | 1.654 ms | 43.18× | 1,472,400 → 50,400 |
| Heavy car congestion; walking wins | 16.237 ms | 1.039 ms | 15.63× | 270,000 → 33,600 |
| Driving wins | 14.989 ms | 15.545 ms | 0.96× | 270,000 → 270,000 |

The read counts cover instrumented synthetic fields, not every native API call or A* expansion. Per-batch .NET 10 thread allocations fall from 36,666,624 to 1,240,128 bytes in the unreachable case and from 18,137,376 to 988,928 bytes in the congested case. The winning-car case allocates the same 18,137,376 bytes. Those totals include routes and scratch data. Timings and allocations are synthetic .NET measurements, not Unity/Mono or native frame/FPS evidence. The final winning-car row takes about 3.7% more time. Earlier validation runs on the same production code measured approximately 30–32× for the unreachable fixture, 14–14.5× for congestion and 0.99–1.01× for winning cars; wall-time variance makes operation counts stronger evidence than any single speedup. This optimization helps losing searches; it does not promise faster winning searches or resolve every previously observed 60 ms route event.

## Review and delivery

Astra, GPT-6.1 Sol, Opus 5.5 and Grok 4.7 approve this bounded production change. Astra and Sol also independently approve the later validation supplement; all 49 production C# files remain identical to the four-model review and native-reference build inputs. See the [review record](2026-10-21-road-search-ceiling-panel.md) for findings, resolutions and the exact scope of each review.

Production and test source, the pre-change baseline, manifests, comparison logs and Mac/Windows builds are retained under ignored `build/road-search-ceiling-20261021/`. The disabled reviewed package is `PersistentCity-road-search-ceiling-reviewed-development.zip` in that directory. The existing `build/PersistentCity`, reviewed gate-index package and PC installations are unchanged. Native performance and gameplay validation remain pending.
