# D2: rival choice and own-ledger outflow

Measurement provenance: the original D2 timings/byte tables and round-one repair
results below were measured on the D2 line based on `51ce709`, before E1 schema
20. They remain historical evidence, not current merged-head measurements. The
base-sync results are recorded separately in D2 checks; no old number is relabelled.

## 1 November 2026 — offline implementation

Base `51ce709`; branch `codex/pc-d2-rival-outflow`. D2 adds read-only rivals to
C2a's common source/shortlist/route path, timed physical/represented outings,
arrival/completion revalidation and exactly-once own-ledger settlement. Unknown
native stock/capacity remains unknown; availability and service duration are
explicit estimates. Rival payments cannot create native sale receipts, native
history revenue or B1 experience. Campaigns remain unknown and unused.

Schema **21** has an isolated, frozen 19-source reader/migration. Schema 20 belongs
to the separate rain slice and is refused here. The [contract](../population-schema21.md)
records the compact target/account/outcome variants, exact conservation identity,
unchanged caps and zero extra consumer-record charge. Schema 17–19 contract
documents, native receipt formats, menus and RLO source are unchanged.

**Initial offline validation (0858529):** all **39/39** required-plus-D2 foreground gate commands
pass, including the final game-assembly build, **2,380/2,380** unit tests and discovery,
the requested regressions/Python harnesses/demos, awareness/candidate regressions
and the new **19,828-assertion** D2 native-double/measurement harness. Four capacity
cases also pass. The final full suite includes the unknown-stock allocation win
and both integer and fractional native-price conservation checks.
This is not native behavior or mod-work performance proof. The game, rig directory,
ModsLocal, bridge profile and rig lease were not accessed. Steam assemblies were
read-only compilation inputs.

The save-contract regression retains the published schema-19 population-only
length **119,083** and every economy result from the C2a sync baseline: **849
orders, 797 purchases, 107,967,424,757,760 spent units, two uncertain receipts**,
with identical order/economy/reload digests. Only the schema header/digest and
documented schema-21 food additions differ. This comparison is captured in the
offline results manifest; player checkout/receipt regressions also pass directly.

The reproducible gate is `python tools/run-d2-checks.py`; every test/run invokes
the required other-dotnet wait first and runs synchronously without an execution
deadline. Ignored raw logs/manifests are under `build/d2/gates/`. The new
`tools/rival-outflow-regression` compiles the actual D1 reader and D2 adapter
against throwing native API/field doubles. D1's existing emitted-IL native
write/API guards remain in the required catalogue regression.

The committed [initial results manifest](../reviews/2026-11-01-d2-rival-outflow-results.json)
retains commands, exit codes, source/log hashes, complete benchmark JSON and the
published schema-19 comparison. Implementation/proof commits are `72971d3` and
`988d4c6`; initial tested DLL SHA-256 is
`72435f39a79abce0bf3ab0b4c9dd3bf349259dccffd6b38fc1ff43480f0e32bc`.

Tests cover slot-zero/represented trips and returns, reload at held/route/service/
payment/return boundaries, held tuning with new weight zero, withdrawal/changed
price/hours/closure/ownership, empty B1/campaign sources, unchanged player receipts,
exact accounts, conservation/overflow and malformed/foreign-schema preservation.
The native-double harness compares complete fixture fields, including stock and
finance canaries. These are offline checks, not native evidence.

## 1 November 2026 — round-one review fixes

`Opus-r1-1`: metadata refresh now records whether an existing registration
advertises any known food item; null/blank labels remain unsupported. Non-food entries publish no food scope, fail the
candidate predicate and receive no detail read or shortlist slot. In-place food
additions/removals change the shared metadata revision. Refresh still reads only
existing fields in bounded units, with no per-consumer product scan.

`Opus-r1-2`: native rival refresh exceptions retire only the rival source for
that loaded city, invalidate its index and emit one warning. No retry/poll loop,
candidate or target read survives. Player commerce and population continue;
saved rival commitments take the existing no-debit refusal and return path.
The read-only bridge line includes the source's failure reason on request.

`Opus-r1-3`: rival capture no longer replaces persistent place hours. Departure
uses the captured detached schedule; only the owning meal outing defers door
admission to its live rival check. Ordinary routines keep their original hours.
The successful and withdrawn-choice tests preserve the original hours object
and byte-identical serialized place through capture, service/cancellation and return.

The focused red cases reproduced shortlist displacement, an escaping refresh
exception in both fresh and cold-reload simulations, and replaced saved hours.
All focused green cases pass: seven non-food checks, five source-failure checks,
four saved-failure checks and all **44 D2 unit cases**. **39/39 distinct gate
commands are green**, including the full **2,381/2,381** suite and discovery,
**19,844** D2 assertions, **358** D1 read-only guards plus 29 catalogue checks,
**336** actual checkout checks, all required regressions/Python harnesses/demos
and four capacity cases. Code repairs are `7a8545f`, `f11600c` and `b435be7`.
The standalone checkout build initially exposed warning storage declared in an
optional partial it omits; storage now lives beside the constructor. Checkout
logic was not changed. After the Runtime-only storage/null-label corrections,
the final production build, catalogue, checkout and complete D2 harness reran and
passed; unit-test inputs did not change after the full-suite pass. The
[round-one results](../reviews/2026-11-01-d2-rival-outflow-r1-results.json) retain
commands, hashes, red/green tails and both measurement runs. The final DLL SHA-256
is `eb75824b5e8767d17a7b7df4765d5b34b762757129a967f01422703eb40aacb7`.
Original measurements below remain initial evidence. No game or rig action is
part of this repair.

First round-one shared-host offline rerun, same bounded benchmark protocol:

| Alternatives | Warm source/rank p50 / p95 / max ms | Warm refresh-unit p95 / max ms | Reader retained bytes |
| --- | ---: | ---: | ---: |
| 1,024 | 0.0170 / 0.0276 / 0.1649 | 0.0044 / 0.0198 | 720,808 |
| 20,000 | 0.0217 / 0.0353 / 0.2386 | 0.0050 / 0.0420 | 14,092,928 |

Source allocation remains 1,563/1,566 bytes/context, warm refresh remains 32
bytes/complete refresh and zero otherwise, settlement p95 **0.0001 ms**, max
**0.1785 ms**, **192 bytes/payment**. The 32/8/16 bounds and zero-weight checks
still pass; no individual D2 micro-sample is >=30 ms. One rerun offline 128-unit
batch is **>=30 ms, max 31.7774 ms**, flagged without finer attribution. Its
p50/p95 is 1.0656/2.5225 ms at 20k (1.4509/2.3904, max 10.6544 ms at 1,024).
The D1 aggregate cold-cache mean **50.0471 ms >=30 ms** is also flagged; it spans
many bounded units. Neither result is native mod-work or whole-frame evidence.
Idle save bytes and record counts are exactly the initial values below.

The **final** native-double rerun includes null/blank advertised-label coverage:

| Alternatives | Warm source/rank p50 / p95 / max ms | Warm refresh-unit p95 / max ms |
| --- | ---: | ---: |
| 1,024 | 0.0187 / 0.0332 / 0.3418 | 0.0059 / 0.0214 |
| 20,000 | 0.0260 / 0.0441 / 0.3405 | 0.0166 / 0.1939 |

Allocation/retention and the 32/8/16 bounds are unchanged from the first rerun.
Settlement p50/p95/max is **0.0001/0.0004/0.2696 ms**, 192 bytes/payment.
No individual D2 micro-sample is >=30 ms. **Three final offline 128-unit batches
are >=30 ms, max 90.6681 ms**, flagged with no finer attribution than the whole
`AdvanceTo` batch. Final batch p50/p95 is 1.0435/2.9578 ms at 20k;
1,024 has 2.0633/3.6534 ms, max 13.1952 ms, zero >=30 ms samples. The final D1
aggregate cold 20k mean **53.2260 ms >=30 ms** spans many bounded units and is
also flagged. Shared-host offline batches are not native mod-work or whole-frame
samples, and do not certify the pending combined 6 Nov gate.

Full capacity rerun payload bytes, record counts and future envelopes also match
the initial figures exactly. **Standalone save latencies >=30 ms are flagged**:
write/read/migration-from-17 milliseconds are reference **192.9/497.8/735.3**,
5k **451.3/1,097.2/1,730.9**, CF32 **3,647.1/12,587.2/refused** and CF64
**3,831.1/8,960.9/refused**. The two CF migration refusals retain the old input
size limit. These single shared-host whole-operation samples are not native
mod-work or frame evidence; serialization remains parked.

## Performance and save allocation

Foreground Windows .NET 10 measurements on the shared host; fixture creation is
excluded. A cold decision is the first draw on a new index with JIT exercised;
warm decisions have
50 warmups and 1,000 measured contexts. Shared refresh/index work is continued
separately. Production decision predicates/details are bounded in the tests.
The 20,000-alternative microbenchmark tests the decision envelope; saved cities'
unchanged 20,000-place cap also includes homes and other places.

| Alternatives | Source/rank cold ms / bytes | Warm p50 / p95 / max ms | Warm bytes/context | Max occupied union / shortlist / route slots |
| --- | ---: | ---: | ---: | ---: |
| 1,024 | 0.2928 / 1,640 | 0.0165 / 0.0189 / 0.0557 | 1,563.072 | 17 / 8 / 16 |
| 20,000 | 0.0716 / 1,496 | 0.0251 / 0.0438 / 0.2239 | 1,566.032 | 17 / 8 / 16 |

The raw source budget remains 32 (24 C2a slots plus eight unknown campaigns),
including empty/refused draws. The production tests permit <=24 predicates,
<=8 captured detail reads plus one selected-target revalidation, and <=16 actual
decision route calls. Neither decisions nor shortlisting scan shops. Cold sample
ordering/JIT differences are not interpreted as better scaling at 20,000.

| Alternatives | Cold 16-read unit p95 / max ms | Warm unit p95 / max ms | Warm allocation | Index 64-item unit p95 / max ms | Retained reader bytes |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1,024 | 0.0268 / 0.3220 | 0.0041 / 0.0167 | 32 bytes/refresh; 0 otherwise | 0.0099 / 0.3124 | 720,464 |
| 20,000 | 0.0352 / 17.9271 | 0.0096 / 0.0509 | 32 bytes/refresh; 0 otherwise | 0.0135 / 0.4652 | 14,092,896 |

Retention is a forced-GC managed heap delta: native fixtures and unmanaged weak
handles are excluded; small benchmark timing containers are included. Unchanged
refreshes retain the index revision. No refresh/index/source/read/settlement
micro-sample reached 30 ms. The inherited D1 aggregate cold-cache benchmark's
20,000-place mean **47.2054 ms >=30 ms** is flagged separately: many bounded
units, never one drained frame; it is not a native mod-work sample.

Atomic Core settlement (10,000 prepared wallets): p50/p95 **0.0001/0.0001 ms**,
max **0.1223 ms**, **192 bytes/payment**, no >=30 ms samples. The zero-weight
unit test records **zero** new refresh checks/units, predicates, detail reads and
purchases; cold saved commitments/held contexts still resume under weight zero.

| 1,914 consumers, three-meal logical day | 1,024 alternatives | 20,000 alternatives |
| --- | ---: | ---: |
| Total CPU / allocated bytes | 1,525.1898 ms / 655,742,384 | 1,846.1956 ms / 676,648,888 |
| Work units / continuations | 123,120 / 98,652 | 147,974 / 123,261 |
| Routes / detail reads / refresh units | 95,401 / 57,063 / 845 | 95,660 / 57,315 / 16,263 |
| Modeled purchases / pending meals at catch-up | 3,745 / 0 | 3,794 / 0 |
| Offline 128-unit batch p50 / p95 / max ms | 1.3798 / 3.0158 / 12.7730 | 1.0363 / 3.8581 / 47.7935 |
| Offline batch samples >=30 ms | 0 | **2, max 47.7935 ms** |

**Both >=30 ms offline batches are flagged.** Their component is the unisolated
128-unit `CitySimulator.AdvanceTo` batch, including shared publication; no finer
attribution was recorded. These fixed-budget fake-route batches have no native
soft-time cutoff and are neither whole-frame cost nor a native mod-work gate.
All wallets conserve funds, all returns complete and the simulation catches up
to minute 2869. Aggregate allocations include the existing choice/routes/household
pipeline; they are not claimed as D2-only allocations. No native write occurred.

| Idle household consumers | Schema-19 bytes | Schema-21 bytes | Delta | Streaming / semantic records, both schemas |
| --- | ---: | ---: | ---: | ---: |
| 1,914 | 2,263,487 | 2,273,076 | +9,589 | 3,829 / 3,829 |
| 5,000 | 5,918,915 | 5,943,934 | +25,019 | 10,001 / 10,001 |

Exactly **5 bytes/idle household consumer + a 19-byte city header**; no extra
record. Full targets remain <=24 bytes and accounts <=20 bytes in existing tuples;
outcome target encoding adds zero bytes. The capacity driver subtracts consumed
D2 bytes once from existing reserves; it adds no record reserve charge.

Full capacity fixtures against the published schema-19 baseline:

| Fixture | Schema-19 bytes | Schema-21 bytes / delta | Streaming / semantic records, unchanged | Budgeted bytes / records |
| --- | ---: | ---: | ---: | ---: |
| Reference (1,914 consumers) | 5,022,655 | 5,033,396 / +10,741 | 54,584 / 54,581 | — |
| 5,000 consumers | 9,710,819 | 9,736,990 / +26,171 | 110,132 / 110,129 | — |
| CF32 (150,000 consumers) | 192,029,621 | 192,780,792 / +751,171 | 1,920,132 / 1,920,129 | 220,664,366 / 2,111,867 |
| CF64 (150,000 consumers) | 230,429,621 | 231,180,792 / +751,171 | 3,520,132 / 3,520,129 | 259,064,366 / 3,711,867 |

The two full future envelopes remain **82.204% / 96.509%** of 256 MiB,
identical to the schema-19 allocation; the 480 additional awareness bytes and
750,691 actual rival bytes at CF scale consume their existing reserves once.
Record budgets are unchanged. CF32/CF64 retain the existing oversized schema-17
migration refusal; current-schema reload succeeds. Strict direct 19→21 migration
and source preservation are exercised separately by unit tests.

**Save latency >=30 ms is flagged separately:** one foreground sample each,
write/read/migration-from-17 milliseconds: reference **119.9/343.3/472.8**,
5,000 **212.7/746.2/1,377.8**, CF32 **2,540.5/9,708.8/refused**, CF64
**3,285.0/9,022.9/refused**. These are standalone whole-operation latency,
not frame or mod-work samples. Archived schema-19 write/read samples were
119.2/335.0, 239.5/747.5, 1,768.5/6,187.6 and 2,617.8/7,423.9 respectively;
shared-host single runs do not isolate causes or establish a matched speedup.
CF read/write and allocation costs remain material; the serializer stays parked.

Taken low-risk wins: one detached arrival read instead of two, reuse its hours
projection, direct indexing of the one-item detail result, cached metadata scopes
and no index invalidation on unchanged refresh. [Measurements and limits](../perf/d2-rival-outflow.md).
Physical population, closure refresh, optional-stop refresh and save serialization
remain parked. No per-frame reflection, catalogue scan/polling or allocation is
added. Bridge strings/totals are built only when requested.

Combined native **mod-work p95 <5 ms** and **max <30 ms** remain to be measured.
Flag every mod-work spike **>=30 ms**, with count/condition/component where
attributable. Whole-frame timing/stalls are separate; offline timers do not
certify the native gate.

## In-game checks for 6 Nov rig

Pending combined-head proof on an existing named disposable FT fixture with an
advertised rival and eligible player food shop. Classifications follow
[the locked-bridge feasibility report at 84be71a](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/84be71a/docs/reviews/rig/2026-10-31-locked-session-feasibility/README.md).
No rig action was performed here. Preserve fixture/profile copies and preferences;
use independent native observations as proof of native non-mutation.

1. **LOCKED-OK — rival choice over a player shop.** From one disposable checkpoint, bridge-dump both alternatives and an admitted resident's held source/shortlist. Advance normal events to an actual rival commitment; record `RivalFoodDiagnostics.Core.RivalChoices`, target kind, exact advertised price and `EstimatedFromPriceAndHours`. Show the player competed in the same eight-shop shortlist, <=32 source slots and <=16 logical route requests. Sampling is not a purchase; there is no minimum rival share.
2. **LOCKED-OK — timed outing and return.** Dump slot zero before departure, at the rival door, during estimated service, after payment and back at its saved origin. Record stable resident/body ownership, times, deadline and one outing/return. Repeat a represented slot's existing excursion with no second body. State proves timing/ownership; visual continuity is item 8.
3. **LOCKED-OK — one exact external debit.** Capture wallet/allowance, external account, spend/count, watermark and native receipt keys immediately before/after service. Verify one exact price-unit transfer to `ExternalFoodSpent` (or the legacy account), conservation, `RivalSettlements +1`, no native held/uncertain receipt and no replay debit. Use prepared disposable checkpoints for arrears/hold/future-essentials refusal.
4. **NEEDS-INPUT — changed terms and ownership.** Use normal fixture UI to change rival price/hours after commitment and before arrival; D2 must not make that write. Show missed service, zero debit, retained return and no substitute. Repeat closure/withdrawal and, if normal UI can induce it, both ownership-transfer directions: old authority refuses. Record scenarios the native fixture cannot induce rather than treating doubles as proof.
5. **LOCKED-OK — interrupted reload.** Save/reload outside timing windows at capture, partial quotes, selected result, outward travel, service, paid state and return. Compare IDs, price/service/deadline, account units, watermark and count with uninterrupted replay. Repeat with weight zero loaded while unloaded: held/saved payments/returns continue; new choices do zero rival work. Copied malformed/foreign/future schemas refuse while preserving source/hash. Changed-terms checkpoints prepared in item 4 can then replay without input.
6. **LOCKED-OK — native non-mutation and player control.** Independently capture rival registration/owner/prices/hours, stock, orders, customer-entry and finance state before/after a paused or explainably matched modeled-purchase window. Compare exact fields; attribute unrelated native clock effects separately. Verify zero rival checkout/customer-entry/supply/demand calls and zero native rival change. A player control keeps its existing receipt namespace/content and exact money/stock/order/accounting path, with no extra body.
7. **LOCKED-OK — matched performance windows.** Freeze base/candidate commits, checkpoint/seed, effective settings, dependencies, clock/body/graphics/population/shop setup and cache warmth. Interleave idle and breakfast/lunch/evening rush windows plus a soak; record pending work/catch-up, source/detail/route counts, refresh units, allocations/GC and retained memory. Record original autosave, disable it in the isolated profile and verify off before every window and after reload. Manual saves stay outside windows; save activity invalidates a clean sample. Report combined mod-work p50/p95/max, require p95 <5 ms and flag every spike >=30 ms with count/condition/component. Report whole-frame timing/stalls separately. Bridge-accessible closed/open diagnostic states can run locked; UI-only variants wait for item 8. Restore autosave/preferences/fixture changes during cleanup.
8. **NEEDS-INPUT — visible continuity and fixture UI.** In the unlocked session, select/follow a rival customer through outward walk, door/service and return; confirm continuous appearance, no second body and agreement with bridge state. Visually check price/hour edits, the player control and UI-only diagnostic/performance variants. Establishing a missing shop fixture through normal UI also waits for input.

[Checks index](../../CHECKS.md).

## Base integration - 1 November 2026

Task `pc-d2-sync-base` merges D2 `ef2d72e` with integration `5877453`, using
merge base `51ce709` and preserving both parents. No feature scope changed.
F1's drawing NO-GO report and E1's weather/settings/tests/tools/checks/perf index
are retained, as are all older slice entries. The [preservation audit](../reviews/2026-11-01-d2-sync-base-preservation.json)
checks every incoming CHECKS index line, both parents' slice-document presence,
test-method preservation (including two deliberately expanded theories) and the
mechanically frozen E1 reader. The [PR handoff](../reviews/2026-11-01-d2-sync-base-pr-description.md)
lists all 18 textual conflicts and their resolutions.

Schema 21 now builds on schema 20. `PopulationSchema20*` retains E1's exact
reader/tuple/semantic contract; its object resolver and strict tuple-property
guard exclude only D2-added DTO fields. The equality/arity guard is not loosened.
The tuple-number delegate binds to the frozen codec itself. `RivalWire21.Migrate`
first validates/upgrades supported predecessors to 20, preserves E1 weather
activation/citizen commitments/consumer suffix/held food multiplier exactly,
then adds D2's next-day watermark and zero/empty defaults before validating 21.
The frozen 19 fixture writer hides E1 fields, and schema-20 bootstrap is accepted.
Reserved 22/23 and all newer schemas remain refused without overwriting source.

Integration changes the shared Core shortlist/offer call sites so both captured
rain travel weighting and rival alternative weighting apply. D2 journey,
settlement, ledger, catalogue and native-sales policy stay intact. Incoming E1
runtime capture/queue/settings code is unchanged. Combined weighting invariants,
19/20 migration/source preservation and held rival reload under changed rain and
rival settings extend the existing tests. The first test build found a local
name collision in that expanded theory; corrected. The next run's 116 failures
all reproduced the frozen tuple-property guard mismatch; 2,305 other tests
passed. The guard now filters only D2 additions and keeps exact predecessor
validation; final rerun results follow below.

Byte/perf provenance: all original D2 tables above and both original D2 JSON
measurement artifacts describe the pre-E1 line based on `51ce709`. They are not
silently relabelled. The rival native-double size tool now compares genuine
schema-20 bytes to 21; awareness/weather fixtures likewise use the correct
predecessor shapes and separate slice byte charges. The capacity benchmark
consumes both weather and rival scalar/image allocations once, retaining every
existing byte/record cap. The C4 comparator pins schema 21 explicitly.

Validation is offline only. No game, rig directory, bridge lease, Steam/Workshop
or ordinary profile was used. Merge rule v2 leaves native behavior and combined
mod-work p95 <5 ms / spikes >=30 ms evidence for the later batched locked-head
rig; pointer/input-only checks wait for Peter's 6 Nov playtest. Whole-frame cost
remains separate. No new optimization was taken and all four parked hotspots
remain parked.

### A5 latest-head continuation

Integration advanced to A5 `0ffb819` while the first offline batch was running.
The completed first merge is `e263af6` (parents `ef2d72e` / `5877453`); a second
real merge uses base `5877453` to bring in A5. Both sides' diffs were inspected
for README and roadmap conflicts: keep both notices, current schema 21 and both
perf index entries. All A5 documents/checks/tests/tools and its incoming Core
inspector changes are retained. Only A5's migration-result assertion needs 20
changed to 21. Full final validation is rerun after this sync; first-batch
measurements above remain identified by their E1/F1 merge parents.

### Final combined-head validation

Acceptance source: `29147ee`, the real merge of `e263af6` and latest A5
`0ffb819`. The game-assembly build passes and all **2,438 unit tests pass**
(0 failed, 0 skipped). All **58/58 final foreground gates pass**, including discovery of all 2,438
tests, every required regression, every root regression project, all three
simulation tools, four capacity cases, the nested pickup regression, A5's extra
rush/integrated/save-bench modes, and the C4 exact-byte comparator. Every dotnet
test/run was preceded by the required guard; no CLI execution deadline was set.
[Commands, log hashes and raw measurements](../reviews/2026-11-01-d2-sync-base-results.json)
retain the actual results. The current integration head `0ffb819` is an ancestor
of the tested source.
The earlier 50-command batch passed and is archived separately because A5
arrived while it was in progress. It does not substitute for this final run.

Final-head measured limits (synthetic, foreground Release tools; one capacity
sample/case; no matched native comparison): source/rank p95 0.0191 / 0.0298 ms
at 1,024 / 20,000 alternatives; settlement p95 0.0006 ms. The two 128-event
whole-day batch p95 values are 2.1011 / 1.7834 ms. One >=30 ms offline batch
sample is flagged: **40.9275 ms** in the 20,000-alternative arm. Native mod-work
and whole-frame attribution remain pending. The 32-source / 8-shortlist /
16-route-slot envelope is unchanged. No new optimization is claimed.

The idle native-double fixture still measures schema 20 -> 21 at exactly
+5 bytes/consumer +19 header, with no additional records. The population-only
save-contract fixture is **120,184 bytes**, and its explicitly weather-stripped
projection remains **119,083 bytes**. The old pre-E1 figure is not a current
population-save size.

| Final capacity fixture | Current bytes | Streaming / semantic records | Full future bytes / records |
| --- | ---: | ---: | ---: |
| reference | 5,058,341 | 55,541 / 55,538 | - |
| consumers5000 | 9,802,053 | 112,632 / 112,629 | - |
| CF32 | 194,180,855 | 1,970,132 / 1,970,129 | 220,664,366 / 2,111,867 |
| CF64 | 232,580,855 | 3,570,132 / 3,570,129 | 259,064,366 / 3,711,867 |

Weather and rival fields each consume their own existing reserve exactly once.
The existing CF32/CF64 schema-17 oversized-migration refusal is retained;
current-schema reload succeeds. Neither byte nor record caps were raised.

Standalone save latency >=30 ms is flagged separately from mod-work samples.
Write/read/migration-from-17 milliseconds (one foreground sample each):

- reference: **128.8 / 346.7 / 650.0**.
- consumers5000: **156.3 / 728.7 / 1,750.9**.
- CF32: **2,015.0 / 6,108.9 / refused (oversized original)**.
- CF64: **2,714.9 / 8,612.8 / refused (oversized original)**.

These shared-host, whole-operation timings do not establish a speedup or native
frame regression. The four parked hotspots remain parked. Final raw measurements
and command results are kept in the companion sync results artifact.

The full 20,000-alternative dense awareness-build benchmark also flags one
>=30 ms sample (**33.2802 ms**, p95 also 33.2802 ms). This is whole catalogue
construction in the offline benchmark, not a native frame or a measured bounded
runtime continuation. Named timing/spike fields from the selected tools are
retained in `offlineTimingFlags` in the raw results; repeated percentile/max
statistics are not counted as additional spike events.

C4 exact-byte control (same schema-21 source with C4 engine/access files omitted
versus present): **41,787 / 41,787 population bytes** and **66,679 / 66,679 food
bytes**, identical hashes and zero byte growth. This remains a pure synthetic
fixture comparison, not ordinary-save or native release proof.

Archived pre-E1 index record (measurements preserved):

> - D2 — 1 November 2026 (offline, original pre-E1 measurements): schema 21, 39/39 gates, 2,380 tests; source/rank p95 0.0189/0.0438 ms at 1,024/20k, settlement p95 0.0001 ms, +5 bytes/idle consumer +19 header with no records; two offline batch spikes >=30 ms (max 47.7935), native gate pending: [measurements and 6 Nov rig checks](#).

### Final-fetch E3 ice cream continuation

The final fetch after A5 acceptance advanced integration from `0ffb819` to
`75c956c` (E3 ice cream feasibility). Merge base is `0ffb819`. The only textual
conflict is README: retain both D2 and ice cream STOP notices. Checks/roadmap/plan
indices merge automatically, retaining every slice. Incoming ice cream reader,
shared descriptor, tools, exact source pins and documents are unchanged. No Core,
persistence, schema, choice, journey, ledger, settlement or menu integration
change is required. Schema 21 and its isolated 20 -> 21 step remain intact.

The 2,438-test / 58-gate A5 acceptance above remains archived evidence on its
named source. A fresh complete batch for the ice cream head adds the new reader
regression, donut/salad/ice cream isolation guards and frozen three-reader baseline.
Final results are recorded below before handoff; no game/rig/profile action occurs.
