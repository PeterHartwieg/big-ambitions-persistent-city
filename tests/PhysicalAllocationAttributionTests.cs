using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PhysicalAllocationAttributionTests
{
    [Fact]
    public void NestedSpansPartitionTheParentExactly()
    {
        var sampler = new GcPauseSampler(64);
        sampler.BeginNested(0, 0);
        Assert.False(sampler.Nested);
        sampler.Start(1);
        sampler.BeginNested(1000, 0);
        sampler.Charge(GcAllocationSegment.PhysicalUnattributed, 1010, 0);
        sampler.Charge(GcAllocationSegment.PhysicalAmbient, 1110, 0);
        sampler.Charge(GcAllocationSegment.PhysicalSpawnAppearance, 5110, 0);
        sampler.Charge(GcAllocationSegment.PhysicalSpawnAppearance, 6110, 0);
        sampler.EndNested(GcAllocationSegment.Physical, GcAllocationSegment.PhysicalUnattributed, 6150, 0);
        Assert.False(sampler.Nested);
        Assert.Equal(5150, sampler.SegmentBytes(GcAllocationSegment.Physical));
        Assert.Equal(1, sampler.SegmentSamples(GcAllocationSegment.Physical));
        Assert.Equal(50, sampler.SegmentBytes(GcAllocationSegment.PhysicalUnattributed));
        Assert.Equal(100, sampler.SegmentBytes(GcAllocationSegment.PhysicalAmbient));
        Assert.Equal(5000, sampler.SegmentBytes(GcAllocationSegment.PhysicalSpawnAppearance));
        Assert.Equal(2, sampler.SegmentSamples(GcAllocationSegment.PhysicalSpawnAppearance));

        sampler.BeginNested(7000, 3);
        sampler.Charge(GcAllocationSegment.PhysicalAmbient, 7100, 3);
        sampler.Charge(GcAllocationSegment.PhysicalSelection, 7200, 4);
        sampler.EndNested(GcAllocationSegment.Physical, GcAllocationSegment.PhysicalUnattributed, 7300, 4);
        sampler.BeginNested(8000, 0);
        sampler.Charge(GcAllocationSegment.PhysicalAmbient, 7900, 0);
        sampler.EndNested(GcAllocationSegment.Physical, GcAllocationSegment.PhysicalUnattributed, 8000, 0);
        Assert.Equal(5150, sampler.SegmentBytes(GcAllocationSegment.Physical));
        Assert.Equal(2, sampler.SegmentSkipped(GcAllocationSegment.Physical));
        Assert.Equal(2, sampler.SegmentSkipped(GcAllocationSegment.PhysicalAmbient));
        Assert.Equal(1, sampler.SegmentSkipped(GcAllocationSegment.PhysicalSelection));
        Assert.Equal(100, sampler.SegmentBytes(GcAllocationSegment.PhysicalAmbient));

        long children = 0;
        for (int i = (int)GcAllocationSegment.PhysicalAmbient; i < GcPauseSampler.Segments; i++) children += sampler.SegmentBytes((GcAllocationSegment)i);
        Assert.Equal(sampler.SegmentBytes(GcAllocationSegment.Physical), children);
    }

    [Fact]
    public void ChargingAllocatesNothingAndIsOffWithoutAMeasurement()
    {
        GcAllocationCounter.Probe();
        Assert.False(PhysicalAllocation.Begin(null));
        var idle = new GcPauseSampler(64);
        Assert.False(PhysicalAllocation.Begin(idle));
        PhysicalAllocation.Charge(GcAllocationSegment.PhysicalAmbient);
        PhysicalAllocation.End();
        Assert.Equal(0, idle.SegmentSamples(GcAllocationSegment.PhysicalAmbient));

        var sampler = new GcPauseSampler(64);
        sampler.Start(1, GcAllocationCounter.Probe());
        Assert.True(PhysicalAllocation.Begin(sampler));
        PhysicalAllocation.Charge(GcAllocationSegment.PhysicalAmbient);
        PhysicalAllocation.End();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int frame = 0; frame < 1024; frame++)
        {
            PhysicalAllocation.Begin(sampler);
            for (int i = (int)GcAllocationSegment.PhysicalAmbient; i < GcPauseSampler.Segments; i++) PhysicalAllocation.Charge((GcAllocationSegment)i);
            PhysicalAllocation.End();
        }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.Equal(1025, sampler.SegmentSamples(GcAllocationSegment.Physical));
        Assert.Equal(0, sampler.SegmentBytes(GcAllocationSegment.Physical));
    }

    [Fact]
    public void CounterProbePrefersPerThreadAllocatedBytes()
    {
        Assert.Equal("thread", GcAllocationCounter.Probe());
        Assert.Equal(0, GcAllocationCounter.Collections);
        long before = GcAllocationCounter.Read();
        var block = new byte[100_000];
        Assert.True(GcAllocationCounter.Read() - before >= block.Length);
        GC.Collect();
        Assert.True(GcAllocationCounter.Read() >= before + block.Length);
    }

    [Fact]
    public void ReportCarriesEverySegmentAndTheCounter()
    {
        var sampler = new GcPauseSampler(64);
        sampler.Start(1, "boehm_total");
        using var report = System.Text.Json.JsonDocument.Parse(sampler.ReportJson(new GcReportContext(1, 1, null, 0, false, 1, 1)));
        Assert.Equal("boehm_total", report.RootElement.GetProperty("alloc_counter").GetString());
        foreach (string name in new[] { "physical_unattributed", "physical_spawn_appearance", "physical_spawn_check", "physical_spawn_pedestrian", "physical_spawn_vehicle",
            "physical_vendor_meals", "physical_fade", "physical_focus", "physical_observe", "physical_release", "physical_parked", "physical_index",
            "physical_queues", "physical_roster", "physical_selection" })
            Assert.Equal(0, report.RootElement.GetProperty(name + "_alloc_bytes").GetInt64());
    }

    private static CitizenIdentity Person(string id, int seed = 7) => new CitizenIdentity { Id = id, Name = "N " + id, Age = 31, AppearanceSeed = seed };

    [Fact]
    public void IdleBodiesGoBackToTheSamePersonAndOtherwiseTheOldestIsRedressed()
    {
        var pool = new DressedBodyPool<string>();
        Assert.False(pool.TryTake(new PersonDressing(Person("a"), 60), out _, out _));
        pool.Return("body-a", new PersonDressing(Person("a"), 60));
        pool.Return("body-b", new PersonDressing(Person("b"), 60));
        pool.Return("body-fresh", default);
        Assert.True(pool.TryTake(new PersonDressing(Person("b"), 60), out var body, out bool dressed));
        Assert.Equal("body-b", body); Assert.True(dressed);
        Assert.True(pool.TryTake(new PersonDressing(Person("a", 8), 60), out body, out dressed));
        Assert.Equal("body-a", body); Assert.False(dressed);
        pool.Return("body-c", new PersonDressing(Person("c"), 60));
        Assert.True(pool.TryTake(new PersonDressing(Person("c"), 61), out body, out dressed));
        Assert.Equal("body-fresh", body); Assert.False(dressed);
        Assert.True(pool.TryTake(default, out body, out dressed));
        Assert.Equal("body-c", body); Assert.False(dressed);
        Assert.Equal(0, pool.Count);
        Assert.False(default(PersonDressing).Equals(default(PersonDressing)));
    }

    [Fact]
    public void NativeTargetSuffixMatchesTheFormattedSuffixWithoutAllocating()
    {
        string[] ids = { "native:road:v1:12:345", "native:road:v1:12:-345", "native:road:v1:0", "native:road:v1:10", "native:road:v1:x10", "345", ":345", "", "native:road:v1:12:2147483647", "native:road:v1:12:-2147483648" };
        int[] targets = { 0, 1, 5, 10, 45, 345, -345, 2147483647, int.MinValue };
        foreach (string id in ids)
            foreach (int target in targets)
                Assert.Equal(id.EndsWith(":" + target.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal), PhysicalPopulation.EndsWithTarget(id, target));
        Assert.False(PhysicalPopulation.EndsWithTarget(null!, 1));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) PhysicalPopulation.EndsWithTarget(ids[0], 345);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Fact]
    public void ParkedNearestIntoAReusedListMatchesTheAllocatingForm()
    {
        var cars = Enumerable.Range(0, 40).Select(i => new ParkedCar("car-" + (i * 7 % 40).ToString("D2"), "o" + i, new Position(i % 9, 0, i % 5), 0)).ToList();
        var focus = new Position(3, 0, 2);
        var into = new List<ParkedCar> { new ParkedCar("stale", "x", focus, 0) };
        foreach (int limit in new[] { 0, 1, 6, 100 })
        {
            var expected = ParkedCars.Nearest((IEnumerable<ParkedCar>)cars, focus, 4, limit);
            var actual = ParkedCars.Nearest(cars, focus, 4, limit, into);
            Assert.Same(into, actual);
            Assert.Equal(expected.Select(c => (c.CarId, c.Distance)), actual.Select(c => (c.CarId, c.Distance)));
        }
        ParkedCars.Nearest(cars, focus, 4, 6, into);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) ParkedCars.Nearest(cars, focus, 4, 6, into);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Fact]
    public void DoorKeyIsCachedPerPlaceAndFollowsMoves()
    {
        var spots = new DwellSpots();
        var place = new Place { Id = "shop-1", Position = new Position(10.04, 0, -3.26) };
        Assert.Equal(DwellSpots.KeyOf(place), spots.Key(place));
        string first = spots.Key(place);
        long before = GC.GetAllocatedBytesForCurrentThread();
        string last = "";
        for (int i = 0; i < 100; i++) last = spots.Key(place);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.Same(first, last);
        place.Position = new Position(11, 0, -3.26);
        Assert.Equal(DwellSpots.KeyOf(place), spots.Key(place));
        Assert.NotEqual(first, spots.Key(place));
        var vendor = new Place { Id = VendorNames.PlacePrefix + "v1", Position = place.Position };
        Assert.Equal(vendor.Id, spots.Key(vendor));
    }
}
