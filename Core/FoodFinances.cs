using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentCity.Core
{
    public sealed partial class FoodCommerce
    {
        // Only the minimum changes, and only by moving later. Reuse a heap slot instead of allocating
        // a new SortedSet node on every posting. This index is derived from saved budget timestamps.
        private sealed class BudgetQueue
        {
            private struct Entry
            {
                public ResidentMeal Meal;
                public double Minute;
                public int Ordinal;
            }
            private readonly List<Entry> items = new List<Entry>();
            public int Count => items.Count;
            public ResidentMeal Min => items[0].Meal;
            // Population enrollment/finance activation builds this derived index before advancement.
            // Cache ordinal ranks once: heap repair never hashes or compares resident-ID strings.
            public void Add(ResidentMeal meal) => items.Add(new Entry { Meal = meal });
            public void Build()
            {
                items.Sort((a, b) => StringComparer.Ordinal.Compare(a.Meal.ResidentId, b.Meal.ResidentId));
                for (int i = 0; i < items.Count; i++)
                {
                    var entry = items[i]; entry.Ordinal = i; entry.Minute = entry.Meal.Budget!.NextPostingMinute; items[i] = entry;
                }
                for (int i = items.Count / 2 - 1; i >= 0; i--) Repair(i);
            }
            private static int Compare(Entry a, Entry b)
            {
                int time = a.Minute.CompareTo(b.Minute);
                return time == 0 ? a.Ordinal.CompareTo(b.Ordinal) : time;
            }
            public void UpdatedMinimum()
            {
                var entry = items[0]; entry.Minute = entry.Meal.Budget!.NextPostingMinute; items[0] = entry;
                Repair(0);
            }
            private void Repair(int at)
            {
                var meal = items[at];
                while (at * 2 + 1 < items.Count)
                {
                    int child = at * 2 + 1;
                    if (child + 1 < items.Count && Compare(items[child + 1], items[child]) < 0) child++;
                    if (Compare(meal, items[child]) <= 0) break;
                    items[at] = items[child]; at = child;
                }
                items[at] = meal;
            }
        }
        private readonly BudgetQueue budgetDue = new BudgetQueue();
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
            budgetDue.Build();
        }
        private void CreateBudget(ResidentMeal meal, CitizenState person)
        {
            string district = city.FindPlace(person.HomeId).District;
            meal.Budget = HouseholdFinance.Create(person, city.State.Seed, city.State.Minute,
                state.Districts.SingleOrDefault(d => d.DistrictId == district), meal.ResidentId);
        }
        public const int MaximumBudgetPostingsPerUnit = 32;
        // Tests may lower the cap to compare interruption cadences, but cannot raise the runtime bound.
        private int budgetPostingsPerEvent = MaximumBudgetPostingsPerUnit;
        internal int BudgetPostingsPerEvent
        {
            get => budgetPostingsPerEvent;
            set => budgetPostingsPerEvent = value >= 1 && value <= MaximumBudgetPostingsPerUnit
                ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }
        public long BudgetPostings { get; private set; }
        public int LastBudgetPostings { get; private set; }
        // Keep the same minute/ordinal order across units. NextBudgetMinute continues to hold the
        // food clock before meals until every posting at that minute has completed. A save needs no
        // cursor: each posted budget already contains its next timestamp and exact conserved ledger.
        private bool StepBudget()
        {
            LastBudgetPostings = 0;
            if (NextBudgetMinute > (due.Count == 0 ? double.PositiveInfinity : due.Min!.DueAt)) return false;
            if (budgetDue.Count == 0) return false;
            double minute = budgetDue.Min!.Budget!.NextPostingMinute;
            int posted = 0;
            do
            {
                var meal = budgetDue.Min!;
                HouseholdFinance.PostDay(meal.Budget!, city.State.Minute, city.FindCitizen(meal.AnchorId)!);
                budgetDue.UpdatedMinimum();
                posted++;
                LastBudgetPostings = posted; BudgetPostings++;
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
