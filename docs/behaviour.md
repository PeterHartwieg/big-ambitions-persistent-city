# What Persistent City does

Behaviour of the development line as of 6 November 2026. Slices merged since then (movement, population, economy, research, carts) are not described here yet; roadmap row X4 brings this page up to date. Where the page and the code disagree, the code is right. Status per slice is in [CHECKS.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/CHECKS.md); nothing here is verified by rig yet. Historical milestones and measurements are in [history-2026-10-development.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/history-2026-10-development.md#readme-body-before-the-6-nov-2026-restructure-moved-6-nov-2026). Terms are in the [glossary](glossary.md).

## Dependency and startup

- Separate codebase and package. No RLO fork, source changes or upstream approval dependency.
- Required dependency: reviewed RLO **0.41.1** or **0.40.4**. Missing, unready or incompatible RLO leaves the mod inactive. RLO releases outside this reviewed pair need a compatibility review; the fail-closed check is deliberate. Going Public sessions are unsupported.
- RLO's **Social Media** option must be enabled and Flare running. If it is disabled after the mod starts, Persistent City stops and keeps its saved payload; re-enable the option and reload the city to resume.
- The mod enables itself and supported food businesses automatically; no manual activation or shop enrollment. Missing or partial configuration uses the enabled defaults. Startup and dependency status are visible in game.
- Pinned Harmony 2.4.2 and its licence are bundled for scoped native hooks. RLO and game assemblies are never bundled. Removing the mod leaves inert own string data in the save.

## Residents and homes

- Population comes from eligible residential interiors the player can rent or buy, using RLO's 400 Flare profiles as an immutable donor catalogue. New residents get mod-owned `pc:<sequence>` IDs by remixing that source; RLO keeps its original social profiles unchanged.
- Capacity uses native `BuildingSizeData.squareMeters` for the rentable or buyable interior, with no whole-property fallback. Provisional occupant limits: 0 below 20 m², 1 below 40 m², 2 below 75 m², 4 below 150 m², 5 below 300 m² and 7 at 300 m² or more. Unknown or undersized interiors are omitted. Household relationships and staff roles are not implemented yet (planned in F5).
- The reference inventory of 335 accessible interiors gives 1,064 maximum occupants and a fresh population target of 957 (90% of occupant capacity). No Hamptons residence passed the access filters; gated private estates, fixed AI villas and special-rival homes are excluded. More than 50,000 residents refuses initialization instead of truncating the city.
- Homes respect district preference and capacity. Residents and their cars stay in the city.
- Population creation and strict saved-state decoding run off the main thread after native inputs are captured. The result is published only if the save and RLO donor catalogue still match.

## Daily life

- Daily work or study, optional shop and leisure visits, and return home; retirees have no workplace. Missing venues stay unassigned.
- Work/study, shopping and leisure choices sample all eligible places with a distance bias. Native building traffic and explicit attraction weights influence choice. Leisure destinations vary by person and day; committed trips and waits keep their destination.
- Sixteen assignments per business or study place is a provisional allocation. Residents can stay without a work or study assignment when those run out. Assignments do not create native employment or payroll.
- Native parks, the outdoor party pier, theatre/cinema and validated Hamptons beach volumes attract longer outings. Big Beach passed native access checks; Small Beach is omitted because no complete NPC access was validated. Outdoor attraction weights are policy, not measured visitor counts.
- Workers have a small stable daily chance to lose a job or find a reachable vacancy. Checks are bounded and apply safely at home; students and retirees keep their routines. Income changes apply to the next unposted day. Rates and support fractions are in `Core/CityBalance.cs`.
- Personal quiet hours vary by resident, persona and day, clipping or skipping optional outings. Work/study and committed journeys take priority. This is a home preference, not a sleep or needs simulation.
- Resting residents stay home across midnight until a meaningful event. Commute preparation is staggered 8–14 hours before the next shift. Established commuters reuse their previous successful route duration as the estimate; only the estimate and work window persist, and departure picks a fresh route. A longer route can still arrive late; a shorter one arrives early and waits.
- Each citizen keeps origin, destination, journey progress and recent admitted visits. An unreachable activity waits and retries; a closed venue never counts as a successful visit. If a venue opens later within the activity window, the citizen waits with destination and deadline preserved.
- Optional stops (breaks, window shopping, park-edge rests, paired chats and performer audiences) target a **25%** default share of eligible visits, capped by feasibility. See [stationary citizens](stationary-citizens.md) and [settings](#settings).

## Walking, queues and ambient walkers

- Walking uses real building entrance samples and complete NPC navigation-mesh paths, never a straight line across disconnected streets. Walkers follow committed corners with stable seeded appearances, at the native pedestrian pool's 2 metres per scaled second. Walking excludes the swimming area.
- Waiting happens indoors. Customers who reached a closed venue stand in its queue only for the last 2 to 10 game minutes before opening (at most 5 in rain, when about half stay inside). Staff and students wait inside. The lead is chosen per citizen and day by `Core/DwellPlanner.cs`, so reloads and camera moves keep it.
- Queue spots: up to eight along the wall beside the door, 0.6 m out, on the walking navmesh and clear of neighbouring doors. Places sharing an entrance share one line. Newcomers join at the back, nobody is placed on an occupied spot, a full line leaves the rest inside, and a queuer who cannot reach their spot within 20 seconds of walking goes back inside. At opening they walk in.
- Waiting to cross is the native red-light hold, with no time cap.
- A random ordinary walker that ends its trip in view (within the game's 100 m pedestrian range) pauses 2 to 6 seconds, then walks to an open business or home within 60 m that it can reach, and goes in. With no such door after two pauses it walks out of view (up to 45 m) and is returned to the pool. It keeps its appearance and never gets a new random native route. Beyond 100 m it stays put until it is off screen.

## Traffic

- The game adapter composes walking paths with native subway station geometry or directed native road waypoints. Road planning uses the mod's own A* search over the native directed network; physical cars use native steering, signals and avoidance.
- Routes compete on estimated door-to-door minutes: access and egress walks, preparation, virtual parking, expected subway wait and road congestion. The fastest evaluated candidate wins, with stable tie order. Station and curb candidates are bounded, so this is the best evaluated route, not a citywide optimum.
- Citywide cars occupy finite directed road sections even out of view: FIFO movement, signals, downstream storage and spillback. One resident and their logical car make exactly one demand record; no anonymous demand is added. Section capacity (six metres per car), 1.8-second discharge headway and sections up to 80 metres are provisional.
- New trips consider current queue delays and blocked streets; routes underway stay committed. True closed capacity cycles can gridlock; no cars are teleported to clear them.
- **Virtual parking**: road travel is visible, curb, preparation and parking transitions are background handoffs. A car stays at its saved curb position and the resident walks back to it. Subway riding is hidden between station walks; there is no bus service.
- Timing policy: roads use native speed limits capped at 40 km/h; subway 10 metres per scaled second with a three-minute expected wait; car preparation and virtual parking add two minutes each; curb connectors up to eight metres. RLO potholes keep road identities and capacity and add their speed reduction to new routes.
- A structural road change while a road journey is committed stops the adapter and keeps the save. Temporary closures keep queued vehicles and are rechecked for reopening.

## Physical bodies

- Nearby citizens get native pedestrian or vehicle bodies; ordinary random street spawns are suppressed once adapter preflight succeeds. Defaults: 48 pedestrians, 24 cars, 120-metre selection radius. Physical selection iterates only persons travelling, queueing or outdoors (F5f).
- While a body is visible, measured motion is authoritative: lights, avoidance and collisions can delay arrival. Releasing the body resumes background travel from the last observed progress. Rendering never creates a resident or a destination.
- Physical cars borrow only inactive ordinary native slots, with no native vehicle save ownership. Cars are acquired outside junction traversals with room for their whole body; the oversized clearance box can withhold cars on narrow or busy streets.
- Existing native ambient vehicles retire gradually off screen while traffic runs. Native customers, workers, service vehicles, stationary scene actors and RLO scenes are explicit exceptions. An original Flare resident shown by RLO is withheld; generated residents are never matched by shared appearance.
- Fast-forward (6×), default indoor travel and time-machine skips release bodies and continue the same journeys in the background. Physical fast-forward is deferred. A recoverable adapter failure restores ordinary spawning and keeps background simulation; uncertain cleanup stops saving and requires a reload.

## Simulation pace

- The default calendar factor is one third: three times as much real time per game minute, without changing movement or Unity physics. Schedules, budgets and deadlines stay in game minutes; time-machine skips keep their target duration.
- Simulation work is bounded per frame and nested inside a native-minute accounting epoch (audit F1). When it falls behind, the mod applies backpressure and shows its pacing status; if it loses an epoch it stops PC-owned food work safely and releases the native clock. Bulk household postings and route queries run in bounded units (audit F3).

## Food businesses and residents

- Eligible player food businesses are taken over automatically at a verified hourly boundary; unsupported businesses stay native. Native inventory, orders, receipts and settlement keep sale authority. Outside an owned food scope, visits have no economic effect.
- Each resident carries a represented-consumer roster (default two consumers per resident) with its own balance, outside income or support, essential expenses and a paid home-food alternative. Purchases use exact integer money ([native-money rules](decisions/2026-10-28-native-money.md)) and idempotent sale receipts.
- Choice is probabilistic and seeded per save, so a reload replays the same day. Traits (taste, price sensitivity, patience, loyalty or curiosity, travel willingness and more) and seeded per-resident product tastes raise or lower chances; distance favours near shops without ruling out far ones.
- Memory: each consumer remembers about four shops. Loyal consumers return, curious ones explore; stockouts, closures and long queues lower the chance of returning. Regulars per own shop are counted from served visits.
- Campaigns: active native marketing campaigns raise choice preference and soften distance for every resident (Peter's 1 November decision). Awareness makes a visit possible; traits still decide.
- Rivals: residents can choose rival food shops. Rivals are read-only; spending at them is recorded in an own outflow ledger, and rivals have estimated service lanes and sell out per meal period (labelled estimated). Nothing is written to rival finances.
- Weather: rain softly changes new outings and food choices and shortens outdoor queues.
- Menus: the live menu limit is four products per shop. The original supported menu is croissants, burgers and hot dogs under native equipment rules. Complete-menu support for any loaded food product (E5b) is partial.
- Market research (C4) exists as an inactive engine; paid research stays off because no safe native fee path was found.

## Inspector and panels

- Click a resident to see home, work, traits in words and what they ate or skipped and why.
- **Ctrl+F8** opens the business feedback panel: week-over-week purchases, sales and causes per managed shop from 14 days of history, budget-state counts and a neutral rival estimate label, plus a fourteen-day rival outflow line.
- `pc population census` prints a read-only world census of beds, households, workplace capacity and implied trips.

## Saves

- Own state is in the `persistentcity:state_v1` key, written at schema 1, which is held until release ([decision](decisions/2026-11-09-schema-one.md)). Nothing migrates. A city from an older development build is kept and refused with a "start a new city" status. Corrupt or foreign data is kept and refused, never reset.
- Saved cities keep identities, assignments, logical cars, traffic ownership, committed journeys, consumer balances and receipts. Restored physical leases resume in the background until a body is reacquired.
- A side key `persistentcity:save-epoch_v1` describes each save; the mod refuses a save that carries its failure or detach marker (audit F7b).
- A genuine clock-modifier change re-evaluates residents safely at home and preserves journey positions; a change that cannot be represented exactly is refused and the mod stops for that session.

## Settings

Edit `<game directory>/ModsLocal/PersistentCity/persistent-city.json` with the city unloaded, then reload. The source template is `config/persistent-city.json`.

- `OptionalStopChoicePercentage` (0–100, default 25): target share of eligible visits that visibly get a scheduled optional stop. 0 means no new stops; 100 means every visit with a feasible stop gets one. Out-of-range numbers clamp; missing or non-numeric values use 25. Only newly admitted visits use it; saved choices never re-roll. Details and measured shares are in [stationary citizens](stationary-citizens.md).
- `GameClockFactor` (0.1–1, default one third): developer override for the calendar factor. It applies only after the mod starts and is restored on shutdown. While it owns a slower clock, Driven Rides' positive ride-clock multiplier defers to it.
- Rendering limits and an explicit disable override are developer diagnostics, not gameplay settings.

## Known limitations

- The place catalogue, assignments and weekly hours are captured at initialization. Food shops refresh their opening hours; other business changes and new buildings do not refresh yet. A map or game update needs a reviewed change.
- Destination weighting uses capacity, district and straight-line distance without checking every route or opening-hour overlap. Disconnected venues stay assigned and retry. Reachability-aware assignment and live venue refresh are not done.
- Simulation can lag after a time skip; diagnostics show its clock beside the native clock. A single native route query cannot be preempted.
- Public access checks exclude private gates and unowned DLC locations. Station and outdoor source IDs are tied to the reviewed game build.
- Taxi replenishment targets the initial active taxi count; availability depends on native spawns.
- Social and employee identity unification with RLO and native employees is deferred; resident income is an explicit outside flow.
- Track F interior view: the default-off F0 experiment can keep bodies at shop entrances, but city drawing from interiors is a no-op (F1 NO-GO).
- Windows performance over long soaks and user playtesting are not verified.

## Continuity rules

Binding rules carried over from the October design page. Residents no longer depart or get replaced; where a rule says so, they stay in the city.

- A route is contiguous and ends at a known destination. Unreachable routes wait and retry; arrival is never fabricated.
- Player/camera position must change only representation quality, never determine which citizen exists or what they did.
- Travel completes before another activity can start. Late/closed arrivals may lose an activity; they do not teleport to recover a timetable.
- Personal quiet hours affect optional outings; work/study and committed journeys/waits retain priority. Home spans cross midnight. Commute preparation is staggered and stores a duration estimate; departure always selects a fresh route. See the [daily-rhythm policy and checks](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/reviews/2026-10-19-daily-rhythms.md).
- An attainable later opening uses a persisted waiting state with the original destination and activity deadline. It never replaces that deadline with a return-home wake time. Waiting is at the current physical entrance; admission is recorded separately.
- A simulation step has a bounded event budget and records how far it has caught up. A snapshot between simultaneous events is valid and deterministic.
- Keep the immutable RLO source roster separate from active residents. Departures happen only at home, release simulated assignments and allocate a nonreused mod-owned identity remixed from source donors. Source profiles remain in RLO.
- Choose work and leisure with a seeded distance bias, allowing farther attractive venues. Daily leisure choices remain fixed once a trip or waiting period begins.
- Persist actual assignments and journeys. Reject incompatible snapshots instead of re-rolling people or discarding newer fields. Housing-policy migrations preserve existing occupants and physical events, and cannot silently shrink an experimental legacy population.
- Native housing inputs use only known rentable/buyable interior area and current access checks. Gated private estates remain excluded regardless of player ownership; fixed AI villas and special-rival homes receive read-only catalogue filters. The reference Hamptons residences are currently excluded by their gates.
- A renderer reports physical delays back to the simulation. Arrival uses the normal admission queue only after measured motion reaches the route end. Small native avoidance detours withhold unproven progress while continuing the already validated corner path; they do not permanently latch a stop flag.
- Road presentation requires the persisted topology fingerprint and directed node sequence. No current waypoint match means no body lease; it never fabricates a road route or borrows an active foreign vehicle. Actual native corner evidence gates segment completion, including stop lines and multiple physics steps between rendered frames.
- Pausing retains physical bodies. Time-machine skips and interiors release them before background catch-up. Native 6× also releases owned bodies and continues background travel after the game's normal traffic clear; normal speed reacquires eligible bodies. Unloading restores native pause/spawn behaviour.
- One later population ID maps Flare, social contacts, employees, scene actors and passengers. The prototype currently imports Flare only and claims no such unification.
