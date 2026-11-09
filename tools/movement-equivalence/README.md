# Movement-equivalence harness (F2a)

City audit F2 (#89), [plan](../../docs/plans/f2-movement.md) section 4, row F2a. A detached Core harness: the real simulator, traffic kernel, food model and save codec, driven frame by frame with a presentation driver double that calls today's lease API as `Runtime/PhysicalPopulation.cs` does. No Unity, no game.

```
dotnet run --project tools/movement-equivalence -c Release -- [--residents 957] [--days 2] [--arms <substring,...>] [--out <file.json>] [--dump <directory>] [--no-self-checks]
```

It runs the self-checks (exit code 1 if one fails), then each selected arm and its bodies-off reference, prints one line per arm and writes the JSON (`persistentcity-f2a-movement-1`). `--dump` writes each arm's journey records and ordered sales as text for diffing. A body arm that differs from bodies-off is today's recorded result, not a failure.

`baseline.json` is the 957-resident, two-day output recorded in [docs/checks/f2a.md](../../docs/checks/f2a.md). The unit tests in `tests/MovementEquivalenceTests.cs` link these sources (all but `Program.cs`).

F2e adds the E2 rows to the self-check section (`HoldReplay.Check`): a player-contact run (`MovementOptions.PlayerContact`) with replay checkpoints (`MovementOptions.Checkpoints`), bodies-off replays of its parsed Player.log hold stream from each checkpoint, and the segment refusals. See [docs/checks/f2e.md](../../docs/checks/f2e.md).
