# C4 performance wins

| Slice | Change | Before | After |
|---|---|---|---|
| C4 | Validate frozen A2 trait bands directly instead of allocating a temporary trait object per input row | Prototype: 32 B/row, median 7.160 ns | 0 B/row, 3.946 ns; ten interleaved 100k-call samples, valid/invalid parity asserted. Offline input setup only; [measurements](../reviews/2026-10-31-c4-measurements.json) |
