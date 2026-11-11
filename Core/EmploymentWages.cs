using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>Optional logical closure evidence, shared by offline inputs and native adapters.</summary>
    public interface IWorkplaceAvailability
    {
        bool WorkplaceOpen(string placeId, double minute);
    }

    /// <summary>Person-owned shift accounting. No native payroll, clock, body or household pooling dependency.</summary>
    public static class EmploymentWages
    {
        // F5's persona schedule: four 480-minute shifts (Friday–Sunday are the weekend).
        public const int ScheduledWeeklyMinutes = 4 * 480;
        internal const int Working = 1, Closed = 2, Settled = 3, ClosureDay = 4, AfterMidnight = 5, AfterMidnightAttended = 6;
        public static bool Eligible(CitizenState person) => person.DayPlaceId.Length != 0
            && person.Identity.Age >= 18 && ResidentLifecycle.IsWorker(person);

        internal static bool Active(EmploymentAccrual a) => a.Shift == Working || a.Shift == Closed || AfterMidnightStage(a);
        // Derived from the saved employment tuple and F5 schedule, never saved as a second cursor.
        internal static double NextPosting(CitizenState person, int seed, double now, out WorkWindow selected,
            IReadOnlyDictionary<string, Place>? places = null)
        {
            selected = default;
            if (!Eligible(person)) return double.PositiveInfinity;
            var a = person.Accrual;
            WorkWindow? window;
            if (Active(a)) window = ActiveWindow(person, seed, places);
            else
            {
                window = DailyRoutine.WorkOnDay(person, seed, now, places);
                bool freshPartialShift = window.HasValue && a.Shift == 0 && now < window.Value.EndsAt
                    && person.JoinedMinute >= window.Value.StartsAt;
                if (!window.HasValue || window.Value.StartsAt < now && !freshPartialShift
                    || a.Shift != 0 && a.Day >= (int)(window.Value.StartsAt / 1440))
                    window = DailyRoutine.NextWork(person, seed, now, places);
            }
            if (!window.HasValue) return double.PositiveInfinity;
            selected = window.Value;
            return Active(a)
                ? a.Shift == Working && selected.EndsAt > (a.Day + 1) * 1440.0
                    ? (a.Day + 1) * 1440.0 : selected.EndsAt
                : Math.Max(now, selected.StartsAt);
        }

        internal static WorkWindow? ActiveWindow(CitizenState person, int seed, IReadOnlyDictionary<string, Place>? places = null)
        {
            var prepared = person.CommutePreparation;
            // F5 already saves the committed work window. Retain it across reload, including an
            // overnight window; a later routine lookup must not shorten an in-progress shift.
            if (prepared.PlaceId == person.DayPlaceId && (int)(prepared.WorkStartsAt / 1440) == person.Accrual.Day
                && prepared.WorkEndsAt - prepared.WorkStartsAt == 480)
                return new WorkWindow(person.DayPlaceId, Activity.Work, prepared.WorkStartsAt, prepared.WorkEndsAt);
            return DailyRoutine.WorkOnDay(person, seed, person.Accrual.Day * 1440.0, places);
        }

        private static bool AfterMidnightStage(EmploymentAccrual a) => a.Shift == AfterMidnight || a.Shift == AfterMidnightAttended;

        internal static void Begin(CitizenState person, WorkWindow window, bool open)
        {
            if (!Eligible(person) || window.Activity != Activity.Work || window.PlaceId != person.DayPlaceId) return;
            int day = (int)(window.StartsAt / 1440);
            var a = person.Accrual;
            if (a.Shift != 0 && a.Day >= day) return;
            int elapsed = Math.Max(0, day - a.Day);
            a.MissMask = elapsed >= 14 ? 0 : (a.MissMask << elapsed) & ((1 << 14) - 1);
            if (a.Shift == 0) a.UnpostedWage = Money.FromUnits(ScheduledWeeklyMinutes / 2);
            a.Day = day; a.Shift = open ? Working : Closed; a.AttendedMinutes = 0;
            person.Accrual = a;
        }

        internal static bool Attending(CitizenState p) => p.Phase == CitizenPhase.AtPlace
            && p.Activity == Activity.Work && p.PlaceId == p.DayPlaceId && p.MealOuting == null;
        // Arrival evidence is persisted already by F5. Retained events do not add attendance twice.
        internal static int CurrentMinutes(CitizenState p, WorkWindow window, double now)
        {
            if (!Attending(p)) return 0;
            double from = Math.Max(window.StartsAt, p.JoinedMinute);
            if (AfterMidnightStage(p.Accrual)) from = Math.Max(from, (p.Accrual.Day + 1) * 1440.0);
            if (p.RecentVisits.Count == 0) return 0;
            var visit = p.RecentVisits[p.RecentVisits.Count - 1];
            if (visit.Activity != Activity.Work || visit.PlaceId != window.PlaceId) return 0;
            from = Math.Max(from, visit.AtMinute);
            double until = Math.Min(now, Math.Min(window.EndsAt, p.ActivityUntilMinute));
            // The tuple stores whole attended minutes. Never pay a minute that was only partly attended.
            return Math.Max(0, (int)Math.Floor(until - from));
        }
        internal static void Leave(CitizenState live, CitizenState working, WorkWindow window, double now, bool force = false)
        {
            if (live.Accrual.Shift != Working && !AfterMidnightStage(live.Accrual)) return;
            if (!Attending(live) || !force && Attending(working) && live.PlaceId == working.PlaceId) return;
            var a = working.Accrual;
            a.AttendedMinutes = checked(a.AttendedMinutes + CurrentMinutes(live, window, now));
            working.Accrual = a;
        }

        /// <summary>One posting, including a midnight portion of an overnight shift. The watermark is published
        /// with the conserved credit. UnpostedWage holds the nonnegative division remainder in exact numerator
        /// units, biased by half a denominator: cumulative rounding to nearest unit without losing fractions.</summary>
        internal static void Settle(CitizenState person, ResidentBudget budget, WorkWindow window, double now)
        {
            var a = person.Accrual;
            if (!Eligible(person) || window.Activity != Activity.Work || window.PlaceId != person.DayPlaceId
                || a.Day != (int)(window.StartsAt / 1440) || !Active(a) || now <= window.StartsAt || now > window.EndsAt) return;
            bool end = now == window.EndsAt;
            if (a.Shift == Closed && !end) return;
            if (!end && (now != (a.Day + 1) * 1440.0 || AfterMidnightStage(a))) return;
            int minutes = a.Shift == Closed ? 0 : checked(a.AttendedMinutes + CurrentMinutes(person, window, now));
            long numerator = checked(Money.FromCents(budget.BaseIncomeCents).Units * 7 * minutes + a.UnpostedWage.Units);
            var wage = Money.FromUnits(numerator / ScheduledWeeklyMinutes);
            var carry = Money.FromUnits(numerator % ScheduledWeeklyMinutes);
            var ledger = budget.Ledger;
            var income = ledger.EmploymentIncome + wage;
            var balance = budget.Balance + wage;
            _ = budget.OpeningBalance + income + budget.IncomeReceived;
            HouseholdFinance.CheckCash(budget);
            int missedActivities = person.MissedActivities;
            ledger.EmploymentIncome = income;
            a.UnpostedWage = carry;
            if (end)
            {
                if (a.Shift == Closed) a.Shift = ClosureDay;
                else
                {
                    if (minutes > 0) a.MissMask &= ~1;
                    else if (a.Shift != AfterMidnightAttended)
                    { a.MissMask |= 1; missedActivities = checked(missedActivities + 1); }
                    a.Shift = Settled;
                }
                a.AttendedMinutes = minutes;
            }
            else { a.Shift = minutes > 0 ? AfterMidnightAttended : AfterMidnight; a.AttendedMinutes = 0; }
            budget.Ledger = ledger; budget.Balance = balance; person.Accrual = a; person.MissedActivities = missedActivities;
        }

        public static int RecentMisses(EmploymentAccrual a, int day)
        {
            int age = Math.Max(0, day - a.Day);
            int mask = age >= 14 ? 0 : a.MissMask << age & ((1 << 14) - 1), count = 0;
            while (mask != 0) { count += mask & 1; mask >>= 1; }
            return count;
        }
        public static double JobLossChance(EmploymentAccrual a, int day)
            => Math.Min(CityBalance.MaximumJobLossChance, CityBalance.DailyJobLossChance
                * (1 + CityBalance.JobLossPerMiss * RecentMisses(a, day)));
    }
}
