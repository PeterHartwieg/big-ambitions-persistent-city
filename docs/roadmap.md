# Persistent City roadmap

What is left before the single release, and the standing rules every slice follows. Done work is in [CHECKS.md](../CHECKS.md). Peter's product decisions of 30 October to 2 November (vision, probabilistic choice, traits, memory, marketing, rivals, release order) are in [docs/decisions/2026-10-30-roadmap-decisions.md](decisions/2026-10-30-roadmap-decisions.md); the 6 November delivery rules are in [2026-11-06-delivery-rules.md](decisions/2026-11-06-delivery-rules.md); the 10 November city direction is in [2026-11-10-city-direction.md](decisions/2026-11-10-city-direction.md).

Principles that still apply: bounded state, no extra route searches per decision, saved state replays identically after a reload, native accounting stays authoritative, nothing is written to rivals, every person on the street is a named resident or visitor. There is no interim release.

## Direction

Peter, 10 November 2026, after the [9 November review](reviews/2026-11-09-city-review.md). The full record is [City direction](decisions/2026-11-10-city-direction.md).

- Everyone on the street is a named person with a home and a job to inspect. The population grows by housing renters in homes the player cannot rent. Through-traffic cars are the one anonymous exception.
- Work follows the workplace's opening hours. Evenings and weekends differ. People who share a home sometimes go out together, each paying from their own wallet.
- Residents buy at every kind of player business through one generic sale path, with a fallback for modded shops. Non-food buying comes from recurring needs and from impulse.
- Order: slices in flight finish, then cleanup, saves and spikes, the city, and sales for every business. The hands-on session stays last.

## Remaining

Order of work, top to bottom. Tracks X and P can run side by side. G and R start after P1.

### In flight

| Item | Status | Notes |
| --- | --- | --- |
| F8b to F8g capability report, verified food contracts, hook conflicts | partial | [PR #194](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/194) open. F8h is conditional on rig check 1. [F8 plan](plans/f8-capability-boundaries.md). |
| F9g presentation reads through the view | partial | [PR #190](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/190) open. |
| F9b, F9d, F9f, F9h acceptance | planned | Assembly fingerprints, native-evidence ledger, movement in the integrated city, first integrated record. [F9 plan](plans/f9-city-acceptance.md). |
| E6a Street Food vendor API | planned | Lives in the Street Food repository. Carts and trucks cannot sell until it exists. [E6 plan](plans/e6-carts-food-trucks.md). |

### Track X: a tree agents can read

From [review section 4](reviews/2026-11-09-city-review.md#4-agent-readability). No behaviour change in any row.

| Item | Status | Notes |
| --- | --- | --- |
| X4 entry docs match the code | planned | `docs/behaviour.md`, `docs/glossary.md`, `docs/save-format.md` and `CHECKS.md` describe the 6 November state: research inactive, visitors without bodies, measured body motion as authority, a JSON save wire, and an index that links 56 of 112 checks files. Generate the README and CHECKS status tables from the checks files. |
| X5 history out of the working tree | planned | Move `docs/checks`, `docs/perf`, `docs/reviews`, `docs/archive`, plans of merged slices, `tools/*-regression` and their evidence JSON to a tag. Keep live reference, decisions and open plans. A PR description and one rolling rig checklist replace the per-slice checks and perf files; the delivery rules change with this row. |
| X6 delete dead code | planned | E3 feasibility readers, street proxy, the traffic kernel's physical-control branches and saved field, legacy representation code with no caller. Track F's F0 experiment goes unless a plan still needs it. |
| X7 code map | planned | One `docs/map.md`: tick path, save path, settings, the partial classes and their files, three how-to recipes. |
| X8 names and folders by meaning | planned | Subfolders with matching namespaces. No slice IDs, schema numbers, V2, Legacy or Prototype in identifiers or file names. Comments state the rule. Remove tests that assert on source text first. |
| X9 test kit | planned | One test city builder, one fake planner, one golden hash per fixture, measurements behind an environment flag. |

### Track P: saves and spikes

From [review section 3](reviews/2026-11-09-city-review.md#3-performance). The population cannot grow until P1 and P4 are done.

| Item | Status | Notes |
| --- | --- | --- |
| P1 single-pass save | planned | One writer and one strict reader straight from the snapshot, encoded off the main thread or into a sidecar file. Target: under 40 ms p95 on the main thread at 3,000 persons, in game. The 9 November rig measured 374 to 986 ms. Replaces the compact columnar writer and F7e. |
| P2 remove the known spikes | planned | Optional-stop scene reads on the event path (97 and 129 ms), the first frames after a speed change (110 to 430 ms), the 30-second RLO reflection read and path-cache clear. |
| P3 walkers keep one path per leg | planned | Today each body recalculates a NavMesh path about every half metre. Needed before the walker cap can rise. |
| P4 Mono GC measurement | planned | Rig capability: GC pauses against heap size at 3,000, 10,000 and 20,000 persons. Pool the per-event working copy if the numbers call for it. |
| P5 per-frame scans | planned | Index active optional stops; sync car progress only for visible cars. |

### Track G: the living city

No plan yet. G0 writes it; the technical direction is in the [decision record](decisions/2026-11-10-city-direction.md#technical-direction).

| Item | Status | Notes |
| --- | --- | --- |
| G0 plan | planned | Docs only. Capacity rule for homes without interior area, population size from P1 and P4 evidence, the day-plan model, household rules, through-traffic share. |
| G1 residents in homes the player cannot rent | planned | Every one a named person with a home and a place to inspect. |
| G2 day plans | planned | A seeded plan per person per day replaces the fixed template. Work windows come from the workplace's hours and a shift type. Meals become plan items. |
| G3 evenings, weekends, days off | planned | Nightlife and late shifts, weekends that differ, days off that vary by workplace and person. |
| G4 households | planned | People who share a home go out, eat and shop together. Wallets stay individual. |
| G5 through-traffic cars | planned | Anonymous cars that cross the map and hold road capacity. The inspector says they are passing through. |
| G6 bodies | planned | Fade in on appearing and out at mode changes, appearance beyond the donor seed, more than the casual wardrobe, then a higher walker cap. |
| G7 own cars | planned | The mod's own kinematic cars follow the kernel position and replace the borrowed native traffic cars and their IL hooks. Parked cars become objects. |

### Track R: sales at every business

No plan yet. R0 writes it and proves the path in game before R3 to R5 start.

| Item | Status | Notes |
| --- | --- | --- |
| R0 plan and proof | planned | Confirm from the game assembly that one native customer path serves every customer-facing business type. Prove one non-food sale by a resident in game. Needs the rig's serving fixture. |
| R1 business types and a live catalogue | planned | Every place carries its business type from the loaded catalogue, modded types included. New and closed businesses refresh. Shared with G2. |
| R2 wallet on the person | planned | Budget and wages move out of the food roster. Food becomes one spender among several. |
| R3 needs and impulse | planned | Need categories from the catalogue. Base rate from the game's own customer counts per type, which is also the fallback for types nobody has described. |
| R4 generic sale path | planned | The resident picks the business, the game's customer processing picks what is sold, the resident pays the booked amount. Food businesses the detailed food path cannot support fall back to it. |
| R5 workplaces without customers | planned | Offices, factories and warehouses: residents staff them on the venue's hours. Contracts and production stay native. |
| R6 feedback | planned | Inspector words and the business panel for non-food visits, purchases and refusals. |

### Proof and release

| Item | Status | Notes |
| --- | --- | --- |
| Rig serving fixture | planned | Rig repository. On 9 November the test shop refused service even without the mod, so no sales, receipt or accounting check can run. Blocks R0 and every food check. |
| Batched rig run | planned | Runs every merged slice's in-game checks against the development head. The [9 November intermediate run](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/170) ran 75 of 510. |
| F4f gate run record | planned | After the combined batch. [F4 plan](plans/f4-perf-gate.md). |
| Final performance gate | planned | Re-check the gate below on the final development head. Save latency is measured in its own requested run against P1's target. |
| Hands-on session | planned | Peter's playtest, last, including A5's pointer and input checks 1–3 and check 12's interactive extension (Peter, 10 November). |
| Single release | planned | One Workshop release after all of the above. |

### Done since the 6 November roadmap

Merged, not verified by rig: E5b2 to E5b5 complete menus, E6b to E6f carts and food trucks, C5a to C5f paid research, F2a to F2h movement, F4a to F4e perf gate tooling, F5c-lite to F5i population, F6d to F6k economy, F7c and F7d saves, F8a, F9a, F9c, F9e, X1 to X3. Evidence per slice is in `docs/checks/`.

## Deferred

| Item | Status | Reason |
| --- | --- | --- |
| Track F interior view: F2 upper floors, F3 polish | planned | F1 shop-view drawing is NO-GO and F1b found no safe renderer. F0 stays a default-off experiment. [Track F plan](plans/track-f-interior-view.md). |
| Drinks and bundles | planned | Deferred by the E3 remaining batch and the E5 decisions. |
| Physical fast-forward (6×) | planned | A 6× physical experiment drifted off roads and jammed. Fast-forward runs in the background. |
| Reachability-aware assignment | planned | Destinations use straight-line weighting; see [behaviour](behaviour.md#known-limitations). Live venue refresh moved to R1. |
| Indoor customers and staff shown as the residents they are | planned | A question for the G0 and R0 plans. The interior view experiments found no safe renderer. |
| Shared household budgets, relationships beyond a shared home | planned | Peter, 10 November: wallets stay individual. |
| Social and employee identity unification with RLO | planned | Native employees and RLO social profiles stay separate. |

## Superseded

| Item | Replaced by |
| --- | --- |
| E3 per-product implementation PRs | The [E5 dynamic loaded-catalogue plan](plans/e5-dynamic-products.md) (2 Nov). E3 records stay as regression evidence. |
| F5c schema and migration | F5c-lite under the no-migration rule (6 Nov). |
| Development-save migration and switch-over days | Schema bump plus refusal of older development cities (6 Nov). |
| Native rig proof as a merge gate (for example audit F1, F0 final head) | The batched rig run (merge rule v2, 6 Nov). |
| Running the `tools/*-regression` suites before release | Build, unit tests and each slice's own checks (6 Nov). |
| "Later: residents as employees, households" | F5 households and employment, F6 attended wages. Shared outings are G4. |
| C4 paid research held inactive | C5a to C5f: research ordered and billed through the native agency path. |
| F7e overlap in the native save window | P1. The 9 November rig measured the overlap window at about 3 percent of encode time. |
| Re-scope plan rows for no migration | The slices merged under the no-migration and schema-at-1 rules. |
| "Food-first products" (31 October) | Track R: every player business (Peter, 10 November). |
| The fixed daily template and the shared three-day weekend | G2 and G3. |
| "Unsupported businesses stay native-owned" as the end state | R4's generic sale path, once proven in game. AGENTS.md keeps the rule until then. |
| The 24 October scope in `implementation-plan.md` | This roadmap and the approved F5, F6 and F7 plans. Archived in [docs/archive](archive/implementation-plan.md). |

## Performance gate

Peter, 30 October 2026. Applies to every step A–E slice re-measure and to the final release; AGENTS.md applies it to every later slice too:

- **Mod-work p95 must stay under 5 ms**, leaving a buffer under the 10 ms frame budget for 100 fps.
- Single mod-work spikes **under 30 ms are acceptable**. Flag every mod-work spike **of 30 ms or more** in the re-measure report.

**Baseline: PASS.** The [30 October review](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/1273abd9a8ec23017deb12cbfe181f9f891b2682/docs/reviews/perf/2026-10-30-performance-review.md) on `codex/pc-perf-review-2026-10-30` records primary dependency-only mod-work p95 **1.259 ms** (highest pooled condition; worst individual window **2.420 ms**) and worst mod-work spike **24.0 ms**. Both pass. If mod p95 climbs, look first at hotspot #1, physical population (**p95 0.64 ms / max 21.7 ms**).

**Latest in-game reading: not passing.** The [9 November intermediate rig run](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/170) at 2,871 persons is provisional because the PC was shared. It measured mod-work p95 between 0.9 and 5.1 ms per window, over 5 ms in two of nine windows, and ten intervals of 30 ms or more. Track P owns the causes.

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
- E5b2 — [perf/e5b2.md](perf/e5b2.md)
- E5b4 — [perf/e5b4.md](perf/e5b4.md)
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
