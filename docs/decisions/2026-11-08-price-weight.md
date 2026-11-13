# Food-choice price weight 1.2, 8 November 2026

Peter decided this on 8 November 2026 after the [F6h rerun](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/f6h.md#acceptance-rerun-after-f6j-8-november-2026): "Let's raise the weight slightly for price sensitivity, but let's do finetuning closer to release." Refs #93. The other decisions in force are in the [register](2026-11-10-city-direction.md).

## Decision

- The default `FoodChoicePriceWeight` is **1.2**: the compiled default in [FoodChoiceSettings](../../Core/FoodChoiceSettings.cs), its NaN fallback, and the shipped [config/persistent-city.json](../../config/persistent-city.json). The clamp stays 0–3.
- A user config that sets the key explicitly keeps its own value. A held decision keeps the tuning it captured until it resolves.
- Fine-tuning waits until closer to release.
- Nothing else moves: the household weight, the $12.50 anchor, the meal extras and the wallet gate are unchanged.

## Acceptance criterion

A markup can raise revenue and still cost demand: +50% on the whole menu that loses about 27% of meals while revenue rises about 11% counts as costing demand. Profit non-dominance and an elasticity beyond −1 are not release or acceptance gates.
