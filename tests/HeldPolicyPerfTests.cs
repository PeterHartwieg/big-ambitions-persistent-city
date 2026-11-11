using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using PersistentCity.Core;
using PersistentCity.Persistence;
using PersistentCity.Runtime;
using Xunit;

namespace PersistentCity.Tests;

// Opt-in offline measurements for docs/perf/e5b4.md: set PC_E5B4_MEASURE to an output file. Choice weight cost with 0, 8
// and 64 dynamic offers against legacy offers, the policy read on a clean frame, held metadata bytes at 64 tuples, and
// fresh-city save bytes and read/write latency at writer 36 against the frozen writer 35.
public sealed class HeldPolicyPerfTests
{
    private const int Offers = 64;

    private static EvaluatedFoodOffer[] Shortlist(int dynamic)
    {
        var originals = new[] { FoodProducts.Croissant, FoodProducts.Burger, FoodProducts.HotDog };
        var offers = new EvaluatedFoodOffer[Offers];
        for (int i = 0; i < Offers; i++)
        {
            bool isDynamic = i < dynamic;
            string product = isDynamic ? "mod:itemname_dish" + i : originals[i % 3];
            FoodProductMetadata? metadata = isDynamic ? new FoodProductMetadata(MealClass.Food, i % 5, 900, 100, 450) : null;
            offers[i] = new EvaluatedFoodOffer("b" + (i / 8), product, Money.FromCents(900 + 10 * i), 4 + i % 7, i % 3, 20, 60,
                60, 50, MealKind.Lunch, newTasteBand: isDynamic ? i % 5 : -1, metadata: metadata);
        }
        return offers;
    }

    private static (double P50, double P95, double Bytes) Time(Action run, int samples = 4000)
    {
        for (int i = 0; i < 200; i++) run();
        var times = new double[samples];
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < samples; i++)
        {
            long start = Stopwatch.GetTimestamp(); run();
            times[i] = (Stopwatch.GetTimestamp() - start) * 1e6 / Stopwatch.Frequency;
        }
        double bytes = (GC.GetAllocatedBytesForCurrentThread() - allocated) / (double)samples;
        Array.Sort(times);
        return (times[samples / 2], times[(int)(samples * 0.95)], bytes);
    }

    [Fact]
    public void MeasureHeldPolicyCosts()
    {
        string? path = PerfGate.Variable("PC_E5B4_MEASURE");
        if (string.IsNullOrEmpty(path)) return;
        var report = new StringBuilder();
        var traits = new ResidentTraits { Bands = "2222222222" };
        var settings = new FoodChoiceSettings();
        var draw = new FoodDrawContext(957, "pc:1", 3, MealKind.Lunch, "ctx", 1);

        // Choice weight: Select over a 64-offer shortlist (8 shops x 8 offers) with 0, 8 or 64 dynamic offers.
        // Two passes in the order 0, 8, 64: the first lets tiered JIT promote the code, the second is reported.
        report.AppendLine("select over 64 offers (second pass): dynamic | p50 us | p95 us | bytes/call");
        for (int pass = 0; pass < 2; pass++)
            foreach (int dynamic in new[] { 0, 8, 64 })
            {
                var offers = Shortlist(dynamic);
                var r = Time(() => FoodChoicePolicy.Select(offers, traits, Money.FromCents(100000), draw, settings, true));
                if (pass == 1) report.AppendLine($"{dynamic} | {r.P50:F2} | {r.P95:F2} | {r.Bytes:F1}");
            }
        report.AppendLine("single weight: kind | p50 ns | bytes/call");
        var legacy = Shortlist(0)[1]; var held = Shortlist(1)[0];
        var lw = Time(() => FoodChoicePolicy.Weight(legacy, traits, Money.Zero, settings), 20000);
        var dw = Time(() => FoodChoicePolicy.Weight(held, traits, Money.Zero, settings, held.Metadata!.Value), 20000);
        report.AppendLine($"legacy | {lw.P50 * 1000:F0} | {lw.Bytes:F1}");
        report.AppendLine($"held revision 2 | {dw.P50 * 1000:F0} | {dw.Bytes:F1}");

        // The market's policy read on a clean context (no input changed) and one rebuild after a config change.
        var image = new LoadedNameImage(1, Enumerable.Range(0, 2000).Select(i => "mod:itemname_dish" + i).ToArray(), FoodEligibilityRefusal.None);
        var config = FoodProductMetadataResolver.Empty; var snapshot = FoodProductMetadataSnapshot.Empty;
        var source = new FoodProductPolicySource(() => image, () => config, _ => snapshot);
        _ = source.Current;
        var clean = Time(() => _ = source.Current, 20000);
        report.AppendLine($"policy read, clean: p50 {clean.P50 * 1000:F0} ns, p95 {clean.P95 * 1000:F0} ns, {clean.Bytes:F1} B/read");
        var policy = source.Current;
        var exec = Time(() => policy.Executable("mod:itemname_dish7"), 20000);
        report.AppendLine($"Executable (dynamic name): p50 {exec.P50 * 1000:F0} ns, {exec.Bytes:F1} B/call");
        var capture = Time(() => policy.Capture("mod:itemname_dish7", traits, 957, "pc:1"), 20000);
        report.AppendLine($"Capture (dynamic name): p50 {capture.P50 * 1000:F0} ns, {capture.Bytes:F1} B/call");
        var start = Stopwatch.GetTimestamp(); long before = GC.GetAllocatedBytesForCurrentThread();
        image = new LoadedNameImage(2, Enumerable.Range(0, 2000).Select(i => "mod:itemname_dish" + i).ToArray(), FoodEligibilityRefusal.None);
        long imageBytes = GC.GetAllocatedBytesForCurrentThread() - before; before = GC.GetAllocatedBytesForCurrentThread();
        start = Stopwatch.GetTimestamp(); _ = source.Current;
        report.AppendLine($"policy rebuild after a new 2,000-name image: {(Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency:F3} ms, {GC.GetAllocatedBytesForCurrentThread() - before} B (image itself {imageBytes} B)");

        // Held metadata bytes at 64 revision-2 tuples (8 shops x 8 offers), against the 8 Ki bound.
        var city = ChangeJournalTests.Fixture(5150);
        var meal = city.Food!.Residents.First(m => m.Decision == null);
        var shopsUsed = city.Food.Shops.Take(1).ToList();
        var decision = new FoodDecisionState { IntentId = "perf", ContextKey = "perf", Attempt = 1, OriginAt = city.Minute, Deadline = city.Minute + 30,
            Tuning = FoodDecisionTuning.Capture(new FoodChoiceSettings()) };
        for (int s = 0; s < 8; s++)
        {
            var shop = new FoodDecisionShop { BusinessId = shopsUsed[0].BusinessId, PlaceId = shopsUsed[0].PlaceId, MealsPerHour = 60 };
            for (int o = 0; o < 8; o++)
                shop.Offers.Add(new FoodDecisionOffer { ProductId = "mod:itemname_dish" + (s * 8 + o), Price = Money.FromCents(900), Units = 5,
                    Metadata = new FoodOfferMetadata(2, FoodMealClass.Food, o % 5, 5000, 5000, 5000) });
            decision.Shops.Add(shop);
        }
        string metadataText = string.Join(",", decision.Shops.SelectMany(s => s.Offers).Select(o => Newtonsoft.Json.JsonConvert.SerializeObject(o.Metadata)));
        report.AppendLine($"held metadata, 64 revision-2 tuples at the widest fits: {Encoding.UTF8.GetByteCount(metadataText)} B as JSON objects; validator budget 2 + 64 x 36 = {2 + 64 * 36} B; bound {FoodNameIdentity.MaximumHeldWire} B");

        // Fresh-city save bytes and read/write latency.
        foreach (int seed in new[] { 5150, 9217 })
        {
            var state = ChangeJournalTests.Fixture(seed);
            string payload = SaveCodec.Write(state);
            var write = Time(() => SaveCodec.Write(state), 60);
            var read = Time(() => SaveCodec.Read(payload), 60);
            report.AppendLine($"seed {seed}: bytes {Encoding.UTF8.GetByteCount(payload)}; write p50 {write.P50 / 1000:F2} ms; read p50 {read.P50 / 1000:F2} ms");
        }
        File.WriteAllText(path, report.ToString());
    }
}
