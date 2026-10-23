using System;
using System.Globalization;

namespace PersistentCity.Core
{
    public readonly struct QuietWindow
    {
        public QuietWindow(double startsAt, double endsAt) { StartsAt = startsAt; EndsAt = endsAt; }
        public double StartsAt { get; }
        public double EndsAt { get; }
    }

    /// <summary>Personal home preferences. Work, study and committed travel remain simulator-owned.</summary>
    public static class DailyRhythm
    {
        // The day identifies the evening, including a NightOwl's bedtime after that midnight.
        public static QuietWindow OnDay(CitizenState person, int citySeed, int day)
        {
            var lookup = new Lookup(person, citySeed);
            return lookup.OnDay(day);
        }

        // One lookup per routine evaluation. Most daytime optional slots can be excluded
        // by persona bounds without constructing keys or hashing any quiet-hour choices.
        internal struct Lookup
        {
            private readonly CitizenState person;
            private readonly int seed;
            private readonly double bed;
            private readonly double wake;
            private bool hasPersonal;
            private int personal;
            private bool hasFirst, hasSecond;
            private int firstDay, secondDay;
            private QuietWindow first, second;

            internal Lookup(CitizenState person, int seed)
            {
                if (person == null) throw new ArgumentNullException(nameof(person));
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
                // Optional slots last at most two hours. The night before yesterday
                // ends before this day starts; tomorrow's bedtime is beyond this slot.
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
    }
}
