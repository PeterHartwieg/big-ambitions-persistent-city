# City audit F7d: compact writer 34, measured offline (8 November 2026)

Refs #94. Plan: [F7 sections 3.1, 5 and 6, row F7d](../plans/f7-save-snapshots.md#4-slice-breakdown); [compact-format decision](../decisions/2026-11-06-f7d-compact-format.md); checks and rig list: [docs/checks/f7d.md](../checks/f7d.md). Base `3f734cb`, writer 33 → writer 34.

All numbers are synthetic: .NET 10 Release on the Windows 11 development PC, one dotnet process at a time, foreground, after the shared other-dotnet wait. Other worktrees' loads were waited for, not excluded, and the host was busy, so timings vary between runs by tens of percent; sizes, records and hashes do not. They are not game, Mono or rig evidence. Save and load samples are the save latency criterion and never count toward ordinary-play mod-work.

## Method

`tests/F7dMeasure.cs` (opt-in): `PC_F7D_MEASURE=<file> dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F7dMeasure`, with `PC_F7D_FIXTURES` and `PC_F7D_SAMPLES`. Fixtures are F7c's fresh cities in the current schema (`tests/F7cFixtures.cs`): the F7a representative replay at 957, 5,000 and 50,000 residents (factor 2), the factor-3 cities, two saturated 957-resident cities, the escaping city and the morning-rush cities with journeys in flight. **Before** is the predecessor JSON writer (the frozen writer 33) and **after** is writer 34, both on the same fresh seeded city in the same process, so the pair is matched. Per fixture: 7 timed samples after one warm-up for `SaveCodec.Write` with every save-cost phase, allocation per write, the strict off-thread read, activation (`ValidateForActivation` plus `CitySimulator` construction) and retained bytes after one read. The writer-34 body is checked equal to the writer-33 bytes relabelled 33→34 before timing. Four processes: the six 957-resident fixtures, the three 5,000 fixtures, then each 50,000 fixture alone.

The **F5 declared load** is the reference load of 1,914–2,871 persons ([F5d](f5d.md)): `representative-957x2` (957 residents + 957 visitors) and `fresh-957x3` (957 + 1,914). The first fixture in a process includes JIT warm-up in both columns, which is why representative 957×2's writer-33 pause is higher than the other 957-resident cities.

## Results

### Size, records and tables

| Fixture | Residents / visitors / journeys | Writer 33 chars | Writer 34 chars (÷33) | UTF-8 bytes 33 → 34 | UTF-16 bytes 34 | Records streaming / semantic (tables included) | Tables: strings / keys / shapes / routes | Route refs / road tags | Nesting | Decoded values |
| --- | --- | ---: | ---: | --- | ---: | --- | --- | --- | ---: | ---: |
| representative-957x2 | 957 / 957 / 0 | 2,456,547 | 849,572 (0.3458) | 2,456,547 → 849,572 | 1,699,144 | 18,934 / 18,931 | 4,599 / 135 / 15 / 0 | 0 / 0 | 8 | 192,670 |
| fresh-957x3 | 957 / 1,914 / 0 | 3,112,173 | 1,152,633 (0.3704) | 3,112,173 → 1,152,633 | 2,305,266 | 22,096 / 22,093 | 4,890 / 135 / 15 / 0 | 0 / 0 | 8 | 259,242 |
| saturated-957x2 | 957 / 957 / 0 | 3,744,651 | 983,514 (0.2626) | 3,744,651 → 983,514 | 1,967,028 | 71,217 / 71,214 | 3,211 / 135 / 15 / 0 | 0 / 0 | 8 | 246,341 |
| saturated-957x3 | 957 / 1,914 / 0 | 4,400,277 | 1,287,437 (0.2926) | 4,400,277 → 1,287,437 | 2,574,874 | 74,379 / 74,376 | 3,502 / 135 / 15 / 0 | 0 / 0 | 8 | 312,913 |
| encoding-max | 957 / 957 / 0 | 2,591,362 | 915,865 (0.3534) | 2,714,242 → 977,305 | 1,831,730 | 19,649 / 19,439 | 4,867 / 135 / 15 / 0 | 0 / 0 | 8 | 193,765 |
| rush-957x2 | 957 / 957 / 229 | 2,386,685 | 908,507 (0.3807) | 2,386,685 → 908,507 | 1,817,014 | 19,933 / 19,930 | 4,837 / 146 / 18 / 9 | 116 / 0 | 10 | 193,652 |
| representative-5000x2 | 5,000 / 5,000 / 0 | 12,837,578 | 4,013,975 (0.3127) | 12,837,578 → 4,013,975 | 8,027,950 | 90,268 / 90,265 | 15,779 / 135 / 15 / 0 | 0 / 0 | 8 | 1,005,488 |
| fresh-5000x3 | 5,000 / 10,000 / 0 | 16,263,665 | 5,480,569 (0.3370) | 16,263,665 → 5,480,569 | 10,961,138 | 106,449 / 106,446 | 16,960 / 135 / 15 / 0 | 0 / 0 | 8 | 1,353,449 |
| rush-5000x2 | 5,000 / 5,000 / 1,189 | 12,462,543 | 4,238,867 (0.3401) | 12,462,543 → 4,238,867 | 8,477,734 | 95,052 / 95,049 | 16,831 / 146 / 18 / 12 | 566 / 0 | 10 | 1,010,500 |
| representative-50000x2 | 50,000 / 50,000 / 0 | 116,812,235 | 35,100,344 (0.3005) | 116,812,235 → 35,100,344 | 70,200,688 | 748,905 / 748,902 | 73,561 / 135 / 15 / 0 | 0 / 0 | 8 | 9,592,538 |
| fresh-50000x3 | 50,000 / 100,000 / 0 | 149,621,494 | 47,812,181 (0.3196) | 149,621,494 → 47,812,181 | 95,624,362 | 901,732 / 901,729 | 82,790 / 135 / 15 / 0 | 0 / 0 | 8 | 13,046,134 |

### Save pause (`SaveCodec.Write`: synchronize + capture + validate + encode; ms p50 / p95 / max (samples ≥ 30 ms of 7))

| Fixture | Writer 33 | Writer 34 | Encode phase p50 33 → 34 | Writer 34 phases p50: synchronize / capture / validate / encode | Compact table pass / column writer (mean ms) |
| --- | --- | --- | --- | --- | --- |
| representative-957x2 | 152.3 / 191.1 / 191.1 (7) | 131.2 / 171.4 / 171.4 (7) | 70.7 → 115.0 | 1.2 / 4.5 / 16.2 / 115.0 | 4.7 / 18.3 |
| fresh-957x3 | 50.3 / 64.4 / 64.4 (7) | 141.3 / 191.3 / 191.3 (7) | 37.1 → 127.4 | 1.1 / 2.8 / 6.8 / 127.4 | 7.6 / 36.1 |
| saturated-957x2 | 69.1 / 93.8 / 93.8 (7) | 134.3 / 149.0 / 149.0 (7) | 51.1 → 114.4 | 0.5 / 11.0 / 4.2 / 114.4 | 7.2 / 26.2 |
| saturated-957x3 | 76.0 / 94.9 / 94.9 (7) | 186.4 / 210.8 / 210.8 (7) | 54.5 → 162.7 | 0.8 / 15.2 / 7.7 / 162.7 | 10.5 / 15.2 |
| encoding-max | 39.4 / 53.4 / 53.4 (7) | 93.9 / 124.5 / 124.5 (7) | 30.5 → 79.3 | 1.1 / 1.4 / 5.3 / 79.3 | 5.7 / 23.2 |
| rush-957x2 | 42.9 / 49.5 / 49.5 (7) | 95.2 / 126.1 / 126.1 (7) | 33.2 → 82.2 | 0.7 / 9.3 / 3.2 / 82.2 | 5.5 / 42.5 |
| representative-5000x2 | 320.5 / 332.8 / 332.8 (7) | 665.8 / 711.4 / 711.4 (7) | 225.6 → 592.5 | 4.5 / 24.2 / 54.1 / 592.5 | 20.3 / 68.9 |
| fresh-5000x3 | 231.5 / 256.8 / 256.8 (7) | 498.4 / 533.1 / 533.1 (7) | 152.3 → 395.1 | 3.8 / 27.6 / 45.4 / 395.1 | 24.1 / 74.4 |
| rush-5000x2 | 179.4 / 224.6 / 224.6 (7) | 410.8 / 429.7 / 429.7 (7) | 129.0 → 344.3 | 2.2 / 16.1 / 29.2 / 344.3 | 20.6 / 54.9 |
| representative-50000x2 | 1,828.1 / 2,041.7 / 2,041.7 (7) | 4,058.7 / 4,288.1 / 4,288.1 (7) | 1,169.2 → 3,454.1 | 27.6 / 272.6 / 239.3 / 3,454.1 | 561.1 / 2,187.3 |
| fresh-50000x3 | 5,245.8 / 5,686.6 / 5,686.6 (7) | 5,760.4 / 6,221.5 / 6,221.5 (7) | 3,204.3 → 4,519.6 | 37.0 / 681.2 / 357.9 / 4,519.6 | 294.4 / 1,187.1 |

### Allocation per save

| Fixture | Writer 33 B (÷ its UTF-16 B) | Writer 34 B (÷ its UTF-16 B) | Writer 34 ÷ writer-33 UTF-16 B |
| --- | ---: | ---: | ---: |
| representative-957x2 | 26,843,736 (5.464) | 40,514,528 (23.844) | 8.25 |
| fresh-957x3 | 36,527,792 (5.869) | 56,406,080 (24.468) | 9.06 |
| saturated-957x2 | 47,035,136 (6.28) | 58,363,992 (29.671) | 7.79 |
| saturated-957x3 | 56,735,960 (6.447) | 74,080,512 (28.771) | 8.42 |
| encoding-max | 27,778,960 (5.36) | 41,305,568 (22.55) | 7.97 |
| rush-957x2 | 26,527,872 (5.557) | 41,298,696 (22.729) | 8.65 |
| representative-5000x2 | 140,315,568 (5.465) | 214,492,464 (26.718) | 8.35 |
| fresh-5000x3 | 191,141,920 (5.876) | 286,413,664 (26.13) | 8.81 |
| rush-5000x2 | 138,553,648 (5.559) | 217,018,896 (25.599) | 8.71 |
| representative-50000x2 | 1,297,329,928 (5.553) | 1,976,572,080 (28.156) | 8.46 |
| fresh-50000x3 | 1,788,199,688 (5.976) | 2,794,135,112 (29.22) | 9.34 |

### Load (off-thread strict read; activation = `ValidateForActivation` + `CitySimulator` construction) and retained memory

| Fixture | Read 33 ms | Read 34 ms | Of which decode + body text (34) | Activation 33 → 34 ms p50 | Read + activation p50 33 → 34 | Read allocation 33 → 34 B | Retained after read 33 → 34 B (÷) |
| --- | --- | --- | ---: | --- | --- | --- | --- |
| representative-957x2 | 102.9 / 372.8 / 372.8 (7) | 133.9 / 164.3 / 164.3 (7) | 32.3 | 41.9 → 30.4 | 144.8 → 164.3 | 55,088,944 → 75,782,864 | 4,995,808 → 4,662,328 (0.933) |
| fresh-957x3 | 124.6 / 145.3 / 145.3 (7) | 167.3 / 183.0 / 183.0 (7) | 32.3 | 41.6 → 47.5 | 166.2 → 214.8 | 77,266,336 → 105,760,280 | 6,935,024 → 6,601,424 (0.952) |
| saturated-957x2 | 129.0 / 150.9 / 150.9 (7) | 165.3 / 202.2 / 202.2 (7) | 36.4 | 54.0 → 49.7 | 183.0 → 215.1 | 84,227,560 → 109,952,656 | 7,545,752 → 7,212,272 (0.956) |
| saturated-957x3 | 158.1 / 223.5 / 223.5 (7) | 245.4 / 274.8 / 274.8 (7) | 61.9 | 59.4 → 69.0 | 217.4 → 314.4 | 106,471,864 → 139,936,656 | 9,484,992 → 9,151,368 (0.965) |
| encoding-max | 103.3 / 133.5 / 133.5 (7) | 116.6 / 146.9 / 146.9 (7) | 28.0 | 38.2 → 33.7 | 141.5 → 150.3 | 56,639,688 → 78,378,912 | 5,271,056 → 4,805,496 (0.912) |
| rush-957x2 | 101.7 / 140.9 / 140.9 (7) | 137.9 / 162.1 / 162.1 (7) | 28.6 | 37.4 → 42.7 | 139.1 → 180.7 | 54,318,400 → 75,730,976 | 4,811,248 → 4,472,368 (0.930) |
| representative-5000x2 | 424.7 / 460.6 / 460.6 (7) | 559.7 / 601.6 / 601.6 (7) | 180.4 | 126.1 → 125.8 | 550.8 → 685.5 | 287,796,392 → 395,097,840 | 26,201,504 → 24,461,632 (0.934) |
| fresh-5000x3 | 533.0 / 590.5 / 590.5 (7) | 724.0 / 776.3 / 776.3 (7) | 218.2 | 180.6 → 167.8 | 713.6 → 891.7 | 403,100,040 → 546,574,848 | 36,305,616 → 34,561,712 (0.952) |
| rush-5000x2 | 431.7 / 439.7 / 439.7 (7) | 551.5 / 577.5 / 577.5 (7) | 168.8 | 133.3 → 132.0 | 565.0 → 683.5 | 283,833,672 → 394,649,920 | 25,202,592 → 23,436,304 (0.930) |
| representative-50000x2 | 3,874.9 / 6,184.3 / 6,184.3 (7) | 5,449.8 / 8,012.3 / 8,012.3 (7) | 3,652.0 | 1,230.8 → 1,264.7 | 5,105.7 → 6,714.5 | 2,638,759,960 → 3,608,515,184 | 236,054,568 → 222,303,720 (0.942) |
| fresh-50000x3 | 5,256.7 / 5,355.2 / 5,355.2 (7) | 7,082.5 / 7,187.5 / 7,187.5 (7) | 2,303.1 | 1,507.4 → 1,598.7 | 6,764.2 → 8,681.1 | 3,749,114,632 → 5,094,090,360 | 332,730,384 → 318,683,136 (0.958) |


## Against the section 3.1 budgets (writer 34; all synthetic)

| Budget | Measured | Result |
| --- | --- | --- |
| Typical bytes ≤ 35 % of the predecessor at representative 957×2, ≤ 6 Mi characters | 849,572 characters, 34.58 % of writer 33 | **met** |
| Same at the F5 declared load (2,871 persons) | fresh-957x3: 1,152,633 characters, 37.04 % | **not met** by 2.04 points (≤ 6 Mi met) |
| Stress bytes ≤ 40 % and ≤ 64 Mi characters | representative 50,000×2: 30.05 %, 35,100,344; fresh 50,000×3: 31.96 %, 47,812,181 | **met** |
| UTF-16 bytes in the native save | 2 × characters (table above) | reported |
| Records under the 4,000,000 cap with tables charged | max 901,732 (fresh 50,000×3) | **met** |
| Typical synchronous pause ≤ 40 ms p95 | 171.4 ms (representative 957×2), 191.3 ms (fresh-957x3) p95; writer 33 191.1 and 64.4 | **not met** |
| Stress pause ≤ 600 ms | 4,058.7 ms p50 at representative 50,000×2 (writer 33 1,828.1) | **not met** |
| Allocation ≤ 2.5 × payload UTF-16 bytes | 22.6–29.7 × the compact payload's UTF-16 bytes; 7.8–9.3 × the predecessor's | **not met** |
| Retained ≤ predecessor; intern-on-decode ≤ 80 % | 0.912–0.965 of writer 33 after one read | no regression **met**; ≤ 80 % **not met** |
| Load (read + activation) ≤ 400 ms typical | 164.3 ms (957×2) and 214.8 ms (957×3) p50 | **met** |
| Load ≤ 2,500 ms stress | 6,714.5 ms p50 at representative 50,000×2 (writer 33 5,105.7) | **not met** |
| Ordinary-play mod-work added | none: F7d runs only inside the save callback and the off-thread read | **met** (0 per-frame work) |

The invariants win over these budgets, as the plan says. The gaps are the ones the decision record named as open risks, now measured on the real writer:

- **Pause.** The compact writer roughly doubles to triples the save pause at 957 and 5,000 residents and adds 2.2 s at representative 50,000×2. The split: the rest of the encode phase is serializing the snapshot into the exact token tree, by subtraction 0.6–2.5 × writer 33's text serialization across the fixtures (noisy host), and the compact pass adds a table pass (5–24 ms at 957–5,000, 561 ms at 50,000×2) and a column writer (15–74 ms, 2,187 ms). F7e's overlap window (plan section 3.5) is the planned answer for the main-thread share; it is not built here.
- **Allocation.** The token tree, the per-column lists and the route content strings allocate about 1.5 × what writer 33's save allocated, against a much smaller payload, so the ratio to the compact payload is 23–30 ×.
- **Stress load.** The reader decodes the columns, writes the body text back and runs the strict body reader on it (two token passes), so a compact read costs the writer-33 read plus decode and body text (32 ms at 957×2, 3.7 s at 50,000×2). This keeps every body check exactly as strict as before; a columnar body reader that skips the text is a later choice.
- **Interning.** One string instance per distinct text is reached for every plain string field (957 home-ID instances → 2 at rush-957x2), but most saved text lives in tuple and image strings the body converters build, so retained memory falls by 4–9 %, not 20 %.
- **F5 declared load bytes.** At 2,871 persons the payload is 37.0 % of writer 33; its visitor routines are positional tuples whose slots compress less than resident objects.

Not run in this slice: the E5d saturated stress set (50,000×2/×3 saturated, CF32, CF64), whose builders are not in the unit project. F7p measured candidate C on them at 20.9–21.9 % of writer 25 with records ≤ 3,775,940; the writer-34 caps and record charge are tested above, and rig check 5 measures a fresh 50,000-resident city.

## ≥ 30 ms flags

Every save sample at or above 30 ms is flagged. Writer 34: 7 of 7 samples on every fixture. Writer 33: 7 of 7 on every fixture in this run as well (host load). Every read sample is ≥ 30 ms on both writers. These are save and load latency samples, a separate criterion from ordinary-play mod-work; F7d adds no per-frame work, so mod-work p95 < 5 ms and whole-frame metrics are unaffected and tracked separately.

## Perf win taken (before/after)

Encoding first wrote the body as JSON text and parsed it back into the tree. Serializing the snapshot straight into the exact tree with a token-tree `JsonWriter` (same serializer, settings and converters; refuses raw JSON and token kinds the body never writes) removed the body string and its parse. Same build, same fixtures, 7 samples each:

| Fixture | Allocation per save before → after | Pause p50 before → after |
| --- | --- | --- |
| representative-957x2 | 57,624,360 → 40,514,688 B | 121.8 → 116.5 ms |
| fresh-957x3 | 77,844,640 → 56,405,992 B | 108.8 → 99.3 ms |
| saturated-957x3 | 104,134,432 → 74,080,576 B | 131.5 → 131.3 ms |
| rush-957x2 | 58,010,800 → 41,298,768 B | 81.6 → 74.6 ms |

Bytes and hashes are unchanged by it (the body-equality and golden tests pass on both). The four parked hotspots stay parked; save serialization is F7's own scope.
