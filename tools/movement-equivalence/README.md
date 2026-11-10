# Movement-equivalence harness (F2a)

City audit F2 (#89), [plan](../../docs/plans/f2-movement.md) section 4, row F2a. A detached Core harness: the real simulator, traffic kernel, food model and save codec, driven frame by frame with a presentation driver double that calls today's lease API as `Runtime/PhysicalPopulation.cs` does. No Unity, no game.

```
dotnet run --project tools/movement-equivalence -c Release -- [--residents 957] [--days 2] [--arms <substring,...>] [--out <file.json>] [--dump <directory>] [--no-self-checks]
```

It runs the self-checks, then each selected arm and its bodies-off reference, prints one line per arm and the time per stage, and writes the JSON (`persistentcity-f2a-movement-1`). `--dump` writes each arm's journey records and ordered sales as text for diffing. Since F2g the exit code is 1 if a self-check or E2 row fails or an arm DIFFERS from its reference.

`baseline.json` is the 957-resident, two-day output. F2a recorded the red pre-F2 result ([docs/checks/f2a.md](../../docs/checks/f2a.md)); F2g replaced it with the green result ([docs/checks/f2g.md](../../docs/checks/f2g.md)), and `RecordedBaselineIsTheGreenMatrix` asserts it. The unit tests in `tests/MovementEquivalenceTests.cs` link these sources (all but `Program.cs`).

F2e adds the E2 rows to the self-check section (`HoldReplay.Check`): a player-contact run (`MovementOptions.PlayerContact`) with replay checkpoints (`MovementOptions.Checkpoints`), bodies-off replays of its parsed Player.log hold stream from each checkpoint, and the segment refusals. See [docs/checks/f2e.md](../../docs/checks/f2e.md).

F5i adds the `visitors` family: the same city plus K−1 walking visitors per resident from three portals near the shop (`MovementOptions.Visitors`, `RepresentationFactor`), with bodies-off references `visitors/plain-60`, `plain-30` and `plain-hidden-travel`. Visitors receive bodies through the active-journey index, and each visitor arm must equal its bodies-off run on every projection, including the `visitors` projection (visitor journeys, sales and wallets). See [docs/checks/f5i.md](../../docs/checks/f5i.md).

F2g adds the rest of the plan matrix with visitor bodies on (`visitors/plain-120`, `focus-none`, `caps-0`, `caps-24`, `failures-10pct`, `lag-slow-head-car`, `cadence-120fps`), a `combined` family that varies focus path, caps, failures, cadence and lag together with a reload or hidden travel, and the E2 arm with visitor bodies on (`HoldReplay.Check(..., visitors: true)`). A reload arm that never reloads now fails loudly. The `pairwise` family is a greedy covering array over the full factor product (residents or visitors × focus × caps × failures × cadence × lag × none/reload/hidden/reload+hidden): every valid pair of factor levels runs in at least one of its 24 arms (`MovementArms.Pairwise`). See [docs/checks/f2g.md](../../docs/checks/f2g.md).
