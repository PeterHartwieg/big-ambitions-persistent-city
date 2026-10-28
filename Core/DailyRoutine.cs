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
            => At(person, citySeed, minute, places, null, true);

        /// <summary>As At with the place list, with the resident's home already resolved by the caller
        /// (null when HomeId names no place), so no catalogue scan is needed.</summary>
        public static RoutineSlot At(CitizenState person, int citySeed, double minute, IReadOnlyList<Place> places, Place? home)
            => At(person, citySeed, minute, places, home, false);

        private static RoutineSlot At(CitizenState person, int citySeed, double minute, IReadOnlyList<Place> places, Place? home, bool findHome)
        {
            var slot = SelectRoutine(person, citySeed, minute, out int anchorDay);
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
                var committed = At(person, citySeed, Math.Max(0, person.ActivityUntilMinute - 0.001));
                if (committed.Activity == Activity.Leisure && committed.EndsAt == slot.EndsAt)
                    return new RoutineSlot(person.PlaceId, slot.Activity, slot.EndsAt);
            }
            if (findHome)
            {
                home = null;
                foreach (var place in places) if (place.Id == person.HomeId) { home = place; break; }
            }
            if (home == null) return slot;
            // The choice key is Identity.Id + ":leisure-day:" + anchorDay, hashed per venue as key + ":" + venue ID.
            uint key = StableRandom.Append(StableRandom.AppendDecimal(RoutineKeys.For(person, citySeed).LeisureChoiceDay, anchorDay), ':');
            string selected = DestinationChoice.Choose(places, home, PlaceKind.Leisure, key);
            return selected == "" ? slot : new RoutineSlot(selected, slot.Activity, slot.EndsAt);
        }

        // Absolute minutes follow Big Ambitions' Day * 1440 + Hour * 60 + Minute.
        // Home spans end at an actual activity, rather than at calendar/quiet-hour boundaries.
        public static RoutineSlot At(CitizenState person, int citySeed, double minute)
            => SelectRoutine(person, citySeed, minute, out _);

        private const double MaxMinute = 1000000000;

        private static RoutineSlot SelectRoutine(CitizenState person, int citySeed, double minute, out int anchorDay)
        {
            CheckMinute(minute);
            if (minute == MaxMinute) throw new ArgumentOutOfRangeException(nameof(minute), "The supported simulation clock is exhausted.");
            int day = (int)Math.Floor(minute / 1440);
            anchorDay = day;
            var keys = RoutineKeys.For(person, citySeed);
            int shift = keys.HourShift;
            var work = WorkOnDay(person, day, shift);
            if (work.HasValue && minute >= work.Value.StartsAt && minute < work.Value.EndsAt)
                return new RoutineSlot(work.Value.PlaceId, work.Value.Activity, Math.Min(MaxMinute, work.Value.EndsAt));
            var quiet = new DailyRhythm.Lookup(person, keys);
            bool late = person.Identity.Persona == Persona.NightOwl;
            double next = MaxMinute;
            // Yesterday supplies overnight leisure; a week ahead finds the next real activity
            // even for a worker with no optional venues and a three-day weekend.
            for (int offset = -1; offset <= 8; offset++)
            {
                int currentDay = day + offset;
                if (currentDay < 0) continue;
                double midnight = currentDay * 1440.0;
                if (midnight >= MaxMinute) break;
                var futureWork = WorkOnDay(person, currentDay, shift);
                if (futureWork.HasValue && futureWork.Value.StartsAt > minute && futureWork.Value.StartsAt < next)
                    next = futureWork.Value.StartsAt;
                bool weekend = Weekend(currentDay);
                double shopFrom = midnight + (late ? 1230 : weekend ? 660 : 1050) + shift;
                double shopUntil = Math.Min(MaxMinute, shopFrom + 45);
                if (person.ShopId.Length > 0 && shopUntil > minute && shopUntil > shopFrom && shopFrom < next
                    && (person.Identity.Persona == Persona.Foodie
                    || RoutineKeys.Draw(keys.ShopDay, currentDay) % 3 != 0))
                {
                    double until = quiet.OptionalEndsAt(shopFrom, shopUntil);
                    if (until > shopFrom && minute >= shopFrom && minute < until)
                    { anchorDay = currentDay; return new RoutineSlot(person.ShopId, Activity.Shop, until); }
                    if (until > shopFrom && shopFrom > minute) next = Math.Min(next, shopFrom);
                }
                double leisureFrom = midnight + (late ? 1320 : weekend ? 840 : 1140) + shift;
                double leisureUntil = Math.Min(MaxMinute, leisureFrom + (weekend ? 120 : 75));
                if (person.LeisureId.Length > 0 && leisureUntil > minute && leisureUntil > leisureFrom && leisureFrom < next
                    && (weekend || person.Identity.Persona == Persona.Sporty || late
                    || RoutineKeys.Draw(keys.LeisureDay, currentDay) % 2 == 0))
                {
                    double until = quiet.OptionalEndsAt(leisureFrom, leisureUntil);
                    if (until > leisureFrom && minute >= leisureFrom && minute < until)
                    { anchorDay = currentDay; return new RoutineSlot(person.LeisureId, Activity.Leisure, until); }
                    if (until > leisureFrom && leisureFrom > minute) next = Math.Min(next, leisureFrom);
                }
                // Later days cannot precede an activity already found in this day.
                // Avoid recomputing a week's hashes/windows at every ordinary event.
                if (currentDay >= day && next <= midnight + 1440) break;
            }
            return new RoutineSlot(person.HomeId, Activity.Home, next);
        }

        private static bool Weekend(int day) => ((day - 1) % 7 + 7) % 7 >= 4;
        private static void CheckMinute(double minute)
        {
            if (!Position.Finite(minute) || minute < 0 || minute > MaxMinute)
                throw new ArgumentOutOfRangeException(nameof(minute));
        }

        /// <summary>The fixed work/study window on this game day, independent of current attendance.</summary>
        public static WorkWindow? WorkOnDay(CitizenState person, int citySeed, double minute)
        {
            CheckMinute(minute);
            return WorkOnDay(person, (int)Math.Floor(minute / 1440), HourShift(person, citySeed));
        }

        private static int HourShift(CitizenState person, int citySeed) => RoutineKeys.For(person, citySeed).HourShift;

        private static WorkWindow? WorkOnDay(CitizenState person, int day, int shift)
        {
            if (Weekend(day) || person.DayPlaceId.Length == 0 || person.Identity.Persona == Persona.Retiree) return null;
            double start = day * 1440.0 + (person.Identity.Persona == Persona.NightOwl ? 720 : person.Identity.Persona == Persona.Parent ? 465 : 510) + shift;
            return new WorkWindow(person.DayPlaceId, person.Identity.Persona == Persona.Student ? Activity.Study : Activity.Work, start, start + 480);
        }

        /// <summary>The earliest future shift, including shifts after the three-day weekend.</summary>
        public static WorkWindow? NextWork(CitizenState person, int citySeed, double minute)
        {
            CheckMinute(minute);
            if (person.DayPlaceId.Length == 0 || person.Identity.Persona == Persona.Retiree) return null;
            int day = (int)Math.Floor(minute / 1440);
            int shift = HourShift(person, citySeed);
            double midnight = day * 1440.0;
            for (int offset = 0; offset <= 8; offset++)
            {
                double onDay = midnight + offset * 1440;
                if (onDay > MaxMinute) break;
                var work = WorkOnDay(person, day + offset, shift);
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
        // Hash states after Id + ":shop:", ":leisure:", ":quiet-day:" and ":leisure-day:".
        internal readonly uint ShopDay, LeisureDay, QuietDay, LeisureChoiceDay;

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
