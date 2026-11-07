using System.Collections.Generic;

namespace PersistentCity.Core
{
    public sealed partial class CitySimulator
    {
        private IWorkplaceAvailability? workplaceAvailability;
        private HashSet<string> logicalWorkplaceClosures = new HashSet<string>(System.StringComparer.Ordinal);
        public void AttachWorkplaceAvailability(IWorkplaceAvailability availability) => workplaceAvailability = availability;
        public bool SetLogicalWorkplaceClosure(string placeId, bool closed)
        {
            if (!places.TryGetValue(placeId, out var p) || p.Kind != PlaceKind.Work || PlanningPending) return false;
            if (closed) logicalWorkplaceClosures.Add(placeId); else logicalWorkplaceClosures.Remove(placeId);
            return true;
        }
        internal bool WorkplaceOpen(string placeId, double minute)
        {
            if (logicalWorkplaceClosures.Contains(placeId) || !places.TryGetValue(placeId, out var p)) return false;
            var evidence = workplaceAvailability ?? routes as IWorkplaceAvailability;
            return evidence != null ? evidence.WorkplaceOpen(placeId, minute) : DailyRoutine.Open(p, minute);
        }
    }
}
