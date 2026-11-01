# A2 performance wins

| Slice | Change | Before | After |
|---|---|---|---|
| A2 | Save validation builds error text only on failure (`Text`/`Bounded`/`Known`/duplicate/unknown-field checks); runs on every save and load | Validation alloc per save **8.39 MiB** (957 residents with journeys, offline .NET 10) | **1.31 MiB** |
| A2 | Read shape check: one reusable duplicate-field set per JSON depth, bit flags per position, integer range from the token value; positions streamed instead of one `JObject` each | Read alloc **83.3 MiB**, read median **119.7 ms**; write alloc **26.1 MiB** (same payload) | **38.8 MiB**, **89.2 ms**; **19.1 MiB** |
| A2 r2 | Schema-17 outcome records as one compact string each (no field names) | 4,038,976 chars at 957 residents end of day; 50,000 × factor 3 above the input cap | 3,470,518 chars (−14.1%); 50,000 × factor 3 at 89.7% of the cap |
| A2 r2 | Unboxed range checks for schema-17 enums in save validation | Validation 1.83 MiB alloc, 4.3 ms per save (957 residents, end of day) | 1.04 MiB, 2.7 ms |
| A2 r1 | Schema-17 outcomes: whole-minute integer times, shop/product kept only for purchases | Bought record 210 chars, skipped 196 (full-precision minutes, shop/product on skips) | 182 and 122 chars (−13% / −38% per record) |
| A2 | Schema-17 traits stored as one 0–4 digit per trait (`{"Bands":"…","Version":1}`) instead of ten named integer fields | 208 chars per trait record (+333,036 chars at 957 residents × 2 consumers) | 34 chars per record (174 fewer; 1.74 M chars saved at 5,000, 17.4 M at 50,000) |
