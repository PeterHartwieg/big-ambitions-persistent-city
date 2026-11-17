using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5EventStepTests
{
    private readonly ITestOutputHelper output;
    public Gr5EventStepTests(ITestOutputHelper output) { this.output = output; }

    [Fact]
    public void AReusedFoodIntentMatchesAFreshOneInEveryProperty()
    {
        var dirty = new FoodIntent();
        var properties = typeof(FoodIntent).GetProperties().Where(p => p.CanWrite && p.Name != nameof(FoodIntent.Choices)).ToList();
        Assert.True(properties.Count >= 24);
        foreach (var p in properties)
        {
            var type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            object value = type == typeof(string) ? "dirty" : type == typeof(int) ? 7 : type == typeof(double) ? 3.5
                : type == typeof(Money) ? Money.FromCents(123) : type.IsEnum ? Enum.GetValues(type).Cast<object>().Last()
                : throw new InvalidOperationException("Unhandled FoodIntent property " + p.Name);
            p.SetValue(dirty, value);
            Assert.NotEqual(JsonConvert.SerializeObject(p.GetValue(new FoodIntent())), JsonConvert.SerializeObject(p.GetValue(dirty)));
        }
        var choices = dirty.Choices;
        var reused = FoodCommerce.FreshIntent(dirty);
        Assert.Same(dirty, reused);
        Assert.Same(choices, reused.Choices);
        Assert.Equal(JsonConvert.SerializeObject(new FoodIntent()), JsonConvert.SerializeObject(reused));
        dirty.Choices.Add(new FoodChoice());
        Assert.Throws<InvalidOperationException>(() => FoodCommerce.FreshIntent(dirty));
    }

    [Fact]
    public void TheBufferedShortlistMatchesThePublicOneAndAllocatesNothingAfterWarmUp()
    {
        var state = Gr5AllocationMeasure.RigCity();
        var optional = state.Places.Where(p => p.Kind == PlaceKind.Shop || p.Kind == PlaceKind.Leisure).ToList();
        var homes = state.Places.Where(p => p.Kind == PlaceKind.Home).Take(60).ToList();
        var ranked = new List<(Place Place, double Score)>(9);
        foreach (var origin in homes)
            foreach (var kind in new[] { PlaceKind.Shop, PlaceKind.Leisure })
            {
                string key = origin.Id + ":optional:" + kind;
                DestinationChoice.Shortlist(optional, origin, kind, key, state.Seed, ranked);
                Assert.Equal(DestinationChoice.Shortlist(optional, origin, kind, key, state.Seed).Select(p => p.Id), ranked.Select(r => r.Place.Id));
            }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 2000; i++) DestinationChoice.Shortlist(optional, homes[i % homes.Count], PlaceKind.Shop, "warm", state.Seed, ranked);
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 1024);
    }

    private static (long[] Bytes, long[] Calls, double[] Max) Window(CitySimulator city, double from, double to)
    {
        var bytes = new long[GcPauseSampler.Segments]; var calls = new long[GcPauseSampler.Segments]; var max = new double[GcPauseSampler.Segments];
        for (double minute = from; minute < to; minute += 1.0 / 30)
            for (int unit = 0; unit < 64; unit++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread(); long started = Stopwatch.GetTimestamp();
                var result = city.AdvanceFrameSlice(minute, 1);
                double ms = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
                int s = (int)GcPauseSampler.AdvanceEventSegment(result.TrafficEvents, result.FoodContinuationEvents, result.CitizenEvents, result.PlanningPending);
                bytes[s] += GC.GetAllocatedBytesForCurrentThread() - before; calls[s]++; max[s] = Math.Max(max[s], ms);
                if (result.CaughtUp) break;
            }
        return (bytes, calls, max);
    }

    [Fact]
    public void CitizenEventsAndFoodContinuationsStayUnderTheirByteAndFrameBudgets()
    {
        var food = (int)GcAllocationSegment.AdvanceEventsFood; var citizen = (int)GcAllocationSegment.AdvanceEventsCitizen;
        double foodMax = double.MaxValue, citizenMax = double.MaxValue;
        long[]? bytes = null, calls = null;
        for (int run = 0; run < 3; run++)
        {
            var (_, city) = Gr5FoodDecisionTests.FoodCity();
            while (!city.AdvanceTo(2 * 1440 + 11 * 60, 256).CaughtUp) { }
            var window = Window(city, 2 * 1440 + 11 * 60, 2 * 1440 + 20 * 60);
            bytes = window.Bytes; calls = window.Calls;
            foodMax = Math.Min(foodMax, window.Max[food]); citizenMax = Math.Min(citizenMax, window.Max[citizen]);
        }
        Assert.True(calls![food] > 3000 && calls[citizen] > 5000, calls[food] + " / " + calls[citizen]);
        double foodPer = bytes![food] / (double)calls[food], citizenPer = bytes[citizen] / (double)calls[citizen];
        output.WriteLine($"food {calls[food]} calls {foodPer:0} B/call max {foodMax:0.000} ms; citizen {calls[citizen]} calls {citizenPer:0} B/event max {citizenMax:0.000} ms");
        PerfGate.Under(foodPer, 1150, "bytes per food continuation (.NET)");
        PerfGate.Under(citizenPer, 1700, "bytes per citizen event (.NET)");
        PerfGate.Under(foodMax, 10, "slowest food continuation unit");
        PerfGate.Under(citizenMax, 10, "slowest citizen event unit");
    }

    [Fact]
    public void EveryCompleteAdvanceFrameThroughTheLunchFoodWindowStaysUnder30Ms()
    {
        const double from = 2 * 1440 + 11.5 * 60, to = 2 * 1440 + 13.5 * 60;
        string uninterrupted = RushSpikeReplay.RunUninterrupted(from, to);
        RushSpikeReplay.Result? result = null;
        try
        {
            TimingBudget.Run(timing =>
            {
                result = RushSpikeReplay.Run(from, to);
                Assert.Equal(uninterrupted, result.SaveSha256);
                Assert.True(result.Purchases > 0 && result.Frames > 10000, result.Purchases + " purchases, " + result.Frames + " frames");
                timing.Under("complete advance frame max", result.FrameMax, 30);
                timing.Under("complete advance frame p95", result.FrameP95, 5);
                timing.Under("single unit max", result.UnitMax, 10);
            });
        }
        finally
        {
            if (result != null)
                output.WriteLine($"frames={result.Frames}; p95={result.FrameP95:F4}ms; frameMax={result.FrameMax:F4}ms at minute {result.MaxFrameMinute} with {result.CollectionsInMaxFrame} gen0; unitMax={result.UnitMax:F4}ms ({result.MaxUnitKind}); spikes30 frame/unit={result.FrameSpikes30}/{result.UnitSpikes30}; maxUnitsInFrame={result.MaxUnitsInFrame}");
        }
    }
}
