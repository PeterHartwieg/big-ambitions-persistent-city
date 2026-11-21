using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Buildings;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;
using UnityEngine.AI;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[CollectionDefinition(nameof(Gr5DoorIndexTests), DisableParallelization = true)]
public sealed class Gr5DoorIndexCollection { }

[Collection(nameof(Gr5DoorIndexTests))]
public sealed class Gr5DoorIndexTests
{
    private readonly ITestOutputHelper output;
    public Gr5DoorIndexTests(ITestOutputHelper output) { this.output = output; }

    private static CityManager City(int controllers, int seed, out List<Place> places)
    {
        var random = new Random(seed);
        var city = new CityManager();
        places = new List<Place>();
        for (int c = 0; c < controllers; c++)
        {
            var controller = new CityBuildingController();
            int doors = 1 + random.Next(3);
            float x = (float)(random.NextDouble() * 900 - 450), z = (float)(random.NextDouble() * 900 - 450);
            for (int d = 0; d < doors; d++)
            {
                var door = new Door();
                double angle = random.Next(4) * Math.PI / 2;
                door.doorTransform.position = new Vector3(x + d * 1.7f, 0, z + (float)(random.NextDouble() * 3));
                door.doorTransform.forward = new Vector3((float)Math.Sin(angle), 0, (float)Math.Cos(angle));
                controller.entranceDoors.Add(door);
                var front = door.doorTransform.position + door.doorTransform.forward * 0.5f;
                float jitter = (float)(random.NextDouble() * 3.2);
                places.Add(new Place { Id = "address:" + c + ":" + d + ":", Kind = PlaceKind.Shop,
                    Position = new Position(front.x + jitter, 0, front.z - jitter * 0.5f) });
            }
            if (c % 7 == 0) controller.entranceDoors.Add(null!);
            city.cityBuildingControllers.Add(c % 11 == 0 ? null! : controller);
        }
        return city;
    }

    private static string Text(Position p) => p.X.ToString("R") + "," + p.Y.ToString("R") + "," + p.Z.ToString("R");

    private static string Describe(DoorQueue? queue)
        => queue == null ? "none" : Text(queue.Door) + "|" + Text(queue.Entrance) + "|" + string.Join(";", queue.Spots.Select(Text));

    private static List<string> Queues(DwellSpots spots, IEnumerable<Place> places, out double milliseconds)
    {
        var read = new List<string>();
        long started = Stopwatch.GetTimestamp();
        foreach (var place in places)
        {
            DoorQueue? queue;
            do { Time.Next(); spots.TryQueue(place, out queue); } while (queue == null && spots.Unsampled(place));
            read.Add(Describe(queue));
        }
        milliseconds = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
        return read;
    }

    [Fact]
    public void IndexedDoorSearchFindsTheSameQueuesAsTheFullScanAndScansTheCityOnce()
    {
        var saved = CityManager.Instance;
        try
        {
            NavMesh.Reset();
            NavMesh.Blocked = (a, b) => Math.Abs(a.x - b.x) + Math.Abs(a.z - b.z) > 6;
            CityManager.Instance = City(1800, 41, out var places);
            int doors = CityManager.Instance.cityBuildingControllers.Where(c => c != null).Sum(c => c.entranceDoors.Count(d => d != null));
            var scan = new DwellSpots { IndexDisabled = true };
            var indexed = new DwellSpots();
            var full = Queues(scan, places, out double scanMs);
            var fast = Queues(indexed, places, out double indexMs);
            int found = full.Count(q => q != "none");
            output.WriteLine("places " + places.Count + ", doors " + doors + ", queues found " + found + "; full scan " + (scanMs / places.Count).ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)
                + " ms per sample, indexed " + (indexMs / places.Count).ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture) + " ms per sample (.NET, doubles; Mono transform reads are native calls)");
            Assert.True(found > places.Count / 2, "most places have a door queue: " + found);
            Assert.True(full.Count(q => q == "none") > 0, "some places have no door within 2.5 m or no reachable spot");
            for (int i = 0; i < places.Count; i++) Assert.True(full[i] == fast[i], places[i].Id + ": " + full[i] + " vs " + fast[i]);
            Assert.Equal(1, indexed.IndexBuilds);
            Assert.Equal(doors, indexed.IndexedDoors);
            Assert.Equal(indexed.DoorSearches, indexed.IndexedSearches);
            Assert.Equal(0, scan.IndexedSearches);
            PerfGate.Under(indexMs / places.Count, scanMs / places.Count, "indexed door sample vs full scan");
        }
        finally { CityManager.Instance = saved; NavMesh.Reset(); }
    }

    [Fact]
    public void TheIndexWarmsInSlicesAndRebuildsWhenTheBuildingsChange()
    {
        var saved = CityManager.Instance;
        try
        {
            NavMesh.Reset();
            CityManager.Instance = City(300, 7, out var places);
            var spots = new DwellSpots();
            int slices = 0;
            while (!spots.WarmIndex(0)) slices++;
            Assert.True(slices >= 250, "one controller per slice when the deadline has passed: " + slices);
            Assert.Equal(1, spots.IndexBuilds);
            Assert.True(spots.WarmIndex(0));
            Time.Next(); spots.TryQueue(places[3], out _);
            Assert.Equal(1, spots.IndexBuilds);
            Assert.Equal(1, spots.IndexedSearches);

            var added = new CityBuildingController();
            var door = new Door();
            door.doorTransform.position = new Vector3((float)places[5].Position.X + 0.4f, 0, (float)places[5].Position.Z);
            added.entranceDoors.Add(door);
            CityManager.Instance.cityBuildingControllers.Add(added);
            Assert.False(spots.WarmIndex(0));
            Assert.True(spots.IndexPending());
            while (spots.IndexPending()) { Time.Next(); Assert.False(spots.TryQueue(places[5], out _)); Assert.True(spots.Unsampled(places[5])); }
            Assert.Equal(2, spots.IndexBuilds);
            Assert.Equal(spots.DoorSearches, spots.IndexedSearches);

            var reference = new DwellSpots { IndexDisabled = true };
            Time.Next(); reference.TryQueue(places[5], out var expected);
            spots.Invalidate(places[5]);
            Time.Next(); spots.TryQueue(places[5], out var actual);
            Assert.Equal(Describe(expected), Describe(actual));

            spots.ForgetDoors();
            while (spots.IndexPending()) { Time.Next(); spots.TryQueue(places[9], out _); }
            Assert.Equal(3, spots.IndexBuilds);
        }
        finally { CityManager.Instance = saved; NavMesh.Reset(); }
    }
    [Fact]
    public void AQueueRequestBetweenWarmUpSlicesKeepsThePartialIndexAndWaitsWithoutACooldown()
    {
        var saved = CityManager.Instance;
        try
        {
            NavMesh.Reset();
            NavMesh.Blocked = (a, b) => Math.Abs(a.x - b.x) + Math.Abs(a.z - b.z) > 6;
            CityManager.Instance = City(1800, 23, out var places);
            var reference = new DwellSpots { IndexDisabled = true };
            var expected = Queues(reference, places.Take(40), out _);
            var spots = new DwellSpots();
            for (int i = 0; i < 5; i++) Assert.False(spots.WarmIndex(0));
            int progress = spots.IndexProgress;
            Assert.Equal(5, progress);
            double slowest = 0; int waits = 0;
            var place = places[17];
            while (spots.IndexPending())
            {
                Time.Next();
                long started = Stopwatch.GetTimestamp();
                Assert.False(spots.TryQueue(place, out _));
                slowest = Math.Max(slowest, (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
                Assert.True(spots.SampledThisFrame);
                Assert.True(spots.Unsampled(place), "a waiting request sets no retry cooldown");
                Assert.True(spots.IndexProgress > progress || !spots.IndexPending(), "each waiting request continues the partial index: " + spots.IndexProgress + " after " + progress);
                progress = spots.IndexProgress;
                waits++;
            }
            Time.Next();
            spots.TryQueue(place, out var first);
            Assert.Equal(expected[17], Describe(first));
            output.WriteLine("request between warm-up slices waited " + waits + " updates, slowest request " + slowest.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                + " ms against a " + DwellSpots.IndexSliceMilliseconds + " ms slice (.NET)");
            Assert.True(waits > 0);
            Assert.Equal(waits, spots.PendingRequests);
            Assert.Equal(1, spots.IndexBuilds);
            Assert.Equal(0, spots.DoorSearches - spots.IndexedSearches);
            PerfGate.Under(slowest, 2 * DwellSpots.IndexSliceMilliseconds, "door request while the index is warming");
            var actual = Queues(spots, places.Take(40), out _);
            Assert.Equal(expected, actual);
            Assert.Equal(1, spots.IndexBuilds);
        }
        finally { CityManager.Instance = saved; NavMesh.Reset(); }
    }

    [Fact]
    public void AnIndexedDoorSearchAllocatesNothingAndIndexSlicesKeepTheirDeadline()
    {
        var saved = CityManager.Instance;
        try
        {
            NavMesh.Reset();
            var city = City(1800, 13, out var places);
            CityManager.Instance = city;
            var spots = new DwellSpots();
            double slowest = 0; int slices = 0;
            while (true)
            {
                long started = Stopwatch.GetTimestamp();
                bool done = spots.WarmIndex(started + Stopwatch.Frequency / 1000);
                slowest = Math.Max(slowest, (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
                slices++;
                if (done) break;
            }
            output.WriteLine("door index of " + spots.IndexedDoors + " doors warmed in " + slices + " slices, slowest "
                + slowest.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " ms against a 1 ms deadline (.NET)");
            PerfGate.Under(slowest, 1.5, "door index slice");
            var points = places.Select(p => new Vector3((float)p.Position.X, 0, (float)p.Position.Z)).ToArray();
            foreach (var point in points) spots.FindDoor(city, point);
            long before = GC.GetAllocatedBytesForCurrentThread();
            int found = 0;
            foreach (var point in points) if (spots.FindDoor(city, point) != null) found++;
            long indexed = GC.GetAllocatedBytesForCurrentThread() - before;
            var scan = new DwellSpots { IndexDisabled = true };
            foreach (var point in points) scan.FindDoor(city, point);
            before = GC.GetAllocatedBytesForCurrentThread();
            foreach (var point in points) scan.FindDoor(city, point);
            long scanned = GC.GetAllocatedBytesForCurrentThread() - before;
            output.WriteLine(points.Length + " door searches, " + found + " doors found: indexed " + indexed + " B, full scan " + scanned + " B (.NET)");
            Assert.True(found > points.Length / 2);
            Assert.Equal(0, indexed);
            Assert.Equal(0, scanned);
            Assert.Equal(1, spots.IndexBuilds);
        }
        finally { CityManager.Instance = saved; NavMesh.Reset(); }
    }

    [Fact]
    public void AWalkerArrivingWhileTheDoorIndexWarmsKeepsItsBodyAndJoinsTheQueueAfterTheLookup()
    {
        var saved = CityManager.Instance;
        const double now = 1440 + 600;
        Position home = new(40, 0, 0), shop = new(100, 0, 0);
        PhysicalPopulation? bodies = null;
        try
        {
            StreetFoodHooks.Reset(); NavMesh.Reset(); AmbientPedestrians.Reserved.Clear(); PhysicalPedestrianBody.Unreachable = null;
            PhysicalPedestrians.VisiblePoint = null;
            NativeTravelPace.Focus = shop; Time.Next(3);
            var city = City(1800, 31, out _);
            for (int far = 0; far < 60000; far++)
            {
                var distant = new CityBuildingController();
                var farDoor = new Door();
                farDoor.doorTransform.position = new Vector3(5000 + far % 250 * 30, 0, 5000 + far / 250 * 30);
                farDoor.doorTransform.forward = new Vector3(0, 0, 1);
                distant.entranceDoors.Add(farDoor);
                city.cityBuildingControllers.Add(distant);
            }
            var door = new Door();
            door.doorTransform.position = new Vector3(100, 0, -0.5f);
            door.doorTransform.forward = new Vector3(0, 0, 1);
            var controller = new CityBuildingController();
            controller.entranceDoors.Add(door);
            city.cityBuildingControllers.Add(controller);
            CityManager.Instance = city;
            var state = Population.Create(new List<CitizenIdentity> { new() { Id = "w", Name = "W", Age = 30, AppearanceSeed = 3 } }, new[] {
                new Place { Id = "home", Name = "Home", Kind = PlaceKind.Home, Capacity = 2, Position = home },
                new Place { Id = "shop", Name = "Shop", Kind = PlaceKind.Shop, Capacity = 2, Position = shop, OpenMinute = 600 + 31 } }, "gr5-door", "0.40.4", 3, now);
            var c = state.Citizens[0];
            c.HomeId = "home"; c.MealOuting = null; c.PlaceId = "home"; c.Phase = CitizenPhase.Travelling;
            c.DestinationId = "shop"; c.DestinationActivity = PersistentCity.Core.Activity.Shop;
            c.Journey = PhysicalWalkerFollowTests.Legs(home, shop);
            c.DepartureMinute = now; c.NextEventMinute = now + c.Journey.Minutes; c.ActivityUntilMinute = now + 300;
            var simulation = new CitySimulator(state, new Walking());
            var spots = new DwellSpots();
            bodies = new PhysicalPopulation(new Transform { Component = new PopulationRuntime() }, simulation, new MultimodalRoutes(), typeof(Gr5DoorIndexTests).Assembly,
                new VendorBodyTests.Log(), 8, 3, 100, spots);
            var pool = PhysicalPedestrians.Last!;
            void Tick() { Time.Next(0.3f); bodies.Update(); }
            Tick();
            Assert.True(pool.Active.ContainsKey("w"));
            simulation.AdvanceTo(now + 30);
            Assert.Equal(CitizenPhase.WaitingForOpening, c.Phase);
            Tick();
            Assert.True(pool.Active.ContainsKey("w"));
            pool.Active["w"].Character.transform.position = PhysicalPedestrianBody.Vector(shop);
            spots.ForgetDoors(); spots.Invalidate(simulation.FindPlace("shop"));
            Assert.True(spots.IndexPending());
            long before = spots.PendingRequests;
            Tick();
            Assert.Equal(before + 1, spots.PendingRequests);
            Assert.True(pool.Active.ContainsKey("w"), "the arriving walker keeps its body while its door lookup waits for the index");
            int waits = 1;
            while (spots.IndexPending() && waits < 1000)
            {
                long asked = spots.PendingRequests;
                Tick(); waits++;
                Assert.True(pool.Active.ContainsKey("w"), "the walker keeps its body on deferred update " + waits);
                Assert.Equal(asked + 1, spots.PendingRequests);
            }
            Assert.True(waits > 1, "the index needs several slices, so the walker asks again across updates");
            Tick();
            Assert.True(spots.TryQueue(simulation.FindPlace("shop"), out var line));
            Assert.True(pool.Active.ContainsKey("w"), "the walker joins the queue once the lookup completes");
            Assert.Contains(pool.Active["w"].Target, line!.Spots);
            output.WriteLine("arriving walker waited " + waits + " updates for the door index");
        }
        finally { bodies?.Dispose(); CityManager.Instance = saved; NavMesh.Reset(); }
    }

    private sealed class Walking : IRoutePlanner
    {
        public Route Plan(Place a, Place b, double at, TravelPreferences p) => PhysicalWalkerFollowTests.Legs(a.Position, b.Position);
    }
}
