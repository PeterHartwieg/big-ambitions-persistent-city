using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PersistentCity.Core
{
    // Engine and input adapters are deliberately internal. Only offline friends can run C4;
    // no Runtime caller exists until the payment/access integration is separately implemented.
    internal sealed class MarketResearchIdentity
    {
        internal MarketResearchIdentity(string city, int day, long catalogueRevision, long placeRevision, long populationRevision)
        {
            if (string.IsNullOrEmpty(city) || city.Length > 256 || day < 0
                || catalogueRevision < 0 || placeRevision < 0 || populationRevision < 0) throw new ArgumentException("Invalid snapshot identity.");
            City = city; Day = day; CatalogueRevision = catalogueRevision; PlaceRevision = placeRevision; PopulationRevision = populationRevision;
            Key = Convert.ToBase64String(Encoding.UTF8.GetBytes(city)) + ":" + day.ToString(CultureInfo.InvariantCulture)
                + ":" + catalogueRevision.ToString(CultureInfo.InvariantCulture) + ":" + placeRevision.ToString(CultureInfo.InvariantCulture)
                + ":" + populationRevision.ToString(CultureInfo.InvariantCulture);
        }
        public string City { get; }
        public int Day { get; }
        public long CatalogueRevision { get; }
        public long PlaceRevision { get; }
        public long PopulationRevision { get; }
        internal string Key { get; }
    }

    /// <summary>Borrow a frozen logical image, not a live CityState or native adapter. The owner holds
    /// its logical publication boundary, including budgets/traits/locations, for the query's lifetime.
    /// A change to ANY input publishes a new identity. Consumer IDs must be unique and ordinally sorted.
    /// No city clone, resident cache or per-shop scan is owned by this reducer.</summary>
    internal interface IMarketResearchSnapshot
    {
        MarketResearchIdentity Identity { get; }
        int Count { get; }
        MarketResearchConsumer Read(int index);
    }

    internal readonly struct MarketResearchConsumer
    {
        internal MarketResearchConsumer(string id, bool represented, Position? home, Position? dayPlace,
            Activity dayActivity, string? traits, byte? mealOpportunities, bool? householdFunded, int? budgetCents)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 256 || (home.HasValue && !home.Value.IsFinite)
                || (dayPlace.HasValue && !dayPlace.Value.IsFinite) || (mealOpportunities.HasValue && mealOpportunities > 7)
                || budgetCents < 0 || (traits != null && !ValidTraits(traits))
                || dayActivity < Activity.Home || dayActivity > Activity.Study) throw new ArgumentException("Invalid research consumer.");
            Id = id; Represented = represented; Home = home; DayPlace = dayPlace; DayActivity = dayActivity;
            Traits = traits; MealOpportunities = mealOpportunities; HouseholdFunded = householdFunded; BudgetCents = budgetCents;
        }
        internal string Id { get; }
        internal bool Represented { get; }
        internal Position? Home { get; }
        internal Position? DayPlace { get; }
        internal Activity DayActivity { get; }
        internal string? Traits { get; }
        internal byte? MealOpportunities { get; }
        internal bool? HouseholdFunded { get; }
        internal int? BudgetCents { get; }
        internal static bool ValidTraits(string bands)
        {
            if (bands.Length != ResidentTraits.Count) return false;
            foreach (char band in bands) if (band < '0' || band > '4') return false;
            return true;
        }
    }

    internal sealed class MarketResearchScope
    {
        internal MarketResearchScope() { Key = "city"; }
        internal MarketResearchScope(string address, Position centre, int radiusMetres)
        {
            if (string.IsNullOrEmpty(address) || address.Length > 256 || !centre.IsFinite
                || radiusMetres < 50 || radiusMetres > 2000) throw new ArgumentException("Invalid catchment.");
            Address = address; Centre = centre; RadiusMetres = radiusMetres;
            Key = Convert.ToBase64String(Encoding.UTF8.GetBytes(address)) + ":" + centre.X.ToString("R", CultureInfo.InvariantCulture)
                + ":" + centre.Y.ToString("R", CultureInfo.InvariantCulture) + ":" + centre.Z.ToString("R", CultureInfo.InvariantCulture)
                + ":" + radiusMetres.ToString(CultureInfo.InvariantCulture);
        }
        internal string? Address { get; }
        internal Position Centre { get; }
        internal int RadiusMetres { get; }
        internal string Key { get; }
        internal bool Contains(MarketResearchConsumer row) => Address == null
            || (row.Home.HasValue && Inside(row.Home.Value)) || (row.DayPlace.HasValue && Inside(row.DayPlace.Value));
        private bool Inside(Position p)
        {
            double x = p.X - Centre.X, y = p.Y - Centre.Y, z = p.Z - Centre.Z;
            return x*x + y*y + z*z <= (double)RadiusMetres * RadiusMetres;
        }
    }

    // Transient OFFLINE continuation only, never attached to CityState/SaveCodec. Public setters
    // permit test-tool JSON round trips; restore validates bounds before copying the fixed array.
    internal sealed class MarketResearchCursor
    {
        public int Version { get; set; } = 1;
        public string QueryKey { get; set; } = "";
        public int Count { get; set; }
        public int Next { get; set; }
        public string LastId { get; set; } = "";
        public int Missing { get; set; }
        public int[] Counters { get; set; } = new int[64];
    }

    internal sealed class MarketResearchEngine
    {
        internal const int MaximumConsumers = 150000, MaximumCells = 64;
        private readonly MarketResearchScope scope;
        private readonly MarketResearchTier tier;
        private MarketResearchCursor cursor = new MarketResearchCursor();
        private string identityKey = "";
        private string queryKey = "";
        private int logicalDay;
        private bool faulted;
        internal MarketResearchReport? Completed { get; private set; }

        internal MarketResearchEngine(IMarketResearchSnapshot source, MarketResearchScope scope, MarketResearchTier tier,
            MarketResearchCursor? resume = null)
        {
            if (tier < MarketResearchTier.Basic || tier > MarketResearchTier.InDepth) throw new ArgumentException("Unknown tier.");
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope)); this.tier = tier;
            Reset(source);
            if (resume == null || resume.QueryKey != queryKey) return; // changed snapshot/scope/tier restarts at zero
            if (resume.Version != 1 || resume.Count != cursor.Count || resume.Next < 0 || resume.Next > resume.Count
                || resume.LastId == null || resume.LastId.Length > 256 || (resume.Next == 0) != (resume.LastId.Length == 0)
                || resume.Missing < 0 || resume.Missing > 3 || resume.Counters == null || resume.Counters.Length != MaximumCells)
                throw new ArgumentException("Invalid research cursor.");
            if (resume.Next > 0 && source.Read(resume.Next - 1).Id != resume.LastId)
                throw new ArgumentException("Invalid research cursor.");
            foreach (int value in resume.Counters) if (value < 0 || value > resume.Next) throw new ArgumentException("Invalid counter.");
            var c = resume.Counters;
            if (c[1] > c[0] || c[2] > c[0] || c[3] > c[0] || c[4] > c[0]
                || c[5]+c[6]+c[7] != c[0] || c[8]+c[9]+c[10]+c[11] != c[0]
                || c[58]+c[59]+c[60] != c[0] || c[61] > c[0] || c[62] != 0 || c[63] != 0
                || ((resume.Missing & 2) != 0) != (c[61] > 0)) throw new ArgumentException("Inconsistent counters.");
            for (int trait = 0; trait < 9; trait++)
            {
                int sum = 0; for (int band = 0; band < 5; band++) sum += c[13+trait*5+band];
                if (sum != c[0]-c[61]) throw new ArgumentException("Inconsistent trait counters.");
            }
            cursor = Copy(resume);
        }

        private static string Key(string identity, int count, MarketResearchScope scope, MarketResearchTier tier)
        {
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes("pc:research:v1|" + identity + "|"
                + count.ToString(CultureInfo.InvariantCulture) + "|" + scope.Key + "|" + ((int)tier).ToString(CultureInfo.InvariantCulture))));
        }

        private void Reset(IMarketResearchSnapshot source)
        {
            Completed = null; faulted = true;
            if (source.Count < 0 || source.Count > MaximumConsumers) throw new ArgumentException("Research population exceeds cap.");
            identityKey = source.Identity.Key; logicalDay = source.Identity.Day; queryKey = Key(identityKey, source.Count, scope, tier);
            cursor = new MarketResearchCursor { QueryKey = queryKey, Count = source.Count };
            Completed = null; faulted = false;
        }

        /// <returns>Consumers examined this unit, including excluded catchment rows.</returns>
        internal int Continue(IMarketResearchSnapshot source, int consumersPerUnit = 256)
        {
            if (source.Identity.Key != identityKey || source.Count != cursor.Count) Reset(source);
            if (faulted) throw new InvalidOperationException("Invalid frozen input; use a new snapshot identity.");
            if (Completed != null) return 0;
            int limit = Math.Min(cursor.Count, cursor.Next + Math.Max(16, Math.Min(1024, consumersPerUnit)));
            int start = cursor.Next;
            try
            {
                for (; cursor.Next < limit; cursor.Next++)
                {
                    var row = source.Read(cursor.Next);
                    if (string.IsNullOrEmpty(row.Id) || string.CompareOrdinal(cursor.LastId, row.Id) >= 0)
                        throw new ArgumentException("Snapshot consumers must be unique and ordinally sorted.");
                    cursor.LastId = row.Id;
                    if (scope.Contains(row)) Count(row);
                    else if (!row.Home.HasValue || !row.DayPlace.HasValue) cursor.Counters[12]++;
                }
                if (source.Identity.Key != identityKey || source.Count != cursor.Count)
                { Reset(source); return limit - start; } // never publish a mixed revision
                if (cursor.Next == cursor.Count) Completed = Project();
                return cursor.Next - start;
            }
            catch { Completed = null; faulted = true; throw; }
        }

        private void Count(MarketResearchConsumer row)
        {
            var c = cursor.Counters; c[0]++; if (row.Represented) c[1]++;
            if (!row.MealOpportunities.HasValue) cursor.Missing |= 1;
            else for (int i = 0; i < 3; i++) if ((row.MealOpportunities.Value & (1 << i)) != 0) c[2+i]++;
            c[!row.HouseholdFunded.HasValue ? 7 : row.HouseholdFunded.Value ? 5 : 6]++;
            c[!row.BudgetCents.HasValue ? 11 : row.BudgetCents < 1000 ? 8 : row.BudgetCents < 2500 ? 9 : 10]++;
            if (!row.Home.HasValue || !row.DayPlace.HasValue) c[12]++;
            if (row.Traits == null) { c[61]++; cursor.Missing |= 2; }
            else for (int i = 0; i < 9; i++)
            {
                int trait = i < 5 ? i : i+1; // loyalty experience needs B1, not the unused A2 predisposition
                c[13 + i*5 + row.Traits[trait] - '0']++;
            }
            c[row.DayActivity == Activity.Work ? 58 : row.DayActivity == Activity.Study ? 59 : 60]++;
        }

        private MarketResearchReport Project()
        {
            int length = tier == MarketResearchTier.Basic ? 13 : tier == MarketResearchTier.Detailed ? 62 : 64;
            var cells = new MarketResearchCell[length]; var c = cursor.Counters;
            void Add(int i, MarketResearchMetric metric, MarketResearchBand band = MarketResearchBand.All, bool known = true)
                => cells[i] = new MarketResearchCell(metric, band, known ? c[i] : (int?)null);
            for (int i = 0; i < 5; i++) Add(i, (MarketResearchMetric)i, known: i < 2 || (cursor.Missing & 1) == 0);
            Add(5, MarketResearchMetric.HouseholdFood, MarketResearchBand.Funded);
            Add(6, MarketResearchMetric.HouseholdFood, MarketResearchBand.Unfunded);
            Add(7, MarketResearchMetric.HouseholdFood, MarketResearchBand.Unknown);
            Add(8, MarketResearchMetric.DiscretionaryBudget, MarketResearchBand.Low);
            Add(9, MarketResearchMetric.DiscretionaryBudget, MarketResearchBand.Neutral);
            Add(10, MarketResearchMetric.DiscretionaryBudget, MarketResearchBand.High);
            Add(11, MarketResearchMetric.DiscretionaryBudget, MarketResearchBand.Unknown);
            Add(12, MarketResearchMetric.UsualLocationUnknown);
            if (length > 13)
            {
                for (int i = 0; i < 9; i++) for (int band = 0; band < 5; band++)
                    Add(13 + i*5 + band, (MarketResearchMetric)((int)MarketResearchMetric.CroissantTaste + i),
                        (MarketResearchBand)((int)MarketResearchBand.Low + band), (cursor.Missing & 2) == 0);
                Add(58, MarketResearchMetric.UsualDayGroup, MarketResearchBand.Work);
                Add(59, MarketResearchMetric.UsualDayGroup, MarketResearchBand.Study);
                Add(60, MarketResearchMetric.UsualDayGroup, MarketResearchBand.Other);
                Add(61, MarketResearchMetric.TraitsUnknown, MarketResearchBand.Unknown);
            }
            if (length == 64)
            {
                Add(62, MarketResearchMetric.LoyaltyDistribution, MarketResearchBand.Unknown, false);
                Add(63, MarketResearchMetric.RivalMarketShare, MarketResearchBand.Unknown, false);
            }
            return new MarketResearchReport(queryKey, tier, logicalDay, scope.Address, scope.RadiusMetres, cells);
        }

        internal MarketResearchCursor Checkpoint() => Copy(cursor);
        private static MarketResearchCursor Copy(MarketResearchCursor c) => new MarketResearchCursor
        { QueryKey = c.QueryKey, Count = c.Count, Next = c.Next, LastId = c.LastId, Missing = c.Missing, Counters = (int[])c.Counters.Clone() };
    }
}
