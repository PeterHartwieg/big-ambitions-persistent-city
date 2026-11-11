using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

// E5b4 opt-in (PC_E5B4_MEASURE_LEGACY=<file>): bytes and time of whole original-three meal decisions (8 shops, capture,
// quotes, choice, checkout) in the weighted single-resident city. Base-compatible: only APIs that exist before E5b4.
public sealed class OriginalThreeDecisionMeasure
{
    [Fact]
    public void MeasureOriginalThreeDecisions()
    {
        string? path = PerfGate.Variable("PC_E5B4_MEASURE_LEGACY");
        if (string.IsNullOrEmpty(path)) return;
        long Run(int seed, out double ms)
        {
            var (city, market, _) = WeightedFoodChoiceTests.Ready(seed, 8);
            foreach (var b in market.Shops) b.Offers.Add(new FoodOffer { ProductId = FoodProducts.Burger, Price = Money.FromCents(120), Units = 100 });
            foreach (var s in city.State.Food!.Shops) s.AdditionalProductIds.Add(FoodProducts.Burger);
            double at = city.State.Food.Residents.Single().DesiredAt;
            while (!city.AdvanceTo(at - 1, 64).CaughtUp) { }
            long before = GC.GetAllocatedBytesForCurrentThread(); long start = Stopwatch.GetTimestamp();
            while (!city.AdvanceTo(at + 60, 1).CaughtUp) { }
            ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        for (int i = 0; i < 20; i++) Run(i, out _);
        long bytes = 0; var times = new double[40];
        for (int i = 0; i < 40; i++) { bytes += Run(i, out times[i]); }
        Array.Sort(times);
        File.WriteAllText(path, $"40 decisions: {bytes / 40.0:F0} B/decision, p50 {times[20]:F3} ms, p95 {times[38]:F3} ms\n");
    }
}
