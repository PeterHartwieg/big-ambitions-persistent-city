namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        /// <summary>Bridge-callable detached consumer memory facts. No UI, market refresh or routes.</summary>
        public object? FoodMemoryDump(string consumerId) => simulation?.DumpFoodMemory(consumerId);
    }
}
