using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PersistentCity.Core
{
    public sealed class RivalFoodSettings
    {
        public double RivalChoiceWeight { get; set; } = 1;
        public int RivalAssumedServiceMinutes { get; set; } = 5;
        public int RivalRefreshGameMinutes { get; set; } = 60;
        public int RivalReadsPerUnit { get; set; } = 16;
        public void Clamp(Action<string>? log = null)
        {
            double Value(double v, double min, double max, double fallback, string key)
            {
                double result = double.IsNaN(v) ? fallback : Math.Max(min, Math.Min(max, v));
                if (result != v) log?.Invoke(key + " clamped/defaulted to " + result.ToString(CultureInfo.InvariantCulture));
                return result;
            }
            int Integer(int v, int min, int max, int fallback, string key)
            {
                if (v == int.MinValue) { log?.Invoke(key + " defaulted to " + fallback); return fallback; }
                return (int)Value(v, min, max, fallback, key);
            }
            RivalChoiceWeight = Value(RivalChoiceWeight, 0, 2, 1, nameof(RivalChoiceWeight));
            RivalAssumedServiceMinutes = Integer(RivalAssumedServiceMinutes, 1, 30, 5, nameof(RivalAssumedServiceMinutes));
            RivalRefreshGameMinutes = Integer(RivalRefreshGameMinutes, 15, 240, 60, nameof(RivalRefreshGameMinutes));
            RivalReadsPerUnit = Integer(RivalReadsPerUnit, 1, 64, 16, nameof(RivalReadsPerUnit));
        }
    }

    /// <summary>Read-only, coherent metadata and bounded live detail reads. Never supplies sale authority.
    /// Refresh is driven only by logical-event preparation, in the existing continuation budget.</summary>
    public interface IRivalFoodMarket
    {
        long RivalRevision { get; }
        void CloseRivals();
        bool NeedsRivalRefresh(double minute);
        void ContinueRivalRefresh(double minute);
        IEnumerable<FoodScope> RivalScopes { get; }
        bool IsRivalCandidate(FoodScope scope);
        bool RivalAtPlace(string place);
        FoodAlternativeObservation? ReadRival(string businessId);
    }

    public static class RivalFoodTerms
    {
        // A 112-bit SHA-256 prefix of the canonical advertised owner/hours (not an identity).
        // Exact price is retained separately; no second schedule/receipt/route tree is saved.
        public static string Fingerprint(FoodAlternativeObservation observation)
        {
            var text = new StringBuilder().Append(observation.OwnerId.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(observation.OwnerId);
            foreach (var window in observation.Hours)
            {
                text.Append('|').Append(window.Day.ToString(CultureInfo.InvariantCulture))
                    .Append(':').Append(window.FromMinute.ToString(CultureInfo.InvariantCulture))
                    .Append(':').Append(window.ToMinute.ToString(CultureInfo.InvariantCulture));
            }
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
            return Convert.ToBase64String(hash, 0, 14).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
        public static bool Canonical(string value)
        {
            if (value == null || value.Length != 19) return false;
            foreach (char c in value) if (!(c >= '0' && c <= '9' || c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c == '-' || c == '_')) return false;
            // The last two bits encode zero padding, not extra noncanonical terms.
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
            return (alphabet.IndexOf(value[18]) & 3) == 0;
        }
        public static bool Eligible(FoodAlternativeObservation? o) => o != null && o.Kind == FoodAlternativeKind.Rival
            && o.Accessible && o.HasReadableDoor && !o.TemporarilyClosed && o.Hours.Count > 0 && o.Hours.Count <= 64 && o.Offers.Count > 0 && o.Offers.Count <= FoodChoicePolicy.MaximumOffersPerShop;
        public static FoodBusiness Capture(FoodAlternativeObservation o, int serviceMinutes)
        {
            if(!Eligible(o) || serviceMinutes < 1 || serviceMinutes > 30) throw new ArgumentException("Invalid estimated rival service.");
            return new FoodBusiness {
            Id = o.Id, PlaceId = o.PlaceId, AlternativeKind = FoodAlternativeKind.Rival, RivalTerms = Fingerprint(o),
            RivalServiceMinutes = serviceMinutes, CustomerServicePercent = o.CustomerServicePercent,
            ServiceEvidence = "Estimated availability from advertised price/hours; native stock and capacity unknown",
            Hours = o.Hours.Select(h => new OpeningWindow { Day = h.Day, FromMinute = h.FromMinute, ToMinute = h.ToMinute }).ToList(),
            Offers = o.Offers.Select(p => new FoodOffer { ProductId = p.ProductId, Price = p.Price, Units = null }).ToList()
            };
        }
    }

    /// <summary>Pure own-ledger transaction. All arithmetic and constraints precede publication;
    /// no callback, yield, receipt or native operation can interleave with these assignments.</summary>
    public static class ExternalFoodSettlement
    {
        public static bool CanPay(ResidentMeal meal, Money price)
        {
            if (price <= Money.Zero || price > Money.FromCents(Money.MaximumNativePriceCents)
                || meal.Purchases == long.MaxValue || meal.ExistingStop && meal.ExistingStopPurchases == long.MaxValue
                || !Money.TryAdd(meal.Spent, price, out _)) return false;
            if (meal.Budget == null) return price <= Money.Max(Money.Zero, meal.Allowance - meal.DayAllowanceUsed)
                && Money.TryAdd(meal.DayAllowanceUsed, price, out _) && Money.TryAdd(meal.LegacyExternalFoodSpent, price, out _);
            return price <= HouseholdFinance.SpendingLimit(meal.Budget)
                && Money.TryAdd(meal.Budget.ExternalFoodSpent, price, out _);
        }
        public static bool TryPay(ResidentMeal meal) => TryPay(meal, null, 0);
        public static bool TryPay(ResidentMeal meal, RivalOutflowHistory? history, double minute)
        {
            if (history != null && (!Position.Finite(minute) || minute < history.StartDay * 1440.0 || minute > 1000000000)) return false;
            if (meal.TargetKind != FoodAlternativeKind.Rival || meal.External.Length != 21 || meal.External[1] != '0'
                || !RivalFoodTerms.Canonical(meal.External.Substring(2))
                || meal.Status != MealStatus.Serving || !CanPay(meal, meal.Price)) return false;
            var spent = meal.Spent + meal.Price;
            var external = (meal.Budget == null ? meal.LegacyExternalFoodSpent : meal.Budget.ExternalFoodSpent) + meal.Price;
            var used = meal.Budget == null ? meal.DayAllowanceUsed + meal.Price : meal.DayAllowanceUsed;
            var balance = meal.Budget == null ? Money.Zero : meal.Budget.Balance - meal.Price;
            string watermark = "11" + meal.External.Substring(2);
            if (meal.Budget != null) { meal.Budget.Balance = balance; meal.Budget.ExternalFoodSpent = external; }
            else meal.LegacyExternalFoodSpent = external;
            meal.DayAllowanceUsed = used; meal.Spent = spent; meal.Purchases++;
            if (meal.ExistingStop) meal.ExistingStopPurchases++;
            history?.Record(meal.Price, (int)(minute / 1440));
            meal.External = watermark; meal.Status = MealStatus.Paid;
            return true;
        }
    }

    public sealed partial class FoodCommerce
    {
        internal RivalFoodSettings RivalSettings { get; private set; } = new RivalFoodSettings();
        private long RivalChoices, RivalSettlements, RivalRefusals;
        private bool rivalResumeNeedsRefresh;
        private bool NeedsRivalPreparation(double minute) => (rivalResumeNeedsRefresh
            || RivalSettings.RivalChoiceWeight > 0 && minute / 1440 >= state.RivalStartsDay)
            && market is IRivalFoodMarket rivals && rivals.NeedsRivalRefresh(minute);
        private IEnumerable<FoodScope> AwarenessScopes()
        {
            var rivals = RivalSettings.RivalChoiceWeight > 0 && city.State.Minute / 1440 >= state.RivalStartsDay ? market as IRivalFoodMarket : null;
            foreach (var scope in state.Shops) yield return rivals != null && rivals.RivalAtPlace(scope.PlaceId) ? null! : scope;
            if (rivals != null) foreach (var scope in rivals.RivalScopes) yield return scope;
        }
        private IEnumerable<FoodAwarenessCatalogue.Entry> AwarenessEntries()
        {
            int visited = 0;
            foreach (var scope in AwarenessScopes())
            {
                // Raw slots include up to 1,024 retired player-scope duplicates, yielded as
                // empty slots. The builder still enforces 20,000 actual alternatives/places.
                if (++visited > FoodAwarenessCatalogue.MaximumAlternatives + 1024) throw new InvalidOperationException("Oversized combined metadata source.");
                var point = scope == null ? (Position?)null : city.FindPosition(scope.PlaceId);
                yield return point.HasValue && point.Value.IsFinite ? new FoodAwarenessCatalogue.Entry(scope!,point.Value) : null!;
            }
        }
        private bool RivalCandidate(FoodScope scope) => market is IRivalFoodMarket rival && rival.IsRivalCandidate(scope);
        private FoodAlternativeObservation? ObserveRival(string id)
        {
            try { return (market as IRivalFoodMarket)?.ReadRival(id); }
            catch (Exception) { return null; } // Unavailable read evidence cannot authorize an outside payment.
        }
        private FoodBusiness? CaptureRival(string id, double minute)
        {
            var observation = ObserveRival(id);
            if (!RivalFoodTerms.Eligible(observation)) return null;
            var business = RivalFoodTerms.Capture(observation!, RivalSettings.RivalAssumedServiceMinutes);
            business.NextServiceMinute = minute;
            return business;
        }
        // Only this committed slot-zero outing defers admission to the live rival check.
        // Ordinary routines and other residents retain the place's original saved hours.
        internal bool OwnsRivalMealVisit(CitizenState person, string place) => person.MealOuting != null
            && !person.MealOuting.Returning && records.TryGetValue(person.Identity.Id, out var meal)
            && meal.TargetKind == FoodAlternativeKind.Rival && meal.PlaceId == place;
        private bool RivalCommitAllowed(ResidentMeal meal, FoodDecisionShop shop, Money price, double minute)
            => ExternalFoodSettlement.CanPay(meal, price) && RivalMatches(shop.BusinessId, shop.PlaceId, shop.RivalTerms, shop.Offers, null, minute, out _, requireOpen: false);
        private bool RivalMatches(string id, string place, string terms, IEnumerable<FoodDecisionOffer>? offers, ResidentMeal? meal, double minute,
            out Place? hours, bool requireOpen = true)
        {
            hours = null;
            var observation = ObserveRival(id);
            if (!RivalFoodTerms.Eligible(observation) || observation!.Id != id || observation.PlaceId != place
                || RivalFoodTerms.Fingerprint(observation) != terms) return false;
            if (meal != null && !observation.Offers.Any(o => o.ProductId == meal.ProductId && o.Price == meal.Price)) return false;
            var snapshot = observation;
            if (offers != null && offers.Any(p => !snapshot.Offers.Any(o => o.ProductId == p.ProductId && o.Price == p.Price))) return false;
            if (!requireOpen) return true;
            hours = new Place { WeeklyHours = observation.Hours.Select(h => new OpeningWindow { Day = h.Day, FromMinute = h.FromMinute, ToMinute = h.ToMinute }).ToList(), OpenMinute = 0, CloseMinute = 0 };
            return DailyRoutine.Open(hours, minute);
        }
        private void ArriveRival(ResidentMeal meal, CitizenState person, double now)
        {
            if (!RivalMatches(meal.BusinessId, meal.PlaceId, meal.External.Substring(2), null, meal, now, out var hours))
            { RivalRefusals++; Finish(meal, MealStatus.Missed, "Business no longer accepts this commitment"); return; }
            double finish = now + meal.ServiceMinutes;
            if (finish + MealRhythm.EatingMinutes(meal.Kind) + meal.ReturnMinutes > meal.Deadline
                || finish >= (DailyRoutine.NextClose(hours!, now) ?? double.NegativeInfinity))
            { Finish(meal, MealStatus.Missed, "Service no longer fits shop opening hours"); return; }
            if (meal.Slot == 0)
            {
                person.RecentVisits.Add(new VisitRecord { AtMinute = now, PlaceId = meal.PlaceId, Activity = Activity.Shop });
                if (person.RecentVisits.Count > 32) person.RecentVisits.RemoveAt(0);
                city.ExtendMealStay(person, finish + MealRhythm.EatingMinutes(meal.Kind));
            }
            meal.ServiceAt = finish; meal.Status = MealStatus.Serving; meal.DueAt = finish;
        }
        private void CompleteRival(ResidentMeal meal, double now)
        {
            if (!RivalMatches(meal.BusinessId, meal.PlaceId, meal.External.Substring(2), null, meal, now, out _))
            { RivalRefusals++; Finish(meal, MealStatus.Missed, "Business no longer accepts this commitment"); return; }
            if (!ExternalFoodSettlement.TryPay(meal, state.Outflow, now))
            { Finish(meal, MealStatus.Missed, "Household funds no longer available at checkout"); return; }
            RivalSettlements++; meal.ServiceAt = now;
            Finish(meal, MealStatus.Paid, "Modeled rival food purchase; availability estimated");
            MealOutcomes.Record(meal, now, checkout: true);
        }
        internal object RivalDiagnostics() => new { RivalChoices, RivalSettlements, RivalRefusals,
            FreeOutflow = city.FreeRivalOutflow(),
            Availability = "EstimatedFromPriceAndHours", NativeRivalCheckout = "Unsupported",
            ExternalFoodSpentUnits = Money.TotalUnits(state.Residents.Select(m => m.Budget?.ExternalFoodSpent ?? m.LegacyExternalFoodSpent)).ToString(CultureInfo.InvariantCulture) };
    }
    public sealed partial class CitySimulator
    {
        public object? DumpRivalFood() => food?.RivalDiagnostics();
    }
}
