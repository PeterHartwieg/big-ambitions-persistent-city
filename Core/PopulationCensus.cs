using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PersistentCity.Core
{
    /// <summary>F5a census policy. Every value is provisional planning policy (F5 plan section 3,
    /// *World capacity interpretation*), not native staffing, and none changes the city.
    /// Answers to the F5 plan's section 9 questions are kept in single named constants.</summary>
    public static class PopulationCensusPolicy
    {
        public const string PolicyId = "world-census-v1";
        // Workplace area bands over native BuildingSizeData.squareMeters, read exactly as for homes.
        // Upper bounds are exclusive: an area equal to a bound belongs to the next band.
        private static readonly int[] WorkplaceBandUpperSquareMetres = { 40, 75, 150, 300, 600 };
        private static readonly int[] WorkplaceBandCapacity = { 2, 4, 6, 10, 16, 24 };
        public const int StudentsPerWorkplaceBandSlot = 2;
        public const int WorkFillPercent = 80;
        // Plan Q4 default: about 15% of the resident count visit per day.
        public const int LeisureVisitorSharePercent = 15;
        public const int DayVisitProbabilityPercent = 25;
        public const int VisitorCapacityPerResident = 2;
        public const int MaximumVisitors = 100000;
        public const int MaximumPersons = 150000;
        public const int MaximumStations = 256;
        // Equal to Persistence SaveCodec.MaxPlaces (asserted by a test; Core cannot reference Persistence).
        public const int MaximumPlaces = 20000;
        // Breakfast, lunch and dinner intents per person per day under meal rhythm v1.
        public const int MealIntentsPerPersonPerDay = 3;
        // One outbound and one return trip for each anchored day (work, study or a visit).
        public const int TripsPerAnchoredDay = 2;
        // Plan Q1 default: visitors arrive by subway and walk; no road portals or visitor cars.
        public static readonly bool VisitorsDrive = false;
        // Plan Q2 default: homes keep HousingPolicy bands and its 90% fill; the census reports that policy ID.
        // F6 contract C1: player-owned businesses are reported on their own flagged line and excluded
        // from assignable workplace capacity, because residents are not the player's native staff.
        public static readonly bool ExcludePlayerOwnedWorkplaces = true;

        public static int WorkplaceCapacity(double areaSquareMetres)
        {
            if (!Position.Finite(areaSquareMetres) || areaSquareMetres <= 0) return 0;
            for (int band = 0; band < WorkplaceBandUpperSquareMetres.Length; band++)
                if (areaSquareMetres < WorkplaceBandUpperSquareMetres[band]) return WorkplaceBandCapacity[band];
            return WorkplaceBandCapacity[WorkplaceBandCapacity.Length - 1];
        }

        public static int StudyCapacity(double areaSquareMetres) => WorkplaceCapacity(areaSquareMetres) * StudentsPerWorkplaceBandSlot;

        // Integer round-half-up of value * percent / 100 for nonnegative inputs.
        internal static long RoundPercent(long value, int percent) => (value * percent + 50) / 100;
    }

    /// <summary>Runtime's read-only load-time facts about one established business.</summary>
    public sealed class CensusWorkplace
    {
        public string WorkPlaceId { get; set; } = "";
        /// <summary>The paired study place for a school; empty otherwise.</summary>
        public string StudyPlaceId { get; set; } = "";
        /// <summary>Native interior area; zero when the catalogue has no positive area.</summary>
        public double AreaSquareMetres { get; set; }
        public bool PlayerOwned { get; set; }
        /// <summary>Native rival AI staff list length, informational only; -1 when not a rival business.</summary>
        public int NativeStaff { get; set; } = -1;
    }

    /// <summary>The three resident facts the census reads, captured on the simulation thread so the
    /// reduction can run detached while the live population keeps changing.</summary>
    public readonly struct CensusResident
    {
        public CensusResident(string homeId, string dayPlaceId, Persona persona) { HomeId = homeId ?? ""; DayPlaceId = dayPlaceId ?? ""; Persona = persona; }
        public string HomeId { get; }
        public string DayPlaceId { get; }
        public Persona Persona { get; }
    }

    public sealed class PopulationCensusInput
    {
        public IReadOnlyList<Place> Places { get; set; } = Array.Empty<Place>();
        public IReadOnlyList<CensusWorkplace> Workplaces { get; set; } = Array.Empty<CensusWorkplace>();
        public IReadOnlyList<CensusResident> Residents { get; set; } = Array.Empty<CensusResident>();
        /// <summary>Copies the resident facts the census reads. Strings are shared immutable references;
        /// no identity, journey or wallet is copied.</summary>
        public static CensusResident[] CaptureResidents(IReadOnlyList<CitizenState> citizens)
        {
            var residents = new CensusResident[citizens.Count];
            for (int i = 0; i < residents.Length; i++)
            {
                var person = citizens[i];
                residents[i] = new CensusResident(person.HomeId, person.DayPlaceId, person.Identity.Persona);
            }
            return residents;
        }

        /// <summary>Detached copies of the census fields of a place catalogue, so later live edits
        /// (hours, capacity refresh) never race the detached reduction.</summary>
        public static List<Place> CapturePlaces(IReadOnlyList<Place> places)
        {
            var copy = new List<Place>(places.Count);
            foreach (var place in places)
                if (place != null) copy.Add(new Place { Id = place.Id, Kind = place.Kind, Capacity = place.Capacity, ResidentialAreaSquareMetres = place.ResidentialAreaSquareMetres });
            return copy;
        }

        /// <summary>Discovered subway station IDs; null while discovery has not completed.</summary>
        public IReadOnlyList<string>? StationIds { get; set; }
    }

    public sealed class CensusWorkplaceLine
    {
        public string PlaceId { get; internal set; } = "";
        public double AreaSquareMetres { get; internal set; }
        public int Capacity { get; internal set; }
        public int StudyCapacity { get; internal set; }
        public int ResidentsAssigned { get; internal set; }
        public bool PlayerOwned { get; internal set; }
        public bool Matched { get; internal set; }
        public int NativeStaff { get; internal set; } = -1;
    }

    /// <summary>Read-only census of the loaded world. Counts, never targets applied to the city:
    /// computing it creates no person, workplace, wallet, save field or native call.</summary>
    public sealed class PopulationCensus
    {
        public bool Refused { get; private set; }
        public string RefusalReason { get; private set; } = "";
        public int Places { get; private set; }
        public int HomeInteriors { get; private set; }
        public long Beds { get; private set; }
        public long ResidentTarget { get; private set; }
        public int Residents { get; private set; }
        public int Households { get; private set; }
        public int OverBandHomes { get; private set; }
        public int ResidentsWithoutHome { get; private set; }
        public int Workplaces { get; private set; }
        public long WorkplaceCapacity { get; private set; }
        public int PlayerOwnedWorkplaces { get; private set; }
        public long PlayerOwnedCapacity { get; private set; }
        public int UnknownAreaWorkplaces { get; private set; }
        public int UnmatchedWorkplaces { get; private set; }
        public int OverCapacityWorkplaces { get; private set; }
        public int NativeStaffWorkplaces { get; private set; }
        public long NativeStaff { get; private set; }
        public int StudyPlaces { get; private set; }
        public long StudyCapacity { get; private set; }
        public int ResidentWorkers { get; private set; }
        public int ResidentWorkersAtPlayerOwned { get; private set; }
        public int Students { get; private set; }
        public bool StationsKnown { get; private set; }
        public int Stations { get; private set; }
        public long CommuterDemand { get; private set; }
        public long CommuterTarget { get; private set; }
        public long DayVisitorsPerDay { get; private set; }
        public long DayVisitorPoolDemand { get; private set; }
        public long DayVisitorPoolTarget { get; private set; }
        public long DayVisitorsPresent { get; private set; }
        public long VisitorCapacity { get; private set; }
        public bool CommuterClamped { get; private set; }
        public bool PoolClamped { get; private set; }
        public long PersistedPersons { get; private set; }
        public long ImpliedCommuteTrips { get; private set; }
        public long ImpliedVisitTrips { get; private set; }
        public long ImpliedMealIntents { get; private set; }
        public bool HouseholdsWithinBeds => Households <= Beds;
        public bool JobsWithinCapacity => ResidentWorkers - ResidentWorkersAtPlayerOwned <= WorkplaceCapacity;
        public bool StudentsWithinCapacity => Students <= StudyCapacity;
        public IReadOnlyList<CensusWorkplaceLine> WorkplaceLines => lines;
        private readonly List<CensusWorkplaceLine> lines = new List<CensusWorkplaceLine>();

        public static PopulationCensus Compute(PopulationCensusInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var census = new PopulationCensus();
            var places = input.Places ?? Array.Empty<Place>();
            var residents = input.Residents ?? Array.Empty<CensusResident>();
            var workplaces = input.Workplaces ?? Array.Empty<CensusWorkplace>();
            census.Places = places.Count;
            if (places.Count > PopulationCensusPolicy.MaximumPlaces) return census.Refuse("places " + places.Count + " exceed " + PopulationCensusPolicy.MaximumPlaces);
            if (residents.Count > HousingPolicy.MaximumResidents) return census.Refuse("residents " + residents.Count + " exceed " + HousingPolicy.MaximumResidents);
            if (input.StationIds != null && input.StationIds.Count > PopulationCensusPolicy.MaximumStations)
                return census.Refuse("stations " + input.StationIds.Count + " exceed " + PopulationCensusPolicy.MaximumStations);

            var kinds = new Dictionary<string, Place>(places.Count, StringComparer.Ordinal);
            int homeCount = 0, dayPlaceCount = 0;
            foreach (var place in places)
            {
                if (place != null && place.Kind == PlaceKind.Home) homeCount++;
                else if (place != null && (place.Kind == PlaceKind.Work || place.Kind == PlaceKind.Study)) dayPlaceCount++;
                if (place == null || string.IsNullOrEmpty(place.Id)) return census.Refuse("place without an ID");
                if (place.Capacity < 0) return census.Refuse("negative capacity at " + place.Id);
                if (!kinds.ContainsKey(place.Id)) kinds.Add(place.Id, place);
                else return census.Refuse("duplicate place " + place.Id);
            }
            var byWork = new Dictionary<string, CensusWorkplace>(workplaces.Count, StringComparer.Ordinal);
            var byStudy = new Dictionary<string, CensusWorkplace>(StringComparer.Ordinal);
            foreach (var workplace in workplaces)
            {
                if (workplace == null || string.IsNullOrEmpty(workplace.WorkPlaceId)) return census.Refuse("workplace without an ID");
                if (byWork.ContainsKey(workplace.WorkPlaceId)) return census.Refuse("duplicate workplace " + workplace.WorkPlaceId);
                byWork.Add(workplace.WorkPlaceId, workplace);
                if (string.IsNullOrEmpty(workplace.StudyPlaceId)) continue;
                if (byStudy.ContainsKey(workplace.StudyPlaceId)) return census.Refuse("duplicate study place " + workplace.StudyPlaceId);
                byStudy.Add(workplace.StudyPlaceId, workplace);
            }

            // Sized once: at most one entry per occupied home or assigned day place.
            var occupancy = new Dictionary<string, int>(Math.Min(homeCount, residents.Count), StringComparer.Ordinal);
            var assigned = new Dictionary<string, int>(Math.Min(dayPlaceCount, residents.Count), StringComparer.Ordinal);
            foreach (var person in residents)
            {
                if (kinds.TryGetValue(person.HomeId ?? "", out var home) && home.Kind == PlaceKind.Home) Increment(occupancy, home.Id);
                else census.ResidentsWithoutHome++;
                if (!string.IsNullOrEmpty(person.DayPlaceId)) Increment(assigned, person.DayPlaceId);
            }
            census.Residents = residents.Count;

            foreach (var place in places)
            {
                if (place.Kind == PlaceKind.Home)
                {
                    census.HomeInteriors++;
                    census.Beds += place.Capacity;
                    int members = Count(occupancy, place.Id);
                    if (members > 0) census.Households++;
                    if (members > place.Capacity) census.OverBandHomes++;
                }
                else if (place.Kind == PlaceKind.Work)
                {
                    byWork.TryGetValue(place.Id, out var live);
                    var line = new CensusWorkplaceLine
                    {
                        PlaceId = place.Id, Matched = live != null, AreaSquareMetres = live?.AreaSquareMetres ?? 0,
                        PlayerOwned = live?.PlayerOwned ?? false, NativeStaff = live?.NativeStaff ?? -1,
                        ResidentsAssigned = Count(assigned, place.Id)
                    };
                    line.Capacity = PopulationCensusPolicy.WorkplaceCapacity(line.AreaSquareMetres);
                    if (live != null && !string.IsNullOrEmpty(live.StudyPlaceId)) line.StudyCapacity = PopulationCensusPolicy.StudyCapacity(line.AreaSquareMetres);
                    census.Workplaces++;
                    if (!line.Matched) census.UnmatchedWorkplaces++;
                    if (line.Capacity == 0) census.UnknownAreaWorkplaces++;
                    if (line.NativeStaff >= 0) { census.NativeStaffWorkplaces++; census.NativeStaff += line.NativeStaff; }
                    if (line.PlayerOwned && PopulationCensusPolicy.ExcludePlayerOwnedWorkplaces) { census.PlayerOwnedWorkplaces++; census.PlayerOwnedCapacity += line.Capacity; }
                    else
                    {
                        census.WorkplaceCapacity += line.Capacity;
                        if (line.ResidentsAssigned > line.Capacity) census.OverCapacityWorkplaces++;
                    }
                    census.lines.Add(line);
                }
                else if (place.Kind == PlaceKind.Study)
                {
                    census.StudyPlaces++;
                    if (byStudy.TryGetValue(place.Id, out var school)) census.StudyCapacity += PopulationCensusPolicy.StudyCapacity(school.AreaSquareMetres);
                }
            }
            census.lines.Sort((a, b) => StringComparer.Ordinal.Compare(a.PlaceId, b.PlaceId));

            foreach (var person in residents)
            {
                if (string.IsNullOrEmpty(person.DayPlaceId) || !kinds.TryGetValue(person.DayPlaceId, out var day)) continue;
                var persona = person.Persona;
                if (day.Kind == PlaceKind.Study && persona == Persona.Student) census.Students++;
                else if (day.Kind == PlaceKind.Work && persona != Persona.Student && persona != Persona.Retiree)
                {
                    census.ResidentWorkers++;
                    if (byWork.TryGetValue(day.Id, out var live) && live.PlayerOwned && PopulationCensusPolicy.ExcludePlayerOwnedWorkplaces)
                        census.ResidentWorkersAtPlayerOwned++;
                }
            }

            if (census.Beds > 0)
            {
                long target = HousingPolicy.Fill(census.Beds);
                if (target > HousingPolicy.MaximumResidents)
                    return census.Refuse("housing target " + target + " exceeds " + HousingPolicy.MaximumResidents);
                census.ResidentTarget = target;
            }

            census.StationsKnown = input.StationIds != null;
            if (input.StationIds != null)
            {
                var stations = new HashSet<string>(StringComparer.Ordinal);
                foreach (var id in input.StationIds) if (!string.IsNullOrEmpty(id)) stations.Add(id);
                census.Stations = stations.Count;
            }
            census.VisitorCapacity = Math.Min((long)PopulationCensusPolicy.VisitorCapacityPerResident * census.Residents, PopulationCensusPolicy.MaximumVisitors);
            long eligibleWorkers = census.ResidentWorkers - census.ResidentWorkersAtPlayerOwned;
            census.CommuterDemand = Math.Max(0, PopulationCensusPolicy.RoundPercent(census.WorkplaceCapacity, PopulationCensusPolicy.WorkFillPercent) - eligibleWorkers);
            census.DayVisitorsPerDay = PopulationCensusPolicy.RoundPercent(census.Residents, PopulationCensusPolicy.LeisureVisitorSharePercent);
            census.DayVisitorPoolDemand = (census.DayVisitorsPerDay * 100 + PopulationCensusPolicy.DayVisitProbabilityPercent - 1) / PopulationCensusPolicy.DayVisitProbabilityPercent;
            // No discovered portal means no visitors, never an invented entry point.
            if (census.Stations > 0)
            {
                census.CommuterTarget = Math.Min(census.CommuterDemand, census.VisitorCapacity);
                census.CommuterClamped = census.CommuterTarget < census.CommuterDemand;
                census.DayVisitorPoolTarget = Math.Min(census.DayVisitorPoolDemand, census.VisitorCapacity - census.CommuterTarget);
                census.PoolClamped = census.DayVisitorPoolTarget < census.DayVisitorPoolDemand;
                census.DayVisitorsPresent = Math.Min(census.DayVisitorsPerDay,
                    PopulationCensusPolicy.RoundPercent(census.DayVisitorPoolTarget, PopulationCensusPolicy.DayVisitProbabilityPercent));
            }
            census.PersistedPersons = census.Residents + census.CommuterTarget + census.DayVisitorPoolTarget;
            if (census.PersistedPersons > PopulationCensusPolicy.MaximumPersons)
                return census.Refuse("persons " + census.PersistedPersons + " exceed " + PopulationCensusPolicy.MaximumPersons);
            census.ImpliedCommuteTrips = PopulationCensusPolicy.TripsPerAnchoredDay * (census.ResidentWorkers + census.Students + census.CommuterTarget);
            census.ImpliedVisitTrips = PopulationCensusPolicy.TripsPerAnchoredDay * census.DayVisitorsPresent;
            census.ImpliedMealIntents = PopulationCensusPolicy.MealIntentsPerPersonPerDay * (census.Residents + census.CommuterTarget + census.DayVisitorsPresent);
            return census;
        }

        /// <summary>Canonical summary lines; identical inputs in any order give identical text.</summary>
        public string Summary()
        {
            var text = new StringBuilder(1024);
            void Line(string key, string value) => text.Append(key).Append('=').Append(value).Append('\n');
            void Number(string key, long value) => Line(key, value.ToString(CultureInfo.InvariantCulture));
            Line("policy", PopulationCensusPolicy.PolicyId);
            if (Refused) { Line("refused", RefusalReason); return text.ToString(); }
            Number("places", Places);
            Line("housing_policy", HousingPolicy.PolicyId);
            Number("homes", HomeInteriors); Number("beds", Beds); Number("resident_target", ResidentTarget);
            Number("residents", Residents); Number("households", Households);
            Number("over_band_homes", OverBandHomes); Number("residents_without_home", ResidentsWithoutHome);
            Number("workplaces", Workplaces - PlayerOwnedWorkplaces); Number("workplace_capacity", WorkplaceCapacity);
            Number("player_owned_workplaces", PlayerOwnedWorkplaces); Number("player_owned_capacity", PlayerOwnedCapacity);
            Line("player_owned_flag", PopulationCensusPolicy.ExcludePlayerOwnedWorkplaces ? "excluded-from-assignment" : "included");
            Number("unknown_area_workplaces", UnknownAreaWorkplaces); Number("unmatched_workplaces", UnmatchedWorkplaces);
            Number("over_capacity_workplaces", OverCapacityWorkplaces);
            Number("native_staff_workplaces", NativeStaffWorkplaces); Number("native_staff", NativeStaff);
            Number("study_places", StudyPlaces); Number("study_capacity", StudyCapacity);
            Number("resident_workers", ResidentWorkers); Number("resident_workers_player_owned", ResidentWorkersAtPlayerOwned);
            Number("students", Students);
            Line("discovered_stations", StationsKnown ? Stations.ToString(CultureInfo.InvariantCulture) : "pending");
            Line("visitor_arrival", PopulationCensusPolicy.VisitorsDrive ? "subway-walk-road" : "subway-walk");
            Number("commuter_demand", CommuterDemand); Number("commuter_target", CommuterTarget);
            Number("day_visitors_per_day", DayVisitorsPerDay); Number("day_visitor_pool_demand", DayVisitorPoolDemand);
            Number("day_visitor_pool_target", DayVisitorPoolTarget); Number("day_visitors_present", DayVisitorsPresent);
            Number("visitor_capacity", VisitorCapacity);
            Line("clamped", CommuterClamped ? (PoolClamped ? "commuters,pool" : "commuters") : PoolClamped ? "pool" : "none");
            Number("persisted_persons", PersistedPersons);
            Number("implied_commute_trips", ImpliedCommuteTrips); Number("implied_visit_trips", ImpliedVisitTrips);
            Number("implied_meal_intents", ImpliedMealIntents);
            Line("check_households_within_beds", HouseholdsWithinBeds ? "ok" : "failed");
            Line("check_jobs_within_capacity", JobsWithinCapacity ? "ok" : "failed");
            Line("check_students_within_capacity", StudentsWithinCapacity ? "ok" : "failed");
            Line("hash", Hash(text.ToString()));
            return text.ToString();
        }

        /// <summary>One line per workplace, ordinal by place ID.</summary>
        public string WorkplaceDetail()
        {
            var text = new StringBuilder(64 + lines.Count * 96);
            foreach (var line in lines)
                text.Append(line.PlaceId).Append(" area=").Append(line.AreaSquareMetres.ToString("R", CultureInfo.InvariantCulture))
                    .Append(" capacity=").Append(line.Capacity.ToString(CultureInfo.InvariantCulture))
                    .Append(" study=").Append(line.StudyCapacity.ToString(CultureInfo.InvariantCulture))
                    .Append(" assigned=").Append(line.ResidentsAssigned.ToString(CultureInfo.InvariantCulture))
                    .Append(" owner=").Append(line.PlayerOwned ? "player" : "other")
                    .Append(" native_staff=").Append(line.NativeStaff >= 0 ? line.NativeStaff.ToString(CultureInfo.InvariantCulture) : "unknown")
                    .Append(line.Matched ? "" : " unmatched").Append('\n');
            return text.ToString();
        }

        private static string Hash(string text) => StableRandom.Hash(text, 0).ToString("x8", CultureInfo.InvariantCulture);

        private PopulationCensus Refuse(string reason)
        {
            var refused = new PopulationCensus { Refused = true, RefusalReason = reason, Places = Places };
            return refused;
        }

        private static void Increment(Dictionary<string, int> values, string id) => values[id] = Count(values, id) + 1;
        private static int Count(Dictionary<string, int> values, string id) => values.TryGetValue(id, out var count) ? count : 0;
    }
}
