using System;
using System.IO;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class OptionalStopUnitSeamsTests
{
    private static OptionalStopUnitSeams Advance(params (string Kind, double UnitMs, double[] Seams)[] units)
    {
        var recorder = new OptionalStopUnitSeams();
        recorder.BeginCall();
        foreach (var unit in units)
        {
            foreach (double seam in unit.Seams) recorder.Seam(seam);
            recorder.EndUnit(unit.Kind, unit.UnitMs);
        }
        recorder.EndCall();
        return recorder;
    }

    [Fact]
    public void MultiUnitAdvanceRecordsEachUnitsOwnSeamTime()
    {
        var r = Advance(("citizen", 2.0, new[] { 0.25, 0.25 }), ("traffic", 12.0, new[] { 1.5 }), ("citizen", 3.0, new[] { 6.0 }),
            ("food-continuation", 0.5, Array.Empty<double>()), ("snapshot", 0.1, Array.Empty<double>()));
        Assert.Equal(5, r.Units);
        Assert.Equal(new[] { 0.5, 1.5, 6.0, 0.0, 0.0 }, Enumerable.Range(0, r.Units).Select(r.UnitSeamMilliseconds));
        Assert.Equal(new[] { "citizen", "traffic", "citizen", "food-continuation", "snapshot" }, Enumerable.Range(0, r.Units).Select(r.UnitKind));
        Assert.Equal(8.0, r.CallSeamMilliseconds, 9);
    }

    [Fact]
    public void SlowestUnitReportsItsOwnSeamNotTheCallTotal()
    {
        var r = Advance(("citizen", 1.0, new[] { 4.0 }), ("traffic", 9.0, new[] { 0.75 }), ("citizen", 2.0, new[] { 3.0 }));
        Assert.Equal(1, r.SlowestUnit);
        Assert.Equal("traffic", r.SlowestUnitKind);
        Assert.Equal(9.0, r.SlowestUnitMilliseconds);
        Assert.Equal(0.75, r.SlowestUnitSeamMilliseconds);
        Assert.Equal(7.75, r.CallSeamMilliseconds, 9);
        Assert.NotEqual(r.CallSeamMilliseconds, r.SlowestUnitSeamMilliseconds);
    }

    [Fact]
    public void MaximumP95AndOverThresholdListAreCorrect()
    {
        var units = Enumerable.Range(0, 40).Select(i => ("citizen", 1.0, new[] { i * 0.25 })).ToArray();
        var r = Advance(units);
        Assert.Equal(9.75, r.MaximumUnitSeamMilliseconds);
        Assert.Equal(9.25, r.P95UnitSeamMilliseconds());
        var over = r.OverThreshold();
        Assert.Equal(Enumerable.Range(21, 19), over.Select(u => u.Unit));
        Assert.All(over, u => Assert.True(u.Milliseconds > OptionalStopUnitSeams.ThresholdMilliseconds));
        Assert.Equal(19, r.UnitsOverThreshold());
        Assert.DoesNotContain(over, u => u.Milliseconds == 5.0);
        string text = r.OverThresholdText();
        Assert.StartsWith("citizen#21 5.250 ms; citizen#22 5.500 ms", text);
        Assert.EndsWith("; +11 more", text);
    }

    [Fact]
    public void SmallCallsUseNearestRankP95()
    {
        Assert.Equal(0, Advance().P95UnitSeamMilliseconds());
        Assert.Equal(2.0, Advance(("a", 1, new[] { 2.0 })).P95UnitSeamMilliseconds());
        Assert.Equal(3.0, Advance(("a", 1, new[] { 3.0 }), ("b", 1, new[] { 1.0 }), ("c", 1, new[] { 2.0 })).P95UnitSeamMilliseconds());
        Assert.Equal("", Advance(("a", 1, new[] { 5.0 })).OverThresholdText());
    }

    [Fact]
    public void SeamsOutsideAnAdvanceCallAreNotChargedToUnits()
    {
        var r = new OptionalStopUnitSeams();
        r.Seam(50);
        r.BeginCall();
        r.Seam(1); r.EndUnit("citizen", 1);
        r.EndCall();
        r.Seam(50);
        r.EndUnit("refresh", 99);
        Assert.Equal(1, r.Units);
        Assert.Equal(1, r.CallSeamMilliseconds);
        Assert.Equal(1, r.SlowestUnitSeamMilliseconds);
        r.BeginCall();
        Assert.Equal(0, r.Units);
        Assert.Equal(-1, r.SlowestUnit);
        Assert.Equal(0, r.SlowestUnitSeamMilliseconds);
    }

    [Fact]
    public void InterruptedCallIsClosedIncompleteAndStopsAccumulating()
    {
        var r = new OptionalStopUnitSeams();
        r.BeginCall();
        r.Seam(6); r.EndUnit("citizen", 7);
        r.Seam(3);
        r.EndCall(false);
        r.Seam(20);
        r.EndUnit("other", 30);
        Assert.False(r.Completed);
        Assert.False(r.CallOpen);
        Assert.Equal(9, r.CallSeamMilliseconds);
        Assert.Equal(1, r.Units);
        r.BeginCall();
        Assert.False(r.Completed);
        r.EndCall();
        Assert.True(r.Completed);
    }

    [Fact]
    public void MaximumKindAndNextUnitOverThresholdWalkTheCrossings()
    {
        var r = Advance(("citizen", 1, new[] { 5.5 }), ("traffic", 1, new[] { 1.0 }), ("food-continuation", 1, new[] { 8.0 }), ("snapshot", 1, new[] { 5.0 }));
        Assert.Equal("food-continuation", r.MaximumUnitSeamKind);
        Assert.Equal(0, r.NextUnitOverThreshold(0));
        Assert.Equal(2, r.NextUnitOverThreshold(1));
        Assert.Equal(-1, r.NextUnitOverThreshold(3));
    }

    [Fact]
    public void LongCallsGrowWithoutLosingUnits()
    {
        var r = Advance(Enumerable.Range(0, 300).Select(i => ("citizen", i == 250 ? 10.0 : 1.0, new[] { i == 250 ? 2.5 : 0.01 })).ToArray());
        Assert.Equal(300, r.Units);
        Assert.Equal(250, r.SlowestUnit);
        Assert.Equal(2.5, r.SlowestUnitSeamMilliseconds);
    }

    [Fact]
    public void AdvancePopulationFeedsPerUnitSeamsAndTheSummaryUsesTheUnitsOwnSeam()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "Runtime", "PersistentCityMod.cs"))) root = Path.GetDirectoryName(root)!;
        string mod = File.ReadAllText(Path.Combine(root, "Runtime", "PersistentCityMod.cs"));
        string environment = File.ReadAllText(Path.Combine(root, "Runtime", "OptionalStopEnvironment.cs"));
        Assert.Contains("UnitSeams.Seam(elapsed);", environment);
        Assert.Contains("unitSeams?.BeginCall();", mod);
        Assert.Contains("unitSeams?.EndUnit(kind, elapsed - unitStarted, cpuNow >= unitCpu && unitCpu >= 0 ? (cpuNow - unitCpu) / 1e6 : -1);", mod);
        Assert.Contains("long unitCpu = unitSeams != null ? ThreadCpuClock.Nanoseconds() : -1;", mod);
        Assert.Contains("slowestAdvanceStopMilliseconds = unitSeams?.SlowestUnitSeamMilliseconds ?? 0;", mod);
        Assert.DoesNotContain("stopsBefore", mod);
        Assert.Matches(@"try\s*\{\s*bool hiddenSkip", mod);
        Assert.Contains("completed = true;\n            }\n            finally { unitSeams?.EndCall(completed); }", mod.Replace("\r\n", "\n"));
        Assert.Contains("ObserveStopCrossings(unitSeams, elapsed);", mod);
        Assert.Contains("Optional-stop units over 5 ms this observation: ", mod);
        Assert.Contains("Optional-stop reads per unit in the slowest advance: ", mod);
    }
}
