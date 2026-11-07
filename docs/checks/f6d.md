Status: implemented (offline); not verified by rig

# F6d — inactive economic ledger contract

Refs #93. Base: `b2a0ab706c1ef7e6f37a7a7b00041f5eac717dab` (E6c), matching `origin/codex/persistent-city` when the slice started. F5c-lite/26, F5d/27, F5e/28, C5c/29, F7c and E6c/30 are on that base. The writer is allocated as `CityState.EconomicLedgerSchema = VendorSchema + 1` (31 on this base), never as a literal next-writer number.

## Implemented contract

- `CitizenState.Accrual`, including each visitor's own routine, belongs to that PersonId's current F5 Employment (`DayPlaceId`/`EmploymentStartDay`; the visitor's Employment derives those fields). It carries attended minutes, exact unposted wage units, a 14-day miss mask and day/shift watermarks. An unemployed person must have the zero tuple.
- `ResidentBudget.Ledger` extends F5's one wallet per PersonId with exact EmploymentIncome, Discretionary and a visit-minute watermark. Existing `IncomeReceived` is OtherIncome while F6d is inactive. No household wallet or second owner key is introduced.
- `CityState.Economy` carries fourteen dated outside-spending rows (fixed retail/leisure categories, exact wide units and visit counts), bounded observed-shop fingerprints and journals. Business references retain F5/B's saved stable slots. Journal products are canonical names in Core, translated through the wire-local name table on every save; journal-only products survive table reordering.
- Each `FoodShopDay` has sixteen fixed counters: legacy/breakfast/lunch/dinner × staff/stock/service/closed. They follow the row's existing fourteen-day retention. No new counter producer is active.
- New accounts/tuples start at zero; observations, fingerprints and journals start empty. Structural event copies and detached F7c snapshots preserve the fields. No wage, discretionary admission/debit, fingerprint observer, journal producer or panel policy is enabled. No switch-over field is added.
- `Opening + EmploymentIncome + IncomeReceived = Balance + EssentialsPaid + HomeFoodPaid + PreparedFoodSpent + ExternalFoodSpent + Discretionary + HeldPurchase`. Existing posting, hold, confirmation, refusal and rival debit transactions check this identity. Checked totals refuse overflow before publication. Save validation converts conservation overflow to a save refusal; stores and sessions retain their prior payload.
- Strict readers reject missing/duplicate/unknown fields, invalid enums, alternate scalar spellings, out-of-range values and oversized collections. Old development writers receive `DevelopmentSchema` / “start a new city”; originals remain unchanged. No migration. Capability and save-epoch rules remain intact; future activation must use the existing capability revision or a new writer as required by its slice.

E6c's vendor wire and validation file and `FoodSaleReceipts.cs` are untouched. E6c's four writer-30 golden fixtures remain pinned through its frozen exact writer/reader; vendor behavior tests also exercise the current writer. No F6b held-estimate fields were needed: `RivalServiceLoad.Reconstruct` still derives load from existing active meals and dated outcomes. Nothing was added for F6b.

## Offline evidence

`EconomicLedgerTests` exercises populated round trips for every new field, detached snapshots, current-writer hashes, fresh zeros, preservation across codec/store/session refusals, canonical money, HTML-safe literal fingerprint encoding, all new bounds, owner conservation overflow on both sides and atomic posting refusal. `F6dMeasure` composes the E5d size matrix against the current F5/F7c/E6c graph and counts both record paths. `F6dHardCapTests` exercises the actual global record, character and UTF-8 limits with the populated ledger. Results and commands are in [the perf record](../perf/f6d.md).

The two seeded populated writer pins (SHA-256, UTF-8/ASCII characters) are:

| Seed | Characters | SHA-256 |
| --- | ---: | --- |
| 5150 | 172,016 | `06b494d2627e0d6675d8c437e1c0dff72e176ab3ae7aaa54a0f152052aace308` |
| 9217 | 171,980 | `c026ffd0611c3a89a0809f3a5217525206d91c853a5d1f443b2d25ac4d30c664` |

`F6dInactiveTraceTests` runs three days with hourly hashes of the complete predecessor projection, purchase-order logs and daily real-codec reloads. At every hourly checkpoint the current build also asserts that all new fields remain zero/empty. Its source was also compiled and run at archived base `b2a0ab7` with `F6D_BASELINE`; both hashes match that actual predecessor and the new writer:

| Seed | State/order trace SHA-256 |
| --- | --- |
| 5150 | `256d605ec0384ff6fdaaa26d44dc48dd825706c88a4f195eab2297bb79679381` |
| 9217 | `046eed1463692641d46f2d7c93ed1604ac3d181d49005184fe362521b921fbb7` |

The broad tests retain old writer pins and exercise frozen historical codecs explicitly. Their reflection contracts exclude the new fields. One deliberately unbalanced overflow fixture now expects refusal at the first ledger mutation, before native checkout, because F6d checks the conservation total there.

## Local checks

- Required `tools/build.py` build with the provided game-managed shim: passed. Local package DLL SHA-256: `b0289829ad0a9b5ad609598ab3de0897e7359c3461cac4db21479694c209aa8f`.
- Required `dotnet test tests/PersistentCity.Tests.csproj`: **3,661 passed, 0 failed**, foreground Debug run (3 m 34 s).
- Strengthened hourly zero/empty inactive trace: **2 passed** in a subsequent focused foreground run.
- Slice size/budget measurement: **2 passed** in Release, including all eight matrix fixtures with three samples per operation (9 m 36 s).
- Slice populated-ledger global caps: **1 passed** in Release (2 m 11 s); 4,000,000 records and exactly 268,435,456 characters/UTF-8 bytes round-trip; all one-over variants refuse and keep originals.
- Slice transaction microbenchmark: **1 passed** in Release; 100,000 owners, zero measured allocation, no ≥30 ms samples.
- `python3 tools/docs-links/check.py`: 365 Markdown files, zero broken relative links. `git diff --check`: passed.

No regression matrix, game or rig run was performed. Shared-machine test runs were preceded by process checks; when another testhost appeared after preflight, this slice's unit run was stopped and restarted after that harness exited.

## In-game checks for the next batched rig

1. **Fresh-city writer save/reload — locked-PC; rig-automated.** Setup: disposable fresh FT city with the F6d build, using existing `rig flow run`, FT `state`/`save`/`load`/`console`; pending capability: export and assert the new ledger fields. Steps: create the city, export its initial economic shape, save, reload and export again. Expected: the next-free writer loads; every new account/accrual/cause counter is zero; fingerprints and journals are empty; IDs and existing money match across reload.
2. **Older-city refusal with payload kept — locked-PC; rig-automated.** Setup: copied disposable FT checkpoint created by the predecessor build; existing FT `save`/`load`; pending capabilities: RIG-MODSET build selection and raw PC payload hash/refusal-status readback. Steps: record the predecessor payload hash, load with F6d, attempt a native save and inspect the PC status and payload again. Expected: “start a new city” / DevelopmentSchema, identical original payload hash, no replacement PC city written, PC food authority in its defined refused/native state.
3. **Unchanged fresh-city behavior — locked-PC; rig-automated.** Setup: matched fresh FT seeds in predecessor and F6d builds, existing `rig flow run`, FT `state`/`time`/`save`/`load`/`console`, `shop_state`/`shop_log`; pending capabilities: RIG-MODSET and an automated full state/order trace comparator. Steps: run identical fresh-city inputs for three game days, collect hourly person/journey/meal/money/order traces, save/reload daily and compare the predecessor projection. Expected: identical traces and existing money, no wages or discretionary debits from the new fields, empty journals/fingerprints and zero cause counters throughout.

These checks are pending for the combined rig batch before release. No game, rig, ModsLocal, bridge, ordinary save, RLO or upstream publication was used by this implementer.

## PR handoff

Title: `F6d: economic ledger contract, inactive (next-free writer after E6c)`.

Use this checks file as the PR body; it includes the implemented contract, no-F6b-addition statement and numbered batched-rig list above. Review against F6d only. Refs #93. No F6e/F6f/F6g activation, shared-doc updates, migration or release work is included.

Composed CF32: **214,047,318 characters / 2,279,079 records**, below the 228,170,137-character 85% target. Composed CF64: **252,447,318 characters / 3,879,079 records**, below both hard caps. No optional reduction: fourteen counter days, eight journal entries and fourteen miss bits remain. The composed fixture includes F5's 150,000 wallets, F7c, the full E6c vendor table, C5c and conservative retained B–E reserves. No exact money field was dropped.

PR performance summary (ASCII bytes = characters; three Release samples; save/cold-load timing separate from play; all bulk timing samples ≥30 ms are flagged). Full p50/p95/max and predecessor timings are in [f6d perf](../perf/f6d.md).

| Fixture | Inactive bytes added | Populated bytes added | Records added when populated | Current write p95 ms | Current read p95 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| 957x2 | 1,001,832 | 1,014,090 | 302 | 599.85 | 819.22 |
| 957x3 | 1,072,650 | 1,084,908 | 302 | 692.35 | 766.19 |
| 5000x2 | 1,600,196 | 1,612,454 | 302 | 774.65 | 1,331.57 |
| 5000x3 | 1,970,196 | 1,982,454 | 302 | 901.87 | 1,463.29 |
| 50000x2 | 8,260,196 | 8,272,454 | 302 | 5,451.13 | 6,162.82 |
| 50000x3 | 11,960,196 | 11,972,454 | 302 | 7,411.24 | 8,530.51 |
| CF32 | 11,960,196 | 12,303,782 | 9,230 | 4,324.45 | 9,254.70 |
| CF64 | 11,960,196 | 12,303,782 | 9,230 | 5,517.48 | 9,196.69 |

Inactive extensions add zero records. All three global one-over checks preserve the original payload. The 32-owner transaction microbenchmark has p95 0.0349 ms, max 0.0747 ms, zero ≥30 ms samples and zero measured allocation. Whole-frame and native mod-work timing are unmeasured.
