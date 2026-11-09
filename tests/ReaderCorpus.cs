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

/// <summary>A population reader and the schema range it reads itself.</summary>
internal sealed record FrozenReader(string Name, int OwnMin, int OwnMax, Func<string?, SaveReadResult> Read, string[] Sources);

internal sealed record CorpusFixture(int Schema, string File, string Sha256, string Text);

internal static class ReaderCorpus
{
    public const int OldestSupportedSchema = 13;
    public const string Folder = "ReaderCorpus";
    /// <summary>The JSON folders cover the development schemas 13-25. X1 removed the reader chain that migrated them; the
    /// product reader refuses every one as DevelopmentSchema, and the folders stay as real refused payloads.</summary>
    public const int NewestJsonSchema = DevelopmentChain.ChainOutput;

    /// <summary>F7d: the compact writer's reader reads only its own schema and refuses every older schema as an older
    /// development city. Its folder is <c>schema-36</c> since E5b4.</summary>
    public static readonly FrozenReader Compact = new("SaveCodec (compact)", CityState.CurrentSchema, CityState.CurrentSchema, SaveCodec.Read,
        new[] { "CompactWire34.cs", "SaveCodec.cs" });

    /// <summary>F2e: writer 35 took the compact wire. The <c>schema-34</c> folder stays as F7d wrote it: the frozen writer-34
    /// shape still rebuilds those bytes, and the product refuses them as an older development city.</summary>
    public const int RefusedCompact = CityState.CompactSchema;

    /// <summary>E5b4: writer 36 took the compact wire with the executable DynamicWire policy. The <c>schema-35</c> folder stays
    /// as F2e wrote it: the frozen writer-35 shape still rebuilds those bytes, and the product refuses them likewise.</summary>
    public const int RefusedHoldCompact = CityState.PlayerHoldSchema;

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
