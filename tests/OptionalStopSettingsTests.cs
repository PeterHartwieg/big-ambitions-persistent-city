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
}
