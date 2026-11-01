namespace PersistentCity.Runtime
{
    /// <summary>Detached conditional field evidence; the shipped fries holder is unestablished.</summary>
    public sealed class FrenchFriesFeasibilitySnapshot : DirectStockFeasibilitySnapshot
    {
        internal FrenchFriesFeasibilitySnapshot(DirectStockFields fields) : base(fields) { }
        public override string ProductId => FrenchFriesFeasibilityReader.Product;
    }

    /// <summary>Explicit main-thread diagnostic only; no hooks, polling or gameplay caller.</summary>
    public static class FrenchFriesFeasibilityReader
    {
        public const string Product = "ba:itemname_frenchfries";
        public static FrenchFriesFeasibilitySnapshot Read(BuildingRegistration registration)
            => new FrenchFriesFeasibilitySnapshot(DirectStockShowcaseReader.Read(registration, DirectStockCandidate.FrenchFries));
    }
}
