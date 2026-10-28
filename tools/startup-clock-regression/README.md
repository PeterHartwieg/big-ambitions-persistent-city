# Startup clock regression

Compiles the actual NativeTravelPace startup logic against clock/UI doubles and the real Core/Persistence projects. It verifies that reload preparation preserves saved future routines, employment checks, commute preparation, parking and an in-flight route before the configured city policy becomes active. A genuine subsequent rate change still retimes remaining travel and wakes safe home routines once. Fresh population uses the configured factor despite an observed pre-policy native rate; unsupported saved timing is refused unchanged.

Run `dotnet run --project tools/startup-clock-regression/StartupClockRegression.csproj`. Optionally append `-- /absolute/path/to/captured-population.json` to validate that a captured supported checkpoint remains canonically unchanged through startup preparation. Captured game data stays outside Git.

This is source regression evidence, not a native save/reload run. The runtime call site starts NativeCityClock and resets the observer before its existing Update rate comparison and population advancement.
