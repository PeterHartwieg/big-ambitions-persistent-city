using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;
public sealed class OptionalStopSettingsTests
{
    [Fact]
    public void FoodChoiceDefaultsClampsAndOneTimeLogDoNotChangeOptionalStopSetting()
    {
        var defaults=JsonConvert.DeserializeObject<PrototypeSettings>("{}")!;
        Assert.Equal(1,defaults.FoodChoiceTemperature);Assert.Equal(.02,defaults.FoodChoiceWeightFloor);
        Assert.Equal(10,defaults.FoodChoiceDistanceScaleMinutes);Assert.Equal(.5,defaults.FoodChoiceQualityWeight);
        var settings=JsonConvert.DeserializeObject<PrototypeSettings>("{\"FoodChoiceTemperature\":-2,\"FoodChoiceWeightFloor\":9,\"FoodChoiceTasteWeight\":99,\"FoodChoicePriceWeight\":-1,\"FoodChoiceDistanceScaleMinutes\":100,\"FoodChoiceHouseholdWeight\":0,\"FoodShortlistTemperature\":100,\"OptionalStopChoicePercentage\":60}")!;
        var logs=new System.Collections.Generic.List<string>();settings.FoodChoice.Clamp(logs.Add);
        Assert.Equal(7,logs.Count);settings.FoodChoice.Clamp(logs.Add);Assert.Equal(7,logs.Count);
        Assert.Equal(.25,settings.FoodChoiceTemperature);Assert.Equal(.25,settings.FoodChoiceWeightFloor);
        Assert.Equal(3,settings.FoodChoiceTasteWeight);Assert.Equal(0,settings.FoodChoicePriceWeight);
        Assert.Equal(60,settings.FoodChoiceDistanceScaleMinutes);Assert.Equal(.1,settings.FoodChoiceHouseholdWeight);
        Assert.Equal(4,settings.FoodShortlistTemperature);Assert.Equal(60,settings.OptionalStopChoicePercentage);
    }
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
