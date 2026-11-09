# F4b perf: complete union probe, 9 November 2026

Offline measurements on the PC (Windows 11, .NET 10.0.13 Release). These are not in-game numbers. The probe adds no Runtime code, so Persistent City's in-game mod-work p95, ≥ 30 ms spikes and whole-frame timing are unchanged by this slice. The probe's native share is measured in the batch (rig check 1 of [the F4b checks](../checks/f4b.md), plan G2). Whole-frame timing and #128 stay separate; no parked hotspot is touched.

Reproduce: `dotnet run -c Release --project tools/perf-union-probe/logic-check/LogicCheck.csproj -- --perf`.

## Probe overhead per scope call (stub loop)

The stub runs the same steps as `ScopePatch.Before`/`After` around a fixed 40-iteration work function: main-thread check, frame check, scope-index lookup, two `Stopwatch.GetTimestamp` calls and the `UnionClock` enter/exit. 2,000,000 calls per run, 7 runs, median of (probed − bare) per call, keys are 80 real `MethodInfo` objects. Harmony's dispatch into the prefix and finalizer, Unity's `Time.frameCount` icall and Mono's JIT are not in it.

| Lookup | Run 1 median (min–max) | Run 2 median (min–max) | Lookup alone |
| --- | ---: | ---: | ---: |
| before: default-comparer `Dictionary<MethodBase,int>` | 47.8 ns (46.2–96.5) | 46.4 ns (46.0–96.5) | 6.6 / 6.9 ns |
| after: reference-equality cache in front, default table on a miss | 40.3 ns (39.8–41.0) | 41.1 ns (38.9–42.8) | 4.9 / 5.4 ns |

**Cheap win taken:** the reference-equality cache, about 6–8 ns (15 %) less per scope call. It is low risk: a miss falls back to the default-comparer table and caches that instance, so a `MethodBase` object that Mono hands out as a different instance for the same method still binds. Applied to the scope and component lookups in `Probe.cs`.

At 40 ns per scope call the probe adds about 2 µs per frame at 50 scope calls, 8 µs at 200 and 40 µs at 1,000 (the `TrafficManager.FixedUpdate` hooks run once per fixed step and per car callback, so busy frames are at the high end). That is inside the union, as the limits state.

## `observerMs` share

`observerMs` holds only the hold-tick and per-call bookkeeping (the counter reads around `AccountingPrefix`, `HoldTracker.Observe`, the tick-buffer write, and the `AdvancePopulation` GC and seam reads). The stub measured one hold tick plus one advance-call check at **5.6–5.9 ns per frame** (median of 7 runs, two processes).

| Scope calls per frame | Probe cost in the stub | `observerMs` share of it |
| ---: | ---: | ---: |
| 50 | 2.02–2.06 µs | 0.3 % |
| 200 | 8.06–8.23 µs | 0.1 % |
| 1,000 | 40.3–41.2 µs | < 0.1 % |

So `observerMs` reports a small part of the probe's own cost; most of it is the scope bookkeeping, which cannot be measured separately in game and is not subtracted. The stub's delegate reads are cheaper than the game's (the probe's `AdvancePopulation` read also goes through dynamic-method getters and `GC.CollectionCount`), so the in-game `observer_ms` will be higher than this; G2 records it.

## Per-frame boundary cost

The frame-start entry (close the prior frame row, copy 11 component values, read the native and logical minute) is reported per frame as `boundary_ms` and measured in game only; it runs outside every PC scope and is not mod-work.

## Tool cost

- `check_scopes.py` on the base (266 source files): **20.1 s before, 3.3 s after** a cheap win. The method finder sliced and stripped the whole file text before every identifier followed by `(` (quadratic); it now steps back over whitespace from the match. Same findings and the same 24 passing tests. The Python suite, which runs the check on about 15 edited source copies, takes about 60 s.
- `build_probe.py`: one Roslyn compile of three files, a few seconds; it runs `check_scopes.py` first.
- `logic-check --perf`: about 10 s.

## Standing gate

Unchanged by this slice: no Runtime, Core or Persistence edit, so product mod-work p95 < 5 ms and the ≥ 30 ms flags are as before. The probe's own cost is part of every measured union in the batch and is reported there, not subtracted.
