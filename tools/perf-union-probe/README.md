# F4b perf union probe

Status: implemented offline; native validation is pending (rig check G2 in the [F4 plan](../../docs/plans/f4-perf-gate.md), section 7). Refs #91. Plan: Decisions 3, 5 and 7.

A rig-only diagnostic DLL that measures the complete Persistent City mod-work union, per-tick calendar holds and per-call advancement spikes from outside the product. It is **never packaged**: `tools/build.py` does not build or copy it, and it never goes into the Persistent City package, the normal game or Peter's ordinary save. It adds no Runtime code.

## Files

- `Probe.cs`: the Unity DLL (rig op, inventory binding, scopes, frames, holds, per-call records, pace shim).
- `ScopeInventory.cs`: the expected entry points, one row per line. Unity-free.
- `ProbeLogic.cs`: pace-shim rule, hold state machine, per-call threshold, union clock, coverage and buffers. Unity-free.
- `HarmonyScan.cs`: reads Harmony's patch info into plain observations.
- `check_scopes.py`: fails when source and `ScopeInventory.cs` differ in either direction.
- `build_probe.py`: builds `PerfUnionProbe.dll` with pinned references and writes `build-manifest.json`.
- `test_check_scopes.py` and `logic-check/`: Python and .NET checks for the above.

## Build and checks

```powershell
python tools/build.py --game-managed '<Managed>'
python tools/perf-union-probe/check_scopes.py
python tools/perf-union-probe/build_probe.py --game-managed '<Managed>' [--output build/perf-union-probe]
python -m unittest discover -s tools/perf-union-probe -p test_*.py
dotnet run -c Release --project tools/perf-union-probe/logic-check/LogicCheck.csproj -- --expect-hash <hash> [--perf]
```

`tools/build.py` places the pinned `0Harmony.dll` the probe compiles against. `build_probe.py` runs `check_scopes.py` first and refuses to build on a mismatch. The probe has no compile reference to `PersistentCity.dll`; it binds Persistent City by reflection, so one artifact serves both modes. Two builds of the same inputs give byte-identical DLLs and manifests. The manifest and the probe's `status` report the same expected-scope hash.

`build_probe.py` also writes `stage/` next to the DLL: `PerfUnionProbe.dll` and the pinned `Dependencies/0Harmony.dll`, the layout of a `ModsLocal/PerfUnionProbe` folder. The rig ops extension discovery registers `perf_union` only once that assembly is loaded, so a rig without the staged folder answers `ops_unknown_op`.

First install on the isolated Mac profile (`<mods>` is the absolute path `$HOME/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal`; a quoted `~` is not expanded), with the player stopped and the agentmac lease held:

1. If `<mods>/PerfUnionProbe` is absent, record that and run `mkdir "<mods>/PerfUnionProbe"`. `install-mac.py --stage-mod` refuses a destination folder that does not exist (`mod_scope`).
2. Run `BA_LOCK_OWNER=<run> python3 scripts/install-mac.py --stage-mod PerfUnionProbe <output>/stage` in the agent-rig checkout, with `<output>` an absolute path: `install-mac.py` does not expand `~`. `tools/rig-helpers/rigpaths.py` (`stage_mod_line`) prints the command with expanded paths. Keep the printed rollback line; it should be followed by `[AgentRigOps] staged_mod name=PerfUnionProbe` with the `PerfUnionProbe.dll` hash from `build-manifest.json`.
3. Start with `--rig-test`, load the FT fixture and check that `op perf_union {"action":"status"}` returns a status with no `after.refusal`. Then arm, and check that `after` has no `refusal` and `armed` is true before `measure-start`:
   - Candidate (Persistent City staged): `op perf_union {"action":"arm","mode":"candidate","expected_scope_hash":"<hash from build-manifest.json>"}`. Do not pass `factor`; the probe reads it from Persistent City. Record the candidate's `after.calendar_factor`.
   - Reference (Persistent City removed): `op perf_union {"action":"arm","mode":"reference","expected_scope_hash":"<hash from build-manifest.json>","factor":<candidate calendar_factor>}`. `factor` is required and must be between 0.1 and 1; without it `arm` refuses with `f4b_reference_factor_0_1_to_1_required`.
   - Then `measure-start` and `measure-stop`. `after.mono_heap` carries `start_bytes`, `start_used_bytes`, `stop_bytes` and `stop_used_bytes`.

Cleanup, with the player stopped: run the printed rollback (it restores the folder as it was at staging, which is empty on a first install, and the receipt). If step 1 created the folder, run `rmdir "<mods>/PerfUnionProbe"` so it is absent again, and check that `install.json` matches the backup hash.

The isolation check accepts the Windows profile (`Big Ambitions Agent Tests`, `%USERPROFILE%/AppData/LocalLow/Hovgaard Games/Big Ambitions Agent Tests`) and the Mac profile (`Big Ambitions Agent Tests Mac`, `~/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac`), both with `-agent-tests -rig-test`.

`check_scopes.py` scans `Core/`, `Compatibility/`, `Persistence/` and `Runtime/` for patches, MonoBehaviour messages, native event subscriptions, player-loop delegates, transpiler helpers, components and call sites, and compares them with the inventory, including each patch's native target. It fails closed on any form it cannot classify, and each transpiler body and target-resolver helper (`PINNED_RESOLVERS`) is pinned by fingerprint.

## Rig op

`{"rig":{"op":"perf_union","action":"...",...}}` through the rig ops extension convention. Every reply is `{before,after}`; a refusal is `after.supported:false` with `after.refusal`. It requires `-agent-tests -rig-test`, the isolated Agent Tests profile and a loaded `FT` city. Actions: `inventory`, `status`, `arm` (`mode`, `expected_scope_hash`, `factor` for reference), `measure-start`, `measure-stop`, `page` (`frames`, `ticks`, `holds`, `calls`, `diagnostics`, `scopes`), `heap` and `disarm`. `arm` needs no player business.

**Candidate mode** (Persistent City active). Every prefix, postfix, finalizer, message, event handler, player-loop delegate and transpiler helper gets an inclusive scope. Each transpiled method is `untimed-transpiled`, because its code runs inside the native method. A row the probe cannot bind is `unbindable`; a PC patch missing from the inventory is `unexpected`. Either makes coverage incomplete, and the gate refuses the window.

**Reference mode** (Persistent City removed). `arm` refuses if any Persistent City assembly or inventory owner is present. The probe installs only its pace shim and frame, GC, QPC, save and scene recording; PC fields are `n/a`.

**Pace shim.** A prefix on `GameManager.RunMainGameTick(float)` that multiplies ordinary positive finite deltas by `factor`, so the reference keeps the declared unheld pace. It is the only probe action that changes native state; `status.pace_shim` reports it.

## Records

- **Frames:** QPC boundary, mod-work union ms, whole-frame ms, `observer_ms`, GC count, save and scene flags, native and logical minute, per-component ms.
- **Ticks and holds:** read around `NativeCityClock.AccountingPrefix`. A tick is held only when a held counter rose; `AccountingLimited` alone is a boundary clip. Hold events carry `carried_in` and `ongoing` at window edges.
- **Calls:** every `AdvancePopulation` call ≥ 30 ms with duration, GC delta and optional-stop read ms. The slowest unit inside a call is always `unknown`.
- **Diagnostics:** `AccountingDiagnostics` and `SaveEpochDiagnostics` once per real second, on flagged diagnostic frames.

## Limits

These hold for every window and are repeated in `status.limits`:

- The union includes nested probe cost, and `observerMs` measures only part of it. G2 measures the native share; it is not subtracted.
- Native work that PC forces inside a scope stays in that scope. Native work outside PC scopes is not mod-work.
- Transpiled methods are `untimed-transpiled`; only their separate PC helpers are timed.
- A method the Mono JIT inlined into its caller is not observed by a Harmony patch. A component the probe cannot patch is `component-unbound`. Components are attribution only, never summed or subtracted.
- `wholeFrameMs` is wall time between player-loop frame starts, not CPU or GPU time. Presented frames come from the presentation trace (F4c).
- Unexpected, unbindable, off-main, cross-frame, overflow, error or save and scene flags invalidate a window.
- Any field the probe could not bind is `unknown` or `null`, never inferred.
- Offline numbers come from a .NET stub loop and are not game evidence.
