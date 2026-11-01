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
B. Memory, loyalty and regulars. **B0 is implemented offline:** schema 18, exact old-source migration and history-inclusive capacity; memory remains empty/inactive and the regulars helper is a pure own-shop projection. **B0b is inserted between B0 and B1:** persist the held A3 tuning image and neutral bounded per-candidate contribution; schema 18 is amended before player use. B1 is implemented offline on B0b: per-consumer generation memory, atomic experience watermarks and captured bounded loyalty/curiosity weights; 6 Nov native proof remains pending. B2 is implemented offline on B1: unique-consumer own-shop regular counts, inspector memory words and read-only bridge flags/counts, with zero saved growth and no choice contribution. Default Q7 thresholds are three served visits, positive effective memory and fourteen days. Native proof remains pending the combined 6 Nov session. [B2 checks](checks/b2-regulars-inspector.md#step-b2-regulars-and-inspector-words---31-october-2026-offline-only). [Contract and capacity evidence](population-schema18.md).
   B1 protected-insertion exception: if recording a new bad visit would evict a competing boost, return chance stays equal (never higher), with a terminal failed watermark and no negative per-shop entry. B2 must not assume every failure leaves a negative entry; `ProtectedInsertions` is session-local, not saved history. [Exact exception](population-schema18.md#b1-execution--31-october-2026).

C. Marketing awareness and targeting, plus the market research product. **C2a implemented offline on B0b:** bounded source/index interface, B1 memory projection/campaign-unknown providers, local/routine and positive daily distant discovery, common eight-slot shortlist, additive schema 19. B1 memory is wired within the existing four source slots and bounded rank; r1 bounds rebuild continuations and restores at-stop observation/diagnostics and strict held-image validation. Combined 6 Nov rig proof pending. [Contract](population-schema19.md). **C4 implemented offline and inactive:** bounded pure research engine/access contract only; paid research stays off because C3 found no safe native fee path. No UI, normal-play computation or saved fields. [Contract and offline proof](market-research.md).
   C2b (1 Nov, offline): hourly observed campaign images, bounded source/index and schema 22 implemented; Peter's 1 Nov rule now applies active preference/softer distance for all residents; final-head rig proof pending. [Checks](checks/c2b-campaign-awareness.md).
D. Rivals as alternatives.
E. Weather, street life, bigger menus and more products.
Then: re-check the same performance gate for the final release, the hands-on session, and one release.
Later: residents as employees, households.

Implementation plan for B–E: [memory, marketing, rivals and the wider street](plans/steps-b-e.md).

E3 status, 31 October (offline): [donut](plans/e3-donut.md) and [cupcake feasibility discovery](plans/e3-cupcake.md) share one isolated on-demand raw-field reader with pinned candidates. Loaded equipment and natural checkout/settlement proof remain pending; no additional product certified or enabled.

E3 salad status, 1 November (offline): [salad stop record](plans/e3-salad.md) adds a pinned conditional probe on the cupcake/donut stack. Shipped holder unestablished; no generic holder extension, menu, gameplay or saved change. Existing read branches retained; no additional cheap safe measured perf win identified. Three-candidate offline timing and the combined 6 Nov handoff are in [CHECKS](checks/e3-salad.md#e3-salad-feasibility---1-november-2026-offline-only).

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

- C2b - [Campaign index reuse, canonical sorting, shared references, active-hour buffers and neutral marker cache](perf/c2b-campaign-awareness.md).

- A2 — [Schema 17, lifestyle traits and base sync evidence](perf/a2-schema17-traits.md).
- A3 — [Weighted food choices, review fixes and native checklist](perf/a3-weighted-choice.md).
- B0 — [Schema 18, capacity, review fixes and native checklist](perf/b0-schema18.md).
- B0b — [Held tuning, base syncs and native checklist](perf/b0b-held-tuning.md).
- E3 — [Donut feasibility reader](perf/e3-donut.md).
- C1 — [Native campaign reader and native checklist](perf/c1-campaign-reader.md).
- D1 — [Read-only rival food catalogue and native checklist](perf/d1-rival-catalogue.md).
- F0 — [Interior view experiment, reviews and native checklist](perf/f0-interior-view.md).
- E2c — [Third-party interior guard](perf/e2c-interior-guard.md).
- B1 — [Shop memory and loyalty v2](perf/b1-memory-loyalty.md).

- B2 — [Observed own-shop regulars and resident memory words](perf/b2-regulars-inspector.md).

- C2a — [Core bounded awareness and yielded index publication](perf/c2a-core-awareness.md).
- E2b — [Bounded street-life timing and fail-closed frontage ownership](perf/e2b-street-life.md).

- E3 cupcake — [Shared reader timing and allocation evidence](perf/e3-cupcake.md).

- E3 salad — [Conditional shared reader timing and allocation evidence](perf/e3-salad.md).

- C4 — [Bounded inactive research and direct trait validation](perf/c4-market-research.md).
