# E1: Weather capture, integration and native checklist

## Base sync — 1 November 2026 (offline only)

Integrates approved E1 `72f1cbd` with development `1f8f4b4` (final C2a
`692b8b5`, B1/B2, C3/C4, E2b/E2c and donut/cupcake feasibility) through a real
two-parent merge, `c332825`. All 42 per-file conflict resolutions and the narrow
Runtime/Core composition are recorded in the [sync description](../reviews/2026-11-01-e1-sync-base-pr-description.md).
The [preservation audit](../reviews/2026-11-01-e1-sync-base-preservation.json)
checks every development index line, the complete original E1 evidence/checklist
and all three performance rows. No base file is missing. C2a's failure-only B1
awareness source, E2b scheduling and C4 engine/access source remain unchanged.

Schema 20 stacks on the final merged schema-19 wire. B1 active revision-1
capabilities and bounded nonzero held contributions remain executable; no old
shape is relaxed. The populated-memory fixture explicitly exercises 18/19/20,
including rainy held replay with changed/disabled settings. C4 save neutrality
still pins the literal current schema, now 20, and requires zero raw-byte delta.
Queue fixtures distinguish E2b physical commitments from E1 logical Q captures.
The awareness benchmark's schema-18 input strips only weather additions and
reports those bytes separately; `5089748` fixes this tool-only stale fixture.

**Final offline validation:** **2,375/2,375 unit tests**, discovery and all
**57/57 gate commands pass** after the strict awareness-fixture repair (58 gate
attempts, with the original failed attempt and successful rerun both retained).
Includes the game-target build, every standalone regression project, the four
Python regression harnesses, pickup continuity, optional-stop performance,
interior wiring, donut differential/source/baseline checks, all three demos,
base save comparison, C4 literal save neutrality, yielded awareness rebuild,
CF32/CF64 and three fresh weather processes. The one-dotnet guard ran before
every command; shared-machine waits are included in command wall times.
[Commands, hashes and final outcomes](../reviews/2026-11-01-e1-sync-base-results.json)
and [raw structured measurements](../reviews/2026-11-01-e1-sync-base-measurements.json).

The weather-only projection exactly matches merged schema 19: **119,083** bytes,
SHA-256 `D7FF8294F56C3B3D5AF736540274E68E9848319B34658859FC15E0D4CDB1F3A2`.
Schema 20 is **120,184** bytes (+1,101); historical economy remains **849 orders /
797 purchases / 107,967,424,757,760 exact units**, with all order/economy/reload
digests equal to development. C4 adds **0 bytes**: population **41,787 -> 41,787**,
food **66,260 -> 66,260**, raw schema-20 UTF-8 equality with C4 source excluded
from the baseline export. C2a awareness still adds exactly **3,074 bytes** at
both 1,914 and 5,000 consumers, with weather counted separately.

CF32/CF64 actual bytes remain **193,429,684 / 231,829,684**; full planned totals
remain **220,664,366 / 259,064,366**, leaving **47,771,090 / 9,371,090** bytes.
Full planned records remain **2,111,867 / 3,711,867**. Weather still consumes
1,400,000 bytes plus 63 header/held-image bytes and 50,000 physical records;
consumer weather adds zero records. All fixed caps/headroom reservations remain.

Three weather processes report **0 >=30 ms across 5,100 measured sweeps**, max
**3.6095 ms**. Capture costs stay 40 B/resident boundary and 0 B/food boundary;
admission and the direct consumer precheck allocate 0 B. Native reads stay one
per capture sweep/frame. **Flagged offline aggregate costs:** 20k D1 build
**36.7610 ms**, one whole awareness-build sample **33.1729 ms**, CF32 write/read
**1,940.4 / 5,869.0 ms**, CF64 **2,266.7 / 7,220.6 ms**. Other aggregate/batch
metrics >=30 ms are enumerated in the structured measurements. These are synthetic
whole-build/save or batch totals, not native mod-work/frame samples; the yielded
awareness units and native gate remain distinct. Whole-frame timing is unmeasured.
The four parked hotspots remain unchanged.

Game-target DLL SHA-256:
`cf3122c2856c80632d760963d35a28555428deb9ec9490bd846bf702e54a92aa`.

The original dated entries below retain their historical baselines and results.
For pending checklist item 5, use integrated development `1f8f4b4` without E1 as
the matched baseline rather than the original `9b13698`; the schema-19 migration
checkpoint in item 4 still applies. Merge rule v2 uses a batched combined locked
rig on the development head, with pointer/input-only checks waiting for Peter's
6 Nov playtest. No per-PR rig, game launch, release or ordinary-save clearance is
claimed. All six original LOCKED-OK / NEEDS-INPUT items remain below.

## E1 review round 1 - 1 November 2026 (offline only)

**Opus-r1-1**, fix **ff11168** on reviewed **8fa4d27**. Queue weather was first
captured by the in-radius renderer lookup. `WaitForOpening` now owns Q capture
when the customer reaches the destination, before any physical observation.
Normal queue refresh only reads saved terms. Migrated/pre-existing waits lacking
a matching Q retain the fallback. Native rain still applies when E1 is disabled
or pre-activation; only its configured lead is gated. No schema/wire-format,
byte-budget, draw-key, route or movement change. [Updated contract](../population-schema20.md).

Seven new real-event regressions fail on the reviewed source: dry commitment,
rain before the first physical refresh, enabled/disabled E1 and save/reload all
lose the dry queue; the three capture/gating cases have no Q at commitment.
All seven pass after the fix, within **90/90 focused cases**. They verify frozen
dry terms, unchanged saved bytes on first refresh, no new source read or route,
and one cached native read with correctly gated new leads.

**1,951/1,951 tests**, discovery and **35/35 foreground gate commands** pass (all
31 requested plus food candidate/choice, weather and C2a comparison). The required
one-dotnet guard ran before every command. [Embedded red/green evidence and command/hash manifest](../reviews/2026-11-01-e1-r1-offline-checks.json).
Save-contract regression retains the exact C2a projected bytes and all historical
order/economy/reload digests. The W record remains at most 22/24 bytes/resident;
consumer weather remains 2/8 extra bytes with zero extra consumer records.
The unchanged weather kernels have **0 >=30 ms across 1,700 measured sweeps**,
max **4.7605 ms** in one Release process; this is offline evidence, not native
mod-work p95 or whole-frame timing. The existing numbered 6 Nov rig checklist
below still applies; native proof remains pending. No parked-hotspot rewrite.

Final game-target DLL SHA-256: `d6c3e373c6ebc9491d25e7e1f9b88bfd99b483844a3faf773e8926155118e95d`.
All owned test/run processes exited; repository process check found zero. No game,
rig/test profile, ModsLocal, bridge profile or lease was touched; no push or merge.


## E1 rain - 1 November 2026 (offline only)

Base C2a **9b13698**; implementation **0ea45a6**, hardened code/tools **dc59816**.
Only E1 is implemented: existing optional-outing admission and A3 food contexts
capture dry/rain/unknown, rain applies the bounded admission/travel coefficients,
and new opening queues retain their captured rain/lead on refresh/reload. Missing/
throwing source is unknown/neutral. Weather cannot create an event/context/route;
held decisions, work/trips/paid terms and existing native optional-stop/paired
continuations retain their rules. The native seam is read lazily once/frame at
new boundaries. `PopulationRuntime.Weather(id)` reads saved inputs only.
[Schema-20 contract](../population-schema20.md). No sibling changes were imported.

Additive 19->20 migration validates the frozen old shape, initializes unknown,
activates next logical day and performs semantic plus serialized-size/UTF-8
preflight before publishing. Old readers and the 18/19 wire shapes remain intact.
The four flat template/loader keys clamp/default/log once, including unusable
boolean/numeric input; they affect the next uncaptured context. No tuning enters
a draw key. The schema-20 held image stores the effective weather coefficient.

**Validation:** **1,944/1,944 tests**, discovery, all **31 requested foreground
commands**, food-candidate/food-choice regressions, baseline comparison, CF32/CF64
and three fresh weather benchmarks pass: **39/39 commands**. After tightening
migration preflight, the affected build/full-suite/discovery/save-contract/compare/
weather audit also passes **6/6**. [Command/hash manifest](../reviews/2026-11-01-e1-offline-checks.json).
Final game-target DLL SHA-256: `f072c805760acfb20e49515d45ff5e42804236a1e40b2c7f898f1c54b0469c50`.
Owned test/run/testhost processes exited; repository process check found zero.
The required wait-other-dotnet guard ran before each final command. No game was
launched. No rig/test profile, ModsLocal, bridge profile or lease was accessed;
no push or PR.

Coverage includes failed/missing source and frame caching; dry/rain/unknown food
and outing capture; active C2a and pre-activation shortlist paths; interrupted
outing route and food capture/quote/selected/departure reload; changed live rain/
disabled or edited tuning without reroll/extra allowance; admission endpoints and
seed nesting; stronger soft travel with positive distant winners; frozen dry/rain
queues; unchanged work, paid food and paired native-rain continuations; strict
migration/roundtrip/future/unknown/bad-token preservation and counted byte budgets.

The baseline/candidate save regression removes **only schema-20 weather fields**:
its 119,083-character projection and SHA-256 match C2a exactly. The 120-resident
population-only current wire is 120,184 characters (+1,101 = 120 null W fields +
the root header). Historical economy remains **849 orders / 797 purchases /
107,967,424,757,760 units**, all three exact order/economy/reload digests unchanged.
Initial fixture/header expectations and a fake shop's missing hours were repaired.
The new independent benchmark exposed an unfinalized E1 admission hash; dc59816
fixes it and exact original-key tests pass. An initial allocation assertion in
unchanged picker code also passes unchanged in subsequent full runs.

**Offline performance and allocation.** [Raw three-process measurements and commands](../reviews/2026-11-01-e1-measurements.json).
Windows .NET 10.0.13 Release, seed 77, synthetic ID/context keys, boundary 2500,
default .7/1.3, no routes/native work. Per case/process: 20 warmups, 100 measured
sweeps; table p50/p95 are medians of three process percentiles, max is largest.
Phases run separately under uncontrolled system load; these are policy/capture
microbenchmarks, not combined mod-work or whole-frame timing.

| Residents/boundaries per sweep | Resident sample + immutable capture p50 / p95 / max ms | Food sample + held-field capture p50 / p95 / max ms | Dry admission p50 / p95 / max ms | Rain admission p50 / p95 / max ms |
| --- | ---: | ---: | ---: | ---: |
| 957 | 0.0208 / 0.0296 / 0.1035 | 0.0099 / 0.0155 / 0.0188 | 0.0053 / 0.0088 / 0.0178 | 0.1349 / 0.1390 / 0.8532 |
| 5,000 | 0.0505 / 0.0849 / 0.8896 | 0.0261 / 0.0267 / 0.1091 | 0.0070 / 0.0075 / 0.0331 | 0.2935 / 0.4153 / 1.2174 |
| 50,000 | 0.3064 / 0.3530 / 1.2868 | 0.1122 / 0.1203 / 0.4687 | 0.0286 / 0.0333 / 0.8152 | 1.6817 / 1.9379 / 2.8653 |

Resident captures allocate **40 bytes/boundary**, held food-field capture and dry/
rain admission **0 bytes/boundary**. One lazy source read per capture sweep/frame:
120 reads per process instead of 114,840 / 600,000 / 6,000,000 at 957 / 5k / 50k boundaries;
the food phase independently confirms the same bound. New code has no per-frame
reflection/polling/scan/allocation or per-resident per-frame rain reads. Existing
native optional-stop safety rules remain unchanged. The roadmap logs all three
cheap wins: frame-cache calls, admission-key strings and the zero-allocation new
consumer precheck. **0 >=30 ms across 5,100 measured policy/capture/precheck
sweeps; max 3.7757 ms.** This does not establish the native p95 <5 ms gate.
Whole-frame cost/stalls remain separate. The four parked hotspots remain parked.

**Saved bytes and headroom.** Nonnull W is **22/24 bytes** inclusive (null 9);
consumer suffix **2/8 extra bytes**, with a full extension 53 bytes including its
existing slot overhead. At 50k x3, 1,400,000 of the 2,400,000 weather reservation
is consumed, leaving **1,000,000 bytes**. The fixtures additionally use 63 constant
header/held-image weather bytes from the separate existing allowances. Each W
charges one physical record, consumer weather adds zero. Both counters agree on
this delta. Capacity tools deduct actual bytes/records once from future reserves.

| Fixture (50k x3) | Actual ASCII/UTF-8 bytes | Full planned budget bytes | Character/UTF-8 headroom | Full planned records / 4M |
| --- | ---: | ---: | ---: | ---: |
| CF32 | 193,429,684 | 220,664,366 | 47,771,090 | 2,111,867 |
| CF64 | 231,829,684 | 259,064,366 | 9,371,090 | 3,711,867 |

CF32 stays below 85% of the fixed cap; CF64 retains 9,371,090 bytes and 288,133
records. No visit/outcome, memory slot, exact money or cap was reduced/raised.
**Flagged >=30 ms offline whole serialization/load samples:** CF32 write/read
1904.6/5933.8 ms; CF64 2473.7/6856.8 ms (one measured run each).
These large synthetic save/load operations are outside ordinary-play/frame windows;
they are not mod-work p95 samples. Their legacy-17 compositions still refuse as
oversized; they do not establish migration of an oversized source.


### In-game checks for 6 Nov rig

Pending native proof on a named disposable FT fixture only. Classification follows
[locked-session feasibility 84be71a](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/84be71a/docs/reviews/rig/2026-10-31-locked-session-feasibility/README.md).
No rig work was performed in this implementation run.

1. **LOCKED-OK - dry/rain matched decisions and bridge read.** Use matched seeded
   disposable checkpoints/populations/shops and natural dry/rain windows; record
   the verified `OptionalStopEnvironment.Raining` read and effective flat settings.
   Join `PopulationRuntime.Weather(residentId)`, saved W/consumer inputs and held
   decision traces to new outing admissions and food-choice travel-distance bins.
   Compare admission counts and near/distant choice mix across matched consumer
   contexts; verify positive distant choices, unchanged context/route caps and
   at most one new native weather read per frame containing capture boundaries.
   Record failed/missing-source unknown-neutral behavior through a read-only test
   source on the disposable fixture. Do not write native weather.
2. **LOCKED-OK - transition safety.** Observe native rain starting during a work
   attendance, committed outward/return trip, paid food service, opening queue and
   paired stop. Compare IDs, routes, paid terms, queue start/end/lead and original
   activity/event deadlines before/after. Rain must not cancel work/travel/purchase
   or shorten an existing queue. Optional/paired stops retain existing native-rain
   cancellation and the original paired continuation; they gain no reroll or trip.
3. **LOCKED-OK - captured replay and departure.** Save outside timing windows at
   outing capture/pending route, food capture/partial quote/selected and committed
   departure. Reload with changed observed rain and changed/disabled E1 tuning;
   compare captured codes/multiplier, key/attempt, shortlist, quote cursor, actual
   route count, winner and paid/return terms against uninterrupted copies. Weather
   alone must create no context, route or retry; the next fresh context uses new
   loaded tuning without waiting another day.
4. **LOCKED-OK - schema-19 migration.** Load a disposable C2a schema-19 checkpoint;
   verify schema 20, unknown/neutral initialization and next-logical-day weather
   activation with stable IDs, money, held shortlist/quotes and commitments. Reload
   before/after activation; verify only fresh next-day contexts sample weather.
   Record actual bytes/records versus the ledger. Keep bad-token/future-version
   fixture copies and verify refusal preserves their original payloads.
5. **LOCKED-OK - matched performance versus C2a `9b13698`.** Interleave baseline
   and E1 idle/breakfast/lunch/evening-rush windows with the same fixture, seed,
   population, shops, native clock/body/graphics settings, panel state and cache
   warmth; include rainy rush/cold quotes and a soak. Record combined mod-work
   p50/p95/max, every spike >=30 ms (count/component/condition), route/query/read
   counts, allocations/GC, pending work and catch-up latency. Require mod-work
   p95 <5 ms; report whole-frame intervals/stalls separately. Record original
   autosave, disable it and verify off before each window and after each reload.
   Keep manual saves outside windows; any save activity invalidates the window.
   Restore original autosave/preferences/fixtures during cleanup. These are the
   ordinary combined 6 Nov windows, not a separate save-specific profiling run.
6. **NEEDS-INPUT - visible continuity and inspector selection.** During the
   unlocked session, follow dry/rain consumers through departure, a queue/paired
   stop transition and paid return; visually check continuity and select the same
   persistent resident with the pointer/inspector. Confirm the bridge-observed
   reduction/distance mix does not hide a visual cancellation or identity swap.
   Bridge dumps alone do not prove pointer selection or visible journey continuity.

## Development notice

> **E1 development status - 1 November 2026 (offline only):** captured dry/rain/unknown inputs for new discretionary outings and food contexts, soft rain travel preference and frozen new-queue leads; additive schema 20 on C2a. [Contract and byte ledger](../population-schema20.md). Offline measurements and the numbered 6 Nov rig handoff are in [CHECKS](#e1-rain---1-november-2026-offline-only). Native combined performance/behavior proof remains pending.
