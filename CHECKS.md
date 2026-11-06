# Validation status

Each slice adds its own `docs/checks/<slice>.md` and exactly one index line here; never append slice sections to this file. Sync/review-fix notes go into the slice's own file.

- City-audit F6c — 4 November 2026, offline: read-only feedback v1 (week-over-week purchases/sales and evaluated-offer causes per shop from the existing 14-day rows, days without a record labeled, neutral rival label constant, current budget-state counts, inspector budget words) with incremental counters replacing the panel's consumer scans; writer 25, no behaviour/economy/save change; 2,899 tests (13 new) and the required commands passed. Panel components: scans p95 1.1088/3.4898/32.1975 ms -> counters 0.0026/0.0050/0.0030 ms at 1,914/10,000/100,000 consumers; per-shop lookups p95 116.1171 -> 14.5748 ms at 1,024 shops; hidden panel 0 B, 0 requests; >=30 ms samples flagged: removed code 45/200 (100,000 consumers) and 200/200 (1,024 shops), new path 1/200 at 1,024 shops (34.2305 ms, open-panel build); posting +0.0006–0.0163 ms p95. [Checks and eight In-game checks for the next batched rig](docs/checks/f6c.md#in-game-checks-for-the-next-batched-rig), [measurements](docs/perf/f6c.md).

- City-audit F5a — 4 November 2026, offline: read-only world census and `pc population census` console diagnostic; writer 25, no behaviour/save/`BusinessAllocation` change; 2,886 tests (30 new) and the required commands passed. On request only: main-thread capture p95 0.023 ms (957 residents) / 0.990–1.382 ms (50,000), detached reduction p95 0.268–0.278 / 7.173–7.644 ms; zero ordinary-frame work, no sample >=30 ms; rival `aiEmployees` verified, workstation count absent. [Checks, ILSpy record and In-game checks for the next batched rig](docs/checks/f5a.md#in-game-checks-for-the-next-batched-rig).

- City-audit F1 — R3 offline: accounting-specific save-change recovery; marked saves remain refused; writer 25; 2,832 tests and all required commands passed. Final detached 30/60/120 p95 0.0370/0.2919/0.3453 ms, max 4.8222/4.6583/3.8542 ms; lag <1 min, supported/stress recovery 2/10 s. R3 repeats 0 samples >=30 ms, max 19.3869 ms; R2 66.3905 ms remains flagged. Native proof blocks merge. [Dated checks and six In-game checks for the next batched rig](docs/checks/f1-accounting-backlog.md), [measurements](docs/perf/f1-accounting-backlog.md).

- City-audit F3 — 3 November 2026, offline: R1 2,821 tests; 32-posting units, finite route retries/query admission, coherent 1,024-candidate capture and resumable publication (native-double capture p95/max 6.1153/19.7485 ms, publication 0.0250/0.1615 ms); writer 25 unchanged. Posting-call p95 0.0208/0.0229/0.0111 ms at 1,914/10,000/100,000 consumers, median 0 B; matched Core replay save hashes equal, backlog drains to zero; 49 stress advancement intervals >=30 ms (max 54.2938 ms) flagged. [Checks, qualifications and five In-game checks for the next batched rig](docs/checks/pc-f3-bounded-bulk-work.md); [full timings/work/allocations](docs/perf/pc-f3-bounded-bulk-work.md).

- City-audit F7a — 4–5 November 2026, offline: 2,862/2,862 unit tests and all 63 required commands exit 0 at code `5d0bb2a` (D3a 71/71, rival-memory matrix 65/65); save-cost recorder (observe/synchronize/validate/encode/publish, sizes, records, activation) on request, null and byte-identical otherwise; `tools/save-cost` representative F3-replay fixtures (hashes = F3) and E5d stress fixtures (hashes = E5d), deterministic; writer 25 unchanged. Representative 957x2 2,333,388 chars, save p50/p95 46.10/87.19 ms; stress 50000x2 2,036.83/2,293.39 ms; all 84 save samples >=30 ms flagged (save latency, not mod-work); main-thread construction max 32.221 ms at representative 50,000x2 flagged. encoding-max (E5d non-ASCII) 149,224 chars / 411,368 UTF-8 bytes, save p95 6.29 ms; escaped read 1/7 >=30 ms flagged. [Checks and seven In-game checks for the next batched rig](docs/checks/f7a.md), [baseline](docs/perf/f7a.md).

- City-audit F5b — 4 November 2026, offline, inactive: pure Core person kind, visitor stay, employment, derived household ID, IdentityPolicy v1 and the ownership validator (rules 1-5, duplication guards, player-owned workplaces refused); not wired; writer 25 unchanged. 78 new unit cases; validator 0 B/call warm, p95 0.7140/5.1875/56.3153 ms at 1,914/10,000/100,000 persons (100,000 whole-graph pass >=30 ms flagged, no frame work); derived identity draw 53-64 ns / 0 B. [Checks and four In-game checks for the next batched rig](docs/checks/f5b-identity-core.md), [measurements](docs/perf/f5b-identity-core.md).

- City-audit F5f — 5 November 2026, offline: active-journey candidate index; physical selection and queue planning iterate travelling/queueing/outdoor persons instead of every citizen, visitors indexed but withheld until F5i, caps/leases/`ByPriority` order unchanged, 30 s shadow check with full-scan fallback; writer 25, no identity/economy change. 2,973 tests (9 new) and the required commands passed; index = full scan after every event/slice/skip/reload/rebuild/lease release. Selection p95 against the unchanged pre-F5f loop at 50,000 persons / 100 active 1.0858 -> 0.0123 ms (1,000 active 1.9762 -> 0.1927 ms), no sample >=30 ms. [Checks and four In-game checks for the next batched rig](docs/checks/f5f.md#in-game-checks-for-the-next-batched-rig), [measurements](docs/perf/f5f.md).

- E5b — 2 November 2026, offline **partial / native integration incomplete**: complete-menu price refusal, cached authored bag policy; focused 1,071 menu / 10,596 checkout assertions (r1); discovery 280 -> 0 B/call; cache 10,100/0 hits/misses, 10,100 marks -> one pending shop, setups 33 vs 990; 1,024-shop detached backlog p95/max 0.0140/0.0497 ms, zero new >=30 ms samples; inherited 62.8223 ms first-use interval flagged; [dated evidence, remaining contracts and six In-game checks for the next batched rig](docs/checks/e5b-complete-menu.md), [performance](docs/perf/e5b-complete-menu.md).

- E5e - 2 November 2026, offline: versioned metadata only, 2,782 unit cases / 39 new E5e cases, full required matrix green; empty/256/budget resolve p95 0.000518/0.000047/0.000726 ms, 0 B; register/unregister/conflict p95 0.1084/0.0414/0.1539 ms; budget publish 0.0117 ms / retained 422,128 B; zero >=30 ms E5e windows. [Full measurements, checks and In-game checks for the next batched rig](docs/checks/e5e-metadata-api.md).

- E5a — 2 November 2026, offline: detached loaded-catalogue/stock-contract reader, writer 24; 2,556 unit tests / 2,744 E5a assertions; 8-offer/2,000-holder rebuild p95 0.4434 ms, backlog unit p95 0.1777 ms; rebuild 300,280 -> 129,144 B, clean reads 0 B/scans; initial backlog spike 32.5793 ms flagged. [Checks, full measurements and In-game checks for the next batched rig](docs/checks/e5a-catalogue-eligibility.md).

- E5c - 2 November 2026, offline: Food default/seeded resident tastes, 2,687 tests; 4/8 weights p95 0.003344/0.004805 ms, 0 B; max-name fresh config p95 1.5955 ms; first-use 59.0058 ms (earlier 57.6619 ms) startup intervals flagged. [Full resolver/config numbers, validation and next batched rig](docs/checks/e5c-neutral-metadata.md).

- E5d - 2 November 2026, offline: writer 25, exact 24 migration, 2,612 unit cases; stress save +6,138 B / 280 records; CF32/64 budgets 223,716,156 / 262,116,156 B; [full bytes/counts/latency/memory](docs/perf/e5d-name-identity-save.md) and [In-game checks for the next batched rig](docs/checks/e5d-name-identity-save.md#in-game-checks-for-the-next-batched-rig).

- E3 remaining batch - 2 November 2026, offline: all six named candidates STOP; twelve-reader worst warm 2,000-entry p95 0.1441 ms, 200 / 49,104 B/read; existing cold 1,112 -> 1,256 B, zero reader spikes >=30 ms; [full timings, proof and 6 Nov rig/PR handoff](docs/checks/e3-remaining-batch.md).

- D4 - 1 November 2026, offline sync: writer 24; 2,556 unit cases; mixed lookup/update p95 0.0003/0.0002 ms, 0 B; matched controls 2,446 B and 8/8 records; [Dated checks and In-game checks for 6 Nov rig](docs/checks/d4-rival-memory.md#in-game-checks-for-6-nov-rig).

- D3a — 1 November 2026 (offline): free fourteen-day totals-only outflow, schema 24; 2,528 tests; warm line 0 B/request, 128-settlement/open p95 0.0687 ms, no component spike ≥30 ms. [Dated evidence and In-game checks for 6 Nov rig](docs/checks/d3a-outflow-line.md).
- D2 - Rival choice/outflow, schema 21 below C2b/E4; offline sync evidence and numbered native checklist: [checks](docs/checks/d2-rival-outflow.md).

- E3 pizza — 1 November 2026 (offline only), holder STOP; 2,000-entry warm p95 donut/cupcake/salad/pizza **0.1589/0.1338/0.1476/0.1122 ms**, **49,104 B/read** (2 entries **200 B**); cold **1,064 B/read**. [Full cold/warm timings, proof and numbered 6 Nov handoff](docs/checks/e3-pizza.md).

- F1b — 1 November 2026, offline: renderer NO-GO; one-shot doors/inventory p95 0.0043/0.2010 ms (native doubles); disabled 0 B, no frame calls. [Dated checks and In-game checks for 6 Nov rig](docs/checks/f1b-street-proxy.md).
- F1 — Shop-view drawing NO-GO, offline validation and numbered 6 Nov handoff: [checks](docs/checks/f1-shop-view.md).
- F0 — Interior view experiment, reviews and native checklist: [checks](docs/checks/f0-interior-view.md).
- D1 — Read-only rival food catalogue and native checklist: [checks](docs/checks/d1-rival-catalogue.md).
- C1 — Native campaign reader and native checklist: [checks](docs/checks/c1-campaign-reader.md).
- E2a — Street-life proof, coverage and native checklist: [checks](docs/checks/e2a-street-life.md).
- A1 — Resident inspector and base sync evidence: [checks](docs/checks/a1-resident-inspector.md).
- A2 — Schema 17, lifestyle traits and base sync evidence: [checks](docs/checks/a2-schema17-traits.md).
- A3 — Weighted food choices, review fixes and native checklist: [checks](docs/checks/a3-weighted-choice.md).
- B0 — Schema 18, capacity, review fixes and native checklist: [checks](docs/checks/b0-schema18.md).
- B0b — Held tuning, base syncs and native checklist: [checks](docs/checks/b0b-held-tuning.md).
- B1 — Shop memory, loyalty v2, review fixes and native checklist: [checks](docs/checks/b1-memory-loyalty.md).
- B2 — Observed own-shop regulars, resident memory words and native checklist: [checks](docs/checks/b2-regulars-inspector.md).
- E3 — Donut feasibility, base syncs and native checklist: [checks](docs/checks/e3-donut.md).
- C3 — Agency/payment discovery, base syncs and native checklist: [checks](docs/checks/c3-agency-payment.md).
- PR #1–#3 history — October accounting, optional stops, reviews and rig evidence: [checks](docs/checks/history-2026-10-pr1-pr3.md).
- Earlier development history — October foundation, food, traffic and performance evidence: [checks](docs/checks/history-2026-10-development.md).
- pc-docs-checks-split — Documentation structure and preservation audit: [checks](docs/checks/pc-docs-checks-split.md).
- E2c — Third-party interior guard, base syncs and native checklist: [checks](docs/checks/e2c-interior-guard.md).
- C2a — Core bounded awareness, reviews, schema 19 and native checklist: [checks](docs/checks/c2a-core-awareness.md).

- E2b — [Bounded street-life, reviews, base syncs and native checklist](docs/checks/e2b-street-life.md).

- E3 cupcake — Shared direct-stock reader, feasibility and native checklist: [checks](docs/checks/e3-cupcake.md).

- E3 salad — Conditional direct-stock probe, feasibility stop, base sync and native checklist: [checks](docs/checks/e3-salad.md).

- C4 — Bounded inactive market research, access contract, base sync and native checklist: [checks](docs/checks/c4-market-research.md).

- C2b - 1 November 2026 (offline): schema 22; hourly/query p95 at 1,024 images 2.6336/0.0065 ms, query/absent/disabled 0 B; owner decision 1 active for all residents; pre-E1 measurements; in-game checks batched under merge rule v2, no rig proof claimed. [Checks and rig list](docs/checks/c2b-campaign-awareness.md).

- E4 — Schema 23 envelope (1 Nov, offline; original pre-E1 measurements): 4/8 choice p95 0.0134/0.0257 ms; supply 1,960 -> 1,552 B; eight-product save 10,135,833 B; 6 Nov checklist: [checks](docs/checks/e4-wider-menu.md).
- A5 — Integrated acceptance, pre-E1 measurements, schema-20 base sync and 12 numbered rig checks: [checks](docs/checks/a5-integrated-acceptance.md).

- E1 — New-outing and food weather, schema 20, base sync and native checklist: [checks](docs/checks/e1-rain.md).

- E3 ice cream - 1 November 2026, offline STOP; D/C/S/I warm 2,000-entry p95 0.1610 / 0.1474 / 0.1605 / 0.1779 ms, 49,104 B/call; cold/small timings and 6 Nov handoff: [checks](docs/checks/e3-icecream.md).

- E3 frenchfries — 1 November 2026, offline feasibility STOP and numbered 6 Nov handoff: [checks](docs/checks/e3-frenchfries.md).
