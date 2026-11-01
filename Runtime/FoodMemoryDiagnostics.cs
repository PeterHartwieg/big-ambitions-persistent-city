namespace PersistentCity.Runtime
{
    public sealed partial class NativeFoodMarket
    {
        public bool ObservedOwnShop(PersistentCity.Core.FoodScope scope)
            => ReferenceEquals(save, SaveGameManager.Current)
                && shopControllers.TryGetValue(scope.PlaceId, out var controller)
                && registrations.TryGetValue(scope.PlaceId, out var reg)
                && ReferenceEquals(controller.buildingRegistration, reg) && reg.RentedByPlayer
                && reg.HasEstablishedBusiness && !reg.AvailableForRent
                && NativeFoodOwnership.ObservedOwnGeneration(reg, scope.BusinessId);
    }

    public sealed partial class PopulationRuntime
    {
        private bool ObservedOwnShop(PersistentCity.Core.FoodScope scope) => foodMarket?.ObservedOwnShop(scope) == true;
        /// <summary>Last rendered panel image, read-only; does not open or rebuild it.</summary>
        public string FoodEconomyPanelText => showEconomy ? economyText : "";
        /// <summary>Bridge-callable detached consumer memory facts. No UI, market refresh or routes.</summary>
        public object[] FoodRegularsDump() => simulation?.DumpFoodRegulars(ObservedOwnShop) ?? System.Array.Empty<object>();
        public string ResidentPopupText => inspector.PopupText;
        public object? FoodMemoryDump(string consumerId) => simulation?.DumpFoodMemory(consumerId, ObservedOwnShop);
    }
}
