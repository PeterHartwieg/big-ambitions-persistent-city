using System;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

namespace PersistentCity.Tests;

/// <summary>
/// F7d: writer 34 stores the compact wire, so tests that edit the saved city as JSON edit its body instead.
/// <see cref="Body"/> expands a compact payload to the writer-33-shaped body it carries; <see cref="Payload"/> encodes an
/// edited schema-34 body back to the compact wire, so the edit is judged by the full strict reader (compact decode, then
/// the body reader). Older-schema JSON, invalid text and canonical compact strings pass through unchanged, so frozen
/// writer tests and refusal tests keep their meaning. A body the compact encoder cannot spell canonically (whitespace,
/// a noncanonical escape, an invalid token) is also passed through unchanged: the product refuses it as Invalid.
/// </summary>
internal static class SaveWire
{
    private static readonly string BodySchema = CityState.CompactSchema.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string Body(string payload)
    {
        if (payload == null || !payload.StartsWith(SaveCodec.CompactPrefix, StringComparison.Ordinal)) return payload!;
        try { return SaveCodec.ExpandCompact(payload); }
        catch (SaveValidationException) { return payload; }
    }

    public static string Payload(string text)
    {
        if (text == null || !text.StartsWith("{", StringComparison.Ordinal) || text.StartsWith(SaveCodec.CompactPrefix, StringComparison.Ordinal)) return text!;
        try
        {
            // Only a schema-34 body the writer could have spelled is re-encoded; its root fields may be in any order.
            if (!(SaveCodec.CompactTree.Parse(text, int.MaxValue, canonical: true) is SaveCodec.CompactObject root)) return text;
            int at = Array.IndexOf(root.Keys, "Schema");
            if (at < 0 || !(root.Values[at] is SaveCodec.CompactNumber schema) || schema.Text != BodySchema) return text;
            return SaveCodec.CompactFromBody(text);
        }
        catch (SaveValidationException) { return text; }
    }

    /// <summary>The body of the current writer's payload for <paramref name="state"/>, for tests that edit saved text.</summary>
    public static string Text(CityState state) => Body(SaveCodec.Write(state));

    public static JObject Parse(string payload) => JObject.Parse(Body(payload));
    public static JObject Parse(string payload, JsonLoadSettings settings) => JObject.Parse(Body(payload), settings);
    public static JToken ParseToken(string payload) => JToken.Parse(Body(payload));

    public static SaveReadResult Read(string? payload) => SaveCodec.Read(payload == null ? null : Payload(payload));
}
