# Performance implementation — 27 October 2026

Development-source optimization of the current schema-13 / 0.10.0 candidate. Peter requested Opus 5.5 at high for complex changes, GPT-6.1 Sol for straightforward changes, and Opus/Astra/Grok/Sol reviews. Two isolated Opus implementations and two isolated Sol implementations worked from the same dirty-source snapshot; no agent/CLI turn caps were used. The earlier load-once discovery change is retained.

## Changes

- **Physical road sampling:** validate each road node and shared junction once within one read-only sample. Public waypoint resolution and each later sample stay live. The scope closes before vehicle loading, ownership pins and activation callbacks. Geometry, historical fragments and rear-clearance rules are unchanged.
- **Walking quotes:** within one K>1 quote operation, reuse its latest native calculation for identical endpoints/filter. Cold plan plus validation uses one query instead of two; stale replacement can use one instead of three. Warm quotes and every new operation still query live. Public access checks, route copies, tolerances and standalone validity checks remain.
- **Physical selection and saved cars:** skip building the model catalogue when a car already has a saved model, check cheap slot predicates before hierarchy classification, and filter stationary indoor residents before position calculation/sorting. Preserve model/paint initialization, original slot order, ownership rechecks, candidate order/caps/cooldowns and queue planning. Sum route durations once inside nonmutating operations, with the same floating-point arithmetic and no retained mutable-route cache.
- **Activation and save validation:** index food-place references once per validation; preserve duplicate/null/malformed refusal semantics. Activation completes the same serialization and exact UTF-16 size checks without retaining the discarded JSON string, including global serializer defaults and escaped output. Normal saves and schema protections remain.
- **RLO compatibility:** reuse immutable reflection metadata for the same assembly object. Readiness, contract version, identities, appearance agreement and save-time checks stay live at the existing cadence. Separate buffers protect nested reads; incompatible reads discard bindings. Binding count/time is included in the existing summary.
- **Native checkout:** reuse the supply image obtained earlier in that one checkout if registration, ordered menu and game minute still match; otherwise re-read. Actual native sale, pending receipt, live post-sale stock read and all reconciliation checks remain. A successful synthetic checkout performs three native simulator setups instead of four. Supply/setup reuse counters are available for later diagnostics.

No population/body cap, clock rate, route-choice policy, ownership scope, RLO source, schema, readiness budget or hidden-skip budget was changed. New RLO identities are never synthesized. No game was launched or rig installed for this increment.

## Offline validation

Frozen candidate source manifest SHA-256: `2a85c9159b4e7bc39f7dfb7e3138385c08622834cd58a22f2a777925554d8dea` (443 files). Full game-target compilation with warnings as errors produced DLL SHA-256 `8e2735ae97a9bb88a71b7336e4c291b11ce5e941aff8e9e186bb56a9ffaed69e`. Compilation is not native execution.

The integrated validation script completed 15 jobs successfully:

| Check | Result |
| --- | --- |
| Core/Persistence/Compatibility suite | 1,218 passed, no failures/skips; baseline 1,200 passed |
| Physical selection/slot/sampler differential | 27,561 checks |
| Road handoff/sample scopes | All actual-source checks passed |
| Road invalidation / speed / closure / ceiling | 265 / 602 / 35 / 69 checks |
| Walking validity / scoped quote operations | 28 existing + 48 new checks |
| Native supply / checkout doubles | 19 / 54 checks |
| Startup clock / discovery | 17 / 677 checks in this integrated run |
| Vehicle ownership / native command doubles | 235 / 65 checks |
| Full game-target compilation | Passed, warnings as errors |

The unchanged road-handoff harness had an obsolete assertion that rejected a lone leading exit regardless of clearance. An independent baseline run reproduced the failure. The corrected assertion tests both pre-clearance refusal and post-clearance allowance, matching the preexisting production rule and its Core tests. No production admission rule was changed. Two older replay tool projects (`food-demand-curve`, `food-native-pilot`) have preexisting reference/build failures on both original and changed source; they are not included among the passing jobs.

## Work reduction, not FPS evidence

- A nine-node road fixture validates each node once and its shared junction once; the old source checks that junction eight times. A separate 18,000-step differential matches complete samples, failures and invalidation reasons: native getter reads 795,557 → 301,869, junction checks 46,588 → 6,830 and full node validations 104,118 → 28,463.
- Physical fixtures retain identical attempts, slots, car fields and bitwise route samples while reducing position calls 93,665 → 56,231 and hierarchy queries 88,308 → 33,300.
- A three-day, two-shop K=2 walking replay with corridor changes retains identical serialized state and purchases: native path-query doubles 545 → 489, with 498 validity checks and 24 rejections unchanged.
- A synthetic 1,000-resident / 2,000-consumer activation allocates approximately 7.90 MB less per call than indexed Write (41.3% fewer thread-allocated bytes). Its exploratory timing sample did **not** establish a CPU speedup. These are allocation counters, not retained/peak process memory.
- Native checkout doubles retain normal stock, bag, order and receipt outcomes with 4 → 3 setups. Binding reuse removes metadata construction, not the full 400-donor live read; the old 30–262 ms RLO-check spikes cannot be attributed to binding alone.

These fixture counts do not establish rendered frame-time or load-time gains. The prior measured 31.6292 ms combined-mod p95 failure remains the latest game evidence and the release performance gate remains open.

## Review

All four independent final reviewers found **no blocker for bounded development-source integration** on the same frozen candidate:

| Reviewer | Outcome |
| --- | --- |
| Claude Opus 5.5, high | Clean; independently ran boundary harnesses and inspected native methods |
| GPT-6 Astra | Clean; verified all 443 source hashes and independently ran 1,218 tests plus boundary harnesses |
| Grok 4.7 (provider model `grok-4.7-build`) | Clean; inspected source, native methods and integrated validation logs; did not rerun tests |
| GPT-6.1 Sol | Clean; verified all 443 source hashes and independently ran 1,218 tests plus boundary harnesses |

This is source-integration acceptance, not native playtest or release clearance. All four assessed the real checkout differences under injected stock writes and the walking stability assumption below. No reviewer treated a post-purchase exception as an absence of side effects. An early progress-log view in the Opus report contains only the first completed integrated jobs; the completed integrator summary records all 15 jobs passing. No source repair was requested by the final panel.

## Native boundary limits

Walking reuse assumes the engine does not apply a navigation change between two observations inside one synchronous quote operation. Source inspection found no mod mutation API, yield or game-script callback in that window; synthetic tests preserve every public-access check and detect between-operation changes. This does not establish Unity's intra-call NavMesh stability. An injected intra-operation navigation change can produce a different result; no cross-request validity cache was added. A temporary native duplicate-query comparison remains appropriate before asserting complete native equivalence.

Checkout reuse was checked against installed native setup/capacity/opening helpers and the known mod source paths. Those can initialize scratch/lazy state or produce malformed-data logs, but the inspected path does not change stock, bags, orders, ownership, staff or money before the sale. Assembly references independently hashed during review:

- `BigAmbitions.dll`: `a06699c98b9a22775c763d0818fec23fd2842592ac3bab125a386bcf4c17951e`.
- `BigAmbitions.Items.dll`: `8d6b1ef122b30a75c81ad73fcc1fcd66c170996a052540660139b2859a78768f`.

Four synthetic hooks that inject stock writes between the observation and sale demonstrate a real difference: the purchase can occur, then reconciliation leaves an uncertain/pending receipt where the old second read would have confirmed it. This is **not** a side-effect-free refusal. The inspected supported path has no such write; arbitrary additional hooks are not covered by that argument. The post-sale check prevents incorrect confirmation, not the purchase itself. Native meal/accounting reconciliation remains required before deployment.

Physical fixtures do not reproduce Unity destroyed-object operators or the entire rendering/collision/acquisition lifecycle. A warm saved car with no ordinary model available also changes only its final diagnostic stage label (`model catalogue` → `inactive model slot`); refusal and saved fields are unchanged.

## Remaining work and evidence

A matched isolated native run must measure combined and component frame distributions, query/setup/binding counts, visible traffic/pedestrian continuity and native food stock/bag/order/receipt/operating-account reconciliation. No ordinary-save deployment, release or recording clearance is implied.

Deferred: admission-preflight reuse inside `PhysicalVehicles.TryAcquire` across its separately reviewed mutation boundaries; broader walking/supply caches; A* landmarks; readiness and hidden-skip budget changes. The skip record averages far below its event cap and does not isolate time-budget/native-hold effects, so increasing those limits without attribution was not justified.

Ignored local evidence is under `build/performance-implementation-20261027/`: frozen dirty baseline and manifests, each agent's source/patch/report, exact model/session launch records, the candidate and compiler output, all integrated validation logs, preliminary/final reviewer reports, and negative-control/differential evidence. Native assemblies, decompiled game code, saves and credentials were not added to Git. Main-checkout integration preserves all preexisting changes.
