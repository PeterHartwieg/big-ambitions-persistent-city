# Food demand: implementation and evidence

The first foundation of the [approved economy roadmap](design.md#agreed-economy-roadmap--21-october-2026) began on 21 October 2026. The first neighborhood food slice is implemented; the broader economy remains in progress and authoritative sales are not enabled in normal play.

## Bounded menus — 23 October

The [0.9.0 menu candidate](reviews/2026-10-23-food-menus.md) adds verified croissant/hot-dog/burger menus and reports actual sales by product and meal. **1,110 tests and eight native Mac days reconcile 667 purchases**. Meal roles and price-driven substitution work; overlapping branches compete and staffing absences materially affect outcomes. Per-product reservations share shop service and bags, and unsupported menu edits visibly pause the affected shop. Opus agrees to close the increment. Shared fit priors still produce overly uniform tastes, and rendered stutters prevent performance clearance. Native replenishment and a normal-play staffing/hours loop are next; the candidate remains disabled and the Mac is restored.

## Breakfast, lunch and dinner — 23 October

The [0.8.0 meal-rhythm candidate](reviews/2026-10-23-meal-rhythm.md) replaces the live scope's single midday opportunity with three individually timed meals and distinct preferences. One saved wallet and daily income/basket posting serve all three; old receipts and active trips survive migration. **1,088 tests and six native Mac days pass.** The same all-day small shop averages 15 breakfast, 39 lunch and 26 dinner purchases; restricted hours isolate the corresponding meal demand. Stock, funds, native accounting, receipt replay and reload checks reconcile. The Mac is restored and released. Croissant-only scope and provisional preferences limit realism; broader menus and fresh rendered performance testing remain.

## Feasible shopping and leisure destinations — 23 October

The [PC comparison](reviews/2026-10-23-optional-destinations.md) verifies the next routine increment at one-third clock speed. New optional outings check actual route estimates and usable opening time, with bounded alternative searches and preserved committed trips. Shopping visits increase 228→429, leisure 238→552 and misses fall 948→446. Existing native food sales remain 38, with settlement/reload conservation intact. All 1,048 tests pass; the isolated rig is restored, and normal profiles remain untouched.

Next, connect eligible existing shopping stops to purchases within their remaining visit time and budget. This increment improves routines; it adds no new transaction path or guaranteed customers. Native rendered performance and household income remain unverified.

## One-third calendar pace — 22 October

The [matched Mac comparison](reviews/2026-10-22-city-clock.md) supports Peter's proposed three-times-longer day. With the same 957 residents, checkpoint and 15/hour shop, actual purchases increase **13→42 per game day**. Movement is unchanged; the calendar slows globally after population initialization, and Driven Rides defers its positive multiplier. A complete native taxi ride confirms one-third time without compounding slowdowns. All 1,038 tests and native conservation/reload checks pass; the rig is restored and released.

More shopping/leisure visits complete, but missed-activity increments also increase. Next, investigate reachable destinations and usable opening windows, then connect existing shopping stops to purchases. Keep personal budgets, district/service preferences and finite stock/service capacity. This remains a disabled private candidate, with rendered performance unverified.

## Demand diagnosis and scaling — 22 October

The [follow-up audit](reviews/2026-10-22-food-demand-audit.md) explains the eight-sales day: only nine of 910 evaluated residents had a feasible shop purchase; the main limit was walking and returning within a 45-minute break. Stable 45–75 minute break flexibility increased native sales to ten. Direct choice evaluation, enrolled-controller reads and conservative trip bounds sharply reduce work, with separate native measurements and 1,000/2,000/5,000-resident offline checks. Purchasing during an existing shopping stop or journey is the next structural food question; higher budgets alone do not resolve the restricted catchment.

## Automatic routines and native sales — 22 October

Recurring food choices now own actual resident round trips and checkout inside an explicitly enrolled retail scope. Schema 8 persists the decisions, journeys and transaction ownership. The isolated PC campaign completed 24 natural purchases across three days at one 15/hour fixture, with native stock/order conservation, $246.07 daily-settled revenue, four reload checkpoints and duplicate-purchase prevention. All 1,023 offline tests pass. Household income and expenses are next; temporary allowances still provide buying power. [Current implementation, evidence and limits](reviews/2026-10-22-food-routine-sales.md).

The sections below retain earlier increments and their evidence; pending-work statements describe those historical stages.

## Native connection and multi-day capture — 22 October

The [isolated PC pilot](reviews/2026-10-22-food-native-pilot.md) now establishes current native availability and actual resident journeys through three native purchases and daily settlements. Actual home-district composition feeds the budget policy. The separate three-day capture produces a gradually declining offline price curve; the in-game full-result export hit its document-size limit and is not claimed as a completed native sweep. All 1,007 tests pass, including observation continuity through clock-rate scheduler rebuilds. The rig's original files/preferences were restored and ownership released. The next step is regular, saved meal decisions and transaction ownership within a limited live scope, followed by household income and expenses.

## Personal budgets and optional purchases — 22 October

New routine experiments use stable personal allowances. Verified home-district working/middle/upper composition can shift the budget distribution, with overlapping ranges and individual variation; missing composition uses an explicitly provisional neutral prior. Personality influences willingness to buy prepared food rather than inferred income. An observed home stop or a provisional packed-meal plan at work/study can produce `DeclinedPurchase`, separately from fulfilled, unmet and unresolved outcomes. No household meal, ingredients or cost is booked by declining a purchase.

Schema 4 preserves earlier experiments' budgets and choices. All 31 earlier PC price points replay unchanged after migration. The new policy's separate offline replay replaces the shared-$18 cliff with a gradual price response and allows lower sales or no customers. The retained PC capture lacks verified home-district composition, so district sensitivity is shown separately using controlled examples. **1,003 tests**, 11 household-adapter checks and a native-target build pass; this increment has no new rig execution or installation. [Policy, curve and evidence](reviews/2026-10-22-food-household-budgets.md).

## Customer service and district appeal — 22 October

Food-place choice now includes the saved customer-service score as a quality proxy and saved district demand for the offered product. These are bounded personal-value adjustments to otherwise feasible choices, with no extra population, needs or spending allowance. District demand already includes supplier saturation, so it is not applied again as a customer-count multiplier. Missing values remain unknown. Schema 3 preserves earlier experiments with zero new weights. [Policy, comparison and checks](reviews/2026-10-22-food-quality-demand.md).

## Routine and service increment — 22 October

The food experiment now accepts recorded stops from the population engine, with reproducible meal timing, actual activity origins, provisional lunch breaks and round-trip deadlines. Service throughput, queue patience and finite stock constrain fulfillment. An explicitly attached observer and bounded native walking-quote entry point connect the experiment to the population without changing its journeys or the native economy. Schema 2 preserves the earlier fixed daily model as a reference mode. [Implementation, small-shop comparison and validation](reviews/2026-10-22-food-routines.md) track the current evidence and remaining limits.

The following sections retain the first increment's scope and checks; its fixed daily demonstration is not the routine/service model.

## What works

- `Core/FoodDemand.cs` runs a detached, one-to-seven-day food experiment. Each supplied resident has one persistent meal intention per day and an explicitly temporary daily spending allowance. The current economic boundary includes only those residents; it supplies no commuters, visitors, wages or outside trade implicitly.
- Businesses compete on food price, supplied travel time, opening waits and a resident's food preference. Every evaluated offer retains an explanation. Unknown stock, unknown/refused routes, unaffordable food and late/closed destinations cannot become successful purchases. When missing evidence prevents a choice or fulfillment, the result is unresolved rather than counted as an unmet need.
- A committed hypothetical trip reaches its destination before fulfillment. Prices, hours and stock are checked again at arrival. Fulfillment consumes one unit of the experiment's stock and records hypothetical spending once. A failed purchase remains unmet for that daily intention; same-day searching for another shop is not implemented.
- Stable ordering, bounded event batches and a separate strict `persistentcity:food-shadow` document retain intentions through an interrupted run. These documents never enter the native save or the existing population payload. The experiment copies supplied people, businesses and route quotes; it does not change actual resident routines or claim that hypothetical journeys occurred in the live city.
- `tools/food-demand` produces an HTML insight view, JSON comparison and initial/final experiment documents. The default fixture compares baseline, a price increase, a shop closure and empty stock. An explicit input-document/city-key pair can replace the synthetic fixture.
- `Runtime/FoodBusinessCatalogue.cs` provides a main-thread, on-demand reader for up to eight explicitly selected shops and sixteen explicitly selected food products. It copies existing registration fields for advertised products, prices and opening hours, and reports unavailable places. It never creates registrations or calls customer-generation helpers. The business key includes the recorded creation day, type and owner, separately from the address/door identity. Continuous lifecycle tracking, including same-day replacement, remains pending.

The native reader intentionally reports stock as **unknown**. An advertised product is not proof of stock, a working producer, staff or service capacity. It is not yet called by `PopulationRuntime`, and it has no periodic scan, hotkey or automatic experiment activation. This prevents an unvalidated observer from silently changing the existing game workload.

## Demonstration and checks

Run from the repository:

```sh
dotnet run --project tools/food-demand/FoodDemand.csproj -- build/food-demand-demo
dotnet test tests/PersistentCity.Tests.csproj
dotnet run --project tools/food-catalogue-regression/FoodCatalogueRegression.csproj --configuration Release
```

Open `build/food-demand-demo/index.html`. All names, journeys and inventories in the default fixture are synthetic, not game measurements. It has 24 fictional residents, two shops and two days (48 meal needs):

| Scenario | Corner Kitchen meals | Market Deli meals | Unmet needs | Hypothetical spending |
|---|---:|---:|---:|---:|
| Baseline | 10 | 32 | 6 | $225 |
| Market Deli meals cost $4 more | 42 | 0 | 6 | $273 |
| Market Deli closed | 42 | 0 | 6 | $273 |
| Both shops out of stock | 0 | 0 | 48 | $0 |

The six baseline misses are the fixture's deliberately insufficient allowances. These totals demonstrate causality, not calibration to Big Ambitions. Stock is finite and not automatically replenished; the experiment does not simulate staff, checkout capacity, wages, household accounts or production recipes. Travel quotes remain fixed input to each experiment, not continuously observed physical movement.

Checked:

- All **957 tests** in the current combined checkout passed, including **15 new food-demand tests**. They cover price/travel/preference responses, closure/stock effects, interrupted journey replay, finite stock contention, repeated advancement, cadence/order independence, detached inputs and refusal of malformed/future/foreign documents.
- **15 checks** execute the actual catalogue-reader source against native field doubles. They cover refreshed prices, business generations, unavailable/access-restricted places, detached snapshots, closed hours, reviewed product selection and refusal of invalid prices. These are offline adapter checks, not Unity execution.
- The full moving source compiles against the local Mac game's Managed assemblies with C# 9, nullable checks and warnings as errors. Frozen source manifest/build log are under ignored `build/economy-first-step/`; compilation package DLL is `5668951d216ef2bce73d1792302d722ff37a7ab88b6be6c45be1dc7e8226ef2d`. It includes existing concurrent compatibility/performance changes and is **not** a separately reviewed deployment candidate.
- The generated report was opened in the browser. Scenario selection, resident selection, price redirection and the no-stock/unmet view were checked. The baseline screenshot is in the generated demo folder.

No native runtime measurement, installation, save modification or economy activation was performed for this increment.

## Native sale integration: source findings

Read from the locally installed `BigAmbitions.dll`, with decompilation kept only in ignored `build/economy-first-step/native/`. The source fingerprint is recorded in that folder's evidence receipt. These findings establish a candidate integration path, not a proven live hook.

1. `AI.Customers.CustomerEntries.CustomerEntriesHelper` owns daily customer entries. `GetEntriesByAddress` generates AI entries when none exist, and that generation can obtain/create a building registration. It is not a universally read-only observer. Player-business redraws replace unfinished entries.
2. `Buildings.Retail.Simulation.RetailBusinessSimulator` processes the relevant hour's entries, accounts for native customer/service capacity, chooses producers and checks native price acceptance. Paid goods subtract producer stock. Completed orders enter the business's `unprocessedCompletedOrders` list.
3. `Helpers.BusinessHelper.ProcessDailyOrders` builds order history, books daily revenue through `GameManager.ChangeMoneySafe`, then clears the pending orders. The future pilot should feed or replace the appropriate demand path while preserving this settlement, rather than credit cash separately for each arrival.
4. The simulator currently creates a random native `CitizenData` for processing. Supplying a persistent customer's decision therefore also requires reviewing how its budget/preferences interact with native price acceptance; replacing only the number of entries is insufficient to make residents authoritative.
5. The inspected RLO `CustomerDraw.Entries` can flush a queued redraw before returning its list. RLO also changes customer generation. A pilot needs explicit demand ownership across those paths. A function named as a read is not enough evidence that it has no side effects.

The proposed integration is to own selected food demand while retaining native stock movement, completed-order accounting and settlement. It does not require recreating the financial ledger in this milestone. Visible indoor customer processing and RLO redraw interactions still require focused review/testing before choosing the exact hook.

## Historical remaining work within the first milestone

Subsequent native sale integration and the [neighborhood food candidate](neighborhood-economy.md) implement the recurring transaction path and persistent resident accounts. The original shadow-stage checklist below is retained as historical context.

1. Observe actual sellable stock, effective staffed throughput and product/serving identity. Keep unknown evidence distinct from stockouts or unavailable service. The routine model supports these inputs; its controlled service rates do not establish native availability.
2. Give the chosen food outing ownership of an actual activity/journey, including return and missed-opportunity handling. Recorded stops currently feed a separate counterfactual; people do not execute both histories. Keep the current work templates separate from future real employment shifts.
3. Refresh inputs with explicit simulation-time semantics. Do not overwrite consumed hypothetical stock whenever the native market is sampled: the observed economy and the counterfactual are different histories. Validate the new home-district capture in game. Home/packed-food preference now permits declining a purchase; ingredients, meal preparation and household finances remain future work, with no free meals or silently invented purchasing power.
4. Compare meaningful responses and demand scale with observed native activity. Quantify missing external demand before adding cohorts. Keep the [small-shop expansion target](design.md#small-shops-and-useful-expansion--22-october-2026) as a balance criterion, including operating costs before claiming profitability. This means useful opportunities for suitable small shops, not a requirement to keep every business viable.
5. Demonstrate the native transaction boundary on a disposable pilot before enabling economic authority, replacing overlapping native demand and retaining native stock/accounting settlement.

The strategic scope remains the short food experiment and a credible path to real sales. Broad preferences, extra needs, social presentation and a general economy framework are not prerequisites.

## Native comparison criterion

Peter clarified on 21 October: aim for **directional alignment, with more variety through semi-realistic simulation**, not perfect symmetry with native sales. Compare price response, closure/availability response, travel sensitivity and choice diversity separately. Keep native potential customers, purchase acceptance and booked sales distinct. A reproducible rig probe and multi-cohort comparison are in [tools/food-native-comparison](../tools/food-native-comparison/README.md). At preparation time both rigs were reserved; Peter then instructed this task to wait only for the Mac rig because the PC will be switched off. The Mac comparison subsequently ran: [native comparison and findings](reviews/2026-10-21-food-native-comparison.md). Directional checks passed, but that fixed-reference model had no day-to-day choice variety or service constraint. The 22 October increment above adds recorded routines and explicit service inputs. These detached native quotes and offline replays are not live booked-sales or resident-journey validation.
