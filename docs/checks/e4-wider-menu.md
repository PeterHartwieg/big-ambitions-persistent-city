# E4 wider menu checks

## 1 November 2026 — offline implementation

Code commits `60b0715` and `6d3068a`; base `51ce709`, branch `codex/pc-e4-wider-menu`. Implements only the [schema 23 envelope](../population-schema23.md); three native products and effective live maximum four remain. Donut/cupcake/salad are reserved identifiers with pending E3 natural checkout/settlement proof. No game, rig, lease, ModsLocal, bridge profile, WSL or Steam writes in this run. Steam assemblies are read-only build inputs.

## Review round 1 - 1 November 2026 (offline only)

**Opus-r1-1:** preserve the catalogue's existing priced-subset behavior. The actual `FoodBusinessCatalogue` can omit a scoped product when its `retailPrices` row is absent while `NativeFoodMarket.CanServe` remains true. E4's count-equality filter then incorrectly removed the entire owned shop from captured demand. The new native-double regression reproduces that exact chain with available stock/service and a full two-product saved scope, failing before the fix at `missing retail price must not remove an owned shop from represented demand`.

Capture now retains every observed known scoped offer when the catalogue is a subset. The **full scope size**, rather than just the priced observation size, must fit the captured configured limit; this cannot hide an oversized menu. Unsupported/out-of-scope offers still reject the whole shop. No missing price is invented, no native eligibility/accounting boundary is changed, and no product is enabled. The regression independently omits the burger and croissant prices, covering an unpriced primary scoped product; each priced offer survives held-capture save/reload and completes exactly one native-double order/bag debit and one product history cell, leaving unpriced stock untouched. Native reachability of missing-price data remains unproven; these are conditional field doubles, not game evidence.

The targeted checkout regression is red before the fix and green after (**376 assertions**); original cache workload remains **957 hits / 27 misses / 33 setups versus 990 uncached**. **33/33 foreground gates pass**, covering all 32 requested commands plus the E4 envelope: production build, **2,348/2,348 tests / 2,348 discovered** (zero failed/skipped), every requested regression/Python harness and all three demos. The E4 envelope retains **25,165 assertions**. Code commit: `977f95b`. Save-contract schema 23 remains **119,156 characters**, digest `6E3492C8956D65B2E110449B6DFA223D25A91222ED87B9FB64727BDF89A9812B`, **849 orders / 797 purchases / two unconfirmed**, with all twelve F0 transitions passing. Production DLL SHA-256: `8afc4944afa1c64682253097efb05150a78eac524a29dc7b64ce596ff1536160`. Cleanup finds **zero task-owned dotnet/testhost processes**. The prescribed other-dotnet guard ran before every test/run; unrelated worktrees were left intact and no execution deadlines were used. [Round-one commands, red/green evidence, measurements and log hashes](e4-wider-menu-review-r1-results.json).

Round-one timing flags: **42/42 bulk serialization samples >=30 ms**, four/eight-product maxima **204.6867 / 246.4089 ms**; these are offline full-save batches, not ordinary-play mod-work. Full-history update maxima **0.5983 / 1.6284 ms**, zero update samples >=30 ms. Native combined mod-work p95 <5 ms, every mod-work spike >=30 ms flagged, and separate whole-frame timings remain pending under the unchanged 6 Nov checklist. No special performance win is claimed for replacing one scalar count check; the prior measured optimization and all qualifications below remain dated evidence. No game, rig, bridge/profile, ModsLocal, lease or WSL work in this fix.

### Bounds audit

| Linked seam | Schema 23/framework | Frozen old/live boundary |
| --- | --- | --- |
| `FoodScope.AdditionalProductIds` | Seven additional IDs, unique with primary | Old 17/18/19 reject fourth additional (fifth offer); active Runtime requires all products supported |
| `NativeFoodMarket.Supported`, identity | Same three supported IDs, hard live maximum four; configured lower bound applies to whole shop | No product registration/checkout expansion; existing ownership observer drains/transfers at boundary |
| Supply cache | Full ordered menu retained in key; minute/authority/registration/identity/flags unchanged | Captures, failure evidence and checkout read live; sale clears cache; no subset cache |
| `FoodSupplyReader.ReadMenu` | Eight, duplicate/empty/oversized refusal; bounded nonarray enumeration | One setup, one counter/bag pool; stock/refill/converted-resource refusals retained |
| A3 snapshots/selection | Eight shops x eight offers = 64; full saved scope checked against the configured limit; all observed known scoped offers retained | Missing retail-price rows preserve the pre-E4 priced subset (Opus-r1-1); no invented price or truncation to fit a limit; unsupported/out-of-scope offers still reject the shop; sixteen route slots |
| Reservations | Existing dynamic place/product keys; no four-cell storage bound | One product per commitment, one scope service clock; shared bags revalidated at checkout, never sum per-offer bag caps |
| `FoodProductSales` | Eight compact reviewed cells; opaque legacy IDs retained | Exact product/count/revenue/meal/hour/district sums; old cell arrays still capped at four |
| `MealRhythm.ProductPenalty` | Reviewed existing fits; unknown/new fits neutral | No recipe/nutritional inference or invented E5 vector |
| `SaveCodec` | Exact 19→23 step, schema-specific shapes, compact precheck, row/cell charging | Old source bounds run before migration; unknown/newer/corrupt/foreign source preserved |
| Product explanations | New derived taste can report Taste; unknown neutral; health/quality priors unknown | A2 bands/order and existing factors unchanged; no per-consumer dictionary |
| Configuration/reload | Flat 4 / 1.0 / 0 defaults; 1–8 / 0–2 / 0–2; held revision 2 only when needed | Effective native cap ≤4; held revision 1/default draw inputs unchanged |

### Validation and measurements

Final foreground command results and measurements are recorded below. Reproduction: `python tools/e4-menu-regression/prepare.py`, prescribed other-dotnet guard, then `dotnet run --project tools/e4-menu-regression/E4MenuRegression.csproj --configuration Release`. `validate.py` runs the entire required foreground matrix with a guard before each test/run. Raw logs remain in ignored `build/e4-menu/validation`; no execution deadline or background test worker is used.

Offline measurements are not combined native mod-work or whole-frame timings. The combined gate remains mod-work p95 <5 ms; report p50/p95/max and every spike ≥30 ms. Whole-frame timing/stalls remain separate. The four parked hotspots are unchanged; narrow E4 cell encoding is a save contract, not a serializer rewrite. [Supply-refresh allocation win](../perf/e4-wider-menu.md).

## Final measured results - 1 November 2026 (offline only)

Production build passes; **2,348/2,348 tests pass and 2,348 cases are discovered**, zero failed/skipped. All **32/32 requested commands** pass, including every listed regression/Python harness and all three demos. The six final focused gates (build, full tests, listing, save contract, E4 envelope and baseline economy comparison) also pass. The additional envelope harness passes **25,165 assertions**. All workloads ran sequentially in the foreground with the prescribed other-dotnet guard, no subprocess deadlines, and build/compiler-server reuse disabled. Other worktrees repeatedly occupied the guard; their processes were left intact.

In the original pre-E1 E4 run, the exact schema-19 source reader ran before 19 -> 23 migration; the integrated chain below now runs through schema 20. Old 17/18/19 fifth-offer refusals, future/reserved/foreign headers, missing/unknown policy, noncanonical cells and tuning, broken sums, oversize menus/history and failed store/session replacement preserve the original payload. Opaque old history IDs survive. Synthetic eight-offer capture round-trips at **17 interrupted boundaries / 16 route calls** in both width arms; lowered live settings do not recapture a held image. Shared queues consume **4/8 distinct product units and 4/8 shared bags**, with **4/8 distinct service times**, identical across reload. Churn beyond 1,024 business-generations/day is retained; unsupported serialization refuses atomically.

**No unproven product enabled:** same three production IDs; effective native cap four even when configured to eight. The production DLL contains no synthetic E4 product ID. A2 traits, the route batch, E3 readers, physical population, food-hours refresh and optional-visit code match the base. Production DLL SHA-256: `6ae4c556688e84afe2bf279399d50f788e50a5a39abfc31e71be21d4dc89b96d`.

Pure policy: 1,000 seeds x 24 existing-product offers have bit-identical weights and byte-identical choices against `51ce709`. Both native-free three-day default economy runs also match the base exactly: **legacy 849 orders / 797 purchases / two unconfirmed; weighted 531 / 499 / one**. Money, order/economy digests and interrupted-reload digests match. Default tuning remains the identical 193-character revision-1 wire. Save-contract population output is schema 23, **119,156 characters**, SHA-256 `6E3492C8956D65B2E110449B6DFA223D25A91222ED87B9FB64727BDF89A9812B`; its 73-character menu header is the only population-only wire addition. All twelve F0 off/on/save/reload transitions pass. The separately executed frozen-base save-contract harness passes: population **119,083 -> 119,156** characters (+73), base digest `D7FF8294F56C3B3D5AF736540274E68E9848319B34658859FC15E0D4CDB1F3A2`; refusal messages and legacy economy JSON are identical. Across twenty calls, write allocation is **14,455,064 -> 14,456,072 B**, read allocation **38,773,560 -> 38,823,000 B**; these are fixed schema/header/codec overheads, separate from the zero-allocation live selection path. Final production rebuild retains the same DLL hash. Cleanup inventory finds **zero task-owned dotnet/testhost processes**, and all foreground runners have completed.

Final .NET 10 Release measurements below use native field doubles, seed 957, 20 warmups; 1,001 samples for selection/reader, 101 full-history updates and 21 bulk serializations. Fixture creation, JIT/cold startup and explicit validation assertions are outside measured windows. B/call counts current-thread allocation, excluding the timing sample array. Array-menu inputs are the actual native caller shape. Samples are sequential, not an interleaved native performance experiment; CPU scheduling/JIT/runtime differences limit causal timing claims. Allocation and byte comparisons are deterministic in these fixtures.

| Workload | p50 / p95 / max ms | B/call | Samples >=30 ms |
| --- | ---: | ---: | ---: |
| Default three-product selection before | 0.0102 / 0.0124 / 0.2240 | 0 | 0 |
| Default three-product selection after | 0.0072 / 0.0081 / 0.0379 | 0 | 0 |
| 4 offers x 8 shops (32 evaluations) | 0.0077 / 0.0134 / 0.1953 | 0 | 0 |
| 8 offers x 8 shops (64 evaluations) | 0.0150 / 0.0257 / 0.2378 | 0 | 0 |
| Supply refresh 4 before | 0.0010 / 0.0018 / 0.1375 | 1,960 | 0 |
| Supply refresh 4 after | 0.0009 / 0.0015 / 0.0101 | 1,552 | 0 |
| Supply refresh 8 after | 0.0018 / 0.0024 / 0.0099 | 2,368 | 0 |

No added selection allocation or native/route work at the live cap: **0 B before/after**, unchanged maximum sixteen quote slots, unchanged valid four-offer supply bytes. Default CPU timings above do not establish literally zero additional instructions. The original cache regression retains **957 hits / 27 misses**, **33 setups versus 990 uncached** for 123 reads/eight shops/three checkouts. Persisted captures, failure observations and checkout still bypass stale cached supply. The supply-refresh win is **408 B/call (20.8%)**; no native frame-time gain is claimed.

| History allocation | Rows / cells | Initial ASCII chars = UTF-8 B | Timed save chars/B after updates | Streaming / semantic records |
| --- | ---: | ---: | ---: | ---: |
| 4 products | 14,336 / 57,344 | 8,648,985 | 8,864,025 | 74,755 / 71,683 |
| 8 products | 14,336 / 114,688 | 10,135,833 | 10,350,873 | 136,195 / 129,027 |

The eight-cell history itself charges **129,024** records; the measured semantic total adds three existing header/consumer records, and streaming adds 7,168 scope menu strings. No global cap changed. The codec limits each reviewed compact product cell to 128 characters; the full eight-cell stress save stays within both the 14,680,064-cell-character allowance and the whole-payload caps. This is the E4 history composition with one consumer and 1,024 scopes, not the final integrated CF32/CF64 city headroom proof. Timed saves follow 20 warmup + 101 measured updates, each incrementing one product and its business/hour/meal/district totals per row; the initial and timed sizes are deliberately distinguished.

| Bulk history work | p50 / p95 / max ms | B/call | Samples >=30 ms |
| --- | ---: | ---: | ---: |
| 4 products: update all 14,336 rows | 0.3746 / 0.7869 / 1.4671 | 0 | 0 |
| 4 products: serialize all 14,336 rows | 207.6773 / 229.4066 / 257.2624 | 80,172,755 | 21 |
| 8 products: update all 14,336 rows | 0.7406 / 2.5212 / 6.6181 | 0 | 0 |
| 8 products: serialize all 14,336 rows | 228.7722 / 245.1546 / 248.9193 | 92,156,970 | 21 |

**Timing flags:** all **42/42 measured bulk serializations >=30 ms**, maxima **257.2624 ms (four) / 248.9193 ms (eight)**. These bulk offline save operations are not clean ordinary-play frame samples or a passing mod-work gate. Eight-product full-history update max **6.6181 ms**, p95 **2.5212 ms**; no update/selection/supply sample reaches 30 ms. The unchanged C1 1,024-business synchronous cold reader separately reports p95 **6.026 ms**, max **9.6265 ms**, zero >=30 ms; bounded continuation p95 **0.0016 ms**, max **1.4906 ms**. Whole-frame timing is unavailable offline. Combined native mod-work p50/p95/max, p95 <5 ms and every spike >=30 ms flagged remain pending for 6 Nov; whole-frame timings/stalls remain separate.

Development-only failures were resolved before the final green run: six old current-format fixture assumptions were updated for schema 23 while retaining separate frozen-source refusals; the baseline reader copy needed a distinct snapshot type name; the benchmark update initially omitted its district counter and was correctly refused by sum validation. No production invariant was relaxed to make those fixtures pass. General serialization and the other three parked hotspots were not rewritten.

[Machine-readable results and log hashes](e4-wider-menu-results.json). [Prepared PR description, including the identical numbered rig list](../plans/e4-wider-menu-pr-description.md).

### In-game checks for 6 Nov rig

Pending in the normal combined session, named disposable FT fixture only. LOCKED-OK follows the [locked-session feasibility report](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/84be71a67aca97aefc0dcce7055caaf3b0257af8/docs/reviews/rig/2026-10-31-locked-session-feasibility/README.md) (`84be71a`); no rig work occurs here. Missing fixtures keep dependent checks pending until check 2. Use only currently supported real products; no dummy products or E3 candidate activation.

1. **LOCKED-OK — largest supported real menu and bridge dump.** On an already prepared disposable owned fixture, dump complete offered IDs/prices, scope/native authority identity and interval, configured/effective cap (≤4), supply-cache counters, direct product stock, single shared bag/counter/service pool, held shortlist/route slots and all product/history sums. Compare against independent bridge fields; current maximum is the verified croissant/burger/hot-dog mix, not eight native offers.
2. **NEEDS-INPUT — fixture/menu UI edits and visual confirmation.** Prepare the largest currently supported real fixture if absent. Remove a product and restore/change the menu through the normal shop UI near an accounting boundary; capture before/after visuals and timestamps. Induce reachable product, bag and staffing shortages. UI edits and visual confirmation wait for the unlocked 6 Nov session; do not manufacture cargo, products, customers or orders.
3. **LOCKED-OK — boundary, shared shortages and counters.** With check 2's prepared checkpoints, bridge-dump before/after native hourly boundaries and verify whole-business authority intervals, pending terms, one shared service queue and bag pool. Product shortage affects only that product; last-bag/counter shortage cannot multiply capacity across offers. Removed products cannot create new commitments; retained history/receipts remain intact. Missing input-dependent checkpoint stays pending.
4. **LOCKED-OK — exact checkout and save/reload.** Observe ordinary represented-consumer purchases on the prepared fixture. Independently reconcile one product/one bag/completed-order delta, exact binary32 price debit, receipt and native daily settlement; no manual native write or repeated settlement. Save/reload outside timing windows at held quote/commitment and paid/unconfirmed checkpoints; verify original terms, ≤16 quote slots, exactly-once accounting, product/count/revenue/meal/history sums and unchanged saved tastes.
5. **LOCKED-OK — unsupported-edit handoff.** After an unsupported UI edit prepared in check 2 (the edit itself is NEEDS-INPUT), observe refusal of the entire menu and native accounting-boundary transfer through bridge authority/state dumps. No supported-subset suppression/sales, overlap, new product checkout or loss of historical totals. After the supported-menu restoration prepared in check 2 (NEEDS-INPUT), inspect reacquisition at a later boundary; do not enable reserved E3 candidates.
6. **LOCKED-OK — matched candidate/base performance windows and cleanup.** Interleave frozen base/candidate ordinary-play windows at idle and breakfast/lunch/evening rush, closed/open relevant UI, cold/warm supply and a soak, using matched seed/checkpoint/clock/body/graphics/population/shop settings. Record original autosave, disable and verify off before every window and again after reload; saves outside windows, any save invalidates a clean sample. Report combined mod-work p50/p95/max, require p95 <5 ms, flag every spike ≥30 ms with count/condition/component, supply reads/hits/misses, route queries, allocation/GC, retained memory, lag/throughput and pending work. Whole-frame timing/stalls are separate. Restore autosave, preferences and fixture changes during cleanup. No special performance-only run.

## Base sync with E1 and F1

Task `pc-e4-sync-base`, 1 November 2026: real merge of integration head `5877453` (E1) and its ancestor `2f12ae9` (F1) into E4. No game or rig access, deployment, push or PR creation. Both branch diffs against merge base `51ce709` were inspected for every conflicted file. E1/F1 checks, notices, tests and roadmap/perf entries remain; E4 keeps one checks index line and this owning evidence file.

Schema 23 now builds on exact schema 20. Frozen imports and 18->19 pass through isolated 19->20 weather activation, then isolated 20->23 menu policy migration. Schema-20 weather fields are neither reset nor recaptured. Reserved 21/22 and future schemas refuse unchanged. Core/runtime automatic merges compose E1 rain inputs with E4 menu limits/tastes and keep the approved missing-price behavior; no additional Core/runtime edits were needed for integration. Persistence gets exact schema-20 contracts and the two isolated steps. Migration fixtures and save-contract byte accounting explicitly project out both weather and menu additions when comparing to schema 19.

**Historical measurements:** all earlier E4 timing/allocation/save-byte tables, JSON evidence and frozen-base comparisons in this file and the original E4 PR description were measured on the `51ce709`-based E4 stack before E1. They are preserved, not silently relabelled as merged-tree results. The perf file repeats this qualification. The current sync results below are separate offline measurements and do not certify native mod-work or whole-frame performance. Original numbered 6 Nov checks and their pending/input qualifications remain in force; no per-PR rig was run under merge rule v2.

Initial E1/F1 validation, before A5 landed: [49-command results, log hashes and preserved initial fixture failure](e4-sync-base-initial-results.json). Merge commit `f50d446` has parents `506e434` and `5877453`. The production build passes. All **2,390 unit tests pass**, with **2,390 listed**, zero failures/skips. All **32 requested commands**, plus the E4 menu regression, pass sequentially in the foreground with the prescribed other-dotnet guard. E4 reports **25,165 assertions**, both 4/8-offer captures replay at **17 interrupted boundaries / 16 route calls**, and shared queues retain 4/8 purchases and distinct service times across reload. Save-contract checks independently reconcile weather and menu bytes and preserve exact schema-20 terms. Its current population fixture is **120,257 characters**; projection back to the schema-19 population shape is **119,083**. The difference is explicitly accounted for by the E1 weather fields and E4 policy header.

Initial E1/F1 merged-tree history sizing from the rerun (not a rewrite of the historical figures above):

| E4 history fixture | Four products | Eight products |
| --- | ---: | ---: |
| Rows / product cells | 14,336 / 57,344 | 14,336 / 114,688 |
| Initial ASCII characters = UTF-8 bytes | 8,649,017 | 10,135,865 |
| Timed characters after updates | 8,864,057 | 10,350,905 |
| Streaming / semantic records | 74,755 / 71,683 | 136,195 / 129,027 |
| Bulk update p95 / max ms | 0.6383 / 0.9966 | 0.3282 / 0.9154 |
| Bulk serialization p95 / max ms | 194.9211 / 198.8847 | 172.8633 / 174.5950 |
| Serialization samples >=30 ms | 21/21 | 21/21 |

The initial/timed saves each grow **32 bytes** from the original E4 fixture because E1 now contributes its weather fields; row/cell totals and charges stay exact. **All 42/42 bulk serialization samples >=30 ms are flagged**, max **198.8847 ms**. These are full offline saves, never clean ordinary-play samples. No update, selection or supply sample reaches 30 ms. Four/eight-offer selection p95 is **0.0131 / 0.0251 ms**, with zero allocations and sixteen route slots. The rerun retains the existing supply allocation result **1,960 -> 1,552 B** at four offers, **2,368 B** at eight. This is confirmation of E4's existing win, not a new integration win. Sequential .NET 10 Release field doubles on a shared development PC, JIT/tiering and scheduling limit timing comparisons; fixture construction is excluded. No causal CPU/native improvement, whole-frame result or combined mod-work gate is claimed. The four parked hotspots remain parked.

All twelve conflict resolutions and integration-specific changes are listed in the [sync PR description](../plans/e4-sync-base-pr-description.md). Core/Runtime files outside the model conflict match the automatic composition of the two approved branches; all incoming-only documentation and all incoming checks index lines are preserved.

The additional sweep found a stale food-candidate partial native double (`Supported` instead of E4's `SupportedMenu`). Only the fixture method name changed; its eligibility expression and all assertions stayed intact. Its rerun passes. The default legacy/weighted economy matches `51ce709` exactly (849/797/2 and 531/499/1 orders/purchases/unconfirmed, with identical order/economy/reload digests). C4-excluded versus included schema-23 fixtures are byte-identical: population 41,860 B, food 66,333 B. These completed results precede the later A5 integration described below.
