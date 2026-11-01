# Sync merged B0 and development slices into approved B0b

PR #16 conflicts after B0 was synced in PR #38 and squash-merged as `3b8f7ec`. Merge `origin/codex/persistent-city` at `3b8f7ec` into approved B0b `c0af745`, with common ancestor `7a6153c`. This PR targets `codex/pc-b0b-held-tuning` and must land with a **merge commit** so B1 #17, C2a #24 and C4 #30 retain the reconciled ancestry. The harness handles pushing and opening this sync PR; PR #16 can then be squash-merged into `codex/persistent-city`.

Conflict resolution and integration only. Both parents' diffs against the merge base are retained in ignored `build/b0b-sync-base-gate/`, alongside the resolved-parent audit. Comparing the approved B0b delta against B0 `192037b` separates its changes from the shared pre-squash A1/A2/A3/B0 history. No rebase, squash or cherry-pick was used.

| Conflicted file | Resolution |
| --- | --- |
| `CHECKS.md` | Preserve both complete histories in order: B0b tuning/replay/capacity/refusal evidence and its saved-payload rig instructions; B0/A3/A2/A1 sync evidence and every F0/D1/C1/E2a section. All seven In-game checks sections and every LOCKED-OK, NEEDS-INPUT and input-only qualification survive, with local checklist numbering. |
| `README.md` | Keep B0b's tuning and 405-byte notice plus all development/B0/A3 and sibling notices. Adjust only B0/A3 checklist links shifted by the restored B0b checklist. |
| `docs/roadmap.md` | Keep B0b between B0 and B1 and its measured streaming-precheck performance row, plus B0's sync status and every development plan/product-decision/performance row. |
| `docs/plans/step-a-inspector-traits-choice.md` | Keep the complete shared A3 plan and development's native complete-address correction; matches development exactly. |
| `docs/population-schema18.md` | Keep B0b's complete in-place amendment, twelve-word image order/bounds, legacy defaults, frozen readers, B1 contribution reservation, all-held-context bound and byte/record ledger; matches approved B0b exactly. |
| `Core/CitySimulator.cs` | Keep development's complete-address formatter API together with the shared A3 continuation scheduling and B0 capability guard; matches development exactly. |
| `Core/FoodChoiceSettings.cs` | Keep B0b's private effective-settings copy; matches approved B0b exactly. |
| `Core/FoodToday.cs` | Keep B0b's immutable tuning reference and neutral bounded per-candidate memory slot, plus the shared B0/A3 contracts; matches approved B0b exactly. |
| `Core/FoodWeightedCommerce.cs` | Keep capture before shortlisting, captured settings for shortlist/selection, and one live capture for migrated/pre-image continuation; matches approved B0b exactly. |
| `Core/ResidentInsight.cs` | Keep development's complete native address formatter, readable fallback and raw map arguments plus all shared traits/meal explanations; matches development exactly. |
| `Persistence/LegacyPopulationCodec.cs` | Keep B0b's frozen decision/shop property allow-lists so schemas 13-17 never acquire new fields through current Core; matches approved B0b exactly. |
| `Persistence/SaveCodec.cs` | Keep the entire B0 schema-18 codec/migration/size contract plus B0b's strict tuning/contribution validation, two permitted pre-image omissions and bound on every held decision including empty shortlists; matches approved B0b exactly. |
| `Runtime/ResidentInspector.cs` | Keep development's native `Streets.AddressHelper.ToFormattedString` callback; matches development exactly. |
| `config/persistent-city.json` | Keep all twelve A3 coefficients and all four default-off F0 settings; matches development exactly. |
| `tests/ResidentInsightTests.cs` | Keep every test method, including development's corrected formatter/fallback cases and the retained earlier street-key case; matches development exactly. |
| `tools/build.py` | Keep development's F0 assembly reference, host fallback and missing-host/SDK diagnostics; matches development exactly. |
| `tools/native-command-regression/run.py` | Keep development's checked host fallback and all existing production-source extraction/argument forwarding; matches development exactly. |
| `tools/optional-stop-regression/run.py` | Keep development's stationary-crowd fixtures, findings/performance argument forwarding and occupancy/cap harness; matches development exactly. |
| `tools/physical-selection-regression/run.py` | Keep development's checked host fallback, F0 wiring tests and selector comparison; matches development exactly. |
| `tools/save-contract-regression/CapacityBench.cs` | Keep B0b's 5,000-consumer fixture and exactly-once subtraction of the real 405-byte image from the existing policy/header reservation; matches approved B0b exactly. |
| `tools/save-contract-regression/Program.cs` | Keep B0b's real held-image fixture, held-contract checks and amendment-aware digest comments, plus development's Runtime import and all twelve F0 off/on hidden/save/reload equality checks. |

Clean merges retain all sibling production files, E2a fixtures, migration tests and the union of test/save-contract project inputs. B0b's entire `HeldFoodTuningTests.cs`, held-tuning regression and held save-contract checks remain unchanged. The imported E2a plan's checklist link moves from suffix `-2` to `-3` because B0b adds a preceding heading. The historical PR #16 description was not conflicted, so its old retarget-after-B0 sentence is deliberately untouched.

**Schema audit:** development still writes population **18**, with `PopulationWire18.cs`, `Core/Model.cs`, `Core/ShopMemory.cs` and the base B0 contracts identical to B0b's approved B0 ancestry. No other merged slice claims B0b's `Tuning` or `MemoryContribution`, changes the tuple slots, or claims schema 19. B0b amends unshipped 18 in place. Schemas 13-17 retain frozen validation before migration; schema-17/pre-image-18 contexts capture live once on first continuation, selected old contexts keep their winner, and malformed/future/unsupported saves remain preserved and refused.

**Exact code impact:** every production C# file matches one parent exactly. All B0b Core tuning/commerce/held-context code and every Persistence source are unchanged from `c0af745`. Existing development inspector and sibling behavior is imported intact. The only tool combining distinct parent code is the save-contract entry point described above. No additional integration fix, new runtime/Core behavior, tuning change or performance optimization was needed.

All **49/49 foreground checks pass**, including all **32 requested commands**:

- Production build against the supplied Steam Managed directory, warnings as errors. DLL SHA-256 `5f1173725cf7155fc795d3f43dc086b52bef08affac5b7383e683e9f48e00139`.
- **1,984/1,984 unit tests**, zero failed/skipped, and **1,984 discovered cases**. This is development's 1,954 cases plus all 30 approved B0b cases.
- All 22 requested Release regression projects, all four Python harnesses and all three demos with the requested arguments.
- Five main extras: native food-candidate, food-choice, held-tuning, choice `--rush`, and optional-stop `--performance --findings`.
- Twelve additional checks: same-schema target/candidate save/economy comparison; base recovery; six capacity fixtures (reference, 5,000 consumers, 5,000 residents, ceiling, CF32, CF64); input boundary; record boundary; history churn; and the nested pickup helper regression. All **26 regression projects** in the tree pass, including that helper's 25 actual Core scheduler/kernel checks.

The prescribed other-dotnet guard ran before every invocation and waited for another worktree before the initial build. Commands ran sequentially in the foreground without execution deadlines; child build/compiler-server reuse was disabled. All commands completed, with no background test, watcher or server left running. [Tracked receipts, log hashes, preservation audit and measurements](2026-11-01-b0b-sync-base-results.json); full logs, parent diffs and baseline source extraction remain ignored under `build/b0b-sync-base-gate/`.

**Migration, replay and refusal evidence:** all schema-13/14/15/16/17 migration and strict older-reader cases, amended-18 round-trips, evaluating/partial-quote/selected reload replay, live-once legacy capture, same-minute next-consumer capture, every coefficient, malformed-image/source preservation, neutral B1 reservation and empty-shortlist bound pass. The eight-candidate image still adds exactly **405 characters/UTF-8 bytes** and no collection records. Every approved B0b test and all existing migration cases are retained unchanged.

The comparator compiles the same tool against archived tracked development sources and the merged sources without switching branches. Both write schema 18: the basic population fixture is byte-identical, **119,083 characters**, digest `018FC26676937FEEF88B7D06AE32B522CCE93A1CC265BCDD87A08C7DD91FBB56`. Refusal outcomes and exact economy/reload digests match: **849 orders, 797 purchases, two unconfirmed and 107,967,424,757,760 spent units**. All twelve F0 off/on hidden/save/reload comparisons pass. Held-image fixtures intentionally gain B0b's approved bytes; their distinct size contract is checked separately.

**Save budget:** base recovery remains above 100 Mi-characters: **115,049,515 characters** (248,039,560 -> 132,990,045). Compared with B0, recovery falls by precisely the 405-byte held image. Full four-entry memory and the complete 20,000-reference/2 Mi-character-and-byte table produce:

| Fixture | Actual characters = UTF-8 bytes | Streaming / semantic records | Complete reserved characters = UTF-8 bytes | Reserved records |
| --- | ---: | ---: | ---: | ---: |
| Reference, 957 residents / 1,914 consumers / 32 visits | 5,013,141 | 54,560 / 54,557 | n/a | n/a |
| 2,500 residents / 5,000 consumers / 32 visits | 9,701,305 | 110,108 / 110,105 | n/a | n/a |
| 5,000 residents / 10,000 consumers / 32 visits | 17,298,040 | 200,108 / 200,105 | n/a | n/a |
| Ceiling, 50,000 residents / 150,000 consumers / no visits | 141,087,195 | 320,108 / 320,105 | n/a | n/a |
| CF32, 50,000 residents / 150,000 consumers / 32 visits | 192,020,107 | 1,920,108 / 1,920,105 | **220,664,366 (82.204%, <=85% pass)** | **2,111,867** |
| CF64, 50,000 residents / 150,000 consumers / 64 visits | 230,420,107 | 3,520,108 / 3,520,105 | **259,064,366 (96.509%, hard-cap pass)** | **3,711,867** |

Each actual fixture is B0 +405 bytes with unchanged records. The existing policy/header allowance deducts these 405 exactly once; complete future budgets remain unchanged. Caps remain **268,435,456 characters/UTF-8 bytes and 4,000,000 records**. CF64 retains its >95% warning and has no 85% claim. Ordinary 32 visits and accepted 64 visits are unchanged.

The exact character/UTF-8 cap passes; one character or UTF-8 byte over and oversized source schemas 13-17 refuse without replacing source text/reference. Exact **4,000,000 streaming / 3,999,997 semantic records** pass at **241,306,355 characters/bytes**; one additional record refuses read/write/store/session atomically. The 280,000-generation history stress refuses global size while preserving every history/product total, **280,000 purchases** and **7,516,192,768,000 revenue units**. No truncation or cap increase.

Capacity timings are one diagnostic sample per fresh process, not p95 or native frame/save evidence. Every listed write/read/migration operation is **>=30 ms** and is explicitly flagged in the results as separate synthetic save/load/migration latency. Reference write/read/17-to-18 migration: **130.1/371.2/533.7 ms**; 5,000 consumers: **160.4/763.3/1,817.5 ms**; 5,000 residents: **586.5/1,206.0/1,807.2 ms**; ceiling: **1,942.4/6,904.1/11,754.6 ms**. CF32/CF64 oversized schema-17 sources refuse rather than becoming fabricated migration samples. All six fixture timings, retained allocations and process peaks are in the results; process peaks include fixture construction and GC, not an operation-only managed peak.

Other offline diagnostics: 1,024-scope native-ID pure-policy p95 **0.1831 ms**, full-decision p95 **0.3061 ms**; maximum full decision/continuation across matrices **10.522/3.1037 ms**. Held eight-shop capture/continuation p95 **35.5/192.2 microseconds**, no measured >=30 ms stage sample; retained DTO graph **1,832 bytes**, matching approved B0b. D1's cold 20,000-place aggregate **42.9453 ms across bounded units** is explicitly flagged, not treated as one native frame. C1 synchronous cold 1,024-business p95 **6.8791 ms**, warm **2.4063 ms**, bounded continuation p95 **0.0015 ms**, with no >=30 ms reader sample. Simultaneous 957-resident optional-admission p95 **7.5526/11.8566 ms** at 25/100%, zero >=30 ms samples; maximum food-routine 16-event batch **15.2409 ms**. Rush replay retains paired save digests and zero modeled backlog. These are partial stage/batch costs, separate from native mod-work and whole-frame measurements; no new integration performance win is claimed.

Preservation assertions verify both entire CHECKS histories as ordered subsequences, all roadmap lines except the older pending-B1 sentence expanded by B0b, all seven checklists/qualifications, all parent test methods and both project-input unions. Every production C# source matches one parent exactly; all seven production files changed by approved B0b match `c0af745` byte for byte.

No game, rig/profile/bridge lease, Steam/Workshop mutation, WSL distro, ordinary save, deployment, release, push or PR publication action. Under merge rule v2, in-game checks belong to the later batched combined locked rig on `codex/persistent-city`; pointer-only checks wait for Peter's 6 Nov playtest. Offline evidence makes no native clearance claim. Existing mod-work p95 <5 ms, every spike >=30 ms flagged, whole-frame cost separate and all four parked hotspots parked remain standing practice.
