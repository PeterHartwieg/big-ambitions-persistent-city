# City audit F7b performance: save-epoch callback overhead, 4 November 2026, offline

## Roadmap perf-log entry (moved 6 Nov 2026)

- City-audit F7b — [Save-epoch callback overhead](f7b.md): no win taken (save/detach path only); publish 0.0001 -> 0.0008 ms p95, 0 -> 808 B per save, load check 0.0012 ms p95; writer-25 SHA-256 unchanged; no frame work beyond a unit counter; offline components only.

Refs #94. [Checks and rig list](../checks/f7b.md). Base `dc59049`, writer **25** unchanged. These are offline .NET 10 component timings on the development PC (Windows 11). They are **not** native save pause, mod-work or whole-frame evidence.

## Method

Run `dotnet run --project tools/save-epoch-regression/SaveEpochRegression.csproj --configuration Release`. The tool builds a minimal 957-resident writer-25 fixture (926,514 characters: identities and homes only, not F7a's representative fixture). It publishes it once through `SaveEpochCallback` with the real `SaveSession`, and requires byte and SHA-256 equality with `SaveCodec.Write`. Then it times 2,000 warm-up calls plus 20,000 measured calls for each arm:

- **Before:** the base callback's publication step only (assigning the encoded string to `modData`).
- **After:** that same assignment inside the F7b callback (ticket, binding and publication checks, marker formatting and write).
- **Load check:** `CheckForStart` plus `DescribesState` on a success marker.

Encoding is outside both publication arms because F7b does not change it. Encoding is shown separately for scale.

## Results

| Arm | p50 / p95 / max ms | Allocated B/call | ≥30 ms |
| --- | ---: | ---: | ---: |
| Before (publish only) | 0.0000 / 0.0001 / 0.0004 | 0 | 0 |
| After (F7b callback incl. publish and marker) | 0.0005 / 0.0008 / 0.3734 | 808 | 0 |
| Load-time marker check | 0.0007 / 0.0012 / 0.0900 | 984 | 0 |

- The marker is 103 characters. The writer-25 encode of the same fixture averages **45.6091 ms** (5 runs), so F7b adds under 0.01 % to the PC save callback on this fixture. The single 0.3734 ms max is an unattributed host outlier, not a ≥30 ms flag.
- Writer-25 SHA-256 `B377CB44780E947DFCFAA3E6C0DBB8452362EC4111E2486F6B64B5741CF0F5BD`, identical through the epoch path and the direct codec.
- **Ordinary play.** F7b adds one integer increment, one decrement and a `try/finally` around `Update` (no allocation, reflection, polling or scan). Save and detach paths run only on native save, failure or unload. The F3 `StepBudget` fixture and the four parked hotspots are untouched. No ordinary-play timing was rerun, because nothing in a measured frame path changed beyond that counter.

## Perf wins

None taken. The changed code is on the save/detach path and is already sub-microsecond at p50. Caching the per-save delegates would save a few dozen bytes per save and is not measurable against a 45 ms encode. Save serialization stays parked for F7c/F7d.

## Limits

These are single-host offline component numbers on a minimal fixture. Native callback cost, the whole native `Save` pause and save sizes on real checkpoints belong to the save-specific rig campaign (plan section 7, checks 1 and 2) and to F7a's representative baseline.
