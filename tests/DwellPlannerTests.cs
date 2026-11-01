using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;

namespace PersistentCity.Tests;

public sealed class DwellPlannerTests
{
    private const int Seed = 11;
    private const double Day = 1440;
    private static readonly Place Shop = new() { Id = "shop", Kind = PlaceKind.Shop, OpenMinute = 9 * 60, CloseMinute = 18 * 60 };

    private static CitizenState WaitingAtShop(string id, double until = Day + 12 * 60) => new()
    {
        Identity = new CitizenIdentity { Id = id }, HomeId = "home", PlaceId = "shop", DestinationId = "shop",
        DestinationActivity = Activity.Shop, Activity = Activity.Shop, Phase = CitizenPhase.WaitingForOpening,
        ActivityUntilMinute = until, NextEventMinute = Day + 9 * 60
    };

    [Fact]
    public void QueueFormsAtMostTenMinutesBeforeOpeningAndEndsAtOpening()
    {
        double opening = Day + 9 * 60;
        foreach (double arrival in new[] { Day + 6 * 60, opening - 30, opening - 10, opening - 3, opening - 0.5 })
            for (int i = 0; i < 200; i++)
            {
                var dwell = DwellPlanner.QueueAtOpening(WaitingAtShop("c" + i), Shop, Seed, arrival, raining: false);
                Assert.True(dwell.HasValue);
                var d = dwell!.Value;
                Assert.Equal(DwellKind.QueueAtOpening, d.Kind);
                Assert.Equal(DwellSpot.Door, d.Spot);
                Assert.Equal(DwellFacing.Door, d.Facing);
                Assert.Equal("shop", d.FacingId);
                Assert.Equal(opening, d.EndMinute);
                Assert.InRange(opening - d.StartMinute, DwellPlanner.MinimumQueueLeadMinutes, DwellPlanner.MaximumQueueLeadMinutes);
                // Nobody stands at the door earlier than the queue window, however early they came.
                Assert.False(d.ActiveAt(opening - DwellPlanner.MaximumQueueLeadMinutes - 0.01));
                Assert.False(d.ActiveAt(opening));
            }
    }

    [Fact]
    public void QueueLeadsSpreadAcrossCitizens()
    {
        var leads = Enumerable.Range(0, 300)
            .Select(i => Day + 9 * 60 - DwellPlanner.QueueAtOpening(WaitingAtShop("c" + i), Shop, Seed, Day + 7 * 60, false)!.Value.StartMinute)
            .ToArray();
        Assert.True(leads.Min() < 4 && leads.Max() > 8, "Arrivals should spread across the window, not all stand from one instant.");
    }

    [Fact]
    public void QueueIsStableForTheSameCitizenAndDay()
    {
        var person = WaitingAtShop("stable");
        var early = DwellPlanner.QueueAtOpening(person, Shop, Seed, Day + 7 * 60, false)!.Value;
        var later = DwellPlanner.QueueAtOpening(person, Shop, Seed, Day + 8 * 60 + 58, false)!.Value;
        Assert.Equal(early.StartMinute, later.StartMinute);
        Assert.Equal(early.EndMinute, later.EndMinute);
        var nextDay = DwellPlanner.QueueAtOpening(WaitingAtShop("stable", 2 * Day + 12 * 60), Shop, Seed, 2 * Day + 7 * 60, false)!.Value;
        Assert.Equal(2 * Day + 9 * 60, nextDay.EndMinute);
    }

    [Fact]
    public void WaitingAwayFromTheDoorOrForARouteHasNoDwell()
    {
        var elsewhere = WaitingAtShop("home-waiter"); elsewhere.PlaceId = "home";
        Assert.Null(DwellPlanner.QueueAtOpening(elsewhere, Shop, Seed, Day + 8 * 60 + 55, false));
        var route = WaitingAtShop("route-waiter"); route.Phase = CitizenPhase.WaitingForRoute;
        Assert.Null(DwellPlanner.QueueAtOpening(route, Shop, Seed, Day + 8 * 60 + 55, false));
        var home = WaitingAtShop("going-home"); home.DestinationActivity = Activity.Home;
        Assert.Null(DwellPlanner.QueueAtOpening(home, Shop, Seed, Day + 8 * 60 + 55, false));
    }

    [Fact]
    public void NoQueueWhenThePlaceDoesNotOpenBeforeTheActivityEnds()
    {
        var person = WaitingAtShop("late", until: Day + 8 * 60 + 30);
        Assert.Null(DwellPlanner.QueueAtOpening(person, Shop, Seed, Day + 8 * 60, false));
        var shut = new Place { Id = "shop", Kind = PlaceKind.Shop, WeeklyHours = { new OpeningWindow { Day = 6, FromMinute = 600, ToMinute = 700 } } };
        Assert.Null(DwellPlanner.QueueAtOpening(WaitingAtShop("shut"), shut, Seed, Day + 8 * 60, false));
    }

    [Fact]
    public void RainShortensAndThinsTheQueue()
    {
        int dry = 0, wet = 0;
        for (int i = 0; i < 400; i++)
        {
            var person = WaitingAtShop("r" + i);
            if (DwellPlanner.QueueAtOpening(person, Shop, Seed, Day + 7 * 60, false).HasValue) dry++;
            var rain = DwellPlanner.QueueAtOpening(person, Shop, Seed, Day + 7 * 60, true);
            if (!rain.HasValue) continue;
            wet++;
            Assert.True(rain.Value.EndMinute - rain.Value.StartMinute <= DwellPlanner.RainQueueLeadMinutes + 1e-9);
        }
        Assert.Equal(400, dry);
        Assert.InRange(wet, 120, 280);
    }

    [Fact]
    public void CrossingWaitHasNoTimeCap()
    {
        // Waiting for a safe crossing ends on the light, never on a clock.
        var rule = DwellRules.For(DwellKind.WaitToCross);
        Assert.Equal(double.PositiveInfinity, rule.MaximumMinutes);
        Assert.False(rule.Optional);
        Assert.Equal(DwellSpot.Kerb, rule.Spot);
        Assert.Equal(DwellFacing.Crossing, rule.Facing);
        var longWait = new Dwell("walker", DwellKind.WaitToCross, "crossing-1", 600, 600 + 45, "crossing-1");
        Assert.True(longWait.ActiveAt(640));
    }

    [Fact]
    public void EveryKindEndsWithinItsCapAndItsWindow()
    {
        var random = new Random(5);
        foreach (DwellKind kind in Enum.GetValues(typeof(DwellKind)))
            for (int i = 0; i < 200; i++)
            {
                double from = random.NextDouble() * 20 * Day, until = from + random.NextDouble() * 90;
                var dwell = DwellPlanner.WithinWindow("c" + i, kind, "spot", Seed, from, until, raining: false);
                if (until <= from) { Assert.Null(dwell); continue; }
                var d = dwell!.Value;
                Assert.True(d.EndMinute > d.StartMinute);
                Assert.True(d.StartMinute >= from && d.EndMinute <= until);
                Assert.True(d.EndMinute - d.StartMinute <= DwellRules.For(kind).MaximumMinutes + 1e-9);
            }
        Assert.Throws<ArgumentOutOfRangeException>(() => new Dwell("c", DwellKind.QueueAtOpening, "shop", 0, 11, "shop"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Dwell("c", DwellKind.Resting, "bench", 5, 5, "bench"));
    }

    [Fact]
    public void RainDropsOnlyTheOptionalKinds()
    {
        foreach (DwellKind kind in Enum.GetValues(typeof(DwellKind)))
        {
            var wet = DwellPlanner.WithinWindow("c", kind, "spot", Seed, Day + 600, Day + 660, raining: true);
            var dry = DwellPlanner.WithinWindow("c", kind, "spot", Seed, Day + 600, Day + 660, raining: false);
            Assert.True(dry.HasValue);
            Assert.Equal(!DwellRules.For(kind).Optional, wet.HasValue);
        }
        Assert.True(DwellRules.For(DwellKind.WindowShopping).Optional);
        Assert.True(DwellRules.For(DwellKind.Resting).Optional);
        Assert.True(DwellRules.For(DwellKind.ChatOutsideVenue).Optional);
        Assert.False(DwellRules.For(DwellKind.QueueAtOpening).Optional);
    }

    [Fact]
    public void SimulatedArrivalAtAClosedShopQueuesOnlyNearOpening()
    {
        // The engine's own wait state feeds the planner: arriving at 08:00 for a 09:00 opening.
        var home = new Place { Id = "home", Kind = PlaceKind.Home, Position = new Position(0, 0, 0) };
        var shop = new Place { Id = "shop", Kind = PlaceKind.Shop, Position = new Position(100, 0, 0), OpenMinute = 9 * 60, CloseMinute = 18 * 60 };
        var person = WaitingAtShop("arrival");
        person.Phase = CitizenPhase.Travelling; person.PlaceId = "home"; person.Activity = Activity.Home;
        person.DepartureMinute = Day + 7 * 60 + 50; person.NextEventMinute = Day + 8 * 60;
        person.Journey = new Route { Legs = { new RouteLeg { From = home.Position, To = shop.Position, Mode = TravelMode.Walk, Minutes = 10 } } };
        var state = new CityState { Seed = Seed, Minute = Day + 7 * 60 + 50, Places = { home, shop }, Citizens = { person } };
        var simulation = new CitySimulator(state, new NoRoutes());
        simulation.AdvanceTo(Day + 8 * 60 + 1);
        var waiting = state.Citizens[0];
        Assert.Equal(CitizenPhase.WaitingForOpening, waiting.Phase);
        Assert.Equal("shop", waiting.PlaceId);
        var dwell = DwellPlanner.QueueAtOpening(waiting, shop, Seed, state.Minute, false)!.Value;
        Assert.False(dwell.ActiveAt(state.Minute));
        Assert.Equal(waiting.NextEventMinute, dwell.EndMinute);
        simulation.AdvanceTo(dwell.EndMinute);
        Assert.Equal(CitizenPhase.AtPlace, state.Citizens[0].Phase);
        Assert.Null(DwellPlanner.QueueAtOpening(state.Citizens[0], shop, Seed, state.Minute, false));
    }

    [Fact]
    public void QueueSlotsAreNeverSharedAndStandingBodiesKeepTheirSpot()
    {
        double opening = Day + 9 * 60;
        var dry = Enumerable.Range(0, 8).Select(i => DwellPlanner.QueueAtOpening(WaitingAtShop("q" + i), Shop, Seed, opening - 1, false)!.Value).ToList();
        var first = DwellPlanner.AssignQueueSlots(Array.Empty<KeyValuePair<string, int>>(), dry, 8);
        Assert.Equal(8, first.Count);
        Assert.Equal(8, first.Values.Distinct().Count());
        var byStart = dry.OrderBy(d => d.StartMinute).ThenBy(d => d.CitizenId, StringComparer.Ordinal).Select(d => d.CitizenId).ToArray();
        Assert.Equal(0, first[byStart[0]]);

        // Rain starts: the plan drops or delays some of the eight, and six newcomers become planned.
        var wet = Enumerable.Range(0, 14).Select(i => DwellPlanner.QueueAtOpening(WaitingAtShop("q" + i), Shop, Seed, opening - 1, true))
            .Where(d => d.HasValue && d.Value.ActiveAt(opening - 1)).Select(d => d!.Value).ToList();
        Assert.True(wet.Count < 14);
        var second = DwellPlanner.AssignQueueSlots(first, wet, 8);
        foreach (var pair in first) Assert.Equal(pair.Value, second[pair.Key]);
        Assert.Equal(second.Count, second.Values.Distinct().Count());
        Assert.True(second.Count <= 8);

        // Bodies leaving free their spots for the next planned queuer, front first.
        var leaving = first.Where(p => p.Value != 0).ToList();
        var third = DwellPlanner.AssignQueueSlots(leaving, dry.Concat(wet), 8);
        Assert.Equal(third.Count, third.Values.Distinct().Count());
        Assert.Contains(0, third.Values);
    }

    [Fact]
    public void PlacesSharingADoorShareOneLine()
    {
        // GamePlaces gives a school's work and study roles the same entrance point; one door, one line.
        var entrance = new Position(12.34, 0.5, -98.76);
        var work = new Place { Id = "address:x:1:work", Position = entrance };
        var study = new Place { Id = "address:x:1:study", Position = entrance };
        var neighbour = new Place { Id = "address:x:3:shop", Position = new Position(16.34, 0.5, -98.76) };
        Assert.Equal(DwellPlanner.DoorKey(work.Position), DwellPlanner.DoorKey(study.Position));
        Assert.NotEqual(DwellPlanner.DoorKey(work.Position), DwellPlanner.DoorKey(neighbour.Position));
    }

    [Fact]
    public void StaffAndStudentsWaitInsideRatherThanQueue()
    {
        foreach (var activity in new[] { Activity.Work, Activity.Study })
        {
            var person = WaitingAtShop("staff-" + activity); person.DestinationActivity = person.Activity = activity;
            Assert.Null(DwellPlanner.QueueAtOpening(person, Shop, Seed, Day + 8 * 60 + 58, false));
        }
        var leisure = WaitingAtShop("visitor"); leisure.DestinationActivity = leisure.Activity = Activity.Leisure;
        Assert.NotNull(DwellPlanner.QueueAtOpening(leisure, Shop, Seed, Day + 8 * 60 + 58, false));
    }

    [Fact]
    public void LateJoinersQueueAtTheBack()
    {
        var dwells = Enumerable.Range(0, 3).Select(i => new Dwell("late" + i, DwellKind.QueueAtOpening, "shop", 530 + i, 540, "shop")).ToList();
        // Two people stand in spots 1 and 2; spot 0 is free because its holder left early.
        var standing = new[] { new KeyValuePair<string, int>("b", 1), new KeyValuePair<string, int>("c", 2) };
        var slots = DwellPlanner.AssignQueueSlots(standing, dwells, 4);
        Assert.Equal(3, slots["late0"]);
        Assert.Equal(0, slots["late1"]);
        Assert.False(slots.ContainsKey("late2"));
    }

    [Fact]
    public void TinyWindowsAtLargeMinutesHaveNoDwell()
    {
        double from = 9e8;
        Assert.Null(DwellPlanner.WithinWindow("c", DwellKind.Resting, "bench", Seed, from, from + 1e-9, false));
    }

    [Fact]
    public void StandingBodiesMoveUpOnlyIntoFreeSpots()
    {
        // B and C stand in spots 1 and 2 because A's body was never created; the front is empty.
        var occupied = new List<int> { 1, 2 };
        Assert.Equal(0, DwellPlanner.MoveUp(occupied, 1));
        // B's walk forward fails: B keeps spot 1, so C can't take it and has nothing free ahead but 0.
        Assert.Equal(0, DwellPlanner.MoveUp(occupied, 2));
        occupied = new List<int> { 0, 1, 2 };
        Assert.Null(DwellPlanner.MoveUp(occupied, 2));
        // After moves the plan is built from the spots bodies actually hold: no spot is published twice.
        var standing = new[] { new KeyValuePair<string, int>("b", 1), new KeyValuePair<string, int>("c", 0) };
        var a = new Dwell("a", DwellKind.QueueAtOpening, "shop", 531, 540, "shop");
        var slots = DwellPlanner.AssignQueueSlots(standing, new[] { a }, 8);
        Assert.Equal(slots.Count, slots.Values.Distinct().Count());
        Assert.Equal(2, slots["a"]);
        // Duplicate reports of one spot never yield two holders.
        var duplicate = new[] { new KeyValuePair<string, int>("b", 1), new KeyValuePair<string, int>("d", 1) };
        Assert.Single(DwellPlanner.AssignQueueSlots(duplicate, Array.Empty<Dwell>(), 8));
    }

    [Theory]
    [InlineData(9, 0)] [InlineData(9, 25)] [InlineData(9, 100)]
    [InlineData(3, 0)] [InlineData(3, 25)] [InlineData(3, 100)]
    public void CommittedQueueKeepsLeadButReloadCanOnlyReconstructCurrentWeather(int hour, int rate)
    {
        double opening = Day + hour * 60;
        var shop = new Place { Id = "shop", Name = "Shop", Kind = PlaceKind.Shop, Capacity = 1, OpenMinute = hour * 60, CloseMinute = (hour + 4) * 60 };
        var home = new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1 };
        var state = Population.Create(new[] { new CitizenIdentity { Id = "queue0", Name = "Queue", Age = 30 } }, new[] { home, shop }, "e2b-queue", "0.40.4", 17, opening - 10);
        var c = state.Citizens.Single(); c.Phase = CitizenPhase.WaitingForOpening; c.PlaceId = c.DestinationId = "shop";
        c.Activity = c.DestinationActivity = Activity.Shop; c.ActivityUntilMinute = opening + 60; c.NextEventMinute = opening;
        var dry = DwellPlanner.QueueAtOpening(c, shop, 17, state.Minute, false)!.Value;
        // Admit only once its deterministic dry lead is active.
        state.Minute = dry.StartMinute + 0.01;
        var wet = DwellPlanner.QueueAtOpening(c, shop, 17, state.Minute, true);
        Assert.NotEqual((Dwell?)dry, wet);
        Assert.Equal(dry, DwellPlanner.QueueAtOpening(c, shop, 17, state.Minute, true, dry));
        string saved = SaveCodec.Write(state);
        var loaded = SaveCodec.Read(saved).State!;
        var sim = new CitySimulator(loaded, new NoRoutes()); sim.AttachOptionalStops(new NoOptionalGeometry { Raining = true }, rate);
        Assert.Equal(saved, SaveCodec.Write(loaded));
        Assert.Equal(wet, DwellPlanner.QueueAtOpening(loaded.Citizens.Single(), shop, 17, loaded.Minute, true));
        Assert.Equal(dry, DwellPlanner.QueueAtOpening(loaded.Citizens.Single(), shop, 17, loaded.Minute, false));
        // Identical save supports both histories: no saved weather/lead/body/slot distinguishes them.
        Assert.Null(DwellPlanner.QueueAtOpening(c, shop, 17, opening, true, dry));
        var foreign = new Dwell("someone-else", DwellKind.QueueAtOpening, "shop", dry.StartMinute, opening, "shop");
        Assert.Equal(wet, DwellPlanner.QueueAtOpening(c, shop, 17, state.Minute, true, foreign));
        sim.AdvanceTo(opening); Assert.Equal(CitizenPhase.AtPlace, loaded.Citizens.Single().Phase); Assert.Single(loaded.Citizens.Single().RecentVisits);
    }

    private sealed class NoRoutes : IRoutePlanner
    {
        public Route? Plan(Place from, Place to, double departureMinute, TravelPreferences preferences) => null;
    }

    private sealed class NoOptionalGeometry : IOptionalStopEnvironment
    {
        public bool Raining { get; set; }
        public IReadOnlyList<OptionalStopSpot> Spots(Place place, DwellKind kind) => Array.Empty<OptionalStopSpot>();
        public OptionalStopAvailability Available(OptionalStopState stop) => OptionalStopAvailability.Ready;
    }

    [Theory]
    [InlineData(0, false)] [InlineData(25, false)] [InlineData(100, false)]
    [InlineData(0, true)] [InlineData(25, true)] [InlineData(100, true)]
    public void OpeningQueueReloadAndAdmissionAreIndependentOfOptionalStopRate(int rate, bool raining)
    {
        // Both ordinary morning opening and a night opening, under the same waiting-state contract.
        foreach (int hour in new[] { 9, 3 })
        {
            double opening = Day + hour * 60;
            var shop = new Place { Id = "shop", Name = "Shop", Kind = PlaceKind.Shop, Capacity = 1,
                Position = new(10, 0, 0), OpenMinute = hour * 60, CloseMinute = (hour + 4) * 60 };
            var home = new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1 };
            var state = Population.Create(new[] { new CitizenIdentity { Id = "queue0", Name = "Queue visitor", Age = 30, Persona = Persona.Commuter } },
                new[] { home, shop }, "e2a-queue", "0.40.4", 17, opening - 1);
            var c = state.Citizens.Single();
            c.Phase = CitizenPhase.WaitingForOpening; c.PlaceId = c.DestinationId = shop.Id;
            c.DestinationActivity = c.Activity = Activity.Shop; c.NextEventMinute = opening; c.ActivityUntilMinute = opening + 60;
            var env = new NoOptionalGeometry { Raining = raining };
            var sim = new CitySimulator(state, new NoRoutes()); sim.AttachOptionalStops(env, rate);
            var original = DwellPlanner.QueueAtOpening(c, shop, state.Seed, state.Minute, raining);
            Assert.NotNull(original);
            Assert.InRange(opening - original!.Value.StartMinute, 2, raining ? 5 : 10);
            string saved = SaveCodec.Write(state);
            var read = SaveCodec.Read(saved); Assert.True(read.Status == SaveReadStatus.Loaded, read.Error);
            var loaded = new CitySimulator(read.State!, new NoRoutes()); loaded.AttachOptionalStops(env, rate);
            var restored = DwellPlanner.QueueAtOpening(loaded.State.Citizens.Single(), shop, state.Seed, state.Minute, raining);
            Assert.Equal(original, restored); Assert.Equal(saved, SaveCodec.Write(loaded.State));
            sim.AdvanceTo(opening); loaded.AdvanceTo(opening);
            Assert.Equal(SaveCodec.Write(sim.State), SaveCodec.Write(loaded.State));
            Assert.Equal(CitizenPhase.AtPlace, loaded.State.Citizens.Single().Phase);
            Assert.Single(loaded.State.Citizens.Single().RecentVisits);
            Assert.Null(DwellPlanner.QueueAtOpening(loaded.State.Citizens.Single(), shop, state.Seed, opening, raining));
            if (rate == 0)
            {
                string admitted = SaveCodec.Write(loaded.State);
                foreach (int otherRate in new[] { 25, 100 })
                {
                    // Replay the identical waiting state under the other rates, comparing
                    // both the queue commitment and the admitted save image.
                    var other = SaveCodec.Read(saved); Assert.True(other.Status == SaveReadStatus.Loaded, other.Error);
                    var otherSim = new CitySimulator(other.State!, new NoRoutes()); otherSim.AttachOptionalStops(env, otherRate);
                    Assert.Equal(original, DwellPlanner.QueueAtOpening(other.State!.Citizens.Single(), shop, state.Seed, opening - 1, raining));
                    otherSim.AdvanceTo(opening);
                    string otherAdmitted = SaveCodec.Write(otherSim.State);
                    if (raining) Assert.Equal(admitted, otherAdmitted);
                    else
                    {
                        // Dry admission also draws an optional-stop chance. That banked
                        // choice may differ by rate; every other persisted fact must agree.
                        var baseline = JObject.Parse(admitted); var variant = JObject.Parse(otherAdmitted);
                        ((JObject)baseline["Citizens"]![0]!).Remove("OptionalStopChoice");
                        ((JObject)variant["Citizens"]![0]!).Remove("OptionalStopChoice");
                        Assert.True(JToken.DeepEquals(baseline, variant), "Queue admission changed across optional-stop rates.");
                    }
                }
            }
        }
    }
}
