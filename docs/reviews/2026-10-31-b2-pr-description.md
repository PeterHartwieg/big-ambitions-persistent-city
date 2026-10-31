# B2: observed own-shop regulars and resident memory words

Stack base/PR target: `codex/pc-b1-memory-loyalty-v2`, PR #17 head `f214cd4`. Implementation: `980ff4f`, feedback reuse/diagnostic proof: `69a54dc`. The harness handles push, PR creation and later retargeting.

B2 projects retained B1 evidence into unique-consumer regular counts for current player-owned business generations. Defaults implement Peter's Q7 decision: at least three confirmed served visits, positive lazy-decayed affinity and evidence within fourteen days (inclusive). Flat `FoodMemoryRegularServedVisits` (2-10) and `FoodMemoryRegularRecentDays` (1-28) clamp/log once and only change presentation.

The open business panel adds `Regulars: N`; one lazy pass covers all own shops at the existing three-second refresh. The resident popup builds short memory phrases only while visible/open, using cached native shop names and readable addresses. Represented consumers are explicitly labelled in diagnostics and never attributed to the selected physical resident. The old free customer-home-district line is removed. No rival/city-wide loyalty, trait or catchment breakdown is added.

Read-only bridge: `FoodRegularsDump()`; `FoodMemoryDump(id)` adds own-shop counts, a consumer regular flag, per-entry flags and separate consumer words; `FoodEconomyPanelText` and `ResidentPopupText` expose cached UI content without opening/building it. Generation checks read existing native authority/registration facts without updating them. Optional recent-cause totals are omitted: B1 can retain an older decay day after disappointment, so its last cause cannot honestly be dated as a recent-window count.

Schema 18, persisted fields, memory/watermark/decay/eviction contract and choice/route/checkout paths are unchanged. Exact payload tests prove **zero saved-byte growth** across reads, reload and replay. B2 installs **zero commit hooks** and does no midnight/per-frame projection work. Physical population, closure refresh, optional-stop refresh and serialization hotspots remain untouched.

**Validation: 1,973/1,973 tests (24 new B2 cases), 1,973 discovered tests, all 31 requested
foreground commands plus the B2 benchmark pass.** The other-worktree wait script
ran before each dotnet test/run; no game/rig process was started. Native authority
regression passes **241 checks** against callback doubles, including B2 read-only
generation/replacement controls. Build DLL SHA-256: `aad4eae65c70bb15883e747a1686f1a311ff3aa38933b01bb4fe2a2057296aa4`.
[Command manifest and source hashes](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/codex/pc-b2-regulars/docs/reviews/2026-10-31-b2-offline-checks.json).
No background test/run/testhost remains.


**Offline performance.** [Raw measurements](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/codex/pc-b2-regulars/docs/reviews/2026-10-31-b2-measurements.json), reproducible with
`dotnet run --project tools/food-regulars-regression/FoodRegularsRegression.csproj --configuration Release`.
Windows .NET 10 Release, 957 residents /1,914 consumers and 2,500 /5,000, sixteen
owned shops, four retained memories per consumer. Forty normalized batch means
after five warmups; percentiles describe batch means, **not individual-call tails**.
Setup/save/load are outside timers; external CPU/JIT load is uncontrolled.
Panel timing covers B2 text/projection only; native accounts and Unity layout are
excluded. Popup timing includes the full production Core capture and text.

| Operation (p50 / p95 / max) | 1,914 consumers | 5,000 consumers | Allocated bytes/call, reference /5k |
| --- | ---: | ---: | ---: |
| All-shop projection refresh (ms) | 0.500 / 0.577 / 0.682 | 0.933 / 1.136 / 1.193 | 41,840 / 120,039 |
| Cached per-shop lookup (ns) | 12.100 / 13.770 / 18.630 | 10.690 / 12.790 / 15.860 | 0 / 0 |
| Panel B2 text + projection (ms) | 0.254 / 1.025 / 1.034 | 1.015 / 1.078 / 1.110 | 44,496 / 122,852 |
| Full Core popup capture + text (ms) | 0.006 / 0.009 / 0.009 | 0.007 / 0.012 / 0.014 | 9,104 / 9,104 |
| Unchanged B1 repeat commit (ns) | 134.300 / 214.200 / 329.300 | 156.000 / 180.100 / 188.900 | 0 / 0 |

B2 adds **zero commit hooks /0 update work per commit**; the unchanged B1 path above
is measured separately. Closed/hidden projection/text has **zero builds and zero
added allocation**, established by source call-site gates and lazy-query tests,
not a native timing window. Open panel refreshes every three seconds, visible
popup at most once per second. No per-shop population scan or midnight sweep.
Saved payloads stay **1,503,076 -> 1,503,076 bytes**
and **3,925,066 -> 3,925,066 bytes**: **zero growth**.
Exact-byte reload/replay and different projection settings preserve choices,
weights, money and route counts. **Offline batches >=30 ms flagged:** zero in these measured batches. These are benchmark batches, not mod-work frames.

Low-risk wins: pre-sized transient consumer dedup allocates
**73,152 -> 38,736 bytes**
at reference and **322,638 -> 116,932 bytes** at 5k
(same IDs/counts; raw data retains JIT/accounting noise). Cached count queries use
one dictionary probe instead of two; the panel reuses the owned snapshot rather
than validating each generation twice; diagnostics reuse effective affinity for
the flag; hidden selected popups stop the prior one-build-per-second refresh.
The roadmap Perf wins log records these measured allocation/work reductions.

Combined **native mod-work p50/p95/max remains pending**, require p95 <5 ms and
flag every spike >=30 ms. **Whole-frame timing/stalls remain separate**.
No native timing or accounting claim follows from these headless measurements.
The four parked hotspots were not changed.


### In-game checks for 6 Nov rig

Pending `rig_test` (Codex Sol, high), on a named disposable FT fixture only. LOCKED-OK means feasible through bridge reads/semantic actions under feasibility report `84be71a`, **not passed**. Pointer/gesture proof remains NEEDS-INPUT. No rig work was run for B2.

1. **LOCKED-OK** - Freeze source/DLL hashes, dependencies, effective projection/memory settings, seed, checkpoint and known slot-zero/represented consumer IDs. Record original autosave, disable it in the isolated profile, verify off before every measured window and again after reload. Save only outside windows; invalidate any window with save activity. Restore original autosave/preferences and fixture files during cleanup.
2. **LOCKED-OK** - Follow a known slot-zero consumer and one represented slot across confirmed served days using `FoodMemoryDump(id)`: retain served counts 1/2/3, effective affinity, last evidence day, `Entries[].Regular`, `StoryKind` and `Represented`. At the third confirmed visit, positive and recent evidence qualifies. Compare `FoodRegularsDump()` before/after: each stable ID contributes once per current generation, irrespective of offers or decision retries. Check exactly fourteen days versus fifteen, decay to zero and retained-memory eviction.
3. **NEEDS-INPUT** - Through the player-shop UI induce and visually verify a product/bag stockout after the known consumer commits/reaches service. Retain native stock/receipt evidence and before/after memory dumps. Choose a fixture consumer whose effective affinity will cross zero under the actual penalty: served count remains unchanged, affinity decreases, regular flag becomes false and that shop's count loses one. Also retain a still-positive control if available; disappointment alone does not necessarily end regular status. Wait for unlocked 6 Nov input.
4. **LOCKED-OK** - After check 3's fixture setup, read `FoodMemoryDump(id)` and `FoodRegularsDump()` without UI: verify the flag/count transition, stockout words, recovery after later confirmed service, and no duplicate experience on repeated reads. No market refresh, route or checkout calls may be caused by these diagnostics.
5. **LOCKED-OK** - Capture all own-shop counts and selected consumer flags before/after save/reload at the same logical checkpoint, then replay that checkpoint with different continuation budgets. Compare exact counts, memory/watermarks, choices and route counts. Compare population-payload bytes with the matched B1 checkpoint/control: B2 adds zero bytes. After an independently established shop-generation replacement, the replacement starts with zero inherited regulars; the old generation is absent from current-shop feedback.
6. **NEEDS-INPUT** - Open Ctrl+F8 and compare each player's own shop's `Regulars: N` with `FoodRegularsDump()` at the same refresh/day. Capture the panel visually and through `FoodEconomyPanelText` after its three-second refresh. Verify rival/stale generations have no regular line and no district/trait/catchment breakdown is shown. Close/reopen and verify freshness.
7. **NEEDS-INPUT** - Hover and click the known physical regular; capture native shop name/readable address and phrases such as `a regular at` and stockout words, with no numeric scores/multipliers/percentages. Move camera/reuse body pool and reopen: stable selection/generation guards still apply. Compare the represented consumer's separately labelled `FoodMemoryDump(id).Words` with the slot-zero popup; represented slots have **no body to hover**, and their stories must never be presented as the selected walker's own visits. Use `ResidentPopupText` to retain the actual cached popup body.
8. **LOCKED-OK** - In the normal combined session, interleave matched idle/breakfast/lunch/evening windows from the same checkpoint with panel/popup closed versus open, plus warm/cold refreshes and a soak. Bridge semantic open/close may exercise downstream content while locked; it does not prove check 7's hover/gesture path. Record combined **mod-work p50/p95/max**, require p95 <5 ms, flag every spike **>=30 ms** with count/condition/component; record **whole-frame timing/stalls separately**. Include allocations/GC, route/query counts, pending work/lag and saved size. Verify closed/hidden surfaces build no new projection/text, open panel refreshes at most every three seconds and visible popup at most once per second. Manual saves remain outside windows.
