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

## Acceptance rerun after F6i, 8 November 2026

Status: **stopped** again, on the same dominant-menu criterion, for a design reason rather than a bug. Base `3f734cb6` (F6i #150), writer 33. Defaults, `FoodChoicePolicy` and schema are unchanged. Everything above this section is the first F6h run on `3c5ce3b4` and stays as recorded.

### What ran

The canonical protocol reran in full: 17 arms × 5 seeds × 42 days on the same F6a/F1-derived fixture (957 residents + 957 visitors, legacy $8/$3/$5 menu, own capacity 60 meals/hour). The full-size seed-0 control reload interrupted 14 meals in flight and returned byte-identical metrics and save. Evidence is in [f6h-rerun-acceptance.json](../../tools/economy-interventions/evidence/f6h-rerun-acceptance.json) and [f6h-rerun-summary.json](../../tools/economy-interventions/evidence/f6h-rerun-summary.json).

The task required price arms where own capacity does not bind. `tools/economy-interventions` now takes three price-only fixture options, shared by every arm and seed of a sweep. `--meals-per-hour N` sets both own sites' staffed ceiling. `--menu native` uses the retained native price levels from the [price-anchor decision](../decisions/2026-11-08-price-anchor.md): burger $12.50, croissant $9.24 and a $16.69 hot-dog slot standing in for salad. `--no-rival` gives the sole-shop case. Staffing and other families still restore the canonical fixture's terms, so the tool refuses them with a changed profile. A canonical run writes the same metric bytes as before; the default-suite test checks that. Each profile is 20 runs: control and three price arms, five seeds, six weeks. Price weights 1.5, 2 and 3 are diagnostics of the existing clamped `FoodChoicePriceWeight` setting, not defaults. All eight profiles are in [f6h-rerun-price-profiles.json](../../tools/economy-interventions/evidence/f6h-rerun-price-profiles.json), with per-seed values, certificates, reload proofs and run hashes.

### Accounting, tradeoffs and recovery (canonical matrix)

**6,832,980 daily owner conservation checks**, all conserved. Zero duplicate receipts, HistoricalClockMisses and out-of-epoch reads. Receipt revenue, outside watermarks and 14-day ordinary windows reconcile every day. Each profile sweep adds 1,607,760 checks with the same result.

Every family now has an effect outside the control seed band. Price moves revenue in all three arms; in the first run only the fixed vector's revenue moved for price, with no quantity effect. All 80 intervention runs recover in **3 days**, the earliest the definition allows. Neither staffing cut raises served meals in any seed: lunch halving serves 919/915/941/913/925 fewer meals inside the cut windows (seeds 0–4) and the zero-staff day 473/478/472/463/465 fewer. No Pareto or every-gain-has-a-cost pass is claimed, because staffing, venue and wholesale cost are not in the detached double.

| Arm | Own purchases/day, weeks 1–2 / 3–4 / 5–6 (seed mean) | Effects outside control band | Recovery days (seeds 0–4) | Residual purchases/day after 14 days (mean) |
| --- | --- | --- | --- | ---: |
| control/control | 482.886 / 481.543 / 482.643 | — | — | — |
| price/product-minus-25 | 482.886 / 484.157 / 482.671 | revenue | 3, 3, 3, 3, 3 | +0.600 |
| price/product-plus-25 | 482.886 / 479.057 / 482.686 | revenue | 3, 3, 3, 3, 3 | +0.200 |
| price/menu-plus-50 | 482.886 / 473.943 / 482.643 | revenue | 3, 3, 3, 3, 3 | +0.200 |
| staffing/lunch-counters-halved | 482.886 / 411.471 / 482.629 | own purchases, revenue | 3, 3, 3, 3, 3 | +0.267 |
| staffing/zero-staff-day | 482.886 / 447.643 / 482.757 | own purchases, revenue | 3, 3, 3, 3, 3 | +0.333 |
| hours/breakfast-removed | 482.886 / 348.714 / 482.471 | own purchases, revenue, rival meals | 3, 3, 3, 3, 3 | +0.400 |
| hours/close-2h-earlier | 482.886 / 404.143 / 482.329 | own purchases, revenue | 3, 3, 3, 3, 3 | +0.333 |
| shortage/product-3-lunches | 482.886 / 481.300 / 482.629 | revenue | 3, 3, 3, 3, 3 | +0.067 |
| shortage/whole-shop-day | 482.886 / 447.314 / 482.486 | own purchases, revenue | 3, 3, 3, 3, 3 | +0.200 |
| employment/workplaces-closed-10pct | 482.886 / 480.343 / 481.929 | income | 3, 3, 3, 3, 3 | +0.067 |
| employment/worker-routes-blocked-5pct | 482.886 / 480.643 / 482.471 | income | 3, 3, 3, 3, 3 | +0.067 |
| access/route-time-plus-50 | 482.886 / 481.929 / 482.829 | none | 3, 3, 3, 3, 3 | +0.467 |
| access/door-closed-2-days | 482.886 / 413.457 / 482.414 | own purchases, revenue | 3, 3, 3, 3, 3 | +0.067 |
| access/second-site-2x | 482.886 / 911.586 / 482.586 | own purchases, revenue, rival meals | 3, 3, 3, 3, 3 | +0.533 |
| rival/lanes-halved | 482.886 / 481.629 / 483.129 | rival meals | 3, 3, 3, 3, 3 | +0.333 |
| rival/price-minus-25 | 482.886 / 480.643 / 482.586 | none | 3, 3, 3, 3, 3 | +0.267 |

Control intervention-period own purchases/day: **481.543**, seed band **473.666–489.420** (first run 460.214, band 451.525–468.903). F6i's anchor gives the legacy menu's cheap items a discount reward, so baseline demand is a little higher. Peak served per hour is 60 in every control seed, equal to capacity.

### Price arms

Quantity and revenue changes compare the intervention fortnight with the same-seed control. Bands are mean ±2 sample sd over five seeds. Arc elasticity uses own purchases for every arm, so the product arms include substitution to the other two products. "Unaffordable refusals" is the share of evaluated own offers refused because the price exceeded the person's spending limit. The certificate column is the minimum weekly profit advantage of menu +50% over the control and both product arms, across every fixed wholesale vector between zero and baseline retail; positive in all ten seed-weeks means dominant.

| Profile | Price weight | Capacity binds | Control own/day | Arm | Quantity change [band] | Revenue change [band] | Arc elasticity mean [band] | Unaffordable refusals control → arm | Certificate min, seeds × weeks ($) |
|---|---|---|---|---|---|---|---|---|---|
| Legacy $8/$3/$5, 60/h (canonical) | 1 | yes | 481.5 | product-minus-25 | +0.5% [+0.2, +0.9] | -9.6% [-10.6, -8.5] | -0.019 [-0.031, -0.007] | 0.19% → 0.19% | — |
| Legacy $8/$3/$5, 60/h (canonical) | 1 | yes | 481.5 | product-plus-25 | -0.5% [-0.6, -0.4] | +3.6% [+2.5, +4.7] | -0.023 [-0.029, -0.018] | 0.19% → 0.20% | — |
| Legacy $8/$3/$5, 60/h (canonical) | 1 | yes | 481.5 | menu-plus-50 | -1.6% [-1.7, -1.5] | +34.6% [+34.0, +35.2] | -0.040 [-0.042, -0.037] | 0.19% → 0.21% | 4,687 to 5,244 |
| Legacy, 6,000/h | 1 | no (peak ≤648) | 2446.0 | product-minus-25 | +1.7% [+1.6, +1.9] | -8.2% [-8.4, -8.0] | -0.059 [-0.064, -0.054] | 0.94% → 0.91% | — |
| Legacy, 6,000/h | 1 | no (peak ≤637) | 2446.0 | product-plus-25 | -1.8% [-1.9, -1.8] | +2.0% [+1.7, +2.2] | -0.082 [-0.085, -0.080] | 0.94% → 0.95% | — |
| Legacy, 6,000/h | 1 | no (peak ≤637) | 2446.0 | menu-plus-50 | -5.6% [-5.9, -5.3] | +29.2% [+28.9, +29.5] | -0.144 [-0.152, -0.136] | 0.94% → 1.07% | 21,790 to 22,856 |
| Native $12.50/$9.24/$16.69, 60/h | 1 | yes | 461.0 | product-minus-25 | +2.0% [+1.5, +2.5] | -11.2% [-11.7, -10.7] | -0.070 [-0.087, -0.052] | 0.39% → 0.37% | — |
| Native $12.50/$9.24/$16.69, 60/h | 1 | yes | 461.0 | product-plus-25 | -1.2% [-1.4, -1.0] | +6.7% [+6.4, +7.0] | -0.055 [-0.063, -0.046] | 0.39% → 0.41% | — |
| Native $12.50/$9.24/$16.69, 60/h | 1 | yes | 461.0 | menu-plus-50 | -6.2% [-6.6, -5.8] | +41.4% [+40.9, +41.9] | -0.160 [-0.170, -0.151] | 0.39% → 0.46% | 10,834 to 11,987 |
| Native, 6,000/h | 1 | no (peak ≤594) | 1914.1 | product-minus-25 | +7.2% [+6.8, +7.5] | -6.0% [-6.3, -5.7] | -0.242 [-0.254, -0.230] | 2.07% → 2.02% | — |
| Native, 6,000/h | 1 | no (peak ≤550) | 1914.1 | product-plus-25 | -4.7% [-5.0, -4.3] | +2.5% [+2.1, +3.0] | -0.215 [-0.230, -0.200] | 2.07% → 2.09% | — |
| Native, 6,000/h | 1 | no (peak ≤550) | 1914.1 | menu-plus-50 | -26.0% [-26.4, -25.6] | +11.2% [+10.7, +11.7] | -0.748 [-0.760, -0.735] | 2.07% → 2.16% | 12,750 to 14,815 |
| Native, 6,000/h, no rival | 1 | no (peak ≤620) | 2044.6 | product-minus-25 | +7.1% [+6.6, +7.6] | -6.1% [-6.5, -5.7] | -0.239 [-0.255, -0.223] | 1.95% → 1.90% | — |
| Native, 6,000/h, no rival | 1 | no (peak ≤575) | 2044.6 | product-plus-25 | -4.6% [-4.8, -4.4] | +2.6% [+2.3, +3.0] | -0.212 [-0.224, -0.201] | 1.95% → 1.97% | — |
| Native, 6,000/h, no rival | 1 | no (peak ≤575) | 2044.6 | menu-plus-50 | -25.8% [-26.1, -25.4] | +11.6% [+11.1, +12.1] | -0.739 [-0.751, -0.728] | 1.95% → 2.04% | 14,216 to 15,951 |
| Native, 6,000/h | 1.5 (diag.) | no (peak ≤610) | 1974.5 | product-minus-25 | +9.7% [+9.3, +10.1] | -3.4% [-3.8, -3.1] | -0.325 [-0.338, -0.312] | 2.09% → 2.07% | — |
| Native, 6,000/h | 1.5 (diag.) | no (peak ≤553) | 1974.5 | product-plus-25 | -5.9% [-6.1, -5.7] | -0.9% [-1.2, -0.6] | -0.272 [-0.282, -0.263] | 2.09% → 2.06% | — |
| Native, 6,000/h | 1.5 (diag.) | no (peak ≤553) | 1974.5 | menu-plus-50 | -36.5% [-37.4, -35.6] | -4.2% [-5.6, -2.8] | -1.117 [-1.151, -1.083] | 2.09% → 1.94% | -8,135 to -4,299 |
| Native, 6,000/h | 2 (diag.) | no (peak ≤633) | 2027.0 | product-minus-25 | +11.6% [+10.9, +12.2] | -1.1% [-1.5, -0.7] | -0.383 [-0.402, -0.363] | 2.08% → 2.09% | — |
| Native, 6,000/h | 2 (diag.) | no (peak ≤554) | 2027.0 | product-plus-25 | -6.7% [-6.9, -6.4] | -3.7% [-4.2, -3.3] | -0.310 [-0.322, -0.297] | 2.08% → 2.02% | — |
| Native, 6,000/h | 2 (diag.) | no (peak ≤554) | 2027.0 | menu-plus-50 | -44.6% [-45.4, -43.8] | -15.9% [-17.1, -14.7] | -1.435 [-1.469, -1.401] | 2.08% → 1.81% | -26,090 to -22,222 |
| Native, 6,000/h | 3 (diag.) | no (peak ≤642) | 2067.6 | product-minus-25 | +13.1% [+12.4, +13.7] | +1.2% [+0.6, +1.8] | -0.429 [-0.450, -0.408] | 2.09% → 2.12% | — |
| Native, 6,000/h | 3 (diag.) | no (peak ≤559) | 2067.6 | product-plus-25 | -8.4% [-8.7, -8.0] | -8.6% [-9.1, -8.1] | -0.392 [-0.409, -0.376] | 2.09% → 1.98% | — |
| Native, 6,000/h | 3 (diag.) | no (peak ≤559) | 2067.6 | menu-plus-50 | -54.6% [-55.2, -54.0] | -30.4% [-31.2, -29.6] | -1.876 [-1.906, -1.847] | 2.09% → 1.65% | -54,864 to -51,429 |

Three things are settled by this table.

1. **Markup now costs demand, at non-binding capacity.** With the native menu and capacity that never binds (peak ≤594 served/hour against 6,000), +50% loses **26.0%** of quantity [25.6, 26.4] and revenue rises **11.2%**, not ~50%. Arc elasticity is **−0.748** against −0.000540 in the first run. Funded home meals absorb the lost demand. The sole-shop profile gives the same result (−25.8% quantity, +11.6% revenue).
2. **Wallets are not the mechanism.** Unaffordable refusals move from 2.07% to 2.16% of evaluated offers in that arm. C9 prices are unchanged at $5/$12. The response comes from the soft price factor.
3. **Capacity-bound arms cannot test price.** At 60/hour the shop rations excess demand, so +50% loses only 1.6% (legacy) or 6.2% (native) of quantity and revenue rises 34.6% or 41.4%. Any choice rule with excess demand at peak gives this. The legacy menu also sits under the $12.50 reference, where the 4× discount ceiling holds most of its weight even at +50%, so even at 6,000/hour it loses only 5.6%.

**Why the gate stays red at default weight.** With all non-price terms fixed, an offer's weight has elasticity −2 at neutral sensitivity. The shop's share of a meal falls by less than that, by about the factor (1 − own share). In the native non-binding control the shop takes 56% of resolved meals in its catchment (1,614 of 2,871 on day 21 of seed 0), home 37% and the rival 6%; the rival is held at its estimated 24/hour limit. That leaves a market elasticity near −0.75 for the +50% arc. Below unit elasticity, revenue rises with price. With any non-negative wholesale cost the quantity loss only widens the margin advantage, so the certificate's minimum is the revenue difference at zero cost, and it is positive in every seed-week ($12,750–14,815). This is the profit-maximizing response of a firm facing inelastic demand. The criterion, read through the wholesale box, needs elasticity beyond −1 at the measured point.

**Price-weight diagnostic.** The existing setting moves that point. At weight 1.5 the native non-binding +50% arm loses 36.5% of quantity, revenue falls 4.2% and the certificate is negative in all ten seed-weeks. At weight 2: −44.6% quantity, −15.9% revenue. At weight 3: −54.6%, −30.4%. At weights 1.5 and 2 the single-product −25% arm gains quantity but loses revenue; at weight 3 its revenue rises 1.2%. These are measurements of an E5 setting, not a default change: the F6h plan's calibration write-back covers rival lanes/units, missed-work k and C9 prices, not price weight.

### Household control band and thresholds

Derived from the canonical control, week 6, mean ±2 sd, rounded outward. [acceptance.py](../../tools/economy-interventions/acceptance.py) now checks control against these values and a Python test pins them to the committed evidence. A rerun that changes the canonical band has to re-derive them.

| Week-6 control metric | Mean | Seed band | Per seed 0–4 | Threshold |
| --- | ---: | --- | --- | --- |
| Arrears share | 0.188% | [−0.084%, 0.461%] | 1, 7, 5, 1, 4 owners of 1,914 | ≤ 0.47% |
| Unfed owner-days in the week | 2.0 | [−1.16, 5.16] | 2, 4, 3, 1, 0 | ≤ 5.2 |
| p10 balance | $691.43 | [$656.75, $726.10] | 690.64, 684.59, 682.69, 677.87, 721.34 | ≥ $656 |

p10 does not fall every week in any control seed: it rises to week 3 ($741.69 mean) and declines after, as in the first run (week 6 then $688.37, band [$654.61, $722.13]). Arrears are 1 to 7 owners. Six weeks still do not prove an indefinitely stationary distribution.

These thresholds belong to the canonical fixture. **Flagged:** every non-binding profile fails them, because people buy about four times as many shop meals (1,914–2,446 a day against 482) and spend down. Native non-binding control week-6 arrears are 1.087% (15–28 owners), p10 falls from $614 to $472, and p10 falls every week in seeds 1 and 3. The capacity-bound native profile (p10 $668, arrears 0.282%) also exceeds the arrears threshold because its menu costs more. These profiles are price-response fixtures, not household-sustainability evidence, and the drain does not cause the price result: refusals stay near 2%.

### Profiles and defaults

| Setting | Before | After this rerun | Reason |
| --- | --- | --- | --- |
| Rival lanes/service | 2 × 12/hour | Unchanged | No write-back evidence; native throughput stays rig check 9. |
| Rival units | 60/meal period | Unchanged | Same. |
| Missed-work k | 0.5 | Unchanged | Employment arms recover in 3 days with income effects outside the band. |
| C9 prices | $5 / $12 | Unchanged | Not used to produce price elasticity. |
| `FoodChoicePriceWeight` | 1 | Unchanged | Outside F6h's write-back list. Weight 1.5+ passes the certificate in the diagnostic; this is reserved for Peter. |
| Household control thresholds | None | Derived above | Canonical fixture only. |

### Options for the reserved decision

1. Raise the default `FoodChoicePriceWeight` to 1.5 or 2. This is the existing setting and needs no schema or formula change. Measured: the +50% menu loses revenue in every seed. Cost: every price signal gets stronger, including discounts, and native baseline own/home shares at native prices are still unmeasured (rig check 2).
2. Raise the funded-home weight (`FoodChoiceHouseholdWeight`, default 1). A larger outside share raises market elasticity at the same policy exponent. Not measured in this rerun.
3. Revise the criterion. For example, accept a menu markup that loses a seed-banded share of quantity while revenue rises well under the price rise, instead of profit non-dominance over the zero-to-retail wholesale box. The box needs elasticity beyond −1, which no firm with substantial market share has in standard economics.

Not proposed: another price formula, wallet exhaustion through C9 charges, or a persisted field.

### Reproduce

Use the task's Homebrew environment, check `pgrep -f testhost` first and run each line in the foreground, one at a time.

```sh
dotnet run --project tools/economy-interventions -c Release -- build/f6h-rerun-full --integrated
dotnet run --project tools/economy-interventions -c Release -- build/f6h-rerun-nb-native --integrated --meals-per-hour 6000 --menu native
# other profiles: --meals-per-hour 6000; --menu native; add --no-rival; add --price-weight 1.5|2|3
python3 tools/economy-interventions/acceptance.py build/f6h-rerun-full --output build/f6h-rerun-full/acceptance.json   # exits 1
python3 -m unittest discover -s tools/economy-interventions -p test_acceptance.py
```

After the first warm run (7.3 s), each canonical six-week run took 4.8–5.5 s. The 6,000/hour profiles took 4.4–6.2 s after their first run (up to 8.4 s). That is tool time, not mod-work.
