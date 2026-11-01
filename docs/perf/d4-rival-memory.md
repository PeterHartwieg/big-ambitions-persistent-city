# D4 rival memory: offline performance

1 November 2026. Reproduce with the foreground Release
`tools/rival-memory-regression/RivalMemoryRegression.csproj` after the shared
other-dotnet guard. Each arm uses 2,000 warmups and 10,000 individual timed calls.
No native frame, route, UI or save operation is inside these timing windows.

The taken win replaces D2's temporary Place plus general opening interval/cache
construction at rival completion with bounded membership in the advertised
same-day windows. It reuses the existing observation, makes no extra read and
allocates nothing. Arrival retains the general next-close calculation. Tests
compare both algorithms through week boundaries, gaps, overlaps, full-day hours
and exact open/close endpoints. General Place overnight semantics are unchanged;
the advertised rival contract has same-day [from,to) windows.

A second taken win counts the signature's delimiters before splitting it for
inspector words. Player authority has extra generation fields and returns false
without allocating eight split strings; six-field rival classification remains
unchanged. This is on-demand UI work only, not a new frame participant. The
benchmark compares the original split/length path against the production check
for an actual-format player generation.

Tested source `eee4dff`; [raw metrics and foreground receipts](../reviews/2026-11-01-d4-rival-memory-results.json).

| Warmed operation | p50 / p95 / max ms | B/call | >=30 ms samples |
| --- | ---: | ---: | ---: |
| Original completion-hours check | 0.0015 / 0.0030 / 2.6968 | 1,464 | 0 |
| Direct advertised membership | 0.0001 / 0.0001 / 0.0005 | 0 | 0 |
| Original own-generation split/rejection | 0.0001 / 0.0002 / 2.1464 | 352 | 0 |
| Delimiter precheck/rejection | 0.0001 / 0.0002 / 0.0058 | 0 | 0 |
| Empty-memory policy lookup | 0.0001 / 0.0001 / 0.0033 | 0 | 0 |
| Active mixed four-entry lookup, including generation index | 0.0001 / 0.0002 / 0.0070 | 0 | 0 |
| Active mixed four-entry repeat update | 0.0002 / 0.0002 / 0.0058 | 0 | 0 |

The original completion check is reproduced from D2's temporary Place/union
path; the original word check reproduces the preceding D4 split/length path for
an actual-format player identity. Allocation comparisons are deterministic in
these warmed arms; no native or end-to-end timing speedup is claimed. These tiny
single-call percentiles are close to timer resolution and subject to JIT/load.

The retained format/control fixture is **2,422 UTF-8 bytes**, **7 streaming / 7
semantic records** with either neutral/active contribution or four all-own entries
using equal-width generation IDs. Its mixed form has two own/two rival entries.
The update arm retains **four entries/four references** through 12,000 updates,
using the unchanged **48-character** saturated memory string; no source flag,
collection, replay-ID history, scalar slot or schema is added. First insertion,
reference registration and setup allocations are excluded, as they are bounded
existing B1 state. Format equality does not mean that filling an empty memory or
recording a new affinity has no saved-content cost: B1 already allocates that.
 These microbenchmarks cannot establish native mod-work p95 <5 ms,
nor attribute whole-frame stalls. All >=30 ms samples are explicitly counted.
Physical population, closure refresh, optional-stop refresh and save serialization
remain parked.

Broader final acceptance flags (not D4 micro-samples): 20k whole-catalogue builds
have 1 dense/5 sparse operations >=30 ms, max 33.0878/56.8085 ms. E4 standalone
four/eight-product history serialization has 21/21 such operations per arm, max
204.2245/148.8218 ms. These whole operations are separate from native mod-work
and whole-frame samples; the raw manifest preserves the counters and units.
