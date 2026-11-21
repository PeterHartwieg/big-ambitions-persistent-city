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
            Time.Next();
            spots.TryQueue(place, out var queue);
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
            Time.Next(); spots.TryQueue(places[5], out _);
            Assert.Equal(2, spots.IndexBuilds);
            Assert.Equal(spots.DoorSearches, spots.IndexedSearches);

            var reference = new DwellSpots { IndexDisabled = true };
            Time.Next(); reference.TryQueue(places[5], out var expected);
            spots.Invalidate(places[5]);
            Time.Next(); spots.TryQueue(places[5], out var actual);
            Assert.Equal(Describe(expected), Describe(actual));

            spots.ForgetDoors();
            Time.Next(); spots.TryQueue(places[9], out _);
            Assert.Equal(3, spots.IndexBuilds);
        }
        finally { CityManager.Instance = saved; NavMesh.Reset(); }
    }
}
