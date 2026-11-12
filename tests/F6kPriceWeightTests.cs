using System;
using System.IO;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

public sealed class F6kPriceWeightTests
{
    static readonly Money Cap = Money.FromCents(1000000);
    static readonly ResidentTraits Neutral = new() { Bands = "2222222222" };
    static EvaluatedFoodOffer Offer(int cents)
        => new("sole", FoodProducts.Burger, Money.FromCents(cents), 0, 0, 0, 90, null, null, MealKind.Legacy);
    static string Root([CallerFilePath] string path = "") => Path.GetDirectoryName(Path.GetDirectoryName(path)!)!;

    [Fact]
    public void CompiledAndShippedDefaultIsOnePointTwoAndHouseholdWeightIsUnchanged()
    {
        var compiled = new FoodChoiceSettings();
        Assert.Equal(1.2, compiled.FoodChoicePriceWeight);
        Assert.Equal(1, compiled.FoodChoiceHouseholdWeight);
        Assert.Equal(1.2, new PrototypeSettings().FoodChoicePriceWeight);
        var shipped = JsonConvert.DeserializeObject<PrototypeSettings>(File.ReadAllText(Path.Combine(Root(), "config", "persistent-city.json")))!;
        Assert.Equal(1.2, shipped.FoodChoicePriceWeight);
        Assert.Equal(1, shipped.FoodChoiceHouseholdWeight);
        Assert.Equal(1.2, JsonConvert.DeserializeObject<PrototypeSettings>("{}")!.FoodChoicePriceWeight);
    }

    [Theory]
    [InlineData(-1, 0)] [InlineData(0, 0)] [InlineData(1, 1)] [InlineData(3, 3)] [InlineData(9, 3)]
    [InlineData(double.NaN, 1.2)] [InlineData(double.NegativeInfinity, 0)] [InlineData(double.PositiveInfinity, 3)]
    public void ClampRangeStaysZeroToThreeAndNaNFallsBackToOnePointTwo(double input, double expected)
    {
        var settings = new FoodChoiceSettings { FoodChoicePriceWeight = input };
        settings.Clamp();
        Assert.Equal(expected, settings.FoodChoicePriceWeight);
    }

    [Theory]
    [InlineData("2222222222")] [InlineData("2220222222")] [InlineData("2224222222")]
    public void FiftyPercentOverTheMealReferenceIsPenalizedMoreAtOnePointTwo(string bands)
    {
        var traits = new ResidentTraits { Bands = bands };
        double Ratio(double weight)
        {
            var settings = new FoodChoiceSettings { FoodChoicePriceWeight = weight };
            return FoodChoicePolicy.Weight(Offer(1875), traits, Cap, settings)
                / FoodChoicePolicy.Weight(Offer(1250), traits, Cap, settings);
        }
        Assert.True(Ratio(1.2) < Ratio(1));
        Assert.Equal(1.0, FoodChoicePolicy.Weight(Offer(1250), Neutral, Cap, new FoodChoiceSettings()), 12);
        if (bands == "2222222222")
        {
            Assert.Equal(Math.Pow(2.0 / 3, 2.4), Ratio(1.2), 12);
            Assert.Equal(4.0 / 9, Ratio(1), 12);
        }
    }

    [Fact]
    public void WriterAndTuningWireAreUnchangedAndAStoredOneIsKept()
    {
        var wide = FoodDecisionTuning.Capture(new FoodChoiceSettings()).ToWire();
        Assert.Equal(241, wide.Length);
        Assert.True(FoodDecisionTuning.TryParse(wide, out var wideImage));
        Assert.Equal(wide, wideImage!.ToWire());
        var fresh = FoodDecisionTuning.Capture(LegacyFoodChoice.Settings()).ToWire();
        Assert.Equal(FoodDecisionTuning.WireLength, fresh.Length);
        Assert.True(FoodDecisionTuning.TryParse(fresh, out var image));
        Assert.Equal(fresh, image!.ToWire());
        var old = FoodDecisionTuning.Capture(LegacyFoodChoice.Settings().Also(s => s.FoodChoicePriceWeight = 1)).ToWire();
        Assert.Equal(fresh.Length, old.Length);
        Assert.NotEqual(fresh, old);
        Assert.True(FoodDecisionTuning.TryParse(old, out var held));
        Assert.Equal(old, held!.ToWire());
    }
}
