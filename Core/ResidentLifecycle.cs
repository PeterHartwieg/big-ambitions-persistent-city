using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PersistentCity.Core
{
    public static class ResidentLifecycle
    {
        public const double Never = 1000000000;
        public const int MaxRecentDepartures = 128;

        public static bool IsWorker(CitizenState person) => person.Identity.Persona != Persona.Student && person.Identity.Persona != Persona.Retiree;
        public static bool IsSafelyHome(CitizenState person) => person.Phase == CitizenPhase.AtPlace
            && person.Activity == Activity.Home && person.PlaceId == person.HomeId
            && person.DestinationId == "" && person.Journey == null && person.DepartureMinute == 0;

        public static void Initialize(CitizenState person, int seed, double minute, bool established = true)
        {
            person.JoinedMinute = minute;
            person.JobChanges = 0;
            person.NextJobChangeMinute = IsWorker(person) ? NextJob(person, seed, minute, established ? 1 : 90) : Never;
            person.NextDepartureMinute = After(minute, 7 + WaitingDays(person.Identity.Id + ":departure", seed, 1200));
        }

        // Schema 2 workers retain overdue events; future dates get the slower rate
        // without changing a commute, daily wake-up, assignment or departure date.
        public static void RetuneLegacyJobs(CityState city)
        {
            foreach (var person in city.Citizens)
                if (IsWorker(person) && Position.Finite(person.NextJobChangeMinute)
                    && person.NextJobChangeMinute > city.Minute && person.NextJobChangeMinute < Never)
                    person.NextJobChangeMinute = Math.Min(Never, city.Minute + (person.NextJobChangeMinute - city.Minute) * 10);
        }

        public static double NextHomeEvent(CitizenState person, double routineMinute, double now)
        {
            if (!IsSafelyHome(person)) return routineMinute;
            double next = routineMinute;
            if (person.NextJobChangeMinute > now && person.NextJobChangeMinute < Never) next = Math.Min(next, person.NextJobChangeMinute);
            if (person.NextDepartureMinute > now && person.NextDepartureMinute < Never) next = Math.Min(next, person.NextDepartureMinute);
            return next;
        }

        // Called only at a completed physical home event. Dates remain pending while away.
        // Return the current resident so the caller never requeues a departed object.
        internal static CitizenState ApplyAtHome(CityState city, CitizenState person, double now)
        {
            if (!IsSafelyHome(person)) return person;
            if (person.NextDepartureMinute < Never && person.NextDepartureMinute <= now)
            {
                if (city.NextResidentSequence < 1 || city.NextResidentSequence == int.MaxValue || city.DepartedCount == int.MaxValue)
                    throw new InvalidOperationException("Resident identity sequence exhausted.");
                var identity = RemixIdentity(city, city.NextResidentSequence);
                var replacement = Population.CreateResident(city, identity, now, Population.Assignments(city, person), established: false);
                int index = city.Citizens.IndexOf(person);
                if (index < 0) throw new InvalidOperationException("Departing resident is not active.");
                // Construct first, commit once. No game/RLO state is involved in this change.
                city.Citizens[index] = replacement;
                city.RecentDepartures.Add(new DepartureRecord
                {
                    Identity = Population.CloneIdentity(person.Identity), JoinedMinute = person.JoinedMinute, DepartedMinute = now
                });
                if (city.RecentDepartures.Count > MaxRecentDepartures) city.RecentDepartures.RemoveAt(0);
                city.NextResidentSequence++;
                city.DepartedCount++;
                return replacement;
            }
            if (person.NextJobChangeMinute < Never && person.NextJobChangeMinute <= now)
            {
                if (IsWorker(person))
                {
                    var used = Population.Assignments(city, person);
                    var home = city.Places.First(p => p.Id == person.HomeId);
                    string next = DestinationChoice.Choose(city.Places, home, PlaceKind.Work,
                        person.Identity.Id + ":job:" + person.JobChanges.ToString(CultureInfo.InvariantCulture), city.Seed,
                        p => p.Id != person.DayPlaceId && Population.Used(used, p.Id) < p.Capacity);
                    if (next != "") { person.DayPlaceId = next; person.JobChanges = checked(person.JobChanges + 1); }
                    person.NextJobChangeMinute = NextJob(person, city.Seed, now, next != "" ? 90 : 1);
                }
                else person.NextJobChangeMinute = Never;
            }
            return person;
        }

        private static double NextJob(CitizenState person, int seed, double minute, double minimumDays) => After(minute,
            minimumDays + WaitingDays(person.Identity.Id + ":job-date:" + person.JobChanges.ToString(CultureInfo.InvariantCulture)
                + ":" + BitConverter.DoubleToInt64Bits(minute).ToString("X16", CultureInfo.InvariantCulture), seed, 900));
        private static double WaitingDays(string key, int seed, double mean)
        {
            double uniform = (StableRandom.Hash(key, seed) + 1.0) / (uint.MaxValue + 2.0);
            return -Math.Log(uniform) * mean;
        }
        private static double After(double minute, double days) => Math.Min(Never, minute + days * 1440);

        public static CitizenIdentity RemixIdentity(CityState city, int sequence) => CreateRemixer(city)(sequence);

        // One immutable view per expansion avoids sorting the same donor bank and
        // rebuilding identical age-compatible pools for every appended resident.
        internal static Func<int, CitizenIdentity> CreateRemixer(CityState city)
        {
            var sources = city.SourceIdentities.OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
            if (sources.Length == 0) throw new InvalidOperationException("New residents require the preserved source population.");
            var compatibleByAge = new Dictionary<int, CitizenIdentity[]>();
            return Remix;

            CitizenIdentity Remix(int sequence)
            {
                if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
                string id = "pc:" + sequence.ToString(CultureInfo.InvariantCulture);
                var appearance = sources[StableRandom.Hash(id + ":appearance", city.Seed) % (uint)sources.Length];
                var surname = sources[StableRandom.Hash(id + ":surname", city.Seed) % (uint)sources.Length];
                // Keep RLO's appearance donor coherent with the new routine. Independent
                // persona donors can add variety only within that donor's age band.
                if (!compatibleByAge.TryGetValue(appearance.Age, out var compatible))
                {
                    compatible = sources.Where(c => FitsAge(c.Persona, appearance.Age)).ToArray();
                    compatibleByAge.Add(appearance.Age, compatible);
                }
                var persona = compatible.Length == 0 ? appearance
                    : compatible[StableRandom.Hash(id + ":persona", city.Seed) % (uint)compatible.Length];
                string given = appearance.Name.Trim(), family = surname.Name.Trim();
                int space = given.LastIndexOf(' ');
                if (space > 0) given = given.Substring(0, space);
                space = family.LastIndexOf(' ');
                if (space >= 0 && space < family.Length - 1) family = family.Substring(space + 1);
                return new CitizenIdentity
                {
                    Id = id, Name = given + " " + family, District = appearance.District,
                    Age = appearance.Age, Female = appearance.Female, AppearanceSeed = appearance.AppearanceSeed,
                    Persona = persona.Persona
                };
            }
        }

        private static bool FitsAge(Persona persona, int age) => persona == Persona.Student ? age >= 18 && age <= 25
            : persona == Persona.Parent ? age >= 27 && age <= 50
            : persona == Persona.Retiree ? age >= 65 && age <= 82
            : age >= 20 && age <= 64;
    }
}
