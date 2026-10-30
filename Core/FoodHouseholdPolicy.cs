using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PersistentCity.Core
{
    public sealed class FoodDistrictBudgetMix
    {
        public string DistrictId { get; set; } = "";
        public int WorkingPercent { get; set; }
        public int MiddlePercent { get; set; }
        public int UpperPercent { get; set; }
        public string Evidence { get; set; } = "";
    }

    /// <summary>Provisional spending policy, not observed income or a household ledger.
    /// Districts change the distribution; stable individual variation remains within each band.
    /// Personality changes the attraction of preparing food, never a person's inferred wealth.</summary>
    public static class FoodHouseholdPolicy
    {
        public static void ValidateMixes(IReadOnlyList<FoodDistrictBudgetMix> mixes)
        {
            if (mixes == null || mixes.Count > 100 || mixes.Any(m => m == null
                || string.IsNullOrWhiteSpace(m.DistrictId) || m.DistrictId.Length > 1024
                || string.IsNullOrWhiteSpace(m.Evidence) || m.Evidence.Length > 700
                || m.WorkingPercent < 0 || m.MiddlePercent < 0 || m.UpperPercent < 0
                || m.WorkingPercent > 100 || m.MiddlePercent > 100 || m.UpperPercent > 100
                || m.WorkingPercent + m.MiddlePercent + m.UpperPercent != 100)
                || mixes.Select(m => m.DistrictId).Distinct(StringComparer.Ordinal).Count() != mixes.Count)
                throw new ArgumentException("Invalid home-district budget distribution.");
        }

        public static void SetAllowance(FoodConsumer person, FoodRoutineResident resident, int seed,
            FoodDistrictBudgetMix? mix, int? fixedAllowance)
        {
            person.HomeDistrictId = resident.HomeDistrictId;
            if (fixedAllowance.HasValue)
            {
                person.DailyAllowanceCents = fixedAllowance.Value;
                person.BudgetEvidence = "Explicit fixed allowance for a controlled comparison; not observed income";
                return;
            }
            int band = (int)(StableRandom.Hash(person.Id + ":food-economic-band", seed) % 100);
            int low = 1100, range = 2200;
            if (mix != null)
            {
                if (mix.DistrictId != resident.HomeDistrictId) throw new ArgumentException("Budget mix belongs to another home district.");
                if (band < mix.WorkingPercent) { low = 900; range = 1500; }
                else if (band < mix.WorkingPercent + mix.MiddlePercent) { low = 1400; range = 2200; }
                else { low = 2400; range = 3000; }
            }
            person.DailyAllowanceCents = low + (int)(StableRandom.Hash(person.Id + ":food-budget", seed) % (uint)(range + 1));
            person.BudgetEvidence = mix == null
                ? "Provisional individual allowance ($11–33); home-district economic mix unknown; not observed income"
                : "Provisional individual allowance from overlapping $9–24/$14–36/$24–54 bands, sampled using the home district's economic mix; not observed income. " + mix.Evidence;
        }

        public static void SetOutsideOption(FoodConsumer person, FoodRoutineResident resident, FoodIntent intent, int seed)
        {
            // No teleport home from work. A packed-meal alternative is an explicitly assumed
            // daily preparation decision, not an observation of ingredients or food consumption.
            if (intent.OriginPlaceId.Length == 0) return;
            string dayKey = person.Id + ":prepared-food:" + intent.Day.ToString(CultureInfo.InvariantCulture);
            bool home = intent.OriginActivity == Activity.Home && intent.OriginPlaceId == resident.HomeId;
            bool work = intent.OriginActivity == Activity.Work || intent.OriginActivity == Activity.Study;
            Persona character = resident.Identity.Persona;
            int packingChance = character == Persona.Foodie ? 20 : character == Persona.Parent || character == Persona.Student ? 65
                : character == Persona.Commuter ? 35 : 45;
            if (!home && (!work || StableRandom.Hash(dayKey + ":pack", seed) % 100 >= packingChance)) return;
            intent.OutsideOption = home ? FoodOutsideOption.HomePreparation : FoodOutsideOption.PackedMeal;
            double characterValue = character == Persona.Foodie ? 1.25 : character == Persona.Student ? .8
                : character == Persona.Parent ? .9 : character == Persona.Commuter ? 1.05 : 1;
            double fraction = (home ? .50 : .45) + (StableRandom.Hash(dayKey + ":value", seed) % 301) / 1000.0;
            int preparationMinutes = home ? 15 + (int)(StableRandom.Hash(person.Id + ":food-preparation", seed) % 21) : 0;
            intent.OutsideOptionScoreCents = (int)Math.Min(1000000, Math.Round((double)person.DailyAllowanceCents * fraction * characterValue
                + (preparationMinutes + intent.EatingMinutes) * person.ValueOfTimeCentsPerMinute * intent.TimeCostMultiplier));
            intent.OutsideOptionEvidence = home
                ? "Provisional willingness-to-buy comparison with preparing food at the observed home; household ingredients/cost/consumption unverified"
                : "Provisional daily packed-meal plan at observed work/study; household ingredients/cost/consumption unverified";
        }
    }
}
