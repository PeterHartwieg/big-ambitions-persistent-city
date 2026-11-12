using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public static class WorkplaceCapacityPolicy
    {
        public const string PolicyId = "workplace-bands-v1";
        public const string Evidence = "native:BuildingSizeData.squareMeters;policy:" + PolicyId;

        public static int Work(double areaSquareMetres, bool playerOwned)
            => playerOwned && PopulationCensusPolicy.ExcludePlayerOwnedWorkplaces ? 0 : PopulationCensusPolicy.WorkplaceCapacity(areaSquareMetres);

        public static int Study(double areaSquareMetres) => PopulationCensusPolicy.StudyCapacity(areaSquareMetres);
    }

    public enum WorkforceKind { None, Office, Factory, Warehouse }

    public static class WorkplaceStaffing
    {
        public const string PolicyId = "workforce-venue-hours-v1";
        public const double ShiftMinutes = 480;

        private static readonly Dictionary<string, WorkforceKind> Kinds = new Dictionary<string, WorkforceKind>(StringComparer.Ordinal)
        {
            ["ba:businesstype_headquarters"] = WorkforceKind.Office,
            ["ba:businesstype_lawfirm"] = WorkforceKind.Office,
            ["ba:businesstype_webdevelopmentagency"] = WorkforceKind.Office,
            ["ba:businesstype_graphicdesigner"] = WorkforceKind.Office,
            ["ba:businesstype_marketingagency"] = WorkforceKind.Office,
            ["ba:businesstype_recruitmentagency"] = WorkforceKind.Office,
            ["ba:businesstype_eventplanningagency"] = WorkforceKind.Office,
            ["ba:businesstype_factory"] = WorkforceKind.Factory,
            ["ba:businesstype_warehouse"] = WorkforceKind.Warehouse,
            ["ba:businesstype_importexport"] = WorkforceKind.Warehouse,
        };

        public static WorkforceKind KindOf(string? businessType)
            => businessType != null && Kinds.TryGetValue(businessType, out var kind) ? kind : WorkforceKind.None;

        public static bool IsWorkforceVenue(Place place) => place.Kind == PlaceKind.Work && KindOf(place.BusinessType) != WorkforceKind.None;

        public static ShiftType Rota(string personId)
            => (StableRandom.Finish(StableRandom.Append(StableRandom.Append(StableRandom.Begin(0), personId ?? ""), ":rota")) & 1) == 0
                ? ShiftType.Early : ShiftType.Late;

        public static bool Staffs(Place workplace, int day) => ShiftStart(workplace, day, ShiftType.Early).HasValue;

        public static double? ShiftStart(Place workplace, int day, ShiftType shift)
        {
            double midnight = day * 1440.0;
            if (midnight + ShiftMinutes + 10080 > 1000000000) return null;
            foreach (var interval in DailyRoutine.Intervals(workplace, midnight))
            {
                double from = Math.Max(interval.From, midnight), last = Math.Min(interval.To - ShiftMinutes, midnight + 1439);
                if (from <= last) return shift == ShiftType.Late ? last : from;
            }
            return null;
        }
    }

    internal sealed class WorkplaceOccupancy
    {
        private readonly Dictionary<string, int> seated = new Dictionary<string, int>(StringComparer.Ordinal);
        private CityState? city;
        private bool stale;

        public void Rebuild(CityState state)
        {
            city = state; stale = false;
            seated.Clear();
            foreach (var person in PersonTable.Schedules(state)) Add(person.DayPlaceId);
        }

        public bool Vacant(Place place)
        {
            if (stale && city != null) Rebuild(city);
            return Seated(place.Id) < place.Capacity;
        }

        public void Move(string from, string to)
        {
            if (string.Equals(from, to, StringComparison.Ordinal)) return;
            if (from.Length != 0)
            {
                int count = Seated(from);
                if (count <= 0) { stale = true; return; }
                if (count == 1) seated.Remove(from); else seated[from] = count - 1;
            }
            Add(to);
        }

        public bool Matches(CityState state)
        {
            if (stale) return false;
            var other = new WorkplaceOccupancy(); other.Rebuild(state);
            if (other.seated.Count != seated.Count) return false;
            foreach (var pair in seated) if (other.Seated(pair.Key) != pair.Value) return false;
            return true;
        }

        private int Seated(string placeId) => seated.TryGetValue(placeId, out int count) ? count : 0;
        private void Add(string placeId) { if (placeId.Length != 0) seated[placeId] = Seated(placeId) + 1; }
    }
}
