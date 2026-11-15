using System;
using System.Linq;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5RateCacheTests
{
    private readonly ITestOutputHelper output;
    public Gr5RateCacheTests(ITestOutputHelper output) { this.output = output; }

    [Fact]
    public void ARateSeenBeforeReturnsANetworkEqualToAFreshRerate()
    {
        var one = new TrafficNetwork("gr5-rate", Gr5RetimeReuseTests.Grid(9, 1), 1);
        var twentyNodes = Gr5RetimeReuseTests.Grid(9, 20);
        var twentySignals = twentyNodes.ToDictionary(n => n.Id, n => n.Signal);
        var twenty = one.Rerated(20, id => twentySignals[id]);
        var cache = new TrafficRateCache();
        cache.Remember(1, one);
        cache.Remember(20, twenty);
        Assert.True(cache.TryGet(1, out var back));
        Assert.Same(one, back);
        var oneNodes = Gr5RetimeReuseTests.Grid(9, 1);
        var oneSignals = oneNodes.ToDictionary(n => n.Id, n => n.Signal);
        Gr5RetimeReuseTests.AssertSame(twenty.Rerated(1, id => oneSignals[id]), back, oneNodes);
        Gr5RetimeReuseTests.AssertSame(new TrafficNetwork("gr5-rate", oneNodes, 1), back, oneNodes);
        Assert.False(cache.TryGet(2, out _));
    }

    [Fact]
    public void TheCacheKeepsTheMostRecentlyUsedRatesWithinItsCapacity()
    {
        var network = new TrafficNetwork("gr5-rate", Gr5RetimeReuseTests.Grid(3, 1), 1);
        var cache = new TrafficRateCache();
        cache.Remember(1, network); cache.Remember(2, network); cache.Remember(20, network);
        Assert.True(cache.TryGet(1, out _));
        cache.Remember(5, network);
        Assert.Equal(TrafficRateCache.Capacity, cache.Count);
        Assert.True(cache.TryGet(1, out _)); Assert.True(cache.TryGet(20, out _)); Assert.True(cache.TryGet(5, out _));
        Assert.False(cache.TryGet(2, out _));
        cache.Remember(0, network); cache.Remember(double.NaN, network);
        Assert.False(cache.TryGet(0, out _));
        Assert.Equal(TrafficRateCache.Capacity, cache.Count);
    }

    [Fact]
    public void AReturnToAKnownRateAllocatesNothingWhereARerateAllocatesTheWholeNetwork()
    {
        var nodes = Gr5RetimeReuseTests.Grid(40, 1);
        var one = new TrafficNetwork("gr5-rate", nodes, 1);
        var twentySignals = Gr5RetimeReuseTests.Grid(40, 20).ToDictionary(n => n.Id, n => n.Signal);
        var oneSignals = nodes.ToDictionary(n => n.Id, n => n.Signal);
        var twenty = one.Rerated(20, id => twentySignals[id]);
        var cache = new TrafficRateCache();
        cache.Remember(1, one); cache.Remember(20, twenty);
        _ = cache.TryGet(1, out _);
        long started = GC.GetAllocatedBytesForCurrentThread();
        _ = twenty.Rerated(1, id => oneSignals[id]);
        long rerate = GC.GetAllocatedBytesForCurrentThread() - started;
        started = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) { Assert.True(cache.TryGet(i % 2 == 0 ? 1 : 20, out _)); cache.Remember(i % 2 == 0 ? 1 : 20, one); }
        long reused = GC.GetAllocatedBytesForCurrentThread() - started;
        output.WriteLine("links " + one.Links.Count + ": rerate " + rerate + " B, 100 cached returns " + reused + " B");
        Assert.Equal(0, reused);
        Assert.True(rerate > 100_000, rerate.ToString());
    }
}
