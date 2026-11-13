# F4: an integrated 60 FPS experience gate

Plan of 9 November 2026, trimmed 13 November 2026. Refs [#91](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/91). F4a to F4e are implemented; F4f, the gate run, is planned. The full plan, with the slice breakdown, file ownership and source citations, is at [`a3a6c8db8686f4382ac3cf941010e0b614e48805`](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/f4-perf-gate.md). Decisions in force are in the [city direction register](../decisions/2026-11-10-city-direction.md).

## 1. Goal

On the declared test PC and settings, a full Persistent City runs at a steady 60 FPS through the busy parts of the day: morning commute, lunch, evening, midnight bookkeeping, a crowded street, a moving camera and the management screens. The city keeps its calendar pace while doing it, and no sale is lost because the computer was busy. Loading, reloading, skipping time and saving have their own limits and are measured on their own.

One frozen, versioned gate (`perf-gate/v1`) measures presented frames, the complete Persistent City mod-work union, simulation lag and pace, and separate latency criteria, on declared fixtures and settings, against a matched dependency-only reference. It reports every unexplained stall as open evidence instead of attributing or dismissing it.

The standing per-slice rule does not change: **mod-work p95 < 5 ms, every mod-work spike ≥ 30 ms flagged, whole-frame timing reported separately** ([roadmap](../roadmap.md), AGENTS.md). F4 adds the experience gate on top of it. The [#128](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/128) investigation stays separate; F4 supplies its attribution data (Decision 7).

Out of scope: the #128 fix, movement authority (F2), save pause reduction (F7), native hook capability contracts (F8) and the offline acceptance entry point (F9).

## 2. Inventory snapshot at the plan's base (`c2fc1b0`)

`tools/perf-union-probe/test_check_scopes.py` reads the hook list below. The live inventory is `tools/perf-union-probe/ScopeInventory.cs`, checked against source by `check_scopes.py`.

- **Native hooks PC installs** (Harmony, verified at the base): `NativeCityClock` tick, accounting and ride prefixes/postfix (`Runtime/NativeCityClock.cs:70`, `:73`, `:82-83`); `NativeTravelPace.ObserveTickPostfix` (`Runtime/NativeTravelPace.cs:36`); `NativeFoodOwnership.AfterNativeTick` on `RunMainGameTick` plus its customer prefixes (`Runtime/NativeFoodOwnership.cs:57-58`, `:68`); `NativeFoodMarket.ResidentPrice` on `CitizenData.IsPriceAcceptable` (`Runtime/NativeFoodMarket.cs:138-139`); `AmbientPedestrians` spawn and state prefixes (`Runtime/AmbientPedestrians.cs:107-109`); `StationaryCrowds.SpawnPrefix` (`Runtime/StationaryCrowds.cs:32`); `ResidentInspector` input, camera and raycast guards (`Runtime/ResidentInspector.cs:79`, `:93-94`, `:105`); `PhysicalTrafficHooks` speed, crash, waypoint and selection hooks, a transpiler, and the `TrafficManager.FixedUpdate` prefix/postfix/transpiler/finalizer (`Runtime/PhysicalTrafficHooks.cs:46-88`); `PhysicalIntersections` set-active prefix and update postfix (`Runtime/PhysicalIntersections.cs:85-86`); `ResearchBilling.RunDailyPostfix` (`Runtime/ResearchBilling.cs:45`); `ResearchAgencyDialog.CampaignSettingsPostfix` (`Runtime/ResearchAgencyDialog.cs:72`).
- **The union timing probe.** The F4b probe in `tools/perf-union-probe/` replaced the E5b probe for gate timing; see its README.

## 3. Decisions

### Decision 1: one declared reference machine and settings

The 60 FPS claim applies to one declared configuration: the owned Windows rig PC and the settings below. The report header records the CPU, GPU, RAM, OS build, GPU driver, display refresh rate and Windows power plan as read on the day; the plan does not assume any of them.

- **Primary settings:** 1920×1080, the game's Low preset, vsync off, frame cap 90. This matches the 30 October campaign, so the dependency-only numbers can be compared with it. A cap above 60 shows headroom; vsync off keeps presented intervals tied to work instead of refresh timing.
- **Report-only arm:** the same evening window at the High preset, recorded for information but outside the pass/fail verdict. Q1 asks whether the claim should cover more settings.
- **Foreground, focused, at High priority.** The 6 November runs were in background `-rig-test` mode at about 30 fps and BelowNormal priority by default; neither represents play. Gate windows need a focused foreground window at the declared resolution (pending RIG-FOREGROUND) and the player at High priority with the CPU contamination sampler on (pending RIG-PRIORITY, the 6 November rig follow-up).

### Decision 2: fixtures and declared loads

Fixture specs are in `tools/perf-gate/fixtures.json`:

- **FT-F4-Declared:** a fresh city aged to day 3 (F5g fill at its targets, about 957 residents) with three staffed and stocked player food shops and the F6 economy at defaults. Main gate fixture.
- **FT-F4-Stress10k:** about 10,000 persisted persons. Judged on M1/M2 and S1/S2 only.
- **Fastest-traveller arm:** FT-F4-Declared at `GameClockFactor = 1`, at native speeds 1–3 with bodies, then speed 6 and a time-machine skip. Judged on the S rows.
- **Dependency-only reference:** the same checkpoints with Persistent City removed and every other mod unchanged, from a disposable copy that is never saved, with the probe in reference mode.

"Fastest-traveller city" is interpreted here as the fastest calendar pace the player can configure; the task did not define it further. The movement state is whatever has merged when the batch runs. The final gate verdict (F4f) needs F2d merged, so that body cost reflects cars following the kernel. Until then, windows are labelled `movement=pre-F2d`, and their verdict counts only as a baseline.

### Decision 3: windows, scripts, warm-up, samples and invalidation

**Windows** (start times and repetitions in `fixtures.json`): W-idle 03:00, W-morning 07:00–08:30, W-lunch 11:45–13:15, W-evening 17:45–19:15, W-midnight 23:30–01:00, and W-accounting, the native hour rollovers inside W-lunch and W-evening with the owned shops open. At speed 1 and factor 1/3 a game minute takes about 3.3 real seconds.

**Camera and route scripts**, each a fixed step list driven by the rig from the window's checkpoint:

- **S-stationary:** the player stands at a kerb point by an owned shop, off walking lines (the F2 plan's check 1 placement).
- **S-path:** a continuous camera path along a dense street and through a quiet district and back, at walking pace (RIG-VIEW `camera look_at` along waypoints).
- **S-sweep:** FT `teleport` across six kerb points 200 m apart, 10 game minutes each (the F2 plan's check 3 sweep). Evening only; it measures acquisition and release churn.
- **S-drive:** the player drives the owned car along a fixed route through the dense street (pending RIG-DRIVE, also requested by F2).
- **S-manage:** the Ctrl+F8 panel open, the resident inspector opened on three residents, then native management screens (business overview, staff, inventory) opened and closed in a fixed order (pending RIG-UI-SCRIPT; the 6 November run reported these as NEEDS-INPUT).

**Reference arm.** Removing Persistent City also removes `NativeCityClock.TickPrefix`, which multiplies ordinary native ticks by `GameClockFactor` (`Runtime/NativeCityClock.cs:94-98`; "ordinary" means active, not loading and no time machine running, `:51-52`). Without a correction the reference would run three times as many game minutes per real second as the candidate at factor 1/3, so its windows would carry a different calendar workload. The reference therefore runs with the F4b probe in **reference mode**:

- **Pace shim.** The probe installs its own prefix on `GameManager.RunMainGameTick(float)` with the same rule: while the game is not loading and no time machine is running, a positive, finite delta is multiplied by the declared factor of the matched candidate arm (1/3, or 1 for the fastest-traveller arm). It runs no Persistent City simulation and no calendar holds, so the reference gets the declared *unheld* pace. The shim is rig-only, like the rest of the probe. It is the only probe action that changes native state, and the report header records it.
- **Readiness.** A reference window starts when the native load has finished, the pace shim reports active, and 60 further real seconds have passed. Persistent City readiness, the accounting text, PC scope coverage and `AccountingDiagnostics` do not apply and are marked `n/a` in the reference's receipts. Autosave, contamination, priority, focus, settings read-back, presentation-trace coverage and the probe's frame-export flags (overflow, cross-frame, error) still apply.
- **Matched coverage.** P4 is computed only for a reference/candidate pair whose windows start at the same checkpoint minute and cover the same game-time span within ±1 game minute, and whose reference pace (game minutes per real second, from the probe's per-frame clock reads) is within 2 % of the declared unheld pace. Candidate holds make the candidate's real duration longer; that difference is reported under S3, not hidden in P4. A pair that fails matching is reported, and its P4 is `not established`.

**Interleaving.** For each window × script, the rig runs reference and candidate in ABAB order from the same checkpoint: two repetitions per build, four for W-evening and W-morning. Each run loads the checkpoint, so builds see the same city state.

**Warm-up.** After a load, timing starts only when Persistent City reports simulation ready, the accounting text no longer says the calendar is held, and 60 further real seconds have passed. Those frames are discarded. The first window of each game process is discarded entirely (Mono JIT and asset streaming).

**Sample size.** Each measured window lasts at least **300 real seconds of clean frames** (18,000 frames at 60 FPS, so p99 then rests on at least 180 tail frames). The minimum is real duration, not a frame count: a complete window with fewer frames because the build ran slower is judged on P1/P2 like any other, and fails them if it is slow. Only an aborted window, or one invalidated by a receipt, is "short"; it is reported and does not count.

**Density validity.** A window counts toward the dense-street criterion only if, during S-path's dense segment, median observed bodies are at least 36 pedestrians (0.75 × 48) and 12 cars (0.5 × 24), read from the 60-second physical diagnostics and RIG-VIEW `screen_pos list`. The 6 November log shows cars ≤ 7 in a fresh city ([f5f-1](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/rig-2026-11-06-results.md)), so dense car load may not be reached. If it is not, the window is reported as "not dense" and I4's dense-traffic part stays unestablished. Lowering the threshold does not make it pass. A presentation stress arm at caps 256/64 (as in F2a) runs once in W-evening, reported only.

**Run budget.** The core matrix (6 windows × S-stationary and S-path × 2 builds × 2–4 repetitions, plus S-sweep, S-drive and S-manage in W-evening) is about 50 windows of 8 real minutes including load and warm-up: roughly 7 hours of locked rig time, plus the stress, fastest-traveller, latency and soak checks.

**Invalidation.** A window is invalid, kept in the report and excluded from the verdict, if any of these receipts fails:
- autosave: the original setting is recorded once, off before each window (also after every reload), no save callback inside the window (`SaveEpochDiagnostics.sequence` unchanged across it), restored at cleanup;
- no task-owned or foreign dotnet build or test was active (the 6 November sampler);
- player at High priority, window focused, declared resolution, preset, cap and vsync read back;
- the probe reports complete coverage (Decision 5) with no overflow, cross-frame, off-main or error flags;
- the presentation trace covers the whole window (Decision 6).

### Decision 4: frozen thresholds, `perf-gate/v1`

The rows M1–M4, P1–P4, S1–S5, L1–L4 and R1 and their pass values are frozen in `tools/perf-gate/contract-v1.json`. Changing a value needs a new contract version and a decision. Percentiles are nearest-rank over every clean frame of one window. Pooled figures pool raw frames across windows of the same condition. The gate never averages percentiles and never takes a percentile of percentiles.

Reading the rows:

- **P1/P2 are the 60 FPS claim; P4 is Persistent City's share of it.** If the dependency-only reference itself misses P1 or P2 on the declared machine, the report says "60 FPS not established on this machine (native baseline)". The verdict then rests on P4 and the M rows, and nothing is attributed to Persistent City without evidence. The 30 October reference p95 of 13–14 ms leaves about 2.7 ms below 16.7 ms, so P4's 1.5 ms is the budget Persistent City may use.
- **Component savings are not frame improvements.** A slice that lowers a component's time reports that in its perf file. Only a matched P4 change counts as a whole-frame improvement.
- **Stress and report-only arms** (FT-F4-Stress10k, High preset, 256/64 caps) are judged on M1/M2 and S1/S2 only. Their other rows are recorded.
- **Save** is not a row. Gameplay windows contain no saves. Save pause criteria are F7's section 3.1 budgets, measured in F7's save-specific campaign (G12).
- **Why these numbers.** M3/M4 bound the tail and the total that M1 lets through: on 6 November the advancement mean was 0.12–0.44 ms, and the 30 October union p95 was 1.26 ms. S3's 1 % and 3 s keep holds unnoticeable at normal speed. S5's limits are the Q2 default. L2 takes F7's frozen native main-thread budget unchanged, so the two plans never hold two values for one measurement. L1, L3 and L4 are first values set from the 6 November start-up observations; they are frozen in v1 like the rest.

### Decision 5: the complete union, measured from outside the product

The gate's mod-work is the nested union of every Persistent City entry point that runs on the main thread, measured by the rig-only F4b probe (`tools/perf-union-probe/`). It is never packaged and adds no Runtime code. Its README holds the scope rules, records and limits.

- **Fail closed.** Every PC prefix, postfix, finalizer, message, event handler and transpiler helper gets an inclusive timing scope. Transpiled methods are `untimed-transpiled`. An unexpected or unbindable PC patch makes coverage incomplete, and the gate refuses the window. `check_scopes.py` fails when source and `ScopeInventory.cs` differ.
- **Components** are nested sub-scopes for attribution only, never summed or subtracted. A method the probe cannot patch is `component-unbound`; the union stays authoritative.
- **Per frame:** Mono GC count, QPC timestamp of the frame boundary (for the presentation join), save callbacks and scene loads.
- **Calendar holds per tick.** One-second `AccountingDiagnostics` samples miss short holds, so the probe records every native tick around `NativeCityClock.AccountingPrefix`. A tick is held only when a held counter rose; `AccountingLimited` alone is a boundary clip, kept out of S3 and S4. Hold and resume events carry `carried-in` and `ongoing` flags at window edges. S2–S4 come from these records.
- **Reference mode:** Persistent City absent; only the pace shim and frame, GC, QPC, save and scene recording. PC-specific fields are `n/a`.
- **Limits** reported in every window: the union includes nested probe cost; `observerMs` measures only part of it; native work that PC forces inside a scope stays in that scope; native work outside PC scopes is not mod-work.

### Decision 6: presented frames and CPU/GPU evidence from a system trace

The rig records presentation with a Windows ETW presentation trace (a PresentMon-class capture; the tool choice is the rig's, pending **RIG-PRESENT**). It gives each present's QPC time, the interval between displayed frames, and, where the tool reports them, GPU busy time and CPU wait. The probe's frame boundaries carry QPC timestamps, so F4c joins the trace to probe frames by time without new game hooks. Whether the game's Unity build exposes `FrameTimingManager` data is not established and is not assumed. Process CPU and per-thread CPU time come from the same trace or from the rig's existing load sampler. The report states whether each frame looks CPU-bound or GPU-bound only when the trace provides GPU busy. Otherwise the field is `unknown`.

### Decision 7: stall attribution, with unexplained stalls kept open

Every presented interval ≥ 50 ms, and every probe whole-frame interval ≥ 50 ms, gets one or more classes, each with its evidence:

| Class | Evidence required |
| --- | --- |
| `pc-union` | PC union mod-work in that frame ≥ 50 % of the frame's excess over the window's reference p50; component and slowest unit named |
| `gc` | Mono GC count rose in that frame (probe) |
| `save-load-scene` | Save callback, load or scene-load flag in that frame (excluded from clean windows by definition; listed when found) |
| `native-frame` | RLO's frame diagnostics in Player.log name a long phase in that frame (the 6 November stalls coincided with long `UpdateRectTransform`/`LateUpdate` frames there), or the trace shows the time in game threads outside PC scopes |
| `gpu-present` | Trace shows GPU busy or present queueing covers the interval |
| `reference-rate` | The dependency-only arm has stalls of the same size at a similar rate in the matched window |
| `unattributed` | None of the above. Kept as open evidence with its frame, game minute, condition and raw numbers; never counted for or against Persistent City |

Rules: a frame may carry several classes, and the remainder (whole frame − PC union) is never attributed to PC. The 6 November record already does this ("Not attributed to PC"), and F4c makes it mechanical.

**#128.** Advancement spikes are mod-work and appear in the M2 list. The probe gives each M2 frame its component times and, for every `AdvancePopulation` call ≥ 30 ms, the duration, GC delta and optional-stop read time. The 60-second summary's slowest-unit fields describe only each observation's maximum call; the report attaches them to the one M2 frame that matches it and never copies them to another spike. Any field the probe could not bind is `unknown`, not inferred. F4 does not fix #128; it hands that investigation every evening and morning M2 flag with this attribution.

### Decision 8: report format and provenance

F4a's analyzer writes `perf-gate-report/v1` as JSON plus a generated Markdown summary:

- **Header (all required, or the report is refused):** source commit, PersistentCity.dll SHA-256, probe DLL SHA-256 and expected-scope list hash, game build, complete mod set with hashes, rig commit and overlay, machine fields (Decision 1), settings read back, PC config keys in effect, fixture names and provenance hashes, contract version, movement state label, and the list of active rig capabilities and their versions.
- **Invalidation receipts** per window (Decision 3).
- **Per-window table:** condition, script, build, repetition, frames, M1–M4, P1–P3, reference and delta (P4), S rows, density fields, body counts, GC count.
- **Verdict per row:** `pass`, `fail` or `not established` (missing capability, invalid windows or native baseline), with the reason. The overall verdict is `pass` only when every primary row passes in every primary window.
- **Spike list (M2)** and **stall table** (Decision 7).
- **Offline and native results stay apart.** F4e's offline pre-gate writes the same schema with `source=offline`. The analyzer refuses to mix offline and native windows in one verdict.

### Decision 9: what F4 does not add

No new Runtime telemetry; no change to frame budgets, caps, radius, clock factor default or event order to pass the gate; no save inside a gameplay window; no claims about the native game beyond the reference arm's numbers.

## 4. Slices

| Slice | Scope | Status |
| --- | --- | --- |
| F4a | Contract and analyzer, `tools/perf-gate/` | implemented |
| F4b | Complete union probe, `tools/perf-union-probe/` | implemented; native check G2 pending |
| F4c | Presentation trace and stall attribution, `tools/perf-gate/` | implemented |
| F4d | Batch flow and fixture specs, `tools/perf-gate/make_gate_flow.py` | implemented |
| F4e | Offline pre-gate, `tools/perf-gate-offline/` | implemented |
| F4f | Gate run record: import the batch's report, record verdict, spikes, stalls and open items | planned |

F4f's final verdict needs F2d merged (F2e preferred), so body cost reflects cars following the kernel. A batch before that is a labelled baseline. No F4 slice edits Core, Persistence or Runtime, or changes the save format.

## 5. Open questions

Only product decisions are listed. Work proceeds on the recommended default.

1. **Which graphics settings should the 60 FPS promise cover?** Recommended default: 1080p at the Low preset on the test PC. High is measured and reported, but not promised.
2. **When you fast-forward through a busy hour, may the city slow the clock so nobody misses a meal or a shift?** Recommended default: yes. A busy hour may take up to twice as long as normal, and a whole day at most a quarter longer. Nothing is skipped, and at normal speed you should not notice it.

All other choices in this plan are technical and recorded in section 3.

## 6. Risks

- **The native baseline may not hold 60 FPS.** P1/P2 then read `not established`, and Persistent City is judged on P4 and the M rows.
- **Dense car load may not occur** in a fresh city (cars ≤ 7 on 6 November). The window is reported as not dense; thresholds and caps are not changed to pass.
- **Rig blockers** (RIG-SHOPFIX, RIG-UI-SCRIPT) can leave G7, G8 and G13 `not established`. The report names the missing capability per row.
- **Fast-forward pace** may run near Q2's limit (offline F5h needed 1.53× the frames of an unheld clock). A G9 failure is a scheduler fix, not a relaxed threshold.

## 7. In-game checks for the batched rig

Every check runs on the headless agent rig in a named disposable FT city (FT-F4-Declared, FT-F4-Stress10k or a disposable copy of their checkpoints), never in Peter's ordinary save. Autosave is recorded, disabled in the isolated profile, verified off before every window and after every reload, and restored at cleanup; a save inside a window invalidates it. The player runs at High priority in a focused foreground window. Preexisting rig files and preferences are restored when the rig is released. No check is left to Peter, a playtest, a manual console session or a visual check by hand. Every timed check reports through F4a's analyzer against `contract-v1.json`.

1. **G1 — Preflight and declared configuration.** Setup: the rig PC at the start of the batch. Steps: read and record the machine fields, set and read back 1920×1080 Low, vsync off, cap 90; record autosave, disable it, verify off; start the dotnet contamination sampler; raise the player to High. Expected: every Decision 8 header field filled; any missing field stops the batch before timing. **rig-automated (existing capability: `rig flow run` autosave guard and health `autosave_disabled`, RIG-MODSET; pending capability: RIG-FOREGROUND (focused window, resolution, preset, vsync and cap set and read back), RIG-PRIORITY (High priority and contamination sampler as a flow step)).**
2. **G2 — Union coverage and probe overhead.** Setup: FT-F4-Declared at 07:00, no business selection. Steps: `arm` the F4b probe; read `status`; run 2 real minutes with the probe measuring and 2 without (probe disarmed), interleaved twice. Expected: every expected scope bound and `check_scopes.py`'s list equal to the probe's bound list; transpiled methods listed as `untimed-transpiled`; no overflow, cross-frame or off-main flags; probe overhead (`observerMs` share and the presented-frame p95 difference between armed and disarmed) recorded. **rig-automated (pending capability: RIG-PERF-UNION, the F4b probe staged through the existing RIG-MODSET transaction with export through the existing rig ops extension convention).**
3. **G3 — Presentation trace alignment.** Setup: as G2. Steps: start the RIG-PRESENT trace, run a 5-minute S-path window, stop both, export. Expected: F4c joins at least 99 % of probe frames to presents; trace covers the window; GPU busy present or explicitly `unknown`. **rig-automated (pending capability: RIG-PRESENT).**
4. **G4 — Declared-load matrix, matched.** Setup: FT-F4-Declared checkpoints at each Decision 3 window start, and the dependency-only reference from the same checkpoints. Steps: for W-idle, W-morning, W-lunch, W-evening and W-midnight × S-stationary and S-path, run the ABAB order with the warm-up and sample rules. Expected: M1–M4, P1–P4, S1–S4 per window as frozen; every M2 flag and every stall classified. **rig-automated (existing capability: RIG-MODSET reference arm, FT `load`/`time`/`teleport`, RIG-VIEW `camera`; pending capability: RIG-PERF-UNION, RIG-PRESENT, RIG-FOREGROUND, RIG-SHOPFIX for the owned shops in the fixture).**
5. **G5 — Dense streets and bodies.** Setup: W-evening checkpoint. Steps: S-path and S-sweep candidate and reference windows; one 256/64 cap arm. Read body counts from the physical diagnostics line every 60 s and `screen_pos list` every 10 s. Expected: density validity recorded; P and M rows for valid windows; the 256/64 arm reported (M1/M2, S1/S2 only). **rig-automated (existing capability: RIG-VIEW `camera`/`screen_pos`, FT `teleport`; pending capability: RIG-PCCONFIG `MaximumPedestrians`/`MaximumVehicles` rows as requested by the F2 plan, RIG-PERF-UNION, RIG-PRESENT).**
6. **G6 — Driving.** Setup: W-evening checkpoint with the owned car parked near the dense street. Steps: S-drive in both builds, ABAB. Expected: P and M rows; body counts. **rig-automated (pending capability: RIG-DRIVE, requested by the F2 plan; RIG-PERF-UNION, RIG-PRESENT).**
7. **G7 — Management UI.** Setup: W-lunch checkpoint, player inside an owned shop. Steps: S-manage in both builds (the Ctrl+F8 panel is a Persistent City screen, so the reference arm runs the native screens only). Expected: P and M rows; `OnGUI` and inspector-guard scopes reported as components. **rig-automated (pending capability: RIG-UI-SCRIPT, scripted pointer and keyboard in a locked session; RIG-PERF-UNION, RIG-PRESENT).**
8. **G8 — Accounting peaks.** Setup: FT-F4-Declared with the three owned shops staffed, stocked and open. Steps: W-accounting windows across two native hour rollovers at lunch and evening, and W-midnight. Read `AccountingDiagnostics` each second. Expected: S1 = 0 misses; M2 flags around `AfterNativeTick` and posting units listed with components; P rows. **rig-automated (pending capability: RIG-SHOPFIX, a complete staffed and stocked food shop built natively, blocked on 6 Nov by refused rental and equipment placement; RIG-PERF-UNION, RIG-PRESENT).**
9. **G9 — Fastest-traveller pace.** Setup: FT-F4-Declared checkpoint at 06:00 with `GameClockFactor = 1`. Steps: (a) one game hour each at native speeds 1, 2 and 3 with bodies, then 4 game hours at speed 6, then a time-machine skip of 120 game minutes; (b) from a second copy of the checkpoint, one complete game day (06:00 to 06:00) at native speed 3 with bodies, the declared speed for the daily criterion; (c) the same game day in the dependency-only reference with the pace shim at factor 1 and speed 3. The unheld-clock denominator is (c)'s measured real duration, cross-checked against the shim's game minutes per real second. The probe's hold/resume events are recorded throughout. Expected: S1 and S2 in every segment; S5's per-hour limit for every game hour of (a) and (b); S5's per-day limit (≤ 1.25 × the denominator) for (b); held time and longest hold per game hour; M rows in the speed 1–3 segments. **rig-automated (existing capability: FT `time`, RIG-WEEKS `time_machine`; pending capability: RIG-PCCONFIG `GameClockFactor`, RIG-PERF-UNION).**
10. **G10 — Startup, reload and skip latency.** Setup: FT-F4-Declared saved at 06:00 (saved outside any window). Steps: cold process start and load; fresh-process load; in-session reload; `skiptime 120m` in both builds. Read F7a's activation diagnostics and the accounting text. Expected: L1–L4. **rig-automated (existing capability: FT `load`/`save`/`time`/`state`, F7a diagnostics through FT `state`; pending capability: RIG-PERF-UNION for the activation frame).**
11. **G11 — Stall attribution and #128.** Setup: the outputs of G4–G9 and G14. Steps: run F4c's classifier over every presented and whole-frame interval ≥ 50 ms and every M2 flag; pull RLO frame-diagnostic lines, the probe's per-call advancement records and the fix-128 summary lines for the same observations. Expected: every stall has a class; `unattributed` stalls listed with frame, minute and condition; every evening and morning M2 flag carries its component times and, for advancement calls, the probe's GC delta and optional-stop ms; the summary's slowest-unit fields appear only on the frame that matches each observation's maximum, and every other per-frame field the probe lacks is `unknown`. **rig-automated (existing capability: Player.log collection in `rig flow run`; pending capability: RIG-PRESENT, RIG-PERF-UNION).**
12. **G12 — Save-specific segment (separately requested).** Setup: requested explicitly by the coordinator as its own segment of the batch, never mixed with G4–G11 windows. Steps: F7 plan section 7 checks 1–8 on FT-F4-Declared and the 957-resident representative checkpoint. Expected: F7's section 3.1 save budgets reported; F7 check 2's phase table is the input to the F7e build decision. **rig-automated (existing capability: `rig flow run`, FT `save`/`console`; pending capability: RIG-SAVEPROF, RIG-SAVETYPES, as requested by the F7 plan).**
13. **G13 — Scripted management session on the measured candidate.** Setup: the exact candidate build measured in G4, on FT-F4-Declared at 10:00. Steps: 20 real minutes at speeds 1–3 of a fixed management script: check the Ctrl+F8 panel, open the inspector on a regular, change a price at an owned shop, open staff and inventory screens, drive to a second shop, return. Record the gate for the whole session. Expected: P1–P3 and M1–M4 hold across the session; every scripted action is acknowledged by a native or PC state readback; S1 = 0. **rig-automated (pending capability: RIG-UI-SCRIPT, RIG-SHOPEDIT price edit, RIG-DRIVE, RIG-PERF-UNION, RIG-PRESENT).**
14. **G14 — Soak.** Setup: FT-F4-Declared at day 3, 06:00. Steps: 3 game days at speed 3 with S-stationary, probe measuring; read managed heap size after a forced GC once per game day (probe action, outside windows, diagnostic frames). Expected: R1; M1 per game day; S1/S2 throughout. **rig-automated (existing capability: FT `time`; pending capability: RIG-PERF-UNION with the heap read).**
15. **G15 — Stress city.** Setup: FT-F4-Stress10k. Steps: G4's W-morning, W-evening and W-midnight × S-path, candidate only. Expected: M1/M2, S1/S2; other rows recorded. **rig-automated (pending capability: RIG-PCFIX `pc-stress-10k@1`, RIG-PERF-UNION, RIG-PRESENT).**

### Rig capability requests (PeterHartwieg/big-ambitions-agent-rig)

- **RIG-PERF-UNION:** stage, arm, measure, stop and page-export the F4b probe through the existing RIG-MODSET transaction and rig ops extension convention; also requested by F7 check 9 and E5d check 6. G2, G4–G11, G13–G15.
- **RIG-PRESENT:** start and stop an ETW presentation trace bound to the game process, with QPC timestamps and GPU busy where available; export CSV with the flow evidence. G3–G8, G11, G13, G15.
- **RIG-FOREGROUND:** focused foreground window at a declared resolution, preset, vsync and frame cap, set and read back. G1 and every timed check.
- **RIG-PRIORITY:** player priority High and the dotnet contamination sampler as flow steps (the 6 November follow-up). G1.
- **RIG-SHOPFIX:** a complete staffed and stocked food shop built natively in a fresh head city (blocked on 6 Nov). G4, G7, G8, G13.
- **RIG-UI-SCRIPT:** scripted pointer and keyboard input in a locked session for panels, inspector and native management screens. G7, G13.
- Shared with other plans: RIG-PCCONFIG rows (`GameClockFactor`, `MaximumPedestrians`, `MaximumVehicles`), RIG-DRIVE (F2), RIG-PCFIX (F5), RIG-SAVEPROF and RIG-SAVETYPES (F7), RIG-SHOPEDIT (F6a).

### Batch placement

The F4 gate is the performance segment of the next combined native/perf batch. It replaces the separate "matched performance" windows that many slice checks request (for example b0-7, c2b-7, e5b-6, f3-1, f5h R8, f6b-8, f7a-7 and F2 rig check 10), because G4–G9 record per-window union, component and whole-frame data that those checks can read. The coordinator maps each slice check to the G window that serves it in the batch checklist. Order inside the batch:

1. Correctness and input-only checks on fresh fixtures (unchanged batch order from the [6 Nov results](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/rig-2026-11-06-results.md#run-order-for-the-next-batch)).
2. G1 → G2 → G3 at the start of the locked segment.
3. G4–G9, G13, G14, G15 with the reference arms interleaved.
4. G10.
5. G11 over the collected outputs.
6. G12, only when separately requested, after the gameplay segment and never inside it.

A batch that runs before F2d merges produces a labelled baseline. F4f's final verdict needs the batch after F2d (preferably after F2e), on a head that includes F5i if it has merged by then.
