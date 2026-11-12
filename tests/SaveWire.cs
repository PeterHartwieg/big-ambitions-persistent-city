using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;

namespace PersistentCity.Tests;

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

    public static int Converted;

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

    public static string Text(CityState state) => SaveCodec.Write(state);

    public static JObject Parse(string payload) => JObject.Parse(payload);
    public static JObject Parse(string payload, JsonLoadSettings settings) => JObject.Parse(payload, settings);
    public static JToken ParseToken(string payload) => JToken.Parse(payload);

    public static SaveReadResult Read(string? payload) => SaveCodec.Read(payload == null ? null : Payload(payload));
}
