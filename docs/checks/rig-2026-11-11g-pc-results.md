# Rig results ? 11 November 2026 g PC (#72 serving shop readiness + first sale)

Selection segment: implemented and verified by rig at `4438c81a180a01791f88410d7526514dd033d35f`, installed rig #72 `10cf7d873a733ac564a84d95cc23b4213c101d7f`.

Native rental PASS: funded size-C selection, no pinned address, selected **58 Fifth Avenue**, `ba:street_fifthavenue#58`, Murray Hill, C1, 225 m?. Readiness FAIL: `ready=false`, exact reason `no_staffed_counter`; all seven days have zero staffed counters. Croissant offer ready with 100 units at $5; no empty cargo holders. The cash register has `point_of_sale=true`, `available=false`, with 300 paper bags; its employee is available and has Default 09:00?18:00 shifts with customer_service=50. The unavailable register is observed; its underlying native requirement is not diagnosed here.

No clock advance or sale wait followed the failed readiness diagnostic. No workaround, second recipe, extra money or shop edit. F8h first sale and dependent shop assertions remain NOT RUN. Segment player quit normally, mods/lifecycle restored and lease released; final manifest-wide verification follows in the next evidence segment.

Raw evidence: `C:/Users/Peter/HART-Builds/persistent-city-rig-2026-11-11g-pc/runs/shop/results/010-shop-selection-build.json`, `011-shop-final-status.json`, `015-shop-native-state.json`, and `readiness.json` in the stage root. Checkpoint label `Owned Staffed Food Shop` is inherited tooling text and does not certify readiness.
