using System;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.EconomyInterventions;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

// F6a employment arms only, six weeks and five seeds; the regression matrix is not invoked.
public sealed class F6eEmploymentArmsTests
{
    private const int ResidentCount = EconomyFixture.Residents;
    private readonly ITestOutputHelper output;
    public F6eEmploymentArmsTests(ITestOutputHelper output) { this.output = output; }
    [Fact]
    public void EmploymentInterventionsReduceEarnedIncomeAndConserveEveryOwnerEveryDay()
    {
        if (PerfGate.Variable("PC_F6E_ARMS") != "1") return;
        foreach (int seed in Enumerable.Range(0, 5))
        {
            var control = EconomyRun.Run(new RunOptions { Seed = seed, Residents = ResidentCount });
            foreach (var arm in InterventionArms.All().Where(a => a.Family == "employment"))
            {
                var run = EconomyRun.Run(new RunOptions { Seed = seed, Residents = ResidentCount, Arm = arm.Name, Family = arm.Family, Inputs = arm.Inputs });
                long Income(RunResult result) => result.Days.Where(d => d.Day >= 15 && d.Day < 29).Sum(d => d.IncomeReceivedUnits);
                Assert.True(Income(run) < Income(control), arm.Name + " must remove worked income");
                Assert.Equal(control.ConservationChecks, run.ConservationChecks);
                Assert.All(run.Days, d => { Assert.Equal(ResidentCount * EconomyFixture.RepresentationFactor, d.ConservationChecks); Assert.Equal(0, d.HistoricalClockMisses); Assert.Equal(0, d.DuplicateOrders); });
                output.WriteLine($"seed={seed} arm={arm.Name} interventionIncomeUnits={Income(run)} control={Income(control)} conservation={run.ConservationChecks}");
            }
        }
    }
}
