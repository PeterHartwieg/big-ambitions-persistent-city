using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>Fixed bounds of the version-1 food choice contract. Size bounds are not tunables:
    /// they cap route work and saved state. Version 1 behaviour arrives with the weighted choice; until
    /// then the legacy choice runs unchanged and only the header and wire contracts exist.</summary>
    public static class FoodChoicePolicy
    {
        public const int Version = 1;
        public const int MaximumShortlist = 8;
        public const int MaximumOffersPerShop = 4;
        public const int MaximumContextsPerMeal = 4;
        public const int MaximumRouteBearingDecisions = 1;
    }

    /// <summary>What a consumer's meal ended as. In-progress states are read from the live meal, not stored.
    /// ChoseAlternative records a choice, never that the person ate it.</summary>
    public enum MealOutcomeStatus { Bought, ChoseAlternative, Skipped, Unconfirmed }

    /// <summary>Fixed reason codes; native or free-form text is never stored in an outcome.</summary>
    public enum MealReasonCode
    {
        Other, Bought, ReceiptConfirmed, PrefersAlternative, CheckoutUnconfirmed, CheckoutRefused,
        NoSuitableStop, NoFeasibleOffer, InsufficientEvidence, ServiceUnavailable, ShopClosed,
        QueueTooLong, NoLongerFits, Interrupted, LeftBeforeService, ScheduleChanged,
        BusinessWithdrew, NativeClockPassed, MoneyRange, FundsUnavailable
    }

    /// <summary>Explanatory factor codes, at most two per outcome or decision.</summary>
    public enum MealFactor { None, Taste, Price, Distance, Queue, Quality, TimePressure, Health, Household }

    /// <summary>One meal's outcome on its own day, keyed by meal kind.</summary>
    public sealed class MealOutcomeRecord
    {
        public MealKind Kind { get; set; }
        public MealOutcomeStatus Status { get; set; }
        public MealReasonCode Reason { get; set; }
        /// <summary>Opaque historical shop identity; may later name a place no longer available.</summary>
        public string PlaceId { get; set; } = "";
        public string ProductId { get; set; } = "";
        public FoodOutsideOption Outside { get; set; }
        public MealFactor Factor1 { get; set; }
        public MealFactor Factor2 { get; set; }
        /// <summary>Simulation minute the outcome was recorded.</summary>
        public double At { get; set; }
        /// <summary>Minute of the native checkout attempt; zero when no checkout was attempted.</summary>
        public double CheckoutAt { get; set; }
    }

    /// <summary>One consumer's current-day outcomes: at most three named meals, or one legacy meal.
    /// Unrecorded counts the leading meals of Day that ended before the schema-17 upgrade.</summary>
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

    /// <summary>One captured offer of a shortlisted shop. Units -1 means unknown stock.</summary>
    public sealed class FoodDecisionOffer
    {
        public string ProductId { get; set; } = "";
        public Money Price { get; set; }
        public int Units { get; set; } = -1;
    }

    /// <summary>Captured native input for one shortlisted shop, so a reload resumes without redrawing.
    /// MealsPerHour -1 means unknown service. Routes are validated walking answers, or null when not yet quoted.</summary>
    public sealed class FoodDecisionShop
    {
        public string BusinessId { get; set; } = "";
        public string PlaceId { get; set; } = "";
        public double MealsPerHour { get; set; } = -1;
        public double NextServiceAt { get; set; }
        public List<FoodDecisionOffer> Offers { get; set; } = new List<FoodDecisionOffer>();
        public Route? Outward { get; set; }
        public Route? Return { get; set; }
    }

    /// <summary>
    /// The single in-progress decision of one meal under the version-1 policy (wire contract; written from the
    /// weighted choice onward). Cleared at commitment or a terminal outcome, whose compact result moves to Today.
    /// Only a route-bearing decision, at most one in the city, carries the shortlist image.
    /// </summary>
    public sealed class FoodDecisionState
    {
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
        public List<FoodDecisionShop> Shops { get; set; } = new List<FoodDecisionShop>();
        public bool RouteBearing => Shops.Count > 0;
    }

    /// <summary>The one shared current-day outcome updater. O(3) per call, no population scan; the day
    /// advances lazily on the consumer's own next outcome. Idempotent by intent: a repeated or later outcome of the
    /// same meal replaces its record, and an outcome of an earlier day never overwrites the current one.</summary>
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

        /// <summary>Record the meal's current terminal status. Non-terminal statuses are ignored.</summary>
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
            Fill(record, meal, status.Value, minute, checkout, outside);
        }

        private static void Fill(MealOutcomeRecord record, ResidentMeal meal, MealOutcomeStatus status, double minute, bool checkout, FoodOutsideOption outside)
        {
            record.Status = status;
            record.Reason = ReasonFor(meal.Status, meal.Reason, checkout);
            record.PlaceId = meal.PlaceId; record.ProductId = meal.ProductId;
            record.Outside = status == MealOutcomeStatus.ChoseAlternative ? outside : FoodOutsideOption.None;
            record.Factor1 = record.Factor2 = MealFactor.None;
            record.At = minute;
            record.CheckoutAt = checkout ? meal.ServiceAt : 0;
        }

        /// <summary>Schema 16 to 17: synthesize only what the single saved meal record shows. Meals of that day
        /// that ended before it are "not recorded before upgrade"; no earlier outcome is inferred.</summary>
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
                // The saved meal holds no outcome time: its appetite time, clamped to its day and the clock.
                double at = Math.Min(cityMinute, Math.Max(dayStart, meal.DesiredAt));
                // Checkout sets ServiceAt to the checkout minute before native code runs.
                bool checkout = (meal.Status == MealStatus.Paid || meal.Status == MealStatus.Uncertain)
                    && meal.ServiceAt >= dayStart && meal.ServiceAt <= cityMinute;
                // The saved record does not name which alternative was chosen.
                Fill(outcome, meal, status.Value, Math.Max(at, checkout ? meal.ServiceAt : at), checkout, FoodOutsideOption.None);
                record.Meals.Add(outcome);
            }
            return record;
        }
    }
}
