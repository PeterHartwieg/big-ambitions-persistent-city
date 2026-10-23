# Persistent City

A separate Big Ambitions mod with a required Real Life Overhaul dependency. Read README.md for scope and evidence.

- Core is pure C# targeting netstandard2.1. Keep Unity and game calls in Runtime.
- The detached food shadow remains hypothetical and must never book native sales. The population phase has no economic effects unless the saved city explicitly owns a food scope.
- On 22 October Peter authorized automatic recurring food decisions, saved journey/settlement ownership and narrow native sales after the isolated pilot. `PopulationRuntime.StartFoodRoutines` explicitly enrolls selected single-product, player-owned full-service shops; no city is opted in by default. The diagnostic helper may create fixtures only in its named disposable FT city on the owned rig. Preserve/restore preexisting rig files/preferences before releasing ownership. The current task does not deploy to Peter's ordinary save, write rival finances, or change RLO.
- Keep RLO access behind Compatibility. Validate its contract and refuse to start on incompatible data; never silently make replacement identities.
- Own state uses a versioned persistentcity namespace. Reject unreadable or future schemas without overwriting them.
- Reuse stable IDs and persist assignments. No identity changes on pool reuse, reload, camera movement or a different update cadence.
- No game DLLs, decompiled code, raw save files or credentials in Git. Reference locally available assemblies in place.
- Tests must check invariants and interrupted journeys, not just repeat formulas. Report offline checks separately from game evidence.
- This project is independent of RLO: do not modify its source or publish anything upstream as part of this work.
