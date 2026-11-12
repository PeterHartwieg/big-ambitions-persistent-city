using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public enum ResidentIncomeSource { ExternalEmployment, ExternalPension, ExternalSupport }

    public static class HouseholdEssentials
    {
        public const string Utilities = "utilities", HouseholdGoods = "household-goods", Clothing = "clothing", PersonalCare = "personal-care";
        private static readonly (string Category, int Percent)[] Shares =
            { (Utilities, 40), (HouseholdGoods, 25), (Clothing, 20), (PersonalCare, 15) };
        private static readonly string[] Names = { Utilities, HouseholdGoods, Clothing, PersonalCare };
        public static IReadOnlyList<string> Categories => Names;
        public static bool Known(string category) => Array.IndexOf(Names, category) >= 0;
        public static bool CanBecomeNeed(string category) => Known(category) && category != Utilities;

        public static int Share(int dailyEssentialsCents, string category)
        {
            if (dailyEssentialsCents < 0) throw new ArgumentOutOfRangeException(nameof(dailyEssentialsCents));
            int taken = 0;
            for (int i = 0; i < Shares.Length; i++)
            {
                int part = i == Shares.Length - 1 ? dailyEssentialsCents - taken : (int)((long)dailyEssentialsCents * Shares[i].Percent / 100);
                if (Shares[i].Category == category) return part;
                taken += part;
            }
            throw new ArgumentException("Unknown essential category: " + category, nameof(category));
        }

        public static int Payable(int dailyEssentialsCents, EssentialNeeds? needs)
        {
            if (needs == null || needs.Count == 0) return dailyEssentialsCents;
            int payable = dailyEssentialsCents;
            foreach (var category in needs.Categories) payable -= Share(dailyEssentialsCents, category);
            return payable;
        }
    }

    public sealed class EssentialNeeds
    {
        private readonly List<string> categories = new List<string>();
        public static EssentialNeeds None { get; } = new EssentialNeeds();
        public int Count => categories.Count;
        public IReadOnlyList<string> Categories => categories;
        public bool Contains(string category) => categories.Contains(category);
        public static EssentialNeeds Of(params string[] categories)
        {
            var needs = new EssentialNeeds();
            foreach (var category in categories)
            {
                if (!HouseholdEssentials.CanBecomeNeed(category)) throw new ArgumentException("Not an essential category that can become a need: " + category);
                if (!needs.categories.Contains(category)) needs.categories.Add(category);
            }
            return needs;
        }
    }

    public sealed class ResidentBudget
    {
        internal FoodMoneyCounters? counters;
        private bool visitorEndowment;
        internal bool VisitorEndowment
        {
            get => visitorEndowment;
            set
            {
                if (visitorEndowment == value) return;
                int before = counters == null ? 0 : FoodMoneyCounters.Flags(this);
                visitorEndowment = value; counters?.Reflag(before, this);
            }
        }
        private int dailyHousingCents, dailyEssentialsCents, dailyHomeFoodCents, lastPostedDay = -1, homeFoodFundedDay = -1;
        private Money openingBalance, balance, incomeReceived, essentialsPaid, homeFoodPaid, preparedFoodSpent, externalFoodSpent, heldPurchase, arrears;
        private OwnerAccounts ledger;
        public OwnerAccounts Ledger
        {
            get => ledger;
            set { var before = ledger.EmploymentIncome; ledger = value; counters?.EmploymentMoved(before, value.EmploymentIncome); }
        }
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
            return budget;
        }

        public static void PostDay(ResidentBudget budget, double minute, CitizenState? person = null, bool visitorEndowment = false, EssentialNeeds? needs = null)
            => PostDayCore(budget, minute, person, visitorEndowment, needs);

        private static void PostDayCore(ResidentBudget budget, double minute, CitizenState? person, bool visitorEndowment, EssentialNeeds? needs)
        {
            int day = checked((int)(minute / 1440));
            if (minute != budget.NextPostingMinute || day <= budget.LastPostedDay)
                throw new InvalidOperationException("Household posting is duplicate or out of order.");
            if (visitorEndowment && person?.IsVisitor == true)
            {
                CheckCash(budget);
                budget.VisitorEndowment = true;
                budget.LastPostedDay = day; budget.NextPostingMinute = (day + 1) * 1440.0;
                return;
            }
            var source = person != null ? IncomeSource(person) : budget.IncomeSource;
            int dailyIncome = person != null ? IncomeAmount(budget.BaseIncomeCents, source) : budget.DailyIncomeCents;
            bool carriedOutsideEmployment = person?.Visitor != null
                && person.VisitorCarried == ResidentIncomeSource.ExternalEmployment && !EmploymentWages.Eligible(person);
            var income = source == ResidentIncomeSource.ExternalEmployment && !carriedOutsideEmployment
                ? Money.Zero : Money.FromCents(dailyIncome);
            var balance = budget.Balance + income;
            var received = budget.IncomeReceived + income;
            var homeFood = Money.FromCents(budget.DailyHomeFoodCents);
            bool fed = balance >= homeFood;
            var homeFoodPaid = fed ? budget.HomeFoodPaid + homeFood : budget.HomeFoodPaid;
            if (fed) balance -= homeFood;
            int unfed = fed ? budget.DaysWithoutHomeFood : checked(budget.DaysWithoutHomeFood + 1);
            var required = budget.Arrears + Money.FromCents(budget.DailyHousingCents)
                + Money.FromCents(HouseholdEssentials.Payable(budget.DailyEssentialsCents, needs));
            var paid = Money.Min(balance, required);
            var essentials = budget.EssentialsPaid + paid;
            balance -= paid;
            _ = budget.OpeningBalance + budget.Ledger.EmploymentIncome + received;
            CheckCash(budget);
            budget.IncomeSource = source; budget.DailyIncomeCents = dailyIncome;
            budget.Balance = balance; budget.IncomeReceived = received; budget.HomeFoodPaid = homeFoodPaid;
            budget.EssentialsPaid = essentials; budget.Arrears = required - paid; budget.DaysWithoutHomeFood = unfed;
            if (fed) budget.HomeFoodFundedDay = day;
            budget.LastPostedDay = day;
            budget.NextPostingMinute = (day + 1) * 1440.0;
            CheckCash(budget);
        }

        public static bool CashConserved(ResidentBudget b)
            => b.OpeningBalance + b.Ledger.EmploymentIncome + b.IncomeReceived == b.Balance + b.EssentialsPaid + b.HomeFoodPaid + b.PreparedFoodSpent + b.ExternalFoodSpent + b.Ledger.Discretionary + b.HeldPurchase;

        internal static void CheckCash(ResidentBudget budget)
        { if (!CashConserved(budget)) throw new InvalidOperationException("Household cash is not conserved."); }

        private static ResidentIncomeSource IncomeSource(CitizenState person)
            => person.Visitor != null ? PersonIncome.Select(person.Identity.Persona, person.Identity.Age, person.Visitor.Employment, person.VisitorCarried)
            : PersonIncome.ForDayPlace(person.Identity.Persona, person.Identity.Age, person.DayPlaceId);

        private static int IncomeAmount(int basis, ResidentIncomeSource source)
            => source == ResidentIncomeSource.ExternalPension ? basis * CityBalance.PensionIncomePercent / 100
                : source == ResidentIncomeSource.ExternalSupport ? basis * CityBalance.SupportIncomePercent / 100 : basis;

        internal static Money SpendingReserve(ResidentBudget budget) => budget.VisitorEndowment ? Money.Zero
            : Money.FromCents((long)budget.DailyHousingCents + budget.DailyEssentialsCents + budget.DailyHomeFoodCents);

        public static Money SpendingLimit(ResidentBudget budget)
        {
            if (budget.HeldPurchase != Money.Zero || budget.Arrears != Money.Zero) return Money.Zero;
            var reserve = SpendingReserve(budget);
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

        public static bool HoldPurchase(ResidentBudget budget, Money price)
        {
            if (price <= Money.Zero || price > SpendingLimit(budget)
                || !Money.TryAdd(budget.PreparedFoodSpent, price, out _)) return false;
            CheckCash(budget);
            budget.Balance -= price; budget.HeldPurchase = price; CheckCash(budget); return true;
        }
        public static void ReleasePart(ResidentBudget budget, Money amount)
        {
            if (amount == Money.Zero) return;
            if (amount < Money.Zero || amount > budget.HeldPurchase) throw new InvalidOperationException("Release exceeds the held purchase.");
            CheckCash(budget); budget.Balance += amount; budget.HeldPurchase -= amount; CheckCash(budget);
        }
        public static void ConfirmPurchase(ResidentBudget budget)
        { CheckCash(budget); budget.PreparedFoodSpent += budget.HeldPurchase; budget.HeldPurchase = Money.Zero; CheckCash(budget); }
        public static void RefusePurchase(ResidentBudget budget)
        { CheckCash(budget); budget.Balance += budget.HeldPurchase; budget.HeldPurchase = Money.Zero; CheckCash(budget); }
    }
}
