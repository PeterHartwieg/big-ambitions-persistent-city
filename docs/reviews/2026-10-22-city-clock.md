# City clock at one third — 22 October 2026

The isolated Mac comparison supports one-third calendar speed as the next pacing baseline. The same 15-capacity shop sold **13 → 42 meals over one game day**, with unchanged price, stock, staff and break policy. More shopping and leisure visits completed throughout the city. This is directional evidence from one fixture, not a claim that every shop becomes viable.

## Implemented

`GameClockFactor` accepts 0.1–1. The private package selects one third but remains disabled by default; an existing configuration without the field retains factor 1. The policy starts only after the population initializes, is scoped to the current save object, and unpatches on shutdown/failure. Movement and Unity physics settings are not changed. Schedules, budgets, eating/service durations and deadlines remain in game minutes; time-machine skips bypass the calendar multiplier.

Driven Rides' positive ride-clock multiplier defers while Persistent City owns a slower calendar. An explicit zero/held clock remains respected. Factor 1 leaves Driven Rides' own setting intact. No Driven Rides or RLO source/package was changed.

A separate native Driven Rides 0.2.0-rc7 stand-taxi trip to 62 First Street completed in 217 real seconds. Live driving samples retained one-third clock speed throughout, despite the installed Driven Rides option being 25%; the slowdowns did not compound. Unity time scale remained 1 and fixed delta time 0.02. The ride reported no contacts, red-light runs or stalls. This is one compatibility trip, not broad vehicle certification.

Clock changes now retime an active meal's arrival notification, planned stay and outstanding return estimate alongside its committed journey. Service already underway, original work deadlines and paid receipts remain unchanged. Six interrupted-trip/reload tests cover slowing and restoring the clock.

The first slower run exposed a test-limiter interaction: the bridge capped a final tick before the calendar multiplier, which repeatedly shrank the remaining time. The calendar prefix now runs before boundary caps. A new clock tracker requires two complete agreeing frames before changing travel pace, retaining the previous positive rate through isolated clipped ticks and pauses. Five regression cases cover the observed failure, multiple ticks per frame and sustained unsupported rates. The repaired native runs both passed a short boundary/pause check before reloading their shared checkpoint.

## Matched native comparison

957 residents; disposable `FT Food Routines Clock Base 20261022`; day 2 01:00 through day 3 01:00. One converted, explicitly stocked and staffed shop at **64 Third Street**, open 08:00–18:00, capacity 15/hour, croissant price approximately $9.24 and staff quality 75. Both starts have exactly the same game minute, population identities, stock, staff and food scope. Both use native 6× fast-forward with physical population bodies disabled. This Mac fixture is different from the earlier PC shop at 30 Third Street.

| Measure, per observed game day | Normal clock | One-third clock |
| --- | ---: | ---: |
| Native purchases | 13 | 42 |
| Native settled gross revenue | $120.18 | $388.28 |
| Chosen food outings | 14 | 56 |
| Complete walking quote pairs | 25 | 147 |
| Completed journeys, all purposes | 2,414 | 3,379 |
| Admitted shopping visits, including food | 20 | 229 |
| Admitted leisure visits | 17 | 212 |
| Missed-activity counter increments | 841 | 1000 |
| Sampled peak customers serving/queued | 3 | 6 |

Baseline: one chosen food trip failed its arrival/queue deadline. Candidate: thirteen failed that check and one was missed because the native clock had passed its purchase window. All outings had returned by the end; no uncertain purchases. Product and bag depletion exactly match native customer counts. Daily orders settled; saving/reloading the settled result and explicitly replaying a paid intention produced no duplicate stock movement or sale.

More missed activities coexist with many more admitted outings. The counter is not a failure rate: these runs do not log a denominator of identical planned opportunities or classify each missed activity by cause. Existing destination selection does not guarantee reachable routes or compatible opening hours. Investigate those choices next; do not attribute the entire difference to lateness or claim that slower time fixes every routine.

## Performance and scope

| Native diagnostic measurement | Normal | One third |
| --- | ---: | ---: |
| Total measured food-decision work | 127.0 ms | 157.2 ms |
| Food decision p95 | 0.277 ms | 0.399 ms |
| Food decision route calls | 50 | 294 |
| Total population advancement work | 8.87 s | 12.24 s |
| Advancement-call p95 | 5.89 ms | 3.50 ms |
| Maximum advancement call | 29.01 ms | 42.61 ms |
| Maximum sampled lag | 26.39 game min | 1.61 game min |

The candidate performs more work per game day but spreads it over roughly three times as much wall time. Advancement timings include diagnostic trace capture and exclude route preparation/rendering. Peak calls still exceed the 2 ms soft budget. This is **not a rendered FPS or 5,000-resident native clearance**.

Offline full-scheduler checks with eight competing shops pass for 1,000/2,000/5,000 residents, with identical order identities and event counts when conservative pruning is toggled. At 5,000 residents, one-third time increases bounded walking queries from 8,589 to 47,489 and simulated purchases from 575 to 638; measured pure-Core window work increases from about 108 to 179 ms. These synthetic paths exclude native navigation. Slower time does not automatically reduce route work. The existing event and per-frame budgets remain in place.

## Validation and next step

- **1,038 tests pass**, plus warnings-as-errors compilation against current local native references. All 66 compiled production sources match the tested frozen snapshot.
- Tested DLL: `0c37016f28ff902ec714344992915adac669518c5b635e8c41400deed38ffb50`.
- PC preparation stopped at Peter's request before deployment or game launch; its reservation was released. Testing then used only the Mac rig.
- The paired campaign and separate taxi follow-up restored and verified the Mac's preexisting files, preferences and installation manifest and released ownership. The final taxi cleanup verified 3,068 preexisting files and removed the diagnostic helper; fresh status confirms stopped, unheld and zero pending operations. Normal profiles were untouched.
- Evidence: ignored `build/clock-pace-mac-20261022/mac-run-r3/`, `ride-check/`, `source-r3/`, `source-r3-manifest.json`, `full-tests-r3.log` and both scale reports. Earlier attempts and their restoration receipts are retained as failed/superseded evidence.

Keep one third as the candidate pacing policy. Next, make destination choice respect reachable routes and usable opening windows, then connect the now much more frequent existing shopping stops to purchases. Preserve budget/quality/district variety and finite stock/service; no guaranteed customer minimum or owner protection.
