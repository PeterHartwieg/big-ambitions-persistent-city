using System;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    // Isolated schema-20 -> 21 step. The frozen E1 reader first upgrades older
    // payloads through 20 and preserves captured weather without resampling.
    internal static class RivalWire21
    {
        internal static bool AddedProperty(Type type, string name)
            => type == typeof(ResidentBudget) && name == "ExternalFoodSpent"
            || type == typeof(ResidentMeal) && (name == "External" || name == "LegacyExternalFoodSpent")
            || type == typeof(FoodScope) && name == "AlternativeKind"
            || type == typeof(FoodAwarenessCandidate) && name == "AlternativeKind"
            || type == typeof(FoodCommerceState) && name == "RivalStartsDay"
            || type == typeof(FoodDecisionShop) && (name == "AlternativeKind" || name == "RivalTerms"
                || name == "RivalServiceMinutes" || name == "AlternativeWeight");

        internal static SaveReadResult Migrate(string source)
        {
            var old = PopulationSchema20.Read(source);
            if (old.Status != SaveReadStatus.Loaded) return old;
            var state = old.State!;
            state.Schema = 21;
            if (state.Food != null) state.Food.RivalStartsDay = (int)(state.Minute / 1440) + 1;
            SaveCodec.ValidateForActivation(state,21);
            return new SaveReadResult(SaveReadStatus.Loaded, state, "", old.SourceSchema);
        }
        private static void Require(bool condition, string reason)
        { if (!condition) throw new SaveValidationException(reason); }

        // Schema 21 reserves status codes 4..7 for the rival target variant. Both target
        // and ordinary status fit the existing single digit: zero added outcome bytes.
        internal static string Outcome(MealOutcomeRecord record)
        {
            string wire = record.ToWire();
            if (record.TargetKind == FoodAlternativeKind.Player) return wire;
            int first = wire.IndexOf('|'), second = wire.IndexOf('|', first + 1);
            return wire.Substring(0, first + 1) + ((int)record.Status + 4).ToString(System.Globalization.CultureInfo.InvariantCulture) + wire.Substring(second);
        }
        internal static bool CanonicalOutcome(string wire)
        {
            if (!MealOutcomeRecord.IsCanonicalWire(wire)) return false;
            int first = wire.IndexOf('|'), second = wire.IndexOf('|', first + 1);
            return second == first + 2 && wire[first + 1] >= '0' && wire[first + 1] <= '7';
        }
        internal static bool ParseOutcome(string wire, out MealOutcomeRecord record)
        {
            record = null!;
            if (!CanonicalOutcome(wire) || !MealOutcomeRecord.TryParse(wire, out record)) return false;
            record.TargetKind = (int)record.Status >= 4 ? FoodAlternativeKind.Rival : FoodAlternativeKind.Player;
            if (record.TargetKind == FoodAlternativeKind.Rival) record.Status = (MealOutcomeStatus)((int)record.Status - 4);
            return true;
        }
        internal static void ValidateOutcome(MealOutcomeRecord record)
        {
            Require(record.TargetKind == FoodAlternativeKind.Player || record.TargetKind == FoodAlternativeKind.Rival, "Unknown outcome target kind.");
            Require(record.TargetKind != FoodAlternativeKind.Rival || (record.Status == MealOutcomeStatus.Bought || record.Status == MealOutcomeStatus.Skipped)
                && record.Reason != MealReasonCode.ReceiptConfirmed, "Modeled rival payment cannot be native receipt evidence.");
        }
        internal static void ValidateMeal(ResidentMeal meal, FoodCommerceState food)
        {
            Require(food.RivalStartsDay >= 0, "Invalid rival activation day.");
            Require(meal.External != null && (meal.External.Length == 0 || meal.External.Length == 21
                && meal.External[0] == '1' && (meal.External[1] == '0' || meal.External[1] == '1')
                && RivalFoodTerms.Canonical(meal.External.Substring(2))), "Invalid rival target/settlement watermark.");
            Require(meal.LegacyExternalFoodSpent >= Money.Zero && (meal.Budget == null || meal.LegacyExternalFoodSpent == Money.Zero), "Invalid legacy external-food account.");
            var account = meal.Budget?.ExternalFoodSpent ?? meal.LegacyExternalFoodSpent;
            Require(account <= meal.Spent && (account == Money.Zero || meal.Purchases > 0), "External outflow exceeds recorded consumer spending.");
            if (meal.External!.Length == 0) return;
            Require(meal.Day >= food.RivalStartsDay && meal.BusinessId.Length > 0 && meal.PlaceId.Length > 0
                && meal.ContextAttempts > 0
                && meal.Price > Money.Zero && meal.ServiceMinutes >= 1 && meal.ServiceMinutes <= 30
                && meal.ServiceMinutes == Math.Truncate(meal.ServiceMinutes), "Invalid rival commitment.");
            Require(meal.Status == MealStatus.Travelling || meal.Status == MealStatus.Serving || meal.Status == MealStatus.Paid || meal.Status == MealStatus.Missed,
                "Rival target has impossible transaction state.");
            Require((meal.External[1] == '1') == (meal.Status == MealStatus.Paid), "Rival settlement watermark differs from payment.");
            Require(meal.Budget == null || meal.Budget.HeldPurchase == Money.Zero, "Modeled rival target cannot hold a native purchase.");
            if(meal.Status == MealStatus.Paid)
                Require(meal.Purchases > 0 && meal.Spent >= meal.Price && (meal.Budget?.ExternalFoodSpent ?? meal.LegacyExternalFoodSpent) >= meal.Price
                    && (meal.Budget != null || meal.DayAllowanceUsed >= meal.Price), "Settled rival target lost its outflow account.");
        }
        internal static void ValidateShop(FoodDecisionShop shop, FoodDecisionState decision, int schema)
        {
            Require(shop.AlternativeKind == FoodAlternativeKind.Player || shop.AlternativeKind == FoodAlternativeKind.Rival, "Unknown captured target kind.");
            Require(Position.Finite(shop.AlternativeWeight) && shop.AlternativeWeight >= 0 && shop.AlternativeWeight <= 2, "Invalid rival choice weight.");
            if (shop.AlternativeKind == FoodAlternativeKind.Player)
                Require(shop.RivalTerms == "" && shop.RivalServiceMinutes == 0 && shop.AlternativeWeight == 1, "Player candidate has rival terms.");
            else
            {
                Require(RivalFoodTerms.Canonical(shop.RivalTerms) && shop.RivalServiceMinutes >= 1 && shop.RivalServiceMinutes <= 30
                    && shop.MealsPerHour == -1 && shop.AlternativeWeight > 0
                    // D4 widens only the current contract; genuine 21/22/23 remain neutral.
                    && (schema >= 24 || shop.MemoryContribution == 0)
                    && !shop.ServiceUnavailable && shop.DistrictId == "" && shop.NextServiceAt == decision.OriginAt, "Invalid estimated rival candidate.");
                Require(shop.Offers != null, "Missing rival offers.");
                foreach (var offer in shop.Offers!) Require(offer != null && offer.Units == -1 && offer.DistrictDemandPercent == -1, "Rival stock/demand must remain unknown.");
            }
        }
        internal static void ValidateAwareness(FoodDecisionState decision)
        {
            if (decision.Awareness == null) return;
            Require(decision.Awareness.Union != null && decision.Shops != null, "Missing captured awareness/shortlist.");
            foreach (var candidate in decision.Awareness.Union!)
                Require(candidate != null && (candidate.AlternativeKind == FoodAlternativeKind.Player || candidate.AlternativeKind == FoodAlternativeKind.Rival), "Unknown awareness alternative kind.");
            foreach (var shop in decision.Shops!)
                Require(shop != null && decision.Awareness.Union!.Find(c => c.BusinessId == shop.BusinessId)?.AlternativeKind == shop.AlternativeKind,
                    "Shortlist changed captured target authority.");
        }
    }
}
