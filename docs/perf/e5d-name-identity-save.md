# E5d: save identity capacity and cost

2 November 2026, offline only. Source base
`034bb7629391ef0fedb61d7122b0d6ee3c451ff9`, writer 24; candidate writer 25.
No game or rig was run. These bulk save/startup operations are separate from
ordinary-play mod-work p95 <5 ms. Every measured sample ≥30 ms is flagged in
the result tables; whole-frame timing/stalls are unavailable here and remain
separate native watch items.

`tools/name-identity-regression/prepare.py` exports the actual base into ignored
build output and compiles a matched runner against its original Core model.
`measure.py` runs baseline/candidate sequentially in the foreground, waiting
for other worktrees' dotnet test/run before each arm. Build-server reuse and
shared compilation are disabled. No deadlines, background test jobs or game
inputs are used. Raw logs/commands/timestamps are in
`build/e5d/measurements/`; the committed summary contains metrics, never saves.

Each ordinary matrix fixture has seed 11, 957/5,000/50,000 residents at factors
2 and 3, three outcomes per consumer, four memories/consumer, the full 20,000
business-reference table, 32 retained visits/resident, 14 × 1,024 history rows
with eight cells each, 256 referenced product names and one 8 × 3 predecessor
held image. Both counters and SHA-256 of the complete original payload must
match the candidate's frozen writer. Migration projected back to 24 must be
byte-identical. The original runtime/native state is not simulated in this
measurement, and these are saturated synthetic histories rather than typical
save sizes.

CF32/CF64 materialize one shared 12 Mi-character existing-state allowance:
14,000 places, full history row structure, 1,024 scopes and remaining name
padding, plus 32/64 visits respectively. The 20,000-place ceiling is exercised
separately by `CF64-limits`, beyond that shared allowance; no place limit changes.
Their separate cold maximum-held image has 64 tuples, keeps all 256 names/history
cells, and round-trips without
execution. Its early/full activation refusal is measured and labeled. The
composition retains all outstanding B–E reserves, charges only measured cell
bytes against the E4 cell reservation and retains its unspent bytes, then adds
the entire 540,672-character/byte and 320-record
E5d allocation conservatively without reclaiming the old reserve. CF32 must
stay within 85% of the character cap; CF64 is hard-cap compatibility only.
Churn uses 2,048 generations/day and preserves every row/cell. Separate runs
cover maximum escaped names and exact/one-over record and input caps.

Read/write/activation each use one warmup and three measured samples; the
encoding fixtures use seven. p95 at three samples is the maximum, not a
statistical native-play estimate. Migration is one measured cold 24 → 25
operation. Managed retained memory is the post-GC difference for a live decoded
graph after warm caches. Native/Mono allocation, process-wide GC pressure,
frame behavior and startup adapter work are not inferred from .NET 10 timings
on this shared development PC. No native setup or route calls occur here.

## Taken low-risk win

The new exact escaped-string check parses four hexadecimal characters from a
span instead of allocating a substring for every Unicode escape. The source
precheck and shape pass also share one token-offset map instead of rescanning
the entire input to construct it twice. This changes neither accepted identities
nor serialized bytes/counts. The matched 256-name fixture and its escaped spelling
round-trip to the same names and canonical output.

The pre-change encoding sample at `aa8b7db` allocates **8,285,000 B/read**, with
p50/p95/max **30.0958/33.0111/33.0111 ms**, four of seven reads ≥30 ms.
Final matched figures and complete matrix results follow. A trial HashSet/sort variant was discarded;
ordinal SortedSet construction remains. This is a narrow new-parser change,
not a rewrite of serialization. Physical population, closure refresh,
optional-stop refresh and general save serialization remain parked.

[Checks and numbered next-batch rig list](../checks/e5d-name-identity-save.md).

## Final matched results

All figures below are offline .NET 10, seed 11. Bytes equal characters in the ASCII matrix. Baseline latency/memory is the **actual archived writer-24 model**, not the slightly larger current Core model running the frozen reader. Frozen 24 bytes/hashes and both counters match that actual base in every arm.

| Fixture | Bytes 24 → 25 | Streaming / semantic 24 → 25 | Retained MiB 24 → 25 |
| --- | ---: | ---: | ---: |
| 957x2 | 17,745,536 → 17,751,674 | 184,787/184,563 → 185,067/184,843 | 36.262 → 37.156 |
| 957x3 | 18,419,556 → 18,425,694 | 186,701/186,477 → 186,981/186,757 | 38.110 → 39.004 |
| 5000x2 | 30,313,445 → 30,319,583 | 334,378/334,154 → 334,658/334,434 | 61.261 → 62.155 |
| 5000x3 | 33,834,959 → 33,841,097 | 344,378/344,154 → 344,658/344,434 | 70.836 → 71.738 |
| 50000x2 | 162,340,698 → 162,346,836 | 1,999,378/1,999,154 → 1,999,658/1,999,434 | 325.666 → 326.560 |
| 50000x3 | 196,752,600 → 196,758,738 | 2,099,378/2,099,154 → 2,099,658/2,099,434 | 420.545 → 421.439 |
| CF32 | 201,827,604 → 201,833,742 | 2,106,322/2,099,154 → 2,106,602/2,099,434 | 428.897 → 429.792 |
| CF64 | 240,227,604 → 240,233,742 | 3,706,322/3,699,154 → 3,706,602/3,699,434 | 502.140 → 503.034 |

The normal matrix uses 32 scopes covering all 256 names. CF materializes all **1,024 scopes × eight IDs** and charges both scope growth and exact history row structure to the same 12 Mi-character existing-state allowance by reducing only synthetic X padding. Only measured cell bytes consume the E4 cell allowance; its unspent bytes remain reserved. No second allowance is added. Each ordinary migration adds **6,138 bytes and 280 records** (256 names + 24 tuples); full maximum-held cold storage is separate. The provenance bit adds bounded per-history-cell managed memory, included above; it is not serialized.

| Fixture | Write 24 → 25, p50/p95/max ms | Read 24 → 25, p50/p95/max ms | Activation 24 → 25, p50/p95/max ms |
| --- | ---: | ---: | ---: |
| 957x2 | 222.1797/255.9252/255.9252 → 250.1175/309.6925/309.6925 | 616.8102/627.6513/627.6513 → 552.5875/615.5073/615.5073 | 164.0421/169.6221/169.6221 → 196.9675/316.2410/316.2410 |
| 957x3 | 185.8716/217.1316/217.1316 → 225.9713/313.9837/313.9837 | 669.2290/731.1524/731.1524 → 557.9467/682.7109/682.7109 | 161.7596/172.5373/172.5373 → 241.6686/256.6551/256.6551 |
| 5000x2 | 389.9180/515.3728/515.3728 → 541.0127/554.6557/554.6557 | 1095.4168/1130.8977/1130.8977 → 1119.2404/1138.4259/1138.4259 | 346.5722/398.7648/398.7648 → 405.4768/475.5121/475.5121 |
| 5000x3 | 563.6144/587.7746/587.7746 → 460.9760/623.1412/623.1412 | 1207.8192/1210.3059/1210.3059 → 1124.4768/1233.4574/1233.4574 | 441.0877/466.6641/466.6641 → 571.4008/604.4622/604.4622 |
| 50000x2 | 2011.3325/2055.0143/2055.0143 → 2031.7341/2081.7804/2081.7804 | 5327.9583/5373.8662/5373.8662 → 5341.9760/5439.7251/5439.7251 | 1732.3542/1829.1338/1829.1338 → 2016.3195/2060.1323/2060.1323 |
| 50000x3 | 2291.1105/2359.0025/2359.0025 → 2595.7578/2609.7902/2609.7902 | 6037.8738/6405.7702/6405.7702 → 7253.5346/7439.0977/7439.0977 | 1814.4419/1922.4401/1922.4401 → 2361.7715/2726.6571/2726.6571 |
| CF32 | 2360.8453/2547.0525/2547.0525 → 2469.9275/2473.2334/2473.2334 | 6845.2414/6950.5949/6950.5949 → 7069.6611/7228.8398/7228.8398 | 2049.9876/2068.9965/2068.9965 → 2617.0090/2688.1271/2688.1271 |
| CF64 | 2963.0397/3391.9815/3391.9815 → 2953.9834/3030.7152/3030.7152 | 7987.7157/8101.0623/8101.0623 → 8006.2934/8219.0233/8219.0233 | 2634.6184/2655.4814/2655.4814 → 2947.5211/3030.5938/3030.5938 |

| Fixture | Early header 24 → 25, p50/p95/max ms | Cold full migration ms |
| --- | ---: | ---: |
| 957x2 | 0.0013/0.0037/0.0037 → 0.0022/0.0062/0.0062 | 1046.5524 |
| 957x3 | 0.0019/0.0025/0.0025 → 0.0035/0.0043/0.0043 | 1116.1826 |
| 5000x2 | 0.0030/0.0044/0.0044 → 0.0035/0.0049/0.0049 | 2230.6407 |
| 5000x3 | 0.0035/0.0045/0.0045 → 0.0023/0.0035/0.0035 | 2048.6978 |
| 50000x2 | 0.0024/0.0057/0.0057 → 0.0012/0.0028/0.0028 | 7685.1841 |
| 50000x3 | 0.0014/0.0036/0.0036 → 0.0016/0.0022/0.0022 | 9786.1234 |
| CF32 | 0.0014/0.0028/0.0028 → 0.0017/0.0048/0.0048 | 9965.7886 |
| CF64 | 0.0012/0.0033/0.0033 → 0.0016/0.0019/0.0019 | 10935.3090 |

Activation means full validation plus counting serialization, not native adapter startup. Early-header timings measure the writer's header-first order; arbitrary older property orders remain readable but need not have constant header latency. Native activation/setup/route cost is unmeasured.

| Full CF cold maximum | Actual bytes / chars | Streaming / semantic | Retained MiB | Budgeted bytes / chars | Budgeted records |
| --- | ---: | ---: | ---: | ---: | ---: |
| CF32, 64 offers | 201,838,710 | 2,106,682/2,099,514 | 429.798 | 223,716,156 | 2,119,712 |
| CF64, 64 offers | 240,238,710 | 3,706,682/3,699,514 | 503.040 | 262,116,156 | 3,719,712 |

The complete CF composition includes every outstanding reserve, including the entire extra 540,672 bytes/chars and 320 records on top of the actual image. CF has **14,000 places**, the full 20,000 business references, full histories and 1,024 scopes within one shared 12 Mi existing-state allowance. The separate `CF64-limits` stress exercises all 20,000 places beyond that allowance, without future-reserve clearance; the place limit remains unchanged. CF32 passes the 85% target; CF64 passes hard caps only. These are reproducible specified compositions, not clearance for every simultaneous Unicode, opaque-history and churn maximum. Each cold image round-trips; its early/full execution attempts **refuse**. Cold p50/p95/max, allocations and refusal latencies are in the linked JSON.

- CF32: existing growth 4,429,907 place + 319,713 scope + 7,570,003 history row structure + 263,289 name = **12,582,912** chars; remaining B–E reserve **19,386,774 chars / 12,710 records**; memory-field headroom **1,950,000** chars.
- CF64: existing growth 4,429,907 place + 319,713 scope + 7,570,003 history row structure + 263,289 name = **12,582,912** chars; remaining B–E reserve **19,386,774 chars / 12,710 records**; memory-field headroom **1,950,000** chars.

Final encoding win: **8,285,000 → 2,065,995 B/read**, saving **6,219,005 B (75.1%)**. Read p50/p95/max **30.0958/33.0111/33.0111 → 28.2538/31.0604/31.0604 ms**. These shared-PC sequential samples establish an allocation win, not a causal CPU/native-gate claim. Before/final ≥30 ms reads: **4/7 → 1/7**. Final write allocation is **1,058,409 B/call**.

Matched allocation fixture: **256 × 256 UTF-16 units** (253 non-ASCII units and a three-digit suffix per name), canonical save **149,224 chars / 408,296 UTF-8 B**; escaped spelling **796,904 chars/B**, table including field overhead **390,161 chars/B**. Counts **486/262** retain the legacy scope-string gap. Both spellings round-trip to identical names/output.

Separate true maximum-escaping fixture: **256 unique names, each 256 non-ASCII UTF-16 units**, all six-character JSON escapes; canonical save **149,224 chars / 411,368 UTF-8 B**, escaped save **804,584 chars/B**, escaped table including field overhead **394,001 chars/B**. Both representations pass and round-trip identically. This is a distinct bound fixture, not the matched pre-win allocation sample. Its complete timings, allocations and spike counts are in the JSON.

Churn: **28,672 rows / 229,376 cells**, 2,048 generations/day; **30,120,102 → 30,126,240 B**, counters **314,091/313,867**. All retained generations survive; no hidden 1,024/day cap.

| Separate 20,000-place stress | Bytes 24 → 25 | Streaming / semantic 24 → 25 | Retained MiB 24 → 25 |
| --- | ---: | ---: | ---: |
| 50,000 x3, 64 visits, 32 scopes | 247,807,024 → 247,813,162 | 3,699,378/3,699,154 → 3,699,658/3,699,434 | 515.924 → 516.818 |

| Operation | 24 → 25, p50/p95/max ms |
| --- | ---: |
| write | 2841.7196/2971.2807/2971.2807 → 3121.8581/3134.0462/3134.0462 |
| read | 8414.0636/8700.3574/8700.3574 → 7520.0468/7673.9932/7673.9932 |
| activation | 3133.9444/3143.6917/3143.6917 → 2452.1119/2457.8137/2457.8137 |
| early | 0.0045/0.0136/0.0136 → 0.0016/0.0035/0.0035 |

Cold migration: **11476.9909 ms**. All bulk write/read/activation samples in both arms reach 30 ms (3/3 each); early headers do not. This current-contract stress is outside the shared CF existing-state allowance and grants no future-reserve clearance. Full source bytes/hashes match the actual archived base; the migration projects back identically.

- `record-boundary`: `{"streamingAtCap":4000000,"semanticAtCap":3999997,"oneRecordOver":"read/write/store/session refused and source reference preserved","chars":242816040,"utf8Bytes":242816040}`
- `input-boundary`: `{"exactCharactersAndUtf8":268435456,"oneCharacterOver":"read/store/session preserved","oneUtf8ByteOver":"read/store/session preserved","oversizedSources":[13,14,15,16,17]}`
- `migration-record-limit`: `{"chars":242816266,"bytes":242816266,"Streaming":4000000,"Semantic":3999997,"requiredNewRecords":27,"migration":"refused; originally valid 24 source text/reference preserved by codec/store/session","Error":"Invalid state: Too many opening, route and visit records."}`
- `20,000-place independent caps`: `{"utf8":{"dimension":"UTF-8","characters":257644370,"bytes":278124370,"result":"read/write/activation/store/session refused; original text/reference and unrelated key preserved"},"characters":{"dimension":"characters","characters":275959722,"bytes":275959722,"result":"read/write/activation/store/session refused; original text/reference and unrelated key preserved"}}`

## Spike flags and qualification

Every matrix bulk write/read/full-activation sample reaches 30 ms in both arms (3/3 per operation); every single cold migration and all cold maximum-held bulk operations are likewise flagged. These are save/startup batches, never ordinary-play mod-work p95. Early-header samples remain below 30 ms. The complete operation-by-operation spike counts, allocation totals, cold refusal timings, timestamps and peaks are in the metrics JSON. Managed GC collection counts were not captured; native/Mono GC and frame costs remain unmeasured.

The two complete regression passes additionally reported these offline distributions with samples at least 30 ms. Whole-index builds and bulk saves are aggregate costs; none is a whole-frame measurement.

| Run / component | Metric | p50/p95/max ms | Samples at least 30 ms |
| --- | --- | ---: | ---: |
| D3a/awareness | `.stages[1].buildMs` | 25.3176/31.8366/31.8366 | 1 |
| D3a/e4-menu | `.results.history[0].serialize` | 155.7864/215.3876/232.9920 | 21 |
| D3a/e4-menu | `.results.history[1].serialize` | 160.2724/215.7853/247.1024 | 21 |
| D3a/name-identity | `.before.timing.write` | 197.8413/205.1961/205.1961 | 3 |
| D3a/name-identity | `.before.timing.read` | 566.2416/572.5082/572.5082 | 3 |
| D3a/name-identity | `.before.timing.activation` | 191.5293/192.1918/192.1918 | 3 |
| D3a/name-identity | `.after.timing.write` | 239.8603/243.8094/243.8094 | 3 |
| D3a/name-identity | `.after.timing.read` | 594.3885/627.4566/627.4566 | 3 |
| D3a/name-identity | `.after.timing.activation` | 322.7992/347.7787/347.7787 | 3 |
| D4/awareness | `.stages[1].buildMs` | 36.6525/82.6261/82.6261 | 7 |
| D4/e4-menu | `.results.history[0].serialize` | 193.2846/225.3048/231.0554 | 21 |
| D4/e4-menu | `.results.history[1].serialize` | 203.1269/247.5740/265.8245 | 21 |
| D4/name-identity | `.before.timing.write` | 213.1308/216.5930/216.5930 | 3 |
| D4/name-identity | `.before.timing.read` | 640.8006/663.8664/663.8664 | 3 |
| D4/name-identity | `.before.timing.activation` | 200.2526/241.3686/241.3686 | 3 |
| D4/name-identity | `.after.timing.write` | 329.3926/331.1639/331.1639 | 3 |
| D4/name-identity | `.after.timing.read` | 550.6268/649.9048/649.9048 | 3 |
| D4/name-identity | `.after.timing.activation` | 246.1581/288.8797/288.8797 | 3 |
| D4/rival-outflow | `.wholeDay[1].advance128UnitSamples` | 0.7964/1.8934/33.3999 | 1 |
[Metrics-only results, including every p50/p95/max and spike count](e5d-name-identity-save-results.json). No ordinary-play or whole-frame sample was collected. The next combined rig batch must measure mod-work p95 <5 ms and every ≥30 ms spike separately from whole-frame cost/stalls; the four parked hotspots remain parked.
