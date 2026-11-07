using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        private readonly int menuLimit;
        private readonly Dictionary<string, BuildingRegistration> registrations = new Dictionary<string, BuildingRegistration>(StringComparer.Ordinal);
        private readonly Dictionary<string, CityBuildingController> shopControllers = new Dictionary<string, CityBuildingController>(StringComparer.Ordinal);
        private long lastOwnershipRefresh = -1;
        private readonly Dictionary<string, string> supplyExclusions = new Dictionary<string, string>(StringComparer.Ordinal);
        private sealed class BagPolicy { internal BagPolicy(bool bags) { Bags = bags; } internal readonly bool Bags; }
        // Weak keys prevent retired registrations/definitions accumulating during menu churn.
        private ConditionalWeakTable<object, BagPolicy> authoredBagPolicies = new ConditionalWeakTable<object, BagPolicy>();
        private readonly Dictionary<Type, FieldInfo?> authoredTagFields = new Dictionary<Type, FieldInfo?>();
        private sealed class ContractRefusal { internal ContractRefusal(long hour) { Hour = hour; } internal readonly long Hour; }
        private ConditionalWeakTable<BuildingRegistration, ContractRefusal> refusedContracts = new ConditionalWeakTable<BuildingRegistration, ContractRefusal>();
        private long lastContractRecoveryHour = -1;
        private void RefuseContract(BuildingRegistration registration)
        { if (!refusedContracts.TryGetValue(registration, out _)) refusedContracts.Add(registration,
            new ContractRefusal(SaveGameManager.Current.Day * 24L + SaveGameManager.Current.Hour)); }
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
                Registration = registration; BusinessId = businessId; Products = products; Image = new DecisionSupplyImage<IReadOnlyDictionary<string, FoodSupplySnapshot>>(supply);
                RentedByPlayer = registration.RentedByPlayer; TemporarilyClosed = registration.temporarilyClosed;
                Established = registration.HasEstablishedBusiness; BusinessType = registration.businessTypeName;
            }
            public BuildingRegistration Registration { get; }
            public string BusinessId { get; }
            public string[] Products { get; }
            public DecisionSupplyImage<IReadOnlyDictionary<string, FoodSupplySnapshot>> Image { get; }
            public IReadOnlyDictionary<string, FoodSupplySnapshot> Supply => Image.Value;
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
        private readonly FoodChangeJournal? changeJournal;
        private bool journalReady;
        public ShopObservation? JournalFor(string businessId) => changeJournal?.Find(businessId);
        private readonly ShopObservedOffer[] journalOffers = new ShopObservedOffer[8];
        public NativeFoodMarket(FoodCommerceState state, IReadOnlyList<Place>? places = null, bool enrolling = false, RivalFoodSettings? rivalSettings = null, Action<string>? rivalWarning = null, int maximumOffers = 4, EconomicLedger? economy = null)
        {
            if (active != null) throw new InvalidOperationException("A food market already owns the native hooks.");
            save = SaveGameManager.Current; this.state = state;
            if (economy != null) changeJournal = new FoodChangeJournal(state, economy);
            this.rivalSettings = rivalSettings ?? new RivalFoodSettings(); this.rivalSettings.Clamp();
            this.rivalWarning = rivalWarning;
            menuLimit=Math.Max(1,Math.Min(FoodMenuPolicy.LiveMaximumOffers,maximumOffers));
            NativeFoodOwnership.ValidatePopulation(state);
            controllers = CityManager.Instance.cityBuildingControllers.Where(c => c != null).ToArray();
            foreach (var controller in controllers) if (controller.buildingRegistration != null) CacheBagPolicy(controller.buildingRegistration);
            discoveryRegistrations = new BuildingRegistration?[controllers.Length];
            discoveryStreets = new string?[controllers.Length];
            discoveryNumbers = new int[controllers.Length];
            discoveryPlaces = new string?[controllers.Length];
            if (places != null)
                foreach (var place in places.Where(p => p.Kind == PlaceKind.Shop)) routinePlaces.Add(place.Id, place);
            DiscoverShops(controllers.Length);
            // A missing definition is unavailable service evidence, not corrupt saved identity.
            // Keep scopes, receipts and history for normal whole-business withdrawal/recovery.
            foreach (var scope in state.Shops)
                if (!scope.Products().All(FoodNameIdentity.IsCanonical)) throw new InvalidOperationException("Saved food scope contains a malformed product identity.");
            RefreshKnownOwnership();
            RefreshRoutineHours(EarliestMinute, true);
            active = this;
            try
            {
                NativeFoodOwnership.Attach(this);
                journalReady = true;
                patches.Patch(AccessTools.Method(typeof(CitizenData), "IsPriceAcceptable", new[] { typeof(string), typeof(float) }),
                    prefix: new HarmonyMethod(typeof(NativeFoodMarket), nameof(ResidentPrice)) { priority = Priority.First });
            }
            catch { Dispose(); throw; }
        }
        public static string PlaceId(BuildingRegistration r) => "address:" + Uri.EscapeDataString(r.StreetName) + ":"
            + r.StreetNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":shop";
        // Warm outside the frame-discovery ring. A newly encountered definition stays refused
        // until an existing ownership refresh can read its authored metadata.
        private void CacheBagPolicy(BuildingRegistration registration)
        {
            if (!registration.RentedByPlayer || !registration.HasEstablishedBusiness || registration.AvailableForRent
                || string.IsNullOrWhiteSpace(registration.businessTypeName)) return;
            var type = BusinessTypeHelper.GetData(registration.businessTypeName);
            if (type == null || authoredBagPolicies.TryGetValue(type, out _)) return;
            bool bags = false;
            if (!authoredTagFields.TryGetValue(type.GetType(), out var field))
            {
                for (Type? t = type.GetType(); t != null; t = t.BaseType)
                {
                    field = t.GetField("tags", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (field != null) break;
                }
                authoredTagFields.Add(type.GetType(), field);
            }
            if (field?.FieldType == typeof(List<string>) && field.GetValue(type) is List<string> tags && tags.Count <= 128
                && tags.All(tag => !string.IsNullOrWhiteSpace(tag)))
                bags = tags.Contains("ba:businesstag_customersneedpaperbags");
            authoredBagPolicies.Add(type, new BagPolicy(bags));
        }
        private bool SupportedMenu(BuildingRegistration r) => !refusedContracts.TryGetValue(r, out _) && SupportedBusiness(r)
            && r.cachedAvailableProducts.Count <= menuLimit;
        private bool CurrentAuthoredBagPolicy(BuildingRegistration r)
        {
            var type = BusinessTypeHelper.GetData(r.businessTypeName);
            if (type == null || !authoredBagPolicies.TryGetValue(type, out var cached) || !cached.Bags
                || !authoredTagFields.TryGetValue(type.GetType(), out var field)
                || !(field?.GetValue(type) is List<string> tags) || tags.Count > 128) return false;
            bool bags = false;
            for (int i = 0; i < tags.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(tags[i])) return false;
                bags |= tags[i] == "ba:businesstag_customersneedpaperbags";
            }
            return bags;
        }
        internal static bool Supported(BuildingRegistration r) => active != null && active.SupportedMenu(r);
        private bool SupportedBusiness(BuildingRegistration r)
        {
            if (!r.RentedByPlayer || !r.HasEstablishedBusiness || r.AvailableForRent || r.cachedAvailableProducts == null
                || r.cachedAvailableProducts.Count < 1 || r.cachedAvailableProducts.Count > FoodMenuPolicy.LiveMaximumOffers) return false;
            for (int i = 0; i < r.cachedAvailableProducts.Count; i++)
            {
                // Historical live support remains bounded here until the notification contract
                // and schema-25 dynamic execution can be integrated together. No new name list.
                if (!FoodProducts.Known(r.cachedAvailableProducts[i])) return false;
                for (int j = 0; j < i; j++) if (r.cachedAvailableProducts[i] == r.cachedAvailableProducts[j]) return false;
            }
            var type = BusinessTypeHelper.GetData(r.businessTypeName);
            return type?.simulator?.GetType() == typeof(FullServiceBusinessSimulator) && !type.hasEntranceFee
                && authoredBagPolicies.TryGetValue(type, out var bags) && bags.Bags;
        }
        public string ScopeStatus(string placeId)
        {
            if (supplyExclusions.TryGetValue(placeId, out var excluded)) return excluded;
            string menu = MenuStatus(placeId);
            return menu.Length > 0 ? menu : supplyExclusions.TryGetValue(placeId, out var supply) ? supply : "";
        }
        private string MenuStatus(string placeId)
        {
            if (registrations.TryGetValue(placeId, out var dynamicReg) && dynamicReg.cachedAvailableProducts != null)
                for (int i = 0; i < dynamicReg.cachedAvailableProducts.Count; i++)
                    if (!FoodProducts.Known(dynamicReg.cachedAvailableProducts[i])) return DynamicIntegrationRefusal;
            if (!registrations.TryGetValue(placeId, out var reg) || !SupportedMenu(reg)) return "Excluded: unsupported menu or business type";
            var authority = NativeFoodOwnership.Find(reg);
            if (authority == null || !authority.CanServe) return "Waiting for a safe native accounting boundary";
            return "";
        }
        public const string DynamicIntegrationRefusal = "E5b dynamic native integration incomplete: mutation notifications, holder reconciliation and held execution need implementation and RIG-CAT/RIG-SHOPEDIT proof";
        internal static bool IsBuying(BuildingRegistration reg) => active != null && active.buying && ReferenceEquals(active.buyerShop, reg);
        public bool IsConfirmedPaid(ResidentMeal meal) => meal.TargetKind == FoodAlternativeKind.Player && ReferenceEquals(save, SaveGameManager.Current)
            && FoodSaleReceipts.ReadStatus(SaveGameManager.Current.modData, meal) == FoodReceiptStatus.Paid;
        public bool CanServe(string businessId)
        {
            if (!ReferenceEquals(save, SaveGameManager.Current)) return false;
            if(!scopesByBusiness.TryGetValue(businessId,out var scope) || !registrations.TryGetValue(scope.PlaceId,out var reg)) return false;
            var authority=NativeFoodOwnership.Find(reg);
            if(authority?.BusinessId!=businessId) return false;
            if(!SupportedMenu(reg)) { NativeFoodOwnership.Observe(reg,false);return false; }
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
                    && SupportedMenu(reg) && NativeFoodOwnership.Find(reg)?.CanServe == true)
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
            var catalogue = FoodBusinessCatalogue.ReadCompleteMenus(selectedPlaces,
                // A replaced registration can leave historical place keys pointing at one controller.
                // Observe that controller once; distinct controllers with conflicting addresses still refuse.
                selectedPlaces.Where(shopControllers.ContainsKey).Select(p => shopControllers[p]).Distinct(),
                SelectedCompleteMenu,
                r => NativeFoodOwnership.Find(r)?.BusinessId ?? FoodBusinessCatalogue.BusinessIdentity(r),
                (reg, error) =>
                {
                    RefuseContract(reg); NativeFoodOwnership.Observe(reg, false); decisionSupply.Remove(PlaceId(reg));
                    if (observed != null) throw error; // A selected checkout retains its precise definite-refusal reason.
                });
            var result = new List<FoodBusiness>();
            foreach (var b in catalogue.Businesses)
            {
                if (!registrations.TryGetValue(b.PlaceId, out var reg) || MenuStatus(b.PlaceId).Length > 0
                    || !scopesByPlace.TryGetValue(b.PlaceId,out var scope) || scope.BusinessId!=b.Id) continue;
                if (!CurrentAuthoredBagPolicy(reg))
                {
                    supplyExclusions[b.PlaceId] = "Excluded: authored bag policy changed or is unverified";
                    RefuseContract(reg); NativeFoodOwnership.Observe(reg, false); decisionSupply.Remove(b.PlaceId);
                    continue;
                }
                b.Offers.RemoveAll(o => !scope.ContainsProduct(o.ProductId));
                var products = scope.Products().ToArray();
                // Checkout (observed != null) always reads live, exactly as before this cache existed.
                var supply = observed != null || capture ? FoodSupplyReader.ReadMenu(reg, products)
                    : DecisionMenu(observationMinute, b.PlaceId, reg, b.Id, products);
                // A live capture supersedes any older decision image without another native read.
                if ((observed != null || capture) && decisionSupply.TryGetValue(b.PlaceId, out var cachedImage))
                    cachedImage.Image.ObserveLive(supply);
                ObserveJournalSupply(b, scope, supply, checked((int)observationMinute));
                if (supply.Values.Any(s => !s.Units.HasValue))
                {
                    supplyExclusions[b.PlaceId] = "Excluded: menu stock contract is unverified";
                    RefuseContract(reg); NativeFoodOwnership.Observe(reg, false); decisionSupply.Remove(b.PlaceId);
                    continue;
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
        private void ObserveJournalSupply(FoodBusiness b, FoodScope scope, IReadOnlyDictionary<string, FoodSupplySnapshot> supply, int observationMinute)
        {
            if (changeJournal != null)
            {
                int index = 0; bool complete = true;
                foreach (var offer in b.Offers)
                {
                    var image = supply[offer.ProductId]; complete &= image.HasSupplyImage;
                    journalOffers[index++] = new ShopObservedOffer { ProductId = offer.ProductId, PriceUnits = offer.Price.Units, Stock = image.ProducerUnits };
                }
                for (int i = 1; i < index; i++)
                {
                    var value = journalOffers[i]; int j = i - 1;
                    while (j >= 0 && StringComparer.Ordinal.Compare(journalOffers[j].ProductId, value.ProductId) > 0)
                    { journalOffers[j + 1] = journalOffers[j]; j--; }
                    journalOffers[j + 1] = value;
                }
                changeJournal.ObserveSupply(b.Id, b.PlaceId, observationMinute, journalOffers, index, supply[scope.ProductId].StaffedCounters, complete);
            }
        }
        private IReadOnlyCollection<string>? SelectedCompleteMenu(BuildingRegistration reg)
        {
            string place = PlaceId(reg);
            if (!registrations.TryGetValue(place, out var registered) || !ReferenceEquals(reg, registered)) return null;
            if (!SupportedMenu(reg)) { NativeFoodOwnership.Observe(reg, false); return null; }
            if (MenuStatus(place).Length > 0 || !scopesByPlace.TryGetValue(place, out var scope)
                || NativeFoodOwnership.Find(reg)?.BusinessId != scope.BusinessId) return null;
            return scope.Products().ToArray();
        }
        private bool BeginContractRecoveryBoundary()
        {
            long hour = SaveGameManager.Current.Day * 24L + SaveGameManager.Current.Hour;
            if (hour <= lastContractRecoveryHour) return false;
            lastContractRecoveryHour = hour; return true;
        }
        private void RevalidateRefusedContract(BuildingRegistration reg, CityBuildingController controller)
        {
            if (!refusedContracts.TryGetValue(reg, out var refusal) || lastContractRecoveryHour <= refusal.Hour) return;
            var authority = NativeFoodOwnership.Find(reg);
            // Never cancel the requested handoff before its protected work has drained.
            if (authority == null || authority.Managed || authority.OwnsHour(lastContractRecoveryHour)
                || !SupportedBusiness(reg) || reg.cachedAvailableProducts.Count > menuLimit
                // SupportedBusiness rejects unestablished shops; ReadMenu also skips supply inspection when closed.
                || reg.temporarilyClosed
                || !GamePlaces.BuildingAccessible(controller) || !CurrentAuthoredBagPolicy(reg)) return;
            try
            {
                var products = reg.cachedAvailableProducts.ToArray();
                var catalogue = FoodBusinessCatalogue.ReadCompleteMenus(new[] { PlaceId(reg) }, new[] { controller }, r => products);
                if (catalogue.Businesses.Count != 1) return;
                var supply = FoodSupplyReader.ReadMenu(reg, products);
                foreach (var item in supply.Values) if (!item.Units.HasValue) return;
            }
            catch (Exception) { return; } // Unreadable native supply remains native-owned, never known zero.
            refusedContracts.Remove(reg); supplyExclusions.Remove(PlaceId(reg)); decisionSupply.Remove(PlaceId(reg));
            // RefreshKnownOwnership requests eligibility; FoodAuthority still waits a later
            // drained boundary with no live native customers before acquiring service.
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
                if (!ReferenceEquals(save, SaveGameManager.Current) || minute < EarliestMinute || minute >= EarliestMinute + 60
                    || !CanServe(meal.BusinessId))
                    return new FoodSale(false, "Native authority or accounting hour changed during checkout");
                approvedProduct = meal.ProductId;
                // Native callbacks can remove or duplicate the selected row after capture.
                // This is a definite pre-receipt refusal, not an uncertain Single() exception.
                if (reg.retailPrices == null || reg.retailPrices.Count > 128)
                    return RefuseCheckoutContract(reg);
                int selectedPrices = 0;
                foreach (var row in reg.retailPrices) if (row != null && row.itemName == meal.ProductId)
                { selectedPrices++; approvedPrice = row.price; }
                if (selectedPrices != 1) return RefuseCheckoutContract(reg);
                Money livePrice;
                try { livePrice = Money.FromNative(approvedPrice); }
                catch (ArgumentOutOfRangeException)
                {
                    // No receipt or native sale exists yet: this is a definite refusal, not uncertain ownership.
                    RefuseContract(reg); NativeFoodOwnership.Observe(reg, false);
                    return new FoodSale(false, "Approved price is outside the supported range");
                }
                if (livePrice != meal.Price)
                    return new FoodSale(false, "Approved price changed during native setup");
                // Setup/open checks may invoke native callbacks. Check the whole approved menu
                // again, not only the chosen price, before creating a receipt or native order.
                if (!CompletePricesMatch(reg, business!) || !SupportedMenu(reg) || !CurrentAuthoredBagPolicy(reg)
                    || BusinessTypeHelper.GetData(reg.businessTypeName)?.HasTag(BigAmbitions.Tags.TagRef.Businesstag.customersneedpaperbags) != true)
                {
                    return RefuseCheckoutContract(reg);
                }
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
        private FoodSale RefuseCheckoutContract(BuildingRegistration reg)
        {
            RefuseContract(reg); NativeFoodOwnership.Observe(reg, false);
            return new FoodSale(false, "Complete food menu changed before native checkout");
        }
        private static bool CompletePricesMatch(BuildingRegistration reg, FoodBusiness approved)
        {
            if (reg.cachedAvailableProducts == null || reg.retailPrices == null || reg.retailPrices.Count > 128
                || reg.cachedAvailableProducts.Count != approved.Offers.Count) return false;
            foreach (var offer in approved.Offers)
            {
                int advertised = 0, prices = 0;
                foreach (string name in reg.cachedAvailableProducts) if (name == offer.ProductId) advertised++;
                foreach (var price in reg.retailPrices) if (price != null && price.itemName == offer.ProductId)
                {
                    prices++;
                    try { if (Money.FromNative(price.price) != offer.Price) return false; }
                    catch (ArgumentOutOfRangeException) { return false; }
                }
                if (advertised != 1 || prices != 1) return false;
            }
            return true;
        }
        public void Dispose()
        {
            ((object)this as IRivalFoodMarket)?.CloseRivals();
            patches.UnpatchAll(PatchId);
            NativeFoodOwnership.Detach(this);
            authoredBagPolicies = new ConditionalWeakTable<object, BagPolicy>(); authoredTagFields.Clear();
            refusedContracts = new ConditionalWeakTable<BuildingRegistration, ContractRefusal>();
            if (ReferenceEquals(active, this)) active = null;
        }
    }
}
