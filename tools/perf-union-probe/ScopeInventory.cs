// Rig-only F4b union probe: the expected Persistent City entry points at this source head.
// check_scopes.py parses the P/M/E/H/C/S rows below and fails when Runtime source and this list differ.
// Unity-free: compiled by Probe and by logic-check. One row per line; keep the argument order.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PerfUnionProbe
{
    public enum ScopeKind { Prefix, Postfix, Finalizer, Transpiler, Message, Event, TranspilerHelper, Component }

    public sealed class ExpectedScope
    {
        public ScopeKind Kind;
        /// <summary>Harmony owner for patches, the event name for events, empty otherwise.</summary>
        public string Owner = "";
        /// <summary>The Persistent City type and method that runs (patch method, message, handler, helper or component).</summary>
        public string Type = "", Method = "";
        /// <summary>The native method a patch targets, the transpiler for a helper, CLR parameter names for a component.</summary>
        public string Target = "";
        public string File = "";
        public string Canonical => Kind + "|" + Owner + "|" + Type + "|" + Method + "|" + Target + "|" + File;
        public string Label => Kind == ScopeKind.Component ? "component:" + Type.Substring(Type.LastIndexOfAny(new[] { '.', '+' }) + 1) + "." + Method
            : Type.Substring(Type.LastIndexOfAny(new[] { '.', '+' }) + 1) + "." + Method;
    }

    /// <summary>A call inside Update that a later slice must not move out of a component or the union unnoticed.</summary>
    public sealed class ExpectedCallSite { public string File = "", Caller = "", Call = ""; }

    public static class ScopeInventory
    {
        public const string Version = "f4b-scope-inventory/1";
        private const string R = "PersistentCity.Runtime.", Core = "PersistentCity.Core.";
        private const string Clock = "hart.persistentcity.cityclock.v1", Observer = "hart.persistentcity.clockobserver.v1",
            Authority = "persistentcity.food-authority", Routines = "persistentcity.food-routines",
            Ambient = "hart.persistentcity.ambientpedestrians.v1", Crowds = "hart.persistentcity.stationarycrowds.v1",
            Inspector = "persistentcity.resident-inspector-input",
            Billing = "persistentcity.research-billing",
            Dialog = "persistentcity.research-dialog", Contract = "persistentcity.food-contract";
        private const string Hooks = R + "LoadedCatalogueLifecycleMembers+Postfixes", ContractFile = "Runtime/LoadedCatalogueLifecycleMembers.cs";

        private static ExpectedScope P(string owner, ScopeKind kind, string type, string method, string target, string file)
            => new ExpectedScope { Kind = kind, Owner = owner, Type = type, Method = method, Target = target, File = file };
        private static ExpectedScope M(string type, string method, string file)
            => new ExpectedScope { Kind = ScopeKind.Message, Type = type, Method = method, File = file };
        private static ExpectedScope E(string evt, string type, string method, string file)
            => new ExpectedScope { Kind = ScopeKind.Event, Owner = evt, Type = type, Method = method, File = file };
        private static ExpectedScope H(string transpiler, string type, string method, string file)
            => new ExpectedScope { Kind = ScopeKind.TranspilerHelper, Type = type, Method = method, Target = transpiler, File = file };
        private static ExpectedScope C(string type, string method, string parameters, string file)
            => new ExpectedScope { Kind = ScopeKind.Component, Type = type, Method = method, Target = parameters, File = file };
        private static ExpectedCallSite S(string file, string caller, string call)
            => new ExpectedCallSite { File = file, Caller = caller, Call = call };

        /// <summary>Every prefix, postfix, finalizer and transpiler Persistent City installs. The first 35 rows are the F4 plan's
        /// section 2 inventory; the food-contract rows were added by E5b2 (#168) and E5b3 (#171) after the plan.</summary>
        public static readonly IReadOnlyList<ExpectedScope> Patches = Array.AsReadOnly(new[]
        {
            P(Clock, ScopeKind.Prefix, R + "NativeCityClock", "TickPrefix", "GameManager.RunMainGameTick", "Runtime/NativeCityClock.cs"),
            P(Clock, ScopeKind.Prefix, R + "NativeCityClock", "AccountingPrefix", "GameManager.RunMainGameTick", "Runtime/NativeCityClock.cs"),
            P(Clock, ScopeKind.Prefix, R + "NativeCityClock", "RidePrefix", "Ride.CapTick", "Runtime/NativeCityClock.cs"),
            P(Clock, ScopeKind.Postfix, R + "NativeCityClock", "RidePostfix", "Ride.CapTick", "Runtime/NativeCityClock.cs"),
            P(Observer, ScopeKind.Postfix, R + "NativeTravelPace", "ObserveTickPostfix", "GameManager.RunMainGameTick", "Runtime/NativeTravelPace.cs"),
            P(Authority, ScopeKind.Postfix, R + "NativeFoodOwnership", "AfterNativeTick", "GameManager.RunMainGameTick", "Runtime/NativeFoodOwnership.cs"),
            P(Authority, ScopeKind.Prefix, R + "NativeFoodOwnership", "HourlyCustomers", "RetailBusinessSimulator.ProcessAllCustomersFromThisHour", "Runtime/NativeFoodOwnership.cs"),
            P(Authority, ScopeKind.Prefix, R + "NativeFoodOwnership", "CustomerSale", "RetailBusinessSimulator.ProcessCustomer", "Runtime/NativeFoodOwnership.cs"),
            P(Authority, ScopeKind.Prefix, R + "NativeFoodOwnership", "CustomerDraw", "CustomerEntriesHelper.GetEntriesByAddress", "Runtime/NativeFoodOwnership.cs"),
            P(Authority, ScopeKind.Prefix, R + "NativeFoodOwnership", "GenerateCustomers", "CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness", "Runtime/NativeFoodOwnership.cs"),
            P(Authority, ScopeKind.Prefix, R + "NativeFoodOwnership", "ResetRegistration", "BuildingRegistration.Reset", "Runtime/NativeFoodOwnership.cs"),
            P(Routines, ScopeKind.Prefix, R + "NativeFoodMarket", "ResidentPrice", "CitizenData.IsPriceAcceptable", "Runtime/NativeFoodMarket.cs"),
            P(Ambient, ScopeKind.Prefix, R + "AmbientPedestrians", "SetActivePrefix", "PedestrianSpawner.SetActivePedestrians", "Runtime/AmbientPedestrians.cs"),
            P(Ambient, ScopeKind.Prefix, R + "AmbientPedestrians", "EndCyclePrefix", "Pedestrian.OnEndCycle", "Runtime/AmbientPedestrians.cs"),
            P(Ambient, ScopeKind.Prefix, R + "AmbientPedestrians", "ChangeStatePrefix", "Pedestrian.ChangeState", "Runtime/AmbientPedestrians.cs"),
            P(Crowds, ScopeKind.Prefix, R + "StationaryCrowds", "SpawnPrefix", "StationaryAiSpawner.Spawn", "Runtime/StationaryCrowds.cs"),
            P(Inspector, ScopeKind.Prefix, R + "ResidentInspector", "MouseRunPrefix", "MouseController.Run", "Runtime/ResidentInspector.cs"),
            P(Inspector, ScopeKind.Prefix, R + "ResidentInspector", "CameraInputPrefix", "PedestrianCam.UpdateInput", "Runtime/ResidentInspector.cs"),
            P(Inspector, ScopeKind.Postfix, R + "ResidentInspector", "CameraInputPostfix", "PedestrianCam.UpdateInput", "Runtime/ResidentInspector.cs"),
            P(Inspector, ScopeKind.Prefix, R + "ResidentInspector", "UiRaycastPrefix", "GraphicRaycaster.Raycast", "Runtime/ResidentInspector.cs"),
            P(Billing, ScopeKind.Postfix, R + "ResearchBilling", "RunDailyPostfix", "MarketingHelper.RunDaily", "Runtime/ResearchBilling.cs"),
            P(Dialog, ScopeKind.Postfix, R + "ResearchAgencyDialog", "CampaignSettingsPostfix", "MarketingAgencyDialog.CampaignSettings", "Runtime/ResearchAgencyDialog.cs"),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook0", "ItemsGetter.OnItemsLoaded", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook1", "ItemsGetter.RegisterModItem", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook2", "ItemsGetter.UnregisterModItem", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook3", "ItemsGetter.ResetStaticData", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook4", "BusinessHelper.UpdateCachedAvailableProducts", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook5", "BuildingRegistration.AddItemInstanceToBuilding", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook6", "BuildingRegistration.RemoveItemInstanceFromBuilding", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook7", "BuildingRegistration.OnBusinessTypeChanged", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook8", "BuildingRegistration.Reset", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook9", "ItemHelper.FillUpShowcaseShelfOrPointOfSale", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook10", "ItemHelper.SubtractFromStock", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook11", "ItemInstance.AddToCargo", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook12", "ItemInstance.RemoveFromCargo", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook13", "ItemInstance.ReduceFromCargo", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook14", "ItemInstance.MergeCargo", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook15", "ItemInstance.MergeCargo", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook16", "CargoInstance.MergeAmount", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook17", "ReStockingHelper.RefillSingleItemWithLimit", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook18", "ReStockingHelper.TryAddStockAmount", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook19", "CargoInstance.ParseIntoItemInstance", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook20", "BuildingRegistration.TemporarilyClose", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook21", "BuildingRegistration.RemoveUnusedRetailPrices", ContractFile),
            P(Contract, ScopeKind.Postfix, Hooks, "Hook22", "CargoInstance.ResetItemCached", ContractFile),
        });

        /// <summary>MonoBehaviour messages on Persistent City components.</summary>
        public static readonly IReadOnlyList<ExpectedScope> Messages = Array.AsReadOnly(new[]
        {
            M(R + "PopulationRuntime", "Update", "Runtime/SaveEpochRuntime.cs"),
            M(R + "PopulationRuntime", "OnGUI", "Runtime/FoodEconomyPanel.cs"),
            M(R + "PopulationRuntime", "OnDestroy", "Runtime/PersistentCityMod.cs"),
            M(R + "PhysicalPedestrianCrosswalk", "OnTriggerEnter", "Runtime/PhysicalPedestrians.cs"),
            M(R + "PhysicalPedestrianCrosswalk", "OnTriggerExit", "Runtime/PhysicalPedestrians.cs"),
        });

        /// <summary>Native event handlers: main-thread entry points outside Update and the Harmony patches.</summary>
        public static readonly IReadOnlyList<ExpectedScope> Events = Array.AsReadOnly(new[]
        {
            E("GlobalEvents.onSaveGame", R + "PopulationRuntime", "Save", "Runtime/SaveEpochRuntime.cs"),
            E("GlobalEvents.onGameUnloaded", R + "PopulationRuntime", "Shutdown", "Runtime/SaveEpochRuntime.cs"),
            E("GlobalEvents.onEnterBuilding", R + "NativeInteriorCityView", "OnBuildingChanged", "Runtime/NativeInteriorCityView.cs"),
            E("GlobalEvents.onExitBuilding", R + "NativeInteriorCityView", "OnBuildingChanged", "Runtime/NativeInteriorCityView.cs"),
            E("GlobalEvents.onTimeMachineStarted", R + "NativeInteriorCityView", "OnTimeMachineStarted", "Runtime/NativeInteriorCityView.cs"),
            E("SceneManager.sceneUnloaded", R + "NativeInteriorCityView", "OnSceneUnloaded", "Runtime/NativeInteriorCityView.cs"),
            E("SceneManager.sceneLoaded", R + "RouteObjectDiscovery", "SceneLoaded", "Runtime/RouteObjectDiscovery.cs"),
            E("SceneManager.sceneUnloaded", R + "RouteObjectDiscovery", "SceneUnloaded", "Runtime/RouteObjectDiscovery.cs"),
        });

        /// <summary>PC methods a transpiler inserts into native code; timed as their own scopes.</summary>
        public static readonly IReadOnlyList<ExpectedScope> TranspilerHelpers = Array.AsReadOnly(new[]
        {
        });

        /// <summary>Attribution sub-scopes inside Update (Decision 5 Components). Never added to the union.</summary>
        public static readonly IReadOnlyList<ExpectedScope> Components = Array.AsReadOnly(new[]
        {
            C(R + "PopulationRuntime", "AdvancePopulation", "Double", "Runtime/PersistentCityMod.cs"),
            C(R + "MultimodalRoutes", "Prepare", "", "Runtime/MultimodalRoutes.cs"),
            C(Core + "CitySimulator", "RefreshOptionalStops", "", "Core/OptionalStopScheduling.cs"),
            C(Core + "CitySimulator", "RefreshTrafficClosures", "", "Core/TrafficJourneys.cs"),
            C(R + "PhysicalPopulation", "Update", "", "Runtime/PhysicalPopulation.cs"),
            C(R + "PopulationRuntime", "UpdateInspector", "", "Runtime/PersistentCityMod.cs"),
            C(R + "NativeFoodMarket", "RefreshDiscovery", "", "Runtime/NativeFoodCandidates.cs"),
            C(R + "NativeFoodMarket", "RefreshRoutineHours", "Double,Boolean", "Runtime/NativeFoodHours.cs"),
            C(R + "StreetFoodVendorMarket", "Prepare", "Int64", "Runtime/StreetFoodVendorMarket.cs"),
            C(R + "PopulationRuntime", "ApplyHoldReplay", "", "Runtime/MovementDiagnostics.cs"),
            C(R + "PhysicalPopulation", "ObservePlayerContact", "Binding,PlayerPose&", "Runtime/PhysicalPopulation.cs"),
        });

        /// <summary>Calls that keep each component inside the timed Update union, including F2e's player-hold sites.</summary>
        public static readonly IReadOnlyList<ExpectedCallSite> CallSites = Array.AsReadOnly(new[]
        {
            S("Runtime/SaveEpochRuntime.cs", "Update", "UpdateCity("),
            S("Runtime/PersistentCityMod.cs", "UpdateCity", "foodMarket?.RefreshDiscovery("),
            S("Runtime/PersistentCityMod.cs", "UpdateCity", "vendorMarket?.Prepare("),
            S("Runtime/PersistentCityMod.cs", "UpdateCity", "foodMarket?.RefreshRoutineHours("),
            S("Runtime/PersistentCityMod.cs", "UpdateCity", "simulation.RefreshOptionalStops("),
            S("Runtime/PersistentCityMod.cs", "UpdateCity", "routes.Prepare("),
            S("Runtime/PersistentCityMod.cs", "UpdateCity", "simulation.RefreshTrafficClosures("),
            S("Runtime/PersistentCityMod.cs", "UpdateCity", "AdvancePopulation("),
            S("Runtime/PersistentCityMod.cs", "UpdateCity", "ApplyHoldReplay("),
            S("Runtime/PersistentCityMod.cs", "UpdateCity", "physical?.Update("),
            S("Runtime/PersistentCityMod.cs", "UpdateCity", "UpdateInspector("),
            S("Runtime/PhysicalPopulation.cs", "Update", "PlayerContact.TryReadPlayer("),
            S("Runtime/PhysicalPopulation.cs", "Update", "ObservePlayerContact("),
        });

        public static IEnumerable<ExpectedScope> All => Patches.Concat(Messages).Concat(Events).Concat(TranspilerHelpers).Concat(Components);

        /// <summary>Owners whose patches belong to Persistent City. Any other owner patching a PC method is foreign.</summary>
        public static IReadOnlyList<string> Owners => Patches.Select(p => p.Owner).Distinct().OrderBy(o => o, StringComparer.Ordinal).ToArray();

        /// <summary>The canonical text whose SHA-256 is the expected-scope list hash. check_scopes.py builds the same text.</summary>
        public static string CanonicalText()
        {
            var lines = new List<string> { Version };
            lines.AddRange(All.Select(s => s.Canonical));
            lines.AddRange(CallSites.Select(c => "CallSite|" + c.File + "|" + c.Caller + "|" + c.Call));
            return string.Join("\n", lines) + "\n";
        }

        public static string Hash()
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(CanonicalText())).Select(b => b.ToString("x2")));
        }
    }
}
