using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{

    public sealed class VisitorRoutine : CitizenState
    {
        public VisitorPerson? Person => Visitor;
        public ResidentIncomeSource? Carried { get => VisitorCarried; set => VisitorCarried = value; }
    }

    public readonly struct PortalStation
    {
        public PortalStation(string id, Position entrance) { Id = id; Entrance = entrance; }
        public string Id { get; }
        public Position Entrance { get; }
    }

    public static class PortalPlaces
    {
        public const string Evidence = "portal:subway-station";

        public static bool Is(Place place) => place.Kind == PlaceKind.Home && place.Capacity == 0
            && string.Equals(place.EvidenceSource, Evidence, StringComparison.Ordinal);

        public static void Add(CityState city, IEnumerable<PortalStation> stations)
        {
            if (city.Visitors.Count != 0) throw new InvalidOperationException("Portals are fixed before visitors are created.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var place in city.Places) ids.Add(place.Id);
            var added = new List<Place>();
            foreach (var station in stations)
            {
                if (string.IsNullOrWhiteSpace(station.Id) || station.Id.Length > 128 || !station.Entrance.IsFinite || !ids.Add(station.Id)) continue;
                added.Add(new Place { Id = station.Id, Name = station.Id, Kind = PlaceKind.Home, Capacity = 0, Position = station.Entrance,
                    District = NearestDistrict(city.Places, station.Entrance), EvidenceSource = Evidence, OpenMinute = 0, CloseMinute = 1440 });
            }
            if (added.Count == 0) return;
            city.Places.AddRange(added);
            city.Places.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var place in added) city.Portals.Add(place.Id);
            city.Portals.Sort(string.CompareOrdinal);
            if (city.Portals.Count > PersonTable.MaximumPortals) throw new InvalidOperationException("Too many portals.");
        }

        private static string NearestDistrict(IReadOnlyList<Place> places, Position at)
        {
            Place? best = null; double distance = double.PositiveInfinity;
            foreach (var place in places)
            {
                if (Is(place)) continue;
                double d = Position.Distance(place.Position, at);
                if (d < distance || d == distance && best != null && string.CompareOrdinal(place.Id, best.Id) < 0) { best = place; distance = d; }
            }
            return best?.District ?? "";
        }
    }

    internal sealed class VisitorFactory
    {
        private readonly CityState city;
        private readonly Dictionary<string, Place> places = new Dictionary<string, Place>(StringComparer.Ordinal);
        private readonly Dictionary<PlaceKind, List<Place>> candidates = new Dictionary<PlaceKind, List<Place>>();
        private readonly IdentityPolicy identities;
        private readonly Func<Place, bool> vacant;
        private readonly Action<string> seat;

        internal VisitorFactory(CityState city, Func<Place, bool> vacant, Action<string> seat)
        {
            this.city = city; this.vacant = vacant; this.seat = seat;
            identities = IdentityPolicy.V1(city.SourceIdentities);
            foreach (var place in city.Places) places[place.Id] = place;
            foreach (var kind in new[] { PlaceKind.Work, PlaceKind.Study, PlaceKind.Shop, PlaceKind.Leisure }) candidates[kind] = new List<Place>();
            foreach (var place in city.Places)
                if (place.Capacity > 0 && candidates.TryGetValue(place.Kind, out var list)) list.Add(place);
        }

        internal bool Working(int sequence) => VisitorSchedules.WorkingPersona(identities.PersonaOf(city.Seed, PersonIds.Visitor(sequence)));

        internal (VisitorPerson Visitor, VisitorRoutine Routine) Create(int sequence, VisitorPattern pattern, double minute)
        {
            string id = PersonIds.Visitor(sequence);
            string portalId = city.Portals[(int)(StableRandom.Hash(id + ":portal", city.Seed) % (uint)city.Portals.Count)];
            var portal = places[portalId];
            var identity = identities.Derive(city.Seed, id);
            var persona = identity.Persona;
            var dayKind = persona == Persona.Student ? PlaceKind.Study : PlaceKind.Work;
            string work = persona == Persona.Retiree || pattern != VisitorPattern.Commuter ? ""
                : DestinationChoice.Choose(candidates[dayKind], portal, dayKind, id + ":day", city.Seed, vacant);
            if (work.Length != 0) seat(work);
            var routine = new VisitorRoutine
            {
                Identity = identity, HomeId = portalId, PlaceId = portalId, DayPlaceId = work,
                ShopId = DestinationChoice.Choose(candidates[PlaceKind.Shop], portal, PlaceKind.Shop, id + ":shop", city.Seed,
                    VisitorTable.GroupOf(id, city.Seed)),
                LeisureId = DestinationChoice.Choose(candidates[PlaceKind.Leisure], portal, PlaceKind.Leisure, id + ":leisure", city.Seed),
                Activity = Activity.Home, Phase = CitizenPhase.AtPlace, NextEventMinute = minute, ActivityUntilMinute = minute,
                Preferences = VisitorSchedules.Preferences(id, persona, city.Seed),
                Traits = ResidentTraits.Generate(city.Seed, id)
            };
            ResidentLifecycle.Initialize(routine, city.Seed, minute);
            var visitor = new VisitorPerson { Id = id, Stay = new VisitorStay { OriginPortalId = portalId,
                Pattern = pattern, Presence = VisitorPresence.Dormant, FirstDay = (int)Math.Floor(minute / 1440), NextStayStart = minute } };
            if (work.Length == 0 && ResidentLifecycle.IsWorker(routine) && identity.Age >= 18)
                routine.VisitorCarried = ResidentIncomeSource.ExternalEmployment;
            VisitorSchedules.Link(visitor, routine);
            VisitorSchedules.Sync(visitor);
            return (visitor, routine);
        }
    }

    public static class VisitorSchedules
    {
        public static int FreshVisitors(int residents, int factor) => residents * Math.Max(0, factor - 1);

        public static IReadOnlyList<VisitorRoutine> Create(CityState city, int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var created = new List<VisitorRoutine>();
            if (count == 0 || city.Portals.Count == 0) return created;
            if ((long)city.Visitors.Count + count > PersonTable.MaximumVisitors
                || (long)city.NextVisitorSequence + count > int.MaxValue) throw new InvalidOperationException("Visitor bound exceeded.");
            var used = Population.Assignments(city);
            var factory = new VisitorFactory(city, p => Population.Used(used, p.Id) < p.Capacity, work => used[work] = Population.Used(used, work) + 1);
            for (int i = 0; i < count; i++)
            {
                var (visitor, routine) = factory.Create(city.NextVisitorSequence + i, PersonPolicy.MigratedConsumerPattern, city.Minute);
                city.Visitors.Add(visitor); city.VisitorRoutines.Add(routine);
                created.Add(routine);
            }
            city.NextVisitorSequence += count;
            Order(city);
            return created;
        }

        public static bool WorkingPersona(Persona persona) => persona != Persona.Student && persona != Persona.Retiree;

        public static bool OnlyNonWorkingSkipped(CityState city, IReadOnlyCollection<int> present)
        {
            var used = new bool[city.NextVisitorSequence];
            foreach (int n in present) if (n > 0 && n < used.Length) used[n] = true;
            var identities = IdentityPolicy.V1(city.SourceIdentities);
            for (int n = 1; n < used.Length; n++)
                if (!used[n] && WorkingPersona(identities.PersonaOf(city.Seed, PersonIds.Visitor(n)))) return false;
            return true;
        }

        public static bool VisitsOn(CitizenState person, int seed, int day)
            => PresentOn(RoutineKeys.For(person, seed), day, VisitorPattern.DayVisit);

        internal static bool PresentOn(RoutineKeys keys, int day, VisitorPattern pattern)
            => RoutineKeys.Draw(keys.VisitDay, day) % 10000 < VisitorTable.BasisPoints(pattern, DayPlanner.Weekday(day));

        public static TravelPreferences Preferences(string id, Persona persona, int seed) => new TravelPreferences
        {
            OwnsCar = PersonPolicy.VisitorsOwnCars, AllowTransit = true,
            WalkingToleranceMetres = (persona == Persona.Sporty ? 2200 : persona == Persona.Retiree ? 700 : 1200)
                * (0.75 + StableRandom.Hash(id + ":walking", seed) % 51 / 100.0),
            FareWeightMinutes = (persona == Persona.Student ? 5 : 2) * (0.75 + StableRandom.Hash(id + ":fare", seed) % 51 / 100.0)
        };

        public static void Order(CityState city)
        {
            if (city.Visitors.Count != city.VisitorRoutines.Count) throw new InvalidOperationException("Every visitor has exactly one routine.");
            var pairs = new List<(VisitorPerson Visitor, VisitorRoutine Routine)>(city.Visitors.Count);
            for (int i = 0; i < city.Visitors.Count; i++) pairs.Add((city.Visitors[i], city.VisitorRoutines[i]));
            pairs.Sort((a, b) => string.CompareOrdinal(a.Visitor.Id, b.Visitor.Id));
            city.Visitors.Clear(); city.VisitorRoutines.Clear();
            foreach (var (visitor, routine) in pairs) { city.Visitors.Add(visitor); city.VisitorRoutines.Add(routine); }
            PersonTable.OrderVisitors(city);
        }

        public static void Link(VisitorPerson visitor, VisitorRoutine routine)
        {
            if (!string.Equals(visitor.Id, routine.Identity.Id, StringComparison.Ordinal)) throw new InvalidOperationException("Visitor routine identity differs from its person.");
            visitor.Routine = routine; routine.Visitor = visitor;
        }

        public static void LinkAll(CityState city, IdentityPolicy? identities = null)
        {
            if (city.Visitors.Count != city.VisitorRoutines.Count) throw new InvalidOperationException("Every visitor has exactly one routine.");
            if (city.Visitors.Count == 0) return;
            identities ??= IdentityPolicy.V1(city.SourceIdentities);
            for (int i = 0; i < city.Visitors.Count; i++)
            {
                var routine = city.VisitorRoutines[i];
                if (routine.Identity == null || routine.Identity.Id != city.Visitors[i].Id) routine.Identity = identities.Derive(city.Seed, city.Visitors[i].Id);
                routine.Traits ??= ResidentTraits.Generate(city.Seed, city.Visitors[i].Id);
                Link(city.Visitors[i], routine);
            }
        }

        public static void DeriveAll(CityState city)
        {
            if (city.Visitors.Count != city.VisitorRoutines.Count) throw new InvalidOperationException("Every visitor has exactly one routine.");
            if (city.Visitors.Count == 0) return;
            var identities = IdentityPolicy.V1(city.SourceIdentities);
            for (int i = 0; i < city.Visitors.Count; i++)
            {
                var visitor = city.Visitors[i]; var routine = city.VisitorRoutines[i];
                routine.Identity = identities.Derive(city.Seed, visitor.Id);
                routine.Traits = ResidentTraits.Generate(city.Seed, visitor.Id);
                routine.Preferences = Preferences(visitor.Id, routine.Identity.Persona, city.Seed);
                Link(visitor, routine);
            }
        }

        public static bool Dormant(CitizenState routine) => routine.PlaceId == routine.HomeId && routine.Activity == Activity.Home
            && routine.Phase != CitizenPhase.Travelling && routine.Journey == null && routine.MealOuting == null;

        public static void Sync(VisitorPerson visitor)
        {
            var routine = visitor.Routine ?? throw new InvalidOperationException("Visitor has no routine.");
            var stay = visitor.Stay;
            if (Dormant(routine))
            {
                stay.Presence = VisitorPresence.Dormant; stay.NextStayStart = routine.NextEventMinute;
                visitor.LastArrival = null; routine.RecentVisits.Clear();
            }
            else
            {
                stay.Presence = VisitorPresence.Active; stay.NextStayStart = 0;
                visitor.LastArrival = routine.RecentVisits.Count == 0 ? null : routine.RecentVisits[routine.RecentVisits.Count - 1];
            }
            if (routine.DayPlaceId.Length == 0) visitor.Employment = null;
            else
            {
                var kind = routine.Identity.Persona == Persona.Student ? EmploymentKind.Study : EmploymentKind.Work;
                var job = visitor.Employment;
                if (job == null || job.WorkplaceId != routine.DayPlaceId || job.StartDay != routine.EmploymentStartDay || job.Kind != kind)
                    visitor.Employment = new Employment { PersonId = visitor.Id, WorkplaceId = routine.DayPlaceId, Kind = kind,
                        StartDay = routine.EmploymentStartDay, ScheduleCode = PersonTable.PersonaScheduleCode };
            }
            visitor.CarriedIncomeSource = routine.VisitorCarried;
        }

        internal static void RecordArrival(CitizenState person, double minute, string placeId, Activity activity)
        {
            var visit = new VisitRecord { AtMinute = minute, PlaceId = placeId, Activity = activity };
            if (person.IsVisitor) { person.RecentVisits.Clear(); person.RecentVisits.Add(visit); return; }
            person.RecentVisits.Add(visit);
            if (person.RecentVisits.Count > 32) person.RecentVisits.RemoveAt(0);
        }

        internal static bool ArrivedForMeal(VisitorPerson visitor, string placeId, double desiredAt)
        {
            var arrival = visitor.LastArrival;
            return arrival != null && arrival.PlaceId == placeId && arrival.Activity == Activity.Shop && arrival.AtMinute >= desiredAt;
        }

        public static string? DescribePerson(CityState city, string id, long anchorReads)
        {
            CitizenState? person = null; VisitorPerson? visitor = null;
            foreach (var resident in city.Citizens) if (resident.Identity.Id == id) { person = resident; break; }
            if (person == null)
                for (int i = 0; i < city.Visitors.Count; i++)
                    if (city.Visitors[i].Id == id) { visitor = city.Visitors[i]; person = city.VisitorRoutines.Count > i ? city.VisitorRoutines[i] : null; break; }
            if (person == null) return null;
            string R(double value) => value.ToString("R", CultureInfo.InvariantCulture);
            var text = new System.Text.StringBuilder()
                .Append("id=").Append(id).Append("\nkind=").Append(visitor == null ? "resident" : "visitor")
                .Append("\nphase=").Append(person.Phase).Append(" activity=").Append(person.Activity).Append(" place=").Append(person.PlaceId)
                .Append(" destination=").Append(person.DestinationId).Append("\nnext_event=").Append(R(person.NextEventMinute))
                .Append(" activity_until=").Append(R(person.ActivityUntilMinute)).Append(" journeys=").Append(person.CompletedJourneys)
                .Append("\nhome=").Append(person.HomeId).Append(" day_place=").Append(person.DayPlaceId);
            if (visitor != null) text.Append("\nvisitor=").Append(Describe(visitor)).Append(" next_stay=").Append(R(visitor.Stay.NextStayStart));
            var meal = city.Food?.Residents.Find(m => m.ResidentId == id);
            if (meal != null)
                text.Append("\nmeal=").Append(meal.Kind).Append(' ').Append(meal.Status).Append(" day=").Append(meal.Day)
                    .Append(" place=").Append(meal.PlaceId).Append(" purchases=").Append(meal.Purchases)
                    .Append(meal.Budget == null ? "" : " income=" + meal.Budget.IncomeSource + " balance_units=" + meal.Budget.Balance.Units.ToString(CultureInfo.InvariantCulture));
            var accrual = person.Accrual;
            var shift = EmploymentWages.ActiveWindow(person, city.Seed, System.Linq.Enumerable.ToDictionary(city.Places, p => p.Id, StringComparer.Ordinal));
            int attended = accrual.AttendedMinutes;
            if (shift.HasValue && EmploymentWages.Active(accrual) && accrual.Shift != EmploymentWages.Closed)
                attended += EmploymentWages.CurrentMinutes(person, shift.Value, city.Minute);
            text.Append("\nwage_attended_minutes=").Append(attended)
                .Append(" wage_remainder_units=").Append(accrual.UnpostedWage.Units)
                .Append(" wage_day=").Append(accrual.Day).Append(" wage_stage=").Append(accrual.Shift)
                .Append(" misses_14_days=").Append(EmploymentWages.RecentMisses(accrual, (int)(city.Minute / 1440)))
                .Append(" employment_income_units=").Append(meal?.Budget?.Ledger.EmploymentIncome.Units ?? 0)
                .Append(accrual.Shift == EmploymentWages.ClosureDay || accrual.Shift == EmploymentWages.Closed ? " workplace closed for the recorded shift" : "");
            return text.Append("\nfood_anchor_reads=").Append(anchorReads).ToString();
        }

        public static string Describe(VisitorPerson visitor) => visitor.Id + " " + visitor.Stay.Presence.ToString().ToLowerInvariant()
            + " portal=" + visitor.Stay.OriginPortalId + (visitor.Employment == null ? "" : " work=" + visitor.Employment.WorkplaceId)
            + (visitor.CarriedIncomeSource.HasValue ? " carried=" + visitor.CarriedIncomeSource.Value.ToString() : "")
            + (visitor.LastArrival == null ? "" : " arrival=" + visitor.LastArrival.PlaceId + "@" + visitor.LastArrival.AtMinute.ToString("R", CultureInfo.InvariantCulture));
    }
}
