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
    public sealed partial class NativeFoodMarket : IFoodMarket, IFoodTicketMarket, IFoodTicketEvidence, IFoodSaleEvidence, IFoodBusinessAuthority, ILocalFoodMarket, IFoodCandidateMarket, IFoodFailureMarket, IFoodStartupValidation, IDisposable
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
        {
            long hour = SaveGameManager.Current.Day * 24L + SaveGameManager.Current.Hour;
            if (!refusedContracts.TryGetValue(registration, out _)) refusedContracts.Add(registration, new ContractRefusal(hour));
            // E5b3 Decision 7: the coordinator keeps the same first refused hour for its recovery gates.
            if (contractMode != ContractMode.Fallback) contracts.Refuse(PlaceId(registration), hour);
        }
        private readonly Harmony patches = new Harmony(PatchId);
        // F8c (D3): the checkout contract is verified at construction. Refused, the market runs in checkout-off mode: no
        // price prefix, CanServe false for every shop, and each owned shop is observed ineligible so the existing release
        // path hands it back at the next drained accounting boundary. Paid and uncertain receipts are still confirmed.
        // F8h (review Sol-r1-1): the conflict decision waits for the game's city load to finish. Until then checkout uses
        // the member contract for eligibility, sells nothing (CanServe, BuyLive) and installs no price prefix.
        private MethodInfo? process;
        private FoodCheckoutMode checkout;
        private bool checkoutSettled;
        /// <summary>food.checkout.shop refusal; null when the checkout contract is verified.</summary>
        internal string? CheckoutRefusal => checkout.Refusal;
        /// <summary>The once-per-load checkout decision is made (the city load finished).</summary>
        internal bool CheckoutSettled => checkoutSettled;
        /// <summary>Sol-r2-2: run once when a decision deferred past construction is made (the activation lines).</summary>
        internal Action? CheckoutSettledOnce { get; set; }
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
        public NativeFoodMarket(FoodCommerceState state, IReadOnlyList<Place>? places = null, bool enrolling = false, RivalFoodSettings? rivalSettings = null, Action<string>? rivalWarning = null, int maximumOffers = FoodMenuPolicy.LiveMaximumOffers, EconomicLedger? economy = null)
        {
            if (active != null) throw new InvalidOperationException("A food market already owns the native hooks.");
            save = SaveGameManager.Current; this.state = state;
            checkout = NativeFoodContract.StartCheckout(NativeFoodContract.CheckoutForLoad(UI.Load.LoadScene.isLoading, out checkoutSettled), out process);
            if (economy != null) changeJournal = new FoodChangeJournal(state, economy);
            contractEconomy = economy;
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
                if (checkoutSettled) InstallCheckoutPrefix();
                StartContractShadow(); // E5b2 hint hooks and stamps; shadow only, never throws.
            }
            catch { Dispose(); throw; }
        }
        private void InstallCheckoutPrefix() => NativeFoodContract.InstallPricePrefix(checkout, () =>
            patches.Patch(AccessTools.Method(typeof(CitizenData), "IsPriceAcceptable", new[] { typeof(string), typeof(float) }),
                prefix: new HarmonyMethod(typeof(NativeFoodMarket), nameof(ResidentPrice)) { priority = Priority.First }));
        /// <summary>F8h (Sol-r1-1): the first RefreshDiscovery after the city load decides checkout once. A refusal
        /// observes every shop ineligible now, so owned shops go through the existing withdrawal; otherwise the price
        /// prefix is installed as at construction.</summary>
        private void SettleCheckout()
        {
            var decided = NativeFoodContract.CheckoutForLoad(UI.Load.LoadScene.isLoading, out bool settled);
            if (!settled) return;
            checkout = NativeFoodContract.StartCheckout(decided, out process);
            checkoutSettled = true;
            if (checkout.Refusal != null) RefreshKnownOwnership();
            else InstallCheckoutPrefix();
            var settledNow = CheckoutSettledOnce; CheckoutSettledOnce = null;
            settledNow?.Invoke();
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
        // E5b3: also the published contract image plus its stamp (Decision 2); E5b1 rules while the coordinator is not active.
        private bool SupportedMenu(BuildingRegistration r)
        {
            if (!checkout.Eligible || refusedContracts.TryGetValue(r, out _)) return false;
            if (!SupportedBusiness(r)) { RefuseRemovedProduct(r); return false; }
            return r.cachedAvailableProducts.Count <= menuLimit && ContractAdmits(r);
        }

        /// <summary>E5b5: the market's product policy (config, API publication and E5b2's loaded-name image), the same
        /// source new decision contexts read. Set once by the host after composing the markets; unset admits no
        /// dynamic product.</summary>
        internal FoodProductPolicySource? ProductPolicySource { get; set; }
        // Decision 12: dynamic acquisition only while every capability passed at city start and the coordinator is active.
        // Warming and fallback keep the original-three guard.
        private bool DynamicAcquisition => contractMode == ContractMode.Active && contractCapabilities.AllPassed;
        private readonly FoodLivePolicy livePolicy = new FoodLivePolicy();
        // A rebuilding catalogue (no current loaded-name image while the reader runs) is not a removed product.
        private FoodProductPolicy CurrentProductPolicy => livePolicy.Resolve(ProductPolicySource?.Current ?? FoodProductPolicy.OriginalThree,
            contractReader != null && contractReader.LoadedNames == null);
        // Image admission (CompleteFoodMenu.Check) classes products with the same resolver, also while warming, so a
        // dynamic shop has a passing image when the coordinator turns active.
        private FoodProductMetadataResolver AdmissionResolver
            => contractCapabilities.AllPassed ? CurrentProductPolicy.Resolver : FoodProductMetadataResolver.Empty;
        // An all-original menu never reads the policy.
        private FoodLiveMenuRefusal LiveMenu(List<string>? menu)
        {
            if (menu == null) return FoodLiveMenuRefusal.Empty;
            bool original = true;
            for (int i = 0; i < menu.Count && original; i++) original = FoodProducts.Known(menu[i]);
            return FoodMenuPolicy.LiveMenu(menu, original || !DynamicAcquisition ? null : CurrentProductPolicy);
        }
        // Startup: from construction until the coordinator first leaves warming (shadow not started yet, or warming).
        private bool StartupPhase => contractMode == ContractMode.Warming
            || contractMode == ContractMode.Fallback && contractFallback == ShadowNotStarted;
        // E5b5: a shop the saved city already owns keeps its ownership and desire through startup, so a reload does not
        // withdraw it while images warm. It sells nothing until its contract image is validated: it is not a capture
        // candidate, checkout refuses it, and Core holds its travelling or serving meals (IFoodStartupValidation). Its
        // names are checked against the policy once loaded names exist. New acquisitions stay guarded, and a failed
        // capability ends startup (Fallback), which withdraws the shop as Decision 12 requires.
        private FoodLiveMenuRefusal LiveMenu(BuildingRegistration r)
        {
            var menu = LiveMenu(r.cachedAvailableProducts);
            if (!StartupHeld(r, menu)) return menu;
            return FoodMenuPolicy.LiveMenu(r.cachedAvailableProducts, CurrentProductPolicy, loadedNamesPending: true);
        }
        private bool StartupHeld(BuildingRegistration r, FoodLiveMenuRefusal menu)
            => menu == FoodLiveMenuRefusal.OriginalThreeGuard && StartupPhase && NativeFoodOwnership.Find(r)?.Managed == true;
        private bool StartupHeld(BuildingRegistration r) => StartupHeld(r, LiveMenu(r.cachedAvailableProducts));
        // F8h (review Sol-r2-1): while checkout waits for the city load, every scoped shop awaits validation, so Core holds
        // its saved commitments (neither bought nor missed) until the decision serves or withdraws it.
        public bool AwaitingValidation(string businessId)
            => ReferenceEquals(save, SaveGameManager.Current) && scopesByBusiness.TryGetValue(businessId, out var scope)
                && (!checkoutSettled || registrations.TryGetValue(scope.PlaceId, out var reg) && StartupHeld(reg));
        public const string StartupValidationRefusal = "Waiting: saved food contract is validated after load; no sale before";
        // E5b5: a mod removed (or a product reclassed) under an owned or wanted shop is a recorded refusal, so desire is
        // withdrawn now, the hand-back waits for the boundary, and a return goes through Decision 7 recovery with a
        // fresh image before any reacquisition. History and receipts are untouched.
        private void RefuseRemovedProduct(BuildingRegistration r)
        {
            if (!DynamicAcquisition || !contractKeys.TryGetValue(r, out var key)) return;
            var row = NativeFoodOwnership.Row(key);
            if (row == null || !row.Managed && !row.Desired) return;
            var menu = LiveMenu(r.cachedAvailableProducts);
            if (menu != FoodLiveMenuRefusal.NotLoaded && menu != FoodLiveMenuRefusal.MealClass) return;
            contractLastRefusal = "menu: " + menu; RefuseContract(r);
        }
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
            if (!r.RentedByPlayer || !r.HasEstablishedBusiness || r.AvailableForRent) return false;
            // E5b5: width, duplicates and the name rule (Decision 12); the published image and CompleteFoodMenu.Check
            // decide the rest through ContractAdmits.
            if (LiveMenu(r) != FoodLiveMenuRefusal.None) return false;
            var type = BusinessTypeHelper.GetData(r.businessTypeName);
            return type?.simulator?.GetType() == typeof(FullServiceBusinessSimulator) && !type.hasEntranceFee
                && authoredBagPolicies.TryGetValue(type, out var bags) && bags.Bags;
        }
        public string ScopeStatus(string placeId)
        {
            if (checkout.ScopeReason is string off) return off;
            if (supplyExclusions.TryGetValue(placeId, out var excluded)) return excluded;
            string menu = MenuStatus(placeId);
            return menu.Length > 0 ? menu : supplyExclusions.TryGetValue(placeId, out var supply) ? supply : "";
        }
        private string MenuStatus(string placeId)
        {
            if (registrations.TryGetValue(placeId, out var menuReg) && menuReg.cachedAvailableProducts != null)
            {
                // Held at startup: no read, capture or sale until the image is validated (ownership kept).
                if (StartupHeld(menuReg)) return StartupValidationRefusal;
                var menu = LiveMenu(menuReg);
                if (menu == FoodLiveMenuRefusal.OriginalThreeGuard)
                    return contractMode == ContractMode.Warming ? "Waiting: food contract warming; non-original products wait for every shop's first image"
                        : DynamicIntegrationRefusal + ": " + DynamicRefusalReason;
                if (menu != FoodLiveMenuRefusal.None) return "Excluded: " + MenuRefusalText(menu);
            }
            if (!registrations.TryGetValue(placeId, out var reg) || !SupportedMenu(reg)) return "Excluded: unsupported menu or business type";
            var authority = NativeFoodOwnership.Find(reg);
            if (authority == null || !authority.CanServe) return "Waiting for a safe native accounting boundary";
            return "";
        }
        /// <summary>E5b5 (Decision 12): shown only when a runtime capability failed or the coordinator fell back; the reason
        /// follows. <c>pc food contract capabilities</c> lists every capability.</summary>
        public const string DynamicIntegrationRefusal = "E5b dynamic native acquisition off; original three only";
        private string DynamicRefusalReason => !contractCapabilities.AllPassed ? "capability failed: " + contractCapabilities.FirstFailure
            : contractFallback.Length > 0 ? contractFallback : "coordinator " + contractMode;
        private static string MenuRefusalText(FoodLiveMenuRefusal refusal) => refusal switch
        {
            FoodLiveMenuRefusal.Empty => "empty menu",
            FoodLiveMenuRefusal.Width => "more than eight products",
            FoodLiveMenuRefusal.Duplicate => "duplicate menu product",
            FoodLiveMenuRefusal.Name => "malformed product name",
            FoodLiveMenuRefusal.NotLoaded => "menu product not in the loaded catalogue",
            FoodLiveMenuRefusal.MealClass => "menu product is not classed Food",
            _ => refusal.ToString()
        };
        internal static bool IsBuying(BuildingRegistration reg) => active != null && active.buying && ReferenceEquals(active.buyerShop, reg);
        // F8c (review Sol-r5-1): the shared reader the unit tests execute; checkout-off does not change it.
        public bool IsConfirmedPaid(ResidentMeal meal)
        {
            var current = SaveGameManager.Current;
            return FoodReceiptEvidence.IsConfirmedPaid(ReferenceEquals(save, current), current?.modData, meal);
        }
        public bool TryConfirmedTicket(ResidentMeal meal, out Money total, out IReadOnlyList<FoodTicketLine> extras)
        {
            var current = SaveGameManager.Current;
            return FoodReceiptEvidence.TryConfirmedTicket(ReferenceEquals(save, current), current?.modData, meal, out total, out extras);
        }
        public bool CanServe(string businessId)
        {
            if (!ReferenceEquals(save, SaveGameManager.Current) || !checkoutSettled || !checkout.Serves) return false;
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
                // E5b3 Decision 3: a selected capture or checkout rechecks the published image before any supply read.
                if ((observed != null || capture) && !ContractRecheck(reg, observed != null))
                {
                    supplyExclusions[b.PlaceId] = ContractCatalogueWaiting ? "Waiting: food catalogue rebuilding"
                        : "Excluded: complete food contract changed since its published image";
                    continue;
                }
                b.Offers.RemoveAll(o => !scope.ContainsProduct(o.ProductId));
                var products = scope.Products().ToArray();
                // Checkout (observed != null) always reads live, exactly as before this cache existed.
                // E5b3 Decision 5: every pre-sale read reconciles native effective holders with the image.
                var supply = observed != null || capture ? FoodSupplyReader.ReadMenu(reg, products, ContractImage(reg))
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
            var supply = FoodSupplyReader.ReadMenu(reg, products, ContractImage(reg));
            decisionSupply[placeId] = new DecisionSupply(reg, businessId, products, supply);
            return supply;
        }
        public FoodSale Buy(ResidentMeal meal, double minute) => BuyChecked(meal, minute, null);
        /// <summary>F6j: every scoped player shop sells extras as further one-product native orders in the same call.</summary>
        public bool SellsExtras(string businessId) => scopesByBusiness.ContainsKey(businessId);
        public FoodSale BuyTicket(ResidentMeal meal, FoodMealTicket ticket, double minute) => BuyChecked(meal, minute, ticket);
        private FoodSale BuyChecked(ResidentMeal meal, double minute, FoodMealTicket? ticket)
        {
            if (meal.TargetKind != FoodAlternativeKind.Player) return new FoodSale(false, "Rivals have no native checkout authority");
            if (ticket != null && (ticket.BusinessId != meal.BusinessId || ticket.Main.ProductId != meal.ProductId || ticket.Total != meal.Price))
                return new FoodSale(false, "Ticket differs from the committed meal");
            // Any checkout attempt may change this shop's native stock, bags or orders.
            try { return BuyLive(meal, minute, ticket); }
            finally { decisionSupply.Remove(meal.PlaceId); }
        }
        // With a ticket, meal.Price is the planned total and the main line is sold at ticket.Main.Price.
        private FoodSale BuyLive(ResidentMeal meal, double minute, FoodMealTicket? ticket)
        {
            if (!checkoutSettled) return new FoodSale(false, "Food checkout waits for the city load to finish"); // before any receipt
            if (minute < EarliestMinute || minute >= EarliestMinute + 60 || !ReferenceEquals(save, SaveGameManager.Current))
                return new FoodSale(false, "Native clock differs from service time");
            var data = SaveGameManager.Current.modData;
            Money mainPrice = ticket?.Main.Price ?? meal.Price;
            if (ticket == null ? FoodSaleReceipts.AlreadyPaid(data, meal) : FoodSaleReceipts.AlreadyPaid(data, meal, ticket.Extras))
                return new FoodSale(true, "Native saved receipt already confirms this intention", null, ticket?.Extras);
            if (receiptDay != SaveGameManager.Current.Day)
            {
                receiptDay = SaveGameManager.Current.Day;
                FoodSaleReceipts.Prune(data, receiptDay, state.Residents
                    .Where(m => m.Status == MealStatus.Uncertain).Select(m => m.IntentId));
            }
            if (AwaitingValidation(meal.BusinessId)) return new FoodSale(false, StartupValidationRefusal); // Core never asks here
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
            if (offer != null && offer.Price != mainPrice) { CheckoutPriceOnlyRefusals++; return new FoodSale(false, "Approved price changed", priceChanged: true); }
            var reg = registrations[meal.PlaceId];
            if (!BusinessHelper.IsBusinessOpen(reg)) return new FoodSale(false, "Shop closed before service", ShopExperience.Closed);
            // A zero sellable-unit count can also mean no staffed counter. Name stockout only
            // when this same live read proves empty product stock or empty bags at a staffed counter.
            if (offer != null && offer.Units == 0 && observed.TryGetValue(meal.PlaceId, out var liveSupply)
                && liveSupply.Supply.TryGetValue(meal.ProductId, out var supply) && supply.Units == 0
                && (supply.ProducerUnits == 0 || supply.StaffedCounters > 0 && supply.BagUnits == 0))
                return new FoodSale(false, "Confirmed stockout before service", ShopExperience.Stockout);
            // GR-3: food is priced on the purchase contract's checkout. The main is mandatory and each planned extra optional,
            // against the allowance the meal was held within; extras over it are dropped from the end, a main over it refuses.
            var quote = PurchaseContract.Quote(PurchaseContract.FoodBasket(meal.ProductId, mainPrice, ticket?.Extras), meal.Allowance);
            if (offer == null || !offer.Units.HasValue || offer.Units <= 0 || !business!.MealsPerHour.HasValue || business.MealsPerHour <= 0
                || offer.Price != mainPrice || quote.Refusal != PurchaseRefusal.None
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
            else
            {
                long reconciled = FoodSupplyReader.Reconciliations;
                before = FoodSupplyReader.ReadMenu(reg, products, ContractImage(reg));
                // Decision 5: a reconciliation mismatch in this read withdraws the whole business, before any receipt.
                if (FoodSupplyReader.Reconciliations != reconciled) return RefuseCheckoutContract(reg);
            }
            if (before.Values.Any(s => !s.Units.HasValue)) return new FoodSale(false, "Menu supply contract changed");
            var refusal = SellLine(meal, minute, reg, business!, scope, meal.ProductId, mainPrice, before,
                () => FoodSaleReceipts.Begin(data, meal, ticket?.Extras), out before);
            if (refusal.HasValue) return refusal.Value;
            // F6j: each extra is one more one-product order with the same checks, sold only after the main
            // line is confirmed. The first refusal stops the extras; an uncertain line throws (no retry).
            var sold = new List<FoodTicketLine>();
            if (ticket != null)
                foreach (var extra in PurchaseContract.KeptExtras(quote, ticket.Extras))
                {
                    var extraOffer = business!.Offers.SingleOrDefault(o => o.ProductId == extra.ProductId);
                    if (extraOffer == null || extraOffer.Price != extra.Price || !before.TryGetValue(extra.ProductId, out var extraSupply)
                        || !(extraSupply.Units > 0) || ScopeStatus(meal.PlaceId).Length > 0 || !BusinessHelper.IsBusinessOpen(reg)) break;
                    if (SellLine(meal, minute, reg, business!, scope, extra.ProductId, extra.Price, before, null, out before).HasValue) break;
                    sold.Add(extra);
                }
            if (ticket == null) FoodSaleReceipts.Confirm(data, meal);
            else FoodSaleReceipts.Confirm(data, meal, mainPrice, ticket.Extras, sold);
            NativePurchases += 1 + sold.Count;
            return new FoodSale(true, sold.Count == 0 ? "Native product, bag and completed order confirmed; daily settlement remains native"
                : "Native products, bags and completed orders confirmed; daily settlement remains native", null, sold);
        }
        /// <summary>One product as one native customer order and bag. Null after a confirmed sale; a refusal before
        /// any receipt or native effect; throws when the native outcome is uncertain. <paramref name="beginReceipt"/>
        /// runs immediately before the native order (the main line); extras share the receipt already begun.</summary>
        private FoodSale? SellLine(ResidentMeal meal, double minute, BuildingRegistration reg, FoodBusiness business, FoodScope scope,
            string product, Money price, IReadOnlyDictionary<string, FoodSupplySnapshot> before, Action? beginReceipt,
            out IReadOnlyDictionary<string, FoodSupplySnapshot> after)
        {
            after = before;
            int orders = reg.unprocessedCompletedOrders.Count;
            var customer = new CustomerEntry { spawnTime = TimeHelper.Now(), order = new Order {
                entries = new List<OrderEntry> { new OrderEntry { itemName = product } } } };
            var simulator = ScriptableObject.CreateInstance<FullServiceBusinessSimulator>();
            try
            {
                simulator.SetUp(reg, SaveGameManager.Current.Hour);
                if (!ReferenceEquals(save, SaveGameManager.Current) || minute < EarliestMinute || minute >= EarliestMinute + 60
                    || !CanServe(meal.BusinessId))
                    return new FoodSale(false, "Native authority or accounting hour changed during checkout");
                approvedProduct = product;
                // Native callbacks can remove or duplicate the selected row after capture.
                // This is a definite pre-receipt refusal, not an uncertain Single() exception.
                if (reg.retailPrices == null || reg.retailPrices.Count > 128)
                    return RefuseCheckoutContract(reg);
                int selectedPrices = 0;
                foreach (var row in reg.retailPrices) if (row != null && row.itemName == product)
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
                if (livePrice != price) { CheckoutPriceOnlyRefusals++; return new FoodSale(false, PriceOnlyRefusal, priceChanged: true); }
                // Setup/open checks may invoke native callbacks. Check the whole approved menu
                // again, not only the chosen price, before creating a receipt or native order.
                // A catalogue that started rebuilding during setup cannot vouch for the image: this sale waits.
                if (ContractCatalogueWaiting) return new FoodSale(false, "Food catalogue rebuilding; this sale waits");
                // E5b3 Decision 3: the published image's holders, definitions and price rows, after native setup.
                var completePrices = CompletePrices(reg, business);
                if (completePrices == FoodPriceVerdict.Refused || !SupportedMenu(reg) || !CurrentAuthoredBagPolicy(reg)
                    || BusinessTypeHelper.GetData(reg.businessTypeName)?.HasTag(BigAmbitions.Tags.TagRef.Businesstag.customersneedpaperbags) != true
                    || !ContractRecheck(reg, true))
                {
                    return RefuseCheckoutContract(reg);
                }
                // E5b5 Q2 default: another valid price is a price-only change. Ownership is kept and this sale is refused
                // before any receipt and Core lets the resident decide again at the counter. Never a sale on changed terms.
                if (completePrices == FoodPriceVerdict.PriceOnly) { CheckoutPriceOnlyRefusals++; return new FoodSale(false, PriceOnlyRefusal, priceChanged: true); }
                if (process == null) return RefuseCheckoutContract(reg); // never after a receipt
                beginReceipt?.Invoke();
                buyerShop = reg; buying = true;
                process.Invoke(simulator, new object[] { customer });
            }
            finally { buying = false; buyerShop = null; UnityEngine.Object.Destroy(simulator); }
            var entries = customer.order.entries.Where(e => e.paid && ItemsGetter.GetByName(e.itemName)?.HasTag(BigAmbitions.Tags.TagRef.Itemtag.isbag) != true).ToArray();
            var image = FoodSupplyReader.ReadMenu(reg, scope.Products());
            if (entries.Length != 1 || entries[0].itemName != product || entries[0].price != approvedPrice
                || !customer.order.completed || image[product].ProducerUnits != before[product].ProducerUnits - 1
                || image[product].BagUnits != before[product].BagUnits - 1
                || scope.Products().Any(p => p != product && image[p].ProducerUnits != before[p].ProducerUnits)
                || reg.unprocessedCompletedOrders.Count != orders + 1)
                throw new InvalidOperationException("Native sale outcome is uncertain; do not retry this intention.");
            after = image;
            return null;
        }
        private FoodSale RefuseCheckoutContract(BuildingRegistration reg)
        {
            RefuseContract(reg); NativeFoodOwnership.Observe(reg, false);
            return new FoodSale(false, "Complete food menu changed before native checkout");
        }
        /// <summary>Checkouts refused because a menu price changed to another valid price (Q2); ownership was kept.</summary>
        public long CheckoutPriceOnlyRefusals { get; private set; }
        public const string PriceOnlyRefusal = "Approved price changed during native setup; ownership kept";
        // E5b5 Q2: a menu that is still one row per product with an exact valid price is PriceOnly when a price differs
        // from the approved terms; a missing, duplicated or invalid row, or a different menu, is Refused (hand-back).
        private static FoodPriceVerdict CompletePrices(BuildingRegistration reg, FoodBusiness approved)
        {
            if (reg.cachedAvailableProducts == null || reg.retailPrices == null || reg.retailPrices.Count > 128
                || reg.cachedAvailableProducts.Count != approved.Offers.Count) return FoodPriceVerdict.Refused;
            var verdict = FoodPriceVerdict.Same;
            foreach (var offer in approved.Offers)
            {
                int advertised = 0, prices = 0;
                foreach (string name in reg.cachedAvailableProducts) if (name == offer.ProductId) advertised++;
                foreach (var price in reg.retailPrices) if (price != null && price.itemName == offer.ProductId)
                {
                    prices++;
                    if (!Money.TryFromNative(price.price, out var live)) return FoodPriceVerdict.Refused;
                    if (live != offer.Price) verdict = FoodPriceVerdict.PriceOnly;
                }
                if (advertised != 1 || prices != 1) return FoodPriceVerdict.Refused;
            }
            return verdict;
        }
        public void Dispose()
        {
            StopContractShadow(); // First: guarded, so it can neither be skipped nor abort the rest.
            ((object)this as IRivalFoodMarket)?.CloseRivals();
            patches.UnpatchAll(PatchId);
            NativeFoodOwnership.Detach(this);
            authoredBagPolicies = new ConditionalWeakTable<object, BagPolicy>(); authoredTagFields.Clear();
            refusedContracts = new ConditionalWeakTable<BuildingRegistration, ContractRefusal>();
            if (ReferenceEquals(active, this)) active = null;
        }
    }
}
