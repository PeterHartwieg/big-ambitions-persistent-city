Status: implemented (offline); not verified by rig

Refs #93. Task `pc-f6j-multi-item-meals`, base `codex/persistent-city` at `1c295905` (F2b #151, includes F6i #150 and the F6h rerun #153). Writer 33, no schema change. Rule and rationale: [multi-item meals decision](../decisions/2026-11-08-multi-item-meals.md). Numbers: [performance](../perf/f6j.md).

## What changed

- `Core/FoodMealExtras.cs`: the pure extras policy. Price bands relative to the main (≤75% cheaper, ≤125% similar, otherwise excluded), neutral chances 0.35 / 0.05 scaled by taste, cheaper band first, wallet gate on the running total, cap 3. No name, class or $12.50 reference is read.
- `Core/FoodMealTickets.cs`, `FoodCommerce.cs`: checkout plans the ticket from the live menu and wallet, holds the total, books only the lines the seller confirms and returns the rest. New `IFoodTicketMarket`; `FoodSale.SoldExtras`. Sellers without the interface sell the main only.
- `Core/RivalFood.cs`: modeled rival tickets add extras from the advertised menu, paid on the total.
- `Core/FoodMarketHistory.cs`: every sold line is one item purchase in the shop-day row, so saved counters keep reconciling.
- `Core/HouseholdFinance.cs`: `ReleasePart` returns unsold extras from a held ticket.
- `Persistence/FoodSaleReceipts.cs`: a receipt may carry `|cents:product` extra fields after the main identity; one-product receipts are unchanged. Prune accepts confirmed ticket receipts.
- `Runtime/NativeFoodMarket.cs`, `FoodMarkets.cs`: extras sell as further one-product native orders, each with the main line's checks, under the meal's one receipt. Vehicles stay main-only.
- `tools/economy-interventions`: the F6h native double sells extras, so the rerun measures whole tickets.
- `Core/FoodChoicePolicy.cs` (review fix Sol-r1-1): a candidate's weight also multiplies by `(main/ticket)^(2 × priceWeight × sensitivity)`, using the ticket the decision plans from the captured menu. The F6i main factor, `FoodChoiceSettings`, both weights and the F6h criterion are untouched.
- Review fix Sol-r1-3: new `IFoodTicketEvidence` and `FoodSaleReceipts.TryReadPaidTicket` settle an uncertain meal from its confirmed receipt's sold total and lines (native market, `FoodMarkets`, the F6h double).

Re-verified at the base: `FoodChoicePolicy.PriceFactor`/`MealReferenceCents` ($12.50), `Select`'s nonpositive/over-limit skip, `HouseholdFinance.SpendingLimit`/`HoldPurchase`, `FoodSaleReceipts` v2 identity, `NativeFoodMarket.BuyLive` one-entry order checks, `ExternalFoodSettlement.TryPay`, and the saved market-history reconciliation in `SaveCodec.ValidateFood`.

## Offline validation

`F6jMealExtrasTests`, 27 passed:

| Requirement | Test |
| --- | --- |
| (a) cheaper preferred over similar | `CheaperExtraIsPreferredOverSimilarlyPricedOne` |
| (b) similar second dish uncommon | `SimilarlyPricedSecondDishIsUncommon`: 0.351 vs 0.051 over 20,000 consumers |
| (c) at most 3 extras | `NeverMoreThanThreeExtras` |
| (d) total within allowance, unaffordable skipped | `RunningTotalStaysWithinAllowanceAndUnaffordableExtrasAreSkipped`, `UnsoldExtrasAreReturnedAndOnlySoldLinesAreBooked` |
| (e) same shop's stock only | `ExtrasComeOnlyFromTheGivenStockedMenu`, `CheckoutSellsSameShopExtrasAndBooksTheWholeTicket` |
| (f) no name matching | `IdenticallyPricedItemsAreTreatedTheSameRegardlessOfName` |
| (g) no $12.50 bargain for extras | `ExtrasReceiveNoMealReferenceBargain` |
| (h) F6i main rule | `F6iPriceAnchorTests` unchanged and passing; `ObservedDecisionReportsCandidateTicketsWithoutChangingChoice` |
| (i) +50% menu marks up the ticket | `WholeMenuMarkupMarksUpTheWholeTicket`: same extras, total exactly ×1.5 |

Review round 1:

- **Sol-r1-1:** `TicketExpenseLowersAShopWeightButNotTheMainAnchor` (exact factor, +50% menu leaves it unchanged, price weight 0 disables it) and `CandidateTicketsEnterTheShopChoice` (two equal shops; only one sells extras, and fewer residents choose its croissant).
- **Sol-r1-2:** `RefusedCheckoutAfterTheLimitFallsStillSaves` and `RefusedRivalSettlementAfterTheLimitFallsStillSaves` lower the wallet limit below a committed main with cash conserved, then save. Both fail on the previous code (`Price ≤ Allowance` broken) and pass now: checkout and rival ticket planning read the limit without overwriting `Allowance`.
- **Sol-r1-3:** `ConfirmedReceiptSettlesTheSoldTicketAfterReload` (fully and partly sold tickets, with and without household budgets) confirms the real receipt format, loses the acknowledgement, reloads, and checks the held money is released down to the sold total, every sold line is recorded once and no order repeats. `PaidTicketEvidenceIsReadOnlyForItsOwnIntentionAndHeldTotal` covers the parser.
- **Sol-r2-1 (also Astra-r2-1, Opus-r2-1):** a ticket held or settled after the wallet limit rose above the saved `Allowance` now records the limit it was planned within (`Allowance = max(saved, current limit)`, never lowered, so refusals keep their saved terms). `LargerTicketAfterTheLimitRisesStillSaves` (paid and uncertain player checkouts) and `LargerRivalTicketAfterTheLimitRisesStillSaves` fail on the previous code and pass now.
- **Opus-r1-4:** candidate and checkout tickets share one filter (known, in-scope, stock net of other meals' reservations).

Also covered: uncertain tickets keep their planned total held and are never replayed; a reload every 37 minutes through a policy day gives identical lines and save bytes; receipt identity, partial confirmation and prune.

Full suite after review round 2: **3,937 passed**, 0 failed. Both writer-33 golden pins unchanged. Build passed.

One existing expectation changed: `EconomyInterventionTests.CatchmentCountsOnlyMealsWhoseDecisionEvaluatedTheShopAndSurvivesReload` required shop-day purchases to equal meals choosing the shop. Shop-day purchases now count sold items, so the test checks `meals ≤ items ≤ 4 × meals`. No other test or hash changed.

Process note: one full test run started while another worktree's `dotnet test` was still running (my `pgrep` check matched only its shell). That run had one failure, the expectation above. The final run, reported here, started with no other test process.

## Notes for the F6h rerun

A resident who would add extras sees that shop as dearer (Sol-r1-1), so realised extras per meal are below the policy rates. The band edge at 75% is a step; at native prices a chosen $12.50 burger main expects about +44% in extras (Opus-r1-2, [decision](../decisions/2026-11-08-multi-item-meals.md)).

Known UX change for the coordinator (Opus-r1-3): the management panel's weekly "Purchases" figures (`FoodFeedback`, `FoodFeedbackV2`) read shop-day purchases, which now count items, so they rise up to fourfold per meal from F6j on and week-over-week comparisons across the upgrade jump. Relabelling to "Items sold" is a UI change outside this slice. Native completed orders and bags also count per line; rig check 1 measures them.

F6h's own-shop quantity reads shop-day purchases, which now count items rather than visits. Revenue is the whole ticket. A resident's `Purchases` stays one per meal.

## In-game checks for the next batched rig

All checks are rig-automated by a freshly resolved `rig_test` session in named disposable FT cities, Mac preferred. They are batched native proof, not this slice's merge gate. Retain before → red / after → green logs or screenshots in the PR before release.

1. **Extras hit the register.** Setup: FT city, one owned food shop with a $12.50 main and two cheaper stocked items, ample staff and bags, funded wallets. Steps: run one lunch period; export receipts, native completed orders, per-product stock, bag stock, register revenue and population ledger. Expected: some meals carry 1–3 extras; every receipt's paid total equals main plus sold extras; each sold line lowers its product stock and bags by exactly one and adds one completed order; native daily revenue equals the population's booked ticket total; no extra appears on a receipt without a native order.
2. **Refusal and uncertainty.** Setup: as check 1. Steps: during service, change one extra's price, then empty its stock; separately interrupt native confirmation after the main line where the rig can do so without a code change, otherwise mark this half not exercised. Expected: a changed or empty extra stops further extras and the resident is refunded for them; the forced exception leaves the meal uncertain with its planned total held, the receipt pending and no second order after reload.
3. **Reload at every boundary.** Setup: as check 1, autosave off and verified before each step. Steps: save/reload with a held decision, a travelling meal, a serving meal, and an uncertain ticket. Expected: no rerolled main, no duplicate order, extras planned from then-current stock and wallet, receipts reconcile exactly once.
4. **Checkout cost.** Setup: populated FT city with an extras shop; record original autosave setting, disable it and check before every window, including after reload. Steps: time mod-work and whole frame separately across a lunch rush. Expected: mod-work p95 < 5 ms; every ≥ 30 ms sample flagged with its component; the per-checkout cost of up to four native orders reported. Restore autosave, rig files and preferences at cleanup.
5. **Vehicles and rivals.** Setup: FT city with a Street Food vehicle and a rival with a multi-item advertised menu. Steps: run one lunch. Expected: vehicle meals stay one product per receipt; rival tickets include extras, rival outflow equals the booked totals, and no rival finance is written.
