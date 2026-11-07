Status: implemented (offline); not verified by rig

# F6d economic ledger save budget and cost

Refs #93. Base `b2a0ab7` includes F5's person/wallet consolidation, C5c, F7c's detached snapshot/wire split and E6c's vendor table. The additive writer is `VendorSchema + 1` (31 on this base). Measurements use the real SaveCodec writer/reader, .NET 10 Release on the Mac, sequential foreground runs. These are offline save costs, separate from ordinary-play mod-work and whole-frame timing.

## Shape freeze and composed envelope

CF32 composes to **214,047,318 characters/ASCII bytes and 2,279,079 records**. The 85% target is 228,170,137 characters, leaving **14,122,819 characters**. CF64 composes to **252,447,318 characters/ASCII bytes and 3,879,079 records**, leaving **15,988,138 characters and 120,921 records** below the hard caps. No optional reductions were needed: fourteen counter days, eight changes per shop and the fourteen-bit miss mask remain. No exact money field was dropped. The 50,000-resident, 4,000,000-record and 268,435,456-character/UTF-8-byte caps are unchanged.

The maximum new wire allocations on this base are:

| Field | Wire and bound | Added records |
| --- | --- | ---: |
| Employment accrual | 26 canonical ASCII characters + field/quote/separator overhead = 39/person, below 40. Fixed cells: attended minutes 3, exact wage units 13, miss mask 3, day 4, shift 3. Every person has a zero tuple until used; an unemployed person's tuple must stay zero. | 0 |
| Owner accounts | 32 characters + tuple-slot overhead = 35/wallet, below 48. EmploymentIncome 13, Discretionary 13, visit minute 6. | 0 |
| Outside totals | Four fixed 26-digit wide decimal totals/counts plus dated row, at most 115 scalar characters; fourteen rows plus field overhead are below 16,384. Categories: retail and leisure. | 14 |
| Journal | At most 35 scalar characters + quotes/separator = 38/entry, below 40; 1,024 × 8 entries. Observed minute 6, kind 1, subject 2, exact before/after units or counts 13 each. Product subjects resolve through the canonical name table; hours subjects identify weekday, window and edge. | 8,192 |
| Fingerprint / shop header | Three base36 business-reference digits plus an empty or twenty-character Z85 128-bit fingerprint. Header/array/separator allocation ≤31/shop, below 32. | 1,024 |
| Meal-period causes | Sixteen three-character base36 counters (0–46,655), plus field overhead = 60/row, below 64; 14 × 1,024 rows in CF = 860,160 characters. | 0 |

New per-person money uses exact nonnegative signed-64-range integer units in fixed-width uppercase base36 cells. The decoder checks overflow and the original literal spelling; it accepts no alternate width, escaping, sign or lowercase form. Dated city totals use D3a-style canonical nonnegative decimal strings up to 26 digits, never floating point. These fixed tuples and counters follow B0's zero-record rule.

F5's actual CF roster has **150,000 persons and 150,000 wallets**, including 100,000 visitors, rather than the plan's illustrative 50,000 owners. The budget charges every one of them. All new values are populated by test code only; production remains inactive.

`F6dMeasure.Build` rebuilds E5d's seed 11 matrix as fresh current-schema cities: 957/5,000/50,000 residents × factor 2/3, 32 retained visits per resident (64 in CF64), a full 20,000-row / 2Mi business-reference table, four memories per person, fourteen days × 1,024 shop histories × eight product cells, weather and one fully quoted 8-shop × 8-offer cold held image. Non-CF has 32 current scopes. CF has 14,000 places, 896 shop scopes plus 128 vehicle scopes, E6c's full 64-live/64-retired/1,024-tombstone table and C5c's all-states research fixture. Cities are constructed at day 13; there is no migration or game execution. The dynamic held fixture is cold storage, as in E5d, not activation evidence.

The conservative CF composition adds to the measured current payload:

- 19,386,774 characters / 12,710 records: E5d's unspent B–E reserve, retained in full;
- 540,672 characters / 320 records: E5d's extra name-identity allocation;
- 12,582,912 characters: the entire E5d existing-state growth allowance **again**, even though the actual fixture already includes the places, scope and history structures;
- 13 × 150,000 = 1,950,000 characters: the B-memory field headroom.

Thus the measured CF payload plus **34,460,358 characters** and **13,030 records** is checked against the envelope. This deliberately keeps reserves that the merged C5c/E6c images also consume. It is a specified conservative composition, not clearance for every simultaneous Unicode, ownership-churn and journey maximum. Saves above any actual bound refuse instead of truncating.

## Size matrix

“Inactive” has zero new values and empty observations/journals. “Populated” adds test-only values for every new field, fourteen outside rows and eight journal entries per current scope. Inactive extensions add zero records in every row. Both streaming and semantic record counters are checked, and every measured payload round-trips byte-identically.

| Fixture | Predecessor characters | Inactive characters added | Populated characters | Populated characters added | Added records | Streaming / semantic |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 957x2 | 14,881,734 | 1,001,832 | 15,895,824 | 1,014,090 | 302 | 187,093 / 186,869 |
| 957x3 | 15,368,226 | 1,072,650 | 16,453,134 | 1,084,908 | 302 | 189,964 / 189,740 |
| 5000x2 | 25,543,184 | 1,600,196 | 27,155,638 | 1,612,454 | 302 | 344,770 / 344,546 |
| 5000x3 | 28,084,108 | 1,970,196 | 30,066,562 | 1,982,454 | 302 | 359,770 / 359,546 |
| 50000x2 | 137,593,657 | 8,260,196 | 145,866,111 | 8,272,454 | 302 | 2,099,770 / 2,099,546 |
| 50000x3 | 163,042,348 | 11,960,196 | 175,014,802 | 11,972,454 | 302 | 2,249,770 / 2,249,546 |
| CF32 | 167,283,178 | 11,960,196 | 179,586,960 | 12,303,782 | 9,230 | 2,266,049 / 2,259,649 |
| CF64 | 205,683,178 | 11,960,196 | 217,986,960 | 12,303,782 | 9,230 | 3,866,049 / 3,859,649 |

All matrix payloads are ASCII, so character and UTF-8 byte counts are identical. CF32 actual payload is 179,586,960 characters; CF64 actual payload is 217,986,960. Their composed counts above include the retained reserves.

## Read/write latency

Three sequential samples per operation/fixture; p50 / p95 / max in milliseconds. The predecessor is the exact frozen vendor writer/reader on the same existing state. Current timings use populated new fields; they include F7c capture and validation on writes. Payload equality checks and subsequent round-trip rewrites are outside the measured interval. With three samples, p95 is the observed maximum; these are save-cost observations, not a native performance certification.

| Fixture | Write predecessor | Write current |
| --- | ---: | ---: |
| 957x2 | 416.72 / 435.26 / 435.26 | 488.35 / 599.85 / 599.85 |
| 957x3 | 414.27 / 432.67 / 432.67 | 421.48 / 692.35 / 692.35 |
| 5000x2 | 599.56 / 618.38 / 618.38 | 620.88 / 774.65 / 774.65 |
| 5000x3 | 611.06 / 649.18 / 649.18 | 718.24 / 901.87 / 901.87 |
| 50000x2 | 3,572.47 / 3,656.32 / 3,656.32 | 3,387.47 / 5,451.13 / 5,451.13 |
| 50000x3 | 3,418.40 / 3,945.11 / 3,945.11 | 6,860.21 / 7,411.24 / 7,411.24 |
| CF32 | 4,433.47 / 7,649.66 / 7,649.66 | 3,831.69 / 4,324.45 / 4,324.45 |
| CF64 | 4,310.16 / 5,727.03 / 5,727.03 | 5,388.69 / 5,517.48 / 5,517.48 |

| Fixture | Read predecessor | Read current |
| --- | ---: | ---: |
| 957x2 | 649.57 / 766.99 / 766.99 | 723.12 / 819.22 / 819.22 |
| 957x3 | 710.18 / 774.02 / 774.02 | 704.21 / 766.19 / 766.19 |
| 5000x2 | 1,119.38 / 1,434.78 / 1,434.78 | 1,139.47 / 1,331.57 / 1,331.57 |
| 5000x3 | 1,194.40 / 1,328.35 / 1,328.35 | 1,420.04 / 1,463.29 / 1,463.29 |
| 50000x2 | 6,194.20 / 7,271.95 / 7,271.95 | 6,081.92 / 6,162.82 / 6,162.82 |
| 50000x3 | 7,555.52 / 10,567.20 / 10,567.20 | 8,144.15 / 8,530.51 / 8,530.51 |
| CF32 | 7,301.26 / 8,775.97 / 8,775.97 | 8,668.12 / 9,254.70 / 9,254.70 |
| CF64 | 8,739.40 / 9,540.30 / 9,540.30 | 7,988.60 / 9,196.69 / 9,196.69 |

Every measured bulk operation reaches 30 ms: **3/3 samples per operation, per fixture**, in both writers (96/96 measured samples overall). They are all flagged as save/cold-load batches. No whole-frame sample was collected. The four known hotspots remain parked; no performance optimization is claimed by this slice.

## Mutation work and hard limits

The existing checked-money transactions were also measured separately from saving: 100,000 owners in 3,125 units of 32 owners, each doing posting, hold/refuse and hold/confirm. Release p50 **0.0331 ms**, p95 **0.0349 ms**, max **0.0747 ms**; **0** samples ≥30 ms and **0 B** allocated in the measured units. This is an offline transaction microbenchmark, not the integrated mod-work gate. Native mod-work p95 <5 ms, flagged ≥30 ms spikes and whole-frame timing remain separate batched-rig evidence.

The populated-ledger hard-cap check uses the real current writer/reader at 4,000,000 streaming records and at exactly 268,435,456 characters/UTF-8 bytes. It then checks one record over, one character over and one UTF-8 byte over, including store/session preservation of the prior payload. Result: **4,000,000 streaming / 3,993,600 semantic records**, and **268,435,456 characters / UTF-8 bytes**, round-trip through the real writer and reader. All three one-over variants refuse in codec/store/session checks and preserve the original text/reference and unrelated key. The caps check passed in a separate foreground Release run (2 m 11 s).

## Reproduction and pins

Run one dotnet process at a time, checking `pgrep -f testhost` before starting. Use the task's PATH and DOTNET_ROOT environment. No tools/*-regression matrix is required or run.

```sh
PC_F6D_MEASURE=/tmp/f6d-measure.jsonl PC_F6D_SAMPLES=3 dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F6dMeasure
PC_F6D_CAPS=/tmp/f6d-caps.txt dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F6dHardCapTests
PC_F6D_OPERATIONS=/tmp/f6d-operations.json dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F6dMutationMeasure
```

The three-day inactive state/order trace was separately run at actual predecessor `b2a0ab7` by exporting that commit into a temporary directory, adding only `F6dInactiveTraceTests.cs` and defining `F6D_BASELINE`. Its two pinned hashes and the current writer's two populated seeded golden hashes are in [the checks file](../checks/f6d.md). E6c's writer-30 golden corpus remains unchanged.

| Populated matrix | SHA-256 |
| --- | --- |
| 957x2 | `304f6b84ee234c9933565178a5cbc34f991a729aef391d56926fff4f6ed2c23d` |
| 957x3 | `7d445316b09d819583c1987556f8871fd33e9b3d8d32d71fb9295e612afa228a` |
| 5000x2 | `be6265fd8d3df85f30b1cb960a10c77ff0709fdfa6f913605c8eeca66a397e44` |
| 5000x3 | `e13814f2dbc63bf86ed7df37a6b32899741cef8bede571bb66877c1ff1103c52` |
| 50000x2 | `257c9ad1a5b17856f887c53f9d9d78295c06cf01418bcf36eeea129353e0118a` |
| 50000x3 | `b4f72183bf1dcb3988a06c2b00ff44835f6e44a39f5da58b590a0ddc8a2d15d5` |
| CF32 | `03cbb30f384ef78afb6af6cf8d747dd6c025bf806fb4f21bfe0b81b6ed814110` |
| CF64 | `1198fc354ab2fe27f77a6fcb83d1a12c655b5e2ea8c298b6986ff5e5c00162d1` |
