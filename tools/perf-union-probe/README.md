# F4b perf union probe

Status: implemented (offline); native validation is pending (rig check G2 in [docs/checks/f4b.md](../../docs/checks/f4b.md)). Refs #91. Plan: [F4 plan](../../docs/plans/f4-perf-gate.md), Decisions 3, 5 and 7.

A rig-only diagnostic DLL that measures the complete Persistent City mod-work union, per-tick calendar holds and per-call advancement spikes from outside the product. It is **never packaged**: `tools/build.py` does not build or copy it, and it never goes into the Persistent City package, Workshop upload, the normal game or Peter's ordinary save. It adds no Runtime code, so the shipped DLL's behaviour and timing do not change. It reads, and never edits, the F2-owned physical files.

## Files

| File | Role |
| --- | --- |
| `Probe.cs` | The Unity DLL: rig op, inventory binding, scopes, frames, holds, per-call records, pace shim. |
| `ScopeInventory.cs` | The expected entry points (one row per line). Unity-free. |
| `ProbeLogic.cs` | The pace-shim rule, hold state machine, per-call threshold, union clock, coverage tracker and drainable buffers. Unity-free. |
| `HarmonyScan.cs` | Reads Harmony's patch info into plain observations. Uses HarmonyLib only; logic-check runs it against the pinned 0Harmony.dll. |
| `check_scopes.py` | Fails when Runtime source and `ScopeInventory.cs` differ in either direction. |
| `build_probe.py` | Builds `PerfUnionProbe.dll` with pinned references and writes `build-manifest.json`. |
| `test_check_scopes.py` | Python tests for the three tools above. |
| `logic-check/` | Console project compiling `ProbeLogic.cs` and `ScopeInventory.cs`: rule checks and the stub-loop overhead. |

## Build and checks

```powershell
python tools/build.py --game-managed '<Managed>'            # places the pinned 0Harmony 2.4.2 the probe compiles against
python tools/perf-union-probe/check_scopes.py               # PASS, the expected-scope hash and counts
python tools/perf-union-probe/build_probe.py --game-managed '<Managed>' [--output build/perf-union-probe]
python -m unittest discover -s tools/perf-union-probe -p test_*.py
dotnet run -c Release --project tools/perf-union-probe/logic-check/LogicCheck.csproj -- --expect-hash <hash> [--perf]
```

`build_probe.py` runs `check_scopes.py` first and refuses to build on any mismatch. References are the game's `mscorlib`, `System`, `System.Core`, `netstandard`, `BigAmbitions`, `BigAmbitions.ModAPI`, `HGExtensions`, `HGPlugins`, `UnityEngine.CoreModule`, `Newtonsoft.Json` and the pinned `0Harmony.dll` (SHA-256 checked). There is no `PersistentCity.dll` reference: the probe binds Persistent City by reflection, so one artifact serves both modes. The compile is deterministic, and the manifest holds names and SHA-256 values only, so two builds of the same inputs give byte-identical DLLs and manifests. The manifest also records the expected-scope hash; the probe's `status` reports the same hash, computed from the same canonical text in C#.

The reference arm runs with Persistent City removed. If no other staged mod loads Harmony, stage the same pinned `0Harmony.dll` next to the probe; `status.harmony_version` reports the loaded version.

## Expected scopes and `check_scopes.py`

`ScopeInventory.cs` lists:

- **58 patches**: the 35 rows of the F4 plan's section 2 inventory and the 23 food-contract postfixes that E5b2 (#168) and E5b3 (#171) added after the plan. Owner, kind, patch method, native target and file.
- **5 MonoBehaviour messages**: `PopulationRuntime.Update`, `OnGUI`, `OnDestroy`; `PhysicalPedestrianCrosswalk.OnTriggerEnter`/`OnTriggerExit`.
- **8 native event subscriptions** (main-thread entry points outside Update): `GlobalEvents.onSaveGame`, `onGameUnloaded`, `onEnterBuilding`, `onExitBuilding`, `onTimeMachineStarted`, and `SceneManager.sceneLoaded`/`sceneUnloaded`. The plan's section 2 does not list these; the probe times them so the union stays complete.
- **3 transpiler helpers**: `SelectIdle`, `ApplyBodyForce`, `ApplyBodyRotation`, which the two transpilers insert into native code.
- **11 components** and **13 call sites**: Decision 5's components plus F2e's player-hold sites `ApplyHoldReplay` (in `UpdateCity`) and `ObservePlayerContact`/`PlayerContact.TryReadPlayer` (in `PhysicalPopulation.Update`).

`check_scopes.py` scans `Core/`, `Compatibility/`, `Persistence/` and `Runtime/` with comments and string contents blanked. It derives every `Patch(` call (direct `HarmonyMethod`, same-file helper `Patch` methods, and the food-contract `typeof(T).GetMethod("HookN")` form), the Harmony owner of each file, MonoBehaviour messages, native event subscriptions and transpiler helpers, and compares them with the inventory in both directions, as multisets that include each patch's native target: a callback reused on another target, a changed target or a second registration is a difference. Targets are resolved from the `Patch(` call's first argument, following locals, fields and `out` assignments and substituting helper parameters; an unresolvable target fails. A helper `Patch` method must register exactly once (one `Patch(` call and one `HarmonyMethod`); the dynamic food-contract call must register exactly the table binding (one `Patch(` call whose target is a local assigned once from `Bind(hook, ...)`), otherwise the check fails. Inside each inventoried transpiler, every `AccessTools.Method`/`DeclaredMethod` or `typeof(T).GetMethod` reference must resolve to `(type, "Name" | nameof(...))`, and a PC type there must be an inventoried helper; any other method-reference form (delegates, `CodeInstruction.Call`, function pointers, other `AccessTools` method lookups, `GetMethods`/`GetMembers`/`GetMethodFromHandle` lookups, cloned instructions) fails, and every `operand =` the transpiler writes must be one of its recognised reference locals. Each component must still be defined once in its file, and each call site must still sit in its caller. It fails closed on anything it cannot classify: an unrecognised `Patch(` or `HarmonyMethod` form, `PatchAll`, attribute patches, reverse patches, IL manipulators, a lambda event subscription, a coroutine, a `RuntimeInitializeOnLoadMethod` or a player-loop change. When F8's hook inventory exists, the check should read it instead (not done here).

## Rig op

Call through the rig ops extension convention: `{"rig":{"op":"perf_union","action":"...",...}}`. Every reply is `{before,after}`; a refusal is `after.supported:false` with a stable `after.refusal` code. The probe requires `-agent-tests -rig-test`, the isolated Agent Tests profile, a loaded city whose save name starts with `FT`, and dispatch on the arm thread. Replies are bounded at 48,000 characters; page with `offset`/`limit`.

| Action | Arguments | Meaning |
| --- | --- | --- |
| `inventory` | | Version, expected-scope hash, owners and counts. |
| `status` | | Mode, coverage, counters, flags, limits. Fields that do not apply to the mode are `n/a`. |
| `arm` | `mode` `candidate` or `reference`; `expected_scope_hash`; `factor` (reference only) | Binds and patches. No player business is needed. Refuses during a native load and when the hash differs. |
| `measure-start` | `window` (label) | Clears buffers. The window starts at the next frame boundary. |
| `measure-stop` | | Writes the stop frame as a partial, diagnostic row and closes the hold window. |
| `page` | `section`: `frames`, `ticks`, `holds`, `calls`, `diagnostics`, `scopes`; `offset`, `limit` | `frames`, `ticks` and `diagnostics` can be drained while measuring: rows have absolute `index` values, and paging from `offset` acknowledges every earlier row. The other sections export after stop. |
| `heap` | | Managed heap after a forced full GC (G14). Refused while measuring. |
| `disarm` | | Unpatches everything, removes the player-loop entry and event handlers. |

**Candidate mode** (Persistent City active). At `arm` the probe patches every expected callback method itself, whether or not Persistent City has installed its native patch yet, so hooks Persistent City installs after arming (leaving a building starts the physical population and its traffic and ambient hooks) are timed:

- prefixes, postfixes and finalizers, messages, event handlers and transpiler helpers get an inclusive timing scope (`timed`);
- each transpiled method is listed as `untimed-transpiled`, because its code runs inside the native method;
- a row that cannot be bound or patched is `unbindable`.

Harmony's patch info (every patch-list field of `Patches`: `Prefixes`, `Postfixes`, `Transpilers`, `Finalizers`, `InnerPrefixes`, `InnerPostfixes`; Lib.Harmony 2.4.2 exposes them as fields, not properties) is scanned at `arm`, at `measure-start`, once per real second while measuring (on the diagnostic frame) and at `measure-stop`. Each PC patch (an inventory owner or a patch method in the PC assembly) must be an inventory row on its expected native target, registered once; anything else is `unexpected`. An unexpected patch seen at any scan stays recorded for the rest of the capture, and `patch_set_changed_in_window` marks a window during which one appeared. `unexpected` or `unbindable` make `coverage_complete` false, and the gate refuses the window. An expected patch absent at the last scan is listed in `not_installed` (for example the Driven Rides adapter without Driven Rides); `scopes` rows carry `installed`.

Components are timed as nested sub-scopes for attribution. A component the probe cannot patch is `component-unbound`; the union stays authoritative.

**Reference mode** (Persistent City removed). `arm` refuses if the PersistentCity assembly or any inventory owner is present, because a second calendar factor would distort the reference. The probe installs only its pace shim and the frame, GC, QPC, save and scene recording. Scope inventory, holds, per-call records and diagnostic sampling are `n/a`.

**Pace shim.** A prefix on `GameManager.RunMainGameTick(float)` at the same priority as `NativeCityClock.TickPrefix`. While the game is not loading and a time machine exists and is not running, a positive, finite delta is multiplied by `factor` (0.1 to 1). Zero, negative, infinite and NaN deltas are unchanged. It runs no Persistent City simulation and no calendar holds, so the reference keeps the declared unheld pace. It is the only probe action that changes native state; `status.pace_shim` reports it as `changes_native_state: true` with tick and multiplied counts.

## Records

- **Frames** (one row per Unity frame; a 131,072-row buffer holds a 20-minute window at the declared 90 FPS cap without draining, and longer windows such as the G14 soak drain while measuring, for example once per real minute): frame, QPC at the frame boundary, mod-work union ms, whole-frame ms, `observer_ms`, the probe's own boundary ms, Mono GC collections during the frame, save callbacks, scene load/unload, native loading, diagnostic flag, late boundary, partial, cross-frame, native and logical minute, and per-component ms. The boundary is a probe entry at the start of the player loop's `EarlyUpdate`; if it cannot be installed, `boundary_source` is `first-callback` and frames carry `late_boundary`.
- **Ticks** (262,144-row buffer, drainable, candidate): a prefix and postfix on `NativeCityClock.AccountingPrefix` read `HeldOrdinaryTicks`, `HeldEconomySkipTicks`, `BoundaryClips`, `WithheldMinutes` and `AccountingLimited` around every native tick. A tick is held only when a held counter rose across the call. `AccountingLimited` without that is a boundary clip, kept apart from holds. Falling counters (a clock restart) are a reset. Each row has the QPC time, withheld minutes this tick, and the native and logical minute.
- **Holds**: a `hold` or `resume` event on each transition inside the window. A hold in progress at the window's first frame opens there, flagged `carried_in`; a hold still open at the last frame closes there, flagged `ongoing`.
- **Calls**: one record per `AdvancePopulation` call of at least 30 ms (exact integer comparison of QPC ticks): duration, Mono GC delta across the call, optional-stop read ms (the `OptionalStopEnvironment.SeamMilliseconds` difference, `null` if the seam is unbound) and target minute. The slowest unit inside a call cannot be read from outside, because `Unit` is a local function; it is always `unknown`.
- **Diagnostics** (8,192-row buffer, drainable): once per real second, outside timed scopes, `AccountingDiagnostics` and `SaveEpochDiagnostics`; that frame is flagged diagnostic. These samples are for reporting and cross-checks only; S2-S4 come from the tick and hold records.

## Limits

These hold for every window and are repeated in `status.limits`:

- The union includes nested probe cost, and `observerMs` measures only part of it (the hold and per-call bookkeeping, not the scope prefixes and finalizers). G2 measures the native share; it is not subtracted.
- Native work that PC forces inside a scope stays in that scope. Native work outside PC scopes is not mod-work.
- Transpiled methods are `untimed-transpiled`; only their separate PC helpers are timed.
- A method the Mono JIT inlined into its caller is not observed by a Harmony patch, and the probe cannot detect that. A component it cannot patch is `component-unbound`.
- Components are attribution only. They are never summed or subtracted from the union or whole frames.
- `wholeFrameMs` is wall time between player-loop frame starts, not CPU or GPU time. Presented frames come from the presentation trace (F4c).
- Unexpected, unbindable, off-main, cross-frame, overflow (`*_dropped`: a full buffer drops new rows and counts them, never overwrites undrained ones), error or save/scene flags invalidate a window; the analyzer (F4a) applies that.
- Any per-frame or per-call field the probe could not bind is `unknown` or `null`, never inferred.
- Offline numbers (docs/perf/f4b.md) come from a .NET stub loop and are not game evidence.
