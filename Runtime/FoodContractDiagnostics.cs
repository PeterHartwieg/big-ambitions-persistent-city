using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Buildings.Retail.Simulation;
using Newtonsoft.Json;
using PersistentCity.Core;
using Debug = UnityEngine.Debug;

namespace PersistentCity.Runtime
{
    /// <summary>E5b2 shadow contract: stamps, hints and the loaded-name image run against today's shops while the
    /// original-three guard and <c>DynamicIntegrationRefusal</c> still decide admission (Decision 13). Nothing here
    /// changes ownership, candidates, captures or checkout. Every failure leaves the shadow idle and is reported.</summary>
    public sealed partial class NativeFoodMarket
    {
        private LoadedCatalogueEligibilityReader? contractReader;
        private FoodContractHintRouter? contractHooks;
        private FoodContractCapabilities contractCapabilities = new FoodContractCapabilities();
        private long contractUnits, contractSlowUnits;
        private double contractUnitMaxMilliseconds;
        private static readonly double ContractMillisecondsPerTimestamp = 1000.0 / Stopwatch.Frequency;

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
                var reader = new LoadedCatalogueEligibilityReader(); contractReader = reader;
                var hooks = FoodContractHintRouter.Install(reader, new LoadedCatalogueLifecycleMembers.HarmonyPatcher(), Environment.CurrentManagedThreadId);
                contractHooks = hooks;
                foreach (var hook in FoodContractHookTable.All) report.Record("hook " + hook.Signature, hooks.Failure(hook.Id));
            }
            catch (Exception e)
            {
                report.Record("shadow start", e.GetType().Name + ": " + e.Message);
                StopContractShadow();
            }
        }

        // Dispose. Unpatches, then drops every native reference the reader holds.
        private void StopContractShadow()
        {
            var hooks = contractHooks; contractHooks = null;
            try { hooks?.Dispose(); } catch (Exception) { }
            var reader = contractReader; contractReader = null;
            try { reader?.Dispose(); } catch (Exception) { }
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
                StopContractShadow();
            }
        }

        // Once per frame after discovery: one bounded reader unit when work is pending; a clean frame reads nothing native.
        private void ContinueContractShadow()
        {
            var reader = contractReader;
            if (reader == null) return;
            contractHooks?.Drain();
            long started = Stopwatch.GetTimestamp();
            bool worked;
            try { worked = reader.Continue(); }
            catch (Exception e)
            {
                contractCapabilities.Record("shadow reader", e.GetType().Name + ": " + e.Message);
                StopContractShadow(); return;
            }
            if (!worked) return;
            double ms = (Stopwatch.GetTimestamp() - started) * ContractMillisecondsPerTimestamp;
            contractUnits++;
            if (ms > contractUnitMaxMilliseconds) contractUnitMaxMilliseconds = ms;
            if (ms >= 30) contractSlowUnits++;
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
                        authority = "none (shadow): original-three guard and DynamicIntegrationRefusal unchanged",
                        capabilitiesPassed = report.AllPassed, firstFailure = report.FirstFailure,
                        stampsReady = LoadedCatalogueEligibilityReader.StampsReady,
                        catalogue = reader == null ? null : new {
                            revision = reader.CatalogueRevision, refusal = reader.CatalogueRefusal.ToString(), count = reader.CatalogueCount,
                            building = reader.CatalogueBuilding, bumps = reader.CatalogueBumps,
                            namesRevision = reader.LoadedNames?.Revision, names = reader.LoadedNames?.Count },
                        shops = reader == null ? null : new {
                            tracked = reader.TrackedShops, pending = reader.PendingShops, stampReads = reader.StampReads,
                            stampMismatches = reader.StampMismatches, dirtyUnits = reader.HintUnits, dirtyCoalesced = reader.HintsCoalesced,
                            passes = reader.ShopPasses, definitionsRead = reader.DefinitionsRead, holdersRead = reader.HoldersRead },
                        hooks = hooks == null ? null : new {
                            installed = hooks.InstalledCount, total = FoodContractHookTable.All.Count, calls = hooks.TotalCalls,
                            newUnits = hooks.TotalNewUnits, hooks.OffThreadCalls, hooks.Faults, hooks.LastFault,
                            generation = FoodContractHintRouter.Generation, hooks.InstalledGeneration },
                        units = new { count = contractUnits, maxMs = Math.Round(contractUnitMaxMilliseconds, 4), atLeast30Ms = contractSlowUnits },
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
                        namesRevision = (names ?? last)?.Revision, namesRefusal = (names ?? last)?.Refusal.ToString(),
                        names = (names ?? last)?.Names };
                default: return null;
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
            if (dump == null) { Debug.LogWarning("[Persistent City] use pc food contract [status|capabilities|hints|stamps|catalogue]"); return; }
            Debug.Log("[Persistent City] food contract " + (view.Length == 0 ? "status" : view) + "\n" + JsonConvert.SerializeObject(dump));
        }
    }
}
