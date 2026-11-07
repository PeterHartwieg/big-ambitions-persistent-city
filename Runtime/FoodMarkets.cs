using System;
using System.Collections.Generic;
using System.Linq;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    /// <summary>One population market, with authority selected by the saved scope channel.</summary>
    public sealed class FoodMarkets : IFoodMarket, ILocalFoodMarket, IFoodCandidateMarket, IFoodBusinessAuthority,
        IFoodSaleEvidence, IFoodFailureMarket, IRivalFoodMarket, IFoodAwarenessRevision, IDisposable
    {
        private readonly FoodCommerceState state;
        private readonly IFoodMarket shops, vehicles;
        private readonly Func<string, Position> position;
        private bool disposed;
        public FoodMarkets(FoodCommerceState state, IFoodMarket shops, IFoodMarket vehicles, Func<string, Position> position)
        { this.state = state; this.shops = shops; this.vehicles = vehicles; this.position = position; }
        private IFoodMarket Market(string id) => state.Shops.Any(s => s.BusinessId == id && s.Channel == FoodScopeChannel.Vehicle) ? vehicles : shops;
        public double EarliestMinute => Math.Max(shops.EarliestMinute, vehicles.EarliestMinute);
        public long AwarenessRevision => ((shops as IFoodAwarenessRevision)?.AwarenessRevision ?? 0) + ((vehicles as IFoodAwarenessRevision)?.AwarenessRevision ?? 0);
        public IReadOnlyList<FoodBusiness> Read(double minute) => shops.Read(minute).Concat(vehicles.Read(minute)).ToArray();
        public FoodSale Buy(ResidentMeal meal, double minute) => Market(meal.BusinessId).Buy(meal, minute);
        public FoodBusiness? ReadBusiness(string id, double minute) => ((ILocalFoodMarket)Market(id)).ReadBusiness(id, minute);
        public FoodBusiness? ReadFailureBusiness(string id, string place, double minute) => ((IFoodFailureMarket)Market(id)).ReadFailureBusiness(id, place, minute);
        public bool CanServe(string id) => ((IFoodBusinessAuthority)Market(id)).CanServe(id);
        public bool IsConfirmedPaid(ResidentMeal meal) => ((IFoodSaleEvidence)Market(meal.BusinessId)).IsConfirmedPaid(meal);
        public bool IsCandidate(FoodScope scope) => ((IFoodCandidateMarket)(scope.Channel == FoodScopeChannel.Vehicle ? vehicles : shops)).IsCandidate(scope);
        public FoodBusiness? CaptureBusiness(string id, double minute) => ((IFoodCandidateMarket)Market(id)).CaptureBusiness(id, minute);
        public IReadOnlyList<FoodBusiness> ReadLocal(Place origin, double minute, double tolerance, int maximumCandidates)
        {
            if (maximumCandidates < 1 || maximumCandidates > 8 || !Position.Finite(tolerance) || tolerance < 0)
                throw new ArgumentException("Invalid local food observation bound.");
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
}
