# B1: Shop memory capture and loyalty policy v2

[Checks index](../../CHECKS.md).

## B1 base sync - 1 November 2026 (offline only)

Merge development `a7ab5bc` into approved B1 `f214cd4` with two-parent ancestry.
Resolve all 29 conflicts by retaining synced B0/B0b and every incoming slice,
then applying B1's reviewed delta. Three shared Core/Runtime files combine the
existing address/interior changes with B1 settings and activation; no B1 policy
or additional behavior change is authored, and no integration fix is required.
[Every conflict and its resolution](../reviews/2026-11-01-b1-sync-base-pr-description.md).

**2,115/2,115 unit tests, 2,115 discovered cases and all 52 foreground checks
pass**: Steam-reference build; all 29 regression projects, including memory,
held tuning, native candidates, pickup, agency payment and interior guard; four
Python harnesses; three demos; six capacity fixtures; base recovery, exact input
and record boundaries, history churn, choice rush and optional-stop diagnostics.
The prescribed other-dotnet guard precedes every command; server reuse is off.

No schema collision: development's Persistence tree equals B1's B0b prerequisite.
Schema remains 18; migrations from 13–17, frozen readers, round-trips, refusal
preservation and held nonzero replay pass. The nonzero eight-shop vector retains
525 bytes within 589, with unchanged collection charges. All six capacity wire
hashes, sizes, record counts and future reservations match synced B0b. CF32/CF64
complete budgets remain **220,664,366 /259,064,366 bytes (82.204% /96.509%)**;
CF64 retains its >95% warning and no 85% claim. Exact 268,435,456-character/UTF-8
and 4,000,000-record boundaries pass. Base recovery remains 115,049,515 characters;
280,000 history generations preserve all purchases and exact totals. Legacy economy
remains 849 orders /797 purchases /107,967,424,757,760 spent units, with unchanged
save/economy/reload digests.

Preservation audit: all 638 executable source/project files match their intended
parent or combination, all 406 historical checks blocks survive, development's
22 checks/performance files are unchanged, and B1's dated notice, five performance
rows and eight native checklist items survive exactly. Local link/heading audit
passes. B1 checks and notices now live here; performance rows have their own
[file](../perf/b1-memory-loyalty.md), with one line in each shared index.

[Command receipts, source hashes, preservation audit and measurements](../reviews/2026-11-01-b1-sync-base-results.json).
Synthetic save/load and aggregate/batch timings >=30 ms are individually flagged
there, separately from native mod-work/whole-frame cost. No new performance win
or native gate clearance is claimed. Production DLL SHA-256:
`0d63860784e2da52df73b9eba0bfef71a4eb0583d9c4c9d29588bb5317dd295e`.

No game/rig/profile/bridge/Workshop/WSL or release work. Under merge rule v2, the
unchanged native checklist below joins the batched combined locked development
head; pointer/input-only work waits for Peter's 6 Nov playtest.

## B1 review round 4 fixes - 31 October 2026 (offline only)

Addresses Sol-r4-1/2. **1,949/1,949 unit tests, discovery and all 31 required
foreground commands pass**, including standalone regressions and all three demos.
The other-worktree dotnet wait runs before each command.
[Command results and tested source hashes](../reviews/2026-10-31-b1-r4-offline-checks.json).

- Bag-stockout revalidation now shares the product-stockout necessary queue,
  deadline and opening bounds. Zero throughput hides service duration, so queue
  plus eating must leave strictly positive time for service. Ten-minute stops,
  excessive queues and eating-only remaining time stay neutral. Fresh named bag
  evidence is still required, and valid longer stops retain exactly-once capture.
  Eight new cases cover these controls with/without held-image reload, then
  save/reload again to verify unchanged memory/watermark, zero routes and zero
  checkout calls. The six invalid-stop cases fail before the fix; both valid-stop
  controls pass before and after. All **90 focused memory/cache tests pass**.
- The physical returning-door closure path now reuses `business.OpeningPlace`.
  The existing mutation-safe cache comparison covers all four B1 sites and
  matches their open/admission/close results. Over **10,000 warmed batches**,
  round-3 behavior allocates **10,720,000 -> 0 bytes (1,072 -> 0 per batch)**
  after this final reuse. All-temporary four-site behavior allocates 42,880,000
  bytes (4,288/batch). Thread/setup/first-use allocation is excluded. The roadmap
  Perf wins log records this offline .NET 10 allocation reduction; no native
  timing/mod-work gate inference is made.

[Pre-fix failures and cache comparison](../reviews/2026-10-31-b1-r4-focused-evidence.txt).
Game-target DLL SHA-256: `ce13dd20a574e46986438b8684350feee6335150aa468e9c311a9054585a4ecd`.
No game or rig work; no saved fields changed. Existing numbered 6 Nov rig checks
below remain pending.


## B1 review round 3 fixes - 31 October 2026 (offline only)

Addresses Sol-r3-1/2 and Opus-r3-1. **1,941/1,941 unit tests, discovery and all
31 required foreground commands pass**, including the standalone regressions and
three demos. The other-worktree dotnet wait runs before each command.
[Command results and tested source hashes](../reviews/2026-10-31-b1-r3-offline-checks.json).

- Reached-stop stockout checks now consider affordable offers: confirmed zero stock
  for the $4 hot dog is captured with $5 available even when a $9 burger remains
  stocked. Wholly unaffordable menus and affordable unknown stock stay neutral;
  known service, queue, opening hours and time must otherwise fit the visit.
  Tests cover captured-image reload, terminal watermark replay, unchanged meal
  identity fields, zero routes and zero checkout calls. The earlier identity test's
  stockout fixture now uses a queue within patience so stockout is causal.
- A failure leaving effective affinity positive subtracts from stored affinity
  and preserves the old decay/expiry day. It cannot keep that habit alive after
  the no-failure comparison expires. Tests compare actual two-shop probabilities
  through day 65 (including the forget threshold), default/max decay and loyalty
  bands 2/4, before/after memory reload; served counts and exactly-once remain intact.
  The schema note explains the retained day anchor; no saved fields change.
- Arrival closure, represented admission and reached-stop checks reuse the existing
  mutation-validated `FoodBusiness.OpeningPlace`. In an offline .NET 10 comparison,
  **10,000 warmed batches across the three sites allocate 32,160,000 -> 0 bytes
  (3,216 -> 0 bytes/batch)**, with identical results. The regression checks both
  replacement and in-place hours mutation. Thread/setup/first-use allocation is
  outside that window. The roadmap Perf wins log records the reduction; no native
  timing or mod-work gate claim is made.

Five correctness reproductions fail against the prior production code; all pass
with the fixes. The new tests add 14 cases, including the hours-cache comparison.
[Focused red evidence and allocation comparison](../reviews/2026-10-31-b1-r3-focused-evidence.txt).
Game-target DLL SHA-256: `ae7bb352957b03398db81c18aa6586c9fcef75a01c11d513d4e28daf988abc98`.
No game or rig work. Existing numbered 6 Nov rig checks below remain pending.


## B1 review round 1 fixes - 31 October 2026 (offline only)

Addresses Sol-r1-1/2 and Opus-r1-1/2 in `07107d4`, with unavailable-proof handling
in `6e5988c`. The four reports reproduce at production seams: **14/14 targeted
cases fail before**, then the corrected multi-shop, arrival/reload, account and
unchosen-stop assertions pass. [Red evidence](../reviews/2026-10-31-b1-r1-red.txt).

- **Eviction and return odds:** preflight the usual expired/affinity/day/ordinal-ID
  victims before inserting a new bad experience. Forgetting any required victim
  must not lower that competing shop's contribution. Otherwise keep the entries,
  increment the session-local `ProtectedInsertions` diagnostic and commit the
  failed watermark exactly once, without allocating a reference. The reported
  capacity-one loyal example stays at **33.33%**, rather than rising to 37.29%.
  With a probability floor or neutral minimum, no stronger bounded penalty can
  universally compensate for losing a competitor's boost; protected capacity cases
  therefore retain the terminal failure without a new per-shop entry. Other bad
  insertions still retain negative memory. Tests cover capacities 1-4, loyal/curious
  bands, actual positive evictions when safe, lowered capacity, floor-bound offers,
  reload, and 4,096 matched seeds/configuration: no newly returning seed.
- **Bag stockout capture:** explicit `Out of stock`/zero-unit evidence is captured
  before generic zero-throughput arrival refusal. A reached existing stop revalidates
  the saved generic service flag with one generation-matched, live failure read;
  native evidence bypasses the decision cache. Unknown staffing and missing/throwing
  proof stay neutral; the original refusal proceeds. Native-source regression also
  warms the cache, exhausts bags in the same minute, distinguishes an unstaffed
  register, refuses a replacement generation and proves no order/receipt is booked.
- **Physical queue behavior:** restore the original slot>0 patience refusal. Physical
  consumers still wait and buy when the meal fits their committed break, both before
  B activation and at weights 0/1. Memory captures Queue only when the existing
  break-fit refusal occurs and removing the wait would make the break fit.
- **Unchosen stops:** pass target business/place directly to the memory store; leave
  meal business/place/product fields untouched. Queue disappointment requires the
  decision's queue reason and a break/opening window which would fit without waiting.
  An already-too-short break remains neutral even if a queue is also present.

No schema/scalar/tuple change, extra route, checkout, midnight sweep, or rig work.
The generation index's new diagnostic counter costs **8 shared managed bytes**;
lookup and warmed repeat commits still allocate **0 bytes**. Full-memory save growth
is unchanged: 77,387 bytes at 1,914 consumers and 200,827 at 5,000; maximum extension
51 inclusive bytes remains within B0's 64. [R1 raw measurements](../reviews/2026-10-31-b1-r1-measurements.json).

| Consumers | Lookup p50/p95/max ns | Repeat commit p50/p95/max ns | Retained consumer-state delta |
| --- | ---: | ---: | ---: |
| 1,914 | 40.5 /43.2 /48.6 | 36.2 /39.4 /44.1 | 474,024 bytes |
| 5,000 | 21.0 /41.0 /41.8 | 19.6 /43.2 /45.2 | 1,228,920 bytes |

Forty normalized batch means after five warmups. The shared index allocates 2,056
bytes in both fixtures; its single GC retained delta is 1,264 /-6,960 bytes, the
negative value being measurement noise. Do not interpret GC deltas as exact sizes.
Three interleaved synthetic rush runs retain zero modeled ordinary-budget lag and
30,624 /80,000 logical routes. Active/weight-zero median Core-loop p50/p95 and largest
max: **0.771/1.159/11.795 vs 0.775/1.252/6.542 ms** at reference;
**0.161/0.284/10.378 vs 0.170/0.346/16.869 ms** at 5,000. Throughput active/zero:
8,465/8,588 and 33,484/27,063 decisions/sec. Ordinary modeled frames: 270-271 and
704-707. **No feature-loop sample >=30 ms**; these are not combined native mod-work
or whole-frame results. External CPU/JIT load is uncontrolled; no timing win claimed.

**Validation:** **1,927/1,927 tests (68 B1 cases), discovery, all 31 requested foreground checks and the memory benchmark pass**. [Foreground command manifest](../reviews/2026-10-31-b1-r1-offline-checks.json).
The live PR #17 description now contains the authored implementation results and the same
eight numbered LOCKED-OK/NEEDS-INPUT checks below (Sol-r1-4), with harness footer
preserved; the API readback matches the submitted body. All owned test processes
have exited; another worktree's active tests were left untouched. Native proof remains pending the normal combined 6 Nov session; the
existing rig checklist remains applicable, including queue-causation and bag cases.

## Step B1 shop memory and loyalty v2 — 31 October 2026 (offline only)

Implemented on B0b `c0af745`, stacked PR target `codex/pc-b0b-held-tuning`.
Production source **`7e43755`**, measurement source **`d896d0b`**; schema stays **18**. Read the B-E plan
at `origin/codex/persistent-city` and PR #14's contract report/probe without merging
or reusing its branch. B0b's captured A3 tuning resolves that earlier blocker.

Stable represented consumer IDs now retain at most four generation-keyed compact
memories (day, saturated served count, affinity, actual cause), an ordered exactly-once
watermark, lazy trait-scaled decay/expiry and deterministic replacement. Only actual
commitments/reached shops with named evidence disappoint; generic mixed stock/staff
refusals, affordability, route/schedule loss and unknown checkout stay neutral. Exact
late receipts update the original generation/service day once. New B capability
activates at the first crossed midnight; pre-B commitments stay neutral. Capture
reduces memory tuning to one bounded scalar/shop before either A3 draw and reuses it
through held-context reload; new uncaptured contexts use loaded edits immediately.
Final multipliers clamp after strength, floors/feasibility and route bounds remain.
All thirteen B1 flat config keys clamp/default and log once. No B2 UI/regular settings.

`PopulationRuntime.FoodMemoryDump(consumerId)` is a read-only bridge-callable detached
snapshot (entries/effective affinity/cause/watermark/capacity misses), with no cost
until called. Generation references append within the existing 20k/2 MiB limits;
no slot reclamation, scans or identity reuse. Saturation counts a skipped memory
capture and saves its terminal watermark without changing settlement or retrying it.
Lower capacity never trims on load/read; normal later insertion applies replacement.

**Save safety.** [B1 execution contract](../population-schema18.md#b1-execution--31-october-2026).
B1 deliberately lifts B0b's nonzero-contribution refusal with bounded read/write/replay
proof; 13-17 resolvers, tuple shapes, reference allocation and record charges stay
unchanged. The save-contract regression now accepts a full nonzero eight-shop image
(**525 bytes** including tuning in that vector), asserts **<=589**, and still refuses
out-of-bound/non-finite values. B0/B0b active-B refusal tests now assert supported B
revision 1 acceptance and unknown-revision refusal, including the earliest actual
native authority hook against doubles. Checkout regression now distinguishes verified
stock/bag exhaustion, unknown staffing and closure rather than matching a generic
mixed-cause message. The frozen legacy economy remains **849 orders / 797 purchases /
107,967,424,757,760 spent units**, with all three exact digests unchanged.

**Capacity recheck:** complete B0 full-memory/full-20k-table fixtures remain exactly
5,013,141 bytes (reference), 9,701,305 (5,000 consumers), 192,020,107 (CF32) and
230,420,107 (CF64), with unchanged record counts. Reserved future budgets remain
220,664,366 bytes /2,111,867 records (82.204%) and 259,064,366 /3,711,867 (96.509%).
The nonzero scalar spends at most another 184 bytes from the *existing* 65,536-byte
policy allowance; no per-consumer/save cap increases. CF64's >95% headroom warning
remains. One diagnostic write/read/migration sample each: reference 126.5/379.5/525.6 ms;
5,000 consumers 388.7/805.8/1,948.4 ms; CF32 write/read 2,859.9/9,133.5 ms;
CF64 3,683.8/10,600.9 ms. **All are >=30 ms, flagged as separate offline save/load
latency**, not ordinary-play samples. Oversized schema-17 ceiling sources still
refuse. These single samples are not latency percentiles or a serialization speedup.

**Offline measurements.** [Reproducible harness/raw data](../reviews/2026-10-31-b1-measurements.json),
`dotnet run --project tools/food-memory-regression --configuration Release`.
Synthetic .NET 10 paths/markets; setup/save outside timers. Forty normalized batch
means after five warmups, not individual-call latency percentiles. Retained sizes
are GC deltas, not Unity measurements. Commit samples are warmed repeat visits to
a retained generation; insertion/first-use state can allocate within the fixed bounds.

| Saturated consumers | Lookup p50 / p95 / max ns per candidate | Commit p50 / p95 / max ns | Lookup / commit allocation | Retained memory | Save growth |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1,914 (957 x2) | 41.3 / 46.6 / 57.4 | 57.7 / 79.7 / 136.9 | 0 / 0 bytes | 474,024 bytes | 77,387 bytes |
| 5,000 (2,500 x2) | 42.5 / 48.0 / 51.2 | 61.5 / 131.0 / 193.4 | 0 / 0 bytes | 1,237,144 bytes | 200,827 bytes |

Each full extension costs **51 inclusive bytes versus B0's 64 allocation**, with
40 bytes of growth versus empty; the 16-entry synthetic generation table adds 827.
The population-allocation totals are 475,608 / 1,240,936 bytes. The shared 16-generation
index adds 1,256 retained /2,048 allocated bytes per city (outside commit timers). One dictionary lookup
and <=4 entry checks/candidate, no catalogue search; selected candidates reuse the
same scalar rather than looking up memory again. No per-frame polling/reflection,
midnight memory sweep, extra routes or ever-growing processed-ID set.

Three interleaved rush samples per weight, 16 citizen /128 total units /2 ms soft
loop, synthetic simultaneous lunch. Below: medians of run p50/p95 and largest max;
these are **Core loop costs, not combined native mod-work or whole-frame timing**.

| Consumers / weight | Loop p50 / p95 / max ms | Frames (three samples) | Median decisions/sec | Logical routes |
| --- | ---: | --- | ---: | ---: |
| 1,914 / 1 | 0.867 / 1.737 / 13.681 | 272 /272 /271 | 6,922 | 30,624 |
| 1,914 / 0 | 0.999 / 1.805 / 17.515 | 272 /273 /271 | 6,261 | 30,624 |
| 5,000 / 1 | 0.337 / 0.588 / 14.128 | 706 /706 /705 | 17,385 | 80,000 |
| 5,000 / 0 | 0.333 / 0.620 / 20.288 | 706 /706 /707 | 15,959 | 80,000 |

All ordinary modeled loops drain within a 45-minute meal window even at 3 logical
minutes/sec and 100 FPS: **zero modeled catch-up lag**. The intentionally one-unit
control takes 34,452 frames (988.56 modeled minutes lag), preserves the exact saved
choices/memory hash and route counts, and is not a production throughput claim.
Repeated/interleaved runs also assert identical hashes per population/weight.
**Zero >=30 ms spikes in these feature/rush samples.** System/JIT load is uncontrolled;
timing varies between runs, so no end-to-end speedup claim is made.
Native mod-work p95/max, whole-frame stalls and native saved size remain unmeasured.

**Perf win:** remove enum boxing in the new watermark comparisons: ~120 -> **72**
bytes/commit (48 bytes saved), then reuse the existing entry and watermark: **0 bytes
per warmed repeat commit**, also 0 lookup allocation. First-use entries/references
still allocate bounded state. The preserved
[before prototype](../reviews/2026-10-31-b1-before-measurements.json) includes small
measurement-list growth noise, removed from the final counter. Reuse of the shortlist
scalar also removes a second lookup/<=4 entry checks for each of eight selected shops.
The allocation comparison is valid across the prototype stages; their uncontrolled
timings are not compared, and the final microbenchmark advances the lookup logical day.
The roadmap Perf wins log records these; all four parked hotspots stay untouched.

**Validation:** **1,909/1,909 unit tests, discovery and all 31 requested commands pass, plus eight additional checks (B1 memory benchmark, A3 candidate/choice/rush and four capacity fixtures): 39 unique passing commands**. All runs are foreground/sequential and wait for
other worktrees' dotnet test/run before starting. [Command evidence](../reviews/2026-10-31-b1-offline-checks.json).
The 50 new B1 cases cover monotonic bad visits, loyal/curious curves over 4,096 seeds,
actual failures versus rejected/unaffordable/unknown/interrupted visits, late receipts
and turnover, every update/save boundary, saturation/eviction/decay/capacity and held
replay; inherited frozen-source tests cover pre-B1 empty memory. No game was launched;
no test install, bridge directory, rig lease or ordinary save was touched.
The MSBuild server was shut down after the gate; a final process query found no
remaining dotnet/testhost process.

### In-game checks for 6 Nov rig

Pending `rig_test` (Codex Sol, high), on a disposable FT fixture. The input split
uses feasibility commit `84be71a` ([report](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/84be71a/docs/reviews/rig/2026-10-31-locked-session-feasibility/README.md)).
LOCKED-OK marks bridge/state/reload/perf feasibility; it is not a passed check. No
special performance-only rig run is requested; these join the normal combined session.

1. **LOCKED-OK** - On a named disposable FT fixture, freeze the tested commit/DLL, dependencies, config, consumer IDs/trait bands, seed and checkpoint. Record original autosave; disable and verify it before every measured window and again after every reload. Keep manual saves outside windows; invalidate any window containing save activity, and restore autosave/preferences during cleanup.
2. **LOCKED-OK** - Use matched loyal and curious consumer cohorts (including represented slots) over several confirmed served days and multiple fixture seeds. Dump each cohort before/after visits; compare return/novelty choice frequencies, per-generation served counts and affinity, four-entry bounds, identical route ceilings and unchanged native checkout authority.
3. **NEEDS-INPUT** - Through the shop UI, induce and visually confirm separate product/bag stockout, closure and excessive-queue failures at a player shop after consumers commit/reach it. Include rejected distant/unaffordable/unknown controls. Capture before/after native evidence and dumps: only named actual failures penalize; unknown checkout remains neutral. Wait for the unlocked 6 Nov session for these interactions.
4. **LOCKED-OK** - With those fixture failures established, follow recovery on days 3-6 through bridge reads: served recovery, lazy decay/expiry, loyalty forgiveness without a positive stockout gain, and generation turnover without inherited memory. Retain dumps and matched receipt evidence per logical day.
5. **LOCKED-OK** - Dump before/after save/reload at evaluating, partial-quote, selected, committed, reached and uncertain-receipt checkpoints. Replay the same checkpoint with different event budgets; compare choices, memory/watermarks, routes and exact money. Edit memory tuning/weight while unloaded: held contributions stay fixed, the next uncaptured context uses the edit that same day. Confirm a late receipt after midnight against its original generation once.
6. **LOCKED-OK** - Reconcile one full native day: exact resident payments/holds, native receipts, stock/bags, completed orders and daily accounts; no extra or missing transactions, including late confirmation and replay. Keep native evidence separate from offline doubles.
7. **LOCKED-OK** - Load a pre-B1 save: empty memory, preserved identity/money/committed terms, inactive B until the next logical midnight, then new captures. Repeat a lower-capacity load without trimming; test ordinary subsequent replacement. Verify future/unknown capability refusal preserves its payload.
8. **LOCKED-OK** - In the normal combined 6 Nov session, interleave matched idle/breakfast/lunch/evening windows from the same checkpoint with memory active versus `FoodMemoryWeight:0`. Report combined **mod-work p50/p95/max**, require p95 <5 ms, flag every spike **>=30 ms** with condition/component, track whole-frame/stalls separately, plus route/query counts, allocations/GC, pending work/catch-up lag and saved byte size. Use bridge-accessible read/perf paths; no mouse/keyboard is required for these windows.


## Dated development notice

> **Step B1 memory and loyalty v2 — 31 October:** bounded per-consumer generation memory, honest exactly-once service experiences, lazy decay/replacement and captured loyal/curious A3 weights now run under schema 18. Offline tests/build and measurements are recorded in [CHECKS](#step-b1-shop-memory-and-loyalty-v2--31-october-2026-offline-only); native accounting/replay/performance proof remains pending the combined 6 Nov rig. B2 presentation remains separate.
