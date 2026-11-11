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
