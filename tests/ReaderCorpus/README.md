# Frozen reader corpus (F7f)

Golden population payloads for the development schemas 13 to 25 and for the compact writers 34, 35 and the current one. Refs #94; the original F7f scope is the F7f row of `docs/plans/f7-save-snapshots.md`, and its checks are in `docs/checks/f7f.md`.

X1 removed the pre-26 reader chain that migrated schemas 13 to 25 (no migration before release). The JSON folders `schema-13` to `schema-25` stay as real older payloads: `tests/ReaderCorpusTests.cs` checks that the product reader refuses each one as DevelopmentSchema and keeps every key unchanged. The X1 record is in `docs/checks/x1.md`.

## Layout

- `schema-NN/` — one folder per schema. `full.json` and `minimal.json` are single-line UTF-8 payloads (no BOM, no trailing newline). `corpus.json` pins each file's SHA-256 and records provenance. `readerWhenFrozen` and `writerCommit` are informational only.
- `.gitattributes` marks this tree `-text`, so line-ending conversion cannot change a pinned byte.

## Provenance

Each fixture is a **synthetic offline payload**, not a native save.

- `schema-13` to `schema-25` were generated once at base `c64e3e2` (writer 25) from `FoodCommerceTests.S17_Full` (`full.json`) and `CurrentSchemaOnlyTests.City` (`minimal.json`) through the frozen writer of each schema. X1 removed those writers; the bytes are unchanged.
- `schema-34`, `schema-35` and the current compact folder come from `ChangeJournalTests.Fixture(5150)` (`full.json`) and `CurrentSchemaOnlyTests.City` (`minimal.json`). The frozen writer-34 and writer-35 shapes still rebuild their folders' bytes.

## Rules

- **Frozen.** Never edit, reformat or regenerate a fixture. A changed byte fails `ReaderCorpus.Load` with the pinned and found hashes. To cover something new, add a new file and its manifest entry.
- **No reader edits.** The corpus checks the readers; it is never a reason to change them.

## Adding a compact writer (schema W+1)

Run once, with the variable naming this folder:
`PC_READER_CORPUS_WRITE=<repo>/tests/ReaderCorpus dotnet test tests/PersistentCity.Tests.csproj --filter FullyQualifiedName~ReaderCorpus_GenerateCompactFolderOnlyWhenExplicitlyRequested`.
The generator writes only the new writer's folder and never rewrites an existing one. Older folders and their manifests stay untouched.
