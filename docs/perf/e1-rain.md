# E1 performance wins

| Slice | Change | Before | After |
|---|---|---|---|
| E1 | Lazy verified-rain frame cache, called only by new boundaries | Uncached E1 control: 50,000 reads/sweep; 6,000,000 over 120 capture sweeps | 1 read/sweep; 120 reads, cached failures/unknown too. Resident capture 40 bytes/boundary, held food fields 0 bytes; no new per-resident per-frame rain read |
| E1 | Incremental admission-key hash with the original finalizer; skip hash for neutral weather | Concatenation control: 127.84 bytes/rain admission, median 43.07 ns at 50k | 0 bytes, 33.63 ns; exact original-key equality asserted across all measured keys and independent seeded/Unicode tests. Same draw domain |
| E1 | Direct schema-20 consumer-prefix shape check without substring | 120 bytes/check; median 326.82 ns | 0 bytes/check; 301.58 ns. Frozen 18/19 memory wire unchanged; [raw E1 measurements](../reviews/2026-11-01-e1-measurements.json) |

Offline policy/capture microbenchmarks only; native mod-work and whole-frame costs remain pending. The four parked hotspots stay parked.
