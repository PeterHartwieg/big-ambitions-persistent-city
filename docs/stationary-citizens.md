# Stationary citizens: optional stops

Peter approved these on 28 October 2026 (DECISIONS-2 and DECISIONS-3) and set the meaning of the share on 29 October (option B). [Original decision record](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/decisions/2026-10-28-optional-stops.md). Other decisions in force are in the [register](decisions/2026-11-10-city-direction.md).

| Kind | Existing activity it uses | Game minutes |
|---|---|---:|
| Individual break | Employee at an open workplace, mid-shift | 5–10 |
| Window shopping | Shopping visit at an open shop | 1–3 |
| Resting | Park visit, at a validated park edge | 10–40 |
| Chatting | Two visitors at the same nightlife venue | 10–30 |
| Performer audience | Pedestrian passing a live performer on an existing walk | 2–8 |

## Policy

- Any rain cancels every optional stop, audiences included.
- Stops use existing eligible visits only. No minimum crowd, anonymous audience, extra nightlife outing or replacement citizen is created. Native anonymous stationary zones stay suppressed. Hour and rain street crowds stay unchanged; weather- and time-aware routines are a separate future feature.
- `OptionalStopChoicePercentage` in `persistent-city.json` (default 25, clamped 0–100) is the **target share of eligible visits that get a real stop**, capped by feasibility. It is not a per-visit probability. 0 means no new stops; 100 means every eligible visit with a feasible stop gets one.
- A visit's choice is saved and never re-rolled on reload, camera movement or route rebuilds. A changed setting applies only to newly admitted visits.
- Stops overlay the existing activity. They never extend its deadline, create travel or move money.

## Accepted limits

The setting does not guarantee a realised share:

- Geometry sampling is not saved, so two replays of one save can differ in which visit gets a stop.
- After a long time without feasible stops (for example cold geometry after load), a backlog of pending chances is spent on the next feasible visits.
- Chance hand-ons go to the lowest stable citizen identity first, so some residents stop more often than others.
- At high settings pending chances run out of storage and are dropped, so the realised share falls below the setting.

The [29 October native campaign](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-reviews-archive-2026-11-09/docs/reviews/rig/2026-10-29-pr3-optionB/README.md) measured 22.6% cold over 10 days and 24.4% over 28 days at `b646dae`. It has not been repeated at the current head.
