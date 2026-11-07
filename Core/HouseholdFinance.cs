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
        // F6c: transient city totals for the read-only food panel; never saved. Each setter reports the exact
        // change so the panel needs no consumer scan. Unattached budgets (tests, codecs, copies) pay one null check.
        internal FoodMoneyCounters? counters;
        private int dailyHousingCents, dailyEssentialsCents, dailyHomeFoodCents, lastPostedDay = -1, homeFoodFundedDay = -1;
        private Money openingBalance, balance, incomeReceived, essentialsPaid, homeFoodPaid, preparedFoodSpent, externalFoodSpent, heldPurchase, arrears;
        public ResidentIncomeSource IncomeSource { get; set; }
        public int BaseIncomeCents { get; set; }
        public int DailyIncomeCents { get; set; }
        public int DailyHousingCents { get => dailyHousingCents; set { var c = counters; if (c == null) dailyHousingCents = value; else { int before = FoodMoneyCounters.Flags(this); dailyHousingCents = value; c.Reflag(before, this); } } }
        public int DailyEssentialsCents { get => dailyEssentialsCents; set { var c = counters; if (c == null) dailyEssentialsCents = value; else { int before = FoodMoneyCounters.Flags(this); dailyEssentialsCents = value; c.Reflag(before, this); } } }
        public int DailyHomeFoodCents { get => dailyHomeFoodCents; set { var c = counters; if (c == null) dailyHomeFoodCents = value; else { int before = FoodMoneyCounters.Flags(this); dailyHomeFoodCents = value; c.Reflag(before, this); } } }
        public Money OpeningBalance { get => openingBalance; set { var c = counters; if (c == null) openingBalance = value; else { int before = FoodMoneyCounters.Flags(this); var old = openingBalance; openingBalance = value; c.Moved(FoodMoneyField.OpeningBalance, old, value, before, this); } } }
        public Money Balance { get => balance; set { var c = counters; if (c == null) balance = value; else { int before = FoodMoneyCounters.Flags(this); var old = balance; balance = value; c.Moved(FoodMoneyField.Balance, old, value, before, this); } } }
        public Money IncomeReceived { get => incomeReceived; set { var c = counters; if (c == null) incomeReceived = value; else { int before = FoodMoneyCounters.Flags(this); var old = incomeReceived; incomeReceived = value; c.Moved(FoodMoneyField.IncomeReceived, old, value, before, this); } } }
        public Money EssentialsPaid { get => essentialsPaid; set { var c = counters; if (c == null) essentialsPaid = value; else { int before = FoodMoneyCounters.Flags(this); var old = essentialsPaid; essentialsPaid = value; c.Moved(FoodMoneyField.EssentialsPaid, old, value, before, this); } } }
        public Money HomeFoodPaid { get => homeFoodPaid; set { var c = counters; if (c == null) homeFoodPaid = value; else { int before = FoodMoneyCounters.Flags(this); var old = homeFoodPaid; homeFoodPaid = value; c.Moved(FoodMoneyField.HomeFoodPaid, old, value, before, this); } } }
        public Money PreparedFoodSpent { get => preparedFoodSpent; set { var c = counters; if (c == null) preparedFoodSpent = value; else { int before = FoodMoneyCounters.Flags(this); var old = preparedFoodSpent; preparedFoodSpent = value; c.Moved(FoodMoneyField.PreparedFoodSpent, old, value, before, this); } } }
        public Money ExternalFoodSpent { get => externalFoodSpent; set { var c = counters; if (c == null) externalFoodSpent = value; else { int before = FoodMoneyCounters.Flags(this); var old = externalFoodSpent; externalFoodSpent = value; c.Moved(FoodMoneyField.ExternalFoodSpent, old, value, before, this); } } }
        public Money HeldPurchase { get => heldPurchase; set { var c = counters; if (c == null) heldPurchase = value; else { int before = FoodMoneyCounters.Flags(this); var old = heldPurchase; heldPurchase = value; c.Moved(FoodMoneyField.HeldPurchase, old, value, before, this); } } }
        public Money Arrears { get => arrears; set { var c = counters; if (c == null) arrears = value; else { int before = FoodMoneyCounters.Flags(this); var old = arrears; arrears = value; c.Moved(FoodMoneyField.Arrears, old, value, before, this); } } }
        public int LastPostedDay { get => lastPostedDay; set { var c = counters; if (c == null) lastPostedDay = value; else { int before = FoodMoneyCounters.Flags(this); lastPostedDay = value; c.Reflag(before, this); } } }
        public int HomeFoodFundedDay { get => homeFoodFundedDay; set { var c = counters; if (c == null) homeFoodFundedDay = value; else { int before = FoodMoneyCounters.Flags(this); homeFoodFundedDay = value; c.Reflag(before, this); } } }
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
            var source = person != null ? IncomeSource(person) : budget.IncomeSource;
            int dailyIncome = person != null ? IncomeAmount(budget.BaseIncomeCents, source) : budget.DailyIncomeCents;
            // Compute every change, including the wage refresh, before assigning any: an overflow
            // leaves the budget unchanged.
            var income = Money.FromCents(dailyIncome);
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
            balance -= paid;
            // The save contract also sums the conservation totals (CashConserved), which can overflow
            // when every amount fits. Posting adds the income to both sides of a conserved ledger, so
            // the funded total bounds the accounted one.
            _ = budget.OpeningBalance + received;
            budget.IncomeSource = source; budget.DailyIncomeCents = dailyIncome;
            budget.Balance = balance; budget.IncomeReceived = received; budget.HomeFoodPaid = homeFoodPaid;
            budget.EssentialsPaid = essentials; budget.Arrears = required - paid; budget.DaysWithoutHomeFood = unfed;
            if (fed) budget.HomeFoodFundedDay = day;
            budget.LastPostedDay = day;
            budget.NextPostingMinute = (day + 1) * 1440.0;
        }

        /// <summary>The save contract's cash conservation: opening savings plus income received equals
        /// cash plus every payment and hold. Both totals are checked, so a total beyond one amount's
        /// range throws <see cref="OverflowException"/> even when every component fits. Holds,
        /// confirmations and refunds move money between accounted amounts and leave both totals unchanged.</summary>
        public static bool CashConserved(ResidentBudget b)
            => b.OpeningBalance + b.IncomeReceived == b.Balance + b.EssentialsPaid + b.HomeFoodPaid + b.PreparedFoodSpent + b.ExternalFoodSpent + b.HeldPurchase;

        // A schema-27 visitor's source is its own (F5b PersonIncome.Select over its Employment and carried outside
        // employer), never an anchor's. Residents keep today's rule, which that function equals for them.
        private static ResidentIncomeSource IncomeSource(CitizenState person)
            => person.Visitor != null ? PersonIncome.Select(person.Identity.Persona, person.Identity.Age, VisitorEmployment(person), person.VisitorCarried)
            : person.Identity.Persona == Persona.Retiree ? ResidentIncomeSource.ExternalPension
                : person.Identity.Age >= 18 && ResidentLifecycle.IsWorker(person) && person.DayPlaceId.Length != 0
                    ? ResidentIncomeSource.ExternalEmployment : ResidentIncomeSource.ExternalSupport;

        // The routine's DayPlaceId is the Employment (its person record is synced from it at each commit).
        private static Employment? VisitorEmployment(CitizenState person) => person.DayPlaceId.Length == 0 ? null
            : new Employment { PersonId = person.Identity.Id, WorkplaceId = person.DayPlaceId,
                Kind = person.Identity.Persona == Persona.Student ? EmploymentKind.Study : EmploymentKind.Work };

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
