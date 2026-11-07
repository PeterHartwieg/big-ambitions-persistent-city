using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace PersistentCity.Core
{
    /// <summary>Facts-only projections, requested by the open panel or diagnostics. Never reads the market.</summary>
    public static class FoodFeedbackV2
    {
        public const int ShopsPerPage = 16;
        public const string ObservationLabel = "Changes are recorded when residents next check the shop; hours use the next schedule refresh. Times are first observed.";
        private static readonly string[] Periods = { "Legacy", "Breakfast", "Lunch", "Dinner" };
        private static readonly string[] Changes = { "price", "stock", "staffed counters", "hours" };
        private static readonly string[] Causes = { "no staffed counter", "stock unavailable", "service/queue", "closed/late" };
        public static int RecordingSince(FoodCommerceState food)
        {
            // Fresh scopes set the choice-policy boundary to creation day + 1. History already records the creation day.
            int since = Math.Max(0, food.ChoiceStartsDay - 1);
            foreach (var row in food.History) if (row.Day < since) since = row.Day;
            return since;
        }
        public static void AppendPeriods(StringBuilder text, IReadOnlyList<FoodShopDay> rows, int today, int recordingSince)
        {
            Span<long> now = stackalloc long[16]; now.Clear();
            Span<long> last = stackalloc long[16]; last.Clear();
            int stockoutDays = 0; bool capped = false;
            foreach (var row in rows)
            {
                if (row.Day < today - 13 || row.Day > today) continue;
                var target = row.Day >= today - 6 ? now : last;
                for (int i = 0; i < 16; i++) { target[i] += row.PeriodCauses.Counts[i]; if (row.PeriodCauses.Counts[i] == MealPeriodCauses.MaximumCount) capped = true; }
                if (row.StockoutObserved) stockoutDays++;
            }
            bool partial = today - 13 < recordingSince;
            text.Append("Meal-period evaluated offers · this week / last week");
            if (partial) text.Append(" · partial: city recording began day ").Append(recordingSince);
            text.AppendLine(" (today so far)");
            if (capped) text.AppendLine("Counters at the storage limit are lower bounds; changes withheld.");
            for (int period = 0; period < 4; period++)
                for (int cause = 0; cause < 4; cause++)
                {
                    int i = period * 4 + cause; if (now[i] == 0 && last[i] == 0) continue;
                    text.Append(Periods[period]).Append(": ").Append(capped ? "at least " : "").Append(now[i]).Append(" evaluated offers found ").Append(Causes[cause])
                        .Append(" (last week ").Append(last[i]).Append(')');
                    if (!partial && !capped) text.Append(" · change ").Append(Signed(now[i] - last[i]));
                    text.AppendLine();
                }
            text.Append("Observed stock-out days in retained 14 days: ").Append(stockoutDays).AppendLine(" (observed refusals only).");
        }
        public static void AppendJournal(StringBuilder text, ShopObservation? shop, IReadOnlyList<FoodShopDay> rows,
            int today, int recordingSince, Func<string, string>? productName = null)
        {
            text.AppendLine(ObservationLabel);
            if (shop == null || shop.Changes.Count == 0) { text.AppendLine("No changes observed yet."); return; }
            if (shop.Evicted > 0) text.Append("Older journal entries evicted: ").Append(shop.Evicted).AppendLine(".");
            for (int entry = 0; entry < shop.Changes.Count; entry++)
            {
                var change = shop.Changes[entry];
                int day = change.ObservedMinute / 1440, clock = change.ObservedMinute % 1440;
                text.Append("First observed day ").Append(day).Append(", ").Append((clock / 60).ToString("00", CultureInfo.InvariantCulture))
                    .Append(':').Append((clock % 60).ToString("00", CultureInfo.InvariantCulture)).Append(": ");
                if (change.Kind == ShopChangeKind.Price || change.Kind == ShopChangeKind.Stock)
                    text.Append(productName?.Invoke(change.ProductId!) ?? change.ProductId).Append(' ');
                text.Append(Changes[(int)change.Kind]);
                if (change.Kind == ShopChangeKind.Hours) text.Append(" weekday ").Append(change.Subject / 128 + 1).Append(" window ").Append(change.Subject % 128 / 2 + 1)
                    .Append(change.Subject % 2 == 0 ? " opens" : " closes");
                text.Append(' ').Append(Value(change, change.Before)).Append(" → ").Append(Value(change, change.After)).AppendLine(".");
                bool mixed = shop.Evicted > 0;
                for (int other = 0; other < shop.Changes.Count; other++)
                {
                    int otherDay = shop.Changes[other].ObservedMinute / 1440;
                    if (other != entry && otherDay >= day - 7 && otherDay <= day + 6) mixed = true;
                }
                long before = 0, after = 0;
                foreach (var row in rows)
                {
                    if (row.Day >= day - 7 && row.Day < day && row.Day >= today - 13) before += row.Purchases;
                    if (row.Day >= day && row.Day <= day + 6 && row.Day <= today && row.Day >= today - 13) after += row.Purchases;
                }
                bool partial = day - 7 < Math.Max(recordingSince, today - 13) || day + 6 > today;
                text.Append("Purchases, 7 days before / from observation day (today so far): ").Append(before).Append(" / ").Append(after);
                if (partial) text.Append(" · partial; change pending complete retained windows");
                else text.Append(" · change ").Append(Signed(after - before));
                if (mixed) text.Append(shop.Evicted > 0 ? " · mixed (older changes evicted)" : " · mixed (overlapping changes)");
                text.AppendLine();
            }
        }
        private static string Signed(long n) => (n >= 0 ? "+" : "") + n.ToString(CultureInfo.InvariantCulture);
        private static string Value(ShopChange change, long n) => change.Kind == ShopChangeKind.Price
            ? ((decimal)n / Money.UnitsPerDollar).ToString("$0.00########", CultureInfo.InvariantCulture)
            : change.Kind == ShopChangeKind.Hours ? n == 1441 ? "absent" : (n / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (n % 60).ToString("00", CultureInfo.InvariantCulture)
            : n.ToString(CultureInfo.InvariantCulture);
        public static void AppendCity(StringBuilder text, FoodMoneySnapshot money, EconomicLedger economy, int today)
        {
            // F6e already maintains these exact budget flags, including attendance wage changes. B4 uses person wallets.
            text.Append("Household strain (person wallets, current): ").Append(money.BehindOnBills).Append('/').Append(money.Budgets)
                .Append(" behind on bills · ").Append(money.NoSpareMoney).Append('/').Append(money.Budgets)
                .AppendLine(" with no spare money for prepared food.");
            BigInteger now = 0, last = 0;
            foreach (var d in economy.Outside)
            {
                if (d.Day < today - 13 || d.Day > today) continue;
                if (d.Day >= today - 6) now += d.RetailUnits + d.LeisureUnits; else last += d.RetailUnits + d.LeisureUnits;
            }
            text.Append("Spending outside your businesses (modeled non-food shops/leisure): ").Append(FoodFeedback.Amount(now))
                .Append(" this week · ").Append(FoodFeedback.Amount(last)).AppendLine(" last week.");
        }
    }
}
