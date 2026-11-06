# F5c-lite — writer-26 person shape: save cost, 6 November 2026

Refs #92. These are offline .NET 10 measurements on this Windows workstation, Release build, run in the foreground in one test process after the shared other-dotnet wait. They are synthetic and are not game, Mono or rig evidence. Save and load timing is its own latency criterion, separate from the ordinary-play mod-work gate.

## Method

The `tests/F5cLiteMeasure.cs` harness runs only when `PC_F5C_LITE_MEASURE` names an output file. Each fresh city is the F7a representative replay (`EconomyScenario` city, household finances, one natural day to 00:30 on day 3, walking routes, nine places, no traffic graph) at representation factor K. The **identical state** is written by writer 25 (the retained exact-25 path, `SaveCodec.WriteForSchema(…, 25)`) and by writer 26. Each payload is read back by its own reader: the internal development chain for 25, `SaveCodec.Read` for 26. So the differences come from the person shape alone. Both paths are warmed first, then each runs 7 measured write+read samples. Retained memory is `GC.GetTotalMemory(true)` before and after one read that is kept alive. Raw rows: [assets/f5c-lite/fresh-city-save-cost.jsonl](assets/f5c-lite/fresh-city-save-cost.jsonl) (code at `b816a2e`; the later enrolment-ordering change does not change payload bytes). The tables below are that first run; the review-fix re-run at the current code is in *Re-measured after the review fixes*, and it supersedes the record and latency figures.

Command: `PC_F5C_LITE_MEASURE=<file> PC_F5C_LITE_SAMPLES=7 dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F5cLiteMeasure`.

## Bytes, records and memory against plan section 5

| Fresh city | Persons (R + V) | Writer 25 chars | Writer 26 chars | Added | % of cap | Records 25 → 26 | Retained 25 → 26 (MiB) | Retained added per visitor |
| --- | --- | ---: | ---: | ---: | ---: | --- | --- | ---: |
| 957 × 2 | 957 + 957 | 2,333,388 | 2,373,033 | +39,645 | 0.88 | 11,312 → 12,269 | 4.15 → 4.24 | 96.9 B |
| 957 × 3 | 957 + 1,914 | 2,887,385 | 2,972,009 | +84,624 | 1.11 | 13,226 → 15,140 | 5.53 → 5.75 | 116.6 B |
| 5,000 × 2 | 5,000 + 5,000 | 12,193,195 | 12,400,121 | +206,926 | 4.62 | 59,279 → 64,279 | 21.81 → 22.27 | 95.7 B |
| 5,000 × 3 | 5,000 + 10,000 | 15,083,715 | 15,525,641 | +441,926 | 5.78 | 69,279 → 79,279 | 28.90 → 30.06 | 121.5 B |
| 50,000 × 2 | 50,000 + 50,000 | 109,762,853 | 112,094,080 | +2,331,227 | 41.76 | 561,915 → 611,915 | 191.29 → 195.98 | 98.4 B |
| 50,000 × 3 | 50,000 + 100,000 | 134,920,935 | 139,504,162 | +4,583,227 | 51.97 | 660,558 → 760,558 | 251.88 → 263.17 | 118.5 B |

The 957 × 2 writer-25 bytes (2,333,388) equal F7a's recorded `representative-957x2` writer-25 size, so this is the same fixture.

Against the section 5 budgets:

- **Resident addition ≤ 64 characters:** at most 15 (`,"P":"1RE1.0.0"`). The removed roster `AnchorId`/`Slot` slots and the factor field partly offset it.
- **Visitor ≤ 256 characters off-map or idle, with the arrival tuple:** the largest measured tuple is 67 characters including quotes and separator. A visitor that also holds an arrival token (21 characters), an employment and a carried source stays under 110. The reader's hard cap is 512, and visitor IDs are limited to 256 characters.
- **One record per person tuple:** in this first run records grew by the visitor count only. After review fix Sol-r1-2 every resident tuple is charged too, so records grow by exactly the person count (see the re-run).
- **Retained memory ≤ 1 KiB per visitor:** 95.7–121.5 B per visitor.
- **Cap:** the 150,000-person fresh city (50,000 × 3) uses 52.0 % of the 256 Mi-character cap, within the 85 % target. These are natural one-day replays, not the saturated E5d matrix that section 5's arithmetic table uses (writer 25 at 50,000 × 3: 196,758,738 characters). Per person, F5c-lite adds less than that table's ceiling: the measured tuples are at most 15 characters per resident and 67 per visitor, against the plan's ≤ 64 and ≤ 256.

## Latency (save-specific criterion, not mod-work)

Write and read p50 / p95 / max in ms, 7 samples each:

| Fresh city | Write 25 | Write 26 | Read 25 | Read 26 |
| --- | --- | --- | --- | --- |
| 957 × 2 | 40.1 / 54.8 / 54.8 | 42.9 / 57.6 / 57.6 | 104.0 / 176.5 / 176.5 | 125.5 / 170.1 / 170.1 |
| 957 × 3 | 37.2 / 69.9 / 69.9 | 42.0 / 57.2 / 57.2 | 139.2 / 157.0 / 157.0 | 119.0 / 152.4 / 152.4 |
| 5,000 × 2 | 187.6 / 273.6 / 273.6 | 213.8 / 278.4 / 278.4 | 566.6 / 641.9 / 641.9 | 617.0 / 742.1 / 742.1 |
| 5,000 × 3 | 252.0 / 354.8 / 354.8 | 269.1 / 381.4 / 381.4 | 681.2 / 778.2 / 778.2 | 699.7 / 1,039.0 / 1,039.0 |
| 50,000 × 2 | 1,835 / 2,052 / 2,052 | 1,487 / 2,271 / 2,271 | 5,118 / 5,666 / 5,666 | 4,133 / 4,505 / 4,505 |
| 50,000 × 3 | 2,122 / 2,549 / 2,549 | 1,810 / 2,197 / 2,197 | 5,549 / 6,384 / 6,384 | 4,989 / 5,101 / 5,101 |

The writer-25 and writer-26 arms differ in both directions, by up to ±20 %, within this workstation's run-to-run noise. Read allocation grows with the payload, by 6.4–10.1 %. **Flagged:** every write and read sample at every size is ≥ 30 ms in both arms, except 1 of 7 writer-25 writes at 957 × 2. This is the save/load criterion already recorded by F7a. Saves run inside the native save callback and reads run off the main thread during initialization; neither is ordinary-play frame work.

## Re-measured after the review fixes

Re-run of the same harness and command at the code after review fixes Sol-r1-1, Sol-r1-2 and Opus-r2-1: the factor-1 own-record roster, the F5b `OwnershipValidator` pass on every schema-26 read and write (`ValidateOwnership26`), and one record per resident tuple and station. Release, 7 samples after warm-up, foreground, same workstation. Raw rows: [assets/f5c-lite/fresh-city-save-cost-r2.jsonl](assets/f5c-lite/fresh-city-save-cost-r2.jsonl).

Payload characters are unchanged (the wire did not change). Records now grow by exactly the person count:

| Fresh city | Records 25 → 26 | Retained 25 → 26 (MiB) | Retained added per visitor | Read allocation 25 → 26 |
| --- | --- | --- | ---: | --- |
| 957 × 2 | 11,312 → 13,226 (+1,914) | 4.18 → 4.27 | 97.2 B | +8.9 % |
| 957 × 3 | 13,226 → 16,097 (+2,871) | 5.54 → 5.79 | 136.0 B | +12.6 % |
| 5,000 × 2 | 59,279 → 69,279 (+10,000) | 21.96 → 22.42 | 97.2 B | +9.1 % |
| 5,000 × 3 | 69,279 → 84,279 (+15,000) | 29.13 → 30.29 | 121.7 B | +12.6 % |
| 50,000 × 2 | 561,915 → 661,915 (+100,000) | 192.81 → 197.50 | 98.4 B | +9.5 % |
| 50,000 × 3 | 660,558 → 810,558 (+150,000) | 254.16 → 265.46 | 118.5 B | +13.2 % |

Write and read p50 / p95 / max in ms:

| Fresh city | Write 25 | Write 26 | Read 25 | Read 26 |
| --- | --- | --- | --- | --- |
| 957 × 2 | 53.4 / 73.2 / 73.2 | 51.6 / 74.3 / 74.3 | 135.6 / 171.7 / 171.7 | 151.1 / 198.0 / 198.0 |
| 957 × 3 | 55.3 / 80.6 / 80.6 | 70.3 / 106.9 / 106.9 | 168.0 / 196.3 / 196.3 | 165.4 / 182.2 / 182.2 |
| 5,000 × 2 | 260.9 / 399.0 / 399.0 | 303.7 / 321.3 / 321.3 | 692.4 / 791.6 / 791.6 | 758.7 / 922.9 / 922.9 |
| 5,000 × 3 | 324.8 / 393.2 / 393.2 | 425.5 / 547.6 / 547.6 | 939.7 / 1,040.8 / 1,040.8 | 1,033.1 / 1,148.3 / 1,148.3 |
| 50,000 × 2 | 1,725 / 2,105 / 2,105 | 2,369 / 2,533 / 2,533 | 4,802 / 5,123 / 5,123 | 5,695 / 6,263 / 6,263 |
| 50,000 × 3 | 2,392 / 2,943 / 2,943 | 2,905 / 4,235 / 4,235 | 6,871 / 7,486 / 7,486 | 8,005 / 8,886 / 8,886 |

Unlike the first run, writer 26 is now consistently slower at 5,000 residents and above. At p50 its writes take +16 % to +37 % longer (+644 ms at 50,000 × 2, +513 ms at 50,000 × 3), and its reads +10 % to +19 % (+893 ms and +1,135 ms). This is the added ownership pass, which builds and sorts person, roster, wallet, intent and employment facts, plus the scheduling-host derivation. At the reference size (957) it is mixed: writes −3 % and +27 %, reads +11 % and −2 %. **Flagged:** every write and read sample in both arms is ≥ 30 ms, as before. This is the save/load criterion, not ordinary-play mod-work: saves run inside the native save callback, and reads run off the main thread during initialization. The run is in the same synthetic, workstation-noise conditions as the first one; no native or rig timing exists.

## Ordinary-play cost

None added. No frame path reads the person shape. `PrepareForSave` is unchanged, and validation of the person table runs only in save, load and activation. The resident `EmploymentStartDay` assignment happens in the existing daily employment event (one integer store), and the inspector counts the household only while a popup refreshes. Mod-work p95 and spikes are therefore unchanged by construction; whole-frame timing is tracked separately, and nothing native is claimed.

## Perf win in code this slice touches

Represented visitors are created when a fresh roster is enrolled (`FoodCommerce` → `PersonTable`, during activation on the main thread). The first version kept the list ordinal with a binary-search insert per visitor, which costs an O(n) shift per visitor for the in-game RLO ID order `c0, c1, …`. It now appends and sorts once (`PersonTable.OrderVisitors`). The harness measured both strategies on that ID order, 5 samples each after warm-up, p50 / max in ms ([assets/f5c-lite/visitor-enrolment-ordering.jsonl](assets/f5c-lite/visitor-enrolment-ordering.jsonl)):

| Residents × K | Visitors | Sorted insert | Append + sort once |
| --- | ---: | --- | --- |
| 957 × 2 | 957 | 0.104 / 0.108 | 0.177 / 0.195 |
| 957 × 3 | 1,914 | 0.338 / 0.366 | 0.425 / 0.598 |
| 5,000 × 2 | 5,000 | 0.917 / 16.345 | 0.782 / 0.914 |
| 5,000 × 3 | 10,000 | 3.063 / 9.734 | 1.823 / 2.104 |
| 50,000 × 2 | 50,000 | 50.952 / 66.652 | 25.370 / 49.463 |
| 50,000 × 3 | 100,000 | 165.597 / 234.991 | 41.073 / 70.392 |

At the reference size the cost is below 0.6 ms either way. **Flagged:** at the 50,000-resident stress size, ordering still takes ≥ 30 ms once, at fresh-roster activation, which already enrols up to 150,000 roster records and wallets on that path. It is a one-time startup cost, not ordinary-play mod-work.

## Limits

These are synthetic Core replays with nine places and no traffic graph, so they understate a native city's bytes, as F7a records. No native save, Mono runtime, rig or whole-frame measurement was taken. The F7a `tools/save-cost` driver was not rerun: it writes into F7a's shared asset folder, and its stress fixtures migrate older schemas, which writer 26 refuses.
