using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.EconomyInterventions;
using PersistentCity.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace PersistentCity.Tests;

// Opt-in offline F6h batch. One filtered foreground testhost; component rows run sequentially.
[Collection(StreetFoodHooks.Collection)]
public sealed class F6hMeasurements
{
    readonly ITestOutputHelper output;
    public F6hMeasurements(ITestOutputHelper output) { this.output = output; }
    static string? DirectoryPath => PerfGate.Variable("PC_F6H_MEASURE");
    void Write(object row)
    {
        string json = JsonConvert.SerializeObject(row);
        File.AppendAllText(Path.Combine(DirectoryPath!, "measurements.jsonl"), json + "\n"); output.WriteLine(json);
    }
    static object Stats(IReadOnlyList<double> samples, long bytes = -1)
    {
        var sorted = samples.OrderBy(v => v).ToArray();
        return new { n = sorted.Length, p50 = sorted[sorted.Length / 2], p95 = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1],
            max = sorted[^1], bytesPerOperation = bytes < 0 ? (double?)null : bytes / (double)sorted.Length,
            spikes30 = samples.Select((ms, index) => new { index, ms }).Where(s => s.ms >= 30).ToArray() };
    }
    object Measure(Action action, int n = 2000, int warm = 100)
    {
        for (int i = 0; i < warm; i++) action();
        var times = new double[n]; long bytes = 0;
        for (int i = 0; i < n; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp(); action();
            times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds; bytes += GC.GetAllocatedBytesForCurrentThread() - before;
        }
        return Stats(times, bytes);
    }
    [Fact]
    public void CadenceAndBodyEvidence()
    {
        if (DirectoryPath == null) return;
        Directory.CreateDirectory(DirectoryPath);
        var reference = EconomyRun.Run(new RunOptions { Integrated = true });
        foreach (int fps in new[] { 30, 60, 120 })
            foreach (int units in new[] { 2, 256 })
            {
                var run = EconomyRun.Run(new RunOptions { Integrated = true, FramesPerSecond = fps,
                    EventUnitsPerFrame = units, MillisecondsPerFrame = units == 2 ? .1 : 4 });
                Assert.Equal(reference.Json, run.Json); Assert.Equal(reference.FinalSaveSha256, run.FinalSaveSha256);
                Write(new { work = "cadence", fps, units, milliseconds = units == 2 ? .1 : 4, consumers = 1914, days = 42,
                    run.Frames, economicMetricsSha256 = EconomyRun.Sha256(run.Json), saveSha256 = run.FinalSaveSha256,
                    run.ConservationChecks, run.EmploymentUnits, run.DiscretionaryUnits, run.Purchases, run.Seconds });
            }
        // Same F1 integrated walking fixture, F2a's actual lease/lag driver, default 48 walkers/24 cars.
        // This fixture has no car routes; body-cap equality is never an F2 contract pass.
        var off = EconomyRun.Run(new RunOptions { Integrated = true, FramesPerSecond = 60, BodyCap = 0 });
        var bodies = EconomyRun.Run(new RunOptions { Integrated = true, FramesPerSecond = 60, BodyCap = 48 });
        Assert.Equal(reference.Json, off.Json); Assert.Equal(reference.FinalSaveSha256, off.FinalSaveSha256);
        Assert.InRange(bodies.MaximumBodies, 1, 48);
        Assert.True(bodies.BodyAcquisitions > 0); Assert.True(bodies.BodyObservations > 0);
        Write(new { work = "body-cap", status = "flagged evidence; no F2 divergence bound", consumers = 1914, days = 42,
            fps = 60, off = new { cap = 0, off.BodyAcquisitions, off.BodyObservations, off.MaximumBodies, off.EmploymentUnits, off.DiscretionaryUnits, off.Purchases,
                metricsSha = EconomyRun.Sha256(off.Json), saveSha = off.FinalSaveSha256 },
            @default = new { pedestrians = 48, vehicles = 24, bodies.BodyAcquisitions, bodies.BodyObservations, bodies.MaximumBodies,
                bodies.EmploymentUnits, bodies.DiscretionaryUnits, bodies.Purchases, metricsSha = EconomyRun.Sha256(bodies.Json), saveSha = bodies.FinalSaveSha256 },
            deltas = new { employmentUnits = bodies.EmploymentUnits - off.EmploymentUnits, discretionaryUnits = bodies.DiscretionaryUnits - off.DiscretionaryUnits,
                purchases = bodies.Purchases - off.Purchases, dailyRowsDifferent = bodies.Days.Zip(off.Days).Count(p => DayJson(p.First) != DayJson(p.Second)) } });
    }
    static string DayJson(DayMetrics day) { var json = new Json(); day.Write(json); return json.ToString(); }

    [Fact]
    public void IntegratedPostingAdvancementStress()
    {
        if (DirectoryPath == null) return;
        Directory.CreateDirectory(DirectoryPath);
        foreach (int consumers in new[] { 1914, 10000, 100000 })
        {
            var run = EconomyRun.Run(new RunOptions { Integrated = true, Residents = consumers / 2, Days = 7, MeasureSlices = true,
                Inputs = new[] { new InterventionInput(3 * 1440, InputKind.ProductPrice, EconomyFixture.Shop, InterventionArms.Product, 1000),
                    new InterventionInput(5 * 1440, InputKind.ProductPrice, EconomyFixture.Shop, InterventionArms.Product, 800) } });
            Assert.Equal(7L * consumers, run.ConservationChecks);
            Assert.True(run.EmploymentUnits > 0); Assert.True(run.DiscretionaryUnits > 0); Assert.True(run.Purchases > 0); Assert.True(run.JournalEntries > 0);
            Write(new { work = "integrated advancement", consumers, days = 7, citizenEventsPerSlice = 1,
                interval = Stats(run.SliceMilliseconds), run.ConservationChecks, run.EmploymentUnits, run.DiscretionaryUnits,
                run.Purchases, run.JournalEntries, run.Seconds, run.AllocatedBytes, metricsSha = EconomyRun.Sha256(run.Json) });
        }
    }

    [Fact]
    public void CurrentSectionSixComponents()
    {
        if (DirectoryPath == null) return;
        Directory.CreateDirectory(DirectoryPath);
        RivalRows(); PanelRows();
        WithEnvironment("PC_F6E_MEASURE", Path.Combine(DirectoryPath, "wages.jsonl"), () => new F6eMeasure(output).AccrualPostingAndSaveLatency());
        WithEnvironment("PC_F6F_MEASURE", Path.Combine(DirectoryPath, "ordinary.jsonl"), () => new F6fMeasure(output).AdmissionLookupPublicationAndRequestedLine());
        new ChangeJournalTests(new TeeOutput(output, Path.Combine(DirectoryPath, "journal.txt"))).MeasurementAndHiddenPanel();
        // Exact current writer at the complete E5d/F6d sizes; predecessor timing remains separately named.
        WithEnvironment("PC_F6D_MEASURE", Path.Combine(DirectoryPath, "saves.jsonl"), () => new F6dMeasure(output).MeasureAndCheckComposedSaveBudget());
    }
    static void WithEnvironment(string key, string value, Action action)
    {
        string? before = Environment.GetEnvironmentVariable(key);
        try { Environment.SetEnvironmentVariable(key, value); action(); }
        finally { Environment.SetEnvironmentVariable(key, before); }
    }
    sealed class TeeOutput : ITestOutputHelper
    {
        readonly ITestOutputHelper inner; readonly string path;
        internal TeeOutput(ITestOutputHelper inner, string path) { this.inner = inner; this.path = path; }
        public void WriteLine(string message) { inner.WriteLine(message); File.AppendAllText(path, message + "\n"); }
        public void WriteLine(string format, params object[] args) => WriteLine(string.Format(format, args));
    }
    void RivalRows()
    {
        foreach ((int consumers, int rivals) in new[] { (1914,64), (10000,1024), (100000,20000) })
        {
            var meals = Enumerable.Range(0, consumers).Select(i => new ResidentMeal { ResidentId = "m" + i, PlaceId = "r" + i % rivals,
                Status = i % 50 == 0 ? MealStatus.Serving : MealStatus.Pending, ServiceAt = 2141 + i % 5, External = i % 50 == 0 ? "10" : "",
                Today = new ResidentFoodToday { Day = 1, Meals = {
                    new MealOutcomeRecord { Status = MealOutcomeStatus.Bought, TargetKind = FoodAlternativeKind.Player, CheckoutAt = 1800 },
                    new MealOutcomeRecord { Status = i % 3 == 0 ? MealOutcomeStatus.Bought : MealOutcomeStatus.Skipped,
                        TargetKind = FoodAlternativeKind.Rival, PlaceId = "r" + i % rivals, CheckoutAt = 2120 },
                    new MealOutcomeRecord { Status = MealOutcomeStatus.Skipped, CheckoutAt = 0 } } } }).ToArray();
            Write(new { work = "rival reconstruction", consumers, rivals,
                interval = Measure(() => { var load = RivalServiceLoad.Reconstruct(meals, 2140); GC.KeepAlive(load); }, 10, 1) });
            for (int i = 0; i < 17; i++) { meals[i].PlaceId = "r0"; meals[i].External = "10"; meals[i].Status = MealStatus.Serving; meals[i].ServiceAt = 2141 + i; }
            var stateLoad = RivalServiceLoad.Reconstruct(meals, 2140);
            var sample = meals[16]; sample.External = "10"; sample.Status = MealStatus.Serving;
            // Existing F3-size roster retained around the measured O(lanes) operation.
            for (int lanes = 2; lanes <= 8; lanes += 6)
            {
                double sink = 0;
                var interval = Measure(() => { stateLoad.Leave(sample); sink += stateLoad.NextFree(sample.PlaceId, 2140, lanes);
                    sink += stateLoad.Count(sample.PlaceId, RivalServiceCalibration.Period(2145)); stateLoad.Admit(sample); }, 20000, 1000);
                var completion = Measure(() => { stateLoad.Paid(sample); stateLoad.Admit(sample); }, 20000, 1000);
                Assert.True(sink > 0); Write(new { work = "rival arrival/completion", consumers, rivals, lanes, arrival = interval, completion });
            }
        }
    }
    [Fact]
    public void RetainedRivalLoadEvidence()
    {
        if (DirectoryPath == null) return;
        Directory.CreateDirectory(DirectoryPath);
        _ = RetainedRivalSnapshot(); // Warm generic/static caches before the measured trials.
        var samples = new (long baselineBytes, long loadedBytes, long bytes)[3];
        for (int i = 0; i < samples.Length; i++) samples[i] = RetainedRivalSnapshot();
        foreach (var sample in samples) Assert.True(sample.bytes > 0, "retained-load delta must be positive");
        Write(new { work = "rival retained", rivals = 20000,
            method = "isolated non-inlined trials; same meal and empty-load roots; two full collections per snapshot",
            samples = samples.Select(s => new { s.baselineBytes, s.loadedBytes, s.bytes }).ToArray() });
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    static (long baselineBytes, long loadedBytes, long bytes) RetainedRivalSnapshot()
    {
        // This separate scope cannot retain a preceding benchmark's large fixture. Both
        // snapshots keep the identical source records and load wrapper alive; only the
        // wrapper's populated dictionaries/lists are added between them.
        var meals = Enumerable.Range(0, 40000).Select(i => new ResidentMeal { PlaceId = "retained" + i / 2,
            Status = MealStatus.Serving, ServiceAt = 2141 + i % 2, External = "10" }).ToArray();
        var load = new RivalServiceLoad();
        long baseline = CollectedHeapBytes();
        foreach (var meal in meals) load.Admit(meal);
        long loaded = CollectedHeapBytes();
        GC.KeepAlive(load); GC.KeepAlive(meals);
        return (baseline, loaded, loaded - baseline);
    }
    static long CollectedHeapBytes()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        return GC.GetTotalMemory(false);
    }
    void PanelRows()
    {
        foreach (int consumers in new[] { 1914, 10000, 100000 })
            foreach (int shops in new[] { 1, 64, 1024 })
            {
                var food = new FoodCommerceState(); var ledger = new EconomicLedger();
                for (int i = 0; i < consumers; i++) food.Residents.Add(new ResidentMeal { ResidentId = "p" + i,
                    Budget = OrdinaryExchangeTests.Wallet(100000) });
                var money = FoodMoneyCounters.Attach(food);
                var journal = new FoodChangeJournal(food, ledger);
                for (int i = 0; i < shops; i++)
                {
                    string id = "b" + i, place = "s" + i;
                    food.Shops.Add(new FoodScope { BusinessId = id, PlaceId = place, ProductId = FoodProducts.Croissant });
                    journal.ObserveSupply(id, place, 1440, new[] { new ShopObservedOffer { ProductId = FoodProducts.Croissant, PriceUnits = 1, Stock = 1 } }, 1, 2);
                    var observation = ledger.Shops[^1];
                    for (int j = 0; j < 8; j++) observation.Changes.Add(new ShopChange { Kind = ShopChangeKind.Price,
                        ProductId = FoodProducts.Croissant, Before = j + 1, After = j + 2, ObservedMinute = 14 * 1440 + j });
                    for (int day = 7; day <= 20; day++) food.History.Add(new FoodShopDay { Day = day, BusinessId = id, PlaceId = place,
                        Purchases = 2, Revenue = Money.FromCents(500), PeriodCauses = new MealPeriodCauses { Counts = Enumerable.Repeat(1,16).ToArray() } });
                }
                string Build()
                {
                    var text = new StringBuilder(money.Snapshot().Text()); var index = FoodFeedback.IndexHistory(food.History);
                    for (int i = 0; i < Math.Min(shops, FoodFeedbackV2.ShopsPerPage); i++)
                    {
                        var shop = food.Shops[i]; var rows = index[FoodFeedback.Key(shop.PlaceId, shop.BusinessId)];
                        FoodFeedback.Weeks(rows, shop.PlaceId, shop.BusinessId, 20, 1).AppendTo(text);
                        FoodFeedbackV2.AppendPeriods(text, rows, 20, 1); FoodFeedbackV2.AppendJournal(text, ledger.Shops[i], rows, 20, 1);
                    }
                    return text.ToString();
                }
                var interval = Measure(() => { _ = Build(); }, 100, 20);
                var gate = new FoodPanelRefresh(); Func<string> build = Build; long requests = money.Requests;
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int frame = 0; frame < 10000; frame++) gate.Update(false, frame / 60.0, build);
                long hiddenBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                Assert.Equal(0, hiddenBytes); Assert.Equal(0, gate.Builds); Assert.Equal(requests, money.Requests);
                Write(new { work = "paged panel", consumers, shops, interval, hiddenBytes, hiddenBuilds = gate.Builds });
            }
    }
}
