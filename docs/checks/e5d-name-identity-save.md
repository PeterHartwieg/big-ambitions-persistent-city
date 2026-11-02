# E5d: name identity and save contract

## 2 November 2026 — offline implementation

Base `034bb7629391ef0fedb61d7122b0d6ee3c451ff9`, branch
`codex/pc-e5d-name-identity-save`. Schema 25 was the next free allocation.
Writer 25, exact frozen 24 and its valid-predecessor chain, bounded canonical
references, lossless opaque history and held metadata are implemented.
[Exact contract](../population-schema25.md). This slice contains no loaded
catalogue eligibility, complete-menu ownership/checkout integration, metadata
config resolver, rival write or RLO change. No additional product is managed.
Open E5 Q1–Q4 remain open. Nothing here is verified by rig.

Normal original-product contexts capture the existing inputs once; choice,
quote cursors, routes, exact wallets/receipts and B/D memory keep their old
semantics. Cold revision-2 held data has a distinct capability: raw codec
inspection/round trips work, while early authority bootstrap, activation,
simulator startup and store/session publication refuse it. An executable that
cannot run that capability cannot replace its original source with a valid
legacy candidate. Native integration remains E5b's work.

## Offline proof

`Name25Tests` covers the exact predecessor before any migration/defaulting;
foreign/missing/bad/duplicate fields and capabilities; 256/257 identities;
invalid UTF-16/control/delimiter names; ordinal reference rewriting;
independent escaped-character/UTF-8 budgets; 64/65 metadata tuples and the
8 KiB image bound; exact raw-field budgets including escaped keys and pre-value
whitespace; both record paths; opaque names up to 512 characters;
mod removal/return without native lookup or remapping; original saved taste
and fits; interrupted journeys/service/paid/uncertain outcomes; and exact
source dictionary text/reference preservation through codec/store/session.
Existing migration, quote-boundary, receipt, money and economy tests remain.
Fixtures using `foodtrucks:itemname_dumplings` and
`foodtrucks:itemname_choppedcheese` are conditional offline data, not loaded
native products or observed purchases.

The measurement driver compiles the **actual base's Core/Persistence** and the
candidate separately, and also compares every complete base payload/hash and
both counts with the candidate's frozen 24 writer. A migrated candidate
projected through exact 24 must reproduce every predecessor byte before any
maximum-held stress expansion. This checks identities, money, histories,
traits, policy dates, quote progress, journeys and held inputs together.
Only originally valid source payloads are migration successes; table overflow
and incompatible canonical scope names explicitly refuse and preserve.

Completed foreground results are recorded below. [Performance methodology, tables and measured limits](../perf/e5d-name-identity-save.md).

## Completed foreground evidence

The full D3a runner completed **65/65 commands**, including the production build, **2,612/2,612 unit cases** (zero failed/skipped), separate discovery, every standalone regression, Python adapters, demos and both archived-source comparisons. The separately required rival-memory validator also passed **59/59 commands**, including another full unit/discovery/build pass; manifests are in ignored `build/d3a/gates/` and `build/d4-validation/`. No native result is implied.

| Fixture | Save bytes 24 -> 25 | Counts 25, streaming / semantic | Retained MiB 24 -> 25 |
| --- | ---: | ---: | ---: |
| 957x2 | 17,745,536 -> 17,751,674 | 185,067/184,843 | 36.262 -> 37.156 |
| 957x3 | 18,419,556 -> 18,425,694 | 186,981/186,757 | 38.110 -> 39.004 |
| 5000x2 | 30,313,445 -> 30,319,583 | 334,658/334,434 | 61.261 -> 62.155 |
| 5000x3 | 33,834,959 -> 33,841,097 | 344,658/344,434 | 70.836 -> 71.738 |
| 50000x2 | 162,340,698 -> 162,346,836 | 1,999,658/1,999,434 | 325.666 -> 326.560 |
| 50000x3 | 196,752,600 -> 196,758,738 | 2,099,658/2,099,434 | 420.545 -> 421.439 |
| CF32 | 201,827,604 -> 201,833,742 | 2,106,602/2,099,434 | 428.897 -> 429.792 |
| CF64 | 240,227,604 -> 240,233,742 | 3,706,602/3,699,434 | 502.140 -> 503.034 |

All six measured migrations add **6,138 bytes and 280 records**. Every source passes the actual writer-24 contract; its full canonical payload/hash and both counters match the frozen 24 implementation. Every migrated graph projects back byte-identically. Full read/write/activation p50/p95/max, allocations, retained memory, CF bounds and individual spike counts are in the performance evidence. These intentionally saturated histories are not typical save-size predictions.

Both complete passes flagged offline aggregates/bulk work: D3a/D4 awareness whole-index p95/max **31.8366 / 82.6261 ms**, with **1 D3a / 7 D4** samples at least 30 ms; all **21/21** E4 bulk serializations in each four/eight-cell arm, largest **265.8245 ms**; and D4's 128-unit food-candidate advance **33.3999 ms** maximum (**1** spike, p95 **1.8934 ms**). Each default save-runner write/read/activation arm flags all **3/3** samples, largest **663.8664 ms**. The full matrix flags every bulk save/read/activation and cold migration individually. [All distributions and spike counts](../perf/e5d-name-identity-save.md#spike-flags-and-qualification). None is an ordinary-play mod-work or whole-frame result.

CF32/CF64 include the full 1,024 scopes, 20,000 business references, 256 names and 64-tuple cold image. Their **14,000 places**, **7,570,003** history-row characters, **319,713** scope-growth characters and residual name padding share exactly **12,582,912** existing-state characters. Only **5,146,624** actual cell characters spend the E4 cell allowance; its unspent **9,533,440** characters remain reserved. With every outstanding reserve and the entire additional E5d allocation, the totals are **223,716,156 / 262,116,156 bytes and characters**, and **2,119,712 / 3,719,712 records**. CF32 passes 85%; CF64 passes hard caps only. The separate **20,000-place** source passes at **247,807,024 -> 247,813,162 B**, without future-reserve clearance. No codec/population/place limit is reduced.

The raw budgets include escaped field names and pre-value whitespace: exact-limit inputs load, one extra character refuses with original text/reference retained. A separate worst-escaping table measures **394,001 ASCII characters/bytes**, below 512 Ki. Churn retains **28,672 rows / 229,376 cells**, with no 1,024-generations/day validator. An originally valid 24 source at **4,000,000 streaming records** refuses the added 27-record migration without overwriting; independent UTF-8 and character excesses refuse through read/write/activation/store/session. All are offline fixtures.

`python tools/name-identity-regression/verify.py` completed every foreground phase; `build/e5d/final-gate/results.json` records exits/logs. The report also independently completes/checks the archived 20,000-place control, verifies every source hash/count and confirms identical Core/Compatibility/Persistence/Runtime Git trees across the measured heads. The full production build DLL SHA-256 is `73ab188f2cdd6b7b517718d16d82375d044d140889b19159946d4b900478c44d`. The simulation/persistence source stayed fixed while the capacity manifests were tightened. `git diff --check` passes. No game, rig, lease, test profile, Steam write, push or PR publication occurred.

## In-game checks for the next batched rig

All checks belong to the headless agent rig, repository
`PeterHartwieg/big-ambitions-agent-rig`, owned by the Coding Projects bot.
Its source was read only at `73c2ae1611d06cd91ac7dc3029a384f8620024a3`;
no rig/player/profile/lease was accessed. Existing flow primitives are
`call`, `save`/`load`, `state`, `day_summary`, `time/run_until`, `remember`/
`expect`, file evidence and guarded fixture copies. This inventory is not
proof of an E5-specific scenario. Every missing capability below is rig work;
none depends on manual input or an unlocked console. Use only named disposable
FT cities, genuine loaded definitions/stock, and independently captured native
facts. Do not manufacture a product, recipe, cargo or purchase to make a check pass.

1. **Real 24 → 25 migration and unchanged facts — rig-automated (pending rig capability: PC semantic save-projection comparator).** The rig creates/copies a named FT schema-24 checkpoint using the writer-24 baseline, captures its PC payload and independent native stock/orders/finance/authority facts, then loads it with the candidate, performs a coordinated save and reloads 25 twice. It reads the source schema, next-day name boundary, exact table, traits/assignments, wallets/holds, B/D memory, D3a outflow, histories and pending terms. It asserts that projecting 25 through the frozen 24 contract reproduces every original fact, with no new purchase/debit/order; the original source remains untouched before the coordinated save.
2. **Refusal and downgrade preserve the real source — rig-automated (pending rig capability: bounded PC payload fault matrix in copied FT checkpoints).** The rig injects only PC namespace variants in disposable copies: future schema/capability, bad/duplicate/missing table or metadata fields, dangling refs, overbound table/image and a 25 payload presented to writer 24. It reads refusal status, native suppression/authority and original PC text/hash before/after reload and attempted save. It asserts byte-identical source preservation, no replacement identities or wallet/history loss and no transfer to overlapping sales engines. It includes the known cold held-policy header and a valid replacement candidate, proving that store/session caching cannot overwrite an unsupported capability.
3. **Save cost and limits on a real checkpoint — rig-automated (pending rig capability: PC payload counts, byte budgets and read/write/activation timers in flow evidence).** Outside ordinary-play timing windows, the rig records actual 24/25 escaped characters, UTF-8 bytes, both embedded counts, table/tuple counts, read/write/activation latency and retained memory from the same FT checkpoint. It compares these with the offline manifest and records actual native population/factor/shops/history; a small fixture supplies no ceiling proof. The rig uses its automated population/history fixture capability if a larger native composition is needed, then asserts both hard caps without trimming and keeps cold startup/save cost separate from the play gate.
4. **Names with Street Food present/absent/returned — rig-automated (pending rig capability: isolated mod-profile matrix with independent transport and PC identity assertions).** With the city unloaded, the rig prepares and restores a present → absent → returned Street Food sequence, preserving its original files/preferences. It independently dumps loaded canonical definitions and compares exact `foodtrucks:itemname_dumplings` / `foodtrucks:itemname_choppedcheese` references, retained opaque/compact histories, totals, original-three traits, wallets and business-generation memory across save/reload and reordered catalogue/config input. It asserts no remap, substitute, regenerated trait or repeat debit. Histories remain readable when definitions are absent. On the combined E5b head it additionally asserts unavailable service, safe whole-business handoff and fresh full-contract/boundary checks on return; actual stock must exist again, and native mod-removal refunds are attributed separately.
5. **Quote/journey/receipt replay — rig-automated (pending rig capability: deterministic PC continuation checkpoints and independent receipt/order delta assertions).** The rig saves/reloads at evaluating quote, outward journey, selected-before-service, paid and uncertain receipt checkpoints and repeats continuation/outcome requests. It compares held class/taste/fits/tuning, quote cursor and routes, product/business names, exact price/holds/wallets/history and return ownership with an uninterrupted control. It asserts one confirmed payment/order at most, no retry of uncertainty and no reroll after configuration/catalogue reordering. Revision-2 metadata execution requires the combined E5c/E5b implementation; this slice's cold header must refuse before authority/activation while preserving all facts.
6. **Batched ordinary-play performance and noninterference — rig-automated (pending rig capability: unified PC mod-work/whole-frame evidence assertions).** In the next existing batch, the rig interleaves matched idle and breakfast/lunch/evening rush windows, with closed/open diagnostics, warm state and a reload; cold migration is timed separately. It asserts unchanged original-product orders, native setup/route counts, no new clean-frame scans/reflection/polling and bounded pending work. It reports mod-work p50/p95/max, requires p95 <5 ms, and flags every spike ≥30 ms with count/condition/component. Whole-frame cost/stalls are separate. The rig records original autosave, disables and verifies it before every window and after reload, excludes saves from windows, discards contaminated samples, and restores settings/files during automated cleanup. No separate performance-only rig session is proposed.

The same numbered list is supplied in the prepared PR description. Loaded
eligibility, missing-mod handoff and native replay proof remain pending the
appropriate combined implementation; offline success never certifies a product
in game.
