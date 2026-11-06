# City audit F6c — feedback v1 performance, 4 November 2026, offline

## Roadmap perf-log entry (moved 6 Nov 2026)

- City-audit F6c — [Incremental panel counters and one-pass shop lookups](f6c.md): open-panel consumer work p95 1.1088/3.4898/32.1975 -> 0.0026/0.0050/0.0030 ms and 743,240/3,863,008/39,248,344 -> 6,280/6,456/7,768 B/request at 1,914/10,000/100,000 consumers; per-shop lookups p95 2.7575/116.1171 -> 0.5459/14.5748 ms at 64/1,024 shops including the new week text; 128-bit counters and tuple keys cut the 1,024-shop build 8,109,056 -> 4,525,584 B; hidden panel 0 B. Cost: +0.0006–0.0163 ms p95 per posting call, 0 B. Offline components, no whole-frame claim.

Refs #93. Measurements named by the F6c row of [the F6 plan](../plans/f6-economy.md#4-slice-breakdown) and its section 6 table: panel build cost at 1 / 64 / 1,024 shops, before/after scan cost at 1,914 / 10,000 / 100,000 consumers, and hidden-panel cost (0 B, no calls). Standing gate: mod-work p95 < 5 ms, every mod-work sample ≥ 30 ms flagged. Whole-frame cost is tracked separately and is not measured here.

## Where the cost lands

- **Panel closed (normal play): zero new work.** No text, snapshot, projection or scan runs. `FoodMoneyCounters.Requests` stays 0. The only added per-event cost is the O(1) counter update inside budget, meal-status and excursion setters on paths that already run (posting, hold/confirm/refuse, rival settlement, meal transitions). No reflection, polling, timer or `Update` hook was added.
- **Panel open:** text is still built at most every 3 s (unchanged cadence), now from the counters plus one pass each over history rows, places and native statements. The O(consumers) scans and the per-shop O(history + places + statements) lookups are gone.
- **Attach (load, migration, route rebuild):** one O(consumers) scan builds the counters, next to the existing attach loop over the same consumers.
- **Parked hotspots:** physical population, closure refresh, optional-stop refresh and save serialization are untouched. Saves are byte-identical (no new field).

## Offline measurement

`dotnet run --project tools/feedback-panel-regression/FeedbackPanelRegression.csproj --configuration Release`, Windows desktop, .NET 10 Release, 20 warm-up + 200 timed requests per row (`Stopwatch`, `GC.GetAllocatedBytesForCurrentThread`). "Before" runs the removed panel code on the same synthetic fixture; "after" runs the shipped path. The tool also fails if counters differ from a full rescan, if a printed money line changed, or if a week sum differs from its rows. Final code (run 2):

### Consumer scans → incremental counters (taken win)

| Consumers | Before p50 / p95 / max ms | Before B/request | After p50 / p95 / max ms | After B/request |
| ---: | --- | ---: | --- | ---: |
| 1,914 | 0.6690 / 1.1088 / 1.4989 | 743,240 | 0.0021 / 0.0026 / 0.0111 | 6,280 |
| 10,000 | 1.4390 / 3.4898 / 4.4826 | 3,863,008 | 0.0045 / 0.0050 / 0.0086 | 6,456 |
| 100,000 | 20.1208 / 32.1975 / **46.5256** | 39,248,344 | 0.0016 / 0.0030 / 0.0048 | 7,768 |

The removed scans had samples ≥ 30 ms at 100,000 consumers (**flagged**; 45 of 200 in the spike-count rerun below). The counters had none in any run. The remaining bytes are the money text itself.

### Per-shop lookups → one-pass index, plus the new week text

20,000 places (the place cap), 14 history rows per shop. "Before" is only the removed lookups (it had no week text); "after" includes the lookups and the new week-over-week text.

| Shops | Before p50 / p95 / max ms | Before B | After p50 / p95 / max ms | After B |
| ---: | --- | ---: | --- | ---: |
| 1 | 0.0377 / 0.0453 / 0.1472 | 336 | 0.1132 / 0.2211 / 0.2695 | 5,520 |
| 64 | 1.9890 / 2.7575 / 8.3129 | 19,992 | 0.4250 / 0.5459 / 7.3592 | 292,864 |
| 1,024 | 89.3232 / 116.1171 / **123.5872** | 319,512 | 2.4110 / 14.5748 / 26.0409 | 4,525,584 |

At one shop the new path costs about 0.1 ms more (one full pass over 20,000 places replaces an early-exit search, plus the new text). From 64 shops on it is faster. The removed lookups were ≥ 30 ms at 1,024 shops (**flagged**; 200 of 200 in the spike-count rerun below). The new path had max 26.0 ms in this run but **1 of 200 samples at 34.2305 ms** in the rerun (**flagged**: open panel, 1,024 managed shops, 20,000 places, component `BuildEconomyText` Core part). The tail comes from building about a megabyte of panel text (GC), at most once per 3 s while the panel is open; it is not ordinary-frame work and only occurs at the 1,024-scope maximum.

### Hidden panel

1,914 consumers, one pass of budget/meal mutations with the panel closed: **0 B** allocated, **0** snapshot requests, **0** text builds. `F6c_NothingIsBuiltWhileHiddenAndMutationsAllocateNothing` repeats this inside a simulated city (10,000 setter rounds, 0 B; 0 requests after three simulated days).

### Cost added to existing paths

Setter overhead (2,000,000 assignments, 0 B):

| Consumers in fixture | Unattached ns/assignment | Attached ns/assignment |
| ---: | ---: | ---: |
| 1,914 | 0.27 | 15.61 |
| 10,000 | 0.29 | 15.81 |
| 100,000 | 0.30 | 6.64 |

Household posting (`tools/bounded-bulk-regression`, 32-posting `StepBudget` calls, seven days after a warm day), call ms median / p95 / max:

| Consumers | Before (base `c64e3e2`) | After, run A | After, run B |
| ---: | --- | --- | --- |
| 1,914 | 0.0129 / 0.0232 / 0.0336 | 0.0361 / 0.0395 / 0.0631 | 0.0231 / 0.0281 / 0.0604 |
| 10,000 | 0.0139 / 0.0169 / 0.1254 | 0.0247 / 0.0273 / 0.0815 | 0.0245 / 0.0268 / 0.0938 |
| 100,000 | 0.0057 / 0.0089 / 0.1999 | 0.0066 / 0.0099 / 0.3482 | 0.0064 / 0.0095 / 0.3509 |

This is a real cost of the incremental counters: about +0.0006 to +0.0163 ms per posting call at p95, roughly 0.2 % of the 5 ms gate, with zero allocation in every run and no sample ≥ 30 ms. It is not a win and is reported as a cost.

### Spike-count rerun (review fix)

Round-1 review found that `spikesAtLeast30` reported only whether the maximum reached 30 ms (0 or 1). The tool now counts every unrounded timed sample ≥ 30 ms and reports `timedSamples`. Rerun on the final code (same fixtures, 200 timed samples per row; p50 / p95 / max ms, samples ≥ 30 ms):

| Row | Before | After |
| --- | --- | --- |
| 1,914 consumers | 0.6670 / 0.7526 / 1.5205, 0 | 0.0022 / 0.0025 / 0.0030, 0 |
| 10,000 consumers | 1.5777 / 3.8990 / 26.5672, 0 | 0.0021 / 0.0027 / 0.0195, 0 |
| 100,000 consumers | 24.0747 / 35.2186 / 70.6390, **45** | 0.0029 / 0.0039 / 0.0064, 0 |
| 1 shop | 0.0635 / 0.0713 / 0.1846, 0 | 0.2175 / 0.2427 / 0.3370, 0 |
| 64 shops | 2.0878 / 2.7115 / 7.5123, 0 | 0.7148 / 0.8812 / 15.8811, 0 |
| 1,024 shops | 107.5294 / 145.3742 / 162.8678, **200** | 2.3325 / 13.2756 / **34.2305, 1** |

Hidden panel again 0 B and 0 requests. Attached setter 20.40 / 9.09 / 4.91 ns.

## Wins taken in this slice

1. **Consumer scans replaced by incremental counters** (the plan's expected win). See the first table: 1,914 consumers p95 1.1088 → 0.0026 ms and 743,240 → 6,280 B/request; 100,000 consumers p95 32.1975 → 0.0030 ms and 39,248,344 → 7,768 B/request.
2. **Per-shop lookups replaced by one pass.** 1,024 shops p95 116.1171 → 14.5748 ms with the new text included; 64 shops 2.7575 → 0.5459 ms.
3. **Cheaper counters and text after the first measurement** (same fixture, run 1 → run 2):
   - Exact 128-bit integer accumulators replaced `decimal`. Attached setter 25.21 / 23.13 / 11.97 → 15.61 / 15.81 / 6.64 ns.
   - A value-tuple history key replaced the per-row string key, and week text now appends straight into the panel builder. 1,024-shop build 8,109,056 → 4,525,584 B/request, 64 shops 509,920 → 292,864 B, 1 shop 8,384 → 5,520 B; timings within noise (1,024-shop p95 12.4616 → 14.5748 ms, max 22.0231 → 26.0409 ms).

## Limits

Synthetic Core fixtures on a desktop CPU under .NET 10, not Unity/Mono and not game execution. The panel's IMGUI layout and draw, native statement reads, shop names and regulars are not included. Run-to-run noise is visible in the posting rows. In-game mod-work p95, ≥ 30 ms flags and whole-frame metrics (tracked separately) come from the next batched rig run ([checks](../checks/f6c.md#in-game-checks-for-the-next-batched-rig), check 6).
