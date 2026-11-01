namespace PersistentCity.Runtime
{
    /// <summary>Detached raw field evidence, never sellable supply or a reservation.</summary>
    public sealed class PizzaFeasibilitySnapshot : DirectStockFeasibilitySnapshot
    {
        internal PizzaFeasibilitySnapshot(DirectStockFields fields) : base(fields) { }
        public override string ProductId => PizzaFeasibilityReader.Product;
    }

    /// <summary>Explicit main-thread diagnostic only; shipped pizza holder remains unestablished.</summary>
    public static class PizzaFeasibilityReader
    {
        public const string Product = "ba:itemname_pizza";
        public static PizzaFeasibilitySnapshot Read(BuildingRegistration registration)
            => new PizzaFeasibilitySnapshot(DirectStockShowcaseReader.Read(registration, DirectStockCandidate.Pizza));
    }
}
