using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

namespace PersistentCity.Tests;

/// <summary>One frozen population reader and the schema range it reads itself (older payloads are delegated
/// down the chain). A later writer adds one row here and one <c>ReaderCorpus/schema-NN</c> folder; older rows and
/// folders stay unchanged.</summary>
internal sealed record FrozenReader(string Name, int OwnMin, int OwnMax, Func<string?, SaveReadResult> Read, string[] Sources);

internal sealed record CorpusFixture(int Schema, string File, string Sha256, string Text);

internal static class ReaderCorpus
{
    public const int OldestSupportedSchema = 13;
    public const string Folder = "ReaderCorpus";
    /// <summary>F5c-lite: the corpus covers the development schemas 13-25. The product reader refuses all of them
    /// (DevelopmentSchema), so the chain and its writer-25 pin are reached through the preserved internal chain.</summary>
    public const int CorpusWriter = DevelopmentChain.ChainOutput;
    public static SaveReadResult Chain(string source) => SaveCodec.ReadDevelopmentChain(source);

    // Ordered oldest to newest. Sources list the Persistence files that implement each reader (maintenance cost).
    public static readonly FrozenReader[] Readers =
    {
        new("LegacyPopulationCodec", 13, 17, LegacyPopulationCodec.Read, new[] { "LegacyPopulationCodec.cs" }),
        new("PopulationSchema19", 18, 19, PopulationSchema19.Read, new[] { "PopulationSchema19*.cs" }),
        new("PopulationSchema20", 20, 20, PopulationSchema20.Read, new[] { "PopulationSchema20*.cs" }),
        new("PopulationSchema24", 21, 24, PopulationSchema24.Read, new[] { "PopulationSchema24*.cs" }),
        new("SaveCodec", 25, 25, source => SaveCodec.ReadDevelopmentChain(source!), new[] { "SaveCodec.cs", "PopulationWire18.cs", "AwarenessWire19.cs", "HeldTuningWire.cs",
            "WeatherWire20.cs", "RivalWire21.cs", "CampaignWire22.cs", "MenuWire23.cs", "OutflowWire24.cs", "NameWire25.cs" }),
    };

    public static FrozenReader ReaderFor(int schema) => Readers.Single(r => r.OwnMin <= schema && schema <= r.OwnMax);

    /// <summary>F7d: the compact writer is the first corpus entry outside the migration chain (no migration before
    /// release). Its reader reads only its own schema and refuses every chain schema as an older development city, so
    /// it is not a row of <see cref="Readers"/>, which tile the chain. Its folder is <c>schema-35</c> since F2e.</summary>
    public static readonly FrozenReader Compact = new("SaveCodec (compact)", CityState.CurrentSchema, CityState.CurrentSchema, SaveCodec.Read,
        new[] { "CompactWire34.cs", "SaveCodec.cs" });

    /// <summary>F2e: writer 35 took the compact wire. The <c>schema-34</c> folder stays as F7d wrote it: the frozen writer-34
    /// shape still rebuilds those bytes, and the product refuses them as an older development city.</summary>
    public const int RefusedCompact = CityState.CompactSchema;

    public static string Root => Path.Combine(AppContext.BaseDirectory, Folder);

    public static string SchemaFolder(int schema) => "schema-" + schema.ToString("00", System.Globalization.CultureInfo.InvariantCulture);

    public static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static string Sha256(string text) => Sha256(Encoding.UTF8.GetBytes(text));

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Loads one schema folder. A missing manifest, a missing file or a changed byte throws.</summary>
    public static IReadOnlyList<CorpusFixture> Load(int schema) => Load(Root, schema);

    public static IReadOnlyList<CorpusFixture> Load(string root, int schema)
    {
        string folder = Path.Combine(root, SchemaFolder(schema));
        var manifest = JObject.Parse(File.ReadAllText(Path.Combine(folder, "corpus.json")));
        if ((int)manifest["schema"]! != schema) throw new InvalidDataException(folder + ": manifest schema mismatch.");
        var fixtures = new List<CorpusFixture>();
        foreach (var entry in manifest["fixtures"]!.OfType<JObject>())
        {
            string file = (string)entry["file"]!, pinned = (string)entry["sha256"]!;
            byte[] bytes = File.ReadAllBytes(Path.Combine(folder, file));
            string actual = Sha256(bytes);
            if (actual != pinned)
                throw new InvalidDataException($"{SchemaFolder(schema)}/{file} changed: pinned sha256 {pinned}, found {actual}. The corpus is frozen; add a new fixture instead of editing one.");
            fixtures.Add(new CorpusFixture(schema, file, pinned, StrictUtf8.GetString(bytes)));
        }
        if (fixtures.Count == 0) throw new InvalidDataException(folder + ": no fixtures.");
        return fixtures;
    }

    /// <summary>Pinned SHA-256 of each fixture after the full chain migrates it and the current writer writes it.
    /// One file per writer, so a new writer adds a pin file instead of editing the schema folders.</summary>
    public static IReadOnlyDictionary<string, string> CurrentWriterPins(int writer)
    {
        var root = JObject.Parse(File.ReadAllText(Path.Combine(Root, "current-writer-" + writer + ".json")));
        if ((int)root["writer"]! != writer) throw new InvalidDataException("Current-writer pin file mismatch.");
        return root["migrated"]!.OfType<JProperty>().ToDictionary(p => p.Name, p => (string)p.Value!, StringComparer.Ordinal);
    }

    public static string Key(CorpusFixture fixture) => SchemaFolder(fixture.Schema) + "/" + fixture.File;

    /// <summary>Relabels the single top-level schema token without reformatting anything else.</summary>
    public static string Relabel(string text, int from, int to)
    {
        string token = "\"Schema\":" + from + ",";
        int at = text.IndexOf(token, StringComparison.Ordinal);
        if (at < 0 || text.IndexOf(token, at + 1, StringComparison.Ordinal) >= 0) throw new InvalidDataException("Expected exactly one schema token.");
        return text.Substring(0, at) + "\"Schema\":" + to + "," + text.Substring(at + token.Length);
    }
}

/// <summary>Writes a city through the frozen writer of an older schema. The recipes are the existing test builders
/// (<c>LegacyWire</c>, <c>LegacyMoneyWire</c>, <c>FoodCommerceTests.S17_Old</c>, <c>E4_Old</c> and <c>E5d_Wire24</c>),
/// restated here only to tolerate a city without food. They generated the corpus once; the tests then use them only to
/// project a migrated city back onto its source schema (the E5d check). They never regenerate a fixture.</summary>
internal static class ReaderCorpusRecipes
{
    public static string Project(CityState state, int schema)
    {
        if (schema >= 25) return DevelopmentChain.Write25(state);
        if (schema <= 17)
        {
            string legacy17 = LegacyWire.Write(state);
            if (schema == 17) return legacy17;
            var token = JObject.Parse(legacy17);
            if (schema <= 14) LegacyMoneyWire.ToLegacy(token, wholeCents: schema == 13);
            return LegacyMoneyWire.AsSchema(token, schema).ToString(Formatting.None);
        }
        int current = state.Schema; string policy = state.MenuPolicy;
        string wire;
        try
        {
            state.Schema = schema < 20 ? 19 : schema;
            if (schema >= 23) state.MenuPolicy = FoodMenuPolicy.Wire;
            wire = schema >= 21 ? DevelopmentChain.Legacy(state, () => PopulationSchema24.WriteForSchema(state, schema)) : schema == 20 ? DevelopmentChain.Legacy(state, () => PopulationSchema20.Write(state)) : DevelopmentChain.Legacy(state, () => PopulationSchema19.Write(state));
        }
        finally { state.Schema = current; state.MenuPolicy = policy; }
        if (schema != 18) return wire;
        var root = JObject.Parse(wire); root["Schema"] = 18;
        if (root["Food"] is JObject food)
        {
            food.Remove("AwarenessStartsDay");
            foreach (var meal in food["Residents"]!) if (meal[9] is JObject decision) decision.Remove("Awareness");
        }
        return root.ToString(Formatting.None);
    }
}
