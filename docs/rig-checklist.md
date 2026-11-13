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
