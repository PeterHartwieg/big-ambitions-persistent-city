# Independent review round 2 triage
HIGH_MEDIUM_FIX: 1

| ID | Severity | PR | file:line | Finding | Raised by (sol/astra/opus) | Decision (FIX / SKIP / QUESTION) | Reason |
| --- | --- | --- | --- | --- | --- | --- | --- |
| R2-01 | Medium | #2 | Runtime/NativeFoodMarket.cs:312; Runtime/FoodBusinessCatalogue.cs:87-91 | Invalid price in the first live checkout read throws and strands held funds as an uncertain meal. | opus | FIX | A recognized range/precision failure occurs before any receipt or sale; return a definite refusal using a dedicated exception type, preserving unrelated failures. Cover both early and post-setup mutations. |
| R2-02 | Low | #1 | tools/event-observation-regression/Stubs.cs:198-205 | Absolute read counts can retarget injected observer faults. | opus | SKIP | No demonstrated mistargeted seam or missing coverage; direct finalizer tests additionally assert original exception identity. Instrumentation bookkeeping would add complexity without fixing a demonstrated regression. |
| R2-03 | Nit | #1 | CHECKS.md / README.md | Round-1 evidence is recorded in dedicated review/tool docs rather than repeated in top-level status files on #1. | opus | SKIP | The branch already contains its diagnostic evidence and limitations in docs/reviews/2026-10-28-event-attribution.md and the harness README. Duplicating it is a documentation-placement preference, not missing validation. |

Sol and Astra approve both PRs with no findings. Opus approves both with nits and identifies the one pre-existing Medium above. Three distinct findings; no product/design questions. PR #3 is excluded.

## Completion

R2-01 fixed in `c500d5b` ? Fix review round 2: refund invalid initial checkout prices. Pushed normally to `codex/pc-decisions-1`. No #1 fixes, so no parent-branch commit/merge was needed. The checkout harness now compiles the actual catalogue source; its ownership stand-in retains its previous stable test identity, and the discovery assertion reads the selected new business rather than the whole synthetic stale-address catalogue.

Validation: failure reproduced before fix; 314 checkout checks, 29 catalogue checks, 1,304 unit tests passed; Steam-DLL build with warnings as errors and git diff --check passed. Native financial verification remains pending. No new product questions.
