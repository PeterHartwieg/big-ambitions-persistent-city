using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace PersistentCity.Core
{
    /// <summary>Own-ledger totals only. No business identity, native receipt or market denominator.
    /// Recorded in the same atomic operation as D2's debit; outcomes never feed this history.</summary>
    public sealed class RivalOutflowHistory
    {
        public const int Days = 14;
        internal int StartDay, ThroughDay;
        internal bool PartialStart;
        internal BigInteger Baseline, ExpiredUnits, ExpiredCount;
        private RivalOutflowView? cachedView;
        private int cachedDay = -1;
        internal readonly List<Row> Rows = new List<Row>(Days);
        internal sealed class Row
        {
            internal int Day;
            internal BigInteger Units, Count;
        }
        public static RivalOutflowHistory Begin(FoodCommerceState food, double minute)
        {
            var baseline = Ledger(food);
            return new RivalOutflowHistory { StartDay = (int)(minute / 1440), ThroughDay = (int)(minute / 1440),
                // A preexisting undated ledger can include a boundary-minute debit. Midnight
                // alone cannot prove this first day is fully covered by the new history.
                PartialStart = minute % 1440 != 0 || baseline > 0, Baseline = baseline };
        }
        public static BigInteger Ledger(FoodCommerceState food)
        {
            BigInteger units = 0;
            foreach (var meal in food.Residents) units += (meal.Budget?.ExternalFoodSpent ?? meal.LegacyExternalFoodSpent).Units;
            return units;
        }
        public void Advance(int day)
        {
            if (day <= ThroughDay) return;
            cachedView = null;
            ThroughDay = day;
            int remove = 0;
            foreach (var row in Rows)
            {
                if (row.Day >= day - Days + 1) break;
                ExpiredUnits += row.Units; ExpiredCount += row.Count; remove++;
            }
            if (remove > 0) Rows.RemoveRange(0, remove);
        }
        internal void Record(Money price, int settlementDay)
        {
            cachedView = null;
            Advance(settlementDay);
            if (settlementDay < ThroughDay - Days + 1)
            { ExpiredUnits += price.Units; ExpiredCount++; return; }
            int at = 0;
            while (at < Rows.Count && Rows[at].Day < settlementDay) at++;
            if (at == Rows.Count || Rows[at].Day != settlementDay) Rows.Insert(at, new Row { Day = settlementDay });
            Rows[at].Units += price.Units; Rows[at].Count++;
        }
        public bool Reconciles(FoodCommerceState food)
        {
            BigInteger total = Baseline + ExpiredUnits;
            foreach (var row in Rows) total += row.Units;
            return total == Ledger(food);
        }
        // Fixed-size scalar wire: six header cells, then at most fourteen day,units,count cells.
        public string ToWire()
        {
            var text = new StringBuilder().Append(StartDay).Append('|').Append(PartialStart ? 1 : 0)
                .Append('|').Append(ThroughDay).Append('|').Append(Baseline.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(ExpiredUnits.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(ExpiredCount.ToString(CultureInfo.InvariantCulture));
            foreach (var row in Rows) text.Append('|').Append(row.Day).Append(',')
                .Append(row.Units.ToString(CultureInfo.InvariantCulture)).Append(',').Append(row.Count.ToString(CultureInfo.InvariantCulture));
            return text.ToString();
        }
        public static RivalOutflowHistory FromWire(string wire)
        {
            if (wire == null || wire.Length > 1024) throw new FormatException("Outflow wire exceeds bound.");
            var fields = wire.Split('|');
            if (fields.Length < 6 || fields.Length > 6 + Days) throw new FormatException("Outflow arity.");
            BigInteger Number(string value)
            {
                if (value.Length == 0 || value.Length > 26 || value.Length > 1 && value[0] == '0') throw new FormatException("Outflow number.");
                foreach (char c in value) if (c < '0' || c > '9') throw new FormatException("Outflow number.");
                return BigInteger.Parse(value, CultureInfo.InvariantCulture);
            }
            var result = new RivalOutflowHistory { StartDay = checked((int)Number(fields[0])),
                ThroughDay = checked((int)Number(fields[2])), Baseline = Number(fields[3]),
                ExpiredUnits = Number(fields[4]), ExpiredCount = Number(fields[5]) };
            if (fields[1] != "0" && fields[1] != "1") throw new FormatException("Outflow partial flag.");
            result.PartialStart = fields[1] == "1";
            int previous = -1;
            for (int i = 6; i < fields.Length; i++)
            {
                var cells = fields[i].Split(',');
                if (cells.Length != 3) throw new FormatException("Outflow day arity.");
                var row = new Row { Day = checked((int)Number(cells[0])), Units = Number(cells[1]), Count = Number(cells[2]) };
                if (row.Day <= previous || row.Day < result.StartDay || row.Day < result.ThroughDay - Days + 1
                    || row.Day > result.ThroughDay || row.Units <= 0 || row.Count <= 0
                    || row.Units < row.Count || row.Units > row.Count * Money.FromCents(Money.MaximumNativePriceCents).Units)
                    throw new FormatException("Outflow day bounds/order.");
                previous = row.Day; result.Rows.Add(row);
            }
            if (result.StartDay < 0 || result.ThroughDay < result.StartDay || result.ThroughDay > 694444
                || result.ExpiredCount > 0 && result.ThroughDay < result.StartDay + Days
                || (result.ExpiredUnits == 0) != (result.ExpiredCount == 0)
                || result.ExpiredUnits < result.ExpiredCount
                || result.ExpiredUnits > result.ExpiredCount * Money.FromCents(Money.MaximumNativePriceCents).Units)
                throw new FormatException("Outflow header bounds.");
            return result;
        }
        public int RecordCount => 1 + Rows.Count;
        public bool ValidAt(FoodCommerceState food, double minute)
        {
            if (ThroughDay != (int)(minute / 1440) || !Reconciles(food)) return false;
            BigInteger count = ExpiredCount, purchases = 0;
            foreach (var row in Rows) count += row.Count;
            foreach (var meal in food.Residents) purchases += meal.Purchases;
            return count <= purchases;
        }
        public RivalOutflowView Capture(int day)
        {
            if (cachedView != null && cachedDay == day) return cachedView;
            var days = new RivalOutflowDay[Days];
            BigInteger total = 0, count = 0;
            int at = 0;
            for (int i = 0; i < Days; i++)
            {
                int date = day - Days + 1 + i;
                while (at < Rows.Count && Rows[at].Day < date) at++;
                var row = at < Rows.Count && Rows[at].Day == date ? Rows[at] : null;
                bool known = date >= StartDay && date >= 0;
                days[i] = new RivalOutflowDay(date, known, known && date == StartDay && PartialStart,
                    row?.Units ?? BigInteger.Zero, row?.Count ?? BigInteger.Zero);
                total += days[i].Units; count += days[i].Count;
            }
            cachedDay = day;
            return cachedView = new RivalOutflowView(days, total, count);
        }
    }
    public sealed class RivalOutflowDay
    {
        public int Day { get; }
        public bool Known { get; }
        public bool Partial { get; }
        public BigInteger Units { get; }
        public BigInteger Count { get; }
        internal RivalOutflowDay(int day, bool known, bool partial, BigInteger units, BigInteger count)
        { Day = day; Known = known; Partial = partial; Units = units; Count = count; }
    }
    public sealed class RivalOutflowView
    {
        private string? text;
        public IReadOnlyList<RivalOutflowDay> Days { get; }
        public BigInteger Units { get; }
        public BigInteger Count { get; }
        internal RivalOutflowView(RivalOutflowDay[] days, BigInteger units, BigInteger count)
        { Days = Array.AsReadOnly(days); Units = units; Count = count; }
        public string Text()
        {
            if (this.text != null) return this.text;
            var text = new StringBuilder("Rival food outflow · last 14 days including today: ");
            text.Append(Amount(Units)).Append(" in ").Append(Count.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" recorded purchases (available history only; unknown days excluded). Residents spent this at rivals rather than player shops; this is modeled spending, not native rival sales or proven lost player sales. Availability estimated from advertised price/hours; native stock and capacity unknown.");
            foreach (var day in Days)
            {
                if (day.Day < 0) continue;
                text.Append("Day ").Append(day.Day.ToString(CultureInfo.InvariantCulture)).Append(": ");
                if (!day.Known) text.AppendLine("unknown (history not recorded)");
                else text.Append(Amount(day.Units)).Append(" · ").Append(day.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(" purchases").AppendLine(day.Partial ? " (partial day; earlier spending unknown)" : "");
            }
            return this.text = text.ToString();
        }
        private static string Amount(BigInteger units)
        {
            var whole = BigInteger.DivRem(units, Money.UnitsPerDollar, out var remainder);
            // 1 / (100 * 2^28) dollars has an exact thirty-place decimal expansion.
            string fraction = (remainder * BigInteger.Pow(5, 28)).ToString(CultureInfo.InvariantCulture).PadLeft(30, '0').TrimEnd('0');
            return "$" + whole.ToString(CultureInfo.InvariantCulture) + (fraction.Length == 0 ? "" : "." + fraction);
        }
    }
    public sealed partial class CitySimulator
    {
        // Called only on the existing panel's three-second text refresh or explicit bridge request.
        public RivalOutflowView? FreeRivalOutflow() => State.Food?.Outflow?.Capture((int)(State.Minute / 1440));
    }
}

