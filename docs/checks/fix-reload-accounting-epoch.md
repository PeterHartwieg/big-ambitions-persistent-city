# Fix: reload refused with "Accounting input epoch was lost"

Status: implemented (offline); not verified by rig. Refs #95, Refs #202, Refs #203 (run mac-b-save-reload).

Cause: native time runs unheld from the native load until Persistent City's `TryStart` installs the clock hold,
which waits for RLO to report Ready. S1 was saved at logical minute 1921.9687 (08:01, 0.03 game minutes before
08:02). RLO's own log line after the load reads 08:02 before PC's startup read went Ready, so activation found the
snapshot one native minute behind and stopped. H1 was saved at 1983.011 (0.99 minutes of slack) and reloaded.
The skipped creation save and sequence 1 play no part.

Fix: at activation, a snapshot exactly one native minute behind, inside the same native hour, is caught up. The
save's own ending epoch must still have drained. Native time stays held while the city drains the crossed minute,
no closure or physical observation runs, and a save in that window is deferred like any save during
initialization. Two or more minutes, an hour or day boundary, a snapshot ahead of native, or unfinished earlier
work still stop as before.

## In-game checks

1. **Immediate reload after a fresh city (locked-PC).** Setup: full modpack including RLO and FoodTrucks, PC
   Enabled and PhysicalEnabled, autosave off in the isolated profile. Steps: start a fresh native new_game and let
   the creation save finish; once PC reports city running, make an ordinary save S1 and go straight to menu and
   load S1. Expected: PC reaches city running with 49/49 targets patched; no "Accounting input epoch was lost" and
   no `start refused (clock.city)`. If the loaded native minute is one past the saved one, the log has a
   "Load catch-up: native time entered minute ..." line followed by "Load catch-up finished at clock ...".
2. **Reload one in-game hour later (locked-PC).** Setup: the reloaded city from check 1, running. Steps: run one
   in-game hour, save H1, menu and load H1. Expected: city running with 49/49 targets patched; the resident identity
   digest of H1 equals that of the reloaded H1.

Either check may run on the Mac or on Windows.
