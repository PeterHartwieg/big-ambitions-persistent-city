using System;

namespace PersistentCity.Core
{
    public enum ResidentIncomeSource { ExternalEmployment, ExternalPension, ExternalSupport }

    /// <summary>Individual budget unit, not an inferred family. All values are cents.
    /// Income and essentials cross an explicit outside-economy boundary. Native business
    /// payroll is not credited a second time and native retail remains the sale authority.</summary>
    public sealed class ResidentBudget
    {
        public ResidentIncomeSource IncomeSource { get; set; }
        public int BaseIncomeCents { get; set; }
        public int DailyIncomeCents { get; set; }
        public int DailyHousingCents { get; set; }
        public int DailyEssentialsCents { get; set; }
        public int DailyHomeFoodCents { get; set; }
        public decimal OpeningBalanceCents { get; set; }
        public decimal BalanceCents { get; set; }
        public decimal IncomeReceivedCents { get; set; }
        public decimal EssentialsPaidCents { get; set; }
        public decimal HomeFoodPaidCents { get; set; }
        public decimal PreparedFoodSpentCents { get; set; }
        public decimal HeldPurchaseCents { get; set; }
        public decimal ArrearsCents { get; set; }
        public int LastPostedDay { get; set; } = -1;
        public int HomeFoodFundedDay { get; set; } = -1;
        public int DaysWithoutHomeFood { get; set; }
        public double NextPostingMinute { get; set; }
    }

    public static class HouseholdFinance
    {
        public const int PolicyVersion = 1;
        public const string Evidence = "Provisional individual budget policy: external income/support, housing and essentials; paid household food basket. No native job or family pooling inferred.";
        private static int Draw(string id, string key, int seed, int low, int high)
            => low + (int)(StableRandom.Hash(id + ":household:" + key, seed) % (uint)(high - low + 1));

        public static ResidentBudget Create(CitizenState person, int seed, double minute, FoodDistrictBudgetMix? mix, string? consumerId = null)
        {
            string id = consumerId ?? person.Identity.Id;
            int band = Draw(id, "income-band", seed, 0, 99);
            int working = mix?.WorkingPercent ?? 40, middle = mix?.MiddlePercent ?? 45;
            int group = band < working ? 0 : band < working + middle ? 1 : 2;
            int income = group == 0 ? Draw(id, "income", seed, 9500, 18000)
                : group == 1 ? Draw(id, "income", seed, 15000, 30000) : Draw(id, "income", seed, 24000, 50000);
            var source = IncomeSource(person);
            int baseIncome = income;
            income = IncomeAmount(baseIncome, source);
            int housing = group == 0 ? Draw(id, "housing", seed, 3000, 6000)
                : group == 1 ? Draw(id, "housing", seed, 4500, 9000) : Draw(id, "housing", seed, 7000, 15000);
            var budget = new ResidentBudget { IncomeSource = source, BaseIncomeCents = baseIncome, DailyIncomeCents = income,
                DailyHousingCents = housing, DailyEssentialsCents = Draw(id, "essentials", seed, 1500, 2500),
                DailyHomeFoodCents = Draw(id, "home-food", seed, 900, 1500),
                OpeningBalanceCents = income * 7L, BalanceCents = income * 7L, NextPostingMinute = minute };
            // Opening savings are an explicit endowment, not a first earned wage.
            return budget;
        }

        public static void PostDay(ResidentBudget budget, double minute, CitizenState? person = null)
        {
            int day = checked((int)(minute / 1440));
            if (minute != budget.NextPostingMinute || day <= budget.LastPostedDay)
                throw new InvalidOperationException("Household posting is duplicate or out of order.");
            // Refresh only the next unposted wage. Earlier postings, savings and holds
            // retain their exact amounts through job transitions and reloads.
            if (person != null)
            {
                budget.IncomeSource = IncomeSource(person);
                budget.DailyIncomeCents = IncomeAmount(budget.BaseIncomeCents, budget.IncomeSource);
            }
            budget.BalanceCents = checked(budget.BalanceCents + budget.DailyIncomeCents);
            budget.IncomeReceivedCents = checked(budget.IncomeReceivedCents + budget.DailyIncomeCents);
            // Fund a real-cost subsistence basket before discretionary spending. A failed
            // payment does not provision food and never turns a declined shop visit into a meal.
            if (budget.BalanceCents >= budget.DailyHomeFoodCents)
            {
                budget.BalanceCents -= budget.DailyHomeFoodCents;
                budget.HomeFoodPaidCents = checked(budget.HomeFoodPaidCents + budget.DailyHomeFoodCents);
                budget.HomeFoodFundedDay = day;
            }
            else budget.DaysWithoutHomeFood = checked(budget.DaysWithoutHomeFood + 1);
            decimal required = checked(budget.ArrearsCents + budget.DailyHousingCents + budget.DailyEssentialsCents);
            decimal paid = Math.Min(budget.BalanceCents, required);
            budget.BalanceCents -= paid;
            budget.EssentialsPaidCents = checked(budget.EssentialsPaidCents + paid);
            budget.ArrearsCents = required - paid;
            budget.LastPostedDay = day;
            budget.NextPostingMinute = (day + 1) * 1440.0;
        }

        private static ResidentIncomeSource IncomeSource(CitizenState person)
            => person.Identity.Persona == Persona.Retiree ? ResidentIncomeSource.ExternalPension
                : person.Identity.Age >= 18 && ResidentLifecycle.IsWorker(person) && person.DayPlaceId.Length != 0
                    ? ResidentIncomeSource.ExternalEmployment : ResidentIncomeSource.ExternalSupport;

        private static int IncomeAmount(int basis, ResidentIncomeSource source)
            => source == ResidentIncomeSource.ExternalPension ? basis * CityBalance.PensionIncomePercent / 100
                : source == ResidentIncomeSource.ExternalSupport ? basis * CityBalance.SupportIncomePercent / 100 : basis;

        public static decimal SpendingLimit(ResidentBudget budget)
        {
            if (budget.HeldPurchaseCents != 0 || budget.ArrearsCents != 0) return 0;
            // Keep the next day's basic bill available. Savings can fund a considered purchase,
            // but replenishing a daily allowance never creates spendable money.
            long reserve = budget.DailyHousingCents + budget.DailyEssentialsCents + budget.DailyHomeFoodCents;
            return Math.Min(1000000, Math.Max(0, budget.BalanceCents - reserve));
        }

        public static int HomeAlternativeValue(ResidentBudget budget, CitizenState person, FoodConsumer consumer, FoodIntent intent, int seed, double convenienceWeight = 1)
        {
            int surplus = Math.Max(0, budget.DailyIncomeCents - budget.DailyHousingCents
                - budget.DailyEssentialsCents - budget.DailyHomeFoodCents);
            double taste = person.Identity.Persona == Persona.Foodie ? 1.4 : person.Identity.Persona == Persona.Student ? .8 : 1;
            double convenience = Math.Min(2500, surplus * (.15 + Draw(person.Identity.Id, "convenience", seed, 0, 15) / 100.0)) * taste * convenienceWeight;
            return (int)Math.Min(1000000, Math.Round(budget.DailyHomeFoodCents / 3.0 + convenience
                + (20 + intent.EatingMinutes) * consumer.ValueOfTimeCentsPerMinute * intent.TimeCostMultiplier));
        }

        public static bool HoldPurchase(ResidentBudget budget, decimal cents)
        {
            if (cents <= 0 || cents > SpendingLimit(budget)) return false;
            decimal remaining = FoodMoney.Add(budget.BalanceCents, -cents);
            FoodMoney.Add(budget.PreparedFoodSpentCents, cents); // Refuse unrepresentable confirmation before native checkout.
            budget.BalanceCents = remaining; budget.HeldPurchaseCents = cents; return true;
        }
        public static void ConfirmPurchase(ResidentBudget budget)
        { budget.PreparedFoodSpentCents = FoodMoney.Add(budget.PreparedFoodSpentCents, budget.HeldPurchaseCents); budget.HeldPurchaseCents = 0; }
        public static void RefusePurchase(ResidentBudget budget)
        { budget.BalanceCents = FoodMoney.Add(budget.BalanceCents, budget.HeldPurchaseCents); budget.HeldPurchaseCents = 0; }
    }
}
