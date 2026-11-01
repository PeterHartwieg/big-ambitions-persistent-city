namespace PersistentCity.Runtime
{
    /// <summary>Detached raw field evidence, never sellable supply or a reservation.</summary>
    public sealed class SaladFeasibilitySnapshot : DirectStockFeasibilitySnapshot
    {
        internal SaladFeasibilitySnapshot(DirectStockFields fields) : base(fields) { }
        public override string ProductId => SaladFeasibilityReader.Product;
    }

    /// <summary>Explicit main-thread diagnostic only; no hooks, polling or gameplay caller.</summary>
    public static class SaladFeasibilityReader
    {
        public const string Product = "ba:itemname_salad";
        public static SaladFeasibilitySnapshot Read(BuildingRegistration registration)
            => new SaladFeasibilitySnapshot(DirectStockShowcaseReader.Read(registration, DirectStockCandidate.Salad));
    }
}
