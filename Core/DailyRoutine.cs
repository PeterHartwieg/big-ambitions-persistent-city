using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public readonly struct RoutineSlot
    {
        public RoutineSlot(string placeId, Activity activity, double endsAt)
        { PlaceId = placeId; Activity = activity; EndsAt = endsAt; }
        public string PlaceId { get; }
        public Activity Activity { get; }
        public double EndsAt { get; }
    }

    public interface ICommuteTimingPlanner : IRoutePlanner { }

    public readonly struct WorkWindow
    {
        public WorkWindow(string placeId, Activity activity, double startsAt, double endsAt)
        { PlaceId = placeId; Activity = activity; StartsAt = startsAt; EndsAt = endsAt; }
        public string PlaceId { get; }
        public Activity Activity { get; }
        public double StartsAt { get; }
        public double EndsAt { get; }
    }

    public static class DailyRoutine
    {
        public static RoutineSlot At(CitizenState person, int citySeed, double minute, IReadOnlyList<Place> places)
            => At(person, citySeed, minute, places, null, true, null);

        public static RoutineSlot At(CitizenState person, int citySeed, double minute, IReadOnlyList<Place> places, Place? home)
            => At(person, citySeed, minute, places, home, false, null);

        internal static RoutineSlot At(CitizenState person, int citySeed, double minute, IReadOnlyList<Place> places, Place? home,
            IReadOnlyDictionary<string, Place> index)
            => At(person, citySeed, minute, places, home, false, index);

        private static RoutineSlot At(CitizenState person, int citySeed, double minute, IReadOnlyList<Place> places, Place? home, bool findHome,
            IReadOnlyDictionary<string, Place>? index)
        {
            var slot = SelectRoutine(person, citySeed, minute, index, out int anchorDay, out bool chosen);
            if (slot.Activity != Activity.Leisure) return slot;
            if (person.ActivityUntilMinute == slot.EndsAt)
            {
                if (person.Phase != CitizenPhase.AtPlace && person.DestinationActivity == Activity.Leisure && person.DestinationId != "")
                    return new RoutineSlot(person.DestinationId, slot.Activity, slot.EndsAt);
            }
            if (person.Phase == CitizenPhase.AtPlace && person.Activity == Activity.Leisure && person.ActivityUntilMinute > 0)
            {
                var committed = SelectRoutine(person, citySeed, Math.Max(0, person.ActivityUntilMinute - 0.001), index, out _, out _);
                if (committed.Activity == Activity.Leisure && committed.EndsAt == slot.EndsAt)
                    return new RoutineSlot(person.PlaceId, slot.Activity, slot.EndsAt);
            }
            if (chosen) return slot;
            if (findHome)
            {
                home = null;
                foreach (var place in places) if (place.Id == person.HomeId) { home = place; break; }
            }
            if (home == null) return slot;
            return new RoutineSlot(DayPlanner.ChooseLeisure(RoutineKeys.For(person, citySeed), anchorDay, places, home, slot.PlaceId), slot.Activity, slot.EndsAt);
        }

        public static RoutineSlot At(CitizenState person, int citySeed, double minute)
            => SelectRoutine(person, citySeed, minute, null, out _, out _);

        internal static RoutineSlot At(CitizenState person, int citySeed, double minute, IReadOnlyDictionary<string, Place>? index)
            => SelectRoutine(person, citySeed, minute, index, out _, out _);

        private const double MaxMinute = 1000000000;

        private static RoutineSlot SelectRoutine(CitizenState person, int citySeed, double minute, IReadOnlyDictionary<string, Place>? index,
            out int anchorDay, out bool chosen)
        {
            CheckMinute(minute);
            if (minute == MaxMinute) throw new ArgumentOutOfRangeException(nameof(minute), "The supported simulation clock is exhausted.");
            int day = (int)Math.Floor(minute / 1440);
            anchorDay = day; chosen = false;
            var keys = RoutineKeys.For(person, citySeed);
            var quiet = new DailyRhythm.Lookup(person, keys);
            var today = DayPlanner.On(person, keys, day, index, ref quiet);
            var work = today.Work;
            if (work.HasValue && minute >= work.Value.StartsAt && minute < work.Value.EndsAt)
                return new RoutineSlot(work.Value.PlaceId, work.Value.Activity, Math.Min(MaxMinute, work.Value.EndsAt));
            if (day > 0 && minute < day * 1440.0 + 480 && !(person.Plan != null && person.Plan.Day >= day))
            {
                var overnight = DayPlanner.WorkFor(person, keys, day - 1, index);
                if (overnight.HasValue && minute >= overnight.Value.StartsAt && minute < overnight.Value.EndsAt)
                    return new RoutineSlot(overnight.Value.PlaceId, overnight.Value.Activity, Math.Min(MaxMinute, overnight.Value.EndsAt));
            }
            double next = MaxMinute;
            for (int offset = minute >= day * 1440.0 + DayPlanner.DayBoundaryMinute ? 0 : -1; offset <= 8; offset++)
            {
                int currentDay = day + offset;
                if (currentDay < 0) continue;
                double midnight = currentDay * 1440.0;
                if (midnight >= MaxMinute) break;
                var routine = offset == 0 ? today : DayPlanner.On(person, keys, currentDay, index, ref quiet);
                var futureWork = routine.Work;
                if (futureWork.HasValue && futureWork.Value.StartsAt > minute && futureWork.Value.StartsAt < next)
                    next = futureWork.Value.StartsAt;
                if (!routine.Present) continue;
                if (routine.HasShop)
                {
                    if (minute >= routine.ShopFrom && minute < routine.ShopUntil)
                    { anchorDay = currentDay; return new RoutineSlot(routine.ShopVenue, Activity.Shop, routine.ShopUntil); }
                    if (routine.ShopFrom > minute) next = Math.Min(next, routine.ShopFrom);
                }
                if (routine.HasLeisure)
                {
                    if (minute >= routine.LeisureFrom && minute < routine.LeisureUntil)
                    { anchorDay = currentDay; chosen = routine.LeisureChosen; return new RoutineSlot(routine.LeisureVenue, Activity.Leisure, routine.LeisureUntil); }
                    if (routine.LeisureFrom > minute) next = Math.Min(next, routine.LeisureFrom);
                }
                if (currentDay >= day && next <= midnight + 1440) break;
            }
            if (next == MaxMinute && person.Visitor?.Stay.Pattern == VisitorPattern.DayVisit) next = Math.Min(MaxMinute, (day + 9) * 1440.0);
            return new RoutineSlot(person.HomeId, Activity.Home, next);
        }

        private static void CheckMinute(double minute)
        {
            if (!Position.Finite(minute) || minute < 0 || minute > MaxMinute)
                throw new ArgumentOutOfRangeException(nameof(minute));
        }

        public static WorkWindow? WorkOnDay(CitizenState person, int citySeed, double minute) => WorkOnDay(person, citySeed, minute, null);

        internal static WorkWindow? WorkOnDay(CitizenState person, int citySeed, double minute, IReadOnlyDictionary<string, Place>? index)
        {
            CheckMinute(minute);
            return DayPlanner.WorkFor(person, RoutineKeys.For(person, citySeed), (int)Math.Floor(minute / 1440), index);
        }

        public static WorkWindow? NextWork(CitizenState person, int citySeed, double minute) => NextWork(person, citySeed, minute, null);

        internal static WorkWindow? NextWork(CitizenState person, int citySeed, double minute, IReadOnlyDictionary<string, Place>? index)
        {
            CheckMinute(minute);
            if (person.DayPlaceId.Length == 0 || person.Identity.Persona == Persona.Retiree) return null;
            int day = (int)Math.Floor(minute / 1440);
            var keys = RoutineKeys.For(person, citySeed);
            double midnight = day * 1440.0;
            for (int offset = 0; offset <= 8; offset++)
            {
                double onDay = midnight + offset * 1440;
                if (onDay > MaxMinute) break;
                var work = DayPlanner.WorkFor(person, keys, day + offset, index);
                if (work.HasValue && work.Value.StartsAt > minute && work.Value.EndsAt <= MaxMinute) return work;
            }
            return null;
        }

        public static double CommutePreparationAt(CitizenState person, int citySeed, WorkWindow window, double previousMinutes)
        {
            CheckMinute(window.StartsAt);
            if (!Position.Finite(previousMinutes) || previousMinutes < 0)
                throw new ArgumentOutOfRangeException(nameof(previousMinutes));
            var keys = RoutineKeys.For(person, citySeed);
            uint personal = keys.CommutePreparation;
            double ordinaryLead = 480 + personal % 361;
            double margin = 30 + keys.CommuteMargin % 61;
            double lead = Math.Max(ordinaryLead, previousMinutes + margin);
            return Math.Max(0, window.StartsAt - lead);
        }

        public static bool Open(Place place, double minute) => NextClose(place, minute).HasValue;

        public static double? NextClose(Place place, double minute)
        {
            var intervals = OpeningIntervals(place, minute);
            double? close = null;
            foreach (var interval in intervals)
            {
                if (close.HasValue)
                {
                    if (interval.From > close.Value) break;
                    close = Math.Max(close.Value, interval.To);
                }
                else if (interval.From <= minute && interval.To > minute) close = interval.To;
                else if (interval.From > minute) break;
            }
            return close.HasValue && close.Value >= minute + (place.WeeklyHours.Count != 0 ? 10080 : 1440) ? double.PositiveInfinity : close;
        }

        public static double? NextOpen(Place place, double minute, double beforeMinute)
        {
            if (!Position.Finite(beforeMinute) || beforeMinute < minute || beforeMinute > 1e9)
                throw new ArgumentOutOfRangeException(nameof(beforeMinute));
            foreach (var interval in OpeningIntervals(place, minute))
            {
                double opening = Math.Max(minute, interval.From);
                if (opening >= beforeMinute) break;
                if (interval.To > opening) return opening;
            }
            return null;
        }

        internal static OpeningInterval[] Intervals(Place place, double minute) => OpeningIntervals(place, minute);

        private static OpeningInterval[] OpeningIntervals(Place place, double minute)
        {
            if (place == null) throw new ArgumentNullException(nameof(place));
            if (!Position.Finite(minute) || minute < 0 || minute > 1e9) throw new ArgumentOutOfRangeException(nameof(minute));
            int day = (int)Math.Floor(minute / 1440);
            var cache = place.OpeningCache;
            if (cache == null || !cache.Matches(place)) cache = null;
            else if (cache.Day == day) return cache.Intervals;
            else if (cache.Previous != null && cache.PreviousDay == day) return cache.Previous;
            var intervals = BuildOpeningIntervals(place, day);
            place.OpeningCache = cache == null ? new OpeningCache(place, day, intervals) : cache.With(day, intervals);
            return intervals;
        }

        private static OpeningInterval[] BuildOpeningIntervals(Place place, int day)
        {
            bool weekly = place.WeeklyHours.Count != 0;
            var intervals = new List<OpeningInterval>();
            for (int offset = -1; offset <= 8; offset++)
            {
                int currentDay = day + offset;
                double midnight = currentDay * 1440.0;
                if (weekly)
                {
                    int weekday = ((currentDay - 1) % 7 + 7) % 7;
                    foreach (var window in place.WeeklyHours)
                        if (window.Day == weekday) AddInterval(intervals, midnight, window.FromMinute, window.ToMinute);
                }
                else AddInterval(intervals, midnight, place.OpenMinute, place.CloseMinute);
            }
            intervals.Sort((a, b) => { int from = a.From.CompareTo(b.From); return from != 0 ? from : a.To.CompareTo(b.To); });
            return intervals.ToArray();
        }

        private static void AddInterval(List<OpeningInterval> intervals, double midnight, int from, int to)
        {
            if (from == to) return;
            intervals.Add(new OpeningInterval(midnight + from, midnight + to + (to < from ? 1440 : 0)));
        }

        internal readonly struct OpeningInterval
        {
            public OpeningInterval(double from, double to) { From = from; To = to; }
            public double From { get; }
            public double To { get; }
        }
    }

    internal sealed class OpeningCache
    {
        private readonly int openMinute, closeMinute;
        private readonly List<OpeningWindow> source;
        private readonly int[] windows;
        internal readonly int Day, PreviousDay;
        internal readonly DailyRoutine.OpeningInterval[] Intervals;
        internal readonly DailyRoutine.OpeningInterval[]? Previous;

        internal OpeningCache(Place place, int day, DailyRoutine.OpeningInterval[] intervals)
        {
            openMinute = place.OpenMinute; closeMinute = place.CloseMinute; source = place.WeeklyHours;
            windows = new int[source.Count * 3];
            for (int i = 0; i < source.Count; i++)
            { var window = source[i]; windows[i * 3] = window.Day; windows[i * 3 + 1] = window.FromMinute; windows[i * 3 + 2] = window.ToMinute; }
            Day = day; Intervals = intervals; PreviousDay = 0; Previous = null;
        }

        private OpeningCache(OpeningCache hours, int day, DailyRoutine.OpeningInterval[] intervals, int previousDay, DailyRoutine.OpeningInterval[]? previous)
        {
            openMinute = hours.openMinute; closeMinute = hours.closeMinute; source = hours.source; windows = hours.windows;
            Day = day; Intervals = intervals; PreviousDay = previousDay; Previous = previous;
        }

        internal OpeningCache With(int day, DailyRoutine.OpeningInterval[] intervals) => new OpeningCache(this, day, intervals, Day, Intervals);

        internal bool Matches(Place place)
        {
            var hours = place.WeeklyHours;
            if (place.OpenMinute != openMinute || place.CloseMinute != closeMinute || !ReferenceEquals(hours, source)
                || hours.Count * 3 != windows.Length) return false;
            for (int i = 0; i < hours.Count; i++)
            {
                var window = hours[i];
                if (window.Day != windows[i * 3] || window.FromMinute != windows[i * 3 + 1] || window.ToMinute != windows[i * 3 + 2]) return false;
            }
            return true;
        }
    }

    internal sealed class RoutineKeys
    {
        internal readonly string Id;
        internal readonly int Seed;
        internal readonly int HourShift, QuietPersonal;
        internal readonly uint CommutePreparation, CommuteMargin, EmploymentTime;
        internal readonly uint ShopDay, LeisureDay, QuietDay, LeisureChoiceDay, VisitDay;

        private RoutineKeys(string id, int seed)
        {
            Id = id; Seed = seed;
            uint person = StableRandom.Append(StableRandom.Begin(seed), id);
            HourShift = (int)(Final(person, ":hours") % 31) - 15;
            QuietPersonal = (int)(Final(person, ":quiet-hours") % 91) - 45;
            CommutePreparation = Final(person, ":commute-preparation");
            CommuteMargin = Final(person, ":commute-margin");
            EmploymentTime = EmploymentTimeOf(id);
            ShopDay = StableRandom.Append(person, ":shop:");
            LeisureDay = StableRandom.Append(person, ":leisure:");
            QuietDay = StableRandom.Append(person, ":quiet-day:");
            LeisureChoiceDay = StableRandom.Append(person, ":leisure-day:");
            VisitDay = StableRandom.Append(person, ":visit:");
        }

        private static uint Final(uint state, string suffix) => StableRandom.Finish(StableRandom.Append(state, suffix));
        private static uint EmploymentTimeOf(string id) => Final(StableRandom.Append(StableRandom.Begin(0), id), ":employment-time");

        internal static RoutineKeys For(CitizenState person, int seed)
        {
            string id = person.Identity.Id ?? "";
            var keys = person.RoutineKeys;
            if (keys != null && keys.Seed == seed && string.Equals(keys.Id, id, StringComparison.Ordinal)) return keys;
            keys = new RoutineKeys(id, seed);
            person.RoutineKeys = keys;
            return keys;
        }

        internal static uint EmploymentTimeFor(CitizenState person)
        {
            string id = person.Identity.Id ?? "";
            var keys = person.RoutineKeys;
            return keys != null && string.Equals(keys.Id, id, StringComparison.Ordinal) ? keys.EmploymentTime : EmploymentTimeOf(id);
        }

        internal static uint Draw(uint prefix, int day) => StableRandom.Finish(StableRandom.AppendDecimal(prefix, day));
    }
}
