# E6: cart and food-truck sales

Residents buy at the player's own carts and food trucks from HART. Street Food (assembly `FoodTrucks`, repository `big-ambitions-food-trucks`). Street Food stays the seller: it owns the vehicle, stock, prices, ledgers and the midnight payment. PC chooses, walks the resident there and calls Street Food's public API.

Status: E6b to E6f (bridge, vendor rules, save shape, vehicle market and checkout, bodies at the hatch, non-original products) are implemented in PC (`Compatibility/StreetFoodBridge.cs`, `Runtime/StreetFoodVendorMarket.cs`). E6a, the API in Street Food, is planned in that repository; nothing here asserts that it exists. The full 6 November plan is in the [13 November snapshot](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/e6-carts-food-trucks.md).

## Contract version 1: `FoodTrucks.ModApi.VendorApi`

PC binds it by reflection in Compatibility and refuses on any mismatch, the same discipline as RLO's `CityApi` ([RLO contract](../rlo-contract.md)). All members are main-thread only, use primitives, strings, arrays and Street Food-owned sealed DTOs, and are bound by name. Nothing is renamed while `ContractVersion` is 1. PC accepts exactly the versions it was built for.

- `enum VendorApiStatus { Ready = 0, Off = 1, NotYet = 2, Refused = 3, Failed = 4 }`
- `VendorApiVehicle`: `VehicleId` (1 to 128 UTF-16 units), `Kind` (`"cart"` or `"truck"`), `Open`, `OpeningId` (increases on every opening, never reused), `OpeningOwner` (`""` for Street Food), `OpenedByCrew`, `EventOrCatering`, `OpenedDay`/`OpenedMinute`, `ClosesDay`/`ClosesMinute` (-1 when no planned close), `X`/`Y`/`Z` (customer side of the window), `QueueX`/`QueueY`/`QueueZ`/`QueueHeading`, `MealsPerHour` (0 = nobody serving), `MenuOverflow` (more than 8 items; arrays then empty), `Items`, `PriceCents` (> 0), `Units` (sellable, >= 0).
- `VendorApiSale`: `BookedDay`, `Cents`, `UnitsBefore`, `UnitsAfter`, `Sequence`.
- `VendorApi`: `ContractVersion => 1`; `Revision`; `Read(VendorApiVehicle[] into, out int count)` (at most 64); `RequestOwner(owner, vehicleId, want, out reason)`; `Renew(owner, nativeDay, nativeMinute, out leaseUntilDay, out leaseUntilMinute)`; `ReleaseAll(owner)`; `Sell(owner, vehicleId, openingId, item, priceCents, nativeDay, nativeMinute, out VendorApiSale? sale, out bool uncertain, out reason)`.

Street Food's promises:

- `Read` copies a detached view, never changes state and does not allocate when `Revision` is unchanged. More than 64 vehicles returns `Refused` with count 0, never a truncated list.
- **Opening boundary.** `RequestOwner(true)` records the native hour of the call. Street Food grants only an opening that starts in a later native hour while the lease is valid. A repeated request keeps the first hour. An open opening never changes owner from Street Food to PC.
- **Handback.** `RequestOwner(false)` or lease expiry hands an open PC-owned opening back at once, and Street Food starts its own customers from that minute with a fresh queue. PC releases only after its own work for the vehicle has drained.
- **Lease.** `Renew` extends it to 60 native minutes after the given time and is saved in Street Food's save, so a reload without PC hands vehicles back within 60 game minutes. PC renews every 15 native minutes.
- While PC owns an opening, Street Food generates none of its own customers for it. Closing, sell-outs, licences, events, crew pay and health checks continue.
- `Sell` checks status, vehicle, opening, owner, lease, item, exact price, stock and ledger room, and refuses with a reason before touching anything. Otherwise it runs Street Food's own one-item sale (`SellItem` with the crew-batch flush) at the given native minute. An exception after the ledger record returns `uncertain = true`.

## Rules PC keeps

- **Clock.** The API carries native day and minute. Street Food's absolute minute is `(day - 1) * 1440 + minute` and PC's is `Day * 1440 + minute`, so neither absolute minute crosses the seam.
- **Money.** Whole cents (`long`). The resident pays the vehicle's price; Street Food books the same cents on the sale day and pays the player at midnight through its own `PayIn`. PC never credits the player.
- **No private writes.** PC never reflects into Street Food private members or edits its state, cargo, ledgers or money outside `VendorApi`, as a fallback or otherwise.
- **Uncertain is final.** An `uncertain` result, an exception or a mismatch in cents, day, product or units keeps the pending receipt and is never retried. Only a confirmed pre-effect refusal cancels the receipt.
- **Whole vehicle.** PC owns an opening only if every menu item is admitted as Food with a positive price and the menu has 1 to 8 items. Otherwise the vehicle stays with Street Food and the reason is shown. A stand move waits until no resident is committed to the old stand; those residents find it closed and keep their money.
- **Missing API.** Without Street Food, or with no API or another contract version, PC runs as before, serves no vehicle and reports why. Saved vendor history loads as facts and the vehicles stay unserviceable.

## Defaults (6 November, Peter can overrule)

1. Residents replace Street Food's own customers at a PC-owned opening; they do not come on top.
2. Openings the player starts and works stay with Street Food. A player who joins a crew window keeps it PC-owned.
3. Drinks follow the E5 rule: food by default; a config `Drink` override keeps the whole vehicle with Street Food.
4. City-event and catering openings stay with Street Food.
5. Street Food reputation and health grades have no extra effect on residents.

## Accepted costs

- If PC owns an opening and stops renewing, Street Food has no customers there until the lease runs out (at most 60 game minutes). This is the price of never counting demand twice.
- A vehicle that keeps moving while residents are committed can wait a few game hours before PC serves its new stand.

## Proof still needed

All of it waits on E6a. The 13 in-game checks (contract present, missing and version 2; ownership at an opening; exact-money sale; choice next to shops and rivals; stand move; handback mid-opening; lease without PC; player-opened windows; uncertain sale; modded products; bodies at the hatch; refusal; performance window) and the rig capabilities they need (RIG-SFAPI, RIG-VLEDGER, RIG-SFFAULT) are in the [rig checklist](../rig-checklist.md). The detailed steps are in [section 7 of the snapshot](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/e6-carts-food-trucks.md#7-in-game-checks-for-the-batched-rig). Check 12 there predates the rule that holds the schema at 1 and is restated below. The other checks keep their snapshot steps.

**Check 12, restated (owner decision, 25 November 2026).** In-game: with the contract-2 Street Food set (the RIG-SFAPI `sf-contract-2` pin), Persistent City attaches to a schema-1 FT city, refuses the Vendor API with `Street Food VendorApi: unsupported contract 2; Persistent City reads contract 1.`, keeps the vendor table empty with no vendor purchases or receipts, and Street Food still serves its own customers. Mac rig 17w ran this arm and it passed ([25f check 12](../rig-checklist.md#e6-check-12-older-city-refusal-and-contract-2-control), #376), so check 12 is complete. RIG-SFAPI needs nothing more for it.

The snapshot's historical sub-arm (load an FT city written by writer 25 or 26 and expect "start a new city" with the payload unchanged) is retired, not deferred. Persistent City has never shipped and the schema is held at 1, so no real city carries writer 25 or 26. A genuine fixture would need an older RLO staged, which the no-RLO-changes rule forbids. The SaveCodec rule stays: a city written by another development schema is refused with "start a new city", without migration, and its payload is kept. Offline tests cover it:

- Schema refusal: `VendorSaveTests.AnOlderDevelopmentCityIsRefusedWithTheNewCityStatusAndKept`, `WorkplaceCapacityTests.AnotherSchemaNumberIsRefusedWithTheStartANewCityStatusAndKept`, `BusinessTypesWalletTests.ACityWrittenBeforeTheWalletMoveIsRefusedWithStartANewCityAndKept`, `WireHeaderTests.ProbeRefusesDuplicateNoncanonicalLegacyOrFutureHeader`, `WireHeaderTests.ProbeSupportsPrettyHeadersAndAllPublishedLegacySchemas` (schemas 0, 2, 13, 25, 29, 36 and 37) and `VendorSaveTests.AVendorTableIsRefusedWhenItIsNotAnArrayOrHasAnotherVersion`.
- Contract-2 refusal: `StreetFoodVendorTests.ContractMismatchRefusesBinding`, `StreetFoodVendorTests.ARunningContractChangeIsRefused` and `StreetFoodVendorTests.MissingOrUnsupportedApiRefusesVehicleScopesAndLeavesShopsUntouched`.
