# F7c: save snapshot split, measured offline (7 November 2026)

Refs #94. Plan: [F7 section 4, row F7c](../plans/f7-save-snapshots.md#4-slice-breakdown) and section 6. Checks: [docs/checks/f7c.md](../checks/f7c.md). Base `57607b2`, writer 29. F7c changes no schema and no bytes.

All numbers are synthetic: .NET 10 Release on this Mac (arm64), one dotnet process at a time, foreground. They are not game, Mono or rig evidence. Save and load samples are the save latency criterion. They never count toward ordinary-play mod-work.

## Method

- `tests/F7cMeasure.cs` (opt-in, `PC_F7C_MEASURE=out.jsonl dotnet test tests/PersistentCity.Tests.csproj -c Release --filter FullyQualifiedName~F7cMeasure`). It calls only API that exists on both commits. I copied it with `tests/F7cFixtures.cs` into a detached worktree of `57607b2` and ran the same file there for the before column. Per fixture it records the payload SHA-256 and sizes, 7 timed `SaveCodec.Write` calls after one warm-up with every phase the recorder knows, allocation per write, the live city's retained bytes from a separate identical build, and retained bytes after a strict read.
- `tests/F7cSnapshotMeasure.cs` (opt-in, `PC_F7C_SNAPSHOT_MEASURE`), after only. It times `SaveCodec.Capture` (synchronize plus copy), `SaveCodec.Encode` (validate plus encode from the snapshot) and the copy alone, with their allocations and the shared and copied route counts.
- `tools/bounded-bulk-regression`, default mode (the F3 `StepBudget` fixture) and `--integrated` (the F3 integrated replay), on both commits.
- Fixtures are fresh cities in the current schema, listed in `tests/F7cFixtures.cs`: the F7a representative replay at 957, 5,000 and 50,000 residents (factor 2), the E5d size matrix at factor 3, two saturated 957-resident cities with 64 recent visits per resident, an escaping city with 240 non-ASCII 256-character product names, and two morning-rush cities saved at 08:20 with journeys in flight. The representative save at 00:30 has no journeys, so only the rush cities exercise route sharing.

The first fixture in each process (representative 957x2) includes JIT warm-up in both columns, which is why its write time is several times the other 957-resident cities.

## Byte identity

Every fixture writes the same bytes on both commits. The F3 integrated replay's final save hashes also match on both commits at all three sizes.

| Fixture | Characters | UTF-8 bytes | SHA-256 (before = after) |
| --- | ---: | ---: | --- |
| representative-957x2 | 2,311,290 | 2,311,290 | `D1CC4FB4EE3F3135AECC0A032FD63AA5257B9BC1739BB76FC98B807113EEE1AF` |
| fresh-957x3 | 2,900,486 | 2,900,486 | `A6DB1093D09D7B0540B56220C5F51F64027DAED008CE0BAB995829B071BAF0E9` |
| saturated-957x2 | 3,622,362 | 3,622,362 | `D06535BAACA65F90F7B8299A0A50C9761A858B48BAAC7373D0872D0D9570B9C1` |
| saturated-957x3 | 4,211,558 | 4,211,558 | `F32DC000A40718334FFACE8F5174E9004161DF7F07D7319A91DF15B18D7A901C` |
| encoding-max | 2,445,781 | 2,568,661 | `D222381479282F55F8B0D26A78760E915A676A0BA43593D70367B67A8424696C` |
| rush-957x2 | 2,213,842 | 2,213,842 | `8F6473A4AD620627E367852AE6AD122146B28185FB2054A71CC188B8B5A11A25` |
| representative-5000x2 | 12,077,305 | 12,077,305 | `7E15285FBC4C6F451DEA26C5324C2AA61EF3048B4B6F9D2AE33A2A0DCCC87C91` |
| fresh-5000x3 | 15,157,090 | 15,157,090 | `7DE3EE45CD9567AE0E373ECC18EAE4287AB726552236A504D07C6506411655B2` |
| rush-5000x2 | 11,576,630 | 11,576,630 | `0C6A0E167DE1BB74FA17F34015EFAFB87758451C78670070D15A1B2996CE982E` |
| representative-50000x2 | 109,878,666 | 109,878,666 | `61DCC602416476A1F2E56C14B26B6198868F046D35B6CBC6033ACCE0EBE7450C` |
| fresh-50000x3 | 139,194,982 | 139,194,982 | `4CD424D9AE460C6C192A2B870A8D786A68473BBFF1F56D72324A97D9722478C3` |

## Save pause: before and after

`SaveCodec.Write`, p50 / p95 / max over 7 samples, then the phase p50s. Before has no capture phase; validation and encoding read the live graph.

| Fixture | Write before ms | Write after ms | Before: sync / validate / encode | After: sync / capture / validate / encode |
| --- | --- | --- | --- | --- |
| representative-957x2 | 138.30 / 150.76 / 150.76 | 145.27 / 160.88 / 160.88 | 1.88 / 11.53 / 124.98 | 2.04 / 4.47 / 13.30 / 123.04 |
| fresh-957x3 | 37.32 / 41.59 / 41.59 | 39.48 / 49.20 / 49.20 | 2.20 / 11.39 / 24.16 | 0.73 / 4.71 / 7.79 / 25.91 |
| saturated-957x2 | 38.28 / 44.20 / 44.20 | 51.58 / 52.59 / 52.59 | 0.59 / 5.60 / 31.72 | 0.56 / 2.97 / 8.81 / 36.81 |
| saturated-957x3 | 49.21 / 84.11 / 84.11 | 59.59 / 115.59 / 115.59 | 0.75 / 12.14 / 36.36 | 0.76 / 3.28 / 14.39 / 39.94 |
| encoding-max | 25.28 / 27.37 / 27.37 | 29.04 / 37.24 / 37.24 | 0.75 / 4.79 / 19.52 | 0.69 / 2.16 / 5.21 / 20.56 |
| rush-957x2 | 24.16 / 26.35 / 26.35 | 27.27 / 34.32 / 34.32 | 0.42 / 3.92 / 19.76 | 0.44 / 3.41 / 4.97 / 18.49 |
| representative-5000x2 | 125.29 / 163.07 / 163.07 | 161.36 / 168.50 / 168.50 | 2.49 / 17.94 / 103.52 | 2.76 / 15.35 / 25.62 / 115.93 |
| fresh-5000x3 | 166.20 / 169.72 / 169.72 | 222.12 / 256.84 / 256.84 | 3.78 / 27.98 / 134.37 | 3.75 / 31.56 / 40.98 / 142.28 |
| rush-5000x2 | 123.67 / 165.36 / 165.36 | 164.91 / 167.47 / 167.47 | 1.98 / 18.17 / 104.04 | 2.06 / 29.94 / 19.72 / 113.03 |
| representative-50000x2 | 1,355.78 / 1,408.42 / 1,408.42 | 1,551.69 / 1,971.62 / 1,971.62 | 25.55 / 246.14 / 1,083.22 | 25.67 / 223.78 / 256.65 / 995.86 |
| fresh-50000x3 | 1,733.05 / 1,789.51 / 1,789.51 | 1,984.43 / 2,569.73 / 2,569.73 | 33.41 / 357.63 / 1,338.51 | 31.71 / 315.01 / 361.10 / 1,235.48 |

The split makes saves slower. The p50 save pause grows by 5% to 34%: 13% at rush-957x2, 14% at representative 50,000x2, 15% at 50,000x3. Most of it is the new capture phase. Validation also runs about 10% slower on a freshly copied graph than on the warm live graph. A scratch run validated a snapshot that had been warmed by one earlier pass at the live graph's speed, so I put the gap down to the young copy, not to a different code path. F7c's job is the split itself. The planned save win is F7d's compact encoding, which encodes from this snapshot.

Samples at or above 30 ms, all flagged as save samples: every 957x2 representative, 957x3, saturated, 5,000 and 50,000 sample on both commits (7 of 7). New flags after F7c: encoding-max 0 → 3 of 7 and rush-957x2 0 → 2 of 7.

## Capture against encode (after)

From `F7cSnapshotMeasure`, p50 over 7 samples. "Validate + encode" is `SaveCodec.Encode` on the snapshot. The copy alone leaves out synchronize.

| Fixture | Capture ms p50 / p95 / max | Copy only ms | Validate + encode ms | Capture / (validate + encode) | Capture allocation B | Encode allocation B | Shared / copied routes |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| representative-957x2 | 6.18 / 10.14 / 10.14 | 4.05 | 136.55 | 0.045 | 3,935,016 | 19,945,984 | 0 / 0 |
| fresh-957x3 | 3.28 / 4.67 / 4.67 | 3.71 | 40.10 | 0.082 | 5,568,912 | 26,400,704 | 0 / 0 |
| saturated-957x2 | 4.88 / 10.40 / 10.40 | 2.48 | 44.31 | 0.110 | 6,557,160 | 37,877,432 | 0 / 0 |
| saturated-957x3 | 9.42 / 22.36 / 22.36 | 4.01 | 56.50 | 0.167 | 8,191,056 | 44,333,248 | 0 / 0 |
| encoding-max | 3.80 / 6.96 / 6.96 | 2.13 | 29.08 | 0.131 | 3,961,560 | 20,869,712 | 0 / 0 |
| rush-957x2 | 2.54 / 6.13 / 6.13 | 2.64 | 28.01 | 0.091 | 3,870,512 | 19,365,528 | 220 / 0 |
| representative-5000x2 | 23.17 / 47.32 / 47.32 | 18.86 | 143.54 | 0.161 | 20,366,184 | 104,509,504 | 0 / 0 |
| fresh-5000x3 | 34.84 / 46.60 / 46.60 | 30.97 | 177.78 | 0.196 | 29,894,376 | 137,407,072 | 0 / 0 |
| rush-5000x2 | 23.26 / 36.10 / 36.10 | 18.86 | 142.85 | 0.163 | 20,035,048 | 101,557,312 | 1,156 / 0 |
| representative-50000x2 | 252.22 / 487.07 / 487.07 | 218.50 | 1,307.05 | 0.193 | 197,505,128 | 955,940,912 | 0 / 0 |
| fresh-50000x3 | 356.70 / 741.96 / 741.96 | 325.88 | 1,599.18 | 0.223 | 289,981,864 | 1,266,509,920 | 0 / 0 |

Capture is 4.5% to 22.3% of validate plus encode on every fixture. That meets F7e's offline half of the condition in plan section 3.5, at most 25% of encode. F7e also needs the native portrait and screenshot window, which only the rig can measure. Capture samples at or above 30 ms: representative-5000x2 2 of 7, fresh-5000x3 5 of 7, rush-5000x2 2 of 7, both 50,000 fixtures 7 of 7. All are save samples.

Every rush journey was committed by the simulator, so the snapshot shares all of them and copies none. A city just loaded from a save copies its journeys until the simulator replaces them. The unit test `CommittedJourneysAreFrozenAndSharedWhileLoadedRoutesAreCopied` checks both cases.

## Allocation per save

| Fixture | Before B | After B | Before / after per UTF-16 byte |
| --- | ---: | ---: | --- |
| rush-957x2 | 19,366,320 | 23,236,216 | 4.374 → 5.248 |
| saturated-957x3 | 44,333,704 | 52,524,520 | 5.263 → 6.236 |
| representative-5000x2 | 104,508,968 | 124,874,912 | 4.327 → 5.170 |
| fresh-5000x3 | 137,407,488 | 167,302,424 | 4.533 → 5.519 |
| representative-50000x2 | 955,941,352 | 1,153,446,528 | 4.350 → 5.249 |
| fresh-50000x3 | 1,266,510,176 | 1,556,492,264 | 4.549 → 5.591 |

The snapshot adds 16% to 23% allocation per save, about the size of the live graph's wire-carried part. Plan section 3.1's 2.5x allocation budget is F7d's to meet.

## Retained memory

| Fixture | Live city before B | Live city after B | After strict read before B | After strict read after B |
| --- | ---: | ---: | ---: | ---: |
| rush-957x2 | 4,538,560 | 4,545,864 | 9,905,904 | 9,914,888 |
| rush-5000x2 | 43,565,224 | 43,597,120 | 51,236,888 | 51,483,728 |
| fresh-5000x3 | 62,436,536 | 62,432,920 | 75,363,240 | 75,386,000 |
| representative-50000x2 | 225,140,632 | 225,133,624 | 449,466,728 | 448,636,136 |
| fresh-50000x3 | 323,917,464 | 324,080,080 | 465,504,368 | 464,896,816 |

The plan expected a small retained win from immutable routes. There is none. A committed route now carries its frozen leg count and total minutes, and each leg a frozen flag. That costs about 24 to 33 bytes per one-leg journey: rush-957x2 +7,304 B for 220 journeys, rush-5000x2 +31,896 B for 1,156. The 00:30 fixtures have no journeys, and their differences are within run noise. The 957x2, 957x3 and saturated live-city rows (not shown) moved by up to 3 MB between runs of the same commit, so I don't report them as a change. What immutable routes do buy is a save that shares each journey instead of copying its legs, and a `Route.Minutes` that no longer sums the legs on every read.

## Ordinary play

F7c adds no per-frame work. `Route.Commit` runs once per committed journey, and `Route.Minutes` adds one count comparison.

F3 `StepBudget` fixture (`tools/bounded-bulk-regression`, 7 days after one discarded day), call ms median / p95 / max, and median allocation:

| Consumers | Before | After | Median B before → after |
| --- | --- | --- | --- |
| 1,914 | 0.0302 / 0.0390 / 0.1170 | 0.0305 / 0.0400 / 0.0750 | 0 → 0 |
| 10,000 | 0.0327 / 0.0378 / 0.1265 | 0.0325 / 0.0369 / 0.0717 | 0 → 0 |
| 100,000 | 0.0071 / 0.0105 / 0.1632 | 0.0072 / 0.0110 / 0.0617 | 0 → 0 |

No regression, and no call reaches 30 ms.

F3 integrated replay (`--integrated`, two runs per commit), frame advancement ms median / p95 / max and total allocation:

| Consumers | Before run 1 | Before run 2 | After run 1 | After run 2 | Total allocation before → after |
| --- | --- | --- | --- | --- | --- |
| 1,914 | 0.0457 / 0.1237 / 21.63 | 0.0460 / 0.1232 / 19.07 | 0.0465 / 0.1235 / 16.76 | 0.0460 / 0.1315 / 16.72 | 91,235,744 → 91,652,192 |
| 10,000 | 0.0241 / 0.1044 / 8.06 | 0.0249 / 0.1015 / 9.28 | 0.0255 / 0.1047 / 8.56 | 0.0249 / 0.1012 / 9.00 | 470,867,576 → 473,044,760 |
| 100,000 | 0.0314 / 0.0816 / 22.24 | 0.0315 / 0.0811 / 24.47 | 0.0306 / 0.0810 / **35.43** | 0.0307 / 0.0820 / 24.13 | 4,086,553,944 → 4,105,209,208 |

Frame p95 is unchanged and far below 5 ms. **Flag:** after run 1 had one 35.43 ms frame at 100,000 consumers, the only sample at or above 30 ms in either commit's runs. Run 2 of the same build had none (max 24.13 ms), and the base runs reached 22.24 and 24.47 ms. Its frame allocation maximum matches the base, and I read it as a host or GC pause, but one rerun does not prove that. The rig's ordinary windows measure it natively. Total allocation rises 0.46%, about 18 B per planned route, from the two new route fields and the leg flag.

## Perf change taken

The per-record copies first used method groups and lambdas that call instance members. Language version 9 allocates a new delegate for each of those on every use, so every place, citizen and decision shop paid one. Binding them once per capture cut capture allocation by about 3% on every fixture. Same fixtures, same process layout, copy-only path:

| Fixture | Before B | After B |
| --- | ---: | ---: |
| rush-957x2 | 3,994,000 | 3,870,808 |
| saturated-957x3 | 8,375,496 | 8,191,056 |
| rush-5000x2 | 20,676,040 | 20,035,344 |
| fresh-5000x3 | 30,855,368 | 29,894,672 |
| representative-50000x2 | 203,905,824 | 197,505,128 |

Capture time moved within run noise, so I claim only the allocation change.

## Hotspots

The save-serialization hotspot stays parked. F7c adds the capture cost above and takes no encoder change. I found no cheap, safe change in the code I touched that would pay it back without touching the encoder or validation. The other three parked hotspots (physical population, closure refresh, optional-stop refresh) are untouched.

## Gate

Mod-work p95 stays under 5 ms: StepBudget p95 is at most 0.011 ms and integrated-replay frame p95 at most 0.1315 ms. One ordinary-play frame of 35.43 ms is flagged above. Save samples are their own criterion, and every one at or above 30 ms is flagged in the tables. Whole-frame timing is not measured offline.
