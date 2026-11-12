using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public static partial class FoodChoicePolicy
    {
        public const int Version = 1;
        public const int MaximumShortlist = 8;
        public const int MaximumOffersPerShop = FoodMenuPolicy.MaximumOffers;
        public const int MaximumContextsPerMeal = 4;
        public const int MaximumRouteBearingDecisions = 1;
    }

    public enum MealOutcomeStatus { Bought, ChoseAlternative, Skipped, Unconfirmed }

    public enum MealReasonCode
    {
        Other, Bought, ReceiptConfirmed, PrefersAlternative, CheckoutUnconfirmed, CheckoutRefused,
        NoSuitableStop, NoFeasibleOffer, InsufficientEvidence, ServiceUnavailable, ShopClosed,
        QueueTooLong, NoLongerFits, Interrupted, LeftBeforeService, ScheduleChanged,
        BusinessWithdrew, NativeClockPassed, MoneyRange, FundsUnavailable
    }

    public enum MealFactor { None, Taste, Price, Distance, Queue, Quality, TimePressure, Health, Household }

    public sealed class MealOutcomeRecord
    {
        public FoodAlternativeKind TargetKind { get; set; }
        public MealKind Kind { get; set; }
        public MealOutcomeStatus Status { get; set; }
        public MealReasonCode Reason { get; set; }
        public string PlaceId { get; set; } = "";
        public string ProductId { get; set; } = "";
        public FoodOutsideOption Outside { get; set; }
        public MealFactor Factor1 { get; set; }
        public MealFactor Factor2 { get; set; }
        public int At { get; set; }
        public int CheckoutAt { get; set; }

        private const int WireFields = 10;

        public string ToWire()
        {
            var text = new System.Text.StringBuilder(32 + ProductId.Length + PlaceId.Length);
            text.Append((int)Kind).Append('|').Append((int)Status).Append('|').Append((int)Reason).Append('|')
                .Append((int)Outside).Append('|').Append((int)Factor1).Append('|').Append((int)Factor2).Append('|')
                .Append(At.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                .Append(CheckoutAt.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('|')
                .Append(ProductId).Append('|').Append(PlaceId);
            return text.ToString();
        }

        public static bool TryParse(string? wire, out MealOutcomeRecord record)
        {
            record = new MealOutcomeRecord();
            if (wire == null) return false;
            var parts = wire.Split(new[] { '|' }, WireFields);
            if (parts.Length != WireFields) return false;
            var numbers = new int[8];
            for (int i = 0; i < 8; i++) if (!Canonical(parts[i], out numbers[i])) return false;
            record.Kind = (MealKind)numbers[0]; record.Status = (MealOutcomeStatus)numbers[1]; record.Reason = (MealReasonCode)numbers[2];
            record.Outside = (FoodOutsideOption)numbers[3]; record.Factor1 = (MealFactor)numbers[4]; record.Factor2 = (MealFactor)numbers[5];
            record.At = numbers[6]; record.CheckoutAt = numbers[7];
            record.ProductId = parts[8]; record.PlaceId = parts[9];
            return true;
        }

        public static bool IsCanonicalWire(string? wire)
        {
            int placeStart = wire == null ? -1 : wire.LastIndexOf('|') + 1;
            int placeLimit = placeStart > 0 && VendorNames.IsVendorPlace(wire!.Substring(placeStart)) ? VendorNames.MaximumPlaceIdLength : 256;
            if (wire == null || wire.Length > 600 - 256 + placeLimit) return false;
            int start = 0;
            for (int field = 0; field < 8; field++)
            {
                int end = wire.IndexOf('|', start), length = end - start;
                if (length < 1 || length > 10 || length > 1 && wire[start] == '0') return false;
                long value = 0;
                for (int i = start; i < end; i++)
                {
                    char c = wire[i]; if (c < '0' || c > '9') return false;
                    value = value * 10 + c - '0';
                }
                if (value > int.MaxValue) return false;
                start = end + 1;
            }
            int productEnd = wire.IndexOf('|', start);
            return productEnd >= start && productEnd - start <= 256 && wire.Length - productEnd - 1 <= placeLimit;
        }

        private static bool Canonical(string text, out int value)
        {
            value = 0;
            if (text.Length == 0 || text.Length > 10 || text.Length > 1 && text[0] == '0') return false;
            long total = 0;
            foreach (char c in text)
            {
                if (c < '0' || c > '9') return false;
                total = total * 10 + (c - '0');
            }
            if (total > int.MaxValue) return false;
            value = (int)total;
            return true;
        }
    }

    public sealed class ResidentFoodToday
    {
        public int Day { get; set; }
        public int Unrecorded { get; set; }
        public List<MealOutcomeRecord> Meals { get; set; } = new List<MealOutcomeRecord>();

        public MealOutcomeRecord? Find(MealKind kind)
        {
            foreach (var record in Meals) if (record.Kind == kind) return record;
            return null;
        }
    }

    public enum FoodDecisionResult { Evaluating, Selected, Outside, NoFeasible }

    public enum FoodQuoteState { Unrequested, Answered, Refused, NotNeeded }

    public sealed class FoodDecisionOffer
    {
        public FoodOfferMetadata? Metadata { get; set; }
        public string ProductId { get; set; } = "";
        public Money Price { get; set; }
        public int Units { get; set; } = -1;
        public int DistrictDemandPercent { get; set; } = -1;
    }

    public sealed class FoodDecisionShop
    {
        public FoodAlternativeKind AlternativeKind { get; set; }
        public FoodAvailabilityEvidenceKind AvailabilityEvidence => AlternativeKind == FoodAlternativeKind.Rival ? FoodAvailabilityEvidenceKind.EstimatedFromPriceAndHours : FoodAvailabilityEvidenceKind.ObservedPlayerSupply;
        public string RivalTerms { get; set; } = "";
        public int RivalServiceMinutes { get; set; }
        public double AlternativeWeight { get; set; } = 1;
        public double ServiceMinutes => AlternativeKind == FoodAlternativeKind.Rival ? RivalServiceMinutes : 60 / MealsPerHour;
        public double MemoryContribution { get; set; }
        public string BusinessId { get; set; } = "";
        public string PlaceId { get; set; } = "";
        public double MealsPerHour { get; set; } = -1;
        public bool ServiceUnavailable { get; set; }
        public double NextServiceAt { get; set; }
        public List<OpeningWindow> Hours { get; set; } = new List<OpeningWindow>();
        public int CustomerServicePercent { get; set; } = -1;
        public string DistrictId { get; set; } = "";
        public List<FoodDecisionOffer> Offers { get; set; } = new List<FoodDecisionOffer>();
        public FoodQuoteState OutwardQuote { get; set; }
        public FoodQuoteState ReturnQuote { get; set; }
        public Route? Outward { get; set; }
        public Route? Return { get; set; }
    }

    public sealed class FoodDecisionState
    {
        public WeatherSample Weather { get; set; }
        public double WeatherTravelMultiplier { get; set; }=1;
        public FoodDecisionTuning? Tuning { get; set; }
        public FoodAwarenessImage? Awareness { get; set; }
        public string IntentId { get; set; } = "";
        public int PolicyVersion { get; set; } = FoodChoicePolicy.Version;
        public string ContextKey { get; set; } = "";
        public int Attempt { get; set; }
        public FoodDecisionResult Result { get; set; }
        public string BusinessId { get; set; } = "";
        public string ProductId { get; set; } = "";
        public FoodOutsideOption Outside { get; set; }
        public MealReasonCode Reason { get; set; }
        public MealFactor Factor1 { get; set; }
        public MealFactor Factor2 { get; set; }
        public double OriginAt { get; set; }
        public double Deadline { get; set; }
        public int QuotesDone { get; set; }
        public List<FoodDecisionShop> Shops { get; set; } = new List<FoodDecisionShop>();
        public bool RouteBearing => Shops.Count > 0;
    }

    public static class MealOutcomes
    {
        private static readonly Dictionary<string, MealReasonCode> Reasons = new Dictionary<string, MealReasonCode>(StringComparer.Ordinal)
        {
            ["No suitable stop in the meal window"] = MealReasonCode.NoSuitableStop,
            ["No feasible purchased meal"] = MealReasonCode.NoFeasibleOffer,
            ["Insufficient stock, service or route evidence"] = MealReasonCode.InsufficientEvidence,
            ["Service unavailable on arrival"] = MealReasonCode.ServiceUnavailable,
            ["Shop closed before admission"] = MealReasonCode.ShopClosed,
            ["Service no longer fits shop opening hours"] = MealReasonCode.ShopClosed,
            ["Queue exceeds patience on arrival"] = MealReasonCode.QueueTooLong,
            ["Arrival or queue no longer fits the break"] = MealReasonCode.NoLongerFits,
            ["Visit was interrupted before admission"] = MealReasonCode.Interrupted,
            ["Resident left before service"] = MealReasonCode.LeftBeforeService,
            ["Schedule or shop changed before departure"] = MealReasonCode.ScheduleChanged,
            ["Business no longer accepts this commitment"] = MealReasonCode.BusinessWithdrew,
            ["Native clock passed the purchase window"] = MealReasonCode.NativeClockPassed,
            ["Purchase would exceed the exact money range"] = MealReasonCode.MoneyRange,
            ["Household funds no longer available at checkout"] = MealReasonCode.FundsUnavailable,
            ["Native purchase did not confirm completion"] = MealReasonCode.CheckoutUnconfirmed,
        };
        private const string ExistingStopPrefix = "Existing stop: ";

        public static int Index(MealKind kind) => kind == MealKind.Legacy ? 0 : kind - MealKind.Breakfast;

        public static MealOutcomeStatus? StatusFor(MealStatus status) => status == MealStatus.Paid ? MealOutcomeStatus.Bought
            : status == MealStatus.Declined ? MealOutcomeStatus.ChoseAlternative : status == MealStatus.Missed ? MealOutcomeStatus.Skipped
            : status == MealStatus.Uncertain ? MealOutcomeStatus.Unconfirmed : (MealOutcomeStatus?)null;

        public static MealReasonCode ReasonFor(MealStatus status, string reason, bool checkout)
        {
            switch (status)
            {
                case MealStatus.Paid: return reason != null && reason.StartsWith("Original native receipt confirmed", StringComparison.Ordinal)
                    ? MealReasonCode.ReceiptConfirmed : MealReasonCode.Bought;
                case MealStatus.Declined: return MealReasonCode.PrefersAlternative;
                case MealStatus.Uncertain: return MealReasonCode.CheckoutUnconfirmed;
            }
            if (checkout) return MealReasonCode.CheckoutRefused;
            string text = reason ?? "";
            if (text.StartsWith(ExistingStopPrefix, StringComparison.Ordinal)) text = text.Substring(ExistingStopPrefix.Length);
            return Reasons.TryGetValue(text, out var code) ? code : MealReasonCode.Other;
        }

        public static void Record(ResidentMeal meal, double minute, bool checkout, FoodOutsideOption outside = FoodOutsideOption.None)
        {
            var status = StatusFor(meal.Status);
            if (!status.HasValue) return;
            var today = meal.Today;
            if (today == null) meal.Today = today = new ResidentFoodToday { Day = meal.Day };
            else if (meal.Day > today.Day) { today.Day = meal.Day; today.Unrecorded = 0; today.Meals.Clear(); }
            else if (meal.Day < today.Day) return;
            if (Index(meal.Kind) < today.Unrecorded) return;
            var record = today.Find(meal.Kind);
            if (record == null)
            {
                record = new MealOutcomeRecord { Kind = meal.Kind };
                int at = 0;
                while (at < today.Meals.Count && today.Meals[at].Kind < meal.Kind) at++;
                today.Meals.Insert(at, record);
            }
            Fill(record, meal, status.Value, Minute(minute), checkout, checkout ? Minute(meal.ServiceAt) : 0, outside);
        }

        public static int Minute(double minute) => (int)Math.Floor(minute);

        public static int CheckoutMinute(ResidentMeal meal, double cityMinute)
            => meal.ServiceAt >= meal.Day * 1440.0 && meal.ServiceAt <= cityMinute ? Minute(meal.ServiceAt) : 0;

        private static void Fill(MealOutcomeRecord record, ResidentMeal meal, MealOutcomeStatus status, int minute, bool checkout, int checkoutAt, FoodOutsideOption outside)
        {
            bool hadPurchase=record.Status==MealOutcomeStatus.Bought || record.Status==MealOutcomeStatus.Unconfirmed;
            record.Status = status;
            record.TargetKind = meal.TargetKind;
            record.Reason = ReasonFor(meal.Status, meal.Reason, checkout);
            bool purchase = status == MealOutcomeStatus.Bought || status == MealOutcomeStatus.Unconfirmed;
            record.PlaceId = purchase ? meal.PlaceId : ""; record.ProductId = purchase ? meal.ProductId : "";
            record.Outside = status == MealOutcomeStatus.ChoseAlternative ? outside : FoodOutsideOption.None;
            if(!purchase || !hadPurchase || record.Factor1==MealFactor.Household) record.Factor1 = record.Factor2 = MealFactor.None;
            record.At = minute;
            record.CheckoutAt = checkoutAt;
        }

        public static ResidentFoodToday Upgrade(ResidentMeal meal, double cityMinute, bool legacyRhythm)
        {
            int today = (int)Math.Floor(cityMinute / 1440);
            if (meal.Day > today)
                return new ResidentFoodToday { Day = today, Unrecorded = legacyRhythm ? 1 : 3 };
            var record = new ResidentFoodToday { Day = meal.Day, Unrecorded = Index(meal.Kind) };
            var status = StatusFor(meal.Status);
            if (status.HasValue)
            {
                var outcome = new MealOutcomeRecord { Kind = meal.Kind };
                double dayStart = meal.Day * 1440.0;
                int at = Minute(Math.Min(cityMinute, Math.Max(dayStart, meal.DesiredAt)));
                int checkoutAt = meal.Status == MealStatus.Paid || meal.Status == MealStatus.Uncertain ? CheckoutMinute(meal, cityMinute) : 0;
                Fill(outcome, meal, status.Value, Math.Max(at, checkoutAt), checkoutAt != 0, checkoutAt, FoodOutsideOption.None);
                record.Meals.Add(outcome);
            }
            return record;
        }
    }
}
