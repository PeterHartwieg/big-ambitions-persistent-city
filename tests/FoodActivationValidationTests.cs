using System;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed partial class FoodCommerceTests
{
    [Theory]
    [InlineData(2)] [InlineData(7)] [InlineData(12)] [InlineData(25)]
    public void ActivationValidationPreservesInterruptedFoodJourneysAndRefusals(double offset)
    {
        var (city, _, _) = Ready(atWork: true, representation: 2);
        Advance(city, city.State.Food!.Residents[0].DesiredAt + offset, 1);
        var state = city.State;
        var saved = SaveCodec.Write(state);
        SaveCodec.ValidateForActivation(state);
        Assert.Equal(saved, SaveCodec.Write(state));
        Action<CityState>[] mutations = {
            s => s.Places.Add(s.Places[0]),
            s => s.Places.Insert(0, null!),
            s => s.Food!.Shops[0].PlaceId = "unknown",
            s => s.Food!.Residents[0].PlaceId = "unknown",
            s => s.Food!.Residents[1].Excursion!.OriginId = "unknown"
        };
        foreach (var mutate in mutations)
        {
            var copy = SaveCodec.Read(saved).State!;
            // Some offsets have already completed the represented excursion.
            if (copy.Food!.Residents[1].Excursion == null && mutate == mutations[4]) continue;
            mutate(copy);
            var oldError = Record.Exception(() => SaveCodec.Write(copy));
            var newError = Record.Exception(() => SaveCodec.ValidateForActivation(copy));
            Assert.NotNull(oldError); Assert.NotNull(newError);
            Assert.Equal(oldError.GetType(), newError.GetType()); Assert.Equal(oldError.Message, newError.Message);
        }
    }
}
