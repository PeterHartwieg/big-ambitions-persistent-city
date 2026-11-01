# E2c performance wins

| Slice | Change | Before | After |
|---|---|---|---|
| E2c, 31 Oct (offline) | Compare wildcard prefixes in place instead of allocating a temporary substring for every assembly/rule comparison; all detection remains once per load. Same adapter, 501 warm scans, 9 loaded .NET assemblies. | 6,296 B/scan; p50/p95/max 0.0084/0.0109/0.0159 ms | 5,936 B/scan; 0.0085/0.0099/0.0182 ms; zero >=30 ms samples. Allocation win 360 B/scan; no claimed timing improvement. Cached reads: 0 B for 1,000,000 reads; no new per-frame call. Final gate repeat p50/p95/max 0.0089/0.0144/0.8122 ms, zero >=30 ms; [method and native limits](../checks/e2c-interior-guard.md#e2c-third-party-interior-guard---31-october-2026-offline-only). |
