# Vehicle classification: prototype comparison and implementation recommendation

21 October 2026. **Recommend cached type metadata plus a reusable component list, retaining the existing three service-component queries and every ownership-check call site.** Native Mac prototypes reduced the warmed ordinary-vehicle classifier's time from 66.27 to 8.01 microseconds per call, **87.91% less time**. This is a same-input method benchmark, not a measured FPS improvement or production release.

This follows the [running profile](2026-10-21-vehicle-classification-profile.md), where classification accumulated 40.634 seconds across 975,233 calls in 180 seconds. Production code is unchanged. The real classifier continued handling the game; the prototypes only inspected vehicles and temporary inactive test fixtures.

## What was compared

All variants preserve the initial live vehicle/prefab null checks and include inactive descendants. The baseline body was copied exactly from reviewed DLL source `782e13b1…`, with only its method name changed. The actual installed private production method was also called independently as the parity oracle.

| Variant | Native hierarchy queries on an ordinary vehicle | Median round mean, µs/check | Round range, µs/check | Time reduction |
| --- | --- | ---: | ---: | ---: |
| Current implementation | Three service searches, then script array | 66.27 | 62.41–74.41 | — |
| Cache type metadata; direct loop over array | Same four queries | 7.66 | 7.46–7.82 | 88.44% |
| **Cache type metadata; reusable list** | **Same three service searches, then script list** | **8.01** | **7.94–8.25** | **87.91%** |
| Cache type metadata; combine into one list scan | One script list query with service type checks | 4.28 | 4.13–4.50 | 93.55% |

Each variant ran 32,768 timed checks over the same 25 ordinary vehicles. Four rounds of 8,192 calls rotate all four variants through every order position. The listed median is the median of four round-average per-call times, not a per-invocation latency percentile. The first three-round pilot gave the same ranking and approximately 89% reduction for the conservative list version.

The cache/direct-loop change accounts for most of the improvement. It changes both repeated assembly-name lookup and LINQ enumeration; the experiment does not isolate their individual shares. Reusing a list was slightly slower than the array version in these timings. Its benefit is avoiding the explicit temporary-array request, with comparable method time. Combining queries saves a further 3.74 microseconds per check in this corpus, but changes the service-query implementation and early-out behavior.

## Narrow implementation scope

In `Runtime/PhysicalVehicles.cs`, keep `RandomAmbient` at every existing call site. Its service checks continue to use the same native types, order and `includeInactive: true` behavior. Replace the final array/LINQ predicate with a live list query and direct loop.

- Cache `Type → foreign/allowed` using the exact existing ordinal assembly-name policy: prefixes `BigAmbitions` and `Unity`, or exact `ExternalPlugins`, remain allowed. Read the assembly name once for a new type. Newly added components are still queried on every call, even when their type is already known.
- Rent one reusable script list. Remove it from the available slot while in use so a nested call cannot overwrite an outer call. Clear it in `finally`, including early return and exception paths. Retain at most one idle list per context, dropping oversized buffers; the prototype uses a capacity limit of 2,048.
- Keep `OwnershipLossReason`, `Valid`, `Position`, `Owned`, acquisition/release and all physics boundaries unchanged, especially the final validation after a permission callback. Preserve current exception behavior and Unity's destroyed-object null semantics.

Retained state consists of type decisions and empty list storage. It grows with distinct component types, rather than elapsed frames or vehicle instances. A warm cache of 27 types was observed, and repeated known-type checks did not grow it. No vehicle result, hierarchy snapshot or positive ownership verdict is cached. The prototype's `AfterFill` callback and benchmark inspection properties are test facilities and must stay out of production.

The single-scan version is a possible later refinement. All three service types inherit `MonoBehaviour` in this installed game, and the native parity fixtures passed. It still changes query composition, service early-outs and potential native-query interception by other mods. Rejection-path timing and broader service/subclass/native compatibility checks are needed before selecting it. The conservative variant already captures most of the measured saving without that extra change.

## Native correctness evidence

Final R2 passed **1,453 assertions**, including **223 fixture assertions**. These counts include warmup and repeated checks; they are not 1,453 distinct scenarios.

All four implementations matched the actual production classifier across all **30 native pool vehicles** before and after timing; 25 were ordinary. Temporary inactive fixtures exercised missing/restored prefabs, inactive roots/children, foreign components added after warming the cache, new foreign types, removal, reparenting, same-frame takeover/release, all three exact service types on both roots and children, and removal of those services. They also tested nesting in both directions (outer ordinary/inner foreign and the reverse), explicitly through both list variants, and buffer cleanup after an injected post-fill exception. Service inheritance and stable cache size for known types were checked.

The exception injection occurs after list fill; it is not a reproduced Unity query failure. Subclass fixtures, missing-script entries, deferred destruction, oversized-buffer retention and assembly-policy boundary tests remain useful production regressions. The classifier was not substituted into live driving, so this run does not validate changed force/ownership behavior. Existing ownership boundaries must be verified unchanged in the eventual production diff.

## Measurement controls and limitations

The isolated Mac rig used the reviewed `782e13b18df3026496150e478d537352cb3be9d28d73fb25c3ee5d8af89cdb23` DLL and the same installed local modpack/FT checkpoint as the previous profile. It ran background Metal, capped at 30 FPS, with two workers. After loading, it paused and settled; each batch verified both zero time scale and the same save object. The diagnostic helper rejects overlap between a classification benchmark and a general profiling capture. No build ran during timing.

Each timed block executes 256 queries synchronously, then yields to the game. Timings exclude yielded frames. Caches and buffers are warmed. Only the ordinary successful path is timed; service rejection has parity checks but no separate timing. Other-game work, scheduling and garbage collection can still affect a timed block. These are warmed same-corpus results, not cold-cache behavior, a whole-game comparison, Windows evidence or a guarantee to recover 87.91% of the previous running-profile cost.

**Allocated-byte measurements are unavailable.** Unity Mono exposes `GC.GetAllocatedBytesForCurrentThread`, but it returned zero even for a positive control allocating at least 65,536 bytes and keeping those arrays alive. R2 disables that counter and writes null allocation results. The pilot's zero values are invalid as allocation evidence. Source inspection shows removal of repeated assembly-name lookup and the explicit component-array request; zero total/native allocation and GC improvement remain unproven.

No runtime/physical failure was reported after the benchmark; the paused simulation had zero backlog and one road snapshot. That is health evidence only: there were no owned cars at the final paused sample. The two existing Bakery/PolyModsHyperreal missing asset-bundle warnings remain, so this is not full feature coverage of the modpack.

## Independent reviews and next gate

Astra, Sol, Claude Opus 5.5 and Grok 4.7 independently reviewed the prototype design. Astra/Sol favor the conservative cached-list implementation. Opus/Grok favor a single scan subject to additional compatibility/parity gates and accept the conservative fallback. All support caching type metadata while retaining live ownership checks. These are design assessments, not approval of a production change.

Review feedback led to symmetric benchmark-busy protection, per-batch pause/save checks, balanced order, root-service fixtures and explicit conservative-list nesting tests. The allocation counter was calibrated and rejected. Astra and Sol independently verified the final arithmetic and scoped interpretation. Opus proposed ordinary statics to avoid thread-local lookup overhead; the prototype retains thread-local storage for isolation. That micro-optimization is not required for the measured result, and neither choice permits Unity queries off the main thread. A review's claim of a new capturing LINQ closure on every original call was not adopted: the predicate captures no local state.

The next implementation should be a small classifier-only production diff with the relevant regressions, frozen-source review and a matched running baseline/candidate capture. Measure actual whole-frame/GC effects and verify service ownership, pause/speed transitions, reload and a longer run. No scheduler, traffic-rule, route, save-schema or citizen-identity change is indicated by these results.

## Artifacts and restoration

Ignored evidence: `build/vehicle-classification-20261021/`, including final/pilot benchmark JSON, analyses, exact diagnostic sources/build manifests, four design reviews, final Astra/Sol evidence reviews, installation receipts, original production-file hashes, logs and cleanup receipts. Final helper SHA-256: `1167fdca798d28755b548e0dff4ad6d12a5a3db95225edf54a69593d43d5cf9a`.

All **137 pre-existing production/test/tool source files** were verified unchanged. The Mac rig is stopped and its reservation released. The original installed DLL `e0756b4a74764c4b04e65cf0edd9a2043cff9ccb4e34dea2942a9063c0582124`, manifest and preferences were restored; all **2,507 pre-existing profile files, including 935 save files**, were hash-verified, and the diagnostic helper was removed. No save command was issued. Windows was not contacted and no new production build was installed.
