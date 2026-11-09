Status: implemented (offline); not verified by rig

# City audit F4b: complete union probe (rig-only), 9 November 2026

Refs [#91](https://github.com/PeterHartwieg/big-ambitions-persistent-city/issues/91). Branch `codex/pc-f4b-union-probe` from `codex/persistent-city` at `b8feb06` (F2e #172 merged; current writer 35). Scope: the F4b row of [the F4 plan](../plans/f4-perf-gate.md#4-slice-breakdown), with Decision 3 (reference arm and pace shim), Decision 5 (complete union, fail-closed inventory, holds) and Decision 7 (per-call advancement records). No schema change, no save-shape change, no Runtime, Core or Persistence edit.

Re-read at this base: every `Patch(`/`HarmonyMethod` site in `Runtime/` (the files listed in the plan's section 2, plus `Runtime/LoadedCatalogueLifecycleMembers.cs` and `Runtime/FoodContractHooks.cs` from E5b2/E5b3), `Runtime/NativeCityClock.cs` (`TickPrefix`, `AccountingPrefix`, counters), `Runtime/PersistentCityMod.cs` (`UpdateCity`, `AdvancePopulation`, `UpdateInspector`), `Runtime/SaveEpochRuntime.cs` (`Update`, `Save`, event subscriptions), `Runtime/AccountingStatus.cs`, `Runtime/OptionalStopEnvironment.cs`, `Runtime/MovementDiagnostics.cs` (`ApplyHoldReplay`), `Runtime/PlayerContact.cs` and `Runtime/PhysicalPopulation.cs` (`Update`, `ObservePlayerContact`; read only), and `tools/e5b-native-rig-proof/` (not edited).

## What is implemented

All in `tools/perf-union-probe/`; see its [README](../../tools/perf-union-probe/README.md).

- **`PerfUnionProbe.dll`** (`Probe.cs`), a rig-only DLL with one rig op, `perf_union`, and actions `inventory`, `status`, `arm`, `measure-start`, `measure-stop`, `page`, `heap`, `disarm`. `arm` needs no player business. It binds Persistent City by reflection only, so the same artifact serves both modes. It is never packaged.
  - **Candidate mode.** At arm every expected callback is patched for timing, installed or not, so hooks Persistent City installs later (entering the street, physical bodies starting) are timed. Every prefix, postfix and finalizer, the five MonoBehaviour messages, the eight native event handlers and the three transpiler helpers get an inclusive timing scope. The two transpiled methods are `untimed-transpiled`. Harmony's patch info is rescanned (all six patch-list fields of `Patches`, through `HarmonyScan.cs`) at arm, at window edges and once per real second while measuring; a PC patch that is not an inventory row on its expected target, or is registered twice, is `unexpected` (kept for the rest of the capture), and an unpatchable row is `unbindable`; either makes `coverage_complete` false. Expected rows absent at the last scan are listed as `not_installed`.
  - **Buffers**: frames 131,072 (a 20-minute window at the 90 FPS cap undrained), ticks 262,144, diagnostics 8,192; all three drain while measuring through `page`, and a full buffer counts dropped rows instead of overwriting.
  - **Components**: the eleven attribution sub-scopes (Decision 5's list plus F2e's `ApplyHoldReplay` and `ObservePlayerContact`), per frame, never added to the union. An unpatchable one is `component-unbound`.
  - **Per frame**: the QPC time at a probe entry at the start of the player loop's `EarlyUpdate`, union mod-work, whole-frame wall interval, `observer_ms`, the probe's boundary cost, Mono GC count, save callbacks (the probe's own `GlobalEvents.onSaveGame` handler), scene load/unload, native loading, diagnostic, late-boundary, partial and cross-frame flags, native and logical minute.
  - **Holds**: a prefix and postfix on `NativeCityClock.AccountingPrefix` record every tick. Held only when `HeldOrdinaryTicks` or `HeldEconomySkipTicks` rose across the call; `AccountingLimited` without that is a boundary clip, kept apart. Hold/resume events with `carried_in` and `ongoing` at the window edges.
  - **Per-call records** for every `AdvancePopulation` call ≥ 30 ms: duration, GC delta, optional-stop ms from the `SeamMilliseconds` difference, target minute; slowest unit `unknown`.
  - **Diagnostics** once per real second, outside timed scopes: `AccountingDiagnostics` and `SaveEpochDiagnostics`; that frame is flagged.
  - **Reference mode**: refuses when Persistent City or any of its owners is present; installs only the pace shim on `GameManager.RunMainGameTick(float)` and the frame, GC, QPC, save and scene recording. PC fields report `n/a`.
- **`ProbeLogic.cs`** (Unity-free): `PaceShim` (the shim rule), `HoldTracker` (the hold state machine), `AdvanceCallRule` (the 30 ms threshold, an exact integer comparison of QPC ticks) and `UnionClock` (nested union and component totals).
- **`ScopeInventory.cs`** (Unity-free): 58 patches (the plan's 35 section 2 rows and the 23 food-contract postfixes added by E5b2/E5b3 after the plan), 5 messages, 8 native event handlers, 3 transpiler helpers, 11 components and 13 call sites; and the expected-scope hash over its canonical text.
- **`check_scopes.py`**: scans `Core/`, `Compatibility/`, `Persistence/` and `Runtime/` and fails on any difference with the inventory in either direction (multisets including each patch's resolved native target), on a moved component or call site, on an unresolvable target or transpiler method reference, on a helper or dynamic `Patch` call that registers anything other than its one recognised target, on reflection lookups or cloned instructions in a transpiler, on an emitted operand that is not a recognised reference, and on any construct it cannot classify.
- **`build_probe.py`**: deterministic build with pinned references (the game's Managed DLLs and Lib.Harmony 2.4.2, SHA-256 checked); runs `check_scopes.py` first; writes a path-free hash manifest.
- **`logic-check/`**: a console project compiling `ProbeLogic.cs`, `ScopeInventory.cs` and `HarmonyScan.cs` against the pinned 0Harmony.dll (SHA-256 checked), with the rule checks, the coverage-lifecycle and capacity checks, the Harmony adapter check on real `Patches` objects and the stub-loop overhead measurement.

### Technical choices

- **Native event handlers are scopes.** The plan's section 2 lists patches and `Update`/`OnGUI`/trigger messages. `PopulationRuntime.Save`/`Shutdown`, `NativeInteriorCityView`'s building, time-machine and scene handlers and `RouteObjectDiscovery`'s scene handlers are also main-thread PC entry points outside Update, so the inventory lists them and the probe times them. `PopulationRuntime.OnDestroy` is listed for the same reason.
- **The 23 food-contract postfixes** (owner `persistentcity.food-contract`) are installed by E5b3's coordinator at this base, so they are part of the union. `test_check_scopes.py` checks that the remaining 35 patch rows equal the plan's section 2 inventory, and that the 23 rows equal `FoodContractHookTable` in `Runtime/FoodContractHooks.cs`.
- **F2e's hold call sites** are components plus pinned call sites, so a later slice that moves `ApplyHoldReplay` out of `UpdateCity` or `ObservePlayerContact` out of `PhysicalPopulation.Update` fails `check_scopes.py`.
- **The hold postfix has a prefix twin.** Comparing counters across the same call, not with the previous tick, keeps a clock restart (counters fall) from looking like a hold; that case is a `counters_reset` tick.
- **The window's first frame** is the first frame boundary after `measure-start`; a carried-in hold opens there. The stop frame is written as a partial, diagnostic row, and an open hold closes there as `ongoing`.
- **Frame boundary in the player loop.** A probe-owned `PlayerLoopSystem` at the start of `EarlyUpdate` gives the frame-start QPC for joining the presentation trace. `disarm` removes only that entry. If the loop cannot be changed, frames carry `late_boundary` and `boundary_source` says `first-callback`.
- **Owner** `f4b.perf-union-probe` is outside the PC owner namespaces, so the probe never sees itself.
- **No compile-time PersistentCity reference.** Reflection binding lets reference mode load with PC removed. Hot-path reads use delegates and dynamic-method getters bound at `arm`.

## Offline checks

Run on the PC (Windows), base `b8feb06`:

| Check | Result |
| --- | --- |
| `python tools/build.py --game-managed "C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"` | passed; `PersistentCity.dll` SHA-256 `d77eaf13…a4d8`, the same before and after this slice |
| `python tools/perf-union-probe/check_scopes.py` | PASS; expected-scope hash `7dafd98f51599a5c637306797c644f467407a370d4402668c830cc8338fe1dc1`; patches 58, messages 5, events 8, helpers 3 |
| `python tools/perf-union-probe/build_probe.py --game-managed …` | passed; `PerfUnionProbe.dll` compiles against the game assemblies with warnings as errors |
| `dotnet run -c Release --project tools/perf-union-probe/logic-check/LogicCheck.csproj -- --expect-hash 7dafd98f… --perf` | 63/63 checks passed; the C# hash equals `check_scopes.py`'s |
| `python -m unittest discover -s tools/perf-union-probe -p test_*.py` | 24 tests OK, including the two-build manifest test (not skipped on this PC) |
| `dotnet test tests/PersistentCity.Tests.csproj` | 4,288 passed, 0 failed |

What the tests cover (F4b row):

- `check_scopes.py` passes on the base (`test_passes_on_base`, `test_command_line_exit_codes`) and fails on a synthetic extra `Patch(` call (`test_fails_on_synthetic_extra_patch_call`), an extra helper call, a new food-contract postfix, a removed patch, a new MonoBehaviour message or class, a new native event or lambda subscription, a moved F2e hold call site, a renamed component, a new transpiler helper, and unrecognised forms (`PatchAll`, a `HarmonyMethod` held in a variable, a positional method argument, a coroutine). Comments and strings do not count.
- The expected list equals the plan's section 2 inventory (`test_expected_list_equals_plan_section_2_inventory`, `test_section_2_names_are_in_the_plan`), and the food-contract rows equal the hook table.
- The build script's hash manifest is stable across two builds (`test_hash_manifest_is_stable_across_two_builds`: two builds into different directories give byte-identical manifests and DLLs, and the manifest holds no machine path).
- The README states the limits (`test_readme_states_the_limits`).
- logic-check: the shim multiplies only ordinary (not loading, time machine present and not running) positive finite deltas and leaves zero, negative, ±infinity and NaN unchanged, and refuses factors outside 0.1–1; a synthetic tick sequence gives the expected six hold/resume events with `carried_in` at the first frame and `ongoing` at the last; `AccountingLimited` without a held-counter increase is a boundary clip with no event; a counter reset is not a hold; a per-call record is written for a 30.0 ms call and not for a 29.9 ms call at 10 MHz, 1 GHz, 3.579545 MHz and 1 kHz timers; nested scopes count once in the union and components are never added.

The current-writer golden hash (writer 35) is unchanged: this slice touches no Core, Persistence or Runtime file, and `dotnet test` includes the pinned golden tests.

## In-game checks for the next batched rig

Every check runs on the owned rig in a named disposable FT city (FT-F4-Declared or a disposable copy of its checkpoints), never in Peter's ordinary save. Autosave is recorded, disabled in the isolated profile, verified off before each window and after every reload, and restored at cleanup. The probe DLL is the one `build_probe.py` built at the merged head; the flow pins its SHA-256 and passes the expected-scope hash to `arm`. Every check is rig-automated; none is left to Peter, a playtest or a by-hand check. **locked-PC** means the check times frames and needs the locked Windows rig session at High priority in a focused foreground window; **input-only** means it checks probe correctness and needs only the rig's scripted flow steps.

1. **Union coverage and probe overhead (plan G2).** locked-PC.
   - Setup: FT-F4-Declared at 07:00, Persistent City active, no business selection, the probe staged next to the candidate build.
   - Steps: load the checkpoint with the player inside a building; `arm` with `mode=candidate` and the expected-scope hash; read `status` and page `scopes`; leave the building and wait until the physical population is active; read `status` again; then two rounds of 2 real minutes measuring and 2 real minutes disarmed; export `frames` after each measured window.
   - Expected: `coverage_complete=true` both before and after leaving the building; the traffic and ambient rows are `installed=false` in the building and `installed=true` after, with nonzero `calls` once measuring; `unexpected` and `unbindable` empty; `patch_set_changed_in_window=false`; `untimed_transpiled` lists `SelectTranspiler` and `BodyForceTranspiler` when physical bodies are on; every `timed` row in `scopes` matches a `check_scopes.py` row at the same head; `frame_dropped`, `cross_frame_scopes`, `off_main_callbacks`, `union_underflows` and `unknown_scope_calls` are 0; `boundary_source=player-loop-early-update`; the `observer_ms` share of mod-work and the presented-frame p95 difference between measured and disarmed rounds are recorded.
   - Capability: pending RIG-PERF-UNION (stage, arm, measure, stop and page-export the probe through the existing RIG-MODSET transaction and the rig ops extension convention); pending RIG-PRESENT for the presented-frame difference; existing `rig flow run` autosave guard.
2. **Reference arm and pace shim (Decision 3; serves G4 and G9c).** locked-PC.
   - Setup: the same checkpoint loaded from a disposable copy with Persistent City removed from the mod set and every other mod unchanged (the pinned 0Harmony staged with the probe if no other mod loads it). Never saved.
   - Steps: `arm` with `mode=reference`, `factor=0.3333333333`; wait for the native load and 60 real seconds; measure 5 real minutes at native speed 1; export `frames`. Repeat with `factor=1` at speed 3. Then, in a candidate session, call `arm` with `mode=reference`.
   - Expected: `pace_shim.active=true` and `multiplied` close to `ticks` (only loading or time-machine ticks unmultiplied); game minutes per real second from the frames' native minutes within 2 % of the declared unheld pace for each factor; scope, hold, call and diagnostic fields `n/a`; the candidate-session reference `arm` is refused with `f4b_reference_requires_persistent_city_absent`.
   - Capability: existing RIG-MODSET per-flow DLL set and FT `load`/`time`; pending RIG-PERF-UNION.
3. **Per-tick holds and boundary clips (Decision 5; serves S2–S4 and G9).** input-only.
   - Setup: FT-F4-Declared saved at 06:00.
   - Steps: load the checkpoint, `arm` candidate before the "Preparing Persistent City" text clears, `measure-start` at once, run 3 game minutes past readiness at speed 1, `measure-stop`; then measure 30 game minutes at speed 3 in the morning commute; export `ticks`, `holds` and `diagnostics`.
   - Expected: the first window starts with a `carried_in` hold that resumes when readiness returns; every `hold`/`resume` pair matches a run of `held=true` ticks; ticks with `accounting_limited=true` and no held-counter rise are `boundary_clip=true` and create no event; summed `withheld_minutes` over the window equals the change in the diagnostics samples' `withheldGameMinutes` within one tick; a hold still open at stop ends `ongoing`.
   - Capability: existing FT `load`/`time`, `rig flow run`; pending RIG-PERF-UNION.
4. **Per-call advancement records (Decision 7; serves G11 and #128).** locked-PC.
   - Setup: FT-F4-Declared at 17:45.
   - Steps: `arm` candidate, measure W-evening (17:45–19:15) at speed 1, export `calls` and `frames`, collect Player.log.
   - Expected: every `AdvancePopulation` call ≥ 30 ms has one `calls` row with duration, `gc_delta`, `optional_stop_ms` (a number while the seam is bound) and `slowest_unit=unknown`; no row is below 30 ms; the largest row's duration matches the 60-second summary's maximum advance for that observation within 0.1 ms; each row's frame has `advance` component time ≥ the row's duration minus 0.1 ms.
   - Capability: existing Player.log collection in `rig flow run`, FT `load`/`time`; pending RIG-PERF-UNION.
5. **Fail-closed arming and inventory states.** input-only.
   - Setup: FT-F4-Declared, candidate build, Driven Rides not in the mod set.
   - Steps: `arm` with a wrong `expected_scope_hash`; `arm` with `mode=candidate` and a `factor`; `arm` correctly; read `status` and page `scopes`; `page` `scopes` while measuring; `heap` while measuring.
   - Expected: refusals `f4b_expected_scope_hash_mismatch`, `f4b_candidate_factor_is_read_from_persistent_city`, `f4b_stop_before_exporting`, `f4b_heap_read_outside_windows_only`; after the correct arm, `RidePrefix` and `RidePostfix` are listed in `not_installed` and coverage is still complete; every food-contract `HookN` row is `timed`.
   - Capability: existing RIG-MODSET, FT `load`; pending RIG-PERF-UNION.
6. **Save and scene counters.** input-only.
   - Setup: a disposable copy of FT-F4-Declared (this check saves; it is not a timing window).
   - Steps: `arm` candidate, `measure-start`, FT `save` once, run 10 real seconds, `measure-stop`; export `frames`; then reload the copy and read `status`.
   - Expected: exactly one frame with `saves=1` and `saves_in_window=1` before the reload; the reload disarms the probe (`armed=false`, `stop_reason` `save_changed` or `disarmed`), and `probe_patches_installed=false`.
   - Capability: existing FT `save`/`load`; pending RIG-PERF-UNION.
7. **Disarm restores native state.** input-only.
   - Setup: FT-F4-Declared, after check 1.
   - Steps: `disarm` and read `status`; `arm` candidate again; page `scopes`; `disarm` and read `status`.
   - Expected: after each `disarm`, `probe_patches_installed=false` and `player_loop_entry=false`; the second arm sees the same `timed`, `untimed-transpiled` and `not_installed` rows as the first and no `unexpected` row; Player.log has no probe exception; the population keeps running (the accounting text and the 60-second summary continue).
   - Capability: existing Player.log collection; pending RIG-PERF-UNION.
8. **Heap read for the soak (plan G14).** input-only.
   - Setup: FT-F4-Declared at day 3, 06:00, outside any window.
   - Steps: `heap` once per game day for 3 game days at speed 3, with the measured windows of G14 in between.
   - Expected: each reply has `after_forced_gc_bytes` > 0 and `before_bytes` ≥ 0; no reply while measuring; R1 is computed from these values by F4a.
   - Capability: existing FT `time`; pending RIG-PERF-UNION.
