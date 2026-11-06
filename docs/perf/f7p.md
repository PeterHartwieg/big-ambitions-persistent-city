# City audit F7p: compact-format prototype matrix (6 November 2026, offline)

Refs #94. Base `e9995e5` (writer **25**). Plan: [F7 section 3.6 and section 4, row F7p](../plans/f7-save-snapshots.md#36-prototype-plan-f7p); [decision record](../decisions/2026-11-06-f7d-compact-format.md); [checks and rig list](../checks/f7p.md). The slice is tools only (`tools/compact-format/`). It changes nothing under `Core/`, `Persistence/` or `Runtime/`, and writer 25 is unchanged. The numbers below are a format comparison. They are not a before/after pair for shipped code, and they are not ordinary-play mod-work.

## Method

Run `python tools/compact-format/measure.py 7` (a full run rewrites the matrix file; `... 7 <fixture ...>` appends those rows only), then `python tools/compact-format/report.py` for the tables. Every (fixture, candidate) pair runs in its own foreground `CompactFormat` process after the shared other-dotnet wait, so peak working set belongs to that pair. Windows 11, .NET SDK 10.0.402, Release, workstation GC. Raw rows: [matrix.jsonl](assets/f7p/matrix.jsonl) (52 rows), [metadata.json](assets/f7p/metadata.json). Metrics only; no payload is committed. Measured at `6512136` (review round 1, which added the format-independent phases). The full run exceeded one tool session, so the last three fixtures (CF32, CF64, encoding-max) were appended in a second foreground step from the same head after the incomplete CF32 rows were dropped. Other worktrees' dotnet loads were waited for but the host was busier than in the first run; timings moved both ways, while bytes, hashes and round-trip results are identical to the first run.

- **Fixtures.** F7a's builders are compiled in place: the three representative F3-replay fixtures and the ten E5d stress cases. Each process rebuilds its fixture and writes it with `SaveCodec.Write`. The driver refuses any row whose writer-25 SHA-256 differs from F7a's retained `baseline.jsonl`, and all 52 rows matched. The strict writer-25 reader accepts each source and rewrites it to identical bytes. The F5 declared population is not available yet, because F5 has not declared it.
- **Snapshot stand-in.** F7c's snapshot does not exist yet. The prototype therefore parses the writer-25 payload once into a detached exact tree: number tokens keep their exact text, strings are unescaped, and key order is kept. Every candidate encodes from that tree and decodes back to it. Candidate A writes the tree with Newtonsoft's default escaping, which is byte-identical to `SaveCodec.Write` (unit test over every UTF-16 code unit). Encode and decode exclude capture, validation and materialization; those are measured separately below.
- **Candidates** (section 3.6). **A**: writer-25 JSON as written today. **B**: columnar compact JSON with per-field columns, recursive flattening of nested lists, positional slot columns for writer-25 tuples, and ordinal tables for strings seen at least twice, field names and field sequences. Number tokens are copied as exact text, or packed as zigzag base-64 varints when the token is exactly the invariant spelling of a long (or of `N.0`). **C**: B plus a per-save route table that deduplicates `Journey`/vehicle `Route` values by exact content, plus road-tag `ServiceId`s split into a topology head and a chained node range. **D**: C, then deflate (Optimal) of its UTF-8, then base64.
- **Format-independent phases** (section 3.6 capture, validate, decode-side activation). They do not depend on the wire candidate, so candidate A's process measures them once per fixture on the same state and tree, seven samples after a warm-up: *synchronize* and *validate* are F7a's `SaveCostRecorder` phases around `SaveCodec.Write` (today's `PrepareForSave` and `Validate`); *capture* is proxied by a detached deep copy of the exact tree (every container copied, immutable leaves shared), i.e. the copying a snapshot capture does, without reading the live graph; *materialize* is an **upper bound**, the strict writer-25 `SaveCodec.Read` of the source text (an F7d decoder materializes from its columns without a second text parse); *activation validate* and *simulator construction* are F7a's `CompleteInitialization` steps. The composed pause is synchronize + capture proxy + validate + encode (publish is about 0.005 ms, F7a); the composed load is decode + materialize + activation validate + construction, an upper bound. F7c replaces the capture proxy with a real snapshot capture.
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
| representative-957x2 | 21.2 / 26.3 / 26.3 (0) | 29.7 / 34.6 / 34.6 (1) | 25.1 / 35.4 / 35.4 (3) | 66.3 / 69.1 / 69.1 (7) | 11.0 / 14.3 | 9,362,392 / 10,780,024 / 10,780,080 / 12,491,000 | 7.77 |
| representative-5000x2 | 42.8 / 81.5 / 81.5 (4) | 134.6 / 159.0 / 159.0 (7) | 151.0 / 183.3 / 183.3 (7) | 177.4 / 222.1 / 222.1 (7) | 58.6 / 87.0 | 48,882,712 / 61,995,416 / 61,995,504 / 70,022,088 | 9.38 |
| representative-50000x2 | 305.1 / 649.0 / 649.0 (7) | 1,759.1 / 2,306.8 / 2,306.8 (7) | 2,161.5 / 2,995.7 / 2,995.7 (7) | 1,924.6 / 2,521.6 / 2,521.6 (7) | 601.7 / 1,478.4 | 440,052,208 / 541,035,824 / 541,037,512 / 613,626,584 | 8.99 |
| 957x2 | 42.9 / 128.9 / 128.9 (7) | 205.4 / 249.6 / 249.6 (7) | 167.9 / 277.2 / 277.2 (7) | 260.6 / 424.3 / 424.3 (7) | 48.1 / 97.2 | 71,169,712 / 91,009,416 / 91,010,448 / 97,988,024 | 9.59 |
| 957x3 | 53.2 / 118.5 / 118.5 (7) | 182.9 / 265.8 / 265.8 (7) | 191.8 / 239.0 / 239.0 (7) | 207.4 / 313.0 / 313.0 (7) | 53.8 / 120.5 | 73,883,872 / 94,807,016 / 94,807,984 / 102,245,624 | 9.57 |
| 5000x2 | 87.4 / 146.3 / 146.3 (7) | 322.7 / 429.8 / 429.8 (7) | 279.3 / 410.3 / 410.3 (7) | 286.3 / 344.7 / 344.7 (7) | 92.2 / 177.6 | 121,554,640 / 147,714,192 / 147,711,384 / 160,423,624 | 9.64 |
| 5000x3 | 84.9 / 170.5 / 170.5 (7) | 292.4 / 413.0 / 413.0 (7) | 362.3 / 456.5 / 456.5 (7) | 329.4 / 382.0 / 382.0 (7) | 107.4 / 252.6 | 135,669,344 / 156,016,824 / 156,017,440 / 170,641,448 | 9.12 |
| churn | 92.3 / 214.3 / 214.3 (7) | 343.6 / 393.3 / 393.3 (7) | 403.1 / 494.3 / 494.3 (7) | 427.7 / 439.0 / 439.0 (7) | 121.1 / 294.6 | 120,782,224 / 159,757,664 / 159,757,992 / 169,570,528 | 11.50 |
| 50000x2 | 419.9 / 681.4 / 681.4 (7) | 1,338.4 / 1,550.5 / 1,550.5 (7) | 1,374.6 / 1,621.6 / 1,621.6 (7) | 1,813.7 / 2,214.9 / 2,214.9 (7) | 423.9 / 855.3 | 650,861,432 / 629,820,936 / 629,822,320 / 694,603,904 | 9.27 |
| 50000x3 | 426.1 / 720.0 / 720.0 (7) | 1,896.4 / 2,013.1 / 2,013.1 (7) | 1,977.9 / 2,220.5 / 2,220.5 (7) | 2,258.4 / 2,530.0 / 2,530.0 (7) | 533.8 / 1,426.6 | 788,810,912 / 844,576,440 / 844,606,936 / 928,040,048 | 9.86 |
| CF32 | 809.2 / 1,076.8 / 1,076.8 (7) | 2,042.9 / 2,203.9 / 2,203.9 (7) | 2,602.2 / 2,790.4 / 2,790.4 (7) | 3,920.0 / 4,492.1 / 4,492.1 (7) | 709.7 / 1,813.4 | 809,174,792 / 881,618,848 / 881,651,656 / 967,697,776 | 9.96 |
| CF64 | 804.5 / 1,620.4 / 1,620.4 (7) | 3,723.5 / 4,311.2 / 4,311.2 (7) | 3,452.3 / 3,902.6 / 3,902.6 (7) | 5,216.6 / 6,439.2 / 6,439.2 (7) | 864.9 / 2,522.6 | 963,112,240 / 947,319,688 / 947,317,880 / 1,044,803,832 | 9.06 |
| encoding-max | 1.1 / 1.7 / 1.7 (0) | 1.1 / 5.0 / 5.0 (0) | 1.0 / 5.9 / 5.9 (0) | 1.7 / 6.0 / 6.0 (0) | 0.3 / 0.7 | 606,408 / 496,808 / 496,864 / 779,248 | 3.38 |

### Decode and memory

| Fixture | Decode A | Decode B | Decode C | Decode D | Decode allocated A / C | Retained MiB A / B / C / D | Distinct string instances A / C | Peak working set MiB A / B / C / D |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| representative-957x2 | 67.9 / 83.3 / 83.3 (7) | 43.6 / 57.8 / 57.8 (6) | 38.5 / 44.3 / 44.3 (5) | 56.8 / 70.8 / 70.8 (6) | 21,000,520 / 16,205,992 | 10.8 / 7.1 / 7.1 / 7.1 | 37,088 / 8,230 | 167 / 159 / 159 / 150 |
| representative-5000x2 | 244.1 / 376.0 / 376.0 (7) | 141.7 / 202.8 / 202.8 (7) | 144.4 / 208.1 / 208.1 (7) | 156.1 / 264.3 / 264.3 (7) | 109,758,728 / 86,050,512 | 56.7 / 36.2 / 36.2 / 36.2 | 194,011 / 29,831 | 624 / 614 / 612 / 588 |
| representative-50000x2 | 1,801.9 / 2,794.8 / 2,794.8 (7) | 2,049.1 / 2,466.4 / 2,466.4 (7) | 1,996.0 / 2,417.5 / 2,417.5 (7) | 1,434.8 / 2,156.0 / 2,156.0 (7) | 1,023,755,184 / 793,456,872 | 530.5 / 339.8 / 339.8 / 339.8 | 1,747,068 / 202,212 | 4,434 / 4,194 / 4,239 / 4,473 |
| 957x2 | 349.7 / 574.1 / 574.1 (7) | 172.5 / 273.0 / 273.0 (7) | 222.4 / 296.8 / 296.8 (7) | 253.3 / 426.8 / 426.8 (7) | 212,257,992 / 128,153,632 | 115.6 / 70.9 / 70.9 / 70.9 | 173,686 / 29,141 | 1,067 / 911 / 874 / 927 |
| 957x3 | 539.0 / 667.9 / 667.9 (7) | 251.4 / 384.6 / 384.6 (7) | 196.8 / 337.6 / 337.6 (7) | 226.4 / 334.2 / 334.2 (7) | 218,369,648 / 133,178,672 | 119.5 / 73.3 / 73.3 / 73.3 | 182,299 / 32,009 | 1,074 / 975 / 962 / 945 |
| 5000x2 | 673.0 / 786.9 / 786.9 (7) | 277.8 / 431.1 / 431.1 (7) | 308.2 / 538.2 / 538.2 (7) | 397.5 / 441.7 / 441.7 (7) | 306,963,632 / 208,908,704 | 168.0 / 98.3 / 98.3 / 98.3 | 419,804 / 52,384 | 1,373 / 1,244 / 1,245 / 1,317 |
| 5000x3 | 530.7 / 810.3 / 810.3 (7) | 342.4 / 530.3 / 530.3 (7) | 324.2 / 513.8 / 513.8 (7) | 366.3 / 482.3 / 482.3 (7) | 338,723,624 / 233,116,984 | 187.9 / 110.3 / 110.3 / 110.3 | 464,804 / 62,312 | 1,662 / 1,571 / 1,557 / 1,620 |
| churn | 686.9 / 962.2 / 962.2 (7) | 305.9 / 573.2 / 573.2 (7) | 492.1 / 701.4 / 701.4 (7) | 350.1 / 486.1 / 486.1 (7) | 388,826,928 / 227,002,528 | 210.2 / 127.7 / 127.7 / 127.7 | 245,366 / 30,165 | 1,689 / 1,628 / 1,654 / 1,549 |
| 50000x2 | 1,716.8 / 2,492.7 / 2,492.7 (7) | 1,885.5 / 2,232.6 / 2,232.6 (7) | 1,931.0 / 2,192.5 / 2,192.5 (7) | 1,581.4 / 2,408.2 / 2,408.2 (7) | 1,299,822,608 / 1,016,614,456 | 723.4 / 383.9 / 383.9 / 383.9 | 3,013,424 / 222,857 | 6,761 / 5,918 / 5,864 / 6,427 |
| 50000x3 | 2,212.0 / 3,230.1 / 3,230.1 (7) | 1,966.6 / 2,976.9 / 2,976.9 (7) | 1,906.4 / 2,983.5 / 2,983.5 (7) | 2,412.4 / 2,906.7 / 2,906.7 (7) | 1,617,558,920 / 1,289,769,048 | 920.6 / 502.7 / 502.7 / 502.7 | 3,463,424 / 316,738 | 8,959 / 7,674 / 7,324 / 8,905 |
| CF32 | 2,623.9 / 4,413.7 / 4,413.7 (7) | 2,112.6 / 3,141.0 / 3,141.0 (7) | 2,425.3 / 3,792.8 / 3,792.8 (7) | 3,061.4 / 4,277.8 / 4,277.8 (7) | 1,656,443,336 / 1,310,827,072 | 937.5 / 515.0 / 515.0 / 515.1 | 3,526,245 / 361,569 | 8,310 / 7,598 / 7,455 / 8,514 |
| CF64 | 5,471.0 / 6,449.7 / 6,449.7 (7) | 2,954.1 / 3,860.1 / 3,860.1 (7) | 3,618.5 / 5,422.1 / 5,422.1 (7) | 4,049.6 / 5,170.7 / 5,170.7 (7) | 1,798,443,352 / 1,523,585,368 | 1,047.4 / 527.2 / 527.2 / 527.2 | 5,126,245 / 361,617 | 8,527 / 8,457 / 8,465 / 9,609 |
| encoding-max | 1.2 / 1.3 / 1.3 (0) | 0.5 / 0.6 / 0.6 (0) | 0.5 / 0.6 / 0.6 (0) | 0.9 / 1.3 / 1.3 (0) | 462,168 / 389,584 | 0.3 / 0.2 / 0.2 / 0.2 | 733 / 354 | 59 / 53 / 53 / 54 |

### Format-independent phases (measured once per fixture in candidate A's process; ms p50 / p95 / max (>= 30 ms of 7))

| Fixture | Synchronize (F7a recorder) | Validate (F7a recorder) | Capture proxy: detached copy | Copy allocated B | Materialize upper bound (strict writer-25 read) | Activation validate | Simulator construction |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| representative-957x2 | 1.5 / 3.0 / 3.0 (0) | 11.4 / 37.4 / 37.4 (1) | 7.8 / 14.4 / 14.4 (0) | 2,109,184 | 116.3 / 387.0 / 387.0 (7) | 30.8 / 51.0 / 51.0 (4) | 0.4 / 0.5 / 0.5 (0) |
| representative-5000x2 | 6.2 / 8.9 / 8.9 (0) | 26.9 / 33.0 / 33.0 (2) | 26.0 / 36.5 / 36.5 (2) | 11,002,664 | 488.7 / 587.8 / 587.8 (7) | 123.4 / 154.5 / 154.5 (7) | 2.6 / 3.3 / 3.3 (0) |
| representative-50000x2 | 38.2 / 45.0 / 45.0 (7) | 165.8 / 559.2 / 559.2 (7) | 191.0 / 582.9 / 582.9 (7) | 103,854,312 | 3,725.4 / 4,399.7 / 4,399.7 (7) | 840.4 / 920.6 / 920.6 (7) | 30.7 / 47.3 / 47.3 (4) |
| 957x2 | 7.6 / 15.8 / 15.8 (0) | 54.6 / 165.9 / 165.9 (7) | 37.2 / 93.2 / 93.2 (7) | 24,658,792 | 587.3 / 670.0 / 670.0 (7) | 202.6 / 347.0 / 347.0 (7) | 0.3 / 0.7 / 0.7 (0) |
| 957x3 | 6.3 / 7.7 / 7.7 (0) | 66.5 / 80.8 / 80.8 (7) | 33.3 / 44.5 / 44.5 (5) | 25,286,584 | 618.1 / 644.0 / 644.0 (7) | 210.9 / 234.9 / 234.9 (7) | 0.3 / 0.6 / 0.6 (0) |
| 5000x2 | 8.5 / 22.0 / 22.0 (0) | 84.0 / 166.0 / 166.0 (7) | 66.1 / 83.1 / 83.1 (7) | 33,941,520 | 1,173.0 / 1,567.2 / 1,567.2 (7) | 365.3 / 674.2 / 674.2 (7) | 1.3 / 2.1 / 2.1 (0) |
| 5000x3 | 11.7 / 13.8 / 13.8 (0) | 85.8 / 106.5 / 106.5 (7) | 61.4 / 66.0 / 66.0 (7) | 37,221,520 | 1,114.1 / 1,171.7 / 1,171.7 (7) | 375.8 / 425.0 / 425.0 (7) | 1.3 / 1.4 / 1.4 (0) |
| churn | 16.9 / 25.0 / 25.0 (0) | 104.7 / 459.7 / 459.7 (7) | 97.3 / 138.4 / 138.4 (7) | 45,532,008 | 1,040.0 / 1,307.6 / 1,307.6 (7) | 431.4 / 442.0 / 442.0 (7) | 0.2 / 0.6 / 0.6 (0) |
| 50000x2 | 29.1 / 30.6 / 30.6 (1) | 188.2 / 198.4 / 198.4 (7) | 205.1 / 217.8 / 217.8 (7) | 131,381,520 | 4,651.6 / 5,166.7 / 5,166.7 (7) | 1,568.6 / 1,651.9 / 1,651.9 (7) | 14.8 / 17.6 / 17.6 (0) |
| 50000x3 | 42.4 / 66.4 / 66.4 (7) | 204.7 / 235.0 / 235.0 (7) | 248.8 / 276.4 / 276.4 (7) | 164,181,520 | 5,752.7 / 6,150.3 / 6,150.3 (7) | 1,768.9 / 1,783.6 / 1,783.6 (7) | 14.5 / 19.2 / 19.2 (0) |
| CF32 | 60.1 / 64.5 / 64.5 (7) | 337.6 / 400.3 / 400.3 (7) | 349.9 / 405.5 / 405.5 (7) | 167,891,888 | 8,419.6 / 11,799.0 / 11,799.0 (7) | 2,550.0 / 3,406.4 / 3,406.4 (7) | 17.5 / 26.2 / 26.2 (0) |
| CF64 | 43.9 / 70.2 / 70.2 (7) | 288.9 / 803.6 / 803.6 (7) | 351.3 / 463.2 / 463.2 (7) | 180,691,888 | 9,758.4 / 11,552.4 / 11,552.4 (7) | 3,473.0 / 3,852.4 / 3,852.4 (7) | 20.5 / 36.1 / 36.1 (1) |
| encoding-max | 0.5 / 0.6 / 0.6 (0) | 1.1 / 5.1 / 5.1 (0) | 0.1 / 0.1 / 0.1 (0) | 21,728 | 9.9 / 12.9 / 12.9 (0) | 3.7 / 5.3 / 5.3 (0) | 0.0 / 0.1 / 0.1 (0) |

### Composed synchronous save pause and load per candidate (ms; sum of phase p50s, and sum of phase p95s as an upper bound)

Pause = synchronize + capture proxy + validate + encode (publish is about 0.005 ms, F7a). Load = decode + materialize upper bound + activation validate + construction; the materialize term re-parses writer-25 text, so load is an upper bound.

| Fixture | Pause A | Pause B | Pause C | Pause D | Load A | Load B | Load C | Load D |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| representative-957x2 | 41.9 / 81.1 | 50.5 / 89.4 | 45.9 / 90.2 | 87.1 / 123.9 | 215.5 / 521.8 | 191.1 / 496.3 | 186.1 / 482.8 | 204.4 / 509.3 |
| representative-5000x2 | 101.9 / 159.9 | 193.7 / 237.3 | 210.1 / 261.6 | 236.5 / 300.4 | 858.8 / 1,121.6 | 756.4 / 948.3 | 759.1 / 953.6 | 770.8 / 1,009.9 |
| representative-50000x2 | 700.1 / 1,836.0 | 2,154.1 / 3,493.8 | 2,556.5 / 4,182.8 | 2,319.6 / 3,708.7 | 6,398.4 / 8,162.4 | 6,645.6 / 7,834.1 | 6,592.4 / 7,785.1 | 6,031.3 / 7,523.6 |
| 957x2 | 142.4 / 403.7 | 304.8 / 524.5 | 267.3 / 552.1 | 360.1 / 699.1 | 1,139.9 / 1,591.8 | 962.8 / 1,290.7 | 1,012.6 / 1,314.5 | 1,043.5 / 1,444.5 |
| 957x3 | 159.3 / 251.4 | 289.1 / 398.8 | 297.9 / 372.0 | 313.5 / 445.9 | 1,368.2 / 1,547.4 | 1,080.6 / 1,264.1 | 1,026.0 / 1,217.1 | 1,055.6 / 1,213.7 |
| 5000x2 | 246.0 / 417.3 | 481.2 / 700.8 | 437.8 / 681.4 | 444.8 / 615.7 | 2,212.6 / 3,030.4 | 1,817.4 / 2,674.6 | 1,847.8 / 2,781.8 | 1,937.1 / 2,685.3 |
| 5000x3 | 243.8 / 356.8 | 451.4 / 599.3 | 521.3 / 642.8 | 488.4 / 568.3 | 2,021.8 / 2,408.4 | 1,833.5 / 2,128.4 | 1,815.3 / 2,111.9 | 1,857.4 / 2,080.4 |
| churn | 311.1 / 837.3 | 562.5 / 1,016.3 | 622.0 / 1,117.4 | 646.5 / 1,062.1 | 2,158.6 / 2,712.4 | 1,777.7 / 2,323.3 | 1,963.9 / 2,451.6 | 1,821.8 / 2,236.2 |
| 50000x2 | 842.2 / 1,128.2 | 1,760.8 / 1,997.3 | 1,796.9 / 2,068.4 | 2,236.1 / 2,661.7 | 7,951.9 / 9,328.9 | 8,120.6 / 9,068.8 | 8,166.0 / 9,028.8 | 7,816.4 / 9,244.5 |
| 50000x3 | 922.1 / 1,297.8 | 2,392.4 / 2,591.0 | 2,473.8 / 2,798.4 | 2,754.3 / 3,107.9 | 9,748.1 / 11,183.2 | 9,502.7 / 10,929.9 | 9,442.5 / 10,936.6 | 9,948.5 / 10,859.8 |
| CF32 | 1,556.8 / 1,947.0 | 2,790.5 / 3,074.2 | 3,349.8 / 3,660.7 | 4,667.6 / 5,362.4 | 13,611.0 / 19,645.2 | 13,099.7 / 18,372.6 | 13,412.3 / 19,024.3 | 14,048.5 / 19,509.4 |
| CF64 | 1,488.6 / 2,957.4 | 4,407.6 / 5,648.3 | 4,136.5 / 5,239.6 | 5,900.7 / 7,776.2 | 18,722.9 / 21,890.7 | 16,206.1 / 19,301.1 | 16,870.4 / 20,863.1 | 17,301.5 / 20,611.7 |
| encoding-max | 2.7 / 7.5 | 2.8 / 10.8 | 2.7 / 11.8 | 3.3 / 11.9 | 14.8 / 19.5 | 14.2 / 18.8 | 14.2 / 18.8 | 14.6 / 19.5 |

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

Section 3.6 makes the section 3.1 targets the pass criteria. **Under those criteria no candidate met every target in this prototype.** C meets the byte, record and retained targets and misses the pause, allocation and stress-load targets.

| Target | Measured here | Result |
| --- | --- | --- |
| Typical bytes ≤ 35 % of writer-25 representative 957×2 (≤ 816,686 chars), absolute ≤ 6 Mi | 694,001 chars (29.74 %); 5,000×2 27.11 %; 50,000×2 27.41 % | **met** |
| Stress bytes ≤ 40 % of 162,346,836 (≤ 64 Mi chars) at 50000×2 | 33,972,886 chars (20.93 %); CF64 52,252,777 | **met** |
| Records under the 4,000,000 cap (table entries charged one each) | max 3,775,940 (CF64); 50000×2 2,062,636 | **met** (CF64 is a compatibility composition, not a budget) |
| Typical synchronous pause ≤ 40 ms p95 (capture + validate + encode + publish) | composed p50 45.9 ms (synchronize 1.5 + capture proxy 7.8 + validate 11.4 + encode 25.1); sum of phase p95s 90.2 ms | **not met** |
| Stress pause ≤ 600 ms | composed p50 1,796.9 ms (29.1 + 205.1 + 188.2 + encode 1,374.6); sum of p95s 2,068.4 ms | **not met** (3.0 × at p50) |
| Allocation ≤ 2.5 × payload UTF-16 bytes | encode 7.8–11.5 × (3.4 × at encoding-max). In absolute terms 629.8 MB at 50000×2, against F7a's writer-25 save allocation of 1,567.7 MB | **not met** |
| Retained ≤ writer-25 retained; intern-on-decode ≤ 80 % | decoded tree, C vs A: 0.66 (representative 957×2) to 0.50 (CF64); distinct string instances 8,230 vs 37,088 at representative 957×2 | **met on the tree basis**; runtime `CityState` retained is F7d's measurement |
| Load ≤ 400 ms typical (read + activation) | composed p50 186.1 ms (decode 38.5 + materialize upper bound 116.3 + activation validate 30.8 + construction 0.4); sum of p95s 482.8 ms | p50 **met** even with the upper-bound materialize; p95 sum over |
| Load ≤ 2,500 ms stress | composed p50 8,166.0 ms; without the materialize term still 3,514.4 ms (decode 1,931.0 + activation validate 1,568.6 + construction 14.8) | **not met**, even as a lower bound |

**Same-harness encode ratio.** In this harness C encodes at 1.19 × A at representative 957×2 (25.1 vs 21.2 ms p50), 3.27 × at 50000×2 (1,374.6 vs 419.9 ms) and 3.2–4.6 × on the other stress fixtures (7.09 × at representative 50,000×2). For comparison, F7a's typed writer-25 `SaveCodec.Write` took 2,036.83 ms p50 at 50000×2, of which encode was 1,815.57 ms. A here is only a near-copy of an already-built tree. The ratio therefore shows that the columnar format itself adds encode work relative to a straight copy: interning, per-column grouping and varint packing. The shortfall is not just a tree-encoder artifact. The C encode split puts the column writer at 57–73 % of encode time (for example 855.3 of 1,374.6 ms at 50000×2). A typed snapshot would avoid re-reading number text and hashing every string occurrence, but this record makes no claim about how fast such a writer is. F7d's planning should treat the 600 ms stress pause and the 2.5 × allocation ceilings as open risks. The bytes are a property of the format and carry over to F7d; the timings are not.

## Flags

Every encode or decode sample ≥ 30 ms is flagged: **648 of 728** samples (52 rows × 7 encode + 7 decode). At representative 957×2, encode ≥ 30 ms occurred in 0/7 (A), 1/7 (B), 3/7 (C) and 7/7 (D) samples, and decode ≥ 30 ms in 7/7 (A), 6/7 (B), 5/7 (C) and 6/7 (D). The format-independent phases flag as shown in their table (for example validate 1/7 and activation validate 4/7 at representative 957×2, every materialize sample). `encoding-max` has none. These are save/load latency samples of an offline tool, a separate criterion from ordinary-play mod-work. F7p adds no runtime code, so it adds no mod-work and no per-frame work. Mod-work p95 < 5 ms and whole-frame metrics are unaffected by this slice and are tracked separately.

## Limits

- **Fixture coverage.** None of the 13 fixtures carries a traffic definition, a committed `Journey` at capture, a vehicle route or a road-tag leg. C and B therefore differ only by the 12-character route header, and both C-only features (route table, road ranges) are covered by unit tests, not by size measurement. The October native archive held 9,093 traffic links (2.67 M UTF-16 code units). Traffic definitions stay encoded as columns (the section 3.4 default). Omitting them in favour of a rebuild from the topology fingerprint was not measured, because there is nothing to measure here. Rig check 1 in the [checks file](../checks/f7p.md) runs this harness on an extracted native payload.
- **Proxies.** Capture is a detached tree copy, not F7c's capture from the live graph. Materialization is the strict writer-25 reader on text, an upper bound. Synchronize and validate are today's writer-25 phases, which F7c moves onto the snapshot.
- **Synthetic .NET 10 CoreCLR** on a shared development PC (other worktrees' dotnet loads waited for, not excluded; this run's host was busier than the first). Mono/Unity timings, native `SerializeSaveGame`, compression and whole-frame effects are not inferred. Number-text exactness on Mono (`R` doubles) is an F7d concern: the prototype never reformats a number, so it cannot show one.
- **D's bytes depend on the zlib build.** Decode → re-encode identity holds within this runtime only, so a pinned-hash corpus (F7f) cannot pin D across CoreCLR and Mono.
- Peak working set is per process and includes the fixture build, the writer-25 source, the source tree and, in A's process, the format-independent phases. Use it to compare candidates within one fixture only.
- The per-family tables (section 3.4 / Q2, Q3) report bytes and prune nothing. At 50000×2, `Citizens[].RecentVisits` drops from 38,450,000 to 8,000,010 chars and `Food.History` from 12,359,027 to 2,251,188 chars without any retention change.

## Perf wins

No mod perf win was taken, because no product code was touched. Inside the prototype, the encoder was made cheaper before the first matrix: varint lengths are computed without building the text, the canonical-integer check is allocation-free, and the shape index is cached per key array. There is no clean before/after pair for that change: an earlier partial matrix ran under different code and host load. The change is therefore not logged as a win. The four parked hotspots stay parked. Save serialization belongs to F7c/F7d, and this slice changes no shipped code.
