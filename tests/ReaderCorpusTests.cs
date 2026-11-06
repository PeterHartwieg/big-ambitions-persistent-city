using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F7f: every supported schema has a frozen golden corpus that its own frozen reader, every newer reader in
/// the chain and the current codec read exactly as pinned, and every foreign, future, older, corrupt or overbound
/// variant is refused with the original string and every other key preserved.</summary>
public sealed class ReaderCorpusTests
{
    public static IEnumerable<object[]> Schemas() =>
        Enumerable.Range(ReaderCorpus.OldestSupportedSchema, ReaderCorpus.CorpusWriter - ReaderCorpus.OldestSupportedSchema + 1).Select(s => new object[] { s });

    private const string ReceiptKey = "persistentcity:food-sale-v2:corpus-intent";

    [Fact]
    public void ReaderCorpus_PackagingCoversEverySupportedSchemaWithOneFolderAndOneReader()
    {
        var folders = Directory.GetDirectories(ReaderCorpus.Root).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var expected = Schemas().Select(s => ReaderCorpus.SchemaFolder((int)s[0])).ToArray();
        Assert.Equal(expected, folders);
        // Readers tile 13..current with no gap or overlap; the newest is the live codec.
        int next = ReaderCorpus.OldestSupportedSchema;
        foreach (var reader in ReaderCorpus.Readers) { Assert.Equal(next, reader.OwnMin); Assert.True(reader.OwnMax >= reader.OwnMin); next = reader.OwnMax + 1; }
        Assert.Equal(ReaderCorpus.CorpusWriter + 1, next);
        Assert.Equal("SaveCodec", ReaderCorpus.Readers[^1].Name);
        var pins = ReaderCorpus.CurrentWriterPins(ReaderCorpus.CorpusWriter);
        var keys = new List<string>();
        foreach (var row in Schemas())
            foreach (var fixture in ReaderCorpus.Load((int)row[0])) keys.Add(ReaderCorpus.Key(fixture));
        Assert.Equal(keys.OrderBy(k => k, StringComparer.Ordinal), pins.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void ReaderCorpus_AChangedFixtureByteFailsLoudly()
    {
        string temp = Path.Combine(Path.GetTempPath(), "pc-reader-corpus-" + Guid.NewGuid().ToString("N"));
        try
        {
            string folder = Path.Combine(temp, ReaderCorpus.SchemaFolder(20));
            Directory.CreateDirectory(folder);
            foreach (string file in Directory.GetFiles(Path.Combine(ReaderCorpus.Root, ReaderCorpus.SchemaFolder(20))))
                File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
            Assert.Equal(2, ReaderCorpus.Load(temp, 20).Count);
            string path = Path.Combine(folder, "full.json");
            byte[] bytes = File.ReadAllBytes(path);
            int at = Array.IndexOf(bytes, (byte)'7'); Assert.True(at > 0);
            bytes[at] = (byte)'8'; // one digit: still valid JSON, still a plausible payload
            File.WriteAllBytes(path, bytes);
            var error = Assert.Throws<InvalidDataException>(() => ReaderCorpus.Load(temp, 20));
            Assert.Contains("schema-20/full.json changed", error.Message);
        }
        finally { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); }
    }

    [Theory]
    [MemberData(nameof(Schemas))]
    public void ReaderCorpus_OwnAndNewerFrozenReadersAcceptOlderReadersReportFuture(int schema)
    {
        foreach (var fixture in ReaderCorpus.Load(schema))
            foreach (var reader in ReaderCorpus.Readers)
            {
                var read = reader.Read(fixture.Text);
                Assert.Equal(schema, read.SourceSchema);
                if (reader.OwnMax >= schema)
                {
                    Assert.True(read.Status == SaveReadStatus.Loaded, $"{reader.Name} {ReaderCorpus.Key(fixture)}: {read.Error}");
                    Assert.Equal(reader.OwnMax, read.State!.Schema);
                }
                else { Assert.Equal(SaveReadStatus.Future, read.Status); Assert.Null(read.State); }
            }
    }

    [Theory]
    [MemberData(nameof(Schemas))]
    public void ReaderCorpus_FullChainMigratesToTheCurrentWriterExactlyAsPinned(int schema)
    {
        var pins = ReaderCorpus.CurrentWriterPins(ReaderCorpus.CorpusWriter);
        foreach (var fixture in ReaderCorpus.Load(schema))
        {
            var read = ReaderCorpus.Chain(fixture.Text);
            Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            Assert.Equal(schema, read.SourceSchema); Assert.Equal(ReaderCorpus.CorpusWriter, read.State!.Schema);
            string current = DevelopmentChain.Write(read.State);
            Assert.Equal(pins[ReaderCorpus.Key(fixture)], ReaderCorpus.Sha256(current));
            Assert.Equal(current, DevelopmentChain.Write(ReaderCorpus.Chain(current).State!));
            // F5c-lite: the product refuses every development schema with its own status and message, keeps every
            // key byte-identical and never replaces the payload; no migration is published.
            var data = Keys(fixture.Text);
            var before = data.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var session = SaveSession.Open(data, read.State.PopulationKey);
            Assert.Equal(SaveReadStatus.DevelopmentSchema, session.InitialReadResult.Status);
            Assert.Equal(schema, session.InitialReadResult.SourceSchema);
            Assert.Contains("older development build; start a new city", session.InitialReadResult.Error);
            Assert.False(session.WriteTo(data, read.State).Success); Assert.False(SaveStore.WriteTo(data, read.State).Success);
            foreach (var (k, value) in before) Assert.Same(value, data[k]);
            Assert.Equal(before.Count, data.Count);
        }
    }

    [Theory]
    [MemberData(nameof(Schemas))]
    public void ReaderCorpus_MigratedCityProjectsBackOntoItsSourceBytes(int schema)
    {
        foreach (var fixture in ReaderCorpus.Load(schema))
        {
            var read = ReaderCorpus.Chain(fixture.Text); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            Assert.Equal(fixture.Text, ReaderCorpusRecipes.Project(read.State!, schema));
        }
    }

    /// <summary>Every relabel to another supported schema is refused by the full chain and by every reader, unless the
    /// two schemas have the same wire shape for that fixture's content (for example whole-cent money read as schema 13 or
    /// 14, or a city without food under schemas that differ only in food fields). Such a relabel must then read to exactly
    /// the same current city as the true label, so the label carries no meaning there. The full fixture is refused under
    /// every label except the 13/14 money pair. Recorded matrix: docs/checks/f7f.md.</summary>
    [Theory]
    [MemberData(nameof(Schemas))]
    public void ReaderCorpus_CrossSchemaVariantsAreRefusedOrMeaningIdenticalAndRefusalsPreserveEveryKey(int schema)
    {
        foreach (var fixture in ReaderCorpus.Load(schema))
        {
            var valid = ReaderCorpus.Chain(fixture.Text).State!;
            string truth = DevelopmentChain.Write(ReaderCorpus.Chain(fixture.Text).State!);
            foreach (var row in Schemas())
            {
                int other = (int)row[0]; if (other == schema) continue;
                string variant = ReaderCorpus.Relabel(fixture.Text, schema, other);
                string label = $"{ReaderCorpus.Key(fixture)} as {other}";
                var chain = ReaderCorpus.Chain(variant);
                foreach (var reader in ReaderCorpus.Readers)
                {
                    var read = reader.Read(variant);
                    var expected = other > reader.OwnMax ? SaveReadStatus.Future : chain.Status;
                    Assert.True(read.Status == expected, $"{label} via {reader.Name}: {read.Status} {read.Error}");
                }
                if (chain.Status == SaveReadStatus.Loaded)
                {
                    Assert.True(fixture.File != "full.json" || Math.Min(schema, other) == 13 && Math.Max(schema, other) == 14, label);
                    Assert.Equal(other, chain.SourceSchema);
                    Assert.Equal(truth, DevelopmentChain.Write(chain.State!));
                }
                else AssertRefusedAndPreserved(variant, valid, SaveReadStatus.Invalid, label);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Schemas))]
    public void ReaderCorpus_FutureOlderCorruptAndOverboundVariantsAreRefusedAndPreserved(int schema)
    {
        foreach (var fixture in ReaderCorpus.Load(schema))
        {
            var valid = ReaderCorpus.Chain(fixture.Text).State!;
            string text = fixture.Text, key = ReaderCorpus.Key(fixture);
            foreach (int future in new[] { ReaderCorpus.CorpusWriter + 1, 99 })
            {
                string variant = ReaderCorpus.Relabel(text, schema, future);
                Assert.All(ReaderCorpus.Readers, r => Assert.Equal(SaveReadStatus.Future, r.Read(variant).Status));
                Assert.Equal(future, ReaderCorpus.Chain(variant).SourceSchema);
                AssertRefusedAndPreserved(variant, valid, SaveReadStatus.Future, key + " future " + future);
            }
            foreach (int older in new[] { ReaderCorpus.OldestSupportedSchema - 1, 1, 0, -1 })
            {
                string variant = ReaderCorpus.Relabel(text, schema, older);
                Assert.All(ReaderCorpus.Readers, r => Assert.Equal(SaveReadStatus.Invalid, r.Read(variant).Status));
                Assert.Contains("Unsupported experimental state schema", ReaderCorpus.Chain(variant).Error);
                AssertRefusedAndPreserved(variant, valid, SaveReadStatus.Invalid, key + " older " + older);
            }
            // SchemaReadable: the top-level schema token still parses, so a reader older than the payload reports
            // Future (refuse and preserve) without inspecting the damage; every other reader reports Invalid.
            var corrupt = new (string Name, string Text, string Error, bool SchemaReadable)[]
            {
                ("truncated", text.Substring(0, text.Length - 1), "Invalid state", false),
                ("half", text.Substring(0, text.Length / 2), "Invalid state", false),
                ("trailing", text + "{}", "Invalid state", false),
                ("unknown field", "{\"Corpus\":0," + text.Substring(1), "Unknown field: Corpus", true),
                ("duplicate schema", "{\"Schema\":" + schema + "," + text.Substring(1), "Duplicate field: Schema", false),
                ("schema string", ReaderCorpus.Relabel(text, schema, 0).Replace("\"Schema\":0,", "\"Schema\":\"" + schema + "\","), "Invalid state schema", false),
                ("empty", "", "Invalid state", false),
            };
            foreach (var (name, variant, error, readable) in corrupt)
            {
                Assert.All(ReaderCorpus.Readers, r => Assert.Equal(readable && r.OwnMax < schema ? SaveReadStatus.Future : SaveReadStatus.Invalid, r.Read(variant).Status));
                Assert.Contains(error, ReaderCorpus.Chain(variant).Error);
                AssertRefusedAndPreserved(variant, valid, SaveReadStatus.Invalid, key + " " + name);
            }
            // One over the per-citizen visit cap: each reader's token check refuses before allocation.
            string overVisits = OverVisits(text, schema);
            foreach (var reader in ReaderCorpus.Readers)
            {
                var read = reader.Read(overVisits);
                if (reader.OwnMax < schema) { Assert.Equal(SaveReadStatus.Future, read.Status); continue; }
                Assert.Equal(SaveReadStatus.Invalid, read.Status); Assert.Contains("Oversized collection", read.Error);
            }
            AssertRefusedAndPreserved(overVisits, valid, SaveReadStatus.Invalid, key + " visits+1");
        }
    }

    public static IEnumerable<object[]> ReaderNewestSchemas() => ReaderCorpus.Readers.Select(r => new object[] { r.OwnMax });

    // Every reader checks the length before it reads the schema, so one 256 Mi-character payload per reader (labelled
    // with that reader's newest schema) covers the bound; per-schema copies cost about 250 ms each without adding a case.
    [Theory]
    [MemberData(nameof(ReaderNewestSchemas))]
    public void ReaderCorpus_OneCharacterOverTheInputCapIsRefusedAndPreserved(int schema)
    {
        var fixture = ReaderCorpus.Load(schema)[0];
        var valid = ReaderCorpus.Chain(fixture.Text).State!;
        // Trailing whitespace keeps it otherwise valid JSON; only the length bound refuses it.
        string over = fixture.Text + new string(' ', SaveCodec.MaxInputCharacters + 1 - fixture.Text.Length);
        Assert.Equal(SaveCodec.MaxInputCharacters + 1, over.Length);
        Assert.All(ReaderCorpus.Readers, r =>
        {
            var read = r.Read(over);
            Assert.Equal(SaveReadStatus.Invalid, read.Status); Assert.Contains("input size limit", read.Error);
        });
        AssertRefusedAndPreserved(over, valid, SaveReadStatus.Invalid, ReaderCorpus.Key(fixture) + " chars+1");
    }

    private static Dictionary<string, string> Keys(string payload) => new(StringComparer.Ordinal)
    {
        [SaveCodec.StateKey] = payload, ["rlo:state"] = "owned by RLO", [ReceiptKey] = "Pending",
        ["persistentcity:food-authority-v1"] = "authority manifest", ["othermod:state"] = "foreign",
    };

    private static void AssertRefusedAndPreserved(string payload, CityState valid, SaveReadStatus expected, string label)
    {
        var read = ReaderCorpus.Chain(payload);
        Assert.True(read.Status == expected, $"{label}: {read.Status} {read.Error}"); Assert.Null(read.State);
        var data = Keys(payload);
        var before = data.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var session = SaveSession.Open(data, valid.PopulationKey);
        // F5c-lite: the product refuses every development schema as DevelopmentSchema; other refusals keep their status.
        var product = SaveCodec.Read(payload).Status;
        Assert.NotEqual(SaveReadStatus.Loaded, product);
        // The header probe stops at the first Schema token; the full reader also refuses duplicate root fields.
        if (SaveCodec.DevelopmentSchemaOf(payload) != null) Assert.True(product == SaveReadStatus.DevelopmentSchema || product == SaveReadStatus.Invalid, label);
        Assert.Equal(product, session.InitialReadResult.Status);
        Assert.False(session.WriteTo(data, valid).Success, label);
        Assert.False(SaveStore.WriteTo(data, valid).Success, label);
        Assert.Equal(before.Count, data.Count);
        foreach (var (key, value) in before) Assert.Same(value, data[key]);
    }

    /// <summary>Replaces the first citizen's <c>RecentVisits</c> array, whatever it holds, with one record over the cap.
    /// Schema 18+ visits are compact tokens: version 1, binary64 minute (1440.0), place index 000, activity 0.</summary>
    internal static string OverVisits(string text, int schema)
    {
        const string field = "\"RecentVisits\":[";
        int start = text.IndexOf(field, StringComparison.Ordinal); Assert.True(start >= 0, field);
        int end = MatchingBracket(text, start + field.Length - 1);
        string visit = schema >= 18 ? "\"1" + "4096800000000000" + "000" + "0\"" : "{\"Activity\":0,\"AtMinute\":1440.0,\"PlaceId\":\"home\"}";
        return text.Substring(0, start) + field + string.Join(",", Enumerable.Repeat(visit, SaveCodec.MaxRecentVisits + 1)) + "]" + text.Substring(end + 1);
    }

    // Index of the bracket closing the one at `open`, skipping JSON string contents.
    private static int MatchingBracket(string text, int open)
    {
        int depth = 0; bool inString = false;
        for (int i = open; i < text.Length; i++)
        {
            char c = text[i];
            if (inString) { if (c == '\\') i++; else if (c == '"') inString = false; continue; }
            if (c == '"') inString = true;
            else if (c == '[' || c == '{') depth++;
            else if ((c == ']' || c == '}') && --depth == 0) return i;
        }
        throw new InvalidDataException("Unclosed array.");
    }

    /// <summary>The over-cap mutation must also work on a checkpoint where every resident already has visit history
    /// (an ordinary native payload): every citizen gets three visits, the city is written through schema N's frozen
    /// writer, and the populated payload loads before its over-cap variant is refused and preserved.</summary>
    [Theory]
    [MemberData(nameof(Schemas))]
    public void ReaderCorpus_VisitCapVariantAlsoBuildsFromPopulatedVisitHistories(int schema)
    {
        var fixture = ReaderCorpus.Load(schema).Single(f => f.File == "full.json");
        var state = ReaderCorpus.Chain(fixture.Text).State!;
        foreach (var citizen in state.Citizens)
            for (int n = 0; n < 3; n++) citizen.RecentVisits.Add(new VisitRecord { AtMinute = state.Minute - 3 + n, PlaceId = "home", Activity = Activity.Home });
        string populated = ReaderCorpusRecipes.Project(state, schema);
        Assert.DoesNotContain("\"RecentVisits\":[]", populated);
        var loaded = ReaderCorpus.Chain(populated); Assert.True(loaded.Status == SaveReadStatus.Loaded, loaded.Error);
        Assert.All(loaded.State!.Citizens, c => Assert.Equal(3, c.RecentVisits.Count));
        string over = OverVisits(populated, schema);
        foreach (var reader in ReaderCorpus.Readers.Where(r => r.OwnMax >= schema))
        {
            var read = reader.Read(over);
            Assert.Equal(SaveReadStatus.Invalid, read.Status); Assert.Contains("Oversized collection", read.Error);
        }
        AssertRefusedAndPreserved(over, loaded.State, SaveReadStatus.Invalid, ReaderCorpus.Key(fixture) + " populated visits+1");
    }
}
