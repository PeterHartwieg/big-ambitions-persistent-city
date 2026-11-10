using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using PersistentCity.Core;
using PersistentCity.EconomyInterventions;
using PersistentCity.Persistence;
using PersistentCity.Runtime;

namespace PersistentCity.Tests.Acceptance;

/// <summary>The in-flight states a reload arm saves and loads at (Decision 3), plus the first save on each day boundary.</summary>
internal enum CityReload { DayBoundary, BetweenFillUnits, UncertainReceipt, ResearchPendingBilling, VendorLeaseOpen, HeldMealDecision, MidJourney }

/// <summary>Faults the oracles must catch (F9c tests): each makes the run throw <see cref="CityViolation"/>.</summary>
public enum CityFault { None, DuplicateSale, SkippedInput, ResearchChargeWithoutReport, VendorSaleOutsideLease,
    /// <summary>Not an oracle failure: two same-minute postings swap execution order with equal amounts; only the ledger comparison can see it.</summary>
    ReorderedPostings }

/// <summary>One arm of the scenario. The reference drains every native minute synchronously (no frame loop, synchronous
/// planner); every other arm runs F1's frame loop with F5h's admitted planner at its cadence and budget.</summary>
internal sealed record CityArm(string Name, bool Reference, int Fps = 60, int Budget = 16, double Milliseconds = 2, bool Skip = false,
    bool Reloads = false, CityFault Fault = CityFault.None, int Days = CityScenario.Days)
{
    public static CityArm ReferenceArm => new("reference", true);
    /// <summary>F5h's F1 arms (30/32/4, 60/16/2, 120/128/2), a hidden skip and the reload arm.</summary>
    public static IEnumerable<CityArm> Arms() => new[]
    {
        new CityArm("fps30", false, 30, 32, 4), new CityArm("fps60", false, 60, 16, 2), new CityArm("fps120", false, 120, 128, 2),
        new CityArm("hidden-skip", false, 60, 32, 4, Skip: true), new CityArm("reload", false, 60, 16, 2, Reloads: true),
    };
}

/// <summary>What one arm produced: the ordered event ledger, the save at every drained day boundary, the native orders and
/// vendor sales, and the facts the tests read.</summary>
internal sealed class CityRun
{
    public string Arm = "";
    public readonly List<string> Ledger = new();
    public readonly List<(long Boundary, string Save)> DaySaves = new();
    public List<string> Orders = new();
    public List<CityVendorApi.Sale> VendorSales = new();
    public readonly List<(CityReload State, double Minute)> Reloads = new();
    public readonly Dictionary<string, ResearchFact> Research = new(StringComparer.Ordinal);
    public long Frames, Checkpoints, HistoricalClockMisses, HistoricalCheckouts, InputsPublished, Postings;
    /// <summary>The schedule this arm ran, derived from its checkpoint.</summary>
    public IReadOnlyList<InterventionInput> Inputs = Array.Empty<InterventionInput>();
    /// <summary>Each agency charge: the order it bills and the native day it was booked to.</summary>
    public List<(string Order, int Day)> ResearchChargeDays = new();
    public string VendorBusiness = "";
    public double MaxLag, Seconds;
    public long? HandbackMinute;
    public string HandbackOwner = "";
    public string Final = "";
    public CityState State = null!;
    public int ResearchCharges;
    public double ToolMsPerDay => Seconds * 1000 / CityScenario.Days;
}

internal sealed class ResearchFact
{
    public string Product = "";
    public double OrderedAt = -1, SurveyedBy = -1, BilledAt = -1, NoticeAt = -1;
    public string Notice = "";
    public bool ReadableNextMorning;
}

/// <summary>Drives one arm of Decision 3 from the fixture checkpoint: 06:00 on day 1 to 06:00 on day 4. Native time
/// advances under the F1 fence (<see cref="AccountingClock"/>); at every whole native minute, once the ending epoch has
/// drained, the scenario runs the checkpoint oracles (hourly), publishes the inputs stamped at that minute (F6a's
/// <see cref="InputSchedule"/>), advances the Street Food stand-in, places research orders and, at native midnight, runs
/// the agency bill. Reload arms save and reload into a new simulator at each <see cref="CityReload"/> state.</summary>
internal sealed class CityScenario
{
    public const int Days = 3;
    private readonly CityCheckpoint checkpoint;
    private readonly CityArm arm;
    private readonly CityNative native;
    private readonly CityRun run = new();
    private readonly InputSchedule schedule;
    private readonly (double Minute, string Product)[] orders = CityFixture.ResearchOrders();
    private readonly CityOracles.Identity identity = new();
    private readonly HashSet<CityReload> pendingReloads = new();
    private readonly List<(long Epoch, InterventionInput Input)> published = new();
    private readonly Dictionary<string, ResearchOrderState> researchStates = new(StringComparer.Ordinal);
    private readonly IRoutePlanner planner;
    private CitySimulator city = null!;
    private ResearchChargeSession billing = null!;
    private ResearchNoticeTracker notices = new();
    private double clock;
    private readonly long end;
    private bool dayReloadDue;
    private bool skippedOne;
    private long missesBeforeReload;

    private CityScenario(CityCheckpoint checkpoint, CityArm arm)
    {
        this.checkpoint = checkpoint; this.arm = arm;
        run.Arm = arm.Name;
        native = new CityNative(checkpoint.Size);
        end = (long)CityFixture.StartMinute + arm.Days * 1440L;
        var read = SaveCodec.Read(checkpoint.Payload);
        if (read.Status != SaveReadStatus.Loaded) throw new CityViolation("fixture refused: " + read.Error);
        schedule = new InputSchedule(CityFixture.Inputs(read.State!).Where(i => i.Minute < end), CityFixture.StartMinute, end);
        run.Inputs = schedule.Inputs;
        planner = arm.Reference ? new CityFixture.SyncWalk() : new F5hIntegratedAcceptanceTests.AdmittedPortalPlanner();
        if (arm.Reloads) foreach (CityReload state in Enum.GetValues(typeof(CityReload))) pendingReloads.Add(state);
        native.Shops.Booked = row => run.Ledger.Add(row);
        native.Api.Booked = row => run.Ledger.Add(row);
        Start(read.State!);
    }

    public static CityRun Run(CityCheckpoint checkpoint, CityArm arm)
    {
        var timer = Stopwatch.StartNew();
        var scenario = new CityScenario(checkpoint, arm);
        if (arm.Reference) scenario.Drained(); else scenario.Frames();
        scenario.Finish();
        scenario.run.Seconds = timer.Elapsed.TotalSeconds;
        return scenario.run;
    }

    // ---- City lifecycle ----

    private void Start(CityState state)
    {
        city = new CitySimulator(state, planner);
        native.Vendor = new StreetFoodVendorMarket(city, native.Api, "", native.Sampler, native.Receipts, () => clock, () => true);
        native.Markets = new FoodMarkets(state.Food!, native.Shops, native.Vendor, id => city.FindPlace(id).Position);
        city.AttachFoodMarket(native.Markets, rivalSettings: native.Shops.RivalSettings);
        city.AttachPopulationFill(checkpoint.Size.Catalogue);
        city.AttachResearch(state.Research);
        city.AttachWorkplaceAvailability(native.Shops);
        AttachPostingCapture();
        city.ActivityObserved += (id, person, minute) => run.Ledger.Add("a|" + CityOracles.M(minute) + "|" + id + "|" + person.Phase + "|" + person.PlaceId + "|" + person.Activity);
        city.RoutineObserved += r => run.Ledger.Add("r|" + CityOracles.M(r.Minute) + "|" + r.CitizenId + "|" + r.DestinationId + "|" + r.Outcome + "|" + r.Reason);
        city.FoodDecisionObserved += t => run.Ledger.Add("d|" + CityOracles.M(city.View.Minute) + "|" + t.Intent.ConsumerId + "|" + t.Intent.Id + "|"
            + t.Intent.Status + "|" + t.Intent.BusinessId + "|" + t.Intent.ProductId);
        billing = new ResearchChargeSession(state.Research, native.Port, () => true, unit => unit());
        notices = new ResearchNoticeTracker();
    }

    private void Reload(CityReload reason)
    {
        native.Vendor!.CompleteForSave();
        // The miss counter belongs to the simulator being replaced: keep it for the whole run.
        missesBeforeReload += city.HistoricalClockMisses;
        if (missesBeforeReload != 0) throw new CityViolation("historical clock miss before the reload at " + reason);
        string saved = SaveCodec.Write(city.State);
        var read = SaveCodec.Read(saved);
        if (read.Status != SaveReadStatus.Loaded) throw new CityViolation("reload refused at " + reason + ": " + read.Error);
        if (SaveCodec.Write(read.State!) != saved) throw new CityViolation("reload round trip changed the save at " + reason);
        // A native save carries the receipt dictionary next to the population bytes.
        native.Receipts = new Dictionary<string, string>(native.Receipts, StringComparer.Ordinal);
        Start(read.State!);
        while (native.Vendor!.Prepare((long)Math.Floor(clock))) { }
        // The fill-unit counter belongs to the simulator, so after the load the pending fill is the state that must survive.
        bool kept = reason == CityReload.BetweenFillUnits ? city.PopulationFillPending : reason == CityReload.DayBoundary || Holds(reason);
        if (!kept) throw new CityViolation("reloaded city lost its " + reason + " state");
        run.Reloads.Add((reason, city.View.Minute));
        pendingReloads.Remove(reason);
    }

    private bool Holds(CityReload state)
    {
        // Meals are not in the city view (F9e); research, vendor rows and persons are read through it.
        var s = city.State; var view = city.View;
        switch (state)
        {
            case CityReload.BetweenFillUnits: return city.PopulationFillPending && city.PopulationFillUnits > 0;
            case CityReload.UncertainReceipt: return s.Food!.Residents.Any(m => m.Status == MealStatus.Uncertain);
            case CityReload.ResearchPendingBilling: return view.Research.OpenOrders.Any(o => o.State == ResearchOrderState.Surveyed);
            case CityReload.VendorLeaseOpen:
                var row = view.FindVendor(CityFixture.Vehicle);
                var vehicle = StreetFoodHooks.Vehicles.Find(v => v.Id == CityFixture.Vehicle);
                return row != null && row.AuthorityDesired && vehicle != null && vehicle.Open && vehicle.Owner == VendorNames.Owner;
            case CityReload.HeldMealDecision: return s.Food!.Residents.Any(m => m.Decision != null);
            case CityReload.MidJourney: return view.ResidentList.Any(c => c.Phase == CitizenPhase.Travelling);
            default: return false;
        }
    }

    private void MaybeReload()
    {
        if (!arm.Reloads) return;
        if (pendingReloads.Contains(CityReload.BetweenFillUnits) && Holds(CityReload.BetweenFillUnits)) { Reload(CityReload.BetweenFillUnits); return; }
        if (dayReloadDue) { dayReloadDue = false; Reload(CityReload.DayBoundary); return; }
        if (city.View.Minute < CityFixture.StartMinute + 30) return;
        foreach (var state in new[] { CityReload.UncertainReceipt, CityReload.ResearchPendingBilling,
                     CityReload.VendorLeaseOpen, CityReload.HeldMealDecision, CityReload.MidJourney })
            if (pendingReloads.Contains(state) && Holds(state)) { Reload(state); return; }
    }

    // ---- Native minute boundary ----

    /// <summary>Everything native that happens as the clock enters <paramref name="epoch"/>. The ending epoch has drained.</summary>
    private void Crossing(long epoch)
    {
        if (!city.IsAccountingEpochDrained(epoch)) throw new CityViolation("ending epoch not drained at " + epoch);
        if (city.View.Minute >= epoch) throw new CityViolation("epoch " + epoch + " observed before its inputs");
        clock = epoch; native.SetClock(epoch);
        if (epoch % 60 == 0) Checkpoint(epoch);
        if (epoch % 1440 == 0)
        {
            run.DaySaves.Add((epoch, SaveCodec.Write(city.State)));
            dayReloadDue = arm.Reloads;
        }
        if (arm.Fault == CityFault.ReorderedPostings && epoch == 2 * 1440) ReorderNextPostings();
        Publish(epoch);
        Vendor(epoch);
        Research(epoch);
    }

    // ---- Posting capture ----

    private static readonly System.Reflection.FieldInfo BudgetDueField = typeof(FoodCommerce).GetField("budgetDue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
    private FoodCommerce food = null!;
    private object budgetDue = null!;
    private System.Reflection.PropertyInfo budgetMin = null!, budgetCount = null!, budgetMinute = null!;

    /// <summary>The harness posts one wallet per posting unit (Core's own test bound, <c>BudgetPostingsPerEvent</c>), so the
    /// wallet a unit posts is the posting queue's minimum just before it. Results do not depend on the bound; the reference
    /// and every arm use the same one.</summary>
    private void AttachPostingCapture()
    {
        food = F5hIntegratedAcceptanceTests.Food(city);
        food.BudgetPostingsPerEvent = 1;
        budgetDue = BudgetDueField.GetValue(food)!;
        budgetMin = budgetDue.GetType().GetProperty("Min")!;
        budgetCount = budgetDue.GetType().GetProperty("Count")!;
        budgetMinute = budgetDue.GetType().GetProperty("Minute")!;
    }

    /// <summary>One simulator unit. A unit that posts a wallet (daily bill, income or shift wage) appends that posting to the
    /// ledger at its execution point: its scheduled minute, the run-wide posting sequence, the wallet and its posted fields.</summary>
    private AdvanceResult Step(double target)
    {
        var next = (int)budgetCount.GetValue(budgetDue)! > 0 ? (ResidentMeal)budgetMin.GetValue(budgetDue)! : null;
        double due = (double)budgetMinute.GetValue(budgetDue)!;
        long before = food.BudgetPostings;
        var advanced = city.AdvanceFrameSlice(target, 1);
        long posted = food.BudgetPostings - before;
        if (posted > 1) throw new CityViolation("a unit posted " + posted + " wallets");
        if (posted == 1)
        {
            run.Postings++;
            if (due > city.View.Minute) throw new CityViolation("wallet " + next!.ResidentId + " posted before its minute " + CityOracles.M(due));
            // The posting's own minute (daily bill or shift end); the simulator clock at that unit depends on the frame target.
            run.Ledger.Add("p|" + CityOracles.M(due) + "|" + run.Postings.ToString(CultureInfo.InvariantCulture) + "|" + next!.ResidentId
                + "|" + CityOracles.Posting(next.Budget!));
        }
        return advanced;
    }

    /// <summary>Fault: the next posting minute's first two wallets swap places in the posting queue, so they post in the other
    /// order with the same amounts. Balances and saves stay equal; only the execution order changes.</summary>
    private void ReorderNextPostings()
    {
        var items = (System.Collections.IList)budgetDue.GetType().GetField("items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(budgetDue)!;
        var entry = items[0]!.GetType();
        var minute = entry.GetField("Minute", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var index = entry.GetField("Index", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        double first = (double)minute.GetValue(items[0])!;
        int other = -1;
        for (int i = 1; i < items.Count && other < 0; i++) if ((double)minute.GetValue(items[i])! == first) other = i;
        if (other < 0) throw new CityViolation("no two wallets post at minute " + first);
        (items[0], items[other]) = (items[other], items[0]);
        index.SetValue(items[0], 0); index.SetValue(items[other], other);
    }

    private void Checkpoint(long epoch)
    {
        string context = arm.Name + " at " + epoch.ToString(CultureInfo.InvariantCulture);
        run.Checkpoints++;
        run.Ledger.Add(CityOracles.Checkpoint(city, identity, epoch, context));
        CityOracles.Conserved(city.State, native, context);
        CityOracles.Research(city.View.Research, native.Port, context);
        CityOracles.Vendor(native.Api, context);
        CityOracles.Schedule(schedule, published, epoch, context);
        if (epoch % 1440 == 360)
            foreach (var fact in run.Research.Values.Where(f => f.BilledAt >= 0 && f.BilledAt == epoch - 360))
                fact.ReadableNextMorning = city.View.Research.KeptOrders.Any(o => o.ProductCode == fact.Product && o.HasReport);
    }

    private void Publish(long epoch)
    {
        if (!schedule.HasInputsAt(epoch)) return;
        foreach (var input in schedule.Inputs.Where(i => (long)i.Minute == epoch))
        {
            if (arm.Fault == CityFault.SkippedInput && !skippedOne) { skippedOne = true; continue; }
            native.Shops.Apply(input);
            published.Add((epoch, input));
            run.InputsPublished++;
            run.Ledger.Add("i|" + input.Json());
        }
        CityOracles.Schedule(schedule, published, epoch + 1, arm.Name + " at " + epoch.ToString(CultureInfo.InvariantCulture));
    }

    private void Vendor(long epoch)
    {
        native.EnterMinute(epoch);
        long day = epoch / 1440 * 1440;
        if (epoch == day + (long)CityFixture.VehicleOpens) { StreetFoodHooks.Open(CityFixture.Vehicle, (int)(CityFixture.VehicleCloses - CityFixture.VehicleOpens)); run.Ledger.Add("vs|" + epoch + "|open"); }
        if (epoch == day + (long)CityFixture.VehicleCloses) { StreetFoodHooks.Close(CityFixture.Vehicle); run.Ledger.Add("vs|" + epoch + "|close"); }
        if (epoch == (long)CityFixture.WithdrawMinute)
        {
            if (!native.Vendor!.Withdraw(CityFixture.Vehicle)) throw new CityViolation("vendor withdrawal refused");
            run.Ledger.Add("vs|" + epoch + "|withdraw");
        }
        if (arm.Fault == CityFault.VendorSaleOutsideLease && epoch == 1440 + 12 * 60) native.LeaseFault = true;
        if (arm.Fault == CityFault.DuplicateSale && epoch == 1440 + 12 * 60) native.Shops.InjectDuplicate = true;
        while (native.Vendor!.Prepare(epoch)) { }
        if (run.HandbackMinute == null && epoch > (long)CityFixture.WithdrawMinute)
        {
            var vehicle = StreetFoodHooks.Find(CityFixture.Vehicle);
            if (vehicle.Open && vehicle.Owner.Length == 0) { run.HandbackMinute = epoch; run.HandbackOwner = vehicle.Owner; run.Ledger.Add("vs|" + epoch + "|handed-back"); }
        }
    }

    private void Research(long epoch)
    {
        native.Port.Day = (int)(epoch / 1440);
        if (epoch % 1440 == 0)
        {
            if (arm.Fault == CityFault.ResearchChargeWithoutReport && epoch == 2 * 1440) native.Port.ChargeWithoutReport = true;
            native.Port.Boundary();
            notices.Before(city.State.Research);
            var tally = billing.Boundary();
            notices.After(n =>
            {
                run.Ledger.Add("n|" + epoch + "|" + n.OrderId + "|" + n.Kind);
                var fact = run.Research.Values.Single(f => city.View.Research.Find(n.OrderId)?.ProductCode == f.Product);
                fact.Notice = n.Kind.ToString(); fact.NoticeAt = epoch;
            });
            if (tally.Attempts > 0) run.Ledger.Add("b|" + epoch + "|" + tally.Attempts + "|" + tally.Paid + "|" + tally.Declined);
        }
        var research = city.View.Research;
        foreach (var order in research.OpenOrders.Concat(research.KeptOrders).Concat(research.ClosedOrders))
        {
            if (researchStates.TryGetValue(order.Id, out var was) && was == order.State) continue;
            researchStates[order.Id] = order.State;
            run.Ledger.Add("rs|" + epoch + "|" + order.Id + "|" + order.State);
            if (!run.Research.TryGetValue(order.ProductCode, out var fact)) continue;
            if (order.State == ResearchOrderState.Surveyed) fact.SurveyedBy = epoch;
            if (order.State == ResearchOrderState.Paid) fact.BilledAt = epoch;
        }
        foreach (var (minute, product) in orders)
        {
            if ((long)Math.Floor(minute) != epoch) continue;
            var refusal = city.Research!.TryOrder(product, CityFixture.ResearchBusiness, CityFixture.Agency, false, CityFixture.ResearchRadius, epoch, out var order, out var ledger);
            if (refusal != ResearchSurveyRefusal.None) throw new CityViolation("research order refused: " + refusal + " " + ledger);
            run.Research[product] = new ResearchFact { Product = product, OrderedAt = epoch };
            researchStates[order!.Id] = order.State;
            run.Ledger.Add("q|" + epoch + "|" + order.Id + "|" + product);
        }
    }

    // ---- Arms ----

    private void Drained()
    {
        long start = (long)CityFixture.StartMinute;
        Crossing0(start);
        for (long epoch = start + 1; epoch <= end; epoch++)
        {
            double ending = AccountingClock.PreviousEpochTarget(epoch);
            int guard = 0;
            while (!Step(ending).CaughtUp) if (++guard > 10_000_000) throw new CityViolation("nonterminating epoch " + epoch);
            Crossing(epoch);
            if (epoch == end) break;
            if (!city.BeginAccountingObservations(epoch)) throw new CityViolation("observation epoch not published at " + epoch);
        }
    }

    /// <summary>The checkpoint's own minute: the hourly oracles and the stand-in's first poll, before any native time passes.</summary>
    private void Crossing0(long start)
    {
        clock = start; native.SetClock(start);
        Checkpoint(start);
        native.EnterMinute(start);
        while (native.Vendor!.Prepare(start)) { }
        if (!city.BeginAccountingObservations(start)) throw new CityViolation("initial epoch not published");
    }

    private void Frames()
    {
        long start = (long)CityFixture.StartMinute;
        Crossing0(start);
        var admitted = (F5hIntegratedAcceptanceTests.AdmittedPortalPlanner)planner;
        double nativeMinute = start;
        while (true)
        {
            if (++run.Frames > 10_000_000) throw new CityViolation("nonterminating arm");
            admitted.NewFrame();
            long started = Stopwatch.GetTimestamp();
            if (!AccountingClock.Compatible(city.View.Minute, nativeMinute)) throw new CityViolation("input epoch escaped at " + nativeMinute);
            if (!city.IsAccountingEpochDrained(nativeMinute)) throw new CityViolation("unfinished ending epoch escaped at " + nativeMinute);
            bool ready = city.IsCaughtUpTo(AccountingClock.Target(nativeMinute));
            bool skipping = arm.Skip && nativeMinute >= 2 * 1440 + 13 * 60 && nativeMinute < 2 * 1440 + 15 * 60;
            float allowed = AccountingClock.Limit(ready, nativeMinute, skipping ? 120f : (float)(30.0 / arm.Fps));
            int hour = (int)Math.Floor(nativeMinute / 60); float minute = (float)(nativeMinute - hour * 60.0) + allowed;
            while (minute >= 60) { minute -= 60; hour++; }
            double next = hour * 60.0 + minute;
            long before = (long)Math.Floor(nativeMinute), after = (long)Math.Floor(next);
            if (after > before + 1) throw new CityViolation("native clock crossed two epochs at " + nativeMinute);
            if (after > before) Crossing(after);
            if (after >= end) break;
            nativeMinute = next; clock = next; native.SetClock(next);
            if (!city.BeginAccountingObservations(nativeMinute)) throw new CityViolation("observation epoch not published at " + nativeMinute);
            double target = Math.Max(city.View.Minute, AccountingClock.Target(nativeMinute));
            run.MaxLag = Math.Max(run.MaxLag, target - city.View.Minute);
            int budget = skipping ? 128 : arm.Budget;
            double milliseconds = skipping ? 16 : arm.Milliseconds;
            double Elapsed() => (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            for (int unit = 0; unit < budget; unit++)
            {
                if (city.PlanningPending)
                {
                    bool done = city.ContinuePlanning(Math.Max(0, milliseconds - Elapsed()));
                    if (!done) { if (Elapsed() >= milliseconds || admitted.AdmissionExhausted) break; continue; }
                    if (Elapsed() >= milliseconds) break;
                    continue;
                }
                var advanced = Step(target);
                // A reload arm ends the frame between two fill units, so the save falls inside the boundary's fill.
                if (pendingReloads.Contains(CityReload.BetweenFillUnits) && Holds(CityReload.BetweenFillUnits)) break;
                if (advanced.PlanningPending) { if (Elapsed() >= milliseconds) break; continue; }
                if (advanced.CaughtUp || Elapsed() >= milliseconds) break;
            }
            city.SynchronizeSnapshot();
            MaybeReload();
        }
    }

    private void Finish()
    {
        city.SynchronizeSnapshot();
        if (!city.OccupancyMatchesFullCount()) throw new CityViolation("occupancy differs from its full count");
        run.HistoricalClockMisses = missesBeforeReload + city.HistoricalClockMisses;
        run.ResearchChargeDays = native.Port.Queue.Select(r => (r.Tag!, r.Day)).ToList();
        run.HistoricalCheckouts = native.Shops.HistoricalCheckouts;
        run.Orders = native.Shops.Orders.ToList();
        run.VendorSales = native.Api.Sales.ToList();
        run.ResearchCharges = native.Port.Queue.Count;
        var vehicle = city.View.FindVendor(CityFixture.Vehicle);
        if (vehicle != null) run.VendorBusiness = vehicle.BusinessId;
        CityOracles.Schedule(schedule, published, end + 1, arm.Name + " at the end");
        run.Final = SaveCodec.Write(city.State);
        run.State = city.State;
    }
}
