# E3 performance wins

| Slice | Change | Before | After |
|---|---|---|---|
| E3 donut (31 Oct, offline) | Remove redundant identity HashSet; ordinal dictionary keys plus matching IDs prove uniqueness; unknown comparers refuse. Both variants use authoring-tag field reads, 1,001 individual reads/native doubles. | 2,000 entries: warm p50/p95/max 0.1253/0.2493/2.5530 ms; 203,288 B/call. Two entries 376 B/call. | 2,000: 0.0853/0.1822/1.5725 ms; 49,104 B/call. Two entries 200 B/call. Zero E3 samples >=30 ms. One cold sample 3.1500 ms/1,144 B to 1.6876 ms/968 B (includes JIT/metadata; no causal cold-time claim). [Method and native limits](../checks/e3-donut.md#e3-donut-feasibility---31-october-2026-offline-only). No per-frame work. |
