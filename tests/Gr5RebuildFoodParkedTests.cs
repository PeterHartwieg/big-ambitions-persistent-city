using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5RebuildFoodParkedTests
{
    private readonly ITestOutputHelper output;
    public Gr5RebuildFoodParkedTests(ITestOutputHelper output) { this.output = output; }

    private static readonly string[] Ids = { "", "rlo:rig:c00042", "visitor:0007", "é-ü:Ω", "p0", "home-12\n", new string('x', 300) };
    private static readonly string[] Suffixes = { ":taste", ":food-time", ":prepared-food:", ":meal:", "", ":optional-stop", ":duration:" };
    private static readonly int[] Days = { 0, 1, 9, 10, 59, 1234567, -1, -80, int.MaxValue, int.MinValue };

    [Fact]
    public void PartHashesEqualTheHashOfTheConcatenatedKey()
    {
        foreach (int seed in new[] { 0, 1, -7, 4321, int.MinValue })
            foreach (var id in Ids)
                foreach (var suffix in Suffixes)
                {
                    Assert.Equal(StableRandom.Hash(id + suffix, seed), StableRandom.Hash(id, suffix, seed));
                    Assert.Equal(StableRandom.Hash(id + suffix + "Lunch", seed), StableRandom.Hash(id, suffix, "Lunch", seed));
                    foreach (int day in Days)
                        Assert.Equal(StableRandom.Hash(id + suffix + day.ToString(CultureInfo.InvariantCulture) + ":pack", seed),
                            StableRandom.Hash(id, suffix, day, ":pack", seed));
                }
        Assert.Equal(StableRandom.Hash(":taste", 3), StableRandom.Hash(null!, ":taste", 3));
    }

    private static string OldContext(FoodRoutineStop stop, bool atStop)
    {
        string text = stop.PlaceId.Length.ToString(CultureInfo.InvariantCulture) + ":" + stop.PlaceId + "|"
            + ((int)stop.Activity).ToString(CultureInfo.InvariantCulture) + "|" + stop.CapturedAt.ToString("R", CultureInfo.InvariantCulture)
            + "|" + stop.DepartureAt.ToString("R", CultureInfo.InvariantCulture);
        using var sha = SHA256.Create();
        return (atStop ? "stop:" : "trip:") + Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
    }

    private static string OldGeometry(Route route)
    {
        var text = new StringBuilder();
        foreach (var leg in route.Legs)
            foreach (var point in new[] { leg.From, leg.To })
                text.Append(point.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(point.Y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(point.Z.ToString("R", CultureInfo.InvariantCulture)).Append(';');
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private static readonly Func<FoodRoutineStop, bool, string> Context = (Func<FoodRoutineStop, bool, string>)typeof(FoodCommerce)
        .GetMethod("Context", BindingFlags.NonPublic | BindingFlags.Static)!.CreateDelegate(typeof(Func<FoodRoutineStop, bool, string>));
    private static readonly Func<Route, string> Geometry = (Func<Route, string>)typeof(FoodCommerce)
        .GetMethod("GeometrySignature", BindingFlags.NonPublic | BindingFlags.Static)!.CreateDelegate(typeof(Func<Route, string>));

    private static readonly double[] Minutes = { 0, -0.0, 1, 1440.5, 1e-300, 1.0 / 3, 87654.321098765, double.MaxValue, -2.5e-7, 123456789012.5 };

    [Fact]
    public void FoodContextAndGeometryKeysAreUnchanged()
    {
        var dice = new System.Random(0x15B);
        foreach (var place in Ids.Where(i => i.Length > 0))
            foreach (var at in Minutes)
                foreach (bool atStop in new[] { true, false })
                {
                    var stop = new FoodRoutineStop { PlaceId = place, Activity = (Activity)(dice.Next(8)), CapturedAt = at, DepartureAt = at + dice.NextDouble() * 90 };
                    Assert.Equal(OldContext(stop, atStop), Context(stop, atStop));
                }
        for (int round = 0; round < 200; round++)
        {
            var route = new Route();
            int legs = 1 + dice.Next(40);
            for (int i = 0; i < legs; i++)
                route.Legs.Add(new RouteLeg { From = Point(dice), To = Point(dice), Minutes = dice.NextDouble(), Mode = TravelMode.Walk });
            Assert.Equal(OldGeometry(route), Geometry(route));
        }
    }

    private static Position Point(System.Random dice) => dice.Next(4) == 0
        ? new Position(Minutes[dice.Next(Minutes.Length)], dice.Next(3) - 1, -Minutes[dice.Next(Minutes.Length)])
        : new Position((dice.NextDouble() - .5) * 4800, dice.NextDouble() * 3, (dice.NextDouble() - .5) * 4800);

    [Fact]
    public void RivalFingerprintIsUnchanged()
    {
        var hours = new[] { new FoodAdvertisedHours(0, 480, 1260), new FoodAdvertisedHours(3, 0, 1440), new FoodAdvertisedHours(6, 59, 61) };
        foreach (var owner in new[] { "rival:1", "é-ü:Ω", new string('o', 200) })
        {
            var observation = new FoodAlternativeObservation("b1", "shop-1", "Main", 4, 0, "ba:businesstype_fastfood", owner,
                FoodAlternativeKind.Rival, true, true, false, 80, 0, new[] { new FoodAdvertisedOffer(FoodProducts.Croissant, Money.FromCents(500)) }, hours);
            var text = new StringBuilder().Append(owner.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(owner);
            foreach (var w in hours) text.Append('|').Append(w.Day).Append(':').Append(w.FromMinute).Append(':').Append(w.ToMinute);
            using var sha = SHA256.Create();
            string expected = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())), 0, 14).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            Assert.Equal(expected, RivalFoodTerms.Fingerprint(observation));
        }
    }

    [Fact]
    public void FoodKeysAllocateOnlyTheirResultAfterWarmUp()
    {
        var stop = new FoodRoutineStop { PlaceId = "rlo:place:0042", Activity = Activity.Work, CapturedAt = 2345.25, DepartureAt = 2791.123456789 };
        var route = new Route();
        for (int i = 0; i < 24; i++) route.Legs.Add(new RouteLeg { From = new Position(i * 1.37, .25, -i * 3.1), To = new Position(i * 1.37 + 1, .25, -i * 3.1 - 2), Minutes = .5, Mode = TravelMode.Walk });
        Context(stop, true); Geometry(route); OldContext(stop, true); OldGeometry(route);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) { Context(stop, i % 2 == 0); Geometry(route); }
        long now = (GC.GetAllocatedBytesForCurrentThread() - before) / 100;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) { OldContext(stop, i % 2 == 0); OldGeometry(route); }
        long old = (GC.GetAllocatedBytesForCurrentThread() - before) / 100;
        output.WriteLine("context+geometry bytes per call: before " + old + ", after " + now);
        Assert.InRange(now, 1, 400);
        Assert.True(now * 10 < old, now + " vs " + old);
    }

    private sealed class Copy { public bool Live = true; public int Model; }

    [Fact]
    public void OwnCarPoolTakeReturnAndEvictionAllocateNothingAfterWarmUp()
    {
        var spare = new Stack<Copy>();
        for (int i = 0; i < 16; i++) spare.Push(new Copy());
        var pool = new OwnCarPool<Copy>(6, c => c.Live, c => { c.Live = false; spare.Push(c); }, createsPerFrame: 2);
        Func<int, Copy?> create = model => { var c = spare.Pop(); c.Live = true; c.Model = model; return c; };
        string[] models = { "a", "b", "c", "d" };
        var held = new List<Copy>(8);
        int frame = 0;
        void Cycle(int rounds)
        {
            for (int round = 0; round < rounds; round++)
            {
                frame++;
                var copy = pool.Take(models[round % 4], frame, create, round % 4);
                if (copy != null) held.Add(copy);
                if (held.Count > 3 || round % 4 == 0 && held.Count > 0) { var back = held[0]; held.RemoveAt(0); pool.Return(models[back.Model], back); }
                if (pool.TakeIdle(models[(round + 1) % 4]) is Copy idle) pool.Return(models[idle.Model], idle);
            }
        }
        void Fresh(string[] names) { foreach (var model in names) { var made = pool.Take(model, ++frame, create, 0); if (made != null) pool.Return("a", made); } }
        Cycle(400);
        Fresh(new[] { "w0", "w1", "w2", "w3", "w4", "w5", "w6", "w7" });
        string[] fresh = { "e", "f", "g", "h", "i", "j", "k", "l" };
        long creations = pool.Creations, least = long.MaxValue;
        for (int pass = 0; pass < 3 && least != 0; pass++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            Cycle(4000);
            Fresh(fresh);
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Assert.Equal(0, least);
        Assert.True(pool.Creations >= creations + fresh.Length, pool.Creations + " vs " + creations + ", count " + pool.Count + ", held " + held.Count);
        Assert.True(pool.Count <= 6);
    }

    [Fact]
    public void ClockRetimeRunsOneStagePerFrameInTheOriginalOrder()
    {
        var stages = new List<ClockRetimeStage>();
        double native = 2, state = 1, routes = 1;
        bool bodies = true;
        for (int frame = 0; frame < 6; frame++)
        {
            var stage = ClockRetimeStages.Next(native, state, routes, bodies);
            stages.Add(stage);
            if (stage == ClockRetimeStage.ReleaseBodies) bodies = false;
            else if (stage == ClockRetimeStage.RetimeJourneys) state = native;
            else if (stage == ClockRetimeStage.RebuildSimulation) routes = state;
            else bodies = true;
        }
        Assert.Equal(new[] { ClockRetimeStage.ReleaseBodies, ClockRetimeStage.RetimeJourneys, ClockRetimeStage.RebuildSimulation,
            ClockRetimeStage.None, ClockRetimeStage.None, ClockRetimeStage.None }, stages);
        Assert.Equal(ClockRetimeStage.RetimeJourneys, ClockRetimeStages.Next(3, 2, 1, false));
        Assert.Equal(ClockRetimeStage.ReleaseBodies, ClockRetimeStages.Next(3, 2, 1, true));
        Assert.Equal(ClockRetimeStage.None, ClockRetimeStages.Next(1.0000001, 1, 1, true));
        Assert.Equal(ClockRetimeStage.RebuildSimulation, ClockRetimeStages.Next(2, 2, 1, true));
    }

    [Fact]
    public void AStagedRetimeEndsWithTheSameSaveAsTheSingleFrameRetime()
    {
        string Run(bool staged)
        {
            var state = Gr5AllocationMeasure.RigCity();
            var city = new CitySimulator(state, new Gr5AdvanceSegmentMeasure.Straight());
            city.AttachFoodMarket(new VisitorRoutineTests.Market { Shops = F5gPopulationFillTests.Shops(state) });
            double day = Math.Floor(state.Minute / 1440) * 1440 + 1440;
            city.AdvanceTo(day + 12 * 60, int.MaxValue);
            state.PrepareForSave();
            TravelTiming.Apply(state, 1.4, 2, out _);
            if (staged)
            {
                Assert.Equal(ClockRetimeStage.RebuildSimulation, ClockRetimeStages.Next(2, state.TravelClockRate, 1, false));
                city.RefreshOptionalStops();
            }
            city = city.RebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight());
            for (double minute = day + 12 * 60; minute < day + 14 * 60; minute += 1.0 / 30)
                for (int unit = 0; unit < 64; unit++)
                {
                    if (city.PlanningPending) { city.ContinuePlanning(1); continue; }
                    if (city.AdvanceFrameSlice(minute, 1).CaughtUp) break;
                }
            return PersistentCity.Persistence.SaveCodec.Write(state);
        }
        Assert.Equal(Run(false), Run(true));
    }

    [Fact]
    public void CameraPanReleasesAtMostFourBodiesPerFrameAndAllLeaveWithinAFewFrames()
    {
        using var f = new VendorBodyTests.Fixture(people: 12, cap: 12, stayMinutes: 300);
        for (int i = 0; i < 16; i++) { Time.Next(0.3f); f.Bodies.Update(); }
        int bound = f.Pool.Active.Count;
        Assert.True(bound > PhysicalPopulation.DistanceReleasesPerFrame, bound.ToString(CultureInfo.InvariantCulture));
        var bodies = f.Pool.Active.Values.ToList();
        NativeTravelPace.Focus = new(5000, 0, 5000);
        var left = new List<int>();
        for (int frame = 0; frame < 4; frame++) { Time.Next(0.02f); f.Bodies.Update(); left.Add(f.Pool.Active.Count); }
        var expected = Enumerable.Range(1, 4).Select(k => Math.Max(0, bound - k * PhysicalPopulation.DistanceReleasesPerFrame)).ToList();
        Assert.Equal(expected, left);
        Assert.All(bodies, b => Assert.False(b.Valid));
        NativeTravelPace.Focus = new(100, 0, 0);
    }

    [Fact]
    public void ParkedContinuationFramesDoNotRescanOrAllocate()
    {
        StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear(); PhysicalPedestrians.VisiblePoint = null;
        var home = new Position(100, 0, 0);
        NativeTravelPace.Focus = home; Time.Next(3);
        var ids = Enumerable.Range(0, 40).Select(i => new CitizenIdentity { Id = "p" + i, Name = "P" + i, Age = 30, AppearanceSeed = 7 }).ToList();
        var state = Population.Create(ids, new[] {
            new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 40, Position = home },
            new Place { Id = "work", Name = "Work", Kind = PlaceKind.Work, Capacity = 40, Position = new Position(2000, 0, 0) } }, "gr5", "0.40.4", 5, 1440 + 600);
        foreach (var c in state.Citizens)
        {
            c.HomeId = c.PlaceId = "home"; c.DayPlaceId = "work"; c.MealOuting = null; c.Phase = CitizenPhase.AtPlace; c.Activity = Activity.Home;
            c.Preferences.OwnsCar = true; c.NextEventMinute = 1440 * 3; c.ActivityUntilMinute = 1440 * 3;
            c.Car = new CarState { Id = c.Identity.Id + ":car", ParkedPosition = new Position(100 + 2 * state.Citizens.IndexOf(c), 0, 8) };
        }
        var city = new CitySimulator(state, new OwnCarTests.NoRoutes());
        using var bodies = new PhysicalPopulation(new Transform { Component = new PopulationRuntime() }, city, new MultimodalRoutes(),
            typeof(OwnCarTests).Assembly, new VendorBodyTests.Log(), 8, 24, 120, new DwellSpots());
        var vehicles = PhysicalVehicles.Last!;
        vehicles.ParkedPlacementLimit = 2; vehicles.ParkedAttemptLimit = 4;
        foreach (var c in state.Citizens) vehicles.Blocked.Add(c.Car!.Id);
        Time.Next(2); bodies.Update();
        for (int frame = 0; frame < 30; frame++) { Time.Next(0.1f); bodies.Update(); }
        int scans = bodies.ParkedScans, continuations = vehicles.ParkedContinuations;
        long bytes = 0; int quiet = 0;
        for (int frame = 0; frame < 100; frame++)
        {
            Time.Next(0.1f);
            int scansBefore = bodies.ParkedScans, before = vehicles.ParkedContinuations;
            long start = GC.GetAllocatedBytesForCurrentThread();
            bodies.Update();
            long spent = GC.GetAllocatedBytesForCurrentThread() - start;
            if (bodies.ParkedScans == scansBefore && vehicles.ParkedContinuations > before) { bytes += spent; quiet++; }
        }
        output.WriteLine("scans " + (bodies.ParkedScans - scans) + ", continuations " + (vehicles.ParkedContinuations - continuations)
            + ", continuation-frame bytes " + bytes + " over " + quiet + " frames");
        Assert.InRange(bodies.ParkedScans - scans, 9, 11);
        Assert.True(quiet >= 40, quiet.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(0, bytes);
    }
}
