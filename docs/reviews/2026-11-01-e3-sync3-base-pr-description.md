# E3 sync3: merge B2 into the approved donut feasibility branch

PR #23 conflicts after B2 #19 lands on development. Merge `origin/codex/persistent-city` at `81472e637ee058b337a849f4e931f6d80fa4ad44` into E3 parent `fb46f38430850dd05794d911cc47c32b3b5832d0`, using merge base `7053a281cfa4d8fb70a0d8cf4e10b056630cfa95` and a real two-parent merge. This integration targets `codex/pc-e3-first-product` and must land with a **merge commit**; the harness handles pushing and opening the PR. PR #23 can then be squash-merged into development.

Conflict resolution and integration only. **No E3 C# code changed:** the approved reader, compiled regression harness and project remain byte-identical to `462a99d`. All incoming production, tests, regression tools and configuration remain byte-identical to development. There is no additional product, gameplay caller, native write or performance-affecting implementation change.

## Conflict resolution and preservation

Both sides' diffs against the merge base were read for the sole conflicted file, `CHECKS.md`, and the automatically merged roadmap.

| File | Resolution |
| --- | --- |
| `CHECKS.md` (sole content conflict) | Keep both complete index lines, B1 then B2 then E3, each once. Keep the split docs layout. E3's entire existing evidence and five numbered native checks stay in `docs/checks/e3-donut.md`, with every native/input-only marker and qualification intact. This sync's evidence goes into that owning slice file. |
| `docs/roadmap.md` (automatic merge, reviewed) | Keep incoming B2 implementation/default-threshold status, B1's protected-insertion exception, the shared performance gates and every performance index, including both B2 and E3. |
| Other files | No conflicts in README, plans, config, source, tests or persistence. Preserve all incoming B2 files and all existing E3 records, checks, performance measurements and tools. |

The per-merge whole-tree audit checks all **1,179 incoming tracked files**. All **499 incoming source/test/tool/config files** and all **26 incoming checks/performance files** match their incoming Git blobs. Only the existing E3 additions to CHECKS, the roadmap and `docs/plans/steps-b-e.md` differ among incoming paths; all incoming lines in those three files survive in order. E3's earlier owning checks content survives verbatim, and its earlier performance file is unchanged. No checks, migration/refusal cases, tests or sections are dropped.

**Save schema:** neither E3 nor this integration adds persistence changes. Incoming `Persistence/` and `Core/Model.cs` are unchanged; the schema remains **18**, with no allocation, new saved field or migration rule. There is no schema conflict.

## Minimal integration fix: make the E3 guard survive the planned squash

The standing guard already avoids a moving-development pin, but still asks Git to resolve three blobs at the historical E3 commit. That commit is absent from development ancestry and need not exist in a fresh clone after PR #23 is squash-merged. A temporary combined-source Git snapshot without feature commit objects reproduces red: `git rev-parse 462a99d:Runtime/DonutFeasibilityReader.cs` fails.

Change only `tools/donut-feasibility-regression/source_checks.py` to embed the SHA-256 values calculated from those same three approved blobs and compare canonical LF bytes, normalizing Windows checkout CRLF. Keep the original commit ID as provenance. No production edit and no re-pin of the implementation. API/assignment/caller isolation checks remain unchanged. Update the E3 plan to explain this history-independent contract.

**Eleven guard probes pass:** no historical commit objects, LF checkout, CRLF checkout, unrelated future Core text, rejection of a new gameplay caller, forbidden native API, native assignment and each of the three approved-file mutations, then restored combined snapshot. All probes operate in a temporary copy; no tracked source is mutated. Whole-tree preservation remains a separate per-merge audit, so future base work does not stale the standing guard.

## Validation

All **43/43 foreground gates pass**, covering every requested command: the Steam-reference production build with warnings as errors, **2,142 unit tests / 2,142 discovered cases** (zero failed/skipped), all **30 top-level Release regression projects**, nested pickup (**25 checks**), the four Python harnesses, all three demos with the requested arguments, E3 source isolation and optional-stop `--performance --findings`. E3 passes **2,082 reader assertions**. The eleven independent guard probes also pass. [Commands, exits, log hashes, measurements, red/green probes and preservation audit](2026-11-01-e3-sync3-base-results.json).

Save-contract output remains schema **18**, **119,083 characters**, digest `018FC26676937FEEF88B7D06AE32B522CCE93A1CC265BCDD87A08C7DD91FBB56`, **849 orders / 797 purchases / two unconfirmed**; all twelve F0 state/save/reload transitions pass. Incoming persistence, the save harness and all migration/refusal tests are byte-identical. This is the merged-tree run, not a separately executed incoming-base comparison. B2 reports zero saved-byte growth at both 957 and 2,500 residents. Production DLL SHA-256: `e450af412737b2b20e9ef3130b7ca71831d1143d62ce57ca6442bf1a6ac48367`.

**Offline timing flags:** D1's cold 20,000-place aggregate **41.5174 ms** is flagged across bounded units. C1 1,024-business cold/warm/continuation p95 is **6.9734 / 2.2826 / 0.0015 ms**. Optional-stop 957-resident admission p95 is **8.8098 / 12.0946 ms** at 25/100%. C1 and all optional-stop timing arms report zero >=30 ms samples. E3 cold read is **1.6256 ms / 968 B**; 2,000-entry warm p50/p95/max is **0.0962 / 0.1494 / 1.3760 ms**, **49,104 B/read**, zero >=30 ms samples. B2's reported benchmark batches have zero >=30 ms spikes; their percentiles are normalized batch means, not individual-call or native frame percentiles. No new performance win or native timing clearance is claimed. The native mod-work p95 <5 ms gate, >=30 ms spike flagging, separate whole-frame evidence and all four parked hotspots remain unchanged.

Every workload ran sequentially in the foreground after the prescribed other-dotnet guard, with build/compiler-server reuse disabled and no execution deadline. All invoked workloads completed. Full logs and task-local audit/probe scripts remain ignored under `build/e3-sync3-base-gate/`. The integration whitespace check against incoming development passes; the first-parent diff inherits B2's unchanged historical red-test log CRCRLF warnings, which are preserved as received.

A final `git fetch origin` still resolves development to **81472e6**, now an ancestor of merge commit **29db576**. C2a has not landed. The combined tree is current against that fetched head.

## Native evidence and scope

Under merge rule v2, in-game checks run later in a batched combined locked `rig_test` session on the development head. Pointer/input-only checks remain for Peter's 6 Nov playtest. All earlier E3 native checklist markers remain in the owning checks file. This integration provides no release or ordinary-save deployment clearance.

No game launch, rig/profile/bridge lease access, Steam/Workshop writes, WSL distro, campaign purchase, in-game money movement, RLO edit, push or PR publication. The production build reads the supplied Steam assemblies in place.
