# Persistent City roadmap

What is left before the single release, and the standing rules every slice follows. Done work is in [CHECKS.md](../CHECKS.md). Peter's product decisions of 30 October to 2 November (vision, probabilistic choice, traits, memory, marketing, rivals, release order) are in [docs/decisions/2026-10-30-roadmap-decisions.md](decisions/2026-10-30-roadmap-decisions.md); the 6 November delivery rules are in [2026-11-06-delivery-rules.md](decisions/2026-11-06-delivery-rules.md).

Principles that still apply: bounded state, no extra route searches per decision, saved state replays identically after a reload, native accounting stays authoritative, nothing is written to rivals. There is no interim release.

## Remaining

| Item | Status | Notes |
| --- | --- | --- |
| E5b complete-menu integration | partial | Dynamic native acquisition, dirty recovery and held-policy execution are not done. [E5b checks](checks/e5b-complete-menu.md). |
| E6 carts and food trucks | planned | Per Peter's E5 Q6 decision: cart and food-truck sales for products without a building holder (Street Food dumplings, chopped cheese), if RIG-CAT finds no building-placeable holder. After E5 and outside its scope. |
| Audit F5c-lite | planned (running) | Schema 26 person shape, refusing older development cities. Replaces F5c's migration design. |
| Audit F5d, F5e, F5g, F5i, F5h | planned | Independent schedules, workplaces and employment, population fill and visitor transport, visitor bodies after F2, integrated acceptance. [F5 plan](plans/f5-population.md#4-slice-breakdown). |
| Audit F6d to F6h | planned | Ledger contract, attended wages, household resources, change journal and feedback v2, multi-week calibration. [F6 plan](plans/f6-economy.md#4-slice-breakdown). F6d also owns the committed-meal profile change across reload left by [F6b](checks/f6b.md). |
| Audit F7p | implemented | Compact-format prototype, tools only; [decision: candidate C for F7d](decisions/2026-11-06-f7d-compact-format.md). |
| Audit F7c, F7d, F7e | planned | Snapshot/wire split, compact writer, overlap in the native save window (F7e conditional). [F7 plan](plans/f7-save-snapshots.md#4-slice-breakdown). |
| Re-scope plan rows for no migration | planned | F5d, F5e, F6d, F6e, F7c and F7d still describe migration or switch-over-day tests. The coordinator proposes the changes; Peter approves them in `docs/decisions/`. |
| Audit F2, F4, F8, F9 | planned, no plan yet | [City audit](reviews/2026-11-03-city-vision-audit.md): F2 presentation must not change journey authority; F4 the perf gate does not prove the requested experience; F8 capability boundaries for dependencies and native patches; F9 an integrated city acceptance path. |
| Batched rig run | planned | Runs every merged slice's "In-game checks for the next batched rig" against the development head, including audit F1's native accounting and complete-cost proof and A5's final-head matched proof. List: [rig-2026-11-06-checklist.md](checks/rig-2026-11-06-checklist.md) plus each checks file. Before the first refusing schema merges, the rig needs to build and age a fresh FT city at head. |
| Final performance gate | planned | Re-check the gate below on the final development head. |
| Hands-on session | planned | Peter's playtest, including A5's pointer and input checks 1–3 and check 12's interactive extension. |
| Single release | planned | One Workshop release after all of the above. |

## Deferred

| Item | Status | Reason |
| --- | --- | --- |
| C4 paid market research | implemented, inactive | C3 found no safe native fee path; tier contents and fees are still open. [C4 checks](checks/c4-market-research.md). |
| Track F interior view: F2 upper floors, F3 polish | planned | F1 shop-view drawing is NO-GO and F1b found no safe renderer. F0 stays a default-off experiment. [Track F plan](plans/track-f-interior-view.md). |
| Drinks and bundles | planned | Deferred by the E3 remaining batch and the E5 decisions. |
| Physical fast-forward (6×) | planned | A 6× physical experiment drifted off roads and jammed. Fast-forward runs in the background. |
| Reachability-aware assignment and live venue refresh | planned | Destinations use straight-line weighting and a catalogue frozen at initialization; see [behaviour](behaviour.md#known-limitations). |
| Social and employee identity unification with RLO | planned | Native employees and RLO social profiles stay separate. |

## Superseded

| Item | Replaced by |
| --- | --- |
| E3 per-product implementation PRs | The [E5 dynamic loaded-catalogue plan](plans/e5-dynamic-products.md) (2 Nov). E3 records stay as regression evidence. |
| F5c schema and migration | F5c-lite under the no-migration rule (6 Nov). |
| Development-save migration and switch-over days | Schema bump plus refusal of older development cities (6 Nov). |
| Native rig proof as a merge gate (for example audit F1, F0 final head) | The batched rig run (merge rule v2, 6 Nov). |
| Running the `tools/*-regression` suites before release | Build, unit tests and each slice's own checks (6 Nov). |
| "Later: residents as employees, households" | F5 households and employment, F6 attended wages. |
| The 24 October scope in `implementation-plan.md` | This roadmap and the approved F5, F6 and F7 plans. Archived in [docs/archive](archive/implementation-plan.md). |

## Performance gate

Peter, 30 October 2026. Applies to every step A–E slice re-measure and to the final release; AGENTS.md applies it to every later slice too:

- **Mod-work p95 must stay under 5 ms**, leaving a buffer under the 10 ms frame budget for 100 fps.
- Single mod-work spikes **under 30 ms are acceptable**. Flag every mod-work spike **of 30 ms or more** in the re-measure report.

**Baseline: PASS.** The [30 October review](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/1273abd9a8ec23017deb12cbfe181f9f891b2682/docs/reviews/perf/2026-10-30-performance-review.md) on `codex/pc-perf-review-2026-10-30` records primary dependency-only mod-work p95 **1.259 ms** (highest pooled condition; worst individual window **2.420 ms**) and worst mod-work spike **24.0 ms**. Both pass. If mod p95 climbs, look first at hotspot #1, physical population (**p95 0.64 ms / max 21.7 ms**).

**Whole-frame watch item.** Unexplained whole-frame stalls are tracked separately from the mod-work gate, including the review's four **104–175 ms** evening stalls. Their cause remains unresolved.

**Standing practice.** Being under 5 ms is never a reason to skip obvious improvements. Every slice's implementation and review must look for cheap, low-risk performance wins (redundant per-frame work, caching, avoidable allocations, refresh rates that can safely drop) and take them in that slice when low-risk and measurable. The review's four hotspots—physical population, closure refresh, optional-stop refresh and save serialization—stay parked as high-risk unless a cheap, safe variant turns up, such as lowering closure or optional-stop refresh frequency where correctness allows.

## Wording

Use "implemented" (merged after review) and "verified by rig" (automated rig proof passed) as separate statuses. Status words: planned, implemented, partial, stopped, verified by rig. Plain sentences, one qualification per claim, and a link to the evidence instead of a restated caveat.

## Perf wins log

One line per slice. Before/after numbers and limits are in each file.

- A2 — [perf/a2-schema17-traits.md](perf/a2-schema17-traits.md)
- A3 — [perf/a3-weighted-choice.md](perf/a3-weighted-choice.md)
- A5 — [perf/a5-integrated-acceptance.md](perf/a5-integrated-acceptance.md)
- B0 — [perf/b0-schema18.md](perf/b0-schema18.md)
- B0b — [perf/b0b-held-tuning.md](perf/b0b-held-tuning.md)
- B1 — [perf/b1-memory-loyalty.md](perf/b1-memory-loyalty.md)
- B2 — [perf/b2-regulars-inspector.md](perf/b2-regulars-inspector.md)
- C1 — [perf/c1-campaign-reader.md](perf/c1-campaign-reader.md)
- C2a — [perf/c2a-core-awareness.md](perf/c2a-core-awareness.md)
- C2b — [perf/c2b-campaign-awareness.md](perf/c2b-campaign-awareness.md)
- C4 — [perf/c4-market-research.md](perf/c4-market-research.md)
- D1 — [perf/d1-rival-catalogue.md](perf/d1-rival-catalogue.md)
- D2 — [perf/d2-rival-outflow.md](perf/d2-rival-outflow.md)
- D3a — [perf/d3a-outflow-line.md](perf/d3a-outflow-line.md)
- D4 — [perf/d4-rival-memory.md](perf/d4-rival-memory.md)
- E1 — [perf/e1-rain.md](perf/e1-rain.md)
- E2b — [perf/e2b-street-life.md](perf/e2b-street-life.md)
- E2c — [perf/e2c-interior-guard.md](perf/e2c-interior-guard.md)
- E3 cupcake — [perf/e3-cupcake.md](perf/e3-cupcake.md)
- E3 donut — [perf/e3-donut.md](perf/e3-donut.md)
- E3 pizza — [perf/e3-pizza.md](perf/e3-pizza.md)
- E3 remaining batch — [perf/e3-remaining-batch.md](perf/e3-remaining-batch.md)
- E3 salad — [perf/e3-salad.md](perf/e3-salad.md)
- E4 — [perf/e4-wider-menu.md](perf/e4-wider-menu.md)
- E5a — [perf/e5a-catalogue-eligibility.md](perf/e5a-catalogue-eligibility.md)
- E5b — [perf/e5b-complete-menu.md](perf/e5b-complete-menu.md)
- E5c — [perf/e5c-neutral-metadata.md](perf/e5c-neutral-metadata.md)
- E5d — [perf/e5d-name-identity-save.md](perf/e5d-name-identity-save.md)
- E5e — [perf/e5e-metadata-api.md](perf/e5e-metadata-api.md)
- F0 (Track F) — [perf/f0-interior-view.md](perf/f0-interior-view.md)
- F1 (audit) — [perf/f1-accounting-backlog.md](perf/f1-accounting-backlog.md)
- F1b (Track F) — [perf/f1b-street-proxy.md](perf/f1b-street-proxy.md)
- F3 (audit) — [perf/pc-f3-bounded-bulk-work.md](perf/pc-f3-bounded-bulk-work.md)
- F5a — [perf/f5a.md](perf/f5a.md)
- F5b — [perf/f5b-identity-core.md](perf/f5b-identity-core.md)
- F5f — [perf/f5f.md](perf/f5f.md)
- F6a — [perf/f6a-intervention-harness.md](perf/f6a-intervention-harness.md)
- F6b — [perf/f6b.md](perf/f6b.md)
- F6c — [perf/f6c.md](perf/f6c.md)
- F7a — [perf/f7a.md](perf/f7a.md)
- F7b — [perf/f7b.md](perf/f7b.md)
- F7f — [perf/f7f.md](perf/f7f.md)
- pc-docs-restructure — [perf/pc-docs-restructure.md](perf/pc-docs-restructure.md)
