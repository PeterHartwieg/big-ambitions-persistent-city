# Stationary citizens: approved optional stops

Peter's 28 October answers in `persistent-city-DECISIONS-2.md` approve all five proposed optional kinds. They supersede the earlier proposal and its unresolved questions. Q1 is **leave street crowds and routines unchanged now**; time/weather-aware routines are a separate future feature.

| Kind | Existing eligible activity | Game minutes | Rain |
|---|---|---:|---|
| Individual break | Employee already admitted at an open workplace; mid-shift opportunity | 5–10 | Cancel in any rain |
| Window shopping | Existing admitted shopping visit at an open shop | 1–3 | Cancel in any rain |
| Resting | Existing park visit, at a validated park edge | 10–40 | Cancel in any rain |
| Chatting | Two existing eligible visitors at the same nightlife venue | 10–30 | Cancel in any rain |
| Performer audience | Persistent pedestrian passing a live performer while arriving on an existing walk | 2–8 | Cancel in any rain |

No minimum crowd, anonymous audience, extra nightlife outing, replacement citizen or native sale is created. The existing quiet-hour and trip-generation rules remain authoritative. A late-night stop can use an already eligible visit; it cannot create one. Native anonymous stationary zones stay suppressed. Performers and the native exceptions already retained by `StationaryCrowds` remain unchanged.

## Approved frequency and QA configuration

DECISIONS-3 approves **25% of chances**. Runtime reads `OptionalStopChoicePercentage` from the existing `persistent-city.json` in `ModContext.ModRootPath`: installed as `<game directory>/ModsLocal/PersistentCity/persistent-city.json`. The section is the **top-level JSON object**, not a nested section. The source default is `config/persistent-city.json`; the build copies it to `build/PersistentCity/persistent-city.json`.

Use an integer from **0 through 100** (default **25**). Negative values clamp to 0 and values above 100 clamp to 100. Missing, non-numeric, null, boolean, object/array or in-range fractional values fall back to 25; numeric strings are accepted. Invalid JSON syntax still follows the existing configuration failure path. Edit while the city is unloaded and reload the mod/city; there is no live file watcher.

Only new visit chances use the updated rate. A saved acceptance, decline or pending conversation choice survives events that retain the same admitted visit and is never re-rolled; active stop deadlines and cancellation remain authoritative even at rate 0. Camera moves and route rebuilds do not affect choices. No QA reset path was added: use a later ordinary visit or a disposable fresh city to observe a new rate. There is one fixed chance per visit and at most one audience chance per existing journey, with worker stops timed mid-shift. This enables production scheduling in source; it is not native playtest or release clearance.

## Scheduling and continuation

`Core/OptionalStopScheduling.cs` evaluates a newly admitted visit once, after its ordinary event commits. Stable citizen/place/visit keys choose participation and duration. Worker opportunities start midway through the remaining shift. Other opportunities start at admission. There is no camera input, frame-random roll or recurring chance while standing. New stop creation requires validated geometry at that admission; a cold entrance deferred by the sampling budget can miss that opportunity. Its participation choice remains saved, but no delayed stop or retry is invented. This differs from retaining an already saved stop while its geometry is revalidated. If time before closure or the existing commitment is below the kind's minimum, the stop is skipped; otherwise the shared `OptionalStops.Plan` clips the duration to that commitment and cap.

The stop overlays the existing admitted activity: the citizen's place, activity, original event/deadline, identity and assignments remain intact. The existing routine event owns the continuation. An event that retains the same admitted visit cancels the stop and keeps its record as evidence that the opportunity was used; changing visits or departing clears the record. Stops do not extend activity deadlines, create travel or restart on reload. A visible body must physically return to its entrance before its newly committed journey can advance; a blocked return can therefore postpone actual travel without changing the original commitment. Food outings and existing native-food service have priority: meal outings cannot start a stop, food-owned shops are excluded, and an accepted meal outing or existing-stop food decision cancels any stop. Cancellation does not alter funds or sales.

Chatting forms a pair from eligible persistent visitors at the same open nightlife place. Both visitors must have accepted their own saved visit choice; the 25% opportunity rate does not guarantee a matching conversation density. Candidates are filtered to eligible local visitors before stable sorting, and an unsuitable partner does not block a later feasible one. Both spots and a common deadline are chosen before either member is published. Membership, facing and the common group identity persist. Missing/cancelled partners cancel the conversation; no singleton or anonymous filler is created. The first visitor may wait indoors for a later eligible visitor without creating a separate meeting trip. There is no minimum evening crowd. A visitor who already received a stop cannot be recruited again during that visit, even after the stop expires, an intermediate event runs, or the city is reloaded. Cancelling an interrupted conversation cancels both members; neither becomes eligible for another chat in the same visit.

Audience opportunities currently use pedestrians completing an existing walking arrival at a venue entrance within eight metres of a live performer. They do not divert routines toward a show or interrupt an in-progress road/transit leg. This is an entrance-passer implementation, **not general mid-route audience interception**. Each member chooses an individual timed stop and keeps its own end; one member is a valid audience. Performer disappearance, replacement, movement away from the recorded target or any rain cancels it. The audience is not native stationary-zone population.

## Saved state

City schema **15** introduced nullable `CitizenState.OptionalStop`: kind, place, stable spot/target identity, spot/facing/return geometry, start/end, original visit/event and conversation members/cancellation. The existing citizen activity remains the continuation; there is no second general itinerary or copied identity.

Schema **16** additionally persists nullable `CitizenState.OptionalStopChoice` for the current visit, including declines and accepted waiting conversation partners. Strict schema-13 and schema-14 migration initializes no active stops; schema-15 migration preserves existing stops. All three migrations leave the unknown visit choice null, which cannot be recruited by a later conversation arrival. It preserves balances, receipt evidence, assignments and interrupted journeys, retaining the previous schema-13 monetary validation. Loading an old save never infers a stop for a current visit. A later newly admitted visit can become an opportunity at the configured rate. Schema-16 payloads must contain both fields, even when null; `OptionalStop` is refused before schema 15 and `OptionalStopChoice` is refused before schema 16. Future/invalid payloads are preserved and refused, never overwritten.

Validation checks kind eligibility, bounded duration, finite geometry, original activity continuation, distinct group slots and reciprocal membership for active conversations. Cancelled/expired records do not restart. Route rebuilds retain the environment and opportunity setting; event working copies own their stop/member copies.

## Runtime and physical continuity

`Runtime/OptionalStopEnvironment.cs` supplies validated geometry independently of the camera:

- Door/entrance stops reuse the off-flow facade sampling in `DwellSpots`, with complete NPC paths and neighbouring-door clearance. Optional stops and opening queues share one city-lifetime cache and one cold facade sample per frame. Destroyed native building references invalidate derived geometry and are rediscovered.
- Chat eligibility checks the native established nightclub category; other leisure categories are not silently treated as nightlife.
- Rest spots are sampled at actual edges of an existing accepted native park volume, inside that volume and reachable on the dry NPC navmesh. No decorative object is assumed to be a usable bench. Bench-specific seating/animations are not claimed.
- Performer-ring points require a live retained performer, complete walking access, small sampling displacement and proximity to a navmesh edge. The performer is read through its native stationary-spawner reference; no spawn method is called. Ring geometry is cached per entrance and live performer image; changes to performer/zone identity, location or presence invalidate it before reuse. Source metadata was inspected read-only from the installed Steam DLL.
- Confirmed missing/unavailable geometry fails closed. A saved facade stop waiting for the shared frame sampling budget remains pending validation: its choice and original deadlines are retained, and no body is admitted to it until validation succeeds. Subsequent frames validate the remaining entrances, including future worker breaks. Rain and confirmed native disappearance still cancel pending stops. Scene identities are revalidated on load; saved deadlines are not regenerated.

The physical layer shares the existing pedestrian cap, radius and RLO reservations. An open-door arrival retains its completed binding through the queued admission event, then joins its selected stop with that same body. A bound arrival can keep its body, walk to the recorded spot, stop and face the target, then walk back to the original entrance. A mid-visit admission emerges from that entrance. Walking to a spot consumes the original stop window; failed approaches cancel rather than extending it. Idle standing does not consume the approach timeout. Return-path failures retain/retry the body rather than popping it at the spot. When an ordinary or meal departure commits during a physical return, its journey is held at the origin, including traffic-first routes and saves. After the body reaches the entrance, walking continues on that body or ownership transfers to the ordinary car/transit path. Queues reserve positions already occupied by optional bodies; an existing queue blocks an optional admission to the same point. Idle/facing uses the current pedestrian body; distinct smoking/phone/clapping animation clips have not been validated or added.

Participation and duration selection never read the camera. Physical approach failures can only be observed for bound visible bodies and can cancel their saved stops; this cancellation is not camera-independent. It never rerolls participation, replaces identity or extends the stop deadline.

Opening queues and safe crossing waits remain their existing mechanisms. A red-light crossing has no time cap. Optional-stop rain cancellation does not change routine eligibility or general crowd caps.

## Evidence and limits

Core tests cover scheduling, all ranges, insufficient windows, closure/rain/spot cancellation, eligibility, persistent audiences and paired conversations, cancellation/reload, strict schema migration, malformed/future refusal, original continuations, midnight visits, event-budget equivalence and route rebuilds. The source-extracted physical harness covers movement/facing/return, blocked paths, queue sharing and immutable deadlines. Selection tests cover shared budgets and RLO reservations.

These are offline checks. No game was launched or installed. Native geometry, visible approach/exit, idle poses, weather observation and performance remain unverified. The default opportunity rate is 25%; audience opportunities are limited to existing walking arrivals; no release or ordinary-save deployment is authorized.
