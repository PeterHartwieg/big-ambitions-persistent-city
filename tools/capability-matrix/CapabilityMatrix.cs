using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PersistentCity.Core;
using PersistentCity.EconomyInterventions;
using PersistentCity.Persistence;
using PersistentCity.Runtime;

namespace PersistentCity.FailureMatrix;

/// <summary>F8g (docs/plans/f8-capability-boundaries.md): the offline failure matrix over the Core 957-resident economy
/// fixture (F6a shape, representation factor 2). Each arm switches one capability off, absent or refused the way the
/// Runtime does, and checks the plan's invariants. Shared by tools/capability-matrix and tests/CapabilityMatrixTests;
/// the Street Food absent-then-returned arm uses the test project's vendor rig and lives in the test.</summary>
public static class CapabilityMatrix
{
    public const int Seed = 11, Residents = EconomyFixture.Residents;

    public sealed class Arm
    {
        public string Name = "", Capabilities = "", Assertion = "";
        public bool Passed;
        public string Detail = "";
        public double Seconds;
    }

    private static RunOptions Options(int days, int? bodyCap = null, int fps = 0, bool ownListed = true)
        => new RunOptions { Seed = Seed, Residents = Residents, Days = days, BodyCap = bodyCap, FramesPerSecond = fps, OwnListed = ownListed };

    /// <summary>Runs every Core arm. <paramref name="days"/> is the simulated length of each economy arm.</summary>
    public static List<Arm> Run(int days, Action<string>? log = null)
    {
        var arms = new List<Arm>();
        Arm Add(string name, string capabilities, string assertion, Func<string> body)
        {
            var arm = new Arm { Name = name, Capabilities = capabilities, Assertion = assertion };
            var timer = System.Diagnostics.Stopwatch.StartNew();
            try { arm.Detail = body(); arm.Passed = true; }
            catch (Exception error) { arm.Detail = error.GetType().Name + ": " + error.Message; }
            arm.Seconds = timer.Elapsed.TotalSeconds;
            arms.Add(arm);
            log?.Invoke((arm.Passed ? "pass " : "FAIL ") + name + " (" + arm.Seconds.ToString("F1", CultureInfo.InvariantCulture) + " s): " + arm.Detail);
            return arm;
        }

        RunResult? control = null;
        Add("control", "every capability active", "receipts have no duplicate intent; every owner's cash is conserved each midnight", () =>
        {
            control = EconomyRun.Run(Options(days));
            Require(control.Purchases > 0 && control.Days.All(d => d.DuplicateOrders == 0 && d.ConservationChecks > 0), "control sold and conserved");
            return "purchases=" + control.Purchases + " save=" + control.FinalSaveSha256[..12];
        });

        // Presentation capabilities have no economic role: the background city is identical with bodies off and never present.
        Add("presentation off vs never present", "physical.vehicles, physical.pedestrians, rlo.reservations (bodies), inspector.*, interior.view",
            "money, wallets, receipts and the saved city equal between bodies off (driver attached, cap 0) and never present", () =>
        {
            var never = EconomyRun.Run(Options(days, null, 30));
            var off = EconomyRun.Run(Options(days, 0, 30));
            Require(off.BodyAcquisitions == 0, "off arm acquired no body");
            Require(never.Json == off.Json, "day metrics (money, wallets, receipts) equal");
            Require(never.FinalSaveSha256 == off.FinalSaveSha256, "saved city byte-identical");
            Require(never.Purchases == off.Purchases && never.DiscretionaryUnits == off.DiscretionaryUnits, "purchases and wallets equal");
            return "purchases=" + off.Purchases + " frames=" + off.Frames + " save=" + off.FinalSaveSha256[..12];
        });

        // food.checkout.shop off: shops stay native-owned, so the city books no PC sale and conserves every wallet.
        Add("food.checkout.shop off", "food.checkout.shop", "no PC receipt is booked; cash is conserved; no duplicate intent", () =>
        {
            var off = EconomyRun.Run(Options(days, ownListed: false));
            Require(off.Days.All(d => d.NativeRevenueUnits == 0 && d.NativeOrders == 0 && d.ConfirmedPreparedUnits == 0), "no PC sale while checkout is off");
            Require(off.Days.All(d => d.DuplicateOrders == 0 && d.ConservationChecks > 0), "conserved, no duplicates");
            Require(control == null || control.Days.Sum(d => d.NativeOrders) > 0, "control does sell, so the arm is not vacuous");
            return "own orders=0 over " + off.Days.Count + " days; conservation checks=" + off.ConservationChecks;
        });

        // Required capabilities refused: no city starts and the saved keys stay byte-identical.
        Add("required refused", "food.authority, clock.observe, clock.city, rlo.donor, clock.drivenrides (present and changed)",
            "a refused required arm leaves the payload byte-identical; the status is a capability refusal, not damage or an older city", () =>
        {
            var (state, market) = EconomyFixture.Build(Seed, Residents);
            EconomyFixture.Attach(state, market, new EconomyRoutes(market), fresh: true);
            var saved = new Dictionary<string, string>(StringComparer.Ordinal) { [SaveStore.StateKey] = SaveCodec.Write(state), ["persistentcity.food-authority"] = "[]" };
            var before = saved.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();
            int refused = 0;
            foreach (var id in NativeCapabilities.All.Where(d => d.Requirement != CapabilityRequirement.Optional).Select(d => d.Id))
            {
                var registry = new CapabilityRegistry();
                foreach (var d in NativeCapabilities.All) registry.Detect(d.Id, d.Id == id ? CapabilityState.Off : CapabilityState.Active, d.Id == id ? "contract changed" : null);
                var start = TryStart(registry, saved);
                Require(start.Status == CapabilityStartStatus.Refused && start.Capability == id, NativeCapabilities.Get(id).Name + " refuses the start");
                Require(start.Message.Contains("not a damaged save and not an older city") && start.Message != SaveCodec.DevelopmentSchemaMessage, "distinct status");
                Require(saved.OrderBy(p => p.Key, StringComparer.Ordinal).SequenceEqual(before), "saved keys byte-identical");
                refused++;
            }
            // Driven Rides absent is not a refusal.
            var absent = new CapabilityRegistry();
            foreach (var d in NativeCapabilities.All) absent.Detect(d.Id, d.Id == CapabilityId.ClockDrivenRides ? CapabilityState.Absent : CapabilityState.Active, d.Id == CapabilityId.ClockDrivenRides ? "not loaded" : null);
            Require(TryStart(absent, saved).Status == CapabilityStartStatus.Ready, "Driven Rides absent starts");
            return refused + " required capabilities refused; payload unchanged";
        });

        // RLO transient: the periodic and save gates continue; the saved city equals a run with RLO Ready throughout.
        Add("rlo.donor transient window", "rlo.donor (Off, NotYet, Refreshing)", "the city runs and saves the Ready bytes; identity hash and resident count unchanged; Shared stops with the last payload kept", () =>
        {
            var ready = TransientRun(days, _ => DonorReadReason.Ready);
            var window = TransientRun(days, check => check % 8 is >= 2 and < 6 ? (check % 3 == 0 ? DonorReadReason.Off : check % 3 == 1 ? DonorReadReason.NotYet : DonorReadReason.Refreshing) : DonorReadReason.Ready);
            Require(window.Stopped == null && ready.Stopped == null, "no stop in a transient window");
            Require(window.Transient > 0, "the window was exercised");
            Require(window.Saves.SequenceEqual(ready.Saves), "every save byte-identical to the Ready run");
            Require(window.Identity == ready.Identity && window.Residents == ready.Residents, "identity hash and resident count unchanged");
            var shared = TransientRun(days, check => check == 30 ? DonorReadReason.Shared : DonorReadReason.Ready);
            Require(shared.Stopped == 30 && shared.Saves.Count > 0 && shared.Saves[^1] == ready.Saves[shared.Saves.Count - 1], "Shared stops; the last save is kept");
            return "checks=" + window.Checks + " transient=" + window.Transient + " saves=" + window.Saves.Count + " identity=" + window.Identity[..12];
        });

        // research.dialog, research.billing, research.delivery: each contract failing alone, through the production gate
        // (ResearchEligibility.SessionEnabled, which PersistentCityMod.AttachResearch uses, and MarketResearchActivation).
        // The control has all three and surveys the order; each failure keeps the order unchanged and moves no resident money.
        Add("research contracts off one at a time", "research.dialog, research.billing, research.delivery",
            "each failed contract names itself, leaves research unattached and the order unchanged; resident cash, wallets, receipts and scopes equal the control", () =>
        {
            var control = Simulate(days, research: ResearchEligibility.SessionEnabled(true, true, true), order: true);
            Require(control.OrderSurveyed, "control: all contracts pass and the order is surveyed");
            var detail = new List<string>();
            foreach (var (name, dialog, billing, delivery) in new[] { ("dialog", false, true, true), ("billing", true, false, true), ("delivery", true, true, false) })
            {
                bool enabled = ResearchEligibility.SessionEnabled(dialog, billing, delivery);
                var activation = new MarketResearchActivation(true, dialog, billing, delivery, enabled);
                Require(!enabled && !activation.Available && activation.Refusal.StartsWith(name == "dialog" ? "agency dialog" : name, StringComparison.Ordinal), name + " names itself");
                var off = Simulate(days, research: enabled, order: true);
                Require(!off.OrderSurveyed && off.OpenOrders == 1, name + ": order kept unchanged");
                Require(off.Economy == control.Economy && off.Scopes == control.Scopes && off.Conserved && off.Duplicates == 0, name + ": resident money equal to the control");
                detail.Add(name + " off: " + activation.Refusal);
            }
            return string.Join("; ", detail) + "; " + control.Economy;
        });

        // food.dynamic: the second site sells a non-historical product. Whether it may be owned is the production menu
        // predicate (FoodMenuPolicy.LiveMenu, which NativeFoodMarket applies with a policy only while dynamic acquisition
        // is on). Control: dynamic on, the site sells. Off: the site is refused, books no PC receipt, keeps its scope bytes.
        Add("food.dynamic off vs on", "food.dynamic", "on: the dynamic site sells; off: it books no PC receipt and keeps its bytes; the other shop sells; conserved", () =>
        {
            var on = Simulate(days, dynamicOn: true);
            var off = Simulate(days, dynamicOn: false);
            Require(on.FarOrders > 0, "control: with dynamic acquisition on the dynamic site sells (" + on.FarOrders + ")");
            Require(off.FarOrders == 0 && off.OwnOrders > 0, "off: no PC sale at the refused site; the historical shop sells");
            Require(off.ScopesBefore == off.Scopes, "off: scope bytes unchanged");
            Require(off.Conserved && on.Conserved && off.Duplicates + on.Duplicates == 0, "conserved, no duplicates");
            return "on: dynamic site orders=" + on.FarOrders + "; off: 0 (own orders=" + off.OwnOrders + ")";
        });

        return arms;
    }

    /// <summary>Every optional or required-when-present capability and the arm (or other check) that fails it. The test
    /// fails when a capability has no entry.</summary>
    public static readonly IReadOnlyDictionary<CapabilityId, string> Coverage = new Dictionary<CapabilityId, string>
    {
        [CapabilityId.FoodCheckoutShop] = "food.checkout.shop off",
        [CapabilityId.FoodDynamic] = "food.dynamic off vs on",
        [CapabilityId.VendorStreetFood] = "CapabilityMatrixTests.StreetFoodAbsentThenReturned (vendor rig)",
        [CapabilityId.ClockDrivenRides] = "required refused (changed refuses; absent starts)",
        [CapabilityId.RloReservations] = "presentation off vs never present (bodies are the reservation guard's only consumer)",
        // The road planner is Runtime: tools/road-speed-regression drives the real RoadRoutes and NativeRoadSpeeds through
        // NotYet/Refreshing answers, a held plan, a lane change and a save in the window (TransientCaptureWaits).
        [CapabilityId.RloRoadSpeeds] = "tools/road-speed-regression (TransientCaptureWaits)",
        [CapabilityId.ResearchDialog] = "research contracts off one at a time",
        [CapabilityId.ResearchBilling] = "research contracts off one at a time",
        [CapabilityId.ResearchDelivery] = "research contracts off one at a time",
        [CapabilityId.PhysicalVehicles] = "presentation off vs never present",
        [CapabilityId.PhysicalPedestrians] = "presentation off vs never present",
        // Runtime-only surfaces with no Core input: the Core city cannot see them (source checks in CapabilityReportTests
        // and HookConflictReportTests; rig checks in docs/checks/f8bg.md).
        [CapabilityId.InspectorClick] = "no Core input: CapabilityReportTests.ReportFromDoublesForEveryState",
        [CapabilityId.InspectorScroll] = "no Core input: CapabilityReportTests.ReportFromDoublesForEveryState",
        [CapabilityId.InspectorUiBlock] = "no Core input: CapabilityReportTests.ReportFromDoublesForEveryState",
        [CapabilityId.InteriorView] = "no Core input: CapabilityReportTests.ReportFromDoublesForEveryState",
    };

    public sealed class Outcome
    {
        public string Economy = "", Scopes = "", ScopesBefore = "", Identity = "";
        public long OwnOrders, FarOrders, Duplicates;
        public bool Conserved = true, OrderSurveyed;
        public int OpenOrders;
    }

    private static string ScopeBytes(CityState state) => string.Join("|", state.Food!.Shops.Select(s =>
        s.PlaceId + "," + s.BusinessId + "," + s.ProductId + "," + string.Join("+", s.AdditionalProductIds) + "," + s.Channel));

    /// <summary>The economy fixture's epoch loop with one capability switched the way the Runtime switches it.</summary>
    public const string DynamicProduct = "ba:itemname_dumplings";

    /// <summary>The dynamic policy a passing E5b5 publication gives: the product is loaded and classed Food by a provider.</summary>
    private static FoodProductPolicy DynamicPolicy()
    {
        var registry = new FoodProductMetadataRegistry(); registry.BindPublicationThread();
        Require(registry.TryCreateProvider("f8g:matrix", out var provider, out _), "provider");
        Require(provider.TryRegisterProductMetadata(DynamicProduct, new FoodProductMetadataInput { MealClass = MealClass.Food }, 1, out _), "metadata");
        var loaded = new FoodProductMetadataLoadedNames(new[] { DynamicProduct }.Concat(FoodProducts.Ids));
        return new FoodProductPolicy(FoodProductMetadataResolver.Empty.WithApi(registry.Publish(loaded)), loaded);
    }

    public static Outcome Simulate(int days, bool research = false, bool order = false, bool? dynamicOn = null)
    {
        var (state, market) = EconomyFixture.Build(Seed, Residents);
        if (dynamicOn is bool on)
        {
            var scope = state.Food!.Shops.Single(s => s.BusinessId == EconomyFixture.FarShop);
            scope.AdditionalProductIds.Add(DynamicProduct);
            // The production predicate: no policy unless dynamic acquisition is on (NativeFoodMarket.LiveMenu).
            var refusal = FoodMenuPolicy.LiveMenu(scope.Products().ToList(), on ? DynamicPolicy() : null);
            Require(on ? refusal == FoodLiveMenuRefusal.None : refusal == FoodLiveMenuRefusal.OriginalThreeGuard, "menu verdict " + refusal);
            market.Shops.Single(s => s.BusinessId == EconomyFixture.FarShop).SiteOpen = refusal == FoodLiveMenuRefusal.None;
        }
        var city = EconomyFixture.Attach(state, market, new EconomyRoutes(market), fresh: true);
        var outcome = new Outcome { ScopesBefore = ScopeBytes(state) };
        if (order)
        {
            Require(state.Research.TryOrder(ResearchProducts.BasicLocalCode, "shop", "agency", false, 500, state.Minute, out _) == ResearchOrderRefusal.None, "order placed");
            if (research) city.AttachResearch(state.Research, 256, address => address == "shop" ? "shop" : null);
        }
        double start = EconomyFixture.StartMinute;
        market.Enter((long)start);
        Require(city.BeginAccountingObservations(start), "initial epoch");
        long end = (long)start + days * 1440L;
        for (long epoch = (long)start + 1; epoch <= end; epoch++)
        {
            double ending = AccountingClock.PreviousEpochTarget(epoch);
            int guard = 0;
            while (!city.AdvanceTo(ending, 10000).CaughtUp) Require(++guard < 100000, "nonterminating epoch");
            market.Enter(epoch);
            Require(city.BeginAccountingObservations(epoch), "observation epoch");
            if (epoch % 1440 == 0)
                foreach (var meal in state.Food!.Residents)
                    outcome.Conserved &= meal.Budget != null && HouseholdFinance.CashConserved(meal.Budget);
        }
        var food = city.State.Food!;
        outcome.OwnOrders = market.NativeOrders; outcome.Duplicates = market.DuplicateOrders;
        outcome.FarOrders = market.Tickets.Where(t => t.Key.Item2 == EconomyFixture.FarShop).Sum(t => t.Value.Meals);
        outcome.Economy = "purchases=" + food.Residents.Sum(m => m.Purchases) + " wallets=" + food.Residents.Aggregate(0L, (t, m) => t + (long)m.Budget!.Balance.Units)
            + " orders=" + market.NativeOrders + " revenue=" + market.NativeRevenueUnits;
        outcome.Scopes = ScopeBytes(city.State);
        outcome.OpenOrders = city.State.Research.Open.Count;
        outcome.OrderSurveyed = city.State.Research.Open.Concat(city.State.Research.Kept).Any(o => o.State != ResearchOrderState.Ordered);
        outcome.Identity = Sha(string.Join("|", city.State.Citizens.Select(c => c.Identity.Id + ":" + c.Identity.Name)));
        return outcome;
    }

    /// <summary>The Runtime order: capabilities first; only a Ready start may read or write the saved keys.</summary>
    private static CapabilityStart TryStart(CapabilityRegistry registry, IDictionary<string, string> saved)
    {
        var start = registry.Start();
        if (start.Status != CapabilityStartStatus.Ready) return start;
        _ = saved[SaveStore.StateKey];
        return start;
    }

    public sealed class TransientResult
    {
        public int Checks, Transient;
        public int? Stopped;
        public readonly List<string> Saves = new();
        public string Identity = "";
        public int Residents;
    }

    /// <summary>One run with a donor read every 30 game minutes (the Runtime's periodic cadence at native pace) and a save
    /// every 6 game hours, gated as PersistentCityMod does after activation.</summary>
    public static TransientResult TransientRun(int days, Func<int, DonorReadReason> read)
    {
        var (state, market) = EconomyFixture.Build(Seed, Residents);
        var city = EconomyFixture.Attach(state, market, new EconomyRoutes(market), fresh: true);
        var result = new TransientResult();
        double start = EconomyFixture.StartMinute;
        market.Enter((long)start);
        Require(city.BeginAccountingObservations(start), "initial epoch");
        long end = (long)start + days * 1440L;
        for (long epoch = (long)start + 1; epoch <= end; epoch++)
        {
            double ending = AccountingClock.PreviousEpochTarget(epoch);
            int guard = 0;
            while (!city.AdvanceTo(ending, 10000).CaughtUp) Require(++guard < 100000, "nonterminating epoch");
            market.Enter(epoch);
            Require(city.BeginAccountingObservations(epoch), "observation epoch");
            if (epoch % 30 != 0) continue;
            var reason = read(result.Checks++);
            var continuity = reason == DonorReadReason.Ready ? DonorContinuity.Match : DonorReads.Continuity(reason);
            if (continuity == DonorContinuity.Transient) result.Transient++;
            if (!DonorReads.Continues(continuity)) { result.Stopped = result.Checks - 1; break; }
            if (epoch % 360 == 0) result.Saves.Add(Sha(SaveCodec.Write(city.State)));
        }
        result.Identity = Sha(string.Join("|", city.State.Citizens.Select(c => c.Identity.Id + ":" + c.Identity.Name + ":" + c.Identity.AppearanceSeed))
            + "#" + string.Join("|", city.State.SourceIdentities.Select(s => s.Id + ":" + s.Name)) + "#" + city.State.PopulationKey);
        result.Residents = city.State.Citizens.Count;
        return result;
    }

    public static string Table(IEnumerable<Arm> arms)
    {
        var text = new StringBuilder("| Arm | Capabilities | Assertion | Result | Wall s |\n| --- | --- | --- | --- | ---: |\n");
        foreach (var arm in arms)
            text.Append("| ").Append(arm.Name).Append(" | ").Append(arm.Capabilities).Append(" | ").Append(arm.Assertion).Append(" | ")
                .Append(arm.Passed ? "pass: " : "FAIL: ").Append(arm.Detail).Append(" | ").Append(arm.Seconds.ToString("F1", CultureInfo.InvariantCulture)).Append(" |\n");
        return text.ToString();
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static string Sha(string text)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }
}
