# Sync approved A5 with Persistent City schema 20

Merge development `5877453` into approved A5 `f3eb16b` using a real two-parent merge from common ancestor `51ce709`. Target: `codex/pc-a5-integrated-acceptance`; retain this ancestry with a merge commit. No new feature or production behavior change is introduced by this sync.

## Conflict resolutions and preservation

- `CHECKS.md`: retain both A5 and E1 entries, F1's automatically merged entry and every existing development index line. Keep A5's detailed evidence and all 12 numbered rig checks in `docs/checks/a5-integrated-acceptance.md`.
- `README.md`: retain A5's integrated acceptance notice and F1's drawing NO-GO notice; update the current source schema from A5's 19 to E1's 20 and include migration from 19.
- No other files conflicted. The roadmap retains A5's status/perf entry, E1's schema-20 status/perf entry and F1's stop entry. All slice plans/checks/perf files and tests/tools from both sides survive. Both sides' conflict diffs were inspected against `51ce709`.

## Schema and acceptance integration

A5 migration tests now use E1's genuine pre-weather schema-19 projection to construct 18/19 input, explicitly assert migration from 16/17/18/19 to 20 with neutral weather and next-day activation, and preserve exact old facts/source bytes. A5's active B/C2a inspector/choice test retains bounded quotes, read-only payloads, exact replay and one checkout while exercising rain/dry/unknown capture and changed weather/settings after held-decision reload.

No A5, E1 or other slice production Core/Runtime/Persistence code changed during resolution. `Core/ResidentInsight.cs` is identical to approved A5, and the remainder of production code is identical to development `5877453`. No minimal production integration fix was needed.

Original A5 timing/save-size measurements remain unchanged and explicitly labeled as measured on `51ce709` plus A5 `bcc3b00` **before E1**. They are not schema-20 performance evidence. The summary script adds a separate sync mode validating the exact E1 schema-20 stable ledger and the historical A5 schema-19 projection/economy; it does not overwrite the old report or loosen contract checks.

## Validation

On merged code `1aaecd5`, **2,392 unit tests passed, zero failed/skipped**, and discovery plus all **51 sequential foreground gate commands passed**: production build against the provided game assemblies; all 36 top-level .NET regression projects (including A5 inspector and E1 weather); all four Python regression launchers; simulate, food-routines and food-demand demos; A5 compatible/integrated choice and 957/5k rush modes; save benchmark. The additional nested pickup regression passed all 25 helper checks. The prescribed dotnet guard ran before every test/run or launcher; build-server reuse was disabled. Production DLL SHA-256: `e4a9bbdee0c698b264d85aab807cc7b8813f182e4731961d4bc45a264d09da07`.

`summarize.py --sync-base` passes the exact E1 schema-20 ledger (120,184 characters, digest `39D2E48CD937D1BDD8E183CDB6125461FB44B67784DA935258CC88EC2BC1CAF0`) and the historical A5 schema-19 projection (119,083 characters, digest `D7FF8294F56C3B3D5AF736540274E68E9848319B34658859FC15E0D4CDB1F3A2`), all stable refusal text and all exact economy/order/reload digests. Allocation counters are diagnostic. Rerun inspector/choice/rush flags report no ≥30 ms samples in their measured matrices; separate ≥30 ms save-operation medians remain flagged, and historical pre-E1 spikes remain recorded. This is not native mod-work or whole-frame proof, nor a causal speedup claim.

[Gate results/log hashes](2026-11-01-a5-sync-base-results.json), [pickup proof](2026-11-01-a5-sync-base-pickup.json), [content preservation audit](2026-11-01-a5-sync-base-preservation.json), [owning A5 checks and 12 numbered rig items](../checks/a5-integrated-acceptance.md). No game, rig, bridge lease, Steam/Workshop write or ordinary-profile operation. Under merge rule v2, native checks await the batched combined locked rig on the final development head; pointer/input acceptance waits for Peter's 6 November playtest. This sync does not claim release approval or new native performance proof.
