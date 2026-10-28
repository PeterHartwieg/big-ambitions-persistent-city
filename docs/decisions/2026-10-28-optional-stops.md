# Optional-stop decisions — 28 October 2026

Authoritative source: Peter's `C:/Users/Peter/.codex/handoffs/leftover-2026-10-28/persistent-city-DECISIONS-2.md` (14:26 JST), reiterated in this task.

1. Include individual breaks, window shopping, resting at validated benches/park edges, chatting groups and performer audiences. All are in scope; implementation order is discretionary.
2. Durations, in game minutes: 5–10, 1–3, 10–40, 10–30 and 2–8 respectively. Any rain cancels optional stops. Audiences are optional too.
3. Stops use already eligible visitors only. No minimum crowd or extra nightlife outings.
4. Audiences consist only of passing persistent citizens who choose a persistent timed audience stop. Native anonymous stationary zones remain suppressed.
5. Q1 is C: leave hour/rain street crowds unchanged now. Weather/time-aware routines are a separately specified future feature.

The prior directions to await kind selection, duration/rain selection, night-density selection and anonymous-audience policy are superseded. The required stop-related version migration is authorized by this task; it does not relax financial or save-refusal rules.

DECISIONS-3 (28 October, 14:55 JST) resolves frequency: **25% of chances**. One fixed chance per existing visit, at most one audience chance per journey, mid-shift breaks for workers, no minimum crowd and no new outings. Decisions persist and never re-roll on camera changes or reloads.

QA can change the existing JSON setting `OptionalStopChoicePercentage` (default 25, clamped 0-100). Only new chances use the new rate. Schema 16 records the per-visit choice, including declines and accepted visitors waiting for conversation partners; schemas 13-15 migrate without inferring choices for existing visits. See [configuration and implementation limits](../stationary-citizens.md). Audience opportunities remain entrance-passing walking arrivals; general mid-route interception and kind-specific animation clips are not claimed.

No game launch, installation, rig access, release or deployment follows from these decisions.
