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
        public string FoodEconomyPanelText => showEconomy ? economyRefresh.Text : "";
        public object[] FoodRegularsDump() => simulation?.DumpFoodRegulars(ObservedOwnShop) ?? System.Array.Empty<object>();
        public string ResidentPopupText => inspector.PopupText;
        public object? FoodMemoryDump(string consumerId) => simulation?.DumpFoodMemory(consumerId, ObservedOwnShop);
    }
}
