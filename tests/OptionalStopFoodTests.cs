using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;
public sealed partial class FoodCommerceTests
{
    [Theory]
    [InlineData(0)] [InlineData(25)] [InlineData(100)]
    public void WeightedFoodUsesTheSameDrawRegardlessOfOptionalStopPercentage(int percentage)
    {
        var (plain,_,_)=WeightedFoodChoiceTests.Ready(49,16);
        var (stops,_,_)=WeightedFoodChoiceTests.Ready(49,16);
        stops.AttachOptionalStops(new StopEnvironment(),percentage);
        double at=plain.State.Food!.Residents[0].DesiredAt;
        Advance(plain,at);Advance(stops,at,1);
        Assert.Equal(Newtonsoft.Json.JsonConvert.SerializeObject(plain.State.Food),Newtonsoft.Json.JsonConvert.SerializeObject(stops.State.Food));
        Assert.True(stops.OptionalStopIndexesMatchState());
    }
    private sealed class StopEnvironment : IOptionalStopEnvironment
    {
        public bool Raining => false;
        public IReadOnlyList<OptionalStopSpot> Spots(Place p, DwellKind kind) => new[] {new OptionalStopSpot {
            Id="spot",TargetId=p.Id,Position=p.Position,Facing=p.Position}};
        public OptionalStopAvailability Available(OptionalStopState stop) => OptionalStopAvailability.Ready;
    }
    [Theory]
    [InlineData(0)] [InlineData(25)] [InlineData(100)]
    public void OptionalStopsCannotOccupyFoodServiceOrChangeItsPurchases(int rate)
    {
        var (plain, a, _) = Ready(); var (stops, b, _) = Ready();
        stops.AttachOptionalStops(new StopEnvironment(),rate);
        Advance(plain,1800); Advance(stops,1800);
        Assert.Equal(a.Orders,b.Orders); Assert.Equal(a.Units,b.Units);
        Assert.Equal(SaveCodec.Write(plain.State),SaveCodec.Write(stops.State));
    }
    [Fact]
    public void AcceptedMealTripCancelsSavedBreakAndKeepsItsOriginalDeadline()
    {
        var (city, _, _) = Ready(atWork:true); var person=city.State.Citizens[0];
        double desired=city.State.Food!.Residents.Single().DesiredAt;
        Advance(city,desired-1);
        var original=new OptionalStopState { Kind=DwellKind.Break,PlaceId="work",SpotId="work-break",TargetId="work",
            Position=new(1,0,0),Facing=new(2,0,0),ReturnPosition=new(1,0,0),StartMinute=desired-1,EndMinute=desired+9,
            VisitMinute=desired-1,ResumeEventMinute=2870 };
        person.OptionalStop=original;
        Advance(city,desired+0.1);
        Assert.True(original.Cancelled); Assert.Equal(desired+9,original.EndMinute);
        Assert.Equal(CitizenPhase.Travelling,person.Phase); Assert.NotNull(person.MealOuting);
        Assert.Equal(SaveReadStatus.Loaded,SaveCodec.Read(SaveCodec.Write(city.State)).Status);
    }
}
