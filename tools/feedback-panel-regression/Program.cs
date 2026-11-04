// City-audit F6c offline measurement and invariant check for the Ctrl+F8 food panel's Core work.
// "Before" reproduces the removed panel code (consumer scans and per-shop lookups) on the same fixture;
// "after" is the shipped path (incremental counters, one-pass history/place index, week projection text).
// Desktop .NET 10 component timings only: no Unity, OnGUI, native reads or whole-frame evidence.
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using PersistentCity.Core;

const int Warmup = 20, Timed = 200;
var failures = new List<string>();
void Require(bool ok, string what) { if (!ok) failures.Add(what); }

static (double p50, double p95, double max, long bytes) Measure(Func<object> work)
{
    for (int i = 0; i < Warmup; i++) work();
    var times = new List<double>(Timed); var bytes = new List<long>(Timed);
    for (int i = 0; i < Timed; i++)
    {
        long allocated = GC.GetAllocatedBytesForCurrentThread(); long start = Stopwatch.GetTimestamp();
        GC.KeepAlive(work());
        times.Add((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
        bytes.Add(GC.GetAllocatedBytesForCurrentThread() - allocated);
    }
    times.Sort(); bytes.Sort();
    return (Math.Round(times[Timed / 2], 4), Math.Round(times[(int)(Timed * .95)], 4), Math.Round(times[^1], 4), bytes[Timed / 2]);
}
static object Row(string name, (double p50, double p95, double max, long bytes) m)
    => new { name, p50Ms = m.p50, p95Ms = m.p95, maxMs = m.max, medianBytes = m.bytes, spikesAtLeast30 = m.max >= 30 ? 1 : 0 };

static FoodCommerceState Consumers(int count)
{
    var random = new Random(count); var food = new FoodCommerceState();
    for (int i = 0; i < count; i++)
    {
        long income = random.Next(9500, 50000);
        var meal = new ResidentMeal { ResidentId = "c" + i, Status = i % 97 == 0 ? MealStatus.Uncertain : MealStatus.Pending,
            Excursion = i % 131 == 0 ? new MealExcursion { Unresolved = true } : null };
        if (i % 50 != 49)
            meal.Budget = new ResidentBudget { OpeningBalance = Money.FromCents(income * 7), Balance = Money.FromCents(random.Next(0, 400000)),
                IncomeReceived = Money.FromCents(income * random.Next(1, 30)), EssentialsPaid = Money.FromCents(random.Next(0, 9000000)),
                HomeFoodPaid = Money.FromCents(random.Next(0, 500000)), PreparedFoodSpent = Money.FromUnits((long)random.Next(0, 100000) * random.Next(1, 1 << 28)),
                HeldPurchase = i % 97 == 0 ? Money.FromCents(800) : Money.Zero, Arrears = i % 23 == 0 ? Money.FromCents(random.Next(1, 20000)) : Money.Zero,
                DailyHousingCents = random.Next(3000, 15000), DailyEssentialsCents = random.Next(1500, 2500), DailyHomeFoodCents = random.Next(900, 1500),
                LastPostedDay = 20, HomeFoodFundedDay = i % 17 == 0 ? 19 : 20 };
        food.Residents.Add(meal);
    }
    return food;
}
static string Amount(BigInteger units) => ((decimal)units / Money.UnitsPerDollar).ToString("C0", CultureInfo.GetCultureInfo("en-US"));

// Exactly the removed BuildEconomyText consumer work (counts plus the seven Money.TotalUnits projections).
static string BeforeScans(FoodCommerceState food)
{
    var text = new StringBuilder();
    int uncertain = food.Residents.Count(m => m.Status == MealStatus.Uncertain);
    if (uncertain > 0) text.AppendLine(uncertain + " purchases await confirmation.");
    int blocked = food.Residents.Count(m => m.Excursion?.Unresolved == true);
    if (blocked > 0) text.AppendLine(blocked + " consumers are waiting for a safe return route.");
    var budgets = food.Residents.Where(m => m.Budget != null).Select(m => m.Budget!).ToArray();
    if (budgets.Length > 0)
    {
        text.AppendLine("CURRENT RESIDENT ACCOUNTS · lifetime totals");
        text.AppendLine("Cash " + Amount(Money.TotalUnits(budgets.Select(b => b.Balance))) + " · received income/support " + Amount(Money.TotalUnits(budgets.Select(b => b.IncomeReceived))));
        text.AppendLine("Housing/essentials paid " + Amount(Money.TotalUnits(budgets.Select(b => b.EssentialsPaid))) + " · home-food baskets "
            + Amount(Money.TotalUnits(budgets.Select(b => b.HomeFoodPaid))) + " · prepared food " + Amount(Money.TotalUnits(budgets.Select(b => b.PreparedFoodSpent))));
        text.AppendLine("Unpaid bills " + Amount(Money.TotalUnits(budgets.Select(b => b.Arrears))) + " · purchase funds awaiting confirmation " + Amount(Money.TotalUnits(budgets.Select(b => b.HeldPurchase))));
    }
    return text.ToString();
}
static string AfterCounters(FoodMoneyCounters counters)
{
    var money = counters.Snapshot(); var text = new StringBuilder();
    if (money.Uncertain > 0) text.AppendLine(money.Uncertain + " purchases await confirmation.");
    if (money.BlockedReturns > 0) text.AppendLine(money.BlockedReturns + " consumers are waiting for a safe return route.");
    if (money.Budgets > 0) text.Append(money.Text());
    return text.ToString();
}

var output = new List<object>();
foreach (int count in new[] { 1914, 10000, 100000 })
{
    var food = Consumers(count); var counters = FoodMoneyCounters.Attach(food);
    var snapshot = counters.Snapshot(); var budgets = food.Residents.Where(m => m.Budget != null).Select(m => m.Budget!).ToArray();
    Require(snapshot.Equals(FoodMoneyCounters.Rescan(food)), count + ": counters differ from rescan");
    Require(snapshot.Balance == Money.TotalUnits(budgets.Select(b => b.Balance)) && snapshot.HeldPurchase == Money.TotalUnits(budgets.Select(b => b.HeldPurchase))
        && snapshot.PreparedFoodSpent == Money.TotalUnits(budgets.Select(b => b.PreparedFoodSpent)), count + ": totals differ from Money.TotalUnits");
    Require(snapshot.Uncertain == food.Residents.Count(m => m.Status == MealStatus.Uncertain), count + ": uncertain count");
    // The amounts the old panel printed are printed unchanged.
    foreach (var line in BeforeScans(food).Split(Environment.NewLine).Where(l => l.StartsWith("Cash ") || l.StartsWith("Housing") || l.StartsWith("Unpaid")))
        Require(AfterCounters(counters).Contains(line), count + ": changed money line " + line);
    var before = Measure(() => BeforeScans(food)); var after = Measure(() => AfterCounters(counters));
    // Setter overhead on the posting/settlement path, attached versus unattached, same operations.
    var loose = new ResidentBudget { Balance = Money.FromCents(1000), DailyHousingCents = 100 };
    var attached = food.Residents.First(m => m.Budget != null).Budget!;
    double Ns(ResidentBudget b)
    {
        var one = Money.FromUnits(1);
        for (int i = 0; i < 100000; i++) { b.Balance += one; b.Arrears = (i & 1) == 0 ? one : Money.Zero; }
        long allocated = GC.GetAllocatedBytesForCurrentThread(); long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < 1000000; i++) { b.Balance += one; b.Arrears = (i & 1) == 0 ? one : Money.Zero; }
        double ns = (Stopwatch.GetTimestamp() - start) * 1e9 / Stopwatch.Frequency / 2000000;
        Require(GC.GetAllocatedBytesForCurrentThread() == allocated, count + ": setter allocated");
        return Math.Round(ns, 2);
    }
    double looseNs = Ns(loose), attachedNs = Ns(attached);
    Require(counters.Snapshot().Equals(FoodMoneyCounters.Rescan(food)), count + ": counters differ after setter loop");
    output.Add(new { measurement = "consumer-scans", consumers = count, before = Row("removed scans", before), after = Row("counters", after),
        setterNsPerAssignment = new { unattached = looseNs, attached = attachedNs }, attachRebuildRequests = counters.Rebuilds });
}

foreach (int shops in new[] { 1, 64, 1024 })
{
    var random = new Random(shops); int placesCount = 20000;
    var places = Enumerable.Range(0, placesCount).Select(i => new Place { Id = "p" + i, Name = "Place " + i }).ToList();
    var scopes = Enumerable.Range(0, shops).Select(i => new FoodScope { PlaceId = "p" + random.Next(placesCount), BusinessId = "b" + i }).ToList();
    var history = new List<FoodShopDay>(); int today = 30;
    foreach (var scope in scopes)
        for (int day = today - 13; day <= today; day++)
            history.Add(new FoodShopDay { Day = day, PlaceId = scope.PlaceId, BusinessId = scope.BusinessId, Purchases = random.Next(0, 80),
                Revenue = Money.FromCents(random.Next(0, 80000)), OffersConsidered = 30, ChosenOffers = 10, StaffRejections = 12, PriceRejections = 8 });
    // Order like the live list (appended by day across shops).
    history = history.OrderBy(h => h.Day).ToList();
    object BeforeLookups()
    {
        long sum = 0;
        foreach (var scope in scopes)
        {
            var place = places.First(p => p.Id == scope.PlaceId);
            var observed = history.Where(h => h.PlaceId == scope.PlaceId && h.BusinessId == scope.BusinessId).OrderByDescending(h => h.Day).FirstOrDefault();
            sum += place.Name.Length + (observed?.Purchases ?? 0);
        }
        return sum;
    }
    object AfterLookups()
    {
        var text = new StringBuilder();
        var index = FoodFeedback.IndexHistory(history);
        var shopPlaces = new Dictionary<string, Place?>(scopes.Count, StringComparer.Ordinal);
        foreach (var scope in scopes) shopPlaces[scope.PlaceId] = null;
        foreach (var p in places) if (shopPlaces.TryGetValue(p.Id, out var found) && found == null) shopPlaces[p.Id] = p;
        var none = new List<FoodShopDay>();
        foreach (var scope in scopes)
        {
            var place = shopPlaces[scope.PlaceId]!;
            var rows = index.TryGetValue(FoodFeedback.Key(scope.PlaceId, scope.BusinessId), out var r) ? r : none;
            FoodShopDay? observed = null;
            foreach (var row in rows) if (observed == null || row.Day > observed.Day) observed = row;
            text.Append(place.Name).Append(observed?.Purchases ?? 0);
            FoodFeedback.Weeks(rows, scope.PlaceId, scope.BusinessId, today).AppendTo(text);
        }
        return text.ToString();
    }
    // Invariant: each projected week equals the rows it summarises.
    var index = FoodFeedback.IndexHistory(history);
    foreach (var scope in scopes)
    {
        var weeks = FoodFeedback.Weeks(index[FoodFeedback.Key(scope.PlaceId, scope.BusinessId)], scope.PlaceId, scope.BusinessId, today);
        var mine = history.Where(h => h.PlaceId == scope.PlaceId && h.BusinessId == scope.BusinessId).ToList();
        Require(weeks.ThisWeek.Purchases == mine.Where(h => h.Day > today - 7).Sum(h => h.Purchases)
            && weeks.LastWeek.Offers == mine.Where(h => h.Day <= today - 7).Sum(h => h.OffersConsidered)
            && weeks.LastWeek.Coverage == FoodWeekCoverage.Complete, shops + " shops: week sums");
    }
    var before = Measure(BeforeLookups); var after = Measure(AfterLookups);
    output.Add(new { measurement = "panel-shop-blocks", shops, places = placesCount, historyRows = history.Count,
        before = Row("removed per-shop lookups (no week text)", before), after = Row("one-pass index plus week text", after) });
}

// Hidden panel: a day of budget/meal mutations requests nothing and allocates nothing.
{
    var food = Consumers(1914); var counters = FoodMoneyCounters.Attach(food); var one = Money.FromUnits(1);
    foreach (var m in food.Residents) { if (m.Budget != null) m.Budget.Balance += one; m.Status = m.Status; } // warm
    long allocated = GC.GetAllocatedBytesForCurrentThread();
    foreach (var m in food.Residents)
    {
        if (m.Budget != null) { m.Budget.Balance += one; m.Budget.LastPostedDay = 21; m.Budget.HomeFoodFundedDay = 21; }
        m.Status = m.Status == MealStatus.Pending ? MealStatus.Travelling : MealStatus.Pending;
    }
    long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
    Require(bytes == 0 && counters.Requests == 0, "hidden panel built or allocated");
    Require(counters.Snapshot().Equals(FoodMoneyCounters.Rescan(food)), "counters differ after hidden mutations");
    output.Add(new { measurement = "hidden-panel", consumers = 1914, mutationBytes = bytes, snapshotRequestsWhileHidden = 0, textBuildsWhileHidden = 0 });
}

foreach (var row in output) Console.WriteLine(JsonSerializer.Serialize(row));
Console.WriteLine(JsonSerializer.Serialize(new { evidence = "Windows .NET 10 Release, synthetic Core fixtures; " + Warmup + " warm-up + " + Timed + " timed requests per row; no Unity/OnGUI, native reads or whole-frame evidence", failures }));
if (failures.Count > 0) { Console.Error.WriteLine(string.Join(Environment.NewLine, failures)); return 1; }
Console.WriteLine("F6c feedback panel regression passed.");
return 0;
