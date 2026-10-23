# Background routing performance — 19 October 2026

## Scope and build

This pass reduces road-planning work in the physical prototype. Schema 5, 957 residents in the reference fixture, route-scoring and departure-scheduling rules, the sixteen-event/two-millisecond soft advancement budget and native movement speeds are unchanged. Economy integration remains deferred. Native fast-forward still releases physical citizen bodies and advances their journeys in the background.

Baseline: 0.4.0 DLL SHA-256 `96ad442703672a682b0305df0e408a0598acdd40b1c5edfe367b93a4c1b560b1`.

Validated private candidate: 0.4.1 DLL SHA-256 `0da1ee72c171fadc2f7ed8c3fd942577c9417f50ba8ef5e82eab55c9f41f93d6`. Offline tests, native compilation, same-request native planner/fingerprint comparisons, and normal-speed movement/persistence checks passed. The paired native performance run measured lower processing cost, but its full-state comparison failed. Full independent city replay equivalence and improved peak backlog are not claimed.

## Changes

- Road A* reuses generation-stamped arrays and an indexed binary heap instead of allocating dictionaries and a tree frontier for every search. Priority order remains exactly `(estimate, cost, node)`. Full-key removal preserves the original stale-priority behavior if a Core caller mutates geometry through a callback. Nested or concurrent calls use independent scratch storage, and exceptions release the retained workspace.
- The native adapter validates each queried waypoint once within a synchronous search. New searches read native flags again; final native path validation, curb selection and physical waypoint resolution remain live. No route winner or closure decision survives into another search.
- Native contract comparisons use indexed lists instead of enumerator allocations. Topology hashing reuses its byte buffer and already-captured arrays while preserving the exact existing encoding, including null versus empty contracts.

The scratch arrays retain memory proportional to snapshot node count and peak search frontier. Public gate checks, periodic topology/access refreshes, walking-cache limits and transport-mode evaluation order remain unchanged.

## Independent review and validation

GPT 6.1 Sol implemented the bounded Core search change and separately reviewed the native adapter delta without finding an actionable correctness issue. All **379 offline tests pass**, including 23 new test cases with 2,520 old/new differential queries. Coverage includes exact route/callback order, priority ties, expansion and path-length limits, closures reopening, permission/topology/geometry changes, returned-output ownership, callback reentrancy, exceptions and generation rollover. Native Mac compilation passed with warnings treated as errors.

The user-requested **Claude Opus 5.5** review ran with the exact `claude-opus-5-5` model and read-only tools. It independently identified midnight commute planning and repeated road-search work as the primary opportunities. The measured profile supports that prioritization. Its suggested scratch-storage and per-search native-validation changes were adopted; its assumption that keyed frontier removal always succeeds was rejected for the public Core callback contract and is covered by mutation tests.

Opus also proposed edge/gate memoization, cheaper route validation, reduced snapshot rebuilding and a larger walking cache. Those remain opportunities, not demonstrated improvements. Cross-query route reuse, deferred commute planning and pruning based on stale reachability were not adopted: they can change closure response, route choice, departure timing or saved car state. The native comparison below is needed in addition to the pure-Core tests; neither alone proves every possible live-game behavior.

A bounded Opus 5.5 follow-up reviewed the implemented delta against the retained original source and found no correctness or safety regression. It confirmed that the test oracle matches the old search, and independently checked heap ordering/removal, generation isolation, exceptional/nested calls, native memoization and fingerprint serialization. It noted a memory tradeoff: nested/concurrent fallback workspaces allocate for the entire reported node count, whereas the old dictionaries grew only with visited nodes. The native adapter does not issue such overlapping queries. Its source review does not substitute for native validation.

## Matched native method

A separate GPT 6.1 Sol agent exclusively owns the isolated Mac rig. Both builds use the same disposable `FT Adversarial Sol Occupied` checkpoint, Metal rendering, native 6×, bridge clock multiplier 1, a 30-FPS cap, two job workers and identical diagnostic helper binaries. Root builds and tests were held until the baseline measurement finished; no competing build or benchmark runs during the candidate measurement.

The diagnostic helper records inclusive method timings and counts, including gate-check counts without per-call timing overhead. Inclusive values overlap and must not be summed. These are instrumented main-thread method wall times, not CPU samples or an uninstrumented FPS benchmark. Totals and counts exclude startup; the retained maximum-call values include startup if it contained the largest call.

The baseline starts at simulation minute 2896.837209701538 and ends at exactly 5777.0, covering two midnight commute bursts. The candidate loads the same stored checkpoint, but native scene startup reaches minute 2898.471159 before the pause command; the initial live states must therefore be compared as well as the final states. A test-only prefix caps the simulation's advancement target at 5777.0 and pauses the native clock after it reaches that endpoint. It never overwrites native clock time. The small difference between native clock and capped simulation at the final snapshot is therefore intentional. Full logical states are retained for comparison; a native NavMesh or physical startup difference requires investigation rather than an automatic correctness claim. This is a paired checkpoint scenario, not a guarantee of identical native frame cadence or workload.

## Baseline measurements

The baseline processed 14,195 events with 93 running samples. Maximum sampled lag was 85.678 game minutes; median lag was zero. This is a new instrumented comparison run, separate from the earlier 111.215-minute adversarial peak.

| Inclusive measurement | Baseline |
| --- | ---: |
| Population update | 66.008 s |
| Population advancement | 53.407 s |
| Multimodal route planning | 51.935 s |
| Road A* | 32.110 s / 5,038 searches |
| Walking calculation | 10.283 s / 28,730 calculations |
| Curb search | 3.916 s |
| Infrastructure preparation outside advancement | 11.821 s |

There were 42,851 walking-plan calls, 15,921,715 public segment checks and 18 periodic refreshes. Ordinary routine selection and opening-interval work were small by comparison. Maximum observed advancement was 138.464 ms, and maximum preparation was 938.590 ms. The latter confirms that preparation contains work outside the advancement soft budget; this pass does not eliminate that stall source.

## Candidate measurements

The candidate processed 14,226 events versus 14,195 in the baseline. The workload counts differ, so these totals are a paired scenario comparison rather than identical-work microbenchmarks.

| Inclusive measurement | Baseline | Candidate |
| --- | ---: | ---: |
| Population update | 66.008 s | 44.887 s |
| Population advancement | 53.407 s | 35.743 s |
| Road A* | 32.110 s / 5,038 searches | 15.758 s / 4,987 searches |
| Walking calculation | 10.283 s | 10.426 s |
| Infrastructure preparation | 11.821 s | 8.408 s |
| Maximum sampled lag | 85.678 game min | 90.274 game min |
| Median sampled lag | 0 | 0 |

Advancement method wall time fell about 33%; road-search wall time fell about 51%, with about 1% fewer road searches. **Peak backlog did not improve.** At the rig's 30-FPS cap, many planning events still exceed the two-millisecond soft budget and therefore permit only one expensive event per frame. Around the second midnight, the candidate processed more events and searches with less measured work, but a preparation refresh also landed inside its observation window. These samples do not isolate a backlog regression or demonstrate a gameplay FPS gain.

## Native equivalence investigation

Both full snapshots reached simulation minute 5777.0, but the canonical comparison failed: 5,412 differing fields across 750 residents. All non-resident state fields matched, as did all 957 identities, homes, day/shop/leisure assignments, preferences, joined/departure scheduling and job-change counters. Differences included six retained visit sequences, fourteen active route mode/road-node sequences, one route present/absent, seven residents' completed-journey counts, five missed-activity counts and one car's model/paint identity. Some differences are substantial, not rounding noise: the largest same-sequence visit-time difference was 121.67 game minutes, and the largest parked-position difference was about 1,025 metres.

Startup native time and initial physical progress already differed. Different execution cadence can also change when live native paths are queried. These are plausible confounders, not a demonstrated explanation. The controlled check below found no planner or fingerprint mismatch for the same native inputs. It does not establish that every full-run difference is harmless or identify the cause of each difference. Full native-state equivalence must not be claimed from the performance comparison.

## Same-request native comparison

A separate diagnostic follow-up ran the exact retained original A* immediately after each optimized search on the same native graph, request, clock rate, expansion limit and public-access delegate. The reference bound the original, uncached native `Disabled` method; it did not reuse optimized permission results. Production outputs and callbacks were preserved. The helper independently streamed the original topology byte encoding as each native node was captured, then compared the complete fingerprint. This adds substantial work and changes frame cadence, so none of this run's timings is used as a performance benchmark.

- **2,813 of 2,813 native requests matched exactly**, including 2,109 successful routes and 704 refusals. Comparisons cover path-node order and exact IEEE-754 bits for each leg's geometry and duration.
- Zero mismatches, errors, skipped non-native requests or additional reference-induced topology invalidations.
- **Eleven complete 18,791-node snapshots matched the original fingerprint bytes.** Every snapshot observed all nodes at capture time; none relied on a late reconstruction.
- Coverage included a midnight commute burst. The helper was then disabled and the game paused for cleanup. This is observed same-input route/fingerprint equivalence, not a full independent city replay guarantee. Every observed snapshot's original and candidate fingerprint was `933cc42989ab85fe41d019deada182e9bf4bc84eb106720de55404ff883ba9a1`.

The copied diagnostic helper SHA-256 is `34b568b1774487673727cb3dff5f41a618e96f5bb376fb84a58bff067e9d191a`. It requires the exact candidate DLL, isolated Mac profile, `-agent-tests` and an FT disposable save, with explicit opt-in. It is not included in the mod package. No production code changed between the performance run, physical/persistence smoke and this follow-up.

## Physical movement and persistence

The candidate's normal-speed smoke test sampled up to fourteen walkers and two cars. All 98 comparable walking and four driving lease pairs had both physical movement and increasing journey progress, with no sampled regression, invalid body, navigation, duplicate, cap or orphan error. This is short discrete coverage, not a full-trip, collision or visual-FPS validation.

A paused save matched the live logical state exactly. Reload retained the stored payload byte-for-byte (SHA-256 `409429f423e1148557e59febbe2636b86c13b5b6813c1e3009246659fb439c39`), all 957 identities and assignments, and allocated car appearances. The native clock advanced 0.333332061768 minutes on reload; this was not an independent replay comparison.

The paired-run campaign restored the original installation manifest byte-for-byte, verified all installed hashes, and stopped/released the rig with no pending command. All pre-existing named checkpoints remained unchanged; 45 of 48 raw saves were unchanged, with only three native recovery saves changed. Other isolated-profile changes and new diagnostic saves/RLO images are inventoried in the ignored cleanup evidence. Normal Mac and Windows profiles were not used. Whole-process sample footprint was 9.3 GiB (peak 9.4) for the candidate versus 9.0 GiB for the baseline; this does not establish a mod-specific memory regression or saving.

The separately reserved shadow follow-up also restored the original installation manifest byte-for-byte, verified every installed hash and stopped/released the rig with no pending command. All named checkpoints remained unchanged; 48 of 50 raw saves were unchanged, with two native recovery saves changed. Its eleven existing-file differences and five new RLO/analytics files are inventoried under `build/background-performance-shadow/run/`. It created no new named save or raw snapshot. Neither campaign reproduced the earlier renderer crash.

## Evidence and limits

Ignored local evidence is under `build/background-performance/`, including the original source copies, baseline package, diagnostic helper, initial/final snapshots, samples and analysis. The Opus opportunity report and bounded follow-up are under `build/background-performance-opus/`. The copied shadow helper and its native result/cleanup evidence are under `build/background-performance-shadow/`. Raw saves, game assemblies and diagnostic helpers are not shipped.

The earlier native HDRP/RLO Flare camera crash remains an unresolved separate stability investigation. This performance pass does not claim to fix it. Windows behavior, sustained larger populations, full frame-time distributions and subjective playability remain unverified.
