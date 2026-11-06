# No-migration re-scope of the remaining F5, F6 and F7 rows, 6 November 2026

This record applies Peter's [delivery rules](2026-11-06-delivery-rules.md#no-save-migration-before-release) to the slice rows that had not started. The model is F5c-lite (PR #127, schema 26). The edited rows are in section 4 of [F5](../plans/f5-population.md), [F6](../plans/f6-economy.md) and [F7](../plans/f7-save-snapshots.md). Section 5 of each plan now says one bump per schema slice, no migration.

Common rule for every schema slice: fresh cities are created in the new shape. A city written by an older development schema is refused with the distinct "start a new city" status. The refusal keeps the stored payload, is not reported as corruption and leaves PC-owned food businesses in a defined native state. Each new writer has a pinned golden hash. F7b's save epoch and strict refusal of future, unknown, malformed and over-bound data stay as they are. Slices that do not bump the schema add a test that the current writer's golden hash is unchanged.

All other scope items, tests, perf measurements and dependencies stay as written, except where noted below.

## Per row

### F5 ([plan](../plans/f5-population.md#4-slice-breakdown))

- **F5c** (pointer only). The row's scope and test cells are now a pointer to F5c-lite, schema 26, PR #127, and this record. The original migration scope is in the plan's Git history.
- **F5d.** Removed: the `IndependentPolicyStartsDay` switch-over, persons switching at their first safe event, the legacy excursion path, the "after the switch" anchor rule, switch-day employment seating, switch-timing tests, conservation and income continuity across the switch day. Replaced by: a schema bump; fresh cities run independent schedules from their first day; older-schema refusal; a golden hash; no anchor read at all; conservation from city creation; income continuity across reload; save/reload with an in-flight excursion, uncertain receipt, rival settlement and held decision; a fresh-seed resident-trace comparison for the arrival guards; employment seating at city creation. Added perf: fresh-city bytes, records and read/write latency against section 5.
- **F5e.** Removed: nothing migration-specific was listed, but the row relied on F5d's switch-day seating for existing cities. Replaced by: a schema bump, because a city seated under `BusinessAllocation = 16` can hold more workers than the new capacity; fresh cities are seated by the new rule at creation; older-schema refusal; a golden hash; the loss/find-rate check runs as a fresh-seed comparison against the base. Dependency added: after F5d's schema merges, because schema slices are serial.
- **F5g.** Removed: "an over-target migrated city". Replaced by: a fresh city seeded above target creates no commuters and retires none; current-writer golden hash unchanged. Fill starting at the next day boundary is its ordinary behaviour, so F5g does not bump.
- **F5h.** Removed: migration from the writer-25 reference fixture through the switch and seven days. Replaced by: a fresh seeded reference city run for seven days with save/reload each day; older-schema refusal; current-writer golden hash unchanged.
- **F5i.** Removed: nothing. Added: bodies are not saved, so the save shape does not change; current-writer golden hash unchanged.
- **Section 5.** Removed: the 13–24 → 25 → new migration chain, migrated consumers, the legacy flag, `LegacyAnchorId`, `IndependentPolicyStartsDay` and frozen readers' caps. Replaced by: one bump per schema slice, the refusal shape, fresh-city byte budgets (same arithmetic) and the expected schema order.

### F6 ([plan](../plans/f6-economy.md#4-slice-breakdown))

- **F6d.** Removed: `WageStartsDay`, exact predecessor round-trip, migration of IDs and money, "not recorded" labels on migrated days, empty fingerprints at migration, projection back to the predecessor and migration latency. Replaced by: fresh cities in the new shape with zero accounts and empty journals; older-schema refusal; a golden hash; a fresh-seed trace/state-hash comparison against the predecessor writer showing behaviour unchanged while inactive. The 16-character `WageStartsDay` budget line is gone; the worst case is now 5,694,336 B and still does not fit today's headroom.
- **F6e.** Removed: the `WageStartsDay` switch-over and its no-double-income tests (shift in progress on that day, reload across that day). Replaced by: a schema bump; fresh cities accrue shift wages from their first day; older-schema refusal; a golden hash; full attendance on a fresh seed reproduces the base weekly income within rounding; a shift in progress across save/reload.
- **F6f.** Removed: nothing. Added: no bump by default, because it starts at the next hourly boundary in any city of the current schema. If pooling must move existing per-person money into a household reserve, that is a switch-over and F6f bumps instead, serial after F6e. Current-writer golden hash unchanged when it does not bump.
- **F6g.** Removed: "windows with migrated days labeled not recorded". Replaced by: a schema bump, so every accepted city recorded counters from its first day; older-schema refusal; a golden hash; a fresh city's first week labeled partial. Section 5 previously allowed F6g a separate version only if its shapes were not frozen with F6d; it now always bumps.
- **F6h.** Removed: nothing. Added: runs on a fresh F5 population fixture in the current schema; current-writer golden hash unchanged.
- **Section 5.** Removed: "Shape and migration (A2/E5d style)", the migration-day legacy posting and F7 migrating F6d's fields. Replaced by: one bump per schema slice, the refusal shape, rows for F6e/F6f/F6g and the expected order.

### F7 ([plan](../plans/f7-save-snapshots.md#4-slice-breakdown))

- **F7c.** Removed: the fixed "writer 25" target. Replaced by: byte identity with the current writer at implementation time (its pinned golden hash), on fixtures built as fresh cities in that schema; older-schema refusal is part of the unchanged reader.
- **F7d.** Removed: migration from the then-current writer through its frozen reader, the A2/E5d migration cases, the dependency on F7f's frozen reader corpus (it existed only to support that migration) and the F6-schema dependency beyond F6d's shape. Replaced by: the format from the [F7d compact-format decision](2026-11-06-f7d-compact-format.md) (candidate C) in the next-free schema; fresh cities created in the compact format; older-schema refusal, JSON writers included; a golden hash; a fresh-seed state-hash comparison between the predecessor JSON writer and the compact writer (IDs, money, held inputs, receipts and journeys exact). Added perf: report the decision record's open pause, allocation and stress-load gaps as measured. The decision record's sentence that F7d migrates from the current writer is superseded.
- **F7e.** Removed: nothing. Added: it encodes whatever the current writer is, changes neither schema nor epoch, and the overlapped payload is byte-identical to the synchronous one (golden hash unchanged).
- **Section 5.** Removed: "Migration shape (A2/E5d style)", frozen predecessors and projection back through the predecessor writer. Replaced by: one bump per schema slice, the refusal shape and the fresh-seed equality check. F7b's text is unchanged apart from one sentence that the epoch stays the same under every later bump.

## Not re-scoped here

These are outside the task's rows and are left for the coordinator. Where they conflict with this record, this record and the delivery rules win:

- F5 section 3, "Migration from represented consumers".
- F6 section 3 text on `WageStartsDay`.
- F6c's test "counters equal full rescans after every mutation, reload and migration".
- F7 section 3.9, "Historical codec maintenance".
- The F7f row (frozen reader corpus). Without migration, its value is questionable. F7d no longer depends on it.
- The section 4 intro sentences in F5 that make rig proof a merge gate. The batched-rig rule replaces them.

## Schema order

Schema numbers are allocated at merge. Only one schema slice is in flight at a time. The next one branches after the previous one merges. Expected order:

1. **F5c-lite**: schema 26, in flight (PR #127).
2. **F5d**: independent schedules, version bump on F5c-lite's shape.
3. **F5e**: workplace capacity, version bump. F5d and F5e may swap places, but they never run together.
4. **F6d**: economic ledger shape, inactive. The F6b conditional held integers fold into this bump.
5. **F6e**: attendance wages, version bump in place of `WageStartsDay`.
6. **F6f**: only if pooling needs a switch-over. Otherwise it does not bump.
7. **F6g**: change journal and meal-period counters, version bump.
8. **F7d**: compact writer, last, so it is built once on the finished shape. It may move ahead of F6e/F6g once F6d has merged. Those two are version-only bumps and would then bump inside the compact writer.

These do not bump: F5f, F5g, F5h, F5i, F6a, F6b (unless its conditional applies), F6c, F6h, F7a, F7b (side key only), F7c, F7e, F7f and F7p.

## Checks for this re-scope

Status: implemented (offline); not verified by rig.

This is a docs-only slice. The task's file rule limits this slice to `docs/plans/` and `docs/decisions/`. The checks and perf notes are therefore here, not in `docs/checks/` or `docs/perf/`.

- Evidence: `git diff --check`, the changed-paths check (only `docs/plans/` and `docs/decisions/`) and `python tools/docs-links/check.py` pass on this branch.
- Perf: not applicable. No code changed, so there is nothing to measure.
- In-game checks for the next batched rig: none from this slice. Each re-scoped slice lists its own checks when implemented, including a fresh city loading, saving and reloading in its schema, and an older-schema FT city refused with the "start a new city" status (pending capability: load a named older-schema FT city and read the refusal status).
