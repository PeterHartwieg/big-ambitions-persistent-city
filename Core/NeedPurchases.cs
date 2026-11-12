using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public sealed partial class CitySimulator
    {
        private IPurchaseMarket? purchases;
        private readonly List<PurchaseCalibration> calibration = new List<PurchaseCalibration>();
        public const int CalibrationRows = 256;

        public void AttachPurchases(IPurchaseMarket market) => purchases = market ?? throw new ArgumentNullException(nameof(market));
        public void DetachPurchases() => purchases = null;
        public IPurchaseMarket? Purchases => purchases;
        public string? PurchaseFault { get; private set; }
        public long ImpulsesInserted { get; private set; }
        public long PurchasesBought { get; private set; }
        public long PurchasesRefused { get; private set; }
        public PurchaseResult? LastPurchase { get; private set; }
        public IReadOnlyList<PurchaseCalibration> Calibration => calibration;

        private VisitRecord? NewNeedVisit(CitizenState live, CitizenState working)
        {
            if (purchases == null || live.IsVisitor || working.MealOuting != null || working.RecentVisits.Count == 0) return null;
            var visit = working.RecentVisits[working.RecentVisits.Count - 1];
            if (visit.AtMinute != State.Minute || visit.Activity != Activity.Shop && visit.Activity != Activity.Leisure || live.RecentVisits.Count > 0
                && ReferenceEquals(visit, live.RecentVisits[live.RecentVisits.Count - 1])) return null;
            return visit;
        }

        private void DeferMissedNeeds(CitizenState live, CitizenState working)
        {
            var needs = working.Needs; var plan = working.Plan;
            if (needs == null || plan == null || live.Plan == null || live.Plan.Day != plan.Day || live.Plan.Items.Count != plan.Items.Count) return;
            for (int i = 0; i < plan.Items.Count; i++)
            {
                var item = plan.Items[i];
                if (item.Kind != PlanItemKind.Need && item.Kind != PlanItemKind.Leisure || plan.StateOf(i) != PlanItemState.Missed || live.Plan.StateOf(i) == PlanItemState.Missed) continue;
                if (!places.TryGetValue(item.VenueId, out var venue)) continue;
                var profile = NeedModel.For(venue.BusinessType);
                var need = profile == null ? null : needs.Find(profile.Category);
                if (need != null && NeedModel.Due(need, plan.Day)) NeedModel.Defer(need, profile!, plan.Day);
            }
        }

        private void CommitNeedPurchase(CitizenState live, VisitRecord visit)
        {
            var market = purchases;
            var wallet = live.Wallet;
            if (market == null || wallet == null || !places.TryGetValue(visit.PlaceId, out var venue)) return;
            var profile = NeedModel.For(venue.BusinessType);
            if (profile == null) return;
            int day = DayPlanner.PlanDay(State.Minute);
            if (day < 0) return;
            var request = new PurchaseRequest(live.Identity.Id, venue.Id, PurchaseIntention.Need(profile.Category),
                NeedModel.Ceiling(profile, wallet), (long)Math.Floor(State.Minute));
            if (!market.Serves(venue.Id)) return;
            var need = NeedModel.Ensure(live, profile, day, State.Seed);
            bool impulse = live.Needs!.LastImpulseDay == day && live.Needs.ImpulseVenue == venue.Id;
            if (need == null || !impulse && !NeedModel.Due(need, day)) return;
            if (impulse) live.Needs.ImpulseVenue = "";
            PurchaseResult? result = null;
            market.Guarded(() =>
            {
                var native = market.Open(request, out var refusal, out var reason);
                result = native == null ? PurchaseResultFor(request, refusal, reason) : PurchaseContract.Run(request, wallet, market.Receipts, native);
                if (native != null) Calibrate(venue.Id, request.Minute / 60, native.NativeHourlyCount, result.IsBought);
            });
            if (result == null) return;
            LastPurchase = result;
            RecordPurchase(live, result, venue.Id);
            if (result.IsBought)
            {
                PurchasesBought++;
                NeedModel.Satisfy(live, need, profile, venue.Id, day, State.Seed);
                return;
            }
            if (result.Refusal == PurchaseRefusal.AlreadyRecorded || result.Refusal == PurchaseRefusal.Unsupported) return;
            PurchasesRefused++;
            if (result.Refusal == PurchaseRefusal.Uncertain)
            {
                PurchaseFault = result.Key + ": " + result.Reason;
                purchases = null;
                return;
            }
            NeedModel.Defer(need, profile, day);
            DayPlanExecutor.Mark(live, visit.Activity, live.ActivityUntilMinute, PlanItemState.Missed, PlanMissCause.Refused);
        }

        public const double ImpulseReachMetres = 25;
        public const double ImpulseVisitMinutes = 30;

        private void ImpulseOnDeparture(CitizenState live)
        {
            var market = purchases; var journey = live.Journey;
            if (market == null || live.IsVisitor || live.Wallet == null || live.Plan == null || journey == null || live.Phase != CitizenPhase.Travelling) return;
            int day = DayPlanner.PlanDay(State.Minute);
            if (day < 0 || live.Plan.Day != day || live.Needs != null && live.Needs.LastImpulseDay == day) return;
            var venues = market.Venues;
            for (int v = 0; v < venues.Count; v++)
            {
                if (venues[v] == live.DestinationId || !places.TryGetValue(venues[v], out var venue) || !DailyRoutine.Open(venue, State.Minute)) continue;
                if (!Passes(journey, venue.Position)) continue;
                var profile = NeedModel.For(venue.BusinessType);
                if (profile == null) continue;
                if (!NeedModel.Impulse(live, profile, venue.Id, day, State.Seed)) continue;
                if (NeedModel.InsertImpulse(live, venue.Id, live.NextEventMinute, ImpulseVisitMinutes)) ImpulsesInserted++;
                return;
            }
        }

        private static bool Passes(Route journey, Position at)
        {
            foreach (var leg in journey.Legs)
                if (leg.Mode == TravelMode.Walk && SegmentDistance(leg.From, leg.To, at) <= ImpulseReachMetres) return true;
            return false;
        }

        private static double SegmentDistance(Position a, Position b, Position p)
        {
            double dx = b.X - a.X, dz = b.Z - a.Z, length = dx * dx + dz * dz;
            double t = length <= 0 ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Z - a.Z) * dz) / length));
            double x = a.X + t * dx - p.X, z = a.Z + t * dz - p.Z;
            return Math.Sqrt(x * x + z * z);
        }

        private NeedPlanning? planning;
        private long planningHour = long.MinValue;

        internal NeedPlanning? NeedPlanningNow()
        {
            var market = purchases;
            if (market == null) return null;
            long hour = (long)Math.Floor(State.Minute / 60);
            if (planning != null && hour == planningHour) return planning;
            var venues = new List<Place>();
            foreach (var id in market.Venues) if (places.TryGetValue(id, out var venue) && NeedModel.For(venue.BusinessType) != null) venues.Add(venue);
            planning = new NeedPlanning(venues); planningHour = hour;
            return planning;
        }

        private static void WakeForNewPlan(CitizenState person, double now)
        {
            var outing = person.MealOuting; var plan = person.Plan;
            if (outing == null || plan == null || outing.OriginActivity != Activity.Home) return;
            double first = double.PositiveInfinity;
            for (int i = 0; i < plan.Items.Count; i++)
            {
                var item = plan.Items[i];
                if (item.Kind == PlanItemKind.Meal || DayPlan.Terminal(plan.StateOf(i)) || item.EndsAt <= now) continue;
                first = Math.Min(first, Math.Max(now, item.EarliestStart));
            }
            if (first < outing.ResumeEvent) outing.ResumeEvent = first;
            if (first < outing.ResumeUntil) outing.ResumeUntil = first;
        }

        private EssentialNeeds? essentials;
        private long essentialsHour = long.MinValue;

        internal EssentialNeeds? PurchaseEssentials()
        {
            var market = purchases;
            if (market == null) return null;
            long hour = (long)Math.Floor(State.Minute / 60);
            if (essentials != null && hour == essentialsHour) return essentials;
            var categories = new List<string>();
            foreach (var id in market.Venues)
            {
                if (!places.TryGetValue(id, out var venue)) continue;
                string category = BusinessTypes.For(venue.BusinessType).EssentialCategory;
                if (HouseholdEssentials.CanBecomeNeed(category) && !categories.Contains(category)) categories.Add(category);
            }
            categories.Sort(StringComparer.Ordinal);
            essentials = EssentialNeeds.Of(categories.ToArray()); essentialsHour = hour;
            return essentials;
        }

        private static PurchaseResult PurchaseResultFor(PurchaseRequest request, PurchaseRefusal refusal, string reason)
            => PurchaseResult.Refused(request.Key, refusal == PurchaseRefusal.None ? PurchaseRefusal.Interrupted : refusal, null, reason);

        private void Calibrate(string venue, long hour, int? native, bool bought)
        {
            int at = calibration.Count - 1;
            if (at >= 0 && calibration[at].VenueId == venue && calibration[at].Hour == hour)
            { calibration[at] = new PurchaseCalibration(venue, hour, native ?? calibration[at].NativeCount, calibration[at].ResidentOrders + (bought ? 1 : 0)); return; }
            if (calibration.Count >= CalibrationRows) calibration.RemoveAt(0);
            calibration.Add(new PurchaseCalibration(venue, hour, native, bought ? 1 : 0));
        }
    }
}
