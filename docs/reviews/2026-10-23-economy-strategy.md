# Economy direction review — 23 October 2026

Peter requested an independent Claude Opus review and an independent Astra review, with discussion rounds among both reviewers and the lead. The overarching goal is a realistic economy that makes gameplay significantly more realistic, complex and fun. This is a strategy review, not a new implementation, deployment or test result. Subsequent authorized implementation and test evidence are tracked in the [neighborhood economy report](2026-10-23-neighborhood-economy.md).

## Outcome

All three reviewers explicitly agree on the exact consolidated plan after three rounds. The next increment is a bounded existing-stop food-purchase experiment. The first playable neighborhood economy additionally requires persistent household accounting, meaningful multi-day business choices and acceptable normal-play performance and pacing. Household finances come before a general detour planner or broader categories.

| Reviewer | Session/model evidence | Final position |
| --- | --- | --- |
| Lead | Own independent first assessment, reconciliation and final review | AGREE |
| Astra | Fresh context, requested `gpt-6-astra`, three rounds | AGREE |
| Opus | One new continuing CLI session; all three provider usage records identify `claude-opus-5-5` | AGREE |

Both external reviewers received the same first-round brief, then identical reconciliation/final packets. First-round assessments were produced before reading the other assessments. The brief summarized the current implementation and sourced native/offline results, with frozen source documents retained. Opus used the supplied packet with tools disabled; this was not an independent code audit or test run.

## What the review changed

The earlier suggestion to follow existing-stop purchases with on-route takeaway is narrowed: detours now need evidence of an access bottleneck, and household finances take priority over general route expansion. Opus initially bundled detours and a provisional wallet into the next increment; the final plan separates the small mechanics experiment from the playable milestone. A wallet without essential expenses and a priced subsistence alternative is insufficient.

All reviewers prioritize understandable business consequences over more visits or a target sales count. A second location must earn its local market; there is no protected chain, guaranteed viable shop, prescribed winner or forced loss. The review also makes management information, actual costs and the slower calendar's player feedback time part of the milestone. Synthetic scaling and advancement p95 alone cannot certify normal play.

A mistaken inference during discussion was explicitly corrected: purchases may increase under unchanged finite meal needs because previously unmet needs are fulfilled. Substitution for standalone trips is possible, not a required quota. Several days are used to examine variation/robustness, without claiming the existing sample size proves statistical noise. Approximately 1,000 persistent residents does not mean 1,000 simultaneously rendered bodies.

## Evidence and authorization limits

The factual starting point is the [optional-destination PC comparison](2026-10-23-optional-destinations.md) and [calendar Mac comparison](2026-10-22-city-clock.md), with their recorded limitations. No new game runs, production code changes or normal-save changes occurred during this review. This records reviewer consensus in response to Peter's request; it does not claim a new implementation or release approval from Peter.

Detailed shared inputs, independent assessments, round responses, final approvals, actual Opus model/session receipts and source hashes are retained locally under ignored `build/economy-strategy-20261023/`. The approved plan body below has SHA-256 `213ae71fd4e1e459a1a2dbca4b343eda020658e879c9f94006212db2efc07a8d` in its original `final-plan-proposal.md` file.

---

# Agreed final plan: a playable neighborhood food economy

## Goal and judgment
Persistent residents should make business management more realistic, complex and fun: different local markets, consequential offers and costs, useful expansion choices, and understandable success or failure. Keep persistent identities/journeys, bounded simulation, native sale/stock/accounting ownership, and the one-third calendar as the candidate foundation. Reviewer agreement supports a direction, not proof of calibration, profitability, performance or fun.

The next implementation increment is a bounded existing-stop food-purchase experiment. The next PLAYABLE milestone combines that with persistent personal/household accounting, interpretable multi-day business choices, and normal-play usability. Do not call the initial experiment a complete economy.

## 1. Connect needs to existing shopping activity, in a bounded experiment
First inspect the opportunity funnel from existing traces/diagnostics where available: shopping activities, actual eligible food-shop visits, pending meal intentions, preference, affordability, hours/service/stock. Keep nearby opportunities requiring a new detour separate from already-present visits. Sparse food visits may reflect generic destination selection rather than a shortage of demand or feasible travel; diagnose purpose, access and affordability separately.

Then let suitable existing shopping activities satisfy the SAME pending food need and spend from the SAME finite allowance as standalone meal outings. Food needs and offers may influence relevant food-shopping candidates within the existing bounded destination choice; every generic shop visit is not a food purchase. Reuse actual journey/admission and native transaction ownership, with no added identities, needs, demand multipliers or duplicate sales. Purchases cannot reset spending or needs through reload.

Use a small enrolled player-shop scope, comparable starting conditions and several ordinary days. One meaningful local comparison plus an offer change is enough for this increment; do not build a large tuning campaign against temporary allowances. Show understandable aggregate purchase/decline/unmet reasons and observed costs. Known net results can be reported only with the relevant costs actually included; otherwise label contribution and missing costs. These are conditional mechanics/directional findings, not calibrated long-run demand or sustainable profitability claims.

Finish with correctly owned native purchases and an interpretable local response, OR a clearly measured blocking assumption. If existing-stop integration needs a broad itinerary rewrite, move household accounting ahead. A small nearby-stop experiment is justified only by an observed access bottleneck; a general detour planner is deferred.

Crucial interpretation: with unchanged finite meal intentions, purchases MAY rise because previously unmet needs can now be fulfilled. Attached buying can also substitute for standalone outings. Check each need's outcome and journey ownership; neither preserving the old sales count nor requiring a particular amount of substitution is a success target.

## 2. Give spending a persistent economic constraint
Immediately next, before broad categories, general detours or claims of a playable economy: balances that persist across days, explicit recurring income, essential expenses and a priced cheaper home-food alternative. A simple accounted household food basket is enough; physical grocery trips and cooking can wait. A wallet alone with another daily income stub is not the finish line.

Start from explicit resident budget units unless actual household relationships justify pooling. Define support for non-earners; do not infer a family or identical income from co-residence. Keep the representation simple enough to affect decisions without simulating domestic life in detail.

Where native payroll, stock replacement and operating costs already exist, preserve that authority. Work destinations alone are not jobs. Unrepresented employers, essential providers and imports may remain declared external flows; replace provisional allowances with a documented income/expense model without claiming those funds were earned in a simulated employer ledger. The milestone is household accounting within an OPEN local economy, not a self-contained city in which every wage originates from modeled shop revenue. Wider employment/payroll connections remain an explicit next stage.

## 3. Prove meaningful business choices across days
Repeat the market experiment after persistent finances. Compare one small shop, two separated small shops, two overlapping small shops, and a larger outlet where useful. Select a few price, service and opening-hour changes. Count real investment/rent/staffing/stock costs; equal nominal capacity does not mean equal cost.

Seek explainable responses: added local reach versus added cost, pricing versus affordability, service quality versus expense, longer hours versus local demand, capacity versus real service bottlenecks. A second shop has to earn its role; ownership provides no protection. An unsuitable offer may fail, and a superior offer may dominate a sample. Do not tune the model until prescribed winners, sales totals or guaranteed losses appear. The one-third clock supplies more travel time, not a customer guarantee.

Give the player a concise management view of who the shop serves, when demand appears, why willing customers were lost, and which constraints matter. Distinguish declined purchases, unmet needs and unavailable evidence. Aggregate bounded observed decisions honestly; they are not an exhaustive count of all counterfactual city demand.

## 4. Gate the playable milestone on performance and fun
Measure normal rendered play at roughly 1,000 persistent residents with the normal visible-body budget, not 1,000 simultaneous rendered actors. Include route preparation, frame pacing/spikes, allocations, save/load and time-skip catch-up; simulation-advance p95 alone is insufficient. Use measured whole-game baselines and practical budgets. Synthetic 2k/5k checks remain regression evidence, not native scale clearance.

Peter should be able to predict a plausible effect of a business change, understand the observed outcome, and want to try another informed decision. The slower calendar's feedback and waiting time must fit an enjoyable session; history/summaries and fast-forward behavior deserve attention if strategic consequences take too long to see. Reviewers cannot certify fun without that play evidence.

## 5. Expand only after that loop works
Add justified takeaway detours and further needs/categories in bounded slices, connect more native employment/payroll and supply costs, then give rivals financial responses and eventually hiring, investment, openings and closures. Sequence refinements should follow observed constraints and player value. Preserve the open-economy boundary where outside employment/trade is appropriate; do not force economic isolation.

## Assumptions that remain experimental
Eligible opportunities, willingness to pay, budget distributions, sustainable shop economics, rendered performance and enjoyable pacing remain unproven. Changes after adding household finances may legitimately change rankings; investigate rather than treating reversal as automatically wrong. Repeated comparable days assess routine variation and robustness; the current sale count alone does not establish a statistical noise claim. No game, production code or normal save changes are part of this strategy review.


---

## Implementation checkpoint — 23 October, bounded menus

The [0.9.0 menu increment](2026-10-23-food-menus.md) is complete as a disabled private candidate after 1,110 offline checks, eight reconciled native Mac days and seven source/evidence rounds with Opus 5.5. Existing-stop ownership, individual budgets and three meal opportunities now support a small verified product menu. Overlapping shops compete, products serve different meals, and a higher burger price creates substitution. Native illness and overly uniform product tastes constrain interpretation; whole-game stutters remain.

The next agreed gate is real replenishment through native suppliers, practical staffing/opening hours, distinct unstaffed/sick feedback and cash runway over several days, followed by a short normal-play test around meal peaks. Keep native absences. Stable individual food tastes are a likely next refinement after that operational loop. Broader categories, general detours, rival finances and payroll remain deferred. This checkpoint does not amend the original agreed plan body above or declare the full playable-economy milestone complete.
