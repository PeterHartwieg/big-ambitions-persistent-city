using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using System.Numerics;
using PersistentCity.MovementEquivalence;
using PersistentCity.Core;
using PersistentCity.Persistence;

namespace PersistentCity.EconomyInterventions;

public enum Fault { None, DuplicatedDebit, UnbalancedDebit, DuplicateNativeOrder }

public sealed class RunOptions
{
    public string Family = "control", Arm = "control";
    public int Seed;
    public int Residents = EconomyFixture.Residents;
    public int Days = EconomyRun.Days;
    public IReadOnlyList<InterventionInput> Inputs = Array.Empty<InterventionInput>();
    /// <summary>Optional city save/reload point. A fractional minute reloads mid-epoch.</summary>
    public double? ReloadAt;
    public Fault Fault;
    public int FaultDay = EconomyRun.BaselineDays + 2;
    public bool OrdinaryVisits;
    public bool Integrated;
    public double PriceWeight = 1;
    public FixtureProfile Profile = FixtureProfile.CapacityBound;
    public bool RivalListed = true, OwnListed = true;
    // F1 cadence: 30 simulated minutes/real second as in AccountingScenario. Zero uses epoch drain.
    public int FramesPerSecond, EventUnitsPerFrame = 256;
    public double MillisecondsPerFrame = 4;
    public int? BodyCap;
    public bool MeasureSlices;
}

public sealed class RunResult
{
    public string Json = "";
    public List<DayMetrics> Days = new();
    public string FinalSaveSha256 = "";
    public long ConservationChecks, InputsPublished, Epochs;
    public int Reloads, InFlightAtReload, JournalEntries;
    public double Seconds;
    public long AllocatedBytes;
    public readonly List<double> SliceMilliseconds = new();
    public long Frames, BodyAcquisitions, BodyObservations;
    public int MaximumBodies;
    public BigInteger EmploymentUnits, DiscretionaryUnits;
    public long Purchases;
}

/// <summary>Deterministic Core scenario runner. The city advances epoch by epoch under the F1
/// accounting boundary: drain the ending epoch, check the ledger at midnight, publish the inputs
/// stamped at the boundary, enter the native epoch and only then observe it. No Runtime code,
/// optional F1 frame cadence and F2a lease-driver inputs, with no Core behaviour change.</summary>
public static class EconomyRun
{
    public const string Schema = "persistentcity-f6a-metrics-1";
    // Section 3.5 protocol: six weeks, five seeds, matched same-seed control.
    public const int BaselineDays = 14, InterventionDays = 14, RevertedDays = 14;
    public const int Days = BaselineDays + InterventionDays + RevertedDays;
    public const int Seeds = 5;
    // Outcomes advance lazily per consumer; 03:00 is after every dinner and before any breakfast.
    public const int OutcomeSampleMinute = 180;
    // F6 section 9 Q3 default: market share stays a diagnostic/experiment metric, not free UI.
    public const bool MarketShareDiagnosticOnly = true;
    // F5 B4 (merged plan): person wallets. Today each represented consumer owns one budget.
    public const string OwnerKind = "consumer-budget";

    public static RunResult Run(RunOptions options)
    {
        if (!double.IsFinite(options.PriceWeight) || options.PriceWeight < 0 || options.PriceWeight > 3)
            throw new ArgumentOutOfRangeException(nameof(options.PriceWeight));
        long allocated = GC.GetTotalAllocatedBytes(true);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var (state, market) = EconomyFixture.Build(options.Seed, options.Residents, options.RivalListed, options.OwnListed, options.Profile);
        market.ChoiceSettings.FoodChoicePriceWeight = options.PriceWeight;
        if (options.Integrated) market.Journal = new FoodChangeJournal(state.Food!, state.Economy);
        bool ordinary = options.OrdinaryVisits || options.Integrated;
        if (ordinary)
        {
            state.Places.Add(new Place { Id = "ordinary-retail", Name = "Outside non-food shop", Kind = PlaceKind.Shop, Position = new Position(3, 0, 0) });
            state.Places.Add(new Place { Id = "ordinary-leisure", Name = "Outside leisure", Kind = PlaceKind.Leisure, Position = new Position(0, 0, 3) });
            foreach (var p in state.Citizens) { p.ShopId = "ordinary-retail"; p.LeisureId = "ordinary-leisure"; }
        }
        var routes = new EconomyRoutes(market);
        var observer = new DecisionObserver();
        var city = EconomyFixture.Attach(state, market, routes, fresh: true);
        if (ordinary) city.AttachOrdinaryExchanges(new OrdinaryExperimentOwnership(state.Minute));
        city.FoodDecisionObserved += observer.Observe;
        double start = EconomyFixture.StartMinute;
        // The last requested day ends at start + Days days; its outcomes are sampled at 03:00 after it.
        long end = (long)start + options.Days * 1440L + OutcomeSampleMinute;
        var schedule = new InputSchedule(options.Inputs, start, end);
        var result = new RunResult();
        market.Enter((long)start);
        Require(city.BeginAccountingObservations(start), "initial epoch not published");
        var pending = new SortedDictionary<int, DayMetrics>();
        var previous = Household.Capture(city.State);
        var ordinaryDays = new Dictionary<int, long>();
        var driver = options.BodyCap.HasValue ? new PresentationDriver(city, new DriverSettings {
            Pedestrians = options.BodyCap.Value, Vehicles = options.BodyCap.Value == 0 ? 0 : 24, Seed = options.Seed }) : null;
        if (driver != null && options.ReloadAt.HasValue) throw new ArgumentException("Body/reload arms are separate experiments");
        void Advance(double target)
        {
            if (options.FramesPerSecond == 0)
            {
                if (!options.MeasureSlices) { Drain(city, target); return; }
                while (!city.IsCaughtUpTo(target))
                {
                    long begin = Stopwatch.GetTimestamp();
                    city.AdvanceFrameSlice(target, 1);
                    result.SliceMilliseconds.Add(Stopwatch.GetElapsedTime(begin).TotalMilliseconds);
                }
                return;
            }
            // Same F1 native float-minute fence and budgeted AdvanceFrameSlice order as
            // AccountingScenario, with the existing F6a/F1-derived fixture and all F6 loops.
            int hour = (int)(Math.Max(city.State.Minute, market.Epoch) / 60);
            float minute = (float)(Math.Max(city.State.Minute, market.Epoch) % 60);
            double native = hour * 60.0 + minute;
            while (!city.IsCaughtUpTo(target))
            {
                Require(AccountingClock.Compatible(city.State.Minute, native), "cadence input epoch escaped");
                Require(city.IsAccountingEpochDrained(native), "cadence ending epoch escaped");
                bool ready = city.IsCaughtUpTo(AccountingClock.Target(native));
                float allowed = AccountingClock.Limit(ready, native, (float)(30.0 / options.FramesPerSecond));
                minute += allowed;
                while (minute >= 60) { minute -= 60; hour++; }
                native = hour * 60.0 + minute;
                Require((long)native == market.Epoch, "cadence crossed undrained epoch");
                Require(city.BeginAccountingObservations(native), "cadence observation not published");
                // F2a bodies move in real time even on F1 frames that hold native time.
                driver?.MoveBodies(1.0 / options.FramesPerSecond, 30.0 / options.FramesPerSecond);
                long begin = Stopwatch.GetTimestamp();
                for (int unit = 0; unit < options.EventUnitsPerFrame; unit++)
                {
                    var advanced = city.AdvanceFrameSlice(Math.Min(target, AccountingClock.Target(native)), 1);
                    if (advanced.CaughtUp || Stopwatch.GetElapsedTime(begin).TotalMilliseconds >= options.MillisecondsPerFrame) break;
                }
                city.SynchronizeSnapshot();
                driver?.Update(new Position(2, 0, 0), false);
                result.Frames++;
                if (options.MeasureSlices) result.SliceMilliseconds.Add(Stopwatch.GetElapsedTime(begin).TotalMilliseconds);
            }
        }
        bool reloaded = false;
        for (long epoch = (long)start + 1; epoch <= end; epoch++)
        {
            double ending = AccountingClock.PreviousEpochTarget(epoch);
            // A fractional reload point saves at the first half-minute at or after it with a meal
            // in flight (decision, travel or service), so the save interrupts real work.
            if (options.ReloadAt is double at && !reloaded && at != Math.Floor(at) && epoch - 0.5 >= at)
            {
                Advance(epoch - 0.5);
                if (InFlight(city) > 0) { city = Reload(city, market, routes, result, observer, ordinary); reloaded = true; }
            }
            Advance(ending);
            bool drained = city.IsAccountingEpochDrained(epoch);
            Require(drained, "ending epoch did not drain");
            int day = (int)(epoch / 1440);
            if (epoch % 1440 == 0 && day >= 2 && day - 1 <= options.Days)
            {
                if (options.Fault != Fault.None && day - 1 == options.FaultDay) Inject(options.Fault, city.State, market);
                var metrics = new DayMetrics { Day = day - 1, OrdinaryVisits = ordinary };
                Check(city, market, metrics);
                if (ordinary)
                {
                    ordinaryDays[metrics.Day] = metrics.OrdinaryDiscretionaryUnits;
                    ordinaryDays.TryGetValue(metrics.Day - EconomicLedger.Days, out long expired);
                    if (metrics.OrdinaryDiscretionaryUnits - expired != metrics.OrdinaryWindowUnits)
                        throw new LedgerViolation("ordinary dated totals do not equal watermarked wallet debits");
                }
                result.ConservationChecks += metrics.ConservationChecks;
                var current = Household.Capture(city.State);
                Household.Write(previous, current, metrics);
                previous = current;
                pending[day - 1] = metrics;
            }
            if (epoch % 1440 == OutcomeSampleMinute && pending.TryGetValue(day - 1, out var sample))
            {
                Outcomes(city.State, day - 1, sample, observer, market);
                result.Days.Add(sample); pending.Remove(day - 1);
            }
            if (options.Fault == Fault.DuplicateNativeOrder && epoch == options.FaultDay * 1440L + 12 * 60)
                market.InjectDuplicateOrder = true;
            result.InputsPublished += schedule.PublishAt(epoch, drained, city.State.Minute, market);
            market.Enter(epoch);
            Require(city.BeginAccountingObservations(epoch), "observation epoch not published");
            result.Epochs++;
            if (options.ReloadAt is double whole && !reloaded && whole == epoch)
            { city = Reload(city, market, routes, result, observer, ordinary); reloaded = true; }
        }
        Require(pending.Count == 0 && result.Days.Count == options.Days, "missing day samples");
        Require(result.Epochs == end - (long)start, "unexpected epoch count");
        Require(options.ReloadAt == null || reloaded, "requested reload did not happen");
        driver?.BeforeSave();
        result.BodyAcquisitions = driver?.Acquired ?? 0;
        result.BodyObservations = driver?.LeaseObservations ?? 0;
        result.MaximumBodies = driver?.MaxWalkers ?? 0;
        result.EmploymentUnits = city.State.Food!.Residents.Aggregate(BigInteger.Zero, (total, m) => total + m.Budget!.Ledger.EmploymentIncome.Units);
        result.DiscretionaryUnits = city.State.Food.Residents.Aggregate(BigInteger.Zero, (total, m) => total + m.Budget!.Ledger.Discretionary.Units);
        result.Purchases = city.State.Food.Residents.Sum(m => m.Purchases);
        result.JournalEntries = city.State.Economy.Shops.Sum(s => s.Changes.Count);
        result.FinalSaveSha256 = Sha256(SaveCodec.Write(city.State));
        result.Json = Write(options, schedule, result.Days);
        result.Seconds = timer.Elapsed.TotalSeconds;
        result.AllocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated;
        return result;
    }

    private static void Drain(CitySimulator city, double target)
    {
        int guard = 0;
        while (!city.AdvanceTo(target, 10000).CaughtUp) Require(++guard < 100000, "nonterminating epoch");
    }

    private static CitySimulator Reload(CitySimulator city, EconomyMarket market, EconomyRoutes routes, RunResult result, DecisionObserver observer, bool ordinary)
    {
        result.InFlightAtReload = InFlight(city);
        string before = SaveCodec.Write(city.State);
        var read = SaveCodec.Read(before);
        Require(read.Status == SaveReadStatus.Loaded, "reload refused: " + read.Error);
        Require(SaveCodec.Write(read.State!) == before, "reload round trip changed the save");
        result.Reloads++;
        if (market.Journal != null) market.Journal = new FoodChangeJournal(read.State!.Food!, read.State.Economy);
        var next = EconomyFixture.Attach(read.State!, market, routes, fresh: false);
        if (ordinary) next.AttachOrdinaryExchanges(new OrdinaryExperimentOwnership(read.State!.Minute));
        // The observer is harness state; it continues across the city reload.
        next.FoodDecisionObserved += observer.Observe;
        return next;
    }

    private static int InFlight(CitySimulator city) => city.State.Food!.Residents.Count(m => m.Decision != null
        || m.Status == MealStatus.Travelling || m.Status == MealStatus.Serving);

    private static void Inject(Fault fault, CityState state, EconomyMarket market)
    {
        var budget = state.Food!.Residents.Select(m => m.Budget).First(b => b != null && b.Balance > Money.FromCents(1000))!;
        switch (fault)
        {
            // Balanced but duplicated: both sides of CashConserved move, the watermarked outflow does not.
            case Fault.DuplicatedDebit: budget.Balance -= Money.FromCents(500); budget.ExternalFoodSpent += Money.FromCents(500); break;
            case Fault.UnbalancedDebit: budget.Balance -= Money.FromCents(1); break;
        }
    }

    /// <summary>The F6 stable-accounting identities, checked for every owner at every midnight.</summary>
    private static void Check(CitySimulator city, EconomyMarket market, DayMetrics metrics)
    {
        var food = city.State.Food!;
        long prepared = 0, uncertain = 0;
        foreach (var meal in food.Residents)
        {
            var b = meal.Budget ?? throw new LedgerViolation("consumer without a budget: " + meal.ResidentId);
            bool conserved;
            try { conserved = HouseholdFinance.CashConserved(b); } catch (OverflowException) { conserved = false; }
            if (!conserved) throw new LedgerViolation("conservation identity failed for " + meal.ResidentId + " on day " + metrics.Day);
            metrics.ConservationChecks++;
            prepared += b.PreparedFoodSpent.Units;
            metrics.OrdinaryDiscretionaryUnits += b.Ledger.Discretionary.Units;
            if (meal.Status == MealStatus.Uncertain && market.IsConfirmedPaid(meal)) uncertain += meal.Price.Units;
        }
        metrics.OrdinaryWindowUnits = (long)city.FreeOutsideSpending().Units;
        metrics.NativeOrders = market.NativeOrders; metrics.NativeRevenueUnits = market.NativeRevenueUnits;
        metrics.ConfirmedPreparedUnits = prepared; metrics.UncertainConfirmedUnits = uncertain;
        metrics.DuplicateOrders = market.DuplicateOrders; metrics.HistoricalClockMisses = city.HistoricalClockMisses;
        metrics.EpochReadViolations = market.EpochReadViolations;
        metrics.OutsideLedgerUnits = (long)RivalOutflowHistory.Ledger(food);
        var view = food.Outflow?.Capture((int)(city.State.Minute / 1440));
        metrics.WatermarkedOutflowWindowUnits = (long)(view?.Units ?? 0);
        metrics.WatermarkedOutflowUnits = food.Outflow == null ? 0 : CumulativeWatermarked(food.Outflow);
        if (metrics.WatermarkedOutflowUnits != metrics.OutsideLedgerUnits)
            throw new LedgerViolation("modeled outside total does not equal cumulative watermarked debits on day " + metrics.Day);
        if (market.DuplicateOrders != 0) throw new LedgerViolation("duplicate native receipt on day " + metrics.Day);
        if (prepared + uncertain != market.NativeRevenueUnits)
            throw new LedgerViolation("PC confirmed receipts do not equal native sales on day " + metrics.Day);
        if (food.Outflow != null && !food.Outflow.Reconciles(food))
            throw new LedgerViolation("modeled outside total does not equal watermarked debits on day " + metrics.Day);
        if (city.HistoricalClockMisses != 0) throw new LedgerViolation("historical clock miss on day " + metrics.Day);
        if (market.EpochReadViolations != 0) throw new LedgerViolation("native read outside its epoch on day " + metrics.Day);
    }

    /// <summary>Lifetime watermarked units: the history's baseline, expired rows and retained rows,
    /// read from its public saved wire (the fourteen-day view alone is a rolling window).</summary>
    public static long CumulativeWatermarked(RivalOutflowHistory history)
    {
        var fields = history.ToWire().Split('|');
        var total = System.Numerics.BigInteger.Parse(fields[3], System.Globalization.CultureInfo.InvariantCulture)
            + System.Numerics.BigInteger.Parse(fields[4], System.Globalization.CultureInfo.InvariantCulture);
        foreach (var row in fields.Skip(6)) total += System.Numerics.BigInteger.Parse(row.Split(',')[1], System.Globalization.CultureInfo.InvariantCulture);
        return (long)total;
    }

    private static void Outcomes(CityState state, int day, DayMetrics metrics, DecisionObserver observer, EconomyMarket market)
    {
        foreach (var meal in state.Food!.Residents)
        {
            var today = meal.Today;
            if (today == null) continue;
            if (today.Day > day) throw new LedgerViolation("outcome sample overlapped the next day for " + meal.ResidentId);
            if (today.Day != day) continue;
            foreach (var o in today.Meals)
            {
                metrics.Resolved++; metrics.OutcomeMeals[MealOutcomes.Index(o.Kind)]++;
                if (o.Status == MealOutcomeStatus.Bought) { if (o.TargetKind == FoodAlternativeKind.Rival) { metrics.RivalMeals++; metrics.RivalHours[o.CheckoutAt % 1440 / 60]++; } else
                    {
                        metrics.OwnMeals++;
                        string business = EconomyFixture.BusinessAt(o.PlaceId) ?? throw new LedgerViolation("own meal at unknown place " + o.PlaceId);
                        if (!metrics.OwnMealHours.TryGetValue(business, out var hours)) metrics.OwnMealHours[business] = hours = new long[24];
                        hours[o.CheckoutAt % 1440 / 60]++;
                    } }
                else if (o.Status == MealOutcomeStatus.ChoseAlternative) metrics.HomeMeals++;
                else if (o.Status == MealOutcomeStatus.Skipped) metrics.Skipped++;
                else metrics.Unconfirmed++;
                foreach (var business in observer.ConsideredBy(day, meal.ResidentId, o.Kind))
                {
                    if (!metrics.Catchments.TryGetValue(business, out var c)) metrics.Catchments[business] = c = new DayMetrics.CatchmentRow();
                    c.Resolved++;
                    if (o.Status == MealOutcomeStatus.Bought)
                    {
                        if (o.TargetKind == FoodAlternativeKind.Rival) c.Rival++;
                        else if (o.PlaceId == EconomyFixture.PlaceOf(business)) c.ThisShop++;
                        else c.OtherOwn++;
                    }
                    else if (o.Status == MealOutcomeStatus.ChoseAlternative) c.Home++;
                    else if (o.Status == MealOutcomeStatus.Skipped) c.Skipped++;
                    else c.Unconfirmed++;
                }
            }
        }
        foreach (var (key, tally) in market.Tickets.Where(t => t.Key.Day == day)) metrics.Tickets[(key.Business, key.Main)] = tally;
        foreach (var key in market.Tickets.Keys.Where(k => k.Day <= day).ToList()) market.Tickets.Remove(key);
        foreach (var (key, offer) in observer.OffersOn(day)) metrics.Offers[key] = offer;
        observer.Prune(day);
        foreach (var row in state.Food.History.Where(h => h.Day == day).OrderBy(h => h.BusinessId, StringComparer.Ordinal))
        {
            var s = new DayMetrics.ShopRow { Purchases = row.Purchases, RevenueUnits = row.Revenue.Units, Considered = row.OffersConsidered,
                Chosen = row.ChosenOffers, Price = row.PriceRejections, Access = row.AccessRejections, Closed = row.ClosedRejections,
                Stock = row.StockRejections, Staff = row.StaffRejections, Service = row.ServiceRejections, Unknown = row.UnknownEvidence,
                Preferred = row.PreferredAlternatives, Hours = row.PurchaseHours.ToArray(), Meals = row.PurchaseMeals.ToArray() };
            foreach (var p in row.ProductSales) s.Products[p.ProductId] = (p.Purchases, p.Revenue.Units);
            metrics.Shops[row.BusinessId] = s;
        }
    }

    private sealed class Household
    {
        public long Income, Unfed, Missed;
        public Dictionary<string, bool> Employed = new(StringComparer.Ordinal);
        public long[] Balances = Array.Empty<long>();
        public long Arrears, Scheduled;

        public static Household Capture(CityState state)
        {
            var h = new Household();
            var balances = new List<long>();
            foreach (var meal in state.Food!.Residents)
            {
                var b = meal.Budget!;
                h.Income += (b.IncomeReceived + b.Ledger.EmploymentIncome).Units; h.Unfed += b.DaysWithoutHomeFood;
                if (b.Arrears > Money.Zero) h.Arrears++;
                if (b.IncomeSource == ResidentIncomeSource.ExternalEmployment) h.Scheduled += Money.FromCents(b.DailyIncomeCents).Units;
                balances.Add(b.Balance.Units);
            }
            foreach (var person in state.Citizens.OrderBy(p => p.Identity.Id, StringComparer.Ordinal))
            { h.Employed[person.Identity.Id] = person.DayPlaceId.Length != 0; h.Missed += person.MissedActivities; }
            balances.Sort(); h.Balances = balances.ToArray();
            return h;
        }
        public static void Write(Household before, Household after, DayMetrics m)
        {
            m.Owners = after.Balances.Length; m.ArrearsOwners = after.Arrears;
            m.UnfedDays = after.Unfed - before.Unfed; m.IncomeReceivedUnits = after.Income - before.Income;
            m.ScheduledEmploymentUnits = after.Scheduled; m.MissedActivities = after.Missed - before.Missed;
            m.MedianBalanceUnits = after.Balances[after.Balances.Length / 2];
            m.P10BalanceUnits = after.Balances[after.Balances.Length / 10];
            foreach (var (id, employed) in after.Employed)
            {
                if (employed) m.Employed++;
                if (before.Employed.TryGetValue(id, out var was) && was != employed) { if (employed) m.JobFinds++; else m.JobLosses++; }
            }
        }
    }

    private static string Write(RunOptions options, InputSchedule schedule, List<DayMetrics> days)
    {
        bool ordinary = options.OrdinaryVisits || options.Integrated;
        var json = new Json();
        json.Open().Field("schema", options.Integrated ? "persistentcity-f6h-metrics-1" : Schema).Field("family", options.Family).Field("arm", options.Arm).Field("seed", options.Seed)
            .Open("fixture").Field("residents", options.Residents).Field("consumers", options.Residents * EconomyFixture.RepresentationFactor)
            .Field("days", options.Days).Field("rivalListed", options.RivalListed).Field("ownListed", options.OwnListed)
            .Field("mealsPerHour", options.Profile.MealsPerHour).Field("walkingRouteMinutes", 1).Field("unitsPerCent", Money.UnitsPerCent).Close()
            .Open("population").Field("ownerKind", ordinary ? "person-wallet" : OwnerKind).Field("perPersonWallets", ordinary)
            .Raw("kindsMeasured", ordinary ? "[\"resident\",\"visitor\"]" : "[\"resident\"]")
            .Raw("kindsReserved", ordinary ? "[]" : "[\"commuter\",\"dayVisitor\"]").Close();
        // Older sweeps carry no profile block; the default profile keeps their bytes unchanged.
        if (options.Profile != FixtureProfile.CapacityBound)
        {
            json.Open("profile").Field("name", options.Profile.Name).Field("mealsPerHour", options.Profile.MealsPerHour).OpenArray("menuCents");
            foreach (var (product, cents) in options.Profile.Menu) json.RawItem("{\"product\":\"" + product + "\",\"cents\":" + cents + "}");
            json.CloseArray().Close();
        }
        if (options.Integrated) json.Open("integrated").Field("writer", CityState.CurrentSchema)
            .Ratio("priceWeight", options.PriceWeight).Field("rivalLanes", RivalServiceCalibration.DefaultLanes)
            .Field("rivalUnits", RivalServiceCalibration.DefaultMealPeriodUnits)
            .Ratio("jobLossK", CityBalance.JobLossPerMiss).Close();
        json.OpenArray("inputs"); foreach (var input in schedule.Inputs) json.RawItem(input.Json()); json.CloseArray();
        json.OpenArray("days"); foreach (var day in days) day.Write(json); json.CloseArray();
        json.Close();
        return json.ToString();
    }

    public static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private sealed class OrdinaryExperimentOwnership : IOrdinaryVisitOwnership
    {
        private long loadedHour, observedHour = -1;
        internal OrdinaryExperimentOwnership(double minute) { loadedHour = (long)(minute / 60); }
        public bool Pending => false;
        public void Continue() { }
        public void ObserveBoundary(double minute) { if ((long)(minute / 60) > loadedHour) observedHour = (long)(minute / 60); }
        public bool TryGet(string place, out OrdinaryVisitTerms terms)
        {
            var kind = place == "ordinary-retail" ? OrdinaryVisitClass.Retail : place == "ordinary-leisure" ? OrdinaryVisitClass.Leisure : OrdinaryVisitClass.ActivityOnly;
            terms = new OrdinaryVisitTerms(observedHour, false, kind); return observedHour >= 0;
        }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
