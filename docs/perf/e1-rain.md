# E1 performance wins

| Slice | Change | Before | After |
|---|---|---|---|
| E1 | Lazy verified-rain frame cache, called only by new boundaries | Uncached E1 control: 50,000 reads/sweep; 6,000,000 over 120 capture sweeps | 1 read/sweep; 120 reads, cached failures/unknown too. Resident capture 40 bytes/boundary, held food fields 0 bytes; no new per-resident per-frame rain read |
| E1 | Incremental admission-key hash with the original finalizer; skip hash for neutral weather | Concatenation control: 127.84 bytes/rain admission, median 43.07 ns at 50k | 0 bytes, 33.63 ns; exact original-key equality asserted across all measured keys and independent seeded/Unicode tests. Same draw domain |
| E1 | Direct schema-20 consumer-prefix shape check without substring | 120 bytes/check; median 326.82 ns | 0 bytes/check; 301.58 ns. Frozen 18/19 memory wire unchanged; [raw E1 measurements](../reviews/2026-11-01-e1-measurements.json) |

Offline policy/capture microbenchmarks only; native mod-work and whole-frame costs remain pending. The four parked hotspots stay parked.

## Base sync re-measure, 1 November 2026

Unchanged E1 kernels, three fresh Release processes: 0 >=30 ms across 5,100
measured sweeps, maximum 3.6095 ms; 40 B/resident capture, 0 B/food capture,
0 B/admission and 0 B/direct precheck. Frame cache remains one read per capture
sweep/frame. [Raw measurements, aggregate >=30 ms flags and capacity](../reviews/2026-11-01-e1-sync-base-measurements.json).
This is synthetic offline evidence; native combined mod-work and whole-frame
results remain pending. No new optimization or parked-hotspot rewrite was made by the integration.

## Latest base re-measure, 1 November 2026

Refreshed through `51ce709`; no new performance-affecting integration change.
Three fresh Release processes: 0 >=30 ms across 5,100
weather/control sweeps, maximum 5.6037 ms including the old concatenation control.
Implemented kernels: 3,900 sweeps, maximum 3.8871 ms, largest process/case p95
3.6779 ms. Byte/record budgets and capture/read bounds
remain unchanged. [Latest measurements and all aggregate/batch >=30 ms flags](../reviews/2026-11-01-e1-sync-latest-measurements.json).
Native mod-work and whole-frame evidence remain pending under the combined rig rule.
