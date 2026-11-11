# Fix: reload refused with "Accounting input epoch was lost"

Status: implemented (offline); not verified by rig. Refs #95, Refs #202, Refs #203 (run mac-b-save-reload).

Cause: native time runs unheld from the native load until Persistent City's `TryStart` installs the clock hold,
which waits for RLO to report Ready. S1 was saved at logical minute 1921.9687 (08:01, 0.03 game minutes before
08:02). RLO's own log line after the load reads 08:02 before PC's startup read went Ready, so activation found the
snapshot one native minute behind and stopped. H1 was saved at 1983.011 (0.99 minutes of slack) and reloaded.
The skipped creation save and sequence 1 play no part.

Fix: PC arms a startup guard on the native tick from its city-load callback (`OnLoadAsync`, which creates the
runtime for each load) and keeps it armed while no city is attached. The guard holds native time for any save no
city has bound yet ([Core/StartupClockHold.cs](../../Core/StartupClockHold.cs), `NativeCityClock.Arm`). It does
not depend on PopulationRuntime's first Update running before the first native tick, and RLO readiness no longer
matters. TryStart replaces the guard with the bound clock in one call. The hold belongs to one save: loading
another save before the first attaches starts over for it. If startup still cannot proceed after 15 seconds
outside the loading screen (for example RLO never becomes ready), native time is released and logged, so the
native city is never frozen for good. The
activation refusals are unchanged: a load that still crossed a minute, or a snapshot with unfinished earlier
work, stops as before.

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
4. **Switch saves during startup (locked-PC).** Setup: as check 3 (RLO not ready). Steps: load S1, and within
   10 seconds go to menu and load S2 (another PC city saved late in a minute) with RLO's Social Media option back
   on. Expected: S2 reaches city running at its saved minute; no "City clock already active" and no lost-epoch
   refusal.

Any check may run on the Mac or on Windows.

11 November 2026, Mac at `eaac521c`: check 1 PARTIAL (reload assertions pass; autosave-off setup unverified), check 2 PASS, checks 3–4 NOT RUN (RIG-RLOTOGGLE). Creation-save-init check 2 PASS. [Results](rig-2026-11-11-mac-results.md); [compact result](fix-reload-accounting-epoch-mac.json).
