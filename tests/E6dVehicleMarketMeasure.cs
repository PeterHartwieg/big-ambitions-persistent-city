using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

[Collection(StreetFoodHooks.Collection)]
public sealed class E6dVehicleMarketMeasure
{
    private readonly ITestOutputHelper output;
    public E6dVehicleMarketMeasure(ITestOutputHelper output) { this.output = output; }
    private static double Ms(long started) => (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
    private void Report(string name, List<double> samples, long bytes)
    {
        samples.Sort();
        output.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "{0}: n={1}, p50={2:F6} ms, p95={3:F6} ms, max={4:F6} ms, bytes/op={5:F1}, spikes>=30ms={6}",
            name, samples.Count, samples[samples.Count / 2], samples[(int)Math.Ceiling(samples.Count * .95) - 1], samples[^1], bytes / (double)samples.Count, samples.Count(t => t >= 30)));
        foreach (double spike in samples.Where(t => t >= 30)) output.WriteLine($"FLAG {name}: {spike:F6} ms");
    }
    private void Measure(string name, Action action, int count = 300)
    {
        var times = new List<double>(count); long bytes = 0; string check = name + " offline p95";
        TimingBudget.Run(timing =>
        {
            for (int i = 0; i < 30; i++) action();
            times = new List<double>(count); bytes = 0;
            for (int i = 0; i < count; i++)
            {
                long allocation = GC.GetAllocatedBytesForCurrentThread(); var start = timing.Start();
                action();
                double elapsed = timing.Stop(start, check); bytes += GC.GetAllocatedBytesForCurrentThread() - allocation;
                times.Add(elapsed);
            }
            times.Sort();
            timing.Under(check, times[(int)Math.Ceiling(count * .95) - 1], 5);
        });
        Report(name, times, bytes);
    }
    [Fact]
    public void SnapshotReadCaptureCheckoutAnd64VehicleLunchRush()
    {
        if (PerfGate.Variable("PC_E6D_MEASURE") != "1") return;
        StreetFoodHooks.Reset();
        var f = new VehicleMarketTests.Fixture(64, 8);
        foreach (var v in StreetFoodHooks.Vehicles) { v.Units[0] = v.Units[1] = 1000000; }
        StreetFoodHooks.Revision++; f.Run(f.Now + 1);
        Measure("Read snapshot, 64 vehicles", () => Assert.Equal(64, f.Market.Read(f.Now).Count));
        Measure("capture batch, 8 vehicles", () => {
            using (f.Market.BeginCapture(f.Now))
                for (int i = 0; i < 8; i++) Assert.NotNull(f.Market.CaptureBusiness(f.City.State.Vendors.Rows[i].BusinessId, f.Now));
        });
        var origin = new Place { Position = new Position(410, 0, 0) };
        Measure("baseline materialize 64 then rank 8", () => {
            var selected = f.Market.Read(f.Now).OrderBy(b => Position.Distance(origin.Position, f.City.FindPlace(b.PlaceId).Position))
                .ThenBy(b => b.Id, StringComparer.Ordinal).Take(8).ToArray(); Assert.Equal(8, selected.Length);
        });
        Measure("current rank then materialize 8", () => Assert.Equal(8, f.Market.ReadLocal(origin, f.Now, 1000, 8).Count));
        var meal = f.Meal("v63");
        Measure("checkout, vehicle 64 of 64", () => { f.Receipts.Clear(); Assert.True(f.Market.Buy(meal, f.Now).Paid); });
        var windows = new List<double>(160); var units = new List<double>(1280); long allBytes = 0, unitBytes = 0;
        TimingBudget.Run(timing =>
        {
            StreetFoodHooks.Reset();
            var lunch = new VehicleMarketTests.Fixture(64, 8);
            foreach (var v in StreetFoodHooks.Vehicles) { v.Units[0] = v.Units[1] = 1000000; }
            StreetFoodHooks.Revision++; lunch.Run(lunch.Now + 1);
            var lunchShops = new VehicleMarketTests.ShopMarket(lunch.City.State.Food!, lunch.Receipts);
            var lunchMarkets = new FoodMarkets(lunch.City.State.Food!, lunchShops, lunch.Market, id => lunch.City.FindPlace(id).Position);
            var meals = Enumerable.Range(0, 64).Select(i => lunch.Meal("v" + i.ToString("D2"), i)).ToArray();
            windows = new List<double>(160); units = new List<double>(1280); allBytes = 0; unitBytes = 0;
            for (int round = 0; round < 180; round++)
            {
                lunch.Now++; StreetFoodHooks.Now = lunch.Now; StreetFoodHooks.Expire();
                lunch.Receipts.Clear(); long allocation = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
                for (int group = 0; group < 8; group++)
                {
                    long b = GC.GetAllocatedBytesForCurrentThread(); var unit = timing.Start();
                    while (lunch.Market.Prepare(lunch.Now)) { }
                    lunchMarkets.ReadLocal(origin, lunch.Now, 1000, 8);
                    for (int i = group * 8; i < group * 8 + 8; i++) Assert.True(lunchMarkets.Buy(meals[i], lunch.Now).Paid);
                    var sample = timing.End(unit);
                    if (round >= 20) { units.Add(timing.Keep("combined market work p95", sample)); unitBytes += GC.GetAllocatedBytesForCurrentThread() - b; }
                }
                double total = Ms(start);
                if (round >= 20) { windows.Add(total); allBytes += GC.GetAllocatedBytesForCurrentThread() - allocation; }
            }
            var sorted = units.OrderBy(u => u).ToList();
            timing.Under("combined market work p95", sorted[(int)Math.Ceiling(sorted.Count * .95) - 1], 5);
        });
        Report("combined market work: preparation + merged capture + 8 checkouts, 64-vehicle lunch", units, unitBytes);
        Report("whole 64-sale lunch burst (8 bounded groups)", windows, allBytes);
        output.WriteLine("Whole-frame cost: unmeasured offline; Street Food DTO/ledger stand-in included, native scene/routing/rendering excluded.");
        output.WriteLine("Parked hotspots unchanged. Autosave absent in this headless process; no save occurs in timing windows.");
    }
}
