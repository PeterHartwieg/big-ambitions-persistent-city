# Agent speed, 9 November 2026

Peter decided these rules on 9 November 2026 after measuring the harness runs from 30 October to 9 November. Slices that ran in parallel waited 32–82 minutes each for the machine-wide dotnet lock. Each implementer ran the full suite 2–14 times (about 5 minutes per run since 9 November), and the harness ran it again after every fix round. Implementer contexts reached 230k–790k tokens. These rules replace the "Local tests" section of the [6 November delivery rules](2026-11-06-delivery-rules.md); everything else there stands.

## Tests: focused while working, full once before merge

- **While working**, and in every fix round, run only the tests in the test files your branch changed:

  ```sh
  python tools/build.py --game-managed "<Big Ambitions_Data/Managed>"
  python tools/test-focused.py
  ```

  `test-focused.py` finds the changed `tests/**/*.cs` files since the merge base with `origin/codex/persistent-city` (uncommitted and untracked files included) and runs their classes through `dotnet test --filter` (`FullyQualifiedName~.Class.`, or `~Class.` in the few files without a namespace). It does not take the dotnet lock. You can also run your own `--filter` without the lock.
- **The full suite runs once**, on the approved head before merge. The harness does this with `test_scope: final`, and only that run takes the dotnet lock (`wait-other-dotnet.ps1`). Implementers and fixers do not run the full suite, except for a broad refactor that the task names as one.
- **Wall-clock budgets in tests** go through `TimingBudget` (tests/Support/TimingBudget.cs) or `PerfGate.Under`, never a bare `Assert.True(elapsed < budget)`. Both are judged only with `PC_PERF_ASSERTS=1` (see "Wall-clock limits in unit tests" below). When judged, `TimingBudget` discounts a busy machine by the test thread's CPU time and retries up to five times. Counters and byte sizes stay exact asserts ([X3](../checks/x3.md)).
- A red full run goes to the local-test fixer, which is how the harness handles any red local test.

Harness `task.yaml` block for a slice (replace the worktree path):

```yaml
test_scope: final
local_tests:
  focused:
  - python tools/build.py --game-managed "C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"
  - python tools/test-focused.py
  full:
  - python tools/build.py --game-managed "C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"
  - powershell -NoProfile -ExecutionPolicy Bypass -File C:\Users\Peter\Coding_Projects\.agent-logs\wait-other-dotnet.ps1 -Repo "<worktree>"
  - cmd /c "set PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH% && dotnet test tests/PersistentCity.Tests.csproj"
```

Do not use `{changed_tests}`: the harness only recognises Python and JavaScript test names, so it would select nothing.

## Schema pins name their own constant

A slice that changes no wire asserts the constant of the schema it relies on (`CityState.PlayerHoldSchema`, for example), not `CityState.CurrentSchema`. Only the slice that introduces a writer pins that writer's golden hash. On 9 November, F5i's `Assert.Equal(35, CityState.CurrentSchema)` turned the integration head red once E5b4 took writer 36. That test was removed, and the same kind of assertion in F5h, F6h and F6k was removed before it could break at writer 37; `HeldPolicyExecutionTests` pins writer 36.

## Wall-clock limits in unit tests

Timed limits (p95 under 1 ms, no unit at or above 30 ms) failed whenever parallel slices loaded the machine, which sent green work to a fixer. Unit tests route them through `PerfGate.Under(ms, limit, label)` (`tests/PerfGate.cs`), which checks the limit only when `PC_PERF_ASSERTS=1`. The timed code, its correctness and allocation assertions always run. Perf evidence for a slice still goes in `docs/perf/<slice>.md`, measured on a quiet machine.

## Reading

- Task prompts name plan sections by heading, not whole plan files. Plans run to 50–90 KB.
- Find a section with `grep -n '^#' <plan>` and read only that line range. For source files over about 500 lines, `grep -n` first and read ranges.
- AGENTS.md and README carry no schema numbers. The current writer is `CityState.CurrentSchema` in `Core/Model.cs`.
- Review records in `docs/reviews/` are kept, except the rig evidence folder `docs/reviews/rig/` (26 MB of rig scripts, logs and JSON), which is at tag `docs-reviews-archive-2026-11-09`; links to it point to that tag on GitHub.
