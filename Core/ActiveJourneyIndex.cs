using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>
    /// F5f active-journey candidate index (city audit F5 #92, docs/plans/f5-population.md section 3). The derived,
    /// in-memory set of persons whose phase can make them a physical-body candidate: travelling, waiting at a door
    /// (queue planning), holding an optional stop, or at an outdoor scene. Physical selection iterates this set instead
    /// of every citizen. It holds no saved state and decides nothing: caps, leases, the candidate filters and their
    /// priority order stay with Runtime's selection, and movement authority stays with F2 #89.
    /// Visitors are indexed with their kind but withheld from <see cref="BodyCandidates"/> until F5i.
    /// </summary>
    public sealed class ActiveJourneyIndex
    {
        /// <summary>F5i switch. Visitors receive no body or lease before F2's movement contract lands (plan B5).</summary>
        public const bool VisitorsReceiveBodies = false;
        /// <summary>Prefix of native outdoor scene places (Runtime OutdoorPlaces: parks, beaches, party piers).</summary>
        public const string OutdoorScenePrefix = "scene-";

        // Swap-removal lists keep membership changes O(1) and enumeration allocation-free. Their order is not
        // roster order; selection sorts by SelectionCandidate.ByPriority, whose ordinal-ID key is total.
        private readonly List<CitizenState> eligible = new List<CitizenState>();
        private readonly List<CitizenState> withheld = new List<CitizenState>();
        // Position in its list; withheld entries are stored as ~index (negative). CitizenState has reference equality.
        private readonly Dictionary<CitizenState, int> positions = new Dictionary<CitizenState, int>();

        /// <summary>Phase-level membership. A superset of every stateful physical-selection candidate
        /// (Runtime PhysicalPopulation: moving journeys, planned queuers, Outdoor); time, distance, cooldown,
        /// reservation and binding filters stay in the selection.</summary>
        public static bool Member(CitizenState c)
            => c.Phase == CitizenPhase.Travelling || c.Phase == CitizenPhase.WaitingForOpening
            || c.Phase == CitizenPhase.AtPlace && (c.OptionalStop != null
                || c.Activity == Activity.Leisure && c.PlaceId.StartsWith(OutdoorScenePrefix, StringComparison.Ordinal));

        /// <summary>Kind from the stable person ID until F5c saves an explicit kind. IDs of no person format
        /// (older fixtures and tests) keep today's resident behaviour.</summary>
        public static PersonKind KindOf(CitizenState c)
            => PersonIds.FitsKind(PersonIds.Classify(c.Identity.Id), PersonKind.Visitor) ? PersonKind.Visitor : PersonKind.Resident;

        public static bool ReceivesBody(CitizenState c) => VisitorsReceiveBodies || KindOf(c) != PersonKind.Visitor;

        /// <summary>Indexed persons that may receive a body, in no particular order.</summary>
        public IReadOnlyList<CitizenState> BodyCandidates => eligible;
        /// <summary>Every indexed person, visitors included.</summary>
        public int Count => eligible.Count + withheld.Count;
        /// <summary>Indexed visitors withheld from body selection (zero while VisitorsReceiveBodies).</summary>
        public int WithheldVisitors => withheld.Count;
        public bool Contains(CitizenState c) => positions.ContainsKey(c);

        internal void Rebuild(IReadOnlyList<CitizenState> citizens)
        {
            eligible.Clear(); withheld.Clear(); positions.Clear();
            for (int i = 0; i < citizens.Count; i++) File(citizens[i]);
        }

        /// <summary>Called wherever a live citizen's phase, activity, place or optional stop may have changed.</summary>
        internal void File(CitizenState c)
        {
            bool member = Member(c);
            bool present = positions.TryGetValue(c, out int at);
            if (member == present) return;
            if (member)
            {
                if (ReceivesBody(c)) { positions.Add(c, eligible.Count); eligible.Add(c); }
                else { positions.Add(c, ~withheld.Count); withheld.Add(c); }
                return;
            }
            positions.Remove(c);
            if (at >= 0) RemoveAt(eligible, at, false); else RemoveAt(withheld, ~at, true);
        }

        private void RemoveAt(List<CitizenState> list, int index, bool negative)
        {
            int last = list.Count - 1;
            if (index != last)
            {
                var moved = list[last];
                list[index] = moved;
                positions[moved] = negative ? ~index : index;
            }
            list.RemoveAt(last);
        }

        /// <summary>Shadow comparison against a full scan: exactly the members of <paramref name="citizens"/>,
        /// each in the list of its kind, with consistent positions. Allocation-free; for tests and the
        /// low-cadence runtime check, never per frame.</summary>
        public bool MatchesScan(IReadOnlyList<CitizenState> citizens)
        {
            int members = 0;
            for (int i = 0; i < citizens.Count; i++)
            {
                var c = citizens[i];
                bool present = positions.TryGetValue(c, out int at);
                if (Member(c) != present) return false;
                if (!present) continue;
                members++;
                bool body = ReceivesBody(c);
                if (body ? at < 0 || at >= eligible.Count || !ReferenceEquals(eligible[at], c)
                    : at >= 0 || ~at >= withheld.Count || !ReferenceEquals(withheld[~at], c)) return false;
            }
            return members == positions.Count && members == Count;
        }
    }
}
