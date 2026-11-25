using System;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class VendorLunchFrameTests
{
    private readonly ITestOutputHelper output;
    public VendorLunchFrameTests(ITestOutputHelper output) { this.output = output; }

    private void Report(string label, VendorLunchReplay.Result r)
    {
        string? log = PerfGate.Variable("PC_VENDOR_LUNCH_LOG");
        static string F(double v) => v.ToString("F4", CultureInfo.InvariantCulture);
        string line = label + ": frames=" + r.Frames.Count + ", game min/s=" + F(r.GameMinutesPerRealSecond)
            + ", saturated=" + F(r.SaturatedShare) + ", prepare-return frames=" + r.Frames.Count(f => f.PrepareReturned)
            + ", total p95/max=" + F(r.P95(f => f.Total)) + "/" + F(r.Max(f => f.Total)) + " (after frame 0 max " + F(r.Frames.Skip(1).Max(f => f.Total)) + ")"
            + ", prepare p95/max=" + F(r.P95(f => f.Prepare)) + "/" + F(r.Max(f => f.Prepare))
            + ", stops p95/max=" + F(r.P95(f => f.Stops)) + "/" + F(r.Max(f => f.Stops))
            + ", advance mean/p95/max=" + F(r.Mean(f => f.Advance)) + "/" + F(r.P95(f => f.Advance)) + "/" + F(r.Max(f => f.Advance))
            + ", physical mean/p95/max=" + F(r.Mean(f => f.Physical)) + "/" + F(r.P95(f => f.Physical)) + "/" + F(r.Max(f => f.Physical)) + " at frame " + r.Frames.FindIndex(f => f.Physical == r.Max(g => g.Physical)) + ", after frame 0 p95/max " + F(VendorLunchReplay.Result.Rank(r.Frames.Skip(1).Select(f => f.Physical), .95)) + "/" + F(r.Frames.Skip(1).Max(f => f.Physical))
            + " (bodies max " + r.Frames.Max(f => f.Bodies) + "; per frame path queries mean/p95/max " + F(r.Mean(f => f.PathQueries)) + "/" + F(r.P95(f => f.PathQueries)) + "/" + r.Frames.Max(f => f.PathQueries)
            + ", per real second " + F(r.Frames.Sum(f => f.PathQueries) / Math.Max(1e-9, r.RealSeconds))
            + ", samples p95/max " + F(r.P95(f => f.Samples)) + "/" + r.Frames.Max(f => f.Samples) + ", acquisitions/s " + F(r.Frames.Sum(f => f.Acquisitions) / Math.Max(1e-9, r.RealSeconds)) + ")"
            + ", sync p95/max=" + F(r.P95(f => f.Sync)) + "/" + F(r.Max(f => f.Sync))
            + ", unit p95/max=" + F(r.P95(f => f.UnitMax)) + "/" + F(r.Max(f => f.UnitMax)) + " (" + r.Frames.OrderByDescending(f => f.UnitMax).First().UnitKind + ")"
            + ", citizen events/frame mean/max=" + F(r.Frames.Average(f => f.CitizenEvents)) + "/" + r.Frames.Max(f => f.CitizenEvents)
            + ", units/frame max=" + r.Frames.Max(f => f.Units)
            + ", frames>=30ms=" + r.Frames.Count(f => f.Total >= 30)
            + ", bytes/frame=" + (r.AllocatedBytes / Math.Max(1, r.Frames.Count))
            + " (prepare " + r.Frames.Sum(f => f.PrepareBytes) / Math.Max(1, r.Frames.Count) + ", stops " + r.Frames.Sum(f => f.StopsBytes) / Math.Max(1, r.Frames.Count)
            + ", advance " + r.Frames.Sum(f => f.AdvanceBytes) / Math.Max(1, r.Frames.Count) + ", physical " + r.Frames.Sum(f => f.PhysicalBytes) / Math.Max(1, r.Frames.Count) + ")" + ", gen0=" + r.Collections
            + ", route calls=" + r.RouteCalls + ", vendor meals=" + r.VendorMeals + ", vendor sales=" + r.VendorPurchases + ", purchases=" + r.Purchases + ", hash=" + r.SaveSha256[..12];
        output.WriteLine(line);
        if (!string.IsNullOrEmpty(log)) System.IO.File.AppendAllText(log, line + Environment.NewLine);
    }

    [Fact]
    public void MeasureLunchFrameCostAgainstFrameDeltaAndVehicles()
    {
        if (PerfGate.Variable("PC_VENDOR_LUNCH_MEASURE") != "1") return;
        string? only = PerfGate.Variable("PC_VENDOR_LUNCH_CASE"), progress = PerfGate.Variable("PC_VENDOR_LUNCH_LOG");
        if (!string.IsNullOrEmpty(progress)) VendorLunchReplay.Progress = text => System.IO.File.AppendAllText(progress, text + Environment.NewLine);
        bool bodies = PerfGate.Variable("PC_VENDOR_LUNCH_BODIES") == "1";
        double route = double.TryParse(PerfGate.Variable("PC_VENDOR_LUNCH_ROUTE_MS"), NumberStyles.Float, CultureInfo.InvariantCulture, out double r) ? r : 0;
        var arms = PerfGate.Variable("PC_VENDOR_LUNCH_ARMS") == "both" ? new[] { false, true } : new[] { true };
        foreach (int vehicles in new[] { 0, 2, 8 })
            foreach (double ms in new[] { 16.7, 33, 66, 120, 250 })
                foreach (bool forecast in arms)
                    if (only == null || only == vehicles.ToString(CultureInfo.InvariantCulture) + ":" + ms.ToString(CultureInfo.InvariantCulture))
                        Report((forecast ? "after " : "before") + " " + vehicles + " vehicles, " + ms.ToString("F1", CultureInfo.InvariantCulture) + " ms frames"
                            + (bodies ? ", bodies" : "") + (route > 0 ? ", route " + route.ToString(CultureInfo.InvariantCulture) + " ms" : ""),
                            VendorLunchReplay.Run(ms / 1000, vehicles: vehicles, bodies: bodies, routeMilliseconds: route, forecast: forecast));
    }

    [Fact]
    public void SeededLunchOutcomesMatchAcrossFrameDeltasForecastAndTheUninterruptedRun()
    {
        var reference = VendorLunchReplay.RunUninterrupted();
        Assert.True(reference.VendorPurchases > 20 && reference.VendorMeals > 20, "the fixture lost its vendor lunch: " + reference.VendorPurchases + " sales");
        foreach (var (seconds, forecast, route) in new[] { (1.0 / 30, true, 0.0), (0.25, true, 0.0), (0.25, false, 0.0), (0.12, true, 2.0) })
        {
            var run = VendorLunchReplay.Run(seconds, forecast: forecast, routeMilliseconds: route);
            Assert.Equal(reference.SaveSha256, run.SaveSha256);
            Assert.Equal(reference.VendorPurchases, run.VendorPurchases);
            Assert.Equal(reference.Purchases, run.Purchases);
            Assert.Equal(reference.NativeTo, run.NativeTo);
        }
    }

    [Fact]
    public void SeededDayOutcomesMatchTheUninterruptedRunAcrossVendorOpeningEveningAndClosing()
    {
        var reference = VendorLunchReplay.RunUninterrupted(to: VendorLunchReplay.DayTo);
        var lunch = VendorLunchReplay.RunUninterrupted();
        Assert.True(reference.VendorPurchases > lunch.VendorPurchases && reference.Purchases > lunch.Purchases, "the day fixture lost its demand outside lunch");
        foreach (var (seconds, resumable) in new[] { (0.25, false), (0.12, true) })
        {
            var run = VendorLunchReplay.Run(seconds, from: VendorLunchReplay.DayFrom, to: VendorLunchReplay.DayTo, resumable: resumable);
            Assert.True(run.Frames.Count > 1000);
            Assert.Equal(reference.SaveSha256, run.SaveSha256);
            Assert.Equal(reference.VendorPurchases, run.VendorPurchases);
            Assert.Equal(reference.Purchases, run.Purchases);
            Assert.Equal(reference.NativeTo, run.NativeTo);
        }
    }

    [Fact]
    public void APendingPlanStartsItsPathStepOnlyWhenTheStepFitsAndCarriesItOverOtherwise()
    {
        const double from = VendorLunchReplay.DayFrom, to = VendorLunchReplay.DayFrom + 20;
        var reference = VendorLunchReplay.RunUninterrupted(from, to);
        VendorLunchReplay.Result before = null!, after = null!;
        TimingBudget.Run(timing =>
        {
            before = VendorLunchReplay.Run(0.25, from, to, forecast: false, resumable: true, pathMilliseconds: 2);
            after = VendorLunchReplay.Run(0.25, from, to, resumable: true, pathMilliseconds: 2);
            static double Over(VendorLunchReplay.Step f) => double.IsNaN(f.Budget) ? 0 : f.Advance - f.Budget;
            output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "08:00 to 08:20, 250 ms frames, cheap quotes, resumable 2 ms path steps: late path starts {0}/{1} -> {2}/{3}, carried {4} -> {5}, advance p95/max {6:0.000}/{7:0.000} -> {8:0.000}/{9:0.000} ms, overrun p95 {10:0.000} -> {11:0.000} ms, game min/s {12:0.000} -> {13:0.000}",
                before.LatePathStarts, before.PathSteps, after.LatePathStarts, after.PathSteps, before.CarriedPaths, after.CarriedPaths,
                before.P95(f => f.Advance), before.Max(f => f.Advance), after.P95(f => f.Advance), after.Max(f => f.Advance),
                before.P95(Over), after.P95(Over), before.GameMinutesPerRealSecond, after.GameMinutesPerRealSecond));
            timing.Under("late path start share", after.LatePathStarts / (double)Math.Max(1, after.PathSteps), 0.05);
            timing.Under("resumable advance overrun p95", after.P95(Over), 0.25);
        });
        Assert.True(before.PathSteps > 20 && before.LatePathStarts > 0, "the fixture no longer starts path steps late without admission");
        Assert.True(after.CarriedPaths > 0, "no pending plan carried its path step into a later frame");
        Assert.Equal(reference.SaveSha256, before.SaveSha256);
        Assert.Equal(reference.SaveSha256, after.SaveSha256);
        Assert.Equal(reference.Purchases, after.Purchases);
    }

    [Fact]
    public void LongFrameDeltasKeepASaturatedAdvanceInsideItsBudgetWithoutExtraAllocation()
    {
        VendorLunchReplay.Result before = null!, after = null!;
        TimingBudget.Run(timing =>
        {
            before = VendorLunchReplay.Run(0.25, routeMilliseconds: 2, forecast: false);
            after = VendorLunchReplay.Run(0.25, routeMilliseconds: 2);
            static double Over(VendorLunchReplay.Step f) => double.IsNaN(f.Budget) ? 0 : f.Advance - f.Budget;
            output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "250 ms frames, 2 ms routes: advance p95/max {0:0.000}/{1:0.000} -> {2:0.000}/{3:0.000} ms, overrun p95 {4:0.000} -> {5:0.000} ms, saturated {6:0.00} -> {7:0.00}, game min/s {8:0.000} -> {9:0.000}, bytes {10} -> {11}",
                before.P95(f => f.Advance), before.Max(f => f.Advance), after.P95(f => f.Advance), after.Max(f => f.Advance),
                before.P95(Over), after.P95(Over), before.SaturatedShare, after.SaturatedShare,
                before.GameMinutesPerRealSecond, after.GameMinutesPerRealSecond, before.AllocatedBytes, after.AllocatedBytes));
            timing.Under("forecast advance overrun p95", after.P95(Over), 0.25);
            timing.Under("forecast advance max", after.Max(f => f.Advance), 3.5);
        });
        Assert.True(before.SaturatedShare > 0.2 && after.SaturatedShare > 0.2, "the fixture no longer saturates the advance");
        Assert.Equal(before.SaveSha256, after.SaveSha256);
        Assert.True(after.AllocatedBytes <= before.AllocatedBytes * 1.05 + 65536, "forecast frames allocated " + after.AllocatedBytes + " bytes against " + before.AllocatedBytes);
    }

    [Fact]
    public void VendorServiceLookupsAllocateNothingPerCall()
    {
        StreetFoodHooks.Reset();
        var f = new VehicleMarketTests.Fixture(2, 4);
        var shops = new VehicleMarketTests.ShopMarket(f.City.State.Food!, f.Receipts);
        var markets = new PersistentCity.Runtime.FoodMarkets(f.City.State.Food!, shops, f.Market, id => f.City.FindPlace(id).Position);
        string vehicle = f.City.State.Vendors.Find("v01")!.BusinessId;
        Assert.True(f.Market.CanServe(vehicle)); Assert.True(markets.CanServe(vehicle));
        bool served = true;
        long allocated;
        for (int i = 0; i < 100; i++) served &= f.Market.CanServe(vehicle) && markets.CanServe(vehicle);
        allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) served &= f.Market.CanServe(vehicle) && markets.CanServe(vehicle);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocated);
        Assert.True(served);
    }
}
