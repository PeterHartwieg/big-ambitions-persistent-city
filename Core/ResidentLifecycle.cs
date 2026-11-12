using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PersistentCity.Core
{
    public static class ResidentLifecycle
    {
        public const double Never = 1000000000;
        public static bool IsWorker(CitizenState person) => person.Identity.Persona != Persona.Student && person.Identity.Persona != Persona.Retiree;
        public static bool IsSafelyHome(CitizenState person) => person.Phase == CitizenPhase.AtPlace
            && person.Activity == Activity.Home && person.PlaceId == person.HomeId
            && person.DestinationId == "" && person.Journey == null && person.DepartureMinute == 0
            && person.MealOuting == null;

        public static void Initialize(CitizenState person, int seed, double minute)
        {
            person.JoinedMinute = minute;
            person.JobChanges = 0;
            person.LastEmploymentCheckDay = (int)(minute / 1440);
            person.EmploymentStartDay = person.DayPlaceId.Length == 0 ? 0 : person.LastEmploymentCheckDay;
        }

        public static double NextHomeEvent(CitizenState person, double routineMinute, double now)
        {
            if (!IsWorker(person) || !IsSafelyHome(person)) return routineMinute;
            if (person.IsVisitor) return routineMinute;
            double nextDay = (person.LastEmploymentCheckDay + 1) * 1440.0
                + 1 + RoutineKeys.EmploymentTimeFor(person) % 240;
            return Math.Min(routineMinute, nextDay <= now ? now + 1 : nextDay);
        }

        internal static IReadOnlyList<Place>? BeginEmploymentCheck(CityState city, CitizenState person, double now)
            => BeginEmploymentCheck(city, person, now, null);

        internal static IReadOnlyList<Place>? BeginEmploymentCheck(CityState city, CitizenState person, double now,
            IReadOnlyDictionary<string, Place>? places, WorkplaceOccupancy? occupancy = null)
        {
            if (!IsWorker(person) || !IsSafelyHome(person) || person.LastEmploymentCheckDay >= (int)(now / 1440)) return null;
            int day = checked(person.LastEmploymentCheckDay + 1);
            person.LastEmploymentCheckDay = day;
            string key = person.Identity.Id + ":employment:" + day.ToString(CultureInfo.InvariantCulture);
            double chance = (StableRandom.Hash(key, city.Seed) + 1.0) / (uint.MaxValue + 2.0);
            if (person.DayPlaceId == "" && person.VisitorCarried == ResidentIncomeSource.ExternalEmployment)
            {
                if (chance < CityBalance.DailyJobLossChance) { person.VisitorCarried = null; return null; }
            }
            else if (person.DayPlaceId != "")
            {
                if (chance >= EmploymentWages.JobLossChance(person.Accrual, day)) return null;
                person.DayPlaceId = "";
                person.Accrual = default;
                person.EmploymentStartDay = 0;
                person.JobChanges = checked(person.JobChanges + 1);
                person.CommutePreparation = new CommutePreparation();
                return null;
            }
            if (person.Visitor?.Stay.Pattern == VisitorPattern.DayVisit) return null;
            if (chance >= CityBalance.DailyJobFindingChance) return null;
            var home = places != null && places.TryGetValue(person.HomeId, out var indexed) ? indexed
                : city.Places.First(p => p.Id == person.HomeId);
            if (occupancy != null)
                return DestinationChoice.Shortlist(city.Places.Where(occupancy.Vacant), home, PlaceKind.Work, key, city.Seed, CityBalance.WorkplaceRouteProbes);
            var used = Population.Assignments(city, person);
            return DestinationChoice.Shortlist(city.Places.Where(p => Population.Used(used, p.Id) < p.Capacity),
                home, PlaceKind.Work, key, city.Seed, CityBalance.WorkplaceRouteProbes);
        }

        internal static void AssignWorkplace(CitizenState person, Place candidate)
        {
            person.DayPlaceId = candidate.Id;
            person.Accrual = default;
            person.VisitorCarried = null;
            person.EmploymentStartDay = person.LastEmploymentCheckDay;
            person.JobChanges = checked(person.JobChanges + 1);
            person.CommutePreparation = new CommutePreparation();
        }

        public static CitizenIdentity RemixIdentity(CityState city, int sequence) => CreateRemixer(city)(sequence);

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
