# B2 performance wins

| Slice | Change | Before | After |
|---|---|---|---|
| B2 | Pre-size the transient unique-consumer set at the known population size | Growing-set comparison: 73,152 /322,638 bytes at 1,914 /5,000 consumers | 38,736 /116,932 bytes; same distinct IDs/counts. Offline .NET 10; raw batch measurements in [B2 evidence](../reviews/2026-10-31-b2-measurements.json). |
| B2 | Skip content refresh while a selected inspector is hidden by native UI/interiors | One scheduled popup capture/text build per second despite invisible popup | Zero captures/text builds while invisible; selection and pool-generation checks retained, next visible refresh catches up. Call-site contract test verifies gate; no native timing claim. |
| B2 | Reuse the projection's owned-shop eligibility for the regulars line | Two new native generation validations per shop/refresh (projection construction, then regulars-line guard) | One validation per shop/refresh; the snapshot gates only the regulars line. Existing shop status/history/accounts stay visible even without regular evidence (Opus-r1-1). Headless call-site/filter tests and native ownership-read regression; no timing claim. |
| B2 | Reuse diagnostic effective affinity for the entry's numeric fact and regular flag | Two lazy-affinity evaluations per retained own-shop entry/dump | One evaluation per entry; at most four repeated evaluations removed per explicit dump; same flag/affinity tests, no timing claim. |
| B2 | Cached shop-count lookup returns the first dictionary result | Two count-dictionary probes per warmed lookup | One probe; warmed lookup allocation remains zero (B2 measurements). |
