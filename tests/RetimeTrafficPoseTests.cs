using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class RetimeTrafficPoseTests
{
    private const string Native = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly string Stand = VendorNames.PlaceId("cart-a");
    private static readonly Position StandAt = new Position(394.0625913, 0.42, 301.7718259);
    private readonly ITestOutputHelper output;
    public RetimeTrafficPoseTests(ITestOutputHelper output) { this.output = output; }

    private static readonly Position[] Nodes = { new Position(311.3710463, 0.42, 268.9128117), new Position(338.9014472, 0.42, 280.0371125), new Position(357.2287735, 0.42, 291.6639044), new Position(389.5137713, 0.42, 299.3301184) };
    private static readonly Position[] Corners = { new Position(186.9084517, 0.42, 112.5121386), new Position(296.1184409, 0.42, 251.5530911), new Position(299.8471235, 0.42, 254.9043378), new Position(301.2918846, 0.42, 259.1180372), new Position(305.6602131, 0.42, 261.4473229), new Position(308.0271517, 0.42, 266.2015503) };

    private sealed class Planner : IRoutePlanner, ITrafficRoutePlanner
    {
        public Planner(double rate)
        {
            TrafficNetwork = new TrafficNetwork(Native, Nodes.Select((p, i) => new TrafficNetworkNode
            {
                Id = i, Position = p, KilometresPerHour = 36, AllowsCar = true, Boundary = i == 0 || i == Nodes.Length - 1,
                Successors = i + 1 < Nodes.Length ? new[] { i + 1 } : Array.Empty<int>()
            }).ToArray(), rate);
        }
        public TrafficNetwork? TrafficNetwork { get; set; }
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) { }
        public bool IsTrafficLinkClosed(int id) => false;
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) => null;
    }

    private static CitySimulator LunchDrive(bool walking = false)
    {
        var home = Corners[0]; var stand = StandAt;
        var state = Population.Create(new[] { new CitizenIdentity { Id = "pv:985", Name = "Lunch driver", Age = 30, AppearanceSeed = 3, Persona = Persona.Commuter } }, new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 1, Position = home },
        }, "retime-pose-test", "0.40.4", 7, 1440);
        state.Food = new FoodCommerceState();
        var scope = new FoodScope { AlternativeKind = FoodAlternativeKind.Player, PlaceId = Stand, ProductId = FoodProducts.Croissant };
        scope.UseChannel(FoodScopeChannel.Vehicle);
        state.Food.Shops.Add(scope);
        var row = state.Vendors.Admit("cart-a", "cart");
        row.Stand = new VendorStand { OpeningId = 1, Position = stand, OpenedDay = 1, OpenedMinute = 0, ClosesDay = 1, ClosesMinute = 1400, Published = true };
        var c = state.Citizens[0];
        c.PlaceId = c.HomeId = "home"; c.DayPlaceId = "home";
        c.Phase = CitizenPhase.Travelling; c.DestinationId = Stand; c.DestinationActivity = Activity.Shop;
        c.Preferences.OwnsCar = !walking;
        c.Car = walking ? null : new CarState { Id = c.Identity.Id + ":car", ParkedPosition = Nodes[0] };
        var route = new Route();
        var walk = Corners.Append(Nodes[0]).ToArray();
        for (int i = 0; i + 1 < walk.Length; i++)
            route.Legs.Add(new RouteLeg { From = walk[i], To = walk[i + 1], Minutes = Position.Distance(walk[i], walk[i + 1]) / 1.4, Mode = TravelMode.Walk });
        for (int i = 0; i + 1 < Nodes.Length; i++)
            route.Legs.Add(walking
                ? new RouteLeg { From = Nodes[i], To = Nodes[i + 1], Minutes = Position.Distance(Nodes[i], Nodes[i + 1]) / 1.4, Mode = TravelMode.Walk }
                : new RouteLeg { From = Nodes[i], To = Nodes[i + 1], Minutes = Position.Distance(Nodes[i], Nodes[i + 1]) / 10 * 1.3, Mode = TravelMode.Car, ServiceId = "native:road:v1:" + Native + ":" + i + ":" + (i + 1) });
        route.Legs.Add(new RouteLeg { From = Nodes[Nodes.Length - 1], To = stand, Minutes = Position.Distance(Nodes[Nodes.Length - 1], stand) / 1.4, Mode = TravelMode.Walk });
        c.Journey = route; c.DepartureMinute = 1440; c.NextEventMinute = 1440 + route.Minutes;
        c.ActivityUntilMinute = c.NextEventMinute + 20;
        c.MealOuting = new MealOuting { OriginId = "home", OriginActivity = Activity.Home, ResumeUntil = 1600, ResumeEvent = 1600 };
        var city = new CitySimulator(state, new Planner(1));
        city.AttachFoodMarket(new VisitorRoutineTests.Market());
        return city;
    }

    private static double Boundary(CitySimulator city, int leg)
    {
        double end = 0;
        var legs = city.State.Citizens[0].Journey!.Legs;
        for (int i = 0; i <= leg; i++) end += legs[i].Minutes;
        return end;
    }

    private static CitySimulator JustPast(CitySimulator city, double past)
    {
        var c = city.State.Citizens[0];
        city.AdvanceTo(1440 + Boundary(city, Corners.Length - 1) + 0.5, int.MaxValue);
        Assert.True(city.TryGetTrafficPermission(c.Identity.Id, out var start));
        double target = Boundary(city, Corners.Length) + past;
        city.AdvanceTo(city.State.Minute + (target - start.NominalMinutes), int.MaxValue);
        Assert.True(city.TryGetTrafficPermission(c.Identity.Id, out var at));
        Assert.InRange(at.NominalMinutes - Boundary(city, Corners.Length), 1e-12, 1e-6);
        return city;
    }

    private static Position Pose(CitySimulator city, CitizenState person) => city.SampleJourney(person).Position;

    private static (double End, Position To) CurrentLeg(CitySimulator city, CitizenState person)
    {
        double progress = city.State.Minute - person.DepartureMinute, end = 0;
        foreach (var leg in person.Journey!.Legs)
        {
            end += leg.Minutes;
            if (end > progress + 1e-6) return (person.DepartureMinute + end, leg.To);
        }
        throw new InvalidOperationException("no current leg");
    }

    private static (ClockRetimeDriver Driver, CitySimulator City) Retime(CitySimulator city, double rate, bool strict)
    {
        var driver = new ClockRetimeDriver { StrictPose = strict };
        while (driver.Update(city.State, rate, false, 1.4, 0) != ClockRetimeOutcome.Committed) { }
        var rebuild = city.BeginRebuildRoutes(new Planner(rate), driver.TakeRetimedTraffic());
        while (!rebuild.Step(0)) { }
        return (driver, rebuild.Result!);
    }

    [Fact]
    public void AQueuedFollowerKeepsItsExactPermissionThroughARescale()
    {
        int queued = 0;
        for (int seed = 0; seed < 64; seed++)
        {
            var r = new Random(seed);
            double free = 2 + r.NextDouble() * 6;
            var links = new[]
            {
                new TrafficLinkDefinition { Id = 0, Capacity = r.Next(2, 8), FreeFlowMinutes = free, HeadwayMinutes = 0.1 + r.NextDouble() * 0.3,
                    Signal = new TrafficSignalSchedule { CycleMinutes = 4 + r.NextDouble() * 4, GreenStartMinutes = 0, GreenDurationMinutes = 0.3 + r.NextDouble() * 0.5, PhaseOffsetMinutes = r.NextDouble() } },
                new TrafficLinkDefinition { Id = 1, Capacity = 10, FreeFlowMinutes = 3, HeadwayMinutes = 0.1 },
            };
            double start = 1 + r.NextDouble() * 2;
            var kernel = new TrafficKernel(links, 1440);
            int cars = r.Next(2, 9);
            double clock = 1440;
            for (int i = 0; i < cars; i++)
            {
                kernel.Enqueue("car" + i, new[]
                {
                    new TrafficRouteLeg { LinkId = 0, StartNominalMinutes = start, EndNominalMinutes = start + free, FreeFlowMinutes = free, StartLinkFraction = 0, EndLinkFraction = 1 },
                    new TrafficRouteLeg { LinkId = 1, StartNominalMinutes = start + free, EndNominalMinutes = start + free + 3, FreeFlowMinutes = 3, StartLinkFraction = 0, EndLinkFraction = 1 },
                });
                clock += 0.05 + r.NextDouble() * 0.6;
                kernel.AdvanceTo(clock, 100000);
            }
            kernel.AdvanceTo(clock + r.NextDouble() * free, 100000);
            var observed = new Dictionary<string, double>();
            for (int i = 0; i < cars; i++)
                if (kernel.TryGetVehicle("car" + i, out var before)) observed[before.VehicleId] = before.NominalMinutes;
            double factor = r.NextDouble() < 0.5 ? 20 : 1.0 / 20;
            var rescaled = kernel.RescaleTime(factor, (id, v) => v <= observed[id] ? v : observed[id] + (v - observed[id]) * factor);
            foreach (var pair in observed)
            {
                var after = rescaled.GetPermission(pair.Key);
                if (after.LinkId.HasValue && after.RouteIndex == 0) queued++;
                Assert.True(after.NominalMinutes == pair.Value, "seed " + seed + " " + pair.Key + " moved from " + pair.Value.ToString("R") + " to " + after.NominalMinutes.ToString("R"));
            }
        }
        output.WriteLine(queued + " cars on the signalled link at a rescale");
        Assert.True(queued > 50, "only " + queued + " cars were on the signalled link");
    }

    [Fact]
    public void ACarJustPastALegStartFailsLoudlyUnderStrictRetime()
    {
        foreach (double past in new[] { 3e-10, 3e-9, 3e-8 })
        {
            var city = JustPast(LunchDrive(), past);
            var error = Assert.ThrowsAny<ArgumentException>(() => Retime(city, 20, strict: true));
            Assert.Contains("measured physical pose", error.Message);
        }
    }

    private static readonly Position OfficeAt = new Position(396.5, 0.42, 303.25);
    private static readonly Position FlatAt = new Position(380.25, 0.42, 300.5);
    private const string Walker = "pv:990";

    private static CitySimulator Commute(int drivers)
    {
        var identities = Enumerable.Range(0, drivers).Select(i => new CitizenIdentity { Id = "pv:" + (985 + i), Name = "Driver " + i, Age = 30, AppearanceSeed = 3 + i, Persona = Persona.Commuter })
            .Append(new CitizenIdentity { Id = Walker, Name = "Lunch walker", Age = 30, AppearanceSeed = 9, Persona = Persona.Commuter });
        var state = Population.Create(identities, new[]
        {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 8, Position = Corners[0] },
            new Place { Id = "flat", Name = "Flat", Kind = PlaceKind.Home, Capacity = 1, Position = FlatAt },
            new Place { Id = "office", Name = "Office", Kind = PlaceKind.Work, Capacity = 8, Position = OfficeAt },
        }, "retime-pose-test", "0.40.4", 7, 1440);
        state.Food = new FoodCommerceState();
        var scope = new FoodScope { AlternativeKind = FoodAlternativeKind.Player, PlaceId = Stand, ProductId = FoodProducts.Croissant };
        scope.UseChannel(FoodScopeChannel.Vehicle);
        state.Food.Shops.Add(scope);
        var row = state.Vendors.Admit("cart-a", "cart");
        row.Stand = new VendorStand { OpeningId = 1, Position = StandAt, OpenedDay = 1, OpenedMinute = 0, ClosesDay = 1, ClosesMinute = 1400, Published = true };
        row.PlacePublished = true;
        for (int d = 0; d < drivers; d++)
        {
            var c = state.Citizens[d];
            c.PlaceId = c.HomeId = "home"; c.DayPlaceId = "office";
            c.Phase = CitizenPhase.Travelling; c.DestinationId = "office"; c.DestinationActivity = Activity.Work;
            c.Preferences.OwnsCar = true;
            c.Car = new CarState { Id = c.Identity.Id + ":car", ParkedPosition = Nodes[0] };
            var route = new Route();
            var walk = Corners.Append(Nodes[0]).ToArray();
            for (int i = 0; i + 1 < walk.Length; i++)
                route.Legs.Add(new RouteLeg { From = walk[i], To = walk[i + 1], Minutes = Position.Distance(walk[i], walk[i + 1]) / 1.4, Mode = TravelMode.Walk });
            for (int i = 0; i + 1 < Nodes.Length; i++)
                route.Legs.Add(new RouteLeg { From = Nodes[i], To = Nodes[i + 1], Minutes = Position.Distance(Nodes[i], Nodes[i + 1]) / 10 * 1.3, Mode = TravelMode.Car, ServiceId = "native:road:v1:" + Native + ":" + i + ":" + (i + 1) });
            route.Legs.Add(new RouteLeg { From = Nodes[Nodes.Length - 1], To = OfficeAt, Minutes = Position.Distance(Nodes[Nodes.Length - 1], OfficeAt) / 1.4, Mode = TravelMode.Walk });
            c.Journey = route; c.DepartureMinute = 1440 - 0.3 * d; c.NextEventMinute = c.DepartureMinute + route.Minutes;
            c.ActivityUntilMinute = c.NextEventMinute + 480;
        }
        var w = state.Citizens[drivers];
        w.PlaceId = w.HomeId = w.DayPlaceId = "flat"; w.Phase = CitizenPhase.AtPlace; w.Activity = Activity.Home;
        w.NextEventMinute = w.ActivityUntilMinute = 1440 + 600;
        var city = new CitySimulator(state, new Planner(1));
        city.AttachFoodMarket(new VisitorRoutineTests.Market());
        city.PublishVendorPlace(row, null);
        return city;
    }

    private static ResidentMeal BookLunch(CitySimulator city)
    {
        var walk = new Route { Legs = new() { new RouteLeg { From = FlatAt, To = StandAt, Minutes = Position.Distance(FlatAt, StandAt) / 1.4, Mode = TravelMode.Walk } } };
        Assert.True(city.TryBeginMealVisit(Walker, Stand, walk, 12, 20, 1440 + 500));
        var person = city.State.Citizens.Single(c => c.Identity.Id == Walker);
        var meal = city.State.Food!.Residents.SingleOrDefault(m => m.ResidentId == Walker);
        if (meal == null) city.State.Food.Residents.Add(meal = new ResidentMeal { ResidentId = Walker, AnchorId = Walker });
        meal.Day = 1; meal.Kind = MealKind.Lunch; meal.Status = MealStatus.Travelling; meal.PlaceId = Stand;
        meal.BusinessId = city.State.Vendors.Find("cart-a")!.BusinessId; meal.ProductId = FoodProducts.Croissant; meal.ReturnMinutes = 12; meal.DueAt = person.NextEventMinute;
        return meal;
    }

    private static void AssertLunchBooked(CitySimulator city, ResidentMeal meal, MealOuting outing, double stay)
    {
        var person = city.State.Citizens.Single(c => c.Identity.Id == Walker);
        Assert.Equal(CitizenPhase.Travelling, person.Phase);
        Assert.Same(outing, person.MealOuting);
        Assert.Equal(Stand, person.DestinationId);
        Assert.Same(meal, Assert.Single(city.State.Food!.Residents, m => m.ResidentId == Walker));
        Assert.Equal(MealStatus.Travelling, meal.Status);
        Assert.Equal(person.NextEventMinute, meal.DueAt);
        Assert.Equal(person.DepartureMinute + person.Journey!.Minutes, person.NextEventMinute, 9);
        Assert.Equal(stay, person.ActivityUntilMinute - person.NextEventMinute, 9);
        var references = new VendorReferences();
        city.CollectVendorReferences(references);
        Assert.Contains(Stand, references.ShortLived);
    }

    [Fact]
    public void ACarJustPastALegStartDegradesAtItsNewPaceAcrossOneToTwentyToOneWhileABookedLunchWalkerKeepsItsBooking()
    {
        var city = Commute(1);
        var driver = city.State.Citizens[0];
        city.AdvanceTo(1440 + Boundary(city, Corners.Length - 1) - 5, int.MaxValue);
        var meal = BookLunch(city);
        var walker = city.State.Citizens.Single(c => c.Identity.Id == Walker);
        var outing = walker.MealOuting!;
        double stay = walker.ActivityUntilMinute - walker.NextEventMinute;
        JustPast(city, 3e-9);
        var pose = Pose(city, driver);
        var walkerPose = Pose(city, walker);
        double remaining = driver.NextEventMinute - city.State.Minute, walkerRemaining = walker.NextEventMinute - city.State.Minute;

        var (fastDriver, fast) = Retime(city, 20, strict: false);
        Assert.Equal(1, fastDriver.PoseDegraded);
        Assert.Equal(1, fastDriver.Degraded);
        Assert.Equal(0, fastDriver.TimingKept);
        Assert.Equal(20, fast.State.TravelClockRate);
        Assert.Equal(CitizenPhase.Travelling, driver.Phase);
        Assert.Equal("office", driver.DestinationId);
        Assert.False(fast.TryGetTrafficPermission(driver.Identity.Id, out _));
        Assert.InRange(Position.Distance(pose, Pose(fast, driver)), 0, 1e-6);
        Assert.InRange(driver.NextEventMinute - fast.State.Minute, remaining * 20 * 0.999, remaining * 20 * 1.001);
        Assert.Equal(walkerPose, Pose(fast, walker));
        Assert.InRange(walker.NextEventMinute - fast.State.Minute, walkerRemaining * 20 * 0.999, walkerRemaining * 20 * 1.001);
        AssertLunchBooked(fast, meal, outing, stay);

        fast.AdvanceTo(fast.State.Minute + 0.5, int.MaxValue);
        double fastRemaining = driver.NextEventMinute - fast.State.Minute;
        var (slowDriver, slow) = Retime(fast, 1, strict: false);
        output.WriteLine("1x remaining " + remaining.ToString("R") + ", 20x " + fastRemaining.ToString("R") + ", back at 1x " + (driver.NextEventMinute - slow.State.Minute).ToString("R")
            + "; degraded back " + slowDriver.Degraded + ", inexact pose " + slowDriver.PoseDegraded);
        Assert.Equal(1, slow.State.TravelClockRate);
        Assert.InRange(driver.NextEventMinute - slow.State.Minute, fastRemaining / 20 * 0.999, fastRemaining / 20 * 1.001);
        AssertLunchBooked(slow, meal, outing, stay);

        slow.AdvanceTo(walker.NextEventMinute + 0.01, int.MaxValue);
        Assert.Equal(Stand, walker.PlaceId);
        Assert.Equal(CitizenPhase.AtPlace, walker.Phase);
        Assert.Same(outing, walker.MealOuting);
        slow.AdvanceTo(1440 + 400, int.MaxValue);
        Assert.Equal("office", driver.PlaceId);
        Assert.Equal(1, driver.CompletedJourneys);
    }

    [Fact]
    public void ALateEvictedLeaderLeavesItsLinkSpanUnjoinedAndOvertakesNothingWhileTheFollowerKeepsTraffic()
    {
        var city = Commute(2);
        var state = city.State;
        var leader = state.Citizens[1];
        var follower = state.Citizens[0];
        city.AdvanceTo(1440 + Boundary(city, Corners.Length - 1) + 1.1, int.MaxValue);
        Assert.True(city.TryGetTrafficPermission(leader.Identity.Id, out var ahead));
        Assert.True(city.TryGetTrafficPermission(follower.Identity.Id, out var behind));
        Assert.Equal(ahead.LinkId, behind.LinkId);
        var pose = Pose(city, leader);
        var driver = new ClockRetimeDriver { StrictPose = false };
        bool moved = false;
        while (true)
        {
            var outcome = driver.Update(state, 20, false, 1.4, 0);
            if (outcome == ClockRetimeOutcome.Committed) break;
            var pending = driver.Pending;
            if (moved || pending == null || (int)Stage.GetValue(pending)! != 8) continue;
            var vehicles = new List<TrafficVehicleState>();
            ((TrafficKernel)Rescaled.GetValue(pending)!).CopyVehiclesInOrder(vehicles);
            Assert.Equal(2, vehicles.Count);
            vehicles.Single(v => v.VehicleId == leader.Identity.Id).MotionStartFraction = 0.5;
            moved = true;
        }
        Assert.True(moved);
        Assert.Equal(1, driver.PoseDegraded);
        var rebuild = city.BeginRebuildRoutes(new Planner(20), driver.TakeRetimedTraffic());
        while (!rebuild.Step(0)) { }
        var fast = rebuild.Result!;
        Assert.InRange(Position.Distance(pose, Pose(fast, leader)), 0, 1e-6);
        Assert.True(fast.TryGetTrafficPermission(follower.Identity.Id, out _));
        Assert.NotNull(ahead.LinkId);
        var (legEnd, linkEnd) = CurrentLeg(fast, leader);
        Assert.Equal(Nodes[1], linkEnd);
        double previous = Position.Distance(Pose(fast, leader), linkEnd);
        int steps = 0;
        for (double minute = fast.State.Minute + 1; minute < legEnd - 1; minute += 1)
        {
            fast.AdvanceTo(minute, int.MaxValue);
            Assert.False(fast.TryGetTrafficPermission(leader.Identity.Id, out _), "the leader rejoined its link at " + minute.ToString("R"));
            double left = Position.Distance(Pose(fast, leader), linkEnd);
            Assert.True(left < previous, "the leader stopped at " + minute.ToString("R"));
            previous = left; steps++;
        }
        output.WriteLine("the leader drove " + steps + " one-minute steps outside traffic on its link; the follower kept its permission");
        Assert.True(steps >= 10, "only " + steps + " steps");
        fast.AdvanceTo(1440 + 2400, int.MaxValue);
        Assert.Equal("office", leader.PlaceId);
        Assert.Equal("office", follower.PlaceId);
    }

    [Fact]
    public void AWalkerToAVendorStandRetimesBothWaysWithItsExactPose()
    {
        var city = LunchDrive(walking: true);
        city.AdvanceTo(1440 + Boundary(city, Corners.Length + 1) + 3e-9, int.MaxValue);
        var pose = Pose(city, city.State.Citizens[0]);
        var (driver, fast) = Retime(city, 20, strict: true);
        Assert.Equal(0, driver.Degraded);
        Assert.Equal(pose, Pose(fast, fast.State.Citizens[0]));
        var (back, slow) = Retime(fast, 1, strict: true);
        Assert.Equal(0, back.Degraded);
        Assert.Equal(pose, Pose(slow, slow.State.Citizens[0]));
        slow.AdvanceTo(1800, int.MaxValue);
        Assert.Equal(Stand, slow.State.Citizens[0].PlaceId);
    }

    [Fact]
    public void ACarToAVendorStandMidLegRetimesBothWaysWithItsExactPose()
    {
        var city = LunchDrive();
        city.AdvanceTo(1440 + Boundary(city, Corners.Length) + 1.1, int.MaxValue);
        var pose = Pose(city, city.State.Citizens[0]);
        var (driver, fast) = Retime(city, 20, strict: true);
        Assert.Equal(0, driver.Degraded);
        Assert.True(fast.TryGetTrafficPermission("pv:985", out _));
        Assert.Equal(pose, Pose(fast, fast.State.Citizens[0]));
        var (back, slow) = Retime(fast, 1, strict: true);
        Assert.Equal(0, back.Degraded);
        Assert.Equal(pose, Pose(slow, slow.State.Citizens[0]));
        slow.AdvanceTo(1800, int.MaxValue);
        Assert.Equal(Stand, slow.State.Citizens[0].PlaceId);
    }

    private static readonly System.Reflection.FieldInfo Stage = typeof(TravelTiming.TravelRetime).GetField("stage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
    private static readonly System.Reflection.FieldInfo Rescaled = typeof(TravelTiming.TravelRetime).GetField("rescaledKernel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ACarTheRescaledKernelMovesIsEvictedAfterTheRescaleAndKeepsTheCityAttached(bool strict, bool fromSave)
    {
        var city = LunchDrive();
        var state = city.State;
        city.AdvanceTo(1440 + Boundary(city, Corners.Length) + 1.1, int.MaxValue);
        var c = state.Citizens[0];
        var pose = Pose(city, c);
        double stay = c.ActivityUntilMinute - c.NextEventMinute;
        var driver = new ClockRetimeDriver { StrictPose = strict };
        if (fromSave) state.RetimeSnapshotSteps = null;
        bool moved = false;
        void Run()
        {
            while (true)
            {
                var outcome = driver.Update(state, 20, false, 1.4, 0);
                if (outcome == ClockRetimeOutcome.Committed) return;
                var pending = driver.Pending;
                if (moved || pending == null || (int)Stage.GetValue(pending)! != 8) continue;
                var vehicles = new List<TrafficVehicleState>();
                ((TrafficKernel)Rescaled.GetValue(pending)!).CopyVehiclesInOrder(vehicles);
                var vehicle = Assert.Single(vehicles);
                vehicle.MotionStartFraction = 0.5;
                moved = true;
            }
        }
        if (strict)
        {
            Assert.Contains("measured physical pose", Assert.ThrowsAny<ArgumentException>(Run).Message);
            return;
        }
        Run();
        Assert.True(moved);
        Assert.Equal(fromSave ? "a save snapshot" : "the live kernel", driver.RetimedFrom);
        Assert.Equal(1, driver.Degraded);
        Assert.Equal(1, driver.PoseDegraded);
        var retimed = driver.TakeRetimedTraffic()!;
        var rebuild = city.BeginRebuildRoutes(new Planner(20), retimed);
        while (!rebuild.Step(0)) { }
        var fast = rebuild.Result!;
        Assert.Equal(20, state.TravelClockRate);
        Assert.Equal(CitizenPhase.Travelling, c.Phase);
        Assert.Equal(Stand, c.DestinationId);
        Assert.Equal(c.DepartureMinute + c.Journey!.Minutes, c.NextEventMinute, 9);
        Assert.Equal(stay, c.ActivityUntilMinute - c.NextEventMinute, 9);
        Assert.InRange(Position.Distance(pose, Pose(fast, c)), 0, 1e-6);
        fast.AdvanceTo(2400, int.MaxValue);
        Assert.Equal(Stand, c.PlaceId);
        Assert.Equal(1, c.CompletedJourneys);
    }
}
