# Gate-access performance review rounds — 21 October 2026

Peter requested the established panel: GPT-6 Astra, GPT-6.1 Sol, Claude Opus 5.5 and Grok 4.7. Each reviewed its own frozen source copy and the bounded change against the pre-index baseline. No reviewer modified production source, ran the game or changed a save. Astra and Sol could run offline checks in their copies; Opus and Grok used read-only file tools.

All four reviewers approve the final source after two rounds. No blocking findings remain.

## Round 1

Frozen source set: `b6141455eb857adf7b30057782384866c09fc3c9df6c0e5f7e8c116ab7517bed`.

| Reviewer | Verdict |
| --- | --- |
| Astra | Approve, two optional improvements |
| Sol | Approve, two optional improvements |
| Opus 5.5 | Request changes to validation/evidence; baseline access also failed |
| Grok 4.7 | Approve, optional benchmark-table improvement |

The review found no demonstrated gate-decision regression. It did find meaningful weaknesses in the evidence:

- The original 80 route comparisons never encountered a gate: the small gate boxes lay between the road grid lines. The revised fixture places gates on edges and asserts actual denials. A separate case proves the expected detour differs from an ungated route, blocks both paths, and restores the detour after a gate is removed.
- The candidate-budget fallback lacked individual blocked/clear comparisons. New default checks force a fallback after two scanned buckets and check both outcomes, including the equality boundary.
- Benchmark aggregate counts could conceal cancelling answer differences. Every request is now compared individually before timing. Documentation identifies the reference as a harness implementation of the old scan and limits allocation measurements to .NET 10.
- Unchanged refreshes constructed and discarded a replacement grid. Capture now precedes comparison; only initial or changed snapshots build an index, before publication. The retained index and moving-leaf envelopes keep their prior behavior.

Opus initially could not read the baseline outside its working directory. Round 2 includes baseline, diffs, response, manifest and evidence inside each reviewer's directory. Sol's initial capacity error was retried successfully. Grok's startup model-cache warning was checked against session metadata (`grok-4.7`); its usage record identifies the backend as `grok-4.7-build`.

## Round 2

Frozen source set: `7c956ee55f55786e6c081e7599f6eff27a49a802ce91bf4a6f4fa9c567cb1414`.

| Reviewer | Verdict |
| --- | --- |
| Astra | Approve; all earlier concerns resolved |
| Sol | Approve; all reported improvements addressed |
| Opus 5.5 | Approve; documentation/evidence suggestions only |
| Grok 4.7 | Approve; optional benchmark-table improvement |

Astra and Sol independently verified all 145 manifest file hashes and ran the revised gate harness: **213,884 checks passed**. Opus and Grok inspected the baseline, full delta, revised source, tests and supplied logs; they did not execute tests or recompute hashes. Grok’s final usage record again identifies `grok-4.7-build`, with `grok-4.7` selected in session metadata.

The remaining evidence suggestions from Opus and Grok were addressed by retaining a standalone default-harness log, repeating the two native-reference compilations with explicit Managed paths and SHA-256 manifests for every reference assembly, and adding the worst measured overlapping-gate row to the performance table. That row takes about 17% more query time (0.85× speedup). These supplements were added after the reviewer copies were frozen, so their verdicts refer to the original supplied evidence; the build-input manifests resolve the reference-provenance limitation noted by both reviewers. These are evidence/documentation changes; all 108 production/test C# and project files still match the round-2 freeze.

The suggested guard against a future unindexed publication path was not added: both current changed-snapshot returns and the public constructor build the index before publication. Reviewers verified these paths. This is a future-maintenance suggestion, not a demonstrated current failure. The fallback regression deliberately asserts externally visible decisions rather than requiring a particular internal branch count.

## Verification and delivery boundary

- **869 existing tests**, **213,884 gate/route checks**, the road-handoff/sweep/gate harness and **602 speed/persistence assertions** pass.
- The benchmark adds **30,150** individual-answer, aggregate-result and allocation assertions, for **244,034** checks in that run. The [performance report](2026-10-21-gate-access-performance.md) gives the synthetic measurements and their limits.
- Mac and retained Windows-reference builds produce the identical DLL SHA-256 `3f87e7f927be074ca649f7e1403bf803ac146cb62c35528655bc36877be8f0a9`, using C# 9 with warnings as errors.
- The reviewed disabled package is `build/gate-access-review-20261021/PersistentCity-gate-index-reviewed-development.zip`. Its build manifest records the reviewed source set, DLL identity and later documentation amendments.
- Raw prompts, frozen manifests, baseline/current copies, diffs, model results and validation evidence are retained under ignored `build/gate-access-review-20261021/`. The initial baseline and implementation remain under `build/gate-access-20261021/`.

This review covers the bounded source change. It does not establish native frame-time/FPS gains, Unity/Mono allocation behavior, physical movement acceptance or deployment readiness. Distribution packages remain disabled by default. After review, Peter authorized [installation of this exact DLL in both PC profiles](2026-10-21-gate-access-pc-install.md), preserving their enabled settings; this adds file-verification evidence only.
