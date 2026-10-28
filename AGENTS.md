# Persistent City

A separate Big Ambitions mod with a required Real Life Overhaul dependency. Read README.md for scope and evidence.

- The current required release scope is `docs/implementation-plan.md`, reconciled on 24 October with GPT-6.1 Sol and Claude Opus 5.5; `docs/reviews/2026-10-24-scope-plan.md` records agreement. It supersedes older expansion requirements. Preserve persistent people, work/leisure and traffic; keep the release bounded. Implementation and validation status are recorded in README.md and CHECKS.md; scope approval does not authorize release or deployment to ordinary saves.

- Core is pure C# targeting netstandard2.1. Keep Unity and game calls in Runtime.
- The detached food shadow remains hypothetical and must never book native sales. The population phase has no economic effects unless the saved city explicitly owns a food scope.
- Historical authorization on 22 October covered automatic recurring food decisions, saved journey/settlement ownership and narrow native sales after the isolated pilot, with selected shops explicitly enrolled by `PopulationRuntime.StartFoodRoutines`. The 23 October continuation extended that opt-in scope to a verified menu of croissants, burgers and hot dogs under native equipment rules.
- On 24 October Peter approved the bounded implementation plan and explicitly instructed execution. This authorizes the current automatic ownership implementation for eligible player food businesses, represented consumers and plug-and-play startup without manual enrollment, superseding the earlier opt-in implementation restriction. Whole-business ownership must still transfer safely at native accounting boundaries, and unsupported businesses remain native-owned. This does not authorize writing rival finances, changing RLO, or deploying to Peter's ordinary save.
- Diagnostic helpers may create fixtures only in their named disposable FT cities on the owned rig. Preserve/restore preexisting rig files/preferences before releasing ownership; native evidence from those fixtures is separate from release approval.
- Performance test runs must disable autosave in the isolated test profile before timing begins. Record the original setting, verify autosave is off before each measured window (including after reload), and restore the original setting during cleanup. Keep manual saves outside measured windows; any save activity inside a window invalidates it as a clean performance sample. Save-specific profiling requires a separate explicitly requested run.
- Keep RLO access behind Compatibility. Validate its contract and refuse to start on incompatible data; never silently make replacement identities.
- Own state uses a versioned persistentcity namespace. Reject unreadable or future schemas without overwriting them.
- Reuse stable IDs and persist assignments. No identity changes on pool reuse, reload, camera movement or a different update cadence.
- No game DLLs, decompiled code, raw save files or credentials in Git. Reference locally available assemblies in place.
- Tests must check invariants and interrupted journeys, not just repeat formulas. Report offline checks separately from game evidence.
- This project is independent of RLO: do not modify its source or publish anything upstream as part of this work.
