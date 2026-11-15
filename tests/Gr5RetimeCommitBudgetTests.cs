using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5RetimeCommitBudgetTests
{
    private readonly ITestOutputHelper output;
    public Gr5RetimeCommitBudgetTests(ITestOutputHelper output) { this.output = output; }

    [Fact]
    public void TheCommitUpdateWithFoodStopsAndTrafficStaysUnderTheFrameBudget()
    {
        var (state, city) = Gr5FoodDecisionTests.FoodCity();
        while (!city.AdvanceTo(2 * 1440 + 12 * 60, 256).CaughtUp) { }
        Assert.NotNull(state.Food);
        Assert.True(state.Citizens.Count(c => c.Phase == CitizenPhase.Travelling) > 20);
        var driver = new ClockRetimeDriver();
        var commits = new List<double>();
        foreach (double rate in new[] { 2.0, 1, 3, 1 })
        {
            while (true)
            {
                long started = Stopwatch.GetTimestamp();
                var outcome = driver.Update(state, rate, false, 1.4, ClockRetimeStages.SliceMilliseconds);
                if (outcome != ClockRetimeOutcome.Committed) continue;
                city = city.RebuildRoutes(new Gr5AdvanceSegmentMeasure.Straight(), driver.TakeRetimedTraffic());
                commits.Add((Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
                break;
            }
            Gr5FoodDecisionTests.Run(city, state.Minute, state.Minute + 2);
        }
        Assert.True(city.ActiveJourneyIndexMatchesState());
        output.WriteLine("commit and rebuild ms with food, stops, finances and traffic: " + string.Join(", ", commits.Select(c => c.ToString("0.00"))));
        PerfGate.Under(commits.Skip(1).Max(), 30, "retime commit and simulator rebuild with food, optional stops and traffic");
    }
}
