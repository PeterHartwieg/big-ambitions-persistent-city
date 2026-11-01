Persistent City D3a: free outflow history and totals-only line

Residents' confirmed D2 rival spending now appears as a free fourteen-day
amount/count line in Ctrl+F8 and the existing rival diagnostics. Unknown history,
partial first days and covered zero days stay distinct. The output contains no
named rival aggregate or market share; rivals remain read-only, native accounting
stays authoritative, and paid research remains off.

Schema 24 is necessary because D2 has no dated rival prices/counts after current
outcomes roll over. Exact validated 23 → 24 migration preserves existing accounts
as an undated baseline; successful payments update bounded history alongside D2's
saved watermark. Baseline + expired + retained amounts reconcile exactly with
D2. Maximum cost is 1,037 bytes and 15 charged records per food city, zero per
consumer; no old consumer tuple/native receipt changes. The final source includes
a conservative partial first day even at midnight when an undated ledger exists.

Validation: **62/62 foreground checks passed**, including the game-assembly build,
**2,528/2,528 unit cases**, discovery, every existing standalone regression tool,
the requested wrappers/demos and the new outflow-line regression.
Commands and hashes are recorded in [the results manifest](2026-11-01-d3a-outflow-line-results.json).
Tests cover retention, late/repeated payments/outcomes, reload, nonzero old-ledger
migration, source preservation/refusal, B1 inactive/campaigns absent,
unknown/partial/zero, exact fractional prices and the free DTO's no-name/no-share
guard. The original player economy/order/reload digests remain unchanged.

Measured offline cache win: open-line allocation 14,336 → 0 B/request, p95
0.0198 → 0.0001 ms. A 128-settlement burst plus refreshed open text has p95/max
0.0687/0.1943 ms; no D3a component sample ≥30 ms. History is approximately
1,529 B closed / 4,769 B open in the 365-day fixture, with 293 property bytes
and 15 records saved. Existing D2/awareness aggregate spikes ≥30 ms are flagged
separately in [checks](../checks/d3a-outflow-line.md) and
[performance](../perf/d3a-outflow-line.md); these component/offline measurements
do not certify native mod-work p95 <5 ms. Whole-frame observations remain separate.
The four parked hotspots are unchanged.

Other final offline flags: one **31.5969 ms D2 128-unit batch** and one
**32.2273 ms awareness whole-index build**. E4 four/eight-product whole-save
p95/max is **202.0061/202.1156 ms** and **201.2296/205.8757 ms**, with
**21/21 samples ≥30 ms per arm**. Earlier D2/awareness maxima
**30.7708/34.0365 ms** remain recorded separately. These aggregate operations
have no D3a-only attribution and are not ordinary native mod-work samples.

No game/rig action was performed. Combined native behavior and performance proof
remains pending the normal 6 Nov session. The concrete handoff follows.

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
