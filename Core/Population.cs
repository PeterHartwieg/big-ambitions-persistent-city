using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentCity.Core
{
    public static class StableRandom
    {
        public static uint Hash(string text, int seed)
        {
            unchecked
            {
                uint value = 2166136261u ^ (uint)seed;
                foreach (char c in text) { value ^= c; value *= 16777619u; }
                value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15;
                return value;
            }
        }
    }

    public static class Population
    {
        public static CityState Create(IEnumerable<CitizenIdentity> identities, IEnumerable<Place> places,
            string populationKey, string rloVersion, int seed, double minute)
            => CreatePopulation(identities, places, populationKey, rloVersion, seed, minute, false);

        public static CityState CreateFromHousing(IEnumerable<CitizenIdentity> identities, IEnumerable<Place> places,
            string populationKey, string rloVersion, int seed, double minute)
            => CreatePopulation(identities, places, populationKey, rloVersion, seed, minute, true);

        private static CityState CreatePopulation(IEnumerable<CitizenIdentity> identities, IEnumerable<Place> places,
            string populationKey, string rloVersion, int seed, double minute, bool useHousing)
        {
            if (!Position.Finite(minute) || minute < 0 || minute > 1e9) throw new ArgumentOutOfRangeException(nameof(minute));
            if (string.IsNullOrWhiteSpace(populationKey)) throw new ArgumentException("A population key is required.");
            var city = new CityState { Seed = seed, Minute = minute, PopulationKey = populationKey, RloVersion = rloVersion };
            city.Places = places.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
            if (city.Places.Count == 0 || city.Places.Any(p => string.IsNullOrWhiteSpace(p.Id) || !p.Position.IsFinite || p.Capacity < 0 || !Enum.IsDefined(typeof(PlaceKind), p.Kind))
                || city.Places.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != city.Places.Count)
                throw new ArgumentException("Places must have unique IDs, known kinds, finite positions and nonnegative capacities.");
            var people = identities.OrderBy(p => p.Id, StringComparer.Ordinal).Select(CloneIdentity).ToList();
            if (people.Count == 0 || people.Any(p => string.IsNullOrWhiteSpace(p.Id) || p.Id.StartsWith("pc:", StringComparison.Ordinal)
                || !Enum.IsDefined(typeof(Persona), p.Persona)) || people.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != people.Count)
                throw new ArgumentException("Citizens must have unique, nonempty identities and known personas.");
            long beds = city.Places.Where(p => p.Kind == PlaceKind.Home).Sum(p => (long)p.Capacity);
            int target = useHousing ? HousingPolicy.Target(beds) : people.Count;
            if (beds < target) throw new InvalidOperationException("The catalogue has insufficient residential capacity; no population was created.");
            city.SourceIdentities = people.Select(CloneIdentity).ToList();
            city.PopulationTarget = target;
            city.InitialImportedResidents = Math.Min(target, people.Count);
            var used = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var identity in people.Take(city.InitialImportedResidents))
            {
                var person = CreateResident(city, identity, minute, used);
                city.Citizens.Add(person);
                Count(used, person.HomeId);
                if (person.DayPlaceId != "") Count(used, person.DayPlaceId);
            }
            AppendResidents(city, target, used);
            return city;
        }

        // Called after catalogue capacity refresh. Existing residents keep all physical
        // events and assignments, even when revised housing implies a smaller target.
        public static void ExpandToHousing(CityState city)
        {
            if (city.Places.Any(p => p.Kind == PlaceKind.Home && p.Capacity < 0))
                throw new InvalidOperationException("Residential capacities must be nonnegative.");
            long beds = city.Places.Where(p => p.Kind == PlaceKind.Home).Sum(p => (long)p.Capacity);
            // Retain a singleton legacy population when 90% occupancy rounds down to zero.
            int target = beds <= 1 && city.Citizens.Count > 0 ? city.Citizens.Count : HousingPolicy.Target(beds);
            AppendResidents(city, target, Assignments(city));
            // A capacity correction never evicts an already established resident.
            city.PopulationTarget = Math.Max(city.Citizens.Count, target);
        }

        private static void AppendResidents(CityState city, int target, Dictionary<string, int> used)
        {
            int additional = Math.Max(0, target - city.Citizens.Count);
            if (city.NextResidentSequence < 1 || (long)city.NextResidentSequence + additional > int.MaxValue)
                throw new InvalidOperationException("Resident identity sequence exhausted.");
            if (additional == 0) return;
            var remix = ResidentLifecycle.CreateRemixer(city);
            var residents = new List<CitizenState>();
            for (int index = 0; index < additional; index++)
            {
                var identity = remix(city.NextResidentSequence + index);
                var person = CreateResident(city, identity, city.Minute, used);
                residents.Add(person);
                Count(used, person.HomeId);
                if (person.DayPlaceId != "") Count(used, person.DayPlaceId);
            }
            city.Citizens.AddRange(residents);
            city.NextResidentSequence += additional;
        }

        public static CitizenIdentity CloneIdentity(CitizenIdentity value) => new CitizenIdentity
        {
            Id = value.Id, Name = value.Name, District = value.District, Age = value.Age,
            Female = value.Female, Persona = value.Persona, AppearanceSeed = value.AppearanceSeed
        };

        public static bool EqualIdentity(CitizenIdentity a, CitizenIdentity b) => a.Id == b.Id && a.Name == b.Name
            && a.District == b.District && a.Age == b.Age && a.Female == b.Female
            && a.Persona == b.Persona && a.AppearanceSeed == b.AppearanceSeed;

        // Called only after strict v1 validation. Preserve every physical event, route and
        // assignment; expansion starts from the recorded simulation clock, not game time.
        public static void InitializeExpansion(CityState city)
        {
            city.SourceIdentities = city.Citizens.OrderBy(c => c.Identity.Id, StringComparer.Ordinal).Select(c => CloneIdentity(c.Identity)).ToList();
            city.RecentDepartures = new List<DepartureRecord>();
            city.NextResidentSequence = 1;
            city.DepartedCount = 0;
            city.PopulationTarget = city.Citizens.Count;
            city.InitialImportedResidents = city.Citizens.Count;
            foreach (var person in city.Citizens) ResidentLifecycle.Initialize(person, city.Seed, city.Minute);
            city.Schema = CityState.CurrentSchema;
        }

        internal static CitizenState CreateResident(CityState city, CitizenIdentity identity, double minute, Dictionary<string, int> used,
            bool established = true)
        {
            var home = city.Places.Where(p => p.Kind == PlaceKind.Home && Used(used, p.Id) < p.Capacity)
                .OrderBy(p => p.District == identity.District ? 0 : 1)
                .ThenBy(p => StableRandom.Hash(identity.Id + ":home:" + p.Id, city.Seed)).ThenBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
            if (home == null) throw new InvalidOperationException("No residential vacancy is available for the new resident.");
            string day = identity.Persona == Persona.Retiree ? "" : DestinationChoice.Choose(city.Places, home,
                identity.Persona == Persona.Student ? PlaceKind.Study : PlaceKind.Work, identity.Id + ":day", city.Seed,
                p => Used(used, p.Id) < p.Capacity);
            var person = new CitizenState
            {
                Identity = CloneIdentity(identity), HomeId = home.Id, PlaceId = home.Id, DayPlaceId = day,
                ShopId = DestinationChoice.Choose(city.Places, home, PlaceKind.Shop, identity.Id + ":shop", city.Seed),
                LeisureId = DestinationChoice.Choose(city.Places, home, PlaceKind.Leisure, identity.Id + ":leisure", city.Seed),
                Activity = Activity.Home, Phase = CitizenPhase.AtPlace, NextEventMinute = minute, ActivityUntilMinute = minute,
                Preferences = new TravelPreferences
                {
                    OwnsCar = identity.Persona != Persona.Student && StableRandom.Hash(identity.Id + ":car", city.Seed) % 100 < 35,
                    AllowTransit = StableRandom.Hash(identity.Id + ":transit", city.Seed) % 100 < 90,
                    WalkingToleranceMetres = (identity.Persona == Persona.Sporty ? 2200 : identity.Persona == Persona.Retiree ? 700 : 1200)
                        * (0.75 + StableRandom.Hash(identity.Id + ":walking", city.Seed) % 51 / 100.0),
                    FareWeightMinutes = (identity.Persona == Persona.Student ? 5 : 2)
                        * (0.75 + StableRandom.Hash(identity.Id + ":fare", city.Seed) % 51 / 100.0)
                }
            };
            ResidentLifecycle.Initialize(person, city.Seed, minute, established);
            return person;
        }

        internal static Dictionary<string, int> Assignments(CityState city, CitizenState? excluded = null)
        {
            var used = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var person in city.Citizens)
            {
                if (ReferenceEquals(person, excluded)) continue;
                Count(used, person.HomeId);
                if (person.DayPlaceId != "") Count(used, person.DayPlaceId);
            }
            return used;
        }
        internal static int Used(Dictionary<string, int> values, string id) => values.TryGetValue(id, out var count) ? count : 0;
        private static void Count(Dictionary<string, int> values, string id) => values[id] = Used(values, id) + 1;
    }
}
