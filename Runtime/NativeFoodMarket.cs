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
    public sealed partial class NativeFoodMarket : IFoodMarket, IFoodSaleEvidence, IFoodBusinessAuthority, ILocalFoodMarket, IFoodCandidateMarket, IFoodFailureMarket, IDisposable
    {
        public const string Product = "ba:itemname_croissant";
        private const string PatchId = "persistentcity.food-routines";
        private static NativeFoodMarket? active;
        private readonly object save;
        private RivalFoodSettings rivalSettings = new RivalFoodSettings();
        private readonly Action<string>? rivalWarning;
        private readonly FoodCommerceState state;
        private readonly Dictionary<string, BuildingRegistration> registrations = new Dictionary<string, BuildingRegistration>(StringComparer.Ordinal);
        private readonly Dictionary<string, CityBuildingController> shopControllers = new Dictionary<string, CityBuildingController>(StringComparer.Ordinal);
        private long lastOwnershipRefresh = -1;
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
        /// <summary>Checkouts whose pre-sale image was the supply read made earlier in the same Buy call.</summary>
        public long CheckoutSupplyReuses { get; private set; }
        /// <summary>Decision reads (Read, ReadBusiness, ReadLocal) answered from this game minute's cached
        /// supply image, and those that made a live native read. Checkout reads never use this cache.</summary>
        public long DecisionSupplyHits { get; private set; }
        public long DecisionSupplyMisses { get; private set; }

        // Decision-path supply cache. One entry per place, valid only for the same save, game minute, food
        // authority payload, registration object, business identity, ordered menu and the registration fields
        // the reader branches on. Buy never reads from it and drops the place's entry when it finishes.
        private sealed class DecisionSupply
        {
            public DecisionSupply(BuildingRegistration registration, string businessId, string[] products,
                IReadOnlyDictionary<string, FoodSupplySnapshot> supply)
            {
                Registration = registration; BusinessId = businessId; Products = products; Supply = supply;
                RentedByPlayer = registration.RentedByPlayer; TemporarilyClosed = registration.temporarilyClosed;
                Established = registration.HasEstablishedBusiness; BusinessType = registration.businessTypeName;
            }
            public BuildingRegistration Registration { get; }
            public string BusinessId { get; }
            public string[] Products { get; }
            public IReadOnlyDictionary<string, FoodSupplySnapshot> Supply { get; }
            public bool RentedByPlayer { get; }
            public bool TemporarilyClosed { get; }
            public bool Established { get; }
            public string BusinessType { get; }
            public bool Matches(BuildingRegistration registration, string businessId, string[] products)
                => ReferenceEquals(Registration, registration) && BusinessId == businessId
                    && Products.SequenceEqual(products, StringComparer.Ordinal)
                    && RentedByPlayer == registration.RentedByPlayer && TemporarilyClosed == registration.temporarilyClosed
                    && Established == registration.HasEstablishedBusiness
                    && string.Equals(BusinessType, registration.businessTypeName, StringComparison.Ordinal);
        }
        private readonly Dictionary<string, DecisionSupply> decisionSupply = new Dictionary<string, DecisionSupply>(StringComparer.Ordinal);
        private long decisionSupplyMinute = long.MinValue;
        private string? decisionSupplyAuthority;

        // One place's supply image from ReadSelected, kept only by the Buy call that asked for it.
        private sealed class SupplyObservation
        {
            public SupplyObservation(BuildingRegistration registration, string[] products, IReadOnlyDictionary<string, FoodSupplySnapshot> supply)
            { Registration = registration; Products = products; Supply = supply; }
            public BuildingRegistration Registration { get; }
            public string[] Products { get; }
            public IReadOnlyDictionary<string, FoodSupplySnapshot> Supply { get; }
        }
        public double EarliestMinute => SaveGameManager.Current.Day * 1440.0 + SaveGameManager.Current.Hour * 60.0;
        public NativeFoodMarket(FoodCommerceState state, IReadOnlyList<Place>? places = null, bool enrolling = false, RivalFoodSettings? rivalSettings = null, Action<string>? rivalWarning = null)
        {
            if (active != null) throw new InvalidOperationException("A food market already owns the native hooks.");
            save = SaveGameManager.Current; this.state = state;
            this.rivalSettings = rivalSettings ?? new RivalFoodSettings(); this.rivalSettings.Clamp();
            this.rivalWarning = rivalWarning;
            NativeFoodOwnership.ValidatePopulation(state);
            controllers = CityManager.Instance.cityBuildingControllers.Where(c => c != null).ToArray();
            discoveryRegistrations = new BuildingRegistration?[controllers.Length];
            discoveryStreets = new string?[controllers.Length];
            discoveryNumbers = new int[controllers.Length];
            discoveryPlaces = new string?[controllers.Length];
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
        public bool IsConfirmedPaid(ResidentMeal meal) => meal.TargetKind == FoodAlternativeKind.Player && ReferenceEquals(save, SaveGameManager.Current)
            && FoodSaleReceipts.ReadStatus(SaveGameManager.Current.modData, meal) == FoodReceiptStatus.Paid;
        public bool CanServe(string businessId)
        {
            if (!ReferenceEquals(save, SaveGameManager.Current)) return false;
            if(!scopesByBusiness.TryGetValue(businessId,out var scope) || !registrations.TryGetValue(scope.PlaceId,out var reg)) return false;
            var authority=NativeFoodOwnership.Find(reg);
            if(authority?.BusinessId!=businessId) return false;
            if(!Supported(reg)) { NativeFoodOwnership.Observe(reg,false);return false; }
            return authority.CanServe;
        }
        private static bool ResidentPrice(string itemName, float price, ref bool __result)
        {
            if (active == null || !active.buying || itemName != active.approvedProduct) return true;
            __result = price == active.approvedPrice; return false;
        }
        public IReadOnlyList<FoodBusiness> Read(double minute) => ReadSelected(minute, state.Shops.Select(s => s.PlaceId).ToArray());
        public FoodBusiness? ReadBusiness(string businessId, double minute)
        {
            scopesByBusiness.TryGetValue(businessId,out var scope);
            return scope == null ? null : ReadSelected(minute, new[] { scope.PlaceId }).SingleOrDefault(b => b.Id == businessId);
        }
        public FoodBusiness? ReadFailureBusiness(string businessId, string placeId, double minute)
            => ReadSelected(minute, new[] { placeId }, capture: true).SingleOrDefault(b => b.Id == businessId && b.PlaceId == placeId);
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
        private IReadOnlyList<FoodBusiness> ReadSelected(double minute, string[] selectedPlaces,
            Dictionary<string, SupplyObservation>? observed = null, bool capture = false)
        {
            RefreshReadMetadata(minute);
            long observationMinute = (long)(SaveGameManager.Current.Day * 1440.0 + SaveGameManager.Current.Hour * 60.0 + SaveGameManager.Current.Minute);
            if (selectedPlaces.Length == 0) return Array.Empty<FoodBusiness>();
            var catalogue = FoodBusinessCatalogue.ReadSelected(selectedPlaces, Products,
                // A replaced registration can leave historical place keys pointing at one controller.
                // Observe that controller once; distinct controllers with conflicting addresses still refuse.
                selectedPlaces.Where(shopControllers.ContainsKey).Select(p => shopControllers[p]).Distinct(),
                r => NativeFoodOwnership.Find(r)?.BusinessId ?? FoodBusinessCatalogue.BusinessIdentity(r));
            var result = new List<FoodBusiness>();
            foreach (var b in catalogue.Businesses)
            {
                if (!registrations.TryGetValue(b.PlaceId, out var reg) || MenuStatus(b.PlaceId).Length > 0
                    || !scopesByPlace.TryGetValue(b.PlaceId,out var scope) || scope.BusinessId!=b.Id) continue;
                b.Offers.RemoveAll(o => !scope.ContainsProduct(o.ProductId));
                var products = scope.Products().ToArray();
                // Checkout (observed != null) always reads live, exactly as before this cache existed.
                var supply = observed != null || capture ? FoodSupplyReader.ReadMenu(reg, products)
                    : DecisionMenu(observationMinute, b.PlaceId, reg, b.Id, products);
                if (supply.Values.Any(s => !s.Units.HasValue))
                {
                    supplyExclusions[b.PlaceId] = "Excluded: menu stock contract is unverified";
                    if(!capture) continue;
                }
                else supplyExclusions.Remove(b.PlaceId);
                if (observed != null) observed[b.PlaceId] = new SupplyObservation(reg, products, supply);
                b.MealsPerHour = supply[scope.ProductId].MealsPerHour; b.ServiceEvidence = supply[scope.ProductId].Evidence;
                b.ServiceUnavailableReason = supply[scope.ProductId].AvailabilityReason;
                foreach (var offer in b.Offers) offer.Units = supply[offer.ProductId].Units;
                result.Add(b);
            }
            return result;
        }
        // A decision read reuses this game minute's image of one shop. It never authorizes a sale:
        // Buy reads supply live, sells, and confirms against a live after image.
        private IReadOnlyDictionary<string, FoodSupplySnapshot> DecisionMenu(long observationMinute, string placeId,
            BuildingRegistration reg, string businessId, string[] products)
        {
            SaveGameManager.Current.modData.TryGetValue(FoodAuthorityStore.Key, out var authority);
            if (decisionSupplyMinute != observationMinute || !ReferenceEquals(decisionSupplyAuthority, authority))
            { decisionSupply.Clear(); decisionSupplyMinute = observationMinute; decisionSupplyAuthority = authority; }
            // A registration whose item list left the supply contract reads live, so the contract failure
            // still surfaces at the decision read and not only at checkout.
            if (reg.itemInstances != null && reg.itemInstances.Count <= 2000
                && decisionSupply.TryGetValue(placeId, out var cached) && cached.Matches(reg, businessId, products))
            { DecisionSupplyHits++; return cached.Supply; }
            DecisionSupplyMisses++;
            var supply = FoodSupplyReader.ReadMenu(reg, products);
            decisionSupply[placeId] = new DecisionSupply(reg, businessId, products, supply);
            return supply;
        }
        public FoodSale Buy(ResidentMeal meal, double minute)
        {
            if (meal.TargetKind != FoodAlternativeKind.Player) return new FoodSale(false, "Rivals have no native checkout authority");
            // Any checkout attempt may change this shop's native stock, bags or orders.
            try { return BuyLive(meal, minute); }
            finally { decisionSupply.Remove(meal.PlaceId); }
        }
        private FoodSale BuyLive(ResidentMeal meal, double minute)
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
            var observed = new Dictionary<string, SupplyObservation>(1, StringComparer.Ordinal);
            FoodBusiness? business;
            try { business = ReadSelected(minute, new[] { meal.PlaceId }, observed).SingleOrDefault(b => b.PlaceId == meal.PlaceId && b.Id == meal.BusinessId); }
            catch (FoodBusinessCatalogue.UnsupportedPriceException)
            {
                // No receipt or native sale has started. A known invalid quote is refundable.
                return new FoodSale(false, "Approved price is outside the supported range");
            }
            catch (FoodBusinessCatalogue.CatalogueRefusalException ex)
            {
                return new FoodSale(false, "Catalogue refused: " + ex.Message);
            }
            var offer = business?.Offers.SingleOrDefault(o => o.ProductId == meal.ProductId);
            string exclusion = ScopeStatus(meal.PlaceId);
            if (exclusion.Length > 0) return new FoodSale(false, exclusion);
            if (offer != null && offer.Price != meal.Price) return new FoodSale(false, "Approved price changed");
            var reg = registrations[meal.PlaceId];
            if (!BusinessHelper.IsBusinessOpen(reg)) return new FoodSale(false, "Shop closed before service", ShopExperience.Closed);
            // A zero sellable-unit count can also mean no staffed counter. Name stockout only
            // when this same live read proves empty product stock or empty bags at a staffed counter.
            if (offer != null && offer.Units == 0 && observed.TryGetValue(meal.PlaceId, out var liveSupply)
                && liveSupply.Supply.TryGetValue(meal.ProductId, out var supply) && supply.Units == 0
                && (supply.ProducerUnits == 0 || supply.StaffedCounters > 0 && supply.BagUnits == 0))
                return new FoodSale(false, "Confirmed stockout before service", ShopExperience.Stockout);
            if (offer == null || !offer.Units.HasValue || offer.Units <= 0 || !business!.MealsPerHour.HasValue || business.MealsPerHour <= 0
                || offer.Price != meal.Price || meal.Price > meal.Allowance
                || meal.ServiceMinutes + 1e-7 < 60.0 / business.MealsPerHour.Value)
                return new FoodSale(false, "Stock or staffing changed");
            var scope = state.Shops.Single(s => s.PlaceId == meal.PlaceId);
            var products = scope.Products().ToArray();
            // The pre-sale image is the supply ReadSelected read above, in this same call, for this registration and
            // menu at this game minute. Nothing in between writes native supply: SetUp, capacity, ownership lookup and
            // IsBusinessOpen only read. Any other case reads afresh. The sale and the after image always stay live.
            double now = SaveGameManager.Current.Day * 1440.0 + SaveGameManager.Current.Hour * 60.0 + SaveGameManager.Current.Minute;
            IReadOnlyDictionary<string, FoodSupplySnapshot> before;
            if (observed.TryGetValue(meal.PlaceId, out var seen) && ReferenceEquals(seen.Registration, reg)
                && seen.Products.SequenceEqual(products, StringComparer.Ordinal) && seen.Supply.Values.All(s => s.AtMinute == now))
            { before = seen.Supply; CheckoutSupplyReuses++; }
            else before = FoodSupplyReader.ReadMenu(reg, products);
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
                Money livePrice;
                try { livePrice = Money.FromNative(approvedPrice); }
                catch (ArgumentOutOfRangeException)
                {
                    // No receipt or native sale exists yet: this is a definite refusal, not uncertain ownership.
                    return new FoodSale(false, "Approved price is outside the supported range");
                }
                if (livePrice != meal.Price)
                    return new FoodSale(false, "Approved price changed during native setup");
                FoodSaleReceipts.Begin(data, meal);
                buyerShop = reg; buying = true;
                Process.Invoke(simulator, new object[] { customer });
            }
            finally { buying = false; buyerShop = null; UnityEngine.Object.Destroy(simulator); }
            var entries = customer.order.entries.Where(e => e.paid && ItemsGetter.GetByName(e.itemName)?.HasTag(BigAmbitions.Tags.TagRef.Itemtag.isbag) != true).ToArray();
            var after = FoodSupplyReader.ReadMenu(reg, scope.Products());
            if (entries.Length != 1 || entries[0].itemName != meal.ProductId || entries[0].price != approvedPrice
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
            ((object)this as IRivalFoodMarket)?.CloseRivals();
            patches.UnpatchAll(PatchId);
            NativeFoodOwnership.Detach(this);
            if (ReferenceEquals(active, this)) active = null;
        }
    }
}
