# Persistent City roadmap

What is left before the single release. Decisions in force, including Peter's 10 November city direction and the 12 November RLO overlap rule, are in the [decision register](decisions/2026-11-10-city-direction.md). Open slices, their dependencies and the reserved questions are in [next tracks](plans/next-tracks.md). There is no interim release.

Principles: bounded state, no extra route searches per decision, saved state replays identically after a reload, native accounting stays authoritative, nothing is written to rivals, every person on the street is a named resident or visitor (through-traffic cars are the one anonymous exception).

## Status at `a3a6c8d` (13 November 2026)

| Area | Status | Evidence |
| --- | --- | --- |
| Prove a sale (S1 reading, S2 probe, first-sale fix) | implemented; 15 targeted rows verified by rig at `060a7947` | [12b report](checks/rig-2026-11-12b-mac-results.md). That build predates GR-3, GR-4, GR-6 to GR-9 and #239. |
| Track X (entry docs, tree cleanup, test kit, code map) | implemented | #220 |
| Track P: P1 single-pass save, P2/P5 spikes and scans, P3 one path per leg | implemented | #223, #219, #221. The 40 ms p95 save target at 3,000 persons is not yet measured in game. |
| Track P: P4 Mono GC measurement (P-D) | planned | Needs a rig capability. |
| GR-1 to GR-4: business types, wallets, day plans, needs, purchase contract, one whole loop | implemented | #225, #227, #234, #236 |
| GR-6 to GR-9: evenings and households, workplaces and feedback, through-traffic, bodies and own cars | implemented | #237, #238 |
| RLO 1.0 overlap: Persistent City overrules where they overlap | implemented | #239, [RLO contract](rlo-contract.md) |
| GR-5: residents in homes the player cannot rent, visitor tuning table | planned | Waits on P-D and in-game evidence for GR-4. |
| E6 carts and food trucks | partial: PC side (E6b to E6f) implemented; E6a Street Food API planned | [E6 plan](plans/e6-carts-food-trucks.md) |

"Implemented" means merged after review. Only the rows a rig report names are verified by rig, and only at that report's build.

## Order

1. P-D Mono GC measurement, and batched rig runs of the merged GR and #239 checks on the current head ([rig checklist](rig-checklist.md)).
2. GR-5 population and visitor table, sized from P-A, P-D and whole-loop evidence.
3. E6a in the Street Food repository, then its vehicle rig checks.
4. F4f gate run record ([F4 plan](plans/f4-perf-gate.md)), once its fixtures and reserved caps exist.
5. Recorded rig walk-through: the same street at morning, midday and night.
6. Final performance gate on the final development head, with save latency in its own requested run.
7. Hands-on session: Peter's playtest, last, including A5's pointer and input checks 1 to 3 and check 12's interactive extension.
8. Single release.

## Deferred

| Item | Reason |
| --- | --- |
| Track F interior view (upper floors, polish) | No safe renderer found. The F0 experiment stays default-off. |
| Drinks and bundles | Deferred by the E3 and E5 decisions. |
| Physical fast-forward (6x) | The experiment drifted off roads and jammed. Fast-forward runs in the background. |
| Reachability-aware assignment | Destinations use straight-line weighting. |
| Indoor customers and staff shown as residents | No safe renderer; the mod claims no entries while the player is inside. |
| Shared household budgets, relationships beyond a shared home | Peter, 10 November: wallets stay individual. |
| Social and employee identity unification with RLO | Native employees and RLO social profiles stay separate. |

Older roadmap rows, the done list and superseded items are in the [13 November snapshot](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/roadmap.md).

## Performance gate

Peter, 30 October 2026, applied to every slice and the release:

- Mod-work p95 under 5 ms, leaving a buffer under the 10 ms frame budget for 100 fps.
- Single mod-work spikes under 30 ms are acceptable. Flag every mod-work spike of 30 ms or more.
- Whole-frame timing is reported separately. The four 104 to 175 ms evening whole-frame stalls from the 30 October review are unexplained.

Baseline: pass. The [30 October review](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/1273abd9a8ec23017deb12cbfe181f9f891b2682/docs/reviews/perf/2026-10-30-performance-review.md) measured mod-work p95 1.259 ms and a worst spike of 24.0 ms.

Latest in-game reading: not passing. The [9 November rig run](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/170) at 2,871 persons, on a shared PC, measured mod-work p95 between 0.9 and 5.1 ms per window and ten intervals of 30 ms or more. P-A, P-B and P-C target those causes. The gate has not been re-measured in game since they merged.

Every slice takes cheap, safe performance wins and reports before/after numbers in its PR description.
