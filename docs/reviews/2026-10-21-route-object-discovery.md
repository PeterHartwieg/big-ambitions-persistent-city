# Route-object discovery performance — 21 October 2026

Reviewed playtest candidate **`782e13b18df3026496150e478d537352cb3be9d28d73fb25c3ee5d8af89cdb23`**, version 0.6.2, city schema 7 / traffic schema 2 unchanged. It includes the preceding signal-invalidation fix 9c15 and replaces recurring global gate/station searches with retained discovery spread across frames. **Not installed on the PC.** Only the isolated Mac rig received temporary builds; its original installation, settings and all 2,409 pre-existing profile files were restored and verified afterward.

## What changed and why

The same full-modpack Mac baseline still spent hundreds of milliseconds searching the whole Unity object universe every 30 seconds, even though this city had only 4 public-gate components and 20 subway-station components. Fixing signal-driven road rebuilds did not remove those periodic searches.

`RouteObjectDiscovery` is owned by the save lifetime. It enumerates loaded scene hierarchies, including inactive objects and Unity's persistent scene, in scheduled slices. It retains relevant components, periodically reconciles additions and replacements, and prunes destroyed/ineligible objects. Gates and stations consume the retained inventory; their live geometry/eligibility checks remain. The runtime uses this source on every planner, including clock-rate replacement. One cold road-scene lookup and startup outdoor discovery remain synchronous.

A slice has a **soft 1 ms** scheduling budget and a 2,048-operation limit. Native subtree queries, root enumeration and copying one parent's child references cannot be preempted and can exceed it. The first two hierarchy levels are split; native branches exceeding 4 ms receive optional further splitting, capped at 256 hints. Child lists are reused with bounded retained count/capacity. A completed sweep always has 30 seconds of idle time before the next periodic pass; explicit scene/topology invalidation bypasses the delay. This avoids both recurring monolithic searches and continuous work after an unusually slow sweep. Hints may still reflect earlier stalls; no claim of optimal adaptation or hours-long stability is made.

Inventory changes publish as one completed generation. A removed gate can be half of a replacement, so readiness stays closed until fresh discovery finishes instead of temporarily opening access. Additive/unloaded scenes, new CityManager/lost road scene and explicit invalidation also fence readiness. Immediate sibling references prevent a destroyed/reordered sibling from causing an unseen gate to be skipped. Native fixtures established the correct Unity hide-flag filtering: `DontSaveInEditor` excludes a component/object; individual `DontSaveInBuild` and `DontUnloadUnusedAsset` remain discoverable. The special persistent-scene anchor is owned and destroyed by this mod.

Diagnostics report last sweep work/duration/branch count, split-hint count and worst slice alongside existing road-invalidation reasons and preparation/closure costs. `Sweeps` counts completed traversal attempts, not necessarily published revisions. This does not alter economy, identities, assignments, traffic rules, save schema or physical body limits. The four unrelated concurrent Compatibility edits in the moving checkout are excluded and preserved.

## Matched native comparison

Isolated background Metal Mac rig, 960 × 540 low, 30 FPS cap, two worker threads, low process priority. Same disposable checkpoint: 957 citizens, 1,011 places, 9,093 road sections; RLO 0.45 / full local modpack; 48 pedestrian / 24 car limits, 120 m radius. Every mod/helper DLL hash and Persistent City setting matched except the candidate DLL. Both builds used identical diagnostic DLLs; only the candidate has a discovery Step hook. After loading, each run settled paused 45 seconds, warmed running 45 seconds, then captured 180 seconds at native 1×. No save command was issued.

The closest matched pair is baseline-c 9c15 versus candidate-d 782e13b1. Timings below are inclusive wall time from Harmony instrumentation; nested calls overlap and must not be summed with Update. They are not CPU samples. The report uses named method metrics; the helper's per-frame `update` subtotal also adds nested hooks and must not be used as exclusive Update attribution.

| Component | Baseline maximum ms | Candidate maximum ms | Baseline total ms / 180 s | Candidate total ms / 180 s |
|---|---:|---:|---:|---:|
| Preparation | 562.471 | 1.702 | 2675.3 | 126.0 |
| Gate refresh | 559.735 | 0.273 | 2376.5 | 1.5 |
| Closure refresh | 12.073 | 9.262 | 3071.7 | 3728.0 |
| Whole Persistent City Update | 594.524 | 120.454 | 26582.5 | 25805.5 |
| Incremental discovery (new, included in Update) | — | 26.091 | — | 1611.4 |

The discovery lifetime count rose from 3 to 8 completed traversal attempts (five during capture); its last pass visited 41,320 branches in 271.5 ms work spread over 7.93 s, with 33 split hints. Initial discovery took 301.5 ms spread over 8.33 s; cold road-scene lookup still took 137.2 ms. These startup costs are outside the warmed comparison.

| Whole-game frame interval | Baseline ms | Candidate ms |
|---|---:|---:|
| Median | 36.07 | 34.73 |
| 95th percentile | 101.31 | 78.27 |
| 99th percentile | 234.41 | 181.11 |
| Maximum | 652.42 | 634.43 |

Frames over 100/250/500 ms were 195/34/19 before and 132/24/8 after. Frame counts were 3698 and 4153; simulated progress was 174.6 and 177.4 game minutes. Both retained one road snapshot, had no routing-wait frames during capture and ended with zero simulation backlog. Physical outcomes were not bit-identical across independently advancing runs: baseline traffic-overshoot diagnostics rose from 0 to 1, versus 0 to 0 in the candidate. This is a reported counter, not a causal safety comparison. These capped/background runs support removal of the identified periodic stalls, **not an interactive FPS guarantee**; significant frame spikes from remaining game/mod work persist.

An earlier baseline-a independently saw 927.5 ms preparation and 770.2 ms gate-refresh maxima. An interim r3 removed those stalls but over-split the hierarchy: 34k→110k branches and 274→685 ms work per sweep. That evidence prompted the final 4 ms threshold and completion-relative idle interval. The r3 DLL is not the delivered candidate.

## Longer bounded run and native correctness

After the matched capture, reloaded the same disposable checkpoint to exercise detach/reinitialization. Created 15 temporary inactive fixtures covering seven hide-flag variants on both components and GameObjects plus a persistent-scene object. The final candidate matched Unity's native global result sets before insertion, with all eligible fixtures and after removal: 4 gates / 20 stations initially, plus 9 eligible gates/9 eligible stations. Readiness recovered after removal; no runtime/physical failure occurred. Fixtures existed only in memory and were not saved.

Then ran two consecutive five-minute native 1× captures. They had zero routing-wait frames and no runtime/physical failure. Worst discovery slice across these captures was 31.176 ms; total discovery work was 6361.9 ms over 600.0 s. Final sampled sweep count 17, branch count 74,635, split hints 108, last work 496.1 ms over 16.17 s. Largest sampled backlog was 0.300 game minutes; the final paused state was fully caught up (0.000). The final physical diagnostic recorded seven traffic overshoots. No matched ten-minute baseline was run, so those events are not attributed to this change and physical-driving safety is not signed off by this performance run. Sampled sweep work ranged from 329.5 to 648.3 ms, and branch captures grew from 41,161 to 74,635. The retained hints adapt during the run; total discovery work is not flat. The cap and idle interval bound that adaptation, but longer-session timing is still unverified. Exact trend samples and complete frame captures remain under the evidence root. This is a ten-minute bounded soak, not proof of hours-long stability or tens of thousands of residents.

## Regression and independent review

- 915 unit tests, 252 actual Runtime invalidation assertions and 602 speed/persistence assertions pass on frozen source-r2. Their compiled production/test inputs are byte-identical in final source-r5; the hash-based reuse receipt lists the only intervening changes.
- Final discovery harness independently passes with primary/Astra/Sol (497–498 assertions; the count varies with the number of slices). It covers inactive/multiple/subclass components, additions/removal/replacement and ID reuse, native eligibility flags, persistent/additive/unloaded scenes, wide/deep trees, sibling mutation, bounded split hints, atomic generations, save disposal, a long-pass idle gap, invalidation bypass and finite scene-change recovery. The broader invalidation harness covers integration with city/road-scene changes and retained-array use.
- The sibling regression fails against r2 and the long-pass scheduler regression fails against r4, then both pass on final r5. A reentrant final-root callback test checks the defensive publication fence; ordinary native hierarchy reads dispatching such callbacks was not reproduced.
- Independent rounds with Astra, GPT 6.1 Sol, Claude Opus 5.5 and Grok 4.7 reviewed source, and all approve final r5. Review findings drove atomic replacement publication, city-reset fencing, native hide-flag checks, stable sibling snapshots, a completion-relative idle gap and the final publication fence. Astra/Sol verified all 170 frozen source hashes. Opus/Grok approvals are code review, not independent native execution. Grok's first attempt ended without a verdict; its later r2 review and compact complete r5 review are retained. Actual CLI usage receipts identify `claude-opus-5-5` and `grok-4.7-build` (requested Grok 4.7).
- Mac-reference and retained Windows-reference builds compile with warnings as errors and are byte-identical. No game/RLO/helper DLLs are distributed. The package stays disabled by default.

Run the focused discovery harness with `dotnet run --project tools/route-discovery-regression/RouteDiscoveryRegression.csproj --configuration Release`. Its `SourceRoot` property can point to an earlier frozen source for a negative control. This stub harness does not itself run Unity; the fixtures above provide native coverage.

## Remaining limits and next performance targets

New components added without a scene event are found by periodic reconciliation; changes behind an already-visited cursor can wait until a later sweep. Detection latency includes the 30 second idle gap plus traversal time. Endless genuine invalidations deliberately keep population preflight closed rather than publish an incomplete gate set. The snapshot is not an instantaneous transaction over arbitrary concurrent hierarchy edits.

Single native calls remain non-preemptible. Native route/path queries, whole-game rendering/other mods, cold startup and dense-population catch-up remain performance targets; this change does not validate a 10,000-person population or match another game's simulation architecture. Full feature-by-feature modpack coverage, representative populated-economy saves, an interactive recording session, Windows timings, allocation profiling and an hours-long soak remain separate checks. No claim is made about Transport Fever's internals.

The Mac rig was stopped, the original DLL `e0756b4a74764c4b04e65cf0edd9a2043cff9ccb4e34dea2942a9063c0582124` and manifest restored, preferences restored, all 2,409 pre-existing profile files hash-verified and both diagnostic helpers removed. The reservation was released. Windows was not contacted and neither PC profile was changed. The playtest package requires a new installation instruction before PC deployment.

Evidence root (ignored): `build/scene-discovery-20261021/`, including source-r5/manifests, test/review logs, baseline-a/c and candidate-d captures, native-fixtures-r5, soak captures/trend samples, receipts and cleanup verification. Bulk prior PC diagnostics remain on the PC.
