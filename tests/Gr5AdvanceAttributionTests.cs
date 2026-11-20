using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class Gr5AdvanceAttributionTests
{
    private readonly ITestOutputHelper output;
    public Gr5AdvanceAttributionTests(ITestOutputHelper output) { this.output = output; }

    [Fact]
    public void ThePlanningSliceAnatomySeparatesTheRouteJobItsSlowestUnitAndNavMeshTime()
    {
        string text = PlanningSliceAnatomy.Text(892.516, 892.4, "direct-walk", 892.3, 891.9, 1, 2);
        Assert.Equal("Slowest planning slice: 892.516 ms, route job 892.400 ms, outside the route job 0.116 ms; its slowest unit direct-walk 892.300 ms, "
            + "NavMesh paths 891.900 ms over 1 queries, other work 0.400 ms, 2 NavMesh samples.", text);
        Assert.Equal("Slowest planning slice: 3.000 ms, route job 0.000 ms, outside the route job 3.000 ms; its slowest unit none 0.000 ms, "
            + "NavMesh paths 0.000 ms over 0 queries, other work 0.000 ms, 0 NavMesh samples.", PlanningSliceAnatomy.Text(3, 0, "", 0, 0, 0, 0));
    }

    [Fact]
    public void FoodContinuationTimeCoversOnlyTheFoodDispatchAndNotTheSurroundingAdvanceWork()
    {
        var (_, city) = Gr5FoodDecisionTests.FoodCity();
        while (!city.AdvanceTo(2 * 1440 + 11 * 60, 256).CaughtUp) { }
        long ticks = 0; int reads = 0;
        city.FoodClock = () => { reads++; return ticks += 7; };
        city.FoodClockFrequency = 1000;
        var maximum = new FoodContinuationMaximum();
        int food = 0;
        for (double minute = 2 * 1440 + 11 * 60; minute < 2 * 1440 + 12 * 60; minute += 1.0 / 30)
            for (int unit = 0; unit < 64; unit++)
            {
                ticks += 1_000_000;
                int before = reads;
                var result = city.AdvanceFrameSlice(minute, 1);
                ticks += 1_000_000;
                if (result.FoodContinuationEvents > 0)
                {
                    food++;
                    Assert.Equal(before + 2, reads);
                    Assert.Equal(7, city.LastFoodContinuationMilliseconds);
                }
                else Assert.Equal(before, reads);
                maximum.Observe(result.FoodContinuationEvents > 0, city.LastFoodContinuationMilliseconds, city.FoodContinuationStep);
                if (result.CaughtUp) break;
            }
        Assert.True(food > 0);
        Assert.Equal(7, maximum.Milliseconds);
        maximum.Observe(false, 1_000_000, "citizen");
        Assert.Equal(7, maximum.Milliseconds);
        maximum.Reset();
        Assert.Equal(0, maximum.Milliseconds); Assert.Equal("", maximum.Step);
    }

    [Fact]
    public void AFoodStepStartsWithoutTheNameOfThePreviousStep()
    {
        var (_, city) = Gr5FoodDecisionTests.FoodCity();
        while (!city.AdvanceTo(2 * 1440 + 11 * 60, 256).CaughtUp) { }
        bool sawCommit = false;
        for (double minute = 2 * 1440 + 11 * 60; minute < 2 * 1440 + 12 * 60 && !sawCommit; minute += 1.0 / 30)
            for (int unit = 0; unit < 64; unit++)
            {
                var result = city.AdvanceFrameSlice(minute, 1);
                if (result.FoodContinuationEvents > 0 && city.FoodContinuationStep == "decision-commit") sawCommit = true;
                if (result.CaughtUp) break;
            }
        Assert.True(sawCommit);
        var food = (FoodCommerce)typeof(CitySimulator).GetField("food", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(city)!;
        typeof(FoodCommerce).GetMethod("Step", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(food, null);
        Assert.NotEqual("decision-commit", city.FoodContinuationStep);
    }

    [Fact]
    public void EveryFoodContinuationUnitNamesTheFoodStepItRan()
    {
        var (_, city) = Gr5FoodDecisionTests.FoodCity();
        while (!city.AdvanceTo(2 * 1440 + 11 * 60, 256).CaughtUp) { }
        var known = new HashSet<string> { "awareness", "campaign", "rival-refresh", "decision-check", "decision-quote", "decision-quote-evaluate", "decision-evaluate", "decision-commit" };
        foreach (var stage in Enum.GetNames(typeof(FoodAwarenessCatalogue.BuildStage))) known.Add("awareness-" + stage.ToLowerInvariant());
        var seen = new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (double minute = 2 * 1440 + 11 * 60; minute < 2 * 1440 + 13 * 60; minute += 1.0 / 30)
            for (int unit = 0; unit < 64; unit++)
            {
                var result = city.AdvanceFrameSlice(minute, 1);
                if (result.FoodContinuationEvents > 0)
                {
                    string step = city.FoodContinuationStep;
                    Assert.Contains(step, known);
                    seen[step] = (seen.TryGetValue(step, out int n) ? n : 0) + 1;
                }
                if (result.CaughtUp) break;
            }
        output.WriteLine(string.Join(", ", seen.Select(p => p.Key + " " + p.Value)));
        Assert.True(seen.Keys.Count(k => k.StartsWith("decision-quote", StringComparison.Ordinal)) > 0);
        Assert.True(seen.TryGetValue("decision-commit", out int commits) && commits > 0);
    }
}
