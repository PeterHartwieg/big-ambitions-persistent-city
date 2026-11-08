Status: implemented (offline); not verified by rig

Refs #93. **Calibration acceptance: stopped** on the dominant-menu design criterion. No product defaults or schema changed. This record contains observations and candidate control envelopes, not approved release thresholds. The PR-description handoff and complete numbered rig list are in [checks](../checks/f6h.md); the current section 6 offline table is in [performance](f6h.md).

## Protocol and retained evidence

Fresh writer-33 F5 population; F6a/F1-derived fixture; 957 residents + 957 independent visitors; one-minute routes; one own shop and an equidistant rival; same three-product menu, price, 07:00–22:00 hours and service rating; a dormant own site at twice the distance. Resident Foodie personas and opening wallets are the inherited fixture, not an inferred city-wide distribution. The own supply double reports 60 meals/hour, not a native measurement. Per-person F6f ordinary visits use outside retail/leisure, the published ownership-hour seam and the existing $5/$12 table. All employment/food/discretionary/journal loops are active. This is detached Core evidence, never a native-payroll/wholesale/stock claim.

Every one of the 16 section 3.5 arms runs six weeks (14 baseline, 14 intervention, 14 reverted days), five seeds 0–4, with a shared matched same-seed no-intervention control (17 × 5 = 85 runs). The acceptance reader asserts exact equality of all baseline day metrics against the matched control. Inputs publish only at drained, unobserved whole-minute epochs. Every owner is checked each midnight. The last day's meal outcomes are sampled at 03:00, after dinner and before breakfast. Day metrics include hourly/meal/product purchases, considered/chosen offers and causes, own/rival/home catchment shares, jobs, income, wallet strain, receipts and outside debits. Individual daily files are reproducible tool output and are not committed raw city saves.

Machine evidence is retained in [f6h-acceptance.json](../../tools/economy-interventions/evidence/f6h-acceptance.json): weekly values/bands by seed, recovery, failure certificates and hashes of all 85 raw metric files. [f6h-summary.json](../../tools/economy-interventions/evidence/f6h-summary.json) retains all F6a summary metrics including shares, causes, jobs, elasticities and per-seed values. These are synthetic experiment metrics with no native/game payload. Price-weight diagnostic evidence is retained separately.

Seed band means **mean ±2 sample standard deviations across five seeds**, not a confidence interval or a hand-selected tolerance. Negative lower bounds on inherently nonnegative quantities are retained as statistical output; observations remain nonnegative. Recovery uses the control's same-day band for a three-day mean wholly after reversion, so three days is the earliest possible result. It is not a claim of zero long-term memory.

## Daily accounting and tradeoff results

**6,832,980 daily owner conservation checks** in the complete matrix. Zero duplicate receipts, zero HistoricalClockMisses, zero out-of-epoch reads. Prepared spend (plus confirmed uncertainty) equals native-double receipt revenue daily; cumulative rival totals equal watermarked outside debits, and every 14-day ordinary-spending window equals its exact cumulative discretionary-debit difference. Three deliberately injected fault kinds turn the representative integrated run red. The native comparator remains rig work.

Both staffing-cut arms never exceed their matched control's intervention-fortnight served total in any seed. Inside the actual cut windows, the lunch-halving arm serves 864/904/909/876/883 fewer meals (seeds 0–4), and the zero-staff day serves 443/454/448/450/444 fewer. Small later-day increases after staff restoration are retained: the zero-staff arm has one to two additional meals on some later days; seed 1 has +2 over control in week 4. These residuals do not turn a cut-window or fortnight gain into a pass for every possible aggregation. All 80 intervention runs meet the specified recovery definition at **3 days**, with end-of-recovery residuals recorded in the machine report. Every family has at least one effect outside the control band in the fixed measured vector (own purchases, revenue, rival meals, income); this is not a claim that each arm moves purchase quantity. Price, employment and rival purchase quantities remain inside the own-purchase control band. No all-arms Pareto/no-dominance pass or every-gain-has-a-cost pass is claimed; staffing expense, venue expense and measured wholesale are not available in the detached double.

| Arm | Own purchases/day, weeks 1–2 / 3–4 / 5–6 (seed mean) | Own quantity outside control band | Recovery days (seeds 0–4) | Residual purchases/day after 14 days (mean) |
| --- | --- | --- | --- | ---: |
| control/control | 457.714 / 460.214 / 461.857 | no | — | — |
| price/product-minus-25 | 457.714 / 460.143 / 461.943 | no | 3, 3, 3, 3, 3 | -0.133 |
| price/product-plus-25 | 457.714 / 460.186 / 461.914 | no | 3, 3, 3, 3, 3 | -0.200 |
| price/menu-plus-50 | 457.714 / 460.114 / 462.071 | no | 3, 3, 3, 3, 3 | +0.067 |
| staffing/lunch-counters-halved | 457.714 / 392.843 / 461.571 | yes | 3, 3, 3, 3, 3 | -0.533 |
| staffing/zero-staff-day | 457.714 / 428.129 / 461.800 | yes | 3, 3, 3, 3, 3 | -0.600 |
| hours/breakfast-removed | 457.714 / 331.371 / 461.086 | yes | 3, 3, 3, 3, 3 | -0.667 |
| hours/close-2h-earlier | 457.714 / 392.071 / 461.071 | yes | 3, 3, 3, 3, 3 | -0.533 |
| shortage/product-3-lunches | 457.714 / 459.443 / 461.629 | no | 3, 3, 3, 3, 3 | -0.333 |
| shortage/whole-shop-day | 457.714 / 427.471 / 461.786 | yes | 3, 3, 3, 3, 3 | -0.200 |
| employment/workplaces-closed-10pct | 457.714 / 458.486 / 460.429 | no | 3, 3, 3, 3, 3 | -0.933 |
| employment/worker-routes-blocked-5pct | 457.714 / 459.071 / 461.171 | no | 3, 3, 3, 3, 3 | -0.800 |
| access/route-time-plus-50 | 457.714 / 459.457 / 461.829 | no | 3, 3, 3, 3, 3 | -0.467 |
| access/door-closed-2-days | 457.714 / 394.900 / 461.414 | yes | 3, 3, 3, 3, 3 | -0.533 |
| access/second-site-2x | 457.714 / 865.814 / 461.600 | yes | 3, 3, 3, 3, 3 | -0.467 |
| rival/lanes-halved | 457.714 / 460.400 / 462.029 | no | 3, 3, 3, 3, 3 | +0.000 |
| rival/price-minus-25 | 457.714 / 460.243 / 462.014 | no | 3, 3, 3, 3, 3 | -0.067 |

Control intervention-period own purchases/day: **460.214**, seed band **451.525–468.903**. F6a before: 465.7/day, band 457.0–474.3. These are different population/economic-loop versions; the change is measured, not a matched coefficient experiment.

## Price failure and bounded diagnostic

F6a already recorded a dominant +50% price extreme and approximately zero price elasticity. With all F6 loops the failure remains. Whole-menu +50% changes quantity by about a tenth of a purchase/day against control while revenue rises almost 50%. Merely enabling wages, paid outings and bounded rivals does not repair price responsiveness.

| Price arm | Served-quantity arc elasticity mean | Per seed 0–4 |
| --- | ---: | --- |
| product-minus-25 | 0.001346 | -0.004069, 0.002711, 0.013645, -0.004189, -0.001366 |
| product-plus-25 | -0.001741 | -0.003491, -0.006974, -0.001751, 0.000000, 0.003513 |
| menu-plus-50 | -0.000540 | 0.000000, -0.001922, -0.000385, -0.000393, 0.000000 |

No wholesale cost is guessed. Let ΔR be the measured weekly revenue difference and Δq the measured product-count difference between the +50% arm and a comparison arm. Over every fixed wholesale vector with 0 ≤ cᵢ ≤ baseline retailᵢ, the minimum profit difference is `ΔR − Σ max(Δqᵢ,0) × baseline retailᵢ`. This is the exact minimum of a linear function on a box. A positive minimum disproves acceptance throughout that box; it does not claim that actual native cost has been read. The fast Python test also compares this certificate with exhaustive endpoint enumeration, including product switching.

| Seed | Week 3 minimum weekly advantage over all other price arms/control ($) | Week 4 minimum ($) |
| --- | ---: | ---: |
| 0 | 6,334.00 | 6,380.00 |
| 1 | 6,312.50 | 6,403.00 |
| 2 | 6,477.00 | 6,469.50 |
| 3 | 6,303.00 | 6,353.00 |
| 4 | 6,378.00 | 6,452.00 |

The default price weight is 1; the existing setting's upper bound is 3. The opt-in `--price-weight 3` diagnostic retains the same five-seed price protocol to test that ordinary bounded knob without changing product defaults. **Measured result: stopped again.** Across 20 six-week runs (control plus all three price arms × five seeds), all 1,607,760 daily owner checks conserve, receipts/outside totals reconcile, and duplicates/historical misses stay zero. [Bounded-setting report](../../tools/economy-interventions/evidence/f6h-price-bound.json) and [full diagnostic summary](../../tools/economy-interventions/evidence/f6h-price-bound-summary.json) retain the seed values and hashes. The +50% arm still wins both intervention weeks in all five seeds throughout the entire stated wholesale box. This is an E5 choice setting, not authority to redesign its response function in F6h.


| Seed | At price weight 3: week 3 minimum advantage ($) | Week 4 minimum ($) |
| --- | ---: | ---: |
| 0 | 6,183.50 | 6,357.00 |
| 1 | 6,430.00 | 6,409.00 |
| 2 | 6,328.50 | 6,432.00 |
| 3 | 6,317.00 | 6,208.50 |
| 4 | 6,334.50 | 6,439.00 |

At the upper-bound setting, menu +50% served-quantity arc elasticity mean is **-0.000538**, versus **−0.000540** at weight 1. This is insufficient to remove the dominant extreme; no default is changed.

The source of the weak response is visible in `FoodChoicePolicy.WeightCore`: `1 + priceWeight × sensitivity × price / spendingLimit`. SpendingLimit is available savings after reserve, capped at $10,000. At the fixture's $3/$5/$8 menu, savings dwarf a meal price; the inverse-linear soft penalty changes little, while service capacity binds. The +50% arm remains below most affordability ceilings. For one offer with its non-price terms fixed, the offer-weight elasticity from this inverse-linear factor has magnitude `bP/(1+bP) < 1` for any positive finite coefficient b. A coefficient changes its scale, not that response shape. The integrated capacity/memory/affordability paths add further effects; the experiments below test the actual simulator rather than asserting a global theorem about them. This is evidence for a design review, not a claim that no conceivable combination of settings could force collapse and remove sales.

**Capacity qualification:** control is capacity-bound at peak (60 served meals/hour in every seed, equal to the double's staffed input). A fresh F6h run after a choice-rule redesign must also measure otherwise matched price arms with non-binding capacity, for example a higher MealsPerHour profile, to distinguish capacity saturation from weak demand response.

**Stop decision:** keep all defaults unchanged. Candidate design options are a meal-specific willingness-to-pay/reference price, a stronger price response function, or an explicitly revised acceptance criterion. Do not exhaust wallets through arbitrary C9 charges to manufacture price elasticity. No persisted field is required or proposed here; if a future design does require one, it must be reported rather than smuggled into writer 33. No questionnaire is sent to Peter.

## Household control envelope (diagnostic candidate only)

Combined resident/visitor **person-wallet** metrics, matching F5's ownership policy and the F6f before-run. This is not a household pool or a resident-only series. The per-seed observed p10 does not fall every week in any control seed. Later weeks decline in several seeds and must remain visible: six weeks does not establish an indefinitely sustainable stationary distribution. No acceptance limit is set from this stopped run.

| Week | Arrears share mean ±2sd (%) | p10 balance mean ±2sd ($) | Unfed owner-days/week mean ±2sd |
| --- | --- | --- | --- |
| 1 | 0.0000 [0.0000, 0.0000] | 691.28 [675.54, 707.02] | 0.00 [0.00, 0.00] |
| 2 | 0.0000 [0.0000, 0.0000] | 731.06 [721.52, 740.60] | 0.00 [0.00, 0.00] |
| 3 | 0.0418 [-0.0456, 0.1292] | 739.07 [720.60, 757.55] | 0.00 [0.00, 0.00] |
| 4 | 0.0940 [-0.0929, 0.2810] | 730.34 [710.66, 750.02] | 0.00 [0.00, 0.00] |
| 5 | 0.1463 [-0.0920, 0.3846] | 713.89 [689.52, 738.26] | 0.60 [-1.19, 2.39] |
| 6 | 0.1672 [-0.0470, 0.3813] | 688.37 [654.61, 722.13] | 1.40 [-0.88, 3.68] |

| Seed | Week-end p10 balances, weeks 1–6 ($) | Week-6 arrears owners / 1,914 | Six-week unfed owner-days |
| --- | --- | --- | ---: |
| 0 | 688.28, 724.90, 735.13, 734.28, 710.78, 687.05 | 1 | 1 |
| 1 | 702.22, 733.71, 727.87, 720.75, 709.93, 680.91 | 5 | 5 |
| 2 | 694.61, 732.46, 744.28, 736.65, 716.10, 685.02 | 5 | 3 |
| 3 | 690.37, 727.53, 736.17, 719.06, 699.72, 672.11 | 1 | 1 |
| 4 | 680.93, 736.69, 751.91, 740.96, 732.94, 716.77 | 4 | 0 |

F6f before-values were week-6 arrears 1–5 owners, p10 $672.11–716.77 and total unfed owner-days 0–5 across seeds; the integrated control reproduces those values. F6a before F5/wages/ordinary endowments had p10 rising from about $1,018 to $1,991 and zero unfed days. These are versioned fixture differences, not evidence that increasing C9 prices would improve sustainability.

## Profiles and staffed throughput

| Setting/band | Before | After this slice | Reason/evidence |
| --- | --- | --- | --- |
| Rival lanes/service | 2 lanes × 12/hour = 24/hour | Unchanged | F6b's native K1 reference was 30/hour (−20%). F6a's input fixture is 60/hour: 24/hour is −60% there. Offline inputs cannot replace the plan's representative native throughput calibration. |
| Rival units | 60/meal period | Unchanged | Lane-only intervention changes 2 → 1 → 2 with units fixed; unit/band finalization stopped. No native stock claim. |
| Missed-work k | 0.5 (base .001, cap .01) | Unchanged | Employment arms measure wage/resource loss; no evidence-backed reason to move k while the price gate fails. |
| C9 modeled prices | Retail $5, leisure $12 | Unchanged | Exact outside payments and household strain measured; no arbitrary larger fee adopted to hide price failure. |
| Household control acceptance | No F6h approved threshold | Stopped; measured candidate above | Late p10 declines and five-seed dispersion remain recorded. |
| Staffed-throughput acceptance | ±25% plan target for comparable rivals; own input 60/hour | Stopped; measured own hourly peaks below | Actual native reference must come from rig readback; served outcomes are not native staffing evidence. |

Control's observed maximum served meals per hour, seeds 0–4: **60, 60, 60, 60, 60** against the double's 60/hour staffed input. No higher staffed ceiling or rival-capacity pass is inferred. Lunch staff halving reduces intervention own meals/day from 460.214 to 392.843; zero-staff-day arm averages 428.129 over the whole intervention fortnight.

## Reproduce

Use the task's Homebrew PATH/DOTNET_ROOT environment. Check `pgrep -f testhost` and wait for it to finish before every dotnet launch. Run all commands sequentially and in the foreground.

```sh
dotnet run --project tools/economy-interventions -c Release -- build/f6h-final --integrated
python3 tools/economy-interventions/acceptance.py build/f6h-final --output build/f6h-final/acceptance.json
# Expected acceptance exit status: 1, with the dominant-menu criterion retained as red.
dotnet run --project tools/economy-interventions -c Release -- build/f6h-price-bound --integrated --price-weight 3 --arms price/product-minus-25,price/product-plus-25,price/menu-plus-50
python3 -m unittest discover -s tools/economy-interventions -p test_acceptance.py
dotnet test tests/PersistentCity.Tests.csproj --filter FullyQualifiedName~F6hIntegratedTests
```

The default full test command and required game-assembly build are recorded in the performance file. No `tools/*-regression` matrix, game, rig, ModsLocal or bridge is run. The independent cadence, flagged body-cap differences, 100,000-consumer integrated stress and full section 6 offline remeasurement are now implemented in the review measurement batch, separately from the price stop. Their current results and machine evidence are in the performance record; no native or F2 divergence-bound pass is inferred. Rig checks are consolidated in the checks file; the body-cap item stays flagged before F2's contract.
