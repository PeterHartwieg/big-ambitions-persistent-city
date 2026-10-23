# Stationary citizens: a proposal

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
