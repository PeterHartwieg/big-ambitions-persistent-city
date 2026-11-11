# Glossary

Terms used in plans and code, as of 11 November 2026. Each entry names where the term is defined. The [code map](map.md) lists the files.

## People

- **Person.** Anyone the simulation tracks with a stable ID: a resident or a visitor (`PersonKind` in [Core/PersonIdentity.cs](../Core/PersonIdentity.cs)). The person table is [Core/PersonTable.cs](../Core/PersonTable.cs).
- **Citizen.** The routine record of one person with a home or portal, routine, journeys and possibly a car: `CitizenState` in [Core/Model.cs](../Core/Model.cs). Visitor routines are `VisitorRoutine : CitizenState` ([Core/VisitorSchedules.cs](../Core/VisitorSchedules.cs)).
- **Resident.** A person who lives in the city. IDs are `c<n>` for an RLO source resident and `pc:<n>` for a mod-made one (`PersonIds` in [Core/PersonIdentity.cs](../Core/PersonIdentity.cs)). Residents never leave the city.
- **Visitor.** A person who lives outside the city, ID `pv:<n>`: a commuter with a city workplace or a day visitor. A visitor arrives through a portal, has no home, car or optional stops, and gets a body like a resident (`VisitorPerson` in [Core/PersonTable.cs](../Core/PersonTable.cs), `VisitorsReceiveBodies` in [Core/PhysicalJourneys.cs](../Core/PhysicalJourneys.cs)).
- **Portal (station table).** A subway station a visitor arrives and leaves through; the city saves at most 256 (`Portals` in [Core/Model.cs](../Core/Model.cs)).
- **Population fill.** The day-boundary step that creates visitors toward the census targets, in bounded units ([Core/PopulationFill.cs](../Core/PopulationFill.cs), [Core/PopulationCensus.cs](../Core/PopulationCensus.cs)).
- **Consumer (meal roster record).** The food-buying state of one person: meal state, wallet, memory and receipts, `ResidentMeal` in [Core/FoodCommerce.cs](../Core/FoodCommerce.cs). Exactly one per person; `AnchorId` equals the person ID and `Slot` is 0.
- **Factor K.** Historical: the old consumers-per-citizen representation factor. The saved factor is always 1. `CityBalance.ConsumerRepresentationFactor` (2) only decides how many commuter visitors a fresh city starts with (K − 1 per resident; [Core/FoodRepresentation.cs](../Core/FoodRepresentation.cs)). Represented consumers (`consumer:<anchor>:<k>`) no longer exist, and the reader refuses such IDs.
- **Household.** All residents who share one home. The ID `h:<HomeId>` is derived, never stored, and holds no money (`HouseholdIds` in [Core/PersonIdentity.cs](../Core/PersonIdentity.cs)).
- **Player hold.** The one way the visible world delays the simulation: the player's avatar or vehicle blocking a person, at most one game minute per journey ([Core/PlayerHolds.cs](../Core/PlayerHolds.cs)).
- **Follow and correction.** Bodies follow the simulation: a walker follows its journey's logical pose and is corrected when it falls too far behind; a car follows the traffic kernel's coordinate ([Core/PhysicalJourneys.cs](../Core/PhysicalJourneys.cs), `Runtime/PhysicalPopulation.Following.cs`).

## Food and money

- **Scope.** One shop or vehicle and product set the mod serves: `FoodScope` in [Core/FoodCommerce.cs](../Core/FoodCommerce.cs) (place, products, business ID, channel Shop or Vehicle, next service time).
- **Owned food scope.** A scope for a player business the mod currently owns. Ownership is a `FoodAuthority` lifetime with `FromHour`/`UntilHour` that changes only at native accounting hours ([Core/FoodAuthority.cs](../Core/FoodAuthority.cs)). Without an owned scope, the population has no economic effect.
- **Receipt.** A native-save marker that makes one purchase idempotent: `persistentcity:food-sale-v1:<intent>` with status Missing, Pending, Paid or Conflict ([Persistence/FoodSaleReceipts.cs](../Persistence/FoodSaleReceipts.cs)). Missing never proves a purchase failed.
- **Held image / held inputs.** Values captured when a food decision starts and saved with it, so a reload or a settings change finishes the same decision: the tuning image (`FoodDecisionTuning`, [Core/FoodDecisionTuning.cs](../Core/FoodDecisionTuning.cs)), the awareness image (`HeldImage` in [Core/FoodAwareness.cs](../Core/FoodAwareness.cs)) and the held product names.
- **Watermark.** The last memory experience applied for a consumer (day, meal, context, stage): `ExperienceWatermark` in [Core/ShopMemory.cs](../Core/ShopMemory.cs). It stops one visit from being recorded twice across reloads.
- **Employment accrual.** A worker's attended minutes and unposted wage for the current shift week ([Core/EmploymentWages.cs](../Core/EmploymentWages.cs)).
- **Visitor endowment.** A visitor's wallet: a fixed amount with no city housing, essentials or daily allowance ([Core/HouseholdFinance.cs](../Core/HouseholdFinance.cs)).
- **Ordinary exchange.** A non-food retail or leisure visit paid at a modeled outside price ([Core/OrdinaryExchanges.cs](../Core/OrdinaryExchanges.cs)).
- **Change journal.** Observed price, stock and opening-hour changes per own shop, shown on the panel ([Core/FoodChangeJournal.cs](../Core/FoodChangeJournal.cs)).
- **Economic ledger.** Fourteen days of per-shop and outside spending, in exact money ([Core/EconomicLedger.cs](../Core/EconomicLedger.cs)).
- **Vendor table / vendor place.** Street Food vehicles the city has served, with live, retired and tombstoned rows; a vendor place ID is `vendor:…` ([Core/VendorState.cs](../Core/VendorState.cs)).
- **Research order.** A paid market-research report `pcr-<n>` in the research ledger ([Core/ResearchLedger.cs](../Core/ResearchLedger.cs), [Core/ResearchProducts.cs](../Core/ResearchProducts.cs)).

## Saves

- **Schema.** `CityState.CurrentSchema` in [Core/Model.cs](../Core/Model.cs), held at 1 until release and never bumped; the format changes in place ([decision](decisions/2026-11-09-schema-one.md)). Historical development builds wrote 2 to 36. Reading reports Missing, Loaded, Invalid or DevelopmentSchema (`SaveReadStatus`).
- **Compact wire (`pcc-c`).** The save string: a columnar JSON envelope over the body tree, with shared string and route tables ([Persistence/CompactWire34.cs](../Persistence/CompactWire34.cs)). See [save format](save-format.md).
- **Epoch.** Two meanings. The *accounting epoch* is one native game minute; every simulation event must finish before native time closes it ([Core/AccountingClock.cs](../Core/AccountingClock.cs)). The *save epoch* is one save described by the side key `persistentcity:save-epoch_v1`: sequence, schema, population key, minute and the payload's length, or a `failed:N` marker ([Persistence/SaveEpoch.cs](../Persistence/SaveEpoch.cs)).

## Architecture

- **Capability.** A named native dependency with a state (not checked, active, off, absent) and a stated fallback ([Core/NativeCapabilities.cs](../Core/NativeCapabilities.cs), [Core/CapabilitySnapshot.cs](../Core/CapabilitySnapshot.cs)); `pc capability` prints them.
- **City view.** The read-only interface observers (panel, inspector, diagnostics, bodies) read the city through: `ICityView` in [Core/CityView.cs](../Core/CityView.cs).
- **Traffic kernel.** The finite road-section queue model that owns every car's progress ([Core/TrafficKernel.cs](../Core/TrafficKernel.cs)).

## Testing and process

- **Slice.** One plan row delivered as one PR, for example F6b. Its evidence, perf numbers and in-game checks are in the PR description ([lean docs](decisions/2026-11-10-lean-docs.md)).
- **Sync run.** A follow-up run that merges the moved base branch into a slice branch ("base sync").
- **Rig.** The owned test machines (Windows PC and Mac) driven by the headless agent rig (`PeterHartwieg/big-ambitions-agent-rig`). Implementers do not use it.
- **FT city.** A disposable test city created and driven by the rig's FT commands (`state`, `save`, `load`, `time`, `shoot`). Never Peter's ordinary save.
- **Batched rig.** One combined rig session against the development head that runs the in-game checks of every merged slice. It does not gate merges ([delivery rules](decisions/2026-11-06-delivery-rules.md)). The current list is the [rig checklist](rig-checklist.md).
- **Test kit.** The shared test builders and doubles in `tests/Kit`: `TestCity`, `NoRoutes`, `FixedWalk`, and the larger fixtures. Wall-clock limits and measurements run only with `PC_PERF_ASSERTS=1` (`tests/PerfGate.cs`).
- **Mod-work vs whole-frame.** Mod-work is time spent in Persistent City's own frame work; the gate is p95 < 5 ms with every spike >= 30 ms flagged. Whole-frame is the full game frame, reported separately because native stalls also land there ([roadmap, performance gate](roadmap.md#performance-gate)).
