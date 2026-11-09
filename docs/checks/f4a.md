Status: implemented (offline); not verified by rig

# F4a: perf gate contract and analyzer, 9 November 2026

Refs [#91](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/91). Branch `codex/pc-f4a-gate-contract` from `origin/codex/persistent-city` at `b8feb06` (F2e #172, writer **35**). Scope: the F4a row of [the F4 plan](../plans/f4-perf-gate.md#4-slice-breakdown), with Decisions 3, 4, 7 and 8 as its spec. [Perf](../perf/f4a.md).

F4a adds tools and tests only. No Core, Persistence or Runtime file changes; writer 35 and the pinned current-writer golden hash are untouched. No schema change and no migration. All results below are offline; none is game evidence.

## What is implemented

All in `tools/perf-gate/`:

- **`contract-v1.json`**: Decision 4 frozen as `perf-gate/v1`. Rows M1–M4, P1–P4, S1–S5, L1–L4 and R1 with their thresholds, plus the rules the verdict uses: nearest-rank percentiles, pooling of raw frames only, 300 s clean minimum, first window per process discarded, P4 matching tolerances (same checkpoint minute, ±1 game minute span, reference pace within 2 %), density thresholds (36 pedestrians, 12 cars), the required receipts and the reference's `n/a` receipts, the required header fields and the 0.1 ms tolerance for matching a summary maximum to a probe frame. The analyzer reads every threshold from this file. Changing a value needs a new contract version and a decision record.
- **`analyze.py`**: reads one or more `perf-gate-export/v1` files and builds a `perf-gate-report/v1`. CLI: `python tools/perf-gate/analyze.py <export.json>... --json-out <r.json> --md-out <r.md>`. Exit 2 with `REFUSED: <reason>` and nothing written when the report is refused.
- **`report.py`**: JSON plus a generated Markdown summary: header, row verdicts, stress/report-only arms, per-window table, invalid windows with reasons, P4 pairs, pooled figures, M2 spike list, stall table, limits.
- **`summary_lines.py`**: parser for the 60-second Player.log line as emitted at `Runtime/PersistentCityMod.cs:362-412` at this base, and the fix-128 attachment rule.
- **`fixtures/`**: `synthetic.py` (builders for test exports and the 50-window timing export) and `summary-lines.txt` (a current-format line, a 6 November line from the [rig record](rig-2026-11-06-results.md), a traffic line that is not a summary, and a line with physical bodies inactive).

### Refusals

The report is refused, with nothing written, when:
- any Decision 8 header field is missing or empty, including each machine field, each read-back setting, and `name`/`sha256`, `name`/`provenance_hash` and `name`/`version` in every mod-set, fixture and capability entry. A `source=offline` export may set the rig-only fields (probe hashes, game build, mod set, rig commit and overlay, settings, capabilities) to `n/a`; a native export may not;
- `contract_version` is not `perf-gate/v1`;
- any export or window lacks `source`, or the exports and windows mix `native` and `offline`;
- two exports carry different headers, a window ID repeats, a timing sample is negative or not finite, or a frame row lacks its timing fields.

### Window validity

A window is invalid, kept in the report and excluded from every verdict and every pooled figure, when any required receipt (`autosave`, `no_dotnet_activity`, `priority_high`, `focused`, `settings_read_back`, `probe_coverage`, `probe_flags`, `presentation_trace`, `pc_readiness`, `accounting_diagnostics`) is missing or `fail`, or `n/a` where not allowed (only a reference window may mark `pc_readiness`, `accounting_diagnostics` and `probe_coverage` `n/a`). A window is also invalid when it is aborted, is the first of its game process, has fewer than 300 clean real seconds or has no clean frames. For a probe-format window (`probe.status` and `probe.frames`, the E5b probe's export shape), a dropped event or frame, timed exception, cross-frame scope, off-main callback, probe error, measurement still running, duplicate frame or skipped Unity frame fails `probe_flags`. Incomplete scope coverage fails `probe_coverage` for a candidate. Diagnostic and warm-up frames are not clean frames.

### Verdicts

- Per candidate window: M1 p95 < 5, M3 p99 ≤ 10, M4 mean ≤ 1.5 ms over clean frames; P1/P2 over the presented-interval series; P3 stalls ≥ 50 ms per 5 minutes and severe stalls ≥ 250 ms. A severe stall classed `pc-union` fails P3, and an unclassified one leaves P3 `not established` (classes come from F4c); S1 clock misses, S2 worst lag sample, S3 held fraction and longest hold at native speeds 1–3 (`n/a` above), S4 recovery to ≤ 0.1 game minute within 5 s after each closed hold. A row with no input is `not established` with the missing capability named.
- Reference windows carry `n/a` for M and S rows; their presented series is the P1/P2 baseline and the P4 reference.
- **P4** pairs a candidate with the reference of the same condition, script, arm and repetition, only when both are valid, the checkpoint minute is equal, the game span is within 1 game minute and the reference pace ratio is within 2 % of 1. Delta = candidate window p95/p99 − reference window p95/p99, each from its own frames. Otherwise `not established` with the reason.
- **Native baseline.** When the matched reference misses P1 or P2, the candidate's row is `not established` with "60 FPS not established on this machine (native baseline)", and the report's `sixty_fps` field says so. The overall verdict then rests on the other rows.
- S5, L1–L4 and R1 come from the export's `measurements` block (G9, G10, G14); `not established` when absent.
- Row verdict over primary-arm valid candidate windows: `fail` if any window fails, `pass` if all pass, else `not established`. Stress and report-only arms are judged on M1, M2, S1 and S2 in `secondary_arm_rows` and do not enter the overall verdict. **Overall** is `pass` only when every row except the M2 flag passes; any `fail` makes it `fail`.
- **Pooled** figures pool the raw clean frames of valid windows per condition, script, build and arm, and take nearest-rank percentiles of that pool. No percentile is averaged or taken of percentiles.

### M2 and fix-128 attribution

Every clean frame with mod-work ≥ 30 ms is listed with window, validity, condition, script, frame, whole-frame time, GC count, components, the largest component, the `AdvancePopulation` component time and the probe's per-call record (GC delta, optional-stop ms) when the export has one for that frame. The summary line's slowest-advance fields (unit kind and ms, optional-stop ms, GC count) attach to the one spike whose advancement time is within 0.1 ms of the observation's maximum advance, inside the observation's frame range when the export gives one. Every other advancement spike gets `unknown` unit fields. A spike with no advancement call gets no advancement fields. When no spike matches, or more than one does, nothing is attached. A 6 November line without the fix-128 segment attaches nothing.

### Summary-line fields parsed

Population (citizens, travelling, awaiting route/opening, clock, lag); advancement (frames, events, total ms, maximum advance, maximum events in one frame); slowest advance (unit ms and kind, optional-stop ms, GC count); optional-stop totals (calls, total, maximum, park trigger scans); routes (road snapshots, gate changes, waiting frames, rebuild reasons, slow routing frames, last rebuild, scene searches and maximum, station captures); discovery sweeps; RLO, door, station and preparation timings; closure refresh; route planning (requests, slices, events, pending frames, total, slowest slice and unit, NavMesh queries, event restarts, inline restarts, road search restarts, units, admission deferrals, path reserve ms); physical diagnostics as key=value pairs (`carAdmission` kept whole), or `inactive`. Each segment missing from a line is `null`.

### Export fields (`perf-gate-export/v1`)

`format`, `source`, `header` (Decision 8 fields), optional `measurements` (`S5.hours[]/days[]` with `real_s`, `unheld_real_s`, `s1_s2_hold`; `L1.samples_s`, `L2.samples_ms`, `L3.samples_s`; `L4.real_s`, `reference_real_s`, `s1_s2_hold`; `R1.heap_mb_per_day`, `m1_p95_per_day`), and `windows[]`: `id`, optional `source`, `condition`, `script`, `build`, `arm`, `repetition`, `native_speed`, `checkpoint_minute`, `game_span_minutes`, `clean_real_seconds`, `reference_pace_ratio` (reference), `first_in_process`, `aborted`, `receipts`, `frames[]` (`frame`, `mod_work_ms`, `whole_frame_ms`, `observer_ms`, `gc`, `diagnostic`, `warmup`, `components`) or `probe` (`status`, `frames[]` with `modWorkMs`, `wholeFrameMs`, `observerMs`, `gcCollections`), `presented_ms[]`, `stall_classes`, `accounting` (`clock_misses`, `lag_samples[[t_s, lag]]`, `holds[{start_s, end_s, carried_in, ongoing}]`), `advance_calls[]`, `summary_lines[]` (strings or `{line, first_frame, last_frame}`), `density`, `bodies`. F4b's final export names may differ. A converter in F4b/F4d maps them to this shape; F4a reads the E5b probe's camelCase rows directly.

## Known limits

- F4b's probe export is not final. F4a reads the E5b probe's frame shape and the fields above.
- Stall classes are F4c's. Until F4c, stalls are listed `unclassified (F4c)`, and a severe stall leaves P3 `not established`.
- S4 uses closed holds only. Recovery after a presented-frame stall needs F4c's QPC join.
- Summary lines carry no frame numbers. Without a frame range, a maximum attaches only when exactly one spike in the window matches it.

## Offline checks

`python -m unittest discover -s tools/perf-gate -p test_*.py`: 35 tests, OK. Each plan test and where it is:

| Plan test | Test |
| --- | --- |
| Nearest-rank percentiles match hand-computed values | `PercentileTests.test_nearest_rank_matches_hand_computed_values`, `test_window_m1_is_nearest_rank_of_its_own_frames` |
| No pooled percentile of percentiles | `PercentileTests.test_pooled_figures_pool_raw_frames_not_percentiles` (pooled p95 1.0 ms from 200 raw frames; the window p95s are 1.0 and 10.0), `test_invalid_windows_are_not_pooled` |
| A missing header field refuses the report | `HeaderTests` (every required field deleted and emptied, nested machine/settings/mod-set fields, version mismatch, `n/a` only offline, CLI exit 2 with no files written) |
| Any failed receipt invalidates a window | `InvalidationTests.test_any_failed_receipt_invalidates_the_window` (each receipt), plus missing receipts, reference `n/a` rules, short/first/aborted windows, probe status flags |
| A mixed offline/native verdict is refused | `SourceTests` (mixed windows, mixed exports, missing source, different headers) |
| P4 delta pairs only matched windows | `PairingTests.test_p4_pairs_only_matched_windows` (checkpoint, span 1.5, pace 3 %, pace missing; 0.9 and 1.9 % still match), `test_p4_never_pairs_across_condition_script_or_repetition`, `test_invalid_reference_gives_no_delta` |
| `not established` when the reference misses P1 | `PairingTests.test_not_established_when_reference_misses_p1` (and the same candidate fails P1 against a reference that meets it) |
| Summary-line parser covers fix-128 fields; attaches only to the matching frame; other spikes `unknown` | `SummaryLineTests` (all segments of a current line, a 6 November line without fix-128 fields, inactive bodies; matching frame only, other advancement spike `unknown`, non-advancement spike no fields, ambiguous and out-of-range maxima attach nowhere; end-to-end through the analyzer and Markdown) |

Also: the contract's row list and values equal Decision 4 (`ContractTests`); changing a contract threshold changes the verdict; S1–S4, P3, stress-arm separation and the overall rule (`RowTests`); CLI JSON and Markdown output and a 50-window run (`ReportTests`).

Build: `python tools/build.py --game-managed "C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"`, OK. Unit tests: `dotnet test tests/PersistentCity.Tests.csproj`, 4,288 passed, 0 failed. The golden-hash test for writer 35 passes unchanged.

## In-game checks for the next batched rig

All run on the agent rig in the plan's named disposable FT cities, never in Peter's ordinary save, with autosave recorded, turned off before every window and after every reload, and restored at cleanup. Each check feeds the batch's exports to `tools/perf-gate/analyze.py` against `contract-v1.json`. "Locked-PC" checks need the locked rig session; "input-only" checks run the analyzer over outputs that the batch already collected. None is left to Peter, a playtest or a by-hand check.

1. **Header completeness on the real batch (G1).** Locked-PC. Setup: the rig PC at batch start. Steps: run G1's preflight, write the header from it, then run the analyzer over the first G4 window export. Expected: the report is written. Every Decision 8 field is filled from read-back values. With one machine field removed from a copy, the analyzer exits 2 and writes nothing. **rig-automated (existing: `rig flow run` autosave guard, RIG-MODSET; pending: RIG-FOREGROUND, RIG-PRIORITY).**
2. **Real probe export ingestion (G2).** Locked-PC. Setup: FT-F4-Declared at 07:00, the F4b probe armed with no business. Steps: 5 real minutes measured, page-export, convert to `perf-gate-export/v1`, analyze. Expected: clean-frame count equals exported non-diagnostic, non-warm-up frames; `probe_coverage` and `probe_flags` pass; M1/M3/M4 match a hand recount of the same export's p95/p99/mean. **rig-automated (pending: RIG-PERF-UNION).**
3. **Declared-load matrix verdict (G4).** Locked-PC. Setup: FT-F4-Declared checkpoints and the dependency-only reference, ABAB. Steps: run G4, analyze all windows in one report. Expected: each candidate pairs with its own reference; P4 `pass`/`fail` only for matched pairs, others `not established` with the reason; if a reference misses P1 or P2, the report shows the native-baseline label; pooled figures list only valid windows. **rig-automated (existing: RIG-MODSET reference arm, FT `load`/`time`/`teleport`, RIG-VIEW `camera`; pending: RIG-PERF-UNION, RIG-PRESENT, RIG-FOREGROUND, RIG-SHOPFIX).**
4. **Receipt invalidation on real data (G4 control).** Locked-PC. Setup: one extra W-idle S-stationary candidate window stopped after 120 real seconds. Steps: export and analyze with the G4 windows. Expected: that window is listed under invalid windows as `short`, is absent from the pooled W-idle figures and does not change any row verdict. **rig-automated (existing: `rig flow run`; pending: RIG-PERF-UNION).**
5. **Fix-128 attribution from Player.log (G11).** Input-only. Setup: the Player.log and probe exports of G4's W-morning and W-evening windows. Steps: pass each window's summary lines with their observation frame ranges into the export, then analyze. Expected: every current-format summary line parses with all segments non-null; each observation whose maximum advance is ≥ 30 ms gives its slowest-unit fields to exactly one M2 frame within 0.1 ms; every other advancement spike shows `unknown`; non-advancement spikes show no advancement fields. **rig-automated (existing: Player.log collection in `rig flow run`; pending: RIG-PERF-UNION).**
6. **Stress arm separation (G15).** Locked-PC. Setup: FT-F4-Stress10k. Steps: G15's windows exported with `arm=stress`, analyzed with the G4 windows. Expected: stress results appear only under `secondary_arm_rows` (M1, M2, S1, S2), and the primary row verdicts equal those of a report built without them. **rig-automated (pending: RIG-PCFIX `pc-stress-10k@1`, RIG-PERF-UNION, RIG-PRESENT).**
7. **Measurement rows (G9, G10, G14).** Input-only. Setup: the batch's G9 pace, G10 latency and G14 soak records. Steps: write them into `measurements` and analyze. Expected: S5, L1–L4 and R1 each `pass` or `fail` with its value; L2 samples ≥ 30 ms listed under `flagged`; a missing record gives `not established`, never `pass`. **rig-automated (existing: FT `time`/`load`/`state`, RIG-WEEKS `time_machine`; pending: RIG-PCCONFIG `GameClockFactor`, RIG-PERF-UNION).**
8. **Offline/native separation (F4e with the batch).** Input-only. Setup: F4e's `source=offline` report and the batch's native export. Steps: pass both to one analyzer run, then each alone. Expected: the joint run is refused with exit 2; each alone produces a report labelled with its source. **rig-automated (existing: rig flow evidence collection; pending: F4e offline export, RIG-PERF-UNION).**
