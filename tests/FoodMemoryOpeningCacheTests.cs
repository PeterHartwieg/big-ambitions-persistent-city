using System;
using System.Linq;
using System.Threading;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class FoodMemoryOpeningCacheTests(ITestOutputHelper output)
{
    [Fact]
    public void ReusedHoursCacheMatchesTemporaryPlacesAndValidatesMutations()
    {
        var arrival = new FoodBusiness { PlaceId = "arrival", Hours = Enumerable.Range(0, 7)
            .Select(d => new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 }).ToList() };
        var stop = new FoodBusiness { PlaceId = "stop", Hours = arrival.Hours.ToList() };
        const int iterations = 10000;
        long fresh = 0, priorRound = 0, reused = 0; double oldResult = 0, priorResult = 0, newResult = 0;
        Exception? failure = null;
        Place Temporary(FoodBusiness b) => new() { Id = b.PlaceId, Kind = PlaceKind.Shop, OpenMinute = 0, CloseMinute = 0, WeeklyHours = b.Hours };
        double Checks(bool cache, bool cacheDoor = true)
        {
            // Arrival closure, represented admission, reached stop and returning-door closure.
            double result = DailyRoutine.Open(cache ? arrival.OpeningPlace : Temporary(arrival), 2160) ? 1 : 0;
            var admission = cache ? arrival.OpeningPlace : Temporary(arrival);
            result += DailyRoutine.NextOpen(admission, 2160, 2220) ?? -1;
            result += DailyRoutine.NextClose(admission, 2160) ?? -1;
            var reached = cache ? stop.OpeningPlace : Temporary(stop);
            result += DailyRoutine.Open(reached, 2160) ? 1 : 0;
            result += DailyRoutine.NextClose(reached, 2160) ?? -1;
            result += DailyRoutine.Open(cache && cacheDoor ? arrival.OpeningPlace : Temporary(arrival), 2160) ? 1 : 0;
            return result;
        }
        // Keep runner work off the allocation counter's thread; thread/setup costs are excluded.
        var worker = new Thread(() =>
        {
            try
            {
                for (int i = 0; i < 100; i++) { Checks(false); Checks(true, false); Checks(true); }
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < iterations; i++) oldResult += Checks(false);
                fresh = GC.GetAllocatedBytesForCurrentThread() - before;
                before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < iterations; i++) priorResult += Checks(true, false);
                priorRound = GC.GetAllocatedBytesForCurrentThread() - before;
                before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < iterations; i++) newResult += Checks(true);
                reused = GC.GetAllocatedBytesForCurrentThread() - before;
            }
            catch (Exception e) { failure = e; }
        });
        worker.Start(); worker.Join();
        Assert.Null(failure); Assert.Equal(oldResult, newResult); Assert.Equal(priorResult, newResult);
        Assert.True(reused < priorRound); Assert.True(priorRound < fresh);
        output.WriteLine($"Hours checks, {iterations} batches (4 sites): temporary={fresh} bytes ({fresh / iterations}/batch), round3={priorRound} bytes ({priorRound / iterations}/batch), cached={reused} bytes ({reused / iterations}/batch).");
        arrival.Hours.Clear();
        Assert.False(DailyRoutine.Open(arrival.OpeningPlace, 2160));
        arrival.Hours = new() { new() { Day = 0, FromMinute = 600, ToMinute = 800 } };
        Assert.True(DailyRoutine.Open(arrival.OpeningPlace, 2160));
        arrival.Hours[0].FromMinute = 730;
        Assert.False(DailyRoutine.Open(arrival.OpeningPlace, 2160));
    }
}
