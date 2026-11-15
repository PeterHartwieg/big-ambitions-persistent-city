# Rig checklist

In-game checks waiting for a batched rig run. A rig run never gates a merge. Each slice lists its checks in its PR description. After merge the coordinator copies them here, and removes a check once a rig run passes it at a build that still contains the change. This list authorizes no run.

Every check runs in a named disposable FT city in the isolated rig profile, never in Peter's ordinary save. Autosave is off before every measured window, including after a reload, and is restored at cleanup.

Detailed specs: [F8 §7](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/f8-capability-boundaries.md#7-in-game-checks-for-the-batched-rig), [F9 §7](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/f9-city-acceptance.md#7-in-game-checks-for-the-next-batched-rig), [E6 §7](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/e6-carts-food-trucks.md#7-in-game-checks-for-the-batched-rig), [F4 plan](plans/f4-perf-gate.md), [11j report](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/checks/rig-2026-11-11j-mac-results.md), [12b report](checks/rig-2026-11-12b-mac-results.md). Checks written before 10 November are in the [docs history tag](https://github.com/PeterHartwieg/big-ambitions-persistent-city/tree/docs-history-2026-11-10/docs/checks); many test save migration, which no longer exists.

## Latest evidence

- **15 November g, Mac, build `c9378fc`:** 3 passed / 2 partial / 4 failed / 5 NOT RUN groups; #282 checks 1–5 / #246-4–5; timing **PROVISIONAL (shared Mac)**; restored ([15g results](#rig-results--15-november-2026-e-mac-279-checks-1-4-246-check-4-re-baseline-at-c9378fc)).

- **15 November e, Mac (planned 16b relaunch; run 15f), build `cbd4e68e`:** 3 passed / 1 partial / 4 failed / 4 NOT RUN groups; A/D completed; timing **PROVISIONAL (shared Mac)**; restored ([15f results](#rig-results--15-november-2026-e-mac-planned-16b-relaunch-279-checks-1-4-246-check-4-re-baseline-at-cbd4e68e)).

- **15 November d, Mac (planned 16a), build `564020b`:** 2 passed / 1 partial / 4 failed / 4 NOT RUN groups; #93 reload adoption verified; #246-4 floor mismatch; day/night gen0 6/5, pause p95 674.160/45.630 ms; timing PROVISIONAL (Mac shared); restored ([15d results](#rig-results--15-november-2026-d-mac-planned-16a-leftover-ft-rigtmp-gr5-15c-delete-246-check-4-via-agent-rig-93-277-checks-1-4-at-564020b)).

- **15 November c, Mac, build `39bd8e0`:** 0 passed / 0 partial / 4 failed / 4 NOT RUN groups. #91 fixture reload failed; day/night gen0 6/4, pause p95 110.57/4651.381 ms; street fail; provisional (Mac shared), restored ([15c results](#rig-results--15-november-2026-c-mac-gr-5-246-check-4-via-91-then-gcstreet-after-275-at-39bd8e0)).
- **15 November b, Mac, build `72b2bb51`:** 0 passed / 1 partial / 3 failed / 4 NOT RUN groups. Day/night gen0 7/6, pause p95 43.403/38.929 ms; street fail; restored ([15b results](#rig-results--15-november-2026-b-mac-gr-5-gcstreet-recheck-after-275-at-72b2bb51)).
- **15 November a, Mac, build `7107f72e`:** 0 passed / 1 partial / 3 failed / 4 NOT RUN groups. Day/night gen0 8/6, pause p95 27.888/37.301 ms; street fail; restored ([15a results](#rig-results--15-november-2026-a-mac-gr-5-gcstreet-recheck-after-273-at-7107f72e)).
- **14 November l, Mac, build `da8b495b`:** 0 passed / 1 partial / 3 failed / 4 NOT RUN groups. Day/night mod 313.5/166.4 MB, gen0 8/7, pause p95 33.488/36.717 ms; street fail; restored ([14l results](#rig-results--14-november-2026-l-mac-gr-5-gcstreet-recheck-after-270-at-da8b495b)).
- **14 November k, Mac, build `8eb87be2`:** 0 passed / 0 partial / 3 failed / 5 NOT RUN groups. Day/night gen0 6/7, pause p95 219.813/234.512 ms; advance 85.040/35.434 MB; street union p95 23.2135 ms; restored ([14k results](#rig-results--14-november-2026-k-mac-gr-5-gctraffic-recheck-after-268-at-8eb87be2)).
- **14 November j, Mac, build `50f52c18`:** 2 passed / 2 partial / 0 failed / 5 NOT RUN groups. Day/night gen0 7/6, pause p95 28.581/32.902 ms; street p95 3.7732/3.8197 ms with three spikes; night pool 16/16; look identity partial; restored ([14j results](#rig-results--14-november-2026-j-mac-gr-5-gc-recheck-after-266-at-50f52c18)).
- **14 November i, Mac, build `65b41db5`:** 1 passed / 1 partial / 0 failed / 4 NOT RUN groups. Five unrentable fixture subjects, rig tally 3 housed / 1 unreadable / 1 empty; PC applied Home evidence not observable in-load; restored ([14i results](#rig-results--14-november-2026-i-mac-246-checks-4-5-with-unrentable_fixture-at-65b41db5)).
- **14 November h, PC, candidate `650fc893`:** 0 passed / 0 partial / 0 failed / 9 NOT RUN. Local staging/windowed start worked; throwaway time/perf/modwork FT guards and fixture population blocked all four windows; exact restoration, lease free ([14h results](#rig-results--14-november-2026-h-pc-gr-5-60-fps-streetbusy-shop--gc-vs-mac-14g-at-650fc893)).
- **14 November g, Mac, build `7ff6a87a`:** 2 passed / 2 partial / 1 failed / 4 NOT RUN groups. Appearance 62.2/26.1 MB, gen0 7/6, pause p95 67.048/141.801 ms; street p95 5.2988/5.3683 ms; look/refill partial; restored ([14g results](#rig-results--14-november-2026-g-mac-gr-5-gc-recheck-after-262-at-7ff6a87a)).
- **14 November f, Mac, build `2f5e72eb`:** 4 passed / 1 partial / 0 failed / 5 NOT RUN; ordinary-save refusal verified, food sales at speed 1 checked, footprint fixture gap; restored ([14f results](#rig-results--14-november-2026-f-mac-244-p-d-7--246-checks-4-5--260-checks-1-3-at-2f5e72eb)).
- **14 November f, PC, candidate `0795aa2c`:** 0 passed / 0 partial / 0 failed / 11 not run. Windows scoped receipt-safe staging missing; installed-player PowerShell probe succeeded; restored, lease free ([14f PC results](#rig-results--14-november-2026-f-pc-244-p-d-7--246-check-4-footprint--gr-5-60-fps-arms-at-0795aa2c)).
- **14 November e, PC, build `2448bc40`:** 0 passed / 0 partial / 0 failed / 9 not run. Default start failed with SSH `homepc` resolution error; P-D 7 tooling gap; restored, lease free ([14e results](#rig-results--14-november-2026-e-pc-244-p-d-check-7-at-2448bc40)).
- **14 November d, PC, build `913abd7d`:** 0 passed / 0 partial / 0 failed / 9 not run. Windows CLI refused `--rig-test`; neither 60 fps window started. Files/hash/lease restored ([14d results](#rig-results--14-november-2026-d-pc-gr-5-60-fps-arms-at-913abd7d)).
- **14 November c, Mac, build `1d5d1b76`:** 1 passed / 1 partial / 1 failed / 6 NOT RUN groups. #255 runtime retime passed; rush-hour continuous visual proof partial; off/on paid receipts absent; restored ([14c results](#rig-results--14-november-2026-c-mac-255-retime-checks-1-3-at-1d5d1b76)).
- **14 November b, Mac, build `eb38a061`:** 0 passed / 0 partial / 3 failed / 7 not run. Day/night GC 11/8 gen0, pause p95 28.191/61.316 ms; physical+advance 1,021.6/433.6 MB; street UpdateCity p95 5.5170 ms; appearance/footprint/actual 60 fps proof blocked; restored ([14b results](#rig-results--14-november-2026-b-mac-gr-5-gc-254-footprint-60-fps-at-eb38a061)).
- **14 November a, Mac, build `b3d58a6a`:** 1 passed / 0 partial / 4 failed / 1 not run. Day/night GC 11/9 gen0, pause p95 103.863/70.122 ms; street UpdateCity p95 5.2683 ms; Living-off passed, toggle repeat stopped, P-D console blocked; restored ([14a results](#rig-results--14-november-2026-a-mac-gr-5-gc-rlo-p-d-7-at-b3d58a6a)).
- **14 November g, Mac, build `37a150d3`:** 0 passed / 0 partial / 3 failed / 6 not run. Day/night GC 11/8 gen0, pause p95 56.671/104.551 ms; street UpdateCity p95 5.1413 ms, two spikes; restored, run complete ([14g results](#rig-results--14-november-2026-g-mac-gr-5-gc-at-37a150d3)).
- **14 November f, Mac, build `d237f443`:** 1 passed / 2 partial / 2 failed / 1 not run. PC callback save p95 742.826 ms (recorded phases 301.027 ms); day/night GC 12/6 gen0, pause p95 65.028/31.977 ms; street mod-work p95 4.2557 ms; old-format reload passed ([14f results](#rig-results--14-november-2026-f-mac-gr-5-at-d237f443)).
- **13 November e, Mac, build `5fe5eb23`:** GR-5: 0 passed / 1 partial / 3 failed / 1 not run. Save p95 731.144 ms; GC 10 gen0 / pause p95 66.260 ms; broader shop mod-work p95 5.0406 ms; 60 fps and footprint proof unavailable ([13e results](#rig-results--13-november-2026-e-mac-gr-5-at-5fe5eb23)).
- **13 November d, Mac, build `f3e3d538` (code `83b6156e`):** P-D 5 passed / 1 partial / 1 not run; RLO 1.0.6 #239 1 passed / 3 partial / 2 not run; GR-9 2 partial / 8 not run. Three GC windows and background capture ran; Living ops remain blocked ([13d results](#rig-results--13-november-2026-d-mac-p-d-mono-gc-rlo-106-239-and-gr-9-at-f3e3d538)).
- **13 November, Mac, build `a3a6c8db`:** GR-4, GR-6/7/8, GR-9 and RLO #239 rows: 2 passed, 6 partial, 1 failed, 11 not run; blocked. The rig ran RLO 0.45.0, not 1.0 ([13 November report](checks/rig-2026-11-13-mac-results.md)).
- **12b, Mac, build `060a7947`:** 15 targeted rows verified by rig: food, goods and service probe sales, ceiling refusal, mid-probe cut refusal and pre-probe recovery, #77 goods and service fixtures, F7-3 Pending capture. This proves those rows at that build only. It predates GR-3, GR-4, GR-6 to GR-9 and #239.
- **12a, Mac:** the old "loaded cut has no half sale" assertion failed. #233 replaced it with refuse-and-recover; 12b passed the new contract, and 12a's failure stands ([12a](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/checks/rig-2026-11-12a-mac-results.md)).
- **11j, Mac:** natural first sale passed; partial rows are listed below.

## 1. Re-run after later product changes

1. **Sales at the current head.** Setup: food, goods and service fixtures from 12b. Steps: repeat 12b's S2 food, goods, service and ceiling probes and the #233 cut and recovery arms. Expected: the 12b results unchanged. Needed because GR-3 (#234) and GR-4 (#236) changed the purchase path.
2. **PR checks since 12b.** The "In-game checks" lists in [#234](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/234), [#236](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/236), [#237](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/237), [#238](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/238) and [#239](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/239). The coordinator copies them into this section. They cover needs purchases, venue readiness, staffed capacity, evenings and households, workplaces, through-traffic, own cars and bodies, and the RLO overlap rules.
3. **RLO overlap and own cars.** Setup: full modpack with RLO 1.0, Living population on, then off. Steps: load, run two game hours, save, reload. Expected: Persistent City overrules RLO where they overlap, as #239 specifies; no native car is borrowed; walkers and parked cars persist across reload (GR-9).

## 2. Shop menus and storms (E5b2 to E5b5)

Missing: original-three, mixed original and dynamic, eight- and nine-offer, catalogue lifecycle, metadata override, refill and resource, and 8- and 64-shop fixtures. The single-croissant fixture and `shop_edit` cannot add the holders and prices. Spec: [E5b probe](../tools/e5b-native-rig-proof/README.md).

4. **Mixed and original-three menus.** Expected: each offer sells at its own price, and catalogue changes reconcile without lost or duplicated receipts.
5. **Eight- and nine-offer menus, refill.** Expected: every offer is reachable; refill and resource use match native.
6. **Repair storms at 8 and 64 shops.** Expected: mutation storms reconcile; mod-work stays inside the perf gate.

## 3. Street Food, vehicle sales and research

7. **E6 checks 1 to 13** ([E6 §7](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/e6-carts-food-trucks.md#7-in-game-checks-for-the-batched-rig)). Missing: RIG-SFAPI, RIG-VLEDGER, RIG-SFFAULT.
8. **Street Food absent, then returned (F8-2).** Steps: save, load without FoodTrucks, run two hours, save, restore and load. Expected: `vendor.streetfood absent` with its reason; shop money equals a run with no cart; the cart requalifies after return. Missing: RIG-PCSAVE.
9. **Research and a truck in one city (F9-7).** Steps: order a neighbourhood snapshot, save and reload while it is pending, run to morning. Expected: one charge on that night's marketing bill, one report, receipts unique across reload. Missing: RIG-LIVE.
10. **Mechanism report (F8-9; 11j partial).** Expected: `shop.fullservice-bag.v1`, `vehicle.streetfood-api.v1`, and `shop.dynamic-catalogue off` for a non-historical product. 11j saw only the shop row.

## 4. Throughput and wholesale

11. **Representative throughput (F6b-2; 11j partial).** Setup: a cohort of staffed food shops. Expected: hourly completions against native MealsPerHour support a median and the default ceiling. 11j had one shop, which is not a cohort.
12. **Price response at weight 1.2 (F9-9).** Steps: raise every price by 50% in one copy for two days. Expected: fewer meals and revenue up by well under 50%. Native evidence, not calibration. Missing: RIG-LEDGER, RIG-PCCONFIG.

## 5. Saves, refusals and interrupted state

13. **Larger F7-1 matrix (11j partial).** Expected: Pending, same-minute fault, relocation and menu arms each keep identities, wallets and receipts across cold reload. Only F7-3's capture arm passed (12b).
14. **X3-1 literal read (11j partial).** Expected: the population read emits `Loaded` with schema 1. 11j saw the reload accepted but not that line.
15. **Unreadable or wrong-schema city refused and kept (F9-8).** Expected: the refusal status; payload and other PC keys byte-identical across a native save. Missing: a wrong-schema FT loader, RIG-PCSAVE.
16. **RLO absent at load (F8-4).** Expected: no city starts; status says RLO is required; payload hash unchanged; native customers serve the shop.
17. **Changed native members (F8-5 to F8-7).** Expected: the named capability goes `off`, the payload is unchanged, owned shops hand back at the next accounting boundary. Missing: RIG-FAULT.
18. **Foreign checkout prefix (F8-10).** Expected: `food.checkout.shop off: foreign prefix by <owner>`; zero PC receipts. Missing: RIG-FOREIGNPATCH.
19. **Business lifecycle (GR-1 checks 2, 3, 5).** Close and replace, modded type, older city. Missing: a lifecycle fixture, a modded-type fixture, a pre-GR-1 save.

## 6. Cadence, bodies, config and seven days (F9)

20. **Seven days with daily save and reload (F9-4).** Expected: PC extracts and native orders equal an uninterrupted copy; ledger conserved daily. Missing: RIG-PCSAVE, RIG-LEDGER, RIG-LIVE.
21. **Native changes across cadences (F9-5).** Frame caps 30, 60, 120 and a hidden skip. Expected: identical extracts; each change at the same minute; lag at most one game minute. Missing: RIG-FRAMECAP.
22. **Detail transitions and in-flight reload (F9-6; F2 matrix).** Bodies on and off. Expected: equal extracts and orders. 11j read movement diagnostics only. Missing: RIG-PCCONFIG.
23. **Provenance, capabilities and summary (F9-1 to F9-3, F9-10).** Expected: a declared fingerprint set, every capability active or inactive with a reason, one merged summary. Missing: RIG-ACCEPT.

## 7. Scale, presentation and performance

24. **Population and GC scale.** FT-F4-Stress10k and city-50k. Expected: M1 and M2 per the [F4 plan](plans/f4-perf-gate.md); GC spikes flagged. Missing: RIG-PCFIX.
25. **Sealed F4 fixtures and windows (F4f, F7e).** Missing: implementation and manifest sealing.
26. **Present, foreground and priority flow steps.** Missing: RIG-PRESENT, RIG-FOREGROUND, RIG-PRIORITY.
27. **Capability report cost (F8-11).** Expected: mod-work p95 under 5 ms; every spike of 30 ms or more listed with its component.

## 8. Owner input

28. **Pointer, hover, focus and click.** Peter's playtest, not the rig.
29. **Windows and PC-overlay rows.** Need the Windows rig.

## Rig results — 13 November 2026 b, Mac (P-D Mono GC at 83b6156e)

Role `rig_test`, where `mac`, isolated `agentmac`. **0 passed / 0 partial / 0 failed / 7 NOT RUN. RESULT: BLOCKED.** Counts cover the seven checks in [PR #244](https://github.com/PeterHartwieg/big-ambitions-persistent-city/pull/244); the setup failure is reported separately. No in-game evidence was obtained. GR-5 still waits for the P-D measurements; this run provides no population-size or GC-cost conclusion. In-game proof gates no merge.

`<stage>` = `/Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13b-mac`. Raw commands, errors, backup manifest and originals remain there. A report copy is at `~/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-13b-mac.md`. No raw logs, binaries, saves or credentials are in this diff.

### Build and rig

| Item | Observation |
|---|---|
| Candidate / verified tip | `83b6156e56275069ad684b26cfa8677dbca4b2a4`. `git fetch origin` followed by `git rev-parse origin/codex/persistent-city` returned the briefing candidate before the build; it had not moved. |
| Build checkout | Detached `rig-2026-11-13b-mac`, created from that tip. Original Persistent City working tree stayed clean at `9c617c62efee91f999695ebc6a47171e1218282e`. |
| Build command / reference | `python3 tools/build.py --game-managed "/Users/peterhartwieg/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"` exited 0. `PATH=/opt/homebrew/bin:$PATH`; `DOTNET_ROOT` came from `/opt/homebrew/bin/brew --prefix dotnet` plus `/libexec`. |
| Product DLL SHA-256 | `61b8292227e17fe9609098ef40d52238b556e2e2fc73da782870b848d1a074cc`. Staged only in the isolated ModsLocal, then restored. |
| Official rig checkout | `eea054728f546f07dc74305112fb429da8997cea`, clean before and after; no edits, commits, installs or pushes there. Read `docs/MAC.md` and the relevant `docs/rig-ops.md` sections before operating. |
| Live installed AgentRigOps SHA-256 | `6478e76cb4c04b44f8d8ef315d4ef75fb5d912034f957696fdc4ad7a6b22691c`, unchanged before and after. This identifies the installed file; no runtime loaded it. |
| Rollback retained, unused | `/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/updates/20261112T070545Z`. The install was not broken by this run; no rollback or rig reinstall attempted. |
| Resources / lease | 45.8 GiB disk free and 66% memory free. Lease was free; acquired with purpose `Persistent City 13 Nov b Mac rig (P-D GC)` and released after restore. No lease wait or resource refusal. |
| Start | `python3 scripts/mac-agent start --mode headless --rig-test`, exit 1 during install verification, before process launch. No background graphics or captures attempted. |
| Evidence branch | `codex/mac-rig-2026-11-13b`, separate `rig-2026-11-13b-mac-evidence` worktree. Remote name was free; no numeric suffix needed. |
| Configuration | Only this run's Persistent City DLL/locale/config and corresponding isolated install-receipt entries were staged. Price and household weights were not overridden or changed; schema stays 1. No production code changes. |

### Per-size measurements

| Requested persons | Status / reached | gen0/1/2 | pause_ms p50/p95/max | frame_ms_plain p95 | mod_ms_plain p95 | GC/frame spikes >=30 ms | incremental / dropped / autosave |
|---|---|---|---|---|---|---|---|
| 3,000 | NOT RUN / unavailable | unavailable | unavailable | unavailable | unavailable | unavailable | unavailable |
| 10,000 | NOT RUN / unavailable | unavailable | unavailable | unavailable | unavailable | unavailable | unavailable |
| 20,000 | NOT RUN / unavailable | unavailable | unavailable | unavailable | unavailable | unavailable | unavailable |

All sizes refer to head `83b6156e56275069ad684b26cfa8677dbca4b2a4`. No `[Persistent City] gc report {json}` line or following human line was produced. Persons, reached/visitor_capacity/portals/refusals, generation counts, heap start/end/peak/before_gc/after_gc, Mono heap/used, pause percentiles, all spikes, frame/mod percentiles, max frame, incremental, dropped frames and autosave-stayed-off are unavailable. Missing values are explicit nulls in [the small measurement record](perf/pd-mono-gc-2026-11-13-mac.json), SHA-256 `8af922735fb5013ce147bff34f38d43a55ca21f76df26bffd5fabe7a0c478807`. These nulls are not zeros or performance passes.

The requested 3,000 → 10,000 → 20,000 same-city sequence did not begin. No top-speed advance, settling hour or 10-minute measured window ran. No conclusion about the 5 ms mod-work p95 target or >=30 ms spikes is possible. No incremental flag was read: if a later run reports incremental=true, collection-frame pause_ms understates total GC work, and frame_ms_plain p95, frame_spikes_30ms and max_frame_ms must be compared across sizes as PR #244 specifies. Other agent sessions were present in the process inventory; any measurements would have been provisional on this shared Mac. This run has no timings to label or compare.

### Per-check results

| Check | Result | Observation |
|---|---|---|
| 1. Sampler off costs nothing | **NOT RUN** | No client launched; no usual mod-work window or previous-head comparison. Absence of a report is not a pass. |
| 2. 3,000 persons | **NOT RUN** | No FT city loaded; no target growth, settling hour or 10-minute window. |
| 3. 10,000 persons | **NOT RUN** | Depends on check 2 in the same fresh city; no window. |
| 4. 20,000 persons | **NOT RUN** | Depends on check 3; no reached count or capacity/portal/refusal status. The reason for not reaching 20,000 is startup refusal, not an observed population limit. |
| 5. Report does not sample itself | **NOT RUN** | No sampler or three reports 10 seconds apart; frame growth and max_frame_ms not observed. |
| 6. Target gone after reload | **NOT RUN** | No measurement city, saved baseline or reload; target=off not observed. |
| 7. Ordinary saves refused | **NOT RUN** | No second disposable non-FT city created; the command refusal and unchanged persons not observed. |

### Findings and tooling gaps

The required startup op failed with exit 1 and exact stderr:

```text
Installed file changed: /Users/peterhartwieg/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/RealLifeOverhaul/Bundles/reallife_anim
```

Offline comparison found **13 pre-existing receipt mismatches**, all under isolated `ModsLocal/RealLifeOverhaul`: two bundles (`reallife_anim`, `reallife_props`), nine locale files (`de`, `en`, `es`, `fr`, `nl`, `pt`, `ru`, `tr`, `zh-cn`), `RealLifeOverhaul.dll` and `thumbnail.jpg`. Every actual hash equals the pre-staging backup hash; the mismatches existed before this run's staging. Expected and actual hashes are in the measurement record and `<stage>/install-discrepancies.json`. The first bundle expected `d8c8e95cba530293961d2185d26251029f595d8391f77740683c0dc4afc4cdcd`, actual `ebd6d965b2550eb31eae3b833dcaba259442c3d0a3a98f25d4dda82c8c414319`.

The existing install verifier was not bypassed, rebaselined for RLO or repaired. RLO was not installed or updated. No code root cause is assigned. The offline installed DLL has numeric version strings `1.0.6.0` and `1.0.6+7024b945c199c5b31a5754ab189c70d03a2d9904` and SHA-256 `c96709779d8bb534c05a6743d243a000eeb3e03901995da03d8d9b5593a9f6fe`; this is an offline file observation, not a verified loaded version or a #239 pass.

Optional read-only capability listing `python3 scripts/mac-agent op ops` exited 1:

```text
No bridge at http://127.0.0.1:28330: start the local Mac fallback with tools/agent-tests/mac-agent start in the Driven Rides checkout; no tunnel is needed
```

Thus current physical player-car control / walker identity binding availability could not be re-verified. No GR-9 operations ran. Console, top-speed and Player.log capabilities are documented but were not exercised; this run establishes a startup installation-verification gap, not their absence. No old Player.log was substituted for evidence at this head. No `pc-gc-measure@1` or manual console/log run could start.

### NOT RUN grouped

- P-D checks 1–7: dependent on a running private rig, blocked by the exact startup error above.
- All three size windows and their GC/human report lines: no client, no city and no measured frames.
- Optional live RLO version and current capability enumeration: startup refused; the ops listing additionally returned the exact No bridge error above.

### Fixtures, autosave and cleanup

Fixture names: **none created**. Planned fresh cities were `FT-PD-GC-20261113b` and `PD-GC-Refusal-20261113b` (the second intentionally non-FT). Neither was created or loaded, so subway-station availability and ordinary-save refusal were not verified. No existing saves were opened, renamed or copied; no fixture save exists from this run.

Before staging, **267 original files** were copied with a SHA-256 manifest: isolated ModsLocal, isolated profile root files, preferences plist and install receipt. The original plist had no key containing `autosav` (`autosave-original.json` is {}). No game-level autosave readback was possible. `--rig-test` was requested, but process launch never occurred, so autosave suppression is not claimed. No measured window or native save occurred.

Cleanup ran `python3 scripts/mac-agent stop` successfully without `--force`, restored all backed-up originals including persistent-city.json and preferences, and verified **267 files / 0 hash mismatches / autosave plist restored=true**. RLO files retain their pre-run hashes, including the original receipt discrepancy. The lease was then released: held=false, pending=0; status running=false, process=null. No game PID was started and no owned rig process remains. Windows, Steam's app, the ordinary profile, other sessions and macOS settings were untouched. No wrap marker was present. Build and evidence worktrees are removed after committing and opening the results PR; raw stage evidence remains outside Git.

### Validation

Build passed. Docs-only evidence adds no production or test changes; no full or regression suite was run. Markdown-ban and changed-document link validation are recorded in the PR. The seven in-game checks remain NOT RUN; offline restore and hash checks are separate evidence.

## Rig results — 13 November 2026 c, Mac (P-D Mono GC at 83b6156e)

Role `rig_test`, where `mac`, isolated `agentmac`. This is the 13c rerun after 13b was BLOCKED at startup at 13:27 Europe/Berlin. **0 passed / 0 partial / 0 failed / 7 NOT RUN. RESULT: BLOCKED.** The setup failure is separate from those seven counts. The existing 13b section remains unchanged. GR-5 still waits for P-D GC measurements: no population-size, pause-cost or mod-work conclusion follows from this run. In-game proof gates no merge.

`<stage>` is `/Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13c-mac`. Backup originals, manifests, errors and restore evidence remain there. Report copy: `/Users/peterhartwieg/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-13c-mac.md`. No raw logs, saves or binaries are added to Git.

### Build and rig

| Item | Observation |
|---|---|
| Candidate / verified tip | `83b6156e56275069ad684b26cfa8677dbca4b2a4`. `git fetch origin` and `git rev-parse origin/codex/persistent-city` returned the briefing candidate before build; the tip had not moved. |
| Build checkout | Detached `rig-2026-11-13c-mac`. Original Persistent City checkout remained clean at `9c617c62efee91f999695ebc6a47171e1218282e`. |
| Build / reference | `python3 tools/build.py --game-managed "/Users/peterhartwieg/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0. PATH prepended `/opt/homebrew/bin`; DOTNET_ROOT was the Homebrew dotnet prefix plus `/libexec`. |
| Candidate DLL SHA-256 | `61b8292227e17fe9609098ef40d52238b556e2e2fc73da782870b848d1a074cc`. Copied to isolated PersistentCity, then restored. |
| Official rig checkout | `e6a82372103ef3e379b5556882a433a3613a56e0`, clean before and after. No edits, commits, pushes or install commands. Read `docs/MAC.md` and relevant `docs/rig-ops.md` sections first. |
| Live AgentRigOps SHA-256 | `387dce1d54c869d4d14e6e87546659419f85c308456909bdbe11a60b773cb8b2`, identical before and after. No runtime launched to load it. |
| Rollback path, unused | `/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/updates/20261113T124248Z`. No rig install was attempted or broken; no rollback ran. |
| Resources / lease | 44.9 GiB disk, 67% memory free. Lease was free; acquired at 12:45:22 UTC with purpose `Persistent City 13 Nov c Mac rig (P-D GC)`, then released after restoration. |
| Configuration | No production code changes. Saved schema stays 1. FoodChoicePriceWeight and FoodChoiceHouseholdWeight were unchanged. Existing persistent-city.json was preserved. |
| Evidence | Existing branch `codex/mac-rig-2026-11-13b`, separate `rig-2026-11-13c-mac-evidence` worktree; `git pull --rebase origin codex/mac-rig-2026-11-13b` was already up to date at `dc9d1b07`. Existing PR #245 only. |

### Per-size measurements

| Requested persons | Status / reached | gen0/1/2 | pause_ms p50/p95/max | frame_ms_plain p95 | mod_ms_plain p95 | GC/frame spikes >=30 ms | incremental / dropped / autosave |
|---|---|---|---|---|---|---|---|
| 3,000 | NOT RUN / unavailable | unavailable | unavailable | unavailable | unavailable | unavailable | unavailable |
| 10,000 | NOT RUN / unavailable | unavailable | unavailable | unavailable | unavailable | unavailable | unavailable |
| 20,000 | NOT RUN / unavailable | unavailable | unavailable | unavailable | unavailable | unavailable | unavailable |

All sizes refer to `83b6156e56275069ad684b26cfa8677dbca4b2a4`. No `[Persistent City] gc report {json}` line or following human line was produced. Persons, reached/visitor_capacity/portals/refusals, generation counts, heap start/end/peak/before_gc/after_gc, Mono heap/used, pause percentiles, spike counts, frame/mod percentiles, max frame, incremental, dropped frames and autosave-stayed-off are unavailable. Explicit nulls are in [the 13c measurement record](perf/pd-mono-gc-2026-11-13c-mac.json), SHA-256 `901459ca6fad6cb7411b3ce9aacedb6d0638f0b97579ec23b1fd0a54c530cc8b`. The 13b JSON is unchanged.

No midnight growth, settling hour or ten-minute window began. The reason 20,000 was not reached is startup refusal, not an observed portal, capacity or population limit. There is no evidence against which to apply the <5 ms mod-work p95 target or enumerate >=30 ms spikes. No incremental flag was read. For a later incremental=true run, pause_ms understates collection work; compare frame_ms_plain p95, frame_spikes_30ms and max_frame_ms across sizes as PR #244 directs. Other agent sessions were present on this Mac; any timing would have been provisional, but this run has none.

### Per-check results

| Check | Result | Observation |
|---|---|---|
| 1. Sampler off costs nothing | **NOT RUN** | No client, usual mod-work window or previous-head comparison. No report line is not a pass. |
| 2. 3,000 persons | **NOT RUN** | No fresh FT city, target, settling hour or ten-minute window. |
| 3. 10,000 persons | **NOT RUN** | The same-city sequence never started. |
| 4. 20,000 persons | **NOT RUN** | No reached count or reason from a population status line. |
| 5. Report does not sample itself | **NOT RUN** | No sampler or three reports ten seconds apart; frames and max_frame_ms unobserved. |
| 6. Target gone after reload | **NOT RUN** | No baseline save, measurement target or reload; target=off unobserved. |
| 7. Ordinary saves refused | **NOT RUN** | No second disposable non-FT city; refusal and unchanged persons unobserved. |

### Findings and tooling gaps

One foreground start attempt ran, with no retry:

```text
python3 scripts/mac-agent start --mode headless --rig-test
exit 1
Installed file changed: /Users/peterhartwieg/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity/PersistentCity.dll
```

The unchanged install receipt pins the original Persistent City DLL SHA-256 `e0756b4a74764c4b04e65cf0edd9a2043cff9ccb4e34dea2942a9063c0582124`. The staged candidate hash is in the build table. Startup validates that receipt before launching. Peter explicitly prohibited edits to install.json and receipts; none were changed or bypassed. This is a receipt gate for the candidate DLL. The earlier RLO startup error was not returned in this run, and read-only RLO/receipt hashes remained unchanged. No code root cause is assigned.

The staging helper copied the candidate DLL, then exited 1 because it assumed the isolated old package already contained the candidate locale path:

```text
Expected existing Persistent City file: /Users/peterhartwieg/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity/Locales/en.json
```

This helper error is recorded separately from the startup refusal. No locale was copied and persistent-city.json was preserved. The single start attempt still ran and produced the exact refusal above. The original DLL was restored; no staging retry or package repair followed.

PR #244 GraphQL read returned `GraphQL: API rate limit already exceeded for user ID 95130991.` REST `gh api repos/PeterHartwieg/big-ambitions-persistent-city/pulls/244 --jq .body` succeeded. No weaker criteria were substituted. Console, speed control and Player.log access were documented but not exercised; startup refusal does not establish that those capabilities are absent. No older Player.log was used as evidence. No captures, GR-9 or #239 checks ran.

Optional offline RLO version strings read `0.45.0.0` and `0.45.0+b12d3581860ce380397223a2790fcce8b72b3482`, consistent with the requested isolated 0.45.0. This is an installed-file observation. No RLO file, receipt, backup tree or install was modified.

### NOT RUN grouped

- P-D checks 1–7 and the 3,000 → 10,000 → 20,000 windows: no client launched because candidate DLL verification refused startup.
- All gc report JSON/human lines and growth/reload/refusal evidence: dependent on that client and fresh disposable cities.
- Optional live capability enumeration: not attempted after the startup failure; no GR-9 or Living-population work.

### Fixtures, autosave and cleanup

Fixture names: **none created**. Planned names were `FT-PD-GC-20261113c` and `PD-GC-Refusal-20261113c` (intentionally non-FT). No city was opened; subway-station availability was not tested. No Peter save or Windows PC was used.

Before copying the DLL, 34 existing files were backed up with SHA-256: the isolated Persistent City tree, isolated profile root files and isolated preference plist. Backup manifest SHA-256: `030d7f1dbb77f0d63fcd5ddfef49203a974ef6c1cdd027470e9d12a5efe0d53a`. RLO and install.json were hashed read-only (49 files) and never copied for restoration. The original plist had no autosave-named key (`autosave-original.json` is {}). No game-level autosave readback was possible. The requested --rig-test process never launched, so suppression is not claimed. No measured window or save occurred.

`python3 scripts/mac-agent stop` returned stopped=true without --force. Only the changed Persistent City DLL needed restoration; all 34 original-file hashes matched afterward, including config, profile preferences and plist. All 49 read-only RLO/receipt hashes matched. AgentRigOps retained its recorded hash. `lock release` returned held=false, pending=0; final status running=false, process=null. No game process was started by this run and none remains. The existing unrelated processes were left alone. No wrap marker was present. Only the 13c build/evidence worktrees are removed after committing and updating PR #245; 13b leftovers remain untouched.

### Validation

Build passed. Evidence-only changes require no production tests; no full or regression suite ran. Markdown-ban, changed-document links, JSON parsing and diff whitespace checks passed. Raw evidence remains outside Git. PR #245 remains open and unmerged, with no reviewers requested.

## Rig results — 13 November 2026 d, Mac (P-D Mono GC, RLO 1.0.6 #239 and GR-9 at f3e3d538)

Role `rig_test`, where `mac`, isolated `agentmac`. This is 13d after 13b was blocked by startup receipt validation and 13c by the missing candidate-staging path. Both earlier sections remain unchanged. **P-D: 5 passed / 1 partial / 0 failed / 1 NOT RUN. RLO #239: 1 passed / 3 partial / 0 failed / 2 NOT RUN. GR-9: 0 passed / 2 partial / 0 failed / 8 NOT RUN. RESULT: BLOCKED** by the Living-option operation and remaining check coverage. In-game proof gates no merge.

The requested three ten-minute Mono GC windows ran. The Living-on owned-food check is verified by rig under RLO 1.0.6. Background capture now completes without the earlier client crash. No production source, schema, economy defaults, RLO files, version or changelog was changed. No release, merge or Workshop action occurred. The Windows PC, Steam app, normal profile and other sessions were not used.

`<stage>` = `/Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13d-mac`. Raw logs, screenshots, fixture extractions, commands, input corrections and backup originals stay there. Report copy: `/Users/peterhartwieg/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-13d-mac.md`. [Complete GC JSON/human lines and sampler-off records](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/codex/mac-rig-2026-11-13b/docs/perf/pd-mono-gc-2026-11-13d-mac.json), 15,771 bytes, SHA-256 `2d4e4f94cb4470955666761534dd2629e40341eea50a29957c55ba03f4cc6086`.

### Build and rig

| Item | Observation |
|---|---|
| Built HEAD | `f3e3d53859f8b21e0619edd61af1d5db22e3e259`, the existing results-branch head. Its non-docs content equals candidate `83b6156e56275069ad684b26cfa8677dbca4b2a4` (#244). |
| Base / evidence branch | `codex/mac-rig-2026-11-13b`, tracking the existing remote branch, in the new `rig-2026-11-13d-mac-evidence` worktree. The candidate was already an ancestor; no candidate-base merge was needed before build. The final fetch/normal merge of the remote results branch was already up to date at f3e3d538; no rebase or force-push. |
| Build | `python3 tools/build.py --game-managed "/Users/peterhartwieg/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0. `/opt/homebrew/bin` was prepended to PATH; DOTNET_ROOT was the Homebrew dotnet prefix plus `/libexec`. |
| Candidate DLL SHA-256 | `61b8292227e17fe9609098ef40d52238b556e2e2fc73da782870b848d1a074cc`. The installed file matched before the run. Startup receipt verification and the live measurement command identify the staged package. Player.log has the population activation line but no commit-stamped build/version line; none is invented. |
| Agent-rig HEAD | `0b18d38ece6e8fe77c35ee5ea694849c7478d054`, clean; `git fetch origin` / `git pull --ff-only` were already up to date. No edits, commits or pushes to that checkout. The final HEAD and clean tree matched the initial check. |
| Live AgentRigOps SHA-256 | `5578f9f4657fa2269ee50aa86adfc6d699881fac7e102084918da625045cd59e`; live health also reports this hash and `ready`. The final on-disk hash was unchanged. |
| RLO loaded | Player.log: `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` Capability status reports Living population on. The Living read/set operations fail as recorded below. |
| Initial resources / lease | 62.1 GiB disk / 61% memory free; lease was free and acquired at 17:55:58 UTC for this run. The initial game was stopped. All lifecycle commands used owner `persistent-city-rig-2026-11-13d-mac`. |
| Autosave original | Native setting 5 minutes / 3 saves; no autosave-named plist key. Every process used `--rig-test`, with #70 suppression confirmed after loads and before/after each window. No preference-based autosave workaround. |
| Configurations | GC and sampler-off windows used the built package defaults, physical caps 96/24 and radius 120 m. The later overlap package changed only the test setting `NeedPurchasesEnabled=true`; its DLL stayed identical. Price/household weights remained 1.2/1.0. |
| Previous-head comparison | Detached build worktree at `9c617c62efee91f999695ebc6a47171e1218282e`, the direct parent of #244; DLL `bdc95999f6b55f54da398ee8eca81079cbd7f91c5ee54e34e56cf73209556641`. Built with the same command, then staged only for the sampler-off comparison. |

Every staging command below ran from the official agent-rig checkout, stopped and leased, first with `--what-if` and then without it. The helper accepted all three; no refresh fallback, manual receipt edit or RLO install/rollback command was used. Each printed rollback is retained verbatim in `<stage>/stage.txt`, `previous-stage.txt` or `candidate-rlo-stage.txt`.

| Exact staging command | Actual receipt-backups folder |
|---|---|
| `python3 scripts/install-mac.py --stage-mod PersistentCity /Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13d-mac/package` | `/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261113T175700Z` |
| `python3 scripts/install-mac.py --stage-mod PersistentCity /Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13d-mac/package-previous` | `/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261113T192637Z` |
| `python3 scripts/install-mac.py --stage-mod PersistentCity /Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13d-mac/package-rlo` | `/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261113T193130Z` |

The first stage added `Locales/en.json` as expected. The initial backup, not either later candidate backup, is the cleanup source. Independent manifests/copies were taken before staging; the original set has 34 files, including the entire original PersistentCity folder, isolated profile root files and preference plist. RLO mod files were hashed read-only, never staged or restored.

### P-D measurements and GR-5

Timings are **provisional on the shared Mac**: other live sessions were present (coding-agent and desktop app processes were observed, without inspecting or changing their work). All three GC windows were headless, at 1× on one fixed street, after one in-game settling hour. They ran from about 01:10 to 02:50 in the city, not a daytime crowd stress scene. The 30 FPS rig cap makes an ordinary frame interval exceed the 30 ms reporting threshold. Whole-frame intervals remain separate from mod work.

| Requested / actual persons | Day / stored frames / dropped | reached / visitor_capacity / portals / refusals | gen0/1/2 | pause_ms p50 / p95 / max | frame_ms_plain p95 / max_frame_ms | mod_ms_plain p95 / max_mod_ms | GC / frame spikes >=30 ms | incremental / autosave off |
|---|---|---|---|---|---|---|---|---|
| 3,000 / **3,222** | 3 / 16,960 / 0 | 3,222 / 1,389 / 20 / 0 | 5 / 0 / 0 | 33.526 / 35.941 / 35.941 | 38.354 / 385.641 | 0.946 / 10.939 | 5 / 16,955 | true / yes |
| 10,000 / **10,000** | 4 / 16,949 / 0 | 10,000 / 8,389 / 20 / 0 | 5 / 0 / 0 | 29.533 / 36.843 / 36.843 | 38.639 / 212.053 | 1.189 / 5.605 | 5 / 16,941 | true / yes |
| 20,000 / **20,000** | 5 / 16,916 / 0 | 20,000 / 18,389 / 20 / 0 | 5 / 0 / 0 | 40.339 / 44.193 / 44.193 | 38.545 / 276.460 | 0.841 / **35.364** | 5 / 16,900 | true / yes |

All times are ms. `mod_ms_plain` times UpdateCity as #244 defines it; the sampler-off windows below use the rig's broader mod-work union. The **35.364 ms** 20,000-person maximum is flagged. The GC command exports a maximum, not a per-frame mod-work spike list; its exact spike count is unavailable. Every exported GC/frame spike count is above. Mono reported `max_generation=0` in each window.

| Actual persons | heap_start_bytes | heap_end_bytes | heap_peak_bytes | heap_before_gc_bytes | heap_after_gc_bytes | mono_heap_bytes | mono_used_bytes |
|---|---|---|---|---|---|---|---|
| 3,222 | 1303191552 | 1220288512 | 1471025152 | 1458471731 | 1281739980 | 1471660032 | 1220288512 |
| 10,000 | 1302716416 | 1327161344 | 1454264320 | 1446135398 | 1303608524 | 1458585600 | 1327161344 |
| 20,000 | 1501503488 | 1456799744 | 1560170496 | 1556833075 | 1391149875 | 1562296320 | 1456799744 |

The fresh city started above 3,000. The target never removes people; 3,222 persisted across two consecutive midnights, satisfying the stabilization stop. The later targets reached exactly. Growth stayed outside sampling. Save readback before/after the sequence stayed at `default_allowed=1`, `armed_allowed=0`, `unrestricted=0`, with saving/compression idle; all automatic attempts were refused. The one allowed save was the initial fixture checkpoint, not a measured-window save.

With `incremental=true`, `pause_ms` describes cycle-ending frames and understates total collection work. Across these observed windows, plain-frame p95 stayed 38.354–38.639 ms; maximum frame time was not monotonic with population. GR-5 now has the requested size measurements, including exact 10,000 and 20,000 populations. These data do not identify a code cause or verify GR-5's broader gameplay/performance target. P-D check 5 and the non-FT refusal check remain incomplete.

| P-D check | Result | Evidence |
|---|---|---|
| 1. Sampler off costs nothing | **PASS** | Same RLO 1.0.6 city/checkpoint, headless, 1×, 120-second union windows: candidate p95 4.8769 ms, previous head 4.8969 ms (−0.0200 ms, −0.41%). The earlier candidate window was 4.9141 ms. Previous-head p95 lies within those candidate observations. No GC command preceded the matched candidate window; no gc report line was emitted. Both matched windows have complete 61-entry-point coverage, valid timing and zero dropped frames. |
| 2. Near 3,000 persons | **PASS** | Full 600-real-second window at actual 3,222; stabilized at two midnights, zero dropped frames. Requested and actual sizes are kept distinct. |
| 3. 10,000 persons | **PASS** | Same city; full 600-real-second window, exact target and zero dropped frames. |
| 4. 20,000 persons | **PASS** | Same city; full 600-real-second window, exact target and zero refusals/dropped frames. |
| 5. Report does not sample itself | **PARTIAL** | Three reports ten seconds apart gave frames 284→566→850; stop gave 853. Max frame grew 174.007→208.456→298.525 ms; gen0 was 0→0→1. Frame growth is observed, but these aggregate reports cannot attribute the maximum increases or establish that reporting caused none of them. No weaker pass criterion is substituted. |
| 6. Target gone after reload | **PASS** | Cold restart/reload of the original FT city without saving the measurement state: `target=off reached=3222`. #70 suppression confirmed after reload. |
| 7. Ordinary save refused | **NOT RUN** | The supported new_game path refuses the disposable non-FT name before creating it. No ordinary/Peter save was used and the mod's own refusal was not exercised. |

Sampler-off windows and every recorded union-work spike >=30 ms:

| Window | Seconds / frames | Mod-work p95 / max / spikes | Whole-frame p95 / max / spikes |
|---|---|---|---|
| Initial candidate | 120.416 / 3152 | 4.9141 / 44.7533 / 1 | 64.4839 / 176.2406 / 3035 |
| Matched candidate | 120.375 / 3081 | 4.8769 / 34.3841 / 1 | 67.7599 / 209.623 / 2931 |
| Matched previous head | 120.395 / 3084 | 4.8969 / 33.5161 / 2 | 69.3143 / 283.8007 / 2928 |

| Window / Unity frame | Mod-work ms | Whole-frame ms | Dominant component |
|---|---|---|---|
| Initial candidate / 4748 | 44.7533 | 94.5057 | PopulationRuntime.Update |
| Matched candidate / 4085 | 34.3841 | 172.3043 | PopulationRuntime.Update |
| Previous / 3436 | 33.5161 | 68.0442 | PopulationRuntime.Update |
| Previous / 3911 | 31.5054 | 68.3142 | PopulationRuntime.Update |

### RLO #239

Both fixtures used the documented Mac recipes with unique id/name/checkpoint overrides, $100M funding/readback, staffed registers on cabinets and independent ready=true reads. Food: `ba:street_fourthavenue#19`; service: `ba:street_fourthavenue#23`. The service package included the head wash and all four service offers. The source/default JSON was not edited; the test override was staged through the sanctioned helper. During overlap, rlo.donor reported transient Refreshing with its saved donor catalogue in use; checkout stayed active.

| Check | Result | Observation |
|---|---|---|
| 1. Living on, owned food shop | **PASS** | The shop became managed at 10:00. From 10:00–12:00, native revenue $35→$110 matches 15 paid Persistent City receipts of 500 cents. The native log has exactly 15 visits and 15 croissant orders at $5, plus 15 normal zero-price paper bags, with no other product lines. No dropped entries or hook faults. Two native draws and hourly batches were suppressed. Checkout remained active; hooks list all five RLO calculator replacements as overruled; status says Living population on. |
| 2. Living off before load | **NOT RUN** | The pre-load set-false request failed with `ops_rlo_option_unavailable`; no off-state city was loaded. |
| 3. Mid-session toggle | **NOT RUN** | The set-false request failed with the same error. No successful transition, two-toggle status or toggled sales interval is claimed. |
| 4. Crowds | **PARTIAL** | Native walking at the RLO university, 26 Second Avenue, and background views ran. Activation/status count 574 native zones, 115 RLO zones, and report ordinary standers suppressed. Street-performer/homeless preservation and RLO CityPeople identity were not independently established in the views. The retained campus views are roof-occluded, so they are not evidence of an unobstructed crowd census. |
| 5. Traffic and RLO scenes | **PARTIAL** | A full 600.544-real-second pavement walk ran at 1×, with 363/363 arrived=true, complete-path segments and periodic background captures. The initial off-pavement waypoint refusal is recorded below. Activation logs 2/2 traffic scene switches stood down and “a scene under way ended”. Physical player-car driving, a controlled rival-door event, scene-car retention and the second Accidents-off-during-hold arm were not run; the rig exposes no supported control for those steps. Cleanup release evidence is below. |
| 6. Native-owned non-food / need basket | **PARTIAL** | Need purchases were enabled and the runtime attached without fault. The ready hairdresser remained unmanaged by food authority (`BusinessContract`, next boundary native). The 13:00–14:00 observation produced two ordinary native visits and sales ($90→$125), but natural need status stayed bought=0/refused=0 with no resident purchase receipts. RLO loyalty/interiors values were not exposed by the queried native Mods page or supported rlo_option names. Therefore no resident basket/one-entry-retirement comparison is claimed. |

The owned-food reconciliation uses actual saved `persistentcity:food-sale-v1:*` paid values, not a probe or a zero-sales equality. Its extraction is `<stage>/rlo-owned-12-pcsave.json`; the compact arithmetic is `rlo-on-reconciliation.json`. A purchase logger added `agent-rig.shop-log` observer postfixes after every performance window and before the sales interval. The hooks report identifies them; no foreign refusing prefix was added. The non-food hour used the logger independently and did not substitute ordinary native customers for residents.

### GR-9 and background capture

Five initial background `capture take` calls completed with `capture_method=render_texture`, `fallback_reason=ops_view_capture_background`, Metal, 640×480 PNGs and live rig-ops readiness afterward. Actual game/UI pixels were inspected; changed UI/view content appears in subsequent captures. The busy-street sequence then produced 94 PNGs over 121.142 seconds, all render_texture; median spacing 1.271 s (range 1.137–1.895 s). No TransformStreamHandle error or capture-induced client exit occurred. No capture-crash restart was needed. The ordinary comparison/reload restarts were separate lifecycle steps. Captures ran after all P-D timing windows.

Only the capture-dependent rows that can run without player-car control or walker/resident identity binding were re-run. Other earlier results are not silently upgraded.

| GR-9 row | Result | Observation / remaining coverage |
|---|---|---|
| 1. Walker fade-in | **PARTIAL** | Two-minute street sequence has moving walkers and clear pavement, without the prior central modal. Its 1.271-second median spacing cannot resolve a 0.75-second fade or exclude a pop between images. |
| 2. Mode-change fades | **NOT RUN** | No supported resident→walker→own-car binding for following both transitions. |
| 3. Parked cars across reload | **NOT RUN** | The full row needs model/body/owner binding and the owner's departure/destination transition; those capabilities remain absent. Not re-run as a weaker logical-record comparison. |
| 4. Own cars / no borrowing hooks | **PARTIAL** | No physicalvehicles/physicalintersections owner in the retained hooks report; ownCars was positive and Personal was held. Observed ownCars reached 3; retired rose 0→55 and loadsRetired 0→44 during the session. The full-modpack log still contains DrivenRides Patch DrivingAI.WaypointRequested, Patch GameSpeedController.SetPause and Patch TrafficManager.FixedUpdate lines. Taxi/service driving was not identified independently, so the full literal criterion is not met. |
| 5. Wardrobe and looks | **NOT RUN** | Opaque walker handles do not bind to a Persistent City resident/donor across reload. |
| 6. 96/48 performance | **NOT RUN** | Outside this capture-fix rerun; the earlier provisional FAIL remains. The sampler-off windows are not a replacement cap comparison. |
| 7. Native/player traffic against own cars | **NOT RUN** | Physical player-car control and a controlled junction sequence are absent. |
| 8. Parked occupancy | **NOT RUN** | No controlled two-resident/curb/player-car/bollard fixture or identity binding. |
| 9. Own cars stop for traffic/player car | **NOT RUN** | No stopped player-car / red-light / adjacent-turn fixture or control. |
| 10. Own cars stop for walkers | **NOT RUN** | No identified own-car / bound walker crossing or exposed layer readback. Casual walking is not collision proof. |

### Tooling gaps, exact refusals and corrected inputs

| Operation | Exact outcome | Consequence |
|---|---|---|
| `python3 scripts/mac-agent op rlo_option '{"action":"read","name":"living_population"}'` | `ops_rlo_option_unavailable` | Original live on-state was read from capability status; stored-value readback unavailable. |
| `python3 scripts/mac-agent op rlo_option '{"action":"set","name":"living_population","value":false}'` (once for each requested arm) | `ops_rlo_option_unavailable` | #239 checks 2 and 3 NOT RUN. No UI toggle, file edit or replacement RLO was used. |
| `python3 scripts/mac-agent call new_game '{"name":"PD-GC-Refusal-13d"}'` | `The bridge starts only games whose name starts with FT, not 'PD-GC-Refusal-13d'.` | P-D 7 NOT RUN. The guard was not bypassed. |
| `op ops {}`, `op screen_pos list`, `call drive {"op":"status"}` | The schemas have opaque walker/business/position targets, no stable resident/donor/car binding or physical ordinary-player-car control. Drive status returned `running:false, phase:none`; the documented drive path is an FT simulated trip. | Binding/car-control rows remain NOT RUN; no invented failing op is used as evidence. |
| `call ui {"op":"escape"}` from native Options | `Nothing is open to close (Esc would open the pause menu).` | Closed through the observed Back [Escape] and MiniMenu Close buttons. No setting value was changed. |
| Initial campus walk to x −163.39, z 335.27 | `No walkable ground within 3 m of x -163.4, z 335.3.` | Stopped the attempt safely after two successful pavement walks; restarted the full ten-minute walk on the verified two-point pavement route. |

Two input mistakes were refused before the affected action: the stage recipe used `FT-RLO...` for a display name instead of required `FT ` (`Shop name must start with "FT " so it stays recognisably disposable`); and the first teleport omitted `who` (`Missing "who": "player" or a food vehicle's id.`). Both were corrected and the documented commands succeeded. An obsolete CloseButton request after collapsing the campaign panel returned `No button or option reads "CloseButton".`; a fresh state read showed no held screen. These are setup corrections, not product failures or capture crashes.

### NOT RUN grouped

- P-D 7: supported city creation/console guards do not provide the non-FT fixture path.
- RLO #239 2–3: Living-population read/set operation unavailable. The off-before-load and two-toggle scenarios remain unverified under 1.0.6.
- GR-9 2, 3, 5, 7, 8, 9, 10: controlled physical player-car and/or stable resident/donor/body bindings are absent. GR-9 6 was deliberately not re-run because it did not depend on the background-capture fix.
- Partial portions: report self-isolation, subsecond fade observation, RLO scene/crowd identity preservation, controlled traffic and option-release arms, and a non-food resident basket purchase with verified RLO loyalty/interiors settings.

### Fixtures, restoration and validation

Created fresh cities: `FT-PD-GC-13d` and `FT-RLO-13d`. Created checkpoints: `FT RLO 13d Food v1`, `FT RLO 13d Owned 12`, `FT RLO 13d Service v1`, `FT RLO 13d Service 14`. The planned `PD-GC-Refusal-13d` was not created. Disposable fixture saves stay in the isolated profile. Actual saved population payloads report schema 1; no measurement target was saved.

The Living restore request, before stopping, also returned `ops_rlo_option_unavailable`. No Living mutation had succeeded; capability status was **on before and after** the restore attempt, matching the original live state. The stored option/journal could not be read through that op. The original isolated preferences were restored and verified independently.

All four game processes stopped normally, without `--force`. The final log reports `[Persistent City] RLO traffic scene switches given back (the player's stored options).` No after-release options-screen verification is claimed.

The execution layer rejected the printed destructive shell rollback before it ran: **`rm -f style commands are not permitted. Use a safer approach`**. Restoration then used a non-destructive equivalent: the staged PersistentCity folder was atomically moved to `<stage>/retained-staged-mod`, the initial `20261113T175700Z/mod` copied into the empty original path, and that backup's unchanged install.json copied back. The staged candidate remains retained for review. No receipt was hand-edited and no RLO backup was restored.

Verification: **34 original-file hashes matched; 0 mismatches**. The full original PersistentCity file manifest matched, including removal of the staged locale addition. Isolated preferences/configuration match their original hashes. The two preexisting RLO diagnostic text files were checked read-only, never restored. **All 53 RLO mod-file hashes match.** install.json SHA-256 is **`6668d8ef07ae4bc4281a8ac53f7611edeb85a2068ee6b60f8bdb6c45284b846b`**, equal to both the initial independent copy and the helper's backup. AgentRigOps retained its recorded hash. Autosave preferences returned to their original bytes; process-only suppression ended with the game.

Final status: `running=false`, `process=null`, lease `held=false`, `pending=0`. None of the four owned game PIDs (22012, 32132, 32403, 32630) or their caffeinate assertions remains. No post-restore game start was needed. Only the two 13d worktrees are removed after the evidence commit and PR update; 13b/13c and other sessions' leftovers remain untouched.

Candidate and previous-head builds passed. Final checks: JSON parses with all three complete report/human lines; all requested person observations and zero dropped samples match raw reports; autosave guard counts show no measured save; 363 complete walking segments; full RLO folder manifest unchanged; restore hashes and stopped/free-lease state verified. `python3 tools/docs-links/check.py`: 20 Markdown files, 0 broken relative links. `python3 tools/md-ban.py --base origin/codex/persistent-city`: no new markdown files. `git diff --check`: clean. No test source changed; no full xUnit or regression suite ran.

## Rig results — 13 November 2026 e, Mac (GR-5 at 5fe5eb23)

Role `rig_test`, where `mac`, isolated `agentmac`. **0 passed / 1 partial / 3 failed / 1 NOT RUN. RESULT: BLOCKED.** Check 1 has partial coverage and an observed broader mod-work gate failure. In-game proof gates no merge. This is the requested 13 November e session; work crossed into 14 November local time. The JSON records UTC window times.

Timing is **provisional on the shared Mac**: other coding and desktop sessions were live. Their work, files, branches and processes were not changed. No Windows PC was contacted or started.

`<stage>` = `/Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13e-mac`. Raw logs, native save profiles, screenshots, fixture extraction, requests, original-file backups and restoration evidence stay there. Report copy: `/Users/peterhartwieg/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-13e-mac.md`. [Compact GC/save evidence](perf/gr5-2026-11-13e-mac.json): 16,764 bytes, SHA-256 `5bad8740f95deef3c88483c347ee5e10644553602f9cd3584a2af3d916646905`. It includes every complete GC JSON/human line, every listed mod-work spike and all 20 valid save rows.

### Build and rig

| Item | Observation |
|---|---|
| Candidate | `5fe5eb23b3be9dcf4843592de56288abfde0193c` (#246). `origin/codex/persistent-city` was unchanged at the initial and final fetch. |
| Evidence/build checkout | New `codex/mac-rig-2026-11-13e`, based on that candidate, in `rig-2026-11-13e-mac-evidence`. One isolated worktree served build and evidence. The original Persistent City checkout was not edited. |
| Build | `python3 tools/build.py --game-managed "/Users/peterhartwieg/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0; `/opt/homebrew/bin` prepended to PATH, DOTNET_ROOT = Homebrew dotnet prefix + `/libexec`. |
| PersistentCity DLL SHA-256 | `a4991f98e95cda22318f100aacf0c7edd7f8fdfbfee069ce82c95c61ae3c30b5`. The staged DLL matched the built package; startup verified the receipt. |
| Agent-rig HEAD | `0b18d38ece6e8fe77c35ee5ea694849c7478d054`, clean before and after; fetch/pull were already up to date. No edits, commits or pushes to that checkout. |
| Live AgentRigOps SHA-256 | `5578f9f4657fa2269ee50aa86adfc6d699881fac7e102084918da625045cd59e`, unchanged at cleanup. Live ops reported ready. |
| RLO version | Player.log: `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` RLO files/options were not staged or edited. |
| Staging command | `python3 scripts/install-mac.py --stage-mod PersistentCity /Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13e-mac/package` |
| Receipt backup | `/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261113T223637Z` |
| Lease/resources | Initially stopped/free; acquired 22:36:08 UTC. 54.8 GiB disk and 84% memory free. No takeover or resource override. |
| Launches | `start --mode headless --rig-test` twice (PIDs 72169, 79205); `start --mode background --rig-test` once (PID 79665). Each start succeeded on its first attempt. |
| Autosave | Original native setting 5 minutes / 3 saves. #70 `autosave` readbacks confirmed suppression after each load and before/after every measured window. All three windows have allowed=0, native_saving_frames=0 and clean=true. |
| Configuration | Candidate defaults throughout. Price/household weights 1.2/1.0. PC SaveCostDiagnostics remained false. Only the separate save pass used the rig request `{"save_specific":true,"allow":["Default"]}`. Saved schema is 1. |
| Build-line limitation | Player.log has the Persistent City activation line but no commit-stamped build line. Identity comes from the matching package/live DLL and receipt validation; no log build stamp is claimed. |

### Per-check results

| #246 check | Result | Observation |
|---|---|---|
| 1. Frame time | **FAIL; partial coverage** | Exact 1,000 residents + 2,000 visitors, target off, 20 subway portals, zero fill refusals. Both 600-second 30-cap windows completed. UpdateCity p95 stayed below 5 ms; broader shop mod-work p95 **5.0406 ms** fails the project gate. The shop arm achieved only **5.92 fps**. Both 60 fps arms are NOT RUN because the supported request was refused. |
| 2. Save latency | **FAIL** | 20 valid paused-city manual saves: PC main-thread p50/p95/max **551.221/731.144/733.817 ms**; p95 target <40 ms. Whole native Save main-thread p50/p95/max **745.126/996.450/1011.803 ms**, separately. |
| 3. GC | **FAIL** | Separate cold-reload, settled, 600-second P-D window with modwork disarmed: **10 gen0**, pause p95 **66.260 ms**, Mono heap **1,561,460,736 bytes**. Expectations: at most 5, 35.9 ms, 1.47 GB. |
| 4. Unrentable footprint | **NOT RUN / tooling gap** | No footprint-derived Home place was exposed. Saved catalogue: 557 residential Homes, all interior-area evidence, plus 20 subway portal places. Neither exposed Home fields nor ops provide native bounds/storeys for matching the three required buildings. Captures work, but cannot supply that missing correspondence. No substitute rentable-building comparison is counted. |
| 5. Unreadable/empty tally | **PARTIAL** | Initial load and both reloads log `unrentable homes housed=0 beds=0 unreadable=0 empty=0`; no Residential places omitted warning. The line is read; no under-one-storey or unreadable candidate was exercised, so their classification/warning distinction remains unverified. |

The same fresh `FT-GR5-13e` city was filled across midnight, then sampled with 3,000 persons. No measurement population target was set. Street: player x=215, z=0. Shop: staffed/open `FT GR5 13e Food`, `ba:street_fourthavenue#21`, entrance x=176.142853, z=-36.35211. Shop revenue rose $65 to $180 and croissants fell 87 to 64 during its window. Before sampling it had already sold 13 croissants. The GC comparison cold-loaded `FT GR5 13e Timing v1`, returned to the street and settled one game hour outside timing.

### Frame time and spikes

All times are ms; fps is stored native window frames / real seconds. The 30 fps value is the requested rig cap, not an assertion of achieved cadence. P-D plain UpdateCity and explicit UpdateCity component percentiles are both retained. Whole-frame values come from the separate native `perf` window.

| Window | Seconds / frames / achieved fps | UpdateCity p95 plain / component | Broader mod-work p95 / max / spikes >=30 | Whole-frame p95 / max / spikes >=30 |
|---|---|---|---|---|
| street-30 | 600.408 / 17110 / 28.50 | 3.890 / 3.8978 | 3.9507 / 100.5736 / 4 | 36.7802 / 304.2106 / 17060 |
| shop-30 | 600.622 / 3554 / 5.92 | 4.997 / 4.9972 | 5.0406 / 43.0733 / 1 | 169.3709 / 304.5454 / 3554 |

Both modwork windows report complete 62-entry-point coverage, valid timing and zero dropped frames. Every exported mod-work spike >=30 ms is listed below; no spike was chased.

| Window / Unity frame | Mod-work ms | UpdateCity component ms | Whole-frame ms in that modwork row |
|---|---|---|---|
| street-30 / 12719 | 100.5736 | 100.5105 | 143.5717 |
| street-30 / 12781 | 30.3934 | 30.3449 | 66.5047 |
| street-30 / 12788 | 38.3335 | 38.2870 | 66.3306 |
| street-30 / 12792 | 60.8035 | 60.7607 | 95.9041 |
| shop-30 / 32241 | 43.0733 | 43.0271 | 193.1222 |

The separate GC-only pass also flags UpdateCity maxima **68.541 ms on plain frames** and **52.530 ms on GC frames**. That sampler exports maxima, not individual mod-work spike rows; those rows/counts are unavailable in this pass. Whole-frame intervals commonly exceed 30 ms at a 30 fps cap and remain separate from mod work.

### Save latency

The native profiler times `PersistentCity.Runtime.PopulationRuntime.Save` in place on the main thread; PC's record-count diagnostics were off. Whole native `Save` phase time includes the rest of that call and is shown separately. Compression ran on its named worker thread and is excluded from main-thread columns. Each next attempt waited for save/compression idle.

The first attempt was too early: the log says `[Mod:PersistentCity] Persistent City skipped the save during initialization; no Persistent City state was written: Persistent City initialization has not completed.`. Its PC callback took 0.509 ms, native Save 460.704 ms; it is retained as invalid, excluded for that explicit initialization refusal. Attempt 21 replaced it. Samples below are all **20 valid attempts 02–21**; all returned, none threw, each retained the same 9,363,620-character PC payload. Percentiles use nearest rank (10th and 19th sorted observations); no valid warm sample was discarded.

| Valid sample / checkpoint suffix | PC main-thread ms | Whole native Save main-thread ms |
|---|---|---|
| 1 / 02 | 599.606 | 779.435 |
| 2 / 03 | 568.778 | 745.126 |
| 3 / 04 | 516.741 | 716.789 |
| 4 / 05 | 731.144 | 980.679 |
| 5 / 06 | 514.815 | 696.991 |
| 6 / 07 | 551.221 | 750.381 |
| 7 / 08 | 517.043 | 701.865 |
| 8 / 09 | 555.988 | 759.827 |
| 9 / 10 | 573.853 | 791.106 |
| 10 / 11 | 523.417 | 704.978 |
| 11 / 12 | 581.189 | 762.802 |
| 12 / 13 | 512.942 | 697.476 |
| 13 / 14 | 733.817 | 996.450 |
| 14 / 15 | 522.438 | 705.399 |
| 15 / 16 | 576.107 | 762.761 |
| 16 / 17 | 518.285 | 699.884 |
| 17 / 18 | 516.532 | 695.133 |
| 18 / 19 | 577.696 | 756.426 |
| 19 / 20 | 529.525 | 712.989 |
| 20 / 21 | 594.261 | 1011.803 |
| p50 | 551.221 | 745.126 |
| p95 | **731.144** | **996.450** |
| max | 733.817 | 1011.803 |

At 1,000 residents this observed city does not meet the 40 ms save budget. No lower cap was measured and no code cause is inferred. The chosen cap is not verified by this run.

### GC

The primary comparison is `gc-clean`: a cold process, candidate defaults, one settling hour, modwork disarmed. Its complete `[Persistent City] gc report {json}` line is in the linked JSON. The first two rows are the frame-time windows with modwork armed and remain separately labelled.

| Window | Persons / frames / dropped | gen0/1/2 | pause p50 / p95 / max ms | Mono heap bytes | sampled heap peak bytes |
|---|---|---|---|---|---|
| street-30 | 3000 / 17103 / 0 | 10/0/0 | 29.574 / 65.087 / 65.087 | 1767194624 | 1766961152 |
| shop-30 | 3000 / 3551 / 0 | 4/0/0 | 34.851 / 37.354 / 37.354 | 1809498112 | 1805901824 |
| gc-clean | 3000 / 17012 / 0 | 10/0/0 | 29.844 / 66.260 / 66.260 | 1561460736 | 1545633792 |
| 13d reference | 3222 / 16960 / 0 | 5/0/0 | 33.526 / 35.941 / 35.941 | 1471660032 | 1471025152 |

Incremental GC was true; `pause_ms` measures cycle-ending frame excess, as in 13d, rather than total incremental collection work. `gc-clean` had 600.392 native window seconds, zero dropped GC samples, no allowed save, and whole-frame p95 37.8646 ms. All three requested GC comparison limits were exceeded.

### Exact operations, gaps and NOT RUN

| Operation | Exact result / gap |
|---|---|
| Documented `agent-frame-cap.json` request `{"fps":60,"vsync":0}`, then `start --mode headless --rig-test`; `health` readback | `frame cap requires a rig-test windowed launch`; requested=null, applied=false, target_frame_rate=30. `mac-agent --mode` exposes only background/headless. Both street/shop 60 fps arms NOT RUN. No launcher bypass. |
| `op pcstate {"action":"list"}` | Eight diagnostics projections; none for Home footprint/bounds/storeys. `op ops {}` has no native building-bounds/storeys operation. |
| `op pcsave {"action":"take","name":"FT GR5 13e Save 20","timeout_s":180}` | Success, schema 1, 1,000 citizens, 2,000 visitors, 1,690 places; 557 non-portal Homes, zero building-bounds evidence. Home fields expose usable area/evidence but not raw bounds/floor count. Required three-building footprint/floor comparison NOT RUN. |
| `op capture {"action":"take"}` | Two successful 640x480 Metal render_texture captures, fallback_reason=`ops_view_capture_background`; initial campaign overlay then clear street inspected. No footprint/storey proof is claimed from them. |
| `op rlo_option {"action":"read","name":"living_population"}` | `ops_rlo_option_unavailable`. Optional #239 checks 2–3 NOT RUN; no option mutation occurred. |
| `call drive {"op":"status"}`; `op ops {}` | running=false, phase=none. No supported physical ordinary-player-car control or walker-to-resident identity binding in the exposed schemas; no such check is claimed. |
| `call new_game {"name":"GR5-NonFT-13e"}` | `The bridge starts only games whose name starts with FT, not 'GR5-NonFT-13e'.` No non-FT city created. |
| Printed rollback shell | Rejected before execution: `rm -f style commands are not permitted. Use a safer approach`. Non-destructive restoration below completed instead. |

NOT RUN grouped: check 1's two 60 fps arms; all of check 4's rotated/ground-overlap/third-building comparison; optional #239 checks 2–3. The unreadable/empty classification cases remain the partial portion of check 5. The clean GC sampler's per-spike rows and a commit-stamped PC log build line are tooling limitations.

Corrected setup inputs: pre-load `op ops`/`op pcstate list` returned `ops_city_required`; early console calls said `console works in the city only; the game is at the loading now.` `call ui {"op":"state"}` returned `Unknown ui op 'state': list, click, set, type, escape or tree.`; `ui list` was then used. Speed 100 returned `speed must be above 0 and at most 60.`; fill used accepted speed 60, while every measured window used 1x. The campaign overlay was collapsed before time-based sampling. These are setup/tool refusals, not additional product failures.

### Fixtures, restoration and validation

Disposable city `FT-GR5-13e`; checkpoints `FT GR5 13e Food v1`, `FT GR5 13e Timing v1`, `FT GR5 13e Save 01` through `21`. Attempt 01 is the invalid initialization-time save; 02–21 are the 20 valid measurements. Fixture saves stay only in the isolated profile. The non-FT probe name was not created.

Independent originals/manifests were taken before staging. The helper printed this rollback verbatim:

```sh
rm -rf "/Users/peterhartwieg/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261113T223637Z/mod" "/Users/peterhartwieg/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261113T223637Z/install.json" "/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

The execution layer rejected that shell before it ran. With game stopped and lease held, the staged mod was moved intact to `<stage>/retained-staged-mod`; the helper's original `mod` folder was copied into the empty live path and its unchanged `install.json` copied back. Original preferences/root configuration were copied back; RLO files were checked read-only. Both request files returned to their original absent state. No receipt was hand-edited.

Verification: **35 original-file hashes matched, zero mismatches; the complete PersistentCity file set matched, including removal of the added locale; all 53 RLO hashes matched**. Original preferences/config/autosave bytes match. install.json SHA-256 **`1d3243f0854087b6d61603fb9611df1048f19a3b8189b61b362bcdc13555d2a9`** matches both independent and helper backups. AgentRigOps hash stayed unchanged. Process-only autosave suppression ended at stop.

All three game stops were normal, without force. Owned PIDs 72169/79205/79665 and their caffeinate assertions are gone. Final `status`: running=false, process=null, lease held=false, pending=0. The official agent-rig checkout remains clean at its recorded HEAD. Only this run's worktree is removed after its evidence commit is pushed and its PR is opened; other sessions' work is untouched.

Candidate build passed. Evidence JSON parses; all three native timing windows are save-clean; 20 valid save profiles and all listed mod-work spikes were reconciled to raw records. Documentation links passed (20 Markdown files, zero broken relative links); markdown ban passed (no new Markdown paths); diff whitespace and the under-100,000-character diff limit passed. No unit-test source changed, so no full xUnit or regression suite ran. No production code, version/changelog, RLO file, schema or food-choice weight changed. No merge or release action.

## Rig results — 14 November 2026 f, Mac (GR-5 at d237f443)

Role `rig_test`, where `mac`, isolated `agentmac`. **1 passed / 2 partial / 2 failed / 1 NOT RUN. RESULT: DONE.** These six rows count #248 checks 1–3 and the GR-5 street, footprint and tally rows once each; unavailable sub-arms are grouped below. The requested run completed; DONE does not mean the save or GC gates passed. In-game proof gates no merge.

All timing is **provisional on the shared Mac**: other live coding sessions were observed by executable name (12 matching processes at preparation), without inspecting or changing their work. No Windows PC, Steam app, normal profile or ordinary save was used. No production source, schema, RLO files/options, price/household weights, version or changelog was changed. No release, Workshop upload, issue creation or merge occurred.

`<stage>` = `/Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13f-mac`. Raw logs, complete native save profiles, diagnostics, extracted fixture payloads, backups and restoration evidence stay there. Report copy: `/Users/peterhartwieg/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-13f-mac.md`. [Compact save/GC/traffic evidence](perf/gr5-2026-11-13f-mac.json): **12,635 bytes**, SHA-256 `eeeb288985b73d3416e4ad70a80ab7b0aedee56d470858ddebe72356e0252503`. It retains the complete GC report lines, all 20 valid save phase/section rows and all exported mod-work spikes. Its raw-evidence manifest gives SHA-256 and filenames for the larger records in the stage.

### Build and rig

| Item | Observation |
|---|---|
| Candidate | `d237f443c689ec946484832443d8adf0f15de852` (#248), unchanged on origin at initial and final fetch. |
| Build/evidence checkout | New `codex/mac-rig-2026-11-13f` from that tip, in `rig-2026-11-13f-mac-evidence`. This one isolated worktree served build and evidence; the original checkout stayed clean. |
| Build | `python3 tools/build.py --game-managed "/Users/peterhartwieg/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0, with `/opt/homebrew/bin` prepended to PATH and Homebrew dotnet `/libexec` as DOTNET_ROOT. |
| PersistentCity DLL SHA-256 | `5d9ac8542e23bcc570af0b2f0098aa976fe922d2e562a137555428266891d7e6`; live staged DLL matched, and each start validated the install receipt. Both staged packages contain this DLL. |
| Agent-rig HEAD | `0b18d38ece6e8fe77c35ee5ea694849c7478d054`, clean before/after; fetch and ff-only pull were already up to date. No edits, commits or pushes to that checkout. |
| AgentRigOps SHA-256 | `5578f9f4657fa2269ee50aa86adfc6d699881fac7e102084918da625045cd59e`, unchanged at cleanup; live ops reported ready. |
| RLO | Player.log: `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` No RLO package or option was staged or edited. |
| PC log identity | Fresh activation: `Persistent population active: 1000 residents, 1000 visitors, 1683 places.` After fill, readback is 1,000/2,000. The log has no commit-stamped PC build line; identity is established by the matching DLL and receipt validation, rather than an invented log stamp. |
| Lease/resources | Initially stopped/free; acquired 2026-11-14 01:49:46 UTC. 54.8 GiB disk / 78% memory free. No takeover, resource override or process cancellation. |
| Launches | Six headless `start --mode headless --rig-test` launches, each successful on its first attempt. Cold starts covered save profiling, GC, the separate street window and both old-format loads. Every stop was graceful; no --force. |
| Autosave original | Native 5 minutes / 3 saves. #70 process suppression was confirmed after each load and before/after every measured window. Original preference/config bytes are restored; the process-only guard ended at stop. |
| Configuration | SaveCostDiagnostics=true for save-pass preparation/profiling; staged false before the cold GC run and kept false thereafter. Only the save-profiling process used profile `agent-save-mode.json` with `{"save_specific":true,"allow":["Default"]}`. FoodChoicePriceWeight/FoodChoiceHouseholdWeight stayed 1.2/1.0. Schema stays 1. |

Both stage commands ran from the official agent-rig checkout with the game stopped and lease held. Packages contain no RLO files. The first backup is the restoration source, not the later diagnostic package backup.

| Exact command | Receipt backup |
|---|---|
| `python3 scripts/install-mac.py --stage-mod PersistentCity /Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13f-mac/package` | `/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T015030Z` |
| `python3 scripts/install-mac.py --stage-mod PersistentCity /Users/peterhartwieg/HART-Builds/persistent-city-rig-2026-11-13f-mac/package-default` | `/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T020250Z` |

### Per-check results

| Check | Result | Observation |
|---|---|---|
| #248 1 / GR-5 save latency | **FAIL** | Full PC callback p95 742.826 ms; recorded phase total p95 301.027 ms. Both exceed p95 <40 ms. The full callback includes the explicitly separate diagnostic record count. Whole native Save p95 935.216 ms. |
| #248 2 / GR-5 GC | **FAIL against the 13d reference** | Day: 12 gen0 / pause p95 65.028 ms / heap 1,493,630,976 bytes. Night: 6 / 31.977 ms / 1,491,255,296 bytes. Night pause is below 13d; night collections and heap are higher. No runtime exception; the observed comparison limits are exceeded. |
| #248 3 / old-format reload | **PASS** | The 5fe5eb23 fixture's arrays loaded, ran 2.022 game minutes, saved with string traffic and cold-reloaded without refusal. Waiting queue state survived; an in-flight car advanced nine route legs. The post-reload traffic projection is identical to the converted checkpoint. |
| GR-5 1 / street frame time | **PARTIAL** | Requested 600 s at 30 fps completed. UpdateCity p95 4.2071 ms; broader mod-work p95 4.2557 ms, both <5 ms. Two >=30 ms spikes are listed below. The 60 fps arms remain NOT RUN; the shop arm was outside this requested street rerun. |
| GR-5 4 / building bounds/storeys | **NOT RUN / tooling gap** | `op ops {}` and `op pcstate {"action":"list"}` expose no native Home bounds/storeys correspondence. No rotated/ground-overlap/third-building visual comparison was substituted. |
| GR-5 5 / unreadable-empty tally | **PARTIAL** | Fresh activation and reloads log `unrentable homes housed=0 beds=0 unreadable=0 empty=0`. The line is verified; unreadable and under-one-storey fixtures were not exercised, so their classification distinction is not established. |

### Save latency and per-section costs

**FAIL.** Twenty valid paused-city saves, attempts 02–21, with SaveCostDiagnostics=true and the native save profiler installed. Attempt 01 is excluded for the exact initialization refusal: `Persistent City initialization has not completed.` It has no successful diagnostic save record; attempt 21 replaces it. Every next attempt waited for native save and compression idle. No valid warm sample was discarded. Percentiles use nearest rank (10th and 19th of 20).

| Measurement | p50 ms | p95 ms | max ms |
|---|---|---|---|
| Full PC main-thread callback | 649.028 | 742.826 | 800.880 |
| Whole native Save main thread | 811.508 | 935.216 | 994.246 |
| PC recorded phase total | 233.823 | 301.027 | 303.928 |
| captureMs | 18.792 | 22.188 | 25.553 |
| validateMs | 51.373 | 86.864 | 91.344 |
| encodeMs | 148.096 | 177.431 | 205.459 |
| recordCountMs (outside phases) | 372.582 | 507.699 | 573.419 |

The full PC callback includes the diagnostic record count; `recordCountOutsidePhases=true` explicitly excludes that count from `totalMs` and the phase columns. Both the full callback and the recorded phase total exceed the p95 <40 ms target. Compression uses the named worker thread and is excluded from the main-thread columns.

13e, diagnostics off: PC callback 551.221/731.144/733.817 ms; whole native Save 745.126/996.450/1011.803 ms. This pass has diagnostics on, so its callback comparison includes additional measurement work. #248 projected about 415 ms; that projection is not substituted for these observations.

Payloads span **5,791,344–5,798,806 characters**, against 9,363,620 in 13e and about 6.2 M expected by #248. The extracted fresh fixture has 1,000 residents, 2,000 visitors, 1,683 places and 13,965 traffic links/definitions. It is a different saved city from 13e's 1,690-place fixture. Schema is 1; Traffic.Links and Traffic.Definitions are strings.

| Section | Encode mean ms | Encode p95 ms | Validate mean ms | Validate p95 ms |
|---|---|---|---|---|
| header | 0.056 | 0.060 | 0.001 | 0.001 |
| places | 25.258 | 25.422 | 2.468 | 2.869 |
| residents | 59.984 | 84.657 | 20.319 | 23.571 |
| visitors | 42.627 | 48.522 | not exported | not exported |
| traffic | 6.855 | 7.157 | 19.556 | 44.719 |
| food | 16.870 | 17.023 | 12.244 | 14.555 |
| economy | 0.027 | 0.029 | 0.624 | 0.764 |

Validation exports no independent visitors field. The compact evidence retains every sample's capture/validate/encode times and both section maps; full readbacks and native profiles remain in the stage.

| Attempt / checkpoint suffix | PC callback ms | Whole native Save ms | Recorded phases ms | Record-count ms | Payload chars |
|---|---|---|---|---|---|
| 02 | 669.010 | 822.071 | 243.879 | 424.285 | 5,798,806 |
| 03 | 659.091 | 805.678 | 231.358 | 427.708 | 5,798,806 |
| 04 | 649.028 | 796.537 | 227.092 | 421.914 | 5,798,806 |
| 05 | 800.880 | 994.246 | 227.430 | 573.419 | 5,798,806 |
| 06 | 628.130 | 808.629 | 235.450 | 392.657 | 5,798,806 |
| 07 | 670.573 | 816.199 | 303.928 | 366.622 | 5,798,806 |
| 08 | 669.616 | 815.830 | 301.027 | 368.568 | 5,792,868 |
| 09 | 670.026 | 814.780 | 298.986 | 371.020 | 5,791,344 |
| 10 | 664.476 | 811.508 | 253.460 | 410.993 | 5,791,344 |
| 11 | 658.220 | 803.668 | 229.472 | 428.728 | 5,791,344 |
| 12 | 665.670 | 812.479 | 233.655 | 431.992 | 5,791,344 |
| 13 | 647.415 | 925.418 | 233.781 | 413.602 | 5,791,344 |
| 14 | 598.631 | 814.887 | 232.530 | 366.078 | 5,791,344 |
| 15 | 605.430 | 810.528 | 233.429 | 371.979 | 5,791,344 |
| 16 | 604.876 | 808.164 | 232.273 | 372.582 | 5,791,344 |
| 17 | 604.639 | 815.314 | 233.823 | 370.792 | 5,791,344 |
| 18 | 598.014 | 808.733 | 234.933 | 363.058 | 5,791,344 |
| 19 | 598.578 | 768.685 | 235.788 | 362.768 | 5,791,344 |
| 20 | 599.542 | 751.933 | 236.556 | 362.965 | 5,791,344 |
| 21 | 742.826 | 935.216 | 235.104 | 507.699 | 5,791,344 |

### GC windows

The same fresh city and fixed street (player x=215, z=0) were used, at 1×. After a cold reload of `FT GR5 13f Timing v1`, the city settled outside timing to 15:30. The night window followed an untimed advance to 01:00. Mod-work sampling and native save profiling were off throughout both GC windows; SaveCostDiagnostics readback was false. All measured windows have allowed=0, native_saving_frames=0 and clean=true. No save entered native work.

| Window | Native seconds / frames / dropped | Game clock | gen0/1/2 | Pause p50/p95/max ms | Mono heap bytes | Sampled heap peak bytes | Whole-frame p95/max ms |
|---|---|---|---|---|---|---|---|
| gc-day | 600.504 / 16768 / 0 | day 2 15:30–18:24 | 12/0/0 | 25.328/65.028/65.028 | 1,493,630,976 | 1,483,522,048 | 44.4502/236.0063 |
| gc-night | 600.363 / 17076 / 0 | day 3 01:00–04:14 | 6/0/0 | 17.381/31.977/31.977 | 1,491,255,296 | 1,414,967,296 | 37.7826/327.4926 |
| 13d reference | 600 s / 16960 / 0 | 01:10–04:22 | 5/0/0 | 33.526/35.941/35.941 | 1,471,660,032 | 1,471,025,152 | plain-frame p95 38.354 |
| 13e reference | 600.392 / 17012 / 0 | daytime, around 15:27 | 10/0/0 | 29.844/66.260/66.260 | 1,561,460,736 | 1,545,633,792 | 37.8646 |

Night is the requested time-of-day comparison with 13d, which had 3,222 persons; this run and 13e have 3,000. Day versus 13e: gen0 increased 10→12, pause p95 66.260→65.028 ms, heap 1.561→1.494 GB. Night versus 13d: 5→6, 35.941→31.977 ms, 1.472→1.491 GB. This is observed comparison evidence, not a code root cause.

Incremental GC is true and max_generation=0. Pause measures cycle-ending frame excess, not all incremental collection work. Day flags UpdateCity maxima **65.605 ms on plain frames** and **31.580 ms on GC frames**. Night maxima are 6.387 and 4.795 ms. The GC sampler exports maxima, not individual mod-work spike rows; those rows/counts cannot be recovered from this operation. Both complete `[Persistent City] gc report {json}` lines are retained verbatim in the compact evidence.

### Street mod-work and whole-frame timing

The street pass cold-reloaded the same checkpoint and settled to the same 15:30 start. It used a separate 600 s window with the GC sampler off. Autosave was checked before/after. Coverage is complete at 62 entry points, timing valid, zero dropped frames and no native save work.

| Native seconds / frames / achieved fps | UpdateCity p95 / max ms | Broader mod-work p95 / max / >=30 ms spikes | Whole-frame p95 / max / >=30 ms frames |
|---|---|---|---|
| 600.215 / 16771 / 27.94 | 4.2071 / 74.0022 | 4.2557 / 74.0481 / 2 | 44.2859 / 343.1558 / 16428 |

13e street: UpdateCity p95 3.8978 ms, broader mod-work 3.9507 ms, whole-frame 36.7802 ms. The new street observations stay below the 5 ms mod-work gate. They do not replace 13e's separate shop-window result of 5.0406 ms.

| Unity frame | Mod-work ms | UpdateCity component ms | Whole-frame ms in that mod-work row |
|---|---|---|---|
| 3411 | 32.3857 | 32.3608 | 64.5939 |
| 3440 | 74.0481 | 74.0022 | 98.1135 |

These are all exported mod-work spikes >=30 ms; none was chased. Whole-frame intervals at a 30 fps cap commonly exceed 30 ms and are reported separately.

### Old-format save and reload

The retained `FT GR5 13e Save 20` was written by 5fe5eb23. Its source SHA-256 is `a0526acf16f10bac951968f8bf96d368b823743001eb796ba6943c1a3c717297`. Extraction confirmed arrays for Traffic.Links/Definitions, 13,965 entries each, and three live traffic vehicles. `c342` was ready/waiting; `pc:197` and `pc:431` were in flight. No previous build or RLO restaging was needed, and the source save was not overwritten.

A cold load on d237f443 succeeded. At 1× it ran **2.0221786 native game minutes**, then paused and saved `FT GR5 13f Converted v1`. A further cold process loaded that checkpoint, waited for physical initialization, stayed paused and saved the verification checkpoint `FT GR5 13f Reloaded v1`. Autosave suppression was checked after each load. Both extractions have schema 1, string-valued Traffic.Links and Traffic.Definitions, 13,965 records per string and no accounting-stop marker.

| Saved logical traffic | Old source | After two-minute interval / conversion | After cold reload / verification save |
|---|---|---|---|
| Minute | 3747.9336547851562 | 3750.0 | 3750.0 |
| Active traffic vehicle count | 3 | 2 | 2 |
| In-flight example `pc:197` | route index 72, order 173 | route index 81, order 173, now queued | same route index, order and timing |
| Waiting example `c342` | route index 45, order 177 | still queued, same record | same queue entry, order and timing |

The complete converted/reloaded traffic projections compare equal, including queues, IDs, route indices, ReadyAt/EnteredAt/MotionStartedAt and orders. This verifies saved logical traffic continuity; it makes no physical player-car or walker identity claim.

### NOT RUN and tooling gaps

| Exact request / discovery | Result | Coverage |
|---|---|---|
| Isolated profile `agent-frame-cap.json` = `{"fps":60,"vsync":0}`, then `python3 scripts/mac-agent start --mode headless --rig-test`, health readback | `frame cap requires a rig-test windowed launch`; applied=false, target_frame_rate=30 | Both GR-5 60 fps arms NOT RUN. Supported Mac modes are background/headless; no launcher bypass. |
| `python3 scripts/mac-agent op ops '{}'`; `op pcstate '{"action":"list"}'` | Ops have no native Home bounds/storeys operation; eight diagnostics projections, none for Home footprint/floor count | GR-5 4 rotated, ground-overlap and third-building comparison NOT RUN. No invented failing op. |
| `python3 scripts/mac-agent op rlo_option '{"action":"read","name":"living_population"}'` | `ops_rlo_option_unavailable` | Optional #239 checks 2 and 3 NOT RUN. No setter or option mutation was attempted. |
| `python3 scripts/mac-agent call drive '{"op":"status"}'`; `op ops '{}'` | running=false, phase=none; schemas expose no physical ordinary-player-car control or walker/resident identity binding | Availability recorded only; no unsupported car/binding check claimed. |
| `python3 scripts/mac-agent call new_game '{"name":"GR5-NonFT-13f"}'` | `The bridge starts only games whose name starts with FT, not 'GR5-NonFT-13f'.` | No non-FT fixture created; guard not bypassed. |
| Printed rollback shell below | Rejected before execution: `rm -f style commands are not permitted. Use a safer approach` | Non-destructive equivalent completed and verified below. |

NOT RUN grouped: both 60 fps arms; all of GR-5 4's three-building comparison; optional #239 2–3. GR-5 5's unreadable/under-one-storey classification remains partial. Missing per-spike rows in the GC-only sampler and a commit-stamped PC log build line are readback limitations. Discovery advertised: ops, panel, rival, weather, catalog, metadata, campaign, shop_fixture, shop_state, shop_log, shop_edit, forced_event, camera, screen_pos, capture, save, pcsave, perf, modwork, pcstate, ledger, access, rival_fingerprint, foreign_patch, probe_checkpoint and rlo_option.

### Findings, fixtures and restoration

At 1,000 residents, this city still spends hundreds of milliseconds in a manual save on the main thread. Its 30-cap street mod-work p95 passes, while the 40 ms save budget and the collection/heap comparison with 13d do not. This run does not verify the 1,000 cap against the save budget. No lower cap was measured or changed, and no code root cause is inferred.

Created: fresh city `FT-GR5-13f`; checkpoints `FT GR5 13f Timing v1`, `FT GR5 13f Save 01`–`21`, `FT GR5 13f Converted v1`, `FT GR5 13f Reloaded v1`. Attempt 01 is the initialization-time non-measurement. Reused, not overwritten: `FT GR5 13e Save 20`. All disposable saves stay in the isolated profile. `GR5-NonFT-13f` was not created.

Before staging, an independent manifest and originals covered the entire original PersistentCity folder, isolated root preference/config files, plist, install.json and request-file absence. RLO mod files were hashed read-only. The helper printed this first rollback verbatim:

```sh
rm -rf "/Users/peterhartwieg/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T015030Z/mod" "/Users/peterhartwieg/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T015030Z/install.json" "/Users/peterhartwieg/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

The execution layer rejected that shell before it ran. With the game stopped and lease held, the staged folder was retained intact as `<stage>/retained-staged-mod`; the helper's original `mod` folder was copied into the empty live path and its unchanged install.json copied back. Changed original preference/root configuration files were copied back from the independent backup. RLO mod files were never restored or rewritten. No receipt was hand-edited.

Verification: **35 original-file hashes and 4 absent paths matched, zero mismatches; complete PersistentCity file set matched; all 53 RLO hashes matched.** Original preferences, persistent-city.json and autosave bytes match. install.json SHA-256 `35e1dccafc8fed62ef9406ec7834c6526a290abb07de6a0f5f912d03cc8144dc` matches both independent and helper backups. AgentRigOps stayed unchanged. All stops were graceful. Final status: running=false, process=null, held=false, pending=0. All six owned game PIDs (12298, 12988, 18392, 19657, 20303, 20539) and their caffeinate assertions are gone. The rig checkout remains clean at 0b18d38.

Validation: candidate build passed; compact evidence JSON parses and is under 20 KB; report is appended to the existing checklist with no new markdown path. The diff contains only the checklist and the small evidence JSON. No full unit suite or regression suite was run for this docs-only result. The single build/evidence worktree is removed after commit/push and PR creation.

## Rig results — 14 November 2026 g, Mac (GR-5 GC at 37a150d3)

Role `rig_test`, where `mac`, isolated `agentmac`. **Counts: 0 passed / 0 partial / 3 failed / 6 NOT RUN.** The three failures are #250 checks 1–3; the six NOT RUN arms are enumerated below. Housing and optional capability readbacks are observations outside those verdict counts. **RESULT: DONE** means the run and restoration are complete; the performance targets did not pass. In-game proof gates no merge.

Timings are **provisional on the shared Mac**: other coding-agent and desktop processes were live. None was contacted, stopped or inspected beyond the process inventory. All commands and three 600-second waits ran in the foreground.

`<stage>` = the rig staging directory for this run. Raw command output, logs and backups remain there. Report copy: `<report-copy>/persistent-city-rig-2026-11-13g-mac.md`. [Compact evidence](perf/gr5-2026-11-13g-mac.json): **19,027 bytes**, SHA-256 `9a6b270848f81ee6f6e47cb47e2746643211a2c0a8461109756a0014b9be7c55`. It retains both complete `pc gc report` JSON/human lines, allocation sample counts, window/save guards, both street spikes, camera readbacks and a filename/hash manifest for larger raw records.

### Build and rig

| Item | Observation |
|---|---|
| Candidate | `37a150d330c294bb64943a39dc1eaa125618cc77` (#250). `origin/codex/persistent-city` was unchanged at initial and final fetch. |
| Checkout | New `codex/mac-rig-2026-11-13g`, based on that tip, in `rig-2026-11-13g-mac-evidence`. One new isolated worktree served build and evidence. Original checkout stayed clean. |
| Build | `python3 tools/build.py --game-managed "<managed-assemblies>"`, passed. PATH included `/opt/homebrew/bin`; DOTNET_ROOT was the Homebrew dotnet prefix plus `/libexec`. |
| PersistentCity DLL SHA-256 | `5557daaed9fe142c12978f66add0de8831d822244bc4c8e42273a3391d9b1bdc`; built, packaged and live staged files matched. |
| Agent-rig HEAD | `0b18d38ece6e8fe77c35ee5ea694849c7478d054`, clean before/after. `git fetch origin` and `git pull --ff-only` were already up to date. No checkout edits, commits or pushes. |
| AgentRigOps SHA-256 | `5578f9f4657fa2269ee50aa86adfc6d699881fac7e102084918da625045cd59e`, unchanged at cleanup; live ops ready. |
| RLO Player.log line | `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` No RLO file or option was changed. |
| PC Player.log identity | `[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places.` No commit-stamped PC build line is emitted; the matching DLL and validated receipt establish package identity. |
| Lease / resources | Initially stopped/free; acquired `2026-11-14T04:41:23.7511250Z` for this run. 54.5 GiB disk / 78% memory free. No takeover or resource override. |
| Start / stop | Two `python3 scripts/mac-agent start --mode headless --rig-test` launches, each successful on its first attempt. One process for day/night GC; one cold restart for street. Both stops graceful; no force. |
| Autosave original | Native 5 minutes / 3 saves, `prevent_autosave=false`. #70 `autosave` readback confirmed `suppressed=true` after both loads and before/after every window. Original preference/config bytes restored; process-only suppression ended at stop. |
| Config | SaveCostDiagnostics=false; native save profiler not installed. Physical caps 96/24, radius 120 m. FoodChoicePriceWeight/FoodChoiceHouseholdWeight stayed 1.2/1.0; writer schema 1. |

Staged once, stopped and leased, from the official agent-rig checkout. The package contained no RLO files.

| Exact staging command | Receipt backup |
|---|---|
| `python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package` | `<receipt-backups>/20261114T044216Z` |

### Per-check outcomes

| #250 check | Result | Observed versus expected |
|---|---|---|
| 1. Day GC, locked-PC | **FAIL** | 11 gen0, pause p95 56.671 ms versus <= about 5 / 36 ms. Reported Mono heap 1,366,654,976 bytes is below the about 1.47 GB target. Physical is the largest measured segment: 805,834,752 bytes. |
| 2. Night GC, locked-PC | **FAIL** | 8 gen0, pause p95 104.551 ms versus <= 5 / about 36 ms. Physical is the largest measured segment: 379,510,784 bytes. |
| 3. Street mod-work, input-only | **FAIL** | UpdateCity p95 5.1413 ms versus <5 ms. Broader mod-work p95 5.1897 ms. Two >=30 ms spikes, listed below. Nine camera pans succeeded. |

These are observed threshold failures, with no operation exception ending a measured window. Both GC windows used `pc gc start`, 600 s without game input, then `pc gc report` and `pc gc stop`. Mod-work sampling was unarmed throughout GC. Street used a separate cold load with the GC sampler off. All three used the same `FT GR5 13f Timing v1` checkpoint, player x=215, z=0, 1,000 residents / 2,000 visitors / 1,683 runtime places. The census reports 1,663 native places plus 20 discovered stations; it is a separate projection.

### GC and allocation results

The day clock began at day 2 15:30 and ended 18:24. An untimed advance then reached day 3 01:00; night ended 04:10. Speed was 1 during measurement. Day and night native union windows were 600.636 / 600.799 s, with 0 allowed saves, 0 native-saving frames and `clean=true`. Automatic attempts were refused (11,600 / 17,054). No save latency was measured.

| Window | Stored frames / dropped | gen0/1/2 | Pause p50 / p95 / max ms | Mono heap bytes | Sampled managed heap peak bytes | Whole-frame p95 / max ms |
|---|---|---|---|---|---|---|
| 14g day | 16868 / 0 | 11/0/0 | 25.904 / 56.671 / 56.671 | 1,366,654,976 | 1,433,260,032 | 43.2988 / 484.8029 |
| 14g night | 17048 / 0 | 8/0/0 | 29.953 / 104.551 / 104.551 | 1,465,794,560 | 1,689,186,304 | 37.8310 / 450.2231 |
| 13f day, d237f443 | 16768 / 0 | 12/0/0 | 25.328 / 65.028 / 65.028 | 1,493,630,976 | 1,483,522,048 | 44.4502 / 236.0063 |
| 13f night, d237f443 | 17076 / 0 | 6/0/0 | 17.381 / 31.977 / 31.977 | 1,491,255,296 | 1,414,967,296 | 37.7826 / 327.4926 |
| 13d night reference, code 83b6156e | 16960 / 0 | 5/0/0 | 33.526 / 35.941 / 35.941 | 1,471,660,032 | 1,471,025,152 | plain-frame p95 38.354 |
| #250 expectation | 600 s | day/night <= about 5 | p95 <= about 36 | day <= about 1.47 GB | — | separate from mod work |

Reported Mono heap is the report-time value; the larger night managed-heap peak is retained separately. The 13d reference had 3,222 persons; 13f and this run have 3,000. Day collections fell 12→11 and pause p95 65.028→56.671 ms versus 13f; night rose 6→8 and 31.977→104.551 ms. Neither reaches the #250 GC targets. Incremental GC=true, max_generation=0; pause describes cycle-ending frame excess, not all incremental collection work.

| Counter | Day bytes | Day samples / skipped | Night bytes | Night samples / skipped |
|---|---:|---|---:|---|
| frame_alloc_bytes | 1,787,936,768 | alloc_frames=16,857 | 1,241,608,192 | alloc_frames=17,040 |
| mod_alloc_bytes | 1,091,997,696 | 16,868 / 1 | 517,779,456 | 17,048 / 1 |
| advance_alloc_bytes | 219,332,608 | 16,775 / 0 | 91,938,816 | 16,955 / 0 |
| physical_alloc_bytes | 805,834,752 | 16,774 / 1 | 379,510,784 | 16,954 / 1 |
| mod minus advance minus physical | 66,830,336 | derived total | 46,329,856 | derived total |

These diagnostic counters sum observed heap growth and exclude segment calls that see a collection. Physical has the largest measured segment total in both windows; this identifies the segment, without a code root-cause claim.

GC-only UpdateCity maxima >=30 ms: **day plain 94.984 ms, day GC 57.374 ms; night plain 86.925 ms, night GC 91.871 ms**. Day plain exceeds 13f's 65.605 ms. The GC operation exports maxima rather than individual spike frames/counts, so those four maxima cannot be expanded into a per-frame list with the available operation.

### Street mod-work and whole-frame results

Cold-reloaded the same checkpoint and settled outside timing to day 2 15:30; street ended 17:36. The camera initially looked at (215,1,0) from (215,4,-9). `op camera look_at` and `release` changed its target between x=210 and 220 every roughly 60 s (nine successful pans), retaining y=4 m. The initial movement readback had 7 followers. This is the 13f street location; no visual crowd-density or building comparison is claimed from headless readbacks.

| Window | Native seconds / whole-frame samples / achieved fps | UpdateCity p95 / max ms | Broader mod-work p95 / max / >=30 ms | Whole-frame p95 / max / >=30 ms frames |
|---|---|---|---|---|
| 14g street | 600.184 / 3493 / 5.82 | 5.1413 / 66.8762 | 5.1897 / 66.9196 / 2 | 197.0065 / 541.8071 / 3493 |
| 13f street | 600.215 / 16771 / 27.94 | 4.2071 / 74.0022 | 4.2557 / 74.0481 / 2 | 44.2859 / 343.1558 / 16428 |

The mod-work recorder spans 601.020 s / 3,494 frames; the separate native union spans 600.184 s / 3,493 samples. Both bracket the full 600-second wait; their command boundaries differ. Coverage complete at 62 entry points, validity=true, no missing components, zero dropped frames, zero off-main-thread/cross-frame/unbalanced scopes and zero UpdateCity exceptions. Autosave suppression confirmed before/after, allowed=0, native_saving_frames=0, clean=true. Whole-frame counts include the rig's 30 FPS cap; this run achieved only 5.82 FPS. The shared-host and camera conditions limit causal comparisons with 13f.

Every exported mod-work spike >=30 ms (2 listed / 2 total):

| Unity frame | Mod-work ms | Dominant component / group | UpdateCity ms | Whole-frame ms in that row | Condition |
|---|---|---|---|---|---|
| 4282 | 66.9196 | updatecity / updatecity | 66.8762 | 224.4750 | hour=15 |
| 7308 | 39.5754 | updatecity / updatecity | 39.5327 | 190.1125 | hour=17 |

Full top-component maps are in the compact JSON. Spikes are reported without a follow-up code investigation.

### Save latency and housing readback

| Save-latency check | Samples | Result |
|---|---:|---|
| Native / Persistent City save latency | 0 | **NOT RUN**: explicitly deferred pending the owner's save-route decision. No save trigger, save profiling or measurement checkpoint in this run. |

Player.log on both loads: `[Persistent City] unrentable homes housed=0 beds=0 unreadable=0 empty=0`. Recorded as requested; this reused checkpoint does not demonstrate new housing allocation. Census: 1,000 residents, 471 households, 557 homes, 1,791 beds, residents_without_home=0; household/job/student capacity checks=ok.

### NOT RUN and tooling gaps

The six counted NOT RUN arms: **save latency (1); GR-5 check 4 building bounds/storeys (1); both 60 FPS arms (2); optional #239 checks 2–3 (2)**.

| Exact operation / discovery | Result | Coverage |
|---|---|---|
| `python3 scripts/mac-agent op ops '{}'`; `op pcstate '{"action":"list"}'` | No native Home bounds/storeys op; eight diagnostics projections, none for Home footprint/floors. | GR-5 4 rotated / ground-overlap / third-building comparison NOT RUN. No invented failing op. |
| `op ops '{}'` and health frame_cap readback | No FPS-changing operation advertised; target_frame_rate=30. Agent-rig HEAD unchanged from 13f; supported Mac launcher modes remain headless/background. | Both 60 FPS arms NOT RUN. No frame-cap request file or launcher override was created. |
| `python3 scripts/mac-agent op rlo_option '{"action":"read","name":"living_population"}'` | `ops_rlo_option_unavailable` | #239 2–3 NOT RUN. No option setter attempted; option left unchanged. |
| `python3 scripts/mac-agent call drive '{"op":"status"}'`; `op ops '{}'` | running=false, phase=none; no physical ordinary-player-car control or walker/resident identity binding advertised. | Availability readback only. |
| `python3 scripts/mac-agent call new_game '{"name":"GR5-NonFT-13g"}'` | `The bridge starts only games whose name starts with FT, not 'GR5-NonFT-13g'.` | Non-FT disposable city unavailable; guard not bypassed. |

Additional readback limitations: Player.log lacks a commit-stamped PC build line; GC-only sampling lacks individual mod-work spike rows. Neither has been invented.

Setup command errors, all outside measurement and corrected before the relevant window:

| Request | Exact error / correction |
|---|---|
| `call move {"x":215,"z":0}` | `Unknown command 'move'. Commands: state, console, debug, time, money, teleport, walk, drive, vehicle, commissary, crew, route, price, marketing, use, taxi, phone, call, eat, floorplan, place, drop, pickup, handtruck, fixture, logistics, parks, day_summary, places, map, ui, shoot, save, load, new_game, quit, op.` Used documented teleport. |
| `call teleport {"x":215,"z":0}` | `Missing "who": "player" or a food vehicle's id.` Corrected to `{"who":"player","x":215,"z":0}`. |
| `call console {"line":"pc inspect"}` | `Warning: ERROR: 'pc' doesn't take 1 parameter(s). Available command(s):` followed by the signatures retained in `<stage>/inspect.json`. Used supported census/movement/pcstate readbacks. |
| `call time {"run_until":{"time":"01:00"},"speed":20,"keep_needs":true}` | `day 2 01:00 has passed already: it is day 2 18:24.` Corrected to `day:"+1"`; paused exactly at day 3 01:00. |

Exact `pc inspect` response:

```text
Warning: ERROR: 'pc' doesn't take 1 parameter(s). Available command(s):
    - <b>pc [String area] [String action]</b>: pc population census | pc gc start|report|stop | pc vendor status | pc vendor sales | pc food decisions | pc food contract | pc probe status | pc probe businesses
    - <b>pc [String area] [String action] [String detail]</b>: pc population person <id> | pc gc population <persons|off> (FT cities only, measurement) | pc vendor withdraw <vehicle> | pc food contract <status|capabilities|hints|stamps|catalogue> (FT cities only)
    - <b>pc [String area] [String action] [String detail] [String value]</b>: pc movement corrections since <seq> | pc probe <resident> <place> <ceiling cents> (FT cities only)
```

The first early population read returned `[Persistent City] gc population refused: no active city.` after native load completion but before PC activation. A later read confirmed all 3,000 persons before sampling. No command was replayed after an ambiguous timeout.

### Findings, fixtures and restoration

At the 1,000-resident cap, these observed day/night GC windows exceed the requested collection/pause targets, and the panning street UpdateCity p95 exceeds 5 ms. The report-time Mono heaps are lower than 13f, while the night sampled managed peak is higher. This run does not establish the cap against those performance targets. Save latency is unmeasured here. No code cause or lower-cap result is inferred.

Reused fixture: `FT-GR5-13f`, loaded checkpoint `FT GR5 13f Timing v1`. No new city or checkpoint was created and no existing fixture save was overwritten. `GR5-NonFT-13g` was refused. Disposable saves remain in the isolated profile.

Before staging, an independent SHA-256 manifest and originals covered all original PersistentCity files, isolated root preferences/configuration, plist, install.json and four absent request paths. RLO files were hashed read-only. The helper printed this rollback, shown here with paths replaced by placeholders:

```sh
rm -rf "<isolated-profile>/ModsLocal/PersistentCity" && cp -Rp "<receipt-backups>/20261114T044216Z/mod" "<isolated-profile>/ModsLocal/PersistentCity" && cp "<receipt-backups>/20261114T044216Z/install.json" "<agent-tests-root>/install.json"
```

Restoration used the helper's original `mod/` and unchanged install.json with the game stopped and lease held. The staged folder was moved intact to `<stage>/retained-staged-mod` instead of deleted, then the helper's original mod folder copied into the empty live path and its receipt copied back. This is the non-destructive equivalent of the printed folder-replacement rollback; no receipt was hand-edited. Changed original agent-runtime.json, tracked_commands.json and isolated plist were restored from the independent backup. RLO files were never rewritten.

Verification: **35 original file hashes and 4 absent paths matched; complete PersistentCity file set matched; all 53 RLO hashes matched; zero mismatches**. Preferences, persistent-city.json and original autosave bytes match. install.json SHA-256 `35e1dccafc8fed62ef9406ec7834c6526a290abb07de6a0f5f912d03cc8144dc` matches both backups. AgentRigOps hash unchanged. Final status running=false, process=null, held=false, pending=0. Both owned game PIDs (39439, 45325) and their caffeinate assertions are gone. No Windows PC, Steam app, normal profile or other session's workspace was used.

Validation: candidate build passed; compact JSON parses and is under 20 KB; only this existing checklist and the small JSON are changed. No full unit or regression suite was run for docs-only evidence. The isolated build/evidence worktree is removed after commit, push and opening the one unmerged docs-only PR.

## Rig results — 14 November 2026 a, Mac (GR-5 GC, RLO, P-D 7 at b3d58a6a)

Role `rig_test`, where `mac`, isolated `agentmac`. **Counts: 1 passed / 0 partial / 4 failed / 1 NOT RUN**, counting #252 checks 1–3, #239 checks 2–3 and #244 check 7 once each. #239 check 3 includes a partially successful first attempt and a failed repeat; it is counted as failed. **RESULT: BLOCKED** by the non-FT console tooling gap. In-game proof gates no merge.

All performance numbers are **provisional on the shared Mac**. Commands and all three 600-second windows ran in the foreground. Other sessions were left alone. No Windows PC, Steam app, ordinary profile or ordinary save was used. Production code, schema 1, FoodChoicePriceWeight 1.2 and FoodChoiceHouseholdWeight 1.0 were unchanged.

Placeholders: `<stage>` is this run's local staging folder, `<agent-logs>` the local agent-log folder, `<managed>` the local game Managed assemblies, `<rig-home>` the isolated rig's tooling folder and `<rig-data>` the isolated game profile's data folder. Raw command replies, full logs, backups and extracted fixture data remain in `<stage>`, outside Git. Report copy: `<agent-logs>/persistent-city-rig-2026-11-14a-mac.md`. [Complete day/night GC JSON and compact street evidence](perf/gr5-2026-11-14a-mac.json): **18,423 bytes**, SHA-256 `6ebe2b30eba12e7410ac415f23b341735257fb08b69172e77fe9bb84fd251ccf`. It includes every allocation key and sample count, both human lines, all street spikes, guard evidence and a raw-record filename/hash-manifest pointer.

### Build and rig

| Item | Observation |
|---|---|
| Candidate | `b3d58a6a8c3f53eaa82652203b35e5ccf73b9241` (#252). `origin/codex/persistent-city` was unchanged at the initial and final fetch. |
| Build/evidence checkout | New `codex/mac-rig-2026-11-14a` from that tip, in `rig-2026-11-14a-mac-evidence`. One isolated worktree served build and evidence. Original checkout remained clean. |
| Build | `python3 tools/build.py --game-managed "<managed>"`, exit 0. PATH included `/opt/homebrew/bin`; DOTNET_ROOT was Homebrew dotnet prefix plus `/libexec`. |
| PersistentCity DLL SHA-256 | `c5fa3d98fed91d8a219eb8ba88ebcb2fe377817458def47c498873db622335c3`. Built, packaged and staged DLLs matched; every start validated install.json. |
| Agent-rig HEAD | `f6a2738eec9de76ff2b6e614263a0e528fbf7974`, clean before/after; fetch and ff-only pull were already up to date. No checkout edits, commits or pushes. |
| AgentRigOps SHA-256 | `51b387f84f41508e3a5bd6765dce582982c3ac40cc2810f4a4e69ec55e6e8142`, unchanged at cleanup; live ops ready. |
| RLO Player.log | `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` RLO files were never staged, updated or restored. |
| PC Player.log identity | `[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places.` No commit-stamped PC build line is emitted; package identity is established by the matching live DLL hash and receipt, rather than an invented log stamp. |
| Lease/resources | Initially stopped/free; acquired `2026-11-14T09:09:12.2716300Z` for this run. Startup checks reported 48.4 GiB disk / 61% memory free. No takeover, resource refusal or process cancellation. |
| Starts/stops | Six `python3 scripts/mac-agent start --mode headless --rig-test` launches, each successful on its first attempt. All six stops graceful; no force. |
| Autosave original | Native 5 minutes / 3 saves, prevent_autosave=false. #70 process-only suppression verified after each load and before/after every measured window. Preferences restored byte for byte; suppression ended at stop. |
| Config | SaveCostDiagnostics=false throughout; native save profiler not installed. Physical caps 96/24, radius 120 m. Saved schema remained 1. |

The package contained no RLO files. Staging ran stopped and leased from the official agent-rig checkout:

| Exact staging command | Receipt backup |
|---|---|
| `python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package` | `<rig-home>/receipt-backups/20261114T090950Z` |

### Per-check outcomes

| Check | Result | Observed against expected |
|---|---|---|
| #252 1, day GC | **FAIL** | 11 gen0 meets <=11 but does not move toward 5; pause p95 103.863 ms fails <56.7 ms; Mono heap 1,416,757,248 bytes passes <=1.47 GB. Advance 106,491,904 bytes is 51.4% below 13g's 219,332,608; physical 809,504,768 exceeds both <806 MB and 13g's 805,834,752. |
| #252 2, night GC | **FAIL** | 9 gen0 fails <=8; pause p95 70.122 ms passes <104.6 ms; Mono heap 1,536,327,680 fails <=1.47 GB. Advance 20,717,568 is 77.5% below 13g's 91,938,816; physical 360,747,008 passes <380 MB. |
| #252 3, street | **FAIL** | UpdateCity p95 5.2683 ms fails <5 ms; broader mod-work p95 5.3216 ms. Nine successful pans and three spikes >=30 ms, listed below. |
| #239 2, Living off before load | **PASS** | Set/read off before cold loading the owned food checkpoint. From 12:00–14:00, three new paid PC receipts ($15) match three native visits, three croissants and revenue $110→$125. Status off; checkout active throughout. |
| #239 3, mid-session toggle | **FAIL; first attempt partial** | First attempt detected both changes within 0.443/0.236 s and reported exactly 2 toggles, with checkout active and seven matched receipts ($35). Those sales were not proven to occur after the transitions; 14:00–17:30 added none. A fresh earlier-checkpoint repeat stopped the PC runtime with the exact route-retiming error below and lost active checkout. No full pass is claimed. |
| #244 P-D 7, ordinary save refused | **NOT RUN / tooling gap** | `new_game` accepted the throwaway RIGTMP city, but the bridge refused `console` before `pc gc population 3000` reached the mod. The mod's expected refusal and unchanged-person assertion were not exercised. |

### GC versus 13g

Both 600-second windows used `FT GR5 13f Timing v1`, 1,000 residents / 2,000 visitors / 1,683 runtime places, target off, 20 portals, zero fill refusals, fixed street player x=215,z=0 and speed 1. Day ran Tuesday 15:30–18:25; an untimed speed-20 advance positioned night at Wednesday 01:00–04:10. Mod-work sampling was unarmed for both GC windows. Autosave checks and native union guards show zero allowed saves, zero native-saving frames and clean=true in every measured window. No sample contains a save.

| Window | Stored frames / dropped | gen0/1/2 | Pause p50/p95/max ms | Mono heap bytes | Sampled managed heap peak bytes | Whole-frame p95/max ms |
|---|---|---|---|---|---|---|
| 14a day | 16,834 / 0 | 11/0/0 | 28.007/103.863/103.863 | 1,416,757,248 | 1,416,048,640 | 38.5703/235.8520 |
| 13g day | 16,868 / 0 | 11/0/0 | 25.904/56.671/56.671 | 1,366,654,976 | 1,433,260,032 | 43.2988/484.8029 |
| 14a night | 17,077 / 0 | 9/0/0 | 30.104/70.122/70.122 | 1,536,327,680 | 1,466,814,464 | 37.6928/194.3882 |
| 13g night | 17,048 / 0 | 8/0/0 | 29.953/104.551/104.551 | 1,465,794,560 | 1,689,186,304 | 37.8310/450.2231 |
| 13d night reference, code 83b6156e | 16,960 / 0 | 5/0/0 | 33.526/35.941/35.941 | 1,471,660,032 | 1,471,025,152 | plain-frame p95 38.354 |

13d had 3,222 persons; this run and 13g have 3,000. The previous run is called 13g by its stage/JSON/PR #251 and 14g by its dated checklist heading. Both GC windows report incremental=true, max_generation=0: pause is cycle-ending frame excess, not total incremental collector work. MB/GB comparisons here use decimal bytes.

| Allocation counter | Day bytes | Day samples / skipped | Night bytes | Night samples / skipped |
|---|---:|---|---:|---|
| frame_alloc_bytes | 1,789,841,408 | 16823 | 1,309,499,392 | 17068 |
| mod_alloc_bytes | 978,952,192 | 16832 / 3 | 424,472,576 | 17076 / 2 |
| advance_alloc_bytes | 106,491,904 | 16735 / 0 | 20,717,568 | 16975 / 0 |
| physical_alloc_bytes | 809,504,768 | 16733 / 2 | 360,747,008 | 16974 / 1 |
| physical_ambient_alloc_bytes | 1,134,592 | 16735 / 0 | 798,720 | 16975 / 0 |
| physical_vehicles_alloc_bytes | 12,288 | 16735 / 0 | 0 | 16975 / 0 |
| physical_bindings_alloc_bytes | 4,096 | 16735 / 0 | 8,192 | 16975 / 0 |

For frame allocation the sample column is alloc_frames; no frame-skipped key is exported. These diagnostics sum observed heap growth and exclude segment calls that see a collection.

13g frame/mod/advance/physical bytes: day **1,787,936,768 / 1,091,997,696 / 219,332,608 / 805,834,752**; night **1,241,608,192 / 517,779,456 / 91,938,816 / 379,510,784**. 13g did not export the three finer physical counters. Physical remains the largest measured mod segment in both current windows. Day physical+advance is **915,996,672 bytes**, above #252's about-100-MB task goal. The three finer physical counters total only 1,150,976 day / 806,912 night bytes; they do not account for most of the full physical total. This is segment evidence, not a code root cause.

GC-only UpdateCity maxima >=30 ms: day plain **66.442**, day GC **83.274**; night plain **86.786**, night GC **63.454**. The GC sampler exports maxima, not individual mod-work spike frames/counts; those rows cannot be reconstructed. All full JSON fields and counts are in the linked evidence.

### Street timing and every mod-work spike

Cold-loaded the same checkpoint and settled to Tuesday 15:30 outside timing; street ended 17:38, speed 1, GC sampler off. Initial camera target (215,1,0), camera (215,4,-9). Nine scheduled `camera release` / `look_at` pans between targets x=210 and 220 succeeded over the 600-second wait. These are headless control readbacks, not a visual crowd-density claim.

| Window | Native seconds / frames / achieved fps | UpdateCity p95/max ms | Broader mod-work p95/max ms / spikes >=30 | Whole-frame p95/max ms / spikes >=30 |
|---|---|---|---|---|
| 14a street | 600.246 / 3,493 / 5.82 | 5.2683/221.5981 | 5.3216/221.6495 / 3 | 196.3584/506.9880 / 3,493 |
| 13g street | 600.184 / 3,493 / 5.82 | 5.1413/66.8762 | 5.1897/66.9196 / 2 | 197.0065/541.8071 / 3,493 |

The mod-work recorder covers 601.278 seconds / 3,494 frames; native union covers 600.246 seconds / 3,493 samples. Both bracket the full 600-second wait. Coverage complete at 62 entry points, validity=true, no missing components, zero dropped frames and zero off-main-thread/cross-frame/unbalanced/depth-overflow faults. Whole-frame counts include the rig's 30 fps cap; achieved cadence was 5.82 fps.

| Unity frame | Mod-work ms | Dominant component/group | UpdateCity ms | Whole-frame ms in row | Condition |
|---|---|---|---|---|---|
| 4387 | 221.6495 | updatecity/updatecity | 221.5981 | 453.8571 | hour=15 |
| 4388 | 44.1359 | updatecity/updatecity | 44.0248 | 335.1515 | hour=15 |
| 5114 | 35.4132 | updatecity/updatecity | 35.3696 | 223.7165 | hour=16 |

All three exported spikes are listed; none was chased. The native whole-frame record is separate from mod-work scope time.

Player.log tally on the GR-5 loads: `[Persistent City] unrentable homes housed=0 beds=0 unreadable=0 empty=0`. This reused checkpoint is not a new housing-allocation test.

### RLO 1.0.6 checks 2 and 3

Original `rlo_option` read: stored=null, live=true, original_saved=false. Off-before-load used `op rlo_option {"action":"set","name":"living_population","value":false}`, verified stored=0/live=false, then a cold restart and load of `FT RLO 13d Owned 12`. The eligible owned shop is `FT RLO 13d Food v1`, `ba:street_fourthavenue#19`, with ready=true, staffed register, croissants $5 and normal free paper bags. The player remained outside. Native shop logging was installed only after all performance windows.

| Sales observation | PC new paid receipts / cents | Native visits / paid croissants / free bags | Revenue | Dropped / hook faults |
|---|---|---|---|---|
| Off, 12:00–14:00 | 3 / 1,500 | 3 / 3 / 3 | $110→$125 | 0 / 0 |
| First toggle attempt, 12:00–14:00 | 7 / 3,500 | 7 / 7 / 7 | $110→$145 | 0 / 0 |

Saved receipt keys were compared with pre-interval live keys; every new receipt is paid, 500 cents, croissant, with no extra paid line. The native log has no additional visits/products beyond the matched receipts and free bags. Both intervals suppressed two native draws and two hourly batches. Hooks list all five `CustomerEntriesCalculatorFactory.* [non-Harmony]` RLO replacements as overruled; checkout stayed active in the off interval and first toggle attempt. Extracted saves have schema 1.

Check 3 first cold-loaded the owned checkpoint with Living on. At speed 1 it set off, waited five real seconds, set on, waited five seconds, then read capability status. Exactly one Player.log transition line per change appeared within **0.443 / 0.236 seconds** from each setter call. Status was `Living population on, 2 toggle(s) this session`. The seven sales are positive matching evidence for the interval, but their timing does not establish selling after the transitions. A separate 14:00–17:30 observation added no receipts, revenue or PC purchases (7→7).

To obtain that missing observation, a fresh process loaded the earlier `FT RLO 13d Food v1` at 08:00, advanced at speed 20 to 10:30, confirmed an active coordinator with one committed image, then resumed at speed 1 for the same off/five-seconds/on/five-seconds sequence. The runtime stopped during this repeat, before it could report either transition; by 12:30 FoodDiagnostics.active=false, capability status `start refused (clock.city)`, checkout not checked, purchases=0. Exact error:

```text
System.ArgumentException: Cannot retime a maximum-size native car route without splitting its current edge.
Parameter name: state
```

The retained stack names `PersistentCity.Core.TravelTiming.Apply` then `PopulationRuntime.UpdateCity`; raw log `<stage>/rlo-toggle-early-Player.log`, first stop line 2722. The repeat's resume operation was `call time {"speed":1,"pause":false,"keep_needs":true}`, after the speed-20 preparation. No causal claim about the RLO setter is made. The failed fixture's later checkpoint retains the documented refusal marker and is not a recovery fixture. No production fix or further investigation was attempted.

Restore succeeded through `op rlo_option {"action":"restore","name":"living_population"}`; independent readback returned **stored=null, live=true, original_saved=false**, matching the original. No RLO file was edited.

### P-D check 7 and exact tooling gaps

`python3 scripts/mac-agent call new_game '{"name":"RIGTMP-PD-GC-Refusal-14a","throwaway":true}'` succeeded under a fresh `--rig-test` start; load.status reached done and population activation occurred. The required call was then attempted exactly:

```sh
python3 scripts/mac-agent call console '{"line":"pc gc population 3000"}'
```

Exact bridge refusal:

```text
console changes the save, and the loaded save 'RIGTMP-PD-GC-Refusal-14a' doesn't start with FT: load an FT save first.
```

The same guard first refused the optional `pc gc start` person-count setup read. The exact required population command was still attempted and recorded afterward. The mod never received it, so neither its `refused: measurement mode needs a disposable FT city` message nor before/after person equality is verified. No normal save or guard bypass was used. Raw replies: `pd-new-game.json`, `pd-population-command.json`, `pd-check.txt`, `pd-summary.json` in the stage.

Other readback/tooling limitations: no commit-stamped PC build line; no individual GC-only mod-work spike frames. The unused legacy `call console {"line":"pc inspect"}` in the reused preparation script returned `Warning: ERROR: 'pc' doesn't take 1 parameter(s). Available command(s):`; supported population, census, movement and pcstate reads supplied the required setup evidence. Two local result-inspection reads raised `KeyError: 'preferences'` and `FileNotFoundError` for the not-yet-written `gc-night-perf-end.json`; neither was a game operation failure, and both required records were retained before analysis.

### NOT RUN grouped

- **Counted: P-D #244 check 7**, because the bridge blocks console input in the successfully created throwaway non-FT city.
- **Outside the six-check count: save latency**, explicitly deferred pending the owner's route decision. No latency sample or save profiler was run; RLO receipt checkpoints were outside all performance windows.
- Remaining readback limits are listed above; unrelated 60 fps, building-footprint and GR-9 rows were not added to this run's count.

### Findings, fixtures and cleanup

At the 1,000-resident cap, advance allocation is lower in both observed GC windows, while physical remains the largest measured mod segment. The day pause/physical limits, night collection/heap limits and street p95 limit do not pass. These data do not establish the cap against #252's performance expectations. Save latency remains unmeasured. Living-off checkout is verified by rig; mid-session detection works in the first attempt but the full continuing-sales check remains failed after the repeat's runtime stop. No lower cap or code root cause is inferred.

Reused: `FT GR5 13f Timing v1`, `FT RLO 13d Owned 12`, `FT RLO 13d Food v1`. Created checkpoints: `FT RLO 14a Off 14`, `FT RLO 14a Toggle 14`, `FT RLO 14a Toggle 1730`, `FT RLO 14a Toggle Early 1230` (stopped-runtime refusal marker). Created throwaway city: `RIGTMP-PD-GC-Refusal-14a`; it was not explicitly saved, and no undocumented removal was attempted. Disposable fixture saves stay in the isolated profile; existing checkpoints were not overwritten.

An independent pre-stage manifest backed up the full PersistentCity folder, isolated root preferences/config files, plist, install.json and four absent request paths. The helper printed this rollback verbatim, retained in `<stage>/stage.txt`:

```sh
rm -rf "<rig-data>/ModsLocal/PersistentCity" && cp -Rp "<rig-home>/receipt-backups/20261114T090950Z/mod" "<rig-data>/ModsLocal/PersistentCity" && cp "<rig-home>/receipt-backups/20261114T090950Z/install.json" "<rig-home>/install.json"
```

The execution layer rejected it before execution: `rm -f style commands are not permitted. Use a safer approach`. With the game stopped and lease held, the non-destructive equivalent moved the staged folder intact to `<stage>/retained-staged-mod`, copied the helper's original `mod/` into the empty live path, then copied its unchanged receipt back. Changed original profile files and the plist were restored from the independent backup. RLO mod files were checked read-only, never restored.

Verification: **35 original file hashes and 4 absent paths matched; full PersistentCity file set matched; all 53 RLO hashes matched; zero mismatches**. Preferences, persistent-city.json and original autosave bytes match. install.json SHA-256 **`94b9b916dd66398dd1728ef3976256d1897751f0dffb0e9af6d4fe2a7bb1bb0d`** matches independent and helper backups; AgentRigOps hash is unchanged. Final status running=false, process=null, lease held=false, pending=0. All six owned game PIDs (91977, 97422, 98252, 98597, 99212, 99546) and their caffeinate assertions are gone.

Validation: candidate build passed; compact JSON parses and is under 20 KB; docs links, markdown ban and diff whitespace checks run for this docs-only result. No full unit or regression suite. Only the existing checklist and compact JSON are changed. The isolated build/evidence worktree is removed after commit, push and opening the one unmerged docs-only PR. No issue, release, Workshop upload, version/changelog change or merge.

## Rig results — 14 November 2026 b, Mac (GR-5 GC #254, footprint, 60 fps at eb38a061)

Role `rig_test`, where `mac`, isolated `agentmac`. **Counts: 0 passed / 0 partial / 3 failed / 7 NOT RUN. RESULT: BLOCKED.** Counts cover #254 checks 1–4, #246 footprint and the two requested 60 fps arms, plus the three owner-excluded checks. The two 600-second windowed diagnostics are recorded below; neither establishes the requested 60 fps arm. In-game proof gates no merge.

All performance numbers are **provisional on the shared Mac**. Every command and all five 600-second windows ran in the foreground. Other sessions were left alone. No Windows PC, Steam app, ordinary profile, normal bridge instance or ordinary save was used. No production code, schema, food weights, release, version/changelog or Workshop change was made.

`<stage>` is the rig stage folder `persistent-city-rig-2026-11-14b-mac`; `<app-support>` is the rig user's `Library/Application Support`; `<agent-logs>` is the agent report folder. Raw evidence: `<stage>`. [Complete GC reports and compact frame evidence](perf/gr5-2026-11-14b-mac.json): **16,572 bytes**, SHA-256 `6442f6a2b6c21fd5965fa3597cc1821cf6df37fadbca92742b5ec4c46bd4707f`. Both full GC JSON objects retain every allocation key and sample/skipped count. The raw filename/hash manifest is `raw-evidence-manifest.json` there (SHA-256 `44cddecdb4b5b4d8dc587d03e7a1388b36ee53214539620cd8096f0a1c21d60d`). Logs, requests, two body screenshots, obstructed captures, backups and scripts remain outside Git. Report copy: `<agent-logs>/persistent-city-rig-2026-11-14b-mac.md`.

### Build and rig

| Item | Observation |
|---|---|
| Tested candidate | `eb38a061200ff2469d4bc5a568c0046144494686` (#254), exactly the remote tip at initial fetch. Final fetch moved to `1d5d1b765218974d41e848926f17137609bf4a58` (#255); that later code was not built or tested here. |
| Branch/worktree | New `codex/mac-rig-2026-11-14b`, based on the tested tip, in `rig-2026-11-14b-mac-evidence`. One isolated worktree served build and evidence; original checkout stayed clean. |
| Build | `python3 tools/build.py --game-managed "<app-support>/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, passed; Homebrew PATH and DOTNET_ROOT used. |
| PersistentCity DLL SHA-256 | `296f4d44ed7f0ac61e79bd99e883b66485ffb011f1b61e65f3fc736640f83f6c`; built/package/staged hashes match, and each start validated the receipt. |
| Agent-rig HEAD | `2b8553606426f3e3e94f8a876dd58d935768732d`, clean before and after; fetch and ff-only pull were already current. No edits, commits or pushes to that checkout. |
| AgentRigOps SHA-256 | `dd11e3fdea78c1c0a25ce33e0168ffbce6e785950d9e10caed55015254d748f8`, unchanged at cleanup; ops ready in the loaded city. |
| RLO Player.log | `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` No RLO files were staged or written. |
| PC Player.log | `[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places.` No commit-stamped build line is emitted; binary identity is established by the matching DLL hashes and validated receipt. Shop fixture has 1,690 runtime places. |
| Lease/resources | Initially stopped/free; acquired `2026-11-14T11:56:58.6792540Z`. Initial resources: 51.5 GiB disk / 79% memory free by `mac-agent resources`; no resource override or refusal. |
| Starts/stops | Six starts, each successful on its first attempt: three headless, three windowed. All stops graceful; no force. Wrong-population and wrong-shop fixture setups were stopped before sampling. |
| Autosave original | 5 minutes / 3 saves, prevent_autosave=false. #70 suppression checked after every load and before/after every measured window. All five guards report clean=true, zero allowed saves and zero native-saving frames. |
| Config/options | SaveCostDiagnostics=false; native save profiler not installed. Schema 1, FoodChoicePriceWeight 1.2 and FoodChoiceHouseholdWeight 1.0. `living_population` unchanged: stored=null, live=true, original_saved=false before/after. |

Staged once, stopped and leased, from the official agent-rig checkout. Package contains no RLO files.

| Exact staging command | Receipt backup |
|---|---|
| `python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package` | `<app-support>/BigAmbitionsAgentTestsMac/receipt-backups/20261114T115729Z` |

### Per-check outcomes

| Check | Result | Observed against expected |
|---|---|---|
| #254 1, day GC | **FAIL** | boehm_total accepted; physical+advance **1,021.6 MB** fails well-under-about-100-MB target. 11 gen0 versus 14a 11; pause p95 **28.191 ms** versus 103.863 ms. Physical is largest; appearance is its largest child (745.4 MB). |
| #254 2, night GC | **FAIL** | boehm_total accepted; physical+advance **433.6 MB** fails the same target. 8 gen0 versus 14a 9; pause p95 **61.316 ms** versus 70.122 ms. Physical is largest; appearance is its largest child (327.8 MB). |
| #254 3, street | **FAIL** | UpdateCity p95 **5.5170 ms**, broader mod-work **5.5824 ms**, both above <5 ms. Four spikes >=30 ms, all listed below. |
| #254 4, appearance release/reacquire | **NOT RUN to required proof / tooling gap** | Windowed start and two independent body images captured. Original walker handle disappears; return walk ends short; no stable PC resident identity or successful reacquisition established. No matching appearance comparison or reuse-count rise is claimed. |
| #246 4, footprint | **NOT RUN / tooling gap** | `op building {"action":"list"}` returns `ops_game_api_unavailable`. No three targets, Home-area comparisons, rotated/yard coverage or visible storey comparison obtained. |
| #246 1, street 60 fps arm | **NOT RUN at required cadence** | Cap 60 applied, vSync=0; seven pre-window readings 4.96–5.71 fps fail 51–66. A separate full 600-second diagnostic ran at 4.74 fps. |
| #246 1, busy-shop 60 fps arm | **NOT RUN at required cadence/setup** | Cap 60 applied; readings 5.00–44.15 fps fail 51–66. Separate 600-second diagnostic at 4.98 fps used the known 13e food fixture; ready=true but no observed sales. It is not the same saved city as the street arm. |
| #239 3, toggle recheck | **NOT RUN / owner-excluded** | #255 was unmerged when this run was launched and is absent from the tested DLL. It merged during the run; a later run covers it. |
| #244 P-D 7 | **NOT RUN / owner-excluded** | Run request defers this until agent-rig #85 console-guard review is complete. No throwaway city or console refusal test attempted here. |
| GR-5 save latency | **NOT RUN / owner-excluded** | Owner decision pending. No latency sample, save profiler or manual save was run. |

### GC against 14a

Day: Tuesday 15:30; night: Wednesday 01:00. Both use `FT GR5 13f Timing v1`, 1,000 residents / 2,000 visitors, target off, street x=215,z=0, speed 1, mod-work sampler unarmed. Untimed speed-20 positioning was limited to required clock setup. No TravelTiming.Apply stop occurred. 14a measured a different allocation counter; raw byte totals below are context, not a like-for-like improvement claim. `boehm_total` is cumulative/process-wide; 14a `heap_used` measures used heap. Legacy frame allocation remains separately labelled heap_used.

| Window | Stored / dropped | gen0/1/2 | Pause p50 / p95 / max ms | Mono heap bytes | Sampled managed heap peak bytes | Native whole-frame p95 / max ms |
|---|---|---|---|---|---|---|
| 14b day | 16781 / 0 | 11/0/0 | 26.459/28.191/28.191 | 1535655936 | 1535467520 | 43.9235/494.3215 |
| 14b night | 16965 / 0 | 8/0/0 | 30.116/61.316/61.316 | 1578450944 | 1510080512 | 38.0592/897.9312 |
| 14a day | 16834 / 0 | 11/0/0 | 28.007/103.863/103.863 | 1416757248 | 1416048640 | 38.5703/235.8520 |
| 14a night | 17077 / 0 | 9/0/0 | 30.104/70.122/70.122 | 1536327680 | 1466814464 | 37.6928/194.3882 |

The original #246/13d reference is 5 gen0, pause p95 35.9 ms and about 1.47 GB Mono heap. Both current collection counts remain above that reference; both Mono heap readings exceed 1.47 GB. GC is incremental, max_generation=0; pause is cycle-ending frame excess, not all incremental collector work. MB/GB here are decimal bytes.

| Allocation bytes | 14b day | 14b night | 14a day / night (heap_used) |
|---|---|---|---|
| Probed whole-frame counter | 2462840480 | 1909833056 | unavailable |
| Legacy frame heap-used counter | 1830817792 | 1328979968 | 1789841408 / 1309499392 |
| Mod intervals | 1142457656 | 557799024 | 978952192 / 424472576 |
| Advance | 236447944 | 72745504 | 106491904 / 20717568 |
| Physical | 785139168 | 360896416 | 809504768 / 360747008 |

Every physical child below includes bytes / samples / skipped. Samples count charged spans, not frames. All 18 child rows sum exactly to physical in both windows; unattributed is zero.

| Physical child | Day bytes / samples / skipped | Night bytes / samples / skipped |
|---|---|---|
| ambient | 3207280 / 16689 / 0 | 3242800 / 16874 / 0 |
| vehicles | 1137328 / 16689 / 0 | 399552 / 16874 / 0 |
| bindings | 23808 / 605584 / 0 | 19376 / 255918 / 0 |
| vendor_meals | 0 / 16689 / 0 | 0 / 16874 / 0 |
| fade | 0 / 16689 / 0 | 0 / 16874 / 0 |
| focus | 0 / 16689 / 0 | 0 / 16874 / 0 |
| observe | 286560 / 588494 / 0 | 103968 / 238834 / 0 |
| release | 6720 / 401 / 0 | 14000 / 210 / 0 |
| parked | 1961776 / 583 / 0 | 1710688 / 586 / 0 |
| index | 80 / 2199 / 0 | 80 / 2191 / 0 |
| queues | 8960 / 2199 / 0 | 17200 / 2191 / 0 |
| roster | 292768 / 2199 / 0 | 194848 / 2191 / 0 |
| selection | 1261480 / 2702 / 0 | 977352 / 2428 / 0 |
| spawn_pedestrian | 29832816 / 858 / 0 | 25655416 / 377 / 0 |
| spawn_appearance | 745426168 / 1281 / 0 | 327776384 / 561 / 0 |
| spawn_check | 1518400 / 854 / 0 | 665088 / 374 / 0 |
| spawn_vehicle | 175024 / 72 / 0 | 119664 / 47 / 0 |
| unattributed | 0 / 33378 / 0 | 0 / 33748 / 0 |

Appearance contributes 94.94% day / 90.82% night of physical allocation. The appearance-check children are 1,518,400 / 665,088 bytes. counter_frames=16,781 / 16,965. Largest measured parent: physical; largest child: spawn_appearance. No code cause or lower-cap recommendation is inferred.

| Window | Last logged pedestrianLooks |
|---|---|
| gc-day | `pedestrianLooks=398 built/4 reused` |
| gc-night | `pedestrianLooks=188 built/1 reused` |
| street | `pedestrianLooks=155 built/2 reused` |
| street60 | `pedestrianLooks=142 built/2 reused` |
| shop60 | `pedestrianLooks=146 built/4 reused` |

### Frame windows and every mod-work spike >=30 ms

Main street: cold load of the GC fixture, Tuesday 15:30, speed 1, nine successful pans. Windowed street diagnostic began 15:33 after the appearance attempt, same fixture. Shop diagnostic cold-loaded `FT GR5 13e Timing v1`, 1,000 residents / 2,000 visitors, Tuesday 15:30, outside the shop entrance x=176.142853,z=-36.35211. All GC samplers were off for these windows.

| Window | Native seconds / frames / achieved fps | UpdateCity p95 / max ms | Broader mod-work p95 / max ms / spikes >=30 | Whole-frame p95 / max ms / spikes >=30 |
|---|---|---|---|---|
| street | 600.284 / 3370 / 5.61 | 5.517/78.1533 | 5.5824/78.2031/4 | 238.9091/1002.2687/3370 |
| street60 | 600.54 / 2848 / 4.74 | 11.3598/154.6731 | 11.6335/183.0037/52 | 312.994/1737.3838/2848 |
| shop60 | 600.365 / 2988 / 4.98 | 11.1759/191.5129 | 11.4017/191.6901/21 | 304.8277/850.0762/2988 |
| 14a street | 600.246 / 3493 / 5.82 | 5.2683/221.5981 | 5.3216/221.6495/3 | 196.3584/506.9880/3493 |

All three mod-work records have complete coverage, valid=true and zero dropped/off-main-thread/cross-frame/unbalanced/depth-overflow faults. Every exported spike is listed: 4 main street, 52 windowed street diagnostic, 21 shop diagnostic (77 total). None was chased. Native whole-frame timings stay separate from the mod-work union.

| Window / Unity frame | Mod-work ms | Dominant component/group | UpdateCity ms | Whole-frame ms in row | Condition |
|---|---|---|---|---|---|
| street / 3961 | 78.2031 | updatecity/updatecity | 78.1533 | 325.0361 | hour=15 |
| street / 4686 | 38.7129 | updatecity/updatecity | 38.667 | 191.5107 | hour=16 |
| street / 6278 | 40.6406 | updatecity/updatecity | 40.4722 | 316.4531 | hour=17 |
| street / 6730 | 38.2282 | updatecity/updatecity | 38.1749 | 189.0002 | hour=17 |
| street60 / 5274 | 36.9974 | updatecity/updatecity | 36.9423 | 192.1885 | hour=16 |
| street60 / 5521 | 39.3364 | updatecity/updatecity | 39.2721 | 205.0291 | hour=16 |
| street60 / 5541 | 60.0369 | updatecity/updatecity | 59.829 | 574.4246 | hour=16 |
| street60 / 5542 | 34.3638 | updatecity/updatecity | 33.6831 | 434.9791 | hour=16 |
| street60 / 5563 | 65.3679 | updatecity/updatecity | 64.5883 | 851.0843 | hour=16 |
| street60 / 5565 | 33.9637 | updatecity/updatecity | 33.8666 | 752.1063 | hour=16 |
| street60 / 5569 | 43.1151 | updatecity/updatecity | 42.6499 | 822.3757 | hour=16 |
| street60 / 5601 | 64.8532 | updatecity/updatecity | 63.3008 | 815.4749 | hour=16 |
| street60 / 5603 | 30.3043 | updatecity/updatecity | 29.077 | 1725.5583 | hour=16 |
| street60 / 5604 | 65.2218 | updatecity/updatecity | 63.9547 | 1385.5413 | hour=16 |
| street60 / 5605 | 100.7534 | updatecity/updatecity | 99.795 | 1539.5576 | hour=16 |
| street60 / 5606 | 100.0533 | updatecity/updatecity | 89.7965 | 1613.6252 | hour=16 |
| street60 / 5607 | 92.9644 | updatecity/updatecity | 89.068 | 1123.5389 | hour=16 |
| street60 / 5609 | 64.0901 | updatecity/updatecity | 63.2534 | 1683.8818 | hour=16 |
| street60 / 5610 | 116.6991 | updatecity/updatecity | 114.6702 | 1184.2536 | hour=16 |
| street60 / 5611 | 32.582 | updatecity/updatecity | 32.1146 | 375.535 | hour=16 |
| street60 / 5617 | 33.7542 | updatecity/updatecity | 32.2659 | 873.402 | hour=16 |
| street60 / 5618 | 50.568 | updatecity/updatecity | 49.3611 | 1384.8985 | hour=16 |
| street60 / 5619 | 40.2232 | updatecity/updatecity | 37.5156 | 1323.0545 | hour=16 |
| street60 / 5620 | 183.0037 | updatecity/updatecity | 154.6731 | 1364.8021 | hour=16 |
| street60 / 5625 | 63.7154 | updatecity/updatecity | 61.8779 | 1078.0434 | hour=16 |
| street60 / 5626 | 39.746 | updatecity/updatecity | 38.6603 | 1119.0615 | hour=16 |
| street60 / 5643 | 40.3283 | updatecity/updatecity | 39.6341 | 821.0729 | hour=16 |
| street60 / 5648 | 97.6177 | updatecity/updatecity | 97.513 | 316.1594 | hour=16 |
| street60 / 5701 | 56.3378 | updatecity/updatecity | 56.2591 | 255.4415 | hour=16 |
| street60 / 5705 | 48.8614 | updatecity/updatecity | 48.7502 | 265.6071 | hour=16 |
| street60 / 5717 | 62.8242 | updatecity/updatecity | 61.466 | 953.6815 | hour=16 |
| street60 / 5720 | 37.7738 | updatecity/updatecity | 36.4564 | 692.257 | hour=16 |
| street60 / 5725 | 42.9559 | updatecity/updatecity | 42.8722 | 370.1785 | hour=16 |
| street60 / 5753 | 37.0138 | updatecity/updatecity | 36.4056 | 949.689 | hour=16 |
| street60 / 5756 | 35.3189 | updatecity/updatecity | 34.2657 | 756.6054 | hour=16 |
| street60 / 5774 | 35.142 | updatecity/updatecity | 34.9927 | 295.7526 | hour=16 |
| street60 / 5780 | 31.0095 | updatecity/updatecity | 30.8932 | 208.3352 | hour=16 |
| street60 / 5782 | 111.1083 | updatecity/updatecity | 110.9008 | 348.6953 | hour=16 |
| street60 / 5783 | 99.7644 | updatecity/updatecity | 99.1704 | 890.4571 | hour=16 |
| street60 / 5787 | 69.0672 | updatecity/updatecity | 68.9753 | 239.5268 | hour=16 |
| street60 / 5790 | 61.4323 | updatecity/updatecity | 61.2462 | 237.5178 | hour=16 |
| street60 / 5793 | 82.4707 | updatecity/updatecity | 82.4019 | 264.0663 | hour=16 |
| street60 / 5801 | 89.9307 | updatecity/updatecity | 89.5512 | 378.6045 | hour=16 |
| street60 / 5807 | 31.9207 | updatecity/updatecity | 31.7754 | 346.0769 | hour=16 |
| street60 / 5817 | 45.0353 | updatecity/updatecity | 44.9328 | 235.4635 | hour=16 |
| street60 / 5820 | 36.4515 | updatecity/updatecity | 36.3678 | 231.797 | hour=16 |
| street60 / 6197 | 35.9882 | updatecity/updatecity | 35.9403 | 200.8827 | hour=16 |
| street60 / 6655 | 49.3145 | updatecity/updatecity | 49.2487 | 280.1985 | hour=16 |
| street60 / 6857 | 56.55 | updatecity/updatecity | 56.4291 | 385.8042 | hour=17 |
| street60 / 6988 | 50.7617 | updatecity/updatecity | 50.537 | 482.1713 | hour=17 |
| street60 / 7034 | 33.1742 | updatecity/updatecity | 33.0605 | 220.7119 | hour=17 |
| street60 / 7039 | 34.038 | updatecity/updatecity | 33.9771 | 232.95 | hour=17 |
| street60 / 7165 | 38.9581 | updatecity/updatecity | 38.7875 | 351.0833 | hour=17 |
| street60 / 7196 | 40.1609 | updatecity/updatecity | 40.1122 | 234.5326 | hour=17 |
| street60 / 7201 | 53.9309 | updatecity/updatecity | 53.8344 | 272.0931 | hour=17 |
| street60 / 7237 | 36.3964 | updatecity/updatecity | 36.2329 | 850.1287 | hour=17 |
| shop60 / 2665 | 191.6901 | updatecity/updatecity | 191.5129 | 562.5442 | hour=15 |
| shop60 / 2666 | 50.3743 | updatecity/updatecity | 50.2249 | 245.71 | hour=15 |
| shop60 / 2694 | 42.1562 | updatecity/updatecity | 42.0404 | 316.4843 | hour=15 |
| shop60 / 2699 | 48.7071 | updatecity/updatecity | 48.6165 | 207.8582 | hour=15 |
| shop60 / 3518 | 72.7335 | updatecity/updatecity | 72.6502 | 239.0418 | hour=16 |
| shop60 / 3642 | 32.2273 | updatecity/updatecity | 31.6131 | 840.353 | hour=16 |
| shop60 / 3646 | 33.5739 | updatecity/updatecity | 32.9509 | 664.2844 | hour=16 |
| shop60 / 3649 | 34.426 | updatecity/updatecity | 33.9819 | 400.6688 | hour=16 |
| shop60 / 3669 | 31.2136 | updatecity/updatecity | 23.8037 | 376.2538 | hour=16 |
| shop60 / 3854 | 36.3836 | updatecity/updatecity | 36.1673 | 224.0276 | hour=16 |
| shop60 / 3869 | 54.7504 | updatecity/updatecity | 54.6565 | 232.5811 | hour=16 |
| shop60 / 3897 | 31.2478 | updatecity/updatecity | 31.1166 | 219.9265 | hour=16 |
| shop60 / 4126 | 42.6629 | updatecity/updatecity | 42.5006 | 249.0814 | hour=16 |
| shop60 / 4143 | 63.7596 | updatecity/updatecity | 63.6726 | 252.4926 | hour=16 |
| shop60 / 4147 | 44.0425 | updatecity/updatecity | 43.9212 | 263.8383 | hour=16 |
| shop60 / 4178 | 31.8277 | updatecity/updatecity | 31.4836 | 551.9618 | hour=16 |
| shop60 / 4345 | 34.1677 | updatecity/updatecity | 33.8036 | 303.2314 | hour=16 |
| shop60 / 4377 | 44.6788 | updatecity/updatecity | 44.5777 | 275.8483 | hour=16 |
| shop60 / 4481 | 51.5786 | updatecity/updatecity | 51.4705 | 254.3372 | hour=16 |
| shop60 / 4531 | 30.4385 | updatecity/updatecity | 30.2818 | 352.38 | hour=16 |
| shop60 / 4734 | 46.5659 | updatecity/updatecity | 46.5059 | 209.6307 | hour=16 |

GC-only UpdateCity maxima (not individual spike exports): gc-day plain 72.146 ms / GC 33.253 ms; gc-night plain 81.497 ms / GC 59.904 ms; individual frame/count rows are not available from that sampler.

All five native windows are clean: no allowed saves and no native-saving frames. Autosave was checked before and after each. No save latency was measured.

### Appearance and exact tooling gaps

The windowed client started with `agent-frame-cap.json` containing `{"fps":60,"vsync":0}`. The first combined captures show a Camera Tools update overlay and native panels. Native panels were closed through supported UI button ops. Virtual input did not dismiss the update overlay. Desktop selection of the private app failed; no other app or desktop setting was changed.

| Operation | Exact result / consequence |
|---|---|
| `op building {"action":"list"}` | `ops_game_api_unavailable`; retained full failed reply in `footprint-error.json`. A read-only repeat returned the same error. No building data was guessed. |
| `cua.getApp("<app-support>/BigAmbitionsAgentTestsMac/Big Ambitions Agent Tests Mac.app")` | `Computer Use server error -10005: cgWindowNotFound` |
| `call ui {"op":"escape"}` | `Nothing is open to close (Esc would open the pause menu).` Native panels were subsequently closed through their listed buttons. |
| `call walk {"x":215,"z":0}` (return, op 29) | `Operation 29 is still pending: use tools/ft op 29; do not repeat the command`. Polled the existing operation, without replay. Final status done, arrived=false, x=230.57,z=-10.04, from_goal=18.53 m, waited_for_traffic_s=30. |
| Street cap verification | applied=true, target_frame_rate=60, vsync_count=0, error=null; measured 5.58, 5.71, 5.32, 5.71, 4.96, 5.58, 5.54 fps. Required 51–66 not reached. |
| Shop cap verification | applied=true, target_frame_rate=60, vsync_count=0, error=null; measured 44.15, 5.00, 5.45, 5.95, 5.32, 5.82, 5.37 fps. Required 51–66 not reached. |

Building-op documentation says storeys=null; the requested substitution was bounds.height_m, compared with PC’s 3.5 m policy. The failed list prevented that comparison, including rotated and yard/sidewalk targets. No footprint/storey conclusion is claimed.

Appearance outward `call walk {"x":305,"z":0}` completed a 100 m pavement route in about 30.3 seconds, ending x=304.09,z=-0.30. The original opaque walker handle was absent after the trip. Rig walker handles do not guarantee a PC resident identity across pool reuse; the required resident identity/reacquisition was not established. Last appearance-attempt counters include 12 built/0 reused before, 4 built/0 reused after; no reuse increase is proven. The attempt log has zero matches for `Appearance meshes unavailable` and `Empty appearance`, and no TravelTiming.Apply stop.

Two independent, visually inspected 1600x900 images are retained outside Git:

| Image | Observation | SHA-256 |
|---|---|---|
| `appearance-body-before.png` | Candidate body in purple shirt/burgundy shorts, short grey hair and red/white shoes. | `95fe425be4db400add52a6284b8b1cfdb9146d2632c2f37f349b3cb627bc4721` |
| `appearance-body-after.png` | Same camera position; empty sidewalk. No matching resident available for comparison. | `c2cebcaf6d58e5869f9f59aee606e9528ac650cbf23e3e0eeeb481c0a9d06659` |

The shop diagnostic used ready=true `FT GR5 13e Food`, `ba:street_fourthavenue#21`, one staffed register, croissants $5. Revenue remained $180 and stock remained 64 croissants / 265 bags, so no busy-sales proof is claimed. The same address in the 14a fixture is native gym `Pump Up`, rented=false; that setup was discarded before measurement. No fixture was created or saved by this run.

### Findings, fixtures and NOT RUN groups

At 1,000 residents, physical+advance remains above #254’s about-100-MB target in both windows, with appearance the largest measured child. Main street UpdateCity and broader mod-work p95 exceed 5 ms. Day pause p95 is lower than 14a, night has one fewer collection, but these observations do not establish the cap against the requested GC/allocation and street gates. Save latency remains unmeasured. No code root cause, alternative cap or release decision is inferred.

**Required proof blocked (four NOT RUN arms):** #254 appearance identity/reacquire comparison; #246 three-building footprint; actual 60 fps street; actual 60 fps busy shop. Captures and low-cadence diagnostics above are retained as observations, not passes.

**Owner-excluded (three NOT RUN checks):** #239 check 3 (later run with #255); #244 P-D check 7 (agent-rig #85 console guard); GR-5 save latency (owner decision pending).

Fixtures: requested `FT RLO 13d Food v1` existed but loaded 1,611 residents / 1,611 visitors, so it could not supply the required cap. GC/main street/appearance/windowed street used the 14a fallback `FT GR5 13f Timing v1` (1,000/2,000; 1,683 runtime places). Shop diagnostic used `FT GR5 13e Timing v1` (1,000/2,000; 1,690 places). Existing saves were never overwritten; no new checkpoint or RIGTMP city was made. The load tally is `[Persistent City] unrentable homes housed=0 beds=0 unreadable=0 empty=0`; reused saves are not a fresh housing-allocation proof.

### Restoration and validation

An independent pre-stage manifest backed up the original PersistentCity folder, isolated root preference/config files, plist and install receipt, and recorded four absent request/config paths. RLO hashes and its original living_population values were recorded without changing them. The helper printed this rollback verbatim:

```sh
rm -rf "<app-support>/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "<app-support>/BigAmbitionsAgentTestsMac/receipt-backups/20261114T115729Z/mod" "<app-support>/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "<app-support>/BigAmbitionsAgentTestsMac/receipt-backups/20261114T115729Z/install.json" "<app-support>/BigAmbitionsAgentTestsMac/install.json"
```

With the game stopped and lease held, the non-destructive equivalent moved the staged mod intact to the raw evidence folder, copied the helper’s original mod folder into the empty path, restored the unchanged receipt, then restored independent preference/config bytes and original frame-cap absence. RLO files were checked read-only.

Verification: **35 original file hashes, 4 absent paths, full PersistentCity file set and all 53 RLO hashes match; zero mismatches**. install.json SHA-256 `2381eebf577366ad720a6a18fcf82a2e8c9b72a1854605ebefdb67f37a413ace`; AgentRigOps SHA-256 unchanged. Original preferences, persistent-city.json, autosave bytes and frame-cap absence match. Process-only autosave suppression ended at stop.

Final status: running=false, process=null, lease held=false, pending=0. All six owned PIDs (28591, 29551, 38926, 40985, 48859, 49413) and their caffeinate assertions are gone. All stops were graceful. No process belonging to another session was stopped.

Validation for this docs-only result: candidate build passed; compact JSON parses and is under 20 KB; docs-links, markdown ban and diff whitespace checks are run before commit. No full unit or regression suite. Only this existing checklist and the compact JSON are changed. The build/evidence worktree is removed after commit, push and opening the one unmerged docs-only PR. No issue or merge is created.

## Rig results — 14 November 2026 d, PC (GR-5 60 fps arms at 913abd7d)

Role `rig_test`, where `pc`, isolated `agent1`. **Counts: 0 passed / 0 partial / 0 failed / 9 NOT RUN. RESULT: BLOCKED.** Counts cover the two requested 60 fps arms and seven excluded rows below; the launch tooling failure is not a measured performance failure. The pre-fix baseline was built and staged, but the required start command was refused before a game process launched. No 600-second window ran. In-game proof gates no merge.

### Build and rig

| Item | Evidence |
|---|---|
| Candidate | `913abd7dbfdcd7f7651baad768169f66bf698dfb`, initial and final `origin/codex/persistent-city`; unchanged. The unmerged spawn-appearance implementation was not used. |
| Worktrees | Results branch `codex/pc-rig-2026-11-14d`, evidence worktree `rig-2026-11-14d-pc-evidence`; detached build worktree `rig-2026-11-14d-pc`. Original checkout remained clean. |
| Official agent-rig | `4bcf80aa04431f3b54cac19335c9c4cbb57a4b48`; clean, fetched, `git pull --ff-only` reported already up to date. No checkout edits, build or overlay installation by this run. |
| Live AgentRigOps | `C:\BigAmbitions-AgentTests\Big Ambitions Agent Tests_Data\Managed\AgentRigOps.dll`, SHA-256 `fe4c40c1ba70512eb0b14704e275c1fa4842e98100ba563cc2d559a13be6bf6a` before acquisition and after cleanup. Matches the existing local overlay DLL/manifest and installed receipt. No binary difference observed; a source-commit stamp is not in that manifest. |
| Lease | Owner `pc-rig-2026-11-14d`. Initially held by an overlay-update role; no takeover. Acquired after status became free at 14:02:53 UTC; released after verified restoration. |
| Build | `python tools/build.py --game-managed "C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"`, exit 0. PATH prefixed with `%LOCALAPPDATA%\Microsoft\dotnet`; requested wait helper ran first. No offline tests. |
| PersistentCity DLL | Built, packaged and staged SHA-256 `087fe9aa8c506c3d2fa43df62fec0f08450095ca415f7c3ffebf64bfd28bde29`. Package contains no RLO or game DLLs. No startup build line was available because launch was refused. |
| Package / raw evidence | `<stage>` = `%USERPROFILE%\HART-Builds\persistent-city-rig-2026-11-14d-pc`; package at `<stage>\package\PersistentCity`. Full backups, build/start output, machine inventory, restore checks and SHA-256 manifest remain outside Git. |
| Staging command | Python `shutil.copytree(package, profile / 'ModsLocal/PersistentCity', dirs_exist_ok=True)`, then compare SHA-256 for every package file. This is the authorized copy fallback; no receipt hand-edit. |
| Backup / rollback source | `<stage>\pre-stage-backup`. Entire original mod folder, receipt/run files, profile configuration and complete isolated registry export, with original bytes or absence in `manifest.json`. Restoration used this run's backup only. |
| Frame-cap file | `%USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\agent-frame-cap.json`; staged exactly `{"fps":60,"vsync":0}` (20 bytes, no newline). Original absence restored. |
| Autosave / configuration | Original runtime autosave setting unavailable without a running player; complete preferences/configuration preserved and hash-verified. No setting changed or measured window started. Candidate package has SaveCostDiagnostics=false. No claim of live suppression. |
| RLO / Food Trucks | No current-run version log: no player launched. Owner-provided Food Trucks 0.10.2 was accepted as a run input, not independently verified. Neither mod was installed, updated or edited. |
| Machine load | Post-cleanup preparation inventory at 14:04:53 UTC: CPU 51%; used RAM 38.55 / 63.84 GiB, free 25.29 GiB. Notable working sets: other game 7.00 GiB, memory compression 5.94 GiB, WSL 4.53 GiB, browser processes, compiler server 0.40 GiB and other agent sessions. This is not a measurement-start sample. No timing numbers exist to label; any later timings on this host need **provisional (PC shared)**. |

### Required arms and exact failure

One required start attempt, in the foreground from the official checkout with the lease held:

```text
python scripts/agent start --mode windowed --rig-test
agent: error: unrecognized arguments: --rig-test
```

The official Windows CLI parser does not accept `--rig-test`. Its ordinary `status` command also uses the SSH path on this PC; the prep attempt returned exactly `ssh: Could not resolve hostname homepc: No such host is known.` Local lock commands worked. No alternate launch, second start attempt, launcher edit, overlay update or receipt bypass was attempted, following the requested one-attempt BLOCKED path.

| Arm | Outcome | Measured fps | Whole-frame p95 / p99 ms | Gen0 / pause p95 / heap | UpdateCity / street mod-work p95 | Every >=30 ms spike |
|---|---|---|---|---|---|---|
| Street, cap 60, 600 s | **NOT RUN / tooling gap**; launch refused | unavailable | unavailable | unavailable | unavailable | unavailable; no window |
| Busy shop, cap 60, 600 s | **NOT RUN / tooling gap**; launch refused before city/fixture | unavailable | unavailable | unavailable | unavailable | unavailable; no window |

Cap health readback: `requested`, `applied`, `vsync_count`, `target_frame_rate`, `measured_fps`, `window_s` are all **unavailable**. The staged request is not evidence that a running player applied it. The Rig bot's earlier health is not reused as this run's proof.

No city was created. Planned fixture `RIGTMP-GR5-14d` remains uncreated; there was no throwaway refusal and no FT fallback. Residents, visitors and `unrentable homes housed=... beds=...` are unavailable. No busy-shop operation was attempted, no save/load or save profiling occurred, and no current-run Player-agent.log is claimed. No <5 ms mod-work or 60 fps pass can be inferred.

### Findings, tooling gaps and NOT RUN

The Windows launch CLI needs a supported PC-local route carrying `RigTest` before these arms can run through the prescribed command. A second staging limitation was found: the only scoped package refresh discovered is `install-mac.py --stage-mod`, which targets the Mac. The Windows `rig_modset.ModSet` transaction replaces the complete DLL set, including RLO/Food Trucks; this run did not use it because those files were outside scope. The authorized copy fallback verified the package but deliberately left `agent-install.json` byte-identical. Its existing PersistentCity entries therefore still described the old package while staged. Launch receipt validation was not reached, so a receipt refusal is a potential next blocker, not an observed error. Both gaps need a reviewed tooling path; no production or rig tooling change was made here.

**Launch-blocked (2 NOT RUN):** street-60 and busy-shop-60, exact parser error above. The missing timing/GC values are nulls, not zeros, in [compact evidence](perf/gr5-2026-11-14d-pc.json) (3382 bytes; SHA-256 `5c3fb29d4fed6179d9f7d147684e214e7595340d6901393a0cc76008ef9a8635`).

**Excluded by this run request (7 NOT RUN):**

1. GR-5 #246 check 4 footprint: known `op building` response `ops_game_api_unavailable` on both rigs; not attempted here.
2. #254 check 4 appearance: stable resident identity across release/reacquire is unavailable; not attempted.
3. GR-5 check 2 save latency: owner save-route decision pending; no saves attempted.
4. Street 30 fps: already attempted on Mac 14b; this run is the 60 fps host only.
5. Busy-shop 30 fps: same reason.
6. #255 / #239 check 3 retime group: Mac 14c already covers it; Mac was not contacted or used.
7. P-D check 7: separate console-guard work; not attempted.

No GitHub issue, release, Workshop upload, merge, version/changelog change or production change was made. Schema stays 1; FoodChoicePriceWeight 1.2 and FoodChoiceHouseholdWeight are unchanged. Other sessions, their files and branches were left alone. No mouse/keyboard input or Windows setting change occurred.

### Restoration and validation

Before copying, SHA-256 backup covered the full isolated PersistentCity tree, `agent-install.json`, `run.json`, root/profile configuration, both optional request files, persistent-city.json, cap-file absence and complete isolated registry preferences. Cleanup removed this run's staged package files, restored the complete original mod folder and exact file bytes/absences, and compared all hashes with `pre-stage-backup\manifest.json`. `restore.json` verifies every recorded file, the full mod inventory and registry export. No changed registry required import. Autosave preferences are byte-identical; runtime state was never started.

No process was launched by this run, so no game stop or force-stop was needed. Process inventory confirmed the isolated player closed before staging and after cleanup. Agent1 lease status was free with pending=0 after release. Original cap absence restored; overlay DLL hash unchanged. No new fixture save exists. No process started by this run remains.

Validation: build passed; compact JSON parsed; documentation diff checked; no offline test suite. Build worktree is removed after evidence commit, and evidence worktree after PR creation. Report copy: `%USERPROFILE%\Coding_Projects\.agent-logs\persistent-city-rig-2026-11-14d-pc.md`.

**RESULT: BLOCKED.**

## Rig results — 14 November 2026 c, Mac (#255 retime checks 1-3 at 1d5d1b76)

Role `rig_test`, where `mac`, isolated `agentmac`. **Counts: 1 passed / 1 partial / 1 failed / 6 NOT RUN groups.** The verdicts count #255 checks 1–3; the six excluded groups are listed below. Check 2 is partial because still screenshots cannot establish motion continuity at every change. No runtime stop occurred. In-game proof gates no merge. **RESULT: DONE** means execution, evidence delivery and restoration completed.

Raw evidence: `<stage>`; report copy: `<agent-logs>/persistent-city-rig-2026-11-14c-mac.md`. Local roots in this section are placeholders: `<stage>` is the run's staging and evidence folder, `<agent-logs>` the local agent-log folder, `<managed>` the local game-managed reference folder, `<rig-tools>` the isolated rig helper's Application Support folder and `<rig-profile>` the isolated game profile's Application Support folder. Raw logs, screenshots, extracted saves, backup manifests and operation replies stay outside Git. All commands ran in the foreground and were awaited. Other sessions were left alone. No Windows PC, ordinary profile, Steam app or ordinary save was used. No production code, weights, schema, version or changelog changed; schema remains 1, FoodChoicePriceWeight 1.2 and FoodChoiceHouseholdWeight 1.0.

### Build and rig

| Item | Observation |
|---|---|
| Candidate | `1d5d1b765218974d41e848926f17137609bf4a58`, origin/codex/persistent-city at preparation. Final fetch: `913abd7dbfdcd7f7651baad768169f66bf698dfb`. |
| Evidence/build branch | `codex/mac-rig-2026-11-14c`, new isolated worktree `rig-2026-11-14c-mac-evidence`; original checkout untouched. One worktree served build and evidence; the unpushed results branch was advanced to the docs-only 913abd7d tip before the report commit. |
| Build | `python3 tools/build.py --game-managed "<managed>"`, exit 0; Homebrew PATH and DOTNET_ROOT as requested. |
| PersistentCity DLL SHA-256 | `087fe9aa8c506c3d2fa43df62fec0f08450095ca415f7c3ffebf64bfd28bde29`; build, package and live staged DLL matched. |
| Agent-rig HEAD | `2b8553606426f3e3e94f8a876dd58d935768732d`, clean; fetch and ff-only pull already up to date. No checkout edits, commits or pushes. |
| AgentRigOps SHA-256 | `dd11e3fdea78c1c0a25ce33e0168ffbce6e785950d9e10caed55015254d748f8`; live op status ready; unchanged at cleanup. |
| RLO version, exact Player.log | `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` RLO mod files unchanged. |
| PC activation, exact Player.log | `[Mod:PersistentCity] Persistent population active: 1611 residents, 1611 visitors, 1685 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.` No commit-stamped PC build line is emitted; matching DLL hashes and validated install receipt establish build identity. |
| Lease/resources | Free/stopped before acquisition; acquired 2026-11-14T13:42:00.5324480Z. Initial resources 69.2 GiB free disk / 57% reported free memory. No refusal, takeover or resource override. |
| Starts/stops | Four cold processes: headless check 1, windowed first check-2 attempt, windowed check-2 repeat, headless check 3; `--rig-test` throughout. Each start succeeded on its first attempt; all stops graceful, no force. |
| Autosave original | Native 5 minutes / 3 saves, prevent_autosave=false. #70 `autosave` confirmed suppressed=true after every load. Process-only suppression ended at stop; original preference/config bytes restored. |
| Frame cap | Temporary `agent-frame-cap.json` with fps=30, vsync=0 for windowed screenshots; removed before headless check 3 and restored to its original absence. No cadence/performance gate. |
| Independent backup | 39 file/absence manifest entries, including full PersistentCity, install.json, isolated root preferences/config and plist; 53 RLO files hashed read-only. |

Exact staging command, run stopped and leased from the official agent-rig checkout:

```sh
python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package
```

Receipt backup: `<rig-tools>/receipt-backups/20261114T134245Z`. Package contains no RLO files; only PersistentCity was staged.

### Per-check outcomes

| #255 check | Result | Evidence |
|---|---|---|
| 1. Maximum native route, speed 20 → 1 | **PASS** | Fresh 08:00 fixture, speed 20 to 10:30, exact `call time {"speed":1,"pause":false,"keep_needs":true}`. FoodDiagnostics.active=true, capability `city running`, no stop line. Next manual save `FT RLO 14c Retime 1030` at 10:31 has schema=1, published epoch sequence=2 and accounting_stop_present=false. |
| 2. Rush-hour changes and visible continuity | **PARTIAL** | Cold repeat ran 1 → 3 → 20 → 1 → 20 → 3 → 1 during rush hour, plus 3 → 1 → 3 within one game minute. Active food diagnostics and no stop; one degraded-retime line at the repeat’s 08:50 speed 20 → 3 change; zero at every other requested change. Two usable screenshot pairs show no obvious jump. Continuous visual motion at every change is unverified. |
| 3. #239 mid-session Living off/on | **FAIL: paid receipts absent** | Fresh 08:00 fixture and speed-20 10:30 setup, then speed 1 and off/on through rlo_option. Both transitions logged; runtime stayed active. Off interval has 0 new paid receipts; on interval adds 0 paid receipts. The continuing-sales expectation was not met; no runtime exception or op error caused the verdict. Original option restored. |

No Persistent City runtime-stop exception or stack was emitted in any of the four processes. Check 1 before/after FoodDiagnostics travelClockRate changed from the speed-20 rate to 0.3333333333333333 at speed 1; capability stayed city running. Save epoch text:

```text
v1
sequence=2
schema=1
minute=2071.2668972015381
population=rlo:0.40.4:724c64860fe2a780eb29eccd135780ac1938ad6a49c20490683a6df7fe7377b0
payload=25429111 persistentcity:state_v1
```

Exact fixture tally (all four loads):

```text
[Persistent City] unrentable homes housed=0 beds=0 unreadable=0 empty=0
```

### Rush-hour sequence and screenshots

The final fetch moved origin from 1d5d1b76 to 913abd7d (#256), changing only docs/rig-checklist.md and docs/perf/gr5-2026-11-14b-mac.json. Production code was identical; the tested DLL remains the build from 1d5d1b76.

First attempt speed changes returned at 08:10, 08:20, 08:30, 08:40, 09:05 and 09:06. Camera correction delayed the last two beyond the requested window, so that attempt supplies runtime observations only. Its process was stopped and a cold repeat ran the requested sequence. No game restart occurred within either attempt.

| Repeat event | Clock returned | Speed |
|---|---|---|
| `check2b-change1-speed3` | 08:10 | 3 |
| `check2b-change2-speed20` | 08:20 | 20 |
| `check2b-change3-speed1` | 08:30 | 1 |
| `check2b-change4-speed20` | 08:40 | 20 |
| `check2b-change5-speed3` | 08:50 | 3 |
| `check2b-change6-speed1` | 08:59 | 1 |
| `check2b-rapid-speed1` | 08:10 | 1 |
| `check2b-rapid-speed3` | 08:10 | 3 |

Cars were present: Player.log reports 65 persistent cars and 63 scheduled road events before the first repeated speed change. Usable raw pairs: `check2b-change1-before.png` / `check2b-change1-after.png` (1 → 3), and `check2b-change3-before.png` / `check2b-change3-after.png` (20 → 1), 960×540. The fixed camera looks along the street from x=215,y=18,z=-18 toward x=215,y=1,z=3. Both show street traffic, kerbside cars and pedestrians; the first has a yellow car beside dark vehicles, the second a yellow taxi and white delivery vehicle. Vehicle and pedestrian positions show no obvious displacement in the sampled pairs. Still frames do not establish continuous trajectories or stable resident identity, so the full visual expectation remains partial.

### Living transitions and paid receipts

Original op read: stored=null, live=true, original_saved=false. For each transition the run resumed at speed 1, called `rlo_option set` with name=living_population and the required boolean, waited five real seconds, and read capability/FoodDiagnostics. Off sales interval was 10:30–12:30; on interval was 12:30–14:30. No sale probe fabricated a receipt. Native shop_log was enabled only for this check, after all rush-hour observations, and stopped afterward. The owned fixture shop is `ba:street_fourthavenue#19`.

| Interval | Paid receipts newly arriving in interval | Paid cents |
|---|---|---|
| Living off | 0 | 0 |
| Living on after second transition | 0 | 0 |

The on count compares saved keys after the on interval against all off-interval keys. Before the toggle, FoodDiagnostics already showed zero purchases and the live key set contained no paid food receipts. The ready shop held 100 croissants at $5, a staffed register and free paper bags, with revenue $0. No causal claim about the option toggle is made: the required continuing-sale evidence did not occur in the two observed intervals. Both native shop_log snapshots report purchases=0, dropped=0 and hook_faults=0. Revenue stayed $0 and croissant stock stayed 100. Final food-contract readback reports capabilitiesPassed=true, firstFailure=null, coordinator mode=Active, images=1, Commits=1 and Refusals=0. These reads establish an active contract, without explaining why no sales arrived. Raw extracted PC keys and native shop visit replies are retained separately. Exact transition lines:

```text
[Mod:PersistentCity] RLO Living population on -> Living population off during play; Persistent City keeps owning its shops, crowds and traffic.
[Mod:PersistentCity] RLO Living population off -> Living population on during play; Persistent City keeps owning its shops, crowds and traffic.
```

Restore op and independent read both returned stored=null, live=true, original_saved=false. No RLO file was edited.

### Player.log around every speed change and toggle

Each block quotes exact numbered lines captured around the operation, including up to three preceding context lines. Long unrelated periodic Population and RLO whole-frame Session diagnostics are omitted here; numbered gaps identify those omissions, and the complete excerpts stay in the raw evidence. The range JSON alongside each raw excerpt records line boundaries and operation arguments; the full process logs remain in the stage. Some successful `time` calls emit no new line, so their preceding context is retained. No missing retime message is invented. The only edit to quoted lines replaces local path roots with the placeholders defined above. The repeat contains one `Clock retime degraded 1 journeys` line (line 2066), inside the 08:50 speed 20 → 3 operation range 2065–2071. Every other rush-hour change has zero, meeting the at-most-one-per-change bound. Other process totals are recorded below. The exact line is in that event’s block below.

`check1-speed1-Player-excerpt.txt`:

```text
2699: crowds: ordinary standers off in every native zone, 115 of them RLO's; RLO's own scene people kept
2700: traffic: ordinary native groups held for Persistent City cars; RLO staged accidents and reckless drivers stood down; cars loaded into held groups past the shares retired off screen; cars RLO holds are never retired
2701: [Mod:FoodTrucks] Test bridge: console 'pc capability status' (1 lines).
2702: [Mod:RealLifeOverhaul] City life check (ground): chat at (252, 0, -13) would stand on parking or a driveway, the place refused: PASS
2703: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2704: [Mod:PersistentCity] Road snapshot started: frame=5645, reason=clock-rate index=-1, scene searches in this planner=0.
2705: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'DeliveryTruck' at 23 km/h began to slow for a sharp bend 2 m ahead, needing 7.4 m/s2 to reach it at 21 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): a firm stop: it was 2 m off when it came into view
2706: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 40 walker(s), 0 standing or sitting; the nearest: nobody within 12 m: PASS.
```

`check1-speed20-to1030-Player-excerpt.txt`:

```text
2432: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: the game is busy (a pause, the time machine, a question, the activity panel, the hospital, the subway or the casino boat); out: 0 walker(s), 0 standing or sitting; the nearest: not looked for: PASS.
2433: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: the game is busy (a pause, the time machine, a question, the activity panel, the hospital, the subway or the casino boat); out: 0 walker(s), 0 standing or sitting; the nearest: not looked for: PASS.
2434: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: the game is busy (a pause, the time machine, a question, the activity panel, the hospital, the subway or the casino boat); out: 0 walker(s), 0 standing or sitting; the nearest: not looked for: PASS.
2435: [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 1 10:30.
2436: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2437: [Mod:PersistentCity] Road snapshot started: frame=3509, reason=clock-rate index=-1, scene searches in this planner=0.
2438: [Mod:RealLifeOverhaul] Street people: besides the walking pedestrians, the standing and sitting people of 7 of the game's pools count as people on the street (BenchPedestrian, CasualStationaryAi, SportPedestrian, SwimmingPoolBaseHuman, StreetPerformer, Homeless, Golfer).
2440: [Mod:RealLifeOverhaul] Session check (work named for a long frame): the line of the frame of 400 ms leaves out 8 piece(s) of work begun after it, before the line was written; what the mod named up to its end is still named (a ring of 16): PASS.
2441: [Mod:RealLifeOverhaul] Meta: welcome shown (after 0.45.0).
2442: [Mod:RealLifeOverhaul] Meta: props notice shown: the props of reallife_props cannot be drawn: the game's lit shader is not supported on this graphics setting.
2443: [Mod:RealLifeOverhaul] Meta: Meta check (welcome notice): shown as the game's own notification with a click that opens the Guide on 'What changed': PASS.
2444: [Mod:RealLifeOverhaul] Car ownership: records read.
2445: [Mod:RealLifeOverhaul] City events: state loaded; day 1 of a 60-day year (Winter), today: nothing on.
2446: [Mod:RealLifeOverhaul] Traffic check (senses): cars sense layers [Player, Human, Vehicles]; pedestrians are on 'Human' with their capsule on; sensor box 4.5 m long on 'AiVehicles', which does not meet the pedestrians' layer in the physics settings; mode: stand-ins on 'Player' for everybody in a car's way.
2447: [Mod:RealLifeOverhaul] Traffic check (proxy): 0 of 16 in use: PASS
2448: [Mod:RealLifeOverhaul] Traffic check (cost): 1 looks in the last minute, 990 microseconds on average and 990 at worst (wanted: under 100 and 300); at most 4 cars looked at, 39 people in the grid, 0 of 16 stand-ins in use, 0 cars braked, 0 watched for contact now; mode StandIns: over budget; where it went, on average and in the worst look (microseconds): the cars gathered 30 and 30, the people 918 and 918, the strips and the decisions 32 and 32, the stand-ins and the fades 10 and 10; the diagnostics' own lines are not counted; people looked over in a look, at most: 40 walking and 0 standing or sitting, 0 of those near enough when last listed to be read; outside the looks, 11 car(s) measured in frames of their own (139 microseconds at most) and 0 that took the figures of a car of their kind.
2449: [Mod:RealLifeOverhaul] Traffic check (city drivers' cost): 1 looks in the last minute, 165 microseconds a look on average and 165 at worst, over 12 cars a frame (wanted: under 150 and 600); at most 3 cars read, 4 things on the roads, 0 cars slowed at once, 0 hold line(s): FAIL, over budget; of it the street read and its cars gathered 77 on average and 77 in the worst look; outside the looks, 2 car(s) measured in frames of their own (148 microseconds at most) and 4 that took the figures of a car of their kind.
2451: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'Taxi' at 27 km/h began to slow for a sharp bend 11 m ahead, needing 1.5 m/s2 to reach it at 19 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
2452: [Mod:RealLifeOverhaul] Civic: ready. Election 0 None, election day 15, approval 58, 6 site(s) in progress.
2453: [Mod:RealLifeOverhaul] Contacts check (picture): 'reallife-cityhall' is a logo, from the game's logo shape 'headquarter3', kept (its file read on a worker, 0.03 ms of the main thread, wanted under 3 ms); 27 contact picture(s): 9 face(s), 18 logo(s), 27 drawn: PASS
```

`check2-change1-speed3-Player-excerpt.txt`:

```text
1824: [Mod:RealLifeOverhaul] Traffic check (proxy): 0 of 16 in use: PASS
1825: [Mod:RealLifeOverhaul] Clips: the 'smoke' clip is played by a street character (said the first time only).
1826: [Mod:RealLifeOverhaul] City life: smoker started 62 m away; 1 of 3 sites out, 1 of 8 people.
1827: [Persistent City] RLO traffic scene switches given back (the player's stored options).
1828: [Mod:PersistentCity] Road snapshot started: frame=2734, reason=clock-rate index=-1, scene searches in this planner=0.
1829: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 40 walker(s), 1 standing or sitting; the nearest: nobody within 12 m: PASS.
1830: [Mod:PersistentCity] RLO single-pass read context=periodic status=Ready identities=400 elapsedMs=3.068 reads=9 totalMs=21.348 maxMs=5.964
```

`check2-change2-speed20-Player-excerpt.txt`:

```text
1934: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'FreightTruckT1' at 40 km/h began to slow for a red light 29 m ahead, needing 2.2 m/s2 to reach it at 0 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
1935: Couldn't find AI business logo: "BusinessLogos/Summit Care Clinic/Billboard" with extension .jpg or .png
1936: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 0 walker(s), 1 standing or sitting; the nearest: nobody within 12 m: PASS.
1937: Couldn't find AI business logo: "BusinessLogos/The Cigar Box/Billboard" with extension .jpg or .png
1938: [Persistent City] RLO traffic scene switches given back (the player's stored options).
1939: [Mod:PersistentCity] Road snapshot started: frame=4010, reason=clock-rate index=-1, scene searches in this planner=0.
```

`check2-change3-speed1-Player-excerpt.txt`:

```text
2005: Couldn't find AI business logo: "BusinessLogos/Maple Leaf Tea/Billboard" with extension .jpg or .png
2006: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 3 walker(s), 1 standing or sitting; the nearest: nobody within 12 m: PASS.
2007: [Mod:FoodTrucks] Test bridge: shot check2-change3-before (960x540) from x 176.0, y 18.0, z -56.0 (the camera stands inside something solid (a building, a prop or a vehicle); the picture may be dark); uncovered SM_FlatironAreaBuilding_03_Variation 02 (CityBuildingController, reveal 0.00, blocking the game's camera); outline lifted from SM_1StStreet_CheapRezidential_1; hover lifted from SM_FlatironAreaBuilding_06_Variation_Corner (under the mouse, its building card open, 6 objects outlined): <rig-profile>/FoodTrucksTest/shots/check2-change3-before.png.
2008: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2009: [Mod:PersistentCity] Road snapshot started: frame=4631, reason=clock-rate index=-1, scene searches in this planner=0.
2010: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'Taxi' at 35 km/h began to slow for a car 35 m ahead, needing 1.2 m/s2 to reach it at 7 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
```

`check2-change4-speed20-Player-excerpt.txt`:

```text
2108: Couldn't find AI business logo: "BusinessLogos/Summit Care Clinic/Billboard" with extension .jpg or .png
2109: [Mod:PersistentCity] RLO single-pass read context=periodic status=Ready identities=400 elapsedMs=3.267 reads=13 totalMs=34.063 maxMs=5.964
2110: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 2 walker(s), 1 standing or sitting; the nearest: nobody within 12 m: PASS.
2111: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2112: [Mod:PersistentCity] Road snapshot started: frame=5779, reason=clock-rate index=-1, scene searches in this planner=0.
2113: [Mod:RealLifeOverhaul] Session: the managed heap grew by 116 MB in one frame of 34 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "city life: a site being set up" 98.3 s before, "city life: a character's look being built (a mesh merge)" 104.9 s before, "city life: a site being set up" 104.9 s before; on foot in the street, game hour 8.
2114: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'Taxi' at 28 km/h began to slow for a red light 26 m ahead, needing 1.2 m/s2 to reach it at 0 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
```

`check2-change5-speed3-Player-excerpt.txt`:

```text
2178: Couldn't find AI business logo: "BusinessLogos/Summit Care Clinic/Billboard" with extension .jpg or .png
2179: [Mod:FoodTrucks] Test bridge: shot check2-change5-before (960x540) from x 215.0, y 18.0, z -18.0; uncovered SM_FlatironAreaBuilding_03_Variation 02 (CityBuildingController, reveal 0.00, blocking the game's camera); outline lifted from SM_1StStreet_CheapRezidential_1; hover lifted from SM_FlatironAreaBuilding_06_Variation_Corner (under the mouse, its building card open, 6 objects outlined): <rig-profile>/FoodTrucksTest/shots/check2-change5-before.png.
2180: Couldn't find AI business logo: "BusinessLogos/Maple Leaf Tea/Billboard" with extension .jpg or .png
2181: [Mod:CityPerformance] PERF frames n=725 mean_ms=41.38 p50_le_ms=35 p95_le_ms=61 p99_le_ms=192 max_ms=652.79 over50=85 bin250plus=7 refill=False
2182: [Mod:CityPerformance] PERF scope=CullingGroupController.Update calls=1450 total_ms=1.622 max_ms=0.017 exceptions=0
2183: [Mod:CityPerformance] PERF scope=TrafficManager.FixedUpdate calls=1469 total_ms=430.697 max_ms=2.165 exceptions=0
2184: [Mod:CityPerformance] PERF scope=PedestrianSpawner.SetActivePedestrians calls=2 total_ms=58.800 max_ms=58.519 exceptions=0
2185: [Mod:CityPerformance] PERF scope=SaveGameManager.SerializeSaveGame calls=0 total_ms=0.000 max_ms=0.000 exceptions=0
2186: [Mod:CityPerformance] PERF scope=GameManager.RunMainGameTick calls=725 total_ms=137.141 max_ms=88.989 exceptions=0
2187: [Mod:CityPerformance] DETAIL hooks=0; retained=0; overwritten=0; daily_retained=0; daily_overwritten=0; session_worst_method=none; max_ms=0; full breakdown in exported methods CSV and capabilities.
2188: [Mod:CityPerformance] TREE requested=True; installed=False; faulted=False; registered_seen=0; configured_records=0; skipped=0; changed_elsewhere=0; map_hold=False; lod_bias=1; maximum_lod=0; configured records are not visible-tree or selected-LOD counts.
2189: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2190: [Mod:PersistentCity] Road snapshot started: frame=6250, reason=clock-rate index=-1, scene searches in this planner=0.
```

`check2-change6-speed1-Player-excerpt.txt`:

```text
2227: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'Bima320' at 35 km/h began to slow for a car 6 m ahead, needing 1.4 m/s2 to reach it at 26 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
2229: [Mod:FoodTrucks] Test bridge: shot check2-change6-before (960x540) from x 215.0, y 18.0, z -18.0; uncovered SM_FlatironAreaBuilding_03_Variation 02 (CityBuildingController, reveal 0.00, blocking the game's camera); outline lifted from SM_1StStreet_CheapRezidential_1; hover lifted from SM_FlatironAreaBuilding_06_Variation_Corner (under the mouse, its building card open, 6 objects outlined): <rig-profile>/FoodTrucksTest/shots/check2-change6-before.png.
2231: [Mod:RealLifeOverhaul] Session: the managed heap grew by 12 MB in one frame of 33 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "flare: the hour's milestones" 8.1 s before, "flare: the hour's words asked ahead" 8.1 s before, "flare: the hour's unread count" 8.1 s before; on foot in the street, game hour 9.
2232: [Mod:RealLifeOverhaul] Traffic check (cost): 413 looks in the last minute, 67 microseconds on average and 606 at worst (wanted: under 100 and 300); at most 16 cars looked at, 43 people in the grid, 2 of 16 stand-ins in use, 1 cars braked, 0 watched for contact now; mode StandIns: over budget; where it went, on average and in the worst look (microseconds): the cars gathered 18 and 20, the people 39 and 14, the strips and the decisions 6 and 45, the stand-ins and the fades 3 and 528; the diagnostics' own lines are not counted; people looked over in a look, at most: 40 walking and 1 standing or sitting, 1 of those near enough when last listed to be read; outside the looks, 0 car(s) measured in frames of their own (0 microseconds at most) and 0 that took the figures of a car of their kind.
2234: [Mod:RealLifeOverhaul] Session: the managed heap grew by 12 MB in one frame of 201 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "flare: the hour's milestones" 8.3 s before, "flare: the hour's words asked ahead" 8.4 s before, "flare: the hour's unread count" 8.4 s before; on foot in the street, game hour 9.
2236: [Mod:RealLifeOverhaul] Traffic check (city drivers' cost): 412 looks in the last minute, 86 microseconds a look on average and 512 at worst, over 12 cars a frame (wanted: under 150 and 600); at most 16 cars read, 17 things on the roads, 9 cars slowed at once, 0 hold line(s): PASS; of it the street read and its cars gathered 57 on average and 94 in the worst look; outside the looks, 0 car(s) measured in frames of their own (0 microseconds at most) and 0 that took the figures of a car of their kind.
```

`check2-initial-speed1-Player-excerpt.txt`:

```text
1746: [Mod:CityPerformance] PERF scope=GameManager.RunMainGameTick calls=664 total_ms=86.297 max_ms=32.667 exceptions=0
1747: [Mod:CityPerformance] DETAIL hooks=0; retained=0; overwritten=0; daily_retained=0; daily_overwritten=0; session_worst_method=none; max_ms=0; full breakdown in exported methods CSV and capabilities.
1748: [Mod:CityPerformance] TREE requested=True; installed=False; faulted=False; registered_seen=0; configured_records=0; skipped=0; changed_elsewhere=0; map_hold=False; lod_bias=1; maximum_lod=0; configured records are not visible-tree or selected-LOD counts.
1749: [Mod:RealLifeOverhaul] Street people: besides the walking pedestrians, the standing and sitting people of 7 of the game's pools count as people on the street (BenchPedestrian, CasualStationaryAi, SportPedestrian, SwimmingPoolBaseHuman, StreetPerformer, Homeless, Golfer).
1751: [Mod:RealLifeOverhaul] Session check (work named for a long frame): the line of the frame of 133 ms leaves out 2 piece(s) of work begun after it, before the line was written; what the mod named up to its end is still named (a ring of 16): PASS.
1752: [Mod:RealLifeOverhaul] Traffic check (senses): cars sense layers [Player, Human, Vehicles]; pedestrians are on 'Human' with their capsule on; sensor box 4.5 m long on 'AiVehicles', which does not meet the pedestrians' layer in the physics settings; mode: stand-ins on 'Player' for everybody in a car's way.
1753: [Mod:RealLifeOverhaul] Traffic check (proxy): 0 of 16 in use: PASS
1754: [Mod:RealLifeOverhaul] Traffic check (cost): 1 looks in the last minute, 965 microseconds on average and 965 at worst (wanted: under 100 and 300); at most 2 cars looked at, 10 people in the grid, 0 of 16 stand-ins in use, 0 cars braked, 0 watched for contact now; mode StandIns: over budget; where it went, on average and in the worst look (microseconds): the cars gathered 28 and 28, the people 892 and 892, the strips and the decisions 34 and 34, the stand-ins and the fades 12 and 12; the diagnostics' own lines are not counted; people looked over in a look, at most: 10 walking and 1 standing or sitting, 1 of those near enough when last listed to be read; outside the looks, 9 car(s) measured in frames of their own (208 microseconds at most) and 0 that took the figures of a car of their kind.
1755: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'DeliveryTruck' at 33 km/h began to slow for a red light 21 m ahead, needing 2.1 m/s2 to reach it at 0 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
1756: [Mod:RealLifeOverhaul] Traffic check (city drivers' cost): 1 looks in the last minute, 229 microseconds a look on average and 229 at worst, over 12 cars a frame (wanted: under 150 and 600); at most 4 cars read, 7 things on the roads, 1 cars slowed at once, 0 hold line(s): FAIL, over budget; of it the street read and its cars gathered 127 on average and 127 in the worst look; outside the looks, 8 car(s) measured in frames of their own (130 microseconds at most) and 1 that took the figures of a car of their kind.
1757: [Mod:RealLifeOverhaul] Civic: ready. Election 0 None, election day 15, approval 58, 6 site(s) in progress.
1759: [Mod:RealLifeOverhaul] Contacts check (picture): 'reallife-cityhall' is a logo, from the game's logo shape 'headquarter3', kept (its file read on a worker, 0.04 ms of the main thread, wanted under 3 ms); 27 contact picture(s): 9 face(s), 18 logo(s), 27 drawn: PASS
```

`check2-rapid-Player-excerpt.txt`:

```text
1858: traffic: own cars inactive; native traffic as the game and RLO run it
1859: [Mod:FoodTrucks] Test bridge: console 'pc capability status' (1 lines).
1860: [Mod:RealLifeOverhaul] Session: the managed heap grew by 15 MB in one frame of 68 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "city life: a site being set up" 3.0 s before, "transit: a parking lane beside a bus zone laid out again" 27.8 s before, "transit: a parking lane beside a bus zone laid out again" 28.0 s before; on foot in the street, game hour 8.
1861: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'Taxi' at 21 km/h began to slow for a car 9 m ahead, needing 2.0 m/s2 to reach it at 7 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
1862: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 40 walker(s), 1 standing or sitting; the nearest: nobody within 12 m: PASS.
```

`check2b-change1-speed3-Player-excerpt.txt`:

```text
1796: [Mod:RealLifeOverhaul] Patrol: two officers on the beat, 54 m away, 4 stops.
1797: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: a job board or an employer's door has the interact key; out: 14 walker(s), 2 standing or sitting; the nearest: not looked for: PASS.
1798: [Mod:FoodTrucks] Test bridge: shot check2b-change1-before (960x540) from x 215.0, y 18.0, z -18.0; uncovered SM_FancyBuilding_V03 Variant 3 (CityBuildingController, reveal 0.00, blocking the game's camera); outline lifted from SM_1StStreet_CheapRezidential_1; hover lifted from Prefab_5thAV_BuildingV1_2 (under the mouse, its building card open, 6 objects outlined): <rig-profile>/FoodTrucksTest/shots/check2b-change1-before.png.
1799: [Persistent City] RLO traffic scene switches given back (the player's stored options).
1800: [Mod:PersistentCity] Road snapshot started: frame=2098, reason=clock-rate index=-1, scene searches in this planner=0.
1801: [Mod:RealLifeOverhaul] Session: the managed heap grew by 95 MB in one frame of 33 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "transit: a parking lane beside a bus zone laid out again" 26.2 s before, "transit: a parking lane beside a bus zone laid out again" 26.6 s before, "transit: a parking lane beside a bus zone laid out again" 26.8 s before; on foot in the street, game hour 8.
1802: [Mod:CityPerformance] PERF frames n=744 mean_ms=40.32 p50_le_ms=35 p95_le_ms=68 p99_le_ms=163 max_ms=393.21 over50=68 bin250plus=1 refill=False
1803: [Mod:CityPerformance] PERF scope=CullingGroupController.Update calls=1488 total_ms=1.682 max_ms=0.011 exceptions=0
1804: [Mod:CityPerformance] PERF scope=SaveGameManager.SerializeSaveGame calls=0 total_ms=0.000 max_ms=0.000 exceptions=0
1805: [Mod:CityPerformance] PERF scope=TrafficManager.FixedUpdate calls=1489 total_ms=430.482 max_ms=0.693 exceptions=0
1806: [Mod:CityPerformance] PERF scope=PedestrianSpawner.SetActivePedestrians calls=1 total_ms=39.676 max_ms=39.676 exceptions=0
1807: [Mod:CityPerformance] PERF scope=GameManager.RunMainGameTick calls=744 total_ms=44.723 max_ms=0.181 exceptions=0
1808: [Mod:CityPerformance] DETAIL hooks=0; retained=0; overwritten=0; daily_retained=0; daily_overwritten=0; session_worst_method=none; max_ms=0; full breakdown in exported methods CSV and capabilities.
1809: [Mod:CityPerformance] TREE requested=True; installed=False; faulted=False; registered_seen=0; configured_records=0; skipped=0; changed_elsewhere=0; map_hold=False; lod_bias=1; maximum_lod=0; configured records are not visible-tree or selected-LOD counts.
```

`check2b-change2-speed20-Player-excerpt.txt`:

```text
1869: [Mod:RealLifeOverhaul] Clips: the 'sip' clip is played by a street character (said the first time only).
1870: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: a job board or an employer's door has the interact key; out: 14 walker(s), 2 standing or sitting; the nearest: not looked for: PASS.
1871: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'Taxi' at 16 km/h began to slow for a red light 8 m ahead, needing 1.5 m/s2 to reach it at 0 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
1872: [Persistent City] RLO traffic scene switches given back (the player's stored options).
1873: [Mod:PersistentCity] Road snapshot started: frame=3396, reason=clock-rate index=-1, scene searches in this planner=0.
1874: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: a job board or an employer's door has the interact key; out: 14 walker(s), 2 standing or sitting; the nearest: not looked for: PASS.
```

`check2b-change3-speed1-Player-excerpt.txt`:

```text
1927: Couldn't find AI business logo: "BusinessLogos/Fenwick and Daughters/Billboard" with extension .jpg or .png
1928: Couldn't find AI business logo: "BusinessLogos/Fenwick and Daughters/Billboard" with extension .jpg or .png
1929: [Mod:FoodTrucks] Test bridge: shot check2b-change3-before (960x540) from x 215.0, y 18.0, z -18.0; uncovered SM_FancyBuilding_V03 Variant 3 (CityBuildingController, reveal 0.00, blocking the game's camera); outline lifted from SM_1StStreet_CheapRezidential_1; hover lifted from Prefab_5thAV_BuildingV1_2 (under the mouse, its building card open, 6 objects outlined): <rig-profile>/FoodTrucksTest/shots/check2b-change3-before.png.
1930: [Persistent City] RLO traffic scene switches given back (the player's stored options).
1931: [Mod:PersistentCity] Road snapshot started: frame=4086, reason=clock-rate index=-1, scene searches in this planner=0.
1932: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: a job board or an employer's door has the interact key; out: 14 walker(s), 2 standing or sitting; the nearest: not looked for: PASS.
1933: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'UMCDesert' at 21 km/h began to slow for a sharp bend 1 m ahead, needing 1.7 m/s2 to reach it at 21 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
```

`check2b-change4-speed20-Player-excerpt.txt`:

```text
2022: Couldn't find AI business logo: "BusinessLogos/Fenwick and Daughters/Billboard" with extension .jpg or .png
2023: [Mod:RealLifeOverhaul] Traffic check (proxy): 0 of 16 in use: PASS
2024: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: a job board or an employer's door has the interact key; out: 14 walker(s), 2 standing or sitting; the nearest: not looked for: PASS.
2025: [Mod:RealLifeOverhaul] Traffic check (proxy): 1 of 16 in use: PASS
2026: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2027: [Mod:PersistentCity] Road snapshot started: frame=5231, reason=clock-rate index=-1, scene searches in this planner=0.
2028: [Mod:RealLifeOverhaul] Traffic check (person ahead): 'Taxi' at 7 km/h, the person 3 m ahead, needs 4 m to stop: braking with whatever it takes (nothing is owed: it must stop): PASS
2029: [Mod:RealLifeOverhaul] Horn (a traffic car's): the car's light Green, 3 in earshot: 0 hurry on, 1 step back, 0 stop and glare, 1 look round.
2030: [Mod:RealLifeOverhaul] Traffic check (horn): 'Taxi': somebody stepped out 3 m ahead at 7 km/h: a hard brake: its horn sounds, and the people answer it: PASS
2031: [Mod:RealLifeOverhaul] Traffic check (proxy): 0 of 16 in use: PASS
```

`check2b-change5-speed3-Player-excerpt.txt`:

```text
2062: [Mod:RealLifeOverhaul] People: the 'hands_up_hold' clip is played by a scene character (said the first time only).
2063: Couldn't find AI business logo: "BusinessLogos/Fenwick and Daughters/Billboard" with extension .jpg or .png
2064: Couldn't find AI business logo: "BusinessLogos/Fenwick and Daughters/Billboard" with extension .jpg or .png
2065: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2066: [Mod:PersistentCity] Clock retime degraded 1 journeys: each keeps its old pace on its current road edge or drives outside traffic until its next road section.
2067: [Mod:PersistentCity] Road snapshot started: frame=5591, reason=clock-rate index=-1, scene searches in this planner=0.
2068: [Mod:RealLifeOverhaul] Traffic check (cost): 413 looks in the last minute, 112 microseconds on average and 2844 at worst (wanted: under 100 and 300); at most 12 cars looked at, 44 people in the grid, 4 of 16 stand-ins in use, 1 cars braked, 0 watched for contact now; mode StandIns: over budget; where it went, on average and in the worst look (microseconds): the cars gathered 19 and 37, the people 72 and 2796, the strips and the decisions 12 and 8, the stand-ins and the fades 9 and 2; the diagnostics' own lines are not counted; people looked over in a look, at most: 40 walking and 2 standing or sitting, 2 of those near enough when last listed to be read; outside the looks, 0 car(s) measured in frames of their own (0 microseconds at most) and 0 that took the figures of a car of their kind.
2070: [Mod:RealLifeOverhaul] Traffic check (city drivers' cost): 410 looks in the last minute, 90 microseconds a look on average and 1081 at worst, over 12 cars a frame (wanted: under 150 and 600); at most 12 cars read, 17 things on the roads, 9 cars slowed at once, 0 hold line(s): FAIL, over budget; of it the street read and its cars gathered 60 on average and 1001 in the worst look; outside the looks, 0 car(s) measured in frames of their own (0 microseconds at most) and 0 that took the figures of a car of their kind.
```

`check2b-change6-speed1-Player-excerpt.txt`:

```text
2116: [Mod:RealLifeOverhaul] Traffic check (person ahead): 'Taxi' at 36 km/h, somebody about to step out 5 m ahead, needs 17 m: they wait at the kerb: PASS
2117: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: a job board or an employer's door has the interact key; out: 14 walker(s), 2 standing or sitting; the nearest: not looked for: PASS.
2118: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'DeliveryTruck' at 24 km/h began to slow for a red light 19 m ahead, needing 1.2 m/s2 to reach it at 0 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
2119: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2120: [Mod:PersistentCity] Road snapshot started: frame=6116, reason=clock-rate index=-1, scene searches in this planner=0.
```

`check2b-initial-speed1-Player-excerpt.txt`:

```text
1714: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: the game is busy (a pause, the time machine, a question, the activity panel, the hospital, the subway or the casino boat); out: 0 walker(s), 0 standing or sitting; the nearest: not looked for: PASS.
1715: [Mod:FoodTrucks] Test bridge: teleport to x 215.0, z 0.0: y 0.01, the only ground surface, walkable; walking routes checked from the player's place.
1716: [Mod:FoodTrucks] Test bridge: moving the player to x 215.0, y 0.1, z 0.0.
1717: [Mod:RealLifeOverhaul] Street people: besides the walking pedestrians, the standing and sitting people of 7 of the game's pools count as people on the street (BenchPedestrian, CasualStationaryAi, SportPedestrian, SwimmingPoolBaseHuman, StreetPerformer, Homeless, Golfer).
1718: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 14 walker(s), 2 standing or sitting; the nearest: nobody within 12 m: PASS.
1719: [Mod:RealLifeOverhaul] Jobs check (board panel): at the board of 23 3rd Street (retail's office): the game's own panel offers the board on the game's interact key.
1721: [Mod:RealLifeOverhaul] Session check (work named for a long frame): the line of the frame of 133 ms leaves out 9 piece(s) of work begun after it, before the line was written; what the mod named up to its end is still named (a ring of 16): PASS.
1722: [Campaign Objectives] Visibility: NativeMenuOpen: OverlayUI.IsVisible=true, overlay RealLifeOverhaul.GameUi.WorldPanel (RealLifeOverhaul) (temporary block)
1724: [Mod:RealLifeOverhaul] Traffic check (senses): cars sense layers [Player, Human, Vehicles]; pedestrians are on 'Human' with their capsule on; sensor box 4.5 m long on 'AiVehicles', which does not meet the pedestrians' layer in the physics settings; mode: stand-ins on 'Player' for everybody in a car's way.
1725: [Mod:RealLifeOverhaul] Traffic check (proxy): 0 of 16 in use: PASS
1726: [Mod:RealLifeOverhaul] Traffic check (cost): 1 looks in the last minute, 912 microseconds on average and 912 at worst (wanted: under 100 and 300); at most 2 cars looked at, 9 people in the grid, 0 of 16 stand-ins in use, 0 cars braked, 0 watched for contact now; mode StandIns: over budget; where it went, on average and in the worst look (microseconds): the cars gathered 31 and 31, the people 836 and 836, the strips and the decisions 32 and 32, the stand-ins and the fades 13 and 13; the diagnostics' own lines are not counted; people looked over in a look, at most: 14 walking and 2 standing or sitting, 2 of those near enough when last listed to be read; outside the looks, 9 car(s) measured in frames of their own (219 microseconds at most) and 0 that took the figures of a car of their kind.
1727: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'DeliveryTruck' at 31 km/h began to slow for a red light 27 m ahead, needing 1.4 m/s2 to reach it at 0 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
1728: [Mod:RealLifeOverhaul] Traffic check (city drivers' cost): 1 looks in the last minute, 224 microseconds a look on average and 224 at worst, over 12 cars a frame (wanted: under 150 and 600); at most 3 cars read, 9 things on the roads, 1 cars slowed at once, 0 hold line(s): FAIL, over budget; of it the street read and its cars gathered 108 on average and 108 in the worst look; outside the looks, 8 car(s) measured in frames of their own (128 microseconds at most) and 2 that took the figures of a car of their kind.
1730: [Mod:RealLifeOverhaul] Civic: ready. Election 0 None, election day 15, approval 58, 6 site(s) in progress.
1731: [Mod:RealLifeOverhaul] Civic: site 1 (Rebuild) at 33 3rd Street shows its hoarding without the game's scaffold: no street side has room for the game's scaffold.
1732: [Mod:RealLifeOverhaul] Civic: site 1 (Rebuild) set out at 33 3rd Street, stage 1: 3 prop(s), 2 bay(s), 0 lift(s).
1733: [Mod:RealLifeOverhaul] Clips: the 'kneel_work' clip is played by a street character (said the first time only).
1734: [Mod:RealLifeOverhaul] Clips: the 'clipboard_write' clip is played by a street character (said the first time only).
1735: [Mod:RealLifeOverhaul] Civic: a crew of 2 at site 1 (Rebuild, stage 1).
1736: [Mod:RealLifeOverhaul] Contacts check (picture): 'reallife-cityhall' is a logo, from the game's logo shape 'headquarter3', kept (its file read on a worker, 0.03 ms of the main thread, wanted under 3 ms); 27 contact picture(s): 9 face(s), 18 logo(s), 27 drawn: PASS
```

`check2b-rapid-Player-excerpt.txt`:

```text
1809: [Mod:CityPerformance] TREE requested=True; installed=False; faulted=False; registered_seen=0; configured_records=0; skipped=0; changed_elsewhere=0; map_hold=False; lod_bias=1; maximum_lod=0; configured records are not visible-tree or selected-LOD counts.
1810: [Mod:FoodTrucks] Test bridge: shot check2b-change1-after (960x540) from x 215.0, y 18.0, z -18.0; uncovered SM_FancyBuilding_V03 Variant 3 (CityBuildingController, reveal 0.00, blocking the game's camera); outline lifted from SM_1StStreet_CheapRezidential_1; hover lifted from Prefab_5thAV_BuildingV1_2 (under the mouse, its building card open, 6 objects outlined): <rig-profile>/FoodTrucksTest/shots/check2b-change1-after.png.
1811: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: a job board or an employer's door has the interact key; out: 14 walker(s), 2 standing or sitting; the nearest: not looked for: PASS.
1812: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'MissamVillian' at 28 km/h began to slow for a red light 27 m ahead, needing 1.2 m/s2 to reach it at 0 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
1813: [Mod:RealLifeOverhaul] City life check (ground): sweeper at (304, 0, 4) would stand on parking or a driveway, the place refused: PASS
1814: [Mod:RealLifeOverhaul] Session: the managed heap grew by 14 MB in one frame of 34 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "city life: a site being set up" 0.0 s before, "transit: a parking lane beside a bus zone laid out again" 31.2 s before, "transit: a parking lane beside a bus zone laid out again" 31.5 s before; on foot in the street, game hour 8.
1815: [Mod:RealLifeOverhaul] Session: the managed heap grew by 8 MB in one frame of 33 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "city life: a site being set up" 0.1 s before, "transit: a parking lane beside a bus zone laid out again" 31.2 s before, "transit: a parking lane beside a bus zone laid out again" 31.6 s before; on foot in the street, game hour 8.
1816: [Mod:RealLifeOverhaul] Session: the managed heap grew by 8 MB in one frame of 34 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "city life: a site being set up" 0.1 s before, "transit: a parking lane beside a bus zone laid out again" 31.3 s before, "transit: a parking lane beside a bus zone laid out again" 31.6 s before; on foot in the street, game hour 8.
1817: [Mod:RealLifeOverhaul] Session: the managed heap grew by 13 MB in one frame of 33 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "city life: a site being set up" 0.2 s before, "transit: a parking lane beside a bus zone laid out again" 31.3 s before, "transit: a parking lane beside a bus zone laid out again" 31.7 s before; on foot in the street, game hour 8.
```

`check3-explicit-unpause-Player-excerpt.txt`:

```text
2527: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 0 walker(s), 0 standing or sitting; the nearest: nobody within 12 m: PASS.
2528: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'FreightTruckT1' at 34 km/h began to slow for a sharp bend 26 m ahead, needing 1.2 m/s2 to reach it at 19 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
2529: [Mod:RealLifeOverhaul] Patrol: two officers on the beat, 62 m away, 4 stops.
2530: [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 1 10:30.
2531: [Mod:PersistentCity] RLO single-pass read context=periodic status=Ready identities=400 elapsedMs=2.717 reads=8 totalMs=13.992 maxMs=3.283
2532: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 0 walker(s), 0 standing or sitting; the nearest: nobody within 12 m: PASS.
```

`check3-off-sales-Player-excerpt.txt`:

```text
2708: crowds: ordinary standers off in every native zone, 115 of them RLO's; RLO's own scene people kept
2709: traffic: ordinary native groups held for Persistent City cars; RLO staged accidents and reckless drivers stood down; cars loaded into held groups past the shares retired off screen; cars RLO holds are never retired
2710: [Mod:FoodTrucks] Test bridge: console 'pc capability status' (1 lines).
2711: [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 1 12:30.
2712: [Mod:CityPerformance] PERF frames n=686 mean_ms=43.78 p50_le_ms=35 p95_le_ms=126 p99_le_ms=250 max_ms=415.53 over50=67 bin250plus=9 refill=False
2713: [Mod:CityPerformance] PERF scope=TrafficManager.FixedUpdate calls=1333 total_ms=295.860 max_ms=0.346 exceptions=0
2714: [Mod:CityPerformance] PERF scope=GameManager.RunMainGameTick calls=686 total_ms=30.606 max_ms=4.437 exceptions=0
2715: [Mod:CityPerformance] PERF scope=CullingGroupController.Update calls=1372 total_ms=1.269 max_ms=0.003 exceptions=0
2716: [Mod:CityPerformance] PERF scope=SaveGameManager.SerializeSaveGame calls=0 total_ms=0.000 max_ms=0.000 exceptions=0
2717: [Mod:CityPerformance] PERF scope=PedestrianSpawner.SetActivePedestrians calls=2 total_ms=33.234 max_ms=33.230 exceptions=0
2718: [Mod:CityPerformance] DETAIL hooks=0; retained=0; overwritten=0; daily_retained=0; daily_overwritten=0; session_worst_method=none; max_ms=0; full breakdown in exported methods CSV and capabilities.
2719: [Mod:CityPerformance] TREE requested=True; installed=False; faulted=False; registered_seen=0; configured_records=0; skipped=0; changed_elsewhere=0; map_hold=False; lod_bias=1; maximum_lod=0; configured records are not visible-tree or selected-LOD counts.
2720: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2721: [Mod:PersistentCity] Clock retime degraded 1 journeys: each keeps its old pace on its current road edge or drives outside traffic until its next road section.
2722: [Mod:PersistentCity] Road snapshot started: frame=5959, reason=clock-rate index=-1, scene searches in this planner=0.
```

`check3-off-speed1-Player-excerpt.txt`:

```text
2655: crowds: ordinary standers off in every native zone, 115 of them RLO's; RLO's own scene people kept
2656: traffic: ordinary native groups held for Persistent City cars; RLO staged accidents and reckless drivers stood down; cars loaded into held groups past the shares retired off screen; cars RLO holds are never retired
2657: [Mod:FoodTrucks] Test bridge: console 'pc capability status' (1 lines).
2658: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2659: [Mod:PersistentCity] Road snapshot started: frame=5760, reason=clock-rate index=-1, scene searches in this planner=0.
2660: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 40 walker(s), 0 standing or sitting; the nearest: nobody within 12 m: PASS.
```

`check3-on-sales-Player-excerpt.txt`:

```text
2992: crowds: ordinary standers off in every native zone, 115 of them RLO's; RLO's own scene people kept
2993: traffic: ordinary native groups held for Persistent City cars; RLO staged accidents and reckless drivers stood down; cars loaded into held groups past the shares retired off screen; cars RLO holds are never retired
2994: [Mod:FoodTrucks] Test bridge: console 'pc capability status' (1 lines).
2996: [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 1 14:30.
2997: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2998: [Mod:PersistentCity] Road snapshot started: frame=8758, reason=clock-rate index=-1, scene searches in this planner=0.
2999: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 40 walker(s), 0 standing or sitting; the nearest: nobody within 12 m: PASS.
```

`check3-on-speed1-Player-excerpt.txt`:

```text
2921: [Mod:RealLifeOverhaul] Navigation: 27 m off the route for 2 s (on foot, 0 m along it): looking for a new route.
2922: [Mod:RealLifeOverhaul] Navigation: route found to 'Police incident' (on foot; off the route): 10 lane points, 47 m, 1 turns, 31 search steps in 1 slices, 0.02 ms in all (longest slice 0.02 ms).
2923: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: the game is busy (a pause, the time machine, a question, the activity panel, the hospital, the subway or the casino boat); out: 0 walker(s), 0 standing or sitting; the nearest: not looked for: PASS.
2924: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2925: [Mod:PersistentCity] Road snapshot started: frame=8568, reason=clock-rate index=-1, scene searches in this planner=0.
```

`check3-speed20-to1030-Player-excerpt.txt`:

```text
2425: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: the game is busy (a pause, the time machine, a question, the activity panel, the hospital, the subway or the casino boat); out: 0 walker(s), 0 standing or sitting; the nearest: not looked for: PASS.
2426: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: the game is busy (a pause, the time machine, a question, the activity panel, the hospital, the subway or the casino boat); out: 0 walker(s), 0 standing or sitting; the nearest: not looked for: PASS.
2427: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: closed: the game is busy (a pause, the time machine, a question, the activity panel, the hospital, the subway or the casino boat); out: 0 walker(s), 0 standing or sitting; the nearest: not looked for: PASS.
2428: [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 1 10:30.
2429: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2430: [Mod:PersistentCity] Road snapshot started: frame=3619, reason=clock-rate index=-1, scene searches in this planner=0.
2431: [Mod:RealLifeOverhaul] Street people: besides the walking pedestrians, the standing and sitting people of 7 of the game's pools count as people on the street (BenchPedestrian, CasualStationaryAi, SportPedestrian, SwimmingPoolBaseHuman, StreetPerformer, Homeless, Golfer).
2434: [Mod:RealLifeOverhaul] Session check (work named for a long frame): the line of the frame of 166 ms leaves out 2 piece(s) of work begun after it, before the line was written; what the mod named up to its end is still named (a ring of 16): PASS.
2435: [Mod:RealLifeOverhaul] Meta: props notice shown: the props of reallife_props cannot be drawn: the game's lit shader is not supported on this graphics setting.
2436: [Mod:RealLifeOverhaul] Meta: Meta check (welcome notice): shown as the game's own notification with a click that opens the Guide on 'What changed': PASS.
2438: [Mod:RealLifeOverhaul] Traffic check (senses): cars sense layers [Player, Human, Vehicles]; pedestrians are on 'Human' with their capsule on; sensor box 4.5 m long on 'AiVehicles', which does not meet the pedestrians' layer in the physics settings; mode: stand-ins on 'Player' for everybody in a car's way.
2439: [Mod:RealLifeOverhaul] Traffic check (proxy): 0 of 16 in use: PASS
2440: [Mod:RealLifeOverhaul] Traffic check (cost): 1 looks in the last minute, 917 microseconds on average and 917 at worst (wanted: under 100 and 300); at most 2 cars looked at, 39 people in the grid, 0 of 16 stand-ins in use, 0 cars braked, 0 watched for contact now; mode StandIns: over budget; where it went, on average and in the worst look (microseconds): the cars gathered 28 and 28, the people 851 and 851, the strips and the decisions 28 and 28, the stand-ins and the fades 10 and 10; the diagnostics' own lines are not counted; people looked over in a look, at most: 40 walking and 0 standing or sitting, 0 of those near enough when last listed to be read; outside the looks, 6 car(s) measured in frames of their own (121 microseconds at most) and 0 that took the figures of a car of their kind.
2441: [Mod:RealLifeOverhaul] Car ownership: records read.
2442: [Mod:RealLifeOverhaul] City events: state loaded; day 1 of a 60-day year (Winter), today: nothing on.
2443: [Mod:RealLifeOverhaul] Civic: ready. Election 0 None, election day 15, approval 58, 6 site(s) in progress.
2444: [Mod:RealLifeOverhaul] Contacts check (picture): 'reallife-cityhall' is a logo, from the game's logo shape 'headquarter3', kept (its file read on a worker, 0.02 ms of the main thread, wanted under 3 ms); 27 contact picture(s): 9 face(s), 18 logo(s), 27 drawn: PASS
2445: [Mod:RealLifeOverhaul] Traffic check (city drivers' cost): 1 looks in the last minute, 177 microseconds a look on average and 177 at worst, over 12 cars a frame (wanted: under 150 and 600); at most 5 cars read, 6 things on the roads, 0 cars slowed at once, 0 hold line(s): FAIL, over budget; of it the street read and its cars gathered 98 on average and 98 in the worst look; outside the looks, 9 car(s) measured in frames of their own (125 microseconds at most) and 1 that took the figures of a car of their kind.
2446: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'VordV150' at 22 km/h began to slow for a red light 11 m ahead, needing 1.8 m/s2 to reach it at 0 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
2447: [Mod:RealLifeOverhaul] Map links: construction on the city map with 6 place(s).
2449: [Mod:RealLifeOverhaul] Map places: 1 row(s) added to the city map's filters (under special buildings).
```

`check3-off-toggle-excerpt.txt`:

```text
2658: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2659: [Mod:PersistentCity] Road snapshot started: frame=5760, reason=clock-rate index=-1, scene searches in this planner=0.
2660: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 40 walker(s), 0 standing or sitting; the nearest: nobody within 12 m: PASS.
2661: [Mod:RealLifeOverhaul] Delivery trips and food spoilage: stood down (the options are off); the game's own logistics run every route again.
2662: [Mod:RealLifeOverhaul] Suppliers: switched off; the contracts and what is owed are kept in the save and go on, each at its next delivery day, when it is on again.
2663: [Mod:RealLifeOverhaul] Rivals seen: switched off; what the save holds of the rivals is kept and goes on when it is on again.
2664: [Mod:RealLifeOverhaul] Flare check (the residents' save): 418 KB in 13.5 ms on 6 worker(s): PASS
2665: [Mod:RealLifeOverhaul] Flare in the city: stood down (the option is off); Flare's talk about a business is in business pressure's figure again.
2666: [Mod:RealLifeOverhaul] Population check (the save): 1307 KB in 50.1 ms on 6 worker(s): PASS
2667: [Mod:RealLifeOverhaul] Living population: stood down (the option is off); the game's own customers and the city economy's own figures stand.
2668: [Mod:PersistentCity] RLO Living population on -> Living population off during play; Persistent City keeps owning its shops, crowds and traffic.
2669: [Mod:RealLifeOverhaul] Session: the managed heap grew by 30 MB in one frame of 198 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "flare: the hour's milestones" 15.4 s before, "flare: the hour's words asked ahead" 15.5 s before, "flare: the hour's unread count" 15.5 s before; on foot in the street, game hour 10.
2670: [Mod:PersistentCity] Slow routing frame=5838, realtime=170.213, prepared=False, preparation_ms=28.894, closure_ms=0.000, last_rebuild=clock-rate index=-1.
2671: [Mod:RealLifeOverhaul] Session: the managed heap grew by 13 MB in one frame of 33 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "flare: the hour's milestones" 16.9 s before, "flare: the hour's words asked ahead" 16.9 s before, "flare: the hour's unread count" 17.0 s before; on foot in the street, game hour 10.
2672: [Mod:RealLifeOverhaul] Session: the managed heap grew by 11 MB in one frame of 34 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "flare: the hour's milestones" 16.9 s before, "flare: the hour's words asked ahead" 17.0 s before, "flare: the hour's unread count" 17.0 s before; on foot in the street, game hour 10.
2673: [Mod:RealLifeOverhaul] Session: the managed heap grew by 16 MB in one frame of 34 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "flare: the hour's milestones" 17.0 s before, "flare: the hour's words asked ahead" 17.0 s before, "flare: the hour's unread count" 17.0 s before; on foot in the street, game hour 10.
2674: [Mod:PersistentCity] Slow routing frame=5863, realtime=171.112, prepared=True, preparation_ms=0.238, closure_ms=157.293, last_rebuild=clock-rate index=-1.
2675: [Persistent City] RLO traffic scenes that take over native cars stood down while Persistent City cars hold traffic (2 of 2 switches, a scene under way ended).
2676: [Persistent City] Ordinary ambient pedestrian requests: suppressed; RLO scenes and global spawning retained.
2677: [Persistent City] Stationary crowds: the game's anonymous standers are off (574 zones, 115 of them RLO's, 0 in view cleared); street performers and the homeless kept; RLO's own scene people untouched.
2678: [Mod:PersistentCity] Physical population active: ordinary pedestrian and vehicle spawns replaced by own bodies and own cars; RLO scenes, customers, workers, taxis and service vehicles retained.
2679: [Mod:RealLifeOverhaul] Session: the managed heap grew by 23 MB in one frame of 33 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "flare: the hour's milestones" 17.5 s before, "flare: the hour's words asked ahead" 17.6 s before, "flare: the hour's unread count" 17.6 s before; on foot in the street, game hour 10.
2680: [Mod:RealLifeOverhaul] Session: the managed heap grew by 8 MB in one frame of 335 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "flare: the hour's milestones" 17.6 s before, "flare: the hour's words asked ahead" 17.6 s before, "flare: the hour's unread count" 17.6 s before (1 begun after it left out); on foot in the street, game hour 10.
2681: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 15 walker(s), 0 standing or sitting; the nearest: nobody within 12 m: PASS.
```

`check3-on-toggle-excerpt.txt`:

```text
2924: [Persistent City] RLO traffic scene switches given back (the player's stored options).
2925: [Mod:PersistentCity] Road snapshot started: frame=8568, reason=clock-rate index=-1, scene searches in this planner=0.
2928: [Mod:RealLifeOverhaul] Delivery trips and food spoilage: started with 0 route(s) kept, 0 trip(s) on the road or waiting, 0 run(s) in the week's log and 1 place(s) with goods that spoil.
2929: [Mod:RealLifeOverhaul] Suppliers: read the save's line (version 1): 0 contract(s) running, $0.00 owed.
2930: [Mod:RealLifeOverhaul] Rivals seen: read the save's line (version 1): 19 rival(s) on record, 1 sighting(s) kept.
2931: [Mod:RealLifeOverhaul] Rivals seen: 1 sighting(s) went out at 12:00 on day 1, 1 of 3 posts today.
2932: [Mod:PersistentCity] RLO Living population off -> Living population on during play; Persistent City keeps owning its shops, crowds and traffic.
2933: [Mod:RealLifeOverhaul] Session: the managed heap grew by 19 MB in one frame of 34 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "the game is saving (the mod writes its state into the save first)" 8.1 s before, "crime: a scene starting" 15.4 s before, "crime: a scene starting" 15.4 s before; on foot in the street, game hour 12.
2934: [Mod:RealLifeOverhaul] Game UI: the game's app icon could not be read back, the glyph alone is used.
2935: [Mod:RealLifeOverhaul] Game UI: app 'RealLifeParties' is on the phone. Button copied from appButtonTemplate (HUD) and appButtonTemplate (full menu); icon Icon-People; it goes into the grids with the others of its line, after the last; its panel is built when it is first opened.
2936: [Mod:RealLifeOverhaul] Game UI: the game's app icon could not be read back, the glyph alone is used.
2937: [Mod:RealLifeOverhaul] Game UI: app 'RealLifeSuppliers' is on the phone. Button copied from appButtonTemplate (HUD) and appButtonTemplate (full menu); icon Icon-Box; it goes into the grids with the others of its line, after the last; its panel is built when it is first opened.
2938: [Mod:RealLifeOverhaul] Phone apps check (paced install): 3 item(s) over 3 frame(s), 2.8 ms in all; the slowest frame 1.06 ms, the slowest item 1.06 ms (RealLifeSuppliers); wanted: no frame over 2 ms; at most 1 app(s) put on in a frame (wanted 1); the phones searched 0 time(s) for 2 name(s) asked (wanted at most 1); the pages laid out 0 time(s) while they went on (wanted 0), then once on each phone (34 apps on the HUD phone, 3 page(s); the full menu's row 3 page(s)): PASS.
2939: [Mod:RealLifeOverhaul] Traffic check (braking in good time): 'VordTiaraVic' at 34 km/h began to slow for a red light 36 m ahead, needing 1.3 m/s2 to reach it at 0 km/h (an ordinary slowing is under 4.5; the game alone would have stopped it within 1.25 m of its sensor): PASS
2940: [Mod:RealLifeOverhaul] Session: the managed heap grew by 20 MB in one frame of 35 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "the game is saving (the mod writes its state into the save first)" 8.7 s before, "crime: a scene starting" 16.0 s before, "crime: a scene starting" 16.0 s before; on foot in the street, game hour 12.
2941: [Mod:RealLifeOverhaul] Session: the managed heap grew by 17 MB in one frame of 34 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "the game is saving (the mod writes its state into the save first)" 8.8 s before, "crime: a scene starting" 16.0 s before, "crime: a scene starting" 16.0 s before; on foot in the street, game hour 12.
2942: [Mod:RealLifeOverhaul] Session: the managed heap grew by 21 MB in one frame of 46 ms; under the mod's own scripts: 4 KB (Camera: ThirdPersonCamera.Update 4 KB); the rest is the game's, or another mod's; the mod's last pieces of work: "the game is saving (the mod writes its state into the save first)" 8.8 s before, "crime: a scene starting" 16.1 s before, "crime: a scene starting" 16.1 s before; on foot in the street, game hour 12.
2943: [Mod:RealLifeOverhaul] Navigation: 27 m off the route for 2 s (on foot, 0 m along it): looking for a new route.
2944: [Mod:RealLifeOverhaul] Navigation: route found to 'Police incident' (on foot; off the route): 10 lane points, 47 m, 1 turns, 31 search steps in 1 slices, 0.02 ms in all (longest slice 0.02 ms).
2945: [Mod:RealLifeOverhaul] Population check (the customers' count at the load): the factory hands out the mod's calculators: PASS
2946: [Mod:RealLifeOverhaul] Living population: loaded (version 5) on day 1: 19738 households, 56356 people in 1426 buildings.
2947: [Mod:PersistentCity] Slow routing frame=8656, realtime=279.088, prepared=True, preparation_ms=0.236, closure_ms=144.560, last_rebuild=clock-rate index=-1.
2948: [Persistent City] RLO traffic scenes that take over native cars stood down while Persistent City cars hold traffic (2 of 2 switches, a scene under way ended).
2949: [Persistent City] Ordinary ambient pedestrian requests: suppressed; RLO scenes and global spawning retained.
2950: [Persistent City] Stationary crowds: the game's anonymous standers are off (574 zones, 115 of them RLO's, 0 in view cleared); street performers and the homeless kept; RLO's own scene people untouched.
2951: [Mod:PersistentCity] Physical population active: ordinary pedestrian and vehicle spawns replaced by own bodies and own cars; RLO scenes, customers, workers, taxis and service vehicles retained.
2952: [Mod:RealLifeOverhaul] Session: the managed heap grew by 32 MB in one frame of 35 ms; under the mod's own scripts: not known (not a sampled frame); the mod's last pieces of work: "the game is saving (the mod writes its state into the save first)" 9.3 s before, "crime: a scene starting" 16.6 s before, "crime: a scene starting" 16.6 s before; on foot in the street, game hour 12.
2954: [Mod:RealLifeOverhaul] Street talk check (offer): nothing offered: nobody who can be talked to within 3.2 m and 60 deg of the player's facing; out: 33 walker(s), 0 standing or sitting; the nearest: nobody within 12 m: PASS.
2956: [Mod:RealLifeOverhaul] Flare in the city: read (version 1): 992 accounts, 0 measured posts, 0 ads; the residents' day runs in slices from now.
2957: [Mod:RealLifeOverhaul] Flare in the city: day 1, 56356 residents' follows read back: 34 residents follow you, 0 reviews by customers, 0 new followers of businesses from their visits, 6 ad(s) shown, 0 post(s) heard, 0 voice(s) new; Flare's attention brought 0 of takings yesterday.
2958: [Mod:RealLifeOverhaul] Flare check (the residents' day): 34 residents follow you against the account's city share of 34 (43040 adults), 136 businesses with attention for tomorrow (11 thousandths on average), 0 of takings moved yesterday: PASS
2960: [Mod:RealLifeOverhaul] People: first runner of the session (Jog): asked for 4.0 m/s, reached 4.0 m/s and covered 10.3 m in 2.7 s; acceleration 14 m/s2 (the game's own 8), turning 360 deg/s (the game's own 120), gives way at priority 15, pavement agent, animator 'CharacterAnimator'.
```

Clock completion and degraded-retime lines from all full logs:

```text
check1-final:2672: [Mod:FoodTrucks] Test bridge: run_until day 1 10:30 reached at day 1 10:30; paused, speed 1.
check2b-final:2066: [Mod:PersistentCity] Clock retime degraded 1 journeys: each keeps its old pace on its current road edge or drives outside traffic until its next road section.
check3-final:2625: [Mod:FoodTrucks] Test bridge: run_until day 1 10:30 reached at day 1 10:30; paused, speed 1.
check3-final:2721: [Mod:PersistentCity] Clock retime degraded 1 journeys: each keeps its old pace on its current road edge or drives outside traffic until its next road section.
check3-final:2871: [Mod:FoodTrucks] Test bridge: run_until day 1 12:30 reached at day 1 12:30; paused, speed 1.
check3-final:3154: [Mod:FoodTrucks] Test bridge: run_until day 1 14:30 reached at day 1 14:30; paused, speed 1.
```


Both headless setups repeated 08:00 clock readings, then advanced after an explicit same-speed `pause:false` retry; no cause is inferred. Its second speed-20 log line is also retained verbatim:

```text
2563: [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 1 10:30.
```

### Logged durations of 30 ms or more

No perf window, GC sampler, mod-work sampler or save profiler was armed. Only already emitted PC log durations were inspected; these are not p95 measurements or a timing gate. Aggregate/maximum fields on one log line can describe overlapping work and are not independent spike events.

| Process log | Line | Reported ms values at least 30 |
|---|---|---|
| `check1-final-Player.log` | 2399 | 601 |
| `check1-final-Player.log` | 2430 | 138.523, 138.523, 138.376, 118.031, 187.143, 125.616 |
| `check1-final-Player.log` | 2543 | 3557.040, 136.719, 136.717, 187.143, 31.968, 3320.256 |
| `check2-final-Player.log` | 1692 | 601 |
| `check2-final-Player.log` | 1721 | 141.643, 141.643, 141.485, 120.765, 227.103, 128.823, 30.877, 30.877 |
| `check2-final-Player.log` | 1873 | 1013.691, 139.487, 139.484, 227.103, 560.337, 103.958, 491.590 |
| `check2-final-Player.log` | 1994 | 4235.352, 227.103, 1293.234, 154.076, 4010.874 |
| `check2-final-Player.log` | 2153 | 1772.778, 40.925, 227.103, 1829.786, 216.511, 1446.327 |
| `check2b-final-Player.log` | 1688 | 601 |
| `check2b-final-Player.log` | 1711 | 144.766, 144.766, 144.602, 121.587, 217.058, 131.001, 32.025, 32.025 |
| `check2b-final-Player.log` | 1828 | 1919.786, 145.466, 145.464, 217.058, 715.933, 98.993, 1395.213 |
| `check2b-final-Player.log` | 1920 | 3903.212, 217.058, 1269.306, 150.772, 3671.550 |
| `check2b-final-Player.log` | 2040 | 1611.571, 42.828, 217.058, 1850.484, 190.646, 1245.211 |
| `check3-final-Player.log` | 2388 | 601 |
| `check3-final-Player.log` | 2423 | 135.620, 135.620, 135.471, 128.051, 209.479, 135.878 |
| `check3-final-Player.log` | 2536 | 3596.458, 135.575, 135.573, 209.479, 38.882, 3322.466 |
| `check3-final-Player.log` | 2737 | 2479.368, 209.479, 1744.508, 157.293, 2106.248 |
| `check3-final-Player.log` | 2818 | 5794.997, 209.479, 2103.403, 5461.415 |
| `check3-final-Player.log` | 3060 | 2492.885, 62.414, 209.479, 1900.764, 144.560, 2127.974 |
| `check3-final-Player.log` | 3145 | 5777.878, 92.920, 209.479, 2109.712, 5505.244 |

### NOT RUN grouped

1. **#244 P-D check 7:** Rig bot console-guard fix agent-rig #85 remains open; no non-FT console attempt in this run.
2. **GR-5 GC/street re-checks:** #254 measured by rig 14b, PR #256; next step pending owner route. No 600-second performance window in this run.
3. **#254 check 4 appearance:** rig 14b tooling gap, reacquired-resident identity; sent to the Rig bot.
4. **GR-5 check 4 footprint:** rig 14b building op returned `ops_game_api_unavailable`; sent to the Rig bot. Not retried here.
5. **GR-5 60 fps arms:** rig 14b Mac cadence 5 fps; sent to the Rig bot. Temporary 30-fps screenshot configuration is not 60-fps evidence.
6. **GR-5 save latency:** owner decision pending. Manual functional checkpoints are not latency measurements.

### Tooling gaps and recovered input errors

- Still captures do not establish continuous visible no-jump proof; check 2 is partial.
- Exact rejected operation: `call shoot` with `frames:1`, including the initial and first 1 → 3 pair attempts. Error: `frames must be a whole number from 8 to 240.` Corrected to frames=8. No additional runtime-stop failure is attributed to it.
- First camera x=176,y=18,z=-56 returned `pose_note: the camera stands inside something solid (a building, a prop or a vehicle); the picture may be dark`. Images show building walls and cannot support a visual pass. Corrected to the documented street camera and repeated cold.
- Optional `call ui {}` returned `Missing "op": list, click, set, escape or tree.` Corrected to `call ui {"op":"list"}`; no native UI state was changed by the read.
- There is no commit-stamped PC build log. The staged DLL hash and receipt establish the tested build; activation lines establish the loaded runtime.

### Fixtures and restoration

Reused only `FT RLO 13d Food v1`. Created disposable manual checkpoints: `FT RLO 14c Retime 1030`, `FT RLO 14c Off 1230`, `FT RLO 14c On 1430`. No existing fixture was overwritten; these saves remain isolated. No throwaway non-FT city was needed.

The helper printed this rollback verbatim:

```sh
rm -rf "<rig-profile>/ModsLocal/PersistentCity" && cp -Rp "<rig-tools>/receipt-backups/20261114T134245Z/mod" "<rig-profile>/ModsLocal/PersistentCity" && cp "<rig-tools>/receipt-backups/20261114T134245Z/install.json" "<rig-tools>/install.json"
```

The execution layer rejected the printed rollback before executing it: `rm -f style commands are not permitted. Use a safer approach`. With the game stopped and the lease held, the safe equivalent moved the staged PersistentCity folder intact to `<stage>/retained-staged-mod`, copied the helper’s original `mod/` into the empty live path, then copied its original install.json. No receipt was hand-edited. Changed original profile files and the plist were restored from the independent backup. All 39 original file/absence hashes match, the mod file set has no extra or missing file, all 53 RLO file hashes match, and the full install receipt validates. AgentRigOps retains its original SHA-256. The RLO op restore independently returned stored=null/live=true/original_saved=false before shutdown. All four game stops were graceful; no force. Final status: running=false, process=null, lease held=false, pending=0. No process started for this run remains running. Frame-cap config is back to its original absence; native autosave preferences are restored, and process-only suppression ended at stop. The official agent-rig checkout remains clean.


Validation: candidate build passed. `git diff --check`, markdown ban and docs-links passed for the docs-only result. Only the existing checklist was changed; no raw evidence, binary, save or new markdown path is committed. The build/evidence worktree is removed after commit/push and PR creation. The PR is left open, unmerged, without reviewers.

## Rig results — 14 November 2026 e, PC (#244 P-D check 7 at 2448bc40)

**RESULT: BLOCKED.** Counts: **0 passed / 0 partial / 0 failed / 9 NOT RUN arms**. The required P-D check 7 is NOT RUN: the single default Windows start failed before launching a player. The other eight arms were excluded by the run scope. This is no in-game proof and gates no merge.

### Build and rig

| Item | Evidence |
|---|---|
| Role / instance | rig_test, Windows PC, agent1; owner `pc-rig-2026-11-14e` throughout. No Mac contacted. |
| Candidate | `2448bc40f47c67c0a84813e0e4f6a9b5c58dd9fe`, fetched `origin/codex/persistent-city`. Moved from requested minimum `913abd7d` by docs-only PR #258. |
| Worktrees | New branch `codex/pc-rig-2026-11-14e` in `rig-2026-11-14e-pc-evidence`; detached build in `rig-2026-11-14e-pc`. Original checkout stayed clean. |
| Agent-rig | Official checkout `4bcf80aa04431f3b54cac19335c9c4cbb57a4b48`; clean before fetch and `git pull --ff-only` (`Already up to date.`). No checkout edit or overlay update. |
| Live AgentRigOps.dll SHA-256 | `fe4c40c1ba70512eb0b14704e275c1fa4842e98100ba563cc2d559a13be6bf6a`; unchanged after cleanup. |
| Build | `python tools/build.py --game-managed "C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"` passed. PATH prefixed with `%LOCALAPPDATA%\Microsoft\dotnet`; wait-other-dotnet helper printed `dotnet lock removed 2026-11-10; continuing`. No offline test suite. |
| PersistentCity.dll SHA-256 | `087fe9aa8c506c3d2fa43df62fec0f08450095ca415f7c3ffebf64bfd28bde29`; built package and copied candidate matched. No loaded-build line: player never started. |
| Package / raw evidence | `<stage>`, the run's local staging folder `persistent-city-rig-2026-11-14e-pc`; package under `<stage>/package/PersistentCity`. Build/start output, pre-stage manifest, backups, retained candidate and restore verification stay outside Git. No RLO package files copied. |
| Staging command | `python <stage>/backup-and-stage.py`: full pre-copy backup, then hash-verified `shutil.copytree(package, isolated ModsLocal/PersistentCity, dirs_exist_ok=True)`. Windows scoped receipt-refresh helper was not found; used the authorized copy fallback. No receipt hand-edit. |
| Backup / rollback source | `<stage>/pre-stage-backup`. Restoration used this run's `PersistentCity` folder and independently backed-up bytes, via `python <stage>/restore.py`. |
| RLO version / PC startup | NOT OBSERVED: no player launched and no current startup lines exist. Prior log contents are not evidence for this build. |
| Machine load | Setup observation: CPU 32%; physical memory 66,938,060 KiB total / 25,647,708 KiB free (about 63.84 / 24.46 GiB). No game process observed. Owner-reported live harness `20261114-144821-pc-fix-gr5-spawn-appearance` remained independent; no wait for load, cancellation, debugging or access to its branch/files. |
| Cap health | Local launcher status after cleanup: `running=false`, `pid=null`, `health=null`. No frame-cap health readback was available. Background default was requested; no windowed/60-fps start or frame-cap file change. |

### P-D check 7 and exact tooling errors

Read PR #244 and PR #246 through REST and the prior 14a / 14b / 13g results. The intended fixture was unsaved non-FT `RIGTMP-PD7-14e`. Agent1 was free and acquired without takeover at `2026-11-14T14:35:54.7615080Z`.

The single start attempt in the official agent-rig checkout, with `BA_LOCK_OWNER=pc-rig-2026-11-14e`, was:

```text
python scripts/agent start
ssh: Could not resolve hostname homepc: No such host is known.
START_EXIT_CODE=1
```

The CLI uses its SSH transport for lifecycle operations. Its earlier read-only `python scripts/agent status` produced the same exact hostname error. No SSH configuration or CLI was changed. No retry, `--rig-test` start, direct launch or FT fallback was attempted after the failed start, as required by the one-attempt rule.

| P-D 7 evidence | Result |
|---|---|
| `call new_game {"name":"RIGTMP-PD7-14e","throwaway":true}` | NOT ATTEMPTED: no player. No new_game refusal is claimed. |
| `call console {"line":"pc gc population"}` | NOT ATTEMPTED. Exact mod console output unavailable. |
| `call console {"line":"pc gc population 3000"}` | NOT ATTEMPTED. Expected substring `refused: measurement mode needs a disposable FT city` was not observed. No `MEASUREMENT MODE` output is claimed. |
| Persons before / immediately after / after one in-game hour | Unavailable / unavailable / unavailable. No city ran; no hour elapsed. |
| Matching Player-agent.log lines | None for this run. Existing log stayed 86,702 bytes with last write `2026-11-14T14:01:58.840534Z`, before the lease/start. No stale startup/refusal lines substituted. |
| Autosave | Complete original isolated preference/config bytes backed up and matched after cleanup. Numeric native autosave setting was not identified from offline settings; runtime OFF could not be verified because startup failed. No city or timed window ran and no autosave setting was changed. |

### Findings and tooling gaps

The observed blocker is default PC CLI transport, not a measurement-mode refusal in the mod. Agent-rig #85 is present in the official checkout and the live Ops hash is recorded, but this run did not exercise its RIGTMP console guard.

There is also a staging limitation: the Windows checkout has no scoped package/receipt refresh comparable to the Mac helper. The authorized copy fallback verified all six package files, but left the original receipt unchanged. The copied `PersistentCity.dll`, `persistent-city.json` and `README.md` differ from the original pinned receipt. A receipt-validated local launch was not attempted after the transport failure; this is a pending staging requirement, not a second observed launch error. No receipt protection was bypassed.

The documentation still requires a rig-test launch for throwaway `new_game` and console acceptance. This run obeyed the default-start-only instruction and did not retry the previously reported unsupported flag. The earlier `agent: error: unrecognized arguments: --rig-test` is background from rig 14d, not an error reproduced here.

### NOT RUN grouped

Nine counted arms, with no product FAIL recorded:

1. **Required P-D 7 (one arm):** default start failed with the exact SSH hostname error above; console refusal and unchanged-person proof unavailable.
2. **GR-5 60 fps (two arms: street / busy shop):** excluded; rig 14d / PR #257 found Windows CLI refuses `--rig-test` and no Windows scoped package stage exists. Rig bot request pending. No timing or cap proof attempted.
3. **GR-5 check 4 footprint (one arm):** excluded; previous building op `ops_game_api_unavailable`, Rig bot fix pending. Not retried here.
4. **#254 check 4 appearance (one arm):** excluded; no stable resident identity through release and reacquire. No appearance attempt.
5. **GR-5 check 2 save latency (one arm):** excluded; owner save-route decision pending. No save or save profiling.
6. **#255 checks 1–3 (three arms):** excluded; covered by Mac rig 14c / PR #258. The zero-receipt finding has a fix run. No rerun or Mac contact.

### Fixtures, restoration and validation

Created fixtures: **none**. `RIGTMP-PD7-14e` was planned but never created; no save, checkpoint, fixture removal or existing save overwrite. No game process was started, so no stop/force-stop was needed and no owned game process remains.

The candidate folder was moved intact into this run's `retained-staged-PersistentCity`; the original folder was copied back from this run's pre-stage backup. Original receipt, preferences and configuration were preserved or restored only from this run's manifest. Cleanup verified **101 checks, zero mismatches**: 47 original file hashes, two expected absences (`agent-frame-cap.json` and root `persistent-city.json`), the isolated registry export, complete 41-file PersistentCity set, 48 RLO hashes read-only, unchanged Ops hash and unchanged log length. Registry export SHA-256: `8adb52a0d6a12694aa30f2ae209f94149e519240f3b32d97c11e2d6b389f9e4c`. RLO, Food Trucks and the overlay were never written.

Released with `python scripts/local_lock.py release`. Final local lease status: `held=false`, `owner=null`, `pending=0`. Direct read-only installed-launcher status: `installed=true`, `running=false`, `pid=null`, `health=null`. No other session's process was stopped. No normal game/profile or bridge ownership was used.

Only this existing checklist is changed. Build passed; docs-links, markdown-ban and whitespace checks are recorded in the results PR. Report copy: `<stage>/../../Coding_Projects/.agent-logs/persistent-city-rig-2026-11-14e-pc.md`, a path relative to `<stage>`; the report stays in the agent-log folder outside the stage. Build worktree is removed after commit; evidence worktree after opening the unmerged docs-only results PR. No production change, issue, release, version/changelog change or merge.

## Rig results — 14 November 2026 f, PC (#244 P-D 7 + #246 check 4 footprint + GR-5 60 fps arms at 0795aa2c)

Role `rig_test`, where `pc`, isolated `agent1`. **Counts: 0 passed / 0 partial / 0 failed / 11 NOT RUN. RESULT: BLOCKED.** Four required arms are waiting on Rig run `20261114-154927-rig-windows-pc-cli-stage`; seven excluded arms are counted separately below. The single installed-player start/status probe succeeded and is outside the product-check counts. In-game proof gates no merge.

### Build and rig

| Item | Evidence |
|---|---|
| Candidate | `0795aa2c503869e5ab1feb91bc0738e2a163bfb9`, newer than required `b243c43b19f374b982c38d8e96633aa4f8f2ee90` by ancestry check. Initial and final fetch agree. This is the base tip, without the unmerged spawn_appearance fix. |
| Worktrees | Results branch `codex/pc-rig-2026-11-14f` in `rig-2026-11-14f-pc-evidence`; detached build in `rig-2026-11-14f-pc`. Original checkout unchanged. |
| Official agent-rig | `022630f7d6df8c533a230ac218413a68b181ed95`; clean, fetched, ff-only pull already current, clean after use. No edit, commit, push or overlay update. |
| Live AgentRigOps SHA-256 | `9a8ad56e01df5d30920da35f5ba7f60e65009528b9c1abf3d5e8667bb1a2378b`; independently hashed before the run, health reports ready with that hash, unchanged at cleanup. |
| Lease | Stable owner `pc-rig-2026-11-14f`; acquired while free at `2026-11-14T15:02:18.4755551Z`, no takeover. Released after stopped-player restoration. |
| Build command | `python tools/build.py --game-managed "C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"`, exit 0. PATH prefixed with `%LOCALAPPDATA%\Microsoft\dotnet`; required wait helper printed `dotnet lock removed 2026-11-10; continuing`. No offline test suite. |
| Built/package PersistentCity DLL SHA-256 | `087fe9aa8c506c3d2fa43df62fec0f08450095ca415f7c3ffebf64bfd28bde29`; matching package retained outside Git. No RLO or game DLL packaged. |
| Installed PersistentCity DLL SHA-256 | `6e0e5b58776a5a4430b4b585e03f7233d1dc5dedfd94200cb7f603a44ea55de1`, mtime 11 November 2026 16:53:38 local. Matches its existing receipt entry; too old for all four required arms. |
| Installed receipt SHA-256 | `agent-install.json`: `7f4f73e05d6ee2eb70c31987f6a3f6c70ece33f4119f5fe61ad0e57aebad1761`, matching launch input and post-cleanup backup. |
| Staged DLL / staging command | **None / skipped.** One search after official fetch/pull found scoped stage/refresh only in Mac `install-mac.py`, documented in `docs/MAC.md`. Windows has no documented scoped receipt-safe helper. The whole-modset transaction is not a PersistentCity-only receipt refresh. No manual copy fallback, receipt hand-edit or unmerged tooling branch was used. Waiting on `20261114-154927-rig-windows-pc-cli-stage`. |
| Stage / backup | Package in `<stage>\package\PersistentCity`, this run's exact-byte backup in `<stage>\pre-stage-backup`, independent manifest `<stage>\manifest.json`. |
| RLO probe log | `[Mod:RealLifeOverhaul] Real Life Overhaul 0.45.0 initialised.` This is the installed probe's observed version. RLO was not changed. |
| PC probe log identity | No Persistent City build/activation line emitted during the menu-only probe. Health includes the loaded `PersistentCity` assembly; the installed DLL hash and launch receipt establish binary identity. No candidate or city activation is claimed. |
| Food Trucks | Health `mod_version=0.10.2`, bridge version 1. No update. |
| Autosave | Numeric native interval/count not obtained in the menu-only probe; no matching autosave-named registry value found. Complete isolated registry/preferences backed up. Probe health `rig_test.enabled=true`, `autosave_disabled=true`, `save_specific=false`; process-only suppression ended at stop. No city or measured window, no persistent autosave setting changed. |
| Frame-cap file | Original absence backed up and preserved; no request staged under the version gate. |
| Machine load | Setup only at `2026-11-14T15:03:43.8482826Z`: CPU 69%; RAM 66,938,060 KiB total / 26,991,224 KiB free (63.84 / 25.74 GiB). Notable working sets: unrelated game 6.74 GiB, WSL 6.26 GiB, memory compression 5.27 GiB, browser and other agent sessions. No measurement-start sample exists. Future timing on this host needs **provisional (PC shared)**. |

Read PR #244 and #246 through REST, prior 14a/14b/14d/14e checklist sections, and the official Windows/ops/throwaway/frame-cap documentation. No Mac contact, lifecycle CLI through `scripts/agent`, real mouse/keyboard action, Windows setting change, normal profile access or other session intervention occurred.

### Required checks

| Arm | Outcome | Missing evidence |
|---|---|---|
| #244 P-D 7, ordinary saves refused | **NOT RUN / version gate** | No `new_game`, `pc gc population` or `pc gc population 3000` call. Exact console refusal and matching log lines unavailable; persons before/immediately after/after one game hour all unavailable. |
| #246 check 4, unrentable home footprint | **NOT RUN / version gate** | No `op building {"action":"list"}` or building reads attempted. No current-run op error, three targets, rotated/yard/plain comparison, Home area, height or storey evidence. Prior fixed building-tool claims are not reused as this run's proof. |
| GR-5 street, cap 60, 600 s | **NOT RUN / version gate** | Measured fps, whole-frame p95/p99, gen0, pause p95, heap, UpdateCity/street mod-work p95 and every >=30 ms spike unavailable. No measurement window. |
| GR-5 busy shop, cap 60, 600 s | **NOT RUN / version gate** | Same unavailable metrics; no city or busy-shop setup. No measurement window. |

All four wait on Rig run `20261114-154927-rig-windows-pc-cli-stage`. No old-build measurement is substituted for the required pre-fix baseline.

### PowerShell route probe and exact tooling observations

With the lease held and `BA_LOCK_TOKEN` set privately from this owner's verified credential, one foreground start ran:

```powershell
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "C:\BigAmbitions-AgentTests\Agent-Windows.ps1" -Action start -Mode windowed -RigTest
```

Exit 0; launched only isolated PID **25848**. Status through the same PowerShell launcher returned running=true, scene=menu, session `7705384b`, Ops ready. No city was created or loaded. The startup log identifies isolated agent1, windowed, runtime 7, audio muted, Steam disconnected. Raw evidence: `<stage>\start.log`, `probe-status.json`, `probe-Player-agent.log`.

Probe cap health, not a 60 fps arm: `requested=null`, `applied=false`, `vsync_count=0`, `target_frame_rate=90`, `measured_fps=66.36`, `window_s=2.0`, `error=null`. No 60 fps compliance or product performance pass is claimed from menu cadence.

Graceful `-Action stop` exited 0: `Quit requested for the verified agent process`. Immediate status still showed quit pending; the first lease release returned exactly **`Commands or lifecycle work are still pending; wait for completion before releasing the lock`**. It was not forced or broken. After process exit, launcher status confirmed running=false/pid=null/health=null, restoration was verified again, and release succeeded with pending=0. No force-stop or second launch.

Recovered local inspection errors: `rg` was unavailable, so searches used PowerShell; registry `Get-ItemProperty` returned `Specified cast is not valid.`, so complete `reg export` and native registry enumeration were used; an initial read targeted the `tools/docs-links` directory and returned access denied, corrected to `tools/docs-links/check.py`. These were local read errors, not product/op failures.

### NOT RUN grouped

1. **Required version-gated group, four arms:** P-D 7, footprint check 4, street 60 fps and busy-shop 60 fps. Receipt-safe Windows stage missing; waiting on `20261114-154927-rig-windows-pc-cli-stage`.
2. **Appearance, one arm:** #254 check 4; no stable resident identity through release/reacquire, identity op still queued.
3. **Save latency, one arm:** GR-5 check 2; owner save-route decision pending. No save profiling.
4. **Retime, three arms:** #255 checks 1–3 / #239-3 already covered by rig 14c / PR #258; zero-receipt finding has a fix run.
5. **30 fps, two arms:** street and busy shop already attempted by rig 14b; this is the Windows 60 fps run.

### Fixtures, restoration and validation

Fixtures created/loaded/saved: **none**. Planned names `RIGTMP-PD7-14f` and `RIGTMP-GR5-14f` were not created. No save/load, fixture removal, existing save overwrite or native sales occurred.

Before startup, this run backed up the entire isolated PersistentCity file set, isolated root settings/configuration, persistent-city.json bytes/absence, frame-cap absence, install receipts/run receipt and complete isolated registry export. No candidate was staged. Post-exit restoration used only this run's backups; file bytes/absences and registry preferences match. **120 verification checks, zero mismatches**, including full PersistentCity file set, read-only RLO/Food Trucks hashes and the Ops DLL. Registry export SHA-256 `d44a9390f89ea5d15b98020c6901b32e226fcd0bca76be6009f4fabcfb93440a`. The generated probe log is retained as evidence, not rolled back. No RLO, Food Trucks or overlay file was written.

Final launcher status: **installed=true, running=false, pid=null, health=null**. PID 25848 no longer exists. Final agent1 lease **held=false, owner=null, pending=0**; no process started by this run remains. Release followed post-exit re-verification. Build worktree removed after the evidence commit; evidence worktree removed after PR creation.

Validation: build passed; docs-links, markdown-ban and `git diff --check` run for this existing-file-only docs change. No offline dotnet tests or regression suites. Report copy: `<home>\Coding_Projects\.agent-logs\persistent-city-rig-2026-11-14f-pc.md`. Raw evidence/backups stay outside Git. No issue, merge, release, Workshop upload, version/changelog or production code change. Schema 1 and both food-choice weights unchanged.

**RESULT: BLOCKED.**

## Rig results — 14 November 2026 f, Mac (#244 P-D 7 + #246 checks 4-5 + #260 checks 1-3 at 2f5e72eb)

**RESULT: DONE. Counts: 4 passed / 1 partial / 0 failed / 5 NOT RUN.** Six required checks: 4 passed, 1 partial, 0 failed and 1 NOT RUN. Four owner-excluded groups are also NOT RUN, giving five NOT RUN report items. In-game proof gates no merge; this is an unmerged docs-only evidence PR.

Role `rig_test`, instance `agentmac`, where `mac`; owner `persistent-city-rig-2026-11-14f-mac`. Raw evidence and independent backups: `<stage>` = `<home>/HART-Builds/persistent-city-rig-2026-11-14f-mac`. Report copy: `<home>/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-14f-mac.md`. All commands ran in the foreground and were awaited. No Windows connection, ordinary profile, Steam app, ordinary save, other session, release, merge, version/changelog or production-code change. Schema remains 1; FoodChoicePriceWeight 1.2 and FoodChoiceHouseholdWeight unchanged.

### Build and rig

| Item | Evidence |
| --- | --- |
| Candidate | `2f5e72eb5b51ad2449bfb692983ff7ce9cf062db`, exactly origin/codex/persistent-city at initial fetch. Final remote tip is recorded below. |
| Worktree | New `codex/mac-rig-2026-11-14f` in `rig-2026-11-14f-mac-evidence`; one isolated worktree served build and evidence. Original checkout stayed clean. |
| Build | `python3 tools/build.py --game-managed "<home>/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0, Homebrew PATH and DOTNET_ROOT. No full test suite. |
| PersistentCity DLL SHA-256 | `bc3a0bac4357d8c4e9435b8e506fc4c839d013440202fddb5c61c523a09b3e2f`; built, packaged and staged hashes match. Every start validated the install receipt. |
| Official agent-rig HEAD | `022630f7d6df8c533a230ac218413a68b181ed95`, clean; fetch and ff-only pull were already up to date. No edits, commit, push or overlay update. Includes #85 and #86. |
| Live AgentRigOps SHA-256 | `154be3036954c314d3bd9eb4409da98f6a9b3a508748f2b1811c457abce07955`, unchanged while running and at cleanup. Live ops ready. |
| RLO startup | `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` All 53 RLO package files hashed unchanged. No RLO install/edit. |
| PC log identity | A: `Persistent population active: 1000 residents, 1000 visitors, 1686 places.` B: `1000 residents, 2000 visitors, 1683 places.` C: `1611 residents, 1611 visitors, 1685 places.` Full lines below. No commit-stamped build line is emitted; the matching live DLL hash and validated receipt establish binary identity. |
| Lease / resources | Initially stopped/free, acquired `2026-11-14T15:52:58.0952490Z`. Startup resources 67.9 GiB disk / 76% reported memory free; vm_stat free+inactive+speculative about 6.76 GiB. No resource refusal, takeover or other-process termination. |
| Starts / stops | Three cold processes, one attempt each: A headless, B windowed, C headless, all with `--rig-test`. All starts succeeded. All stops graceful, no force. |
| Autosave | Native original 5 minutes / 3 saves, prevent_autosave=false. #70 process suppression confirmed after each load and before/after sales intervals. Preferences and configs restored byte for byte; suppression ends with the process. No save or checkpoint created. Final guard attempts/refused=20185/20185, default_allowed=0, armed_allowed=0, unrestricted=0; in_progress=false and compress_thread_alive=false. |
| Frame cap / backup | Temporary B `agent-frame-cap.json` fps=30, vsync=0, removed for C and restored to original absence. Independent pre-stage manifest covers 38 file/absence entries, including the whole PC folder, install receipt, isolated root preferences/config and plist. |

Exact staging command, game stopped and lease held, official agent-rig checkout:

```sh
python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package
```

Receipt backup: `<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T155326Z`. Package contains no RLO files. The rollback printed by staging is recorded below with paths redacted. Automatic command review rejected its removal command (`rm -f style commands are not permitted. Use a safer approach`). Stopped and leased, restoration instead renamed the staged folder into the raw evidence folder, copied back the entire backup mod folder and then copied back install.json. No receipt was edited; hashes and the exact file set were verified:

```sh
rm -rf "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T155326Z/mod" "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T155326Z/install.json" "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

### Per-check outcomes

| Required check | Result | Evidence |
| --- | --- | --- |
| A, #244 item 7, ordinary saves refused | PASS | Unsaved throwaway `RIGTMP-PD7-14f`. Both status and target calls reached the mod and returned its disposable-FT refusal. Live manifest: 2,000 persons before, immediately after and after more than one game hour. |
| B, #246 item 4, unrentable footprint | NOT RUN / fixture gap | Fixed `building list` succeeds: 585 residential rows, all rentable_interior=true, zero unrentable subjects. No eligible three-building sample, Home footprint/floor comparison or screenshot proof. |
| B, #246 item 5, unreadable/empty tally | PARTIAL | Exact load tally recorded: housed=0 beds=0 unreadable=0 empty=0. No unreadable or empty unrentable subjects, so the classification and omitted-warning behavior are unexercised. |
| C, #260 item 1, speed 20 then speed 1 sales | PASS | 10:30–12:30 adds 21 paid $5 receipts, matching native purchases and paid shop_log units, revenue and stock. Raw log purchase counter also includes free bags, detailed below. |
| C, #260 item 2, Living off/on | PASS | Off adds 21 paid receipts; on adds 29. Exactly 2 transition lines, active runtime, original option restored. |
| C, #260 item 3, reach / outcomes | PASS | Exactly 2 reach lines for the two clock-rate changes; 5.85 / ~117 m readbacks. Missed dominates at 10:30; paid native outcome present by 12:30. |

### A — ordinary-save refusal

Fresh headless process; #70 suppression true. `call new_game {"name":"RIGTMP-PD7-14f","throwaway":true}` succeeded, load.status done. The throwaway city was never saved or removed. Exact console calls and outputs, including the confirmed repeat after load settled:

```text
call console {"line":"pc gc population"}
Warning: [Persistent City] gc population refused: measurement mode needs a disposable FT city.
call console {"line":"pc gc population 3000"}
Warning: [Persistent City] gc population refused: measurement mode needs a disposable FT city.
```

Matching Player.log lines (the same two lines occur in the initial attempt and the settled repeat):

```text
[Persistent City] gc population refused: measurement mode needs a disposable FT city.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Persistent City] gc population refused: measurement mode needs a disposable FT city.
[Mod:FoodTrucks] Test bridge: console 'pc gc population 3000' (1 lines).
```

No `MEASUREMENT MODE` line was emitted: neither rejected command entered that mode. Exact read-only manifest counts:

| Observation | City minute / clock | Residents | Visitors | Persons | Fill created |
| --- | --- | --- | --- | --- | --- |
| Before settled refusal | 1922.044656 / 08:02 | 1000 | 1000 | 2000 | 0 |
| Immediately after | 1922.155444 / 08:02 | 1000 | 1000 | 2000 | 0 |
| After one hour | 1990.115011 / 09:10 | 1000 | 1000 | 2000 | 0 |

The early census computation first reported persisted_persons=1000 while stations were pending, then a planned total of 3000 after station discovery. Those are census targets, not the live count; the `pc population manifest` replies above measure the actual persons. Each is retained in the raw evidence. No time mutation was sent to the non-FT city; the hour elapsed naturally.

### B — footprint subjects and load tally

Cold windowed rig-test process, `FT GR5 13f Timing v1`, the 14a/14b cap fixture, paused after load. Exact operation `op building {"action":"list"}` succeeded: residential=585, rentable=585, unrentable=0, pc_home_candidate=557, rotated=3. Three individual `read` calls also succeeded; these are tooling observations, not eligible check-4 evidence:

| Place / address | Yaw | Interior area m² | PC candidate | Eligibility |
| --- | --- | --- | --- | --- |
| `address:ba%3Astreet_broadwaystreet:2:home` / 2 Broadway | 0° | 204 | true | rentable, excluded |
| `address:ba%3Astreet_firststreet:22:home` / 22 1st Street | 268.953° | 204 | true | rentable, excluded |
| `address:ba%3Astreet_hovgaardlane:1:home` / 1 Hovgaard Lane | 315° | 8496 | false | rentable, blocked Hamptons villa, excluded |

`storeys=null`, `storeys_error=ops_building_storeys_not_native` is the documented readback, not an op failure. No floor count or visible ground area is guessed. There is no eligible rotated or yard/sidewalk subject to compare with footprint × floors × 0.6. No screenshots were needed or claimed. The existing fixture was extracted read-only to raw evidence; it was not saved or overwritten.

Exact post-load Player.log:

```text
[Persistent City] unrentable homes housed=0 beds=0 unreadable=0 empty=0
```

The previous `ops_game_api_unavailable` failure did not recur. No missing rig capability blocked the list/read calls; the remaining check-4 gap is the requested fixture's lack of unrentable subjects.

### C — speed-1 sales and Living transitions

Cold headless rig-test process; fixture `FT RLO 13d Food v1` loaded at 08:00; `shop_log start` filtered to `ba:street_fourthavenue#19`, capacity 10000. Ready=true, croissant $5, stock 100 and free bags 300, revenue $0. Speed 20 was used only for the 08:00–10:30 positioning advance. An explicit unpause at 08:34 cleared run_until; it was re-armed at 09:22 for the same 10:30 target, without changing speed. It arrived paused at 10:30.01. Both sales windows used `time {"speed":1,"pause":false,"keep_needs":true}`, ordinary clock polling and a pause at their endpoint. No sales window used run_until or speed 20.

| Readback | Living | Paid receipt total | New receipts | Native purchase total | Paid shop_log units | Raw purchase lines | Revenue | Croissants / bags |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 10:30 | on | 0 | 0 | 0 | 0 | 0 | $0 | 100 / 300 |
| 12:30 | off | 21 | 21 | 21 | 21 | 42 | $105 | 79 / 279 |
| 14:30 | on | 50 | 29 | 50 | 50 | 100 | $250 | 50 / 250 |

All receipt keys match the exact 138-character paid receipt value for the observed BusinessId and croissant. Its UTF-8 SHA-256 is `ca326c1e66a05d6ebcc0df039620e1dd3892881a12db58244853f8ff960bb517`. `pcstate keys` exposes hashes and lengths; comparison with the expected `paid|500|<escaped business>|ba%3Aitemname_croissant` proves their status and price without a save. No pending or conflicting receipt hash appeared. BusinessId: `business:ba%3Astreet_fourthavenue:19:1:ba%3Abusinesstype_coffeeshop:player:generation:1`.

Every paid croissant has one native visit, one paid product line and one $0 paper-bag line. Thus raw shop_log `purchases` is twice the paid receipt count; paid product lines and distinct orders equal receipts. All log snapshots have dropped=0, lost=false, hook_faults=0. Shop readiness and FoodDiagnostics.active remain true. Final accounting_stop_present=false. Revenue changes are $5 per new receipt; both croissants and bags decrease by that count. No probe fabricated a sale.

Original living_population: stored=null, live=true, original_saved=false. Off setter: stored=0/live=false; on setter: stored=1/live=true; restore and independent read: stored=null/live=true/original_saved=false. No RLO file changed. Exact Player.log speed-change/toggle excerpts, in process order (line numbers refer to C-final.log):

```text
2421: [Mod:PersistentCity] Food reach at clock rate 6.667 game minutes per second: walking covers 0.3 m per game minute, so a 60-minute lunch break with 20 minutes eating and 1 minute service reaches food shops within 6 m one way.
2792: [Mod:PersistentCity] RLO Living population on -> Living population off during play; Persistent City keeps owning its shops, crowds and traffic.
2831: [Mod:PersistentCity] Food reach at clock rate 0.333 game minutes per second: walking covers 6.0 m per game minute, so a 60-minute lunch break with 20 minutes eating and 1 minute service reaches food shops within 117 m one way.
3500: [Mod:PersistentCity] RLO Living population off -> Living population on during play; Persistent City keeps owning its shops, crowds and traffic.
```

FoodDiagnostics readbacks:

| Clock | Clock rate | Walking m/game minute | Lunch reach m | Top settled outcome / paid outcome |
| --- | --- | --- | --- | --- |
| 10:30 | 6.66666667 | 0.3 | 5.85 | 1208 × `Missed: No suitable stop in the meal window`; paid=0 |
| 12:30 | 0.333333343 | 5.99999983 | 116.999997 | 1191 × `Declined: Prefers the paid household food basket to the observed purchase options; no shop spend or stock consumed; household food basket is funded`; paid=21 |
| 14:30 | 0.333333343 | 5.99999983 | 116.999997 | 1436 × `Declined: Prefers the paid household food basket to the observed purchase options; no shop spend or stock consumed; household food basket is funded`; paid=50 |

The exact paid outcome reason in this runtime is `Paid: Native product, bag and completed order confirmed; daily settlement remains native`, rather than the shorter `Paid: Native purchase` wording in #260. It is backed by the matching paid receipts and native orders above. At 10:30 the miss count is 1208, exceeding the 1064 household-basket declines. The speed-1 reach also reads 116.999996731 m in the mid-window read.

### Runtime / logged spikes

Persistent City runtime stop: none. No TravelTiming.Apply stop, activation refusal or op failure. An unrelated mod logged this stop verbatim:

```text
[Bakery] HelpSystem was not created within 120 seconds; Help patch stopped to avoid permanent background polling.
```

No performance sampler, GC window or save profiler was armed. The following existing PC log warnings and observation maxima are >=30 ms; these are component observations, not a whole-mod p95 or an exhaustive per-frame export. Repeated maxima can refer to the same warning. Whole-frame timing was not measured. Full source lines remain in logged-spikes.json outside Git.

| Log:line | Kind / clock | Observed ms |
| --- | --- | --- |
| A-final.log:2564 | routing warning | preparation_ms=132.826, closure_ms=0.0 |
| A-final.log:2637 | routing warning | preparation_ms=1.202, closure_ms=47.742 |
| A-final.log:2642 | observation maximum / 1920.0 | advance=146.405, closure_refresh=47.742, preparation_frame=132.826 |
| A-final.log:2813 | observation maximum / 1926.3 | advance=140.178 |
| B-final.log:1702 | routing warning | preparation_ms=132.976, closure_ms=0.0 |
| B-final.log:1726 | observation maximum / 3780.0 | advance=147.673, preparation_frame=132.976 |
| C-final.log:2552 | routing warning | preparation_ms=127.809, closure_ms=0.0 |
| C-final.log:2576 | observation maximum / 1920.0 | advance=144.229, preparation_frame=127.809 |
| C-final.log:2728 | observation maximum / 2009.4 | advance=135.177 |
| C-final.log:2841 | routing warning | preparation_ms=0.256, closure_ms=162.199 |
| C-final.log:2882 | observation maximum / 2074.0 | closure_refresh=162.199 |
| C-final.log:3059 | observation maximum / 2104.0 | advance=141.141 |
| C-final.log:3131 | observation maximum / 2118.4 | advance=128.399 |
| C-final.log:3216 | observation maximum / 2135.9 | advance=131.71 |
| C-final.log:3290 | observation maximum / 2154.1 | advance=129.631 |
| C-final.log:3381 | observation maximum / 2171.1 | advance=130.721 |
| C-final.log:3463 | observation maximum / 2190.0 | advance=130.443 |
| C-final.log:3578 | observation maximum / 2207.8 | advance=132.387 |
| C-final.log:3673 | observation maximum / 2225.4 | advance=138.127 |
| C-final.log:3764 | observation maximum / 2244.9 | advance=132.295 |
| C-final.log:3851 | observation maximum / 2262.7 | advance=135.229 |
| C-final.log:3915 | observation maximum / 2278.5 | advance=136.171 |
| C-final.log:3994 | observation maximum / 2293.8 | advance=321.675 |

### NOT RUN groups / tooling gaps

1. **Required #246 item 4:** fixture gap, all 585 residential buildings expose a rentable interior; no eligible three-home comparison. Building list/read now work.
2. **GR-5 GC/street re-checks:** excluded; spawn_appearance fix still in progress at run authorization; recheck follows its merge. No performance window in this run.
3. **#254 item 4 appearance:** excluded; resident identity through reacquire is a Rig bot gap.
4. **GR-5 60 fps arms:** excluded; Mac reaches about 5 fps in prior runs, Windows CLI lacks --rig-test, both Rig bot gaps. Neither arm measured here; Windows untouched.
5. **GR-5 save latency:** excluded; owner decision pending. No save profiling.

Exact in-game op failures in this run: none. The printed shell rollback was rejected before execution with `rm -f style commands are not permitted. Use a safer approach`; the verified rename/copy restoration completed cleanup without that removal command. Building read returns the documented `ops_building_storeys_not_native` field; visible-storey comparison remains unavailable because there are no eligible subjects. The zero tally is partial evidence, not proof of nonzero empty/unreadable classification. No issue was opened and no bot was contacted.

### Fixtures and cleanup

Created unsaved throwaway: `RIGTMP-PD7-14f`. Reused only `FT GR5 13f Timing v1` and `FT RLO 13d Food v1`. No new fixture save/checkpoint, overwrite or removal. Existing disposable fixture saves stay in the isolated profile.

Cleanup: three graceful stops; no --force. The safer equivalent restore returned the whole PC folder and install.json; the candidate folder was retained outside the profile. Isolated preference cache was restored from this run's own backup, then original bytes copied back. All 38 manifest entries match=true, exact PC file set matches=true, all 53 RLO files unchanged=true. Frame-cap and absent-config entries restored. living_population restored through its op before stop. Original autosave readback at cleanup remained 5 minutes / 3 saves, prevent_autosave=false; guard confirms no allowed save. Lease released; final status running=false, held=false, no process started by this run remains. Agent-rig and original PC checkout clean.

`[Mod:PersistentCity] Persistent population active: 1000 residents, 1000 visitors, 1686 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.`

`[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.`

`[Mod:PersistentCity] Persistent population active: 1611 residents, 1611 visitors, 1685 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.`

Final origin/codex/persistent-city: `7ff6a87acf75e35a3a56d41abd842a43980c19d7`; candidate binary remains the SHA recorded above.

The evidence branch was fast-forwarded to the final tip after testing to preserve the intervening checklist entries. #262 (the appearance fix) merged during the run; its production code was not built or tested here. GC/street re-checks remain excluded.

Offline verification: candidate build passed; docs-links, markdown-ban and git diff --check recorded in the PR. No test source changed, no focused classes, no full suite. Raw evidence filename/hash manifest: `raw-evidence-manifest.json` in the stage. Build/evidence worktree removed after this evidence commit and PR creation.

Refs #244
Refs #246
Refs #260

## Rig results — 14 November 2026 g, Mac (GR-5 GC recheck after #262 at 7ff6a87a)

**RESULT: DONE. Counts: 2 passed / 2 partial / 1 failed / 4 NOT RUN groups.** The five #262 checks count once each; the four excluded groups below are additional NOT RUN items. Day/night pass #262's allocation and collection-count expectations. The street <5 ms gate fails. Neither visual check is a full pass. This does not establish the overall GR-5 GC/street gates: sampler pause p95 regressed. In-game proof gates no merge.

Role `rig_test`, where `mac`, isolated `agentmac`, owner `persistent-city-rig-2026-11-14g-mac`. Raw evidence `<stage>` = `<home>/HART-Builds/persistent-city-rig-2026-11-14g-mac`. Report copy: `<home>/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-14g-mac.md`. [Complete GC reports and compact window/visual evidence](perf/gr5-2026-11-14g-mac.json): **17,069 bytes**, SHA-256 `0244af1a5674dca5850cd85c41cf781cc922f8487d5ef340f17802725b91a8cf`. Every GC allocation key, sample/skipped count, counter_frame_bytes and human report is retained. Larger records and screenshot hashes are in `<stage>/raw-evidence-manifest.json`, SHA-256 `2c9fa0a5a2ee8928e7c32c6aa23657955f7cee36310ff071a23dab4af0a04643`.

All performance observations are provisional on the shared Mac. Every command and measurement ran in the foreground and was awaited. No Windows contact, other-session intervention, Steam/ordinary profile or ordinary save access, production change, RLO file change, schema/weight/version/changelog change, issue creation, release, Workshop upload or merge. Schema stays 1; FoodChoicePriceWeight 1.2 and FoodChoiceHouseholdWeight 1.0.

### Build and rig

| Item | Evidence |
| --- | --- |
| Tested candidate | `7ff6a87acf75e35a3a56d41abd842a43980c19d7`, exactly the initial origin/codex/persistent-city tip and requested #262 merge. Final fetch moved to `844ce2b95b23a53431ce18de26cf09fe44966159` (#263, 14f docs only). The results branch was fast-forwarded to that docs-only tip before appending this section; tested production code is unchanged. |
| Branch / worktree | New `codex/mac-rig-2026-11-14g`, `rig-2026-11-14g-mac-evidence`, serving both build and evidence. Original checkout stayed clean. |
| Build | `python3 tools/build.py --game-managed "<home>/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0; Homebrew PATH and DOTNET_ROOT. |
| PersistentCity DLL SHA-256 | `a0a1ef89db96420ce375100a09f6236c13d47b10f3856f9c1963079b0613341b`; built, package and live staged DLL matched; both starts validated the receipt. |
| Official agent-rig HEAD | `022630f7d6df8c533a230ac218413a68b181ed95`, clean before/after. Fetch/ff-only pull already current. No edits, commit or push. |
| Live AgentRigOps SHA-256 | `154be3036954c314d3bd9eb4409da98f6a9b3a508748f2b1811c457abce07955`, matched live ready health and cleanup. |
| RLO version, exact Player.log | `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` All 53 package files unchanged. |
| PC activation, exact Player.log | `[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.` No commit-stamped build line is emitted; the matching staged hash and validated receipt establish binary identity. |
| Lease | 14f held the rig at first status. One foreground loop checked every 120 s, without takeover, from 16:09:36 to 16:19:37 UTC. Acquired `2026-11-14T16:19:37.590259Z` after running=false/held=false. No build or stage before ownership. |
| Resources / starts | Pre-stage 67.7 GiB free disk / 74% reported free memory; before B 64.7 GiB / 79%. A: one successful `start --mode headless --rig-test`, PID 47738. B: one successful cold `start --mode windowed --rig-test`, PID 72372. Two graceful stops, no force; no refusal or resource override. |
| Autosave original / suppression | Native 5 minutes / 3 saves, prevent_autosave=false. #70 `autosave` suppressed=true after every completed load and before/after every measured window. All four perf guards clean=true, zero allowed saves, zero native-saving frames. No manual save, checkpoint or save-latency measurement. |
| Config / options | SaveCostDiagnostics=false throughout; readback requested=false, save=null, activation=null. Native save profiler installed=false. Temporary B frame cap fps=30/vsync=0 applied, original absence restored. living_population unchanged: stored=null, live=true, original_saved=false before/after. No RLO toggle was performed. |

Exact staging command from official agent-rig, stopped and leased:

```sh
python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package
```

Receipt backup: `<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T162033Z`. Package contains no RLO files. Printed rollback, path-sanitized (`<home>` replaces the user home directory):

```sh
rm -rf "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T162033Z/mod" "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T162033Z/install.json" "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

### Per-check outcomes

| #262 check | Result | Evidence against the PR expectation |
| --- | --- | --- |
| 1. Day, 600 s at 15:30 | PASS | Appearance **62.155 MB / 1,203 samples**, 11.99x below 14b's 745.426 MB / 1,281, better than the projected 90–130 MB; **7 gen0 < 11**. Pause p95 **67.048 ms**, regressed from 28.191; physical+advance **309.570 MB**, from 1,021.6. |
| 2. Night, 600 s at 01:00 | PASS | Appearance **26.120 MB / 492 samples**, 12.55x below 327.776 MB / 561; **6 gen0 < 8**. Pause p95 **141.801 ms**, regressed from 61.316; physical+advance **134.021 MB**, from 433.6. |
| 3. Street, 600 s | FAIL | Qualified window: UpdateCity p95 **5.2988 ms**, mod-work p95 **5.3683 ms**, both exceed strict <5 ms. One spike >=30 ms, **64.7872 ms**, frame 56448, updatecity. 14b: 5.5170/5.5824 ms, four spikes, max 78.2031. |
| 4. Looks, release/reacquire and pan | PARTIAL / resident identity tooling gap | Three native walker handles attempted; two follow calls succeeded, third stale. No stable PC resident identity or verified release/reacquire comparison. Pan completed **60.759 s** with five screenshots; visible dressed walkers showed no evident undressed, distorted or T-posed body in inspected images. Both required blendshape error searches returned 0. Stills do not prove the absence of a brief flash or invisible body. |
| 5. Deferred burst / refill | PARTIAL / fixture density and comparison gaps | Five-position rapid camera sweep completed. Fresh 7th Street address: at most 3 nearby walkers over 38.645 s. Busy-street return after speed-mode pool clearing: 2 at 0.631 s, >=12 by 6.754 s, >=24 by 12.636 s, >=32 by 30.620 s, 35 at 42.619 s. Neither arm reached 48–96, so the 12–24 s full-density expectation is unverified. No wrong-look flash evident in inspected stills; no continuous flash proof. 14b's candidate was not restarted for a paired refill measurement; its 3–6 s figure remains the PR expectation. |

### GC windows and residual decision

Fixture: `FT GR5 13f Timing v1`, the 14a/14b fallback, 1,000 residents / 2,000 visitors / 1,683 places, person target off, street player x=215,z=0, speed 1. `FT RLO 13d Food v1` exists but again loaded 1,611/1,611, so was rejected before sampling. Day began Tuesday 15:30, night Wednesday 01:00. GC samplers were stopped outside their windows; mod-work timing was unarmed for both GC windows. The 600 s waits are bounded by the start/stop console replies; native guard unions were 601.466/600.556 s. Every allocation counter is `boehm_total`; legacy frame_alloc_bytes remains separately labelled `heap_used`. MB below means decimal MB.

| Window | Stored / dropped frames | gen0/1/2 | Pause p50 / p95 / max ms | Mono heap bytes | Counter frames / counter_frame_bytes | Native whole-frame p95 / max ms |
| --- | --- | --- | --- | --- | --- | --- |
| day | 16469 / 0 | 7/0/0 | 37.458/67.048/67.048 | 1629356032 | 16469 / 1740692800 | 49.0074/861.6949 |
| night | 16950 / 0 | 6/0/0 | 42.231/141.801/141.801 | 1894055936 | 16950 / 1611097296 | 41.8950/1330.2122 |

Every allocation field and sample/skipped count is in the linked full GC JSON. The segment table gives bytes / samples / skipped; child spans overlap their parents. `physical_spawn_randomize` and `physical_spawn_bake` are inside `physical_spawn_appearance` and are not added to physical or to the totals.

| Allocation segment | Day bytes / samples / skipped | Night bytes / samples / skipped |
| --- | --- | --- |
| mod | 431063872 / 16470 / 0 | 254285376 / 16951 / 0 |
| advance | 209050960 / 16373 / 0 | 71735360 / 16861 / 0 |
| physical | 100518648 / 16373 / 0 | 62285312 / 16861 / 0 |
| physical | 100518648 / 16373 / 0 | 62285312 / 16861 / 0 |
| physical_ambient | 3341808 / 16373 / 0 | 3239296 / 16861 / 0 |
| physical_vehicles | 1087184 / 16373 / 0 | 399840 / 16861 / 0 |
| physical_bindings | 15984 / 583598 / 0 | 11424 / 224962 / 0 |
| physical_vendor_meals | 0 / 16373 / 0 | 144 / 16861 / 0 |
| physical_fade | 0 / 16373 / 0 | 0 / 16861 / 0 |
| physical_focus | 0 / 16373 / 0 | 192 / 16861 / 0 |
| physical_observe | 274160 / 566823 / 0 | 104000 / 207918 / 0 |
| physical_release | 15008 / 402 / 0 | 13808 / 183 / 0 |
| physical_parked | 1961392 / 583 / 0 | 1709504 / 586 / 0 |
| physical_index | 80 / 2179 / 0 | 80 / 2195 / 0 |
| physical_queues | 8960 / 2179 / 0 | 17392 / 2195 / 0 |
| physical_roster | 287328 / 2179 / 0 | 191312 / 2195 / 0 |
| physical_selection | 1353912 / 2680 / 0 | 691288 / 2395 / 0 |
| physical_spawn_pedestrian | 29837120 / 824 / 0 | 29701224 / 331 / 0 |
| physical_spawn_appearance | 62154768 / 1203 / 0 | 26120448 / 492 / 0 |
| physical_spawn_check | 0 / 802 / 0 | 0 / 328 / 0 |
| physical_spawn_vehicle | 180944 / 78 / 0 | 85360 / 33 / 0 |
| physical_unattributed | 0 / 32746 / 0 | 0 / 33722 / 0 |
| physical_spawn_randomize | 3555456 / 401 / 0 | 1490912 / 164 / 0 |
| physical_spawn_bake | 1849408 / 401 / 0 | 1633872 / 164 / 0 |

Appearance residual excluding randomize and bake: **56,749,904 / 22,995,664 bytes** day/night. Bake plus that remainder is **58,599,312 / 24,629,536 bytes**, **7.86% / 7.51%** of 14b appearance. Appearance as a whole is **8.34% / 7.97%** of 14b. Thus **neither reading exceeds about 10%**; #262's proposed combiner replacement trigger is not met by these measurements. Randomize/bake are not double-counted.

Largest measured allocation parent is now **advance**: 209,050,960 / 71,735,360 bytes. Largest physical child is **spawn_appearance** by day (62,154,768); **spawn_pedestrian** by night (29,701,224, versus 26,120,448 appearance). Appearance's uninstrumented remainder exceeds its separately measured randomize and bake children. No code root cause for the pause regression is inferred. The total process-wide counter stays much larger than PC's measured spans.

| Window | Last periodic pedestrianLooks built / reused / deferred | Pool hit rate reused/(built+reused) | Exact builds within GC span |
| --- | --- | --- | --- |
| Day | 340 / 22 / 1,034, 0 idle / 96 owned | 6.08% | 401, from 1,203 appearance samples / 3; randomize and bake each 401 samples |
| Night | 165 / 2 / 1,118, 96 idle / 96 owned | 1.20% | 164, from 492 appearance samples / 3; randomize and bake each 164 samples |

Look counts and hit rates above are the last periodic log snapshots, not exact window deltas; the rig exposes no end-of-window counter projection. They are kept distinct from the allocation sampler's exact build counts. The latest snapshot need not coincide with `pc gc stop`.

### Street windows and every mod-work spike >=30 ms

The first street window used the documented inner-component profile (advance, route preparation, physical population, inspector) but omitted a separate UpdateCity timer. Its complete mod-work evidence is retained as diagnostic, not used to claim the required UpdateCity result. After discovering the omission, the untimed advance toward next afternoon was paused at Wednesday 20:18 and the unchanged fixture reloaded in the same A process; no measurement was open then. The qualified window reloaded the saved fixture state, positioned Tuesday 15:30 and timed UpdateCity, matching 14b's component boundary. Nine pans succeeded in each street window. No spike was chased.

| Window | Seconds / frames / achieved fps | UpdateCity p95 / max ms | Mod-work p95 / max ms / spikes >=30 | Whole-frame p95 / max ms / spikes >=30 |
| --- | --- | --- | --- | --- |
| initial diagnostic | 600.332/4429/7.38 | not separately timed | 6.1662/196.9031/19 | 226.6872/1192.4037/4278 |
| qualified | 600.399/3485/5.80 | 5.2988/64.7304 | 5.3683/64.7872/1 | 200.5798/506.5189/3485 |
| 14b reference | 600.284/3370/5.61 | 5.5170/78.1533 | 5.5824/78.2031/4 | 238.9091/1002.2687/3370 |

Both records have coverage.complete=true, validity.valid=true, zero dropped frames and zero off-main-thread/cross-frame/unbalanced/depth-overflow faults. Whole-frame timing is separate from mod-work. The component profile and day differ between the diagnostic and qualified windows, so their difference is not an optimization claim.

| Window / Unity frame | Mod-work ms | Dominant component / group | Whole-frame ms in row |
| --- | --- | --- | --- |
| diagnostic / 48744 | 196.9031 | PopulationRuntime.Update / frame_update | 227.1576 |
| diagnostic / 50687 | 42.8869 | physical_population / physical_population | 224.2295 |
| diagnostic / 50775 | 41.6359 | physical_population / physical_population | 481.2128 |
| diagnostic / 50784 | 94.1541 | PopulationRuntime.Update / frame_update | 589.6928 |
| diagnostic / 50816 | 36.4798 | PopulationRuntime.Update / frame_update | 228.5977 |
| diagnostic / 50933 | 32.1965 | advancement / advancement | 347.1219 |
| diagnostic / 50939 | 38.3795 | physical_population / physical_population | 244.2708 |
| diagnostic / 51385 | 45.1151 | physical_population / physical_population | 220.1075 |
| diagnostic / 51414 | 68.4536 | physical_population / physical_population | 491.4984 |
| diagnostic / 51439 | 49.7060 | physical_population / physical_population | 781.2728 |
| diagnostic / 51444 | 44.1786 | PopulationRuntime.Update / frame_update | 265.4448 |
| diagnostic / 51535 | 40.5315 | physical_population / physical_population | 231.5308 |
| diagnostic / 51545 | 35.3069 | physical_population / physical_population | 228.2260 |
| diagnostic / 51560 | 37.0766 | physical_population / physical_population | 660.9367 |
| diagnostic / 51561 | 39.0091 | physical_population / physical_population | 266.2937 |
| diagnostic / 51569 | 47.0807 | physical_population / physical_population | 222.7891 |
| diagnostic / 51587 | 31.8290 | physical_population / physical_population | 223.1465 |
| diagnostic / 51589 | 31.9059 | physical_population / physical_population | 468.6219 |
| diagnostic / 51592 | 30.2596 | physical_population / physical_population | 593.9217 |
| qualified / 56448 | 64.7872 | updatecity / updatecity | 221.6390 |

All 20 exported mod-work spikes are listed, including the extra diagnostic. The qualified spike's UpdateCity contribution is 64.7304 ms. Qualified mod-work p95 is 0.2141 ms (3.8%) below 14b and UpdateCity 0.2182 ms (4.0%) below, but both still fail <5 ms. Shared-host variation prevents attributing those small differences to the fix alone.

### Windowed visual evidence and exact op failures

A cold windowed rig-test process used the same 1,000/2,000 fixture. Two before images show dressed moving walkers (red top and green top); the third handle was stale. Return images include occluded/empty views and do not establish the same resident's clothes, skin or body shape. The rig documentation explicitly says walker handles can be reused without a persistent resident identity guarantee. Exact resident-identity attempts:

- `op screen_pos {"action":"get","target":{"kind":"walker","id":"walker-a066d42e3d714e15a31af9e25ed74fee"}}`: returned a current native position, with no PC resident identity guarantee.
- `op screen_pos {"action":"get","target":{"kind":"walker","id":"walker-e88571aea7fd48a0b2179fdd1f4bf003"}}`: returned a current native position, with no PC resident identity guarantee.
- `op screen_pos {"action":"get","target":{"kind":"walker","id":"walker-a4e5250f45f5449db478eff8b3c0aead"}}`: `ops_view_stale_target`. The preceding `camera follow` on this same target also returned `ops_view_stale_target`.

The 60.759 s pan retained `look-pan-shot-0/3/6/9/11.png`. Refill screenshots and active-walker counts are retained for both arms; some views are occluded by buildings, limiting visual conclusions. No undressed/distorted/T-posed body or wrong-look flash is evident in inspected images, but still captures are not a continuous burst record. Both Player.log strings `Merged body already has blendshapes` and `Merged body vertex layout differs` have **0 matches**. No Persistent City runtime-stop line was observed.

Fresh-street setup: `screen_pos get` for business `ba:street_seventhstreet#2` returned x=-140.1295,z=372.905426, more than 500 m from the original focus. `call teleport {"who":"player","place":"address:ba:street_seventhstreet#2"}` succeeded; only 0–3 nearby walkers were observed through 38.645 s. A return to x=215,z=0 after the 17:30 speed-mode advance clears the body pool and supplies the second observation. It is a return to an already visited street, not a paired unseen-street benchmark. The observed 35-walker maximum does not establish demand for 48–96 walkers. Full-density refill time and a fresh paired 14b comparison are NOT RUN within this partial check; no weaker pass threshold is used.

Other exact errors and recovery:

| Command | Exact error / action |
| --- | --- |
| `call saves {}` | `Unknown command 'saves'. Commands: state, console, debug, time, money, teleport, walk, drive, vehicle, commissary, crew, route, price, use, taxi, phone, call, eat, floorplan, place, drop, pickup, handtruck, fixture, logistics, parks, day_summary, places, map, ui, shoot, save, load, new_game, quit, op.` Preparatory enumeration unavailable; documented FT fixtures loaded directly. |
| `call time {"run_until":{"time":"15:30"},"speed":20,"keep_needs":true}` when already at the target | `day 2 15:30 has passed already: it is day 2 15:30.` No window opened; current-time positioning was skipped. |
| `call time {"pause":true}` during same-name reload dispatch | `time works in the city only; the game is at the loading now.` Reload had briefly reported the previous done snapshot; waited for current load to finish, then confirmed autosave and paused. No load command replay. |
| `call teleport {"who":"player","x":500,"z":0}` for look departure and initial refill | `No ground found at x 500.0, z 0.0.` Both refusals retained. Used a native business entrance for the subsequent valid trip. |

### Player.log excerpts for clock changes

The untimed speed-20 positioning and reached/paused-speed-1 lines are retained below. Explicit speed-1 measurement calls also read back speed=1, paused=false in the raw replies. RLO was never toggled.

```text
A [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 15:30.
A [Mod:FoodTrucks] Test bridge: run_until day 2 15:30 reached at day 2 15:30; paused, speed 1.
A [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 01:00.
A [Mod:FoodTrucks] Test bridge: run_until day 3 01:00 reached at day 3 01:00; paused, speed 1.
A [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 15:30.
A [Mod:FoodTrucks] Test bridge: run_until day 3 15:30 reached at day 3 15:30; paused, speed 1.
A [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 4 15:30.
A [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 15:30.
A [Mod:FoodTrucks] Test bridge: run_until day 2 15:30 reached at day 2 15:30; paused, speed 1.
B [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 15:30.
B [Mod:FoodTrucks] Test bridge: run_until day 2 15:30 reached at day 2 15:30; paused, speed 1.
B [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 17:30.
B [Mod:FoodTrucks] Test bridge: run_until day 2 17:30 reached at day 2 17:30; paused, speed 1.
```

The A `day 4 15:30` advance has no reached line because it was explicitly paused before the corrective reload. No sample was running during this setup. No runtime-stop/error line is hidden by that interruption.

### NOT RUN groups, fixtures and cleanup

1. **GR-5 street 60 fps:** excluded; Mac prior runs reach about 5 fps at 1,000 residents. Windows scoped staging remains missing (agent-rig #87); Windows was not contacted.
2. **GR-5 busy-shop 60 fps:** same cadence/staging exclusions. No shop performance arm in this request.
3. **GR-5 save latency:** excluded; owner decision pending. SaveCostDiagnostics stayed off, no profiler or explicit save.
4. **All other acceptance checks:** outside this run's A/B scope, including P-D refusal, footprint/tally, native sales and RLO toggle arms. The newly merged 14f report was read after final fetch, not rerun here.

Sub-arms not established within partial checks are not counted again: stable PC resident identity across release/reacquire, full-density 48–96 refill time, continuous wrong-look flash proof and paired 14b refill. These are proof/tooling or fixture limits, not passing results.

Reused `FT RLO 13d Food v1` for population validation only, and `FT GR5 13f Timing v1` for all measurements/visual checks. No new save, checkpoint or throwaway city; existing fixtures were not overwritten or removed.

Cleanup, stopped and leased: renamed the staged candidate into `<stage>/staged-candidate-restored-out`, copied the full receipt backup mod folder into the empty path, then restored install.json unchanged. Original isolated root preferences/config, PlayerSettings, plist, persistent-city.json and frame-cap absence were restored from this run's independent backup. All **35 original file hashes / 4 absent paths / exact PersistentCity file set / 53 RLO file hashes match; zero mismatches**. install.json SHA-256 `c57df8e5b67cc581e5006705e028f7dab2898681545ce96d4da1a57854e52529`. AgentRigOps hash unchanged. The native autosave readback remained the original 5 minutes / 3 saves; process-only suppression ended at stop. living_population was never changed. All 23 helper-generated PNGs were copied/hash-verified into `<stage>` and removed from their isolated helper output paths; raw screenshots remain outside Git.

Lease released: final running=false, process=null, held=false, pending=0. Both owned player PIDs and their caffeinate assertions are gone. Official agent-rig and original PC checkout remain clean. One worktree served build/evidence and is removed after commit, push and opening the unmerged PR.

Offline validation: candidate build passed; compact JSON parses and is under 20 KB. Docs-links passed (20 files, 0 broken relative links); markdown-ban and `git diff --check` passed for this docs-only change. No test files changed, so no focused classes; no full suite or regression suite. Only this existing checklist and the compact JSON are changed.

Refs #262

## Rig results ? 14 November 2026 h, PC (GR-5 60 fps street/busy-shop + GC vs Mac 14g at 650fc893)

**RESULT: BLOCKED. Counts: 0 passed / 0 partial / 0 failed / 9 NOT RUN.** Four required measurement arms were blocked during setup; five excluded groups are counted separately below. No 600 s window began. The Windows baseline, allocation/GC comparison and strict UpdateCity / mod-work p95 <5 ms gates remain unverified. In-game proof gates no merge.

Role `rig_test`, where `pc`, owner `pc-rig-2026-11-14h`, isolated `agent1`. All observed cadence values below are **PC 60 fps windowed, provisional (PC shared)**. A configured cap is verified; sustained 60 fps is not. The Mac was not contacted, started or used. The seventh-fix branch and other sessions were untouched.

[Compact evidence](perf/gr5-2026-11-14h-pc.json) contains explicit null measurement fields, cap/guard readbacks, machine-load snapshot, Mac reference values and restoration results. `<stage>` is this run's local staging folder and `<home>` the rig user's home folder. Raw evidence and this run's backups: `<stage>`. The raw manifest records file sizes and SHA-256 values; no logs, binaries, credentials or saves are committed. Report copy: `<home>\Coding_Projects\.agent-logs\persistent-city-rig-2026-11-14h-pc.md`.

### Build and rig

| Item | Evidence |
| --- | --- |
| Candidate | `650fc893d0119921e2b37576ac42b115d20e5ed8`; initial and final fetch agree. Same production code as Mac 14g's `7ff6a87a`; #264 added only results. Seventh fix absent. |
| Worktrees | New results branch `codex/pc-rig-2026-11-14h`, evidence `rig-2026-11-14h-pc-evidence`; separate detached build `rig-2026-11-14h-pc`. Original checkout unchanged. |
| Official agent-rig | `e77831929bd4d347ba064e02f485c056d523107e`; clean before/after, fetch and ff-only pull already current. No edits, commits, push or overlay update. |
| AgentRigOps SHA-256 | `1e9c612a3a96fa1f967553ddc0af466dd9e2819f59692cdf5a7766228a85d673`; live hash, ready health and cleanup agree. |
| Build | Windows Steam-Managed build: `python tools/build.py --game-managed "C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"`, exit 0. PATH prefixed with `%LOCALAPPDATA%\Microsoft\dotnet`; required wait helper printed `dotnet lock removed 2026-11-10; continuing`. No offline dotnet suite. |
| PersistentCity DLL SHA-256 | `a0a1ef89db96420ce375100a09f6236c13d47b10f3856f9c1963079b0613341b`, identical to Mac 14g. Built package copied into this run's stage; receipt-validated start accepted it. No RLO/game DLL packaged. |
| Staging / rollback | Official #87 scoped stage succeeded. Backup `C:\BigAmbitions-AgentTests\receipt-backups\20261114T183607Z`; 1 added, 41 existing package receipt paths rehashed, 42 pinned entries. What-if preceded the real stage. Other mod receipt entries left unchanged. |
| Start / stop | One `python scripts\agent start --mode windowed --rig-test`, successful first attempt, isolated PID 4596. Official `status` and `health` succeeded locally. One graceful `scripts\agent stop`; no force, no other process stopped. |
| Food Trucks | Health `mod_version=0.10.2`; unchanged. |
| RLO, exact log | `[Mod:RealLifeOverhaul] Real Life Overhaul 0.45.0 initialised.` Mac 14g used 1.0.6. This run did not install/update/edit RLO. PC activated; no RLO incompatibility is inferred. All 48 original RLO file hashes match at cleanup. |
| PC activation, exact log | `[Mod:PersistentCity] Persistent population active: 957 residents, 957 visitors, 1081 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.` No commit-stamped build line is emitted; deterministic DLL identity and successful receipt validation establish the build. |
| Autosave / SaveCost | Original native autosave 5 minutes / 3 saves, prevent_autosave=false. Rig-test guard enabled=true, autosave_disabled=true, save_specific=false, allow=[], error=null. SaveCostDiagnostics requested=false, save=null, activation=null; native profiler installed=false. Read before stop: no save attempts/allowed/refused/native save activity, no perf window active. No save or save-latency probe. |
| Lease | Acquired free at 18:34:33 UTC. Initial release followed stop/rollback; cleanup audit required reacquisition at 18:39:38 UTC under the same owner, then final release after exact verification. No takeover, other owner or pending work. |
| Machine load | Setup snapshot 18:38:24 UTC: Intel i5-13600KF, CPU 64%, total RAM 63.84 GiB, free 22.57 GiB. Notable resident processes: isolated player 5.49 GiB, PathOfExileSteam 4.93 GiB, vmmemWSL 2.85 GiB, Path of Building 1.34 GiB, browser and another CLI session. These are working sets, not attributed CPU rates. No measured window started, so no per-window load readings exist. |

Exact stage command, stopped and leased, preceded by the same command with `--what-if`:

```text
python scripts\stage_mod_windows.py --stage-mod PersistentCity <stage>\package
```

Printed rollback, executed stopped and leased:

```text
python scripts\stage_mod_windows.py --rollback "C:\BigAmbitions-AgentTests\receipt-backups\20261114T183607Z" PersistentCity
```

### Cap health and fixture

Staged exactly `{"fps":60,"vsync":0}` in the isolated profile's `agent-frame-cap.json`; original absence restored. Initial menu readback: requested=60, applied=true, vsync_count=0, target_frame_rate=60, measured_fps=56.46, window_s=2.00, error=null. Loaded-city observations ranged 46.42?49.61 fps in retained setup snapshots. Last pre-stop readback: requested=60, applied=true, vsync_count=0, target_frame_rate=60, measured_fps=49.61, window_s=2.02, error=null. These are **PC 60 fps windowed, provisional (PC shared)** setup observations, not a 600 s measurement. The applied cap stayed present; sustained 51?66 fps qualification was not established. No cold restart was needed for cap loss.

Neither `FT RLO 13d Food v1` nor the Mac fallback `FT GR5 13f Timing v1` exists in this isolated PC SaveGames inventory. Used the authorized fallback:

```text
new_game {"name":"RIGTMP-GR5-14h","throwaway":true}
```

It completed with load.status=done, Monday 08:00, speed 1, unpaused, player x=215,z=0. Last live manifest: **957 residents / 957 visitors**, 20 portals, person-target unchanged; no `pc gc population 3000` command. Census resident_target=957, commuter_target=1914 and persisted_persons=2871 are census plans, not evidence of a live 1,000/2,000 city. Load tally: `[Persistent City] unrentable homes housed=0 beds=0 unreadable=0 empty=0`. No busy-shop fixture was created. The throwaway city was never saved; native creation left only its portrait JPEG, with no `.hsg`/city save. No undocumented deletion was attempted.

### Required arms and comparison

| Arm, in requested order | Result | PC 60 fps windowed evidence |
| --- | --- | --- |
| C day, 15:30, 600 s | NOT RUN / tooling and fixture gaps | `time` positioning refused the non-FT city; `perf begin` refused it. Required population/cadence unverified. |
| C night, 01:00, 600 s | NOT RUN / same setup gate | No valid day window or supported time positioning in this city. |
| A street, 600 s | NOT RUN / same setup gate | `modwork arm` refused the non-FT city. No UpdateCity or mod-work timing. |
| B busy-shop, 600 s | NOT RUN / same setup gate | Same city has no established busy-shop fixture; scoped mutation guard and required population/cadence prevent qualification. |

For all four arms, measured fps, whole-frame p95/p99, gen0, pause p95, heap, UpdateCity/mod-work p95 and >=30 ms spike counts/frames/components are **unavailable**, not zero. No GC sampler was started; `pc gc report` confirmed `gc measurement not started`. Consequently there is no complete GC report or allocation-segment/sample-count/pedestrianLooks/pool-hit comparison to publish. All physical_spawn_* segments, physical_unattributed and counter_frame_bytes remain unavailable. Randomize and bake remain children of appearance and must not be added to it.

Reference only, from [Mac 14g](#rig-results--14-november-2026-g-mac-gr-5-gc-recheck-after-262-at-7ff6a87a) and [its complete JSON](perf/gr5-2026-11-14g-mac.json):

| Metric | Mac 14g day / night | Mac 14b day / night | PC 60 fps windowed |
| --- | --- | --- | --- |
| gen0 | 7 / 6 | 11 / 8 | unavailable |
| Pause p95 ms | 67.048 / 141.801 | 28.191 / 61.316 | unavailable |
| Mono heap bytes | 1,629,356,032 / 1,894,055,936 | see 14b full report | unavailable |
| Appearance MB / samples | 62.155 / 1,203; 26.120 / 492 | 745.426 / 1,281; 327.776 / 561 | unavailable |
| Physical + advance MB | 309.570 / 134.021 | 1,021.6 / 433.6 | unavailable |
| Street UpdateCity / mod-work p95 ms | 5.2988 / 5.3683 | 5.5170 / 5.5824 | unavailable |
| Street >=30 ms mod-work spikes | 1, max 64.7872 ms | 4, max 78.2031 ms | unavailable |

Mac 14g GC windows were headless at about 30 Hz; this PC was windowed with a 60 fps target. They are not like-for-like frame-time comparisons. The required GC pause/gen0/heap/allocation comparison remains blocked by absent PC samples. No improvement, regression, <5 ms pass or product failure is inferred from this run.

### Exact op failures and tooling gaps

1. Day positioning, `time {"run_until":{"day":1,"time":"15:30"},"speed":20,"keep_needs":true}`: `time changes the save, and the loaded save 'RIGTMP-GR5-14h' doesn't start with FT: load an FT save first.` No time mutation occurred.
2. `perf {"action":"begin","label":"gc-day"}`: `ops_ft_save_required`. No window opened; subsequent perf read confirmed active=false.
3. `modwork {"action":"arm","components":[{"name":"updatecity","type":"PersistentCity.Runtime.PopulationRuntime","method":"UpdateCity"}]}`: `ops_ft_save_required`. Subsequent status confirmed armed=false, bound=0, no open window.
4. Preparatory `help {}` at the menu: `help works in the city only; the game is at the menu now.` Switched to documented commands and direct isolated inventory; no gameplay effect.

The #87 local lifecycle and scoped staging paths both worked; the earlier CLI/staging blockers did not recur. The remaining gap is that the authorized non-FT throwaway supports console but retains FT-only time/perf/mod-work mutation guards. No guard bypass, tooling edit, unrelated FT fallback or production fix was attempted. Fresh-world housing/population differs from the requested fixture, and RLO differs from Mac 14g. Those observations do not establish a code cause.

### Excluded NOT RUN groups

1. #246 checks 4?5, unrentable footprint/tally: Rig bot fixture still in flight, run `20261114-192649-rig-unrentable-residential`.
2. #262 checks 4?5, look stability/burst-refill: no stable resident identity through release/reacquire and no dense-walkers fixture; queue resident-reacquire=14g gap 1, dense-walkers=14g gap 2.
3. GR-5 check 2, save latency: owner save-route decision pending via Spock. No save profiling.
4. P-D 7 and #260 food checks: covered by Mac 14f; outside this run.
5. Seventh fix: still in flight on the Mac; later recheck. Its branch, worktree, lease, compiler and game were not used or disturbed.

### Restoration and validation

Gracefully stopped only owned PID 4596. Official status showed running=false, health=null, pid=null; process inventory found no Big Ambitions process. Executed this run's printed rollback. Full PersistentCity file inventory matches, all **45 original file hashes and four absences** match, and all **48 RLO files** match. No RLO files or overlay were restored/edited by the tester. Exact isolated registry preference export matches; PlayerSettings, persistent-city config, original autosave preferences and frame-cap absence are restored.

The scoped rollback reserialized/reordered `agent-install.json`, so its first post-rollback whole-file hash differed despite identical top-level data and complete path-to-entry maps. After verifying those maps, restored the exact original receipt backup bytes under the documented stopped-player/lifecycle reservation. No receipt entries were hand-edited or refreshed. Final receipt SHA-256 `4ace1ba1365a2dababda6861be7ce27bbe72c0fba4f48122a5af5cb08c9b5111` matches the pre-stage manifest. Registry export discrepancies in the first audit were resolved by importing this run's original export. Final audit: zero mismatches. AgentRigOps hash unchanged. Final lease free, pending=0; no owned process remains. The unsaved throwaway portrait is retained because no documented removal operation was used.

Build passed; compact JSON parsed; docs-links, markdown ban and whitespace checks recorded with the evidence commit. No offline test suite or regression suite. Only this checklist and the compact JSON changed. No production code, RLO, schema 1, food weights, version/changelog, release, Workshop, GitHub issue or merge. Build worktree removed after commit; evidence worktree removed after PR creation. All steps ran in the foreground and were awaited.

**RESULT: BLOCKED.**

## Rig results — 14 November 2026 i, Mac (#246 checks 4-5 with unrentable_fixture at 65b41db5)

**RESULT: DONE. Counts: 1 passed / 1 partial / 0 failed / 4 NOT RUN groups.** #246 check 4 is partial; check 5 passes the explicitly documented rig-classification fallback. Persistent City's applied-state Home evidence and tally are **not observable in-load**. No PC mismatch is claimed. In-game proof gates no merge.

Role `rig_test`, where `mac`, isolated `agentmac`, owner `persistent-city-rig-2026-11-14i-mac`. `<stage>` means `<home>/HART-Builds/persistent-city-rig-2026-11-14i-mac`; report copy `<home>/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-14i-mac.md`. Raw replies, both Player logs and nine screenshots stay outside Git. Raw evidence manifest `<stage>/raw-evidence-manifest.json`, SHA-256 `3d3e3839a60a8e6e53090a2ffda47957e2cbbcf399af2a678b4767ba11c4ab65`.

### Build and rig

| Item | Evidence |
| --- | --- |
| Candidate | `65b41db5a9567a1c79aa5cb972232800d0710b59`; initial and final fetched origin/codex/persistent-city tip, unchanged. |
| Branch / worktree | New `codex/mac-rig-2026-11-14i` / `rig-2026-11-14i-mac-evidence`; one isolated worktree served build and evidence. Other sessions and the original checkout were untouched. |
| Build | `python3 tools/build.py --game-managed "<home>/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0, Homebrew PATH and DOTNET_ROOT. No test sources changed; no full suite or regression suite. |
| PersistentCity DLL SHA-256 | `a0a1ef89db96420ce375100a09f6236c13d47b10f3856f9c1963079b0613341b`; built, package and live staged DLL matched. Both starts validated the receipt. |
| Official agent-rig HEAD | `17ff9f86ef07a8c071a90886fa76ed2e98f56c57`, clean; fetch / ff-only pull already current. Includes agent-rig #89. No checkout edit, commit, push or overlay update. |
| Live AgentRigOps SHA-256 | `d3188b0e837188aec8be85535846953170da99807ad22914ccf4a3c38d77ed3c`; live binary contains `unrentable_fixture`, loaded health reports the same hash, unchanged at cleanup. First in-game fixture use recorded below. |
| RLO Player.log | `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` All 53 package files hash unchanged; no RLO file installation, edit or restoration. |
| PC Player.log | `[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places.` The full line is below. No commit-stamped build line is emitted; matching DLL hashes and the validated receipt establish binary identity. |
| Lease / resources | Free/stopped initially; acquired `2026-11-14T19:16:55.201839Z`. Pre-stage 64.0 GiB disk / 77% reported free memory; after filter timeout 61.0 GiB / 44%. No resource refusal, takeover or other-process termination. |
| Starts / loads / stops | Two windowed rig-test processes, PIDs 26504 and 26861; one attempt per start, both successful. Cold source load 51.5 s, disposable load 65.5 s. Two graceful stops; no force. Temporary frame cap 30/vsync 0 restored to original absence; no performance window. |
| Autosave / saves | Original native 5 minutes / 3 saves, prevent_autosave=false; #70 suppression confirmed before and after both loads and at finish. One explicit save created only `FT RIGTMP-GR5-14i`, before apply. In the applied-state process, guard attempts/default_allowed/armed_allowed/unrestricted all 0; native save/compression idle. No save after apply, including after revert. |
| RLO options | Before/after equal: living_population stored=null, live=true; social_media stored=1, live=true; social_media_notices stored=0, live=false; original_saved=false for all. No option mutation. All saved RLO preference values backed up and restored with the isolated plist. |

Exact staging command, game stopped and lease held, from official agent-rig:

```sh
python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package
```

Receipt backup `<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T191747Z`. Package contains no RLO files. Printed rollback, verbatim apart from `<home>` path substitution:

```sh
rm -rf "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T191747Z/mod" "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T191747Z/install.json" "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

Automatic command review rejected that shell command before execution: `rm -f style commands are not permitted. Use a safer approach`. The stopped, leased restore retained the candidate folder in raw evidence, copied the entire backup mod into the empty live path, then copied back the backup receipt. No receipt was hand-edited.

### Per-check results

| Check | Result | Evidence |
| --- | --- | --- |
| #246 item 4, unrentable Home footprint | PARTIAL | All three required housed subjects exist, including rotated and yard/sidewalk cases. Their native geometry and rig-policy floors were read. PC reads places before ops run and has no documented reload route that preserves this fixture, so applied PC area evidence/floors and PC-to-visible ratios are not observable in-load. Screenshots show roof/ground context; facade occlusion prevents a reliable independent storey count. |
| #246 item 5, unreadable / empty tally | PASS, documented fallback | All 5 unrentable rows have pc_home_candidate=true: housed=3, unreadable=1, empty=1. Both class filters match 1. Empty's sole enabled collider has height 0, floors 0, capacity 0; it is empty, not unreadable. PC's own line and omitted-places warning for the applied state are not observable in-load. This is rig `UnrentableHome` evidence under agent-rig's prescribed fallback, not a nonzero PC runtime tally. |

Followed agent-rig `docs/rig-fixtures.md` “Unrentable residential subjects” and `docs/rig-ops.md` building contract. Loaded `FT GR5 13f Timing v1`, paused, saved the new name `FT RIGTMP-GR5-14i`, stopped and cold-loaded that disposable copy. Status before apply: applied=false, subjects=[], missing=[]. Apply's `after`: applied=true, 5 subjects, **missing=[]**. Each subject had size `agentrig:unrentable_fixture`, rentable_interior=false and the role's expected class. Original sizes: `ba:buildingsize_l` for rotated/plain, `ba:buildingsize_b` for yard, `ba:buildingsize_n` for unreadable/empty.

| Role / place | Rotated | Yard / sidewalk | Rig world footprint / policy floors (height) | PC Home area evidence / floors | PC-to-visible ratio |
| --- | --- | --- | --- | --- | --- |
| housed_rotated / `address:ba%3Astreet_firststreet:22:home` | true | false | 1488.514 m² / 13 (48.719 m high) | not observable in-load | not observable in-load |
| housed_yard / `address:ba%3Astreet_fifthavenue:25:home` | false | true | 426.709 m² / 5 (18.389 m high) | not observable in-load | not observable in-load |
| housed_plain / `address:ba%3Astreet_broadwaystreet:2:home` | false | false | 3826.156 m² / 11 (40.332 m high) | not observable in-load | not observable in-load |
| unreadable / `address:ba%3Astreet_broadwaystreet:13:home` | false | false | unavailable / unavailable | not observable in-load | not observable in-load |
| empty / `address:ba%3Astreet_broadwaystreet:23:home` | false | false | 2528.192 m² / 0 (0.000 m high) | not observable in-load | not observable in-load |

For the housed subjects only, the geometry proxy comparison is below. Local collider area removes yaw; renderer AABB is a visible-bounds envelope that can include roof structures and parts. These are not measured ground polygons or independent storey counts. All world/local ratios are below 1.5; this alone does not pass check 4. The last column is **rig input arithmetic**, world footprint × policy floors × 0.6, not a Home value observed from Persistent City.

| Role | Local collider area m² | Visible renderer AABB area m² | Visible envelope height m | World / local area | World / renderer AABB area | Rig implied usable area m² |
| --- | --- | --- | --- | --- | --- | --- |
| housed_rotated | 1431.112 | 1493.488 | 49.920 | 1.040 | 0.997 | 11610.409 |
| housed_yard | 426.709 | 437.253 | 23.514 | 1.000 | 0.976 | 1280.127 |
| housed_plain | 3826.154 | 3826.155 | 47.418 | 1.000 | 1.000 | 25252.630 |

Rotated yaw is 268.953°. Yard ground/bottom plane extends beyond the collider envelope (outside_footprint=true); its part is present but active=false at readback, so the fixture's “yard/sidewalk” selector establishes geometric overlap, not an active yard visual. Nine independent camera PNGs from the windowed rig-test start are retained only in `<stage>`. All were opened for inspection. The top views retain roofs/street context. First facade views are partly blocked by neighbouring buildings; the rotated/yard close views are inside solid geometry and the plain close view is obscured by a tree. None is used to invent a visible-storey count.

### Tally, exact operations and tooling gaps

`building {"action":"list","rentable":false}`: residential=585, matched=5, total=5, housed=3, unreadable=1, empty=1. Every matched row is a PC Home candidate. `rotated:true` matches only 22 1st Street. The original `meets_yard_or_sidewalk:true` request's eventual reply matches only 25 5th Avenue. The class selectors unreadable and empty each match their respective Broadway address. No missing role or `ops_building_fixture_no_native_subject` occurred.

Exact in-game operation errors:

```text
python3 scripts/mac-agent op building '{"action":"list","rentable":false,"meets_yard_or_sidewalk":true}'
{"error":"ops_timeout_do_not_retry"}
```

The 30 s client deadline expired while the hook reported `ops_heartbeat_stalled`. No request replay. The hook later resumed naturally; the original response `62034df8a23d444f9e96b693ce3833ab.json` was read and copied to raw evidence with ok=true, matched=1 and the expected filters. Thus the selected yard subject is established, while the client timeout remains a tooling failure. No cause inside the rig is asserted.

```text
python3 scripts/mac-agent op building '{"action":"read","place":"address:ba%3Astreet_broadwaystreet:13:home"}'
{"error":"ops_building_bounds_unavailable"}
```

This is the deliberately collider-free unreadable subject. Its detailed geometry is NOT RUN / tooling gap; the successful class/list replies establish the fallback tally. `read` for the empty subject succeeded. Every successful geometry read returns storeys=null / `ops_building_storeys_not_native`, the documented native-storey limitation, not another dispatch error. PC Home evidence, applied PC tally and applied PC warning are not observable in-load: the docs explicitly prohibit saving this applied fixture and state that reload would lose the disabled-collider part. No applied-state reload/save was attempted.

### Player.log excerpts and observed durations

Both loads log:

```text
[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.
[Persistent City] unrentable homes housed=0 beds=0 unreadable=0 empty=0
[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.
```

The zero tally describes the city at load, before apply. It is **not observable in-load** for the applied state and is not a mismatch. Temporary load-time hook warnings (`ops_heartbeat_stalled` in A, `ops_pump_missing` in B) recovered with `ops_pump_resumed`; both bounded loads completed. The yard-filter interval additionally logs:

```text
[AgentTests] Rig ops: disabled (ops_heartbeat_stalled)
[AgentTests] Rig ops: ready (ops_pump_resumed)
```

No perf, GC, modwork or save-profiler measurement was armed. Already-emitted PC log observations at or above 30 ms are listed below; component durations are not whole-frame measurements, and cumulative totals are not spikes.

| Process / log evidence | Observed single-call / maximum durations >=30 ms |
| --- | --- |
| A, source fixture, Slow routing frames 775 / 860 | preparation_ms=130.646 / 77.051 |
| A, Population observation | maximum advance 160.440; slowest unit food-continuation 160.394; historical planner scene-search maximum 121.085 (overlaps preparation evidence) |
| B, disposable fixture, Slow routing frames 868 / 1019 | preparation_ms=215.592; closure_ms=46.749 |
| B, first Population observation | maximum advance 178.292; slowest unit food-continuation 178.027; historical planner scene-search maximum 185.771 (overlaps preparation evidence) |
| B, next Population observation | maximum advance 154.314; slowest unit food-continuation 154.264 |

These logs do not establish event-by-event spike counts or p95. No extra timing window was run and no performance investigation followed the spikes.

### NOT RUN grouped

1. **GR-5 GC/street re-checks:** excluded; the seventh fix, pause/advance/street, is still in progress on this Mac; recheck follows its merge. Other session/worktree untouched.
2. **#262 checks 4–5:** excluded; no stable walker resident identity across reacquire, and no dense-street 48–96 walker fixture with paired refill. Rig bot gaps.
3. **GR-5 60 fps arms:** excluded, Windows-only; PC rig profile lacks FT fixtures, a Rig bot gap. Windows was not contacted or started.
4. **GR-5 save latency:** excluded; owner decision pending. The single disposable-copy save was setup only, with no save latency measurement.

Partial-check omissions are not counted again: PC's applied Home evidence/tally/warning, independent visible storeys and unreadable subject detailed geometry remain unverified as described above. No production changes, release, Workshop upload, version/changelog update, issue, reviewers, bot messages or merge. Schema stays 1; FoodChoicePriceWeight and FoodChoiceHouseholdWeight unchanged.

### Fixtures, restoration and validation

Reused source `FT GR5 13f Timing v1`; created only disposable copy **`FT RIGTMP-GR5-14i`** before mutation. The source fixture's four matching save/metadata file hashes are unchanged. Applied fixture was reverted under the lease; after.applied=false, subjects=[], missing=[], and a fresh unrentable list reports matched=0 / all class counts 0. Quit without saving. The disposable save remains in the isolated profile; no undocumented removal.

Independent pre-stage backup covered 35 files and 4 absent paths: whole PC package, receipt, isolated root configuration/preferences, PlayerSettings, persistent-city.json and isolated plist. Original native autosave and RLO options were recorded. After graceful stop, the whole original package/receipt was restored through the non-destructive equivalent above; isolated preference cache was imported from the backup, then original file bytes restored. Original frame-cap absence restored. All **35 original file hashes / 4 absent paths / exact PC file set / 53 RLO file hashes / 4 source fixture file hashes match; zero mismatches**. install.json SHA-256 `dea36ce03586cc53d3b2c7e5cd0fda6d9a54e61c670a21d0676ad18feee54a95`. AgentRigOps hash unchanged. All three RLO option readbacks match. Nine helper PNGs were copied/hash-verified to raw evidence and removed from their own helper output paths while stopped.

Lease released; final running=false, process=null, held=false, pending=0. Owned PIDs 26504/26861 and their caffeinate assertions are gone. Official agent-rig and original PC checkout clean. Build/evidence worktree is removed after commit, push and opening the unmerged docs-only PR. Only this existing checklist changes; raw evidence and report copy stay outside Git.

Offline validation: candidate build passed. Docs-links, markdown-ban and whitespace checks are recorded in the PR. No test source changed, no focused classes, no full suite. Final fetch left the candidate tip unchanged at `65b41db5`.

Refs #246

## Rig results — 14 November 2026 j, Mac (GR-5 GC recheck after #266 at 50f52c18)

**RESULT: DONE. Counts: 2 passed / 2 partial / 0 failed / 5 NOT RUN groups.** Day GC meets #266's allocation/count/heap expectation; night heap and pool shrink. Street **UpdateCity / mod-work p95 = 3.7732 / 3.8197 ms**, both below 5 ms, with **three >=30 ms spikes**. Their body-growth attribution is unverified, so the street check is partial. Look stability is partial through the resident-reacquire tooling gap. In-game proof gates no merge; no merge was performed.

Role `rig_test`, where `mac`, isolated `agentmac`, owner `persistent-city-rig-2026-11-14j-mac`. `<stage>` = `<home>/HART-Builds/persistent-city-rig-2026-11-14j-mac`. [Complete GC reports and compact evidence](perf/gr5-2026-11-14j-mac.json): **15,640 bytes**, SHA-256 `67f6d43e6a734ffbc1b8f350a3e508423e42b676ae5437f37f53d99f048ca1dc`. Raw command replies, logs, scripts, nine inspected PNGs and backups stay in `<stage>`. Raw manifest SHA-256 `267c207a05ed25b04db202ba2788216f0a7d8840d0f62a598f8ba0f115d00629`. Report copy: `<home>/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-14j-mac.md`.

### Build and rig

| Item | Evidence |
| --- | --- |
| Candidate | `50f52c18a840260daa7dec1ae47b51199d7db712`, initial and final fetched `origin/codex/persistent-city` agree; #266 included. |
| Worktree / branch | New `rig-2026-11-14j-mac-evidence` / `codex/mac-rig-2026-11-14j`; one isolated worktree served build and evidence. Original checkout unchanged. |
| Official agent-rig HEAD | `17ff9f86ef07a8c071a90886fa76ed2e98f56c57`; clean; fetch and ff-only pull already current. No tooling edit, build or overlay update. |
| Live AgentRigOps SHA-256 | `d3188b0e837188aec8be85535846953170da99807ad22914ccf4a3c38d77ed3c`; pre-start, loaded health and cleanup agree. |
| Build | `PATH=/opt/homebrew/bin:$PATH`, `DOTNET_ROOT` from Homebrew dotnet; `python3 tools/build.py --game-managed "<home>/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0. |
| PersistentCity DLL SHA-256 | `d1114d9094da71bb18f4137a6a446211c0654c235a7ca14d81f3514e0590247d`. Staged package contains no RLO or game DLL. |
| Loaded build identity | Candidate module ID `598a81ab-915b-4c39-a889-0f06c6c69325` matches loaded health. Receipt-validated starts accepted the staged hashes. No commit-stamped build line is emitted; the exact activation line is quoted below. A brief local module-ID inspection ran during the street window; shared-host cadence is observational. |
| Stage | `python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package`; stopped and leased. Receipt backup `<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T201646Z`; refreshed 28 scoped receipt entries. |
| Starts | One successful headless `start --mode headless --rig-test`, PID 41540; one successful cold windowed `start --mode windowed --rig-test`, PID 50499. No forced stop or load failure. |
| B frame cap | Isolated `agent-frame-cap.json` = `{"fps":30,"vsync":0}`; final applied=true, target=30, vSync=0; final short reading 5.72 fps. This is visual evidence, not a 30/60 fps performance arm. |
| RLO | `[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.` living_population stored absent/live=true; social_media=1/true; social_media_notices=0/false. No option toggle; no RLO file edit. |
| Autosave / SaveCost | Original native 5 minutes / 3 saves, prevent_autosave=false. #70 guard enabled and suppressed before/after each window and after both bounded loads. All window allowed saves and native saving frames = 0. SaveCostDiagnostics requested=false, writerSchema=1, save=null, activation=null; native save profiler installed=false. No manual save or save-latency measurement. |
| Lease / resources | Free at inspection; acquired 20:15:50.794588 UTC. Initial resources 62.9 GiB disk / 85% memory free; before B 62.8 GiB / 77%. No takeover, resource refusal, PC contact, other-session intervention or bot message. |

Printed rollback, path-sanitized:

```sh
rm -rf "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T201646Z/mod" "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T201646Z/install.json" "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

Cleanup used the stopped, leased equivalent that retains the candidate in `<stage>/staged-candidate-restored-out`, copies the complete backup mod into the empty live path and copies the exact backup receipt. No install receipt was hand-edited.

### Per-check outcome against each Expected line

| #266 check | Result | Expected, and observed |
| --- | --- | --- |
| 1. Day GC, 15:30 / 600 s | PASS | **Expected: advance clearly below 209 MB, gen0 at or below 7, and heap no higher than 14g. Met:** advance 107.466 MB, gen0=7, heap_after_gc 1,321,669,778 bytes, Mono heap 1,527,726,080 bytes; both heap readings below 14g. |
| 2. Night GC, 01:00 / 600 s after speed 20 | PASS | **Expected: owned bodies falling toward 16 after about 2 minutes idle, trimmed rising, and a smaller heap than 14g. Met in the observed idle period:** owned peaks at 42, first recorded trim by snapshot 300 s, reaches 17 by 420 s and 16 by 540 s; trimmed 0→28; Mono heap 1,579,483,136 < 1,894,055,936. Individual-body idle ages are not exposed; the full sampled trajectory is below, rather than a claim that the pool reached 16 at wall-clock t=120 s. |
| 3. Street / 600 s with pans | PARTIAL | **Expected: no spike >=30 ms from body growth; p95 under 5 ms at about 28 fps or more. At about 6 fps, p95 follows the reported build ms.** Strict <5 ms is met: 3.7732/3.8197 ms at 8.13 fps. Three UpdateCity spikes remain; no body-growth subcomponent was timed, so the no-growth-spike clause is **unverified**, not passed. Build estimates are 2.1–2.8 ms over the timed interval; the full-window p95 is compatible with build plus other UpdateCity work, but those summaries cannot prove per-frame correlation. Required 28+ fps is not reached. |
| 4. Look stability / cold windowed | PARTIAL / tooling gap | **Expected: the same look before and after, no undressed or invisible bodies, and no “Appearance meshes unavailable” or “Empty appearance” in the log.** Same-resident comparison unverified: two native targets went stale on return in 11.55/11.47 s. 120.437 s pan completed; nine stills inspected. Visible bodies are dressed; sparse/occluded return views and still captures cannot prove no invisible body or brief flash. All four required error strings have zero matches in both logs. |

The pass counts refer to the GC expectation lines, not every historical GR-5 target. The street p95 gate passes; its spike-source proof remains partial. No weaker numerical gate is substituted.

### GC windows and every allocation segment

Fixture **`FT GR5 13f Timing v1`**, confirmed 1,000 residents / 2,000 visitors / 1,683 places, person-target off, player x=215,z=0. No fallback was needed. Day Tuesday 15:30, night Wednesday 01:00. Speed=1 in each measurement; mod-work sampler unarmed. Untimed speed-20 advances occur outside windows. GC sampler incremental=true, max_generation=0; pause is cycle-ending frame excess, not all incremental collector work. Allocation counter=`boehm_total`; legacy frame allocation remains `heap_used`. MB/GB comparisons use decimal bytes; the logged KB scratch field is bytes/1024.

| Window | Stored / dropped frames | gen0/1/2 | Pause p50 / p95 / max ms | heap_after_gc_bytes | mono_heap_bytes | Mod-work in GC frames p95 / max ms |
| --- | --- | --- | --- | --- | --- | --- |
| day | 16846 / 0 | 7/0/0 | 26.799/28.581/28.581 | 1321669778 | 1527726080 | 2.353 / 2.353 |
| night | 17079 / 0 | 6/0/0 | 28.562/32.902/32.902 | 1372768256 | 1579483136 | 0.88 / 0.88 |

| Reference / metric | 14b day / night | 14g day / night | 14j day / night |
| --- | --- | --- | --- |
| gen0 | 11 / 8 | 7 / 6 | 7 / 6 |
| Pause p95 ms | 28.191 / 61.316 | 67.048 / 141.801 | 28.581 / 32.902 |
| Physical+advance MB | 1,021.587 / 433.642 | 309.570 / 134.021 | 200.927 / 68.086 |
| Advance MB | 236.448 / 72.746 | 209.051 / 71.735 | 107.466 / 33.489 |
| Mono heap bytes | 1,535,655,936 / 1,578,450,944 | 1,629,356,032 / 1,894,055,936 | 1,527,726,080 / 1,579,483,136 |

Day advance is 48.6% below 14g; night 53.3% below. Collection counts equal 14g and below 14b. Pause p95 day is 1.4% above 14b and 57.4% below 14g; night is 46.3% below 14b and 76.8% below 14g. These are measured comparisons, not controlled attribution across shared-host runs.

Every measured allocation segment follows, **bytes / samples / skipped**. Randomize and bake overlap appearance and are not added again. Four advance children sum to advance.

| Allocation segment | Day bytes / samples / skipped | Night bytes / samples / skipped |
| --- | --- | --- |
| mod | 315570912 / 16847 / 0 | 180623280 / 17080 / 0 |
| advance | 107466400 / 16753 / 0 | 33488696 / 16995 / 0 |
| physical | 93461016 / 16753 / 0 | 34597112 / 16995 / 0 |
| physical_ambient | 3219792 / 16753 / 0 | 3266032 / 16995 / 0 |
| physical_vehicles | 317664 / 16753 / 0 | 223296 / 16995 / 0 |
| physical_bindings | 111456 / 534725 / 0 | 736 / 120755 / 0 |
| physical_vendor_meals | 0 / 16753 / 0 | 0 / 16995 / 0 |
| physical_fade | 0 / 16753 / 0 | 0 / 16995 / 0 |
| physical_focus | 0 / 16753 / 0 | 0 / 16995 / 0 |
| physical_observe | 84624 / 517682 / 0 | 58928 / 103656 / 0 |
| physical_release | 7184 / 291 / 0 | 8112 / 132 / 0 |
| physical_parked | 1966928 / 584 / 0 | 1501664 / 587 / 0 |
| physical_index | 0 / 2198 / 0 | 80 / 2203 / 0 |
| physical_queues | 8960 / 2198 / 0 | 17200 / 2203 / 0 |
| physical_roster | 286800 / 2198 / 0 | 207808 / 2203 / 0 |
| physical_selection | 597032 / 2690 / 0 | 750296 / 2376 / 0 |
| physical_spawn_pedestrian | 30124416 / 829 / 0 | 13464480 / 230 / 0 |
| physical_spawn_appearance | 56698832 / 1074 / 0 | 15030256 / 273 / 0 |
| physical_spawn_check | 0 / 716 / 0 | 0 / 182 / 0 |
| physical_spawn_vehicle | 37328 / 21 / 0 | 68224 / 34 / 0 |
| physical_unattributed | 0 / 33506 / 0 | 0 / 33990 / 0 |
| physical_spawn_randomize | 3179248 / 358 / 0 | 832256 / 91 / 0 |
| physical_spawn_bake | 2659968 / 358 / 0 | 1578000 / 91 / 0 |
| advance_planning | 52017312 / 9424 / 0 | 22199992 / 3947 / 0 |
| advance_events | 55449088 / 21638 / 0 | 11288704 / 19875 / 0 |
| advance_stops | 0 / 16753 / 0 | 0 / 16995 / 0 |
| advance_other | 0 / 16753 / 0 | 0 / 16995 / 0 |

| Other counter | Day | Night |
| --- | --- | --- |
| counter_frame_bytes | 1631773400 | 1533437240 |
| counter_frames | 16846 | 17079 |
| frame_alloc_bytes | 1133277184 | 946946048 |
| max_mod_ms | 56.67 | 79.019 |
| gc_spikes_30ms | 7 | 6 |
| frame_spikes_30ms | 16834 | 17076 |

GC `max_mod_ms` is 56.670/79.019 ms. `pc gc stop` exposes aggregate maxima/counts, no frame/component spike list, so no exact-frame rows can be recovered for those GC maxima. This is a sampler readback limit, not zero spikes. Every exported **street mod-work** spike is listed below. Whole-frame counts are separate from mod-work and reflect the low frame rate.

### Residual and retained-state decision

- **day:** largest disjoint parent advance = 107,466,400 bytes, 53.49% of physical+advance. Largest advance subsegment `advance_events` = 55,449,088 bytes (51.60% of advance). Largest physical child `physical_spawn_appearance` = 56,698,832 bytes (60.67% of physical). Appearance 56,698,832 bytes = **7.61% of 14b**; appearance excluding randomize and bake = 50,859,616; bake plus that remainder = 53,519,584 bytes = **7.18% of 14b appearance**. Both appearance readings are below ~10%.
- **night:** largest disjoint parent physical = 34,597,112 bytes, 50.81% of physical+advance. Largest advance subsegment `advance_planning` = 22,199,992 bytes (66.29% of advance). Largest physical child `physical_spawn_appearance` = 15,030,256 bytes (43.44% of physical). Appearance 15,030,256 bytes = **4.59% of 14b**; appearance excluding randomize and bake = 12,620,000; bake plus that remainder = 14,198,000 bytes = **4.33% of 14b appearance**. Both appearance readings are below ~10%.

For clarity, the broader physical+advance residual is **19.67% / 15.70% of 14b**, above 10%; that is not the appearance/combiner trigger. No cause inside advance_events/planning is inferred from the names alone. #266's unimplemented entry projection and native spawn activation remain possible follow-up subjects, not proven causes here.

Bake scratch peaks at **45 sizes / 1,678 KB = 1.718 MB**, about **0.11% of the day Mono heap**. That is small against this heap and the model's 23 MB bake caches; the measured scratch does not support prioritizing a cap as the next slice. Scratch sizes/bytes reset when the physical population is recreated for speed mode; the report does not treat this as a process-wide permanent high-water mark.

### AppearanceBuilds, AppearanceReuses, pool hit rate and pedestrianLooks

Last periodic snapshots, not exact start-to-stop counter deltas. Pool hit rate is reused/(built+reused). GC randomize and bake each have **358 / 91 samples** day/night, giving the exact dressed-build counts inside those spans. The periodic logs are intentionally distinguished from those counts.

| Window | AppearanceBuilds / AppearanceReuses | Pool hit % | Idle / owned | Grown / trimmed | Fitted / waited | ms build | bake sizes / KB bake scratch |
| --- | --- | --- | --- | --- | --- | --- | --- |
| day GC | 298 / 16 | 5.10 | 15 / 88 | 88 / 1 | 238 / 146 | 2.4 | 45 / 1678 |
| night GC | 92 / 1 | 1.08 | 16 / 16 | 43 / 28 | 4 / 129 | 3.3 | 44 / 1634 |
| street | 148 / 7 | 4.52 | 27 / 45 | 54 / 10 | 40 / 160 | 2.1 | 45 / 1678 |

Night owned/idle over time: each row is the last periodic log seen in a snapshot taken at the stated wall-clock offset. Logs update about once a minute, so these are not exact event timestamps or per-body idle-age measurements.

| Snapshot seconds | Owned | Idle | Trimmed | Grown |
| --- | --- | --- | --- | --- |
| 60 | 2 | 1 | 0 | 1 |
| 120 | 28 | 15 | 0 | 27 |
| 180 | 42 | 21 | 0 | 41 |
| 240 | 42 | 35 | 0 | 41 |
| 300 | 41 | 33 | 1 | 41 |
| 360 | 29 | 24 | 13 | 41 |
| 420 | 17 | 16 | 25 | 41 |
| 480 | 18 | 16 | 26 | 43 |
| 540 | 16 | 16 | 28 | 43 |
| 600 | 16 | 16 | 28 | 43 |

### Street timing and every mod-work spike >=30 ms

Wednesday 15:30, same process/fixture after night and untimed advance; unlike 14g's corrective reload, no street reload was needed. GC sampler stopped, player x=215,z=0, nine camera pans successful. Native guard interval 601.677 s includes setup/end readback; timed mod-work interval below. No save allowed, no native saving frame. Coverage.complete=true, validity.valid=true, zero off-main-thread/cross-frame/unbalanced/depth-overflow/drop faults.

| Window | Seconds / frames / achieved fps | UpdateCity p95 / max ms | Mod-work p95 / max ms / >=30 spikes | Whole-frame p95 / max ms / >=30 count |
| --- | --- | --- | --- | --- |
| 14j | 600.815 / 4887 / 8.13 | 3.7732 / 70.1801 | 3.8197 / 70.2369 / 3 | 176.7198 / 450.1921 / 4863 |
| 14g | 600.399 / 3485 / 5.80 | 5.2988 / 64.7304 | 5.3683 / 64.7872 / 1 | 200.5798 / 506.5189 / 3485 |
| 14b | 600.284 / 3370 / 5.61 | 5.5170 / 78.1533 | 5.5824 / 78.2031 / 4 | 238.9091 / 1002.2687 / 3370 |

| Unity frame | Mod-work ms | UpdateCity ms | Dominant component / group | Whole-frame ms | Condition |
| --- | --- | --- | --- | --- | --- |
| 51358 | 70.2369 | 70.1801 | updatecity / updatecity | 103.8241 | speed=1|hour=15 |
| 54091 | 30.9771 | 30.9255 | updatecity / updatecity | 314.6618 | speed=1|hour=16 |
| 55161 | 33.2070 | 33.1629 | updatecity / updatecity | 200.2013 | speed=1|hour=17 |

All three spikes are reported, not chased. UpdateCity is the measured component boundary, so body growth, dressing, advance or another inner source cannot be assigned from these rows. No clean no-body-growth-spike claim follows from the improved p95. UpdateCity p95 is 28.8% below 14g; mod-work p95 is 28.8% below. Achieved cadence and street day differ; no isolated causal claim is made. `NativeCityClock.MaximumLagMinutes=1.0` both during untimed setup and after the window; session maximum includes advances. Final lag=0, caughtUpToTarget=true, refused=false, stoppedAfterAccountingFailure=false, historicalClockMisses=0. No runtime stop occurred.

### Look evidence, exact failures and tooling gaps

Cold windowed rig-test start, same fixture Tuesday 15:30, player x=215,z=0. Two native walker targets were followed and photographed, camera pointed away for 8 s, then their positions and camera-follow were requested again. The returned screenshots occurred within 11.549/11.465 s of departure; both handles were stale. Exact failed operations:

- `python3 scripts/mac-agent op screen_pos '{"action":"get","target":{"kind":"walker","id":"walker-09edc5be52bf4429b628df3fac486280"}}'` → `ops_view_stale_target`.
- `python3 scripts/mac-agent op camera '{"action":"follow","target":{"kind":"walker","id":"walker-09edc5be52bf4429b628df3fac486280"},"offset":{"x":0,"y":4,"z":-6},"seconds":10}'` → `ops_view_stale_target`.
- `python3 scripts/mac-agent op screen_pos '{"action":"get","target":{"kind":"walker","id":"walker-4e18f46e62c24da2b8bfbd748589e425"}}'` → `ops_view_stale_target`.
- `python3 scripts/mac-agent op camera '{"action":"follow","target":{"kind":"walker","id":"walker-4e18f46e62c24da2b8bfbd748589e425"},"offset":{"x":0,"y":4,"z":-6},"seconds":10}'` → `ops_view_stale_target`.

These are native walker handles, with no durable Persistent City resident identity or release/reacquire guarantee. No resident-reacquire op is advertised. No weaker same-look proof is substituted. Two before images show a yellow top/dark trousers and a blue top/shorts; return views contain other walkers or empty road. They are not an appearance-change failure. The 120.437 s pan used 24 camera operations and five additional images. All nine PNGs inspected; visible walkers are dressed, but empty/occluded views and sparse still sampling leave brief flashes/invisible bodies unproven. No refill benchmark was run; #262 dense-walker proof remains excluded.

| Exact Player.log string | A matches | B matches |
| --- | --- | --- |
| Appearance meshes unavailable | 0 | 0 |
| Empty appearance | 0 | 0 |
| Merged body already has blendshapes | 0 | 0 |
| Merged body vertex layout differs | 0 | 0 |

Other observation: the first `call console '{"line":"pc gc population"}'` just after bounded load completion returned `Warning: [Persistent City] gc population refused: no active city.` The native load had completed before the mod's asynchronous activation. No window opened then; the next query confirmed reached=3000 / 1000 residents / 2000 visitors before positioning and sampling. No ops_load_* failure, replay, staging refusal or runtime stop occurred.

Tooling limits within partial checks: durable resident reacquisition; body-growth attribution beneath the UpdateCity timing boundary; GC sampler frame/component lists for its aggregate maxima; no commit-stamped build log line. Exact invoked operations and available provenance are retained above. These omissions are not counted again as excluded groups.

### Player.log excerpts: build, speed changes and toggles

```text
[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.
[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.
A [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 15:30.
A [Mod:FoodTrucks] Test bridge: run_until day 2 15:30 reached at day 2 15:30; paused, speed 1.
A [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 01:00.
A [Mod:FoodTrucks] Test bridge: run_until day 3 01:00 reached at day 3 01:00; paused, speed 1.
A [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 15:30.
A [Mod:FoodTrucks] Test bridge: run_until day 3 15:30 reached at day 3 15:30; paused, speed 1.
B [Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 15:30.
B [Mod:FoodTrucks] Test bridge: run_until day 2 15:30 reached at day 2 15:30; paused, speed 1.
```

Explicit speed=1 replies are retained before each measured window and the visual check. No RLO toggle occurred. Transient startup NotReady/Flare messages precede successful activation; they are not a runtime stop.

### NOT RUN grouped

1. **GR-5 60 fps arms:** excluded; Mac reaches about 5 fps at 1,000 residents, and the PC throwaway time/perf/modwork fix was not landed for this request. No Windows PC contact or start.
2. **GR-5 save latency:** owner decision pending. SaveCostDiagnostics off, no save measurement or manual save.
3. **#262 checks 4–5 full identity/dense-walker proof:** Rig bot fixtures pending. Check 4's limited visual attempt above does not replace stable identity or a 48–96-walker paired refill. Refill outcome: NOT RUN.
4. **#246 check 4 full proof:** Rig bot in-load observation gap; excluded, not repeated here.
5. **Everything outside A/B:** excluded by scope, including other food, footprint, P-D and RLO-toggle checks. No new fixture, throwaway world or unrelated acceptance arm.

### Fixtures, restoration and validation

Only **`FT GR5 13f Timing v1`** was loaded, twice via `python3 scripts/mac-agent load '{"name":"FT GR5 13f Timing v1"}'`; both bounded loads completed. `FT RLO 13d Food v1` was not needed or loaded. No fixture save was created, changed by a save command, overwritten or removed.

Stopped only owned PIDs **41540 / 50499**, gracefully. While stopped and leased, restored full PersistentCity and exact install.json from receipt backup; imported this run's isolated preference backup and restored exact original file bytes, PlayerSettings, runtime configuration, persistent-city config and frame-cap absence. Native autosave remained 5 minutes / 3 saves; process-only suppression ended at stop. RLO live values matched their original stored/default values before stop; no original-saved toggle record. **32 original file hashes / 3 absent paths / exact PersistentCity file set / 53 RLO file hashes match; zero mismatches.** install.json SHA-256 `dea36ce03586cc53d3b2c7e5cd0fda6d9a54e61c670a21d0676ad18feee54a95`; AgentRigOps hash unchanged. All nine own helper PNGs copied/hash-verified into `<stage>` and removed only from their own helper output paths.

Lease released: running=false, process=null, held=false, pending=0. Both owned PIDs and their caffeinate assertions are gone. Official agent-rig and original Persistent City checkout clean. Evidence/build worktree is removed after commit, push and opening the PR. No production code, RLO package, schema 1, FoodChoicePriceWeight (1.2), FoodChoiceHouseholdWeight, version/changelog, release, Workshop, issue, reviewers or merge changed.

Validation: candidate build passed; compact JSON parses and is under 20 KB. Docs-links, markdown-ban and whitespace checks recorded with the PR. No test source changed; no focused test classes, full suite or regression suite. Final fetch left the candidate at 50f52c18. All long commands ran in the foreground and were awaited.

Refs #266

## Rig results — 14 November 2026 k, Mac (GR-5 GC/traffic recheck after #268 at 8eb87be2)

**0 passed / 0 partial / 3 failed / 5 NOT RUN groups. RESULT: DONE.** All three required 600 s windows completed in one fresh headless rig-test process. Counts use the GC gate for day/night and the full traffic expectation for street; completion is not a GC/street acceptance claim.

Role `rig_test`, where `mac`, isolated `agentmac`, owner `persistent-city-rig-2026-11-14k-mac`. `<stage>` = `<home>/HART-Builds/persistent-city-rig-2026-11-14k-mac`. Raw logs, replies, scripts and backups remain outside Git. Report copy: `<home>/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-14k-mac.md`.

[Complete GC reports and compact timing evidence](perf/gr5-2026-11-14k-mac.json): 17,557 bytes, SHA-256 `793d2535e9090707900647ea6ec30a55f93dbe8625320704ae0c4851ab307ef5`. Raw manifest `<stage>/raw-evidence-manifest.json`, SHA-256 `e95cbacfc0c34c7ac1b1b1223db3d2507b95966e9fd6cfc7a86232aea558ac61`.

### Build and rig

| Item | Evidence |
| --- | --- |
| Candidate | `8eb87be2c622357daa2cd6f11e0af590dc23f2d5`; initial tip matched the requested merge. Final fetched tip `8eb87be2c622357daa2cd6f11e0af590dc23f2d5`. Tested binary stayed at the initial SHA. |
| Branch / worktree | New `codex/mac-rig-2026-11-14k` / `rig-2026-11-14k-mac-evidence`; one isolated worktree served build/evidence. Original checkout and other sessions untouched. |
| Official agent-rig HEAD | `17ff9f86ef07a8c071a90886fa76ed2e98f56c57`; clean, fetch and ff-only pull current. No tooling edits or rig overlay update. |
| Live AgentRigOps SHA-256 | `d3188b0e837188aec8be85535846953170da99807ad22914ccf4a3c38d77ed3c`; pre-start, loaded health and cleanup agree. |
| Build | Homebrew PATH / DOTNET_ROOT; `python3 tools/build.py --game-managed "<home>/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0. |
| PersistentCity DLL SHA-256 | `365d427a2b968223d1d19c6d5ca63bfbc440fd01a63627a35e2509dcf7733ecf`; built package and live staged DLL matched; start validated receipt. Package has no RLO or game DLL. |
| Loaded build | Package MVID `45939a7e-b32e-41bb-9936-8638d625d052` matches loaded health (inspection after measurement). No commit-stamped build line is emitted; hash-validated receipt and activation establish provenance. |
| Stage | `python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package`; stopped and leased; refreshed 28 scoped receipt entries. Receipt backup `<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T215916Z`. |
| Start | One headless `start --mode headless --rig-test`, PID 74916; one bounded load. No load failure, cold restart or forced stop. |
| RLO | `Real Life Overhaul 1.0.6 initialised.` living_population stored absent/live=true; social_media=1; social_media_notices=0. No option toggle or RLO file edit. |
| Autosave / SaveCost | Original native 5 minutes / 3 saves, prevent_autosave=false. #70 process guard enabled/suppressed after load and before/after each window. SaveCostDiagnostics requested=false, writerSchema=1, save=null, activation=null; native save profiler installed=false. No manual save or save-latency measurement. |
| Lease / resources | Free/stopped initially; acquired 2026-11-14T21:58:32.3102350Z. Pre-start 63.5 GiB disk / 75% reported memory free. No other session intervention, bot message or Windows contact. |

Printed rollback, path-redacted:

```sh
rm -rf "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T215916Z/mod" "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T215916Z/install.json" "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

### Per-check Expected lines and outcomes

The REST body of PR #268 contains an “In-game checks” instruction and TARGETS, but no separately labelled Expected lines or completed before/after implementation table. The Expected lines below apply its stated target and this run’s explicit GC/street gates; no numerical criterion is relaxed. Offline proxy targets (below 44.73/4.36 MB, busy below 9,303 B/event, idle 0 B) are not re-tested by this in-game evidence.

- **1. Day, 15:30 / 600 s: FAIL. Expected:** report all requested counters; GC pause p95 under about 36 ms **and** gen0 at or under about 5 per window. Day target from #268: advance well under 100 MB per in-game 600 s day window. **Observed:** gen0 6, pause p95 219.813 ms, advance 85.040 MB. GC gate not met. Day advance target: 85.040 MB (15.0% below 100 MB); day advance target met at this measured level.
- **2. Night, 01:00 / 600 s after speed 20: FAIL. Expected:** report all requested counters; GC pause p95 under about 36 ms **and** gen0 at or under about 5 per window. Day target from #268: advance well under 100 MB per in-game 600 s day window. **Observed:** gen0 7, pause p95 234.512 ms, advance 35.434 MB. GC gate not met. The day-only allocation threshold is not silently applied to night.
- **3. Headless street, 600 s with camera pans: FAIL. Expected:** busy-street traffic and arrivals look unchanged versus 14j (same journeys, forecasts, results); UpdateCity/mod-work p95 <5 ms and every >=30 ms spike reported. Timed union and operational counters are below. Headless has no rendered traffic/arrival comparison, so unchanged visual behaviour is unverified; no weaker visual pass substituted.

### GC windows, timing and baselines

Fixture `FT GR5 13f Timing v1`, 1,000 residents / 2,000 visitors / 1,683 places, person-target off, player x=215,z=0. No fallback, new city or save. Day Tuesday 15:30; night Wednesday 01:00; street Wednesday 15:30. Speed=1 in windows, speed-20 advances outside. GC counter=boehm_total; legacy frame_alloc_bytes=heap_used. Incremental=true, max_generation=0; pause is cycle-ending frame excess, not total incremental collector work. MB means decimal bytes.

| Window | frames / dropped | gen0/1/2 | pause p50 / p95 / max ms | heap_after_gc_bytes | mono_heap_bytes | physical+advance MB | advance MB |
| --- | --- | --- | --- | --- | --- | --- | --- |
| gc-day | 15066 / 0 | 6/0/0 | 50.486/219.813/219.813 | 1367141034 | 1568964608 | 156.411 | 85.040 |
| gc-night | 16697 / 0 | 7/0/0 | 33.347/234.512/234.512 | 1333411254 | 1549402112 | 75.158 | 35.434 |

| Metric | 14b day / night | 14g day / night | 14j day / night | 14k day / night |
| --- | --- | --- | --- | --- |
| gen0 | 11 / 8 | 7 / 6 | 7 / 6 | 6 / 7 |
| pause p95 ms | 28.191 / 61.316 | 67.048 / 141.801 | 28.581 / 32.902 | 219.813 / 234.512 |
| physical+advance MB | 1021.587 / 433.642 | 309.570 / 134.021 | 200.927 / 68.086 | 156.411 / 75.158 |
| advance MB | 236.448 / 72.746 | 209.051 / 71.735 | 107.466 / 33.489 | 85.040 / 35.434 |

gc-day vs 14j: gen0 -1; pause p95 +191.232 ms; advance -22.426 MB (-20.9%).
gc-night vs 14j: gen0 +1; pause p95 +201.610 ms; advance +1.946 MB (+5.8%).

Every allocation segment below is bytes / samples / skipped. Randomize and bake overlap appearance and are not added again. Four advance children sum to advance; no finer advance sub-segments are exposed by this candidate.

| Allocation segment | Day bytes / samples / skipped | Night bytes / samples / skipped |
| --- | --- | --- |
| mod | 283686640 / 15067 / 0 | 190858008 / 16698 / 0 |
| advance | 85039992 / 14956 / 0 | 35434288 / 16625 / 0 |
| physical | 71370752 / 14956 / 0 | 39723784 / 16625 / 0 |
| physical_ambient | 2874768 / 14956 / 0 | 3193840 / 16625 / 0 |
| physical_vehicles | 1174928 / 14956 / 0 | 571776 / 16625 / 0 |
| physical_bindings | 17488 / 386991 / 0 | 17376 / 131893 / 0 |
| physical_vendor_meals | 0 / 14956 / 0 | 0 / 16625 / 0 |
| physical_fade | 0 / 14956 / 0 | 0 / 16625 / 0 |
| physical_focus | 0 / 14956 / 0 | 0 / 16625 / 0 |
| physical_observe | 398320 / 371790 / 0 | 144432 / 115147 / 0 |
| physical_release | 9888 / 246 / 0 | 8880 / 152 / 0 |
| physical_parked | 1919152 / 576 / 0 | 1708304 / 585 / 0 |
| physical_index | 80 / 2123 / 0 | 80 / 2184 / 0 |
| physical_queues | 9152 / 2123 / 0 | 17200 / 2184 / 0 |
| physical_roster | 287904 / 2123 / 0 | 193024 / 2184 / 0 |
| physical_selection | 1291128 / 2533 / 0 | 592168 / 2360 / 0 |
| physical_spawn_pedestrian | 21737976 / 569 / 0 | 14445456 / 250 / 0 |
| physical_spawn_appearance | 39317584 / 726 / 0 | 16341024 / 300 / 0 |
| physical_spawn_check | 0 / 484 / 0 | 0 / 200 / 0 |
| physical_spawn_vehicle | 176768 / 83 / 0 | 96224 / 26 / 0 |
| physical_unattributed | 2155616 / 29912 / 0 | 2394000 / 33250 / 0 |
| physical_spawn_randomize | 2161248 / 242 / 0 | 913856 / 100 / 0 |
| physical_spawn_bake | 2829728 / 242 / 0 | 1561072 / 100 / 0 |
| advance_planning | 43076448 / 9019 / 0 | 21750912 / 4236 / 0 |
| advance_events | 39809880 / 18411 / 0 | 11289376 / 19854 / 0 |
| advance_stops | 2153664 / 14956 / 0 | 2394000 / 16625 / 0 |
| advance_other | 0 / 14956 / 0 | 0 / 16625 / 0 |

### Remaining allocation and pool findings

Day advance is 59.3% below 14g and 20.9% below 14j, meeting the below-100 MB day target at 85.040 MB. Both composite GC gates fail on pause p95 and gen0. The mod residual outside physical+advance exceeds either named parent; its source is not instrumented by the allocation sampler. This run does not assign that residual or the pause regression to a production method.

- **gc-day:** largest physical/advance parent `advance` = 85,039,992 bytes (54.37% of physical+advance). Largest advance sub-segment `advance_planning` = 43,076,448 bytes (50.65% of advance). Mod allocation outside physical+advance = 127,275,896 bytes (44.86% of mod). Last logged AppearanceBuilds/Reuses=222/13, pool hit=5.53% (reuse/(build+reuse)); periodic, not exact window deltas.
- **gc-night:** largest physical/advance parent `physical` = 39,723,784 bytes (52.85% of physical+advance). Largest advance sub-segment `advance_planning` = 21,750,912 bytes (61.38% of advance). Mod allocation outside physical+advance = 115,699,936 bytes (60.62% of mod). Last logged AppearanceBuilds/Reuses=101/1, pool hit=0.98% (reuse/(build+reuse)); periodic, not exact window deltas.

| Window | Last logged pedestrianLooks |
| --- | --- |
| gc-day | 222 built/13 reused/3530 deferred/17 idle/65 owned/232 baked/45 bake sizes/1678 KB bake scratch/65 grown/1 trimmed/6 fitted/279 waited/4.9 ms build, ownCars=0, parkedCars=16, instances=26, heldGroups=Personal, retired=34, loadsRetired=29, shareReapplied=2, carAdmission=pc:257:admitted: |
| gc-night | 101 built/1 reused/5242 deferred/16 idle/16 owned/479 baked/43 bake sizes/1618 KB bake scratch/46 grown/31 trimmed/52 fitted/93 waited/6.3 ms build, ownCars=0, parkedCars=18, instances=25, heldGroups=Personal, retired=0, loadsRetired=0, shareReapplied=2, carAdmission=c57:admitted: |
| street | 30 built/0 reused/1166 deferred/16 idle/23 owned/644 baked/36 bake sizes/1289 KB bake scratch/25 grown/3 trimmed/12 fitted/41 waited/11.0 ms build, ownCars=0, parkedCars=18, instances=18, heldGroups=Personal, retired=25, loadsRetired=16, shareReapplied=1, carAdmission=:: |

### UpdateCity, mod-work, whole-frame and every >=30 ms mod-work spike

All windows used the rig union timer with UpdateCity, AdvancePopulation, route preparation, RunPhysical, inspector, body acquire, instantiate and dress components. Component timings are **self time**: children are subtracted. Thus the separately reported UpdateCity p95 is self time, not an inclusive UpdateCity percentile and not directly comparable to 14j’s single-component UpdateCity p95. Inclusive UpdateCity is bounded by the complete mod-work union in each frame, so union p95 <5 ms proves the inclusive <5 ms gate, but its exact percentile is a tooling gap. Observer overhead stays in union. GC windows now also have rig timing patches, unlike 14j; allocation/cadence comparisons are observational on this shared host.

| Window | seconds / frames / fps | UpdateCity self p95 / max ms | mod-work p95 / max / spikes | whole-frame p95 / max ms | MaximumLagMinutes |
| --- | --- | --- | --- | --- | --- |
| gc-day | 600.988 / 15068 / 25.07 | 0.4234 / 63.3563 | 3.8191 / 98.8772 / 16 | 68.1656 / 1466.9142 | 0.7866191864018219 |
| gc-night | 600.673 / 16708 / 27.82 | 0.38 / 118.2388 | 2.6698 / 139.7778 / 2 | 45.2048 / 1158.5734 | 1.0 |
| street | 605.379 / 2583 / 4.27 | 4.9955 / 269.979 | 23.2135 / 387.9724 / 108 | 899.4064 / 6621.1226 | 1.0 |

MaximumLagMinutes is the session maximum and includes untimed speed-20 setup. Final lag/caught-up state is reported separately below.

14j street: UpdateCity 3.7732 ms, union 3.8197 ms, 8.13 fps, three spikes 70.2369/30.9771/33.2070 ms. 14b street: 5.5170/5.5824 ms, 5.61 fps, four spikes.

The nominal waits were 600 s; actual timed spans include command/readback dispatch and are authoritative in the table. Street native clock waiting reached 433.847 s; final lag=0.111 game minutes, caughtUpToTarget=false, refused=false, stoppedAfterAccountingFailure=false.

14k street union vs 14j: +19.3938 ms (+507.7%). Strict union <5 ms not met. Exact inclusive UpdateCity delta unavailable under the component split.

| Window | Unity frame | mod-work ms | dominant self component | whole-frame ms | top self components ms |
| --- | --- | --- | --- | --- | --- |
| gc-day | 5090 | 98.8772 | updatecity | 144.742 | updatecity=63.3563; body_instantiate=16.4223; advance=8.0741; physical=6.462; body_dress=2.3368; body_acquire=1.8998 |
| gc-day | 10095 | 40.4293 | body_instantiate | 166.5232 | body_instantiate=30.578; physical=4.5698; updatecity=2.569; advance=1.8759; prepare=0.2528; NativeCityClock.AccountingPrefix=0.2034 |
| gc-day | 11483 | 39.6167 | body_acquire | 93.3502 | body_acquire=14.2983; prepare=13.5775; body_dress=5.3176; advance=2.8753; physical=2.4443; updatecity=0.968 |
| gc-day | 16269 | 30.6313 | physical | 286.8034 | physical=15.8542; updatecity=7.9927; advance=3.9978; prepare=1.3638; NativeCityClock.AccountingPrefix=1.151; PopulationRuntime.Update=0.0786 |
| gc-day | 16396 | 32.7795 | physical | 202.319 | physical=23.1956; advance=5.416; updatecity=3.4274; prepare=0.288; NativeCityClock.AccountingPrefix=0.2861; PopulationRuntime.Update=0.0576 |
| gc-day | 16526 | 66.3271 | physical | 205.7908 | physical=27.8439; body_acquire=12.3925; updatecity=11.5046; body_dress=10.7138; advance=3.241; prepare=0.2654 |
| gc-day | 17118 | 33.4014 | body_instantiate | 102.6016 | body_instantiate=26.6952; advance=5.0353; physical=0.9077; updatecity=0.5696; body_acquire=0.0632; prepare=0.0429 |
| gc-day | 17301 | 49.2285 | physical | 154.5323 | physical=28.0427; prepare=6.38; body_dress=5.8946; updatecity=3.4652; body_acquire=2.9804; advance=2.0708 |
| gc-day | 17358 | 62.9492 | body_instantiate | 178.318 | body_instantiate=56.1125; physical=2.899; advance=2.6209; updatecity=1.1185; body_acquire=0.0646; prepare=0.0628 |
| gc-day | 17373 | 59.8352 | body_dress | 218.9851 | body_dress=40.5386; body_acquire=15.9226; physical=1.8584; updatecity=1.4126; prepare=0.033; NativeCityClock.AccountingPrefix=0.0198 |
| gc-day | 17391 | 57.7255 | physical | 110.8249 | physical=24.2028; body_acquire=18.3099; body_dress=12.538; updatecity=1.346; advance=1.2423; prepare=0.033 |
| gc-day | 17405 | 65.8161 | body_instantiate | 128.9681 | body_instantiate=49.7429; physical=13.8988; updatecity=1.3245; advance=0.6319; body_acquire=0.0823; prepare=0.0566 |
| gc-day | 17407 | 51.9825 | physical | 218.8943 | physical=26.1501; updatecity=9.7912; body_acquire=8.2832; body_dress=5.5267; prepare=0.8672; advance=0.6093 |
| gc-day | 17491 | 30.3692 | updatecity | 111.0859 | updatecity=28.6766; prepare=1.05; physical=0.5767; advance=0.0209; NativeCityClock.AccountingPrefix=0.0145; inspector=0.0089 |
| gc-day | 17492 | 42.0883 | body_instantiate | 137.6511 | body_instantiate=34.6246; physical=3.1335; body_acquire=2.3849; prepare=1.0637; updatecity=0.7883; NativeCityClock.AccountingPrefix=0.0244 |
| gc-day | 17727 | 48.4438 | body_instantiate | 105.5592 | body_instantiate=44.2604; advance=1.565; physical=1.0938; body_acquire=1.0314; updatecity=0.4059; prepare=0.0321 |
| gc-night | 26129 | 139.7778 | updatecity | 274.3695 | updatecity=118.2388; advance=6.809; physical=5.9871; body_instantiate=4.8419; body_dress=1.8484; body_acquire=1.3078 |
| gc-night | 39311 | 30.896 | updatecity | 68.7367 | updatecity=29.6228; prepare=1.0582; physical=0.1541; advance=0.0139; NativeCityClock.AccountingPrefix=0.0134; inspector=0.0112 |
| street | 50298 | 272.5144 | updatecity | 393.655 | updatecity=269.979; prepare=2.5003; NativeCityClock.AccountingPrefix=0.0145; ResidentInspector.UiRaycastPrefix=0.0064; physical=0.0048; PopulationRuntime.Update=0.0034 |
| street | 50352 | 39.6271 | prepare | 71.7974 | prepare=38.2664; updatecity=1.3163; PopulationRuntime.Update=0.0204; NativeCityClock.AccountingPrefix=0.0116; ResidentInspector.UiRaycastPrefix=0.0053; ResidentInspector.MouseRunPrefix=0.0015 |
| street | 50402 | 71.2955 | updatecity | 104.1166 | updatecity=53.1768; advance=6.8178; body_instantiate=4.275; physical=4.0817; body_dress=1.5605; body_acquire=1.1084 |
| street | 51999 | 74.4791 | physical | 2083.0537 | physical=38.6554; updatecity=27.3742; prepare=2.7778; advance=2.7152; NativeCityClock.AccountingPrefix=2.3225; NativeCityClock.TickPrefix=0.2217 |
| street | 52000 | 57.2924 | physical | 1665.1822 | physical=25.4016; updatecity=15.0222; advance=8.1429; prepare=7.5327; NativeCityClock.AccountingPrefix=0.6629; ResidentInspector.MouseRunPrefix=0.1655 |
| street | 52004 | 42.6555 | physical | 2549.6287 | physical=34.2959; updatecity=6.7726; advance=0.7534; prepare=0.308; NativeCityClock.AccountingPrefix=0.2378; PopulationRuntime.LateUpdate=0.0893 |
| street | 52005 | 45.6479 | physical | 1633.4054 | physical=30.4979; updatecity=10.4702; advance=3.1751; prepare=0.968; NativeCityClock.AccountingPrefix=0.2889; inspector=0.1049 |
| street | 52006 | 34.9193 | physical | 1255.0898 | physical=21.9824; updatecity=8.9995; advance=2.4855; NativeCityClock.AccountingPrefix=0.7507; prepare=0.4031; ResidentInspector.MouseRunPrefix=0.1038 |
| street | 52007 | 34.9595 | physical | 1513.4191 | physical=21.1574; updatecity=9.3278; advance=2.6096; NativeCityClock.AccountingPrefix=1.0298; prepare=0.4378; PopulationRuntime.Update=0.1293 |
| street | 52008 | 77.001 | updatecity | 1414.3722 | updatecity=53.9639; physical=18.6431; advance=2.0384; prepare=1.7572; NativeCityClock.AccountingPrefix=0.3394; NativeFoodOwnership.AfterNativeTick=0.0646 |
| street | 52009 | 36.3526 | physical | 1365.1843 | physical=21.0994; updatecity=12.5195; prepare=1.6793; NativeCityClock.AccountingPrefix=0.7042; advance=0.1067; ResidentInspector.MouseRunPrefix=0.054 |
| street | 52012 | 35.1351 | physical | 1504.0187 | physical=22.9207; updatecity=7.6865; prepare=2.2667; NativeCityClock.AccountingPrefix=1.702; ResidentInspector.MouseRunPrefix=0.19; PopulationRuntime.Update=0.0868 |
| street | 52014 | 37.5512 | physical | 1537.5573 | physical=28.5301; updatecity=5.3109; prepare=2.01; NativeCityClock.AccountingPrefix=1.2496; advance=0.1615; ResidentInspector.MouseRunPrefix=0.0995 |
| street | 52015 | 34.8557 | physical | 1619.5215 | physical=23.1679; updatecity=8.4282; prepare=1.4693; NativeCityClock.AccountingPrefix=0.9094; advance=0.5063; PopulationRuntime.Update=0.1555 |
| street | 52019 | 44.1765 | physical | 1874.4061 | physical=27.2785; updatecity=9.9627; advance=3.8515; prepare=1.5658; NativeCityClock.AccountingPrefix=1.0896; inspector=0.1323 |
| street | 52020 | 30.0645 | physical | 1121.1033 | physical=13.2718; updatecity=8.3038; advance=5.2584; prepare=1.4543; NativeCityClock.AccountingPrefix=0.8821; PopulationRuntime.Update=0.4239 |
| street | 52022 | 30.1564 | physical | 1426.2693 | physical=22.4235; updatecity=4.7463; prepare=1.3333; NativeCityClock.AccountingPrefix=0.8329; ResidentInspector.MouseRunPrefix=0.3916; advance=0.1786 |
| street | 52023 | 59.3145 | updatecity | 1662.2998 | updatecity=35.4283; physical=16.7315; advance=4.8845; prepare=1.4327; NativeCityClock.AccountingPrefix=0.2974; AmbientPedestrians.SetActivePrefix=0.1653 |
| street | 52024 | 43.1328 | physical | 1387.7137 | physical=24.1459; updatecity=11.3417; advance=5.6768; prepare=1.3159; NativeCityClock.AccountingPrefix=0.3235; NativeTravelPace.ObserveTickPostfix=0.1308 |
| street | 52025 | 39.5447 | physical | 1658.8328 | physical=23.9414; updatecity=8.892; advance=4.1344; prepare=1.4034; NativeCityClock.AccountingPrefix=0.8636; ResidentInspector.MouseRunPrefix=0.0809 |
| street | 52026 | 44.967 | physical | 1552.0231 | physical=27.1476; updatecity=8.0358; advance=7.9416; prepare=1.386; NativeCityClock.AccountingPrefix=0.2853; inspector=0.0517 |
| street | 52030 | 31.5307 | physical | 1314.136 | physical=14.7889; updatecity=7.4606; advance=2.8761; NativeCityClock.AccountingPrefix=2.5144; NativeCityClock.TickPrefix=1.393; prepare=1.3078 |
| street | 52032 | 90.7907 | updatecity | 1594.1625 | updatecity=61.3185; physical=22.7539; prepare=2.8371; advance=2.8035; NativeCityClock.AccountingPrefix=0.897; inspector=0.0423 |
| street | 52043 | 32.5013 | physical | 1167.4115 | physical=25.5053; advance=4.3958; updatecity=1.2287; prepare=1.2042; ResidentInspector.UiRaycastPrefix=0.0661; inspector=0.0345 |
| street | 52046 | 40.6142 | physical | 953.2037 | physical=24.5474; updatecity=9.824; prepare=3.0426; advance=2.6256; NativeCityClock.AccountingPrefix=0.3647; NativeFoodOwnership.AfterNativeTick=0.0457 |
| street | 52069 | 62.9893 | physical | 819.2982 | physical=54.2806; updatecity=3.6044; advance=2.3523; prepare=1.4118; NativeCityClock.AccountingPrefix=1.1265; ResidentInspector.MouseRunPrefix=0.0768 |
| street | 52089 | 58.2051 | physical | 537.1635 | physical=42.2739; updatecity=7.4963; advance=5.296; prepare=1.6258; NativeCityClock.AccountingPrefix=1.4625; inspector=0.0235 |
| street | 52119 | 38.0723 | physical | 768.8061 | physical=27.2002; updatecity=4.547; advance=3.0839; NativeCityClock.AccountingPrefix=2.0864; ResidentInspector.MouseRunPrefix=0.6122; prepare=0.3385 |
| street | 52128 | 33.1687 | physical | 486.1929 | physical=13.5707; updatecity=7.687; advance=6.0548; prepare=5.4036; NativeCityClock.AccountingPrefix=0.2266; PopulationRuntime.Update=0.1041 |
| street | 52141 | 30.9297 | advance | 609.1096 | advance=21.8601; physical=8.1489; updatecity=0.7807; prepare=0.0567; NativeCityClock.AccountingPrefix=0.0269; PopulationRuntime.LateUpdate=0.0172 |
| street | 52159 | 48.3188 | physical | 1296.2685 | physical=36.9652; updatecity=7.3693; advance=2.3845; prepare=1.2675; PopulationRuntime.Update=0.119; NativeCityClock.AccountingPrefix=0.0924 |
| street | 52184 | 59.5188 | updatecity | 597.038 | updatecity=36.879; physical=18.0462; advance=3.0889; prepare=1.2908; NativeCityClock.AccountingPrefix=0.1214; PopulationRuntime.Update=0.0304 |
| street | 52203 | 51.2016 | updatecity | 723.8093 | updatecity=22.3202; prepare=16.2354; physical=10.0359; advance=2.1007; PopulationRuntime.Update=0.3822; NativeCityClock.AccountingPrefix=0.073 |
| street | 52205 | 46.6671 | physical | 739.9105 | physical=27.4741; updatecity=15.1779; prepare=1.8493; advance=1.4326; NativeCityClock.AccountingPrefix=0.3892; PopulationRuntime.Update=0.1008 |
| street | 52212 | 37.1554 | physical | 675.3426 | physical=24.3921; updatecity=7.77; advance=3.4847; ResidentInspector.MouseRunPrefix=0.5215; prepare=0.3716; NativeCityClock.AccountingPrefix=0.3067 |
| street | 52358 | 33.9052 | physical | 371.565 | physical=30.1603; advance=1.3587; prepare=1.1049; updatecity=1.0447; NativeCityClock.AccountingPrefix=0.1038; PopulationRuntime.LateUpdate=0.0617 |
| street | 52383 | 36.2511 | updatecity | 386.8748 | updatecity=33.9068; advance=1.6914; physical=0.5603; prepare=0.0344; NativeCityClock.AccountingPrefix=0.0189; ResidentInspector.UiRaycastPrefix=0.0155 |
| street | 52384 | 42.603 | physical | 561.4432 | physical=32.6393; updatecity=4.7186; advance=4.3105; NativeCityClock.AccountingPrefix=0.594; prepare=0.1544; ResidentInspector.UiRaycastPrefix=0.1229 |
| street | 52387 | 53.6516 | physical | 1108.6623 | physical=36.2036; updatecity=12.298; advance=2.4327; prepare=1.0319; NativeCityClock.AccountingPrefix=0.8962; ResidentInspector.UiRaycastPrefix=0.4167 |
| street | 52390 | 69.4839 | physical | 1423.4665 | physical=30.918; updatecity=20.2094; prepare=12.757; advance=5.0538; NativeCityClock.AccountingPrefix=0.2442; inspector=0.1062 |
| street | 52391 | 49.9962 | physical | 1647.0498 | physical=38.361; updatecity=6.7945; prepare=2.9221; advance=1.3441; NativeCityClock.AccountingPrefix=0.2992; PopulationRuntime.LateUpdate=0.0644 |
| street | 52392 | 40.9347 | physical | 815.6728 | physical=31.3877; updatecity=5.8417; advance=2.8996; prepare=0.3588; NativeCityClock.AccountingPrefix=0.2975; inspector=0.0353 |
| street | 52394 | 36.5689 | physical | 1370.0403 | physical=25.4305; updatecity=7.0595; advance=3.1886; prepare=0.3469; NativeCityClock.AccountingPrefix=0.2964; NativeCityClock.TickPrefix=0.1212 |
| street | 52395 | 74.7541 | physical | 1746.6154 | physical=44.5458; updatecity=18.9962; advance=4.5437; prepare=3.6629; NativeCityClock.AccountingPrefix=1.9076; ResidentInspector.MouseRunPrefix=0.3519 |
| street | 52396 | 52.5366 | physical | 1755.2548 | physical=39.4988; updatecity=8.1952; advance=2.4979; prepare=1.4692; NativeCityClock.AccountingPrefix=0.5217; inspector=0.1375 |
| street | 52397 | 66.1695 | physical | 1961.8113 | physical=53.5309; updatecity=7.38; advance=3.8363; prepare=0.4749; NativeCityClock.AccountingPrefix=0.2599; PopulationRuntime.LateUpdate=0.2301 |
| street | 52398 | 36.8622 | physical | 1617.3093 | physical=21.5383; updatecity=11.6424; advance=2.7621; prepare=0.3784; NativeCityClock.AccountingPrefix=0.2909; PopulationRuntime.LateUpdate=0.0661 |
| street | 52399 | 74.9377 | updatecity | 1657.9734 | updatecity=59.421; physical=9.5268; prepare=4.0385; advance=0.9799; NativeCityClock.AccountingPrefix=0.2947; NativeFoodOwnership.AfterNativeTick=0.2441 |
| street | 52400 | 43.8616 | physical | 1386.7293 | physical=22.274; updatecity=10.1679; prepare=6.1033; advance=2.7446; NativeCityClock.AccountingPrefix=1.0959; NativeFoodOwnership.AfterNativeTick=1.009 |
| street | 52401 | 42.3524 | physical | 1520.9839 | physical=27.2973; updatecity=12.4197; prepare=1.4496; advance=0.6048; NativeCityClock.AccountingPrefix=0.3389; PopulationRuntime.Update=0.0468 |
| street | 52402 | 42.6162 | physical | 1966.4081 | physical=28.6034; updatecity=9.639; advance=2.287; prepare=1.5125; NativeCityClock.AccountingPrefix=0.2782; PopulationRuntime.LateUpdate=0.0807 |
| street | 52403 | 55.5318 | physical | 1688.584 | physical=34.1187; updatecity=13.0429; advance=4.641; prepare=1.9403; NativeCityClock.AccountingPrefix=1.4664; PopulationRuntime.Update=0.0972 |
| street | 52404 | 36.9968 | physical | 1405.5906 | physical=19.8434; updatecity=11.1866; advance=2.4698; prepare=2.2058; NativeCityClock.AccountingPrefix=0.7676; ResidentInspector.MouseRunPrefix=0.1939 |
| street | 52405 | 49.3202 | updatecity | 1713.7155 | updatecity=22.8264; physical=20.3533; advance=2.3985; prepare=1.9977; NativeCityClock.AccountingPrefix=1.1198; ResidentInspector.UiRaycastPrefix=0.1855 |
| street | 52406 | 57.6927 | physical | 1506.5843 | physical=40.4515; updatecity=10.493; advance=3.4989; prepare=1.5051; NativeCityClock.AccountingPrefix=1.3088; ResidentInspector.MouseRunPrefix=0.1722 |
| street | 52407 | 40.456 | physical | 1727.6446 | physical=24.4926; updatecity=9.4424; advance=2.7435; prepare=2.3805; NativeCityClock.AccountingPrefix=0.8042; PopulationRuntime.LateUpdate=0.2546 |
| street | 52408 | 32.893 | physical | 1231.2911 | physical=21.5677; updatecity=5.6284; advance=2.9773; prepare=1.8369; NativeCityClock.AccountingPrefix=0.5738; PopulationRuntime.Update=0.1014 |
| street | 52409 | 41.8842 | physical | 1597.4762 | physical=28.6244; updatecity=6.5563; advance=3.9817; prepare=2.1251; NativeCityClock.AccountingPrefix=0.2704; inspector=0.1659 |
| street | 52410 | 57.0009 | physical | 1302.9116 | physical=29.0452; updatecity=14.1055; advance=9.8593; prepare=2.861; NativeCityClock.AccountingPrefix=0.6882; inspector=0.1069 |
| street | 52416 | 43.2323 | physical | 1534.482 | physical=26.745; updatecity=5.0603; prepare=4.7014; advance=3.6525; ResidentInspector.MouseRunPrefix=1.3788; NativeCityClock.AccountingPrefix=1.1763 |
| street | 52417 | 87.3877 | physical | 1491.6774 | physical=51.8977; updatecity=22.9367; advance=6.7794; prepare=4.1818; NativeCityClock.AccountingPrefix=1.2854; ResidentInspector.MouseRunPrefix=0.1431 |
| street | 52418 | 49.492 | physical | 1639.0521 | physical=25.1325; updatecity=17.0389; advance=4.3234; prepare=2.3845; NativeCityClock.AccountingPrefix=0.3438; PopulationRuntime.Update=0.0746 |
| street | 52419 | 34.9736 | physical | 1107.5603 | physical=17.8597; updatecity=9.5331; advance=4.2515; prepare=1.5136; NativeCityClock.AccountingPrefix=1.4661; ResidentInspector.MouseRunPrefix=0.0817 |
| street | 52420 | 45.655 | physical | 555.1228 | physical=24.5574; updatecity=10.7585; advance=7.8599; prepare=1.4602; NativeCityClock.AccountingPrefix=0.6409; ResidentInspector.MouseRunPrefix=0.103 |
| street | 52422 | 75.5374 | physical | 1564.8005 | physical=60.2704; updatecity=8.8369; advance=3.6924; prepare=2.0989; NativeCityClock.AccountingPrefix=0.2509; inspector=0.1151 |
| street | 52423 | 85.3571 | updatecity | 3393.5119 | updatecity=56.4529; physical=22.5448; advance=3.631; prepare=1.8967; NativeCityClock.AccountingPrefix=0.3089; PopulationRuntime.LateUpdate=0.2411 |
| street | 52424 | 51.1842 | physical | 1590.872 | physical=33.5098; advance=5.7665; updatecity=5.4401; prepare=4.7153; NativeCityClock.AccountingPrefix=1.3429; ResidentInspector.MouseRunPrefix=0.1388 |
| street | 52425 | 51.5042 | physical | 1561.1156 | physical=31.4412; updatecity=14.1761; advance=2.9418; prepare=1.6521; NativeCityClock.AccountingPrefix=0.845; inspector=0.1193 |
| street | 52426 | 52.0875 | physical | 1165.4068 | physical=34.7796; updatecity=10.8715; prepare=2.7832; advance=2.284; NativeCityClock.AccountingPrefix=0.933; ResidentInspector.MouseRunPrefix=0.1025 |
| street | 52427 | 65.1053 | physical | 2543.1285 | physical=44.2806; updatecity=12.1298; advance=3.636; prepare=3.3838; NativeCityClock.AccountingPrefix=1.0017; NativeCityClock.TickPrefix=0.2803 |
| street | 52428 | 116.9942 | updatecity | 1867.2504 | updatecity=70.0823; physical=38.4047; advance=3.7542; prepare=2.1184; NativeCityClock.AccountingPrefix=1.8766; NativeFoodOwnership.AfterNativeTick=0.5242 |
| street | 52429 | 121.1725 | physical | 1720.3656 | physical=98.0953; updatecity=13.4878; advance=7.2504; prepare=1.7177; NativeCityClock.AccountingPrefix=0.344; inspector=0.1299 |
| street | 52430 | 51.0121 | physical | 1773.5216 | physical=30.6546; updatecity=13.235; advance=3.6778; prepare=2.9366; NativeCityClock.AccountingPrefix=0.3134; inspector=0.049 |
| street | 52431 | 56.1135 | physical | 2397.1273 | physical=30.3069; prepare=11.1194; updatecity=7.9807; advance=6.0135; NativeCityClock.AccountingPrefix=0.2866; PopulationRuntime.Update=0.1464 |
| street | 52434 | 276.741 | physical | 6621.1226 | physical=259.5299; updatecity=11.3692; advance=2.0356; prepare=1.487; PopulationRuntime.LateUpdate=1.1479; NativeCityClock.AccountingPrefix=0.7157 |
| street | 52435 | 387.9724 | physical | 4650.0313 | physical=230.6636; updatecity=142.6508; prepare=11.9835; advance=2.2011; NativeCityClock.AccountingPrefix=0.2565; inspector=0.0488 |
| street | 52436 | 34.5212 | physical | 1228.8399 | physical=21.6533; advance=6.0212; updatecity=5.0079; prepare=1.4084; NativeCityClock.AccountingPrefix=0.2223; inspector=0.0522 |
| street | 52476 | 50.9713 | updatecity | 762.5587 | updatecity=44.8384; physical=2.51; advance=2.3351; prepare=1.2254; NativeCityClock.AccountingPrefix=0.0166; inspector=0.0145 |
| street | 52483 | 63.4459 | physical | 1268.192 | physical=46.7059; updatecity=8.6435; advance=5.7246; prepare=1.3663; PopulationRuntime.Update=0.5397; NativeCityClock.AccountingPrefix=0.2439 |
| street | 52492 | 43.0119 | physical | 1778.4066 | physical=26.6258; updatecity=9.15; advance=3.6701; prepare=2.6093; PopulationRuntime.Update=0.5413; NativeCityClock.AccountingPrefix=0.2636 |
| street | 52493 | 43.2188 | physical | 1520.4689 | physical=23.0833; updatecity=12.8507; advance=5.0265; prepare=1.6847; NativeCityClock.AccountingPrefix=0.2999; PopulationRuntime.Update=0.1004 |
| street | 52494 | 30.2504 | physical | 1388.585 | physical=21.7624; updatecity=5.6189; NativeCityClock.AccountingPrefix=1.3665; advance=0.6922; prepare=0.351; ResidentInspector.MouseRunPrefix=0.1558 |
| street | 52495 | 34.0295 | physical | 1700.6031 | physical=21.0389; updatecity=8.8824; advance=1.9462; prepare=1.5728; NativeCityClock.AccountingPrefix=0.2988; PopulationRuntime.Update=0.0723 |
| street | 52497 | 55.7549 | physical | 1367.5902 | physical=42.1676; updatecity=8.6284; advance=2.1036; NativeCityClock.AccountingPrefix=1.3654; prepare=0.7908; ResidentInspector.MouseRunPrefix=0.3241 |
| street | 52498 | 40.9364 | physical | 1318.2603 | physical=27.3606; updatecity=8.4882; advance=2.7977; prepare=1.6553; NativeCityClock.AccountingPrefix=0.3942; ResidentInspector.UiRaycastPrefix=0.0674 |
| street | 52499 | 34.656 | physical | 630.2087 | physical=20.695; updatecity=8.391; advance=3.8183; prepare=1.3035; NativeCityClock.AccountingPrefix=0.2655; inspector=0.0418 |
| street | 52515 | 30.1553 | physical | 1047.1921 | physical=14.7797; updatecity=9.0266; prepare=2.627; NativeCityClock.AccountingPrefix=2.0804; advance=1.2193; ResidentInspector.MouseRunPrefix=0.1114 |
| street | 52516 | 38.4923 | physical | 709.1675 | physical=27.0612; advance=5.8913; updatecity=4.4099; prepare=1.0553; NativeCityClock.AccountingPrefix=0.0228; inspector=0.0175 |
| street | 52522 | 44.409 | physical | 531.776 | physical=39.4107; updatecity=2.4504; prepare=1.5015; advance=0.9517; inspector=0.0481; NativeCityClock.AccountingPrefix=0.0166 |
| street | 52663 | 37.4035 | updatecity | 564.903 | updatecity=29.1177; physical=6.8022; prepare=1.103; advance=0.244; NativeCityClock.AccountingPrefix=0.0826; inspector=0.0189 |
| street | 52674 | 41.9129 | physical | 799.7057 | physical=23.4792; advance=8.3144; updatecity=6.0931; prepare=3.557; NativeCityClock.AccountingPrefix=0.266; inspector=0.0843 |
| street | 52680 | 41.641 | physical | 457.5378 | physical=37.3549; updatecity=1.747; advance=1.2225; prepare=1.1254; NativeCityClock.AccountingPrefix=0.1049; inspector=0.0191 |
| street | 52720 | 34.3468 | physical | 571.7182 | physical=24.7333; updatecity=5.0432; advance=2.8018; NativeCityClock.AccountingPrefix=1.0587; prepare=0.3098; ResidentInspector.MouseRunPrefix=0.1134 |
| street | 52723 | 35.2148 | updatecity | 608.9113 | updatecity=31.511; advance=1.942; prepare=1.0661; physical=0.5978; NativeCityClock.AccountingPrefix=0.0638; ResidentInspector.UiRaycastPrefix=0.0102 |
| street | 52726 | 35.8368 | physical | 409.0721 | physical=15.8381; updatecity=12.4446; advance=4.1167; prepare=2.8966; NativeCityClock.AccountingPrefix=0.3668; PopulationRuntime.Update=0.0431 |
| street | 52728 | 46.1448 | physical | 547.9342 | physical=36.1683; updatecity=6.4539; prepare=2.1725; advance=0.8296; NativeCityClock.AccountingPrefix=0.3248; PopulationRuntime.Update=0.0678 |
| street | 52733 | 32.7313 | updatecity | 636.6769 | updatecity=27.4991; advance=2.6117; prepare=1.5069; physical=1.0023; PopulationRuntime.Update=0.0379; NativeCityClock.AccountingPrefix=0.0243 |
| street | 52735 | 82.4642 | physical | 967.7 | physical=66.3581; updatecity=8.3544; advance=3.3129; prepare=2.3887; NativeCityClock.AccountingPrefix=1.4695; ResidentInspector.MouseRunPrefix=0.1689 |
| street | 52741 | 38.6103 | physical | 1838.4415 | physical=24.6646; updatecity=7.2661; advance=2.5373; NativeCityClock.AccountingPrefix=1.8464; prepare=1.3497; ResidentInspector.UiRaycastPrefix=0.6076 |
| street | 52742 | 30.539 | physical | 1005.9267 | physical=17.702; updatecity=5.8487; advance=5.0164; prepare=1.4736; NativeCityClock.AccountingPrefix=0.2868; inspector=0.0515 |
| street | 52753 | 54.1311 | physical | 1488.4953 | physical=28.0818; advance=13.8855; updatecity=10.2398; prepare=1.462; NativeCityClock.AccountingPrefix=0.2571; inspector=0.0783 |
| street | 52766 | 37.4511 | physical | 981.2701 | physical=30.0869; updatecity=3.3287; prepare=2.696; NativeCityClock.AccountingPrefix=0.409; advance=0.4034; inspector=0.3042 |
| street | 52769 | 59.0912 | updatecity | 1073.7471 | updatecity=57.5424; prepare=1.0562; advance=0.2709; physical=0.1737; NativeCityClock.AccountingPrefix=0.0153; ResidentInspector.UiRaycastPrefix=0.0102 |
| street | 52785 | 31.3038 | physical | 1076.2269 | physical=15.8516; updatecity=7.1967; advance=5.4199; NativeCityClock.AccountingPrefix=2.2665; prepare=0.4063; inspector=0.0473 |
| street | 52788 | 32.5683 | physical | 1348.2906 | physical=23.6001; advance=3.592; updatecity=3.4336; inspector=1.1985; prepare=0.3293; NativeCityClock.AccountingPrefix=0.2948 |
| street | 52803 | 30.1877 | physical | 429.8891 | physical=22.4396; updatecity=6.2984; advance=0.5145; prepare=0.3256; NativeCityClock.AccountingPrefix=0.2578; ResidentInspector.MouseRunPrefix=0.1535 |
| street | 52809 | 78.2395 | physical | 1515.0675 | physical=46.7799; updatecity=27.5099; advance=2.3459; prepare=1.1518; NativeCityClock.AccountingPrefix=0.2598; inspector=0.0552 |
| street | 52810 | 88.1387 | physical | 1100.8403 | physical=65.2234; updatecity=13.9137; advance=7.1642; prepare=1.3125; NativeCityClock.AccountingPrefix=0.2608; inspector=0.0918 |

Every exported mod-work spike is listed (126 rows: day 16, night 2, street 108). The compact JSON keeps three largest spike examples per window; complete frame/component lists are in this table and the hash-indexed raw end replies. The dominant self component attributes the measured mod work; it does not establish an allocating method or GC cause. `pc gc stop` exposes aggregate GC pause counts/percentiles but no per-pause Unity frame IDs; those pauses cannot be assigned exact frames from its response. Spikes are reported, not chased. Whole-frame spikes remain separate from mod-work; complete raw whole-frame records are in the stage.

gc-day: coverage.complete=True, validity={"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}; save_activity={"attempts":10081,"allowed":0,"refused":10081,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}; NativeCityClock final={"policy":"minute-epoch-drain-v1","configuredCalendarFactor":0.3333333333333333,"nativeMinute":3960.9490726590157,"logicalMinute":3960.9490726590157,"caughtUpToTarget":true,"lagMinutes":0.0,"maxLagMinutes":0.7866191864018219,"supportedMaxLagMinutes":1.0,"heldOrdinaryTicks":3497,"heldSkipTicks":0,"boundaryClips":180,"withheldGameMinutes":123.5844794174991,"waitingSeconds":0.0,"maxRecoverySeconds":12.1572111,"refused":false,"pacingAffected":true,"stoppedAfterAccountingFailure":false,"historicalClockMisses":0}.
gc-night: coverage.complete=True, validity={"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}; save_activity={"attempts":16722,"allowed":0,"refused":16722,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}; NativeCityClock final={"policy":"minute-epoch-drain-v1","configuredCalendarFactor":0.3333333333333333,"nativeMinute":4562.857531547546,"logicalMinute":4562.857531547546,"caughtUpToTarget":true,"lagMinutes":0.0,"maxLagMinutes":1.0,"supportedMaxLagMinutes":1.0,"heldOrdinaryTicks":8623,"heldSkipTicks":0,"boundaryClips":782,"withheldGameMinutes":1229.0320480729472,"waitingSeconds":0.0,"maxRecoverySeconds":15.5762799,"refused":false,"pacingAffected":true,"stoppedAfterAccountingFailure":false,"historicalClockMisses":0}.
street: coverage.complete=True, validity={"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}; save_activity={"attempts":2591,"allowed":0,"refused":2591,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}; NativeCityClock final={"policy":"minute-epoch-drain-v1","configuredCalendarFactor":0.3333333333333333,"nativeMinute":5280.111106872559,"logicalMinute":5280.0,"caughtUpToTarget":false,"lagMinutes":0.11110687255859375,"maxLagMinutes":1.0,"supportedMaxLagMinutes":1.0,"heldOrdinaryTicks":13861,"heldSkipTicks":0,"boundaryClips":1500,"withheldGameMinutes":2384.727051503306,"waitingSeconds":433.8469514,"maxRecoverySeconds":41.3219146,"refused":false,"pacingAffected":true,"stoppedAfterAccountingFailure":false,"historicalClockMisses":0}.

### Host load and memory pressure at each boundary

| Window | Boundary UTC | uptime load | memory_pressure -Q |
| --- | --- | --- | --- |
| gc-day | 2026-11-14T22:01:18Z | 23:01  up 1 day,  4:39, 1 user, load averages: 13.12 7.96 5.92 | System-wide memory free percentage: 46% |
| gc-day | 2026-11-14T22:11:22Z | 23:11  up 1 day,  4:49, 1 user, load averages: 12.17 14.52 11.64 | System-wide memory free percentage: 78% |
| gc-night | 2026-11-14T22:15:18Z | 23:15  up 1 day,  4:53, 1 user, load averages: 22.75 16.79 13.19 | System-wide memory free percentage: 71% |
| gc-night | 2026-11-14T22:25:21Z | 23:25  up 1 day,  5:03, 1 user, load averages: 3.98 8.02 10.21 | System-wide memory free percentage: 76% |
| street | 2026-11-14T22:29:46Z | 23:29  up 1 day,  5:07, 1 user, load averages: 8.10 6.44 8.79 | System-wide memory free percentage: 77% |
| street | 2026-11-14T22:39:53Z | 23:39  up 1 day,  5:18, 1 user, load averages: 90.45 61.07 35.49 | System-wide memory free percentage: 45% |

Street load rose from 8.10/6.44/8.79 to 90.45/61.07/35.49, and memory free fell 77%→45%. This severe shared-host load qualifies the street regression; it is not isolated production-code attribution. All 108 street spikes were exported: 89 physical, 17 UpdateCity self, 1 advance and 1 prepare dominant.

`vm_stat` snapshots are retained with each boundary. Another harness session may have been building/testing; no other session was inspected, stopped or changed. These loads qualify noise; they do not prove which process caused it.

### Traffic observations and Player.log excerpts

GC sampler was stopped for the street window. Nine look_at pans at x=197/215/233 and z=0/12 completed; player stayed x=215,z=0. Headless rendering cannot show whether traffic/arrivals look identical to 14j. Periodic population logs and route-progress counters are retained as operational evidence, not forecast equality proof.

| Last periodic street snapshot | pedestrians | cars | acquired | released | journeyIndexMismatches | journeyIndexFallback |
| --- | --- | --- | --- | --- | --- | --- |
| 14j | 18 | 0 | 163 | 145 | 0 | False |
| 14k | 7 | 0 | 29 | 22 | 0 | False |

These cumulative physical counts show ongoing acquisition/release, not native shop-arrival or forecast equality. The snapshots occur at different achieved cadences and are not a paired behavioural proof.

```text
[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.
[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 15:30.
[Mod:FoodTrucks] Test bridge: run_until day 2 15:30 reached at day 2 15:30; paused, speed 1.
[Mod:FoodTrucks] Test bridge: console 'pc gc start' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (2 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 01:00.
[Mod:FoodTrucks] Test bridge: run_until day 3 01:00 reached at day 3 01:00; paused, speed 1.
[Mod:FoodTrucks] Test bridge: console 'pc gc start' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (2 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (2 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 15:30.
[Mod:FoodTrucks] Test bridge: run_until day 3 15:30 reached at day 3 15:30; paused, speed 1.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
```

Explicit speed=1 replies are retained before every window. No RLO toggle occurred. The first gc population read immediately after native load said `Warning: [Persistent City] gc population refused: no active city.` The next read confirmed 3000/1000/2000 after asynchronous activation; no window was opened before confirmation. No ops_load_* failure or runtime stop occurred.

### NOT RUN grouped and tooling gaps

- **Look stability (#266 check 4):** excluded, completed in 14j as far as tooling allows.
- **GR-5 60 fps street/busy-shop arms:** excluded; Mac about 5 fps at this population; PC throwaway time/perf/modwork fix agent-rig #90 not merged for this request. No PC contact.
- **GR-5 save latency:** excluded, owner decision pending; SaveCostDiagnostics off, no save profiling.
- **#262 checks 4–5 and #246 check 4 full proof:** excluded, Rig bot fixture/observation gaps.
- **Everything else:** outside the explicitly requested A checks; no release, Workshop, version/changelog, production, schema or price/household-weight change.

Tooling gaps within performed checks: exact inclusive UpdateCity p95 under nested self-time instrumentation; GC-pause frame IDs absent from the successful `pc gc stop` response; rendered traffic/arrival/forecast equality in the required headless process; no finer advance allocation sub-segments or commit-stamped build log. No op was invoked to bypass these limits. Invoked rig ops had no errors; the startup population warning is quoted above.

### Cleanup and offline validation

Stopped only owned PID 74916 normally. Restored the full PersistentCity folder and exact receipt from the printed backup, stopped and leased; preserved staged candidate in `<stage>/staged-candidate-restored-out`. Restored isolated preferences/config, plist, PlayerSettings and original absent paths. Verified 35 original file hashes, 3 absent paths, exact PersistentCity file set and 53 RLO hashes; mismatches=[]. AgentRigOps unchanged. install.json SHA-256 `dea36ce03586cc53d3b2c7e5cd0fda6d9a54e61c670a21d0676ad18feee54a95`. Original autosave settings restored; process suppression ends at stop. Disposable fixture unchanged; no new save. Final running=False, lease held=False, pending=0; owned PID and caffeinate gone.

Build passed. Compact JSON parses and is under 20 KB; docs-links, markdown-ban and diff checks recorded with this docs-only change. No test sources changed, no focused classes or full/regression suite. Evidence committed and pushed; PR remains unmerged, no reviewers. The isolated worktree is removed after opening the PR.

Refs #268

## Rig results — 14 November 2026 l, Mac (GR-5 GC/street recheck after #270 at da8b495b)

**RESULT: DONE. Counts: 0 passed / 1 partial / 3 failed / 4 NOT RUN groups.** Both GC windows miss #270’s allocation/count/pause targets. Street: FAIL; look stability: partial. In-game proof gates no merge; this evidence PR remains unmerged.

Role `rig_test`, where `mac`, isolated `agentmac`, owner `persistent-city-rig-2026-11-14l-mac`. `<stage>` = `<home>/HART-Builds/persistent-city-rig-2026-11-14l-mac`. [Compact GC/frame evidence](perf/gr5-2026-11-14l-mac.json): 17,571 bytes, SHA-256 `4b6ec80d655663f670f56cb62fbbbffd0d8e6ce38d351e76c7ceb7648e396abc`. Raw replies, full reports, Player logs, scripts, images and pre-stage backups stay there. Report copy: `<home>/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-14l-mac.md`.

Raw evidence index: `<stage>/raw-evidence-manifest.json`, SHA-256 `80174cbec02f22776bdde0d8052602b30eb3f3295575c9a545e00cff91fbf806`; pre-stage manifest and restore verification included.

### Build and rig

| Item | Evidence |
| --- | --- |
| Candidate | `da8b495b6a4b6092a8d18107507044cd2aa1bd94`; initial fetched tip agrees; docs-only #271 merged during the run, final fetched tip `73b6fdbc09878bf7d995d067086fb0fdfa802cc8`. #270 and #268 included. |
| Worktree / branch | New `rig-2026-11-14l-mac-evidence` / `codex/mac-rig-2026-11-14l`; isolated worktree used for build and evidence; original checkout unchanged. |
| Official agent-rig HEAD | `17ff9f86ef07a8c071a90886fa76ed2e98f56c57`; clean before fetch / ff-only pull; already current. No rig edit, commit, build or update. |
| Live AgentRigOps SHA-256 | `d3188b0e837188aec8be85535846953170da99807ad22914ccf4a3c38d77ed3c`; live pre-start hash, loaded health and cleanup agree. |
| Build | `PATH=/opt/homebrew/bin:$PATH`; `DOTNET_ROOT` from Homebrew dotnet; `python3 tools/build.py --game-managed "<home>/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`; exit 0. No RLO or game DLL packaged. |
| PersistentCity DLL SHA-256 | `888f49e4fb0a622896927d7efa85ba6b9eab0b7095370f29d9b467c678d6b239`; package and staged copy agree. |
| Loaded build | Health module ID `e487d483-9a39-4fb1-ba64-bcfca5c8fbdc` occurs once in candidate DLL. Receipt-validated starts accepted staged hashes. No commit-stamped build line exists; activation quoted below. |
| Stage | `python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package`; stopped and leased; 28 scoped receipt entries refreshed. Backup `<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T231443Z`. |
| Starts | Headless rig-test PID 10080: day, night, street in one process. Separate background graphics rig-test PID 26377 for looks. No load failure or forced stop. |
| RLO | Player.log: Real Life Overhaul 1.0.6 initialised. living_population absent/live=true; social_media=1/true; social_media_notices=0/false. No option toggle or RLO file edit. |
| Autosave / save cost | Original native 5 minutes / 3 saves, prevent_autosave=false. #70 process guard suppressed before/after every window and after each load; all measured allowed saves / native saving frames = 0. SaveCostDiagnostics requested=false; native profiler installed=false. No manual save or latency measurement. |
| Lease / resources | Free at inspection; acquired 2026-11-14T23:13:51.461922Z. Initial memory free 81%. Every measurement start load <=8; zero load-wait seconds. No PC contact, takeover, other-session intervention, bot message or macOS setting change. |

Printed rollback, path-sanitized with `<home>` (game stopped, lease held):

```sh
rm -rf "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T231443Z/mod" "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261114T231443Z/install.json" "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

Cleanup used the equivalent move-aside/copy-back form, retaining the staged candidate in `<stage>/staged-candidate-restored-out`. The complete original mod folder and exact receipt were restored, then hashes verified. No receipt was hand-edited.

### Per-check results and Expected lines

- **1. Day 15:30 / 600 s: FAIL. Expected (#270 TARGETS):** total mod allocation per 600 s window at least a third below 14j, gen0 about 5 or fewer, pause p95 at or below 14j and under ~36 ms; appearance/body reuse a large share, spawn_appearance + spawn_pedestrian several times smaller. **Observed:** mod 313.467 MB vs 315.571 MB (0.67% lower; one-third threshold 210.381 MB), gen0 8, pause p95 33.488 ms vs 28.581 ms. Allocation, collection count and no-pause-regression clauses not met. Pause under ~36 ms met. Spawn and reuse detail below.
- **2. Night 01:00 / 600 s after speed 20: FAIL. Expected (#270 TARGETS):** total mod allocation per 600 s window at least a third below 14j, gen0 about 5 or fewer, pause p95 at or below 14j and under ~36 ms; appearance/body reuse a large share, spawn_appearance + spawn_pedestrian several times smaller. **Observed:** mod 166.405 MB vs 180.623 MB (7.87% lower; one-third threshold 120.416 MB), gen0 7, pause p95 36.717 ms vs 32.902 ms. Allocation, collection count and no-pause-regression clauses not met. Pause under ~36 ms not met. Spawn and reuse detail below.
- **3. Headless street / 600 s with pans: FAIL. Expected (#270 TARGETS):** UpdateCity / mod-work p95 under 5 ms, no >=30 ms body-work spike. Observed UpdateCity boundary self / union p95 5.4914 / 5.5548 ms; 10 union spikes and 10 PC phase spikes. The p95 clause is not met. Frame 51811 spends 31.038 ms in physical_unattributed; its body-growth source is unresolved, so the no-body-work-spike clause is unverified. Full frame/component rows below. No weaker threshold substituted.
- **4. Look stability: PARTIAL. Expected (#270 TARGETS):** no visual change: dressed bodies, same look per resident across release/reacquire, no invisible or undressed bodies, none of “Appearance meshes unavailable”, “Empty appearance” or “Merged body ...”. Inspected screenshots and exact tooling outcomes below. No durable resident/reacquire op is advertised, so same-resident stability is unverified. Sparse images cannot prove no invisible body or brief flash.

Idle ticks staying 0 B is not separately exercised; the requested windows are moving speed-1 gameplay, and zero-valued segments are reported without treating them as an idle-tick test.

### GC windows and baselines

Fixture `FT GR5 13f Timing v1`, 1,000 residents / 2,000 visitors / 1,683 places, person-target off, player x=215,z=0. Day Tuesday 15:30; night Wednesday 01:00; street Wednesday 15:30. Speed=1 in windows; speed-20 advances outside. `boehm_total` allocation counter; legacy frame allocation uses heap_used. Incremental=true, max_generation=0; pause means cycle-ending frame excess, not all incremental collector work. MB means decimal bytes.

| Window | frames / dropped | gen0/1/2 | pause p50 / p95 / max ms | heap_after_gc_bytes | mono_heap_bytes | mod / physical / advance MB |
| --- | --- | --- | --- | --- | --- | --- |
| gc-day | 16804 / 0 | 8/0/0 | 26.708/33.488/33.488 | 1221783552 | 1457061888 | 313.467 / 113.771 / 101.657 |
| gc-night | 17026 / 0 | 7/0/0 | 31.653/36.717/36.717 | 1337786368 | 1608056832 | 166.405 / 35.991 / 33.572 |

| Metric | 14b day / night | 14j day / night | 14k day / night (load-contaminated) | 14l day / night |
| --- | --- | --- | --- | --- |
| gen0 | 11 / 8 | 7 / 6 | 6 / 7 | 8 / 7 |
| pause p95 ms | 28.191 / 61.316 | 28.581 / 32.902 | 219.8 / 234.5 | 33.488 / 36.717 |
| mod MB | not retained here | 315.571 / 180.623 | 283.687 / 190.858 | 313.467 / 166.405 |
| physical+advance MB | 1021.587 / 433.642 | 200.927 / 68.086 | 156.411 / 75.158 | 215.427 / 69.563 |
| advance MB | 236.448 / 72.746 | 107.466 / 33.489 | 85.0 / 35.4 | 101.657 / 33.572 |

gc-day: vs 14j mod -0.67%, advance -5.41%, pause p95 +4.907 ms, gen0 +1. Vs load-contaminated 14k mod +10.50%, advance +19.54%, pause p95 -186.325 ms, gen0 +2.
gc-night: vs 14j mod -7.87%, advance +0.25%, pause p95 +3.815 ms, gen0 +1. Vs load-contaminated 14k mod -12.81%, advance -5.25%, pause p95 -197.795 ms, gen0 +0.

All 14b, 14j and 14k baselines are shared-host measurements on the same Mac, not controlled A/B runs. 14k additionally had severe shared-host build/test load, up to load average 90; its deltas are load-contaminated, not controlled code attribution. 14l measured-window boundaries are below; no build or test ran in this session during timing.

### Every allocation segment and sub-segment

| Segment | Day bytes / samples / skipped | Night bytes / samples / skipped |
| --- | --- | --- |
| mod | 313466760 / 16805 / 0 | 166405264 / 17027 / 0 |
| advance | 101656568 / 16711 / 0 | 33572232 / 16947 / 0 |
| physical | 113770760 / 16806 / 0 | 35990768 / 17028 / 0 |
| physical_ambient | 2140880 / 16711 / 0 | 2172208 / 16947 / 0 |
| physical_vehicles | 1038848 / 16711 / 0 | 385936 / 16947 / 0 |
| physical_bindings | 12464 / 525991 / 0 | 15312 / 274981 / 0 |
| physical_vendor_meals | 0 / 16711 / 0 | 0 / 16947 / 0 |
| physical_fade | 0 / 16711 / 0 | 0 / 16947 / 0 |
| physical_focus | 0 / 16711 / 0 | 0 / 16947 / 0 |
| physical_observe | 261200 / 508967 / 0 | 99952 / 257831 / 0 |
| physical_release | 9968 / 329 / 0 | 2480 / 295 / 0 |
| physical_parked | 1969904 / 586 / 0 | 1723584 / 589 / 0 |
| physical_index | 80 / 2191 / 0 | 80 / 2194 / 0 |
| physical_queues | 8960 / 2191 / 0 | 27184 / 2194 / 0 |
| physical_roster | 285456 / 2191 / 0 | 209184 / 2194 / 0 |
| physical_selection | 1400664 / 2768 / 0 | 654064 / 2413 / 0 |
| physical_spawn_pedestrian | 41773560 / 807 / 0 | 667128 / 281 / 0 |
| physical_spawn_appearance | 50000864 / 936 / 0 | 15208928 / 273 / 0 |
| physical_spawn_check | 0 / 624 / 0 | 0 / 182 / 0 |
| physical_spawn_vehicle | 168352 / 82 / 0 | 90496 / 29 / 0 |
| physical_unattributed | 14699560 / 33517 / 0 | 14734232 / 33975 / 0 |
| physical_spawn_randomize | 2802160 / 312 / 0 | 798624 / 91 / 0 |
| physical_spawn_bake | 2775056 / 312 / 0 | 1691520 / 91 / 0 |
| advance_planning | 45550560 / 9461 / 0 | 20019896 / 3989 / 0 |
| advance_events | 53699624 / 21567 / 0 | 11111936 / 19951 / 0 |
| advance_stops | 2406384 / 16711 / 0 | 2440368 / 16947 / 0 |
| advance_other | 0 / 16711 / 0 | 32 / 16947 / 0 |
| physical_spawn_grow | 38248208 / 132 / 0 | 0 / 0 / 0 |
| mod_prelude | 2584608 / 16805 / 0 | 2642128 / 17027 / 0 |
| mod_rlo | 3541904 / 16805 / 0 | 3530080 / 17027 / 0 |
| mod_food | 1279184 / 16805 / 0 | 1279648 / 17027 / 0 |
| mod_optional_stops | 2498192 / 16805 / 0 | 2455680 / 17027 / 0 |
| mod_routes | 75000152 / 16805 / 0 | 74009272 / 17027 / 0 |
| mod_closures | 10365632 / 16711 / 0 | 10120912 / 16947 / 0 |
| mod_hold_replay | 0 / 16711 / 0 | 0 / 16947 / 0 |
| mod_physical_setup | 0 / 13741 / 0 | 0 / 13475 / 0 |
| mod_inspector | 2406384 / 16711 / 0 | 2440864 / 16947 / 0 |
| mod_summary | 361328 / 16429 / 0 | 360880 / 16216 / 0 |
| mod_other | 272 / 16805 / 0 | 576 / 17027 / 0 |

Advance children partition advance; physical children partition physical. randomize, bake and grow overlap spawn parents and must not be added again. mod_* children partition the outer residual. Samples skipped after collections are explicit.

- gc-day: previously unattributed outer residual now 98,039,432 bytes; mod_* segments account for 98,037,656, leaving 1,776 bytes of boundary/counter difference. `mod_routes`=75,000,152 and `mod_closures`=10,365,632 dominate. These boundary names do not identify an allocating method. Appearance + pedestrian spawn = 91.774 MB versus 86.823 MB in 14j; several-fold reduction not met.
- gc-night: previously unattributed outer residual now 96,842,264 bytes; mod_* segments account for 96,840,040, leaving 2,224 bytes of boundary/counter difference. `mod_routes`=74,009,272 and `mod_closures`=10,120,912 dominate. These boundary names do not identify an allocating method. Appearance + pedestrian spawn = 15.876 MB versus 28.495 MB in 14j; several-fold reduction not met.

### Look counters, pool hit rate and retained bodies

| Window | AppearanceBuilds / AppearanceReuses | reuse / (build+reuse) % | last periodic pedestrianLooks |
| --- | --- | --- | --- |
| gc-day | 280 / 29 | 9.39 | 280 built/29 reused/2718 deferred/32 idle/102 owned/8 adopted/290 baked/45 bake sizes/1678 KB bake scratch/108 grown/15 trimmed/224 fitted/162 waited/2.4 ms build |
| gc-night | 91 / 95 | 51.08 | 91 built/95 reused/1574 deferred/33 idle/33 owned/124 adopted/556 baked/44 bake sizes/1634 KB bake scratch/0 grown/91 trimmed/0 fitted/89 waited/4.3 ms build |
| street | 105 / 34 | 24.46 | 105 built/34 reused/401 deferred/32 idle/53 owned/86 adopted/800 baked/45 bake sizes/1678 KB bake scratch/26 grown/59 trimmed/0 fitted/129 waited/6.9 ms build |

These are last periodic cumulative snapshots, not exact start-to-stop counter deltas. 14j snapshots: day 298/16 (5.10%), night 92/1 (1.08%), street 148/7 (4.52%). Day reuse is 9.39%, still a small share: that target is not met. Night reuse is 51.08%, a majority: that clause is met. No arbitrary numeric threshold replaces the target. Exact sampler randomize/bake/grow sample counts are in the allocation table.

### Timing, top GC frames and every >=30 ms mod-work spike

| Window | seconds / frames / achieved fps | UpdateCity self p95 / max ms | mod-work p95 / max ms / spikes | whole-frame p95 / max ms / spikes | MaximumLagMinutes |
| --- | --- | --- | --- | --- | --- |
| gc-day | 600.603 / 16811 / 27.99 | 0.3146 / 50.3308 | 3.4712 / 61.9973 / 2 | 42.8955 / 172.1595 / 16565 | 0.8821749687199372 |
| gc-night | 600.599 / 17035 / 28.36 | 0.3677 / 66.8887 | 2.6328 / 80.9280 / 1 | 37.8688 / 516.5799 / 16818 | 0.8821749687199372 |
| street | 601.424 / 4573 / 7.60 | 5.4914 / 359.4385 | 5.5548 / 359.4729 / 10 | 221.2817 / 965.5998 / 4541 | 1.0 |

GC windows use nested updatecity/advance/prepare/physical/inspector rig components: UpdateCity is self time, not inclusive p95. Street re-arms with only the UpdateCity component, as in 14j, plus automatic entry-point hooks; its boundary self time excludes any nested automatic hooks. Exact inclusive UpdateCity p95 is not exported. Total per-frame mod-work is the union of all timed scopes, bounds UpdateCity, and is the strict <5 ms gate used here. PC’s phase recorder separately supplies inner attribution; its Update-only values and the all-entry union have different scope. Whole-frame timings are separate.

Shared-host baselines: 14j street boundary / union p95 3.7732 / 3.8197 ms, three spikes, 8.13 fps. 14l deltas +1.7182 / +1.7351 ms. 14b 5.5170 / 5.5824 ms, four spikes, 5.61 fps. 14k had 108 street spikes under severe load contamination; the comparison is not isolated evidence of code effects.

**gc-day: every exported union spike** (2 total / 2 listed), matched to PC phase records by Unity frame:

| Frame | union ms | rig dominant / top self components ms | PC mod / phase ms | PC detail ms / gc phase | whole-frame ms |
| --- | --- | --- | --- | --- | --- |
| 3757 | 61.9973 | updatecity; updatecity=50.3308, advance=6.3522, physical=5.037, prepare=0.2374, NativeCityClock.AccountingPrefix=0.0113, PopulationRuntime.Update=0.0075 | 61.947 / physical_unattributed=26.554 | physical_spawn_grow=2.167 / none | 94.0142 |
| 16301 | 33.6589 | physical; physical=33.3021, updatecity=0.2827, prepare=0.0183, advance=0.0137, NativeCityClock.AccountingPrefix=0.011, inspector=0.0094 | 33.614 / physical_spawn_pedestrian=32.474 | physical_spawn_grow=32.462 / physical_spawn_pedestrian | 64.3958 |

**gc-night: every exported union spike** (1 total / 1 listed), matched to PC phase records by Unity frame:

| Frame | union ms | rig dominant / top self components ms | PC mod / phase ms | PC detail ms / gc phase | whole-frame ms |
| --- | --- | --- | --- | --- | --- |
| 26859 | 80.928 | updatecity; updatecity=66.8887, physical=7.25, advance=6.5201, prepare=0.2278, NativeCityClock.AccountingPrefix=0.0115, PopulationRuntime.Update=0.0089 | 80.876 / physical_unattributed=31.984 | physical_spawn_bake=0.512 / none | 128.3859 |

**street: every exported union spike** (10 total / 10 listed), matched to PC phase records by Unity frame:

| Frame | union ms | rig dominant / top self components ms | PC mod / phase ms | PC detail ms / gc phase | whole-frame ms |
| --- | --- | --- | --- | --- | --- |
| 51716 | 359.4729 | updatecity; updatecity=359.4385, NativeCityClock.AccountingPrefix=0.0141, ResidentInspector.UiRaycastPrefix=0.0073, PopulationRuntime.Update=0.0069, NativeTravelPace.ObserveTickPostfix=0.0015, ResidentInspector.MouseRunPrefix=0.0014 | 359.442 / mod_routes=359.288 | none=0 / mod_routes | 409.9833 |
| 51811 | 72.8058 | updatecity; updatecity=72.7426, PopulationRuntime.LateUpdate=0.0329, NativeCityClock.AccountingPrefix=0.0114, PopulationRuntime.Update=0.0074, ResidentInspector.UiRaycastPrefix=0.0058, ResidentInspector.MouseRunPrefix=0.0013 | 72.747 / physical_unattributed=31.038 | physical_spawn_bake=0.914 / none | 95.122 |
| 54291 | 35.8596 | updatecity; updatecity=35.7844, PopulationRuntime.Update=0.0242, PopulationRuntime.LateUpdate=0.0227, NativeCityClock.AccountingPrefix=0.0139, ResidentInspector.UiRaycastPrefix=0.0083, ResidentInspector.MouseRunPrefix=0.0015 | 35.805 / mod_summary=30.356 | none=0 / none | 342.5705 |
| 54636 | 31.6301 | updatecity; updatecity=31.3583, NativeCityClock.AccountingPrefix=0.137, PopulationRuntime.LateUpdate=0.0447, ResidentInspector.MouseRunPrefix=0.026, PopulationRuntime.Update=0.0218, ResidentInspector.UiRaycastPrefix=0.0183 | 31.375 / physical_parked=12.936 | none=0 / none | 462.7409 |
| 55224 | 35.8843 | updatecity; updatecity=35.621, NativeCityClock.AccountingPrefix=0.1496, PopulationRuntime.Update=0.056, PopulationRuntime.LateUpdate=0.0332, ResidentInspector.UiRaycastPrefix=0.008, NativeTravelPace.ObserveTickPostfix=0.008 | 35.667 / physical_roster=9.572 | none=0 / none | 364.8978 |
| 55240 | 31.9082 | updatecity; updatecity=31.8433, PopulationRuntime.LateUpdate=0.0229, PopulationRuntime.Update=0.0144, NativeCityClock.AccountingPrefix=0.0135, ResidentInspector.UiRaycastPrefix=0.007, ResidentInspector.MouseRunPrefix=0.0019 | 31.848 / mod_summary=22.616 | none=0 / none | 228.7934 |
| 55616 | 41.757 | updatecity; updatecity=41.1434, NativeCityClock.AccountingPrefix=0.4363, PopulationRuntime.Update=0.1018, ResidentInspector.MouseRunPrefix=0.0321, PopulationRuntime.LateUpdate=0.0268, ResidentInspector.UiRaycastPrefix=0.0082 | 41.202 / mod_optional_stops=10.234 | none=0 / none | 452.6298 |
| 55618 | 41.5307 | updatecity; updatecity=40.1885, PopulationRuntime.Update=0.4938, NativeCityClock.AccountingPrefix=0.4629, PopulationRuntime.LateUpdate=0.2306, ResidentInspector.UiRaycastPrefix=0.0677, ResidentInspector.MouseRunPrefix=0.0545 | 40.622 / physical_parked=11.948 | none=0 / none | 418.2179 |
| 55620 | 32.9644 | updatecity; updatecity=32.3469, NativeCityClock.AccountingPrefix=0.2878, PopulationRuntime.Update=0.2178, PopulationRuntime.LateUpdate=0.0318, ResidentInspector.MouseRunPrefix=0.0295, ResidentInspector.UiRaycastPrefix=0.0253 | 32.48 / physical_parked=6.898 | none=0 / none | 334.1804 |
| 56085 | 42.5574 | updatecity; updatecity=42.1942, PopulationRuntime.Update=0.2618, NativeCityClock.AccountingPrefix=0.0598, PopulationRuntime.LateUpdate=0.0276, ResidentInspector.UiRaycastPrefix=0.0065, NativeTravelPace.ObserveTickPostfix=0.003 | 42.255 / physical_index=21.736 | none=0 / none | 432.386 |

**gc-day: full 16-row GC top-frame report**, highest PC Update work first:

| Frame | mod ms | phase / ms | detail / ms | gc phase / collections | alloc phase / bytes | total alloc bytes |
| --- | --- | --- | --- | --- | --- | --- |
| 3757 | 61.947 | physical_unattributed / 26.554 | physical_spawn_grow / 2.167 | none / 0 | physical_unattributed / 12279624 | 25317760 |
| 16301 | 33.614 | physical_spawn_pedestrian / 32.474 | physical_spawn_grow / 32.462 | physical_spawn_pedestrian / 1 | physical_spawn_pedestrian / 289680 | 292704 |
| 3760 | 9.896 | physical_vehicles / 4.051 | none / 0 | none / 0 | advance_planning / 764352 | 841600 |
| 7419 | 7.56 | physical_selection / 4.124 | none / 0 | none / 0 | physical_selection / 157760 | 168912 |
| 4549 | 7.29 | physical_spawn_pedestrian / 6.833 | physical_spawn_grow / 6.824 | none / 0 | physical_spawn_pedestrian / 289680 | 292128 |
| 16493 | 7.004 | advance_planning / 3.086 | physical_spawn_bake / 0.57 | none / 0 | physical_spawn_appearance / 152048 | 179312 |
| 14653 | 6.686 | advance_planning / 2.39 | physical_spawn_bake / 0.5 | none / 0 | physical_spawn_appearance / 146080 | 180320 |
| 18786 | 6.65 | advance_planning / 2.084 | physical_spawn_bake / 0.49 | none / 0 | physical_spawn_appearance / 176880 | 206064 |
| 12030 | 6.637 | physical_vehicles / 3.627 | none / 0 | none / 0 | advance_planning / 2656 | 5088 |
| 9842 | 6.594 | advance_planning / 3.02 | physical_spawn_bake / 0.742 | none / 0 | physical_spawn_appearance / 136224 | 147952 |
| 16269 | 6.58 | physical_spawn_appearance / 4.416 | physical_spawn_bake / 0.499 | none / 0 | physical_spawn_appearance / 136960 | 161712 |
| 15367 | 6.51 | advance_planning / 3.291 | physical_spawn_bake / 0.505 | none / 0 | physical_spawn_appearance / 150720 | 167552 |
| 6260 | 6.485 | advance_planning / 2.571 | physical_spawn_bake / 0.548 | none / 0 | physical_spawn_appearance / 224240 | 318912 |
| 18664 | 6.339 | advance_planning / 1.753 | physical_spawn_bake / 0.552 | none / 0 | physical_spawn_appearance / 151328 | 165392 |
| 17220 | 6.33 | advance_planning / 1.73 | physical_spawn_bake / 0.532 | none / 0 | physical_spawn_appearance / 137696 | 166736 |
| 13948 | 6.327 | advance_planning / 3.266 | physical_spawn_bake / 0.718 | none / 0 | physical_spawn_appearance / 134480 | 148528 |

**gc-night: full 16-row GC top-frame report**, highest PC Update work first:

| Frame | mod ms | phase / ms | detail / ms | gc phase / collections | alloc phase / bytes | total alloc bytes |
| --- | --- | --- | --- | --- | --- | --- |
| 26859 | 80.876 | physical_unattributed / 31.984 | physical_spawn_bake / 0.512 | none / 0 | physical_unattributed / 12276280 | 25575168 |
| 26954 | 15.098 | physical_spawn_pedestrian / 10.973 | none / 0 | none / 0 | physical_selection / 1856 | 5568 |
| 27320 | 14.335 | advance_events / 12.129 | none / 0 | none / 0 | advance_events / 218208 | 228368 |
| 27516 | 12.609 | physical_release / 10.336 | none / 0 | none / 0 | advance_planning / 7392 | 11440 |
| 27273 | 11.852 | physical_spawn_pedestrian / 5.354 | none / 0 | none / 0 | advance_events / 4352 | 11072 |
| 27470 | 11.31 | physical_spawn_appearance / 6.494 | physical_spawn_bake / 1.05 | none / 0 | physical_spawn_appearance / 165296 | 176096 |
| 26938 | 10.893 | physical_spawn_pedestrian / 6.502 | none / 0 | none / 0 | advance_events / 8992 | 15024 |
| 27015 | 10.625 | physical_spawn_pedestrian / 7.488 | none / 0 | none / 0 | advance_planning / 13792 | 21104 |
| 27023 | 10.463 | physical_spawn_pedestrian / 6.468 | none / 0 | none / 0 | physical_selection / 14064 | 38400 |
| 38964 | 10.341 | advance_planning / 10.029 | none / 0 | none / 0 | advance_planning / 6736 | 10224 |
| 26946 | 9.048 | physical_spawn_pedestrian / 5.08 | none / 0 | none / 0 | advance_planning / 2928 | 8496 |
| 39574 | 8.58 | advance_planning / 8.038 | none / 0 | none / 0 | advance_planning / 16240 | 21440 |
| 27825 | 7.712 | advance_planning / 2.512 | physical_spawn_bake / 0.534 | none / 0 | physical_spawn_appearance / 198128 | 203888 |
| 28321 | 7.61 | physical_spawn_appearance / 3.264 | physical_spawn_bake / 0.542 | none / 0 | physical_spawn_appearance / 160768 | 217968 |
| 28603 | 7.531 | advance_planning / 2.616 | physical_spawn_bake / 0.49 | none / 0 | physical_spawn_appearance / 149248 | 152352 |
| 27497 | 7.514 | physical_spawn_appearance / 3.306 | physical_spawn_bake / 0.658 | none / 0 | physical_spawn_appearance / 223600 | 234352 |

PC phase recorder: day/night/street spikes and counts retained without chasing them. A collection ending inside a phase is correlation, not proof that the phase caused the collection. GC pause rows still have no per-pause Unity frame IDs; top mod-work frames and their `gc_phase` are exported.

gc-day: coverage.complete=True, validity=`{"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}`; save_activity=`{"attempts":10816,"allowed":0,"refused":10816,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}`; clock final lag=0.022049903869628906, caughtUpToTarget=False, refused=False, stoppedAfterAccountingFailure=False, historicalClockMisses=0.
gc-night: coverage.complete=True, validity=`{"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}`; save_activity=`{"attempts":17046,"allowed":0,"refused":17046,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}`; clock final lag=0.0, caughtUpToTarget=True, refused=False, stoppedAfterAccountingFailure=False, historicalClockMisses=0.
street: coverage.complete=True, validity=`{"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}`; save_activity=`{"attempts":4582,"allowed":0,"refused":4582,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}`; clock final lag=0.09999465942382812, caughtUpToTarget=False, refused=False, stoppedAfterAccountingFailure=False, historicalClockMisses=0.

### Host load and memory at every window boundary

| Window | Boundary UTC | uptime | memory_pressure -Q |
| --- | --- | --- | --- |
| gc-day start | 2026-11-14T23:16:32Z | 0:16  up 1 day,  5:54, 1 user, load averages: 5.32 5.82 9.68 | System-wide memory free percentage: 75% |
| gc-day end | 2026-11-14T23:26:33Z | 0:26  up 1 day,  6:04, 1 user, load averages: 2.90 3.04 6.07 | System-wide memory free percentage: 75% |
| gc-night start | 2026-11-14T23:30:18Z | 0:30  up 1 day,  6:08, 1 user, load averages: 3.50 3.22 5.44 | System-wide memory free percentage: 66% |
| gc-night end | 2026-11-14T23:40:20Z | 0:40  up 1 day,  6:18, 1 user, load averages: 4.35 4.43 5.03 | System-wide memory free percentage: 58% |
| street start | 2026-11-14T23:45:06Z | 0:45  up 1 day,  6:23, 1 user, load averages: 4.53 4.73 5.02 | System-wide memory free percentage: 71% |
| street end | 2026-11-14T23:55:08Z | 0:55  up 1 day,  6:33, 1 user, load averages: 6.35 7.93 6.75 | System-wide memory free percentage: 70% |

At each start, the single foreground load loop inspected the 1-minute average; all were <=8 on the first read, so no 60 s sleeps were required (wait=0 s each). Raw boundary snapshots also retain vm_stat. Other sessions were not inspected or changed.

### Visual check and exact failures

Separate cold background graphics rig-test start, same fixture Tuesday 15:30, player x=215,z=0. Nine 1600×900 PNGs inspected: first close view includes a dressed walker in a red jacket and dark trousers; its return contains dressed walkers but the target is distant. The second close/return view is occluded by a building. Five pan stills contain sparse dressed walkers or empty/occluded sidewalk. No undressed body is seen in the inspected images; the images do not prove no invisible body or brief appearance flash, or unchanged visuals versus a synchronized baseline.

Two native walker handles were selected. After an 8 s camera-away interval, the first handle (`walker-a003dd287c154421abf30362d533be80`) still answered screen_pos and camera-follow; its return capture occurred after 11.914 s. The second (`walker-469c462227ce4d1aa31d9cfaac62f003`) returned `ops_view_stale_target` for both screen_pos get and camera follow; return after 11.871 s. A native body handle has no persistent resident ID and no release/reacquire event proof. Even the surviving handle cannot establish that the same resident was assigned before and after, or that release/reacquire happened. Same-resident look stability remains unverified; this is not an asserted appearance-change failure. The pan sequence used 24 camera operations over 121.914 s. All requested appearance-error strings have zero matches in both Player logs.

| Player.log string | Headless A matches | Background B matches |
| --- | --- | --- |
| Appearance meshes unavailable | 0 | 0 |
| Empty appearance | 0 | 0 |
| Merged body | 0 | 0 |
| Merged body already has blendshapes | 0 | 0 |
| Merged body vertex layout differs | 0 | 0 |

- `python3 scripts/mac-agent call teleport {"x": 215, "z": 0}` → `Missing "who": "player" or a food vehicle's id.`.
- `python3 scripts/mac-agent op screen_pos {"action": "get", "target": {"kind": "walker", "id": "walker-469c462227ce4d1aa31d9cfaac62f003"}}` → `{ "error": "ops_view_stale_target", "reply": { "ok": false, "id": "9832f911693340308264b5eac50a8340", "session": "5cecf2c6bbea47769b9ce83575df334e", "error": "ops_view_stale_target" } }`.
- `python3 scripts/mac-agent op camera {"action": "follow", "target": {"kind": "walker", "id": "walker-469c462227ce4d1aa31d9cfaac62f003"}, "offset": {"x": 0, "y": 4, "z": -6}, "seconds": 10}` → `{ "error": "ops_view_stale_target", "reply": { "ok": false, "id": "1f22729883314256aca2a7a02c524bef", "session": "5cecf2c6bbea47769b9ce83575df334e", "error": "ops_view_stale_target" } }`.

The second walker handle went stale; the first remained readable. The initial teleport omitted who=player and was refused before mutation; the corrected call succeeded. The first gc population read after native load returned `Warning: [Persistent City] gc population refused: no active city.`; asynchronous activation completed before the next read confirmed reached=3000. No window opened before confirmation. No ops_load_* failure, staging refusal or runtime stop occurred.

### Player.log excerpts: activation, speed changes and toggles

```text
[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 15:30.
[Mod:FoodTrucks] Test bridge: run_until day 2 15:30 reached at day 2 15:30; paused, speed 1.
[Mod:FoodTrucks] Test bridge: console 'pc gc start' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (3 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 01:00.
[Mod:FoodTrucks] Test bridge: run_until day 3 01:00 reached at day 3 01:00; paused, speed 1.
[Mod:FoodTrucks] Test bridge: console 'pc gc start' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (3 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (3 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 15:30.
[Mod:FoodTrucks] Test bridge: run_until day 3 15:30 reached at day 3 15:30; paused, speed 1.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc frames start' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc frames stop' (2 lines).
```

Explicit speed=1 replies and autosave readbacks are retained before each window. No RLO toggle occurred, so there is no toggle excerpt. SaveCostDiagnostics stayed off.

### NOT RUN grouped

- **GR-5 60 fps arms:** excluded; Mac historically about 5 fps at 1,000 residents in graphics mode; PC throwaway time/perf/modwork fix agent-rig #90 not merged for this request. Headless achieved fps does not verify a graphics arm. No Windows PC contact.
- **GR-5 save latency:** excluded, owner decision pending; no save profiling or save-latency measurement.
- **#262 checks 4–5 and #246 check 4 full proof:** excluded, Rig bot fixture/observation gaps.
- **Everything else:** outside the requested GR-5 GC/street/looks checks, including a standalone idle-tick allocation check. No release, Workshop, version/changelog, production-code, schema or food-weight change.

Tooling gaps within performed checks: durable resident identity/reacquisition; sparse visual observation cannot prove no invisible/briefly undressed body; exact inclusive UpdateCity p95 under automatic nested hooks; per-GC-pause frame IDs; no commit-stamped build log. Exact invoked errors are above, with no invented failure for an unadvertised op.

### Cleanup and offline validation

Stopped only owned players normally. Restored full original ModsLocal/PersistentCity, exact install.json, isolated preferences/config/plist/PlayerSettings and original absent paths. Verified 35 file hashes, 3 absent paths, PersistentCity file set and 53 RLO hashes; mismatches=`[]`. AgentRigOps unchanged. install.json SHA-256 `dea36ce03586cc53d3b2c7e5cd0fda6d9a54e61c670a21d0676ad18feee54a95`. Original autosave settings restored; process guard ends at stop. Disposable fixture saves unchanged; no new save. Final running=false, lease held=false, pending=0; both owned player PIDs and their caffeinate processes gone.

Candidate build passed. Compact JSON parses and is under 20 KB; docs-links, markdown-ban and diff checks recorded in the PR. No test source changed, no full/regression suite run. One build/evidence worktree removed after commit, push and PR creation.

Refs #270

## Rig results — 15 November 2026 a, Mac (GR-5 GC/street recheck after #273 at 7107f72e)

**RESULT: DONE. Counts: 0 passed / 1 partial / 3 failed / 4 NOT RUN groups.** Completed day/night/street 600 s windows in one fresh headless process. Look proof remains partial. In-game proof gates no merge; this docs-only evidence PR remains unmerged.

Role `rig_test`, where `mac`, isolated `agentmac`, owner `persistent-city-rig-2026-11-15a-mac`. `<stage>` = `<home>/HART-Builds/persistent-city-rig-2026-11-15a-mac`. [Compact GC/frame evidence](perf/gr5-2026-11-15a-mac.json): 19,969 bytes, SHA-256 `e6617b9808a883ae61dfcea518222e6457ad30fdea4d746d2aecc23ba2802f18`. Raw logs, replies, scripts, screenshots and backups stay outside Git. Report copy: `<home>/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-15a-mac.md`.

Day gen0 8 / pause p95 27.888 ms / mod 295.628 MB; night gen0 6 / pause p95 37.301 ms / mod 151.470 MB. Street UpdateCity self / union p95 6.5466 / 6.6550 ms, 37 >=30 ms spikes. The GC and street gates remain open.

Raw evidence index: `<stage>/raw-evidence-manifest.json`, SHA-256 `99a3a5fcc32b56ce3518b182b2acb6f4341d1d6728106f4567bc9b7e849cd9c5`; full replies, Player logs, backups and candidate hash verification remain there.

### Build and rig

| Item | Evidence |
| --- | --- |
| Candidate | `7107f72ee9f2bf91bbf3170f75e3eba4c675a836`. Initial origin/codex/persistent-city matched the requested merge; final fetched tip `7107f72ee9f2bf91bbf3170f75e3eba4c675a836`. Tested binary stayed at the initial SHA. |
| Worktree / branch | New `rig-2026-11-15a-mac-evidence` / `codex/mac-rig-2026-11-15a`; isolated build/evidence worktree. Original checkout clean and untouched. |
| Official agent-rig HEAD | `17ff9f86ef07a8c071a90886fa76ed2e98f56c57`; clean, fetch and ff-only pull already current. No tooling edit or update. |
| Live AgentRigOps SHA-256 | `d3188b0e837188aec8be85535846953170da99807ad22914ccf4a3c38d77ed3c`; pre-start, loaded health and restore verification agree. |
| Build | `PATH=/opt/homebrew/bin:$PATH`; `DOTNET_ROOT` from Homebrew dotnet; `python3 tools/build.py --game-managed "<home>/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0. No RLO/game DLL packaged. |
| PersistentCity DLL SHA-256 | `6ae4d3df0c1cb2d2f285d8be6b9f91420740c8a8836314c2683d3dd6833fd0d2`; package and live staged copy agree. |
| Loaded build | Health module ID `7d06c1a3-984d-418c-b79d-1685a4b06991` occurs once in candidate DLL. Receipt-validated start accepted staged hashes. No commit-stamped build line is emitted; activation excerpts below. |
| Stage | `python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package`; game stopped, lease held; 28 scoped receipt entries refreshed. Backup `<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261115T023340Z`. |
| Starts | Headless rig-test PID 85401 for all three measured windows. Background graphics rig-test for look observations only. See exact invoked failures below. |
| RLO | Player.log: Real Life Overhaul 1.0.6 initialised. living_population absent/live=true; social_media=1/true; social_media_notices=0/false. No RLO option toggle or file change. |
| Autosave / SaveCost | Original native 5 minutes / 3 saves, prevent_autosave=false. #70 process guard suppressed after every load and before/after every window. SaveCostDiagnostics requested=false, writerSchema=1; native save profiler installed=false. No manual save or save-latency measurement. |
| Lease / resources | Free/stopped initially; acquired 2026-11-15T02:32:37.473863Z. Pre-start 52.3 GiB disk, 76% reported memory free. No other session intervention, bot message, Windows contact or macOS setting change. |

Printed rollback, home paths redacted (game stopped, lease held):

```sh
rm -rf "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261115T023340Z/mod" "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261115T023340Z/install.json" "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

Cleanup used the equivalent move-aside/copy-back form, retaining the staged candidate outside the profile before copying the complete original mod and exact receipt back. No receipt was hand-edited.

### Per-check Expected lines and outcomes

> 1. Day gc window. Setup: load the fixture, set the clock to 15:30, idle camera. Steps: run a 600 s gc window. Expected: report gen0, pause p95, heap, and mod/physical/advance totals plus every segment and sub-segment against rig 14l (mod 313.5 MB, gen0 8, pause p95 33.488 ms). advance_events should drop from 14l's 53.7 MB. Name the largest `mod_routes_*`, `mod_closures_*` and `physical_unattributed_*` sub-segment.
> 2. Night gc window. Same as 1 at 01:00. Compare against 14l night (mod 166.4 MB, gen0 7, pause p95 36.717 ms).
> 3. Street window. Setup: same fixture, daytime, pan the camera across the city. Steps: run a 600 s street window. Expected: report UpdateCity self/union and mod-work p95 against 14l (5.4914 / 5.5548 ms) and every >=30 ms spike with its `phase`, `detail` and new `sub` field. A routes spike should name `mod_routes_rebuild`, `_speeds`, `_nodes` or `_sweep`.
> 4. Look stability. Setup: same fixture, daytime street. Steps: follow 5 residents through release and reacquire of their bodies, then reload the save and find them again. Expected: dressed bodies, same look per resident before and after, and none of "Appearance meshes unavailable", "Empty appearance" or "Merged body ..." in the log.

- **1. Day 15:30, 600 s: FAIL.** Expected reporting completed below. GC gate: pause p95 under ~36 ms AND gen0 at or under ~5. Observed gen0 8, pause p95 27.888 ms: gate not met. #273 allocation target <=208.978 MB (one-third below 14l): observed 295.628 MB, not met. Pause target <=28.6 ms: met. advance_events 49.904 MB vs 14l 53.700 MB (-7.07%).
- **2. Night 01:00 after speed 20, 600 s: FAIL.** Expected reporting completed below. GC gate: pause p95 under ~36 ms AND gen0 at or under ~5. Observed gen0 6, pause p95 37.301 ms: gate not met. #273 allocation target <=110.937 MB (one-third below 14l): observed 151.470 MB, not met. Pause target <=32.9 ms: not met. advance_events 8.036 MB vs 14l 11.112 MB (-27.68%).
- **3. Street, daytime 600 s with pans: FAIL.** UpdateCity self / mod-work union p95 6.5466 / 6.6550 ms; strict <5 ms not met. 37 union frames >=30 ms; no-spike target not met. Every exported spike and its new sub/sub_ms follows.
- **4. Look stability: PARTIAL.** Five native walker handles were followed, moved out of view for 8 s, and queried on return. Three handles remained readable and two returned ops_view_stale_target for both screen_pos get and camera follow. Return delays were approximately 12–14 s. All five old handles returned ops_view_stale_target after the bounded reload, which completed in 175.1 s without an ops_load_* failure or cold restart. Eleven 1600×900 captures were inspected. Pair 0 shows a dressed figure with orange headwear, dark clothing and light/purple shoes before and after; pair 1 has cropped or non-comparable visible subjects; pair 2 is blocked by a building; pairs 3–4 contain sparse/distant walkers or empty pavement. The after-reload wide shot contains a dressed walker. No undressed body was seen in these images. The handles do not identify Persistent City residents or prove that a body was released and reacquired, and all old handles became stale on reload: the required five-resident same-look proof is NOT RUN / tooling gap, not a pass or an observed appearance-change failure. Sparse stills cannot rule out invisible bodies or brief flashes. Both complete process logs contain zero instances of Appearance meshes unavailable, Empty appearance and Merged body.

No weaker look pass criterion is substituted. Reported performance gates and completion of the Expected reporting are separate outcomes. Idle ticks staying 0 B and exact simulation equivalence are offline checks, not established by these moving speed-1 windows.

### GC and 15a versus 14l

Fixture `FT GR5 13f Timing v1`, 1,000 residents / 2,000 visitors / 1,683 places, person-target off, player x=215,z=0. No fallback fixture. Day Tuesday 15:30; night Wednesday 01:00; street Wednesday 16:04 after a shared-host load wait. Speed=1 in windows, speed-20 advances outside. MB is decimal. `boehm_total`, incremental=true, max_generation=0; pause means cycle-ending frame excess, not total incremental collector work.

| Window | frames / dropped | gen0/1/2 | pause p50 / p95 / max ms | heap_after_gc_bytes | mono_heap_bytes | mod / physical / advance MB |
| --- | --- | --- | --- | --- | --- | --- |
| gc-day | 16843 / 0 | 8/0/0 | 26.794/27.888/27.888 | 1270401536 | 1456128000 | 295.628 / 112.167 / 97.634 |
| gc-night | 15987 / 0 | 6/0/0 | 30.581/37.301/37.301 | 1358660949 | 1553727488 | 151.470 / 36.201 / 30.515 |

| Metric | 14l day / night | 15a day / night | 15a minus 14l |
| --- | --- | --- | --- |
| gen0 | 8 / 7 | 8 / 6 | +0 / -1 |
| pause p95 ms | 33.488 / 36.717 | 27.888 / 37.301 | -5.600 / +0.584 |
| mod MB | 313.467 / 166.405 | 295.628 / 151.470 | -17.839 (-5.69%) / -14.936 (-8.98%) |
| physical MB | 113.771 / 35.991 | 112.167 / 36.201 | -1.604 (-1.41%) / +0.210 (+0.58%) |
| advance MB | 101.657 / 33.572 | 97.634 / 30.515 | -4.023 (-3.96%) / -3.057 (-9.11%) |
| advance_events MB | 53.700 / 11.112 | 49.904 / 8.036 | -3.796 (-7.07%) / -3.076 (-27.68%) |
| advance_planning MB | 45.551 / 20.020 | 45.317 / 20.190 | -0.233 (-0.51%) / +0.170 (+0.85%) |
| mod_routes MB | 75.000 / 74.009 | 62.786 / 62.952 | -12.214 (-16.29%) / -11.057 (-14.94%) |
| mod_closures MB | 10.366 / 10.121 | 10.368 / 10.042 | +0.003 (+0.03%) / -0.079 (-0.78%) |
| physical_unattributed MB | 14.700 / 14.734 | 15.265 / 15.168 | +0.565 (+3.84%) / +0.434 (+2.95%) |

These are shared-host measurements, not a controlled A/B run. Host boundary load and CPU processes follow; this session ran no builds or tests during timing.

### Every allocating segment and sub-segment

| Segment | Day bytes / samples / skipped | Night bytes / samples / skipped |
| --- | --- | --- |
| mod | 295627832 / 16844 / 0 | 151469640 / 15988 / 0 |
| advance | 97633720 / 16754 / 0 | 30515360 / 15900 / 0 |
| physical | 112166680 / 16845 / 0 | 36200960 / 15989 / 0 |
| physical_ambient | 2147504 / 16754 / 0 | 2038576 / 15900 / 0 |
| physical_vehicles | 1142624 / 16754 / 0 | 225808 / 15900 / 0 |
| physical_bindings | 16224 / 527756 / 0 | 23536 / 285323 / 0 |
| physical_vendor_meals | 0 / 16754 / 0 | 0 / 15900 / 0 |
| physical_fade | 0 / 16754 / 0 | 0 / 15900 / 0 |
| physical_focus | 0 / 16754 / 0 | 0 / 15900 / 0 |
| physical_observe | 288000 / 510691 / 0 | 61328 / 269222 / 0 |
| physical_release | 10032 / 328 / 0 | 1840 / 288 / 0 |
| physical_parked | 1971024 / 586 / 0 | 1689680 / 577 / 0 |
| physical_index | 80 / 2197 / 0 | 80 / 2121 / 0 |
| physical_queues | 8960 / 2197 / 0 | 17424 / 2121 / 0 |
| physical_roster | 286144 / 2197 / 0 | 217872 / 2121 / 0 |
| physical_selection | 1332392 / 2761 / 0 | 639616 / 2339 / 0 |
| physical_spawn_pedestrian | 39967200 / 798 / 0 | 80480 / 289 / 0 |
| physical_spawn_appearance | 49563040 / 930 / 0 | 15971472 / 288 / 0 |
| physical_spawn_check | 0 / 620 / 0 | 0 / 192 / 0 |
| physical_spawn_vehicle | 168800 / 76 / 0 | 65056 / 25 / 0 |
| physical_unattributed | 15264656 / 33601 / 0 | 15168192 / 31891 / 0 |
| physical_spawn_randomize | 2781488 / 310 / 0 | 843536 / 96 / 0 |
| physical_spawn_bake | 2775056 / 310 / 0 | 1691520 / 96 / 0 |
| advance_planning | 45317136 / 9406 / 0 | 20189616 / 4184 / 0 |
| advance_events | 49904008 / 21679 / 0 | 8036096 / 18082 / 0 |
| advance_stops | 2412576 / 16754 / 0 | 2289648 / 15900 / 0 |
| advance_other | 0 / 16754 / 0 | 0 / 15900 / 0 |
| physical_spawn_grow | 37089488 / 128 / 0 | 0 / 0 / 0 |
| mod_prelude | 2598192 / 16844 / 0 | 2426144 / 15988 / 0 |
| mod_rlo | 3507232 / 16843 / 0 | 3519936 / 15988 / 0 |
| mod_food | 1279216 / 16844 / 0 | 855168 / 15988 / 0 |
| mod_optional_stops | 2508256 / 16844 / 0 | 2306144 / 15988 / 0 |
| mod_routes | 62786040 / 16844 / 0 | 62952440 / 15988 / 0 |
| mod_closures | 10368288 / 16754 / 0 | 10041888 / 15900 / 0 |
| mod_hold_replay | 0 / 16754 / 0 | 0 / 15900 / 0 |
| mod_physical_setup | 0 / 16689 / 0 | 0 / 15527 / 0 |
| mod_inspector | 2412576 / 16754 / 0 | 2289968 / 15900 / 0 |
| mod_summary | 360912 / 16679 / 0 | 361056 / 15362 / 0 |
| mod_other | 352 / 16844 / 0 | 0 / 15988 / 0 |
| mod_routes_speeds | 920544 / 14174 / 0 | 939232 / 14372 / 0 |
| mod_routes_nodes | 57936920 / 14174 / 0 | 58273512 / 14372 / 0 |
| mod_routes_sweep | 256 / 16753 / 0 | 144 / 15898 / 0 |
| mod_routes_rebuild | 3927552 / 16844 / 0 | 3738624 / 15988 / 0 |
| mod_closures_network | 10208960 / 16754 / 0 | 9878576 / 15900 / 0 |
| mod_closures_links | 159328 / 16754 / 0 | 159200 / 15900 / 0 |
| advance_events_traffic | 570160 / 2764 / 0 | 435648 / 1728 / 0 |
| advance_events_food | 30123744 / 1037 / 0 | 3893840 / 320 / 0 |
| advance_events_citizen | 14185016 / 3843 / 0 | 2071984 / 2125 / 0 |
| advance_events_route_begin | 5025088 / 3414 / 0 | 1634624 / 1544 / 0 |
| advance_events_other | 0 / 10621 / 0 | 0 / 12365 / 0 |
| advance_planning_slice | 30765576 / 5992 / 0 | 12761808 / 2640 / 0 |
| advance_planning_finish | 14551560 / 3414 / 0 | 7427808 / 1544 / 0 |
| physical_setup_places | 273928 / 1 / 0 | 273928 / 1 / 0 |
| physical_setup_bodies | 180016 / 1 / 0 | 206112 / 1 / 0 |
| physical_setup_vehicles | 15920 / 2 / 0 | 16320 / 2 / 0 |
| physical_setup_ambient | 12362744 / 2 / 0 | 12362936 / 2 / 0 |
| physical_setup_preflight | 2000 / 1 / 0 | 752 / 1 / 0 |
| physical_unattributed_setup | 4240 / 1 / 0 | 4208 / 1 / 0 |
| physical_unattributed_update | 0 / 16754 / 0 | 224 / 15900 / 0 |
| physical_unattributed_waiting | 13232 / 90 / 0 | 14112 / 88 / 0 |
| physical_unattributed_entry | 2412576 / 16754 / 0 | 2289600 / 15900 / 0 |

Sub-segments overlap their parents; do not add them again. physical spawn randomize/bake/grow also overlap their spawn parents. Samples skipped after collections are explicit.

- gc-day largest `mod_routes_*`: `mod_routes_nodes` 57.937 MB, 92.28% of parent, 19.60% of total mod.
- gc-day largest `mod_closures_*`: `mod_closures_network` 10.209 MB, 98.46% of parent, 3.45% of total mod.
- gc-day largest `advance_events_*`: `advance_events_food` 30.124 MB, 60.36% of parent, 10.19% of total mod.
- gc-day largest `advance_planning_*`: `advance_planning_slice` 30.766 MB, 67.89% of parent, 10.41% of total mod.
- gc-day largest `physical_unattributed_*`: `physical_unattributed_entry` 2.413 MB, 15.80% of parent, 0.82% of total mod.
- gc-day largest remaining leaf: `mod_routes_nodes` 57.937 MB, 19.60% of total mod.
- gc-night largest `mod_routes_*`: `mod_routes_nodes` 58.274 MB, 92.57% of parent, 38.47% of total mod.
- gc-night largest `mod_closures_*`: `mod_closures_network` 9.879 MB, 98.37% of parent, 6.52% of total mod.
- gc-night largest `advance_events_*`: `advance_events_food` 3.894 MB, 48.45% of parent, 2.57% of total mod.
- gc-night largest `advance_planning_*`: `advance_planning_slice` 12.762 MB, 63.21% of parent, 8.43% of total mod.
- gc-night largest `physical_unattributed_*`: `physical_unattributed_entry` 2.290 MB, 15.09% of parent, 1.51% of total mod.
- gc-night largest remaining leaf: `mod_routes_nodes` 58.274 MB, 38.47% of total mod.

gc-day largest remaining parent segment: `mod_routes` 62.786 MB, 21.24% of total mod. `physical_setup_ambient` is 12.363 MB (80.99% of physical_unattributed); the physical_setup_* sub-segments plus physical_unattributed_* remainders reconcile that parent. They overlap it.
gc-night largest remaining parent segment: `mod_routes` 62.952 MB, 41.56% of total mod. `physical_setup_ambient` is 12.363 MB (81.51% of physical_unattributed); the physical_setup_* sub-segments plus physical_unattributed_* remainders reconcile that parent. They overlap it.

Street preparation reached 15:30, then the inherited runner resumed speed 1 before waiting for shared-host load to fall from 18.19. It was paused at 16:04 outside any measured window. Only the owned controller was interrupted, exit 130 with `KeyboardInterrupt` in `loadgate` at `time.sleep(60)`; the same game process and lease remained. A continuation waited paused, then resumed speed 1 and measured 600 s from 16:04. This warm-up differs from 14l: absence of its initial routes spike cannot establish that spike is fixed. Initial controller PID verification returned `AssertionError: []` before mutation; corrected exact-path verification selected only the owned controller. No game runtime stop occurred.

### Findings for the next fix slice

The largest remaining source is mod_routes_nodes: 57.937 MB day and 58.274 MB night, 92.28% / 92.57% of mod_routes. mod_closures_network carries 98.46% / 98.37% of closures; advance_events_food is the largest events child; advance_planning_slice is the largest planning child. The former ~12.3 MB physical burst is now named physical_setup_ambient (12.363 MB in each GC window), 80.99% / 81.51% of physical_unattributed. Street body growth still reaches 134.255 ms (frame 64552). No routes-dominant >=30 ms frame occurred inside this street window; its preparation warm-up prevents a claim that 14l’s 359 ms initial route frame is fixed.

### Appearance counters and pool hit rate

| Window | AppearanceBuilds / Reuses | reuse / (build+reuse) % | last periodic pedestrianLooks |
| --- | --- | --- | --- |
| gc-day | 274 / 28 | 9.27 | 274 built/28 reused/2337 deferred/31 idle/97 owned/8 adopted/284 baked/45 bake sizes/1678 KB bake scratch/103 grown/15 trimmed/223 fitted/152 waited/3.0 ms build |
| gc-night | 96 / 94 | 49.47 | 96 built/94 reused/832 deferred/32 idle/32 owned/119 adopted/567 baked/44 bake sizes/1634 KB bake scratch/0 grown/87 trimmed/20 fitted/74 waited/35.2 ms build |
| street | 131 / 35 | 21.08 | 131 built/35 reused/1102 deferred/30 idle/48 owned/67 adopted/838 baked/45 bake sizes/1678 KB bake scratch/47 grown/66 trimmed/0 fitted/176 waited/41.4 ms build |

These are last periodic cumulative snapshots, not exact window deltas. 14l day 280/29 (9.39%), night 91/95 (51.08%), street 105/34 (24.46%).

### Timing and every >=30 ms spike

| Window | seconds / frames / achieved fps | UpdateCity self p95 / max ms | mod-work p95 / max ms / spikes | whole-frame p95 / max ms / spikes | NativeCityClock.MaximumLagMinutes |
| --- | --- | --- | --- | --- | --- |
| gc-day | 600.630 / 16850 / 28.05 | 0.3100 / 49.1873 | 3.4447 / 60.6002 / 1 | 38.6039 / 214.7326 / 16511 | 0.8329019546513337 |
| gc-night | 601.135 / 15995 / 26.61 | 0.3729 / 78.7846 | 3.0534 / 96.8682 / 7 | 48.1055 / 1736.4678 / 15515 | 1.0 |
| street | 601.401 / 4307 / 7.16 | 6.5466 / 190.8775 | 6.6550 / 191.2324 / 37 | 257.6140 / 2519.1499 / 4251 | 1.0 |

Street vs 14l UpdateCity/union p95: +1.0552 / +1.1002 ms; union spike count +27; fps -0.44.

GC windows use nested updatecity/advance/prepare/physical/inspector components. Street re-arms with only UpdateCity plus automatic entry-point hooks, matching 14l. UpdateCity is boundary self time; exact inclusive UpdateCity p95 is not exported under nested hooks. Mod-work union counts nested scopes once and supplies the strict <5 ms gate. PC phases provide inner attribution; whole-frame timing is separate.

**gc-day: every exported union spike (1 total / 1 listed)**

| Frame | union ms | dominant | PC mod ms | phase / ms | detail / ms | sub / sub_ms | whole-frame ms |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 3847 | 60.6002 | updatecity | 60.542 | physical_unattributed / 28.832 | physical_spawn_grow / 2.145 | physical_setup_ambient / 26.003 | 98.4924 |

coverage.complete=True; validity=`{"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}`; save_activity=`{"attempts":10847,"allowed":0,"refused":10847,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}`; final lag=0.0, refused=False, stoppedAfterAccountingFailure=False, historicalClockMisses=0.

**gc-night: every exported union spike (7 total / 7 listed)**

| Frame | union ms | dominant | PC mod ms | phase / ms | detail / ms | sub / sub_ms | whole-frame ms |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 27010 | 96.8682 | updatecity | 96.822 | physical_unattributed / 41.95 | physical_spawn_bake / 0.6 | physical_setup_ambient / 37.523 | 127.7302 |
| 37348 | 38.3295 | physical | 38.244 | physical_spawn_appearance / 21.346 | physical_spawn_bake / 2.373 | physical_unattributed_update / 0.051 | 58.7027 |
| 37498 | 31.1587 | physical | 31.109 | physical_spawn_appearance / 21.527 | physical_spawn_bake / 2.416 | mod_routes_speeds / 0.017 | 59.0957 |
| 37613 | 34.2723 | physical | 33.725 | physical_parked / 16.866 | none / 0 | advance_events_citizen / 2.871 | 174.1906 |
| 38193 | 30.5926 | physical | 30.538 | physical_spawn_appearance / 20.942 | physical_spawn_bake / 1.326 | mod_routes_speeds / 0.021 | 65.1695 |
| 38371 | 31.6208 | physical | 31.546 | physical_spawn_appearance / 18.199 | physical_spawn_bake / 1.763 | mod_routes_speeds / 0.02 | 62.6299 |
| 41404 | 61.7717 | physical | 60.241 | physical_parked / 42.078 | none / 0 | mod_routes_speeds / 0.952 | 384.2137 |

coverage.complete=True; validity=`{"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}`; save_activity=`{"attempts":16006,"allowed":0,"refused":16006,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}`; final lag=0.0, refused=False, stoppedAfterAccountingFailure=False, historicalClockMisses=0.

**street: every exported union spike (37 total / 37 listed)**

| Frame | union ms | dominant | PC mod ms | phase / ms | detail / ms | sub / sub_ms | whole-frame ms |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 64008 | 43.0235 | updatecity | 42.769 | physical_spawn_pedestrian / 37.123 | physical_spawn_grow / 36.717 | advance_planning_slice / 0.782 | 625.3729 |
| 64009 | 31.4886 | updatecity | 30.021 | physical_parked / 13.693 | none / 0 | advance_planning_slice / 1.475 | 1225.6756 |
| 64011 | 33.9658 | updatecity | 33.877 | physical_spawn_appearance / 14.18 | physical_spawn_bake / 1.81 | mod_routes_rebuild / 2.694 | 244.6093 |
| 64200 | 64.952 | updatecity | 64.812 | physical_spawn_pedestrian / 36.455 | physical_spawn_bake / 2.571 | advance_events_traffic / 0.226 | 1623.8256 |
| 64201 | 32.0321 | updatecity | 31.39 | physical_spawn_pedestrian / 7.675 | physical_spawn_bake / 0.81 | mod_closures_links / 1.475 | 1079.9728 |
| 64203 | 42.2852 | updatecity | 42.18 | physical_spawn_pedestrian / 36.796 | physical_spawn_grow / 36.685 | advance_events_citizen / 2.88 | 451.1585 |
| 64206 | 83.2187 | updatecity | 82.948 | physical_spawn_pedestrian / 26.95 | physical_spawn_bake / 1.514 | advance_events_citizen / 4.326 | 765.7872 |
| 64207 | 37.1872 | updatecity | 36.747 | physical_parked / 12.435 | none / 0 | advance_events_citizen / 3.231 | 414.04 |
| 64208 | 49.6159 | updatecity | 49.51 | mod_rlo / 45.162 | none / 0 | mod_routes_sweep / 1.005 | 590.2984 |
| 64361 | 35.7123 | updatecity | 35.662 | advance_events / 32.903 | none / 0 | advance_events_traffic / 32.901 | 234.4467 |
| 64385 | 54.6631 | updatecity | 52.374 | physical_parked / 21.205 | none / 0 | advance_events_citizen / 3.783 | 970.6657 |
| 64386 | 47.7311 | updatecity | 46.549 | physical_parked / 14.462 | none / 0 | mod_closures_links / 1.326 | 1103.9031 |
| 64402 | 35.8558 | updatecity | 35.157 | physical_parked / 13.223 | none / 0 | advance_planning_slice / 2.721 | 760.8699 |
| 64407 | 51.0784 | updatecity | 49.676 | physical_parked / 27.279 | none / 0 | advance_events_citizen / 5.306 | 1672.598 |
| 64408 | 49.6777 | updatecity | 48.388 | physical_parked / 11.63 | none / 0 | mod_closures_links / 2.201 | 2519.15 |
| 64409 | 37.4901 | updatecity | 35.358 | physical_parked / 13.189 | none / 0 | mod_closures_links / 0.874 | 1868.5511 |
| 64410 | 38.9481 | updatecity | 37.578 | physical_parked / 12.506 | none / 0 | mod_closures_links / 2.123 | 624.6531 |
| 64412 | 48.2963 | updatecity | 46.842 | physical_parked / 16.287 | none / 0 | advance_events_citizen / 6.187 | 674.2004 |
| 64414 | 65.2287 | updatecity | 64.453 | physical_parked / 24.353 | none / 0 | advance_events_food / 8.454 | 1353.0969 |
| 64415 | 87.9771 | updatecity | 87.011 | physical_index / 29.014 | none / 0 | advance_events_food / 13.008 | 1438.0869 |
| 64416 | 79.8011 | updatecity | 79.306 | mod_rlo / 48.3 | none / 0 | mod_routes_sweep / 1.009 | 673.3001 |
| 64435 | 56.3015 | updatecity | 55.025 | physical_parked / 19.327 | none / 0 | advance_events_citizen / 4.102 | 956.0297 |
| 64437 | 55.2463 | updatecity | 53.942 | physical_parked / 24.273 | none / 0 | mod_closures_network / 1.29 | 1245.8501 |
| 64438 | 45.5075 | updatecity | 43.822 | physical_parked / 11.349 | none / 0 | advance_events_citizen / 4.778 | 1488.6704 |
| 64439 | 43.2685 | updatecity | 41.947 | physical_parked / 12.428 | none / 0 | advance_events_citizen / 2.597 | 866.8166 |
| 64475 | 76.4816 | updatecity | 76.342 | physical_spawn_appearance / 39.235 | physical_spawn_randomize / 21.776 | advance_events_citizen / 0.799 | 872.2912 |
| 64476 | 41.0957 | updatecity | 39.702 | physical_roster / 11.22 | none / 0 | mod_closures_links / 2.976 | 1681.3238 |
| 64478 | 52.6093 | updatecity | 51.766 | physical_parked / 14.784 | none / 0 | advance_events_route_begin / 10.177 | 525.796 |
| 64514 | 36.1748 | updatecity | 36.106 | physical_spawn_appearance / 21.724 | physical_spawn_bake / 1.288 | advance_planning_finish / 1.184 | 216.5194 |
| 64516 | 39.0127 | updatecity | 38.958 | mod_summary / 34.789 | none / 0 | advance_planning_slice / 1.306 | 236.2953 |
| 64543 | 33.3887 | updatecity | 33.264 | physical_parked / 14.158 | none / 0 | advance_planning_slice / 6.238 | 628.0619 |
| 64546 | 34.3525 | updatecity | 34.144 | mod_optional_stops / 8.186 | none / 0 | advance_planning_slice / 6.744 | 666.6877 |
| 64547 | 46.0037 | updatecity | 45.79 | physical_parked / 21.566 | none / 0 | advance_planning_slice / 1.851 | 1339.2886 |
| 64548 | 47.788 | updatecity | 46.259 | physical_parked / 18.221 | none / 0 | advance_planning_slice / 2.245 | 818.5593 |
| 64552 | 191.2324 | updatecity | 190.883 | physical_spawn_pedestrian / 134.358 | physical_spawn_grow / 134.255 | advance_events_route_begin / 21.528 | 867.7503 |
| 64640 | 41.4157 | updatecity | 41.365 | physical_spawn_pedestrian / 37.755 | physical_spawn_grow / 37.73 | advance_planning_slice / 1.859 | 208.3983 |
| 64876 | 31.6815 | updatecity | 31.452 | mod_optional_stops / 22.67 | none / 0 | mod_closures_links / 0.648 | 772.9873 |

coverage.complete=True; validity=`{"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}`; save_activity=`{"attempts":4319,"allowed":0,"refused":4319,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}`; final lag=0.055912017822265625, refused=False, stoppedAfterAccountingFailure=False, historicalClockMisses=0.

### Host load, memory pressure and top CPU at every boundary

**gc-day start, 2026-11-15T02:35:21Z**

```text
3:35  up 1 day,  9:13, 1 user, load averages: 3.55 3.62 4.54
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 73%
PID  %CPU COMM
49418  82.3 suggestd
85401  52.9 Big Ambitions Agent Tests Mac
    1  18.4 launchd
  444  10.7 coreaudiod
  403   5.2 locationd
  756   4.3 Activity Monitor
85581   3.2 Python
```

**gc-day end, 2026-11-15T02:45:23Z**

```text
3:45  up 1 day,  9:23, 1 user, load averages: 2.67 3.08 3.92
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 59%
PID  %CPU COMM
87234 129.4 node
85401  96.3 Big Ambitions Agent Tests Mac
49418  87.1 suggestd
  444  10.7 coreaudiod
  735   2.7 Resolve
  756   1.1 Activity Monitor
  420   1.1 bluetoothd
```

**gc-night start, 2026-11-15T02:49:08Z**

```text
3:49  up 1 day,  9:27, 1 user, load averages: 3.81 3.52 3.92
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 64%
PID  %CPU COMM
88125 146.6 node
49418  86.1 suggestd
85401  57.3 Big Ambitions Agent Tests Mac
  444  10.5 coreaudiod
50220   3.2 com.apple.Virtualization.VirtualMachine
85581   2.9 Python
  735   2.2 Resolve
```

**gc-night end, 2026-11-15T02:59:10Z**

```text
3:59  up 1 day,  9:37, 1 user, load averages: 5.87 5.67 4.81
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 35%
PID  %CPU COMM
85401  91.2 Big Ambitions Agent Tests Mac
49418  85.3 suggestd
96265  40.6 chrome-headless-shell
  735  18.8 Resolve
96264  11.3 chrome-headless-shell
  444  10.7 coreaudiod
96263   3.4 node
```

**street start, 2026-11-15T03:16:13Z**

```text
4:16  up 1 day,  9:54, 1 user, load averages: 5.78 14.06 12.79
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 82%
PID  %CPU COMM
49418  73.2 suggestd
98768  64.3 Google Chrome
85401  29.5 Big Ambitions Agent Tests Mac
98772  23.8 Google Chrome Helper (Aperitif)
  359  15.6 fseventsd
98776  11.6 Google Chrome Helper (Aperitif Renderer)
98773   8.7 Google Chrome Helper (Aperitif)
```

**street end, 2026-11-15T03:26:16Z**

```text
4:26  up 1 day, 10:04, 1 user, load averages: 6.41 8.84 10.41
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 34%
PID  %CPU COMM
85401  94.8 Big Ambitions Agent Tests Mac
49418  81.0 suggestd
99893  72.1 routined
99482  38.9 chrome-headless-shell
99481  12.7 chrome-headless-shell
  444   6.9 coreaudiod
50220   4.0 com.apple.Virtualization.VirtualMachine
```

### Look observations, logs and exact op failures

Five native walker handles were followed, moved out of view for 8 s, and queried on return. Three handles remained readable and two returned ops_view_stale_target for both screen_pos get and camera follow. Return delays were approximately 12–14 s. All five old handles returned ops_view_stale_target after the bounded reload, which completed in 175.1 s without an ops_load_* failure or cold restart. Eleven 1600×900 captures were inspected. Pair 0 shows a dressed figure with orange headwear, dark clothing and light/purple shoes before and after; pair 1 has cropped or non-comparable visible subjects; pair 2 is blocked by a building; pairs 3–4 contain sparse/distant walkers or empty pavement. The after-reload wide shot contains a dressed walker. No undressed body was seen in these images. The handles do not identify Persistent City residents or prove that a body was released and reacquired, and all old handles became stale on reload: the required five-resident same-look proof is NOT RUN / tooling gap, not a pass or an observed appearance-change failure. Sparse stills cannot rule out invisible bodies or brief flashes. Both complete process logs contain zero instances of Appearance meshes unavailable, Empty appearance and Merged body.

| Player.log string | Headless A matches | Graphics B matches |
| --- | --- | --- |
| Appearance meshes unavailable | 0 | 0 |
| Empty appearance | 0 | 0 |
| Merged body | 0 | 0 |

- `python3 scripts/mac-agent op screen_pos {"action": "get", "target": {"kind": "walker", "id": "walker-5d25032dc74c4eab86663efea420196d"}}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "174bd690ad284ec98433fc7be8b449cf",     "session": "3fe9c243ba4c47098c9c241a240ea43c",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op camera {"action": "follow", "target": {"kind": "walker", "id": "walker-5d25032dc74c4eab86663efea420196d"}, "offset": {"x": 0, "y": 4, "z": -6}, "seconds": 10}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "6c5c7dd9726549f4a5e54e385e1a2bca",     "session": "3fe9c243ba4c47098c9c241a240ea43c",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos {"action": "get", "target": {"kind": "walker", "id": "walker-d7f34a2fd52647099b42350ccab42fd7"}}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "853610d194064b4e8fb9051d5511e191",     "session": "3fe9c243ba4c47098c9c241a240ea43c",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op camera {"action": "follow", "target": {"kind": "walker", "id": "walker-d7f34a2fd52647099b42350ccab42fd7"}, "offset": {"x": 0, "y": 4, "z": -6}, "seconds": 10}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "dcb52a87083146fa8c96984b2801fba6",     "session": "3fe9c243ba4c47098c9c241a240ea43c",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos {"action": "get", "target": {"kind": "walker", "id": "walker-39b040c0b86346ecb275ceb76605ca6c"}}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "bd6bfa8bd1c6461dab431861b7ee80ef",     "session": "3fe9c243ba4c47098c9c241a240ea43c",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos {"action": "get", "target": {"kind": "walker", "id": "walker-adf8710278f44e2c8081e01e56a2ca7b"}}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "eac0ad75691d4251a3ec16dc4251f6e4",     "session": "3fe9c243ba4c47098c9c241a240ea43c",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos {"action": "get", "target": {"kind": "walker", "id": "walker-5d25032dc74c4eab86663efea420196d"}}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "bc036cdd99a74b529e6a92ddd6055e93",     "session": "3fe9c243ba4c47098c9c241a240ea43c",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos {"action": "get", "target": {"kind": "walker", "id": "walker-4992a8cb049343138dffc1a70d2a2b03"}}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "8d54dbd05bc6449499d92a660cade6dc",     "session": "3fe9c243ba4c47098c9c241a240ea43c",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos {"action": "get", "target": {"kind": "walker", "id": "walker-d7f34a2fd52647099b42350ccab42fd7"}}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "bde4a47af7ad476088fa27d505327ccb",     "session": "3fe9c243ba4c47098c9c241a240ea43c",     "error": "ops_view_stale_target"   } }`.

### Player.log excerpts: activation, speed changes and toggles

```text
[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.
[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 15:30.
[Mod:FoodTrucks] Test bridge: run_until day 2 15:30 reached at day 2 15:30; paused, speed 1.
[Mod:FoodTrucks] Test bridge: console 'pc gc start' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (3 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 01:00.
[Mod:FoodTrucks] Test bridge: run_until day 3 01:00 reached at day 3 01:00; paused, speed 1.
[Mod:FoodTrucks] Test bridge: console 'pc gc start' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (3 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (3 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 15:30.
[Mod:FoodTrucks] Test bridge: run_until day 3 15:30 reached at day 3 15:30; paused, speed 1.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc frames start' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc frames stop' (2 lines).
```

Explicit speed=1 replies and autosave readbacks are retained before each window. No RLO toggle occurred. SaveCostDiagnostics stayed off.

### NOT RUN grouped

- **GR-5 60 fps arms:** excluded; Mac historically ~5 fps at 1,000 residents; PC throwaway time/perf/modwork fix agent-rig #90 not merged. Headless fps does not verify a graphics arm. No Windows PC contact.
- **GR-5 save latency:** excluded, owner decision pending; no save profiling or save-latency measurement.
- **#262 checks 4–5 and #246 check 4 full proof:** excluded, Rig bot fixture/observation gaps.
- **Anything else:** outside the four requested checks, including standalone idle-tick and simulation-equivalence proof. No release, Workshop, version/changelog, production-code, schema or food-weight change.

Tooling gaps: native walker handles do not expose durable Persistent City resident identity or a release/reacquire event; exact inclusive UpdateCity p95 under nested hooks; per-GC-pause Unity frame IDs; no commit-stamped Persistent City build line. Exact invoked errors are above.

### Cleanup and offline validation

Stopped only owned players; restored full original ModsLocal/PersistentCity and exact install.json, isolated preferences/config/plist/PlayerSettings and original absent paths. Verified 35 original file hashes, 3 absent paths, PersistentCity file set and 53 RLO hashes; mismatches=`[]`. AgentRigOps unchanged. install.json SHA-256 `dea36ce03586cc53d3b2c7e5cd0fda6d9a54e61c670a21d0676ad18feee54a95`. Original autosave setting restored; guard ends at stop. No new save. Lease released; final running=false, held=false, pending=0. Owned player and caffeinate processes gone.

Candidate build passed. Docs-links checked 20 Markdown files with zero broken relative links; markdown-ban and git diff --check passed. Compact JSON parses and is under 20 KB. Docs-only evidence validation is recorded in the PR; no test code changed and no full/regression suite run. Isolated build/evidence worktree removed after commit, push and PR creation.

Refs #273

## Rig results — 15 November 2026 b, Mac (GR-5 GC/street recheck after #275 at 72b2bb51)

**RESULT: DONE. Counts: 0 passed / 1 partial / 3 failed / 4 NOT RUN groups.** Day, night and street measured in one fresh headless rig-test process. Look proof remains partial. In-game proof gates no merge; this docs-only PR remains unmerged.

Role `rig_test`, where `mac`, isolated `agentmac`, owner `persistent-city-rig-2026-11-15b-mac`. `<stage>` = `<home>/HART-Builds/persistent-city-rig-2026-11-15b-mac`. [Compact GC/frame evidence](perf/gr5-2026-11-15b-mac.json): 12411 bytes, SHA-256 `f9485b3ff72065a4b86033329cb962c6c113a9159f6ddcee59f98f0d429dd4ac`. Raw logs, replies, scripts, captures and backups remain there. Report copy: `<home>/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-15b-mac.md`. MB is decimal.

Day mod 177.883 MB / gen0 7 / pause p95 43.403 ms; night 78.728 MB / 6 / 38.929 ms. Both allocation targets met; GC gate not met. Street self/union p95 6.8007/6.9556 ms, 28 >=30 ms frames; street gate not met.

Raw evidence manifest: `<stage>/raw-evidence-manifest.json`, SHA-256 `07d59fbacba8172f41ea0c3dc8425771b63f29b8e4b221c096a75a775e51d41f`.

### Build and rig

| Item | Evidence |
| --- | --- |
| Candidate | `72b2bb51cf897cca41ec04ad021934dfc9860ebd`; initial fetched tip matched the requested SHA. Final fetched tip `72b2bb51cf897cca41ec04ad021934dfc9860ebd`. Tested candidate unchanged. |
| Worktree / branch | New `rig-2026-11-15b-mac-evidence` / `codex/mac-rig-2026-11-15b`; original checkout unchanged. |
| Official agent-rig HEAD | `e6b818c3c463b436270aa240d66f06125d90cb63`; clean, fetch / ff-only pull already current. No rig source or installation update. |
| Live AgentRigOps SHA-256 | `590583898493435aa8683528fd4992e9184e21dbeea0fdfac5b0e566cdbee533`; verified before launch and after cleanup. |
| Build | `PATH=/opt/homebrew/bin:$PATH`; `DOTNET_ROOT` from Homebrew dotnet; `python3 tools/build.py --game-managed "<home>/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0. Package contains no RLO/game DLL. |
| PersistentCity DLL SHA-256 | `3141419552653a7b072b6d4b71c5982f2daecb1dd9b2c2219598909b7f2524e4`; built, packaged and staged copies match. |
| Stage | `python3 scripts/install-mac.py --stage-mod PersistentCity <stage>/package`; stopped and leased. Receipt backup `<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261115T065052Z`; 28 entries refreshed. |
| Process | Headless rig-test PID 55063, all three windows. Separate graphics process only for look observations. |
| RLO | 1.0.6; original living_population absent/live=true, social_media=1/true, social_media_notices=0/false. No RLO option change. |
| Autosave / SaveCost | Original native 5 minutes / 3 saves, prevent_autosave=false. #70 guard suppressed before/after each window and after loads. SaveCostDiagnostics requested=false; writerSchema=1. No manual save or latency measurement. |
| Lease / resources | Free/stopped initially; acquired 2026-11-15T06:49:41.398516Z. Before start 42.2 GiB disk / 40% reported memory free. Shared host; no other-session intervention or Windows contact. |

Printed rollback (stopped, leased), verbatim apart from `<home>` substituted for the home-directory path:

```sh
rm -rf "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261115T065052Z/mod" "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261115T065052Z/install.json" "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

Cleanup used the equivalent move-aside/copy-back form, preserving the staged candidate outside the profile and restoring the complete original mod plus exact receipt.

### Expected lines and per-check outcomes

> All input-only, no locked PC. Fixture FT GR5 13f Timing v1, 1,000 residents / 2,000 visitors, autosave checked off before each window.
> 1. **Day GC window.** Setup: load the fixture, clock 15:30, idle camera. Steps: run a 600 s gc window. Expected:
>    - Report gen0, pause p95, heap, and the mod, physical and advance totals, plus every segment and sub-segment against rig 15a (mod 295.6 MB, gen0 8, pause p95 27.9 ms).
>    - No `physical_setup_ambient` ≥30 ms frame. `mod_routes_nodes` and `mod_closures_network` far below 57.9 / 10.2 MB.
>    - "reused unchanged snapshot=True" for any clock-rate rebuild in Player.log.
> 2. **Night GC window.** Same as check 1 at 01:00, against 15a night (151.5 MB, gen0 6, pause p95 37.3 ms).
> 3. **Street window.** Setup: same fixture, daytime, pan the camera. Steps: run a 600 s street window. Expected:
>    - Report UpdateCity self/union and mod-work p95 against 15a (6.5466 / 6.6550 ms).
>    - Report every ≥30 ms spike with phase, detail and sub. None from `physical_spawn_grow`, `physical_parked` or `advance_events_traffic`.
>    - Report whole-frame p95/max and the worst body-growth frame separately from mod work (15a: 134 ms growth frame).
> 4. **Look stability.** Setup: same fixture, daytime street. Steps: follow 5 residents through body release and reacquire, then reload the save and find them again. Expected:
>    - Dressed bodies and the same look per resident before and after.
>    - Parked cars appear at their saved curbs.
>    - None of "Appearance meshes unavailable", "Empty appearance" or "Merged body ..." in the log.

- **1. Day 15:30, 600 s: FAIL.** GC gate: pause p95 under ~36 ms AND gen0 at or under ~5. Observed 7 gen0 / 43.403 ms; GC gate not met. #275 mod target <=209 MB: 177.883 MB, met. Every exported segment and sub-segment follows. Setup and snapshot Expected clauses are assessed below.
- **2. Night 01:00 after speed 20, 600 s: FAIL.** GC gate: pause p95 under ~36 ms AND gen0 at or under ~5. Observed 6 gen0 / 38.929 ms; GC gate not met. #275 mod target <=111 MB: 78.728 MB, met. Every exported segment and sub-segment follows. Setup and snapshot Expected clauses are assessed below.
- **3. Street, 600 s with pans: FAIL.** UpdateCity self / mod-work union p95 6.8007 / 6.9556 ms. Strict <5 ms gate not met; 28 >=30 ms mod-work frames, no-spike target not met. Attribution and whole-frame numbers follow.
- **4. Look stability: PARTIAL.** Five native-handle cycles and the reload were observed. Dressed bodies were visible; five-resident same-look/reacquire/reload identity and saved-curb assignments remain NOT RUN / tooling gaps. Detailed observations and exact errors follow.

### GC totals and 15b versus 15a

Fixture `FT GR5 13f Timing v1`, 1,000 residents / 2,000 visitors, player x=215,z=0; no fallback. Speed 1 inside windows; speed-20 advances outside. Idle camera for GC. Street pans use the independent camera. `boehm_total`, incremental=true, max_generation=0; pause is cycle-ending frame excess, not total incremental collector work.

| Window | frames / dropped | gen0/1/2 | pause p50 / p95 / max ms | heap_after_gc_bytes | mono_heap_bytes | mod / physical / advance MB |
| --- | --- | --- | --- | --- | --- | --- |
| gc-day | 17009 / 0 | 7/0/0 | 29.432/43.403/43.403 | 1282617929 | 1476640768 | 177.883 / 64.263 / 95.208 |
| gc-night | 15240 / 0 | 6/0/0 | 35.537/38.929/38.929 | 1284586837 | 1661190144 | 78.728 / 32.291 / 29.369 |

| Metric | 15a day / night | 15b day / night | Delta day / night |
| --- | --- | --- | --- |
| mod MB | 295.628 / 151.470 | 177.883 / 78.728 | -117.745 (-39.83%) / -72.741 (-48.02%) |
| physical MB | 112.167 / 36.201 | 64.263 / 32.291 | -47.904 (-42.71%) / -3.910 (-10.80%) |
| advance MB | 97.634 / 30.515 | 95.208 / 29.369 | -2.426 (-2.48%) / -1.146 (-3.76%) |
| mod_routes_nodes MB | 57.937 / 58.274 | 0.516 / 0.447 | -57.421 (-99.11%) / -57.826 (-99.23%) |
| mod_closures_network MB | 10.209 / 9.879 | 0.229 / 0.550 | -9.980 (-97.76%) / -9.328 (-94.43%) |
| advance_events MB | 49.904 / 8.036 | 47.118 / 7.864 | -2.786 (-5.58%) / -0.172 (-2.14%) |
| advance_planning MB | 45.317 / 20.190 | 45.641 / 19.311 | +0.324 (+0.71%) / -0.879 (-4.35%) |
| physical_spawn_appearance MB | 49.563 / 15.971 | 43.075 / 16.590 | -6.488 (-13.09%) / +0.618 (+3.87%) |
| physical_spawn_grow MB | 37.089 / 0.000 | 0.876 / 0.032 | -36.214 (-97.64%) / +0.032 (n/a; baseline 0) |
| physical_setup_ambient MB | 12.363 / 12.363 | 0.174 / 0.174 | -12.189 (-98.59%) / -12.189 (-98.59%) |
| physical_parked MB | 1.971 / 1.690 | 9.590 / 8.824 | +7.619 (+386.55%) / +7.134 (+422.21%) |
| mod_rlo MB | 3.507 / 3.520 | 3.529 / 3.536 | +0.022 (+0.63%) / +0.016 (+0.45%) |
| gen0 | 8 / 6 | 7 / 6 | -1 / 0 |
| pause p95 ms | 27.888 / 37.301 | 43.403 / 38.929 | +15.515 / +1.628 |

15a street began at 16:04 after its load wait; 15b keeps the city paused through load waits and starts street at 15:30. Shared-host observations, not a controlled A/B comparison. Host boundaries below record other load; this session ran no build/test during measurements. A concurrent Rig-bot perf-attribution session, `20261115-081854-rig-gr5-perf-attribution`, ran on the same Mac from about 08:18 CET (07:18Z), overlapping the second half of gc-night (07:13:08Z to 07:23:10Z) and all of street (07:27:56Z to 07:38:02Z). Night and street timings are shared-host measurements affected by that overlap.

### Every allocating segment and sub-segment

| Segment | Day bytes / samples / skipped | Night bytes / samples / skipped |
| --- | --- | --- |
| mod | 177882992 / 17010 / 0 | 78728440 / 15241 / 0 |
| advance | 95207656 / 17005 / 0 | 29369216 / 15237 / 0 |
| physical | 64262504 / 17011 / 0 | 32290920 / 15242 / 0 |
| physical_ambient | 2179920 / 17005 / 0 | 1953440 / 15237 / 0 |
| physical_vehicles | 1205504 / 17005 / 0 | 462208 / 15237 / 0 |
| physical_bindings | 11872 / 445323 / 0 | 11552 / 286230 / 0 |
| physical_vendor_meals | 0 / 17005 / 0 | 144 / 15237 / 0 |
| physical_fade | 0 / 17005 / 0 | 0 / 15237 / 0 |
| physical_focus | 0 / 17005 / 0 | 0 / 15237 / 0 |
| physical_observe | 314400 / 428037 / 0 | 119712 / 270787 / 0 |
| physical_release | 7904 / 297 / 0 | 3296 / 282 / 0 |
| physical_parked | 9590064 / 5693 / 0 | 8823728 / 5235 / 0 |
| physical_index | 80 / 2212 / 0 | 80 / 2087 / 0 |
| physical_queues | 8960 / 2212 / 0 | 17264 / 2087 / 0 |
| physical_roster | 15024 / 2212 / 0 | 29424 / 2087 / 0 |
| physical_selection | 1402928 / 2774 / 0 | 901600 / 2333 / 0 |
| physical_spawn_pedestrian | 3209472 / 757 / 0 | 424576 / 304 / 0 |
| physical_spawn_appearance | 43075248 / 804 / 0 | 16589824 / 300 / 0 |
| physical_spawn_check | 32 / 536 / 0 | 0 / 200 / 0 |
| physical_spawn_vehicle | 128624 / 73 / 0 | 82352 / 42 / 0 |
| physical_unattributed | 3112472 / 34018 / 0 | 2871720 / 30481 / 0 |
| physical_spawn_randomize | 2404336 / 268 / 0 | 875872 / 100 / 0 |
| physical_spawn_bake | 2775056 / 268 / 0 | 1691520 / 100 / 0 |
| advance_planning | 45640960 / 9461 / 0 | 19310992 / 4438 / 0 |
| advance_events | 47117976 / 21517 / 0 | 7864096 / 17215 / 0 |
| advance_stops | 2448720 / 17005 / 0 | 2194128 / 15237 / 0 |
| advance_other | 0 / 17005 / 0 | 0 / 15237 / 0 |
| physical_spawn_grow | 875744 / 189 / 0 | 32464 / 11 / 0 |
| mod_prelude | 2535600 / 17010 / 0 | 2284992 / 15241 / 0 |
| mod_rlo | 3529184 / 17010 / 0 | 3535808 / 15241 / 0 |
| mod_food | 1277184 / 17010 / 0 | 856576 / 15241 / 0 |
| mod_optional_stops | 2450576 / 17010 / 0 | 2194704 / 15241 / 0 |
| mod_routes | 5363584 / 17010 / 0 | 4924048 / 15241 / 0 |
| mod_closures | 388480 / 17005 / 0 | 713200 / 15237 / 0 |
| mod_hold_replay | 0 / 17005 / 0 | 0 / 15237 / 0 |
| mod_physical_setup | 0 / 16951 / 0 | 0 / 15178 / 0 |
| mod_inspector | 2488160 / 17005 / 0 | 2197744 / 15237 / 0 |
| mod_summary | 362416 / 17003 / 0 | 361104 / 15231 / 0 |
| mod_other | 3808 / 17010 / 0 | 0 / 15241 / 0 |
| mod_routes_speeds | 902080 / 14095 / 0 | 845344 / 13167 / 0 |
| mod_routes_nodes | 516096 / 14095 / 0 | 447488 / 13167 / 0 |
| mod_routes_sweep | 0 / 16998 / 0 | 256 / 15227 / 0 |
| mod_routes_rebuild | 3945408 / 17010 / 0 | 3630960 / 15241 / 0 |
| mod_closures_network | 229088 / 17005 / 0 | 550080 / 15237 / 0 |
| mod_closures_links | 159392 / 17005 / 0 | 159392 / 15237 / 0 |
| advance_events_traffic | 288496 / 2771 / 0 | 225360 / 2205 / 0 |
| advance_events_food | 28296576 / 987 / 0 | 3878448 / 320 / 0 |
| advance_events_citizen | 13546712 / 3729 / 0 | 2119344 / 2107 / 0 |
| advance_events_route_begin | 4986192 / 3379 / 0 | 1640848 / 1547 / 0 |
| advance_events_other | 0 / 10651 / 0 | 64 / 11036 / 0 |
| advance_planning_slice | 29432344 / 6082 / 0 | 11428080 / 2891 / 0 |
| advance_planning_finish | 16208616 / 3379 / 0 | 7882912 / 1547 / 0 |
| physical_setup_places | 273928 / 1 / 0 | 273928 / 1 / 0 |
| physical_setup_bodies | 180032 / 1 / 0 | 206128 / 1 / 0 |
| physical_setup_vehicles | 15696 / 2 / 0 | 15616 / 2 / 0 |
| physical_setup_ambient | 174192 / 2 / 0 | 174192 / 2 / 0 |
| physical_setup_preflight | 2000 / 1 / 0 | 752 / 1 / 0 |
| physical_unattributed_setup | 4240 / 1 / 0 | 4240 / 1 / 0 |
| physical_unattributed_update | 12944 / 17005 / 0 | 2160 / 15237 / 0 |
| physical_unattributed_waiting | 720 / 5 / 0 | 576 / 4 / 0 |
| physical_unattributed_entry | 2448720 / 17005 / 0 | 2194128 / 15237 / 0 |

Sub-segments overlap parents and are not additive. Samples skipped on collection frames are explicit.

- gc-day largest `mod_routes_*`: `mod_routes_rebuild` 3.945 MB, 73.56% of parent / 2.22% of total mod.
- gc-day largest `mod_closures_*`: `mod_closures_network` 0.229 MB, 58.97% of parent / 0.13% of total mod.
- gc-day largest `advance_events_*`: `advance_events_food` 28.297 MB, 60.05% of parent / 15.91% of total mod.
- gc-day largest `advance_planning_*`: `advance_planning_slice` 29.432 MB, 64.49% of parent / 16.55% of total mod.
- gc-day largest `physical_unattributed_*`: `physical_unattributed_entry` 2.449 MB, 78.67% of parent / 1.38% of total mod.
- gc-day largest remaining leaf: `physical_spawn_appearance` 43.075 MB / 24.22% of total mod.
- gc-night largest `mod_routes_*`: `mod_routes_rebuild` 3.631 MB, 73.74% of parent / 4.61% of total mod.
- gc-night largest `mod_closures_*`: `mod_closures_network` 0.550 MB, 77.13% of parent / 0.70% of total mod.
- gc-night largest `advance_events_*`: `advance_events_food` 3.878 MB, 49.32% of parent / 4.93% of total mod.
- gc-night largest `advance_planning_*`: `advance_planning_slice` 11.428 MB, 59.18% of parent / 14.52% of total mod.
- gc-night largest `physical_unattributed_*`: `physical_unattributed_entry` 2.194 MB, 76.40% of parent / 2.79% of total mod.
- gc-night largest remaining leaf: `physical_spawn_appearance` 16.590 MB / 21.07% of total mod.

### Appearance counters and pool hit rate

| Window | AppearanceBuilds / Reuses | reuse / (build+reuse) % | last pedestrianLooks |
| --- | --- | --- | --- |
| gc-day | 233 / 27 | 10.38 | 233 built/27 reused/7455 deferred/35 idle/87 owned/8 adopted/243 baked/45 bake sizes/1678 KB bake scratch/89 grown/11 trimmed/127 fitted/283 waited/4.7 ms build |
| gc-night | 100 / 87 | 46.52 | 100 built/87 reused/1454 deferred/32 idle/32 owned/103 adopted/546 baked/44 bake sizes/1634 KB bake scratch/5 grown/76 trimmed/0 fitted/109 waited/36.7 ms build |
| street | 97 / 29 | 23.02 | 97 built/29 reused/1402 deferred/32 idle/59 owned/69 adopted/762 baked/45 bake sizes/1678 KB bake scratch/31 grown/41 trimmed/1 fitted/156 waited/28.0 ms build |

Last periodic cumulative snapshots, not exact window deltas. 15a reuse: day 9.3%, night 49%, street 21%.

### Timing and every >=30 ms spike

| Window | seconds / frames / achieved fps | UpdateCity self p95 / max ms | mod-work union p95 / max ms / spikes | whole-frame p95 / max ms | NativeCityClock.MaximumLagMinutes |
| --- | --- | --- | --- | --- | --- |
| gc-day | 600.578 / 17015 / 28.33 | 0.3103 / 45.4841 | 3.4543 / 75.7166 / 1 | 38.3859 / 523.1505 | 1.0 |
| gc-night | 600.594 / 15248 / 25.39 | 0.6210 / 40.1614 | 3.7258 / 103.7139 / 16 | 59.2196 / 1786.3961 | 1.0 |
| street | 604.099 / 4250 / 7.04 | 6.8007 / 243.9327 | 6.9556 / 243.9770 / 28 | 269.4354 / 1780.5302 | 1.0 |

Street versus 15a self / union p95 deltas +0.2541 / +0.3006 ms; spike count delta -9.

Each requested controller hold was 600 s. Exported rig durations include command/start/end overhead: 600.578 s day, 600.594 s night and 604.099 s street; the exact figures are above. No save or speed change occurred inside those windows.

GC hooks nest UpdateCity, advance, prepare, physical and inspector. Street re-arms only UpdateCity plus automatic entry points, matching 15a. Self is boundary self time; union counts nested scopes once. Exact inclusive UpdateCity p95 is not exported with nested hooks. Whole-frame timing is separate.

**gc-day: 1 union spikes / 1 exported**

| Frame | union ms | dominant | PC mod ms | phase / ms | detail / ms | sub / sub_ms | whole-frame ms |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 49397 | 75.7166 | updatecity | 75.661 | physical_spawn_pedestrian / 28.647 | physical_spawn_grow / 23.839 | mod_closures_network / 8.059 | 97.712 |

coverage.complete=True; validity=`{"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}`; save_activity=`{"attempts":17028,"allowed":0,"refused":17028,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}`; final lag=0.0, refused=False, stoppedAfterAccountingFailure=False, historicalClockMisses=0.

**gc-night: 16 union spikes / 16 exported**

| Frame | union ms | dominant | PC mod ms | phase / ms | detail / ms | sub / sub_ms | whole-frame ms |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 72630 | 52.7088 | updatecity | 52.656 | mod_closures / 20.77 | physical_spawn_bake / 1.243 | mod_closures_network / 19.28 | 98.3555 |
| 73587 | 57.963 | physical | 57.498 | physical_parked / 16.947 | none / 0 | mod_closures_links / 7.448 | 181.7595 |
| 75013 | 30.8037 | physical | 30.65 | physical_spawn_appearance / 11.408 | physical_spawn_bake / 0.93 | advance_planning_slice / 2.991 | 69.7156 |
| 76446 | 35.4926 | physical | 35.065 | physical_parked / 15.313 | none / 0 | mod_closures_links / 6.32 | 139.3867 |
| 77056 | 40.1306 | physical | 39.141 | physical_parked / 20.764 | none / 0 | mod_closures_links / 2.727 | 396.1585 |
| 78209 | 51.4826 | physical | 51.407 | physical_spawn_appearance / 22.972 | physical_spawn_randomize / 1.686 | advance_events_citizen / 1.286 | 125.0032 |
| 78314 | 42.5335 | physical | 41.06 | physical_parked / 16.041 | none / 0 | mod_closures_links / 5.795 | 274.0854 |
| 79205 | 39.6863 | physical | 39.62 | physical_spawn_pedestrian / 21.009 | physical_spawn_randomize / 4.244 | mod_closures_links / 0.076 | 59.6171 |
| 79792 | 53.714 | physical | 51.935 | physical_parked / 17.354 | none / 0 | mod_closures_links / 5.675 | 412.4827 |
| 80142 | 39.3742 | physical | 38.8 | physical_parked / 15.733 | none / 0 | advance_planning_slice / 4.561 | 283.4511 |
| 80294 | 51.5115 | physical | 51.289 | physical_spawn_appearance / 20.451 | physical_spawn_randomize / 2.351 | advance_planning_slice / 3.689 | 121.9542 |
| 80446 | 83.4719 | physical | 82.662 | physical_parked / 35.994 | none / 0 | mod_closures_links / 3.286 | 355.8196 |
| 80555 | 36.7052 | physical | 36.632 | physical_spawn_pedestrian / 18.99 | physical_spawn_bake / 1.216 | mod_closures_links / 0.062 | 56.5632 |
| 80941 | 55.3435 | updatecity | 53.422 | mod_rlo / 34.939 | none / 0 | mod_closures_links / 0.862 | 168.0956 |
| 81239 | 33.6881 | physical | 33.19 | physical_index / 21.825 | none / 0 | advance_planning_slice / 3.626 | 151.6787 |
| 81278 | 103.7139 | physical | 102.693 | physical_spawn_pedestrian / 32.644 | physical_spawn_bake / 1.093 | advance_events_citizen / 6.068 | 531.3962 |

coverage.complete=True; validity=`{"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}`; save_activity=`{"attempts":15262,"allowed":0,"refused":15262,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}`; final lag=0.0, refused=False, stoppedAfterAccountingFailure=False, historicalClockMisses=0.

**street: 28 union spikes / 28 exported**

| Frame | union ms | dominant | PC mod ms | phase / ms | detail / ms | sub / sub_ms | whole-frame ms |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 95966 | 243.977 | updatecity | 243.938 | mod_routes / 243.762 | none / 0 | mod_routes_rebuild / 231.703 | 316.412 |
| 95981 | 32.6016 | updatecity | 32.56 | mod_closures / 12.912 | physical_spawn_bake / 0.501 | mod_closures_network / 12.859 | 44.2471 |
| 99479 | 42.2777 | updatecity | 41.784 | physical_parked / 17.071 | none / 0 | advance_planning_slice / 3.033 | 1066.6575 |
| 99480 | 33.9268 | updatecity | 33.841 | physical_spawn_appearance / 15.7 | physical_spawn_bake / 1.977 | advance_planning_finish / 6.249 | 375.777 |
| 99569 | 56.5458 | updatecity | 53.382 | physical_parked / 17.078 | none / 0 | advance_planning_slice / 3.008 | 1087.3404 |
| 99570 | 33.587 | updatecity | 33.1 | physical_parked / 9.724 | none / 0 | mod_closures_links / 1.659 | 1114.1763 |
| 99571 | 49.6798 | updatecity | 49.097 | physical_parked / 15.113 | none / 0 | advance_planning_slice / 3.387 | 1234.2803 |
| 99572 | 63.1461 | updatecity | 61.929 | physical_parked / 15.58 | physical_spawn_bake / 1.004 | advance_planning_slice / 1.991 | 457.7759 |
| 99701 | 32.0573 | updatecity | 31.903 | physical_spawn_appearance / 10.983 | physical_spawn_bake / 1.348 | advance_events_citizen / 0.829 | 209.279 |
| 99798 | 38.4312 | updatecity | 37.876 | physical_parked / 13.472 | none / 0 | mod_closures_links / 1.257 | 470.6591 |
| 99824 | 51.185 | updatecity | 51.099 | physical_spawn_appearance / 16.435 | physical_spawn_bake / 2.47 | mod_routes_rebuild / 5.213 | 306.396 |
| 99865 | 33.6128 | updatecity | 33.558 | physical_spawn_pedestrian / 15.019 | physical_spawn_bake / 1.338 | advance_planning_slice / 0.612 | 264.625 |
| 99908 | 34.2392 | updatecity | 34.152 | physical_spawn_appearance / 17.701 | physical_spawn_bake / 2.44 | advance_planning_slice / 1.219 | 221.064 |
| 100000 | 31.2956 | updatecity | 30.951 | physical_parked / 6.393 | none / 0 | mod_closures_links / 4.424 | 857.4386 |
| 100002 | 49.1407 | updatecity | 47.91 | physical_parked / 12.463 | none / 0 | mod_closures_links / 5.707 | 934.0298 |
| 100007 | 46.1745 | updatecity | 46.01 | physical_index / 39.581 | none / 0 | advance_planning_slice / 2.115 | 483.4256 |
| 100008 | 59.8013 | updatecity | 59.365 | physical_parked / 18.709 | none / 0 | advance_planning_slice / 5.215 | 1187.4177 |
| 100009 | 58.7496 | updatecity | 57.23 | physical_parked / 18.622 | none / 0 | advance_planning_slice / 4.588 | 824.5152 |
| 100026 | 43.5348 | updatecity | 43.441 | physical_spawn_appearance / 16.508 | physical_spawn_bake / 2.988 | advance_events_citizen / 2.038 | 281.4201 |
| 100037 | 36.7523 | updatecity | 34.967 | physical_parked / 9.464 | none / 0 | advance_events_food / 3.424 | 475.4609 |
| 100039 | 52.4116 | updatecity | 51.724 | physical_parked / 15.772 | none / 0 | mod_routes_rebuild / 3.688 | 848.213 |
| 100122 | 36.7918 | updatecity | 36.229 | physical_parked / 11.487 | none / 0 | advance_planning_slice / 6.592 | 592.8624 |
| 100166 | 34.6578 | updatecity | 34.345 | physical_roster / 8.26 | none / 0 | advance_events_citizen / 5.726 | 278.4946 |
| 100200 | 68.4221 | updatecity | 67.918 | physical_parked / 32.333 | none / 0 | advance_events_citizen / 2.911 | 583.3443 |
| 100202 | 33.1522 | updatecity | 32.88 | physical_parked / 13.65 | none / 0 | advance_events_citizen / 1.248 | 544.3552 |
| 100203 | 43.5238 | updatecity | 43.363 | physical_release / 26.906 | none / 0 | advance_events_citizen / 2.656 | 1123.8365 |
| 100204 | 66.9448 | updatecity | 66.408 | physical_parked / 19.555 | none / 0 | mod_closures_links / 4.508 | 1780.5302 |
| 100205 | 36.6365 | updatecity | not exported | not exported | not exported | not exported | 465.2791 |

coverage.complete=True; validity=`{"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}`; save_activity=`{"attempts":4259,"allowed":0,"refused":4259,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}`; final lag=0.09945210578462138, refused=False, stoppedAfterAccountingFailure=False, historicalClockMisses=0.

gc-day: largest remaining segment `advance_events` 47.118 MB / 26.49% of mod. Frame counter allocation 1093.960 MB. Routes nodes 0.516 MB / closures network 0.229 MB are far below 15a's 57.9/58.3 and 10.2/9.9 MB: Expected allocation clause met.

gc-day: physical_setup_ambient 0.174 MB; 0 exported >=30 ms setup_ambient frames. Expected no such frame: no such frame exported; full per-sub maximum proof unavailable. Every >=30 ms PC frame is included above; phase/detail/sub retain only their dominant scopes. No weaker pass criterion is substituted.

gc-night: largest remaining segment `advance_planning` 19.311 MB / 24.53% of mod. Frame counter allocation 795.943 MB. Routes nodes 0.447 MB / closures network 0.550 MB are far below 15a's 57.9/58.3 and 10.2/9.9 MB: Expected allocation clause met.

gc-night: physical_setup_ambient 0.174 MB; 0 exported >=30 ms setup_ambient frames. Expected no such frame: no such frame exported; full per-sub maximum proof unavailable. Every >=30 ms PC frame is included above; phase/detail/sub retain only their dominant scopes. No weaker pass criterion is substituted.

Clock-rate snapshot lines: 6 True / 1 False. The first construction reports False; later unchanged-snapshot reuse is reported explicitly. The literal all-clock-rate-lines Expected clause is therefore not fully met. No false line is omitted.

Street worst exported synchronous growth row: none in exported top/spike rows. This is an observed row, not proof of the worst async integration frame: pc gc frames stop exports top mod-work frames and dominant phase/detail/sub only, while Unity async integration is outside this scope. Whole-frame p95/max are reported separately. The requested exact worst async body-growth frame is NOT RUN / tooling gap; 15a synchronous growth was 134.255 ms.

Street `physical_spawn_grow`: 0 >=30 ms PC frames name this phase/detail/sub, 0 with that contribution itself >=30 ms; No >=30 ms contribution exported.

Street `physical_parked`: 16 >=30 ms PC frames name this phase/detail/sub, 1 with that contribution itself >=30 ms; Expected none not met. Rows 100200.

Street `advance_events_traffic`: 0 >=30 ms PC frames name this phase/detail/sub, 0 with that contribution itself >=30 ms; No >=30 ms contribution exported.

Street `mod_rlo`: 0 >=30 ms PC frames name this phase/detail/sub, 0 with that contribution itself >=30 ms; No >=30 ms contribution exported.

No staged-mod version/SHA build line was emitted in Player.log. Package/staged DLL SHA-256 and receipt-validated start establish disk provenance; loaded health confirms AgentRigOps SHA-256. A commit-stamped runtime Persistent City build line is a tooling gap, not invented evidence.


### Host boundaries

**gc-day start, 2026-11-15T06:59:20Z**

```text
7:59  up 1 day, 13:37, 1 user, load averages: 6.48 14.18 16.09
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 78%
PID  %CPU COMM
49418  84.2 suggestd
55063  43.0 Big Ambitions Agent Tests Mac
  444  11.9 coreaudiod
  700   6.7 usernoted
  744   5.8 NotificationCenter
  768   3.1 Finder
  735   2.3 Resolve
```

**gc-day end, 2026-11-15T07:09:23Z**

```text
8:09  up 1 day, 13:47, 1 user, load averages: 3.86 4.54 9.42
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 76%
PID  %CPU COMM
50220 213.8 com.apple.Virtualization.VirtualMachine
55063  90.4 Big Ambitions Agent Tests Mac
49418  68.9 suggestd
50209  38.5 limactl
50359  20.8 ssh.sock [mux]
63431  13.9 node
63433  12.1 node
```

**gc-night start, 2026-11-15T07:13:08Z**

```text
8:13  up 1 day, 13:51, 1 user, load averages: 6.62 6.66 9.25
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 81%
PID  %CPU COMM
63966 116.2 node
49418  90.4 suggestd
55063  64.3 Big Ambitions Agent Tests Mac
50220  15.0 com.apple.Virtualization.VirtualMachine
  444  11.0 coreaudiod
55969   3.2 Python
  735   2.4 Resolve
```

**gc-night end, 2026-11-15T07:23:10Z**

```text
8:23  up 1 day, 14:01, 1 user, load averages: 3.03 5.50 7.56
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 82%
PID  %CPU COMM
49418  76.8 suggestd
55063  71.7 Big Ambitions Agent Tests Mac
  915  17.3 sysmond
  444  10.2 coreaudiod
  756   2.6 Activity Monitor
65223   2.3 mdworker_shared
  735   2.3 Resolve
```

**street start, 2026-11-15T07:27:56Z**

```text
8:27  up 1 day, 14:06, 1 user, load averages: 2.69 3.96 6.27
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 82%
PID  %CPU COMM
49418  91.6 suggestd
55063  54.1 Big Ambitions Agent Tests Mac
  444  10.5 coreaudiod
  756   4.1 Activity Monitor
  735   2.1 Resolve
  644   0.9 audioanalyticsd
  654   0.8 com.apple.DriverKit-AppleBCMWLAN
```

**street end, 2026-11-15T07:38:02Z**

```text
8:38  up 1 day, 14:16, 1 user, load averages: 7.46 7.75 7.69
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 41%
PID  %CPU COMM
68010 112.1 next-server (v16.4.2)
55063  98.8 Big Ambitions Agent Tests Mac
49418  70.4 suggestd
68032  41.6 chrome-headless-shell
50220  24.1 com.apple.Virtualization.VirtualMachine
68031  10.6 chrome-headless-shell
  444  10.2 coreaudiod
```

### Look observations and exact failures

Five fresh native walker handles were selected one per cycle. Player and camera moved from x=215,z=0 to a verified native-walker ground point x=317.531,z=-122.201805 (159.5 m away; configured body radius 120 m), waited 8 s, returned, and waited 5 s. Three old handles (0, 1, 3) returned ops_view_stale_target for both screen_pos get and camera follow; two (2, 4) remained readable. Return steps took 17.0–17.5 s. The bounded reload completed in 86.3 s, with no ops_load_* failure or cold restart; all five old handles then returned ops_view_stale_target. Fourteen 1600x900 captures were inspected. Pair 2 shows an orange-haired figure in dark clothes and pink shoes before and after; pair 4 shows a red-haired figure in a green top and tan lower clothing before and after. Release of those two bodies is not established. Other pairs lose the selected subject or show unrelated dressed figures after return. The purple-shirted player avatar is not counted as a resident. No undressed body was observed. Curbside cars are visible, but saved-curb identities/assignments are not exported; several cars appear raised above the pavement in the stills, without proof of a new regression. The after-reload wide shot shows curbside cars and the player; the old residents cannot be found by identity. The required five-resident same-look proof and saved-curb proof are NOT RUN / tooling gaps, not replaced by these observations. Sparse stills cannot rule out invisible bodies or flashes. Both full process logs have zero occurrences of Appearance meshes unavailable, Empty appearance and Merged body. The first look controller stopped on a refused x=600,z=0 teleport; a second x=0,z=0 probe was also refused. Both exact errors are below; the same graphics process continued from verified ground.


| Player.log string | Headless | Graphics |
| --- | --- | --- |
| Appearance meshes unavailable | 0 | 0 |
| Empty appearance | 0 | 0 |
| Merged body | 0 | 0 |

- `python3 scripts/mac-agent call teleport {"who": "player", "x": 600, "z": 0}` → `No ground found at x 600.0, z 0.0.`.
- `python3 scripts/mac-agent call teleport {"who":"player","x":0,"z":0}` → `No walkable ground at x 0.0, z 0.0: no walkable surface of 1: 0.01 not walkable; walking routes not checked (more than 100 m from the player).`.
- `python3 scripts/mac-agent op screen_pos {"action": "get", "target": {"kind": "walker", "id": "walker-6a5ebe62644c40019dd0616177a74aa5"}}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "d3d554db09714904b35d52e18931ef1d",     "session": "5d8d023ee92c4a4d91dd8de93a54d341",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op camera {"action": "follow", "target": {"kind": "walker", "id": "walker-6a5ebe62644c40019dd0616177a74aa5"}, "offset": {"x": 0, "y": 4, "z": -6}, "seconds": 10}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "4a47cc495c514169ad796cd72c4d0bba",     "session": "5d8d023ee92c4a4d91dd8de93a54d341",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos {"action": "get", "target": {"kind": "walker", "id": "walker-c57f0de75abe4349b25fa1d6d93e2fb8"}}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "1df69fa9622449418c8032bd3529a25f",     "session": "5d8d023ee92c4a4d91dd8de93a54d341",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op camera {"action": "follow", "target": {"kind": "walker", "id": "walker-c57f0de75abe4349b25fa1d6d93e2fb8"}, "offset": {"x": 0, "y": 4, "z": -6}, "seconds": 10}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "8857d543caec4292837313be63e951d1",     "session": "5d8d023ee92c4a4d91dd8de93a54d341",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos {"action": "get", "target": {"kind": "walker", "id": "walker-0df27f709b8b47d883713fe64dc67a54"}}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "f8e47819d8ed4210850d9fe0b0703b2d",     "session": "5d8d023ee92c4a4d91dd8de93a54d341",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op camera {"action": "follow", "target": {"kind": "walker", "id": "walker-0df27f709b8b47d883713fe64dc67a54"}, "offset": {"x": 0, "y": 4, "z": -6}, "seconds": 10}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "3eb1c8ee1c60440e831b059fde748a03",     "session": "5d8d023ee92c4a4d91dd8de93a54d341",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos {"action": "get", "target": {"kind": "walker", "id": "walker-6a5ebe62644c40019dd0616177a74aa5"}}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "b904d87ff5ec4775b186137022273719",     "session": "5d8d023ee92c4a4d91dd8de93a54d341",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos {"action": "get", "target": {"kind": "walker", "id": "walker-c57f0de75abe4349b25fa1d6d93e2fb8"}}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "8126d8d3452949e4a5b7f58bbdeaca23",     "session": "5d8d023ee92c4a4d91dd8de93a54d341",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos {"action": "get", "target": {"kind": "walker", "id": "walker-11a2edc64c084945b8103b238054b351"}}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "840e140fa9c249ca9430c370f60c33b8",     "session": "5d8d023ee92c4a4d91dd8de93a54d341",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos {"action": "get", "target": {"kind": "walker", "id": "walker-0df27f709b8b47d883713fe64dc67a54"}}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "237d76a85ae34488b506456796837fdf",     "session": "5d8d023ee92c4a4d91dd8de93a54d341",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos {"action": "get", "target": {"kind": "walker", "id": "walker-178c8978a7684675ac3d8ab7da08ad04"}}` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "5bec3851a29a47ec8a85352b47425a97",     "session": "5d8d023ee92c4a4d91dd8de93a54d341",     "error": "ops_view_stale_target"   } }`.
No game runtime stop occurred; AccountingDiagnostics remained refused=false / stoppedAfterAccountingFailure=false in all three measured windows. The only controller stop was the refused-ground look step, resumed in the same graphics process.

### Player.log activation, build, speed and toggle excerpts

```text
[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:PersistentCity] Road snapshot started: frame=41095, reason=clock-rate index=-1, scene searches in this planner=1, reused unchanged snapshot=False.
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 15:30.
[Mod:PersistentCity] Road snapshot started: frame=41323, reason=clock-rate index=-1, scene searches in this planner=0, reused unchanged snapshot=True.
[Mod:FoodTrucks] Test bridge: run_until day 2 15:30 reached at day 2 15:30; paused, speed 1.
[Mod:PersistentCity] Road snapshot started: frame=49382, reason=clock-rate index=-1, scene searches in this planner=0, reused unchanged snapshot=True.
[Mod:FoodTrucks] Test bridge: console 'pc gc start' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (3 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 01:00.
[Mod:PersistentCity] Road snapshot started: frame=66444, reason=clock-rate index=-1, scene searches in this planner=0, reused unchanged snapshot=True.
[Mod:FoodTrucks] Test bridge: run_until day 3 01:00 reached at day 3 01:00; paused, speed 1.
[Mod:PersistentCity] Road snapshot started: frame=72615, reason=clock-rate index=-1, scene searches in this planner=0, reused unchanged snapshot=True.
[Mod:FoodTrucks] Test bridge: console 'pc gc start' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (3 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (3 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 15:30.
[Mod:PersistentCity] Road snapshot started: frame=87918, reason=clock-rate index=-1, scene searches in this planner=0, reused unchanged snapshot=True.
[Mod:FoodTrucks] Test bridge: run_until day 3 15:30 reached at day 3 15:30; paused, speed 1.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc frames start' (1 lines).
[Mod:PersistentCity] Road snapshot started: frame=95966, reason=clock-rate index=-1, scene searches in this planner=0, reused unchanged snapshot=True.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc frames stop' (2 lines).
```

Player.log has no explicit speed=1 marker; these bridge replies record every measured-window speed reset. No RLO toggle occurred. SaveCostDiagnostics remained off.

| Window speed=1 command reply | Autosave before / after suppressed |
| --- | --- |
| gc-day `{"day":2,"weekday":"Tuesday","time":"15:30","paused":false,"speed":1.0,"run_until":null,"arrived_at":null,"keep_needs":true}` | True / True |
| gc-night `{"day":3,"weekday":"Wednesday","time":"01:00","paused":false,"speed":1.0,"run_until":null,"arrived_at":null,"keep_needs":true}` | True / True |
| street `{"day":3,"weekday":"Wednesday","time":"15:30","paused":false,"speed":1.0,"run_until":null,"arrived_at":null,"keep_needs":true}` | True / True |

### NOT RUN grouped

- **GR-5 60 fps arms:** excluded; Mac ~5 fps at 1,000 residents; agent-rig #90 installed only on Mac, PC offline. No Windows contact.
- **GR-5 save latency:** excluded, owner decision pending.
- **#262 checks 4–5 and #246 check 4 full proof:** excluded, Rig bot fixture/observation gaps.
- **Anything else:** outside these four checks, including standalone idle-tick and simulation-equivalence proof. No release, Workshop, production-code, version/changelog, schema or food-weight change.

Tooling gaps: native walker handles do not identify persistent residents or expose body release/reacquire; no saved-curb identity readback; inclusive UpdateCity p95 under nested hooks; async Unity body integration cost is outside the synchronous growth scope. Exact invoked errors are above.

### Cleanup and validation

Stopped only owned players. Restored complete ModsLocal/PersistentCity, exact install.json, isolated preferences/config and absent paths. 35 original hashes, 3 absent paths, PersistentCity file set and 53 read-only RLO hashes verified; mismatches=[]. AgentRigOps unchanged. Receipt SHA-256 `ad7a41740c9d5d53dd281b45add47795b01fa34a3a76f0ba96efc595707c7864`. Original autosave settings restored; process guard ends at stop. Lease released, running=false / held=false / pending=0; owned player and caffeinate processes gone. No fixture save was written.

Candidate build passed. Evidence validation: docs-links, markdown-ban and git diff --check; compact JSON parses and stays under 20 KB. No test code changed; no full/regression suite run. Isolated worktree removed after evidence commit, push and PR creation.

Refs #275

## Rig results — 15 November 2026 c, Mac (GR-5 #246 check 4 via #91, then GC/street after #275 at 39bd8e0)

**RESULT: DONE. Counts: 0 passed / 0 partial / 4 failed / 4 NOT RUN groups.** D ran first, then day/night GC and headless street, each held for 600 s. Check-4 reload adoption failed. Timing is **provisional (Mac shared)**. In-game proof gates no merge; leave this docs-only PR unmerged.

Role `rig_test`, where `mac`, isolated `agentmac`; owner `persistent-city-rig-2026-11-15c-mac`. `<stage>` = `<home>/HART-Builds/persistent-city-rig-2026-11-15c-mac`. Raw evidence, replies, logs, controllers and backups remain outside Git there. Report copy: `<home>/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-15c-mac.md`. MB is decimal.

[Compact evidence](perf/gr5-2026-11-15c-mac.json): 10501 bytes, SHA-256 `65073f26c88982906425504093d269f5144aba05f93a3d190bf3edda0cc97b38`. Raw evidence manifest `<stage>/raw-evidence-manifest.json`, SHA-256 `479aec804818b2af6bc006f9d0f5bb1476574e7e51360b421e495444ad40aa24`.

### Build and rig

| Item | Evidence |
| --- | --- |
| Candidate | `39bd8e08d2ac58283f36ac269ea41751143e3a69`. Initial fetch moved origin/codex/persistent-city from requested `72b2bb51` to `39bd8e0` (15b docs merge); final fetched tip `39bd8e08d2ac58283f36ac269ea41751143e3a69`. The tested DLL remains the initial candidate. Its hash equals 15b because only documentation changed. |
| Worktree / branch | New `rig-2026-11-15c-mac-evidence` / `codex/mac-rig-2026-11-15c`; built there. Original checkout unchanged. |
| Official agent-rig HEAD | `61bc2e23038874f2295adf8540dc54a9a62607ee` (#91); clean, fetch and ff-only pull already current. No edit, build, installation update or overlay rollback. |
| AgentRigOps SHA-256 | `9a9a24f9c5b4f812233a1e710d02512c2b62bf2b638d51363afb11e6984761b4` before start; loaded health and final verification retained. |
| PersistentCity DLL SHA-256 | `3141419552653a7b072b6d4b71c5982f2daecb1dd9b2c2219598909b7f2524e4` built/package/staged. Receipt-validated start accepted the candidate; no commit/SHA-stamped PC runtime build line is emitted. Activation lines below. |
| Build | `PATH=/opt/homebrew/bin:$PATH`; `DOTNET_ROOT` from Homebrew dotnet; `python3 tools/build.py --game-managed "<home>/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0. No wait-other-dotnet helper or documented Mac equivalent found. No RLO/game DLL packaged. |
| Stage | `python3 scripts/install-mac.py --stage-mod PersistentCity <home>/HART-Builds/persistent-city-rig-2026-11-15c-mac/package`; game stopped, lease held. Receipt backup `<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261115T081455Z`; 28 entries refreshed. |
| Process | Headless `--rig-test` PID 81704; same player for D, C and street. No cold restart or ops_load_* failure. |
| RLO | Player.log: Real Life Overhaul 1.0.6 initialised. living_population absent/live=true; social_media=1/true; social_media_notices=0/false. No RLO option or file changed. |
| Autosave / SaveCost | Original native autosave 5 minutes / 3 saves, prevent_autosave=false. #70 process suppression verified after loads and before/after each window. SaveCostDiagnostics requested=false, writerSchema=1; native save profiler installed=false. D explicitly saved only its own disposable name; no save ran inside timing. |
| Lease / resources | Free/stopped initially; acquired 2026-11-15T08:14:02.670592Z. Initial 45.4 GiB free / 82% reported memory free. No Windows contact, other-session intervention or macOS setting change. |
| Host qualification | All three windows provisional (Mac shared). Live sessions and host boundaries below; this rig session ran no build/test during timing. |

Printed rollback, placeholder-normalized (`<home>` replaces the home prefix; stopped, owned):

```sh
rm -rf "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261115T081455Z/mod" "<home>/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261115T081455Z/install.json" "<home>/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

Cleanup used the equivalent move-aside/copy-back form: retained the staged candidate outside the profile, replaced the complete mod folder from the receipt backup and restored exact install.json.

### D first: #246 check 4 and #91 fixture flow

**FAIL.** #246 Expected: "footprint within about 1.5x of the visible ground area and floors within one of the visible storeys." Read/list geometry and derived-storey checks ran, but independent visible-storey and per-Home saved area evidence are not established by these headless reads. The required reload flow independently failed; no weaker pass criterion is substituted.

1. Created and loaded exactly `FT RIGTMP-GR5-15c`, no throwaway flag, then paused.
2. Applied under the lease: applied=true, sidecar=true, restored_on_load=false; all five roles supplied, missing=[].
3. In-load list: 585 residential, 5 unrentable = housed 3 / unreadable 1 / empty 1; all five pc_home_candidate=true. One rotated and one yard subject. The mod tally is **not observable in-load** after apply: its earlier scan logged housed=0 beds=0 unreadable=0 empty=0.
4. `save {"action":"trigger","type":"Default","name":"FT RIGTMP-GR5-15c","overwrite":true}` completed; waited for save/compression to settle.
5. Bounded `mac-agent load` of that exact name completed, then autosave verified and paused.
6. Immediate `unrentable_fixture status` returned `{"applied":false,"save":"FT RIGTMP-GR5-15c","restored_on_load":false,"sidecar":true,"subjects":[],"missing":[]}`. List reported housed=5 / unreadable=0 / empty=0; the mod logged `unrentable homes housed=5 beds=1834 unreadable=0 empty=0`. All five roles were **not** visible: both disabled-collider classes became housed. No `ops_building_fixture_reload_incomplete` error was emitted. This was an observed fixture reload failure, not a missing-native-subject exception.
7. Revert succeeded. Its before snapshot shows late adoption (applied=true, restored_on_load=true), too late for the mod scan; after: applied=false, restored_on_load=false, sidecar=false. Original sizes and usable colliders returned for all five; unrentable list returned 0. Save deletion is NOT RUN / tooling gap: installed `save` schema has read/profile/trigger, with no documented delete op. The reverted disposable save is retained in the isolated profile; no hand deletion or other fixture deletion.

| Role | Place | Original size | apply class / reload class | apply derived storeys | restored colliders |
| --- | --- | --- | --- | --- | --- |
| housed_rotated | `address:ba%3Astreet_firststreet:22:home` | ba:buildingsize_l | housed / housed | 13 | 59 |
| housed_yard | `address:ba%3Astreet_fifthavenue:25:home` | ba:buildingsize_b | housed / housed | 5 | 2 |
| housed_plain | `address:ba%3Astreet_broadwaystreet:2:home` | ba:buildingsize_l | housed / housed | 11 | 2 |
| unreadable | `address:ba%3Astreet_broadwaystreet:13:home` | ba:buildingsize_n | unreadable / housed | None | 2 |
| empty | `address:ba%3Astreet_broadwaystreet:23:home` | ba:buildingsize_n | empty / housed | 0 | 2 |

Read/list storeys use `derived:Floor(bounds.height_m / 3.5)`. Equality held for every readable subject; the unreadable subject returned exact `ops_building_bounds_unavailable`, with list storeys=null. No native storey count was invented; `ops_building_storeys_not_native` did not occur.

| Subject | collider local / world AABB m² | renderer world AABB m² | collider height m / derived storeys | world collider / renderer area |
| --- | --- | --- | --- | --- |
| housed_rotated | 1431.112 / 1488.514 | 1493.488 | 48.719 / 13 | 0.9967 |
| housed_yard | 426.709 / 426.709 | 437.253 | 18.389 / 5 | 0.9759 |
| housed_plain | 3826.154 / 3826.156 | 3826.155 | 40.332 / 11 | 1.0000 |

Collider/renderer AABB area ratios fall within 1.5x for these three subjects. Collider heights rotated/yard/plain are 48.719 / 18.389 / 40.332 m; renderer heights 49.920 / 23.514 / 47.418 m. Applying the same 3.5 m divisor to renderer heights would give 14 / 6 / 13 versus collider-derived 13 / 5 / 11, so the plain subject differs by two on that proxy. Renderer height is not an independent count of visible storeys; the literal visual floor gate remains unverified. Yard read meets_yard_or_sidewalk=true before and after reload.

| List | matched | geometry_reads / slices | client timeout |
| --- | --- | --- | --- |
| D-yard | 1 | 5 / 1 | 120 s |
| D-whole-yard | 108 | 585 / 24 | 120 s |
| D-reloaded-yard | 1 | 5 / 1 | 120 s |
| D-reloaded-whole-yard | 108 | 585 / 24 | 120 s |

No scan timeout occurred. Sealed timing fixture loaded only after revert: 585 residential / 0 unrentable, so the disposable changes did not enter C/street.

### Checks 1–3 Expected lines and gates

> 1. **Day GC window.** Setup: load the fixture, clock 15:30, idle camera. Steps: run a 600 s gc window. Expected:
>    - Report gen0, pause p95, heap, and the mod, physical and advance totals, plus every segment and sub-segment against rig 15a (mod 295.6 MB, gen0 8, pause p95 27.9 ms).
>    - No `physical_setup_ambient` ≥30 ms frame. `mod_routes_nodes` and `mod_closures_network` far below 57.9 / 10.2 MB.
>    - "reused unchanged snapshot=True" for any clock-rate rebuild in Player.log.
> 2. **Night GC window.** Same as check 1 at 01:00, against 15a night (151.5 MB, gen0 6, pause p95 37.3 ms).
> 3. **Street window.** Setup: same fixture, daytime, pan the camera. Steps: run a 600 s street window. Expected:
>    - Report UpdateCity self/union and mod-work p95 against 15a (6.5466 / 6.6550 ms).
>    - Report every ≥30 ms spike with phase, detail and sub. None from `physical_spawn_grow`, `physical_parked` or `advance_events_traffic`.
>    - Report whole-frame p95/max and the worst body-growth frame separately from mod work (15a: 134 ms growth frame).

- **1. Day 15:30, 600 s: FAIL, provisional (Mac shared).** gen0 6 (<=5: not met); pause p95 110.570 ms (<36: not met); mod 272.206 MB (<=209: not met). Reporting and allocation/phase evidence below.
- **2. Night 01:00 after speed 20, 600 s: FAIL, provisional (Mac shared).** gen0 4 (<=5: met); pause p95 4651.381 ms (<36: not met); mod 66.856 MB (<=111: met). Reporting and allocation/phase evidence below.
- **3. Street 600 s: FAIL, provisional (Mac shared).** UpdateCity self / union p95 16.8084 / 17.2191 ms; strict <5 ms not met. 114 >=30 ms union spikes; no-spike gate not met. Spikes are reported, not chased.

Fixture `FT GR5 13f Timing v1`, 1,000 residents / 2,000 visitors / 1,683 places, player x=215,z=0. No fallback. GC sampler off for street; pans every minute. Speed 1 in timing, speed 20 only in positioning. No save latency measurement. `boehm_total`, incremental collector, max_generation=0; pause is cycle-ending frame excess rather than all incremental collector work.

| GC window | frames / dropped | gen0/1/2 | pause p50 / p95 / max ms | heap_after_gc_bytes | mono_heap_bytes | mod / physical / advance MB |
| --- | --- | --- | --- | --- | --- | --- |
| gc-day | 16400 / 0 | 6/0/0 | 45.507 / 110.570 / 110.570 | 1634745002 | 1955135488 | 272.206 / 59.968 / 87.318 |
| gc-night | 9131 / 0 | 4/0/0 | 105.600 / 4651.381 / 4651.381 | 1615849472 | 1810857984 | 66.856 / 24.582 / 26.937 |

| Metric (day / night) | 15a (#274) | 15b (#276) | 15c (Mac shared) |
| --- | --- | --- | --- |
| mod MB | 295.628 / 151.470 | 177.883 / 78.728 | 272.206 / 66.856 |
| physical MB | 112.167 / 36.201 | 64.263 / 32.291 | 59.968 / 24.582 |
| advance MB | 97.634 / 30.515 | 95.208 / 29.369 | 87.318 / 26.937 |
| routes_nodes MB | 57.937 / 58.274 | 0.516 / 0.447 | 3.472 / 3.368 |
| closures_network MB | 10.209 / 9.879 | 0.229 / 0.550 | 0.229 / 0.327 |
| advance_events MB | 49.904 / 8.036 | 47.118 / 7.864 | 44.427 / 8.015 |
| advance_planning MB | 45.317 / 20.190 | 45.641 / 19.311 | 40.531 / 17.610 |
| spawn_appearance MB | 49.563 / 15.971 | 43.075 / 16.590 | 39.325 / 13.168 |
| spawn_grow MB | 37.089 / 0.000 | 0.876 / 0.032 | 0.700 / 0.003 |
| setup_ambient MB | 12.363 / 12.363 | 0.174 / 0.174 | 0.175 / 0.175 |
| parked MB | 1.971 / 1.690 | 9.590 / 8.824 | 9.374 / 6.317 |
| gen0 | 8.000 / 6.000 | 7.000 / 6.000 | 6.000 / 4.000 |
| pause p95 ms | 27.888 / 37.301 | 43.403 / 38.929 | 110.570 / 4651.381 |

15a, 15b and 15c are all shared-host measurements, not a controlled A/B comparison; their differences in this table and in the street deltas below cannot establish a product regression. 15b night overlapped `20261115-081854-rig-gr5-perf-attribution` after 08:18 CET, and street overlapped it throughout; those timings may be skewed. 15c is also shared and is not a clean baseline. 15a street began at 16:04 after a wait; 15b/15c target 15:30. The twelfth-fix testhost PID 86541 (parent 86539, dotnet test parent 86535) was active in the final minute of day; retained `build-attribution-before-night.json` names its worktree and command. No completed PC 15c report exists: that run was stopped mid-measurement when Peter started gaming.

### Every segment and sub-segment

| Segment | Day bytes / samples / skipped | Night bytes / samples / skipped |
| --- | --- | --- |
| mod | 272206392 / 16401 / 0 | 66856048 / 9132 / 0 |
| advance | 87318128 / 16386 / 0 | 26936608 / 9107 / 0 |
| physical | 59968288 / 16402 / 0 | 24581896 / 9133 / 0 |
| physical_ambient | 2210848 / 16386 / 0 | 1167472 / 9107 / 0 |
| physical_vehicles | 1380832 / 16386 / 0 | 516784 / 9107 / 0 |
| physical_bindings | 15696 / 403635 / 0 | 10976 / 262422 / 0 |
| physical_vendor_meals | 0 / 16386 / 0 | 0 / 9107 / 0 |
| physical_fade | 0 / 16386 / 0 | 0 / 9107 / 0 |
| physical_focus | 0 / 16386 / 0 | 0 / 9107 / 0 |
| physical_observe | 290448 / 386981 / 0 | 131872 / 253138 / 0 |
| physical_release | 8912 / 286 / 0 | 2864 / 252 / 0 |
| physical_parked | 9374496 / 5563 / 0 | 6316736 / 3726 / 0 |
| physical_index | 80 / 2189 / 0 | 80 / 1689 / 0 |
| physical_queues | 8960 / 2189 / 0 | 17264 / 1689 / 0 |
| physical_roster | 15712 / 2189 / 0 | 29888 / 1689 / 0 |
| physical_selection | 1572936 / 2699 / 0 | 1045304 / 1900 / 0 |
| physical_spawn_pedestrian | 2553728 / 675 / 0 | 136552 / 247 / 0 |
| physical_spawn_appearance | 39324624 / 729 / 0 | 13168064 / 234 / 0 |
| physical_spawn_check | 0 / 486 / 0 | 0 / 156 / 0 |
| physical_spawn_vehicle | 191472 / 78 / 0 | 51776 / 42 / 0 |
| physical_unattributed | 3019544 / 32790 / 0 | 1986264 / 18242 / 0 |
| physical_spawn_randomize | 2175840 / 243 / 0 | 685536 / 78 / 0 |
| physical_spawn_bake | 2773680 / 243 / 0 | 1606768 / 78 / 0 |
| advance_planning | 40531288 / 10071 / 0 | 17610128 / 5748 / 0 |
| advance_events | 44427256 / 20857 / 0 | 8015072 / 9372 / 0 |
| advance_stops | 2359584 / 16386 / 0 | 1311408 / 9107 / 0 |
| advance_other | 0 / 16386 / 0 | 0 / 9107 / 0 |
| physical_spawn_grow | 699776 / 164 / 0 | 3328 / 6 / 0 |
| mod_prelude | 2447440 / 16401 / 0 | 1334304 / 9132 / 0 |
| mod_rlo | 3529728 / 16401 / 0 | 3514496 / 9132 / 0 |
| mod_food | 1277184 / 16401 / 0 | 429440 / 9132 / 0 |
| mod_optional_stops | 2362304 / 16401 / 0 | 1315200 / 9132 / 0 |
| mod_routes | 112142776 / 16401 / 0 | 6584968 / 9132 / 0 |
| mod_closures | 388528 / 16386 / 0 | 490976 / 9107 / 0 |
| mod_hold_replay | 0 / 16386 / 0 | 0 / 9107 / 0 |
| mod_physical_setup | 0 / 16380 / 0 | 0 / 9105 / 0 |
| mod_inspector | 2395008 / 16386 / 0 | 1311408 / 9107 / 0 |
| mod_summary | 362032 / 16385 / 0 | 356752 / 9106 / 0 |
| mod_other | 4224 / 16401 / 0 | 0 / 9132 / 0 |
| mod_routes_speeds | 862032 / 13452 / 0 | 376416 / 5878 / 0 |
| mod_routes_nodes | 3472216 / 13452 / 0 | 3368392 / 5878 / 0 |
| mod_routes_sweep | 0 / 16386 / 0 | 704 / 9107 / 0 |
| mod_routes_rebuild | 105472800 / 16401 / 0 | 2839456 / 9132 / 0 |
| mod_closures_network | 229264 / 16386 / 0 | 327200 / 9107 / 0 |
| mod_closures_links | 159264 / 16386 / 0 | 159696 / 9107 / 0 |
| advance_events_traffic | 20928 / 1680 / 0 | 46784 / 917 / 0 |
| advance_events_food | 26465728 / 927 / 0 | 3878400 / 230 / 0 |
| advance_events_citizen | 12454296 / 3504 / 0 | 2012320 / 1781 / 0 |
| advance_events_route_begin | 5486304 / 3873 / 0 | 2077568 / 2029 / 0 |
| advance_events_other | 0 / 10873 / 0 | 0 / 4415 / 0 |
| advance_planning_slice | 25577880 / 6197 / 0 | 10809488 / 3720 / 0 |
| advance_planning_finish | 14953408 / 3874 / 0 | 6800640 / 2028 / 0 |
| physical_setup_places | 273928 / 1 / 0 | 273928 / 1 / 0 |
| physical_setup_bodies | 179600 / 1 / 0 | 206128 / 1 / 0 |
| physical_setup_vehicles | 15968 / 2 / 0 | 11664 / 2 / 0 |
| physical_setup_ambient | 174544 / 2 / 0 | 174544 / 2 / 0 |
| physical_setup_preflight | 800 / 1 / 0 | 752 / 1 / 0 |
| physical_unattributed_setup | 4240 / 1 / 0 | 4240 / 1 / 0 |
| physical_unattributed_update | 7504 / 16386 / 0 | 0 / 9107 / 0 |
| physical_unattributed_waiting | 3376 / 15 / 0 | 3600 / 25 / 0 |
| physical_unattributed_entry | 2359584 / 16386 / 0 | 1311408 / 9107 / 0 |

Sub-segments overlap parents and must not be summed. Skipped collection-frame samples are explicit.

- gc-day largest remaining segment: `mod_routes` 112.143 MB / 41.20% of mod.
- gc-day largest remaining leaf: `mod_routes_rebuild` 105.473 MB / 38.75% of mod.
- gc-day largest `mod_routes_*`: `mod_routes_rebuild` 105.473 MB / 94.05% of parent / 38.75% of mod.
- gc-day largest `mod_closures_*`: `mod_closures_network` 0.229 MB / 59.01% of parent / 0.08% of mod.
- gc-day largest `advance_events_*`: `advance_events_food` 26.466 MB / 59.57% of parent / 9.72% of mod.
- gc-day largest `advance_planning_*`: `advance_planning_slice` 25.578 MB / 63.11% of parent / 9.40% of mod.
- gc-day largest `physical_unattributed_*`: `physical_unattributed_entry` 2.360 MB / 78.14% of parent / 0.87% of mod.
- gc-night largest remaining segment: `advance_planning` 17.610 MB / 26.34% of mod.
- gc-night largest remaining leaf: `physical_spawn_appearance` 13.168 MB / 19.70% of mod.
- gc-night largest `mod_routes_*`: `mod_routes_nodes` 3.368 MB / 51.15% of parent / 5.04% of mod.
- gc-night largest `mod_closures_*`: `mod_closures_network` 0.327 MB / 66.64% of parent / 0.49% of mod.
- gc-night largest `advance_events_*`: `advance_events_food` 3.878 MB / 48.39% of parent / 5.80% of mod.
- gc-night largest `advance_planning_*`: `advance_planning_slice` 10.809 MB / 61.38% of parent / 16.17% of mod.
- gc-night largest `physical_unattributed_*`: `physical_unattributed_entry` 1.311 MB / 66.02% of parent / 1.96% of mod.

### Appearance counters and pool hit rate

| Window | AppearanceBuilds / Reuses | reuse / (build+reuse) % | last pedestrianLooks |
| --- | --- | --- | --- |
| gc-day | 231 / 24 | 9.41 | 231 built/24 reused/8389 deferred/31 idle/70 owned/7 adopted/252 baked/45 bake sizes/1678 KB bake scratch/78 grown/16 trimmed/149 fitted/237 waited/6.7 ms build |
| gc-night | 78 / 86 | 52.44 | 78 built/86 reused/708 deferred/30 idle/34 owned/106 adopted/532 baked/43 bake sizes/1552 KB bake scratch/3 grown/75 trimmed/0 fitted/82 waited/77.9 ms build |
| street | 53 / 26 | 32.91 | 53 built/26 reused/121 deferred/31 idle/56 owned/84 adopted/729 baked/44 bake sizes/1666 KB bake scratch/11 grown/39 trimmed/0 fitted/73 waited/57.6 ms build |

Last periodic cumulative snapshots, not exact boundary deltas. 15a reuse day/night/street 9.3% / 49% / 21%; 15b 10.38% / 46.52% / 23.02%.

### Timing and every >=30 ms spike

| Window (all provisional, Mac shared) | seconds / frames / fps | UpdateCity self p95 / max ms | mod-work union p95 / max ms / spikes | whole-frame p95 / max ms | MaximumLagMinutes |
| --- | --- | --- | --- | --- | --- |
| gc-day | 601.215 / 16408 / 27.29 | 0.2901 / 158.5686 | 3.3813 / 159.1223 / 5 | 46.5869 / 936.9279 | 1.0 |
| gc-night | 605.326 / 9135 / 15.09 | 1.9531 / 274.3132 | 9.6616 / 348.5835 / 93 | 137.9068 / 5399.7441 | 1.0 |
| street | 605.862 / 3387 / 5.59 | 16.8084 / 121.1979 | 17.2191 / 122.1744 / 114 | 441.9586 / 4310.7739 | 1.0 |

15a street self/union p95 6.5466 / 6.6550 ms, 37 spikes; 15b 6.8007 / 6.9556 ms, 28 spikes (shared after 08:18 CET). 15c self/union deltas versus 15a +10.2618 / +10.5641 ms. All three street runs are shared-host measurements, so these deltas are not a controlled A/B result and cannot establish a product regression.

Each controller hold was 600 s; exported durations include begin/end command overhead. The first controller was interrupted only in the paused pre-night CPU-load wait, then resumed in the same game process at 01:00 after confirming memory headroom. No measurement window was interrupted; subsequent windows use memory headroom and shared-load qualification rather than a CPU quietness wait. GC nested hooks cover UpdateCity, advance, prepare, physical and inspector. Street arms UpdateCity plus automatic entry points, as in 15a/15b. Self excludes nested scopes; union counts them once. Exact inclusive UpdateCity p95 under nested GC hooks is not exported. Whole-frame timing is separate.

**gc-day: 5 union spikes / 5 exported**

PC phase recorder counted 5 spikes and exported 5 spike rows; 0 union rows lack PC phase/detail/sub. No phase-row retention gap in this window. Every available union row follows; missing fields are explicit.

| Frame | union ms | dominant | PC mod ms | phase / ms | detail / ms | sub / sub_ms | whole-frame ms |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 6037 | 159.1223 | updatecity | 159.084 | mod_routes / 158.914 | none / 0 | mod_routes_rebuild / 157.414 | 209.2313 |
| 17532 | 48.4858 | updatecity | 48.245 | mod_rlo / 17.585 | none / 0 | advance_events_citizen / 12.073 | 350.627 |
| 19743 | 57.8051 | physical | 57.745 | physical_spawn_appearance / 54.585 | physical_spawn_bake / 0.626 | mod_closures_links / 0.099 | 95.6226 |
| 22205 | 32.4995 | physical | 31.789 | physical_spawn_appearance / 8.476 | physical_spawn_bake / 2.408 | advance_events_citizen / 0.356 | 555.8124 |
| 22330 | 33.2525 | physical | 32.892 | physical_spawn_appearance / 8.781 | physical_spawn_randomize / 1.395 | mod_routes_speeds / 1.863 | 171.0061 |

coverage.complete=True; validity=`{"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}`; save_activity=`{"attempts":10462,"allowed":0,"refused":10462,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}`; final lag=0.0, refused=False, stoppedAfterAccountingFailure=False, historicalClockMisses=0.

**gc-night: 93 union spikes / 93 exported**

PC phase recorder counted 89 spikes and exported 64 spike rows; 24 union rows lack PC phase/detail/sub. Full attribution is NOT RUN / tooling gap: the successful reply exposes the count/list retention gap. Every available union row follows; missing fields are explicit.

| Frame | union ms | dominant | PC mod ms | phase / ms | detail / ms | sub / sub_ms | whole-frame ms |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 41688 | 175.9541 | updatecity | 175.893 | advance_events / 41.882 | physical_spawn_bake / 1.361 | advance_events_food / 41.881 | 232.5911 |
| 42603 | 31.5276 | physical | 31.168 | physical_roster / 10.698 | none / 0 | mod_closures_links / 1.876 | 139.9262 |
| 42687 | 31.1511 | physical | 31.096 | physical_spawn_appearance / 17.4 | physical_spawn_randomize / 1.692 | advance_planning_slice / 1.184 | 120.3375 |
| 43140 | 86.2236 | physical | 85.802 | physical_spawn_appearance / 35.296 | physical_spawn_bake / 5.699 | advance_planning_slice / 2.166 | 242.2557 |
| 43204 | 40.2667 | physical | 40.223 | physical_spawn_appearance / 17.513 | physical_spawn_bake / 2.111 | advance_planning_slice / 1.618 | 86.278 |
| 43486 | 49.8938 | physical | 49.538 | physical_spawn_appearance / 12.942 | physical_spawn_bake / 0.439 | mod_closures_links / 1.146 | 118.7931 |
| 43661 | 60.2059 | updatecity | 59.99 | mod_rlo / 29.086 | physical_spawn_bake / 0.851 | mod_closures_links / 1.023 | 113.1869 |
| 43853 | 48.4603 | physical | 48.227 | physical_spawn_appearance / 18.604 | physical_spawn_bake / 2.396 | advance_planning_slice / 5.788 | 178.5987 |
| 43877 | 38.417 | physical | 38.368 | physical_spawn_appearance / 10.99 | physical_spawn_bake / 1.705 | advance_planning_slice / 0.659 | 133.0031 |
| 43987 | 37.4218 | physical | 37.311 | physical_spawn_pedestrian / 16.453 | physical_spawn_bake / 1.289 | advance_planning_slice / 2.58 | 64.5727 |
| 44163 | 73.2234 | physical | 71.943 | physical_parked / 32.477 | none / 0 | advance_planning_slice / 7.355 | 819.0055 |
| 44169 | 43.0492 | physical | 42.902 | physical_spawn_appearance / 23.605 | physical_spawn_bake / 3.112 | advance_planning_slice / 3.888 | 146.4155 |
| 44203 | 39.9807 | physical | 39.481 | physical_parked / 18.976 | none / 0 | mod_closures_links / 6.405 | 223.4399 |
| 44205 | 34.055 | physical | 34.002 | physical_index / 14.434 | none / 0 | advance_planning_finish / 2.392 | 75.1248 |
| 44210 | 49.2669 | physical | 49.043 | physical_parked / 24.656 | none / 0 | mod_closures_links / 6.475 | 286.7729 |
| 44213 | 38.8348 | physical | 38.273 | physical_parked / 13.189 | none / 0 | advance_planning_finish / 5.865 | 210.6893 |
| 44222 | 34.0121 | physical | 33.973 | physical_spawn_appearance / 19.612 | physical_spawn_randomize / 1.622 | mod_routes_sweep / 1.082 | 81.3205 |
| 44385 | 48.2492 | physical | 47.751 | physical_spawn_appearance / 18.77 | physical_spawn_randomize / 2.808 | mod_closures_links / 1.317 | 192.5734 |
| 44739 | 71.6203 | physical | 70.674 | physical_parked / 21.239 | none / 0 | advance_planning_slice / 9.38 | 481.9581 |
| 44768 | 68.5932 | updatecity | 68.527 | mod_rlo / 63.76 | none / 0 | advance_planning_slice / 0.788 | 323.5258 |
| 44769 | 70.7717 | physical | 70.588 | physical_index / 44.06 | none / 0 | advance_planning_slice / 9.309 | 384.4117 |
| 44789 | 46.8184 | physical | 46.73 | physical_spawn_appearance / 24.359 | physical_spawn_bake / 2.799 | advance_events_citizen / 1.671 | 102.0272 |
| 44802 | 93.4673 | physical | 92.168 | mod_summary / 37.453 | none / 0 | mod_closures_links / 2.022 | 467.1996 |
| 44885 | 41.127 | physical | 41.063 | physical_spawn_appearance / 27.247 | physical_spawn_randomize / 2.079 | mod_closures_links / 0.176 | 72.0423 |
| 44896 | 51.4156 | physical | 50.6 | physical_parked / 13.022 | none / 0 | advance_planning_slice / 7.065 | 174.2297 |
| 44958 | 35.7673 | physical | 35.716 | physical_spawn_appearance / 21.438 | physical_spawn_randomize / 2.824 | advance_planning_finish / 0.439 | 66.4461 |
| 45004 | 57.8011 | physical | 57.747 | physical_release / 52.965 | none / 0 | advance_events_citizen / 1.549 | 157.6198 |
| 45258 | 57.774 | physical | 57.702 | physical_parked / 33.07 | none / 0 | advance_planning_slice / 2.277 | 456.9377 |
| 45344 | 44.1843 | physical | 43.733 | physical_parked / 17.275 | none / 0 | advance_planning_slice / 4.228 | 528.4216 |
| 45414 | 43.1402 | physical | 43.08 | physical_spawn_appearance / 21.639 | physical_spawn_bake / 2.827 | mod_closures_links / 0.04 | 97.6354 |
| 45546 | 162.1045 | updatecity | 160.972 | mod_rlo / 77.68 | none / 0 | mod_closures_links / 4.698 | 511.6683 |
| 45768 | 30.1057 | physical | not exported | not exported | not exported | not exported | 135.2125 |
| 45822 | 34.1883 | physical | 34.127 | physical_spawn_appearance / 21.988 | physical_spawn_randomize / 2.186 | mod_closures_links / 0.06 | 94.2355 |
| 45872 | 55.58 | physical | 54.612 | physical_parked / 13.643 | none / 0 | advance_planning_slice / 12.754 | 381.2393 |
| 46010 | 44.2383 | physical | 43.84 | physical_parked / 13.727 | none / 0 | mod_closures_links / 4.237 | 344.1217 |
| 46031 | 35.0183 | physical | 34.976 | physical_spawn_appearance / 17.678 | physical_spawn_randomize / 1.139 | mod_routes_sweep / 1.001 | 61.984 |
| 46191 | 30.0585 | physical | not exported | not exported | not exported | not exported | 154.2347 |
| 46461 | 36.108 | physical | 34.988 | physical_parked / 17.316 | none / 0 | advance_events_citizen / 3.201 | 351.6095 |
| 46622 | 49.4307 | updatecity | 49.362 | mod_summary / 47.565 | none / 0 | mod_routes_speeds / 0.028 | 128.4901 |
| 46758 | 64.3084 | physical | 62.743 | physical_parked / 34.145 | none / 0 | advance_events_citizen / 3.96 | 460.7609 |
| 46840 | 56.2925 | physical | 56.237 | physical_bindings / 55.749 | none / 0 | mod_closures_links / 0.034 | 142.4941 |
| 46872 | 63.991 | physical | 62.201 | physical_parked / 29.786 | none / 0 | advance_events_citizen / 4.169 | 462.6057 |
| 47271 | 46.8908 | physical | 44.52 | physical_parked / 18.051 | none / 0 | advance_planning_slice / 11.647 | 387.5152 |
| 47520 | 92.9164 | physical | 92.792 | physical_spawn_appearance / 60.743 | physical_spawn_randomize / 4.956 | mod_closures_links / 0.132 | 124.7589 |
| 47553 | 52.2103 | physical | 52.094 | physical_spawn_appearance / 26.349 | physical_spawn_randomize / 3.234 | advance_planning_finish / 1.11 | 121.7118 |
| 47571 | 64.4215 | physical | 64.267 | physical_spawn_appearance / 33.56 | physical_spawn_randomize / 2.623 | mod_routes_sweep / 1.318 | 139.2231 |
| 47594 | 56.5804 | updatecity | 56.536 | mod_summary / 54.698 | none / 0 | mod_routes_sweep / 0.938 | 130.1569 |
| 47599 | 47.5183 | advance | 47.386 | advance_planning / 37.056 | none / 0 | advance_planning_slice / 37.05 | 181.2798 |
| 47970 | 38.0098 | physical | 37.329 | physical_parked / 22.04 | none / 0 | mod_closures_links / 1.999 | 406.5084 |
| 48004 | 49.6487 | physical | 49.571 | physical_spawn_appearance / 28.558 | physical_spawn_randomize / 3.979 | mod_closures_links / 0.073 | 99.9447 |
| 48174 | 72.3934 | physical | 72.33 | physical_spawn_appearance / 31.797 | physical_spawn_randomize / 2.486 | mod_closures_links / 0.051 | 118.2807 |
| 48191 | 35.574 | physical | 34.077 | physical_parked / 18.517 | none / 0 | advance_events_citizen / 4.508 | 244.8446 |
| 48304 | 47.9197 | updatecity | 47.314 | mod_food / 45.41 | none / 0 | advance_events_citizen / 0.773 | 459.0084 |
| 48306 | 74.8016 | physical | 73.016 | physical_parked / 39.107 | none / 0 | advance_planning_slice / 8.75 | 823.298 |
| 48370 | 44.1715 | physical | 42.732 | physical_parked / 21.328 | none / 0 | advance_planning_slice / 5.812 | 176.5341 |
| 48509 | 44.1462 | physical | 44.073 | physical_index / 36.866 | none / 0 | advance_events_citizen / 3.952 | 103.9417 |
| 48646 | 98.2344 | physical | 98.108 | physical_spawn_appearance / 62.5 | physical_spawn_bake / 3.556 | mod_closures_links / 0.035 | 171.146 |
| 48653 | 43.5297 | physical | 42.419 | physical_parked / 26.278 | none / 0 | advance_planning_slice / 1.481 | 403.3778 |
| 48733 | 33.7083 | physical | 32.69 | physical_parked / 15.601 | none / 0 | advance_events_route_begin / 2.339 | 377.0198 |
| 48757 | 78.6167 | physical | 78.55 | physical_spawn_appearance / 39.378 | physical_spawn_randomize / 3.766 | mod_closures_links / 0.792 | 150.1726 |
| 48768 | 37.5204 | physical | 37.3 | physical_release / 33.185 | none / 0 | advance_events_traffic / 0.965 | 137.4915 |
| 48818 | 39.1217 | advance | 39.026 | advance_planning / 34.3 | none / 0 | advance_planning_slice / 34.298 | 175.9984 |
| 49024 | 35.5069 | physical | 35.458 | physical_spawn_appearance / 21.265 | physical_spawn_bake / 2.534 | mod_routes_speeds / 0.02 | 64.9718 |
| 49086 | 77.5931 | physical | 77.509 | physical_spawn_appearance / 32.946 | physical_spawn_bake / 2.096 | advance_events_traffic / 0.218 | 142.4772 |
| 49096 | 42.8187 | physical | 41.806 | physical_parked / 15.351 | none / 0 | mod_routes_rebuild / 7.274 | 433.3807 |
| 49308 | 74.4763 | updatecity | 73.048 | mod_rlo / 48.251 | none / 0 | advance_events_citizen / 2.917 | 440.9373 |
| 49359 | 65.0466 | updatecity | not exported | not exported | not exported | not exported | 172.3008 |
| 49406 | 83.0837 | physical | 80.763 | physical_parked / 41.75 | none / 0 | advance_events_citizen / 8.346 | 531.2528 |
| 49523 | 63.021 | physical | not exported | not exported | not exported | not exported | 448.2243 |
| 49597 | 32.1832 | physical | not exported | not exported | not exported | not exported | 282.8151 |
| 49672 | 58.5542 | physical | not exported | not exported | not exported | not exported | 349.7972 |
| 49805 | 30.6899 | advance | not exported | not exported | not exported | not exported | 160.8348 |
| 49866 | 31.2819 | physical | not exported | not exported | not exported | not exported | 210.9358 |
| 49947 | 39.886 | physical | not exported | not exported | not exported | not exported | 353.8168 |
| 49982 | 283.8606 | updatecity | 283.782 | mod_rlo / 272.792 | none / 0 | advance_planning_slice / 6.242 | 786.3001 |
| 49991 | 110.4106 | updatecity | 107.558 | mod_summary / 48.983 | none / 0 | mod_routes_sweep / 1.013 | 531.4579 |
| 50049 | 348.5835 | physical | 346.985 | physical_parked / 341.209 | none / 0 | mod_routes_sweep / 1.011 | 2359.7122 |
| 50050 | 52.3276 | physical | not exported | not exported | not exported | not exported | 5399.7442 |
| 50051 | 53.3445 | physical | not exported | not exported | not exported | not exported | 531.5035 |
| 50056 | 32.1134 | physical | not exported | not exported | not exported | not exported | 246.7541 |
| 50057 | 31.2578 | physical | not exported | not exported | not exported | not exported | 420.7165 |
| 50134 | 79.8917 | physical | 79.851 | physical_spawn_appearance / 55.903 | physical_spawn_bake / 2.207 | mod_routes_speeds / 0.021 | 131.562 |
| 50251 | 72.0522 | updatecity | not exported | not exported | not exported | not exported | 522.3257 |
| 50294 | 41.1022 | physical | not exported | not exported | not exported | not exported | 126.3504 |
| 50337 | 50.2127 | physical | not exported | not exported | not exported | not exported | 520.2995 |
| 50401 | 66.428 | physical | not exported | not exported | not exported | not exported | 95.1034 |
| 50424 | 30.3013 | physical | not exported | not exported | not exported | not exported | 598.5079 |
| 50506 | 48.5945 | physical | not exported | not exported | not exported | not exported | 277.0115 |
| 50517 | 46.9427 | physical | not exported | not exported | not exported | not exported | 345.4019 |
| 50537 | 67.9229 | physical | not exported | not exported | not exported | not exported | 97.6358 |
| 50637 | 45.7996 | updatecity | not exported | not exported | not exported | not exported | 147.8834 |
| 50731 | 35.781 | physical | not exported | not exported | not exported | not exported | 507.971 |
| 50752 | 54.2347 | physical | not exported | not exported | not exported | not exported | 767.7319 |

coverage.complete=True; validity=`{"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}`; save_activity=`{"attempts":9143,"allowed":0,"refused":9143,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}`; final lag=0.067108154296875, refused=False, stoppedAfterAccountingFailure=False, historicalClockMisses=0.

**street: 114 union spikes / 114 exported**

PC phase recorder counted 113 spikes and exported 64 spike rows; 44 union rows lack PC phase/detail/sub. Full attribution is NOT RUN / tooling gap: the successful reply exposes the count/list retention gap. Every available union row follows; missing fields are explicit.

| Frame | union ms | dominant | PC mod ms | phase / ms | detail / ms | sub / sub_ms | whole-frame ms |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 60756 | 46.2939 | updatecity | 46.238 | physical_unattributed / 18.416 | physical_spawn_bake / 0.491 | physical_setup_ambient / 11.374 | 94.9124 |
| 63075 | 40.647 | updatecity | 40.587 | mod_rlo / 31.902 | physical_spawn_bake / 0.739 | mod_closures_links / 0.131 | 220.6225 |
| 63083 | 34.0116 | updatecity | 33.484 | physical_spawn_pedestrian / 8.533 | physical_spawn_bake / 0.564 | mod_routes_sweep / 1.002 | 341.1962 |
| 63092 | 57.1963 | updatecity | 55.754 | physical_parked / 14.292 | physical_spawn_randomize / 2.688 | mod_routes_sweep / 1.007 | 1688.0462 |
| 63095 | 30.5111 | updatecity | not exported | not exported | not exported | not exported | 356.6643 |
| 63434 | 34.9552 | updatecity | 34.81 | physical_parked / 22.391 | none / 0 | mod_closures_links / 0.572 | 288.3483 |
| 63482 | 87.3187 | updatecity | 86.786 | advance_events / 38.876 | physical_spawn_grow / 4.941 | advance_events_citizen / 38.873 | 783.0113 |
| 63485 | 74.6448 | updatecity | 73.78 | physical_spawn_pedestrian / 20.69 | physical_spawn_bake / 1.522 | advance_events_citizen / 8.572 | 1253.5962 |
| 63486 | 62.0384 | updatecity | 60.723 | physical_parked / 31.956 | physical_spawn_grow / 8.124 | mod_closures_links / 2.073 | 3501.947 |
| 63487 | 48.1959 | updatecity | 45.791 | physical_parked / 16.36 | physical_spawn_grow / 0.843 | mod_closures_links / 3.551 | 1559.2831 |
| 63488 | 72.4395 | updatecity | 71.334 | physical_spawn_pedestrian / 16.548 | physical_spawn_bake / 0.864 | advance_planning_slice / 4.038 | 1248.2228 |
| 63489 | 59.5554 | updatecity | 58.961 | physical_parked / 23.761 | none / 0 | mod_closures_links / 3.236 | 949.9397 |
| 63513 | 77.4791 | updatecity | 76.952 | physical_spawn_pedestrian / 30.962 | physical_spawn_bake / 1.47 | advance_planning_slice / 1.247 | 978.7136 |
| 63599 | 35.1055 | updatecity | 35.016 | physical_spawn_pedestrian / 16.61 | physical_spawn_randomize / 1.611 | mod_routes_sweep / 1.008 | 271.602 |
| 63617 | 47.1232 | updatecity | 46.171 | physical_spawn_pedestrian / 21.755 | physical_spawn_bake / 4.015 | advance_planning_slice / 1.628 | 575.8678 |
| 63618 | 36.6285 | updatecity | 36.11 | physical_parked / 14.817 | none / 0 | advance_planning_slice / 4.439 | 730.3737 |
| 63686 | 39.9247 | updatecity | 39.7 | physical_parked / 21.127 | none / 0 | advance_planning_slice / 5.982 | 380.5418 |
| 63735 | 59.4485 | updatecity | 57.83 | physical_parked / 24.232 | none / 0 | mod_closures_links / 3.673 | 1366.2779 |
| 63783 | 35.7663 | updatecity | 34.4 | physical_parked / 14.256 | none / 0 | mod_closures_links / 0.995 | 2607.7818 |
| 63784 | 73.6636 | updatecity | 72.755 | physical_parked / 26.315 | none / 0 | advance_events_citizen / 6.502 | 2011.6665 |
| 63787 | 114.3339 | updatecity | 113.362 | mod_summary / 58.122 | none / 0 | advance_planning_slice / 8.371 | 1915.1332 |
| 63788 | 57.5317 | updatecity | 54.942 | physical_parked / 23.494 | none / 0 | advance_planning_slice / 5.73 | 2422.0535 |
| 63789 | 75.876 | updatecity | 75.037 | physical_parked / 29.502 | none / 0 | advance_planning_slice / 12.973 | 2287.062 |
| 63790 | 30.7749 | updatecity | 30.17 | physical_parked / 10.197 | none / 0 | advance_planning_slice / 7.103 | 2130.8239 |
| 63791 | 46.607 | updatecity | 44.743 | physical_parked / 17.932 | none / 0 | advance_planning_finish / 3.189 | 1478.0682 |
| 63792 | 46.253 | updatecity | 45.502 | physical_parked / 18.894 | none / 0 | mod_closures_links / 3.606 | 3460.1007 |
| 63793 | 57.5364 | updatecity | 56.291 | physical_parked / 19.742 | none / 0 | advance_planning_slice / 3.74 | 2226.1357 |
| 63794 | 79.024 | updatecity | 78.413 | physical_parked / 30.458 | none / 0 | advance_planning_slice / 7.531 | 1145.1452 |
| 63800 | 65.5967 | updatecity | 65.524 | physical_spawn_appearance / 39.55 | physical_spawn_randomize / 2.23 | advance_planning_finish / 1.483 | 319.7229 |
| 63826 | 36.3894 | updatecity | 36.246 | physical_spawn_pedestrian / 15.804 | physical_spawn_bake / 1.806 | mod_routes_sweep / 1.391 | 225.2924 |
| 63843 | 74.5116 | updatecity | 74.359 | mod_rlo / 65.653 | none / 0 | advance_events_citizen / 1.926 | 657.4812 |
| 63844 | 69.5512 | updatecity | 67.621 | physical_release / 24.296 | none / 0 | mod_routes_speeds / 1.625 | 481.5325 |
| 63852 | 59.4998 | updatecity | 58.663 | physical_parked / 26.497 | none / 0 | mod_closures_links / 3.011 | 1740.0617 |
| 63853 | 42.5111 | updatecity | 41.482 | physical_parked / 16.367 | none / 0 | advance_planning_slice / 2.447 | 2499.4553 |
| 63854 | 37.6057 | updatecity | 37.11 | physical_parked / 15.13 | none / 0 | mod_closures_links / 3.508 | 512.5786 |
| 63876 | 45.5548 | updatecity | 45.049 | physical_parked / 21.59 | none / 0 | advance_planning_slice / 6.522 | 544.8092 |
| 63881 | 33.486 | updatecity | 32.668 | physical_parked / 12.032 | none / 0 | mod_closures_links / 2.284 | 507.4988 |
| 63882 | 54.2829 | updatecity | 54.212 | physical_spawn_pedestrian / 24.472 | physical_spawn_randomize / 0.858 | advance_planning_slice / 7.698 | 398.6738 |
| 63887 | 79.1283 | updatecity | 79.044 | advance_events / 49.925 | none / 0 | advance_events_citizen / 49.921 | 336.5559 |
| 63896 | 94.7636 | updatecity | 92.56 | physical_spawn_appearance / 34.512 | physical_spawn_randomize / 2.068 | advance_events_traffic / 1.847 | 4310.7738 |
| 63897 | 34.3689 | updatecity | 33.386 | physical_index / 11.948 | none / 0 | mod_closures_links / 2.569 | 1786.6317 |
| 63898 | 65.4166 | updatecity | 64.393 | physical_parked / 30.471 | none / 0 | mod_closures_links / 7.398 | 1558.7945 |
| 63899 | 85.8093 | updatecity | 85.384 | mod_rlo / 42.742 | none / 0 | mod_routes_sweep / 1.012 | 1033.0234 |
| 63914 | 72.6639 | updatecity | 72.558 | physical_spawn_pedestrian / 41.625 | physical_spawn_bake / 3.124 | advance_planning_slice / 7.529 | 333.4058 |
| 63924 | 48.2692 | updatecity | 48.169 | physical_parked / 20.35 | none / 0 | advance_planning_slice / 8.846 | 505.6866 |
| 63926 | 59.9753 | updatecity | 57.722 | physical_parked / 27.516 | none / 0 | advance_planning_slice / 5.76 | 2025.1474 |
| 63927 | 46.7976 | updatecity | 45.882 | physical_parked / 10.792 | none / 0 | mod_closures_links / 4.683 | 2243.91 |
| 63928 | 73.2302 | updatecity | 72.601 | physical_parked / 30.053 | none / 0 | mod_closures_links / 5.78 | 1687.3575 |
| 63929 | 60.0551 | updatecity | 58.642 | physical_parked / 18.24 | none / 0 | mod_closures_links / 4.824 | 1930.4417 |
| 63930 | 84.4059 | updatecity | 83.702 | physical_parked / 35.025 | none / 0 | mod_closures_links / 4.372 | 1749.1753 |
| 63931 | 49.4935 | updatecity | 48.983 | physical_parked / 19.706 | none / 0 | advance_planning_slice / 6.067 | 1262.4198 |
| 63932 | 69.1978 | updatecity | 66.908 | physical_parked / 31.703 | none / 0 | advance_planning_finish / 5.481 | 2256.2336 |
| 63933 | 43.6716 | updatecity | 43.368 | mod_routes / 12.675 | none / 0 | mod_routes_rebuild / 10.743 | 340.4888 |
| 63971 | 31.1488 | updatecity | 30.716 | physical_parked / 7.315 | none / 0 | advance_planning_slice / 4.433 | 942.195 |
| 63990 | 34.7105 | updatecity | 34.369 | physical_parked / 16.316 | none / 0 | advance_planning_slice / 2.404 | 434.4977 |
| 63996 | 52.0435 | updatecity | 51.961 | advance_events / 47.908 | none / 0 | advance_events_citizen / 47.907 | 260.9041 |
| 64001 | 55.7344 | updatecity | 53.487 | physical_parked / 17.092 | none / 0 | mod_closures_links / 7.23 | 1732.2244 |
| 64002 | 47.054 | updatecity | 45.567 | physical_parked / 18.249 | none / 0 | advance_planning_slice / 3.053 | 2772.2574 |
| 64003 | 45.8239 | updatecity | 45.233 | physical_parked / 8.795 | none / 0 | advance_planning_slice / 4.702 | 1720.6226 |
| 64004 | 48.6294 | updatecity | 47.916 | physical_parked / 25.328 | none / 0 | mod_closures_links / 4.047 | 1709.1031 |
| 64005 | 59.2499 | updatecity | 57.002 | physical_parked / 10.386 | none / 0 | mod_routes_rebuild / 7.832 | 1268.4077 |
| 64009 | 50.8625 | updatecity | 50.79 | physical_spawn_pedestrian / 23.022 | physical_spawn_bake / 3.245 | advance_planning_finish / 1.851 | 224.6316 |
| 64028 | 78.8282 | updatecity | 78.491 | mod_rlo / 20.464 | none / 0 | mod_closures_links / 5.199 | 808.3143 |
| 64030 | 44.1286 | updatecity | 43.933 | physical_parked / 15.979 | none / 0 | advance_planning_slice / 5.428 | 575.6856 |
| 64035 | 65.5712 | updatecity | not exported | not exported | not exported | not exported | 1930.6952 |
| 64036 | 60.7575 | updatecity | not exported | not exported | not exported | not exported | 1627.2697 |
| 64037 | 44.6082 | updatecity | not exported | not exported | not exported | not exported | 2383.3785 |
| 64038 | 45.5893 | updatecity | not exported | not exported | not exported | not exported | 1698.7455 |
| 64039 | 63.0781 | updatecity | not exported | not exported | not exported | not exported | 1765.2999 |
| 64040 | 74.4747 | updatecity | not exported | not exported | not exported | not exported | 1495.3052 |
| 64042 | 80.5842 | updatecity | 79.15 | physical_parked / 23.925 | none / 0 | advance_planning_slice / 12.438 | 2256.6313 |
| 64043 | 52.2633 | updatecity | not exported | not exported | not exported | not exported | 1702.9188 |
| 64044 | 63.5221 | updatecity | not exported | not exported | not exported | not exported | 1709.5824 |
| 64045 | 44.4435 | updatecity | not exported | not exported | not exported | not exported | 2067.9207 |
| 64046 | 50.253 | updatecity | not exported | not exported | not exported | not exported | 1161.061 |
| 64047 | 35.3329 | updatecity | not exported | not exported | not exported | not exported | 1606.9596 |
| 64048 | 59.1767 | updatecity | not exported | not exported | not exported | not exported | 2096.526 |
| 64049 | 104.2513 | updatecity | 103.717 | mod_rlo / 61.454 | none / 0 | advance_planning_slice / 5.094 | 1700.3629 |
| 64050 | 53.1261 | updatecity | not exported | not exported | not exported | not exported | 2145.3815 |
| 64052 | 67.0774 | updatecity | not exported | not exported | not exported | not exported | 1629.7875 |
| 64053 | 38.2478 | updatecity | not exported | not exported | not exported | not exported | 1664.4096 |
| 64054 | 40.2649 | updatecity | not exported | not exported | not exported | not exported | 1378.1904 |
| 64055 | 53.9101 | updatecity | not exported | not exported | not exported | not exported | 1863.1795 |
| 64056 | 61.4964 | updatecity | not exported | not exported | not exported | not exported | 1400.3877 |
| 64057 | 52.6392 | updatecity | not exported | not exported | not exported | not exported | 2610.5968 |
| 64058 | 58.8662 | updatecity | not exported | not exported | not exported | not exported | 1875.8269 |
| 64059 | 57.7573 | updatecity | not exported | not exported | not exported | not exported | 1667.0528 |
| 64060 | 64.5989 | updatecity | not exported | not exported | not exported | not exported | 1808.6872 |
| 64061 | 53.5576 | updatecity | not exported | not exported | not exported | not exported | 2446.3303 |
| 64063 | 59.2218 | updatecity | not exported | not exported | not exported | not exported | 1785.1348 |
| 64064 | 87.3923 | updatecity | 85.992 | physical_index / 33.87 | none / 0 | mod_closures_links / 3.631 | 1788.6034 |
| 64065 | 101.0809 | updatecity | 100.469 | mod_summary / 55.985 | none / 0 | advance_planning_slice / 6.564 | 1640.9558 |
| 64066 | 122.1744 | updatecity | 121.404 | mod_rlo / 74.384 | none / 0 | advance_planning_finish / 2.087 | 1361.2837 |
| 64067 | 63.8591 | updatecity | not exported | not exported | not exported | not exported | 847.7095 |
| 64069 | 60.1199 | updatecity | not exported | not exported | not exported | not exported | 1382.3923 |
| 64072 | 62.7755 | updatecity | not exported | not exported | not exported | not exported | 1386.3953 |
| 64074 | 66.1214 | updatecity | not exported | not exported | not exported | not exported | 1606.1904 |
| 64091 | 62.566 | updatecity | not exported | not exported | not exported | not exported | 384.0208 |
| 64101 | 92.3899 | updatecity | 89.498 | advance_planning / 40.344 | none / 0 | advance_planning_slice / 40.342 | 1586.826 |
| 64102 | 79.5436 | updatecity | not exported | not exported | not exported | not exported | 1776.1737 |
| 64103 | 49.1821 | updatecity | not exported | not exported | not exported | not exported | 1998.6243 |
| 64104 | 102.1976 | updatecity | 100.587 | advance_planning / 44.704 | none / 0 | advance_planning_slice / 44.701 | 1819.669 |
| 64105 | 54.0291 | updatecity | not exported | not exported | not exported | not exported | 1408.9576 |
| 64106 | 53.2309 | updatecity | not exported | not exported | not exported | not exported | 975.2702 |
| 64110 | 31.5475 | updatecity | not exported | not exported | not exported | not exported | 270.9114 |
| 64118 | 62.5364 | updatecity | not exported | not exported | not exported | not exported | 1518.8007 |
| 64119 | 62.1525 | updatecity | not exported | not exported | not exported | not exported | 1730.4698 |
| 64120 | 55.3916 | updatecity | not exported | not exported | not exported | not exported | 1377.9972 |
| 64121 | 40.4939 | updatecity | not exported | not exported | not exported | not exported | 429.1448 |
| 64129 | 51.4008 | updatecity | not exported | not exported | not exported | not exported | 1964.4266 |
| 64130 | 43.0878 | updatecity | not exported | not exported | not exported | not exported | 1262.1142 |
| 64131 | 56.9505 | updatecity | not exported | not exported | not exported | not exported | 2026.3935 |
| 64132 | 36.3192 | updatecity | not exported | not exported | not exported | not exported | 862.077 |
| 64138 | 57.2638 | updatecity | not exported | not exported | not exported | not exported | 2057.9238 |

Additional PC phase spikes: `[{"frame":60750,"mod_ms":83.552,"phase":"mod_routes","phase_ms":80.928,"detail":"none","detail_ms":0,"sub":"mod_routes_speeds","sub_ms":76.772,"gc_phase":"mod_routes","collections":1,"alloc_phase":"mod_routes","alloc_phase_bytes":1115064,"alloc_bytes":1122456}]`.

coverage.complete=True; validity=`{"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}`; save_activity=`{"attempts":3394,"allowed":0,"refused":3394,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}`; final lag=0.08885765075683594, refused=False, stoppedAfterAccountingFailure=False, historicalClockMisses=0.

gc-day: physical_setup_ambient 0.175 MB; 0 exported >=30 ms setup contributions. None exported; dominant scopes do not prove a maximum for every sub-scope. Routes nodes 3.472 MB / closures network 0.229 MB versus 15a 57.9 / 10.2 MB day and 58.3 / 9.9 MB night.

gc-night: physical_setup_ambient 0.175 MB; 0 exported >=30 ms setup contributions. None exported; dominant scopes do not prove a maximum for every sub-scope. Routes nodes 3.368 MB / closures network 0.327 MB versus 15a 57.9 / 10.2 MB day and 58.3 / 9.9 MB night.

Worst exported synchronous body-growth row: `{"frame":63486,"mod_ms":60.723,"phase":"physical_parked","phase_ms":31.956,"detail":"physical_spawn_grow","detail_ms":8.124,"sub":"mod_closures_links","sub_ms":2.073,"gc_phase":"none","collections":0,"alloc_phase":"physical_spawn_pedestrian","alloc_phase_bytes":18560,"alloc_bytes":21456,"whole_frame_ms":3501.947}`. Exact worst Unity async body-integration frame is NOT RUN / tooling gap: the recorder exports top mod-work frames and dominant phase/detail/sub, not an independent async integration scope. Whole-frame p95/max above is not substituted. 15a synchronous growth frame was 134.255 ms.

Street `physical_spawn_grow`: 3 >=30 ms PC frames name the scope; 0 with that contribution itself >=30 ms. Frames: none exported. Expected no >=30 ms contribution from this scope: not fully verified; none >=30 ms in retained rows.
Street `physical_parked`: 39 >=30 ms PC frames name the scope; 6 with that contribution itself >=30 ms. Frames: 63486, 63794, 63898, 63928, 63930, 63932. Expected no >=30 ms contribution from this scope: not met.
Street `advance_events_traffic`: 1 >=30 ms PC frames name the scope; 0 with that contribution itself >=30 ms. Frames: none exported. Expected no >=30 ms contribution from this scope: not fully verified; none >=30 ms in retained rows.
Street `mod_rlo`: 4 >=30 ms PC frames name the scope; 3 with that contribution itself >=30 ms. Frames: 63075, 63843, 63899. Expected no >=30 ms contribution from this scope: reported.

Clock-rate rebuild lines: 6 True / 1 False. All such lines are retained below; the Expected always-True clause is not fully met. Initial construction is distinguished by its log, not silently omitted.

### Host load and other sessions

`20261115-081854-rig-gr5-perf-attribution` was rechecked: finished/success at 08:46:57 CET and no live harness process for it during these windows. The twelfth-fix run `20261115-090523-mac-fix-gr5-rebuild-food-appearance-parked` and other detected sessions/builds are named at both boundaries below. Intermediate minute snapshots are retained under `<stage>`. Any active overlap makes the complete window provisional (Mac shared).

**gc-day start, 2026-11-15T08:20:27Z — provisional (Mac shared)**

```text
9:20  up 1 day, 14:58, 1 user, load averages: 4.93 5.40 5.84
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 73%
PID  %CPU COMM
83346 100.5 dotnet
49418  90.9 suggestd
81704  52.3 Big Ambitions Agent Tests Mac
  444  11.1 coreaudiod
83299   4.6 Python
  915   3.1 sysmond
  735   2.0 Resolve
  654   1.2 com.apple.DriverKit-AppleBCMWLAN
  756   1.1 Activity Monitor
  421   0.6 notifyd
  561   0.5 mds_stores
```

- `20261115-033628-ux-akte-one-screen-1966`: phase=review, status=None, finished_at=None.
- `20261115-081854-rig-gr5-perf-attribution`: phase=done, status=success, finished_at=2026-11-15T08:46:57+01:00.
- `20261115-090523-mac-fix-gr5-rebuild-food-appearance-parked`: phase=implement, status=None, finished_at=None.

Harness commands/PIDs:

```text
76767 /opt/homebrew/Cellar/python@3.15/3.15.0/Frameworks/Python.framework/Versions/3.15/Resources/Python.app/Contents/MacOS/Python -m harness resume --answer <home>/Coding_Projects/_bot-runs/mvp-harness/answer-1966-0905.md --resolved <home>/Coding_Projects/_bot-runs/mvp-harness/fresh-resolved-1966-0905.json <home>/Coding_Projects/_harness-runs/20261115-033628-ux-akte-one-screen-1966
77725 /opt/homebrew/Cellar/python@3.15/3.15.0/Frameworks/Python.framework/Versions/3.15/Resources/Python.app/Contents/MacOS/Python -m harness run --run-dir <home>/Coding_Projects/_harness-runs/20261115-090523-mac-fix-gr5-rebuild-food-appearance-parked
```

Other coding/build process PID / PPID / CPU / elapsed / command:

```text
78025 77725   0.1          14:27 <home>/.local/bin/claude
78353     1   0.0          12:53 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
78354     1   0.0          12:53 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
78364 78354   0.0          12:51 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/sdk/10.0.400/Roslyn/bincore/VBCSCompiler
83341 83324   0.0          00:06 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
83345 83341   0.0          00:06 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
83346 83345 100.5          00:05 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
```

**gc-day end, 2026-11-15T08:30:29Z — provisional (Mac shared)**

```text
9:30  up 1 day, 15:08, 1 user, load averages: 7.43 4.66 4.94
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 39%
PID  %CPU COMM
86541 143.4 dotnet
50220 102.6 com.apple.Virtualization.VirtualMachine
81704  86.8 Big Ambitions Agent Tests Mac
78364  63.6 VBCSCompiler
86553  36.1 node
86552  23.9 node
86570  14.3 mdworker_shared
86560  12.2 replayd
86549  11.7 node
50209   8.5 limactl
49418   8.3 suggestd
```

- `20261115-033628-ux-akte-one-screen-1966`: phase=fix, status=None, finished_at=None.
- `20261115-081854-rig-gr5-perf-attribution`: phase=done, status=success, finished_at=2026-11-15T08:46:57+01:00.
- `20261115-090523-mac-fix-gr5-rebuild-food-appearance-parked`: phase=implement, status=None, finished_at=None.

Harness commands/PIDs:

```text
76767 /opt/homebrew/Cellar/python@3.15/3.15.0/Frameworks/Python.framework/Versions/3.15/Resources/Python.app/Contents/MacOS/Python -m harness resume --answer <home>/Coding_Projects/_bot-runs/mvp-harness/answer-1966-0905.md --resolved <home>/Coding_Projects/_bot-runs/mvp-harness/fresh-resolved-1966-0905.json <home>/Coding_Projects/_harness-runs/20261115-033628-ux-akte-one-screen-1966
77725 /opt/homebrew/Cellar/python@3.15/3.15.0/Frameworks/Python.framework/Versions/3.15/Resources/Python.app/Contents/MacOS/Python -m harness run --run-dir <home>/Coding_Projects/_harness-runs/20261115-090523-mac-fix-gr5-rebuild-food-appearance-parked
```

Other coding/build process PID / PPID / CPU / elapsed / command:

```text
78025 77725   3.8          24:29 <home>/.local/bin/claude
78353     1   0.0          22:55 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
78354     1   0.0          22:55 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
78364 78354  63.6          22:53 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/sdk/10.0.400/Roslyn/bincore/VBCSCompiler
83604 76767   4.7          08:31 <home>/.local/bin/claude
86535 85540   0.0          00:23 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
86539 86535   0.0          00:22 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
86541 86539 143.4          00:22 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
```

**gc-night start, 2026-11-15T08:43:24Z — provisional (Mac shared)**

```text
9:43  up 1 day, 15:21, 1 user, load averages: 11.26 13.32 10.25
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 51%
PID  %CPU COMM
88339 270.8 dotnet
89006  57.1 chrome-headless-shell
49418  54.9 suggestd
81704  51.1 Big Ambitions Agent Tests Mac
88942  47.7 chrome-headless-shell
88940  38.0 node
88941  20.9 chrome-headless-shell
89032  20.3 mdworker_shared
88931  15.0 next-server (v16.4.2)
89029  11.7 mdworker_shared
50220  10.6 com.apple.Virtualization.VirtualMachine
```

- `20261115-033628-ux-akte-one-screen-1966`: phase=fix, status=None, finished_at=None.
- `20261115-081854-rig-gr5-perf-attribution`: phase=done, status=success, finished_at=2026-11-15T08:46:57+01:00.
- `20261115-090523-mac-fix-gr5-rebuild-food-appearance-parked`: phase=implement, status=None, finished_at=None.

Harness commands/PIDs:

```text
76767 /opt/homebrew/Cellar/python@3.15/3.15.0/Frameworks/Python.framework/Versions/3.15/Resources/Python.app/Contents/MacOS/Python -m harness resume --answer <home>/Coding_Projects/_bot-runs/mvp-harness/answer-1966-0905.md --resolved <home>/Coding_Projects/_bot-runs/mvp-harness/fresh-resolved-1966-0905.json <home>/Coding_Projects/_harness-runs/20261115-033628-ux-akte-one-screen-1966
77725 /opt/homebrew/Cellar/python@3.15/3.15.0/Frameworks/Python.framework/Versions/3.15/Resources/Python.app/Contents/MacOS/Python -m harness run --run-dir <home>/Coding_Projects/_harness-runs/20261115-090523-mac-fix-gr5-rebuild-food-appearance-parked
```

Other coding/build process PID / PPID / CPU / elapsed / command:

```text
78025 77725   0.0          37:24 <home>/.local/bin/claude
78353     1   0.0          35:50 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
78354     1   0.0          35:50 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
78364 78354   0.0          35:48 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/sdk/10.0.400/Roslyn/bincore/VBCSCompiler
83604 76767   0.0          21:26 <home>/.local/bin/claude
88310 88298   0.0          04:43 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
88314 88310   0.0          04:42 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
88315 88310   0.0          04:42 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
88337 88314   0.0          04:37 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
88339 88337 270.8          04:37 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
88931 88930  15.0          01:19 next-server (v16.4.2)
88941 88940  20.9          01:07 <home>/Library/Caches/ms-playwright/chromium_headless_shell-1248/chrome-headless-shell-mac-arm64/chrome-headless-shell
88942 88941  47.7          01:07 <home>/Library/Caches/ms-playwright/chromium_headless_shell-1248/chrome-headless-shell-mac-arm64/chrome-headless-shell
88943 88941   0.3          01:07 <home>/Library/Caches/ms-playwright/chromium_headless_shell-1248/chrome-headless-shell-mac-arm64/chrome-headless-shell
89006 88941  57.1          00:07 <home>/Library/Caches/ms-playwright/chromium_headless_shell-1248/chrome-headless-shell-mac-arm64/chrome-headless-shell
```

**gc-night end, 2026-11-15T08:53:32Z — provisional (Mac shared)**

```text
9:53  up 1 day, 15:31, 1 user, load averages: 15.09 12.79 11.31
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 53%
PID  %CPU COMM
89849  90.0 VBCSCompiler
81704  82.4 Big Ambitions Agent Tests Mac
49418  61.8 suggestd
89881  56.4 dotnet
89847  25.8 dotnet
89879  24.9 dotnet
89868  14.1 dotnet
50220  11.8 com.apple.Virtualization.VirtualMachine
89883  10.1 mdworker_shared
89882   8.6 mdworker_shared
  444   8.6 coreaudiod
```

- `20261115-033628-ux-akte-one-screen-1966`: phase=fix, status=None, finished_at=None.
- `20261115-081854-rig-gr5-perf-attribution`: phase=done, status=success, finished_at=2026-11-15T08:46:57+01:00.
- `20261115-090523-mac-fix-gr5-rebuild-food-appearance-parked`: phase=implement, status=None, finished_at=None.

Harness commands/PIDs:

```text
76767 /opt/homebrew/Cellar/python@3.15/3.15.0/Frameworks/Python.framework/Versions/3.15/Resources/Python.app/Contents/MacOS/Python -m harness resume --answer <home>/Coding_Projects/_bot-runs/mvp-harness/answer-1966-0905.md --resolved <home>/Coding_Projects/_bot-runs/mvp-harness/fresh-resolved-1966-0905.json <home>/Coding_Projects/_harness-runs/20261115-033628-ux-akte-one-screen-1966
77725 /opt/homebrew/Cellar/python@3.15/3.15.0/Frameworks/Python.framework/Versions/3.15/Resources/Python.app/Contents/MacOS/Python -m harness run --run-dir <home>/Coding_Projects/_harness-runs/20261115-090523-mac-fix-gr5-rebuild-food-appearance-parked
```

Other coding/build process PID / PPID / CPU / elapsed / command:

```text
78025 77725   4.0          47:32 <home>/.local/bin/claude
83604 76767   1.6          31:34 <home>/.local/bin/claude
89847     1  25.8          00:26 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
89848     1   0.1          00:26 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
89849 89847  90.0          00:25 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/sdk/10.0.400/Roslyn/bincore/VBCSCompiler
89868 89828  14.1          00:02 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
89879 89868  24.9          00:01 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
89881 89879  56.4          00:01 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
```

**street start, 2026-11-15T09:00:59Z — provisional (Mac shared)**

```text
10:00  up 1 day, 15:39, 1 user, load averages: 12.05 13.90 12.71
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 76%
PID  %CPU COMM
50220 186.7 com.apple.Virtualization.VirtualMachine
90142  97.6 dotnet
91302  54.8 node
81704  40.7 Big Ambitions Agent Tests Mac
50209  39.5 limactl
49418  31.8 suggestd
91772  25.0 node
50359  22.2 ssh.sock [mux]
91631  16.1 node
91444  12.9 node
91769  11.1 node
```

- `20261115-033628-ux-akte-one-screen-1966`: phase=local_tests, status=None, finished_at=None.
- `20261115-081854-rig-gr5-perf-attribution`: phase=done, status=success, finished_at=2026-11-15T08:46:57+01:00.
- `20261115-090523-mac-fix-gr5-rebuild-food-appearance-parked`: phase=local_tests, status=None, finished_at=None.

Harness commands/PIDs:

```text
76767 /opt/homebrew/Cellar/python@3.15/3.15.0/Frameworks/Python.framework/Versions/3.15/Resources/Python.app/Contents/MacOS/Python -m harness resume --answer <home>/Coding_Projects/_bot-runs/mvp-harness/answer-1966-0905.md --resolved <home>/Coding_Projects/_bot-runs/mvp-harness/fresh-resolved-1966-0905.json <home>/Coding_Projects/_harness-runs/20261115-033628-ux-akte-one-screen-1966
77725 /opt/homebrew/Cellar/python@3.15/3.15.0/Frameworks/Python.framework/Versions/3.15/Resources/Python.app/Contents/MacOS/Python -m harness run --run-dir <home>/Coding_Projects/_harness-runs/20261115-090523-mac-fix-gr5-rebuild-food-appearance-parked
```

Other coding/build process PID / PPID / CPU / elapsed / command:

```text
89847     1   0.0          07:53 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
89848     1   0.0          07:53 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
89849 89847   0.0          07:52 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/sdk/10.0.400/Roslyn/bincore/VBCSCompiler
90114 77725   0.0          06:55 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
90128 90114   0.0          06:55 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
90129 90114   0.0          06:55 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
90141 90128   0.0          06:48 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
90142 90141  97.6          06:48 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
```

**street end, 2026-11-15T09:11:08Z — provisional (Mac shared)**

```text
10:11  up 1 day, 15:49, 1 user, load averages: 14.94 14.64 13.38
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 38%
PID  %CPU COMM
96993 171.1 next-server (v16.4.2)
98077  83.4 dotnet
98200  78.3 dotnet
81704  75.6 Big Ambitions Agent Tests Mac
49418  60.5 suggestd
  444   8.8 coreaudiod
50220   5.4 com.apple.Virtualization.VirtualMachine
97312   4.0 claude
89013   3.4 Python
 1051   3.0 Grok Bot Helper
  561   2.2 mds_stores
```

- `20261115-033628-ux-akte-one-screen-1966`: phase=local_tests, status=None, finished_at=None.
- `20261115-081854-rig-gr5-perf-attribution`: phase=done, status=success, finished_at=2026-11-15T08:46:57+01:00.
- `20261115-090523-mac-fix-gr5-rebuild-food-appearance-parked`: phase=review, status=None, finished_at=None.

Harness commands/PIDs:

```text
76767 /opt/homebrew/Cellar/python@3.15/3.15.0/Frameworks/Python.framework/Versions/3.15/Resources/Python.app/Contents/MacOS/Python -m harness resume --answer <home>/Coding_Projects/_bot-runs/mvp-harness/answer-1966-0905.md --resolved <home>/Coding_Projects/_bot-runs/mvp-harness/fresh-resolved-1966-0905.json <home>/Coding_Projects/_harness-runs/20261115-033628-ux-akte-one-screen-1966
77725 /opt/homebrew/Cellar/python@3.15/3.15.0/Frameworks/Python.framework/Versions/3.15/Resources/Python.app/Contents/MacOS/Python -m harness run --run-dir <home>/Coding_Projects/_harness-runs/20261115-090523-mac-fix-gr5-rebuild-food-appearance-parked
```

Other coding/build process PID / PPID / CPU / elapsed / command:

```text
96993 96992 185.2          07:33 next-server (v16.4.2)
97312 77725   1.6          06:42 <home>/.local/bin/claude
98073 98072   0.0          02:27 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
98076 98073   0.0          02:26 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
98077 98076  78.1          02:26 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
98198 97234   0.0          00:40 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
98199 98198   0.0          00:40 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
98200 98199  71.4          00:40 /opt/homebrew/Cellar/dotnet/10.0.400/libexec/dotnet
98209 98206   0.3          00:32 <home>/Library/Caches/ms-playwright/chromium_headless_shell-1248/chrome-headless-shell-mac-arm64/chrome-headless-shell
98210 98209   0.0          00:32 <home>/Library/Caches/ms-playwright/chromium_headless_shell-1248/chrome-headless-shell-mac-arm64/chrome-headless-shell
98211 98209   0.0          00:32 <home>/Library/Caches/ms-playwright/chromium_headless_shell-1248/chrome-headless-shell-mac-arm64/chrome-headless-shell
98212 98209   0.0          00:32 <home>/Library/Caches/ms-playwright/chromium_headless_shell-1248/chrome-headless-shell-mac-arm64/chrome-headless-shell
```

### Exact invoked failures and tooling gaps

- `python3 scripts/mac-agent op ops {}` → `{   "error": "ops_hook_not_ready" }`.
- `python3 scripts/mac-agent op building {"action": "read", "place": "address:ba%3Astreet_broadwaystreet:13:home"}` → `{   "error": "ops_building_bounds_unavailable",   "reply": {     "ok": false,     "id": "7954fe19e7224125b0dc365c49cf3359",     "session": "a081ffec352e45928dbddf7f5a9da7f1",     "error": "ops_building_bounds_unavailable"   } }`.

The menu-time `ops_hook_not_ready` capability read was repeated after city load. `ops_building_bounds_unavailable` was expected for the collider-free unreadable subject. The check-4 reload returned successful ops with incorrect status/classes, not an ops_load_* error. No `ops_building_fixture_reload_incomplete` was returned. PC phase-row retention (night counted 89 / street 113, retained 64 each, versus 93 / 114 union spikes), missing delete action, independent visible-storey/per-Home evidence, exact inclusive nested UpdateCity p95, async body integration attribution and runtime commit/SHA build line remain tooling gaps.

### Player.log activation and clock excerpts

```text
[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.
[Mod:PersistentCity] Persistent population active: 1000 residents, 1000 visitors, 1685 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.
[Mod:PersistentCity] Road snapshot started: frame=3175, reason=initial index=-1, scene searches in this planner=1, reused unchanged snapshot=False.
[Mod:PersistentCity] Persistent population active: 1000 residents, 1000 visitors, 1685 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.
[Mod:PersistentCity] Road snapshot started: frame=4449, reason=initial index=-1, scene searches in this planner=1, reused unchanged snapshot=False.
[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:PersistentCity] Road snapshot started: frame=5374, reason=clock-rate index=-1, scene searches in this planner=1, reused unchanged snapshot=False.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 2 15:30.
[Mod:PersistentCity] Road snapshot started: frame=5709, reason=clock-rate index=-1, scene searches in this planner=0, reused unchanged snapshot=True.
[Mod:FoodTrucks] Test bridge: run_until day 2 15:30 reached at day 2 15:30; paused, speed 1.
[Mod:FoodTrucks] Test bridge: console 'pc gc start' (1 lines).
[Mod:PersistentCity] Road snapshot started: frame=6037, reason=clock-rate index=-1, scene searches in this planner=0, reused unchanged snapshot=True.
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (3 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 01:00.
[Mod:PersistentCity] Road snapshot started: frame=22453, reason=clock-rate index=-1, scene searches in this planner=0, reused unchanged snapshot=True.
[Mod:FoodTrucks] Test bridge: run_until day 3 01:00 reached at day 3 01:00; paused, speed 1.
[Mod:PersistentCity] Road snapshot started: frame=41662, reason=clock-rate index=-1, scene searches in this planner=0, reused unchanged snapshot=True.
[Mod:FoodTrucks] Test bridge: console 'pc gc start' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (3 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc stop' (3 lines).
[Mod:FoodTrucks] Test bridge: running the clock at speed 20 until day 3 15:30.
[Mod:PersistentCity] Road snapshot started: frame=50838, reason=clock-rate index=-1, scene searches in this planner=0, reused unchanged snapshot=True.
[Mod:FoodTrucks] Test bridge: run_until day 3 15:30 reached at day 3 15:30; paused, speed 1.
[Mod:PersistentCity] Road snapshot started: frame=60740, reason=clock-rate index=-1, scene searches in this planner=0, reused unchanged snapshot=True.
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc frames start' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc population' (1 lines).
[Mod:FoodTrucks] Test bridge: console 'pc gc frames stop' (2 lines).
```

| Window speed=1 reply | Autosave before / after suppressed |
| --- | --- |
| gc-day `{"day":2,"weekday":"Tuesday","time":"15:30","paused":false,"speed":1.0,"run_until":null,"arrived_at":null,"keep_needs":true}` | True / True |
| gc-night `{"day":3,"weekday":"Wednesday","time":"01:00","paused":false,"speed":1.0,"run_until":null,"arrived_at":null,"keep_needs":true}` | True / True |
| street `{"day":3,"weekday":"Wednesday","time":"15:30","paused":false,"speed":1.0,"run_until":null,"arrived_at":null,"keep_needs":true}` | True / True |

### NOT RUN grouped

- **GR-5 60 fps street and busy-shop arms:** Mac reaches about 5–8 fps at 1,000 residents. Wait on the PC until Peter says it is free. PC 15c was stopped mid-run at 09:05–09:10 CET when Peter started gaming; no finished PC 15c 60 fps result was copied. No Windows contact.
- **GR-5 check 2 / save latency:** owner save-route decision pending via Spock; no save profiling.
- **#262 checks 4–5 look stability / burst-refill:** waiting on resident-reacquire and dense-walkers fixtures.
- **#275 look-stability check 4:** outside this run; D above is #246 check 4 via #91.

Additional NOT RUN / tooling gaps: disposable-save deletion through a documented op, independent visible-storey/per-Home area proof, exact worst async body-growth frame and PC phase/detail/sub rows beyond recorder retention. No ordinary profile, sealed save, RLO file, schema, food-choice weight, production code, version or changelog changed. No release, Workshop, issue creation or merge.

### Cleanup and evidence validation

Stopped only the owned headless player. Fixture reverted before C; final applied=false / sidecar=false. Restored complete PersistentCity and exact install.json from receipt backup; restored isolated preferences, persistent-city config, autosave/frame-cap settings and original absent paths. Verified 35 original hashes, 3 absent paths, complete PersistentCity file set, 53 read-only RLO hashes; mismatches=[]. Receipt SHA-256 `bdfa5ed281ffea87c872fcdb630974af5aa1fbe9dc1e656254d795b2933bd5dd`. Overlay unchanged at `9a9a24f9c5b4f812233a1e710d02512c2b62bf2b638d51363afb11e6984761b4`. Lease released; final running=false, held=false, pending=0. Owned game and caffeinate processes gone. Reverted `FT RIGTMP-GR5-15c` save retained because no documented deletion op is installed.

Build passed; evidence checked with docs-links, md-ban, JSON parsing, size check and git diff --check. No test or production code changed; no full/regression suite run. The isolated build/evidence worktree is removed after commit, push and PR creation.

Refs #275
Refs #246
Refs #274
Refs #276

## Rig results — 15 November 2026 d, Mac (planned 16a; leftover FT RIGTMP-GR5-15c delete, #246 check 4 via agent-rig #93, #277 checks 1-4 at 564020b)

**RESULT: DONE. Counts: 2 passed / 1 partial / 4 failed / 4 NOT RUN groups.** Order C0, D, day, night, street, look. Timing/GC/street numbers are **PROVISIONAL (Mac shared)**. The unresolved native proof gates apply to product changes. This docs-only results PR merges after current-head reviewer approval.

Role `rig_test`, where `mac`, isolated `agentmac`. Run `persistent-city-rig-2026-11-15d-mac`, planned name Mac rig 16a. `<stage>` = `~/HART-Builds/persistent-city-rig-2026-11-15d-mac`; raw replies, logs, scripts, screenshots and backups remain there. Report copy `~/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-15d-mac.md`. MB is decimal.

[Compact GC/frame evidence](perf/gr5-2026-11-15d-mac.json): 12174 bytes, SHA-256 `def8f4091e00d8d461e870e1311f0b5c504799ff64a9d5f38ea439076fef39bc`.

Raw evidence index `<stage>/raw-evidence-manifest.json`, SHA-256 `66b129a2e5c8f7dd2db5ddcb25f6b355b89145a010acd4369693e05a2933a49f`. Full GC JSON has per-source SHA-256 and stage-relative pointers in compact evidence; the tables below are the excerpt.

### Build and rig

| Item | Evidence |
| --- | --- |
| Candidate | `564020b73298ee2a268eb60f09c55bc99a69716f`; requested tip reverified and unchanged at build. Built in new `rig-2026-11-15d-mac-evidence` worktree, branch `codex/mac-rig-2026-11-15d`. |
| agent-rig HEAD | `e3e62a200ae3438d56ff06432811d2c8778de762` (#93, includes #92); clean, fetch and ff-only pull current. No checkout edits, commits or pushes; no overlay update/rollback. |
| Live AgentRigOps SHA-256 | `aa3481a680defffca4290069a29a0eb53332ff97ac95e5f9e94fb17aab13726e`; before start, loaded health and cleanup verification. |
| PersistentCity DLL SHA-256 | `4d9adc4e98c23722cd370b08c4b6dc3a101ae37fc60afae12a52ec7bf8e02258`; built, packaged and staged. |
| Build | `PATH=/opt/homebrew/bin:$PATH`; `DOTNET_ROOT` from Homebrew dotnet; `python3 tools/build.py --game-managed "~/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0. Package contains no game/RLO assemblies. |
| Stage | `python3 scripts/install-mac.py --stage-mod PersistentCity ~/HART-Builds/persistent-city-rig-2026-11-15d-mac/package`; stopped and lease held. Receipt backup `~/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261115T111514Z`; 28 entries refreshed. |
| Headless processes | C0 PID 49403; D and all timing PID 50212. Restart between C0 and D to reach the menu for new_game. No ops_load_* failure. |
| RLO | Real Life Overhaul 1.0.6 initialised. Options read: living_population stored absent/live=true; social_media=1/true; social_media_notices=0/false. No RLO changes. |
| Autosave and SaveCost | Original native autosave 5 minutes / 3 saves, prevent_autosave=false. #70 suppression verified after loads and before/after all windows. SaveCostDiagnostics requested=false; writerSchema=1; save profiler installed=false. No save latency measurement. |
| City names | `FT GR5 13f Timing v1`: 1,000 residents / 2,000 visitors / 1,683 places; no fallback. Fresh `FT RIGTMP-GR5-15d-mac`: 1,000 residents / 1,000 visitors; deleted after revert. Leftover `FT RIGTMP-GR5-15c` deleted; `FT RIGTMP-GR5-15c-mac` absent. |
| Lease | Free/stopped at launch; acquired 2026-11-15T11:14:19.675102Z. No Windows contact or other-session intervention. |

Stage and Player.log both record:

```text
[AgentRigOps] staged_mod name=PersistentCity version=absent informational=0.10.0-persistent-city-candidate sha256=4d9adc4e98c23722cd370b08c4b6dc3a101ae37fc60afae12a52ec7bf8e02258
```

The informational version contains no commit stamp; the staged DLL hash identifies the tested build. The same line is retained from the start logs. Persistent City activation lines and RLO version are retained in the raw Player logs.

Printed rollback, verbatim apart from replacing the personal home prefix with `~` (quoted paths require expanding it before execution):

```sh
rm -rf "~/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "~/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261115T111514Z/mod" "~/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "~/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261115T111514Z/install.json" "~/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

Final fetched origin/codex/persistent-city tip: `1cbcc130335116dfc5968861983586d5a4e6531d`; moved after build; this run remains pinned to the tested candidate above.

Own pre-stage manifest `<stage>/pre-stage-manifest.json`, SHA-256 `6d5b7f3e6a8cb4b9a9c396da588ba7e86fdac8e3af46a24922a0e50fed5566f5`; backups remain outside Git.

### C0 leftover deletion

**PASS.** Loaded `FT GR5 13f Timing v1` first. Every read/delete reply follows; deletion ran under the lease, with the target not loaded. No sidecar refusal or orphan-sidecar deletion. `FT RIGTMP-GR5-14i` and sealed saves were not deleted.

`C0-read-FT RIGTMP-GR5-15c`: `{"name":"FT RIGTMP-GR5-15c","exists":true,"loaded":false,"files":[{"file":"Big Ambitions/ArZSVdJA8U6h1lAJnx221A==/FT RIGTMP-GR5-15c.hsg","bytes":3433184},{"file":"Big Ambitions/ArZSVdJA8U6h1lAJnx221A==/FT RIGTMP-GR5-15c.hsg.meta","bytes":5403},{"file":"Big Ambitions/ArZSVdJA8U6h1lAJnx221A==/FT RIGTMP-GR5-15c.jpg","bytes":4723}]}`

`C0-delete-FT RIGTMP-GR5-15c`: `{"before":{"name":"FT RIGTMP-GR5-15c","exists":true,"loaded":false,"files":[{"file":"Big Ambitions/ArZSVdJA8U6h1lAJnx221A==/FT RIGTMP-GR5-15c.hsg","bytes":3433184},{"file":"Big Ambitions/ArZSVdJA8U6h1lAJnx221A==/FT RIGTMP-GR5-15c.hsg.meta","bytes":5403},{"file":"Big Ambitions/ArZSVdJA8U6h1lAJnx221A==/FT RIGTMP-GR5-15c.jpg","bytes":4723}]},"after":{"name":"FT RIGTMP-GR5-15c","exists":false,"loaded":false,"files":[]}}`

`C0-read-FT RIGTMP-GR5-15c-mac`: `{"name":"FT RIGTMP-GR5-15c-mac","exists":false,"loaded":false,"files":[]}`

### D: #246 check 4, fresh fixture and check-5 tally

Check 4 Expected: "footprint within about 1.5x of the visible ground area and floors within one of the visible storeys." **FAIL:** the plain subject has collider-derived floors 11 versus renderer-derived visible storeys 13. The independent visible-storey field remains labelled derived; no native exterior floor count exists in this op. This is not a visual floor-count pass.

Check 5 Expected: "buildings under one storey count as empty, not unreadable, and the omitted-places warning only counts unreadable ones." **PASS** on this load: rig/PC tally 3 housed / 1 unreadable / 1 empty; the empty subject has collider-derived storeys=0 and the omitted-place warning counts 1 unreadable, 0 undersized.

1. Created exactly `FT RIGTMP-GR5-15d-mac`, no throwaway flag. Paused and verified autosave suppression.
2. Apply: all five roles supplied, missing=[], applied=true, sidecar=true.
3. In-load list: 585 residential / 5 unrentable, 3 housed / 1 unreadable / 1 empty, all five pc_home_candidate=true; 3 housed include rotated and yard rows. The earlier PC tally is not observable in-load after apply (0/0/0 before mutation).
4. Default save only under the same loaded disposable name, overwrite=true; waited for compression to settle.
5. Bounded load of that name completed; paused. Immediate status was the first fixture operation before any other mutation.
6. Status applied=True, restored_on_load=True, adopted_at=`CityManager.InitCityBuildingControllers`, sidecar=True; load_hook.installed=True, error=None. Same place per role as apply; all-or-nothing adoption succeeded. Both load-hook seams have no error; the fallback reports already_adopted. Repeated list checks match apply. No late adoption used.
7. Revert returned applied=false, sidecar=false; original sizes and colliders restored. Post-revert unrentable list is zero.
8. Loaded sealed timing fixture, then disposable_save read/delete; after.exists=false. Both replies follow.

`D-delete-read`: `{"name":"FT RIGTMP-GR5-15d-mac","exists":true,"loaded":false,"files":[{"file":"Big Ambitions/eEVpwA8U6E2gkY5YPHcsTg==/FT RIGTMP-GR5-15d-mac.hsg","bytes":2966714},{"file":"Big Ambitions/eEVpwA8U6E2gkY5YPHcsTg==/FT RIGTMP-GR5-15d-mac.hsg.meta","bytes":8595},{"file":"Big Ambitions/eEVpwA8U6E2gkY5YPHcsTg==/FT RIGTMP-GR5-15d-mac.jpg","bytes":4723}]}`

`D-delete`: `{"before":{"name":"FT RIGTMP-GR5-15d-mac","exists":true,"loaded":false,"files":[{"file":"Big Ambitions/eEVpwA8U6E2gkY5YPHcsTg==/FT RIGTMP-GR5-15d-mac.hsg","bytes":2966714},{"file":"Big Ambitions/eEVpwA8U6E2gkY5YPHcsTg==/FT RIGTMP-GR5-15d-mac.hsg.meta","bytes":8595},{"file":"Big Ambitions/eEVpwA8U6E2gkY5YPHcsTg==/FT RIGTMP-GR5-15d-mac.jpg","bytes":4723}]},"after":{"name":"FT RIGTMP-GR5-15d-mac","exists":false,"loaded":false,"files":[]}}`

| Role | Place | Original size | Apply / reload class |
| --- | --- | --- | --- |
| housed_rotated | `address:ba%3Astreet_firststreet:22:home` | ba:buildingsize_l | housed / housed |
| housed_yard | `address:ba%3Astreet_fifthavenue:25:home` | ba:buildingsize_b | housed / housed |
| housed_plain | `address:ba%3Astreet_broadwaystreet:2:home` | ba:buildingsize_l | housed / housed |
| unreadable | `address:ba%3Astreet_broadwaystreet:13:home` | ba:buildingsize_n | unreadable / unreadable |
| empty | `address:ba%3Astreet_broadwaystreet:23:home` | ba:buildingsize_n | empty / empty |

after.missing at apply/reload: `[]` / `[]`.

| Role / place | Reload class | PC-derived floors | Independent visible storeys | Independent footprint / gross / usable m² | world collider / visible AABB area |
| --- | --- | --- | --- | --- | --- |
| housed_rotated / `address:ba%3Astreet_firststreet:22:home` | housed | 13 | 14 | 1431.112 / 20035.568 / 12021.341 | 0.9967 |
| housed_yard / `address:ba%3Astreet_fifthavenue:25:home` | housed | 5 | 6 | 426.709 / 2560.254 / 1536.152 | 0.9759 |
| housed_plain / `address:ba%3Astreet_broadwaystreet:2:home` | housed | 11 | 13 | 3826.154 / 49740.002 / 29844.001 | 1.0 |

Independent fields explicitly exclude BuildingSize, BuildingSizeHelper squareMeters, the fixture journal and sidecar. Housed rotated/yard/plain visible heights are 49.920 / 23.514 / 47.418 m; independent visible storeys 14 / 6 / 13; collider floors 13 / 5 / 11. Footprint ratios are within 1.5x; the plain floor difference is two. The independent per-Home usable areas above are geometry readbacks, not fixture-written BuildingSize or saved Home records. A separate saved-Home area extraction was not performed.

Unreadable read fails closed with exact `ops_building_bounds_unavailable`, before and after reload; no count is invented. Empty list storeys=0; independent renderer storeys=8 and home_area follows its one retained collider (footprint=2528.194, gross=20225.552, usable=12135.331 m²), which is not evidence that PC houses it. No role is missing.

| List | matched | geometry_reads / slices | timeout |
| --- | --- | --- | --- |
| D-list-all | 5 | 5 / 1 | 120 s |
| D-list-yard | 1 | 5 / 1 | 120 s |
| D-list-unreadable | 1 | 5 / 1 | 120 s |
| D-list-empty | 1 | 5 / 1 | 120 s |
| D-reloaded-list-all | 5 | 5 / 1 | 120 s |
| D-reloaded-list-yard | 1 | 5 / 1 | 120 s |
| D-reloaded-list-unreadable | 1 | 5 / 1 | 120 s |
| D-reloaded-list-empty | 1 | 5 / 1 | 120 s |

Player.log after fixture reload:

```text
[Persistent City] unrentable homes housed=0 beds=0 unreadable=0 empty=0
[Persistent City] unrentable homes housed=3 beds=1270 unreadable=1 empty=1
[Persistent City] Residential places omitted: 1 without a finite footprint or floor count; 0 below the household policy's minimum interior area.
```

### #277 Expected lines and per-check outcomes

> 1. **Day GC window.** Setup: load the fixture, clock 15:30, idle camera. Steps: run a 600 s gc window. Expected: report gen0, pause p95, heap, mod, physical and advance, and every segment and sub-segment against rig 15b (mod 177.9 MB, gen0 7, pause p95 43.4 ms). `physical_parked` back near 15a (2.0 MB) with about 600 samples. Report `advance_events_food` bytes per call against 15b (about 28.7 KB).
> 2. **Night GC window.** Same as check 1 at 01:00, against 15b night (78.7 MB, gen0 6, pause p95 38.9 ms; parked 8.8 MB). For every ≥30 ms spike report phase, detail, sub, `gc_phase` and `collections`.
> 3. **Street window.** Setup: same fixture, daytime, pan the camera. Steps: run a 600 s street window. Expected: report UpdateCity self/union and mod-work p95 against 15b (6.80 / 6.96 ms), every ≥30 ms spike with phase, detail, sub and `gc_phase`, and whole-frame p95/max separately. No ≥30 ms frame from `physical_parked`, `physical_release` or `mod_routes_rebuild` outside the retime update; copy the `Clock retime:` Player.log line for any retime in the window.
> 4. **Look stability.** Setup: same fixture, daytime street. Steps: follow 5 residents through body release and reacquire, change speed once (retime), then reload and find them again. Expected: dressed bodies and the same look per resident before and after; parked cars at their saved curbs; none of "Appearance meshes unavailable", "Empty appearance" or "Merged body" in Player.log.

**gc-day: FAIL, PROVISIONAL (Mac shared).** gen0 6 (target <=5: not met), pause p95 674.160 ms (target <36: not met), mod 139.858 MB (target <=119: not met).

**gc-night: FAIL, PROVISIONAL (Mac shared).** gen0 5 (target <=5: met), pause p95 45.630 ms (target <36: not met), mod 75.720 MB (target <=53: not met).

**Street: FAIL, PROVISIONAL (Mac shared).** UpdateCity exclusive / inclusive p95 3.7571 / 3.7571 ms; mod-work union p95 3.8278 ms; 1 >=30 ms union spikes. Gate inclusive UpdateCity and union <5 ms: met; zero >=30 ms mod-work frames: not met.

### GC totals and 15a/15b/15c comparisons

Idle camera for GC, speed 1 inside windows; speed 20 advances outside, including the advance to 01:00. Player x=215,z=0. Each controller hold is 600 s; exported durations include begin/end command overhead. No save inside any window; perf save_activity and autosave replies below. `boehm_total` pause is cycle-ending frame excess, not all incremental collector work.

| GC window (PROVISIONAL) | frames / dropped | gen0 / gen1 / gen2 | pause p50 / p95 / max ms | heap_after_gc_bytes / mono_heap_bytes | mod / physical / advance MB |
| --- | --- | --- | --- | --- | --- |
| gc-day | 15347 / 0 | 6 / 0 / 0 | 44.262 / 674.16 / 674.16 | 1485079893 / 1907130368 | 139.858 / 45.710 / 74.047 |
| gc-night | 16999 / 0 | 5 / 0 / 0 | 42.606 / 45.63 / 45.63 | 1661009920 / 1822670848 | 75.720 / 22.489 / 34.783 |

| day / night | 15a #274 | 15b #276 | 15c #278 PROVISIONAL, NOT a baseline | 15d PROVISIONAL |
| --- | --- | --- | --- | --- |
| mod MB | 295.628 / 151.470 | 177.883 / 78.728 | 272.206 / 66.856 | 139.858 / 75.720 |
| physical MB | 112.167 / 36.201 | 64.263 / 32.291 | 59.968 / 24.582 | 45.710 / 22.489 |
| advance MB | 97.634 / 30.515 | 95.208 / 29.369 | 87.318 / 26.937 | 74.047 / 34.783 |
| gen0 | 8.000 / 6.000 | 7.000 / 6.000 | 6.000 / 4.000 | 6.000 / 5.000 |
| pause p95 ms | 27.888 / 37.301 | 43.403 / 38.929 | 110.570 / 4651.381 | 674.160 / 45.630 |

15a street self/union p95 6.5466/6.6550 ms, 37 spikes; 15b 6.80/6.96 ms, 28 spikes, including the 244 ms mod_routes_rebuild frame 95966. 15c 16.81/17.22 ms, 114 spikes; its night pause p95 4651 ms was a host-load artefact and all 15c timing is provisional, NOT a baseline. Shared-host differences do not establish a product regression or controlled A/B improvement.

Profiler GC durations are reported separately from the PC sampler’s cycle-ending frame excess. A large shared-host frame excess is not asserted to be a GC.Collect marker duration.

| Window PROVISIONAL | PC sampler pause p95 ms | rig marker rows / collection rows | collection-row duration p95 ms | all marker max / total ms |
| --- | --- | --- | --- | --- |
| gc-day | 674.16 | 13 / 6 | 36.7109 | 36.7109 / 225.9039 |
| gc-night | 45.63 | 10 / 5 | 35.3608 | 35.3608 / 180.0405 |

### Every allocation segment and sub-segment

| Segment | 15b day / night bytes | 15d day bytes / samples / skipped | 15d night bytes / samples / skipped |
| --- | --- | --- | --- |
| frame | 1093959680 / 795942912 | 802836480 / not exported / not exported | 806047744 / not exported / not exported |
| mod | 177882992 / 78728440 | 139857600 / 15348 / 0 | 75719880 / 17000 / 0 |
| advance | 95207656 / 29369216 | 74046960 / 15335 / 0 | 34783168 / 16996 / 0 |
| physical | 64262504 / 32290920 | 45710112 / 15349 / 0 | 22488520 / 17001 / 0 |
| physical_ambient | 2179920 / 1953440 | 1966288 / 15335 / 0 | 2186960 / 16996 / 0 |
| physical_vehicles | 1205504 / 462208 | 877120 / 15335 / 0 | 433344 / 16996 / 0 |
| physical_bindings | 11872 / 11552 | 11408 / 336913 / 0 | 1520 / 197071 / 0 |
| physical_vendor_meals | 0 / 144 | 0 / 15335 / 0 | 656 / 16996 / 0 |
| physical_fade | 0 / 0 | 0 / 15335 / 0 | 32 / 16996 / 0 |
| physical_focus | 0 / 0 | 0 / 15335 / 0 | 2192 / 16996 / 0 |
| physical_observe | 314400 / 119712 | 388432 / 321350 / 0 | 277744 / 179903 / 0 |
| physical_release | 7904 / 3296 | 5408 / 244 / 0 | 1584 / 234 / 0 |
| physical_parked | 9590064 / 8823728 | 1797232 / 1190 / 0 | 1605056 / 1193 / 0 |
| physical_index | 80 / 80 | 80 / 2130 / 0 | 80 / 2208 / 0 |
| physical_queues | 8960 / 17264 | 9024 / 2130 / 0 | 17456 / 2208 / 0 |
| physical_roster | 15024 / 29424 | 126832 / 2130 / 0 | 29392 / 2208 / 0 |
| physical_selection | 1402928 / 901600 | 1289176 / 2591 / 0 | 553864 / 2386 / 0 |
| physical_spawn_pedestrian | 3209472 / 424576 | 2682384 / 599 / 0 | 63560 / 250 / 0 |
| physical_spawn_appearance | 43075248 / 16589824 | 33456448 / 624 / 0 | 14148608 / 291 / 0 |
| physical_spawn_check | 32 / 0 | 0 / 416 / 0 | 0 / 194 / 0 |
| physical_spawn_vehicle | 128624 / 82352 | 233248 / 70 / 0 | 41024 / 25 / 0 |
| physical_unattributed | 3112472 / 2871720 | 2867032 / 30686 / 0 | 3125448 / 33999 / 0 |
| physical_spawn_randomize | 2404336 / 875872 | 1869408 / 208 / 0 | 845360 / 97 / 0 |
| physical_spawn_bake | 2775056 / 1691520 | 2127792 / 208 / 0 | 432 / 97 / 0 |
| advance_planning | 45640960 / 19310992 | 38375672 / 9371 / 0 | 23007504 / 6608 / 0 |
| advance_events | 47117976 / 7864096 | 33463048 / 18966 / 0 | 9324272 / 20782 / 0 |
| advance_stops | 2448720 / 2194128 | 2208240 / 15335 / 0 | 2449712 / 16996 / 0 |
| advance_other | 0 / 0 | 0 / 15335 / 0 | 1104 / 16996 / 0 |
| physical_spawn_grow | 875744 / 32464 | 804608 / 155 / 0 | 0 / 0 / 0 |
| mod_prelude | 2535600 / 2284992 | 2331440 / 15348 / 0 | 2587392 / 17000 / 0 |
| mod_rlo | 3529184 / 3535808 | 3530176 / 15348 / 0 | 3541104 / 17000 / 0 |
| mod_food | 1277184 / 856576 | 1278384 / 15348 / 0 | 1277760 / 17000 / 0 |
| mod_optional_stops | 2450576 / 2194704 | 2232912 / 15348 / 0 | 2454032 / 17000 / 0 |
| mod_routes | 5363584 / 4924048 | 7669584 / 15348 / 0 | 5283984 / 17000 / 0 |
| mod_closures | 388480 / 713200 | 388864 / 15335 / 0 | 492544 / 16996 / 0 |
| mod_hold_replay | 0 / 0 | 0 / 15335 / 0 | 224 / 16996 / 0 |
| mod_physical_setup | 0 / 0 | 0 / 14576 / 0 | 0 / 13926 / 0 |
| mod_inspector | 2488160 / 2197744 | 2296736 / 15335 / 0 | 2447552 / 16996 / 0 |
| mod_summary | 362416 / 361104 | 364032 / 15311 / 0 | 362832 / 16954 / 0 |
| mod_other | 3808 / 0 | 2720 / 15348 / 0 | 0 / 17000 / 0 |
| mod_routes_speeds | 902080 / 845344 | 801472 / 12523 / 0 | 1034064 / 15874 / 0 |
| mod_routes_nodes | 516096 / 447488 | 3160720 / 12523 / 0 | 432832 / 15874 / 0 |
| mod_routes_sweep | 0 / 256 | 0 / 15335 / 0 | 80 / 16996 / 0 |
| mod_routes_rebuild | 3945408 / 3630960 | 3706896 / 15348 / 0 | 3816400 / 17000 / 0 |
| mod_closures_network | 229088 / 550080 | 229536 / 15335 / 0 | 331776 / 16996 / 0 |
| mod_closures_links | 159392 / 159392 | 159328 / 15335 / 0 | 159936 / 16996 / 0 |
| advance_events_traffic | 288496 / 225360 | 20672 / 1479 / 0 | 46688 / 850 / 0 |
| advance_events_food | 28296576 / 3878448 | 20541984 / 788 / 0 | 3878496 / 410 / 0 |
| advance_events_citizen | 13546712 / 2119344 | 7811560 / 3010 / 0 | 1912032 / 1917 / 0 |
| advance_events_route_begin | 4986192 / 1640848 | 5088832 / 3489 / 0 | 3485280 / 3108 / 0 |
| advance_events_other | 0 / 64 | 0 / 10200 / 0 | 1776 / 14497 / 0 |
| advance_planning_slice | 29432344 / 11428080 | 24722968 / 5883 / 0 | 17071472 / 3500 / 0 |
| advance_planning_finish | 16208616 / 7882912 | 13652704 / 3488 / 0 | 5936032 / 3108 / 0 |
| physical_setup_places | 273928 / 273928 | 273992 / 1 / 0 | 273992 / 1 / 0 |
| physical_setup_bodies | 180032 / 206128 | 180032 / 1 / 0 | 206128 / 1 / 0 |
| physical_setup_vehicles | 15696 / 15616 | 15984 / 2 / 0 | 15904 / 2 / 0 |
| physical_setup_ambient | 174192 / 174192 | 173776 / 2 / 0 | 174192 / 2 / 0 |
| physical_setup_preflight | 2000 / 752 | 2000 / 1 / 0 | 752 / 1 / 0 |
| physical_unattributed_setup | 4240 / 4240 | 3568 / 1 / 0 | 4240 / 1 / 0 |
| physical_unattributed_update | 12944 / 2160 | 7568 / 15335 / 0 | 624 / 16996 / 0 |
| physical_unattributed_waiting | 720 / 576 | 1872 / 13 / 0 | 576 / 4 / 0 |
| physical_unattributed_entry | 2448720 / 2194128 | 2208240 / 15335 / 0 | 2448912 / 16996 / 0 |

All 15d GC values are PROVISIONAL (Mac shared). Child segments overlap their parents; do not add them again. Baseline 15b bytes are shown for every exported segment; absent fields remain not exported.

gc-day: physical_parked 1.797 MB / 1190 samples (Expected near 2.0 MB and about 600 samples); advance_events_food 20541984 bytes / 788 calls = 26068.5 B/call versus same-window 15b 28669.3 B/call (day Expected reference about 28.7 KB/call).

gc-night: physical_parked 1.605 MB / 1193 samples (Expected near 2.0 MB and about 600 samples); advance_events_food 3878496 bytes / 410 calls = 9459.7 B/call versus same-window 15b 12120.1 B/call (day Expected reference about 28.7 KB/call).

### Inclusive/exclusive timing, whole frames and every spike

| Window PROVISIONAL | seconds / frames | UpdateCity self / inclusive p95 ms | union p95 / max ms / >=30 count | whole-frame p95 / max ms |
| --- | --- | --- | --- | --- |
| gc-day | 600.733 / 15353 | 0.3677 / 3.6083 | 3.6738 / 112.6671 / 12 | 60.5975 / 1930.4984 |
| gc-night | 600.670 / 17009 | 0.2774 / 2.4411 | 2.4921 / 32.7752 / 1 | 39.3452 / 579.2371 |
| street | 600.947 / 4754 | 3.7571 / 3.7571 | 3.8278 / 44.0253 / 1 | 202.3171 / 1254.2650 |

**gc-day: every component, exclusive / inclusive.** Actual UpdateCity key: `components.updatecity`; inclusive p95 uses `components.updatecity.inclusive.p95_ms`.

| Component | calls | exclusive p95 / max / total ms | inclusive p95 / max / total ms |
| --- | --- | --- | --- |
| advance | 15339 | 2.5223 / 44.3182 / 6921.9543 | 2.5223 / 44.3182 / 6921.9543 |
| PopulationRuntime.LateUpdate | 15354 | 0.0042 / 0.9168 / 40.2165 | 0.0326 / 1.355 / 379.4641 |
| PopulationRuntime.Update | 15354 | 0.0108 / 0.8426 / 130.1117 | 3.6153 / 111.7748 / 19060.2713 |
| inspector | 15339 | 0.0083 / 0.3665 / 89.6653 | 0.0083 / 0.3665 / 89.6653 |
| AmbientPedestrians.SetActivePrefix | 3 | 0.0 / 0.0191 / 0.0457 | 0.0 / 0.0191 / 0.0457 |
| NativeCityClock.AccountingPrefix | 15354 | 0.0176 / 0.909 / 227.2464 | 0.0176 / 0.909 / 227.2464 |
| NativeCityClock.RidePostfix | 15354 | 0.0006 / 0.0274 / 8.108 | 0.0006 / 0.0274 / 8.108 |
| NativeCityClock.RidePrefix | 15354 | 0.0007 / 0.0155 / 8.3887 | 0.0007 / 0.0155 / 8.3887 |
| NativeCityClock.TickPrefix | 15354 | 0.0012 / 0.0139 / 14.8219 | 0.0012 / 0.0139 / 14.8219 |
| NativeFoodOwnership.AfterNativeTick | 15354 | 0.0008 / 0.8733 / 11.7326 | 0.0008 / 0.8733 / 11.7326 |
| NativeTravelPace.ObserveTickPostfix | 15354 | 0.0018 / 0.0245 / 23.1392 | 0.0018 / 0.0245 / 23.1392 |
| ResidentInspector.CameraInputPostfix | 30708 | 0.0011 / 0.0126 / 10.6363 | 0.0011 / 0.0126 / 10.6363 |
| ResidentInspector.MouseRunPrefix | 15354 | 0.002 / 0.0532 / 24.0799 | 0.002 / 0.0532 / 24.0799 |
| ResidentInspector.UiRaycastPrefix | 568098 | 0.0085 / 0.4217 / 115.3195 | 0.0085 / 0.4217 / 115.3195 |
| physical | 46047 | 0.9797 / 51.698 / 7222.6452 | 0.9797 / 51.698 / 7222.6452 |
| prepare | 15354 | 0.0502 / 3.4916 / 883.0983 | 0.0502 / 3.4916 / 883.0983 |
| PhysicalPedestrianCrosswalk.OnTriggerEnter | 1102 | 0.0 / 1.4312 / 12.4013 | 0.0 / 1.4312 / 12.4013 |
| PhysicalPedestrianCrosswalk.OnTriggerExit | 646 | 0.0 / 1.8063 / 7.6331 | 0.0 / 1.8063 / 7.6331 |
| updatecity | 15354 | 0.3677 / 53.9274 / 4152.0441 | 3.6083 / 111.4937 / 18930.1596 |

Spike retention: union 12 counted / 12 listed; PC phase recorder 11 counted / 11 spike rows; 0 union rows lack retained phase attribution. Rig gap 3 spike-row cap fix was not installed. The tested Core/ModFrameRecorder.cs has SpikeCapacity=64; rows beyond that cap can be absent. The rig union list has its own separately reported count; a 256-row phase capacity is not assumed. Missing rows/fields below are explicit.

PC phase count/list truncation: none observed in this window. GC-pause row retention is reported by its count/listed/truncated fields below.

| unity_frame | union ms | dominant | phase / ms | detail / ms | sub / ms | gc_phase | collections | whole-frame ms |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 22667 | 62.892 | updatecity | physical_spawn_pedestrian / 23.94 | physical_spawn_grow / 18.807 | physical_setup_ambient / 8.212 | none | 0 | 95.5404 |
| 35133 | 32.7978 | physical | physical_spawn_pedestrian / 17.272 | physical_spawn_bake / 2.331 | mod_closures_links / 3.917 | none | 0 | 118.0647 |
| 35162 | 42.0749 | physical | physical_index / 30.405 | physical_spawn_grow / 3.348 | advance_planning_slice / 5.427 | none | 0 | 133.3285 |
| 35314 | 38.5543 | physical | physical_spawn_appearance / 6.69 | physical_spawn_bake / 0.618 | mod_closures_links / 2.448 | none | 0 | 128.1626 |
| 35578 | 49.4453 | physical | physical_parked / 18.176 | physical_spawn_bake / 0.87 | advance_planning_slice / 1.812 | none | 0 | 121.8318 |
| 35678 | 38.2745 | physical | physical_parked / 17.713 | none / 0 | mod_closures_links / 3.772 | none | 0 | 1930.4984 |
| 35679 | 112.6671 | updatecity | mod_rlo / 45.77 | physical_spawn_bake / 2.022 | advance_planning_slice / 5.018 | none | 0 | 888.101 |
| 35681 | 66.73 | physical | physical_parked / 19.592 | none / 0 | advance_events_route_begin / 7.239 | none | 0 | 816.0729 |
| 35688 | 30.0071 | advance | advance_planning / 27.03 | none / 0 | advance_planning_slice / 27.028 | none | 0 | 71.8551 |
| 35741 | 39.9277 | physical | physical_index / 24.654 | physical_spawn_grow / 10.672 | advance_planning_slice / 2.018 | none | 0 | 128.0054 |
| 35743 | 31.2331 | physical | physical_parked / 6.834 | none / 0 | mod_closures_links / 5.284 | none | 0 | 142.1686 |
| 36046 | 45.3821 | advance | advance_events / 44.283 | none / 0 | advance_events_citizen / 44.282 | none | 0 | 106.4 |

Whole-frame exports are separate: perf p95/max 60.8784/2033.8661 ms; 14656 >=30 ms whole frames, 256 rows listed, truncated flag=False. Count/list differences mean later whole-frame rows are not all exported even when that flag is false; these are not mod-work spikes.

coverage.complete=True; validity=`{"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}`; save_activity=`{"attempts":15370,"allowed":0,"refused":15370,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}`.

`gc_pauses` from modwork end (unity_frame keyed): `{"format":"agent-rig-gc-pauses/1","frame_id":"UnityEngine.Time.frameCount","collection_source":"System.GC.CollectionCount","duration_source":"Unity.Profiling.ProfilerRecorder:GC.Collect","duration_unavailable_reason":null,"count":13,"listed":13,"truncated":false,"total_duration_ms":225.9039,"max_duration_ms":36.7109,"pauses":[{"unity_frame":24322,"duration_ms":4.279,"collections":0,"generation":null},{"unity_frame":24547,"duration_ms":6.9954,"collections":0,"generation":null},{"unity_frame":24551,"duration_ms":33.8563,"collections":1,"generation":0},{"unity_frame":27000,"duration_ms":2.998,"collections":0,"generation":null},{"unity_frame":27007,"duration_ms":30.3202,"collections":1,"generation":0},{"unity_frame":29508,"duration_ms":2.478,"collections":0,"generation":null},{"unity_frame":29568,"duration_ms":34.1421,"collections":1,"generation":0},{"unity_frame":32067,"duration_ms":3.0153,"collections":0,"generation":null},{"unity_frame":32171,"duration_ms":36.7109,"collections":1,"generation":0},{"unity_frame":34596,"duration_ms":2.067,"collections":0,"generation":null},{"unity_frame":34609,"duration_ms":31.157,"collections":1,"generation":0},{"unity_frame":37137,"duration_ms":3.0022,"collections":0,"generation":null},{"unity_frame":37192,"duration_ms":34.8825,"collections":1,"generation":0}]}`.

`gc_pauses` from perf end (unity_frame keyed): `{"format":"agent-rig-gc-pauses/1","frame_id":"UnityEngine.Time.frameCount","collection_source":"System.GC.CollectionCount","duration_source":"Unity.Profiling.ProfilerRecorder:GC.Collect","duration_unavailable_reason":null,"count":13,"listed":13,"truncated":false,"total_duration_ms":225.9039,"max_duration_ms":36.7109,"pauses":[{"unity_frame":24322,"duration_ms":4.279,"collections":0,"generation":null},{"unity_frame":24547,"duration_ms":6.9954,"collections":0,"generation":null},{"unity_frame":24551,"duration_ms":33.8563,"collections":1,"generation":0},{"unity_frame":27000,"duration_ms":2.998,"collections":0,"generation":null},{"unity_frame":27007,"duration_ms":30.3202,"collections":1,"generation":0},{"unity_frame":29508,"duration_ms":2.478,"collections":0,"generation":null},{"unity_frame":29568,"duration_ms":34.1421,"collections":1,"generation":0},{"unity_frame":32067,"duration_ms":3.0153,"collections":0,"generation":null},{"unity_frame":32171,"duration_ms":36.7109,"collections":1,"generation":0},{"unity_frame":34596,"duration_ms":2.067,"collections":0,"generation":null},{"unity_frame":34609,"duration_ms":31.157,"collections":1,"generation":0},{"unity_frame":37137,"duration_ms":3.0022,"collections":0,"generation":null},{"unity_frame":37192,"duration_ms":34.8825,"collections":1,"generation":0}]}`.

**gc-night: every component, exclusive / inclusive.** Actual UpdateCity key: `components.updatecity`; inclusive p95 uses `components.updatecity.inclusive.p95_ms`.

| Component | calls | exclusive p95 / max / total ms | inclusive p95 / max / total ms |
| --- | --- | --- | --- |
| advance | 17001 | 1.6773 / 6.5914 / 3420.4939 | 1.6773 / 6.5914 / 3420.4939 |
| PopulationRuntime.LateUpdate | 17010 | 0.003 / 0.0393 / 30.4153 | 0.0259 / 1.902 / 341.2655 |
| PopulationRuntime.Update | 17010 | 0.0084 / 0.0688 / 120.646 | 2.4478 / 32.7315 / 10194.6656 |
| inspector | 17001 | 0.0061 / 0.0134 / 78.4857 | 0.0061 / 0.0134 / 78.4857 |
| AmbientPedestrians.SetActivePrefix | 3 | 0.0 / 0.0082 / 0.0201 | 0.0 / 0.0082 / 0.0201 |
| NativeCityClock.AccountingPrefix | 17010 | 0.0149 / 0.496 / 222.9575 | 0.0149 / 0.496 / 222.9575 |
| NativeCityClock.RidePostfix | 17010 | 0.0006 / 0.0059 / 8.7008 | 0.0006 / 0.0059 / 8.7008 |
| NativeCityClock.RidePrefix | 17010 | 0.0006 / 0.0041 / 8.9001 | 0.0006 / 0.0041 / 8.9001 |
| NativeCityClock.TickPrefix | 17010 | 0.0011 / 0.0087 / 15.7028 | 0.0011 / 0.0087 / 15.7028 |
| NativeFoodOwnership.AfterNativeTick | 17010 | 0.0008 / 0.0219 / 11.237 | 0.0008 / 0.0219 / 11.237 |
| NativeTravelPace.ObserveTickPostfix | 17010 | 0.0018 / 0.0184 / 25.8847 | 0.0018 / 0.0184 / 25.8847 |
| ResidentInspector.CameraInputPostfix | 34020 | 0.0008 / 0.0099 / 9.9856 | 0.0008 / 0.0099 / 9.9856 |
| ResidentInspector.MouseRunPrefix | 17010 | 0.0017 / 0.0094 / 24.3153 | 0.0017 / 0.0094 / 24.3153 |
| ResidentInspector.UiRaycastPrefix | 629370 | 0.0083 / 0.0483 / 121.7221 | 0.0083 / 0.0483 / 121.7221 |
| physical | 51021 | 0.641 / 7.4334 / 3750.4339 | 0.641 / 7.4334 / 3750.4339 |
| prepare | 17010 | 0.0264 / 1.2667 / 604.8025 | 0.0264 / 1.2667 / 604.8025 |
| PhysicalPedestrianCrosswalk.OnTriggerEnter | 769 | 0.0 / 0.0319 / 3.2114 | 0.0 / 0.0319 / 3.2114 |
| PhysicalPedestrianCrosswalk.OnTriggerExit | 465 | 0.0 / 0.017 / 1.6812 | 0.0 / 0.017 / 1.6812 |
| updatecity | 17010 | 0.2774 / 20.3614 / 2530.6538 | 2.4411 / 32.7177 / 10074.0196 |

Spike retention: union 1 counted / 1 listed; PC phase recorder 1 counted / 1 spike rows; 0 union rows lack retained phase attribution. Rig gap 3 spike-row cap fix was not installed. The tested Core/ModFrameRecorder.cs has SpikeCapacity=64; rows beyond that cap can be absent. The rig union list has its own separately reported count; a 256-row phase capacity is not assumed. Missing rows/fields below are explicit.

PC phase count/list truncation: none observed in this window. GC-pause row retention is reported by its count/listed/truncated fields below.

| unity_frame | union ms | dominant | phase / ms | detail / ms | sub / ms | gc_phase | collections | whole-frame ms |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 44174 | 32.7752 | updatecity | mod_closures / 11.163 | physical_spawn_bake / 0.444 | mod_closures_network / 10.944 | none | 0 | 64.2971 |

Whole-frame exports are separate: perf p95/max 39.5496/579.1862 ms; 16629 >=30 ms whole frames, 256 rows listed, truncated flag=False. Count/list differences mean later whole-frame rows are not all exported even when that flag is false; these are not mod-work spikes.

coverage.complete=True; validity=`{"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}`; save_activity=`{"attempts":17038,"allowed":0,"refused":17038,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}`.

`gc_pauses` from modwork end (unity_frame keyed): `{"format":"agent-rig-gc-pauses/1","frame_id":"UnityEngine.Time.frameCount","collection_source":"System.GC.CollectionCount","duration_source":"Unity.Profiling.ProfilerRecorder:GC.Collect","duration_unavailable_reason":null,"count":10,"listed":10,"truncated":false,"total_duration_ms":180.0405,"max_duration_ms":35.3608,"pauses":[{"unity_frame":46334,"duration_ms":3.0048,"collections":0,"generation":null},{"unity_frame":46369,"duration_ms":33.5925,"collections":1,"generation":0},{"unity_frame":49549,"duration_ms":2.8456,"collections":0,"generation":null},{"unity_frame":49607,"duration_ms":33.0618,"collections":1,"generation":0},{"unity_frame":52818,"duration_ms":3.0098,"collections":0,"generation":null},{"unity_frame":52847,"duration_ms":32.005,"collections":1,"generation":0},{"unity_frame":56057,"duration_ms":3.0216,"collections":0,"generation":null},{"unity_frame":56119,"duration_ms":35.3608,"collections":1,"generation":0},{"unity_frame":59363,"duration_ms":2.894,"collections":0,"generation":null},{"unity_frame":59383,"duration_ms":31.2446,"collections":1,"generation":0}]}`.

`gc_pauses` from perf end (unity_frame keyed): `{"format":"agent-rig-gc-pauses/1","frame_id":"UnityEngine.Time.frameCount","collection_source":"System.GC.CollectionCount","duration_source":"Unity.Profiling.ProfilerRecorder:GC.Collect","duration_unavailable_reason":null,"count":11,"listed":11,"truncated":false,"total_duration_ms":228.7571,"max_duration_ms":48.7166,"pauses":[{"unity_frame":44165,"duration_ms":48.7166,"collections":1,"generation":0},{"unity_frame":46334,"duration_ms":3.0048,"collections":0,"generation":null},{"unity_frame":46369,"duration_ms":33.5925,"collections":1,"generation":0},{"unity_frame":49549,"duration_ms":2.8456,"collections":0,"generation":null},{"unity_frame":49607,"duration_ms":33.0618,"collections":1,"generation":0},{"unity_frame":52818,"duration_ms":3.0098,"collections":0,"generation":null},{"unity_frame":52847,"duration_ms":32.005,"collections":1,"generation":0},{"unity_frame":56057,"duration_ms":3.0216,"collections":0,"generation":null},{"unity_frame":56119,"duration_ms":35.3608,"collections":1,"generation":0},{"unity_frame":59363,"duration_ms":2.894,"collections":0,"generation":null},{"unity_frame":59383,"duration_ms":31.2446,"collections":1,"generation":0}]}`.

**street: every component, exclusive / inclusive.** Actual UpdateCity key: `components.updatecity`; inclusive p95 uses `components.updatecity.inclusive.p95_ms`.

| Component | calls | exclusive p95 / max / total ms | inclusive p95 / max / total ms |
| --- | --- | --- | --- |
| PopulationRuntime.LateUpdate | 4755 | 0.0262 / 0.1761 / 94.3592 | 0.0262 / 0.1761 / 94.3592 |
| PopulationRuntime.Update | 4755 | 0.0087 / 4.699 / 38.4017 | 3.7645 / 43.9712 / 7334.8366 |
| AmbientPedestrians.EndCyclePrefix | 1 | 0.0 / 2.0413 / 2.0413 | 0.0 / 2.0413 / 2.0413 |
| AmbientPedestrians.SetActivePrefix | 2 | 0.0 / 0.0111 / 0.02 | 0.0 / 0.0111 / 0.02 |
| NativeCityClock.AccountingPrefix | 4755 | 0.0156 / 0.0535 / 63.1242 | 0.0156 / 0.0535 / 63.1242 |
| NativeCityClock.RidePostfix | 4755 | 0.0007 / 0.0042 / 2.5274 | 0.0007 / 0.0042 / 2.5274 |
| NativeCityClock.RidePrefix | 4755 | 0.0007 / 0.005 / 2.6531 | 0.0007 / 0.005 / 2.6531 |
| NativeCityClock.TickPrefix | 4755 | 0.0012 / 0.0121 / 4.874 | 0.0012 / 0.0121 / 4.874 |
| NativeFoodOwnership.AfterNativeTick | 4755 | 0.0009 / 0.0241 / 3.5628 | 0.0009 / 0.0241 / 3.5628 |
| NativeTravelPace.ObserveTickPostfix | 4755 | 0.0017 / 0.0362 / 6.8198 | 0.0017 / 0.0362 / 6.8198 |
| ResidentInspector.CameraInputPostfix | 9510 | 0.0009 / 0.0112 / 3.1365 | 0.0009 / 0.0112 / 3.1365 |
| ResidentInspector.MouseRunPrefix | 4755 | 0.0018 / 0.0538 / 7.3294 | 0.0018 / 0.0538 / 7.3294 |
| ResidentInspector.UiRaycastPrefix | 175935 | 0.0086 / 0.1101 / 35.3507 | 0.0086 / 0.1101 / 35.3507 |
| PhysicalPedestrianCrosswalk.OnTriggerEnter | 805 | 0.0049 / 0.0495 / 4.4848 | 0.0049 / 0.0495 / 4.4848 |
| PhysicalPedestrianCrosswalk.OnTriggerExit | 479 | 0.002 / 0.0716 / 2.3155 | 0.002 / 0.0716 / 2.3155 |
| updatecity | 4755 | 3.7571 / 43.9572 / 7296.4349 | 3.7571 / 43.9572 / 7296.4349 |

Spike retention: union 1 counted / 1 listed; PC phase recorder 1 counted / 1 spike rows; 0 union rows lack retained phase attribution. Rig gap 3 spike-row cap fix was not installed. The tested Core/ModFrameRecorder.cs has SpikeCapacity=64; rows beyond that cap can be absent. The rig union list has its own separately reported count; a 256-row phase capacity is not assumed. Missing rows/fields below are explicit.

PC phase count/list truncation: none observed in this window. GC-pause row retention is reported by its count/listed/truncated fields below.

| unity_frame | union ms | dominant | phase / ms | detail / ms | sub / ms | gc_phase | collections | whole-frame ms |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 67921 | 44.0253 | updatecity | advance_events / 17.051 | physical_spawn_bake / 0.441 | advance_events_food / 17.049 | none | 0 | 99.2734 |

Whole-frame exports are separate: perf p95/max 202.1755/1313.275 ms; 4741 >=30 ms whole frames, 256 rows listed, truncated flag=False. Count/list differences mean later whole-frame rows are not all exported even when that flag is false; these are not mod-work spikes.

coverage.complete=True; validity=`{"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}`; save_activity=`{"attempts":4770,"allowed":0,"refused":4770,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}`.

`gc_pauses` from modwork end (unity_frame keyed): `{"format":"agent-rig-gc-pauses/1","frame_id":"UnityEngine.Time.frameCount","collection_source":"System.GC.CollectionCount","duration_source":"Unity.Profiling.ProfilerRecorder:GC.Collect","duration_unavailable_reason":null,"count":5,"listed":5,"truncated":false,"total_duration_ms":106.8078,"max_duration_ms":35.6891,"pauses":[{"unity_frame":67980,"duration_ms":35.6891,"collections":1,"generation":0},{"unity_frame":70195,"duration_ms":2.9989,"collections":0,"generation":null},{"unity_frame":70226,"duration_ms":32.5404,"collections":1,"generation":0},{"unity_frame":71422,"duration_ms":3.016,"collections":0,"generation":null},{"unity_frame":71480,"duration_ms":32.5635,"collections":1,"generation":0}]}`.

`gc_pauses` from perf end (unity_frame keyed): `{"format":"agent-rig-gc-pauses/1","frame_id":"UnityEngine.Time.frameCount","collection_source":"System.GC.CollectionCount","duration_source":"Unity.Profiling.ProfilerRecorder:GC.Collect","duration_unavailable_reason":null,"count":7,"listed":7,"truncated":false,"total_duration_ms":112.8419,"max_duration_ms":35.6891,"pauses":[{"unity_frame":67916,"duration_ms":3.0134,"collections":0,"generation":null},{"unity_frame":67980,"duration_ms":35.6891,"collections":1,"generation":0},{"unity_frame":70195,"duration_ms":2.9989,"collections":0,"generation":null},{"unity_frame":70226,"duration_ms":32.5404,"collections":1,"generation":0},{"unity_frame":71422,"duration_ms":3.016,"collections":0,"generation":null},{"unity_frame":71480,"duration_ms":32.5635,"collections":1,"generation":0},{"unity_frame":72671,"duration_ms":3.0207,"collections":0,"generation":null}]}`.

Clock retime lines from measured headless process (window membership follows command times/raw before-and-after logs):

```text
[Mod:PersistentCity] Clock retime: body release 0.0 ms in the previous update, journey retime 125.8 ms, simulation rebuild 47.6 ms.
[Mod:PersistentCity] Clock retime: body release 0.0 ms in the previous update, journey retime 124.5 ms, simulation rebuild 41.5 ms.
[Mod:PersistentCity] Clock retime: body release 519.7 ms in the previous update, journey retime 249.2 ms, simulation rebuild 30.0 ms.
[Mod:PersistentCity] Clock retime: body release 87.8 ms in the previous update, journey retime 171.9 ms, simulation rebuild 40.0 ms.
[Mod:PersistentCity] Clock retime: body release 26.9 ms in the previous update, journey retime 174.3 ms, simulation rebuild 95.0 ms.
[Mod:PersistentCity] Clock retime: body release 34.2 ms in the previous update, journey retime 82.7 ms, simulation rebuild 26.6 ms.
[Mod:PersistentCity] Clock retime: body release 56.3 ms in the previous update, journey retime 100.6 ms, simulation rebuild 36.8 ms.
```

Street Clock retime lines between frames-start/stop log markers: none. The complete preparation/timing retime lines above are retained separately; no unkeyed log line is assigned an invented Unity frame.

Street `physical_parked` tagged >=30 ms PC frames: none retained. Dominant phase rows alone do not establish the absence of this scope from every spike; the global zero-spike gate is assessed above.

Street `physical_release` tagged >=30 ms PC frames: none retained. Dominant phase rows alone do not establish the absence of this scope from every spike; the global zero-spike gate is assessed above.

Street `mod_routes_rebuild` tagged >=30 ms PC frames: none retained. Dominant phase rows alone do not establish the absence of this scope from every spike; the global zero-spike gate is assessed above.

### Shared-host evidence at every window boundary

**gc-day start 2026-11-15T11:33:06Z — PROVISIONAL (Mac shared).**

```text
12:33  up 1 day, 18:11, 1 user, load averages: 13.78 12.54 10.64
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 75%
  PID  %CPU COMM
49418  84.4 suggestd
50220  48.1 com.apple.Virtualization.VirtualMachine
50212  31.1 Big Ambitions Agent Tests Mac
59297  29.7 node
  444  11.1 coreaudiod
59268  10.4 node
52261   4.9 Python
```

Session statuses at this boundary: `20261115-110851-mac-fix-gr5-retime-appearance-planning` phase=review, finished=None; `20261115-115959-ux-insurgo-history-1970` phase=implement, finished=None; `20261115-121910-rig-player-car` phase=review, finished=None. This rig session/build: candidate `564020b`, agentmac headless PID 50212.

Live build/process worktrees by PID: `{"54999":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning"],"55001":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning"],"55075":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning"],"55078":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning"],"55114":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning"],"55253":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning"],"55800":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"55908":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"55914":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"55920":["~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car"],"55922":["~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car"],"55981":["~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car"],"55987":["~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car"],"56010":["~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car"],"56166":["~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car"],"59268":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"]}`. Full process snapshots are in raw evidence.

**gc-day end 2026-11-15T11:43:09Z — PROVISIONAL (Mac shared).**

```text
12:43  up 1 day, 18:21, 1 user, load averages: 8.79 9.54 9.70
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 77%
  PID  %CPU COMM
50212  87.8 Big Ambitions Agent Tests Mac
49418  78.8 suggestd
63895  66.2 dotnet
63896  63.2 dotnet
63888  49.4 dotnet
63898  11.1 taskgated-helper
  444   9.0 coreaudiod
```

Session statuses at this boundary: `20261115-110851-mac-fix-gr5-retime-appearance-planning` phase=fix, finished=None; `20261115-115959-ux-insurgo-history-1970` phase=review, finished=None; `20261115-121910-rig-player-car` phase=fix, finished=None. This rig session/build: candidate `564020b`, agentmac headless PID 50212.

Live build/process worktrees by PID: `{"63741":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"63742":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"63799":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"63800":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"63806":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"63888":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/Compatibility"],"63895":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/Persistence"],"63896":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/Core"]}`. Full process snapshots are in raw evidence.

**gc-night start 2026-11-15T11:46:55Z — PROVISIONAL (Mac shared).**

```text
12:46  up 1 day, 18:25, 1 user, load averages: 5.82 8.00 9.01
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 72%
  PID  %CPU COMM
63952 289.8 dotnet
49418  81.3 suggestd
50212  54.7 Big Ambitions Agent Tests Mac
64859  22.4 mdworker_shared
  444   9.1 coreaudiod
63508   8.3 claude
52261   4.3 Python
```

Session statuses at this boundary: `20261115-110851-mac-fix-gr5-retime-appearance-planning` phase=fix, finished=None; `20261115-115959-ux-insurgo-history-1970` phase=review, finished=None; `20261115-121910-rig-player-car` phase=fix, finished=None. This rig session/build: candidate `564020b`, agentmac headless PID 50212.

Live build/process worktrees by PID: `{"63888":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/Compatibility"],"63895":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/tests"],"63896":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/Core"],"63951":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/tests"],"63952":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/tests/bin/Debug/net10.0"],"64008":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"64011":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"64071":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"64075":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"64131":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"64209":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"]}`. Full process snapshots are in raw evidence.

**gc-night end 2026-11-15T11:56:58Z — PROVISIONAL (Mac shared).**

```text
12:56  up 1 day, 18:35, 1 user, load averages: 5.87 6.33 7.48
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 66%
  PID  %CPU COMM
66120 294.5 dotnet
50212  91.3 Big Ambitions Agent Tests Mac
49418  81.1 suggestd
  444  10.5 coreaudiod
  735   2.2 Resolve
  937   2.1 io.tailscale.ipn.macsys.network-extension
  654   1.6 com.apple.DriverKit-AppleBCMWLAN
```

Session statuses at this boundary: `20261115-110851-mac-fix-gr5-retime-appearance-planning` phase=local_tests, finished=None; `20261115-115959-ux-insurgo-history-1970` phase=fix, finished=None; `20261115-121910-rig-player-car` phase=review, finished=None. This rig session/build: candidate `564020b`, agentmac headless PID 50212.

Live build/process worktrees by PID: `{"66089":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/Compatibility"],"66102":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/tests"],"66103":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/Core"],"66119":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/tests"],"66120":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/tests/bin/Debug/net10.0"],"66752":["~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car"],"66753":["~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car"],"66818":["~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car"],"66824":["~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car"],"66879":["~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car"],"66932":["~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car"]}`. Full process snapshots are in raw evidence.

**street start 2026-11-15T12:00:55Z — PROVISIONAL (Mac shared).**

```text
13:00  up 1 day, 18:39, 1 user, load averages: 3.78 5.29 6.77
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 69%
  PID  %CPU COMM
66120 100.0 dotnet
49418  91.5 suggestd
50212  35.8 Big Ambitions Agent Tests Mac
  444  11.3 coreaudiod
52261   4.5 Python
  735   2.2 Resolve
  915   0.7 sysmond
```

Session statuses at this boundary: `20261115-110851-mac-fix-gr5-retime-appearance-planning` phase=local_tests, finished=None; `20261115-115959-ux-insurgo-history-1970` phase=fix, finished=None; `20261115-121910-rig-player-car` phase=review, finished=None. This rig session/build: candidate `564020b`, agentmac headless PID 50212.

Live build/process worktrees by PID: `{"66089":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/Compatibility"],"66102":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/tests"],"66103":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/Core"],"66119":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/tests"],"66120":["~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/tests/bin/Debug/net10.0"],"66752":["~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car"],"66753":["~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car"],"66818":["~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car"],"66824":["~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car"],"66879":["~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car"],"66932":["~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car"]}`. Full process snapshots are in raw evidence.

**street end 2026-11-15T12:10:58Z — PROVISIONAL (Mac shared).**

```text
13:10  up 1 day, 18:49, 1 user, load averages: 7.67 6.46 6.58
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 59%
  PID  %CPU COMM
50220 180.2 com.apple.Virtualization.VirtualMachine
50212 105.5 Big Ambitions Agent Tests Mac
76914  84.4 node
49418  53.4 suggestd
50209  40.5 limactl
76909  36.3 node
50359  21.7 ssh.sock [mux]
```

Session statuses at this boundary: `20261115-110851-mac-fix-gr5-retime-appearance-planning` phase=done, finished=2026-11-15T13:07:40+01:00; `20261115-115959-ux-insurgo-history-1970` phase=fix, finished=None. This rig session/build: candidate `564020b`, agentmac headless PID 50212.

Live build/process worktrees by PID: `{"74723":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"75418":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"75481":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"76308":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"76309":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"76310":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"76311":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"76312":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"76313":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"76314":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"76391":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"76897":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"76900":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"76903":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"76909":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"],"76914":["~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970"]}`. Full process snapshots are in raw evidence.

Known co-tenants: thirteenth-fix run `20261115-110851-mac-fix-gr5-retime-appearance-planning`, harness PID 18005, branch `codex/mac-fix-gr5-retime-appearance-planning`, PR #279; `20261115-115959-ux-insurgo-history-1970`, harness PID 26553; additional discovered sessions are listed above. No shared worktree, cancellation, debugging, file/branch modification or messages to those sessions. CPU quietness wait occurred before day start; host-load qualifications apply to every measured window.

**gc-day sessions/builds observed at boundaries and minute samples:**

| Session | phases seen while live |
| --- | --- |
| 20261115-110851-mac-fix-gr5-retime-appearance-planning | fix, review |
| 20261115-115959-ux-insurgo-history-1970 | implement, local_tests, pr, review |
| 20261115-121910-rig-player-car | fix, review |

| Build/process worktree | PIDs seen |
| --- | --- |
| ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning | 54999, 55001, 55075, 55078, 55114, 55253 |
| ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 | 55800, 55908, 55914, 59268, 59580, 59609, 60014, 60047, 60230, 60316, 60323, 60572, 60573, 60574, 60575, 60576, 60577, 60578, 61408, 61719, 61720, 61722, 62103, 62104, 62105, 62106, 62107, 62108, 62393, 62395, 62606, 63087, 63156, 63159, 63166, 63741, 63742, 63799, 63800, 63806 |
| ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car | 55920, 55922, 55981, 55987, 56010, 56166 |
| ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/Compatibility | 60567, 62330, 63144, 63453, 63888 |
| ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/Persistence | 63895 |
| ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/Core | 60611, 62338, 63153, 63469, 63896 |
| ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/tests | 60594, 62337, 62350, 63152, 63161, 63468, 63482 |
| ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/tests/bin/Debug/net10.0 | 62352, 63162, 63483 |

**gc-night sessions/builds observed at boundaries and minute samples:**

| Session | phases seen while live |
| --- | --- |
| 20261115-110851-mac-fix-gr5-retime-appearance-planning | fix, local_tests |
| 20261115-115959-ux-insurgo-history-1970 | fix, review |
| 20261115-121910-rig-player-car | ci, fix, push, review |

| Build/process worktree | PIDs seen |
| --- | --- |
| ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/Compatibility | 63888, 66089 |
| ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/tests | 63895, 63951, 66102, 66119 |
| ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/Core | 63896, 66103 |
| ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/tests/bin/Debug/net10.0 | 63952, 66120 |
| ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 | 64008, 64011, 64071, 64075, 64131, 64209, 65219 |
| ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car | 66752, 66753, 66818, 66824, 66879, 66932 |

**street sessions/builds observed at boundaries and minute samples:**

| Session | phases seen while live |
| --- | --- |
| 20261115-110851-mac-fix-gr5-retime-appearance-planning | local_tests, review |
| 20261115-115959-ux-insurgo-history-1970 | fix |
| 20261115-121910-rig-player-car | ci, fix, review |

| Build/process worktree | PIDs seen |
| --- | --- |
| ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/Compatibility | 66089, 68291 |
| ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/tests | 66102, 66119, 68292, 68299, 68549, 68550, 68666, 68671, 69504, 69513 |
| ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/Core | 66103, 68293 |
| ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning/tests/bin/Debug/net10.0 | 66120, 68300, 68551, 68672, 69534 |
| ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car | 66752, 66753, 66818, 66824, 66879, 66932, 70195, 70198, 70257, 70260, 70316, 70377 |
| ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 | 68208, 68230, 68703, 69020, 69068, 69071, 69077, 69343, 69392, 69395, 69401, 70086, 70115, 74723, 75418, 75481, 76308, 76309, 76310, 76311, 76312, 76313, 76314, 76391, 76897, 76900, 76903, 76909, 76914 |
| ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-retime-appearance-planning | 67735, 67739, 67803, 67805, 67838, 68033 |
| ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-player-car/rig-ops/tests | 69081 |

### #277 check 4 look stability

**PARTIAL.** Graphics process PID 77500, same candidate DLL, background --rig-test outside every timing window. Five distinct native walker handles were followed, player/camera moved from x=215,z=0 to x=317.531,z=-122.201805 (159.5 m, beyond the configured 120 m body radius), waited 8 s, returned and waited 5 s. This does not establish five distinct persistent residents or actual body release/reacquire: the installed ops expose native handles, not resident/body links.

Return durations for cycles 0–4: 19.638 / 18.153 / 18.536 / 18.257 / 18.971 s (exact controller values remain in look.log). Handles 0–2 remained readable on return; 3–4 returned ops_view_stale_target for screen_pos get and camera follow. Changed speed 1→2, waited 10 s, restored speed 1, waited 5 s, then bounded reload completed in 99.3 s. No ops_load_* failure or cold recovery. All five old handles returned ops_view_stale_target after reload; rediscovery found 40 fresh native handles, with no persistent identity link to the earlier five.

Twelve 1600×900 captures inspected. Pair 0 has a cropped dressed blond/red-and-black figure before, then building obstruction. Pair 1 shows consistent grey/light top, magenta skirt, dark leggings and light shoes before/after, without proof of body release. Pair 2 is obstructed by building geometry in both images. Pair 3 shows dressed figures but the selected handle became stale; the image cannot establish identity. Pair 4 loses the selected red-shirted figure after return and leaves the purple-shirted player. The player is not counted as a resident. After-reload wide capture shows the player and curbside cars. No undressed body was observed; sparse/obstructed stills do not prove every required resident stayed dressed or exclude brief flashes.

Curbside cars are visible; some appear raised or overlapping in the initial/close views. Saved-curb identities and assignments are not exported, so the literal saved-curb gate is NOT RUN / tooling gap. No new regression is established by these stills. Five-resident same-look continuity through actual release/reacquire, the retime and reload remains NOT RUN / tooling gap. No weaker pass criterion is substituted.

Zero instances of each requested error string in each complete retained process log:

| Process log | Appearance meshes unavailable | Empty appearance | Merged body |
| --- | --- | --- | --- |
| Player-C0.log | 0 | 0 | 0 |
| Player-A-final.log | 0 | 0 | 0 |
| Player-B.log | 0 | 0 | 0 |

Graphics Clock retime lines:

```text
[Mod:PersistentCity] Clock retime: body release 0.0 ms in the previous update, journey retime 139.4 ms, simulation rebuild 56.5 ms.
[Mod:PersistentCity] Clock retime: body release 0.0 ms in the previous update, journey retime 128.3 ms, simulation rebuild 35.5 ms.
[Mod:PersistentCity] Clock retime: body release 188.6 ms in the previous update, journey retime 244.2 ms, simulation rebuild 60.3 ms.
[Mod:PersistentCity] Clock retime: body release 0.0 ms in the previous update, journey retime 90.1 ms, simulation rebuild 30.1 ms.
```

Exact invoked look op failures (successful schema/capability read is retained as B-ops.json):

- `python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "walker", "id": "walker-ac1813db8c4f452290acf1fd6a211b22"}}'` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "ed1c87b202334845a7852f2e940cad1b",     "session": "671012e72e154ea6bebb9b84a2aca8b8",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op camera '{"action": "follow", "target": {"kind": "walker", "id": "walker-ac1813db8c4f452290acf1fd6a211b22"}, "offset": {"x": 0, "y": 4, "z": -6}, "seconds": 10}'` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "a90ecf71e7ca4b51a30d48d02eedcb07",     "session": "671012e72e154ea6bebb9b84a2aca8b8",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "walker", "id": "walker-0b80f7c6008a480287fa99dd99fa16bb"}}'` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "ddd4f2a433154d1faa53ec4c160b95b7",     "session": "671012e72e154ea6bebb9b84a2aca8b8",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op camera '{"action": "follow", "target": {"kind": "walker", "id": "walker-0b80f7c6008a480287fa99dd99fa16bb"}, "offset": {"x": 0, "y": 4, "z": -6}, "seconds": 10}'` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "409e1c5e89914e918b3d87c3ac77b031",     "session": "671012e72e154ea6bebb9b84a2aca8b8",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "walker", "id": "walker-55199280320b48e8950c46a53a896010"}}'` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "a75f521854bd4307a7a8b446fdc6011f",     "session": "671012e72e154ea6bebb9b84a2aca8b8",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "walker", "id": "walker-dbebe6318151490b8f9b5270c7f6caed"}}'` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "57940211c65e40e599d25e75a524cc20",     "session": "671012e72e154ea6bebb9b84a2aca8b8",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "walker", "id": "walker-08ae2731b32d4433b30f34cd235ca1a1"}}'` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "0cd17c5eec68417cbb04b9f76176de10",     "session": "671012e72e154ea6bebb9b84a2aca8b8",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "walker", "id": "walker-ac1813db8c4f452290acf1fd6a211b22"}}'` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "f41c92b4f00844d394b2fe6eb8da1c5c",     "session": "671012e72e154ea6bebb9b84a2aca8b8",     "error": "ops_view_stale_target"   } }`.
- `python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "walker", "id": "walker-0b80f7c6008a480287fa99dd99fa16bb"}}'` → `{   "error": "ops_view_stale_target",   "reply": {     "ok": false,     "id": "32322249d0394d7685c4e398c8542966",     "session": "671012e72e154ea6bebb9b84a2aca8b8",     "error": "ops_view_stale_target"   } }`.


### Findings, exact errors and NOT RUN

`building read` for the collider-free unreadable subject returned `ops_building_bounds_unavailable` both before and after reload; this is the expected closed failure, not a missing-role failure. All five roles exist. No `ops_building_fixture_reload_incomplete` occurred.

Preparation command refusals, recovered before D: `new_game {"name":"FT RIGTMP-GR5-15d-mac"}` from the loaded C0 city returned `new_game works from the main menu only; the game is at the city now.` The attempted bridge `menu {}` returned `Unknown command 'menu'.` with the command list. Stopped/restarted only the owned player and created the new city from the menu. A local controller initially dereferenced a null health.load while new_game was starting (`AttributeError: 'NoneType' object has no attribute 'get'`); resumed health polling in the same player, without repeating new_game. These are retained in raw logs.

- GR-5 60 fps street/busy-shop arms: NOT RUN, Windows-only; Mac about 5–8 fps at 1,000 residents. Peter is gaming on the PC; no PC work or contact.
- GR-5 save latency: NOT RUN, owner decision pending; no save profiling or save-latency measurement.
- #262 checks 4–5 / #254-4 look identity: NOT RUN, resident-reacquire op missing from installed rig capabilities.
- All other checks outside C0, D and #277 1–4: NOT RUN, outside this run. Literal resident identity/release/reacquire/reload and saved-curb proof within #277-4 remain tooling gaps, detailed above.

### Cleanup and validation

Fixture reverted before timing; disposable_save confirms both target disposables absent; no fixture sidecar remains. Owned agentmac game stopped normally. Printed stage rollback restored the complete PersistentCity folder and install.json; own pre-stage manifest verified exact hashes. Isolated preferences, persistent-city.json, autosave and frame-cap configuration restored, including absence where originally absent. RLO manifest unchanged. AgentRigOps remains at #93 hash. Lease released; no process started by this run remains running.

Restore verification: `{"original_files_verified":29,"original_absent_paths_verified":3,"rlo_files_verified":53,"mismatches":[],"agent_rig_ops_sha256":"aa3481a680defffca4290069a29a0eb53332ff97ac95e5f9e94fb17aab13726e","install_json_sha256":"31566114100270354118e29cefa63871db1b9ee358d2abf161724d877c630166","fixture_sidecars":0,"disposables_absent":["FT RIGTMP-GR5-15c","FT RIGTMP-GR5-15c-mac","FT RIGTMP-GR5-15d-mac"],"sealed_fixture_hash_match":true,"lease_released":true,"started_game_processes_gone":[49403,50212,77500],"caffeinate_processes_gone":true}`.

Offline verification: build passed; docs link and markdown-ban checks run for the results diff. No production code changes, no full test suite or regression suite. Original checkout unchanged. Build/evidence worktree removed after evidence commit and PR creation. No merge, release, Workshop, version/changelog, schema or weight change.

Refs #246 #277

## Rig results — 15 November 2026 e, Mac (planned 16b relaunch; #279 checks 1-4, #246 check 4 re-baseline at cbd4e68e)

**RESULT: DONE. 3 passed / 1 partial / 4 failed / 4 NOT RUN groups.** Counted checks: A1–A4, D4–D5, leftover cleanup and E27 when measured. All timing/GC/street numbers are **PROVISIONAL (shared Mac)**. The unresolved native proof gates apply to product changes. This docs-only results PR merges after current-head reviewer approval.

Role `rig_test`, where `mac`, isolated `agentmac`, run `persistent-city-rig-2026-11-15f-mac`. `<stage>` = `~/HART-Builds/persistent-city-rig-2026-11-15f-mac`. Order: A day, night, street, look; D fresh re-baseline; E diagnostic reads. Raw logs, replies, controllers and backups remain outside Git in `<stage>`. Report copy: `~/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-15f-mac.md`. MB is decimal.

The prior 15e attempt (#281, superseded) stopped before game start because its stage invocation omitted BA_LOCK_OWNER. This relaunch prefixed every agent-rig command with `export BA_LOCK_OWNER=persistent-city-rig-2026-11-15f-mac;`, echoed the owner before acquiring and staging, and staged successfully on its first attempt.

MarkdownBanTests and tools/md-ban.py ban new Markdown paths without the owner-applied docs-approved label. As requested, results are appended here and no new docs/checks report is created. No historical 15d JSON is overwritten; full GC JSON stays in the stage, with hashes and complete segment/spike excerpts below.

### Build and rig

| Item | Evidence |
| --- | --- |
| Candidate | `cbd4e68e58dee03272d23612cee23b52d5ebcda8`; initial fetch matched the requested origin/codex/persistent-city tip. Includes #279 merge 1cbcc130 plus #280 evidence; fourteenth fix absent. Final fetched tip: cbd4e68e58dee03272d23612cee23b52d5ebcda8. Tested build remains pinned. |
| Worktree / branch | `rig-2026-11-15f-mac-evidence` / `codex/mac-rig-2026-11-15f`; fresh isolated build/evidence worktree. Original checkout untouched. |
| agent-rig HEAD | Initial `62106d35be6735fab3e126d3905a5de069068145` (#94/#95 including #93/#92); clean, fetch and ff-only pull already current. Final checkout HEAD `14f75ce5409766272831f9f507e50492991295f3` moved during the run; installed AgentRigOps hash stayed unchanged. This run made no checkout edits or overlay update. |
| Live AgentRigOps SHA-256 | `dc621edf1136d8111ae3bc821d0e7f17903df2f03281b1e7fb1c16dd5a0e589d` before launch and after cleanup. |
| PersistentCity DLL SHA-256 | `2d4986e9e59ce067b0901ede6aa4e39553598eca395e77971a0a526d51864fa7`; build, package, stage and Player.log agree. |
| PersistentCity loaded build line | No standalone commit-stamped PC build line is emitted. The exact staged_mod version/informational/hash line and runtime activation excerpts below identify the loaded candidate; no commit stamp is invented. |
| Build | `PATH=/opt/homebrew/bin:$PATH`, Homebrew DOTNET_ROOT; `python3 tools/build.py --game-managed "~/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed"`, exit 0. Package has no RLO or game DLLs. |
| Staging | `export BA_LOCK_OWNER=persistent-city-rig-2026-11-15f-mac; python3 scripts/install-mac.py --stage-mod PersistentCity ~/HART-Builds/persistent-city-rig-2026-11-15f-mac/package`; stopped and leased. Receipt backup `~/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261115T125937Z`; 28 entries refreshed. |
| Player | Headless rig-test A PID 98723; D PID 21166. No graphics process. Starts/reloads recorded in commands.jsonl. |
| RLO | Player.log: Real Life Overhaul 1.0.6 initialised. Options living_population stored absent/live true, social_media 1/true, social_media_notices 0/false; final reads agree. No RLO file or option changes. |
| Autosave / SaveCost | Original native autosave 5 minutes / 3 saves, prevent_autosave=false. #70 process suppression verified after reloads and before/after every measured window. SaveCostDiagnostics requested=false, writerSchema=1, native save profiler installed=false. Only D disposable saved outside timing; no save latency measured. |
| Fixture / population | `FT GR5 13f Timing v1`: 1,000 residents / 2,000 visitors / 1,683 places, person target off; no fallback. D: `FT RIGTMP-GR5-15f-mac`, population evidence below. |
| Pre-stage backup | Own pre-stage-manifest.json SHA-256 `2f2a95e15ac4b3dace3a04442209692ab7d12f368577ecc0062a9b9b5c8f2a26`; PersistentCity, preferences, config presence/absence and install receipt backed up; complete RLO file manifest retained. |
| Stream guard | `harness stream-guard status`: idle, obs64.exe not running; Mac run only. No Windows PC contact. |

Stage and live Player.log identity:

```text
[AgentRigOps] staged_mod name=PersistentCity version=absent informational=0.10.0-persistent-city-candidate sha256=2d4986e9e59ce067b0901ede6aa4e39553598eca395e77971a0a526d51864fa7
```

Printed rollback, verbatim except the personal home prefix is replaced by `~` (expand before execution); used stopped and owned:

```text
rm -rf "~/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "~/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261115T125937Z/mod" "~/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "~/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261115T125937Z/install.json" "~/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

`[Mod:RealLifeOverhaul] Real Life Overhaul 1.0.6 initialised.`
`[Mod:PersistentCity] Persistent population active: 1000 residents, 2000 visitors, 1683 places. Native-pace walking, subway and car journeys; physical population preflight follows road initialization. Ctrl+F8 opens food business feedback.`

### #279 Expected requirements and outcomes

The #279 body has no separately formatted numbered Expected lines. Its literal In-game checks requirements and TARGETS are retained here; no substitute acceptance criteria are introduced.

```text
TARGETS: gen0 about 5 or fewer and pause p95 under ~36 ms day and night; street UpdateCity / mod-work p95 under 5 ms with no >=30 ms mod-work frame (in particular no retime/rebuild frame); idle ticks stay 0 B; no visual or behaviour change (same simulation results and routes, dressed bodies, same look per resident, none of the "Appearance meshes unavailable" / "Empty appearance" / "Merged body ..." log strings).
```

```text
In-game checks go in the PR body under "In-game checks", marking locked-PC vs input-only, and must include: (1) day 15:30 and (2) night 01:00 600 s gc windows on fixture FT GR5 13f Timing v1 at 1,000 residents / 2,000 visitors reporting gen0, pause p95, heap, mod, physical, advance and every segment and sub-segment vs the latest rig report; (3) a 600 s street window with pans and at least one clock-rate change reporting UpdateCity / mod-work p95 and every >=30 ms spike with phase, detail, sub and gc_phase (none from retime/mod_routes_rebuild, physical_spawn_appearance, mod_rlo); (4) a look-stability check (dressed bodies, same look across release/reacquire, none of the error strings).
```

**gc-day: FAIL, PROVISIONAL.** 600 s hold; gen0 7 (<=5: not met), sampler pause p95 31.829 ms (<~36: met). Complete requested totals, segments and retained spikes follow.

**gc-night: FAIL, PROVISIONAL.** 600 s hold; gen0 7 (<=5: not met), sampler pause p95 100.850 ms (<~36: not met). Complete requested totals, segments and retained spikes follow.

**Street: FAIL, PROVISIONAL.** Actual component key `components.updatecity`: self/inclusive p95 4.0914/4.0914 ms; union p95 4.1405 ms. Strict <5 ms met; zero >=30 ms mod-work frames not met (3 counted). Clock rate changed 1→2→1 inside the window; the recorded condition label speed=1 is static metadata, not a live speed readback. The no-retime/rebuild-spike requirement is not met: unity_frame 57969 and 59278 are mod_routes_rebuild, gc_phase=none, collections=0. No appearance/RLO >=30 ms dominant row is observed in the retained records; full rows follow.

**Look: PARTIAL.** The zero-error-string clause is checked below. Five persistent resident identities, dressed-body continuity through actual release/reacquire/retime/reload, and saved-curb assignments remain NOT RUN: installed ops have native walker handles but no resident/body/reacquire binding, and headless rendering is refused. No weaker look pass criterion is used. Idle 0 B and exact simulation/route equivalence are offline claims not established by these moving-city windows.

### GC totals and comparisons (all PROVISIONAL)

Idle camera, player x=215,z=0; day 15:30 and night 01:00 after speed-20 advance; speed 1 within GC windows. boehm_total sampler pause is cycle-ending frame excess, distinct from the Unity GC.Collect profiler marker. Shared-host differences do not establish controlled product regression or improvement.

| Window | frames / dropped | gen0 / 1 / 2 | pause p50 / p95 / max ms | heap start / end / peak / after_gc / Mono bytes | mod / physical / advance MB |
| --- | --- | --- | --- | --- | --- |
| gc-day | 16609 / 0 | 7/0/0 | 27.676/31.829/31.829 | 1222799360/1397968896/1403588608/1230224822/1410007040 | 267.436/64.904/99.256 |
| gc-night | 16413 / 0 | 7/0/0 | 29.71/100.85/100.85 | 1321627648/1309044736/1429716992/1287960283/1445593088 | 129.231/22.449/29.047 |

| day / night | 15a | 15b | 15c shared host artefacts | 15d shared | 15f shared |
| --- | --- | --- | --- | --- | --- |
| mod MB | 295.628 / 151.470 | 177.883 / 78.728 | 272.206 / 66.856 | 139.858 / 75.720 | 267.436 / 129.231 |
| physical MB | 112.167 / 36.201 | 64.263 / 32.291 | 59.968 / 24.582 | 45.710 / 22.489 | 64.904 / 22.449 |
| advance MB | 97.634 / 30.515 | 95.208 / 29.369 | 87.318 / 26.937 | 74.047 / 34.783 | 99.256 / 29.047 |
| gen0 | 8.000 / 6.000 | 7.000 / 6.000 | 6.000 / 4.000 | 6.000 / 5.000 | 7.000 / 7.000 |
| pause p95 ms | 27.888 / 37.301 | 43.403 / 38.929 | 110.570 / 4651.381 | 674.160 / 45.630 | 31.829 / 100.850 |

15d day pause p95 was 674.160 ms in its raw report; its omission from the launch shorthand is not a 36 ms pass. 15d street self/union 3.7571/3.8278 ms with one ~44 ms frame; 15b 6.8007/6.9556 ms with 28 spikes; 15a 6.5466/6.6550 ms with 37; 15c 16.8084/17.2191 ms with 114. None is a clean baseline.

| Street run (PROVISIONAL) | UpdateCity self p95 ms | mod-work union p95 ms | >=30 ms mod-work frames |
| --- | --- | --- | --- |
| 15a | 6.5466 | 6.655 | 37 |
| 15b | 6.8007 | 6.9556 | 28 |
| 15c | 16.8084 | 17.2191 | 114 |
| 15d | 3.7571 | 3.8278 | 1 |
| 15f | 4.0914 | 4.1405 | 3 |

### Every allocation segment and sub-segment

| Segment | 15d day / night bytes | 15f day bytes / samples / skipped | 15f night bytes / samples / skipped |
| --- | --- | --- | --- |
| frame | 802836480 / 806047744 | 1115439104 / not exported / not exported | 877658112 / not exported / not exported |
| mod | 139857600 / 75719880 | 267435784 / 16610 / 0 | 129231464 / 16414 / 0 |
| advance | 74046960 / 34783168 | 99256352 / 16578 / 0 | 29047280 / 16359 / 0 |
| physical | 45710112 / 22488520 | 64903696 / 16594 / 0 | 22449384 / 16376 / 0 |
| physical_ambient | 1966288 / 2186960 | 2127312 / 16578 / 0 | 2105264 / 16359 / 0 |
| physical_vehicles | 877120 / 433344 | 1314288 / 16578 / 0 | 382576 / 16359 / 0 |
| physical_bindings | 11408 / 1520 | 16640 / 511071 / 0 | 11744 / 296870 / 0 |
| physical_vendor_meals | 0 / 656 | 0 / 16578 / 0 | 80 / 16359 / 0 |
| physical_fade | 0 / 32 | 0 / 16578 / 0 | 0 / 16359 / 0 |
| physical_focus | 0 / 2192 | 496 / 16578 / 0 | 112 / 16359 / 0 |
| physical_observe | 388432 / 277744 | 289472 / 494148 / 0 | 99136 / 280309 / 0 |
| physical_release | 5408 / 1584 | 6736 / 360 / 0 | 1648 / 284 / 0 |
| physical_parked | 1797232 / 1605056 | 1896784 / 1206 / 0 | 1621536 / 1193 / 0 |
| physical_index | 80 / 80 | 80 / 2201 / 0 | 80 / 2176 / 0 |
| physical_queues | 9024 / 17456 | 8960 / 2201 / 0 | 17200 / 2176 / 0 |
| physical_roster | 126832 / 29392 | 15024 / 2201 / 0 | 29872 / 2176 / 0 |
| physical_selection | 1289176 / 553864 | 1663264 / 2873 / 0 | 612160 / 2398 / 0 |
| physical_spawn_pedestrian | 2682384 / 63560 | 3669432 / 920 / 0 | 77536 / 294 / 0 |
| physical_spawn_appearance | 33456448 / 14148608 | 50596048 / 963 / 0 | 14403584 / 291 / 0 |
| physical_spawn_check | 0 / 0 | 0 / 642 / 0 | 0 / 194 / 0 |
| physical_spawn_vehicle | 233248 / 41024 | 246496 / 73 / 0 | 49248 / 25 / 0 |
| physical_unattributed | 2867032 / 3125448 | 3052664 / 33174 / 0 | 3037608 / 32737 / 0 |
| physical_spawn_randomize | 1869408 / 845360 | 2866432 / 321 / 0 | 850768 / 97 / 0 |
| physical_spawn_bake | 2127792 / 432 | 2172016 / 321 / 0 | 0 / 97 / 0 |
| advance_planning | 38375672 / 23007504 | 46507952 / 9631 / 0 | 18856688 / 3971 / 0 |
| advance_events | 33463048 / 9324272 | 50360944 / 22307 / 0 | 7832288 / 18927 / 0 |
| advance_stops | 2208240 / 2449712 | 2387456 / 16578 / 0 | 2356896 / 16359 / 0 |
| advance_other | 0 / 1104 | 0 / 16578 / 0 | 1408 / 16359 / 0 |
| physical_spawn_grow | 804608 / 0 | 487040 / 224 / 0 | 0 / 0 / 0 |
| mod_prelude | 2331440 / 2587392 | 2477024 / 16610 / 0 | 2450080 / 16414 / 0 |
| mod_rlo | 3530176 / 3541104 | 3531744 / 16610 / 0 | 3517840 / 16414 / 0 |
| mod_food | 1278384 / 1277760 | 1277184 / 16610 / 0 | 851520 / 16414 / 0 |
| mod_optional_stops | 2232912 / 2454032 | 2392832 / 16610 / 0 | 2370704 / 16414 / 0 |
| mod_routes | 7669584 / 5283984 | 90395528 / 16610 / 0 | 65149616 / 16414 / 0 |
| mod_closures | 388864 / 492544 | 388416 / 16578 / 0 | 674816 / 16359 / 0 |
| mod_hold_replay | 0 / 224 | 0 / 16578 / 0 | 0 / 16359 / 0 |
| mod_physical_setup | 0 / 0 | 0 / 16253 / 0 | 0 / 15626 / 0 |
| mod_inspector | 2296736 / 2447552 | 2432896 / 16578 / 0 | 2355696 / 16359 / 0 |
| mod_summary | 364032 / 362832 | 365584 / 16456 / 0 | 364336 / 16114 / 0 |
| mod_other | 2720 / 0 | 3456 / 16610 / 0 | 144 / 16414 / 0 |
| mod_routes_speeds | 801472 / 1034064 | 915088 / 14279 / 0 | 968320 / 15056 / 0 |
| mod_routes_nodes | 3160720 / 432832 | 3472216 / 14279 / 0 | 3472264 / 15056 / 0 |
| mod_routes_sweep | 0 / 80 | 32 / 16571 / 0 | 256 / 16354 / 0 |
| mod_routes_rebuild | 3706896 / 3816400 | 83875472 / 16610 / 0 | 58653224 / 16414 / 0 |
| mod_closures_network | 229536 / 331776 | 229088 / 16578 / 0 | 511408 / 16359 / 0 |
| mod_closures_links | 159328 / 159936 | 159328 / 16578 / 0 | 159680 / 16359 / 0 |
| advance_events_traffic | 20672 / 46688 | 288496 / 2888 / 0 | 259584 / 1954 / 0 |
| advance_events_food | 20541984 / 3878496 | 32566512 / 1124 / 0 | 3894880 / 320 / 0 |
| advance_events_citizen | 7811560 / 1912032 | 12345536 / 4080 / 0 | 2071696 / 2156 / 0 |
| advance_events_route_begin | 5088832 / 3485280 | 5160336 / 3546 / 0 | 1604560 / 1522 / 0 |
| advance_events_other | 0 / 1776 | 64 / 10669 / 0 | 1568 / 12975 / 0 |
| advance_planning_slice | 24722968 / 17071472 | 33180312 / 6084 / 0 | 12512064 / 2449 / 0 |
| advance_planning_finish | 13652704 / 5936032 | 13327640 / 3547 / 0 | 6344624 / 1522 / 0 |
| physical_setup_places | 273992 / 273992 | 273992 / 1 / 0 | 273992 / 1 / 0 |
| physical_setup_bodies | 180032 / 206128 | 180032 / 1 / 0 | 206128 / 1 / 0 |
| physical_setup_vehicles | 15984 / 15904 | 15984 / 2 / 0 | 15904 / 2 / 0 |
| physical_setup_ambient | 173776 / 174192 | 173776 / 2 / 0 | 173776 / 2 / 0 |
| physical_setup_preflight | 2000 / 752 | 800 / 1 / 0 | 752 / 1 / 0 |
| physical_unattributed_setup | 3568 / 4240 | 3568 / 1 / 0 | 3568 / 1 / 0 |
| physical_unattributed_update | 7568 / 624 | 14144 / 16578 / 0 | 4144 / 16359 / 0 |
| physical_unattributed_waiting | 1872 / 576 | 2672 / 15 / 0 | 2304 / 16 / 0 |
| physical_unattributed_entry | 2208240 / 2448912 | 2387552 / 16578 / 0 | 2357040 / 16359 / 0 |

Child segments overlap parents. These are all exported allocation fields, including sub-segments; they are not additive across the hierarchy.

| Phase / sub-segment | day total ms | night total ms |
| --- | --- | --- |
| advance | 0 | 0 |
| advance_events | 707.441 | 392.178 |
| advance_events_citizen | 254.782 | 170.438 |
| advance_events_food | 184.52 | 49.44 |
| advance_events_other | 36.048 | 45.397 |
| advance_events_route_begin | 193.147 | 81.546 |
| advance_events_traffic | 36.938 | 43.546 |
| advance_other | 127.194 | 101.91 |
| advance_planning | 5510.741 | 3251.935 |
| advance_planning_finish | 928.122 | 561.913 |
| advance_planning_slice | 4581.395 | 2689.128 |
| advance_stops | 44.314 | 71.98 |
| mod | 0 | 0 |
| mod_closures | 1385.647 | 1239.473 |
| mod_closures_links | 1239.851 | 1087.144 |
| mod_closures_network | 120.792 | 115.389 |
| mod_food | 33.846 | 34.748 |
| mod_hold_replay | 5.659 | 5.787 |
| mod_inspector | 101.84 | 118.313 |
| mod_optional_stops | 994.551 | 144.459 |
| mod_other | 10.369 | 10.782 |
| mod_physical_setup | 2.947 | 2.871 |
| mod_prelude | 1450.023 | 1666.395 |
| mod_rlo | 55.449 | 72.909 |
| mod_routes | 778.908 | 1015.522 |
| mod_routes_nodes | 22.157 | 33.358 |
| mod_routes_rebuild | 245.995 | 366.857 |
| mod_routes_speeds | 234.298 | 270.734 |
| mod_routes_sweep | 262.73 | 330.16 |
| mod_summary | 14.565 | 36.382 |
| physical | 0 | 0 |
| physical_ambient | 200.059 | 225.319 |
| physical_bindings | 322.738 | 219.351 |
| physical_fade | 5.661 | 5.802 |
| physical_focus | 62.164 | 65.505 |
| physical_index | 17.038 | 33.739 |
| physical_observe | 3041.465 | 1997.29 |
| physical_parked | 535.69 | 687.153 |
| physical_queues | 82.043 | 189.362 |
| physical_release | 86.778 | 127.74 |
| physical_roster | 430.72 | 555.36 |
| physical_selection | 72.553 | 44.874 |
| physical_setup_ambient | 1.192 | 7.838 |
| physical_setup_bodies | 0.549 | 1.051 |
| physical_setup_places | 1.719 | 1.879 |
| physical_setup_preflight | 0.054 | 0.26 |
| physical_setup_vehicles | 0.985 | 2.171 |
| physical_spawn_appearance | 483.06 | 263.276 |
| physical_spawn_bake | 159.075 | 52.174 |
| physical_spawn_check | 0.982 | 0.368 |
| physical_spawn_grow | 89.454 | 0 |
| physical_spawn_pedestrian | 404.459 | 371.512 |
| physical_spawn_randomize | 16.549 | 9.337 |
| physical_spawn_vehicle | 15.362 | 28.109 |
| physical_unattributed | 93.086 | 99.685 |
| physical_unattributed_entry | 41.601 | 44.343 |
| physical_unattributed_setup | 0.11 | 0.258 |
| physical_unattributed_update | 40.996 | 35.568 |
| physical_unattributed_waiting | 0.037 | 0.06 |
| physical_vehicles | 432.63 | 256.152 |
| physical_vendor_meals | 7.455 | 8.122 |

### Inclusive/exclusive timing and spike retention

| Window PROVISIONAL | seconds / frames | UpdateCity key | self / inclusive p95 ms | union p95 / max ms / >=30 frames | whole-frame p95 / max ms |
| --- | --- | --- | --- | --- | --- |
| gc-day | 600.712/16618 | updatecity | 0.3089/3.4115 | 3.4692/37.4323/2 | 46.5319/596.0975 |
| gc-night | 600.734/16420 | updatecity | 0.3765/2.9422 | 3.0080/89.2865/3 | 47.0762/1349.7930 |
| street | 600.604/4822 | updatecity | 4.0914/4.0914 | 4.1405/85.2268/3 | 201.1872/483.9311 |

**gc-day: all components.** Self time excludes nested timed scopes; inclusive time includes them; union counts nested scopes once.

| Component | calls | exclusive p95 / max / total ms | inclusive p95 / max / total ms |
| --- | --- | --- | --- |
| advance | 16587 | 2.5915 / 9.8433 / 6347.9613 | 2.5915 / 9.8433 / 6347.9613 |
| PopulationRuntime.LateUpdate | 16619 | 0.003 / 0.0504 / 30.3205 | 0.025 / 3.4176 / 330.9173 |
| PopulationRuntime.Update | 16619 | 0.0088 / 0.5868 / 122.4958 | 3.4203 / 37.4051 / 17604.594 |
| inspector | 16587 | 0.0065 / 0.0347 / 78.8618 | 0.0065 / 0.0347 / 78.8618 |
| AmbientPedestrians.EndCyclePrefix | 1 | 0.0 / 1.1437 / 1.1437 | 0.0 / 1.1437 / 1.1437 |
| AmbientPedestrians.SetActivePrefix | 4 | 0.0 / 0.0132 / 0.0224 | 0.0 / 0.0132 / 0.0224 |
| NativeCityClock.AccountingPrefix | 16619 | 0.015 / 0.1401 / 214.0496 | 0.015 / 0.1401 / 214.0496 |
| NativeCityClock.RidePostfix | 16619 | 0.0006 / 0.0046 / 8.1208 | 0.0006 / 0.0046 / 8.1208 |
| NativeCityClock.RidePrefix | 16619 | 0.0006 / 0.0048 / 8.5396 | 0.0006 / 0.0048 / 8.5396 |
| NativeCityClock.TickPrefix | 16619 | 0.001 / 0.0062 / 14.2272 | 0.001 / 0.0062 / 14.2272 |
| NativeFoodOwnership.AfterNativeTick | 16619 | 0.0009 / 0.6428 / 13.1315 | 0.0009 / 0.6428 / 13.1315 |
| NativeTravelPace.ObserveTickPostfix | 16619 | 0.0016 / 0.0171 / 22.794 | 0.0016 / 0.0171 / 22.794 |
| ResidentInspector.CameraInputPostfix | 33238 | 0.0011 / 0.0055 / 10.6709 | 0.0011 / 0.0055 / 10.6709 |
| ResidentInspector.MouseRunPrefix | 16619 | 0.0019 / 0.0151 / 26.4405 | 0.0019 / 0.0151 / 26.4405 |
| ResidentInspector.UiRaycastPrefix | 581665 | 0.0073 / 0.031 / 108.237 | 0.0073 / 0.031 / 108.237 |
| physical | 49808 | 0.9168 / 8.2224 / 6631.526 | 0.9168 / 8.2224 / 6631.526 |
| prepare | 16602 | 0.0244 / 6.8602 / 588.2799 | 0.0244 / 6.8602 / 588.2799 |
| PhysicalPedestrianCrosswalk.OnTriggerEnter | 2173 | 0.0042 / 0.2247 / 11.1449 | 0.0042 / 0.2247 / 11.1449 |
| PhysicalPedestrianCrosswalk.OnTriggerExit | 1423 | 0.0015 / 0.3588 / 5.9023 | 0.0015 / 0.3588 / 5.9023 |
| updatecity | 16619 | 0.3089 / 37.3879 / 4136.0657 | 3.4115 / 37.392 / 17482.0982 |

| Recorder | counted | retained | truncated | cap |
| --- | --- | --- | --- | --- |
| rig mod-work (#94) | 2 | 2 | False | 1024 |
| rig whole-frame (#94) | 15966 | 1024 | True | 1024 |
| PC phase recorder | 2 | 2 | False | 64 |
| rig GC pause rows | 14 | 14 | False | 4096 |

Every retained >=30 ms mod-work row, joined to PC attribution by Unity frame. Missing phase data remains unavailable; it is never inferred from a nearby GC frame. Whole-frame timing and retention are separate from mod work.

| unity_frame | union ms | phase / ms | detail / ms | sub / ms | gc_phase | collections | rig GC marker ms / collections | dominant |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 3375 | 37.4323 | mod_routes/37.221 | none/0 | mod_routes_rebuild/37.22 | none | 0 | none | updatecity |
| 3376 | 30.3484 | mod_routes/30.138 | none/0 | mod_routes_rebuild/30.137 | none | 0 | none | updatecity |

GC pauses keyed by unity_frame, source Unity.Profiling.ProfilerRecorder:GC.Collect. Total/max marker ms: 183.7945/25.5425.

| unity_frame | GC.Collect duration ms | collections | generation |
| --- | --- | --- | --- |
| 3397 | 2.8188 | 0 | None |
| 3420 | 23.2558 | 1 | 0 |
| 5813 | 0.4688 | 0 | None |
| 5841 | 24.4625 | 1 | 0 |
| 8174 | 3.948 | 0 | None |
| 8216 | 24.2103 | 1 | 0 |
| 10721 | 1.6789 | 0 | None |
| 10756 | 22.999 | 1 | 0 |
| 13187 | 2.8312 | 0 | None |
| 13196 | 23.4621 | 1 | 0 |
| 15608 | 2.6309 | 0 | None |
| 15619 | 22.4837 | 1 | 0 |
| 17966 | 3.0022 | 0 | None |
| 17992 | 25.5425 | 1 | 0 |

Additional perf-guard GC rows at the different window seams:

| unity_frame | GC.Collect duration ms | collections | generation |
| --- | --- | --- | --- |
| 19975 | 3.0054 | 0 | None |

Coverage complete=True; validity={"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}. Union of the mod assembly's Unity messages (Update/LateUpdate/FixedUpdate/OnGUI, Start/OnDestroy, OnTrigger*/OnCollision* and the rest), its Harmony prefixes/postfixes/finalizers, the mod methods its transpilers inject into native IL, GlobalEvents and SceneManager subscribers and IModBigAmbitions methods, timed in place. Coroutines, worker threads and callbacks registered with other native events are not timed. Observer overhead of nested scopes stays inside the union.

| Autosave before / after | native-saving frames | save activity |
| --- | --- | --- |
| True/True | 0 | {"attempts":10674,"allowed":0,"refused":10674,"native_saving_frames":0,"clean":true,"invalid_reasons":[]} |

**gc-night: all components.** Self time excludes nested timed scopes; inclusive time includes them; union counts nested scopes once.

| Component | calls | exclusive p95 / max / total ms | inclusive p95 / max / total ms |
| --- | --- | --- | --- |
| advance | 16363 | 1.774 / 14.8468 / 3768.6129 | 1.774 / 14.8468 / 3768.6129 |
| PopulationRuntime.LateUpdate | 16421 | 0.0035 / 0.1399 / 36.8093 | 0.0267 / 0.5232 / 349.4203 |
| PopulationRuntime.Update | 16421 | 0.0103 / 4.1061 / 141.5304 | 2.9506 / 89.2585 / 13439.2612 |
| inspector | 16363 | 0.0077 / 0.1119 / 89.9271 | 0.0077 / 0.1119 / 89.9271 |
| AmbientPedestrians.SetActivePrefix | 2 | 0.0 / 0.0121 / 0.0187 | 0.0 / 0.0121 / 0.0187 |
| NativeCityClock.AccountingPrefix | 16421 | 0.016 / 0.4106 / 227.9165 | 0.016 / 0.4106 / 227.9165 |
| NativeCityClock.RidePostfix | 16421 | 0.0007 / 0.0144 / 8.5912 | 0.0007 / 0.0144 / 8.5912 |
| NativeCityClock.RidePrefix | 16421 | 0.0007 / 0.015 / 8.8777 | 0.0007 / 0.015 / 8.8777 |
| NativeCityClock.TickPrefix | 16421 | 0.0011 / 0.0378 / 15.0299 | 0.0011 / 0.0378 / 15.0299 |
| NativeFoodOwnership.AfterNativeTick | 16421 | 0.0009 / 0.0295 / 12.5054 | 0.0009 / 0.0295 / 12.5054 |
| NativeTravelPace.ObserveTickPostfix | 16421 | 0.0019 / 0.0325 / 25.9338 | 0.0019 / 0.0325 / 25.9338 |
| ResidentInspector.CameraInputPostfix | 32842 | 0.0014 / 0.0173 / 12.0988 | 0.0014 / 0.0173 / 12.0988 |
| ResidentInspector.MouseRunPrefix | 16421 | 0.0021 / 0.0364 / 28.4058 | 0.0021 / 0.0364 / 28.4058 |
| ResidentInspector.UiRaycastPrefix | 574735 | 0.0086 / 0.0514 / 114.9363 | 0.0086 / 0.0514 / 114.9363 |
| physical | 49163 | 0.9402 / 52.0668 / 5502.0862 | 0.9402 / 52.0668 / 5502.0862 |
| prepare | 16379 | 0.0285 / 2.8508 / 702.0599 | 0.0285 / 2.8508 / 702.0599 |
| PhysicalPedestrianCrosswalk.OnTriggerEnter | 1056 | 0.0 / 0.5192 / 6.4532 | 0.0 / 0.5192 / 6.4532 |
| PhysicalPedestrianCrosswalk.OnTriggerExit | 698 | 0.0 / 0.3314 / 3.4227 | 0.0 / 0.3314 / 3.4227 |
| updatecity | 16421 | 0.3765 / 89.246 / 3547.6557 | 2.9422 / 89.2463 / 13297.7308 |

| Recorder | counted | retained | truncated | cap |
| --- | --- | --- | --- | --- |
| rig mod-work (#94) | 3 | 3 | False | 1024 |
| rig whole-frame (#94) | 15702 | 1024 | True | 1024 |
| PC phase recorder | 3 | 3 | False | 64 |
| rig GC pause rows | 14 | 14 | False | 4096 |

Every retained >=30 ms mod-work row, joined to PC attribution by Unity frame. Missing phase data remains unavailable; it is never inferred from a nearby GC frame. Whole-frame timing and retention are separate from mod work.

| unity_frame | union ms | phase / ms | detail / ms | sub / ms | gc_phase | collections | rig GC marker ms / collections | dominant |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 30050 | 89.2865 | mod_routes/89.144 | none/0 | mod_routes_rebuild/89.142 | mod_routes | 1 | 30.7455/1 | updatecity |
| 30067 | 76.3214 | mod_closures/19.946 | physical_spawn_bake/0.634 | mod_closures_network/18.65 | none | 0 | none | updatecity |
| 30189 | 72.1088 | physical_parked/20.208 | none/0 | mod_closures_links/8.305 | none | 0 | none | physical |

GC pauses keyed by unity_frame, source Unity.Profiling.ProfilerRecorder:GC.Collect. Total/max marker ms: 203.8896/30.7455.

| unity_frame | GC.Collect duration ms | collections | generation |
| --- | --- | --- | --- |
| 30034 | 3.0187 | 0 | None |
| 30050 | 30.7455 | 1 | 0 |
| 32407 | 2.9997 | 0 | None |
| 32443 | 24.2863 | 1 | 0 |
| 35089 | 2.4831 | 0 | None |
| 35127 | 26.5131 | 1 | 0 |
| 37792 | 2.7876 | 0 | None |
| 37829 | 26.8584 | 1 | 0 |
| 40573 | 3.0085 | 0 | None |
| 40587 | 23.6654 | 1 | 0 |
| 43317 | 8.3767 | 0 | None |
| 43325 | 22.018 | 1 | 0 |
| 45952 | 2.312 | 0 | None |
| 45967 | 24.8167 | 1 | 0 |

Coverage complete=True; validity={"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}. Union of the mod assembly's Unity messages (Update/LateUpdate/FixedUpdate/OnGUI, Start/OnDestroy, OnTrigger*/OnCollision* and the rest), its Harmony prefixes/postfixes/finalizers, the mod methods its transpilers inject into native IL, GlobalEvents and SceneManager subscribers and IModBigAmbitions methods, timed in place. Coroutines, worker threads and callbacks registered with other native events are not timed. Observer overhead of nested scopes stays inside the union.

| Autosave before / after | native-saving frames | save activity |
| --- | --- | --- |
| True/True | 0 | {"attempts":16451,"allowed":0,"refused":16451,"native_saving_frames":0,"clean":true,"invalid_reasons":[]} |

**street: all components.** Self time excludes nested timed scopes; inclusive time includes them; union counts nested scopes once.

| Component | calls | exclusive p95 / max / total ms | inclusive p95 / max / total ms |
| --- | --- | --- | --- |
| PopulationRuntime.LateUpdate | 4823 | 0.0212 / 0.8462 / 81.7873 | 0.0212 / 0.8462 / 81.7873 |
| PopulationRuntime.Update | 4823 | 0.0077 / 0.0606 / 30.8222 | 4.0984 / 85.1993 / 8427.0997 |
| AmbientPedestrians.EndCyclePrefix | 2 | 0.0 / 0.0031 / 0.0031 | 0.0 / 0.0031 / 0.0031 |
| AmbientPedestrians.SetActivePrefix | 4 | 0.0 / 0.0078 / 0.0129 | 0.0 / 0.0078 / 0.0129 |
| NativeCityClock.AccountingPrefix | 4823 | 0.0151 / 0.055 / 63.9328 | 0.0151 / 0.055 / 63.9328 |
| NativeCityClock.RidePostfix | 4823 | 0.0006 / 0.0009 / 2.3636 | 0.0006 / 0.0009 / 2.3636 |
| NativeCityClock.RidePrefix | 4823 | 0.0006 / 0.0048 / 2.4882 | 0.0006 / 0.0048 / 2.4882 |
| NativeCityClock.TickPrefix | 4823 | 0.001 / 0.0022 / 4.1664 | 0.001 / 0.0022 / 4.1664 |
| NativeFoodOwnership.AfterNativeTick | 4823 | 0.0009 / 0.0062 / 3.5684 | 0.0009 / 0.0062 / 3.5684 |
| NativeTravelPace.ObserveTickPostfix | 4823 | 0.0016 / 0.0059 / 6.2049 | 0.0016 / 0.0059 / 6.2049 |
| ResidentInspector.CameraInputPostfix | 9646 | 0.0008 / 0.0034 / 2.9367 | 0.0008 / 0.0034 / 2.9367 |
| ResidentInspector.MouseRunPrefix | 4823 | 0.0019 / 0.0063 / 7.797 | 0.0019 / 0.0063 / 7.797 |
| ResidentInspector.UiRaycastPrefix | 163982 | 0.0072 / 0.0268 / 31.4809 | 0.0072 / 0.0268 / 31.4809 |
| PhysicalPedestrianCrosswalk.OnTriggerEnter | 981 | 0.0046 / 0.0863 / 4.0936 | 0.0046 / 0.0863 / 4.0936 |
| PhysicalPedestrianCrosswalk.OnTriggerExit | 527 | 0.0015 / 0.0194 / 1.8597 | 0.0015 / 0.0194 / 1.8597 |
| updatecity | 4823 | 4.0914 / 85.1893 / 8396.2769 | 4.0914 / 85.1896 / 8396.2775 |

| Recorder | counted | retained | truncated | cap |
| --- | --- | --- | --- | --- |
| rig mod-work (#94) | 3 | 3 | False | 1024 |
| rig whole-frame (#94) | 4822 | 1024 | True | 1024 |
| PC phase recorder | 3 | 3 | False | 64 |
| rig GC pause rows | 8 | 8 | False | 4096 |

Every retained >=30 ms mod-work row, joined to PC attribution by Unity frame. Missing phase data remains unavailable; it is never inferred from a nearby GC frame. Whole-frame timing and retention are separate from mod work.

| unity_frame | union ms | phase / ms | detail / ms | sub / ms | gc_phase | collections | rig GC marker ms / collections | dominant |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 57969 | 49.5042 | mod_routes/49.26 | none/0 | mod_routes_rebuild/49.258 | none | 0 | none | updatecity |
| 59278 | 85.2268 | mod_routes/84.949 | none/0 | mod_routes_rebuild/84.947 | none | 0 | none | updatecity |
| 59318 | 30.4416 | mod_closures/10.347 | physical_spawn_bake/0.508 | mod_closures_network/10.18 | none | 0 | none | updatecity |

GC pauses keyed by unity_frame, source Unity.Profiling.ProfilerRecorder:GC.Collect. Total/max marker ms: 121.1486/29.8036.

| unity_frame | GC.Collect duration ms | collections | generation |
| --- | --- | --- | --- |
| 55529 | 3.0112 | 0 | None |
| 55549 | 29.8036 | 1 | 0 |
| 57760 | 3.0008 | 0 | None |
| 57799 | 25.034 | 1 | 0 |
| 58655 | 3.021 | 0 | None |
| 58715 | 27.6643 | 1 | 0 |
| 59593 | 2.4855 | 0 | None |
| 59650 | 27.1281 | 1 | 0 |

Coverage complete=True; validity={"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}. Union of the mod assembly's Unity messages (Update/LateUpdate/FixedUpdate/OnGUI, Start/OnDestroy, OnTrigger*/OnCollision* and the rest), its Harmony prefixes/postfixes/finalizers, the mod methods its transpilers inject into native IL, GlobalEvents and SceneManager subscribers and IModBigAmbitions methods, timed in place. Coroutines, worker threads and callbacks registered with other native events are not timed. Observer overhead of nested scopes stays inside the union.

| Autosave before / after | native-saving frames | save activity |
| --- | --- | --- |
| True/True | 0 | {"attempts":4837,"allowed":0,"refused":4837,"native_saving_frames":0,"clean":true,"invalid_reasons":[]} |

### Clock retime lines

```text
[Mod:PersistentCity] Clock retime: body release 0.0 ms in the previous update, journey retime 61.1 ms over 15 updates (slowest 4.8 ms, restarts 0), final slice and commit 2.2 ms, simulation rebuild 13.7 ms.
[Mod:PersistentCity] Clock retime: body release 48.7 ms in the previous update, journey retime 52.4 ms over 13 updates (slowest 4.1 ms, restarts 0), final slice and commit 0.9 ms, simulation rebuild 11.4 ms.
[Mod:PersistentCity] Clock retime: body release 37.2 ms in the previous update, journey retime 54.1 ms over 14 updates (slowest 4.1 ms, restarts 0), final slice and commit 0.9 ms, simulation rebuild 11.8 ms.
[Mod:PersistentCity] Clock retime: body release 57.0 ms in the previous update, journey retime 70.7 ms over 18 updates (slowest 4.1 ms, restarts 0), final slice and commit 1.3 ms, simulation rebuild 16.2 ms.
[Mod:PersistentCity] Clock retime: body release 246.0 ms in the previous update, journey retime 175.6 ms over 41 updates (slowest 5.7 ms, restarts 0), final slice and commit 3.0 ms, simulation rebuild 85.0 ms.
[Mod:PersistentCity] Clock retime: body release 62.4 ms in the previous update, journey retime 66.7 ms over 17 updates (slowest 4.6 ms, restarts 0), final slice and commit 1.2 ms, simulation rebuild 15.7 ms.
[Mod:PersistentCity] Clock retime: body release 60.0 ms in the previous update, journey retime 81.4 ms over 20 updates (slowest 4.5 ms, restarts 0), final slice and commit 1.6 ms, simulation rebuild 23.2 ms.
[Mod:PersistentCity] Clock retime: body release 49.3 ms in the previous update, journey retime 75.3 ms over 19 updates (slowest 4.2 ms, restarts 0), final slice and commit 1.2 ms, simulation rebuild 16.1 ms.
[Mod:PersistentCity] Clock retime: body release 84.9 ms in the previous update, journey retime 88.1 ms over 22 updates (slowest 4.1 ms, restarts 0), final slice and commit 1.5 ms, simulation rebuild 20.7 ms.
```

### Shared-host evidence at every window boundary

All numbers above are PROVISIONAL. This session ran no build or test during the measured windows. Each boundary records uptime load, memory pressure and the exact first eight ps lines; per-minute snapshots and build cwd attribution remain in the stage. The running game is the A headless candidate shown above.

**gc-day start 2026-11-15T13:01:19Z**

```text
14:01  up 1 day, 19:39, 1 user, load averages: 5.33 6.24 6.49
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 62%
  PID  %CPU COMM
96325 100.0 dotnet
49418  90.2 suggestd
98723  53.2 Big Ambitions Agent Tests Mac
  444  10.9 coreaudiod
98977   4.4 Python
  735   2.8 Resolve
  915   1.4 sysmond
```

**gc-day end 2026-11-15T13:11:22Z**

```text
14:11  up 1 day, 19:49, 1 user, load averages: 8.03 6.32 6.40
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 64%
  PID  %CPU COMM
98723  96.5 Big Ambitions Agent Tests Mac
49418  56.5 suggestd
  384  36.1 mds
50220  12.0 com.apple.Virtualization.VirtualMachine
    1  10.0 launchd
  444   7.1 coreaudiod
  561   4.1 mds_stores
```

| Live session | phases observed |
| --- | --- |
| 20261115-115959-ux-insurgo-history-1970 | fix, review |
| 20261115-133316-mac-fix-gr5-rig15d-leaves | implement, local_tests, review |
| 20261115-134624-rig-walker-binding | ci |

| Build/service PID | observed cwd |
| --- | --- |
| 3013 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 5519 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves |
| 5522 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves |
| 5585 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves |
| 5587 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves |
| 5643 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves |
| 6697 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 96320 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/Compatibility |
| 96322 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests |
| 96323 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/Core |
| 96324 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests |
| 96325 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests/bin/Debug/net10.0 |
| 97612 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97613 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97656 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97657 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97686 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97748 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 99477 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/Compatibility |
| 99490 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests |
| 99491 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/Core |
| 99492 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests |
| 99493 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests/bin/Debug/net10.0 |

**gc-night start 2026-11-15T13:17:42Z**

```text
14:17  up 1 day, 19:55, 1 user, load averages: 7.96 8.60 7.52
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 43%
  PID  %CPU COMM
11205 141.7 next-server (v16.4.2)
49418  86.6 suggestd
98723  40.2 Big Ambitions Agent Tests Mac
11230  26.1 chrome-headless-shell
  444   9.9 coreaudiod
11229   9.4 chrome-headless-shell
98977   4.8 Python
```

**gc-night end 2026-11-15T13:27:45Z**

```text
14:27  up 1 day, 20:05, 1 user, load averages: 9.45 7.76 7.52
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 58%
  PID  %CPU COMM
15942 292.8 dotnet
98723  98.3 Big Ambitions Agent Tests Mac
50220  68.3 com.apple.Virtualization.VirtualMachine
49418  51.3 suggestd
16061  22.1 node
50209  10.0 limactl
  444   7.9 coreaudiod
```

| Live session | phases observed |
| --- | --- |
| 20261115-115959-ux-insurgo-history-1970 | ci, fix, local_tests, review |
| 20261115-133316-mac-fix-gr5-rig15d-leaves | fix, review |
| 20261115-141518-rig-resident-reacquire | ci, fix, implement, review |

| Build/service PID | observed cwd |
| --- | --- |
| 10210 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 10211 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 10257 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 10258 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 10298 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 10329 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 10341 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 10656 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 10721 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves |
| 10731 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves |
| 10732 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests/bin/Debug/net10.0 |
| 11139 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 11189 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 11193 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 11202 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 11227 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 11228 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 11673 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 11703 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 11798 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 11849 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 11852 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 12047 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 12048 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 12049 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 12050 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 12051 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 12052 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 12238 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 12745 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 12746 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 12798 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 12800 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 12842 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 13017 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 13401 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 13680 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 13681 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 13696 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 13700 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 13701 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 13824 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 13827 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 14087 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 14088 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 14089 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 14090 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 14091 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 14092 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 14351 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 14694 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 14742 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 14745 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 14746 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 15367 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 15370 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 15435 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 15436 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 15492 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 15559 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 15616 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/Compatibility |
| 15617 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests |
| 15618 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/Core |
| 15874 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/Compatibility |
| 15883 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests |
| 15884 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/Core |
| 15936 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests |
| 15942 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests/bin/Debug/net10.0 |
| 16005 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 16052 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 16055 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 16061 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 6697 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 7164 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 7167 | ~/Coding_Projects/work/MVP_project-worktrees/ux-insurgo-history-1970 |
| 7266 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves |
| 7268 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves |
| 7329 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves |
| 7337 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves |
| 7389 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves |
| 7439 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves |
| 97612 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97613 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97656 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97657 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97686 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97748 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |

**street start 2026-11-15T13:33:15Z**

```text
14:33  up 1 day, 20:11, 1 user, load averages: 6.11 7.88 7.78
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 68%
  PID  %CPU COMM
15942  99.7 dotnet
49418  84.9 suggestd
98723  39.3 Big Ambitions Agent Tests Mac
  444  11.1 coreaudiod
17338  10.2 mdworker_shared
98977   4.0 Python
  735   2.0 Resolve
```

**street end 2026-11-15T13:43:18Z**

```text
14:43  up 1 day, 20:21, 1 user, load averages: 5.80 6.61 7.01
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 67%
  PID  %CPU COMM
98723 112.1 Big Ambitions Agent Tests Mac
18135 100.0 dotnet
49418  87.6 suggestd
  444  11.5 coreaudiod
  735   2.6 Resolve
11192   2.5 VBCSCompiler
  384   1.4 mds
```

| Live session | phases observed |
| --- | --- |
| 20261115-133316-mac-fix-gr5-rig15d-leaves | fix, local_tests |
| 20261115-141518-rig-resident-reacquire | ci, fix, review |

| Build/service PID | observed cwd |
| --- | --- |
| 15874 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/Compatibility |
| 15883 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests |
| 15884 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/Core |
| 15936 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests |
| 15942 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests/bin/Debug/net10.0 |
| 16813 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 16817 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 16871 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 16874 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 16909 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 16998 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 17765 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire/rig-ops/tests |
| 18116 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/Compatibility |
| 18129 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests |
| 18130 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/Core |
| 18134 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests |
| 18135 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests/bin/Debug/net10.0 |
| 18220 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 18224 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 18290 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 18294 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 18299 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 18369 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 19289 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 19291 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 19359 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 19362 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 19400 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 19448 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-resident-reacquire |
| 97612 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97613 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97656 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97657 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97686 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97748 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |

### A4 look stability observations

5 native walker handles were followed, moved beyond the 120 m body radius, and queried on return; speed 1→2→1 and a reload followed. These handles do not identify persistent residents or prove body release/reacquire. Headless rendering cannot establish dressed bodies. No saved-curb identity readback is supplied by the player-car op.

| Log | Appearance meshes unavailable | Empty appearance | Merged body |
| --- | --- | --- | --- |
| Player-A-look-final.log | 0 | 0 | 0 |
| Player-D-final.log | 0 | 0 | 0 |

### D: #246 check 4 fresh re-baseline and check 5

Check 4 Expected: "footprint within about 1.5x of the visible ground area and floors within one of the visible storeys." Check 5 Expected: "buildings under one storey count as empty, not unreadable, and the omitted-places warning only counts unreadable ones."

Fresh city `FT RIGTMP-GR5-15f-mac`, no throwaway flag. Official #93 steps 1–8: create/load, pause/autosave check, apply, list/read, Default save under the same loaded name, reload, pause and immediate status before any other mutation, repeat lists/readbacks, revert, load another FT city, read/delete. No late apply/revert adoption used.

```text
[Persistent City] MEASUREMENT MODE gc population (diagnostic only; not saved, cleared on reload; existing persons are never removed) target=off reached=2000 residents=1000 visitors=1000 portals=20 fill=attached refusals=0 visitor_capacity=unplanned. Visitors are added at the next midnight boundary; the fill stops at its own limits and reports the reached count.
```

Apply subjects:

| Role | Place | Class | Original size |
| --- | --- | --- | --- |
| housed_rotated | address:ba%3Astreet_firststreet:22:home | housed | ba:buildingsize_l |
| housed_yard | address:ba%3Astreet_fifthavenue:25:home | housed | ba:buildingsize_b |
| housed_plain | address:ba%3Astreet_broadwaystreet:2:home | housed | ba:buildingsize_l |
| unreadable | address:ba%3Astreet_broadwaystreet:13:home | unreadable | ba:buildingsize_n |
| empty | address:ba%3Astreet_broadwaystreet:23:home | empty | ba:buildingsize_n |

Apply missing=[]; reload missing=[]. A missing native role would be count 0 / ops_building_fixture_no_native_subject, not a failed role.

Immediate reload status:

```text
{
  "applied": true,
  "restored_on_load": true,
  "adopted_at": "CityManager.InitCityBuildingControllers",
  "sidecar": true,
  "load_hook": {
    "installed": true,
    "patch_owner": "agent-rig.unrentable-fixture-load",
    "seams": [
      "CityManager.InitCityBuildingControllers",
      "GlobalEvents.InvokeOnGameLoaded"
    ],
    "error": null,
    "last": {
      "GlobalEvents.InvokeOnGameLoaded": {
        "save": "FT RIGTMP-GR5-15f-mac",
        "outcome": "already_adopted",
        "error": null,
        "utc": "2026-11-15T13:50:54.3180700Z"
      },
      "CityManager.InitCityBuildingControllers": {
        "save": "FT RIGTMP-GR5-15f-mac",
        "outcome": "adopted",
        "error": null,
        "utc": "2026-11-15T13:50:52.0305790Z"
      }
    }
  }
}
```

Same place per role as apply: True.

| List | matched | geometry_reads / slices | unrentable total / housed / unreadable / empty |
| --- | --- | --- | --- |
| D-list-all | 5 | 5/1 | 5/3/1/1 |
| D-list-yard | 1 | 5/1 | 5/3/1/1 |
| D-list-unreadable | 1 | 5/1 | 5/3/1/1 |
| D-list-empty | 1 | 5/1 | 5/3/1/1 |
| D-reloaded-list-all | 5 | 5/1 | 5/3/1/1 |
| D-reloaded-list-yard | 1 | 5/1 | 5/3/1/1 |
| D-reloaded-list-unreadable | 1 | 5/1 | 5/3/1/1 |
| D-reloaded-list-empty | 1 | 5/1 | 5/3/1/1 |

Every list used a 120 s client timeout. The yard-filter place was read in both loads; at least three unrentable rows and a rotated row were present. All five roles are pc_home_candidate; the loaded mod may omit a candidate with no NavMesh entrance.

| Role | Place | collider floors | independent visible storeys | footprint m² | gross m² | usable m² | collider / visible world AABB | within one floor |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| housed_rotated | address:ba%3Astreet_firststreet:22:home | 13 | 14 | 1431.112 | 20035.568 | 12021.341 | 0.9967 | True |
| housed_yard | address:ba%3Astreet_fifthavenue:25:home | 5 | 6 | 426.709 | 2560.254 | 1536.152 | 0.9759 | True |
| housed_plain | address:ba%3Astreet_broadwaystreet:2:home | 11 | 13 | 3826.154 | 49740.002 | 29844.001 | 1.0 | False |

**Check 4 FAIL:** expected floors within one is not met for the plain subject (see table). The footprint ratios meet about 1.5x. This is the expected re-baseline before the fourteenth fix; the test does not weaken its Expected line. Independent storeys remain renderer-derived, not a native exterior floor count. independent.home_area excludes BuildingSize, size-helper areas, fixture journal and sidecar; no saved-Home extraction is claimed.

**Check 5 PASS:** in-load after apply, the rig classification is the available tally; PC’s earlier line is not observable in-load for the changed subjects. After reload the mod sees 3 housed / 1 unreadable / 1 empty, and the omitted warning counts only unreadable.

```text
[Persistent City] unrentable homes housed=0 beds=0 unreadable=0 empty=0
[Persistent City] unrentable homes housed=3 beds=1270 unreadable=1 empty=1
[Persistent City] Residential places omitted: 1 without a finite footprint or floor count; 0 below the household policy's minimum interior area.
```

Collider-free unreadable reads fail closed with ops_building_bounds_unavailable before and after reload; no count is invented. Revert returned applied=false, original sizes/colliders restored, sidecar=false; post-revert list and deletion replies are retained.

```text
{"applied":false,"save":"FT RIGTMP-GR5-15f-mac","restored_on_load":false,"adopted_at":null,"load_hook":{"installed":true,"patch_owner":"agent-rig.unrentable-fixture-load","seams":["CityManager.InitCityBuildingControllers","GlobalEvents.InvokeOnGameLoaded"],"error":null,"last":{"GlobalEvents.InvokeOnGameLoaded":{"save":"FT RIGTMP-GR5-15f-mac","outcome":"already_adopted","error":null,"utc":"2026-11-15T13:50:54.3180700Z"},"CityManager.InitCityBuildingControllers":{"save":"FT RIGTMP-GR5-15f-mac","outcome":"adopted","error":null,"utc":"2026-11-15T13:50:52.0305790Z"}}},"sidecar":false,"subjects":[],"missing":[]}
```

### Disposable cleanup

`D-delete-read`:

```text
{"name":"FT RIGTMP-GR5-15f-mac","exists":true,"loaded":false,"files":[{"file":"Big Ambitions/smVm91wWcUSxOS86131cOw==/FT RIGTMP-GR5-15f-mac.hsg","bytes":2565575},{"file":"Big Ambitions/smVm91wWcUSxOS86131cOw==/FT RIGTMP-GR5-15f-mac.hsg.meta","bytes":5153},{"file":"Big Ambitions/smVm91wWcUSxOS86131cOw==/FT RIGTMP-GR5-15f-mac.jpg","bytes":4723}]}
```

`D-delete`:

```text
{"before":{"name":"FT RIGTMP-GR5-15f-mac","exists":true,"loaded":false,"files":[{"file":"Big Ambitions/smVm91wWcUSxOS86131cOw==/FT RIGTMP-GR5-15f-mac.hsg","bytes":2565575},{"file":"Big Ambitions/smVm91wWcUSxOS86131cOw==/FT RIGTMP-GR5-15f-mac.hsg.meta","bytes":5153},{"file":"Big Ambitions/smVm91wWcUSxOS86131cOw==/FT RIGTMP-GR5-15f-mac.jpg","bytes":4723}]},"after":{"name":"FT RIGTMP-GR5-15f-mac","exists":false,"loaded":false,"files":[]}}
```

`leftover-read-FT RIGTMP-GR5-15d-mac`:

```text
{"name":"FT RIGTMP-GR5-15d-mac","exists":false,"loaded":false,"files":[]}
```

`leftover-read-FT RIGTMP-GR5-15c`:

```text
{"name":"FT RIGTMP-GR5-15c","exists":false,"loaded":false,"files":[]}
```

The target was not loaded for deletion; D-delete after.exists=false. The 15d/15c leftovers were read, and deleted only if present. FT RIGTMP-GR5-14i and sealed fixtures were not deleted.

### Optional E and pending checklist inventory

Read docs/rig-checklist.md from the pinned origin tip, including 15a–15d. No rig-2026-11-06 checklist or STATUS.md exists in this checkout. E diagnostic replies are retained for capability status/hooks, movement status, player-car identity/pose, options and final save state. These reads do not complete the separate driving, resident identity or saved-curb scenarios.

**E27 capability report cost: PASS, PROVISIONAL.** Separate 600 s window in the timing fixture, GC sampler off; capability status and hooks each requested ten times. Direct named method `components.capability_report` times PopulationRuntime.CapabilityCommand; updatecity is also bound. Strict union p95 <5 ms and every >=30 ms component row are reported.

| seconds / frames | union p95 / max ms | whole-frame p95 / max ms | capability calls | capability self / inclusive p95 / max / total ms |
| --- | --- | --- | --- | --- |
| 600.676/16536 | 3.1165/43.0735 | 45.6996/529.0204 | 20 | {"exclusive":{"count":16536,"p50_ms":0.0,"p95_ms":0.0,"p99_ms":0.0,"max_ms":40.518,"mean_ms":0.0238,"spikes_30ms":2,"total_ms":394.2884},"inclusive":{"count":16536,"p50_ms":0.0,"p95_ms":0.0,"p99_ms":0.0,"max_ms":40.518,"mean_ms":0.0238,"spikes_30ms":2,"total_ms":394.2884}} |

Component percentiles include zero-call frames; method max/total and call count are supplied so sparse report calls are not hidden by those zeros.

| Component | calls | exclusive p95 / max ms | inclusive p95 / max ms |
| --- | --- | --- | --- |
| capability_report | 20 | 0.0/40.518 | 0.0/40.518 |
| PopulationRuntime.LateUpdate | 16537 | 0.0244/3.39 | 0.0244/3.39 |
| PopulationRuntime.Update | 16537 | 0.0074/0.0736 | 3.0476/28.4355 |
| AmbientPedestrians.SetActivePrefix | 2 | 0.0/0.018 | 0.0/0.018 |
| NativeCityClock.AccountingPrefix | 16537 | 0.0167/0.7672 | 0.0167/0.7672 |
| NativeCityClock.RidePostfix | 16537 | 0.0007/0.0036 | 0.0007/0.0036 |
| NativeCityClock.RidePrefix | 16537 | 0.0007/0.0114 | 0.0007/0.0114 |
| NativeCityClock.TickPrefix | 16537 | 0.0009/0.0049 | 0.0009/0.0049 |
| NativeFoodOwnership.AfterNativeTick | 16537 | 0.0008/0.7355 | 0.0008/0.7355 |
| NativeTravelPace.ObserveTickPostfix | 16537 | 0.0018/0.0125 | 0.0018/0.0125 |
| ResidentInspector.CameraInputPostfix | 33074 | 0.0011/0.0213 | 0.0011/0.0213 |
| ResidentInspector.MouseRunPrefix | 16537 | 0.0016/0.0058 | 0.0016/0.0058 |
| ResidentInspector.UiRaycastPrefix | 611869 | 0.0091/0.0462 | 0.0091/0.0462 |
| PhysicalPedestrianCrosswalk.OnTriggerEnter | 1133 | 0.0/0.0533 | 0.0/0.0533 |
| PhysicalPedestrianCrosswalk.OnTriggerExit | 695 | 0.0/0.3494 | 0.0/0.3494 |
| updatecity | 16537 | 3.0409/28.4261 | 3.0409/28.4261 |

| unity_frame | union ms | dominant component | phase | detail | sub | gc_phase | collections |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 4758 | 43.0735 | capability_report | unavailable | unavailable | unavailable | unavailable | unavailable |
| 12867 | 31.4948 | capability_report | unavailable | unavailable | unavailable | unavailable | unavailable |

| Recorder | counted | retained | truncated | cap |
| --- | --- | --- | --- | --- |
| E mod-work | 2 | 2 | False | 1024 |
| E whole-frame | 15961 | 1024 | True | 1024 |
| E phase | 0 | 0 | False | 64 |

| unity_frame | GC.Collect ms | collections | generation |
| --- | --- | --- | --- |
| 4761 | 3.0256 | 0 | None |
| 4801 | 33.7617 | 1 | 0 |
| 7545 | 3.0032 | 0 | None |
| 7585 | 29.8552 | 1 | 0 |
| 10160 | 3.0037 | 0 | None |
| 10185 | 30.2266 | 1 | 0 |
| 12867 | 2.9286 | 0 | None |
| 12906 | 31.8206 | 1 | 0 |
| 15831 | 3.0095 | 0 | None |
| 15852 | 30.307 | 1 | 0 |
| 18576 | 3.0054 | 0 | None |
| 18614 | 31.6305 | 1 | 0 |
| 21250 | 2.9802 | 0 | None |

E autosave before/after suppressed=True/True; save activity={"attempts":11159,"allowed":0,"refused":11159,"native_saving_frames":0,"clean":true,"invalid_reasons":[]}; coverage complete=True; validity={"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}.

**E boundary start 2026-11-15T13:53:01Z**

```text
14:53  up 1 day, 20:31, 1 user, load averages: 7.92 5.93 6.18
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 48%
  PID  %CPU COMM
22771 285.5 dotnet
21166  59.7 Big Ambitions Agent Tests Mac
49418  50.7 suggestd
21850  15.7 claude
  432  10.2 loginwindow
  444   7.5 coreaudiod
  561   4.6 mds_stores
```

**E boundary end 2026-11-15T14:03:04Z**

```text
15:03  up 1 day, 20:41, 1 user, load averages: 7.29 5.95 6.07
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 56%
  PID  %CPU COMM
25191 417.1 dotnet
21166  97.6 Big Ambitions Agent Tests Mac
49418  37.3 suggestd
  444   6.8 coreaudiod
  403   2.8 locationd
  915   2.6 sysmond
  735   1.3 Resolve
```

| Live E session | phases |
| --- | --- |
| 20261115-133316-mac-fix-gr5-rig15d-leaves | fix, local_tests |
| 20261115-144928-ux-wording-ids-1967 | implement |

| Build/service PID | cwd |
| --- | --- |
| 22690 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/Compatibility |
| 22692 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests |
| 22693 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/Core |
| 22767 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests |
| 22771 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests/bin/Debug/net10.0 |
| 25133 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/Compatibility |
| 25133 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/Core |
| 25150 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves |
| 25150 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests |
| 25151 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/Core |
| 25190 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests |
| 25191 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-rig15d-leaves/tests/bin/Debug/net10.0 |
| 97612 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97613 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97656 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97657 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97686 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 97748 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |

`<stage>/capability-end.json` SHA-256 `b32193688dcd8fd5a9cf97724f9d66696ee08cc26aaffa717abb6c2c43b4262d`, 42920 bytes.
`<stage>/capability-guard-end.json` SHA-256 `190f3fd0a86519f88eb93d090aaa49c7a5bf5b0ecb9ced643b559d365b371f3a`, 133117 bytes.
`<stage>/capability-frames-stop.json` SHA-256 `795bcdef1ea6a5421aa5f8a6097fc134c40437360cda327ee851c237374f3ceb`, 7153 bytes.

| E diagnostic | Observed |
| --- | --- |
| player car identity | {"identity":"VehicleInstance.id","inside_vehicle":false,"cars":[],"hold":null} |
| player car pose | {;   "error": "ops_car_none",;   "reply": {;     "ok": false,;     "id": "f623057e6a3a4d96a145f9e7f24ab414",;     "session": "07570b5ac65b419aa30a863c50f50f84",;     "error": "ops_car_none";   }; } |
| RLO option values | {"living_population":{"name":"living_population","mod_id":"RealLifeOverhaul","stored":null,"live":true,"original_saved":false},"social_media":{"name":"social_media","mod_id":"RealLifeOverhaul","stored":1,"live":true,"original_saved":false},"social_media_notices":{"name":"social_media_notices","mod_id":"RealLifeOverhaul","stored":0,"live":false,"original_saved":false}} |

### NOT RUN grouped

- GR-5 60 fps street/busy-shop arms: NOT RUN, Windows-only; Mac about 5–8 fps at 1,000 residents; Peter is gaming on the PC. No PC contact or work.
- GR-5 save latency: NOT RUN, owner decision pending; no save profiling or save-latency measurement.
- #262 checks 4–5 / #254-4 look identity, plus literal #279-4 resident/body/reload and saved-curb proof: NOT RUN, resident-reacquire/binding op absent from the installed build; headless rendering unavailable.
- Other pending checks not selected for this batch: NOT RUN as individually listed below.

| Checklist row | Outcome | Reason / remaining work |
| --- | --- | --- |
| 1 Natural city loop | NOT RUN | No independent full-loop observation scheduled beyond A/D. |
| 2 Other PR checks since 12b | NOT RUN | Only the named #279/#246 rows and E observations selected; remaining food/work/household fixtures not exercised. |
| 3 RLO overlap and own cars | NOT RUN | No off-before-load/two-toggle comparison or saved-curb identity scenario in this run. |
| 4 Mixed/original-three menus | NOT RUN | Menu fixtures unavailable in this loaded timing city. |
| 5 Eight/nine offers and refill | NOT RUN | No multi-offer/stock fixture prepared. |
| 6 Repair storms 8/64 shops | NOT RUN | No mutation-storm cohort fixture. |
| 7 E6 checks 1–13 | NOT RUN | Required Street Food vehicle API/ledger/fault fixture capabilities absent. |
| 8 Street Food absent/returned | NOT RUN | Requires a separate modset removal/restoration arm, outside candidate-only staging. |
| 9 Research and truck | NOT RUN | No pending research/truck checkpoint fixture. |
| 10 Mechanism report | NOT RUN | No qualified vehicle business observation; capability diagnostics alone are insufficient. |
| 11 Representative throughput | NOT RUN | No staffed-shop cohort. |
| 12 Price response at 1.2 | NOT RUN | No paired two-day price intervention; weights unchanged. |
| 13 Larger F7 matrix | NOT RUN | No Pending/fault/relocation/menu comparison arms. |
| 14 X3-1 literal Loaded/schema read | NOT RUN | No dedicated population-reader literal evidence; writerSchema=1 alone is not that check. |
| 15 Wrong/unreadable schema | NOT RUN | No disposable wrong-schema loader/payload arm; schema remains 1. |
| 16 RLO absent | NOT RUN | Would need a separate modset removal arm. |
| 17 Changed native members | NOT RUN | No fault-injection arm selected. |
| 18 Foreign checkout prefix | NOT RUN | No foreign-patch fixture arm selected. |
| 19 Business lifecycle | NOT RUN | Lifecycle/modded-type/older-city fixtures not loaded. |
| 20 Seven days | NOT RUN | No paired seven-day extracts/order/ledger run. |
| 21 Cadences 30/60/120 | NOT RUN | Needs the Windows/frame-cap matrix; no PC work. |
| 22 Detail/in-flight reload | NOT RUN | No paired detail/config comparison fixture. |
| 23 Provenance/capability/summary | PARTIAL | Capability diagnostics read; no complete declared fingerprint set and acceptance summary. |
| 24 Population/GC scale | NOT RUN | Only the authorized 1,000/2,000 fixture measured; stress fixtures not loaded. |
| 25 Sealed F4 fixtures | NOT RUN | Implementation/manifest sealing outside this evidence run. |
| 26 Present/foreground/priority | NOT RUN | No host focus/priority/OS-setting intervention authorized. |
| 27 Capability report cost | PASS | Separate 600 s cost window above. |
| 28 Pointer/hover/focus/click | NOT RUN | Owner playtest. |
| 29 Windows/PC overlay | NOT RUN | Windows-only; Peter is gaming there. |

Supplementary inventory outcomes are not counted again in the A/D/cleanup summary. GR-9 #238 checks 2/3/5/7/8/9/10 remain NOT RUN: #95 player-car ops do not bind resident owner/body identity, and this headless run supplies no visual crossing, parked assignment, red-light or collision-response proof.

### Exact failures and tooling gaps

| Step | Exact command | Exact output |
| --- | --- | --- |
| look-headless-shoot | export BA_LOCK_OWNER=persistent-city-rig-2026-11-15f-mac; python3 scripts/mac-agent call shoot '{"name": "rig15f-look", "x": 215, "y": 18, "z": -18, "look_at": {"x": 215, "y": 1, "z": 0}, "width": 960, "height": 540, "frames": 8, "timeout_s": 60}' | InvalidOperationException: Screenshots require the agent instance's background mode. |
| look-after-reload-0 | export BA_LOCK_OWNER=persistent-city-rig-2026-11-15f-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "walker", "id": "walker-e530ed1f7e3b4f208ed21ac60e14b593"}}' | {;   "error": "ops_view_stale_target",;   "reply": {;     "ok": false,;     "id": "cee9da3657d54dd4bf04aa28562d9f20",;     "session": "44a958010fe44c1f8b20c3df668be1b7",;     "error": "ops_view_stale_target";   }; } |
| look-after-reload-1 | export BA_LOCK_OWNER=persistent-city-rig-2026-11-15f-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "walker", "id": "walker-f821507ef35244abaa8941695956cd08"}}' | {;   "error": "ops_view_stale_target",;   "reply": {;     "ok": false,;     "id": "46550bec3290483597c8c78d9938e850",;     "session": "44a958010fe44c1f8b20c3df668be1b7",;     "error": "ops_view_stale_target";   }; } |
| look-after-reload-2 | export BA_LOCK_OWNER=persistent-city-rig-2026-11-15f-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "walker", "id": "walker-f48332f9a981498eac35dbbf9c4c7fbd"}}' | {;   "error": "ops_view_stale_target",;   "reply": {;     "ok": false,;     "id": "0b17963e5fdb4dd195f018deb4e38e6f",;     "session": "44a958010fe44c1f8b20c3df668be1b7",;     "error": "ops_view_stale_target";   }; } |
| look-after-reload-3 | export BA_LOCK_OWNER=persistent-city-rig-2026-11-15f-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "walker", "id": "walker-ab95b65914e34281a5ee466dd647339a"}}' | {;   "error": "ops_view_stale_target",;   "reply": {;     "ok": false,;     "id": "deb8ee1d93284ecd8b44a0509706d35e",;     "session": "44a958010fe44c1f8b20c3df668be1b7",;     "error": "ops_view_stale_target";   }; } |
| look-after-reload-4 | export BA_LOCK_OWNER=persistent-city-rig-2026-11-15f-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "walker", "id": "walker-adab76edbc1d4fb6aae68b7e1c8692b7"}}' | {;   "error": "ops_view_stale_target",;   "reply": {;     "ok": false,;     "id": "48ba5be84a41489d87ee894df80113a7",;     "session": "44a958010fe44c1f8b20c3df668be1b7",;     "error": "ops_view_stale_target";   }; } |
| D-read-unreadable | export BA_LOCK_OWNER=persistent-city-rig-2026-11-15f-mac; python3 scripts/mac-agent op building '{"action": "read", "place": "address:ba%3Astreet_broadwaystreet:13:home"}' | {;   "error": "ops_building_bounds_unavailable",;   "reply": {;     "ok": false,;     "id": "c39c8a923c6d4436bd80446020765145",;     "session": "07570b5ac65b419aa30a863c50f50f84",;     "error": "ops_building_bounds_unavailable";   }; } |
| D-reloaded-read-unreadable | export BA_LOCK_OWNER=persistent-city-rig-2026-11-15f-mac; python3 scripts/mac-agent op building '{"action": "read", "place": "address:ba%3Astreet_broadwaystreet:13:home"}' | {;   "error": "ops_building_bounds_unavailable",;   "reply": {;     "ok": false,;     "id": "a501bf4a8f844d58852f6d570b36ea4f",;     "session": "07570b5ac65b419aa30a863c50f50f84",;     "error": "ops_building_bounds_unavailable";   }; } |
| E-car-pose | export BA_LOCK_OWNER=persistent-city-rig-2026-11-15f-mac; python3 scripts/mac-agent op car '{"action": "pose"}' | {;   "error": "ops_car_none",;   "reply": {;     "ok": false,;     "id": "f623057e6a3a4d96a145f9e7f24ab414",;     "session": "07570b5ac65b419aa30a863c50f50f84",;     "error": "ops_car_none";   }; } |

Staging had no refusal and required no corrected retry. ops_building_bounds_unavailable is the intended fail-closed unreadable read. ops_view_stale_target does not prove a look changed. Missing stable resident/reacquire/saved-curb capability is evidenced by ops enumeration, not by an invented command or assumed failure. Spikes are reported, not chased.

### Raw evidence and restoration

| Stage-relative raw source | bytes | SHA-256 |
| --- | --- | --- |
| gc-day-gc-stop.json | 16644 | dc4e81628d44aa1115d8c2a511603ce23116481b5a39f4b190fb16483b5c915c |
| gc-night-gc-stop.json | 16864 | c3f6ec43368b209112b2b6815fac91775f34197a4dca501bca2c3f108b854b48 |
| street-frames-stop.json | 8000 | 3c31e09a3c39265b72a9242bf9eaaa692ec25e8389472e6b5db77ec7bda8c79a |
| gc-day-modwork-end.json | 49365 | 60fbb9cb657f0657da2336bdceb99c2512f77a65010f2c824a24064a0aac57af |
| gc-night-modwork-end.json | 48873 | a583cbef0eac772c2026f5ab4ce87a6c6ae0f14bdba456fc1999f5b2c2532c20 |
| street-end.json | 41829 | 520e10d32ddf60224c30b7ff359142edc568daf07588cc629b5058612001dadd |
| gc-day-guard-end.json | 133210 | 43b405a19b2a86855a09626fa335a2b875dcf897b0545c084bda2ddcfb8dbbf6 |
| gc-night-guard-end.json | 134076 | c40450836a58a7b637835b28074dd3f34d761f2284d7c35fb5841ff8a2a028d4 |
| street-guard-end.json | 133159 | 8e708acf997a766189368348ef5acf20838c94c9bebcfb6709f9b78e19207ee7 |
| Player-A-look-final.log | 2142006 | 28bbd23da4e521e73855e0cae92a486ca57c5ade0238a5571de4dbcd37a8f0ea |
| Player-D-reloaded.log | 676845 | 9c1dddb625b7e18b462df62b7dcd4a8e76635b1a43bc690886f4d9956cdc78d1 |
| Player-E-final.log | 1346487 | 12447ebb813e270bc5c8cdfcedaf68e191c9d65e28877d0ff77936b39f74335d |
| commands.jsonl | 72411 | 1137f367324282887a8a101546696170a0a0ba2a38182f5b2f460f4f2727e9d4 |

Full JSON exceeds the small evidence-file budget; all segments/spikes are excerpted above, and full reports remain at `<stage>/<source>` with the hashes in this table. Raw manifest `<stage>/raw-evidence-manifest.json` SHA-256 `b4c0f794d3ebf256d0566c8cf796eed73ed6fe5942eb307870b9ea09c2f7f56e`. No raw log, binary or save is committed.

Fixture reverted, own disposable deleted via disposable_save, no fixture sidecar remains. Owned game stopped normally unless an exact stop failure above says otherwise. Printed stage rollback restored the entire original PersistentCity folder and install.json. Isolated preferences/config/autosave/frame-cap bytes or original absence restored and checked against this run’s pre-stage manifest. RLO files and sealed fixture hashes unchanged. AgentRigOps hash unchanged. Lease released and owned game/caffeinate processes gone.

```text
{"original_files_verified":32,"original_absent_paths_verified":3,"rlo_files_verified":53,"mismatches":[],"agent_rig_ops_sha256":"dc621edf1136d8111ae3bc821d0e7f17903df2f03281b1e7fb1c16dd5a0e589d","install_json_sha256":"2f77e28ac0aec86a6399d2488f9f90e37c01059900d234f9aff5b7fc5cedf19a","fixture_sidecars":0,"disposables_absent":["FT RIGTMP-GR5-15c","FT RIGTMP-GR5-15d-mac","FT RIGTMP-GR5-15f-mac"],"sealed_fixture_hash_match":true,"lease_released":true,"started_game_processes_gone":[98723,21166],"caffeinate_processes_gone":true}
```

Offline checks: candidate build passed; results diff uses existing Markdown only. tools/md-ban.py, docs-links/check.py and git diff --check run before commit. No test source changed; no full suite or regression suite. No production code, schema, weights, version, changelog, release or Workshop change. Original checkout and other sessions untouched. Only this run’s build/evidence worktree is removed after commit/push and PR creation.

Refs #246 #279

## Rig results — 15 November 2026 e, Mac (#279 checks 1-4, #246 check 4 re-baseline at c9378fc)

**RESULT: DONE. 3 passed / 2 partial / 4 failed / 5 NOT RUN groups.** Counted checks are A1–A4, #282-5 height reporting, #246-4 footprint, #246-5 tally, leftover cleanup and optional current-build #262-5 telemetry. All timing/GC/street numbers are **PROVISIONAL (shared Mac)**. In-game proof gates no merge.

Role `rig_test`, where `mac`, isolated `agentmac`, run `persistent-city-rig-2026-11-15g-mac`. The section label above is the launcher-requested label; this is run **15g**, testing **#282**. `<stage>` = `~/HART-Builds/persistent-city-rig-2026-11-15g-mac`. Raw logs, replies, controllers and backups remain there. Report copy: `~/Coding_Projects/.agent-logs/persistent-city-rig-2026-11-15g-mac.md`. MB is decimal. Order: A day/night/street/look, D fresh fixture, E.

MarkdownBanTests and tools/md-ban.py ban new Markdown paths without an owner-applied docs-approved label. Results therefore append to this existing checklist; no docs/checks report is added. No production code, schema, food weights, RLO files, overlay or OS settings changed. No Windows contact, release or merge.

### Build and rig

| Item | Evidence |
| --- | --- |
| Candidate | `c9378fcd131486acd44a5f1a4e54513b4384701b`. origin/codex/persistent-city moved from requested `01a3cb1b540321cfdaf75cbca0814cc030491c5e` only by #283 docs merge. Includes #282. Final fetched tip: c9378fcd131486acd44a5f1a4e54513b4384701b. Tested DLL remains pinned. |
| Branch / worktree | codex/mac-rig-2026-11-15g / rig-2026-11-15g-mac-evidence; built there, original checkout not edited. |
| Build | python3 tools/build.py --game-managed '~/Library/Application Support/BigAmbitionsMCP/dll-import-shim/Big Ambitions_Data/Managed'; PATH=/opt/homebrew/bin:$PATH, DOTNET_ROOT from brew --prefix dotnet + /libexec; exit 0. |
| PersistentCity DLL SHA-256 | b3f9d8024d0a5174619a50cd5168caf29b6864e56c495796919240fb2565363d |
| agent-rig HEAD | 546b3f766f8aaca401ea209b33e908c22f3e00be at launch (clean; #94/#95/#96/#97/#98). Official checkout later moved externally to c2483e1a6e74a2286244a7b348c2fac1305b8ade, clean at cleanup. Live AgentRigOps bytes stayed unchanged; this role made no overlay install/update or official-checkout edits. |
| Live AgentRigOps SHA-256 | 40a288c24dc1976e85d67599395f222b544258c0b820015dbf92b0981be6e6ee; before/after verified. |
| Stage | export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/install-mac.py --stage-mod PersistentCity ~/HART-Builds/persistent-city-rig-2026-11-15g-mac/package; stopped, lease held, owner echoed; first attempt succeeded; 28 receipt entries. |
| Receipt backup | ~/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261115T142839Z |
| Start | export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent start --mode headless --rig-test |
| RLO | Real Life Overhaul 1.0.6; SaveCostDiagnostics requested=false; schema 1. |
| City | FT GR5 13f Timing v1: 1,000 residents / 2,000 visitors; no fallback. FT RIGTMP-GR5-15g-mac: fresh 1,000 residents / 1,000 visitors; deleted. |
| Autosave original | native interval 5 minutes / 3 saves, prevent_autosave=false. --rig-test suppression checked before/after every window and after reload; settings restored. |
| Stream guard | idle: obs64.exe is not running (since 2026-11-14T12:44:01+01:00, checked 2026-11-15T15:23:01+01:00, from registry); registry read only, no Windows contact. |

```text
[AgentRigOps] staged_mod name=PersistentCity version=absent informational=0.10.0-persistent-city-candidate sha256=b3f9d8024d0a5174619a50cd5168caf29b6864e56c495796919240fb2565363d
```

Rollback executed verbatim from `<stage>/stage.log`, with the game stopped and the lease held. This committed excerpt abbreviates home paths to `~`:

```text
rm -rf "~/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp -Rp "~/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261115T142839Z/mod" "~/Library/Application Support/com.hovgaardgames.bigambitions.agenttests.mac/ModsLocal/PersistentCity" && cp "~/Library/Application Support/BigAmbitionsAgentTestsMac/receipt-backups/20261115T142839Z/install.json" "~/Library/Application Support/BigAmbitionsAgentTestsMac/install.json"
```

### Per-check Expected lines and outcomes

**#282 check 1: FAIL.** 1. **Day GC window.** Setup: FT GR5 13f Timing v1, 1,000 residents / 2,000 visitors, clock 15:30, idle camera, speed 1 after a 20 to 1 change. Steps: run a 600 s gc window. Expected: report gen0, pause p95, heap, mod, physical, advance and every segment and sub-segment vs 15d. physical_spawn_appearance well below 33.5 MB. The look diagnostics show `fast merges` > 0 and `failed merges` 0. mod_routes_nodes near 0.5 MB if the rate returned to one seen earlier in the session.

**#282 check 2: FAIL.** 2. **Night GC window.** Same at 01:00. Expected: appearance well below 14.1 MB; report every segment vs 15d night.

**#282 check 3: FAIL.** 3. **Street window.** Setup: same fixture, daytime. Steps: 600 s street window with pans and at least one clock-rate change and back. Expected: report UpdateCity and mod-work p95 and every ≥30 ms spike with phase, detail, sub and gc_phase, plus whole-frame p95/max separately. The phase recorder reports `phase_spikes_30ms`, `phase_spikes_retained` and `phase_spikes_truncated=false`. Note any spike from retime/mod_routes_rebuild, physical_spawn_appearance or mod_rlo with its whole-frame time.

**#282 check 4: PARTIAL.** 4. **Look stability.** Setup: same fixture, daytime street. Steps: follow 5 residents through body release and reacquire, change speed once, reload and find them again. Expected: dressed and animated bodies with blendshapes applied, the same look per resident before and after, `failed merges` 0, and none of "Appearance meshes unavailable", "Empty appearance" or "Merged body" in Player.log.

**#282 check 5: PASS.** 5. **#246 check 4.** Setup: fresh fixture with the three subjects. Steps: run the check. Expected: report collider height and renderer height per subject next to the floor counts. The mod is unchanged, so floors stay 13 / 5 / 11; see item 6 for why the renderer storeys are higher.

Separate GC gate from #246-3: gen0 no worse than 5, pause p95 no worse than 35.9 ms (launcher target under about 36 ms), heap at or below 1.47 GB. Street gate: UpdateCity and union p95 under 5 ms, no mod-work frame >=30 ms. A measured failure is retained even when the broader reporting Expected line is met.

| Check | Outcome |
| --- | --- |
| gc-day | FAIL |
| gc-night | FAIL |
| street | FAIL |
| look | PARTIAL |
| 282-5-height-report | PASS |
| 246-4-footprint | FAIL |
| 246-5-tally | PASS |
| leftover-cleanup | PASS |
| 262-5-current-refill | PARTIAL |

| Window | Criterion | Observed | Met |
| --- | --- | --- | --- |
| gc-day | gen0 <=5 | 7 | False |
| gc-day | pause p95 <=35.9 ms (about 36 ms) | 449.376 | False |
| gc-day | Mono heap <=1.47 GB | 1474428928 | False |
| gc-day | appearance well below 33.5 MB | 43.274 | not met |
| gc-day | fast merges >0 / failed merges =0 | 0 / 0 | False |
| gc-night | gen0 <=5 | 5 | True |
| gc-night | pause p95 <=35.9 ms (about 36 ms) | 102.848 | False |
| gc-night | Mono heap <=1.47 GB | 1575092224 | False |
| gc-night | appearance well below 14.1 MB | 13.931 | well-below reduction not demonstrated; fast-path clause separately checked |
| gc-night | fast merges >0 / failed merges =0 | 0 / 0 | False |
| gc-day | mod_routes_nodes near 0.5 MB on return to seen rate | 3.369 | not met: about 3.37 MB retained |
| gc-night | mod_routes_nodes near 0.5 MB on return to seen rate | 3.369 | not met: about 3.37 MB retained |
| street | UpdateCity inclusive p95 <5 ms | 6.9194 | False |
| street | union p95 <5 ms | 6.9628 | False |
| street | no union frame >=30 ms | 25 | False |
| street | phase_spikes_truncated=false | False | True |

### GC totals and comparisons — all 15g numbers PROVISIONAL

**gc-day**

| Metric | Value |
| --- | --- |
| residents | 1000 |
| visitors | 2000 |
| persons | 3000 |
| frames | 16297 |
| alloc_counter | "boehm_total" |
| gen0 | 7 |
| gen1 | 0 |
| gen2 | 0 |
| pause_ms | {"count":7,"p50":27.297,"p95":449.376,"max":449.376} |
| heap_start_bytes | 1267228672 |
| heap_end_bytes | 1278316544 |
| heap_peak_bytes | 1467764736 |
| mono_heap_bytes | 1474428928 |
| mono_used_bytes | 1278316544 |
| counter_frame_bytes | 1593398120 |
| mod_alloc_bytes | 214783392 |
| physical_alloc_bytes | 56495024 |
| advance_alloc_bytes | 91585432 |

Look diagnostics, last periodic cumulative snapshot (not exact window deltas): `273 built/31 reused/7680 deferred/31 idle/95 owned/10 adopted/285 baked/45 bake sizes/1678 KB bake scratch/96 grown/12 trimmed/183 fitted/280 waited/3.3 ms build/0 fast merges/285 native merges/0 failed merges`.

Fast/native/failed merges: 0 / 285 / 0. **BodyMerge patch is inert in this run: fast merges=0.** Last error: none in the exported diagnostic.

**gc-night**

| Metric | Value |
| --- | --- |
| residents | 1000 |
| visitors | 2000 |
| persons | 3000 |
| frames | 14253 |
| alloc_counter | "boehm_total" |
| gen0 | 5 |
| gen1 | 0 |
| gen2 | 0 |
| pause_ms | {"count":5,"p50":33.469,"p95":102.848,"max":102.848} |
| heap_start_bytes | 1417809920 |
| heap_end_bytes | 1517711360 |
| heap_peak_bytes | 1561313280 |
| mono_heap_bytes | 1575092224 |
| mono_used_bytes | 1517711360 |
| counter_frame_bytes | 1358912480 |
| mod_alloc_bytes | 124296728 |
| physical_alloc_bytes | 21542280 |
| advance_alloc_bytes | 29176192 |

Look diagnostics, last periodic cumulative snapshot (not exact window deltas): `94 built/95 reused/1377 deferred/32 idle/32 owned/111 adopted/552 baked/45 bake sizes/1678 KB bake scratch/2 grown/81 trimmed/0 fitted/96 waited/21.0 ms build/0 fast merges/552 native merges/0 failed merges`.

Fast/native/failed merges: 0 / 552 / 0. **BodyMerge patch is inert in this run: fast merges=0.** Last error: none in the exported diagnostic.

| Window versus 15d | gen0 | pause p95 ms | mod MB | physical MB | advance MB | spawn_appearance MB | routes nodes / rebuild MB |
| --- | --- | --- | --- | --- | --- | --- | --- |
| gc-day | 6 | 674.16 | 139.858 | 45.71 | 74.047 | 33.456 | 3.161 / 3.707 |
| gc-night | 5 | 45.63 | 75.72 | 22.489 | 34.783 | 14.149 | 0.433 / 3.816 |

| Window versus 15f | gen0 | pause p95 ms | mod MB | physical MB | advance MB | spawn_appearance MB | routes nodes / rebuild MB |
| --- | --- | --- | --- | --- | --- | --- | --- |
| gc-day | 7 | 31.829 | 267.436 | 64.904 | 99.256 | 50.596 | 3.472 / 83.875 |
| gc-night | 7 | 100.85 | 129.231 | 22.449 | 29.047 | 14.404 | 3.472 / 58.653 |

Prior day/night comparisons: 15a gen0 8/6, pause p95 27.888/37.301 ms, mod 295.6/151.5 MB; 15b gen0 7/6, pause p95 43.403/38.929 ms, mod 177.9/78.7 MB; 15c gen0 6/4, pause p95 110.57/4651.381 ms (host-load artefact). 15d day mod 139.9 MB/gen0 6, night mod 75.7 MB/gen0 5/pause p95 45.6 ms. 15f at cbd4e68e excludes #282: day gen0 7/mod 267.4 MB/pause p95 31.8 ms/spawn_appearance 50.6 MB/routes rebuild 83.9 MB; night gen0 7/mod 129.2 MB/pause p95 100.9 ms/routes rebuild 58.7 MB. Shared-host differences cannot establish a controlled A/B improvement or regression.

Every exported segment and sub-segment below: bytes / samples / skipped. Children overlap parents and must not be added again.

| Segment | 15d day | 15d night | 15f day | 15f night | 15g day | 15g night |
| --- | --- | --- | --- | --- | --- | --- |
| frame | 802836480 / None / None | 806047744 / None / None | 1115439104 / None / None | 877658112 / None / None | 1000472576 / None / None | 821596160 / None / None |
| mod | 139857600 / 15348 / 0 | 75719880 / 17000 / 0 | 267435784 / 16610 / 0 | 129231464 / 16414 / 0 | 214783392 / 16298 / 0 | 124296728 / 14254 / 0 |
| advance | 74046960 / 15335 / 0 | 34783168 / 16996 / 0 | 99256352 / 16578 / 0 | 29047280 / 16359 / 0 | 91585432 / 16262 / 0 | 29176192 / 14217 / 0 |
| physical | 45710112 / 15349 / 0 | 22488520 / 17001 / 0 | 64903696 / 16594 / 0 | 22449384 / 16376 / 0 | 56495024 / 16279 / 0 | 21542280 / 14232 / 0 |
| physical_ambient | 1966288 / 15335 / 0 | 2186960 / 16996 / 0 | 2127312 / 16578 / 0 | 2105264 / 16359 / 0 | 2084720 / 16262 / 0 | 1821792 / 14217 / 0 |
| physical_vehicles | 877120 / 15335 / 0 | 433344 / 16996 / 0 | 1314288 / 16578 / 0 | 382576 / 16359 / 0 | 841056 / 16262 / 0 | 456544 / 14217 / 0 |
| physical_bindings | 11408 / 336913 / 0 | 1520 / 197071 / 0 | 16640 / 511071 / 0 | 11744 / 296870 / 0 | 12960 / 448573 / 0 | 11056 / 285962 / 0 |
| physical_vendor_meals | 0 / 15335 / 0 | 656 / 16996 / 0 | 0 / 16578 / 0 | 80 / 16359 / 0 | 0 / 16262 / 0 | 0 / 14217 / 0 |
| physical_fade | 0 / 15335 / 0 | 32 / 16996 / 0 | 0 / 16578 / 0 | 0 / 16359 / 0 | 0 / 16262 / 0 | 0 / 14217 / 0 |
| physical_focus | 0 / 15335 / 0 | 2192 / 16996 / 0 | 496 / 16578 / 0 | 112 / 16359 / 0 | 64 / 16262 / 0 | 272 / 14217 / 0 |
| physical_observe | 388432 / 321350 / 0 | 277744 / 179903 / 0 | 289472 / 494148 / 0 | 99136 / 280309 / 0 | 213328 / 432036 / 0 | 118144 / 271540 / 0 |
| physical_release | 5408 / 244 / 0 | 1584 / 234 / 0 | 6736 / 360 / 0 | 1648 / 284 / 0 | 6032 / 287 / 0 | 1648 / 286 / 0 |
| physical_parked | 1797232 / 1190 / 0 | 1605056 / 1193 / 0 | 1896784 / 1206 / 0 | 1621536 / 1193 / 0 | 1848160 / 1189 / 0 | 1597424 / 1211 / 0 |
| physical_index | 80 / 2130 / 0 | 80 / 2208 / 0 | 80 / 2201 / 0 | 80 / 2176 / 0 | 80 / 2161 / 0 | 80 / 2090 / 0 |
| physical_queues | 9024 / 2130 / 0 | 17456 / 2208 / 0 | 8960 / 2201 / 0 | 17200 / 2176 / 0 | 8960 / 2161 / 0 | 17200 / 2090 / 0 |
| physical_roster | 126832 / 2130 / 0 | 29392 / 2208 / 0 | 15024 / 2201 / 0 | 29872 / 2176 / 0 | 15024 / 2161 / 0 | 29424 / 2090 / 0 |
| physical_selection | 1289176 / 2591 / 0 | 553864 / 2386 / 0 | 1663264 / 2873 / 0 | 612160 / 2398 / 0 | 1446464 / 2742 / 0 | 655648 / 2313 / 0 |
| physical_spawn_pedestrian | 2682384 / 599 / 0 | 63560 / 250 / 0 | 3669432 / 920 / 0 | 77536 / 294 / 0 | 3630648 / 780 / 0 | 124048 / 292 / 0 |
| physical_spawn_appearance | 33456448 / 624 / 0 | 14148608 / 291 / 0 | 50596048 / 963 / 0 | 14403584 / 291 / 0 | 43273584 / 822 / 0 | 13931472 / 282 / 0 |
| physical_spawn_check | 0 / 416 / 0 | 0 / 194 / 0 | 0 / 642 / 0 | 0 / 194 / 0 | 0 / 548 / 0 | 0 / 188 / 0 |
| physical_spawn_vehicle | 233248 / 70 / 0 | 41024 / 25 / 0 | 246496 / 73 / 0 | 49248 / 25 / 0 | 103472 / 75 / 0 | 54144 / 25 / 0 |
| physical_unattributed | 2867032 / 30686 / 0 | 3125448 / 33999 / 0 | 3052664 / 33174 / 0 | 3037608 / 32737 / 0 | 3010472 / 32543 / 0 | 2723384 / 28451 / 0 |
| physical_spawn_randomize | 1869408 / 208 / 0 | 845360 / 97 / 0 | 2866432 / 321 / 0 | 850768 / 97 / 0 | 2451872 / 274 / 0 | 824512 / 94 / 0 |
| physical_spawn_bake | 2127792 / 208 / 0 | 432 / 97 / 0 | 2172016 / 321 / 0 | 0 / 97 / 0 | 2080768 / 274 / 0 | 0 / 94 / 0 |
| advance_planning | 38375672 / 9371 / 0 | 23007504 / 6608 / 0 | 46507952 / 9631 / 0 | 18856688 / 3971 / 0 | 45275472 / 9394 / 0 | 19272960 / 4418 / 0 |
| advance_events | 33463048 / 18966 / 0 | 9324272 / 20782 / 0 | 50360944 / 22307 / 0 | 7832288 / 18927 / 0 | 43912280 / 21072 / 0 | 7855840 / 16270 / 0 |
| advance_stops | 2208240 / 15335 / 0 | 2449712 / 16996 / 0 | 2387456 / 16578 / 0 | 2356896 / 16359 / 0 | 2341728 / 16262 / 0 | 2047392 / 14217 / 0 |
| advance_other | 0 / 15335 / 0 | 1104 / 16996 / 0 | 0 / 16578 / 0 | 1408 / 16359 / 0 | 48 / 16262 / 0 | 0 / 14217 / 0 |
| physical_spawn_grow | 804608 / 155 / 0 | 0 / 0 / 0 | 487040 / 224 / 0 | 0 / 0 / 0 | 1013152 / 195 / 0 | 4064 / 4 / 0 |
| mod_prelude | 2331440 / 15348 / 0 | 2587392 / 17000 / 0 | 2477024 / 16610 / 0 | 2450080 / 16414 / 0 | 2430864 / 16298 / 0 | 2137520 / 14254 / 0 |
| mod_rlo | 3530176 / 15348 / 0 | 3541104 / 17000 / 0 | 3531744 / 16610 / 0 | 3517840 / 16414 / 0 | 3529696 / 16298 / 0 | 3535968 / 14254 / 0 |
| mod_food | 1278384 / 15348 / 0 | 1277760 / 17000 / 0 | 1277184 / 16610 / 0 | 851520 / 16414 / 0 | 1277184 / 16298 / 0 | 851456 / 14254 / 0 |
| mod_optional_stops | 2232912 / 15348 / 0 | 2454032 / 17000 / 0 | 2392832 / 16610 / 0 | 2370704 / 16414 / 0 | 2347664 / 16298 / 0 | 2052576 / 14254 / 0 |
| mod_routes | 7669584 / 15348 / 0 | 5283984 / 17000 / 0 | 90395528 / 16610 / 0 | 65149616 / 16414 / 0 | 53933880 / 16298 / 0 | 61878048 / 14254 / 0 |
| mod_closures | 388864 / 15335 / 0 | 492544 / 16996 / 0 | 388416 / 16578 / 0 | 674816 / 16359 / 0 | 389856 / 16262 / 0 | 696720 / 14217 / 0 |
| mod_hold_replay | 0 / 15335 / 0 | 224 / 16996 / 0 | 0 / 16578 / 0 | 0 / 16359 / 0 | 55904 / 16262 / 0 | 0 / 14217 / 0 |
| mod_physical_setup | 0 / 14576 / 0 | 0 / 13926 / 0 | 0 / 16253 / 0 | 0 / 15626 / 0 | 0 / 15496 / 0 | 0 / 12401 / 0 |
| mod_inspector | 2296736 / 15335 / 0 | 2447552 / 16996 / 0 | 2432896 / 16578 / 0 | 2355696 / 16359 / 0 | 2391056 / 16262 / 0 | 2047472 / 14217 / 0 |
| mod_summary | 364032 / 15311 / 0 | 362832 / 16954 / 0 | 365584 / 16456 / 0 | 364336 / 16114 / 0 | 381056 / 16154 / 0 | 378384 / 13911 / 0 |
| mod_other | 2720 / 15348 / 0 | 0 / 17000 / 0 | 3456 / 16610 / 0 | 144 / 16414 / 0 | 6032 / 16298 / 0 | 112 / 14254 / 0 |
| mod_routes_speeds | 801472 / 12523 / 0 | 1034064 / 15874 / 0 | 915088 / 14279 / 0 | 968320 / 15056 / 0 | 875664 / 13655 / 0 | 782672 / 12213 / 0 |
| mod_routes_nodes | 3160720 / 12523 / 0 | 432832 / 15874 / 0 | 3472216 / 14279 / 0 | 3472264 / 15056 / 0 | 3368696 / 13655 / 0 | 3368600 / 12213 / 0 |
| mod_routes_sweep | 0 / 15335 / 0 | 80 / 16996 / 0 | 32 / 16571 / 0 | 256 / 16354 / 0 | 0 / 16260 / 0 | 432 / 14213 / 0 |
| mod_routes_rebuild | 3706896 / 15348 / 0 | 3816400 / 17000 / 0 | 83875472 / 16610 / 0 | 58653224 / 16414 / 0 | 49685872 / 16298 / 0 | 57722536 / 14254 / 0 |
| mod_closures_network | 229536 / 15335 / 0 | 331776 / 16996 / 0 | 229088 / 16578 / 0 | 511408 / 16359 / 0 | 229216 / 16262 / 0 | 533040 / 14217 / 0 |
| mod_closures_links | 159328 / 15335 / 0 | 159936 / 16996 / 0 | 159328 / 16578 / 0 | 159680 / 16359 / 0 | 160640 / 16262 / 0 | 159760 / 14217 / 0 |
| advance_events_traffic | 20672 / 1479 / 0 | 46688 / 850 / 0 | 288496 / 2888 / 0 | 259584 / 1954 / 0 | 288416 / 2694 / 0 | 259712 / 2041 / 0 |
| advance_events_food | 20541984 / 788 / 0 | 3878496 / 410 / 0 | 32566512 / 1124 / 0 | 3894880 / 320 / 0 | 27859008 / 985 / 0 | 3878448 / 320 / 0 |
| advance_events_citizen | 7811560 / 3010 / 0 | 1912032 / 1917 / 0 | 12345536 / 4080 / 0 | 2071696 / 2156 / 0 | 10779448 / 3725 / 0 | 2084432 / 2105 / 0 |
| advance_events_route_begin | 5088832 / 3489 / 0 | 3485280 / 3108 / 0 | 5160336 / 3546 / 0 | 1604560 / 1522 / 0 | 4985408 / 3379 / 0 | 1633200 / 1539 / 0 |
| advance_events_other | 0 / 10200 / 0 | 1776 / 14497 / 0 | 64 / 10669 / 0 | 1568 / 12975 / 0 | 0 / 10289 / 0 | 48 / 10265 / 0 |
| advance_planning_slice | 24722968 / 5883 / 0 | 17071472 / 3500 / 0 | 33180312 / 6084 / 0 | 12512064 / 2449 / 0 | 30306680 / 6015 / 0 | 12454208 / 2879 / 0 |
| advance_planning_finish | 13652704 / 3488 / 0 | 5936032 / 3108 / 0 | 13327640 / 3547 / 0 | 6344624 / 1522 / 0 | 14968792 / 3379 / 0 | 6818752 / 1539 / 0 |
| physical_setup_places | 273992 / 1 / 0 | 273992 / 1 / 0 | 273992 / 1 / 0 | 273992 / 1 / 0 | 273992 / 1 / 0 | 273992 / 1 / 0 |
| physical_setup_bodies | 180032 / 1 / 0 | 206128 / 1 / 0 | 180032 / 1 / 0 | 206128 / 1 / 0 | 181408 / 1 / 0 | 206128 / 1 / 0 |
| physical_setup_vehicles | 15984 / 2 / 0 | 15904 / 2 / 0 | 15984 / 2 / 0 | 15904 / 2 / 0 | 15984 / 2 / 0 | 15904 / 2 / 0 |
| physical_setup_ambient | 173776 / 2 / 0 | 174192 / 2 / 0 | 173776 / 2 / 0 | 173776 / 2 / 0 | 173776 / 2 / 0 | 173776 / 2 / 0 |
| physical_setup_preflight | 2000 / 1 / 0 | 752 / 1 / 0 | 800 / 1 / 0 | 752 / 1 / 0 | 800 / 1 / 0 | 752 / 1 / 0 |
| physical_unattributed_setup | 3568 / 1 / 0 | 4240 / 1 / 0 | 3568 / 1 / 0 | 3568 / 1 / 0 | 3568 / 1 / 0 | 3568 / 1 / 0 |
| physical_unattributed_update | 7568 / 15335 / 0 | 624 / 16996 / 0 | 14144 / 16578 / 0 | 4144 / 16359 / 0 | 16880 / 16262 / 0 | 0 / 14217 / 0 |
| physical_unattributed_waiting | 1872 / 13 / 0 | 576 / 4 / 0 | 2672 / 15 / 0 | 2304 / 16 / 0 | 2304 / 16 / 0 | 2016 / 14 / 0 |
| physical_unattributed_entry | 2208240 / 15335 / 0 | 2448912 / 16996 / 0 | 2387552 / 16578 / 0 | 2357040 / 16359 / 0 | 2341760 / 16262 / 0 | 2047248 / 14217 / 0 |

Every exported PC phase duration (ms), separate from allocation bytes:

| Phase | day ms | night ms |
| --- | --- | --- |
| mod | 0 | 0 |
| advance | 0 | 0 |
| physical | 0 | 0 |
| physical_ambient | 239.867 | 234.438 |
| physical_vehicles | 352.55 | 306.122 |
| physical_bindings | 307.793 | 240.22 |
| physical_vendor_meals | 6.582 | 6.44 |
| physical_fade | 5.044 | 4.234 |
| physical_focus | 67.705 | 70.93 |
| physical_observe | 2853.859 | 2245.952 |
| physical_release | 65.324 | 376.872 |
| physical_parked | 729.684 | 1310.739 |
| physical_index | 18.444 | 98.308 |
| physical_queues | 87.445 | 243.17 |
| physical_roster | 435.964 | 789.68 |
| physical_selection | 73.357 | 110.88 |
| physical_spawn_pedestrian | 480.953 | 450.577 |
| physical_spawn_appearance | 519.556 | 482.227 |
| physical_spawn_check | 1.057 | 2.595 |
| physical_spawn_vehicle | 24.056 | 35.206 |
| physical_unattributed | 106.199 | 100.119 |
| physical_spawn_randomize | 14.098 | 23.462 |
| physical_spawn_bake | 159.608 | 73.713 |
| advance_planning | 5941.288 | 4301.714 |
| advance_events | 752.357 | 669.429 |
| advance_stops | 46.256 | 69.054 |
| advance_other | 124.041 | 130.126 |
| physical_spawn_grow | 142.602 | 3.974 |
| mod_prelude | 1580.773 | 2025.93 |
| mod_rlo | 65.301 | 126.946 |
| mod_food | 31.591 | 31.857 |
| mod_optional_stops | 1110.574 | 137.491 |
| mod_routes | 875.827 | 1013.127 |
| mod_closures | 1355.46 | 1346.667 |
| mod_hold_replay | 10.63 | 9.896 |
| mod_physical_setup | 2.812 | 2.24 |
| mod_inspector | 100.709 | 117.662 |
| mod_summary | 22.743 | 50.203 |
| mod_other | 10.928 | 10.363 |
| mod_routes_speeds | 229.828 | 256.928 |
| mod_routes_nodes | 40.09 | 22.08 |
| mod_routes_sweep | 366.691 | 461.206 |
| mod_routes_rebuild | 222.229 | 257.511 |
| mod_closures_network | 123.633 | 127.916 |
| mod_closures_links | 1208.263 | 1182.196 |
| advance_events_traffic | 42.521 | 82.72 |
| advance_events_food | 177.94 | 32.318 |
| advance_events_citizen | 278.66 | 382.262 |
| advance_events_route_begin | 215.64 | 131.407 |
| advance_events_other | 35.322 | 37.489 |
| advance_planning_slice | 4855.96 | 3552.399 |
| advance_planning_finish | 1083.95 | 747.886 |
| physical_setup_places | 2.103 | 1.588 |
| physical_setup_bodies | 1.157 | 0.742 |
| physical_setup_vehicles | 4.744 | 1.693 |
| physical_setup_ambient | 2.866 | 4.897 |
| physical_setup_preflight | 0.369 | 0.273 |
| physical_unattributed_setup | 0.127 | 0.238 |
| physical_unattributed_update | 42.101 | 37.148 |
| physical_unattributed_waiting | 1.511 | 0.056 |
| physical_unattributed_entry | 42.683 | 45.676 |

Leaf allocation per call, for comparison with 15d; call counts are exported allocation samples:

| Leaf | 15d day / night B per call | 15g day / night B per call |
| --- | --- | --- |
| advance_events_food | 26068.5 / 9459.7 | 28283.3 / 12120.1 |
| advance_events_citizen | 2595.2 / 997.4 | 2893.8 / 990.2 |
| advance_events_route_begin | 1458.5 / 1121.4 | 1475.4 / 1061.2 |
| advance_planning_finish | 3914.2 / 1909.9 | 4429.9 / 4430.6 |

### Mod-work, whole-frame timing and every retained spike

Each controller holds 600 s; exported recorder windows include command-boundary overhead. Whole-frame intervals and GC marker durations remain separate from the mod-work union.

| Window | seconds / complete frames | UpdateCity key | self / inclusive p95 ms | union p95 / max ms | union >=30 ms | whole-frame p95 / max ms |
| --- | --- | --- | --- | --- | --- | --- |
| gc-day | 600.817 / 16303 | updatecity | 0.321 / 3.4944 | 3.5479 / 79.8044 | 3 | 45.1551 / 1107.5208 |
| gc-night | 600.585 / 14261 | updatecity | 0.5716 / 3.8049 | 3.8674 / 65.7413 | 12 | 72.9781 / 1487.8044 |
| street | 600.751 / 3902 | updatecity | 6.9194 / 6.9194 | 6.9628 / 268.2268 | 25 | 282.9525 / 1857.7632 |

15f street reference: UpdateCity self/inclusive p95 4.09/4.14 ms, 3 union frames >=30 ms (two mod_routes_rebuild). 15a self/inclusive 6.5466/6.6550 ms (37 spikes), 15b 6.80/6.96 ms (28), 15c 16.81/17.22 ms (114); shared-host differences are provisional.

**gc-day attribution.** Self excludes nested timed scopes; inclusive includes them; union counts nested scopes once.

| Actual component key | calls | exclusive p95 / max / total ms | inclusive p95 / max / total ms |
| --- | --- | --- | --- |
| advance | 16267 | 2.6092 / 8.6827 / 6819.6613 | 2.6092 / 8.6827 / 6819.6613 |
| PopulationRuntime.LateUpdate | 16304 | 0.0029 / 1.4315 / 31.8087 | 0.023 / 3.6132 / 308.5392 |
| PopulationRuntime.Update | 16304 | 0.0085 / 1.4938 / 116.9217 | 3.501 / 79.7503 / 18483.8932 |
| inspector | 16267 | 0.0062 / 0.073 / 76.9278 | 0.0062 / 0.073 / 76.9278 |
| AmbientPedestrians.ChangeStatePrefix | 1 | 0.0 / 0.0025 / 0.0025 | 0.0 / 0.0025 / 0.0025 |
| AmbientPedestrians.EndCyclePrefix | 1 | 0.0 / 0.0045 / 0.0045 | 0.0 / 0.0045 / 0.0045 |
| AmbientPedestrians.SetActivePrefix | 3 | 0.0 / 0.0264 / 0.0348 | 0.0 / 0.0264 / 0.0348 |
| BodyMerge.MergePrefix | 278 | 0.0 / 0.0495 / 0.7489 | 0.0 / 0.0495 / 0.7489 |
| NativeCityClock.AccountingPrefix | 16304 | 0.0135 / 0.6749 / 190.949 | 0.0135 / 0.6749 / 190.949 |
| NativeCityClock.RidePostfix | 16304 | 0.0007 / 0.0049 / 9.8006 | 0.0007 / 0.0049 / 9.8006 |
| NativeCityClock.RidePrefix | 16304 | 0.0007 / 0.0319 / 9.9565 | 0.0007 / 0.0319 / 9.9565 |
| NativeCityClock.TickPrefix | 16304 | 0.0011 / 0.0516 / 15.6585 | 0.0011 / 0.0516 / 15.6585 |
| NativeFoodOwnership.AfterNativeTick | 16304 | 0.001 / 0.6952 / 14.5338 | 0.001 / 0.6952 / 14.5338 |
| NativeTravelPace.ObserveTickPostfix | 16304 | 0.0018 / 0.0374 / 25.9037 | 0.0018 / 0.0374 / 25.9037 |
| ResidentInspector.CameraInputPostfix | 32608 | 0.0008 / 0.0276 / 10.614 | 0.0008 / 0.0276 / 10.614 |
| ResidentInspector.MouseRunPrefix | 16304 | 0.0017 / 0.0608 / 24.6496 | 0.0017 / 0.0608 / 24.6496 |
| ResidentInspector.UiRaycastPrefix | 570640 | 0.009 / 6.481 / 131.9708 | 0.009 / 6.481 / 131.9708 |
| physical | 48854 | 0.9049 / 40.2761 / 6646.1816 | 0.9049 / 40.3256 / 6646.9238 |
| prepare | 16283 | 0.0265 / 5.6981 / 717.4218 | 0.0265 / 5.6981 / 717.4218 |
| PhysicalPedestrianCrosswalk.OnTriggerEnter | 1837 | 0.0036 / 0.1271 / 9.0738 | 0.0036 / 0.1271 / 9.0738 |
| PhysicalPedestrianCrosswalk.OnTriggerExit | 1223 | 0.0 / 2.6305 / 9.051 | 0.0 / 2.6305 / 9.051 |
| updatecity | 16304 | 0.321 / 55.4043 / 4382.7655 | 3.4944 / 79.7028 / 18366.9715 |

| Recorder | counted | retained | truncated | cap |
| --- | --- | --- | --- | --- |
| rig mod-work #94 | 3 | 3 | False | 1024 |
| rig whole-frame #94 | 16022 | 1024 | True | 1024 |
| PC phase recorder | 3 | 3 | False | 1024 |
| rig GC pause markers | 15 | 15 | False | 4096 |

Each union >=30 ms row below is joined by exact unity_frame to PC phase attribution and the independent whole-frame sample. Missing phase attribution remains unavailable. Whole-frame >=30 ms counts and row retention are separate; whole-frame row truncation does not imply lost mod-work rows.

| unity_frame | union ms | phase / ms | detail / ms | sub / ms | gc_phase | collections | whole-frame ms | dominant component |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 4311 | 79.8044 | physical_spawn_pedestrian/33.973 | physical_spawn_grow/26.914 | mod_closures_network/10.854 | none | 0 | 96.3803 | updatecity |
| 5054 | 49.3558 | physical_spawn_pedestrian/15.755 | physical_spawn_bake/2.98 | mod_closures_links/1.743 | none | 0 | 165.8152 | physical |
| 5280 | 33.1385 | physical_spawn_pedestrian/26.54 | physical_spawn_grow/26.515 | mod_routes_sweep/4.283 | none | 0 | 107.1627 | physical |

GC pauses keyed by unity_frame; marker duration source: Unity.Profiling.ProfilerRecorder:GC.Collect.

| unity_frame | GC.Collect marker duration ms | collections | generation |
| --- | --- | --- | --- |
| 5387 | 4.4809 | 0 | None |
| 5399 | 25.104 | 1 | 0 |
| 8030 | 2.7442 | 0 | None |
| 8164 | 2.4298 | 0 | None |
| 8175 | 24.257 | 1 | 0 |
| 10469 | 3.009 | 0 | None |
| 10491 | 24.7543 | 1 | 0 |
| 12720 | 3.0106 | 0 | None |
| 12728 | 22.7026 | 1 | 0 |
| 15037 | 3.0105 | 0 | None |
| 15047 | 23.5185 | 1 | 0 |
| 17265 | 3.0032 | 0 | None |
| 17301 | 24.5049 | 1 | 0 |
| 19522 | 3.0089 | 0 | None |
| 19545 | 24.2471 | 1 | 0 |

Coverage complete=True; validity={"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}. Union of the mod assembly's Unity messages (Update/LateUpdate/FixedUpdate/OnGUI, Start/OnDestroy, OnTrigger*/OnCollision* and the rest), its Harmony prefixes/postfixes/finalizers, the mod methods its transpilers inject into native IL, GlobalEvents and SceneManager subscribers and IModBigAmbitions methods, timed in place. Coroutines, worker threads and callbacks registered with other native events are not timed. Observer overhead of nested scopes stays inside the union.

| Autosave before / after | native-saving frames | save activity |
| --- | --- | --- |
| True/True | 0 | {"attempts":12181,"allowed":0,"refused":12181,"native_saving_frames":0,"clean":true,"invalid_reasons":[]} |

**gc-night attribution.** Self excludes nested timed scopes; inclusive includes them; union counts nested scopes once.

| Actual component key | calls | exclusive p95 / max / total ms | inclusive p95 / max / total ms |
| --- | --- | --- | --- |
| advance | 14222 | 2.1391 / 18.8188 / 5123.9759 | 2.1391 / 18.8188 / 5123.9759 |
| PopulationRuntime.LateUpdate | 14262 | 0.004 / 0.625 / 40.6857 | 0.0284 / 1.1271 / 314.3878 |
| PopulationRuntime.Update | 14262 | 0.011 / 1.2007 / 122.1179 | 3.8154 / 64.9064 / 17236.255 |
| inspector | 14222 | 0.0094 / 1.5081 / 89.9273 | 0.0094 / 1.5081 / 89.9273 |
| AmbientPedestrians.SetActivePrefix | 2 | 0.0 / 0.013 / 0.0249 | 0.0 / 0.013 / 0.0249 |
| BodyMerge.MergePrefix | 95 | 0.0 / 0.1175 / 1.3209 | 0.0 / 0.1175 / 1.3209 |
| NativeCityClock.AccountingPrefix | 14262 | 0.0158 / 2.0387 / 201.5526 | 0.0158 / 2.0387 / 201.5526 |
| NativeCityClock.RidePostfix | 14262 | 0.0006 / 0.0423 / 7.6457 | 0.0006 / 0.0423 / 7.6457 |
| NativeCityClock.RidePrefix | 14262 | 0.0006 / 0.0328 / 7.6229 | 0.0006 / 0.0328 / 7.6229 |
| NativeCityClock.TickPrefix | 14262 | 0.0011 / 0.1996 / 13.3976 | 0.0011 / 0.1996 / 13.3976 |
| NativeFoodOwnership.AfterNativeTick | 14262 | 0.001 / 0.3647 / 13.341 | 0.001 / 0.3647 / 13.341 |
| NativeTravelPace.ObserveTickPostfix | 14262 | 0.002 / 0.1038 / 24.3989 | 0.002 / 0.1038 / 24.3989 |
| ResidentInspector.CameraInputPostfix | 28524 | 0.0012 / 0.2339 / 10.5199 | 0.0012 / 0.2339 / 10.5199 |
| ResidentInspector.MouseRunPrefix | 14262 | 0.0022 / 0.2117 / 26.2984 | 0.0022 / 0.2117 / 26.2984 |
| ResidentInspector.UiRaycastPrefix | 499170 | 0.0079 / 0.2406 / 101.4415 | 0.0079 / 0.2406 / 101.4415 |
| physical | 42720 | 1.5564 / 57.2092 / 7405.2484 | 1.5564 / 57.2385 / 7406.5623 |
| prepare | 14236 | 0.1102 / 7.8724 / 840.3795 | 0.1102 / 7.8724 / 840.3795 |
| PhysicalPedestrianCrosswalk.OnTriggerEnter | 1059 | 0.0 / 1.5063 / 14.0879 | 0.0 / 1.5063 / 14.0879 |
| PhysicalPedestrianCrosswalk.OnTriggerExit | 698 | 0.0 / 2.1239 / 11.8078 | 0.0 / 2.1239 / 11.8078 |
| updatecity | 14262 | 0.5716 / 38.16 / 3926.9913 | 3.8049 / 64.805 / 17114.1371 |

| Recorder | counted | retained | truncated | cap |
| --- | --- | --- | --- | --- |
| rig mod-work #94 | 12 | 12 | False | 1024 |
| rig whole-frame #94 | 13008 | 1024 | True | 1024 |
| PC phase recorder | 12 | 12 | False | 1024 |
| rig GC pause markers | 10 | 10 | False | 4096 |

Each union >=30 ms row below is joined by exact unity_frame to PC phase attribution and the independent whole-frame sample. Missing phase attribution remains unavailable. Whole-frame >=30 ms counts and row retention are separate; whole-frame row truncation does not imply lost mod-work rows.

| unity_frame | union ms | phase / ms | detail / ms | sub / ms | gc_phase | collections | whole-frame ms | dominant component |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 26725 | 53.7783 | mod_closures/24.442 | physical_spawn_bake/0.63 | mod_closures_network/23.046 | none | 0 | 95.0097 | updatecity |
| 28337 | 33.6139 | physical_spawn_appearance/6.562 | physical_spawn_bake/0.661 | mod_closures_links/4.621 | none | 0 | 100.3096 | physical |
| 28839 | 65.7413 | physical_parked/18.732 | physical_spawn_randomize/1.16 | mod_closures_links/5.558 | none | 0 | 250.7787 | physical |
| 28986 | 42.6645 | physical_roster/14.364 | none/0 | mod_closures_links/5.19 | none | 0 | 124.3597 | physical |
| 29143 | 62.9031 | physical_spawn_appearance/16.342 | physical_spawn_bake/1.631 | mod_closures_links/2.286 | none | 0 | 209.9378 | physical |
| 29310 | 30.8466 | mod_rlo/26.722 | none/0 | advance_events_citizen/0.95 | none | 0 | 98.377 | updatecity |
| 29586 | 58.7688 | physical_parked/22.593 | none/0 | mod_closures_links/7.044 | none | 0 | 427.3941 | physical |
| 29727 | 36.0504 | physical_roster/12.33 | none/0 | mod_closures_links/5.552 | none | 0 | 265.7413 | physical |
| 29854 | 55.445 | physical_parked/14.017 | none/0 | mod_closures_links/6.13 | none | 0 | 308.8178 | physical |
| 30120 | 45.7158 | physical_parked/16.636 | none/0 | mod_closures_links/6.431 | none | 0 | 406.1889 | physical |
| 30151 | 38.8753 | physical_spawn_appearance/26.214 | physical_spawn_bake/1.759 | advance_planning_finish/0.706 | none | 0 | 100.9541 | physical |
| 30368 | 42.2518 | physical_parked/14.084 | none/0 | advance_planning_slice/2.627 | none | 0 | 243.6116 | physical |

GC pauses keyed by unity_frame; marker duration source: Unity.Profiling.ProfilerRecorder:GC.Collect.

| unity_frame | GC.Collect marker duration ms | collections | generation |
| --- | --- | --- | --- |
| 27325 | 3.1172 | 0 | None |
| 27392 | 27.6282 | 1 | 0 |
| 29941 | 2.7203 | 0 | None |
| 30033 | 33.3123 | 1 | 0 |
| 32767 | 3.0284 | 0 | None |
| 32812 | 25.3171 | 1 | 0 |
| 35665 | 4.8104 | 0 | None |
| 35696 | 26.622 | 1 | 0 |
| 38507 | 11.0025 | 0 | None |
| 38570 | 24.8483 | 1 | 0 |

Coverage complete=True; validity={"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}. Union of the mod assembly's Unity messages (Update/LateUpdate/FixedUpdate/OnGUI, Start/OnDestroy, OnTrigger*/OnCollision* and the rest), its Harmony prefixes/postfixes/finalizers, the mod methods its transpilers inject into native IL, GlobalEvents and SceneManager subscribers and IModBigAmbitions methods, timed in place. Coroutines, worker threads and callbacks registered with other native events are not timed. Observer overhead of nested scopes stays inside the union.

| Autosave before / after | native-saving frames | save activity |
| --- | --- | --- |
| True/True | 0 | {"attempts":14281,"allowed":0,"refused":14281,"native_saving_frames":0,"clean":true,"invalid_reasons":[]} |

**street attribution.** Self excludes nested timed scopes; inclusive includes them; union counts nested scopes once.

| Actual component key | calls | exclusive p95 / max / total ms | inclusive p95 / max / total ms |
| --- | --- | --- | --- |
| PopulationRuntime.LateUpdate | 3903 | 0.025 / 0.3653 / 69.0112 | 0.025 / 0.3653 / 69.0112 |
| PopulationRuntime.Update | 3903 | 0.014 / 0.8082 / 38.6108 | 6.9251 / 268.1993 / 10558.0707 |
| AmbientPedestrians.ChangeStatePrefix | 4 | 0.0 / 0.0798 / 0.0906 | 0.0 / 0.0798 / 0.0906 |
| AmbientPedestrians.EndCyclePrefix | 5 | 0.0 / 0.0142 / 0.0318 | 0.0 / 0.0142 / 0.0318 |
| AmbientPedestrians.SetActivePrefix | 3 | 0.0 / 0.0097 / 0.0168 | 0.0 / 0.0097 / 0.0168 |
| BodyMerge.MergePrefix | 94 | 0.0 / 0.0171 / 0.2533 | 0.0 / 0.0171 / 0.2533 |
| NativeCityClock.AccountingPrefix | 3903 | 0.0182 / 1.565 / 82.0691 | 0.0182 / 1.565 / 82.0691 |
| NativeCityClock.RidePostfix | 3903 | 0.0006 / 0.0101 / 1.9574 | 0.0006 / 0.0101 / 1.9574 |
| NativeCityClock.RidePrefix | 3903 | 0.0006 / 0.0022 / 2.027 | 0.0006 / 0.0022 / 2.027 |
| NativeCityClock.TickPrefix | 3903 | 0.0011 / 0.1646 / 5.0252 | 0.0011 / 0.1646 / 5.0252 |
| NativeFoodOwnership.AfterNativeTick | 3903 | 0.001 / 0.1737 / 4.511 | 0.001 / 0.1737 / 4.511 |
| NativeTravelPace.ObserveTickPostfix | 3903 | 0.0019 / 0.1383 / 6.7692 | 0.0019 / 0.1383 / 6.7692 |
| ResidentInspector.CameraInputPostfix | 7806 | 0.001 / 0.0819 / 2.5971 | 0.001 / 0.0819 / 2.5971 |
| ResidentInspector.MouseRunPrefix | 3903 | 0.0023 / 1.5889 / 11.8986 | 0.0023 / 1.5889 / 11.8986 |
| ResidentInspector.UiRaycastPrefix | 136605 | 0.0081 / 1.688 / 31.5903 | 0.0081 / 1.688 / 31.5903 |
| PhysicalPedestrianCrosswalk.OnTriggerEnter | 542 | 0.0041 / 0.0388 / 2.7021 | 0.0041 / 0.0388 / 2.7021 |
| PhysicalPedestrianCrosswalk.OnTriggerExit | 286 | 0.0006 / 0.0277 / 1.2153 | 0.0006 / 0.0277 / 1.2153 |
| updatecity | 3903 | 6.9194 / 268.1631 / 10519.2111 | 6.9194 / 268.1699 / 10519.4599 |

| Recorder | counted | retained | truncated | cap |
| --- | --- | --- | --- | --- |
| rig mod-work #94 | 25 | 25 | False | 1024 |
| rig whole-frame #94 | 3777 | 1024 | True | 1024 |
| PC phase recorder | 25 | 25 | False | 1024 |
| rig GC pause markers | 8 | 8 | False | 4096 |

Each union >=30 ms row below is joined by exact unity_frame to PC phase attribution and the independent whole-frame sample. Missing phase attribution remains unavailable. Whole-frame >=30 ms counts and row retention are separate; whole-frame row truncation does not imply lost mod-work rows.

| unity_frame | union ms | phase / ms | detail / ms | sub / ms | gc_phase | collections | whole-frame ms | dominant component |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 48793 | 34.5628 | mod_closures/7.918 | physical_spawn_bake/0.605 | mod_closures_network/7.83 | none | 0 | 66.611 | updatecity |
| 50504 | 30.4295 | physical_parked/11.993 | none/0 | advance_planning_slice/4.02 | none | 0 | 306.1266 | updatecity |
| 50508 | 39.2309 | physical_parked/20.49 | none/0 | mod_routes_sweep/1.083 | none | 0 | 1111.3851 | updatecity |
| 50562 | 30.8762 | advance_planning/14.329 | none/0 | advance_planning_slice/14.327 | none | 0 | 867.5264 | updatecity |
| 50737 | 30.468 | mod_rlo/27.159 | none/0 | advance_planning_slice/0.291 | none | 0 | 237.7973 | updatecity |
| 50774 | 37.6328 | physical_parked/21.497 | none/0 | advance_planning_slice/1.204 | none | 0 | 399.6171 | updatecity |
| 50839 | 48.0169 | physical_parked/14.453 | none/0 | mod_closures_links/2.25 | none | 0 | 1227.117 | updatecity |
| 50840 | 45.5296 | physical_parked/12.59 | none/0 | mod_closures_links/7.811 | none | 0 | 1392.653 | updatecity |
| 50841 | 33.1826 | physical_parked/9.817 | none/0 | advance_planning_slice/5.332 | none | 0 | 459.2637 | updatecity |
| 50959 | 33.1993 | physical_parked/7.508 | none/0 | advance_planning_slice/1.894 | none | 0 | 860.1547 | updatecity |
| 50963 | 41.19 | physical_parked/7.636 | none/0 | advance_planning_slice/1.943 | none | 0 | 770.6156 | updatecity |
| 50968 | 47.2648 | physical_parked/20.298 | none/0 | mod_closures_links/1.817 | none | 0 | 639.1577 | updatecity |
| 50970 | 30.1845 | mod_rlo/24.675 | none/0 | mod_closures_links/0.316 | none | 0 | 255.2067 | updatecity |
| 50979 | 47.7098 | physical_parked/13.865 | none/0 | advance_planning_slice/1.873 | none | 0 | 1139.1517 | updatecity |
| 50980 | 47.6639 | physical_parked/19.828 | none/0 | advance_planning_slice/3.067 | none | 0 | 1408.1166 | updatecity |
| 50981 | 33.6215 | physical_roster/8.646 | none/0 | mod_routes_sweep/1.012 | none | 0 | 585.4567 | updatecity |
| 51014 | 268.2268 | mod_routes/267.664 | none/0 | mod_routes_rebuild/267.662 | none | 0 | 676.2987 | updatecity |
| 51015 | 103.0925 | mod_routes/94.403 | none/0 | mod_routes_rebuild/94.401 | none | 0 | 831.7611 | updatecity |
| 51068 | 40.7904 | mod_rlo/29.109 | none/0 | mod_routes_rebuild/4.307 | none | 0 | 452.2444 | updatecity |
| 51112 | 33.0359 | mod_rlo/27.223 | none/0 | mod_routes_rebuild/4.741 | none | 0 | 255.1349 | updatecity |
| 51141 | 211.4736 | mod_routes/209.619 | none/0 | mod_routes_rebuild/209.617 | none | 0 | 482.0863 | updatecity |
| 51187 | 261.6755 | mod_closures/78.972 | physical_spawn_bake/5.9 | mod_closures_network/75.794 | none | 0 | 504.5343 | updatecity |
| 51268 | 118.0148 | mod_routes/117.732 | none/0 | mod_routes_rebuild/117.731 | none | 0 | 307.4677 | updatecity |
| 51325 | 42.0428 | mod_routes/41.778 | none/0 | mod_routes_rebuild/41.776 | mod_routes | 1 | 238.4946 | updatecity |
| 51344 | 38.1196 | mod_closures/9.518 | physical_spawn_bake/0.533 | mod_closures_network/9.371 | none | 0 | 189.367 | updatecity |

GC pauses keyed by unity_frame; marker duration source: Unity.Profiling.ProfilerRecorder:GC.Collect.

| unity_frame | GC.Collect marker duration ms | collections | generation |
| --- | --- | --- | --- |
| 48978 | 2.8178 | 0 | None |
| 49024 | 28.024 | 1 | 0 |
| 50704 | 4.5915 | 0 | None |
| 50778 | 37.5108 | 1 | 0 |
| 51286 | 6.6701 | 0 | None |
| 51325 | 29.8853 | 1 | 0 |
| 52406 | 5.8137 | 0 | None |
| 52452 | 29.5482 | 1 | 0 |

Coverage complete=True; validity={"off_main_thread_calls":0,"cross_frame_scopes":0,"unbalanced_exits":0,"depth_overflow":0,"frames_dropped":0,"valid":true,"invalid_reasons":[]}. Union of the mod assembly's Unity messages (Update/LateUpdate/FixedUpdate/OnGUI, Start/OnDestroy, OnTrigger*/OnCollision* and the rest), its Harmony prefixes/postfixes/finalizers, the mod methods its transpilers inject into native IL, GlobalEvents and SceneManager subscribers and IModBigAmbitions methods, timed in place. Coroutines, worker threads and callbacks registered with other native events are not timed. Observer overhead of nested scopes stays inside the union.

| Autosave before / after | native-saving frames | save activity |
| --- | --- | --- |
| True/True | 0 | {"attempts":3915,"allowed":0,"refused":3915,"native_saving_frames":0,"clean":true,"invalid_reasons":[]} |

### Clock retime lines

```text
[Mod:PersistentCity] Clock retime: body release 0.0 ms in the previous update, journey retime 66.7 ms over 17 updates (slowest 5.5 ms, restarts 0), final slice and commit 2.4 ms, simulation rebuild 13.7 ms.
[Mod:PersistentCity] Clock retime: body release 97.1 ms in the previous update, journey retime 70.3 ms over 18 updates (slowest 4.3 ms, restarts 0), final slice and commit 1.4 ms, simulation rebuild 14.7 ms.
[Mod:PersistentCity] Clock retime: body release 201.6 ms in the previous update, journey retime 91.0 ms over 21 updates (slowest 8.1 ms, restarts 0), final slice and commit 1.9 ms, simulation rebuild 22.0 ms.
[Mod:PersistentCity] Clock retime: body release 57.4 ms in the previous update, journey retime 93.4 ms over 23 updates (slowest 5.8 ms, restarts 0), final slice and commit 2.6 ms, simulation rebuild 29.9 ms.
[Mod:PersistentCity] Clock retime: body release 63.6 ms in the previous update, journey retime 106.1 ms over 26 updates (slowest 5.6 ms, restarts 0), final slice and commit 2.3 ms, simulation rebuild 23.9 ms.
[Mod:PersistentCity] Clock retime: body release 99.9 ms in the previous update, journey retime 56.4 ms over 14 updates (slowest 4.1 ms, restarts 0), final slice and commit 2.2 ms, simulation rebuild 18.6 ms.
[Mod:PersistentCity] Clock retime: body release 172.6 ms in the previous update, journey retime 96.5 ms over 24 updates (slowest 4.2 ms, restarts 0), final slice and commit 1.8 ms, simulation rebuild 26.0 ms.
[Mod:PersistentCity] Clock retime: body release 267.7 ms in the previous update, journey retime 590.3 ms over 125 updates (slowest 7.8 ms, restarts 0), final slice and commit 26.3 ms, simulation rebuild 181.8 ms.
[Mod:PersistentCity] Clock retime: body release 117.7 ms in the previous update, journey retime 312.6 ms over 58 updates (slowest 41.8 ms, restarts 0), final slice and commit 1.6 ms, simulation rebuild 24.7 ms.
```

### Shared-host evidence at each performance-window boundary

All measurements are PROVISIONAL (shared Mac). This role ran no build/test during the windows. The candidate headless game was live throughout A. The following session inventory and build cwd attribution come from boundary and per-minute snapshots; unrelated sessions were neither controlled nor modified.

**gc-day start 2026-11-15T14:31:22Z**

```text
15:31  up 1 day, 21:09, 1 user, load averages: 11.42 11.38 9.62
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 52%
  PID  %CPU COMM
41816  91.2 next-server (v16.4.2)
49418  87.2 suggestd
50220  57.9 com.apple.Virtualization.VirtualMachine
41639  55.2 Big Ambitions Agent Tests Mac
41868  39.7 chrome-headless-shell
41867  12.3 chrome-headless-shell
  444   9.6 coreaudiod
```

**gc-day end 2026-11-15T14:41:26Z**

```text
15:41  up 1 day, 21:19, 1 user, load averages: 4.67 5.32 6.94
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 81%
  PID  %CPU COMM
45332 153.3 node
41639  99.8 Big Ambitions Agent Tests Mac
49418  86.9 suggestd
  444  10.5 coreaudiod
  915   6.6 sysmond
  756   2.3 Activity Monitor
  735   2.2 Resolve
```

| Live session | phases observed |
| --- | --- |
| 20261115-144928-ux-wording-ids-1967 | fix, local_tests, review |
| 20261115-151909-mac-fix-gr5-planning-events-rebuild | implement |

| Build/service PID | observed cwd |
| --- | --- |
| 40411 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40412 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40498 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40500 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40531 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40604 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 41696 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 41787 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 41791 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 41797 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 41847 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 41860 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 43015 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 43016 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 43073 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 43074 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 43082 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 43248 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 43250 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 43308 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 43309 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 43364 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 43446 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 43660 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/Compatibility |
| 43664 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/tests |
| 43665 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/Core |
| 45310 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 45332 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |

**gc-night start 2026-11-15T14:45:12Z**

```text
15:45  up 1 day, 21:23, 1 user, load averages: 13.62 8.03 7.62
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 54%
  PID  %CPU COMM
37508 323.6 VBCSCompiler
50220 148.3 com.apple.Virtualization.VirtualMachine
41639  58.4 Big Ambitions Agent Tests Mac
46408  36.0 dotnet
46407  35.2 dotnet
50209  17.5 limactl
50359  10.6 ssh.sock [mux]
```

**gc-night end 2026-11-15T14:55:16Z**

```text
15:55  up 1 day, 21:33, 1 user, load averages: 6.33 11.85 10.90
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 79%
  PID  %CPU COMM
48593 144.1 node
46648 100.0 dotnet
41639  92.1 Big Ambitions Agent Tests Mac
49418  80.4 suggestd
  444  10.2 coreaudiod
  756   6.0 Activity Monitor
  664   3.1 cfprefsd
```

| Live session | phases observed |
| --- | --- |
| 20261115-144928-ux-wording-ids-1967 | fix, local_tests |
| 20261115-151909-mac-fix-gr5-planning-events-rebuild | implement |

| Build/service PID | observed cwd |
| --- | --- |
| 40411 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40412 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40498 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40500 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40531 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40604 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 45926 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 45986 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 45989 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46237 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46238 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46239 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46240 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46257 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46308 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46347 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46384 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46400 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/Compatibility |
| 46407 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/tests |
| 46408 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/Core |
| 46635 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/Compatibility |
| 46639 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/tests |
| 46640 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/Core |
| 46646 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46647 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/tests |
| 46648 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/tests/bin/Debug/net10.0 |
| 46799 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46800 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46801 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46806 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46947 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46948 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46949 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46950 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46951 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 46953 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 47148 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 47151 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 47152 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 47153 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 47154 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 47157 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 47458 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 47739 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 47784 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 47787 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 47791 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 47816 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 47819 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 48572 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 48593 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |

**street start 2026-11-15T15:00:26Z**

```text
16:00  up 1 day, 21:38, 1 user, load averages: 30.26 18.29 13.74
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 63%
  PID  %CPU COMM
49852 150.5 dotnet
52038  63.9 node
52046  56.9 node
51966  55.5 node
50220  53.5 com.apple.Virtualization.VirtualMachine
52035  51.8 node
52001  45.6 node
```

**street end 2026-11-15T15:10:31Z**

```text
16:10  up 1 day, 21:48, 1 user, load averages: 4.76 9.47 12.05
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 81%
  PID  %CPU COMM
57328 136.8 node
41639 106.7 Big Ambitions Agent Tests Mac
49418  76.2 suggestd
  915  17.8 sysmond
  444  10.1 coreaudiod
  756   9.4 Activity Monitor
  384   2.6 mds
```

| Live session | phases observed |
| --- | --- |
| 20261115-144928-ux-wording-ids-1967 | ci, coordinate, fix, local_tests, review |
| 20261115-151909-mac-fix-gr5-planning-events-rebuild | local_tests, review |
| 20261115-155614-rig-comment-ban | ci, fix, implement, local_tests, review |

| Build/service PID | observed cwd |
| --- | --- |
| 40411 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40412 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40498 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40500 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40531 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40604 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 48846 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 48915 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 48919 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 49755 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/Compatibility |
| 49816 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/tests |
| 49817 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/Core |
| 49850 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/tests |
| 49852 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/tests/bin/Debug/net10.0 |
| 51966 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 52035 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 52038 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 52046 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 52082 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 52545 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 52643 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-comment-ban/rig-ops/tests |
| 52644 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-comment-ban/rig-ops/tests |
| 52800 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 52855 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 52858 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 52859 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 52864 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 52865 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 53003 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-comment-ban |
| 53004 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-comment-ban |
| 53053 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-comment-ban |
| 53054 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-comment-ban |
| 53064 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-comment-ban |
| 53098 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-comment-ban |
| 53604 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-comment-ban |
| 53605 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-comment-ban |
| 53653 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-comment-ban |
| 53656 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-comment-ban |
| 53692 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-comment-ban |
| 53764 | ~/Coding_Projects/big-ambitions/big-ambitions-agent-rig-worktrees/rig-comment-ban |
| 55602 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 55605 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 55665 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 55672 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 55720 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 55764 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 56242 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild |
| 56245 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild |
| 56304 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild |
| 56305 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild |
| 56326 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild |
| 56471 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild |
| 56505 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 57314 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |

**refill start 2026-11-15T15:31:00Z**

```text
16:31  up 1 day, 22:09, 1 user, load averages: 28.43 27.75 19.13
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 57%
  PID  %CPU COMM
68955 104.5 Big Ambitions Agent Tests Mac
68661  98.0 dotnet
50220  86.4 com.apple.Virtualization.VirtualMachine
49418  70.2 suggestd
  432  21.9 loginwindow
  444   9.6 coreaudiod
71080   7.5 mdworker_shared
```

**refill end 2026-11-15T15:31:50Z**

```text
16:31  up 1 day, 22:10, 1 user, load averages: 16.85 24.69 18.49
The system has 17179869184 (1048576 pages with a page size of 16384).
System-wide memory free percentage: 35%
  PID  %CPU COMM
71205 137.2 next-server (v16.4.2)
68955 103.4 Big Ambitions Agent Tests Mac
49418  68.5 suggestd
71250  41.2 chrome-headless-shell
71249  11.1 chrome-headless-shell
  444   9.9 coreaudiod
57445   9.1 PerfPowerServices
```

| Live session | phases observed |
| --- | --- |
| 20261115-144928-ux-wording-ids-1967 | local_tests |
| 20261115-151909-mac-fix-gr5-planning-events-rebuild | fix |
| 20261115-162834-rig-md-ban | ci |

| Build/service PID | observed cwd |
| --- | --- |
| 40411 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40412 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40498 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40500 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40531 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 40604 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city |
| 68656 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/tests |
| 68660 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/tests |
| 68661 | ~/Coding_Projects/big-ambitions/big-ambitions-persistent-city-worktrees/fix-gr5-planning-events-rebuild/tests/bin/Debug/net10.0 |
| 71054 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 71175 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 71184 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 71202 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 71244 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |
| 71248 | ~/Coding_Projects/work/MVP_project-worktrees/ux-wording-ids-1967 |

### A4 resident identity and look stability

5 Persistent City resident targets selected through #96 identity and #97 resident reacquire operations. A resident target, rather than a pooled walker handle, is used for every follow and reacquire. donor_id is unexposed by this op.

| Resident id | initial binding | after away | after return | after speed change | after reload at 15:00 | after replay to seen time |
| --- | --- | --- | --- | --- | --- | --- |
| pc:502 | {"source":"persistent_city","resident_id":"pc:502","body_generation":58,"donor_id":null,"donor_reason":"ops_view_walker_donor_unexposed","stable_across_reload":true} | {"source":"persistent_city","resident_id":"pc:502","body_generation":58,"donor_id":null,"donor_reason":"ops_view_walker_donor_unexposed","stable_across_reload":true} | {"source":"persistent_city","resident_id":"pc:502","body_generation":58,"donor_id":null,"donor_reason":"ops_view_walker_donor_unexposed","stable_across_reload":true} | no reply | no reply | no reply |
| pc:552 | {"source":"persistent_city","resident_id":"pc:552","body_generation":75,"donor_id":null,"donor_reason":"ops_view_walker_donor_unexposed","stable_across_reload":true} | {"source":"persistent_city","resident_id":"pc:552","body_generation":75,"donor_id":null,"donor_reason":"ops_view_walker_donor_unexposed","stable_across_reload":true} | {"source":"persistent_city","resident_id":"pc:552","body_generation":75,"donor_id":null,"donor_reason":"ops_view_walker_donor_unexposed","stable_across_reload":true} | no reply | no reply | no reply |
| c121 | {"source":"persistent_city","resident_id":"c121","body_generation":57,"donor_id":null,"donor_reason":"ops_view_walker_donor_unexposed","stable_across_reload":true} | {"source":"persistent_city","resident_id":"c121","body_generation":57,"donor_id":null,"donor_reason":"ops_view_walker_donor_unexposed","stable_across_reload":true} | {"source":"persistent_city","resident_id":"c121","body_generation":57,"donor_id":null,"donor_reason":"ops_view_walker_donor_unexposed","stable_across_reload":true} | no reply | no reply | no reply |
| pc:193 | {"source":"persistent_city","resident_id":"pc:193","body_generation":85,"donor_id":null,"donor_reason":"ops_view_walker_donor_unexposed","stable_across_reload":true} | {"source":"persistent_city","resident_id":"pc:193","body_generation":85,"donor_id":null,"donor_reason":"ops_view_walker_donor_unexposed","stable_across_reload":true} | {"source":"persistent_city","resident_id":"pc:193","body_generation":85,"donor_id":null,"donor_reason":"ops_view_walker_donor_unexposed","stable_across_reload":true} | no reply | no reply | no reply |
| pc:319 | {"source":"persistent_city","resident_id":"pc:319","body_generation":66,"donor_id":null,"donor_reason":"ops_view_walker_donor_unexposed","stable_across_reload":true} | {"source":"persistent_city","resident_id":"pc:319","body_generation":66,"donor_id":null,"donor_reason":"ops_view_walker_donor_unexposed","stable_across_reload":true} | {"source":"persistent_city","resident_id":"pc:319","body_generation":66,"donor_id":null,"donor_reason":"ops_view_walker_donor_unexposed","stable_across_reload":true} | no reply | no reply | no reply |

**PARTIAL:** headless mode cannot prove dressed, animated bodies, blendshape output, clothes/skin/body-shape equality or a wrong-look flash. Persistent id/body-generation readbacks above are kept separate from visual proof. The paused distance round trip retained all five body generations and proves no release. The speed change then returned ops_view_resident_absent for all five. Reload found all five logical ids at work; replay to the seen clock time also returned ops_view_resident_absent for all five. Logical ids were preserved, but body reacquisition is NOT RUN/unverified for these targets. Follow readbacks and exact errors remain in the stage. A fresh reload can have no active body for a saved resident; ops_view_resident_absent is not identity replacement.

Player-A-final.log final cumulative look diagnostics: `69 built/7 reused/1972 deferred/32 idle/59 owned/36 adopted/748 baked/45 bake sizes/1678 KB bake scratch/32 grown/9 trimmed/0 fitted/131 waited/4.8 ms build/0 fast merges/748 native merges/0 failed merges`.

Player-A-look-final.log final cumulative look diagnostics: `55 built/9 reused/3159 deferred/3 idle/60 owned/8 adopted/824 baked/44 bake sizes/1634 KB bake scratch/51 grown/0 trimmed/0 fitted/155 waited/35.1 ms build/0 fast merges/824 native merges/0 failed merges`.

| Log | Appearance meshes unavailable | Empty appearance | Merged body |
| --- | --- | --- | --- |
| Player-A-look-final.log | 0 | 0 | 0 |
| Player-D-final.log | 0 | 0 | 0 |

### D: official #93 fixture flow, #282 check 5 and #246 checks 4–5

Fresh disposable city `FT RIGTMP-GR5-15g-mac`, created with new_game {"name":"FT RIGTMP-GR5-15g-mac"}; no throwaway flag. Steps 1–8: create/load; pause and verify autosave suppression; apply; list/read; Default save under that same loaded name; reload; pause and immediate fixture status before any other mutation; repeat list/read; revert; load the other FT city; read/delete. No sealed fixture was saved or deleted.

```text
{"apply":{"applied":true,"save":"FT RIGTMP-GR5-15g-mac","restored_on_load":false,"adopted_at":null,"load_hook":{"installed":true,"patch_owner":"agent-rig.unrentable-fixture-load","seams":["CityManager.InitCityBuildingControllers","GlobalEvents.InvokeOnGameLoaded"],"error":null,"last":{}},"sidecar":true,"subjects":[{"role":"housed_rotated","place":"address:ba%3Astreet_firststreet:22:home","original_size":"ba:buildingsize_l","size":"agentrig:unrentable_fixture","rentable_interior":false,"rotated":true,"meets_yard_or_sidewalk":false,"unrentable_class":"housed"},{"role":"housed_yard","place":"address:ba%3Astreet_fifthavenue:25:home","original_size":"ba:buildingsize_b","size":"agentrig:unrentable_fixture","rentable_interior":false,"rotated":false,"meets_yard_or_sidewalk":true,"unrentable_class":"housed"},{"role":"housed_plain","place":"address:ba%3Astreet_broadwaystreet:2:home","original_size":"ba:buildingsize_l","size":"agentrig:unrentable_fixture","rentable_interior":false,"rotated":false,"meets_yard_or_sidewalk":false,"unrentable_class":"housed"},{"role":"unreadable","place":"address:ba%3Astreet_broadwaystreet:13:home","original_size":"ba:buildingsize_n","size":"agentrig:unrentable_fixture","rentable_interior":false,"rotated":false,"meets_yard_or_sidewalk":false,"unrentable_class":"unreadable"},{"role":"empty","place":"address:ba%3Astreet_broadwaystreet:23:home","original_size":"ba:buildingsize_n","size":"agentrig:unrentable_fixture","rentable_interior":false,"rotated":false,"meets_yard_or_sidewalk":false,"unrentable_class":"empty"}],"missing":[]},"reload":{"applied":true,"save":"FT RIGTMP-GR5-15g-mac","restored_on_load":true,"adopted_at":"CityManager.InitCityBuildingControllers","load_hook":{"installed":true,"patch_owner":"agent-rig.unrentable-fixture-load","seams":["CityManager.InitCityBuildingControllers","GlobalEvents.InvokeOnGameLoaded"],"error":null,"last":{"GlobalEvents.InvokeOnGameLoaded":{"save":"FT RIGTMP-GR5-15g-mac","outcome":"already_adopted","error":null,"utc":"2026-11-15T15:27:36.8479900Z"},"CityManager.InitCityBuildingControllers":{"save":"FT RIGTMP-GR5-15g-mac","outcome":"adopted","error":null,"utc":"2026-11-15T15:27:33.8364520Z"}}},"sidecar":true,"subjects":[{"role":"housed_rotated","place":"address:ba%3Astreet_firststreet:22:home","original_size":"ba:buildingsize_l","size":"agentrig:unrentable_fixture","rentable_interior":false,"rotated":true,"meets_yard_or_sidewalk":false,"unrentable_class":"housed"},{"role":"housed_yard","place":"address:ba%3Astreet_fifthavenue:25:home","original_size":"ba:buildingsize_b","size":"agentrig:unrentable_fixture","rentable_interior":false,"rotated":false,"meets_yard_or_sidewalk":true,"unrentable_class":"housed"},{"role":"housed_plain","place":"address:ba%3Astreet_broadwaystreet:2:home","original_size":"ba:buildingsize_l","size":"agentrig:unrentable_fixture","rentable_interior":false,"rotated":false,"meets_yard_or_sidewalk":false,"unrentable_class":"housed"},{"role":"unreadable","place":"address:ba%3Astreet_broadwaystreet:13:home","original_size":"ba:buildingsize_n","size":"agentrig:unrentable_fixture","rentable_interior":false,"rotated":false,"meets_yard_or_sidewalk":false,"unrentable_class":"unreadable"},{"role":"empty","place":"address:ba%3Astreet_broadwaystreet:23:home","original_size":"ba:buildingsize_n","size":"agentrig:unrentable_fixture","rentable_interior":false,"rotated":false,"meets_yard_or_sidewalk":false,"unrentable_class":"empty"}],"missing":[]}}
```

Adoption must be all-or-nothing. Observed restored_on_load=True, adopted_at=CityManager.InitCityBuildingControllers, load_hook={"installed":true,"patch_owner":"agent-rig.unrentable-fixture-load","seams":["CityManager.InitCityBuildingControllers","GlobalEvents.InvokeOnGameLoaded"],"error":null,"last":{"GlobalEvents.InvokeOnGameLoaded":{"save":"FT RIGTMP-GR5-15g-mac","outcome":"already_adopted","error":null,"utc":"2026-11-15T15:27:36.8479900Z"},"CityManager.InitCityBuildingControllers":{"save":"FT RIGTMP-GR5-15g-mac","outcome":"adopted","error":null,"utc":"2026-11-15T15:27:33.8364520Z"}}}. Same place per role is compared with apply, and sidecar is retained only until revert.

| List (120 s client timeout) | matched | tally | scan geometry_reads / slices |
| --- | --- | --- | --- |
| D all | 5 | {"total":5,"housed":3,"unreadable":1,"empty":1,"policy":"native:building-bounds;policy:unrentable-footprint-floors-v1"} | {"geometry_reads":5,"slices":1} |
| D yard | 1 | {"total":5,"housed":3,"unreadable":1,"empty":1,"policy":"native:building-bounds;policy:unrentable-footprint-floors-v1"} | {"geometry_reads":5,"slices":1} |
| D unreadable | 1 | {"total":5,"housed":3,"unreadable":1,"empty":1,"policy":"native:building-bounds;policy:unrentable-footprint-floors-v1"} | {"geometry_reads":5,"slices":1} |
| D empty | 1 | {"total":5,"housed":3,"unreadable":1,"empty":1,"policy":"native:building-bounds;policy:unrentable-footprint-floors-v1"} | {"geometry_reads":5,"slices":1} |
| D-reloaded all | 5 | {"total":5,"housed":3,"unreadable":1,"empty":1,"policy":"native:building-bounds;policy:unrentable-footprint-floors-v1"} | {"geometry_reads":5,"slices":1} |
| D-reloaded yard | 1 | {"total":5,"housed":3,"unreadable":1,"empty":1,"policy":"native:building-bounds;policy:unrentable-footprint-floors-v1"} | {"geometry_reads":5,"slices":1} |
| D-reloaded unreadable | 1 | {"total":5,"housed":3,"unreadable":1,"empty":1,"policy":"native:building-bounds;policy:unrentable-footprint-floors-v1"} | {"geometry_reads":5,"slices":1} |
| D-reloaded empty | 1 | {"total":5,"housed":3,"unreadable":1,"empty":1,"policy":"native:building-bounds;policy:unrentable-footprint-floors-v1"} | {"geometry_reads":5,"slices":1} |

The five fixture roles are checked against pc_home_candidate list rows; PC can omit candidates without a NavMesh entrance. Before reload the rig sees the changed fixture, while PC’s load-time tally is **not observable in-load** for those changes. Reload comparison follows.

```text
[Persistent City] unrentable homes housed=0 beds=0 unreadable=0 empty=0
[Persistent City] unrentable homes housed=3 beds=1270 unreadable=1 empty=1
[Persistent City] Residential places omitted: 1 without a finite footprint or floor count; 0 below the household policy's minimum interior area.
```

#246 check 4 Expected: "footprint within about 1.5x of the visible ground area and floors within one of the visible storeys." #282 check 5 Expected: "report collider height and renderer height per subject next to the floor counts. The mod is unchanged, so floors stay 13 / 5 / 11".

| Role | place | collider height m | renderer height m | collider floors | visible storeys | footprint m² | gross m² | usable m² | collider / visible AABB | within one floor |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| housed_rotated | address:ba%3Astreet_firststreet:22:home | 48.719 | 49.92 | 13 | 14 | 1431.112 | 20035.568 | 12021.341 | 0.9967 | True |
| housed_yard | address:ba%3Astreet_fifthavenue:25:home | 18.389 | 23.514 | 5 | 6 | 426.709 | 2560.254 | 1536.152 | 0.9759 | True |
| housed_plain | address:ba%3Astreet_broadwaystreet:2:home | 40.332 | 47.418 | 11 | 13 | 3826.154 | 49740.002 | 29844.001 | 1.0 | False |

**#282 check 5 PASS** for the requested height/floor report. **#246 check 4 FAIL**: the plain subject’s 11 collider floors versus 13 renderer-derived visible storeys differ by two. No weaker criterion is substituted. The three footprint ratios meet about 1.5x. Heights explain the gap but do not make the check pass. Renderer-derived storeys are not a native exterior floor count. Independent home_area excludes BuildingSize, size-helper squareMeters, the fixture journal and sidecar; it is not a saved-Home extraction.

#246 check 5 Expected: "buildings under one storey count as empty, not unreadable, and the omitted-places warning only counts unreadable ones." Reload tally above is the proof; in-load tally is the rig’s candidate classification, not a second PC scan. The collider-free unreadable read fails closed with ops_building_bounds_unavailable; no floor count is invented. The empty subject has zero collider floors.

```text
{"applied":false,"save":"FT RIGTMP-GR5-15g-mac","restored_on_load":false,"adopted_at":null,"load_hook":{"installed":true,"patch_owner":"agent-rig.unrentable-fixture-load","seams":["CityManager.InitCityBuildingControllers","GlobalEvents.InvokeOnGameLoaded"],"error":null,"last":{"GlobalEvents.InvokeOnGameLoaded":{"save":"FT RIGTMP-GR5-15g-mac","outcome":"already_adopted","error":null,"utc":"2026-11-15T15:27:36.8479900Z"},"CityManager.InitCityBuildingControllers":{"save":"FT RIGTMP-GR5-15g-mac","outcome":"adopted","error":null,"utc":"2026-11-15T15:27:33.8364520Z"}}},"sidecar":false,"subjects":[],"missing":[]}
```

### Disposable-save reads and cleanup

`D-delete-read`:

```text
{"name":"FT RIGTMP-GR5-15g-mac","exists":true,"loaded":false,"files":[{"file":"Big Ambitions/FVkILQxK9EC2qabia3YKNw==/FT RIGTMP-GR5-15g-mac.hsg","bytes":3032322},{"file":"Big Ambitions/FVkILQxK9EC2qabia3YKNw==/FT RIGTMP-GR5-15g-mac.hsg.meta","bytes":5159},{"file":"Big Ambitions/FVkILQxK9EC2qabia3YKNw==/FT RIGTMP-GR5-15g-mac.jpg","bytes":4723}]}
```

`D-delete`:

```text
{"before":{"name":"FT RIGTMP-GR5-15g-mac","exists":true,"loaded":false,"files":[{"file":"Big Ambitions/FVkILQxK9EC2qabia3YKNw==/FT RIGTMP-GR5-15g-mac.hsg","bytes":3032322},{"file":"Big Ambitions/FVkILQxK9EC2qabia3YKNw==/FT RIGTMP-GR5-15g-mac.hsg.meta","bytes":5159},{"file":"Big Ambitions/FVkILQxK9EC2qabia3YKNw==/FT RIGTMP-GR5-15g-mac.jpg","bytes":4723}]},"after":{"name":"FT RIGTMP-GR5-15g-mac","exists":false,"loaded":false,"files":[]}}
```

`leftover-read-FT RIGTMP-GR5-15f-mac`:

```text
{"name":"FT RIGTMP-GR5-15f-mac","exists":false,"loaded":false,"files":[]}
```

`leftover-read-FT RIGTMP-GR5-15d-mac`:

```text
{"name":"FT RIGTMP-GR5-15d-mac","exists":false,"loaded":false,"files":[]}
```

`leftover-read-FT RIGTMP-GR5-15c`:

```text
{"name":"FT RIGTMP-GR5-15c","exists":false,"loaded":false,"files":[]}
```

Target deletion occurs only after loading a different FT city; D-delete after.exists=false. Leftovers are deleted only if unexpectedly present. FT RIGTMP-GR5-14i and sealed fixtures are retained.

### Optional E — current-build density/refill telemetry

**PARTIAL #262 check 5:** current-build fresh-street telemetry uses installed density operations and the reviewed rig.walker_refill.v2 calculation. No paired 14b candidate is staged in this run; visual wrong-look flash is NOT RUN in headless mode.

E-density-original: {"center":{"kind":"player","world":{"x":215.0,"y":0.102532983,"z":0.0}},"radius":50.0,"metric":"xz","nearby":3,"active":5,"band":{"min":48,"max":96},"in_band":false,"hotspot":{"x":227.85040283203125,"z":0.9646453857421875,"nearby":3},"frame":5230,"unscaled_time":352.82745361328125,"excludes_player":true}

```text
{"schema":"rig.walker_refill.v2","label":"15g-current-fresh-street","radius":50,"band":{"min":48,"max":96},"started_utc":"2026-11-15T15:31:02Z","burst":{"size":0,"t":null,"window":3.0},"thresholds":{"12":null,"24":null,"32":null,"48":null,"96":null},"peak":{"nearby":0,"t":0.0},"final":{"nearby":0,"t":46.199},"in_band":false}
```

Full native-time samples: `<stage>/E-refill-record.json`, SHA-256 `7a5804fe98161d4b24a18e20f9c72480fe40bba64409675f64bd4b2eda27f765`. Counts below the target band are recorded; the density op creates no walkers. Native active counts are an upper bound, not evidence of the 48–96 target being reached.

### NOT RUN — grouped with reasons

| Group | Checks / reason |
| --- | --- |
| Windows only / PC excluded | GR-5 60 fps street and busy-shop arms: Mac about 5–8 fps at 1,000 residents; Peter is gaming. No Windows client contact, startup or use. |
| Owner decision pending | GR-5 save latency / #246 check 2: separate save-profiling run is not authorized here; no save latency measured. |
| Visual proof unavailable in headless | #282-4 dressed/animated/blendshape/same-look proof; #254-4 screenshot comparison; #262-4 clothes/skin/body-shape and no distorted/T-posed bodies; #262-5 wrong-look flash. Identity/operation observations are partial, never substituted for captures. |
| Paired or driving scenario outside this run | Full #262-5 comparison against 14b: that build was not staged. GR-9 #238-1 fades, -2 mode transitions, -3 saved parked-car model/paint, -5 twenty-walker wardrobe/donor comparison, -6 paired 48/96 cap perf, -7 junction yielding, -8 parking overlap, -9 traffic/red-light stopping, -10 walker stopping: no required visual/paired/driving setup in this headless batch. #238-4 diagnostic reads alone do not prove taxis/service traffic behavior. |
| Other pending checklist rows | Items 1–26 and 28–29 retain their documented scenario/fixture/tooling/owner prerequisites; they are outside A/D and the optional identity/refill probes. Item 27 capability-report-cost was measured in 15f; no additional 600 s arm here. Everything else not listed in this launch is NOT RUN. |

### Exact op failures and tooling gaps

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent call shoot '{"name": "rig15g-look", "x": 215, "y": 18, "z": -18, "look_at": {"x": 215, "y": 1, "z": 0}, "width": 960, "height": 540, "frames": 8, "timeout_s": 60}'` (exit 1):

```text
InvalidOperationException: Screenshots require the agent instance's background mode.
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent call teleport '{"who": "player", "x": -500, "z": 500}'` (exit 1):

```text
No walkable ground at x -500.0, z 500.0: no walkable surface of 1: 0.01 not walkable; walking routes not checked (more than 100 m from the player).
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "resident", "id": "pc:502"}}'` (exit 1):

```text
{
  "error": "ops_view_resident_absent",
  "reply": {
    "ok": false,
    "id": "49db1e4fea6e414dab21afb376c5778c",
    "session": "abb79e5d43e14cadbf66fd8e054e7a8c",
    "error": "ops_view_resident_absent"
  }
}
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "resident", "id": "pc:552"}}'` (exit 1):

```text
{
  "error": "ops_view_resident_absent",
  "reply": {
    "ok": false,
    "id": "0496086f5f0b48bda72c21d3e1ee50ff",
    "session": "abb79e5d43e14cadbf66fd8e054e7a8c",
    "error": "ops_view_resident_absent"
  }
}
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "resident", "id": "c121"}}'` (exit 1):

```text
{
  "error": "ops_view_resident_absent",
  "reply": {
    "ok": false,
    "id": "d0522856a772461983bf71e5eb14fa2d",
    "session": "abb79e5d43e14cadbf66fd8e054e7a8c",
    "error": "ops_view_resident_absent"
  }
}
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "resident", "id": "pc:193"}}'` (exit 1):

```text
{
  "error": "ops_view_resident_absent",
  "reply": {
    "ok": false,
    "id": "50c6f998f30f4d9e97dca0fe9a65f78a",
    "session": "abb79e5d43e14cadbf66fd8e054e7a8c",
    "error": "ops_view_resident_absent"
  }
}
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "resident", "id": "pc:319"}}'` (exit 1):

```text
{
  "error": "ops_view_resident_absent",
  "reply": {
    "ok": false,
    "id": "8cab6c46ea0544c6956dbbe321608b92",
    "session": "abb79e5d43e14cadbf66fd8e054e7a8c",
    "error": "ops_view_resident_absent"
  }
}
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "resident", "id": "pc:502"}}'` (exit 1):

```text
{
  "error": "ops_view_resident_absent",
  "reply": {
    "ok": false,
    "id": "f6b43742d71d41b8b05257384a7fdf5c",
    "session": "abb79e5d43e14cadbf66fd8e054e7a8c",
    "error": "ops_view_resident_absent"
  }
}
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "resident", "id": "pc:552"}}'` (exit 1):

```text
{
  "error": "ops_view_resident_absent",
  "reply": {
    "ok": false,
    "id": "6910d2c305b940738326db44d2a490ae",
    "session": "abb79e5d43e14cadbf66fd8e054e7a8c",
    "error": "ops_view_resident_absent"
  }
}
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "resident", "id": "c121"}}'` (exit 1):

```text
{
  "error": "ops_view_resident_absent",
  "reply": {
    "ok": false,
    "id": "de27544c219d42ea869990d0f18270b9",
    "session": "abb79e5d43e14cadbf66fd8e054e7a8c",
    "error": "ops_view_resident_absent"
  }
}
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "resident", "id": "pc:193"}}'` (exit 1):

```text
{
  "error": "ops_view_resident_absent",
  "reply": {
    "ok": false,
    "id": "8997e5b565f449c19b1b944b88a4e06b",
    "session": "abb79e5d43e14cadbf66fd8e054e7a8c",
    "error": "ops_view_resident_absent"
  }
}
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "resident", "id": "pc:319"}}'` (exit 1):

```text
{
  "error": "ops_view_resident_absent",
  "reply": {
    "ok": false,
    "id": "45ab9de5b8874136852fcc1486a92ab8",
    "session": "abb79e5d43e14cadbf66fd8e054e7a8c",
    "error": "ops_view_resident_absent"
  }
}
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "resident", "id": "pc:502"}}'` (exit 1):

```text
{
  "error": "ops_view_resident_absent",
  "reply": {
    "ok": false,
    "id": "c68c810ace6a456a90a3994b91527896",
    "session": "abb79e5d43e14cadbf66fd8e054e7a8c",
    "error": "ops_view_resident_absent"
  }
}
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "resident", "id": "pc:552"}}'` (exit 1):

```text
{
  "error": "ops_view_resident_absent",
  "reply": {
    "ok": false,
    "id": "6200ca9a6358411494a1d16eb832890f",
    "session": "abb79e5d43e14cadbf66fd8e054e7a8c",
    "error": "ops_view_resident_absent"
  }
}
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "resident", "id": "c121"}}'` (exit 1):

```text
{
  "error": "ops_view_resident_absent",
  "reply": {
    "ok": false,
    "id": "67330d3c82944b9e8e39008d643d9a31",
    "session": "abb79e5d43e14cadbf66fd8e054e7a8c",
    "error": "ops_view_resident_absent"
  }
}
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "resident", "id": "pc:193"}}'` (exit 1):

```text
{
  "error": "ops_view_resident_absent",
  "reply": {
    "ok": false,
    "id": "587e0fdba43d4c468f5ed2cc952ce6b9",
    "session": "abb79e5d43e14cadbf66fd8e054e7a8c",
    "error": "ops_view_resident_absent"
  }
}
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent op screen_pos '{"action": "get", "target": {"kind": "resident", "id": "pc:319"}}'` (exit 1):

```text
{
  "error": "ops_view_resident_absent",
  "reply": {
    "ok": false,
    "id": "5f05533b1e83421891cbaf78ef8896c3",
    "session": "abb79e5d43e14cadbf66fd8e054e7a8c",
    "error": "ops_view_resident_absent"
  }
}
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent op building '{"action": "read", "place": "address:ba%3Astreet_broadwaystreet:13:home"}'` (exit 1):

```text
{
  "error": "ops_building_bounds_unavailable",
  "reply": {
    "ok": false,
    "id": "a87f8cc7f5d54595bd56ac8ebca92cc1",
    "session": "94d85e1b5e27489a9cc1fc78705656c9",
    "error": "ops_building_bounds_unavailable"
  }
}
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent op building '{"action": "read", "place": "address:ba%3Astreet_broadwaystreet:13:home"}'` (exit 1):

```text
{
  "error": "ops_building_bounds_unavailable",
  "reply": {
    "ok": false,
    "id": "c314b5be8a3d46bf9a5a117027dd02aa",
    "session": "94d85e1b5e27489a9cc1fc78705656c9",
    "error": "ops_building_bounds_unavailable"
  }
}
```

`export BA_LOCK_OWNER=persistent-city-rig-2026-11-15g-mac; python3 scripts/mac-agent call teleport '{"who": "player", "x": 227.85040283203125, "z": 0.9646453857421875}'` (exit 1):

```text
Something stands at x 227.9, z 1.0: PersistentCity own cars/BodyCollider (layer AiVehicles).
```

A collider-free unreadable fixture returning ops_building_bounds_unavailable is the expected fail-closed read, not an invented floor value. Other missing capabilities are recorded above; no production fix was attempted.

### Restoration and offline validation

```text
{"original_files_verified":32,"original_absent_paths_verified":3,"rlo_files_verified":53,"mismatches":[],"agent_rig_ops_sha256":"40a288c24dc1976e85d67599395f222b544258c0b820015dbf92b0981be6e6ee","install_json_sha256":"d49deda807eb3ca93d8e91983fca2fbcd6df4ff7fe620edf2fcc5e8e9e28c512","fixture_sidecars":0,"disposables_absent":["FT RIGTMP-GR5-15c","FT RIGTMP-GR5-15d-mac","FT RIGTMP-GR5-15f-mac","FT RIGTMP-GR5-15g-mac"],"sealed_fixture_hash_match":true,"lease_released":true,"started_game_processes_gone":[41639,68955],"caffeinate_processes_gone":true}
```

Stopped only owned agentmac processes, reverted the fixture first, restored the whole PersistentCity folder and exact install.json with the printed rollback, restored isolated preferences/config/autosave/frame-cap state, verified complete file sets and hashes, confirmed no fixture sidecar, released the lease and checked all started game/caffeinate processes were gone. RLO and the live overlay were verified unchanged. Build output and raw logs remain outside Git. The evidence/build worktree is removed after commit/push/PR creation.

Offline validation: build exited 0; tools/md-ban.py, tools/docs-links/check.py and tools/test-focused.py are run for this docs-only evidence change. No full suite or regression suite is run by this rig role. In-game outcomes above are separate from those offline checks.

Raw report records and hashes:

| Record | bytes | SHA-256 |
| --- | --- | --- |
| gc-day-gc-stop.json | 17270 | 5fba3014c492497d1676a583968bf4ffb3cec6d4d43b616a9b175977798da071 |
| gc-night-gc-stop.json | 19896 | fe2d379a44f2eaef830a1eec7db31f6684bcafec2e8533274a9733272729d8a0 |
| gc-day-modwork-end.json | 51899 | 312dee504b9a7a005f8803864b58680bf753da99ce7f89ad96d565cdc4cd9390 |
| gc-night-modwork-end.json | 54426 | 0af4bcfe61c98fc443f185ecb365743fdef91af44ae019cbbd099eb3b452f52b |
| street-end.json | 57555 | 61c5d0b1ee8a2923fbda8a3ab787802c72e3ec27962360d82e519e7a2e3f3401 |
| street-frames-stop.json | 14613 | 51da96aba8bfa612d354819f3493b64f83268bae56decd5b6ab54d24c66f43e1 |
| D-status-reloaded.json | 2461 | 00319da4a180d11ca555b9b691384fe7cc455d1fb179f7f39c1c347de1776660 |
| restore-verification.json | 637 | 64a6d3bbdfda43d17a85809232629625b5cd3da29dfb26b85b6a0ade1c385cc8 |

Raw JSON is kept in `<stage>`; no full record is added to Git because the combined GC records exceed 20 KB. All exported segment values, retained mod-work spikes, pause markers and window-boundary host evidence are excerpted above.
