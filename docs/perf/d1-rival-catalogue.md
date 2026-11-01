# D1 performance wins

| Slice | Change | Before | After |
|---|---|---|---|
| D1 (31 Oct, offline) | Reuse address-tuple metadata index; no escaped address strings on warm refresh. Zero work when not called. | Warm refresh allocation 227,200 / 4,629,632 B at 1,024 / 20,000 places (0.2286 / 1.9294 ms). | 32 / 32 B (0.1061 / 1.4670 ms); 16-read units 0 B. Retention trade-off: 422,208 / 8,500,600 to 491,848 / 9,838,136 B. [Method, cold/detail costs and limits](../reviews/2026-10-31-d1-rival-reader.md#offline-validation-and-performance). No native timing claim. |
