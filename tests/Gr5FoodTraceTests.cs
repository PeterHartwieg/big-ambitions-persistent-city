using System;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5FoodTraceTests
{
    private readonly ITestOutputHelper output;
    public Gr5FoodTraceTests(ITestOutputHelper output) { this.output = output; }

    private (long Bytes, int Traces, string Hash) Lunch(bool observe, bool serialize)
    {
        var (state, city, _) = Gr5AdvanceAllocationTests.City();
        while (!city.AdvanceTo(2 * 1440 + 11 * 60, 256).CaughtUp) { }
        int traces = 0;
        if (observe) city.FoodDecisionObserved += trace =>
        {
            traces++;
            if (serialize) _ = JsonConvert.SerializeObject(new { resident = trace.Consumer.Id, trace.Intent.Id, trace.MealKind, trace.Intent.BusinessId,
                vendors = trace.Businesses.Where(b => VendorNames.IsVendorPlace(b.PlaceId)), trace.ChoiceCosts, trace.Decision, trace.RouteChecks });
        };
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (double minute = 2 * 1440 + 11 * 60; minute < 2 * 1440 + 14 * 60; minute += 1.0 / 30) city.AdvanceTo(minute, 64);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        city.SynchronizeSnapshot();
        return (bytes, traces, RushSpikeReplay.Sha(DevelopmentChain.Write(state)));
    }

    [Fact]
    public void FoodDecisionTracingChangesNoDecisionAndItsCostStaysOffTheUnobservedPath()
    {
        var plain = Lunch(false, false);
        var traced = Lunch(true, false);
        var recorded = Lunch(true, true);
        output.WriteLine("11:00-14:00 food city (.NET): unobserved " + plain.Bytes + " B, traced " + traced.Bytes + " B (" + traced.Traces
            + " traces), traced with the decision JSON line " + recorded.Bytes + " B");
        Assert.Equal(0, plain.Traces);
        Assert.True(traced.Traces > 100, "traces " + traced.Traces);
        Assert.Equal(plain.Hash, traced.Hash);
        Assert.Equal(plain.Hash, recorded.Hash);
        Assert.True(plain.Bytes < traced.Bytes && traced.Bytes < recorded.Bytes, "bytes " + plain.Bytes + " / " + traced.Bytes + " / " + recorded.Bytes);
    }
}
