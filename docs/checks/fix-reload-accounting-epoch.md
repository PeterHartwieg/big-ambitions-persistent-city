# Fix: reload refused with "Accounting input epoch was lost"

Status: implemented (offline); not verified by rig. Refs #95, Refs #202, Refs #203 (run mac-b-save-reload).

Cause: native time runs unheld from the native load until Persistent City's `TryStart` installs the clock hold,
which waits for RLO to report Ready. S1 was saved at logical minute 1921.9687 (08:01, 0.03 game minutes before
08:02). RLO's own log line after the load reads 08:02 before PC's startup read went Ready, so activation found the
snapshot one native minute behind and stopped. H1 was saved at 1983.011 (0.99 minutes of slack) and reloaded.
The skipped creation save and sequence 1 play no part.

Fix: PC now holds native time from its first startup attempt on a save, before RLO is ready and behind the
loading screen, instead of only once RLO reports Ready ([Core/StartupClockHold.cs](../../Core/StartupClockHold.cs)).
No native minute can pass between the load and activation, so a save taken late in a minute loads in the same
minute. If startup still cannot proceed after 15 seconds outside the loading screen (for example RLO never becomes
ready), native time is released and logged, so the native city is never frozen for good. The activation refusals
are unchanged: a load that still crossed a minute, or a snapshot with unfinished earlier work, stops as before.

## In-game checks

1. **Immediate reload after a fresh city (locked-PC).** Setup: full modpack including RLO and FoodTrucks, PC
   Enabled and PhysicalEnabled, autosave off in the isolated profile. Steps: start a fresh native new_game and let
   the creation save finish; once PC reports city running, make an ordinary save S1 and go straight to menu and
   load S1. Expected: PC reaches city running with 49/49 targets patched; no "Accounting input epoch was lost" and
   no `start refused (clock.city)`; the native clock after activation reads the saved minute (08:01 for a save
   at 08:01), and no "startup is still waiting" warning is logged.
2. **Reload one in-game hour later (locked-PC).** Setup: the reloaded city from check 1, running. Steps: run one
   in-game hour, save H1, menu and load H1. Expected: city running with 49/49 targets patched; the resident identity
   digest of H1 equals that of the reloaded H1.
3. **RLO not ready releases native time (input-only).** Setup: as check 1 with RLO's Social Media option off so
   Flare never runs. Steps: load S1 and wait 20 seconds unpaused. Expected: one "Persistent City startup is still
   waiting after 15 seconds; native time is released" warning, then native time advances; the native game stays
   playable.

Any check may run on the Mac or on Windows.
