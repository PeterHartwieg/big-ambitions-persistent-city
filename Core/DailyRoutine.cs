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

    /// <summary>Opt in to planning work/study departures from committed route durations.</summary>
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

        /// <summary>As At with the place list, with the resident's home already resolved by the caller
        /// (null when HomeId names no place), so no catalogue scan is needed.</summary>
        public static RoutineSlot At(CitizenState person, int citySeed, double minute, IReadOnlyList<Place> places, Place? home)
            => At(person, citySeed, minute, places, home, false, null);

        /// <summary>The simulator's lookup: with the place index, so unplanned days fit work to the workplace's hours exactly
        /// as their plan will.</summary>
        internal static RoutineSlot At(CitizenState person, int citySeed, double minute, IReadOnlyList<Place> places, Place? home,
            IReadOnlyDictionary<string, Place> index)
            => At(person, citySeed, minute, places, home, false, index);

        private static RoutineSlot At(CitizenState person, int citySeed, double minute, IReadOnlyList<Place> places, Place? home, bool findHome,
            IReadOnlyDictionary<string, Place>? index)
        {
            var slot = SelectRoutine(person, citySeed, minute, index, out int anchorDay, out bool chosen);
            if (slot.Activity != Activity.Leisure) return slot;
            // A committed activity owns its venue until its deadline, including waits
            // at a closed entrance and reloads during its journey.
            if (person.ActivityUntilMinute == slot.EndsAt)
            {
                if (person.Phase != CitizenPhase.AtPlace && person.DestinationActivity == Activity.Leisure && person.DestinationId != "")
                    return new RoutineSlot(person.DestinationId, slot.Activity, slot.EndsAt);
            }
            // Venue closure shortens an attending resident's event deadline, but
            // does not select a second venue in the same leisure window. Comparing
            // routine windows also avoids retaining a late previous-day arrival.
            if (person.Phase == CitizenPhase.AtPlace && person.Activity == Activity.Leisure && person.ActivityUntilMinute > 0)
            {
                var committed = SelectRoutine(person, citySeed, Math.Max(0, person.ActivityUntilMinute - 0.001), index, out _, out _);
                if (committed.Activity == Activity.Leisure && committed.EndsAt == slot.EndsAt)
                    return new RoutineSlot(person.PlaceId, slot.Activity, slot.EndsAt);
            }
            // GR-2: a saved plan chose its leisure venue when it was drawn.
            if (chosen) return slot;
            if (findHome)
            {
                home = null;
                foreach (var place in places) if (place.Id == person.HomeId) { home = place; break; }
            }
            if (home == null) return slot;
            return new RoutineSlot(DayPlanner.ChooseLeisure(RoutineKeys.For(person, citySeed), anchorDay, places, home, slot.PlaceId), slot.Activity, slot.EndsAt);
        }

        // Absolute minutes follow Big Ambitions' Day * 1440 + Hour * 60 + Minute.
        // Home spans end at an actual activity, rather than at calendar/quiet-hour boundaries.
        public static RoutineSlot At(CitizenState person, int citySeed, double minute)
            => SelectRoutine(person, citySeed, minute, null, out _, out _);

        internal static RoutineSlot At(CitizenState person, int citySeed, double minute, IReadOnlyDictionary<string, Place>? index)
            => SelectRoutine(person, citySeed, minute, index, out _, out _);

        private const double MaxMinute = 1000000000;

        // GR-2: each day's windows come from the person's saved plan for that day, or from the same draws for a day not
        // planned yet (lookahead), so a Home span ends where the next plan will start an item.
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
            // A shift begun yesterday can run past midnight (eight hours at most). Once today's plan has replaced
            // yesterday's, every item of that plan has ended (DayPlanExecutor.Ensure), so its shift is over and is never
            // drawn again from hours that may have changed since it was committed.
            if (day > 0 && minute < day * 1440.0 + 480 && !(person.Plan != null && person.Plan.Day >= day))
            {
                var overnight = DayPlanner.WorkFor(person, keys, day - 1, index);
                if (overnight.HasValue && minute >= overnight.Value.StartsAt && minute < overnight.Value.EndsAt)
                    return new RoutineSlot(overnight.Value.PlaceId, overnight.Value.Activity, Math.Min(MaxMinute, overnight.Value.EndsAt));
            }
            double next = MaxMinute;
            // Yesterday supplies overnight leisure (ending by 00:15, so not after the day boundary); a week ahead finds the
            // next real activity even for a worker with no optional venues and a three-day weekend.
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
                // Later days cannot precede an activity already found in this day.
                // Avoid recomputing a week's hashes/windows at every ordinary event.
                if (currentDay >= day && next <= midnight + 1440) break;
            }
            // F5g: a pool member can draw no visit day inside this window. Wake it at the first unchecked midnight to look
            // further ahead instead of ending its Home span at the clock limit, which would leave it dormant for good.
            if (next == MaxMinute && person.Visitor?.Stay.Pattern == VisitorPattern.DayVisit) next = Math.Min(MaxMinute, (day + 9) * 1440.0);
            return new RoutineSlot(person.HomeId, Activity.Home, next);
        }

        private static void CheckMinute(double minute)
        {
            if (!Position.Finite(minute) || minute < 0 || minute > MaxMinute)
                throw new ArgumentOutOfRangeException(nameof(minute));
        }

        /// <summary>The fixed work/study window on this game day, independent of current attendance: the saved plan's for
        /// a planned day, else the shift template.</summary>
        public static WorkWindow? WorkOnDay(CitizenState person, int citySeed, double minute) => WorkOnDay(person, citySeed, minute, null);

        /// <summary>As WorkOnDay; with the place index an unplanned day fits the shift to the workplace's hours.</summary>
        internal static WorkWindow? WorkOnDay(CitizenState person, int citySeed, double minute, IReadOnlyDictionary<string, Place>? index)
        {
            CheckMinute(minute);
            return DayPlanner.WorkFor(person, RoutineKeys.For(person, citySeed), (int)Math.Floor(minute / 1440), index);
        }

        /// <summary>The earliest future shift, including shifts after the three-day weekend.</summary>
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

        /// <summary>A cheap, personal preparation deadline; no route or arrival guarantee is inferred.</summary>
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

        /// <summary>
        /// The end of the active union of opening windows, or null if closed. Positive infinity
        /// means continuously open. Weekly windows override legacy daily hours when present.
        /// </summary>
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

        /// <summary>First open instant within [minute, beforeMinute), or null if none remains.</summary>
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

        /// <summary>The opening intervals from the day before <paramref name="minute"/>'s day to a week after, sorted.</summary>
        internal static OpeningInterval[] Intervals(Place place, double minute) => OpeningIntervals(place, minute);

        private static OpeningInterval[] OpeningIntervals(Place place, double minute)
        {
            if (place == null) throw new ArgumentNullException(nameof(place));
            if (!Position.Finite(minute) || minute < 0 || minute > 1e9) throw new ArgumentOutOfRangeException(nameof(minute));
            int day = (int)Math.Floor(minute / 1440);
            // The windows for a day depend only on the day and the place's hours. The cache keeps an exact
            // copy of the hours it was built from and is rebuilt whenever any of them differ, so every
            // writer (a replaced list, an edited window, changed daily hours) is observed without a hook.
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
            // Yesterday supplies overnight windows; a full future week plus tomorrow allows us
            // to distinguish an actual closure from a schedule that remains open every week.
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

    /// <summary>Transient opening windows of one place for its two most recently built days, with an
    /// exact copy of the hours they were built from. Immutable: a change publishes a new instance.</summary>
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

    /// <summary>
    /// Per-resident routine hash inputs, each a pure function of Identity.Id and the city seed, computed
    /// once instead of concatenating and hashing a key string at every routine evaluation. The day-keyed
    /// draws keep the hash state after the fixed key prefix and continue it over the day's digits, which
    /// gives exactly StableRandom.Hash(prefix + day, seed).
    /// </summary>
    internal sealed class RoutineKeys
    {
        internal readonly string Id;
        internal readonly int Seed;
        internal readonly int HourShift, QuietPersonal;
        internal readonly uint CommutePreparation, CommuteMargin, EmploymentTime;
        // Hash states after Id + ":shop:", ":leisure:", ":quiet-day:", ":leisure-day:" and (F5g) ":visit:".
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
        // The employment check time is drawn with seed 0, independent of the city.
        private static uint EmploymentTimeOf(string id) => Final(StableRandom.Append(StableRandom.Begin(0), id), ":employment-time");

        internal static RoutineKeys For(CitizenState person, int seed)
        {
            // A null ID concatenates as empty, as the string keys did.
            string id = person.Identity.Id ?? "";
            var keys = person.RoutineKeys;
            if (keys != null && keys.Seed == seed && string.Equals(keys.Id, id, StringComparison.Ordinal)) return keys;
            keys = new RoutineKeys(id, seed);
            person.RoutineKeys = keys;
            return keys;
        }

        /// <summary>StableRandom.Hash(Identity.Id + ":employment-time", 0), from the cache when it is current.</summary>
        internal static uint EmploymentTimeFor(CitizenState person)
        {
            string id = person.Identity.Id ?? "";
            var keys = person.RoutineKeys;
            return keys != null && string.Equals(keys.Id, id, StringComparison.Ordinal) ? keys.EmploymentTime : EmploymentTimeOf(id);
        }

        /// <summary>The hash of a day-keyed draw: the prefix state continued over the day's invariant digits.</summary>
        internal static uint Draw(uint prefix, int day) => StableRandom.Finish(StableRandom.AppendDecimal(prefix, day));
    }
}
