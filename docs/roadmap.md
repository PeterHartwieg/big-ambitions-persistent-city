# Persistent City roadmap (Peter's answers, 2026-10-30 15:17–15:24 JST, guided Q&A with Spock)

Replaces the phase order in docs/reviews/2026-10-27-realism-gameplay-roadmap.md where they differ. Same principles still apply: bounded state, no extra route searches per decision, saved state replays identically after a reload, native accounting stays authoritative, nothing is written to rivals.

## Vision
"Both equally: a living city that also drives my business." It should feel like a deep optimization game, not formula numbers you work out in the first 5 hours.

## Product decisions
1. **Choice is probabilistic, not a fixed score.** Preferences raise the chance for an item. Distance raises the chance for nearby shops but never rules out distant ones. Keep it deterministic per save through seeded draws, so a reload replays the same day.
2. **Resident inspector first.** Click a resident to see where they live and work, what their preferences are, and what they ate or skipped and why.
3. **Individuals are described in words, not numbers** (e.g. "loves burgers", "price-sensitive", "hates waiting"). City-wide statistics come from **paid market research through marketing agencies**, which gives players a new thing to spend money on.
4. **Traits, core plus lifestyle:** food preferences, price sensitivity, patience (queues), loyalty vs curiosity, willingness to travel, health-conscious, brand or quality focus, time pressure (commuters).
5. **Memory is driven by the loyalty trait.** Loyal residents keep going back to the same shops, curious ones try new places. Bad visits (stockout, closed, queue) lower the chance of returning, weighted by traits. Memory stays bounded, about 4 shops per consumer. The slice re-measure checks the cost.
6. **Marketing means awareness plus targeting.** Campaign types reach different resident groups (commuters, health-conscious, ...). Awareness only makes a visit possible; traits still decide.
7. **Rivals are real alternatives.** Residents can choose them over Peter's shops. Rivals are read-only with an outflow line, and nothing native is written.
8. **Weather, street life and bigger menus come after the core loop works.**

## Order
**Peter 15:25 JST: "Release will only happen at the end."** There's no interim release. Steps A–E build on the development line (still merged through the normal gates), and the single Workshop release comes after the full roadmap. Performance stays a standing requirement that is re-checked as features land, and it must pass before that final release.

0. Performance: the 30 October comprehensive review is done and passes the mod-work gate below. Spock agreed: no optimization work now; re-measure as each slice lands. This isn't a release step.
A. Inspector, traits and probabilistic choice. Start now (Peter 15:24 JST), with the standing performance gate applied to each slice.
B. Memory, loyalty and regulars. **B0 is implemented offline:** schema 18, exact old-source migration and history-inclusive capacity; memory remains empty/inactive and the regulars helper is a pure own-shop projection. **B0b is inserted between B0 and B1:** persist the held A3 tuning image and neutral bounded per-candidate contribution; schema 18 is amended before player use. B1 resumes on top of B0b; B2 UI remains pending. [Contract and capacity evidence](population-schema18.md).

C. Marketing awareness and targeting, plus the market research product.
D. Rivals as alternatives.
E. Weather, street life, bigger menus and more products.
Then: re-check the same performance gate for the final release, the hands-on session, and one release.
Later: residents as employees, households.

Implementation plan for B–E: [memory, marketing, rivals and the wider street](plans/steps-b-e.md).

Peter’s [31 October Q1-Q7 decisions](plans/steps-b-e.md#product-decisions-peter-31-oct-2026) specify tiered paid research as a third native-agency option (proven fee or unavailable), local/distant discovery, estimated rivals, food-first products, no anonymous crowds/night multiplier and own-shop regulars; tier contents/fees remain open.

Track F (Peter, 31 October): independently authored city view from interiors, F0 spike → F1 shop view/customer entry → F2 upper-floor/penthouse view → F3 polish. [F0 findings and plan](plans/track-f-interior-view.md): default-off runtime eligibility/entrance-anchor experiment; **city drawing is a no-op pending F1**, with no save-format change or A-stack dependency. E2c (31 Oct, offline) implements only the once-per-load third-party detection guard, forced-off F0 state and read-only bridge verdict; native proof remains pending. [E2c evidence](checks/e2c-interior-guard.md#e2c-third-party-interior-guard---31-october-2026-offline-only). The same mod-work/whole-frame evidence rules apply to F.

## Performance gate (Peter, 2026-10-30)

Applies to every step A–E slice re-measure and to the final release:

- **Mod-work p95 must stay under 5 ms**, leaving a buffer under the 10 ms frame budget for 100 fps.
- Single mod-work spikes **under 30 ms are acceptable**. Flag every mod-work spike **of 30 ms or more** in the re-measure report.

**Baseline: PASS.** The [30 October review](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/1273abd9a8ec23017deb12cbfe181f9f891b2682/docs/reviews/perf/2026-10-30-performance-review.md) on `codex/pc-perf-review-2026-10-30` records primary dependency-only mod-work p95 **1.259 ms** (highest pooled condition; worst individual window **2.420 ms**) and worst mod-work spike **24.0 ms**. Both pass. If mod p95 climbs, look first at hotspot #1, physical population (**p95 0.64 ms / max 21.7 ms**).

**Whole-frame watch item.** Unexplained whole-frame stalls are tracked separately from the mod-work gate, including the review's four **104–175 ms** evening stalls. Their cause remains unresolved.

**Standing practice.** Being under 5 ms is never a reason to skip obvious improvements. Every slice's implementation and review must look for cheap, low-risk performance wins (redundant per-frame work, caching, avoidable allocations, refresh rates that can safely drop) and take them in that slice when low-risk and measurable. The review's four hotspots—physical population, closure refresh, optional-stop refresh and save serialization—stay parked as high-risk unless a cheap, safe variant turns up, such as lowering closure or optional-stop refresh frequency where correctness allows.

### Perf wins log

Record every taken win with before/after numbers.

Each slice records its wins in its own `docs/perf/<slice>.md` and adds one index line here. Keep before/after numbers and measurement limits in that file; sync/review-fix notes stay with the slice.

- A2 — [Schema 17, lifestyle traits and base sync evidence](perf/a2-schema17-traits.md).
- A3 — [Weighted food choices, review fixes and native checklist](perf/a3-weighted-choice.md).
- B0 — [Schema 18, capacity, review fixes and native checklist](perf/b0-schema18.md).
- B0b — [Held tuning, base syncs and native checklist](perf/b0b-held-tuning.md).
- E3 — [Donut feasibility reader](perf/e3-donut.md).
- C1 — [Native campaign reader and native checklist](perf/c1-campaign-reader.md).
- D1 — [Read-only rival food catalogue and native checklist](perf/d1-rival-catalogue.md).
- F0 — [Interior view experiment, reviews and native checklist](perf/f0-interior-view.md).
- E2c — [Third-party interior guard](perf/e2c-interior-guard.md).
