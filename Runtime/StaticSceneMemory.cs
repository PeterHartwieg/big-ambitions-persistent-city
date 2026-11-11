using System;
using System.Collections.Generic;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    /// <summary>F2h: native scene availability that does not depend on where the player stands. An object
    /// counts once it has been seen active and not excluded by the game; a later deactivation (native culling,
    /// pooling or anything else without a game reason) no longer withdraws it. Game reasons (DLC not owned,
    /// closed or inaccessible building) are passed on every read and keep today's rule. Keys are static scene
    /// keys, or place ids for frontages. Unity-free so tests can drive it with a scene double.</summary>
    internal sealed class StaticSceneMemory
    {
        private readonly HashSet<string> validated = new HashSet<string>(StringComparer.Ordinal);
        public int Count => validated.Count;
        public bool Admit(string key, bool active, bool gameExcluded)
        {
            if (gameExcluded) return false;
            if (validated.Contains(key)) return true;
            // Never seen active: inspection found no distance deactivation at first discovery, so this is a
            // game reason (scene authoring or DLC) and keeps today's rule.
            if (!active) return false;
            validated.Add(key); return true;
        }
        /// <summary>A rebind to a different native owner validates afresh.</summary>
        public void Forget(string key) => validated.Remove(key);
        /// <summary>Retimed routes keep the same save-scoped scene evidence.</summary>
        public void Include(StaticSceneMemory other) => validated.UnionWith(other.validated);
    }

    /// <summary>One street-performer spawner as the scene reports it. Center is the spawner's authored performer
    /// area (its position giver), never a spawned performer. It carries no activity: a spawner can be inactive
    /// because of player state (CityMapHider hides city objects while the player is indoors or the map is open),
    /// so only game reasons exclude a site.</summary>
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

    /// <summary>F2h: audience targets from static spawner locations. The native performer is spawned only while
    /// its spawner is in the 60 m culling band around the player, by chance and at a random point, so neither
    /// its presence nor its position decides which audience stops exist or survive. A visible audience member
    /// waits facing the spot when no performer is out (design default Q4).</summary>
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
        /// <summary>Replaces the catalogue from a scene read. Called when the loaded scene set changes, not per query.</summary>
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
        /// <summary>Sites whose performer area is within passing distance of this entrance.</summary>
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
        /// <summary>The saved stop still names a catalogued site and faces its static performer area.</summary>
        public bool Available(string targetId, Position facing)
            => sites.TryGetValue(targetId, out var site) && Position.Distance(facing, site.Position) <= FacingTolerance;
    }

    internal enum SceneScan { None, Parks, Performers }

    /// <summary>P2: which whole-scene optional-stop scans are pending. A scene-set change or a destroyed park trigger
    /// marks a cache pending at once, before any availability or spot query reads it; the flag clears only when that
    /// scan has run, so a frame spent on another scan leaves it pending. Unity-free so tests can drive it.</summary>
    internal sealed class SceneScanSchedule
    {
        private bool parks = true, performers = true;
        public void SceneSetChanged() { parks = true; performers = true; }
        public void ParkTriggerLost() => parks = true;
        /// <summary>Frontage kinds are scheduled by the frontage rebind, not here.</summary>
        public bool Due(DwellKind kind) => kind == DwellKind.Audience ? performers : kind == DwellKind.Resting && parks;
        public SceneScan Next => parks ? SceneScan.Parks : performers ? SceneScan.Performers : SceneScan.None;
        public void Completed(SceneScan scan)
        {
            if (scan == SceneScan.Parks) parks = false;
            else if (scan == SceneScan.Performers) performers = false;
        }
    }
}
