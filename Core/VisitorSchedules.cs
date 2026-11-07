using System;
using System.Collections.Generic;
using System.Globalization;

namespace PersistentCity.Core
{
    // F5d (city audit F5 #92, docs/plans/f5-population.md section 3 Independent schedules and Visitor lifecycle):
    // schema 27 gives every visitor its own routine in the one CitySimulator event queue. There is no switch-over
    // day, no host or anchor relation and no represented excursion: a fresh city runs this way from its first day.

    /// <summary>A visitor's routine (schema 27). It is an ordinary scheduler entry like a resident's, kept outside
    /// <see cref="CityState.Citizens"/> so resident-only consumers (RLO identities, bodies, cars, census) never see
    /// it. Identity and traits are derived (contract B6); the home is the visitor's portal place, where it is
    /// off-map; it owns no car, no traffic record and no recent-visit history beyond the latest arrival.</summary>
    public sealed class VisitorRoutine : CitizenState
    {
        /// <summary>The visitor person this routine belongs to (linked on creation and read).</summary>
        public VisitorPerson? Person => Visitor;
        /// <summary>The routine's carried outside income source (published to the person at each commit).</summary>
        public ResidentIncomeSource? Carried { get => VisitorCarried; set => VisitorCarried = value; }
    }

    /// <summary>A subway station read when the city's first roster is enrolled; it becomes a portal place.</summary>
    public readonly struct PortalStation
    {
        public PortalStation(string id, Position entrance) { Id = id; Entrance = entrance; }
        public string Id { get; }
        public Position Entrance { get; }
    }

    /// <summary>Portal places: zero-capacity Home-kind places at subway entrances, listed in
    /// <see cref="CityState.Portals"/>. Capacity zero keeps them out of housing, destination and workplace choice.</summary>
    public static class PortalPlaces
    {
        public const string Evidence = "portal:subway-station";

        public static bool Is(Place place) => place.Kind == PlaceKind.Home && place.Capacity == 0
            && string.Equals(place.EvidenceSource, Evidence, StringComparison.Ordinal);

        /// <summary>Adds each station as a portal place once, before any visitor exists. The portal's district is the
        /// district of the nearest saved non-portal place (ordinal-ID tie-break), so a visitor's budget uses its
        /// portal neighbourhood's mix (section 3 Resources). Station IDs that collide with a saved place are skipped.</summary>
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

    public static class VisitorSchedules
    {
        /// <summary>Visitors created with a fresh roster: K−1 per resident for the requested factor K (1–3), the
        /// population the represented consumers stood for. F5g fills toward the census target later.</summary>
        public static int FreshVisitors(int residents, int factor) => residents * Math.Max(0, factor - 1);

        /// <summary>Creates <paramref name="count"/> commuter visitors <c>pv:&lt;n&gt;</c> at the city clock. Each gets a
        /// portal by <c>Hash(seed, PersonId, "portal")</c>, a derived identity, its own preferences, shop and leisure
        /// venues, and employment seated with today's rule (closest-scoring workplace with a vacancy, counted with the
        /// residents' and earlier visitors' assignments). A worker left without a vacancy keeps an outside employer as
        /// its carried income source. Visitors start off-map at their portal (Dormant) with their first event now.
        /// Without a portal no visitor is created (missing stations never invent a portal). Returns the new routines.</summary>
        public static IReadOnlyList<VisitorRoutine> Create(CityState city, int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var created = new List<VisitorRoutine>();
            if (count == 0 || city.Portals.Count == 0) return created;
            if ((long)city.Visitors.Count + count > PersonTable.MaximumVisitors
                || (long)city.NextVisitorSequence + count > int.MaxValue) throw new InvalidOperationException("Visitor bound exceeded.");
            var places = new Dictionary<string, Place>(StringComparer.Ordinal);
            foreach (var place in city.Places) places[place.Id] = place;
            var identities = IdentityPolicy.V1(city.SourceIdentities);
            var used = Population.Assignments(city);
            // Review Opus-r1-1 perf: the candidates of each kind once per call, in catalogue order, instead of scanning
            // every place three times per visitor. DestinationChoice keeps only these, so every choice is unchanged.
            var candidates = new Dictionary<PlaceKind, List<Place>>();
            foreach (var kind in new[] { PlaceKind.Work, PlaceKind.Study, PlaceKind.Shop, PlaceKind.Leisure }) candidates[kind] = new List<Place>();
            foreach (var place in city.Places)
                if (place.Capacity > 0 && candidates.TryGetValue(place.Kind, out var list)) list.Add(place);
            int day = (int)Math.Floor(city.Minute / 1440);
            for (int i = 0; i < count; i++)
            {
                string id = PersonIds.Visitor(city.NextVisitorSequence + i);
                string portalId = city.Portals[(int)(StableRandom.Hash(id + ":portal", city.Seed) % (uint)city.Portals.Count)];
                var portal = places[portalId];
                var identity = identities.Derive(city.Seed, id);
                var persona = identity.Persona;
                string work = persona == Persona.Retiree ? "" : DestinationChoice.Choose(candidates[persona == Persona.Student ? PlaceKind.Study : PlaceKind.Work], portal,
                    persona == Persona.Student ? PlaceKind.Study : PlaceKind.Work, id + ":day", city.Seed,
                    p => Population.Used(used, p.Id) < p.Capacity);
                if (work.Length != 0) used[work] = Population.Used(used, work) + 1;
                var routine = new VisitorRoutine
                {
                    Identity = identity, HomeId = portalId, PlaceId = portalId, DayPlaceId = work,
                    ShopId = DestinationChoice.Choose(candidates[PlaceKind.Shop], portal, PlaceKind.Shop, id + ":shop", city.Seed),
                    LeisureId = DestinationChoice.Choose(candidates[PlaceKind.Leisure], portal, PlaceKind.Leisure, id + ":leisure", city.Seed),
                    Activity = Activity.Home, Phase = CitizenPhase.AtPlace, NextEventMinute = city.Minute, ActivityUntilMinute = city.Minute,
                    Preferences = Preferences(id, persona, city.Seed),
                    Traits = ResidentTraits.Generate(city.Seed, id)
                };
                ResidentLifecycle.Initialize(routine, city.Seed, city.Minute);
                var visitor = new VisitorPerson { Id = id, Stay = new VisitorStay { OriginPortalId = portalId,
                    Pattern = PersonPolicy.MigratedConsumerPattern, Presence = VisitorPresence.Dormant, FirstDay = day, NextStayStart = city.Minute } };
                // An unseated worker keeps an outside employer (carried income) until its own employment outcome.
                if (work.Length == 0 && ResidentLifecycle.IsWorker(routine) && identity.Age >= 18)
                    routine.VisitorCarried = ResidentIncomeSource.ExternalEmployment;
                Link(visitor, routine);
                Sync(visitor);
                city.Visitors.Add(visitor); city.VisitorRoutines.Add(routine);
                created.Add(routine);
            }
            city.NextVisitorSequence += count;
            Order(city);
            return created;
        }

        /// <summary>A visitor's travel preferences, derived from (seed, PersonId) and never stored. Section 9 Q1 default:
        /// visitors come by subway and walk; they own no car. Tolerance and fare weight follow the resident draws.</summary>
        public static TravelPreferences Preferences(string id, Persona persona, int seed) => new TravelPreferences
        {
            OwnsCar = PersonPolicy.VisitorsOwnCars, AllowTransit = true,
            WalkingToleranceMetres = (persona == Persona.Sporty ? 2200 : persona == Persona.Retiree ? 700 : 1200)
                * (0.75 + StableRandom.Hash(id + ":walking", seed) % 51 / 100.0),
            FareWeightMinutes = (persona == Persona.Student ? 5 : 2) * (0.75 + StableRandom.Hash(id + ":fare", seed) % 51 / 100.0)
        };

        /// <summary>Restores ordinal order of the aligned visitor and routine lists after appends.</summary>
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

        /// <summary>Links every visitor with its routine (lists aligned by index) and derives the routines' identity.</summary>
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

        /// <summary>Codec read (schema 27): every routine gets the derived facts that are never stored (identity, traits,
        /// travel preferences) and is linked to its person. The lists are aligned by index.</summary>
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

        /// <summary>True while the visitor is off-map at its portal: Dormant (section 3 Visitor lifecycle). A visitor still
        /// waiting there for a route to its first destination has not arrived and stays dormant.</summary>
        public static bool Dormant(CitizenState routine) => routine.PlaceId == routine.HomeId && routine.Activity == Activity.Home
            && routine.Phase != CitizenPhase.Travelling && routine.Journey == null && routine.MealOuting == null;

        /// <summary>Publishes a committed routine onto its person record: presence and next stay start, the single
        /// <c>LastArrival</c> tuple (cleared on departure), the Employment formalizing DayPlaceId, and the carried source.</summary>
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
                // Arrival records are never edited after the arrival commit, so the tuple shares the routine's record
                // (F5d perf: no second record per visitor arrival).
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

        /// <summary>Records an admitted arrival. A resident appends to its bounded recent visits exactly as before; a
        /// visitor keeps only this latest arrival, which becomes its <c>LastArrival</c> tuple.</summary>
        internal static void RecordArrival(CitizenState person, double minute, string placeId, Activity activity)
        {
            var visit = new VisitRecord { AtMinute = minute, PlaceId = placeId, Activity = activity };
            if (person.IsVisitor) { person.RecentVisits.Clear(); person.RecentVisits.Add(visit); return; }
            person.RecentVisits.Add(visit);
            if (person.RecentVisits.Count > 32) person.RecentVisits.RemoveAt(0);
        }

        /// <summary>The visitor admission guard (section 3 Arrival evidence): the latest arrival is a Shop arrival at the
        /// meal's place no earlier than the meal's desired minute. Residents keep their RecentVisits check.</summary>
        internal static bool ArrivedForMeal(VisitorPerson visitor, string placeId, double desiredAt)
        {
            var arrival = visitor.LastArrival;
            return arrival != null && arrival.PlaceId == placeId && arrival.Activity == Activity.Shop && arrival.AtMinute >= desiredAt;
        }

        /// <summary>Read-only diagnostic of one person (resident or visitor) from stored facts only, for the debug line
        /// <c>pc population person &lt;id&gt;</c>; null if no such person exists. Never draws, plans or changes state.</summary>
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
            var shift = EmploymentWages.ActiveWindow(person, city.Seed);
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
