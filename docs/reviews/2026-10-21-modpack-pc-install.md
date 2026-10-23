# Reviewed modpack build installed on PC — 21 October 2026

> Superseded installation: both profiles subsequently received the [reviewed retiming fix](2026-10-21-retiming-pc-install.md). The hashes and receipts below describe the earlier installation.

Peter explicitly requested installation in both the normal game and Windows test rig after the four-model review. Installation and independent readback completed at 12:03 CEST. Both games were closed and both ownership reservations were free before work; no prior session was interrupted.

Both profiles now contain the frozen reviewed Persistent City 0.6.2 development DLL `e0756b4a74764c4b04e65cf0edd9a2043cff9ccb4e34dea2942a9063c0582124`, replacing `00e7d761177a61f517605d07249daf8373ec28243ac8ff7aa4e4a88163d051f6`. This combines the gate index, bounded car search, closure refresh and exact RLO 0.45 compatibility change. It excludes the concurrent CityApi changes in the working checkout.

The transferred, immutable reviewed ZIP has SHA-256 `24d772247ad110faa176c4e5776dff903dfa93b3635027d0c24899a62db1902a`. All package-file hashes were checked before staging. The normal game has 40 verified installed files; the test rig has 39. Existing historical documentation was retained. The exact RLO 0.45 DLL `2f360a36…` was already installed in both and was not changed.

| Installation | Mod directory |
| --- | --- |
| Normal Steam game | `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions\ModsLocal\PersistentCity` |
| Isolated Windows rig | `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\ModsLocal\PersistentCity` |

The existing configurations were preserved byte-for-byte, SHA-256 `abfecb215a3c3ea89e6a1137c7445726dde66947c06d361adb2be1337cf78c66`: Enabled and PhysicalEnabled true, 48 pedestrians, 24 vehicles, radius 120 m. The distribution's disabled-default config was not installed. Each folder contains an INSTALLATION.json explaining the configuration exception to the immutable package manifest.

Verified backups, staging and receipts are at `C:\Users\Peter\HART-Backups\persistent-city-modpack-20261021T100118Z`. `normal-before` / `agent1-before` are verified copies, and `normal-previous` / `agent1-previous` hold the replaced folders. The original rig manifest is retained as `agent-install-before.json`. Rollback must acquire the corresponding ownership, confirm the games are closed, preserve any newer changes and restore the desired mod folder; the rig manifest must also be reconciled with the restored files.

Only Persistent City's entries in `C:\BigAmbitions-AgentTests\agent-install.json` were refreshed. All 97 manifest entries verify, and the 58 unrelated entries retain their original hashes. Final manifest SHA-256: `552874bc2ba3b8ee61e1a6a0f74f1740621083e648a87be2b983f98c40d1a900`. Both ownership reservations were explicitly released after verification.

No game was launched or save loaded/written. This installation adds no native Windows gameplay/performance evidence; the [compatibility report's remaining limitations](2026-10-21-modpack-compatibility.md) still apply. Detailed preflight, inventories, commands, backups and independent readback are retained under ignored `build/modpack-compatibility-20261021/pc-install/`.
