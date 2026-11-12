using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PersistentCity.Core;

namespace PersistentCity.EconomyInterventions;

public sealed class Json
{
    private readonly StringBuilder text = new();
    private readonly Stack<bool> first = new();
    public override string ToString() => text.ToString();
    private void Comma() { if (first.Count > 0) { if (!first.Peek()) text.Append(','); first.Pop(); first.Push(false); } }
    public Json Open(string? key = null) { if (key != null) KeyOnly(key); else Comma(); text.Append('{'); first.Push(true); return this; }
    public Json Close() { text.Append('}'); first.Pop(); return this; }
    public Json OpenArray(string? key = null) { if (key != null) KeyOnly(key); else Comma(); text.Append('['); first.Push(true); return this; }
    public Json CloseArray() { text.Append(']'); first.Pop(); return this; }
    private void KeyOnly(string key) { Comma(); text.Append('"').Append(key).Append("\":"); }
    public Json Field(string key, long value) { KeyOnly(key); text.Append(value.ToString(CultureInfo.InvariantCulture)); return this; }
    public Json Field(string key, bool value) { KeyOnly(key); text.Append(value ? "true" : "false"); return this; }
    public Json Field(string key, string value) { KeyOnly(key); text.Append('"').Append(value.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"'); return this; }
    public Json Ratio(string key, double value) { KeyOnly(key); text.Append(Fixed(value)); return this; }
    public Json Null(string key) { KeyOnly(key); text.Append("null"); return this; }
    public Json Raw(string key, string json) { KeyOnly(key); text.Append(json); return this; }
    public Json Values(string key, IEnumerable<long> values)
    { KeyOnly(key); text.Append('[').Append(string.Join(",", values.Select(v => v.ToString(CultureInfo.InvariantCulture)))).Append(']'); return this; }
    public Json Ratios(string key, IEnumerable<double> values)
    { KeyOnly(key); text.Append('[').Append(string.Join(",", values.Select(Fixed))).Append(']'); return this; }
    public Json Item(long value) { Comma(); text.Append(value.ToString(CultureInfo.InvariantCulture)); return this; }
    public Json RawItem(string json) { Comma(); text.Append(json); return this; }
    public static string Fixed(double value) => double.IsNaN(value) || double.IsInfinity(value) ? "null"
        : Math.Round(value, 6, MidpointRounding.AwayFromZero).ToString("0.######", CultureInfo.InvariantCulture);
}

public sealed class DayMetrics
{
    public int Day;
    public long Owners, ArrearsOwners, UnfedDays, MedianBalanceUnits, P10BalanceUnits, IncomeReceivedUnits,
        ScheduledEmploymentUnits, Employed, JobLosses, JobFinds, MissedActivities;
    public long ConservationChecks, NativeOrders, NativeRevenueUnits, ConfirmedPreparedUnits, UncertainConfirmedUnits,
        DuplicateOrders, HistoricalClockMisses, OutsideLedgerUnits, WatermarkedOutflowUnits, WatermarkedOutflowWindowUnits, EpochReadViolations;
    public bool OrdinaryVisits;
    public long OrdinaryDiscretionaryUnits, OrdinaryWindowUnits;
    public long Resolved, OwnMeals, RivalMeals, HomeMeals, Skipped, Unconfirmed;
    public readonly long[] OutcomeMeals = new long[4];
    public readonly long[] RivalHours = new long[24];
    public readonly SortedDictionary<string, long[]> OwnMealHours = new(StringComparer.Ordinal);
    public readonly SortedDictionary<(string Business, string Main), (long Meals, long Extras, long Planned)> Tickets = new();
    public readonly SortedDictionary<string, ShopRow> Shops = new(StringComparer.Ordinal);
    public readonly SortedDictionary<string, CatchmentRow> Catchments = new(StringComparer.Ordinal);
    public readonly SortedDictionary<string, OfferRow> Offers = new(StringComparer.Ordinal);

    public sealed class CatchmentRow { public long Resolved, ThisShop, OtherOwn, Rival, Home, Skipped, Unconfirmed; }
    public sealed class OfferRow { public long Considered, Feasible, Chosen; }

    public sealed class ShopRow
    {
        public long Purchases, RevenueUnits, Considered, Chosen, Price, Access, Closed, Stock, Staff, Service, Unknown, Preferred;
        public long[] Hours = new long[24], Meals = new long[4];
        public readonly SortedDictionary<string, (long Purchases, long RevenueUnits)> Products = new(StringComparer.Ordinal);
    }

    public long OwnPurchases => Shops.Values.Sum(s => s.Purchases);
    public long OwnRevenueUnits => Shops.Values.Sum(s => s.RevenueUnits);
    public long ProductPurchases(string product) => Shops.Values.Sum(s => s.Products.TryGetValue(product, out var p) ? p.Purchases : 0);
    public long ProductOffers(string product, Func<OfferRow, long> metric) => Offers.Where(o => o.Key.EndsWith("|" + product, StringComparison.Ordinal)).Sum(o => metric(o.Value));
    public long Catchment(string business, Func<CatchmentRow, long> metric) => Catchments.TryGetValue(business, out var c) ? metric(c) : 0;

    public void Write(Json json)
    {
        json.Open().Field("day", Day).Field("week", (Day - 1) / 7 + 1).Field("phase", Phase(Day));
        json.Open("own").Field("purchases", OwnPurchases).Field("revenueUnits", OwnRevenueUnits);
        json.OpenArray("shops");
        foreach (var (id, s) in Shops)
        {
            var mealHours = OwnMealHours.TryGetValue(id, out var h) ? h : new long[24];
            json.Open().Field("businessId", id).Field("purchases", s.Purchases).Field("revenueUnits", s.RevenueUnits)
                .Field("meals", mealHours.Sum()).Values("mealsByCheckoutHour", mealHours)
                .Values("byMeal", s.Meals).Values("byHour", s.Hours)
                .Open("offers").Field("considered", s.Considered).Field("chosen", s.Chosen).Field("price", s.Price)
                .Field("travelTime", s.Access).Field("closedLate", s.Closed).Field("stock", s.Stock).Field("noStaffedCounter", s.Staff)
                .Field("serviceQueue", s.Service).Field("unknown", s.Unknown).Field("preferredAnother", s.Preferred).Close();
            json.OpenArray("tickets");
            foreach (var ((business, main), t) in Tickets.Where(t => t.Key.Business == id))
                json.Open().Field("mainProductId", main).Field("meals", t.Meals).Field("extras", t.Extras).Field("plannedExtras", t.Planned).Close();
            json.CloseArray();
            json.OpenArray("products");
            foreach (var (product, p) in s.Products) json.Open().Field("productId", product).Field("purchases", p.Purchases).Field("revenueUnits", p.RevenueUnits).Close();
            json.CloseArray().Close();
        }
        json.CloseArray().Close();
        json.Open("rival").Field("meals", RivalMeals).Values("byCheckoutHour", RivalHours).Field("estimatedLimits", "estimated (modeled service limits)").Close();
        json.Open("share").Field("diagnosticOnly", EconomyRun.MarketShareDiagnosticOnly)
            .Open("city").Field("resolved", Resolved).Field("own", OwnMeals).Field("rival", RivalMeals).Field("home", HomeMeals)
            .Field("skipped", Skipped).Field("unconfirmed", Unconfirmed).Values("resolvedByMeal", OutcomeMeals).Close()
            .Field("catchmentDefinition", "resolved meal opportunities whose decision evaluated the business");
        json.OpenArray("catchments");
        foreach (var (id, c) in Catchments)
            json.Open().Field("businessId", id).Field("resolved", c.Resolved).Field("thisShop", c.ThisShop).Field("otherOwn", c.OtherOwn)
                .Field("rival", c.Rival).Field("home", c.Home).Field("skipped", c.Skipped).Field("unconfirmed", c.Unconfirmed).Close();
        json.CloseArray().Close();
        json.OpenArray("offerEvidence");
        foreach (var (key, o) in Offers)
            json.Open().Field("businessId", key.Split('|')[0]).Field("productId", key.Split('|')[1]).Field("evaluated", o.Considered)
                .Field("feasible", o.Feasible).Field("chosen", o.Chosen).Close();
        json.CloseArray();
        json.Open("households").Field("ownerKind", OrdinaryVisits ? "person-wallet" : EconomyRun.OwnerKind).Field("owners", Owners).Field("arrearsOwners", ArrearsOwners)
            .Field("unfedDays", UnfedDays).Field("medianBalanceUnits", MedianBalanceUnits).Field("p10BalanceUnits", P10BalanceUnits)
            .Field("incomeReceivedUnits", IncomeReceivedUnits).Field("scheduledEmploymentUnits", ScheduledEmploymentUnits)
            .Field("employed", Employed).Field("jobLosses", JobLosses).Field("jobFinds", JobFinds).Field("missedActivities", MissedActivities).Close();
        json.Open("kinds").Open(OrdinaryVisits ? "allPersons" : "resident").Field("consumers", Owners).Field("own", OwnMeals).Field("rival", RivalMeals)
            .Field("home", HomeMeals).Field("skipped", Skipped).Close().Close();
        json.Open("accounting").Field("conservationChecks", ConservationChecks).Field("conserved", true)
            .Field("nativeOrders", NativeOrders).Field("nativeRevenueUnits", NativeRevenueUnits)
            .Field("confirmedPreparedUnits", ConfirmedPreparedUnits).Field("uncertainConfirmedUnits", UncertainConfirmedUnits)
            .Field("duplicateReceipts", DuplicateOrders).Field("historicalClockMisses", HistoricalClockMisses)
            .Field("outsideLedgerUnits", OutsideLedgerUnits).Field("watermarkedOutflowUnits", WatermarkedOutflowUnits)
            .Field("watermarkedOutflowWindowUnits", WatermarkedOutflowWindowUnits)
            .Field("ordinaryDiscretionaryUnits", OrdinaryDiscretionaryUnits).Field("ordinaryWindowUnits", OrdinaryWindowUnits)
            .Field("epochReadViolations", EpochReadViolations).Close();
        json.Close();
    }

    public static string Phase(int day) => day <= EconomyRun.BaselineDays ? "baseline"
        : day <= EconomyRun.BaselineDays + EconomyRun.InterventionDays ? "intervention" : "reverted";
}

public sealed class DecisionObserver
{
    private readonly SortedDictionary<int, Dictionary<string, SortedSet<string>>> considered = new();
    private readonly SortedDictionary<int, SortedDictionary<string, DayMetrics.OfferRow>> offers = new();
    private static readonly SortedSet<string> None = new(StringComparer.Ordinal);

    private static bool Own(string business) => business == EconomyFixture.Shop || business == EconomyFixture.FarShop;
    private static string Key(string consumer, MealKind kind) => consumer + "|" + (int)kind;

    public void Observe(FoodDecisionTrace trace)
    {
        var intent = trace.Intent;
        foreach (var choice in intent.Choices)
        {
            if (!Own(choice.BusinessId)) continue;
            if (!considered.TryGetValue(intent.Day, out var day)) considered[intent.Day] = day = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            string key = Key(trace.Consumer.Id, trace.MealKind);
            if (!day.TryGetValue(key, out var set)) day[key] = set = new SortedSet<string>(StringComparer.Ordinal);
            set.Add(choice.BusinessId);
            if (!offers.TryGetValue(intent.Day, out var rows)) offers[intent.Day] = rows = new SortedDictionary<string, DayMetrics.OfferRow>(StringComparer.Ordinal);
            string offer = choice.BusinessId + "|" + choice.ProductId;
            if (!rows.TryGetValue(offer, out var row)) rows[offer] = row = new DayMetrics.OfferRow();
            row.Considered++;
            if (choice.ScoreCents.HasValue) row.Feasible++;
            if (choice.Reason.StartsWith("Chosen:", StringComparison.Ordinal) && intent.Status == FoodIntentStatus.Travelling) row.Chosen++;
        }
    }
    public IEnumerable<string> ConsideredBy(int day, string consumer, MealKind kind)
        => considered.TryGetValue(day, out var d) && d.TryGetValue(Key(consumer, kind), out var set) ? set : None;
    public IEnumerable<KeyValuePair<string, DayMetrics.OfferRow>> OffersOn(int day)
        => offers.TryGetValue(day, out var rows) ? rows : Enumerable.Empty<KeyValuePair<string, DayMetrics.OfferRow>>();
    public void Prune(int day)
    {
        foreach (var d in considered.Keys.Where(k => k <= day).ToList()) considered.Remove(d);
        foreach (var d in offers.Keys.Where(k => k <= day).ToList()) offers.Remove(d);
    }
}

public sealed class LedgerViolation : Exception { public LedgerViolation(string message) : base(message) { } }
