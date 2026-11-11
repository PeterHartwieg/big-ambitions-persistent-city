using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

namespace PersistentCity.Tests;

/// <summary>
/// P-A: the save string is the JSON body itself, so tests that edit the saved city edit the payload directly.
/// <see cref="Payload"/> removes the whitespace an indented <c>JObject.ToString()</c> adds to a current-schema body, so the
/// edit is judged by the full strict reader in the writer's spelling. Strings are never respelled: text whose header is not
/// the writer's (another schema number, a respelled field name, invalid text) passes through unchanged.
/// </summary>
internal static class SaveWire
{
    public static string Body(string payload) => payload;

    public static string Payload(string text)
    {
        if (text == null || !text.StartsWith("{", StringComparison.Ordinal)) return text!;
        string stripped = StripWhitespace(text);
        if (ReferenceEquals(stripped, text) || !stripped.StartsWith(SaveCodec.BodyPrefix, StringComparison.Ordinal)) return text;
        Converted++;
        return stripped;
    }

    /// <summary>Current-schema bodies this helper respaced to the writer's spelling in this test process.</summary>
    public static int Converted;

    /// <summary>JSON whitespace outside strings removed; the same instance when there is none.</summary>
    private static string StripWhitespace(string text)
    {
        StringBuilder? output = null;
        bool inString = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inString)
            {
                if (c == '\\' && i + 1 < text.Length) { output?.Append(c); c = text[++i]; }
                else if (c == '"') inString = false;
            }
            else if (c == '"') inString = true;
            else if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
            {
                output ??= new StringBuilder(text, 0, i, text.Length);
                continue;
            }
            output?.Append(c);
        }
        return output == null ? text : output.ToString();
    }

    /// <summary>The current writer's payload for <paramref name="state"/>, for tests that edit saved text.</summary>
    public static string Text(CityState state) => SaveCodec.Write(state);

    public static JObject Parse(string payload) => JObject.Parse(payload);
    public static JObject Parse(string payload, JsonLoadSettings settings) => JObject.Parse(payload, settings);
    public static JToken ParseToken(string payload) => JToken.Parse(payload);

    public static SaveReadResult Read(string? payload) => SaveCodec.Read(payload == null ? null : Payload(payload));
}
