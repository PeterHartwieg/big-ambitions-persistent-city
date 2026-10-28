# Feasible optional destinations — 23 October 2026

Peter approved continuing from the one-third calendar trial, with the PC available again. This increment checks the feasibility of new shopping/leisure outings before committing a resident's journey. Connecting existing shopping stops to purchases remains the following step.

## Implemented behaviour

New optional outings retain their original activity window. The familiar shop or seeded daily leisure venue is considered first. A deterministic, distance-weighted shortlist supplies alternatives from the resident's actual current location, with stronger neighbourhood preference than permanent assignments. Attraction and native traffic still influence variety; ownership does not influence eligibility.

The search checks at most eight candidates and makes at most two full route requests for the optional destination. Opening schedules can reject a candidate without a route request. An actual route estimate must leave at least five game minutes for shopping or fifteen for leisure before the venue closes or the activity ends. These are provisional useful-visit floors inside the existing window, not additional free time. Walking-only residents also use conservative distance/time refusals; walking bounds never exclude an allowed car/transit route.

The selected route is used directly for departure. A resident can leave before opening, reach the venue and wait there. If the bounded search finds no feasible candidate, the resident returns or stays home until the original deadline. That fallback survives reload and early lifecycle/clock-rebuild events, so the same optional window is not rerolled. An ordinary return-home route can be needed in addition to the two candidate searches.

Already committed journeys and waits retain their saved destination/deadline. Work/study assignments remain unchanged. Unexpected closures or actual traffic delays can still cause missed activities. No new save fields, customer minimum, free purchase or household income is introduced. Ordinary shopping/leisure visits still have no economic effect; existing opted-in meal outings retain native transaction ownership.

## Diagnostics and validation

An optional per-event observer records each missed-activity increment with its cause, separately from rejected candidates and successful selections. It allocates diagnostic rows only while subscribed and survives route-timing rebuilds. The native helper uses it in both builds: the baseline differs from the preceding clock candidate only by diagnostic observation, with no destination-policy change.

All **1,048 tests pass**. New coverage includes closed, unreachable and too-late preferred venues; leaving before opening; admission and fallback through reload; route-request bounds; preserved committed waits; multimodal pruning safety; deterministic variety and diagnostic subscription continuity. Two earlier closure tests now change the hours after departure, preserving their unexpected-closure invariant while allowing the new planner to reject known bad schedules.

The candidate compiles against fresh isolated-PC native references with warnings as errors. Candidate DLL: `4c03585d369a02294cb63ab72340704be0083b24e8e91bfca122fcc351aace1d`.

Synthetic full-routine checks use 400 mixed-hours venues, no work assignments and one-third-speed walking. At 1,000/2,000/5,000 residents, serialized outcomes are identical with event budgets 1 and 128. At 5,000, admitted optional visits increase 2,158→6,316 and misses fall 4,494→410; route calls increase 10,775→13,565. Measured one-event-slice Core work rises about 116→365 ms for the full day, and allocated bytes about 63→213 MB. Removing per-candidate predicate allocations reduced the candidate’s allocated bytes by about 45%, with identical saved outcomes. These are single-process synthetic measurements, not native navigation or rendered performance. More viable outings require more work; existing scheduler budgets remain in force.

## Native evidence

The matched isolated-PC comparison uses the same `FT Food Routines Settled 20261022` checkpoint, day 4 01:00 through day 5 01:00, at one-third calendar speed and native 6× fast-forward. Physical population bodies are disabled. The 957 residents and stocked/staffed 15/hour pastry shop at 30 Third Street are unchanged between starts; hours are 08:00–18:00.

The completed reference day has 948 missed activities: 514 late arrivals, 364 without usable opening time and 70 failed route waits. Every late arrival is shopping/leisure; 47 optional trips already had expired deadlines in the starting checkpoint. Work contributes 58 misses (26 closed arrivals and 32 route failures); work assignment policy is outside this increment. Reference admitted visits are 228 shopping, 238 leisure, 698 work and 16 study, with 38 native food purchases.

The completed comparison gives:

| Per observed game day | Previous planner | Feasibility planner |
| --- | ---: | ---: |
| Admitted shopping visits, including meals | 228 | 429 |
| Admitted leisure visits | 238 | 552 |
| Missed activities | 948 | 446 |
| Late arrivals | 514 | 47 |
| Completed journeys, all purposes | 3,523 | 3,644 |
| Native food purchases | 38 | 38 |
| Native settled gross revenue | $389.61 | $389.61 |
| Work / study admissions | 698 / 16 | 698 / 16 |

All 47 candidate late arrivals belonged to already-expired windows in the starting checkpoint. No newly chosen optional outing missed admission on arrival in this run. Of the remaining candidate misses, 312 exhausted the two-route search, 29 had no feasible candidate in the shortlist and 58 were unchanged work failures. A bounded search is not proof that no suitable place exists anywhere in the city. The planner selected 537 alternatives and 384 originally proposed venues.

Native advancement p95 was essentially unchanged, **3.075→3.004 ms**, with total measured advancement **10.30→9.68 seconds** over the game day. The maximum call increased **19.96→27.12 ms**; sampled lag decreased **1.96→1.62 game minutes**. These timings include diagnostic trace capture and exclude route preparation and rendering. They do not establish an FPS improvement or native 5,000-resident clearance.

Stock/bag depletion matches all native customer counts, orders settled, all food outings returned, and saving/reloading plus explicitly replaying a paid intention produced no duplicate sale. Every missed-activity increment has a diagnostic reason.

The same checkpoint, start minute, stock, staffing, scopes, identities, assignments, journeys and other population fields were retained. Loading this older normal-clock checkpoint recomputes cached commute times against live native navigation: 287 estimates differed, mostly rounding; 33 differences exceeded one millisecond of game time and the largest was 23.71 game minutes, with matching departure adjustments. The evidence records each difference. This limits causal precision; it is a directional native comparison, not bitwise-identical route input. Offline cadence checks supply the deterministic control.

Both runs restored and verified the rig's 4,012 preexisting files, preferences and installation manifest, removed the helper and released ownership. The final stopped/unheld state is checked separately. Normal profiles were untouched. The private package remains disabled.

Keep the one-third calendar and feasibility planner as the candidate. Next, let eligible purchases happen during existing shopping stops, using the resident's current journey, remaining visit time, budget and native stock/service constraints. Work assignment feasibility, household incomes and rendered population performance remain later gates.

The first reference-to-candidate handoff encountered a plain-text stop response that the runner tried to parse as JSON. The reference evidence was preserved and the rig restored/verified/released; the corrected runner then reacquired the rig and deployed the candidate from that restored baseline. Final cleanup also needed a bounded wait for the asynchronous game shutdown; no file restoration ran while the player was still alive.

Evidence is retained under ignored `build/routine-venues-20261023/`. Earlier transfer and test-fixture failures are retained separately; they are not successful validation.
