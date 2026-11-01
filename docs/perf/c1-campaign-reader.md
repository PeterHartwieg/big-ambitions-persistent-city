# C1 performance wins

| Slice | Change | Before | After |
|---|---|---|---|
| C1 | Cache verified enum/list metadata per bounded shape; replace per-business duplicate hash sets with at most 8 x 8 comparisons (offline .NET, 1,024 businesses x six campaigns, 301 reads) | Warm p50/p95 3.316/5.196 ms; 3,637,080 allocated bytes/read | Warm p50/p95 0.980/2.380 ms; 1,637,960 bytes/read (55% fewer); one-business continuation p50 0.0029 to 0.0008 ms. Cold/read-tail and native limits: [C1 checks](../checks/c1-campaign-reader.md#c1-native-campaign-reader---31-october-2026-offline-only). |
