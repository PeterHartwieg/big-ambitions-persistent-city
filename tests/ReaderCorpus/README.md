# Frozen reader corpus (F7f)

Golden population payloads for every supported schema, 13 to the current writer. The tests in `tests/ReaderCorpusTests.cs` read them with every frozen reader on each run, so a refactor cannot silently change what an old payload means. Refs #94; scope is the F7f row of `docs/plans/f7-save-snapshots.md`. Checks and the cross-schema matrix: `docs/checks/f7f.md`.

## Layout

- `schema-NN/` — one folder per schema. `full.json` and `minimal.json` are single-line UTF-8 payloads (no BOM, no trailing newline). `corpus.json` pins each file's SHA-256 and records provenance. `readerWhenFrozen` and `writerCommit` are informational only. The reader registry in `tests/ReaderCorpus.cs` decides which reader owns a schema.
- `current-writer-W.json` — SHA-256 of each fixture after the full chain migrates it and writer `W` writes it. Only the file for `CityState.CurrentSchema` is asserted. Older pin files stay as a record.
- `.gitattributes` marks this tree `-text`, so line-ending conversion cannot change a pinned byte.

## Provenance

Each fixture is a **synthetic offline payload**, not a native save. It was generated once at base `c64e3e2` (writer 25) from existing test builders:

- `full.json`: `FoodCommerceTests.S17_Full` (2 residents, factor 2, three meals, a selected decision with routes and offers, current-day outcomes).
- `minimal.json`: `CurrentSchemaOnlyTests.City` (1 resident, 1 home, no food scope).

Both pass through `ReaderCorpusRecipes.Project(city, N)`, which applies the frozen writer of schema N. That covers `LegacyWire`/`LegacyMoneyWire` for 13–17, `PopulationSchema19` for 18 (stripped like `E4_Old`) and 19, `PopulationSchema20`, `PopulationSchema24.WriteForSchema` for 21–24 and `SaveCodec` for 25. Generating twice gives identical bytes.

The rig's native fixtures (`pc-schema-13..16@1`, `pc-schema-25-ref@1`, `pc-representative-957@1` in big-ambitions-agent-rig `491ffbf`) are still pending production. Once a native payload is sealed, extract it and add it as a further file in the matching folder, recording its rig fixture reference and save SHA-256. Do not replace a synthetic file with it.

## Rules

- **Frozen.** Never edit, reformat or regenerate a fixture. A changed byte fails `ReaderCorpus.Load` with the pinned and found hashes. To cover something new, add a new file and its manifest entry.
- **No reader edits.** The corpus checks the readers; it is never a reason to change them.

## Adding a writer (schema W+1)

1. Freeze the schema-W codec as its own reader (E5d pattern). In `ReaderCorpus.Readers`, add one row for it and move the live `SaveCodec` row to `W+1`. In `ReaderCorpusRecipes.Project`, route schema W to the frozen writer.
2. Run once, with the variable naming this folder:
   `PC_READER_CORPUS_WRITE=<repo>/tests/ReaderCorpus dotnet test tests/PersistentCity.Tests.csproj --filter FullyQualifiedName~ReaderCorpus_Generate`.
   The generator writes only `schema-(W+1)/` and `current-writer-(W+1).json`, and never rewrites an existing folder.
3. Review the new pins, then record lines and test time per reader with `python tools/reader-corpus/maintenance_cost.py` in `docs/perf/`.

Older folders and their manifests stay untouched.
