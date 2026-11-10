# Save schema held at 1 until release, 9 November 2026

Peter, 9 November 2026: "We should hold it at 1 until release. Nobody has a save since its not released." He chose "plain 1, no check": no build stamp or format fingerprint replaces the bumps. He also asked: "make sure its documented somewhere so no agent starts reintroducing it".

## Rule

- The save schema is 1 until release. `CityState.CurrentSchema` in `Core/Model.cs` is 1. The other version numbers our own state writes into a save are 1 too: `FoodShadowState.CurrentSchema`, `TrafficKernelState.CurrentSchema` and the compact envelope's `CompactFormatVersion`.
- Never bump it. Never add a per-feature schema constant (`ResearchSchema`, `VendorSchema` and the like). Never plan a writer number.
- Change the save format in place. The writer writes every field and the reader reads every field, with no branch on a schema number.
- `SaveCodec.Read` reads schema 1. Any other number (older development builds wrote 2 to 36) returns the existing `DevelopmentSchema` status ("start a new city") and keeps the payload. There is no migration, no build stamp and no format fingerprint.
- Strict refusal of malformed and over-bound data, save-epoch safety and the pinned golden hashes stay. A deliberate format change re-pins the golden hashes in the same change; there is one golden hash per fixture.
- `tests/SchemaHeldAtOneTests.cs` fails if `CityState.CurrentSchema` is not 1.

## Why

No one has a released save. Each bump cost coordination between parallel slices (who takes the next free writer), a frozen writer and reader per schema, re-pinned goldens, and pins that broke when another slice took the next number.

## Accepted risk

A development city saved at schema 1 by an older build may misread or fail to load in a newer build, because the format changes in place. Rig and test cities are rebuilt at head. Peter accepted this.

## Scope

This overrides every plan or decision that says "bump the schema", "takes the next free writer", "writer N" or "schema change", including the [6 November delivery rules](2026-11-06-delivery-rules.md#no-save-migration-before-release) bullet that a switch-over day bumps the schema, the [no-migration re-scope](2026-11-06-no-migration-rescope.md) and the "Schema pins" section of the [agent-speed rules](2026-11-09-agent-speed.md#schema-pins-name-their-own-constant). Plans under `docs/plans/` are frozen and keep their old text; this record and AGENTS.md override them.

At release, schema 1 becomes the first real format, and normal bump-and-migrate starts from there.

X3 implemented this rule: [checks](https://github.com/PeterHartwieg/big-ambitions-persistent-city/blob/docs-history-2026-11-10/docs/checks/x3.md).
