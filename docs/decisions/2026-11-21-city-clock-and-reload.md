# City clock, event replay and reload, 21 November 2026

Recorded on 21 November 2026 from code comments that the comment cleanup removed (Peter, 20 November 2026). The other decisions in force are in the [register](2026-11-10-city-direction.md).

## Decision

- An event whose citizen version changed during its run is discarded and the original event is requeued ([CitySimulator](../../Core/CitySimulator.cs), `RunEvent`). Each replay gets the same finite frame admission as its first attempt, so restarts never grant an unlimited call.
- The city clock is held while events replay. Later events can change route forecasts, employment and meals, and reordering them would let a frame cadence change city outcomes.
- Until the native clock patch is bound to the current save's snapshot, no native minute passes ([NativeCityClock](../../Runtime/NativeCityClock.cs)). This covers the startup guard and a save change that `PopulationRuntime` has not handled yet.
- Transient preparation (food awareness, rival outflow indexes) restarts on reload. Interrupted-journey tests let those units finish and interrupt every published shift, citizen, posting or route transition instead ([AttendanceWageTests](../../tests/AttendanceWageTests.cs), [RivalOutflowTests](../../tests/RivalOutflowTests.cs)).
