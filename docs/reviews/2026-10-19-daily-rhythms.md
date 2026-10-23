# Daily rhythms and staggered commute preparation — 2026-10-19

## Scope and agreed behavior

Version 0.5.0 / schema 6 adds personal home preferences and changes background event scheduling. Population, economic isolation, RLO scene exceptions, native clock settings, the physical/background ownership model, and the sixteen-event / soft-two-millisecond frame budget are unchanged.

The user selected personal routines: work/study and committed journeys take priority over quiet hours. Quiet spans are deterministic from resident identity, persona, city seed and day. Baselines are 23:00–07:30 for ordinary residents, 21:30–06:30 for parents/retirees, 22:30–07:00 for students and 23:50–09:00 for night owls, with personal ±45-minute and daily ±15-minute variation. Before the native Friday–Sunday weekend, bedtime is 30 minutes later and wake time 60 minutes later. These are home preferences, not sleep duration, fatigue or needs simulation. Optional visits are clipped or skipped by these preferences. Most existing outings already end before quiet time; the practical clipping chiefly affects some night-owl outings. No extra outings were introduced merely to make the quiet policy more visible. Work/study windows are unchanged; an existing journey/wait/attendance keeps its saved destination and deadline. Night-owl leisure may continue past midnight using the originating day's venue selection.

Home windows now cross midnight to the next actual activity. Quiet start/end and calendar rollover do not create citizen events. Lifecycle deadlines still run when safely home. A worker prepares the next commute at an individual time 8–14 hours before the shift, or earlier if a previous comparable route estimate plus a personal margin requires it. Preparation occurs only when safely home. Established commuters carry their previous successful same-workplace duration into the next work window without another estimate search. Unknown, changed or previously unmeasured commutes still receive an advance estimate. The saved record contains the work window, preparation time and estimated duration; no selected route or geometry is reused. A failed first estimate is retried at shift start. An existing positive duration survives a failed departure attempt, which uses the normal route retry window. Departure selects a fresh route so closures and reopened roads compete normally. Shorter fresh routes can arrive early and wait; longer routes can arrive late. Unknown routes or arriving home after the ideal departure do not receive backdated departures or a punctuality guarantee. The reused duration is the successful route plan, not a learned record of subsequent physical traffic delays; those delays still control the current journey.

Home arrival schedules any immediately due preparation/departure for the following event, retaining a route only when it will actually be used; it does not calculate and discard a duplicate route. The first event after startup, an old-save migration or a genuine clock conversion may still have immediate preparation work. This policy targets steady overnight work and avoids changing existing committed state merely to produce a quieter first sample.

## Save compatibility

The key remains `persistentcity:state_v1`. Exact old schemas 1–5 are validated before migration; schema 6 adds strict commute-preparation fields. Older payloads start with an empty record while keeping identities, assignments, routes, waits, admission history and event dates. Existing schema-1 expansion and schema-2 job-date migration behavior remains intact. Schema-3+ housing preservation rules are unchanged.

Preparation validates its target's role, finite dates, ordering and bounded duration. Stale metadata for a previous workplace may survive while away, then is discarded or replaced safely at home. Resident replacement creates a new empty record. A true travel-clock conversion clears preparation and wakes safely-home residents after all conversions validate. Ordinary native 1×/6× changes do not retime the policy. A duration mixes spatial movement with fixed-minute waits/preparation, so multiplying the whole estimate by a clock ratio would be incorrect; it is intentionally invalidated. A failed conversion is atomic.

## Offline checks

- Full offline suite: **469 passed, 0 failed**, .NET 10, warnings as errors.
- Native game-target compile: C# 9, warnings as errors, succeeded.
- Candidate DLL SHA-256: `7c4dd639eef7eaab0be5c2a2f90760d237014812cf4fa3c48eac4cdafa56a4a1`.
- New checks cover personal quiet spans, work priority, three-day weekends, overnight leisure venue identity, preparation distribution, early/late/failing fresh routes, previous-evening commutes, no backdating, lifecycle events, cadence/budget/reload equivalence, original activity ownership, schema1–5 migration, strict schema6 validation and atomic timing changes.
- Historical large legacy fixtures now remove the schema6-only field when constructing old wire payloads. The 25,495-resident /150-leg migration stress fixture still passes; this is compatibility evidence, not a supported default population or native performance claim.

## Review

GPT-6.1 Sol integration review reproduced one issue: a shortened departure route could reach a permanently closed workplace before the shift, return home, then depart again and count the same missed shift twice. The simulator now preserves that missed activity's home fallback until its original deadline. The reviewer independently verified the fix, and the integration suite includes a regression. A follow-up schema5 test found that a direct wake from a timing change could also lose that fallback; it now survives both home arrivals and direct/lifecycle wakes. Shift endpoints use exact integer game minutes within the supported bound. GPT-6.1 Sol approves the final disabled offline candidate, with native validation pending.

Exact Opus 5.5 initial review prompted reuse of previous durations, elimination of discarded home-arrival queries and cheaper quiet/routine lookup. Opus 5.5 approves the revised disabled, offline-validated private candidate, with native testing pending. Its final evidence note was resolved by the completed 469-test full run and final native-target compilation, both performed after the last code changes; the final DLL hash above was rechecked. No native speed, backlog, FPS or playability improvement is claimed from offline tests alone.

## Synthetic operation-count evidence

A deterministic 14-day fixture compared the preserved 0.4.1 binary with current core sources: 957 commuters, eight shifts each, fixed successful 120-minute routes, no optional venues, no turnover. Both completed 15,312 journeys with zero misses and unchanged population. This isolates commute/event scheduling and is deliberately simpler than a real city.

| Count | 0.4.1 | 0.5.0 |
|---|---:|---:|
| All route requests | 22,968 | 16,269 |
| Outbound requests | 15,312 | 8,613 |
| Homebound requests | 7,656 | 7,656 |
| Simulation events | 44,022 | 39,211 |
| Route requests exactly at later midnights | 6,699 | 0 |
| Events exactly at later midnights | 12,441 | 14 |
| Largest same-minute event group after startup | 957 | 43 |

Total route requests fall 29.2% including initial estimates; after the first shift, steady workdays use 1,914 rather than 2,871 requests (33.3% fewer). The remaining fourteen midnight events are individual preparation times coinciding with midnight, not a population-wide rollover. Event histograms use the actual queued event time, including the final event when advancement catches up. This fixture reports operation counts only; no CPU timing claim is drawn from it. Real mixed activities, failures, distant routes and lifecycle changes will produce different savings.

The quiet-lookup optimization was separately compared with its first implementation: 1,391,291 exact routine comparisons, zero mismatches. A paired Release synthetic workload of 222,720 lookups, tiered compilation disabled, measured 153.82–161.69 ms versus 21.08–21.38 ms across three pairs, with allocations 283,730,400 versus 24,403,920 bytes (91.4% lower). This compares two implementations of the new routine lookup, not whole 0.4.1 versus 0.5.0, native paths, FPS or simulation lag.

## Native validation — explicitly pending

The user selected **Leave native comparison pending** because the Mac rig belongs to another active playtest. The rig agent performed only a read-only status check; it acquired no reservation, installed nothing and issued no game/world control. No native 0.5.0 load, schema6 save/reload, physical movement or performance validation has been performed. The other playtest was untouched.

Local hourly-profiling instrumentation compiled and a paired scenario handoff is prepared under ignored `build/daily-rhythms/rig/`. The future comparison should use matched starting state, native6 / bridge1 /30FPS, two nights and intervening day, separate startup/migration from steady operation, verify persistence and run a normal-speed motion smoke. Instrumentation itself still needs native loading/binding verification. The follow-up review requested counting `TravelTiming.Apply` and true recalculations to detect clock-rate jitter causing repeated estimate invalidation; the local profiler now includes those counters. Intentional routine changes make full-city baseline/candidate equality inappropriate; invariants and save/reload continuity require separate checks. No autonomous later rig run is scheduled.

