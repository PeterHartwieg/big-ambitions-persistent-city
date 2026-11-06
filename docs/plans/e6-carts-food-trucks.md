# E6: cart and food-truck sales

Plan, 6 November 2026. Docs only; this change implements nothing. Persistent City is not live or released.

Base: `origin/codex/persistent-city` at `b798b7e` (after `e9995e5`), writer **25**. Every PC file and line cited below was re-read at this base. Street Food evidence is read-only from `big-ambitions-food-trucks` `origin/main` at [`242881d`](https://github.com/PeterHartwieg/big-ambitions-food-trucks/tree/242881d616d8faebb4dc41b2a996c6c96122bcdc) (version 0.10.11). Rig evidence is read-only from `big-ambitions-agent-rig` at `70021ed`.

"Street Food" and "Food Trucks" are one mod: assembly `FoodTrucks`, repository `big-ambitions-food-trucks`, Workshop title "HART. Street Food" (`big-ambitions-modpack/modpack.json:51-52`). This plan calls it Street Food.

Why E6 is needed: Peter's E5 Q6 decision ([E5 plan section 9](e5-dynamic-products.md#9-product-decisions)) put cart and truck sales next after E5 if no building-placeable holder exists for dumplings and chopped cheese. The E5 plan already records that street equipment is not placeable building furniture ([E5 section 3, Street Food evidence](e5-dynamic-products.md#street-food-evidence-and-the-mod-metadata-api)), and E5b check 1 treats "no holder" as a completed negative result that starts this scope ([rig checklist, E5b check 1](../checks/rig-2026-11-06-checklist.md#e5b-complete-menu-and-refusalrecovery)). Peter put E6 in the first release on 6 November because Persistent City ships in his modpack together with Street Food.

## 1. Goal and acceptance

The player gets Persistent City residents as customers at their own carts and food trucks. A resident who is hungry near an open player vehicle can choose it the same way they choose a player shop: by price, distance, queue, taste and memory, competing with player shops and rival shops. They walk to the vehicle, wait their turn, pay from their own wallet and remember how it went. Street Food stays in charge of the vehicle, its stock, its prices and its money: each resident purchase goes through Street Food's own sale path and is paid to the player in Street Food's midnight takings, exactly like Street Food's own customers.

Acceptance:

1. An open, Persistent City-owned vehicle appears as a choice for residents near it, scored with the same choice, awareness and memory code as a player shop. Rival shops and their F6b service limits are unchanged.
2. Every resident purchase at a vehicle is made by Street Food's sale path through its public API. PC never edits Street Food state, cargo, ledgers or money by any other route. PC never credits the player.
3. Money is exact: the resident pays the vehicle's price in whole cents, Street Food books the same cents on the sale day, and the player receives them in Street Food's midnight payment. A purchase whose outcome cannot be confirmed is recorded as uncertain and never retried.
4. A vehicle is owned whole or not at all. PC owns an opening only when every menu item is admissible; Street Food's own customers are switched off for exactly the openings PC owns, so demand is never counted twice.
5. Ownership starts only at the start of a vehicle opening and ends only when PC's own work for that vehicle has drained. If PC stops renewing its lease (PC missing, city refused, PC crashed), Street Food takes the vehicle back by itself.
6. A vehicle that moves to a new stand cannot serve a resident who chose its old stand. That resident records a closed visit, keeps their money and returns.
7. Original-three products (hot dog, burger, croissant) work first. Dumplings, chopped cheese and other modded or vanilla products work once E5b's held-policy execution is merged, using the same name identity, food-by-default metadata and seeded per-resident taste as shops.
8. Without Street Food, or with a Street Food build without the API, PC runs as today and reports why vehicles are not served. RLO is unchanged.
9. Mod-work p95 stays below 5 ms with vehicles active; every mod-work spike of 30 ms or more is flagged.
10. The new schema refuses older development cities with the "start a new city" status. No migration.

## 2. Current state at the base

### What exists in Persistent City

- **Shop authority.** `NativeFoodMarket` (`Runtime/NativeFoodMarket.cs:22`) implements `IFoodMarket`, `IFoodSaleEvidence`, `IFoodBusinessAuthority`, `ILocalFoodMarket`, `IFoodCandidateMarket` and `IFoodFailureMarket`. The interfaces are in `Core/FoodCommerce.cs:140-171`. `SupportedBusiness` (`:178-192`) accepts only rented, established full-service shops whose whole menu is `FoodProducts.Known`. A vehicle is not a `BuildingRegistration`, so none of this applies to carts.
- **Checkout.** `Buy`/`BuyLive` (`NativeFoodMarket.cs:353-464`) checks save, accounting hour, ownership, approved price and live supply, writes a pending receipt, runs one native sale, then checks before/after stock and orders. Any mismatch throws "outcome is uncertain; do not retry". This pattern is reused for vehicles; the native call is replaced by Street Food's API call.
- **Receipts.** `FoodSaleReceipts` (`Persistence/FoodSaleReceipts.cs:41-60`) keys a receipt by meal intent and stores price, business and product. It only requires `TargetKind == Player`, so a vehicle business can use it unchanged.
- **Business lifetime.** `FoodAuthority` (`Core/FoodAuthority.cs:7-55`) holds desired/requested/from/until hours and a generation, and `Boundary` only transfers after old work has drained and no live native customers remain. `NativeFoodOwnership` (`Runtime/NativeFoodOwnership.cs:31`) applies it to shops through native hooks. `FoodAuthorityStore` (`Persistence/FoodAuthorityStore.cs:53-55`) only accepts `address:` places and `business:` signatures.
- **Places.** Food choice, travel and stops resolve a scope's `PlaceId` through `CitySimulator.FindPlace` (`Core/CitySimulator.cs:56`), built once from `CityState.Places` (`:150-154`). Scopes are found by place (`state.Shops.Single(s => s.PlaceId == ...)` in `Core/FoodStops.cs:67`). Meal trips use captured walking paths (`Core/FoodWeightedCommerce.cs:370`). There is no way to add or move a place during play.
- **Choice and awareness.** `FoodWeightedCommerce` builds a bounded awareness catalogue with a staged builder (`Core/FoodWeightedCommerce.cs:16-54`) and refuses any offer that is not `FoodProducts.Known` (`:190`). `NameWire25` refuses held offers for products that are not `Known` (`Persistence/NameWire25.cs:76`). E5b's checks list held-policy execution as not done ([E5b checks](../checks/e5b-complete-menu.md#refusal-boundary-and-remaining-implementation)).
- **Name identity and metadata.** E5d schema-25 name references, E5c food-by-default/seeded taste resolver and E5e metadata API are implemented offline ([E5d](../checks/e5d-name-identity-save.md), [E5e](../checks/e5e-metadata-api.md)).
- **Rivals.** `IRivalFoodMarket` (`Core/RivalFood.cs:39`) is read-only and never supplies sale authority. F6b estimated service limits are implemented ([F6b checks](../checks/f6b.md)).
- **Third-party contract pattern.** `RloBridge` (`Compatibility/RloBridge.cs:16`) binds RLO's public static `CityApi` by reflection, checks `ContractVersion` and every promised member, and refuses on mismatch ([RLO bridge](../rlo-bridge.md)). Compatibility is netstandard2.1 and references only Core.
- **Schema.** `CityState.CurrentSchema = 25` (`Core/Model.cs:256`). F5c-lite is running and takes schema 26 ([roadmap](../roadmap.md)).

### What exists in Street Food

- Street Food is the sale authority for its vehicles. `CartSelling.SellItem` (`unity/FoodTrucks/Scripts/CartSelling.cs:1317`) prices the item in whole cents (`CartPricing.PriceCents`), checks its ledgers can record the sale, finds a paid, unsealed cargo stack, records the sale in `StreetSales`, `CrewTakings` and `SoldCounts`, then takes one unit. `ServeCustomer` (`:1097`) sells one customer's order.
- Money reaches the player only at midnight: `SettleClosingDay` (`:1234`) runs in the `BusinessHelper.RunDaily` prefix and pays each vehicle's day through `Payments.PayIn`, which confirms the transaction was booked (`Payments.cs`).
- Street Food generates its own abstract customers from its demand model (`CartDemand.Plan`, `SalesSimulator`, `CustomerPick`) and shows a queue near the player (`CartQueueView`). Crew-opened windows are run by `HireRoutes`.
- The only public integration class is `BigCopilotSnapshot` (`BigCopilotSnapshot.cs:15`), a versioned `V1` read and reviewed route write used by Big Copilot Link. All sale methods are private. There is no public sale path today.
- Street Food's absolute minute is `(day - 1) * 1440 + minute`; PC's is `Day * 1440 + minute` ([Street Food crew-driving notes](https://github.com/PeterHartwieg/big-ambitions-food-trucks/blob/242881d616d8faebb4dc41b2a996c6c96122bcdc/docs/crew-driving-and-fuel.md)). A contract must pass native day and minute, not either absolute minute.

### Reusable as is

`FoodSaleReceipts`, `FoodAuthority` (interval and generation rules), `ResidentMeal` and the meal state machine, the choice policy and weights, B memory keyed by business generation, awareness sources, market history cells, exact `Money`, the F1 accounting clock and the F3 continuation budget.

### Invariants to keep

- **Sale authority stays with the seller.** Street Food sells, books and pays. PC only calls Street Food's public API and verifies the result.
- **Exact money.** Prices and sales are whole cents (`long`); no float conversion on the vehicle path.
- **F1 accounting boundary.** A vehicle sale happens inside the current native minute epoch. PC never sells for a past minute and never across Street Food's midnight settlement ([F1 plan](f1-accounting-boundary.md)).
- **F3 bounded units.** Vehicle reads and stand updates run in bounded continuation units; no unbounded scan in a frame ([F3 checks](../checks/pc-f3-bounded-bulk-work.md)).
- **F7b save epoch.** No new PC payload key in native mod data, so the save-epoch payload list (`Persistence/SaveEpoch.cs:36`, at most 16 lines) is unchanged ([F7b checks](../checks/f7b.md)).
- **No rival finance writes.** Vehicles are player businesses; rivals stay read-only.
- **RLO unchanged.** E6 adds no RLO access.
- **Detached food shadow stays hypothetical.** E6 does not touch it.

## 3. Design

### 3.1 The seam: a versioned public API in Street Food

**Decision: Street Food adds a small versioned public static API, `FoodTrucks.ModApi.VendorApi`. PC binds it by reflection in `Compatibility/StreetFoodBridge.cs`, the same way `RloBridge` binds RLO's `CityApi`.**

Why:

- A read-only bridge cannot sell. A purchase must change cargo, the sale ledgers and the midnight takings. Those are private Street Food members (`SellItem`, `StreetSales.Record`, `CrewTakings.Accrue`). Reflecting into them would write Street Food state through a private path, which this plan forbids, and would break on any Street Food refactor (the problem `rlo-bridge.md` describes for RLO).
- Native events cannot carry it either. Carts are not native businesses: there is no `CustomerEntry`, `ProcessCustomer` or `BuildingRegistration` for a vehicle, and Street Food pays through one `ChangeMoneySafe` per vehicle and day, which does not identify customers.
- An E5e-style registration API in PC would make Street Food reference PC. E5 requires that Street Food never needs PC. With the API in Street Food, Street Food references nothing, and PC refuses safely if the API is missing or incompatible.
- Street Food already has this pattern: `BigCopilotSnapshot` is a versioned public static class called by another mod on the main thread.
- Placing the bridge in Compatibility keeps all name-based reflection into other mods in one layer with one validate-and-refuse discipline. Runtime gets a validated Core DTO.

### 3.2 Contract version 1

All members are main-thread only, use only primitive types, strings, arrays and Street Food-owned sealed DTO classes, and are bound by name. Nothing may be renamed while `ContractVersion` is 1.

```csharp
namespace FoodTrucks.ModApi
{
    public enum VendorApiStatus { Ready = 0, Off = 1, NotYet = 2, Refused = 3, Failed = 4 }

    public sealed class VendorApiVehicle
    {
        public readonly string VehicleId;        // Street Food's vehicle id, 1-128 UTF-16 units
        public readonly string Kind;             // "cart" or "truck"
        public readonly bool Open;
        public readonly long OpeningId;          // increases on every opening of this vehicle, never reused
        public readonly string OpeningOwner;     // "" (Street Food) or the owner that holds this opening
        public readonly bool OpenedByCrew;       // false when the player opened it
        public readonly bool EventOrCatering;    // the opening is at a city event or a catering gig
        public readonly int OpenedDay, OpenedMinute;   // native day and minute of day
        public readonly int ClosesDay, ClosesMinute;   // -1 when no planned close (player-opened)
        public readonly float X, Y, Z;           // the customer side of the window, on pavement (world position)
        public readonly float QueueX, QueueY, QueueZ, QueueHeading;
        public readonly int MealsPerHour;        // Street Food's current serving rate, floor; 0 = nobody serving
        public readonly bool MenuOverflow;       // more than 8 menu items; arrays below are then empty
        public readonly string[] Items;          // the equipment menu, ordinal order, exact item names
        public readonly long[] PriceCents;       // this vehicle's current price, > 0
        public readonly int[] Units;             // sellable units (paid, unsealed, not held), >= 0
    }

    public sealed class VendorApiSale
    {
        public readonly int BookedDay;           // native day the takings are booked on
        public readonly long Cents;
        public readonly int UnitsBefore, UnitsAfter;
        public readonly long Sequence;           // Street Food's sale counter for this vehicle
    }

    public static class VendorApi
    {
        public static int ContractVersion => 1;
        public static long Revision { get; }     // changes whenever any field above could change
        public static VendorApiStatus Read(VendorApiVehicle[] into, out int count);   // at most 64
        public static VendorApiStatus RequestOwner(string owner, string vehicleId, bool want, out string reason);
        public static VendorApiStatus Renew(string owner, int nativeDay, int nativeMinute, out int leaseUntilDay, out int leaseUntilMinute);
        public static VendorApiStatus ReleaseAll(string owner);
        public static VendorApiStatus Sell(string owner, string vehicleId, long openingId, string item,
            long priceCents, int nativeDay, int nativeMinute, out VendorApiSale? sale, out bool uncertain, out string reason);
    }
}
```

Street Food's promises for version 1:

- `Read` copies a detached view; it never allocates per frame when `Revision` is unchanged and never changes state. More than 64 vehicles returns `Refused` and count 0, never a truncated list.
- `RequestOwner(owner, id, true)` records a wish together with the native hour of the call (`day * 24 + hour`). Street Food grants it only to an opening that starts in a **later native hour** than that recorded hour while the lease is valid: that opening gets `OpeningOwner = owner`. An opening in the same hour as the request gets `""` and the request stays recorded for the next opening. A repeated `RequestOwner(true)` keeps the first recorded hour. An opening's owner never changes from Street Food to PC while it is open.
- `RequestOwner(owner, id, false)` and lease expiry hand an open PC-owned opening back to Street Food at once. Street Food then starts its own customers from that minute with a fresh queue. PC calls `want = false` only after its own work for the vehicle has drained (3.5).
- `Renew` extends the lease to 60 native minutes after the given time. A lease that has run out hands every opening of that owner back, as above. The lease is saved in Street Food's save, so a reload without PC hands vehicles back within 60 game minutes.
- While an opening is owned by PC, Street Food generates none of its own customers for it. Everything else continues: closing at the stop's end, sell-outs, licences, events, crew pay for serving and health checks.
- `Sell` checks status, vehicle, opening id, owner and lease, menu item, `priceCents` equal to the current price, stock and ledger room. It refuses with a reason before touching anything. When all checks pass it runs Street Food's own one-item sale at the given native minute (its `SellItem` path with the crew-batch flush) and returns the booked day, cents and before/after units. If an exception happens after the ledger record, it returns `uncertain = true`.

Street Food may add members in later versions. PC accepts exactly the versions it was built for.

### 3.3 Vehicles as places and stands

**Decision: each PC-owned vehicle gets one vendor place, `vendor:<escaped vehicle id>`, with `PlaceKind.Shop`, kept in a new saved vendor table and added to the simulator's place map. Its position and hours belong to one stand. A move to a new stand waits until nobody is committed to the old one.**

A stand is one opening at one position: `(OpeningId, X, Y, Z, opened, closes)`. PC uses Street Food's height, then checks it against the NavMesh at that point (the walking-route query in `Runtime/WalkingRoutes.cs` needs a 3D position); if no NavMesh point lies within 2 m, PC refuses that stand and shows the reason, and the vehicle stays with Street Food for that opening. PC publishes the vendor place for a stand with the stand's position and one opening window. When the vehicle closes, the window ends, so residents who arrive later get the existing closed-shop outcome.

When a vehicle opens somewhere else, PC must not move the place while any resident is travelling to it, waiting there or holds a committed meal for it, because their captured walking path and arrival check point at the old position. Until those meals resolve, the vendor place keeps the old position with no open hours: those residents arrive, find it closed, record `ShopExperience.Closed` and keep their money. When the last reference resolves, PC moves the place and opens the new stand. Meals resolve within one meal window, so the new stand waits at most a few game hours, usually none.

Why this design:

- Every food path keys on `PlaceId` and `FindPlace`. One stable place per vehicle keeps scopes, history, receipts and B memory on one business across stands, so a regular keeps their memory of "their" cart.
- A new place id per stand would start a new business generation at every move and wipe memory; it would also churn the place table.
- Snapping a stand to the nearest existing building would collide with that building's own shop scope at the same `PlaceId`.

Vendor places are walking-only destinations, like outdoor places (`Runtime/OutdoorPlaces.cs`): residents reach them only on food excursions with captured walking paths, never by car, bus or subway. They are never homes, workplaces or optional-stop destinations; the optional-place list in `CitySimulator` (`:153`) skips them. At most 64 live and 64 retired vendor places exist (3.5); they do not count toward the 20,000 city places.

### 3.4 Business identity and scope

- Signature `streetfood:vehicle:<escaped id>`, business id `<signature>:generation:<n>` (the `FoodAuthority.BusinessId` form). The generation increases only if Street Food reports a different kind for the same id, or the id disappears and later returns. Memory, history and receipts stay on one generation across stands.
- `FoodScope` gets a channel field (`Shop` or `Vehicle`). Scopes with `Vehicle` are served by the vehicle market. The scope's products are the vehicle's equipment menu.
- Whole-vehicle rule, the same as shops: PC owns an opening only if every menu item is a canonical, loaded name, has a positive exact price, and is admitted by the E5c resolver as `Food`; the menu has 1 to 8 items; and the product is executable under the current held policy. Until E5b's held-policy execution lands, that means the original three only. Any failure leaves the whole vehicle with Street Food and shows the reason. Units of zero are known zero (sold out), not a refusal.
- `MealsPerHour = 0` means nobody is serving; the vehicle is unavailable, not refused.

### 3.5 Ownership state machine

PC keeps one `FoodAuthority`-style record per vehicle in the population payload (not in `FoodAuthorityStore`, whose early manifest only exists to steer native shop hooks before population load; Street Food's own grant and lease do that job for vehicles).

- **Request.** When a vehicle is eligible, PC sets `Desired` and calls `RequestOwner(true)`. When it stops being eligible, PC clears `Desired` and stops new captures at once.
- **Acquire.** Street Food enforces the same later-hour rule as `FoodAuthority.Boundary` (`hour > RequestedHour`): it grants only an opening that starts in a later native hour than the request (3.2). When the snapshot reports `OpeningOwner = "persistentcity"`, PC checks that the opening's hour is later than its own `RequestedHour`, records `FromHour` and publishes the stand. If Street Food ever reports a grant that breaks this rule, PC does not serve it and calls `RequestOwner(false)` at once (no PC work exists for that opening yet), so Street Food resumes its own customers; the reason is shown.
- **Serve.** `CanServe` is true only while the snapshot says open, the opening id equals the stand, the owner is PC, the lease is valid and the whole-vehicle rule holds.
- **Release.** After `Desired` is cleared and no meal is pending, travelling or being served at this vehicle (old work drained), PC calls `RequestOwner(false)` and records `UntilHour`. Street Food resumes its own customers from that minute.
- **Lease.** PC renews every 15 native minutes from the logical-event preparation step, so a 60-minute lease never runs out while PC is healthy. If PC cannot renew (stopped epoch, refused city), it calls `ReleaseAll` once when it can, otherwise the lease runs out by itself.
- **Missing vehicle.** A vehicle that disappears from the snapshot (sold, removed, Street Food absent) is unserviceable at once; its place, scope, history and receipts stay as saved facts while anything still points at them.
- **Retirement.** A missing vehicle's row becomes a retired row. It is removed, with its vendor place and its scope, only at a save-consistent event boundary when nothing references it: no meal, held decision, routine stop or excursion names its place or business; no receipt for it is pending or uncertain; and no retained market history row (fourteen days) names it. Live rows (vehicles in the snapshot) are at most 64, matching Street Food's limit; retired rows are at most 64 more. When a new vehicle needs a row and all retired slots are taken, the oldest retired row (earliest `UntilHour`, then ordinal id) whose references have all cleared is pruned first; if none can be pruned, the new vehicle stays with Street Food and the reason is shown. Nothing is pruned while referenced. Rows are not retired while Street Food is absent: a vehicle missing only because Street Food is absent keeps its live row.

Openings Street Food marks `EventOrCatering` stay with Street Food (section 9, Q4). Openings the player started stay with Street Food (section 9, Q2).

### 3.6 Choice, awareness, memory and rivals

- The vehicle market turns each served stand into a `FoodBusiness` with `AlternativeKind = Player`, the stand's place, one opening window, offers with exact prices and units, and `MealsPerHour`. From there the existing choice code scores it exactly like a shop: price, distance, queue (`NextServiceAt`), taste and fit, memory contribution, weather multiplier. No vehicle-specific weight is added.
- Awareness: the vendor place enters the same catalogue. Routine, discovery and memory sources work unchanged; a stand change bumps the awareness revision and the existing staged builder rebuilds it in bounded steps. No awareness source is added for vehicles.
- Memory: B memory records served, closed, stockout and queue experiences per business generation, unchanged.
- Rivals: rival scopes, F6b lanes and sell-outs are unchanged. Vehicles are never rivals and rivals never see vehicles.
- `ReadLocal` ranks vendor places with shops by saved position before any supply read, inside its existing limit of 8. The composite market does the merging and ranking (3.8).

### 3.7 Checkout

The vehicle `Buy` follows `BuyLive` step by step:

1. Check save, native minute inside the current epoch (`EarliestMinute`), receipt state (`AlreadyPaid`) and `CanServe`.
2. Read one vehicle from the current snapshot; refuse if opening id, owner, price or units changed, or service time is shorter than `60 / MealsPerHour`.
3. `FoodSaleReceipts.Begin`.
4. Call `VendorApi.Sell` with the meal's product, exact cents and the native day and minute.
5. Refused before effects (`Sell` returned a refusal with `uncertain = false` and no sale): call the new `FoodSaleReceipts.Cancel`, then return a definite refusal (stockout reason only when Street Food says the unit count was zero). `Cancel` removes the receipt only when its value is exactly `pending|` plus this meal's identity (price, business, product); any other value throws and the meal is treated as uncertain. It is called only on this path. Shops need no cancel because `BuyLive` refuses before `Begin`. `uncertain`, an exception, or a result whose cents, day, product or `UnitsAfter = UnitsBefore - 1` do not match: throw "outcome is uncertain; do not retry", exactly as for shops.
6. Re-read the vehicle; other items' units must be unchanged. Then `FoodSaleReceipts.Confirm`.

An exception, an uncertain result or a mismatch keeps the pending receipt as evidence, exactly as for shops; only a confirmed pre-effect refusal is cancelled.

The resident's wallet is charged by the existing meal path after a confirmed sale, in exact cents. The player's money comes only from Street Food's midnight `PayIn`. F1 already drains PC's events before native time enters the next minute, so every PC sale is booked before Street Food's `RunDaily` prefix settles the day.

### 3.8 Composition with shops

A Runtime composite market implements every interface `NativeFoodMarket` implements, plus the rival interfaces.

- **Calls tied to one scope or business** (`Buy`, `ReadBusiness`, `CaptureBusiness`, `IsCandidate`, `CanServe`, `IsConfirmedPaid`, `ReadFailureBusiness`) go to the shop or vehicle market by the scope's channel.
- **Calls not tied to one scope are merged.** `Read(minute)` returns both markets' results. `ReadLocal(origin, minute, tolerance, maximumCandidates)` asks each market for its own candidates, merges them, ranks them by saved-position distance from `origin` with the business id as the ordinal tie-break (the shop market's own rule, `NativeFoodMarket.cs:242-246`), and applies `maximumCandidates` once. `EarliestMinute` is the later of the two values. `BeginCapture` opens both markets' captures and returns one handle that disposes both.
- **Rival calls** go to the shop market unchanged.

**Population validation is split by channel.** `NativeFoodMarket`'s constructor calls `NativeFoodOwnership.ValidatePopulation`, which refuses the population if any `state.Shops` entry has no matching row in the shop authority manifest (`Runtime/NativeFoodOwnership.cs:118-131`). Vehicle scopes have no such row by design (3.5), so E6d changes that loop to check only scopes with channel `Shop`; the reverse check, that every manifest row with `FromHour >= 0` has a matching scope, also compares with `Shop` scopes only. Shop validation stays exactly as strict as today. Vehicle scopes are validated separately against the vendor table: each `Vehicle` scope must have exactly one vendor row with the same place id and business id, each row has at most one scope, and every vehicle meal, decision and receipt must name a row's place. That check runs in E6c's reader (save shape) and again in the vehicle market's constructor (live state). This is the only change to shop code; `NativeFoodMarket.cs` itself is not edited.

### 3.9 Bodies

When a resident's body is visible, it walks to the stand position and waits on the queue line from `QueueX/QueueZ/QueueHeading`. Body caps and physical-population rules are unchanged. Street Food's own queue view is not changed and does not draw PC residents.

### 3.10 Cadence

PC reads `VendorApi.Revision` once per logical minute (one static property). Only a changed revision triggers `Read`, which is copied into a validated Core image in units of at most 16 vehicles. Stand publication and awareness rebuild run in the same bounded continuation. Nothing runs per frame beyond the revision read.

## 4. Slice breakdown

Effort is engineer-days including review and offline proof. Each slice writes `docs/checks/<slice>.md` and `docs/perf/<slice>.md` only (E6a writes its records in the Street Food repository under its own rules).

| Slice | Scope | Effort | Offline tests | Perf measurement | Schema | Depends on / parallel |
| --- | --- | --- | --- | --- | --- | --- |
| **E6a — Street Food vendor API** (Street Food repository, separate PR) | `VendorApi` v1 from 3.2: snapshot, revision, owner request at opening, lease, release, sale through `SellItem`, no own customers for PC-owned openings, grant and lease saved in Street Food's save. | 3–4 | Street Food Core/unit tests: opening-boundary grant; a request at 10:05 with an opening at 10:30 stays Street Food's and keeps the request; the next opening at 11:00 or later is granted; a repeated request keeps the first hour; lease expiry mid-opening, release resumes own customers, sale refusals before effects, exact cents and booked day across midnight catch-up, uncertain flag after ledger write, 64/65 vehicles, 8/9 items, revision unchanged means no allocation, reload with and without lease. | Street Food tick with 0/8/64 open vehicles, PC-owned vs own customers; `Read` and `Sell` cost. | Street Food's own save rules; none in PC. | Starts now. Parallel with E6b and E6c; the contract text in 3.2 is the agreement. |
| **E6b — bridge and Core vendor rules** | `Compatibility/StreetFoodBridge.cs` (bind, validate, copy); the in-memory vendor types in `Core/VendorState.cs` and the `FoodScope.Channel` field; Core vendor image validation, stand rules, ownership state machine, lease schedule, retirement rule; `CitySimulator` vendor place publish/move/retire with the "no references" guard; awareness revision bump on stand change. No Runtime wiring. | 2–3 | Fake `VendorApi` types: missing API, wrong version, missing member, wrong type, `Refused`/`Failed`; bounds and names; stand move deferred while a resident travels or waits and applied after; acquire only at a later opening; release only after drain; lease renew and expiry; a Street Food grant that breaks the later-hour rule is released; NavMesh-height refusal; retirement only after every reference clears, oldest retired row pruned first, new vehicle refused when no row can be pruned; identical results under different update cadence. | Revision poll, image copy at 8/64 vehicles, stand publish, awareness rebuild after a move. | None. | Parallel with E6a. Edits `CitySimulator.cs` and `FoodWeightedCommerce.cs`: serialize with any F5 slice editing those files. |
| **E6c — vendor save shape** | Next free writer (expected **27**): serializes E6b's types (`FoodScope.Channel`, vendor table with place, stand, deferred stand, authority, generation, live/retired flag), food-place checks that accept vendor places, channel-aware scope validation, strict reader, older-city refusal with "start a new city" status, pinned golden hash. | 1–2 | Round trip; bounds 64/65 live and 64/65 retired rows, 128/129-character ids; vehicles churned past 64 (sold and bought repeatedly) keep the city saveable; reload of a deferred stand move; `Vehicle` scope without a row, row without its scope, meal naming a missing vendor place; duplicate ids; channel/place mismatch; stand without vendor place; future schema refused and preserved; older schema refused with the new-city status; golden hash. | Save bytes and read/write time at 0/8/64 live vendors and 64 live plus 64 retired. | **One bump.** | After E6b (it serializes E6b's types) and after F5c-lite (schema 26), because both edit `SaveCodec.cs` and `Model.cs`. Parallel with E6a. |
| **E6d — vehicle market and checkout** | `Runtime/StreetFoodVendorMarket.cs`, `Runtime/FoodMarkets.cs` composite with merged `Read`/`ReadLocal`/`BeginCapture` (3.8), channel-aware `NativeFoodOwnership.ValidatePopulation`, `FoodSaleReceipts.Cancel`, wiring at market construction in `PersistentCityMod.cs`, receipts, rig-readable `pc vendor` console diagnostics. Original-three products only. | 3–4 | Test-double API: positive sale with exact cents/day/units; every pre-effect refusal; uncertain and exception paths never retried; paid receipt replay after reload; price change between choice and checkout; stand closed on arrival; whole-vehicle refusal for a non-`Known` item; no captures after `Desired` cleared; composite forwards rivals unchanged; eight nearer shops plus one vehicle give eight candidates, nearest first, and seven nearer shops plus one vehicle include the vehicle; `EarliestMinute` is the later value; one capture handle disposes both; a pre-effect refusal cancels the receipt and the same intent then buys at another shop, also with a save/reload between the two; an uncertain result keeps the pending receipt; `Cancel` with a different identity throws; an integration test constructs a mixed shop/vehicle city, saves, reloads and constructs it again, with Street Food present and with Street Food absent (vehicle scopes load and stay unserviceable); a shop scope missing from the manifest is still refused. | Snapshot-driven `Read`, capture and checkout p50/p95/max, allocations; 64-vehicle rush; mod-work gate. | Uses E6c's writer. | Needs E6c (and so E6b); codes against the E6a contract with a test double, so it can merge before E6a ships. |
| **E6e — bodies at the hatch** | Physical-population dwell at the stand's queue line; no Street Food queue change. | 1–2 | Body placement from queue fields; bodies leave on stand close; caps unchanged; no identity change on pool reuse. | Body spawn/dwell cost at the vehicle, whole-frame reported separately. | None. | After E6d. Owns `Runtime/DwellSpots.cs` and the vendor hook in `Runtime/PhysicalPopulation.cs`. |
| **E6f — non-original vehicle products** | Admit dumplings, chopped cheese, Bakery and vanilla items on vehicles through E5b's held-policy execution: name identity, food-by-default, seeded taste, config/API overrides; Street Food absent/returned behaviour. | 1–2 | Dynamic names on a test-double vehicle; Drink/Bundle/NonFood/Unknown override keeps the whole vehicle with Street Food; taste stable across reload and catalogue order; missing Street Food keeps history and receipts; return re-validates. | Choice cost with 8-item vehicle menus. | None (uses writer from E6c). | After E5b held-policy execution and E6d. Edits no file of E6e. |

File ownership:

| File | Owner |
| --- | --- |
| Street Food `unity/FoodTrucks/Scripts/ModApi/*`, its `CartSelling.cs`, save state and tests | E6a |
| `Compatibility/StreetFoodBridge.cs`, `Core/VendorState.cs`, `Core/VendorStands.cs`, `FoodScope.Channel` in `Core/FoodCommerce.cs`, vendor edits in `Core/CitySimulator.cs` and `Core/FoodWeightedCommerce.cs` | E6b |
| `Persistence/VendorWire<N>.cs` (N = the new writer), edits in `Persistence/SaveCodec.cs` (including `FoodPlaceLookup`) and `Core/Model.cs` | E6c |
| `Runtime/StreetFoodVendorMarket.cs`, `Runtime/FoodMarkets.cs`, channel filter in `ValidatePopulation` (`Runtime/NativeFoodOwnership.cs`), `Cancel` in `Persistence/FoodSaleReceipts.cs`, market wiring in `Runtime/PersistentCityMod.cs`, `Runtime/VendorDiagnostics.cs` | E6d |
| `Runtime/DwellSpots.cs`, vendor hook in `Runtime/PhysicalPopulation.cs` | E6e |
| Vehicle admission in `Runtime/StreetFoodVendorMarket.cs` after E6d merges | E6f |

E6a runs in parallel with everything else. E6c follows E6b, because it serializes E6b's types. E6d follows E6c. E6e and E6f need E6d and can run in parallel with each other. Total about 11–17 days; the PC critical path is E6b → E6c → E6d → E6e/E6f.

## 5. Save schema impact

- **One bump, in E6c**, to the next free writer when it merges (27 if F5c-lite takes 26). No migration. A city written by an older development schema is refused with the "start a new city" status and its payload is kept unchanged. The new writer's golden hash is pinned.
- Added to the population payload:
  - `FoodScope.Channel`: `Shop` or `Vehicle`. Every existing scope writes `Shop`.
  - A vendor table, at most 64 live plus 64 retired rows (3.5), ordinal by vehicle id. Each row has a live/retired flag and: vehicle id (1–128 UTF-16 units, no control characters), kind, generation, place id, stand (opening id, position x/y/z, opened/closes native day and minute, published flag), deferred stand (same fields or absent) and the vehicle's authority (desired, requested, from, until, suspended).
  - Vendor places are rebuilt from the table on load; they are not written to `CityState.Places`. The codec's food-place checks (`FoodPlaceLookup` in `Persistence/SaveCodec.cs:906-1004`, which today require every scope, history row, routine stop and meal place to be a city `Shop` place) also accept a vendor row's place. E6c owns that change.
- Bound: at most about 128 × 600 = 76,800 characters, far below the codec caps; E6c measures the real figure.
- Unchanged: `FoodSaleReceipts` keys and value format (E6d adds only an identity-checked `Cancel`), `FoodAuthorityStore` (`food-authority-v1`), the save-epoch marker and its payload list, every rival record.
- Street Food keeps its own grant and lease in its own save. PC writes them only through `VendorApi`.

## 6. Performance

Gate: mod-work p95 below 5 ms; flag every mod-work spike of 30 ms or more with count, condition and component; whole-frame timing reported separately ([roadmap](../roadmap.md#performance-gate)).

Expected PC cost:

- Revision poll: one static property per logical minute, about 1 µs, no allocation.
- Snapshot copy on change: at most 64 vehicles × 8 items in units of 16 vehicles. Expected well under 0.5 ms per unit; Street Food builds the view, PC validates it.
- Stand publish and awareness rebuild: only on a stand change, through the existing staged builder.
- Checkout: one `Sell` call plus two one-vehicle reads per purchase. Expected under 0.2 ms; purchases are rare events.
- Choice: vehicles add at most 8 candidates to `ReadLocal`'s existing limit, so per-decision cost does not grow beyond today's bound.

Street Food's own tick gets cheaper for PC-owned openings because it simulates no customers there; that is measured in E6a and reported as whole-frame, not PC mod-work.

| Slice | Measures |
| --- | --- |
| E6a | Street Food tick with 0/8/64 open vehicles, own vs PC-owned; `Read`/`Sell` cost and allocations. |
| E6b | Revision poll, image copy at 8/64 vehicles, stand publish, awareness rebuild after one and many moves. |
| E6c | Save bytes, read/write time at 0/8/64 live vendors and 64 live plus 64 retired. |
| E6d | Read, capture, checkout p50/p95/max and allocations; 64-vehicle lunch rush; combined mod-work p95 and ≥30 ms spikes. |
| E6e | Body dwell cost near a vehicle; whole-frame separate. |
| E6f | Choice cost with 8-item vehicle menus and dynamic names. |

Cheap wins found during a slice are taken and logged with before/after numbers in that slice's perf file. No save profiling is planned.

## 7. In-game checks for the batched rig

All checks run on the headless agent rig in one disposable FT city, **FT-E6-Vendors**, against the development head. The [rig repository](https://github.com/PeterHartwieg/big-ambitions-agent-rig/tree/70021ed) is read-only for this plan; the capabilities named below are from its `docs/rig-ops.md`, `docs/rig-flows.md`, `docs/rig-modset.md` and `docs/rig-fixtures.md`, and from Street Food's [test bridge](https://github.com/PeterHartwieg/big-ambitions-food-trucks/blob/242881d616d8faebb4dc41b2a996c6c96122bcdc/docs/test-bridge.md) (the FT bridge). Every timed window follows the autosave rule: record the original setting, disable it, check it is off before every window and after every reload, keep saves outside windows, discard a contaminated sample, restore at cleanup.

Shared setup (existing capability: FT bridge `vehicle buy`, `vehicle restock`, `commissary order`, `price`, `route add_stop`, `crew assign`/`schedule`, `fixture`, `time`, `save`/`load`; rig-ops `shop_fixture` for the player shop; `rig flow run`): buy cart A with only a Hot Dog Grill (menu: hot dog) and truck B with an Industrial Grill (menu: burger and kabob); stock both through the commissary; set prices; give each a crew and a weekday route with two stops 400 m apart near a dense residential block, a PC-owned player shop and a rival shop; save checkpoint `FT-E6-Vendors-base`. Cart A is the eligible vehicle. Truck B is the whole-vehicle refusal case until E6f, because kabob is not an original-three product.

1. **Contract present, missing and old.** rig-automated (existing capability: RIG-MODSET DLL sets, FT `console`/`state`; pending capability: RIG-SFAPI).
   Setup: three arms from the base checkpoint: Street Food with E6a, Street Food 0.10.11 (no API), Street Food with E6a plus a test build that reports `ContractVersion = 2`.
   Steps: load; run `pc vendor status`; run one hour of game time; read FT `day_summary`.
   Expected: E6a arm shows bridge Ready, cart A eligible and truck B refused with the reason "menu item not executable: ba:itemname_kabob" (exact name as loaded). The other arms show "Street Food vendor API missing" or "unsupported contract 2", no vendor scope, and Street Food sells to its own customers with normal `day_summary` lines.

2. **Ownership starts at an opening.** rig-automated (existing capability: FT `route`, `time`, `state`, `console`, `day_summary`).
   Setup: base checkpoint, E6a arm; both vehicles closed.
   Steps: let PC request ownership; run until the crews open stop 1; run one hour; let the routes move to stop 2 and open; run one hour.
   Expected: `pc vendor status` shows cart A desired, then from-hour equal to its stop-1 opening hour. Street Food's log shows no own customers for cart A's PC-owned openings and normal customers before. Truck B keeps Street Food's own customers throughout. If the request came in the same hour as an opening, that opening stays Street Food's and the next one is PC's.

3. **Positive sale with exact money.** rig-automated (existing capability: FT `state`, `time`, `console`, `day_summary`, RIG-LEDGER; pending capability: RIG-VLEDGER).
   Setup: checkpoint from check 2 with the cart open and PC-owned at lunch.
   Steps: run lunch; read PC receipts and resident wallets with `pc vendor sales`; read FT `day_summary` and the cart's cargo; run past midnight; read native transactions.
   Expected: every PC vendor receipt is paid, each matches one unit fewer of hot dogs, Street Food's day units and cents rise by the same count and exact cents, each resident's wallet falls by the price, and the midnight Street Food payment for the cart equals Street Food's day total including PC sales. No PC credit to the player. No duplicate receipt.

4. **Choice next to shops and rivals.** rig-automated (existing capability: FT `console`, `state`, RIG-RIVALFP `rival_fingerprint`).
   Setup: the cart open at stop 1, within walking range of a PC-owned player shop and a rival shop.
   Steps: run breakfast and lunch; dump decision traces for 50 residents near stop 1 with `pc food decisions`; read rival fingerprints before and after.
   Expected: the cart appears as a candidate with source tags (routine, discovery, memory) and the same score terms as the shop; some residents choose each option; rival fingerprints are unchanged.

5. **Stand move with committed residents.** rig-automated (existing capability: FT `route`, `time`, `console`, `save`/`load`).
   Setup: the cart open at stop 1 with at least one resident travelling to it; set the stop's close to five minutes later.
   Steps: let the route close stop 1 and open stop 2; follow the travelling resident; save and reload while the move is deferred; continue until the move applies.
   Expected: the resident arrives at stop 1, records a closed visit and keeps their money. The vendor place keeps stop 1's position until no reference remains, then moves to stop 2; residents near stop 2 can choose it afterwards. The reload keeps the deferred move.

6. **Handback mid-opening.** rig-automated (existing capability: FT `console`, `state`, `time`; the `pc vendor withdraw <vehicle>` diagnostic is an E6d deliverable, usable only in FT cities).
   Setup: cart A open and PC-owned at lunch with at least two residents committed to it.
   Steps: run `pc vendor withdraw` for cart A; run until both committed residents finish; run 30 more minutes; read Street Food's log and `pc vendor status`.
   Expected: from the withdraw minute no new resident chooses cart A; the committed residents are served or fail safely; then PC releases and Street Food's own customers resume in the same opening. No minute has both PC and Street Food customers. The next opening is requested again under the normal rules.

7. **Lease and missing PC.** rig-automated (existing capability: RIG-MODSET, FT `save`/`load`/`time`/`day_summary`).
   Setup: save with the cart PC-owned and open.
   Steps: relaunch without Persistent City; load; run 90 game minutes; then relaunch with PC and load the original save.
   Expected: without PC, the cart's Street Food customers resume within 60 game minutes. With PC, the lease is renewed at load, paid receipts stay paid, and committed meals finish or fail safely with no duplicate sale.

8. **Player-opened windows.** rig-automated (existing capability: FT `vehicle open`/`close`/`work_window`, `state`, `console`).
   Steps: the player opens the cart and works the window for 30 minutes; then closes it and lets the crew open.
   Expected: the player-opened opening stays Street Food's with its visible queue; the crew opening is PC's (section 9 Q2 default).

9. **Uncertain sale is not retried.** rig-automated (pending capability: RIG-SFFAULT).
   Setup: an E6a test build with a guarded rig fault that throws after the ledger record on the next sale.
   Steps: arm the fault; run until one PC sale; save; reload.
   Expected: that meal is `Uncertain`, its receipt pending, no second `Sell` call for that intent, and Street Food's ledger shows one sale.

10. **Modded products on a vehicle.** rig-automated (existing capability: RIG-CAT `catalog`, FT `vehicle restock`, `console`, `metadata_config`; requires E6f merged).
    Setup: a cart with a dumpling steamer and griddle stocked with dumplings and chopped cheese.
    Steps: read the catalogue for both names; run lunch; dump decisions and receipts; repeat with `metadata_config` marking chopped cheese `Drink`.
    Expected: both products are admitted as Food with default fits and stable per-resident taste across a reload; paid sales follow check 3. With the `Drink` override the whole cart stays Street Food-owned.

11. **Bodies at the hatch.** rig-automated (existing capability: `camera look_at`, `screen_pos list`, `capture`, FT `shoot` pose `hatch-pavement`; requires E6e).
    Steps: follow a resident body to the open truck; capture screenshots while they queue and leave.
    Expected: the body walks to the pavement side, waits on the queue line, leaves after service; no body stands in the road.

12. **Older city refused.** rig-automated (existing capability: FT `load`, `console`; RIG-MODSET).
    Steps: load an FT city written by writer 25 or 26 with the E6c build.
    Expected: "start a new city" status, payload unchanged, Street Food runs its own customers.

13. **Performance window.** rig-automated (existing capability: `rig flow run` autosave guard, FT `state`/`time`/`console`/`shoot`; pending capability: unified PC mod-work/whole-frame assertions already requested for F6b).
    Steps: matched idle, breakfast, lunch and evening windows with 0, 2 and 8 PC-owned vehicles, then a stand-move burst.
    Expected: mod-work p95 below 5 ms; every spike of 30 ms or more listed with component; whole-frame reported separately.

Rig capability requests (for Coding Projects; no check is assigned to Peter):

- **RIG-SFAPI**: RIG-MODSET input sets for Street Food builds with the patched FT bridge: E6a build, 0.10.11 without API, and a contract-version-2 test build. Unblocks checks 1, 7, 12.
- **RIG-VLEDGER**: extend RIG-LEDGER to vehicles: join FT `day_summary` per vehicle, Street Food's midnight transaction (label, day, amount) and PC vendor receipts and wallet deltas, and assert equality. Unblocks check 3.
- **RIG-SFFAULT**: arm a one-shot guarded fault in an E6a test build through the FT bridge. Unblocks check 9.
- The unified mod-work/whole-frame assertions already requested for F6b check 8 cover check 13.

## 8. Dependencies on other plans and running slices

- **E5b held-policy execution** (remaining E5b work, [checks](../checks/e5b-complete-menu.md#refusal-boundary-and-remaining-implementation)): needed by E6f only. E6a–E6e work with the original three products without it.
- **E5c/E5d/E5e**: name identity, food-by-default, seeded taste and metadata API are reused as implemented; E6 changes none of them.
- **F5c-lite** (running, schema 26): E6c takes the next writer after it and rebases on its `SaveCodec`/`Model` edits.
- **F5d/F5e/F5g**: they edit `CitySimulator.cs`; E6b's vendor-place edit is small and must be serialized with them.
- **F7c/F7d** (snapshot/wire split, compact writer): if one lands first, E6c writes its vendor table in that format; E6 does not depend on them.
- **F1, F3, F7b**: used as contracts; E6 adds no accounting, posting or save-epoch change.
- **F6b, F6d**: rival limits unchanged; F6d's ledger contract should list vehicle sales as player revenue booked by Street Food. No change to F6d is needed for E6 to merge.
- **F8** (capability boundaries, no plan yet): E6 adds one external dependency boundary (Street Food's API), handled like RLO's.
- **Street Food crew-driving plan**: independent. E6 does not route trucks through PC traffic.
- **Rig**: RIG-SFAPI, RIG-VLEDGER and RIG-SFFAULT above; RIG-LIVE is a prerequisite for all checks, as for E5.

## 9. Game-design questions for Peter

1. **Do residents replace Street Food's own customers at a vehicle PC owns, or come on top of them?** Recommended default: replace, as PC does for shops. Street Food's area, venue, event and price-curve demand then no longer drives that opening; residents nearby do.
2. **Should openings the player starts and works themselves get residents?** Recommended default: no. Player-opened windows keep Street Food's own visible queue; crew-opened windows get residents. A player who joins a crew window keeps it PC-owned.
3. **Drinks on a vehicle (soda, coffee, tea, slushies).** E5 makes every new product Food by default, so a resident may buy a soda as a meal. Recommended default for E6: keep the E5 rule; marking a drink as `Drink` in config keeps that vehicle with Street Food; drink add-ons wait for the deferred drinks-and-bundles item.
4. **City events and catering.** Recommended default: openings at a city event or a catering gig stay with Street Food, because PC has no event crowds.
5. **Street Food reputation and health grades.** Recommended default: no extra effect on residents in E6. Residents judge a vehicle by their own experiences (served, queue, stockout, closed); Street Food keeps its grades for its own customers.

## 10. Risks and refusals

- **Street Food without the API, or an unsupported version.** PC refuses vehicle scopes, shows the reason and leaves Street Food untouched. No reflection into private members as a fallback.
- **Street Food fails mid-sale.** `uncertain` or an exception makes the meal uncertain with a pending receipt; it is never retried. Street Food's ledger stays its own record.
- **Double demand.** Prevented by the opening-boundary grant and immediate handback; check 6 asserts no minute has both kinds of customer. A Street Food bug here would double-count sales, so E6a tests it directly.
- **Lost demand.** If PC owns an opening and then dies, Street Food has no customers until the lease runs out (at most 60 game minutes). This is the price of never double-counting.
- **Stand moves.** A vehicle that keeps moving while residents are committed can wait a few game hours before PC serves its new stand. Accepted; the alternative breaks captured paths.
- **More than 64 vehicles or more than 8 menu items.** Street Food reports `Refused` or `MenuOverflow`; PC leaves those vehicles with Street Food. No truncation.
- **Clock mismatch.** The API carries native day and minute, so the two mods' absolute-minute formulas never meet.
- **Price changed between choice and service.** Checkout refuses before effects; the resident keeps their money.
- **Street Food removed with saved vendor history.** Scopes, history and receipts load as saved facts; vehicles are unserviceable; nothing is remapped. If Street Food returns, each vehicle re-qualifies at its next opening.
- **Refused here:** writing Street Food state outside `VendorApi`; reflecting into Street Food private members; PC crediting the player; selling for a past minute; serving a stand other than the one the resident chose; treating vehicles as rivals; any change to RLO, rival finances or native shop checkout.
