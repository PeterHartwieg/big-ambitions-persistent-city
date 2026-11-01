# C2a core bounded awareness

## Sol-r1-1 — failure memory admission (1 November 2026, offline only)

The integration admitted every unexpired B1 entry, so a first failed visit could
create a guaranteed memory source where the control had no awareness at all.
A scalar penalty cannot compensate for zero admission becoming positive. A bad
insertion could also evict a remembered competitor whose scalar was below novelty,
and refreshed negative-memory age could outlive the control's source expiry.

Fresh memory admission now requires prior successful service, positive **stored**
affinity and an unexpired entry. B1's existing update/decay and contribution rules
are unchanged: surviving positive failures retain their old age; entries crossing
zero cannot reopen admission through a newly dated penalty. Nonpositive entries
remain available to B1's contribution when other sources sample their shop.

The bounded insertion guard also protects a competitor that could supply memory
awareness on/after the saved activation day, including zero memory strength and
lowered capacity. This uses B1's existing protected-insertion exception and failed
watermark; no new saved field or collection is introduced. Entries expired by
activation remain reclaimable. Already-held images retain their original inputs.
Runtime, schema 19, source/shortlist/route limits and native writers are unchanged.

The regression runs 128 matched seeds for each of nine situations, three named
failures and two reload modes: **54 cases / 6,912 matched comparisons**. Each pair
uses the real memory store, production awareness capture, held shortlist and final
commit, including checkpoint reloads before capture and with the captured decision.
It covers empty insertion, normal/zero-weight/lowered-capacity competitor eviction,
expiry before activation, failure at/past expiry, repeat failures and usable positive
habits. Lowered-capacity cases fill the eight-shop shortlist; route requests stay
within sixteen. The earlier integration test now expects empty source slots for
negative memories instead of endorsing their admission.

**Final evidence:** the final 54-case regression against original `8d44988` Core
produces **48 failures / 6 passes**; the passing cases are positive-habit controls.
With the fix, **2,251/2,251 unit tests and 2,251 discovered cases pass**, including
all 54 new cases and B1's unchanged strict negative-penalty tests. **41/41 foreground
gate commands pass**: required game build, all requested commands, plus the other
regression executables. There are no gained target-choice seeds or new target
admissions in the 6,912 matched comparisons. The evidence pins the identical
regression source and restored candidate Core hashes; the manifest embeds the full
red output, command/source hashes and benchmark data. Game-target DLL SHA-256:
`8e5d1883347382120ff46b63c301cf0a1cb3ad8ea87912e74ff7bfa788bb7cd2`.

Red/green evidence and the final foreground gate are in the
[review manifest](../reviews/2026-11-01-c2a-sync-base-r1-results.json).
Offline evidence is separate from the later combined rig and input-only playtest;
no game, rig, ordinary profile, bridge lease, Steam/Workshop or WSL was used.

## Base sync — 1 November 2026 (offline only)

`pc-c2a-sync-base` integrates approved C2a `9b13698` with development head
`81472e6` through a real two-parent merge. The target still writes schema 18;
C2a retains schema 19 and its frozen 18-to-19 reader. Landed B0/B0b fixes,
B1 memory/loyalty, B2 regulars, C1/C3, D1, F0 and E2c are retained.

B1's existing saved memory supplies the four C2a memory slots while B is active.
Expired entries supply no reference; exact generation lookup prevents inheritance
by replacements. The common shortlist uses B1's existing bounded contribution,
and selected shops retain that scalar in the held image. This adds no saved field,
source slot, candidate, route, writer or per-frame scan. The diagnostic uses the
same rank. B2's projection/inspector code is unchanged.

The D1 observation enum and C2a candidate enum had the same C# name with different
meanings. The latter is now `FoodAwarenessAlternativeKind`; its managed/external
values remain 0/1, so catalogue hashes and persistence stay unchanged.

The dated notices and complete checklists below are historical evidence, preserved
from C2a. For both numbered native checklists, apply these sync amendments:

- Item 4: on the combined head, verify four bounded B1 generation references for
  populated memory, no expired/replacement inheritance and unchanged held
  contributions. Empty/inactive memory still supplies no references. Campaign's
  eight slots remain unknown and unused.
- Item 6: use the batched combined-head locked rig under merge rule v2. The old
  B0b comparison is historical context; an isolated per-PR rig is not a sync gate.
  Preserve the stated autosave, mod-work/spike and separate whole-frame rules.
- Item 7 remains `NEEDS-INPUT` for Peter's 6 November playtest; bridge output
  alone does not establish pointer selection or visible journey continuity.

No game, rig, bridge lease, ordinary profile, Steam/Workshop or WSL was used.
Local results and the complete conflict-resolution inventory are recorded in the
[sync review](../reviews/2026-11-01-c2a-sync-base-pr-description.md) and
[command manifest](../reviews/2026-11-01-c2a-sync-base-results.json).

**Final local validation:** game-target build (warnings as errors), **2,197/2,197
unit tests**, **2,197 discovered cases**, every regression executable (including
C1/C3, awareness, B1/B2, held tuning, interior guard and pickup continuity), all
four Python regression harnesses and all three requested simulations pass.
**45/45 foreground gate commands pass**, including four capacity runs. The command
manifest records output hashes, source hashes, full benchmark data and the merge/
document-preservation audit. DLL SHA-256: `c3deb14e74606651822ce7cd57eacd9e4ee2fc66884ceb4ee6b51b85ada0ce7e`.

| Full memory/table fixture | UTF-8 bytes / characters | Streaming / semantic records | Budget with future reserves | Write / read ms |
| --- | ---: | ---: | ---: | ---: |
| reference | 5,022,655 | 54,584 / 54,581 | not CF | 125.3 / 347.4 |
| consumers5000 | 9,710,819 | 110,132 / 110,129 | not CF | 116.5 / 687.2 |
| CF32 | 192,029,621 | 1,920,132 / 1,920,129 | 220,664,366 (82.204%) | 2261.8 / 7267.2 |
| CF64 | 230,429,621 | 3,520,132 / 3,520,129 | 259,064,366 (96.509%) | 3744.7 / 10890.9 |

Every listed write/read is **>=30 ms**, flagged as separate offline save/load
latency, not normal-frame mod work. The non-null schema-17 migration timings in
the manifest are also >=30 ms and flagged as separate offline migration latency. CF32/CF64's historical schema-17 source exceeds
the legacy input cap and is refused as before; the schema-19 fixture round trips
pass. CF32 retains the 85% goal; CF64 retains its existing >95% warning and passes
the unchanged hard cap. Future-envelope record totals remain **2,111,867** and
**3,711,867**. C2a consumes exactly **9,514 bytes / 24 records** from existing
reserves, with full four-entry B1 memory, watermarks and the 20,000-reference table.
The [perf notes](../perf/c2a-core-awareness.md#base-sync-validation--1-november-2026)
flag the synchronous offline full-index samples above 30 ms separately.

> **C2a review r1:** shared geometry rebuilds now yield in bounded units; eligibility-only flips reuse the index. Existing-stop admission/diagnostics and post-activation null refusal are fixed. **1,911 tests and 33 foreground checks pass**; native rig proof remains pending. [Review evidence](#c2a-review-round-1--31-october-2026-offline-only).

> **Step C2a core awareness ? 31 October:** bounded memory seam (empty), neighbourhood/routine and daily distant discovery feed the existing eight-shop choice and sixteen-route ceiling. Schema **19** adds only one held source image and an activation header; campaign source remains unknown. **1,902 tests pass**, with offline regressions/capacity measurements recorded in [CHECKS](#step-c2a-core-bounded-awareness--31-october-2026-offline-only). Combined 6 Nov rig proof remains pending. [Contract](../population-schema19.md).

## C2a review round 2 - 31 October 2026 (offline only)

Sol-r2-1 and Opus-r2-1 share one cause: stop membership was frozen at food-market
attachment before awareness activation. Restored the legacy live scope lookup
when the published awareness index is absent or stale, including a diagnostic
image built before a late acquisition. A current image still provides indexed
membership; normal post-activation events retain bounded preparation. Stop lookup
does not build geometry. Removed the attachment-only dictionary. No wire or
schema change, native write, game/rig activity or PR metadata edit.

Three new regression cases reproduce a missing held stop decision on the reviewed
source, while the reloaded checkpoint recognizes the acquired shop. They compare
uninterrupted/reloaded/inspected decisions and final saved state on day 1 with
activation on day 2, with no diagnostic, a diagnostic before acquisition, and a
diagnostic after acquisition. Fix **49158aa** passes all three cases, with identical
held decisions, final saves and orders and zero route requests. **1,914/1,914 tests**,
test discovery and **33/33 foreground gate commands** pass (all 31 requested plus
the native-candidate and food-choice regressions). [Command manifest and embedded
red/green evidence](../reviews/2026-10-31-c2a-r2-offline-checks.json).
The save-contract regression passes unchanged; no payload bytes were added.
The existing numbered 6 Nov rig checklist below still applies; native proof remains
pending.

## C2a review round 1 — 31 October 2026 (offline only)

Fix commit **19289e3** on submitted **9fa59fa**. Addresses Sol-r1-2/3 and
Opus-r1-1/2/3/4. The actual PR #24 body is updated separately for Sol-r1-4,
preserving its harness footer. No other slice, schema allocation or native writer
is added. [Updated contract](../population-schema19.md).

Five targeted red cases reproduce both dense at-stop failures (draws 0/4), the
mismatched diagnostic, post-activation null acceptance and inline rebuild/capture.
All turn green. Geometry construction now has a private resumable collector,
merge sort, canonical hash and cell passes, with **64 items maximum per unit**.
Even invalid/missing places consume collector work instead of hiding a scan in an
iterator. The next logical event and minute stay pinned until coherent publication;
no meal context is consumed. Changed input discards/restarts the partial image;
reload replays the unconsumed event. Work shares the existing total-unit/time caps
and continuation counters. Normal stop/decision/commit paths never synchronously
construct an index. Candidacy-only changes retain the geometry revision.

At a shop stop, its supported generation takes **Origin slot 0** even when random
draws are disabled. It replaces an origin draw within the existing 24/32/8/16
bounds; price/stock/hours/deadline/outside choice still apply. The diagnostic uses
the same held/current stop mode and captured shortlist coefficients. Schema 19's
unchanged wire now refuses a null awareness image on/after the meal's activation
day; pre-activation/schema-18 held decisions still replay without reranking.

**Validation:** **1,911/1,911 tests**, **49 awareness cases** (nine new review cases),
discovery, all 31 requested foreground commands and native-candidate/choice
regressions pass: **33/33 gate commands**.
[Command manifest](../reviews/2026-10-31-c2a-r1-offline-checks.json).
The native-source harness asserts eligibility changes leave the revision unchanged
across discovery cursor positions and reload. Other tests cover coherent partial
publication, 1,024/20k work bounds, replacement restart, reload/cadence identity,
dense at-stop admission and diagnostic equality. The save-contract regression
retains all exact historical economy/order/reload digests. No payload field or
byte/record allocation changes; schema 18 migration remains valid.

**Rebuild work-unit measurement:** six complete builds/layout/process, three fresh
sequential .NET 10 Release processes, including initial/cold/JIT work. Sizes 1,024
and 20,000; dense (10,0,0) and 125 m sparse grid, matching the original benchmark.
Unit timers include initial builder creation, the managed-scope entry projection
and place lookup; counters/stats are outside.
Total CPU is the sum across all yielded units, not a single frame or spike.
[Red symptom and repeatable measurements](../reviews/2026-10-31-c2a-r1-rebuild.json).

| Alternatives / layout | Work unit p50 / p95 / max ms | Measured units | Max items/unit | Whole build summed CPU p50 / max ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1,024 / dense | 0.0033 / 0.0121 / 5.5345 | 7,362 | 64 | 1.9222 / 16.0049 |
| 20,000 / dense | 0.0027 / 0.0090 / 3.7794 | 169,020 | 64 | 36.3103 / 42.2991 |
| 1,024 / sparse | 0.0035 / 0.0123 / 0.3889 | 8,172 | 64 | 2.1441 / 2.6267 |
| 20,000 / sparse | 0.0021 / 0.0095 / 11.8403 | 187,344 | 64 | 41.1984 / 53.3200 |

Across **371,898 work units**, **0 units >=30 ms**, largest **11.8403 ms**.
The old normal-frame synchronous rebuild path had 40 >=30 ms samples, max78.1916 ms;
those whole-build costs are now split into bounded continuations. Six full builds
per case/process give 409 /9,390 /454 /10,408 units per dense1k/dense20k/sparse1k/
sparse20k build: at the unchanged 128-unit cap, 4 /74 /4 /82 budget-only frames,
with the existing time cap potentially yielding sooner. These totals expose
completion/catch-up cost; yielding does not consume meals or advance event order.
Eligibility-only changes incur **zero geometry rebuilds**, covered by native-source
cursor/reload tests. Source-slot/shortlist/route limits and persisted bytes stay fixed.
Native combined mod-work p95 <5 ms and <30 ms spikes remain subject to the combined
rig session; whole-frame timing remains separate. The four parked hotspots are
untouched. No game, rig, ordinary save, ModsLocal, bridge profile or lease was
touched, and no push or merge occurred.

### In-game checks for 6 Nov rig

Pending: a `rig_test` session must use a named disposable FT fixture and attach
before/red ? after/green bridge dumps/logs or captures to the PR. The locked-PC
classification follows [84be71a's feasibility report](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/84be71a/docs/reviews/rig/2026-10-31-locked-session-feasibility/README.md).
No rig action was performed by this implementation run.

1. **LOCKED-OK ? source/position capture.** On the same seeded disposable checkpoint,
   call `PopulationRuntime.FoodAwareness(consumerId, day)` for a known resident and
   represented slot. Join sampled business IDs to saved scopes/places and native
   shop entrances; verify home/work/current distances, <=9 cells per anchor,
   <=4 local draws each, raw union/dedup flags, <=24 core slots, eight reserved
   unknown campaign slots, <=8 shortlist and <=16 actual decision route requests.
   Repeat without state changes and after reload; attach matching dumps. Include
   at-stop contexts in a dense catalogue with random draws 0 and 4: the present
   eligible shop must occupy Origin slot 0 inside the existing bounds.
2. **LOCKED-OK ? distant opportunity.** Select a supported player shop beyond every
   local anchor radius. Derive seeded logical days through the read-only dump in
   bounded batches until its discovery sample is **eligible**, then show that it
   shares the ordinary shortlist competition. Record consumer/seed/day/revision
   and native position. An admission is neither a committed journey nor a sale.
3. **LOCKED-OK ? refusal after admission.** On matched disposable fixture copies,
   retain a sampled generation while testing unaffordable price, unavailable
   stock, closed hours and an insufficient deadline. Record captured eligibility,
   existing feasibility reasons, zero forbidden purchases and <=16 routes.
   Compare changed taste/household preferences through recorded choices across
   seeds; awareness must not guarantee a purchase or bypass outside choice.
4. **LOCKED-OK ? campaign and memory boundaries.** Verify all eight campaign slots
   say unknown, no campaign source bit/admission appears, and the production memory
   source is empty. Native campaigns may exist but must have no C2a provider call,
   saved mask, discount or capacity effect. Attach the consumer dump and schema.
5. **LOCKED-OK ? migration/replay/size.** Make a disposable copy of a baseline
   schema-18 checkpoint; load with candidate, verify schema 19/next-day activation,
   unchanged IDs/wallets/committed terms, then save/reload at capture, partial quote
   and selected boundaries. Compare IDs, flags, shortlist, quote cursor, money and
   purchase identity against uninterrupted replay under different event budgets.
   Edit tuning only while unloaded: held contexts retain their image and the next
   fresh context uses new values that same day. Record exact saved-size delta;
   preserve refused/malformed source copies and leave ordinary saves untouched.
6. **LOCKED-OK ? matched performance windows.** Interleave candidate and B0b
   `c0af745` at idle and breakfast/lunch/evening rush, matching checkpoint, seed,
   shops, population, clock/body/graphics settings, cache warmth and panel state;
   include a soak and a catalogue-change/rebuild observation. Verify rebuild
   work yields between units, blocks publication until complete and restarts
   coherently on replacement; eligibility-only flips must perform zero rebuilds. Record mod-work
   p50/p95/max, **every spike >=30 ms**, route/query counts, allocations/GC,
   pending meal work and catch-up latency. Require mod-work p95 <5 ms; record
   whole-frame intervals/stalls **separately**. Record original autosave, disable
   and verify off before **each** window and after reload. Keep manual saves
   outside windows; invalidate any window containing save activity. Restore the
   original autosave/preferences/fixtures during cleanup. No separate save-profile
   timing run is implied.
7. **NEEDS-INPUT ? visible journey sanity.** In the unlocked 6 Nov session, select
   and follow a consumer whose admitted offer survives feasibility through the
   storefront visit/service/return; visually verify continuous travel and the
   existing inspector's individual explanation. Repeat a refusal case. Bridge
   dumps alone do not establish pointer selection or visible continuity.

## Step C2a core bounded awareness ? 31 October 2026 (offline only)

Base **c0af745 (B0b)**; Core/index implementation **b50c756**, final config/refusal
coverage **0d79664**. Only C2a is implemented. B1/B2, C1, D1 and F0 were not merged
or copied. The remote B?E plan was read through Git; it is absent in this ancestry.
[Schema 19 and interface contract](../population-schema19.md).

Memory is an empty four-reference provider seam. Neighbourhood/routine uses up to
four mass-weighted indexed draws each for home, work/study and current origin,
with nine cells/anchor and no dense-cell scan. Eight positive daily discovery
slots cover the complete place-backed catalogue irrespective of distance.
The campaign seam is always unknown and never called, even if a nonempty provider
is injected. There are **24 C2a / 32 reserved-total source slots**, deterministic
union/dedup without refill, the same weighted **eight-shop** shortlist, existing
complete feasibility/trait/offer/outside choice and **<=16 route requests**.
Plain external-kind metadata can share these slots later without enrollment in
player authority; C2a supplies no rival reader, journey or settlement.

Indexes rebuild on publication/place change or first use. Steady captures do
bounded source work and direct generation lookups, without an all-shop predicate,
per-frame reflection/scan/polling/allocation or resident-by-shop matrix. Native
capture removes the inherited full controller and routine-hours sweeps; bounded
sampled eligibility and live winning-shop terms retain factual refusals. The four
parked hotspots remain parked. New configuration stays flat, clamps/defaults once,
handles numeric strings and nonfinite endpoints, and changes the next uncaptured
context. The read-only bridge method `PopulationRuntime.FoodAwareness(id, day?)`
dumps source slots/union/dedup/proposed shortlist plus the separately labelled
held image/shortlist; it creates no context, route or native write.

**Validation:** **1,902/1,902 tests**, including **40 C2a cases**, test discovery,
all **31 requested foreground commands** and the native-candidate/food-choice
regressions pass (33 gate commands). [Command manifest](../reviews/2026-10-31-c2a-offline-checks.json).
Final game-target DLL SHA-256: `4401ed1512d28a8ea296f2d3a5c22921ac1522cb28c36af43e4f0e34e4932818`.
All owned test/run processes exited; compiler/build servers shut down.
Coverage includes permutation, dense-cell last-entry reach, empty/replaced
catalogues, all source/union/route bounds at 1,024 and 20,000, explicit empty/unknown
and nonempty campaign injection, empty/four-reference memory, distant discovery,
local refusal without refill, price/stock/hours/deadline refusal, taste/household
choice, capture/partial-quote/selected replay, changed cadence/config, read-only
inspection, 18->19 migration, strict 18 field refusal and malformed-image/source
preservation. One radius test was corrected to permit a legitimate discovery
exposure; its original failed attempt is retained. Historical economy remains
**849 orders / 797 purchases / 107,967,424,757,760 units**, with all three exact
order/economy/reload digests unchanged. The population-only wire stays 119,083
characters; its digest changes solely with schema 18->19.

**Offline performance.** [Measurement manifests and reproducible commands](../reviews/2026-10-31-c2a-measurements.json),
`tools/awareness-regression` on Windows .NET 10.0.13 Release, seed 77, one consumer,
radius 250 m, four draws/anchor, discovery .02 and a four-reference memory double.
Dense positions are (10,0,0); sparse positions are a 125 m grid, 200 columns; all
anchors are (0,0,0). Three fresh sequential processes, 300 warmups then 3,000
measured contexts/layout/process; before/after order alternates per context.
B0b's metadata rank loop uses the same weights/IDs/positions; native supply,
routes and scope copies are excluded from both timers. Index cost is separate:
two startup/JIT warmups, then ten measured builds/process/layout. Table p50/p95
are medians of three run percentiles; max is the largest observed value. System
load is uncontrolled; all maxima remain visible.

| Alternatives / layout | B0b rank p50 / p95 / max ms | C2a source+union+rank p50 / p95 / max ms | B0b / C2a bytes per context | Index build/rebuild p50 / p95 / max ms | Build allocated bytes |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1,024 / dense | 0.1073 / 0.2393 / 5.5627 | 0.0116 / 0.0436 / 0.8222 | 306 / 987 | 2.0451 / 26.4999 / 27.2204 | 275,640 |
| 20,000 / dense | 3.5486 / 4.5085 / 8.5244 | 0.0290 / 0.0486 / 0.8597 | 280 / 885 | 30.7147 / 41.7452 / 72.9799 | 4,254,290 |
| 1,024 / sparse | 0.1249 / 0.1613 / 1.0213 | 0.0057 / 0.0120 / 0.1534 | 280 / 827 | 2.3263 / 9.4777 / 10.4809 | 692,054 |
| 20,000 / sparse | 3.5047 / 4.2679 / 6.2920 | 0.0175 / 0.0322 / 0.7737 | 280 / 827 | 43.2643 / 73.4230 / 78.1916 | 13,475,542 |

Across **36,000 measured C2a contexts**, there are **zero >=30 ms source/rank
samples**, at most eight shortlisted shops and sixteen logical route slots.
Production Core tests separately count actual planner requests at both catalogue
sizes and bound metadata predicates to 24 and detailed captures to eight.
**All 40 measured index rebuild spikes >=30 ms are flagged:** 16 dense/20k
(max **72.9799 ms**) and 24 sparse/20k (max **78.1916 ms**). The 1,024 rebuild
samples have none. These event-only 20k costs are a retained limitation for future
large catalogues and the combined rig gate; there is no claim that ordinary native
mod-work p95 <5 ms or the <30 ms spike gate has been established here. Native
mod-work and whole-frame results remain separate and pending the combined rig.
The inherited A3-only benchmark (pre-awareness activation) has 1,024/native-ID
policy p95 **0.2117 ms**, above its 0.20 ms engineering target; its full-decision
p95 is 0.2960 ms and largest measured decision across cases 10.1772 ms.

Cheap measured wins are logged in the roadmap: prefix reuse/bounded dedup reduce
the first dense capture prototype's ~4.4?4.6 KB/context to 0.9?1.0 KB here;
streamed binary catalogue hashing and shared per-scale ordinal arrays reduce
20k dense/sparse build allocation **17.48/32.00 MB -> 4.25/13.48 MB**. Build wall
time remains variable and the >=30 ms samples above are not hidden by those wins.
The actual native-candidate/hours harness verifies **zero** full capture sweeps,
eight requested detailed reads and cold/reload/cursor-independent sampled refusal.
Its unbatched eight-read control is 0.1105 ms median versus the new batch's 0.0094 ms;
this synthetic control is not a matched native B0b frame comparison.

**Save/allocation proof.** Schema **19** adds an activation header and the one held
source image only. Schema 18's reader is frozen, rejects new fields, validates old
inputs and activates fresh awareness on the next logical day; an inherited held
A3 context finishes its original shortlist/quotes without reranking. Malformed or
future inputs remain preserved. There are no campaign images, daily discovered
ID lists, extra consumer tuples, memory writes or changed money/menu caps.

| Consumers | Extra persistent bytes per idle consumer/day | One held image incl. field | City total incl. header | Managed image graph (shared IDs) |
| --- | ---: | ---: | ---: | ---: |
| Reference: 957 residents / 1,914 consumers | 0 | 3,051 | **3,074** | 1,048 |
| 2,500 residents / 5,000 consumers | 0 | 3,051 | **3,074** | 1,048 |

These lean fixtures use 24 unique native-format IDs and the same eight-shop held
terms before/after: **3,038 image + 13 field + 23 city-header bytes**. Managed image
allocation excludes its holder reference and already-shared identity strings.
No held image costs only the city header (23 bytes at a one-digit activation day);
a grandfathered held null adds 17 bytes. Header digit growth is charged normally.

Full-memory/table/visit fixtures below stress sixteen 512-character generation
IDs alongside the existing eight short held IDs. Every row adds exactly **9,514
bytes** above B0b and **24 records** to both counters. All old B0 facts remain.

| Full B0 fixture | Schema-19 UTF-8 bytes / characters | Delta from B0b | Streaming / semantic records | Write / read / 17 migration ms |
| --- | ---: | ---: | ---: | ---: |
| reference | 5,022,655 | +9,514 | 54,584 / 54,581 | 129.3 / 371.2 / 559.3 |
| consumers5000 | 9,710,819 | +9,514 | 110,132 / 110,129 | 383.8 / 946.7 / 1944.0 |
| CF32 | 192,029,621 | +9,514 | 1,920,132 / 1,920,129 | 2,757.4 / 9,066.3 / source refused |
| CF64 | 230,429,621 | +9,514 | 3,520,132 / 3,520,129 | 3,512.3 / 10,453.1 / source refused |

**Every listed write/read and non-null migration timing is >=30 ms and explicitly
flagged as separate offline save/startup/reload latency.** These single samples
are size/regression evidence, not native mod-work p95. CF32/CF64 retain the exact
B0b future-envelope totals: **220,664,366 bytes / 2,111,867 records (82.204%)** and
**259,064,366 / 3,711,867 (96.509%)**. Actual C2a consumption is deducted once
from the existing 64 KiB headers / 1 MiB decision and record reserves. CF32 meets
the 85% goal; CF64 retains the existing >95% warning and passes the unchanged hard
caps. No global cap or per-consumer allocation is increased.

No game, test rig, ordinary save, ModsLocal, bridge profile or rig lease was touched.
Native behavior/accounting, mod-work p95/spikes and visible journeys still require
rig proof. No push, PR creation or merge was performed.

### In-game checks for 6 Nov rig

Pending: a `rig_test` session must use a named disposable FT fixture and attach
before/red ? after/green bridge dumps/logs or captures to the PR. The locked-PC
classification follows [84be71a's feasibility report](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/84be71a/docs/reviews/rig/2026-10-31-locked-session-feasibility/README.md).
No rig action was performed by this implementation run.

1. **LOCKED-OK ? source/position capture.** On the same seeded disposable checkpoint,
   call `PopulationRuntime.FoodAwareness(consumerId, day)` for a known resident and
   represented slot. Join sampled business IDs to saved scopes/places and native
   shop entrances; verify home/work/current distances, <=9 cells per anchor,
   <=4 local draws each, raw union/dedup flags, <=24 core slots, eight reserved
   unknown campaign slots, <=8 shortlist and <=16 actual decision route requests.
   Repeat without state changes and after reload; attach matching dumps.
2. **LOCKED-OK ? distant opportunity.** Select a supported player shop beyond every
   local anchor radius. Derive seeded logical days through the read-only dump in
   bounded batches until its discovery sample is **eligible**, then show that it
   shares the ordinary shortlist competition. Record consumer/seed/day/revision
   and native position. An admission is neither a committed journey nor a sale.
3. **LOCKED-OK ? refusal after admission.** On matched disposable fixture copies,
   retain a sampled generation while testing unaffordable price, unavailable
   stock, closed hours and an insufficient deadline. Record captured eligibility,
   existing feasibility reasons, zero forbidden purchases and <=16 routes.
   Compare changed taste/household preferences through recorded choices across
   seeds; awareness must not guarantee a purchase or bypass outside choice.
4. **LOCKED-OK ? campaign and memory boundaries.** Verify all eight campaign slots
   say unknown, no campaign source bit/admission appears, and the production memory
   source is empty. Native campaigns may exist but must have no C2a provider call,
   saved mask, discount or capacity effect. Attach the consumer dump and schema.
5. **LOCKED-OK ? migration/replay/size.** Make a disposable copy of a baseline
   schema-18 checkpoint; load with candidate, verify schema 19/next-day activation,
   unchanged IDs/wallets/committed terms, then save/reload at capture, partial quote
   and selected boundaries. Compare IDs, flags, shortlist, quote cursor, money and
   purchase identity against uninterrupted replay under different event budgets.
   Edit tuning only while unloaded: held contexts retain their image and the next
   fresh context uses new values that same day. Record exact saved-size delta;
   preserve refused/malformed source copies and leave ordinary saves untouched.
6. **LOCKED-OK ? matched performance windows.** Interleave candidate and B0b
   `c0af745` at idle and breakfast/lunch/evening rush, matching checkpoint, seed,
   shops, population, clock/body/graphics settings, cache warmth and panel state;
   include a soak and a catalogue-change/rebuild observation. Record mod-work
   p50/p95/max, **every spike >=30 ms**, route/query counts, allocations/GC,
   pending meal work and catch-up latency. Require mod-work p95 <5 ms; record
   whole-frame intervals/stalls **separately**. Record original autosave, disable
   and verify off before **each** window and after reload. Keep manual saves
   outside windows; invalidate any window containing save activity. Restore the
   original autosave/preferences/fixtures during cleanup. No separate save-profile
   timing run is implied.
7. **NEEDS-INPUT ? visible journey sanity.** In the unlocked 6 Nov session, select
   and follow a consumer whose admitted offer survives feasibility through the
   storefront visit/service/return; visually verify continuous travel and the
   existing inspector's individual explanation. Repeat a refusal case. Bridge
   dumps alone do not establish pointer selection or visible continuity.

[Checks index](../../CHECKS.md).
