using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;

namespace PersistentCity.Tests;

/// <summary>
/// The routine, opening-hours, destination-choice and road-tag helpers exactly as they were before the
/// 28 October core identity change (string keys hashed per call, a sorted list per opening query, a
/// Split per road tag). CoreIdentityTests compares the production helpers with these on realistic
/// residents and places; any divergence is a behaviour change, not a refactor.
/// </summary>
internal static class CoreIdentityReference
{
    public static RoutineSlot At(CitizenState person, int citySeed, double minute, IReadOnlyList<Place> places)
    {
        var slot = SelectRoutine(person, citySeed, minute, out int anchorDay);
        if (slot.Activity != Activity.Leisure) return slot;
        if (person.ActivityUntilMinute == slot.EndsAt)
        {
            if (person.Phase != CitizenPhase.AtPlace && person.DestinationActivity == Activity.Leisure && person.DestinationId != "")
                return new RoutineSlot(person.DestinationId, slot.Activity, slot.EndsAt);
        }
        if (person.Phase == CitizenPhase.AtPlace && person.Activity == Activity.Leisure && person.ActivityUntilMinute > 0)
        {
            var committed = At(person, citySeed, Math.Max(0, person.ActivityUntilMinute - 0.001));
            if (committed.Activity == Activity.Leisure && committed.EndsAt == slot.EndsAt)
                return new RoutineSlot(person.PlaceId, slot.Activity, slot.EndsAt);
        }
        Place? home = null;
        foreach (var place in places) if (place.Id == person.HomeId) { home = place; break; }
        if (home == null) return slot;
        string selected = Choose(places, home, PlaceKind.Leisure,
            person.Identity.Id + ":leisure-day:" + anchorDay.ToString(CultureInfo.InvariantCulture), citySeed);
        return selected == "" ? slot : new RoutineSlot(selected, slot.Activity, slot.EndsAt);
    }

    public static RoutineSlot At(CitizenState person, int citySeed, double minute) => SelectRoutine(person, citySeed, minute, out _);

    private const double MaxMinute = 1000000000;

    private static RoutineSlot SelectRoutine(CitizenState person, int citySeed, double minute, out int anchorDay)
    {
        CheckMinute(minute);
        if (minute == MaxMinute) throw new ArgumentOutOfRangeException(nameof(minute));
        int day = (int)Math.Floor(minute / 1440);
        anchorDay = day;
        int shift = HourShift(person, citySeed);
        var work = WorkOnDay(person, day, shift);
        if (work.HasValue && minute >= work.Value.StartsAt && minute < work.Value.EndsAt)
            return new RoutineSlot(work.Value.PlaceId, work.Value.Activity, Math.Min(MaxMinute, work.Value.EndsAt));
        var quiet = new Lookup(person, citySeed);
        bool late = person.Identity.Persona == Persona.NightOwl;
        double next = MaxMinute;
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
                || StableRandom.Hash(person.Identity.Id + ":shop:" + currentDay.ToString(CultureInfo.InvariantCulture), citySeed) % 3 != 0))
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
                || StableRandom.Hash(person.Identity.Id + ":leisure:" + currentDay.ToString(CultureInfo.InvariantCulture), citySeed) % 2 == 0))
            {
                double until = quiet.OptionalEndsAt(leisureFrom, leisureUntil);
                if (until > leisureFrom && minute >= leisureFrom && minute < until)
                { anchorDay = currentDay; return new RoutineSlot(person.LeisureId, Activity.Leisure, until); }
                if (until > leisureFrom && leisureFrom > minute) next = Math.Min(next, leisureFrom);
            }
            if (currentDay >= day && next <= midnight + 1440) break;
        }
        return new RoutineSlot(person.HomeId, Activity.Home, next);
    }

    private static bool Weekend(int day) => ((day - 1) % 7 + 7) % 7 >= 4;
    private static void CheckMinute(double minute)
    {
        if (!Position.Finite(minute) || minute < 0 || minute > MaxMinute) throw new ArgumentOutOfRangeException(nameof(minute));
    }

    public static WorkWindow? WorkOnDay(CitizenState person, int citySeed, double minute)
    {
        CheckMinute(minute);
        return WorkOnDay(person, (int)Math.Floor(minute / 1440), HourShift(person, citySeed));
    }

    private static int HourShift(CitizenState person, int citySeed)
        => (int)(StableRandom.Hash(person.Identity.Id + ":hours", citySeed) % 31) - 15;

    private static WorkWindow? WorkOnDay(CitizenState person, int day, int shift)
    {
        if (Weekend(day) || person.DayPlaceId.Length == 0 || person.Identity.Persona == Persona.Retiree) return null;
        double start = day * 1440.0 + (person.Identity.Persona == Persona.NightOwl ? 720 : person.Identity.Persona == Persona.Parent ? 465 : 510) + shift;
        return new WorkWindow(person.DayPlaceId, person.Identity.Persona == Persona.Student ? Activity.Study : Activity.Work, start, start + 480);
    }

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

    public static double CommutePreparationAt(CitizenState person, int citySeed, WorkWindow window, double previousMinutes)
    {
        uint personal = StableRandom.Hash(person.Identity.Id + ":commute-preparation", citySeed);
        double ordinaryLead = 480 + personal % 361;
        double margin = 30 + StableRandom.Hash(person.Identity.Id + ":commute-margin", citySeed) % 61;
        double lead = Math.Max(ordinaryLead, previousMinutes + margin);
        return Math.Max(0, window.StartsAt - lead);
    }

    public static double NextHomeEventDraw(CitizenState person) => StableRandom.Hash(person.Identity.Id + ":employment-time", 0) % 240;

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
        foreach (var interval in OpeningIntervals(place, minute))
        {
            double opening = Math.Max(minute, interval.From);
            if (opening >= beforeMinute) break;
            if (interval.To > opening) return opening;
        }
        return null;
    }

    private static List<(double From, double To)> OpeningIntervals(Place place, double minute)
    {
        int day = (int)Math.Floor(minute / 1440);
        bool weekly = place.WeeklyHours.Count != 0;
        var intervals = new List<(double From, double To)>();
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
        return intervals;
    }

    private static void AddInterval(List<(double From, double To)> intervals, double midnight, int from, int to)
    {
        if (from == to) return;
        intervals.Add((midnight + from, midnight + to + (to < from ? 1440 : 0)));
    }

    public static QuietWindow OnDay(CitizenState person, int citySeed, int day) => new Lookup(person, citySeed).OnDay(day);

    private struct Lookup
    {
        private readonly CitizenState person;
        private readonly int seed;
        private readonly double bed, wake;
        private bool hasPersonal;
        private int personal;
        private bool hasFirst, hasSecond;
        private int firstDay, secondDay;
        private QuietWindow first, second;

        internal Lookup(CitizenState person, int seed)
        {
            this.person = person; this.seed = seed;
            bool earlier = person.Identity.Persona == Persona.Parent || person.Identity.Persona == Persona.Retiree;
            bool student = person.Identity.Persona == Persona.Student;
            bool late = person.Identity.Persona == Persona.NightOwl;
            bed = late ? 1430 : earlier ? 1290 : student ? 1350 : 1380;
            wake = late ? 1980 : earlier ? 1830 : student ? 1860 : 1890;
            hasPersonal = false; personal = 0;
            hasFirst = hasSecond = false; firstDay = secondDay = 0;
            first = second = default;
        }

        internal QuietWindow OnDay(int day)
        {
            if (hasFirst && firstDay == day) return first;
            if (hasSecond && secondDay == day) return second;
            if (!hasPersonal)
            {
                personal = (int)(StableRandom.Hash(person.Identity.Id + ":quiet-hours", seed) % 91) - 45;
                hasPersonal = true;
            }
            string key = person.Identity.Id + ":quiet-day:" + day.ToString(CultureInfo.InvariantCulture);
            int daily = (int)(StableRandom.Hash(key, seed) % 31) - 15;
            bool weekendWake = WeekendWake(day);
            double midnight = day * 1440.0;
            var window = new QuietWindow(midnight + bed + personal + daily + (weekendWake ? 30 : 0),
                midnight + wake + personal + daily + (weekendWake ? 60 : 0));
            secondDay = firstDay; second = first; hasSecond = hasFirst;
            firstDay = day; first = window; hasFirst = true;
            return window;
        }

        internal double OptionalEndsAt(double from, double until)
        {
            int day = (int)Math.Floor(from / 1440);
            for (int offset = -1; offset <= 0; offset++)
            {
                int quietDay = day + offset;
                bool weekendWake = WeekendWake(quietDay);
                double midnight = quietDay * 1440.0;
                double earliestBed = midnight + bed - 60 + (weekendWake ? 30 : 0);
                double latestWake = midnight + wake + 60 + (weekendWake ? 60 : 0);
                if (until <= earliestBed || from >= latestWake) continue;
                var quiet = OnDay(quietDay);
                if (from >= quiet.StartsAt && from < quiet.EndsAt) return from;
                if (quiet.StartsAt > from) until = Math.Min(until, quiet.StartsAt);
            }
            return until;
        }

        private static bool WeekendWake(int day) => ((day % 7) + 7) % 7 >= 4;
    }

    public static IReadOnlyList<Place> Shortlist(IEnumerable<Place> places, Place origin, PlaceKind kind, string choiceKey, int seed, int maximum = 8)
    {
        var ranked = new List<(Place Place, double Score)>();
        foreach (var place in places)
        {
            if (place.Kind != kind || place.Capacity <= 0) continue;
            double attraction = Position.Finite(place.AttractionWeight) ? Math.Max(.05, Math.Min(20, place.AttractionWeight)) : 1;
            double traffic = place.NativeTrafficIndex < 0 ? 1 : 1 + Math.Min(100, place.NativeTrafficIndex) / 100.0;
            double distance = Position.Distance(origin.Position, place.Position) / 250.0;
            double uniform = (StableRandom.Hash(choiceKey + ":" + place.Id, seed) + 1.0) / (uint.MaxValue + 2.0);
            double score = -Math.Log(uniform) * (1 + distance) * (1 + distance) / (attraction * traffic);
            int index = 0;
            while (index < ranked.Count && (score > ranked[index].Score
                || (score == ranked[index].Score && StringComparer.Ordinal.Compare(place.Id, ranked[index].Place.Id) >= 0))) index++;
            if (index >= maximum) continue;
            ranked.Insert(index, (place, score));
            if (ranked.Count > maximum) ranked.RemoveAt(maximum);
        }
        return ranked.Select(x => x.Place).ToArray();
    }

    public static string Choose(IEnumerable<Place> places, Place origin, PlaceKind kind, string choiceKey, int seed, Func<Place, bool>? eligible = null)
    {
        string best = "";
        double bestScore = double.PositiveInfinity;
        foreach (var place in places.Where(p => p.Kind == kind && p.Capacity > 0))
        {
            if (eligible != null && !eligible(place)) continue;
            double attraction = Position.Finite(place.AttractionWeight) ? Math.Max(0.05, Math.Min(20, place.AttractionWeight)) : 1;
            double traffic = place.NativeTrafficIndex < 0 ? 1 : 1 + Math.Min(100, place.NativeTrafficIndex) / 100.0;
            double distance = Position.Distance(origin.Position, place.Position) / 1200.0;
            double weight = attraction * traffic / ((1 + distance) * (1 + distance));
            double uniform = (StableRandom.Hash(choiceKey + ":" + place.Id, seed) + 1.0) / (uint.MaxValue + 2.0);
            double score = -Math.Log(uniform) / weight;
            if (score < bestScore || (score == bestScore && StringComparer.Ordinal.Compare(place.Id, best) < 0))
            { bestScore = score; best = place.Id; }
        }
        return best;
    }

    /// <summary>TrafficNetwork.TryTag before the change, for a network whose NativeTopologyId is topology.</summary>
    public static bool NetworkTag(string? service, string topology, out int from, out int to, out bool fragment)
    {
        from = to = -1;
        fragment = false;
        if (service == null) return false;
        string[] parts = service.Split(':');
        if (parts.Length != 6 || parts[0] != "native" || parts[1] != "road" ||
            (parts[2] != "v1" && parts[2] != "fragment1") || !string.Equals(parts[3], topology, StringComparison.Ordinal) ||
            !int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out from) ||
            !int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out to)) return false;
        fragment = parts[2] == "fragment1";
        return true;
    }

    /// <summary>The parse half of RoadRoutes.TryTag (v1 only) before the change.</summary>
    public static bool RoadRoutesTag(string? service, string topology, out int from, out int to)
    {
        from = to = -1;
        if (string.IsNullOrEmpty(service)) return false;
        var parts = service.Split(':');
        return parts.Length == 6 && parts[0] == "native" && parts[1] == "road" && parts[2] == "v1" && parts[3] == topology
            && int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out from)
            && int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out to);
    }

    /// <summary>The parse half of RoadRoutes.KnownPhysicalFragment before the change.</summary>
    public static bool RoadRoutesFragment(string service, string topology, out int from, out int to)
    {
        from = to = -1;
        var parts = service.Split(':');
        return parts.Length == 6 && parts[0] == "native" && parts[1] == "road" && parts[2] == "fragment1" && parts[3] == topology
            && int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out from)
            && int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out to) && from != to;
    }
}
