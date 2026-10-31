# VERDICT: FAIL

**Observed defect: A1 address labels and story/destination text expose internal `ba:street_*` keys.** The native address formatter resolves the same addresses correctly. This blocks the stacked A1/A2/A3 merge gate. Pointer limitations are **DEFERRED-INPUT**, never failures. No merge, release or ordinary-save clearance is granted. Role: `rig_test`.

The remaining defined native gates pass in the bounded fixture: no unexpected Persistent City errors/exceptions, measured mod-work p95 <5 ms, inspector payload/side-effect invariants, A2 migration/round-trip/refusal, and A3 decision/replay/accounting checks. [Structured verdict and counts](checks.json).

## Provenance and method

- Candidate **`754c2b67610e69a42aee65a342b1e2a403530a25`** (A3 PR #7), containing A1 PR #4 `ee9486b17db082aff1f764251568fb4670ffca2a` and A2 PR #5 `41c870f64f8d5495285026daa99029ea4033bb95`; both ancestry checks pass. Candidate DLL SHA-256 **`29ef76b33deb22f499cd1aff02b8507d26a0f19cf7a16fa38568eb75706e5cdd`**.
- Baseline **`6d43276198eba5d0dd016b51bc7fc670bc651762`**, schema 16, no inspector. DLL SHA-256 **`f7f0c36e42dcd2b66067245602f27622a2020c2df5670ecf636e9b9525ca471c`**.
- Both unchanged detached worktrees built with `python tools/build.py --game-managed "C:/BigAmbitions-AgentTests/Big Ambitions Agent Tests_Data/Managed"`, with the required per-user dotnet PATH prefix. **No offline dotnet test suite run.** Launcher verified every installed receipt before each launch. Six owned processes loaded the recorded arm; the frame observer DLL is byte-identical across all arms. [Build/load hashes and PIDs](tested-builds.json).
- Agent1 only; disposable retained FT morning fixture copies and named FT checkpoints. **957 residents, 1,914 food consumers (957 own and 957 represented), 1,059 places, one eligible owned croissant shop.** Unchanged RLO plus Persistent City, bridge-only host and stage observers; Workshop disabled. This is not full-modpack or large-market certification.
- Normal visible window, D3D11, Low, 1920×1080, target 90 FPS, vSync 0, calendar factor one third. Closed arms reload the same morning fixture and start at day 5 07:53 within less than one game minute of one another; exact native minutes are retained in JSON. The active-choice open window is day 6 about 07:20–07:40. Startup, skip, save and reload are outside captures.
- WTS positively reports session 11 **locked** at both observations. Rendering and Unity captures work; no unlock, activation or pointer injection attempted. UI/inspector/map/view seams are labelled **seam, not gesture proof**. [Initial desktop query](native-desktop.json), [final locked query](session-final.json).

## Per-check results

### A1

| Check | Result | Evidence / scope |
| --- | --- | --- |
| Name, personality words, own story, destination/reason and three meal lines | **PASS** | Native popup content captured through Open/select/Refresh; actual home, commute and food-journey cases. [inspector-summary.json](inspector-summary.json) |
| Readable localized home/work address text | **FAIL** | Inspector exposes ba:street_* keys. Native formatter gives 18 9th Street and 7 4th Avenue for those same addresses. Also visible in screenshots. [inspector-summary.json](inspector-summary.json) |
| Home and work/study map link method | **PASS** | Both native map calls return success and open the map at the supplied native address; seam, not gesture proof. [inspector-summary.json](inspector-summary.json) |
| No body, route, market, visit, purchase or save-payload side effects | **PASS** | Frozen same-dispatch route/market counters, body lists and entire SaveCodec payload remain identical. Six market methods observed. [inspector-summary.json](inspector-summary.json) |
| Identity/traits across view movement and body-pool reuse | **PASS** | pc:350 uses the same native body, lease generation 2 → 3, with an intervening zero-body view. Entire saved payload remains identical. [a3-summary.json](a3-summary.json) |
| Closed/open picking and inspector timing | **PASS** | Zero pick passes in the measured closed/open stationary-pointer states; Update and whole OnGUI attributed separately. Real hover remains deferred. [performance-summary.json](performance-summary.json) |
| No unexpected mod errors/exceptions | **PASS** | No Persistent City exception/error or inspector guard warning across the six owned game processes. [log-checks.json](log-checks.json) |
| Hover highlight and clearing off a walker | **DEFERRED-INPUT** | Locked-session pointer/focus limitation; Peter’s own playtest. No gesture pass/fail inferred from a seam. [inspector-summary.json](inspector-summary.json) |
| Real click to open/select and click address links | **DEFERRED-INPUT** | Locked-session pointer/focus limitation; Peter’s own playtest. No gesture pass/fail inferred from a seam. [inspector-summary.json](inspector-summary.json) |
| Missed clicks passing through; hold/release and right-click routing | **DEFERRED-INPUT** | Locked-session pointer/focus limitation; Peter’s own playtest. No gesture pass/fail inferred from a seam. [inspector-summary.json](inspector-summary.json) |
| Popup wheel/scroll versus native camera zoom | **DEFERRED-INPUT** | Locked-session pointer/focus limitation; Peter’s own playtest. No gesture pass/fail inferred from a seam. [inspector-summary.json](inspector-summary.json) |
| Native-UI exclusion under the pointer and focus-dependent routing | **DEFERRED-INPUT** | Locked-session pointer/focus limitation; Peter’s own playtest. No gesture pass/fail inferred from a seam. [inspector-summary.json](inspector-summary.json) |

### A2

| Check | Result | Evidence / scope |
| --- | --- | --- |
| Traits shown as words and stable across reload/reuse | **PASS** | Word-only personality lines; resident and represented-consumer trait hashes survive both round trips and the body lease change. [inspector-summary.json](inspector-summary.json) |
| Today: chose household food wording | **PASS** | Actual day-6 resident c1 shows Breakfast: chose household food; no claim that the alternative was paid or eaten. [inspector-summary.json](inspector-summary.json) |
| Uncertain/unconfirmed wording | **NOT-RUN** | No natural unconfirmed checkout in the available fixture; no lost-acknowledgement mutation injected. [a3-summary.json](a3-summary.json) |
| Real schema-16 migration and preservation | **PASS** | Baseline-created saves load and write schema 17. All old fields match exact source-payload projections; 957 residents, 1,914 consumers and six in-flight legacy meals retained. [migration-summary.json](migration-summary.json) |
| Earlier meals: not recorded before upgrade | **PASS** | Verified day-5 12:30 baseline: 301 consumers/141 own residents carry earlier-meal markers; Clara’s popup displays the expected breakfast wording. [migration-summary.json](migration-summary.json) |
| ChoiceStartsDay is next day; activation observed | **PASS** | Both migrated fixtures start policy on day 6; day-5 retained traces are legacy, day-6 traces contain weighted decisions. [migration-summary.json](migration-summary.json) |
| Save/reload keeps traits and Today records | **PASS** | Both morning and noon schema-17 round trips retain identical trait/Today hashes; no re-roll. [migration-summary.json](migration-summary.json) |
| Native shop authority and receipts preserved | **PASS** | Authority hashes match. Noon source and upgraded native saves retain all 27 receipt keys and values exactly. [native-save-contract.json](native-save-contract.json) |
| Corrupt and future-schema refusal without overwrite | **PASS** | Native loads refuse corrupted and schema-99 copies. Both whole-file hashes remain unchanged; store seam also rejects replacement and preserves its sentinel. [migration-summary.json](migration-summary.json) |
| Save size and save/load/migration timing | **PASS** | Native/Unity single samples measured outside every performance window; comparison numbers below. [save-times.json](save-times.json) |
| Same-seed legacy choice before activation | **PARTIAL** | Matched morning spending/purchases agree at zero and no weighted decision is seen before day 6. Full-day legacy observer hits its 5,000-record cap; no full baseline/candidate trace identity claim. [a3-summary.json](a3-summary.json) |
| 50,000-resident headroom | **NOT-RUN** | No suitable existing large-city FT fixture identified; only the retained 957-resident fixture used. [migration-summary.json](migration-summary.json) |

### A3

| Check | Result | Evidence / scope |
| --- | --- | --- |
| (a) Weighted decisions, shortlist/result/reason/exact price | **PASS** | Each replay observes 878 decisions: 877 household alternatives and one shop selection. At most one captured shop in this one-shop market, within the eight-shop cap. [a3-summary.json](a3-summary.json) |
| (b) Checkpoint replay determinism | **PASS** | 878/878 context projections match, including shortlist order, recomputed draw hashes, result, reason and exact quote. All 1,914 end money records match. [a3-summary.json](a3-summary.json) |
| (c) Household alternative: words, no order/stock delta | **PASS** | Actual household wording verified. Replay spans have zero new native purchases/orders/receipts and zero product/bag delta while alternatives resolve; selected shop journey has not checked out yet. [a3-summary.json](a3-summary.json) |
| (d) Accounting sanity over played span | **PASS** | After in-flight reload: exactly one checkout, receipt and resident purchase; 275221504000 units debit equals receipt/order price; one product and bag consumed. All 1,914 budgets conserve cash, no negatives. Native completed order awaits day-6 daily settlement. [a3-summary.json](a3-summary.json) |
| (e) Reload an in-flight journey, checkout and return | **PASS** | pc:350 saved outward, reloads, purchases once and returns to its original home; journey, outing and excursion clear by 10:00. [a3-summary.json](a3-summary.json) |
| (e) Save at each mid-decision quote cursor | **NOT-RUN** | No evaluating decision caught in this small single-shop fixture; journey replay was exercised instead. [a3-summary.json](a3-summary.json) |
| (f) More-than-eight-shop market | **NOT-RUN** | Fixture has one eligible owned shop; no suitable existing 16-shop fixture used. [a3-summary.json](a3-summary.json) |
| (f) Winner outside nearest eight/distant-winner fixture | **NOT-RUN** | Single-shop market cannot establish this case. [a3-summary.json](a3-summary.json) |
| (g) Explanation words with zero route/market requests | **PASS** | Post-activation household and food-journey words captured with frozen route snapshots and six market-method counters unchanged; entire payload unchanged. [inspector-summary.json](inspector-summary.json) |
| (g) Queue/deadline/unknown/unconfirmed explanation variants | **NOT-RUN** | Those specific native situations were not produced by the available fixture. [inspector-summary.json](inspector-summary.json) |
| (h) Normal-play work/route attribution and backlog | **PASS** | Active-choice open window: 100 observed requests fall within raw logical-minute bounds. Slightly wider counter bracket: 316 total units, 108 citizen units, 208 food continuation units, 29 walking requests; lag/pending decisions end at zero. [performance-summary.json](performance-summary.json) |

The actual defective popup and the correct earlier-meal wording are visible in [noon popup](noon-popup.jpg). The current-day household outcome and word-only traits are visible in [household popup](household-popup.jpg), with [city context](city-and-inspector.jpg). Map seams succeed for `18 9th Street` and `7 4th Avenue`; the inspector instead shows their internal keys. No production fix was made.

## Performance

Milliseconds below are **p50 / p95 / max**. Mod work is the Update/authority union plus same-frame whole `PopulationRuntime.OnGUI`; nested inspector Update is not added twice. Existing input-guard callbacks outside those primary scopes remain unmeasured. Percentiles use floor(n×q); initial/current partial mod frames are excluded. No dropped frames, timing exceptions, cross-frame attribution errors or active status/report calls in any capture. [Distributions, raw-file mapping, load and counters](performance-summary.json).

| Window, in run order | Seconds | FPS | Whole frame ms | Mod work ms | Mod spikes ≥30 ms | Load: CPU before→after; free RAM GiB before→after; dotnet/testhost; codex before→after |
| --- | ---: | ---: | --- | --- | ---: | --- |
| base1 | 60.248 | 70.59 | 13.796 / 18.740 / 74.692 | 0.436 / 0.723 / 16.206 | 0 | 73.3→77.0%; 21.8→21.7; D/T 0/0→0/0; C 10→10 |
| cand1 | 60.239 | 80.61 | 12.237 / 16.572 / 54.219 | 0.344 / 0.614 / 15.932 | 0 | 55.4→77.8%; 24.8→21.1; D/T 0/0→0/0; C 8→10 |
| base2 | 60.293 | 89.46 | 11.194 / 12.872 / 52.905 | 0.253 / 0.351 / 9.931 | 0 | 32.7→23.9%; 28.7→30.1; D/T 0/0→0/0; C 12→16 |
| cand2 | 60.230 | 89.26 | 11.165 / 12.819 / 52.804 | 0.308 / 0.415 / 10.517 | 0 | 26.5→29.4%; 30.3→30.0; D/T 0/0→0/0; C 16→14 |
| cand2-open | 60.255 | 88.42 | 11.312 / 13.422 / 32.019 | 0.581 / 0.812 / 4.611 | 0 | 37.7→40.7%; 24.7→25.0; D/T 0/0→0/0; C 16→16 |

| Pooled condition | Seconds / FPS | Whole frame ms | Mod work ms |
| --- | --- | --- | --- |
| baseline-closed | 120.541 / 80.03 | 12.169 / 16.977 / 74.692 | 0.309 / 0.638 / 16.206 |
| candidate-closed-legacy-day | 120.469 / 84.93 | 11.917 / 15.318 / 54.219 | 0.317 / 0.547 / 15.932 |
| candidate-open-active-choice | 60.255 / 88.42 | 11.312 / 13.422 / 32.019 | 0.581 / 0.812 / 4.611 |

**Measured perf gate: PASS. Zero mod-work spikes ≥30 ms in all five windows**, so the frame/phase/window spike list is empty. Whole-frame stalls are separate: base1: 1 >50 ms / 0 >100 ms / 0 >250 ms, cand1: 1 >50 ms / 0 >100 ms / 0 >250 ms, base2: 1 >50 ms / 0 >100 ms / 0 >250 ms, cand2: 2 >50 ms / 0 >100 ms / 0 >250 ms, cand2-open: 0 >50 ms / 0 >100 ms / 0 >250 ms.

**The 30 October whole-frame p95 jump is not reproduced here:** matched pooled baseline 16.977 ms → candidate 15.318 ms; neither matched pair worsens. This is informational under a locked desktop and varying load, not a causal improvement claim; no bisect performed.

All windows have verified autosave off before/after, unchanged save-file inventories and no native save activity. Original autosave was on (`preventAutoSave=false`, five minutes, three slots). Verification repeats after reload. Each closed arm uses a fresh fixture; four closed windows precede the one required open window. No extra performance-only rerun.

Every window has concurrent system/agent load. Top process CPU-delta rankings, CPU total, RAM and dotnet/testhost/codex/Python counts are in the JSON; typical leaders include the game, virtualisation, Python and antimalware. Per-process rankings are not synchronized CPU percentages. Before baseline 2, external dotnet/testhost activity was allowed to finish: **810 seconds** of waiting, no process controlled. Other window load checks were clear of dotnet/testhost. Baseline 1 also overlaps short stage-helper compilation and is retained as an under-load sample. Guard/export/counter seams evolved during functional work; the frame timing observer remained identical. Whole-frame comparisons retain these limits.

Closed/open inspector timing comes from the mod’s per-mode counters and observer components; the open bracket records 67 builds, zero picks and 5,868 Update calls (60.513 ms total), plus 11,736 draw calls (458.814 ms total). These counter brackets extend slightly beyond the narrow 60-second CSV. The active-choice work bracket records **316 total units / 108 citizen units / 208 food continuations**, 29 walking requests, and zero ending lag/pending decisions. There are 100 trace request minutes within raw capture bounds; the 124-record observer span includes warm-up. Full-route/16-shop rush capacity remains untested.

## Save and migration observations

| Native save sample | HSG bytes | Payload chars at observer export | Bridge save s | Observer codec write ms |
| --- | ---: | ---: | ---: | ---: |
| schema16-save | 1,106,234 | 5,732,118 | 0.672 | 389.066 |
| schema17-save | 1,134,644 | 6,053,491 | 0.531 | 273.290 |
| inflight-save | 1,318,079 | 6,868,397 | 0.500 | 385.519 |
| noon-schema16-save | 1,542,276 | 8,048,941 | 0.703 | 342.999 |
| noon-schema17-save | 1,574,716 | 8,392,505 | 0.750 | 483.413 |

Morning Unity codec read+upgrade from 16: **688.788 ms**, 5,732,118 → 6,051,558 characters. Noon baseline codec read: **936.189 ms**; noon candidate read+upgrade: **776.332 ms**. Schema-17 read: **709.712 ms**. Native load-to-ready: morning migration **43.140 s**, schema-17 round trip **31.156 s**, noon migration **30.703 s**, noon round trip **41.766 s**. [All sample timings](save-times.json).

These are single Unity/native samples under changing load, not an isolated save-performance regression estimate. Payload exports can include gameplay after the native save callback; migration equality compares the exact **saved source**, whose hash matches the candidate reader, rather than assuming the later live export is byte-identical. The native reload advances about one third of a minute before the bridge pauses it; exact codec migration comparisons separate that subsequent gameplay. Both written versions actually report schema **17**.

Native authority hashes and all 27 noon receipts match exactly. Corrupt/future copies are proper disposable native gzip saves with a same-length population-payload mutation; the game loads the container, the mod refuses the payload, and both original whole-file hashes remain unchanged. The separate store seam also refuses replacement without partial overwrite. [Native namespace hashes](native-save-contract.json), [migration/refusal/round-trip results](migration-summary.json).

The first later-fixture attempt paused at 08:22 despite a 12:30 target; it is not used as noon evidence. The stage clock wait was corrected to require actual arrival, and a separate baseline functional run created the verified 12:30 fixture. A phone clock hold was cleared through its native heading button; a same-name reload/loading race was retried before starting replay 2. Initial map helper calls without a selected insight were superseded by successful final map calls. These are tooling corrections, not mod defects. Early mutable route snapshots are not used for zero-query proof; final frozen snapshots and six explicitly patched market methods establish it.

## A3 accounting and remaining limits

The two cold checkpoint playbacks each retain **878 decisions (877 household / one shop)**, with 878 identical context/shortlist/draw/result/reason/quote projections and **zero money differences across 1,914 consumers**. Draw hashes are recomputed from observed saved key inputs using the version-1 contract, not claimed as an independently logged RNG stream. Captured quote: **275221504000 exact units = $10.25279998779296875**.

Resident `pc:350` is saved on the outward journey. After reload and play to day 6 10:00: **one** new paid receipt, resident purchase, native checkout and completed native order; resident spending, receipt and food-history revenue each increase by **275221504000 units**, with product stock and bags each falling by **one**. Every sampled household ledger conserves exact cash and remains nonnegative. The resident returns to its original home; journey/outing/excursion are clear. Day-5 legacy settlement is 57 purchases; native revenue **584.4096** has the exact binary32 bits of the exact sum rounded once. The one weighted day-6 order is still queued for native daily settlement; no full day-6 statement claim. [Replay, native order, wallet, stock, return and pool evidence](a3-summary.json).

Limits are explicit in the check tables: no 50k fixture, more-than-eight-shop/distant-winner fixture, quote-cursor checkpoint, or natural uncertain checkout. The broad legacy trace reaches its 5,000-record cap; authoritative saved ledgers and the weighted replay traces remain complete. No seed retuning, native stock injection, rival finance writes or RLO changes were made.

## Deferred input for Peter

**A1 / PR #4:** hover/clear highlight; real walker/popup/address clicks; missed-click, hold/release and right-click pass-through; wheel scrolling versus camera zoom; native-UI-under-pointer exclusion and focus-dependent routing. A2 / PR #5 and A3 / PR #7 have **no separate input-only check** in this subset; their inspector gestures inherit that A1 list. All semantic popup/map/view results above remain **seam, not gesture proof**.

## Cleanup and publication

**Restore/hash/lease: PASS.** All six owned game processes quit normally, exit 0; no kill. Original full profile **4,828/4,828** hashes and install references **107/107** match. Agent1 ModsLocal, persistent-city.json, saves, preferences/autosave, receipt, run metadata and Player log are restored. Preference-export and all three root-file SHA-256 values match the original backups. The profile manifest and verified backup precede installation; additional root/preference manifest hashes were consolidated from the unchanged original backup copies during testing. [Backup/restore/normal quits/release](cleanup-summary.json).

Lease released: **held=false, pending=0**. No owned game/helper process remains. WTS still reports locked. No Windows setting/session connection, ordinary profile, unrelated worktree/process, Steam/Workshop, RLO source, PR, merge or issue action was performed. Test tooling and private raw saves/logs/backups remain outside Git. Production worktrees remain unchanged. Only this evidence folder is published from the dedicated docs branch based on `6d43276`.

Run `python verify_evidence.py` in this folder to recompute all five distributions and pooled conditions and verify hashes, counters and evidence invariants. The bundle contains sanitized JSON, compressed timing CSVs and three JPEGs; no DLL, raw game save or credential.
