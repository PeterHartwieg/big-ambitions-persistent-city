# Glossary

Terms used in plans, checks and code. Each entry names where the term is defined.

## People

- **Person.** Anyone the simulation tracks with a stable ID. Today that is a citizen and the consumers anchored to it. F5 plans one `Person` record with kind Resident or Visitor ([F5 plan, section 3](plans/f5-population.md); `PersonKind` in [Core/PersonIdentity.cs](../Core/PersonIdentity.cs)).
- **Citizen.** One simulated person with a home, routine, journeys and possibly a car: `CitizenState` in [Core/Model.cs](../Core/Model.cs). Citizens are the only people who get physical bodies.
- **Resident.** A citizen who lives in the city. IDs are `c<n>` for an RLO source resident and `pc:<n>` for a mod-made one (`PersonIds` in [Core/PersonIdentity.cs](../Core/PersonIdentity.cs)). Residents never leave the city.
- **Consumer.** One food-buying unit with its own meal state, budget, memory and receipts: `ResidentMeal` in [Core/FoodCommerce.cs](../Core/FoodCommerce.cs). Wallets are per consumer, not per family ([Core/HouseholdFinance.cs](../Core/HouseholdFinance.cs)).
- **Represented consumer.** An extra consumer attached to a citizen to scale demand without extra bodies. Slot 0 is the citizen itself; slot k > 0 has ID `consumer:<anchor>:<k>` (`FoodRepresentation.ConsumerId` in [Core/FoodRepresentation.cs](../Core/FoodRepresentation.cs)). It shares the anchor's routine stops and never has a body, car or home.
- **Visitor.** A planned person kind for commuters and day visitors who live outside the city, ID `pv:<n>` (`VisitorStay`, `VisitorPattern` in [Core/PersonIdentity.cs](../Core/PersonIdentity.cs)). Indexed since F5f but given no body until F5i.
- **Factor K.** The representation factor: consumers per citizen. Default 2 (`CityBalance.ConsumerRepresentationFactor` in [Core/CityBalance.cs](../Core/CityBalance.cs)), set when a city first gets food state and saved per city, valid 1–3. F5 plans to make it 1.
- **Legacy anchor.** The citizen a represented consumer is attached to (`ResidentMeal.AnchorId`). The F5 plan keeps it as `LegacyAnchorId` for consumers that become persons ([F5 plan, migration](plans/f5-population.md#migration-from-represented-consumers)); under the 6 Nov no-migration rule that migration is being re-scoped.
- **Household.** All residents who share one home. The ID `h:<HomeId>` is derived, never stored, and holds no money (`HouseholdIds` in [Core/PersonIdentity.cs](../Core/PersonIdentity.cs)).

## Food and money

- **Scope.** One shop and product set the mod serves: `FoodScope` in [Core/FoodCommerce.cs](../Core/FoodCommerce.cs) (place, products, business ID, next service time).
- **Owned food scope.** A scope for a player business the mod currently owns. Ownership is a `FoodAuthority` lifetime with `FromHour`/`UntilHour` that changes only at native accounting hours ([Core/FoodAuthority.cs](../Core/FoodAuthority.cs)). Without an owned scope, the population has no economic effect.
- **Receipt.** A native-save marker that makes one purchase idempotent: `persistentcity:food-sale-v2:<intent>` with status Missing, Pending, Paid or Conflict ([Persistence/FoodSaleReceipts.cs](../Persistence/FoodSaleReceipts.cs)). Missing never proves a purchase failed.
- **Held image / held inputs.** Values captured when a food decision starts and saved with it, so a reload or a settings change finishes the same decision: the A3 tuning image (`FoodDecisionTuning`, [Core/FoodDecisionTuning.cs](../Core/FoodDecisionTuning.cs)), the awareness image (`HeldImage` in [Core/FoodAwareness.cs](../Core/FoodAwareness.cs)) and E5d's held product names. New decisions use current values.
- **Watermark.** The last memory experience applied for a consumer (day, meal, context, stage): `ExperienceWatermark` in [Core/ShopMemory.cs](../Core/ShopMemory.cs). It stops one visit from being recorded twice across reloads.

## Saves

- **Writer N / schema N.** The save format version the code writes: `CityState.CurrentSchema` in [Core/Model.cs](../Core/Model.cs), now 25, in the `persistentcity:state_v1` key ([Persistence/SaveCodec.cs](../Persistence/SaveCodec.cs)). "Writer 25 unchanged" means a slice changed no saved bytes. Reading reports Missing, Loaded, Invalid or Future (`SaveReadStatus`).
- **Epoch.** Two meanings. The *accounting epoch* is one native game minute; every simulation event must finish before native time closes it ([Core/AccountingClock.cs](../Core/AccountingClock.cs), [audit F1 plan](plans/f1-accounting-boundary.md)). The *save epoch* is one save described by the side key `persistentcity:save-epoch_v1`: sequence, schema, minute and each payload's length, or a `failed:N` marker ([Persistence/SaveEpoch.cs](../Persistence/SaveEpoch.cs)).

## Testing and process

- **Slice.** One plan row delivered as one PR, for example F6b. Each slice owns `docs/checks/<slice>.md` and `docs/perf/<slice>.md`.
- **Sync run.** A follow-up run that merges the moved base branch into a slice branch and records the resolution in that slice's checks file ("base sync").
- **Rig.** The owned Windows PC test machine driven by the headless agent rig (`PeterHartwieg/big-ambitions-agent-rig`). Implementers do not use it.
- **FT city.** A disposable test city created and driven by the rig's FT commands (`state`, `save`, `load`, `time`, `shoot`). Never Peter's ordinary save.
- **Batched rig.** One combined rig session against the development head that runs the in-game checks of every merged slice (merge rule v2). It does not gate merges ([delivery rules](decisions/2026-11-06-delivery-rules.md)). The current list is [rig-2026-11-06-checklist.md](checks/rig-2026-11-06-checklist.md).
- **Mod-work vs whole-frame.** Mod-work is time spent in Persistent City's own frame work; the gate is p95 < 5 ms with every spike >= 30 ms flagged. Whole-frame is the full game frame, reported separately because native stalls also land there ([roadmap, performance gate](roadmap.md#performance-gate)).
