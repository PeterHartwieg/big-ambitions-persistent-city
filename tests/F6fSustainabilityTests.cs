using System;
using System.IO;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.EconomyInterventions;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

public sealed class F6fSustainabilityTests
{
    private readonly ITestOutputHelper output;
    public F6fSustainabilityTests(ITestOutputHelper output) { this.output = output; }
    [Theory]
    [InlineData("control")]
    [InlineData("workplaces-closed-10pct")]
    [InlineData("worker-routes-blocked-5pct")]
    public void F6aArmsMeasureOrdinaryOutflowAndDailySustainability(string name)
    {
        var arm = name == "control" ? null : InterventionArms.All().Single(a => a.Name == name);
        var options = new RunOptions { Seed = 3, Residents = 8, Days = 42, OrdinaryVisits = true,
            Family = arm?.Family ?? "control", Arm = name, Inputs = arm?.Inputs ?? Array.Empty<InterventionInput>() };
        var run = EconomyRun.Run(options);
        Assert.Equal(42 * 8 * 2, run.ConservationChecks);
        Assert.All(run.Days, d => {
            Assert.InRange(d.ArrearsOwners, 0, d.Owners); Assert.True(d.UnfedDays >= 0);
            Assert.True(d.P10BalanceUnits >= 0); Assert.True(d.MedianBalanceUnits >= d.P10BalanceUnits);
            Assert.Equal(0, d.HistoricalClockMisses); Assert.Equal(0, d.DuplicateOrders);
        });
        Assert.True(run.Days.Last().OrdinaryDiscretionaryUnits > 0);
        Assert.True(run.Days.Last().OrdinaryWindowUnits > 0);
        Assert.Contains("ordinaryDiscretionaryUnits", run.Json);
        Assert.Contains("\"ownerKind\":\"person-wallet\",\"perPersonWallets\":true", run.Json);
        Assert.Contains("\"kindsMeasured\":[\"resident\",\"visitor\"]", run.Json);
        foreach (var week in run.Days.GroupBy(d => (d.Day - 1) / 7 + 1))
        {
            var last = week.Last();
            string row = $"{name} week {week.Key}: owners={last.Owners}, arrears={last.ArrearsOwners}, unfed={week.Sum(d => d.UnfedDays)}, "
                + $"p10Units={last.P10BalanceUnits}, medianUnits={last.MedianBalanceUnits}, ordinaryLifetimeUnits={last.OrdinaryDiscretionaryUnits}";
            output.WriteLine(row);
            if (PerfGate.Variable("PC_F6F_SUSTAINABILITY") is string path) File.AppendAllText(path, row + Environment.NewLine);
        }
    }
    [Fact]
    public void FullSizeFiveSeedArmsConserveAndReportSustainability()
    {
        if (PerfGate.Variable("PC_F6F_FULL_ARMS") != "1") return;
        foreach (int seed in Enumerable.Range(0, 5))
            foreach (string name in new[] { "control", "workplaces-closed-10pct", "worker-routes-blocked-5pct" })
            {
                var arm = name == "control" ? null : InterventionArms.All().Single(a => a.Name == name);
                var run = EconomyRun.Run(new RunOptions { Seed = seed, Residents = EconomyFixture.Residents, OrdinaryVisits = true,
                    Family = arm?.Family ?? "control", Arm = name, Inputs = arm?.Inputs ?? Array.Empty<InterventionInput>() });
                Assert.Equal(42L * EconomyFixture.Residents * EconomyFixture.RepresentationFactor, run.ConservationChecks);
                Assert.All(run.Days, d => { Assert.Equal(0, d.HistoricalClockMisses); Assert.Equal(0, d.DuplicateOrders); });
                Assert.True(run.Days.Last().OrdinaryDiscretionaryUnits > 0);
                var weeks = run.Days.GroupBy(d => (d.Day - 1) / 7 + 1).Select(w => w.Last()).ToArray();
                string row = $"seed={seed} arm={name} conservation={run.ConservationChecks} "
                    + $"weekP10Units={string.Join(',', weeks.Select(w => w.P10BalanceUnits))} "
                    + $"weekArrears={string.Join(',', weeks.Select(w => w.ArrearsOwners))} "
                    + $"unfed={run.Days.Sum(d => d.UnfedDays)} ordinaryUnits={run.Days.Last().OrdinaryDiscretionaryUnits}";
                output.WriteLine(row);
                if (PerfGate.Variable("PC_F6F_SUSTAINABILITY") is string path) File.AppendAllText(path, row + Environment.NewLine);
            }
    }

}
