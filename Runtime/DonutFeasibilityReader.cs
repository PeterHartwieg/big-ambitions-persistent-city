namespace PersistentCity.Runtime
{
    /// <summary>Detached raw field evidence, never sellable supply or a reservation.</summary>
    public sealed class DonutFeasibilitySnapshot : DirectStockFeasibilitySnapshot
    {
        internal DonutFeasibilitySnapshot(DirectStockFields fields) : base(fields) { }
        public override string ProductId => DonutFeasibilityReader.Product;
    }

    /// <summary>Explicit main-thread diagnostic only; no hooks, polling or gameplay caller.</summary>
    public static class DonutFeasibilityReader
    {
        public const string Product = "ba:itemname_donut";
        public static DonutFeasibilitySnapshot Read(BuildingRegistration registration)
            => new DonutFeasibilitySnapshot(DirectStockShowcaseReader.Read(registration, DirectStockCandidate.Donut));
    }
}
