using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PersistentCity.Core
{
    // A saved overlay on an admitted activity. The original activity, deadline and next event
    // remain the continuation; no synthetic visit, journey, purchase or identity is created.
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

    // Runtime supplies only validated, reachable, off-flow geometry. Selection never reads a camera.
    public enum OptionalStopAvailability { Unavailable, Deferred, Ready }

    public interface IOptionalStopEnvironment
    {
        bool Raining { get; }
        IReadOnlyList<OptionalStopSpot> Spots(Place place, DwellKind kind);
        OptionalStopAvailability Available(OptionalStopState stop);
    }

    public static class OptionalStopPolicy
    {
        // Peter approved 25% in DECISIONS-3: the target share of eligible visits that visibly get a
        // scheduled stop, capped by feasibility (option B, 29 October). Settings affect only new visits.
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

    /// <summary>Chance accounting since the last attach (a load, or a route rebuild after the travel clock rate
    /// changes): Drawn = Realised + change in pending chances + Dropped. Eligible counts eligible visits, the
    /// share's denominator. A conversation realises two stops, one of them from its partner's pending chance.
    /// HandedOn and PassedOn count chances moved to another citizen by a hand-on or a repeated visit.</summary>
    public struct OptionalStopChanceTally
    {
        public long Eligible, Drawn, Realised, HandedOn, PassedOn, Dropped;
    }

    public sealed partial class CitySimulator
    {
        private IOptionalStopEnvironment? stopEnvironment;
        private int? stopPercentage;
        private OptionalStopTiming? stopTiming;
        // Derived, in-memory indexes of saved state, rebuilt on attach and kept exact at every change.
        // Pending chances are ordered by stable identity; holders waiting at a leisure venue (possible
        // conversation partners) are kept apart so that other chances are handed on first. Citizens holding
        // neither a chance nor a stop can receive a repeated visit's second chance.
        private static readonly IComparer<CitizenState> ById = Comparer<CitizenState>.Create((a, b) => string.CompareOrdinal(a.Identity.Id, b.Identity.Id));
        private readonly SortedSet<CitizenState> bankedChances = new SortedSet<CitizenState>(ById);
        private readonly SortedSet<CitizenState> waitingChances = new SortedSet<CitizenState>(ById);
        private readonly HashSet<CitizenState> withStops = new HashSet<CitizenState>();
        private readonly SortedSet<CitizenState> freeOfChances = new SortedSet<CitizenState>(ById);
        private OptionalStopChanceTally chanceTally;
        /// <summary>Diagnostics since the last attach (not saved): eligible visits, draws, stops, moved and dropped chances.</summary>
        public OptionalStopChanceTally OptionalStopChances => chanceTally;
        public void AttachOptionalStops(IOptionalStopEnvironment environment, int? percentage, OptionalStopTiming? timing = null)
        {
            if (percentage < 0 || percentage > 100) throw new ArgumentOutOfRangeException(nameof(percentage));
            stopEnvironment = environment ?? throw new ArgumentNullException(nameof(environment)); stopPercentage = percentage; stopTiming = timing;
            bankedChances.Clear(); waitingChances.Clear(); withStops.Clear(); freeOfChances.Clear(); chanceTally = default;
            foreach (var person in State.Citizens) FileOptionalStop(person);
            // Old saves do not acquire new stops just because they were loaded.
            RefreshOptionalStops();
        }
        public OptionalStopAvailability OptionalStopStatus(OptionalStopState stop)
            => stopEnvironment?.Available(stop) ?? OptionalStopAvailability.Ready;

        public void RefreshOptionalStops()
        {
            if (stopEnvironment == null) return;
            foreach (var person in State.Citizens)
            {
                var stop = person.OptionalStop;
                if (stop == null || stop.Cancelled || State.Minute >= stop.EndMinute) continue;
                if (stopEnvironment.Raining || person.Phase != CitizenPhase.AtPlace || person.PlaceId != stop.PlaceId
                    || person.MealOuting != null || State.Minute >= person.ActivityUntilMinute
                    || !DailyRoutine.Open(places[person.PlaceId], State.Minute) || OptionalStopStatus(stop) == OptionalStopAvailability.Unavailable)
                    stop.Cancelled = true;
            }
            // A conversation requires its actual persistent partner, including after removal or cancellation.
            foreach (var person in State.Citizens)
            {
                var stop = person.OptionalStop;
                if (stop == null || stop.Cancelled || State.Minute >= stop.EndMinute || stop.Kind != DwellKind.ChatOutsideVenue) continue;
                if (stop.Members.Any(id => !citizenEvents.TryGetValue(id, out var item)
                    || !CurrentCitizenSlot(item, item.Citizen) || item.Citizen.OptionalStop == null
                    || item.Citizen.OptionalStop.Cancelled || item.Citizen.OptionalStop.TargetId != stop.TargetId
                    || item.Citizen.OptionalStop.StartMinute != stop.StartMinute)) stop.Cancelled = true;
            }
        }
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
        // Called wherever a live citizen's choice, stop, phase, activity or outing may have changed.
        internal void FileOptionalStop(CitizenState person)
        {
            if (stopEnvironment == null) return;
            bool pending = PendingChance(person), waiting = pending && WaitingForPartner(person);
            if (pending && !waiting) bankedChances.Add(person); else bankedChances.Remove(person);
            if (waiting) waitingChances.Add(person); else waitingChances.Remove(person);
            if (person.OptionalStop != null) withStops.Add(person); else withStops.Remove(person);
            if (Free(person)) freeOfChances.Add(person); else freeOfChances.Remove(person);
        }
        private static bool Free(CitizenState c) => c.OptionalStopChoice != true && c.OptionalStop == null;
        internal bool OptionalStopIndexesMatchState()
            => bankedChances.SetEquals(State.Citizens.Where(c => PendingChance(c) && !WaitingForPartner(c)))
            && waitingChances.SetEquals(State.Citizens.Where(c => PendingChance(c) && WaitingForPartner(c)))
            && withStops.SetEquals(State.Citizens.Where(c => c.OptionalStop != null))
            && freeOfChances.SetEquals(State.Citizens.Where(Free));

        private void ScheduleOptionalStop(CitizenState person, Route? arrivalRoute)
        {
            double now = State.Minute;
            if (stopEnvironment == null || !stopPercentage.HasValue || stopEnvironment.Raining || person.Phase != CitizenPhase.AtPlace
                || person.MealOuting != null || person.OptionalStop != null || person.RecentVisits.Count == 0
                || person.RecentVisits[person.RecentVisits.Count - 1].AtMinute != now
                || person.RecentVisits[person.RecentVisits.Count - 1].PlaceId != person.PlaceId) return;
            var place = places[person.PlaceId];
            if (!DailyRoutine.Open(place, now) || person.Activity == Activity.Home || person.Activity == Activity.Study) return;
            // Existing food service has priority over optional presentation at the same shop.
            if (State.Food != null && State.Food.Shops.Any(s => s.PlaceId == person.PlaceId)) return;
            string key = OptionalStopPolicy.VisitKey(person, now);
            // The setting is the share of eligible visits that visibly get a stop; each draw is one chance.
            // A chance its visit cannot use stays pending on a citizen, across later events and saves,
            // until a feasible visit uses it: the holder's own later visit, or a visit that was not drawn.
            // Feasible visits therefore fill the share left by infeasible ones, up to the feasible share.
            bool drawn = OptionalStopPolicy.Choose(key, State.Seed, stopPercentage.Value);
            chanceTally.Eligible++;
            if (drawn) chanceTally.Drawn++;
            // At 0 no new stop appears, not even from chances still pending from a save at a higher rate.
            if (stopPercentage.Value == 0) { person.OptionalStopChoice = person.OptionalStopChoice == true; return; }
            TrimPendingChances();
            bool carried = person.OptionalStopChoice == true;
            if (drawn || carried)
            {
                // A carried chance is already among the pending ones; a new pending chance needs room.
                bool room = bankedChances.Count + waitingChances.Count < PendingChanceLimit, bank = stopPercentage.Value < 100;
                person.OptionalStopChoice = true;
                bool used = TryPublish(person, arrivalRoute, key, now, false);
                if (used) chanceTally.Realised++;
                // An unused chance stays with this citizen. At 100 nothing can be handed on, so only a
                // conversation partner waiting at a leisure venue keeps one.
                if (!used && (!carried && !room || !bank && !WaitingForPartner(person)))
                { person.OptionalStopChoice = false; chanceTally.Dropped++; }
                // A citizen holds at most one chance. With a drawn and a carried chance the second passes to
                // the first citizen in identity order holding none, so repeated visits do not lose chances.
                if (drawn && carried && !(bank && (used || room) && PassOnChance())) chanceTally.Dropped++;
                return;
            }
            person.OptionalStopChoice = false;
            if (bankedChances.Count + waitingChances.Count == 0 || !TryPublish(person, arrivalRoute, key, now, true)) return;
            // The donor is chosen after planning, so a waiting holder that became this visit's conversation
            // partner (spending its own chance) is never also the donor.
            var donor = Lowest(bankedChances.Count > 0 ? bankedChances : waitingChances);
            donor.OptionalStopChoice = false; FileOptionalStop(donor);
            person.OptionalStopChoice = true;
            chanceTally.HandedOn++; chanceTally.Realised++;
        }
        // Pending chances are bounded by four rounds of draws for the whole city, and never more than one
        // per resident (every resident from 25 up). Feasible visits can then absorb backlogs that build up
        // over several days of repeated routines, while a long stretch without feasible stops cannot later
        // release more extra stops than the bound.
        private int PendingChanceLimit
            => (int)Math.Min(State.Citizens.Count, Math.Max(1L, 4L * State.Citizens.Count * stopPercentage!.Value / 100));
        private bool PassOnChance()
        {
            if (freeOfChances.Count == 0) return false;
            var recipient = Lowest(freeOfChances); recipient.OptionalStopChoice = true; FileOptionalStop(recipient);
            chanceTally.PassedOn++; return true;
        }
        // Chances inherited from a save at a higher setting are trimmed to the current bound at the first
        // eligible visit, lowest identity first and holders away from leisure venues first. At 100 only
        // waiting conversation partners keep a chance.
        private void TrimPendingChances()
        {
            int limit = PendingChanceLimit;
            while (bankedChances.Count > 0 && (stopPercentage == 100 || bankedChances.Count + waitingChances.Count > limit)) Drop(Lowest(bankedChances));
            while (waitingChances.Count > limit) Drop(Lowest(waitingChances));
        }
        // Callers check the set is non-empty; an empty set here is an index bug, not a missing chance.
        private static CitizenState Lowest(SortedSet<CitizenState> set)
            => set.Min ?? throw new InvalidOperationException("Optional-stop index is unexpectedly empty.");
        private void Drop(CitizenState holder) { holder.OptionalStopChoice = false; FileOptionalStop(holder); chanceTally.Dropped++; }
        private bool TryPublish(CitizenState person, Route? arrivalRoute, string key, double now, bool taker)
        {
            DwellKind kind = person.Activity == Activity.Work ? DwellKind.Break
                : person.Activity == Activity.Shop ? DwellKind.WindowShopping : DwellKind.Resting;
            // Arrival pedestrians are passers-by at this entrance. No trip is generated toward a performer.
            if ((person.Activity == Activity.Shop || person.Activity == Activity.Leisure) && arrivalRoute != null
                && arrivalRoute.Legs.Count > 0 && arrivalRoute.Legs.Last().Mode == TravelMode.Walk
                && TryStop(person, DwellKind.Audience, key, now, now, out var audience))
            { Publish(person, audience!); return true; }
            // A conversation spends this visit's chance and the waiting partner's own chance.
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
            // The first usable spot in ordinal id order, found without sorting or allocating.
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
        // Only citizens with a stop can occupy a spot; the answer does not depend on iteration order.
        private bool Occupied(Position at, double start, double end)
        {
            foreach (var c in withStops)
                if (c.OptionalStop != null && !c.OptionalStop.Cancelled && c.OptionalStop.EndMinute > start
                    && c.OptionalStop.StartMinute < end && Position.Distance(c.OptionalStop.Position, at) < 0.65) return true;
            return false;
        }
        private bool TryConversation(CitizenState arriving, double now, bool taker)
        {
            // A visit without its own chance needs two others: the partner's own and a donor's.
            if (waitingChances.Count == 0 || taker && bankedChances.Count + waitingChances.Count < 2) return false;
            CitizenState? partner = null;
            // Waiting partners in stable-identity order.
            foreach (var other in waitingChances)
            {
                if (ReferenceEquals(other, arriving) || other.PlaceId != arriving.PlaceId || other.RecentVisits.Count == 0) continue;
                double visit = other.RecentVisits[other.RecentVisits.Count - 1].AtMinute;
                if (other.RecentVisits[other.RecentVisits.Count - 1].PlaceId != other.PlaceId
                    || other.OptionalStopChoice != true) continue;
                string group = "chat:" + arriving.Identity.Id + ":" + now.ToString("R", CultureInfo.InvariantCulture);
                if (!TryStop(arriving, DwellKind.ChatOutsideVenue, group, now, now, out var first)) return false;
                // Choose both spots before publishing either member.
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
            // After the loop: the partner leaves the waiting index.
            FileOptionalStop(arriving); FileOptionalStop(partner); chanceTally.Realised++;
            return true;
        }
    }
}
