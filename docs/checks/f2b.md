Status: implemented (offline); not verified by rig

# City audit F2b — read-only presentation boundary, 8 November 2026

Refs [#89](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/89). Branch `codex/pc-f2b-presentation-boundary` from `origin/codex/persistent-city` at `3f734cb` (writer **33**). Scope: the F2b row and Decision 3 of [the F2 plan](../plans/f2-movement.md#decision-3-remove-body-originated-logical-writes-f2b), with Peter's accepted section 9 defaults ([6 Nov decisions](../decisions/2026-11-06-design-defaults.md)). The plan's writer-25 references are historical; this slice adds no saved state, no schema bump and no migration, and the pinned current-writer golden hash is unchanged (no Persistence file is touched).

Re-read at this base: `Core/WeatherPolicy.cs` (`CaptureQueueWeather`, `QueueAtOpening`), `Core/CitySimulator.cs` (`WaitForOpening`, which already captured the queue weather at the logical wait event), `Core/OptionalStopScheduling.cs` (`RefreshOptionalStops`, `CancelOptionalStop`, `Occupied`), and in `Runtime/PhysicalPopulation.cs` `PlanQueues`, `QueueDwell`, `Standing`, `TryJoinOptional`, `ObserveOptional` and `ObserveForSave`.

## What is implemented

- **No body-driven stop cancellation.** `Runtime/PhysicalPopulation.cs` no longer calls `CancelOptionalStop`. In `TryJoinOptional`, a door queue holding the facade spot or a refused path to the spot sets a new presentation flag, `OptionalStandsShort`: the body stands where it is (at the entrance it arrived at, or where it was acquired) facing the stop. In `ObserveOptional`, 20 s of real walking without reaching the spot (`QueueStepSeconds`) sets the same flag instead of cancelling. The saved stop, its conversation partner and the spot occupancy Core plans against (`Occupied`) stay as Core left them. A body standing short does not reserve the queue slot at the stop's spot (`Standing`), since it is not on it.
- **Runtime rain check removed.** `RainHelper.isRaining` is gone from the body path. Rain still cancels stops logically in `RefreshOptionalStops` (through `OptionalStopEnvironment.Raining`), and the body then walks back because the saved stop is no longer active. That is now the only way a body leaves its stop early.
- **Door-queue weather is read-only.** `QueueAtOpening` returns no queue dwell when the person's capture is missing or belongs to another boundary; it never samples weather or writes `W`. The capture is taken only by the logical wait event (`WaitForOpening` → `CaptureQueueWeather`, now `internal` so tests can play that event).
- **Allowed-member source test.** `PresentationBoundaryTests.PhysicalRuntimeCallsOnlyTheReviewedSimulationMembers` scans every `Runtime/Physical*.cs` for `simulation.<member>` and requires the set to equal a reviewed list exactly (no unlisted member, no stale entry), each member to exist on `CitySimulator`, and no assignment through `simulation.State`. The list has three groups:
  - read-only: `State`, `ActiveJourneys`, `ActiveJourneyIndexMatchesState`, `ActivityObserved` (event subscription), `FindPlace`, `OptionalStopStatus`, `PositionOf`, `QueueAtOpening`, `SampleJourney`, `TryGetTrafficPermission`;
  - lease API, allowed until F2c/F2d remove it: `TryAcquirePhysicalJourney`, `ObservePhysicalJourney`, `ReleasePhysicalJourney`, `IsPhysicalJourneyValid`, `TrySamplePhysicalJourney`, `TryHoldPhysicalDeparture`;
  - logical refresh: `RefreshOptionalStops`, which `ObserveForSave` runs before a save. It applies Core's own rules (rain, closing, phase, availability, partners), the same call `PersistentCityMod` makes every frame, and its answer does not depend on any body. It is listed separately so a reviewer sees it; it is not a body-originated write. Its availability input is F2h's subject.

### Technical choices

- "Nearest reachable point" (Decision 3) is the body's current position. The body reached it on the NavMesh, so it is reachable, and no extra path queries per frame are needed. A standing-short body is not retried toward the spot; it stays until the stop ends logically.
- `CancelOptionalStop` stays in Core for its logical callers (meal outings in `FoodStops`/`FoodWeightedCommerce`, optional visits, a replaced visit). Only the Runtime callers are removed.
- Two existing `WeatherTests` cases set up a wait by hand and relied on `QueueAtOpening` to capture. They now play the wait event's capture explicitly; the second one also asserts that a new opening gets no queue dwell from the query until its own capture.
- The pedestrian body double (`tests/VendorPhysicalRuntimeDouble.cs`) gained a static `Unreachable` hook so a test can refuse a NavMesh path.

## Offline checks

Build: `python tools/build.py --game-managed "C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"` passed. `dotnet test tests/PersistentCity.Tests.csproj`: 3,904 passed, 0 failed.

`tests/PresentationBoundaryTests.cs` (production `PhysicalPopulation` on the native scene doubles, a saved two-person conversation):

| Test | Asserts | Before this change |
| --- | --- | --- |
| `OptionalStopSurvivesAnUnreachableSpot` | refused path: body stays bound, stopped, facing the stop; neither stop cancelled; stops, members, phases and events unchanged | red (body released, stop cancelled) |
| `OptionalStopSurvivesAQueueHoldingItsSpot` | queue body on the spot: optional body stands short without targeting the spot; both stops and the queue body unchanged | red |
| `OptionalStopSurvivesTwentySecondsOfBlockedWalking` | 25 s of walking without progress: body stops short, path dropped; stops and both member lists unchanged, also after `ObserveForSave` | red |
| `LogicalEndOfTheStopStillSendsTheBodyBack` | a Core cancellation sends the body back to the entrance; the body writes nothing | red (body already released at 20 s) |
| `QueueWeatherIsCapturedAtTheLogicalWaitEvent` | `AdvanceTo` into a wait captures `W` at the opening boundary before any query; a later query with changed weather reads nothing new and leaves the save bytes equal | green (Core already captured there) |
| `QueueAtOpeningWritesNothingAndGivesNoDwellWithoutACapture` | weather-source spy: no capture or a stale one gives no dwell, 0 source reads, `W` and save bytes unchanged; with the event's capture the answer equals `DwellPlanner.QueueAtOpening` on it, still with no read or write | red |
| `PhysicalRuntimeCallsOnlyTheReviewedSimulationMembers` | the exact allowed-member list above | red (`CancelOptionalStop`, `RainHelper`) |

"Before" was taken by putting the base `Runtime/PhysicalPopulation.cs` and `Core/WeatherPolicy.cs` back (with only `CaptureQueueWeather` made internal so the tests compile) and running the class: 6 failed, 1 passed.

## In-game checks for the next batched rig

Fixture: a disposable FT city on the owned rig with Persistent City and RLO, a shop or venue front within 120 m of a reachable player spot, autosave off and verified before every window (also after reload). These are the checks F2b makes meaningful; full camera equivalence still needs F2c/F2d (plan checks 1–3) and F2h (plan check 11).

1. **A blocked body does not cancel its optional stop.** Setup: checkpoint `optional_stop_visible` with a resident visibly walking to or standing at an optional stop. Steps: arm A teleports the player onto the stop spot and stands there 60 real seconds, then runs to the stop's end minute; arm B loads the same checkpoint with `PhysicalEnabled = false` and runs to the same minute; extract the PC save from both. Expected: `pcsave_compare` equal on `journeys` and `meals_campaigns`, including the saved optional stop, its `Cancelled` flag and the partner's stop; the body stands near the spot or entrance in arm A's captures. **rig-automated (existing capability: FT `teleport`/`time`, RIG-VIEW `capture`, RIG-PCSAVE `pcsave_compare`; pending capability: RIG-CHECKPOINTS state `optional_stop_visible`, RIG-PCCONFIG row for `PhysicalEnabled`).**
2. **Rain ends optional stops logically, with or without bodies.** Setup: check 1's checkpoint. Steps: start native rain at the same minute in both arms (A: player beside the stop; B: `PhysicalEnabled = false`), run 10 game minutes, extract. Expected: the stops are cancelled in both arms with equal `journeys` projections; in arm A the body walks back to the entrance after the cancellation. **rig-automated (existing capability: FT `time`/`teleport`, RIG-PCSAVE; pending capability: a rig weather control to start native rain at a stamped minute, RIG-PCCONFIG `PhysicalEnabled`).**
3. **Door-queue weather does not depend on the camera.** Setup: checkpoint 15 game minutes before a shop opens, with residents due to wait at its door. Steps: arm A keeps the player beside the door; arm B teleports the player 1 km away; start native rain 2 game minutes after the waits begin in both; run past the opening; extract. Expected: equal saved resident weather captures (`W`) and equal `journeys` projections; no `W` written after the wait began in either arm. **rig-automated (existing capability: FT `teleport`/`time`, RIG-PCSAVE; pending capability: a RIG-PCSAVE projection that includes the resident weather capture `W`, if `journeys` does not, and the rig weather control from check 2).**

Offline results above are not native evidence. These checks are batched for the next rig run and do not gate merge.
