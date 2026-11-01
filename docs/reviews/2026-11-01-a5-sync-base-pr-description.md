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

Pending foreground gate completion; results will be recorded before commit. No game, rig, bridge lease, Steam/Workshop write or ordinary-profile operation. Under merge rule v2, native checks await the batched combined locked rig on the final development head; pointer/input acceptance waits for Peter's 6 November playtest. This sync does not claim release approval or new native performance proof.
