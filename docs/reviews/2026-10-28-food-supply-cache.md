# Food supply cache and batched budget postings, 28 October 2026

This implements finding 7 of the [27 October architecture review](2026-10-27-architecture-performance-review.md#7-the-food-layer-creates-and-destroys-unity-objects-per-decision). Three changes, all in development source: a per-minute supply cache on the meal decision path, memoized place IDs for shop discovery, and one food event per midnight for household budget postings. No game was launched. Save schema stays 13, no serialized field changed, and no policy version moved.

## What changed

`Runtime/NativeFoodMarket.cs`

- `ReadSelected` now asks `DecisionMenu` for supply when it serves a decision read (`Read`, `ReadBusiness`, `ReadLocal`). Checkout calls `ReadSelected` with its `observed` dictionary, and that path still calls `FoodSupplyReader.ReadMenu` directly, as before.
- `Buy` is a thin wrapper around the old body, renamed `BuyLive`. Its `finally` drops the cached image for the meal's place whatever the outcome: refused, paid, or thrown as uncertain.
- New counters `DecisionSupplyHits` and `DecisionSupplyMisses` sit next to `CheckoutSupplyReuses`.
- `RefreshDiscovery` still walks 64 controller slots on every call, as before. Place IDs are now memoized per controller slot and rebuilt only when that slot's registration object, street name reference or street number changes. An internal `DiscoveryPlaceIdBuilds` counter records each rebuild for the harness.

`Runtime/FoodSupplyReader.cs` is unchanged. `ReadMenu` creates, sets up and destroys its simulator exactly as before.

`Core/FoodFinances.cs`

- `StepBudget` posts every budget whose `NextPostingMinute` equals the first due budget's minute, in `BudgetOrder`, inside one `Step` call.
- An internal `BudgetPostingsPerEvent` (default unbounded) caps the batch. The differential test sets it to 1, which runs the earlier one-posting-per-event code path exactly. Play never sets it.

`Core/HouseholdFinance.cs` and `Core/FoodCommerce.cs` are unchanged.

New tests and harness checks:

- `tests/FoodBudgetBatchTests.cs`, seven cases (four inline data rows of one theory plus three facts).
- `tools/food-checkout-regression/Program.cs`, five new scenarios: shared reads within a minute, the stale image never reaching checkout, every invalidation, an eight-shop workload, and discovery without rebuilding place IDs.

## The decision cache

### Key

An entry is per place ID. A read hits only if all of these match the entry:

- the save. `ReadSelected` throws before any supply read when `SaveGameManager.Current` is not the market's save, and a new save gets a new market, so a save change can never reach a cached entry.
- the native game minute, the same `observationMinute` (`Day * 1440 + Hour * 60 + Minute`, floored) that `ReadSelected` already uses for its ownership refresh. A new minute clears the whole cache.
- the food authority payload, by reference. `NativeFoodOwnership.Persist` writes a freshly serialized string to `modData["persistentcity:food-authority-v1"]` on every authority change, including registration resets, business replacements and boundary transfers. A different string object clears the whole cache, even if its text is equal.
- the `BuildingRegistration` object, by reference.
- the business ID `ReadSelected` resolved for this read (the authority's generation-stamped ID).
- the ordered product list from the scope.
- the four registration fields the reader branches on before setup: `RentedByPlayer`, `temporarilyClosed`, `HasEstablishedBusiness` and `businessTypeName`.

### Invalidations

| Event | Effect |
| --- | --- |
| Any `Buy` for a place, refused, paid or thrown | that place's entry removed in `finally` |
| `RefreshKnownOwnership` (start of each new minute, each native hour boundary, before save) | every refreshed place's entry removed |
| New game minute | whole cache cleared |
| Authority payload rewritten (`Observe` with a replacement or changed signature, `BuildingRegistration.Reset`, boundary transfer) | whole cache cleared |
| Replaced registration object, changed business ID or menu | entry does not match; read live and replaced |
| Changed owner flag, temporary closure, establishment or business type | entry does not match; read live and replaced |
| Save change | market refuses the read; a new market starts empty |

### Why the cache cannot book a sale on stale stock

A decision image can be up to one game minute old. Native changes the mod does not make, like the player restocking a shelf or a staff member leaving, show up at the next minute rather than immediately. So a resident who decides in the same native minute as a player restock, staffing change or bag change sees the earlier image, and may skip a shop the live read would have offered. The same holds for the game's own writers: the distributed hourly processing of a mod-owned shop still runs its restock tasks (`HourlyCustomers` suppresses only the customer part), so in the first minutes of a native hour a cached image can lag a native restock or a native stock draw by up to one minute, in either direction. A stale image that still shows stock sends the resident to a shop whose checkout then refuses live. This is the stated relaxation of the cache: a decision image is at most one native minute old against any native writer. Checkout stays live. That only affects which shop a resident picks, or whether an arrival sees service. It never authorizes a sale. `Buy` keeps its 27 October semantics exactly: a live selected read inside the call, reuse of that read only for the same registration, menu and exact game minute, then the native sale and a live after image. The checkout harness has a case that empties the shelf behind a cached image: the decision read still shows one croissant, and `Buy` refuses with "Stock or staffing changed", writes no receipt and uses no bag.

The authority-payload term is there because `NativeFoodOwnership.DrainOldWork` (on replacement and on `BuildingRegistration.Reset`) runs native `ForceCompleteAllWork`, which can restock shelves mid-minute. Every path that drains also persists the payload, so the cache is cleared. The only other drain, in `AfterNativeTick`, happens after the native hour advances, so the minute key already differs.

### Shop discovery

`PersistentCityMod.Update` calls `RefreshDiscovery` every frame, and it still runs `DiscoverShops(64)` on every call. Before, each call built 64 escaped place-ID strings. Now a slot's string is built once and reused until that slot's registration object, street name reference or street number changes, so a walk over unchanged addresses is field reads, reference comparisons and dictionary lookups.

A first version of this change ran discovery once per second over half the ring. An independent reviewer found that this moved when `FoodAuthority.RequestedHour` is first recorded. A business that becomes eligible in the last two seconds before a native hour boundary would be first observed at H+1, and the `hour > RequestedHour` rule in `FoodAuthority.Boundary` would then acquire it at H+2 instead of H+1. With the strings memoized, the cadence saved little, so the per-frame walk is back and observation timing matches the code before this change.

## Batched budget postings

`PostDay` moves every posted budget to `(day + 1) * 1440`, so at a midnight all budgets are due at the same minute. Before, each posting was one food event. With 1,914 budgets and the 128-event frame cap, a midnight took about 15 frames.

The batch keeps the order and the position of every posting:

1. `CitySimulator.AdvanceCore` runs a food event at minute M only when the food minute is strictly earlier than the next citizen and traffic events. Citizen and traffic events at M always run first. That is untouched.
2. `StepBudget` still runs only when the earliest budget is due no later than the earliest meal, so budgets at M still precede meals at M.
3. Inside the batch, budgets come off `budgetDue.Min` in `BudgetOrder` (posting minute, then resident ID), the same order as before. A posted budget returns with tomorrow's minute, so the loop cannot post it twice, and stops at the first budget due later.
4. Between two old per-budget events at M, nothing else could run. Citizen and traffic queues are after M, and `PostDay` does not touch them. The only difference was where a frame boundary fell, and frames do not change simulation-time order.

A budget created at a fractional minute (a resident enrolled at 1440.5, say) has a different posting minute and does not join a batch. One test covers this.

`FoodBudgetBatchTests` checks:

- **Byte-identical saves.** 60 residents, three meals a day, one deliberately uncertain checkout and half the residents with a citizen event exactly at midnight. The batch and the per-budget reference run for six days with event budgets 1, 16, 128 and 10,000. `SaveCodec.Write` output matches at the end of every day and at the end, including across a reload on day 3. Native orders (intent and minute) match too.
- **Same order of observable events.** The test records every citizen event and every market call (read, buy, receipt evidence) with the clock and the number of budgets already posted for that day. The two traces are identical.
- **Event counts.** The batch makes exactly 59 fewer events at each of the six posting minutes: 354 in total.
- **Order at midnight.** All 30 midnight citizen events see 0 budgets posted. The uncertain meal's evidence check at midnight, and every other food call at that minute, sees all 60.
- **Old saves.** A save written by per-budget stepping part-way through a midnight, loaded under the batch, finishes byte-identical to the uninterrupted per-budget run.

Two mutations confirm that the tests catch a wrong batch. Letting a meal run in the same event as the batch fails all four differential rows. Letting the batch reach budgets due in the next minute fails the fractional-minute test.

Spreading postings across the hour was not needed, and was not done.

## Verification

All runs used the shared working tree, which includes the other agent's concurrent Core changes. The machine load average was above 100 during the test run.

| Check | Result |
| --- | --- |
| `dotnet test tests/PersistentCity.Tests.csproj --configuration Release` | First run: 1,241 passed (1,234 existing plus 7 new). After the discovery revert, on the tree as it then stood: 1,259 passed, 0 failed, 0 skipped, 7 of them new |
| `tools/food-checkout-regression` (actual `NativeFoodMarket`, `NativeFoodHours`, `FoodSupplyReader` source) | 219 checks passed after the discovery revert, was 54; the 54 existing checks are unchanged, including 3 native setups and 2 reader setups per successful checkout |
| `tools/food-supply-regression` | 19 checks passed |
| `tools/food-catalogue-regression` | 29 checks passed |
| `tools/food-hours-regression` | passed |
| `tools/food-household-catalogue-regression` | 11 checks passed |
| `tools/food-ownership-regression` | 78 checks passed |
| `tools/food-demand`, `tools/food-choice-signals`, `tools/food-routines` (957 residents) | completed, outputs written to a scratch folder |
| `tools/food-menus/replay` on `build/food-menus-mac-r5-20261023` | completed, `exactBaselineReplay: true` |
| `tools/food-demand-audit/replay --scale` | completed; order hashes identical with and without pruning at 1,000, 2,000 and 5,000 residents (no household finances in this replay) |
| `tools/food-demand-curve`, `tools/food-native-pilot` replay projects | both build with 0 warnings and 0 errors; they need rig evidence to run |
| `tools/food-household-comparison` | fails before any changed code runs: `FoodShadowCodec.Read` rejects the 22 October shadow document as a wrong shape. Neither the codec nor the shadow model is in this change |
| `tools/walking-quote-regression` | 28 plus 48 checks passed; the three-day K=2 replay, which enables household finances, still reports 545 / 489 path queries, 498 quote checks, 24 rejections and identical purchases |
| `python3 tools/build.py` against the installed Mac game assemblies | passed with warnings as errors; DLL SHA-256 `f11497adad25e9d061e5e5ae42259c2d1ac9a74dceb4db3b75db5ad1041c70f2`. Compilation only, nothing installed |

`tools/simulate` has no food code, so it was not run. The rig scripts under `tools/food-*` (`pc.py`, `run_mac.py`, `session.py` and the rest) need an owned rig and were not run. No food harness extracts production source by text markers; the three that link production files (`food-checkout`, `food-supply`, `food-catalogue`) compile them whole.

The eight-shop workload in the checkout harness runs 123 decision reads over three game minutes, with one checkout per minute:

| | Supply-reader setups |
| --- | --- |
| Without the cache | 990 (984 decision, 6 checkout) |
| With the cache | 33 (27 decision misses, 6 checkout) |

Decision cache: 957 hits, 27 misses. That is eight misses at each new minute plus one after each checkout. Checkout setups are the same in both rows.

Seven mutations of `NativeFoodMarket.cs` were run against the checkout harness. Removing the Buy invalidation, the ownership-refresh invalidation, the authority-payload term or the closure field each fails a named check. Letting checkout use the cache throws the uncertain-outcome exception. A stale place-ID memo fails the new-address check, and a memo that never hits fails the no-rebuild check.

## What is not verified

- No game was launched. Every native claim about the cache is argued from the mod source and the native behaviour the 27 October reviewers recorded, not observed. The key assumes that native code changes stock, staff and bags within one game minute only through paths the mod does not own (player actions, which can be up to a minute stale on the decision path only) or through `ForceCompleteAllWork` calls that the mod pairs with an authority write or an hour change. A native restock that runs mid-minute without either, which the distributed hourly processing does in the first minutes of an hour, leaves a decision image stale until the next minute; an independent reviewer confirmed that path from the ownership source. Checkout still reads live. If that lag ever matters, the cache can skip the first native minute after an hour boundary, or drop a place's entry from a postfix on the per-registration hourly work path the ownership hooks already patch.
- `HasEstablishedBusiness`, `RentedByPlayer`, `temporarilyClosed` and `businessTypeName` were compiled against the installed game assemblies but not exercised in Unity.
- The discovery memo assumes that `StreetName` returns the same string object while the address is unchanged. If it returns a new string each time, the memo misses and rebuilds the ID, as before. That is slower but still correct.
- The new counters are not in `PersistentCityMod.FoodDiagnostics` or the 60-second summary, because those live in a file outside this change set.
- No frame-time, allocation or setup-count measurement was taken in native play. The harness counts below come from synthetic native surfaces.
