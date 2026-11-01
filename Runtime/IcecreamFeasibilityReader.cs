namespace PersistentCity.Runtime
{
    /// <summary>Detached raw field evidence, never sellable supply or a reservation.</summary>
    public sealed class IcecreamFeasibilitySnapshot : DirectStockFeasibilitySnapshot
    {
        internal IcecreamFeasibilitySnapshot(DirectStockFields fields) : base(fields) { }
        public override string ProductId => IcecreamFeasibilityReader.Product;
    }

    /// <summary>Explicit main-thread diagnostic only; no hooks, polling or gameplay caller.</summary>
    public static class IcecreamFeasibilityReader
    {
        public const string Product = "ba:itemname_icecream";
        public static IcecreamFeasibilitySnapshot Read(BuildingRegistration registration)
            => new IcecreamFeasibilitySnapshot(DirectStockShowcaseReader.Read(registration, DirectStockCandidate.Icecream));
    }
}
