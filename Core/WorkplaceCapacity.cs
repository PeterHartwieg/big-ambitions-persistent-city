using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>F5e (city audit F5 #92): how many persons a saved Work or Study place can seat. Replaces the fixed
    /// <c>BusinessAllocation = 16</c> with F5a's area bands over native <c>BuildingSizeData.squareMeters</c>
    /// (<see cref="PopulationCensusPolicy.WorkplaceCapacity"/>), the input the census already reports. It is simulation
    /// policy, not native staffing: no native staff list, workstation or hiring member is read or written for it.
    /// A business without a positive catalogue area seats nobody (no capacity is invented), and a player-owned
    /// business seats nobody, because Persistent City persons are not the player's native staff (F6 request C1,
    /// <see cref="PopulationCensusPolicy.ExcludePlayerOwnedWorkplaces"/>). Shop and leisure allocations are attraction
    /// weights for customers, not people employed, and are unchanged. Capacities are evaluated once, from the catalogue
    /// read when the city is created, and then stay with the saved places: a saved city never re-reads them. Player
    /// ownership is therefore the creation-time ownership. A business the player rents later keeps its band and the
    /// persons seated there; one the player gives up keeps capacity zero. No later ownership change reseats anyone;
    /// any reconciliation with native staffing is F6's.</summary>
    public static class WorkplaceCapacityPolicy
    {
        public const string PolicyId = "workplace-bands-v1";
        /// <summary>Evidence suffix on every Work and Study place seated under this policy.</summary>
        public const string Evidence = "native:BuildingSizeData.squareMeters;policy:" + PolicyId;

        /// <summary>Capacity of a Work place: the area band, or zero when the area is unknown or the player owns it.</summary>
        public static int Work(double areaSquareMetres, bool playerOwned)
            => playerOwned && PopulationCensusPolicy.ExcludePlayerOwnedWorkplaces ? 0 : PopulationCensusPolicy.WorkplaceCapacity(areaSquareMetres);

        /// <summary>Capacity of a school's Study place: the area band times the students per band slot.</summary>
        public static int Study(double areaSquareMetres) => PopulationCensusPolicy.StudyCapacity(areaSquareMetres);
    }

    public enum WorkforceKind { None, Office, Factory, Warehouse }

    /// <summary>GR-7 (R5): offices, factories and warehouses have no customers (J0 processing table: workforce only), so the
    /// city staffs them on their own opening hours. Their workers keep a two-shift rota drawn once from the person ID: the
    /// early shift starts at the day's opening, so its workers arrive before the doors open, and the late shift ends at the
    /// closing, so its workers leave after they shut (<see cref="DayPlanner.FitShift"/>). A day without an opening long
    /// enough for a whole shift staffs nobody. Seats stay <see cref="WorkplaceCapacityPolicy"/>'s area band, so no workplace
    /// holds more persons than its capacity. Nothing here reads or writes native staff, production or wages.</summary>
    public static class WorkplaceStaffing
    {
        public const string PolicyId = "workforce-venue-hours-v1";
        public const double ShiftMinutes = 480;

        // Catalogue names read from the loaded game data (12 Nov 2026); a modded or unlisted type keeps the ordinary shifts.
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

        /// <summary>A Work place whose business is an office, factory or warehouse.</summary>
        public static bool IsWorkforceVenue(Place place) => place.Kind == PlaceKind.Work && KindOf(place.BusinessType) != WorkforceKind.None;

        /// <summary>The person's rota shift: early or late, drawn once from the person ID (seed 0, like the days off).</summary>
        public static ShiftType Rota(string personId)
            => (StableRandom.Finish(StableRandom.Append(StableRandom.Append(StableRandom.Begin(0), personId ?? ""), ":rota")) & 1) == 0
                ? ShiftType.Early : ShiftType.Late;

        /// <summary>True when the venue opens on calendar day <paramref name="day"/> long enough for a whole shift that starts
        /// that day (the shift rule of <see cref="DayPlanner.FitShift"/>).</summary>
        public static bool Staffs(Place workplace, int day) => ShiftStart(workplace, day, ShiftType.Early).HasValue;

        /// <summary>The rota shift's start on <paramref name="day"/>: the early shift at the first whole-shift opening of the
        /// day, the late shift so that it ends when that opening closes. Null when the day has no such opening.</summary>
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

    /// <summary>F5e capacity bookkeeping: persons seated per Work/Study place (residents and visitor routines), kept by
    /// the simulator so a successful job-finding roll checks vacancies in O(1) per place instead of recounting every
    /// person. It is derived state, never saved: rebuilt from the city when a simulator is built or visitors are
    /// scheduled, and moved only where an event commits a changed DayPlaceId. A full recount must always equal it; if a
    /// commit ever finds it out of step, it recounts before the next vacancy check instead of guessing.</summary>
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

        /// <summary>One person's committed DayPlaceId change.</summary>
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

        /// <summary>True when the index equals a full recount of the city (tests and diagnostics).</summary>
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
