# City audit F7f — frozen reader maintenance cost, 5 November 2026, offline

Refs #94. This is the measurement named by the [F7 plan](../plans/f7-save-snapshots.md#4-slice-breakdown)'s F7f row: test time and lines per frozen reader. Section 3.9 asks for it so that a later retirement decision has data. This record is not a retirement decision. [Checks](../checks/f7f.md).

## Runtime cost

**Zero.** F7f changes no Runtime, Core, Persistence or Compatibility source. It adds no `Update` work, reflection, polling, scans or allocations, and the gameplay load path is unchanged. The mod-work gate (p95 < 5 ms, spikes ≥ 30 ms flagged) is unaffected by construction. No in-game sample was taken, and whole-frame cost is not measured or claimed. The four parked hotspots stay parked.

## Method

- **Lines.** Physical lines of each reader's Persistence files, using the patterns in the `ReaderCorpus.Readers` registry. `SaveCodec` counts `SaveCodec.cs` plus the unprefixed wire files it owns (18–25). Shared files (`SaveSession`, `SaveStore`, `MoneyJson`, receipts, authority, accounting stop) are excluded.
- **Test time.** The durations of the parameterized `ReaderCorpusTests` cases are summed per owning reader. For the over-cap case, a schema is assigned to the reader that owns it. The figure is the median of three foreground runs. Command: `dotnet test tests/PersistentCity.Tests.csproj --no-build --filter FullyQualifiedName~ReaderCorpusTests --logger trx`, Debug build, .NET 10, Windows development PC. The table then comes from `python tools/reader-corpus/maintenance_cost.py --trx <3 files>`, which starts no dotnet process.
- **Limits.** Per-case durations come from xUnit and include JIT warm-up for whichever case runs first in a class, so first-run outliers exist (for example 1,829 ms vs 651 ms for the migration theory in one earlier run). These are development-PC timings, not CI timings.

## Per frozen reader (after)

| Reader | Schemas | Files | Lines | Corpus files | Corpus bytes | Corpus test ms (median of 3) |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| LegacyPopulationCodec | 13-17 | 1 | 1240 | 10 | 35246 | 974.4 |
| PopulationSchema19 | 18-19 | 4 | 1650 | 4 | 14248 | 600.7 |
| PopulationSchema20 | 20-20 | 5 | 1714 | 2 | 7263 | 343.3 |
| PopulationSchema24 | 21-24 | 10 | 2133 | 8 | 29825 | 842.3 |
| SaveCodec | 25-25 | 10 | 2450 | 2 | 7729 | 397.3 |
| shared facts (packaging, hash pin) | - | - | - | - | - | 20.9 |

That gives 9,187 reader lines across 30 files, and 26 fixtures totalling 94,311 bytes. The whole corpus suite took 3,093 / 3,289 / 3,164 ms over the three runs, a median of 3,164 ms for 72 cases. Corpus test infrastructure adds 535 lines: `ReaderCorpus.cs` 127, `ReaderCorpusTests.cs` 245, `ReaderCorpusGenerator.cs` 76 and `maintenance_cost.py` 87. Per schema, the cost is about 160–490 ms of test time and 2 fixtures of 1.4–7.2 kB.

Per test method (median of 3 runs, ms): cross-schema matrix 939.8, over-cap ×5 871.4, migration pins 644.1, future/older/corrupt/overbound 491.6, projection 99.7, reader acceptance 95.5, packaging 13.2, changed-byte 7.6.

## Win taken (test time only)

The first version allocated one 256 Mi-character over-cap payload per schema (13 × about 512 MB of UTF-16). Every reader checks the length before it reads the schema, so one payload per reader, labelled with that reader's newest schema, covers the same bound for every reader.

| Measure (median of 3) | Before | After |
| --- | ---: | ---: |
| Over-cap test | 3,294.7 ms (13 cases) | 871.4 ms (5 cases) |
| Corpus suite, sum of per-method medians | 6,188.0 ms (80 cases) | 3,162.9 ms (72 cases) |

Every reader is still covered against both refusal and preservation. This affects test time only, with no mod-work, save or load effect.

## Adding a writer

A new writer adds one registry row and one corpus folder (2 fixtures of about 2–8 kB). It also adds five schema cases (about 0.2–0.5 s by the table above), one over-cap case for the newly frozen reader (about 175 ms), and one more relabel to every existing schema's cross-schema case. Re-run the script with three fresh TRX files and add a dated table here.
