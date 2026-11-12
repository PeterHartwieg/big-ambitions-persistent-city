using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PersistentCity.Compatibility;
using PersistentCity.Core;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class E6bVendorMeasure
{
    private readonly ITestOutputHelper output;
    public E6bVendorMeasure(ITestOutputHelper output) { this.output = output; StreetFoodHooks.Reset(); }

    private static (double p50, double p95, double max) Stats(List<double> values)
    {
        var s = values.OrderBy(v => v).ToList();
        return (s[s.Count / 2], s[Math.Min(s.Count - 1, (int)Math.Ceiling(s.Count * .95) - 1)], s[^1]);
    }
    private void Report(string name, List<double> ms, long bytes, int samples)
    {
        var (p50, p95, max) = Stats(ms);
        string allocation = bytes < 0 ? "allocation not measured" : (bytes / Math.Max(1, samples)) + " B per sample";
        output.WriteLine($"{name}: p50 {p50:0.0000} ms, p95 {p95:0.0000} ms, max {max:0.0000} ms, {allocation}, {samples} samples");
    }
    private static double Ms(long start) => Stopwatch.GetElapsedTime(start).TotalMilliseconds;

    [Fact]
    public void RevisionPollImageCopyStandPublishReferencesAndAwarenessRebuild()
    {
        if (string.IsNullOrEmpty(PerfGate.Variable("PC_E6B_MEASURE"))) return;
        var polls = new List<double>(1000); long pollBytes = 0;
        TimingBudget.Run(timing =>
        {
            StreetFoodHooks.Reset();
            for (int i = 0; i < 64; i++) StreetFoodHooks.Add("v" + i.ToString("D2"), x: i * 20);
            var pollRig = new StreetFoodVendorTests.Rig();
            pollRig.Run(2 * 1440 + 480);
            Assert.Equal(64, pollRig.Table.Live);
            long minute = 2 * 1440 + 481;
            for (int i = 0; i < 2000; i++) pollRig.Vendors.Poll(minute);
            polls = new List<double>(1000);
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) { var t = timing.Start(); for (int j = 0; j < 100; j++) pollRig.Vendors.Poll(minute); polls.Add(timing.Stop(t, "revision poll p95") / 100); }
            pollBytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
            Assert.Equal(0, pollBytes);
            timing.Under("revision poll p95", Stats(polls).p95, 0.05);
        });
        Report("revision poll, unchanged, 64 rows", polls, pollBytes, 100000);

        foreach (int count in new[] { 8, 64 })
        {
            var whole = new List<double>(); var units = new List<double>(); long allocated = 0;
            TimingBudget.Run(timing =>
            {
                StreetFoodHooks.Reset();
                for (int i = 0; i < count; i++) StreetFoodHooks.Add("v" + i.ToString("D2"), x: i * 20, items: new[] { FoodProducts.HotDog, FoodProducts.Burger, FoodProducts.Croissant });
                var bridge = StreetFoodBridge.Bind(typeof(FoodTrucks.ModApi.VendorApi));
                var reader = new VendorImageReader(bridge);
                whole = new List<double>(); units = new List<double>(); allocated = 0;
                for (int sample = 0; sample < 320; sample++)
                {
                    StreetFoodHooks.Revision++;
                    long before = GC.GetAllocatedBytesForCurrentThread(); var t = timing.Start();
                    reader.Poll();
                    var first = timing.End(t);
                    double total = first.Ms;
                    while (reader.Pending) { var u = timing.Start(); reader.Continue(); var unit = timing.End(u); total += unit.Ms; if (sample >= 20) units.Add(timing.Keep("image copy per unit p95", unit)); }
                    if (sample >= 20) { whole.Add(total); units.Add(timing.Keep("image copy per unit p95", first)); allocated += GC.GetAllocatedBytesForCurrentThread() - before; }
                    Assert.True(reader.Image.Ready, reader.Image.Reason);
                    Assert.Equal(count, reader.Image.Count);
                }
                timing.Under("image copy per unit p95", Stats(units).p95, 5);
            });
            Report($"image copy, {count} vehicles, whole copy", whole, allocated, 300);
            Report($"image copy, {count} vehicles, per unit (Read or 16 vehicles)", units, -1, units.Count);
        }

        StreetFoodVendorTests.Rig rig = null!;
        var applies = new List<double>(); var single = new List<double>(); long applyBytes = 0;
        TimingBudget.Run(timing =>
        {
            StreetFoodHooks.Reset();
            for (int i = 0; i < 64; i++) StreetFoodHooks.Add("v" + i.ToString("D2"), x: i * 20);
            rig = new StreetFoodVendorTests.Rig();
            long now = 2 * 1440 + 485; rig.Run(now);
            applies = new List<double>(); applyBytes = 0;
            for (int round = 0; round < 60; round++)
            {
                rig.RunTo(now + 60, 15); now = rig.Now;
                foreach (var v in StreetFoodHooks.Vehicles) { StreetFoodHooks.Close(v.Id); StreetFoodHooks.Open(v.Id, 40, x: v.X + 7); }
                long before = GC.GetAllocatedBytesForCurrentThread();
                rig.Vendors.Poll(now);
                var calls = new List<TimedSample>();
                while (true) { var t = timing.Start(); bool more = rig.Vendors.Continue(now); var call = timing.End(t); if (!more) break; calls.Add(call); }
                if (round >= 5) { applies.Add(timing.Keep("apply unit p95", calls[^1])); applyBytes += GC.GetAllocatedBytesForCurrentThread() - before; }
                Assert.All(rig.Table.Rows, r => Assert.True(rig.Vendors.CanServe(r.BusinessId, now), r.Reason));
            }
            timing.Under("apply unit p95", Stats(applies).p95, 5);
        });
        Report("apply unit, 64 vehicles all at a new stand (allocation: whole copy + apply)", applies, applyBytes, applies.Count);
        var row = rig.Table.Rows[0];
        for (int i = 0; i < 1020; i++)
        {
            row.Stand!.Position = new Position(i % 2, 0, 0);
            long t = Stopwatch.GetTimestamp();
            rig.City.PublishVendorPlace(row, null);
            if (i >= 20) single.Add(Ms(t));
        }
        Report("PublishVendorPlace, one stand", single, -1, single.Count);

        var food = rig.City.State.Food!;
        for (int i = 0; i < 20000; i++)
            food.Residents.Add(new ResidentMeal { ResidentId = "m" + i, AnchorId = "m" + i, Status = i % 3 == 0 ? MealStatus.Travelling : MealStatus.Paid,
                PlaceId = i < 64 ? rig.Table.Rows[i].PlaceId : "s0" });
        var references = new VendorReferences(); var scans = new List<double>();
        TimingBudget.Run(timing =>
        {
            scans = new List<double>();
            for (int i = 0; i < 220; i++) { var t = timing.Start(); rig.City.CollectVendorReferences(references); var scan = timing.End(t); if (i >= 20) scans.Add(timing.Keep("CollectVendorReferences p95", scan)); }
            timing.Under("CollectVendorReferences p95", Stats(scans).p95, 5);
        });
        Report("CollectVendorReferences, 20,000 meals", scans, -1, scans.Count);

        var (city, _, _) = WeightedFoodChoiceTests.Ready(3, shops: 1000);
        var table = new VendorTable();
        for (int i = 0; i < 64; i++)
        {
            var vendor = table.Admit("w" + i.ToString("D2"), "cart");
            vendor.Stand = new VendorStand { OpeningId = 1, Position = new Position(i * 15, 0, 5), OpenedDay = 2, OpenedMinute = 600, ClosesDay = 2, ClosesMinute = 700, Published = true };
            var menu = new VendorVehicle { ItemCount = 1 }; menu.Items[0] = FoodProducts.HotDog;
            city.PublishVendorPlace(vendor, menu);
        }
        foreach (int moves in new[] { 1, 64 })
        {
            var whole = new List<double>(); var units = new List<double>(); int unitCount = 0;
            TimingBudget.Run(timing =>
            {
                whole = new List<double>(); units = new List<double>();
                for (int sample = 0; sample < 60; sample++)
                {
                    for (int m = 0; m < moves; m++) { var moved = table.Rows[m]; moved.Stand!.Position = new Position(moved.Stand.Position.X + 1, 0, 5); city.PublishVendorPlace(moved, null); }
                    Assert.False(city.FoodAwarenessCurrent);
                    var builder = FoodAwarenessCatalogue.BeginBuild(city.State.Food!.Shops, city.FindPosition);
                    double total = 0; unitCount = 0;
                    using (builder)
                        while (true) { var t = timing.Start(); bool done = builder.Continue(); var unit = timing.End(t); total += unit.Ms; unitCount++; if (sample >= 10) units.Add(timing.Keep("awareness rebuild per unit p95", unit)); if (done) break; }
                    if (sample >= 10) whole.Add(total);
                    city.InspectFoodAwareness("r");
                }
                timing.Under("awareness rebuild per unit p95", Stats(units).p95, 5);
            });
            Report($"awareness rebuild after {moves} move(s), 1,064 scopes, {unitCount} units, whole", whole, -1, whole.Count);
            Report($"awareness rebuild after {moves} move(s), per unit", units, -1, units.Count);
        }

        StreetFoodHooks.Reset(); StreetFoodHooks.Add("A");
        rig = new StreetFoodVendorTests.Rig(); rig.Run(2 * 1440 + 480);
        var renewals = new List<double>(); long renewBytes = 0;
        for (int i = 1; i <= 220; i++)
        {
            long at = 2 * 1440 + 480 + i * 15;
            long before = GC.GetAllocatedBytesForCurrentThread(), t = Stopwatch.GetTimestamp();
            rig.Vendors.Poll(at);
            if (i > 20) { renewals.Add(Ms(t)); renewBytes += GC.GetAllocatedBytesForCurrentThread() - before; }
        }
        Report("poll with lease renewal", renewals, renewBytes, renewals.Count);
    }
}
