# Neighborhood food economy — experimental candidate

The economy links ordinary shopping stops and optional meal outings to the same breakfast, lunch or dinner intention. A resident can buy at most once per meal, sharing one wallet across the day. Arriving at a relevant shop can make an earlier rejected offer worth reconsidering, because the extra trip is no longer needed. Service and eating must fit the visit already scheduled. A paid purchase does not create another appetite or journey.

This is an open local economy. Each resident has an individual account; sharing an address does not create a family or shared money. External employment, pensions and support provide declared inflows. Housing, other essentials and a home-food basket consume money. Selected shops still use the game's stock, staff, orders, rent, wages and daily financial statements. Work destinations are routine assignments, not evidence of a native employment contract.

## Using the candidate

The mod remains disabled by default. In an enabled test city, Ctrl+F8 opens the neighborhood economy panel. The 0.9.0 development candidate can enroll up to eight player-owned, full-service shops whose entire advertised menu uses the supported stocked products: croissants, burgers and hot dogs. Native furniture and business-type restrictions still apply. Start resident purchases and budgets explicitly. An existing allowance-based trial can enable budgets after current purchases finish. Activation belongs to that saved city and is never automatic on migration.

Use the normal business controls to change prices, staffing and hours. The enrolled shops’ routine hours refresh from the native schedules; this does not refresh the entire city catalogue. The panel shows evaluated offer outcomes, sales by product and meal, customer home districts, purchase hours and settled operating results. An unsupported menu change pauses resident sales at that shop and is shown in the panel; restoring a supported enrolled menu allows sales to resume, including after reload. Counts of offers include repeat decisions; they are not unique prospective customers or an estimate of all district demand. An unavailable route or supply observation remains unknown evidence. Gross sales and native operating profit are different measures; investment, debt and personal taxes require separate consideration.

The initial enrollment is a fixed, narrow shop scope. Full live business-catalogue updates, adding arbitrary new product categories and rival investment/closure behavior are later work. Use a saved city with the intended businesses established for this comparison. Disable/remove the mod only through the existing documented load procedure; no normal save is modified by the test campaign.

## Bounded menus — 0.9.0 / schema 12

Residents choose one (shop, product) offer for the current meal. Stock reservations belong to the chosen product; all products at the same shop share the service queue and bags. Checkout rechecks supply, price and staff before committing a native order. There is no separate capacity allowance per menu item. Empty or unsupported shops do not trigger unnecessary pathfinding. Known impossibility now takes priority over missing route evidence, so some earlier unresolved decisions are classified as home-food declines; this is a reporting/decision clarification, not extra spending.

The provisional mismatch values below are added to perceived cost, never to the charged price. They are gameplay assumptions, not measured nutrition or a promised sales mix.

| Product | Breakfast | Lunch | Dinner |
| --- | ---: | ---: | ---: |
| Croissant | $0 | $3.50 | $12 |
| Burger | $12 | $1 | $0 |
| Hot dog | $9 | $0 | $4.50 |

Schema 12 retains the original scoped product and adds a bounded list of other products. Older single-product history is attributed to its original product. Pending trips, uncertain checkouts, receipt keys and held money retain their ownership. A live business is never granted a menu or producer that the game disallows. The native comparison uses a café and a fast-food restaurant, not an invented combination of equipment. Eight native Mac days reconcile 667 purchases, with source/evidence review by Opus 5.5. Product choices remain too uniform for realistic taste diversity; native sickness confounds two scenario averages and rendered frame pacing remains poor. See [the menu report](reviews/2026-10-23-food-menus.md) for daily results and limits.

## Breakfast, lunch and dinner — meal policy 1

The 0.8.0 candidate adds three optional prepared-food opportunities per resident per day. Each resident still has one scheduled meal record and one wallet. A new meal never creates income or refreshes that wallet; the paid household basket covers the day's home-food alternative. The older temporary-allowance mode also shares its daily limit across meals. These are discretionary choices, not three guaranteed customers or a nutritional simulation.

| Meal | Preferred time | Opportunity window | Eating time | Preferences |
| --- | --- | --- | --- | --- |
| Breakfast | Wake +30 minutes, personal ±15 and daily ±10 | 90 minutes | 10 minutes | Faster, cheaper, short queues; up to 45 minutes for the whole outing |
| Lunch | Wake +5 hours, personal ±30 and daily ±15 | 120 minutes | 20 minutes | Convenience and the available work/study break; existing 45/60/75-minute flexibility |
| Dinner | Wake +11.5 hours, personal ±45 and daily ±15 | Up to 150 minutes, ending before sleep/midnight | 30 minutes | More weight on service quality, less on travel time; up to 90 minutes when free |

Waking and sleeping already vary by persona, resident and day. The dinner target moves earlier if necessary to leave at least 90 minutes before bedtime or midnight. Work, committed travel, opening hours and remaining activity time can shorten or prevent any opportunity. Meal windows do not overlap. Stable hashing keeps habits and daily variation identical across reloads and update sizes.

Compared with that resident's existing preferences, breakfast values time at 1.3× and service quality at 0.6×; dinner uses 0.8× and 1.6×. Breakfast willingness to pay for convenience is 0.55× the lunch baseline. Dinner uses 1.25×, or 1× for parents. Individual income, persona, travel preferences and district product demand still matter. These weights are provisional gameplay assumptions, not measured real-world estimates.

The earlier 0.8.0 comparison offered only croissants. A croissant carries a $3.50 perceived mismatch cost at lunch and $12 at dinner, with no penalty at breakfast. This affects choice only: its price and the native charge remain unchanged. The 0.9.0 catalogue extends this explicit suitability model to burgers and hot dogs; uncatalogued products cannot enroll through the native adapter. Dinner demand should not automatically make a pastry shop a successful restaurant.

Schema 11 retains the exact pending, travelling, paid or uncertain single-meal intention from older saves, including its receipt key. After that legacy day, the resident switches to breakfast/lunch/dinner. New receipts include the meal and day. An uncertain checkout retains its identity and held money; later meals cannot overwrite or retry it. Daily history reports purchases by meal alongside hours and districts. The detached food-shadow experiment retains its previous single-meal contract as a historical reference.

## Budget policy 1

Amounts below are explicit design assumptions in game dollars, not measured local household economics or actual native wages. The saved district class mix determines a stable income band, with fallback weights of 40% working, 45% middle and 15% upper. Variation within each band is stable by identity.

| Daily amount | Working band | Middle band | Upper band |
| --- | --- | --- | --- |
| Employment income | $95–180 | $150–300 | $240–500 |
| Housing | $30–60 | $45–90 | $70–150 |
| Other essentials | $15–25 | $15–25 | $15–25 |
| Home-food basket | $9–15 | $9–15 | $9–15 |

Pension income is 70% of the sampled amount. Under-18 residents, students and residents without a day assignment receive explicit outside support at 60%. These are individual budget units; no parent-child relationship or native job is inferred. Seven days of income are supplied once as declared opening savings. Daily income is posted once, the home-food basket is paid first, and remaining funds pay housing, essentials and arrears. Unpaid bills remain debt rather than negative cash or newly created money.

Discretionary purchases reserve the following day's essentials and require no unpaid arrears. Price also competes with a funded home-food alternative, personal convenience preferences, available time, travel, customer-service quality and the district product-demand signal. A large bank balance alone does not make every price attractive. The home-food basket is accounted food provision; physical grocery trips, pantry contents and cooking are not modeled yet.

At checkout, purchase funds are held before the game is called. A confirmed native sale debits the account; a confirmed refusal refunds the hold. An uncertain native outcome retains the hold and blocks further discretionary purchases, preventing an automatic duplicate charge or sale. Migration never silently enables budgets. Reloads retain balances, posting dates, pending intentions and receipts.

## Performance and persistence

Budget postings share the simulator's bounded event budget and use a due queue. An arrival wakes only that resident's relevant food decision. The system does not scan all residents for shopping opportunities every frame. Management history retains fourteen days for at most eight shops; the panel builds summaries only when opened and refreshes every three seconds.

The normal visible limits remain 48 pedestrians and 24 vehicles around the player. Approximately 1,000 persistent residents does not require 1,000 rendered bodies. The one-third calendar changes available game time while retaining movement pace; it is not a sales multiplier. During a hidden time skip with an enrolled economy, the calendar waits for budgeted resident processing before crossing each native accounting hour. This preserves current-hour purchase ownership without issuing historical sales. The hidden skip candidate allows up to 128 events under a soft 16 ms advancement budget per frame; ordinary play retains its existing 128 total-event, 16 resident-event and soft 2 ms limits. Individual native route calls cannot be preempted and can exceed the soft budget. A three-day native Mac check preserved every resident budget, purchase count, native stock and operating statement, while reducing measured skip time by about 72% (roughly 29–34 seconds/day, including diagnostics). The first route-discovery pass while paused also has a soft 16 ms budget with the existing 2,048-step work limit; recurring and ordinary discovery retain 1 ms. Ordinary fast-forward, hidden time skips and normal rendering are assessed separately.

## Validation status

The current **0.8.0 / schema 11** meal-rhythm candidate passes **1,088 tests** and six native Mac days. The all-day small shop averages 15 breakfast, 39 lunch and 26 dinner purchases. Stock, cash, native accounting, saved receipts and reloads reconcile; the rig is restored and released. See the [meal-rhythm review](reviews/2026-10-23-meal-rhythm.md) for preferences, timing, measured results and limits. The following paragraph records the preceding 0.7.0 neighborhood campaign; its rendered measurements were not repeated for 0.8.0.

The model and native adapter compile against the local game assemblies with warnings treated as errors. The implementation passed 1,073 offline tests, including cash conservation, refunds and uncertain outcomes, posting cadence, reloads, existing-stop intent ownership, bounded management history, and clock backpressure under limited event budgets. All eight native Mac business cases pass across seventeen game days, and the faster-skip comparison preserves the complete measured economic state. Three focused rendered samples show no clear aggregate slowdown at 957 residents, but whole-game spikes remain. Native fast-forward/resume, live hours, save/reload and the final management panel are checked. The final paused reload took about 103 seconds in this full modpack, so loading remains a usability concern. Allocation counters and GPU timings were unavailable; these tests do not establish smooth performance on every machine. See the [implementation review](reviews/2026-10-23-neighborhood-economy.md) for measured results and limits; offline checks alone do not certify gameplay economics, normal rendered performance or fun.
