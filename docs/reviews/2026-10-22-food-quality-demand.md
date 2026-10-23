# Customer service and district appeal — 22 October 2026

Peter requested customer service as a proxy for quality and district market demand in food-place decisions. Both now influence the detached food-choice experiment. They change the relative attractiveness of feasible offers; they do not create residents, meal opportunities, budgets or service capacity. Normal-play food journeys and native sales remain disabled.

## Choice behavior

After the existing affordability, opening, stock, service and round-trip checks, the resident compares:

`price + outing time cost − food preference + customer-service adjustment + district-product adjustment`

Each adjustment is `(50 − observed percentage) / 100 × the resident's value for that signal`. A higher score makes the offer less attractive. Fifty is the explicit neutral reference. Unknown observations stay null and contribute no adjustment; they are not represented as measured average or excellent service. The insight view retains both observed percentages and each adjustment alongside the final choice score.

New routine-generated consumers have stable, seeded personal weights: a full 0-to-100-point service difference is worth $1–$4, while the same district-appeal difference is worth $0.50–$2. These are provisional game-balance settings, not empirical estimates. No daily random reroll or additional route search is introduced. Existing schemas migrate with zero new weights, preserving their earlier choices.

Customer service and service throughput are distinct. A well-rated but unstaffed, closed, unaffordable or out-of-stock shop still cannot serve a meal. A quality advantage changes perceived value, never the price charged or money available. No hypothetical quality reputation is written back to the native game.

## Native signal meanings

Local game-source inspection identifies the BizMan customer-service bar as the saved `satisfaction.customerService` value. The catalogue reader copies this field, including the game's possible initial default of 50. It is a reported service score, not proof of current staffing or an independently measured food-quality rating.

District demand comes from the existing saved product-market entry for the shop's district and product. The native calculation already includes provider saturation and market events. It is not a count of hungry residents. The reader copies existing values without calling market recalculation, registration creation or a helper that invents a missing default. Negative event-driven values are normalized to zero in the detached snapshot, preserving the raw value in its evidence text; the native field is unchanged. Missing data remains unknown and ambiguous duplicate entries are refused.

We therefore use district demand as a modest relative product/district appeal signal, not another multiplier on the resident population or each shop's customer count. The same product percentage shared by two branches in one district contributes the same adjustment. Opening a branch does not independently re-roll or reduce each resident's interest in that product. This does not make the native percentage an independent measure of tastes: once Persistent City owns the wider market, its own purchases/unfulfilled opportunities should inform local pressure instead of perpetuating the legacy provider formula.

This increment still chooses among available offers. If only one feasible offer remains, an affordable purchase can still occur despite weak signals. Optional purchases, home-food alternatives and a calibrated price curve remain separate work.

## Controlled comparison

The same 957 synthetic residents, seven days and 6,699 opportunities compare two equally reachable shops. A costs $8. B costs $9 unless stated. Deliberately high throughput isolates choice effects from queues; the fixture is not a native sales or operating-capacity measurement.

| Change | Shop A meals | Shop B meals |
| --- | ---: | ---: |
| Equal service and district appeal | 6,699 | 0 |
| A service 40, B service 90; B costs $1 more | 2,205 | 4,494 |
| Same service advantage; B costs $4 more | 6,699 | 0 |
| Equal service; district appeal A 20, B 90; B costs $1 more | 4,158 | 2,541 |
| Both service and district appeal favor B | 98 | 6,601 |

Different residents accept different premiums. The combined result is deliberately strong because the two offers otherwise have equal travel and no binding supply constraint; it does not establish a realistic market share. All six directional/conservation checks pass. The prior local-expansion comparison is unchanged: first small shop 541, second neighborhood shop 517, and one larger shop 965. No extra customers or buying power were needed.

Reproduce with `dotnet run --project tools/food-choice-signals/FoodChoiceSignals.csproj --configuration Release -- build/food-choice-signals-demo`. Its insight input contains a separately labeled 24-resident subset.

## Validation and scope

- Full Release suite: **989 passed**, including **12 new quality/district cases**. The 47 focused food cases also pass. Tests cover heterogeneous premium choices, feasibility limits, common district signals, unknown versus zero values, interrupted service and strict legacy migration.
- Actual catalogue-reader source: **25 checks passed against native field doubles**, including district/product scoping, refresh, detached values, missing/invalid data and non-mutating normalization.
- Native-target C# 9 warnings-as-errors build passed. Candidate DLL SHA-256: `74c4f01e4ee2324b672ca6672addf90c21f8527d8924d240bbe4e9a04284f658`.
- Food schema 3 preserves schemas 1 and 2 through strict migration. Population schema 7 is unchanged. The retained schema-2 routine demonstration was replayed separately.
- The insight's score breakdown and price-driven switch back to the cheaper shop were verified in the browser. The interactive subset is not the full-cohort result.

This increment has **not been exercised in a new native rig session**. The earlier routine observation validates the preceding candidate, not these newly added fields. Neither rig nor normal game profile was changed during this increment. No extra per-frame observation or background food loop is enabled. The new scoring adds constant work per already-evaluated offer; no FPS claim is made.

Ignored evidence, frozen source, logs, comparisons and browser previews are under `build/food-quality-demand-20261022/`. Game binaries, native decompilation and raw saves remain outside Git.
