namespace PersistentCity.Runtime
{
    /// <summary>Detached raw field evidence, never sellable supply or a reservation.</summary>
    public sealed class CupcakeFeasibilitySnapshot : DirectStockFeasibilitySnapshot
    {
        internal CupcakeFeasibilitySnapshot(DirectStockFields fields) : base(fields) { }
        public override string ProductId => CupcakeFeasibilityReader.Product;
    }

    /// <summary>Explicit main-thread diagnostic only; no hooks, polling or gameplay caller.</summary>
    public static class CupcakeFeasibilityReader
    {
        public const string Product = "ba:itemname_cupcake";
        public static CupcakeFeasibilitySnapshot Read(BuildingRegistration registration)
            => new CupcakeFeasibilitySnapshot(DirectStockShowcaseReader.Read(registration, DirectStockCandidate.Cupcake));
    }
}
