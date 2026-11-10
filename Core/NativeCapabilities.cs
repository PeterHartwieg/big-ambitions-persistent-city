using System;
using System.Collections.Generic;
using System.Text;

namespace PersistentCity.Core
{
    // F8a (docs/plans/f8-capability-boundaries.md, decisions D1 and D2): one capability model for every native hook,
    // reflected native contract and companion-mod contract, plus the static Harmony hook inventory. Not wired to Runtime.

    /// <summary>The D2 capabilities, in report order.</summary>
    public enum CapabilityId : byte
    {
        FoodAuthority, FoodCheckoutShop, FoodDynamic, VendorStreetFood, ClockObserve, ClockCity, ClockDrivenRides,
        RloDonor, RloReservations, RloRoadSpeeds, ResearchDialog, ResearchBilling, ResearchDelivery,
        PhysicalVehicles, PhysicalPedestrians, InspectorClick, InspectorScroll, InspectorUiBlock, InteriorView,
    }

    /// <summary>RequiredWhenPresent: a companion mod whose contract must hold if it is loaded; absent is fine.</summary>
    public enum CapabilityRequirement : byte { Required, Optional, RequiredWhenPresent }

    /// <summary>D1 states. Off always carries a reason; Absent is only for capabilities backed by a companion mod.</summary>
    public enum CapabilityState : byte { NotChecked, Active, Off, Absent }

    /// <summary>The only point at which an Off or Absent capability may be checked again (D1: Off is sticky for the session).</summary>
    public enum CapabilityRecheck : byte { CityLoad, CompanionRevision }

    /// <summary>The features a capability disables when it is not active. A fixed list per capability.</summary>
    [Flags]
    public enum CapabilityScope : uint
    {
        None = 0,
        WholeCity = 1 << 0,
        ShopOwnership = 1 << 1,
        DynamicShops = 1 << 2,
        VehicleScopes = 1 << 3,
        MarketResearch = 1 << 4,
        VisibleBodies = 1 << 5,
        RouteTiming = 1 << 6,
        InspectorClick = 1 << 7,
        InspectorScroll = 1 << 8,
        InspectorUiBlock = 1 << 9,
        InteriorView = 1 << 10,
    }

    /// <summary>Applied changed the registry; Unchanged was a legal no-op (Off stays Off with its first reason);
    /// Rejected broke a D1 transition rule and changed nothing.</summary>
    public enum CapabilityTransition : byte { Applied, Unchanged, Rejected }

    public sealed class CapabilityDefinition
    {
        internal CapabilityDefinition(CapabilityId id, string name, CapabilityRequirement requirement, CapabilityScope scope,
            CapabilityRecheck recheck, string? companion, string nativeContract, string fallback, bool offByDefault = false)
        {
            Id = id; Name = name; Requirement = requirement; Scope = scope; Recheck = recheck; Companion = companion;
            NativeContract = nativeContract; Fallback = fallback; OffByDefault = offByDefault;
        }
        public CapabilityId Id { get; }
        /// <summary>The stable report ID, such as <c>food.authority</c>.</summary>
        public string Name { get; }
        public CapabilityRequirement Requirement { get; }
        public CapabilityScope Scope { get; }
        public CapabilityRecheck Recheck { get; }
        /// <summary>The companion mod this capability binds, or null for a native game contract (which cannot be Absent).</summary>
        public string? Companion { get; }
        public string NativeContract { get; }
        public string Fallback { get; }
        /// <summary>Off unless a later slice or a setting turns it on (D2: food.dynamic, interior.view).</summary>
        public bool OffByDefault { get; }
    }

    public static class NativeCapabilities
    {
        private const CapabilityScope Research = CapabilityScope.MarketResearch;
        private const string ResearchFallback = "Research hidden; orders kept.";

        /// <summary>D2, indexed by <see cref="CapabilityId"/>.</summary>
        public static readonly IReadOnlyList<CapabilityDefinition> All = Array.AsReadOnly(new[]
        {
            new CapabilityDefinition(CapabilityId.FoodAuthority, "food.authority", CapabilityRequirement.Required, CapabilityScope.WholeCity,
                CapabilityRecheck.CityLoad, null, "food-authority hooks (H1)",
                "Not started; native customers serve every shop; payload and manifest unchanged."),
            new CapabilityDefinition(CapabilityId.FoodCheckoutShop, "food.checkout.shop", CapabilityRequirement.Optional, CapabilityScope.ShopOwnership,
                CapabilityRecheck.CityLoad, null, "food-routines IsPriceAcceptable prefix (H2), ProcessCustomer and FoodSupplyReader members",
                "Shops stay native-owned with a scope reason."),
            new CapabilityDefinition(CapabilityId.FoodDynamic, "food.dynamic", CapabilityRequirement.Optional, CapabilityScope.DynamicShops,
                CapabilityRecheck.CityLoad, null, "E5a lifecycle members and the E5b2 food-contract hint postfixes (shadow)",
                "DynamicIntegrationRefusal; the E5b remainder owns turning it on.", offByDefault: true),
            new CapabilityDefinition(CapabilityId.VendorStreetFood, "vendor.streetfood", CapabilityRequirement.Optional, CapabilityScope.VehicleScopes,
                CapabilityRecheck.CompanionRevision, "Street Food (FoodTrucks)", "Street Food VendorApi contract 1",
                "Street Food keeps its own customers."),
            new CapabilityDefinition(CapabilityId.ClockObserve, "clock.observe", CapabilityRequirement.Required, CapabilityScope.WholeCity,
                CapabilityRecheck.CityLoad, null, "clockobserver RunMainGameTick(float) postfix (H4)", "Not started."),
            new CapabilityDefinition(CapabilityId.ClockCity, "clock.city", CapabilityRequirement.Required, CapabilityScope.WholeCity,
                CapabilityRecheck.CityLoad, null, "cityclock RunMainGameTick(float) prefixes (H3)", "Not started."),
            new CapabilityDefinition(CapabilityId.ClockDrivenRides, "clock.drivenrides", CapabilityRequirement.RequiredWhenPresent, CapabilityScope.WholeCity,
                CapabilityRecheck.CityLoad, "Driven Rides (DrivenRides.Ride)", "DrivenRides.Ride.CapTick(ref float) prefix and postfix (H3)",
                "Not started, with a status naming Driven Rides."),
            new CapabilityDefinition(CapabilityId.RloDonor, "rlo.donor", CapabilityRequirement.Required, CapabilityScope.WholeCity,
                CapabilityRecheck.CityLoad, "Real Life Overhaul (RealLifeOverhaul)", "RLO CityApi contract 1 or the reviewed reflection fallback",
                "Not started; the saved city is kept unchanged."),
            new CapabilityDefinition(CapabilityId.RloReservations, "rlo.reservations", CapabilityRequirement.Optional, CapabilityScope.VisibleBodies,
                CapabilityRecheck.CityLoad, "Real Life Overhaul (RealLifeOverhaul)", "RLO CityApi reservation reads",
                "Bodies off; the background city runs."),
            new CapabilityDefinition(CapabilityId.RloRoadSpeeds, "rlo.roadspeeds", CapabilityRequirement.Optional, CapabilityScope.RouteTiming,
                CapabilityRecheck.CityLoad, "Real Life Overhaul (RealLifeOverhaul)", "RLO CityApi road speed reads",
                "Road-speed capture refused; F8d records the effect on route preparation."),
            new CapabilityDefinition(CapabilityId.ResearchDialog, "research.dialog", CapabilityRequirement.Optional, Research,
                CapabilityRecheck.CityLoad, null, "research-dialog CampaignSettings() postfix (H6)", ResearchFallback),
            new CapabilityDefinition(CapabilityId.ResearchBilling, "research.billing", CapabilityRequirement.Optional, Research,
                CapabilityRecheck.CityLoad, null, "research-billing RunDaily() postfix (H5) and its billing contract", ResearchFallback),
            new CapabilityDefinition(CapabilityId.ResearchDelivery, "research.delivery", CapabilityRequirement.Optional, Research,
                CapabilityRecheck.CityLoad, null, "Contact message members", ResearchFallback),
            new CapabilityDefinition(CapabilityId.PhysicalVehicles, "physical.vehicles", CapabilityRequirement.Optional, CapabilityScope.VisibleBodies,
                CapabilityRecheck.CityLoad, null, "physicalvehicles and physicalintersections hooks (H7, H8)",
                "Native traffic; background city unchanged."),
            new CapabilityDefinition(CapabilityId.PhysicalPedestrians, "physical.pedestrians", CapabilityRequirement.Optional, CapabilityScope.VisibleBodies,
                CapabilityRecheck.CityLoad, null, "ambientpedestrians and stationarycrowds hooks (H9, H10)",
                "Native pedestrians; background city unchanged."),
            new CapabilityDefinition(CapabilityId.InspectorClick, "inspector.click", CapabilityRequirement.Optional, CapabilityScope.InspectorClick,
                CapabilityRecheck.CityLoad, null, "MouseController.Run() prefix (H11)", "Inspector usable without it."),
            new CapabilityDefinition(CapabilityId.InspectorScroll, "inspector.scroll", CapabilityRequirement.Optional, CapabilityScope.InspectorScroll,
                CapabilityRecheck.CityLoad, null, "PedestrianCam.UpdateInput(float) prefix and postfix (H11)", "Inspector usable without it."),
            new CapabilityDefinition(CapabilityId.InspectorUiBlock, "inspector.uiblock", CapabilityRequirement.Optional, CapabilityScope.InspectorUiBlock,
                CapabilityRecheck.CityLoad, null, "GraphicRaycaster.Raycast prefix (H11)", "Inspector usable without it."),
            new CapabilityDefinition(CapabilityId.InteriorView, "interior.view", CapabilityRequirement.Optional, CapabilityScope.InteriorView,
                CapabilityRecheck.CityLoad, null, "F0 interior view and the third-party interior guard", "Off.", offByDefault: true),
        });

        public static int Count => All.Count;
        public static CapabilityDefinition Get(CapabilityId id) => All[Index(id)];

        public static bool TryParse(string name, out CapabilityId id)
        {
            foreach (var definition in All)
                if (string.Equals(definition.Name, name, StringComparison.Ordinal)) { id = definition.Id; return true; }
            id = default; return false;
        }

        internal static int Index(CapabilityId id)
        {
            int i = (int)id;
            if ((uint)i >= (uint)All.Count) throw new ArgumentOutOfRangeException(nameof(id));
            return i;
        }
    }

    public enum CapabilityStartStatus : byte { Ready, Waiting, Refused }

    /// <summary>Whether the city may start. Refused is a capability refusal: it is not a damaged save and not an older
    /// development city, and its message says so; the saved city is kept unchanged.</summary>
    public readonly struct CapabilityStart
    {
        internal CapabilityStart(CapabilityStartStatus status, CapabilityId? capability, string message)
        { Status = status; Capability = capability; Message = message; }
        public CapabilityStartStatus Status { get; }
        /// <summary>The first blocking capability in report order; null when Ready.</summary>
        public CapabilityId? Capability { get; }
        public string Message { get; }
    }

    /// <summary>D1 state per capability for one session. Updates allocate nothing; <see cref="Revision"/> advances on
    /// every applied change so a report can be reused until the state changes.</summary>
    public sealed class CapabilityRegistry
    {
        private readonly CapabilityState[] states = new CapabilityState[NativeCapabilities.Count];
        private readonly string?[] reasons = new string?[NativeCapabilities.Count];

        public long Revision { get; private set; }
        public CapabilityState State(CapabilityId id) => states[NativeCapabilities.Index(id)];
        /// <summary>The Off or Absent reason; null when Active or not checked.</summary>
        public string? Reason(CapabilityId id) => reasons[NativeCapabilities.Index(id)];

        /// <summary>First detection: NotChecked to Active, Off or Absent. Any later call is rejected; use
        /// <see cref="Fail"/> or <see cref="Recheck"/>.</summary>
        public CapabilityTransition Detect(CapabilityId id, CapabilityState outcome, string? reason = null)
        {
            int i = NativeCapabilities.Index(id);
            Validate(i, outcome, reason);
            if (states[i] != CapabilityState.NotChecked || !Allowed(i, outcome)) return CapabilityTransition.Rejected;
            return Set(i, outcome, reason);
        }

        /// <summary>A later verification failure: Active to Off. Off stays Off and keeps its first reason.</summary>
        public CapabilityTransition Fail(CapabilityId id, string reason)
        {
            int i = NativeCapabilities.Index(id);
            Validate(i, CapabilityState.Off, reason);
            if (states[i] == CapabilityState.Off) return CapabilityTransition.Unchanged;
            if (states[i] != CapabilityState.Active) return CapabilityTransition.Rejected;
            return Set(i, CapabilityState.Off, reason);
        }

        /// <summary>The capability's declared re-check point. Any other point is rejected, so Off and Absent are sticky until it.</summary>
        public CapabilityTransition Recheck(CapabilityId id, CapabilityRecheck point, CapabilityState outcome, string? reason = null)
        {
            int i = NativeCapabilities.Index(id);
            Validate(i, outcome, reason);
            if (point != NativeCapabilities.All[i].Recheck || states[i] == CapabilityState.NotChecked || !Allowed(i, outcome))
                return CapabilityTransition.Rejected;
            if (states[i] == outcome && string.Equals(reasons[i], Normal(outcome, reason), StringComparison.Ordinal))
                return CapabilityTransition.Unchanged;
            return Set(i, outcome, reason);
        }

        /// <summary>Scopes switched off: every Off capability's scope, and every Absent one's unless it is RequiredWhenPresent.
        /// NotChecked disables nothing here; <see cref="Start"/> holds the city until required checks have run.</summary>
        public CapabilityScope DisabledScopes
        {
            get
            {
                var off = CapabilityScope.None;
                for (int i = 0; i < states.Length; i++)
                {
                    var definition = NativeCapabilities.All[i];
                    if (states[i] == CapabilityState.Off
                        || states[i] == CapabilityState.Absent && definition.Requirement != CapabilityRequirement.RequiredWhenPresent)
                        off |= definition.Scope;
                }
                return off;
            }
        }

        public bool IsEnabled(CapabilityScope scope) => scope != CapabilityScope.None && (DisabledScopes & scope) == CapabilityScope.None;

        /// <summary>Refused when a Required capability is Off or Absent or a RequiredWhenPresent one is Off (first in report
        /// order); Waiting when a required check has not run; otherwise Ready. Optional capabilities never block.</summary>
        public CapabilityStart Start()
        {
            int waiting = -1;
            for (int i = 0; i < states.Length; i++)
            {
                var requirement = NativeCapabilities.All[i].Requirement;
                if (requirement == CapabilityRequirement.Optional) continue;
                var state = states[i];
                if (state == CapabilityState.Off || state == CapabilityState.Absent && requirement == CapabilityRequirement.Required)
                    return new CapabilityStart(CapabilityStartStatus.Refused, (CapabilityId)i, RefusalMessage(i));
                if (state == CapabilityState.NotChecked && waiting < 0) waiting = i;
            }
            return waiting < 0 ? new CapabilityStart(CapabilityStartStatus.Ready, null, "")
                : new CapabilityStart(CapabilityStartStatus.Waiting, (CapabilityId)waiting,
                    "Persistent City is waiting for " + NativeCapabilities.All[waiting].Name + " to be checked.");
        }

        private string RefusalMessage(int i)
        {
            var definition = NativeCapabilities.All[i];
            string what = states[i] == CapabilityState.Absent
                ? definition.Name + " is missing" + (definition.Companion == null ? "" : " (" + definition.Companion + " is required)")
                : definition.Name + " is off: " + reasons[i];
            return "Persistent City did not start: " + what + ". The game or a companion mod changed; this is not a damaged save "
                + "and not an older city, so a fixed game or mod version will start it again. The saved city is kept unchanged.";
        }

        private static void Validate(int i, CapabilityState outcome, string? reason)
        {
            if (outcome == CapabilityState.NotChecked || outcome > CapabilityState.Absent) throw new ArgumentOutOfRangeException(nameof(outcome));
            if (outcome == CapabilityState.Off && string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException(NativeCapabilities.All[i].Name + ": an Off capability needs a reason.", nameof(reason));
        }

        private static bool Allowed(int i, CapabilityState outcome)
            => outcome != CapabilityState.Absent || NativeCapabilities.All[i].Companion != null;

        private static string? Normal(CapabilityState outcome, string? reason)
            => outcome == CapabilityState.Active || string.IsNullOrEmpty(reason) ? null : reason;

        private CapabilityTransition Set(int i, CapabilityState outcome, string? reason)
        {
            states[i] = outcome; reasons[i] = Normal(outcome, reason); Revision++;
            return CapabilityTransition.Applied;
        }
    }

    /// <summary>The D7 text, deterministic for a given registry state and context. Wording: active, off, absent, not
    /// checked; never "compatible", never "verified by rig".</summary>
    public sealed class CapabilityReport
    {
        private static readonly (CapabilityScope Scope, string Text)[] ScopeNames =
        {
            (CapabilityScope.WholeCity, "whole city"), (CapabilityScope.ShopOwnership, "shop ownership"),
            (CapabilityScope.DynamicShops, "dynamic shops"), (CapabilityScope.VehicleScopes, "vehicle scopes"),
            (CapabilityScope.MarketResearch, "market research"), (CapabilityScope.VisibleBodies, "visible bodies"),
            (CapabilityScope.RouteTiming, "route timing"), (CapabilityScope.InspectorClick, "inspector click"),
            (CapabilityScope.InspectorScroll, "inspector scroll"), (CapabilityScope.InspectorUiBlock, "inspector UI block"),
            (CapabilityScope.InteriorView, "interior view"),
        };

        private readonly StringBuilder text = new StringBuilder(2048);
        private CapabilityRegistry? cachedRegistry;
        private long cachedRevision = -1;
        private string? cachedContext, cachedHeader, cachedStatus;

        /// <summary><c>pc capability status</c>: one header line and one line per capability. Reused while neither the
        /// registry revision nor the context changes.</summary>
        public string Status(CapabilityRegistry registry, string gameVersion) => Status(registry, gameVersion, null);

        /// <summary>F8d: <paramref name="header"/>, when given, replaces the start status in the first line (a running city,
        /// or a start held by a transient RLO answer).</summary>
        public string Status(CapabilityRegistry registry, string gameVersion, string? header)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            gameVersion ??= "";
            if (cachedStatus != null && ReferenceEquals(registry, cachedRegistry) && registry.Revision == cachedRevision
                && string.Equals(gameVersion, cachedContext, StringComparison.Ordinal) && string.Equals(header, cachedHeader, StringComparison.Ordinal))
                return cachedStatus;
            text.Clear();
            text.Append("Persistent City capabilities (game ").Append(OneLine(gameVersion)).Append("): ");
            if (header != null) text.Append(OneLine(header));
            else
            {
                var start = registry.Start();
                text.Append("start ").Append(StartWord(start.Status));
                if (start.Capability.HasValue) text.Append(" (").Append(NativeCapabilities.Get(start.Capability.Value).Name).Append(')');
            }
            text.Append('\n');
            foreach (var definition in NativeCapabilities.All)
            {
                Line(registry, definition);
                text.Append('\n');
            }
            cachedRegistry = registry; cachedRevision = registry.Revision; cachedContext = gameVersion; cachedHeader = header;
            return cachedStatus = text.ToString();
        }

        /// <summary><c>pc capability show &lt;id&gt;</c>: the status line plus native contract, hook targets, fallback and re-check point.</summary>
        public string Show(CapabilityRegistry registry, CapabilityId id)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var definition = NativeCapabilities.Get(id);
            text.Clear();
            Line(registry, definition);
            text.Append("\nnative contract: ").Append(definition.NativeContract);
            if (definition.Companion != null) text.Append("\ncompanion: ").Append(definition.Companion);
            foreach (var target in NativeHookInventory.Targets)
                if (target.Capability == id)
                    text.Append("\nhook: ").Append(target.Owner).Append(' ').Append(target.Signature).Append(' ').Append(Kinds(target.Kinds));
            text.Append("\nfallback: ").Append(definition.Fallback)
                .Append("\nre-check: ").Append(definition.Recheck == CapabilityRecheck.CityLoad ? "city load" : "companion revision")
                .Append('\n');
            return text.ToString();
        }

        private void Line(CapabilityRegistry registry, CapabilityDefinition definition)
        {
            var state = registry.State(definition.Id);
            text.Append(definition.Name).Append(' ').Append(RequirementWord(definition.Requirement)).Append(' ').Append(StateWord(state));
            var reason = registry.Reason(definition.Id);
            if (reason != null) text.Append(": ").Append(OneLine(reason));
            else if (state == CapabilityState.NotChecked && definition.OffByDefault) text.Append(" (off by default)");
            text.Append(" | scope: ");
            AppendScope(definition.Scope);
        }

        private void AppendScope(CapabilityScope scope)
        {
            bool first = true;
            foreach (var (flag, name) in ScopeNames)
            {
                if ((scope & flag) == 0) continue;
                if (!first) text.Append(", ");
                text.Append(name); first = false;
            }
        }

        public static string StateWord(CapabilityState state) => state switch
        {
            CapabilityState.Active => "active", CapabilityState.Off => "off", CapabilityState.Absent => "absent", _ => "not checked",
        };
        public static string RequirementWord(CapabilityRequirement requirement) => requirement switch
        {
            CapabilityRequirement.Required => "required", CapabilityRequirement.RequiredWhenPresent => "required-when-present", _ => "optional",
        };
        private static string StartWord(CapabilityStartStatus status) => status switch
        {
            CapabilityStartStatus.Ready => "ready", CapabilityStartStatus.Waiting => "waiting", _ => "refused",
        };
        private static string Kinds(NativePatchKinds kinds)
        {
            var parts = new List<string>(4);
            if ((kinds & NativePatchKinds.Prefix) != 0) parts.Add("prefix");
            if ((kinds & NativePatchKinds.Postfix) != 0) parts.Add("postfix");
            if ((kinds & NativePatchKinds.Transpiler) != 0) parts.Add("transpiler");
            if ((kinds & NativePatchKinds.Finalizer) != 0) parts.Add("finalizer");
            return string.Join("+", parts);
        }
        private static string OneLine(string value)
            => value.IndexOf('\n') < 0 && value.IndexOf('\r') < 0 ? value : value.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
    }

    [Flags]
    public enum NativePatchKinds : byte { None = 0, Prefix = 1, Postfix = 2, Transpiler = 4, Finalizer = 8 }

    /// <summary>One Persistent City Harmony owner ID and the Runtime file that constructs it.</summary>
    public sealed class NativeHookOwner
    {
        internal NativeHookOwner(string id, string file, int patchCallSites, string? tableFile = null)
        { Id = id; File = file; PatchCallSites = patchCallSites; TableFile = tableFile; }
        public string Id { get; }
        /// <summary>Repository-relative path, forward slashes.</summary>
        public string File { get; }
        /// <summary>Harmony <c>.Patch(</c> call sites in <see cref="File"/>; the source inventory test checks the count.</summary>
        public int PatchCallSites { get; }
        /// <summary>A file whose table lists the targets when the owner patches from data (food-contract).</summary>
        public string? TableFile { get; }
    }

    /// <summary>One patched native method for one owner. Parameters use CLR short names; null means the source resolves
    /// the target by name only (none since F8c).</summary>
    public sealed class NativeHookTarget
    {
        internal NativeHookTarget(string owner, string type, string method, string[]? parameters, NativePatchKinds kinds, CapabilityId capability)
        {
            Owner = owner; Type = type; Method = method; Kinds = kinds; Capability = capability;
            Parameters = parameters == null ? null : Array.AsReadOnly(parameters);
        }
        public string Owner { get; }
        public string Type { get; }
        public string Method { get; }
        public IReadOnlyList<string>? Parameters { get; }
        public NativePatchKinds Kinds { get; }
        public CapabilityId Capability { get; }
        public string Signature => Type + "." + Method + (Parameters == null ? " (by name)" : "(" + string.Join(", ", Parameters) + ")");
    }

    /// <summary>Static inventory of every Harmony owner and target Persistent City installs, re-read at the F8a base
    /// (after F2c-F2e, E5b2/E5b3 and F5h): 12 owners, 52 owner-target rows. Section 2 of the plan listed 11 owners and
    /// 27 methods; E5b2 added the food-contract owner with its 23 hint postfixes. tests/HookInventorySourceTests.cs fails
    /// when Runtime adds an owner or target this list does not declare.</summary>
    public static class NativeHookInventory
    {
        public const string FoodAuthority = "persistentcity.food-authority";
        public const string FoodRoutines = "persistentcity.food-routines";
        public const string FoodContract = "persistentcity.food-contract";
        public const string CityClock = "hart.persistentcity.cityclock.v1";
        public const string ClockObserver = "hart.persistentcity.clockobserver.v1";
        public const string ResearchBilling = "persistentcity.research-billing";
        public const string ResearchDialog = "persistentcity.research-dialog";
        public const string PhysicalVehicles = "hart.persistentcity.physicalvehicles.v1";
        public const string PhysicalIntersections = "hart.persistentcity.physicalintersections.v1";
        public const string AmbientPedestrians = "hart.persistentcity.ambientpedestrians.v1";
        public const string StationaryCrowds = "hart.persistentcity.stationarycrowds.v1";
        public const string InspectorInput = "persistentcity.resident-inspector-input";

        public static readonly IReadOnlyList<NativeHookOwner> Owners = Array.AsReadOnly(new[]
        {
            new NativeHookOwner(FoodAuthority, "Runtime/NativeFoodOwnership.cs", 1),
            new NativeHookOwner(FoodRoutines, "Runtime/NativeFoodMarket.cs", 1),
            new NativeHookOwner(FoodContract, "Runtime/LoadedCatalogueLifecycleMembers.cs", 1, "Runtime/FoodContractHooks.cs"),
            new NativeHookOwner(CityClock, "Runtime/NativeCityClock.cs", 3),
            new NativeHookOwner(ClockObserver, "Runtime/NativeTravelPace.cs", 1),
            new NativeHookOwner(ResearchBilling, "Runtime/ResearchBilling.cs", 1),
            new NativeHookOwner(ResearchDialog, "Runtime/ResearchAgencyDialog.cs", 1),
            new NativeHookOwner(PhysicalVehicles, "Runtime/PhysicalTrafficHooks.cs", 5),
            new NativeHookOwner(PhysicalIntersections, "Runtime/PhysicalIntersections.cs", 2),
            new NativeHookOwner(AmbientPedestrians, "Runtime/AmbientPedestrians.cs", 3),
            new NativeHookOwner(StationaryCrowds, "Runtime/StationaryCrowds.cs", 1),
            new NativeHookOwner(InspectorInput, "Runtime/ResidentInspector.cs", 3),
        });

        private const NativePatchKinds Pre = NativePatchKinds.Prefix, Post = NativePatchKinds.Postfix;
        private static NativeHookTarget T(string owner, string type, string method, string[]? parameters, NativePatchKinds kinds, CapabilityId capability)
            => new NativeHookTarget(owner, type, method, parameters, kinds, capability);
        private static NativeHookTarget Hint(string type, string method, params string[] parameters)
            => new NativeHookTarget(FoodContract, type, method, parameters, Post, CapabilityId.FoodDynamic);
        private static readonly string[] None = Array.Empty<string>();

        public static readonly IReadOnlyList<NativeHookTarget> Targets = Array.AsReadOnly(new[]
        {
            // F8c pinned these six (NativeFoodContractSpec.Authority); before F8c they were resolved by name only.
            T(FoodAuthority, "GameManager", "RunMainGameTick", new[] { "Single" }, Post, CapabilityId.FoodAuthority),
            T(FoodAuthority, "RetailBusinessSimulator", "ProcessAllCustomersFromThisHour", None, Pre, CapabilityId.FoodAuthority),
            T(FoodAuthority, "RetailBusinessSimulator", "ProcessCustomer", new[] { "CustomerEntry" }, Pre, CapabilityId.FoodAuthority),
            T(FoodAuthority, "CustomerEntriesHelper", "GetEntriesByAddress", new[] { "Address" }, Pre, CapabilityId.FoodAuthority),
            T(FoodAuthority, "CustomerEntriesHelper", "UpdateCustomerEntriesForPlayerBusiness", new[] { "BuildingRegistration", "DayOfWeekOrdered" }, Pre, CapabilityId.FoodAuthority),
            T(FoodAuthority, "BuildingRegistration", "Reset", new[] { "Boolean" }, Pre, CapabilityId.FoodAuthority),
            T(FoodRoutines, "CitizenData", "IsPriceAcceptable", new[] { "String", "Single" }, Pre, CapabilityId.FoodCheckoutShop),
            // E5b2 Decision 1 table (Runtime/FoodContractHooks.cs), in table order.
            Hint("ItemsGetter", "OnItemsLoaded", "IList`1"),
            Hint("ItemsGetter", "RegisterModItem", "Item"),
            Hint("ItemsGetter", "UnregisterModItem", "String"),
            Hint("ItemsGetter", "ResetStaticData"),
            Hint("BusinessHelper", "UpdateCachedAvailableProducts", "BuildingRegistration"),
            Hint("BuildingRegistration", "AddItemInstanceToBuilding", "ItemInstance"),
            Hint("BuildingRegistration", "RemoveItemInstanceFromBuilding", "ItemInstance", "Boolean"),
            Hint("BuildingRegistration", "OnBusinessTypeChanged"),
            Hint("BuildingRegistration", "Reset", "Boolean"),
            Hint("ItemHelper", "FillUpShowcaseShelfOrPointOfSale", "ItemInstance"),
            Hint("ItemHelper", "SubtractFromStock", "ItemInstance", "Int32"),
            Hint("ItemInstance", "AddToCargo", "CargoInstance", "Int32"),
            Hint("ItemInstance", "RemoveFromCargo", "CargoInstance"),
            Hint("ItemInstance", "ReduceFromCargo", "CargoInstance", "Int32"),
            Hint("ItemInstance", "MergeCargo", "CargoInstance", "CargoInstance"),
            Hint("ItemInstance", "MergeCargo", "CargoInstance", "CargoInstance", "Int32"),
            Hint("CargoInstance", "MergeAmount", "CargoInstance", "Int32"),
            Hint("ReStockingHelper", "RefillSingleItemWithLimit", "ItemInstance"),
            Hint("ReStockingHelper", "TryAddStockAmount", "ItemInstance", "CargoInstance", "Int32"),
            Hint("CargoInstance", "ParseIntoItemInstance", "ItemInstance"),
            Hint("BuildingRegistration", "TemporarilyClose", "Boolean"),
            Hint("BuildingRegistration", "RemoveUnusedRetailPrices"),
            Hint("CargoInstance", "ResetItemCached"),
            T(CityClock, "GameManager", "RunMainGameTick", new[] { "Single" }, Pre, CapabilityId.ClockCity),
            T(CityClock, "Ride", "CapTick", new[] { "Single&" }, Pre | Post, CapabilityId.ClockDrivenRides),
            T(ClockObserver, "GameManager", "RunMainGameTick", new[] { "Single" }, Post, CapabilityId.ClockObserve),
            T(ResearchBilling, "MarketingHelper", "RunDaily", None, Post, CapabilityId.ResearchBilling),
            T(ResearchDialog, "MarketingAgencyDialog", "CampaignSettings", None, Post, CapabilityId.ResearchDialog),
            T(PhysicalVehicles, "GameSpeedController", "ChangeTimeScale", new[] { "TimeSpeed", "Single" }, Pre, CapabilityId.PhysicalVehicles),
            T(PhysicalVehicles, "DrivingAI", "VehicleCrashHandler", new[] { "Int32", "Int32", "Boolean" }, Post, CapabilityId.PhysicalVehicles),
            T(PhysicalVehicles, "DensityManager", "AddVehicle", new[] { "Boolean", "Int32", "Int32", "Vector3", "Vector3" },
                NativePatchKinds.Transpiler, CapabilityId.PhysicalVehicles),
            T(PhysicalVehicles, "TrafficManager", "FixedUpdate", None,
                Pre | Post | NativePatchKinds.Transpiler | NativePatchKinds.Finalizer, CapabilityId.PhysicalVehicles),
            T(PhysicalVehicles, "DrivingAI", "HasReachedOrPassedWaypoint", new[] { "Int32", "Int32" }, Pre, CapabilityId.PhysicalVehicles),
            T(PhysicalVehicles, "DrivingAI", "WaypointRequested", new[] { "Int32", "VehicleTypes" }, Pre, CapabilityId.PhysicalVehicles),
            T(PhysicalVehicles, "AiCarRescueCheck", "DoUpdate", new[] { "Single" }, Pre, CapabilityId.PhysicalVehicles),
            T(PhysicalVehicles, "TrafficDespawner", "OnTriggerEnter", new[] { "Collider" }, Pre, CapabilityId.PhysicalVehicles),
            T(PhysicalIntersections, "IntersectionManager", "SetActiveIntersection", new[] { "List`1" }, Pre, CapabilityId.PhysicalVehicles),
            T(PhysicalIntersections, "IntersectionManager", "UpdateIntersections", None, Post, CapabilityId.PhysicalVehicles),
            T(AmbientPedestrians, "PedestrianSpawner", "SetActivePedestrians", new[] { "Int32" }, Pre, CapabilityId.PhysicalPedestrians),
            T(AmbientPedestrians, "Pedestrian", "OnEndCycle", None, Pre, CapabilityId.PhysicalPedestrians),
            T(AmbientPedestrians, "Pedestrian", "ChangeState", new[] { "Boolean" }, Pre, CapabilityId.PhysicalPedestrians),
            T(StationaryCrowds, "StationaryAiSpawner", "Spawn", new[] { "Vector3" }, Pre, CapabilityId.PhysicalPedestrians),
            T(InspectorInput, "MouseController", "Run", None, Pre, CapabilityId.InspectorClick),
            T(InspectorInput, "PedestrianCam", "UpdateInput", new[] { "Single" }, Pre | Post, CapabilityId.InspectorScroll),
            T(InspectorInput, "GraphicRaycaster", "Raycast", new[] { "PointerEventData", "List`1" }, Pre, CapabilityId.InspectorUiBlock),
        });

        public static NativeHookOwner? Owner(string id)
        {
            foreach (var owner in Owners) if (string.Equals(owner.Id, id, StringComparison.Ordinal)) return owner;
            return null;
        }

        /// <summary>One line per target, in inventory order, for diagnostics and the PR record.</summary>
        public static string Describe()
        {
            var text = new StringBuilder();
            foreach (var target in Targets)
                text.Append(target.Owner).Append(' ').Append(target.Signature).Append(" -> ")
                    .Append(NativeCapabilities.Get(target.Capability).Name).Append('\n');
            return text.ToString();
        }

    }
}
