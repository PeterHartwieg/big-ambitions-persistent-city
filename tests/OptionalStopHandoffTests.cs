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
    // "shop" offers four window-shopping spots and "park" only two conversation spots; "far" and "otherpark" offer none.
    private sealed class Venues : IOptionalStopEnvironment
    {
        public bool Raining { get; set; }
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

    private static CitySimulator Scene(IEnumerable<Holder> holders, IEnumerable<Arrival> arrivals, out Dictionary<string, CitizenState> people, int rate = Rate)
    {
        var holderList = holders.ToList(); var arrivalList = arrivals.ToList();
        var ids = holderList.Select(h => h.Id).Concat(arrivalList.Select(a => a.Id)).ToArray();
        var places = new[] { new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = ids.Length },
            new Place { Id = "shop", Name = "Shop", Kind = PlaceKind.Shop, Capacity = ids.Length, Position = new(100, 0, 0) },
            new Place { Id = "far", Name = "Far shop", Kind = PlaceKind.Shop, Capacity = ids.Length, Position = new(300, 0, 0) },
            new Place { Id = "park", Name = "Park", Kind = PlaceKind.Leisure, Capacity = ids.Length, Position = new(200, 0, 0) },
            new Place { Id = "otherpark", Name = "Other park", Kind = PlaceKind.Leisure, Capacity = ids.Length, Position = new(500, 0, 0) } };
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
            while (OptionalStopPolicy.Choose(OptionalStopPolicy.VisitKey(new CitizenState { Identity = c.Identity, PlaceId = a.Venue }, at), state.Seed, rate) != a.Drawn) at++;
            c.Phase = CitizenPhase.Travelling; c.PlaceId = c.HomeId; c.Activity = Activity.Home; c.DestinationId = a.Venue;
            c.DestinationActivity = venue.Kind == PlaceKind.Leisure ? Activity.Leisure : Activity.Shop;
            c.DepartureMinute = Start; c.NextEventMinute = at; c.ActivityUntilMinute = Until;
            if (a.Carrying) c.OptionalStopChoice = true;
            c.Journey = new Walking().Plan(places[0], venue, Start, c.Preferences); c.Journey.Legs[0].Minutes = at - Start;
            Assert.True(at < Until - 60);
        }
        var sim = new CitySimulator(state, new Walking()); sim.AttachOptionalStops(new Venues(), rate);
        return sim;
    }
    private static double ArrivalOf(Dictionary<string, CitizenState> people, string id) => people[id].NextEventMinute;
    private static void Through(CitySimulator sim, double minute) { sim.AdvanceTo(minute); Assert.True(sim.OptionalStopIndexesMatchState()); }
    private static bool Pending(CitizenState c) => c.OptionalStopChoice == true && c.OptionalStop == null;

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RainPreservesPendingChanceForADryUndrawnArrivalAcrossReload(bool reload)
    {
        var sim = Scene(new[] { new Holder("banker") }, new[] {
            new Arrival("wet", "shop", 1450, false), new Arrival("dry", "shop", 1490, false) }, out var people, rate: 25);
        var env = new Venues { Raining = true }; sim.AttachOptionalStops(env, 25);
        double dryArrival = ArrivalOf(people, "dry");
        Through(sim, ArrivalOf(people, "wet"));
        Assert.True(Pending(people["banker"])); Assert.Null(people["wet"].OptionalStop);
        Assert.Equal(default, sim.OptionalStopChances); // wet admissions do not consume the denominator or stock
        if (reload)
        {
            var read = SaveCodec.Read(SaveCodec.Write(sim.State)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            sim = new CitySimulator(read.State!, new Walking()); sim.AttachOptionalStops(env, 25);
            people = sim.State.Citizens.ToDictionary(c => c.Identity.Id);
        }
        env.Raining = false; Through(sim, dryArrival);
        Assert.False(people["banker"].OptionalStopChoice);
        Assert.Null(people["wet"].OptionalStop); // rain clearing cannot replay that admission
        Assert.Equal(DwellKind.WindowShopping, people["dry"].OptionalStop!.Kind);
        Assert.Equal(new OptionalStopChanceTally { Eligible = 1, Realised = 1, HandedOn = 1 }, sim.OptionalStopChances);
        Assert.Equal(Until, people["dry"].NextEventMinute);
        Assert.Equal(SaveReadStatus.Loaded, SaveCodec.Read(SaveCodec.Write(sim.State)).Status);
    }

    // The nominal bound is two, but an expired stop record still occupies its resident's slot until departure.
    [Fact]
    public void ExhaustedRecipientsDropASecondChanceBelowTheNominalBound()
    {
        var sim = Scene(Array.Empty<Holder>(), new[] { new Arrival("b", "shop", 1450, true),
            new Arrival("h", "far", 1480, true, Carrying: true) }, out var p, rate: 25);
        Through(sim, ArrivalOf(p, "b"));
        Assert.Equal(DwellKind.WindowShopping, p["b"].OptionalStop!.Kind);
        Through(sim, ArrivalOf(p, "h"));
        Assert.True(p["b"].OptionalStop!.EndMinute < sim.State.Minute);
        Assert.True(p["b"].Phase == CitizenPhase.AtPlace && p["b"].PlaceId == "shop");
        Assert.Single(sim.State.Citizens, Pending);
        Assert.Equal(new OptionalStopChanceTally { Eligible = 2, Drawn = 2, Realised = 1, Dropped = 1 }, sim.OptionalStopChances);
        // Opening pending = 1: two new draws = one stop + unchanged pending + one drop.
        var t = sim.OptionalStopChances;
        Assert.Equal(t.Drawn, t.Realised + sim.State.Citizens.Count(Pending) - 1 + t.Dropped);
        var read = SaveCodec.Read(SaveCodec.Write(sim.State));
        Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        Assert.Equal(SaveCodec.Write(sim.State), SaveCodec.Write(read.State!));
    }

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
        Assert.Equal(new OptionalStopChanceTally { Eligible = 1, Realised = 1 }, sim.OptionalStopChances);
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
        Assert.Equal(new OptionalStopChanceTally { Eligible = 1, Realised = 2, HandedOn = 1 }, sim.OptionalStopChances);
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
        Assert.Equal(new OptionalStopChanceTally { Eligible = 1 }, sim.OptionalStopChances);
    }

    [Fact]
    public void AtZeroAPendingChanceIsNeitherUsedNorHandedOn()
    {
        var sim = Scene(new[] { new Holder("d") }, new[] { new Arrival("t", "shop", 1450, false) }, out var p);
        sim.AttachOptionalStops(new Venues(), 0);
        Through(sim, ArrivalOf(p, "t"));
        Assert.Null(p["t"].OptionalStop); Assert.True(Pending(p["d"]));
    }

    // Review ob2: the waiting holders are the arriving visitor's only local partner and a holder at another park.
    // Whichever sorts first, the local one converses and the remote one donates; the result survives a reload.
    [Theory]
    [InlineData("a", "z")] // d438cc6: no stop, because "a" was spent as the donor first
    [InlineData("z", "a")]
    public void UndrawnArrivalKeepsItsOnlyLocalPartnerAndTakesAnotherWaitingDonor(string local, string remote)
    {
        var sim = Scene(new[] { new Holder(local, "park"), new Holder(remote, "otherpark") },
            new[] { new Arrival("t", "park", 1450, false), new Arrival("u", "far", 1600, false) }, out var p);
        string before = SaveCodec.Write(sim.State);
        Through(sim, ArrivalOf(p, "t"));
        Assert.Equal(DwellKind.ChatOutsideVenue, p["t"].OptionalStop?.Kind);
        Assert.Equal(new[] { "t", local }, p["t"].OptionalStop!.Members); Assert.Equal(p["t"].OptionalStop!.Members, p[local].OptionalStop!.Members);
        Assert.True(p["t"].OptionalStopChoice); Assert.True(p[local].OptionalStopChoice);
        Assert.False(p[remote].OptionalStopChoice); Assert.Null(p[remote].OptionalStop);
        Assert.Equal(new OptionalStopChanceTally { Eligible = 1, Realised = 2, HandedOn = 1 }, sim.OptionalStopChances);
        string after = SaveCodec.Write(sim.State);
        var read = SaveCodec.Read(before); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var replay = new CitySimulator(read.State!, new Walking()); replay.AttachOptionalStops(new Venues(), Rate); replay.AdvanceTo(sim.State.Minute);
        Assert.Equal(after, SaveCodec.Write(replay.State));
        read = SaveCodec.Read(after); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = new CitySimulator(read.State!, new Walking()); loaded.AttachOptionalStops(new Venues(), Rate); loaded.RefreshOptionalStops();
        Assert.Equal(after, SaveCodec.Write(loaded.State));
        Assert.True(loaded.OptionalStopIndexesMatchState());
    }

    // A citizen holds one chance. Drawn again while carrying one, it keeps one at the far shop (no geometry) or uses
    // one at the shop, and passes the other to "b", the first citizen in identity order holding neither.
    // d438cc6 dropped it.
    [Theory]
    [InlineData("far")] [InlineData("shop")]
    public void ARepeatedVisitsSecondChancePassesToTheFirstCitizenHoldingNone(string venue)
    {
        var sim = Scene(Array.Empty<Holder>(), new[] { new Arrival("h", venue, 1450, true, Carrying: true),
            new Arrival("b", "far", 1600, false), new Arrival("c", "far", 1610, false) }, out var p);
        string before = SaveCodec.Write(sim.State);
        Through(sim, ArrivalOf(p, "h"));
        Assert.True(Pending(p["b"])); Assert.Null(p["c"].OptionalStopChoice);
        Assert.True(p["h"].OptionalStopChoice); Assert.Equal(venue == "shop", p["h"].OptionalStop != null);
        Assert.Equal(new OptionalStopChanceTally { Eligible = 1, Drawn = 1, Realised = venue == "shop" ? 1 : 0, PassedOn = 1 }, sim.OptionalStopChances);
        var read = SaveCodec.Read(before); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var replay = new CitySimulator(read.State!, new Walking()); replay.AttachOptionalStops(new Venues(), Rate); replay.AdvanceTo(sim.State.Minute);
        Assert.Equal(SaveCodec.Write(sim.State), SaveCodec.Write(replay.State));
    }

    // Chances saved at a higher setting: 20 of 40 residents hold one and 20 undrawn visits follow at the shop.
    // At 10 the bound is 4 x 10% x 40 = 16, so the first eligible visit trims h00-h03 and then takes h04's chance.
    // At 25 the bound is every resident: the inherited chances are kept and can all be spent. At 0 none is
    // trimmed or spent.
    [Theory]
    [InlineData(10, 4)] [InlineData(25, 0)] [InlineData(0, 0)]
    public void InheritedChancesAboveTheCurrentBoundAreTrimmedLowestIdentityFirst(int rate, int trimmed)
    {
        var holders = Enumerable.Range(0, 20).Select(i => "h" + i.ToString("D2")).ToArray();
        var sim = Scene(holders.Select(id => new Holder(id)),
            Enumerable.Range(0, 20).Select(i => new Arrival("t" + i.ToString("D2"), "shop", 1450 + i * 6, false)), out _);
        var read = SaveCodec.Read(SaveCodec.Write(sim.State)); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
        var loaded = new CitySimulator(read.State!, new Walking()); loaded.AttachOptionalStops(new Venues(), rate);
        var q = loaded.State.Citizens.ToDictionary(c => c.Identity.Id);
        Through(loaded, ArrivalOf(q, "t00"));
        Assert.All(holders.Take(trimmed), id => Assert.False(q[id].OptionalStopChoice));
        if (rate > 0) { Assert.False(q[holders[trimmed]].OptionalStopChoice); Assert.NotNull(q["t00"].OptionalStop); }
        Assert.All(holders.Skip(trimmed + (rate > 0 ? 1 : 0)), id => Assert.True(Pending(q[id])));
        Assert.Equal(trimmed, loaded.OptionalStopChances.Dropped);
        Through(loaded, Until);
        var t = loaded.OptionalStopChances;
        Assert.Equal(0, t.Drawn); Assert.Equal(trimmed, t.Dropped); Assert.Equal(t.HandedOn, t.Realised);
        // Every kept chance is spent once or still pending; at 0 all twenty are still pending.
        Assert.Equal(20 - trimmed, t.Realised + holders.Count(id => Pending(q[id])));
        if (rate == 0) Assert.Equal(0, t.Realised); else Assert.True(t.Realised > 10, "realised " + t.Realised);
    }

    // At 100 every visit is drawn, so nothing can be handed on: a holder away from a leisure venue loses its chance
    // at the next eligible visit, and an unusable draw ends with its visit unless the visitor waits for a partner.
    [Fact]
    public void AtHundredOnlyWaitingConversationPartnersKeepAChance()
    {
        var sim = Scene(new[] { new Holder("a"), new Holder("w", "park") },
            new[] { new Arrival("f", "far", 1450, true), new Arrival("r", "park", 1480, true) }, out var p);
        sim.AttachOptionalStops(new Venues(), 100);
        Through(sim, ArrivalOf(p, "f"));
        Assert.False(p["a"].OptionalStopChoice); Assert.False(p["f"].OptionalStopChoice); Assert.True(Pending(p["w"]));
        Assert.Equal(new OptionalStopChanceTally { Eligible = 1, Drawn = 1, Dropped = 2 }, sim.OptionalStopChances);
        Through(sim, ArrivalOf(p, "r"));
        Assert.Equal(DwellKind.ChatOutsideVenue, p["r"].OptionalStop?.Kind); Assert.Equal(p["r"].OptionalStop!.Members, p["w"].OptionalStop!.Members);
        Assert.Equal(new OptionalStopChanceTally { Eligible = 2, Drawn = 2, Realised = 2, Dropped = 2 }, sim.OptionalStopChances);
    }
}
