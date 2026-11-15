using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class Gr5RetimeAllocationTests
{
    private readonly ITestOutputHelper output;
    public Gr5RetimeAllocationTests(ITestOutputHelper output) { this.output = output; }

    private const string Retimed = "222E663F2FD8440FF74517903D8F0E151B9DEAB5270102C90E679DB4D0B0F03C";

    private static (string Hash, long[] Bytes) RetimeThroughRates(bool committed)
    {
        var (state, _) = TravelTimingMaximumRouteTests.RigScaleCity(TravelTimingMaximumRouteTests.FixtureLinks);
        if (committed) foreach (var c in state.Citizens) c.Journey = c.Journey!.Commit();
        var simulator = new CitySimulator(state, new NoRoutes());
        var driver = new ClockRetimeDriver();
        var rates = new[] { 3.0, 20, 1, 20 };
        var bytes = new long[rates.Length];
        for (int r = 0; r < rates.Length; r++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            while (true)
            {
                var outcome = driver.Update(state, rates[r], false, 2, ClockRetimeStages.SliceMilliseconds);
                Assert.NotEqual(ClockRetimeOutcome.Restarted, outcome);
                if (outcome != ClockRetimeOutcome.Committed) continue;
                simulator = simulator.RebuildRoutes(new NoRoutes(), driver.TakeRetimedTraffic());
                break;
            }
            bytes[r] = GC.GetAllocatedBytesForCurrentThread() - before;
        }
        simulator.AdvanceTo(state.Minute + 0.5, int.MaxValue);
        state.PrepareForSave();
        return (RushSpikeReplay.Sha(SaveCodec.Write(state)), bytes);
    }

    [Fact]
    public void RetimedJourneysTrafficAndSaveMatchThePinnedPreChangeHash()
    {
        var (hash, bytes) = RetimeThroughRates(committed: false);
        output.WriteLine(hash + "; MB per retime: " + string.Join(", ", bytes.Select(b => (b / 1e6).ToString("0.0"))));
        Assert.Equal(Retimed, hash);
    }

    [Fact]
    public void ARetimeOfCommittedJourneysStaysUnderItsAllocationCeiling()
    {
        var (_, bytes) = RetimeThroughRates(committed: true);
        output.WriteLine("committed journeys, MB per retime: " + string.Join(", ", bytes.Select(b => (b / 1e6).ToString("0.0"))));
        PerfGate.Under(bytes.Skip(1).Max() / 1e6, 70, "MB allocated by one retime at 13,965 links (.NET)");
    }

    [Fact]
    public void AnUnchangedClockRateAllocatesNothingInTheRetimeDriver()
    {
        var (state, _) = TravelTimingMaximumRouteTests.RigScaleCity();
        var driver = new ClockRetimeDriver();
        for (int i = 0; i < 16; i++) Assert.Equal(ClockRetimeOutcome.None, driver.Update(state, state.TravelClockRate, true, 2, 4));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) driver.Update(state, state.TravelClockRate, i % 2 == 0, 2, 4);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
