# Delivery rules, 6 November 2026

Peter decided these rules on 6 November 2026 after a readability proposal by Claude and two independent reviews (gpt-6-astra and Claude Opus 5.5). They replace older instructions where they differ. The short form is in [AGENTS.md](../../AGENTS.md).

## In-game checks are batched

In-game checks are collected for one batched rig run against the development head (merge rule v2). They do not gate a slice's merge. Native findings become follow-up fix slices, not reverts. The batched rig run must actually happen before release; it is listed as remaining work in the [roadmap](../roadmap.md).

Each slice writes its checks under the heading "In-game checks for the next batched rig" in its own checks file. This replaces older wording that called native proof a merge blocker, for example for city-audit F1.

## Local tests

Superseded on 9 November 2026: focused tests while working, the full suite once before merge. See [agent speed](2026-11-09-agent-speed.md).

Before handing over a slice, run:

1. the build: `python tools/build.py --game-managed "<Big Ambitions_Data/Managed>"`;
2. the unit tests: `dotnet test tests/PersistentCity.Tests.csproj`;
3. the slice's own new checks.

The `tools/*-regression` suites are not run before release. They stay in the repository as historical evidence.

## No save migration before release

Superseded on 9 November 2026 by [save schema held at 1](2026-11-09-schema-one.md): never bump the schema, including for a switch-over day; change the format in place.

Persistent City has not been released, and development cities are disposable.

- A new schema refuses cities written by an older development schema. The refusal has its own status that tells the player to start a new city. It must not look like corruption, and it keeps the stored payload unchanged.
- A behaviour change that would need a switch-over day (a "starts day" field) bumps the schema instead.
- These guarantees stay: save-epoch safety, strict refusal of future, unknown, malformed and over-bound data without overwriting it, and a pinned golden hash for the current writer, so any change to the save shape forces a schema bump.
- If a migration is ever needed, there is one release migration story, written once at release time.

F5c-lite is the first slice under this rule. This decision does not itself remove the existing migration code.

## Slices edit no shared docs

A slice edits only `docs/checks/<slice>.md` and `docs/perf/<slice>.md`. It does not edit README.md, CHECKS.md, docs/roadmap.md, AGENTS.md or plan documents. The coordinator updates the README status table and the CHECKS index after merge. Plans are frozen once approved; owner decisions made later go into a dated file in this folder.

## Unchanged

- Every slice still has a perf file, `docs/perf/<slice>.md` (30 October practice).
- The performance gate in the [roadmap](../roadmap.md#performance-gate) is unchanged.
- Release still waits for the remaining work in the roadmap, and there is one release.
