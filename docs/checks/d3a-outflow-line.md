# D3a: free outflow history/line and rival-core acceptance

## 1 November 2026 — offline implementation

Task `pc-d3a-outflow-line`, branch `codex/pc-d3a-outflow-line`, base `c85dc19` on the existing schema-23 integration. Implementation commits `7cd4809` and `88551e8`; final gates use `88551e8`.
D3a adds a free fourteen-day total amount/count line to the existing Ctrl+F8
food panel and D2's on-request bridge diagnostics. It needs neither B1 memory
nor campaign/research providers. Paid research remains off; no agency UI,
purchase, entitlement, named rival bucket/report or market-share output is added.

Rivals remain real read-only alternatives under D2. No native rival write,
receipt or checkout is added. Native accounting stays authoritative. The line
labels modeled spending and estimated availability, unknown stock/capacity,
unknown historical days, the partial first day, and covered days with zero.
It does not claim that every rival purchase would otherwise have been a player
sale. Exact amounts are displayed without manufacturing historical numbers.

D2 cannot supply dated history from its lifetime ledger/current-day outcomes,
so [additive schema 24](../population-schema24.md) is necessary. Each validated
predecessor migrates through exact schema 23; its existing external account is
the unknown historical baseline. Fresh confirmed D2 settlements update history
inside the same atomic debit and reuse its saved paid watermark. Repeated or
older meal outcomes never add history. Retained plus expired amounts plus the
baseline must equal D2's account exactly. Counts exclude unknown pre-history.
Fourteen dated rows plus one rollup are bounded at **1,037 property bytes and
15 charged records per food city**, zero per-consumer cost. No identity,
menu, native receipt or old tuple format changes. Sibling D4 remains independent.

The new tests cover lifetime/retained reconciliation, fourteen-day rollover,
late earlier-meal and expired-day settlements, repeated outcomes/payment calls,
reload replay, nonzero old-ledger migrations from 21/22/23, fractional price
precision, unknown/partial/zero, missing/forged/foreign/future contracts and
store/session source preservation. Real D2 represented consumers reload with
memory inactive and campaigns absent, with zero native buys. A type/serialization
guard permits only free totals/date/known fields and rejects any named aggregate
or market-share field. Cache invalidation preserves prior immutable snapshots.

Foreground gates are reproduced with `python tools/run-d3a-checks.py`. Every
test/run (including Python wrappers that invoke dotnet) first invokes Peter's
other-dotnet wait; commands run sequentially, without execution deadlines or
detached workers. Steam assemblies are read-only compilation inputs. The game,
agent test cities, ModsLocal, bridge profile and rig lease were not accessed.

Final validation: **62/62 foreground commands passed**, including the required
game-assembly build, **2,528/2,528 unit cases**, zero-exit test discovery (2,466
listed names, with executed theory cases counted separately), all existing
standalone regression tools, requested Python wrappers/demos, additional product
source guards and C4/E4 save/economy comparisons. Core source is `88551e8`;
`6ab81d3` only extends the harness and makes C4's save-neutral assertion use the
current schema. Final built DLL SHA-256 is
`3dd427ca92bf392ee0a9012faf6698a9f6ca57c3b7dc4badab7b162a632b47fc`.
Artifact hashes and the reproducible command manifest are in
[the offline results](../reviews/2026-11-01-d3a-outflow-line-results.json).
The existing save regression retains **849 orders, 797 purchases,
107,967,424,757,760 spent units and two uncertain receipts**, with the original
player-order, economy and reload digests; its weather-stripped population
projection remains **119,083 bytes**. Offline doubles/regressions are not native
accounting or behavior proof.

Performance: final offline free-line closed/warm-open **0 B/request**; uncached control **14,336 B/request**. Cache before/after p95 **0.0198 →
0.0001 ms**. A 128-late-settlement/repeated-attempt burst p95/max is
**0.0543/0.2375 ms**; 128 rush settlements plus changed open text
**0.0687/0.1943 ms**, no D3a component spike ≥30 ms. Initial standalone rush
p95 was 0.1081 ms. Named fixture retained history is approximately **1,529 B
closed / 4,769 B open**, saved history **293 property bytes / 15 records** after
365 days. [Full p50/p95/max, control, allocation and limits](../perf/d3a-outflow-line.md).

Other regressions flag **one 31.5969 ms D2 whole-day 128-unit batch** and a
**32.2273 ms awareness whole-index build**. Neither has finer attribution or is
a native mod-work/frame sample. Combined native mod-work p95 <5 ms and every
mod-work spike ≥30 ms remain pending; whole-frame stalls/cost are separate.
The four parked hotspots remain parked.
The preceding gate's one 30.7708 ms D2 batch and one 34.0365 ms awareness
whole-build sample are also flagged; final rerun numbers above are separate.
The additional E4 regression flags **21/21 standalone serializations ≥30 ms
in each four/eight-product arm**: p50/p95/max **143.7052/202.0061/202.1156 ms**
and **156.7238/201.2296/205.8757 ms**. These whole-save offline operations are
separate from normal mod-work/frame cost; serialization remains parked.

## 1 November 2026 — round-one review gate (Sol-r1-1, offline only)

**MERGE BLOCKED: PR #72 must not merge until the authorized combined 6 Nov
`rig_test` session (Codex Sol, high) attaches verifiable before → red and
after → green evidence to the PR.** That proof must cover exact accounting
and native rival non-mutation, interrupted/repeated settlement replay, the
totals-only free-output boundary, and combined mod-work performance with the
free panel closed/open and during rush/late-settlement bursts. The numbered
handoff below defines the concrete checks.

Combined mod-work p95 must remain below 5 ms; flag every mod-work spike ≥30 ms
with its condition/component where attributable. Whole-frame cost and stalls
remain separate. Offline results above do not satisfy this native gate and do
not make the PR ready to merge. The review remains an evidence requirement;
this offline fix performs no rig action and claims no native acceptance.

Round-one verification on unchanged production code: **40/40 required
foreground commands passed**, including the game-assembly build,
**2,528/2,528 unit cases**, discovery, regressions, Python wrappers and demos.
The required other-dotnet wait ran before every test/run. This rerun flags
one **38.8685 ms D2 128-unit batch** and one **33.5306 ms awareness whole-index
build** as offline aggregate operations, with no finer attribution or native
mod-work/frame conclusion. Historical measurements above remain unchanged;
these passes do not remove the native merge block.

## In-game checks for 6 Nov rig

Pending combined-head evidence in an existing named disposable FT fixture.
Classifications follow [locked-bridge feasibility at 84be71a](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/84be71a/docs/reviews/rig/2026-10-31-locked-session-feasibility/README.md).
No rig work was done here. Record original autosave, disable it and verify off
before every measured window and after reload; manual saves stay outside windows.
Restore autosave, fixture/profile files and preferences during session cleanup.
Any window containing save activity is invalid as a clean performance sample.

1. **LOCKED-OK — free totals and exact debits.** On a prepared FT checkpoint, bridge-dump `RivalFoodDiagnostics.Core.FreeOutflow`, each consumer's D2 external-spend account/watermark and the simulation minute before/after rival service. Prove one price-unit debit and count increment; baseline + expired + retained units equals D2's ledger. Advance past fourteen days and compare the rolling free totals/zeros with retained rows; earlier unrecorded days remain unknown. No mouse/keyboard is needed for these dumps and advances.
2. **LOCKED-OK — native rival non-mutation and player control.** Independently bridge-read rival stock, owner, advertised price/hours, finance/customer/order state and receipt keys before/after modeled settlements. Prove no rival native checkout or receipt and no mod-written native field; explain unrelated native clock changes separately. A managed player purchase still produces its original exact native debit/stock/history receipt once.
3. **NEEDS-INPUT — managed price/stock/hours changes.** In the unlocked fixture UI, change an eligible player shop's price, stock and hours, then observe fresh decisions against the read-only rival alternative. Confirm no guaranteed rival share or invented stock, existing commitments retain their terms, and withdrawn/changed rival terms refuse without debit or substitution. Capture UI/native observations and the free total before/after each induced scenario; the mod never makes the rival edit.
4. **LOCKED-OK — reload with B1 inactive and campaigns disabled.** Use prepared fixture/settings with empty memory contribution and no campaign images/provider; dump effective settings and empty sources. Save/reload outside timing windows at outward travel, service, paid state and return. Repeat continuation/outcome diagnostics; compare IDs, D2 paid watermark, exact external units, free counts/coverage and return ownership with uninterrupted replay. Repeat a retained-boundary/expired-history checkpoint; no extra debit/count or named output appears.
5. **LOCKED-OK — closed-panel/rush and late-burst performance.** With the free panel closed, bridge-collect matched idle and meal-rush windows plus pending older commitments completing in a burst. Record p50/p95/max combined mod-work, all ≥30 ms spike counts/conditions/components, progress/lag and allocation/GC if available. Track whole-frame time/stalls separately. Check D2 ledger reconciliation and the fourteen-row/one-rollup bound before/after each window; this needs no UI input.
6. **NEEDS-INPUT — open free panel and visual boundary.** Open Ctrl+F8 during the unlocked session; visually confirm readable totals-only wording, unknown/partial versus zero, no named rival aggregate or market share, and matching before/after bridge totals. Collect paired open/closed idle/rush windows with the same setup: combined mod-work p95 <5 ms, every ≥30 ms spike flagged, whole-frame reported separately. UI opening/closing and visual confirmation require input; bridge sampling itself can then run locked.
