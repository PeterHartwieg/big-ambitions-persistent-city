namespace PersistentCity.Runtime
{
    /// <summary>Cumulative main-thread diagnostics for one route planner. Compare snapshot deltas;
    /// counters reset when the planner is rebuilt. Timings are measured work, not performance claims.</summary>
    internal sealed class NativeRouteMetrics
    {
        public long WalkingRequests { get; internal set; }
        public long WalkingInvalidInputs { get; internal set; }
        public long WalkingDistanceRejects { get; internal set; }
        public long WalkingCacheHits { get; internal set; }
        public long WalkingNegativeCacheHits { get; internal set; }
        public long WalkingCacheMisses { get; internal set; }
        public long WalkingCacheEvictions { get; internal set; }
        public long WalkingCacheClears { get; internal set; }
        public long WalkingCalculations { get; internal set; }
        public long WalkingFailedCalculations { get; internal set; }
        public double WalkingCalculationMilliseconds { get; internal set; }
        public double MaximumWalkingCalculationMilliseconds { get; internal set; }
        public long NavMeshSampleQueries { get; internal set; }
        public long NavMeshPathQueries { get; internal set; }
        public double NavMeshPathMilliseconds { get; internal set; }
        public double MaximumNavMeshPathMilliseconds { get; internal set; }
        public long WalkingQuoteChecks { get; internal set; }
        public long WalkingQuoteRejected { get; internal set; }
        public long NavMeshRaycastQueries { get; internal set; }
        public double WalkingQuoteCheckMilliseconds { get; internal set; }
        public double MaximumWalkingQuoteCheckMilliseconds { get; internal set; }
        public long CitizenPlanRequests { get; internal set; }
        public long SelectedWalking { get; internal set; }
        public long SelectedSubway { get; internal set; }
        public long SelectedCar { get; internal set; }
        public long NoFeasibleRoute { get; internal set; }
        public long SubwayConnectorRequests { get; internal set; }
        public long SubwayConnectorCacheHits { get; internal set; }
        public long SubwayConnectorCacheMisses { get; internal set; }
        public long SubwayCandidates { get; internal set; }
    }
}
