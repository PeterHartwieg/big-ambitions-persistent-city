using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class OptionalVisitTests
{
    private sealed class Walking : IRoutePlanner
    {
        public Route Plan(Place from, Place to, double at, TravelPreferences preferences) => new()
        { Legs = new() { new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk, Minutes = 10 } } };
    }
    private static CitySimulator Ready()
    {
        var state = Population.Create(new[] { new CitizenIdentity { Id = "person", Name = "Resident", Persona = Persona.Commuter } },
            new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1 },
                new Place { Id = "shop", Name = "Shop", Kind = PlaceKind.Shop, Position = new(10, 0, 0) } }, "optional-test", "0.40.4", 17, 1440);
        var person = state.Citizens.Single(); person.NextDepartureMinute = person.NextJobChangeMinute = ResidentLifecycle.Never;
        person.Phase = CitizenPhase.AtPlace; person.PlaceId = person.HomeId; person.Activity = Activity.Home;
        person.ActivityUntilMinute = person.NextEventMinute = 1800;
        return new CitySimulator(state, new Walking());
    }
    private static Route Out(CitySimulator s) => new Walking().Plan(s.State.Places.Single(p => p.Id == "home"),
        s.State.Places.Single(p => p.Id == "shop"), s.State.Minute, new TravelPreferences());

    [Fact]
    public void VisitOwnsOneJourneyAndReturnsThroughNormalScheduler()
    {
        var sim = Ready(); var c = sim.State.Citizens.Single(); var route = Out(sim);
        Assert.True(sim.TryBeginOptionalShopVisit(c.Identity.Id, "shop", route, 10, 15, 1500));
        route.Legs[0].Minutes = 500; // caller cannot mutate the committed trip.
        Assert.False(sim.TryBeginOptionalShopVisit(c.Identity.Id, "shop", Out(sim), 10, 15, 1500));
        Assert.Empty(c.RecentVisits);
        sim.AdvanceTo(1450); Assert.Equal("shop", c.PlaceId); Assert.Single(c.RecentVisits);
        sim.AdvanceTo(1475); Assert.Equal("home", c.PlaceId); Assert.Equal(2, c.CompletedJourneys);
    }

    [Fact]
    public void InsufficientWindowOrClosureLeavesAllStateUntouched()
    {
        var sim = Ready(); var c = sim.State.Citizens.Single(); string before = SaveCodec.Write(sim.State);
        Assert.False(sim.TryBeginOptionalShopVisit(c.Identity.Id, "shop", Out(sim), 10, 15, 1460));
        Assert.False(sim.TryBeginOptionalShopVisit(c.Identity.Id, "shop", Out(sim), 10, 15, 1900));
        Assert.Equal(before, SaveCodec.Write(sim.State));
        sim.State.Places.Single(p => p.Id == "shop").WeeklyHours.Add(new OpeningWindow { Day = 1, FromMinute = 600, ToMinute = 660 });
        before = SaveCodec.Write(sim.State);
        Assert.False(sim.TryBeginOptionalShopVisit(c.Identity.Id, "shop", Out(sim), 10, 15, 1500));
        Assert.Equal(before, SaveCodec.Write(sim.State));
    }

    [Fact]
    public void RouteRebuildKeepsArrivalObservationAndUnsubscriptionOwnership()
    {
        var original = Ready(); var person = original.State.Citizens.Single(); var arrivals = 0;
        void Observe(string id, CitizenState state, double minute)
        { if (state.Phase == CitizenPhase.AtPlace && state.Activity == Activity.Shop) arrivals++; }
        original.ActivityObserved += Observe;
        using var recorder = new FoodRoutineRecorder(original);
        Assert.True(original.TryBeginOptionalShopVisit(person.Identity.Id, "shop", Out(original), 10, 15, 1500));
        original.AdvanceTo(1445);
        var rebuilt = original.RebuildRoutes(new Walking());
        rebuilt.AdvanceTo(1450);
        Assert.Equal(1, arrivals);
        rebuilt = rebuilt.RebuildRoutes(new Walking());
        rebuilt.AdvanceTo(1475);
        var captured = recorder.Finish();
        Assert.Contains(captured.Spans, s => s.Activity == Activity.Shop && s.From == 1450 && s.Until == 1465);
        original.ActivityObserved -= Observe;
        // Removing a subscription through its original simulator also removes it after rebuilds.
        person.ActivityUntilMinute = person.NextEventMinute = 1800;
        rebuilt = rebuilt.RebuildRoutes(new Walking());
        Assert.True(rebuilt.TryBeginOptionalShopVisit(person.Identity.Id, "shop", Out(rebuilt), 10, 15, 1550));
        rebuilt.AdvanceTo(1510);
        Assert.Equal(1, arrivals);
    }

    [Fact]
    public void MidJourneySaveResumesIdenticallyWithoutSecondAdmission()
    {
        var sim = Ready(); var c = sim.State.Citizens.Single();
        Assert.True(sim.TryBeginOptionalShopVisit(c.Identity.Id, "shop", Out(sim), 10, 15, 1500));
        sim.AdvanceTo(1445);
        var resumed = new CitySimulator(SaveCodec.Read(SaveCodec.Write(sim.State)).State!, new Walking());
        sim.AdvanceTo(1475);
        while (!resumed.AdvanceTo(1475, 1).CaughtUp) { }
        Assert.Equal(SaveCodec.Write(sim.State), SaveCodec.Write(resumed.State));
        Assert.Single(resumed.State.Citizens.Single().RecentVisits, v => v.Activity == Activity.Shop);
    }
}
