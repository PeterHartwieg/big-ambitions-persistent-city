# Reuse save contract metadata — 28 October 2026

A bounded, byte-preserving part of architecture finding 5. `SaveCodec` now reuses its private `StateContractResolver` metadata cache. Its property list depends only on save DTO types: writable properties, ordinal property order and required/nullability rules. Json.NET's resolver cache supports concurrent resolution. Each call still gets fresh serializer settings and converter instances, and application-wide `JsonConvert` defaults still apply.

Full state validation, traffic reconstruction/progress checks, size limits, strict shape checks, unsupported/future preservation and all serialization passes remain. No schema, field, validation policy or gameplay output changes. This does **not** solve the main-thread O(city) save cost or remove the synchronous RLO read.

## Offline evidence

`tools/save-contract-regression/compare.py` builds the same 120-resident fixture against baseline `63248e4` and current Core/Persistence. The 108,227-character payload has the same SHA-256 (`8AFB1D599D7E2503487799A142FD2FB0B76DEAF7AC6BE0BD868B499C16B1E173`); missing/future/malformed/unknown-field refusals match, including error messages. Warmed .NET 10 thread allocation for 20 writes falls from 36,369,304 to 21,794,064 bytes; 20 reads fall from 78,218,592 to 61,925,608 bytes. These include the whole operation, not just metadata.

A new test performs 32 concurrent independent read/write/refusal sequences, requiring exact bytes and continued rejection of invalid event times after warm-up. The full suite passes 1,267 tests. Existing six-day core-identity differential preserves all seven full city-save hashes including interrupted traffic/reload, as well as event and routing outputs.

No Unity/Mono allocation or frame measurement was taken. No game launched; offline/source/build evidence only. Save-time RLO removal, weaker validation and format changes remain outside this PR and await Peter's design decisions where they alter the contract.
