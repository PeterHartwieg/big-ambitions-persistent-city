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

Pending the next segment. No sale verdict inferred from a successful fixture build.

## Findings

The size-C selection and documented funding step reached and completed native rental at this head.

## Tooling gaps

None in the completed selection segment.

## Fixtures

Fresh native character only: `FT E5b PC 11 Nov d 86c2e92f Ready`, `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\SaveGames\Big Ambitions\7GtI09kH1Uqnnwoy22MA+A==\FT E5b PC 11 Nov d 86c2e92f Ready.hsg`, SHA-256 `574df5f88bc641653b37b8488d040d65abc6fc602133576300c04b9d5c7b3fad`.

Owned staffed shop checkpoint: `FT PC 11 Nov d shop Owned Staffed Food Shop`, `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\SaveGames\Big Ambitions\7GtI09kH1Uqnnwoy22MA+A==\FT PC 11 Nov d shop Owned Staffed Food Shop.hsg`, SHA-256 `c298933d9c9c5020600912480f504e37d17fa136408c1b80bd9a8d3bb2b39ae7`.

## Cleanup

Each completed lifecycle restored its named state and released its lease. Final full-run restore is pending.
