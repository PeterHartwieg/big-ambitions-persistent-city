# Fix: new-game creation save during initialization

Status: implemented; creation-save check verified by rig (Mac); immediate manual-save reload failed. Refs #95, Refs #200 (run mac-creation-1).

A native save that arrives while Persistent City is still initializing, and hits no other refusal, is now deferred.
Nothing is written, the epoch is not poisoned, the sequence is not advanced and PC does not stop. PC logs
`Persistent City skipped the save during initialization; no Persistent City state was written: ...`.
The unit-on-stack, nested-save, replaced-dictionary and earlier-failure refusals are unchanged.

## In-game checks

1. **Creation save with PC enabled (locked-PC).** Setup: full modpack including RLO and FoodTrucks, PC Enabled and
   PhysicalEnabled, autosave state recorded. Steps: start a fresh native new_game and let the automatic creation
   save finish. Expected: the creation save completes; the log contains the "skipped the save during initialization"
   line and no "save epoch refused" line; PC reaches city running with 49/49 patch targets patched; no
   `start refused (clock.city)`.
2. **Save and reload (locked-PC).** Setup: the city from check 1, running. Steps: make an ordinary save, quit to
   menu, load it. Expected: the save contains the PC payload and a success save-epoch marker; the load attaches
   and the city runs with 49/49 targets patched.

Either check may run on the Mac or on Windows.

Rig result, 10 November 2026 (b), Mac at `18008ef3`: check 1 PASS (creation save deferred, city running, 49/49 targets); check 2 FAIL (schema-1 success epoch saved; reload stops PC: `Accounting input epoch was lost`). [Result](fix-creation-save-init-mac-b.json); [batched report](rig-2026-11-10b-mac-results.md).
