New discretionary outings and food decision contexts now capture dry/rain/unknown
once. Rain softly reduces outing admission and strengthens distance preference;
held contexts, paid food, work and committed trips retain their inputs/terms.
New queues freeze their rain lead. Missing/failed source is neutral. Runtime reads
only the verified rain seam through a lazy frame cache.

Additive population schema 20 stacks on C2a `9b13698`, with strict 19->20 migration
and next-day capability activation. Old 18/19 wire contracts remain unchanged.
The four flat settings stay outside draw keys. W uses at most 22/24 bytes per
resident; the existing consumer extension adds 2/8 bytes and zero record charges.
No sibling behavior, street-life timing keys, menus or native writer is imported.
[Contract and ledger](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/codex/pc-e1-rain/docs/population-schema20.md).

**Validation:** **1,944/1,944 tests**, discovery, all **31 requested foreground
commands**, food-candidate/food-choice regressions, baseline comparison, CF32/CF64
and three fresh weather benchmarks pass: **39/39 commands**. After tightening
migration preflight, the affected build/full-suite/discovery/save-contract/compare/
weather audit also passes **6/6**. [Command/hash manifest](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/codex/pc-e1-rain/docs/reviews/2026-11-01-e1-offline-checks.json).
Final game-target DLL SHA-256: `f072c805760acfb20e49515d45ff5e42804236a1e40b2c7f898f1c54b0469c50`.
Owned test/run/testhost processes exited; repository process check found zero.
The required wait-other-dotnet guard ran before each final command. No game was
launched. No rig/test profile, ModsLocal, bridge profile or lease was accessed;
no push or PR.

The projected schema-19 bytes and all exact historical economy/reload digests match approved C2a.

**Offline performance and allocation.** [Raw three-process measurements and commands](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/codex/pc-e1-rain/docs/reviews/2026-11-01-e1-measurements.json).
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
