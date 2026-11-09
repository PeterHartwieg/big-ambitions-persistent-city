using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using PersistentCity.Core;
using PersistentCity.EconomyInterventions;

namespace PersistentCity.Tests.Acceptance;

/// <summary>Decision 3's oracles. Each failure throws <see cref="CityViolation"/>, so a scenario never ends with a broken
/// invariant: identity at every drained hour, conservation of every wallet, the city ledger and the native receipts,
/// research charges backed by delivered reports, vendor sales inside the lease, and every scheduled input published once at
/// its minute. <see cref="Equal"/> compares an arm with the reference: event ledger, saves at day boundaries, native orders.</summary>
internal static class CityOracles
{
    public static string M(double minute) => minute.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>Every ID seen in the run and the entity it named, so a reused ID for another entity is caught.</summary>
    internal sealed class Identity
    {
        public readonly Dictionary<string, string> Seen = new(StringComparer.Ordinal);
        public void Record(string kind, string id, string entity, string context)
        {
            string key = kind + ":" + id;
            if (Seen.TryGetValue(key, out var was) && was != entity)
                throw new CityViolation(context + ": " + kind + " ID " + id + " reused for another entity (" + was + " -> " + entity + ")");
            Seen[key] = entity;
        }
    }

    /// <summary>The ID sets at a drained hour as one ledger row (their digest and counts), after checking no ID changed entity.
    /// Households are homes with their residents; vendor rows are the business identity of each generation.</summary>
    public static string Checkpoint(CityState s, Identity seen, long epoch, string context)
    {
        var persons = new List<string>(s.Citizens.Count + s.Visitors.Count);
        foreach (var c in s.Citizens) { seen.Record("person", c.Identity.Id, "resident|" + c.Identity.Name + "|" + c.HomeId, context); persons.Add(c.Identity.Id); }
        foreach (var v in s.Visitors) { seen.Record("person", v.Id, "visitor|" + v.Stay.Pattern, context); persons.Add(v.Id); }
        if (persons.Count != persons.Distinct(StringComparer.Ordinal).Count()) throw new CityViolation(context + ": duplicate person ID");
        var households = s.Citizens.GroupBy(c => c.HomeId, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
        foreach (var h in households)
            seen.Record("household", h.Key, string.Join(",", h.Select(c => c.Identity.Id).OrderBy(i => i, StringComparer.Ordinal)), context);
        foreach (var p in s.Places) seen.Record("place", p.Id, p.Kind + "|" + p.Name, context);
        foreach (var r in s.Vendors.Rows) seen.Record("vendor", r.BusinessId, r.VehicleId + "|" + r.PlaceId, context);
        var research = s.Research.Open.Concat(s.Research.Kept).Concat(s.Research.Closed).ToList();
        foreach (var o in research) seen.Record("research", o.Id, o.Product.Code + "|" + o.BusinessAddress + "|" + M(o.OrderMinute), context);
        var text = new StringBuilder();
        foreach (var id in persons.OrderBy(i => i, StringComparer.Ordinal)) text.Append(id).Append(',');
        text.Append('|'); foreach (var h in households) text.Append(h.Key).Append(',');
        text.Append('|'); foreach (var p in s.Places.Select(p => p.Id).OrderBy(i => i, StringComparer.Ordinal)) text.Append(p).Append(',');
        text.Append('|'); foreach (var r in s.Vendors.Rows.Select(r => r.BusinessId + "=" + r.State).OrderBy(i => i, StringComparer.Ordinal)) text.Append(r).Append(',');
        text.Append('|'); foreach (var o in research.Select(o => o.Id + "=" + o.State).OrderBy(i => i, StringComparer.Ordinal)) text.Append(o).Append(',');
        BigInteger income = 0, essentials = 0, home = 0, prepared = 0, discretionary = 0;
        foreach (var b in s.Food!.Residents.Select(m => m.Budget).Where(b => b != null))
        {
            income += b!.IncomeReceived.Units + b.Ledger.EmploymentIncome.Units; essentials += b.EssentialsPaid.Units; home += b.HomeFoodPaid.Units;
            prepared += b.PreparedFoodSpent.Units; discretionary += b.Ledger.Discretionary.Units;
        }
        return "h|" + epoch.ToString(CultureInfo.InvariantCulture) + "|" + persons.Count + "|" + households.Count + "|" + s.Places.Count + "|" + s.Vendors.Rows.Count
            + "|" + research.Count + "|" + Sha(text.ToString()) + "|" + income + "|" + essentials + "|" + home + "|" + prepared + "|" + discretionary;
    }

    /// <summary>Per wallet CashConserved and the BigInteger city ledger (F5g), extended to native money: PC's confirmed
    /// prepared-food spending plus the confirmed price of every still-uncertain meal equals what the shop double and the
    /// Street Food stand-in booked. No duplicate native order and no historical checkout.</summary>
    public static void Conserved(CityState s, CityNative native, string context)
    {
        BigInteger sources = 0, uses = 0, prepared = 0, uncertain = 0;
        foreach (var meal in s.Food!.Residents)
        {
            var b = meal.Budget ?? throw new CityViolation(context + ": consumer without a wallet " + meal.ResidentId);
            bool conserved;
            try { conserved = HouseholdFinance.CashConserved(b); } catch (OverflowException) { conserved = false; }
            if (!conserved) throw new CityViolation(context + ": wallet not conserved for " + meal.ResidentId);
            sources += Money.TotalUnits(new[] { b.OpeningBalance, b.IncomeReceived, b.Ledger.EmploymentIncome });
            uses += Money.TotalUnits(new[] { b.Balance, b.EssentialsPaid, b.HomeFoodPaid, b.PreparedFoodSpent, b.ExternalFoodSpent, b.Ledger.Discretionary, b.HeldPurchase });
            prepared += b.PreparedFoodSpent.Units;
            if (meal.Status == MealStatus.Uncertain && native.Markets!.IsConfirmedPaid(meal)) uncertain += meal.Price.Units;
        }
        if (sources != uses) throw new CityViolation(context + ": city ledger " + sources + " != " + uses);
        if (native.Shops.Duplicates != 0) throw new CityViolation(context + ": duplicate native order");
        if (native.Shops.HistoricalCheckouts != 0) throw new CityViolation(context + ": historical native checkout");
        BigInteger booked = native.Shops.RevenueUnits;
        foreach (var sale in native.Api.Sales) booked += Money.FromCents(sale.Cents).Units;
        if (prepared + uncertain != booked)
            throw new CityViolation(context + ": PC confirmed receipts " + (prepared + uncertain) + " != native sales " + booked);
    }

    /// <summary>Every tagged agency charge belongs to exactly one Paid order with its report, every Paid order has exactly one
    /// charge at its product's price, and native money moved by exactly the charges.</summary>
    public static void Research(ResearchLedger ledger, CityResearchPort port, string context)
    {
        var charged = new Dictionary<string, int>(StringComparer.Ordinal);
        float total = 0;
        foreach (var record in port.Queue)
        {
            var order = ledger.Find(record.Tag);
            if (order == null || order.State != ResearchOrderState.Paid || order.Report == null)
                throw new CityViolation(context + ": research charge " + record.Tag + " without a delivered report");
            if (record.Amount != -order.Product.PriceDollars) throw new CityViolation(context + ": research charge " + record.Tag + " at another price");
            charged[record.Tag!] = charged.TryGetValue(record.Tag!, out var n) ? n + 1 : 1;
            total += record.Amount;
        }
        foreach (var (tag, count) in charged) if (count != 1) throw new CityViolation(context + ": research order " + tag + " charged " + count + " times");
        foreach (var order in ledger.Kept) if (!charged.ContainsKey(order.Id)) throw new CityViolation(context + ": paid research order " + order.Id + " without a charge");
        if (port.Money != CityResearchPort.OpeningMoney + total) throw new CityViolation(context + ": native money moved by more than the research charges");
    }

    /// <summary>Every Street Food sale was made while the stand-in's lease was valid and the vehicle was PC's, and sequences are unique.</summary>
    public static void Vendor(CityVendorApi api, string context)
    {
        var sequences = new HashSet<long>();
        foreach (var sale in api.Sales)
        {
            if (sale.Minute >= sale.LeaseUntil || sale.Owner != VendorNames.Owner)
                throw new CityViolation(context + ": vendor sale outside its lease at " + sale.Minute + " (lease until " + sale.LeaseUntil + ", owner '" + sale.Owner + "')");
            if (!sequences.Add(sale.Sequence)) throw new CityViolation(context + ": duplicate vendor sale " + sale.Sequence);
        }
    }

    /// <summary>Every input stamped before <paramref name="through"/> was published exactly once, at its own minute.</summary>
    public static void Schedule(InputSchedule schedule, IReadOnlyList<(long Epoch, InterventionInput Input)> published, long through, string context)
    {
        var due = schedule.Inputs.Where(i => i.Minute < through).ToList();
        var done = published.Where(p => p.Epoch < through).ToList();
        if (done.Any(p => p.Epoch != (long)p.Input.Minute)) throw new CityViolation(context + ": input published at another minute");
        if (!due.SequenceEqual(done.Select(p => p.Input)))
        {
            var missing = due.Except(done.Select(p => p.Input)).FirstOrDefault();
            throw new CityViolation(context + ": scheduled input " + (missing?.Json() ?? "published twice") + " skipped");
        }
    }

    /// <summary>The arm equals the reference: the same event ledger in the same order, the same save bytes at every drained
    /// day boundary, the same native orders and vendor sales, and the same final save.</summary>
    public static void Equal(CityRun reference, CityRun arm)
    {
        int common = Math.Min(reference.Ledger.Count, arm.Ledger.Count);
        for (int i = 0; i < common; i++)
            if (reference.Ledger[i] != arm.Ledger[i])
                throw new CityViolation(arm.Arm + ": event " + i + " differs: reference '" + reference.Ledger[i] + "', arm '" + arm.Ledger[i] + "'");
        if (reference.Ledger.Count != arm.Ledger.Count)
            throw new CityViolation(arm.Arm + ": " + arm.Ledger.Count + " events, reference " + reference.Ledger.Count);
        if (!reference.DaySaves.Select(d => d.Boundary).SequenceEqual(arm.DaySaves.Select(d => d.Boundary)))
            throw new CityViolation(arm.Arm + ": different day boundaries");
        for (int i = 0; i < reference.DaySaves.Count; i++)
            if (reference.DaySaves[i].Save != arm.DaySaves[i].Save)
                throw new CityViolation(arm.Arm + ": save at day boundary " + reference.DaySaves[i].Boundary + " differs");
        if (!reference.Orders.SequenceEqual(arm.Orders)) throw new CityViolation(arm.Arm + ": native orders differ");
        if (!reference.VendorSales.Select(Sale).SequenceEqual(arm.VendorSales.Select(Sale))) throw new CityViolation(arm.Arm + ": vendor sales differ");
        if (reference.Final != arm.Final) throw new CityViolation(arm.Arm + ": final save differs");
    }

    /// <summary>A sale without the stand-in's lease end: a reload arm renews the transient lease at other minutes.</summary>
    private static string Sale(CityVendorApi.Sale s) => s.Minute + "|" + s.Vehicle + "|" + s.Opening + "|" + s.Item + "|" + s.Cents + "|" + s.Sequence + "|" + s.Owner;

    public static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
