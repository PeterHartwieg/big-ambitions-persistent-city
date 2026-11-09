using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BigAmbitions.Items;
using Buildings.Retail.Simulation;
using Helpers;
using Newtonsoft.Json;
using PersistentCity.Core;
using Debug = UnityEngine.Debug;

namespace PersistentCity.Runtime
{
    /// <summary>E5b2 shadow reader plus the E5b3 contract coordinator (Decisions 3, 3a, 4-8, 13). The original-three guard
    /// in <c>SupportedBusiness</c> and <c>DynamicIntegrationRefusal</c> still decide which products are admitted; the
    /// coordinator only makes original-three shops stricter: published images, rechecks, reconciliation, the boundary
    /// commit gate and dynamic recovery. When the capabilities fail, the shadow faults or the catalogue is refused, the
    /// market runs on the E5b1 contract unchanged (<see cref="ContractMode.Fallback"/>) and the status says why.</summary>
    public sealed partial class NativeFoodMarket
    {
        private LoadedCatalogueEligibilityReader? contractReader;
        private FoodContractHintRouter? contractHooks;
        private FoodContractCapabilities contractCapabilities = new FoodContractCapabilities();
        private long contractUnits, contractSlowUnits;
        private double contractUnitMaxMilliseconds;
        private static readonly double ContractMillisecondsPerTimestamp = 1000.0 / Stopwatch.Frequency;

        internal enum ContractMode { Fallback, Warming, Active }
        private ContractMode contractMode = ContractMode.Fallback;
        private string contractFallback = "shadow not started";
        private readonly FoodContractCoordinator contracts = new FoodContractCoordinator();
        // Capture batches, checkouts, recoveries and boundaries each compare a distinct definition once.
        private readonly FoodDefinitionEpoch contractEpoch = new FoodDefinitionEpoch();
        private readonly FoodDefinitionEpoch preparationEpoch = new FoodDefinitionEpoch();
        private readonly FoodDefinitionEpoch boundaryEpoch = new FoodDefinitionEpoch();
        private readonly NativeContractLive contractLive = new NativeContractLive();
        private sealed class RegistrationComparer : IEqualityComparer<BuildingRegistration>
        {
            internal static readonly RegistrationComparer Instance = new RegistrationComparer();
            public bool Equals(BuildingRegistration? a, BuildingRegistration? b) => ReferenceEquals(a, b);
            public int GetHashCode(BuildingRegistration value) => RuntimeHelpers.GetHashCode(value);
        }
        // Place key of each registration discovery holds, so SupportedMenu reads the image without building a place ID.
        private readonly Dictionary<BuildingRegistration, string> contractKeys = new Dictionary<BuildingRegistration, string>(RegistrationComparer.Instance);
        private readonly HashSet<string> contractWarming = new HashSet<string>(StringComparer.Ordinal);
        private FoodReferencedNames contractNames = new FoodReferencedNames();
        private readonly EconomicLedger? contractEconomy;
        private readonly List<string> contractHistory = new List<string>(8);
        private readonly List<string> contractRecoveries = new List<string>(FoodContractCoordinator.RecoveriesPerFrame);
        private readonly List<FoodDefinitionDependency> contractChanged = new List<FoodDefinitionDependency>();
        private readonly List<Item> contractRecopy = new List<Item>();
        private readonly List<string> contractNamesBuffer = new List<string>(16);
        private readonly Money[] contractPrices = new Money[FoodMenuPolicy.MaximumOffers];
        private Func<FoodAuthority, FoodContractImage, bool>? contractGate;
        private static CollectionVersionReader<List<CargoInstance>>? cargoVersion;
        private const double ContractFrameBudgetMilliseconds = 0.5;
        private const int ContractUnitsPerFrame = 64, PreparationsPerFrame = 64;
        // Counters for pc food contract status/boundary and the perf record.
        private long contractRechecks, contractRecheckRefusals, contractGateRuns, contractGateRefusals, contractRecoveryUnits;
        private long contractDiscards, contractRecopyEvents, contractBusinessRecopies, contractFrames, contractFramesOverBudget;
        private double contractRecheckMax, contractGateMax, contractFrameMax, contractBoundaryCommitMs, contractBoundaryCommitMax;
        private string? contractLastRefusal;
        private readonly double[] contractUnitSamples = new double[1024];
        private int contractUnitSampleCount;

        // Constructor, after the existing price patch. Never throws.
        private void StartContractShadow()
        {
            var report = new FoodContractCapabilities(); contractCapabilities = report;
            try
            {
                LoadedCatalogueEligibilityReader.Prepare(report);
                string? discovery = null;
                try { LoadedCatalogueLifecycleMembers.Discover(); } catch (Exception e) { discovery = e.Message; }
                report.Record("E5a lifecycle member discovery (19 exact signatures)", discovery);
                const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
                report.Record("RetailBusinessSimulator.availableProducers bound",
                    typeof(RetailBusinessSimulator).GetField("availableProducers", hidden) == null ? "field missing" : null);
                report.Record("RetailBusinessSimulator.pointOfSales bound",
                    typeof(RetailBusinessSimulator).GetField("pointOfSales", hidden) == null ? "field missing" : null);
                // E5b3 Decision 3: holder cargo lists by reference, version and count.
                cargoVersion = CollectionVersionReader<List<CargoInstance>>.Bind(out var cargoFailure);
                report.Record("version reader cargoInstances List<CargoInstance>._version int", cargoVersion == null ? cargoFailure : null);
                report.Record("self-test List<CargoInstance> add, set, remove", FoodContractSelfTest.List<CargoInstance>(cargoVersion, null!, null!));
                var reader = new LoadedCatalogueEligibilityReader(); contractReader = reader;
                var hooks = FoodContractHintRouter.Install(reader, new LoadedCatalogueLifecycleMembers.HarmonyPatcher(), Environment.CurrentManagedThreadId);
                contractHooks = hooks;
                foreach (var hook in FoodContractHookTable.All) report.Record("hook " + hook.Signature, hooks.Failure(hook.Id));
                if (!report.AllPassed) { contractFallback = "capability failed: " + report.FirstFailure; return; }
                contractNames = FoodReferencedNames.From(state, contractEconomy);
                contractGate = CommitGate;
                reader.ShopPublished = OnContractPublished;
                // Seed every shop discovery already holds; owned shops serve on the E5b1 contract until all have an image.
                contractMode = ContractMode.Warming; contractFallback = "";
                foreach (var pair in registrations)
                {
                    IndexContractKey(pair.Key, pair.Value, null);
                    if (reader.MarkShopDirty(pair.Key, pair.Value, menuLimit)) contractWarming.Add(pair.Key);
                }
            }
            catch (Exception e)
            {
                report.Record("shadow start", e.GetType().Name + ": " + e.Message);
                StopContractShadow();
            }
        }

        // Dispose. Unpatches, then drops every native reference the reader and the coordinator hold.
        private void StopContractShadow(string reason = "shadow stopped")
        {
            var hooks = contractHooks; contractHooks = null;
            try { hooks?.Dispose(); } catch (Exception) { }
            var reader = contractReader; contractReader = null;
            if (reader != null) reader.ShopPublished = null;
            try { reader?.Dispose(); } catch (Exception) { }
            contractMode = ContractMode.Fallback; contractFallback = reason;
            contracts.Clear(); contractWarming.Clear(); contractKeys.Clear();
            contractEpoch.Clear(); preparationEpoch.Clear(); boundaryEpoch.Clear(); contractLive.Bind(null);
        }

        // Existing discovery visit, once per visited registration. A fault stops the shadow, never discovery.
        private void VisitContract(string place, BuildingRegistration registration)
        {
            var reader = contractReader;
            if (reader == null) return;
            try { reader.Visit(place, registration, menuLimit); }
            catch (Exception e)
            {
                contractCapabilities.Record("shadow visit", e.GetType().Name + ": " + e.Message);
                StopContractShadow("shadow visit faulted");
            }
        }

        // Once per frame after discovery. Preparation, recovery and reader units share one 0.5 ms budget; a clean
        // frame reads nothing native.
        private void ContinueContractShadow()
        {
            var reader = contractReader;
            if (reader == null) return;
            contractHooks?.Drain();
            long frameStart = Stopwatch.GetTimestamp();
            bool any = false;
            try
            {
                if (contractMode != ContractMode.Fallback)
                {
                    for (int i = 0; i < PreparationsPerFrame && Elapsed(frameStart) < ContractFrameBudgetMilliseconds; i++)
                    {
                        if (!contracts.TryNextPrepare(out var key)) break;
                        any = true; PrepareUnit(reader, key);
                    }
                    if (contractMode == ContractMode.Active && contracts.PendingRecoveries > 0)
                    {
                        long hour = AccountingHour();
                        contracts.TakeRecoveries(hour, contractRecoveries);
                        foreach (var key in contractRecoveries)
                        {
                            any = true; long started = Stopwatch.GetTimestamp();
                            RecoverContract(key, hour); contractRecoveryUnits++;
                            RecordUnit(Elapsed(started));
                            if (Elapsed(frameStart) >= ContractFrameBudgetMilliseconds) break;
                        }
                    }
                }
                for (int i = 0; i < ContractUnitsPerFrame; i++)
                {
                    long started = Stopwatch.GetTimestamp();
                    if (!reader.Continue()) break;
                    any = true; RecordUnit(Elapsed(started));
                    if (Elapsed(frameStart) >= ContractFrameBudgetMilliseconds) break;
                }
                if (contractMode == ContractMode.Warming) FinishWarming(reader);
            }
            catch (Exception e)
            {
                contractCapabilities.Record("shadow reader", e.GetType().Name + ": " + e.Message);
                StopContractShadow("shadow reader faulted"); return;
            }
            if (!any) return;
            double frame = Elapsed(frameStart);
            contractFrames++;
            if (frame > contractFrameMax) contractFrameMax = frame;
            if (frame > ContractFrameBudgetMilliseconds) contractFramesOverBudget++;
        }
        private static double Elapsed(long started) => (Stopwatch.GetTimestamp() - started) * ContractMillisecondsPerTimestamp;
        private void RecordUnit(double ms)
        {
            contractUnits++;
            if (ms > contractUnitMaxMilliseconds) contractUnitMaxMilliseconds = ms;
            if (ms >= 30) contractSlowUnits++;
            contractUnitSamples[contractUnitSampleCount++ % contractUnitSamples.Length] = ms;
        }
        private static long AccountingHour() => SaveGameManager.Current.Day * 24L + SaveGameManager.Current.Hour;

        // Warming ends when the catalogue is published and every seeded shop has an image. A refused catalogue keeps E5b1.
        private void FinishWarming(LoadedCatalogueEligibilityReader reader)
        {
            if (reader.Catalogue == null) return;
            if (reader.CatalogueRefusal != FoodEligibilityRefusal.None)
            { contractMode = ContractMode.Fallback; contractFallback = "catalogue refused: " + reader.CatalogueRefusal; return; }
            if (contractWarming.Count > 0) return;
            contractMode = ContractMode.Active;
            // Shops whose first image failed while warming are withdrawn now, exactly as a failed rebuild would be.
            foreach (var key in contracts.Keys.ToArray())
            {
                var image = contracts.Image(key);
                if (image != null && !image.Passed && registrations.TryGetValue(key, out var reg)) WithdrawFailedImage(reg);
            }
        }

        // Decision 4 step 1 for one queued shop: Decision 3a first, then the E5a shop pass.
        private void PrepareUnit(LoadedCatalogueEligibilityReader reader, string key)
        {
            long started = Stopwatch.GetTimestamp();
            if (!registrations.TryGetValue(key, out var reg)) { RecordUnit(Elapsed(started)); return; }
            var image = contracts.Image(key);
            if (image != null)
            {
                contractLive.Bind(reg); contractChanged.Clear();
                var refusal = FoodContractRecheck.Definitions(image, contractLive, preparationEpoch, contractChanged);
                if (refusal == FoodContractRecheckRefusal.None) refusal = FoodContractRecheck.BusinessType(image, contractLive);
                if (refusal != FoodContractRecheckRefusal.None)
                {
                    Recopy(reg, refusal);
                    if (contractMode == ContractMode.Active) RefuseContractImage(reg, "preparation: " + refusal);
                }
            }
            reader.MarkShopDirty(key, reg, menuLimit);
            RecordUnit(Elapsed(started));
        }

        // Decision 4 step 3. Runs inside the reader unit that finished the pass. Never partial: the image is built
        // whole and swapped in one call, or discarded.
        private void OnContractPublished(string key, BuildingRegistration reg, FoodEligibilityImage eligibility, ShopContractStamp stamp)
        {
            var reader = contractReader;
            if (reader == null || contractMode == ContractMode.Fallback) return;
            if (!registrations.TryGetValue(key, out var current) || !ReferenceEquals(current, reg)) return;
            // A write since the pass started: the reader's next visit sees the same mismatch and rebuilds.
            if (!LoadedCatalogueEligibilityReader.CurrentStamp(reg).Matches(stamp)) { contractDiscards++; return; }
            var catalogue = reader.Catalogue;
            if (catalogue == null || catalogue.Revision != eligibility.CatalogueRevision) { contractDiscards++; return; }
            long hour = AccountingHour();
            contractLive.Bind(reg);
            var holders = new FoodHolderSlot[eligibility.Holders.Count];
            for (int i = 0; i < holders.Length; i++)
            {
                var h = eligibility.Holders[i];
                var expected = new FoodHolderSlot(h.HolderId, h.DefinitionId, h.ResourceId, h.IsBagCounter, null, null, 0, 0, null, null, 0);
                if (!contractLive.ReadHolder(expected, out holders[i]) || !holders[i].Shaped
                    || !string.Equals(holders[i].DefinitionId, h.DefinitionId, StringComparison.Ordinal))
                { contractDiscards++; reader.MarkShopDirty(key, reg, menuLimit); return; }
            }
            FoodDefinitionDependency[] definitions = Array.Empty<FoodDefinitionDependency>();
            FoodBusinessTypeCopy? business = null;
            var admission = CompleteFoodMenuRefusal.None;
            if (eligibility.StockContractMatched)
            {
                contractNamesBuffer.Clear();
                foreach (var offer in eligibility.Offers) AddName(offer.ProductId);
                foreach (var h in eligibility.Holders) { AddName(h.DefinitionId); AddName(h.ResourceId); }
                definitions = new FoodDefinitionDependency[contractNamesBuffer.Count];
                contractRecopy.Clear();
                for (int i = 0; i < definitions.Length; i++)
                {
                    string name = contractNamesBuffer[i];
                    var item = ItemsGetter.GetByName(name, true);
                    if (item == null || !catalogue.TryGet(name, out var copy)) { contractDiscards++; reader.MarkShopDirty(key, reg, menuLimit); return; }
                    if (!copy.Matches(LoadedCatalogueEligibilityReader.Facts(item))) contractRecopy.Add(item);
                    definitions[i] = new FoodDefinitionDependency(name, item, copy);
                }
                // In-place definition edit during the pass: re-copy and rebuild against the fresh facts.
                if (contractRecopy.Count > 0) { contractDiscards++; contractRecopyEvents++; reader.RecopyDefinitions(contractRecopy.ToArray()); return; }
                if (contractLive.ReadBusinessType(out var type)) business = new FoodBusinessTypeCopy(type);
                admission = Admission(key, reg, eligibility, eligibility.CatalogueRevision == reader.CatalogueRevision);
            }
            var image = new FoodContractImage(key, hour, eligibility, holders, definitions, business, admission, stamp);
            contracts.Publish(image); contractWarming.Remove(key);
            if (!image.Passed && contractMode == ContractMode.Active) WithdrawFailedImage(reg);
        }
        private void AddName(string name) { if (!contractNamesBuffer.Contains(name)) contractNamesBuffer.Add(name); }

        // Decision 8. nativeContract is true only because Decisions 2, 3 and 3a just passed for this image; Decision 5
        // runs at every supply read that follows and refuses the shop there.
        private CompleteFoodMenuRefusal Admission(string key, BuildingRegistration reg, FoodEligibilityImage eligibility, bool currentRevision)
        {
            string business = NativeFoodOwnership.Row(key)?.BusinessId ?? FoodBusinessCatalogue.BusinessIdentity(reg);
            CompleteFoodMenu.HistoryProducts(state.History, key, business, SaveGameManager.Current.Day, contractHistory);
            return CompleteFoodMenu.Check(eligibility, FoodProductMetadataResolver.Empty, currentRevision, true, contractHistory, contractNames.Names);
        }
        private void WithdrawFailedImage(BuildingRegistration reg)
        {
            var authority = NativeFoodOwnership.Find(reg);
            if (authority == null || !authority.Managed && !authority.Desired) return;
            RefuseContract(reg); NativeFoodOwnership.Observe(reg, false);
        }
        private void RefuseContractImage(BuildingRegistration reg, string reason)
        {
            contractLastRefusal = reason;
            RefuseContract(reg); NativeFoodOwnership.Observe(reg, false); decisionSupply.Remove(PlaceId(reg));
        }
        private void Recopy(BuildingRegistration reg, FoodContractRecheckRefusal refusal)
        {
            var reader = contractReader;
            if (reader == null) return;
            if (contractChanged.Count > 0)
            {
                contractRecopy.Clear();
                foreach (var d in contractChanged) if (d.Live is Item item && !contractRecopy.Contains(item)) contractRecopy.Add(item);
                contractRecopyEvents++; reader.RecopyDefinitions(contractRecopy.ToArray());
            }
            else if (refusal == FoodContractRecheckRefusal.DefinitionReference) { contractRecopyEvents++; reader.MarkCatalogueDirty(); }
            if (refusal == FoodContractRecheckRefusal.BusinessType)
            {
                // Re-copy the authored bag policy and rebuild every shop against the live type.
                var type = BusinessTypeHelper.GetData(reg.businessTypeName);
                if (type != null) { authoredBagPolicies.Remove(type); CacheBagPolicy(reg); }
                contractBusinessRecopies++; reader.RequeueAllShops();
            }
        }

        // Decision 2, read by SupportedMenu: the published image plus the current stamp. E5b1 rules outside Active.
        private bool ContractAdmits(BuildingRegistration r)
        {
            if (contractMode != ContractMode.Active) return true;
            if (!contractKeys.TryGetValue(r, out var key) || !registrations.TryGetValue(key, out var current) || !ReferenceEquals(current, r))
                return false;
            var image = contracts.Serving(key);
            if (image == null) return false;
            return contracts.Admit(key, image.Stamp is ShopContractStamp stamp && LoadedCatalogueEligibilityReader.CurrentStamp(r).Matches(stamp));
        }
        private FoodContractImage? ContractImage(BuildingRegistration r)
            => contractMode == ContractMode.Active && contractKeys.TryGetValue(r, out var key) ? contracts.Serving(key) : null;
        private void IndexContractKey(string place, BuildingRegistration reg, BuildingRegistration? previous)
        {
            if (previous != null && !ReferenceEquals(previous, reg)) contractKeys.Remove(previous);
            contractKeys[reg] = place;
        }

        // Decision 3 at a selected capture (checkout=false, inside its batch epoch) or after native setup at checkout.
        private bool ContractRecheck(BuildingRegistration reg, bool checkout)
        {
            if (contractMode != ContractMode.Active) return true;
            var image = ContractImage(reg);
            if (image == null) return false;
            if (checkout) contractEpoch.Begin();
            long started = Stopwatch.GetTimestamp();
            contractRechecks++;
            contractLive.Bind(reg); contractChanged.Clear();
            var refusal = FoodContractRecheck.Structure(image, contractLive, contractEpoch, contractChanged);
            var prices = refusal == FoodContractRecheckRefusal.None ? FoodContractRecheck.Prices(image, contractLive, contractPrices) : FoodPriceVerdict.Same;
            double ms = Elapsed(started);
            if (ms > contractRecheckMax) contractRecheckMax = ms;
            if (refusal == FoodContractRecheckRefusal.None && prices != FoodPriceVerdict.Refused)
            {
                if (prices == FoodPriceVerdict.PriceOnly) RecordPrices(image);
                return true;
            }
            contractRecheckRefusals++;
            if (refusal != FoodContractRecheckRefusal.None) Recopy(reg, refusal);
            RefuseContractImage(reg, (checkout ? "checkout: " : "capture: ") + (refusal != FoodContractRecheckRefusal.None ? refusal.ToString() : "Price"));
            return false;
        }
        // Q2 price-only change: a new image with the live prices, swapped atomically. Checkout still compares the
        // captured price exactly (E5b1), so a change between capture and checkout refuses there.
        private void RecordPrices(FoodContractImage image)
        {
            var next = new Money[image.Prices.Count];
            Array.Copy(contractPrices, next, next.Length);
            contracts.Replace(image, image.WithPrices(next));
        }

        // Called by NativeFoodOwnership.AfterNativeTick once per drained boundary, before its rows.
        internal void BeginContractCommit() { boundaryEpoch.Begin(); contractBoundaryCommitMs = 0; }
        internal FoodContractBoundary.Outcome CommitBoundary(FoodAuthority row, long hour, bool liveNativeCustomers)
        {
            var coordinator = contractMode == ContractMode.Fallback ? null : contracts;
            long started = Stopwatch.GetTimestamp();
            var outcome = FoodContractBoundary.Commit(row, coordinator, hour, liveNativeCustomers, true,
                contractReader?.CatalogueRevision ?? -1, contractGate ??= CommitGate);
            double ms = Elapsed(started);
            contractBoundaryCommitMs += ms;
            if (contractBoundaryCommitMs > contractBoundaryCommitMax) contractBoundaryCommitMax = contractBoundaryCommitMs;
            return outcome;
        }
        // Decision 4 step 4: one last synchronous gate after the drain, for an acquisition that is ready and in budget.
        private bool CommitGate(FoodAuthority row, FoodContractImage image)
        {
            long started = Stopwatch.GetTimestamp();
            contractGateRuns++;
            try
            {
                if (contractMode != ContractMode.Active || !registrations.TryGetValue(row.PlaceId, out var reg)) return false;
                // A stamp change between drain and commit blocks the transfer (Decision 2 withdraws the desire).
                if (!(image.Stamp is ShopContractStamp stamp) || !LoadedCatalogueEligibilityReader.CurrentStamp(reg).Matches(stamp))
                {
                    contracts.Invalidate(row.PlaceId); NativeFoodOwnership.Observe(reg, false);
                    contractGateRefusals++; contractLastRefusal = "commit: stamp"; return false;
                }
                contractLive.Bind(reg); contractChanged.Clear();
                var refusal = FoodContractRecheck.Structure(image, contractLive, boundaryEpoch, contractChanged);
                var prices = refusal == FoodContractRecheckRefusal.None ? FoodContractRecheck.Prices(image, contractLive, contractPrices) : FoodPriceVerdict.Same;
                var admission = refusal == FoodContractRecheckRefusal.None && prices != FoodPriceVerdict.Refused
                    ? Admission(row.PlaceId, reg, image.Eligibility, image.CatalogueRevision == contractReader?.CatalogueRevision)
                    : CompleteFoodMenuRefusal.None;
                if (refusal == FoodContractRecheckRefusal.None && prices != FoodPriceVerdict.Refused && admission == CompleteFoodMenuRefusal.None
                    && SupportedBusiness(reg) && !refusedContracts.TryGetValue(reg, out _))
                {
                    if (prices == FoodPriceVerdict.PriceOnly) RecordPrices(image);
                    return true;
                }
                contractGateRefusals++;
                if (refusal != FoodContractRecheckRefusal.None) Recopy(reg, refusal);
                RefuseContractImage(reg, "commit: " + (refusal != FoodContractRecheckRefusal.None ? refusal.ToString()
                    : prices == FoodPriceVerdict.Refused ? "Price" : admission != CompleteFoodMenuRefusal.None ? admission.ToString() : "Menu"));
                return false;
            }
            finally
            {
                double ms = Elapsed(started);
                if (ms > contractGateMax) contractGateMax = ms;
            }
        }

        // Decision 4 step 1 at an advancing hour: owned, acquiring and refused shops are rebuilt during the hour.
        private void PrepareContractHour(string place, BuildingRegistration reg, FoodAuthority authority)
        {
            if (contractMode == ContractMode.Fallback) return;
            bool refused = refusedContracts.TryGetValue(reg, out _);
            if (!authority.Managed && !authority.Desired && !refused) return;
            contracts.Prepare(place);
            if (refused) contracts.RequeueRecovery(place);
        }

        // Decision 7: one recovery unit. Same rules as RevalidateRefusedContract, any contract, with a fresh image.
        private void RecoverContract(string key, long hour)
        {
            if (!registrations.TryGetValue(key, out var reg) || !shopControllers.TryGetValue(key, out var controller)) return;
            if (!refusedContracts.TryGetValue(reg, out var refusal) || hour <= refusal.Hour) return;
            var authority = NativeFoodOwnership.Find(reg);
            bool handoff = authority != null && !authority.Managed && !authority.OwnsHour(hour);
            if (contracts.RecoveryGate(key, hour, handoff, NativeFoodOwnership.LiveNativeCustomers(reg)) != FoodRecoveryRefusal.None) return;
            var image = contracts.Image(key)!;
            if (!SupportedBusiness(reg) || reg.cachedAvailableProducts.Count > menuLimit || reg.temporarilyClosed
                || !GamePlaces.BuildingAccessible(controller) || !CurrentAuthoredBagPolicy(reg)) return;
            if (!(image.Stamp is ShopContractStamp stamp) || !LoadedCatalogueEligibilityReader.CurrentStamp(reg).Matches(stamp)) return;
            contractEpoch.Begin(); contractLive.Bind(reg);
            if (FoodContractRecheck.Structure(image, contractLive, contractEpoch) != FoodContractRecheckRefusal.None
                || FoodContractRecheck.Prices(image, contractLive) == FoodPriceVerdict.Refused) return;
            try
            {
                var products = reg.cachedAvailableProducts.ToArray();
                var catalogue = FoodBusinessCatalogue.ReadCompleteMenus(new[] { key }, new[] { controller }, r => products);
                if (catalogue.Businesses.Count != 1) return;
                var supply = FoodSupplyReader.ReadMenu(reg, products, image);
                foreach (var item in supply.Values) if (!item.Units.HasValue) return;
            }
            catch (Exception) { return; } // Unreadable native supply remains native-owned, never known zero.
            refusedContracts.Remove(reg); supplyExclusions.Remove(key); decisionSupply.Remove(key);
            contracts.Recovered(key);
            // RefreshKnownOwnership requests eligibility; the acquisition waits a later drained boundary and the commit gate.
        }

        /// <summary>Detached diagnostic view for <c>pc food contract</c>; null for an unknown view.</summary>
        internal object? ContractDiagnostics(string view)
        {
            var reader = contractReader; var hooks = contractHooks; var report = contractCapabilities;
            switch (view)
            {
                case "":
                case "status":
                    return new {
                        shadow = reader != null,
                        authority = contractMode == ContractMode.Active
                            ? "coordinator (E5b3): original-three guard and DynamicIntegrationRefusal unchanged; images, rechecks, reconciliation, commit gate and recovery active"
                            : contractMode == ContractMode.Warming ? "warming: E5b1 contract until every seeded shop has an image; acquisitions deferred"
                            : "E5b1 fallback: " + contractFallback,
                        capabilitiesPassed = report.AllPassed, firstFailure = report.FirstFailure,
                        stampsReady = LoadedCatalogueEligibilityReader.StampsReady,
                        catalogue = reader == null ? null : new {
                            revision = reader.CatalogueRevision, refusal = reader.CatalogueRefusal.ToString(), count = reader.CatalogueCount,
                            building = reader.CatalogueBuilding, bumps = reader.CatalogueBumps,
                            recopied = reader.DefinitionRecopies, fullRecopies = reader.FullRecopies,
                            namesRevision = reader.LoadedNames?.Revision, names = reader.LoadedNames?.Count },
                        shops = reader == null ? null : new {
                            tracked = reader.TrackedShops, pending = reader.PendingShops, stampReads = reader.StampReads,
                            stampMismatches = reader.StampMismatches, dirtyUnits = reader.HintUnits, dirtyCoalesced = reader.HintsCoalesced,
                            passes = reader.ShopPasses, definitionsRead = reader.DefinitionsRead, holdersRead = reader.HoldersRead },
                        coordinator = new {
                            mode = contractMode.ToString(), images = contracts.Count, warming = contractWarming.Count,
                            preparing = contracts.PendingPreparations, recovering = contracts.PendingRecoveries,
                            contracts.Publishes, contracts.Invalidations, contracts.Refusals, contracts.Recoveries, contracts.Deferrals, contracts.Commits,
                            discards = contractDiscards, recopyEvents = contractRecopyEvents, businessRecopies = contractBusinessRecopies,
                            rechecks = contractRechecks, recheckRefusals = contractRecheckRefusals, recheckMaxMs = Math.Round(contractRecheckMax, 4),
                            gateRuns = contractGateRuns, gateRefusals = contractGateRefusals, gateMaxMs = Math.Round(contractGateMax, 4),
                            boundaryCommitMaxMs = Math.Round(contractBoundaryCommitMax, 4), recoveryUnits = contractRecoveryUnits,
                            reconciliationRefusals = FoodSupplyReader.Reconciliations, lastReconciliation = FoodSupplyReader.LastReconciliation.ToString(),
                            lastRefusal = contractLastRefusal, definitionsCompared = contractEpoch.Compared + preparationEpoch.Compared + boundaryEpoch.Compared,
                            definitionsShared = contractEpoch.Shared + preparationEpoch.Shared + boundaryEpoch.Shared,
                            nativeSetups = FoodSupplyReader.Setups },
                        hooks = hooks == null ? null : new {
                            installed = hooks.InstalledCount, total = FoodContractHookTable.All.Count, calls = hooks.TotalCalls,
                            newUnits = hooks.TotalNewUnits, hooks.OffThreadCalls, hooks.Faults, hooks.LastFault,
                            generation = FoodContractHintRouter.Generation, hooks.InstalledGeneration },
                        units = new { count = contractUnits, p95Ms = Math.Round(UnitP95(), 4), maxMs = Math.Round(contractUnitMaxMilliseconds, 4),
                            atLeast30Ms = contractSlowUnits, frames = contractFrames, frameMaxMs = Math.Round(contractFrameMax, 4),
                            framesOverBudget = contractFramesOverBudget },
                    };
                case "capabilities":
                    return new { passed = report.AllPassed, firstFailure = report.FirstFailure,
                        entries = report.Entries.Select(e => new { capability = e.Key, passed = e.Value == null, failure = e.Value }) };
                case "hints":
                    return new {
                        installed = hooks != null, generation = FoodContractHintRouter.Generation,
                        offThread = hooks?.OffThreadCalls ?? 0, faults = hooks?.Faults ?? 0,
                        hooks = FoodContractHookTable.All.Select(h => new { h.Writer, signature = h.Signature, kind = h.Kind.ToString(),
                            failure = hooks?.Failure(h.Id), calls = hooks?.Calls(h.Id) ?? 0, newUnits = hooks?.NewUnits(h.Id) ?? 0,
                            coalesced = hooks?.CoalescedHints(h.Id) ?? 0, unknown = hooks?.UnknownHints(h.Id) ?? 0 }) };
                case "stamps":
                    if (reader == null) return new { shadow = false };
                    return new { stampsReady = LoadedCatalogueEligibilityReader.StampsReady, catalogueRevision = reader.CatalogueRevision,
                        shops = reader.ShopKeys.OrderBy(k => k, StringComparer.Ordinal).Select(key => {
                            bool image = reader.TryGet(key, out var published);
                            bool stamped = reader.TryStamp(key, out var recorded, out var current);
                            return new { place = key, image, refusal = image ? published.Refusal.ToString() : null,
                                offers = image ? published.Offers.Count : 0, published = stamped ? recorded.ToString() : null,
                                current = stamped ? current.ToString() : null, matches = stamped && current.Matches(recorded) };
                        }).ToArray() };
                case "catalogue":
                    if (reader == null) return new { shadow = false };
                    var names = reader.LoadedNames; var last = reader.LastLoadedNames;
                    return new { revision = reader.CatalogueRevision, refusal = reader.CatalogueRefusal.ToString(), count = reader.CatalogueCount,
                        building = reader.CatalogueBuilding, bumps = reader.CatalogueBumps, current = names != null,
                        recopied = reader.DefinitionRecopies, fullRecopies = reader.FullRecopies,
                        namesRevision = (names ?? last)?.Revision, namesRefusal = (names ?? last)?.Refusal.ToString(),
                        names = (names ?? last)?.Names };
                case "boundary":
                    long hour = AccountingHour(); long revision = reader?.CatalogueRevision ?? -1;
                    return new {
                        mode = contractMode.ToString(), hour, nextBoundary = hour + 1, catalogueRevision = revision,
                        commitsPerBoundary = FoodContractCoordinator.CommitsPerBoundary, recoveriesPerFrame = FoodContractCoordinator.RecoveriesPerFrame,
                        preparing = contracts.PendingPreparations, recovering = contracts.PendingRecoveries,
                        shops = contracts.Keys.OrderBy(k => k, StringComparer.Ordinal).Select(key => {
                            var row = NativeFoodOwnership.Row(key); var image = contracts.Image(key);
                            var last = contracts.LastReadiness(key, out long at);
                            string readiness = row == null ? "no authority" : !row.Desired ? (row.Managed ? "hand back" : "native")
                                : row.Managed && !row.Suspended ? "owned" : contracts.Ready(key, hour + 1, row.RequestedHour, revision).ToString();
                            return new { place = key, phase = contracts.Phase(key).ToString(), preparedHour = image?.BuiltHour,
                                passed = image?.Passed, refusal = image == null ? null : image.Passed ? null
                                    : image.Eligibility.Refusal != FoodEligibilityRefusal.None ? image.Eligibility.Refusal.ToString() : image.Admission.ToString(),
                                refusedHour = contracts.RefusedHour(key), nextBoundary = readiness, deferred = row?.Deferred,
                                desired = row?.Desired, requestedHour = row?.RequestedHour, managed = row?.Managed,
                                lastBoundary = at < 0 ? (long?)null : at, lastReadiness = at < 0 ? null : last.ToString() };
                        }).ToArray() };
                default: return null;
            }
        }
        private double UnitP95()
        {
            int n = Math.Min(contractUnitSampleCount, contractUnitSamples.Length);
            if (n == 0) return 0;
            var copy = new double[n]; Array.Copy(contractUnitSamples, copy, n); Array.Sort(copy);
            return copy[Math.Min(n - 1, (int)Math.Ceiling(n * 0.95) - 1)];
        }

        /// <summary>Main-thread live reads for the rechecks, bound to one registration at a time. No allocation.</summary>
        private sealed class NativeContractLive : IFoodContractLive
        {
            private BuildingRegistration? reg;
            internal void Bind(BuildingRegistration? registration) => reg = registration;
            public bool ReadHolder(in FoodHolderSlot image, out FoodHolderSlot live)
            {
                live = default;
                var instances = reg?.itemInstances;
                if (instances == null || !instances.TryGetValue(image.HolderId, out var instance) || instance == null) return false;
                var cargo = instance.cargoInstances;
                int count = cargo?.Count ?? 0;
                var stock = count > 0 ? cargo![0] : null;
                int version = cargo != null && cargoVersion != null ? cargoVersion.Read(cargo) : 0;
                live = new FoodHolderSlot(image.HolderId, instance.itemName, image.ResourceId, image.IsBagCounter, instance, cargo,
                    version, count, stock, stock?.itemName, stock == null ? 0 : stock.nestedCargoInstances?.Count ?? -1);
                return true;
            }
            public bool ReadDefinition(string name, out object? live, out LoadedStockFacts facts)
            {
                var item = ItemsGetter.GetByName(name, true);
                live = item; facts = item == null ? default : LoadedCatalogueEligibilityReader.Facts(item);
                return item != null;
            }
            public bool ReadBusinessType(out FoodBusinessTypeFacts live)
            {
                var type = reg == null ? null : BusinessTypeHelper.GetData(reg.businessTypeName);
                live = type == null ? default : new FoodBusinessTypeFacts(type, type.simulator?.GetType(), type.hasEntranceFee,
                    LoadedCatalogueEligibilityReader.RawTags(type));
                return type != null;
            }
            public int PriceRows(string product, out float price)
            {
                price = 0; int rows = 0;
                var prices = reg?.retailPrices;
                if (prices == null || prices.Count > LoadedStockLimits.Rows) return -1;
                for (int i = 0; i < prices.Count; i++)
                {
                    var row = prices[i];
                    if (row != null && string.Equals(row.itemName, product, StringComparison.Ordinal)) { rows++; price = row.price; }
                }
                return rows;
            }
        }
    }

    public sealed partial class PopulationRuntime
    {
        private void FoodContractCommand(string view)
        {
            var save = SaveGameManager.Current;
            if (!VendorDiagnosticCity(save?.SaveGameName)) { Debug.LogWarning("[Persistent City] food contract diagnostics require an FT city."); return; }
            if (failed || simulation == null || save == null || !ReferenceEquals(saveIdentity, save) || foodMarket == null)
            { Debug.LogWarning("[Persistent City] food contract diagnostics unavailable: no active city."); return; }
            var dump = foodMarket.ContractDiagnostics(view);
            if (dump == null) { Debug.LogWarning("[Persistent City] use pc food contract [status|capabilities|hints|stamps|catalogue|boundary]"); return; }
            Debug.Log("[Persistent City] food contract " + (view.Length == 0 ? "status" : view) + "\n" + JsonConvert.SerializeObject(dump));
        }
    }
}
