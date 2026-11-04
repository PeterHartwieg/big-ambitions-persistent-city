using System;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.EconomyInterventions;
using Xunit;

namespace PersistentCity.Tests;

// F6a harness invariants on the small fixture: determinism (including an interrupted save/reload),
// epoch-boundary inputs, per-owner daily conservation and loud fault injections.
public sealed class EconomyInterventionTests
{
    static Arm ArmNamed(string name) => InterventionArms.All().Single(a => a.Name == name);
    static long Purchases(DayMetrics day, string business) => day.Shops.TryGetValue(business, out var row) ? row.Purchases : 0;

    [Fact]
    public void SameSeedGivesByteIdenticalMetricsAndOtherSeedsDiffer()
    {
        var a = EconomyRun.Run(SelfChecks.Small());
        var b = EconomyRun.Run(SelfChecks.Small());
        Assert.Equal(a.Json, b.Json); Assert.Equal(a.FinalSaveSha256, b.FinalSaveSha256);
        var other = SelfChecks.Small(); other.Seed = 4;
        Assert.NotEqual(a.Json, EconomyRun.Run(other).Json);
    }

    [Theory]
    [InlineData("control", SelfChecks.MidEpochReload)]
    [InlineData("lunch-counters-halved", SelfChecks.MidEpochReload)]
    [InlineData("lunch-counters-halved", SelfChecks.InputBoundaryReload)]
    [InlineData("door-closed-2-days", 16 * 1440 + 720.25)]
    public void SaveReloadMidRunKeepsMetricsAndSaveByteIdentical(string arm, double reload)
    {
        var chosen = arm == "control" ? null : ArmNamed(arm);
        var reference = EconomyRun.Run(SelfChecks.Small(chosen));
        var reloaded = EconomyRun.Run(SelfChecks.Small(chosen, reload));
        Assert.Equal(1, reloaded.Reloads);
        if (reload != Math.Floor(reload)) Assert.True(reloaded.InFlightAtReload > 0, "reload must interrupt a meal in flight");
        Assert.Equal(reference.Json, reloaded.Json);
        Assert.Equal(reference.FinalSaveSha256, reloaded.FinalSaveSha256);
    }

    [Fact]
    public void InputsArePublishedOnlyAtDrainedUnobservedEpochBoundaries()
    {
        Assert.Throws<InputViolation>(() => new InputSchedule(new[] {
            new InterventionInput(15 * 1440 + 0.5, InputKind.Door, EconomyFixture.Shop, "", 1) }, EconomyFixture.StartMinute, 60 * 1440));
        Assert.Throws<InputViolation>(() => new InputSchedule(new[] {
            new InterventionInput(EconomyFixture.StartMinute, InputKind.Door, EconomyFixture.Shop, "", 1) }, EconomyFixture.StartMinute, 60 * 1440));
        var market = EconomyFixture.Build(0, 4).Market;
        var arm = ArmNamed("zero-staff-day");
        var schedule = new InputSchedule(arm.Inputs, EconomyFixture.StartMinute, 60 * 1440);
        long first = (long)arm.Inputs[0].Minute;
        Assert.Throws<InputViolation>(() => schedule.PublishAt(first, false, first - 1, market));
        Assert.Throws<InputViolation>(() => schedule.PublishAt(first, true, first, market));
        Assert.Equal(EconomyFixture.MealsPerHour, market.Shops[0].MealsPerHour);
        Assert.Equal(0, schedule.PublishAt(first - 1, true, first - 2, market));
        Assert.Equal(1, schedule.PublishAt(first, true, AccountingClock.PreviousEpochTarget(first), market));
        Assert.Equal(0, market.Shops[0].MealsPerHour);
        // Every native read is fenced to the published epoch.
        market.Enter(first);
        Assert.NotNull(market.CaptureBusiness(EconomyFixture.Shop, first + 0.75));
        Assert.Throws<InputViolation>(() => market.CaptureBusiness(EconomyFixture.Shop, first + 1));
        Assert.Throws<InputViolation>(() => market.CaptureBusiness(EconomyFixture.Shop, first - 0.25));
        Assert.Throws<InputViolation>(() => market.Enter(first));
    }

    [Fact]
    public void EveryArmInputIsAWholeMinuteInsideItsInterventionWindowAndRevertsToBaseTerms()
    {
        foreach (var arm in InterventionArms.All())
        {
            Assert.NotEmpty(arm.Inputs);
            Assert.All(arm.Inputs, i => Assert.Equal(Math.Floor(i.Minute), i.Minute));
            Assert.All(arm.Inputs, i => Assert.InRange(i.Minute, InterventionArms.FirstInterventionDay * 1440.0, InterventionArms.FirstRevertedDay * 1440.0));
            var market = EconomyFixture.Build(0, 4).Market;
            string Terms() => string.Join("|", market.Shops.Select(s => string.Join(",", s.Prices) + s.MealsPerHour + s.OpenMinute + s.CloseMinute
                + string.Join(",", s.StockOverride) + s.DoorClosed + s.SiteOpen + s.RouteTimePercent))
                + string.Join(",", market.RivalShop.Prices) + market.RivalShop.EstimatedLanes + string.Join(",", market.ClosedWorkplaces) + market.WorkerRoutesBlockedPercent;
            string before = Terms();
            foreach (var input in arm.Inputs) market.Apply(input, (long)input.Minute);
            Assert.True(before == Terms(), arm.Name + " does not revert to base terms");
            Assert.Equal(InterventionArms.RivalLanesDefaultPercent, market.RivalShop.EstimatedLanes);
        }
    }

    [Fact]
    public void ProtocolMatchesSection35()
    {
        Assert.Equal(42, EconomyRun.Days); Assert.Equal(5, EconomyRun.Seeds);
        Assert.Equal((14, 14, 14), (EconomyRun.BaselineDays, EconomyRun.InterventionDays, EconomyRun.RevertedDays));
        var families = InterventionArms.All().GroupBy(a => a.Family).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(new[] { ("access", 3), ("employment", 2), ("hours", 2), ("price", 3), ("rival", 2), ("shortage", 2), ("staffing", 2) },
            families.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => (f.Key, f.Value)).ToArray());
        Assert.Equal(957, EconomyFixture.Residents); Assert.Equal(2, EconomyFixture.RepresentationFactor);
        Assert.Empty(InterventionArms.Control.Inputs);
    }

    [Fact]
    public void ConservationIdentityIsCheckedForEveryOwnerEveryDayAndAccountingIsExact()
    {
        var run = EconomyRun.Run(SelfChecks.Small(ArmNamed("whole-shop-day")));
        int owners = SelfChecks.SmallResidents * EconomyFixture.RepresentationFactor;
        Assert.Equal(EconomyRun.Days, run.Days.Count);
        Assert.Equal(Enumerable.Range(1, EconomyRun.Days), run.Days.Select(d => d.Day));
        Assert.All(run.Days, d => {
            Assert.Equal(owners, d.ConservationChecks); Assert.Equal(owners, d.Owners);
            Assert.Equal(0, d.DuplicateOrders); Assert.Equal(0, d.HistoricalClockMisses); Assert.Equal(0, d.EpochReadViolations);
            Assert.Equal(d.NativeRevenueUnits, d.ConfirmedPreparedUnits + d.UncertainConfirmedUnits);
            Assert.Equal(d.Resolved, d.OwnMeals + d.RivalMeals + d.HomeMeals + d.Skipped + d.Unconfirmed);
        });
        Assert.Equal((long)EconomyRun.Days * owners, run.ConservationChecks);
        Assert.Equal(6, run.InputsPublished);
        // The whole-shop stockout day sells nothing at the own shop; other days do.
        Assert.Equal(0, run.Days.Single(d => d.Day == 16).OwnPurchases);
        Assert.True(run.Days.Single(d => d.Day == 15).OwnPurchases > 0 && run.Days.Single(d => d.Day == 17).OwnPurchases > 0);
        Assert.True(run.Days.Single(d => d.Day == 16).Shops.Values.Sum(s => s.Stock) > 0);
    }

    [Theory]
    [InlineData(Fault.DuplicatedDebit, "watermarked debits")]
    [InlineData(Fault.UnbalancedDebit, "conservation identity failed")]
    [InlineData(Fault.DuplicateNativeOrder, "duplicate native receipt")]
    public void FaultInjectionsTurnTheLedgerCheckRed(Fault fault, string message)
    {
        var error = Assert.Throws<LedgerViolation>(() => EconomyRun.Run(SelfChecks.Small(fault: fault)));
        Assert.Contains(message, error.Message);
        Assert.Contains("day " + (EconomyRun.BaselineDays + 2), error.Message);
    }

    [Fact]
    public void MetricsSchemaReservesPerKindAndPerPersonWalletLinesButMeasuresTodaysPopulationOnly()
    {
        var run = EconomyRun.Run(SelfChecks.Small());
        Assert.StartsWith("{\"schema\":\"" + EconomyRun.Schema + "\"", run.Json);
        Assert.Contains("\"ownerKind\":\"consumer-budget\",\"perPersonWallets\":false", run.Json);
        Assert.Contains("\"kindsMeasured\":[\"resident\"],\"kindsReserved\":[\"commuter\",\"dayVisitor\"]", run.Json);
        Assert.Contains("\"kinds\":{\"resident\":{", run.Json);
        Assert.DoesNotContain("\"commuter\":{", run.Json);
        Assert.Contains("\"diagnosticOnly\":true", run.Json);
        Assert.True(EconomyRun.MarketShareDiagnosticOnly);
        Newtonsoft.Json.Linq.JObject.Parse(run.Json);
    }

    [Fact]
    public void SecondSiteWalksTwiceAsLongAsTheFirstAndAccessInputsScaleBoth()
    {
        var (state, market) = EconomyFixture.Build(0, 4);
        var routes = new EconomyRoutes(market);
        Place P(string id) => state.Places.Single(p => p.Id == id);
        double Minutes(string from, string to) => routes.Plan(P(from), P(to), 0, new TravelPreferences())!.Legs.Sum(l => l.Minutes);
        Assert.Equal(1, Minutes("home", "shop")); Assert.Equal(1, Minutes("shop", "home"));
        Assert.Equal(2 * Minutes("home", "shop"), Minutes("home", "shop-far"));
        Assert.Equal(2 * Minutes("shop", "home"), Minutes("shop-far", "home"));
        market.Shops.Single(s => s.BusinessId == EconomyFixture.FarShop).RouteTimePercent = 150;
        Assert.Equal(3, Minutes("home", "shop-far"));
    }

    [Fact]
    public void RunSimulatesExactlyTheRequestedDaysPlusTheFinalOutcomeSample()
    {
        var options = SelfChecks.Small(); options.Days = 7;
        var run = EconomyRun.Run(options);
        Assert.Equal(7 * 1440 + EconomyRun.OutcomeSampleMinute, run.Epochs);
        Assert.Equal(7, run.Days.Count);
    }

    [Fact]
    public void CumulativeWatermarkedOutflowMatchesTheLedgerAcrossTheFourteenDayRollover()
    {
        var run = EconomyRun.Run(SelfChecks.Small());
        Assert.All(run.Days, d => Assert.Equal(d.OutsideLedgerUnits, d.WatermarkedOutflowUnits));
        Assert.True(run.Days.Last().OutsideLedgerUnits > 0);
        // After rollover the rolling window is smaller than the lifetime total; before it they agree.
        Assert.Equal(run.Days.Single(d => d.Day == 10).OutsideLedgerUnits, run.Days.Single(d => d.Day == 10).WatermarkedOutflowWindowUnits);
        Assert.True(run.Days.Last().WatermarkedOutflowWindowUnits < run.Days.Last().WatermarkedOutflowUnits);
    }

    [Fact]
    public void CatchmentCountsOnlyMealsWhoseDecisionEvaluatedTheShopAndSurvivesReload()
    {
        var site = ArmNamed("second-site-2x");
        var run = EconomyRun.Run(SelfChecks.Small(site));
        Assert.All(run.Days, d => {
            foreach (var (business, c) in d.Catchments)
            {
                Assert.True(c.Resolved <= d.Resolved);
                Assert.Equal(c.Resolved, c.ThisShop + c.OtherOwn + c.Rival + c.Home + c.Skipped + c.Unconfirmed);
                Assert.True(c.ThisShop <= Purchases(d, business));
            }
            Assert.Equal(Purchases(d, EconomyFixture.Shop), d.Catchment(EconomyFixture.Shop, c => c.ThisShop));
            Assert.All(d.Offers.Values, o => Assert.True(o.Chosen <= o.Feasible && o.Feasible <= o.Considered));
        });
        // The dormant second site has no catchment until it opens, and one while it is open.
        Assert.All(run.Days.Where(d => d.Day < InterventionArms.FirstInterventionDay), d => Assert.False(d.Catchments.ContainsKey(EconomyFixture.FarShop)));
        Assert.Contains(run.Days, d => d.Catchment(EconomyFixture.FarShop, c => c.Resolved) > 0);
        Assert.Contains(run.Days, d => d.Catchment(EconomyFixture.Shop, c => c.Resolved) < d.Resolved);
        Assert.Equal(run.Json, EconomyRun.Run(SelfChecks.Small(site, SelfChecks.MidEpochReload)).Json);
    }
}
