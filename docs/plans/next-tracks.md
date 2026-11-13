# Next tracks: what is still open

Open work at `a3a6c8d` (13 November 2026), in the order of the [roadmap](../roadmap.md). Decisions in force are in the [decision register](../decisions/2026-11-10-city-direction.md). One PR per slice. The save schema stays 1; a format change is made in place. Nothing here plans a release, a Workshop upload or an RLO change.

S1, S2, X-A, X-B, P-A, P-B, P-C, J0, GR-1 to GR-4 and GR-6 to GR-9 are implemented (#217 to #238), as is the RLO overlap rule (#239). Their slice text is in the [13 November snapshot](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/next-tracks.md). Implemented is not verified by rig: the [12b report](../checks/rig-2026-11-12b-mac-results.md) covers 15 rows at `060a7947`, before GR-3, GR-4, GR-6 to GR-9 and #239. The rest wait in the [rig checklist](../rig-checklist.md).

## Open slices

### P-D: Mono GC measurement (P4)

- **Scope.** Rig capability request plus one measurement run: GC pauses against heap size at 3,000, 10,000 and 20,000 persons. Pool the per-event working copy only if the numbers call for it, as a follow-up.
- **Testing:** rig-needing. **Depends on:** a capability in the rig repository.
- **Owns:** the measurement command in `Runtime/MovementDiagnostics.cs` if one is needed; nothing else here.

### GR-5: population and visitor table (G1, G8)

- **Scope.** Residents in homes the player cannot rent, each a named person with a home and a place to inspect, housed by the second capacity rule in the [J0 plan](j0-living-city-and-sales.md#homes-without-interior-area). Visitor constants in `Core/PopulationCensus.cs` become a tuning table: commuters and day visitors by kind and weekday, and named visitor groups with a share and a leaning toward business types. A group changes who comes, never what a shop is paid.
- **Schema:** no bump. **Testing:** offline; rig-needing for frame time, save latency and GC at the new size. **Depends on:** P-D, and in-game evidence that the GR-4 whole loop works (earn, plan a non-food visit, travel, buy, pay, reload).
- **Owns:** `Core/PopulationCensus.cs`, `Core/PopulationFill.cs`, `Core/HousingPolicy.cs`, `Core/CityBalance.cs`, footprint and floor reads in `Runtime/GamePlaces.cs`.
- **Tests:** every person has a home and a place; a group never changes a shop's price or payment; the census is reproducible from the same inputs.
- **Size.** Not decided. It comes from P-A save latency (target under 40 ms p95 on the main thread at 3,000 persons, not yet measured in game), P-D and the whole-loop run. No revenue fallback is chosen here (Q1).

### E6a: Street Food vendor API

In the Street Food repository. The PC side is implemented. See the [E6 plan](e6-carts-food-trucks.md).

### Proof and release

| Item | Status | Needs |
| --- | --- | --- |
| Batched rig run of merged checks | partial | Current head; the GR, #239 and E6 rows in the [rig checklist](../rig-checklist.md). |
| F4f gate run record | planned, not launched | Sealed FT-F4 and FT-F9 fixtures and the Q6 caps ([F4 plan](f4-perf-gate.md)). |
| Recorded rig walk-through | planned | The same street at morning, midday and night. |
| Final performance gate | planned | Final development head; save latency in its own requested run. |
| Hands-on session | planned | Peter, last. |
| Single release | planned | Everything above. |

## File conflicts

- `Runtime/GamePlaces.cs` and `Core/CityBalance.cs`: GR-5 only.
- `Runtime/MovementDiagnostics.cs`: P-D only.

## Reserved questions for Peter

Each has a default the code uses until Peter answers.

- **Q1. If visitors are not enough to keep your shops' income at the game's normal level, what should give?** Options: lower income, one visit counts as several customers, or higher spend per visit. Default: one visit is one customer, capped at service capacity, until GR-5 and a measured native count bring numbers. The options are in the [J0 plan](j0-living-city-and-sales.md#revenue-scale-and-q1).
- **Q2. Should players see and change the visitor table?** Default: hidden balance settings only.
- **Q3. How much of the traffic should be cars just passing through?** Default: 10 percent, as implemented in GR-8.
- **Q4. Once one sale path works for every shop, should the mod take over customers at every player business automatically?** Default: yes, after the generic sale path is verified by rig at the current head. The AGENTS.md ownership rule changes in that PR, not before.
- **Q5. The old interior-view experiment (F0) is off by default. Delete it?** Default: keep it off.
- **Q6. Performance caps for present, foreground and priority work (F4f).** Reserved; F4f stays unlaunched until answered.

`FoodChoicePriceWeight` (1.2), `FoodChoiceHouseholdWeight`, the F6h criterion and the F6i and F6j rules are not changed by any slice here.
