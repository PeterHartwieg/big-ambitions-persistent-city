# Food-choice price anchor, 8 November 2026

Peter selected option 1 after [F6h stopped](../checks/f6h.md): price needs a meal-scale anchor so a monopoly cannot raise its whole menu heavily without a demand penalty. Customers compare taste, travel and price; the hard wallet affordability check stays. Refs #93.

## Implemented rule

[FoodChoicePolicy](../../Core/FoodChoicePolicy.cs) multiplies each offer's existing taste/travel/queue/quality/time/meal-fit weight by:

```text
reference = $5
sensitivity = 0.5 + 0.25 * PriceSensitivityBand   // bands 0..4 => 0.5..1.5
priceFactor = exp(-2 * FoodChoicePriceWeight * sensitivity * (price / reference - 1))
```

The common $5 reference is a typical prepared-meal design anchor: the existing detached fixture has $3 croissants, $5 hot dogs and $8 burgers. It is independent of savings, current shop prices and rival availability, so a sole shop cannot move the anchor by marking up its menu. This adds no product coefficient or setting. The $5 outside-retail charge is a separate, unchanged F6f rule.

The exponential expresses the cost of an additional dollar relative to a meal. It equals 1 at reference, rewards cheaper offers and stays positive for affordable offers. Unlike the previous savings denominator, it gives rich and poor customers the same price signal when both can afford an offer. No additional wallet-pressure term is needed. The existing `FoodChoicePriceWeight` remains clamped to 0–3, default 1; zero disables only this soft signal.

Default strength 2 makes a neutral customer's $5 → $7.50 price factor 1 → 0.368. The markup/reference ratio ranges from 0.607 to 0.223 across all price-sensitivity bands. These factors compete with taste's 0.5–2 ratios and a neutral customer's ten-minute travel factor of 0.5. Taste and proximity can therefore still win a comparison. At the reference, the local own-weight price elasticity is −2 for neutral sensitivity; this is a policy property, not measured market elasticity. Existing temperature, weight floor, memory and alternative weighting still apply; the floor bounds extreme penalties.

Every offer, including rival and metadata-backed products, uses the same reference. Relative comparisons follow directly. A funded household alternative is the **reference-price equivalent** outside option: its price factor is 1 and `FoodChoiceHouseholdWeight` scales its preference/convenience weight (default 1, existing 0.1–5 clamp). This does not estimate the price of cooking or debit home food again. A markup reduces the shop's weight relative to this fixed outside option even when no other shop exists. Without funded home food or another feasible offer, the sole affordable shop still wins the draw; lowering its weight does not invent food or an outside option.

## Affordability, persistence and scope

`Select` skips `price <= 0` and `price > allowance` before scoring. `HouseholdFinance.SpendingLimit` and `HoldPurchase` are unchanged: next-day basics remain reserved, savings available for a purchase cap at $10,000, and a hold or arrears gives a zero limit. Tight wallets still feel the anchored price penalty on affordable meals and refuse unaffordable meals.

Writer **33** and both current-writer golden hashes stay unchanged. No persisted setting, wire field, policy key, historical accounting or switch-over day changes. New choices use the rule; selected commitments retain their saved price and do not draw again. Existing reload/interrupted-checkout tests cover that boundary.

The defaults stay 2 estimated rival lanes, 60 estimated units/period, five-minute service, missed-work k=0.5 and C9 retail $5/leisure $12. No wallet exhaustion is used to produce the price effect. Product coefficients, player UI and other slices are unchanged. The [short offline evidence](../perf/f6i.md) verifies demand direction; it does not complete the stopped F6h multi-week acceptance/calibration.
