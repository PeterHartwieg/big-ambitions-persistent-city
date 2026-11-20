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

    private sealed class ClockedPlanner : IRoutePlanner, ICitizenRoutePlanner, ITrafficRoutePlanner
    {
        private readonly RushSpikeReplay.Planner inner = new RushSpikeReplay.Planner();
        public long Ticks, Plans;
        public bool Armed;
        public TrafficNetwork? TrafficNetwork { get { if (Armed) Ticks += 1_000_000; return inner.TrafficNetwork; } }
        public void SetTrafficForecast(Func<int, double, TrafficLinkForecast>? forecast) => inner.SetTrafficForecast(forecast);
        public bool IsTrafficLinkClosed(int linkId) => inner.IsTrafficLinkClosed(linkId);
        public Route? Plan(Place from, Place to, double minute, TravelPreferences preferences) { Tick(); return inner.Plan(from, to, minute, preferences); }
        public Route? Plan(Place from, Place to, double minute, CitizenState citizen) { Tick(); return inner.Plan(from, to, minute, citizen); }
        private void Tick() { if (Armed) { Ticks += 5; Plans++; } }
    }

    [Fact]
    public void FoodContinuationTimeCoversOnlyTheFoodDispatchAndNotTheSurroundingAdvanceWork()
    {
        var state = RushSpikeReplay.Create();
        var planner = new ClockedPlanner();
        var city = new CitySimulator(state, planner);
        city.AttachFoodMarket(new Gr5FoodDecisionTests.CachedMarket()); city.EnableHouseholdFinances();
        city.AttachOptionalStops(new RushSpikeReplay.Stops(), OptionalStopPolicy.ApprovedChoicePercentage);
        while (!city.AdvanceTo(2 * 1440 + 11 * 60, 256).CaughtUp) { }
        city.FoodClock = () => planner.Ticks;
        city.FoodClockFrequency = 1000;
        planner.Armed = true;
        var maximum = new FoodContinuationMaximum();
        int food = 0, quoted = 0;
        for (double minute = 2 * 1440 + 11 * 60; minute < 2 * 1440 + 12 * 60; minute += 1.0 / 30)
            for (int unit = 0; unit < 64; unit++)
            {
                long plansBefore = planner.Plans, ticksBefore = planner.Ticks;
                var result = city.AdvanceFrameSlice(minute, 1);
                if (result.FoodContinuationEvents > 0)
                {
                    food++;
                    long plans = planner.Plans - plansBefore;
                    if (plans > 0) quoted++;
                    Assert.True(planner.Ticks - ticksBefore >= 1_000_000, "the slice ran no traffic network read");
                    Assert.Equal(5.0 * plans, city.LastFoodContinuationMilliseconds);
                }
                maximum.Observe(result.FoodContinuationEvents > 0, city.LastFoodContinuationMilliseconds, city.FoodContinuationStep);
                if (result.CaughtUp) break;
            }
        Assert.True(food > 0); Assert.True(quoted > 0);
        Assert.InRange(maximum.Milliseconds, 5, 1000);
        maximum.Observe(false, 1_000_000, "citizen");
        Assert.InRange(maximum.Milliseconds, 5, 1000);
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
