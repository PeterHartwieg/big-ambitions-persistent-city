# Reviewed traffic-retiming fix installed on PC — 21 October 2026

Installation and independent readback completed at 13:33 CEST. Peter authorized both installations and saved/closed the normal game; both processes were confirmed stopped before changing files. The normal game was not used for testing. Both ownership reservations were released after verification.

Both profiles now contain Persistent City 0.6.2 development DLL `6e0e5b58776a5a4430b4b585e03f7233d1dc5dedfd94200cb7f603a44ea55de1`, replacing reviewed baseline `e0756b4a…`. It includes the reviewed performance improvements and RLO 0.45 compatibility, plus the [retiming and legacy-save migration fix](2026-10-21-retiming-fix.md). Concurrent CityApi work remains excluded. Outer city schema remains 7 and nested traffic schema is now 2.

The immutable reviewed ZIP SHA-256 is `803ed4eb4bc471c9ab3996f3c2ce56aedc6c7190491cfe459324c8c65345b91e`. All packaged files were verified before staging and all installed files independently read back. The normal folder contains 42 verified files and the rig folder 41; retained historical documentation accounts for files beyond the new package.

| Installation | Mod directory |
| --- | --- |
| Normal Steam game | `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions\ModsLocal\PersistentCity` |
| Isolated Windows rig | `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\ModsLocal\PersistentCity` |

Each existing configuration was preserved byte-for-byte. Normal configuration SHA-256: `abfecb215a3c3ea89e6a1137c7445726dde66947c06d361adb2be1337cf78c66`; rig configuration: `abfecb215a3c3ea89e6a1137c7445726dde66947c06d361adb2be1337cf78c66`. Both retain Enabled/PhysicalEnabled true, 48 pedestrians, 24 vehicles and 120 m radius. The archive remains disabled by default; each installed `INSTALLATION.json` records this deliberate configuration difference.

The rig manifest now verifies all 99 entries; all 58 entries outside Persistent City remain unchanged. This preserves the newer Food Trucks package detected during testing. The temporary diagnostic helper is absent. The original pre-task private rig registry preferences were restored exactly before final installation.

Verified backups, the immutable ZIP, staged inventories, previous mod folders and receipts are at `C:\Users\Peter\HART-Backups\persistent-city-retiming-install-20261021T111954Z`. `normal-before` / `agent1-before` are verified copies; `normal-previous` / `agent1-previous` retain the replaced directories. `agent-install-before.json` retains the original manifest. Rollback must acquire ownership, require closed processes, preserve newer changes and reconcile the rig manifest. An old DLL cannot read new traffic-schema-2 saves; use the corresponding older save backup when reverting. Normal game saves were not opened or modified.

Validation applies to the exact installed DLL: 915 offline tests, Astra / Sol / Opus 5.5 / Grok 4.7 source approval, and 65 bounded Windows rig regression checks. Astra and Sol independently reviewed the completed native evidence. Both games remain closed; the first normal-game session after installation is untested. Genuine Driven Rides behavior, a representative production save and recording/performance clearance remain outside this bounded regression.

Local receipts: ignored `build/retiming-fix-20261021/pc-install/`; native evidence and cleanup: `windows-r4/`. The [previous installation](2026-10-21-modpack-pc-install.md) is historical.
