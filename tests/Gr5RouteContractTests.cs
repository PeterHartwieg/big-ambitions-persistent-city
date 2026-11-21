using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5RouteContractTests
{
    private readonly ITestOutputHelper output;
    public Gr5RouteContractTests(ITestOutputHelper output) { this.output = output; }

    private enum Kind { Car, Truck, Bus }

    [Fact]
    public void EqualContractsShareOneArrayAndAHitAllocatesNothing()
    {
        var contracts = new SharedContracts<int>();
        var first = contracts.Intern(new List<int> { 0, 2, 5 });
        var second = contracts.Intern(new List<int> { 0, 2, 5 });
        var other = contracts.Intern(new List<int> { 0, 5, 2 });
        Assert.Same(first, second);
        Assert.NotSame(first, other);
        Assert.Equal(new[] { 0, 5, 2 }, other);
        Assert.Null(contracts.Intern(null));
        Assert.Empty(contracts.Intern(new List<int>())!);
        Assert.Same(contracts.Intern(new List<int>()), contracts.Intern(new List<int>()));
        var cars = new SharedContracts<Kind>();
        Assert.Same(cars.Intern(new List<Kind> { Kind.Car, Kind.Bus }), cars.Intern(new List<Kind> { Kind.Car, Kind.Bus }));
        var source = new List<int> { 0, 2, 5 };
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) contracts.Intern(source);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void AHashCollisionStillReturnsAnEqualCopy()
    {
        var contracts = new SharedContracts<int>();
        int[] a = { 1, 0 }, b = { 0, 31 };
        Assert.Equal(Hash(a), Hash(b));
        var first = contracts.Intern(a.ToList())!;
        var second = contracts.Intern(b.ToList())!;
        Assert.Equal(a, first); Assert.Equal(b, second);
        Assert.Same(first, contracts.Intern(a.ToList()));
        Assert.Equal(b, contracts.Intern(b.ToList()));
        static int Hash(int[] values) { unchecked { int h = 17 + values.Length; foreach (int v in values) h = h * 31 + v; return h; } }
    }

    [Fact]
    public void RigWaypointContractsCollapseToTheirDistinctSets()
    {
        var random = new Random(7);
        var sets = new[] { new List<int> { 0, 1, 2 }, new List<int> { 0 }, new List<int> { 1, 2 }, new List<int> { 0, 1, 2, 3 } };
        var contracts = new SharedContracts<int>();
        const int waypoints = 29010;
        var kept = new int[waypoints][];
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < waypoints; i++) kept[i] = contracts.Intern(new List<int>(sets[random.Next(sets.Length)]))!;
        long interned = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        var copies = new int[waypoints][];
        for (int i = 0; i < waypoints; i++) copies[i] = new List<int>(sets[random.Next(sets.Length)]).ToArray();
        long copied = GC.GetAllocatedBytesForCurrentThread() - before;
        int distinct = kept.Distinct().Count();
        output.WriteLine(waypoints + " waypoint contracts: " + distinct + " retained arrays (was " + waypoints + "); allocation with the source lists "
            + (interned / 1e6).ToString("0.000") + " MB vs " + (copied / 1e6).ToString("0.000") + " MB (.NET)");
        Assert.Equal(sets.Length, distinct);
        Assert.Equal(sets.Length, contracts.Distinct);
        Assert.True(interned < copied, "interning allocated " + interned + " against " + copied);
    }

    private static readonly MethodInfo Split = typeof(TravelTiming).GetMethod("SplitService", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static string Before(RouteLeg leg)
    {
        if (leg.Mode != TravelMode.Car || !leg.ServiceId.StartsWith("native:road:", StringComparison.Ordinal)) return leg.ServiceId;
        string[] parts = leg.ServiceId.Split(':');
        if (parts.Length == 6 && (parts[2] == "v1" || parts[2] == "fragment1") && parts[3].Length > 0 &&
            int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out int from) &&
            int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out int to) && from != to)
            return parts[2] == "fragment1" ? leg.ServiceId : "native:road:fragment1:" + parts[3] + ":" + parts[4] + ":" + parts[5];
        return "";
    }

    [Fact]
    public void SplitRoadServicesMatchTheSplitParserForEveryTagShape()
    {
        string topology = new string('a', 64);
        var services = new[]
        {
            "native:road:v1:" + topology + ":12:13", "native:road:v1:" + topology + ":007:13", "native:road:fragment1:" + topology + ":12:13",
            "native:road:v1:" + topology + ":12:12", "native:road:v1::12:13", "native:road:v2:" + topology + ":12:13",
            "native:road:v1:" + topology + ":12:13:9", "native:road:v1:" + topology + ":12", "native:road:v1:" + topology + ":+12:13",
            "native:road:v1:" + topology + ": 12:13", "native:road:v1:" + topology + ":-1:13", "native:road:v1:" + topology + ":12:",
            "native:road:v1:" + topology + ":99999999999:13", "native:road:", "native:subway", "", "bus:7", "native:road:fragment1:x:0:1",
        };
        foreach (var mode in new[] { TravelMode.Car, TravelMode.Walk, TravelMode.Subway })
            foreach (string service in services)
            {
                var leg = new RouteLeg { From = new Position(0, 0, 0), To = new Position(1, 0, 0), Minutes = 1, Mode = mode, ServiceId = service };
                Assert.Equal(Before(leg), (string)Split.Invoke(null, new object[] { leg })!);
            }
        var road = new RouteLeg { From = new Position(0, 0, 0), To = new Position(1, 0, 0), Minutes = 1, Mode = TravelMode.Car, ServiceId = services[0] };
        var split = (Func<RouteLeg, string>)Split.CreateDelegate(typeof(Func<RouteLeg, string>));
        split(road); Before(road);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) split(road);
        long now = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) Before(road);
        long old = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine("bytes per split road leg: " + now / 100 + " (was " + old / 100 + ", .NET)");
        Assert.True(now * 2 < old, "split service " + now + " against " + old);
    }
}
