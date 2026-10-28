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

## Remaining decision and activation status

**The automatic opportunity rate remains unset.** Peter selected the kinds, ranges, rain rule, night policy and audience ownership, but did not specify frequency. `persistent-city-DESIGN-QUESTIONS-3.md` records context, options and a recommendation: one deterministic opportunity per existing visit (mid-shift for workers), with 25%, 100% or 10% participation. A question was sent in this session; no answer is assumed.

The scheduler, persistence, Runtime integration and tests are implemented. Runtime attaches the environment with `OptionalStopPolicy.ApprovedChoicePercentage`, currently `null`: it restores/cancels saved stops but does not create new ones. Tests supply explicit percentages. This is an **incomplete activation decision**, not a shipped feature or a claim of new visible stops. No user-facing tuning control or manual enrollment was added.

## Scheduling and continuation

`Core/OptionalStopScheduling.cs` evaluates a newly admitted visit once, after its ordinary event commits. Stable citizen/place/visit keys choose participation and duration. Worker opportunities start midway through the remaining shift. Other opportunities start at admission. There is no camera input, frame-random roll or recurring chance while standing. If time before closure or the existing commitment is below the kind's minimum, the stop is skipped; otherwise the shared `OptionalStops.Plan` clips the duration to that commitment and cap.

The stop overlays the existing admitted activity: the citizen's place, activity, original event/deadline, identity and assignments remain intact. The existing routine event owns the continuation and clears the record. Stops do not delay work, create travel or restart on reload. Food outings and existing native-food service have priority: meal outings cannot start a stop, food-owned shops are excluded, and an accepted meal outing or existing-stop food decision cancels any stop. Cancellation does not alter funds or sales.

Chatting forms a pair from eligible persistent visitors at the same open nightlife place. Both spots and a common deadline are chosen before either member is published. Membership, facing and the common group identity persist. Missing/cancelled partners cancel the conversation; no singleton or anonymous filler is created. The first visitor may wait indoors for a later eligible visitor without creating a separate meeting trip. There is no minimum evening crowd.

Audience opportunities currently use pedestrians completing an existing walking arrival at a venue entrance within eight metres of a live performer. They do not divert routines toward a show or interrupt an in-progress road/transit leg. This is an entrance-passer implementation, **not general mid-route audience interception**. Each member chooses an individual timed stop and keeps its own end; one member is a valid audience. Performer disappearance, replacement, movement away from the recorded target or any rain cancels it. The audience is not native stationary-zone population.

## Saved state

City schema **15** adds nullable `CitizenState.OptionalStop`: kind, place, stable spot/target identity, spot/facing/return geometry, start/end, original visit/event and conversation members/cancellation. The existing citizen activity remains the continuation; there is no second general itinerary or copied identity.

Strict schema-13 and schema-14 migration initializes no active stops. It preserves balances, receipt evidence, assignments and interrupted journeys, retaining the previous schema-13 monetary validation. Loading an old save never infers a stop for a current visit. A later newly admitted visit can become an opportunity once a rate is approved. Current payloads must contain the new field, even when null; legacy payloads containing it are refused. Future/invalid payloads are preserved and refused, never overwritten.

Validation checks kind eligibility, bounded duration, finite geometry, original activity continuation, distinct group slots and reciprocal membership for active conversations. Cancelled/expired records do not restart. Route rebuilds retain the environment and opportunity setting; event working copies own their stop/member copies.

## Runtime and physical continuity

`Runtime/OptionalStopEnvironment.cs` supplies validated geometry independently of the camera:

- Door/entrance stops reuse the off-flow facade sampling in `DwellSpots`, with complete NPC paths and neighbouring-door clearance.
- Chat eligibility checks the native established nightclub category; other leisure categories are not silently treated as nightlife.
- Rest spots are sampled at actual edges of an existing accepted native park volume, inside that volume and reachable on the dry NPC navmesh. No decorative object is assumed to be a usable bench. Bench-specific seating/animations are not claimed.
- Performer-ring points require a live retained performer, complete walking access, small sampling displacement and proximity to a navmesh edge. The performer is read through its native stationary-spawner reference; no spawn method is called. Source metadata was inspected read-only from the installed Steam DLL.
- Missing/unavailable geometry fails closed. Scene identities are revalidated on load; saved deadlines are not regenerated.

The physical layer shares the existing pedestrian cap, radius and RLO reservations. A bound arrival can keep its body, walk to the recorded spot, stop and face the target, then walk back to the original entrance. A mid-visit admission emerges from that entrance. Walking to a spot consumes the original stop window; failed approaches cancel rather than extending it. Idle standing does not consume the approach timeout. Return-path failures retain/retry the body rather than popping it at the spot. Queues reserve positions already occupied by optional bodies; an existing queue blocks an optional admission to the same point. Idle/facing uses the current pedestrian body; distinct smoking/phone/clapping animation clips have not been validated or added.

Opening queues and safe crossing waits remain their existing mechanisms. A red-light crossing has no time cap. Optional-stop rain cancellation does not change routine eligibility or general crowd caps.

## Evidence and limits

Core tests cover scheduling, all ranges, insufficient windows, closure/rain/spot cancellation, eligibility, persistent audiences and paired conversations, cancellation/reload, strict schema migration, malformed/future refusal, original continuations, midnight visits, event-budget equivalence and route rebuilds. The source-extracted physical harness covers movement/facing/return, blocked paths, queue sharing and immutable deadlines. Selection tests cover shared budgets and RLO reservations.

These are offline checks. No game was launched or installed. Native geometry, visible approach/exit, idle poses, weather observation and performance remain unverified. The opportunity rate remains open; audience opportunities are limited to existing walking arrivals; no release or ordinary-save deployment is authorized.
