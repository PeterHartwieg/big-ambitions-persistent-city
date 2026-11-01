# B0b performance wins

| Slice | Change | Before | After |
|---|---|---|---|
| B0b | Nonmaterializing streaming shape precheck for the new held tuning image, before full graph decode | Materializing prototype: 136 bytes/check, median 1,375.69 ns | 0 bytes/check, 187.79 ns (three fresh processes, 300k calls/run; offline .NET 10). Continuation allocation unchanged; [raw B0b measurements](../reviews/2026-10-31-b0b-measurements.json) |
