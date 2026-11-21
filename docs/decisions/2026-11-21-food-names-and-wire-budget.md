# Food name table and wire budget accounting, 21 November 2026

Recorded on 21 November 2026 from code comments that the comment cleanup removed (Peter, 20 November 2026). The other decisions in force are in the [register](2026-11-10-city-direction.md).

## Decision

- `FoodReferencedNames` ([CompleteFoodMenu](../../Core/CompleteFoodMenu.cs)) is a superset of the city's referenced product-name table, kept by the shop market. It is seeded once from the loaded food state and economy ledger, from the same sources the save writer uses, and grows with every product a scope adopts. Every name the save can reference enters through a scope's products, so the set never undercounts. A retired name only makes the preflight stricter until reload.
- A complete save rebuilds every wire-local name reference together ([NameWire25](../../Persistence/NameWire25.cs)). The in-memory graph holds names only; catalogue removal and order are not inputs to the referenced-name list.
- Wire budgets count conservative wire units, not managed heap size. Strings charge escaped JSON and UTF-8 separately, plus a fixed allowance for keys, separators and bookkeeping (64 units per string and 128 per claim in [FoodProductMetadataRegistry](../../Core/FoodProductMetadataRegistry.cs), 3 per token in [FoodProductOverrideSettings](../../Runtime/FoodProductOverrideSettings.cs)). Claims are charged even when fields are absent. No edit serializes or scans the whole registry, and the override reader keeps no second table and no source whitespace.
- The F6d size measurement ([F6dMeasure](../../tests/F6dMeasure.cs)) keeps the full outstanding E5d reserve and its 12 MiB growth allowance, even though the measured image already contains places, scopes and full histories.
