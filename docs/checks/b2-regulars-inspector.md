# B2: Observed own-shop regulars and resident memory words

[Checks index](../../CHECKS.md).

## B2 base sync - 1 November 2026 (offline only)

Merge development `7053a28` into approved B2 `2c74051` with two-parent ancestry.
Keep synced B0/B0b/B1 and every incoming slice; combine B2's reviewed inspector
memory presentation with the existing complete native address formatter. No
new policy, schema or additional integration fix. [All 24 conflict resolutions
and validation details](../reviews/2026-11-01-b2-sync-base-pr-description.md).

**2,142/2,142 unit tests, 2,142 discovered cases and all 53 foreground checks
pass**: Steam-reference build; all thirty regression projects; four Python
harnesses; three demos; six capacity fixtures; exact input/record boundaries,
base recovery, history churn, choice rush and optional-stop diagnostics. The
prescribed other-dotnet guard precedes every command; server reuse is off.

Persistence and the schema-18 contract are identical to both parents. Migrations
13–17, frozen readers, round-trips, nonzero held replay and refusal preservation
pass. All six capacity hashes/sizes/record counts and reservations match synced
B1; CF32/CF64 reserved budgets remain **82.204% /96.509%**, with CF64's existing
hard-cap-only qualification. Economy/reload digests remain exact. B2 adds
**zero saved bytes** at both benchmark populations and in read/reload/replay tests.

Preservation audit: all **499 executable/source files** match the expected
combination; all **428 historical B2-head checks blocks**, eight native checklist
items, the dated notice and five performance rows survive. All **24 incoming
checks/perf files** are unchanged; **300 local links** resolve. Historical
measurements below remain intact. Current offline projection p95 is **0.667
/1.009 ms** at 1,914 /5,000 consumers; full Core popup p95 **0.010 /0.013 ms**
with **9,136 bytes/call**, versus historical R1 9,104. These are normalized batch
means, excluding native panel accounts/Unity; no speedup/native claim. B2's
measured batches have zero >=30 ms spikes; other offline save/load/batch metrics
at least 30 ms are flagged individually in the [command receipts, source hashes
and measurements](../reviews/2026-11-01-b2-sync-base-results.json).

No game/rig/profile/lease, Steam/Workshop mutation, ordinary save or WSL access.
Under merge rule v2, the native checklist joins the batched combined locked
development head; pointer/input checks wait for Peter's 6 Nov playtest. Native
mod-work p95 <5 ms, >=30 ms spike reporting, separate whole-frame tracking and
the four parked hotspots remain unchanged. No deployment or release clearance.

## B2 review round 1 fixes - 31 October 2026 (offline only)

Review fixes (Opus-r1-1/2/3, source `ef2a40c`): existing own-scope panel blocks
retain their status, paused warning, history and accounts; unavailable regular
evidence suppresses only the regulars line. B1's dump again includes **all retained
entries**, including stale/unobserved/withdrawn generations. Each entry exposes
`OwnCurrent`; `Regular` and own-shop totals require it. Inspector words and
operating regular counts remain restricted to own-current shops. The redundant
post-cache `if (!built)` branch is removed.

Five focused cases fail on the prior source, then all five pass: three historical
generation cases plus strengthened diagnostic and panel source-gate contracts.
The panel check is a headless call-site contract, not native UI evidence.
Diagnostic tests prove unchanged payload bytes and zero routes/checkout, including
after reload.

**Validation: 1,976/1,976 tests, 1,976 discovered tests, all 31 requested
foreground commands plus the B2 benchmark pass.** Wait script before each dotnet
test/run. [Pre-fix five-case failures](../reviews/2026-10-31-b2-r1-red.txt);
[command results/source hashes](../reviews/2026-10-31-b2-r1-offline-checks.json).
Game-target DLL SHA-256: `813771f31cdc1531e0d22ce8a2e46032524006a661a2d925c2329de68302d629`.
No game or rig work; schema 18, saved fields and choice paths unchanged.

**R1 offline remeasurement:** .NET 10 Release, saturated four-entry memory,
sixteen shops, forty normalized batch means after five warmups; not individual
call tails or native mod-work. Setup/save outside timers; external CPU/JIT noise
is uncontrolled. Panel numbers cover B2 text/projection only, excluding native
accounts/Unity; popup numbers cover full Core capture/text.
[Raw R1 measurements](../reviews/2026-10-31-b2-r1-measurements.json).

| Operation (p50 / p95 / max) | 1,914 consumers | 5,000 consumers | Allocated bytes/call, reference /5k |
| --- | ---: | ---: | ---: |
| All-shop projection refresh (ms) | 0.472 / 0.629 / 0.678 | 0.531 / 0.606 / 0.645 | 41,840 / 120,057 |
| Cached per-shop lookup (ns) | 11.830 / 12.620 / 14.910 | 5.560 / 6.270 / 8.200 | 0 / 0 |
| Panel B2 text + projection (ms) | 0.219 / 0.937 / 0.991 | 0.990 / 1.025 / 1.064 | 44,496 / 122,849 |
| Full Core popup capture + text (ms) | 0.006 / 0.010 / 0.011 | 0.007 / 0.011 / 0.013 | 9,104 / 9,104 |
| Unchanged B1 repeat commit (ns) | 132.700 / 186.100 / 236.300 | 168.400 / 192.200 / 225.100 | 0 / 0 |

**Offline batches >=30 ms flagged:** zero in these measured batches. These are benchmark batches, not mod-work frames.
Closed/hidden builders and commit maintenance remain zero by unchanged call-site
gates (the original source-contract/lazy tests still pass). Saved growth remains
**zero bytes**, exact payload/reload/replay tests pass. Whole-frame timing and
native mod-work p95 <5 ms /spikes >=30 ms remain separate pending 6 Nov checks.
The same eight numbered rig checks in the B2 entry below remain pending; check 5
now explicitly retains old-generation evidence with `OwnCurrent:false`, and
check 6 preserves existing own-scope blocks when only regulars are unavailable.


## Step B2 regulars and inspector words - 31 October 2026 (offline only)

Implemented on B1 PR #17 `f214cd4`, target `codex/pc-b1-memory-loyalty-v2`.
Source commits `980ff4f` and `69a54dc`; schema stays **18**. Read the remote
B-E plan and B1 PR/entry without merging siblings. The B-E plan is absent locally,
so its status is not edited here.

Regulars count stable consumers, including represented slots, once per retained,
current owned business generation: >=3 served visits, positive trait-aware
effective affinity, evidence at most fourteen days old (inclusive). Two flat
projection-only settings clamp/log once: `FoodMemoryRegularServedVisits` 3 (2-10)
and `FoodMemoryRegularRecentDays` 14 (1-28). One lazy consumer pass covers all shops
at the open panel's three-second refresh, then cached O(1) lookups. Nothing is
maintained at commit/midnight/closed UI.

The own-shop panel shows `Regulars: N`; current cached native registration and
authority identity filter sold/reset/replaced generations without updating
authority. The old customer-home-district breakdown is removed. The visible
resident popup adds short own-memory phrases, native shop labels/readable
addresses and no scores/multipliers/percentages. Existing selection/pool-generation
checks stay intact. Represented stories have no physical body and remain separate.

Bridge reads: `FoodRegularsDump()`; `FoodMemoryDump(id)` now adds a consumer
regular flag, per-entry flags, own-shop counts, words and explicit represented
labels. `FoodEconomyPanelText` / `ResidentPopupText` return cached UI strings,
never opening/building UI. No rival/city-wide loyalty or trait/catchment data.
Optional recent-cause totals are omitted because B1 may retain an older decay
anchor after disappointment; the actual last cause cannot honestly be counted as
a dated recent-window failure. Protected bad insertions may leave no shop entry;
B2 invents none.

No saved fields, memory/decay/eviction/watermark contract, choice weights, native
sales, money, routes or RLO changed.

**Validation: 1,973/1,973 tests (24 new B2 cases), 1,973 discovered tests, all 31 requested
foreground commands plus the B2 benchmark pass.** The other-worktree wait script
ran before each dotnet test/run; no game/rig process was started. Native authority
regression passes **241 checks** against callback doubles, including B2 read-only
generation/replacement controls. Build DLL SHA-256: `aad4eae65c70bb15883e747a1686f1a311ff3aa38933b01bb4fe2a2057296aa4`.
[Command manifest and source hashes](../reviews/2026-10-31-b2-offline-checks.json).
No background test/run/testhost remains.

**Offline performance.** [Raw measurements](../reviews/2026-10-31-b2-measurements.json), reproducible with
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
5. **LOCKED-OK** - Capture all own-shop counts and selected consumer flags before/after save/reload at the same logical checkpoint, then replay that checkpoint with different continuation budgets. Compare exact counts, memory/watermarks, choices and route counts. Compare population-payload bytes with the matched B1 checkpoint/control: B2 adds zero bytes. After an independently established shop-generation replacement, the replacement starts with zero inherited regulars; the old generation remains visible in retained `Entries` with `OwnCurrent:false` / `Regular:false`, and is absent from own-shop regular totals.
6. **NEEDS-INPUT** - Open Ctrl+F8 and compare each player's own shop's `Regulars: N` with `FoodRegularsDump()` at the same refresh/day. Capture the panel visually and through `FoodEconomyPanelText` after its three-second refresh. Verify rival/stale generations have no regular line and no district/trait/catchment breakdown is shown. Existing own-scope status/history/account blocks remain visible even when their regulars line is withheld. Close/reopen and verify freshness.
7. **NEEDS-INPUT** - Hover and click the known physical regular; capture native shop name/readable address and phrases such as `a regular at` and stockout words, with no numeric scores/multipliers/percentages. Move camera/reuse body pool and reopen: stable selection/generation guards still apply. Compare the represented consumer's separately labelled `FoodMemoryDump(id).Words` with the slot-zero popup; represented slots have **no body to hover**, and their stories must never be presented as the selected walker's own visits. Use `ResidentPopupText` to retain the actual cached popup body.
8. **LOCKED-OK** - In the normal combined session, interleave matched idle/breakfast/lunch/evening windows from the same checkpoint with panel/popup closed versus open, plus warm/cold refreshes and a soak. Bridge semantic open/close may exercise downstream content while locked; it does not prove check 7's hover/gesture path. Record combined **mod-work p50/p95/max**, require p95 <5 ms, flag every spike **>=30 ms** with count/condition/component; record **whole-frame timing/stalls separately**. Include allocations/GC, route/query counts, pending work/lag and saved size. Verify closed/hidden surfaces build no new projection/text, open panel refreshes at most every three seconds and visible popup at most once per second. Manual saves remain outside windows.

## Dated development notice

> **Step B2 regulars and inspector words - 31 October:** own-shop observed regular counts and resident memory phrases are implemented as on-demand, read-only projections of B1 evidence. Two flat projection settings default to three served visits and fourteen days; effective memory must stay positive. No saved bytes or choice changes. [Offline checks, measurements and numbered 6 Nov rig checks](b2-regulars-inspector.md#step-b2-regulars-and-inspector-words---31-october-2026-offline-only); native proof remains pending.
