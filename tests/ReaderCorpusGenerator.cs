using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed partial class FoodCommerceTests
{
    // Mainline commit whose CityState.CurrentSchema first equalled the schema (null: the schema existed only on a
    // stacked branch and was first merged together with a later writer). Provenance only; nothing is built from it.
    private static readonly IReadOnlyDictionary<int, string?> CorpusWriterCommits = new Dictionary<int, string?>
    {
        [13] = "63248e4", [14] = "7357f78", [15] = null, [16] = "21db30b", [17] = "8b05227", [18] = "3b8f7ec", [19] = "692b8b5",
        [20] = "5877453", [21] = null, [22] = null, [23] = "2bd9006", [24] = "fb958ae", [25] = "4ee0c8d",
    };

    private static readonly (string File, string Builder, Func<CityState> City)[] CorpusCities =
    {
        ("full.json", "FoodCommerceTests.S17_Full (2 residents, factor 2, three meals, active decision with routes, outcomes)", () => S17_Full().city.State),
        ("minimal.json", "CurrentSchemaOnlyTests.City (1 resident, 1 home, no food scope)", CurrentSchemaOnlyTests.City),
    };

    /// <summary>Writes the frozen reader corpus once. Runs only when <c>PC_READER_CORPUS_WRITE</c> names the
    /// <c>tests/ReaderCorpus</c> directory and refuses to replace an existing schema folder, so ordinary test runs
    /// never regenerate or edit a pinned fixture. A later writer adds its folder with the same entry point.</summary>
    [Fact]
    public void ReaderCorpus_GenerateOnlyWhenExplicitlyRequested()
    {
        string? target = Environment.GetEnvironmentVariable("PC_READER_CORPUS_WRITE");
        if (string.IsNullOrEmpty(target)) return;
        var pins = new JObject();
        for (int schema = ReaderCorpus.OldestSupportedSchema; schema <= CityState.CurrentSchema; schema++)
        {
            string folder = Path.Combine(target, ReaderCorpus.SchemaFolder(schema));
            bool exists = Directory.Exists(folder);
            var fixtures = new JArray();
            foreach (var (file, builder, city) in CorpusCities)
            {
                string text = exists ? File.ReadAllText(Path.Combine(folder, file), Encoding.UTF8) : ReaderCorpusRecipes.Project(city(), schema);
                var read = SaveCodec.Read(text); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
                pins[ReaderCorpus.SchemaFolder(schema) + "/" + file] = ReaderCorpus.Sha256(SaveCodec.Write(read.State!));
                if (exists) continue;
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, file), new UTF8Encoding(false).GetBytes(text));
                fixtures.Add(new JObject
                {
                    ["file"] = file, ["sha256"] = ReaderCorpus.Sha256(text), ["characters"] = text.Length,
                    ["builder"] = builder,
                    ["recipe"] = "ReaderCorpusRecipes.Project(city, " + schema + ")",
                    ["kind"] = "synthetic offline payload from test builders; not a native save",
                });
            }
            if (exists) continue;
            var manifest = new JObject
            {
                ["schema"] = schema, ["readerWhenFrozen"] = ReaderCorpus.ReaderFor(schema).Name,
                ["writerCommit"] = CorpusWriterCommits[schema],
                ["rigFixture"] = schema <= 16 ? "pc-schema-" + schema + "@1: pending production in big-ambitions-agent-rig (491ffbf); not sealed, no native payload yet"
                    : schema == 25 ? "pc-schema-25-ref@1 / pc-representative-957@1: pending production in big-ambitions-agent-rig (491ffbf); not sealed"
                    : "none planned",
                ["fixtures"] = fixtures,
            };
            File.WriteAllText(Path.Combine(folder, "corpus.json"), manifest.ToString(Formatting.Indented) + "\n");
        }
        string pinFile = Path.Combine(target, "current-writer-" + CityState.CurrentSchema + ".json");
        if (!File.Exists(pinFile))
            File.WriteAllText(pinFile, new JObject { ["writer"] = CityState.CurrentSchema, ["migrated"] = pins }.ToString(Formatting.Indented) + "\n");
    }
}
