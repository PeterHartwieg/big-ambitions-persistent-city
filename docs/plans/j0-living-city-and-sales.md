# J0: living city and sales

Native findings and the open G and R choices. GR-1 to GR-4 and GR-6 to GR-9 are implemented; their design text is in the [13 November snapshot](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/j0-living-city-and-sales.md). Open slices are in [next tracks](next-tracks.md). Docs only.

## Native customer path, read 11 November 2026

Read from code only; nothing here was measured in game. Assembly: `BigAmbitions.dll`, Steam build `25482473`, file date 23 Sep 2026, SHA-256 `9427561b81aaab2ada09c22a3c98dd9a4017a65225b389513885bf336852e911` (the file the [native money decision](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/decisions/2026-10-28-native-money.md) read). Decompiled with `ilspycmd` 11.2.0 outside Git; the Unity asset bundles were not read.

### Generation, processing and booking are three steps

- **Generation.** `CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness` builds each open hour's entries for a business tagged `Businesstag.generatesrevenue` whose requirements are met, and stores them in the private static `BusinessCustomerEntries` keyed by `Address`. The calculator comes from `CustomerEntriesCalculatorFactory`: Casino, Retail (furniture, appliance, coffee shop and building type retail), CinemaTheater, Office, or the base. The order is chosen at generation, not at checkout.
- **Processing.** `BusinessSimulatorHelper.RunHourly` runs each open player business's `BusinessType.simulator` through `SetUp` and `SimulateCurrentHour`. It skips the building the player stands in when `spawnCustomers` is set; there `IndoorCustomerSpawner` and the employee scripts complete the same entries.
- **Booking.** No money moves at checkout. `BusinessHelper.RunDaily` → `ProcessDailyOrders` sums the paid entries of `unprocessedCompletedOrders` and books one `ChangeMoneySafe(totalRevenue, "ba:transaction_revenue")` per day. Stock is taken at processing (`ItemInstance.SubtractFromStock`).

### What the native count includes

`GetCustomersByHour` = `ceil(initial × GetPromotionMultiplier() × GetDayAndHourMultiplier(hour, day))`. Retail caps it at `customerCapacity`; Casino returns `initial`.

| Factor | In the count? |
| --- | --- |
| Building size (`BuildingSizeHelper` capacity; pre-2847 retail saves use floor area × `GetMaxHcpsqmForRegistration`) | yes |
| Marketing and location: `baseCustomerPromotionMultiplier + 0.75 × promotion.total / 100`, `promotion.total = min(trafficIndex + marketing × marketingStrength, 100)` | yes |
| Day and hour factors, opening hours | yes |
| Price | no: checked per item at checkout (`IsPriceAcceptable`), never on the order total |
| Satisfaction and neighbourhood demand | basket size only |

A second hourly cap applies at processing: `RetailBusinessSimulator.GetMaxCustomerCapacityThisHour`, lowered by staffed furniture `CustomersLimit`.

### Charges added during processing

Generated baskets leave out `defaultEntranceFee`, `weekendOnlyEntranceFee` and items tagged `Itemtag.isticket`. `RetailBusinessSimulator.ProcessEntranceFee` appends a paid fee entry during processing when `BusinessType.hasEntranceFee` is set. Cinema and theatre put the ticket first in the generated order instead, and do not call `ProcessEntranceFee`. A ceiling check must therefore price `order.entries` plus every charge the family appends, before the first stock mutation. Pricing the generated basket alone can book revenue above the ceiling.

### Families override the base

Cinema/Theatre and Gym override `ProcessCustomer` without reaching the base body; Nightclub, Hairdresser and Self-service run their own refusals first. A hook on the base method does not cover them. Casino has no simulator class in the assembly.

### Not established

- Which business type uses which simulator. It is set in asset data (`BusinessType.simulator`), not in code. The purchase contract builds its adapter table from that runtime field, never from shop names.
- The casino processor.
- Whether the indoor spawner and employee scripts accept a pre-bound entry.
- Claiming one native entry for a resident does not by itself prove that the mod-written basket for an empty hour is safe for every family. The [12b report](../checks/rig-2026-11-12b-mac-results.md) proves food, one goods and one service fixture at `060a7947`; it does not cover every fee, ticket or service family.

## Homes without interior area

Proposal for GR-5. `HousingPolicy.Capacity` stays for rentable homes. Residential buildings the player cannot rent get capacity = floor(footprint area × floors × 0.6 usable share ÷ 30 m² per person), with footprint and floor count read in `GamePlaces`. A building with a missing or non-finite number houses nobody and is counted in a diagnostic. The 90% fill and `MaximumResidents` still cap the city. 0.6 and 30 m² are tuning values in `CityBalance`.

## Revenue scale and Q1

The mod's side, from the 9 November [review](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/reviews/2026-11-09-city-review.md): about 3 resident visits per shop per day with 2,871 people (957 residents, 1,914 commuters). The native side has no recorded daily count. If a shop open 9 hours got one native customer per hour, native visits would be 3× the resident visits; with multipliers above 1 they could be 10× or more. Visits scale about linearly with people, so 8,000 to 15,000 named people closes a 3× gap and leaves 3.6× to 1.9× of a 10× gap. Visitor groups move visits between shop types and do not raise the total. To measure: log `GetCustomersByHour`, `customerCapacity` and booked revenue for one shop for one day.

**Q1. If visitors are not enough to keep your shops' income at the game's normal level, what should give?** Default until Peter answers: one visit is one customer, capped at service capacity.

| Option | For | Against |
| --- | --- | --- |
| A. Lower income | Every sale is a person you can click | Rent and wages are tuned to native volume; changes difficulty a lot |
| B. One visit counts as several customers (native count ÷ resident visits) | Keeps native income; marketing and location still matter | Panel shows more customers than walked in; stock falls faster than the street suggests |
| C. Bigger baskets | Every unit is bought by a named person | Baskets the native order would not choose; poorer districts still fall short; shifts the F6h price response |

The planners proposed: grow the population first (GR-5), measure the remaining gap, then B with a capped multiplier (at most 4) shown in the panel. This is a recommendation, not an approval.
