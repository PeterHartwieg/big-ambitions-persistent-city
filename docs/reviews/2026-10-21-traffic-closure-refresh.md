# Traffic-closure refresh — 21 October 2026

After the three leading [frame-cost findings](2026-10-20-frame-cost-review.md), the next selected opportunity is `RefreshTrafficClosures`, which runs every prepared frame. This is a source-based priority: native profiling has not established its current share of frame cost. The remaining walking-key allocations, four per-search dictionaries, physical selection and RLO reflection work are unchanged.

## Change

The simulator reuses a list for the same detached, ascending, unique set of occupied/demanded sections, their head vehicles' immediate receivers, and previously closed sections. Sorting and compacting the list replaces allocation of a tree node per section and a second list copy each frame. The public `RelevantLinkIds` property still returns a detached snapshot.

An optional batch interface lets the native planner validate a shared waypoint and junction once within one synchronous refresh. Each pass gets a fresh generation, including a wraparound reset. The simulator ends the scope in a `finally` block; its scratch list and nesting guard are cleared even on failure. Legacy planners retain individual calls. No native result survives into the next pass, and single-section checks at traffic-entry boundaries still read live state. The scope relies on the existing main-thread, synchronous, read-only native validation contract; it does not span frames or advance traffic events.

A successful full-section gate check is retained while the immutable access snapshot and captured road geometry remain unchanged. `SetAccess` invalidates it on a different snapshot reference. Node validation still occurs in the same order, and blocked gate checks keep their prior order rather than caching a whole closed-section answer. Structural failures continue to mark the road snapshot stale and close subsequent queries. Road search, selected-route validation, gate refresh cadence, traffic event order, save schemas and physical movement are unchanged.

The native planner retains two arrays (one integer stamp and one boolean per waypoint) and one boolean per section, allocated lazily. The simulator retains its peak relevant-list capacity. This trades bounded retained scratch memory for less repeated allocation. The 60-second runtime summary now separately reports total and maximum closure-refresh time and its call count; it still does not measure complete frame cost. The timer includes `EnsureTrafficNetwork`, so an occasional network-binding frame can dominate its maximum. The steady-state measurements below exclude that binding work.

## Offline evidence

- **883 tests pass**: the previous 877 plus six closure-refresh cases, including four exception stages. They check sorted detached IDs, dormant receivers, legacy/batched kernel equality and cleanup after begin/read/end/nested failures.
- The same harness compiled against the frozen previous source and current source produces **24 identical full closure-state snapshots**, with **35 assertions per revision**. It covers close/reopen with the same native frame counter, ordinary boundary freshness, generation wrap, replaced/removed gates, native-read failure/retry, twelve native structural mutations and dormant closed-link reopening. The final comparator also passes under `python -O`; a mismatching fingerprint still raises an explicit exception in that mode.
- Three independent scratch mutations fail as intended: omitting gate-cache invalidation, omitting the stamp clear at generation reset, and failing to end the native batch. Production source was never mutated. These establish the sensitivity of those checks, not exhaustive protection against every possible cache bug.
- The existing **38 exact full-route comparisons** with 69 assertions per revision remain identical. The road-handoff/sweep/gate harness, **213,884 gate checks**, and **602 speed/persistence assertions** pass.
- C# 9 builds with warnings as errors against local Mac and retained Windows game references produce identical DLL SHA-256 `6be406eba6e2178d4322d5635b6c0092fe11ac089b6b38413ffcb0bc0c16c63f`. Explicit reference hashes and source inputs are retained.

## Synthetic full-refresh measurements

The [harness instructions](../../tools/traffic-closure-regression/README.md) describe the five synthetic scenarios. All five exact fixture fingerprints match across versions before timing, and each timed row is checked against that preflight. Three alternating baseline/candidate process pairs each measure five warmed batches of **300 complete closure refreshes** with tiered compilation disabled. No competing build or benchmark ran. The table reports the median of the three process medians; allocations are medians of the retained batch samples.

| Scenario | Previous source | Current source | Speedup | Managed allocation per batch |
| --- | ---: | ---: | ---: | ---: |
| 957 demands, junction-heavy | 140.721 ms | 79.247 ms | 1.78× | 12,825,600 → 76,800 bytes |
| 128 demands, long sections | 33.385 ms | 27.777 ms | 1.20× | 1,850,400 → 67,200 bytes |
| Long sections with 512 nearby gates | 56.633 ms | 27.548 ms | 2.06× | 1,850,400 → 67,200 bytes |
| One demand | 0.229 ms | 0.166 ms | 1.38× | 129,600 → 38,400 bytes |
| No demand | 0.020 ms | 0.015 ms | 1.37× | 40,800 → 33,600 bytes |

The three process medians ranged from 139.651–142.229 ms before and 77.545–80.295 ms after for junction-heavy traffic; 33.090–34.049 and 27.356–31.856 ms for long sections; and 55.973–57.299 and 27.254–27.657 ms for gate-heavy sections. Sparse medians ranged from 0.223–0.241 and 0.164–0.166 ms; empty medians from 0.0189–0.0203 and 0.0145–0.0152 ms. Three pairs remain a small sample.

In the junction-heavy fixture, instrumented native-field reads fall from **3,753,900 to 1,744,200** per batch (54% fewer). Long-section/gate-heavy reads fall from 2,073,600 to 1,845,000 (11% fewer), and sparse reads from 7,800 to 5,700. Instrumentation covers selected synthetic field getters and junction reads, not every native API call. Managed allocations fall about 99.4% in the junction-heavy fixture and 96.4% in long/gate-heavy sections; refresh is not allocation-free. Incidental runtime allocations varied slightly in some batches: the raw ranges are retained with the measurements.

These are warmed .NET 10 synthetic measurements, not Unity/Mono allocation or game-frame results. Initial scratch allocation and snapshot construction are outside the timings. Timed closures stay unchanged; changing closures and replaced snapshots have correctness checks but no timing claim. The timed source had 34 regression assertions; a later frame-counter assertion and comparator hardening do not change production code, fixtures or timed loops. Absolute sparse/empty times are very small and their speedup ratios should not be treated as a meaningful frame improvement. The populated examples save about 0.02–0.20 ms per synthetic refresh; they do not establish that this change fixes the earlier 60 ms route events. Newly added native diagnostics can measure the real closure-refresh contribution during a later playtest.

## Review and delivery status

Astra, GPT-6.1 Sol, Opus 5.5 and Grok 4.7 approve the production change. Astra and Sol also approve the later validation supplement. All 49 production C# files in the reviewed package match the initial review and native-reference build inputs. See the [panel record](2026-10-21-traffic-closure-panel.md) for findings, dispositions and evidence each reviewer checked.

Evidence is retained under ignored `build/traffic-closure-refresh-20261021/`. The reviewed package is `PersistentCity-traffic-closure-reviewed-development.zip` in that directory, disabled by default.

Concurrent RLO 0.45 compatibility edits appeared after the review freeze and were preserved in the working tree. This package excludes them and retains the reviewed RLO 0.40.4/0.41.1 contract. The traffic-refresh improvement is present in both the package and the working tree.

No game was launched, no save was changed and this candidate has not been installed. Native frame-time/FPS and gameplay validation remain pending; prior reviewed packages and PC installations are unchanged.
