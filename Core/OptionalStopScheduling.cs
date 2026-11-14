using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PersistentCity.Core
{
    public sealed class OptionalStopState
    {
        public DwellKind Kind { get; set; }
        public string PlaceId { get; set; } = "";
        public string SpotId { get; set; } = "";
        public string TargetId { get; set; } = "";
        public Position Position { get; set; }
        public Position Facing { get; set; }
        public Position ReturnPosition { get; set; }
        public double StartMinute { get; set; }
        public double EndMinute { get; set; }
        public double VisitMinute { get; set; }
        public double ResumeEventMinute { get; set; }
        public bool Cancelled { get; set; }
        public List<string> Members { get; set; } = new List<string>();
        public bool ActiveAt(double minute) => !Cancelled && minute >= StartMinute && minute < EndMinute;
        public OptionalStopState Copy() => new OptionalStopState { Kind = Kind, PlaceId = PlaceId,
            SpotId = SpotId, TargetId = TargetId, Position = Position, Facing = Facing, ReturnPosition = ReturnPosition,
            StartMinute = StartMinute, EndMinute = EndMinute, VisitMinute = VisitMinute,
            ResumeEventMinute = ResumeEventMinute, Cancelled = Cancelled, Members = new List<string>(Members) };
    }

    public sealed class OptionalStopSpot
    {
        public string Id { get; set; } = "";
        public string TargetId { get; set; } = "";
        public Position Position { get; set; }
        public Position Facing { get; set; }
    }

    public enum OptionalStopAvailability { Unavailable, Deferred, Ready }

    public interface IOptionalStopEnvironment
    {
        bool Raining { get; }
        IReadOnlyList<OptionalStopSpot> Spots(Place place, DwellKind kind);
        OptionalStopAvailability Available(OptionalStopState stop);
    }

    public static class OptionalStopPolicy
    {
        public static int ApprovedChoicePercentage => 25;

        public static double Minimum(DwellKind kind)
        {
            switch (kind)
            {
                case DwellKind.Break: return 5;
                case DwellKind.WindowShopping: return 1;
                case DwellKind.ChatOutsideVenue: return 10;
                case DwellKind.Resting: return 10;
                case DwellKind.Audience: return 2;
                default: throw new ArgumentException("Not an optional stop.", nameof(kind));
            }
        }
        public static double Duration(string key, DwellKind kind, int seed) => Duration(key, kind, seed, DwellRules.For(kind).MaximumMinutes);
        public static double Duration(string key, DwellKind kind, int seed, double maximum) => Minimum(kind)
            + StableRandom.Hash(key + ":duration:" + kind, seed) % 10001 / 10000.0
                * (maximum - Minimum(kind));
        public static bool Choose(string key, int seed, int percentage)
        {
            if (percentage < 0 || percentage > 100) throw new ArgumentOutOfRangeException(nameof(percentage));
            return StableRandom.Hash(key + ":optional-stop", seed) % 100 < percentage;
        }
        internal static string VisitKey(CitizenState person, double at) => person.Identity.Id + ":" + person.PlaceId
            + ":" + at.ToString("R", CultureInfo.InvariantCulture);
    }

    public sealed class OptionalStopTiming
    {
        public OptionalStopTiming(double @break = 10, double chat = 30, double windowShopping = 3, double audience = 8, double resting = 40)
        {
            Break = Clamp(DwellKind.Break, @break); Chat = Clamp(DwellKind.ChatOutsideVenue, chat);
            WindowShopping = Clamp(DwellKind.WindowShopping, windowShopping); Audience = Clamp(DwellKind.Audience, audience);
            Resting = Clamp(DwellKind.Resting, resting);
        }
        public double Break { get; }
        public double Chat { get; }
        public double WindowShopping { get; }
        public double Audience { get; }
        public double Resting { get; }
        public double Maximum(DwellKind kind)
        {
            switch (kind)
            {
                case DwellKind.Break: return Break;
                case DwellKind.ChatOutsideVenue: return Chat;
                case DwellKind.WindowShopping: return WindowShopping;
                case DwellKind.Audience: return Audience;
                case DwellKind.Resting: return Resting;
                default: throw new ArgumentException("Not an optional stop.", nameof(kind));
            }
        }
        private static double Clamp(DwellKind kind, double value) => double.IsNaN(value)
            ? DwellRules.For(kind).MaximumMinutes : Math.Max(OptionalStopPolicy.Minimum(kind), Math.Min(DwellRules.For(kind).MaximumMinutes, value));
    }

    public struct OptionalStopChanceTally
    {
        public long Eligible, Drawn, Realised, HandedOn, PassedOn, Dropped;
    }

    public sealed partial class CitySimulator
    {
        private IOptionalStopEnvironment? stopEnvironment;
        private int? stopPercentage;
        private OptionalStopTiming? stopTiming;
        private static readonly IComparer<CitizenState> ById = Comparer<CitizenState>.Create((a, b) => string.CompareOrdinal(a.Identity.Id, b.Identity.Id));
        private readonly SortedSet<CitizenState> bankedChances = new SortedSet<CitizenState>(ById);
        private readonly SortedSet<CitizenState> waitingChances = new SortedSet<CitizenState>(ById);
        private readonly HashSet<CitizenState> withStops = new HashSet<CitizenState>();
        private readonly SortedSet<CitizenState> freeOfChances = new SortedSet<CitizenState>(ById);
        private readonly HashSet<CitizenState> activeStops = new HashSet<CitizenState>();
        private readonly List<CitizenState> stopScratch = new List<CitizenState>();
        private OptionalStopChanceTally chanceTally;
        public OptionalStopChanceTally OptionalStopChances => chanceTally;
        public void AttachOptionalStops(IOptionalStopEnvironment environment, int? percentage, OptionalStopTiming? timing = null)
        {
            if (percentage < 0 || percentage > 100) throw new ArgumentOutOfRangeException(nameof(percentage));
            stopEnvironment = environment ?? throw new ArgumentNullException(nameof(environment)); stopPercentage = percentage; stopTiming = timing;
            bankedChances.Clear(); waitingChances.Clear(); withStops.Clear(); freeOfChances.Clear(); activeStops.Clear(); chanceTally = default;
            foreach (var person in State.Citizens) FileOptionalStop(person);
            RefreshOptionalStops();
        }
        public OptionalStopAvailability OptionalStopStatus(OptionalStopState stop)
            => stopEnvironment?.Available(stop) ?? OptionalStopAvailability.Ready;

        public void RefreshOptionalStops()
        {
            if (stopEnvironment == null) return;
            PruneActiveStops();
            stopScratch.Clear(); foreach (var active in activeStops) stopScratch.Add(active);
            foreach (var person in stopScratch)
            {
                var stop = person.OptionalStop!;
                if (stopEnvironment.Raining || person.Phase != CitizenPhase.AtPlace || person.PlaceId != stop.PlaceId
                    || person.MealOuting != null || State.Minute >= person.ActivityUntilMinute
                    || !DailyRoutine.Open(places[person.PlaceId], State.Minute) || OptionalStopStatus(stop) == OptionalStopAvailability.Unavailable)
                    stop.Cancelled = true;
            }
            foreach (var person in stopScratch)
            {
                var stop = person.OptionalStop;
                if (stop == null || stop.Cancelled || State.Minute >= stop.EndMinute || stop.Kind != DwellKind.ChatOutsideVenue) continue;
                if (!MembersAligned(stop)) stop.Cancelled = true;
            }
            stopScratch.Clear();
        }
        private bool MembersAligned(OptionalStopState stop)
        {
            foreach (var id in stop.Members)
                if (!citizenEvents.TryGetValue(id, out var item)
                    || !CurrentCitizenSlot(item, item.Citizen) || item.Citizen.OptionalStop == null
                    || item.Citizen.OptionalStop.Cancelled || item.Citizen.OptionalStop.TargetId != stop.TargetId
                    || item.Citizen.OptionalStop.StartMinute != stop.StartMinute) return false;
            return true;
        }
        private static bool StopActive(CitizenState c, double minute)
            => c.OptionalStop != null && !c.OptionalStop.Cancelled && minute < c.OptionalStop.EndMinute;
        private Predicate<CitizenState>? inactiveStop;
        private void PruneActiveStops() => activeStops.RemoveWhere(inactiveStop ??= c => !StopActive(c, State.Minute));
        public void CancelOptionalStop(string citizenId)
        {
            if (!citizenEvents.TryGetValue(citizenId, out var item) || item.Citizen.OptionalStop == null) return;
            var stop = item.Citizen.OptionalStop; stop.Cancelled = true;
            foreach (var member in stop.Members)
                if (citizenEvents.TryGetValue(member, out var partner) && partner.Citizen.OptionalStop != null
                    && partner.Citizen.OptionalStop.TargetId == stop.TargetId && partner.Citizen.OptionalStop.StartMinute == stop.StartMinute)
                    partner.Citizen.OptionalStop.Cancelled = true;
        }
        private static bool PendingChance(CitizenState c) => c.OptionalStopChoice == true && c.OptionalStop == null;
        private static bool WaitingForPartner(CitizenState c) => c.Phase == CitizenPhase.AtPlace && c.Activity == Activity.Leisure && c.MealOuting == null;
        internal void FileOptionalStop(CitizenState person)
        {
            activeJourneys.File(person);
            if (stopEnvironment == null || person.IsVisitor) return;
            bool pending = PendingChance(person), waiting = pending && WaitingForPartner(person);
            if (pending && !waiting) bankedChances.Add(person); else bankedChances.Remove(person);
            if (waiting) waitingChances.Add(person); else waitingChances.Remove(person);
            if (person.OptionalStop != null) withStops.Add(person); else withStops.Remove(person);
            if (StopActive(person, State.Minute)) activeStops.Add(person); else activeStops.Remove(person);
            if (Free(person)) freeOfChances.Add(person); else freeOfChances.Remove(person);
        }
        private static bool Free(CitizenState c) => c.OptionalStopChoice != true && c.OptionalStop == null;
        internal bool OptionalStopIndexesMatchState()
            => bankedChances.SetEquals(State.Citizens.Where(c => PendingChance(c) && !WaitingForPartner(c)))
            && waitingChances.SetEquals(State.Citizens.Where(c => PendingChance(c) && WaitingForPartner(c)))
            && withStops.SetEquals(State.Citizens.Where(c => c.OptionalStop != null))
            && freeOfChances.SetEquals(State.Citizens.Where(Free))
            && ActiveStopIndexMatchesScan();
        internal bool ActiveStopIndexMatchesScan()
        {
            if (stopEnvironment == null) return activeStops.Count == 0;
            PruneActiveStops();
            return activeStops.SetEquals(State.Citizens.Where(c => !c.IsVisitor && StopActive(c, State.Minute)));
        }

        private void ScheduleOptionalStop(CitizenState person, Route? arrivalRoute)
        {
            double now = State.Minute;
            if (stopEnvironment == null || !stopPercentage.HasValue || person.IsVisitor || stopEnvironment.Raining || person.Phase != CitizenPhase.AtPlace
                || person.MealOuting != null || person.OptionalStop != null || person.RecentVisits.Count == 0
                || person.RecentVisits[person.RecentVisits.Count - 1].AtMinute != now
                || person.RecentVisits[person.RecentVisits.Count - 1].PlaceId != person.PlaceId) return;
            var place = places[person.PlaceId];
            if (!DailyRoutine.Open(place, now) || person.Activity == Activity.Home || person.Activity == Activity.Study) return;
            if (State.Food != null && State.Food.Shops.Any(s => s.PlaceId == person.PlaceId)) return;
            string key = OptionalStopPolicy.VisitKey(person, now);
            bool drawn = OptionalStopPolicy.Choose(key, State.Seed, stopPercentage.Value);
            chanceTally.Eligible++;
            if (drawn) chanceTally.Drawn++;
            if (stopPercentage.Value == 0) { person.OptionalStopChoice = person.OptionalStopChoice == true; return; }
            TrimPendingChances();
            bool carried = person.OptionalStopChoice == true;
            if (drawn || carried)
            {
                bool room = bankedChances.Count + waitingChances.Count < PendingChanceLimit, bank = stopPercentage.Value < 100;
                person.OptionalStopChoice = true;
                bool used = TryPublish(person, arrivalRoute, key, now, false);
                if (used) chanceTally.Realised++;
                if (!used && (!carried && !room || !bank && !WaitingForPartner(person)))
                { person.OptionalStopChoice = false; chanceTally.Dropped++; }
                if (drawn && carried && !(bank && (used || room) && PassOnChance())) chanceTally.Dropped++;
                return;
            }
            person.OptionalStopChoice = false;
            if (bankedChances.Count + waitingChances.Count == 0 || !TryPublish(person, arrivalRoute, key, now, true)) return;
            var donor = Lowest(bankedChances.Count > 0 ? bankedChances : waitingChances);
            donor.OptionalStopChoice = false; FileOptionalStop(donor);
            person.OptionalStopChoice = true;
            chanceTally.HandedOn++; chanceTally.Realised++;
        }
        private int PendingChanceLimit
            => (int)Math.Min(State.Citizens.Count, Math.Max(1L, 4L * State.Citizens.Count * stopPercentage!.Value / 100));
        private bool PassOnChance()
        {
            if (freeOfChances.Count == 0) return false;
            var recipient = Lowest(freeOfChances); recipient.OptionalStopChoice = true; FileOptionalStop(recipient);
            chanceTally.PassedOn++; return true;
        }
        private void TrimPendingChances()
        {
            int limit = PendingChanceLimit;
            while (bankedChances.Count > 0 && (stopPercentage == 100 || bankedChances.Count + waitingChances.Count > limit)) Drop(Lowest(bankedChances));
            while (waitingChances.Count > limit) Drop(Lowest(waitingChances));
        }
        private static CitizenState Lowest(SortedSet<CitizenState> set)
            => set.Min ?? throw new InvalidOperationException("Optional-stop index is unexpectedly empty.");
        private void Drop(CitizenState holder) { holder.OptionalStopChoice = false; FileOptionalStop(holder); chanceTally.Dropped++; }
        private bool TryPublish(CitizenState person, Route? arrivalRoute, string key, double now, bool taker)
        {
            DwellKind kind = person.Activity == Activity.Work ? DwellKind.Break
                : person.Activity == Activity.Shop ? DwellKind.WindowShopping : DwellKind.Resting;
            if ((person.Activity == Activity.Shop || person.Activity == Activity.Leisure) && arrivalRoute != null
                && arrivalRoute.Legs.Count > 0 && arrivalRoute.Legs.Last().Mode == TravelMode.Walk
                && TryStop(person, DwellKind.Audience, key, now, now, out var audience))
            { Publish(person, audience!); return true; }
            if (person.Activity == Activity.Leisure && TryConversation(person, now, taker)) return true;
            double end = Math.Min(person.NextEventMinute, person.ActivityUntilMinute);
            double start = kind == DwellKind.Break ? now + (end - now) / 2 : now;
            if (!TryStop(person, kind, key, now, start, out var planned)) return false;
            Publish(person, planned!); return true;
        }
        private void Publish(CitizenState person, OptionalStopState stop) { person.OptionalStop = stop; FileOptionalStop(person); }
        private bool TryStop(CitizenState person, DwellKind kind, string key, double visit, double start,
            out OptionalStopState? planned, Position? reserved = null)
        {
            planned = null;
            var place = places[person.PlaceId];
            double until = Math.Min(person.NextEventMinute, Math.Min(person.ActivityUntilMinute, DailyRoutine.NextClose(place, start) ?? start));
            double duration = Math.Min(OptionalStopPolicy.Duration(key, kind, State.Seed, stopTiming?.Maximum(kind) ?? DwellRules.For(kind).MaximumMinutes), until - start);
            if (duration < OptionalStopPolicy.Minimum(kind)) return false;
            var spots = stopEnvironment!.Spots(place, kind);
            OptionalStopSpot? best = null;
            for (int i = 0; i < spots.Count; i++)
            {
                var spot = spots[i];
                if (best != null && string.CompareOrdinal(spot.Id, best.Id) >= 0) continue;
                if (string.IsNullOrEmpty(spot.Id) || spot.Id.Length > 512 || string.IsNullOrEmpty(spot.TargetId) || spot.TargetId.Length > 512
                    || !spot.Position.IsFinite || !spot.Facing.IsFinite || Position.Distance(spot.Position, place.Position) > 100) continue;
                if (reserved.HasValue && Position.Distance(reserved.Value, spot.Position) < 0.65) continue;
                if (Occupied(spot.Position, start, start + duration)) continue;
                best = spot;
            }
            if (best == null) return false;
            var dwell = OptionalStops.Plan(person.Identity.Id, kind, best.Id, best.TargetId, start, duration, until, stopEnvironment.Raining);
            if (!dwell.HasValue) return false;
            planned = new OptionalStopState { Kind = kind, PlaceId = place.Id, SpotId = best.Id,
                TargetId = best.TargetId, Position = best.Position, Facing = best.Facing, ReturnPosition = place.Position,
                StartMinute = dwell.Value.StartMinute, EndMinute = dwell.Value.EndMinute, VisitMinute = visit, ResumeEventMinute = person.NextEventMinute };
            return true;
        }
        private bool Occupied(Position at, double start, double end)
        {
            foreach (var c in activeStops)
                if (c.OptionalStop != null && !c.OptionalStop.Cancelled && c.OptionalStop.EndMinute > start
                    && c.OptionalStop.StartMinute < end && Position.Distance(c.OptionalStop.Position, at) < 0.65) return true;
            return false;
        }
        private bool TryConversation(CitizenState arriving, double now, bool taker)
        {
            if (waitingChances.Count == 0 || taker && bankedChances.Count + waitingChances.Count < 2) return false;
            CitizenState? partner = null;
            foreach (var other in waitingChances)
            {
                if (ReferenceEquals(other, arriving) || other.PlaceId != arriving.PlaceId || other.RecentVisits.Count == 0) continue;
                double visit = other.RecentVisits[other.RecentVisits.Count - 1].AtMinute;
                if (other.RecentVisits[other.RecentVisits.Count - 1].PlaceId != other.PlaceId
                    || other.OptionalStopChoice != true) continue;
                string group = "chat:" + arriving.Identity.Id + ":" + now.ToString("R", CultureInfo.InvariantCulture);
                if (!TryStop(arriving, DwellKind.ChatOutsideVenue, group, now, now, out var first)) return false;
                if (!TryStop(other, DwellKind.ChatOutsideVenue, group, visit, now, out var second, first!.Position)) continue;
                double end = Math.Min(first!.EndMinute, second!.EndMinute);
                first.EndMinute = second.EndMinute = end;
                first.TargetId = second.TargetId = group;
                first.Facing = second.Position; second.Facing = first.Position;
                first.Members.Add(arriving.Identity.Id); first.Members.Add(other.Identity.Id);
                second.Members = new List<string>(first.Members);
                arriving.OptionalStop = first; other.OptionalStop = second;
                partner = other; break;
            }
            if (partner == null) return false;
            FileOptionalStop(arriving); FileOptionalStop(partner); chanceTally.Realised++;
            return true;
        }
    }
}
