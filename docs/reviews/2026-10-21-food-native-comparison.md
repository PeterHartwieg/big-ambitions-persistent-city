# Food demand: native comparison, 21 October 2026

**Directionally promising; everyday variety and service availability still need work.** The prototype reacts sensibly to prices, closures, missing supply and longer trips. It substitutes between shops instead of trying to reproduce the game's totals. Its choices remain too repetitive when a resident's circumstances stay fixed. No production economy behavior was changed.

## What actually ran

The isolated Mac rig ran a new disposable `FT Food Economy Comparison` city with the existing mod pack, including RLO. The diagnostic found 79 food businesses, 43 with matching advertised products, and selected four coffee shops selling croissants: Donut Days, Roasted Inc, Rise and Grind, NYC, and Brewed Beans. They span Midtown and Murray Hill. This is one food category, not a nutritionally complete diet or a representative citywide market sample.

The probe executed the game's own `CustomerEntriesCalculatorRetail.GetCustomersByHour`, `RetailBusinessSimulator.CompleteOrderEntry`, `CitizenData.IsPriceAcceptable`, closed-business checks, and missing-producer/service refusal paths. Quotes used copied price lists and detached order entries. Three actual social classes were checked at eight price levels in each shop; an additional Undefined-enum case was retained as a diagnostic, not a population cohort. None of the four inspected pricing/calculator methods had direct Harmony patches; the installed mod pack still supplied the surrounding market state.

Two paused native captures matched exactly. The compared registration prices, schedules, pending orders, order histories and inventory fields were unchanged. These were actual native method executions, **not booked sales or a multi-day live sales experiment**.

The current `FoodDemand` implementation then ran offline against the observed prices and weekly hours: 12 seeded cohorts of 240 synthetic residents over seven days, 12 scenarios, and two target shops. That is **483,840 evaluated meal intentions**, including repeated baselines across the two target comparisons. Allowances ($4–$20), travel quotes (3–50 minutes), meal windows and plentiful finite supply were controlled inputs, not measurements of live residents, journeys or inventory. No output was tuned to match native totals.

## Findings

For Roasted Inc, the busiest shop in the shadow baseline, mean weekly purchases across the twelve cohorts were:

| Scenario | Shadow purchases at this shop | Native price-acceptance reference |
| --- | ---: | --- |
| Observed $9.66 price | 410.3 | Working, middle and upper classes accept |
| Price +10% | 167.2 | Working/middle reject; upper accepts |
| Price +25% | 59.9 | Working/middle reject; upper accepts |
| Price +50% | 8.3 | All three reject |
| Shop closed | 0 | Native business-open check refuses all hours |
| No sellable stock at target | 0 | Native missing-producer path makes no sale |
| Twice the travel time to target | 159.3 | Our travel-response check; native personal route choice was not tested |

These columns measure different stages and have different denominators. Their magnitudes are not intended to match. The native reference applies sharp acceptance thresholds; our result reflects budget, opening-time and travel tradeoffs between alternatives. A modest price increase can still move a large amount of our demand because these shops offer the same item without modeled quality or loyalty differences.

Donut Days, the more expensive and less popular shadow shop, also lost demand monotonically: 28.8 weekly purchases at its observed $12.11 price, 13.0 after +10%, 4.8 after +25%, and zero at twice the price. Its native quote acceptance fell to zero for all three classes at +10%.

Closing Roasted Inc reduced total fulfilled needs from 1,057.7 to 1,042.3 out of 1,680. About **96% of its former purchases moved to other shops** under the experiment's plentiful-supply assumptions. Removing all stock left all 1,680 needs unmet. All ten diagnostic/invariant checks passed for each target, including monotonic price response in every cohort. The existing 15 food-demand regression tests also passed.

**Variety is the main weakness.** Different residents distributed baseline demand over all four shops. About 9.5% of repeat purchasers changed shops during the week with the observed schedules. When all shops remained open and personal inputs stayed fixed, no resident changed shop. The current model has stable decisions but no changing appetite, learned habit or activity-driven origin. Its single shared food product also means that product preferences cannot distinguish these four offers.

**Service availability is a real gap.** Native processing refuses a purchase without a staffed/service point. The shadow model currently has no such constraint and can count a purchase whenever its own stock, price, hours and route inputs permit one. Missing producers were tested as a controlled native refusal; actual shop inventory/staff levels were not measured. These findings do not establish live sales throughput or population adequacy. The fixture's unmet needs likewise do not establish a citywide hunger rate.

## Next step

Keep the overall demand model. Connect food opportunities to residents' actual home/work/leisure routines and let meal timing, urgency and preferences vary modestly with their day while remaining reproducible. Include verified service/sellable availability before counting fulfillment. This should create understandable reasons for changing shops, with stable personal preferences rather than arbitrary daily reshuffling. Then repeat this directional comparison using observed activity origins and routes. Do not tune toward exact native customer counts.

## Evidence and cleanup

Reproducible tooling: [tools/food-native-comparison](../../tools/food-native-comparison/README.md). Local ignored receipts: `build/food-native-comparison-20261021/`, including `mac-run/native-0.json`, the identical repeat, `results/comparison.json`, `results-roasted/comparison.json`, build/reference hashes and source receipts. Probe DLL SHA-256: `02f9db53610e2bcea3d316754302ac8b1616bdc9b49d4cd639f973d04c7b3a38`.

The game exceeded the launcher's normal shutdown wait. A subsequent force-stop attempt hit the launcher's identity guard during process teardown; it did not report successful forced termination. Once process exit was verified, restoration completed separately. **All 2,677 preexisting profile files were verified**, six changed files restored, preferences and the installation manifest restored, and the helper removed. Twenty new files belonging to the disposable run were retained and listed in the cleanup receipt. Final status: stopped, reservation released, no pending work. The normal profile was untouched. Windows was checked for availability before Peter's Mac-only instruction and was not controlled or queried afterward.
