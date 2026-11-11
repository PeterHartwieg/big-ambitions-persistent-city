using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentCity.Core
{
    public sealed partial class FoodCommerce
    {
        // Daily and shift postings share one stable minute/ordinal heap and its 32-posting bound.
        // Reuse entries; employment changes repair their existing slot. All due times are derived.
        // GR-1: an entry is a person and that person's own wallet; the food roster is not consulted.
        private sealed class BudgetQueue
        {
            private sealed class Entry
            {
                internal CitizenState Person = null!;
                internal ResidentBudget Wallet = null!;
                internal string Id = "";
                internal double Minute, WageAt = double.PositiveInfinity;
                internal int Index;
                internal WorkWindow Window;
                internal string Workplace = "";
                // GR-2: the plan items the window was read with; a newly drawn or replanned plan schedules the entry again.
                internal object? PlanItems;
            }
            private readonly FoodCommerce food;
            private readonly List<Entry> items = new List<Entry>();
            private readonly Dictionary<string, Entry> byPerson = new Dictionary<string, Entry>(StringComparer.Ordinal);
            internal BudgetQueue(FoodCommerce food) { this.food = food; }
            public int Count => items.Count;
            internal bool Contains(string personId) => byPerson.ContainsKey(personId);
            public CitizenState Min => items[0].Person;
            public double Minute => items.Count == 0 ? double.PositiveInfinity : items[0].Minute;
            public void Add(CitizenState person)
            {
                var wallet = person.Wallet ?? throw new InvalidOperationException("A posted person has no wallet: " + person.Identity.Id + ".");
                var e = new Entry { Person = person, Wallet = wallet, Id = person.Identity.Id };
                wallet.VisitorEndowment = food.city.VisitorEndowments && person.IsVisitor;
                items.Add(e);
                byPerson.Add(e.Id, e);
            }
            internal void RestoreVisitorWalletModes()
            {
                foreach (var e in items)
                    e.Wallet.VisitorEndowment = food.city.VisitorEndowments && e.Person.IsVisitor;
            }
            public void Build()
            {
                items.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
                for (int i = 0; i < items.Count; i++)
                {
                    var e = items[i]; e.Index = i;
                    Schedule(e); e.Minute = Math.Min(e.Wallet.NextPostingMinute, e.WageAt);
                }
                for (int i = items.Count / 2 - 1; i >= 0; i--) Repair(i);
            }
            private void Schedule(Entry e)
            {
                e.Workplace = e.Person.DayPlaceId; e.PlanItems = e.Person.Plan?.Items; e.WageAt = double.PositiveInfinity;
                if (!EmploymentWages.Eligible(e.Person)) return;
                double now = food.city.State.Minute;
                e.WageAt = EmploymentWages.NextPosting(e.Person, food.city.State.Seed, now, out e.Window, food.city.PlaceIndex);
                if (e.WageAt < now) throw new InvalidOperationException("Overdue shift posting; preserve the city.");
            }
            // Ties break by ordinal person ID, the order Build sorts by (before F5g: that rank as an int). F5g compares the IDs
            // themselves so a person enrolled later (Insert) takes its rank without renumbering every entry.
            private static int Compare(Entry a, Entry b)
            {
                int time = a.Minute.CompareTo(b.Minute);
                return time == 0 ? string.CompareOrdinal(a.Id, b.Id) : time;
            }
            /// <summary>F5g: adds the wallet of a person created after the queue was built, in its ordinary heap position.</summary>
            internal void Insert(CitizenState person)
            {
                Add(person);
                var e = items[items.Count - 1];
                Schedule(e); e.Minute = Math.Min(e.Wallet.NextPostingMinute, e.WageAt);
                int at = items.Count - 1;
                while (at > 0)
                {
                    int parent = (at - 1) / 2;
                    if (Compare(items[parent], e) <= 0) break;
                    items[at] = items[parent]; items[at].Index = at; at = parent;
                }
                items[at] = e; e.Index = at;
            }
            public void PostMinimum()
            {
                var e = items[0]; var budget = e.Wallet;
                if (e.WageAt <= budget.NextPostingMinute)
                {
                    if (!EmploymentWages.Active(e.Person.Accrual))
                        EmploymentWages.Begin(e.Person, e.Window, food.city.WorkplaceOpen(e.Window.PlaceId, e.WageAt));
                    else EmploymentWages.Settle(e.Person, budget, e.Window, e.WageAt);
                    Schedule(e);
                }
                else HouseholdFinance.PostDay(budget, food.city.State.Minute, e.Person, food.city.VisitorEndowments, food.EssentialNeeds);
                e.Minute = Math.Min(budget.NextPostingMinute, e.WageAt);
                Repair(0);
            }
            internal void Leave(CitizenState live, CitizenState working, bool force = false)
            {
                if (byPerson.TryGetValue(live.Identity.Id, out var e))
                    EmploymentWages.Leave(live, working, e.Window, food.city.State.Minute, force);
            }
            internal void Changed(CitizenState person)
            {
                if (!byPerson.TryGetValue(person.Identity.Id, out var e)) return;
                if (e.Workplace == person.DayPlaceId)
                {
                    if (ReferenceEquals(e.PlanItems, person.Plan?.Items)) return;
                    // A running shift keeps the window it began with; only a shift not yet begun follows a new plan.
                    if (EmploymentWages.Active(person.Accrual)) { e.PlanItems = person.Plan?.Items; return; }
                }
                Schedule(e); e.Minute = Math.Min(e.Wallet.NextPostingMinute, e.WageAt);
                int at = e.Index;
                while (at > 0)
                {
                    int parent = (at - 1) / 2;
                    if (Compare(items[parent], e) <= 0) break;
                    items[at] = items[parent]; items[at].Index = at; at = parent;
                }
                items[at] = e; e.Index = at; Repair(at);
            }
            private void Repair(int at)
            {
                var e = items[at];
                while (at * 2 + 1 < items.Count)
                {
                    int child = at * 2 + 1;
                    if (child + 1 < items.Count && Compare(items[child + 1], items[child]) < 0) child++;
                    if (Compare(e, items[child]) <= 0) break;
                    items[at] = items[child]; items[at].Index = at; at = child;
                }
                items[at] = e; e.Index = at;
            }
        }
        private readonly BudgetQueue budgetDue;
        private double NextBudgetMinute => budgetDue.Minute;
        /// <summary>GR-1: essential categories bought as needs, which the daily posting no longer deducts. Empty until the
        /// need model (GR-3) sets it.</summary>
        internal EssentialNeeds EssentialNeeds { get; set; } = EssentialNeeds.None;
        /// <summary>GR-1: binds every roster record of the live city to its person's wallet when food attaches. The wallet
        /// is the person's; a record built in memory with only a budget hands that budget to its person (adopts it), and two
        /// different objects refuse. Not SaveCodec.LinkWallets, which overwrites every link from the persons and is only for
        /// read or snapshot state.</summary>
        private void AdoptRecordWallets()
        {
            foreach (var meal in state.Residents)
            {
                var person = city.FindCitizen(meal.ResidentId);
                if (person == null) { if (meal.Budget != null) throw new InvalidOperationException("A wallet has no person: " + meal.ResidentId + "."); continue; }
                if (person.Wallet == null) person.Wallet = meal.Budget;
                else if (meal.Budget == null) meal.Budget = person.Wallet;
                else if (!ReferenceEquals(person.Wallet, meal.Budget)) throw new InvalidOperationException("A food record holds a wallet that is not its person's: " + meal.ResidentId + ".");
            }
        }
        /// <summary>F5g: enrols a person created at a day boundary exactly once: its own meal record (meal rhythm from the
        /// current day) and, with finances on, its wallet from <see cref="HouseholdFinance.Create"/> with its portal
        /// district's mix. An existing record is refused, never re-seeded.</summary>
        internal void EnrolCreated(CitizenState person)
        {
            if (records.ContainsKey(person.Identity.Id)) throw new InvalidOperationException("A created person already has a meal record: " + person.Identity.Id + ".");
            var meal = Enrol(person);
            rivalLoad.Observe(meal, RivalServiceCalibration.Period(city.State.Minute));
            due.Add(meal);
            if (meal.Budget != null) budgetDue.Insert(person);
        }
        internal void RestoreVisitorWalletModes() => budgetDue.RestoreVisitorWalletModes();
        internal bool TracksEmployment(CitizenState person) => EmploymentWages.Eligible(person) && budgetDue.Contains(person.Identity.Id);
        internal bool PostingDueAt(double minute) => NextBudgetMinute == minute;
        internal void AccrueWorkDeparture(CitizenState live, CitizenState working) => budgetDue.Leave(live, working);
        internal void AccrueWorkDeparture(CitizenState person) => budgetDue.Leave(person, person, true);
        internal void EmploymentChanged(CitizenState person) => budgetDue.Changed(person);
        internal void EnableFinances()
        {
            if (state.Residents.Any(m => m.LegacyExternalFoodSpent != Money.Zero)) throw new InvalidOperationException("Preserve the legacy external-food ledger; use a household-finance scope before new outside purchases.");
            if (state.HouseholdFinanceVersion != 0) throw new InvalidOperationException("Household finances are already active.");
            if (state.Residents.Any(m => Active(m) || m.Status == MealStatus.Uncertain))
                throw new InvalidOperationException("Finish current purchases before enabling household finances.");
            state.HouseholdFinanceVersion = HouseholdFinance.PolicyVersion;
            foreach (var meal in state.Residents)
            { var person = Owner(meal)!; CreateBudget(meal, person); budgetDue.Add(person); }
            budgetDue.Build();
        }
        private void CreateBudget(ResidentMeal meal, CitizenState person)
        {
            string district = city.FindPlace(person.HomeId).District;
            // The wallet is the person's; the food record only links to it.
            person.Wallet = HouseholdFinance.Create(person, city.State.Seed, city.State.Minute,
                state.Districts.SingleOrDefault(d => d.DistrictId == district), meal.ResidentId);
            meal.Budget = person.Wallet;
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
            double minute = budgetDue.Minute;
            int posted = 0;
            do
            {
                budgetDue.PostMinimum();
                posted++;
                LastBudgetPostings = posted; BudgetPostings++;
            }
            while (posted < BudgetPostingsPerEvent && budgetDue.Minute == minute);
            return true;
        }
        private Money AvailableForMeal(ResidentMeal meal)
        {
            if (meal.Budget != null) meal.Budget.VisitorEndowment = city.VisitorEndowments && Owner(meal)?.IsVisitor == true;
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
