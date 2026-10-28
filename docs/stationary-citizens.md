# Stationary citizens: optional stops approved, policy choices pending

The game's stationary zones have been switched off (see [issues.md](issues.md)). This is a proposal for what should stand in the street instead. Step one (queues at opening; crossing waits stay the existing red-light hold, with no time cap) is in the development build but not checked in game; the rest isn't implemented.

## The idea

Nobody stands in a street for no reason. Every person who stands still should be one of the simulation's citizens doing something the simulation knows about, for a bounded time, at a place where that thing happens. Because the standing comes from the citizen's day, it follows the hour, the weekday, the weather and the place's opening hours without separate rules, and it ends: the citizen walks on when the reason is over.

A standing citizen is a *dwell*: who stands (citizen id), why (dwell kind), where (spot), from when, until when, facing what. The Core plans dwells as part of the day schedule; Runtime only shows the ones near the camera.

## Dwell kinds

| Kind | Who | Where | When | Duration | Weather |
|---|---|---|---|---|---|
| Waiting for a bus | commuters whose route includes RLO's buses (0.41.1) | the stop | before the bus arrives | until it comes, at most ~10 min | stands under the shelter, umbrellas otherwise |
| Waiting to cross | any walker | the kerb at a crossing | red phase | the light's wait | as is |
| Queue at opening | customers of a place that opens soon | its door, in a line | the last ~10 min before opening | until opening | fewer people; under awnings |
| Smoking or phone break | employees of an open business | beside its entrance, off the walking line | mid-shift, once or twice | 5–10 min | skipped in heavy rain, or under cover |
| Waiting for someone | citizens with a meeting or a shared activity (dinner, cinema) | outside the venue | a few minutes before the shared start | until the other arrives | under cover |
| Chatting outside a bar or club | evening and night visitors | in small groups (2–4) outside nightlife venues | Thursday to Saturday evenings most | 10–30 min | much rarer in rain or cold |
| Window shopping | shoppers between stops | in front of open shops | shop hours, weekends most | 1–3 min | rare in rain |
| Audience | passers-by with time to spare | a ring facing a street performer | while the performer plays | 2–8 min | fewer, under umbrellas |
| Resting | older or tired citizens, park visitors | benches and park edges | daytime, warm days | 10–40 min | none in rain |
| Waiting for a taxi or a lift | citizens whose next leg is a taxi or a car pickup | the kerb | until the pickup | a few minutes | under cover |

Each kind declares where it may happen, as a spot type (door, kerb, stop, venue front, bench, performer ring), and how it ends: a time, an event (bus arrives, light turns, place opens, friend arrives) or a cap.

## Rules that keep it believable

- **Bounded.** Every dwell has a hard maximum, except waiting to cross: that ends when the light lets the walker cross safely, however long it takes. When it ends, the citizen walks on toward the next activity's destination: never a freeze in the open, never a pop-out in view.
- **Off the walking line.** Spots are sampled beside the pavement's flow (against a wall, beside a door, at the kerb), so standers don't block walkers or the player.
- **Grouped where people group.** Chatters and audiences come in small groups facing each other or the performer. Queues line up. Most other dwells are alone.
- **Weather-aware.** Rain moves dwells under cover (awnings, shelters, doorways), shortens them, or cancels the optional ones (window shopping, resting, chatting). Umbrellas stay as the game does them.
- **Hour-aware.** Dwells come from the schedule, so a 3 a.m. street has a few people outside a club and at a taxi rank, and nobody window shopping.
- **Animation fits the kind.** Phone, smoking, holding a bag, checking a watch, talking with gestures, clapping for a performer. The game's stationary poses (`StationaryAiBehavior`) already cover phone, held object and idle variety; talking pairs and queues can use them with facing rules.
- **Budgeted.** Show at most N dwells within the visible radius (start with the existing 48 within 120 m), nearest first. Every dwell has a stable id, so a stander doesn't change appearance when it reappears.

## How it fits the code

1. **Core: a dwell planner (pure C#, tested).** Given a citizen's next activity, its route, the place's hours, the hour and the weather, it decides whether a dwell happens, which kind, and its start and end. It's deterministic per citizen and day, so reloads and camera moves don't change it. Tests: dwells always end; no dwell outside a place's hours or a kind's window; rain cancels the optional kinds; a queue never forms more than ~10 min before opening; group sizes stay in range.
2. **Runtime: spots.** For each place, sample a few dwell spots per type from what we already read (`GamePlaces`, `OutdoorPlaces`, entrances, `WalkingRoutes` for kerbs and crossings), checked against the NPC navmesh and kept off the main walking line. Cache them per place.
3. **Runtime: showing dwells.** Reuse the physical citizen bodies from `PhysicalPopulation`. A citizen in a dwell walks to its spot, turns to face what it faces, plays the kind's pose, and at the end walks on. This replaces both of today's frozen states: "awaiting opening" becomes a queue-at-opening dwell near the time, and "awaiting a route" waits indoors.
4. **Rollout in steps:** queues at opening and waiting at crossings first (they reuse what exists and fix issues 2 and 3), then breaks and chatting outside venues, then audiences, resting and window shopping.

## Questions for Peter

- Should street performers keep a small anonymous audience until audiences come from citizens, or play to whoever passes?
- How busy should a night street feel: a handful of people outside bars, or near empty?


## 28 October implementation design

Peter chose **Q2 B: add optional stops**. This approves the direction, not every row of the original proposal. Q1 (hour/rain crowds) remains open: this increment must not change ambient display caps, trip generation or quiet hours. The existing opening queues and safe crossing holds stay unchanged.

### Proposed first increment and unresolved choices

Recommend individual employee breaks (5?10 game minutes) and window shopping during an existing shopping visit (1?3). Resting at verified benches or park edges (10?40) is a possible second increment. Chatting (10?30) needs persistent groups; audiences (2?8) need a live performer lifecycle. These are proposals, not enabled kinds. Meeting, pickup and bus waits require actual coordinated services; no fictional service is introduced.

The original ranges remain proposed. No new minimum night population is specified: recommend eligibility from existing visits and quiet hours, with no extra nightlife trips. Peter must select the kinds, ranges, density and audience policy. The requested `persistent-city-DESIGN-QUESTIONS-2.md` handoff records options and recommendations.

### Mechanics implemented without choosing policy

`Core/OptionalStops.cs` accepts an explicitly selected kind, existing citizen/spot identities, requested duration and a free-until deadline. It clips to the existing kind cap and next commitment, refuses nonoptional event/audience kinds, and applies the already-declared `DwellRule.Optional` rain cancellation. Its continuation check ends a committed stop at its original deadline, on rain or when its spot disappears. These pure mechanics neither schedule stops nor alter citizens, trips, purchases or physical selection. There are **no new visible stops yet**: selecting and enabling a kind awaits Peter's answer.

Rain semantics for the eventual scheduler remain a decision: recommend cancel in any rain for the initial individual kinds. Shelters, heavy-rain thresholds and umbrellas need native evidence. Cancellation means resume the saved continuation; it never means hide the citizen, restart the timer or teleport. No weather input is connected to the new helper in production yet, so Q1 stays untouched.

### Persistence and Runtime integration required after selection

A scheduled stop must belong to the existing citizen, not a pooled body. Persist kind, place/spot identity, start, end, facing target and the original activity/route continuation. Commit a stop once in Core before physical admission. Camera movement, body reuse and different update cadence must never roll it again. A resumed save uses the recorded deadline; expired or cancelled stops resume their original continuation. Validate the spot against the live navmesh; missing spots end the optional stop safely without changing identity. Future integration must add a versioned schema with an explicit no-active-stop migration, not infer stops on loading old saves. The present helper has no saved fields and causes no stop-related schema change; schema 14 belongs solely to Q3 accounting.

Runtime will share the existing physical citizen budget, sample off-flow spots, walk a body to its spot and out along the continuation, and retain leases across the transition. It must not book native sales, reassign identities or resurrect anonymous stationary crowds. Group/audience membership and performer loss need explicit policy before implementation.

### Tests and remaining evidence

Offline tests cover caps, clipping at commitments, no time available, rain refusal/cancellation, missing spots, immutable deadlines after reconstruction and varying sampling cadence, and refusal of event/audience kinds. They test common mechanics, not scheduler or rendered behavior. Once kinds are selected, add interrupted-journey save migration, mid-stop reload, route/spot removal, food/work priority, midnight/quiet-hours, cadence differential and camera/pool continuity tests, then separately authorized native motion/animation checks. Nothing in this design authorizes game launch or release.
