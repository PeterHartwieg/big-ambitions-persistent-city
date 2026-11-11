# Combined rig checklist — historical link

Rig reports up to 11 November (e) and the F8, F9 and fix evidence moved to the tag `tools-history-2026-11-11` (X5). The original checklist is retained in [docs history](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/rig-2026-11-06-checklist.md).

Results of the first batched run (head `e9995e5`): [rig-2026-11-06-results.md in docs history](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/rig-2026-11-06-results.md).
Results of the intermediate batched run (head `c2fc1b0`): [rig-2026-11-09-results.md](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/tools-history-2026-11-11/docs/checks/rig-2026-11-09-results.md).

10 November Mac batch: [results](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/tools-history-2026-11-11/docs/checks/rig-2026-11-10-mac-results.md), [F8h check 1 input](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/tools-history-2026-11-11/docs/checks/f8h-check1-modpack-owners-mac.json).

10 November PC batch: [intermediate results](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/tools-history-2026-11-11/docs/checks/rig-2026-11-10-results.md), [F8h check 1 owner inventory](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/tools-history-2026-11-11/docs/checks/f8h-check1-modpack-owners-pc.json) verified by rig at `b3e5a910`; post-9-Nov checks tracked in the report.

10 November Mac batch (b): [creation-save fix and re-run results](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/tools-history-2026-11-11/docs/checks/rig-2026-11-10b-mac-results.md), [fix checks](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/tools-history-2026-11-11/docs/checks/fix-creation-save-init-mac-b.json); creation save PASS, immediate manual-save reload FAIL at `18008ef3`.

11 November 2026, Mac at `eaac521c`: [reload accounting-epoch fix and re-run rows](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/tools-history-2026-11-11/docs/checks/rig-2026-11-11-mac-results.md) — 2 passed / 6 partial / 0 failed / 150 not run; isolated restore/hash check and lease release complete.

11 November 2026 (b), Mac at `2f1e8136`: [F8h conflict refusals and autosave re-runs](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/tools-history-2026-11-11/docs/checks/rig-2026-11-11b-mac-results.md).

11 November 2026 PC at `11217a8f`: [Windows-only rows, autosave-off re-runs and F8h first-sale attempt](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/tools-history-2026-11-11/docs/checks/rig-2026-11-11-pc-results.md) ? 6 passed / 8 partial / 0 failed / 170 not run; restore/hash verification and lease release complete.

11 November 2026 Windows combined-batch subset at `97448c04`: F7 check 2 / F4 G12 **PARTIAL**, fresh day-1 957 head city, Default/MidnightSave/RecoverSave capture-to-encode **1.917?3.563%**, synchronous portrait/screenshot-request methods **8.395?20.787 ms**. F7 check 7 Default cold load restored 957/957. Historical before arm and process-memory profiler fields remain unavailable. [Save-phase tables and subsequent runnable rows](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/tools-history-2026-11-11/docs/checks/rig-2026-11-11-batch-results.md).

11 November 2026 Windows combined-batch completion at `97448c04`: F7 check 2 / F4 G12 **PARTIAL**, all three SaveTypes on day 1 and day 3 (957 residents; 957/1,914 visitors), capture/encode **1.054-3.563%**, portrait/screenshot-request method window **8.395-20.787 ms**; all six checkpoints cold-loaded with F7a initialization timings. F4 G10 head subset partial, native 120m skip completed. F7 check 9 lunch union **FAIL**, provisional (PC shared): p95 6.6857 ms, five >=30 ms spikes (max 193.3404 ms); this is not F4 G4. [Full report and grouped NOT RUN rows](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/tools-history-2026-11-11/docs/checks/rig-2026-11-11-batch-results.md).

11 November 2026 (e), Mac at `86c2e92f`: [#70 --rig-test / Mac shop_fixture smoke and autosave-off re-runs](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/tools-history-2026-11-11/docs/checks/rig-2026-11-11e-mac-results.md) — 4 passed / 1 partial / 0 failed / 1 not run (targeted subset); reload-epoch checks 1–2 PASS, X3-1 PARTIAL; restore/hash verification and lease release complete.
