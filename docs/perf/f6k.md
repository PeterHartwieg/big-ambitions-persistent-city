# F6k performance and numbers

Refs #93. F6k changes one default (`FoodChoicePriceWeight` 1 → 1.2). It changes no code path: the same `Math.Pow` runs per scored offer with a different exponent. Mod-work cost is unchanged, so no timing was taken. No calibration campaign ran.

## Latest measurement (weight 1, not rerun)

From the authorized F6h rerun after F6j, PR #160 at `f8a7f5db`, [calibration record](f6h-calibration.md). Native prices, 6,000 meals/hour (capacity does not bind):

| Price weight | +50% menu: meals | Items | Revenue | Item arc elasticity | Certificate, 10 seed-weeks |
|---|---|---|---|---|---|
| 1 (old default) | −27.2% [−27.5, −27.0] | −26.5% | +10.8% [+10.1, +11.4] | −0.764 | positive in all |
| 1.5 (diagnostic) | −37.6% | −35.7% | −2.0% [−3.2, −0.8] | −1.087 | negative in all (−$5,176 to −$2,228) |
| 2 (diagnostic) | −45.0% | −42.0% | −10.6% | −1.329 | negative in all |

Unaffordable refusals 2.03% → 2.13% and extras per meal 0.131 → 0.142 at weight 1, so wallets are not the mechanism. Without the rival: −27.0% meals, +11.1% revenue.

## Policy effect at 1.2 (offline, exact)

A neutral resident's +50% weight ratio is (2/3)^2.4 = 0.378, down from 4/9 = 0.444. `F6kPriceWeightTests` checks that it is lower at 1.2 for bands 0, 2 and 4. In the sole-shop draw test (4,096 draws, neutral resident, funded home), the shop wins 2,036 at $12.50 and 1,114 at $18.75.

No native or offline multi-seed number exists at 1.2. Fine-tuning is deferred until closer to release ([decision](../decisions/2026-11-08-price-weight.md)).
