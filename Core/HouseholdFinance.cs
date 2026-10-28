using System;

namespace PersistentCity.Core
{
    public enum ResidentIncomeSource { ExternalEmployment, ExternalPension, ExternalSupport }

    /// <summary>Individual budget unit, not an inferred family. Policy rates are whole cents;
    /// ledger amounts are exact <see cref="Money"/>.
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
        public Money OpeningBalance { get; set; }
        public Money Balance { get; set; }
        public Money IncomeReceived { get; set; }
        public Money EssentialsPaid { get; set; }
        public Money HomeFoodPaid { get; set; }
        public Money PreparedFoodSpent { get; set; }
        public Money HeldPurchase { get; set; }
        public Money Arrears { get; set; }
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
                OpeningBalance = Money.FromCents(income) * 7, Balance = Money.FromCents(income) * 7, NextPostingMinute = minute };
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
            // Compute every amount before assigning any: an overflow leaves the budget unchanged.
            var income = Money.FromCents(budget.DailyIncomeCents);
            var balance = budget.Balance + income;
            var received = budget.IncomeReceived + income;
            // Fund a real-cost subsistence basket before discretionary spending. A failed
            // payment does not provision food and never turns a declined shop visit into a meal.
            var homeFood = Money.FromCents(budget.DailyHomeFoodCents);
            bool fed = balance >= homeFood;
            var homeFoodPaid = fed ? budget.HomeFoodPaid + homeFood : budget.HomeFoodPaid;
            if (fed) balance -= homeFood;
            int unfed = fed ? budget.DaysWithoutHomeFood : checked(budget.DaysWithoutHomeFood + 1);
            var required = budget.Arrears + Money.FromCents(budget.DailyHousingCents) + Money.FromCents(budget.DailyEssentialsCents);
            var paid = Money.Min(balance, required);
            var essentials = budget.EssentialsPaid + paid;
            budget.Balance = balance - paid; budget.IncomeReceived = received; budget.HomeFoodPaid = homeFoodPaid;
            budget.EssentialsPaid = essentials; budget.Arrears = required - paid; budget.DaysWithoutHomeFood = unfed;
            if (fed) budget.HomeFoodFundedDay = day;
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

        public static Money SpendingLimit(ResidentBudget budget)
        {
            if (budget.HeldPurchase != Money.Zero || budget.Arrears != Money.Zero) return Money.Zero;
            // Keep the next day's basic bill available. Savings can fund a considered purchase,
            // but replenishing a daily allowance never creates spendable money.
            var reserve = Money.FromCents((long)budget.DailyHousingCents + budget.DailyEssentialsCents + budget.DailyHomeFoodCents);
            return Money.Min(Money.FromCents(1000000), Money.Max(Money.Zero, budget.Balance - reserve));
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

        /// <summary>Hold funds only if the hold and its later confirmation both fit the money unit,
        /// so neither can fail after native checkout. A refused hold changes nothing.</summary>
        public static bool HoldPurchase(ResidentBudget budget, Money price)
        {
            if (price <= Money.Zero || price > SpendingLimit(budget)
                || !Money.TryAdd(budget.PreparedFoodSpent, price, out _)) return false;
            budget.Balance -= price; budget.HeldPurchase = price; return true;
        }
        public static void ConfirmPurchase(ResidentBudget budget)
        { budget.PreparedFoodSpent += budget.HeldPurchase; budget.HeldPurchase = Money.Zero; }
        public static void RefusePurchase(ResidentBudget budget)
        { budget.Balance += budget.HeldPurchase; budget.HeldPurchase = Money.Zero; }
    }
}
