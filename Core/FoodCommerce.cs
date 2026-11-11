using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PersistentCity.Core
{
    /// <summary>Opt-in diagnostic of one decision. Not retained by the simulation or saved.
    /// Subscribers must copy anything they retain and must not change these decision inputs.</summary>
    public sealed class FoodDecisionTrace
    {
        public FoodConsumer Consumer { get; set; } = null!;
        public FoodIntent Intent { get; set; } = null!;
        public IReadOnlyList<FoodBusiness> Businesses { get; set; } = Array.Empty<FoodBusiness>();
        public IReadOnlyList<FoodTravelQuote> Quotes { get; set; } = Array.Empty<FoodTravelQuote>();
        public Dictionary<string, string> RouteChecks { get; set; } = new Dictionary<string, string>();
        public MealKind MealKind { get; set; }
        public IEnumerable<object> ChoiceCosts => Intent.Choices.Select(c => new {
            c.BusinessId, c.ProductId, c.Price, c.Reason, c.ScoreCents,
            FitCents = MealRhythm.ProductPenalty(MealKind, c.ProductId),
            c.ServiceQualityAdjustmentCents, c.DistrictAppealAdjustmentCents,
            PreferenceCents = c.ProductId == Consumer.PreferredProductId ? -Consumer.PreferenceBonusCents : 0,
            TimeCents = c.ScoreCents - c.Price.ApproximateCents() - c.ServiceQualityAdjustmentCents - c.DistrictAppealAdjustmentCents
                - MealRhythm.ProductPenalty(MealKind, c.ProductId)
                + (c.ProductId == Consumer.PreferredProductId ? Consumer.PreferenceBonusCents : 0),
            HomeFoodMarginCents = Intent.OutsideOptionScoreCents - c.ScoreCents
        });
        public int RouteCalls { get; set; }
        public double ElapsedMilliseconds { get; set; }
        public FoodDecisionState? Decision { get; set; }
        /// <summary>F6j: each feasible candidate main with the extras the same policy would add from its captured
        /// menu: the ticket whose total the choice compared as that shop's expense.</summary>
        public IReadOnlyList<FoodMealTicket> Tickets { get; set; } = Array.Empty<FoodMealTicket>();
        /// <summary>Shortlist/hash/traits/sampling work only, excluding supply capture, feasibility and routes.
        /// Null if this diagnostic did not observe the initial capture (for example after reload).</summary>
        public double? ChoiceMilliseconds { get; set; }
    }
    public enum MealStatus { Pending, Travelling, Serving, Paid, Declined, Missed, Uncertain }
    public sealed class MealOuting
    {
        public string OriginId { get; set; } = "";
        public Activity OriginActivity { get; set; }
        public double ResumeUntil { get; set; }
        public double ResumeEvent { get; set; }
        public bool Returning { get; set; }
    }
    public sealed class FoodScope : IFoodScopeView
    {
        public FoodAlternativeKind AlternativeKind { get; set; }
        public string PlaceId { get; set; } = "";
        // Keep the original product identity for existing scopes and pending receipts.
        public string ProductId { get; set; } = "";
        public List<string> AdditionalProductIds { get; set; } = new List<string>();
        public bool ContainsProduct(string id) => ProductId == id || AdditionalProductIds.Contains(id);
        public IEnumerable<string> Products() => new[] { ProductId }.Concat(AdditionalProductIds);
        public string BusinessId { get; set; } = "";
        public double NextServiceAt { get; set; }
        /// <summary>Which market serves this scope. The setter is not public, so the save writer does not see it
        /// yet: only Shop scopes exist in a saved city until E6c's writer serializes the channel.</summary>
        public FoodScopeChannel Channel { get; private set; }
        public void UseChannel(FoodScopeChannel channel)
        {
            if (channel != FoodScopeChannel.Shop && channel != FoodScopeChannel.Vehicle) throw new ArgumentOutOfRangeException(nameof(channel));
            Channel = channel;
        }
        // F9e: the read-only view (getters only; no field, wire or save change).
        string IFoodScopeView.PlaceId => PlaceId;
        string IFoodScopeView.BusinessId => BusinessId;
        string IFoodScopeView.ProductId => ProductId;
        IReadOnlyList<string> IFoodScopeView.ExtraProductIds => AdditionalProductIds;
        FoodScopeChannel IFoodScopeView.Channel => Channel;
        FoodAlternativeKind IFoodScopeView.AlternativeKind => AlternativeKind;
        double IFoodScopeView.NextServiceAt => NextServiceAt;
    }
    /// <summary>Shop scopes are served by the native shop market, Vehicle scopes by the Street Food vehicle market.</summary>
    public enum FoodScopeChannel { Shop = 0, Vehicle = 1 }
    public sealed class ResidentMeal
    {
        // F6c: transient panel counters (never saved); see ResidentBudget.counters.
        internal FoodMoneyCounters? counters;
        private MealStatus status;
        private MealExcursion? excursion;
        private ResidentBudget? budget;
        public string ResidentId { get; set; } = "";
        /// <summary>Roster owner. From schema 26 every record is its own person's (AnchorId == ResidentId, Slot 0);
        /// schemas up to 25 stored the represented consumer's host here.</summary>
        public string AnchorId { get; set; } = "";
        public int Slot { get; set; }
        private string? hostId;
        private int? hostSlot;
        /// <summary>Scheduling adapter, never persisted: the resident whose day a represented consumer eats on, and its
        /// slot (0 for the resident's own meals). Today's excursion schedule reads only these; ownership stays the
        /// person's own. Without an assignment it falls back to the stored AnchorId/Slot (pre-26 form).</summary>
        public string HostId => hostId ?? AnchorId;
        public int HostSlot => hostSlot ?? Slot;
        public void AssignHost(string anchorId, int slot)
        {
            if (string.IsNullOrEmpty(anchorId) || slot < 0) throw new ArgumentException("Invalid roster host.");
            hostId = anchorId; hostSlot = slot;
        }
        /// <summary>Save snapshot capture (F7c): copy the transient host assignment exactly, including "none".</summary>
        internal void CopyHostTo(ResidentMeal target) { target.hostId = hostId; target.hostSlot = hostSlot; }
        public string PreferredProductId { get; set; } = "";
        public int PreferenceBonusCents { get; set; }
        public bool NeedsHomeResync { get; set; }
        public MealExcursion? Excursion { get => excursion; set { var c = counters; if (c == null) excursion = value; else { var old = excursion; excursion = value; c.Replaced(old, value); } } }
        public int Day { get; set; }
        public MealKind Kind { get; set; }
        public Money DayAllowanceUsed { get; set; }
        public double DesiredAt { get; set; }
        public double ExpiresAt { get; set; }
        public double DueAt { get; set; }
        public MealStatus Status { get => status; set { var c = counters; if (c == null) status = value; else { var old = status; status = value; c.Restatus(old, value); } } }
        public string PlaceId { get; set; } = "";
        public string BusinessId { get; set; } = "";
        public string ProductId { get; set; } = "";
        public string Reason { get; set; } = "";
        public Money Allowance { get; set; }
        public Money Price { get; set; }
        public double ServiceMinutes { get; set; }
        public double ServiceAt { get; set; }
        public double Deadline { get; set; }
        public double ReturnMinutes { get; set; }
        public ResidentBudget? Budget { get => budget; set { var c = counters; if (c == null) budget = value; else { var old = budget; budget = value; c.Replaced(old, value); } } }
        public bool ExistingStop { get; set; }
        public long ExistingStopPurchases { get; set; }
        public long Purchases { get; set; }
        public Money Spent { get; set; }
        /// <summary>Traits of a represented consumer (slot above zero), keyed by its stable consumer ID.
        /// Null for slot zero, whose traits are its anchor resident's.</summary>
        public ResidentTraits? RepresentedTraits { get; set; }
        /// <summary>Current-day outcomes; null until this consumer's first recorded outcome.</summary>
        public ResidentFoodToday? Today { get; set; }
        /// <summary>The version-1 policy's in-progress decision for this meal; null under the legacy choice.</summary>
        public FoodDecisionState? Decision { get; set; }
        /// <summary>Retry latch for the current meal: the last processed decision context and how many distinct
        /// contexts this meal has used (at most FoodChoicePolicy.MaximumContextsPerMeal). Same-context polling reuses it.</summary>
        public string LastContextKey { get; set; } = "";
        public int ContextAttempts { get; set; }
        public ConsumerMemory Memory { get; set; } = new ConsumerMemory();
        // Empty is player authority. Rival revision/status plus exact advertised-hours fingerprint.
        public string External { get; set; } = "";
        public Money LegacyExternalFoodSpent { get; set; }
        public FoodAlternativeKind TargetKind => External.Length == 0 ? FoodAlternativeKind.Player : FoodAlternativeKind.Rival;
        public FoodAvailabilityEvidenceKind AvailabilityEvidence => TargetKind == FoodAlternativeKind.Rival ? FoodAvailabilityEvidenceKind.EstimatedFromPriceAndHours : FoodAvailabilityEvidenceKind.ObservedPlayerSupply;
        public string IntentId => MealRhythm.IntentId(ResidentId, Day, Kind);
    }
    public sealed class FoodCommerceState
    {
        public RivalOutflowHistory? Outflow { get; set; }
        public List<BusinessReference> BusinessReferences { get; set; } = new List<BusinessReference>();
        public int RepresentationFactor { get; set; } = 1;
        public List<FoodRoutineStop> RoutineStops { get; set; } = new List<FoodRoutineStop>();
        public List<FoodShopDay> History { get; set; } = new List<FoodShopDay>();
        public int HouseholdFinanceVersion { get; set; }
        public int MealRhythmVersion { get; set; }
        public List<FoodScope> Shops { get; set; } = new List<FoodScope>();
        public List<ResidentMeal> Residents { get; set; } = new List<ResidentMeal>();
        public List<FoodDistrictBudgetMix> Districts { get; set; } = new List<FoodDistrictBudgetMix>();
        /// <summary>Food choice policy header. New scopes and migrated cities activate on the next in-game day.
        /// No runtime setting changes either value; committed legacy meals retain their own terms.</summary>
        public int ChoicePolicyVersion { get; set; } = FoodChoicePolicy.Version;
        public int ChoiceStartsDay { get; set; }
        public int AwarenessStartsDay { get; set; }
        public int RivalStartsDay { get; set; }
        public List<FoodCampaignImage> Campaigns { get; set; } = new List<FoodCampaignImage>();
    }
    public readonly struct FoodSale
    {
        public FoodSale(bool paid, string reason, ShopExperience? failure = null, IReadOnlyList<FoodTicketLine>? soldExtras = null,
            bool priceChanged = false)
        { Paid = paid; Reason = reason; Failure = failure; this.soldExtras = soldExtras; PriceChanged = priceChanged && !paid; }
        /// <summary>E5b5 Q2: a definite refusal before any receipt or native order because a menu price changed to another
        /// valid price. The seller kept ownership; the resident may decide again at the counter.</summary>
        public bool PriceChanged { get; }
        private readonly IReadOnlyList<FoodTicketLine>? soldExtras;
        /// <summary>F6j: the ticket's extras the seller actually sold, in ticket order. Empty for a main-only sale.</summary>
        public IReadOnlyList<FoodTicketLine> SoldExtras => soldExtras ?? Array.Empty<FoodTicketLine>();
        public ShopExperience? Failure { get; }
        public bool Paid { get; }
        public string Reason { get; }
    }
    /// <summary>E5b5: a market whose saved businesses can be briefly unvalidated after a load (contract images warming).
    /// Core neither checks out nor drops a commitment to such a business; it waits a minute and asks again.</summary>
    public interface IFoodStartupValidation
    {
        bool AwaitingValidation(string businessId);
    }
    /// <summary>One main-thread sale call must finish before saving the native world plus CityState.
    /// Exceptions leave a persisted Uncertain decision which is never retried automatically.</summary>
    public interface IFoodMarket
    {
        double EarliestMinute { get; }
        IReadOnlyList<FoodBusiness> Read(double minute);
        FoodSale Buy(ResidentMeal meal, double minute);
    }
    /// <summary>Bound expensive native supply reads before materializing offers. Discovery
    /// and business authority remain separate from this local decision shortlist.</summary>
    public interface ILocalFoodMarket
    {
        IReadOnlyList<FoodBusiness> ReadLocal(Place origin, double minute, double walkingToleranceMetres, int maximumCandidates);
        FoodBusiness? ReadBusiness(string businessId, double minute);
    }
    /// <summary>Metadata only; the scope catalogue and cached entrances are ranked before any supply read.</summary>
    public interface IFoodCandidateMarket
    {
        /// <summary>Refresh metadata before ranking, then share it across this atomic capture. No yield or checkout.</summary>
        IDisposable BeginCapture(double minute);
        bool IsCandidate(FoodScope scope);
        FoodBusiness? CaptureBusiness(string businessId, double minute);
    }
    /// <summary>F6j: a seller whose own checkout can sell same-visit extras after the main dish, under the meal's one
    /// receipt. Paid means the main sold; SoldExtras lists the extras that also sold. A seller without this
    /// interface, or returning false, sells the main dish only, so no extra is ever booked without a sale.</summary>
    public interface IFoodTicketMarket
    {
        bool SellsExtras(string businessId);
        FoodSale BuyTicket(ResidentMeal meal, FoodMealTicket ticket, double minute);
    }
    /// <summary>F6j: the seller's confirmed receipt for an uncertain meal, read without a checkout replay. Total is
    /// what actually sold (at most the held planned total); extras are the sold extra lines.</summary>
    public interface IFoodTicketEvidence
    {
        bool TryConfirmedTicket(ResidentMeal meal, out Money total, out IReadOnlyList<FoodTicketLine> extras);
    }
    public interface IFoodBusinessAuthority
    {
        bool CanServe(string businessId);
    }
    /// <summary>Read-only proof of the original checkout. Absence is never proof of refusal.</summary>
    public interface IFoodSaleEvidence
    {
        bool IsConfirmedPaid(ResidentMeal meal);
    }

    /// <summary>Sequential meal opportunities, each paid from its own consumer's budget (with household
    /// finances on, one ResidentBudget per roster record, seeded by consumer ID; no shared or household
    /// wallet). No nutrition or closed-city payroll claim. Events share the population clock and its bounded frame budget.</summary>
    public sealed partial class FoodCommerce
    {
        private sealed class Order : IComparer<ResidentMeal>
        {
            public int Compare(ResidentMeal? a, ResidentMeal? b)
            {
                if (ReferenceEquals(a, b)) return 0;
                if (a == null) return -1; if (b == null) return 1;
                int time = a.DueAt.CompareTo(b.DueAt);
                return time != 0 ? time : StringComparer.Ordinal.Compare(a.ResidentId, b.ResidentId);
            }
        }
        private readonly CitySimulator city;
        private readonly IFoodMarket market;
        private readonly IRoutePlanner routes;
        private readonly FoodCommerceState state;
        private readonly Dictionary<string, ResidentMeal> records;
        private readonly SortedSet<ResidentMeal> due = new SortedSet<ResidentMeal>(new Order());
        private readonly Dictionary<string, int> reservations = new Dictionary<string, int>(StringComparer.Ordinal);
        public double NextMinute => pendingDecision?.Decision?.OriginAt
            ?? Math.Min(NextBudgetMinute, due.Count == 0 ? double.PositiveInfinity : due.Min!.DueAt);
        internal IFoodMarket Market => market;
        public long HistoricalClockMisses { get; private set; }
        internal ResidentMeal? FindMeal(string consumerId) => records.TryGetValue(consumerId, out var meal) ? meal : null;
        /// <summary>Diagnostic (never saved): food or finance lookups of a person other than the meal's own owner, the
        /// pre-27 represented-consumer anchor. Schema 27 keeps it at zero (F5d: no anchor read at all).</summary>
        public long AnchorReads { get; private set; }
        internal int QueuedMeals => due.Count;
        internal int DueMealsAtOrBefore(double minute)
        {
            int count = 0;
            foreach (var meal in due) { if (meal.DueAt > minute) break; count++; }
            return count;
        }
        // The person whose schedule, wallet inputs and traits a meal reads. From schema 27 that is always its owner.
        private CitizenState? Owner(ResidentMeal meal)
        {
            if (!string.Equals(meal.HostId, meal.ResidentId, StringComparison.Ordinal)) AnchorReads++;
            return city.FindCitizen(meal.HostId);
        }
        internal FoodCommerce(CitySimulator city, IFoodMarket market, IRoutePlanner routes, FoodChoiceSettings? choiceSettings = null, FoodMemorySettings? memorySettings = null, FoodAwarenessSettings? awarenessSettings = null, IFoodAwarenessProvider? memory = null, IFoodAwarenessProvider? campaign = null, RivalFoodSettings? rivalSettings = null)
        {
            this.city = city; this.market = market; this.routes = routes;
            budgetDue = new BudgetQueue(this);
            RivalSettings = rivalSettings ?? new RivalFoodSettings(); RivalSettings.Clamp();
            ChoiceSettings = choiceSettings ?? new FoodChoiceSettings();
            ChoiceSettings.Clamp();
            AwarenessSettings=awarenessSettings ?? new FoodAwarenessSettings(); AwarenessSettings.Clamp();
            MemoryProvider=memory;CampaignProvider=campaign;
            state = city.State.Food ?? throw new InvalidOperationException("No food state.");
            state.Outflow ??= RivalOutflowHistory.Begin(state, city.State.Minute);
            MemorySettings = memorySettings ?? new FoodMemorySettings(); MemorySettings.Clamp();
            // Share the campaign publisher's current generation index.
            memoryStore = (campaign as FoodCampaignSource)?.ReferencesFor(state) ?? new FoodMemoryStore(state);
            if(state.Residents.Count==0) { state.ChoiceStartsDay=(int)Math.Floor(city.State.Minute/1440)+1; state.AwarenessStartsDay=state.ChoiceStartsDay; state.RivalStartsDay=state.ChoiceStartsDay; }
            records = state.Residents.ToDictionary(r => r.ResidentId, StringComparer.Ordinal);
            storedMemoryProvider = new StoredMemoryAwareness(this);
            // A loaded roster reads no wallet of its own: link each record to its person's before anything spends.
            AdoptRecordWallets();
            InitializeRepresentation();
            long rivalPeriod = RivalServiceCalibration.Period(city.State.Minute);
            foreach (var entry in state.Residents)
            {
                rivalLoad.Observe(entry, rivalPeriod);
                if (entry.DueAt < city.State.Minute) throw new InvalidOperationException("Overdue meal record.");
                due.Add(entry);
                if (entry.Budget != null) budgetDue.Add(Owner(entry)!);
                if (Active(entry)) Reserve(entry, 1);
                if (entry.Decision != null) pendingDecision = entry;
                if (market is IRivalFoodMarket && (Active(entry) && entry.TargetKind == FoodAlternativeKind.Rival
                    || entry.Decision?.Shops.Any(s => s.AlternativeKind == FoodAlternativeKind.Rival) == true)) rivalResumeNeedsRefresh = true;
            }
            budgetDue.Build();
            Feedback = FoodMoneyCounters.Attach(state);
        }
        private static bool Active(ResidentMeal m) => m.Status == MealStatus.Travelling || m.Status == MealStatus.Serving;
        private static string ReservationKey(string shop, string product) => shop + "\n" + product;
        private void Reserve(ResidentMeal meal, int delta)
        {
            if (meal.TargetKind == FoodAlternativeKind.Rival) return;
            string key = ReservationKey(meal.PlaceId, meal.ProductId);
            reservations.TryGetValue(key, out int count); reservations[key] = count + delta;
        }
        private void ApplyReservations(FoodBusiness business)
        {
            foreach (var offer in business.Offers)
            {
                reservations.TryGetValue(ReservationKey(business.PlaceId, offer.ProductId), out int count);
                if (offer.Units.HasValue) offer.Units = Math.Max(0, offer.Units.Value - count);
            }
        }
        private ResidentMeal Enrol(CitizenState person, int slot = 0)
        {
            string id = FoodRepresentation.ConsumerId(person.Identity.Id, slot);
            if (records.TryGetValue(id, out var existing)) return existing;
            var resident = Resident(person);
            // The record is the person's own; the host only schedules today's represented excursion.
            var meal = new ResidentMeal { ResidentId = id, AnchorId = id, Slot = 0 };
            meal.AssignHost(person.Identity.Id, slot);
            // Schema 27: a visitor is its own person with its own routine; its tastes are drawn by its own ID.
            if (person.IsVisitor) meal.RepresentedTraits = person.Traits;
            if (slot > 0 || person.IsVisitor)
            {
                if (slot > 0) meal.RepresentedTraits = ResidentTraits.Generate(city.State.Seed, id);
                var products = FoodProducts.Ids.OrderBy(p => p, StringComparer.Ordinal).ToArray();
                meal.PreferredProductId = products[StableRandom.Hash(id + ":taste", city.State.Seed) % (uint)products.Length];
                meal.PreferenceBonusCents = 50 + (int)(StableRandom.Hash(id + ":taste-strength", city.State.Seed) % 101);
            }
            var consumer = Consumer(person, Money.Zero, meal);
            FoodHouseholdPolicy.SetAllowance(consumer, resident, city.State.Seed,
                state.Districts.SingleOrDefault(d => d.DistrictId == resident.HomeDistrictId), null);
            meal.Allowance = consumer.DailyAllowance;
            if (state.HouseholdFinanceVersion != 0) CreateBudget(meal, person);
            StartMeal(meal, person, (int)(city.State.Minute / 1440), state.MealRhythmVersion == 0 ? MealKind.Legacy : MealKind.Breakfast);
            records.Add(meal.ResidentId, meal); state.Residents.Add(meal); Feedback?.Add(meal);
            // Schema 26: every roster record is one person; a represented slot is a visitor person (no behaviour reads it).
            if (slot > 0) PersonTable.AddRepresentedVisitor(city.State, id);
            return meal;
        }
        private void StartMeal(ResidentMeal meal, CitizenState person, int day, MealKind kind)
        {
            if (meal.Day != day) meal.DayAllowanceUsed = Money.Zero;
            meal.Day = day; meal.Kind = kind; meal.DesiredAt = MealRhythm.DesiredAt(person, city.State.Seed, day, kind);
            meal.ExpiresAt = MealRhythm.ExpiresAt(person, city.State.Seed, day, kind);
            meal.DueAt = Math.Max(city.State.Minute, meal.DesiredAt);
            meal.Status = MealStatus.Pending; meal.Reason = ""; meal.PlaceId = meal.BusinessId = meal.ProductId = "";
            meal.External = "";
            meal.ExistingStop = false;
            meal.Decision = null; meal.LastContextKey = ""; meal.ContextAttempts = 0;
            meal.Price = Money.Zero; meal.ServiceAt = meal.ServiceMinutes = meal.ReturnMinutes = meal.Deadline = 0;
        }
        internal void Step()
        {
            if (pendingDecision != null)
            {
                var pendingMeal=pendingDecision;
                due.Remove(pendingMeal);
                try { ContinueFoodDecision(pendingMeal,Owner(pendingMeal)!); }
                finally { due.Add(pendingMeal); }
                return;
            }
            if (StepBudget()) return;
            var meal = due.Min!; due.Remove(meal);
            try { StepMeal(meal); DayPlanExecutor.SyncMeals(city.FindCitizen(meal.HostId), meal); }
            finally { due.Add(meal); }
        }
        private void StepMeal(ResidentMeal meal)
        {
            var person = Owner(meal);
            if (person == null) throw new InvalidOperationException("Meal has no resident.");
            double now = city.State.Minute;
            if (meal.Status == MealStatus.Uncertain && meal.Excursion != null)
            {
                // A blocked walking return does not prevent independently proving the
                // original transaction. Movement remains unresolved after proof settles cash.
                try
                {
                    if (ConfirmedEvidence(meal, out var paid, out var extras))
                        SettleConfirmed(meal, paid, extras, "Original native receipt confirmed; no checkout replay");
                }
                catch (Exception) { /* Missing proof preserves held funds and the original receipt. */ }
            }
            if (StepExcursion(meal, person)) { return; }
            // A dormant visitor is off-map: no new demand until its next stay (section 3 Visitor lifecycle). Unresolved
            // obligations (an uncertain receipt above, a held purchase) stay with the same person and wallet.
            if (person.IsVisitor && VisitorSchedules.Dormant(person) && !Active(meal) && meal.Status != MealStatus.Uncertain)
            { SkipDormantMeals(meal, person, now); return; }
            // E5b5: a saved commitment to a business still being validated after a load waits; it is neither bought nor
            // dropped. Deadlines and every other check apply unchanged once validation ends.
            if (Active(meal) && meal.TargetKind == FoodAlternativeKind.Player && market is IFoodStartupValidation startup
                && startup.AwaitingValidation(meal.BusinessId))
            { meal.DueAt = Math.Max(meal.DueAt, now + 1); return; }
            FoodBusiness? closedStand = null;
            if (Active(meal) && meal.TargetKind == FoodAlternativeKind.Player && market is IFoodBusinessAuthority authority && !authority.CanServe(meal.BusinessId))
            {
                closedStand = ClosedStand(meal, now);
                if (closedStand == null)
                { Finish(meal, MealStatus.Missed, "Business no longer accepts this commitment"); return; }
                if (meal.Status == MealStatus.Serving)
                { Finish(meal, MealStatus.Missed, "Service no longer fits shop opening hours"); return; }
                // Keep the captured trip to the old stand. Only reaching it supplies a Closed visit.
            }
            if (!Active(meal) && meal.Status != MealStatus.Pending)
            {
                // Retain the exact unresolved receipt and any held money across later meals.
                if (meal.Status == MealStatus.Uncertain)
                {
                    bool confirmed = false;
                    Money paid = Money.Zero; IReadOnlyList<FoodTicketLine> extras = Array.Empty<FoodTicketLine>();
                    try { confirmed = ConfirmedEvidence(meal, out paid, out extras); }
                    catch (Exception) { /* Unavailable evidence cannot authorize spending or a refund. */ }
                    if (confirmed)
                    {
                        SettleConfirmed(meal, paid, extras, "Original native receipt confirmed; no checkout replay");
                        meal.DueAt = now + .001;
                    }
                    else meal.DueAt = (Math.Floor(now / 1440) + 1) * 1440;
                    return;
                }
                if (meal.HostSlot == 0 && person.MealOuting != null)
                { meal.DueAt = Math.Max(now + 1, city.RoutineRetryMinute(person)); return; }
                NextMeal(meal, out int day, out MealKind kind);
                if (day < (int)(now / 1440)) { day = (int)(now / 1440); kind = state.MealRhythmVersion == 0 ? MealKind.Legacy : MealKind.Breakfast; }
                StartMeal(meal, person, day, kind);
            }
            if (now < meal.DesiredAt) meal.DueAt = meal.DesiredAt;
            else if (now < market.EarliestMinute)
            {
                // Native inventory is current, never replay sales into a historical day/hour.
                if (Active(meal)) { HistoricalClockMisses++; Finish(meal, MealStatus.Missed, "Native clock passed the purchase window"); }
                meal.DueAt = Math.Max(meal.DueAt, market.EarliestMinute);
            }
            else if (meal.Status == MealStatus.Pending)
            {
                if (now >= meal.ExpiresAt) Finish(meal, MealStatus.Missed, "No suitable stop in the meal window");
                else if (meal.HostSlot > 0) DecideRepresented(meal, person);
                else if (AtFoodStop(person)) DecideAtStop(meal, person);
                else if (person.Phase != CitizenPhase.AtPlace || person.MealOuting != null || person.Activity == Activity.Shop
                    || Math.Min(person.NextEventMinute, person.ActivityUntilMinute) - now < 25)
                    meal.DueAt = Math.Min(meal.ExpiresAt, Math.Max(now + 1, city.RoutineRetryMinute(person)));
                else Decide(meal, person);
            }
            else if (meal.Status == MealStatus.Travelling)
            {
                bool admitted = meal.HostSlot > 0 ? meal.Excursion != null && now >= meal.Excursion.ArrivesAt && !meal.Excursion.Returning
                    && now < meal.Deadline && Position.Distance(city.FindPlace(meal.PlaceId).Position, meal.Excursion.DestinationPosition) <= .01
                    : person.MealOuting != null && !person.MealOuting.Returning
                    && person.Phase == CitizenPhase.AtPlace && person.PlaceId == meal.PlaceId
                    && (meal.TargetKind == FoodAlternativeKind.Rival
                        // Visitors keep one LastArrival tuple instead of RecentVisits; residents keep this check unchanged.
                        || (person.Visitor != null ? VisitorSchedules.ArrivedForMeal(person.Visitor, meal.PlaceId, meal.DesiredAt)
                            : person.RecentVisits.Any(v => v.PlaceId == meal.PlaceId && v.Activity == Activity.Shop && v.AtMinute >= meal.DesiredAt)));
                if (admitted)
                {
                    if (meal.TargetKind == FoodAlternativeKind.Rival) { ArriveRival(meal, person, now); return; }
                    memoryStore.CommitStage(meal, ExperienceStage.Reached);
                    var available = ReadBusiness(meal.BusinessId, now) ?? closedStand;
                    if (available?.PlaceId != meal.PlaceId) available = null;
                    if (city.State.Capabilities.Active && (NewChoice(meal) || closedStand != null) && available != null
                        && !DailyRoutine.Open(available.OpeningPlace, now))
                    { Finish(meal, MealStatus.Missed, "Shop closed before admission"); return; }
                    if (available?.MealsPerHour == null || available.MealsPerHour <= 0)
                    {
                        if (city.State.Capabilities.Active && available?.ServiceUnavailableReason == "Out of stock")
                        {
                            var live = FailureEvidence(meal.BusinessId, meal.PlaceId, now, available);
                            if (live?.Id == meal.BusinessId && live.PlaceId == meal.PlaceId && live.ServiceUnavailableReason == "Out of stock"
                                && live.Offers.Any(o => o.ProductId == meal.ProductId && o.Units == 0))
                                Experience(meal, ShopExperience.Stockout, now);
                        }
                        Finish(meal, MealStatus.Missed, "Service unavailable on arrival"); return;
                    }
                    double? close = null;
                    if (meal.HostSlot > 0)
                    {
                        var hours = available.OpeningPlace;
                        var open = DailyRoutine.NextOpen(hours, now, meal.Deadline);
                        if (!open.HasValue)
                        { Finish(meal, MealStatus.Missed, "Shop closed before admission"); return; }
                        if (open.Value > now)
                        { meal.Excursion!.AdmissionAt = open.Value; meal.DueAt = open.Value; return; }
                        close = DailyRoutine.NextClose(hours, now);
                    }
                    meal.ServiceMinutes = 60.0 / available.MealsPerHour.Value;
                    var scope = state.Shops.Single(s => s.PlaceId == meal.PlaceId);
                    meal.ServiceAt = Math.Max(now, scope.NextServiceAt) + meal.ServiceMinutes;
                    if (meal.HostSlot > 0 && Math.Max(0, scope.NextServiceAt - now) > meal.Excursion!.MaximumQueueMinutes)
                        Finish(meal, MealStatus.Missed, "Queue exceeds patience on arrival");
                    else if (meal.HostSlot > 0 && (!close.HasValue || meal.ServiceAt >= close.Value))
                        Finish(meal, MealStatus.Missed, "Service no longer fits shop opening hours");
                    else if (meal.ServiceAt + MealRhythm.EatingMinutes(meal.Kind) + meal.ReturnMinutes > meal.Deadline)
                    {
                        // Preserve the existing refusal and accounts. Attribute queue disappointment
                        // only when removing the observed wait would have made this break fit.
                        if (scope.NextServiceAt > now && now + meal.ServiceMinutes + MealRhythm.EatingMinutes(meal.Kind) + meal.ReturnMinutes <= meal.Deadline)
                            Experience(meal, ShopExperience.Queue, now);
                        Finish(meal, MealStatus.Missed, "Arrival or queue no longer fits the break");
                    }
                    else
                    {
                        if (meal.HostSlot == 0) city.ExtendMealStay(person, meal.ServiceAt + MealRhythm.EatingMinutes(meal.Kind));
                        scope.NextServiceAt = meal.ServiceAt;
                        meal.Status = MealStatus.Serving; meal.DueAt = meal.ServiceAt;
                    }
                }
                else if (meal.HostSlot > 0 || person.MealOuting == null || person.MealOuting.Returning || now >= meal.Deadline)
                    Finish(meal, MealStatus.Missed, "Visit was interrupted before admission");
                else meal.DueAt = Math.Max(now + .001, person.NextEventMinute);
            }
            else if (meal.Status == MealStatus.Serving)
            {
                if (meal.HostSlot > 0 ? meal.Excursion == null || meal.Excursion.Returning || now > meal.Deadline
                    : person.Phase != CitizenPhase.AtPlace || person.PlaceId != meal.PlaceId
                    || (meal.ExistingStop ? person.MealOuting != null || person.Activity != Activity.Shop
                        || now >= Math.Min(person.ActivityUntilMinute, person.NextEventMinute)
                        : person.MealOuting == null || person.MealOuting.Returning))
                    Finish(meal, MealStatus.Missed, "Resident left before service");
                else
                {
                    if (meal.TargetKind == FoodAlternativeKind.Rival) { CompleteRival(meal, now); return; }
                    // Every later addition must fit before funds move or native checkout starts.
                    if (!CanSettle(meal))
                    { Finish(meal, MealStatus.Missed, "Purchase would exceed the exact money range"); return; }
                    if (meal.Budget == null && meal.Price > AvailableForMeal(meal))
                    { Finish(meal, MealStatus.Missed, "Household funds no longer available at checkout"); return; }
                    // F6j: extras are planned from live terms; the wallet check and hold cover the whole ticket.
                    var mainPrice = meal.Price;
                    var limit = CurrentAllowance(meal);
                    var ticket = CheckoutTicket(meal, now);
                    if (ticket != null) { meal.Price = ticket.Total; if (!CanSettle(meal)) { meal.Price = mainPrice; ticket = null; } }
                    if (meal.Budget != null && !HouseholdFinance.HoldPurchase(meal.Budget, meal.Price))
                    { meal.Price = mainPrice; Finish(meal, MealStatus.Missed, "Household funds no longer available at checkout"); return; }
                    // A held ticket may exceed the limit saved at commitment when the wallet grew since; record the
                    // limit it was planned and held within. Never lowered, so a refusal keeps its saved terms.
                    if (ticket != null) meal.Allowance = Money.Max(meal.Allowance, limit);
                    if (meal.Budget == null) meal.DayAllowanceUsed += meal.Price;
                    // Persist uncertainty before entering native code. No exception can trigger a replay.
                    // An uncertain ticket keeps its planned total as Price and as the held amount.
                    meal.ServiceAt = now;
                    Finish(meal, MealStatus.Uncertain, "Native purchase did not confirm completion");
                    try
                    {
                        var result = ticket != null ? ((IFoodTicketMarket)market).BuyTicket(meal, ticket, now) : market.Buy(meal, now);
                        // E5b5 Q2: after a price-only refusal the resident decides once more at the counter, on the live price.
                        if (!result.Paid && result.PriceChanged && result.SoldExtras.Count == 0 && CounterRedecision(meal, now, ref mainPrice))
                        { ticket = null; result = market.Buy(meal, now); }
                        var sold = SoldExtras(ticket, result);
                        Money held = meal.Price, paid = mainPrice;
                        foreach (var line in sold) paid += line.Price;
                        if (result.Paid) meal.Price = paid;
                        if (meal.Budget != null) {
                            if (result.Paid) { HouseholdFinance.ReleasePart(meal.Budget, held - paid); HouseholdFinance.ConfirmPurchase(meal.Budget); }
                            else HouseholdFinance.RefusePurchase(meal.Budget);
                        }
                        else meal.DayAllowanceUsed -= result.Paid ? held - paid : held;
                        if (!result.Paid) meal.Price = mainPrice;
                        meal.Status = result.Paid ? MealStatus.Paid : MealStatus.Missed; meal.Reason = result.Reason;
                        if (result.Paid) ConfirmPurchase(meal, result.Reason, sold);
                        else {
                            if (result.Failure == ShopExperience.Closed || result.Failure == ShopExperience.Stockout || result.Failure == ShopExperience.Queue)
                                Experience(meal, result.Failure.Value, now);
                            RecordOutcome(meal, now, true);
                        }
                        if (meal.HostSlot > 0 && meal.Excursion != null) ScheduleReturn(meal);
                    }
                    catch (Exception) { /* Retain uncertain ownership; never issue a second order. */ }
                }
            }
        }
        /// <summary>E5b5 Q2: the seller refused before any receipt because a menu price changed (the selected product's or
        /// another one's). The resident decides again at the counter from the shop's live menu: the selected product if it
        /// is still stocked and affordable at its live price, otherwise the stocked, affordable scoped product it likes best
        /// (taste band, then lower price, then ordinal name; no random draw). Main only. The refused attempt's hold is
        /// released first; when nothing fits, it is restored and the refusal stands. Called at most once per checkout, so
        /// a price that keeps moving ends as an ordinary refusal.</summary>
        private bool CounterRedecision(ResidentMeal meal, double now, ref Money mainPrice)
        {
            var business = ReadBusiness(meal.BusinessId, now);
            var scope = state.Shops.FirstOrDefault(s => s.PlaceId == meal.PlaceId && s.BusinessId == meal.BusinessId);
            var person = Owner(meal);
            if (business == null || business.Id != meal.BusinessId || business.PlaceId != meal.PlaceId || scope == null || person == null)
                return false;
            Money held = meal.Price; string chosen = meal.ProductId;
            if (meal.Budget != null) HouseholdFinance.RefusePurchase(meal.Budget); else meal.DayAllowanceUsed -= held;
            var limit = CurrentAllowance(meal);
            var policy = ProductPolicy();
            var candidates = new List<(FoodOffer Offer, int Taste)>(business.Offers.Count);
            foreach (var offer in business.Offers)
            {
                // This meal's own reservation was released when it became uncertain; others' units stay held.
                reservations.TryGetValue(ReservationKey(meal.PlaceId, offer.ProductId), out int reserved);
                if (!(offer.Units - reserved > 0) || offer.Price > limit || !scope.ContainsProduct(offer.ProductId)
                    || !policy.Executable(offer.ProductId)) continue;
                int taste = offer.ProductId == chosen ? int.MaxValue
                    : FoodProducts.Known(offer.ProductId) ? ExtraTaste(meal, person, offer.ProductId)
                    : policy.TasteBand(offer.ProductId, Traits(meal, person), city.State.Seed, meal.ResidentId);
                candidates.Add((offer, taste));
            }
            candidates.Sort((a, b) => a.Taste != b.Taste ? b.Taste.CompareTo(a.Taste)
                : a.Offer.Price != b.Offer.Price ? a.Offer.Price.CompareTo(b.Offer.Price)
                : StringComparer.Ordinal.Compare(a.Offer.ProductId, b.Offer.ProductId));
            foreach (var (offer, _) in candidates)
            {
                meal.Price = offer.Price; meal.ProductId = offer.ProductId;
                if (!CanSettle(meal) || meal.Budget != null && !HouseholdFinance.HoldPurchase(meal.Budget, meal.Price)) continue;
                if (meal.Budget == null) meal.DayAllowanceUsed += meal.Price;
                meal.Allowance = Money.Max(meal.Allowance, limit);
                mainPrice = meal.Price;
                // The unconfirmed outcome must name the product and terms now held, before native code runs again:
                // if the second checkout throws, it is what the save keeps (factors of the purchase are retained).
                RecordOutcome(meal, city.State.Minute, true);
                return true;
            }
            meal.Price = held; meal.ProductId = chosen;
            if (meal.Budget != null) { if (!HouseholdFinance.HoldPurchase(meal.Budget, held)) throw new InvalidOperationException("Restoring a released hold failed."); }
            else meal.DayAllowanceUsed += held;
            return false;
        }
        private void ConfirmPurchase(ResidentMeal meal, string reason, IReadOnlyList<FoodTicketLine>? extras = null)
        {
            Experience(meal, ShopExperience.Served, meal.ServiceAt);
            RecordPurchase(meal, extras); meal.Purchases++; meal.Spent += meal.Price;
            if (meal.ExistingStop) meal.ExistingStopPurchases++;
            meal.Status = MealStatus.Paid; meal.Reason = reason;
            RecordOutcome(meal, city.State.Minute, true);
        }
        // The only outcome writer. A lazy day rollover drops earlier records, so the derived rival load drops them too.
        private void RecordOutcome(ResidentMeal meal, double minute, bool checkout, FoodOutsideOption outside = FoodOutsideOption.None)
        {
            if (meal.Today != null && meal.Day > meal.Today.Day && MealOutcomes.StatusFor(meal.Status).HasValue)
                foreach (var record in meal.Today.Meals) rivalLoad.Forget(record);
            MealOutcomes.Record(meal, minute, checkout, outside);
        }
        private void Finish(ResidentMeal meal, MealStatus status, string reason, FoodOutsideOption outside = FoodOutsideOption.None)
        {
            if (status == MealStatus.Missed && Active(meal)) FailedVisit(meal, reason);
            string choiceReason=meal.Reason;
            if (Active(meal)) Reserve(meal, -1);
            if (meal.Status == MealStatus.Serving && meal.TargetKind == FoodAlternativeKind.Rival) rivalLoad.Leave(meal);
            meal.Status = status; meal.Reason = reason;
            RecordOutcome(meal, city.State.Minute, status == MealStatus.Uncertain, outside);
            if((status==MealStatus.Uncertain || status==MealStatus.Paid && meal.TargetKind==FoodAlternativeKind.Rival)
                && meal.Today?.Find(meal.Kind) is MealOutcomeRecord outcome)
            {
                if(choiceReason.Contains("likes it")) outcome.Factor1=MealFactor.Taste;
                if(choiceReason.Contains("values quality"))
                {if(outcome.Factor1==MealFactor.None) outcome.Factor1=MealFactor.Quality;else outcome.Factor2=MealFactor.Quality;}
            }
            var person = Owner(meal)!;
            NextMeal(meal, out int day, out MealKind kind);
            meal.DueAt = Math.Max(city.State.Minute + .001, MealRhythm.DesiredAt(person, city.State.Seed, day, kind));
            if (meal.HostSlot > 0 && meal.Excursion != null) ScheduleReturn(meal);
        }
        /// <summary>A dormant visitor's meal opportunities before its next stay end without an intent or outcome; the
        /// first meal still open at the stay start waits for it. Bounded: a stay is at most a few days ahead.</summary>
        private void SkipDormantMeals(ResidentMeal meal, CitizenState person, double now)
        {
            double stay = Math.Max(now, person.NextEventMinute);
            int day, guard = 0;
            MealKind kind;
            if (meal.Status != MealStatus.Pending)
            {
                NextMeal(meal, out day, out kind);
                if (day < (int)(now / 1440)) { day = (int)(now / 1440); kind = state.MealRhythmVersion == 0 ? MealKind.Legacy : MealKind.Breakfast; }
                StartMeal(meal, person, day, kind);
            }
            while (meal.ExpiresAt <= stay && guard++ < 64)
            {
                NextMeal(meal, out day, out kind);
                StartMeal(meal, person, day, kind);
            }
            meal.DueAt = Math.Max(now + .001, Math.Max(meal.DueAt, Math.Min(stay, meal.ExpiresAt)));
        }
        private void NextMeal(ResidentMeal meal, out int day, out MealKind kind)
        {
            bool nextDay = meal.Kind == MealKind.Legacy || meal.Kind == MealKind.Dinner;
            day = meal.Day + (nextDay ? 1 : 0);
            kind = state.MealRhythmVersion == 0 ? MealKind.Legacy : nextDay ? MealKind.Breakfast : meal.Kind + 1;
        }
        private FoodRoutineResident Resident(CitizenState p) => new FoodRoutineResident { Identity = p.Identity, HomeId = p.HomeId,
            HomeDistrictId = city.FindPlace(p.HomeId).District, WalkingToleranceMetres = p.Preferences.WalkingToleranceMetres };
        private FoodConsumer Consumer(CitizenState p, Money allowance, ResidentMeal? meal = null) => new FoodConsumer { Id = meal?.ResidentId ?? p.Identity.Id, Name = p.Identity.Name,
            PreferredProductId = meal?.PreferredProductId ?? "", PreferenceBonusCents = meal?.PreferenceBonusCents ?? 0,
            OriginPlaceId = p.PlaceId, DailyAllowance = allowance,
            ValueOfTimeCentsPerMinute = 4 + (int)(StableRandom.Hash((meal?.ResidentId ?? p.Identity.Id) + ":food-time", city.State.Seed) % 9),
            ServiceQualityValueCents = 100 + (int)(StableRandom.Hash((meal?.ResidentId ?? p.Identity.Id) + ":food-quality", city.State.Seed) % 301),
            DistrictAppealValueCents = 50 + (int)(StableRandom.Hash((meal?.ResidentId ?? p.Identity.Id) + ":food-district", city.State.Seed) % 151) };
        private void Decide(ResidentMeal meal, CitizenState person)
        {
            if (NewChoice(meal)) { BeginFoodDecision(meal,person,false); return; }
            long started = city.ObservingFood ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            var trace = city.ObservingFood ? new FoodDecisionTrace() : null;
            double now = city.State.Minute;
            CaptureRoutineStop(person);
            var stop = meal.HostSlot == 0 ? new FoodRoutineStop { AnchorId = person.Identity.Id, PlaceId = person.PlaceId, Activity = person.Activity,
                DepartureAt = Math.Min(person.NextEventMinute, person.ActivityUntilMinute) } : routineStops[person.Identity.Id];
            var consumer = Consumer(person, AvailableForMeal(meal), meal);
            consumer.OriginPlaceId = stop.PlaceId;
            MealRhythm.Apply(consumer, meal.Kind);
            var intent = new FoodIntent { Id = meal.IntentId, ConsumerId = meal.ResidentId, Day = meal.Day,
                DesiredAt = meal.DesiredAt, RequestedAt = now, Deadline = Math.Min(meal.ExpiresAt,
                    Math.Min(now + (meal.HostSlot == 0 ? MealRhythm.BreakMinutes(person, city.State.Seed, meal.Kind) : RepresentedBreak(person, stop, meal.Kind)),
                        stop.DepartureAt)),
                OriginPlaceId = stop.PlaceId, OriginActivity = stop.Activity, EatingMinutes = MealRhythm.EatingMinutes(meal.Kind), MaximumQueueMinutes = MealRhythm.QueueMinutes(meal.Kind, stop.Activity),
                TimeCostMultiplier = 1 + Math.Min(1, (now - meal.DesiredAt) / 120), ContextEvidence = meal.Kind + ": live scheduled presence and departure deadline; meal-specific time, quality and food suitability" };
            SetFoodAlternative(meal, person, consumer, intent);
            var businesses = ReadLocalBusinesses(city.FindPlace(stop.PlaceId), now, person.Preferences.WalkingToleranceMetres);
            var quotes = new List<FoodTravelQuote>();
            var refusals = new Dictionary<string, string>(StringComparer.Ordinal);
            var paths = new Dictionary<string, Route>(StringComparer.Ordinal);
            var returnGeometry = new Dictionary<string, string>(StringComparer.Ordinal);
            var preferences = new TravelPreferences { AllowTransit = false, OwnsCar = false, WalkingToleranceMetres = person.Preferences.WalkingToleranceMetres };
            foreach (var b in businesses)
            {
                var scope = state.Shops.Single(s => s.PlaceId == b.PlaceId);
                b.NextServiceMinute = Math.Max(now, scope.NextServiceAt);
                ApplyReservations(b);
                var rejection = FoodDemand.KnownUnavailable(b, consumer, now, intent.Deadline);
                if (rejection != null)
                { refusals[b.Id] = rejection; if (trace != null) trace.RouteChecks[b.Id] = rejection; continue; }
                var origin = city.FindPlace(stop.PlaceId); var shop = city.FindPlace(b.PlaceId);
                if (Position.Distance(origin.Position, shop.Position) > preferences.WalkingToleranceMetres)
                { refusals[b.Id] = "Beyond walking tolerance"; if (trace != null) trace.RouteChecks[b.Id] = refusals[b.Id]; continue; }
                // A safe geometric bound saves native path searches for distant shops. It is
                // not a route quote: feasible candidates still need complete outward/return paths.
                if (routes is IWalkingTimeBound bound && b.MealsPerHour > 0
                    && now + bound.MinimumWalkingMinutes(origin, shop) + bound.MinimumWalkingMinutes(shop, origin)
                        + 60.0 / b.MealsPerHour.Value + intent.EatingMinutes > intent.Deadline)
                { refusals[b.Id] = "Travel and service cannot fit break"; if (trace != null) trace.RouteChecks[b.Id] = refusals[b.Id]; continue; }
                var outward = WalkingQuote(origin, shop, now, preferences, trace);
                var back = outward == null ? null : WalkingQuote(shop, origin, now + outward.Minutes, preferences, trace);
                if (outward == null || back == null || outward.Legs.Any(l => l.Mode != TravelMode.Walk) || back.Legs.Any(l => l.Mode != TravelMode.Walk))
                { if (trace != null) trace.RouteChecks[b.Id] = outward == null ? "No outward walking path" : back == null ? "No return walking path" : "Non-walking path"; continue; }
                if (trace != null) trace.RouteChecks[b.Id] = "Complete walking paths";
                paths.Add(b.Id, outward);
                if (meal.HostSlot > 0) returnGeometry.Add(b.Id, GeometrySignature(back));
                quotes.Add(new FoodTravelQuote { ConsumerId = meal.ResidentId, BusinessId = b.Id, IntentId = meal.IntentId,
                    OriginPlaceId = stop.PlaceId, Minutes = outward.Minutes, ReturnMinutes = back.Minutes, Evidence = "Current complete walking paths" });
            }
            FoodDemand.ChooseObserved(consumer, businesses, quotes, intent, now, refusals, meal.Kind);
            RecordChoices(intent, businesses, meal.Kind);
            var choice = intent;
            if (trace != null)
            {
                trace.MealKind = meal.Kind; trace.Consumer = consumer; trace.Intent = choice; trace.Businesses = businesses; trace.Quotes = quotes;
                trace.ElapsedMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                city.ObserveFood(trace);
            }
            if (choice.Status != FoodIntentStatus.Travelling)
            { Finish(meal, choice.Status == FoodIntentStatus.DeclinedPurchase ? MealStatus.Declined : MealStatus.Missed, choice.Outcome, choice.OutsideOption); return; }
            var selected = businesses.Single(b => b.Id == choice.BusinessId);
            var selectedChoice = choice.Choices.Single(c => c.BusinessId == choice.BusinessId && c.ProductId == choice.ProductId);
            double stay = selectedChoice.ServiceEndsAt!.Value - (now + paths[selected.Id].Minutes) + intent.EatingMinutes;
            if (meal.HostSlot == 0 && !city.TryBeginMealVisit(person.Identity.Id, selected.PlaceId, paths[selected.Id], choice.ReturnTravelMinutes!.Value, stay, intent.Deadline))
            { Finish(meal, MealStatus.Missed, "Schedule or shop changed before departure"); return; }
            meal.PlaceId = selected.PlaceId; meal.BusinessId = selected.Id; meal.ProductId = choice.ProductId;
            meal.Price = selectedChoice.Price; meal.ServiceMinutes = 60.0 / selected.MealsPerHour!.Value;
            meal.ReturnMinutes = choice.ReturnTravelMinutes!.Value; meal.Deadline = intent.Deadline;
            meal.Status = MealStatus.Travelling;
            if (meal.HostSlot > 0)
            {
                meal.Excursion = new MealExcursion { OriginId = stop.PlaceId, DestinationId = selected.PlaceId, OriginActivity = stop.Activity,
                    OriginPosition = city.FindPlace(stop.PlaceId).Position, DestinationPosition = city.FindPlace(selected.PlaceId).Position,
                    DepartedAt = now, ArrivesAt = now + paths[selected.Id].Minutes, SharedDepartureAt = stop.DepartureAt,
                    AdmissionAt = selectedChoice.AdmissionMinute!.Value,
                    ReturnGeometry = returnGeometry[selected.Id], MaximumQueueMinutes = intent.MaximumQueueMinutes };
                meal.DueAt = meal.Excursion.AdmissionAt;
            }
            else meal.DueAt = person.NextEventMinute;
            Reserve(meal, 1);
        }
    }
}
