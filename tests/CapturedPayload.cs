using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

internal static class CapturedPayload
{
    private static readonly Regex Decimal = new Regex(@"(?<![\w.])\d+\.\d+(?![\w.])", RegexOptions.CultureInvariant);

    public static string Load(string path) => Normalize(Extract(System.IO.File.ReadAllText(path)));

    public static string Extract(string text)
    {
        if (text.StartsWith(SaveCodec.BodyPrefix, StringComparison.Ordinal)) return text;
        var wrapper = JObject.Parse(text);
        return (string?)wrapper["values"]?[SaveCodec.StateKey] ?? throw new InvalidOperationException("No Persistent City payload in " + nameof(text) + ".");
    }

    public static string Normalize(string payload) => Decimal.Replace(payload, match =>
    {
        string text = match.Value;
        if (Digits(text) < 16) return text;
        double value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        string shortest = value.ToString("R", CultureInfo.InvariantCulture);
        if (shortest.IndexOfAny(new[] { 'E', 'e' }) >= 0) return text;
        Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(double.Parse(shortest, NumberStyles.Float, CultureInfo.InvariantCulture)));
        return shortest.Contains('.') || !text.EndsWith(".0", StringComparison.Ordinal) ? shortest : shortest + ".0";
    });

    private static int Digits(string text)
    {
        int count = 0; bool leading = true;
        foreach (char c in text)
        {
            if (c == '.') continue;
            if (leading && c == '0') continue;
            leading = false; count++;
        }
        return count;
    }
}
