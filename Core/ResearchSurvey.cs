using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace PersistentCity.Core
{
    public enum ResearchSurveyRefusal { None, NoFoodRoster, RosterTooLarge, UnknownBusinessPlace, ClockMismatch, Ledger }

    public sealed class ResearchSurveyRunner
    {
        private readonly CitySimulator city;
        private long seenSequence = -1;
        private ResearchOrder? current;
        private readonly List<string> unsurveyable = new List<string>();
        private OrdinalRosterIndex? index;
        private CityResearchSnapshot? snapshot;
        private MarketResearchEngine? engine;
        private List<ResidentMeal>? roster;
        private int rosterCount;
        private double rosterMinute;
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
        public int ConsumersPerUnit { get; }
        public Func<string, string?>? BusinessPlace { get; }
        public string? SurveyingOrderId { get { Refresh(); return current?.Id; } }
        public IReadOnlyList<string> UnsurveyableOrders => unsurveyable;
        public long IndexUnits { get; private set; }
        public long SurveyUnits { get; private set; }
        public long Restarts { get; private set; }
        public long Completed { get; private set; }

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
            if (Position.Finite(orderMinute) && orderMinute >= 0 && orderMinute <= 1e9
                && !AccountingClock.Compatible(city.State.Minute, orderMinute)) return ResearchSurveyRefusal.ClockMismatch;
            ledgerRefusal = Ledger.TryOrder(productCode, businessAddress, agencyAddress, taxDeductible, radiusMetres, orderMinute, out order);
            return ledgerRefusal == ResearchOrderRefusal.None ? ResearchSurveyRefusal.None : ResearchSurveyRefusal.Ledger;
        }

        internal bool HoldsThrough(double targetMinute)
        {
            Refresh();
            return current != null && current.SurveyAtMinute <= targetMinute;
        }

        internal bool ReadyBefore(double nextEventMinute, double targetMinute)
            => HoldsThrough(targetMinute) && nextEventMinute > current!.SurveyAtMinute;

        internal void Continue()
        {
            Refresh();
            var order = current;
            if (order == null) return;
            if (index != null && !SameRoster()) { Restarts++; Clear(); }
            if (index == null && !Start())
            {
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
            if (!SameRoster()) { Restarts++; Clear(); return; }
            var report = engine.Completed;
            if (report == null) return;
            var refusal = Ledger.TrySeal(order.Id, report);
            if (refusal != ResearchMoveRefusal.None) throw new InvalidOperationException("The ledger refused a completed survey: " + refusal);
            Completed++;
            Clear();
            current = null; seenSequence = -1;
        }

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

        private bool SameRoster() => ReferenceEquals(city.State.Food?.Residents, roster)
            && roster!.Count == rosterCount && city.State.Minute == rosterMinute;

        private void Clear()
        {
            index = null; snapshot = null; engine = null; roster = null;
            if (bufferKeys != null) Array.Clear(bufferKeys, 0, bufferKeys.Length);
        }
    }

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
        internal int[] Order => Done ? source : throw new InvalidOperationException("The index is not built.");
        internal int Count => count;

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
            long revision = BitConverter.DoubleToInt64Bits(at);
            Identity = new MarketResearchIdentity(CityKey(researchOrder.Id, city.State.PopulationKey), day, revision, revision, revision);
        }

        public MarketResearchIdentity Identity { get; }
        public int Count { get; }

        public MarketResearchConsumer Read(int index)
        {
            var meal = roster[order[index]];
            var person = city.FindCitizen(meal.HostId);
            bool represented = meal.HostSlot > 0;
            Position? home = null, dayPlace = null;
            var activity = Activity.Home;
            string? traits = null;
            if (person != null)
            {
                home = city.FindPosition(person.HomeId);
                activity = person.Identity.Persona == Persona.Retiree || person.DayPlaceId.Length == 0 ? Activity.Home
                    : person.Identity.Persona == Persona.Student ? Activity.Study : Activity.Work;
                dayPlace = activity == Activity.Home ? home : city.FindPosition(person.DayPlaceId);
                var bands = represented || person.IsVisitor ? meal.RepresentedTraits : person.Traits;
                if (bands != null && bands.IsValid) traits = bands.Bands;
            }
            return new MarketResearchConsumer(meal.ResidentId, represented, home, dayPlace, activity, traits,
                Meals(meal), Funded(meal), Budget(meal));
        }

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
