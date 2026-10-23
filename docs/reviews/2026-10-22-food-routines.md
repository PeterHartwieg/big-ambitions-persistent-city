# Routine-based food demand — 22 October 2026

Peter asked for food decisions grounded in realistic routines, subject to game mechanics and performance, and for small local shops to support useful expansion. This increment connects the read-only food experiment to recorded population activity and adds service constraints. It does not enable authoritative food sales or change the current work/leisure schedule templates.

## Implemented

- An explicitly attached recorder observes completed home/work/shop/leisure stops from `CitySimulator`. Travel, opening waits and missed attendance supply no usable stop. Normal play has no recorder, extra scheduling or food scan. Recording leaves population results unchanged in the paired regression.
- One purchased-meal opportunity per resident/day uses personal wake policy, stable individual timing and modest reproducible daily variation. The actual recorded stop supplies the origin. Late travel can delay an opportunity, increase urgency or leave it unresolved. This is a bounded retail slice, not a full nutrition/sleep model.
- A hypothetical outing must fit walking both ways, waiting for opening/service and eating before the recorded next departure. Work/study uses an explicit provisional 45-minute break; other stops allow at most 75 minutes. No actual employee roster or wage changes are introduced. A failed outing has no same-day retry yet.
- Effective sellable/staffed throughput is separate from demand. Unknown service remains unresolved; zero service refuses the purchase. Aggregate service time, queue patience, finite stock reservations and completion checks prevent a small shop from serving unlimited simultaneous customers. Queued intentions survive save/reload without duplicate spending or stock consumption.
- Route work has an explicit call budget, a bounded candidate shortlist and separate outbound/return path validation. Native walking geometry uses the existing cache; distance never becomes a fabricated successful route. Observed origins and hypothetical route estimates remain separate from executed food journeys.
- The insight view shows the origin/activity, outing window, return deadline, service time and reason for refusal. Schema 2 is separate from population schema 7; strict schema-1 food documents retain the old fixed-reference behavior through explicit migration.

Local source inspection of `RetailBusinessSimulator.GetMaxCustomerCapacityThisHour` shows that the native building capacity acts as an hourly ceiling and is reduced by equipment/staff capacity. Smoothing that effective rate into continuous service intervals is our modeling policy. An advertised product or a capacity label alone still cannot establish sellable stock and staffed service.

## Controlled expansion comparison

The same 957 residents and 6,699 purchased-meal opportunities were replayed over seven days in a synthetic two-neighborhood city using the real population event engine. Recorded origins were 3,633 work, 2,882 home, 130 shop and 54 leisure stops. Food windows varied reproducibly; each resident averaged 6.84 distinct meal times over seven days. The experiment adds no residents, needs or allowance when a shop opens.

| Controlled market | First shop meals | Second shop meals | Total served |
| --- | ---: | ---: | ---: |
| One 15/hour shop | 541 | — | 541 |
| Two 15/hour shops in different neighborhoods | 541 | 517 | 1,058 |
| One 30/hour shop | 965 | — | 965 |
| Two 15/hour shops at the same location | 509 | 449 | 958 |
| Second neighborhood shop unable to serve | 541 | 0 | 541 |

The second neighborhood location adds reachable demand without reducing the first shop's sales in this fixture. The overlapping pair shares some existing demand and also relieves queues. A larger shop helps the dense local market but cannot reach the other neighborhood's short walking outings. This demonstrates a mechanism for useful small shops, not calibrated balance or profitability. The two-shop market is deliberately insufficient for the entire cohort's concentrated demand; missed purchase opportunities are not a population hunger rate.

A 25% price rise at the second neighborhood shop did not reduce its sales here: it remained affordable and had no reachable competitor. This exposes the current limited outside option; home food, choosing not to buy, and later retries are still absent. The earlier competing-shop price comparison remains separate evidence. Do not interpret this spatial fixture as a general price-elasticity calibration.

## Validation and performance

The combined Release suite passed **977 tests**, including **20 new routine/service cases** plus the 15 prior food-demand tests. The full run includes the native-discovered long-route refusal regression. The 15 actual catalogue-reader checks also passed. A retained schema-1 demonstration migrated and reproduced its 42/6 baseline. Full native-target compilation passed with C# 9 and warnings as errors; frozen candidate DLL SHA-256 is `735795d7213410e268b699caa3a129ccb679220ffd5dbf1456a405f85e00d166`.

The synthetic 957-resident weekly food scenarios took about 28–100 ms each in the recorded Release run, excluding native navigation, setup and serialization. The slowest 16-event batch was 11.15 ms; event count alone is not a frame-time guarantee. The paired-neighborhood case needed 13,398 route-planner calls, at most two per batch, after walking-tolerance lower bounds eliminated impossible candidates. These fixture calls are cheap; native route cost requires its own measurement. No live background food loop is enabled, and no normal-play FPS improvement is claimed.

The HTML insight was checked in the browser: work versus day-off origins, return deadlines, and zero purchases in the no-stock case. Its 24-resident subset reruns its own queues and must not be read as the full-cohort result.

## Native observation

The initial attempt used the bridge clock multiplier at 60 while movement retained normal native speed. This distorts journey duration in game hours, so its all-home observation is not balance evidence. It also exposed a valid walking route longer than the one-day quote boundary. The final candidate refuses that route as outside the bounded experiment instead of aborting the batch, with a regression test. Subsequent observation uses native fast-forward with the clock multiplier at normal.

A second run captured 957 residents and 3,725 completed activity spans, ran the strict and controlled replays, and wrote both final states. Its final report then failed because two Brewed Beans branches share a display name. The diagnostic now keys branch totals by stable business ID. This reporting repair does not change the candidate mod. Both failed attempts stopped/restored/released the rig; they remain retained evidence, not successful end-to-end receipts.

The native diagnostic deliberately samples four shops selected by address, with a shortlist of three for each observed origin. This is not complete neighborhood coverage. A shop omitted by the shortlist stays unknown rather than proving that no food is available. The game's compressed clock also makes many native cross-city walks too long for a lunch break; such trips must be refused. Before interpreting native purchase rates as balance, extend geographically representative shop coverage and examine travel-time feasibility. Do not fix low coverage by inventing routes or inflating population.

The final repeat completed successfully in a fresh FT city with physical representation disabled. It recorded **957 residents and 3,729 completed activity spans**, from the first morning through day 3 midnight; the food replay uses the complete intervening day. Of 957 opportunities, **700 originate at work, 15 at study and 195 at home**. Another **47 have no eligible observed stop** and remain explicitly unresolved. The raw receipt's enum grouping includes these missing origins under the default Home value; `analysis.json` excludes them from the actual home-origin count.

The sampled market was Rise and Grind, NYC; Roasted Inc; Five Fries; and Donut Days. The 2,730 shortlisted candidates required **3,764 native route calls**, at most two per batch, with **1,353 complete round-trip quotes**. The worst quote batch took **10.1424 ms**. This is bounded diagnostic work, not a hard wall-time guarantee or full-modpack FPS validation.

| Native activity/route replay | Fulfilled | Unmet | Unresolved |
| --- | ---: | ---: | ---: |
| Strict native stock/service evidence | 0 | 0 | 957 |
| Controlled 15 meals/hour and 10,000 units per offer | 21 | 0 | 936 |

Unknown native stock/staffing cannot establish a sale. With explicit hypothetical availability, the four shops serve 3, 2, 3 and 13 meals respectively. Twenty fulfilled outings originate at work and one at home. Of the remaining opportunities, 47 lack an eligible stop and 889 retain insufficient route/market evidence. Missing shortlist coverage is not a proven unmet need. Complete round trips have a median of 332.74 game minutes, well beyond a lunch break; this geographically narrow sample is not a citywide sales forecast or an estimate of hunger.

The paused population serialization and sampled native prices, hours, closure flags, pending/completed orders and item cargo remained unchanged. The helper's counters and both final experiment documents were saved successfully. No actual customer purchase was booked by the experiment.

The runner then stopped the rig, restored and verified **all 2,876 preexisting profile files**, restored preferences and the installation manifest, removed the diagnostic helper and released its reservation. Seventeen new test-save/log artifacts remain in the isolated profile. Final status confirms no process and no held lock. Neither Windows nor the normal Mac profile was contacted or modified. Exact receipts are under `build/food-routines-20261022/mac-run-final/`; `mac-run/` and `mac-run-repaired/` retain the earlier failed attempts.

## Evidence and next boundary

Reproduction: [tools/food-routines](../../tools/food-routines/README.md). Ignored receipts and source snapshot: `build/food-routines-20261022/`, including `comparison/report.json`, `insight/`, the test/build logs and native diagnostic receipts. No raw saves, game DLLs or decompiled source are added to Git.

Before live economic authority, broaden local market coverage and assess native travel-time feasibility, observe effective staffing/sellable stock, integrate an actual chosen food journey with the population's activity ownership, and establish native sale execution without duplicate demand. Actual employee shifts, home-food supply and household money remain later parts of the agreed economy roadmap. Keep the useful small-shop comparison as a balance criterion as those systems arrive.
