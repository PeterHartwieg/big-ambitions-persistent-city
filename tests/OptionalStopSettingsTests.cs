using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;
public sealed class OptionalStopSettingsTests
{
    [Fact]
    public void ApprovedAndMissingSettingsDefaultTo25()
    {
        Assert.Equal(25, OptionalStopPolicy.ApprovedChoicePercentage);
        Assert.Equal(25, new PrototypeSettings().OptionalStopChoicePercentage);
        Assert.Equal(25, JsonConvert.DeserializeObject<PrototypeSettings>("{}")!.OptionalStopChoicePercentage);
    }
    [Theory]
    [InlineData(-1, 0)] [InlineData(101, 100)] [InlineData(0, 0)] [InlineData(100, 100)] [InlineData(60, 60)]
    public void SetterClamps(int input, int expected)
        => Assert.Equal(expected, new PrototypeSettings { OptionalStopChoicePercentage = input }.OptionalStopChoicePercentage);
    [Theory]
    [InlineData("-10", 0)] [InlineData("150", 100)] [InlineData("75", 75)]
    [InlineData("\"nonsense\"", 25)] [InlineData("null", 25)] [InlineData("true", 25)]
    [InlineData("{}", 25)] [InlineData("[]", 25)] [InlineData("25.5", 25)]
    [InlineData("\"60\"", 60)] [InlineData("1e100", 100)] [InlineData("-1e100", 0)]
    public void JsonHandlesInvalidIndividualValuesWithoutLosingOtherSettings(string input, int expected)
    {
        var settings = JsonConvert.DeserializeObject<PrototypeSettings>("{\"Enabled\":false,\"OptionalStopChoicePercentage\":" + input + "}")!;
        Assert.Equal(expected, settings.OptionalStopChoicePercentage); Assert.False(settings.Enabled);
    }

    public static System.Collections.Generic.IEnumerable<object[]> TimingKeys()
    {
        yield return new object[] { "StreetLifeBreakMaximumMinutes", DwellKind.Break };
        yield return new object[] { "StreetLifeChatMaximumMinutes", DwellKind.ChatOutsideVenue };
        yield return new object[] { "StreetLifeWindowShoppingMaximumMinutes", DwellKind.WindowShopping };
        yield return new object[] { "StreetLifeAudienceMaximumMinutes", DwellKind.Audience };
        yield return new object[] { "StreetLifeRestingMaximumMinutes", DwellKind.Resting };
    }
    [Theory]
    [MemberData(nameof(TimingKeys))]
    public void TimingDefaultsAndBothClampsAndMalformedValuesAreReportedOnce(string key, DwellKind kind)
    {
        double minimum = OptionalStopPolicy.Minimum(kind), maximum = DwellRules.For(kind).MaximumMinutes;
        Assert.Equal(maximum, JsonConvert.DeserializeObject<PrototypeSettings>("{}")!.StopTiming().Maximum(kind));
        foreach (string value in new[] { "-1", "1e100", "\"-Infinity\"", "\"Infinity\"", "null", "true", "{}", "[]", "\"bad\"", "\"NaN\"", (minimum + 0.5).ToString(System.Globalization.CultureInfo.InvariantCulture) })
        {
            var settings = JsonConvert.DeserializeObject<PrototypeSettings>("{\"Enabled\":false,\"" + key + "\":" + value + "}")!;
            double expected = value == "-1" || value == "\"-Infinity\"" ? minimum : maximum;
            Assert.Equal(expected, settings.StopTiming().Maximum(kind)); Assert.False(settings.Enabled);
            var messages = new System.Collections.Generic.List<string>(); settings.ReportStreetLifeSettings(messages.Add);
            Assert.Equal(2, messages.Count); Assert.Contains(key, messages[0]);
            settings.ReportStreetLifeSettings(messages.Add); Assert.Equal(2, messages.Count);
        }
        foreach (double endpoint in new[] { minimum, maximum })
        {
            var settings = JsonConvert.DeserializeObject<PrototypeSettings>("{\"" + key + "\":\"" + endpoint + "\"}")!;
            Assert.Equal(endpoint, settings.StopTiming().Maximum(kind));
            var messages = new System.Collections.Generic.List<string>(); settings.ReportStreetLifeSettings(messages.Add); Assert.Single(messages);
        }
        var property = typeof(PrototypeSettings).GetProperty(key)!;
        var direct = new PrototypeSettings(); property.SetValue(direct, -1); Assert.Equal(minimum, direct.StopTiming().Maximum(kind));
        property.SetValue(direct, int.MaxValue); Assert.Equal(maximum, direct.StopTiming().Maximum(kind));
    }
    [Fact]
    public void MalformedJsonKeepsExistingFileLevelFailure()
        => Assert.ThrowsAny<JsonException>(() => JsonConvert.DeserializeObject<PrototypeSettings>("{\"StreetLifeBreakMaximumMinutes\":5,"));

    [Theory]
    [MemberData(nameof(TimingKeys))]
    public void DefaultTimingIsBitIdenticalToPriorFormulaAndTunedTimingKeepsItsDraw(string key, DwellKind kind)
    {
        var defaults = new PrototypeSettings().StopTiming();
        for (int seed = 0; seed < 40; seed++)
            for (int visit = 0; visit < 100; visit++)
            {
                string drawKey = key + visit;
                double dice = StableRandom.Hash(drawKey + ":duration:" + kind, seed) % 10001 / 10000.0;
                double prior = OptionalStopPolicy.Minimum(kind) + dice * (DwellRules.For(kind).MaximumMinutes - OptionalStopPolicy.Minimum(kind));
                Assert.Equal(prior, OptionalStopPolicy.Duration(drawKey, kind, seed, defaults.Maximum(kind)));
                Assert.Equal(OptionalStopPolicy.Minimum(kind), OptionalStopPolicy.Duration(drawKey, kind, seed, OptionalStopPolicy.Minimum(kind)));
            }
    }
}
