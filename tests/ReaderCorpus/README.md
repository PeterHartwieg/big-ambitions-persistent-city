# Reader corpus (F7f)

Golden population payloads of the writer. The save schema is held at 1 until release ([decision](../../docs/decisions/2026-11-09-schema-one.md)), so there is one folder, `schema-01`. X3 removed the folders of the development schemas 13 to 35 and the old `schema-36` folder; the record is in `docs/checks/x3.md`.

## Layout

- `schema-01/`: `full.json` and `minimal.json` are single-line UTF-8 payloads (no BOM, no trailing newline). `corpus.json` pins each file's SHA-256 and records provenance.
- `retired-pcc-c-minimal.json`: the same 1-resident city as the F7d columnar writer stored it before P-A. Not a corpus fixture; `SaveRoundTripTests` checks that it is refused as an older development city and kept.
- `.gitattributes` marks this tree `-text`, so line-ending conversion cannot change a pinned byte.

## Provenance

Each fixture is a **synthetic offline payload**, not a native save: `ChangeJournalTests.Fixture(5150)` (`full.json`) and `CurrentSchemaOnlyTests.City` (`minimal.json`), written by `SaveCodec.Write`.

## Rules

- A changed byte fails `ReaderCorpus.Load` with the pinned and found hashes. `tests/ReaderCorpusTests.cs` checks that the writer reads and rewrites each payload exactly, and that the same payload relabelled with any other schema number is refused with every key kept.
- A deliberate format change re-pins the folder in the same change: delete `schema-01`, then run
  `PC_READER_CORPUS_WRITE=<repo>/tests/ReaderCorpus dotnet test tests/PersistentCity.Tests.csproj --filter FullyQualifiedName~ReaderCorpus_GenerateFolderOnlyWhenExplicitlyRequested`.
  Never add a folder for a new schema number.
