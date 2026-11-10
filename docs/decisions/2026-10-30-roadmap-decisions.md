# Roadmap decisions, 30 October to 2 November 2026

Moved verbatim from docs/roadmap.md on 6 November 2026, when the roadmap was cut to remaining work. The A–E headline lines that carried slice status were moved into each slice's checks file instead. Release, merge-gate and migration rules made later are in [2026-11-06-delivery-rules.md](2026-11-06-delivery-rules.md).

## Persistent City roadmap (Peter's answers, 2026-10-30 15:17–15:24 JST, guided Q&A with Spock)

Replaces the phase order in docs/reviews/2026-10-27-realism-gameplay-roadmap.md where they differ. Same principles still apply: bounded state, no extra route searches per decision, saved state replays identically after a reload, native accounting stays authoritative, nothing is written to rivals.

### Vision
"Both equally: a living city that also drives my business." It should feel like a deep optimization game, not formula numbers you work out in the first 5 hours.

### Product decisions
1. **Choice is probabilistic, not a fixed score.** Preferences raise the chance for an item. Distance raises the chance for nearby shops but never rules out distant ones. Keep it deterministic per save through seeded draws, so a reload replays the same day.
2. **Resident inspector first.** Click a resident to see where they live and work, what their preferences are, and what they ate or skipped and why.
3. **Individuals are described in words, not numbers** (e.g. "loves burgers", "price-sensitive", "hates waiting"). City-wide statistics come from **paid market research through marketing agencies**, which gives players a new thing to spend money on.
4. **Traits, core plus lifestyle:** food preferences, price sensitivity, patience (queues), loyalty vs curiosity, willingness to travel, health-conscious, brand or quality focus, time pressure (commuters).
5. **Memory is driven by the loyalty trait.** Loyal residents keep going back to the same shops, curious ones try new places. Bad visits (stockout, closed, queue) lower the chance of returning, weighted by traits. Memory stays bounded, about 4 shops per consumer. The slice re-measure checks the cost.
6. **Marketing means awareness plus targeting.** Campaign types reach different resident groups (commuters, health-conscious, ...). Awareness only makes a visit possible; traits still decide.
7. **Rivals are real alternatives.** Residents can choose them over Peter's shops. Rivals are read-only with an outflow line, and nothing native is written.
8. **Weather, street life and bigger menus come after the core loop works.**

### Order
**Peter 15:25 JST: "Release will only happen at the end."** There's no interim release. Steps A–E build on the development line (still merged through the normal gates), and the single Workshop release comes after the full roadmap. Performance stays a standing requirement that is re-checked as features land, and it must pass before that final release.

0. Performance: the 30 October comprehensive review is done and passes the mod-work gate below. Spock agreed: no optimization work now; re-measure as each slice lands. This isn't a release step.
A. Inspector, traits and probabilistic choice. Start now (Peter 15:24 JST), with the standing performance gate applied to each slice.

Then: re-check the same performance gate for the final release, the hands-on session, and one release.
Later: residents as employees, households.
Implementation plan for B–E: [memory, marketing, rivals and the wider street](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/archive/steps-b-e.md).

E5 plan, Peter 2 November 2026: per-product implementation is superseded by the [dynamic loaded-catalogue plan](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/plans/e5-dynamic-products.md); the **18:02 JST decisions** record food-by-default admission after the full native contract, seeded per-resident tastes, **E5e mod metadata API**, deferred bundles and confirmed dumplings/chopped cheese cases. E3 records are regression evidence, with no E3 product proven in game. Docs only; no implementation or save change. Implementation and automated rig verification are separate statuses.

E6 / next scope: **cart and food-truck sales support** for products without a building holder (Street Food dumplings/chopped cheese), if RIG-CAT finds no building-placeable holder; per Peter's E5 Q6 decision, **planned, not started**, after E5 and outside its scope.

Peter’s [31 October Q1-Q7 decisions](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/archive/steps-b-e.md#product-decisions-peter-31-oct-2026) specify tiered paid research as a third native-agency option (proven fee or unavailable), local/distant discovery, estimated rivals, food-first products, no anonymous crowds/night multiplier and own-shop regulars; tier contents/fees remain open.

Track F (Peter, 31 October): independently authored city view from interiors, F0 spike → F1 shop view/customer entry → F2 upper-floor/penthouse view → F3 polish. [F0 findings and plan](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/plans/track-f-interior-view.md): default-off runtime eligibility/entrance-anchor experiment; **city drawing is a no-op pending F1**, with no save-format change or A-stack dependency. E2c (31 Oct, offline) implements only the once-per-load third-party detection guard, forced-off F0 state and read-only bridge verdict; native proof remains pending. [E2c evidence](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/e2c-interior-guard.md#e2c-third-party-interior-guard---31-october-2026-offline-only). The same mod-work/whole-frame evidence rules apply to F.
