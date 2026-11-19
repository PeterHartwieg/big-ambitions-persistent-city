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

    private (long Bytes, int Traces, string Hash, double Milliseconds, double Slowest) Lunch(bool observe, bool serialize)
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
        double total = 0, slowest = 0;
        for (double minute = 2 * 1440 + 11 * 60; minute < 2 * 1440 + 14 * 60; minute += 1.0 / 30)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            city.AdvanceTo(minute, 64);
            double elapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            total += elapsed; slowest = Math.Max(slowest, elapsed);
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        city.SynchronizeSnapshot();
        return (bytes, traces, RushSpikeReplay.Sha(DevelopmentChain.Write(state)), total, slowest);
    }

    [Fact]
    public void FoodDecisionTracingChangesNoDecisionAndItsCostStaysOffTheUnobservedPath()
    {
        var plain = Lunch(false, false);
        var traced = Lunch(true, false);
        var recorded = Lunch(true, true);
        string T((long Bytes, int Traces, string Hash, double Milliseconds, double Slowest) run) => run.Bytes + " B, "
            + run.Milliseconds.ToString("0") + " ms total, slowest frame " + run.Slowest.ToString("0.00") + " ms";
        output.WriteLine("11:00-14:00 food city (.NET Debug): unobserved " + T(plain) + "; traced (" + traced.Traces + " traces) " + T(traced)
            + "; traced with the decision JSON line " + T(recorded));
        Assert.Equal(0, plain.Traces);
        Assert.True(traced.Traces > 100, "traces " + traced.Traces);
        Assert.Equal(plain.Hash, traced.Hash);
        Assert.Equal(plain.Hash, recorded.Hash);
        Assert.True(plain.Bytes < traced.Bytes && traced.Bytes < recorded.Bytes, "bytes " + plain.Bytes + " / " + traced.Bytes + " / " + recorded.Bytes);
    }
}
