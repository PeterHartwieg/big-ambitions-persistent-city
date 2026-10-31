# Sync the F0 base into A1 while preserving merge ancestry

PR #4's A1 inspector branch diverged from `codex/persistent-city` when F0 (#8, `7a344a7`) landed. Merge that base into `codex/pc-a1-sync-base`, starting from A1 `fbb5989` (including the address formatter fix #11), with a real two-parent merge. This PR targets `codex/pc-a1-inspector` and must itself land with a merge commit so the base ancestry survives.

Compared both parents against merge base `7a6153cfa9bb88e28dea905b285b4273c3e268c7`. Conflict resolutions:

| File | Resolution |
|---|---|
| `CHECKS.md` | Retain all A1 inspector, traits, address-formatter evidence and native address checklist, plus all F0 evidence, review fixes and native checklists. |
| `README.md` | Retain both A1 and F0 status sections and F0's qualified indoor-travel description. |
| `tools/build.py` | Union the assembly references: F0's `BigAmbitions.InteriorDesigner` and A1's `UnityEngine.UI`, `UnityEngine.InputLegacyModule`, `BigAmbitions.PlacementSystem`. Retain all source globs, pinned dependencies and packaging steps; use F0's checked PATH/local-host discovery and empty-SDK diagnostic. |
| `tools/native-command-regression/run.py` | Retain the complete existing extraction, harness sources, checks, Release invocation and argument forwarding; use F0's checked PATH/local-host discovery. |
| `tools/physical-selection-regression/run.py` | Retain the existing differential harness plus F0's wiring, full-pool comparator and performance checks; retain all generated/compiled sources and references, including `InteriorViewState`, `PrototypeSettings` and Newtonsoft.Json. Use F0's checked PATH/local-host discovery. |

Reviewed the auto-merges in `Runtime/PersistentCityMod.cs` and `Runtime/PhysicalPopulation.cs`: F0's start/restore/save/failure, admission/retention/cap and outdoor-construction changes coexist with A1's inspector update/suspend/reset/detach and read-only picking. No extra runtime or integration fix was needed. No new behavior beyond the two parents.

Validation: **31/31 required offline commands pass**: production build against installed Managed assemblies (warnings as errors), **1,618/1,618 unit tests**, test discovery, all 21 standalone regressions, all four Python harnesses and all three demos. All completed sequentially in the foreground with the prescribed dotnet guard before each command. Build/native-command/physical-selection use the original PATH; vehicle-ownership/optional-stop receive the local SDK directory on their child PATH. Native-command passes 72 checks; physical-selection passes 1,938 F0 wiring and 27,568 differential checks; startup-clock passes 1,057 F0 lifecycle and 17 clock checks. Save-contract bytes remain identical off/on across twelve transitions. Checked both parents' documentation additions and compilation/reference unions; `git diff --check` passes. DLL SHA-256: `5b71efdd6a70b1100c326a492580cfe06673f8e9fa52e79ef2ddd7a02eb8d074`. Logs and per-command exit codes are ignored under `build/sync-base-gate/`; the durable summary is in [CHECKS.md](../../CHECKS.md#a1-base-sync-after-f0---31-october-2026-offline-only).

The separate native rig_test gates recorded for A1 and F0 remain open. This task performs offline validation only; no game, rig/profile/lease, Steam/Workshop, deployment, push or PR publication action.
