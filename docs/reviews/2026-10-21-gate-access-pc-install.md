# Reviewed gate-index build installed on PC — 21 October 2026

Peter requested installation in both his normal game and the isolated Windows test rig after the [four-model review rounds](2026-10-21-gate-access-panel.md). Both installations are complete. The normal game was updated at 09:34 CEST and the test rig at 09:36 CEST; final independent readback completed at 09:37 CEST.

The installed DLL is `3f87e7f927be074ca649f7e1403bf803ac146cb62c35528655bc36877be8f0a9`, the exact reviewed revision of the 0.6.2 development tree. It replaces `bebe1f5e9f622c0ed6188b1faed4400aa1eb4cda239d74d6045b2a2f25a0455d` in both locations. The transferred archive SHA-256 is `57db51b0e9c8635bbf17af420c83f716784c8cb4ad0a8ec1e061c76f7af514b9`.

| Installation | Mod directory | Verification |
| --- | --- | --- |
| Normal Steam game | `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions\ModsLocal\PersistentCity` | All 31 installed files match the prepared update |
| Agent test rig | `C:\Users\Peter\AppData\LocalLow\Hovgaard Games\Big Ambitions Agent Tests\ModsLocal\PersistentCity` | All 30 installed files match the prepared update; all 88 rig manifest entries verify |

Each existing `persistent-city.json` was preserved byte-for-byte, SHA-256 `abfecb215a3c3ea89e6a1137c7445726dde66947c06d361adb2be1337cf78c66`. Both retain `Enabled=true`, `PhysicalEnabled=true`, 48 pedestrians, 24 vehicles and a 120-metre radius. The package's default disabled configuration was not copied over these settings. Installed build manifests record each target and the retained configuration; README, CHECKS and the gate-review reports were refreshed. Existing historical documentation was retained.

The rig was stopped but still reserved by `hart-group-opus-playtest`. Peter explicitly authorized release of that reservation. The update acquired its own ownership, replaced the package, refreshed only Persistent City's entries in `agent-install.json`, verified the 58 unrelated entries unchanged, and released ownership. Final rig manifest SHA-256: `b13a77af8a62e3dad4f7a0d9a54f428c0d4349a26bc15f5b2bae8f9c30ed8694`. Both normal and agent locks are free with zero pending commands.

Verified copies of both previous mod folders, the original rig manifest, staged files and installation receipts are retained at `C:\Users\Peter\HART-Backups\persistent-city-gate-index-20261021T073301Z`. `normal-previous` and `agent1-previous` contain the previous installations. A rollback must first check that neither instance is running, acquire the respective ownership, preserve any newer changes, and restore the relevant mod folder; restoring the rig also requires updating its manifest to the restored files.

No game was launched, save loaded or save file written. This installation verifies files and settings; it adds no in-game performance or gameplay evidence. The reviewed distribution ZIP remains immutable and disabled by default. Local commands, preparation inventories, handoff receipt and independent readback are retained under ignored `build/gate-access-review-20261021/pc-install/`.
