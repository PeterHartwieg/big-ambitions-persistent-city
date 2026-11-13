# Rig checklist

In-game checks waiting for a batched rig run. A rig run never gates a merge. Each slice lists its checks in its PR description. After merge the coordinator copies them here, and removes a check once a rig run passes it at a build that still contains the change. This list authorizes no run.

Every check runs in a named disposable FT city in the isolated rig profile, never in Peter's ordinary save. Autosave is off before every measured window, including after a reload, and is restored at cleanup.

Detailed specs: [F8 §7](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/f8-capability-boundaries.md#7-in-game-checks-for-the-batched-rig), [F9 §7](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/f9-city-acceptance.md#7-in-game-checks-for-the-next-batched-rig), [E6 §7](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/plans/e6-carts-food-trucks.md#7-in-game-checks-for-the-batched-rig), [F4 plan](plans/f4-perf-gate.md), [11j report](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/a3a6c8db8686f4382ac3cf941010e0b614e48805/docs/checks/rig-2026-11-11j-mac-results.md), [12b report](checks/rig-2026-11-12b-mac-results.md). Checks written before 10 November are in the [docs history tag](https://github.com/PeterHartwieg/big-ambitions-persistent-city/tree/docs-history-2026-11-10/docs/checks); many test save migration, which no longer exists.

## Latest evidence

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
