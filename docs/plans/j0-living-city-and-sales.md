# J0: living city and sales

Joint plan for G and R ([next tracks](next-tracks.md), slice J0). Slice S1 started this file with the "Native customer path" section only; J0 adds the rest. Docs only.

## Native customer path

What the game assembly does with customers at a player business, read from the code. Nothing here was measured in game. Member names are quoted; no decompiled code is kept in the tree.

### Evidence

- Assembly: `BigAmbitions.dll` in the Steam `Big Ambitions_Data/Managed` folder on the PC, Steam build id `25482473`, file date 23 Sep 2026, SHA-256 `9427561b81aaab2ada09c22a3c98dd9a4017a65225b389513885bf336852e911` (the same file the [native money decision](../decisions/2026-10-28-native-money.md) read).
- How read: the ILSpy CLI already installed on the PC (`ilspycmd` 11.2.0) decompiled the assembly into a folder under `%TEMP%`, outside Git, on 11 Nov 2026. The game was not started. Only the code was read; the Unity asset bundles (`StreamingAssets/aa`) were not.

### Generation: `CustomerEntriesHelper`

- `CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness(BuildingRegistration, DayOfWeekOrdered)` builds the day's entries for a player business (`RentedByPlayer`). It runs from `Init` and `UpdateCustomerEntriesForAllPlayerBusinesses`. It returns early unless the business type has the tag `Businesstag.generatesrevenue` and `BuildingRegistration.AreAllRequirementsMet()` is true. For each open hour (`BusinessHelper.IsBusinessOpen`) from the current hour to 23 it calls `CustomerEntriesCalculator.GetCustomersByHour`, subtracts entries already present for that hour, and appends `GetCustomersEntriesForHour(hour, count, NeighborhoodHelper.GetData(..).customerDemandsWeight)`. Entries are stored in the private static dictionary `BusinessCustomerEntries`, keyed by `Address`. Uncompleted entries are dropped when it rebuilds (`GetBusinessCustomerEntries`).
- `CustomerEntriesHelper.GetEntriesByAddress(Address)` returns the stored list. For an address with no list (AI businesses) it fills one through `GenerateAiEntries`.
- `CustomerEntriesCalculatorFactory.GetCustomerEntriesCalculator(BuildingRegistration)` picks the calculator:
  - `CustomerEntriesCalculatorCasino` for `ba:businesstype_casino`.
  - `CustomerEntriesCalculatorRetail` for `ba:businesstype_furniturestore`, `ba:businesstype_appliancestore` and `ba:businesstype_coffeeshop`, and for building type `ba:buildingtype_retail`.
  - `CustomerEntriesCalculatorCinemaTheater` (a subclass of Retail) for building types `ba:buildingtype_cinema` and `ba:buildingtype_theater`.
  - `CustomerEntriesCalculatorOffice` for `ba:buildingtype_office`.
  - The base `CustomerEntriesCalculator` for everything else.
- `CustomerEntry` has `spawnTime` (a `Timestamp`), `completed` and `order` (`Order`). `CustomerEntriesCalculator.SetUpCustomersEntriesListForHour` creates the entries, gives each a spawn minute (`GetCustomerSpawnMinute`), and adds `Order.customerDemandTypes` from `BusinessType.customerDemandSets`. Completed orders already in `BuildingRegistration.unprocessedCompletedOrders` for that hour count against the hour's number.
- The order is chosen at generation time, not at checkout. `CustomerEntriesCalculatorRetail.AddProductsToCustomersEntriesList` loops over `BuildingRegistration.cachedAvailableProducts`, skips entrance fees and items tagged `Itemtag.isticket`, and works out how many units of each product the hour sells: entries × `Item.productSalesRatio` × neighbourhood demand (`ProductMarketHelper.GetNeighborhoodDemand`) × satisfaction (`GetCustomerSatisfactionMultiplier`) × `BusinessType.GetProductImpact` × an amount up to `maxAmountPerProduct`. It then gives those units to random entries in the hour. A service product (`ItemType.ServiceProduct`) goes at most once to each entry. Cinema and theatre put `ba:itemname_cinematicket` or `ba:itemname_theaterticket` first in every order. Casino orders take 1 or 2 random products. Entries with an empty order are removed unless `BusinessType.acceptCustomersWithoutOrderEntries` is set.

### What the native count includes

`CustomerEntriesCalculator.GetCustomersByHour` = `ceil(initial × GetPromotionMultiplier() × GetDayAndHourMultiplier(hour, day))`. The Retail version also caps it at `BuildingRegistration.customerCapacity`, and the Casino version returns `initial` with no multipliers.

| Factor | Where | In the count? |
| --- | --- | --- |
| Building size | `GetInitialCustomers` → `BuildingSizeHelper.GetData(..).GetCustomerCapacity`. Retail saves started before build 2847 use `BusinessHelper.GetMaxHcpsqmForRegistration` × floor area instead | yes |
| Marketing and location | `GetPromotionMultiplier`: `gameVariables.baseCustomerPromotionMultiplier + 0.75 × promotion.total / 100`. `BusinessHelper.UpdatePromotion` sets `promotion.total = min(trafficIndex + marketing × marketingStrength, 100)` (the building's traffic index plus marketing efficiency) | yes |
| Day and hour | `BusinessType.dayFactorMultipliers`, `hourlyFactorMultipliers` | yes |
| Opening hours | `BusinessHelper.IsBusinessOpen(registration, hour)` | yes |
| Price | not in the count. Price acts later, entry by entry, at checkout (below) | no |
| Satisfaction, demand | not in the count. They change how many units go into the orders (Retail calculator) | no (basket only) |

One more hourly cap applies at processing: `RetailBusinessSimulator.GetMaxCustomerCapacityThisHour` (building capacity, lowered by staffed furniture `CustomersLimit`). Entries above the cap are marked `completed` without an order.

### Processing: business simulators

`BusinessSimulatorHelper.RunHourly` gives every open player business whose `BusinessType.simulator` is set to a `DistributedWork` queue (`Work`), which calls `simulator.SetUp(registration, hour)` and then `SimulateCurrentHour()`. It skips the building the player is standing in when `BusinessType.spawnCustomers` is set (unless the time machine is running). Inside that building `IndoorCustomerSpawner` spawns visible `Customer`s from the same `GetEntriesByAddress` list, and the employee and kiosk scripts (`SelfServiceEmployee`, `FullServiceEmployee`, `TicketKioskController`, `ProcessHaircutEntriesAlmostLeaving` and others) complete the orders.

The simulators are `ScriptableObject` assets. Which business type uses which asset is set in the `BusinessType.simulator` field of the asset data, not in code, and **was not determined**: the asset bundles were not read. The "types" column below lists only what the code itself names (class names, business type and item names in code, `CreateAssetMenu` paths). J0 or S2 must confirm the mapping in game, for example by logging `BusinessTypeHelper.GetData(reg).simulator.GetType()` for each player business type.

| Family | Class (`CreateAssetMenu`) | Types the code points to | Per-customer method | Specific behaviour |
| --- | --- | --- | --- | --- |
| Retail base | `RetailBusinessSimulator` (`BusinessSimulator/Retail`) | none directly; base of all below | `ProcessCustomer(CustomerEntry)` → `ProcessEntranceFee`, `ProcessItemInOrder`, `CompleteOrderEntry`, `ProcessPaperBag`, `Order.Complete` | Order goes to `unprocessedCompletedOrders` |
| Self-service shop | `SelfServiceBusinessSimulator` (`Retail/SelfService`) | goods shops with shelves and checkouts (assumed) | `ProcessCustomer` override | Refuses the entry with no shopping containers (`CustomersNeedShoppingContainer`); items with `requiresWeighing` need scales |
| Full-service | `FullServiceBusinessSimulator` (`Retail/FullService`) | counter shops; the mod's food path uses it ([city direction](../decisions/2026-11-10-city-direction.md)) | inherits | Seats and trash bins get dirt |
| Cinema, theatre | `CinemaTheaterBusinessSimulator` (`Retail/CinemaTheater`, subclass of self-service) | `ba:buildingtype_cinema`, `ba:buildingtype_theater` (calculator) | `ProcessCustomer` override | If the ticket price is refused, the rest of the order is refused. A theatre without actors (`BusinessHelper.ShouldWarnTheaterHasNoActors`) refuses every entry. Does not call `ProcessEntranceFee` |
| Nightclub | `NightclubBusinessSimulator` (`Retail/FullServices/Nightclub`) | nightclub | `ProcessCustomer`, `ProcessItemInOrder` overrides | Nobody served without a staffed DJ booth; drops retail entries at random according to DJ skill; entrance fee through the base |
| Hairdresser | `HairdresserBusinessSimulator` (`Retail/FullServices/Hairdresser`) | hairdresser | `ProcessCustomer`, `ProcessItemInOrder` overrides | Service needs a stocked hair-care shelf and a staffed station |
| Gym | `GymBusinessSimulator` (`Retail/Gym`) | gym | `ProcessCustomer`, `ProcessItemInOrder` overrides | Entrance fee first (`ProcessEntranceFee`); workout machines and showers |
| Office | `OfficeBusinessSimulator` | `ba:buildingtype_office` | own | Not customer-facing; out of scope |
| Factory | `FactoryBusinessSimulator` | `ba:businesstype_factory` | own | Not customer-facing; out of scope |
| Casino | none found | `ba:businesstype_casino` (calculator only) | not found | No casino simulator class in `BigAmbitions.dll`. Which processor it uses was not determined |

Entrance-fee venues: any type with `BusinessType.hasEntranceFee`. The fee item comes from `BusinessTypeHelper.GetEntranceFeeNameForBusinessType` (`defaultEntranceFee`, `weekendOnlyEntranceFee`). `RetailBusinessSimulator.ProcessEntranceFee` charges it first. If the fee is refused, the order is completed with no paid entry and only the fee recorded.

### Checkout and booking

- The buyer is not a person. Every `ProcessCustomer` takes a fresh `CitizenHelper.CreateRandomCitizenDataNonAlloc(neighbourhood)`, one shared static `CitizenData` that is overwritten each time.
- Price check per entry: `CompleteOrderEntry` sets `OrderEntry.price = ItemHelper.GetPrice(item, registration)` and `priceAccceptable = CitizenData.IsPriceAcceptable(item, price)`, which is `price <= ItemHelper.CalculateMaxAcceptablePrice(item, neighbourhood, socialClass)` (market reference price × `CitizenHelper.MaxAcceptableRelativePrice`, plus 0.3 under a player monopoly). `paid = priceAccceptable`. Each item is checked on its own; nothing checks the total of the order.
- Stock is taken immediately for paid goods (`ItemInstance.SubtractFromStock`).
- No money moves at checkout. `Order.Complete` only sets score, timestamp, `completed` and cleanliness. Revenue is booked once a day: `GameManager` → `BusinessHelper.RunDaily` → `ProcessDailyOrders` sums the paid entries of `unprocessedCompletedOrders` (`CreateDailyOrderHistory`), books `GameManager.ChangeMoneySafe(totalRevenue, "ba:transaction_revenue")`, and clears the list. `Order.Pay` (which does call `ChangeMoneySafe`) is only for the player's own purchases (`isPlayer`).

### Can one entry be bound to one resident with a ceiling?

From the code, yes, with limits:

- **Binding.** `CustomerEntry` and `Order` have no field for a person, and the citizen is random and thrown away. A binding has to be kept by the mod: own entry objects (the food path already writes its own entry, see the [city direction](../decisions/2026-11-10-city-direction.md)), or a mod table from `CustomerEntry` to resident while the entry lives in `BusinessCustomerEntries`. Ordinary native processing never keeps or reports an identity.
- **Ceiling before the sale commits.** Two boundaries matter. The first stock mutation is `ItemInstance.SubtractFromStock` for a paid good inside `ProcessItemInOrder` (and the paper bag in `ProcessPaperBag`, whose appended `Order.AddPaperBagEntry` sets only `wholesalePrice`, so it adds no customer price). The order-booking boundary comes later: the order joins `unprocessedCompletedOrders` at the end of `ProcessCustomer`, and money moves only at the daily boundary. The incoming basket is incomplete for entrance-fee venues: `CustomerEntriesCalculatorRetail` leaves `defaultEntranceFee`, `weekendOnlyEntranceFee` and ticket items out of generated baskets, and `RetailBusinessSimulator.ProcessEntranceFee` creates a new paid `OrderEntry` for the item named by `BusinessTypeHelper.GetEntranceFeeNameForBusinessType` and appends it to `order.entries` during processing (`IndoorCustomerSpawner.TryToProcessEntranceFeeEntryToOrder` does the same for visible customers). A prefix on `RetailBusinessSimulator.ProcessCustomer` (already a mod hook) can price the basket before any stock moves, but only if it prices `order.entries` plus every checkout-added charge that applies, at least the entrance fee when `BusinessType.hasEntranceFee` is set, with `ItemHelper.GetPrice`. Pricing `order.entries` alone can approve a basket at the resident's ceiling and then book revenue above it. The game checks prices per item, not the total, so the total ceiling must be the mod's check. One catch: subclasses that override `ProcessCustomer` (Cinema/Theatre, Gym) do not call the base method's body, and Nightclub, Hairdresser and Self-service run their own checks before the base. A prefix on the base method does not cover the first two. Each family needs its own hook or a mod-run entry.
- **Not determined from reading:** the asset mapping from business type to simulator, and the casino processor; whether `IndoorCustomerSpawner` and the employee scripts (player inside) can take a pre-bound entry without changes. S2 proves the sale in game.

## Day plans

### Model

- A `DayPlan` belongs to one person and one game day. It holds the day number, the seed it was drawn with, and an ordered list of `PlanItem`s.
- A `PlanItem` has a kind (`Work`, `Meal`, `Need`, `Outing`, `Home`), a venue (a place ID, or none for a need that picks its venue when it starts), a window (earliest start, latest start), a duration, a priority (`Fixed`, `Wanted`, `Optional`) and a state (`Pending`, `Travelling`, `Active`, `Done`, `Missed` with one cause, `Cancelled`).
- Generation runs once at the person's day boundary (04:00 game time, the quietest hour in today's template) and reads only saved state: role, shift type, the workplace's weekly hours, wallet, needs due today, the household outing for today, the place catalogue. Draws come from a seed of person ID and day number, so regenerating from the same inputs gives the same plan. The plan is saved anyway, because wallet and catalogue change during the day.
- Order of generation: work first (from the workplace hours and a shift type `Early`, `Day`, `Late` or `Night`; days off from a per-person weekday mask drawn once and saved with the person), then meals as items in the gaps (replacing the `MealRhythm` interrupt), then needs due today by urgency, then the household outing if one is set, then home. Items that do not fit are left out, not squeezed.
- Visitors get the same plan, with `Work` only for commuters and no `Outing`. Their day starts and ends at a subway portal.

### Executor

- One `DayPlanExecutor` replaces the separate scheduling state in `CitySimulator` (commute preparation, meal outings, waits for opening) and in `OptionalStopScheduling`. It is event-driven: each person has one next-wake time in the existing event queue, so the executor does no per-frame walk. The `RefreshOptionalStops` full walk (a P-B hotspot) goes with it.
- Rules, in order: a `Fixed` item that is due starts. A `Wanted` or `Optional` item starts if its window is open and the venue is open on arrival (travel time from the kernel's estimate); otherwise it waits up to its latest start and then becomes `Missed(Closed | Late | NoRoute)`. A late arrival shortens the item, never the next `Fixed` one. A missed `Need` item goes back to the need model as deferred. A purchase refused by the contract marks the item `Missed(Refused, cause)`.
- Reload: the executor reads the saved plan and the in-flight journey and continues. No item is redrawn. A journey interrupted by a save resumes from its saved leg, as today.
- Cadence: results depend only on event times, so two update cadences give the same item states (GR-2 test).

## Need model

- A need is a category from the loaded catalogue (business type tags and item types) with three numbers: interval in days, spread, and a spending band. A category nobody describes gets the neutral default: interval 7 days, spread 3 days, band from the person's wallet tier. Modded business types get the default unless a table row names them.
- Each person keeps, per need: next-due day and last venue. A satisfied need sets next-due to today plus interval plus or minus a seeded spread. A deferred need (missed, refused, wallet short) comes due again the next day with higher urgency, up to three times, then resets to a full interval. Venue memory reuses the shape of the food memory store.
- Impulse: while passing an open business on a journey leg, one seeded draw per person, venue and day at a small base rate, raised by how close the related need is to due. At most one impulse per person per day. An impulse becomes an inserted `Optional` plan item if it fits before the next `Fixed` one.
- Native counts calibrate and do not drive. The native hourly count already contains building size, promotion (traffic index and marketing) and the day and hour factors (S1). Venue choice uses distance, memory and price level, not marketing, so marketing is not applied twice.
- Household essentials in `HouseholdFinance` are re-cut with the wallet move (GR-1): a category that becomes a need stops being deducted as an essential, so it is not paid twice.

## Purchase contract

`PurchaseRequest(resident, venue, intention, ceiling)` in; `PurchaseResult` out, either `Bought(lines, total)` or `Refused(cause)`. The intention is a need category or a food choice. The ceiling is the smaller of the wallet balance and the need's spending band.

Common steps for every family:

1. Take an order. First choice: claim one uncompleted native `CustomerEntry` of the current hour from `CustomerEntriesHelper.GetEntriesByAddress(address)` whose order is not empty. The game then keeps order choice, stock and booking. A mod table maps the entry to the resident while it lives. If the hour has none left, the result is `Refused(NoCustomerSlot)`: the native count is the venue's cap. Food keeps writing its own one-product entry, as today.
2. Price the whole basket before any stock moves: `order.entries` priced with `ItemHelper.GetPrice`, plus the entrance fee from `BusinessTypeHelper.GetEntranceFeeNameForBusinessType` when `BusinessType.hasEntranceFee` is set, plus the ticket item for cinema and theatre. If the total is over the ceiling, drop lines from the end until it fits; if nothing fits, `Refused(Ceiling)`. The game's own per-item check (`CitizenData.IsPriceAcceptable`) still runs afterwards.
3. Process through the family's own `ProcessCustomer`, with the mod's citizen data in place of `CitizenHelper.CreateRandomCitizenDataNonAlloc`.
4. Read the processed order (paid entries and prices), debit the wallet by the paid total, record the receipt. The player's money arrives at the native daily boundary (`BusinessHelper.ProcessDailyOrders`), as for food.

Adapters per processing family (S1 table):

| Family | Hook point | Adapter work |
| --- | --- | --- |
| `RetailBusinessSimulator` base, `FullServiceBusinessSimulator` | prefix on `RetailBusinessSimulator.ProcessCustomer` | common steps only; food already uses this |
| `SelfServiceBusinessSimulator` | its `ProcessCustomer` override | the `CustomersNeedShoppingContainer` and missing-scales refusals map to `Refused(VenueNotReady)` |
| `CinemaTheaterBusinessSimulator` | its own `ProcessCustomer` override (does not reach the base prefix) | ticket priced in step 2; a refused ticket refuses the rest; a theatre without actors maps to `Refused(VenueNotReady)` |
| `GymBusinessSimulator` | its own `ProcessCustomer` override | entrance fee priced in step 2 |
| `NightclubBusinessSimulator` | its override, before the base | no staffed DJ booth maps to `Refused(VenueNotReady)`; the DJ-skill random drop maps to `Refused(Dropped)` |
| `HairdresserBusinessSimulator` | its override, before the base | missing hair-care shelf or station maps to `Refused(VenueNotReady)` |
| Casino | not found | unsupported until S2 finds the processor; stays native-owned |
| `OfficeBusinessSimulator`, `FactoryBusinessSimulator` | none | no customers; workforce only (GR-7) |

A business type whose `BusinessType.simulator` asset is none of these stays native-owned, the fallback for modded shops with their own code. S2 confirms the type-to-simulator mapping in game; GR-3 builds the adapter table from that log, never from shop names.

While the player stands inside the venue (the `IndoorCustomerSpawner` path) the mod claims no entries for that building, and the resident's item waits or misses with `Refused(PlayerInside)`. Whether visible customers can carry a bound entry stays with the indoor-customers row of the roadmap.

## Households

- A household is the residents of one home. Visitors have none.
- At most one shared item per household per day: an `Outing` to one venue, drawn at plan generation for an evening or a day off when at least two members are free in the window. Each member's plan carries the same item and window.
- Cancelling rule: any member not arrived by the latest start is `Missed(Late)`. If fewer than two members have arrived by then, the arrived member's item becomes an ordinary visit and the outing is `Cancelled` for the rest. Each member buys with their own contract call and wallet.

## Homes without interior area

`HousingPolicy.Capacity` stays for rentable homes. Residential buildings the player cannot rent get a second rule: capacity = floor(footprint area × floors × 0.6 usable share ÷ 30 m² per person), with the footprint from the building's bounds and the floor count from its building data, both read in `GamePlaces`. If either number is missing or not finite, the building houses nobody and is counted in a diagnostic; nothing is guessed. The 90% fill and `MaximumResidents` still apply to the city total. GR-5 sets the population size from P-A, P-D and GR-4 evidence; the factors 0.6 and 30 m² are tuning values in `CityBalance`.

## Through-traffic share

Through cars are a share of all cars on the road, setting `ThroughTrafficShare`, default 0.10 (the Q3 default), range 0 to 0.5. Entries per map edge follow the hour's resident car count × share ÷ (1 − share), so the share holds as resident traffic rises and falls. At 0 the kernel is identical to today. They are not saved.

## Revenue-scale gap

From numbers already recorded in the repo; nothing new was run.

| Quantity | Value | Source |
| --- | --- | --- |
| Population of the reference city | 957 residents; 957 visitors on day 1, 1,914 by day 3 | [rig 11 Nov batch](../checks/rig-2026-11-11-batch-results.md) |
| Places | 1,083 | [F8h check 1 log](../checks/f8h-check1-modpack-owners-mac.json) |
| Resident visits | about 3 per shop per day | [9 Nov review](../reviews/2026-11-09-city-review.md) |
| Offline own-shop ceiling | 60 meals per hour, capacity-bound profile | [F9h record](../checks/f9h.md) |
| Price response at weight 1.2 | +50% menu: meals −1.7%, revenue +29.7% (offline) | [F9h record](../checks/f9h.md) |
| Native customers at a player shop | not measured: every rig fixture so far recorded no orders and revenue 0 | [rig 9 Nov](../checks/rig-2026-11-09-results.md), [rig 11 Nov e](../checks/rig-2026-11-11e-mac-results.md) |

The mod's side is known: about 3 visits per shop per day. The native side is not measured. S1 shows the native count is `ceil(initial × promotion × day and hour factor)` for every open hour, capped at `customerCapacity` for retail. One native customer per open hour at a shop open 9 hours is already 9 a day, three times today's resident visits, and the multipliers raise it. So the gap is at least 3× and plausibly 10× or more. S2 gives the exact number: log `GetCustomersByHour` and `customerCapacity` for the FT shop for one day.

Growing the population: visits scale about linearly with people. 8,000 to 15,000 named people (the review's estimate) is 4× to 8× today's 1,914 people present, which closes a 3× gap and leaves a 10× gap at 1.3× to 2.5×. Visitor groups (G8) move visits between shop types and do not raise the total.

## Reserved question Q1

**If visitors are not enough to keep your shops' income at the game's normal level, what should give?** Default until answered: none. The plan uses the native count as each venue's cap and books one visit as one customer.

| Option | Pros | Cons |
| --- | --- | --- |
| A. Lower income: fewer customers than native | Truthful: every sale is a person you can click. No new code | Rent and wages are tuned to native volume, so shops may not cover rent. Changes the game's difficulty a lot |
| B. One visit counts as several customers (per-venue multiplier = native count ÷ resident visits) | Keeps native income. The multiplier comes from the native count, so marketing and location still matter. Small code | The panel shows more customers than walked in, and the inspector must say so. Stock falls faster than the street suggests |
| C. Higher spend per visit (bigger baskets) | Every unit is bought by a named person | Baskets the native order would not choose; the resident's ceiling caps it, so poorer districts still fall short. Shifts the price response F6h measured |

Recommendation: grow the population first (GR-5), measure the remaining gap with S2's count, then use B with a capped multiplier (for example at most 4) shown in the panel. Peter decides.
