# Same-visit meal extras, 8 November 2026

Peter decided (via Spock, 8 Nov ~14:26 Europe/Berlin) to implement multi-item meals and then re-run F6h. His words: "it should be mostly snack like items, but in rare cases a 2nd main dish", "Prefer cheaper items as extras; a second similarly priced dish is uncommon", and "As many extras as they like and can afford, with a safety cap of 3." Extras come from the same shop's actual stock. Nothing is guessed from product names. Refs #93.

A meal is now one main dish plus up to three optional extras from the same shop, in the same visit. Extras create no second appetite, journey or service slot. Bundles sold as one SKU stay deferred, as in the [E5 plan](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/plans/e5-dynamic-products.md).

## Main dish

The F6i formula is unchanged: each offer's weight uses `min(4, ($12.50/main price)^(2 × FoodChoicePriceWeight × sensitivity))`, and `Select` refuses a main above the allowance. `FoodChoicePriceWeight`, `FoodChoiceHouseholdWeight` and the F6h pass criterion are untouched.

## Shop expense is the whole ticket

Review fix Sol-r1-1 (also Astra-r1-1, Opus-r1-1). While evaluating each candidate main, the decision plans that candidate's ticket from the shop's captured menu with the same policy checkout uses. The candidate's weight is then also multiplied by

```text
ticketFactor = (main / ticket) ^ (2 × FoodChoicePriceWeight × sensitivity)
```

That is 1 without extras and never above 1, so a cheap side can only make a shop dearer, never a better meal deal. Combined with the main's factor it equals `($12.50 / ticket)` at the same sensitivity, without the discount reward. Two otherwise equal mains whose planned tickets cost different amounts now get different weights. A whole-menu markup leaves `main/ticket` unchanged, so only the F6i main factor moves; a markup on extras alone lowers the weight. Price weight 0 disables this signal along with the main's. The held decision stores only its result, as before; on resume it uses the captured menu and wallet.

One consequence is worth knowing before reading the F6h rerun. A resident who would add extras at a shop sees that shop as dearer, so some of them choose a cheaper main or a shop without extras instead. Realised extras per meal are therefore lower than the policy rates below, which apply once a main is chosen.

## Extra rule

[`FoodMealExtras`](../../Core/FoodMealExtras.cs) runs once the main is known:

1. **Candidates.** Every other item on that shop's current menu that already passes the sale contract: in scope, a known product, a positive price, and (for a player shop) at least one sellable unit after other committed meals' reservations. Modeled rivals publish no stock, so every advertised known product counts. Nothing is invented and no name, display string or meal class is read. A drink-classed product can be an extra if the shop stocks it; it still cannot be a main.
2. **Price band, relative to the main.** At most 75% of the main's price is *cheaper* (snack-like). Above 75% and up to 125% is a *similarly priced* second dish. Above 125% is never an extra. The comparison is exact integer money.
3. **Liking.** One deterministic draw per (seed, consumer, day, meal, shop, main, item). A neutral-taste resident wants a cheaper item with chance **0.35** and a similarly priced one with chance **0.05**, 7× rarer. Taste scales the chance by the existing 0.5–2 taste ratios, capped at 0.9. Price inside a band does not matter: a $0.50 side and a $9 side under a $12.50 main get the same chance, so there is no bargain reward.
4. **Order.** Liked items go cheaper band first, then by a taste-weighted random rank (ties by product ID, which only breaks exact equal draws).
5. **Wallet.** Items are added in that order while main plus extras stays within the same limit used for the main: `HouseholdFinance.SpendingLimit` with a budget, otherwise the remaining daily allowance. An item that would exceed it is skipped and the next is tried. At most three extras.

Why these numbers. The retained native prices are $9.24/$10.25 croissants, $12.50 burgers and $16.69 salad ([F6i](2026-11-08-price-anchor.md)). A $9.24 croissant is 74% of a $12.50 burger, so it is a snack-like extra to a burger; a $16.69 salad (134%) is never an extra to a burger, while a $12.50 burger next to a $10.25 croissant main (122%) is the rare second dish. With 0.35 per cheaper item a neutral resident at a shop with three cheaper items adds about one extra per meal; the measured rates are in [the F6j performance record](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/perf/f6j.md).

The band edge is a step (review note Opus-r1-2). Vanilla food shops mostly sell full dishes, so many "cheaper" extras at native prices are smaller second dishes: a $9.24 croissant next to a $12.50 burger is wanted 35% of the time, but repriced to $9.38 (75.04%) it drops to 5%. On the native-price menu in the performance record ($9.24 and $5 cheaper, $10.25 similar, next to a $12.50 burger) a chosen main carries 0.755 extras, so the expected ticket is about +44% over the main alone (0.35 × $9.24 + 0.05 × $10.25 + 0.35 × $5 ≈ $5.49 on $12.50), before the choice effect above. A single-item price edit near 75% of a main moves that item's uptake sevenfold. A whole-menu markup keeps every ratio and does not cross the edge. The step is kept because it is what the decision asked for (cheaper items common, similar ones uncommon) and it is easy to test; a taper between 50% and 100% would be a new owner decision.

Because the bands depend only on price ratios, a whole-menu markup keeps the same extras and marks the whole ticket up by the same factor. A +50% menu gives a +50% ticket for a resident who can still afford it.

## Ticket, wallet and checkout

The ticket is main plus extras. It is what is held, charged, booked as spend and revenue, and what F6h measures. Market history counts every sold line as one item purchase (review note Opus-r1-3: the management panel's weekly "Purchases" figure therefore counts items from F6j on; relabelling it is a UI change left to the coordinator), so the saved shop-day, product, hour, meal and buyer-district counters keep reconciling. A resident's own `Purchases` count stays one per meal.

A committed meal saves only its main, as before. Checkout plans the extras again from the live menu, stock, prices and wallet with the same deterministic draw, then holds the whole ticket. A held decision therefore reloads unchanged, and a committed ticket never includes an extra that became unaffordable or unavailable. While terms are unchanged the checkout ticket equals the candidate ticket used at choice time: both use known, in-scope products and stock net of other committed meals' reservations (review note Opus-r1-4). An observed decision reports every candidate's ticket.

Player extras sell through the existing native checkout, never as modeled sales. The native market sells each extra as one more one-product customer order, with the same price-row, menu, bag-policy and stock/bag/order delta checks as the main dish, after the main is confirmed. All lines share the intention's one receipt. The receipt text gains `|cents:product` fields for the planned or sold extras, and its leading price is then the ticket total; a one-product receipt is byte-for-byte unchanged. The first extra refused before any native effect stops the extras and the resident is refunded for unsold ones. An uncertain line leaves the whole meal uncertain with its planned total held and never retries, as a one-product checkout does today. If the receipt was confirmed but the acknowledgement or population update was lost (review fix Sol-r1-3), reconciliation reads the receipt's sold total and lines: it must name this intention, business and main, and its total may not exceed the held planned total. The unsold part is returned, the sold total is booked, and every sold line is recorded once, with no checkout replay. A one-entry order per line reuses the contract the rig has already exercised; a single multi-entry native order would need unverified `ProcessCustomer` behaviour, so it is not used. The cost is one bag and one completed native order per line rather than per visit; the rig checks measure it.

The Street Food vehicle market has no extras checkout yet, so vehicle meals stay main-only. Modeled rival tickets include extras from the advertised menu, paid through the existing outside settlement on the total.

## Schema

None. Writer 33 and its golden hashes are unchanged: no `CityState` field, wire shape or persisted setting is added, and extras are never saved. The native receipt is kept in game save data, outside the population schema. An older build reading a multi-line receipt sees conflicting evidence and refuses to retry, which is the safe direction.
