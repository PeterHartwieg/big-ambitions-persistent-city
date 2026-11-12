using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>
    /// GR-3: the simulator side of needs and the purchase contract. A resident admitted for a shop visit at a venue the
    /// purchase market serves buys through the contract when the venue's need is due; a visit while the need is not due
    /// is a visit only. Everything runs at event commit on the live person, so an abandoned event never buys. Without an
    /// attached market nothing here runs and no person gains needs.
    /// </summary>
    public sealed partial class CitySimulator
    {
        private IPurchaseMarket? purchases;
        private readonly List<PurchaseCalibration> calibration = new List<PurchaseCalibration>();
        public const int CalibrationRows = 256;

        public void AttachPurchases(IPurchaseMarket market) => purchases = market ?? throw new ArgumentNullException(nameof(market));
        public void DetachPurchases() => purchases = null;
        public long PurchasesBought { get; private set; }
        public long PurchasesRefused { get; private set; }
        public PurchaseResult? LastPurchase { get; private set; }
        /// <summary>The latest native-count calibration rows (venue hour, native count, resident orders); diagnostics only.</summary>
        public IReadOnlyList<PurchaseCalibration> Calibration => calibration;

        /// <summary>The shop visit this event recorded at the commit minute, read before commit (as ordinary exchanges do).</summary>
        private VisitRecord? NewNeedVisit(CitizenState live, CitizenState working)
        {
            if (purchases == null || live.IsVisitor || working.MealOuting != null || working.RecentVisits.Count == 0) return null;
            var visit = working.RecentVisits[working.RecentVisits.Count - 1];
            if (visit.AtMinute != State.Minute || visit.Activity != Activity.Shop || live.RecentVisits.Count > 0
                && ReferenceEquals(visit, live.RecentVisits[live.RecentVisits.Count - 1])) return null;
            return visit;
        }

        /// <summary>A need item the event missed goes back to the need model as deferred (due again tomorrow).</summary>
        private void DeferMissedNeeds(CitizenState live, CitizenState working)
        {
            var needs = working.Needs; var plan = working.Plan;
            if (needs == null || plan == null || live.Plan == null || live.Plan.Day != plan.Day || live.Plan.Items.Count != plan.Items.Count) return;
            for (int i = 0; i < plan.Items.Count; i++)
            {
                var item = plan.Items[i];
                if (item.Kind != PlanItemKind.Need || plan.StateOf(i) != PlanItemState.Missed || live.Plan.StateOf(i) == PlanItemState.Missed) continue;
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
            // Ask the market first: an unsupported or native-owned venue gives the person no need state at all.
            if (!market.Serves(venue.Id)) return;
            var need = NeedModel.Ensure(live, profile, day, State.Seed);
            if (need == null || !NeedModel.Due(need, day)) return;
            PurchaseResult? result = null;
            market.Guarded(() =>
            {
                var native = market.Open(request, out var refusal, out var reason);
                result = native == null ? PurchaseResultFor(request, refusal, reason) : PurchaseContract.Run(request, wallet, market.Receipts, native);
                if (native != null) Calibrate(venue.Id, request.Minute / 60, native.NativeHourlyCount, result.IsBought);
            });
            if (result == null) return;
            LastPurchase = result;
            if (result.IsBought)
            {
                PurchasesBought++;
                NeedModel.Satisfy(live, need, profile, venue.Id, day, State.Seed);
                return;
            }
            if (result.Refusal == PurchaseRefusal.AlreadyRecorded || result.Refusal == PurchaseRefusal.Unsupported) return;
            PurchasesRefused++;
            NeedModel.Defer(need, profile, day);
            DayPlanExecutor.Mark(live, Activity.Shop, live.ActivityUntilMinute, PlanItemState.Missed, PlanMissCause.Refused);
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
