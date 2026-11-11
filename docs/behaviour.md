# What Persistent City does

Behaviour of the development line as of 11 November 2026 (X4). Where this page and the code disagree, the code is right. Status per item is on the [roadmap](roadmap.md); in-game checks still open are in the [rig checklist](rig-checklist.md). Terms are in the [glossary](glossary.md); where things live in the code is in the [code map](map.md). Older milestones are in the tags `docs-history-2026-11-10` and `tools-history-2026-11-11`.

## Dependency and startup

- Separate codebase and package. No RLO fork, source changes or upstream approval dependency.
- Required dependency: a reviewed RLO release, **0.40.4**, **0.41.1** or **0.45.0** (`RloIdentityAdapter.ReviewedVersions`). The donor catalogue is read through RLO's public `CityApi` contract 1, with the reviewed reflection reader behind it. Missing or incompatible RLO leaves the mod inactive; other releases need a compatibility review. Going Public sessions are unsupported.
- RLO's **Social Media** option and Flare supply the donor catalogue. A donor read that is off, not yet ready or refreshing is transient: a running city continues and saves on its saved catalogue. A shared, failed, incompatible or missing donor stops the mod and keeps the saved payload (`CapabilitySnapshot`).
- Every native dependency is a named **capability** with a stated fallback (19 in `NativeCapabilities`: food authority, shop checkout, dynamic menus, Street Food vehicles, the city clock and its observer, Driven Rides, RLO donor, reservations and road speeds, research dialog, billing and delivery, physical vehicles and pedestrians, the inspector's click, scroll and UI block, and the interior view). `pc capability status`, `pc capability hooks` and `pc capability show <id>` print the read-only report. A foreign Harmony patch that can skip or replace the checkout path turns shop checkout off with the owner named (F8h).
- The mod enables itself and supported food businesses automatically; no manual activation or shop enrollment. Missing configuration uses the defaults. Startup and dependency status are visible in game.
- Pinned Harmony 2.4.2 and its licence are bundled for scoped native hooks. RLO and game assemblies are never bundled. Removing the mod leaves inert own string data in the save.

## People and homes

- **Residents** come from eligible residential interiors the player can rent or buy, using RLO's 400 Flare profiles as an immutable donor catalogue. New residents get mod-owned `pc:<sequence>` IDs remixed from that source; RLO keeps its social profiles unchanged. Residents never leave the city.
- Capacity uses native `BuildingSizeData.squareMeters` for the rentable or buyable interior. Provisional occupant limits: 0 below 20 m², 1 below 40 m², 2 below 75 m², 4 below 150 m², 5 below 300 m² and 7 at 300 m² or more. Unknown or undersized interiors are omitted. The reference inventory of 335 accessible interiors gives 1,064 maximum occupants and a fresh population of 957 (90%). No Hamptons residence passes the access filters. More than 50,000 residents refuses initialization instead of truncating the city.
- **Visitors** (`pv:<n>`) live outside the city and arrive through subway stations ("portals", at most 256). A visitor is a commuter or a day visitor, has no home, car or optional stops, and travels by transit. A fresh city starts with one commuter per resident.
- **Population fill** (F5g) runs at every day boundary toward census targets: commuters up to 80% of workplace capacity minus resident workers, a day-visitor pool of 15% of residents divided by the 25% draw share, both together at most twice the residents and at most 100,000. Day visits are drawn at 13.75% on weekdays and 40% on weekend days. At most 64 persons per unit and 1,024 per boundary; without a station nobody is created (`PopulationCensus`, `PopulationFill`).
- Households are the residents of one home (`h:<HomeId>`, derived). Wallets are individual.
- Population creation and strict saved-state decoding run off the main thread after native inputs are captured. The result is published only if the save and the donor catalogue still match.

## Daily life

- Daily work or study, optional shop and leisure visits, and return home; retirees have no workplace. Missing venues stay unassigned.
- Work and study places seat persons by area band (F5e, `WorkplaceCapacity`): 2 below 40 m², 4 below 75, 6 below 150, 10 below 300, 16 below 600 and 24 from 600 m²; study places seat twice the band. A place without a positive area, and every player-owned business, seats nobody. Assignments do not create native employment or payroll.
- Work/study, shopping and leisure choices sample all eligible places with a distance bias. Native building traffic and explicit attraction weights influence choice. Leisure destinations vary by person and day; committed trips and waits keep their destination.
- Native parks, the outdoor party pier, theatre/cinema and validated Hamptons beach volumes attract longer outings. Small Beach is omitted because no complete NPC access was validated.
- **Wages** (F6): a city job pays per attended shift, four 480-minute shifts a week, wage = base income × 7 × attended minutes / 1,920 (`EmploymentWages`). Missed shifts over 14 days raise the daily job-loss chance. Workers also have a small stable chance to find a reachable vacancy. Retirees and support recipients get an outside income on the daily posting.
- Personal quiet hours vary by resident, persona and day, clipping or skipping optional outings. Work/study and committed journeys take priority.
- Resting residents stay home across midnight until a meaningful event. Commute preparation is staggered 8–14 hours before the next shift; established commuters reuse their previous successful route duration as the estimate, and departure picks a fresh route.
- Each person keeps origin, destination, journey progress and recent admitted visits. An unreachable activity waits and retries; a closed venue never counts as a successful visit. If a venue opens later within the activity window, the person waits with destination and deadline preserved.
- Optional stops (breaks, window shopping, park-edge rests, paired chats and performer audiences) target a **25%** default share of eligible resident visits, capped by feasibility. See [stationary citizens](stationary-citizens.md) and [settings](#settings).

## Walking, queues and ambient walkers

- Walking uses real building entrance samples and complete NPC navigation-mesh paths, never a straight line across disconnected streets. Walking excludes the swimming area.
- Waiting happens indoors. Customers who reached a closed venue stand in its queue only for the last 2 to 10 game minutes before opening (at most 5 in rain). Staff and students wait inside. The lead is chosen per person and day by `Core/DwellPlanner.cs`, so reloads and camera moves keep it.
- Queue spots: up to eight along the wall beside the door, 0.6 m out, on the walking navmesh and clear of neighbouring doors. Places sharing an entrance share one line. Street Food vehicles have their own customer queue.
- A random ordinary walker that ends its trip in view (within the game's 100 m pedestrian range) pauses 2 to 6 seconds, then walks to an open business or home within 60 m that it can reach, and goes in. With no such door after two pauses it walks out of view (up to 45 m) and is returned to the pool.

## Traffic

- The game adapter composes walking paths with native subway station geometry or directed native road waypoints. Road planning uses the mod's own A* search over the native directed network.
- Routes compete on estimated door-to-door minutes: access and egress walks, preparation, virtual parking, expected subway wait and road congestion. The fastest evaluated candidate wins, with stable tie order.
- Citywide cars occupy finite directed road sections in the traffic kernel even out of view: FIFO movement, signals, downstream storage and spillback. One resident and their car make exactly one demand record. Section capacity (six metres per car), 1.8-second discharge headway and sections up to 80 metres are provisional.
- New trips consider current queue delays and blocked streets; routes underway stay committed. True closed capacity cycles can gridlock; no cars are teleported to clear them.
- **Virtual parking**: road travel is visible; curb, preparation and parking transitions are background handoffs. Subway riding is hidden between station walks; there is no bus service.
- Timing policy: roads use native speed limits capped at 40 km/h; subway 10 metres per scaled second with a three-minute expected wait; car preparation and virtual parking add two minutes each; curb connectors up to eight metres. RLO potholes add their speed reduction to new routes.
- A structural road change while a road journey is committed stops the adapter and keeps the save. Temporary closures keep queued vehicles and are rechecked for reopening.

## Physical bodies

- Nearby residents and visitors get native pedestrian or vehicle bodies; ordinary random street spawns are suppressed once adapter preflight succeeds. Defaults: 48 pedestrians, 24 cars, 120-metre radius. Selection reads the presentation grid cells inside the radius (F2f).
- **Bodies follow the simulation** (F2c, F2d). A walker follows the logical pose of its committed journey: native pace up to 1.5 m behind, up to 1.5× pace at 3 m, and above 8 m it is corrected (moved when off screen; on screen it hurries for 10 s, then fades at the next corner or door). A car's native drive is capped at the traffic kernel's coordinate. No body writes logical state, and the kernel never waits for one.
- **Player holds** (F2e) are the one exception: the player's own avatar (within 1 m ahead) or vehicle (within 4 m ahead) can delay a person by at most one game minute per journey. Holds are logged and replayed on load (`PlayerHolds`).
- Physical cars borrow only inactive ordinary native slots, with no native vehicle save ownership. Existing native ambient vehicles retire gradually off screen. Native customers, workers, service vehicles, stationary scene actors and RLO scenes are explicit exceptions.
- Fast-forward (6×), indoor travel and time-machine skips release bodies; the same journeys continue in the background. A recoverable adapter failure restores ordinary spawning; uncertain cleanup stops saving and requires a reload.

## Simulation pace

- The default calendar factor is one third: three times as much real time per game minute, without changing movement or Unity physics. Schedules and deadlines stay in game minutes.
- Simulation work is bounded per frame (128–1,024 units, 16–128 person events, 2–4 ms) inside a native-minute accounting epoch (`AccountingClock`). When it falls behind, the mod holds native time and shows its pacing status; if it loses an epoch it stops PC-owned food work safely and releases the native clock.

## Food businesses and residents

- Eligible player food businesses are taken over automatically at a verified hourly boundary; unsupported businesses stay native. Native inventory, orders, receipts and settlement keep sale authority. Outside an owned food scope, visits have no economic effect.
- Each person (resident or visitor) has one meal roster record with its own wallet, memory and receipts. Resident wallets get income, essential expenses and a paid home-food alternative; visitor wallets are an endowment. Money is exact integer cents ([native-money rules](decisions/2026-10-28-native-money.md)); purchases use idempotent sale receipts.
- Choice is probabilistic and seeded per save, so a reload replays the same day. Traits and seeded per-person product tastes raise or lower chances; price, distance, queue, quality, time pressure and household weights are settings.
- Memory: each person remembers about four shops. Loyal people return, curious ones explore; stockouts, closures and long queues lower the chance of returning. Regulars per own shop are counted from served visits.
- Campaigns: active native marketing campaigns raise choice preference and reach nearby residents. Awareness makes a visit possible; traits still decide.
- Rivals: people can choose rival food shops. Rivals are read-only; spending there goes to an own outflow ledger with estimated service lanes and sell-out per meal period. Nothing is written to rival finances.
- **Ordinary exchanges** (F6): non-food retail and leisure visits charge the person's wallet a modeled outside price ($5.00 and $12.00 by default, `OrdinaryRetailVisitCents` and `OrdinaryLeisureVisitCents`); nothing is paid to native businesses.
- Weather: rain softly changes new outings and food choices and shortens outdoor queues.
- Menus: four products per shop by default (`FoodMenuMaximumOffersPerShop`, at most 8). Any loaded food product is admitted once the loaded catalogue contract has warmed up and every capability check passes; drinks, bundles, non-food and unknown products keep the whole shop native (`FoodMenuPolicy`).
- **Street Food carts and food trucks** (E6): with the Street Food mod present, its vehicles are served through `VendorApi` contract 1. Up to 64 live vehicles; a vehicle with more than four offers stays with Street Food. Street Food owns stock, ledgers and money; Persistent City holds only the receipt. `pc vendor status|sales|withdraw` inspect them.
- **Market research** (C5) is active. The native Marketing Agency dialog offers three reports for a food business Persistent City owns: basic local ($500), detailed local ($2,000) and detailed city ($6,000). The fee is charged by one native money change at the agency's daily run and the report arrives as an agency text message. One open order per business, eight per city.

## Inspector and panels

- Click a resident to see home, work, traits in words and what they ate or skipped and why.
- **Ctrl+F8** opens the business panel: pages of 16 shops with week-over-week purchases, sales, causes and meal periods, the change journal, native account readouts, rival outflow, a city economy section, the population headline (residents, visitors, stations), accounting lag and a research-reports button.
- Console: `pc population census [workplaces]`, `pc population person <id>`, `pc population manifest`, `pc capability …`, `pc ordinary status`, `pc food contract …`, `pc movement …`, `pc vendor …`, and `pcresearch status|orders|report <id>|window open|close`. `pc food decisions` and `pc workplace close|open` work only in fixture cities.

## Saves

- Own state is in `persistentcity:state_v1`, a compact columnar string (`pcc-c`) at schema 1, which is held until release ([decision](decisions/2026-11-09-schema-one.md)). The format changes in place; nothing migrates. A city from an older development build is kept and refused with a "start a new city" status. Corrupt or foreign data is kept and refused, never reset. Details: [save format](save-format.md).
- Saved cities keep identities, assignments, cars, traffic queues, committed journeys, wallets, receipts, research orders and Street Food vendor rows.
- The side key `persistentcity:save-epoch_v1` describes each save. A save that carries a failure or detach marker, or whose payload length, population key or minute does not match its marker, is refused at load.
- A genuine clock-modifier change re-evaluates residents safely at home and preserves journey positions; a change that cannot be represented exactly is refused and the mod stops for that session.

## Settings

Edit `<game directory>/ModsLocal/PersistentCity/persistent-city.json` with the city unloaded, then reload. The source template is `config/persistent-city.json`; the full key table is in the [code map](map.md#settings).

- `OptionalStopChoicePercentage` (0–100, default 25): target share of eligible visits that visibly get a scheduled optional stop. Out-of-range numbers clamp; missing or non-numeric values use 25. Only newly admitted visits use it. Details are in [stationary citizens](stationary-citizens.md).
- Food choice, memory, campaign, rival, menu, weather, street-life and market-research keys tune the simulation within clamped ranges.
- `GameClockFactor` (0.1–1, default one third) and the body limits are developer settings: a value outside its range refuses to start. `InteriorCityView` and the third-party interior guard are developer settings for the F0 experiment.

## Known limitations

- The place catalogue, assignments and weekly hours are captured at initialization. Food shops refresh their opening hours; other business changes and new buildings do not refresh yet.
- Destination weighting uses capacity, district and straight-line distance without checking every route or opening-hour overlap. Disconnected venues stay assigned and retry.
- Simulation can lag after a time skip; diagnostics show its clock beside the native clock. A single native route query cannot be preempted.
- Public access checks exclude private gates and unowned DLC locations. Station and outdoor source IDs are tied to the reviewed game build.
- Social and employee identity unification with RLO and native employees is deferred. Wages are a modeled outside flow, not native payroll.
- Track F interior view: the default-off F0 experiment (`InteriorCityView`) can keep bodies near shop entrances while the player is inside; drawing the city from interiors is a no-op (F1 NO-GO). The third-party interior guard can veto it.
- Windows performance over long soaks and user playtesting are not verified.

## Continuity rules

Binding rules carried over from the October design page, updated for bodies that follow the simulation.

- A route is contiguous and ends at a known destination. Unreachable routes wait and retry; arrival is never fabricated.
- Player and camera position change only representation, never which person exists or what they did. The player-hold rule above is the only visible-world effect on the simulation.
- Travel completes before another activity can start. Late or closed arrivals may lose an activity; they do not teleport to recover a timetable.
- Personal quiet hours affect optional outings; work/study and committed journeys keep priority. Commute preparation stores a duration estimate; departure always selects a fresh route.
- An attainable later opening uses a persisted waiting state with the original destination and activity deadline.
- A simulation step has a bounded event budget and records how far it has caught up. A snapshot between simultaneous events is valid and deterministic.
- The immutable RLO source roster stays separate from active residents; source profiles remain in RLO.
- Persist actual assignments and journeys. Reject incompatible snapshots instead of re-rolling people or discarding newer fields.
- Native housing inputs use only known rentable or buyable interior area and current access checks. Gated private estates stay excluded regardless of player ownership.
- Road presentation requires the persisted topology fingerprint and directed node sequence. No waypoint match means no body; the adapter never fabricates a road route or borrows an active foreign vehicle.
- Pausing keeps bodies. Time-machine skips, interiors and native 6× release them and continue in the background; normal speed reacquires eligible bodies. Unloading restores native pause and spawn behaviour.
