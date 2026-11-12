using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;

namespace PersistentCity.Tests;

internal sealed record CorpusFixture(int Schema, string File, string Sha256, string Text);

internal static class ReaderCorpus
{
    public const string Folder = "ReaderCorpus";

    public static string Root => Path.Combine(AppContext.BaseDirectory, Folder);

    public static string SchemaFolder(int schema) => "schema-" + schema.ToString("00", System.Globalization.CultureInfo.InvariantCulture);

    public static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static string Sha256(string text) => Sha256(Encoding.UTF8.GetBytes(text));

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static IReadOnlyList<CorpusFixture> Load() => Load(Root);

    public static IReadOnlyList<CorpusFixture> Load(string root)
    {
        int schema = CityState.CurrentSchema;
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
                throw new InvalidDataException($"{SchemaFolder(schema)}/{file} changed: pinned sha256 {pinned}, found {actual}. A deliberate format change re-pins the fixture.");
            fixtures.Add(new CorpusFixture(schema, file, pinned, StrictUtf8.GetString(bytes)));
        }
        if (fixtures.Count == 0) throw new InvalidDataException(folder + ": no fixtures.");
        return fixtures;
    }

    public static string Key(CorpusFixture fixture) => SchemaFolder(fixture.Schema) + "/" + fixture.File;
}
