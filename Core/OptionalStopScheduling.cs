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
    public interface IOptionalStopEnvironment
    {
        bool Raining { get; }
        IReadOnlyList<OptionalStopSpot> Spots(Place place, DwellKind kind);
        bool Available(OptionalStopState stop);
    }

    public static class OptionalStopPolicy
    {
        // DECISIONS-3 asks for the opportunity rate. Null deliberately does not infer a product choice.
        public static int? ApprovedChoicePercentage => null;

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
        public static double Duration(string key, DwellKind kind, int seed) => Minimum(kind)
            + StableRandom.Hash(key + ":duration:" + kind, seed) % 10001 / 10000.0
                * (DwellRules.For(kind).MaximumMinutes - Minimum(kind));
        public static bool Choose(string key, int seed, int percentage)
        {
            if (percentage < 0 || percentage > 100) throw new ArgumentOutOfRangeException(nameof(percentage));
            return StableRandom.Hash(key + ":optional-stop", seed) % 100 < percentage;
        }
        internal static string VisitKey(CitizenState person, double at) => person.Identity.Id + ":" + person.PlaceId
            + ":" + at.ToString("R", CultureInfo.InvariantCulture);
    }

    public sealed partial class CitySimulator
    {
        private IOptionalStopEnvironment? stopEnvironment;
        private int? stopPercentage;
        public void AttachOptionalStops(IOptionalStopEnvironment environment, int? percentage)
        {
            if (percentage < 0 || percentage > 100) throw new ArgumentOutOfRangeException(nameof(percentage));
            stopEnvironment = environment ?? throw new ArgumentNullException(nameof(environment)); stopPercentage = percentage;
            // Old saves do not acquire new stops just because they were loaded.
            RefreshOptionalStops();
        }
        public void RefreshOptionalStops()
        {
            if (stopEnvironment == null) return;
            foreach (var person in State.Citizens)
            {
                var stop = person.OptionalStop;
                if (stop == null || stop.Cancelled || State.Minute >= stop.EndMinute) continue;
                if (stopEnvironment.Raining || person.Phase != CitizenPhase.AtPlace || person.PlaceId != stop.PlaceId
                    || person.MealOuting != null || State.Minute >= person.ActivityUntilMinute
                    || !DailyRoutine.Open(places[person.PlaceId], State.Minute) || !stopEnvironment.Available(stop))
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
            if (!OptionalStopPolicy.Choose(key, State.Seed, stopPercentage.Value)) return;
            DwellKind kind = person.Activity == Activity.Work ? DwellKind.Break
                : person.Activity == Activity.Shop ? DwellKind.WindowShopping : DwellKind.Resting;
            // Arrival pedestrians are passers-by at this entrance. No trip is generated toward a performer.
            if ((person.Activity == Activity.Shop || person.Activity == Activity.Leisure) && arrivalRoute != null
                && arrivalRoute.Legs.Count > 0 && arrivalRoute.Legs.Last().Mode == TravelMode.Walk
                && TryStop(person, DwellKind.Audience, key, now, now, out var audience))
            { person.OptionalStop = audience; return; }
            if (person.Activity == Activity.Leisure && TryConversation(person, now)) return;
            double end = Math.Min(person.NextEventMinute, person.ActivityUntilMinute);
            double start = kind == DwellKind.Break ? now + (end - now) / 2 : now;
            if (TryStop(person, kind, key, now, start, out var planned)) person.OptionalStop = planned;
        }
        private bool TryStop(CitizenState person, DwellKind kind, string key, double visit, double start,
            out OptionalStopState? planned, Position? reserved = null)
        {
            planned = null;
            var place = places[person.PlaceId];
            double until = Math.Min(person.NextEventMinute, Math.Min(person.ActivityUntilMinute, DailyRoutine.NextClose(place, start) ?? start));
            double duration = Math.Min(OptionalStopPolicy.Duration(key, kind, State.Seed), until - start);
            if (duration < OptionalStopPolicy.Minimum(kind)) return false;
            foreach (var spot in stopEnvironment!.Spots(place, kind).OrderBy(s => s.Id, StringComparer.Ordinal))
            {
                if (string.IsNullOrEmpty(spot.Id) || spot.Id.Length > 512 || string.IsNullOrEmpty(spot.TargetId) || spot.TargetId.Length > 512
                    || !spot.Position.IsFinite || !spot.Facing.IsFinite || Position.Distance(spot.Position, place.Position) > 100) continue;
                if (reserved.HasValue && Position.Distance(reserved.Value, spot.Position) < 0.65) continue;
                if (State.Citizens.Any(c => c.OptionalStop != null && !c.OptionalStop.Cancelled
                    && c.OptionalStop.EndMinute > start && c.OptionalStop.StartMinute < start + duration
                    && Position.Distance(c.OptionalStop.Position, spot.Position) < 0.65)) continue;
                var dwell = OptionalStops.Plan(person.Identity.Id, kind, spot.Id, spot.TargetId, start, duration, until, stopEnvironment.Raining);
                if (!dwell.HasValue) return false;
                planned = new OptionalStopState { Kind = kind, PlaceId = place.Id, SpotId = spot.Id,
                    TargetId = spot.TargetId, Position = spot.Position, Facing = spot.Facing, ReturnPosition = place.Position,
                    StartMinute = dwell.Value.StartMinute, EndMinute = dwell.Value.EndMinute, VisitMinute = visit, ResumeEventMinute = person.NextEventMinute };
                return true;
            }
            return false;
        }
        private bool TryConversation(CitizenState arriving, double now)
        {
            foreach (var other in State.Citizens.OrderBy(c => c.Identity.Id, StringComparer.Ordinal))
            {
                if (ReferenceEquals(other, arriving) || other.PlaceId != arriving.PlaceId || other.Phase != CitizenPhase.AtPlace
                    || other.Activity != Activity.Leisure || other.OptionalStop != null || other.MealOuting != null
                    || other.RecentVisits.Count == 0) continue;
                double visit = other.RecentVisits.Last().AtMinute;
                if (other.RecentVisits.Last().PlaceId != other.PlaceId
                    || !OptionalStopPolicy.Choose(OptionalStopPolicy.VisitKey(other, visit), State.Seed, stopPercentage.GetValueOrDefault())) continue;
                string group = "chat:" + arriving.Identity.Id + ":" + now.ToString("R", CultureInfo.InvariantCulture);
                if (!TryStop(arriving, DwellKind.ChatOutsideVenue, group, now, now, out var first)) return false;
                // Choose both spots before publishing either member.
                if (!TryStop(other, DwellKind.ChatOutsideVenue, group, visit, now, out var second, first!.Position)) return false;
                double end = Math.Min(first!.EndMinute, second!.EndMinute);
                first.EndMinute = second.EndMinute = end;
                first.TargetId = second.TargetId = group;
                first.Facing = second.Position; second.Facing = first.Position;
                first.Members.Add(arriving.Identity.Id); first.Members.Add(other.Identity.Id);
                second.Members = new List<string>(first.Members);
                arriving.OptionalStop = first; other.OptionalStop = second;
                return true;
            }
            return false;
        }
    }
}
