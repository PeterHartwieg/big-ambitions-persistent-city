using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public enum ResidentIncomeSource { ExternalEmployment, ExternalPension, ExternalSupport }

    /// <summary>GR-1: the drawn daily essentials (<see cref="ResidentBudget.DailyEssentialsCents"/>) cut into categories.
    /// The draw and its total are unchanged; a category that has become a need (<see cref="EssentialNeeds"/>) is bought
    /// through the wallet by its own spender and is no longer deducted at the daily posting, so it is never paid twice.</summary>
    public static class HouseholdEssentials
    {
        public const string Utilities = "utilities", HouseholdGoods = "household-goods", Clothing = "clothing", PersonalCare = "personal-care";
        // Whole percent of the daily essentials; the last row takes the rounding remainder, so the shares sum exactly.
        private static readonly (string Category, int Percent)[] Shares =
            { (Utilities, 40), (HouseholdGoods, 25), (Clothing, 20), (PersonalCare, 15) };
        private static readonly string[] Names = { Utilities, HouseholdGoods, Clothing, PersonalCare };
        public static IReadOnlyList<string> Categories => Names;
        public static bool Known(string category) => Array.IndexOf(Names, category) >= 0;
        /// <summary>Utilities are billed, never bought at a business, so they cannot become a need.</summary>
        public static bool CanBecomeNeed(string category) => Known(category) && category != Utilities;

        /// <summary>This category's whole cents of the daily essentials.</summary>
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

        /// <summary>The part of the daily essentials the posting still deducts.</summary>
        public static int Payable(int dailyEssentialsCents, EssentialNeeds? needs)
        {
            if (needs == null || needs.Count == 0) return dailyEssentialsCents;
            int payable = dailyEssentialsCents;
            foreach (var category in needs.Categories) payable -= Share(dailyEssentialsCents, category);
            return payable;
        }
    }

    /// <summary>GR-1: the essential categories that are needs, bought by their own spender from the person's wallet.
    /// Empty until the need model (GR-3) names one.</summary>
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

    /// <summary>A person's wallet (GR-1: owned by <see cref="CitizenState.Wallet"/>; food is one spender of it).
    /// Individual budget unit, not an inferred family. Policy rates are whole cents;
    /// ledger amounts are exact <see cref="Money"/>.
    /// Income and essentials cross an explicit outside-economy boundary. Native business
    /// payroll is not credited a second time and native retail remains the sale authority.</summary>
    public sealed class ResidentBudget
    {
        // F6c: transient city totals for the read-only food panel; never saved. Each setter reports the exact
        // change so the panel needs no consumer scan. Unattached budgets (tests, codecs, copies) pay one null check.
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
            // Opening savings are an explicit endowment, not a first earned wage.
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
                return; // endowment pays purchases; no city housing, essentials or daily outside allowance
            }
            // Refresh only the next unposted wage. Earlier postings, savings and holds
            // retain their exact amounts through job transitions and reloads.
            var source = person != null ? IncomeSource(person) : budget.IncomeSource;
            int dailyIncome = person != null ? IncomeAmount(budget.BaseIncomeCents, source) : budget.DailyIncomeCents;
            // Compute every change, including the wage refresh, before assigning any: an overflow
            // leaves the budget unchanged.
            // A visitor's carried outside employer has no logical city shift to accrue. Keep that
            // existing daily income until an employment outcome replaces it; city jobs earn only shifts.
            bool carriedOutsideEmployment = person?.Visitor != null
                && person.VisitorCarried == ResidentIncomeSource.ExternalEmployment && !EmploymentWages.Eligible(person);
            var income = source == ResidentIncomeSource.ExternalEmployment && !carriedOutsideEmployment
                ? Money.Zero : Money.FromCents(dailyIncome);
            var balance = budget.Balance + income;
            var received = budget.IncomeReceived + income;
            // Fund a real-cost subsistence basket before discretionary spending. A failed
            // payment does not provision food and never turns a declined shop visit into a meal.
            var homeFood = Money.FromCents(budget.DailyHomeFoodCents);
            bool fed = balance >= homeFood;
            var homeFoodPaid = fed ? budget.HomeFoodPaid + homeFood : budget.HomeFoodPaid;
            if (fed) balance -= homeFood;
            int unfed = fed ? budget.DaysWithoutHomeFood : checked(budget.DaysWithoutHomeFood + 1);
            // Essentials that are needs are paid by their own spender, not here (GR-1 re-cut).
            var required = budget.Arrears + Money.FromCents(budget.DailyHousingCents)
                + Money.FromCents(HouseholdEssentials.Payable(budget.DailyEssentialsCents, needs));
            var paid = Money.Min(balance, required);
            var essentials = budget.EssentialsPaid + paid;
            balance -= paid;
            // The save contract also sums the conservation totals (CashConserved), which can overflow
            // when every amount fits. Posting adds the income to both sides of a conserved ledger, so
            // the funded total bounds the accounted one.
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

        /// <summary>The save contract's cash conservation: opening savings plus income received equals
        /// cash plus every payment and hold. Both totals are checked, so a total beyond one amount's
        /// range throws <see cref="OverflowException"/> even when every component fits. Holds,
        /// confirmations and refunds move money between accounted amounts and leave both totals unchanged.</summary>
        public static bool CashConserved(ResidentBudget b)
            => b.OpeningBalance + b.Ledger.EmploymentIncome + b.IncomeReceived == b.Balance + b.EssentialsPaid + b.HomeFoodPaid + b.PreparedFoodSpent + b.ExternalFoodSpent + b.Ledger.Discretionary + b.HeldPurchase;

        internal static void CheckCash(ResidentBudget budget)
        { if (!CashConserved(budget)) throw new InvalidOperationException("Household cash is not conserved."); }

        // A schema-27 visitor's source is its own (F5b PersonIncome.Select over its Employment and carried outside
        // employer), never an anchor's. F5e: a resident's comes from its own Employment (built from DayPlaceId) through
        // the same rule, which equals today's resident rule; a Study enrolment never gives employment income.
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
            // Keep the next day's basic bill available. Savings can fund a considered purchase,
            // but replenishing a daily allowance never creates spendable money.
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

        /// <summary>Hold funds only if the hold and its later confirmation both fit the money unit,
        /// so neither can fail after native checkout. A refused hold changes nothing.</summary>
        public static bool HoldPurchase(ResidentBudget budget, Money price)
        {
            if (price <= Money.Zero || price > SpendingLimit(budget)
                || !Money.TryAdd(budget.PreparedFoodSpent, price, out _)) return false;
            CheckCash(budget);
            budget.Balance -= price; budget.HeldPurchase = price; CheckCash(budget); return true;
        }
        /// <summary>F6j: return the unsold part of a held ticket before confirming the rest.</summary>
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
