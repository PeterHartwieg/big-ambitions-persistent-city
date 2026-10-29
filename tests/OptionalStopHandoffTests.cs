using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;
// Hand-ons of pending chances between named citizens, with draws fixed by choosing each arrival minute.
public sealed class OptionalStopHandoffTests
{
    private const double Start = 1440, Until = 1700;
    private const int Rate = 50;
    private sealed class Walking : IRoutePlanner
    {
        public Route Plan(Place from, Place to, double at, TravelPreferences p) => new()
        { Legs = { new RouteLeg { From = from.Position, To = to.Position, Mode = TravelMode.Walk, Minutes = 10 } } };
    }
    // "shop" offers four window-shopping spots and "park" only two conversation spots.
    private sealed class Venues : IOptionalStopEnvironment
    {
        public bool Raining => false;
        public IReadOnlyList<OptionalStopSpot> Spots(Place place, DwellKind kind)
            => place.Id == "shop" && kind == DwellKind.WindowShopping || place.Id == "park" && kind == DwellKind.ChatOutsideVenue
                ? (place.Id == "shop" ? new[] { 0, 2, 4, 6 } : new[] { 0, 2 }).Select(x => new OptionalStopSpot { Id = place.Id + ":" + x, TargetId = place.Id,
                    Position = new(place.Position.X + x, 0, 1), Facing = place.Position }).ToArray() : Array.Empty<OptionalStopSpot>();
        public OptionalStopAvailability Available(OptionalStopState stop) => OptionalStopAvailability.Ready;
    }
    // Arrival is the first minute at or after `from` whose draw for this citizen and venue is `drawn`.
    // `Carrying` arrivals already hold a pending chance from an earlier visit.
    private sealed record Arrival(string Id, string Venue, double From, bool Drawn, bool Carrying = false);
    private sealed record Holder(string Id, string? Park = null, double EventAt = Until);

    private static CitySimulator Scene(IEnumerable<Holder> holders, IEnumerable<Arrival> arrivals, out Dictionary<string, CitizenState> people)
    {
        var holderList = holders.ToList(); var arrivalList = arrivals.ToList();
        var ids = holderList.Select(h => h.Id).Concat(arrivalList.Select(a => a.Id)).ToArray();
        var places = new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = ids.Length },
            new Place { Id = "shop", Name = "Shop", Kind = PlaceKind.Shop, Capacity = ids.Length, Position = new(100, 0, 0) },
            new Place { Id = "far", Name = "Far shop", Kind = PlaceKind.Shop, Capacity = ids.Length, Position = new(300, 0, 0) },
            new Place { Id = "park", Name = "Park", Kind = PlaceKind.Leisure, Capacity = ids.Length, Position = new(200, 0, 0) } };
        var state = Population.Create(ids.Select(id => new CitizenIdentity { Id = id, Name = id, Age = 30, Persona = Persona.Commuter }),
            places, "handoff-tests", "0.40.4", 17, Start);
        people = state.Citizens.ToDictionary(c => c.Identity.Id);
        foreach (var c in state.Citizens) { c.ShopId = ""; c.LeisureId = ""; }
        foreach (var h in holderList)
        {
            var c = people[h.Id]; c.OptionalStopChoice = true; c.NextEventMinute = c.ActivityUntilMinute = h.EventAt;
            c.Phase = CitizenPhase.AtPlace; c.Journey = null; c.DestinationId = "";
            if (h.Park == null) { c.PlaceId = c.HomeId; c.Activity = Activity.Home; continue; }
            c.PlaceId = h.Park; c.Activity = Activity.Leisure;
            c.RecentVisits.Add(new VisitRecord { AtMinute = Start, PlaceId = h.Park, Activity = Activity.Leisure });
        }
        foreach (var a in arrivalList)
        {
            var c = people[a.Id]; var venue = places.Single(p => p.Id == a.Venue); double at = a.From;
            while (OptionalStopPolicy.Choose(OptionalStopPolicy.VisitKey(new CitizenState { Identity = c.Identity, PlaceId = a.Venue }, at), state.Seed, Rate) != a.Drawn) at++;
            c.Phase = CitizenPhase.Travelling; c.PlaceId = c.HomeId; c.Activity = Activity.Home; c.DestinationId = a.Venue;
            c.DestinationActivity = venue.Kind == PlaceKind.Leisure ? Activity.Leisure : Activity.Shop;
            c.DepartureMinute = Start; c.NextEventMinute = at; c.ActivityUntilMinute = Until;
            if (a.Carrying) c.OptionalStopChoice = true;
            c.Journey = new Walking().Plan(places[0], venue, Start, c.Preferences); c.Journey.Legs[0].Minutes = at - Start;
            Assert.True(at < Until - 60);
        }
        var sim = new CitySimulator(state, new Walking()); sim.AttachOptionalStops(new Venues(), Rate);
        return sim;
    }
    private static double ArrivalOf(Dictionary<string, CitizenState> people, string id) => people[id].NextEventMinute;
    private static void Through(CitySimulator sim, double minute) { sim.AdvanceTo(minute); Assert.True(sim.OptionalStopIndexesMatchState()); }
    private static bool Pending(CitizenState c) => c.OptionalStopChoice == true && c.OptionalStop == null;

    [Fact]
    public void ChancesAreHandedOnFromHoldersAwayBeforeWaitingPartnersInOrdinalIdentityOrder()
    {
        // Ordinal order puts "A1" before "a0" ('A' < 'a'); culture-aware order would not. "0w" sorts first
        // overall but waits at the park as a possible partner, so it is handed on last.
        var sim = Scene(new[] { new Holder("a0", EventAt: 1520), new Holder("A1"), new Holder("0w", "park") },
            new[] { new Arrival("t1", "shop", 1450, false), new Arrival("t2", "shop", 1470, false),
                new Arrival("t3", "shop", 1490, false), new Arrival("t4", "shop", 1530, false) }, out var p);
        Through(sim, ArrivalOf(p, "t1"));
        Assert.False(p["A1"].OptionalStopChoice); Assert.True(Pending(p["a0"])); Assert.True(Pending(p["0w"]));
        Assert.NotNull(p["t1"].OptionalStop); Assert.True(p["t1"].OptionalStopChoice);
        Through(sim, ArrivalOf(p, "t2"));
        Assert.False(p["a0"].OptionalStopChoice); Assert.True(Pending(p["0w"])); Assert.NotNull(p["t2"].OptionalStop);
        Through(sim, ArrivalOf(p, "t3"));
        Assert.False(p["0w"].OptionalStopChoice); Assert.NotNull(p["t3"].OptionalStop);
        // A donor's own later event does not restore its chance, and with no chance left a taker gets nothing.
        Through(sim, ArrivalOf(p, "t4")); Assert.True(sim.State.Minute > 1520);
        Assert.NotEqual(true, p["a0"].OptionalStopChoice); Assert.Null(p["t4"].OptionalStop); Assert.False(p["t4"].OptionalStopChoice);
        Assert.Equal(3, sim.OptionalStopChances.HandedOn);
        Assert.Equal(3, sim.State.Citizens.Count(c => c.OptionalStop != null));
        Assert.DoesNotContain(sim.State.Citizens, Pending);
    }

    [Fact]
    public void APendingChanceOutlivesItsVisitAndPaysForTheHoldersOwnNextFeasibleVisit()
    {
        // "h" is drawn at the far shop, which has no geometry; its chance stays pending after it leaves.
        var sim = Scene(Array.Empty<Holder>(), new[] { new Arrival("h", "far", 1450, true) }, out var p);
        Through(sim, ArrivalOf(p, "h")); Assert.True(Pending(p["h"]));
        Through(sim, Until + 1); Assert.False(p["h"].Phase == CitizenPhase.AtPlace && p["h"].PlaceId == "far"); Assert.True(Pending(p["h"]));
        var reloaded = SaveCodec.Read(SaveCodec.Write(sim.State)); Assert.True(reloaded.Status == SaveReadStatus.Loaded, reloaded.Error);
        Assert.True(reloaded.State!.Citizens.Single().OptionalStopChoice);

        // An undrawn visit carrying such a chance uses its own and leaves the other holder's alone.
        sim = Scene(new[] { new Holder("a") }, new[] { new Arrival("h", "shop", 1450, false, Carrying: true) }, out p);
        Through(sim, ArrivalOf(p, "h"));
        Assert.NotNull(p["h"].OptionalStop); Assert.True(p["h"].OptionalStopChoice); Assert.True(Pending(p["a"]));
        Assert.Equal(new OptionalStopChanceTally { Realised = 1 }, sim.OptionalStopChances);
    }

    [Fact]
    public void TakenOverWaitingPartnerCanNoLongerBeRecruited()
    {
        Holder[] holders = { new("w", "park") };
        // Control: without the taker, a drawn arrival at the park converses with the waiting partner.
        var control = Scene(holders, new[] { new Arrival("r", "park", 1480, true) }, out var c);
        Through(control, ArrivalOf(c, "r"));
        Assert.Equal(DwellKind.ChatOutsideVenue, c["w"].OptionalStop!.Kind); Assert.Equal(c["w"].OptionalStop!.Members, c["r"].OptionalStop!.Members);

        // The waiting partner is the only chance left; an undrawn shop visit takes it over first.
        var sim = Scene(holders, new[] { new Arrival("t", "shop", 1450, false), new Arrival("r", "park", 1480, true) }, out var p);
        Through(sim, ArrivalOf(p, "t"));
        Assert.Equal(DwellKind.WindowShopping, p["t"].OptionalStop!.Kind); Assert.False(p["w"].OptionalStopChoice);
        string saved = SaveCodec.Write(sim.State);
        Through(sim, ArrivalOf(p, "r"));
        // The park has only conversation spots, so the drawn arrival now waits with its own chance.
        Assert.Null(p["w"].OptionalStop); Assert.Null(p["r"].OptionalStop); Assert.True(Pending(p["r"]));
        // Reloaded just after the hand-on, the result is the same.
        var read = SaveCodec.Read(saved); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = new CitySimulator(read.State!, new Walking()); loaded.AttachOptionalStops(new Venues(), Rate);
        loaded.AdvanceTo(sim.State.Minute);
        Assert.Equal(SaveCodec.Write(sim.State), SaveCodec.Write(loaded.State));
    }

    [Fact]
    public void UndrawnArrivalConversesWithAWaitingPartnerUsingAnotherPendingChance()
    {
        var sim = Scene(new[] { new Holder("d"), new Holder("w", "park") }, new[] { new Arrival("t", "park", 1450, false) }, out var p);
        string before = SaveCodec.Write(sim.State);
        Through(sim, ArrivalOf(p, "t"));
        var a = p["t"].OptionalStop!; var b = p["w"].OptionalStop!;
        Assert.Equal(DwellKind.ChatOutsideVenue, a.Kind); Assert.Equal(DwellKind.ChatOutsideVenue, b.Kind);
        Assert.Equal(new[] { "t", "w" }, a.Members); Assert.Equal(a.Members, b.Members);
        Assert.Equal(a.TargetId, b.TargetId); Assert.Equal(a.StartMinute, b.StartMinute); Assert.Equal(a.EndMinute, b.EndMinute);
        Assert.NotEqual(a.SpotId, b.SpotId); Assert.Equal(a.Position, b.Facing); Assert.Equal(b.Position, a.Facing);
        Assert.True(a.EndMinute <= Until);
        // Two stops, two chances: the partner's own and the one handed on from "d".
        Assert.False(p["d"].OptionalStopChoice); Assert.True(p["t"].OptionalStopChoice); Assert.True(p["w"].OptionalStopChoice);
        Assert.Equal(new OptionalStopChanceTally { Realised = 2, HandedOn = 1 }, sim.OptionalStopChances);
        string after = SaveCodec.Write(sim.State);
        // Saved before the arrival or just after it, a reload reaches the same state and keeps the pair.
        var read = SaveCodec.Read(before); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var replay = new CitySimulator(read.State!, new Walking()); replay.AttachOptionalStops(new Venues(), Rate); replay.AdvanceTo(sim.State.Minute);
        Assert.Equal(after, SaveCodec.Write(replay.State));
        read = SaveCodec.Read(after); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = new CitySimulator(read.State!, new Walking()); loaded.AttachOptionalStops(new Venues(), Rate); loaded.RefreshOptionalStops();
        Assert.Equal(after, SaveCodec.Write(loaded.State));
        Assert.False(loaded.State.Citizens.Single(c => c.Identity.Id == "t").OptionalStop!.Cancelled);
    }

    [Fact]
    public void UndrawnArrivalDoesNotConverseWhenThePartnersChanceIsTheOnlyOne()
    {
        // Two stops cannot come from one chance: the partner keeps waiting with its chance.
        var sim = Scene(new[] { new Holder("w", "park") }, new[] { new Arrival("t", "park", 1450, false) }, out var p);
        Through(sim, ArrivalOf(p, "t"));
        Assert.Null(p["t"].OptionalStop); Assert.False(p["t"].OptionalStopChoice); Assert.True(Pending(p["w"]));
        Assert.Equal(default, sim.OptionalStopChances);
    }

    [Fact]
    public void AtZeroAPendingChanceIsNeitherUsedNorHandedOn()
    {
        var sim = Scene(new[] { new Holder("d") }, new[] { new Arrival("t", "shop", 1450, false) }, out var p);
        sim.AttachOptionalStops(new Venues(), 0);
        Through(sim, ArrivalOf(p, "t"));
        Assert.Null(p["t"].OptionalStop); Assert.True(Pending(p["d"]));
    }
}
