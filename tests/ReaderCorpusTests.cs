using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F7f: the writer's golden corpus. The schema is held at 1 until release, so the corpus has one folder; the
/// writer reads and rewrites it exactly as pinned, and any other schema number is refused with every key kept.</summary>
public sealed class ReaderCorpusTests
{
    private const string ReceiptKey = "persistentcity:food-sale-v1:corpus-intent";

    private static readonly (string File, string Builder, Func<CityState> City)[] Cities =
    {
        ("full.json", "ChangeJournalTests.Fixture(5150) (3-shop fresh city after 2,000 minutes, journal and meal-period causes)", () => ChangeJournalTests.Fixture(5150)),
        ("minimal.json", "CurrentSchemaOnlyTests.City (1 resident, 1 home, no food scope)", CurrentSchemaOnlyTests.City),
    };

    [Fact]
    public void ReaderCorpus_HasOneFolderForTheHeldSchema()
    {
        var folders = Directory.GetDirectories(ReaderCorpus.Root).Select(Path.GetFileName).ToArray();
        Assert.Equal(new[] { ReaderCorpus.SchemaFolder(CityState.CurrentSchema) }, folders);
    }

    [Fact]
    public void ReaderCorpus_AChangedFixtureByteFailsLoudly()
    {
        string temp = Path.Combine(Path.GetTempPath(), "pc-reader-corpus-" + Guid.NewGuid().ToString("N"));
        try
        {
            string name = ReaderCorpus.SchemaFolder(CityState.CurrentSchema);
            string folder = Path.Combine(temp, name);
            Directory.CreateDirectory(folder);
            foreach (string file in Directory.GetFiles(Path.Combine(ReaderCorpus.Root, name)))
                File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
            Assert.Equal(2, ReaderCorpus.Load(temp).Count);
            string path = Path.Combine(folder, "full.json");
            byte[] bytes = File.ReadAllBytes(path);
            int at = Array.IndexOf(bytes, (byte)'7'); Assert.True(at > 0);
            bytes[at] = (byte)'8'; // one digit: still valid JSON, still a plausible payload
            File.WriteAllBytes(path, bytes);
            var error = Assert.Throws<InvalidDataException>(() => ReaderCorpus.Load(temp));
            Assert.Contains(name + "/full.json changed", error.Message);
        }
        finally { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); }
    }

    /// <summary>Writes the writer's folder once. Runs only when <c>PC_READER_CORPUS_WRITE</c> names the
    /// <c>tests/ReaderCorpus</c> directory and never replaces an existing folder.</summary>
    [Fact]
    public void ReaderCorpus_GenerateFolderOnlyWhenExplicitlyRequested()
    {
        string? target = Environment.GetEnvironmentVariable("PC_READER_CORPUS_WRITE");
        if (string.IsNullOrEmpty(target)) return;
        string folder = Path.Combine(target, ReaderCorpus.SchemaFolder(CityState.CurrentSchema));
        if (Directory.Exists(folder)) return;
        Directory.CreateDirectory(folder);
        var fixtures = new Newtonsoft.Json.Linq.JArray();
        foreach (var (file, builder, city) in Cities)
        {
            string text = SaveCodec.Write(city());
            Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(text).Status);
            File.WriteAllBytes(Path.Combine(folder, file), new UTF8Encoding(false).GetBytes(text));
            fixtures.Add(new Newtonsoft.Json.Linq.JObject
            {
                ["file"] = file, ["sha256"] = ReaderCorpus.Sha256(text), ["characters"] = text.Length, ["builder"] = builder,
                ["recipe"] = "SaveCodec.Write(city) (fresh city in the single-pass writer, no migration)",
                ["kind"] = "synthetic offline payload from test builders; not a native save",
            });
        }
        var manifest = new Newtonsoft.Json.Linq.JObject
        {
            ["schema"] = CityState.CurrentSchema, ["reader"] = "SaveCodec.Read", ["writerCommit"] = null,
            ["rigFixture"] = "none: a fresh city on the rig is X3 batched rig check 1", ["fixtures"] = fixtures,
        };
        File.WriteAllText(Path.Combine(folder, "corpus.json"), manifest.ToString(Newtonsoft.Json.Formatting.Indented) + "\n");
    }

    [Fact]
    public void ReaderCorpus_CorpusLoadsAsPinnedRewritesItsOwnBytesAndOtherSchemasAreRefused()
    {
        var fixtures = ReaderCorpus.Load();
        Assert.Equal(Cities.Select(c => c.File).OrderBy(f => f, StringComparer.Ordinal), fixtures.Select(f => f.File).OrderBy(f => f, StringComparer.Ordinal));
        foreach (var fixture in fixtures)
        {
            string key = ReaderCorpus.Key(fixture);
            Assert.StartsWith(SaveCodec.BodyPrefix, fixture.Text);
            var read = SaveCodec.Read(fixture.Text);
            Assert.True(read.Status == SaveReadStatus.Loaded, key + ": " + read.Error);
            Assert.Equal(CityState.CurrentSchema, read.SourceSchema);
            // Decode -> re-encode is byte-identical, and the builder still produces the pinned bytes.
            Assert.Equal(fixture.Text, SaveCodec.Write(read.State!));
            Assert.Equal(fixture.Text, SaveCodec.Write(Cities.Single(c => c.File == fixture.File).City()));
            AssertRefusedAndPreserved(fixture.Text.Substring(0, fixture.Text.Length - 1), read.State!, SaveReadStatus.Invalid, key + " truncated");
            // Older development builds wrote 2 to 36; any other number is refused the same way, never migrated.
            foreach (int other in new[] { 0, 2, 33, 36, 37 })
                AssertRefusedAndPreserved(fixture.Text.Replace(SaveCodec.BodyPrefix, "{\"Schema\":" + other + ",\"Capabilities\":"), read.State!,
                    SaveReadStatus.DevelopmentSchema, key + " relabelled " + other);
        }
    }

    private static void AssertRefusedAndPreserved(string payload, CityState valid, SaveReadStatus expected, string label)
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
