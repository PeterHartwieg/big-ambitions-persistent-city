# Native command regression harness

Run `python3 tools/native-command-regression/run.py` from the repository. It extracts the production fixed-step, command and receipt methods into temporary deferred-physics test doubles, then checks that every fixed step starts a new ownership epoch, and owned/foreign body isolation, skipped grounded commands, strict pairing, receipt timing, horizontal/rotation holds and preserved vertical/native stopping behavior. This is an offline harness, not Unity execution.

Optionally pass the local game's Managed directory and the compiled PersistentCity DLL as two positional arguments. The additional read-only check inspects the real native FixedUpdate and invokes the production transpiler without installing a patch. It verifies the native grounded skip and rejects changed force-mode, body-slot, branch and rotation shapes. Game assemblies are referenced locally and never copied into this repository or package.

The normal check needs .NET SDK 10 and the pinned Harmony DLL already prepared by `tools/build.py`. Temporary compilation outputs are removed at exit.
