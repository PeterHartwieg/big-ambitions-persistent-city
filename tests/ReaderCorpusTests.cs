using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F7f: every development schema from 13 has a frozen golden corpus. X1 removed the pre-26 reader chain, so the
/// JSON folders 13-25 remain only as real older payloads that the product reader refuses as DevelopmentSchema with every
/// key preserved; the compact writer's own folder reads and rewrites exactly as pinned.</summary>
public sealed class ReaderCorpusTests
{
    public static IEnumerable<object[]> Schemas() =>
        Enumerable.Range(ReaderCorpus.OldestSupportedSchema, ReaderCorpus.NewestJsonSchema - ReaderCorpus.OldestSupportedSchema + 1).Select(s => new object[] { s });

    private const string ReceiptKey = "persistentcity:food-sale-v2:corpus-intent";

    [Fact]
    public void ReaderCorpus_PackagingCoversEveryCorpusSchemaWithOneFolder()
    {
        var folders = Directory.GetDirectories(ReaderCorpus.Root).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var expected = Schemas().Select(s => ReaderCorpus.SchemaFolder((int)s[0])).Append(ReaderCorpus.SchemaFolder(ReaderCorpus.RefusedCompact))
            .Append(ReaderCorpus.SchemaFolder(ReaderCorpus.RefusedHoldCompact)).Append(ReaderCorpus.SchemaFolder(ReaderCorpus.Compact.OwnMin)).ToArray();
        Assert.Equal(expected, folders);
        foreach (var row in Schemas()) Assert.Equal(2, ReaderCorpus.Load((int)row[0]).Count);
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

    // ---- F7d: the compact writer's own corpus (schema 34 by F7d, 35 by F2e, 36 since E5b4; no migration) ----

    private static readonly (string File, string Builder, Func<CityState> City)[] CompactCities =
    {
        ("full.json", "ChangeJournalTests.Fixture(5150) (3-shop fresh city after 2,000 minutes, journal and meal-period causes)", () => ChangeJournalTests.Fixture(5150)),
        ("minimal.json", "CurrentSchemaOnlyTests.City (1 resident, 1 home, no food scope)", CurrentSchemaOnlyTests.City),
    };

    /// <summary>Writes the current compact writer's folder (<c>schema-36</c> since E5b4) once. Runs only when <c>PC_READER_CORPUS_WRITE</c> names the <c>tests/ReaderCorpus</c>
    /// directory and never replaces an existing folder.</summary>
    [Fact]
    public void ReaderCorpus_GenerateCompactFolderOnlyWhenExplicitlyRequested()
    {
        string? target = Environment.GetEnvironmentVariable("PC_READER_CORPUS_WRITE");
        if (string.IsNullOrEmpty(target)) return;
        string folder = Path.Combine(target, ReaderCorpus.SchemaFolder(ReaderCorpus.Compact.OwnMin));
        if (Directory.Exists(folder)) return;
        Directory.CreateDirectory(folder);
        var fixtures = new Newtonsoft.Json.Linq.JArray();
        foreach (var (file, builder, city) in CompactCities)
        {
            string text = SaveCodec.Write(city());
            Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(text).Status);
            File.WriteAllBytes(Path.Combine(folder, file), new UTF8Encoding(false).GetBytes(text));
            fixtures.Add(new Newtonsoft.Json.Linq.JObject
            {
                ["file"] = file, ["sha256"] = ReaderCorpus.Sha256(text), ["characters"] = text.Length, ["builder"] = builder,
                ["recipe"] = "SaveCodec.Write(city) (fresh city in the compact writer, no migration)",
                ["kind"] = "synthetic offline payload from test builders; not a native save",
            });
        }
        var manifest = new Newtonsoft.Json.Linq.JObject
        {
            ["schema"] = ReaderCorpus.Compact.OwnMin, ["readerWhenFrozen"] = ReaderCorpus.Compact.Name, ["writerCommit"] = null,
            ["rigFixture"] = "none: a fresh compact city on the rig is F7d batched rig check 1", ["fixtures"] = fixtures,
        };
        File.WriteAllText(Path.Combine(folder, "corpus.json"), manifest.ToString(Newtonsoft.Json.Formatting.Indented) + "\n");
    }

    [Fact]
    public void ReaderCorpus_CompactCorpusLoadsAsPinnedRewritesItsOwnBytesAndTheWriter33ReaderReportsFuture()
    {
        var fixtures = ReaderCorpus.Load(ReaderCorpus.Compact.OwnMin);
        Assert.Equal(CompactCities.Select(c => c.File).OrderBy(f => f, StringComparer.Ordinal), fixtures.Select(f => f.File).OrderBy(f => f, StringComparer.Ordinal));
        foreach (var fixture in fixtures)
        {
            string key = ReaderCorpus.Key(fixture);
            Assert.StartsWith(SaveCodec.CompactPrefix, fixture.Text);
            var read = ReaderCorpus.Compact.Read(fixture.Text);
            Assert.True(read.Status == SaveReadStatus.Loaded, key + ": " + read.Error);
            Assert.Equal(CityState.CurrentSchema, read.SourceSchema);
            // Decode -> re-encode is byte-identical, and the builder still produces the pinned bytes.
            Assert.Equal(fixture.Text, SaveCodec.Write(read.State!));
            Assert.Equal(fixture.Text, SaveCodec.Write(CompactCities.Single(c => c.File == fixture.File).City()));
            // The frozen writer-33 reader sees a newer schema and keeps it.
            Assert.Equal(SaveReadStatus.Future, SaveCodec.ReadWriter33(fixture.Text).Status);
            string future = fixture.Text.Replace(SaveCodec.CompactPrefix, "{\"Schema\":37,\"f\":");
            Assert.Equal(SaveReadStatus.Future, SaveCodec.Read(future).Status);
            AssertCompactRefusedAndPreserved(future, read.State!, SaveReadStatus.Future, key + " future");
            AssertCompactRefusedAndPreserved(fixture.Text.Substring(0, fixture.Text.Length - 1), read.State!, SaveReadStatus.Invalid, key + " truncated");
            AssertCompactRefusedAndPreserved(fixture.Text.Replace(SaveCodec.CompactPrefix, "{\"Schema\":33,\"f\":"), read.State!, SaveReadStatus.DevelopmentSchema, key + " relabelled 33");
            AssertCompactRefusedAndPreserved(fixture.Text.Replace(SaveCodec.CompactPrefix, "{\"Schema\":34,\"f\":"), read.State!, SaveReadStatus.DevelopmentSchema, key + " relabelled 34");
            AssertCompactRefusedAndPreserved(fixture.Text.Replace(SaveCodec.CompactPrefix, "{\"Schema\":35,\"f\":"), read.State!, SaveReadStatus.DevelopmentSchema, key + " relabelled 35");
        }
    }

    /// <summary>F2e: F7d's writer-34 folder is unchanged; the frozen writer-34 shape still builds its bytes, and the current
    /// reader refuses each one as an older development city with the "start a new city" status and keeps every key.
    /// E5b4: the same holds for F2e's writer-35 folder.</summary>
    [Theory]
    [InlineData(ReaderCorpus.RefusedCompact)]
    [InlineData(ReaderCorpus.RefusedHoldCompact)]
    public void ReaderCorpus_TheWriter34CompactCorpusIsRefusedAsAnOlderDevelopmentCityAndStillRebuildsItsBytes(int schema)
    {
        var fixtures = ReaderCorpus.Load(schema);
        Assert.Equal(CompactCities.Select(c => c.File).OrderBy(f => f, StringComparer.Ordinal), fixtures.Select(f => f.File).OrderBy(f => f, StringComparer.Ordinal));
        foreach (var fixture in fixtures)
        {
            string key = ReaderCorpus.Key(fixture);
            Assert.StartsWith("{\"Schema\":" + schema + ",\"f\":", fixture.Text);
            var city = CompactCities.Single(c => c.File == fixture.File).City();
            Assert.Equal(fixture.Text, schema == ReaderCorpus.RefusedCompact ? DevelopmentChain.Write34(city) : DevelopmentChain.Write35(city));
            var read = SaveCodec.Read(fixture.Text);
            Assert.Equal(SaveReadStatus.DevelopmentSchema, read.Status);
            Assert.Equal(schema, read.SourceSchema);
            Assert.Contains("start a new city", read.Error);
            AssertCompactRefusedAndPreserved(fixture.Text, CurrentSchemaOnlyTests.City(), SaveReadStatus.DevelopmentSchema, key);
        }
    }

    [Theory]
    [MemberData(nameof(Schemas))]
    public void ReaderCorpus_TheCompactReaderRefusesEveryChainSchemaAsAnOlderDevelopmentCity(int schema)
    {
        foreach (var fixture in ReaderCorpus.Load(schema))
        {
            var read = ReaderCorpus.Compact.Read(fixture.Text);
            Assert.Equal(SaveReadStatus.DevelopmentSchema, read.Status);
            Assert.Equal(schema, read.SourceSchema);
            Assert.Contains("start a new city", read.Error);
            AssertCompactRefusedAndPreserved(fixture.Text, CurrentSchemaOnlyTests.City(), SaveReadStatus.DevelopmentSchema, ReaderCorpus.Key(fixture));
        }
    }

    private static void AssertCompactRefusedAndPreserved(string payload, CityState valid, SaveReadStatus expected, string label)
    {
        var read = SaveCodec.Read(payload);
        Assert.True(read.Status == expected, $"{label}: {read.Status} {read.Error}"); Assert.Null(read.State);
        var data = Keys(payload);
        var before = data.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var session = SaveSession.Open(data, valid.PopulationKey);
        Assert.Equal(expected, session.InitialReadResult.Status);
        Assert.False(session.WriteTo(data, valid).Success, label);
        Assert.False(SaveStore.WriteTo(data, valid).Success, label);
        Assert.Equal(before.Count, data.Count);
        foreach (var (key, value) in before) Assert.Same(value, data[key]);
    }

    private static Dictionary<string, string> Keys(string payload) => new(StringComparer.Ordinal)
    {
        [SaveCodec.StateKey] = payload, ["rlo:state"] = "owned by RLO", [ReceiptKey] = "Pending",
        ["persistentcity:food-authority-v1"] = "authority manifest", ["othermod:state"] = "foreign",
    };
}
