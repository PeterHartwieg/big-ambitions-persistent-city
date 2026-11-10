# Food-choice price anchor, 8 November 2026

Peter selected option 1 after [F6h stopped](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f6h.md): price needs a meal-scale anchor so a monopoly cannot raise its whole menu heavily without a demand penalty. Customers compare taste, travel and price; the hard wallet affordability check stays. Refs #93.

## Implemented rule

[FoodChoicePolicy](../../Core/FoodChoicePolicy.cs) multiplies each offer's existing taste/travel/queue/quality/time/meal-fit weight by:

```text
reference = $12.50
sensitivity = 0.5 + 0.25 * PriceSensitivityBand   // bands 0..4 => 0.5..1.5
priceFactor = min(4, (reference / price) ^ (2 * FoodChoicePriceWeight * sensitivity))
```

Review correction **Opus-r1-1** replaces the initial $5 exponential rule from `c6a4796`. That anchor came from a cheap detached fixture and floored ordinary native-price offers. The retained native records instead show [croissants at $9.2448](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/reviews/2026-10-22-food-native-pilot.md), [croissants at $10.2528](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/reviews/2026-10-22-food-routine-sales.md), [burgers at $12.50](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/reviews/2026-10-23-food-menus.md), and [salad at $16.69](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/reviews/2026-10-22-food-household-budgets.md). The common **$12.50** reference sits within that observed range. It is a fixed typical-meal reference, not a product coefficient or a new config setting; savings, current menu edits and rival availability cannot move it. This implements the original typical-prepared-meal anchor option without adding a native catalogue dependency or a persisted first-observed price that a monopoly could initialize high.

The power portion has constant own-weight price elasticity: a proportional markup gives the same proportional penalty at any price level, before the existing bounds. At default price weight 1, elasticity is −2 for neutral sensitivity and ranges from −1 to −3 across the five sensitivity bands. This is a policy property, not measured market elasticity. A +50% markup retains **0.667, 0.544, 0.444, 0.363, 0.296** of the previous weight across bands 0–4. Taste's 0.5–2 ratios and a neutral customer's ten-minute travel factor of 0.5 remain comparable. A maximum discount reward of 4 prevents extremely cheap meals from saturating the final upper weight bound and hiding taste, travel or memory. This ceiling is inactive at all four recorded native price levels for every sensitivity band at default strength. Existing temperature, weight floor, memory and alternative weighting still apply; extreme offers can still reach those bounds.

At the $12.50 reference the price factor is exactly 1 at every sensitivity. Across the recorded $9.24–$16.69 price levels, neutral price factors are **1.830–0.561**, and band-4 factors are **2.476–0.420**, comparable to funded cooking's weight 1. At $16.69, a band-4 customer's +50% offer retains weight **0.124**, above the 0.02 floor even with the ordinary travel/time case in the regression test. The anchor is grounded in retained native price levels; baseline own/home shares at native prices still require the numbered rig check, rather than an assumption that a price-model change preserves demand.

Rich and poor customers receive the same price signal when both can afford an offer. No extra wallet-pressure term is needed. The existing `FoodChoicePriceWeight` remains clamped to 0–3, default 1; zero disables only the soft signal. For **Opus-r1-2**, the `Weight` overloads explicitly document that allowance is ignored and retained for caller compatibility; selection and checkout enforce affordability.

Every offer, including rival and metadata-backed products, uses the same reference. Relative comparisons follow directly. A funded household alternative is the **reference-price equivalent** outside option: its price factor is 1 and `FoodChoiceHouseholdWeight` scales its preference/convenience weight (default 1, existing 0.1–5 clamp). This does not estimate the price of cooking or debit home food again. A markup reduces the shop's weight relative to this fixed outside option even when no other shop exists. Without funded home food or another feasible offer, the sole affordable shop still wins the draw; lowering its weight does not invent food or an outside option.

## Affordability, persistence and scope

`Select` skips `price <= 0` and `price > allowance` before scoring. `HouseholdFinance.SpendingLimit` and `HoldPurchase` are unchanged: next-day basics remain reserved, savings available for a purchase cap at $10,000, and a hold or arrears gives a zero limit. Tight wallets still feel the anchored price penalty on affordable meals and refuse unaffordable meals.

Writer **33** and both current-writer golden hashes stay unchanged. No persisted setting, wire field, policy key, historical accounting or switch-over day changes. New choices use the rule; selected commitments retain their saved price and do not draw again. Existing reload/interrupted-checkout tests cover that boundary.

The defaults stay 2 estimated rival lanes, 60 estimated units/period, five-minute service, missed-work k=0.5 and C9 retail $5/leisure $12. No wallet exhaustion is used to produce the price effect. Product coefficients, player UI and other slices are unchanged. The [short offline evidence](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/perf/f6i.md) verifies demand direction; it does not complete the stopped F6h multi-week acceptance/calibration.
