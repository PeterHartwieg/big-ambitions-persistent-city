# City audit F7p: compact-format prototype checks (6 November 2026, offline)

Refs #94. Plan: [F7 section 3.6 and section 4, row F7p](../plans/f7-save-snapshots.md#36-prototype-plan-f7p). Base `e9995e5` (writer **25**). Matrix: [docs/perf/f7p.md](../perf/f7p.md). Decision: [F7d compact wire format](../decisions/2026-11-06-f7d-compact-format.md). This slice is offline and tools only. It is not verified by rig, and it has no in-game behavior to verify.

## Scope

- **Implemented:** `tools/compact-format/`. It contains the exact wire tree (the F7c snapshot stand-in), the four section 3.6 candidates (A writer-25 JSON, B columnar compact JSON, C = B + shared route table + road-tag ranges, D = C + deflate + base64), the per-process matrix driver `measure.py`, the table generator `report.py`, and a `file:<path>` input for an extracted writer-25 payload. `tests/CompactFormatPrototypeTests.cs` links the codec sources. Section 3.6 suggested `tools/save-snapshot-prototype/`; the task named `tools/compact-format/`, and the content is the same.
- **Unchanged:** nothing under `Core/`, `Persistence/` or `Runtime/` changes. Writer 25, the shipped reader and every schema stay as they are, and no side key is added. The prototype is not referenced by the mod build. If F5c's schema lands first, F7d migrates from that writer; F7p's measurements stay against writer 25.
- **Peter's section 9 questions:** Q2 and Q3 keep their defaults (14 days of shop history, today's visit limit), and nothing is pruned. The values are the named constants `CompactFormatProgram.ShopHistoryDays` and `RecentVisitsLimit`. Q1 is not touched.

## Offline checks run (6 November 2026)

Every command ran in the foreground after the shared other-dotnet wait. No background process was left running.

| Check | Result |
| --- | --- |
| `python tools/build.py --game-managed "...\Big Ambitions_Data\Managed"` | passed |
| `dotnet test tests/PersistentCity.Tests.csproj` | **3,179 / 3,179 passed** (11 new F7p cases) |
| `python tools/compact-format/measure.py 7` (this slice's check), all 13 F7a fixtures × A/B/C/D | 52/52 rows. Writer-25 source SHA-256 equals F7a's retained hash for every fixture. The strict reader accepts each source and rewrites it identically. **Zero round-trip differences.** Decoded tree → writer-25 bytes identical, and decode → re-encode byte-identical, for every candidate on every fixture |

New unit cases: Newtonsoft-identical escaping over every UTF-16 code unit plus a surrogate pair; every candidate round-trips representative 957×2 exactly (tree equality, writer-25 bytes, re-encode identity, strict reader accepts the rewrite); edge shapes (number spellings `-0`, `-0.0`, `1E-05`, beyond-`long` integers, nulls mixed with values, varying shapes and tuple lengths, control and non-BMP strings, shared journeys and vehicle routes as one reference, chained road ranges); canonical road-tag splitting only; varint round-trip over the whole `long` range with overlong, surplus and bad-symbol refusal; five tampered-payload refusals (format, version, unknown field, shape index, column tag); truncated and foreign payloads refused.

**Review round 1 (6 November 2026).** Fixes for reviewer findings:
- The matrix now also measures capture (a detached-copy proxy), synchronize and validate (F7a recorder), materialization (upper bound: strict writer-25 read), activation validate and simulator construction. Each phase is measured once per fixture in candidate A's process and composed into pause and load per candidate.
- The full 52-row matrix was rerun at `6512136`. It has zero differences, and bytes and hashes are identical to the first run.
- `measure.py` rewrites the matrix file on a full run and appends only for an explicit fixture list.
- The decision record now states that no candidate met every target, and gives the same-harness C/A encode ratio.

Following the 6 November owner rule, the other `tools/*-regression` suites, feasibility source checks and validation matrices were not run; they are not required for this slice.

## Result summary (synthetic .NET 10)

- **Bytes:** C is 29.74 % of writer 25 at representative 957×2 (694,001 chars; target ≤ 35 % = 816,686) and 20.93 % at stress 50000×2 (33,972,886 chars; target ≤ 64 Mi). Both targets are met.
- **Pause, allocation and load (review round 1 matrix, with capture/validate/activation phases):** the composed C pause is 45.9 ms p50 at representative 957×2 (target ≤ 40 ms p95) and 1,796.9 ms at 50000×2 (target ≤ 600 ms). Allocation is 7.8–11.5 × payload UTF-16 bytes (target ≤ 2.5 ×). Stress load is 3,514 ms even without the upper-bound materialization (target 2,500 ms). Typical load is 186.1 ms p50 (target 400 ms). In the same harness C encodes at 1.19–7.09 × A. **Under section 3.6's pass criteria no candidate met every target.** C is selected on bytes and identity, conditional on F7d meeting the ceilings (see the decision record).
- **Flags:** 648 of 728 encode/decode samples are ≥ 30 ms (format-independent phase flags are in the perf record). These are offline save/load samples, not mod-work. F7p adds no runtime code and no per-frame work, so mod-work p95 < 5 ms and whole-frame metrics are not affected and stay tracked separately. The four parked hotspots stay parked.
- **Decision:** C for F7d. D is not selected.

## In-game checks for the next batched rig

F7p ships no runtime code, so no section 7 check verifies F7p behavior in game, and none of them gates this slice. Two things this slice does make testable are the decision's measured limits: no synthetic fixture holds traffic definitions, journeys at capture or road legs, and the native per-character save cost behind the D trade-off has not been measured. The checks below close those gaps on real checkpoints. All three belong to the separately requested **save-specific campaign** (F7 checks 2 and 6), with explicit saves and never mixed with gameplay timing windows. Each run applies the AGENTS.md autosave rules: record the original preference, verify autosave off before each measured window including after reload, keep manual saves outside windows, and restore the preference in cleanup. They are executed by the headless agent rig ([PeterHartwieg/big-ambitions-agent-rig](https://github.com/PeterHartwieg/big-ambitions-agent-rig), read-only here). Nothing is deferred to Peter, a playtest or a by-hand check. The checks are batched and do not gate merge.

1. **Prototype round-trip and size on a native payload (save-specific; F7 check 2 checkpoint).** On the 957-resident representative checkpoint `pc-representative-957@1` and on one checkpoint with active car traffic (traffic definitions, vehicles in transit and road-tag legs present), the rig makes one explicit `Default` save in a disposable copy. It extracts `persistentcity:state_v1` into a temporary file outside every repository, then runs `CompactFormat file:<path> <A|B|C|D> 7` once per candidate. It asserts that the strict reader accepts the payload as writer 25 and that every candidate reports zero differences, `writer25Identical=true` and `reencodeIdentical=true`. It records characters, UTF-8 and UTF-16 bytes, gzip(UTF-16), table and route counts, road-tag count, encode/decode p50/p95/max and per-family characters, and flags every sample ≥ 30 ms as save latency. It deletes the extracted payload in cleanup and keeps only metrics. — rig-automated (existing capability: `rig flow run`, FT `save`/`load`; pending rig capability: **RIG-PCSAVE** payload extraction, plus an offline-tool step that runs a repository command against the extracted file and stores its JSON as flow evidence).
2. **Same check at scale (save-specific; F7 check 6 checkpoints).** The rig repeats check 1 on `city-50k@1` and on a 10,000-consumer checkpoint. It also asserts that C's records stay under the 4,000,000 cap with table entries charged, and it records the process peak working set of each candidate run. — rig-automated (existing capability: fixture library `city-50k@1` provenance, `rig flow run`; pending rig capability: **RIG-PCSAVE** with the offline-tool step, plus production of `city-50k@1`).
3. **Native save cost per PC character (save-specific; F7 checks 2 and 6, writer 25).** On the checkpoints of checks 1 and 2, the rig records native `SerializeSaveGame` and compression-thread durations, together with PC payload characters and the UTF-16 share of the decompressed native save, for `Default` saves. It reports each checkpoint's ms per million PC characters, which is the input to the decision record's condition for reopening D. It asserts each save completed and flags every phase ≥ 30 ms. No assertion compares candidates natively, because none is built. — rig-automated (existing capability: `rig flow run`, FT `save`/`console`; pending rig capability: **RIG-SAVEPROF**).

An in-game behavior release still needs a `rig_test` session (Codex Sol, high) with before → red / after → green proof on its PR. This tools-only slice changes no in-game behavior and neither performs nor claims that proof.
