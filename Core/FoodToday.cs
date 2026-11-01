using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    /// <summary>Fixed bounds of the version-1 food choice contract. Size bounds are not tunables:
    /// they cap route work and saved state. Before the saved activation day the legacy choice runs unchanged.</summary>
    public static partial class FoodChoicePolicy
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

    /// <summary>One meal's outcome on its own day, keyed by meal kind. On the wire it is one compact string,
    /// "Kind|Status|Reason|Outside|Factor1|Factor2|At|CheckoutAt|ProductId|PlaceId", with canonical non-negative
    /// integers: field names would otherwise make up about half of each record, and there are up to three per
    /// consumer. PlaceId is last, so it may itself contain '|'; ProductId may not.</summary>
    public sealed class MealOutcomeRecord
    {
        public MealKind Kind { get; set; }
        public MealOutcomeStatus Status { get; set; }
        public MealReasonCode Reason { get; set; }
        /// <summary>Opaque historical shop identity, kept only for a purchase (bought or unconfirmed);
        /// may later name a place no longer available.</summary>
        public string PlaceId { get; set; } = "";
        public string ProductId { get; set; } = "";
        public FoodOutsideOption Outside { get; set; }
        public MealFactor Factor1 { get; set; }
        public MealFactor Factor2 { get; set; }
        /// <summary>Whole simulation minute the outcome was recorded (rounded down).</summary>
        public int At { get; set; }
        /// <summary>Whole minute of the native checkout attempt (rounded down); zero when none was attempted
        /// or, for an upgraded record, when the saved meal held no checkout minute on its day.</summary>
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

        /// <summary>Strict inverse of ToWire: exactly ten fields, canonical non-negative integers (no sign, no
        /// leading zero, within int). Enum ranges and every other rule are checked by save validation.</summary>
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

        /// <summary>Schema-18 streaming precheck. Does not create child records before the enclosing
        /// rollup's complete arity has passed. The frozen schema-17 reader keeps its original TryParse.</summary>
        public static bool IsCanonicalWire(string? wire)
        {
            if (wire == null || wire.Length > 600) return false;
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
            return productEnd >= start && productEnd - start <= 256 && wire.Length - productEnd - 1 <= 256;
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

    /// <summary>Progress of one walking-quote slot of a shortlisted shop. Answered carries its route; Refused means
    /// the planner returned no usable path; NotNeeded means the slot was settled without a request (the shop was
    /// pruned by cheap factual checks, or its outward quote was not answered so no return was asked).</summary>
    public enum FoodQuoteState { Unrequested, Answered, Refused, NotNeeded }

    /// <summary>One captured offer of a shortlisted shop. Units -1 means unknown stock;
    /// DistrictDemandPercent -1 means unknown district demand (no appeal adjustment), otherwise 0-100.</summary>
    public sealed class FoodDecisionOffer
    {
        public string ProductId { get; set; } = "";
        public Money Price { get; set; }
        public int Units { get; set; } = -1;
        public int DistrictDemandPercent { get; set; } = -1;
    }

    /// <summary>Captured native input for one shortlisted shop, so a reload resumes without rereading mutable
    /// native state: the same evidence FoodBusiness carries into feasibility and choice. MealsPerHour -1 means unknown
    /// service; ServiceUnavailable records a native refusal reason without its free text; Hours are the captured
    /// weekly opening windows (empty: closed all week); CustomerServicePercent -1 means unknown quality, otherwise
    /// 0-100; DistrictId is the shop's district for district demand. Each quote slot records its progress; a route
    /// is present exactly when its slot is Answered.</summary>
    public sealed class FoodDecisionShop
    {
        /// <summary>Reserved B1 effective per-shop log-weight contribution, bounded -8..8.
        /// Empty/neutral is zero. B0b never populates or applies it.</summary>
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

    /// <summary>
    /// The single in-progress decision of one meal under the version-1 policy (wire contract; written from the
    /// weighted choice onward). Cleared at commitment or a terminal outcome, whose compact result moves to Today.
    /// Only a route-bearing decision, at most one in the city, carries the shortlist image.
    /// </summary>
    public sealed class FoodDecisionState
    {
        /// <summary>Null only for a migrated/pre-image context; capture live once on first continuation.</summary>
        public FoodDecisionTuning? Tuning { get; set; }
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
        /// <summary>Quote slots settled so far, in shortlist order (outward then return for each shop): slots
        /// below this are not Unrequested, slots from it on are. A reload resumes at this slot without repeating a
        /// request or skipping one. At most two per shop (sixteen).</summary>
        public int QuotesDone { get; set; }
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
            Fill(record, meal, status.Value, Minute(minute), checkout, checkout ? Minute(meal.ServiceAt) : 0, outside);
        }

        /// <summary>A recorded minute: the whole minute, rounded down. The inspector shows no finer time.</summary>
        public static int Minute(double minute) => (int)Math.Floor(minute);

        /// <summary>The checkout minute an outcome of this meal must carry, from the retained meal: its checkout
        /// minute (ServiceAt is set to it before native code runs), or zero when that is not on the meal's day or
        /// after the clock (an upgraded record of a meal that held none).</summary>
        public static int CheckoutMinute(ResidentMeal meal, double cityMinute)
            => meal.ServiceAt >= meal.Day * 1440.0 && meal.ServiceAt <= cityMinute ? Minute(meal.ServiceAt) : 0;

        private static void Fill(MealOutcomeRecord record, ResidentMeal meal, MealOutcomeStatus status, int minute, bool checkout, int checkoutAt, FoodOutsideOption outside)
        {
            bool hadPurchase=record.Status==MealOutcomeStatus.Bought || record.Status==MealOutcomeStatus.Unconfirmed;
            record.Status = status;
            record.Reason = ReasonFor(meal.Status, meal.Reason, checkout);
            // Only a purchase names its shop and product; a skip or alternative is described by its reason.
            bool purchase = status == MealOutcomeStatus.Bought || status == MealOutcomeStatus.Unconfirmed;
            record.PlaceId = purchase ? meal.PlaceId : ""; record.ProductId = purchase ? meal.ProductId : "";
            record.Outside = status == MealOutcomeStatus.ChoseAlternative ? outside : FoodOutsideOption.None;
            // The unconfirmed checkout already holds the saved choice factors. Confirmation/refusal
            // changes transaction evidence, not why that purchase was selected.
            if(!purchase || !hadPurchase || record.Factor1==MealFactor.Household) record.Factor1 = record.Factor2 = MealFactor.None;
            record.At = minute;
            record.CheckoutAt = checkoutAt;
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
                int at = Minute(Math.Min(cityMinute, Math.Max(dayStart, meal.DesiredAt)));
                // Checkout sets ServiceAt to the checkout minute before native code runs.
                int checkoutAt = meal.Status == MealStatus.Paid || meal.Status == MealStatus.Uncertain ? CheckoutMinute(meal, cityMinute) : 0;
                // The saved record does not name which alternative was chosen.
                Fill(outcome, meal, status.Value, Math.Max(at, checkoutAt), checkoutAt != 0, checkoutAt, FoodOutsideOption.None);
                record.Meals.Add(outcome);
            }
            return record;
        }
    }
}
