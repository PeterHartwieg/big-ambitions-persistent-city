# Food ownership and contract evidence, 21 November 2026

Recorded on 21 November 2026 from code comments that the comment cleanup removed (Peter, 20 November 2026). The other decisions in force are in the [register](2026-11-10-city-direction.md).

## Decision

- Food ownership eligibility comes only from the reviewed menu and business contract. Operational outages (stock, staff) change offers, never ownership; the policy takes no stock or staff input ([FoodAuthorityTests](../../tests/FoodAuthorityTests.cs)).
- `ShopContractStamp` ([FoodContractStamps](../../Runtime/FoodContractStamps.cs)) is a structural stamp of one registration: menu, price and furniture collections by reference, version and count; business type by reference; four scalar flags. In-place writes to an element's fields (a price, a stock name, an amount) do not move it. `FoodContractRecheck` ([FoodContractState](../../Core/FoodContractState.cs)) checks content. A stamp mismatch dirties the shadow image, never ownership.
- A native reset replaces `retailPrices` even when it reuses the registration object, so the rival catalogue treats any price-collection replacement as ambiguous ([RivalFoodCatalogue](../../Runtime/RivalFoodCatalogue.cs)).
- Held food images keep only a generic service flag. Named bag-stockout evidence is revalidated only when the time and queue bounds fit, and with zero throughput the meal alone must leave spare time before the deadline ([FoodMemoryCommerce](../../Core/FoodMemoryCommerce.cs)).
