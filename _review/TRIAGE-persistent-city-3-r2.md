# PR #3 independent review round 2 triage
HIGH_MEDIUM_FIX: 2

| ID | Severity | PR | file:line | Finding | Raised by (sol/astra/opus) | Decision (FIX / SKIP / QUESTION) | Reason |
|---|---|---|---|---|---|---|---|
| R2-1 | Medium | #3 | Runtime/OptionalStopEnvironment.cs:164 | Cold-cache sampling deferral permanently cancels valid saved stops | astra | FIX | Distinguish deferred validation from confirmed loss; preserve saved deadlines and choices, while blocking physical admission until validated. Test multiple saved entrances with actual shared cache and fixed frame. |
| R2-2 | Low | #3 | Runtime/OptionalStopEnvironment.cs:48 | New visits at cold entrances can miss a stop opportunity | astra | SKIP | New stops require validated geometry at admission; a chosen visit is not a guaranteed stop. No existing stop/continuation is lost here. Preserve the single admission opportunity rather than introduce delayed scheduling or retry/reroll behavior in this fix. Document the distinction explicitly. |
| R2-3 | Low | #3 | Runtime/PhysicalPopulation.cs:466 | Refused departure hold throws through simulation observer | opus M1 | SKIP | Reviewer could not construct a reachable refusal. Current synchronous commitment/lease guards exclude the proposed race; existing ordinary, meal and traffic-first regressions exercise these paths. Silently releasing authority would conceal a violated movement invariant. Future hypothetical changes do not justify weakening it. |
| R2-4 | Medium | #3 | Core/CitySimulator.cs:483 | Same-visit events erase persisted waiting conversation choices | opus L1 | FIX | Preserve accepted and declined choices when an event retains the same admitted visit; never rederive from a potentially changed rate. Test intermediate events, reload and later recruitment. |
| R2-5 | Low | #3 | README.md:111 | Short config summary omits fractional-value fallback | opus N1 | SKIP | The summary is not incorrect; linked detailed configuration docs already explicitly cover fractions, and converter tests cover them. No behavioral ambiguity in the authoritative instructions. |

Sol approves with no findings. Astra requests changes for R2-1; Opus approves with nits. Starting head dbf797ab06; normal base merge 32cc844 includes decisions head 9ee38e9. Approved 25% configuration and schema 16 retained. No new product question. Existing untracked RESULT.md remains untouched.

Completed: both FIX items implemented and tested in bd55eb4; evidence recorded in e904c5f. Branch pushed normally through e904c5f. Full suite: 1,368 unit tests, all 21 standalone regressions and all additional checks pass. Production and helper build with warnings as errors. No new questions; PR remains draft.
