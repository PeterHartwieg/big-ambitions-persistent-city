# Rig results ? 11 November 2026 d PC (shop_fixture selection + F8h first sale)

Status: partial, run in progress. Completed segment: shop_fixture selection PASS. Remaining checks have no verdict yet.

[Combined checklist](rig-2026-11-06-checklist.md). [Previous PC run](rig-2026-11-11c-pc-results.md).

Role `rig_test`, HomePCHartwieg, isolated background agent1. Schema held at 1. Testing only.

`<stage>` = `C:/Users/Peter/HART-Builds/persistent-city-rig-2026-11-11d-pc`. Raw logs, saves, binaries and backups stay outside Git.

## Build and rig

| Item | Value |
|---|---|
| Candidate | `86c2e92f33e176fd29cea0072c0f5341f299e97f`; fetched and verified before build, requested tip unchanged. |
| Build | Requested tools/build.py passed; dotnet PATH prefix and wait-other-dotnet applied. No offline dotnet tests. |
| DLL SHA-256 | `4c500997601b098e18665feca8d2b6a3130e7a0180249f515944ce09d6436850` |
| Rig checkout | `baebea60ac96b378ddedfd28aa25682ae3526c3d`; remote main matches, checkout read only. |
| Installed overlay | Ops revision `baebea60ac96b378ddedfd28aa25682ae3526c3d`; Ops SHA-256 `8fbbc7294fd0741e64ff46b5046be9b5b0ba368bc6d6b2e1c51b623c812fc1a3`. No overlay rebuild/update/install. |
| Rollback | `C:/BigAmbitions-AgentTests/revisions/20261111T102906939` untouched, as are older rollback revisions. |
| Backup | Current post-install 301 protected entries plus isolated HKCU key, hash-verified in `<stage>/backup-manifest.json` and `original-lifecycle/`. |
| Isolation | Full 30-DLL modpack, port 18330, stable owner `pc-rig-2026-11-11d`; no foreground or real input. |
| Installed ops | Live ops includes shop_fixture, foreign_patch, rlo_option, pcstate, pcsave, ledger, rival_fingerprint, modwork. |

## Runs

| Flow | Outcome | Evidence |
|---|---|---|
| creation | PASS, fresh native new_game and ready checkpoint | `<stage>/runs/creation` |
| shop | Complete native build; all recorded actions succeeded | `<stage>/runs/shop` |

## shop_fixture selection

**PASS, verified by rig.** Paused, set documented fixture money to $100,000,000, and built the checked-in pc-food-shop recipe with `address` deleted and `selection: {building_size: ba:buildingsize_c}` added. Native rental succeeded. No campaign or rival-finance write.

Picked **24 Fourth Avenue**, business id `ba:street_fourthavenue#24`, neighborhood `ba:neighborhood_hellskitchen`, size `ba:buildingsize_c`, version **2**, **225 m?**. Native readback: rented=true, property owned=false, rent/day $792, deposit $47,520. The player operates the business as a tenant. One customer-service employee, seven 09:00?18:00 Default shifts, cash register, two bakery showcases, storage shelf, cleaning station, 100 croissants at $5 and 300 paper bags. Raw complete receipt and shop_state remain in `<stage>/runs/shop/results`.

## F8h first sale / check 1 shop assertions

**PARTIAL, first attempt retained.** Normal reload reported checkout active, but the built shop?s boundary image was Refused / MissingDefinition. The natural window recorded zero purchases, no dropped entries and no hook faults; native attempted croissant orders were unavailable, while native stock still read 100 croissants / 300 bags. No PC sale receipt was written. This is no first-sale proof.

The stage waiter accepted an old paused-health snapshot after requesting next-day 10:00, so that attempted setup was not the specified window. The actual retained window loaded day 1 near 08:00 and ran to 12:00. The corrected stage waiter will require exact target day/time. Raw evidence: `<stage>/runs/firstsale`, including the complete shop log and boundary diagnostics. No production change or timing verdict.

## Findings

The size-C selection and documented funding step reached and completed native rental at this head.

## Tooling gaps

None in the completed selection segment.

## Fixtures

Fresh native character only: `FT E5b PC 11 Nov d 86c2e92f Ready`, `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\SaveGames\Big Ambitions\7GtI09kH1Uqnnwoy22MA+A==\FT E5b PC 11 Nov d 86c2e92f Ready.hsg`, SHA-256 `574df5f88bc641653b37b8488d040d65abc6fc602133576300c04b9d5c7b3fad`.

Owned staffed shop checkpoint: `FT PC 11 Nov d shop Owned Staffed Food Shop`, `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\SaveGames\Big Ambitions\7GtI09kH1Uqnnwoy22MA+A==\FT PC 11 Nov d shop Owned Staffed Food Shop.hsg`, SHA-256 `c298933d9c9c5020600912480f504e37d17fa136408c1b80bd9a8d3bb2b39ae7`.

## Cleanup

Each completed lifecycle restored its named state and released its lease. Final full-run restore is pending.

## E5b supported mixed-shop setup

**PARTIAL, verified by rig setup only.** The checked-in `shop-multi-offer.json` spec reconciled the same named rented business at 24 Fourth Avenue with a new fixture id. No new rental, funding or campaign step. Native readback now advertises croissant and donut, with 100 units each and 300 bags. The loaded catalogue identifies donut as a base-game product with a complete bakery-showcase holder link; the native product whitelist includes croissant/cupcake/donut. PC?s post-build image has `passed:true`, `refusal:null`; it is still native-owned until a future acquisition boundary. Checkout, accounting and visual assertions remain unproved. Raw evidence: `<stage>/runs/mixed`.

### Corrected normal-load first-sale window

**F8h-2 PARTIAL.** The corrected waiter reached native day 2 10:00 paused exactly. The mixed shop was Published / passed / managed, desired=true, lastReadiness=Ready. A manual checkpoint was normally reloaded; autosave suppression verified. The exact 10:00?12:00 window completed with **0 purchases, 0 dropped, 0 hook faults**, no log entries, unchanged 100 croissants / 100 donuts / 300 bags, $0 daily shop revenue and no food-sale receipt key. Checkout was active, but there was no first sale to prove. Raw evidence: `<stage>/runs/firstsale2`, including `arrived-exact-nextday-1000.json`, window boundary facts and the retained checkpoint.

### F8h check 1 shop boundary assertions

**PARTIAL, ownership transitions verified by rig.** This fresh prefix cycle was required for the shop assertions left open on 11c. The normal 12:00?13:00 extension had zero purchases and no new receipt. Installed `agent-rig.foreign.pc1111d`, reloaded the owned-shop checkpoint, and recorded the exact shop exclusion: `Excluded: food checkout contract off: foreign prefix by agent-rig.foreign.pc1111d on RetailBusinessSimulator.ProcessCustomer(CustomerEntry)`.

At hour 61 the shop was managed=true, desired=false, nextBoundary=hand back. By hour 63 it was managed=false, nextBoundary=native. The 13:00?15:00 native window recorded **4 visits, 8 not_purchased entries and 4 left_without_buying**, all purchases zero; native reason `unavailable`, stock and $0 revenue unchanged, dropped=0 and hook_faults=0. No food-sale receipt key existed before or after the refused window. Native dispatch is observed; successful native service is unproved.

After removal and normal reload at hour 63 the shop was managed=false, desired=true, nextBoundary=Ready. At hour 64 (16:00) it was managed=true, nextBoundary=owned, lastBoundary=64, lastReadiness=Ready. Thus hand-back and reacquisition are proved. Raw evidence: `<stage>/runs/foreignshop/results`, boundary records 033/046/059/067 and retained logs. No reload-epoch check was repeated.

### Native-shop halves: F8a-5 / F8bg-4

**PARTIAL.** Loaded the same fresh shop checkpoint without RLO. PC reported `start refused (rlo.donor)` and `RealLifeOverhaul is required and is not loaded.`, with no start-a-new-city wording. The 16:00?17:00 window completed; all live PC key hashes remained identical and no new food receipt appeared. The bounded native shop log contained zero visits/purchases, dropped=0, hook_faults=0; native service remains unproved. RLO and the complete DLL set were restored by the lifecycle. No absent-state checkpoint was saved. Raw evidence: `<stage>/runs/rlo-absent` and `<stage>/rlo-absent-summary.json`.

## E5b shop subsets

PARTIAL. The documented croissant/donut setup and a paused native donut-price edit/restore were recorded with shop_state, PC boundary/stamps/hints and capability readbacks. This is a two-offer subset; no in-flight checkout, burger/hot-dog mutation matrix, eight/nine-offer fixture, independent notification trace, in-place definition edit, removal/return transport, or paired performance controls were supplied. Raw evidence: `<stage>/runs/remaining/results`, e5b-prefixed actions.

## F9 shop checkpoint

PARTIAL. Created a local unsealed day-three 07:30 f9-city-ready checkpoint from the same fresh character and selected shop; recorded PC capability, manifest, shop, food decisions and existing projections. No official FT-F9 manifest was sealed. Formal rows requiring RIG-ACCEPT, paired head/base or daily-reload controls, PhysicalEnabled/config readback, Street Food vendor/API or reserved presentation steps remain NOT RUN. Raw evidence: `<stage>/runs/remaining`, f9-prefixed actions and `f9-day3-Player.log`.
