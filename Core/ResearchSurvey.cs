using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace PersistentCity.Core
{
    /// <summary>Why the survey runner refused an order before the ledger saw it. Ledger means the ledger
    /// itself refused; its own reason is returned alongside.</summary>
    public enum ResearchSurveyRefusal { None, NoFoodRoster, RosterTooLarge, UnknownBusinessPlace, ClockMismatch, Ledger }

    /// <summary>Runs each Ordered research order's survey inside the F1 hold (C5 plan 3.4). An order records
    /// SurveyAtMinute; the survey starts only once the simulator has drained through that minute (no route
    /// job, no campaign publication, no due citizen, food or traffic event at or before it) and runs before
    /// any later event. Until it finishes, NoEventsThrough reports pending work, so native time is held, but
    /// it is never unfinished previous-epoch work. The work is transient: a save writes the order as Ordered,
    /// reload replays the events through SurveyAtMinute and the survey restarts from zero. No cursor is saved.
    /// Units are bounded: first the ordinal index (at most OrdinalRosterIndex.MovesPerUnit element moves),
    /// then the C4 engine (at most ConsumersPerUnit consumers). Reads the city, never writes it.</summary>
    public sealed class ResearchSurveyRunner
    {
        private readonly CitySimulator city;
        private long seenSequence = -1;
        private ResearchOrder? current;
        private readonly List<string> unsurveyable = new List<string>();
        // The running survey of current. Null between surveys and after a restart.
        private OrdinalRosterIndex? index;
        private CityResearchSnapshot? snapshot;
        private MarketResearchEngine? engine;
        private List<ResidentMeal>? roster;
        private int rosterCount;
        private double rosterMinute;
        // Reused between surveys while large enough: the index needs two int arrays and one key array.
        private int[]? bufferA, bufferB;
        private string[]? bufferKeys;

        internal ResearchSurveyRunner(CitySimulator city, ResearchLedger ledger, int consumersPerUnit, Func<string, string?>? businessPlace)
        {
            this.city = city;
            Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            ConsumersPerUnit = Math.Max(16, Math.Min(1024, consumersPerUnit));
            BusinessPlace = businessPlace;
        }

        public ResearchLedger Ledger { get; }
        /// <summary>The C4 unit size, clamped to 16-1,024 as MarketResearchSettings does.</summary>
        public int ConsumersPerUnit { get; }
        /// <summary>Maps a business address to its Place ID. Null means the address is the Place ID.</summary>
        public Func<string, string?>? BusinessPlace { get; }
        /// <summary>The order whose survey runs now or next, or null.</summary>
        public string? SurveyingOrderId { get { Refresh(); return current?.Id; } }
        /// <summary>Ordered orders this session cannot survey (no roster, roster over the C4 cap, or an
        /// unknown business place). They hold nothing and stay Ordered; a reload checks them again.</summary>
        public IReadOnlyList<string> UnsurveyableOrders => unsurveyable;
        public long IndexUnits { get; private set; }
        public long SurveyUnits { get; private set; }
        /// <summary>Surveys started again because the roster or the clock changed between units.</summary>
        public long Restarts { get; private set; }
        public long Completed { get; private set; }

        /// <summary>Checks that this city can survey the order, then stores it in the ledger. orderMinute is the
        /// native minute; the ledger derives SurveyAtMinute from it. The caller has checked that the business
        /// is an eligible Persistent City food business.</summary>
        public ResearchSurveyRefusal TryOrder(string? productCode, string? businessAddress, string? agencyAddress, bool taxDeductible,
            int radiusMetres, double orderMinute, out ResearchOrder? order, out ResearchOrderRefusal ledgerRefusal)
        {
            order = null; ledgerRefusal = ResearchOrderRefusal.None;
            var food = city.State.Food;
            if (food == null || food.Residents.Count == 0) return ResearchSurveyRefusal.NoFoodRoster;
            if (food.Residents.Count > MarketResearchEngine.MaximumConsumers) return ResearchSurveyRefusal.RosterTooLarge;
            if (ResearchProducts.TryGet(productCode, out var product) && product!.Coverage == ResearchCoverage.Catchment
                && !string.IsNullOrWhiteSpace(businessAddress) && Centre(businessAddress!) == null)
                return ResearchSurveyRefusal.UnknownBusinessPlace;
            // The ledger refuses an invalid minute itself; only a valid one can be compared with the city clock.
            if (Position.Finite(orderMinute) && orderMinute >= 0 && orderMinute <= 1e9
                && !AccountingClock.Compatible(city.State.Minute, orderMinute)) return ResearchSurveyRefusal.ClockMismatch;
            ledgerRefusal = Ledger.TryOrder(productCode, businessAddress, agencyAddress, taxDeductible, radiusMetres, orderMinute, out order);
            return ledgerRefusal == ResearchOrderRefusal.None ? ResearchSurveyRefusal.None : ResearchSurveyRefusal.Ledger;
        }

        /// <summary>True while a survey due at or before targetMinute has not finished. With no open order this
        /// is one sequence comparison and one null check.</summary>
        internal bool HoldsThrough(double targetMinute)
        {
            Refresh();
            return current != null && current.SurveyAtMinute <= targetMinute;
        }

        /// <summary>A survey unit may run now: the next event is later than SurveyAtMinute.</summary>
        internal bool ReadyBefore(double nextEventMinute, double targetMinute)
            => HoldsThrough(targetMinute) && nextEventMinute > current!.SurveyAtMinute;

        /// <summary>One bounded unit: an index unit, or a C4 engine unit. Seals the report on completion.</summary>
        internal void Continue()
        {
            Refresh();
            var order = current;
            if (order == null) return;
            if (index != null && !SameRoster()) { Restarts++; Clear(); }
            if (index == null && !Start())
            {
                // The roster was removed or grew past the cap after the order was picked. Treat the order as
                // unsurveyable (technical decision 2): release the hold instead of failing the frame.
                unsurveyable.Add(order.Id); Clear();
                current = null; seenSequence = -1;
                return;
            }
            if (!index!.Done) { index.Continue(); IndexUnits++; return; }
            if (engine == null)
            {
                snapshot = new CityResearchSnapshot(city, order, roster!, index.Order, rosterCount);
                engine = new MarketResearchEngine(snapshot, Scope(order), order.Product.Tier);
            }
            engine.Continue(snapshot!, ConsumersPerUnit); SurveyUnits++;
            // Never seal a report read across a roster or clock change.
            if (!SameRoster()) { Restarts++; Clear(); return; }
            var report = engine.Completed;
            if (report == null) return;
            var refusal = Ledger.TrySeal(order.Id, report);
            if (refusal != ResearchMoveRefusal.None) throw new InvalidOperationException("The ledger refused a completed survey: " + refusal);
            Completed++;
            Clear();
            current = null; seenSequence = -1; // pick the next Ordered order
        }

        // The ledger is changed only on the game thread by TryOrder (here or direct) and by TrySeal (here).
        // A new order always advances NextSequence, so the scan runs only when an order arrived.
        private void Refresh()
        {
            if (seenSequence == Ledger.NextSequence && (current == null || current.State == ResearchOrderState.Ordered)) return;
            seenSequence = Ledger.NextSequence;
            if (current != null && current.State != ResearchOrderState.Ordered) { current = null; Clear(); }
            ResearchOrder? next = null;
            foreach (var order in Ledger.Open)
            {
                if (order.State != ResearchOrderState.Ordered || unsurveyable.Contains(order.Id)) continue;
                if (!Surveyable(order)) { unsurveyable.Add(order.Id); continue; }
                // Earliest SurveyAtMinute first; Open is in sequence order, so ties keep the order sequence.
                if (next == null || order.SurveyAtMinute < next.SurveyAtMinute) next = order;
            }
            if (!ReferenceEquals(next, current)) { Clear(); current = next; }
        }

        private bool Surveyable(ResearchOrder order)
        {
            var food = city.State.Food;
            if (food == null || food.Residents.Count == 0 || food.Residents.Count > MarketResearchEngine.MaximumConsumers) return false;
            return order.Product.Coverage == ResearchCoverage.City || Centre(order.BusinessAddress) != null;
        }

        private Position? Centre(string businessAddress)
        {
            string? place = BusinessPlace == null ? businessAddress : BusinessPlace(businessAddress);
            return string.IsNullOrEmpty(place) ? null : city.FindPosition(place!);
        }

        private MarketResearchScope Scope(ResearchOrder order) => order.Product.Coverage == ResearchCoverage.City
            ? new MarketResearchScope() : new MarketResearchScope(order.BusinessAddress, Centre(order.BusinessAddress)!.Value, order.RadiusMetres);

        // False when the roster is gone, empty or over the C4 cap; Surveyable was checked only when the order was picked.
        private bool Start()
        {
            var food = city.State.Food;
            if (food == null || food.Residents.Count == 0 || food.Residents.Count > MarketResearchEngine.MaximumConsumers) return false;
            roster = food.Residents; rosterCount = roster.Count; rosterMinute = city.State.Minute;
            if (bufferA == null || bufferA.Length < rosterCount)
            { bufferA = new int[rosterCount]; bufferB = new int[rosterCount]; bufferKeys = new string[rosterCount]; }
            index = new OrdinalRosterIndex(roster, rosterCount, bufferA, bufferB!, bufferKeys!);
            return true;
        }

        // The minute and the roster count are checked before and after each unit (plan 3.4). The list identity
        // also covers a replaced roster of the same size.
        private bool SameRoster() => ReferenceEquals(city.State.Food?.Residents, roster)
            && roster!.Count == rosterCount && city.State.Minute == rosterMinute;

        private void Clear()
        {
            index = null; snapshot = null; engine = null; roster = null;
            if (bufferKeys != null) Array.Clear(bufferKeys, 0, bufferKeys.Length); // drop string references
        }
    }

    /// <summary>Ordinal order of the food roster by ResidentId, built by a bottom-up merge sort over int
    /// arrays in bounded units. Each unit moves at most MovesPerUnit elements (the key copy counts as moves),
    /// so a 100,000-consumer roster never sorts in one nonpreemptible call.</summary>
    internal sealed class OrdinalRosterIndex
    {
        internal const int MovesPerUnit = 4096;
        private readonly IReadOnlyList<ResidentMeal> roster;
        private readonly string[] keys;
        private int[] source, target;
        private readonly int count;
        private int filled, width = 1, next, mid, end, left, right;

        internal OrdinalRosterIndex(IReadOnlyList<ResidentMeal> roster, int count, int[] a, int[] b, string[] keys)
        {
            this.roster = roster; this.count = count; source = a; target = b; this.keys = keys;
            Done = count == 0;
        }

        internal bool Done { get; private set; }
        /// <summary>Roster positions in ordinal ResidentId order. Valid once Done; length may exceed Count.</summary>
        internal int[] Order => Done ? source : throw new InvalidOperationException("The index is not built.");
        internal int Count => count;

        /// <returns>Element moves made, at most moves.</returns>
        internal int Continue(int moves = MovesPerUnit)
        {
            int made = 0;
            for (; filled < count && made < moves; filled++, made++) { keys[filled] = roster[filled].ResidentId; source[filled] = filled; }
            if (filled < count) return made;
            if (count <= 1) { Done = true; return made; }
            while (made < moves && !Done)
            {
                if (next == count)
                {
                    var swap = source; source = target; target = swap;
                    width <<= 1; next = 0; end = 0;
                    if (width >= count) { Done = true; break; }
                    continue;
                }
                if (next == end)
                {
                    left = next; mid = Math.Min(count, next + width); end = Math.Min(count, mid + width); right = mid;
                }
                // Once one run is exhausted the rest is a straight copy.
                if (left == mid || right == end)
                {
                    int from = left == mid ? right : left, n = Math.Min(end - next, moves - made);
                    Array.Copy(source, from, target, next, n);
                    if (left == mid) right += n; else left += n;
                    next += n; made += n;
                    continue;
                }
                target[next++] = string.CompareOrdinal(keys[source[left]], keys[source[right]]) <= 0 ? source[left++] : source[right++];
                made++;
            }
            return made;
        }
    }

    /// <summary>The C4 frozen source over the live food roster during the hold. The hold keeps the minute, the
    /// roster and every field read here fixed; the runner checks the minute and roster before and after each
    /// unit and restarts if they changed. A missing input stays null, as C4 requires.</summary>
    internal sealed class CityResearchSnapshot : IMarketResearchSnapshot
    {
        private readonly CitySimulator city;
        private readonly IReadOnlyList<ResidentMeal> roster;
        private readonly int[] order;
        private readonly int day;
        private readonly bool mealsKnown, financesKnown;

        internal CityResearchSnapshot(CitySimulator city, ResearchOrder researchOrder, IReadOnlyList<ResidentMeal> roster, int[] order, int count)
        {
            this.city = city; this.roster = roster; this.order = order; Count = count;
            double at = researchOrder.SurveyAtMinute;
            day = (int)Math.Floor(at / 1440);
            var food = city.State.Food!;
            mealsKnown = food.MealRhythmVersion != 0;
            financesKnown = food.HouseholdFinanceVersion != 0;
            // SurveyAtMinute stands in for the catalogue, place and population revisions: the hold keeps all
            // three fixed. The order ID keeps two orders of the same minute apart.
            long revision = BitConverter.DoubleToInt64Bits(at);
            Identity = new MarketResearchIdentity(CityKey(researchOrder.Id, city.State.PopulationKey), day, revision, revision, revision);
        }

        public MarketResearchIdentity Identity { get; }
        public int Count { get; }

        public MarketResearchConsumer Read(int index)
        {
            var meal = roster[order[index]];
            var person = city.FindCitizen(meal.AnchorId);
            Position? home = null, dayPlace = null;
            var activity = Activity.Home;
            string? traits = null;
            if (person != null)
            {
                home = city.FindPosition(person.HomeId);
                activity = person.Identity.Persona == Persona.Retiree || person.DayPlaceId.Length == 0 ? Activity.Home
                    : person.Identity.Persona == Persona.Student ? Activity.Study : Activity.Work;
                // Without a work or study place the usual day place is home.
                dayPlace = activity == Activity.Home ? home : city.FindPosition(person.DayPlaceId);
                var bands = meal.Slot > 0 ? meal.RepresentedTraits : person.Traits;
                if (bands != null && bands.IsValid) traits = bands.Bands;
            }
            return new MarketResearchConsumer(meal.ResidentId, meal.Slot > 0, home, dayPlace, activity, traits,
                Meals(meal), Funded(meal), Budget(meal));
        }

        // Meal opportunities still open in the logical day: a meal kind counts unless its meal is already
        // resolved or the record has moved past it. Legacy single meals have no kind and stay unknown.
        private byte? Meals(ResidentMeal meal)
        {
            if (!mealsKnown || meal.Kind == MealKind.Legacy) return null;
            if (meal.Day > day) return 0;
            if (meal.Day < day) return 7;
            int bits = 0;
            for (var kind = MealKind.Breakfast; kind <= MealKind.Dinner; kind++)
                if (kind > meal.Kind || kind == meal.Kind && Unresolved(meal.Status)) bits |= 1 << ((int)kind - 1);
            return (byte)bits;
        }
        private static bool Unresolved(MealStatus status)
            => status == MealStatus.Pending || status == MealStatus.Travelling || status == MealStatus.Serving;

        // Individual basket funding for the logical day; unknown until that day's budget posting.
        private bool? Funded(ResidentMeal meal)
        {
            var budget = meal.Budget;
            if (!financesKnown || budget == null || budget.LastPostedDay != day) return null;
            return budget.HomeFoodFundedDay == day;
        }

        private int? Budget(ResidentMeal meal)
        {
            var budget = meal.Budget;
            if (!financesKnown || budget == null) return null;
            return (int)(HouseholdFinance.SpendingLimit(budget).Units / Money.UnitsPerCent);
        }

        private static string CityKey(string orderId, string populationKey)
        {
            string key = orderId + "|" + populationKey;
            if (key.Length <= 256) return key;
            using var hash = SHA256.Create();
            return orderId + "|sha256:" + Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(populationKey)));
        }
    }
}
