using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PersistentCity.Core
{
    // Opening queues are planned here; OptionalStopScheduling owns the optional kinds.
    // WaitToCross remains the existing safe red-light hold, with no time cap.
    public enum DwellKind { QueueAtOpening, WaitToCross, Break, WaitForSomeone, ChatOutsideVenue, WindowShopping, Audience, Resting, WaitForPickup, WaitForBus }
    public enum DwellSpot { Door, Kerb, Stop, VenueFront, Bench, PerformerRing }
    public enum DwellFacing { Door, Crossing, Group, Shopfront, Performer, Street }

    /// <summary>One bounded stand: who, why, where, from when, until when, facing what.</summary>
    public readonly struct Dwell
    {
        public Dwell(string citizenId, DwellKind kind, string spotId, double startMinute, double endMinute, string facingId)
        {
            if (string.IsNullOrEmpty(citizenId)) throw new ArgumentException("A dwell needs a citizen.", nameof(citizenId));
            if (!Position.Finite(startMinute) || !Position.Finite(endMinute) || endMinute <= startMinute
                || endMinute - startMinute > DwellRules.For(kind).MaximumMinutes + 1e-9)
                throw new ArgumentOutOfRangeException(nameof(endMinute), "A dwell must end within its kind's maximum.");
            CitizenId = citizenId; Kind = kind; SpotId = spotId ?? ""; StartMinute = startMinute; EndMinute = endMinute; FacingId = facingId ?? "";
        }
        public string CitizenId { get; }
        public DwellKind Kind { get; }
        public DwellSpot Spot => DwellRules.For(Kind).Spot;
        /// <summary>The place or crossing whose spots this dwell uses.</summary>
        public string SpotId { get; }
        public double StartMinute { get; }
        public double EndMinute { get; }
        public DwellFacing Facing => DwellRules.For(Kind).Facing;
        /// <summary>What the citizen faces: a place id for a door, a crossing id for a kerb.</summary>
        public string FacingId { get; }
        public bool ActiveAt(double minute) => minute >= StartMinute && minute < EndMinute;
    }

    public readonly struct DwellRule
    {
        public DwellRule(DwellSpot spot, DwellFacing facing, double maximumMinutes, bool optional)
        { Spot = spot; Facing = facing; MaximumMinutes = maximumMinutes; Optional = optional; }
        public DwellSpot Spot { get; }
        public DwellFacing Facing { get; }
        /// <summary>Hard cap. No dwell of this kind lasts longer. Infinite for kinds that end only on an event.</summary>
        public double MaximumMinutes { get; }
        /// <summary>Optional kinds are dropped in rain.</summary>
        public bool Optional { get; }
    }

    public static class DwellRules
    {
        public static DwellRule For(DwellKind kind)
        {
            switch (kind)
            {
                case DwellKind.QueueAtOpening: return new DwellRule(DwellSpot.Door, DwellFacing.Door, DwellPlanner.MaximumQueueLeadMinutes, false);
                // A walker waits for a safe crossing however long the light takes. It ends on the light, not on a clock.
                case DwellKind.WaitToCross: return new DwellRule(DwellSpot.Kerb, DwellFacing.Crossing, double.PositiveInfinity, false);
                case DwellKind.Break: return new DwellRule(DwellSpot.Door, DwellFacing.Street, 10, true);
                case DwellKind.WaitForSomeone: return new DwellRule(DwellSpot.VenueFront, DwellFacing.Street, 15, false);
                case DwellKind.ChatOutsideVenue: return new DwellRule(DwellSpot.VenueFront, DwellFacing.Group, 30, true);
                case DwellKind.WindowShopping: return new DwellRule(DwellSpot.VenueFront, DwellFacing.Shopfront, 3, true);
                case DwellKind.Audience: return new DwellRule(DwellSpot.PerformerRing, DwellFacing.Performer, 8, true);
                case DwellKind.Resting: return new DwellRule(DwellSpot.Bench, DwellFacing.Street, 40, true);
                case DwellKind.WaitForPickup: return new DwellRule(DwellSpot.Kerb, DwellFacing.Street, 10, false);
                case DwellKind.WaitForBus: return new DwellRule(DwellSpot.Stop, DwellFacing.Street, 10, false);
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }

    /// <summary>
    /// Pure dwell decisions. The same citizen, day and inputs always give the same dwell, so a
    /// reload, a camera move or a different frame cadence never changes who stands where or when.
    /// </summary>
    public static class DwellPlanner
    {
        public const double MaximumQueueLeadMinutes = 10;
        public const double MinimumQueueLeadMinutes = 2;
        public const double RainQueueLeadMinutes = 5;

        /// <summary>
        /// A citizen who reached a closed destination queues at its door only for the last few
        /// minutes before it opens. Earlier, and for anyone waiting elsewhere, the wait is indoors.
        /// In rain fewer queue and for less time.
        /// </summary>
        public static Dwell? QueueAtOpening(CitizenState person, Place place, int citySeed, double minute, bool raining, double rainLeadMinutes = RainQueueLeadMinutes)
        {
            if (person == null) throw new ArgumentNullException(nameof(person));
            if (place == null) throw new ArgumentNullException(nameof(place));
            // Customers queue. Staff and students wait inside for their own start, which the simulator
            // may set later than the opening (CitySimulator.AdmissionFrom).
            if (person.Phase != CitizenPhase.WaitingForOpening
                || (person.DestinationActivity != Activity.Shop && person.DestinationActivity != Activity.Leisure)
                || person.PlaceId != place.Id || person.DestinationId != place.Id || minute >= person.ActivityUntilMinute) return null;
            double? opening = DailyRoutine.NextOpen(place, minute, person.ActivityUntilMinute);
            if (!opening.HasValue || opening.Value <= minute) return null;
            string key = person.Identity.Id + ":queue:" + place.Id + ":" + Day(opening.Value);
            uint dice = StableRandom.Hash(key, citySeed);
            if (raining && (dice & 1) == 0) return null;
            double maximum = raining ? Math.Max(MinimumQueueLeadMinutes,Math.Min(MaximumQueueLeadMinutes,rainLeadMinutes)) : MaximumQueueLeadMinutes;
            double lead = MinimumQueueLeadMinutes + (dice >> 1) % 1000 / 999.0 * (maximum - MinimumQueueLeadMinutes);
            return new Dwell(person.Identity.Id, DwellKind.QueueAtOpening, place.Id, opening.Value - lead, opening.Value, place.Id);
        }

        /// <summary>
        /// Generic placement for kinds bounded by a window [from, until): a deterministic length
        /// within the kind's cap and a start within the window. Optional kinds are dropped in rain.
        /// </summary>
        public static Dwell? WithinWindow(string citizenId, DwellKind kind, string spotId, int citySeed, double fromMinute, double untilMinute, bool raining)
        {
            if (!Position.Finite(fromMinute) || !Position.Finite(untilMinute)) throw new ArgumentOutOfRangeException(nameof(untilMinute));
            var rule = DwellRules.For(kind);
            if (raining && rule.Optional) return null;
            double room = untilMinute - fromMinute;
            if (room <= 0) return null;
            uint dice = StableRandom.Hash(citizenId + ":" + kind + ":" + spotId + ":" + Day(fromMinute), citySeed);
            double length = Math.Min(room, rule.MaximumMinutes * (0.5 + 0.5 * (dice % 1000) / 999.0));
            double start = fromMinute + (room - length) * ((dice >> 10) % 1000) / 999.0;
            double end = Math.Min(untilMinute, start + length);
            // A window too small to represent at this minute's precision has no dwell.
            return end > start ? new Dwell(citizenId, kind, spotId, start, end, spotId) : (Dwell?)null;
        }

        /// <summary>
        /// Spots in one door's line. A body already standing in the line keeps its spot until it
        /// leaves, even if a weather change has since dropped it from the plan; nobody is placed on
        /// an occupied spot. Planned newcomers fill the free spots in order of planned start, then id.
        /// Places that share a door share one line.
        /// </summary>
        public static Dictionary<string, int> AssignQueueSlots(IEnumerable<KeyValuePair<string, int>> standing, IEnumerable<Dwell> planned, int capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            var slots = new Dictionary<string, int>(StringComparer.Ordinal);
            var taken = new HashSet<int>();
            foreach (var pair in standing.OrderBy(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal))
                if (pair.Value >= 0 && pair.Value < capacity && !slots.ContainsKey(pair.Key) && taken.Add(pair.Value)) slots.Add(pair.Key, pair.Value);
            // Newcomers join at the back of the line, behind everyone already in it. Gaps nearer the
            // door are filled only once the back is full.
            int back = taken.Count == 0 ? 0 : taken.Max() + 1;
            foreach (var dwell in planned.Where(d => d.Kind == DwellKind.QueueAtOpening)
                .OrderBy(d => d.StartMinute).ThenBy(d => d.CitizenId, StringComparer.Ordinal))
            {
                if (slots.ContainsKey(dwell.CitizenId)) continue;
                int slot = back;
                while (slot < capacity && taken.Contains(slot)) slot++;
                if (slot >= capacity) { slot = 0; while (slot < capacity && taken.Contains(slot)) slot++; }
                if (slot >= capacity) break;
                slots.Add(dwell.CitizenId, slot); taken.Add(slot);
                if (slot >= back) back = slot + 1;
            }
            return slots;
        }

        /// <summary>
        /// The spot a standing body should move up to: the free spot nearest the door, ahead of its
        /// own, that nobody holds. Null when nothing ahead is free. Callers apply one move at a time
        /// and only after the body accepts the walk, so a failed move keeps its old spot reserved.
        /// </summary>
        public static int? MoveUp(IReadOnlyCollection<int> occupied, int current)
        {
            for (int slot = 0; slot < current; slot++)
                if (!occupied.Contains(slot)) return slot;
            return null;
        }

        /// <summary>One queue line per entrance: places with the same entrance point share it.</summary>
        public static string DoorKey(Position entrance) => entrance.X.ToString("F1", CultureInfo.InvariantCulture) + ","
            + entrance.Y.ToString("F1", CultureInfo.InvariantCulture) + "," + entrance.Z.ToString("F1", CultureInfo.InvariantCulture);

        private static string Day(double minute) => Math.Floor(minute / 1440).ToString(CultureInfo.InvariantCulture);
    }
}
