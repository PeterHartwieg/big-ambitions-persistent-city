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
        private bool StepBudget()
        {
            if (NextBudgetMinute > (due.Count == 0 ? double.PositiveInfinity : due.Min!.DueAt)) return false;
            if (budgetDue.Count == 0) return false;
            var meal = budgetDue.Min!; budgetDue.Remove(meal);
            HouseholdFinance.PostDay(meal.Budget!, city.State.Minute, city.FindCitizen(meal.AnchorId)!);
            budgetDue.Add(meal);
            return true;
        }
        private int AvailableForMeal(ResidentMeal meal)
        {
            if (meal.Budget != null) meal.AllowanceCents = HouseholdFinance.SpendingLimit(meal.Budget);
            return meal.Budget != null ? meal.AllowanceCents : Math.Max(0, meal.AllowanceCents - meal.DayAllowanceUsedCents);
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
