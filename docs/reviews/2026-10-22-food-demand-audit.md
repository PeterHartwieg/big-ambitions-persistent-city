# Food demand diagnosis and scaling — 22 October 2026

Peter questioned the earlier eight purchases per day and requested a diagnosis/calibration that remains affordable for 1,000+ residents. The main constraint is the time available for a separate return trip. Lower prices alone cannot make this particular shop draw a large customer base. A modest break-policy change raised native purchases from eight to ten per day; performance changes reduced measured food-decision work by 92.5% in the repeated PC run. This does not establish commercial viability.

## Where the customers went

An instrumented repeat loaded the same isolated 957-resident city and ran one further natural day at the unchanged 30 Third Street fixture. It again produced eight purchases. All 910 evaluated decisions were reproduced exactly offline from their captured inputs; 47 other residents found no eligible stop during their opportunity window.

| First binding constraint or result | Residents |
|---|---:|
| Walking path unavailable or beyond tolerance | 470 |
| Outward walk alone exceeded the available break | 363 |
| Service, eating and return no longer fitted | 44 |
| Price exceeded the allowance | 24 |
| No eligible activity stop | 47 |
| Feasible shop lost to home/packed-food preference | 1 |
| Purchased | 8 |
| Total | 957 |

The 470 path cases comprise 163 beyond the geometric walking limit, 297 without an accepted outward walking path and ten without an accepted return. A refused native route does not establish which gate, path length or navigation constraint caused the refusal. All 363 original “Closed or too late” cases were actually too late on arrival, rather than closed during a feasible trip. Of 246 outside-option outcomes, 245 had no feasible shop purchase; the old broad “prefers packed/home food” label should not be read as a measured preference loss.

At the observed native clock rate, walking covers two world metres per game minute. A fixed 45-minute break minus four minutes of service and 15 of eating leaves 26 minutes for a round trip: at best about 26 metres each way. Roads, turns and gates can reduce that catchment further. The old test therefore mostly served immediate neighbours. This explains a low result without implying that all 957 residents should visit one pastry shop.

## Calibration and boundaries

Work/study break flexibility is now stable per resident: 25% receive 45 minutes, 60% an hour and 15% 75 minutes. Home/leisure retains its 75-minute limit. All opportunities still end before the actual next activity commitment and meal-window expiry. This is a provisional policy, not observed native employment contracts. Existing committed trips retain their saved deadline. Neither prices, purchasing power, food frequency, walking speed nor the number of citizens changes.

The held-input price replay chooses 12 purchases at $3, ten at $7, eight at $10.25 and three at $30. A uniform 60-minute diagnostic window permits 12 choices; 90 minutes permits 22 and three hours 67. These are independent choices at the captured origins, stock and queues. They do not settle native sales, include added queue interactions or prove that extended breaks fit later commitments. The larger windows were not adopted.

The next structural food step is to examine purchases during an existing shopping stop or as a short detour on a journey, including takeaway. At present every eligible decision requires a separate return trip, and ordinary Shop activities are excluded. Increasing restaurant-meal frequency or making everybody's lunch break several hours long would hide that limitation. Small-shop profitability and the amount of legitimate outside demand still require evidence; no minimum sales or owner protection is introduced. Household finances follow a credible demand/visit loop.

## Performance changes

The production scheduler remains event driven, with one saved meal record per resident and bounded advancement. It does not poll all residents every frame. An optional diagnostic observer carries decision explanations only while explicitly attached; it survives route-index rebuilds and is not serialized.

- Live decisions call the shared choice calculation directly. They no longer construct, clone, validate and schedule an entire one-person shadow experiment. Persisted shadow experiments retain their strict validating boundary and use the same choice calculation.
- Native market reads retain the enrolled building controllers and recheck their current access, business generation, price, hours, service and stock. They no longer scan every city building for every decision. There is no long-lived stock/price cache; checkout still reads current native state.
- A conservative geometric bound, using the current native walking pace, rejects round trips that cannot fit even before turns or delays. Complete outward/return routes remain required for candidates that survive it. This does not fabricate paths or change feasible purchases.

With tiered JIT compilation disabled, a seven-trial local benchmark of 1,000 captured decisions reduced median pure-choice work from 12.19 to 1.00 ms and allocations from 14.59 to 1.95 MB. At 5,000 decisions it fell from 60.13 to 4.90 ms. These figures exclude native market reads, route searches and rendering.

The complete Core scaling fixture uses eight competing shops, 100 distributed home origins, real meal events/queues/returns and synthetic complete walking paths. It advances the daytime window in one-event slices. With geometric pruning, route requests fell 16,504→1,864 for 1,000 residents; 32,866→3,586 for 2,000; and 81,789→8,589 for 5,000. Each pair retained exactly the same purchases and event counts; a repeat verification also matched purchase identity/order hashes. Optimized median total Core work was 15.25, 33.18 and 94.55 ms respectively across the whole simulated daytime window. Sampled maximum single-event cost remained up to 3.10 ms in the 5,000 case. These synthetic timings are not Unity frame times or native 5,000-resident clearance.

## Verification and native comparison

All 1,027 tests pass, including competing-offer equivalence, retained inputs, observer rebuild/unsubscription and a 1,200-resident route-pruning comparison at different event budgets. The actual catalogue adapter passes 27 checks against native field doubles, including a bound read visiting one enrolled controller in a 1,001-building inventory and rechecking access. The candidate compiles against the freshly copied PC assemblies with warnings as errors.

Instrumented baseline DLL: `ac6a9dfbb79d21c5240b708273a54d07c186be4047de183ce06d2bf2418bbcee`. Candidate DLL: `67f73f6cd155fcca1ef1c166c63da6c3e6967d9cf48ad0ee405c977463f154ac`. Both use the diagnostic helper `17dba030bad919d1282dc9a071005b0d644a10ec40974e4b9f33b14705eb10df`. Compiled source manifests, separate baseline/candidate captures, native reference hashes, replay reports and tests are retained under ignored `build/food-demand-audit-20261022/`.

The two native runs loaded the same day-four 01:00 save, each with 276 pastries/bags, the same $10.2528 unit price, 957 residents and 08:00–18:00 hours. Each ran to day-five 01:00. Both evaluated 910 decisions, and 47 residents found no eligible stop. The candidate chose eleven outings: ten settled and one missed its break deadline after queueing. The two additional buyers were naturally selected; no buyer, appetite time or acceptance was scripted.

| Native measure | Instrumented baseline | Candidate |
|---|---:|---:|
| Settled purchases | 8 | 10 |
| Settled gross revenue | $82.0224 | $102.5280 |
| Remaining pastries and bags, each | 268 | 266 |
| Food-decision route requests | 1,197 | 46 |
| Total food-decision work over the day | 1,500.43 ms | 113.02 ms |
| Median decision | 1.4689 ms | 0.0768 ms |
| 95th-percentile decision | 3.2141 ms | 0.2391 ms |
| Slowest decision | 16.6853 ms | 10.4206 ms |

Candidate decisions reject 724 impossible round trips before native path searches and 163 offers beyond walking tolerance. Twenty-three candidates receive complete route quotes. Prices, household allowances, traffic timing and walk speed remain unchanged. Stock/bag deductions and daily native customer/revenue history match purchases; no uncertain sales or unfinished meal outings remain. Receipts retain the eight prior-day and ten current-day purchases as intended.

These timings cover the food decision, including market reads and outward/return quotes, and exclude diagnostic serialization, committing the resulting journey, checkout and unrelated simulation/rendering. The runs use the same starting save but have different break allowances and resulting journeys; they are not identical instruction traces. The separate captured-input benchmark establishes exact choice equivalence for the shared-calculation optimization. A repeated scaling verification also compares hashes of purchase identity/order, not just purchase counts. The remaining 10.42 ms native decision spike exceeds the runtime's soft two-millisecond advancement budget; a native query cannot be preempted. Physical rendering is disabled, and this campaign does not establish full-modpack FPS or a long-session memory bound.

A final native save/reload after daily settlement preserved purchase counts, spending, receipts, history, orders and stock. Explicitly replaying a paid intention returned its receipt without an extra order or stock debit. `native-checks.json`, `native-comparison.json` and `settled-reload-checks.json` retain these results.

All 3,971 preexisting rig profile files, original preferences and the installation manifest were restored and verified against the fresh backup at `C:\BigAmbitions-AgentTests-Rollback\FoodDemandAudit-20261022`. The helper was removed; fresh status confirms the rig stopped, reservation released and zero pending operations. New disposable FT saves remain available. The normal game/profile, PC power and backup configuration were untouched. The disabled private package contains the exact tested candidate; `final-source-check.json` confirms all 64 compiled sources still match its frozen manifest.
