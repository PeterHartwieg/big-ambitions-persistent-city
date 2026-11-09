# Food-choice price weight 1.2, 8 November 2026

Peter decided this on 8 November 2026 (about 18:24 Europe/Berlin), after the [F6h rerun on the F6j base](../checks/f6h.md#acceptance-rerun-after-f6j-8-november-2026): "Let's raise the weight slightly for price sensitivity, but let's do finetuning closer to release." Refs #93.

## Decision

- The default `FoodChoicePriceWeight` goes from 1 to **1.2**. That covers the compiled default in [FoodChoiceSettings](../../Core/FoodChoiceSettings.cs), its NaN fallback, and the shipped [config/persistent-city.json](../../config/persistent-city.json). The clamp stays 0–3.
- Peter asked for a slight raise. He did not pick the measured diagnostic points 1.5 or 2, and Persistent City chose 1.2 as the modest step. At 1.5 the +50% whole-menu markup already loses revenue in every seed-week (−2.0%, 37.6% fewer meals); at 2 it loses 45% of meals. Both are well past "slightly".
- Fine-tuning the weight waits until closer to release. No measurement campaign runs now.
- Nothing else moves: `FoodChoiceHouseholdWeight` stays 1, the F6i $12.50 main-dish anchor, the F6j extras bands and chances, the wallet gate and every other F6 default are unchanged.

## What 1.2 does

The price exponent is `2 × weight × sensitivity`. A +50% offer keeps this share of its weight, bands 0–4:

| Weight | Band 0 | Band 1 | Band 2 (neutral) | Band 3 | Band 4 |
|---|---|---|---|---|---|
| 1 (F6i) | 0.667 | 0.544 | 0.444 | 0.363 | 0.296 |
| 1.2 (F6k) | 0.615 | 0.482 | 0.378 | 0.296 | 0.232 |

The discount ceiling of 4 is still inactive at the recorded native prices: band 4 at $9.24 reaches 2.97. A band-4 +50% offer at $16.69 keeps weight 0.082, above the 0.02 floor.

No native measurement exists at 1.2. Going by the [#160 numbers](../perf/f6k.md), it should fall between weight 1 (−27% meals, +11% revenue for +50%) and 1.5. This is an expectation, not a measured result.

## F6h acceptance

F6h no longer stops on profit non-dominance over the whole wholesale box. A +50% whole-menu markup that loses about 27% of meals while revenue rises about 11%, well under the 50% price rise, counts as costing demand. Neither an unprofitable markup nor an elasticity beyond −1 is a release or acceptance gate. The three options listed after F6i and F6j (raise the price weight to 1.5 or 2, raise the household weight, revise the criterion) are settled by this decision and are not raised again.

## Saves and existing cities

No schema change: writer 34, both golden hashes and the tuning wire are unchanged. The live choice settings are not stored per city. `PersistentCityMod` reads them from the installed `persistent-city.json` at every load, so a city loaded with the shipped file uses 1.2 for new decisions. A user file that sets the key explicitly keeps its own value. Held decisions carry the tuning image they captured; one captured at 1 keeps 1 until it resolves. Nothing rewrites a stored 1.
