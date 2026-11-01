using System.Diagnostics;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;
using PersistentCity.Core;
using PersistentCity.Persistence;

// Foreground-only microbenchmarks, actual Core/Persistence, no native or Unity inputs.
static class Program
{
    static int assertions;
    static object? sink;
    static void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
    static ResidentMeal Meal() => new() { Status = MealStatus.Serving, External = "10AAAAAAAAAAAAAAAAAAA",
        Price = Money.FromNative(1.2345f), Allowance = Money.FromCents(1000000) };
    static void Prepare(ResidentMeal meal)
    { meal.Status = MealStatus.Serving; meal.External = "10AAAAAAAAAAAAAAAAAAA"; meal.DayAllowanceUsed = Money.Zero; }
    static RivalOutflowHistory Full(out FoodCommerceState food)
    {
        food = new FoodCommerceState(); var meal = Meal(); food.Residents.Add(meal);
        var history = RivalOutflowHistory.Begin(food, 0);
        for (int day = 0; day < 365; day++)
        {
            Prepare(meal); Check(ExternalFoodSettlement.TryPay(meal, history, day * 1440), "settlement");
            Check(history.Reconciles(food) && history.RecordCount <= 15, "bounded ledger");
        }
        return history;
    }
    // Exact initial D3a projection algorithm, retained as the measured uncached control.
    static RivalOutflowView Fresh(RivalOutflowHistory history, int day)
    {
        var days = new RivalOutflowDay[14]; BigInteger total = 0, count = 0; int at = 0;
        for (int i = 0; i < 14; i++)
        {
            int date = day - 13 + i;
            while (at < history.Rows.Count && history.Rows[at].Day < date) at++;
            var row = at < history.Rows.Count && history.Rows[at].Day == date ? history.Rows[at] : null;
            bool known = date >= history.StartDay && date >= 0;
            days[i] = new RivalOutflowDay(date, known, known && date == history.StartDay && history.PartialStart,
                row?.Units ?? BigInteger.Zero, row?.Count ?? BigInteger.Zero);
            total += days[i].Units; count += days[i].Count;
        }
        return new RivalOutflowView(days, total, count);
    }
    static object Measure(Action action, int samples = 2000)
    {
        for (int i = 0; i < 100; i++) action();
        var values = new double[samples];
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < samples; i++)
        { long start = Stopwatch.GetTimestamp(); action(); values[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds; }
        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        Array.Sort(values);
        return new { p50 = values[samples / 2], p95 = values[(int)(samples * .95) - 1], max = values[^1],
            spikesAtLeast30ms = values.Count(v => v >= 30), bytesPerSample = bytes / (double)samples, samples };
    }
    static void Main()
    {
        var history = Full(out var food);
        string wire = history.ToWire();
        Check(history.Capture(364).Count == 14, "last fourteen");
        Check(history.Reconciles(food), "full ledger reconciliation");
        Check(Fresh(history, 364).Text() == history.Capture(364).Text(), "cache equals uncached control");
        var closed = Measure(() => { }); // No new closed-panel participant, call, scan or allocation.
        var before = Measure(() => sink = Fresh(history, 364).Text());
        var open = Measure(() => sink = history.Capture(364).Text());
        var meal = food.Residents[0];
        var burst = Measure(() =>
        {
            for (int i = 0; i < 128; i++)
            {
                Prepare(meal); Check(ExternalFoodSettlement.TryPay(meal, history, 364 * 1440 + 10), "late burst");
                Check(!ExternalFoodSettlement.TryPay(meal, history, 364 * 1440 + 10), "repeat burst");
            }
        }, 1000);
        Check(history.Reconciles(food) && history.RecordCount == 15, "burst reconciliation");
        var rush = Measure(() =>
        {
            for (int i = 0; i < 128; i++) { Prepare(meal); ExternalFoodSettlement.TryPay(meal, history, 364 * 1440 + 60); }
            sink = history.Capture(364).Text();
        }, 1000);
        var retention = new RivalOutflowHistory[2000];
        long heapBefore = GC.GetTotalMemory(true);
        for (int i = 0; i < retention.Length; i++) retention[i] = RivalOutflowHistory.FromWire(wire);
        long retainedClosed = GC.GetTotalMemory(true) - heapBefore;
        for (int i = 0; i < retention.Length; i++) sink = retention[i].Capture(364).Text();
        long retainedOpen = GC.GetTotalMemory(true) - heapBefore;
        GC.KeepAlive(retention);
        var replay = RivalOutflowHistory.FromWire(history.ToWire());
        Check(replay.Reconciles(food), "replay ledger");
        Console.WriteLine(JsonConvert.SerializeObject(new { assertions, closed, uncachedOpenControl = before,
            warmOpen = open, lateSettlement128Burst = burst, rush128PlusOpen = rush,
            savedHistoryCharacters = wire.Length, savedPropertyUtf8Bytes = Encoding.UTF8.GetByteCount("\"Outflow\":\"" + wire + "\","),
            maxHistoryCharacters = 1024, maxChargedRecords = 15,
            retainedClosedBytesPerHistory = retainedClosed / (double)retention.Length,
            retainedOpenBytesPerHistory = retainedOpen / (double)retention.Length }, Formatting.Indented));
    }
}
