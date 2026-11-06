# City audit F7p: compact-format prototype matrix (6 November 2026, offline)

Refs #94. Base `e9995e5` (writer **25**). Plan: [F7 section 3.6 and section 4, row F7p](../plans/f7-save-snapshots.md#36-prototype-plan-f7p); [decision record](../decisions/2026-11-06-f7d-compact-format.md); [checks and rig list](../checks/f7p.md). The slice is tools only (`tools/compact-format/`). It changes nothing under `Core/`, `Persistence/` or `Runtime/`, and writer 25 is unchanged. The numbers below are a format comparison. They are not a before/after pair for shipped code, and they are not ordinary-play mod-work.

## Method

Run `python tools/compact-format/measure.py 7` (or `... 7 <fixture ...>`), then `python tools/compact-format/report.py` for the tables. Every (fixture, candidate) pair runs in its own foreground `CompactFormat` process after the shared other-dotnet wait, so peak working set belongs to that pair. Windows 11, .NET SDK 10.0.402, Release, workstation GC. Raw rows: [matrix.jsonl](assets/f7p/matrix.jsonl) (52 rows), [metadata.json](assets/f7p/metadata.json). Metrics only; no payload is committed. `metadata.json` records `7b15760` as the head because the run used the working tree that was committed next as `889ebc7`. That commit adds only the `file:` fixture input, which the matrix does not use.

- **Fixtures.** F7a's builders are compiled in place: the three representative F3-replay fixtures and the ten E5d stress cases. Each process rebuilds its fixture and writes it with `SaveCodec.Write`. The driver refuses any row whose writer-25 SHA-256 differs from F7a's retained `baseline.jsonl`, and all 52 rows matched. The strict writer-25 reader accepts each source and rewrites it to identical bytes. The F5 declared population is not available yet, because F5 has not declared it.
- **Snapshot stand-in.** F7c's snapshot does not exist yet. The prototype therefore parses the writer-25 payload once into a detached exact tree: number tokens keep their exact text, strings are unescaped, and key order is kept. Every candidate encodes from that tree and decodes back to it. Candidate A writes the tree with Newtonsoft's default escaping, which is byte-identical to `SaveCodec.Write` (unit test over every UTF-16 code unit). **Capture, validation and materialization into `CityState` are not in these timings.** F7a measured today's synchronize/validate/activation, and F7c will measure capture.
- **Candidates** (section 3.6). **A**: writer-25 JSON as written today. **B**: columnar compact JSON with per-field columns, recursive flattening of nested lists, positional slot columns for writer-25 tuples, and ordinal tables for strings seen at least twice, field names and field sequences. Number tokens are copied as exact text, or packed as zigzag base-64 varints when the token is exactly the invariant spelling of a long (or of `N.0`). **C**: B plus a per-save route table that deduplicates `Journey`/vehicle `Route` values by exact content, plus road-tag `ServiceId`s split into a topology head and a chained node range. **D**: C, then deflate (Optimal) of its UTF-8, then base64.
- **Per sample.** One discarded warm-up, then seven encode and seven decode calls. Allocation is `GC.GetAllocatedBytesForCurrentThread` per call. Retained is the post-GC difference for one live decoded tree. p95 over seven samples is the second-largest-or-max order statistic. `gzip(UTF-16)` is GZip Optimal of the string's UTF-16LE bytes. It estimates how the native compressor sees the string (Odin stores `modData` as UTF-16) and is not a native measurement.
- **Round-trip** (every row, after timing): field-by-field equality of the decoded tree with the source tree (kinds, key order, exact number text, exact strings, element order), rewrite of the decoded tree to the exact writer-25 bytes, and decode → re-encode byte identity. A failure stops the run. No row failed.

## Results

### Size

| Fixture | A chars | B chars (÷A) | C chars (÷A) | D chars (÷A) | UTF-8 = chars | gzip(UTF-16) A / B / C / D bytes |
| --- | ---: | ---: | ---: | ---: | --- | ---: |
| representative-957x2 | 2,333,388 | 693,989 (0.2974) | 694,001 (0.2974) | 210,749 (0.0903) | yes | 353,129 / 186,984 / 186,988 / 184,634 |
| representative-5000x2 | 12,193,195 | 3,305,114 (0.2711) | 3,305,126 (0.2711) | 969,597 (0.0795) | yes | 1,826,301 / 838,127 / 838,137 / 791,842 |
| representative-50000x2 | 109,762,853 | 30,090,216 (0.2741) | 30,090,228 (0.2741) | 8,743,957 (0.0797) | yes | 17,511,691 / 7,724,139 / 7,724,152 / 7,509,857 |
| 957x2 | 17,751,674 | 4,743,832 (0.2672) | 4,743,844 (0.2672) | 261,837 (0.0147) | yes | 613,909 / 238,200 / 238,193 / 127,567 |
| 957x3 | 18,425,694 | 4,951,607 (0.2687) | 4,951,619 (0.2687) | 311,925 (0.0169) | yes | 705,485 / 283,605 / 283,618 / 159,517 |
| 5000x2 | 30,319,583 | 7,657,556 (0.2526) | 7,657,568 (0.2526) | 784,965 (0.0259) | yes | 1,651,107 / 697,566 / 697,564 / 456,654 |
| 5000x3 | 33,841,097 | 8,557,285 (0.2529) | 8,557,297 (0.2529) | 981,741 (0.0290) | yes | 2,129,732 / 883,202 / 883,194 / 618,228 |
| churn | 30,126,240 | 6,945,432 (0.2305) | 6,945,444 (0.2305) | 282,577 (0.0094) | yes | 875,026 / 285,321 / 285,337 / 131,795 |
| 50000x2 | 162,346,836 | 33,972,874 (0.2093) | 33,972,886 (0.2093) | 5,578,765 (0.0344) | yes | 12,889,839 / 5,069,223 / 5,069,236 / 4,209,798 |
| 50000x3 | 196,758,738 | 42,834,195 (0.2177) | 42,834,207 (0.2177) | 7,472,485 (0.0380) | yes | 17,589,514 / 6,902,137 / 6,901,873 / 5,777,531 |
| CF32 | 201,833,742 | 44,251,409 (0.2192) | 44,251,421 (0.2192) | 7,682,857 (0.0381) | yes | 17,806,745 / 7,062,261 / 7,062,190 / 5,834,933 |
| CF64 | 240,233,742 | 52,252,765 (0.2175) | 52,252,777 (0.2175) | 7,782,229 (0.0324) | yes | 19,863,757 / 7,270,843 / 7,271,232 / 5,918,501 |
| encoding-max | 149,224 | 73,565 (0.4930) | 73,577 (0.4931) | 6,537 (0.0438) | no: A 411,368 / C 204,649 | 5,622 / 4,895 / 4,900 / 4,803 |

### Records, tables and round-trip

| Fixture | Semantic records (A) | C table entries (strings / keys / shapes / routes) | C records vs 4,000,000 cap | Fields compared | Differences | Writer-25 bytes after decode | Decode -> re-encode identical |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| representative-957x2 | 11,309 | 4,492 (4,355 / 123 / 14 / 0) | 15,801 | 175,277 × 4 | 0 | identical, all 4 | yes, all 4 |
| representative-5000x2 | 59,276 | 14,582 (14,445 / 123 / 14 / 0) | 73,858 | 915,006 × 4 | 0 | identical, all 4 | yes, all 4 |
| representative-50000x2 | 561,912 | 62,585 (62,448 / 123 / 14 / 0) | 624,497 | 8,773,462 × 4 | 0 | identical, all 4 | yes, all 4 |
| 957x2 | 184,843 | 4,483 (4,295 / 166 / 22 / 0) | 189,326 | 1,752,374 × 4 | 0 | identical, all 4 | yes, all 4 |
| 957x3 | 186,757 | 4,486 (4,298 / 166 / 22 / 0) | 191,243 | 1,811,708 × 4 | 0 | identical, all 4 | yes, all 4 |
| 5000x2 | 334,434 | 17,625 (17,437 / 166 / 22 / 0) | 352,059 | 2,573,103 × 4 | 0 | identical, all 4 | yes, all 4 |
| 5000x3 | 344,434 | 17,694 (17,506 / 166 / 22 / 0) | 362,128 | 2,883,103 × 4 | 0 | identical, all 4 | yes, all 4 |
| churn | 313,867 | 5,507 (5,319 / 166 / 22 / 0) | 319,374 | 3,214,646 × 4 | 0 | identical, all 4 | yes, all 4 |
| 50000x2 | 1,999,434 | 63,202 (63,014 / 166 / 22 / 0) | 2,062,636 | 11,365,103 × 4 | 0 | identical, all 4 | yes, all 4 |
| 50000x3 | 2,099,434 | 67,793 (67,605 / 166 / 22 / 0) | 2,167,227 | 14,465,103 × 4 | 0 | identical, all 4 | yes, all 4 |
| CF32 | 2,099,434 | 76,458 (76,270 / 166 / 22 / 0) | 2,175,892 | 14,657,595 × 4 | 0 | identical, all 4 | yes, all 4 |
| CF64 | 3,699,434 | 76,506 (76,318 / 166 / 22 / 0) | 3,775,940 | 16,257,595 × 4 | 0 | identical, all 4 | yes, all 4 |
| encoding-max | 262 | 412 (300 / 101 / 11 / 0) | 674 | 1,369 × 4 | 0 | identical, all 4 | yes, all 4 |

### Encode (ms p50 / p95 / max (samples >= 30 ms of 7); allocated bytes median)

| Fixture | A | B | C | D | C table pass / column writer p50 | Allocated A / B / C / D | C alloc ÷ C UTF-16 bytes |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| representative-957x2 | 15.9 / 20.6 / 20.6 (0) | 25.0 / 40.4 / 40.4 (3) | 24.7 / 35.5 / 35.5 (2) | 37.7 / 60.7 / 60.7 (6) | 10.0 / 13.4 | 9,362,392 / 10,780,024 / 10,780,080 / 12,491,000 | 7.77 |
| representative-5000x2 | 86.5 / 113.0 / 113.0 (5) | 120.2 / 165.2 / 165.2 (7) | 137.7 / 170.0 / 170.0 (7) | 147.5 / 170.2 / 170.2 (7) | 59.6 / 75.5 | 48,882,712 / 61,995,416 / 61,995,504 / 70,005,720 | 9.38 |
| representative-50000x2 | 358.7 / 402.0 / 402.0 (7) | 1,683.9 / 2,005.5 / 2,005.5 (7) | 1,355.3 / 1,473.1 / 1,473.1 (7) | 1,630.5 / 1,983.5 / 1,983.5 (7) | 428.6 / 896.7 | 440,060,360 / 541,039,272 / 541,037,512 / 613,634,544 | 8.99 |
| 957x2 | 40.9 / 90.6 / 90.6 (7) | 168.7 / 205.3 / 205.3 (7) | 180.8 / 281.0 / 281.0 (7) | 175.5 / 270.2 / 270.2 (7) | 60.8 / 112.2 | 71,169,712 / 91,009,416 / 91,010,448 / 97,988,024 | 9.59 |
| 957x3 | 48.4 / 93.0 / 93.0 (7) | 168.1 / 237.1 / 237.1 (7) | 157.7 / 205.3 / 205.3 (7) | 175.6 / 275.8 / 275.8 (7) | 49.3 / 103.5 | 73,883,872 / 94,807,016 / 94,807,984 / 102,249,496 | 9.57 |
| 5000x2 | 79.1 / 131.9 / 131.9 (7) | 241.4 / 325.1 / 325.1 (7) | 256.6 / 318.2 / 318.2 (7) | 289.2 / 383.6 / 383.6 (7) | 75.7 / 177.6 | 121,554,640 / 147,714,192 / 147,711,384 / 160,423,688 | 9.64 |
| 5000x3 | 72.0 / 147.3 / 147.3 (7) | 386.8 / 452.7 / 452.7 (7) | 376.2 / 443.2 / 443.2 (7) | 389.4 / 435.3 / 435.3 (7) | 111.7 / 260.2 | 135,669,344 / 156,016,824 / 156,017,440 / 170,641,448 | 9.12 |
| churn | 83.2 / 205.9 / 205.9 (7) | 395.6 / 506.6 / 506.6 (7) | 427.5 / 460.2 / 460.2 (7) | 422.1 / 446.6 / 446.6 (7) | 121.7 / 303.5 | 120,782,224 / 159,757,664 / 159,757,992 / 169,570,528 | 11.50 |
| 50000x2 | 823.4 / 967.6 / 967.6 (7) | 1,572.6 / 1,947.6 / 1,947.6 (7) | 1,373.6 / 1,537.5 / 1,537.5 (7) | 1,535.6 / 1,699.1 / 1,699.1 (7) | 434.3 / 895.1 | 650,869,584 / 629,820,936 / 629,822,320 / 694,603,904 | 9.27 |
| 50000x3 | 616.4 / 704.1 / 704.1 (7) | 2,300.5 / 2,482.7 / 2,482.7 (7) | 1,885.6 / 2,097.8 / 2,097.8 (7) | 2,146.0 / 2,251.4 / 2,251.4 (7) | 494.4 / 1,275.3 | 788,819,064 / 844,576,440 / 844,606,936 / 928,046,208 | 9.86 |
| CF32 | 808.7 / 1,033.1 / 1,033.1 (7) | 1,940.6 / 2,083.6 / 2,083.6 (7) | 2,373.8 / 2,739.5 / 2,739.5 (7) | 2,244.1 / 2,373.7 / 2,373.7 (7) | 662.9 / 1,710.7 | 809,174,792 / 881,618,816 / 881,651,704 / 967,705,776 | 9.96 |
| CF64 | 732.9 / 828.3 / 828.3 (7) | 2,286.4 / 2,602.3 / 2,602.3 (7) | 2,470.3 / 3,062.1 / 3,062.1 (7) | 3,136.1 / 3,604.2 / 3,604.2 (7) | 713.2 / 1,615.0 | 963,120,392 / 947,311,640 / 947,310,248 / 1,043,755,072 | 9.06 |
| encoding-max | 1.2 / 4.7 / 4.7 (0) | 1.2 / 5.0 / 5.0 (0) | 1.1 / 6.2 / 6.2 (0) | 1.4 / 5.0 / 5.0 (0) | 0.3 / 0.7 | 606,408 / 496,808 / 496,864 / 779,248 | 3.38 |

### Decode and memory

| Fixture | Decode A | Decode B | Decode C | Decode D | Decode allocated A / C | Retained MiB A / B / C / D | Distinct string instances A / C | Peak working set MiB A / B / C / D |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| representative-957x2 | 41.7 / 70.9 / 70.9 (7) | 40.3 / 50.4 / 50.4 (5) | 38.8 / 43.4 / 43.4 (4) | 38.0 / 52.3 / 52.3 (6) | 21,000,520 / 16,205,992 | 10.8 / 7.1 / 7.1 / 7.1 | 37,088 / 8,230 | 168 / 156 / 156 / 146 |
| representative-5000x2 | 211.0 / 229.0 / 229.0 (7) | 151.0 / 222.5 / 222.5 (7) | 151.7 / 249.0 / 249.0 (7) | 117.3 / 204.8 / 204.8 (7) | 109,758,736 / 86,050,736 | 56.7 / 36.2 / 36.2 / 36.2 | 194,011 / 29,831 | 542 / 490 / 495 / 550 |
| representative-50000x2 | 1,943.1 / 2,218.6 / 2,218.6 (7) | 1,418.3 / 1,993.9 / 1,993.9 (7) | 1,564.5 / 1,917.0 / 1,917.0 (7) | 1,620.6 / 1,868.8 / 1,868.8 (7) | 1,023,755,200 / 793,456,864 | 530.5 / 339.8 / 339.8 / 339.8 | 1,747,068 / 202,212 | 4,289 / 4,068 / 4,062 / 4,547 |
| 957x2 | 424.5 / 504.9 / 504.9 (7) | 187.2 / 279.2 / 279.2 (7) | 263.8 / 313.8 / 313.8 (7) | 231.1 / 288.2 / 288.2 (7) | 212,258,000 / 128,153,632 | 115.6 / 70.9 / 70.9 / 70.9 | 173,686 / 29,141 | 898 / 851 / 769 / 835 |
| 957x3 | 325.9 / 498.5 / 498.5 (7) | 175.4 / 287.6 / 287.6 (7) | 176.1 / 266.8 / 266.8 (7) | 255.7 / 309.0 / 309.0 (7) | 218,369,648 / 133,178,904 | 119.5 / 73.3 / 73.3 / 73.3 | 182,299 / 32,009 | 891 / 799 / 902 / 845 |
| 5000x2 | 531.9 / 711.2 / 711.2 (7) | 373.3 / 421.7 / 421.7 (7) | 365.9 / 453.8 / 453.8 (7) | 365.1 / 433.4 / 433.4 (7) | 306,963,624 / 208,908,728 | 168.0 / 98.3 / 98.3 / 98.3 | 419,804 / 52,384 | 1,188 / 1,241 / 1,212 / 1,215 |
| 5000x3 | 655.0 / 756.2 / 756.2 (7) | 390.4 / 581.9 / 581.9 (7) | 389.7 / 604.2 / 604.2 (7) | 386.0 / 612.6 / 612.6 (7) | 338,723,624 / 233,116,992 | 187.9 / 110.3 / 110.3 / 110.3 | 464,804 / 62,312 | 1,344 / 1,454 / 1,464 / 1,402 |
| churn | 909.0 / 1,054.6 / 1,054.6 (7) | 385.1 / 582.6 / 582.6 (7) | 391.2 / 623.0 / 623.0 (7) | 534.6 / 601.2 / 601.2 (7) | 388,826,936 / 227,002,520 | 210.2 / 127.7 / 127.7 / 127.7 | 245,366 / 30,165 | 1,409 / 1,384 / 1,526 / 1,340 |
| 50000x2 | 2,194.0 / 2,940.9 / 2,940.9 (7) | 1,850.8 / 2,373.5 / 2,373.5 (7) | 1,822.2 / 2,100.5 / 2,100.5 (7) | 1,624.8 / 2,635.7 / 2,635.7 (7) | 1,299,822,608 / 1,016,614,472 | 723.4 / 383.9 / 383.9 / 383.9 | 3,013,424 / 222,857 | 5,716 / 5,598 / 5,604 / 5,849 |
| 50000x3 | 2,865.4 / 4,005.5 / 4,005.5 (7) | 2,596.7 / 2,789.0 / 2,789.0 (7) | 2,336.5 / 2,717.8 / 2,717.8 (7) | 2,247.8 / 2,800.2 / 2,800.2 (7) | 1,617,558,936 / 1,289,769,056 | 920.6 / 502.7 / 502.7 / 502.7 | 3,463,424 / 316,738 | 7,444 / 6,501 / 6,591 / 6,689 |
| CF32 | 3,025.3 / 3,382.8 / 3,382.8 (7) | 2,510.2 / 3,288.1 / 3,288.1 (7) | 2,597.6 / 2,922.4 / 2,922.4 (7) | 2,522.0 / 2,952.0 / 2,952.0 (7) | 1,656,443,344 / 1,310,827,080 | 937.5 / 515.0 / 515.0 / 515.0 | 3,526,245 / 361,569 | 7,103 / 6,485 / 7,069 / 6,949 |
| CF64 | 3,253.2 / 4,129.8 / 4,129.8 (7) | 3,109.1 / 4,788.4 / 4,788.4 (7) | 2,974.4 / 3,276.7 / 3,276.7 (7) | 3,292.6 / 3,699.6 / 3,699.6 (7) | 1,798,443,344 / 1,523,585,368 | 1,047.4 / 527.2 / 527.2 / 527.3 | 5,126,245 / 361,617 | 8,718 / 8,236 / 8,292 / 7,679 |
| encoding-max | 0.7 / 0.7 / 0.7 (0) | 0.5 / 0.7 / 0.7 (0) | 0.5 / 0.6 / 0.6 (0) | 1.0 / 1.3 / 1.3 (0) | 462,168 / 389,584 | 0.3 / 0.2 / 0.2 / 0.2 | 733 / 354 | 53 / 53 / 53 / 54 |

### Characters per field family, representative-957x2 (A vs C; families >= 0.5 % of A)

| Path | A | C | C ÷ A |
| --- | ---: | ---: | ---: |
| `$` | 2,333,388 | 583,845 | 0.250 |
| `$.Citizens` | 993,853 | 144,384 | 0.145 |
| `$.Citizens[]` | 992,895 | 144,376 | 0.145 |
| `$.Citizens[].CommutePreparation` | 83,259 | <0.5 % |  |
| `$.Citizens[].Identity` | 111,751 | 18,257 | 0.163 |
| `$.Citizens[].Preferences` | 92,076 | 15,728 | 0.171 |
| `$.Citizens[].RecentVisits` | 159,837 | 48,135 | 0.301 |
| `$.Citizens[].Traits` | 32,538 | 13,417 | 0.412 |
| `$.Citizens[].W` | 14,930 | <0.5 % |  |
| `$.Food` | 1,223,856 | 420,488 | 0.344 |
| `$.Food.Residents` | 1,114,829 | 392,442 | 0.352 |
| `$.Food.Residents[]` | 1,112,914 | 392,433 | 0.353 |
| `$.Food.RoutineStops` | 107,686 | 27,510 | 0.255 |
| `$.Food.RoutineStops[]` | 106,728 | 27,502 | 0.258 |
| `$.SourceIdentities` | 112,709 | 18,265 | 0.162 |
| `$.SourceIdentities[]` | 111,751 | 18,257 | 0.163 |
| `$.SourceIdentities[].Id` | 16,269 | <0.5 % |  |
| `$.SourceIdentities[].Name` | 12,331 | <0.5 % |  |

### Characters per field family, 50000x2 (A vs C; families >= 0.5 % of A)

| Path | A | C | C ÷ A |
| --- | ---: | ---: | ---: |
| `$` | 162,346,836 | 33,037,908 | 0.204 |
| `$.Citizens` | 81,801,775 | 13,314,351 | 0.163 |
| `$.Citizens[]` | 81,751,774 | 13,314,341 | 0.163 |
| `$.Citizens[].CommutePreparation` | 4,350,000 | <0.5 % |  |
| `$.Citizens[].Identity` | 5,484,955 | 1,166,485 | 0.213 |
| `$.Citizens[].Preferences` | 4,796,849 | <0.5 % |  |
| `$.Citizens[].RecentVisits` | 38,450,000 | 8,000,010 | 0.208 |
| `$.Citizens[].Traits` | 1,700,000 | <0.5 % |  |
| `$.Citizens[].W` | 850,000 | <0.5 % |  |
| `$.Food` | 80,132,128 | 19,668,876 | 0.245 |
| `$.Food.BusinessReferences` | 2,097,152 | 1,908,305 | 0.910 |
| `$.Food.BusinessReferences[]` | 2,077,151 | 1,908,295 | 0.919 |
| `$.Food.History` | 12,359,027 | 2,251,188 | 0.182 |
| `$.Food.History[]` | 12,344,690 | 2,251,178 | 0.182 |
| `$.Food.Residents` | 65,666,551 | 15,506,918 | 0.236 |
| `$.Food.Residents[]` | 65,566,550 | 15,506,907 | 0.237 |

## Against the section 3.1 targets (candidate C; all synthetic)

| Target | Measured here | Result |
| --- | --- | --- |
| Typical bytes ≤ 35 % of writer-25 representative 957×2 (≤ 816,686 chars), absolute ≤ 6 Mi | 694,001 chars (29.74 %); 5,000×2 27.11 %; 50,000×2 27.41 % | **met** |
| Stress bytes ≤ 40 % of 162,346,836 (≤ 64 Mi chars) at 50000×2 | 33,972,886 chars (20.93 %); CF64 52,252,777 | **met** |
| Records under the 4,000,000 cap (table entries charged one each) | max 3,775,940 (CF64); 50000×2 2,062,636 | **met** (CF64 is a compatibility composition, not a budget) |
| Typical synchronous pause ≤ 40 ms p95 (capture + validate + encode + publish) | encode alone p50/p95 24.7/35.5 ms; adding F7a's writer-25 synchronize and validate p95 (6.9 + 13.9 ms) gives about 56 ms | **not shown**: the prototype's encode p95 alone uses 89 % of the budget |
| Stress pause ≤ 600 ms | encode alone p50/p95 1,373.6/1,537.5 ms | **not met by the prototype encoder** (2.3× over at p50) |
| Allocation ≤ 2.5 × payload UTF-16 bytes | 7.8–11.5 × (3.4 × at encoding-max). In absolute terms 629.8 MB at 50000×2, against F7a's writer-25 save allocation of 1,567.7 MB | **not met** |
| Retained ≤ writer-25 retained; intern-on-decode ≤ 80 % | decoded tree, C vs A: 0.66 (representative 957×2) to 0.50 (CF64); distinct string instances 8,230 vs 37,088 at representative 957×2 | **met on the tree basis**; runtime `CityState` retained is F7d's measurement |
| Load ≤ 400 ms typical, ≤ 2,500 ms stress (read + activation) | decode only: 38.8/43.4 ms at representative 957×2, 1,822/2,100 ms at 50000×2 | typical decode fits; stress decode alone uses 84 % of the budget before activation (F7a writer-25 activation validate p50 1,728.65 ms). **Not shown** |

The timing and allocation rows describe this text-tree prototype. Its encoder re-reads every number token as text, builds one list per column at every nesting level, and hashes every string occurrence. The C encode split shows the column writer at 54–72 % of encode time, for example 895.1 of 1,373.6 ms at 50000×2. Even plain A, a near-copy of the tree, takes 823 ms at 50000×2. A typed snapshot would hold numbers as `long`/`double` and IDs already interned at capture, but this record makes no claim about how fast such a writer would be. The bytes are a property of the format and carry over to F7d. The timings are not.

## Flags

Every encode or decode sample ≥ 30 ms is flagged: **647 of 728** samples (52 rows × 7 encode + 7 decode). At representative 957×2, encode ≥ 30 ms occurred in 0/7 (A), 3/7 (B), 2/7 (C) and 6/7 (D) samples. Decode ≥ 30 ms occurred in 7/7 (A), 5/7 (B), 4/7 (C) and 6/7 (D) samples. `encoding-max` has none. These are save/load latency samples of an offline tool, a separate criterion from ordinary-play mod-work. F7p adds no runtime code, so it adds no mod-work and no per-frame work. Mod-work p95 < 5 ms and whole-frame metrics are unaffected by this slice and are tracked separately.

## Limits

- **Fixture coverage.** None of the 13 fixtures carries a traffic definition, a committed `Journey` at capture, a vehicle route or a road-tag leg. C and B therefore differ only by the 12-character route header, and both C-only features (route table, road ranges) are covered by unit tests, not by size measurement. The October native archive held 9,093 traffic links (2.67 M UTF-16 code units). Traffic definitions stay encoded as columns (the section 3.4 default). Omitting them in favour of a rebuild from the topology fingerprint was not measured, because there is nothing to measure here. Rig check 1 in the [checks file](../checks/f7p.md) runs this harness on an extracted native payload.
- **Synthetic .NET 10 CoreCLR** on a shared development PC (other worktrees' dotnet loads waited for, not excluded). Mono/Unity timings, native `SerializeSaveGame`, compression and whole-frame effects are not inferred. Number-text exactness on Mono (`R` doubles) is an F7d concern: the prototype never reformats a number, so it cannot show one.
- **D's bytes depend on the zlib build.** Decode → re-encode identity holds within this runtime only, so a pinned-hash corpus (F7f) cannot pin D across CoreCLR and Mono.
- Peak working set is per process and includes the fixture build, the writer-25 source and the source tree. Use it to compare candidates within one fixture only.
- The per-family tables (section 3.4 / Q2, Q3) report bytes and prune nothing. At 50000×2, `Citizens[].RecentVisits` drops from 38,450,000 to 8,000,010 chars and `Food.History` from 12,359,027 to 2,251,188 chars without any retention change.

## Perf wins

No mod perf win was taken, because no product code was touched. Inside the prototype, the encoder was made cheaper before the final matrix: varint lengths are computed without building the text, the canonical-integer check is allocation-free, and the shape index is cached per key array. There is no clean before/after pair for that change: an earlier partial matrix ran under different code and host load. The change is therefore not logged as a win. The four parked hotspots stay parked. Save serialization belongs to F7c/F7d, and this slice changes no shipped code.
