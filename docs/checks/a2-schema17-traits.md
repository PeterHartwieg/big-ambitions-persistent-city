# A2: Schema 17, lifestyle traits and base sync evidence

## A2 base sync after merged A1/F0/D1/C1/E2a - 31 October 2026 (offline only)

Merge base `84660a5` into approved A2 `41c870f`, keeping both parents and common ancestor `7a6153c`. Resolve all twelve conflicts with both sides' complete intent: A1's corrected native address formatter plus A2 traits/Today outcomes; every check, migration case, test, plan/roadmap/performance entry and native checklist marker remains. The base has not claimed schema 17 or changed the schema-16 population contract; A2's codec/model/traits/outcome contract and schema-17 tests remain unchanged. [Prepared PR description, per-file resolutions and later A2 rig checks](../reviews/2026-10-31-a2-sync-base-pr-description.md).

**Validation:** all **32/32 required offline commands pass**, including the read-only Steam-reference production build (warnings as errors), **1,791/1,791 unit tests** with zero failed/skipped, separate test discovery, all 22 Release regression projects, four Python harnesses and three demos. Two extra checks also pass: schema-16-base versus schema-17-candidate save/economy comparison and optional-stop `--performance --findings` (**34/34 including extras**). The comparator retains identical orders, money and reload economy: **849 orders, 797 purchases, two unconfirmed**; F0's twelve off/on save/reload byte-equality transitions pass. One minimal tool-only integration fix moves the F0 PASS message to stderr so the comparator receives valid JSON; no production behavior change beyond the parent combination. Production DLL SHA-256 `9b820a695480e842b09a4876a8466077163a6dca27850744b9cefe48ad723e30`. Sequential foreground commands, prescribed other-dotnet guards, disabled build-server reuse, red/final logs and preservation checks: ignored `build/a2-sync-base-gate/`. `git diff --check` passes.

**Offline timing flags:** D1 aggregate cold build **39.0773 ms across bounded units**; C1 1,024-business synchronous cold p95 **5.9115 ms**; optional-stop 957-resident simultaneous admission p95 **8.5257/9.3130 ms** at 25/100% (zero >=30 ms samples in all reported optional-stop arms). These are synthetic costs, separate from native frames; details are in the prepared description. Native practice stays mod-work p95 <5 ms, every >=30 ms spike flagged, whole-frame timing separate, and the four broad hotspots parked. No new performance win is claimed.

This ancestry sync lands by merge commit into A2. The separate locked-PC `rig_test` session follows before PR #5's squash merge into the development base: real schema-16 migration/reload, bridge-captured Today meals, save time/size and mod-work against base. Inspector trait words remain input-only for Peter's playtest unless bridge-captured. Dated parent evidence below is preserved as history. No game launch, rig/profile/bridge lease access, ordinary-save deployment, release, push or PR publication action.

## Step A2 population schema 17 and lifestyle traits - 30 October 2026 (offline only)

Implements slice A2 of the [step A plan](../plans/step-a-inspector-traits-choice.md), stacked on A1. 17 was confirmed as the next free population schema (current was 16). No game, rig or native run.

**What schema 17 adds.**
- `CitizenState.Traits`: one required record per resident, also when there is no food scope. Ten traits in generation version 1 (three product tastes, price sensitivity, queue patience, loyalty/curiosity, travel willingness, health, quality/brand and time pressure). Each is a fixed band 0-4 stored as one digit in `Bands`. Traits come from the saved `CityState.Seed` and the stable ID through `pc:traits:v1|<length>:<id>|<trait>`, with no names, homes, jobs, bodies or settings in the key. Shares are 10/20/40/20/10%. Traits are immutable after generation, and event copies share them.
- `ResidentMeal.RepresentedTraits`: required for slots above zero, keyed by the stable consumer ID. It is null for slot zero, which uses its anchor's traits.
- `FoodCommerceState.ChoicePolicyVersion` (1) and `ChoiceStartsDay`. A new food scope starts at day 0; a migrated city starts the next in-game day.
- `ResidentMeal.Today`: at most three outcomes keyed by breakfast, lunch and dinner, or one legacy meal. Each holds a status (bought, chose an alternative, skipped, unconfirmed), a fixed reason code, optional shop and product, the alternative kind, at most two factor codes, the recording minute and the checkout minute. One shared updater (`MealOutcomes.Record`) runs at `Finish`, `ConfirmPurchase` and the checkout-refusal assignment. It is idempotent per intent, and the day advances lazily on the consumer's next outcome, so there is no midnight scan. A late confirmation never overwrites a later day.
- Wire contracts for A3, with their bounds: nullable `ResidentMeal.Decision` (at most 8 shops, 4 offers each, and only one route-bearing decision in the city carries the shortlist and its walking routes) and the retry latch `LastContextKey`/`ContextAttempts` (at most 4 contexts per meal). A2 never writes them.
- No A3 logic: food choice, prices, purchases and money are unchanged. No new native authority, rival writes, RLO change or config keys; nothing reads choice tunables until A3.

**Migration.** `SaveCodec.Read` accepts 13/14/15/16 through an explicit allow-list. Schema 16 now has its own contract resolver. One `IntroducedIn` table drives the per-schema unknown-field exclusions, the missing-field counts and the resolvers. After the old shape passes its own contract, `UpgradeTo17` generates traits, sets activation to the next day and synthesizes only the current meal's recorded facts. Meals of that day which ended earlier are marked "not recorded before upgrade". The full schema-17 validation then runs before a store or session may replace the payload. Refused and preserved: schemas 1-12, future schemas, partial/duplicate/unknown fields, bad trait version or bands, wrong slot linkage, invalid policy or activation day, impossible or mismatched outcomes, nonfinite values, oversized outcome/shortlist/menu arrays, a second route-bearing decision, unknown live products and inconsistent route endpoints. Shape limits run before deserialization, and the new nested records count toward `MaxEmbeddedRecords`.

**Inspector.** Personality shows trait words only ("loves burgers", "watches prices", "dislikes queues", "happy to travel for food", "prefers lighter food", "values quality", ...); neutral bands and loyalty/curiosity are not described. Today's meals come from the recorded outcomes, with a meal under way shown live. Earlier upgraded meals read "not recorded (before the upgrade)", a funded alternative reads "chose household food", and an unconfirmed checkout is never called paid or skipped. Without a food scope, meal data reads unavailable; opening the popup changes nothing.

**Same economy as before.** `tools/save-contract-regression` gained a fixed three-day economy scenario. It has 60 residents with two consumers each, three meals, finances, three shops, checkout refusals, lost acknowledgements with later proof and a mid-run reload. Compiled against the pre-A2 tree (`a817bcd`) and this branch, it gives identical results: **849 orders, 797 purchases, 2 unconfirmed**, identical order log, money and saves outside the schema-17 fields, before and after reload. `S17_EconomyScenarioMatchesThePreSchema17Baseline` pins those baseline values. `compare.py` passes; refusal outcomes match too.

**Save size and time (offline .NET 10, this PC, median of 5/3/3 runs, one run for factor 3; not Unity/Mono). Updated in review round 2.** Household finances, two consumers per resident unless stated. Schema 17 is measured at its largest steady state: every consumer holds three recorded purchases at the end of the day, each naming a shop in the native address format (`address:<escaped street>:<n>:shop`) and a live product.

| Residents (factor) | Chars 16 → 17 | Share of input cap | Write ms | Read ms | Read+upgrade from 16 ms | Retained MiB |
|---|---|---|---|---|---|---|
| 957 (reference) | 2,662,766 → 3,470,518 (**+30.3%**) | 1.3% | 64.9 → 69.3 | 81.1 → 289.0* | 51.9 → 80.7 | 1.8 → 3.5 |
| 5,000 | 13,916,700 → 18,136,744 (**+30.3%**) | 6.8% | 109.1 → 155.3 | 416.4 → 521.1 | 415.1 → 394.7 | 9.4 → 18.5 |
| 50,000 (codec ceiling) | 131,318,953 → 173,518,997 (**+32.1%**) | 64.6% | 946.1 → 1,340.5 | 4,131.8 → 4,163.7 | 4,231.6 → 3,712.6 | 79.4 → 171.3 |
| 50,000 (factor 3, 150,000 consumers) | 177,730,855 → 240,680,899 (**+35.4%**) | 89.7% | 1,529.9 → 1,228.5 | 5,467.8 → 4,650.5 | 5,814.0 → 4,036.8 | 104.8 → 242.6 |

\*An outlier: the same read measured 75.9 ms in the focused profile below. Single runs on this desktop vary by up to about 30%, so only the size columns are exact.

Schema 17 still makes end-of-day saves **noticeably bigger and somewhat slower**: +30-35% characters, about 1.4× write time and about 2× loaded memory. Almost all of the growth is the three outcome records per consumer, now one compact string each (review round 2); traits plus empty decision fields add about 9%. Early in the day, payloads sit between the schema-16 and end-of-day sizes.

**Roster ceiling unchanged.** The supported roster limit (residents × factor, up to 150,000) is the same as before schema 17. The round-1 consumer ceiling of 130,000 was reverted in round 2, because it refused valid schema-16 cities. Instead, outcome records were compacted, so the largest supported end-of-day payload (50,000 residents at factor 3) fits at 89.7% of the 256 Mi-character cap. Real cities carry more journey and visit data than this fixture, so the cap shares are lower bounds. `SaveCodec.Write` still refuses any oversized payload rather than truncating it, and the source payload is preserved.

Upgrading a schema-16 payload costs about the same as reading one. Trait generation takes **0.4-0.7 µs** per consumer, once at creation or upgrade. The outcome updater takes **65-80 ns** per call and allocates **192 bytes** per consumer-day, with no per-frame or midnight work. Rig save time and size versus base are still unmeasured.

**Perf wins taken (low risk, same acceptance and error text).** Validation messages are built only on failure. The shape check reuses one duplicate-field set per depth, uses bit flags for positions and checks integer ranges without formatting. Positions are streamed instead of building a `JObject` each. At 957 residents with journeys: read allocation **83.3 → 38.8 MiB**, read **119.7 → 89.2 ms**, validation allocation per save **8.39 → 1.31 MiB**, write allocation **26.1 → 19.1 MiB**. Traits as digits save 174 characters per record (333,036 at 957 residents).

**Validation.** New `S17_*` tests:
- trait golden vectors from an independent key reimplementation, determinism and band shares, and independence from order, names, districts and seed changes;
- represented slots, housing-expansion residents, and events never changing traits;
- 13/14/15/16 → 17 with exact preservation (everything except activation day and outcomes is byte-identical), next-day activation, and a meal in progress finishing on legacy terms with identical orders afterwards;
- upgrade synthesis rules and a full-contract round trip with a route-bearing decision;
- **61** refuse-and-preserve cases through codec, store and session, including four codec regressions for the perf changes, plus future schemas;
- outcome capture, idempotency, skip→purchase replacement, lazy midnight, late receipt confirmation, and inspector words and today's meals.
Existing migration tests now drop schema-17 fields when labelling older schemas. `dotnet test` **1,641/1,641** before review.

**Review round 1 fixes (offline).**
- **Current outcomes agree with the retained meal (Astra r1-1).** A bought or unconfirmed outcome of the meal still held must name that meal's shop, product and checkout minute (`MealOutcomes.CheckoutMinute`). Once the meal advances, the record is an opaque historical reference, and a shop that has since left the catalogue stays valid. Skipped and alternative outcomes name no shop or product. Changed shop, changed product, erased checkout and moved checkout are refused and preserved through codec, store and session, both for live-recorded and upgrade-synthesized purchases, and refused at write.
- **Compact decisions (Astra r1-2).** A selected decision's product must be a live product even when it carries no shortlist image.
- **Complete decision snapshot (Astra r1-3).** `FoodDecisionShop` also captures the weekly opening windows (same rules and `MaxOpeningWindows` bound as places, counted toward `MaxEmbeddedRecords`), whether native service was refused, customer-service quality (-1 unknown, else 0-100) and the shop's district. `FoodDecisionOffer` adds district demand (-1 unknown, else 0-100, requiring a district). Shape, validation and round trip are covered; A3 choice execution stays inactive.
- **Outcome compaction (Opus r1-1).** `At`/`CheckoutAt` are whole minutes stored as integers, and only purchases keep shop and product. A bought record goes from 210 to 182 characters and a skipped one from 196 to 122.
- **Realistic size benchmark (Opus r1-2, r1-4).** The benchmark now uses native-format shop addresses and three purchases per consumer, takes three samples at the ceiling and also measures factor 3. This found the factor-3 overflow and led to the consumer ceiling above.
- **Rig checklist (Opus r1-3).** Trait words are checked for stability and words-only, not against purchases: they have no behavioural effect until A3.
- 11 new refusal cases (current-purchase mismatch ×4 in two forms, non-purchase naming a shop, compact unknown product, quality, opening hours, oversized hours, district and district-demand evidence, fractional outcome minute). `dotnet test` **1,652/1,652**. The economy scenario and its pinned baseline values are unchanged.

**Review round 2 fixes (offline).**
- **Roster ceiling restored (Astra r2-1).** `MaxFoodConsumers` is removed. The shape limit is back to `MaxCitizens × 3`, and validation adds no roster cap. The size overflow is handled by compaction instead: each outcome record is one compact string, `Kind|Status|Reason|Outside|Factor1|Factor2|At|CheckoutAt|ProductId|PlaceId`, with strictly canonical non-negative integers; the place is last, so an opaque place ID may contain `|`. Shape and converter refuse any other form. A bought record goes from 182 to about 85 characters. New test: a 43,400-resident city at factor 3 (130,200 consumers) loads from schema 16, and saves and reloads at its end-of-day state (23 s).
- **Quote progress (Astra r2-2).** Each shortlisted shop records `OutwardQuote`/`ReturnQuote` (`Unrequested`, `Answered`, `Refused`, `NotNeeded`), and the decision records `QuotesDone`, its cursor in shortlist order. Validation checks:
  - slots before the cursor are settled and slots from it on are not;
  - a route is present exactly when its slot is answered;
  - a return is asked only after an answered outward quote;
  - a decision chooses only after all slots are settled;
  - a selected offer's shop has both quotes answered.
  An interrupted decision with a refused outward quote, a refused return quote and a pending shop round-trips exactly, and it differs from the same image before the refused return was asked.
- **Enum validation without boxing (Opus r2-1).** The schema-17 enums in outcomes and decisions are dense from zero. They are checked with an unboxed range check, `Dense`, that keeps `Known`'s message, and a test pins each enum's maximum and density. At 957 residents with three outcomes per consumer: validation allocation per save **1.83 → 1.04 MiB** and validation time **4.3 → 2.7 ms**.
- **Native proof (Astra r2-3).** Still outstanding: it requires the separate rig_test session before merge (below).
- 12 new refusal cases (record form, quote progress) and four new tests (compact record round trip and enum pins, interrupted-decision round trip, a product that cannot be written compactly, the 130,200-consumer city). `dotnet test` **1,668/1,668**.

**Review round 3 fix (offline).** The reader's cap on a compact outcome record was 512 characters, but validation accepts records up to 553 (two 256-character IDs), so `Write` could produce a save that its own reader refused. `SaveCodec.MaxMealOutcomeWire` = 600 now covers every record validation accepts. A test writes and reads back a record with two 256-character IDs (red under the old cap), refuses a 257-character ID at write, and refuses and preserves a record above 600 characters. Native proof (Astra r3-1) is still outstanding; see the rig checklist below. Offline gate at `dd6107b`: **31/31** commands exit 0, `dotnet test` **1,669/1,669**, production DLL SHA-256 `9c44a0eb1afa9df6f2acfb8a74927c900e9871fb17c249afd0222f050b4573d0`, logs in ignored `build/a2-gate-r3/`.

**Offline gate after round 2 (code head `50ee041`).** All **31/31** gate commands exit 0, including `dotnet test` 1,668/1,668 and the save-contract baseline/candidate economy comparison. Production DLL SHA-256 `912025d094003b94d137ef7f150930971cdbfee5a84bc55b4b2a6fe7fca36979`. Logs: ignored `build/a2-gate-r2/`.

**Offline gate (code head `123775c`, superseded by round 2 above).** All **31/31** gate commands exit 0: the production build, `dotnet test` (1,652/1,652), test list, 21 standalone regressions including the save-contract baseline/candidate economy comparison, four Python harnesses and three demos. The food-routines and food-demand demo reports equal the pre-A2 baseline, excluding timing fields. Production DLL SHA-256 `27f5dfa5b42f1bce6eec4c2ac7e81121da55edd28122b4dddbbb18e80c6962d6`. Logs: ignored `build/a2-gate-r1/`.

**Rig session still required before merge (`rig_test`, Codex Sol, high), with baseline-to-candidate proof attached to the PR.**
- Migrate a real schema-16 save of a disposable FT city, reload it, and save/reload again as schema 17.
- Trait words: stable for the same resident across reload, pool reuse and camera movement, and never numbers. Trait words have **no behavioural effect until A3**: a resident shown as "dislikes burgers" or "watches prices" can still buy burgers or expensive items under the legacy choice, and that is not a defect.
- Today's meal lines agree with native purchases (product, shop and bag/order evidence), including an upgraded day's "not recorded (before the upgrade)" meals and an unconfirmed checkout.
- A meal interrupted by the upgrade (travelling, serving or unconfirmed) finishes on its legacy terms; the next-day activation leaves money and receipts unchanged.
- Save time and size and mod work against base, with autosave off in measured windows (record the setting, verify after reload, restore it). Flag every mod-work spike of 30 ms or more.


## Development notices

> **Step A2 population schema 17 - 30 October:** every resident and represented consumer gets saved lifestyle traits (seeded, fixed 0-4 bands, shown only as words in the inspector). Each consumer records today's meal outcomes, and the A3 decision contract is in place. Valid 13-16 saves migrate exactly, with the new policy starting the next in-game day. Food choice and economy are unchanged against the schema-16 baseline. End-of-day saves are about **30-35% larger**; the largest supported city (50,000 residents, three consumers each) stays under the input cap. Offline only; native rig proof required before merge. [Evidence](a2-schema17-traits.md#step-a2-population-schema-17-and-lifestyle-traits---30-october-2026-offline-only).


[Checks index](../../CHECKS.md).
