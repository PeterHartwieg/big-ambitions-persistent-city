using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentCity.Core
{
    public sealed partial class FoodCommerce
    {
        private sealed class BudgetOrder : IComparer<ResidentMeal>
        {
            public int Compare(ResidentMeal? a, ResidentMeal? b)
            {
                if (ReferenceEquals(a, b)) return 0;
                if (a == null) return -1; if (b == null) return 1;
                int time = a.Budget!.NextPostingMinute.CompareTo(b.Budget!.NextPostingMinute);
                return time == 0 ? StringComparer.Ordinal.Compare(a.ResidentId, b.ResidentId) : time;
            }
        }
        private readonly SortedSet<ResidentMeal> budgetDue = new SortedSet<ResidentMeal>(new BudgetOrder());
        private double NextBudgetMinute => budgetDue.Count == 0 ? double.PositiveInfinity : budgetDue.Min!.Budget!.NextPostingMinute;
        internal void EnableFinances()
        {
            if (state.Residents.Any(m => m.LegacyExternalFoodSpent != Money.Zero)) throw new InvalidOperationException("Preserve the legacy external-food ledger; use a household-finance scope before new outside purchases.");
            if (state.HouseholdFinanceVersion != 0) throw new InvalidOperationException("Household finances are already active.");
            if (state.Residents.Any(m => Active(m) || m.Status == MealStatus.Uncertain))
                throw new InvalidOperationException("Finish current purchases before enabling household finances.");
            state.HouseholdFinanceVersion = HouseholdFinance.PolicyVersion;
            foreach (var meal in state.Residents)
            { CreateBudget(meal, city.FindCitizen(meal.AnchorId)!); budgetDue.Add(meal); }
        }
        private void CreateBudget(ResidentMeal meal, CitizenState person)
        {
            string district = city.FindPlace(person.HomeId).District;
            meal.Budget = HouseholdFinance.Create(person, city.State.Seed, city.State.Minute,
                state.Districts.SingleOrDefault(d => d.DistrictId == district), meal.ResidentId);
        }
        /// <summary>Most postings one food event makes. Unbounded in play; a differential test sets 1 to
        /// reproduce the earlier one-posting-per-event stepping exactly.</summary>
        internal int BudgetPostingsPerEvent { get; set; } = int.MaxValue;
        // Every budget due at this minute posts in one event, in BudgetOrder. PostDay moves a posted budget
        // to the next midnight, so the loop cannot post one twice. Citizen and traffic events at this minute
        // ran before this food event, and meals due at this minute still follow every budget due at it.
        private bool StepBudget()
        {
            if (NextBudgetMinute > (due.Count == 0 ? double.PositiveInfinity : due.Min!.DueAt)) return false;
            if (budgetDue.Count == 0) return false;
            double minute = budgetDue.Min!.Budget!.NextPostingMinute;
            int posted = 0;
            do
            {
                var meal = budgetDue.Min!; budgetDue.Remove(meal);
                HouseholdFinance.PostDay(meal.Budget!, city.State.Minute, city.FindCitizen(meal.AnchorId)!);
                budgetDue.Add(meal);
                posted++;
            }
            while (posted < BudgetPostingsPerEvent && budgetDue.Min!.Budget!.NextPostingMinute == minute);
            return true;
        }
        private Money AvailableForMeal(ResidentMeal meal)
        {
            if (meal.Budget != null) meal.Allowance = HouseholdFinance.SpendingLimit(meal.Budget);
            return meal.Budget != null ? meal.Allowance : Money.Max(Money.Zero, meal.Allowance - meal.DayAllowanceUsed);
        }
        private void SetFoodAlternative(ResidentMeal meal, CitizenState person, FoodConsumer consumer, FoodIntent intent)
        {
            if (meal.Budget == null)
            { FoodHouseholdPolicy.SetOutsideOption(consumer, Resident(person), intent, city.State.Seed); return; }
            consumer.BudgetEvidence = HouseholdFinance.Evidence;
            if (meal.Budget.HomeFoodFundedDay != meal.Day) return;
            intent.OutsideOption = FoodOutsideOption.FundedHouseholdFood;
            intent.OutsideOptionScoreCents = HouseholdFinance.HomeAlternativeValue(meal.Budget, person, consumer, intent, city.State.Seed, MealRhythm.ConvenienceWeight(person, meal.Kind));
            intent.OutsideOptionEvidence = "Household food basket already paid for this day; defer prepared food to the household alternative. No extra trip or free food is booked.";
        }
    }
    public sealed partial class CitySimulator
    {
        public void EnableHouseholdFinances()
        {
            if (food == null) throw new InvalidOperationException("An enrolled food market is required.");
            food.EnableFinances();
        }
    }
}
