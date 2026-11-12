using System;
using System.Collections.Generic;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    internal sealed class StaticSceneMemory
    {
        private readonly HashSet<string> validated = new HashSet<string>(StringComparer.Ordinal);
        public int Count => validated.Count;
        public bool Admit(string key, bool active, bool gameExcluded)
        {
            if (gameExcluded) return false;
            if (validated.Contains(key)) return true;
            if (!active) return false;
            validated.Add(key); return true;
        }
        public void Forget(string key) => validated.Remove(key);
        public void Include(StaticSceneMemory other) => validated.UnionWith(other.validated);
    }

    internal readonly struct PerformerSiteReading
    {
        public PerformerSiteReading(string key, bool gameExcluded, Position center, object zone, object area)
        { Key = key; GameExcluded = gameExcluded; Center = center; Zone = zone; Area = area; }
        public string Key { get; }
        public bool GameExcluded { get; }
        public Position Center { get; }
        public object Zone { get; }
        public object Area { get; }
    }

    internal sealed class PerformerSites
    {
        public const double PassingRadius = 8;
        public const double FacingTolerance = 0.5;
        private readonly Dictionary<string, AudienceTarget> sites = new Dictionary<string, AudienceTarget>(StringComparer.Ordinal);
        private readonly List<AudienceTarget> ordered = new List<AudienceTarget>();
        private readonly Dictionary<string, (Position Origin, AudienceTarget[] Targets)> near
            = new Dictionary<string, (Position, AudienceTarget[])>(StringComparer.Ordinal);
        public int Count => sites.Count;
        public int CachedPlaces => near.Count;
        public void Replace(IEnumerable<PerformerSiteReading> readings)
        {
            sites.Clear(); ordered.Clear(); near.Clear();
            foreach (var reading in readings)
            {
                if (!reading.Center.IsFinite || reading.GameExcluded) continue;
                string id = "performer:" + reading.Key;
                if (sites.ContainsKey(id)) continue;
                var site = new AudienceTarget(reading.Zone, reading.Area, id, reading.Center);
                sites.Add(id, site); ordered.Add(site);
            }
            ordered.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
        }
        public IReadOnlyList<AudienceTarget> Near(Place place)
        {
            if (near.TryGetValue(place.Id, out var known) && known.Origin.Equals(place.Position)) return known.Targets;
            var result = new List<AudienceTarget>();
            foreach (var site in ordered)
                if (Position.Distance(place.Position, site.Position) <= PassingRadius) result.Add(site);
            var targets = result.ToArray();
            near[place.Id] = (place.Position, targets);
            return targets;
        }
        public bool Available(string targetId, Position facing)
            => sites.TryGetValue(targetId, out var site) && Position.Distance(facing, site.Position) <= FacingTolerance;
    }

    internal enum SceneScan { None, Parks, Performers }

    internal sealed class SceneScanSchedule
    {
        private bool parks = true, performers = true;
        public void SceneSetChanged() { parks = true; performers = true; }
        public void ParkTriggerLost() => parks = true;
        public bool Due(DwellKind kind) => kind == DwellKind.Audience ? performers : kind == DwellKind.Resting && parks;
        public SceneScan Next => parks ? SceneScan.Parks : performers ? SceneScan.Performers : SceneScan.None;
        public void Completed(SceneScan scan)
        {
            if (scan == SceneScan.Parks) parks = false;
            else if (scan == SceneScan.Performers) performers = false;
        }
        public static OptionalStopAvailability AfterGeometry(OptionalStopAvailability geometry, bool scanPending)
            => geometry != OptionalStopAvailability.Ready && scanPending ? OptionalStopAvailability.Deferred : geometry;
    }
}
