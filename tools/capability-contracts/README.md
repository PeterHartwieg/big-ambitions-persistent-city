# capability-contracts

F8c/F8e stub harness ([F8 plan](../../docs/plans/f8-capability-boundaries.md) D8). It compiles `Runtime/NativeFoodContract.cs` (with the test-only `PC_FAULT_POINTS` symbol) and, from F8e, `Runtime/HookConflicts.cs` against game stubs and the Harmony 2.4.2 package that `tools/build.py` pins.

Run in the foreground: `dotnet run -c Release`. Each arm prints one line; a failed check throws and exits non-zero. Synthetic native surfaces, not game execution.
