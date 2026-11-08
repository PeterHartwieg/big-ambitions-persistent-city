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

        /// <summary>Persons seated at a place (F5g staging adds its own staged seats to this).</summary>
        public int Count(string placeId)
        {
            if (stale && city != null) Rebuild(city);
            return Seated(placeId);
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
