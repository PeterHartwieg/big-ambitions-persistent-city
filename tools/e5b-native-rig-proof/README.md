# E5b native rig proof probe

Status: partial. Transport, readback and fresh fixture creation passed native checks; E5b behaviour validation is pending. Gate timing now uses the F4b probe in `tools/perf-union-probe/`.

This is a separate rig-only DLL. Never add it to the Persistent City package, the normal game or an ordinary save. This tool is not proof that any E5b native assertion passes.

## Tools

- `build_probe.py --game-managed <Managed> --candidate <PersistentCity.dll> --harmony <0Harmony.dll> --output <dir>`: builds from local references only and writes a build manifest. It installs and launches nothing.
- `make_flow.py`: writes a discovery and readback flow; it runs nothing. `--business 'STREET#N=Exact name'` repeats up to eight times.
- `run_flow.py FLOW.json --evidence <new dir>`: runs the flow through the rig session. It captures the isolated `run.json`, `PlayerSettings.json` and HKCU product key before start and restores them exactly after the player exits. It sets `HARNESS_ALERTS=off` and keeps every selfcheck result, including failures.
- `produce_fixture.py --name 'FT E5b UNIQUE' --mods <mods.json> --evidence <new dir>`: creates a fresh native city through the bridge `new_game`. It backs up existing isolated saves and restores any change to them. Native shop construction refused at `UpdateGroundItemPosition`; keep that refusal and never seed equipment or cargo to bypass it.

## Rig op

`{"rig":{"op":"e5b_proof","action":"...",...}}`. Every reply is `{before,after}`; a refusal is `after.supported:false` with `after.refusal`. Transport `ok:true` alone does not mean the action succeeded. Replies cap at 48,000 characters; page with `offset`/`limit`. No mutation is replayed after an uncertain reply.

Actions: `discover`, `runtime`, `placement-facts`, `arm` (1–8 selected player businesses), `status`, `snapshot`, `read`, `capture`, `replay-paid`, `events`, `measure-start`/`measure-stop`, `frames`, the `fault-close`/`fault-price`/`fault-cargo` negative tests, `restore` and `disarm`.

Fault actions inject negative contract state on a paused checkpoint; they cannot establish natural depletion or refill. `restore` refuses if native state changed the faulted list; then stop the player and discard the disposable copy. Never report that as an exact restoration. The probe never writes native saves or manufactures ownership, people, orders or positive eligibility.

## Limits

- Off-main-thread callbacks, overflow, callback errors and cross-frame scopes invalidate evidence.
- Frame mod-work is the nested union of the bound PC scopes and includes nested probe cost; `observerMs` measures only part of it. Whole-frame wall intervals stay separate, with no CPU or GPU attribution.
- `analyze.py` reports distributions and every ≥ 30 ms flag; it never certifies native behaviour.
- Autosave off, the `-rig-test` guard, idle .NET work and restoring rig files are session prerequisites. The probe's status does not prove them.
