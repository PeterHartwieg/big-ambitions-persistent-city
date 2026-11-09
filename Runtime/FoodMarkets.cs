using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    /// <summary>One population market, with authority selected by the saved scope channel.</summary>
    public sealed class FoodMarkets : IFoodMarket, IFoodTicketMarket, IFoodTicketEvidence, ILocalFoodMarket, IFoodCandidateMarket, IFoodBusinessAuthority,
        IFoodSaleEvidence, IFoodFailureMarket, IFoodClosedStandMarket, IRivalFoodMarket, IFoodAwarenessRevision, IFoodProductPolicyMarket, IDisposable
    {
        private readonly FoodCommerceState state;
        private readonly IFoodMarket shops, vehicles;
        private readonly Func<string, Position> position;
        private readonly FoodProductPolicySource? policy;
        private bool disposed;
        public FoodMarkets(FoodCommerceState state, IFoodMarket shops, IFoodMarket vehicles, Func<string, Position> position,
            FoodProductPolicySource? policy = null)
        { this.state = state; this.shops = shops; this.vehicles = vehicles; this.position = position; this.policy = policy; }
        /// <summary>E5b4: one policy for shop and vehicle scopes alike, so both channels hold the same names and metadata.</summary>
        public IFoodProductPolicy ProductPolicy => policy?.Current ?? FoodProductPolicy.OriginalThree;
        private IFoodMarket Market(string id) => state.Shops.Any(s => s.BusinessId == id && s.Channel == FoodScopeChannel.Vehicle) ? vehicles : shops;
        public double EarliestMinute => Math.Max(shops.EarliestMinute, vehicles.EarliestMinute);
        public long AwarenessRevision => ((shops as IFoodAwarenessRevision)?.AwarenessRevision ?? 0) + ((vehicles as IFoodAwarenessRevision)?.AwarenessRevision ?? 0);
        public IReadOnlyList<FoodBusiness> Read(double minute) => shops.Read(minute).Concat(vehicles.Read(minute)).ToArray();
        public FoodSale Buy(ResidentMeal meal, double minute) => Market(meal.BusinessId).Buy(meal, minute);
        // F6j: only a seller with its own extras checkout (the shop market) receives a ticket; vehicles sell the main only.
        public bool SellsExtras(string id) => Market(id) is IFoodTicketMarket tickets && tickets.SellsExtras(id);
        public FoodSale BuyTicket(ResidentMeal meal, FoodMealTicket ticket, double minute) => ((IFoodTicketMarket)Market(meal.BusinessId)).BuyTicket(meal, ticket, minute);
        public FoodBusiness? ReadBusiness(string id, double minute) => ((ILocalFoodMarket)Market(id)).ReadBusiness(id, minute);
        public FoodBusiness? ReadFailureBusiness(string id, string place, double minute) => ((IFoodFailureMarket)Market(id)).ReadFailureBusiness(id, place, minute);
        public FoodBusiness? ReadClosedStand(string id, string place, double minute) => (Market(id) as IFoodClosedStandMarket)?.ReadClosedStand(id, place, minute);
        public bool CanServe(string id) => ((IFoodBusinessAuthority)Market(id)).CanServe(id);
        public bool IsConfirmedPaid(ResidentMeal meal) => ((IFoodSaleEvidence)Market(meal.BusinessId)).IsConfirmedPaid(meal);
        public bool TryConfirmedTicket(ResidentMeal meal, out Money total, out IReadOnlyList<FoodTicketLine> extras)
        {
            if (Market(meal.BusinessId) is IFoodTicketEvidence tickets) return tickets.TryConfirmedTicket(meal, out total, out extras);
            total = meal.Price; extras = Array.Empty<FoodTicketLine>();
            return IsConfirmedPaid(meal);
        }
        public bool IsCandidate(FoodScope scope) => ((IFoodCandidateMarket)(scope.Channel == FoodScopeChannel.Vehicle ? vehicles : shops)).IsCandidate(scope);
        public FoodBusiness? CaptureBusiness(string id, double minute) => ((IFoodCandidateMarket)Market(id)).CaptureBusiness(id, minute);
        public IReadOnlyList<FoodBusiness> ReadLocal(Place origin, double minute, double tolerance, int maximumCandidates)
        {
            if (maximumCandidates < 1 || maximumCandidates > 8 || !Position.Finite(tolerance) || tolerance < 0)
                throw new ArgumentException("Invalid local food observation bound.");
            // Preserve the native small-market menu/cache and shop-only local-read behavior.
            if (!state.Shops.Any(s => s.Channel == FoodScopeChannel.Vehicle))
                return ((ILocalFoodMarket)shops).ReadLocal(origin, minute, tolerance, maximumCandidates);
            if (state.Shops.Count <= maximumCandidates) return Read(minute);
            // Rank saved geometry before reading either seller's supply; apply the shared bound once.
            using (BeginCapture(minute))
                return state.Shops.Where(IsCandidate).Select(s => new { Scope = s, Distance = Position.Distance(origin.Position, position(s.PlaceId)) })
                    .Where(s => s.Distance <= tolerance).OrderBy(s => s.Distance).ThenBy(s => s.Scope.BusinessId, StringComparer.Ordinal)
                    .Take(maximumCandidates).Select(s => CaptureBusiness(s.Scope.BusinessId, minute)).Where(b => b != null).Cast<FoodBusiness>().ToArray();
        }
        public IDisposable BeginCapture(double minute)
        {
            var first = ((IFoodCandidateMarket)shops).BeginCapture(minute);
            try { return new Capture(first, ((IFoodCandidateMarket)vehicles).BeginCapture(minute)); }
            catch { first.Dispose(); throw; }
        }
        private sealed class Capture : IDisposable
        {
            private IDisposable? first, second;
            public Capture(IDisposable first, IDisposable second) { this.first = first; this.second = second; }
            public void Dispose()
            {
                var a = first; var b = second; first = second = null;
                try { a?.Dispose(); } finally { b?.Dispose(); }
            }
        }
        private IRivalFoodMarket Rivals => (IRivalFoodMarket)shops;
        public long RivalRevision => Rivals.RivalRevision;
        public void CloseRivals() => Rivals.CloseRivals();
        public bool NeedsRivalRefresh(double minute) => Rivals.NeedsRivalRefresh(minute);
        public void ContinueRivalRefresh(double minute) => Rivals.ContinueRivalRefresh(minute);
        public IEnumerable<FoodScope> RivalScopes => Rivals.RivalScopes;
        public bool IsRivalCandidate(FoodScope scope) => Rivals.IsRivalCandidate(scope);
        public bool RivalAtPlace(string place) => Rivals.RivalAtPlace(place);
        public FoodAlternativeObservation? ReadRival(string id) => Rivals.ReadRival(id);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { (shops as IDisposable)?.Dispose(); } finally { (vehicles as IDisposable)?.Dispose(); }
        }

        // Shared with NativeFoodOwnership so offline checks exercise both directions of the live guard.
        internal static void ValidateShops(FoodCommerceState state, IEnumerable<FoodAuthority> manifest,
            Func<FoodAuthority, FoodScope, bool> matches, Action<string> refuse)
        {
            var rows = manifest.ToDictionary(r => r.PlaceId, StringComparer.Ordinal);
            foreach (var scope in state.Shops.Where(s => s.Channel == FoodScopeChannel.Shop))
                if (!rows.TryGetValue(scope.PlaceId, out var row) || !matches(row, scope))
                    refuse("Saved food scope differs from its authority manifest");
            foreach (var row in rows.Values.Where(r => r.FromHour >= 0))
                if (!state.Shops.Any(s => s.Channel == FoodScopeChannel.Shop && s.PlaceId == row.PlaceId && matches(row, s)))
                    refuse("Saved food authority has no corresponding population scope");
        }
    }

    /// <summary>E5b4: the market's product policy, rebuilt from config, the API publication and the published loaded-name
    /// image only when one of them is a new object. A clean read returns the cached policy and allocates nothing. Read
    /// once per uncaptured decision context, on the main thread; held tuples never read it.</summary>
    public sealed class FoodProductPolicySource
    {
        private readonly Func<LoadedNameImage?> names;
        private readonly Func<FoodProductMetadataResolver> config;
        private readonly Func<FoodProductMetadataLoadedNames?, FoodProductMetadataSnapshot> api;
        private LoadedNameImage? image;
        private FoodProductMetadataLoadedNames? loaded;
        private FoodProductMetadataResolver? lastConfig;
        private FoodProductMetadataSnapshot? lastSnapshot;
        private FoodProductPolicy current = FoodProductPolicy.OriginalThree;

        public FoodProductPolicySource(Func<LoadedNameImage?> names, Func<FoodProductMetadataResolver> config,
            Func<FoodProductMetadataLoadedNames?, FoodProductMetadataSnapshot> api)
        {
            this.names = names ?? throw new ArgumentNullException(nameof(names));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }
        public long Publications { get; private set; }
        /// <summary>Empty while the last publication succeeded; otherwise why the policy fell back to the original three.</summary>
        public string Failure { get; private set; } = "";

        public FoodProductPolicy Current
        {
            get
            {
                try
                {
                    var next = names();
                    if (!ReferenceEquals(next, image))
                    {
                        image = next;
                        // A refused or missing image admits no dynamic name; a dirty catalogue publishes null.
                        loaded = next == null || next.Refusal != FoodEligibilityRefusal.None ? null : new FoodProductMetadataLoadedNames(next.Names);
                    }
                    var resolver = config(); var snapshot = api(loaded);
                    if (Failure.Length > 0 || !ReferenceEquals(resolver, lastConfig) || !ReferenceEquals(snapshot, lastSnapshot) || !ReferenceEquals(loaded, current.LoadedNames))
                    {
                        current = new FoodProductPolicy(resolver.WithApi(snapshot), loaded);
                        lastConfig = resolver; lastSnapshot = snapshot; Publications++; Failure = "";
                    }
                }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
                {
                    // Never a partial policy: new contexts hold the original three until a later publication succeeds.
                    Failure = e.GetType().Name + ": " + e.Message; current = FoodProductPolicy.OriginalThree;
                    image = null; loaded = null; lastConfig = null; lastSnapshot = null;
                }
                return current;
            }
        }
    }
}
