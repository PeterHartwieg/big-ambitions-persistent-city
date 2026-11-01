# Population schema 23 — E4 menu envelope

1 November 2026. Owner: `pc-e4-wider-menu`; integrated predecessor: **22** (C2b, after E1 schema 20). D2 schema 21 remains reserved and refused. Contracts 13-20 and 22 retain their exact source shapes and limits. E4 first landed at `2bd9006` with predecessor 20; this composition follows its documented predecessor rule.

`MenuWire23.cs` owns the isolated migration and product-cell converter/precheck. The source is streamed with its exact schema resolver, four-offer limits, token rules, capability checks and semantic validation **before** migration. The menu migration accepts only exact predecessor 22. C2b's isolated 20 -> 22 step validates exact schema 20 and adds empty campaign history first; the composed chain is 19 -> 20 -> 22 -> 23. Existing 18->19 and frozen 13-17 imports reach 19 first, then the isolated `WeatherWire20.cs` migration sets next-day weather activation and schema 20. The exact schema-20 resolver includes all E1 weather fields and excludes campaigns. The exact schema-22 resolver includes C2b campaign images and its 32-source held envelope. Both exclude E4's header, tuning revision 2 and compact history cells. The menu step only sets the fixed product-policy header and schema 23, then validates activation. Existing schema-20 activation day, resident commitments, consumer weather and held decision weather/multiplier remain exact. No identities, tastes, money, outcomes, scopes, orders or historical rows are regenerated. No original dictionary value is changed on failure: `SaveStore`/`SaveSession` validate a complete candidate before replacement.

`MenuPredecessorSchema` is now 22. C2b campaign images, held ActiveCampaign flags, weather and all prior facts survive the menu step unchanged. D2 still needs its own approved predecessor resolver/migration below this chain; no schema-21 wire shape is inferred.

## Header and allocation

Required root string `MenuPolicy` has the exact value:

`1|1=croissant|2=burger|3=hotdog|4=donut|5=cupcake|6=salad`

Version 1 pins the projection and canonical `ba:itemname_…` mapping in `FoodMenuPolicy`. Codes 1–3 retain existing saved A2 tastes; codes 4–6 reserve identities only. They are **not sale permissions**. Codes 7–16 remain unallocated. E5 siblings must coordinate append-only allocation, reviewed priors and product-specific native proof. This executable refuses different tables/versions. Header text is fixed ASCII, well below the 64 Ki character definition/header allocations; there is no growing per-resident table. Both early authority bootstrap and simulator construction check the policy before execution. Root schema/capabilities/menu headers are emitted before the graph. Full startup still validates the entire source, including duplicate/unknown fields.

Schemas 18/19/20/22 exclude `MenuPolicy`, schema-23 tuning images and compact history cells from their allow-lists. They retain four captured offers, four product sales cells and three additional scope IDs. Reserved 21–22 and foreign shapes refuse as unsupported; >23 reports Future. The frozen 13–17 codec retains its exact shape and semantics; its three product checks now name the explicit immutable `KnownLegacy` list (the identical original three), so future E5 definitions cannot widen an old reader.

## Menus and tuning

Schema 23 accepts one primary plus up to seven distinct additional product IDs, eight offers per held shop, eight shortlisted shops and eight product cells per business-day. Runtime still admits only the same three supported native products, with **effective maximum min(configured, 4)**. No reserved product, recipe, drink, cart or bundle gains support. A menu cannot be partially captured to fit a limit. Unsupported edits and lowered native limits use existing whole-business handoff at the accounting boundary; pending terms/receipts remain historical facts.

One meal selects one product. Product reservations remain keyed by place/product without a width-sized array. Service scheduling uses the existing single `NextServiceAt` per scope. Native supply observes one eligible counter/bag pool in one setup, exposes the same shared cap on each offer (these caps must never be summed), and checkout revalidates/consumes it once. The menu cache remains keyed by save/minute, authority object, registration, business identity, ordered full menu and relevant registration fields. Persisted captures and checkout deliberately bypass stale decision-cache images; after-checkout invalidation remains mandatory. No per-frame work was added.

Flat config defaults/ranges: `FoodMenuMaximumOffersPerShop=4` (1–8), `FoodMenuNewTasteWeight=1` (0–2), `FoodMenuHealthWeight=0` (0–2). They use the existing loader, clamp/default/log path and city-unloaded reload semantics. The single held tuning image stores effective settings: original revision 1 stays **193 identical characters** for menu defaults; revision 2 is **241** characters, the same twelve binary64 words followed by maximum offers, new-taste weight and health weight (three binary64 words). Maximum offers must be an integral 1–8 value. Old source readers accept revision 1 only. Held contexts use captured settings after reload; later uncaptured contexts use loaded settings.

## Tastes and priors

A2 version 1's ten saved bands/order/generator are untouched. Existing croissant/burger/hot-dog preferences and fit vectors are unchanged. Unknown products are neutral, never assigned the hot-dog band. New taste projection version 1 hashes `pc:product-taste:v1|<consumerId length>:<consumerId>|<canonical decimal code>` with the saved city seed using existing StableRandom semantics. Percentiles 0–9/10–29/30–69/70–89/90–99 map to bands 0/1/2/3/4. No name, home, pool, camera, day, update cadence or config is an input. No per-consumer dictionary is persisted. Health/quality tags are unknown and neutral; future reviewed tags/meal-fit priors belong to E5. No native nutrition or recipe is inferred.

## History and budgets

Reviewed product cells use exactly seven canonical nonnegative integer JSON entries:

`[productCode,purchases,revenueUnits,legacyMeals,breakfastMeals,lunchMeals,dinnerMeals]`

Money remains exact signed-64-bit unit semantics with nonnegative history values; counters fit nonnegative int64. The seven-entry maximum (single/two-digit code plus six ≤19-digit numbers, six separators and two brackets) is ≤124 characters, within **128**. Shape/arity/code/numeric/length bounds are checked before cell allocation. Existing count/revenue/hour/meal/district reconciliation still runs, including overflow checks. Empty/unconfirmed entries do not become sales. Opaque historical product IDs retain the old named-object representation losslessly, including legacy text; they confer no live permission. This exception preserves old readable history instead of introducing a hidden historical-ID ceiling.

Both counting paths now charge each history row and product cell once. The specified **14 × 1,024 rows + 114,688 cells = 129,024** allocation fits the unchanged **4,000,000** cap; the product-cell allowance is **14,680,064 characters**. Scalar counters inside a cell add no records. Eight offers increase cheap evaluations to 64, never the sixteen route slots. Ordinary history retention remains fourteen days; migration/serialization never trims retained history.

The allocation is a sizing composition, **not** a new 1,024 historical-generations/day ceiling. The existing `14 × MaxPlaces` history-row ceiling is retained; churn beyond 1,024 generations/day is preserved if the complete payload fits. More than eight product cells in one generation/day, bad sums, record/character/UTF-8 limits or unsupported shapes cause atomic refusal. No histories or product totals are silently omitted. The global resident/place/route/depth/size limits are unchanged.

The offline E4 harness compiles surrogate `test:e4:*` definitions in its own project only, via erased partial hooks. Production builds do not compile that file and contain no synthetic product definition. This tests 4/8 captures, interrupted quote reloads, supply and the full history allocation without registering dummy native items.
