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
    /// <summary>Opt-in single-stocked-product retail slice. Saves use the same native snapshot
    /// as population/meal records. Native code owns inventory, orders and daily accounting.</summary>
    public sealed class NativeFoodMarket : IFoodMarket, IDisposable
    {
        public const string Product = "ba:itemname_croissant";
        private const string PatchId = "persistentcity.food-routines";
        private static NativeFoodMarket? active;
        private readonly object save;
        private readonly FoodCommerceState state;
        private readonly Dictionary<string, BuildingRegistration> registrations;
        private readonly CityBuildingController[] controllers;
        private readonly string[] selectedPlaces;
        private static readonly string[] Products = { Product };
        private readonly Harmony patches = new Harmony(PatchId);
        private static readonly FieldInfo Registration = AccessTools.Field(typeof(BusinessSimulator), "buildingRegistration");
        private static readonly MethodInfo Process = AccessTools.Method(typeof(RetailBusinessSimulator), "ProcessCustomer");
        private bool buying;
        private int receiptDay = -1;
        private float approvedPrice;
        private BuildingRegistration? buyerShop;
        public long SuppressedBatches { get; private set; }
        public long SuppressedDraws { get; private set; }
        public long NativePurchases { get; private set; }
        public double EarliestMinute => SaveGameManager.Current.Day * 1440.0 + SaveGameManager.Current.Hour * 60.0;
        public NativeFoodMarket(FoodCommerceState state)
        {
            if (active != null) throw new InvalidOperationException("A food market already owns the native hooks.");
            save = SaveGameManager.Current; this.state = state;
            selectedPlaces = state.Shops.Select(s => s.PlaceId).ToArray();
            controllers = CityManager.Instance.cityBuildingControllers.Where(c => c != null && c.buildingRegistration != null
                && selectedPlaces.Contains(PlaceId(c.buildingRegistration))).ToArray();
            registrations = controllers.Select(c => c.buildingRegistration).ToDictionary(PlaceId, StringComparer.Ordinal);
            foreach (var scope in state.Shops)
                if (scope.ProductId != Product || !registrations.TryGetValue(scope.PlaceId, out var reg) || !Supported(reg))
                    throw new InvalidOperationException("Food routines require selected player-owned full-service shops advertising only the supported stocked pastry.");
            active = this;
            try
            {
                patches.Patch(AccessTools.Method(typeof(RetailBusinessSimulator), "ProcessAllCustomersFromThisHour"),
                    prefix: new HarmonyMethod(typeof(NativeFoodMarket), nameof(HourlyCustomers)) { priority = Priority.First });
                patches.Patch(AccessTools.Method(typeof(CustomerEntriesHelper), "GetEntriesByAddress"),
                    prefix: new HarmonyMethod(typeof(NativeFoodMarket), nameof(CustomerDraw)) { priority = Priority.First });
                patches.Patch(AccessTools.Method(typeof(CustomerEntriesHelper), "UpdateCustomerEntriesForPlayerBusiness"),
                    prefix: new HarmonyMethod(typeof(NativeFoodMarket), nameof(GenerateCustomers)) { priority = Priority.First });
                patches.Patch(Process, prefix: new HarmonyMethod(typeof(NativeFoodMarket), nameof(CustomerSale)) { priority = Priority.First });
                patches.Patch(AccessTools.Method(typeof(CitizenData), "IsPriceAcceptable", new[] { typeof(string), typeof(float) }),
                    prefix: new HarmonyMethod(typeof(NativeFoodMarket), nameof(ResidentPrice)) { priority = Priority.First });
            }
            catch { Dispose(); throw; }
        }
        public static string PlaceId(BuildingRegistration r) => "address:" + Uri.EscapeDataString(r.StreetName) + ":"
            + r.StreetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":shop";
        private static bool Supported(BuildingRegistration r) => r.RentedByPlayer && r.HasEstablishedBusiness
            && r.cachedAvailableProducts != null && r.cachedAvailableProducts.Count == 1 && r.cachedAvailableProducts[0] == Product
            && BusinessTypeHelper.GetData(r.businessTypeName)?.simulator?.GetType() == typeof(FullServiceBusinessSimulator)
            && !BusinessTypeHelper.GetData(r.businessTypeName).hasEntranceFee;
        private bool Owns(BuildingRegistration r) => ReferenceEquals(save, SaveGameManager.Current)
            && registrations.Values.Any(v => ReferenceEquals(r, v));
        private static bool HourlyCustomers(RetailBusinessSimulator __instance)
        {
            if (active == null || !active.Owns((BuildingRegistration)Registration.GetValue(__instance))) return true;
            active.SuppressedBatches++; return false; // RestockShelvesIfItsTime and stock tasks still execute.
        }
        private static bool GenerateCustomers(BuildingRegistration registration) => active == null || !active.Owns(registration);
        private static bool CustomerSale(RetailBusinessSimulator __instance)
        {
            var reg = (BuildingRegistration)Registration.GetValue(__instance);
            return active == null || !active.Owns(reg) || active.buying && ReferenceEquals(active.buyerShop, reg);
        }
        private static bool CustomerDraw(Address address, ref List<CustomerEntry> __result)
        {
            if (active == null || !ReferenceEquals(active.save, SaveGameManager.Current)
                || !active.registrations.Values.Any(r => r.Address == address)) return true;
            active.SuppressedDraws++; __result = new List<CustomerEntry>(); return false;
        }
        private static bool ResidentPrice(string itemName, float price, ref bool __result)
        {
            if (active == null || !active.buying || itemName != Product) return true;
            __result = Math.Abs(price - active.approvedPrice) < .001f; return false;
        }
        public IReadOnlyList<FoodBusiness> Read(double minute)
        {
            if (!ReferenceEquals(save, SaveGameManager.Current)) throw new InvalidOperationException("Food market save changed.");
            var catalogue = FoodBusinessCatalogue.ReadSelected(selectedPlaces, Products, controllers);
            var result = new List<FoodBusiness>();
            foreach (var b in catalogue.Businesses)
            {
                if (!registrations.TryGetValue(b.PlaceId, out var reg) || !Supported(reg)
                    || !state.Shops.Any(s => s.PlaceId == b.PlaceId && s.BusinessId == b.Id)) continue;
                var supply = FoodSupplyReader.Read(reg, Product);
                b.MealsPerHour = supply.MealsPerHour; b.ServiceEvidence = supply.Evidence;
                foreach (var offer in b.Offers) offer.Units = supply.Units;
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
            { receiptDay = SaveGameManager.Current.Day; FoodSaleReceipts.Prune(data, receiptDay); }
            var business = Read(minute).SingleOrDefault(b => b.PlaceId == meal.PlaceId && b.Id == meal.BusinessId);
            var offer = business?.Offers.SingleOrDefault(o => o.ProductId == meal.ProductId);
            if (offer == null || !offer.Units.HasValue || offer.Units <= 0 || !business!.MealsPerHour.HasValue || business.MealsPerHour <= 0
                || offer.PriceCents != meal.PriceCents || meal.PriceCents > meal.AllowanceCents
                || meal.ServiceMinutes + 1e-7 < 60.0 / business.MealsPerHour.Value)
                return new FoodSale(false, "Stock, staff or approved price changed");
            var reg = registrations[meal.PlaceId];
            if (!BusinessHelper.IsBusinessOpen(reg)) return new FoodSale(false, "Shop closed before service");
            var before = FoodSupplyReader.Read(reg, Product);
            int orders = reg.unprocessedCompletedOrders.Count;
            var customer = new CustomerEntry { spawnTime = TimeHelper.Now(), order = new Order {
                entries = new List<OrderEntry> { new OrderEntry { itemName = Product } } } };
            var simulator = ScriptableObject.CreateInstance<FullServiceBusinessSimulator>();
            try
            {
                simulator.SetUp(reg, SaveGameManager.Current.Hour);
                approvedPrice = reg.retailPrices.Single(p => p.itemName == Product).price;
                FoodSaleReceipts.Begin(data, meal);
                buyerShop = reg; buying = true;
                Process.Invoke(simulator, new object[] { customer });
            }
            finally { buying = false; buyerShop = null; UnityEngine.Object.Destroy(simulator); }
            var entry = customer.order.entries.Single(e => e.itemName == Product);
            var after = FoodSupplyReader.Read(reg, Product);
            if (!entry.paid || !customer.order.completed || after.ProducerUnits != before.ProducerUnits - 1
                || after.BagUnits != before.BagUnits - 1 || reg.unprocessedCompletedOrders.Count != orders + 1)
                throw new InvalidOperationException("Native sale outcome is uncertain; do not retry this intention.");
            FoodSaleReceipts.Confirm(data, meal);
            NativePurchases++; return new FoodSale(true, "Native product, bag and completed order confirmed; daily settlement remains native");
        }
        public void Dispose()
        {
            patches.UnpatchAll(PatchId);
            if (ReferenceEquals(active, this)) active = null;
        }
    }
}
