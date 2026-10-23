# Open issues

## Recurring road and discovery hitches: reviewed candidate, PC pending (21 October)

Candidate `782e13b1…` includes the signal/topology fix and replaces periodic global gate/station searches with retained, incremental discovery. A matched full-modpack Mac test reduced preparation maximum 562→1.7 ms and gate refresh 560→0.27 ms, with no road rebuilds or backlog. Four-model reviews, regression negative controls, native mutation/reload fixtures and a ten-minute follow-up pass. Other whole-game spikes remain, and discovery's per-call budget is soft; hours-long stability and much larger populations are unverified. [Implementation and measurements](reviews/2026-10-21-route-object-discovery.md). **Not installed on PC.** The Mac rig was restored and released.

## Resolved: clock acceleration stops Persistent City (21 October)

The Windows smoke reproduced readiness and queue-wakeup failures during accelerated time. Reviewed DLL `6e0e5b58…` fixes residual-motion rounding, reconstructs derived retimed wakeups and uses stable signal boundaries. Validated legacy schedules explicitly migrate to traffic schema 2. All 915 offline tests and 65 bounded Windows regression checks pass, including repeated 60×/1× cycles, native 6×/1×, simulated truck travel, save and fresh-process reload. The four-model panel approves the final source, and the fix is installed in both PC profiles. [Fix and limits](reviews/2026-10-21-retiming-fix.md); [installation](reviews/2026-10-21-retiming-pc-install.md). Genuine driving and full recording/performance clearance remain separate open checks.

## Earlier pedestrian findings

Found on 20 October 2026 while watching agent playtest attempt 13 on the PC rig: many pedestrians stand still in the street whatever the hour or the weather. The causes below were read in the code (game 1.0 decompile, this repo, CityPerformance), not measured in game. Line numbers are as of that day.

## 1. A walker that ends its trip on screen stays frozen as long as you watch

`AmbientPedestrians.EndCyclePrefix` holds an ordinary walker that finishes its cycle while visible: it calls `OnRelease` and stops its agent (`Runtime/AmbientPedestrians.cs`, `Hold`). Only `Update` retires held bodies, and only once they are off screen, one per frame. A walker you keep in view, for example while the camera follows the player along a street, therefore stands mid-pavement indefinitely.

Expected: a held body leaves on its own after a short, believable pause (it turns and walks out of view, or into a door), or is never stopped in the open in the first place.

Status: implemented in the development build, not checked in game. Within the game's 100 m pedestrian range a held body walks to an open business or home within 60 m after 2 to 6 seconds and goes in, or, when no such door is reachable, walks toward a point off screen or nearest the frame edge inside that range (`AmbientPedestrians.Resume`). If it is still in view there, it pauses and tries again, so on some streets it can pace while watched. Beyond that range it still stands until it is off screen, because the game's movement code skips arrivals and crossings out there.

## 2. Citizens waiting for a route stand at the door and retry every 30 game minutes

"Awaiting a route" counts as outdoors (`Runtime/PhysicalPopulation.cs`, around line 177). The citizen is placed at its current place's entrance (`Core/CitySimulator.cs`, around lines 113 and 400) and stopped (`PhysicalPopulation.cs`, around line 213). With no route found it retries every 30 game minutes (`CitySimulator.cs`, around line 285), standing at the door in between, for as long as its activity lasts. Up to 48 waiting citizens within 120 m are shown (`Runtime/PersistentCityMod.cs`, around lines 57–59).

Expected: a citizen who can't get a route waits indoors, or is not shown, rather than standing at the door for hours.

Status: implemented in the development build, not checked in game. Waiting for a route is no longer shown outdoors.

## 3. Citizens waiting for a place to open stand at its door from the moment they arrive

"Awaiting opening" places the citizen at its own entrance or at the closed destination's (`CitySimulator.cs`, around lines 191, 252 and 406) until the opening time, however early they arrived, in any weather.

Expected: they arrive a few minutes before opening, or wait somewhere fitting (a café, a bench, under cover when it rains), and form a short queue at the door only near opening time.

Status: implemented in the development build, not checked in game. They wait indoors and queue beside the door only in the last 2 to 10 minutes before opening (see `Core/DwellPlanner.cs`). Departure times are unchanged, so they still arrive early; they just don't stand outside.

## 4. The hour and the rain no longer change how many people walk the streets

The game sets the number of walking pedestrians by hour and rain through `PedestrianSpawner.SetActivePedestrians` (1.0: `TimeOfDayController.cs`, around line 552). `AmbientPedestrians.SetActivePrefix` stores the requested amount and skips the call, so the street crowd no longer thins at night or in the rain. Persistent City's own population fills the street instead, and it doesn't account for weather either.

Expected: the visible crowd follows the time of day and the weather, either through the game's request or through the simulation's own outdoor share.

## 5. CityPerformance's pedestrian refill depends on load order

Both this mod and CityPerformance hook `PedestrianSpawner.SetActivePedestrians`. CityPerformance turns its refill off if another mod already hooked that method (`big-ambitions-performance/src/CityPerformance.cs`, around lines 212–213). Which of the two takes effect therefore depends on the order the mods load in.

Expected: the two agree on one owner. For example, CityPerformance stands down whenever Persistent City is present, and logs that it did.

## Done: the game's anonymous standers are switched off

The game's stationary zones placed 2–4 anonymous standers each time a zone came into view, regardless of hour or weather (1.0: `StationaryAiSpawner.SpawnGroupOverFrames`). `Runtime/StationaryCrowds.cs` now skips their `Spawn(Vector3)`. Street performers and the homeless from the same zones remain. The proposed replacement is in [stationary-citizens.md](stationary-citizens.md). It compiles, but hasn't been checked in game yet.
