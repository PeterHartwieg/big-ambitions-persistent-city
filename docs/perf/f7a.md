# City audit F7a: writer-25 save-cost baseline (4 November 2026, offline)

Refs #94. Base `dc59049` (writer **25**); measured at `edf4949`. Plan: [F7 section 4, row F7a](../plans/f7-save-snapshots.md#4-slice-breakdown); [checks and numbered rig list](../checks/f7a.md). This is a **baseline**. F7a changes no serialization, so it has no before/after pair, and no performance win was taken (see the end of this record). Save and load are their own latency criterion. They are never ordinary-play mod-work and never count toward the mod-work p95 gate.

## Method

Run `python tools/save-cost/measure.py 7` (or one fixture at a time: `python tools/save-cost/measure.py 7 <fixture>`). Every fixture runs in its own foreground `SaveCost` process after the shared other-dotnet wait, so peak working set belongs to that fixture. Windows 11, .NET SDK 10.0.402, Release, workstation GC. Raw rows: [baseline.jsonl](assets/f7a/baseline.jsonl), [determinism.jsonl](assets/f7a/determinism.jsonl), [metadata.json](assets/f7a/metadata.json). Metrics only; no payload is committed.

- **Representative** (`representative-NxF`): the F3 integrated replay of the actual Core scheduler (`EconomyScenario` seed 23, two consumers per resident, household finances, work/leisure routines, detached checkout, deterministic walking), from 05:00 through all meal peaks and the next midnight (minute 2,910). It replays one event per slice until caught up, independent of wall-clock time. Final hashes equal F3's retained `saveSha256` at all three sizes.
- **Stress** (E5d matrix): rebuilt with E5d's own helpers (`tools/name-identity-regression`, compiled in place, not edited) and migrated 24 → 25 as E5d does. Hashes equal E5d's retained `afterSha256` for every case E5d recorded (957/5,000/50,000 × 2/3, CF32, CF64). E5d did not retain a `churn` hash.
- Per save: one discarded warm-up, then seven alternating diagnostics-off/on `SaveSession.WriteTo` calls on a session bound to the live dictionary (the runtime's trusted publish path). Each on/off payload is checked byte-identical, every phase covered, phase ticks summed to the measured span, and payload size equal to the record. p95 at seven samples is the second-largest-or-max order statistic, not a native estimate.
- Load: seven strict `SaveCodec.Read` calls (background in runtime), then `ValidateForActivation` (background), then `new CitySimulator` (the main-thread construction inside `CompleteInitialization`; food/route attachment and Unity work are not included). Retained is the post-GC difference for one live decoded graph.
- `observe` is runtime-only (RLO read, `BeforeSave`, campaign completion, ownership refresh, epoch check). Offline it is the call entry, ≈0.001 ms, and is omitted below.

## Payload size and records (writer 25)

| Fixture | Residents / consumers | Characters = UTF-8 bytes | UTF-16 storage bytes | Streaming / semantic records |
| --- | ---: | ---: | ---: | ---: |
| representative-957x2 | 957 / 1,914 | 2,333,388 | 4,666,776 | 11,312 / 11,309 |
| representative-5000x2 | 5,000 / 10,000 | 12,193,195 | 24,386,390 | 59,279 / 59,276 |
| representative-50000x2 | 50,000 / 100,000 | 109,762,853 | 219,525,706 | 561,915 / 561,912 |
| 957x2 | 957 / 1,914 | 17,751,674 | 35,503,348 | 185,067 / 184,843 |
| 957x3 | 957 / 2,871 | 18,425,694 | 36,851,388 | 186,981 / 186,757 |
| 5000x2 | 5,000 / 10,000 | 30,319,583 | 60,639,166 | 334,658 / 334,434 |
| 5000x3 | 5,000 / 15,000 | 33,841,097 | 67,682,194 | 344,658 / 344,434 |
| churn | 957 / 1,914 | 30,126,240 | 60,252,480 | 314,091 / 313,867 |
| 50000x2 | 50,000 / 100,000 | 162,346,836 | 324,693,672 | 1,999,658 / 1,999,434 |
| 50000x3 | 50,000 / 150,000 | 196,758,738 | 393,517,476 | 2,099,658 / 2,099,434 |
| CF32 | 50,000 / 150,000 | 201,833,742 | 403,667,484 | 2,106,602 / 2,099,434 |
| CF64 | 50,000 / 150,000 | 240,233,742 | 480,467,484 | 3,706,602 / 3,699,434 |

All payloads are ASCII, so characters equal UTF-8 bytes. UTF-16 storage is 2 × characters (Odin stores the string as UTF-16). The F7 section 3.1 typical byte target ("≤ 35 % of F7a's measured writer-25 representative bytes") therefore resolves to **≤ 816,686 characters** at representative 957×2. The stress target stays ≤ 64 Mi characters against 162,346,836 at 50000x2.

## Save phases (synthetic, ms; diagnostics off unless stated)

| Fixture | Synchronize p50 / p95 | Validate p50 / p95 | Encode p50 / p95 | Publish p50 / p95 | Total off p50 / p95 / max | Total on p50 / p95 / max | ≥30 ms (off) | Allocated B / save | ÷ UTF-16 bytes |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| representative-957x2 | 1.519 / 6.904 | 6.163 / 13.904 | 27.396 / 99.594 | 0.005 / 0.007 | 46.10 / 87.19 / 87.19 | 35.14 / 109.32 / 109.32 | 7/7 | 17,355,776 | 3.72 |
| representative-5000x2 | 2.975 / 5.660 | 8.440 / 28.998 | 98.351 / 113.433 | 0.003 / 0.005 | 114.48 / 222.67 / 222.67 | 109.77 / 147.86 / 147.86 | 7/7 | 90,511,840 | 3.71 |
| representative-50000x2 | 34.332 / 45.236 | 182.943 / 219.282 | 1,214.133 / 1,308.707 | 0.005 / 0.006 | 1,217.18 / 1,668.38 / 1,668.38 | 1,428.48 / 1,542.49 / 1,542.49 | 7/7 | 812,227,576 | 3.70 |
| 957x2 | 5.798 / 16.239 | 38.296 / 48.344 | 215.986 / 269.874 | 0.004 / 0.011 | 275.91 / 331.05 / 331.05 | 258.25 / 313.32 / 313.32 | 7/7 | 156,861,200 | 4.42 |
| 957x3 | 6.329 / 10.376 | 40.572 / 53.861 | 241.806 / 250.746 | 0.005 / 0.011 | 259.96 / 302.08 / 302.08 | 291.29 / 310.65 / 310.65 | 7/7 | 163,683,448 | 4.44 |
| 5000x2 | 7.001 / 19.164 | 58.197 / 86.897 | 362.397 / 491.131 | 0.004 / 0.006 | 465.87 / 739.17 / 739.17 | 426.00 / 585.04 / 585.04 | 7/7 | 277,319,048 | 4.57 |
| 5000x3 | 8.827 / 16.562 | 50.882 / 78.722 | 395.605 / 462.475 | 0.004 / 0.005 | 418.12 / 550.14 / 550.14 | 452.56 / 528.40 / 528.40 | 7/7 | 312,490,752 | 4.62 |
| churn | 11.089 / 27.658 | 57.609 / 87.333 | 340.100 / 383.966 | 0.004 / 0.006 | 424.94 / 508.21 / 508.21 | 420.30 / 461.71 / 461.71 | 7/7 | 258,038,424 | 4.28 |
| 50000x2 | 27.799 / 37.773 | 212.551 / 231.020 | 1,815.569 / 2,005.877 | 0.004 / 0.005 | 2,036.83 / 2,293.39 / 2,293.39 | 2,071.80 / 2,270.83 / 2,270.83 | 7/7 | 1,567,748,152 | 4.83 |
| 50000x3 | 43.544 / 47.880 | 181.738 / 296.123 | 2,149.265 / 2,234.713 | 0.004 / 0.004 | 2,437.78 / 2,621.31 / 2,621.31 | 2,364.16 / 2,578.31 / 2,578.31 | 7/7 | 1,914,058,376 | 4.86 |
| CF32 | 35.576 / 57.119 | 216.405 / 256.797 | 2,327.486 / 2,446.962 | 0.004 / 0.005 | 2,354.28 / 2,586.99 / 2,586.99 | 2,559.39 / 2,739.33 / 2,739.33 | 7/7 | 1,945,648,000 | 4.82 |
| CF64 | 37.383 / 64.123 | 238.152 / 261.928 | 2,651.056 / 2,781.066 | 0.004 / 0.004 | 2,676.85 / 3,109.63 / 3,109.63 | 2,937.21 / 3,043.62 / 3,043.62 | 7/7 | 2,434,690,688 | 5.07 |

**Flagged: every one of the 84 measured save samples (off arm) is ≥30 ms**, including all seven at representative 957×2 (p50 46.10 ms, max 87.19 ms). These are save samples, a separate latency criterion. They are not ordinary-play mod-work spikes. Phase columns come from the diagnostics-on arm, whose order statistics are taken independently, so phase p95s need not sum to total p95.

Observations, all synthetic: encode (Newtonsoft serialization plus both size checks) is the largest phase at every fixture, 78–91 % of the p50 phase sum. Validate is next. Synchronize (`PrepareForSave`, outflow/name preparation) is ≤ 5 % at p50. Publish on the trusted path is ≈ 0.005 ms. Allocation per save is 3.7× the UTF-16 payload bytes on representative fixtures and 4.3–5.1× on stress fixtures, against the section 3.1 F7d target of ≤ 2.5×. **Diagnostics on vs off:** both arms vary in both directions by host noise (for example 46.10 → 35.14 ms p50 at 957×2, 1,217 → 1,428 ms at 50,000×2). The recorder itself allocates 0 B per cycle (unit test), and the payload bytes are identical in every sample.

## Load (synthetic, ms)

| Fixture | Background read p50 / p95 / max | Read allocated B | Background activation validate p50 / p95 | Main-thread simulator construction p50 / p95 / max | Retained MiB | Peak working set MiB |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| representative-957x2 | 205.22 / 375.18 / 375.18 | 40,395,336 | 55.75 / 82.34 | 0.406 / 0.592 / 0.592 | 4.124 | 176 |
| representative-5000x2 | 373.30 / 503.57 / 503.57 | 210,857,168 | 106.02 / 138.31 | 1.361 / 1.855 / 1.855 | 21.620 | 400 |
| representative-50000x2 | 4,446.40 / 5,061.45 / 5,061.45 | 1,904,021,512 | 1,085.19 / 1,271.70 | 25.692 / **32.221** / **32.221** | 189.382 | 2,421 |
| 957x2 | 642.89 / 679.62 / 679.62 | 292,004,328 | 226.81 / 353.25 | 0.262 / 0.470 / 0.470 | 37.156 | 614 |
| 957x3 | 672.23 / 735.96 / 735.96 | 302,759,080 | 257.71 / 308.21 | 0.316 / 0.466 / 0.466 | 39.004 | 621 |
| 5000x2 | 1,164.85 / 1,315.64 / 1,315.64 | 511,044,416 | 434.08 / 489.45 | 1.069 / 1.951 / 1.951 | 62.155 | 948 |
| 5000x3 | 1,173.51 / 1,299.86 / 1,299.86 | 566,644,856 | 420.34 / 476.18 | 1.020 / 1.706 / 1.706 | 71.730 | 1,050 |
| churn | 1,014.82 / 1,119.91 / 1,119.91 | 489,512,152 | 363.75 / 425.31 | 0.227 / 0.374 / 0.374 | 63.201 | 877 |
| 50000x2 | 4,987.06 / 5,269.73 / 5,269.73 | 2,817,684,504 | 1,728.65 / 1,886.49 | 13.410 / 17.218 / 17.218 | 326.560 | 4,599 |
| 50000x3 | 6,467.21 / 7,139.19 / 7,139.19 | 3,365,898,912 | 2,066.61 / 2,278.16 | 16.208 / 26.424 / 26.424 | 421.439 | 4,895 |
| CF32 | 6,451.73 / 6,651.21 / 6,651.21 | 3,443,412,000 | 2,162.82 / 2,266.09 | 15.683 / 16.847 / 16.847 | 429.784 | 5,314 |
| CF64 | 7,418.91 / 8,060.67 / 8,060.67 | 4,253,330,792 | 2,672.97 / 2,965.23 | 20.761 / 24.470 / 24.470 | 503.034 | 6,110 |

**Flagged:** every background read and activation-validate sample is ≥30 ms. Both run off the main thread while the calendar is held (F1), so they are not frame work. **Main-thread construction reached ≥30 ms in 2 of 7 samples at representative 50,000×2 (max 32.221 ms).** That is startup latency on the stress population, recorded against the section 3.1 "native ≤ 50 ms main-thread completion" budget. It is not ordinary-play mod-work. Retained memory at 957x2/5000x2/50000x2 equals E5d's records exactly (37.156 / 62.155 / 326.560 MiB).

## Representativeness and limits

- The representative fixtures carry natural one-day histories (avg 6.9 visits per resident at 957, one shop-history row; three scopes enrolled, of which the detached market serves one; 5,077 purchases at 957×2). They have **9 places and no traffic definitions** (the replay uses walking routes, so no road graph is saved). The October native archive had 9,093 traffic links (2.67 M UTF-16 code units) and many more places. The representative byte and pause figures therefore **understate a native city** by at least the traffic/place share. The native representative checkpoint (`pc-representative-957@1`, rig checks 2–4 in the [checks file](../checks/f7a.md)) closes that gap.
- No fixture invents visitor identity: every consumer belongs to a saved resident (F5 contract B6). The F5 declared population is added once F5 declares it.
- .NET 10 CoreCLR on a shared development PC. Mono/Unity timings, native `SerializeSaveGame`, portrait, compression and whole-frame effects are not inferred. Sizes are sequential (one process each); host noise is visible in the on/off arms.
- `synchronize` here is today's `PrepareForSave` path. It is not F7c's snapshot capture, so the F7e precondition (capture ≤ 25 % of encode) is for F7c to measure, not this record.

## Perf wins

No win was taken. The code F7a adds runs only inside an explicitly requested save record or once per load. It does no per-frame work, reflection, polling or scans, and it allocates nothing on ordinary saves (null recorder) or per recorder cycle (0 B over 1,000 cycles, unit test). Its one optional extra cost, record counting, runs only when requested and is timed separately (`recordCountMs`, outside the phase timers). Encode dominates the pause, but save serialization is a parked hotspot that F7c/F7d own, so F7a leaves it unchanged. No cheaper low-risk variant was found in the touched lines; `SaveCodec.WriteForSchema` already computed the UTF-8 byte count exactly once, and F7a reuses that value for the record. The other three parked hotspots (physical population, closure refresh, optional-stop refresh) are untouched.
