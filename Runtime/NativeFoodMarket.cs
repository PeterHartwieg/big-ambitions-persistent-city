using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AI.Citizens;
using BigAmbitions.Items;
using AI.Customers.CustomerEntries;
using Buildings.Retail.Simulation;
using HarmonyLib;
using Helpers;
using Entities;
using Helpers.BusinessSimulation;
using PersistentCity.Core;
using PersistentCity.Persistence;
using UnityEngine;

namespace PersistentCity.Runtime
{
    /// <summary>Automatic whole-business food authority. Saves use the same native snapshot
    /// as population/meal records. Native code owns inventory, orders and daily accounting.</summary>
    public sealed partial class NativeFoodMarket : IFoodMarket, IFoodSaleEvidence, IFoodBusinessAuthority, ILocalFoodMarket, IDisposable
    {
        public const string Product = "ba:itemname_croissant";
        private const string PatchId = "persistentcity.food-routines";
        private static NativeFoodMarket? active;
        private readonly object save;
        private readonly FoodCommerceState state;
        private readonly Dictionary<string, BuildingRegistration> registrations = new Dictionary<string, BuildingRegistration>(StringComparer.Ordinal);
        private readonly Dictionary<string, CityBuildingController> shopControllers = new Dictionary<string, CityBuildingController>(StringComparer.Ordinal);
        private int discoveryCursor;
        private long lastOwnershipRefresh = -1;
        private readonly CityBuildingController[] controllers;
        private readonly Dictionary<string, string> supplyExclusions = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly string[] Products = FoodProducts.Ids.ToArray();
        private readonly Harmony patches = new Harmony(PatchId);
        private static readonly MethodInfo Process = AccessTools.Method(typeof(RetailBusinessSimulator), "ProcessCustomer");
        private bool buying;
        private int receiptDay = -1;
        private float approvedPrice;
        private string approvedProduct = "";
        private BuildingRegistration? buyerShop;
        public long SuppressedBatches => NativeFoodOwnership.SuppressedBatches;
        public long SuppressedDraws => NativeFoodOwnership.SuppressedDraws;
        public long NativePurchases { get; private set; }
        public double EarliestMinute => SaveGameManager.Current.Day * 1440.0 + SaveGameManager.Current.Hour * 60.0;
        public NativeFoodMarket(FoodCommerceState state, IReadOnlyList<Place>? places = null, bool enrolling = false)
        {
            if (active != null) throw new InvalidOperationException("A food market already owns the native hooks.");
            save = SaveGameManager.Current; this.state = state;
            NativeFoodOwnership.ValidatePopulation(state);
            controllers = CityManager.Instance.cityBuildingControllers.Where(c => c != null).ToArray();
            if (places != null)
                foreach (var place in places.Where(p => p.Kind == PlaceKind.Shop)) routinePlaces.Add(place.Id, place);
            DiscoverShops(controllers.Length);
            foreach (var scope in state.Shops)
                if (!scope.Products().All(FoodProducts.Known)) throw new InvalidOperationException("Saved food scope contains an unsupported product.");
            RefreshKnownOwnership();
            RefreshRoutineHours(EarliestMinute, true);
            active = this;
            try
            {
                NativeFoodOwnership.Attach(this);
                patches.Patch(AccessTools.Method(typeof(CitizenData), "IsPriceAcceptable", new[] { typeof(string), typeof(float) }),
                    prefix: new HarmonyMethod(typeof(NativeFoodMarket), nameof(ResidentPrice)) { priority = Priority.First });
            }
            catch { Dispose(); throw; }
        }
        public static string PlaceId(BuildingRegistration r) => "address:" + Uri.EscapeDataString(r.StreetName) + ":"
            + r.StreetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":shop";
        internal static bool Supported(BuildingRegistration r) => r.RentedByPlayer && r.HasEstablishedBusiness && !r.AvailableForRent
            && r.cachedAvailableProducts != null && r.cachedAvailableProducts.Count >= 1 && r.cachedAvailableProducts.Count <= 4
            && r.cachedAvailableProducts.All(FoodProducts.Known)
            && r.cachedAvailableProducts.Distinct(StringComparer.Ordinal).Count() == r.cachedAvailableProducts.Count
            && BusinessTypeHelper.GetData(r.businessTypeName)?.simulator?.GetType() == typeof(FullServiceBusinessSimulator)
            && !BusinessTypeHelper.GetData(r.businessTypeName).hasEntranceFee
            && BusinessTypeHelper.GetData(r.businessTypeName).HasTag(BigAmbitions.Tags.TagRef.Businesstag.customersneedpaperbags);
        public string ScopeStatus(string placeId)
        {
            string menu = MenuStatus(placeId);
            return menu.Length > 0 ? menu : supplyExclusions.TryGetValue(placeId, out var supply) ? supply : "";
        }
        private string MenuStatus(string placeId)
        {
            if (!registrations.TryGetValue(placeId, out var reg) || !Supported(reg)) return "Excluded: unsupported menu or business type";
            var authority = NativeFoodOwnership.Find(reg);
            if (authority == null || !authority.CanServe) return "Waiting for a safe native accounting boundary";
            return "";
        }
        internal static bool IsBuying(BuildingRegistration reg) => active != null && active.buying && ReferenceEquals(active.buyerShop, reg);
        public bool IsConfirmedPaid(ResidentMeal meal) => ReferenceEquals(save, SaveGameManager.Current)
            && FoodSaleReceipts.ReadStatus(SaveGameManager.Current.modData, meal) == FoodReceiptStatus.Paid;
        public bool CanServe(string businessId)
        {
            if (!ReferenceEquals(save, SaveGameManager.Current)) return false;
            foreach (var reg in registrations.Values)
            {
                var authority = NativeFoodOwnership.Find(reg);
                if (authority?.BusinessId != businessId) continue;
                if (!Supported(reg)) { NativeFoodOwnership.Observe(reg, false); return false; }
                return authority.CanServe;
            }
            return false;
        }
        // The controller ring visits every address, never just the first N eligible shops.
        // Native city buildings are static; business registration/menu/ownership are live.
        public void RefreshDiscovery() => DiscoverShops(64);
        private void DiscoverShops(int budget)
        {
            for (int count = 0; count < budget && controllers.Length > 0; count++)
            {
                var controller = controllers[discoveryCursor++ % controllers.Length];
                if (discoveryCursor == controllers.Length) discoveryCursor = 0;
                var reg = controller.buildingRegistration;
                if (reg == null || string.IsNullOrWhiteSpace(reg.StreetName)) continue;
                string place = PlaceId(reg);
                if (!routinePlaces.ContainsKey(place) || !GamePlaces.BuildingAccessible(controller)) continue;
                if (!reg.RentedByPlayer && !registrations.ContainsKey(place)) continue;
                registrations[place] = reg; shopControllers[place] = controller;
                var authority = NativeFoodOwnership.Observe(reg, Supported(reg));
                if (authority.CanServe) ActivateScope(authority, reg);
            }
        }
        internal void RefreshKnownOwnership()
        {
            foreach (var pair in shopControllers)
            {
                var reg = pair.Value.buildingRegistration;
                if (reg == null) { NativeFoodOwnership.Retire(pair.Key); registrations.Remove(pair.Key); continue; }
                registrations[pair.Key] = reg;
                var authority = NativeFoodOwnership.Observe(reg, Supported(reg) && GamePlaces.BuildingAccessible(pair.Value));
                if (authority.CanServe) ActivateScope(authority, reg);
            }
        }
        internal void ActivateScope(FoodAuthority authority, BuildingRegistration reg)
        {
            if (!authority.CanServe || !Supported(reg)) return;
            var products = reg.cachedAvailableProducts.OrderBy(p => p, StringComparer.Ordinal).ToArray();
            var scope = state.Shops.SingleOrDefault(s => s.PlaceId == authority.PlaceId);
            if (scope == null) { scope = new FoodScope { PlaceId = authority.PlaceId }; state.Shops.Add(scope); }
            if (scope.BusinessId != authority.BusinessId) scope.NextServiceAt = EarliestMinute;
            scope.BusinessId = authority.BusinessId; scope.ProductId = products[0]; scope.AdditionalProductIds = products.Skip(1).ToList();
        }
        private static bool ResidentPrice(string itemName, float price, ref bool __result)
        {
            if (active == null || !active.buying || itemName != active.approvedProduct) return true;
            __result = Math.Abs(price - active.approvedPrice) < .001f; return false;
        }
        public IReadOnlyList<FoodBusiness> Read(double minute) => ReadSelected(minute, state.Shops.Select(s => s.PlaceId).ToArray());
        public FoodBusiness? ReadBusiness(string businessId, double minute)
        {
            var scope = state.Shops.SingleOrDefault(s => s.BusinessId == businessId);
            return scope == null ? null : ReadSelected(minute, new[] { scope.PlaceId }).SingleOrDefault(b => b.Id == businessId);
        }
        public IReadOnlyList<FoodBusiness> ReadLocal(Place origin, double minute, double walkingToleranceMetres, int maximumCandidates)
        {
            if (maximumCandidates < 1 || maximumCandidates > 8 || !Position.Finite(walkingToleranceMetres) || walkingToleranceMetres < 0)
                throw new ArgumentException("Invalid local food observation bound.");
            // Preserve the original small comparison catalogue. Above that size, cheap saved
            // entrance geometry selects candidates before native supply setup/stock inspection.
            if (state.Shops.Count <= maximumCandidates) return Read(minute);
            var selected = state.Shops.Where(s => routinePlaces.ContainsKey(s.PlaceId) && registrations.TryGetValue(s.PlaceId, out var reg)
                    && Supported(reg) && NativeFoodOwnership.Find(reg)?.CanServe == true)
                .Select(s => new { s.PlaceId, s.BusinessId, Distance = Position.Distance(origin.Position, routinePlaces[s.PlaceId].Position) })
                .Where(s => s.Distance <= walkingToleranceMetres).OrderBy(s => s.Distance).ThenBy(s => s.BusinessId, StringComparer.Ordinal)
                .Take(maximumCandidates).Select(s => s.PlaceId).ToArray();
            return ReadSelected(minute, selected);
        }
        private IReadOnlyList<FoodBusiness> ReadSelected(double minute, string[] selectedPlaces)
        {
            if (!ReferenceEquals(save, SaveGameManager.Current)) throw new InvalidOperationException("Food market save changed.");
            long observationMinute = (long)(SaveGameManager.Current.Day * 1440.0 + SaveGameManager.Current.Hour * 60.0 + SaveGameManager.Current.Minute);
            if (lastOwnershipRefresh != observationMinute) { lastOwnershipRefresh = observationMinute; RefreshKnownOwnership(); }
            RefreshRoutineHours(minute, true);
            if (selectedPlaces.Length == 0) return Array.Empty<FoodBusiness>();
            var catalogue = FoodBusinessCatalogue.ReadSelected(selectedPlaces, Products,
                selectedPlaces.Where(shopControllers.ContainsKey).Select(p => shopControllers[p]),
                r => NativeFoodOwnership.Find(r)?.BusinessId ?? FoodBusinessCatalogue.BusinessIdentity(r));
            var result = new List<FoodBusiness>();
            foreach (var b in catalogue.Businesses)
            {
                if (!registrations.TryGetValue(b.PlaceId, out var reg) || MenuStatus(b.PlaceId).Length > 0
                    || !state.Shops.Any(s => s.PlaceId == b.PlaceId && s.BusinessId == b.Id)) continue;
                var scope = state.Shops.Single(s => s.PlaceId == b.PlaceId);
                b.Offers.RemoveAll(o => !scope.ContainsProduct(o.ProductId));
                var supply = FoodSupplyReader.ReadMenu(reg, scope.Products());
                if (supply.Values.Any(s => !s.Units.HasValue))
                { supplyExclusions[b.PlaceId] = "Excluded: menu stock contract is unverified"; continue; }
                supplyExclusions.Remove(b.PlaceId);
                b.MealsPerHour = supply[scope.ProductId].MealsPerHour; b.ServiceEvidence = supply[scope.ProductId].Evidence;
                b.ServiceUnavailableReason = supply[scope.ProductId].AvailabilityReason;
                foreach (var offer in b.Offers) offer.Units = supply[offer.ProductId].Units;
                result.Add(b);
            }
            return result;
        }
        public FoodSale Buy(ResidentMeal meal, double minute)
        {
            if (minute < EarliestMinute || minute >= EarliestMinute + 60 || !ReferenceEquals(save, SaveGameManager.Current))
                return new FoodSale(false, "Native clock differs from service time");
            var data = SaveGameManager.Current.modData;
            if (FoodSaleReceipts.AlreadyPaid(data, meal)) return new FoodSale(true, "Native saved receipt already confirms this intention");
            if (receiptDay != SaveGameManager.Current.Day)
            {
                receiptDay = SaveGameManager.Current.Day;
                FoodSaleReceipts.Prune(data, receiptDay, state.Residents
                    .Where(m => m.Status == MealStatus.Uncertain).Select(m => m.IntentId));
            }
            if (!CanServe(meal.BusinessId)) return new FoodSale(false, "Business is no longer owned by Persistent City");
            var business = ReadSelected(minute, new[] { meal.PlaceId }).SingleOrDefault(b => b.PlaceId == meal.PlaceId && b.Id == meal.BusinessId);
            var offer = business?.Offers.SingleOrDefault(o => o.ProductId == meal.ProductId);
            string exclusion = ScopeStatus(meal.PlaceId);
            if (exclusion.Length > 0) return new FoodSale(false, exclusion);
            if (offer != null && offer.PriceCents != meal.PriceCents) return new FoodSale(false, "Approved price changed");
            if (offer == null || !offer.Units.HasValue || offer.Units <= 0 || !business!.MealsPerHour.HasValue || business.MealsPerHour <= 0
                || offer.PriceCents != meal.PriceCents || meal.PriceCents > meal.AllowanceCents
                || meal.ServiceMinutes + 1e-7 < 60.0 / business.MealsPerHour.Value)
                return new FoodSale(false, "Stock or staffing changed");
            var reg = registrations[meal.PlaceId];
            if (!BusinessHelper.IsBusinessOpen(reg)) return new FoodSale(false, "Shop closed before service");
            var scope = state.Shops.Single(s => s.PlaceId == meal.PlaceId);
            var before = FoodSupplyReader.ReadMenu(reg, scope.Products());
            if (before.Values.Any(s => !s.Units.HasValue)) return new FoodSale(false, "Menu supply contract changed");
            int orders = reg.unprocessedCompletedOrders.Count;
            var customer = new CustomerEntry { spawnTime = TimeHelper.Now(), order = new Order {
                entries = new List<OrderEntry> { new OrderEntry { itemName = meal.ProductId } } } };
            var simulator = ScriptableObject.CreateInstance<FullServiceBusinessSimulator>();
            try
            {
                simulator.SetUp(reg, SaveGameManager.Current.Hour);
                approvedProduct = meal.ProductId;
                approvedPrice = reg.retailPrices.Single(p => p.itemName == meal.ProductId).price;
                FoodSaleReceipts.Begin(data, meal);
                buyerShop = reg; buying = true;
                Process.Invoke(simulator, new object[] { customer });
            }
            finally { buying = false; buyerShop = null; UnityEngine.Object.Destroy(simulator); }
            var entries = customer.order.entries.Where(e => e.paid && ItemsGetter.GetByName(e.itemName)?.HasTag(BigAmbitions.Tags.TagRef.Itemtag.isbag) != true).ToArray();
            var after = FoodSupplyReader.ReadMenu(reg, scope.Products());
            if (entries.Length != 1 || entries[0].itemName != meal.ProductId || Math.Abs(entries[0].price - approvedPrice) >= .001f
                || !customer.order.completed || after[meal.ProductId].ProducerUnits != before[meal.ProductId].ProducerUnits - 1
                || after[meal.ProductId].BagUnits != before[meal.ProductId].BagUnits - 1
                || scope.Products().Any(p => p != meal.ProductId && after[p].ProducerUnits != before[p].ProducerUnits)
                || reg.unprocessedCompletedOrders.Count != orders + 1)
                throw new InvalidOperationException("Native sale outcome is uncertain; do not retry this intention.");
            FoodSaleReceipts.Confirm(data, meal);
            NativePurchases++; return new FoodSale(true, "Native product, bag and completed order confirmed; daily settlement remains native");
        }
        public void Dispose()
        {
            patches.UnpatchAll(PatchId);
            NativeFoodOwnership.Detach(this);
            if (ReferenceEquals(active, this)) active = null;
        }
    }
}
