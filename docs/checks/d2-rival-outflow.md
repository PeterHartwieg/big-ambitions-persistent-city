# D2: rival choice and own-ledger outflow

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

**Offline validation:** all **39/39** required-plus-D2 foreground gate commands
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

The committed [results manifest](../reviews/2026-11-01-d2-rival-outflow-results.json)
retains commands, exit codes, source/log hashes, complete benchmark JSON and the
published schema-19 comparison. Implementation/proof commits are `72971d3` and
`988d4c6`; final DLL SHA-256 is
`72435f39a79abce0bf3ab0b4c9dd3bf349259dccffd6b38fc1ff43480f0e32bc`.

Tests cover slot-zero/represented trips and returns, reload at held/route/service/
payment/return boundaries, held tuning with new weight zero, withdrawal/changed
price/hours/closure/ownership, empty B1/campaign sources, unchanged player receipts,
exact accounts, conservation/overflow and malformed/foreign-schema preservation.
The native-double harness compares complete fixture fields, including stock and
finance canaries. These are offline checks, not native evidence.

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
